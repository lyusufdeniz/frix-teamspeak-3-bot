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

  /** one built-in command: usage + new name. Saved when the field is left or Enter is pressed; the old name keeps working. */
  function builtinRow(b) {
    const supported = 'alias' in b;
    const alias = F.input({ value: b.alias || '', placeholder: b.trigger, maxlength: 32, 'aria-label': t('commands.renameLabel', { trigger: b.trigger }), disabled: !supported });
    const state = h('span', { class: 'builtin-state', 'aria-hidden': 'true' });
    let saving = false;
    const save = async () => {
      const v = alias.value.trim().replace(/^!/, '');
      if (saving || v === (b.alias || '')) return;
      saving = true; state.replaceChildren(icon('refresh')); state.className = 'builtin-state busy';
      try {
        await F.api('PUT', 'commands/builtin/' + enc(b.trigger), { alias: v, keepOld: true });
        b.alias = v || null; alias.value = v;
        state.replaceChildren(icon('check')); state.className = 'builtin-state ok';
        F.toast(v ? t('commands.renamed', { from: b.trigger, to: v }) : t('commands.renameReset', { trigger: b.trigger }));
        bumpAvatar();
      } catch (e) { F.fail(e); state.replaceChildren(); state.className = 'builtin-state'; alias.value = b.alias || ''; }
      saving = false;
    };
    alias.addEventListener('change', save);
    alias.addEventListener('keydown', (e) => { if (e.key === 'Enter') { e.preventDefault(); alias.blur(); } });
    return h('div', { class: 'builtin-row' },
      h('div', { class: 'builtin-usage-cell' }, h('code', { class: 'builtin-usage', text: b.usage })),
      h('div', { class: 'input-prefix builtin-alias' }, h('span', { text: '!' }), alias), state);
  }

  /** rename rows for the built-in commands that belong to one plugin (or to none: id = null) */
  function commandsBlock(builtins, id) {
    const ids = id == null ? null : [].concat(id);
    const mine = builtins.filter((b) => (ids ? ids.includes((b.plugins || [])[0]) : !(b.plugins || []).length));
    if (!mine.length) return null;
    return h('div', { class: 'plugin-cmds' },
      h('div', { class: 'plugin-cmds-head' }, icon('terminal'), h('strong', { text: t('plugins.commands') }), h('span', { class: 'muted small', text: t('plugins.commandsHelp') })),
      h('div', { class: 'builtin-list' }, mine.map(builtinRow)));
  }

  /** preview of the generated idle avatar (shown in the Avatar plugin) */
  function avatarPreview() {
    const img = h('img', { class: 'avatar-img', alt: t('commands.avatarAlt'), src: '/panel/api/avatar?t=' + F.avatarStamp });
    const box = h('figure', { class: 'avatar-box' }, img, h('figcaption', null, h('span', { class: 'label-inline', text: t('commands.avatar') }),
      h('span', { class: 'muted small avatar-missing', text: t('commands.avatarNone') }),
      F.iconBtn('refresh', t('common.refresh'), bumpAvatar, 'sm')));
    img.tabIndex = 0;
    img.setAttribute('role', 'button');
    img.title = t('commands.avatar');
    const openBig = () => F.modal({ title: t('commands.avatar'), body: h('div', { class: 'avatar-full' }, h('img', { src: '/panel/api/avatar?t=' + F.avatarStamp, alt: t('commands.avatarAlt') })), actions: [{ label: t('common.close') }] });
    img.addEventListener('click', openBig);
    img.addEventListener('keydown', (e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); openBig(); } });
    img.addEventListener('error', () => box.classList.add('missing'));
    img.addEventListener('load', () => box.classList.remove('missing'));
    return box;
  }

  // icon per plugin (falls back to "box")
  const PLUGIN_ICONS = {
    g_music: 'music', g_radio: 'radio', g_look: 'image', g_reconnect: 'refresh', g_welcome: 'hand', g_schedule: 'clock',
    g_moderation: 'shield', g_server: 'server', g_activity: 'chart', g_fun: 'dice',
    music: 'music', radio: 'radio', npstatus: 'idcard', avatar: 'image', welcome: 'hand', advertisement: 'megaphone', scheduled: 'calendar',
    alonepause: 'pause', idleradio: 'radio', voteskip: 'vote', djmode: 'headphones', afkmover: 'moon', namefilter: 'filter', channelguard: 'shield',
    antiflood: 'zap', autoreconnect: 'refresh', normalize: 'volume', autoplay: 'repeat', favorites: 'star', scheduleplay: 'clock', activity: 'chart', registration: 'userPlus', tempchannel: 'folderPlus', ranks: 'trophy', supportnotify: 'bell', channelinfo: 'info', fun: 'dice',
  };

  // ================================================================== PLUGINS
  Pages.plugins = function (root) {
    // /plugins/custom = custom commands, /plugins/texts = bot replies (sidebar sub-menu)
    const sub = location.pathname.replace(/\/+$/, '').split('/')[2];
    if (sub === 'custom') { root.append(F.pageHead(t('commands.custom'), t('commands.customSub'))); commandsTab(root); return {}; }
    if (sub === 'texts') { root.append(F.pageHead(t('commands.tabTexts'), t('commands.textsSub'))); textsTab(root); return {}; }
    let botName = F.state.selected;
    let builtins = [];
    const sel = h('select', { class: 'input', 'aria-label': t('plugins.bot') });
    sel.addEventListener('change', () => { botName = sel.value; load(); });
    const list = h('div', { class: 'plugin-list' }, F.skeleton(3, 'sk-row'));
    root.append(F.pageHead(t('plugins.title'), t('plugins.sub'), h('label', { class: 'inline-field' }, h('span', { class: 'label-inline', text: t('plugins.bot') }), sel)), list);
    let radios = [];
    let channels = [];
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
        const [pl, rd, cm, ch] = await Promise.all([F.api('GET', 'plugins/' + enc(botName)), F.api('GET', 'radios'), F.api('GET', 'commands'),
          F.api('GET', 'bots/' + enc(botName) + '/channels').catch(() => null)]);
        channels = (ch && ch.channels) || [];
        radios = rd || [];
        builtins = (cm && cm.builtin) || [];
        list.replaceChildren(...grouped(pl || []));
        const general = commandsBlock(builtins, null);
        const tpa = F.$('#top-page-actions');
        if (general && tpa && !tpa.querySelector('.gen-cmds')) tpa.prepend(F.btn(t('plugins.generalCmds'), { kind: 'ghost', icon: 'terminal', cls: 'gen-cmds',
          onClick: () => F.modal({ title: t('plugins.generalCmds'), size: 'wide', body: commandsBlock(builtins, null), actions: [{ label: t('common.close') }] }) }));
        if (!pl || !pl.length) list.append(F.empty('box', t('plugins.none')));
      } catch (e) { F.fail(e); list.replaceChildren(F.empty('alert', t('err.loadFailed'))); }
    }
    // sections keep the (now 17) plugins scannable; unknown ids land in "other"
    const GROUPS = [
      ['core', ['g_music', 'g_radio', 'g_look', 'g_reconnect']],
      ['community', ['g_welcome', 'g_moderation', 'g_server', 'g_activity']],
      ['extras', ['g_schedule', 'g_fun']],
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
          h('div', { class: 'plugin-group-head' }, h('h2', { id: hid, text: t('plugins.group.' + key) }), count), h('div', { class: 'plugin-grid' }, cards));
      });
    }
    function card(p) {
      const cfg = p.config || {};
      const getters = [];
      const parts = p.parts || [];
      const sections = [];
      let curSec = null;
      const fields = (p.schema || []).map((f) => {
        const val = cfg[f.key] !== undefined ? cfg[f.key] : f.default;
        let ctl, get;
        if (f.type === 'section') {
          // a feature inside the group: its own on/off switch, its settings follow
          const sw = F.switchEl(val === true || val === 'true', F.loc(f.label));
          // collapsible card: the header toggles the settings below it (closed by default)
          const chev = h('button', { type: 'button', class: 'pf-sec-toggle', 'aria-expanded': 'false', 'aria-label': F.loc(f.label) }, icon('chevron'));
          const secEl = h('div', { class: 'pf-section span2 collapsed' }, h('span', { class: 'plugin-ic sm' }, icon(PLUGIN_ICONS[f.key] || 'box')), sw, chev,
            f.help ? h('p', { class: 'muted small pf-sec-help', text: F.loc(f.help) }) : null);
          curSec = { id: f.key, sw, els: [], head: secEl };
          sections.push(curSec);
          const sec = curSec;
          sec.setOpen = (open) => {
            secEl.classList.toggle('collapsed', !open);
            chev.setAttribute('aria-expanded', String(open));
            sec.els.forEach((e) => e.classList.toggle('sec-hidden', !open));
          };
          chev.addEventListener('click', () => sec.setOpen(secEl.classList.contains('collapsed')));
          secEl.addEventListener('click', (e) => { if (!e.target.closest('.switch, button, input, label')) chev.click(); });
          sw.input.addEventListener('change', () => { sec.els.forEach((e) => e.classList.toggle('sec-off', !sw.input.checked)); if (sw.input.checked) sec.setOpen(true); });
          getters.push([f.key, () => sw.input.checked]);
          return secEl;
        }
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
        } else if (f.type === 'channel') {
          // themed dropdown with the bot's channels (indented tree); free text when the bot is offline
          if (channels.length) {
            const v = val == null ? '' : String(val);
            const opts = [['', t('plugins.noChannel')]].concat(channels.map((c) => [String(c.id), '\u2003'.repeat(c.depth) + c.name]));
            const byName = !opts.some((o) => o[0] === v) && v ? channels.find((c) => c.name.toLowerCase() === v.toLowerCase()) : null;
            if (v && !byName && !opts.some((o) => o[0] === v)) opts.push([v, v + ' (?)']);
            ctl = F.select(opts, byName ? String(byName.id) : v); get = () => ctl.value;
          } else { ctl = F.input({ value: val == null ? '' : String(val), placeholder: t('plugins.channelPh') }); get = () => ctl.value; }
        } else if (f.type === 'channels') {
          const cur = String(val || '').split('\n').map((x) => x.trim()).filter(Boolean);
          if (channels.length) {
            const chosen = new Set(cur.map((x) => { const c = channels.find((ch) => String(ch.id) === x.replace(/^\//, '') || ch.name.toLowerCase() === x.toLowerCase()); return c ? String(c.id) : x; }));
            const boxes = channels.map((c) => {
              const cb = h('input', { type: 'checkbox', value: String(c.id), checked: chosen.has(String(c.id)) });
              return h('label', { class: 'ch-pick', style: 'padding-left:' + (8 + c.depth * 16) + 'px' }, cb, h('span', { text: c.name }));
            });
            ctl = h('div', { class: 'ch-list' }, boxes);
            const unknown = [...chosen].filter((x) => !channels.some((c) => String(c.id) === x));
            get = () => boxes.filter((b) => b.querySelector('input').checked).map((b) => b.querySelector('input').value).concat(unknown).join('\n');
          } else { ctl = F.textarea({ value: cur.join('\n'), rows: 4, placeholder: t('plugins.channelsPh') }); get = () => ctl.value; }
        } else { ctl = F.input({ value: val == null ? '' : String(val) }); get = () => ctl.value; }
        getters.push([f.key, get]);
        const help = f.help ? F.loc(f.help) : null;
        const bb = BB_FIELDS.has(f.key) && (f.type === 'text' || f.type === 'textarea');
        if (bb) ctl = bbField(ctl).el;
        const fe = f.type === 'bool' ? h('div', { class: 'field' }, ctl, help ? h('div', { class: 'help', text: help }) : null) : F.field(F.loc(f.label), ctl, help, { cls: bb || f.type === 'textarea' || f.type === 'channels' ? 'span2' : '' });
        if (curSec) curSec.els.push(fe);
        return fe;
      });
      // commands (and the avatar preview) go under their own section
      const items = [];
      let cmdTotal = 0;
      const addExtras = (partId) => {
        if (partId === 'avatar') items.push(h('div', { class: 'span2' }, avatarPreview()));
        const cb = commandsBlock(builtins, partId);
        if (cb) { cb.classList.add('span2'); cmdTotal += cb.querySelectorAll('.builtin-row').length; items.push(cb); return cb; }
        return null;
      };
      if (sections.length) {
        let sec = null;
        fields.forEach((fe) => {
          const s2 = sections.find((x) => x.sw.closest('.pf-section') === fe);
          if (s2) { if (sec) { const x = addExtras(sec.id); if (x) sec.els.push(x); } sec = s2; }
          items.push(fe);
        });
        if (sec) { const x = addExtras(sec.id); if (x) sec.els.push(x); }
        sections.forEach((x) => { x.els.forEach((e) => e.classList.toggle('sec-off', !x.sw.input.checked)); x.setOpen(false); });
        // a feature without settings or commands needs no toggle arrow
        sections.forEach((x) => { if (!x.els.length) x.head.classList.add('no-body'); });
      } else {
        items.push(...fields);
        parts.forEach((pt) => addExtras(pt.id));
      }
      const collect = () => { const c = {}; getters.forEach(([k, g]) => { c[k] = g(); }); return c; };
      const enabled = F.switchEl(p.enabled, null, { 'aria-label': t('plugins.enabled') + ': ' + F.loc(p.name) });
      const el = h('article', { class: 'card plugin-tile' + (p.enabled ? ' on' : '') });
      const save = async (btn, quiet) => F.busy(btn, async () => {
        try {
          await F.api('PUT', 'plugins/' + enc(botName) + '/' + enc(p.id), { enabled: enabled.input.checked, config: collect() });
          el.classList.toggle('on', enabled.input.checked);
          if (el.onToggle) el.onToggle();
          F.toast(quiet ? (enabled.input.checked ? t('plugins.on', { name: F.loc(p.name) }) : t('plugins.off', { name: F.loc(p.name) })) : t('common.saved'));
          F.refreshBots();
          return true;
        } catch (e) { F.fail(e); if (quiet) enabled.input.checked = !enabled.input.checked; return false; }
      });
      enabled.input.addEventListener('change', async () => {
        const ok = await save(null, true);
        // a group switched on with none of its features on: open the dialog to pick them
        if (ok && enabled.input.checked && sections.length && !sections.some((x) => x.sw.input.checked)) { openSettings(); F.toast(t('plugins.pickFeatures'), 'info'); }
      });
      const hasSettings = items.length > 0;
      // settings + commands open in a dialog; the tile stays compact
      const body = h('div', { class: 'plugin-body' },
        h('p', { class: 'muted small', text: F.loc(p.description) }),
        items.length ? h('div', { class: 'form-grid' }, items) : null);
      const openSettings = () => F.modal({
        title: [h('span', { class: 'plugin-ic sm' }, icon(PLUGIN_ICONS[p.id] || 'box')), ' ', F.loc(p.name)], size: 'wide', body,
        actions: fields.length > sections.length || sections.length ? [{ label: t('common.cancel') }, { label: t('common.save'), kind: 'primary', primary: true, icon: 'check', onClick: () => save(null, false) }] : [{ label: t('common.close') }],
      });
      const cfgBtn = hasSettings ? F.btn(t('plugins.configure'), { kind: 'ghost', icon: 'sliders', cls: 'btn-sm', onClick: openSettings }) : null;
      el.append(
        h('div', { class: 'tile-top' }, h('span', { class: 'plugin-ic' }, icon(PLUGIN_ICONS[p.id] || 'box')),
          h('div', { class: 'tile-titles' }, h('h3', { class: 'plugin-name', text: F.loc(p.name) }),
            h('span', { class: 'plugin-state', text: p.enabled ? t('plugins.stateOn') : t('plugins.stateOff') })), enabled),
        h('p', { class: 'plugin-desc', text: F.loc(p.description), title: F.loc(p.description) }),
        h('div', { class: 'tile-foot' }, cfgBtn, cmdTotal ? h('span', { class: 'muted small', text: t('plugins.cmdCount', { n: cmdTotal }) }) : null));
      if (hasSettings) el.addEventListener('click', (e) => { if (!e.target.closest('.switch, button')) openSettings(); });
      enabled.input.addEventListener('change', () => { el.querySelector('.plugin-state').textContent = enabled.input.checked ? t('plugins.stateOn') : t('plugins.stateOff'); });
      return el;
    }
    let first = true;
    function onBots() { fillSel(); if (first) { first = false; load(); } }
    return { onBots };
  };

  // ================================================================== COMMANDS + TEXTS
  function commandsTab(pane) {
    const custom = h('div', { class: 'cmd-list' }, F.skeleton(3, 'sk-row'));
    setTimeout(() => F.fitPage(custom));
    pane.append(
      h('section', { class: 'card' }, h('div', { class: 'card-head' }, h('h2', { class: 'card-title' }, icon('terminal'), t('commands.custom')),
        F.btn(t('commands.new'), { kind: 'primary', icon: 'plus', onClick: () => form(null) })),
      h('p', { class: 'muted small' }, F.rich(t('commands.customHelp'))), custom));
    let data = null;
    async function load() {
      try { data = await F.api('GET', 'commands'); render(); }
      catch (e) { F.fail(e); custom.replaceChildren(F.empty('alert', t('err.loadFailed'))); }
    }
    function render() {
      const items = data.custom || [];
      if (!items.length) { custom.replaceChildren(F.empty('terminal', t('commands.none'), t('commands.noneHint'))); return; }
      const head = h('div', { class: 'tbl-row tbl-head fit-skip' },
        h('span', { text: t('commands.f.trigger') }), h('span', { text: t('commands.f.kind') }), h('span', { text: t('commands.f.value') }),
        h('span', { text: t('commands.f.desc') }), h('span', { text: t('plugins.enabled') }), h('span'));
      custom.replaceChildren(head, ...items.map((c) => {
        const sw = F.switchEl(c.enabled, null, { 'aria-label': t('commands.enabled') + ': !' + c.trigger });
        sw.input.addEventListener('change', async () => {
          try { await F.api('PUT', 'commands/' + c.id, Object.assign({}, c, { enabled: sw.input.checked })); c.enabled = sw.input.checked; row.classList.toggle('disabled', !c.enabled); F.toast(t('common.saved')); bumpAvatar(); }
          catch (e) { sw.input.checked = !sw.input.checked; F.fail(e); }
        });
        const row = h('div', { class: 'tbl-row cmd-trow' + (c.enabled ? '' : ' disabled') },
          h('code', { class: 'cmd-trigger', text: '!' + c.trigger }),
          h('span', null, F.badge(t('commands.kind.' + c.kind), c.kind === 'alias' ? 'info' : 'accent')),
          h('span', { class: 'cmd-value', text: c.value, title: c.value }),
          h('span', { class: 'muted small tbl-ellipsis', text: c.description || '–', title: c.description || '' }),
          sw,
          h('span', { class: 'tbl-actions' }, F.btn(t('common.edit'), { kind: 'ghost', icon: 'edit', cls: 'btn-sm', onClick: () => form(c) }), F.iconBtn('trash', t('common.delete'), () => del(c), 'danger')));
        return row;
      }));
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
      const bbv = bbField(value);
      const desc = F.input({ value: c && c.description ? c.description : '', maxlength: 200 });
      const en = F.switchEl(c ? c.enabled : true, t('commands.enabled'));
      const sync = () => {
        kindHelp.replaceChildren(icon('info'), h('div', null, F.rich(t('commands.kindHelp.' + kind.value))));
        value.placeholder = kind.value === 'alias' ? '!play' : 'Discord: https://discord.gg/… {user}';
        const txt = kind.value === 'text';
        bbv.bar.hidden = !txt; bbv.prev.classList.toggle('off', !txt);
      };
      kind.addEventListener('change', sync); sync();
      F.modal({
        title: c ? t('commands.edit') : t('commands.new'),
        body: h('div', null,
          F.field(t('commands.f.trigger'), h('div', { class: 'input-prefix' }, h('span', { text: '!' }), trig), t('commands.f.triggerHelp')),
          F.field(t('commands.f.kind'), kind), kindHelp,
          F.field(t('commands.f.value'), bbv.el),
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

  // ---- BBCode (TeamSpeak) helpers for the bot-texts editor
  const BB_SWATCHES = ['#00FF00', '#FF5555', '#FFD54A', '#4FC3F7', '#FF9800', '#E040FB', '#FFFFFF', '#9E9E9E'];
  /** wraps the textarea selection in open…close */
  function bbWrap(ta, open, close) {
    const a = ta.selectionStart, b = ta.selectionEnd, v = ta.value;
    ta.value = v.slice(0, a) + open + v.slice(a, b) + close + v.slice(b);
    ta.focus();
    ta.setSelectionRange(a + open.length, b + open.length);
    ta.dispatchEvent(new Event('input'));
  }
  function bbToolbar(ta) {
    const b = (label, tag, title, cls) => h('button', { type: 'button', class: 'bb-btn ' + cls, title, 'aria-label': title, text: label, onclick: () => bbWrap(ta, '[' + tag + ']', '[/' + tag + ']') });
    const custom = h('input', { type: 'color', class: 'bb-color', value: '#00ff00', title: t('texts.bb.color'), 'aria-label': t('texts.bb.color') });
    custom.addEventListener('change', () => bbWrap(ta, '[color=' + custom.value.toUpperCase() + ']', '[/color]'));
    const sw = BB_SWATCHES.map((c) => h('button', { type: 'button', class: 'bb-swatch', style: '--c:' + c, title: c, 'aria-label': t('texts.bb.color') + ' ' + c, onclick: () => bbWrap(ta, '[color=' + c + ']', '[/color]') }));
    const strip = h('button', { type: 'button', class: 'bb-btn', title: t('texts.bb.clear'), 'aria-label': t('texts.bb.clear'), text: '⌫', onclick: () => {
      const a = ta.selectionStart, z = ta.selectionEnd;
      const [x, y] = a === z ? [0, ta.value.length] : [a, z];
      ta.value = ta.value.slice(0, x) + ta.value.slice(x, y).replace(/\[\/?(b|i|u|s|color|url|size)(=[^\]]*)?\]/gi, '') + ta.value.slice(y);
      ta.dispatchEvent(new Event('input'));
    } });
    return h('div', { class: 'bb-bar', role: 'toolbar', 'aria-label': 'BBCode' },
      b('B', 'b', t('texts.bb.bold'), 'b'), b('I', 'i', t('texts.bb.italic'), 'i'), b('U', 'u', t('texts.bb.underline'), 'u'), b('S', 's', t('texts.bb.strike'), 's'),
      b('URL', 'url', t('texts.bb.link'), 'url'),
      h('span', { class: 'bb-sep' }), ...sw, custom, h('span', { class: 'bb-sep' }), strip);
  }
  // plugin fields whose text is sent to TeamSpeak chat / pokes (BBCode works there)
  const BB_FIELDS = new Set(['welcome.message', 'advertisement.messages', 'advertisement.schedule', 'afkmover.message', 'namefilter.message',
    'supportnotify.staffMessage', 'supportnotify.userMessage', 'supportnotify.noStaffMessage',
    'registration.staffMessage', 'registration.userMessage', 'registration.doneMessage',
    'ranks.message', 'channelguard.message', 'antiflood.warnMessage']);
  /** text field + BBCode toolbar + live preview */
  function bbField(ctl) {
    const prev = h('div', { class: 'bb-preview bb-field-prev' });
    const paint = () => { prev.hidden = !ctl.value.trim(); prev.replaceChildren(bbRender(ctl.value, null)); };
    ctl.addEventListener('input', paint); paint();
    const bar = bbToolbar(ctl);
    return { el: h('div', { class: 'bb-field' }, bar, ctl, prev), bar, prev, paint };
  }
  const safeColor = (c) => (/^(#[0-9a-f]{3}|#[0-9a-f]{6}|[a-z]{3,20})$/i.test(String(c).trim()) ? String(c).trim() : 'inherit');
  /** safe BBCode → DOM preview (only tags TeamSpeak supports in chat), mimics FrixChat.Color */
  function bbRender(text, lk) {
    const root = h('span', { class: 'bb-out' });
    let cur = root;
    if (lk && !/\[color/i.test(text)) {
      if (lk.bold && !/\[b\]/i.test(text)) { const bEl = h('b'); cur.append(bEl); cur = bEl; }
      if (lk.color && String(lk.color).toLowerCase() !== 'none') { const c = h('span', { style: 'color:' + safeColor(lk.color) }); cur.append(c); cur = c; }
    }
    const stack = [cur];
    const top = () => stack[stack.length - 1];
    const re = /\[(\/?)(b|i|u|s|color|url)(?:=([^\]]*))?\]/gi;
    let last = 0, m;
    while ((m = re.exec(text))) {
      if (m.index > last) top().append(document.createTextNode(text.slice(last, m.index)));
      last = re.lastIndex;
      const tag = m[2].toLowerCase();
      if (m[1]) { if (stack.length > 1 && top().dataset.tag === tag) stack.pop(); continue; }
      const el = tag === 'color' ? h('span', { style: 'color:' + safeColor(m[3] || '') })
        : tag === 'url' ? h('span', { class: 'bb-link' }) : h(tag);
      el.dataset.tag = tag;
      top().append(el); stack.push(el);
    }
    if (last < text.length) top().append(document.createTextNode(text.slice(last)));
    return root;
  }
  /** the BBCode the bot really sends: default colour / bold added like FrixChat.Color on the server */
  function withLook(text, lk) {
    if (!lk || !text.trim() || /\[color/i.test(text)) return text;
    const body = String(lk.color).toLowerCase() === 'none' ? text : '[color=' + lk.color + ']' + text + '[/color]';
    return lk.bold && !/\[b\]/i.test(text) ? '[b]' + body + '[/b]' : body;
  }
  // current chat look (default colour / bold); text previews repaint when it changes
  let look = { color: '#00FF00', errorColor: '#FF5555', bold: true };
  let previews = [];

  function lookCard() {
    const color = h('input', { type: 'color', class: 'look-color', value: '#00ff00', 'aria-label': t('texts.look.color') });
    const none = h('input', { type: 'checkbox' });
    const err = h('input', { type: 'color', class: 'look-color', value: '#ff5555', 'aria-label': t('texts.look.errorColor') });
    const bold = F.switchEl(true, t('texts.look.bold'));
    const sample = h('div', { class: 'bb-preview look-sample' });
    const cur = () => ({ color: none.checked ? 'none' : color.value.toUpperCase(), errorColor: err.value.toUpperCase(), bold: bold.input.checked });
    const paint = () => {
      color.disabled = none.checked;
      const c = cur();
      sample.replaceChildren(bbRender(t('texts.look.sample'), c), h('br'), bbRender(t('texts.look.sampleErr'), Object.assign({}, c, { color: c.errorColor })));
    };
    [color, none, err, bold.input].forEach((x) => x.addEventListener('input', paint));
    none.addEventListener('change', paint); bold.input.addEventListener('change', paint);
    const toHex = (c) => (/^#[0-9a-f]{6}$/i.test(c) ? c.toLowerCase() : /^#[0-9a-f]{3}$/i.test(c) ? '#' + c.slice(1).split('').map((x) => x + x).join('').toLowerCase() : null);
    F.api('GET', 'settings').then((st) => {
      if (!st || !st.chat) return;
      look = st.chat;
      none.checked = String(look.color).toLowerCase() === 'none';
      if (toHex(look.color)) color.value = toHex(look.color);
      if (toHex(look.errorColor)) err.value = toHex(look.errorColor);
      bold.input.checked = !!look.bold;
      paint(); previews.forEach((f) => f());
    }).catch(() => { /* defaults */ });
    const save = F.btn(t('common.save'), { kind: 'primary', icon: 'check' });
    save.addEventListener('click', () => F.busy(save, async () => {
      try { await F.api('PUT', 'settings', { chat: cur() }); look = cur(); previews.forEach((f) => f()); F.toast(t('common.saved')); }
      catch (e) { F.fail(e); }
    }));
    paint();
    return h('section', { class: 'card' },
      h('h2', { class: 'card-title' }, icon('text'), t('texts.look.title')),
      h('p', { class: 'muted small' }, F.rich(t('texts.look.help'))),
      h('div', { class: 'look-grid' },
        F.field(t('texts.look.color'), h('div', { class: 'row gap-s' }, color, h('label', { class: 'check' }, none, h('span', { text: t('texts.look.none') })))),
        F.field(t('texts.look.errorColor'), err),
        bold),
      sample, h('div', { class: 'look-foot' }, save));
  }

  function textsTab(pane) {
    previews = [];
    const lang = F.state.lang;
    const filter = F.input({ type: 'search', placeholder: t('texts.filter'), 'aria-label': t('texts.filter') });
    const list = h('div', { class: 'text-list' }, F.skeleton(4, 'sk-row'));
    setTimeout(() => F.fitPage(list));
    filter.addEventListener('input', applyFilter);
    F.$('#top-page-actions').append(F.btn(t('texts.look.title'), { kind: 'ghost', icon: 'text', onClick: () => F.modal({ title: t('texts.look.title'), size: 'wide', body: lookCard(), actions: [{ label: t('common.close') }] }) }));
    pane.append(h('section', { class: 'card texts-card' },
      h('p', { class: 'muted small' }, F.rich(t('texts.help'))),
      h('div', { class: 'toolbar' }, F.field(t('texts.filter'), filter, null, { cls: 'grow' })),
      list));
    function applyFilter() {
      const q = filter.value.trim().toLowerCase();
      list.querySelectorAll('.text-row').forEach((r) => { r.hidden = !!q && !r.dataset.search.includes(q); });
    }
    async function load() {
      list.replaceChildren(...F.skeleton(4, 'sk-row'));
      try {
        const items = (await F.api('GET', 'texts?lang=' + enc(lang))) || [];
        list.replaceChildren(h('div', { class: 'tbl-row text-trow tbl-head fit-skip' },
          h('span', { text: t('texts.key') }), h('span', { text: t('texts.bb.preview') }), h('span', { text: t('texts.state') }), h('span')), ...items.map(row));
        applyFilter();
      } catch (e) { F.fail(e); list.replaceChildren(F.empty('alert', t('err.loadFailed'))); }
    }
    // table row: key | preview | state | edit / reset
    function row(it) {
      const isErr = ['failed', 'stream.error'].includes(it.key);
      const lookFor = () => (isErr ? Object.assign({}, look, { color: look.errorColor }) : look);
      const prev = h('div', { class: 'text-prev' });
      const state = h('span');
      const resetB = F.iconBtn('refresh', t('texts.reset'), async () => {
        try { await F.api('PUT', 'texts', { lang, key: it.key, value: '' }); it.custom = false; it.value = it.defaultValue; paint(); F.toast(t('texts.wasReset')); if (it.key.startsWith('avatar.')) bumpAvatar(); }
        catch (e) { F.fail(e); }
      });
      const paint = () => {
        prev.replaceChildren(bbRender(it.value, lookFor()));
        state.replaceChildren(it.custom ? F.badge(t('texts.custom'), 'accent') : F.badge(t('texts.defaultState')));
        resetB.disabled = !it.custom;
        el.dataset.search = (it.key + ' ' + it.value + ' ' + it.defaultValue).toLowerCase();
      };
      const el = h('div', { class: 'tbl-row text-row text-trow' },
        h('code', { class: 'text-key', text: it.key, title: it.key }), prev, state,
        h('span', { class: 'tbl-actions' }, F.btn(t('common.edit'), { kind: 'ghost', icon: 'edit', cls: 'btn-sm', onClick: () => edit(it, lookFor, paint) }), resetB));
      previews.push(paint);
      paint();
      return el;
    }

    function edit(it, lookFor, paintRow) {
      // chat texts open with the colour / bold the bot adds, so the codes are visible and editable
      const chat = !it.key.startsWith('avatar.');
      const shown = chat ? withLook(it.value, lookFor()) : it.value;
      const ta = F.textarea({ value: shown, rows: 5, 'aria-label': it.key, class: 'input text-editor' });
      const prev = h('div', { class: 'bb-preview' });
      const paint = () => prev.replaceChildren(bbRender(ta.value, lookFor()));
      ta.addEventListener('input', paint);
      paint();
      const put = async (value) => {
        try {
          await F.api('PUT', 'texts', { lang, key: it.key, value });
          const custom = !!value && value !== it.defaultValue;
          it.custom = custom; it.value = value || it.defaultValue;
          paintRow();
          F.toast(custom ? t('common.saved') : t('texts.wasReset'));
          if (it.key.startsWith('avatar.')) bumpAvatar();
          return true;
        } catch (e) { F.fail(e); return false; }
      };
      const insertVar = (v) => { const i = ta.selectionStart; ta.value = ta.value.slice(0, i) + v + ta.value.slice(ta.selectionEnd); ta.focus(); ta.setSelectionRange(i + v.length, i + v.length); paint(); };
      const vars = [...new Set((it.vars || []).map((v) => '{' + v + '}').concat(it.defaultValue.match(/\{\w+\}/g) || []))];
      F.modal({
        title: [h('code', { class: 'text-key', text: it.key })],
        size: 'wide',
        body: h('div', { class: 'text-edit' },
          bbToolbar(ta), ta,
          vars.length ? h('div', { class: 'text-vars' }, h('span', { class: 'muted small', text: t('texts.vars') }),
            vars.map((v) => h('button', { type: 'button', class: 'var-chip', text: v, onclick: () => insertVar(v) }))) : null,
          h('div', { class: 'label', text: t('texts.bb.preview') }), prev,
          h('div', { class: 'muted small text-default' }, h('span', { text: t('texts.default') + ': ' }), h('span', { class: 'default-val', text: it.defaultValue }))),
        actions: [
          { label: t('texts.reset'), icon: 'refresh', onClick: async () => { const ok = await put(''); return ok; } },
          { label: t('common.cancel') },
          { label: t('common.save'), kind: 'primary', primary: true, icon: 'check', onClick: () => {
            const v = ta.value;
            if (v === shown) return true; // nothing changed
            if (v === it.defaultValue || (chat && v === withLook(it.defaultValue, lookFor()))) return put(''); // back to the default
            return put(v);
          } },
        ],
      });
      setTimeout(() => ta.focus(), 50);
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
    { key: 'youtube', icon: 'music' },
    { key: 'updates', icon: 'download' },
    { key: 'system', icon: 'info' },
  ];
  Pages.settings = function (root) {
    const secKey = () => {
      let k = location.pathname.replace(/\/+$/, '').split('/')[2] || 'general';
      if (k === 'maintenance') k = 'system';
      return SETTINGS_SECTIONS.some((x) => x.key === k) ? k : 'general';
    };
    // one page, categories stacked as collapsible sections (open state remembered)
    const body = h('div', { class: 'settings-body set-acc' }, F.skeleton(2, 'sk-card'));
    root.append(F.pageHead(t('settings.title'), t('settings.sub')), body);
    let s = null;
    async function load() {
      try { s = await F.api('GET', 'settings'); render(); }
      catch (e) { F.fail(e); body.replaceChildren(F.empty('alert', t('err.loadFailed'))); }
    }
    function render() {
      if (!s) return;
      const wanted = location.pathname.split('/')[2] ? secKey() : null;
      const build = {
        general: () => [langCard(), panelCard()],
        youtube: () => [ytdlpCard(), cookieCard()],
        updates: () => [updateCard()],
        system: () => [sysCard(), supportCard(), restartCard()],
      };
      body.replaceChildren(...SETTINGS_SECTIONS.map((sec) => {
        const lsKey = 'frix.set.' + sec.key;
        const stored = lsGet(lsKey);
        const open = wanted ? wanted === sec.key : stored != null ? stored === '1' : sec.key === 'general';
        const id = F.uid('sa');
        const content = h('div', { class: 'acc-body', id, hidden: !open }, build[sec.key]());
        const head = h('button', { type: 'button', class: 'acc-head', 'aria-expanded': open ? 'true' : 'false', 'aria-controls': id },
          h('span', { class: 'acc-ic' }, icon(sec.icon)),
          h('span', { class: 'acc-text' }, h('strong', { text: t('settings.sec.' + sec.key) }), h('span', { class: 'muted small', text: t('settings.secDesc.' + sec.key) })),
          h('span', { class: 'acc-chev' }, icon('chevron')));
        const el = h('section', { class: 'acc' + (open ? ' open' : ''), id: 'set-' + sec.key }, head, content);
        head.addEventListener('click', () => {
          const o = content.hidden;
          content.hidden = !o; el.classList.toggle('open', o); head.setAttribute('aria-expanded', o ? 'true' : 'false');
          lsSet(lsKey, o ? '1' : '0');
        });
        return el;
      }));
      if (wanted) setTimeout(() => { const el = document.getElementById('set-' + wanted); if (el) el.scrollIntoView({ block: 'start' }); });
    }
    // settings row: title + explanation on the left, controls on the right
    const card = (ic, title, desc, ...kids) => h('section', { class: 'card set-card' },
      h('div', { class: 'set-head' }, h('h3', { class: 'card-title' }, icon(ic), title), desc ? h('p', { class: 'muted small card-desc' }, desc) : null),
      h('div', { class: 'set-body' }, kids));

    function panelCard() {
      const theme = F.select([['lacivert', t('theme.lacivert')], ['krem', t('theme.krem')]], F.theme(), { class: 'input theme-sel' });
      theme.addEventListener('change', () => F.setTheme(theme.value));
      return card('sliders', t('settings.panelCard'), t('settings.panelHelp'),
        h('div', { class: 'set-grid' }, F.field(t('theme.title'), theme)));
    }
    function langCard() {
      const sel = F.langPicker({ langs: s.languages && s.languages.length ? s.languages : ['tr', 'en'], value: s.lang, label: t('settings.lang'), align: 'left' });
      const save = F.btn(t('common.save'), { kind: 'primary', icon: 'check' });
      save.addEventListener('click', () => F.busy(save, async () => {
        const v = sel.value();
        if (v === s.lang) return;
        if (!(await F.confirm(t('settings.langConfirm'), { ok: t('common.yes') }))) return;
        try {
          await F.api('PUT', 'settings', { lang: v }); s.lang = v;
          F.setLang(v); bumpAvatar(); F.toast(t('settings.langSaved'));
        } catch (e) { F.fail(e); }
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
    function supportCard() {
      return card('coffee', t('support.title'), t('support.text'),
        h('div', null, h('a', { href: F.SUPPORT_URL, target: '_blank', rel: 'noopener noreferrer', class: 'btn btn-support' }, icon('coffee'), h('span', { text: 'Buy me a coffee' }))));
    }
    function cmdCard() {
      return card('terminal', t('nav.commands'), t('settings.cmdHelp'),
        h('a', { href: '/plugins', 'data-link': '', class: 'small link-arrow' }, t('settings.cmdLink'), ' →'));
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
              try { await F.api('POST', 'update/apply'); updateProgress(r.latest); } catch (er) { F.fail(er); }
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
    /** modal with stages + progress bar while the update downloads/extracts; hands over to waitForRestart */
    function updateProgress(version) {
      const STAGES = ['checking', 'downloading', 'extracting', 'restarting'];
      const bar = h('div', { class: 'upd-bar-fill' });
      const barBox = h('div', { class: 'upd-bar', role: 'progressbar', 'aria-valuemin': '0', 'aria-valuemax': '100', 'aria-valuenow': '0' }, bar);
      const detail = h('div', { class: 'muted small upd-detail' });
      const steps = h('ol', { class: 'upd-steps' }, STAGES.map((st) => h('li', { dataset: { st } }, h('span', { class: 'upd-dot' }), h('span', { text: t('update.stage.' + st) }))));
      const mb = (n) => (n / 1048576).toFixed(1);
      let done = false, lastStage = null;
      const box = h('div', { class: 'restart-overlay', role: 'alertdialog', 'aria-live': 'polite' },
        h('div', { class: 'restart-card upd-card' }, h('h2', { text: t('update.title', { v: version || '' }) }), steps, barBox, detail));
      document.body.appendChild(box);
      const paint = (p) => {
        const idx = STAGES.indexOf(p.stage);
        steps.querySelectorAll('li').forEach((li, i) => { li.className = i < idx ? 'done' : i === idx ? 'on' : ''; });
        const pct = p.stage === 'restarting' ? 100 : (p.percent || 0);
        // new stage starts from its own value without animating backwards
        if (p.stage !== lastStage) { bar.style.transition = 'none'; bar.style.width = pct + '%'; void bar.offsetWidth; bar.style.transition = ''; lastStage = p.stage; }
        bar.style.width = pct + '%';
        barBox.setAttribute('aria-valuenow', String(pct));
        barBox.classList.toggle('indeterminate', p.stage === 'checking' || (p.stage === 'downloading' && !p.total));
        detail.textContent = p.stage === 'downloading'
          ? (p.total ? t('update.mb', { a: mb(p.received), b: mb(p.total), p: pct }) : t('update.mbNoTotal', { a: mb(p.received) }))
          : p.stage === 'extracting' ? t('update.files', { a: p.received, b: p.total, p: pct })
          : p.stage === 'restarting' ? t('update.restartingHint') : '';
      };
      const poll = async () => {
        if (done) return;
        let p;
        try { p = await F.api('GET', 'update/progress'); } catch (_) { p = null; }
        if (!p) { done = true; box.remove(); F.waitForRestart(t('settings.updating')); return; } // server already restarting
        paint(p);
        if (p.stage === 'failed') {
          done = true;
          detail.textContent = t('update.failed') + (p.error ? ' — ' + F.errMsg({ code: p.error, message: p.error }) : '');
          box.querySelector('.upd-card').append(h('div', { class: 'look-foot' }, F.btn(t('common.close'), { kind: 'ghost', onClick: () => box.remove() })));
          return;
        }
        if (p.stage === 'restarting') { setTimeout(() => { done = true; box.remove(); F.waitForRestart(t('settings.updating')); }, 1200); return; }
        setTimeout(poll, 400);
      };
      paint({ stage: 'checking' });
      poll();
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
    load();
    return { onSubRoute: render };
  };

  // ================================================================== LOGS
  Pages.logs = function (root) {
    const pane = h('div', { role: 'tabpanel' });
    let cur = null;
    const kill = () => { if (cur && cur.destroy) { try { cur.destroy(); } catch (_) { /* */ } } cur = null; };
    let k = location.pathname.replace(/\/+$/, '').split('/')[2] || new URLSearchParams(location.search).get('tab') || 'live';
    if (!['live', 'history', 'audit'].includes(k)) k = 'live';
    const titles = { live: t('logs.tabLive'), history: t('logs.tabHistory'), audit: t('settings.sec.audit') };
    root.append(F.pageHead(titles[k], k === 'audit' ? t('settings.secDesc.audit') : t('logs.sub')), pane);
    cur = ({ live: liveLogs, history: historyLogs, audit: auditLog })[k](pane) || {};
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
      if (!(await F.confirm(t('logs.clearConfirm'), { danger: true, ok: t('logs.clear') }))) return;
      try { const r = await F.api('POST', 'logs/clear'); box.replaceChildren(); n = 0; count.textContent = ''; F.toast(r && r.files ? t('logs.clearedFiles', { n: r.files }) : t('logs.cleared')); } catch (e) { F.fail(e); }
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
      try { const r = await F.api('POST', 'logs/clear'); F.toast(r && r.files ? t('logs.clearedFiles', { n: r.files }) : t('logs.cleared')); page = 1; load(); } catch (e) { F.fail(e); }
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
    data.forEach((sec) => {
      (sec.items || []).forEach((it) => {
        const d = h('button', { type: 'button', class: 'faq-q-item', id: 'faq-' + it.id },
          h('span', { class: 'faq-sec-tag', text: sec.section }), h('span', { class: 'faq-q', text: it.q }), icon('chevron', 'faq-chev'));
        d.addEventListener('click', () => F.modal({ title: it.q, size: 'wide', body: h('div', { class: 'faq-body' }, (it.a || []).map(block)), actions: [{ label: t('common.close') }] }));
        d.dataset.search = (sec.section + ' ' + it.q + ' ' + JSON.stringify(it.a)).toLowerCase();
        items.push(d);
        list.append(d);
      });
    });
    const none = F.empty('help', t('faq.none'));
    none.hidden = true;
    none.classList.add('fit-skip');
    list.append(none);
    setTimeout(() => F.fitPage(list));
    if (location.hash.startsWith('#faq-')) { const tg = document.getElementById(location.hash.slice(1)); if (tg) setTimeout(() => tg.click(), 60); }
    q.addEventListener('input', () => {
      const v = q.value.trim().toLowerCase();
      let shown = 0;
      items.forEach((d) => { const ok = !v || d.dataset.search.includes(v); d.hidden = !ok; if (ok) { shown++; if (v) d.open = true; } });
      list.querySelectorAll('.faq-section').forEach((g) => { g.hidden = ![...g.querySelectorAll('.faq-item')].some((d) => !d.hidden); });
      none.hidden = shown > 0;
    });
  };
})();
