import { createServer } from 'node:http';
import { readFileSync } from 'node:fs';
import { createHash, randomBytes, scrypt, timingSafeEqual } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import express from 'express';
import { Server } from 'socket.io';
import { Game, type Song } from './game.ts';
import { networkInterfaces } from 'node:os';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const port = Number(process.env.PORT ?? 3000);
const clientId = process.env.SPOTIFY_CLIENT_ID ?? '';
const hostPasswordHash = process.env.HOST_PASSWORD_HASH ?? '';
const sessionCookieName = 'hitster_host_session';
const sessionDurationMs = 12 * 60 * 60 * 1000;
const hostSessions = new Map<string, number>();
const loginAttempts = new Map<string, { count: number; resetAt: number }>();
const spotifyAuthFlows = new Map<string, { verifier: string; state: string; redirectUri: string; expiresAt: number }>();
const publicUrl = process.env.PUBLIC_URL?.replace(/\/+$/, '');

function parsePasswordHash(hash: string): { salt: Buffer; digest: Buffer } | null {
  if (!hash) return null;
  const [algorithm, salt, digest, ...extra] = hash.split('$');
  if (algorithm !== 'scrypt' || !salt || !digest || extra.length > 0) {
    throw new Error('HOST_PASSWORD_HASH muss das Format scrypt$<salt>$<hash> haben');
  }
  const saltBuffer = Buffer.from(salt, 'base64url');
  const digestBuffer = Buffer.from(digest, 'base64url');
  if (saltBuffer.length < 16 || digestBuffer.length !== 64) {
    throw new Error('HOST_PASSWORD_HASH enthält einen ungültigen Salt oder Hash');
  }
  return { salt: saltBuffer, digest: digestBuffer };
}

const configuredPassword = parsePasswordHash(hostPasswordHash);

function cookieValue(cookieHeader: string | undefined, name: string): string | undefined {
  return cookieHeader?.split(';').map((part) => part.trim())
    .find((part) => part.startsWith(`${name}=`))
    ?.slice(name.length + 1);
}

function validSession(token: string | undefined): boolean {
  if (!token) return false;
  const expiresAt = hostSessions.get(token);
  if (!expiresAt) return false;
  if (expiresAt <= Date.now()) {
    hostSessions.delete(token);
    return false;
  }
  return true;
}

function requestHasHostSession(request: express.Request): boolean {
  return validSession(cookieValue(request.headers.cookie, sessionCookieName));
}

function requireHostSession(request: express.Request, response: express.Response, next: express.NextFunction): void {
  if (!requestHasHostSession(request)) {
    response.status(401).json({ error: 'Bitte zuerst das Host-Passwort eingeben.' });
    return;
  }
  next();
}

function scryptPassword(password: string, salt: Buffer): Promise<Buffer> {
  return new Promise((resolve, reject) => {
    scrypt(password, salt, 64, (error, derivedKey) => {
      if (error) reject(error);
      else resolve(derivedKey);
    });
  });
}

function isSecureRequest(request: express.Request): boolean {
  return request.secure || request.headers['x-forwarded-proto'] === 'https';
}

function loadSongs(): Song[] {
  return JSON.parse(readFileSync(path.join(root, 'data', 'songs.json'), 'utf8')) as Song[];
}

const app = express();
app.set('trust proxy', process.env.TRUST_PROXY === '1' ? 1 : false);
app.use(express.json({ limit: '10kb' }));
app.use(express.static(path.join(root, 'public')));

app.get('/healthz', (_req, res) => {
  res.status(204).end();
});

app.get('/api/host-auth', (req, res) => {
  res.setHeader('Cache-Control', 'no-store');
  res.json({ authenticated: requestHasHostSession(req), configured: configuredPassword !== null });
});

app.post('/api/host-login', async (req, res) => {
  res.setHeader('Cache-Control', 'no-store');
  if (!configuredPassword) {
    res.status(503).json({ error: 'Host-Passwort ist auf dem Server nicht konfiguriert.' });
    return;
  }

  const now = Date.now();
  const address = req.ip ?? req.socket.remoteAddress ?? 'unknown';
  let attempts = loginAttempts.get(address);
  if (!attempts || attempts.resetAt <= now) {
    attempts = { count: 0, resetAt: now + 15 * 60 * 1000 };
    loginAttempts.set(address, attempts);
  }
  if (attempts.count >= 10) {
    res.status(429).json({ error: 'Zu viele Anmeldeversuche. Bitte in 15 Minuten erneut versuchen.' });
    return;
  }
  attempts.count++;

  const password = req.body?.password;
  if (typeof password !== 'string' || password.length > 1024) {
    res.status(400).json({ error: 'Bitte ein gültiges Passwort eingeben.' });
    return;
  }
  const candidate = await scryptPassword(password, configuredPassword.salt);
  if (!timingSafeEqual(candidate, configuredPassword.digest)) {
    res.status(401).json({ error: 'Das Passwort ist falsch.' });
    return;
  }

  loginAttempts.delete(address);
  const token = randomBytes(32).toString('base64url');
  hostSessions.set(token, now + sessionDurationMs);
  const secure = isSecureRequest(req) ? '; Secure' : '';
  res.setHeader(
    'Set-Cookie',
    `${sessionCookieName}=${token}; HttpOnly; SameSite=Lax; Path=/; Max-Age=${sessionDurationMs / 1000}${secure}`,
  );
  res.json({ ok: true });
});

