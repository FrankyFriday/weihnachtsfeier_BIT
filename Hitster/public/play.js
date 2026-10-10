import { esc, timelineHtml, emit, avatarHtml, teamAvatarHtml, cardHtml, progressHtml, confettiHtml, toast } from '/common.js';

const socket = io();
const app = document.getElementById('app');
const saved = JSON.parse(localStorage.getItem('hitster-player') ?? 'null');
let state = null;
let me = null;
let message = '';

async function join(code, name, token) {
  try {
    const res = await emit(socket, 'player:join', code, name, token);
    me = res.token;
    localStorage.setItem('hitster-player', JSON.stringify({ code, token: me }));
    message = '';
  } catch (e) {
    message = e.message;
    me = null;
    render();
  }
}

socket.on('connect', () => {
  if (saved) join(saved.code, '', saved.token);
});
socket.on('state', (s) => {
  state = s;
  render();
});

const topbar = (right = '') =>
  `<header class="topbar"><div class="logo"><span class="logo-mark">♪</span>Hitster</div>${right}</header>`;

function renderJoin() {
  app.innerHTML = `<header class="topbar"><a class="logo" href="/"><span class="logo-mark">♪</span>Hitster</a><span class="chip">DABEI SEIN</span></header>
    <section class="hero join-hero"><p class="eyebrow"><span></span> Dein Platz wartet</p><h1>Rein in die <span>Runde.</span></h1>
    <p class="lead">Gib den Raumcode vom Host-Bildschirm ein.</p></section>
    <form class="panel center-card stack" id="joinForm" autocomplete="off">
      <label class="field-label" for="code">Raumcode</label>
      <input class="code-input" id="code" placeholder="ABCD" maxlength="4" value="${esc(saved?.code ?? '')}" autocapitalize="characters" aria-label="Vierstelliger Raumcode" required>
      <label class="field-label" for="name">Dein Spielername</label>
      <input id="name" placeholder="Wie sollen wir dich nennen?" maxlength="20" autocomplete="nickname">
      <button class="big block" id="go" type="submit">Beitreten <span aria-hidden="true">→</span></button>
      ${message ? `<div class="alert">${esc(message)}</div>` : ''}
    </form>`;
  document.getElementById('joinForm').onsubmit = (e) => {
    e.preventDefault();
    const code = document.getElementById('code').value.trim().toUpperCase();
    join(code, document.getElementById('name').value, saved?.code === code ? saved.token : undefined);
  };
}

const teamOf = (id) => state.teams.find((t) => t.id === id);
const playerName = (token) => state.players.find((p) => p.token === token)?.name ?? '';

function lobbyStage(self) {
  const teamPicker = state.solo ? '' : `<p class="sub" style="margin-top:18px">Dein Team – tippe zum Wechseln</p>
    <div class="team-grid" style="text-align:left">${state.teams.map((t) => {
    const count = state.players.filter((p) => p.teamId === t.id).length;
    return `<button class="team-tile ${t.id === self.teamId ? 'mine' : ''}" data-team="${t.id}" style="--hue:${t.hue}">
        <div class="row">${teamAvatarHtml(t, true)}<span class="tname">${esc(t.name)}</span><span class="count">${count}</span></div></button>`;
  }).join('')}</div>`;
  const myTeam = state.solo ? '' : `<p class="sub">Du spielst in <b style="color:hsl(${teamOf(self.teamId)?.hue} 85% 70%)">${esc(teamOf(self.teamId)?.name ?? '')}</b></p>`;
  return `<div class="stage"><div class="vinyl"></div><h2>Gleich geht's los</h2><p class="sub">Warte auf den Start durch den Host …</p>${myTeam}${teamPicker}</div>`;
}

