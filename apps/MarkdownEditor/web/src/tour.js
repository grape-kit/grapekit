// İlk kullanım tanıtımı: arayüzün ana bölümlerini sırayla vurgular. Geçilebilir; Yardım menüsünden yeniden açılır.
import { t } from './i18n.js';
import { icon } from './icons.js';

const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

// Hedef görünmüyorsa (ör. kenar çubuğu kapalı) kart ortada açılır
const STEPS = [
  { key: 'write', sel: '.editor-pane', icon: 'pen' },
  { key: 'views', sel: '.tb-seg', icon: 'split' },
  { key: 'toolbar', sel: '.toolbar', icon: 'heading' },
  { key: 'export', sel: '.tb-export', icon: 'pdf' },
  { key: 'sidebar', sel: '.sidebar', icon: 'sidebar' },
  { key: 'palette', sel: null, icon: 'search' },
];

let active = null;

/** Turu başlatır. onDone: bitince ya da geçilince (bir kez) çağrılır. */
export function startTour(onDone) {
  if (active) return;
  let i = 0;
  const root = document.createElement('div');
  root.className = 'tour';
  root.innerHTML = '<div class="tour-hole"></div><div class="tour-card" role="dialog" aria-modal="true" aria-live="polite"></div>';
  document.body.append(root);
  const hole = root.querySelector('.tour-hole'), card = root.querySelector('.tour-card');
  const prevFocus = document.activeElement;

  const target = () => {
    const s = STEPS[i].sel && document.querySelector(STEPS[i].sel);
    const r = s?.getBoundingClientRect();
    return r && r.width > 4 && r.height > 4 && s.offsetParent !== null ? r : null;
  };

  function place() {
    const r = target();
    const pad = 6;
    if (r) {
      Object.assign(hole.style, { display: 'block', left: r.left - pad + 'px', top: r.top - pad + 'px', width: r.width + pad * 2 + 'px', height: r.height + pad * 2 + 'px' });
    } else hole.style.display = 'none';
    root.classList.toggle('tour-dim', !r);
    // Kart: hedefin altına, sığmazsa üstüne, o da olmazsa yanına; ekranda kalır
    const cw = card.offsetWidth, ch = card.offsetHeight, W = innerWidth, H = innerHeight, m = 14;
    let x, y;
    if (!r) { x = (W - cw) / 2; y = (H - ch) / 2; }
    else if (r.bottom + m + ch < H - 8) { x = r.left; y = r.bottom + m; }
    else if (r.top - m - ch > 8) { x = r.left; y = r.top - m - ch; }
    else { x = r.right + m + cw < W ? r.right + m : r.left - m - cw; y = r.top + (r.height - ch) / 2; }
    // Büyük alanlarda (düzenleyici, kenar çubuğu) kartı alanın içine, üst köşeye yakın koy
    if (r && r.height > H * 0.5) { x = r.width > cw + 40 ? r.left + 24 : r.right + m; y = r.top + 24; if (x + cw > W - 8) x = r.left + 24; }
    card.style.left = Math.max(8, Math.min(W - cw - 8, x)) + 'px';
    card.style.top = Math.max(8, Math.min(H - ch - 8, y)) + 'px';
  }

  function draw() {
    const s = STEPS[i], last = i === STEPS.length - 1;
    card.innerHTML = `<div class="tour-head">${icon(s.icon)}<span class="tour-step">${i + 1} / ${STEPS.length}</span></div>
      <h3>${esc(t('tour.' + s.key))}</h3><p>${t('tour.' + s.key + 'D')}</p>
      <div class="tour-dots">${STEPS.map((_, k) => `<i class="${k === i ? 'on' : ''}"></i>`).join('')}</div>
      <div class="tour-f">
        ${last ? '' : `<button type="button" class="btn btn-ghost btn-sm" data-t="skip">${esc(t('tour.skip'))}</button>`}
        <span class="sp"></span>
        ${i > 0 ? `<button type="button" class="btn btn-sm" data-t="back">${esc(t('tour.back'))}</button>` : ''}
        <button type="button" class="btn btn-primary btn-sm" data-t="next">${esc(t(last ? 'tour.done' : 'tour.next'))}</button>
      </div>`;
    place();
    card.querySelector('[data-t="next"]').focus();
  }

  function finish() {
    removeEventListener('resize', place);
    document.removeEventListener('keydown', onKey, true);
    root.remove();
    active = null;
    prevFocus?.focus?.();
    onDone?.();
  }
  const go = (d) => { const n = i + d; if (n < 0) return; if (n >= STEPS.length) { finish(); return; } i = n; draw(); };

  function onKey(e) {
    if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); finish(); }
    else if (e.key === 'ArrowRight' || (e.key === 'Enter' && !e.target.closest?.('[data-t]'))) { e.preventDefault(); e.stopPropagation(); go(1); }
    else if (e.key === 'ArrowLeft') { e.preventDefault(); e.stopPropagation(); go(-1); }
    else if (e.key === 'Tab') {
      // Odak kartın içinde kalsın
      const b = [...card.querySelectorAll('button')]; const k = b.indexOf(document.activeElement);
      e.preventDefault(); b[(k + (e.shiftKey ? -1 : 1) + b.length) % b.length]?.focus();
    } else if (!e.ctrlKey && !e.altKey) e.stopPropagation(); // yazı düzenleyiciye gitmesin
  }
  card.addEventListener('click', (e) => {
    const a = e.target.closest('[data-t]')?.dataset.t;
    if (a === 'next') go(1); else if (a === 'back') go(-1); else if (a === 'skip') finish();
  });
  // Karartılmış alana tıklamak turu kapatmaz (yanlışlıkla kaçırılmasın), yalnızca yutulur
  root.addEventListener('mousedown', (e) => { if (!e.target.closest('.tour-card')) e.preventDefault(); });
  addEventListener('resize', place);
  document.addEventListener('keydown', onKey, true);
  active = { finish };
  draw();
}

export function tourActive() { return !!active; }
