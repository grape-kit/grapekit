// Formaty arayüzü: ana sayfa (bırakma alanı + hızlı araçlar) ve iş ekranı (dosya listesi + hedef/ayarlar + kuyruk).
import { call, on, hasHost } from './bridge.js';
import { t, setLang, getLang, applyI18n } from './i18n.js';
import { icon } from './icons.js';
import { esc, h, toast, dialog, ask, prompt, showMenu, closeMenus } from './ui.js';
import { TARGETS, target, groupsFor, DEFAULT_OPTS, buildRequest, outExt, QUICK, TOOLS, BUILTIN_PRESETS } from './formats.js';
import { settingsDialog } from './settings.js';
import { startTour } from './tour.js';

const $ = (s, r = document) => r.querySelector(s);
const clone = (o) => JSON.parse(JSON.stringify(o));

// ===================== Durum =====================
const DEFAULTS = {
  lang: '', theme: 'system', tourDone: false, autoUpdate: true, notify: true, parallel: 0, hw: true,
  output: { mode: 'same', dir: '', sub: 'Formaty', template: '{name}', conflict: 'rename', keepDates: true, recycle: false, keepStructure: true },
  opts: clone(DEFAULT_OPTS), lastTarget: { image: 'webp', video: 'mp4', audio: 'mp3', pdf: 'pdf-img' }, targetId: '',
  presets: [],
};
export const app = {
  info: {}, settings: clone(DEFAULTS), files: [], view: 'home', targetId: '', job: null, result: null, seq: 0,
};
const S = () => app.settings;

function merge(base, saved) {
  if (!saved || typeof saved !== 'object') return base;
  for (const k of Object.keys(saved)) {
    if (base[k] && typeof base[k] === 'object' && !Array.isArray(base[k]) && saved[k] && typeof saved[k] === 'object' && !Array.isArray(saved[k])) merge(base[k], saved[k]);
    else if (saved[k] !== undefined) base[k] = saved[k];
  }
  return base;
}

let saveTimer = 0;
export function saveSettings() { clearTimeout(saveTimer); saveTimer = setTimeout(() => call('settings.save', { settings: app.settings }), 300); }

// ===================== Biçimlendirme =====================
const nf = () => new Intl.NumberFormat(getLang() === 'en' ? 'en-US' : 'tr-TR', { maximumFractionDigits: 1 });
export function size(b) {
  if (b == null || b < 0) return '';
  const u = ['B', 'KB', 'MB', 'GB', 'TB']; let i = 0; let v = b;
  while (v >= 1000 && i < u.length - 1) { v /= 1024; i++; } // "1.001 KB" yerine "1 MB"
  return `${nf().format(i === 0 ? v : v < 10 ? Math.round(v * 10) / 10 : Math.round(v))} ${u[i]}`;
}
function dur(s) {
  if (!(s > 0)) return '';
  s = Math.round(s); const hh = Math.floor(s / 3600), mm = Math.floor((s % 3600) / 60), ss = s % 60;
  return hh ? `${hh}:${String(mm).padStart(2, '0')}:${String(ss).padStart(2, '0')}` : `${mm}:${String(ss).padStart(2, '0')}`;
}
function parseTime(v) {
  v = String(v || '').trim().replace(',', '.'); if (!v) return 0;
  const p = v.split(':').map(Number); if (p.some(isNaN)) return 0;
  return p.reduce((a, x) => a * 60 + x, 0);
}
const baseName = (p) => p.split(/[\\/]/).pop();
const dirOf = (p) => p.replace(/[\\/][^\\/]*$/, '');

/** Yeni sürüm varsa ayarlar simgesinde (ve açıksa ayarlar penceresinde) kırmızı "1" rozeti. */
export function updateBadges() {
  for (const el of document.querySelectorAll('.tb-settings, .set-nav [data-s="about"]')) {
    let b = el.querySelector('.upd-badge');
    if (app.info.updateAvailable && !b) { b = document.createElement('span'); b.className = 'upd-badge'; b.textContent = '1'; el.append(b); }
    if (!app.info.updateAvailable) b?.remove();
    if (b) b.title = app.info.updateLabel || '';
  }
  window.dispatchEvent(new CustomEvent('formaty-update'));
}

// ===================== Tema ve dil =====================
function applyTheme() {
  const m = S().theme;
  app.dark = m === 'dark' || (m === 'system' && (hasHost ? app.info.dark : matchMedia('(prefers-color-scheme: dark)').matches));
  document.documentElement.dataset.theme = app.dark ? 'dark' : 'light';
  call('window.theme', { dark: app.dark });
}

export function applySetting(k) {
  if (k === 'lang' || k === '*') { setLang(S().lang || app.info.lang); call('app.lang', { lang: getLang() }); applyI18n(); render(); }
  if (k === 'theme' || k === '*') applyTheme();
  saveSettings();
}

// ===================== Dosyalar =====================
const KIND_ICON = { image: 'image', video: 'video', audio: 'audio', pdf: 'pdf' };

/** Yolları (dosya/klasör) listeye ekler. */
async function addPaths(paths, { recursive = true } = {}) {
  if (!paths?.length) return 0;
  const items = (await call('files.expand', { paths, recursive })) || [];
  const have = new Set(app.files.map((f) => f.path.toLowerCase()));
  let n = 0;
  for (const it of items) {
    if (have.has(it.path.toLowerCase())) continue;
    have.add(it.path.toLowerCase());
    app.files.push({ id: ++app.seq, ...it, status: 'idle', progress: 0 });
    n++;
  }
  const skipped = paths.length && !items.length;
  if (skipped) toast(t('files.noneSupported'), { kind: 'warn' });
  if (n) {
    resetResults();
    if (!app.targetId || !compatibleCount(target(app.targetId))) pickDefaultTarget();
    showView('work');
  }
  render();
  return n;
}

function resetResults() {
  if (app.job) return;
  app.result = null;
  for (const f of app.files) if (f.status !== 'idle') { f.status = 'idle'; f.progress = 0; f.error = ''; f.out = ''; f.outSize = 0; }
}

function kinds() { return new Set(app.files.map((f) => f.kind)); }
const accepts = (tg, f) => tg && tg.accepts.includes(f.kind);
const compatibleCount = (tg) => app.files.filter((f) => accepts(tg, f)).length;

function pickDefaultTarget() {
  const gs = groupsFor(kinds());
  if (!gs.length) { app.targetId = ''; return; }
  // En çok dosyası olan türün grubu
  const count = (g) => app.files.filter((f) => accepts(target(S().lastTarget[g]) || TARGETS.find((x) => x.group === g), f)).length;
  const g = gs.sort((a, b) => count(b) - count(a))[0];
  app.targetId = S().lastTarget[g] || TARGETS.find((x) => x.group === g).id;
}

function removeFile(id) {
  if (app.job) return;
  app.files = app.files.filter((f) => f.id !== id);
  if (!app.files.length) { app.result = null; showView('home'); }
  render();
}

function clearFiles() {
  if (app.job) return;
  app.files = []; app.result = null; showView('home'); render();
}

// Küçük resim ve bilgi: görünür olunca istenir
const thumbObserver = 'IntersectionObserver' in window ? new IntersectionObserver((entries) => {
  for (const e of entries) {
    if (!e.isIntersecting) continue;
    thumbObserver.unobserve(e.target);
    const f = app.files.find((x) => x.id === +e.target.dataset.id);
    if (f) loadMeta(f);
  }
}, { rootMargin: '200px' }) : null;

async function loadMeta(f) {
  if (f.metaLoading) return;
  f.metaLoading = true;
  const [info, thumb] = await Promise.all([
    f.kind === 'audio' || f.kind === 'video' || f.kind === 'pdf' || f.kind === 'image' ? call('file.info', { path: f.path }).catch(() => null) : null,
    f.kind !== 'audio' ? call('file.thumb', { path: f.path, size: 112 }).catch(() => null) : null,
  ]);
  f.info = info || {}; f.thumb = thumb;
  const row = $(`.frow[data-id="${f.id}"]`);
  if (row) row.replaceWith(rowEl(f));
}

// ===================== Görünüm =====================
function showView(v) {
  app.view = v;
  document.body.dataset.view = v;
  for (const b of document.querySelectorAll('.tb-nav button')) b.setAttribute('aria-current', String(b.dataset.view === v));
  if (v === 'work' && !S().tourDone && app.files.length) setTimeout(() => runTour(), 500);
}

