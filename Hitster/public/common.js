export function esc(text) {
  const div = document.createElement('div');
  div.textContent = text ?? '';
  return div.innerHTML;
}

function hueFor(text) {
  let h = 0;
  for (const c of String(text)) h = (h * 31 + c.codePointAt(0)) % 360;
  return h;
}

export function avatarHtml(name, small = false, hue = hueFor(name)) {
  const initial = esc(Array.from(String(name || '?'))[0]?.toUpperCase() ?? '?');
  return `<span class="avatar${small ? ' sm' : ''}" style="--hue:${hue}">${initial}</span>`;
}

export function teamAvatarHtml(team, small = false) {
  return avatarHtml(team.name.replace(/^Team /, ''), small, team.hue);
}

export function cardHtml(song) {
  const hue = Math.round(Math.min(Math.max((song.year - 1950) / 80, 0), 1) * 300);
  return `<div class="card" style="--h:${hue}"><div class="year">${song.year}</div><div class="title">${esc(song.title)}</div><div class="artist">${esc(song.artist)}</div></div>`;
}

export function timelineHtml(timeline, withSlots, compact = false) {
  const cls = compact ? 'timeline compact' : 'timeline';
  if (!timeline.length) {
    return withSlots
      ? `<div class="${cls}"><button class="slot wide" data-slot="0"><span>＋</span><span>Hier einordnen</span></button></div>`
      : '<div class="no-cards">Noch keine Karten</div>';
  }
  const slot = (i) => `<button class="slot" data-slot="${i}" title="Hier einordnen" aria-label="Hier einordnen">＋</button>`;
  let html = `<div class="${cls}">`;
  if (withSlots) html += slot(0);
  timeline.forEach((song, i) => {
    html += cardHtml(song);
    if (withSlots) html += slot(i + 1);
  });
  return html + '</div>';
}

export function progressHtml(count, total) {
  const pct = Math.min(100, Math.round((count / total) * 100));
  return `<div class="progress"><i style="width:${pct}%"></i></div>`;
}

export function confettiHtml() {
  const colors = ['#ff4fa3', '#7c5cff', '#3cd5ff', '#3ddc97', '#ffd166'];
  let html = '<div class="confetti">';
  for (let i = 0; i < 60; i++) {
    const style = `left:${Math.random() * 100}%;background:${colors[i % colors.length]};animation-duration:${2.5 + Math.random() * 3}s;animation-delay:${Math.random() * 1.5}s`;
    html += `<i style="${style}"></i>`;
  }
  return html + '</div>';
}

export function toast(message) {
  const el = document.createElement('div');
  el.className = 'toast';
  el.textContent = message;
  document.body.appendChild(el);
  setTimeout(() => el.remove(), 3500);
}

export function emit(socket, event, ...args) {
  return new Promise((resolve, reject) => {
    socket.emit(event, ...args, (res) => (res.ok ? resolve(res) : reject(new Error(res.error))));
  });
}
