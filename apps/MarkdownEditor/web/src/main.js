// Markdown Editörü — ana modül: arayüzü kurar, modülleri birbirine bağlar.
import { app, mergeSettings, baseName, extOf, MD_EXT, IMG_EXT } from './state.js';
import { call, on as onHost, hasHost } from './bridge.js';
import { on, emit } from './events.js';
import { t, setLang, getLang, applyI18n } from './i18n.js';
import { icon } from './icons.js';
import { toast, showMenu, esc, ask } from './ui.js';
import { createEditor, reconfigure, setPlaceholder, toggleTask, insertSnippet, EditorView } from './editor.js';
import { paint, lineMap, lineToY, yToLine } from './preview.js';
import { stats, setAlertTitles, setMetaLabels } from './render.js';
import { def, run, registerEditing, editorKeymap, buildMenubar, buildToolbar, refreshToolbarState, handleKey, bindKeys, commands } from './commands.js';
import * as docs from './docs.js';
import * as dlg from './dialogs.js';
import * as xp from './exportui.js';
import { initSidebar, show as showSide, markOutline, relabel as relabelSide, openFolder, focusSearch } from './sidebar.js';
import { settingsDialog } from './settings.js';
import { TEMPLATES, CHEATSHEET, templateText } from './templates.js';
import { startTour } from './tour.js';
import { htmlToMarkdown, isRichHtml } from './importer.js';

const $ = (s) => document.querySelector(s);
const META = {
  tr: { title: 'Başlık', author: 'Yazar', date: 'Tarih', tags: 'Etiketler', description: 'Açıklama', subtitle: 'Alt başlık', version: 'Sürüm', lang: 'Dil', category: 'Kategori', keywords: 'Anahtar kelimeler' },
  en: { title: 'Title', author: 'Author', date: 'Date', tags: 'Tags', description: 'Description', subtitle: 'Subtitle', version: 'Version', lang: 'Language', category: 'Category', keywords: 'Keywords' },
};
const ui = {};

// ===================== Ayarlar =====================
let saveTimer = 0;
function saveSettings() { clearTimeout(saveTimer); saveTimer = setTimeout(() => call('settings.save', { settings: app.settings }), 400); }

// Yakınlaştırma basamakları (%)
const ZOOMS = [60, 70, 80, 90, 100, 110, 125, 150, 175, 200, 250];

/** Bir ayar değişince ilgili yerleri günceller. */
function applySetting(k) {
  const s = app.settings;
  if (k === 'lang' || k === '*') { setLang(s.lang); relabel(); }
  if (k === 'appTheme' || k === '*') applyTheme();
  if (['editorFont', 'editorFontSize', 'tabSize', 'wordWrap', 'lineNumbers', 'spellcheck', 'spellLang', 'focusMode', 'typewriter', 'editorMaxWidth', 'zoom', '*'].includes(k)) reconfigure(app.view, { ...s, editorFontSize: Math.round(s.editorFontSize * s.zoom / 100) });
  if (['previewTheme', 'previewFontSize', 'previewWidth', 'lineHeight', 'zoom', '*'].includes(k)) applyPreviewStyle();
  if (['breaks', 'remoteImages', 'previewTheme', '*'].includes(k)) renderSoon(0);
  if (['viewMode', 'sidebar', '*'].includes(k)) applyLayout();
  if (k === 'zoom') drawStatus();
  saveSettings();
  refreshChrome();
}

function applyTheme() {
  const m = app.settings.appTheme;
  app.dark = m === 'dark' || (m === 'system' && (hasHost ? app.info.dark : matchMedia('(prefers-color-scheme: dark)').matches));
  document.documentElement.dataset.theme = app.dark ? 'dark' : 'light';
  call('window.theme', { dark: app.dark });
  applyPreviewStyle();
  renderSoon(0);
}

function applyPreviewStyle() {
  const s = app.settings, z = s.zoom / 100;
  const el = ui.preview;
  const theme = s.previewTheme === 'dark' || (app.dark && s.previewTheme === 'grapekit') ? (s.previewTheme === 'dark' ? 'dark' : 'grapekit') : s.previewTheme;
  el.className = `preview md md-theme-${theme}${app.dark && s.previewTheme !== 'sepia' ? ' md-dark' : ''}`;
  el.style.setProperty('--md-size', s.previewFontSize * z + 'px');
  el.style.setProperty('--md-width', s.previewWidth ? s.previewWidth * z + 'px' : 'none');
  el.style.setProperty('--md-lh', s.lineHeight);
}

// ===================== Yerleşim =====================
function applyLayout() {
  const s = app.settings, w = ui.workspace;
  w.dataset.mode = app.active ? s.viewMode : 'welcome';
  w.classList.toggle('no-side', !s.sidebar || app.zen);
  document.body.classList.toggle('zen', app.zen);
  w.style.setProperty('--side-w', s.sideWidth + 'px');
  w.style.setProperty('--split', (s.splitRatio * 100).toFixed(2) + '%');
  ui.welcome.hidden = !!app.active;
  if (!app.active) drawWelcome();
  requestAnimationFrame(() => app.view.requestMeasure());
}

function dragSplitter(el, onMove, onEnd) {
  el.addEventListener('pointerdown', (e) => {
    e.preventDefault(); el.setPointerCapture(e.pointerId); document.body.classList.add('dragging');
    const move = (ev) => onMove(ev);
    const up = () => { el.removeEventListener('pointermove', move); el.removeEventListener('pointerup', up); document.body.classList.remove('dragging'); onEnd?.(); };
    el.addEventListener('pointermove', move); el.addEventListener('pointerup', up);
  });
}

// ===================== Önizleme =====================
let renderTimer = 0, rendering = false, again = false, map = [];
function renderSoon(delay) {
  clearTimeout(renderTimer);
  const len = app.view?.state.doc.length || 0;
  renderTimer = setTimeout(renderNow, delay ?? (len > 200000 ? 450 : len > 50000 ? 250 : 120));
}
async function renderNow() {
  if (!app.active || app.settings.viewMode === 'editor') { map = []; return; }
  if (rendering) { again = true; return; }
  rendering = true;
  const keep = ui.previewPane.scrollTop;
  try {
    await paint(ui.preview, app.view.state.doc.toString(), {
      docBase: app.active.docBase, remoteImages: app.settings.remoteImages, breaks: app.settings.breaks, dark: app.dark && app.settings.previewTheme !== 'sepia', copyLabel: t('copy'),
    });
    ui.previewPane.scrollTop = keep;
    map = lineMap(ui.preview);
    if (app.settings.syncScroll) syncFromEditor();
  } catch (e) { console.error(e); }
  rendering = false;
  if (again) { again = false; renderSoon(60); }
}
// Görseller yüklenince eşleme kayar
document.addEventListener('load', (e) => { if (e.target.tagName === 'IMG' && ui.preview.contains(e.target)) { clearTimeout(ui.mapT); ui.mapT = setTimeout(() => { map = lineMap(ui.preview); }, 100); } }, true);

