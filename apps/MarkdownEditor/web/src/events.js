// Uygulama içi olaylar: 'tabs' (sekme listesi değişti), 'active' (etkin belge değişti), 'doc' (içerik değişti),
// 'saved', 'settings', 'folder', 'theme', 'lang'
const map = new Map();
export function on(ev, fn) { if (!map.has(ev)) map.set(ev, new Set()); map.get(ev).add(fn); return () => map.get(ev).delete(fn); }
export function emit(ev, data) { for (const fn of map.get(ev) || []) { try { fn(data); } catch (e) { console.error(ev, e); } } }
