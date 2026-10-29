// Düzenleyici: CodeMirror 6 + Markdown. Biçimlendirme komutları, akıllı listeler, odak/daktilo modu, ayarlanabilir bölmeler.
import { EditorState, EditorSelection, Compartment, StateField, RangeSetBuilder } from '@codemirror/state';
import {
  EditorView, keymap, lineNumbers, highlightActiveLine, highlightActiveLineGutter, drawSelection, dropCursor,
  rectangularSelection, crosshairCursor, highlightSpecialChars, Decoration, ViewPlugin, placeholder,
} from '@codemirror/view';
import { defaultKeymap, history, historyKeymap, indentWithTab, moveLineUp, moveLineDown, copyLineDown, toggleComment } from '@codemirror/commands';
import { syntaxHighlighting, HighlightStyle, indentOnInput, bracketMatching, foldGutter, foldKeymap, codeFolding } from '@codemirror/language';
import { markdown, markdownLanguage } from '@codemirror/lang-markdown';
import { languages } from '@codemirror/language-data';
import { search, searchKeymap, highlightSelectionMatches, selectNextOccurrence } from '@codemirror/search';
import { closeBrackets, closeBracketsKeymap } from '@codemirror/autocomplete';
import { tags as t } from '@lezer/highlight';

const mdStyle = HighlightStyle.define([
  { tag: t.heading1, class: 'cm-h cm-h1' }, { tag: t.heading2, class: 'cm-h cm-h2' }, { tag: t.heading3, class: 'cm-h cm-h3' },
  { tag: [t.heading4, t.heading5, t.heading6], class: 'cm-h' },
  { tag: t.strong, class: 'cm-strong' }, { tag: t.emphasis, class: 'cm-em' }, { tag: t.strikethrough, class: 'cm-strike' },
  { tag: t.link, class: 'cm-link' }, { tag: t.url, class: 'cm-url' }, { tag: t.monospace, class: 'cm-code' },
  { tag: t.quote, class: 'cm-quote' }, { tag: [t.processingInstruction, t.contentSeparator], class: 'cm-mark' },
  { tag: t.list, class: 'cm-list' }, { tag: t.meta, class: 'cm-meta' }, { tag: t.comment, class: 'cm-comment' },
  // Kod blokları içindeki diller
  { tag: t.keyword, class: 'tok-keyword' }, { tag: [t.string, t.special(t.string)], class: 'tok-string' },
  { tag: [t.number, t.bool, t.null], class: 'tok-number' }, { tag: [t.function(t.variableName), t.function(t.propertyName)], class: 'tok-function' },
  { tag: [t.typeName, t.className], class: 'tok-type' }, { tag: t.propertyName, class: 'tok-property' },
  { tag: [t.operator, t.punctuation], class: 'tok-punct' }, { tag: t.tagName, class: 'tok-tag' }, { tag: t.attributeName, class: 'tok-attr' },
]);

// --- Odak modu: imlecin bulunduğu paragraf dışındakiler soluk ---
function paragraphRange(doc, pos) {
  let a = doc.lineAt(pos), b = a;
  while (a.number > 1 && doc.line(a.number - 1).text.trim() !== '') a = doc.line(a.number - 1);
  while (b.number < doc.lines && doc.line(b.number + 1).text.trim() !== '') b = doc.line(b.number + 1);
  return [a.from, b.to];
}
const dim = Decoration.line({ class: 'cm-dim' });
const focusPlugin = ViewPlugin.fromClass(class {
  constructor(v) { this.decorations = this.build(v); }
  update(u) { if (u.docChanged || u.selectionSet || u.viewportChanged) this.decorations = this.build(u.view); }
  build(v) {
    const b = new RangeSetBuilder();
    const [from, to] = paragraphRange(v.state.doc, v.state.selection.main.head);
    for (const { from: vf, to: vt } of v.visibleRanges) {
      for (let p = vf; p <= vt;) {
        const l = v.state.doc.lineAt(p);
        if (l.to < from || l.from > to) b.add(l.from, l.from, dim);
        p = l.to + 1;
      }
    }
    return b.finish();
  }
}, { decorations: (p) => p.decorations });

