// Markdown → güvenli HTML. GitHub uyumlu sözdizimi + matematik + diyagram + uyarı kutuları + görev listeleri + içindekiler.
// Her blok öğeye kaynak satırı (data-line) yazılır: senkron kaydırma, görev kutusu tıklama ve PDF bölümleri bunu kullanır.
import MarkdownIt from 'markdown-it';
import footnoteMod from 'markdown-it-footnote';
import { full as emoji } from 'markdown-it-emoji';
import subMod from 'markdown-it-sub';
import supMod from 'markdown-it-sup';
import markMod from 'markdown-it-mark';
import deflistMod from 'markdown-it-deflist';
import anchorMod from 'markdown-it-anchor';
import katexMod from '@vscode/markdown-it-katex';
const plug = (m) => (typeof m === 'function' ? m : m?.default ?? m);
const katexPlugin = plug(katexMod);
const anchor = plug(anchorMod);
const [footnote, sub, sup, mark, deflist] = [footnoteMod, subMod, supMod, markMod, deflistMod].map(plug);
import hljs from 'highlight.js';
import DOMPurify from 'dompurify';
import * as yaml from 'js-yaml';

/** Türkçe karakterleri koruyan başlık kimliği: "Özel Başlık!" → "özel-başlık" */
export function slugify(s) {
  return String(s).trim().toLocaleLowerCase('tr').replace(/<[^>]+>/g, '').replace(/[^\p{L}\p{N}\s-]/gu, '').replace(/\s+/g, '-').replace(/-+/g, '-');
}

const esc = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
const ALERTS = { NOTE: 'note', TIP: 'tip', IMPORTANT: 'important', WARNING: 'warning', CAUTION: 'caution' };

// Ön bilgi kartındaki bilinen alanların görünen adları (dile göre setMetaLabels ile değişir)
let metaLabels = { title: 'Başlık', author: 'Yazar', date: 'Tarih', tags: 'Etiketler', description: 'Açıklama', subtitle: 'Alt başlık', version: 'Sürüm', lang: 'Dil', category: 'Kategori', keywords: 'Anahtar kelimeler' };
export function setMetaLabels(m) { metaLabels = m; }

let alertTitles = { note: 'Not', tip: 'İpucu', important: 'Önemli', warning: 'Uyarı', caution: 'Dikkat' };
export function setAlertTitles(t) { alertTitles = t; }

