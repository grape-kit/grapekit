// Komutlar: menü, araç çubuğu, komut paleti ve kısayollar tek listeden beslenir.
import { undo, redo, selectAll, indentMore, indentLess, moveLineUp, moveLineDown, copyLineDown } from '@codemirror/commands';
import { openSearchPanel, selectNextOccurrence } from '@codemirror/search';
import { app } from './state.js';
import { t } from './i18n.js';
import { icon } from './icons.js';
import { esc, showMenu, closeMenus } from './ui.js';
import { wrap, linePrefix, heading, insertSnippet, selectedText } from './editor.js';
import { tableOp } from './table.js';

export const commands = new Map();

/** Komut tanımlar. c: { run, kbd, icon, label?, checked?() , enabled?() } */
export function def(id, c) { commands.set(id, { id, ...c }); }
export const label = (id) => commands.get(id)?.label?.() ?? t('cmd.' + id);
export async function run(id, ...args) {
  const c = commands.get(id);
  if (!c || (c.enabled && !c.enabled())) return false;
  closeMenus();
  await c.run(...args);
  return true;
}

// --- Kısayollar ---
function norm(e) {
  const parts = [];
  if (e.ctrlKey) parts.push('Ctrl'); if (e.altKey) parts.push('Alt'); if (e.shiftKey) parts.push('Shift');
  let k = e.key.length === 1 ? e.key.toUpperCase() : e.key;
  if (e.code?.startsWith('Digit')) k = e.code.slice(5);
  const codes = { Slash: '/', Equal: '=', Minus: '-', Comma: ',', Period: '.', Backslash: '\\', BracketLeft: '[', BracketRight: ']', Semicolon: ';' };
  if (codes[e.code]) k = codes[e.code];
  if (k === ' ') k = 'Space';
  parts.push(k);
  return parts.join('+');
}
const bound = new Map(); // "Ctrl+B" → id
export function bindKeys() {
  bound.clear();
  for (const c of commands.values()) for (const k of [].concat(c.kbd || [])) bound.set(k, c.id);
}
export function handleKey(e) {
  const id = bound.get(norm(e));
  if (!id) return false;
  const c = commands.get(id);
  if (c.editorOnly && !e.target.closest('.cm-editor')) return false;
  e.preventDefault(); e.stopPropagation();
  run(id);
  return true;
}

// --- Düzenleyici yardımcıları ---
const V = () => app.view;
const ed = (fn) => () => { if (!app.active) return; fn(V()); };

