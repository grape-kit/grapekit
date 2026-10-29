// Markdown → Word (.docx). markdown-it belirteçleri doğrudan Word öğelerine çevrilir.
// Formüller ve diyagramlar kabukta (Chromium) görsele çevrilip yerleştirilir.
import {
  Document, Packer, Paragraph, TextRun, HeadingLevel, ExternalHyperlink, InternalHyperlink, ImageRun, Table, TableRow, TableCell,
  WidthType, BorderStyle, ShadingType, AlignmentType, LevelFormat, FootnoteReferenceRun, Bookmark, PageBreak,
} from 'docx';
import MarkdownIt from 'markdown-it';
import footnoteMod from 'markdown-it-footnote';
import { full as emoji } from 'markdown-it-emoji';
import subMod from 'markdown-it-sub';
import supMod from 'markdown-it-sup';
import markMod from 'markdown-it-mark';
import deflistMod from 'markdown-it-deflist';
import katexMod from '@vscode/markdown-it-katex';
const plug = (m) => (typeof m === 'function' ? m : m?.default ?? m);
const katexPlugin = plug(katexMod);
const [footnote, sub, sup, mark, deflist] = [footnoteMod, subMod, supMod, markMod, deflistMod].map(plug);
import katex from 'katex';
import { splitFrontMatter, slugify } from './render.js';
import { resolveLocal } from './export.js';
import { call } from './bridge.js';
import { drawDiagrams } from './preview.js';

const FONT = 'Calibri', MONO = 'Consolas', ACCENT = '6D28D9', MAXW = 600;
const md = new MarkdownIt({ html: true, linkify: true }).use(footnote).use(emoji).use(sub).use(sup).use(mark).use(deflist).use(katexPlugin, { throwOnError: false });

