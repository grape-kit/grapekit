// Uygulama durumu ve ayarlar. Diğer modüller buradan okur/yazar.
export const DEFAULTS = {
  lang: '', appTheme: 'system',            // system | light | dark
  viewMode: 'split', sidebar: true, sideTab: 'outline', sideWidth: 260, splitRatio: 0.5,
  syncScroll: true, zoom: 100,
  // Düzenleyici
  editorFont: '', editorFontSize: 15, tabSize: 4, wordWrap: true, lineNumbers: false, spellcheck: true, spellLang: 'tr',
  editorMaxWidth: 0, focusMode: false, typewriter: false, smartPaste: true,
  // Önizleme
  previewTheme: 'grapekit', previewFontSize: 16, previewWidth: 820, lineHeight: 1.7, breaks: false, remoteImages: true, codeLineNumbers: false,
  // Dosyalar
  autosave: false, autosaveDelay: 2000, imageFolder: 'resimler', defaultEncoding: 'utf-8', defaultEol: 'crlf', restoreSession: true,
  // Dışa aktarma
  pdf: { paper: 'A4', landscape: false, margins: 'normal', scale: 100, theme: 'github', header: 'none', footer: 'pageOf', cover: true, toc: false, background: true, breakH1: false },
  html: { theme: 'github', embed: true },
  png: { scale: 2, width: 900, theme: 'github' },
  batch: { format: 'pdf', sub: true, same: true, out: '' },
  // Güncelleme
  autoUpdate: true,
  userTemplates: [],
};

export const app = {
  settings: structuredClone(DEFAULTS),
  info: { version: '', packaged: false, lang: 'tr', dark: false },
  tabs: [],          // Belge sekmeleri
  active: null,      // Etkin sekme
  closed: [],        // Kapatılan sekmeler (geri açmak için)
  folder: null,      // Açık klasör yolu
  recent: [],
  view: null,        // CodeMirror EditorView
  dark: false,       // Şu anki koyu tema
  zen: false,
};

let tabSeq = 0;
export function newTabId() { return 't' + (++tabSeq) + '-' + Date.now().toString(36); }

export const baseName = (p) => (p ? p.replace(/[\\/]+$/, '').split(/[\\/]/).pop() : '');
export const dirName = (p) => (p ? p.replace(/[\\/][^\\/]*$/, '') : '');
export const extOf = (p) => (/\.([^.\\/]+)$/.exec(p || '') || [])[1]?.toLowerCase() || '';

/** Ayarları birleştirir (iç içe nesneler korunur). */
export function mergeSettings(saved) {
  const s = structuredClone(DEFAULTS);
  for (const k in saved || {}) {
    if (!(k in DEFAULTS)) continue;
    if (DEFAULTS[k] && typeof DEFAULTS[k] === 'object' && !Array.isArray(DEFAULTS[k])) s[k] = { ...DEFAULTS[k], ...saved[k] };
    else s[k] = saved[k];
  }
  return s;
}

export const MD_EXT = ['md', 'markdown', 'mdown', 'mkd', 'mkdn', 'mdx', 'txt', 'text'];
export const IMG_EXT = ['png', 'jpg', 'jpeg', 'gif', 'webp', 'svg', 'bmp', 'avif'];