function render() {
  renderNav();
  if (app.view === 'home') renderHome(); else renderWork();
}

function renderNav() {
  const b = $('.tb-nav [data-view="work"] .count');
  if (b) b.textContent = app.files.length ? String(app.files.length) : '';
  $('.tb-nav [data-view="work"]').disabled = !app.files.length;
}

// ---------- Ana sayfa ----------
function renderHome() {
  const el = $('#home');
  el.innerHTML = `<div class="home">
    <div class="drop" tabindex="0">
      <div class="drop-art">${icon('upload')}</div>
      <h1>${esc(t('home.title'))}</h1>
      <p>${esc(t('home.lead'))}</p>
      <div class="drop-btns">
        <button type="button" class="btn btn-primary btn-lg" data-a="files">${icon('filePlus')}${esc(t('add.files'))}</button>
        <button type="button" class="btn btn-lg" data-a="folder">${icon('folderPlus')}${esc(t('add.folder'))}</button>
      </div>
      <div class="drop-kinds"><span>${icon('image')}${esc(t('kind.image'))}</span><span>${icon('video')}${esc(t('kind.video'))}</span><span>${icon('audio')}${esc(t('kind.audio'))}</span><span>${icon('pdf')}PDF</span></div>
    </div>
    <h2 class="home-h">${esc(t('home.tools'))}</h2>
    <div class="tools">${TOOLS.map((x) => `<button type="button" class="tool" data-tool="${x.id}">
      <span class="tool-ic k-${x.kind}">${icon(x.icon)}</span><span class="tool-t"><b>${esc(t(x.title))}</b><small>${esc(t(x.sub))}</small></span></button>`).join('')}</div>
    <p class="home-hint">${t('home.hint')}</p>
  </div>`;
  el.querySelector('[data-a="files"]').onclick = () => pickFiles();
  el.querySelector('[data-a="folder"]').onclick = () => pickFolder();
  el.querySelector('.drop').onkeydown = (e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); pickFiles(); } };
  for (const b of el.querySelectorAll('.tool')) b.onclick = () => useTool(TOOLS.find((x) => x.id === b.dataset.tool));
}

async function pickFiles(kind = '') {
  const paths = await call('dialog.files', { kind });
  if (paths?.length) await addPaths(paths);
}
async function pickFolder() {
  const p = await call('dialog.folder', {});
  if (p) await addPaths([p]);
}

/** Hızlı araç: hedefi ve ayarları hazırlar, dosya seçtirir. */
async function useTool(tool) {
  applyTarget(tool.target, tool.patch);
  const before = app.files.length;
  await pickFiles(tool.kind);
  if (app.files.length > before) { app.targetId = tool.target; render(); }
}

function applyTarget(id, patch) {
  const tg = target(id); if (!tg) return;
  app.targetId = id;
  S().lastTarget[tg.group] = id;
  if (patch) merge(S().opts[tg.group], clone(patch));
  saveSettings();
}

// ---------- İş ekranı ----------
function renderWork() {
  const el = $('#work');
  if (!el.firstElementChild) {
    el.innerHTML = `<section class="files">
        <div class="files-h">
          <h2>${esc(t('files.title'))} <span class="files-count"></span></h2>
          <div class="files-tools">
            <button type="button" class="btn btn-sm" data-a="files">${icon('filePlus')}<span>${esc(t('add.files'))}</span></button>
            <button type="button" class="btn btn-sm" data-a="folder">${icon('folderPlus')}<span>${esc(t('add.folder'))}</span></button>
            <button type="button" class="icon-btn" data-a="more" title="${esc(t('files.more'))}" aria-label="${esc(t('files.more'))}">${icon('more')}</button>
          </div>
        </div>
        <div class="result" hidden></div>
        <div class="flist" role="list"></div>
      </section>
      <aside class="panel">
        <div class="panel-scroll"></div>
        <div class="panel-foot"></div>
      </aside>`;
    el.querySelector('[data-a="files"]').onclick = () => pickFiles();
    el.querySelector('[data-a="folder"]').onclick = () => pickFolder();
    el.querySelector('[data-a="more"]').onclick = (e) => {
      const r = e.currentTarget.getBoundingClientRect();
      showMenu([
        { label: t('sort.name'), icon: 'sliders', run: () => sortFiles('name') },
        { label: t('sort.size'), run: () => sortFiles('size') },
        { label: t('sort.kind'), run: () => sortFiles('kind') },
        '-',
        { label: t('files.removeDone'), icon: 'check', disabled: !!app.job || !app.files.some((f) => f.status === 'done'), run: () => { app.files = app.files.filter((f) => f.status !== 'done'); if (!app.files.length) showView('home'); render(); } },
        { label: t('files.clear'), icon: 'trash', disabled: !!app.job, run: clearFiles },
      ], r.right, r.bottom + 4, { align: 'right' });
    };
  }
  renderList();
  renderPanel();
  renderFoot();
  renderResult();
}

function sortFiles(by) {
  if (app.job) return;
  const c = new Intl.Collator(getLang(), { numeric: true, sensitivity: 'base' });
  app.files.sort(by === 'size' ? (a, b) => b.size - a.size : by === 'kind' ? (a, b) => a.kind.localeCompare(b.kind) || c.compare(a.rel, b.rel) : (a, b) => c.compare(a.rel, b.rel));
  renderList();
}

function renderList() {
  const list = $('#work .flist');
  if (!list) return;
  $('#work .files-count').textContent = app.files.length ? `· ${app.files.length}` : '';
  list.replaceChildren(...app.files.map(rowEl));
}

function metaText(f) {
  const i = f.info || {};
  const parts = [f.ext.toUpperCase(), size(f.size)];
  if (i.width && i.height && f.kind !== 'audio') parts.push(`${i.width}×${i.height}`);
  if (i.duration) parts.push(dur(i.duration));
  if (f.kind === 'pdf' && i.pages) parts.push(t('meta.pages', { n: i.pages }));
  if (i.encrypted) parts.push(t('meta.locked'));
  return parts.filter(Boolean).join(' · ');
}

function rowEl(f) {
  const tg = target(app.targetId);
  const ok = accepts(tg, f);
  const thumb = f.thumb ? `<img src="${esc(f.thumb)}" alt="" loading="lazy">` : `<span class="fth-ic k-${f.kind}">${icon(KIND_ICON[f.kind] || 'file')}</span>`;
  const sub = f.rel && f.rel.includes('\\') ? dirOf(f.rel) : '';
  const reorder = tg && (tg.id === 'pdf-merge' || (S().opts[tg.group]?.combine && (tg.group === 'video' || tg.group === 'audio')) || (tg.id === 'img-pdf' && S().opts.image.combinePdf));
  let status = '';
  switch (f.status) {
    case 'working': status = `<div class="fprog"><i style="width:${Math.round(f.progress * 100)}%"></i></div><span class="fpct">${Math.round(f.progress * 100)}%</span>`; break;
    case 'done': {
      const saved = f.outSize && f.size ? Math.round((1 - f.outSize / f.size) * 100) : null;
      status = `<span class="fres ok">${icon('checkCircle')}<span>${f.outSize ? esc(size(f.outSize)) : esc(t('st.done'))}${saved != null && f.count <= 1 ? ` <em class="${saved >= 0 ? 'down' : 'up'}">${saved >= 0 ? '−' : '+'}${Math.abs(saved)}%</em>` : ''}${f.count > 1 ? ` · ${esc(t('st.files', { n: f.count }))}` : ''}</span></span>
        ${f.out ? `<button type="button" class="icon-btn" data-a="reveal" title="${esc(t('st.reveal'))}" aria-label="${esc(t('st.reveal'))}">${icon('folderOpen')}</button>` : ''}`;
      break;
    }
    case 'error': status = `<span class="fres err" title="${esc(f.error)}">${icon('alert')}<span>${esc(f.error || t('st.error'))}</span></span>`; break;
    case 'skipped': status = `<span class="fres muted">${esc(f.error || t('st.skipped'))}</span>`; break;
    case 'cancelled': status = `<span class="fres muted">${esc(t('st.cancelled'))}</span>`; break;
    default: status = ok ? (app.job ? `<span class="fres muted">${esc(t('st.waiting'))}</span>` : '') : `<span class="fres warn" title="${esc(t('st.incompatibleD'))}">${esc(t('st.incompatible'))}</span>`;
  }
  const el = h(`<div class="frow s-${f.status}${ok ? '' : ' off'}" role="listitem" data-id="${f.id}"${reorder && !app.job ? ' draggable="true"' : ''}>
    ${reorder && !app.job ? `<span class="fgrip" aria-hidden="true">${icon('grip')}</span>` : ''}
    <span class="fth">${thumb}</span>
    <span class="fname"><b title="${esc(f.path)}">${esc(baseName(f.path))}</b><small>${sub ? `<span class="fdir">${esc(sub)}</span> · ` : ''}${esc(metaText(f))}</small></span>
    <span class="fstat">${status}</span>
    ${app.job ? '' : `<button type="button" class="icon-btn fx" data-a="remove" title="${esc(t('files.remove'))}" aria-label="${esc(t('files.remove'))}">${icon('x')}</button>`}
  </div>`);
  el.querySelector('[data-a="remove"]')?.addEventListener('click', () => removeFile(f.id));
  el.querySelector('[data-a="reveal"]')?.addEventListener('click', () => call('shell.reveal', { path: f.out, select: f.count <= 1 }));
  if (!f.metaLoading) thumbObserver ? thumbObserver.observe(el) : loadMeta(f);
  if (el.draggable) wireDrag(el, f);
  return el;
}

