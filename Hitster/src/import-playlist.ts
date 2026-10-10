import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { exec } from 'node:child_process';
import { createHash, randomBytes } from 'node:crypto';
import { createServer } from 'node:http';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import type { Song } from './game.ts';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const songsFile = path.join(root, 'data', 'songs.json');
const cacheFile = path.join(root, 'data', 'year-cache.json');
const userAgent = 'HitsterLocal/0.1 ( local-hobby-project )';

interface SpotifyTrack {
  id: string | null;
  name: string;
  artists: { name: string }[];
  album: { release_date: string };
  external_ids?: { isrc?: string };
  is_local?: boolean;
}

interface YearInfo {
  year: number;
  source: 'musicbrainz' | 'spotify';
}

const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

function playlistId(input: string): string {
  const match = input.match(/playlist[/:]([A-Za-z0-9]+)/);
  return match ? match[1] : input.trim();
}

async function spotifyToken(): Promise<string> {
  const id = process.env.SPOTIFY_CLIENT_ID;
  if (!id) {
    throw new Error('SPOTIFY_CLIENT_ID muss in .env stehen');
  }

  const redirectUri = 'http://127.0.0.1:8888/callback';
  const codeVerifier = randomBytes(48).toString('base64url');
  const codeChallenge = createHash('sha256').update(codeVerifier).digest('base64url');
  const state = randomBytes(16).toString('base64url');
  const authorizationUrl = new URL('https://accounts.spotify.com/authorize');
  authorizationUrl.search = new URLSearchParams({
    response_type: 'code',
    client_id: id,
    redirect_uri: redirectUri,
    scope: 'playlist-read-private playlist-read-collaborative',
    state,
    code_challenge_method: 'S256',
    code_challenge: codeChallenge,
  }).toString();

  const code = await new Promise<string>((resolve, reject) => {
    const server = createServer((request, response) => {
      const callbackUrl = new URL(request.url ?? '/', redirectUri);
      if (request.method !== 'GET' || callbackUrl.pathname !== '/callback') {
        response.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
        response.end('Nicht gefunden');
        return;
      }

      const fail = (message: string) => {
        response.writeHead(400, { 'Content-Type': 'text/plain; charset=utf-8' });
        response.end(message, () => {
          server.close((error) => reject(error ?? new Error(message)));
        });
      };

      if (callbackUrl.searchParams.get('state') !== state) {
        fail('Anmeldung fehlgeschlagen: Der Spotify-Status ist ungültig. Bitte erneut versuchen.');
        return;
      }
      const authorizationError = callbackUrl.searchParams.get('error');
      if (authorizationError) {
        fail('Spotify-Anmeldung abgebrochen. Bitte erneut versuchen und den Zugriff bestätigen.');
        return;
      }
      const authorizationCode = callbackUrl.searchParams.get('code');
      if (!authorizationCode) {
        fail('Spotify-Anmeldung fehlgeschlagen: Es wurde kein Autorisierungscode empfangen.');
        return;
      }

      response.writeHead(200, { 'Content-Type': 'text/plain; charset=utf-8' });
      response.end('Fertig, du kannst dieses Fenster schließen.', () => {
        server.close((error) => {
          if (error) {
            reject(new Error(`Spotify-Anmeldung fehlgeschlagen: ${error.message}`));
          } else {
            resolve(authorizationCode);
          }
        });
      });
    });

    server.once('error', (error) => reject(new Error(`Spotify-Anmeldeserver konnte nicht gestartet werden: ${error.message}`)));
    server.listen(8888, '127.0.0.1', () => {
      console.log(`Spotify-Autorisierung: ${authorizationUrl}`);
      exec(`start "" "${authorizationUrl}"`, (error) => {
        if (error) {
          console.error(`Browser konnte nicht automatisch geöffnet werden: ${error.message}`);
        }
      });
    });
  });

  let response: Response;
  try {
    response = await fetch('https://accounts.spotify.com/api/token', {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({
        grant_type: 'authorization_code',
        code,
        redirect_uri: redirectUri,
        client_id: id,
        code_verifier: codeVerifier,
      }),
    });
  } catch (error) {
    throw new Error(`Spotify-Token fehlgeschlagen: ${error instanceof Error ? error.message : String(error)}`);
  }
  if (!response.ok) {
    throw new Error(`Spotify-Token fehlgeschlagen (${response.status}): ${await response.text()}`);
  }
  let token: { access_token?: string };
  try {
    token = (await response.json()) as { access_token?: string };
  } catch {
    throw new Error('Spotify-Token fehlgeschlagen: Ungültige Antwort vom Spotify-Server.');
  }
  if (!token.access_token) {
    throw new Error('Spotify-Token fehlgeschlagen: Die Antwort enthielt keinen Zugriffstoken.');
  }
  return token.access_token;
}

