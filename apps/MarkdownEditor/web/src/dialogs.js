// Ekleme ve araç diyalogları: bağlantı, görsel, tablo, satıra git, komut paleti, şablon, emoji, uyarı kutusu, kısayollar.
import emojiData from 'markdown-it-emoji/lib/data/full.mjs';
import { app, baseName, dirName } from './state.js';
import { call } from './bridge.js';
import { t, getLang } from './i18n.js';
import { icon } from './icons.js';
import { dialog, field, esc, h, toast, prompt } from './ui.js';
import { insertSnippet, selectedText, gotoLine } from './editor.js';
import { newTable } from './table.js';
import { commands, label, run } from './commands.js';
import { TEMPLATES, templateText } from './templates.js';
import { newDoc } from './docs.js';

const V = () => app.view;

// ---------- Bağlantı ----------
export async function linkDialog() {
  const sel = selectedText(V());
  let clip = '';
  try { clip = (await navigator.clipboard.readText()).trim(); } catch { /* izin yok */ }
  const url0 = /^(https?:|mailto:)\S+$/i.test(clip) ? clip : /^(https?:|mailto:)/i.test(sel) ? sel : '';
  const { value } = await dialog({
    title: t('dlg.link.title'),
    body: field.text('l-text', t('dlg.link.text'), url0 === sel ? '' : sel) + field.text('l-url', t('dlg.link.url'), url0, 'https://'),
    buttons: [{ label: t('insert'), primary: true, value: (d) => ({ text: d.querySelector('#l-text').value, url: d.querySelector('#l-url').value.trim() }) }, { label: t('cancel'), value: null }],
    onOpen: (d) => (url0 && !sel ? d.querySelector('#l-text') : d.querySelector(url0 ? '#l-text' : '#l-url')).focus(),
  });
  if (!value || !value.url) return;
  const url = /\s/.test(value.url) ? `<${value.url}>` : value.url;
  insertSnippet(V(), `[${value.text || value.url}](${url})|`);
}

// ---------- Görsel ----------
export async function imageDialog() {
  const tab = app.active;
  const { value } = await dialog({
    title: t('dlg.image.title'),
    body: `${field.text('i-alt', t('dlg.image.alt'), selectedText(V()))}
      <label class="field-l" for="i-src">${esc(t('dlg.image.src'))}</label>
      <div class="field-row"><input class="field" id="i-src" type="text" placeholder="https:// …"><button type="button" class="btn" id="i-browse">${esc(t('browse'))}</button></div>
      ${field.check('i-copy', t('dlg.image.copy'), !!tab?.path)}`,
    buttons: [{ label: t('insert'), primary: true, value: (d) => ({ alt: d.querySelector('#i-alt').value, src: d.querySelector('#i-src').value.trim(), copy: d.querySelector('#i-copy').checked }) }, { label: t('cancel'), value: null }],
    onOpen: (d) => {
      d.querySelector('#i-browse').onclick = async () => {
        const p = await call('file.openDialog', { multi: false, kind: 'image' });
        if (p?.[0]) { d.querySelector('#i-src').value = p[0]; if (!d.querySelector('#i-alt').value) d.querySelector('#i-alt').value = baseName(p[0]).replace(/\.[^.]+$/, ''); }
      };
    },
  });
  if (!value?.src) return;
  let src = value.src;
  if (/^[a-z]:[\\/]|^\\\\/i.test(src)) {
    if (value.copy && tab?.path) {
      const r = await call('image.import', { docDir: dirName(tab.path), folder: app.settings.imageFolder, source: src });
      src = r.rel;
    } else if (tab?.path) src = await call('path.relative', { from: dirName(tab.path), to: src });
    else src = 'file:///' + src.replace(/\\/g, '/');
  }
  insertSnippet(V(), `![${value.alt}](${src.includes(' ') ? `<${src}>` : src})|`);
}

/** Panodan/sürüklemeden görsel kaydeder ve bağlantısını ekler. data: base64, ext: png/jpg… */
export async function insertImageData(data, ext, name = '') {
  const tab = app.active;
  if (!tab?.path) { toast(t('ask.saveFirstImage'), { kind: 'warn', actions: [{ label: t('cmd.file.saveAs'), run: () => run('file.saveAs') }] }); return; }
  const r = await call('image.save', { docDir: dirName(tab.path), folder: app.settings.imageFolder, data, ext, name });
  insertSnippet(V(), `![${name.replace(/\.[^.]+$/, '') || ''}](${r.rel.includes(' ') ? `<${r.rel}>` : r.rel})|`);
  toast(t('info.imageSaved', { name: r.rel }), { timeout: 2500 });
}

