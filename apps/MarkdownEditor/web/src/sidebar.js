// Kenar çubuğu: İçindekiler, Dosyalar (klasör ağacı), Ara (dosyalarda).
import { app, baseName, dirName, extOf, MD_EXT, IMG_EXT } from './state.js';
import { call, on as onHost } from './bridge.js';
import { emit, on } from './events.js';
import { t } from './i18n.js';
import { icon } from './icons.js';
import { esc, showMenu, prompt, ask, toast } from './ui.js';
import { headings } from './render.js';
import { gotoLine } from './editor.js';
import { openPaths } from './docs.js';

let root, tabsEl, bodyEl, current = 'outline';
const expanded = new Set();
let tree = null; // { path, dirs: [], files: [] } klasör → içerik önbelleği
const cache = new Map();

export function initSidebar(el, saveSettings) {
  root = el;
  tabsEl = el.querySelector('.side-tabs');
  bodyEl = el.querySelector('.side-body');
  current = app.settings.sideTab || 'outline';
  drawTabs();
  tabsEl.addEventListener('click', (e) => { const b = e.target.closest('[data-side]'); if (b) { show(b.dataset.side); app.settings.sideTab = current; saveSettings(); } });
  on('active', () => { if (current === 'outline') drawOutline(); else if (current === 'files') markActiveFile(); });
  on('doc', () => { if (current === 'outline') outlineSoon(); });
  on('open-folder', (p) => openFolder(p, saveSettings));
  onHost('folder-changed', ({ path }) => { cache.delete(path.toLowerCase()); if (current === 'files') drawFiles(); });
  show(current);
}

function drawTabs() {
  tabsEl.innerHTML = ['outline', 'files', 'search'].map((k) => `<button type="button" role="tab" data-side="${k}" aria-selected="${k === current}" title="${esc(t('side.' + k))}">${icon(k === 'outline' ? 'toc' : k === 'files' ? 'folder' : 'search')}<span>${esc(t('side.' + k))}</span></button>`).join('');
}

export function show(which) {
  current = which;
  for (const b of tabsEl.querySelectorAll('[data-side]')) b.setAttribute('aria-selected', String(b.dataset.side === which));
  if (which === 'outline') drawOutline(); else if (which === 'files') drawFiles(); else drawSearch();
}
export const currentSide = () => current;
export function relabel() { drawTabs(); show(current); }

// ===================== İçindekiler =====================
let outlineTimer = 0, outlineData = [];
function outlineSoon() { clearTimeout(outlineTimer); outlineTimer = setTimeout(drawOutline, 300); }
function drawOutline() {
  if (!app.active) { bodyEl.innerHTML = `<p class="side-empty">${esc(t('side.noOutline'))}</p>`; return; }
  outlineData = headings(app.view.state.doc.toString());
  if (!outlineData.length) { bodyEl.innerHTML = `<p class="side-empty">${esc(t('side.noOutline'))}</p>`; return; }
  const min = Math.min(...outlineData.map((x) => x.level));
  bodyEl.innerHTML = `<nav class="outline">${outlineData.map((x, i) => `<button type="button" class="ol-item ol-l${x.level - min + 1}" data-i="${i}" title="${esc(x.text)}">${esc(x.text || '—')}</button>`).join('')}</nav>`;
  bodyEl.querySelector('.outline').onclick = (e) => {
    const b = e.target.closest('.ol-item'); if (!b) return;
    const h = outlineData[+b.dataset.i];
    gotoLine(app.view, h.line + 1);
    emit('reveal-line', h.line);
  };
  markOutline(app.view.state.doc.lineAt(app.view.state.selection.main.head).number - 1);
}
/** İmlecin bulunduğu bölümü vurgular. */
export function markOutline(line) {
  if (current !== 'outline' || !outlineData.length) return;
  let k = -1;
  for (let i = 0; i < outlineData.length; i++) if (outlineData[i].line <= line) k = i; else break;
  for (const b of bodyEl.querySelectorAll('.ol-item')) b.classList.toggle('cur', +b.dataset.i === k);
}

// ===================== Dosyalar =====================
export async function openFolder(path, saveSettings) {
  if (!path) { path = await call('folder.dialog', {}); if (!path) return; }
  app.folder = path; cache.clear(); expanded.clear(); expanded.add(path.toLowerCase());
  call('folder.watch', { path });
  emit('folder', path);
  saveSettings?.();
  show('files');
  app.settings.sideTab = 'files';
}

async function list(dir) {
  const k = dir.toLowerCase();
  if (!cache.has(k)) cache.set(k, await call('folder.list', { path: dir }));
  return cache.get(k);
}

