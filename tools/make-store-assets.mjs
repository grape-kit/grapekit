// Microsoft Store (MSIX) görselleri: uygulamanın özel çizim simgesinden tüm boyutlar ve ölçekler.
// Kullanım: node make-store-assets.mjs <brand-klasörü> <çıktı-klasörü>
//   ör. node make-store-assets.mjs ../apps/KeepAwake/brand ../apps/KeepAwake/packaging/Assets
// <brand>/icon-art.svg (büyük) ve icon-art-small.svg (16–32 px) kullanılır.
import fs from 'node:fs';
import path from 'node:path';
import sharp from 'sharp';

const [brand, out] = process.argv.slice(2);
if (!brand || !out) { console.error('Kullanım: node make-store-assets.mjs <brand> <çıktı>'); process.exit(1); }
fs.mkdirSync(out, { recursive: true });
const art = fs.readFileSync(path.join(brand, 'icon-art.svg'));
const smallFile = path.join(brand, 'icon-art-small.svg');
const small = fs.existsSync(smallFile) ? fs.readFileSync(smallFile) : art;

const icon = (px) => sharp(px <= 32 ? small : art).resize(px, px).png().toBuffer();

/** Saydam zemin üzerinde ortalanmış simge. ratio: simgenin kısa kenara oranı. */
async function tile(w, h, ratio, file) {
  const s = Math.round(Math.min(w, h) * ratio);
  await sharp({ create: { width: w, height: h, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } } })
    .composite([{ input: await icon(s), left: Math.round((w - s) / 2), top: Math.round((h - s) / 2) }])
    .png().toFile(path.join(out, file));
}

const scales = [100, 125, 150, 200, 400];
const k = (v, sc) => Math.round(v * sc / 100);
for (const sc of scales) {
  await tile(k(44, sc), k(44, sc), 1, `Square44x44Logo.scale-${sc}.png`);
  await tile(k(150, sc), k(150, sc), 0.66, `Square150x150Logo.scale-${sc}.png`);
  await tile(k(310, sc), k(150, sc), 0.66, `Wide310x150Logo.scale-${sc}.png`);
  await tile(k(71, sc), k(71, sc), 0.72, `SmallTile.scale-${sc}.png`);
  await tile(k(310, sc), k(310, sc), 0.6, `LargeTile.scale-${sc}.png`);
  await tile(k(50, sc), k(50, sc), 1, `StoreLogo.scale-${sc}.png`);
  await tile(k(620, sc), k(300, sc), 0.5, `SplashScreen.scale-${sc}.png`);
}
// Görev çubuğu / Başlat menüsü: plakasız (unplated) hedef boyutlar
for (const t of [16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256]) {
  for (const alt of ['', '_altform-unplated', '_altform-lightunplated']) {
    await tile(t, t, 1, `Square44x44Logo.targetsize-${t}${alt}.png`);
  }
}
// Store listesi için 300x300 kare logo (Partner Center > Mağaza logoları)
await sharp(art).resize(300, 300).png().toFile(path.join(out, '..', 'store-logo-300.png'));
console.log('Store görselleri hazır →', path.resolve(out));
