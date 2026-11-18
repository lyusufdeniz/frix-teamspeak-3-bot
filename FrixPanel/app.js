/* Frix panel – core: helpers, API, i18n, shell, router, polling, mini player, login & setup.
   Pages live in pages.js (bots, player, radios) and admin.js (plugins, commands, users, settings, logs, faq). */
'use strict';
(() => {
  const F = (window.F = {});
  const Pages = (window.Pages = window.Pages || {});
  const LS_LANG = 'frix.lang';
  const LS_BOT = 'frix.bot';

  // replaceChildren() that ignores null/false and flattens arrays (used everywhere for conditional children)
  const nativeReplace = Element.prototype.replaceChildren;
  Element.prototype.replaceChildren = function (...kids) {
    return nativeReplace.apply(this, kids.flat(Infinity).filter((k) => k != null && k !== false));
  };

  const lsGet = (k) => { try { return localStorage.getItem(k); } catch (_) { return null; } };
  const lsSet = (k, v) => { try { localStorage.setItem(k, v); } catch (_) { /* ignore */ } };

  const S = (F.state = {
    me: null, lang: 'tr', version: '', bots: [], botsLoaded: false, botsAt: 0,
    selected: lsGet(LS_BOT) || '', online: true,
  });

  // ------------------------------------------------------------------ i18n
  F.t = function (key, vars) {
    const dict = window.I18N[S.lang] || window.I18N.en;
    let s = dict[key];
    if (s == null) s = window.I18N.en[key]; // missing keys fall back to English, then Turkish
    if (s == null) s = window.I18N.tr[key];
    if (s == null) return key;
    if (vars) s = s.replace(/\{(\w+)\}/g, (m, k) => (vars[k] != null ? String(vars[k]) : m));
    return s;
  };
  F.has = (key) => (window.I18N[S.lang] || {})[key] != null;
  /** pick a {tr,en,…} object in the current language (fallback: en, then tr) */
  F.loc = (o) => (o == null ? '' : typeof o === 'string' ? o : o[S.lang] || o.en || o.tr || '');
  const t = F.t;

  // ------------------------------------------------------------------ DOM
  const SVGNS = 'http://www.w3.org/2000/svg';
  function h(tag, props, ...kids) {
    const el = document.createElement(tag);
    if (props) {
      for (const k of Object.keys(props)) {
        const v = props[k];
        if (v == null || v === false) continue;
        if (k === 'class') el.className = v;
        else if (k === 'text') el.textContent = v;
        else if (k === 'style' && typeof v === 'object') Object.assign(el.style, v);
        else if (k === 'dataset') Object.assign(el.dataset, v);
        else if (k.startsWith('on') && typeof v === 'function') el.addEventListener(k.slice(2).toLowerCase(), v);
        else if (k === 'value' || k === 'checked' || k === 'selected' || k === 'indeterminate') el[k] = v;
        else el.setAttribute(k, v === true ? '' : String(v));
      }
    }
    append(el, kids);
    return el;
  }
  function append(el, kids) {
    for (const c of kids) {
      if (c == null || c === false) continue;
      if (Array.isArray(c)) append(el, c);
      else if (c instanceof Node) el.appendChild(c);
      else el.appendChild(document.createTextNode(String(c)));
    }
  }
  F.h = h;
  F.$ = (sel, root) => (root || document).querySelector(sel);
  F.enc = encodeURIComponent;

  // compact icon DSL: p:path | l:x1 y1 x2 y2 | c:cx cy r | pl:points | pg:points | r:x y w h rx
  // Frix line icons (24px grid, drawn for this panel; stroke width comes from CSS)
  const ICONS = {
    play: 'p:M8 5.5v13l10-6.5z',
    pause: 'p:M9 5.5v13M15 5.5v13',
    next: 'p:M6 6l8 6-8 6z|p:M18 6v12',
    prev: 'p:M18 6l-8 6 8 6z|p:M6 6v12',
    stop: 'r:6.5 6.5 11 11 1.5',
    repeat: 'p:M4 11V9.5A2.5 2.5 0 0 1 6.5 7H19|p:M16 4l3 3-3 3|p:M20 13v1.5a2.5 2.5 0 0 1-2.5 2.5H5|p:M8 20l-3-3 3-3',
    shuffle: 'p:M4 7h3.5c4.5 0 4.5 10 9 10H20|p:M4 17h3.5c1.6 0 2.6-1.2 3.4-2.8|p:M13.1 9.8C13.9 8.2 14.9 7 16.5 7H20|p:M17.5 4.5 20 7l-2.5 2.5|p:M17.5 14.5 20 17l-2.5 2.5',
    volume: 'p:M4 9.5h3l4.5-4v13l-4.5-4H4z|p:M15 9.5a3.5 3.5 0 0 1 0 5|p:M17.5 7a7 7 0 0 1 0 10',
    plus: 'p:M12 5v14M5 12h14',
    menu: 'p:M4 7h16M4 12h16M4 17h16',
    clock: 'c:12 12 8.5|p:M12 7.5V12l3 2',
    idcard: 'r:3 5 18 14 2|c:9 11 2.2|p:M5.5 16.5c.8-1.8 2-2.5 3.5-2.5s2.7.7 3.5 2.5|p:M14 10h4M14 13.5h3',
    hand: 'p:M8 13V6.5a1.5 1.5 0 0 1 3 0V11|p:M11 10.5v-6a1.5 1.5 0 0 1 3 0v6|p:M14 10.5V6a1.5 1.5 0 0 1 3 0v8a6 6 0 0 1-6 6h-.5a6 6 0 0 1-4.6-2.2L4 14.6a1.5 1.5 0 0 1 2.3-1.9L8 14.5',
    megaphone: 'p:M4 10v4h3l7 4V6l-7 4z|p:M17 9.5a3 3 0 0 1 0 5|p:M7 14l1 5h2.5l-1-4.5',
    calendar: 'r:4 5.5 16 14.5 2|p:M8 3.5v4M16 3.5v4M4 10h16|p:M8.5 14h2M13.5 14h2',
    vote: 'p:M7 10.5V20H4.5v-9.5z|p:M7 11l4-7c1.2 0 2 1 1.7 2.2L12 10h5.6a2 2 0 0 1 2 2.4l-1.3 6A2 2 0 0 1 16.4 20H7',
    headphones: 'p:M4 15v-3a8 8 0 0 1 16 0v3|r:3.5 14 4 6 1.5|r:16.5 14 4 6 1.5',
    moon: 'p:M19 14.5A7.5 7.5 0 1 1 9.5 5a6 6 0 0 0 9.5 9.5z',
    filter: 'p:M4 5h16l-6 7.5V19l-4-2v-4.5z',
    shield: 'p:M12 3.5l7 3v5.5c0 4.2-3 7.4-7 8.5-4-1.1-7-4.3-7-8.5V6.5z|p:M9 12l2 2 4-4',
    zap: 'p:M13 3L5 13.5h6L10 21l8-10.5h-6z',
    userPlus: 'c:10 8 3.5|p:M3.5 19.5c.9-3.4 3.4-5 6.5-5s5.6 1.6 6.5 5|p:M18.5 7.5v5M16 10h5',
    folderPlus: 'p:M3.5 7a2 2 0 0 1 2-2h4l2 2.5h7a2 2 0 0 1 2 2V17a2 2 0 0 1-2 2h-13a2 2 0 0 1-2-2z|p:M12 10.5v5M9.5 13h5',
    trophy: 'p:M8 4h8v5a4 4 0 0 1-8 0z|p:M8 6H5a3 3 0 0 0 3 4M16 6h3a3 3 0 0 1-3 4|p:M12 13v4M8.5 20h7l-.8-3H9.3z',
    bell: 'p:M6.5 16.5V11a5.5 5.5 0 0 1 11 0v5.5l1.5 1.5H5z|p:M10 20a2 2 0 0 0 4 0',
    dice: 'r:4 4 16 16 3|c:8.5 8.5 1.1|c:15.5 15.5 1.1|c:15.5 8.5 1.1|c:8.5 15.5 1.1|c:12 12 1.1',
    cpu: 'r:7 7 10 10 1.5|p:M10 3v4M14 3v4M10 17v4M14 17v4M3 10h4M3 14h4M17 10h4M17 14h4',
    memory: 'r:3 7 18 10 1.5|p:M7 10v4M11 10v4M15 10v4|p:M6 17v3M10 17v3M14 17v3M18 17v3',
    arrowUp: 'p:M12 19V5M6 11l6-6 6 6',
    arrowDown: 'p:M12 5v14M6 13l6 6 6-6',
    trash: 'p:M5 7h14|p:M9.5 7V5h5v2|p:M7 7l.8 12h8.4L17 7|p:M10.5 10.5v5M13.5 10.5v5',
    edit: 'p:M5 19l1-4L15.5 5.5a2.1 2.1 0 0 1 3 3L9 18z|p:M13.5 7.5l3 3',
    power: 'p:M12 4v7|p:M7.5 6.8a7 7 0 1 0 9 0',
    refresh: 'p:M19 12a7 7 0 1 1-2.05-4.95|p:M19 5v4h-4',
    search: 'c:10.5 10.5 5.5|p:M15 15l4.5 4.5',
    upload: 'p:M12 15V4.5|p:M7.5 9 12 4.5 16.5 9|p:M5 15v3.5h14V15',
    download: 'p:M12 4.5V15|p:M7.5 10.5 12 15l4.5-4.5|p:M5 15v3.5h14V15',
    x: 'p:M6.5 6.5l11 11M17.5 6.5l-11 11',
    check: 'p:M5 12.5l4.5 4.5L19 7.5',
    up: 'p:M12 19V5|p:M6.5 10.5 12 5l5.5 5.5',
    down: 'p:M12 5v14|p:M6.5 13.5 12 19l5.5-5.5',
    logout: 'p:M10 5H5.5v14H10|p:M14 8l4 4-4 4|p:M18 12H9',
    login: 'p:M14 5h4.5v14H14|p:M9 8l4 4-4 4|p:M13 12H4',
    globe: 'c:12 12 8.5|p:M3.5 12h17|p:M12 3.5c2.4 2.4 3.5 5.2 3.5 8.5s-1.1 6.1-3.5 8.5c-2.4-2.4-3.5-5.2-3.5-8.5s1.1-6.1 3.5-8.5z',
    grid: 'r:4.5 4.5 6 6 1|r:13.5 4.5 6 6 1|r:4.5 13.5 6 6 1|r:13.5 13.5 6 6 1',
    music: 'p:M9 17.5V6l10-2v11.5|c:6.5 17.5 2.5|c:16.5 15.5 2.5',
    radio: 'c:12 12 1.5|p:M8.5 15.5a5 5 0 0 1 0-7|p:M15.5 8.5a5 5 0 0 1 0 7|p:M5.6 18.4a9 9 0 0 1 0-12.8|p:M18.4 5.6a9 9 0 0 1 0 12.8',
    box: 'p:M5 8h3.5a2 2 0 1 1 4 0H16v3.5a2 2 0 1 1 0 4V19H5z',
    terminal: 'r:3.5 5 17 14 1.5|p:M7.5 10l2.5 2-2.5 2|p:M12.5 14.5h4',
    users: 'c:9 9 3|p:M3.5 19c.6-3 2.8-4.5 5.5-4.5s4.9 1.5 5.5 4.5|p:M15.5 6.2a3 3 0 0 1 0 5.6|p:M17 14.8c1.8.5 3 1.9 3.5 4.2',
    sliders: 'p:M5 7h9M18 7h1M5 17h1M10 17h9|c:16 7 2|c:8 17 2',
    file: 'p:M6 3.5h8l4 4v13H6z|p:M14 3.5v4h4|p:M9 12h6M9 15.5h6',
    help: 'c:12 12 8.5|p:M9.8 9.7a2.3 2.3 0 1 1 3.2 2.1c-.7.3-1 .8-1 1.5v.4|p:M12 16.6v.1',
    user: 'c:12 8.5 3.5|p:M5 19.5c.8-3.3 3.6-5 7-5s6.2 1.7 7 5',
    more: 'c:6 12 1|c:12 12 1|c:18 12 1',
    server: 'r:4 4.5 16 6 1.5|r:4 13.5 16 6 1.5|p:M7.5 7.5h.01M7.5 16.5h.01',
    list: 'p:M9 7h10M9 12h10M9 17h10|p:M5 7h.01M5 12h.01M5 17h.01',
    link: 'p:M13 5h6v6|p:M19 5l-8 8|p:M17 13.5V19H5V7h5.5',
    key: 'c:8 15 3.5|p:M10.5 12.5 19 4|p:M16 7l2.5 2.5|p:M13.5 9.5l2 2',
    alert: 'p:M12 4 3 19.5h18z|p:M12 10v4|p:M12 16.8v.1',
    info: 'c:12 12 8.5|p:M12 11v5|p:M12 7.9v.1',
    chevron: 'p:M6.5 9.5 12 15l5.5-5.5',
    eye: 'p:M2.5 12S6 5.5 12 5.5 21.5 12 21.5 12 18 18.5 12 18.5 2.5 12 2.5 12z|c:12 12 2.8',
    lock: 'r:5.5 10.5 13 9 1.5|p:M8.5 10.5V8a3.5 3.5 0 0 1 7 0v2.5',
    hash: 'p:M5 9.5h14M5 14.5h14M10 5l-1.5 14M15.5 5 14 19',
    wave: 'p:M3.5 12h3l2-5.5 3 11 3-13 2.5 10 1.5-2.5h2',
    text: 'p:M5 6.5h14M12 6.5V19M9 19h6',
    sun: 'c:12 12 3.5|p:M12 3.5v2M12 18.5v2M3.5 12h2M18.5 12h2M6 6l1.4 1.4M16.6 16.6 18 18M6 18l1.4-1.4M16.6 7.4 18 6',
    moon: 'p:M18.5 14.5A7 7 0 0 1 9.5 5.5a7 7 0 1 0 9 9z',
    image: 'r:4 5 16 14 1.5|c:9 10 1.5|p:M5 17l4.5-4 3 2.5 3-3.5L20 16',
    chart: 'p:M4 19.5h16|p:M7.5 16v-4|p:M12 16V6.5|p:M16.5 16v-6.5',
    playlist: 'p:M4 6.5h11M4 11h11M4 15.5h6.5|c:15.5 17.5 2|p:M17.5 17.5V9.5l3-1',
    save: 'p:M5 4.5h11l3 3v12H5z|p:M8 4.5v4.5h7V4.5|r:8 13 8 6.5 0',
  };
  F.icon = function (name, cls) {
    const svg = document.createElementNS(SVGNS, 'svg');
    svg.setAttribute('viewBox', '0 0 24 24');
    svg.setAttribute('class', 'ic' + (cls ? ' ' + cls : ''));
    svg.setAttribute('aria-hidden', 'true');
    svg.setAttribute('focusable', 'false');
    for (const part of (ICONS[name] || ICONS.info).split('|')) {
      const i = part.indexOf(':');
      const kind = part.slice(0, i);
      const a = part.slice(i + 1);
      const n = a.split(' ');
      let el;
      if (kind === 'p') { el = document.createElementNS(SVGNS, 'path'); el.setAttribute('d', a); }
      else if (kind === 'l') { el = document.createElementNS(SVGNS, 'line'); ['x1', 'y1', 'x2', 'y2'].forEach((k, j) => el.setAttribute(k, n[j])); }
      else if (kind === 'c') { el = document.createElementNS(SVGNS, 'circle'); ['cx', 'cy', 'r'].forEach((k, j) => el.setAttribute(k, n[j])); }
      else if (kind === 'pl' || kind === 'pg') { el = document.createElementNS(SVGNS, kind === 'pl' ? 'polyline' : 'polygon'); el.setAttribute('points', a); }
      else if (kind === 'r') { el = document.createElementNS(SVGNS, 'rect'); ['x', 'y', 'width', 'height', 'rx'].forEach((k, j) => n[j] != null && el.setAttribute(k, n[j])); }
      if (el) svg.appendChild(el);
    }
    return svg;
  };
  const icon = F.icon;

  /** icon button with accessible label */
  F.iconBtn = function (name, label, onClick, cls) {
    return h('button', { type: 'button', class: 'btn-icon' + (cls ? ' ' + cls : ''), 'aria-label': label, title: label, onclick: onClick }, icon(name));
  };
  /** normal button: btn('Save', {kind:'primary', icon:'check', onClick}) */
  F.btn = function (label, o) {
    o = o || {};
    return h('button', {
      type: o.type || 'button', class: 'btn' + (o.kind ? ' btn-' + o.kind : '') + (o.cls ? ' ' + o.cls : ''),
      onclick: o.onClick, disabled: o.disabled, title: o.title, 'aria-label': o.aria,
    }, o.icon ? icon(o.icon) : null, label ? h('span', { text: label }) : null);
  };
  /** run an async action while showing a busy state on a button */
  F.busy = async function (btn, fn) {
    if (btn && btn.classList.contains('is-busy')) return;
    if (btn) { btn.disabled = true; btn.classList.add('is-busy'); }
    try { return await fn(); }
    finally { if (btn) { btn.disabled = false; btn.classList.remove('is-busy'); } }
  };

  let uid = 0;
  F.uid = (p) => (p || 'f') + ++uid;

  F.field = function (label, control, help, opts) {
    opts = opts || {};
    const target = control.matches && control.matches('input,select,textarea') ? control : control.querySelector && control.querySelector('input,select,textarea');
    if (target && !target.id) target.id = F.uid('in');
    const helpEl = help ? h('div', { class: 'help', id: F.uid('h') }) : null;
    if (helpEl) {
      if (help instanceof Node) helpEl.appendChild(help); else helpEl.textContent = help;
      if (target) target.setAttribute('aria-describedby', helpEl.id);
    }
    return h('div', { class: 'field' + (opts.cls ? ' ' + opts.cls : '') },
      label ? h('label', { for: target ? target.id : null, text: label }) : null, control, helpEl);
  };
  F.input = (props) => h('input', Object.assign({ class: 'input', type: 'text', autocomplete: 'off', spellcheck: 'false' }, props));
  F.textarea = (props) => h('textarea', Object.assign({ class: 'input', rows: 4, spellcheck: 'false' }, props));
  F.select = function (options, value, props) {
    const s = h('select', Object.assign({ class: 'input' }, props));
    for (const o of options) {
      const [v, l] = Array.isArray(o) ? o : [o, o];
      s.appendChild(h('option', { value: String(v), text: l }));
    }
    if (value != null) s.value = String(value);
    return s;
  };
  F.switchEl = function (checked, label, props) {
    const inp = h('input', Object.assign({ type: 'checkbox', role: 'switch', checked: !!checked }, props));
    const el = h('label', { class: 'switch' }, inp, h('span', { class: 'switch-track', 'aria-hidden': 'true' }, h('span', { class: 'switch-thumb' })), label ? h('span', { class: 'switch-label', text: label }) : null);
    el.input = inp;
    return el;
  };
  F.badge = (text, kind) => h('span', { class: 'badge' + (kind ? ' badge-' + kind : ''), text });
  F.empty = (ic, title, text, action) => h('div', { class: 'empty' }, h('div', { class: 'empty-ic' }, icon(ic)), h('div', { class: 'empty-title', text: title }), text ? (typeof text === 'string' ? h('p', { class: 'muted', text }) : h('p', { class: 'muted' }, text)) : null, action || null);
  F.skeleton = (n, cls) => Array.from({ length: n || 3 }, () => h('div', { class: 'skeleton ' + (cls || '') }));
  /** page title, subtitle and actions live in the top bar (one place for every page) */
  F.pageHead = function (title, sub, actions) {
    const tt = F.$('#top-title');
    if (!tt) return h('div', { class: 'page-head' }, h('div', null, h('h1', { text: title }), sub ? h('p', { class: 'muted', text: sub }) : null), actions ? h('div', { class: 'page-actions' }, actions) : null);
    tt.textContent = title;
    F.$('#top-sub').textContent = sub || '';
    F.$('#top-page-actions').replaceChildren(...[].concat(actions || []).filter(Boolean));
    document.title = title + ' · frix';
    return document.createComment('page-head');
  };
  /** text containing `code` segments → nodes */
  F.rich = function (str) {
    const out = [];
    String(str).split('`').forEach((part, i) => { if (part) out.push(i % 2 ? h('code', { text: part }) : document.createTextNode(part)); });
    return out;
  };

  // ------------------------------------------------------------------ formatting
  F.fmtTime = function (sec) {
    sec = Math.max(0, Math.floor(sec || 0));
    const hh = Math.floor(sec / 3600), mm = Math.floor((sec % 3600) / 60), ss = sec % 60;
    const p = (x) => String(x).padStart(2, '0');
    return hh ? hh + ':' + p(mm) + ':' + p(ss) : mm + ':' + p(ss);
  };
  const LOCALES = { tr: 'tr-TR', en: 'en-GB', de: 'de-DE', es: 'es-ES', fr: 'fr-FR', ru: 'ru-RU', pt: 'pt-BR', pl: 'pl-PL', it: 'it-IT' };
  F.fmtNum = (n) => Number(n || 0).toLocaleString(LOCALES[S.lang] || 'en-GB');
  F.fmtDate = function (iso) {
    if (!iso) return '–';
    const d = new Date(iso);
    if (isNaN(d)) return String(iso);
    return d.toLocaleString(LOCALES[S.lang] || 'en-GB', { dateStyle: 'short', timeStyle: 'short' });
  };

  // ------------------------------------------------------------------ roles
  const RANK = { viewer: 1, dj: 2, admin: 3 };
  F.isAdmin = () => !!S.me && S.me.role === 'admin';
  F.canControl = () => !!S.me && (RANK[S.me.role] || 0) >= 2;

  // ------------------------------------------------------------------ API
  F.api = async function (method, path, body, opts) {
    opts = opts || {};
    const init = { method, credentials: 'same-origin', headers: {}, cache: 'no-store' };
    if (method !== 'GET') init.headers['X-Panel'] = '1';
    if (body !== undefined) {
      if (opts.text) { init.headers['Content-Type'] = 'text/plain; charset=utf-8'; init.body = body; }
      else { init.headers['Content-Type'] = 'application/json'; init.body = JSON.stringify(body); }
    }
    let res;
    try { res = await fetch('/panel/api/' + path, init); }
    catch (_) { const e = new Error('network'); e.code = 'network'; throw e; }
    let data = null;
    const txt = await res.text();
    if (txt) { try { data = JSON.parse(txt); } catch (_) { data = { error: txt.slice(0, 300) }; } }
    if (res.status === 401 && !opts.no401) F.onUnauthorized();
    if (!res.ok) {
      const code = data && data.error ? String(data.error) : 'HTTP ' + res.status;
      const e = new Error(code); e.code = code; e.status = res.status; e.data = data || {};
      throw e;
    }
    return data;
  };
  F.errMsg = function (e) {
    const code = e && (e.code || e.message);
    if (code && window.I18N.tr['err.' + code] != null) return t('err.' + code, e.data || {});
    return code || t('err.unknown');
  };
  F.fail = function (e) {
    if (e && e.status === 401) return;
    F.toast(F.errMsg(e), 'err');
  };

  // ------------------------------------------------------------------ toasts
  let toastBox = null;
  F.toast = function (msg, kind) {
    kind = kind || 'ok';
    if (!toastBox || !toastBox.isConnected) {
      toastBox = h('div', { class: 'toasts', role: 'status', 'aria-live': 'polite' });
      document.body.appendChild(toastBox);
    }
    const el = h('div', { class: 'toast toast-' + kind },
      icon(kind === 'err' ? 'alert' : kind === 'info' ? 'info' : 'check'),
      h('span', { class: 'toast-msg', text: msg }),
      h('button', { type: 'button', class: 'toast-x', 'aria-label': t('common.close'), onclick: () => close() }, icon('x')));
    toastBox.appendChild(el);
    requestAnimationFrame(() => requestAnimationFrame(() => el.classList.add('show')));
    let gone = false;
    function close() {
      if (gone) return; gone = true;
      el.classList.remove('show');
      setTimeout(() => el.remove(), 250);
    }
    setTimeout(close, kind === 'err' ? 7000 : 3800);
  };

  // ------------------------------------------------------------------ modal
  F.modal = function (o) {
    const prevFocus = document.activeElement;
    const titleId = F.uid('mt');
    const form = h('form', { class: 'modal', role: 'dialog', 'aria-modal': 'true', 'aria-labelledby': titleId, novalidate: true });
    const back = h('div', { class: 'modal-back' }, form);
    if (o.size) form.classList.add('modal-' + o.size);
    const closeBtn = F.iconBtn('x', t('common.close'), () => close());
    form.append(h('div', { class: 'modal-head' }, h('h2', { id: titleId }, o.title), closeBtn));
    const body = h('div', { class: 'modal-body' });
    append(body, [o.body]);
    form.append(body);
    const foot = h('div', { class: 'modal-foot' });
    let primary = null;
    for (const a of o.actions || [{ label: t('common.close'), kind: 'ghost' }]) {
      const b = F.btn(a.label, { kind: a.kind || 'ghost', icon: a.icon, type: a.primary ? 'submit' : 'button' });
      if (a.primary) primary = b;
      b.addEventListener('click', async (ev) => {
        ev.preventDefault();
        if (!a.onClick) return close();
        const r = await F.busy(b, () => a.onClick(api));
        if (r !== false) close();
      });
      foot.append(b);
    }
    if (foot.children.length) form.append(foot);
    form.addEventListener('submit', (ev) => { ev.preventDefault(); if (primary) primary.click(); });
    back.addEventListener('mousedown', (ev) => { if (ev.target === back) close(); });
    const onKey = (ev) => {
      if (ev.key === 'Escape') { ev.preventDefault(); close(); }
      else if (ev.key === 'Tab') {
        const f = [...form.querySelectorAll('button,input,select,textarea,a[href],[tabindex]:not([tabindex="-1"])')].filter((x) => !x.disabled && x.offsetParent !== null);
        if (!f.length) return;
        if (ev.shiftKey && document.activeElement === f[0]) { ev.preventDefault(); f[f.length - 1].focus(); }
        else if (!ev.shiftKey && document.activeElement === f[f.length - 1]) { ev.preventDefault(); f[0].focus(); }
      }
    };
    document.addEventListener('keydown', onKey);
    document.body.appendChild(back);
    document.body.classList.add('has-modal');
    requestAnimationFrame(() => requestAnimationFrame(() => back.classList.add('show')));
    const first = form.querySelector('.modal-body input:not([type=hidden]):not([type=checkbox]):not([type=file]),.modal-body select,.modal-body textarea') || primary || closeBtn;
    setTimeout(() => first && first.focus(), 40);
    let closed = false;
    function close() {
      if (closed) return; closed = true;
      document.removeEventListener('keydown', onKey);
      back.classList.remove('show');
      setTimeout(() => { back.remove(); if (!document.querySelector('.modal-back')) document.body.classList.remove('has-modal'); }, 200);
      if (prevFocus && prevFocus.isConnected && prevFocus.focus) prevFocus.focus();
      if (o.onClose) o.onClose();
    }
    const api = { close, el: form, body };
    return api;
  };
  F.confirm = function (msg, o) {
    o = o || {};
    return new Promise((resolve) => {
      let ok = false;
      F.modal({
        title: o.title || t('common.confirm'), size: 'sm',
        body: h('p', { class: 'confirm-text', text: msg }),
        actions: [
          { label: t('common.cancel'), kind: 'ghost' },
          { label: o.ok || t('common.yes'), kind: o.danger ? 'danger' : 'primary', primary: true, onClick: () => { ok = true; } },
        ],
        onClose: () => resolve(ok),
      });
    });
  };

  // ------------------------------------------------------------------ language
  F.setLang = function (lang) {
    if (!window.I18N[lang] || lang === S.lang) return;
    S.lang = lang;
    lsSet(LS_LANG, lang);
    document.documentElement.lang = lang;
    if (S.me) { buildShell(); renderRoute(); updateAll(); }
    else if (F.rerenderAuth) F.rerenderAuth();
  };
  /** native name of a language code ("Deutsch"); unknown codes are returned as-is */
  F.langName = (code) => (window.I18N[code] && window.I18N[code]['meta.name']) || window.I18N.en['lang.' + code] || code;
  F.flag = (code) => (window.Flags ? window.Flags.el(code) : h('span', { class: 'flag flag-none', 'aria-hidden': 'true', text: code }));

  /* Language dropdown (button + listbox). Rows show flag + native name; a native <select> can't show SVG flags.
     o: { langs, value, onChange(code), label, compact (button = flag + code), align: 'right' | 'left' }
     Keyboard: Enter/Space/ArrowDown/ArrowUp open; arrows/Home/End move; Enter/Space pick; Escape/Tab close;
     a letter jumps to the next language starting with it. Outside click closes. */
  F.langPicker = function (o) {
    let value = o.value;
    let active = -1;
    const langs = o.langs.slice();
    const lid = F.uid('lp');
    const btnFlag = h('span', { class: 'lp-flag' });
    const btnText = h('span', { class: 'lp-text' });
    const btn = h('button', { type: 'button', class: 'lp-btn' + (o.compact ? ' compact' : ' input'), 'aria-haspopup': 'listbox', 'aria-expanded': 'false', 'aria-controls': lid },
      btnFlag, btnText, icon('chevron', 'sm lp-chev'));
    const list = h('ul', { class: 'lp-list' + (o.align === 'left' ? ' left' : ''), role: 'listbox', id: lid, tabindex: '-1', hidden: true, 'aria-label': o.label });
    const rows = langs.map((code, i) => {
      const li = h('li', { class: 'lp-opt', role: 'option', id: lid + '-' + code, 'aria-selected': 'false', lang: code },
        F.flag(code), h('span', { class: 'lp-name', text: F.langName(code) }),
        o.tags && o.tags[code] ? h('span', { class: 'lp-tag', text: o.tags[code] }) : null, h('span', { class: 'lp-code', text: code }));
      li.addEventListener('mousemove', () => setActive(i));
      li.addEventListener('click', (e) => { e.stopPropagation(); pick(code); });
      return li;
    });
    list.append(...rows);
    const wrap = h('div', { class: 'lp' + (o.compact ? ' lp-compact' : ' lp-full') }, btn, list);

    function paint() {
      btnFlag.replaceChildren(F.flag(value));
      btnText.textContent = o.compact ? value : F.langName(value);
      const lbl = (o.label ? o.label + ': ' : '') + F.langName(value);
      btn.setAttribute('aria-label', lbl); btn.title = lbl;
      rows.forEach((r, i) => r.setAttribute('aria-selected', langs[i] === value ? 'true' : 'false'));
    }
    function setActive(i) {
      if (i < 0 || i >= rows.length) return;
      active = i;
      rows.forEach((r, j) => r.classList.toggle('active', j === i));
      list.setAttribute('aria-activedescendant', rows[i].id);
      const r = rows[i], top = r.offsetTop, bot = top + r.offsetHeight;
      if (top < list.scrollTop) list.scrollTop = top; else if (bot > list.scrollTop + list.clientHeight) list.scrollTop = bot - list.clientHeight;
    }
    function onDoc(e) { if (!wrap.contains(e.target)) close(false); }
    function open() {
      if (!list.hidden) return;
      list.hidden = false; btn.setAttribute('aria-expanded', 'true'); wrap.classList.add('open');
      setActive(Math.max(0, langs.indexOf(value)));
      list.focus({ preventScroll: true });
      document.addEventListener('pointerdown', onDoc, true);
    }
    function close(focusBtn) {
      if (list.hidden) return;
      list.hidden = true; btn.setAttribute('aria-expanded', 'false'); wrap.classList.remove('open');
      document.removeEventListener('pointerdown', onDoc, true);
      if (focusBtn) btn.focus();
    }
    function pick(code) {
      close(true);
      if (code === value) return;
      value = code; paint();
      if (o.onChange) o.onChange(code);
    }
    btn.addEventListener('click', (e) => { e.stopPropagation(); if (list.hidden) open(); else close(true); });
    btn.addEventListener('keydown', (e) => {
      if (['ArrowDown', 'ArrowUp', 'Enter', ' '].includes(e.key)) { e.preventDefault(); open(); if (e.key === 'ArrowUp') setActive(rows.length - 1); }
    });
    list.addEventListener('keydown', (e) => {
      const k = e.key;
      if (k === 'ArrowDown') { e.preventDefault(); setActive(Math.min(rows.length - 1, active + 1)); }
      else if (k === 'ArrowUp') { e.preventDefault(); setActive(Math.max(0, active - 1)); }
      else if (k === 'Home' || k === 'PageUp') { e.preventDefault(); setActive(0); }
      else if (k === 'End' || k === 'PageDown') { e.preventDefault(); setActive(rows.length - 1); }
      else if (k === 'Enter' || k === ' ') { e.preventDefault(); if (active >= 0) pick(langs[active]); }
      else if (k === 'Escape') { e.preventDefault(); e.stopPropagation(); close(true); }
      else if (k === 'Tab') close(false);
      else if (k.length === 1 && /\p{L}/u.test(k)) {
        const c = k.toLowerCase();
        for (let n = 1; n <= rows.length; n++) {
          const i = (active + n) % rows.length;
          if (F.langName(langs[i]).toLowerCase().startsWith(c) || langs[i].startsWith(c)) { setActive(i); break; }
        }
      }
    });
    list.addEventListener('focusout', (e) => { if (!wrap.contains(e.relatedTarget) && e.relatedTarget) close(false); });
    paint();
    wrap.set = (code) => { value = code; paint(); };
    wrap.value = () => value;
    return wrap;
  };

  /** panel language picker for the header and the login / setup screens */
  F.langSwitch = function () {
    return F.langPicker({ langs: Object.keys(window.I18N), value: S.lang, compact: true, label: t('common.language'),
      onChange: (code) => { F.setLang(code); const b = F.$('.lp-compact .lp-btn'); if (b) b.focus(); } });
  };

  // ------------------------------------------------------------------ bots / selection
  F.bot = (name) => S.bots.find((b) => b.name === name) || null;
  F.selectedBot = () => F.bot(S.selected);
  function ensureSelected() {
    if (S.bots.length && !F.bot(S.selected)) {
      const pick = S.bots.find((b) => b.live) || S.bots[0];
      S.selected = pick.name;
      lsSet(LS_BOT, S.selected);
    }
  }
  F.selectBot = function (name) {
    if (!name || name === S.selected) return;
    S.selected = name;
    lsSet(LS_BOT, name);
    updateBotSelect();
    mini.update();
    if (F.curRoute && F.curRoute.usesBot) renderRoute();
    else if (F.page && F.page.onBots) F.page.onBots(S.bots);
  };
  /** current playback position with client-side interpolation between polls */
  F.livePos = function (bot) {
    const p = bot && bot.live && bot.live.playing;
    if (!p) return 0;
    let pos = p.position || 0;
    if (!p.paused && bot.status === 'connected') pos += (performance.now() - S.botsAt) / 1000;
    if (p.length > 0) pos = Math.min(pos, p.length);
    return pos;
  };
  F.cleanTitle = (x) => String(x == null ? '' : x).replace(/^\p{Extended_Pictographic}️?\s*/u, '');
  F.botLabel = (b) => (b.nickname && b.nickname !== b.name ? b.nickname + ' (' + b.name + ')' : b.name);

  F.control = async function (botName, action, value, extra) {
    const body = Object.assign({ action }, extra || {});
    if (value !== undefined) body.value = value;
    try {
      await F.api('POST', 'bots/' + F.enc(botName) + '/control', body);
      F.refreshBots();
      return true;
    } catch (e) { F.fail(e); return false; }
  };

  // ------------------------------------------------------------------ add-song tracker
  F.track = { status: 'idle' };
  const trackListeners = new Set();
  F.onTrack = (fn) => { trackListeners.add(fn); return () => trackListeners.delete(fn); };
  function emitTrack() {
    mini.renderTrack();
    trackListeners.forEach((fn) => { try { fn(F.track); } catch (e) { console.error(e); } });
  }
  F.addSong = async function (botName, payload, now) {
    if (F.track.status === 'run') { F.toast(t('track.busy'), 'info'); return false; }
    const b = F.bot(botName);
    const lv = b && b.live;
    const tr = F.track = {
      status: 'run', bot: botName, title: payload.title || payload.query || '', step: 0, t0: Date.now(), foundAt: 0,
      prevTitle: lv && lv.playing ? lv.playing.title : null, prevCount: lv ? lv.queueCount : 0,
      wasPlaying: !!(lv && lv.playing), sawLoading: false, secs: 0, now: !!now, msg: '',
    };
    emitTrack();
    schedulePoll();
    try {
      const r = await F.api('POST', 'bots/' + F.enc(botName) + '/play', Object.assign({}, payload, { now: !!now }));
      if (F.track !== tr) return true;
      tr.step = 1; tr.foundAt = Date.now();
      if (r && r.title) tr.title = r.title;
      emitTrack();
      setTimeout(() => F.refreshBots(), 250);
      return true;
    } catch (e) {
      if (F.track === tr) finishTrack('err', F.errMsg(e));
      return false;
    }
  };
  function finishTrack(status, msg) {
    const tr = F.track;
    tr.status = status; tr.msg = msg || '';
    if (status === 'ok' || status === 'queued') tr.step = 3;
    if (status === 'ok' && msg) tr.title = msg;
    emitTrack();
    setTimeout(() => { if (F.track === tr) { F.track = { status: 'idle' }; emitTrack(); } }, status === 'err' ? 9000 : 5000);
    schedulePoll();
  }
  /* Steps: 0 searching (request in flight) → 1 found (POST returned) → 2 opening stream (live.loading)
     → 3 playing (live.playing appeared/changed) or "queued" (queue grew while something else plays). */
  function updateTrack() {
    const tr = F.track;
    if (tr.status !== 'run' || tr.step < 1) return;
    const b = F.bot(tr.bot);
    const lv = b && b.live;
    if (!lv) return finishTrack('err', t('err.bot_offline'));
    const queuedBehind = tr.wasPlaying && !tr.now;
    if (lv.loading) {
      tr.step = 2; tr.sawLoading = true; tr.secs = lv.loading.seconds || 0;
    } else if (lv.playing && (tr.sawLoading || (!queuedBehind && lv.playing.title !== tr.prevTitle))) {
      return finishTrack('ok', lv.playing.title);
    } else if (tr.sawLoading && !lv.playing) {
      return finishTrack('err', t('track.failed'));
    } else if (queuedBehind && lv.queueCount > tr.prevCount && Date.now() - tr.foundAt > 900) {
      return finishTrack('queued', t('track.queuedPos', { pos: lv.queueCount }));
    }
    if (Date.now() - tr.t0 > 90000) return finishTrack('err', t('err.timeout'));
    emitTrack();
  }
  F.trackSteps = () => [t('track.search'), t('track.found'), t('track.stream'), F.track.status === 'queued' ? t('track.queued') : t('track.playing')];
  F.trackText = function (tr) {
    if (tr.status === 'err') return tr.msg || t('track.failed');
    if (tr.status === 'queued') return t('track.queued') + ' · ' + tr.msg;
    if (tr.status === 'ok') return t('track.playing') + ': ' + (tr.msg || tr.title);
    if (tr.step === 0) return t('track.search') + '… ' + tr.title;
    if (tr.step === 1) return t('track.found') + ': ' + tr.title;
    return t('track.stream') + '… ' + (tr.secs ? t('track.secs', { s: tr.secs.toFixed(1) }) : '');
  };

  // ------------------------------------------------------------------ polling
  let pollTimer = null, pollBusy = false, tickTimer = null;
  F.refreshBots = async function () {
    if (!S.me || pollBusy) return;
    pollBusy = true;
    try {
      const bots = await F.api('GET', 'bots');
      S.bots = Array.isArray(bots) ? bots : [];
      // the server prefixes radio titles with an emoji; the panel uses its own icons instead
      for (const b of S.bots) { const p = b.live && b.live.playing; if (p && p.title) p.title = F.cleanTitle(p.title); }
      S.botsLoaded = true;
      S.botsAt = performance.now();
      setOnline(true);
      updateAll();
    } catch (e) {
      if (e.code === 'network' || e.status >= 500) setOnline(false);
    } finally { pollBusy = false; }
  };
  function updateAll() {
    ensureSelected();
    updateBotSelect();
    updateTrack();
    mini.update();
    if (F.page && F.page.onBots) { try { F.page.onBots(S.bots); } catch (e) { console.error(e); } }
  }
  function schedulePoll() {
    clearTimeout(pollTimer);
    if (!S.me) return;
    const iv = F.track.status === 'run' ? 700 : document.hidden ? 10000 : 2000;
    pollTimer = setTimeout(async () => { await F.refreshBots(); schedulePoll(); }, iv);
  }
  document.addEventListener('visibilitychange', () => { if (S.me && !document.hidden) { F.refreshBots(); schedulePoll(); } });
  function setOnline(on) {
    if (S.online === on) return;
    S.online = on;
    const b = F.$('#conn-banner');
    if (b) b.hidden = on;
  }

  /** poll /state until the program is back (after restart / update) */
  F.waitForRestart = function (title) {
    const msg = h('p', { class: 'muted', text: t('settings.restartWait') });
    const box = h('div', { class: 'restart-overlay', role: 'alertdialog', 'aria-live': 'assertive' },
      h('div', { class: 'restart-card' }, h('div', { class: 'spinner big' }), h('h2', { text: title || t('settings.restarting') }), msg));
    document.body.appendChild(box);
    clearTimeout(pollTimer);
    S.me = null; // stop polling while down
    const t0 = Date.now();
    let wentDown = false;
    const check = async () => {
      try {
        const r = await fetch('/panel/api/state', { credentials: 'same-origin', cache: 'no-store' });
        if (r.ok && (wentDown || Date.now() - t0 > 20000)) { location.reload(); return; }
        if (!r.ok) wentDown = true;
      } catch (_) { wentDown = true; }
      if (Date.now() - t0 > 120000) msg.textContent = t('settings.restartSlow');
      setTimeout(check, 2000);
    };
    setTimeout(check, 2500);
  };

  // ------------------------------------------------------------------ shell
  const NAV = [
    { path: '/', key: 'dashboard', icon: 'grid' },
    { path: '/bots', key: 'bots', icon: 'server' },
    { path: '/player', key: 'player', icon: 'music', usesBot: true },
    { path: '/playlists', key: 'playlists', icon: 'playlist' },
    { path: '/radios', key: 'radios', icon: 'radio' },
    { path: '/stats', key: 'stats', icon: 'chart' },
    { path: '/plugins', key: 'plugins', icon: 'box', admin: true, sub: true, children: [['/plugins', 'plugins.all'], ['/plugins/custom', 'commands.custom'], ['/plugins/texts', 'commands.tabTexts']] },
    { path: '/users', key: 'users', icon: 'users', admin: true },
    { path: '/settings', key: 'settings', icon: 'sliders', admin: true, sub: true, children: [['/settings/general', 'settings.sec.general'], ['/settings/youtube', 'settings.sec.youtube'], ['/settings/updates', 'settings.sec.updates'], ['/settings/maintenance', 'settings.sec.maintenance']] },
    { path: '/logs', key: 'logs', icon: 'file', admin: true, sub: true, children: [['/logs/live', 'logs.tabLive'], ['/logs/history', 'logs.tabHistory'], ['/logs/audit', 'settings.sec.audit']] },
    { path: '/faq', key: 'faq', icon: 'help' },
  ];
  const ROUTES = NAV.concat([{ path: '/account', key: 'account', icon: 'user' }]);

  let botSel = null, botDot = null;
  function updateBotSelect() {
    if (!botSel) return;
    const sig = S.bots.map((b) => b.name + '|' + b.nickname).join(',');
    if (botSel.dataset.sig !== sig) {
      botSel.dataset.sig = sig;
      botSel.replaceChildren(...S.bots.map((b) => h('option', { value: b.name, text: F.botLabel(b) })));
      botSel.disabled = !S.bots.length;
    }
    if (S.selected) botSel.value = S.selected;
    const b = F.selectedBot();
    if (botDot) {
      botDot.className = 'dot dot-' + (b ? b.status : 'offline');
      botDot.title = b ? t('status.' + b.status) : '';
    }
  }

  // ------------------------------------------------------------------ theme
  const LS_THEME = 'frix.theme';
  F.theme = () => (document.documentElement.dataset.theme === 'krem' ? 'krem' : 'lacivert');
  F.setTheme = function (th) {
    document.documentElement.dataset.theme = th;
    lsSet(LS_THEME, th);
    const m = document.querySelector('meta[name=theme-color]');
    if (m) m.content = th === 'krem' ? '#f3f3f8' : '#131316';
    document.querySelectorAll('.theme-btn').forEach(paintThemeBtn);
    document.querySelectorAll('select.theme-sel').forEach((x) => { x.value = th; });
  };
  function paintThemeBtn(b) {
    const dark = F.theme() === 'lacivert';
    b.replaceChildren(icon(dark ? 'sun' : 'moon'));
    const label = dark ? t('theme.toKrem') : t('theme.toLacivert');
    b.setAttribute('aria-label', label); b.title = label;
  }
  F.themeBtn = function () {
    const b = h('button', { type: 'button', class: 'btn-icon theme-btn', onclick: () => F.setTheme(F.theme() === 'krem' ? 'lacivert' : 'krem') });
    paintThemeBtn(b);
    return b;
  };
  F.setTheme(lsGet(LS_THEME) === 'krem' ? 'krem' : 'lacivert');

  F.brand = (sub) => h('span', { class: 'brand-mark' }, h('img', { src: '/logo.svg', alt: '', width: 32, height: 32 }),
    h('span', { class: 'brand-text' }, h('span', { class: 'wordmark', text: 'frix' }), sub ? h('span', { class: 'brand-sub', text: 'teamspeak 3 bot' }) : null));

  function buildShell() {
    const app = F.$('#app');
    app.replaceChildren();
    app.className = 'app';
    botSel = h('select', { class: 'input bot-select', 'aria-label': t('top.botSelect'), title: t('top.botSelect'), onchange: (e) => F.selectBot(e.target.value) });
    botSel.dataset.sig = '';
    botDot = h('span', { class: 'dot' });
    const me = S.me;
    const userMenu = h('div', { class: 'menu', hidden: true, role: 'menu' },
      h('div', { class: 'menu-head' }, h('strong', { text: me.name }), F.badge(t('role.' + me.role), me.role)),
      h('a', { href: '/account', 'data-link': '', role: 'menuitem', class: 'menu-item' }, icon('key'), h('span', { text: t('nav.account') })),
      h('button', { type: 'button', role: 'menuitem', class: 'menu-item', onclick: logout }, icon('logout'), h('span', { text: t('top.logout') })));
    const userBtn = h('button', { type: 'button', class: 'user-btn', 'aria-haspopup': 'menu', 'aria-expanded': 'false', 'aria-label': t('top.userMenu') },
      h('span', { class: 'avatar', text: (me.name[0] || '?').toLowerCase() }), h('span', { class: 'user-name', text: me.name }), icon('chevron', 'sm'));
    userBtn.addEventListener('click', (e) => { e.stopPropagation(); togglePop(userMenu, userBtn); });

    // left sidebar: brand, grouped navigation, "add song" button; on narrow screens it becomes a drawer
    const admin = F.isAdmin();
    const byKey = (k) => NAV.find((n) => n.key === k);
    const groups = [
      ['music', ['dashboard', 'bots', 'player', 'playlists', 'radios']],
      ['manage', admin ? ['plugins', 'users', 'settings', 'logs'] : []],
      ['other', ['stats', 'faq']],
    ].filter((g) => g[1].length);
    // items with children open a sub-menu under them (instead of tabs inside the page)
    const link = (k) => {
      const n = byKey(k);
      if (!n.children) {
        return h('li', null, h('a', { href: n.path, 'data-link': '', class: 'side-item', 'data-path': n.path },
          icon(n.icon), h('span', { text: t('nav.' + n.key) })));
      }
      const subId = F.uid('sub');
      const subList = h('ul', { class: 'side-sub', id: subId, hidden: true },
        n.children.map(([path, key]) => h('li', null, h('a', { href: path, 'data-link': '', class: 'side-subitem', 'data-sub': path }, h('span', { text: t(key) })))));
      const btn = h('button', { type: 'button', class: 'side-item side-parent', 'data-parent': n.path, 'aria-expanded': 'false', 'aria-controls': subId },
        icon(n.icon), h('span', { text: t('nav.' + n.key) }), h('span', { class: 'side-chev' }, icon('chevron', 'sm')));
      btn.addEventListener('click', () => {
        const open = btn.getAttribute('aria-expanded') !== 'true';
        btn.setAttribute('aria-expanded', open ? 'true' : 'false');
        subList.hidden = !open;
        if (open && !location.pathname.startsWith(n.path)) F.go(n.children[0][0]);
      });
      return h('li', { class: 'side-has-sub' }, btn, subList);
    };
    const side = h('aside', { class: 'sidebar', id: 'sidebar', 'aria-label': t('nav.main') },
      h('div', { class: 'side-brand' }, h('a', { href: '/', 'data-link': '', class: 'brand', 'aria-label': 'frix' }, F.brand(true))),
      h('div', { class: 'side-bot' }, h('div', { class: 'side-group-title', text: t('top.botSelect') }), h('label', { class: 'bot-select-wrap' }, botDot, botSel)),
      h('nav', { class: 'sidenav' }, groups.map(([g, keys]) => h('div', { class: 'side-group' },
        h('div', { class: 'side-group-title', text: t('nav.group.' + g) }), h('ul', null, keys.map(link))))),
      h('div', { class: 'side-player', id: 'side-player' }),
      h('div', { class: 'side-foot muted small', text: 'frix ' + (S.version ? 'v' + S.version : '') }));
    const scrim = h('div', { class: 'side-scrim', onclick: () => closeDrawer() });
    const burger = h('button', { type: 'button', class: 'btn-icon burger', 'aria-label': t('nav.menu'), title: t('nav.menu'), 'aria-controls': 'sidebar', 'aria-expanded': 'false',
      onclick: () => (document.body.classList.contains('drawer-open') ? closeDrawer() : openDrawer()) }, icon('menu'));
    function openDrawer() { document.body.classList.add('drawer-open'); burger.setAttribute('aria-expanded', 'true'); const f = side.querySelector('a,button'); if (f) f.focus(); }
    closeDrawer = function () { if (!document.body.classList.contains('drawer-open')) return; document.body.classList.remove('drawer-open'); burger.setAttribute('aria-expanded', 'false'); };

    const top = h('header', { class: 'topbar' },
      h('div', { class: 'top-left' }, burger, h('div', { class: 'top-heading' }, h('h1', { class: 'top-title', id: 'top-title' }), h('p', { class: 'top-sub', id: 'top-sub' }))),
      h('div', { class: 'top-actions' },
        h('div', { class: 'top-page-actions', id: 'top-page-actions' }),
        h('span', { class: 'top-sep' }),
        F.themeBtn(),
        h('div', { class: 'menu-wrap' }, userBtn, userMenu)));

    const banner = h('div', { id: 'conn-banner', class: 'conn-banner', hidden: S.online, role: 'status' }, h('span', { class: 'spinner' }), h('span', { text: t('top.offline') }));
    const main = h('main', { id: 'main', class: 'main', tabindex: '-1' });
    const skip = h('a', { href: '#main', class: 'skip', text: t('common.skip'), onclick: (e) => { e.preventDefault(); main.focus(); } });
    const dock = h('div', { class: 'dock' });
    app.append(skip, side, scrim, h('div', { class: 'content' }, top, banner, main, dock));
    // now-playing card: bottom of the sidebar on desktop, pinned to the bottom on phones
    const miniEl = mini.build();
    const mq = matchMedia('(max-width: 999px)');
    const place = () => (mq.matches ? dock : side.querySelector('#side-player')).append(miniEl);
    place();
    mq.onchange = place;
    updateBotSelect();
  }
  let closeDrawer = () => {};

  let openPop = null;
  function togglePop(menu, btn) {
    const willOpen = menu.hidden;
    closePops();
    if (willOpen) {
      menu.hidden = false; btn.setAttribute('aria-expanded', 'true'); openPop = { menu, btn };
      const f = menu.querySelector('a,button'); if (f) f.focus();
    }
  }
  function closePops() {
    if (openPop) { openPop.menu.hidden = true; openPop.btn.setAttribute('aria-expanded', 'false'); openPop = null; }
  }
  document.addEventListener('click', (e) => {
    const a = e.target.closest && e.target.closest('a[data-link]');
    if (a && !e.ctrlKey && !e.metaKey && !e.shiftKey && e.button === 0) {
      e.preventDefault();
      closePops();
      F.go(a.getAttribute('href'));
      return;
    }
    if (openPop && !openPop.menu.contains(e.target)) closePops();
  });
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && openPop) { const b = openPop.btn; closePops(); b.focus(); return; }
    if (e.key === 'Escape' && document.body.classList.contains('drawer-open')) { closeDrawer(); const bb = F.$('.burger'); if (bb) bb.focus(); }
  });

  // ------------------------------------------------------------------ router
  F.go = function (path) {
    if (path !== location.pathname) history.pushState(null, '', path);
    renderRoute();
    window.scrollTo({ top: 0 });
  };
  window.addEventListener('popstate', () => { if (S.me) renderRoute(); });

  function renderRoute() {
    if (!S.me) return;
    // same page, different sub-section (e.g. /settings/youtube): let the page switch without a full re-render
    if (F.page && F.page.onSubRoute && F.curRoute && F.curRoute.sub && F.pageMain && F.pageMain === F.$('#main')) {
      const p = location.pathname.replace(/\/+$/, '') || '/';
      if (p === F.curRoute.path || p.startsWith(F.curRoute.path + '/')) { F.page.onSubRoute(); return; }
    }
    if (F.page && F.page.destroy) { try { F.page.destroy(); } catch (e) { console.error(e); } }
    F.page = null;
    const path = location.pathname.replace(/\/+$/, '') || '/';
    if (path === '/commands') { history.replaceState(null, '', '/plugins/custom'); return renderRoute(); }
    let r = ROUTES.find((x) => x.path === path || (x.sub && path.startsWith(x.path + '/')));
    if (!r) { history.replaceState(null, '', '/'); r = ROUTES[0]; }
    F.curRoute = r;
    document.title = t('nav.' + r.key) + ' · frix';
    document.querySelectorAll('.sidenav [data-path]').forEach((a) => {
      if (a.dataset.path === r.path) a.setAttribute('aria-current', 'page'); else a.removeAttribute('aria-current');
    });
    // sub-menus: open the one of the current page, mark the current child
    document.querySelectorAll('.sidenav .side-parent').forEach((b) => {
      const mine = b.dataset.parent === r.path;
      b.classList.toggle('active', mine);
      if (mine) { b.setAttribute('aria-expanded', 'true'); b.nextElementSibling.hidden = false; }
    });
    const subs = [...document.querySelectorAll('.sidenav [data-sub]')];
    const best = subs.filter((a) => path === a.dataset.sub || path.startsWith(a.dataset.sub + '/')).sort((x, y) => y.dataset.sub.length - x.dataset.sub.length)[0]
      || subs.find((a) => r.children && a.dataset.sub === r.children[0][0] && (path === r.path));
    subs.forEach((a) => { if (a === best) a.setAttribute('aria-current', 'page'); else a.removeAttribute('aria-current'); });
    const tt = F.$('#top-title'); if (tt) tt.textContent = t('nav.' + r.key);
    const tsb = F.$('#top-sub'); if (tsb) tsb.textContent = '';
    const tpa = F.$('#top-page-actions'); if (tpa) tpa.replaceChildren();
    closeDrawer();
    const main = F.$('#main');
    main.replaceChildren();
    main.classList.remove('enter'); void main.offsetWidth; main.classList.add('enter');
    if (r.admin && !F.isAdmin()) { main.append(F.empty('lock', t('common.forbidden'))); return; }
    const fn = Pages[r.key];
    F.pageMain = main;
    try { F.page = (fn && fn(main)) || {}; }
    catch (e) { console.error(e); main.append(F.empty('alert', t('err.unknown'), String(e && e.message))); F.page = {}; }
    if (S.botsLoaded && F.page.onBots) F.page.onBots(S.bots);
  }
  F.renderRoute = renderRoute;

  // ------------------------------------------------------------------ mini player (thin strip above the bar)
  const mini = (() => {
    let el, cover, title, sub, fill, time, playBtn, nextBtn, lastSig = '';
    function build() {
      cover = h('div', { class: 'mini-cover' });
      title = h('a', { href: '/player', 'data-link': '', class: 'mini-title' });
      sub = h('span', { class: 'mini-sub' });
      fill = h('div', { class: 'mini-fill' });
      const line = h('div', { class: 'mini-line', 'aria-hidden': 'true' }, fill);
      time = h('span', { class: 'mini-time' });
      playBtn = F.iconBtn('play', t('player.play'), () => { const b = F.selectedBot(); if (b) F.control(b.name, 'pause'); }, 'sm');
      nextBtn = F.iconBtn('next', t('player.next'), () => { const b = F.selectedBot(); if (b) F.control(b.name, 'next'); }, 'sm');
      el = h('section', { class: 'mini', 'aria-label': t('mini.label') },
        line, cover, h('div', { class: 'mini-info' }, title, sub), time,
        h('div', { class: 'mini-ctrls' }, playBtn, nextBtn));
      lastSig = '';
      update();
      return el;
    }
    function setCover(p) {
      const key = p ? (p.thumbnail || (p.radio ? 'radio' : 'music')) : 'none';
      if (cover.dataset.key === key) return;
      cover.dataset.key = key;
      cover.replaceChildren(p && p.thumbnail ? h('img', { src: p.thumbnail, alt: '', loading: 'lazy' }) : p && p.radio ? icon('radio') : h('img', { src: '/logo.svg', alt: '' }));
    }
    function update() {
      if (!el) return;
      const b = F.selectedBot();
      const lv = b && b.live;
      const p = lv && lv.playing;
      const can = F.canControl() && !!lv;
      playBtn.disabled = !can || !p;
      nextBtn.disabled = !can || !lv.queueCount;
      playBtn.hidden = nextBtn.hidden = !F.canControl();
      const tr = F.track;
      const tracking = tr.status !== 'idle';
      const sig = (b ? b.name + b.status : '') + '|' + (p ? p.title + p.paused + p.thumbnail + p.requestedBy : '') + S.lang + tr.status + tr.step + tr.secs + tr.title;
      if (sig !== lastSig) {
        lastSig = sig;
        setCover(p);
        if (!b) { title.textContent = S.botsLoaded ? t('mini.noBots') : t('common.loading'); sub.textContent = ''; }
        else if (!lv) { title.textContent = t('mini.botOff'); sub.textContent = F.botLabel(b); }
        else if (!p) { title.textContent = t('mini.idle'); sub.textContent = F.botLabel(b); }
        else { title.textContent = p.title; sub.textContent = F.botLabel(b); }
        el.classList.toggle('tracking', tracking);
        el.classList.toggle('track-err', tr.status === 'err');
        if (tracking) { title.textContent = tr.title || title.textContent; sub.textContent = F.trackText(tr); }
        title.title = title.textContent;
        const paused = !p || p.paused;
        playBtn.replaceChildren(icon(paused ? 'play' : 'pause'));
        playBtn.setAttribute('aria-label', paused ? t('player.play') : t('player.pause'));
        playBtn.title = playBtn.getAttribute('aria-label');
      }
      tick();
    }
    function tick() {
      if (!el) return;
      const tr = F.track;
      if (tr.status !== 'idle') {
        fill.style.width = (tr.status === 'err' ? 100 : [12, 40, 72, 100][tr.step]) + '%';
        time.textContent = '';
        return;
      }
      const b = F.selectedBot();
      const p = b && b.live && b.live.playing;
      if (p && p.live === true) { fill.style.width = '100%'; time.textContent = t('player.live').toLowerCase(); }
      else if (p && p.length > 0) { const pos = F.livePos(b); fill.style.width = (100 * pos / p.length).toFixed(2) + '%'; time.textContent = F.fmtTime(pos) + ' / ' + F.fmtTime(p.length); }
      else if (p) { fill.style.width = '0%'; time.textContent = F.fmtTime(F.livePos(b)) + ' / --:--'; }
      else { fill.style.width = '0%'; time.textContent = ''; }
    }
    function renderTrack() { if (el) { lastSig = ''; update(); } }
    return { build, update, tick, renderTrack };
  })();

  // ------------------------------------------------------------------ shared: add-song box + stepper
  /** ara → bulundu → akış açılıyor → çalıyor, driven by F.track */
  F.buildStepper = function () {
    const head = h('div', { class: 'stepper-head' });
    const steps = h('ol', { class: 'steps' });
    const msg = h('div', { class: 'stepper-msg' });
    const el = h('div', { class: 'stepper', hidden: true, role: 'status', 'aria-live': 'polite' }, head, steps, msg);
    function update(tr) {
      if (tr.status === 'idle') { el.hidden = true; return; }
      el.hidden = false;
      el.className = 'stepper st-' + tr.status;
      head.replaceChildren(h('span', { class: 'stepper-title', text: tr.title || '…' }),
        tr.status === 'run' && tr.step === 2 && tr.secs ? h('span', { class: 'mono muted small', text: t('track.secs', { s: tr.secs.toFixed(1) }) }) : null);
      steps.replaceChildren(...F.trackSteps().map((l, i) => {
        const st = tr.status === 'err' && i === tr.step ? 'fail' : i < tr.step || (i === tr.step && tr.status !== 'run') ? 'done' : i === tr.step ? 'active' : '';
        return h('li', { class: st }, h('span', { class: 'step-label', text: l }));
      }));
      msg.textContent = tr.status === 'run' ? '' : F.trackText(tr);
    }
    return { el, update };
  };

  /** search/link input + results + stepper for one bot */
  F.buildAdder = function (botName) {
    const q = F.input({ placeholder: t('player.addPh'), 'aria-label': t('player.addLabel'), maxlength: 300, type: 'search', enterkeyhint: 'search' });
    const go = F.btn(t('player.search'), { kind: 'primary', icon: 'search', type: 'submit' });
    const results = h('div', { class: 'results', 'aria-live': 'polite' });
    const stepper = F.buildStepper();
    const form = h('form', { class: 'add-form', role: 'search' }, h('div', { class: 'input-ic' }, icon('search'), q), go);
    form.addEventListener('submit', (e) => {
      e.preventDefault();
      e.stopPropagation();
      const v = q.value.trim();
      if (!v) { q.focus(); return; }
      if (/^https?:\/\//i.test(v)) {
        results.replaceChildren();
        F.addSong(botName, { query: v }, false).then((ok) => { if (ok) q.value = ''; });
        return;
      }
      F.busy(go, async () => {
        results.replaceChildren(...F.skeleton(3, 'sk-row'));
        try {
          const list = await F.api('GET', 'search?q=' + F.enc(v) + '&bot=' + F.enc(botName));
          render(list || [], v);
        } catch (er) { results.replaceChildren(); F.fail(er); }
      });
    });
    function render(list, query) {
      if (!list.length) { results.replaceChildren(F.empty('search', t('player.noResults'))); return; }
      results.replaceChildren(
        h('div', { class: 'results-head' }, h('span', { class: 'mono muted small', text: t('player.resultsFor', { q: query, n: list.length }) }),
          F.btn(t('common.close'), { kind: 'link', onClick: () => results.replaceChildren() })),
        h('ul', { class: 'result-list' }, list.map((r) => h('li', { class: 'result' },
          h('div', { class: 'result-thumb' }, r.thumbnail ? h('img', { src: r.thumbnail, alt: '', loading: 'lazy' }) : icon('music')),
          h('div', { class: 'result-title', text: r.title || r.id, title: r.title || r.id }),
          h('div', { class: 'result-actions' },
            F.btn(t('player.add'), { kind: 'ghost', icon: 'plus', onClick: () => F.addSong(botName, { id: r.id, title: r.title }, false) }),
            F.btn(t('player.playNow'), { kind: 'primary', icon: 'play', onClick: () => F.addSong(botName, { id: r.id, title: r.title }, true) }))))));
    }
    stepper.update(F.track);
    const off = F.onTrack((tr) => stepper.update(tr));
    const el = h('div', { class: 'adder' }, form, stepper.el, results);
    return { el, destroy: off, focus: () => q.focus() };
  };
  F.mini = mini;

  // ------------------------------------------------------------------ auth views
  function stopApp() {
    S.me = null;
    clearTimeout(pollTimer); clearInterval(tickTimer);
    if (F.page && F.page.destroy) { try { F.page.destroy(); } catch (_) { /* */ } }
    F.page = null;
    document.querySelectorAll('.modal-back').forEach((m) => m.remove());
    document.body.classList.remove('has-modal');
  }
  F.onUnauthorized = function () {
    if (!S.me && F.$('.auth')) return;
    stopApp();
    renderLogin();
  };
  async function logout() {
    try { await F.api('POST', 'logout', undefined, { no401: true }); } catch (_) { /* ignore */ }
    stopApp();
    history.replaceState(null, '', '/');
    renderLogin();
  }

  function authFrame(...content) {
    const app = F.$('#app');
    app.className = 'auth';
    app.replaceChildren(h('div', { class: 'auth-bg', 'aria-hidden': 'true' }),
      h('main', { class: 'auth-card' },
        h('div', { class: 'auth-top' }, h('div', { class: 'auth-brand' }, F.brand(true)), h('div', { class: 'row gap-s' }, F.themeBtn())),
        content,
        h('div', { class: 'auth-foot muted', text: S.version ? 'v' + S.version : '' })));
  }
  function pwInput(props) {
    const inp = F.input(Object.assign({ type: 'password' }, props));
    const btn = h('button', { type: 'button', class: 'pw-eye', 'aria-label': t('common.showPassword'), title: t('common.showPassword'), 'aria-pressed': 'false' }, icon('eye'));
    btn.addEventListener('click', () => {
      const show = inp.type === 'password';
      inp.type = show ? 'text' : 'password';
      btn.setAttribute('aria-pressed', show ? 'true' : 'false');
    });
    const w = h('div', { class: 'pw-wrap' }, inp, btn);
    w.input = inp;
    return w;
  }
  F.pwInput = pwInput;

  function renderLogin(prefill) {
    document.title = t('login.title') + ' · frix';
    const user = F.input({ name: 'username', autocomplete: 'username', required: true, value: (prefill && prefill.user) || '' });
    F.rerenderAuth = () => renderLogin({ user: user.value });
    const pass = pwInput({ name: 'password', autocomplete: 'current-password', required: true });
    const err = h('div', { class: 'form-error', role: 'alert' });
    const submit = F.btn(t('login.submit'), { kind: 'primary', type: 'submit', cls: 'btn-block', icon: 'login' });
    const form = h('form', { class: 'auth-form', novalidate: true },
      h('h1', { text: t('login.title') }), h('p', { class: 'muted', text: t('login.subtitle') }),
      F.field(t('login.user'), user), F.field(t('login.pass'), pass), err, submit);
    form.addEventListener('submit', (e) => {
      e.preventDefault();
      err.textContent = '';
      if (!user.value.trim() || !pass.input.value) { err.textContent = t('login.fill'); return; }
      F.busy(submit, async () => {
        try {
          await F.api('POST', 'login', { user: user.value.trim(), pass: pass.input.value }, { no401: true });
          await afterAuth();
        } catch (ex) {
          err.textContent = F.errMsg(ex);
          pass.input.select();
        }
      });
    });
    authFrame(form);
    setTimeout(() => (user.value ? pass.input : user).focus(), 30);
  }

  function renderSetup(st, step, draft, focusLang) {
    draft = draft || { lang: S.lang, user: '', pass: '', pass2: '', code: '' };
    step = step || 1;
    document.title = t('setup.title') + ' · frix';
    const steps = h('ol', { class: 'wizard-steps', 'aria-label': t('setup.progress') },
      [t('setup.step1'), t('setup.step2')].map((s, i) => h('li', { class: i + 1 === step ? 'on' : i + 1 < step ? 'done' : '', 'aria-current': i + 1 === step ? 'step' : null }, h('span', { class: 'wz-num', text: String(i + 1) }), h('span', { text: s }))));
    if (step === 1) {
      F.rerenderAuth = () => { draft.lang = S.lang; renderSetup(st, 1, draft); };
      const codes = Object.keys(window.I18N);
      const choose = (code) => {
        if (code === draft.lang) return;
        draft.lang = code; S.lang = code; lsSet(LS_LANG, code); document.documentElement.lang = code; renderSetup(st, 1, draft, true);
      };
      // radio group: one tab stop (the selected card), arrow keys move + select, like native radios
      const choices = h('div', { class: 'lang-cards', role: 'radiogroup', 'aria-label': t('setup.chooseLang') },
        codes.map((code) => {
          const on = draft.lang === code;
          return h('button', { type: 'button', role: 'radio', 'aria-checked': on ? 'true' : 'false', tabindex: on ? '0' : '-1', class: 'lang-card' + (on ? ' on' : ''), lang: code,
            'data-code': code, onclick: () => choose(code) },
            F.flag(code), h('span', { class: 'lang-card-text' }, h('strong', { text: F.langName(code) }), h('span', { class: 'muted', text: window.I18N[code]['meta.desc'] || '' })),
            h('span', { class: 'lang-code', text: code }));
        }));
      choices.addEventListener('keydown', (e) => {
        const d = { ArrowDown: 1, ArrowRight: 1, ArrowUp: -1, ArrowLeft: -1 }[e.key];
        if (!d) return;
        e.preventDefault();
        choose(codes[(codes.indexOf(draft.lang) + d + codes.length) % codes.length]);
      });
      const next = F.btn(t('setup.next'), { kind: 'primary', cls: 'btn-block', onClick: () => renderSetup(st, 2, draft) });
      authFrame(h('div', { class: 'auth-form' }, h('h1', { text: t('setup.title') }), h('p', { class: 'muted', text: t('setup.welcome') }), steps,
        h('h2', { class: 'h3', text: t('setup.chooseLang') }), choices, h('p', { class: 'help setup-note', text: t('setup.cmdNote') }), next));
      setTimeout(() => (focusLang ? choices.querySelector('.lang-card.on') || next : next).focus(), 30);
      return;
    }
    const user = F.input({ autocomplete: 'username', value: draft.user, maxlength: 32 });
    const pass = pwInput({ autocomplete: 'new-password', value: draft.pass, maxlength: 200 });
    const pass2 = pwInput({ autocomplete: 'new-password', value: draft.pass2, maxlength: 200 });
    const code = F.input({ autocomplete: 'one-time-code', value: draft.code, maxlength: 64 });
    const err = h('div', { class: 'form-error', role: 'alert' });
    const save = () => { draft.user = user.value; draft.pass = pass.input.value; draft.pass2 = pass2.input.value; draft.code = code.value; };
    F.rerenderAuth = () => { save(); draft.lang = S.lang; renderSetup(st, 2, draft); };
    const submit = F.btn(t('setup.create'), { kind: 'primary', type: 'submit', icon: 'check' });
    const back = F.btn(t('setup.back'), { kind: 'ghost', onClick: () => { save(); renderSetup(st, 1, draft); } });
    const form = h('form', { class: 'auth-form', novalidate: true },
      h('h1', { text: t('setup.title') }), steps,
      h('p', { class: 'muted', text: t('setup.accountIntro') }),
      F.field(t('setup.user'), user, t('setup.userHelp')),
      F.field(t('setup.pass'), pass, t('setup.passHelp')),
      F.field(t('setup.pass2'), pass2),
      st.setupNeedsCode ? h('div', { class: 'callout callout-warn' }, icon('key'), h('div', null, h('strong', { text: t('setup.codeTitle') }), h('p', null, F.rich(t('setup.codeHelp'))))) : null,
      st.setupNeedsCode ? F.field(t('setup.code'), code) : null,
      err, h('div', { class: 'row gap end' }, back, submit));
    form.addEventListener('submit', (e) => {
      e.preventDefault();
      save();
      err.textContent = '';
      const name = draft.user.trim();
      if (!/^[\p{L}\p{N}_.-]{3,32}$/u.test(name)) { err.textContent = t('err.bad_username'); user.focus(); return; }
      if (draft.pass.length < 8 || draft.pass.length > 200) { err.textContent = t('err.weak_password'); pass.input.focus(); return; }
      if (draft.pass !== draft.pass2) { err.textContent = t('setup.mismatch'); pass2.input.focus(); return; }
      if (st.setupNeedsCode && !draft.code.trim()) { err.textContent = t('setup.needCode'); code.focus(); return; }
      F.busy(submit, async () => {
        try {
          const body = { user: name, pass: draft.pass, lang: draft.lang };
          if (st.setupNeedsCode) body.code = draft.code.trim();
          await F.api('POST', 'setup', body, { no401: true });
          F.toast(t('setup.done'));
          await afterAuth();
        } catch (ex) { err.textContent = F.errMsg(ex); }
      });
    });
    authFrame(form);
    setTimeout(() => user.focus(), 30);
  }

  async function afterAuth() {
    const st = await F.api('GET', 'state', undefined, { no401: true });
    if (!st.user) { renderLogin(); return; }
    startApp(st);
  }

  function startApp(st) {
    F.rerenderAuth = null;
    S.me = st.user;
    S.version = st.version || S.version;
    S.online = true;
    buildShell();
    renderRoute();
    F.refreshBots().then(schedulePoll);
    clearInterval(tickTimer);
    tickTimer = setInterval(() => { mini.tick(); if (F.page && F.page.onTick) F.page.onTick(); }, 500);
  }

  async function boot() {
    // the panel shows the bot's single language (Settings → Language); the browser's last value is only a fallback until the server answers
    const saved = lsGet(LS_LANG);
    if (saved && window.I18N[saved]) S.lang = saved;
    document.documentElement.lang = S.lang;
    let st;
    try { st = await F.api('GET', 'state', undefined, { no401: true }); }
    catch (_) {
      const app = F.$('#app');
      app.className = 'auth';
      app.replaceChildren(h('main', { class: 'auth-card' }, F.empty('alert', t('boot.fail'), t('boot.failText'), F.btn(t('common.retry'), { kind: 'primary', icon: 'refresh', onClick: boot }))));
      return;
    }
    if (!st.setupRequired && st.lang && window.I18N[st.lang]) { S.lang = st.lang; lsSet(LS_LANG, st.lang); }
    document.documentElement.lang = S.lang;
    S.version = st.version || '';
    if (st.setupRequired) renderSetup(st);
    else if (!st.user) renderLogin();
    else startApp(st);
  }
  document.addEventListener('DOMContentLoaded', boot);
})();