// Senkron kaydırma: hangi bölme kaydırılıyorsa o önder
let leader = null, leaderT = 0;
function lead(who) { leader = who; clearTimeout(leaderT); leaderT = setTimeout(() => { leader = null; }, 160); }
function editorTopLine() {
  const v = app.view, top = v.scrollDOM.scrollTop;
  const blk = v.lineBlockAtHeight(top);
  const line = v.state.doc.lineAt(blk.from).number - 1;
  return line + Math.max(0, Math.min(1, (top - blk.top) / Math.max(1, blk.height)));
}
function syncFromEditor() {
  if (!app.settings.syncScroll || app.settings.viewMode !== 'split' || !map.length || leader === 'preview') return;
  const v = app.view;
  if (v.scrollDOM.scrollTop + v.scrollDOM.clientHeight >= v.scrollDOM.scrollHeight - 2) { ui.previewPane.scrollTop = ui.previewPane.scrollHeight; return; }
  ui.previewPane.scrollTop = lineToY(map, editorTopLine()) - 8;
}
function syncFromPreview() {
  if (!app.settings.syncScroll || app.settings.viewMode !== 'split' || !map.length || leader === 'editor') return;
  const p = ui.previewPane;
  const v = app.view;
  if (p.scrollTop + p.clientHeight >= p.scrollHeight - 2) { v.scrollDOM.scrollTop = v.scrollDOM.scrollHeight; return; }
  const line = yToLine(map, p.scrollTop + 8);
  const n = Math.floor(line), frac = line - n;
  const l = v.state.doc.line(Math.min(v.state.doc.lines, n + 1));
  const blk = v.lineBlockAt(l.from);
  v.scrollDOM.scrollTop = blk.top + blk.height * frac;
}

// ===================== Sekme şeridi =====================
function drawTabs() {
  const el = ui.tabs;
  el.innerHTML = app.tabs.map((x) => {
    const dirty = docs.isDirty(x);
    return `<div class="tab${x === app.active ? ' active' : ''}${dirty ? ' dirty' : ''}" role="tab" aria-selected="${x === app.active}" draggable="true" data-id="${x.id}" title="${esc(x.path || docs.tabTitle(x))}">
      ${icon('file')}<span class="tab-name">${esc(docs.tabTitle(x))}</span><button type="button" class="tab-x" tabindex="-1" aria-label="${esc(t('cmd.file.close'))}"><i class="dot"></i>${icon('x')}</button></div>`;
  }).join('');
  el.querySelector('.tab.active')?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
  const title = app.active ? `${docs.isDirty(app.active) ? '● ' : ''}${docs.tabTitle(app.active)} — ${t('app.name')}` : t('app.name');
  if (document.title !== title) document.title = title;
}
function wireTabs() {
  const el = ui.tabs;
  const tabOf = (n) => app.tabs.find((x) => x.id === n?.closest('.tab')?.dataset.id);
  el.addEventListener('mousedown', (e) => {
    const tab = tabOf(e.target); if (!tab) return;
    if (e.button === 1) { e.preventDefault(); docs.closeTab(tab); return; }
    if (e.button === 0 && !e.target.closest('.tab-x')) docs.activate(tab);
  });
  el.addEventListener('click', (e) => { if (e.target.closest('.tab-x')) docs.closeTab(tabOf(e.target)); });
  el.addEventListener('dblclick', (e) => { if (!e.target.closest('.tab')) run('file.new'); });
  el.addEventListener('wheel', (e) => { el.scrollLeft += e.deltaY; }, { passive: true });
  el.addEventListener('contextmenu', (e) => {
    const tab = tabOf(e.target); if (!tab) return;
    e.preventDefault();
    const idx = app.tabs.indexOf(tab);
    showMenu([
      { label: t('cmd.file.close'), run: () => docs.closeTab(tab), kbd: 'Ctrl+W' },
      { label: getLang() === 'en' ? 'Close others' : 'Diğerlerini kapat', run: async () => { for (const x of app.tabs.filter((y) => y !== tab)) if (!(await docs.closeTab(x))) break; } },
      { label: getLang() === 'en' ? 'Close tabs to the right' : 'Sağdakileri kapat', run: async () => { for (const x of app.tabs.slice(idx + 1)) if (!(await docs.closeTab(x))) break; } },
      '-',
      { label: t('cmd.file.save'), run: () => docs.save(tab), kbd: 'Ctrl+S' },
      { label: getLang() === 'en' ? 'Copy path' : 'Yolu kopyala', disabled: !tab.path, run: () => navigator.clipboard.writeText(tab.path) },
      { label: t('cmd.file.reveal'), disabled: !tab.path, run: () => call('shell.reveal', { path: tab.path }) },
    ], e.clientX, e.clientY);
  });
  // Sürükleyerek sıralama
  let dragId = null;
  el.addEventListener('dragstart', (e) => { dragId = e.target.closest('.tab')?.dataset.id; e.dataTransfer.effectAllowed = 'move'; e.dataTransfer.setData('text/x-tab', dragId); });
  el.addEventListener('dragover', (e) => { if (dragId) { e.preventDefault(); } });
  el.addEventListener('drop', (e) => {
    if (!dragId) return;
    e.preventDefault();
    const over = tabOf(e.target), from = app.tabs.findIndex((x) => x.id === dragId);
    if (over && from >= 0) { const [m] = app.tabs.splice(from, 1); let to = app.tabs.indexOf(over); const r = e.target.closest('.tab').getBoundingClientRect(); if (e.clientX > r.left + r.width / 2) to++; app.tabs.splice(to, 0, m); drawTabs(); docs.saveSessionSoon(); }
    dragId = null;
  });
  el.addEventListener('dragend', () => { dragId = null; });
}

