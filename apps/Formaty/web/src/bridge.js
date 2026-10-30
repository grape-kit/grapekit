// Kabukla (C#) iletişim: istek/yanıt (JSON-RPC benzeri) ve kabuktan gelen olaylar.
// Kabuk yoksa (tarayıcıda geliştirme) bellek içi yedek kullanılır.
const wv = window.chrome?.webview;
let seq = 0;
const pending = new Map();
const listeners = new Map();

if (wv) {
  wv.addEventListener('message', (e) => {
    const m = typeof e.data === 'string' ? JSON.parse(e.data) : e.data;
    if (m.id != null && pending.has(m.id)) {
      const { resolve, reject } = pending.get(m.id);
      pending.delete(m.id);
      if (m.error) reject(Object.assign(new Error(m.error.message || 'Hata'), { code: m.error.code }));
      else resolve(m.result);
    } else if (m.event) {
      for (const fn of listeners.get(m.event) || []) fn(m.data);
    }
  });
}

/** Kabukta bir yöntemi çağırır. */
export function call(method, params = {}, files = null) {
  if (!wv) return devFallback(method, params);
  return new Promise((resolve, reject) => {
    const id = ++seq;
    pending.set(id, { resolve, reject });
    const msg = JSON.stringify({ id, method, params });
    // Bırakılan dosyalar ek nesne olarak gider: kabuk gerçek dosya yollarını görür
    if (files?.length && wv.postMessageWithAdditionalObjects) wv.postMessageWithAdditionalObjects(msg, files);
    else wv.postMessage(msg);
  });
}

/** Kabuk olayını dinler (ör. 'open-files', 'file-changed', 'window-state'). */
export function on(event, fn) {
  if (!listeners.has(event)) listeners.set(event, []);
  listeners.get(event).push(fn);
  return () => listeners.set(event, listeners.get(event).filter((f) => f !== fn));
}

export const hasHost = !!wv;

// Arayüz hataları kabuğun hata günlüğüne yazılır (hata.log)
let logged = 0;
function report(message, stack) {
  if (!wv || ++logged > 50) return;
  wv.postMessage(JSON.stringify({ id: null, method: 'app.log', params: { message: String(message).slice(0, 2000), stack: String(stack || '').slice(0, 4000) } }));
}
addEventListener('error', (e) => report(e.message, e.error?.stack || `${e.filename}:${e.lineno}`));
addEventListener('unhandledrejection', (e) => report(e.reason?.message || e.reason, e.reason?.stack));
const origError = console.error;
console.error = (...a) => { origError(...a); report(a.map((x) => x?.message || x).join(' '), a.find((x) => x?.stack)?.stack); };

// --- Tarayıcıda geliştirme için basit yedek ---
const mem = { settings: {}, files: {} };
async function devFallback(method, p) {
  switch (method) {
    case 'app.info': return { version: '0.0.0-dev', lang: navigator.language.startsWith('tr') ? 'tr' : 'en', packaged: false, args: [], dark: matchMedia('(prefers-color-scheme: dark)').matches };
    case 'settings.load': return mem.settings;
    case 'settings.save': mem.settings = p.settings; return true;
    case 'session.load': return null;
    case 'session.save': case 'recovery.save': case 'recovery.clear': return true;
    case 'recovery.list': return [];
    case 'recent.list': return [];
    case 'file.read': return { text: mem.files[p.path] ?? '', encoding: 'utf-8', bom: false, eol: '\n', mtime: 0 };
    case 'file.write': mem.files[p.path] = p.text; return { mtime: Date.now() };
    default: console.warn('Kabuk yok:', method, p); return null;
  }
}
