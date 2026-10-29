// Ayarlar penceresi (uygulama içi). Değişiklikler hemen uygulanır.
import { app, DEFAULTS } from './state.js';
import { call, on as onHost } from './bridge.js';
import { t, getLang } from './i18n.js';
import { icon } from './icons.js';
import { dialog, field, wireSegs, esc, ask } from './ui.js';

const SECTIONS = ['general', 'editor', 'preview', 'export', 'files', 'about'];

export async function settingsDialog(apply, initial = 'general') {
  let sec = initial;
  await dialog({
    title: t('set.title'), width: 820, className: 'settings', buttons: [],
    body: `<div class="set-wrap"><nav class="set-nav">${SECTIONS.map((s) => `<button type="button" data-s="${s}">${icon({ general: 'settings', editor: 'pen', preview: 'eye', export: 'export', files: 'folder', about: 'help' }[s])}<span>${esc(t('set.' + s))}</span></button>`).join('')}</nav><div class="set-body"></div></div>`,
    onOpen: (d) => {
      const body = d.querySelector('.set-body');
      const nav = d.querySelector('.set-nav');
      const draw = () => {
        for (const b of nav.querySelectorAll('button')) b.setAttribute('aria-current', String(b.dataset.s === sec));
        body.innerHTML = render(sec);
        wireSegs(body);
        wire(body, sec, apply, draw);
        body.scrollTop = 0;
      };
      nav.onclick = (e) => { const b = e.target.closest('button'); if (b) { sec = b.dataset.s; draw(); } };
      draw();
    },
  });
}

const S = () => app.settings;
const row = (title, desc, control) => `<div class="set-row"><div><b>${esc(title)}</b>${desc ? `<small>${esc(desc)}</small>` : ''}</div><div class="set-ctl">${control}</div></div>`;
const toggle = (id, on) => `<button type="button" class="toggle" role="switch" id="${id}" aria-checked="${!!on}"><i></i></button>`;
const sel = (id, opts, v) => `<select class="field" id="${id}">${opts.map(([a, b]) => `<option value="${esc(a)}"${String(a) === String(v) ? ' selected' : ''}>${esc(b)}</option>`).join('')}</select>`;
const THEMES = () => [['grapekit', 'Grapekit'], ['github', 'GitHub'], ['academic', getLang() === 'en' ? 'Academic' : 'Akademik'], ['sepia', getLang() === 'en' ? 'Sepia' : 'Sepya'], ['dark', t('dark')]];