// ===================== Durum çubuğu =====================
let statT = 0;
function drawStatus() {
  clearTimeout(statT);
  statT = setTimeout(() => {
    const el = ui.status;
    if (!app.active) { el.innerHTML = ''; return; }
    const st = app.view.state, r = st.selection.main, line = st.doc.lineAt(r.head);
    const selN = st.selection.ranges.reduce((a, x) => a + (x.to - x.from), 0);
    const s = stats(st.doc.toString());
    const tab = app.active;
    const enc = tab.encoding === 'utf-8' ? (tab.bom ? 'UTF-8 BOM' : 'UTF-8') : tab.encoding.toUpperCase();
    el.innerHTML = `<span>${esc(t('st.line', { l: line.number, c: r.head - line.from + 1 }))}</span>
      ${selN ? `<span>${esc(t('st.sel', { n: selN }))}${st.selection.ranges.length > 1 ? ` · ${st.selection.ranges.length} ↕` : ''}</span>` : ''}
      <span>${esc(t('st.words', { n: s.words.toLocaleString(getLang()) }))}</span><span>${esc(t('st.chars', { n: s.chars.toLocaleString(getLang()) }))}</span>
      <span>${esc(t('st.read', { n: s.minutes }))}</span><span class="st-grow"></span>
      ${tab.path ? `<span class="st-save ${docs.isDirty(tab) ? 'dirty' : ''}">${esc(docs.isDirty(tab) ? t('st.unsaved') : t('st.saved'))}</span>` : ''}
      <button type="button" class="st-btn" data-st="autosave" title="${esc(t('st.autosave'))}">${icon('save')}${esc(app.settings.autosave ? t('on') : t('off'))}</button>
      <button type="button" class="st-btn" data-st="enc">${esc(enc)}</button><button type="button" class="st-btn" data-st="eol">${tab.eol === '\r\n' ? 'CRLF' : 'LF'}</button>
      <button type="button" class="st-btn" data-st="spell">${icon('check')}${esc(app.settings.spellcheck ? (app.settings.spellLang === 'en' ? 'EN' : 'TR') : t('off'))}</button>
      <button type="button" class="st-btn" data-st="zoom">${app.settings.zoom}%</button>`;
  }, 60);
}
function wireStatus() {
  ui.status.addEventListener('click', (e) => {
    const b = e.target.closest('[data-st]'); if (!b) return;
    const r = b.getBoundingClientRect(), tab = app.active;
    const k = b.dataset.st;
    if (k === 'autosave') { app.settings.autosave = !app.settings.autosave; applySetting('autosave'); drawStatus(); }
    else if (k === 'eol') showMenu([['\r\n', 'CRLF (Windows)'], ['\n', 'LF (Unix / macOS)']].map(([v, l]) => ({ label: l, checked: tab.eol === v, run: () => { tab.eol = v; tab.savedDoc = app.view.newState('').doc; drawStatus(); drawTabs(); } })), r.left, r.top - 90);
    else if (k === 'enc') showMenu([['utf-8', false, 'UTF-8'], ['utf-8', true, 'UTF-8 BOM'], ['utf-16le', false, 'UTF-16 LE'], ['windows-1254', false, 'Windows-1254 (Türkçe)']].map(([v, bom, l]) => ({ label: l, checked: tab.encoding === v && tab.bom === bom, run: () => { tab.encoding = v; tab.bom = bom; tab.savedDoc = app.view.newState('').doc; drawStatus(); drawTabs(); } })), r.left, r.top - 130);
    else if (k === 'spell') showMenu([{ label: t('off'), checked: !app.settings.spellcheck, run: () => { app.settings.spellcheck = false; applySetting('spellcheck'); } }, { label: 'Türkçe', checked: app.settings.spellcheck && app.settings.spellLang === 'tr', run: () => { app.settings.spellcheck = true; app.settings.spellLang = 'tr'; applySetting('spellLang'); } }, { label: 'English', checked: app.settings.spellcheck && app.settings.spellLang === 'en', run: () => { app.settings.spellcheck = true; app.settings.spellLang = 'en'; applySetting('spellLang'); } }], r.left, r.top - 110);
    else if (k === 'zoom') showMenu(ZOOMS.map((z) => ({ label: z + '%', checked: app.settings.zoom === z, run: () => { app.settings.zoom = z; applySetting('zoom'); } })), r.left, r.top - ZOOMS.length * 32 - 16);
  });
}

// ===================== Tanıtım =====================
/** Tur bir belge açıkken anlamlı: yoksa yeni bir belge açılır. */
function runTour() {
  if (!app.active) docs.newDoc('');
  applyLayout();
  setTimeout(() => startTour(() => { if (!app.settings.tourDone) { app.settings.tourDone = true; saveSettings(); } }), 0);
}

// ===================== Karşılama =====================
async function drawWelcome() {
  const recent = (await call('recent.list', {})) || [];
  const lang = getLang();
  ui.welcome.innerHTML = `<div class="wl">
    <div class="wl-head"><img src="icon.png" width="64" height="64" alt=""><div><h1>${esc(t('app.name'))}</h1><p>${esc(t('welcome.lead'))}</p></div></div>
    <div class="wl-actions">
      <button type="button" class="wl-big" data-a="new">${icon('filePlus')}<b>${esc(t('welcome.new'))}</b><kbd>Ctrl+N</kbd></button>
      <button type="button" class="wl-big" data-a="open">${icon('folderOpen')}<b>${esc(t('welcome.open'))}</b><kbd>Ctrl+O</kbd></button>
      <button type="button" class="wl-big" data-a="folder">${icon('folder')}<b>${esc(t('welcome.folder'))}</b><kbd>Ctrl+Shift+O</kbd></button>
      <button type="button" class="wl-big" data-a="guide">${icon('help')}<b>${esc(t('cmd.help.cheatsheet'))}</b><kbd>F1</kbd></button>
    </div>
    <div class="wl-cols">
      <section><h2>${esc(t('welcome.templates'))}</h2><div class="wl-list">${TEMPLATES.map((x) => `<button type="button" data-tpl="${x.id}">${icon(x.icon)}<span>${esc(t('tpl.' + x.id))}</span></button>`).join('')}</div></section>
      <section><h2>${esc(t('welcome.recent'))}</h2><div class="wl-list">${recent.length ? recent.slice(0, 10).map((r) => `<button type="button" data-path="${esc(r.path)}" title="${esc(r.path)}">${icon(r.pinned ? 'pin' : 'file')}<span>${esc(baseName(r.path))}<small>${esc(r.path.replace(/[\\/][^\\/]*$/, ''))}</small></span></button>`).join('') : `<p class="side-empty">${esc(t('welcome.none'))}</p>`}</div></section>
    </div>
    <p class="wl-hint">${t('welcome.hint')}</p></div>`;
  ui.welcome.onclick = (e) => {
    const b = e.target.closest('button'); if (!b) return;
    if (b.dataset.a === 'new') run('file.new'); else if (b.dataset.a === 'open') run('file.open'); else if (b.dataset.a === 'folder') run('file.openFolder'); else if (b.dataset.a === 'guide') run('help.cheatsheet');
    else if (b.dataset.tpl) docs.newDoc(templateText(TEMPLATES.find((x) => x.id === b.dataset.tpl), lang));
    else if (b.dataset.path) docs.openPaths([b.dataset.path]);
  };
  ui.welcome.oncontextmenu = (e) => {
    const b = e.target.closest('[data-path]'); if (!b) return;
    e.preventDefault();
    const pinned = recent.find((r) => r.path === b.dataset.path)?.pinned;
    showMenu([{ label: pinned ? (lang === 'en' ? 'Unpin' : 'Sabitlemeyi kaldır') : (lang === 'en' ? 'Pin' : 'Sabitle'), icon: 'pin', run: async () => { await call('recent.pin', { path: b.dataset.path, pinned: !pinned }); drawWelcome(); } },
      { label: lang === 'en' ? 'Remove from list' : 'Listeden kaldır', icon: 'x', run: async () => { await call('recent.remove', { path: b.dataset.path }); drawWelcome(); } }], e.clientX, e.clientY);
  };
}