export function registerEditing() {
  def('edit.undo', { run: ed((v) => undo(v)), kbd: 'Ctrl+Z', icon: 'undo', editorOnly: true });
  def('edit.redo', { run: ed((v) => redo(v)), kbd: ['Ctrl+Y', 'Ctrl+Shift+Z'], icon: 'redo', editorOnly: true });
  def('edit.cut', { run: () => { V().focus(); document.execCommand('cut'); }, kbdHint: 'Ctrl+X' });
  def('edit.copy', { run: () => { V().focus(); document.execCommand('copy'); }, kbdHint: 'Ctrl+C' });
  def('edit.selectAll', { run: ed((v) => { v.focus(); selectAll(v); }), kbdHint: 'Ctrl+A' });
  def('edit.find', { run: ed((v) => { v.focus(); openSearchPanel(v); }), kbd: 'Ctrl+F', icon: 'search' });
  def('edit.replace', { run: ed((v) => { v.focus(); openSearchPanel(v); setTimeout(() => v.dom.querySelector('.cm-search input[name=replace]')?.focus(), 0); }), kbd: 'Ctrl+H' });
  def('edit.nextMatch', { run: ed((v) => selectNextOccurrence(v)), kbdHint: 'Ctrl+D' });
  def('edit.lineUp', { run: ed((v) => moveLineUp(v)), kbdHint: 'Alt+↑' });
  def('edit.lineDown', { run: ed((v) => moveLineDown(v)), kbdHint: 'Alt+↓' });
  def('edit.dupLine', { run: ed((v) => copyLineDown(v)), kbdHint: 'Shift+Alt+↓' });

  def('fmt.bold', { run: ed((v) => wrap(v, '**', '**', t('cmd.fmt.bold').toLocaleLowerCase())), kbd: 'Ctrl+B', icon: 'bold' });
  def('fmt.italic', { run: ed((v) => wrap(v, '*', '*', t('cmd.fmt.italic').toLocaleLowerCase())), kbd: 'Ctrl+I', icon: 'italic' });
  def('fmt.strike', { run: ed((v) => wrap(v, '~~')), kbd: 'Alt+Shift+5', icon: 'strike' });
  def('fmt.code', { run: ed((v) => wrap(v, '`')), kbd: 'Ctrl+E', icon: 'code' });
  def('fmt.mark', { run: ed((v) => wrap(v, '==')), kbd: 'Ctrl+Shift+H', icon: 'mark' });
  def('fmt.sup', { run: ed((v) => wrap(v, '^')), kbd: 'Ctrl+.' });
  def('fmt.sub', { run: ed((v) => wrap(v, '~')) });
  for (let i = 0; i <= 6; i++) def('fmt.h' + i, { run: ed((v) => heading(v, i)), kbd: i ? 'Ctrl+' + i : 'Ctrl+0', label: () => (i ? `${t('cmd.fmt.h')} ${i}` : t('cmd.fmt.h0')) });
  def('fmt.quote', { run: ed((v) => linePrefix(v, '> ', { exclusive: /^> / })), kbd: 'Ctrl+Shift+.', icon: 'quote' });
  def('fmt.ul', { run: ed((v) => linePrefix(v, '- ')), kbd: 'Ctrl+Shift+8', icon: 'ul' });
  def('fmt.ol', { run: ed((v) => linePrefix(v, '1. ', { numbered: true })), kbd: 'Ctrl+Shift+7', icon: 'ol' });
  def('fmt.task', { run: ed((v) => linePrefix(v, '- [ ] ')), kbd: 'Ctrl+Shift+9', icon: 'task' });
  def('fmt.indent', { run: ed((v) => indentMore(v)), kbdHint: 'Tab' });
  def('fmt.outdent', { run: ed((v) => indentLess(v)), kbdHint: 'Shift+Tab' });
  def('fmt.clear', {
    run: ed((v) => {
      const s = selectedText(v);
      if (!s) return;
      const clean = s.replace(/(\*\*|__|~~|==|\*|_|`|\^|~)(.+?)\1/g, '$2').replace(/^#{1,6} /gm, '').replace(/^> /gm, '').replace(/\[([^\]]+)\]\([^)]+\)/g, '$1');
      const r = v.state.selection.main;
      v.dispatch({ changes: { from: r.from, to: r.to, insert: clean } });
    }),
    kbd: 'Ctrl+\\',
  });

  def('insert.code', { run: ed((v) => insertSnippet(v, '```|\n{sel}\n```', { block: true })), kbd: 'Ctrl+Shift+K', icon: 'codeblock' });
  def('insert.hr', { run: ed((v) => insertSnippet(v, '---\n|', { block: true })), icon: 'hr' });
  def('insert.math', { run: ed((v) => insertSnippet(v, '$$\n|{sel}|\n$$', { block: true })), kbd: 'Ctrl+Shift+M', icon: 'math' });
  def('insert.mathInline', { run: ed((v) => wrap(v, '$', '$', 'x^2')), kbd: 'Ctrl+M' });
  def('insert.mermaid', { run: ed((v) => insertSnippet(v, '```mermaid\nflowchart LR\n  A[|Başla|] --> B{Karar}\n  B -- Evet --> C[Sonuç]\n  B -- Hayır --> A\n```', { block: true })), icon: 'diagram' });
  def('insert.footnote', {
    run: ed((v) => {
      const n = ((v.state.doc.toString().match(/\[\^(\d+)\]/g) || []).map((x) => +x.slice(2, -1)).reduce((a, b) => Math.max(a, b), 0)) + 1;
      const pos = v.state.selection.main.to;
      v.dispatch({ changes: [{ from: pos, insert: `[^${n}]` }, { from: v.state.doc.length, insert: `\n\n[^${n}]: ` }], selection: { anchor: v.state.doc.length + `[^${n}]`.length + `\n\n[^${n}]: `.length } });
      v.focus();
    }),
    icon: 'footnote',
  });
  def('insert.pagebreak', { run: ed((v) => insertSnippet(v, '<!-- pagebreak -->\n|', { block: true })), kbd: 'Ctrl+Enter', icon: 'pagebreak' });
  def('insert.toc', { run: ed((v) => insertSnippet(v, '[TOC]\n|', { block: true })), icon: 'toc' });
  def('insert.frontMatter', {
    run: ed((v) => {
      if (/^---\r?\n/.test(v.state.doc.toString())) return;
      const d = new Date().toISOString().slice(0, 10);
      v.dispatch({ changes: { from: 0, insert: `---\ntitle: \nauthor: \ndate: ${d}\n---\n\n` }, selection: { anchor: 11 } });
      v.focus();
    }),
  });
  def('insert.date', {
    run: ed((v) => insertSnippet(v, new Date().toLocaleString(app.settings.lang === 'en' ? 'en-US' : 'tr-TR', { dateStyle: 'long', timeStyle: 'short' }) + '|')),
    kbd: 'Alt+Shift+D', icon: 'calendar',
  });
  def('insert.details', { run: ed((v) => insertSnippet(v, '<details>\n<summary>|Başlık|</summary>\n\n{sel}\n\n</details>', { block: true })), icon: 'details' });

  for (const op of ['format', 'rowAbove', 'rowBelow', 'colLeft', 'colRight', 'delRow', 'delCol', 'alignL', 'alignC', 'alignR'])
    def('tbl.' + op, { run: ed((v) => tableOp(v, op)), kbd: op === 'format' ? 'Ctrl+Alt+Shift+F' : undefined });
}

/** Düzenleyici içi kısayollar (tabloda Tab vb.) */
export const editorKeymap = [
  { key: 'Tab', run: (v) => tableOp(v, 'next') },
  { key: 'Shift-Tab', run: (v) => tableOp(v, 'prev') },
];

// ===================== Menü çubuğu =====================
export function menuItems(ids) {
  return ids.map((id) => {
    if (id === '-') return '-';
    if (typeof id === 'object') return id;
    const c = commands.get(id);
    if (!c) return null;
    return {
      label: label(id), icon: c.icon, kbd: [].concat(c.kbd || c.kbdHint || [])[0], checked: c.checked?.(),
      disabled: c.enabled ? !c.enabled() : false, run: () => run(id), submenu: c.submenu,
    };
  }).filter(Boolean);
}

export function buildMenubar(el, menus) {
  el.innerHTML = '';
  let openIdx = -1;
  const btns = menus.map((m, i) => {
    const b = document.createElement('button');
    b.type = 'button'; b.className = 'menubar-item'; b.textContent = t(m.label); b.setAttribute('role', 'menuitem'); b.setAttribute('aria-haspopup', 'true');
    const open = () => {
      const r = b.getBoundingClientRect();
      openIdx = i; btns.forEach((x) => x.classList.toggle('open', x === b));
      showMenu(menuItems(m.items()), r.left, r.bottom + 2, { onClose: () => { if (openIdx === i) { openIdx = -1; b.classList.remove('open'); } } });
    };
    b.addEventListener('mousedown', (e) => { e.preventDefault(); if (openIdx === i) closeMenus(); else open(); });
    b.addEventListener('mouseenter', () => { if (openIdx >= 0 && openIdx !== i) open(); });
    b.addEventListener('keydown', (e) => {
      if (e.key === 'ArrowDown' || e.key === 'Enter' || e.key === ' ') { open(); setTimeout(() => document.querySelector('.menu .menu-item:not([disabled])')?.focus(), 0); e.preventDefault(); }
      else if (e.key === 'ArrowRight') { btns[(i + 1) % btns.length].focus(); e.preventDefault(); }
      else if (e.key === 'ArrowLeft') { btns[(i - 1 + btns.length) % btns.length].focus(); e.preventDefault(); }
    });
    el.append(b);
    return b;
  });
  // Alt ile menü çubuğuna odaklan
  return { focus: () => btns[0]?.focus(), btns };
}

// ===================== Araç çubuğu =====================
export function buildToolbar(el, groups) {
  el.innerHTML = '';
  for (const g of groups) {
    const wrapEl = document.createElement('div'); wrapEl.className = 'tb-group' + (g.end ? ' tb-end' : '');
    for (const id of g.items) {
      if (typeof id === 'object') { wrapEl.append(id.el()); continue; }
      const c = commands.get(id);
      if (!c) continue;
      const b = document.createElement('button');
      b.type = 'button'; b.className = 'tb-btn'; b.dataset.cmd = id;
      const k = [].concat(c.kbd || c.kbdHint || [])[0];
      b.title = label(id) + (k ? ` (${k})` : ''); b.setAttribute('aria-label', label(id));
      b.innerHTML = icon(c.icon || 'more');
      b.addEventListener('mousedown', (e) => e.preventDefault()); // düzenleyiciden odak çalmasın
      b.addEventListener('click', () => run(id));
      wrapEl.append(b);
    }
    el.append(wrapEl);
  }
}

export function refreshToolbarState(el) {
  for (const b of el.querySelectorAll('[data-cmd]')) {
    const c = commands.get(b.dataset.cmd);
    if (c?.checked) b.setAttribute('aria-pressed', String(!!c.checked()));
    if (c?.enabled) b.disabled = !c.enabled();
  }
}

export { esc };