function render(sec) {
  const s = S();
  switch (sec) {
    case 'general': return `<h3>${esc(t('set.general'))}</h3>
      ${row(t('set.lang'), '', field.seg('s-lang', [['tr', 'Türkçe'], ['en', 'English']], s.lang || getLang()))}
      ${row(t('set.theme'), '', field.seg('s-appTheme', [['system', t('system')], ['light', t('light')], ['dark', t('dark')]], s.appTheme))}
      ${row(t('set.restoreSession'), t('set.restoreSessionD'), toggle('s-restoreSession', s.restoreSession))}
      ${app.info.packaged ? '' : row(t('set.updates'), '', toggle('s-autoUpdate', s.autoUpdate))}`;
    case 'editor': return `<h3>${esc(t('set.editor'))}</h3>
      ${row(t('set.fontSize'), '', sel('s-editorFontSize', [12, 13, 14, 15, 16, 17, 18, 20, 22, 24].map((n) => [n, n + ' px']), s.editorFontSize))}
      ${row(t('set.font'), '', sel('s-editorFont', [['', 'Cascadia Code / Consolas'], ['Consolas, monospace', 'Consolas'], ['"Cascadia Mono", monospace', 'Cascadia Mono'], ['Figtree, "Segoe UI", sans-serif', 'Figtree (orantılı)'], ['Georgia, serif', 'Georgia (serif)']], s.editorFont))}
      ${row(t('set.tabSize'), '', field.seg('s-tabSize', [[2, '2'], [4, '4'], [8, '8']], s.tabSize))}
      ${row(t('set.maxWidth'), '', sel('s-editorMaxWidth', [[0, t('set.unlimited')], [70, '70'], [80, '80'], [100, '100'], [120, '120']], s.editorMaxWidth))}
      ${row(t('set.wrap'), '', toggle('s-wordWrap', s.wordWrap))}
      ${row(t('set.lineNumbers'), '', toggle('s-lineNumbers', s.lineNumbers))}
      ${row(t('set.spell'), '', toggle('s-spellcheck', s.spellcheck))}
      ${row(t('set.spellLang'), '', field.seg('s-spellLang', [['tr', 'Türkçe'], ['en', 'English']], s.spellLang))}
      ${row(t('set.smartPaste'), t('set.smartPasteD'), toggle('s-smartPaste', s.smartPaste))}
      ${row(t('cmd.view.focus'), '', toggle('s-focusMode', s.focusMode))}
      ${row(t('cmd.view.typewriter'), '', toggle('s-typewriter', s.typewriter))}`;
    case 'preview': return `<h3>${esc(t('set.preview'))}</h3>
      ${row(t('set.previewTheme'), '', sel('s-previewTheme', THEMES(), s.previewTheme))}
      ${row(t('set.previewFont'), '', sel('s-previewFontSize', [13, 14, 15, 16, 17, 18, 20, 22].map((n) => [n, n + ' px']), s.previewFontSize))}
      ${row(t('set.previewWidth'), '', sel('s-previewWidth', [[680, '680 px'], [760, '760 px'], [820, '820 px'], [960, '960 px'], [0, t('set.unlimited')]], s.previewWidth))}
      ${row(t('set.lineHeight'), '', field.seg('s-lineHeight', [[1.5, '1,5'], [1.7, '1,7'], [1.9, '1,9']], s.lineHeight))}
      ${row(t('set.breaks'), t('set.breaksD'), toggle('s-breaks', s.breaks))}
      ${row(t('set.syncScroll'), '', toggle('s-syncScroll', s.syncScroll))}
      ${row(t('set.remoteImages'), t('set.remoteImagesD'), toggle('s-remoteImages', s.remoteImages))}`;
    case 'export': return `<h3>${esc(t('set.export'))}</h3>
      ${row(t('pdf.paper'), '', sel('x-paper', [['A4', 'A4'], ['A5', 'A5'], ['A3', 'A3'], ['Letter', 'Letter'], ['Legal', 'Legal']], s.pdf.paper))}
      ${row(t('pdf.theme') + ' (PDF)', '', sel('x-theme', THEMES().filter(([v]) => v !== 'dark'), s.pdf.theme))}
      ${row(t('pdf.footer'), '', sel('x-footer', [['none', t('pdf.hNone')], ['page', t('pdf.fPage')], ['pageOf', t('pdf.fPageOf')]], s.pdf.footer))}
      ${row(t('pdf.cover'), t('pdf.coverHint'), toggle('x-cover', s.pdf.cover))}
      ${row(t('html.embed'), '', toggle('x-embed', s.html.embed))}`;
    case 'files': return `<h3>${esc(t('set.files'))}</h3>
      ${row(t('set.autosave'), t('set.autosaveD'), toggle('s-autosave', s.autosave))}
      ${row(t('set.imageFolder'), t('set.imageFolderD'), `<input class="field" id="s-imageFolder" value="${esc(s.imageFolder)}" style="width:160px">`)}
      ${row(t('set.defaultEncoding'), '', sel('s-defaultEncoding', [['utf-8', 'UTF-8'], ['utf-8-bom', 'UTF-8 (BOM)'], ['utf-16le', 'UTF-16 LE']], s.defaultEncoding))}
      ${row(t('set.defaultEol'), '', field.seg('s-defaultEol', [['crlf', 'CRLF (Windows)'], ['lf', 'LF (Unix)']], s.defaultEol))}
      ${row(t('set.associate'), t('set.associateD'), `<button type="button" class="btn" id="a-assoc">${esc(t('open'))}</button>`)}
      ${app.info.packaged ? '' : row(t('set.explorer'), t('set.explorerD'), toggle('a-explorer', app.info.explorerMenu))}
      <div class="set-reset"><button type="button" class="btn btn-danger" id="a-reset">${esc(t('set.reset'))}</button></div>`;
    case 'about': return `<div class="about">
      <img src="icon.png" width="88" height="88" alt="">
      <div><h3>${esc(t('app.name'))} ${esc(app.info.version)}</h3><p>${esc(t('set.free'))}</p><p class="hint">${esc(t('set.privacy'))}</p>
      <p class="hint" id="u-status">${esc(app.info.packaged ? t('set.storeUpdates') : app.info.updateStatus || '')}</p>
      <div class="btn-row">${app.info.packaged ? '' : `<button type="button" class="btn btn-primary" id="u-check">${esc(app.info.updateAvailable ? app.info.updateLabel : t('cmd.help.updates'))}</button>`}
      <button type="button" class="btn" data-url="page">${esc(t('cmd.help.site'))}</button>
      <button type="button" class="btn" data-url="feedback">${esc(t('cmd.help.feedback'))}</button>
      <button type="button" class="btn" data-url="coffee">${icon('coffee')}${esc(t('cmd.help.coffee'))}</button></div>
      <p class="hint">© 2026 Grapekit · MIT</p></div></div>`;
    default: return '';
  }
}