async function fetchPlaylistTracks(id: string, token: string): Promise<SpotifyTrack[]> {
  const tracks: SpotifyTrack[] = [];
  for (const endpoint of ['items', 'tracks']) {
    let url: string | null = `https://api.spotify.com/v1/playlists/${id}/${endpoint}?limit=100`;
    let failed = false;
    tracks.length = 0;
    while (url) {
      const response: Response = await fetch(url, { headers: { Authorization: `Bearer ${token}` } });
      if (!response.ok) {
        console.warn(`/${endpoint}: ${response.status} ${await response.text()}`);
        failed = true;
        break;
      }
      const page = (await response.json()) as { items: { track?: SpotifyTrack; item?: SpotifyTrack }[]; next: string | null };
      for (const entry of page.items) {
        const track = entry.track ?? entry.item;
        if (track?.id && !track.is_local) {
          tracks.push(track);
          if (tracks.length === 100) {
            return tracks;
          }
        }
      }
      url = page.next;
    }
    if (!failed) {
      return tracks;
    }
  }
  throw new Error('Playlist nicht lesbar (sie muss dem eingeloggten Spotify-Konto gehören oder dort kollaborativ sein; von Spotify kuratierte Playlists funktionieren nicht)');
}

async function musicBrainzYear(track: SpotifyTrack): Promise<number | null> {
  const isrc = track.external_ids?.isrc;
  const artist = track.artists[0]?.name ?? '';
  const queries = [
    ...(isrc ? [`isrc:${isrc}`] : []),
    `recording:"${track.name.replace(/"/g, '')}" AND artist:"${artist.replace(/"/g, '')}"`,
  ];
  for (const query of queries) {
    await sleep(1100);
    const response = await fetch(
      `https://musicbrainz.org/ws/2/recording?query=${encodeURIComponent(query)}&fmt=json&limit=25`,
      { headers: { 'User-Agent': userAgent } },
    );
    if (!response.ok) {
      continue;
    }
    const data = (await response.json()) as { recordings?: { 'first-release-date'?: string }[] };
    const years = (data.recordings ?? [])
      .map((r) => Number.parseInt(r['first-release-date']?.slice(0, 4) ?? '', 10))
      .filter((y) => y >= 1900);
    if (years.length > 0) {
      return Math.min(...years);
    }
  }
  return null;
}

async function main() {
  const input = process.argv[2];
  if (!input) {
    console.log('Aufruf: npm run import -- <Playlist-URL oder -ID>');
    process.exit(1);
  }

  const token = await spotifyToken();
  const tracks = await fetchPlaylistTracks(playlistId(input), token);
  console.log(`${tracks.length} Titel gelesen, ermittle Originaljahre (MusicBrainz, ca. 1-2 s pro neuem Titel)`);

  const cache: Record<string, YearInfo> = existsSync(cacheFile) ? JSON.parse(readFileSync(cacheFile, 'utf8')) : {};
  const songs: Song[] = [];
  let index = 0;
  for (const track of tracks) {
    index++;
    const spotifyYear = Number.parseInt(track.album.release_date.slice(0, 4), 10);
    let info = cache[track.id!];
    if (!info) {
      const mbYear = await musicBrainzYear(track).catch(() => null);
      info = mbYear !== null && mbYear <= spotifyYear
        ? { year: mbYear, source: 'musicbrainz' }
        : { year: spotifyYear, source: 'spotify' };
      cache[track.id!] = info;
      writeFileSync(cacheFile, JSON.stringify(cache, null, 2));
    }
    songs.push({ id: track.id!, title: track.name, artist: track.artists.map((a) => a.name).join(', '), year: info.year });
    console.log(`[${index}/${tracks.length}] ${info.year} (${info.source}) ${track.artists[0]?.name} - ${track.name}`);
  }

  writeFileSync(songsFile, JSON.stringify(songs, null, 2));
  console.log(`\n${songs.length} Songs nach data/songs.json geschrieben. Falsche Jahre dort direkt korrigieren.`);
}

main().catch((e) => {
  console.error(e instanceof Error ? e.message : e);
  process.exit(1);
});