// ===================== Yapıştır ve bırak =====================
async function onPaste(e, view) {
  const cd = e.clipboardData; if (!cd) return false;
  // Görsel
  const file = [...cd.items].find((i) => i.kind === 'file' && i.type.startsWith('image/'))?.getAsFile();
  if (file && !cd.getData('text/plain')) {
    e.preventDefault();
    const b64 = await blobToB64(file);
    dlg.insertImageData(b64, file.type.split('/')[1].replace('jpeg', 'jpg'), file.name && file.name !== 'image.png' ? file.name : '');
    return true;
  }
  const text = cd.getData('text/plain'), html = cd.getData('text/html');
  // URL'yi seçili metne bağlantı olarak yapıştır
  const sel = view.state.selection.main;
  if (!sel.empty && /^(https?:\/\/|mailto:)\S+$/i.test(text.trim()) && !view.state.sliceDoc(sel.from, sel.to).includes('\n')) {
    e.preventDefault();
    insertSnippet(view, `[{sel}](${text.trim()})‸`);
    return true;
  }
  if (app.settings.smartPaste && !pastePlain && isRichHtml(html, text)) {
    e.preventDefault();
    const md = htmlToMarkdown(html);
    view.dispatch(view.state.replaceSelection(md));
    return true;
  }
  pastePlain = false;
  return false;
}
let pastePlain = false;
const blobToB64 = (b) => new Promise((res) => { const r = new FileReader(); r.onload = () => res(String(r.result).split(',')[1]); r.readAsDataURL(b); });

async function onDrop(e, view) {
  const files = [...(e.dataTransfer?.files || [])];
  const mdPath = e.dataTransfer?.getData('text/x-md-path');
  if (mdPath) { e.preventDefault(); const pos = view.posAtCoords({ x: e.clientX, y: e.clientY }); if (pos != null) view.dispatch({ selection: { anchor: pos } }); const rel = app.active?.path ? await call('path.relative', { from: app.active.docDir, to: mdPath }) : mdPath; insertSnippet(view, `[${baseName(mdPath).replace(/\.[^.]+$/, '')}](${rel.includes(' ') ? `<${rel}>` : rel})‸`); return true; }
  if (!files.length) return false;
  e.preventDefault();
  const pos = view.posAtCoords({ x: e.clientX, y: e.clientY });
  if (pos != null) view.dispatch({ selection: { anchor: pos } });
  const paths = await call('drop.paths', {}, e.dataTransfer.files);
  for (let i = 0; i < files.length; i++) {
    const f = files[i], p = paths?.[i];
    const ext = extOf(f.name);
    if (IMG_EXT.includes(ext)) {
      if (p && app.active?.path) { const r = await call('image.import', { docDir: app.active.docDir, folder: app.settings.imageFolder, source: p }); insertSnippet(view, `![${f.name.replace(/\.[^.]+$/, '')}](${r.rel.includes(' ') ? `<${r.rel}>` : r.rel})\n‸`); }
      else dlg.insertImageData(await blobToB64(f), ext, f.name);
    } else if (MD_EXT.includes(ext) && p) docs.openPaths([p]);
  }
  return true;
}