function create(opts) {
  const md = new MarkdownIt({
    html: true, linkify: true, typographer: false, breaks: !!opts.breaks,
    highlight(code, lang) {
      const l = (lang || '').trim().split(/\s+/)[0].toLowerCase();
      if (l && hljs.getLanguage(l)) {
        try { return hljs.highlight(code, { language: l, ignoreIllegals: true }).value; } catch { /* düz metin */ }
      }
      return esc(code);
    },
  });
  md.use(footnote).use(emoji).use(sub).use(sup).use(mark).use(deflist)
    .use(katexPlugin, { throwOnError: false, output: 'htmlAndMathml' })
    .use(anchor, { slugify, tabIndex: false });
  md.linkify.set({ fuzzyEmail: false });

  // Kaynak satırı: her blok açılış etiketine data-line
  md.core.ruler.push('source_line', (state) => {
    for (const t of state.tokens) {
      if (t.map && t.nesting >= 0 && t.type !== 'inline' && t.level < 30) t.attrSet('data-line', String(t.map[0]));
    }
  });

  // Görev listeleri: "- [ ] iş" / "- [x] iş"
  md.core.ruler.after('inline', 'task_lists', (state) => {
    const tk = state.tokens;
    for (let i = 2; i < tk.length; i++) {
      if (tk[i].type !== 'inline' || tk[i - 1].type !== 'paragraph_open' || tk[i - 2].type !== 'list_item_open') continue;
      const m = /^\[([ xX])\][  ]/.exec(tk[i].content);
      if (!m) continue;
      const checked = m[1] !== ' ';
      const first = tk[i].children[0];
      if (first?.type === 'text') first.content = first.content.slice(4);
      const box = new state.Token('html_inline', '', 0);
      box.content = `<input type="checkbox" class="task" data-line="${tk[i].map?.[0] ?? tk[i - 2].map?.[0]}"${checked ? ' checked' : ''}> `;
      tk[i].children.unshift(box);
      tk[i - 2].attrJoin('class', 'task-item');
      // Listeyi işaretle
      for (let j = i - 3; j >= 0; j--) {
        if ((tk[j].type === 'bullet_list_open' || tk[j].type === 'ordered_list_open') && tk[j].level === tk[i - 2].level - 1) { tk[j].attrJoin('class', 'task-list'); break; }
      }
    }
  });

  // Uyarı kutuları: > [!NOTE]
  md.core.ruler.after('block', 'alerts', (state) => {
    const tk = state.tokens;
    for (let i = 0; i < tk.length - 2; i++) {
      if (tk[i].type !== 'blockquote_open' || tk[i + 1].type !== 'paragraph_open' || tk[i + 2].type !== 'inline') continue;
      const m = /^\[!(NOTE|TIP|IMPORTANT|WARNING|CAUTION)\]\s*(?:\n|$)/i.exec(tk[i + 2].content);
      if (!m) continue;
      const kind = ALERTS[m[1].toUpperCase()];
      tk[i].attrJoin('class', 'alert alert-' + kind);
      tk[i + 2].content = tk[i + 2].content.slice(m[0].length);
      const title = new state.Token('html_block', '', 0);
      title.content = `<p class="alert-title">${esc(alertTitles[kind])}</p>\n`;
      tk.splice(i + 1, 0, title);
    }
  });

  // Kod blokları: dil etiketi + kopyala; mermaid ayrı
  const fence = md.renderer.rules.fence;
  md.renderer.rules.fence = (tokens, idx, o, env, self) => {
    const t = tokens[idx];
    const lang = (t.info || '').trim().split(/\s+/)[0].toLowerCase();
    const line = t.map ? t.map[0] : '';
    if (lang === 'mermaid') return `<div class="mermaid-box" data-line="${line}"><pre class="mermaid-src" hidden>${esc(t.content)}</pre><div class="mermaid-out"></div></div>\n`;
    if (lang === 'math' || lang === 'latex' || lang === 'katex') return `<div class="math-block" data-line="${line}" data-tex="${esc(t.content)}"></div>\n`;
    const html = fence(tokens, idx, o, env, self);
    return `<div class="code-box" data-line="${line}">${lang ? `<span class="code-lang">${esc(lang)}</span>` : ''}<button class="code-copy" type="button" data-copy></button>${html}</div>\n`;
  };

  // Sayfa sonu
  md.renderer.rules.html_block = ((orig) => (tokens, idx, o, env, self) => {
    const c = tokens[idx].content.trim();
    if (/^<!--\s*(pagebreak|page-break|sayfa\s*sonu)\s*-->$/i.test(c)) return `<div class="page-break" data-line="${tokens[idx].map?.[0] ?? ''}"></div>\n`;
    return orig ? orig(tokens, idx, o, env, self) : tokens[idx].content;
  })(md.renderer.rules.html_block);

  // Tablolar kaydırılabilir kutuda
  md.renderer.rules.table_open = (tokens, idx, o, env, self) => `<div class="table-box">${self.renderToken(tokens, idx, o)}`;
  md.renderer.rules.table_close = (tokens, idx, o, env, self) => `${self.renderToken(tokens, idx, o)}</div>`;
  return md;
}

let mdCache = null, mdKey = '';
function engine(opts) {
  const k = JSON.stringify(opts);
  if (k !== mdKey) { mdCache = create(opts); mdKey = k; }
  return mdCache;
}

/** Ön bilgiyi (YAML) ayırır. */
export function splitFrontMatter(src) {
  const m = /^---[ \t]*\r?\n([\s\S]*?)\r?\n(?:---|\.\.\.)[ \t]*(?:\r?\n|$)/.exec(src);
  if (!m) return { meta: null, body: src, offset: 0 };
  let meta = null;
  try { meta = yaml.load(m[1]); } catch { meta = { _error: true }; }
  if (meta && typeof meta !== 'object') meta = null;
  const lines = m[0].split('\n').length - 1;
  return { meta, body: '\n'.repeat(lines) + src.slice(m[0].length), offset: lines };
}

