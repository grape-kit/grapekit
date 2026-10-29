// Belgeler ve sekmeler: aç, kaydet, kapat, değişiklik takibi, dosya izleme, otomatik kaydetme, kurtarma, oturum.
import { EditorSelection } from '@codemirror/state';
import { app, newTabId, baseName, dirName, extOf, IMG_EXT } from './state.js';
import { call } from './bridge.js';
import { emit } from './events.js';
import { t } from './i18n.js';
import { ask, toast } from './ui.js';
import { EditorView } from './editor.js';

let untitledSeq = 0;

export const isDirty = (tab) => !!tab && (tab === app.active ? !app.view.state.doc.eq(tab.savedDoc) : tab.dirty);
export const tabTitle = (tab) => tab.path ? baseName(tab.path) : `${t('untitled')}${tab.untitled > 1 ? ' ' + tab.untitled : ''}`;

function normalize(text) { return text.replace(/\r\n?/g, '\n'); }

async function attachBase(tab) {
  tab.docDir = tab.path ? dirName(tab.path) : '';
  tab.docBase = tab.docDir ? await call('docbase', { dir: tab.docDir }) : '';
}

/** Yeni sekme (boş ya da metinli). */
export function newDoc(text = '', { path = null, meta = {} } = {}) {
  const st = app.view.newState(text);
  const tab = { id: newTabId(), path, untitled: path ? 0 : ++untitledSeq, state: st, savedDoc: path ? st.doc : app.view.newState('').doc, dirty: false,
    encoding: meta.encoding || app.settings.defaultEncoding, bom: !!meta.bom, eol: meta.eol || (app.settings.defaultEol === 'lf' ? '\n' : '\r\n'),
    mtime: meta.mtime || 0, previewScroll: 0, docBase: '', docDir: '' };
  app.tabs.push(tab);
  attachBase(tab).then(() => { if (app.active === tab) emit('active', tab); });
  activate(tab);
  emit('tabs');
  return tab;
}

/** Sekmeyi etkinleştirir: düzenleyici durumunu değiştirir. */
export function activate(tab) {
  if (!tab) return;
  if (app.active && app.active !== tab && app.tabs.includes(app.active)) {
    app.active.state = app.view.state;
    app.active.dirty = !app.view.state.doc.eq(app.active.savedDoc);
  }
  app.active = tab;
  app.view.setState(tab.state);
  emit('active', tab);
  emit('tabs');
  requestAnimationFrame(() => app.view.focus());
}

/** Dosya(ları) açar; zaten açıksa o sekmeye geçer. */
export async function openPaths(paths, { activateLast = true } = {}) {
  let last = null;
  for (const p of paths) {
    const ext = extOf(p);
    if (IMG_EXT.includes(ext)) continue;
    const open = app.tabs.find((x) => x.path && x.path.toLowerCase() === p.toLowerCase());
    if (open) { last = open; continue; }
    try {
      const r = await call('file.read', { path: p });
      // Boş, değişmemiş "Adsız" sekmeyi yeni dosyayla değiştir
      const blank = app.tabs.length === 1 && !app.tabs[0].path && !isDirty(app.tabs[0]) && app.view.state.doc.length === 0 ? app.tabs[0] : null;
      const tab = newDoc(normalize(r.text), { path: p, meta: r });
      tab.savedDoc = tab.state.doc;
      if (blank) closeTab(blank, { force: true });
      call('recent.add', { path: p });
      call('file.watch', { paths: app.tabs.filter((x) => x.path).map((x) => x.path) });
      last = tab;
    } catch (e) {
      toast(e.code === 'too-big' ? t('err.tooBig') : t('err.open', { m: e.message }), { kind: 'error' });
    }
  }
  if (last && activateLast) activate(last);
  return last;
}

export async function openDialog() {
  const paths = await call('file.openDialog', { multi: true, kind: 'md' });
  if (paths?.length) await openPaths(paths);
}