app.post('/api/host-logout', requireHostSession, (req, res) => {
  res.setHeader('Cache-Control', 'no-store');
  const token = cookieValue(req.headers.cookie, sessionCookieName);
  if (token) hostSessions.delete(token);
  res.setHeader('Set-Cookie', `${sessionCookieName}=; HttpOnly; SameSite=Lax; Path=/; Max-Age=0${isSecureRequest(req) ? '; Secure' : ''}`);
  res.json({ ok: true });
});

app.post('/api/spotify-auth', requireHostSession, (req, res) => {
  const redirectUri = req.body?.redirect_uri;
  if (typeof redirectUri !== 'string') {
    res.status(400).json({ error: 'Spotify-Redirect-URI fehlt.' });
    return;
  }

  let callback: URL;
  try {
    callback = new URL(redirectUri);
  } catch {
    res.status(400).json({ error: 'Spotify-Redirect-URI ist ungültig.' });
    return;
  }
  if (!['http:', 'https:'].includes(callback.protocol) || callback.pathname !== '/host.html' || callback.search || callback.hash) {
    res.status(400).json({ error: 'Spotify-Redirect-URI muss auf /host.html zeigen.' });
    return;
  }

  const sessionToken = cookieValue(req.headers.cookie, sessionCookieName)!;
  const verifier = randomBytes(48).toString('base64url');
  const state = randomBytes(24).toString('base64url');
  const challenge = createHash('sha256').update(verifier).digest('base64url');
  spotifyAuthFlows.set(sessionToken, {
    verifier,
    state,
    redirectUri,
    expiresAt: Date.now() + 10 * 60 * 1000,
  });
  res.setHeader('Cache-Control', 'no-store');
  res.json({ challenge, state });
});

app.get('/api/config', requireHostSession, (_req, res) => {
  res.json({
    clientId,
    songCount: loadSongs().length,
    playerUrls: publicUrl
      ? [`${publicUrl}/play.html`]
      : localIPs().map((ip) => `http://${ip}:${port}/play.html`),
  });
});

app.post('/api/token', requireHostSession, async (req, res) => {
  res.setHeader('Cache-Control', 'no-store');
  const grantType = req.body?.grant_type;
  const body = new URLSearchParams({ client_id: clientId });

  if (grantType === 'authorization_code') {
    const sessionToken = cookieValue(req.headers.cookie, sessionCookieName)!;
    const flow = spotifyAuthFlows.get(sessionToken);
    spotifyAuthFlows.delete(sessionToken);
    if (!flow || flow.expiresAt <= Date.now() || typeof req.body?.state !== 'string' || req.body.state !== flow.state) {
      res.status(400).json({ error: 'Spotify-Anmeldung abgelaufen oder ungültig. Bitte erneut anmelden.' });
      return;
    }
    if (typeof req.body?.code !== 'string' || !req.body.code) {
      res.status(400).json({ error: 'Spotify hat keinen Autorisierungscode zurückgegeben.' });
      return;
    }
    body.set('grant_type', grantType);
    body.set('code', req.body.code);
    body.set('redirect_uri', flow.redirectUri);
    body.set('code_verifier', flow.verifier);
  } else if (grantType === 'refresh_token' && typeof req.body?.refresh_token === 'string') {
    body.set('grant_type', grantType);
    body.set('refresh_token', req.body.refresh_token);
  } else {
    res.status(400).json({ error: 'Spotify-Token-Anfrage ist ungültig.' });
    return;
  }

  const response = await fetch('https://accounts.spotify.com/api/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body,
  });
  res.status(response.status).json(await response.json());
});

const httpServer = createServer(app);
const io = new Server(httpServer);
const games = new Map<string, Game>();

io.use((socket, next) => {
  socket.data.hostSessionToken = cookieValue(socket.handshake.headers.cookie, sessionCookieName);
  next();
});

function newCode(): string {
  const letters = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
  let code: string;
  do {
    code = Array.from({ length: 4 }, () => letters[Math.floor(Math.random() * letters.length)]).join('');
  } while (games.has(code));
  return code;
}

