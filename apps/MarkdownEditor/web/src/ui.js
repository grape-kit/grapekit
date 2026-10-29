// Ortak arayüz parçaları: açılır menü, bağlam menüsü, diyalog, onay, bildirim. Klavyeyle tam kullanılabilir.
import { t } from './i18n.js';
import { icon } from './icons.js';

const layer = () => document.getElementById('layer');
export const esc = (s) => String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
export const h = (html) => { const d = document.createElement('template'); d.innerHTML = html.trim(); return d.content.firstElementChild; };

// ===================== Menü =====================
// items: [{ label, icon, kbd, checked, disabled, run, submenu: [...] } | '-']
let openMenu = null;

export function closeMenus() {
  while (openMenu) { const m = openMenu; openMenu = m.parentMenu; m.el.remove(); m.onClose?.(); }
}

export function showMenu(items, x, y, { parentMenu = null, onClose, align = 'left', minWidth = 0 } = {}) {
  if (!parentMenu) closeMenus();
  const el = h(`<div class="menu" role="menu"></div>`);
  if (minWidth) el.style.minWidth = minWidth + 'px';
  const menu = { el, parentMenu, onClose, child: null };
  const rows = [];
  for (const it of items) {
    if (it === '-') { el.append(h('<div class="menu-sep" role="separator"></div>')); continue; }
    if (it.header) { el.append(h(`<div class="menu-head">${esc(it.header)}</div>`)); continue; }
    const row = h(`<button type="button" class="menu-item" role="${it.checked !== undefined ? 'menuitemcheckbox' : 'menuitem'}"${it.disabled ? ' disabled' : ''}>
      <span class="mi-check">${it.checked ? icon('check') : it.icon ? icon(it.icon) : ''}</span>
      <span class="mi-label">${esc(it.label)}</span>
      <span class="mi-kbd">${it.submenu ? icon('chevR') : esc(it.kbd || '')}</span></button>`);
    if (it.checked !== undefined) row.setAttribute('aria-checked', it.checked ? 'true' : 'false');
    const openSub = () => {
      if (menu.child) { closeFrom(menu.child); menu.child = null; }
      if (!it.submenu) return;
      const r = row.getBoundingClientRect();
      menu.child = showMenu(typeof it.submenu === 'function' ? it.submenu() : it.submenu, r.right - 4, r.top - 6, { parentMenu: menu });
    };
    row.addEventListener('mouseenter', () => { row.focus(); if (it.submenu) openSub(); else if (menu.child) { closeFrom(menu.child); menu.child = null; } });
    row.addEventListener('click', (e) => { e.stopPropagation(); if (it.submenu) { openSub(); menu.child?.el.querySelector('.menu-item:not([disabled])')?.focus(); return; } closeMenus(); it.run?.(); });
    row._open = openSub;
    rows.push(row);
    el.append(row);
  }
  el.addEventListener('keydown', (e) => {
    const list = rows.filter((r) => !r.disabled);
    const i = list.indexOf(document.activeElement);
    if (e.key === 'ArrowDown') { list[(i + 1) % list.length]?.focus(); e.preventDefault(); }
    else if (e.key === 'ArrowUp') { list[(i - 1 + list.length) % list.length]?.focus(); e.preventDefault(); }
    else if (e.key === 'ArrowRight' && document.activeElement?._open) { document.activeElement._open(); menu.child?.el.querySelector('.menu-item:not([disabled])')?.focus(); e.preventDefault(); }
    else if (e.key === 'ArrowLeft' && parentMenu) { closeFrom(menu); parentMenu.child = null; e.preventDefault(); }
    else if (e.key === 'Escape') { closeMenus(); e.preventDefault(); }
    else if (e.key === 'Home') { list[0]?.focus(); e.preventDefault(); }
    else if (e.key === 'End') { list[list.length - 1]?.focus(); e.preventDefault(); }
    else if (e.key.length === 1 && !e.ctrlKey && !e.altKey) {
      const k = e.key.toLocaleLowerCase();
      const j = list.findIndex((r, n) => n > i && r.querySelector('.mi-label').textContent.toLocaleLowerCase().startsWith(k));
      (list[j] || list.find((r) => r.querySelector('.mi-label').textContent.toLocaleLowerCase().startsWith(k)))?.focus();
    }
  });
  layer().append(el);
  // Ekrana sığdır
  const W = innerWidth, H = innerHeight, r = el.getBoundingClientRect();
  let left = align === 'right' ? x - r.width : x;
  if (left + r.width > W - 4) left = parentMenu ? x - r.width - (parentMenu.el.offsetWidth - 8) : W - r.width - 4;
  let top = y; if (top + r.height > H - 4) top = Math.max(4, H - r.height - 4);
  el.style.left = Math.max(4, left) + 'px'; el.style.top = top + 'px';
  openMenu = menu;
  return menu;
}

