/* Frix panel – pages: plugins, commands/texts, users, account, settings, logs, faq */
'use strict';
(() => {
  const F = window.F;
  const Pages = window.Pages;
  const h = F.h, t = F.t, icon = F.icon, enc = F.enc;
  const lsGet = (k) => { try { return localStorage.getItem(k); } catch (_) { return null; } };
  const lsSet = (k, v) => { try { if (v == null) localStorage.removeItem(k); else localStorage.setItem(k, v); } catch (_) { /* */ } };
  /** the bot's idle avatar is generated from commands/texts/radios: bump after changes */
  F.avatarStamp = Date.now();
  const bumpAvatar = () => { F.avatarStamp = Date.now(); document.querySelectorAll('img.avatar-img').forEach((i) => { i.closest('.avatar-box').classList.remove('missing'); i.src = '/panel/api/avatar?t=' + F.avatarStamp; }); };
  F.bumpAvatar = bumpAvatar;

  function tabs(defs, onPick, initial) {
    const list = h('div', { class: 'tabs', role: 'tablist' });
    const btns = defs.map((d) => {
      const b = h('button', { type: 'button', role: 'tab', class: 'tab', 'aria-selected': 'false', id: 'tab-' + d.key }, icon(d.icon), h('span', { text: d.label }));
      b.addEventListener('click', () => pick(d.key));
      b.addEventListener('keydown', (e) => {
        const i = defs.indexOf(d);
        if (e.key === 'ArrowRight' || e.key === 'ArrowLeft') {
          e.preventDefault();
          const n = defs[(i + (e.key === 'ArrowRight' ? 1 : defs.length - 1)) % defs.length];
          pick(n.key); list.querySelector('#tab-' + n.key).focus();
        }
      });
      list.append(b);
      return b;
    });
    function pick(key) {
      btns.forEach((b, i) => { const on = defs[i].key === key; b.setAttribute('aria-selected', on ? 'true' : 'false'); b.tabIndex = on ? 0 : -1; b.classList.toggle('on', on); });
      onPick(key);
    }
    setTimeout(() => pick(initial || defs[0].key));
    return list;
  }

  // ================================================================== PLUGINS
  Pages.plugins = function (root) {
    let botName = F.state.selected;
    const sel = h('select', { class: 'input', 'aria-label': t('plugins.bot') });
    sel.addEventListener('change', () => { botName = sel.value; load(); });
    const list = h('div', { class: 'plugin-list' }, F.skeleton(3, 'sk-row'));
    const setAll = (open) => list.querySelectorAll('.plugin-card').forEach((c) => c.setOpen(open));
    root.append(F.pageHead(t('plugins.title'), t('plugins.sub'), h('label', { class: 'inline-field' }, h('span', { class: 'label-inline', text: t('plugins.bot') }), sel)),
      h('div', { class: 'toolbar' }, h('span', { class: 'grow' }),
        F.btn(t('plugins.expandAll'), { kind: 'ghost', icon: 'down', onClick: () => setAll(true) }),
        F.btn(t('plugins.collapseAll'), { kind: 'ghost', icon: 'up', onClick: () => setAll(false) })),
      list);
    let radios = [];
    let sig = '';

    function fillSel() {
      const s = F.state.bots.map((b) => b.name).join(',');
      if (s === sig) return;
      sig = s;
      sel.replaceChildren(...F.state.bots.map((b) => h('option', { value: b.name, text: F.botLabel(b) })));
      if (!F.bot(botName)) botName = F.state.bots[0] ? F.state.bots[0].name : '';
      sel.value = botName;
    }
    async function load() {
      if (!botName) { list.replaceChildren(F.empty('server', t('bots.none'))); return; }
      list.replaceChildren(...F.skeleton(4, 'sk-row'));
      try {
        const [pl, rd] = await Promise.all([F.api('GET', 'plugins/' + enc(botName)), F.api('GET', 'radios')]);
        radios = rd || [];
        list.replaceChildren(...grouped(pl || []));
        if (!pl || !pl.length) list.append(F.empty('box', t('plugins.none')));
      } catch (e) { F.fail(e); list.replaceChildren(F.empty('alert', t('err.loadFailed'))); }
    }
    // sections keep the (now 17) plugins scannable; unknown ids land in "other"
    const GROUPS = [
      ['welcome', ['welcome', 'advertisement', 'scheduled']],
      ['music', ['alonepause', 'idleradio', 'voteskip', 'djmode']],
      ['moderation', ['afkmover', 'namefilter', 'channelguard', 'antiflood']],
      ['server', ['registration', 'tempchannel', 'ranks', 'supportnotify', 'channelinfo']],
      ['fun', ['fun']],
    ];
    function grouped(pl) {
      const known = new Set(GROUPS.flatMap((g) => g[1]));
      const defs = GROUPS.map(([key, ids]) => [key, ids.map((id) => pl.find((p) => p.id === id)).filter(Boolean)])
        .concat([['other', pl.filter((p) => !known.has(p.id))]]);
      return defs.filter(([, ps]) => ps.length).map(([key, ps]) => {
        const count = h('span', { class: 'plugin-group-count' });
        const cards = ps.map(card);
        const paint = () => {
          const on = cards.filter((c) => c.classList.contains('on')).length;
          count.textContent = t('plugins.groupCount', { on, n: cards.length });
          count.classList.toggle('some', on > 0);
        };
        cards.forEach((c) => { c.onToggle = paint; });
        paint();
        const hid = F.uid('pg');
        return h('section', { class: 'plugin-group', 'aria-labelledby': hid },
          h('div', { class: 'plugin-group-head' }, h('h2', { id: hid, text: t('plugins.group.' + key) }), count), cards);
      });
    }
    function card(p) {
      const cfg = p.config || {};
      const getters = [];
      const fields = (p.schema || []).map((f) => {
        const val = cfg[f.key] !== undefined ? cfg[f.key] : f.default;
        let ctl, get;
        if (f.type === 'textarea') { ctl = F.textarea({ value: val == null ? '' : String(val), rows: 4 }); get = () => ctl.value; }
        else if (f.type === 'number') { ctl = F.input({ type: 'number', value: val == null ? '' : String(val), inputmode: 'numeric' }); get = () => (ctl.value === '' ? f.default : Number(ctl.value)); }
        else if (f.type === 'bool') { ctl = F.switchEl(val === true || val === 'true', F.loc(f.label)); get = () => ctl.input.checked; }
        else if (f.type === 'select') {
          const opts = (f.options || []).map((o) => [o, F.has('opt.' + o) ? t('opt.' + o) : o]);
          ctl = F.select(opts, val); get = () => ctl.value;
        } else if (f.type === 'radio') {
          const opts = [['', t('plugins.noRadio')]].concat(radios.map((r) => [r.name, r.name]));
          if (val && !radios.some((r) => r.name === val)) opts.push([val, val + ' (?)']);
          ctl = F.select(opts, val || ''); get = () => ctl.value;
        } else { ctl = F.input({ value: val == null ? '' : String(val) }); get = () => ctl.value; }
        getters.push([f.key, get]);
        const help = f.help ? F.loc(f.help) : null;
        return f.type === 'bool' ? h('div', { class: 'field' }, ctl, help ? h('div', { class: 'help', text: help }) : null) : F.field(F.loc(f.label), ctl, help, { cls: f.type === 'textarea' ? 'span2' : '' });
      });
      const collect = () => { const c = {}; getters.forEach(([k, g]) => { c[k] = g(); }); return c; };
      const enabled = F.switchEl(p.enabled, null, { 'aria-label': t('plugins.enabled') + ': ' + F.loc(p.name) });
      const el = h('article', { class: 'card plugin-card' + (p.enabled ? ' on' : '') });
      const bodyId = F.uid('pl');
      const save = async (btn, quiet) => F.busy(btn, async () => {
        try {
          await F.api('PUT', 'plugins/' + enc(botName) + '/' + enc(p.id), { enabled: enabled.input.checked, config: collect() });
          el.classList.toggle('on', enabled.input.checked);
          if (el.onToggle) el.onToggle();
          F.toast(quiet ? (enabled.input.checked ? t('plugins.on', { name: F.loc(p.name) }) : t('plugins.off', { name: F.loc(p.name) })) : t('common.saved'));
          F.refreshBots();
        } catch (e) { F.fail(e); if (quiet) enabled.input.checked = !enabled.input.checked; }
      });
      enabled.input.addEventListener('change', () => save(null, true));
      const saveBtn = F.btn(t('common.save'), { kind: 'primary', icon: 'check' });
      saveBtn.addEventListener('click', () => save(saveBtn, false));
      const toggle = h('button', { type: 'button', class: 'plugin-row', 'aria-expanded': 'false', 'aria-controls': bodyId },
        h('span', { class: 'plugin-name', text: F.loc(p.name) }),
        h('span', { class: 'plugin-desc', text: F.loc(p.description), title: F.loc(p.description) }),
        h('span', { class: 'plugin-chev' }, icon('chevron')));
      const body = h('div', { class: 'plugin-body', id: bodyId, hidden: true },
        h('p', { class: 'muted small', text: F.loc(p.description) }),
        fields.length ? h('div', { class: 'form-grid' }, fields) : null,
        h('div', { class: 'card-foot' }, saveBtn));
      const lsKey = 'frix.plugin.' + p.id + '.open';
      el.setOpen = (open) => {
        body.hidden = !open;
        toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
        el.classList.toggle('open', open);
        lsSet(lsKey, open ? '1' : null);
      };
      toggle.addEventListener('click', () => el.setOpen(body.hidden));
      el.append(h('div', { class: 'plugin-head' }, toggle, enabled), body);
      el.setOpen(lsGet(lsKey) === '1');
      return el;
    }
    let first = true;
    function onBots() { fillSel(); if (first) { first = false; load(); } }
    return { onBots };
  };

  // ================================================================== COMMANDS + TEXTS
  Pages.commands = function (root) {
    const pane = h('div', { role: 'tabpanel' });
    const img = h('img', { class: 'avatar-img', alt: t('commands.avatarAlt'), src: '/panel/api/avatar?t=' + F.avatarStamp });
    const box = h('figure', { class: 'avatar-box' }, img, h('figcaption', null, h('span', { class: 'label-inline', text: t('commands.avatar') }),
      h('span', { class: 'muted small avatar-missing', text: t('commands.avatarNone') }),
      F.iconBtn('refresh', t('common.refresh'), bumpAvatar, 'sm')));
    // küçük önizleme: tıklayınca gerçek boyutta (300 px genişlik) açılır
    img.tabIndex = 0;
    img.setAttribute('role', 'button');
    img.title = t('commands.avatar');
    const openBig = () => F.modal({ title: t('commands.avatar'), body: h('div', { class: 'avatar-full' }, h('img', { src: '/panel/api/avatar?t=' + F.avatarStamp, alt: t('commands.avatarAlt') })), actions: [{ label: t('common.close') }] });
    img.addEventListener('click', openBig);
    img.addEventListener('keydown', (e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); openBig(); } });
    img.addEventListener('error', () => box.classList.add('missing'));
    img.addEventListener('load', () => box.classList.remove('missing'));
    root.append(h('div', { class: 'commands-head' }, F.pageHead(t('commands.title'), t('commands.sub')), box),
      tabs([{ key: 'cmd', label: t('commands.tabCommands'), icon: 'terminal' }, { key: 'texts', label: t('commands.tabTexts'), icon: 'text' }],
        (k) => { pane.setAttribute('aria-labelledby', 'tab-' + k); pane.replaceChildren(); (k === 'cmd' ? commandsTab : textsTab)(pane); }),
      pane);
  };

  /** set every built-in command name to one language's defaults (POST commands/builtin/defaults {lang}); Commands page + Settings */
  function cmdDefaultsBar(onDone) {
    let dLang = 'en';
    const host = h('div', { class: 'lp-host' });
    // English is the default command set: list it first and tag it
    const mk = (langs) => host.replaceChildren(F.langPicker({ langs: ['en'].concat(langs.filter((l) => l !== 'en')), value: dLang, label: t('commands.defaultsLang'), align: 'left',
      tags: { en: t('commands.defaultTag') }, onChange: (c) => { dLang = c; } }));
    mk(Object.keys(window.I18N));
    F.api('GET', 'settings').then((st) => {
      if (st && st.commandLang) dLang = st.commandLang; // komutların şu anki dili
      if (st && Array.isArray(st.languages) && st.languages.length) { if (!st.languages.includes(dLang)) dLang = 'en'; mk(st.languages); }
    }).catch(() => { /* keep panel languages */ });
    const apply = F.btn(t('commands.defaultsApply'), { kind: 'ghost', icon: 'refresh' });
    apply.addEventListener('click', async () => {
      if (!(await F.confirm(t('commands.defaultsConfirm'), { ok: t('common.yes') }))) return;
      await F.busy(apply, async () => {
        try {
          const r = (await F.api('POST', 'commands/builtin/defaults', { lang: dLang })) || {};
          const skipped = Array.isArray(r.skipped) ? r.skipped : [];
          F.toast(t('commands.defaultsDone'));
          if (skipped.length) F.toast(t('commands.defaultsSkipped', { list: skipped.map((x) => '!' + String(x).replace(/^!/, '')).join(', ') }), 'info');
          if (onDone) await onDone();
        } catch (e) { F.fail(e); }
      });
    });
    return h('div', { class: 'toolbar defaults-bar' }, F.field(t('commands.defaultsLang'), host), apply);
  }

  function commandsTab(pane) {
    const builtin = h('div', { class: 'builtin-list' });
    const custom = h('div', { class: 'cmd-list' }, F.skeleton(3, 'sk-row'));
    pane.append(
      h('section', { class: 'card' }, h('div', { class: 'card-head' }, h('h2', { class: 'card-title' }, icon('terminal'), t('commands.custom')),
        F.btn(t('commands.new'), { kind: 'primary', icon: 'plus', onClick: () => form(null) })),
      h('p', { class: 'muted small' }, F.rich(t('commands.customHelp'))), custom),
      h('section', { class: 'card' }, h('h2', { class: 'card-title' }, icon('list'), t('commands.builtin')), h('p', { class: 'muted small' }, F.rich(t('commands.builtinHelp'))),
        cmdDefaultsBar(async () => { await load(); bumpAvatar(); }), builtin));
    let data = null;
    async function load() {
      try { data = await F.api('GET', 'commands'); render(); }
      catch (e) { F.fail(e); custom.replaceChildren(F.empty('alert', t('err.loadFailed'))); }
    }
    function render() {
      builtin.replaceChildren(...(data.builtin || []).map(builtinRow));
      const items = data.custom || [];
      if (!items.length) { custom.replaceChildren(F.empty('terminal', t('commands.none'), t('commands.noneHint'))); return; }
      custom.replaceChildren(...items.map((c) => {
        const sw = F.switchEl(c.enabled, null, { 'aria-label': t('commands.enabled') + ': !' + c.trigger });
        sw.input.addEventListener('change', async () => {
          try { await F.api('PUT', 'commands/' + c.id, Object.assign({}, c, { enabled: sw.input.checked })); c.enabled = sw.input.checked; row.classList.toggle('disabled', !c.enabled); F.toast(t('common.saved')); bumpAvatar(); }
          catch (e) { sw.input.checked = !sw.input.checked; F.fail(e); }
        });
        const row = h('div', { class: 'cmd-row' + (c.enabled ? '' : ' disabled') },
          h('div', { class: 'cmd-main' },
            h('div', { class: 'row gap-s wrap' }, h('code', { class: 'cmd-trigger', text: '!' + c.trigger }), F.badge(t('commands.kind.' + c.kind), c.kind === 'alias' ? 'info' : 'accent')),
            h('div', { class: 'cmd-value', text: c.value }),
            c.description ? h('div', { class: 'muted small', text: c.description }) : null),
          h('div', { class: 'cmd-actions' }, sw, F.iconBtn('edit', t('common.edit'), () => form(c)), F.iconBtn('trash', t('common.delete'), () => del(c), 'danger')));
        return row;
      }));
    }
    function builtinRow(b) {
      const supported = 'alias' in b;
      const alias = F.input({ value: b.alias || '', placeholder: b.trigger, maxlength: 32, 'aria-label': t('commands.renameLabel', { trigger: b.trigger }), disabled: !supported });
      const keep = h('input', { type: 'checkbox', checked: b.keepOld !== false, disabled: !supported });
      const keepLbl = h('label', { class: 'check' }, keep, h('span', { text: t('commands.keepOld') }));
      const result = h('code', { class: 'builtin-result' });
      const paint = () => {
        const a = (b.alias || '').trim();
        result.textContent = a ? '!' + b.trigger + ' → !' + a + (b.keepOld ? '  (+ !' + b.trigger + ')' : '') : '!' + b.trigger;
        result.classList.toggle('renamed', !!a);
      };
      paint();
      const save = F.btn(t('common.save'), { kind: 'ghost', icon: 'check', disabled: !supported });
      save.addEventListener('click', () => F.busy(save, async () => {
        const body = { alias: alias.value.trim().replace(/^!/, ''), keepOld: keep.checked };
        try {
          await F.api('PUT', 'commands/builtin/' + enc(b.trigger), body);
          b.alias = body.alias || null; b.keepOld = body.keepOld; alias.value = body.alias; paint();
          F.toast(body.alias ? t('commands.renamed', { from: b.trigger, to: body.alias }) : t('commands.renameReset', { trigger: b.trigger }));
          bumpAvatar();
        } catch (e) { F.fail(e); }
      }));
      alias.addEventListener('keydown', (e) => { if (e.key === 'Enter') { e.preventDefault(); save.click(); } });
      return h('div', { class: 'builtin-row' },
        h('code', { class: 'builtin-usage', text: b.usage }),
        h('div', { class: 'input-prefix builtin-alias' }, h('span', { text: '!' }), alias),
        keepLbl, save, result);
    }
    async function del(c) {
      if (!(await F.confirm(t('commands.deleteConfirm', { trigger: c.trigger }), { danger: true, ok: t('common.delete') }))) return;
      try { await F.api('DELETE', 'commands/' + c.id); F.toast(t('common.deleted')); load(); bumpAvatar(); } catch (e) { F.fail(e); }
    }
    function form(c) {
      const trig = F.input({ value: c ? c.trigger : '', maxlength: 32, placeholder: 'discord' });
      const kind = F.select([['text', t('commands.kind.text')], ['alias', t('commands.kind.alias')]], c ? c.kind : 'text');
      const kindHelp = h('div', { class: 'callout small' });
      const value = F.textarea({ value: c ? c.value : '', rows: 3, maxlength: 1000 });
      const desc = F.input({ value: c && c.description ? c.description : '', maxlength: 200 });
      const en = F.switchEl(c ? c.enabled : true, t('commands.enabled'));
      const sync = () => {
        kindHelp.replaceChildren(icon('info'), h('div', null, F.rich(t('commands.kindHelp.' + kind.value))));
        value.placeholder = kind.value === 'alias' ? '!play' : 'Discord: https://discord.gg/… {user}';
      };
      kind.addEventListener('change', sync); sync();
      F.modal({
        title: c ? t('commands.edit') : t('commands.new'),
        body: h('div', null,
          F.field(t('commands.f.trigger'), h('div', { class: 'input-prefix' }, h('span', { text: '!' }), trig), t('commands.f.triggerHelp')),
          F.field(t('commands.f.kind'), kind), kindHelp,
          F.field(t('commands.f.value'), value),
          F.field(t('commands.f.desc'), desc, t('commands.f.descHelp')), en),
        actions: [{ label: t('common.cancel') }, {
          label: t('common.save'), kind: 'primary', primary: true, icon: 'check', onClick: async () => {
            const body = { trigger: trig.value.trim().replace(/^!/, ''), kind: kind.value, value: value.value.trim(), enabled: en.input.checked, description: desc.value.trim() || null };
            if (!body.trigger) { F.toast(t('commands.f.triggerReq'), 'err'); trig.focus(); return false; }
            if (!body.value) { F.toast(t('commands.f.valueReq'), 'err'); value.focus(); return false; }
            if (body.kind === 'alias' && !body.value.startsWith('!')) { F.toast(t('commands.f.aliasReq'), 'err'); value.focus(); return false; }
            try {
              if (c) await F.api('PUT', 'commands/' + c.id, body); else await F.api('POST', 'commands', body);
              F.toast(t('common.saved')); load(); bumpAvatar();
            } catch (e) { F.fail(e); return false; }
          },
        }],
      });
    }
    load();
  }

  function textsTab(pane) {
    let lang = F.state.lang;
    // flag dropdown; starts with tr/en (+ current) and is rebuilt with the server's language list
    const langHost = h('div', { class: 'lp-host' });
    const mkPicker = (langs) => langHost.replaceChildren(F.langPicker({ langs, value: lang, label: t('texts.lang'), align: 'left', onChange: (c) => { lang = c; load(); } }));
    mkPicker(['tr', 'en'].includes(lang) ? ['tr', 'en'] : ['tr', 'en', lang]);
    F.api('GET', 'settings').then((st) => {
      if (!st || !Array.isArray(st.languages) || !st.languages.length) return;
      const next = st.languages.includes(lang) ? lang : st.lang;
      const changed = next !== lang;
      lang = next;
      mkPicker(st.languages);
      if (changed) load();
    }).catch(() => { /* keep tr/en */ });
    const filter = F.input({ type: 'search', placeholder: t('texts.filter'), 'aria-label': t('texts.filter') });
    const list = h('div', { class: 'text-list' }, F.skeleton(4, 'sk-row'));
    filter.addEventListener('input', applyFilter);
    pane.append(h('section', { class: 'card' },
      h('p', { class: 'muted small' }, F.rich(t('texts.help'))),
      h('div', { class: 'toolbar' }, F.field(t('texts.lang'), langHost), F.field(t('texts.filter'), filter, null, { cls: 'grow' })),
      list));
    function applyFilter() {
      const q = filter.value.trim().toLowerCase();
      list.querySelectorAll('.text-row').forEach((r) => { r.hidden = !!q && !r.dataset.search.includes(q); });
    }
    async function load() {
      list.replaceChildren(...F.skeleton(4, 'sk-row'));
      try {
        const items = (await F.api('GET', 'texts?lang=' + enc(lang))) || [];
        list.replaceChildren(...items.map(row));
        applyFilter();
      } catch (e) { F.fail(e); list.replaceChildren(F.empty('alert', t('err.loadFailed'))); }
    }
    function row(it) {
      const ta = F.textarea({ value: it.value, rows: 2, 'aria-label': it.key });
      const badge = F.badge(t('texts.custom'), 'accent');
      badge.hidden = !it.custom;
      const reset = F.btn(t('texts.reset'), { kind: 'ghost', icon: 'refresh' });
      reset.disabled = !it.custom;
      const save = F.btn(t('common.save'), { kind: 'ghost', icon: 'check' });
      const put = (value, btn) => F.busy(btn, async () => {
        try {
          await F.api('PUT', 'texts', { lang, key: it.key, value });
          const custom = !!value && value !== it.defaultValue;
          it.custom = custom; it.value = value || it.defaultValue;
          ta.value = it.value; badge.hidden = !custom; reset.disabled = !custom;
          F.toast(custom ? t('common.saved') : t('texts.wasReset'));
          if (it.key.startsWith('avatar.')) bumpAvatar();
        } catch (e) { F.fail(e); }
      });
      save.addEventListener('click', () => put(ta.value, save));
      reset.addEventListener('click', () => put('', reset));
      return h('div', { class: 'text-row', dataset: { search: (it.key + ' ' + it.value + ' ' + it.defaultValue).toLowerCase() } },
        h('div', { class: 'row gap-s wrap' }, h('code', { class: 'text-key', text: it.key }), badge),
        ta,
        h('div', { class: 'text-foot' }, h('div', { class: 'muted small grow' }, h('span', { text: t('texts.default') + ': ' }), h('span', { class: 'default-val', text: it.defaultValue })), reset, save));
    }
    load();
  }

  // ================================================================== USERS
  const roleHelp = (r) => t('role.' + r + '.help');
  Pages.users = function (root) {
    const list = h('div', { class: 'table-wrap' }, F.skeleton(3, 'sk-row'));
    root.append(F.pageHead(t('users.title'), t('users.sub'), [
      F.btn(t('nav.account'), { kind: 'ghost', icon: 'key', onClick: () => F.go('/account') }),
      F.btn(t('users.new'), { kind: 'primary', icon: 'plus', onClick: create })]),
    h('section', { class: 'card' }, list),
    h('section', { class: 'card' }, h('h2', { class: 'card-title' }, icon('info'), t('users.roles')),
      h('dl', { class: 'roles' }, ['admin', 'dj', 'viewer'].map((r) => [h('dt', null, F.badge(t('role.' + r), r)), h('dd', { class: 'muted', text: roleHelp(r) })]))));
    async function load() {
      try {
        const users = (await F.api('GET', 'users')) || [];
        const me = F.state.me.name;
        list.replaceChildren(h('table', { class: 'table' },
          h('thead', null, h('tr', null, [t('users.name'), t('users.role'), t('users.created'), t('users.lastLogin'), ''].map((x) => h('th', { scope: 'col', text: x })))),
          h('tbody', null, users.map((u) => {
            const self = u.name === me;
            const role = F.select(['admin', 'dj', 'viewer'].map((r) => [r, t('role.' + r)]), u.role, { 'aria-label': t('users.role') + ': ' + u.name, disabled: self, class: 'input input-sm' });
            role.addEventListener('change', async () => {
              try { await F.api('PUT', 'users/' + u.id, { role: role.value }); u.role = role.value; F.toast(t('users.roleChanged', { name: u.name, role: t('role.' + role.value) })); }
              catch (e) { role.value = u.role; F.fail(e); }
            });
            return h('tr', null,
              h('td', { 'data-label': t('users.name') }, h('strong', { text: u.name }), self ? F.badge(t('users.you'), 'accent') : null),
              h('td', { 'data-label': t('users.role') }, role),
              h('td', { 'data-label': t('users.created'), class: 'muted', text: F.fmtDate(u.created) }),
              h('td', { 'data-label': t('users.lastLogin'), class: 'muted', text: u.lastLogin ? F.fmtDate(u.lastLogin) : t('users.never') }),
              h('td', { class: 'td-actions' },
                F.iconBtn('key', t('users.resetPw'), () => resetPw(u)),
                self ? null : F.iconBtn('trash', t('common.delete'), () => del(u), 'danger')));
          }))));
      } catch (e) { F.fail(e); list.replaceChildren(F.empty('alert', t('err.loadFailed'))); }
    }
    function roleCards(value) {
      const name = F.uid('role');
      const wrap = h('div', { class: 'role-cards', role: 'radiogroup', 'aria-label': t('users.role') });
      ['dj', 'viewer', 'admin'].forEach((r) => {
        wrap.append(h('label', { class: 'role-card' },
          h('input', { type: 'radio', name, value: r, checked: r === value }),
          h('span', { class: 'role-card-body' }, F.badge(t('role.' + r), r), h('span', { class: 'muted small', text: roleHelp(r) }))));
      });
      wrap.get = () => (wrap.querySelector('input:checked') || {}).value || 'dj';
      return wrap;
    }
    function create() {
      const name = F.input({ maxlength: 32, autocomplete: 'off' });
      const pw = F.pwInput({ autocomplete: 'new-password', maxlength: 200 });
      const roles = roleCards('dj');
      F.modal({
        title: t('users.new'),
        body: h('div', null, F.field(t('users.name'), name, t('setup.userHelp')), F.field(t('users.password'), pw, t('setup.passHelp')), h('div', { class: 'label', text: t('users.role') }), roles),
        actions: [{ label: t('common.cancel') }, {
          label: t('common.create'), kind: 'primary', primary: true, icon: 'check', onClick: async () => {
            const n = name.value.trim();
            if (!/^[\p{L}\p{N}_.-]{3,32}$/u.test(n)) { F.toast(t('err.bad_username'), 'err'); name.focus(); return false; }
            if (pw.input.value.length < 8) { F.toast(t('err.weak_password'), 'err'); pw.input.focus(); return false; }
            try { await F.api('POST', 'users', { name: n, pass: pw.input.value, role: roles.get() }); F.toast(t('users.created_', { name: n })); load(); }
            catch (e) { F.fail(e); return false; }
          },
        }],
      });
    }
    function resetPw(u) {
      const pw = F.pwInput({ autocomplete: 'new-password', maxlength: 200 });
      F.modal({
        title: t('users.resetPwFor', { name: u.name }), size: 'sm',
        body: h('div', null, F.field(t('users.newPassword'), pw, t('users.resetHelp'))),
        actions: [{ label: t('common.cancel') }, {
          label: t('common.save'), kind: 'primary', primary: true, icon: 'check', onClick: async () => {
            if (pw.input.value.length < 8) { F.toast(t('err.weak_password'), 'err'); return false; }
            try { await F.api('PUT', 'users/' + u.id, { pass: pw.input.value }); F.toast(t('users.pwReset', { name: u.name })); }
            catch (e) { F.fail(e); return false; }
          },
        }],
      });
    }
    async function del(u) {
      if (!(await F.confirm(t('users.deleteConfirm', { name: u.name }), { danger: true, ok: t('common.delete') }))) return;
      try { await F.api('DELETE', 'users/' + u.id); F.toast(t('common.deleted')); load(); } catch (e) { F.fail(e); }
    }
    load();
  };

  // ================================================================== ACCOUNT
  Pages.account = function (root) {
    const me = F.state.me;
    const old = F.pwInput({ autocomplete: 'current-password' });
    const nw = F.pwInput({ autocomplete: 'new-password', maxlength: 200 });
    const nw2 = F.pwInput({ autocomplete: 'new-password', maxlength: 200 });
    const err = h('div', { class: 'form-error', role: 'alert' });
    const submit = F.btn(t('account.change'), { kind: 'primary', icon: 'check', type: 'submit' });
    const form = h('form', { class: 'card narrow', novalidate: true },
      h('h2', { class: 'card-title' }, icon('key'), t('account.changePw')),
      F.field(t('account.old'), old), F.field(t('account.new'), nw, t('setup.passHelp')), F.field(t('account.new2'), nw2), err,
      h('p', { class: 'muted small', text: t('account.note') }),
      h('div', { class: 'card-foot' }, submit));
    form.addEventListener('submit', (e) => {
      e.preventDefault();
      err.textContent = '';
      if (nw.input.value.length < 8) { err.textContent = t('err.weak_password'); return; }
      if (nw.input.value !== nw2.input.value) { err.textContent = t('setup.mismatch'); return; }
      F.busy(submit, async () => {
        try {
          await F.api('POST', 'me/password', { old: old.input.value, new: nw.input.value });
          F.toast(t('account.changed'));
          form.reset(); old.input.value = nw.input.value = nw2.input.value = '';
        } catch (ex) { err.textContent = F.errMsg(ex); }
      });
    });
    root.append(F.pageHead(t('nav.account'), null),
      h('section', { class: 'card narrow account-card' },
        h('div', { class: 'avatar big', text: (me.name[0] || '?').toLowerCase() }),
        h('div', null, h('h2', { class: 'card-title', text: me.name }), F.badge(t('role.' + me.role), me.role), h('p', { class: 'muted small', text: roleHelp(me.role) }))),
      form);
  };

  // ================================================================== SETTINGS
  // ================================================================== SETTINGS (sections: /settings/<key>)
  const SETTINGS_SECTIONS = [
    { key: 'general', icon: 'sliders' },
    { key: 'commands', icon: 'terminal' },
    { key: 'youtube', icon: 'music' },
    { key: 'updates', icon: 'download' },
    { key: 'maintenance', icon: 'power' },
    { key: 'audit', icon: 'list' },
  ];
  Pages.settings = function (root) {
    const secKey = () => {
      const k = location.pathname.replace(/\/+$/, '').split('/')[2] || 'general';
      return SETTINGS_SECTIONS.some((x) => x.key === k) ? k : 'general';
    };
    const links = SETTINGS_SECTIONS.map((sec) => h('a', { href: '/settings/' + sec.key, 'data-link': '', class: 'subnav-item', 'data-key': sec.key },
      icon(sec.icon), h('span', { text: t('settings.sec.' + sec.key) })));
    const subnav = h('nav', { class: 'subnav', 'aria-label': t('settings.sections') }, links);
    const body = h('div', { class: 'settings-body' }, F.skeleton(2, 'sk-card'));
    root.append(F.pageHead(t('settings.title'), t('settings.sub')), h('div', { class: 'settings-layout' }, subnav, body));
    let s = null;
    async function load() {
      try { s = await F.api('GET', 'settings'); render(); }
      catch (e) { F.fail(e); body.replaceChildren(F.empty('alert', t('err.loadFailed'))); }
    }
    function render() {
      const k = secKey();
      links.forEach((a) => { if (a.dataset.key === k) a.setAttribute('aria-current', 'page'); else a.removeAttribute('aria-current'); });
      const cur = links.find((a) => a.dataset.key === k);
      if (cur && cur.scrollIntoView && matchMedia('(max-width: 720px)').matches) cur.scrollIntoView({ block: 'nearest', inline: 'center' });
      if (!s) return;
      const head = h('div', { class: 'section-head' }, h('h2', { text: t('settings.sec.' + k) }), h('p', { class: 'muted small', text: t('settings.secDesc.' + k) }));
      const cards = {
        general: () => [panelCard(), langCard(), sysCard()],
        commands: () => [cmdCard()],
        youtube: () => [ytdlpCard(), cookieCard()],
        updates: () => [updateCard()],
        maintenance: () => [restartCard()],
        audit: () => [auditLinkCard()],
      }[k]();
      body.replaceChildren(head, ...cards);
      document.title = t('settings.sec.' + k) + ' · ' + t('nav.settings') + ' · frix';
    }
    const card = (ic, title, desc, ...kids) => h('section', { class: 'card set-card' }, h('h3', { class: 'card-title' }, icon(ic), title), desc ? h('p', { class: 'muted small card-desc' }, desc) : null, kids);

    function panelCard() {
      const lang = F.langPicker({ langs: Object.keys(window.I18N), value: F.state.lang, label: t('settings.panelLang'), align: 'left', onChange: (c) => F.setLang(c) });
      const theme = F.select([['lacivert', t('theme.lacivert')], ['krem', t('theme.krem')]], F.theme(), { class: 'input theme-sel' });
      theme.addEventListener('change', () => F.setTheme(theme.value));
      return card('sliders', t('settings.panelCard'), t('settings.panelHelp'),
        h('div', { class: 'set-grid' }, F.field(t('settings.panelLang'), lang), F.field(t('theme.title'), theme)));
    }
    function langCard() {
      const sel = F.langPicker({ langs: s.languages && s.languages.length ? s.languages : ['tr', 'en'], value: s.lang, label: t('settings.lang'), align: 'left' });
      const save = F.btn(t('common.save'), { kind: 'primary', icon: 'check' });
      save.addEventListener('click', () => F.busy(save, async () => {
        const v = sel.value();
        try { await F.api('PUT', 'settings', { lang: v }); s.lang = v; F.toast(t('settings.langSaved')); } catch (e) { F.fail(e); }
      }));
      return card('globe', t('settings.lang'), t('settings.langHelp'), h('div', { class: 'set-row' }, h('div', { class: 'grow' }, sel), save));
    }
    function sysCard() {
      return card('info', t('settings.system'), null,
        h('dl', { class: 'kv' },
          h('dt', { text: t('settings.versionLabel') }), h('dd', { text: s.version }),
          h('dt', { text: t('settings.os') }), h('dd', { text: s.os }),
          h('dt', { text: 'yt-dlp' }), h('dd', { text: s.ytdlp ? t('settings.found') : t('settings.notFound') })));
    }
    function cmdCard() {
      return card('terminal', t('commands.defaultsLang'), null,
        cmdDefaultsBar(() => bumpAvatar()),
        h('a', { href: '/commands', 'data-link': '', class: 'small link-arrow' }, t('settings.cmdLink'), ' →'));
    }
    function ytdlpCard() {
      const out = h('pre', { class: 'pre' });
      const outBox = h('details', { class: 'notes', hidden: true }, h('summary', { text: t('settings.output') }), out);
      const btn = F.btn(t('settings.ytdlpUpdate'), { kind: 'primary', icon: 'download', disabled: !s.ytdlp });
      btn.addEventListener('click', () => F.busy(btn, async () => {
        outBox.hidden = false; outBox.open = true; out.textContent = t('settings.ytdlpRunning');
        try {
          const r = await F.api('POST', 'ytdlp/update');
          out.textContent = (r && r.output) || '(no output)';
          F.toast(r && r.ok ? t('settings.ytdlpDone') : t('settings.ytdlpFail'), r && r.ok ? 'ok' : 'err');
        } catch (e) { out.textContent = F.errMsg(e); F.fail(e); }
      }));
      return card('terminal', 'yt-dlp', t('settings.ytdlpHelp'),
        h('div', { class: 'set-row' }, s.ytdlp ? F.badge(t('settings.found'), 'ok') : F.badge(t('settings.notFound'), 'err'), h('span', { class: 'grow' }), btn), outBox);
    }
    function cookieCard() {
      const status = h('div', { class: 'small' });
      const paint = () => status.replaceChildren(s.cookies && s.cookies.exists
        ? h('span', null, F.badge(t('settings.cookiesOn'), 'ok'), ' ', h('span', { class: 'muted', text: t('settings.cookiesUpdated', { d: F.fmtDate(s.cookies.updated) }) }))
        : F.badge(t('settings.cookiesOff'), 'muted'));
      paint();
      const file = h('input', { type: 'file', accept: '.txt,text/plain', hidden: true });
      const up = F.btn(t('settings.cookiesUpload'), { kind: 'primary', icon: 'upload', onClick: () => file.click() });
      const rm = F.btn(t('settings.cookiesRemove'), { kind: 'ghost', icon: 'trash' });
      rm.disabled = !(s.cookies && s.cookies.exists);
      file.addEventListener('change', async () => {
        const f = file.files && file.files[0];
        file.value = '';
        if (!f) return;
        await F.busy(up, async () => {
          try { await F.api('POST', 'cookies', await f.text(), { text: true }); F.toast(t('settings.cookiesSaved')); await refresh(); } catch (e) { F.fail(e); }
        });
      });
      rm.addEventListener('click', async () => {
        if (!(await F.confirm(t('settings.cookiesRemoveConfirm'), { danger: true, ok: t('settings.cookiesRemove') }))) return;
        await F.busy(rm, async () => {
          try { await F.api('DELETE', 'cookies'); F.toast(t('settings.cookiesRemoved')); await refresh(); } catch (e) { F.fail(e); }
        });
      });
      async function refresh() {
        try { const n = await F.api('GET', 'settings'); s.cookies = n.cookies; paint(); rm.disabled = !(s.cookies && s.cookies.exists); } catch (_) { /* */ }
      }
      return card('key', t('settings.cookies'), F.rich(t('settings.cookiesHelp')),
        h('div', { class: 'set-row wrap' }, status, h('span', { class: 'grow' }), up, rm, file),
        h('a', { href: '/faq#faq-cookies', 'data-link': '', class: 'small link-arrow' }, t('settings.cookiesFaq'), ' →'));
    }
    function updateCard() {
      const repo = F.input({ value: s.updateRepo || '', placeholder: t('settings.repoPh') });
      const saveRepo = F.btn(t('common.save'), { kind: 'ghost', icon: 'check' });
      saveRepo.addEventListener('click', () => F.busy(saveRepo, async () => {
        try { await F.api('PUT', 'settings', { updateRepo: repo.value.trim() }); s.updateRepo = repo.value.trim(); F.toast(t('common.saved')); } catch (e) { F.fail(e); }
      }));
      const out = h('div', { class: 'update-out' });
      const check = F.btn(t('settings.check'), { kind: 'primary', icon: 'refresh' });
      check.addEventListener('click', () => F.busy(check, async () => {
        out.replaceChildren(...F.skeleton(1, 'sk-row'));
        try {
          const r = await F.api('GET', 'update/check');
          const apply = r.available ? F.btn(t('settings.updateNow'), { kind: 'primary', icon: 'download', onClick: async (e) => {
            if (!(await F.confirm(t('settings.updateConfirm', { v: r.latest }), { ok: t('settings.updateNow') }))) return;
            await F.busy(e.currentTarget, async () => {
              try { await F.api('POST', 'update/apply'); F.waitForRestart(t('settings.updating')); } catch (er) { F.fail(er); }
            });
          } }) : null;
          out.replaceChildren(
            h('div', { class: 'callout ' + (r.error ? 'callout-err' : r.available ? 'callout-ok' : '') }, icon(r.error ? 'alert' : r.available ? 'download' : 'check'),
              h('div', null, h('strong', { text: r.error ? F.errMsg({ code: r.error }) : r.available ? t('settings.available') : t('settings.upToDate') }))),
            h('dl', { class: 'kv' }, h('dt', { text: t('settings.current') }), h('dd', { text: r.current || s.version }),
              h('dt', { text: t('settings.latest') }), h('dd', { text: r.latest || '–' })),
            r.notes ? h('details', { class: 'notes' }, h('summary', { text: t('settings.notes') }), h('pre', { class: 'pre', text: r.notes })) : null,
            r.url ? h('a', { href: r.url, target: '_blank', rel: 'noopener noreferrer', class: 'small' }, t('settings.releasePage'), ' ', icon('link', 'sm')) : null,
            apply ? h('div', { class: 'card-foot' }, apply) : null);
        } catch (e) { out.replaceChildren(); F.fail(e); }
      }));
      return [
        card('download', t('settings.updates'), t('settings.version', { v: s.version }), h('div', { class: 'set-row' }, check), out),
        card('link', t('settings.repo'), t('settings.repoHelp'), h('div', { class: 'set-row' }, h('div', { class: 'grow' }, repo), saveRepo)),
      ];
    }
    function restartCard() {
      const btn = F.btn(t('settings.restart'), { kind: 'danger', icon: 'power', onClick: async () => {
        if (!(await F.confirm(t('settings.restartConfirm'), { danger: true, ok: t('settings.restart') }))) return;
        try { await F.api('POST', 'restart'); F.waitForRestart(); } catch (e) { F.fail(e); }
      } });
      const c = card('power', t('settings.restartTitle'), t('settings.restartHelp'), h('div', { class: 'set-row' }, btn));
      c.classList.add('danger-zone');
      return c;
    }
    function auditLinkCard() {
      return card('list', t('settings.audit'), t('settings.auditHere'),
        h('div', { class: 'set-row' }, h('a', { href: '/logs?tab=audit', 'data-link': '', class: 'btn btn-ghost' }, icon('list'), h('span', { text: t('settings.auditLink') }))));
    }
    load();
    return { onSubRoute: render };
  };

  // ================================================================== LOGS
  Pages.logs = function (root) {
    const pane = h('div', { role: 'tabpanel' });
    let cur = null;
    const kill = () => { if (cur && cur.destroy) { try { cur.destroy(); } catch (_) { /* */ } } cur = null; };
    root.append(F.pageHead(t('logs.title'), t('logs.sub')),
      tabs([{ key: 'live', label: t('logs.tabLive'), icon: 'wave' }, { key: 'history', label: t('logs.tabHistory'), icon: 'file' }, { key: 'audit', label: t('settings.sec.audit'), icon: 'list' }],
        (k) => { kill(); pane.setAttribute('aria-labelledby', 'tab-' + k); pane.replaceChildren(); cur = ({ live: liveLogs, history: historyLogs, audit: auditLog })[k](pane) || {}; },
        ['live', 'history', 'audit'].includes(new URLSearchParams(location.search).get('tab')) ? new URLSearchParams(location.search).get('tab') : 'live'),
      pane);
    return { destroy: kill };
  };

  /** first / prev / [page] of N / next / last; onGo(page) */
  function pager(onGo) {
    let page = 1, pages = 1;
    const num = h('input', { type: 'number', class: 'input pager-num', min: 1, value: 1, inputmode: 'numeric', 'aria-label': t('pager.page') });
    const of = h('span', { class: 'pager-of mono small' });
    const go = (p) => { p = Math.min(Math.max(1, Math.floor(Number(p) || 1)), pages); if (p !== page) onGo(p); else num.value = page; };
    const first = F.iconBtn('prev', t('pager.first'), () => go(1), 'sm');
    const prev = F.iconBtn('chevron', t('pager.prev'), () => go(page - 1), 'sm pager-prev');
    const next = F.iconBtn('chevron', t('pager.next'), () => go(page + 1), 'sm pager-next');
    const last = F.iconBtn('next', t('pager.last'), () => go(pages), 'sm');
    num.addEventListener('change', () => go(num.value));
    num.addEventListener('keydown', (e) => { if (e.key === 'Enter') { e.preventDefault(); go(num.value); } });
    const el = h('nav', { class: 'pager', 'aria-label': t('pager.label') }, first, prev, h('span', { class: 'pager-mid' }, num, of), next, last);
    el.set = (p, n) => {
      page = Math.max(1, p || 1); pages = Math.max(1, n || 1);
      num.value = page; num.max = pages; of.textContent = '/ ' + F.fmtNum(pages);
      first.disabled = prev.disabled = page <= 1; next.disabled = last.disabled = page >= pages;
      el.hidden = pages <= 1;
    };
    el.set(1, 1);
    return el;
  }
  const sizeSelect = (sizes, val, onChange) => {
    const s = F.select(sizes.map((n) => [n, String(n)]), val, { 'aria-label': t('pager.size'), class: 'input pager-size' });
    s.addEventListener('change', () => onChange(Number(s.value)));
    return s;
  };
  const debounce = (fn, ms) => { let tm = 0; return (...a) => { clearTimeout(tm); tm = setTimeout(() => fn(...a), ms); }; };

  function liveLogs(pane) {
    const onlyWarn = F.switchEl(false, t('logs.onlyWarn'));
    const auto = F.switchEl(true, t('logs.autoScroll'));
    const box = h('div', { class: 'logbox', role: 'log', 'aria-live': 'off', tabindex: '0', 'aria-label': t('logs.title') });
    const count = h('span', { class: 'muted small' });
    const clear = F.btn(t('logs.clear'), { kind: 'ghost', icon: 'trash' });
    pane.append(h('section', { class: 'card log-card' }, h('div', { class: 'toolbar' }, onlyWarn, auto, h('span', { class: 'grow' }), count, clear), box));
    let since = 0, timer = null, alive = true, n = 0;
    const MAX = 1500;
    const filt = () => box.classList.toggle('only-warn', onlyWarn.input.checked);
    onlyWarn.input.addEventListener('change', () => { filt(); if (auto.input.checked) box.scrollTop = box.scrollHeight; });
    auto.input.addEventListener('change', () => { if (auto.input.checked) box.scrollTop = box.scrollHeight; });
    clear.addEventListener('click', async () => {
      if (!(await F.confirm(t('logs.clearConfirm'), { ok: t('logs.clear') }))) return;
      try { await F.api('POST', 'logs/clear'); box.replaceChildren(); n = 0; count.textContent = ''; F.toast(t('logs.cleared')); } catch (e) { F.fail(e); }
    });
    async function poll() {
      try {
        const r = await F.api('GET', 'logs?since=' + since);
        if (!alive) return;
        if (r.last < since) { since = 0; box.replaceChildren(); n = 0; return poll(); }
        const items = r.items || [];
        if (items.length) {
          const frag = document.createDocumentFragment();
          for (const it of items) frag.append(logLine(it));
          box.append(frag);
          n += items.length;
          while (n > MAX && box.firstChild) { box.firstChild.remove(); n--; }
          if (auto.input.checked) box.scrollTop = box.scrollHeight;
        }
        since = r.last;
        count.textContent = t('logs.count', { n });
        if (!n && !box.querySelector('.empty')) box.append(F.empty('file', t('logs.empty')));
        else if (n) { const e = box.querySelector('.empty'); if (e) e.remove(); }
      } catch (e) { if (e.status !== 401) count.textContent = F.errMsg(e); }
      if (alive) timer = setTimeout(poll, 2000);
    }
    poll();
    return { destroy: () => { alive = false; clearTimeout(timer); } };
  }
  function logLine(it) {
    const lv = String(it.level || 'Info').toLowerCase();
    return h('div', { class: 'log-line lv-' + lv }, h('span', { class: 'log-time', text: it.time }), h('span', { class: 'log-lv', text: it.level }),
      it.source ? h('span', { class: 'log-src', text: it.source, title: it.source }) : null, h('span', { class: 'log-msg', text: it.message }));
  }

  function historyLogs(pane) {
    let page = 1, size = 100, q = '', seq = 0;
    const LEVELS = ['Info', 'Warn', 'Error', 'Fatal'];
    const levels = new Set();
    const chips = h('div', { class: 'chips', role: 'group', 'aria-label': t('logs.level') }, LEVELS.map((lv) => {
      const b = h('button', { type: 'button', class: 'chip chip-' + lv.toLowerCase(), 'aria-pressed': 'false', text: lv });
      b.addEventListener('click', () => {
        if (levels.has(lv)) levels.delete(lv); else levels.add(lv);
        b.setAttribute('aria-pressed', levels.has(lv) ? 'true' : 'false');
        page = 1; load();
      });
      return b;
    }));
    const search = F.input({ type: 'search', placeholder: t('logs.search'), 'aria-label': t('logs.search') });
    search.addEventListener('input', debounce(() => { q = search.value.trim(); page = 1; load(); }, 350));
    const total = h('span', { class: 'muted small mono' });
    const clear = F.btn(t('logs.clear'), { kind: 'ghost', icon: 'trash' });
    clear.addEventListener('click', async () => {
      if (!(await F.confirm(t('logs.clearConfirm'), { danger: true, ok: t('logs.clear') }))) return;
      try { await F.api('POST', 'logs/clear'); F.toast(t('logs.cleared')); page = 1; load(); } catch (e) { F.fail(e); }
    });
    const box = h('div', { class: 'logbox log-history', tabindex: '0', 'aria-label': t('logs.tabHistory') });
    const pg = pager((p) => { page = p; load(); });
    pane.append(h('section', { class: 'card log-card' },
      h('div', { class: 'toolbar' }, h('div', { class: 'field' }, h('span', { class: 'label', text: t('logs.level') }), chips),
        F.field(t('logs.searchLabel'), search, null, { cls: 'grow' }),
        F.field(t('pager.size'), sizeSelect([50, 100, 200], size, (n) => { size = n; page = 1; load(); }))),
      h('div', { class: 'row gap-s wrap log-meta' }, total, h('span', { class: 'grow' }), clear),
      box, pg));
    async function load() {
      const my = ++seq;
      box.classList.add('loading');
      try {
        const r = (await F.api('GET', 'logs/page?page=' + page + '&size=' + size + '&level=' + enc([...levels].join(',')) + '&q=' + enc(q))) || {};
        if (my !== seq) return;
        const items = r.items || [];
        total.textContent = t('logs.total', { n: F.fmtNum(r.total || 0) });
        box.replaceChildren(...(items.length ? items.map(logLine) : [F.empty('file', q || levels.size ? t('logs.noMatch') : t('logs.empty'))]));
        box.scrollTop = 0;
        page = r.page || page;
        pg.set(page, r.pages || 1);
      } catch (e) { if (my === seq) { F.fail(e); box.replaceChildren(F.empty('alert', t('err.loadFailed'))); } }
      finally { if (my === seq) box.classList.remove('loading'); }
    }
    load();
    return { destroy: () => { seq++; } };
  }

  const AUDIT_ACTIONS = ['login', 'login_failed', 'setup', 'password_changed', 'audit_cleared'];
  function auditLog(pane) {
    let page = 1, size = 50, q = '', seq = 0;
    const search = F.input({ type: 'search', placeholder: t('audit.search'), 'aria-label': t('audit.search') });
    search.addEventListener('input', debounce(() => { q = search.value.trim(); page = 1; load(); }, 350));
    const total = h('span', { class: 'muted small mono' });
    const clear = F.btn(t('audit.clear'), { kind: 'danger', icon: 'trash' });
    clear.addEventListener('click', async () => {
      if (!(await F.confirm(t('audit.clearConfirm'), { danger: true, ok: t('audit.clear') }))) return;
      try { await F.api('DELETE', 'audit'); F.toast(t('audit.cleared')); page = 1; q = ''; search.value = ''; load(); } catch (e) { F.fail(e); }
    });
    const wrap = h('div', { class: 'table-wrap' }, F.skeleton(3, 'sk-row'));
    const pg = pager((p) => { page = p; load(); });
    pane.append(h('section', { class: 'card' },
      h('div', { class: 'toolbar' }, F.field(t('logs.searchLabel'), search, null, { cls: 'grow' }),
        F.field(t('pager.size'), sizeSelect([50, 100, 200], size, (n) => { size = n; page = 1; load(); }))),
      h('div', { class: 'row gap-s wrap log-meta' }, total, h('span', { class: 'grow' }), F.iconBtn('refresh', t('common.refresh'), () => load()), clear),
      wrap, pg));
    const actionLabel = (a) => (AUDIT_ACTIONS.includes(a) ? h('span', { class: 'badge', title: a, text: t('audit.act.' + a) }) : h('code', { text: a || '' }));
    async function load() {
      const my = ++seq;
      try {
        const r = (await F.api('GET', 'audit?page=' + page + '&size=' + size + '&q=' + enc(q))) || {};
        if (my !== seq) return;
        const items = r.items || [];
        total.textContent = t('logs.total', { n: F.fmtNum(r.total || 0) });
        page = r.page || page;
        pg.set(page, r.pages || 1);
        if (!items.length) { wrap.replaceChildren(F.empty('list', q ? t('logs.noMatch') : t('settings.auditEmpty'))); return; }
        wrap.replaceChildren(h('table', { class: 'table table-compact audit-table' },
          h('thead', null, h('tr', null, [t('settings.auditTime'), t('settings.auditUser'), 'IP', t('settings.auditAction'), t('settings.auditDetail')].map((x) => h('th', { scope: 'col', class: x === 'IP' ? 'no-tt' : null, text: x })))),
          h('tbody', null, items.map((a) => h('tr', null,
            h('td', { 'data-label': t('settings.auditTime'), class: 'muted nowrap', text: F.fmtDate(a.time) }),
            h('td', { 'data-label': t('settings.auditUser'), text: a.user || '–' }),
            h('td', { 'data-label': 'IP', class: 'mono small', text: a.ip || '–' }),
            h('td', { 'data-label': t('settings.auditAction') }, actionLabel(a.action)),
            h('td', { 'data-label': t('settings.auditDetail'), class: 'muted wrap-any', text: a.detail || '' }))))));
      } catch (e) { if (my === seq) { F.fail(e); wrap.replaceChildren(F.empty('alert', t('err.loadFailed'))); } }
    }
    load();
    return { destroy: () => { seq++; } };
  }

  // ================================================================== FAQ
  Pages.faq = function (root) {
    const q = F.input({ type: 'search', placeholder: t('faq.search'), 'aria-label': t('faq.search') });
    const list = h('div', { class: 'faq' });
    root.append(F.pageHead(t('faq.title'), t('faq.sub')), h('div', { class: 'input-ic faq-search' }, icon('search'), q), list);
    const data = (window.FAQ && (window.FAQ[F.state.lang] || window.FAQ.en || window.FAQ.tr)) || [];
    const block = (p) => {
      if (Array.isArray(p)) return h('ul', null, p.map((li) => h('li', null, F.rich(li))));
      if (p && p.table) {
        const [head, ...rows] = p.table;
        return h('div', { class: 'table-wrap' }, h('table', { class: 'table table-compact faq-table' },
          h('thead', null, h('tr', null, head.map((c) => h('th', { scope: 'col', text: c })))),
          h('tbody', null, rows.map((r) => h('tr', null, r.map((c, i) => h('td', { 'data-label': head[i] }, F.rich(c))))))));
      }
      return h('p', null, F.rich(p));
    };
    const items = [];
    const toc = h('nav', { class: 'faq-toc', 'aria-label': t('faq.toc') });
    data.forEach((sec, si) => {
      const secId = 'faq-s' + si;
      toc.append(h('a', { href: '#' + secId, class: 'tag', text: sec.section, onclick: (e) => { e.preventDefault(); document.getElementById(secId).scrollIntoView({ behavior: 'smooth', block: 'start' }); } }));
      const group = h('section', { class: 'faq-section', id: secId }, h('h2', { class: 'faq-sec-title', text: sec.section }));
      (sec.items || []).forEach((it) => {
        const d = h('details', { class: 'faq-item', id: 'faq-' + it.id },
          h('summary', null, h('span', { class: 'faq-q', text: it.q }), icon('chevron', 'faq-chev')),
          h('div', { class: 'faq-body' }, (it.a || []).map(block)));
        d.dataset.search = (it.q + ' ' + JSON.stringify(it.a)).toLowerCase();
        items.push(d);
        group.append(d);
      });
      list.append(group);
    });
    list.before(toc);
    const none = F.empty('help', t('faq.none'));
    none.hidden = true;
    list.append(none);
    if (location.hash.startsWith('#faq-')) {
      const target = document.getElementById(location.hash.slice(1));
      if (target && target.tagName === 'DETAILS') { target.open = true; setTimeout(() => target.scrollIntoView({ block: 'start' }), 60); }
    }
    q.addEventListener('input', () => {
      const v = q.value.trim().toLowerCase();
      let shown = 0;
      items.forEach((d) => { const ok = !v || d.dataset.search.includes(v); d.hidden = !ok; if (ok) { shown++; if (v) d.open = true; } });
      list.querySelectorAll('.faq-section').forEach((g) => { g.hidden = ![...g.querySelectorAll('.faq-item')].some((d) => !d.hidden); });
      none.hidden = shown > 0;
    });
  };
})();