function broadcast(game: Game): void {
  io.to(game.code).emit('state', game.publicState());
}

function playCurrentTrack(game: Game): void {
  if (game.currentSong && game.phase === 'placing') {
    io.to(`${game.code}:host`).emit('host:play', { trackId: game.currentSong.id });
  }
}

function guarded(handler: (...args: any[]) => unknown) {
  return (...args: any[]) => {
    const ack = typeof args[args.length - 1] === 'function' ? args.pop() : () => {};
    try {
      ack({ ok: true, ...((handler(...args) as object | undefined) ?? {}) });
    } catch (e) {
      ack({ ok: false, error: e instanceof Error ? e.message : String(e) });
    }
  };
}

function localIPs(): string[] {
  return Object.values(networkInterfaces())
    .flat()
    .filter((i): i is NonNullable<typeof i> => !!i && i.family === 'IPv4' && !i.internal)
    .map((i) => i.address);
}

io.on('connection', (socket) => {
  let game: Game | undefined;
  let playerToken: string | undefined;
  let isHost = false;

  socket.on('host:create', guarded(() => {
    if (!validSession(socket.data.hostSessionToken)) {
      throw new Error('Bitte zuerst das Host-Passwort eingeben.');
    }
    game = new Game(newCode(), loadSongs());
    games.set(game.code, game);
    isHost = true;
    socket.join([game.code, `${game.code}:host`]);
    broadcast(game);
    return { code: game.code };
  }));

  socket.on('player:join', guarded((code: string, name: string, token?: string, teamId?: number) => {
    const target = games.get(String(code).toUpperCase());
    if (!target) {
      throw new Error('Raum nicht gefunden');
    }
    let player = token ? target.findPlayer(token) : undefined;
    if (!player) {
      const trimmed = String(name ?? '').trim().slice(0, 20);
      if (!trimmed) {
        throw new Error('Name fehlt');
      }
      if (target.phase !== 'lobby') {
        throw new Error('Spiel läuft bereits');
      }
      player = target.addPlayer(trimmed, teamId);
    }
    player.connected = true;
    game = target;
    playerToken = player.token;
    socket.join(target.code);
    broadcast(target);
    return { token: player.token };
  }));

  socket.on('host:teams', guarded((count: number) => {
    if (!validSession(socket.data.hostSessionToken) || !isHost || !game) throw new Error('Nur der angemeldete Host kann Teams festlegen');
    game.setTeamCount(Number(count));
    broadcast(game);
  }));

  socket.on('player:team', guarded((teamId: number) => {
    if (!game || !playerToken) throw new Error('Nicht im Spiel');
    game.moveToTeam(playerToken, Number(teamId));
    broadcast(game);
  }));

  socket.on('host:start', guarded(() => {
    if (!validSession(socket.data.hostSessionToken) || !isHost || !game) throw new Error('Nur der angemeldete Host kann starten');
    game.start();
    broadcast(game);
    playCurrentTrack(game);
  }));

  socket.on('player:place', guarded((index: number) => {
    if (!game || !playerToken) throw new Error('Nicht im Spiel');
    game.place(playerToken, index);
    broadcast(game);
  }));

  socket.on('game:next', guarded(() => {
    if (!game) throw new Error('Kein Spiel');
    if (isHost && !validSession(socket.data.hostSessionToken)) throw new Error('Host-Anmeldung abgelaufen. Bitte erneut anmelden.');
    if (!isHost && !game.canAct(playerToken)) throw new Error('Nur Host oder Spielführer');
    game.next();
    broadcast(game);
    playCurrentTrack(game);
  }));

  socket.on('host:kick', guarded((token: string) => {
    if (!validSession(socket.data.hostSessionToken) || !isHost || !game || game.phase !== 'lobby') throw new Error('Nur der angemeldete Host kann Spieler entfernen');
    game.removePlayer(token);
    broadcast(game);
  }));

  socket.on('disconnect', () => {
    const player = game && playerToken ? game.findPlayer(playerToken) : undefined;
    if (game && player) {
      player.connected = false;
      broadcast(game);
    }
  });
});

httpServer.listen(port, '0.0.0.0', () => {
  const ips = localIPs();
  console.log(`Hitster läuft:`);
  console.log(`  Host (lokal):  http://127.0.0.1:${port}`);
  for (const ip of ips) {
    console.log(`  Mitspieler:    http://${ip}:${port}`);
  }
  if (!clientId) {
    console.log('Hinweis: SPOTIFY_CLIENT_ID fehlt in .env, der Host kann keine Musik abspielen.');
  }
  if (!configuredPassword) {
    console.warn('Hinweis: HOST_PASSWORD_HASH fehlt. Die Host-Funktionen bleiben gesperrt.');
  }
});
