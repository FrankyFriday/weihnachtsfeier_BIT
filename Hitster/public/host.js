import { esc, timelineHtml, emit, avatarHtml, teamAvatarHtml, cardHtml, progressHtml, confettiHtml } from '/common.js';

const app = document.getElementById('app');
const redirectUri = `${location.origin}/host.html`;
const scopes = 'streaming user-read-email user-read-private user-modify-playback-state user-read-playback-state';

let config = { clientId: '', songCount: 0, playerUrls: [] };
let auth = JSON.parse(localStorage.getItem('hitster-spotify') ?? 'null');
let deviceId = null;
let spotifyPlayer = null;
let socket = null;
let state = null;
let roomCode = null;
let status = '';
let hostAuthenticated = false;

async function login() {
  const response = await fetch('/api/spotify-auth', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ redirect_uri: redirectUri }),
  });
  const flow = await response.json();
  if (!response.ok) throw new Error(flow.error ?? 'Spotify-Anmeldung konnte nicht vorbereitet werden.');
  const params = new URLSearchParams({
    client_id: config.clientId,
    response_type: 'code',
    redirect_uri: redirectUri,
    scope: scopes,
    code_challenge_method: 'S256',
    code_challenge: flow.challenge,
    state: flow.state,
  });
  location.href = `https://accounts.spotify.com/authorize?${params}`;
}