function closeFrom(m) {
  let c = m; while (c) { c.el.remove(); const n = c.child; c.child = null; c = n; }
  if (openMenu === m || isDesc(m)) openMenu = m.parentMenu;
}
function isDesc(m) { let c = openMenu; while (c) { if (c === m) return true; c = c.parentMenu; } return false; }

document.addEventListener('mousedown', (e) => { if (openMenu && !e.target?.closest?.('.menu') && !e.target?.closest?.('.menubar button')) closeMenus(); }, true);
addEventListener('blur', () => closeMenus());
addEventListener('resize', () => closeMenus());

// ===================== Bildirim =====================
export function toast(msg, { kind = 'info', actions = [], timeout = 4000 } = {}) {
  let box = document.querySelector('.toasts');
  if (!box) { box = h('<div class="toasts" role="status" aria-live="polite"></div>'); document.body.append(box); }
  const el = h(`<div class="toast toast-${kind}"><span>${esc(msg)}</span></div>`);
  for (const a of actions) {
    const b = h(`<button type="button">${esc(a.label)}</button>`);
    b.onclick = () => { el.remove(); a.run(); };
    el.append(b);
  }
  const x = h(`<button type="button" class="toast-x" aria-label="${esc(t('close'))}">${icon('x')}</button>`);
  x.onclick = () => el.remove();
  el.append(x);
  box.append(el);
  if (timeout) setTimeout(() => { el.classList.add('out'); setTimeout(() => el.remove(), 250); }, timeout + actions.length * 3000);
  return el;
}

// ===================== Diyalog =====================
/**
 * Diyalog açar. body: HTML (string) ya da öğe; buttons: [{ label, primary, value, danger }]
 * Dönüş: Promise<{ value, el }>; Esc / dış tıklama → value: null
 */
export function dialog({ title, body, buttons = [{ label: t('ok'), primary: true, value: true }, { label: t('cancel'), value: null }], width = 460, onOpen, className = '' }) {
  return new Promise((resolve) => {
    const prev = document.activeElement;
    const back = h(`<div class="modal-back"></div>`);
    const el = h(`<div class="modal ${className}" role="dialog" aria-modal="true" aria-label="${esc(title)}" style="width:${width}px">
      <div class="modal-h"><h2>${esc(title)}</h2><button type="button" class="icon-btn modal-x" aria-label="${esc(t('close'))}">${icon('x')}</button></div>
      <div class="modal-b"></div><div class="modal-f"></div></div>`);
    const b = el.querySelector('.modal-b');
    if (typeof body === 'string') b.innerHTML = body; else if (body) b.append(body);
    const f = el.querySelector('.modal-f');
    const done = (value) => { back.remove(); document.removeEventListener('keydown', key, true); prev?.focus?.(); resolve({ value, el }); };
    for (const btn of buttons) {
      const x = h(`<button type="button" class="btn${btn.primary ? ' btn-primary' : ''}${btn.danger ? ' btn-danger' : ''}">${esc(btn.label)}</button>`);
      x.onclick = () => done(typeof btn.value === 'function' ? btn.value(el) : btn.value);
      f.append(x);
    }
    if (!buttons.length) f.remove();
    el.querySelector('.modal-x').onclick = () => done(null);
    back.append(el);
    back.addEventListener('mousedown', (e) => { if (e.target === back) done(null); });
    const key = (e) => {
      if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); done(null); }
      else if (e.key === 'Enter' && !e.shiftKey && e.target.tagName !== 'TEXTAREA' && e.target.tagName !== 'BUTTON' && !e.target?.closest?.('.no-enter')) {
        const p = buttons.find((x) => x.primary); if (p) { e.preventDefault(); done(typeof p.value === 'function' ? p.value(el) : p.value); }
      } else if (e.key === 'Tab') { // odak diyalogda kalır
        const fs = [...el.querySelectorAll('button, input, select, textarea, [tabindex="0"]')].filter((n) => !n.disabled && n.offsetParent);
        if (!fs.length) return;
        if (e.shiftKey && document.activeElement === fs[0]) { fs[fs.length - 1].focus(); e.preventDefault(); }
        else if (!e.shiftKey && document.activeElement === fs[fs.length - 1]) { fs[0].focus(); e.preventDefault(); }
      }
    };
    document.addEventListener('keydown', key, true);
    layer().append(back);
    onOpen?.(el, done);
    (el.querySelector('[autofocus]') || el.querySelector('input, select, textarea') || f.querySelector('.btn-primary') || f.querySelector('button'))?.focus();
  });
}

