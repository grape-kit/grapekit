// Dışa aktarma: Markdown → bağımsız HTML belgesi. PDF / PNG / yazdırma kabukta Chromium ile üretilir.
import katex from 'katex';
import { render, headings } from './render.js';
import { drawDiagrams } from './preview.js';
import { call } from './bridge.js';
import { t } from './i18n.js';

const esc = (s) => String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

let cssCache = null;
async function previewCss() {
  if (!cssCache) cssCache = await (await fetch('preview.css')).text();
  return cssCache;
}

/** Göreli yolu belgenin klasörüne göre mutlak dosya yoluna çevirir. */
export function resolveLocal(docDir, rel) {
  if (/^[a-z]:[\\/]/i.test(rel) || rel.startsWith('\\\\')) return rel;
  const parts = (docDir || '').replace(/\\/g, '/').split('/');
  for (const p of decodeURIComponent(rel).replace(/\\/g, '/').split('/')) {
    if (p === '..') parts.pop(); else if (p !== '.' && p !== '') parts.push(p);
  }
  return parts.join('\\');
}

/** KaTeX yazı tiplerini base64 olarak gömer (HTML çıktısı başka bilgisayarda da doğru görünsün). */
async function embedFonts(css) {
  // KaTeX'in woff/ttf yedeklerini at (woff2 yeterli); kalan woff2 ve marka ttf'lerini göm
  css = css.replace(/,\s*url\([^)]+\.(woff|ttf)\)\s*format\(["'](woff|truetype)["']\)/g, '');
  const urls = [...new Set([...css.matchAll(/url\((['"]?)([^)'"]+\.(woff2|ttf))\1\)/g)].map((m) => m[2]))];
  for (const u of urls) {
    try {
      const buf = await (await fetch(u.replace(/^\.?\//, ''))).arrayBuffer();
      const b64 = bytesToB64(new Uint8Array(buf));
      css = css.split(u).join((u.endsWith('.ttf') ? 'data:font/ttf;base64,' : 'data:font/woff2;base64,') + b64);
    } catch { /* yazı tipi yoksa sistemdeki kullanılır */ }
  }
  return css;
}

function bytesToB64(bytes) {
  let s = ''; const chunk = 0x8000;
  for (let i = 0; i < bytes.length; i += chunk) s += String.fromCharCode.apply(null, bytes.subarray(i, i + chunk));
  return btoa(s);
}

/** Belgedeki yerel görselleri data: adresine çevirir. */
async function embedImages(root, docDir) {
  for (const img of root.querySelectorAll('img[src]')) {
    const src = img.getAttribute('src');
    if (/^(data:|https?:)/i.test(src) && !/\.grapekit\.local\//i.test(src)) continue;
    const rel = img.dataset.rel || src.replace(/^https:\/\/doc\.grapekit\.local\/[0-9a-f]+\//i, '');
    try {
      const r = await call('file.readBase64', { path: resolveLocal(docDir, rel) });
      if (r) img.src = `data:${r.mime};base64,${r.data}`;
    } catch { /* görsel bulunamadı: olduğu gibi kalsın */ }
  }
}

/**
 * Bağımsız HTML belgesi üretir.
 * o: { title, theme, dark, docBase, docDir, embed (görselleri göm), forPrint, pdf: {...}, lang }
 */
export async function buildHtml(src, o = {}) {
  const { html, meta } = render(src, { breaks: o.breaks, docBase: o.docBase });
  const host = document.createElement('article');
  host.className = 'md';
  host.innerHTML = html;
  // Görseller: belgenin klasörü
  for (const img of host.querySelectorAll('img[src]')) {
    const s = img.getAttribute('src');
    if (/^(data:|blob:|https?:)/i.test(s)) continue;
    img.dataset.rel = s;
    if (o.docBase) img.src = o.docBase + s.replace(/^\.\//, '').split('/').map(encodeURIComponent).join('/');
  }
  for (const el of host.querySelectorAll('.math-block')) katex.render(el.dataset.tex, el, { displayMode: true, throwOnError: false, output: 'htmlAndMathml' });
  for (const b of host.querySelectorAll('.code-copy')) b.remove();
  for (const b of host.querySelectorAll('.task')) b.setAttribute('disabled', '');
  // Diyagramlar: ekrandaki geçici bir kutuda çizilir (mermaid ölçüm için DOM'da olmalı)
  host.style.cssText = 'position:fixed;left:-10000px;top:0;width:900px';
  document.body.append(host);
  try { await drawDiagrams(host, !!o.dark && !o.forPrint); } finally { host.remove(); host.style.cssText = ''; }
  for (const s of host.querySelectorAll('.mermaid-src')) s.remove();
  if (o.embed) await embedImages(host, o.docDir);

  const title = o.title || meta?.title || t('untitled');
  let css = await previewCss();
  css = o.embedFonts ? await embedFonts(css) : css.replace(/url\((['"]?)(?:\.\/|\/)?fonts\//g, `url($1${location.origin}/fonts/`);
  let pre = '';
  const p = o.pdf;
  if (p?.cover && meta && (meta.title || meta.author)) {
    pre += `<section class="cover"><h1>${esc(meta.title || title)}</h1>${meta.subtitle ? `<p class="cover-sub">${esc(meta.subtitle)}</p>` : ''}
      <div class="cover-meta">${meta.author ? `<p>${esc(meta.author)}</p>` : ''}${meta.date ? `<p>${esc(meta.date instanceof Date ? meta.date.toISOString().slice(0, 10) : meta.date)}</p>` : ''}</div></section>`;
    host.querySelector('.front-matter')?.remove();
  }
  if (p?.toc) {
    const hs = headings(src).filter((x) => x.level <= 3);
    if (hs.length) pre += `<section class="toc-page"><h2>${esc(t('pdf.contents'))}</h2><nav class="toc">${hs.map((x) => `<a class="toc-l${x.level}" href="#${esc(x.id)}">${esc(x.text)}</a>`).join('')}</nav></section>`;
  }
  const printCss = p ? pdfCss(p) : '';
  return `<!doctype html>
<html lang="${o.lang || 'tr'}"${o.dark && !o.forPrint ? ' data-theme="dark"' : ''}>
<head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="generator" content="Markdown Editörü (Grapekit)">
<title>${esc(title)}</title>
<style>${css}</style>
<style>body{margin:0;background:var(--md-bg)}.page{max-width:${o.forPrint ? 'none' : '880px'};margin:0 auto;padding:${o.forPrint ? '0' : '40px 32px 80px'}}${printCss}</style>
</head>
<body class="md-theme-${esc(o.theme || 'github')}"><main class="page">${pre}<article class="md">${host.innerHTML}</article></main></body></html>`;
}

const MARGINS = { narrow: 12, normal: 20, wide: 30 }; // mm

/** PDF için sayfa CSS'i */
export function pdfCss(p) {
  const m = MARGINS[p.margins] ?? 20;
  return `@page{size:${p.paper || 'A4'} ${p.landscape ? 'landscape' : 'portrait'};margin:${p.header !== 'none' ? m + 6 : m}mm ${m}mm ${p.footer !== 'none' ? m + 6 : m}mm}
  html,body{background:#fff}
  .md pre,.md .code-box,.md table,.md .mermaid-box,.md .math-block,.md img,.md .alert{break-inside:avoid}
  .md h1,.md h2,.md h3,.md h4{break-after:avoid}
  .page-break{break-after:page;height:0;border:0;margin:0}
  .cover{height:calc(100vh - 2px);display:flex;flex-direction:column;justify-content:center;break-after:page}
  .toc-page{break-after:page}
  ${p.breakH1 ? '.md h1:not(:first-child){break-before:page}' : ''}
  .md a{color:inherit;text-decoration:underline}
  .md .toc a{text-decoration:none}`;
}

/** Chromium yazdırma ayarları (DevTools Page.printToPDF) */
export function pdfOptions(p, title) {
  const date = new Date().toLocaleDateString(p.lang === 'en' ? 'en-US' : 'tr-TR');
  const style = '<style>*{font-family:Figtree,Segoe UI,sans-serif;font-size:8.5px;color:#6b6478}.w{width:100%;padding:0 14mm;display:flex;justify-content:space-between}</style>';
  const header = p.header === 'title' ? `${style}<div class="w"><span>${esc(title)}</span><span></span></div>`
    : p.header === 'titleDate' ? `${style}<div class="w"><span>${esc(title)}</span><span>${esc(date)}</span></div>` : '<span></span>';
  const pageOf = t('pdf.pageOf', { p: '<span class="pageNumber"></span>', t: '<span class="totalPages"></span>' });
  const footer = p.footer === 'page' ? `${style}<div class="w" style="justify-content:center"><span class="pageNumber"></span></div>`
    : p.footer === 'pageOf' ? `${style}<div class="w" style="justify-content:center"><span>${pageOf}</span></div>` : '<span></span>';
  return {
    printBackground: !!p.background, scale: (p.scale || 100) / 100, preferCSSPageSize: true,
    displayHeaderFooter: p.header !== 'none' || p.footer !== 'none', headerTemplate: header, footerTemplate: footer, generateDocumentOutline: true, generateTaggedPDF: true,
  };
}

export async function copyHtml(src, o) {
  const html = await buildHtml(src, { ...o, embed: true });
  const body = /<article class="md">([\s\S]*)<\/article>/.exec(html)?.[1] ?? html;
  const plain = src;
  await navigator.clipboard.write([new ClipboardItem({
    'text/html': new Blob([`<meta charset="utf-8"><div>${body}</div>`], { type: 'text/html' }),
    'text/plain': new Blob([plain], { type: 'text/plain' }),
  })]);
}

export { bytesToB64 };