// --- Daktilo modu: imleç dikeyde ortada kalır ---
const typewriter = EditorView.updateListener.of((u) => {
  if (!u.selectionSet && !u.docChanged) return;
  const head = u.state.selection.main.head;
  requestAnimationFrame(() => u.view.dispatch({ effects: EditorView.scrollIntoView(head, { y: 'center' }) }));
});

// Grapekit renkleri (CSS değişkenleri; açık/koyu temaya kendiliğinden uyar). EditorView.theme taban temadan önceliklidir.
const gkTheme = EditorView.theme({
  '&': { backgroundColor: 'var(--bg)', color: 'var(--ink)' },
  '.cm-content': { caretColor: 'var(--accent)' },
  '.cm-gutters': { backgroundColor: 'var(--bg)', color: 'var(--muted)', border: 'none' },
  '.cm-activeLine': { backgroundColor: 'rgba(124, 58, 237, .055)' },
  '.cm-activeLineGutter': { backgroundColor: 'transparent', color: 'var(--accent-text)' },
  '.cm-cursor, .cm-dropCursor': { borderLeftColor: 'var(--accent)', borderLeftWidth: '2px' },
  '&.cm-focused > .cm-scroller > .cm-selectionLayer .cm-selectionBackground, .cm-selectionBackground, .cm-content ::selection': { backgroundColor: 'var(--sel)' },
  '.cm-selectionMatch': { backgroundColor: 'rgba(167, 139, 250, .22)' },
  '&.cm-focused .cm-matchingBracket': { backgroundColor: 'rgba(167, 139, 250, .3)', outline: 'none' },
  '.cm-foldPlaceholder': { backgroundColor: 'var(--accent-soft)', border: 'none', color: 'var(--accent-text)' },
  '.cm-panels': { backgroundColor: 'var(--panel)', color: 'var(--ink)' },
  '.cm-panels.cm-panels-top': { borderBottom: '1px solid var(--line)' },
  '.cm-searchMatch': { backgroundColor: 'rgba(250, 204, 21, .35)', outline: '1px solid rgba(202, 138, 4, .5)' },
  '.cm-searchMatch.cm-searchMatch-selected': { backgroundColor: 'rgba(249, 115, 22, .45)' },
  '.cm-tooltip': { backgroundColor: 'var(--bg)', border: '1px solid var(--line-2)', color: 'var(--ink)' },
  '.cm-placeholder': { color: 'var(--muted)' },
});

const cWrap = new Compartment(), cNumbers = new Compartment(), cSpell = new Compartment(), cFocus = new Compartment(),
  cType = new Compartment(), cTheme = new Compartment(), cTab = new Compartment(), cPlace = new Compartment();

/** Ayarlardan düzenleyici uzantıları */
function opt(s) {
  return {
    wrap: s.wordWrap !== false ? EditorView.lineWrapping : [],
    numbers: s.lineNumbers ? [lineNumbers(), highlightActiveLineGutter(), foldGutter({ openText: '▾', closedText: '▸' })] : [foldGutter({ openText: '▾', closedText: '▸' })],
    spell: EditorView.contentAttributes.of({ spellcheck: s.spellcheck !== false ? 'true' : 'false', lang: s.spellLang || 'tr', autocorrect: 'off', autocapitalize: 'off' }),
    focus: s.focusMode ? focusPlugin : [],
    type: s.typewriter ? typewriter : [],
    theme: EditorView.theme({ '&': { fontSize: (s.editorFontSize || 15) + 'px' }, '.cm-scroller': { fontFamily: s.editorFont || 'var(--font-mono)' }, '.cm-content': { maxWidth: s.editorMaxWidth ? s.editorMaxWidth + 'ch' : 'none', fontWeight: '400' } }),
    tab: EditorState.tabSize.of(s.tabSize || 4),
  };
}