/** Evet/Hayır/Vazgeç türü soru. buttons: [{label, value, primary}] */
export async function ask(message, buttons) {
  const { value } = await dialog({ title: t('app.name'), body: `<p class="ask">${esc(message)}</p>`, buttons, width: 440 });
  return value;
}

/** Tek satır giriş. */
export async function prompt(title, { value = '', placeholder = '', label = '' } = {}) {
  const { value: v, el } = await dialog({
    title,
    body: `${label ? `<label class="field-l">${esc(label)}</label>` : ''}<input class="field" type="text" value="${esc(value)}" placeholder="${esc(placeholder)}" autofocus>`,
    buttons: [{ label: t('ok'), primary: true, value: (d) => d.querySelector('input').value }, { label: t('cancel'), value: null }],
    onOpen: (d) => { const i = d.querySelector('input'); setTimeout(() => i.select(), 0); },
  });
  return v;
}

/** Biçimli form alanları üreticisi (diyaloglar için). */
export const field = {
  text: (id, label, value = '', ph = '') => `<label class="field-l" for="${id}">${esc(label)}</label><input class="field" id="${id}" type="text" value="${esc(value)}" placeholder="${esc(ph)}">`,
  number: (id, label, value, min, max) => `<label class="field-l" for="${id}">${esc(label)}</label><input class="field" id="${id}" type="number" value="${value}" min="${min}" max="${max}">`,
  select: (id, label, opts, value) => `<label class="field-l" for="${id}">${esc(label)}</label><select class="field" id="${id}">${opts.map(([v, l]) => `<option value="${esc(v)}"${String(v) === String(value) ? ' selected' : ''}>${esc(l)}</option>`).join('')}</select>`,
  check: (id, label, checked, hint = '') => `<label class="check"><input type="checkbox" id="${id}"${checked ? ' checked' : ''}><span class="check-box"></span><span><b>${esc(label)}</b>${hint ? `<small>${esc(hint)}</small>` : ''}</span></label>`,
  seg: (id, opts, value) => `<div class="seg" role="radiogroup" id="${id}">${opts.map(([v, l]) => `<button type="button" role="radio" data-v="${esc(v)}" aria-checked="${String(v) === String(value)}">${esc(l)}</button>`).join('')}</div>`,
};

/** .seg düğme gruplarını tıklanabilir yapar; değer: el.dataset.value */
export function wireSegs(root) {
  for (const s of root.querySelectorAll('.seg')) {
    const btns = [...s.querySelectorAll('button')];
    s.dataset.value = btns.find((b) => b.getAttribute('aria-checked') === 'true')?.dataset.v ?? '';
    for (const b of btns) b.onclick = () => { btns.forEach((x) => x.setAttribute('aria-checked', String(x === b))); s.dataset.value = b.dataset.v; s.dispatchEvent(new Event('change')); };
  }
}