// Sıralama (birleştirme işlerinde sıra önemli)
let dragId = 0;
function wireDrag(el, f) {
  el.addEventListener('dragstart', (e) => { dragId = f.id; el.classList.add('dragging'); e.dataTransfer.effectAllowed = 'move'; e.dataTransfer.setData('text/x-formaty', String(f.id)); });
  el.addEventListener('dragend', () => { dragId = 0; el.classList.remove('dragging'); document.querySelectorAll('.drop-before,.drop-after').forEach((x) => x.classList.remove('drop-before', 'drop-after')); });
  el.addEventListener('dragover', (e) => {
    if (!dragId || dragId === f.id) return;
    e.preventDefault();
    const r = el.getBoundingClientRect(); const after = e.clientY > r.top + r.height / 2;
    el.classList.toggle('drop-after', after); el.classList.toggle('drop-before', !after);
  });
  el.addEventListener('dragleave', () => el.classList.remove('drop-before', 'drop-after'));
  el.addEventListener('drop', (e) => {
    if (!dragId) return;
    e.preventDefault(); e.stopPropagation();
    const after = el.classList.contains('drop-after');
    const from = app.files.findIndex((x) => x.id === dragId);
    const [m] = app.files.splice(from, 1);
    let to = app.files.findIndex((x) => x.id === f.id); if (after) to++;
    app.files.splice(to, 0, m);
    renderList();
  });
}

// ---------- Sağ panel: hedef + ayarlar + çıktı ----------
function renderPanel() {
  const box = $('#work .panel-scroll');
  if (!box) return;
  const scroll = box.scrollTop;
  const gs = groupsFor(kinds());
  const tg = target(app.targetId);
  box.innerHTML = `
    <section class="psec">
      <h3>${icon('sparkles')}<span class="h3t">${esc(t('panel.target'))}</span><button type="button" class="link-btn" data-a="presets">${icon('bookmark')}<span>${esc(t('pre.title'))}</span>${icon('chevD')}</button></h3>
      ${gs.map((g) => `<div class="tgroup"><div class="tgroup-h">${esc(t('group.' + g))}</div><div class="chips">${TARGETS.filter((x) => x.group === g).map((x) => {
        const n = compatibleCount(x);
        return `<button type="button" class="chip${x.id === app.targetId ? ' on' : ''}${x.icon ? ' wide' : ''}" data-t="${x.id}" title="${esc(t(x.sub))}" aria-pressed="${x.id === app.targetId}"${n ? '' : ' disabled'}>
          ${x.icon ? icon(x.icon) : ''}<b>${esc(t(x.label))}</b></button>`;
      }).join('')}</div></div>`).join('')}
      ${tg ? `<p class="tdesc">${icon('info')}<span>${esc(t(tg.sub))}</span></p>` : ''}
    </section>
    ${tg ? `<section class="psec opts">${optionsHtml(tg)}</section>` : ''}
    <section class="psec">
      <h3>${icon('folder')}${esc(t('out.title'))}</h3>
      ${outputHtml(tg)}
    </section>`;
  box.scrollTop = scroll;
  box.querySelector('[data-a="presets"]')?.addEventListener('click', (e) => presetMenu(e.currentTarget));
  for (const b of box.querySelectorAll('.chip[data-t]')) b.onclick = () => { applyTarget(b.dataset.t); resetResults(); renderList(); renderPanel(); renderFoot(); renderResult(); };
  wireOptions(box, tg);
  if (app.job) for (const x of box.querySelectorAll('button, input, select')) x.disabled = true;
}

// --- Ayar bağlama: data-k="resize.mode" → S().opts[group].resize.mode ---
function getPath(o, path) { return path.split('.').reduce((a, k) => (a == null ? a : a[k]), o); }
function setPath(o, path, v) { const ks = path.split('.'); const last = ks.pop(); const p = ks.reduce((a, k) => (a[k] ??= {}), o); p[last] = v; }

function ctl(group) {
  const o = () => S().opts[group];
  const v = (k) => getPath(o(), k);
  return {
    seg: (k, opts) => `<div class="seg" data-k="${k}" role="radiogroup">${opts.map(([val, l]) => `<button type="button" role="radio" data-v="${esc(val)}" aria-checked="${String(v(k)) === String(val)}">${esc(l)}</button>`).join('')}</div>`,
    select: (k, opts) => `<select class="field" data-k="${k}">${opts.map(([val, l]) => `<option value="${esc(val)}"${String(v(k)) === String(val) ? ' selected' : ''}>${esc(l)}</option>`).join('')}</select>`,
    range: (k, min, max, step = 1, unit = '') => `<div class="range"><input type="range" data-k="${k}" min="${min}" max="${max}" step="${step}" value="${v(k)}"><output>${v(k)}${unit}</output></div>`,
    num: (k, ph = '', unit = '', min = 0, max = 100000) => `<div class="num"><input class="field" type="number" data-k="${k}" min="${min}" max="${max}" value="${v(k) || ''}" placeholder="${esc(ph)}">${unit ? `<span>${esc(unit)}</span>` : ''}</div>`,
    text: (k, ph = '', type = 'text') => `<input class="field" type="${type}" data-k="${k}" value="${esc(v(k) ?? '')}" placeholder="${esc(ph)}" spellcheck="false">`,
    time: (k, ph) => `<input class="field" type="text" data-k="${k}" data-time="1" value="${v(k) ? esc(dur(v(k)) || v(k)) : ''}" placeholder="${esc(ph)}" spellcheck="false">`,
    color: (k) => `<input class="color" type="color" data-k="${k}" value="${esc(v(k))}">`,
    toggle: (k, label, hint = '') => `<label class="trow"><span><b>${esc(label)}</b>${hint ? `<small>${esc(hint)}</small>` : ''}</span><button type="button" class="toggle" role="switch" data-k="${k}" aria-checked="${!!v(k)}"><i></i></button></label>`,
  };
}
const row = (label, ctlHtml, hint = '') => `<div class="orow"><label class="olabel">${esc(label)}${hint ? `<small>${esc(hint)}</small>` : ''}</label>${ctlHtml}</div>`;
const qualityLevels = () => [['small', t('q.small')], ['balanced', t('q.balanced')], ['high', t('q.high')], ['max', t('q.max')]];

