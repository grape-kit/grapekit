// Markdown tablo düzenleme: imleç tablodayken hizalama, satır/sütun ekle-sil, sütun hizası, Tab ile hücre geçişi.
import { EditorSelection } from '@codemirror/state';

const isRow = (text) => /^\s*\|.*\|\s*$/.test(text) || /^\s*[^|`]+\|/.test(text) && text.includes('|');
const isSep = (text) => /^\s*\|?\s*:?-{1,}:?\s*(\|\s*:?-{1,}:?\s*)*\|?\s*$/.test(text);

/** İmlecin bulunduğu tabloyu bulur: { from, to, startLine, rows: [[hücre]], aligns, cursorRow, cursorCol } */
export function findTable(state, pos = state.selection.main.head) {
  const doc = state.doc;
  const cur = doc.lineAt(pos);
  if (!isRow(cur.text)) return null;
  let a = cur.number, b = cur.number;
  while (a > 1 && isRow(doc.line(a - 1).text)) a--;
  while (b < doc.lines && isRow(doc.line(b + 1).text)) b++;
  if (b - a < 1 || !isSep(doc.line(a + 1).text)) return null;
  const split = (text) => {
    let s = text.trim();
    if (s.startsWith('|')) s = s.slice(1);
    if (s.endsWith('|') && !s.endsWith('\\|')) s = s.slice(0, -1);
    const cells = []; let buf = '', code = false;
    for (let i = 0; i < s.length; i++) {
      const ch = s[i];
      if (ch === '`') code = !code;
      if (ch === '\\' && s[i + 1] === '|') { buf += '\\|'; i++; continue; }
      if (ch === '|' && !code) { cells.push(buf.trim()); buf = ''; } else buf += ch;
    }
    cells.push(buf.trim());
    return cells;
  };
  const lines = []; for (let n = a; n <= b; n++) lines.push(doc.line(n));
  const aligns = split(lines[1].text).map((c) => (c.startsWith(':') && c.endsWith(':') ? 'c' : c.endsWith(':') ? 'r' : c.startsWith(':') ? 'l' : ''));
  const rows = lines.filter((_, i) => i !== 1).map((l) => split(l.text));
  const cols = Math.max(aligns.length, ...rows.map((r) => r.length));
  for (const r of rows) while (r.length < cols) r.push('');
  while (aligns.length < cols) aligns.push('');
  // İmlecin hücresi
  const ln = cur.number - a;
  const before = cur.text.slice(0, pos - cur.from);
  const cursorCol = Math.max(0, (before.trim().startsWith('|') ? before.split('|').length - 2 : before.split('|').length - 1));
  return { from: lines[0].from, to: lines[lines.length - 1].to, startLine: a, rows, aligns, cursorRow: ln === 0 ? 0 : Math.max(0, ln - 1), onSep: ln === 1, cursorCol: Math.min(cols - 1, cursorCol) };
}

const width = (s) => [...s].length;

/** Tabloyu düzenli metne çevirir. */
export function formatTable({ rows, aligns }) {
  const cols = aligns.length;
  const w = Array.from({ length: cols }, (_, c) => Math.max(3, ...rows.map((r) => width(r[c] ?? ''))));
  const pad = (s, n, al) => {
    const d = n - width(s);
    if (al === 'r') return ' '.repeat(d) + s;
    if (al === 'c') return ' '.repeat(Math.floor(d / 2)) + s + ' '.repeat(Math.ceil(d / 2));
    return s + ' '.repeat(d);
  };
  const line = (r) => '| ' + r.map((c, i) => pad(c ?? '', w[i], aligns[i])).join(' | ') + ' |';
  const sep = '| ' + aligns.map((al, i) => (al === 'c' ? ':' + '-'.repeat(w[i] - 2) + ':' : al === 'r' ? '-'.repeat(w[i] - 1) + ':' : al === 'l' ? ':' + '-'.repeat(w[i] - 1) : '-'.repeat(w[i]))).join(' | ') + ' |';
  return [line(rows[0]), sep, ...rows.slice(1).map(line)].join('\n');
}

/** Tablo metninde (satır, sütun) hücresinin içerik başlangıç konumu. */
function cellOffset(text, row, col) {
  const lines = text.split('\n');
  const li = row === 0 ? 0 : row + 1;
  const l = lines[li];
  let idx = 0;
  for (let i = 0; i <= col; i++) idx = l.indexOf('|', idx) + 1;
  let off = 0; for (let i = 0; i < li; i++) off += lines[i].length + 1;
  return off + idx + 1;
}

function apply(view, tbl, row, col) {
  const text = formatTable(tbl);
  const pos = tbl.from + cellOffset(text, Math.min(row, tbl.rows.length - 1), Math.min(col, tbl.aligns.length - 1));
  view.dispatch({ changes: { from: tbl.from, to: tbl.to, insert: text }, selection: EditorSelection.cursor(pos) });
  view.focus();
  return true;
}

/** Tablo işlemleri. op: format | rowAbove | rowBelow | colLeft | colRight | delRow | delCol | alignL | alignC | alignR | next | prev */
export function tableOp(view, op) {
  const tbl = findTable(view.state);
  if (!tbl) return false;
  const cols = tbl.aligns.length;
  let { cursorRow: r, cursorCol: c } = tbl;
  const empty = () => Array(cols).fill('');
  switch (op) {
    case 'format': break;
    case 'rowAbove': tbl.rows.splice(Math.max(1, r), 0, empty()); r = Math.max(1, r); break;
    case 'rowBelow': tbl.rows.splice(r + 1, 0, empty()); r++; break;
    case 'colLeft': tbl.rows.forEach((x) => x.splice(c, 0, '')); tbl.aligns.splice(c, 0, ''); break;
    case 'colRight': tbl.rows.forEach((x) => x.splice(c + 1, 0, '')); tbl.aligns.splice(c + 1, 0, ''); c++; break;
    case 'delRow': if (tbl.rows.length > 2 && r > 0) { tbl.rows.splice(r, 1); r = Math.min(r, tbl.rows.length - 1); } break;
    case 'delCol': if (cols > 1) { tbl.rows.forEach((x) => x.splice(c, 1)); tbl.aligns.splice(c, 1); c = Math.min(c, cols - 2); } break;
    case 'alignL': tbl.aligns[c] = 'l'; break;
    case 'alignC': tbl.aligns[c] = 'c'; break;
    case 'alignR': tbl.aligns[c] = 'r'; break;
    case 'next':
      if (c < cols - 1) c++; else if (r < tbl.rows.length - 1) { r++; c = 0; } else { tbl.rows.push(empty()); r++; c = 0; }
      break;
    case 'prev':
      if (c > 0) c--; else if (r > 0) { r--; c = cols - 1; } else return true;
      break;
    default: return false;
  }
  return apply(view, tbl, r, c);
}

/** Yeni tablo metni. */
export function newTable(rows, cols, header = 'Başlık') {
  const head = Array.from({ length: cols }, (_, i) => `${header} ${i + 1}`);
  const body = Array.from({ length: Math.max(1, rows) }, () => Array(cols).fill(''));
  return formatTable({ rows: [head, ...body], aligns: Array(cols).fill('') });
}

export const inTable = (state) => !!findTable(state);
