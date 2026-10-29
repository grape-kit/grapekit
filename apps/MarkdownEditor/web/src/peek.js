// Dosya Gezgini önizleme bölmesi: kabuk belgeyi gönderir, aynı çizim motoruyla salt okunur gösterilir.
import { paint } from './preview.js';
import { setAlertTitles, setMetaLabels } from './render.js';

const TEXT = {
  tr: { meta: { title: 'Başlık', author: 'Yazar', date: 'Tarih', tags: 'Etiketler', description: 'Açıklama', subtitle: 'Alt başlık', version: 'Sürüm', lang: 'Dil', category: 'Kategori', keywords: 'Anahtar kelimeler' },
    alerts: { note: 'Not', tip: 'İpucu', important: 'Önemli', warning: 'Uyarı', caution: 'Dikkat' }, copy: 'Kopyala' },
  en: { meta: { title: 'Title', author: 'Author', date: 'Date', tags: 'Tags', description: 'Description', subtitle: 'Subtitle', version: 'Version', lang: 'Language', category: 'Category', keywords: 'Keywords' },
    alerts: { note: 'Note', tip: 'Tip', important: 'Important', warning: 'Warning', caution: 'Caution' }, copy: 'Copy' },
};

const el = document.querySelector('.preview');

async function show(m) {
  const s = m.settings || {};
  const L = TEXT[m.lang === 'en' ? 'en' : 'tr'];
  document.documentElement.lang = m.lang === 'en' ? 'en' : 'tr';
  setMetaLabels(L.meta);
  setAlertTitles(L.alerts);
  const pt = s.previewTheme || 'grapekit';
  const theme = pt === 'dark' || (m.dark && pt === 'grapekit') ? (pt === 'dark' ? 'dark' : 'grapekit') : pt;
  el.className = `preview md md-theme-${theme}${m.dark && pt !== 'sepia' ? ' md-dark' : ''}`;
  el.style.setProperty('--md-size', (s.previewFontSize || 16) + 'px');
  el.style.setProperty('--md-width', s.previewWidth ? s.previewWidth + 'px' : 'none');
  el.style.setProperty('--md-lh', s.lineHeight || 1.7);
  await paint(el, m.text || '', { docBase: m.docBase || '', remoteImages: !!s.remoteImages, breaks: !!s.breaks, dark: !!m.dark, copyLabel: L.copy });
  for (const b of el.querySelectorAll('.code-copy')) b.remove();
  for (const b of el.querySelectorAll('input[type=checkbox]')) b.disabled = true;
  document.body.style.setProperty('--peek-bg', getComputedStyle(el).backgroundColor);
  window.scrollTo(0, 0);
}

// Bağlantılar önizlemede gezinmesin: dış adresler varsayılan tarayıcıda açılır, başlık bağlantıları kaydırır
document.addEventListener('click', (e) => {
  const a = e.target.closest('a[href]');
  if (!a) return;
  e.preventDefault();
  const href = a.getAttribute('href');
  if (href.startsWith('#')) { document.getElementById(decodeURIComponent(href.slice(1)))?.scrollIntoView({ block: 'start' }); return; }
  if (/^(https?:|mailto:)/i.test(href)) window.chrome?.webview?.postMessage({ open: href });
});
document.addEventListener('contextmenu', (e) => e.preventDefault());

window.chrome?.webview?.addEventListener('message', (e) => { show(e.data).catch((err) => { el.textContent = String(err?.message || err); }); });
window.chrome?.webview?.postMessage({ ready: true });