function optionsHtml(tg) {
  const g = tg.group, c = ctl(g), o = S().opts[g];
  const h3 = `<h3>${icon('sliders')}${esc(t('panel.options'))}</h3>`;
  if (g === 'image') {
    if (tg.id === 'img-pdf') return h3
      + c.toggle('combinePdf', t('o.combinePdf'), t('o.combinePdfD'))
      + row(t('o.page'), c.seg('page', [['a4', 'A4'], ['letter', 'Letter'], ['fit', t('o.pageFit')]]))
      + row(t('o.margin'), c.range('margin', 0, 40, 1, ' mm'))
      + row(t('o.quality'), c.range('quality', 40, 100, 1));
    const lossy = ['jpg', 'webp', 'avif', 'jxl', 'jp2'].includes(tg.id);
    const losslessable = ['webp', 'avif', 'jxl'].includes(tg.id);
    const r = o.resize;
    let s = h3;
    if (lossy && !(losslessable && o.lossless)) s += row(t('o.quality'), c.range('quality', 1, 100, 1), t('o.qualityD'));
    if (losslessable) s += c.toggle('lossless', t('o.lossless'), t('o.losslessD'));
    if (['jpg', 'webp', 'avif'].includes(tg.id) && !o.lossless) s += row(t('o.targetKb'), c.num('targetKb', t('o.off'), 'KB', 0, 100000), t('o.targetKbD'));
    if (tg.id === 'png') s += c.toggle('palette', t('o.palette'), t('o.paletteD'));
    s += row(t('o.resize'), c.select('resize.mode', [['none', t('o.rs.none')], ['long', t('o.rs.long')], ['fit', t('o.rs.fit')], ['exact', t('o.rs.exact')], ['percent', t('o.rs.percent')]]));
    if (r.mode === 'long') s += row(t('o.rs.longPx'), c.num('resize.long', '1920', 'px', 1, 30000));
    if (r.mode === 'fit' || r.mode === 'exact') s += `<div class="orow two">${c.num('resize.width', t('o.width'), 'px', 1, 30000)}<span class="x">×</span>${c.num('resize.height', t('o.height'), 'px', 1, 30000)}</div>`;
    if (r.mode === 'percent') s += row(t('o.rs.percentL'), c.range('resize.percent', 5, 200, 5, '%'));
    if (r.mode !== 'none' && r.mode !== 'percent' && r.mode !== 'exact') s += c.toggle('resize.upscale', t('o.upscale'), t('o.upscaleD'));
    s += `<details class="adv"><summary>${esc(t('o.more'))}</summary>`;
    s += row(t('o.rotate'), c.seg('rotate', [['', t('o.none')], ['90', '90°'], ['180', '180°'], ['270', '270°'], ['hflip', '⇋']]));
    s += c.toggle('keepMeta', t('o.keepMeta'), t('o.keepMetaD'));
    if (['jpg', 'bmp'].includes(tg.id)) s += row(t('o.background'), c.color('background'), t('o.backgroundD'));
    if (['gif', 'webp'].includes(tg.id)) s += c.toggle('keepAnimation', t('o.keepAnim'), t('o.keepAnimD'));
    if (tg.id === 'jpg') s += c.toggle('progressive', t('o.progressive'), t('o.progressiveD'));
    s += row(t('o.watermark'), c.text('watermark.text', t('o.watermarkPh')));
    if (o.watermark?.text) s += row(t('o.wmPos'), c.seg('watermark.position', [['tl', '↖'], ['tr', '↗'], ['c', '•'], ['bl', '↙'], ['br', '↘']]));
    s += '</details>';
    return s;
  }
  if (g === 'video') {
    if (tg.id === 'frames') return h3
      + row(t('o.every'), c.num('every', '1', t('o.sec'), 0.04, 3600))
      + row(t('o.imgFormat'), c.seg('imageFormat', [['jpg', 'JPG'], ['png', 'PNG'], ['webp', 'WebP']]));
    if (tg.id === 'vid-gif' || tg.id === 'vid-webp') return h3
      + row(t('o.width'), c.select('width', [[320, '320 px'], [480, '480 px'], [640, '640 px'], [800, '800 px'], [1080, '1080 px']]))
      + row(t('o.fps'), c.seg('fps', [[0, t('o.auto')], [10, '10'], [15, '15'], [24, '24']]))
      + row(t('o.quality'), c.seg('quality', qualityLevels()))
      + trimRows(c) + c.toggle('loop', t('o.loop'));
    const codecs = tg.id === 'webm' ? [['auto', 'VP9'], ['av1', 'AV1']] : tg.id === 'avi' ? [['auto', 'MPEG-4']] : [['auto', t('o.auto') + ' (H.264)'], ['h264', 'H.264'], ['hevc', 'HEVC (H.265)'], ['av1', 'AV1']];
    let s = h3 + row(t('o.quality'), c.seg('quality', qualityLevels()), t('o.vqualityD'));
    s += row(t('o.targetMb'), `<div class="presets">${[[0, t('o.off')], [8, '8 MB'], [16, '16 MB'], [25, '25 MB'], [50, '50 MB']].map(([v, l]) => `<button type="button" class="pchip${+o.targetMb === v ? ' on' : ''}" data-set="targetMb" data-v="${v}">${esc(l)}</button>`).join('')}</div>${c.num('targetMb', t('o.custom'), 'MB', 0, 100000)}`, t('o.targetMbD'));
    s += row(t('o.resolution'), c.select('height', [[0, t('o.original')], [2160, '4K (2160p)'], [1440, '1440p'], [1080, '1080p (Full HD)'], [720, '720p (HD)'], [480, '480p'], [360, '360p']]));
    s += `<details class="adv"><summary>${esc(t('o.more'))}</summary>`;
    if (codecs.length > 1) s += row(t('o.codec'), c.select('vcodec', codecs), t('o.codecD'));
    s += row(t('o.fps'), c.select('fps', [[0, t('o.original')], [60, '60'], [30, '30'], [25, '25'], [24, '24']]));
    s += row(t('o.audioTrack'), c.seg('audio', [['keep', t('o.keep')], ['remove', t('o.remove')]]));
    s += row(t('o.speed'), c.select('speed', [[0.5, '0.5×'], [0.75, '0.75×'], [1, '1×'], [1.25, '1.25×'], [1.5, '1.5×'], [2, '2×']]));
    s += trimRows(c);
    s += row(t('o.rotate'), c.seg('rotate', [['', t('o.none')], ['90', '90°'], ['180', '180°'], ['270', '270°']]));
    s += c.toggle('hw', t('o.hw'), t('o.hwD'));
    s += c.toggle('combine', t('o.combineVideo'), t('o.combineVideoD'));
    s += '</details>';
    return s;
  }
  if (g === 'audio') {
    const lossless = ['flac', 'wav', 'aiff', 'alac'].includes(tg.id);
    let s = h3;
    if (!lossless) {
      if (!(tg.id === 'mp3' && o.mode === 'vbr')) s += row(t('o.quality'), c.seg('quality', qualityLevels()), t('o.aqualityD.' + tg.id));
      if (tg.id === 'mp3') {
        s += row(t('o.mode'), c.seg('mode', [['cbr', t('o.cbr')], ['vbr', t('o.vbr')]]), t('o.modeD'));
        if (o.mode === 'vbr') s += row(t('o.vbrLevel'), c.select('vbr', [0, 1, 2, 3, 4, 5, 6, 7, 8, 9].map((x) => [x, `V${x}${x === 0 ? ' — ' + t('o.vbrBest') : x === 2 ? ' — ' + t('o.vbrGood') : ''}`])));
        else s += row(t('o.bitrate'), c.select('kbps', [[0, t('o.byQuality')], ...[64, 96, 128, 160, 192, 224, 256, 320].map((x) => [x, x + ' kbps'])]));
      } else s += row(t('o.bitrate'), c.select('kbps', [[0, t('o.byQuality')], ...[48, 64, 96, 128, 160, 192, 256, 320].map((x) => [x, x + ' kbps'])]));
    }
    if (tg.id === 'wav' || tg.id === 'aiff') s += row(t('o.bits'), c.seg('bits', [[16, '16 bit'], [24, '24 bit'], [32, '32 bit float']]), t('o.bitsD'));
    if (tg.id === 'flac') { s += row(t('o.flacLevel'), c.range('compression', 0, 8, 1), t('o.flacLevelD')); s += row(t('o.bits'), c.seg('bits', [[0, t('o.original')], [16, '16 bit'], [24, '24 bit']])); }
    if (lossless && app.files.some((f) => f.kind === 'audio' && ['mp3', 'aac', 'm4a', 'ogg', 'opus', 'wma'].includes(f.ext))) s += `<p class="note">${icon('info')}<span>${esc(t('o.losslessNote'))}</span></p>`;
    s += row(t('o.sampleRate'), c.select('sampleRate', [[0, t('o.original')], [22050, '22.05 kHz'], [32000, '32 kHz'], [44100, '44.1 kHz (CD)'], [48000, '48 kHz'], [96000, '96 kHz']]));
    s += row(t('o.channels'), c.seg('channels', [['keep', t('o.keep')], ['mono', 'Mono'], ['stereo', 'Stereo']]));
    s += `<details class="adv"><summary>${esc(t('o.more'))}</summary>`;
    s += row(t('o.normalize'), c.select('normalize', [[0, t('o.off')], [-14, '−14 LUFS (Spotify, YouTube)'], [-16, '−16 LUFS (Podcast)'], [-23, '−23 LUFS (TV)']]), t('o.normalizeD'));
    s += row(t('o.volume'), c.range('volumeDb', -12, 12, 1, ' dB'));
    if (['mp3', 'aac', 'alac', 'flac'].includes(tg.id)) s += c.toggle('keepCover', t('o.keepCover'), t('o.keepCoverD'));
    s += c.toggle('trimSilence', t('o.trimSilence'), t('o.trimSilenceD'));
    s += trimRows(c);
    s += row(t('o.speed'), c.select('speed', [[0.5, '0.5×'], [0.75, '0.75×'], [1, '1×'], [1.25, '1.25×'], [1.5, '1.5×'], [2, '2×']]));
    s += c.toggle('combine', t('o.combineAudio'), t('o.combineAudioD'));
    s += '</details>';
    return s;
  }
  // PDF
  switch (tg.id) {
    case 'pdf-img': return h3
      + row(t('o.imgFormat'), c.seg('imageFormat', [['jpg', 'JPG'], ['png', 'PNG'], ['webp', 'WebP']]))
      + row(t('o.dpi'), c.select('dpi', [[72, '72 dpi — ' + t('o.dpiScreen')], [150, '150 dpi — ' + t('o.dpiNormal')], [200, '200 dpi'], [300, '300 dpi — ' + t('o.dpiPrint')], [600, '600 dpi']]))
      + row(t('o.pages'), c.text('pages', t('o.pagesPh')), t('o.pagesD'))
      + (o.imageFormat !== 'png' ? row(t('o.quality'), c.range('quality', 40, 100, 1)) : '')
      + row(t('o.pdfPassword'), c.text('password', t('o.ifLocked'), 'password'));
    case 'pdf-merge': return h3 + `<p class="note">${icon('grip')}<span>${esc(t('o.mergeOrder'))}</span></p>` + row(t('o.fileName'), c.text('name', t('o.mergeNamePh')));
    case 'pdf-split': return h3
      + row(t('o.splitMode'), c.seg('mode', [['each', t('o.split.each')], ['every', t('o.split.every')], ['ranges', t('o.split.ranges')]]))
      + (o.mode === 'every' ? row(t('o.split.everyN'), c.num('every', '2', t('o.pagesUnit'), 1, 10000)) : '')
      + (o.mode === 'ranges' ? row(t('o.split.rangesL'), c.text('ranges', '1-3, 4-6, 7-'), t('o.split.rangesD')) : '')
      + row(t('o.pdfPassword'), c.text('password', t('o.ifLocked'), 'password'));
    case 'pdf-compress': return h3 + row(t('o.level'), c.seg('level', [['light', t('o.lv.light')], ['balanced', t('o.lv.balanced')], ['strong', t('o.lv.strong')]]), t('o.compressD'))
      + row(t('o.pdfPassword'), c.text('password', t('o.ifLocked'), 'password'));
    case 'pdf-protect': return h3 + row(t('o.newPassword'), c.text('userPassword', '', 'password'), t('o.newPasswordD')) + row(t('o.pdfPassword'), c.text('password', t('o.ifLocked'), 'password'));
    case 'pdf-unlock': return h3 + row(t('o.currentPassword'), c.text('password', '', 'password'), t('o.currentPasswordD'));
  }
  return '';
}

