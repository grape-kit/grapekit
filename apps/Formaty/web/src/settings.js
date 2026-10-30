// Ayarlar penceresi: Genel, Dönüştürme, Hakkında.
import { call } from './bridge.js';
import { t, getLang } from './i18n.js';
import { icon } from './icons.js';
import { esc, dialog } from './ui.js';
import { app, applySetting, saveSettings, updateBadges } from './main.js';

const SECTIONS = ['general', 'convert', 'about'];

export function settingsDialog(start = 'general') {
  let sec = start;
  const S = app.settings;
  const tg = (id, on) => `<button type="button" class="toggle" role="switch" id="${id}" aria-checked="${!!on}"><i></i></button>`;
  const row = (title, desc, ctl) => `<div class="set-row"><div><b>${esc(title)}</b>${desc ? `<small>${esc(desc)}</small>` : ''}</div>${ctl}</div>`;
  const seg = (id, opts, v) => `<div class="seg" id="${id}" role="radiogroup">${opts.map(([val, l]) => `<button type="button" role="radio" data-v="${esc(val)}" aria-checked="${String(v) === String(val)}">${esc(l)}</button>`).join('')}</div>`;

  function body() {
    if (sec === 'general') return `<h3>${esc(t('set.general'))}</h3>`
      + row(t('set.lang'), '', seg('s-lang', [['tr', 'Türkçe'], ['en', 'English']], getLang()))
      + row(t('set.theme'), '', seg('s-theme', [['system', t('set.system')], ['light', t('set.light')], ['dark', t('set.dark')]], S.theme))
      + (app.info.packaged ? '' : row(t('set.explorer'), t('set.explorerD'), tg('s-explorer', app.info.explorerMenu)))
      + row(t('set.notify'), t('set.notifyD'), tg('s-notify', S.notify))
      + (app.info.packaged ? '' : row(t('set.autoUpdate'), t('set.autoUpdateD'), tg('s-update', S.autoUpdate)))
      + row(t('set.tour'), t('set.tourD'), `<button type="button" class="btn btn-sm" id="s-tour">${esc(t('set.tourBtn'))}</button>`);
    if (sec === 'convert') return `<h3>${esc(t('set.convert'))}</h3>`
      + row(t('set.parallel'), t('set.parallelD', { n: app.info.cpu || 4 }), `<select class="field narrow" id="s-parallel">${[[0, t('set.auto')], [1, '1'], [2, '2'], [3, '3'], [4, '4'], [6, '6'], [8, '8']].map(([v, l]) => `<option value="${v}"${+S.parallel === v ? ' selected' : ''}>${esc(l)}</option>`).join('')}</select>`)
      + row(t('set.hw'), t('set.hwD'), tg('s-hw', S.hw))
      + `<div class="set-row col"><div><b>${esc(t('set.hwList'))}</b><small id="s-hwlist">${esc(t('set.hwChecking'))}</small></div></div>`
      + row(t('set.reset'), t('set.resetD'), `<button type="button" class="btn btn-sm" id="s-reset">${esc(t('set.resetBtn'))}</button>`);
    return `<div class="about"><img src="icon.png" width="72" height="72" alt=""><div><h3>Formaty</h3><p>${esc(t('about.version', { v: app.info.version || '' }))}</p><p class="muted">${esc(t('about.lead'))}</p></div></div>`
      + (app.info.packaged ? '' : `<div class="set-row upd-row${app.info.updateAvailable ? ' has-update' : ''}"><div><b>${esc(t('set.updates'))}${app.info.updateAvailable ? '<span class="upd-badge inline">1</span>' : ''}</b><small id="u-status">${esc(app.info.updateStatus || '')}</small></div><button type="button" class="btn btn-sm${app.info.updateAvailable ? ' btn-primary' : ''}" id="u-check">${esc(app.info.updateAvailable ? app.info.updateLabel : t('set.checkUpdates'))}</button></div>`)
      + `<div class="about-links">
          <button type="button" class="btn btn-sm" data-url="page">${icon('external')}${esc(t('about.site'))}</button>
          <button type="button" class="btn btn-sm" data-url="feedback">${icon('help')}${esc(t('about.feedback'))}</button>
          <button type="button" class="btn btn-sm" data-url="coffee">${icon('coffee')}${esc(t('about.coffee'))}</button>
          <button type="button" class="btn btn-sm" id="s-licenses">${icon('file')}${esc(t('about.licenses'))}</button>
        </div>`;
  }

  return dialog({
    title: t('nav.settings'), width: 760, className: 'settings', buttons: [],
    body: `<div class="set-wrap"><nav class="set-nav">${SECTIONS.map((s) => `<button type="button" data-s="${s}">${icon({ general: 'settings', convert: 'sliders', about: 'info' }[s])}<span>${esc(t('set.' + s))}</span></button>`).join('')}</nav><div class="set-body"></div></div>`,
    onOpen: (el) => {
      const b = el.querySelector('.set-body');
      const draw = () => {
        for (const n of el.querySelectorAll('.set-nav button')) n.setAttribute('aria-current', String(n.dataset.s === sec));
        b.innerHTML = body();
        wire();
      };
      const wire = () => {
        for (const s of b.querySelectorAll('.seg')) for (const x of s.querySelectorAll('button')) x.onclick = () => {
          if (s.id === 's-lang') { S.lang = x.dataset.v; applySetting('lang'); el.querySelector('.modal-h h2').textContent = t('nav.settings'); for (const n of el.querySelectorAll('.set-nav button')) n.querySelector('span').textContent = t('set.' + n.dataset.s); }
          if (s.id === 's-theme') { S.theme = x.dataset.v; applySetting('theme'); }
          draw();
        };
        const tog = (id, fn) => { const x = b.querySelector('#' + id); if (x) x.onclick = async () => { const v = x.getAttribute('aria-checked') !== 'true'; x.setAttribute('aria-checked', String(v)); await fn(v); }; };
        tog('s-explorer', async (v) => { app.info.explorerMenu = await call('shell.explorerMenu', { enabled: v }); });
        tog('s-notify', (v) => { S.notify = v; saveSettings(); });
        tog('s-update', (v) => { S.autoUpdate = v; saveSettings(); });
        tog('s-hw', (v) => { S.hw = v; saveSettings(); });
        b.querySelector('#s-parallel')?.addEventListener('change', (e) => { S.parallel = +e.target.value; saveSettings(); });
        b.querySelector('#s-tour')?.addEventListener('click', () => { el.closest('.modal-back')?.querySelector('.modal-x')?.click(); window.dispatchEvent(new CustomEvent('formaty-tour')); });
        b.querySelector('#s-reset')?.addEventListener('click', () => { window.dispatchEvent(new CustomEvent('formaty-reset')); draw(); });
        b.querySelector('#s-licenses')?.addEventListener('click', () => call('shell.licenses', {}));
        for (const u of b.querySelectorAll('[data-url]')) u.onclick = () => call('shell.url', { which: u.dataset.url });
        const hw = b.querySelector('#s-hwlist');
        if (hw) call('engines.hw', {}).then((list) => {
          const names = { nvenc: 'NVIDIA NVENC', qsv: 'Intel Quick Sync', amf: 'AMD AMF', mf: 'Windows Media Foundation' };
          const by = {};
          for (const e of list || []) { const [codec, kind] = e.split('_'); (by[names[kind] || kind] ??= []).push(codec.toUpperCase()); }
          hw.textContent = Object.keys(by).length ? Object.entries(by).map(([k, v]) => `${k}: ${v.join(', ')}`).join(' · ') : t('set.hwNone');
        });
        const uc = b.querySelector('#u-check');
        if (uc) uc.onclick = async () => {
          if (app.info.updateAvailable) { call('update.install', {}); return; }
          uc.disabled = true;
          Object.assign(app.info, (await call('update.check', {})) || {});
          b.querySelector('#u-status').textContent = app.info.updateStatus || '';
          uc.disabled = false;
          updateBadges();
        };
      };
      for (const n of el.querySelectorAll('.set-nav button')) n.onclick = () => { sec = n.dataset.s; draw(); };
      // Pencere açıkken durum değişirse (yeni sürüm bulundu, indirme yüzdesi) Hakkında bölümünü tazele
      const onUpd = () => { if (!el.isConnected) { window.removeEventListener('formaty-update', onUpd); return; } if (sec === 'about') draw(); };
      window.addEventListener('formaty-update', onUpd);
      draw();
      updateBadges();
    },
  });
}