/** Başlıkları çıkarır: [{ level, text, id, line }] */
export function headings(src) {
  const { body } = splitFrontMatter(src);
  const md = engine({});
  const tokens = md.parse(body, {});
  const out = [];
  for (let i = 0; i < tokens.length; i++) {
    if (tokens[i].type === 'heading_open') {
      const text = tokens[i + 1].children.filter((c) => c.type === 'text' || c.type === 'code_inline' || c.type === 'emoji').map((c) => c.content).join('');
      out.push({ level: +tokens[i].tag.slice(1), text, id: tokens[i].attrGet('id') || slugify(text), line: tokens[i].map?.[0] ?? 0 });
    }
  }
  return out;
}

function tocHtml(hs) {
  if (!hs.length) return '';
  const min = Math.min(...hs.map((h) => h.level));
  return '<nav class="toc">' + hs.map((h) => `<a class="toc-l${h.level - min + 1}" href="#${esc(h.id)}">${esc(h.text)}</a>`).join('') + '</nav>';
}

function metaCard(meta) {
  if (!meta || meta._error) return '';
  const rows = Object.entries(meta).filter(([, v]) => v != null && typeof v !== 'object' || Array.isArray(v))
    .map(([k, v]) => `<div><dt>${esc(metaLabels[k.toLowerCase()] || k)}</dt><dd>${esc(Array.isArray(v) ? v.join(', ') : v)}</dd></div>`).join('');
  return rows ? `<dl class="front-matter" data-line="0">${rows}</dl>` : '';
}

DOMPurify.addHook('uponSanitizeElement', (node, data) => {
  if (data.tagName === 'input' && node.getAttribute?.('type') !== 'checkbox') node.remove?.();
});

/**
 * Markdown'ı HTML'e çevirir. opts: { breaks, docBase (göreli görseller için), remoteImages }
 * Dönüş: { html, meta, headings }
 */
export function render(src, opts = {}) {
  const { meta, body } = splitFrontMatter(src);
  const md = engine({ breaks: !!opts.breaks });
  let html = md.render(body, {});
  const hs = /\[TOC\]|\[\[_TOC_\]\]|\[İÇİNDEKİLER\]/i.test(body) ? headings(src) : null;
  if (hs) html = html.replace(/<p[^>]*>\s*(\[TOC\]|\[\[_TOC_\]\]|\[İÇİNDEKİLER\])\s*<\/p>/gi, tocHtml(hs));
  html = metaCard(meta) + html;
  html = DOMPurify.sanitize(html, {
    ADD_ATTR: ['data-line', 'data-tex', 'data-copy', 'target', 'checked', 'aria-hidden', 'encoding', 'xmlns', 'display', 'mathvariant'],
    ADD_TAGS: ['semantics', 'annotation', 'mrow', 'mi', 'mo', 'mn', 'msup', 'msub', 'mfrac', 'msqrt', 'mroot', 'mtext', 'mspace', 'mtable', 'mtr', 'mtd', 'mover', 'munder', 'munderover', 'msubsup', 'mstyle', 'mpadded', 'menclose', 'math'],
    FORBID_TAGS: ['style', 'form', 'iframe', 'object', 'embed', 'script', 'base', 'meta', 'link'],
    FORBID_ATTR: ['onerror', 'onload', 'onclick', 'formaction'],
    ALLOW_UNKNOWN_PROTOCOLS: false,
  });
  return { html, meta, headings: hs };
}

/** Sözcük ve okuma süresi (kod blokları ve işaretler hariç). */
export function stats(src) {
  const text = splitFrontMatter(src).body.replace(/```[\s\S]*?```/g, ' ').replace(/[#>*_`~\-|[\]()!]/g, ' ');
  const words = (text.match(/[\p{L}\p{N}][\p{L}\p{N}'’.-]*/gu) || []).length;
  return { words, chars: src.length, minutes: Math.max(1, Math.round(words / 200)) };
}