/** Kaydeder; yolu yoksa farklı kaydet. Dönüş: başarılı mı */
export async function save(tab = app.active, { as = false } = {}) {
  if (!tab) return false;
  if (tab === app.active) tab.state = app.view.state;
  let path = tab.path;
  if (!path || as) {
    const suggested = path ? baseName(path) : guessName(tab.state.doc.toString());
    path = await call('file.saveDialog', { name: suggested, kind: 'md', dir: tab.docDir || app.folder || '' });
    if (!path) return false;
  }
  const text = tab.state.doc.toString().replace(/\n/g, tab.eol);
  try {
    const r = await call('file.write', { path, text, encoding: tab.encoding, bom: tab.bom });
    const renamed = path !== tab.path;
    tab.path = path; tab.mtime = r.mtime; tab.savedDoc = tab.state.doc; tab.dirty = false; tab.untitled = 0;
    if (renamed) { await attachBase(tab); call('recent.add', { path }); call('file.watch', { paths: app.tabs.filter((x) => x.path).map((x) => x.path) }); }
    emit('saved', tab); emit('tabs');
    if (tab === app.active) emit('active', tab);
    clearRecovery();
    return true;
  } catch (e) {
    toast(t('err.save', { m: e.message }), { kind: 'error' });
    return false;
  }
}

export async function saveAll() { for (const tab of [...app.tabs]) if (isDirty(tab)) if (!(await save(tab))) return false; return true; }

