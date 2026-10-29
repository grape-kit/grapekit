// Kurulum sihirbazı görselleri (Inno Setup BMP ister).
// node make-wizard.mjs            → installer/wizard-large(.2x).bmp  (tüm uygulamalar için ortak)
// node make-wizard.mjs <klasör>   → <klasör>/wizard-small(.2x).bmp     (uygulamanın icon-512.png dosyasından)
import fs from 'node:fs';
import path from 'node:path';
import sharp from 'sharp';

import { fileURLToPath } from 'node:url';
const here = path.dirname(fileURLToPath(import.meta.url));

async function bmp(input, w, h, file, bg = '#FFFFFF') {
  const { data } = await sharp(input).resize(w, h, { fit: 'contain', background: bg }).flatten({ background: bg }).raw().toBuffer({ resolveWithObject: true });
  const row = Math.ceil(w * 3 / 4) * 4, size = 54 + row * h, b = Buffer.alloc(size);
  b.write('BM', 0); b.writeUInt32LE(size, 2); b.writeUInt32LE(54, 10); b.writeUInt32LE(40, 14);
  b.writeInt32LE(w, 18); b.writeInt32LE(h, 22); b.writeUInt16LE(1, 26); b.writeUInt16LE(24, 28); b.writeUInt32LE(row * h, 34);
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const s = ((h - 1 - y) * w + x) * 3, d = 54 + y * row + x * 3;
    b[d] = data[s + 2]; b[d + 1] = data[s + 1]; b[d + 2] = data[s];
  }
  fs.writeFileSync(file, b);
}

const target = process.argv[2];
if (target) {
  const icon = path.join(target, 'icon-512.png');
  await bmp(icon, 55, 55, path.join(target, 'wizard-small.bmp'));
  await bmp(icon, 110, 110, path.join(target, 'wizard-small-2x.bmp'));
  console.log('Küçük sihirbaz görselleri →', target);
} else {
  // Büyük görsel: gece moru zemin, üzüm işareti, "Grapekit" ve slogan
  const tiles = [[34, 54], [80, 54], [126, 54], [57, 98], [103, 98], [80, 142]].map(([x, y], i) =>
    `<rect x="${x}" y="${y}" width="40" height="40" rx="14" fill="url(#t${i % 2})"/><rect x="${x + 7}" y="${y + 6}" width="12" height="6" rx="3" fill="#fff" fill-opacity=".6"/>`).join('');
  const svg = (w, h) => `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 164 314">
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#3B1A78"/><stop offset=".55" stop-color="#1A0B33"/><stop offset="1" stop-color="#0D0619"/></linearGradient>
    <radialGradient id="gl" cx=".5" cy=".38" r=".5"><stop offset="0" stop-color="#8B5CF6" stop-opacity=".55"/><stop offset="1" stop-color="#8B5CF6" stop-opacity="0"/></radialGradient>
    <linearGradient id="t0" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#E2D9FF"/><stop offset="1" stop-color="#5B21B6"/></linearGradient>
    <linearGradient id="t1" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#C4B5FD"/><stop offset="1" stop-color="#4C1D95"/></linearGradient>
  </defs>
  <rect width="164" height="314" fill="url(#bg)"/><rect width="164" height="314" fill="url(#gl)"/>
  <g transform="translate(37 70) scale(.45)">
    <path d="M100 56 C100 42 103 30 110 18" fill="none" stroke="#65A30D" stroke-width="6" stroke-linecap="round"/>
    <path d="M104 34 C112 14 140 6 162 14 C152 36 126 46 104 34 Z" fill="#22C55E"/>${tiles}
  </g>
  <text x="82" y="190" text-anchor="middle" font-family="Segoe UI, Arial" font-weight="800" font-size="24" fill="#F7F4FF">grape<tspan fill="#C4B5FD" font-weight="400">kit</tspan></text>
  <text x="82" y="212" text-anchor="middle" font-family="Segoe UI, Arial" font-size="9.5" fill="#B9AADF">Windows için küçük araçlar</text>
  <text x="82" y="296" text-anchor="middle" font-family="Segoe UI, Arial" font-size="8.5" fill="#8C84A3">grapekit.com</text>
</svg>`;
  const dir = path.join(here, '..', 'installer');
  await bmp(Buffer.from(svg(164, 314)), 164, 314, path.join(dir, 'wizard-large.bmp'), '#1A0B33');
  await bmp(Buffer.from(svg(328, 628)), 328, 628, path.join(dir, 'wizard-large-2x.bmp'), '#1A0B33');
  console.log('Büyük sihirbaz görselleri →', dir);
}