async function drawFiles() {
  if (!app.folder) {
    bodyEl.innerHTML = `<div class="side-empty"><p>${esc(t('side.noFolder'))}</p><button type="button" class="btn btn-primary" id="sf-open">${icon('folderOpen')}${esc(t('side.openFolder'))}</button></div>`;
    bodyEl.querySelector('#sf-open').onclick = () => openFolder(null);
    return;
  }
  const head = `<div class="side-head"><b title="${esc(app.folder)}">${esc(baseName(app.folder))}</b>
    <button type="button" class="icon-btn" data-a="newFile" title="${esc(t('side.newFile'))}">${icon('filePlus')}</button>
    <button type="button" class="icon-btn" data-a="newFolder" title="${esc(t('side.newFolder'))}">${icon('folderPlus')}</button>
    <button type="button" class="icon-btn" data-a="refresh" title="${esc(t('side.refresh'))}">${icon('refresh')}</button>
    <button type="button" class="icon-btn" data-a="close" title="${esc(t('side.closeFolder'))}">${icon('x')}</button></div>`;
  const html = await branch(app.folder, 0);
  bodyEl.innerHTML = head + `<div class="tree" role="tree">${html}</div>`;
  bodyEl.querySelector('.side-head').onclick = async (e) => {
    const a = e.target.closest('[data-a]')?.dataset.a;
    if (a === 'refresh') { cache.clear(); drawFiles(); }
    else if (a === 'close') { app.folder = null; call('folder.watch', { path: '' }); emit('folder', null); drawFiles(); }
    else if (a === 'newFile') newEntry(app.folder, false);
    else if (a === 'newFolder') newEntry(app.folder, true);
  };
  const tr = bodyEl.querySelector('.tree');
  tr.onclick = async (e) => {
    const n = e.target.closest('.tn'); if (!n) return;
    if (n.dataset.dir) { const k = n.dataset.path.toLowerCase(); expanded.has(k) ? expanded.delete(k) : expanded.add(k); drawFiles(); }
    else openFileNode(n.dataset.path);
  };
  tr.oncontextmenu = (e) => {
    const n = e.target.closest('.tn'); if (!n) return;
    e.preventDefault();
    const p = n.dataset.path, isDir = !!n.dataset.dir, parent = isDir ? p : dirName(p);
    showMenu([
      ...(isDir ? [] : [{ label: t('open'), icon: 'file', run: () => openFileNode(p) }]),
      { label: t('side.newFile'), icon: 'filePlus', run: () => newEntry(parent, false) },
      { label: t('side.newFolder'), icon: 'folderPlus', run: () => newEntry(parent, true) },
      '-',
      { label: t('side.rename'), icon: 'edit2', run: () => renameEntry(p) },
      { label: t('side.delete'), icon: 'trash', run: () => deleteEntry(p) },
      '-',
      { label: t('cmd.file.reveal'), icon: 'external', run: () => call('shell.reveal', { path: p }) },
    ], e.clientX, e.clientY);
  };
  tr.ondragstart = (e) => { const n = e.target.closest('.tn'); if (n && !n.dataset.dir) e.dataTransfer.setData('text/x-md-path', n.dataset.path); };
  markActiveFile();
}

async function branch(dir, depth) {
  const r = await list(dir);
  if (!r) return '';
  let html = '';
  for (const d of r.dirs) {
    const open = expanded.has(d.toLowerCase());
    html += `<div class="tn tn-dir" role="treeitem" aria-expanded="${open}" data-dir="1" data-path="${esc(d)}" style="--d:${depth}" tabindex="-1">${icon(open ? 'chevD' : 'chevR', 'ic tw')}${icon(open ? 'folderOpen' : 'folder')}<span>${esc(baseName(d))}</span></div>`;
    if (open) html += await branch(d, depth + 1);
  }
  for (const f of r.files) {
    const ext = extOf(f);
    if (!MD_EXT.includes(ext) && !IMG_EXT.includes(ext)) continue;
    html += `<div class="tn tn-file${IMG_EXT.includes(ext) ? ' tn-img' : ''}" role="treeitem" draggable="true" data-path="${esc(f)}" style="--d:${depth}" tabindex="-1"><span class="tw"></span>${icon(IMG_EXT.includes(ext) ? 'image' : 'file')}<span>${esc(baseName(f))}</span></div>`;
  }
  return html;
}

function markActiveFile() {
  const p = app.active?.path?.toLowerCase();
  for (const n of bodyEl.querySelectorAll('.tn-file')) n.classList.toggle('cur', n.dataset.path.toLowerCase() === p);
}

