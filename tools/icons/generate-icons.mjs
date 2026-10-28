// Erzeugt das App-Icon (mit Hintergrund und Dart) und das vereinfachte Favicon als SVG.
import fs from 'node:fs';
// Aufruf: node tools/icons/generate-icons.mjs tools/icons  (PNG-Export siehe README.md in diesem Ordner)
const out = process.argv[2] ?? ".";
const order = [20, 1, 18, 4, 13, 6, 10, 15, 2, 17, 3, 19, 7, 16, 8, 11, 14, 9, 12, 5];
const C = { dark: '#1b1b1f', cream: '#f1e6c8', red: '#e53935', green: '#2e9d48', board: '#0b0b0d', wire: '#9aa0a6' };

const f = n => +n.toFixed(2);
function polar(cx, cy, r, deg) { const a = (deg - 90) * Math.PI / 180; return [cx + r * Math.cos(a), cy + r * Math.sin(a)]; }
function wedge(cx, cy, r1, r2, a1, a2, fill) {
  const [x1, y1] = polar(cx, cy, r2, a1), [x2, y2] = polar(cx, cy, r2, a2);
  const [x3, y3] = polar(cx, cy, r1, a2), [x4, y4] = polar(cx, cy, r1, a1);
  return `<path d="M${f(x1)} ${f(y1)}A${r2} ${r2} 0 0 1 ${f(x2)} ${f(y2)}L${f(x3)} ${f(y3)}A${r1} ${r1} 0 0 0 ${f(x4)} ${f(y4)}Z" fill="${fill}"/>`;
}

function board(cx, cy, R, { detail }) {
  // Radien relativ zum Brett, für kleine Größen etwas vergrößerte Ringe/Bull
  const s = R / 100;
  const r = detail
    ? { dOut: 86, dIn: 78, tOut: 55, tIn: 47, ob: 14, b: 6.5 }
    : { dOut: 92, dIn: 76, tOut: 56, tIn: 42, ob: 20, b: 10 };
  const parts = [`<circle cx="${cx}" cy="${cy}" r="${R}" fill="${C.board}"/>`];
  if (detail) parts.push(`<circle cx="${cx}" cy="${cy}" r="${f(R - 1.5 * s)}" fill="none" stroke="#2c2c33" stroke-width="${f(1.5 * s)}"/>`);
  order.forEach((n, i) => {
    const a1 = i * 18 - 9, a2 = i * 18 + 9;
    const single = i % 2 === 0 ? C.dark : C.cream;
    const ring = i % 2 === 0 ? C.red : C.green;
    parts.push(wedge(cx, cy, r.ob * s, r.dOut * s, a1, a2, single));
    parts.push(wedge(cx, cy, r.dIn * s, r.dOut * s, a1, a2, ring));
    parts.push(wedge(cx, cy, r.tIn * s, r.tOut * s, a1, a2, ring));
  });
  parts.push(`<circle cx="${cx}" cy="${cy}" r="${f(r.ob * s)}" fill="${C.green}"/>`);
  parts.push(`<circle cx="${cx}" cy="${cy}" r="${f(r.b * s)}" fill="${C.red}"/>`);
  if (detail) {
    // Drähte für etwas Tiefe
    const w = f(0.45 * s);
    parts.push(`<g fill="none" stroke="${C.wire}" stroke-opacity=".55" stroke-width="${w}">` +
      [r.dOut, r.dIn, r.tOut, r.tIn, r.ob].map(x => `<circle cx="${cx}" cy="${cy}" r="${f(x * s)}"/>`).join('') +
      order.map((_, i) => { const [x1, y1] = polar(cx, cy, r.ob * s, i * 18 - 9), [x2, y2] = polar(cx, cy, r.dOut * s, i * 18 - 9); return `<line x1="${f(x1)}" y1="${f(y1)}" x2="${f(x2)}" y2="${f(y2)}"/>`; }).join('') +
      `</g>`);
  }
  return parts.join('');
}

// Dart entlang der +x-Achse, Spitze im Ursprung; wird gedreht und an die Einstichstelle verschoben.
function dart(x, y, angle, len) {
  const k = len / 170;
  return `<g transform="translate(${x} ${y}) rotate(${angle}) scale(${f(k)})">
    <path d="M0 0L22 -3.2L22 3.2Z" fill="#cfd4d9"/>
    <rect x="20" y="-9.5" width="62" height="19" rx="7" fill="url(#barrel)"/>
    <g stroke="#5f6b73" stroke-width="1.6" opacity=".7"><line x1="34" y1="-7.5" x2="34" y2="7.5"/><line x1="44" y1="-7.5" x2="44" y2="7.5"/><line x1="54" y1="-7.5" x2="54" y2="7.5"/><line x1="64" y1="-7.5" x2="64" y2="7.5"/></g>
    <rect x="80" y="-3.5" width="46" height="7" rx="3" fill="#eceff1"/>
    <path d="M112 0L132 -34L172 -34L158 0Z" fill="#ef5350"/>
    <path d="M112 0L132 34L172 34L158 0Z" fill="#c62828"/>
    <path d="M110 0L172 0" stroke="#ffffff" stroke-opacity=".5" stroke-width="3"/>
  </g>`;
}

const defs = `<defs>
  <linearGradient id="barrel" x1="0" y1="-9.5" x2="0" y2="9.5" gradientUnits="userSpaceOnUse">
    <stop offset="0" stop-color="#eef1f3"/><stop offset=".5" stop-color="#aab4bb"/><stop offset="1" stop-color="#6f7b83"/>
  </linearGradient>
  <radialGradient id="bg" cx=".5" cy=".42" r=".75"><stop offset="0" stop-color="#2a2a31"/><stop offset="1" stop-color="#141417"/></radialGradient>
  <filter id="shadow" x="-20%" y="-20%" width="140%" height="140%"><feDropShadow dx="0" dy="6" stdDeviation="8" flood-color="#000" flood-opacity=".55"/></filter>
</defs>`;

// App-Icon: vollflächiger Hintergrund, Brett plus Dart
const content = `<g filter="url(#shadow)">${board(256, 262, 188, { detail: true })}</g>
  <g filter="url(#shadow)">${dart(256, 262 - 188 * 0.51, -42, 185)}</g>`;
const svg = (body) => `<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512">${defs}
  <rect width="512" height="512" fill="url(#bg)"/>
  ${body}
</svg>`;
const icon = svg(content);

// Maskable: Inhalt auf 78 % verkleinert, damit runde Masken unter Android nichts abschneiden
const maskable = svg(`<g transform="translate(256 256) scale(.78) translate(-256 -256)">${content}</g>`);

// Favicon: nur das Brett, vereinfacht und randlos, damit es bei 16-32 px erkennbar bleibt
const favicon = `<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64">${board(32, 32, 31, { detail: false })}</svg>`;

fs.writeFileSync(`${out}/icon.svg`, icon);
fs.writeFileSync(`${out}/icon-maskable.svg`, maskable);
fs.writeFileSync(`${out}/favicon.svg`, favicon);
console.log('ok', icon.length, maskable.length, favicon.length);