async function requestToken(body) {
  const res = await fetch('/api/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  const data = await res.json();
  if (!res.ok) throw new Error(data.error_description ?? data.error ?? 'Token-Fehler');
  auth = {
    accessToken: data.access_token,
    refreshToken: data.refresh_token ?? auth?.refreshToken,
    expiresAt: Date.now() + (data.expires_in - 60) * 1000,
  };
  localStorage.setItem('hitster-spotify', JSON.stringify(auth));
}

async function accessToken() {
  if (auth && Date.now() >= auth.expiresAt) {
    await requestToken({ grant_type: 'refresh_token', refresh_token: auth.refreshToken });
  }
  return auth.accessToken;
}

async function handleRedirect() {
  const params = new URLSearchParams(location.search);
  const code = params.get('code');
  const authorizationError = params.get('error');
  if (!code && !authorizationError) return;
  history.replaceState({}, '', '/host.html');
  if (authorizationError) {
    throw new Error(`Spotify-Anmeldung abgebrochen: ${params.get('error_description') ?? authorizationError}`);
  }
  const state = params.get('state');
  if (!state) throw new Error('Spotify-Anmeldung fehlgeschlagen: Spotify hat keinen Status zurückgegeben.');
  await requestToken({
    grant_type: 'authorization_code',
    code,
    state,
  });
  localStorage.removeItem('hitster-verifier');
}

function startPlayer() {
  const create = () => {
    spotifyPlayer = new Spotify.Player({
      name: 'Hitster Host',
      getOAuthToken: (cb) => accessToken().then(cb),
      volume: 0.8,
    });
    spotifyPlayer.addListener('ready', ({ device_id }) => {
      deviceId = device_id;
      render();
    });
    spotifyPlayer.addListener('initialization_error', ({ message }) => { status = message; render(); });
    spotifyPlayer.addListener('authentication_error', ({ message }) => { status = `${message} (Premium nötig, ggf. neu anmelden)`; render(); });
    spotifyPlayer.addListener('account_error', ({ message }) => { status = `${message} (Premium nötig)`; render(); });
    spotifyPlayer.connect();
  };
  if (window.Spotify) create();
  else {
    window.onSpotifyWebPlaybackSDKReady = create;
    const sdk = document.createElement('script');
    sdk.src = 'https://sdk.scdn.co/spotify-player.js';
    sdk.onerror = () => {
      status = 'Spotify Web Playback SDK konnte nicht geladen werden.';
      render();
    };
    document.head.appendChild(sdk);
  }
}

async function playTrack(trackId) {
  if (!deviceId) {
    status = 'Kein Spotify-Gerät bereit';
    render();
    return;
  }
  const res = await fetch(`https://api.spotify.com/v1/me/player/play?device_id=${deviceId}`, {
    method: 'PUT',
    headers: { Authorization: `Bearer ${await accessToken()}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({ uris: [`spotify:track:${trackId}`] }),
  });
  status = res.ok ? '' : `Abspielen fehlgeschlagen (${res.status}): ${await res.text()}`;
  render();
}

function connectSocket() {
  socket = io();
  socket.on('state', (s) => { state = s; render(); });
  socket.on('host:play', ({ trackId }) => playTrack(trackId));
}

async function createRoom() {
  await spotifyPlayer.activateElement();
  connectSocket();
  const res = await emit(socket, 'host:create');
  roomCode = res.code;
}

const topbar = (right = '') =>
  `<header class="topbar"><div class="logo"><span class="logo-mark">♪</span>Hitster</div><div class="row">${right}${hostAuthenticated ? '<button id="hostLogout" class="ghost">Abmelden</button>' : ''}</div></header>`;

function renderPasswordGate(message = '') {
  app.innerHTML = `<header class="topbar"><a class="logo" href="/"><span class="logo-mark">♪</span>Hitster</a></header>
    <section class="hero join-hero"><p class="eyebrow"><span></span> GESCHÜTZTER HOST-BEREICH</p><h1>Nur für den <span>Host.</span></h1>
      <p class="lead">Gib das Host-Passwort ein, um Spotify zu verbinden und ein Spiel zu starten.</p></section>
    <form class="panel center-card stack login-card" id="hostPasswordForm" autocomplete="on">
      <label class="field-label" for="hostPassword">Host-Passwort</label>
      <input id="hostPassword" name="password" type="password" placeholder="Passwort" autocomplete="current-password" required>
      <button class="big block" type="submit">Anmelden <span aria-hidden="true">→</span></button>
      ${message ? `<div class="alert">${esc(message)}</div>` : ''}
    </form>`;
  document.getElementById('hostPasswordForm').onsubmit = async (event) => {
    event.preventDefault();
    const button = event.currentTarget.querySelector('button[type="submit"]');
    button.disabled = true;
    try {
      const response = await fetch('/api/host-login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ password: document.getElementById('hostPassword').value }),
      });
      const result = await response.json();
      if (!response.ok) throw new Error(result.error ?? 'Anmeldung fehlgeschlagen');
      location.reload();
    } catch (error) {
      renderPasswordGate(error instanceof Error ? error.message : String(error));
    }
  };
}

const teamOf = (id) => state.teams.find((t) => t.id === id);
const membersOf = (team) => state.players.filter((p) => p.teamId === team.id);
const playerName = (token) => state.players.find((p) => p.token === token)?.name ?? '';

function teamCard(team) {
  const active = team.id === state.currentTeamId && (state.phase === 'placing' || state.phase === 'reveal');
  const members = membersOf(team);
  const info = state.solo
    ? `${team.timeline.length} / ${state.cardsToWin} Karten`
    : `${team.timeline.length} / ${state.cardsToWin} Karten · ${members.length} Spieler`;
  const captain = active && !state.solo && state.captainToken
    ? `<div class="small captain">Spielführer: ${esc(playerName(state.captainToken))}</div>` : '';
  return `<div class="pcard ${active ? 'active' : ''}" style="--hue:${team.hue}">
    <div class="head">${teamAvatarHtml(team)}<div class="who"><b>${esc(team.name)}</b><span class="count">${info}</span>${captain}</div>${active ? '<span class="badge">dran</span>' : ''}</div>
    ${progressHtml(team.timeline.length, state.cardsToWin)}${timelineHtml(team.timeline, false, true)}</div>`;
}

function stageHtml(team) {
  if (state.phase === 'placing') {
    const sub = state.solo
      ? 'Song läuft – wohin gehört er in die Zeitleiste?'
      : `Song läuft – Spielführer <span class="captain">${esc(playerName(state.captainToken))}</span> legt die Karte, das Team berät sich.`;
    return `<div class="stage teamturn" style="--hue:${team.hue}"><div class="vinyl"></div><h2>${esc(team.name)} ist dran</h2>
      <p class="sub">${sub}</p><div class="eq"><i></i><i></i><i></i><i></i><i></i></div></div>`;
  }
  if (state.phase === 'reveal') {
    const r = state.reveal;
    return `<div class="stage ${r.correct ? 'win' : 'lose'}"><div class="verdict">${r.correct ? '✅' : '❌'}</div>
      <h2 class="${r.correct ? 'ok' : 'bad'}">${esc(team.name)}: ${r.correct ? 'Richtig!' : 'Falsch'}</h2>
      <div class="reveal-card">${cardHtml(r.song)}</div>
      <button id="next" class="big">Weiter</button></div>`;
  }
  if (state.phase === 'finished') {
    const winners = state.teams.filter((t) => state.winnerTeamIds.includes(t.id));
    const names = winners.map((t) => esc(t.name)).join(', ');
    const members = state.solo ? '' : `<p class="sub">${winners.flatMap((t) => membersOf(t).map((p) => esc(p.name))).join(', ')}</p>`;
    return `${confettiHtml()}<div class="stage win"><div class="trophy">🏆</div><h2>${names} gewinnt!</h2>${members}</div>`;
  }
  return '';
}

function teamTile(team) {
  const members = membersOf(team);
  const chips = members.map((p) => `<span class="m ${p.connected ? '' : 'off'}">${esc(p.name)}<button class="x" data-kick="${p.token}" title="Entfernen">×</button></span>`).join('');
  return `<div class="team-tile" style="--hue:${team.hue}"><div class="row">${teamAvatarHtml(team, true)}<span class="tname">${esc(team.name)}</span><span class="count">${members.length}</span></div>
    <div class="members">${chips || '<span class="muted small">noch leer</span>'}</div></div>`;
}

function lobbyHtml() {
  const n = state.teamCount;
  const label = n === 0 ? 'Einzelspiel' : `${n} Teams`;
  const filled = state.solo ? state.players.length : state.teams.filter((t) => membersOf(t).length > 0).length;
  const hint = state.solo
    ? 'Jeder spielt für sich.'
    : `${state.players.length} Spieler, im Schnitt ${(state.players.length / n).toFixed(1).replace('.0', '')} pro Team. Neue Spieler werden automatisch gleichmäßig verteilt und können ihr Team am Handy wechseln.`;
  const body = state.solo
    ? `<div class="player-list">${state.players.map((p) => `<div class="player-row">${avatarHtml(p.name)}<span class="name">${esc(p.name)}</span>
        <button class="ghost" data-kick="${p.token}">Entfernen</button></div>`).join('') || '<div class="empty">Noch niemand da – Code teilen!</div>'}</div>`
    : `<div class="team-grid">${state.teams.map(teamTile).join('')}</div>`;
  const playerLinks = config.playerUrls.length
    ? config.playerUrls.map((url) => `<a class="join-url" href="${esc(url)}">${esc(url)}</a>`).join('')
    : '<span class="muted small">Keine Host-IP im lokalen Netzwerk gefunden. Bitte die Netzwerkverbindung prüfen.</span>';
  return `<div class="grid-2">
    <div class="panel stack room-panel"><div class="panel-heading"><span class="eyebrow"><span></span> Euer Spielraum</span><span class="chip"><i class="dot on"></i> Bereit</span></div>
      <h3>Code zum Beitreten</h3>
      <div class="code">${state.code.split('').map((c) => `<span>${esc(c)}</span>`).join('')}</div>
      <div class="player-links"><p class="muted small">Einen dieser Host-Links an Mitspieler im selben WLAN schicken:</p>${playerLinks}</div></div>
    <div class="panel stack mode-panel"><div class="panel-heading"><span class="eyebrow"><span></span> EURE REGELN</span><span class="choice-icon">♫</span></div>
      <h3>Spielmodus</h3>
      <div class="stepper"><button class="secondary" id="teamsMinus" ${n === 0 ? 'disabled' : ''}>−</button><output>${label}</output>
      <button class="secondary" id="teamsPlus" ${n >= 20 ? 'disabled' : ''}>＋</button></div>
      <p class="muted small">${hint}</p>
      <button id="start" class="big block" ${filled < 2 ? 'disabled' : ''}>Alle da? Spiel starten <span aria-hidden="true">→</span></button>
      ${filled < 2 ? `<p class="muted small" style="text-align:center">${state.solo ? 'Mindestens 2 Spieler nötig' : 'Mindestens 2 Teams mit Mitspielern nötig'}</p>` : ''}</div></div>
    <div class="panel roster-panel" style="grid-column:1/-1"><div class="row between"><div><p class="eyebrow"><span></span> DIE RUNDE</p><h3>${state.solo ? 'Spieler' : 'Teams'}</h3></div><span class="chip">${state.players.length} ${state.players.length === 1 ? 'Spieler' : 'Spieler'} dabei</span></div>${body}</div>`;
}

function render() {
  if (!config.clientId) {
    app.innerHTML = `${topbar()}<div class="alert">SPOTIFY_CLIENT_ID fehlt in .env</div>`;
    return;
  }
  if (!auth) {
    app.innerHTML = `${topbar()}${status ? `<div class="alert">${esc(status)}</div>` : ''}<section class="hero"><p class="eyebrow"><span></span> DIE MUSIKZENTRALE</p><h1>Deine Runde.<br><span>Dein Soundtrack.</span></h1>
      <p class="lead">Melde dich mit Spotify Premium an. Dein Gerät wird zum DJ-Pult für die ganze Runde.</p></section>
      <div class="panel center-card login-card"><span class="choice-icon">♫</span><h3>Spotify verbinden</h3><p class="muted">Damit die Songs direkt im Spiel erklingen.</p><button class="big block" id="login">Mit Spotify anmelden <span aria-hidden="true">→</span></button></div>`;
    document.getElementById('login').onclick = () => login().catch((error) => {
      status = `Spotify-Anmeldung konnte nicht gestartet werden: ${error instanceof Error ? error.message : String(error)}`;
      render();
    });
    return;
  }
  const alertHtml = status ? `<div class="alert">${esc(status)}</div>` : '';
  let html;
  if (!roomCode) {
    html = `${topbar(`<span class="chip"><span class="dot ${deviceId ? 'on' : 'pulse'}"></span>${deviceId ? 'Spotify bereit' : 'Spotify verbindet …'}</span>`)}${alertHtml}
      <section class="hero"><p class="eyebrow"><span></span> SPOTIFY IST VERBUNDEN</p><h1>Der nächste Hit<br><span>gehört euch.</span></h1><p class="lead"><strong>${config.songCount}</strong> Songs sind bereit für eure Zeitreise.</p></section>
      <div class="panel center-card create-card"><div class="vinyl"></div><button id="create" class="big block" ${deviceId ? '' : 'disabled'}>Raum erstellen <span aria-hidden="true">→</span></button></div>`;
  } else if (state) {
    const team = teamOf(state.currentTeamId);
    html = `${topbar(`<span class="chip">Raum <b>${esc(state.code)}</b></span>`)}${alertHtml}`;
    if (state.phase === 'lobby') {
      html += lobbyHtml();
    } else {
      html += stageHtml(team ?? state.teams.find((t) => state.winnerTeamIds.includes(t.id)));
      html += `<div class="players cols">${state.teams.map(teamCard).join('')}</div>
        <p class="muted small" style="text-align:center;margin-top:18px">Stapel: ${state.deckSize} Karten</p>`;
    }
  } else {
    html = topbar() + alertHtml;
  }
  app.innerHTML = html;

  document.getElementById('hostLogout')?.addEventListener('click', async () => {
    try {
      await fetch('/api/host-logout', { method: 'POST' });
      location.reload();
    } catch (error) {
      status = `Abmelden fehlgeschlagen: ${error instanceof Error ? error.message : String(error)}`;
      render();
    }
  });
  const fail = (e) => { status = e.message; render(); };
  document.getElementById('create')?.addEventListener('click', () => createRoom().catch(fail));
  document.getElementById('start')?.addEventListener('click', () => emit(socket, 'host:start').catch(fail));
  document.getElementById('next')?.addEventListener('click', () => emit(socket, 'game:next').catch(fail));
  document.getElementById('teamsMinus')?.addEventListener('click', () => emit(socket, 'host:teams', state.teamCount <= 2 ? 0 : state.teamCount - 1).catch(fail));
  document.getElementById('teamsPlus')?.addEventListener('click', () => emit(socket, 'host:teams', state.teamCount === 0 ? 2 : state.teamCount + 1).catch(fail));
  app.querySelectorAll('[data-kick]').forEach((b) => {
    b.onclick = () => emit(socket, 'host:kick', b.dataset.kick);
  });
}

async function init() {
  try {
    const authResponse = await fetch('/api/host-auth');
    const authState = await authResponse.json();
    if (!authState.configured) {
      renderPasswordGate('Host-Passwort ist nicht eingerichtet. Bitte HOST_PASSWORD_HASH auf dem Server konfigurieren.');
      return;
    }
    if (!authState.authenticated) {
      renderPasswordGate();
      return;
    }
    hostAuthenticated = true;
    config = await (await fetch('/api/config')).json();
    try {
      await handleRedirect();
    } catch (error) {
      status = error instanceof Error ? error.message : String(error);
    }
    render();
    if (auth) startPlayer();
  } catch (error) {
    status = `Host konnte nicht geladen werden: ${error instanceof Error ? error.message : String(error)}`;
    app.innerHTML = `${topbar()}<div class="alert">${esc(status)}</div>`;
  }
}

init();