// ---------- Tablo ----------
export async function tableDialog() {
  let rows = 3, cols = 3;
  const grid = Array.from({ length: 8 }, (_, r) => Array.from({ length: 8 }, (_, c) => `<button type="button" class="tg-cell" data-r="${r + 1}" data-c="${c + 1}" tabindex="-1" aria-label="${r + 1}×${c + 1}"></button>`).join('')).join('');
  const { value } = await dialog({
    title: t('dlg.table.title'), width: 380,
    body: `<p class="hint">${esc(t('dlg.table.hint'))}</p><div class="table-grid">${grid}</div>
      <div class="row2">${field.number('tb-rows', t('dlg.table.rows'), rows, 1, 200)}${field.number('tb-cols', t('dlg.table.cols'), cols, 1, 30)}</div>`,
    buttons: [{ label: t('insert'), primary: true, value: (d) => ({ rows: +d.querySelector('#tb-rows').value, cols: +d.querySelector('#tb-cols').value }) }, { label: t('cancel'), value: null }],
    onOpen: (d, done) => {
      const paint = (r, c) => { for (const x of d.querySelectorAll('.tg-cell')) x.classList.toggle('on', +x.dataset.r <= r && +x.dataset.c <= c); };
      paint(rows, cols);
      for (const x of d.querySelectorAll('.tg-cell')) {
        x.onmouseenter = () => paint(+x.dataset.r, +x.dataset.c);
        x.onclick = () => done({ rows: +x.dataset.r, cols: +x.dataset.c });
      }
      d.querySelector('.table-grid').onmouseleave = () => paint(+d.querySelector('#tb-rows').value, +d.querySelector('#tb-cols').value);
      for (const id of ['#tb-rows', '#tb-cols']) d.querySelector(id).oninput = () => paint(+d.querySelector('#tb-rows').value, +d.querySelector('#tb-cols').value);
    },
  });
  if (!value) return;
  insertSnippet(V(), newTable(Math.max(1, value.rows), Math.min(30, Math.max(1, value.cols)), t('dlg.table.header')) + '\n|', { block: true });
}

// ---------- Satıra git ----------
export async function gotoDialog() {
  const n = V().state.doc.lines;
  const v = await prompt(t('dlg.goto.title'), { placeholder: t('dlg.goto.ph', { n }) });
  const m = /^(\d+)(?:[:,](\d+))?$/.exec((v || '').trim());
  if (m) gotoLine(V(), +m[1], m[2] ? +m[2] - 1 : 0);
}

// ---------- Komut paleti ----------
export async function palette() {
  const all = [...commands.values()].filter((c) => !c.hidden && !c.submenu).map((c) => ({ id: c.id, label: label(c.id), kbd: [].concat(c.kbd || c.kbdHint || [])[0] || '', icon: c.icon }));
  all.sort((a, b) => a.label.localeCompare(b.label, getLang()));
  await dialog({
    title: t('cmd.edit.palette').replace('…', ''), width: 560, className: 'palette', buttons: [],
    body: `<input class="field" type="text" id="p-q" placeholder="${esc(t('dlg.palette.ph'))}" autocomplete="off" autofocus><div class="p-list" role="listbox"></div>`,
    onOpen: (d, done) => {
      const q = d.querySelector('#p-q'), list = d.querySelector('.p-list');
      let shown = [], sel = 0;
      const norm = (s) => s.toLocaleLowerCase(getLang()).normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/ı/g, 'i');
      const draw = () => {
        const words = norm(q.value).split(/\s+/).filter(Boolean);
        shown = all.filter((c) => words.every((w) => norm(c.label).includes(w) || c.id.includes(w))).slice(0, 60);
        sel = Math.min(sel, Math.max(0, shown.length - 1));
        list.innerHTML = shown.map((c, i) => `<div class="p-item${i === sel ? ' sel' : ''}" role="option" data-i="${i}">${c.icon ? icon(c.icon) : '<span class="ic"></span>'}<span>${esc(c.label)}</span><kbd>${esc(c.kbd)}</kbd></div>`).join('')
          || `<div class="p-empty">${esc(t('side.noResults'))}</div>`;
        list.querySelector('.sel')?.scrollIntoView({ block: 'nearest' });
      };
      q.oninput = () => { sel = 0; draw(); };
      q.onkeydown = (e) => {
        if (e.key === 'ArrowDown') { sel = Math.min(shown.length - 1, sel + 1); draw(); e.preventDefault(); }
        else if (e.key === 'ArrowUp') { sel = Math.max(0, sel - 1); draw(); e.preventDefault(); }
        else if (e.key === 'Enter') { e.preventDefault(); e.stopPropagation(); const c = shown[sel]; done(null); if (c) setTimeout(() => run(c.id), 0); }
      };
      list.onclick = (e) => { const it = e.target.closest('.p-item'); if (it) { const c = shown[+it.dataset.i]; done(null); setTimeout(() => run(c.id), 0); } };
      draw();
    },
  });
}

