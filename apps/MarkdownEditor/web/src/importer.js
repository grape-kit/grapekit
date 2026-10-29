// HTML / Word → Markdown (içe aktarma ve biçimli yapıştırma).
import TurndownService from 'turndown';
import { gfm } from 'turndown-plugin-gfm';

let td = null;
function service() {
  if (td) return td;
  td = new TurndownService({ headingStyle: 'atx', hr: '---', bulletListMarker: '-', codeBlockStyle: 'fenced', emDelimiter: '*', strongDelimiter: '**', linkStyle: 'inlined' });
  td.use(gfm);
  td.remove(['script', 'style', 'meta', 'link', 'noscript', 'iframe', 'object', 'embed', 'head', 'title']);
  // Kod bloğu dili: <pre><code class="language-js">
  td.addRule('fencedLang', {
    filter: (n) => n.nodeName === 'PRE' && n.firstChild && n.firstChild.nodeName === 'CODE',
    replacement: (_, n) => {
      const code = n.firstChild;
      const lang = (/(?:language|lang)-(\S+)/.exec(code.className || '') || [])[1] || '';
      const text = code.textContent.replace(/\n$/, '');
      const fence = text.includes('```') ? '~~~' : '```';
      return `\n\n${fence}${lang}\n${text}\n${fence}\n\n`;
    },
  });
  // Word'ün boş paragrafları ve gereksiz span'leri
  td.addRule('wordJunk', { filter: (n) => /^(O:P|W:.*)$/i.test(n.nodeName), replacement: () => '' });
  // Markdown Editörü'nün kendi HTML çıktısındaki ön bilgi kartı → YAML ön bilgi
  td.addRule('frontMatter', {
    filter: (n) => n.nodeName === 'DL' && /\bfront-matter\b/.test(n.className || ''),
    replacement: (_, n) => {
      const rows = [...n.querySelectorAll('div')].map((d) => {
        const dt = d.querySelector('dt');
        const k = dt?.getAttribute('data-key') || dt?.textContent.trim().toLowerCase();
        const v = d.querySelector('dd')?.textContent.trim() ?? '';
        return k ? `${k}: ${/[:#'"]/.test(v) ? JSON.stringify(v) : v}` : '';
      }).filter(Boolean);
      return rows.length ? `---\n${rows.join('\n')}\n---\n\n` : '';
    },
  });
  td.addRule('mark', { filter: ['mark'], replacement: (c) => (c.trim() ? `==${c}==` : c) });
  td.addRule('sup', { filter: ['sup'], replacement: (c) => (c.trim() ? `^${c}^` : c) });
  td.addRule('sub', { filter: ['sub'], replacement: (c) => (c.trim() ? `~${c}~` : c) });
  // Word kalın/italik span'leri (style ile)
  td.addRule('styledSpan', {
    filter: (n) => n.nodeName === 'SPAN' && /font-weight:\s*(bold|[6-9]00)|font-style:\s*italic/i.test(n.getAttribute('style') || ''),
    replacement: (c, n) => {
      if (!c.trim()) return c;
      const st = n.getAttribute('style');
      let out = c;
      if (/font-style:\s*italic/i.test(st)) out = `*${out}*`;
      if (/font-weight:\s*(bold|[6-9]00)/i.test(st)) out = `**${out}**`;
      return out;
    },
  });
  return td;
}

/** HTML'i Markdown'a çevirir. */
export function htmlToMarkdown(html) {
  // Pano HTML'i: StartFragment/EndFragment arası
  const frag = /<!--StartFragment-->([\s\S]*)<!--EndFragment-->/.exec(html);
  const src = frag ? frag[1] : html;
  return service().turndown(src).replace(/\n{3,}/g, '\n\n').replace(/ /g, ' ').trim() + '\n';
}

/** .docx (base64) → Markdown */
export async function docxToMarkdown(b64) {
  const mammoth = (await import('mammoth/mammoth.browser.js')).default;
  const buf = Uint8Array.from(atob(b64), (c) => c.charCodeAt(0)).buffer;
  const { value } = await mammoth.convertToHtml({ arrayBuffer: buf }, {
    styleMap: ["p[style-name='Title'] => h1:fresh", "p[style-name='Subtitle'] => h2:fresh", "p[style-name='Quote'] => blockquote:fresh", "r[style-name='Code'] => code"],
    convertImage: mammoth.images.imgElement((img) => img.read('base64').then((d) => ({ src: `data:${img.contentType};base64,${d}` }))),
  });
  return htmlToMarkdown(value);
}

/** Yapıştırılan HTML gerçekten biçimli mi (yoksa düz metin olarak kalsın)? */
export function isRichHtml(html, plain) {
  if (!html) return false;
  if (/<(h[1-6]|table|ul|ol|li|strong|b|em|i|a |img|pre|blockquote)[\s>]/i.test(html)) {
    // Kod düzenleyicilerden gelen tek renkli <pre>/<span> HTML'leri düz metin kalsın
    if (/^\s*(<meta[^>]*>)?\s*<(div|pre)[^>]*style="[^"]*(monospace|Consolas|Menlo)/i.test(html)) return false;
    return true;
  }
  return false;
}