/**
 * Düzenleyici oluşturur. handlers: { onChange(view), onScroll(view), onSelection(view), onPaste(e, view), onDrop(e, view), keymap: [...] }
 */
export function createEditor(parent, settings, handlers) {
  const o = opt(settings);
  const state = (doc) => EditorState.create({
    doc,
    extensions: [
      highlightSpecialChars(), history(), drawSelection(), dropCursor(), EditorState.allowMultipleSelections.of(true),
      indentOnInput(), bracketMatching(), closeBrackets(), rectangularSelection(), crosshairCursor(), highlightActiveLine(),
      highlightSelectionMatches(), codeFolding(), search({ top: true }),
      markdown({ base: markdownLanguage, codeLanguages: languages, addKeymap: true }),
      syntaxHighlighting(mdStyle), gkTheme,
      cNumbers.of(o.numbers), cWrap.of(o.wrap), cSpell.of(o.spell), cFocus.of(o.focus), cType.of(o.type), cTheme.of(o.theme), cTab.of(o.tab), cPlace.of([]),
      keymap.of([
        ...(handlers.keymap || []),
        { key: 'Alt-ArrowUp', run: moveLineUp }, { key: 'Alt-ArrowDown', run: moveLineDown }, { key: 'Shift-Alt-ArrowDown', run: copyLineDown },
        { key: 'Mod-d', run: selectNextOccurrence, preventDefault: true }, { key: 'Mod-/', run: toggleComment },
        ...closeBracketsKeymap, ...searchKeymap, ...historyKeymap, ...foldKeymap, indentWithTab, ...defaultKeymap,
      ]),
      EditorView.updateListener.of((u) => {
        if (u.docChanged) handlers.onChange?.(u.view, u);
        if (u.selectionSet || u.docChanged) handlers.onSelection?.(u.view);
      }),
      EditorView.domEventHandlers({
        scroll: (e, v) => handlers.onScroll?.(v),
        paste: (e, v) => handlers.onPaste?.(e, v),
        drop: (e, v) => handlers.onDrop?.(e, v),
      }),
    ],
  });
  const view = new EditorView({ parent, state: state('') });
  view.newState = state;
  return view;
}

export function reconfigure(view, settings) {
  const o = opt(settings);
  view.dispatch({ effects: [cWrap.reconfigure(o.wrap), cNumbers.reconfigure(o.numbers), cSpell.reconfigure(o.spell),
    cFocus.reconfigure(o.focus), cType.reconfigure(o.type), cTheme.reconfigure(o.theme), cTab.reconfigure(o.tab)] });
}

export function setPlaceholder(view, text) { view.dispatch({ effects: cPlace.reconfigure(text ? placeholder(text) : []) }); }

// ===================== Biçimlendirme komutları =====================

/** Seçimi işaretle sarar ya da işareti kaldırır (ör. **kalın**). */
export function wrap(view, left, right = left, sample = '') {
  const { state } = view;
  view.dispatch(state.changeByRange((r) => {
    const text = state.sliceDoc(r.from, r.to);
    const before = state.sliceDoc(r.from - left.length, r.from), after = state.sliceDoc(r.to, r.to + right.length);
    if (before === left && after === right) { // kaldır
      return { changes: [{ from: r.from - left.length, to: r.from }, { from: r.to, to: r.to + right.length }], range: EditorSelection.range(r.from - left.length, r.to - left.length) };
    }
    if (text.startsWith(left) && text.endsWith(right) && text.length >= left.length + right.length) {
      return { changes: { from: r.from, to: r.to, insert: text.slice(left.length, text.length - right.length) }, range: EditorSelection.range(r.from, r.to - left.length - right.length) };
    }
    const body = text || sample;
    return { changes: { from: r.from, to: r.to, insert: left + body + right }, range: EditorSelection.range(r.from + left.length, r.from + left.length + body.length) };
  }));
  view.focus();
  return true;
}