// ---------- Şablonlar ----------
export async function templateDialog() {
  const lang = getLang();
  const mine = app.settings.userTemplates || [];
  const card = (id, ic, name, preview) => `<button type="button" class="tpl" data-id="${esc(id)}">${icon(ic)}<b>${esc(name)}</b><small>${esc(preview)}</small></button>`;
  const first = (s) => s.replace(/^---[\s\S]*?---\s*/, '').split('\n').find((l) => l.trim())?.replace(/^#+\s*/, '').slice(0, 60) || '';
  const body = `<div class="tpl-grid">${card('blank', 'filePlus', t('welcome.new'), '')}${TEMPLATES.map((x) => card(x.id, x.icon, t('tpl.' + x.id), first(lang === 'en' ? x.en : x.tr))).join('')}</div>
    ${mine.length ? `<h3 class="sub-h">${esc(t('dlg.tpl.mine'))}</h3><div class="tpl-grid">${mine.map((x, i) => card('u' + i, 'template', x.name, first(x.text))).join('')}</div>` : ''}`;
  const { value } = await dialog({
    title: t('dlg.tpl.title'), width: 720, body, buttons: [{ label: t('cancel'), value: null }],
    onOpen: (d, done) => { for (const b of d.querySelectorAll('.tpl')) b.onclick = () => done(b.dataset.id); },
  });
  if (!value) return;
  if (value === 'blank') { newDoc(''); return; }
  const text = value.startsWith('u') ? mine[+value.slice(1)].text : templateText(TEMPLATES.find((x) => x.id === value), lang);
  newDoc(text);
}

export async function saveAsTemplate(saveSettings) {
  const name = await prompt(t('cmd.file.saveTemplate').replace('…', ''), { label: t('dlg.tpl.name'), value: app.active ? '' : '' });
  if (!name) return;
  app.settings.userTemplates = [...(app.settings.userTemplates || []).filter((x) => x.name !== name), { name, text: V().state.doc.toString() }];
  saveSettings();
  toast(t('info.templateSaved'));
}

// ---------- Emoji ----------
const EMOJI = Object.entries(emojiData);
export async function emojiDialog() {
  await dialog({
    title: t('dlg.emoji.title'), width: 520, buttons: [], className: 'emoji-dlg',
    body: `<input class="field" id="e-q" type="text" placeholder="${esc(t('dlg.emoji.ph'))}" autocomplete="off" autofocus><div class="emoji-grid"></div>`,
    onOpen: (d, done) => {
      const q = d.querySelector('#e-q'), g = d.querySelector('.emoji-grid');
      const draw = () => {
        const s = q.value.trim().toLowerCase().replace(/:/g, '');
        const list = (s ? EMOJI.filter(([k]) => k.includes(s)) : EMOJI).slice(0, 240);
        g.innerHTML = list.map(([k, e]) => `<button type="button" title=":${esc(k)}:" data-k="${esc(k)}">${e}</button>`).join('');
      };
      q.oninput = draw;
      q.onkeydown = (e) => { if (e.key === 'Enter') { e.preventDefault(); e.stopPropagation(); g.querySelector('button')?.click(); } };
      g.onclick = (e) => { const b = e.target.closest('button'); if (b) { done(null); insertSnippet(V(), `:${b.dataset.k}:|`); } };
      draw();
    },
  });
}

// ---------- Uyarı kutusu ----------
export async function alertDialog() {
  const kinds = ['note', 'tip', 'important', 'warning', 'caution'];
  const { value } = await dialog({
    title: t('dlg.alert.title'), width: 420, buttons: [{ label: t('cancel'), value: null }],
    body: `<div class="alert-pick">${kinds.map((k) => `<button type="button" class="ap ap-${k}" data-k="${k}">${icon('alert')}<span>${esc(t('alert.' + k))}</span></button>`).join('')}</div>`,
    onOpen: (d, done) => { for (const b of d.querySelectorAll('.ap')) b.onclick = () => done(b.dataset.k); },
  });
  if (!value) return;
  const sel = selectedText(V());
  insertSnippet(V(), `> [!${value.toUpperCase()}]\n> ${sel ? sel.split('\n').join('\n> ') : '|'}`, { block: true });
}

// ---------- Kısayollar ----------
export async function shortcutsDialog() {
  const rows = [...commands.values()].filter((c) => c.kbd || c.kbdHint).map((c) => [label(c.id), [].concat(c.kbd || c.kbdHint).join('  ·  ')])
    .sort((a, b) => a[0].localeCompare(b[0], getLang()));
  const extra = getLang() === 'en'
    ? [['Multiple cursors', 'Alt + click'], ['Column selection', 'Alt + drag'], ['Fold / unfold', 'Ctrl+Shift+[ / ]'], ['Next cell in a table', 'Tab']]
    : [['Çoklu imleç', 'Alt + tıklama'], ['Sütun seçimi', 'Alt + sürükle'], ['Katla / aç', 'Ctrl+Shift+[ / ]'], ['Tabloda sonraki hücre', 'Tab']];
  await dialog({
    title: t('cmd.help.shortcuts'), width: 620, buttons: [{ label: t('close'), primary: true, value: true }],
    body: `<input class="field" id="k-q" placeholder="${esc(t('dlg.palette.ph'))}" autofocus><table class="kbd-table"><tbody>${[...rows, ...extra].map(([a, b]) => `<tr><td>${esc(a)}</td><td><kbd>${esc(b)}</kbd></td></tr>`).join('')}</tbody></table>`,
    onOpen: (d) => {
      d.querySelector('#k-q').oninput = (e) => { const s = e.target.value.toLocaleLowerCase(); for (const tr of d.querySelectorAll('tr')) tr.hidden = !tr.textContent.toLocaleLowerCase().includes(s); };
    },
  });
}

export { h };