function trimRows(c) {
  return `<div class="orow two"><div><label class="olabel">${esc(t('o.start'))}</label>${c.time('start', '0:00')}</div><div><label class="olabel">${esc(t('o.end'))}</label>${c.time('end', t('o.toEnd'))}</div></div>`;
}

function outputHtml(tg) {
  const o = S().output;
  const seg = `<div class="seg out-mode" role="radiogroup">${[['same', t('out.same')], ['sub', t('out.sub')], ['custom', t('out.custom')]].map(([v, l]) => `<button type="button" role="radio" data-v="${v}" aria-checked="${o.mode === v}">${esc(l)}</button>`).join('')}</div>`;
  let s = seg;
  if (o.mode === 'sub') s += `<div class="orow"><label class="olabel">${esc(t('out.subName'))}</label><input class="field" data-o="sub" value="${esc(o.sub)}" spellcheck="false"></div>`;
  if (o.mode === 'custom') s += `<div class="out-dir"><button type="button" class="btn btn-sm" data-a="pickOut">${icon('folderOpen')}${esc(t('out.choose'))}</button><span title="${esc(o.dir)}">${esc(o.dir || t('out.none'))}</span></div>`
    + `<label class="trow"><span><b>${esc(t('out.keepStructure'))}</b><small>${esc(t('out.keepStructureD'))}</small></span><button type="button" class="toggle" role="switch" data-o="keepStructure" aria-checked="${o.keepStructure}"><i></i></button></label>`;
  const combine = tg && (tg.id === 'pdf-merge' || (tg.id === 'img-pdf' && S().opts.image.combinePdf) || ((tg.group === 'video' || tg.group === 'audio') && S().opts[tg.group].combine));
  s += `<details class="adv"><summary>${esc(t('o.more'))}</summary>`;
  if (!combine) s += `<div class="orow"><label class="olabel">${esc(t('out.template'))}<small>${esc(t('out.templateD'))}</small></label><input class="field" data-o="template" value="${esc(o.template)}" spellcheck="false"></div>`;
  s += `<div class="orow"><label class="olabel">${esc(t('out.conflict'))}</label><select class="field" data-o="conflict">${[['rename', t('out.c.rename')], ['overwrite', t('out.c.overwrite')], ['skip', t('out.c.skip')]].map(([v, l]) => `<option value="${v}"${o.conflict === v ? ' selected' : ''}>${esc(l)}</option>`).join('')}</select></div>`;
  s += `<label class="trow"><span><b>${esc(t('out.keepDates'))}</b><small>${esc(t('out.keepDatesD'))}</small></span><button type="button" class="toggle" role="switch" data-o="keepDates" aria-checked="${o.keepDates}"><i></i></button></label>`;
  s += `<label class="trow"><span><b>${esc(t('out.recycle'))}</b><small>${esc(t('out.recycleD'))}</small></span><button type="button" class="toggle" role="switch" data-o="recycle" aria-checked="${o.recycle}"><i></i></button></label>`;
  s += '</details>';
  return s;
}