function openFileNode(p) {
  if (IMG_EXT.includes(extOf(p))) { emit('insert-image-path', p); return; }
  openPaths([p]);
}

async function newEntry(parent, isDir) {
  const name = await prompt(t(isDir ? 'side.newFolder' : 'side.newFile'), { value: isDir ? '' : '.md' });
  if (!name) return;
  try {
    const r = await call(isDir ? 'dir.create' : 'file.create', { dir: parent, name: isDir ? name : /\.[a-z0-9]+$/i.test(name) ? name : name + '.md' });
    expanded.add(parent.toLowerCase()); cache.delete(parent.toLowerCase());
    await drawFiles();
    if (!isDir) openPaths([r.path]);
  } catch (e) { toast(e.message, { kind: 'error' }); }
}

async function renameEntry(p) {
  const name = await prompt(t('side.rename'), { value: baseName(p) });
  if (!name || name === baseName(p)) return;
  try {
    const r = await call('fs.rename', { path: p, name });
    for (const tab of app.tabs) if (tab.path?.toLowerCase() === p.toLowerCase()) { tab.path = r.path; }
    cache.clear(); emit('tabs'); drawFiles();
  } catch (e) { toast(e.message, { kind: 'error' }); }
}

async function deleteEntry(p) {
  const ok = await ask(t('side.deleteConfirm', { name: baseName(p) }), [{ label: t('side.delete'), danger: true, primary: true, value: true }, { label: t('cancel'), value: false }]);
  if (!ok) return;
  try { await call('fs.recycle', { path: p }); cache.clear(); drawFiles(); } catch (e) { toast(e.message, { kind: 'error' }); }
}

// ===================== Ara =====================
let lastQuery = { q: '', regex: false, matchCase: false };
function drawSearch() {
  bodyEl.innerHTML = `<div class="side-search">
    <input class="field" id="ss-q" type="search" placeholder="${esc(t('side.searchPh'))}" value="${esc(lastQuery.q)}">
    <div class="ss-opts"><label class="mini-check"><input type="checkbox" id="ss-re"${lastQuery.regex ? ' checked' : ''}> ${esc(t('side.searchRegex'))}</label>
    <label class="mini-check"><input type="checkbox" id="ss-case"${lastQuery.matchCase ? ' checked' : ''}> ${esc(t('side.searchCase'))}</label></div>
    <div class="ss-res"></div></div>`;
  const q = bodyEl.querySelector('#ss-q'), res = bodyEl.querySelector('.ss-res');
  let timer = 0;
  const go = async () => {
    lastQuery = { q: q.value, regex: bodyEl.querySelector('#ss-re').checked, matchCase: bodyEl.querySelector('#ss-case').checked };
    if (!lastQuery.q.trim()) { res.innerHTML = ''; return; }
    if (!app.folder) { res.innerHTML = `<p class="side-empty">${esc(t('side.needFolder'))}</p>`; return; }
    const r = await call('folder.search', { path: app.folder, query: lastQuery.q, regex: lastQuery.regex, matchCase: lastQuery.matchCase });
    if (!r?.length) { res.innerHTML = `<p class="side-empty">${esc(t('side.noResults'))}</p>`; return; }
    const total = r.reduce((a, f) => a + f.hits.length, 0);
    res.innerHTML = `<p class="ss-sum">${esc(t('side.results', { n: total, f: r.length }))}</p>` + r.map((f) => `<div class="ss-file"><div class="ss-fn" title="${esc(f.path)}">${icon('file')}${esc(baseName(f.path))}<small>${esc(dirName(f.path).slice(app.folder.length).replace(/^[\\/]/, ''))}</small></div>
      ${f.hits.map((h) => `<button type="button" class="ss-hit" data-path="${esc(f.path)}" data-line="${h.line}" data-col="${h.col}"><span class="ln">${h.line}</span><span>${esc(h.before)}<mark>${esc(h.match)}</mark>${esc(h.after)}</span></button>`).join('')}</div>`).join('');
  };
  q.oninput = () => { clearTimeout(timer); timer = setTimeout(go, 300); };
  bodyEl.querySelector('#ss-re').onchange = go; bodyEl.querySelector('#ss-case').onchange = go;
  res.onclick = async (e) => {
    const b = e.target.closest('.ss-hit'); if (!b) return;
    await openPaths([b.dataset.path]);
    gotoLine(app.view, +b.dataset.line, +b.dataset.col);
  };
  q.focus();
  if (lastQuery.q) go();
}
export function focusSearch() { show('search'); }
