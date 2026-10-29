// Grapekit uygulama simgesi üretici: tüm uygulamalar aynı tasarım dilini taşır.
// Kullanım: node make-icon.mjs <lucide-simge-adı> <#vurguRengi> <çıktı-klasörü>
//   ör. node make-icon.mjs arrow-big-up-dash "#7C3AED" ../apps/CapsLock/brand
// Üretir: app.ico (16–256), icon-512.png, icon-1024.png
import fs from 'node:fs';
import path from 'node:path';
import sharp from 'sharp';

const [glyph, accent = '#7C3AED', out = '.'] = process.argv.slice(2);
if (!glyph) { console.error('Kullanım: node make-icon.mjs <lucide-adı> <#renk> <klasör>'); process.exit(1); }
fs.mkdirSync(out, { recursive: true });

const hex = h => h.replace('#', '').match(/../g).map(x => parseInt(x, 16));
const toHex = a => '#' + a.map(v => Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, '0')).join('');
const mix = (a, b, k) => toHex(hex(a).map((v, i) => v + (hex(b)[i] - v) * k));
const top = mix(accent, '#FFFFFF', .28), bottom = mix(accent, '#1A0B33', .42);

const res = await fetch(`https://cdn.jsdelivr.net/npm/lucide-static@0.460.0/icons/${glyph}.svg`);
if (!res.ok) throw new Error('Simge bulunamadı: ' + glyph);
const inner = (await res.text()).replace(/^[\s\S]*?<svg[^>]*>/, '').replace(/<\/svg>[\s\S]*$/, '');

// Grapekit işareti (rozet için): 6 kutucuk + yaprak
const tiles = [[34, 54], [80, 54], [126, 54], [57, 98], [103, 98], [80, 142]].map(([x, y], i) =>
  `<rect x="${x}" y="${y}" width="40" height="40" rx="14" fill="${i % 2 ? '#7C3AED' : '#9A6BFB'}"/>`).join('');
const mark = `<path d="M100 56 C100 42 103 30 110 18" fill="none" stroke="#4D7C0F" stroke-width="7" stroke-linecap="round"/><path d="M104 34 C112 14 140 6 162 14 C152 36 126 46 104 34 Z" fill="#22C55E"/>${tiles}`;

function svg(size, badge) {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 512 512">
<defs>
  <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="${top}"/><stop offset="1" stop-color="${bottom}"/></linearGradient>
  <linearGradient id="hl" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff" stop-opacity=".22"/><stop offset=".5" stop-color="#fff" stop-opacity="0"/></linearGradient>
  <filter id="sh" x="-20%" y="-20%" width="140%" height="150%"><feDropShadow dx="0" dy="10" stdDeviation="12" flood-color="#12062A" flood-opacity=".35"/></filter>
</defs>
<rect x="16" y="16" width="480" height="480" rx="116" fill="url(#bg)"/>
<rect x="16" y="16" width="480" height="480" rx="116" fill="url(#hl)"/>
<rect x="17.5" y="17.5" width="477" height="477" rx="114.5" fill="none" stroke="#fff" stroke-opacity=".16" stroke-width="3"/>
<g filter="url(#sh)" transform="translate(106 ${badge ? 96 : 106}) scale(12.5)" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">${inner}</g>
${badge ? `<g transform="translate(360 360)"><circle cx="64" cy="64" r="70" fill="#1A0B33" stroke="#fff" stroke-opacity=".9" stroke-width="8"/><g transform="translate(12 14) scale(.52)">${mark}</g></g>` : ''}
</svg>`;
}

const sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
const pngs = [];
for (const s of sizes) pngs.push({ s, buf: await sharp(Buffer.from(svg(s, s >= 96))).png().toBuffer() });
const head = Buffer.alloc(6 + 16 * pngs.length);
head.writeUInt16LE(0, 0); head.writeUInt16LE(1, 2); head.writeUInt16LE(pngs.length, 4);
let off = head.length;
pngs.forEach(({ s, buf }, i) => {
  const o = 6 + i * 16;
  head.writeUInt8(s >= 256 ? 0 : s, o); head.writeUInt8(s >= 256 ? 0 : s, o + 1);
  head.writeUInt16LE(1, o + 4); head.writeUInt16LE(32, o + 6);
  head.writeUInt32LE(buf.length, o + 8); head.writeUInt32LE(off, o + 12); off += buf.length;
});
fs.writeFileSync(path.join(out, 'app.ico'), Buffer.concat([head, ...pngs.map(p => p.buf)]));
for (const s of [512, 1024]) await sharp(Buffer.from(svg(s, true))).png().toFile(path.join(out, `icon-${s}.png`));
fs.writeFileSync(path.join(out, 'icon.svg'), svg(512, true));
console.log('Simge hazır →', path.resolve(out));