function wireOptions(box, tg) {
  const g = tg?.group;
  const opts = () => S().opts[g];
  const changed = (rerender) => { saveSettings(); resetResults(); if (rerender) { renderPanel(); renderList(); } renderFoot(); renderResult(); };
  // Ayarlar
  for (const el of box.querySelectorAll('.opts [data-k]')) {
    const k = el.dataset.k;
    if (el.classList.contains('seg')) {
      for (const b of el.querySelectorAll('button')) b.onclick = () => {
        const cur = getPath(opts(), k); const v = typeof cur === 'number' ? +b.dataset.v : b.dataset.v;
        setPath(opts(), k, v); changed(true);
      };
    } else if (el.classList.contains('toggle')) {
      el.onclick = () => { setPath(opts(), k, !getPath(opts(), k)); changed(true); };
    } else if (el.type === 'range') {
      const out = el.parentElement.querySelector('output'); const unit = out.textContent.replace(/^[-\d.]+/, '');
      el.oninput = () => { out.textContent = el.value + unit; setPath(opts(), k, +el.value); saveSettings(); renderFoot(); };
      el.onchange = () => changed(false);
    } else if (el.tagName === 'SELECT') {
      el.onchange = () => { const cur = getPath(opts(), k); setPath(opts(), k, typeof cur === 'number' ? +el.value : el.value); changed(true); };
    } else {
      el.onchange = () => {
        let v = el.value;
        if (el.dataset.time) v = parseTime(v);
        else if (el.type === 'number') v = Math.max(0, +v || 0);
        setPath(opts(), k, v);
        changed(k.startsWith('watermark') || k === 'targetMb' || k === 'every');
      };
      if (el.type === 'color') el.oninput = el.onchange;
    }
  }
  for (const b of box.querySelectorAll('.pchip[data-set]')) b.onclick = () => { setPath(opts(), b.dataset.set, +b.dataset.v); changed(true); };
  // Çıktı
  const o = S().output;
  for (const b of box.querySelectorAll('.out-mode button')) b.onclick = async () => {
    o.mode = b.dataset.v;
    if (o.mode === 'custom' && !o.dir) { const d = await call('dialog.folder', { title: t('out.choose') }); if (d) o.dir = d; else o.mode = 'same'; }
    saveSettings(); renderPanel();
  };
  box.querySelector('[data-a="pickOut"]')?.addEventListener('click', async () => { const d = await call('dialog.folder', { title: t('out.choose'), dir: o.dir }); if (d) { o.dir = d; saveSettings(); renderPanel(); } });
  for (const el of box.querySelectorAll('[data-o]')) {
    const k = el.dataset.o;
    if (el.classList.contains('toggle')) el.onclick = () => { o[k] = !o[k]; saveSettings(); renderPanel(); };
    else el.onchange = () => { o[k] = el.value.trim() || DEFAULTS.output[k]; saveSettings(); };
  }
  // Açılır "Daha fazla" bölümlerinin durumu yeniden çizimde korunsun
  for (const d of box.querySelectorAll('details.adv')) {
    const key = (tg?.id || '') + ':' + [...box.querySelectorAll('details.adv')].indexOf(d);
    if (openAdv.has(key)) d.open = true;
    d.ontoggle = () => { if (d.open) openAdv.add(key); else openAdv.delete(key); };
  }
}
const openAdv = new Set();

// ---------- Hazır ayarlar ----------
function applyPreset(p) {
  const tg = target(p.target); if (!tg) return;
  // Yerleşik: grup varsayılanı + değişiklikler; kullanıcınınki: kaydedilen ayarların tamamı
  S().opts[tg.group] = p.opts ? clone(p.opts) : merge(clone(DEFAULT_OPTS[tg.group]), clone(p.patch || {}));
  applyTarget(p.target);
  resetResults(); render();
  toast(t('pre.applied', { name: p.opts ? p.name : t(p.name) }));
}

function presetMenu(anchor) {
  const tg = target(app.targetId);
  const usable = (p) => { const x = target(p.target); return x && app.files.some((f) => x.accepts.includes(f.kind)); };
  const r = anchor.getBoundingClientRect();
  const builtins = BUILTIN_PRESETS.filter(usable);
  const mine = S().presets.filter(usable);
  const items = [];
  if (builtins.length) items.push({ header: t('pre.builtin') }, ...builtins.map((p) => ({ label: t(p.name), run: () => applyPreset(p) })));
  if (mine.length) items.push('-', { header: t('pre.mine') }, ...mine.map((p) => ({ label: p.name, icon: 'bookmark', run: () => applyPreset(p) })));
  if (items.length) items.push('-');
  items.push({ label: t('pre.save'), icon: 'save', disabled: !tg || !!app.job, run: () => savePreset() });
  if (S().presets.length) items.push({ label: t('pre.delete'), icon: 'trash', submenu: S().presets.map((p) => ({ label: p.name, run: () => { S().presets = S().presets.filter((x) => x !== p); saveSettings(); toast(t('pre.deleted', { name: p.name })); } })) });
  showMenu(items, r.right, r.bottom + 4, { align: 'right', minWidth: 240 });
}

async function savePreset() {
  const tg = target(app.targetId); if (!tg) return;
  const name = (await prompt(t('pre.saveTitle'), { label: t('pre.nameL'), placeholder: t('pre.namePh', { fmt: t(tg.label) }) }))?.trim();
  if (!name) return;
  const existing = S().presets.findIndex((p) => p.name.toLocaleLowerCase() === name.toLocaleLowerCase());
  const p = { name, target: tg.id, opts: clone(S().opts[tg.group]) };
  if (existing >= 0) S().presets[existing] = p; else S().presets.push(p);
  saveSettings();
  toast(t('pre.saved', { name }));
}

// ---------- Tahmini çıktı boyutu ----------
const MB = 1024 * 1024;
const LV = { small: 0, balanced: 1, high: 2, max: 3 };
/** Hedef ve ayarlara göre yaklaşık toplam çıktı (bayt); hesaplanamıyorsa null. */
function estimate(tg, o, files) {
  if (!tg || !files.length) return null;
  let total = 0;
  for (const f of files) {
    const i = f.info;
    if (!i) return null;
    if (tg.group === 'image') {
      if (o.targetKb > 0 && ['jpg', 'webp', 'avif'].includes(tg.id)) { total += o.targetKb * 1024; continue; }
      return null;
    }
    const end = o.end > 0 && o.end < (i.duration || 0) ? o.end : i.duration || 0;
    const d0 = Math.max(0, end - (o.start || 0)) / (o.speed || 1);
    if (!(d0 > 0)) return null;
    if (tg.group === 'video') {
      if (['vid-gif', 'vid-webp', 'frames'].includes(tg.id)) return null;
      if (o.targetMb > 0) { total += Math.min(o.targetMb * MB, (f.size || Infinity) * Math.min(1, d0 * (o.speed || 1) / (i.duration || d0))); continue; } // hedef üst sınırdır
      const short = Math.min(i.width || 1280, i.height || 720), long = Math.max(i.width || 1280, i.height || 720);
      const outShort = o.height > 0 && o.height < short ? o.height : short;
      const px = outShort * Math.round(long * outShort / short);
      const fps = o.fps > 0 && (!i.fps || o.fps < i.fps) ? o.fps : i.fps || 30;
      const codec = o.vcodec === 'auto' ? (tg.id === 'webm' ? 'vp9' : tg.id === 'avi' ? 'mpeg4' : 'h264') : o.vcodec;
      const factor = { h264: 1, hevc: 0.65, av1: 0.55, vp9: 0.7, mpeg4: 1.6 }[codec] || 1;
      const bpp = [0.05, 0.08, 0.12, 0.2][LV[o.quality] ?? 1];
      let kbps = (px * fps * bpp * factor) / 1000;
      if (o.audio !== 'remove' && i.hasAudio) kbps += [96, 128, 192, 256][LV[o.quality] ?? 1];
      total += (kbps * 1000 / 8) * d0;
      continue;
    }
    if (tg.group === 'audio') {
      if (!i.hasAudio) return null;
      const ch = o.channels === 'mono' ? 1 : o.channels === 'stereo' ? 2 : i.channels || 2;
      const sr = o.sampleRate || i.sampleRate || 44100;
      if (['wav', 'aiff', 'flac', 'alac'].includes(tg.id)) {
        const bits = o.bits || 16;
        const raw = sr * ch * bits / 8 * d0;
        total += tg.id === 'flac' || tg.id === 'alac' ? raw * 0.6 : raw;
        continue;
      }
      const lv = LV[o.quality] ?? 2;
      let kbps = o.kbps > 0 ? o.kbps : ({ mp3: [96, 160, 256, 320], aac: [96, 160, 224, 320], opus: [64, 112, 160, 256], ogg: [112, 160, 224, 320] }[tg.id] || [96, 160, 224, 320])[lv];
      if (tg.id === 'mp3' && o.mode === 'vbr') kbps = [245, 225, 190, 175, 165, 130, 115, 100, 85, 65][o.vbr ?? 2];
      total += kbps * 1000 / 8 * d0;
      continue;
    }
    return null;
  }
  return total;
}

/** Tahmin için gereken dosya bilgilerini (henüz yüklenmemişse) arka planda ister. */
function ensureMeta(files) {
  for (const f of files.slice(0, 400)) if (!f.info && !f.metaLoading) loadMeta(f).then(() => throttleFoot());
}

function etaText(j) {
  const p = jobProgress();
  const el = (Date.now() - j.started - (j.pausedMs || 0)) / 1000;
  if (j.paused || p < 0.02 || el < 2) return '';
  const left = el * (1 - p) / p;
  j.eta = j.eta ? j.eta * 0.7 + left * 0.3 : left; // sıçramasın
  const sec = Math.round(j.eta);
  return sec < 60 ? t('eta.sec', { n: Math.max(1, sec) }) : sec < 3600 ? t('eta.min', { n: Math.round(sec / 60) }) : t('eta.hour', { h: Math.floor(sec / 3600), m: Math.round((sec % 3600) / 60) });
}

