// Dışa aktarma, içe aktarma, yazdırma ve toplu dönüştürme diyalogları.
import { app, baseName, dirName, extOf } from './state.js';
import { call } from './bridge.js';
import { t, getLang } from './i18n.js';
import { dialog, field, wireSegs, esc, toast } from './ui.js';
import { buildHtml, pdfOptions, copyHtml } from './export.js';
const buildDocx = async (...a) => (await import('./docx.js')).buildDocx(...a);
const htmlToMarkdown = async (h) => (await import('./importer.js')).htmlToMarkdown(h);
const docxToMarkdown = async (b) => (await import('./importer.js')).docxToMarkdown(b);
import { splitFrontMatter } from './render.js';
import { newDoc } from './docs.js';

const THEMES = () => [['github', 'GitHub'], ['grapekit', 'Grapekit'], ['academic', getLang() === 'en' ? 'Academic' : 'Akademik'], ['sepia', getLang() === 'en' ? 'Sepia' : 'Sepya'], ['dark', t('dark')]];
const docTitle = (tab, src) => splitFrontMatter(src).meta?.title || (/^#\s+(.+)$/m.exec(src)?.[1]) || (tab?.path ? baseName(tab.path).replace(/\.[^.]+$/, '') : t('untitled'));
const outName = (tab, ext) => (tab?.path ? baseName(tab.path).replace(/\.[^.]+$/, '') : docTitle(tab, app.view.state.doc.toString()).replace(/[\\/:*?"<>|]/g, '').slice(0, 60)) + '.' + ext;

function done(path) {
  toast(t('info.exported', { name: baseName(path) }), { actions: [{ label: t('info.openFile'), run: () => call('shell.open', { path }) }, { label: t('info.openFolder'), run: () => call('shell.reveal', { path }) }], timeout: 6000 });
}
const fail = (e) => toast(t('err.export', { m: e.message || e }), { kind: 'error', timeout: 8000 });

function ctx(tab) {
  return { docBase: tab?.docBase || '', docDir: tab?.docDir || '', breaks: app.settings.breaks, lang: getLang() };
}

// ===================== PDF =====================
function pdfForm(p) {
  return `<div class="form-grid">
    <div>${field.select('p-paper', t('pdf.paper'), [['A4', 'A4'], ['A5', 'A5'], ['A3', 'A3'], ['Letter', 'Letter'], ['Legal', 'Legal']], p.paper)}</div>
    <div><label class="field-l">${esc(t('pdf.orient'))}</label>${field.seg('p-orient', [['p', t('pdf.portrait')], ['l', t('pdf.landscape')]], p.landscape ? 'l' : 'p')}</div>
    <div><label class="field-l">${esc(t('pdf.margins'))}</label>${field.seg('p-margins', [['narrow', t('pdf.mNarrow')], ['normal', t('pdf.mNormal')], ['wide', t('pdf.mWide')]], p.margins)}</div>
    <div>${field.select('p-scale', t('pdf.scale'), [[80, '80%'], [90, '90%'], [100, '100%'], [110, '110%'], [125, '125%']], p.scale)}</div>
    <div>${field.select('p-theme', t('pdf.theme'), THEMES().filter(([v]) => v !== 'dark'), p.theme)}</div>
    <div>${field.select('p-header', t('pdf.header'), [['none', t('pdf.hNone')], ['title', t('pdf.hTitle')], ['titleDate', t('pdf.hTitleDate')]], p.header)}</div>
    <div>${field.select('p-footer', t('pdf.footer'), [['none', t('pdf.hNone')], ['page', t('pdf.fPage')], ['pageOf', t('pdf.fPageOf')]], p.footer)}</div>
  </div>
  <div class="checks">${field.check('p-cover', t('pdf.cover'), p.cover, t('pdf.coverHint'))}${field.check('p-toc', t('pdf.toc'), p.toc)}${field.check('p-breakH1', t('pdf.breakH1'), p.breakH1)}${field.check('p-bg', t('pdf.background'), p.background)}</div>`;
}
function readPdf(d) {
  return {
    paper: d.querySelector('#p-paper').value, landscape: d.querySelector('#p-orient').dataset.value === 'l', margins: d.querySelector('#p-margins').dataset.value,
    scale: +d.querySelector('#p-scale').value, theme: d.querySelector('#p-theme').value, header: d.querySelector('#p-header').value, footer: d.querySelector('#p-footer').value,
    cover: d.querySelector('#p-cover').checked, toc: d.querySelector('#p-toc').checked, breakH1: d.querySelector('#p-breakH1').checked, background: d.querySelector('#p-bg').checked,
  };
}

/** Markdown metnini PDF dosyasına yazar. */
export async function writePdf(src, tab, p, path) {
  const html = await buildHtml(src, { ...ctx(tab), theme: p.theme, forPrint: true, pdf: { ...p, lang: getLang() }, title: docTitle(tab, src) });
  return call('pdf.export', { html, path, options: pdfOptions({ ...p, lang: getLang() }, docTitle(tab, src)), docDirs: tab?.docDir ? [tab.docDir] : [] });
}

export async function exportPdf(saveSettings) {
  const tab = app.active; if (!tab) return;
  const src = app.view.state.doc.toString();
  const { value } = await dialog({
    title: t('pdf.title'), width: 560, body: pdfForm(app.settings.pdf),
    buttons: [{ label: t('pdf.preview'), value: (d) => ({ preview: true, p: readPdf(d) }) }, { label: t('export'), primary: true, value: (d) => ({ p: readPdf(d) }) }, { label: t('cancel'), value: null }],
    onOpen: (d) => wireSegs(d),
  });
  if (!value) return;
  app.settings.pdf = value.p; saveSettings();
  try {
    if (value.preview) {
      const tmp = await call('temp.path', { name: outName(tab, 'pdf') });
      await writePdf(src, tab, value.p, tmp);
      await call('pdf.view', { path: tmp, title: outName(tab, 'pdf') });
      return exportPdf(saveSettings);
    }
    const path = await call('file.saveDialog', { name: outName(tab, 'pdf'), kind: 'pdf', dir: tab.docDir });
    if (!path) return;
    await writePdf(src, tab, value.p, path);
    done(path);
  } catch (e) { fail(e); }
}

// ===================== HTML =====================
export async function exportHtml(saveSettings) {
  const tab = app.active; if (!tab) return;
  const h0 = app.settings.html;
  const { value } = await dialog({
    title: t('html.title'), width: 440,
    body: field.select('h-theme', t('pdf.theme'), THEMES(), h0.theme) + `<div class="checks">${field.check('h-embed', t('html.embed'), h0.embed)}</div>`,
    buttons: [{ label: t('export'), primary: true, value: (d) => ({ theme: d.querySelector('#h-theme').value, embed: d.querySelector('#h-embed').checked }) }, { label: t('cancel'), value: null }],
  });
  if (!value) return;
  app.settings.html = value; saveSettings();
  const path = await call('file.saveDialog', { name: outName(tab, 'html'), kind: 'html', dir: tab.docDir });
  if (!path) return;
  try {
    const src = app.view.state.doc.toString();
    const html = await buildHtml(src, { ...ctx(tab), theme: value.theme === 'dark' ? 'github' : value.theme, dark: value.theme === 'dark', embed: value.embed, embedFonts: true, title: docTitle(tab, src) });
    await call('file.write', { path, text: value.embed ? html : html.replace(/https:\/\/doc\.grapekit\.local\/[0-9a-f]+\//g, ''), encoding: 'utf-8', bom: false });
    done(path);
  } catch (e) { fail(e); }
}

// ===================== Word =====================
export async function exportDocx() {
  const tab = app.active; if (!tab) return;
  const path = await call('file.saveDialog', { name: outName(tab, 'docx'), kind: 'docx', dir: tab.docDir });
  if (!path) return;
  try {
    const src = app.view.state.doc.toString();
    const b64 = await buildDocx(src, { docDir: tab.docDir, title: docTitle(tab, src), lang: getLang() });
    await call('file.writeBase64', { path, data: b64 });
    done(path);
  } catch (e) { fail(e); }
}

// ===================== PNG =====================
export async function exportPng(saveSettings) {
  const tab = app.active; if (!tab) return;
  const p0 = app.settings.png;
  const { value } = await dialog({
    title: t('png.title'), width: 440,
    body: `<div class="form-grid">${field.select('g-width', t('png.width'), [[700, '700 px'], [900, '900 px'], [1200, '1200 px'], [1600, '1600 px']], p0.width)}${field.select('g-scale', t('png.scale'), [[1, '1×'], [2, '2× (Retina)'], [3, '3×']], p0.scale)}</div>${field.select('g-theme', t('pdf.theme'), THEMES(), p0.theme)}`,
    buttons: [{ label: t('export'), primary: true, value: (d) => ({ width: +d.querySelector('#g-width').value, scale: +d.querySelector('#g-scale').value, theme: d.querySelector('#g-theme').value }) }, { label: t('cancel'), value: null }],
  });
  if (!value) return;
  app.settings.png = value; saveSettings();
  const path = await call('file.saveDialog', { name: outName(tab, 'png'), kind: 'png', dir: tab.docDir });
  if (!path) return;
  try {
    const src = app.view.state.doc.toString();
    const html = await buildHtml(src, { ...ctx(tab), theme: value.theme === 'dark' ? 'github' : value.theme, dark: value.theme === 'dark', title: docTitle(tab, src) });
    await call('png.export', { html, path, width: value.width, scale: value.scale, docDirs: tab.docDir ? [tab.docDir] : [] });
    done(path);
  } catch (e) { fail(e); }
}

export async function copyAsHtml() {
  const tab = app.active; if (!tab) return;
  try { await copyHtml(app.view.state.doc.toString(), { ...ctx(tab), theme: 'github' }); toast(t('info.copiedHtml'), { timeout: 2000 }); } catch (e) { fail(e); }
}

export async function printDoc() {
  const tab = app.active; if (!tab) return;
  const src = app.view.state.doc.toString();
  const p = app.settings.pdf;
  const html = await buildHtml(src, { ...ctx(tab), theme: p.theme, forPrint: true, pdf: { ...p, lang: getLang() }, title: docTitle(tab, src) });
  await call('print', { html, docDirs: tab.docDir ? [tab.docDir] : [] });
}

// ===================== İçe aktarma =====================
export async function importDialog() {
  const paths = await call('file.openDialog', { multi: true, kind: 'import' });
  for (const p of paths || []) {
    try {
      const ext = extOf(p);
      let md;
      if (ext === 'docx') md = await docxToMarkdown((await call('file.readBase64', { path: p })).data);
      else md = await htmlToMarkdown((await call('file.read', { path: p })).text);
      const tab = newDoc(md);
      tab.importedName = baseName(p).replace(/\.[^.]+$/, '') + '.md';
      tab.importedDir = dirName(p);
    } catch (e) { toast(t('err.import', { m: e.message }), { kind: 'error' }); }
  }
}

// ===================== Toplu dönüştürme =====================
export async function batchDialog(saveSettings) {
  const b0 = app.settings.batch;
  let folder = app.folder || '';
  const { value } = await dialog({
    title: t('batch.title'), width: 560,
    body: `<label class="field-l">${esc(t('batch.folder'))}</label><div class="field-row"><input class="field" id="b-folder" value="${esc(folder)}" readonly><button type="button" class="btn" id="b-pick">${esc(t('browse'))}</button></div>
      <p class="hint" id="b-found"></p>
      <div class="checks">${field.check('b-sub', t('batch.sub'), b0.sub)}</div>
      <label class="field-l">${esc(t('batch.format'))}</label>${field.seg('b-format', [['pdf', 'PDF'], ['html', 'HTML'], ['docx', 'Word']], b0.format)}
      <label class="field-l">${esc(t('batch.out'))}</label>
      <div class="checks">${field.check('b-same', t('batch.same'), b0.same)}</div>
      <div class="field-row" id="b-outrow"${b0.same ? ' hidden' : ''}><input class="field" id="b-out" value="${esc(b0.out)}" readonly><button type="button" class="btn" id="b-outpick">${esc(t('browse'))}</button></div>
      <div class="progress" hidden><div class="bar"><i></i></div><p class="hint" id="b-status"></p><ul class="errors"></ul></div>`,
    buttons: [{ label: t('start'), primary: true, value: (d) => ({ go: true, d }) }, { label: t('close'), value: null }],
    onOpen: (d) => {
      wireSegs(d);
      const count = async () => {
        if (!folder) return;
        const files = await call('folder.files', { path: folder, recursive: d.querySelector('#b-sub').checked });
        d.querySelector('#b-found').textContent = t('batch.found', { n: files.length });
      };
      d.querySelector('#b-pick').onclick = async () => { const f = await call('folder.dialog', {}); if (f) { folder = f; d.querySelector('#b-folder').value = f; count(); } };
      d.querySelector('#b-outpick').onclick = async () => { const f = await call('folder.dialog', {}); if (f) d.querySelector('#b-out').value = f; };
      d.querySelector('#b-same').onchange = (e) => { d.querySelector('#b-outrow').hidden = e.target.checked; };
      d.querySelector('#b-sub').onchange = count;
      count();
    },
  });
  if (!value?.go) return;
  const d = value.d;
  const opts = { format: d.querySelector('#b-format').dataset.value, sub: d.querySelector('#b-sub').checked, same: d.querySelector('#b-same').checked, out: d.querySelector('#b-out').value };
  app.settings.batch = opts; saveSettings();
  if (!folder || (!opts.same && !opts.out)) return batchDialog(saveSettings);
  await runBatch(folder, opts);
}

async function runBatch(folder, opts) {
  const files = await call('folder.files', { path: folder, recursive: opts.sub });
  let ok = 0; const errors = [];
  const box = await new Promise((resolve) => {
    dialog({
      title: t('batch.title'), width: 520, buttons: [{ label: t('close'), value: null }],
      body: `<div class="progress"><div class="bar"><i></i></div><p class="hint" id="b-status"></p><ul class="errors"></ul></div>`,
      onOpen: (el) => resolve(el),
    });
  });
  const bar = box.querySelector('.bar i'), st = box.querySelector('#b-status'), errs = box.querySelector('.errors');
  for (let i = 0; i < files.length; i++) {
    const f = files[i];
    st.textContent = t('batch.running', { i: i + 1, n: files.length }) + ' — ' + baseName(f);
    bar.style.width = ((i / files.length) * 100).toFixed(1) + '%';
    try {
      const r = await call('file.read', { path: f });
      const src = r.text.replace(/\r\n?/g, '\n');
      const dir = dirName(f);
      const tab = { path: f, docDir: dir, docBase: await call('docbase', { dir }) };
      const rel = f.slice(folder.length).replace(/^[\\/]/, '');
      const target = (opts.same ? f : opts.out + '\\' + rel).replace(/\.[^.\\/]+$/, '.' + opts.format);
      await call('dir.ensure', { path: dirName(target) });
      if (opts.format === 'pdf') await writePdf(src, tab, app.settings.pdf, target);
      else if (opts.format === 'html') await call('file.write', { path: target, text: await buildHtml(src, { ...ctx(tab), theme: app.settings.html.theme, embed: true, embedFonts: true, title: docTitle(tab, src) }), encoding: 'utf-8', bom: false });
      else await call('file.writeBase64', { path: target, data: await buildDocx(src, { docDir: dir, title: docTitle(tab, src) }) });
      ok++;
    } catch (e) { errors.push(f); errs.insertAdjacentHTML('beforeend', `<li>${esc(baseName(f))}: ${esc(e.message || e)}</li>`); }
  }
  bar.style.width = '100%';
  st.textContent = t('batch.done', { ok, fail: errors.length });
}

/** Komut satırından (Gezgin menüsü): pencere açmadan PDF. */
export async function cliToPdf(input, output) {
  const r = await call('file.read', { path: input });
  const dir = dirName(input);
  const tab = { path: input, docDir: dir, docBase: await call('docbase', { dir }) };
  await writePdf(r.text.replace(/\r\n?/g, '\n'), tab, app.settings.pdf, output || input.replace(/\.[^.\\/]+$/, '.pdf'));
}