/** İlk başlıktan dosya adı önerir. */
function guessName(text) {
  const m = /^#\s+(.+)$/m.exec(text) || /^title:\s*(.+)$/m.exec(text);
  const base = (m ? m[1] : t('untitled')).replace(/[\\/:*?"<>|#]/g, '').trim().slice(0, 60) || t('untitled');
  return base + '.md';
}

/** Sekmeyi kapatır (kaydedilmemişse sorar). Dönüş: kapandı mı */
export async function closeTab(tab = app.active, { force = false } = {}) {
  if (!tab) return true;
  if (!force && isDirty(tab)) {
    if (tab !== app.active) activate(tab);
    const r = await ask(t('ask.saveChanges', { name: tabTitle(tab) }), [
      { label: t('save'), primary: true, value: 'save' }, { label: t('dontSave'), value: 'discard', danger: true }, { label: t('cancel'), value: null }]);
    if (r === null) return false;
    if (r === 'save' && !(await save(tab))) return false;
  }
  const i = app.tabs.indexOf(tab);
  if (i < 0) return true;
  if (tab === app.active) tab.state = app.view.state;
  if (tab.path) app.closed.push({ path: tab.path, cursor: tab.state.selection.main.head });
  if (app.closed.length > 20) app.closed.shift();
  app.tabs.splice(i, 1);
  if (app.active === tab) {
    app.active = null;
    const next = app.tabs[Math.min(i, app.tabs.length - 1)];
    if (next) activate(next); else { app.view.setState(app.view.newState('')); emit('active', null); }
  }
  emit('tabs');
  call('file.watch', { paths: app.tabs.filter((x) => x.path).map((x) => x.path) });
  saveSessionSoon();
  return true;
}

export async function reopenClosed() {
  const c = app.closed.pop();
  if (!c) return;
  const tab = await openPaths([c.path]);
  if (tab) app.view.dispatch({ selection: EditorSelection.cursor(Math.min(c.cursor, app.view.state.doc.length)), effects: EditorView.scrollIntoView(c.cursor, { y: 'center' }) });
}

/** Bütün sekmeleri kapatmadan önce sorar (pencere kapanırken). */
export async function confirmCloseAll() {
  if (app.active) { app.active.state = app.view.state; app.active.dirty = isDirty(app.active); }
  const dirty = app.tabs.filter(isDirty);
  if (!dirty.length) return true;
  const r = await ask(dirty.length === 1 ? t('ask.saveChanges', { name: tabTitle(dirty[0]) }) : t('ask.saveChangesMany', { n: dirty.length }), [
    { label: t('save'), primary: true, value: 'save' }, { label: t('dontSave'), value: 'discard', danger: true }, { label: t('cancel'), value: null }]);
  if (r === null) return false;
  if (r === 'save') { for (const d of dirty) if (!(await save(d))) return false; }
  return true;
}

// ===================== Dosya dışarıda değişirse =====================
export async function onFileChanged({ path, kind }) {
  const tab = app.tabs.find((x) => x.path && x.path.toLowerCase() === path.toLowerCase());
  if (!tab) return;
  if (kind === 'deleted') { tab.mtime = 0; toast(t('ask.deletedOnDisk', { name: tabTitle(tab) }), { kind: 'warn', timeout: 8000 }); return; }
  let r;
  try { r = await call('file.read', { path }); } catch { return; }
  if (r.mtime === tab.mtime) return;
  const text = normalize(r.text);
  const reload = () => {
    const isActive = tab === app.active;
    const st = isActive ? app.view.state : tab.state;
    const head = Math.min(st.selection.main.head, text.length);
    const tr = st.update({ changes: { from: 0, to: st.doc.length, insert: text }, selection: EditorSelection.cursor(head) });
    tab.state = tr.state; tab.savedDoc = tr.state.doc; tab.mtime = r.mtime; tab.dirty = false;
    if (isActive) app.view.setState(tab.state);
    emit('tabs'); if (isActive) emit('active', tab);
  };
  if (!isDirty(tab)) { reload(); return; }
  if (tab !== app.active) activate(tab);
  const ans = await ask(t('ask.changedOnDisk', { name: tabTitle(tab) }), [
    { label: t('ask.loadDisk'), value: 'disk', danger: true }, { label: t('ask.keepMine'), primary: true, value: 'mine' }]);
  if (ans === 'disk') reload(); else tab.mtime = r.mtime;
}

// ===================== Otomatik kaydetme =====================
let autoTimer = 0;
export function scheduleAutosave() {
  if (!app.settings.autosave) return;
  clearTimeout(autoTimer);
  autoTimer = setTimeout(() => { for (const tab of app.tabs) if (tab.path && isDirty(tab)) save(tab); }, app.settings.autosaveDelay || 2000);
}

// ===================== Kurtarma ve oturum =====================
let recTimer = 0;
export function scheduleRecovery() {
  clearTimeout(recTimer);
  recTimer = setTimeout(writeRecovery, 5000);
}
function writeRecovery() {
  if (app.active) app.active.state = app.view.state;
  const items = app.tabs.filter(isDirty).map((x) => ({ id: x.id, path: x.path, name: tabTitle(x), text: x.state.doc.toString() }));
  call(items.length ? 'recovery.save' : 'recovery.clear', { items });
}
function clearRecovery() { if (!app.tabs.some(isDirty)) call('recovery.clear', {}); else scheduleRecovery(); }

/** Açılışta: kurtarılacak belge varsa sorar. */
export async function offerRecovery() {
  const items = await call('recovery.list', {});
  if (!items?.length) return false;
  const r = await ask(t('ask.recovery', { n: items.length }), [{ label: t('restore'), primary: true, value: true }, { label: t('discard'), value: false, danger: true }]);
  if (r) {
    for (const it of items) {
      let tab = it.path ? app.tabs.find((x) => x.path?.toLowerCase() === it.path.toLowerCase()) : null;
      if (!tab && it.path) { tab = await openPaths([it.path]); }
      if (!tab) tab = newDoc('');
      activate(tab);
      app.view.dispatch({ changes: { from: 0, to: app.view.state.doc.length, insert: it.text } });
    }
  }
  call('recovery.clear', {});
  return !!r;
}

let sessTimer = 0;
export function saveSessionSoon() { clearTimeout(sessTimer); sessTimer = setTimeout(saveSession, 1000); }
export function saveSession() {
  if (app.active) app.active.state = app.view.state;
  return call('session.save', {
    folder: app.folder,
    active: app.active?.path || null,
    tabs: app.tabs.filter((x) => x.path).map((x) => ({ path: x.path, cursor: x.state.selection.main.head, scroll: x === app.active ? app.view.scrollDOM.scrollTop : x.scroll || 0 })),
  });
}

export async function restoreSession() {
  const s = await call('session.load', {});
  if (!s) return false;
  let any = false;
  for (const x of s.tabs || []) {
    const ok = await call('file.stat', { path: x.path });
    if (!ok?.exists) continue;
    const tab = await openPaths([x.path], { activateLast: false });
    if (tab) { tab.state = tab.state.update({ selection: EditorSelection.cursor(Math.min(x.cursor, tab.state.doc.length)) }).state; tab.scroll = x.scroll; any = true; }
  }
  const act = app.tabs.find((x) => x.path && x.path === s.active) || app.tabs[app.tabs.length - 1];
  if (act) { activate(act); requestAnimationFrame(() => { app.view.scrollDOM.scrollTop = act.scroll || 0; }); }
  if (s.folder) emit('open-folder', s.folder);
  return any;
}