// ---------- Alt çubuk: başlat / duraklat / iptal ----------
function renderFoot() {
  const foot = $('#work .panel-foot');
  if (!foot) return;
  const tg = target(app.targetId);
  const n = tg ? compatibleCount(tg) : 0;
  const skip = app.files.length - n;
  const j = app.job;
  if (j) {
    const pct = Math.round(jobProgress() * 100);
    foot.innerHTML = `<div class="jprog"><div class="jbar"><i style="width:${pct}%"></i></div><div class="jtxt"><b>${pct}%</b><span>${esc(t(j.paused ? 'job.paused' : 'job.running', { done: j.finished, total: j.total }))}</span><em class="eta">${esc(etaText(j))}</em></div></div>
      <div class="foot-btns"><button type="button" class="btn" data-a="pause">${icon(j.paused ? 'play' : 'pause')}${esc(t(j.paused ? 'job.resume' : 'job.pause'))}</button>
      <button type="button" class="btn btn-danger" data-a="cancel">${icon('x')}${esc(t('job.cancel'))}</button></div>`;
    foot.querySelector('[data-a="pause"]').onclick = () => {
      j.paused = !j.paused;
      if (j.paused) j.pausedAt = Date.now(); else { j.pausedMs = (j.pausedMs || 0) + (Date.now() - (j.pausedAt || Date.now())); j.eta = 0; }
      call('job.pause', { id: j.id, paused: j.paused }); renderFoot();
    };
    foot.querySelector('[data-a="cancel"]').onclick = () => call('job.cancel', { id: j.id });
    return;
  }
  const combine = tg && (tg.id === 'pdf-merge' || (tg.id === 'img-pdf' && S().opts.image.combinePdf) || ((tg.group === 'video' || tg.group === 'audio') && S().opts[tg.group]?.combine && tg.id !== 'frames' && tg.id !== 'vid-gif' && tg.id !== 'vid-webp'));
  const extLabel = tg ? (tg.group === 'pdf' || tg.id === 'frames' ? outExt(tg, S().opts[tg.group]).toUpperCase() : t(tg.label)) : '';
  const label = !tg ? t('job.pickTarget') : combine ? t('job.startCombine', { n, ext: extLabel }) : t('job.start', { n, ext: extLabel });
  const files = tg ? app.files.filter((f) => accepts(tg, f)) : [];
  ensureMeta(files);
  const est = tg ? estimate(tg, S().opts[tg.group], files) : null;
  const inSize = files.reduce((a, f) => a + (f.size || 0), 0);
  const estHtml = est != null ? `<p class="foot-est">${icon('info')}<span>${esc(t(tg.group === 'image' ? 'est.max' : 'est.size', { out: size(est), in: size(inSize) }))}</span></p>` : '';
  foot.innerHTML = `${estHtml}${skip > 0 && tg ? `<p class="foot-note">${icon('info')}${esc(t('job.skipNote', { n: skip }))}</p>` : ''}
    <button type="button" class="btn btn-go" data-a="start"${n && !(combine && n < 2 && tg.id === 'pdf-merge') ? '' : ' disabled'}>${icon('arrowR')}<span>${esc(label)}</span><kbd>Ctrl+Enter</kbd></button>`;
  foot.querySelector('[data-a="start"]').onclick = () => startJob();
}

function jobProgress() {
  const j = app.job; if (!j) return 0;
  if (j.combine) return j.combineProgress || 0;
  const items = j.map.map((id) => app.files.find((f) => f.id === id)).filter(Boolean);
  if (!items.length) return 0;
  return items.reduce((a, f) => a + (['done', 'error', 'skipped', 'cancelled'].includes(f.status) ? 1 : f.status === 'working' ? f.progress : 0), 0) / items.length;
}

// ---------- Sonuç özeti ----------
function renderResult() {
  const box = $('#work .result');
  if (!box) return;
  const r = app.result;
  if (!r) { box.hidden = true; return; }
  box.hidden = false;
  const saved = r.inBytes > 0 && r.outBytes > 0 ? Math.round((1 - r.outBytes / r.inBytes) * 100) : null;
  const ok = r.failed === 0 && !r.cancelled;
  box.className = `result ${ok ? 'ok' : r.done ? 'mixed' : 'bad'}`;
  box.innerHTML = `<span class="res-ic">${icon(ok ? 'checkCircle' : 'alert')}</span>
    <div class="res-t"><b>${esc(r.cancelled ? t('res.cancelled', { n: r.done }) : r.failed ? t('res.partial', { n: r.done, f: r.failed }) : r.combine ? t('res.combined', { n: r.done }) : t('res.done', { n: r.done }))}</b>
    <small>${r.outBytes ? `${esc(size(r.inBytes))} → ${esc(size(r.outBytes))}${saved != null ? ` (${saved >= 0 ? '−' : '+'}${Math.abs(saved)}%)` : ''} · ` : ''}${esc(t('res.time', { s: nf().format(r.seconds) }))}${r.skipped ? ' · ' + esc(t('res.skipped', { n: r.skipped })) : ''}</small></div>
    <div class="res-btns">${r.outputs?.length ? `<button type="button" class="btn btn-sm" data-a="open">${icon('folderOpen')}${esc(t('res.open'))}</button>` : ''}
    ${r.failed ? `<button type="button" class="btn btn-sm" data-a="retry">${icon('refresh')}${esc(t('res.retry'))}</button>` : ''}</div>`;
  box.querySelector('[data-a="open"]')?.addEventListener('click', () => {
    const first = r.outputs[0];
    call('shell.reveal', { path: first, select: true });
  });
  box.querySelector('[data-a="retry"]')?.addEventListener('click', () => startJob(app.files.filter((f) => f.status === 'error')));
}

// ===================== İş =====================
async function startJob(only) {
  if (app.job) return;
  const tg = target(app.targetId);
  if (!tg) return;
  const o = S().opts[tg.group];
  const req = buildRequest(tg, o);
  if (!req) return;
  const files = (only || app.files).filter((f) => accepts(tg, f));
  if (!files.length) return;
  // Doğrulamalar
  if (tg.id === 'pdf-protect' && !o.userPassword) { toast(t('err.needPassword'), { kind: 'warn' }); return; }
  if (tg.id === 'pdf-unlock' && !o.password) { toast(t('err.needPassword'), { kind: 'warn' }); return; }
  if (S().output.mode === 'custom' && !S().output.dir) { toast(t('err.needDir'), { kind: 'warn' }); return; }
  if (S().output.recycle) {
    const v = await ask(t('ask.recycle', { n: files.length }), [{ label: t('ask.recycleYes'), value: true, primary: true }, { label: t('cancel'), value: null }]);
    if (!v) return;
  }
  if (req.options && tg.group === 'video' && req.op !== 'frames') req.options.hw = !!S().hw && o.hw !== false;
  for (const f of app.files) if (files.includes(f)) { f.status = 'idle'; f.progress = 0; f.error = ''; f.out = ''; f.outSize = 0; f.count = 0; }
  app.result = null;
  const id = await call('job.start', {
    op: req.op, format: req.format, options: req.options || {}, parallel: S().parallel || 0,
    items: files.map((f) => ({ path: f.path, rel: f.rel })),
    output: { ...S().output, template: req.combine ? '{name}' : S().output.template },
  });
  app.job = { id, map: files.map((f) => f.id), total: files.length, finished: 0, paused: false, combine: !!req.combine, combineProgress: 0, started: Date.now() };
  renderWork();
}

function onItem(d) {
  const j = app.job; if (!j || d.id !== j.id) return;
  const f = app.files.find((x) => x.id === j.map[d.index]); if (!f) return;
  const prev = f.status;
  f.status = d.status;
  if (d.progress != null) f.progress = d.progress;
  if (d.status === 'done') { f.outSize = d.outSize; f.out = d.out; f.count = d.count ?? 1; }
  if (d.error) f.error = d.error;
  if (['done', 'error', 'skipped', 'cancelled'].includes(d.status) && !['done', 'error', 'skipped', 'cancelled'].includes(prev)) j.finished++;
  // Satırı yerinde güncelle (bütün listeyi yeniden çizme)
  const row = $(`.frow[data-id="${f.id}"]`);
  if (row) {
    if (d.status === 'working' && prev === 'working') {
      const bar = row.querySelector('.fprog i'), pct = row.querySelector('.fpct');
      if (bar) { bar.style.width = Math.round(f.progress * 100) + '%'; pct.textContent = Math.round(f.progress * 100) + '%'; }
      else row.replaceWith(rowEl(f));
    } else row.replaceWith(rowEl(f));
  }
  throttleFoot();
}

