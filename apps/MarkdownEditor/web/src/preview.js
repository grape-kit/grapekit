// Önizleme alanı: HTML'i yerleştirir, diyagram/matematik çizer, görsel yollarını çözer, etkileşimleri bağlar.
import katex from 'katex';
import { render } from './render.js';

let mermaidMod = null;
const mermaidCache = new Map(); // kaynak+tema → svg
let mermaidTheme = '';

async function mermaid(dark) {
  if (!mermaidMod) mermaidMod = (await import('mermaid')).default;
  const theme = dark ? 'dark' : 'light';
  if (theme !== mermaidTheme) {
    // Grapekit renkleri (lila / gece moru)
    const v = dark
      ? { background: '#160A2C', primaryColor: '#2A1A52', primaryBorderColor: '#A78BFA', primaryTextColor: '#F1ECFF', secondaryColor: '#1E1038', tertiaryColor: '#241544',
          lineColor: '#B4A9CF', textColor: '#F1ECFF', mainBkg: '#2A1A52', nodeBorder: '#A78BFA', clusterBkg: '#1E1038', clusterBorder: '#3B2766', edgeLabelBackground: '#241544',
          noteBkgColor: '#3B2470', noteTextColor: '#F1ECFF', noteBorderColor: '#A78BFA', actorBkg: '#2A1A52', actorBorder: '#A78BFA', actorTextColor: '#F1ECFF', signalColor: '#CFC6EA', signalTextColor: '#F1ECFF' }
      : { background: '#FFFFFF', primaryColor: '#F1EAFE', primaryBorderColor: '#7C3AED', primaryTextColor: '#1A0B33', secondaryColor: '#FAF8FE', tertiaryColor: '#F3EFFB',
          lineColor: '#6B6478', textColor: '#1A0B33', mainBkg: '#F1EAFE', nodeBorder: '#7C3AED', clusterBkg: '#FAF8FE', clusterBorder: '#D9D1EC', edgeLabelBackground: '#FFFFFF',
          noteBkgColor: '#FFF7D6', noteTextColor: '#1A0B33', noteBorderColor: '#D9B44A', actorBkg: '#F1EAFE', actorBorder: '#7C3AED', actorTextColor: '#1A0B33', signalColor: '#4B3F66', signalTextColor: '#1A0B33' };
    mermaidMod.initialize({ startOnLoad: false, securityLevel: 'strict', theme: 'base', themeVariables: { ...v, fontFamily: 'Figtree, Segoe UI, sans-serif', fontSize: '15px' } });
    mermaidTheme = theme;
  }
  return mermaidMod;
}

let seq = 0;
/** Kutudaki mermaid kaynaklarını SVG'ye çevirir (önbellekli). */
export async function drawDiagrams(root, dark) {
  const boxes = root.querySelectorAll('.mermaid-box');
  if (!boxes.length) return;
  const m = await mermaid(dark);
  for (const box of boxes) {
    const src = box.querySelector('.mermaid-src')?.textContent ?? '';
    const out = box.querySelector('.mermaid-out');
    const key = (dark ? 'd:' : 'l:') + src;
    if (mermaidCache.has(key)) { out.innerHTML = mermaidCache.get(key); continue; }
    try {
      const { svg } = await m.render('mmd' + (++seq), src);
      mermaidCache.set(key, svg);
      if (mermaidCache.size > 200) mermaidCache.delete(mermaidCache.keys().next().value);
      out.innerHTML = svg;
    } catch (e) {
      out.innerHTML = `<pre class="mermaid-error">${String(e?.message || e).replace(/</g, '&lt;').slice(0, 500)}</pre>`;
      document.querySelectorAll('body > [id^="dmmd"]').forEach((n) => n.remove()); // mermaid'in hata artığı
    }
  }
}

function drawMath(root) {
  for (const el of root.querySelectorAll('.math-block')) {
    try { katex.render(el.dataset.tex, el, { displayMode: true, throwOnError: false, output: 'htmlAndMathml' }); } catch { el.textContent = el.dataset.tex; }
  }
}

/** Göreli görsel ve bağlantı yollarını belgenin klasörüne göre çözer. */
function resolvePaths(root, docBase, remoteImages) {
  for (const img of root.querySelectorAll('img[src]')) {
    const s = img.getAttribute('src');
    if (/^(data:|blob:)/i.test(s)) continue;
    if (/^https?:/i.test(s)) { if (!remoteImages) { img.dataset.blocked = s; img.removeAttribute('src'); img.classList.add('img-blocked'); } continue; }
    // Belgenin klasör adresi henüz hazır değilse (yeni açılış) görseli yüklemeye çalışma; adres gelince yeniden çizilir
    if (!docBase) { img.removeAttribute('src'); img.classList.add('img-pending'); continue; }
    img.src = docBase + s.replace(/^\.\//, '').split('/').map(encodeURIComponent).join('/').replace(/%23/g, '#');
    img.loading = 'lazy';
    // Klasör eşlemesi yeni yapıldıysa ilk istek kaçabilir: bir kez daha dene
    img.onerror = () => { if (!img.dataset.retry) { img.dataset.retry = '1'; setTimeout(() => { img.src = img.src.split('?')[0] + '?r=' + Date.now(); }, 250); } };
  }
}

/**
 * Belgeyi önizlemeye çizer.
 * ctx: { docBase, remoteImages, breaks, dark, copyLabel }
 */
export async function paint(el, src, ctx) {
  const { html, meta } = render(src, ctx);
  el.innerHTML = html;
  resolvePaths(el, ctx.docBase, ctx.remoteImages);
  drawMath(el);
  for (const b of el.querySelectorAll('.code-copy')) b.textContent = ctx.copyLabel || 'Kopyala';
  await drawDiagrams(el, ctx.dark);
  return { meta };
}

/** Satır → önizleme konumu eşlemesi (senkron kaydırma için). */
export function lineMap(el) {
  const top = el.getBoundingClientRect().top - el.scrollTop;
  const pts = [];
  for (const n of el.querySelectorAll('[data-line]')) {
    const line = +n.dataset.line;
    if (Number.isNaN(line)) continue;
    const y = n.getBoundingClientRect().top - top;
    if (!pts.length || line > pts[pts.length - 1].line) pts.push({ line, y });
  }
  return pts;
}

/** Satır numarasından (kesirli) önizleme y değeri. */
export function lineToY(pts, line) {
  if (!pts.length) return 0;
  let a = pts[0], b = pts[pts.length - 1];
  for (let i = 0; i < pts.length; i++) {
    if (pts[i].line <= line) a = pts[i]; else { b = pts[i]; break; }
  }
  if (b.line === a.line) return a.y;
  return a.y + (b.y - a.y) * Math.min(1, (line - a.line) / (b.line - a.line));
}

/** Önizleme y değerinden satır numarası. */
export function yToLine(pts, y) {
  if (!pts.length) return 0;
  let a = pts[0], b = pts[pts.length - 1];
  for (let i = 0; i < pts.length; i++) {
    if (pts[i].y <= y) a = pts[i]; else { b = pts[i]; break; }
  }
  if (b.y === a.y) return a.line;
  return a.line + (b.line - a.line) * Math.min(1, (y - a.y) / (b.y - a.y));
}