function stageHtml(self, canAct) {
  if (state.phase === 'lobby') return lobbyStage(self);
  const team = teamOf(state.currentTeamId);
  if (state.phase === 'finished') {
    const won = state.winnerTeamIds.includes(self.teamId);
    const names = state.teams.filter((t) => state.winnerTeamIds.includes(t.id)).map((t) => esc(t.name)).join(', ');
    return `${won ? confettiHtml() : ''}<div class="stage ${won ? 'win' : ''}"><div class="trophy">${won ? '🏆' : '🏁'}</div>
      <h2>${won ? (state.solo ? 'Du hast gewonnen!' : 'Euer Team hat gewonnen!') : 'Spiel beendet'}</h2><p class="sub">Sieger: ${names}</p></div>`;
  }
  const myTeamTurn = state.currentTeamId === self.teamId;
  if (state.phase === 'placing') {
    if (canAct) {
      const sub = state.solo ? 'Hör zu und tippe auf ＋, wohin der Song gehört.' : 'Berate dich mit deinem Team und tippe auf ＋, wohin der Song gehört.';
      return `<div class="stage turn"><h2>🎧 ${state.solo ? 'Du bist dran!' : 'Du bist Spielführer!'}</h2><p class="sub">${sub}</p><div class="eq"><i></i><i></i><i></i><i></i><i></i></div></div>`;
    }
    if (myTeamTurn) {
      return `<div class="stage teamturn" style="--hue:${team.hue}"><div class="vinyl"></div><h2>Euer Team ist dran</h2>
        <p class="sub">Spielführer <span class="captain">${esc(playerName(state.captainToken))}</span> legt die Karte – beratet euch!</p></div>`;
    }
    return `<div class="stage"><div class="vinyl"></div><h2>${esc(team.name)} ist dran</h2><p class="sub">ordnet gerade einen Song ein …</p></div>`;
  }
  const r = state.reveal;
  const who = myTeamTurn && !state.solo ? 'Ihr' : esc(team.name);
  return `<div class="stage ${r.correct ? 'win' : 'lose'}"><div class="verdict">${r.correct ? '✅' : '❌'}</div>
    <h2 class="${r.correct ? 'ok' : 'bad'}">${r.correct ? 'Richtig!' : 'Falsch'}</h2>
    <div class="reveal-card">${cardHtml(r.song)}</div>
    <p class="sub">${who}${r.correct ? ' behält die Karte' : ' verliert die Karte'}</p>
    ${canAct ? '<p style="margin-top:16px"><button id="next" class="big">Weiter</button></p>' : ''}</div>`;
}

function render() {
  if (!state || !me) {
    renderJoin();
    return;
  }
  const self = state.players.find((p) => p.token === me);
  if (!self) {
    me = null;
    renderJoin();
    return;
  }
  const myTeam = teamOf(self.teamId);
  const captain = state.players.find((p) => p.token === state.captainToken);
  const canAct = state.currentTeamId === self.teamId && (me === state.captainToken || !captain?.connected);
  const teamChip = myTeam && !state.solo ? `<span class="chip" style="--hue:${myTeam.hue}">${teamAvatarHtml(myTeam, true)} ${esc(myTeam.name)}</span>` : '';
  let html = topbar(`${teamChip}<span class="chip">Raum <b>${esc(state.code)}</b></span>`);
  html += stageHtml(self, canAct);

  if (state.phase !== 'lobby' && myTeam) {
    const title = state.solo ? 'Deine Zeitleiste' : `Zeitleiste von ${esc(myTeam.name)}`;
    html += `<div class="panel"><div class="row between"><div class="row">${teamAvatarHtml(myTeam)}<h3>${title}</h3></div>
      <span class="count">${myTeam.timeline.length} / ${state.cardsToWin}</span></div>${progressHtml(myTeam.timeline.length, state.cardsToWin)}
      ${timelineHtml(myTeam.timeline, canAct && state.phase === 'placing')}</div>`;
    const others = state.teams.filter((t) => t.id !== myTeam.id);
    if (others.length) {
      html += `<h3 class="muted" style="margin:22px 4px 8px">${state.solo ? 'Die anderen' : 'Die anderen Teams'}</h3><div class="players">`;
      for (const t of others) {
        const active = t.id === state.currentTeamId && (state.phase === 'placing' || state.phase === 'reveal');
        html += `<div class="pcard ${active ? 'active' : ''}" style="--hue:${t.hue}"><div class="head">${teamAvatarHtml(t, true)}
          <div class="who"><b>${esc(t.name)}</b></div><span class="count">${t.timeline.length} / ${state.cardsToWin}</span></div>
          ${timelineHtml(t.timeline, false, true)}</div>`;
      }
      html += '</div>';
    }
  }
  app.innerHTML = html;

  app.querySelectorAll('[data-slot]').forEach((b) => {
    b.onclick = () => emit(socket, 'player:place', Number(b.dataset.slot)).catch((e) => toast(e.message));
  });
  app.querySelectorAll('[data-team]').forEach((b) => {
    b.onclick = () => emit(socket, 'player:team', Number(b.dataset.team)).catch((e) => toast(e.message));
  });
  const next = document.getElementById('next');
  if (next) next.onclick = () => emit(socket, 'game:next').catch((e) => toast(e.message));
}

render();