let footTimer = 0;
function throttleFoot() { if (footTimer) return; footTimer = setTimeout(() => { footTimer = 0; renderFoot(); }, 120); }

function onDone(d) {
  const j = app.job; if (!j || d.id !== j.id) return;
  for (const id of j.map) { const f = app.files.find((x) => x.id === id); if (f && (f.status === 'working' || f.status === 'idle')) f.status = d.cancelled ? 'cancelled' : 'idle'; }
  app.job = null;
  app.result = { ...d, combine: j.combine };
  if (j.combine && d.done && d.outputs?.length) {
    for (const id of j.map) { const f = app.files.find((x) => x.id === id); if (f) { f.status = 'done'; f.out = d.outputs[0]; f.outSize = 0; f.count = 1; } }
  }
  renderWork();
  if (S().notify && !d.cancelled && (d.done || d.failed)) {
    call('window.flash', {});
    const saved = d.inBytes > 0 && d.outBytes > 0 ? ` · ${size(d.inBytes)} → ${size(d.outBytes)}` : '';
    call('notify', {
      title: d.failed ? t('notify.partial') : t('notify.done'),
      text: (d.failed ? t('res.partial', { n: d.done, f: d.failed }) : j.combine ? t('res.combined', { n: d.done }) : t('res.done', { n: d.done })) + saved,
      path: d.outputs?.[0] || '',
    });
  }
}

// ===================== Gezgin hızlı işlemleri =====================
let quickTimer = 0, quickPaths = [], quickAction = null;
function quick(paths, action) {
  quickPaths.push(...paths);
  if (action) quickAction = action;
  clearTimeout(quickTimer);
  // Gezgin çoklu seçimde her dosya için ayrı başlatır: kısa süre topla, tek iş yap
  quickTimer = setTimeout(async () => {
    const ps = [...new Set(quickPaths)]; const act = quickAction; quickPaths = []; quickAction = null;
    if (app.job) { await addPaths(ps); return; }
    if (act && QUICK[act]) {
      app.files = []; app.result = null;
      const [tid, patch, outPatch] = QUICK[act];
      applyTarget(tid, patch);
      const saveTpl = S().output.template;
      if (outPatch?.template) S().output.template = outPatch.template;
      await addPaths(ps);
      app.targetId = tid; render();
      await startJob();
      if (outPatch?.template) S().output.template = saveTpl;
    } else await addPaths(ps);
  }, 650);
}

// ===================== Tanıtım =====================
function runTour() {
  if ($('.tour')) return;
  startTour(() => { S().tourDone = true; saveSettings(); });
}

// ===================== Kabuk ve olaylar =====================
function buildChrome() {
  $('.tb-nav').innerHTML = `<button type="button" data-view="home">${icon('home')}<span>${esc(t('nav.home'))}</span></button>
    <button type="button" data-view="work">${icon('sliders')}<span>${esc(t('nav.work'))}</span><span class="count"></span></button>`;
  for (const b of document.querySelectorAll('.tb-nav button')) b.onclick = () => { if (!b.disabled) { showView(b.dataset.view); render(); } };
  $('.tb-settings').onclick = () => settingsDialog(app.info.updateAvailable ? 'about' : 'general');
  $('.tb-settings').title = t('nav.settings');
  $('.tb-settings').setAttribute('aria-label', t('nav.settings'));
  $('[data-win="close"]').onclick = () => requestClose();
  $('[data-win="min"]').onclick = () => call('window.min', {});
  $('[data-win="max"]').onclick = () => call('window.max', {});
  renderNav();
  for (const b of document.querySelectorAll('.tb-nav button')) b.setAttribute('aria-current', String(b.dataset.view === app.view));
}

async function requestClose() {
  if (app.job) {
    const v = await ask(t('ask.closeBusy'), [{ label: t('ask.closeYes'), value: true, danger: true }, { label: t('cancel'), value: null, primary: true }]);
    if (!v) return;
    await call('job.cancel', { id: app.job.id });
  }
  call('app.exit', {});
}

function wireDrop() {
  const overlay = $('.drop-overlay');
  let depth = 0;
  const isFiles = (e) => [...(e.dataTransfer?.types || [])].includes('Files');
  document.addEventListener('dragenter', (e) => { if (!isFiles(e)) return; depth++; overlay.hidden = false; });
  document.addEventListener('dragleave', (e) => { if (!isFiles(e)) return; if (--depth <= 0) { depth = 0; overlay.hidden = true; } });
  document.addEventListener('dragover', (e) => { if (isFiles(e)) { e.preventDefault(); e.dataTransfer.dropEffect = 'copy'; } });
  document.addEventListener('drop', async (e) => {
    if (!isFiles(e)) return;
    e.preventDefault(); depth = 0; overlay.hidden = true;
    if (app.job) { toast(t('err.busy'), { kind: 'warn' }); return; }
    const paths = await call('drop.paths', {}, e.dataTransfer.files);
    await addPaths(paths || []);
  });
}

function wireKeys() {
  document.addEventListener('keydown', (e) => {
    if (document.querySelector('.modal-back, .tour')) return;
    const k = (e.ctrlKey ? 'Ctrl+' : '') + (e.shiftKey ? 'Shift+' : '') + (e.altKey ? 'Alt+' : '') + e.key;
    if (k === 'Ctrl+o') { e.preventDefault(); pickFiles(); }
    else if (k === 'Ctrl+Shift+O' || k === 'Ctrl+Shift+o') { e.preventDefault(); pickFolder(); }
    else if (k === 'Ctrl+Enter' && app.view === 'work') { e.preventDefault(); startJob(); }
    else if (k === 'Ctrl+,') { e.preventDefault(); settingsDialog(); }
    else if (k === 'Escape' && app.job) { /* iptal yalnızca düğmeyle */ }
    else if (k === 'Ctrl+v' && !/INPUT|TEXTAREA/.test(document.activeElement?.tagName)) { /* panodan dosya: WebView2 vermez */ }
  });
}

async function boot() {
  app.info = (await call('app.info', {})) || {};
  const saved = await call('settings.load', {});
  app.settings = merge(clone(DEFAULTS), saved || {});
  setLang(S().lang || app.info.lang || 'tr');
  applyTheme();
  applyI18n();
  buildChrome();
  wireDrop();
  wireKeys();
  on('open-files', (d) => quick(d.paths || [], d.action));
  addEventListener('formaty-tour', () => { if (app.files.length) { showView('work'); render(); } setTimeout(runTour, 200); });
  addEventListener('formaty-reset', () => { S().opts = clone(DEFAULT_OPTS); S().output = clone(DEFAULTS.output); saveSettings(); render(); toast(t('set.resetDone')); });
  on('item', onItem);
  on('combine', (d) => { if (app.job && d.id === app.job.id) { app.job.combineProgress = d.progress; throttleFoot(); } });
  on('job-done', onDone);
  on('job-error', (d) => toast(d.message, { kind: 'error', timeout: 8000 }));
  on('close-request', () => requestClose());
  on('theme-changed', (d) => { app.info.dark = d.dark; if (S().theme === 'system') applyTheme(); });
  on('update-status', (d) => { Object.assign(app.info, d); updateBadges(); if (d.notify && d.updateLabel) toast(d.updateLabel, { actions: [{ label: t('upd.install'), run: () => call('update.install', {}) }], timeout: 15000 }); });
  if (!app.info.engines && hasHost) toast(t('err.engines'), { kind: 'error', timeout: 0 });
  showView('home');
  render();
  document.body.classList.add('ready');
  call('app.ready', {});
  if (app.info.args?.length) quick(app.info.args, app.info.action);
  if (app.info.debug) window.__fmt = { app, call, render, startJob, addPaths, applyTarget, quick, settingsDialog, runTour, showView, updateBadges };
}

boot().catch((e) => { console.error(e); document.body.classList.add('ready'); toast(String(e.message || e), { kind: 'error', timeout: 0 }); });