function wire(body, sec, apply, redraw) {
  const s = S();
  const set = (k, v) => { s[k] = v; apply(k); };
  for (const tg of body.querySelectorAll('.toggle')) tg.onclick = () => {
    const v = tg.getAttribute('aria-checked') !== 'true';
    tg.setAttribute('aria-checked', String(v));
    const id = tg.id;
    if (id.startsWith('s-')) set(id.slice(2), v);
    else if (id === 'x-cover') { s.pdf.cover = v; apply('pdf'); }
    else if (id === 'x-embed') { s.html.embed = v; apply('html'); }
    else if (id === 'a-explorer') call('shell.explorerMenu', { enabled: v }).then((r) => { app.info.explorerMenu = r; });
  };
  for (const sg of body.querySelectorAll('.seg')) sg.addEventListener('change', () => {
    const k = sg.id.slice(2); let v = sg.dataset.value;
    if (k === 'tabSize') v = +v; if (k === 'lineHeight') v = +v;
    set(k, v);
    if (k === 'lang') redraw();
  });
  for (const se of body.querySelectorAll('select')) se.onchange = () => {
    const id = se.id, v = /^-?\d+(\.\d+)?$/.test(se.value) ? +se.value : se.value;
    if (id.startsWith('s-')) set(id.slice(2), v);
    else if (id === 'x-paper') { s.pdf.paper = v; apply('pdf'); }
    else if (id === 'x-theme') { s.pdf.theme = v; apply('pdf'); }
    else if (id === 'x-footer') { s.pdf.footer = v; apply('pdf'); }
  };
  const img = body.querySelector('#s-imageFolder');
  if (img) img.onchange = () => set('imageFolder', img.value.trim().replace(/[\\/]+$/, '') || 'resimler');
  const assoc = body.querySelector('#a-assoc');
  if (assoc) assoc.onclick = () => call('shell.defaultApps', {});
  const reset = body.querySelector('#a-reset');
  if (reset) reset.onclick = async () => {
    if (!(await ask(t('set.reset') + '?', [{ label: t('reset'), danger: true, primary: true, value: true }, { label: t('cancel'), value: false }]))) return;
    const keep = { userTemplates: s.userTemplates };
    Object.assign(s, structuredClone(DEFAULTS), keep);
    apply('*'); redraw();
  };
  for (const b of body.querySelectorAll('[data-url]')) b.onclick = () => call('shell.url', { which: b.dataset.url });
  const u = body.querySelector('#u-check');
  if (u) u.onclick = async () => {
    if (app.info.updateAvailable) { call('update.install', {}); return; }
    u.disabled = true;
    const r = await call('update.check', {});
    Object.assign(app.info, r || {});
    redraw();
  };
  if (sec === 'about') {
    const off = onHost('update-status', (r) => { Object.assign(app.info, r); const st = body.querySelector('#u-status'); if (st) st.textContent = r.updateStatus || ''; else off(); });
  }
}