function selectedLines(state) {
  const lines = new Set();
  for (const r of state.selection.ranges) {
    for (let l = state.doc.lineAt(r.from).number; l <= state.doc.lineAt(r.to).number; l++) lines.add(l);
  }
  return [...lines].sort((a, b) => a - b).map((n) => state.doc.line(n));
}

/** Satır başı işaretini (ör. "> ", "- ", "## ") ekler, kaldırır ya da değiştirir. */
export function linePrefix(view, prefix, { numbered = false, exclusive = /^(#{1,6} |> |[-*+] \[[ xX]\] |[-*+] |\d+[.)] )/ } = {}) {
  const { state } = view;
  const lines = selectedLines(state);
  const all = lines.every((l) => (numbered ? /^\d+[.)] /.test(l.text) : l.text.startsWith(prefix)));
  const changes = lines.map((l, i) => {
    const m = exclusive.exec(l.text);
    const cur = m ? m[0] : '';
    const want = all ? '' : numbered ? `${i + 1}. ` : prefix;
    return { from: l.from, to: l.from + cur.length, insert: want };
  });
  view.dispatch({ changes });
  view.focus();
  return true;
}

export const heading = (view, level) => linePrefix(view, level ? '#'.repeat(level) + ' ' : '', { exclusive: /^#{1,6} / });

/** İmleç işareti (metinde geçmeyen karakter): bir tane → imleç, iki tane → aradaki seçilir. */
export const CARET = '‸';

/** İmleçte metin ekler; {sel} seçili metin yerine geçer, CARET imleç konumudur. */
export function insertSnippet(view, tpl, { block = false } = {}) {
  const { state } = view;
  view.dispatch(state.changeByRange((r) => {
    let text = tpl.replace('{sel}', state.sliceDoc(r.from, r.to));
    let pre = '', post = '';
    if (block) {
      const line = state.doc.lineAt(r.from);
      if (line.text.slice(0, r.from - line.from).trim() !== '') pre = '\n\n';
      else if (line.number > 1 && state.doc.line(line.number - 1).text.trim() !== '') pre = '\n';
      post = '\n';
    }
    text = pre + text + post;
    let caret = text.indexOf(CARET), end = caret;
    const sel2 = text.indexOf(CARET, caret + 1);
    if (sel2 >= 0) { end = sel2 - 1; text = text.slice(0, sel2) + text.slice(sel2 + 1); }
    if (caret >= 0) text = text.slice(0, caret) + text.slice(caret + 1); else { caret = end = text.length; }
    return { changes: { from: r.from, to: r.to, insert: text }, range: EditorSelection.range(r.from + caret, r.from + end) };
  }));
  view.focus();
  return true;
}

export function selectedText(view) { const r = view.state.selection.main; return view.state.sliceDoc(r.from, r.to); }

/** Belgedeki bir satırı değiştirir (önizlemedeki görev kutusu için). */
export function toggleTask(view, line) {
  const l = view.state.doc.line(line + 1);
  const m = /^(\s*(?:[-*+]|\d+[.)])\s+\[)([ xX])(\])/.exec(l.text);
  if (!m) return;
  const pos = l.from + m[1].length;
  view.dispatch({ changes: { from: pos, to: pos + 1, insert: m[2] === ' ' ? 'x' : ' ' } });
}

export function gotoLine(view, line, col = 0) {
  const l = view.state.doc.line(Math.max(1, Math.min(view.state.doc.lines, line)));
  view.dispatch({ selection: { anchor: Math.min(l.to, l.from + col) }, effects: EditorView.scrollIntoView(l.from, { y: 'center' }) });
  view.focus();
}

export { EditorView, EditorSelection };
