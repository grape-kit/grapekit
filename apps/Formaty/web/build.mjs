// Formaty arayüzünü paketler: src/ → wwwroot/ (çevrimdışı; yazı tipleri içeride).
//   node build.mjs           tek seferlik derleme (küçültülmüş)
//   node build.mjs --watch   geliştirme: değişince yeniden derler
import * as esbuild from 'esbuild';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const out = path.join(here, 'wwwroot');
const watch = process.argv.includes('--watch');

fs.rmSync(out, { recursive: true, force: true });
fs.mkdirSync(path.join(out, 'fonts'), { recursive: true });

// fs.cpSync OneDrive klasörlerinde takılabildiği için basit özyinelemeli kopya
function copy(from, to) {
  const src = path.join(here, from), dst = path.join(out, to);
  if (fs.statSync(src).isDirectory()) {
    fs.mkdirSync(dst, { recursive: true });
    for (const f of fs.readdirSync(src)) copy(path.join(from, f), path.join(to, f));
  } else fs.writeFileSync(dst, fs.readFileSync(src));
}
copy('src/index.html', 'index.html');
copy('src/assets', '.');
for (const f of ['Bricolage-Bold.ttf', 'Figtree-Regular.ttf', 'Figtree-SemiBold.ttf'])
  fs.writeFileSync(path.join(out, 'fonts', f), fs.readFileSync(path.join(here, '../../../core/Grapekit.Core/Fonts', f)));

const common = { bundle: true, minify: !watch, sourcemap: watch ? 'inline' : false, target: 'chrome120', logLevel: 'info' };
const ctxs = [
  await esbuild.context({ ...common, entryPoints: { app: path.join(here, 'src/main.js') }, outdir: out, format: 'esm' }),
  await esbuild.context({ ...common, entryPoints: { app: path.join(here, 'src/styles/app.css') }, outdir: out, external: ['/fonts/*'] }),
];
if (watch) { for (const c of ctxs) await c.watch(); console.log('İzleniyor…'); }
else { for (const c of ctxs) { await c.rebuild(); await c.dispose(); } }