async function captureDoc(body) {
  const css = (await (await fetch('preview.css')).text()).replace(/url\((['"]?)(?:\.\/|\/)?fonts\//g, `url($1${location.origin}/fonts/`);
  return `<!doctype html><html><head><meta charset="utf-8"><style>${css}</style><style>body{margin:0;background:#fff}.cap{margin:0 0 8px}</style></head><body><div class="md md-theme-github">${body}</div></body></html>`;
}

const b64ToBytes = (b64) => Uint8Array.from(atob(b64), (c) => c.charCodeAt(0));

function pngSize(bytes) { // PNG başlığından genişlik/yükseklik
  const v = new DataView(bytes.buffer, bytes.byteOffset);
  return bytes[0] === 0x89 ? { w: v.getUint32(16), h: v.getUint32(20) } : null;
}
function imgSize(bytes, mime) {
  if (mime === 'image/png') return pngSize(bytes);
  if (mime === 'image/jpeg') {
    for (let i = 2; i < bytes.length - 9;) {
      if (bytes[i] !== 0xff) { i++; continue; }
      const m = bytes[i + 1], len = (bytes[i + 2] << 8) | bytes[i + 3];
      if (m >= 0xc0 && m <= 0xc3) return { h: (bytes[i + 5] << 8) | bytes[i + 6], w: (bytes[i + 7] << 8) | bytes[i + 8] };
      i += 2 + len;
    }
  }
  if (mime === 'image/gif') return { w: bytes[6] | (bytes[7] << 8), h: bytes[8] | (bytes[9] << 8) };
  return { w: 400, h: 300 };
}
const fit = ({ w, h }, max = MAXW, dpr = 1) => { w /= dpr; h /= dpr; const k = Math.min(1, max / w); return { width: Math.round(w * k), height: Math.round(h * k) }; };
const typeOf = (mime) => ({ 'image/png': 'png', 'image/jpeg': 'jpg', 'image/gif': 'gif', 'image/bmp': 'bmp' })[mime] || 'png';

/**
 * Word belgesi üretir. o: { docDir, title, lang } → base64
 */
export async function buildDocx(src, o = {}) {
  const { meta, body } = splitFrontMatter(src);
  const env = {};
  const tokens = md.parse(body, env);

  // 1) Formül ve diyagramları görsele çevir (tek seferde)
  const caps = [];
  for (const tk of tokens) {
    if (tk.type === 'math_block' || (tk.type === 'fence' && /^(math|latex|katex)$/i.test(tk.info.trim()))) caps.push({ kind: 'math', display: true, src: tk.content, tk });
    else if (tk.type === 'fence' && /^mermaid$/i.test(tk.info.trim())) caps.push({ kind: 'mermaid', src: tk.content, tk });
    else if (tk.type === 'inline') for (const c of tk.children || []) if (c.type === 'math_inline') caps.push({ kind: 'math', display: false, src: c.content, tk: c });
  }
  if (caps.length) {
    let html = '';
    caps.forEach((c, i) => {
      if (c.kind === 'math') html += `<div class="cap" id="cap${i}" style="display:inline-block;padding:2px;background:#fff">${katex.renderToString(c.src, { displayMode: c.display, throwOnError: false, output: 'html' })}</div><br>`;
      else html += `<div class="cap mermaid-box" id="cap${i}" style="display:inline-block;background:#fff;padding:8px"><pre class="mermaid-src" hidden>${c.src.replace(/</g, '&lt;')}</pre><div class="mermaid-out"></div></div><br>`;
    });
    // Diyagramlar burada çizilir; kabuk yalnızca görüntüsünü alır
    const box = document.createElement('div');
    box.className = 'md md-theme-github';
    box.style.cssText = 'position:fixed;left:-20000px;top:0;width:900px;background:#fff';
    box.innerHTML = html;
    document.body.append(box);
    try { await drawDiagrams(box, false); } finally { box.remove(); }
    for (const s of box.querySelectorAll('.mermaid-src')) s.remove();
    const res = await call('render.capture', { html: await captureDoc(box.innerHTML), count: caps.length });
    (res || []).forEach((png, i) => { if (png) { const bytes = b64ToBytes(png.data); caps[i].tk._img = { bytes, size: fit({ w: png.w, h: png.h }, MAXW, png.dpr || 2) }; } });
  }

  // 2) Görseller
  const images = new Map();
  for (const tk of tokens) for (const c of tk.children || []) if (c.type === 'image') {
    const s = c.attrGet('src');
    if (!s || /^https?:/i.test(s) || images.has(s)) continue;
    try {
      const r = s.startsWith('data:') ? { mime: /^data:([^;]+)/.exec(s)[1], data: s.split(',')[1] } : await call('file.readBase64', { path: resolveLocal(o.docDir, s) });
      if (r) { const bytes = b64ToBytes(r.data); images.set(s, { bytes, mime: r.mime, size: fit(imgSize(bytes, r.mime) || { w: 400, h: 300 }) }); }
    } catch { /* bulunamadı */ }
  }

  // 3) Dipnotlar
  const footnotes = {};
  const fnIndex = new Map();
  let fnSeq = 0;

  // 4) Satır içi → TextRun listesi
  function runs(children, base = {}) {
    const out = [];
    const st = { ...base };
    let link = null;
    const push = (r) => { if (link) link.children.push(r); else out.push(r); };
    for (const c of children || []) {
      switch (c.type) {
        case 'text': if (c.content) push(new TextRun({ text: c.content, bold: st.b, italics: st.i, strike: st.s, highlight: st.mark ? 'yellow' : undefined, superScript: st.sup, subScript: st.sub, font: st.mono ? MONO : undefined, style: link ? 'Hyperlink' : undefined, color: st.color })); break;
        case 'emoji': push(new TextRun({ text: c.content })); break;
        case 'strong_open': st.b = true; break; case 'strong_close': st.b = base.b; break;
        case 'em_open': st.i = true; break; case 'em_close': st.i = base.i; break;
        case 's_open': st.s = true; break; case 's_close': st.s = false; break;
        case 'mark_open': st.mark = true; break; case 'mark_close': st.mark = false; break;
        case 'sup_open': st.sup = true; break; case 'sup_close': st.sup = false; break;
        case 'sub_open': st.sub = true; break; case 'sub_close': st.sub = false; break;
        case 'code_inline': push(new TextRun({ text: c.content, font: MONO, size: 20, shading: { type: ShadingType.CLEAR, fill: 'F1EDF8', color: 'auto' }, color: '6D28D9' })); break;
        case 'softbreak': push(new TextRun({ text: ' ' })); break;
        case 'hardbreak': push(new TextRun({ break: 1 })); break;
        case 'link_open': {
          const href = c.attrGet('href') || '';
          link = href.startsWith('#') ? { internal: href.slice(1), children: [] } : { href, children: [] };
          break;
        }
        case 'link_close':
          if (link) {
            const l = link; link = null;
            if (l.internal) out.push(new InternalHyperlink({ anchor: l.internal, children: l.children }));
            else if (/^(https?:|mailto:)/i.test(l.href)) out.push(new ExternalHyperlink({ link: l.href, children: l.children }));
            else out.push(...l.children);
          }
          break;
        case 'image': {
          const im = images.get(c.attrGet('src'));
          if (im) push(new ImageRun({ type: typeOf(im.mime), data: im.bytes, transformation: im.size, altText: { title: c.content || 'image', description: c.content || '', name: c.content || 'image' } }));
          else push(new TextRun({ text: `[${c.content || 'görsel'}]`, italics: true, color: '888888' }));
          break;
        }
        case 'math_inline':
          if (c._img) push(new ImageRun({ type: 'png', data: c._img.bytes, transformation: c._img.size }));
          else push(new TextRun({ text: c.content, italics: true, font: 'Cambria Math' }));
          break;
        case 'footnote_ref': {
          const id = c.meta.id;
          if (!fnIndex.has(id)) fnIndex.set(id, ++fnSeq);
          push(new FootnoteReferenceRun(fnIndex.get(id)));
          break;
        }
        case 'html_inline': {
          if (/type="checkbox"/.test(c.content)) push(new TextRun({ text: /checked/.test(c.content) ? '☑ ' : '☐ ', font: 'Segoe UI Symbol' }));
          else if (/^<br\s*\/?>$/i.test(c.content)) push(new TextRun({ break: 1 }));
          break;
        }
        default: break;
      }
    }
    return out;
  }

  // 5) Bloklar
  const children = [];
  const lists = []; // { ordered, instance }
  let listInstance = 0;
  let quote = 0;
  let pendingTask = null;
  const HEAD = [HeadingLevel.HEADING_1, HeadingLevel.HEADING_2, HeadingLevel.HEADING_3, HeadingLevel.HEADING_4, HeadingLevel.HEADING_5, HeadingLevel.HEADING_6];

  const paraProps = () => {
    const p = {};
    if (lists.length) {
      const l = lists[lists.length - 1];
      if (!l.used) { p.numbering = { reference: l.ordered ? 'numbers' : 'bullets', level: Math.min(lists.length - 1, 8), instance: l.instance }; l.used = true; }
      else p.indent = { left: 720 * lists.length };
    }
    if (quote) { p.indent = { left: 567 * quote }; p.border = { left: { style: BorderStyle.SINGLE, size: 18, color: 'C4B5FD', space: 12 } }; }
    return p;
  };

  // Dipnot içeriklerini topla (ayrı bölümde gelir)
  let inFootnote = null;
  for (let i = 0; i < tokens.length; i++) {
    const tk = tokens[i];
    if (tk.type === 'footnote_open') { inFootnote = tk.meta.id; if (!fnIndex.has(inFootnote)) fnIndex.set(inFootnote, ++fnSeq); footnotes[fnIndex.get(inFootnote)] = { children: [] }; continue; }
    if (tk.type === 'footnote_close') { inFootnote = null; continue; }
    if (tk.type === 'footnote_block_open' || tk.type === 'footnote_block_close' || tk.type === 'footnote_anchor') continue;
    const target = inFootnote != null ? footnotes[fnIndex.get(inFootnote)].children : children;

    switch (tk.type) {
      case 'heading_open': {
        const inline = tokens[i + 1];
        const text = inline.content;
        const id = slugify(text);
        target.push(new Paragraph({ heading: HEAD[+tk.tag.slice(1) - 1], children: [new Bookmark({ id, children: runs(inline.children) })] }));
        i += 2; break;
      }
      case 'paragraph_open': {
        const inline = tokens[i + 1];
        const only = inline.children?.length === 1 && inline.children[0];
        if (only && only.type === 'html_inline' && /pagebreak|page-break/i.test(only.content)) { target.push(new Paragraph({ children: [new PageBreak()] })); i += 2; break; }
        target.push(new Paragraph({ ...paraProps(), spacing: { after: lists.length ? 60 : 160 }, children: runs(inline.children, { i: quote ? true : undefined }) }));
        i += 2; break;
      }
      case 'bullet_list_open': lists.push({ ordered: false, instance: ++listInstance }); break;
      case 'ordered_list_open': lists.push({ ordered: true, instance: ++listInstance }); break;
      case 'bullet_list_close': case 'ordered_list_close': lists.pop(); break;
      case 'list_item_open': if (lists.length) lists[lists.length - 1].used = false; break;
      case 'blockquote_open': quote++; break;
      case 'blockquote_close': quote--; break;
      case 'hr': target.push(new Paragraph({ border: { bottom: { style: BorderStyle.SINGLE, size: 6, color: 'D9D3E8', space: 1 } }, spacing: { after: 200 } })); break;
      case 'fence': case 'code_block': case 'math_block': {
        if (tk._img) { target.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { before: 120, after: 160 }, children: [new ImageRun({ type: 'png', data: tk._img.bytes, transformation: tk._img.size })] })); break; }
        const lines = tk.content.replace(/\n$/, '').split('\n');
        lines.forEach((ln, k) => target.push(new Paragraph({
          shading: { type: ShadingType.CLEAR, fill: 'F6F4FB', color: 'auto' }, spacing: { after: k === lines.length - 1 ? 160 : 0, line: 260 },
          border: k === 0 ? { top: { style: BorderStyle.SINGLE, size: 4, color: 'E7E1F3', space: 4 } } : k === lines.length - 1 ? { bottom: { style: BorderStyle.SINGLE, size: 4, color: 'E7E1F3', space: 4 } } : undefined,
          indent: lists.length ? { left: 720 * lists.length } : undefined,
          children: [new TextRun({ text: ln || ' ', font: MONO, size: 19 })],
        })));
        break;
      }
      case 'html_block': {
        if (/pagebreak|page-break/i.test(tk.content)) target.push(new Paragraph({ children: [new PageBreak()] }));
        break;
      }
      case 'table_open': {
        const rows = []; let row = null; let header = false; const aligns = [];
        let j = i + 1;
        for (; j < tokens.length && tokens[j].type !== 'table_close'; j++) {
          const x = tokens[j];
          if (x.type === 'thead_open') header = true; else if (x.type === 'thead_close') header = false;
          else if (x.type === 'tr_open') row = { cells: [], header };
          else if (x.type === 'tr_close') rows.push(row);
          else if (x.type === 'th_open' || x.type === 'td_open') {
            const style = x.attrGet('style') || '';
            const al = /center/.test(style) ? AlignmentType.CENTER : /right/.test(style) ? AlignmentType.RIGHT : AlignmentType.LEFT;
            if (row.header) aligns.push(al);
            row.cells.push({ inline: tokens[j + 1], al });
          }
        }
        const cols = Math.max(...rows.map((r) => r.cells.length));
        target.push(new Table({
          width: { size: 100, type: WidthType.PERCENTAGE },
          rows: rows.map((r) => new TableRow({
            tableHeader: r.header,
            children: Array.from({ length: cols }, (_, k) => {
              const c = r.cells[k];
              return new TableCell({
                shading: r.header ? { type: ShadingType.CLEAR, fill: 'F1EAFE', color: 'auto' } : undefined,
                margins: { top: 60, bottom: 60, left: 100, right: 100 },
                children: [new Paragraph({ alignment: c?.al, children: c ? runs(c.inline.children, { b: r.header }) : [] })],
              });
            }),
          })),
        }));
        target.push(new Paragraph({ spacing: { after: 120 } }));
        i = j; break;
      }
      case 'dt_open': target.push(new Paragraph({ children: runs(tokens[i + 1].children, { b: true }), spacing: { after: 40 } })); i += 2; break;
      case 'dd_open': {
        // dd içinde paragraf gelir: girintili yaz
        let j = i + 1;
        while (tokens[j] && tokens[j].type !== 'dd_close') {
          if (tokens[j].type === 'inline') target.push(new Paragraph({ indent: { left: 567 }, spacing: { after: 120 }, children: runs(tokens[j].children) }));
          j++;
        }
        i = j; break;
      }
      default: break;
    }
  }

  // Başlık kartı (ön bilgi)
  if (meta && (meta.title || meta.author)) {
    const head = [];
    if (meta.title) head.push(new Paragraph({ heading: HeadingLevel.TITLE, children: [new TextRun({ text: String(meta.title) })] }));
    const sub2 = [meta.author, meta.date instanceof Date ? meta.date.toISOString().slice(0, 10) : meta.date].filter(Boolean).join(' · ');
    if (sub2) head.push(new Paragraph({ spacing: { after: 300 }, children: [new TextRun({ text: sub2, color: '6B6478' })] }));
    children.unshift(...head);
  }

  const level = (i, fmt, text) => ({ level: i, format: fmt, text, alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 720 * (i + 1), hanging: 360 } } } });
  const doc = new Document({
    creator: 'Markdown Editörü (Grapekit)', title: o.title || meta?.title || '', description: '',
    styles: {
      default: { document: { run: { font: FONT, size: 22 }, paragraph: { spacing: { line: 300 } } } },
      paragraphStyles: [
        { id: 'Title', name: 'Title', basedOn: 'Normal', next: 'Normal', run: { size: 52, bold: true, color: '1A0B33' }, paragraph: { spacing: { after: 120 } } },
        { id: 'Heading1', name: 'Heading 1', basedOn: 'Normal', next: 'Normal', quickFormat: true, run: { size: 36, bold: true, color: '1A0B33' }, paragraph: { spacing: { before: 360, after: 160 }, border: { bottom: { style: BorderStyle.SINGLE, size: 4, color: 'E7E1F3', space: 4 } } } },
        { id: 'Heading2', name: 'Heading 2', basedOn: 'Normal', next: 'Normal', quickFormat: true, run: { size: 30, bold: true, color: '1A0B33' }, paragraph: { spacing: { before: 300, after: 120 } } },
        { id: 'Heading3', name: 'Heading 3', basedOn: 'Normal', next: 'Normal', quickFormat: true, run: { size: 26, bold: true, color: '1A0B33' }, paragraph: { spacing: { before: 240, after: 100 } } },
        { id: 'Heading4', name: 'Heading 4', basedOn: 'Normal', next: 'Normal', quickFormat: true, run: { size: 23, bold: true }, paragraph: { spacing: { before: 200, after: 80 } } },
        { id: 'Heading5', name: 'Heading 5', basedOn: 'Normal', next: 'Normal', run: { size: 22, bold: true, color: '4B3F66' } },
        { id: 'Heading6', name: 'Heading 6', basedOn: 'Normal', next: 'Normal', run: { size: 22, italics: true, color: '4B3F66' } },
      ],
      characterStyles: [{ id: 'Hyperlink', name: 'Hyperlink', run: { color: ACCENT, underline: { type: 'single', color: ACCENT } } }],
    },
    numbering: {
      config: [
        { reference: 'bullets', levels: Array.from({ length: 9 }, (_, i) => level(i, LevelFormat.BULLET, ['•', '◦', '▪'][i % 3])) },
        { reference: 'numbers', levels: Array.from({ length: 9 }, (_, i) => level(i, [LevelFormat.DECIMAL, LevelFormat.LOWER_LETTER, LevelFormat.LOWER_ROMAN][i % 3], `%${i + 1}.`)) },
      ],
    },
    footnotes,
    sections: [{ properties: { page: { margin: { top: 1134, bottom: 1134, left: 1134, right: 1134 } } }, children }],
  });
  return Packer.toBase64String(doc);
}