// ===================== Önizleme etkileşimleri =====================
function wirePreview() {
  ui.preview.addEventListener('click', async (e) => {
    const a = e.target.closest('a[href]');
    if (a) {
      e.preventDefault();
      const href = a.getAttribute('href');
      if (href.startsWith('#')) { const id = decodeURIComponent(href.slice(1)); const tgt = ui.preview.querySelector(`[id="${CSS.escape(id)}"]`); tgt?.scrollIntoView({ behavior: 'smooth', block: 'start' }); return; }
      if (/^(https?:|mailto:)/i.test(href)) { call('shell.url', { url: href }); return; }
      const clean = href.replace(/^https:\/\/doc\.grapekit\.local\/[0-9a-f]+\//, '').split('#')[0];
      const path = await call('path.resolve', { dir: app.active?.docDir || '', rel: decodeURIComponent(clean) });
      if (MD_EXT.includes(extOf(path))) docs.openPaths([path]); else call('shell.open', { path });
      return;
    }
    const task = e.target.closest('input.task');
    if (task) { e.preventDefault(); toggleTask(app.view, +task.dataset.line); return; }
    const cp = e.target.closest('.code-copy');
    if (cp) { const code = cp.parentElement.querySelector('code')?.innerText ?? ''; await navigator.clipboard.writeText(code); cp.textContent = t('copied'); setTimeout(() => { cp.textContent = t('copy'); }, 1400); return; }
    const blocked = e.target.closest('img.img-blocked');
    if (blocked) { blocked.src = blocked.dataset.blocked; blocked.classList.remove('img-blocked'); }
  });
  // Önizlemede çift tıkla → kaynağa git
  ui.preview.addEventListener('dblclick', (e) => {
    const n = e.target.closest('[data-line]'); if (!n || app.settings.viewMode === 'preview') return;
    const line = +n.dataset.line;
    const l = app.view.state.doc.line(line + 1);
    app.view.dispatch({ selection: { anchor: l.from }, effects: EditorView.scrollIntoView(l.from, { y: 'center' }) });
    app.view.focus();
  });
}

// ===================== Komutlar =====================
function registerCommands() {
  registerEditing();
  const S = app.settings;
  const view = (m) => () => { S.viewMode = m; applySetting('viewMode'); renderSoon(0); };
  def('file.new', { run: () => docs.newDoc(''), kbd: 'Ctrl+N', icon: 'filePlus' });
  def('file.newTemplate', { run: () => dlg.templateDialog(), kbd: 'Ctrl+Shift+N', icon: 'template' });
  def('file.open', { run: () => docs.openDialog(), kbd: 'Ctrl+O', icon: 'folderOpen' });
  def('file.openFolder', { run: () => openFolder(null, saveSettings), kbd: 'Ctrl+Shift+O', icon: 'folder' });
  def('file.save', { run: () => docs.save(), kbd: 'Ctrl+S', icon: 'save', enabled: () => !!app.active });
  def('file.saveAs', { run: () => docs.save(app.active, { as: true }), kbd: 'Ctrl+Shift+S', enabled: () => !!app.active });
  def('file.saveAll', { run: () => docs.saveAll(), kbd: 'Ctrl+Alt+S' });
  def('file.close', { run: () => docs.closeTab(), kbd: ['Ctrl+W', 'Ctrl+F4'], enabled: () => !!app.active });
  def('file.reopen', { run: () => docs.reopenClosed(), kbd: 'Ctrl+Shift+T', enabled: () => app.closed.length > 0 });
  def('file.import', { run: () => xp.importDialog(), icon: 'import' });
  def('file.saveTemplate', { run: () => dlg.saveAsTemplate(saveSettings), enabled: () => !!app.active });
  def('file.reveal', { run: () => call('shell.reveal', { path: app.active.path }), enabled: () => !!app.active?.path, icon: 'external' });
  def('file.print', { run: () => xp.printDoc(), kbd: 'Ctrl+Shift+P', icon: 'printer', enabled: () => !!app.active });
  def('file.settings', { run: () => settingsDialog(applySetting), kbd: 'Ctrl+,', icon: 'settings' });
  def('file.exit', { run: () => requestClose(), kbd: 'Alt+F4' });
  def('file.recent', {
    run: () => {}, hidden: true,
    submenu: () => [], // menü açılırken doldurulur (aşağıda)
  });
  def('edit.paste', { run: async () => { app.view.focus(); try { const tx = await navigator.clipboard.readText(); app.view.dispatch(app.view.state.replaceSelection(tx)); } catch { document.execCommand('paste'); } } });
  def('edit.pastePlain', { run: async () => { app.view.focus(); try { const tx = await navigator.clipboard.readText(); app.view.dispatch(app.view.state.replaceSelection(tx)); } catch { /* izin yok */ } }, kbd: 'Ctrl+Shift+V' });
  def('edit.findInFiles', { run: () => { S.sidebar = true; applySetting('sidebar'); focusSearch(); }, kbd: 'Ctrl+Shift+F', icon: 'search' });
  def('edit.gotoLine', { run: () => dlg.gotoDialog(), kbd: 'Ctrl+G', enabled: () => !!app.active });
  def('edit.palette', { run: () => dlg.palette(), kbd: 'Ctrl+P', icon: 'search' });
  def('edit.copyHtml', { run: () => xp.copyAsHtml(), kbd: 'Ctrl+Alt+C', enabled: () => !!app.active });

  def('view.editor', { run: view('editor'), kbd: 'Ctrl+Alt+1', icon: 'pen', checked: () => S.viewMode === 'editor' });
  def('view.split', { run: view('split'), kbd: 'Ctrl+Alt+2', icon: 'split', checked: () => S.viewMode === 'split' });
  def('view.preview', { run: view('preview'), kbd: 'Ctrl+Alt+3', icon: 'eye', checked: () => S.viewMode === 'preview' });
  def('view.sidebar', { run: () => { S.sidebar = !S.sidebar; applySetting('sidebar'); }, kbd: 'Ctrl+Shift+B', icon: 'sidebar', checked: () => S.sidebar });
  def('view.outline', { run: () => { S.sidebar = true; applySetting('sidebar'); showSide('outline'); }, kbd: 'Ctrl+Shift+L', icon: 'toc' });
  def('view.files', { run: () => { S.sidebar = true; applySetting('sidebar'); showSide('files'); }, kbd: 'Ctrl+Shift+E', icon: 'folder' });
  def('view.focus', { run: () => { S.focusMode = !S.focusMode; applySetting('focusMode'); }, kbd: 'Ctrl+Alt+F', icon: 'focus', checked: () => S.focusMode });
  def('view.typewriter', { run: () => { S.typewriter = !S.typewriter; applySetting('typewriter'); }, kbd: 'Ctrl+Alt+T', icon: 'typewriter', checked: () => S.typewriter });
  def('view.zen', { run: () => toggleZen(), kbd: 'Ctrl+Shift+Enter', icon: 'fullscreen', checked: () => app.zen });
  def('view.fullscreen', { run: () => call('window.fullscreen', {}), kbd: 'F11' });
  def('view.syncScroll', { run: () => { S.syncScroll = !S.syncScroll; applySetting('syncScroll'); }, checked: () => S.syncScroll });
  def('view.wrap', { run: () => { S.wordWrap = !S.wordWrap; applySetting('wordWrap'); }, kbd: 'Alt+Z', checked: () => S.wordWrap });
  def('view.lineNumbers', { run: () => { S.lineNumbers = !S.lineNumbers; applySetting('lineNumbers'); }, checked: () => S.lineNumbers });
  def('view.spell', { run: () => { S.spellcheck = !S.spellcheck; applySetting('spellcheck'); }, kbd: 'F7', checked: () => S.spellcheck });
  def('view.zoomIn', { run: () => { S.zoom = ZOOMS.find((z) => z > S.zoom) ?? ZOOMS.at(-1); applySetting('zoom'); }, kbd: ['Ctrl+=', 'Ctrl++'] });
  def('view.zoomOut', { run: () => { S.zoom = ZOOMS.findLast((z) => z < S.zoom) ?? ZOOMS[0]; applySetting('zoom'); }, kbd: 'Ctrl+-' });
  def('view.zoomReset', { run: () => { S.zoom = 100; applySetting('zoom'); }, kbd: 'Ctrl+Alt+0' });
  const themeNames = { grapekit: 'Grapekit', github: 'GitHub', academic: () => (getLang() === 'en' ? 'Academic' : 'Akademik'), sepia: () => (getLang() === 'en' ? 'Sepia' : 'Sepya'), dark: () => t('dark') };
  def('view.theme', { run: () => {}, submenu: () => Object.entries(themeNames).map(([k, n]) => ({ label: typeof n === 'function' ? n() : n, checked: S.previewTheme === k, run: () => { S.previewTheme = k; applySetting('previewTheme'); } })), icon: 'palette' });
  def('view.appTheme', { run: () => {}, submenu: () => [['system', t('system')], ['light', t('light')], ['dark', t('dark')]].map(([k, n]) => ({ label: n, checked: S.appTheme === k, run: () => { S.appTheme = k; applySetting('appTheme'); } })) });

  def('insert.link', { run: () => app.active && dlg.linkDialog(), kbd: 'Ctrl+K', icon: 'link' });
  def('insert.image', { run: () => app.active && dlg.imageDialog(), kbd: 'Ctrl+Shift+I', icon: 'image' });
  def('insert.table', { run: () => app.active && dlg.tableDialog(), kbd: 'Ctrl+Alt+G', icon: 'table' });
  def('insert.emoji', { run: () => app.active && dlg.emojiDialog(), kbd: 'Ctrl+Shift+J', icon: 'emoji' });
  def('insert.alert', { run: () => app.active && dlg.alertDialog(), icon: 'alert' });

  def('exp.pdf', { run: () => xp.exportPdf(saveSettings), kbd: 'Ctrl+Alt+P', icon: 'pdf', enabled: () => !!app.active });
  def('exp.html', { run: () => xp.exportHtml(saveSettings), enabled: () => !!app.active });
  def('exp.docx', { run: () => xp.exportDocx(), enabled: () => !!app.active });
  def('exp.png', { run: () => xp.exportPng(saveSettings), enabled: () => !!app.active, icon: 'image' });
  def('exp.batch', { run: () => xp.batchDialog(saveSettings), icon: 'layers' });

  def('help.cheatsheet', { run: () => { const tab = docs.newDoc(CHEATSHEET[getLang()]); tab.savedDoc = tab.state.doc; tab.guide = true; app.view.setState(tab.state); docs.activate(tab); S.viewMode = 'split'; applySetting('viewMode'); }, kbd: 'F1', icon: 'help' });
  def('help.tour', { run: () => runTour() });
  def('help.shortcuts', { run: () => dlg.shortcutsDialog(), kbd: 'Ctrl+Alt+K', icon: 'keyboard' });
  def('help.feedback', { run: () => call('shell.url', { which: 'feedback' }) });
  def('help.site', { run: () => call('shell.url', { which: 'page' }), icon: 'external' });
  def('help.coffee', { run: () => call('shell.url', { which: 'coffee' }), icon: 'coffee' });
  def('help.updates', { run: () => settingsDialog(applySetting, 'about'), hidden: () => app.info.packaged });
  def('help.about', { run: () => settingsDialog(applySetting, 'about'), icon: 'help' });
  bindKeys();
}

const MENUS = [
  { label: 'menu.file', items: () => ['file.new', 'file.newTemplate', '-', 'file.open', 'file.openFolder', { label: t('cmd.file.recent'), icon: 'clock', submenu: recentMenu }, 'file.reopen', '-', 'file.save', 'file.saveAs', 'file.saveAll', 'file.saveTemplate', '-', 'file.import', 'file.print', 'file.reveal', '-', 'file.close', 'file.settings', 'file.exit'] },
  { label: 'menu.edit', items: () => ['edit.undo', 'edit.redo', '-', 'edit.cut', 'edit.copy', 'edit.paste', 'edit.pastePlain', 'edit.copyHtml', '-', 'edit.selectAll', 'edit.nextMatch', '-', 'edit.find', 'edit.replace', 'edit.findInFiles', 'edit.gotoLine', '-', 'edit.lineUp', 'edit.lineDown', 'edit.dupLine', '-', 'edit.palette'] },
  { label: 'menu.view', items: () => ['view.editor', 'view.split', 'view.preview', '-', 'view.sidebar', 'view.outline', 'view.files', '-', 'view.theme', 'view.appTheme', '-', 'view.focus', 'view.typewriter', 'view.zen', 'view.fullscreen', '-', 'view.syncScroll', 'view.wrap', 'view.lineNumbers', 'view.spell', '-', 'view.zoomIn', 'view.zoomOut', 'view.zoomReset'] },
  { label: 'menu.insert', items: () => ['insert.link', 'insert.image', 'insert.table', 'insert.code', '-', 'insert.math', 'insert.mathInline', 'insert.mermaid', 'insert.alert', 'insert.details', '-', 'insert.footnote', 'insert.toc', 'insert.frontMatter', 'insert.pagebreak', 'insert.hr', '-', 'insert.emoji', 'insert.date'] },
  { label: 'menu.format', items: () => ['fmt.bold', 'fmt.italic', 'fmt.strike', 'fmt.code', 'fmt.mark', 'fmt.sup', 'fmt.sub', '-', 'fmt.h1', 'fmt.h2', 'fmt.h3', 'fmt.h4', 'fmt.h5', 'fmt.h6', 'fmt.h0', '-', 'fmt.quote', 'fmt.ul', 'fmt.ol', 'fmt.task', 'fmt.indent', 'fmt.outdent', '-', 'fmt.clear'] },
  { label: 'menu.table', items: () => ['insert.table', '-', 'tbl.format', '-', 'tbl.rowAbove', 'tbl.rowBelow', 'tbl.colLeft', 'tbl.colRight', '-', 'tbl.delRow', 'tbl.delCol', '-', 'tbl.alignL', 'tbl.alignC', 'tbl.alignR'] },
  { label: 'menu.export', items: () => ['exp.pdf', 'exp.html', 'exp.docx', 'exp.png', '-', 'edit.copyHtml', 'file.print', '-', 'exp.batch'] },
  { label: 'menu.help', items: () => ['help.cheatsheet', 'help.shortcuts', 'help.tour', '-', 'help.feedback', 'help.site', 'help.coffee', '-', ...(app.info.packaged ? [] : ['help.updates']), 'help.about'] },
];

let recentCache = [];
function recentMenu() {
  if (!recentCache.length) return [{ label: t('welcome.none'), disabled: true }];
  return recentCache.slice(0, 15).map((r) => ({ label: baseName(r.path), icon: r.pinned ? 'pin' : 'file', run: () => docs.openPaths([r.path]) }));
}

function headingButton() {
  const b = document.createElement('button');
  b.type = 'button'; b.className = 'tb-btn tb-wide'; b.innerHTML = icon('heading') + icon('chevD', 'ic ic-sm');
  b.title = t('cmd.fmt.h'); b.setAttribute('aria-label', t('cmd.fmt.h'));
  b.addEventListener('mousedown', (e) => e.preventDefault());
  b.onclick = () => { const r = b.getBoundingClientRect(); showMenu([1, 2, 3, 4, 5, 6, 0].map((n) => ({ label: n ? `${t('cmd.fmt.h')} ${n}` : t('cmd.fmt.h0'), kbd: 'Ctrl+' + n, run: () => run('fmt.h' + n) })), r.left, r.bottom + 4); };
  return b;
}
function viewSeg() {
  const d = document.createElement('div'); d.className = 'tb-seg'; d.setAttribute('role', 'radiogroup');
  for (const [m, ic] of [['editor', 'pen'], ['split', 'split'], ['preview', 'eye']]) {
    const b = document.createElement('button'); b.type = 'button'; b.dataset.cmd = 'view.' + m; b.innerHTML = icon(ic);
    b.title = t('cmd.view.' + m); b.setAttribute('aria-label', t('cmd.view.' + m)); b.setAttribute('role', 'radio');
    b.onclick = () => run('view.' + m); d.append(b);
  }
  return d;
}
function exportButton() {
  const b = document.createElement('button'); b.type = 'button'; b.className = 'btn btn-primary btn-sm tb-export';
  b.innerHTML = icon('export') + `<span>${esc(t('export'))}</span>` + icon('chevD', 'ic ic-sm');
  b.onclick = () => { const r = b.getBoundingClientRect(); showMenu(MENUS[6].items().map((id) => (id === '-' ? '-' : { label: commands.get(id) ? (commands.get(id).label?.() ?? t('cmd.' + id)) : id, icon: commands.get(id)?.icon, kbd: [].concat(commands.get(id)?.kbd || [])[0], run: () => run(id) })), r.right, r.bottom + 4, { align: 'right' }); };
  return b;
}

function buildChrome() {
  ui.menubar = buildMenubar($('.menubar'), MENUS);
  buildToolbar($('.toolbar'), [
    { items: ['edit.undo', 'edit.redo'] },
    { items: [{ el: headingButton }, 'fmt.bold', 'fmt.italic', 'fmt.strike', 'fmt.code', 'fmt.mark'] },
    { items: ['fmt.quote', 'fmt.ul', 'fmt.ol', 'fmt.task'] },
    { items: ['insert.link', 'insert.image', 'insert.table', 'insert.code'] },
    { items: ['insert.math', 'insert.mermaid', 'insert.alert', 'insert.footnote', 'insert.pagebreak', 'insert.emoji'] },
    { items: ['view.focus', 'view.zen'] },
    { end: true, items: [{ el: viewSeg }, 'view.sidebar', { el: exportButton }] },
  ]);
  applyI18n();
  refreshChrome();
}
function refreshChrome() { refreshToolbarState($('.toolbar')); for (const b of document.querySelectorAll('.tb-seg button')) b.setAttribute('aria-checked', String(b.dataset.cmd === 'view.' + app.settings.viewMode)); }
function relabel() {
  setMetaLabels(META[getLang()]);
  setAlertTitles({ note: t('alert.note'), tip: t('alert.tip'), important: t('alert.important'), warning: t('alert.warning'), caution: t('alert.caution') });
  buildChrome(); relabelSide(); drawTabs(); drawStatus(); setPlaceholder(app.view, t('editor.placeholder')); if (!app.active) drawWelcome(); renderSoon(0);
  call('app.lang', { lang: getLang() });
}

function toggleZen() {
  app.zen = !app.zen;
  if (app.zen && app.settings.viewMode === 'split') { app.zenPrev = 'split'; app.settings.viewMode = 'editor'; }
  else if (!app.zen && app.zenPrev) { app.settings.viewMode = app.zenPrev; app.zenPrev = null; }
  applyLayout(); call('window.fullscreen', { on: app.zen }); refreshChrome();
}

// ===================== Kapanış =====================
let closing = false;
async function requestClose() {
  if (closing) return;
  closing = true;
  try {
    if (!(await docs.confirmCloseAll())) return;
    await docs.saveSession();
    call('recovery.clear', {});
    await call('app.exit', {});
  } finally { closing = false; }
}

// ===================== Başlangıç =====================
async function boot() {
  app.info = (await call('app.info', {})) || app.info;
  app.settings = mergeSettings(await call('settings.load', {}));
  if (!app.settings.lang) app.settings.lang = app.info.lang || 'tr';
  if (!app.info.lang) app.info.lang = app.settings.lang;
  setLang(app.settings.lang);
  setMetaLabels(META[getLang()]);
  setAlertTitles({ note: t('alert.note'), tip: t('alert.tip'), important: t('alert.important'), warning: t('alert.warning'), caution: t('alert.caution') });

  Object.assign(ui, { workspace: $('.workspace'), preview: $('.preview'), previewPane: $('.preview-pane'), tabs: $('.tabs'), status: $('.statusbar'), welcome: $('.welcome') });
  app.view = createEditor($('.editor'), { ...app.settings, editorFontSize: Math.round(app.settings.editorFontSize * app.settings.zoom / 100) }, {
    keymap: editorKeymap,
    onChange: (v) => { emit('doc'); renderSoon(); drawStatus(); docs.scheduleAutosave(); docs.scheduleRecovery(); if (app.active) { const d = docs.isDirty(app.active); if (d !== app.active._shownDirty) { app.active._shownDirty = d; drawTabs(); } } },
    onSelection: (v) => { drawStatus(); markOutline(v.state.doc.lineAt(v.state.selection.main.head).number - 1); },
    onScroll: () => { if (leader !== 'preview') { lead('editor'); syncFromEditor(); } },
    onPaste: (e, v) => { onPaste(e, v); return e.defaultPrevented; },
    onDrop: (e, v) => { onDrop(e, v); return e.defaultPrevented; },
  });
  setPlaceholder(app.view, t('editor.placeholder'));
  ui.previewPane.addEventListener('scroll', () => { if (leader !== 'editor') { lead('preview'); syncFromPreview(); } }, { passive: true });

  registerCommands();
  buildChrome();
  wireTabs(); wireStatus(); wirePreview();
  initSidebar($('.sidebar'), saveSettings);
  // Geliştirme derlemesi: otomatik testlerin erişimi
  if (app.info.debug) window.__mde = { app, run, docs, dlg, xp, emit, call, settingsDialog, applySetting, importer: () => import('./importer.js') };

  // Bölücüler
  dragSplitter($('.split'), (e) => {
    const r = ui.workspace.querySelector('.editor-pane').parentElement.getBoundingClientRect();
    const left = app.settings.sidebar && !app.zen ? app.settings.sideWidth + 4 : 0;
    app.settings.splitRatio = Math.min(0.85, Math.max(0.15, (e.clientX - r.left - left) / (r.width - left)));
    ui.workspace.style.setProperty('--split', (app.settings.splitRatio * 100).toFixed(2) + '%');
  }, () => { saveSettings(); map = lineMap(ui.preview); });
  $('.split').addEventListener('dblclick', () => { app.settings.splitRatio = 0.5; applyLayout(); saveSettings(); });
  dragSplitter($('.side-split'), (e) => {
    app.settings.sideWidth = Math.min(520, Math.max(180, e.clientX - ui.workspace.getBoundingClientRect().left));
    ui.workspace.style.setProperty('--side-w', app.settings.sideWidth + 'px');
  }, saveSettings);

  // Pencere düğmeleri
  for (const b of document.querySelectorAll('[data-win]')) b.onclick = () => (b.dataset.win === 'close' ? requestClose() : call('window.' + b.dataset.win, {}));

  // Kısayollar (düzenleyici dışında da)
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Alt' && !e.repeat) { ui.altDown = true; return; }
    ui.altDown = false;
    if (document.querySelector('.modal-back') && !e.target?.closest?.('.cm-editor')) return;
    handleKey(e);
  }, true);
  document.addEventListener('keyup', (e) => { if (e.key === 'Alt' && ui.altDown) { ui.altDown = false; ui.menubar.focus(); } });
  document.addEventListener('contextmenu', (e) => { if (!e.target?.closest?.('.cm-content, input, textarea, .preview')) e.preventDefault(); });
  // Pencereye dosya bırakma (düzenleyici dışı)
  document.addEventListener('dragover', (e) => { if ([...(e.dataTransfer?.types || [])].includes('Files')) e.preventDefault(); });
  document.addEventListener('drop', async (e) => {
    if (e.target?.closest?.('.cm-editor') || !e.dataTransfer?.files?.length) return;
    e.preventDefault();
    const paths = await call('drop.paths', {}, e.dataTransfer.files);
    if (paths?.length) docs.openPaths(paths.filter((p) => MD_EXT.includes(extOf(p))));
  });

  // Olaylar
  on('tabs', () => { drawTabs(); applyLayout(); docs.saveSessionSoon(); });
  on('active', (tab) => {
    // İlk kez bir belge açıldığında tanıtım (komut satırı dönüştürmede değil)
    if (tab && !app.settings.tourDone && !app.info.headless && !document.querySelector('.modal')) setTimeout(() => { if (!app.settings.tourDone && app.active && !document.querySelector('.tour, .modal')) runTour(); }, 700);
    drawTabs(); drawStatus(); applyLayout(); renderSoon(0);
    if (tab) requestAnimationFrame(() => { ui.previewPane.scrollTop = tab.previewScroll || 0; });
  });
  on('saved', () => { drawTabs(); drawStatus(); });
  on('folder', () => { docs.saveSessionSoon(); });
  on('insert-image-path', async (p) => {
    if (!app.active?.path) { toast(t('ask.saveFirstImage'), { kind: 'warn' }); return; }
    const rel = await call('path.relative', { from: app.active.docDir, to: p });
    insertSnippet(app.view, `![${baseName(p).replace(/\.[^.]+$/, '')}](${rel.includes(' ') ? `<${rel}>` : rel})‸`);
  });
  on('reveal-line', (line) => { if (app.settings.viewMode !== 'editor' && map.length) { lead('editor'); ui.previewPane.scrollTo({ top: lineToY(map, line) - 8, behavior: 'smooth' }); } });

  onHost('open-files', ({ paths }) => docs.openPaths(paths));
  onHost('file-changed', (d) => docs.onFileChanged(d));
  onHost('close-request', () => requestClose());
  onHost('theme-changed', ({ dark }) => { app.info.dark = dark; if (app.settings.appTheme === 'system') applyTheme(); });
  onHost('window-state', ({ maximized, fullscreen }) => {
    document.body.classList.toggle('maximized', !!maximized); document.body.classList.toggle('fullscreen', !!fullscreen);
    const b = document.querySelector('[data-win="max"]');
    if (b) { b.title = t(maximized ? 'win.restore' : 'win.max'); b.innerHTML = maximized ? '<svg viewBox="0 0 10 10"><rect x="2.5" y=".5" width="7" height="7"/><path d="M.5 2.5v7h7"/></svg>' : '<svg viewBox="0 0 10 10"><rect x=".5" y=".5" width="9" height="9"/></svg>'; }
  });
  onHost('update-status', (r) => { Object.assign(app.info, r); if (r.updateAvailable && r.notify) toast(r.updateStatus, { actions: [{ label: t('cmd.help.updates'), run: () => settingsDialog(applySetting, 'about') }], timeout: 10000 }); });
  onHost('cli-pdf', async ({ input, output, id, format }) => {
    try { await xp.cliConvert(input, output, format || 'pdf'); call('cli.done', { id, ok: true }); } catch (e) { call('cli.done', { id, ok: false, error: e.message || String(e) }); }
  });
  onHost('recent', (list) => { recentCache = list || []; });

  applyTheme();
  applyPreviewStyle();
  applyLayout();
  recentCache = (await call('recent.list', {})) || [];

  // Açılış: kurtarma → oturum → komut satırı dosyaları
  // Gezgin'den "PDF'e dönüştür" (pencere görünmez): oturum ve kurtarmaya dokunma
  const recovered = app.info.headless ? true : await docs.offerRecovery();
  if (!recovered && app.settings.restoreSession) await docs.restoreSession();
  const args = (app.info.args || []).filter((a) => MD_EXT.includes(extOf(a)));
  if (args.length) await docs.openPaths(args);
  applyLayout();
  document.body.classList.add('ready');
  call('app.ready', {});
}

boot().catch((e) => { console.error(e); document.body.classList.add('ready'); toast(String(e.message || e), { kind: 'error', timeout: 0 }); });
