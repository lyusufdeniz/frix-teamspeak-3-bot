/* Frix panel – pages: bots (dashboard), player, radios */
'use strict';
(() => {
  const F = window.F;
  const Pages = window.Pages;
  const h = F.h, t = F.t, icon = F.icon, enc = F.enc;

  const pluginName = (id) => (F.has('plugin.' + id) || window.I18N.tr['plugin.' + id] ? t('plugin.' + id) : id);
  const statusLabel = (s) => t('status.' + s);

  function progressBlock() {
    const fill = h('div', { class: 'prog-fill' });
    const bar = h('div', { class: 'prog' }, fill);
    const cur = h('span', { class: 'time' });
    const len = h('span', { class: 'time' });
    const live = h('span', { class: 'live-tag', text: t('player.live') });
    const el = h('div', { class: 'prog-wrap' }, cur, bar, len, live);
    function set(bot) {
      const p = bot && bot.live && bot.live.playing;
      if (!p) { el.hidden = true; return; }
      el.hidden = false;
      const pos = F.livePos(bot);
      // LIVE only when the server says so; length 0 without live = length not known yet (first second of a song)
      const isLive = p.live === true, hasLen = p.length > 0;
      live.hidden = !isLive; len.hidden = isLive; bar.classList.toggle('live', isLive);
      cur.textContent = F.fmtTime(pos);
      len.textContent = hasLen ? F.fmtTime(p.length) : '--:--';
      fill.style.width = isLive ? '100%' : hasLen ? (100 * pos / p.length).toFixed(2) + '%' : '0%';
    }
    return { el, set };
  }

  // ================================================================== BOTS / DASHBOARD
  Pages.bots = function (root) {
    const admin = F.isAdmin();
    const grid = h('div', { class: 'bot-grid' }, F.skeleton(2, 'sk-card'));
    root.append(
      F.pageHead(t('bots.title'), t('bots.sub'), admin ? F.btn(t('bots.new'), { kind: 'primary', icon: 'plus', onClick: () => botForm(null) }) : null),
      grid);
    F.fitPage(grid);
    const cards = new Map();
    let loaded = false;

    function buildCard(name) {
      const dot = h('span', { class: 'dot' });
      const stTxt = h('span', { class: 'status-text' });
      const selBadge = h('span', { class: 'sel-tag', text: t('bots.selected') });
      const nick = h('h2', { class: 'bot-nick' });
      const kv = h('dl', { class: 'kv kv-tight' });
      const npCover = h('div', { class: 'np-cover' });
      const npTitle = h('div', { class: 'np-title' });
      const npSub = h('div', { class: 'np-sub' });
      const prog = progressBlock();
      const np = h('div', { class: 'np' }, npCover, h('div', { class: 'np-info' }, npTitle, npSub, prog.el));
      const plugins = h('div', { class: 'bot-plugins' });
      const actions = h('div', { class: 'card-actions' });
      const el = h('article', { class: 'card bot-card' },
        h('div', { class: 'bot-top' }, h('div', { class: 'status' }, dot, stTxt), h('div', { class: 'row gap-s' }, selBadge, h('span', { class: 'mono muted small', text: '#' + name }))),
        nick, kv, np, plugins, actions);
      let last = null;
      let actSig = '';
      function update(b) {
        last = b;
        el.dataset.status = b.status;
        dot.className = 'dot dot-' + b.status;
        stTxt.textContent = statusLabel(b.status);
        selBadge.hidden = F.state.selected !== b.name;
        el.classList.toggle('is-selected', F.state.selected === b.name);
        nick.textContent = b.nickname || b.name;
        const lv = b.live;
        const row = (k, v, extra) => [h('dt', { text: k }), h('dd', null, h('span', { text: v }), extra || null)];
        kv.replaceChildren(
          row(t('bots.k.server'), b.address || '–', b.hasPassword ? h('span', { class: 'kv-ic', title: t('bots.hasPassword') }, icon('lock', 'sm')) : null),
          row(t('bots.k.channel'), b.channel ? b.channel : t('bots.defaultChannel')),
          row(t('bots.k.quality'), b.bitrate + ' kbps' + (b.run ? ' · ' + t('bots.autostart') : '')),
          lv ? row(t('bots.k.queue'), String(lv.queueCount) + ' · ' + t('bots.k.volume') + ' ' + Math.round(lv.volume) + '%') : null);
        const p = lv && lv.playing;
        const key = p ? p.thumbnail || (p.radio ? 'radio' : 'music') : lv ? 'idle' : 'off';
        if (npCover.dataset.key !== key) {
          npCover.dataset.key = key;
          npCover.replaceChildren(p && p.thumbnail ? h('img', { src: p.thumbnail, alt: '', loading: 'lazy' }) : icon(p && p.radio ? 'radio' : lv ? 'music' : 'power'));
        }
        np.classList.toggle('np-idle', !p);
        if (lv && lv.loading) { npTitle.textContent = t('track.stream') + '…'; npSub.textContent = lv.loading.title || ''; }
        else if (p) { npTitle.textContent = p.title; npSub.textContent = (p.paused ? t('player.paused') + ' · ' : '') + (p.requestedBy ? t('player.requestedBy', { user: p.requestedBy }) : ''); }
        else if (lv) { npTitle.textContent = t('bots.idle'); npSub.textContent = ''; }
        else { npTitle.textContent = t('bots.offlineHint'); npSub.textContent = ''; }
        npTitle.title = npTitle.textContent;
        prog.set(b);
        plugins.replaceChildren((b.plugins || []).length
          ? [h('span', { class: 'muted small', text: t('bots.k.plugins') + ' ' }), ...(b.plugins || []).map((id) => h('span', { class: 'tag', text: pluginName(id) }))]
          : h('span', { class: 'muted small', text: t('bots.k.plugins') + ' –' }));
        const sig = b.status + (b.live ? 1 : 0) + F.state.lang;
        if (sig !== actSig) { actSig = sig; buildActions(b); }
      }
      function buildActions(b) {
        const running = !!b.live;
        // labelled: player + settings; icon-only (with tooltip): start/stop, restart, and delete pushed to the far right
        const list = [
          F.btn(t('bots.openPlayer'), { kind: 'ghost', icon: 'play', onClick: () => { F.selectBot(b.name); F.go('/player'); } }),
        ];
        if (admin) {
          list.push(
            F.btn(t('bots.openSettings'), { kind: 'ghost', icon: 'sliders', onClick: () => botForm(last) }),
            h('div', { class: 'ca-icons' },
              F.iconBtn('power', running ? t('bots.stop') : t('bots.start'), (e) => power(e.currentTarget, b.name, running ? 'stop' : 'start'), running ? 'is-on' : ''),
              running ? F.iconBtn('refresh', t('bots.restart'), (e) => power(e.currentTarget, b.name, 'restart')) : null,
              h('span', { class: 'ca-sep', 'aria-hidden': 'true' }),
              F.iconBtn('trash', t('common.delete'), () => del(last), 'danger')));
        }
        actions.replaceChildren(...list.filter(Boolean));
      }
      return { el, update, tick: () => { const b = F.bot(name); if (b) prog.set(b); } };
    }

    async function power(btn, name, action) {
      await F.busy(btn, async () => {
        try {
          await F.api('POST', 'bots/' + enc(name) + '/' + action);
          F.toast(t('bots.' + action + 'ed', { name }));
          await F.refreshBots();
        } catch (e) { F.fail(e); }
      });
    }
    async function del(b) {
      if (!(await F.confirm(t('bots.deleteConfirm', { name: b.name }), { danger: true, ok: t('common.delete') }))) return;
      try {
        await F.api('DELETE', 'bots/' + enc(b.name));
        F.toast(t('bots.deleted', { name: b.name }));
        await F.refreshBots();
      } catch (e) { F.fail(e); }
    }

    function onBots(bots) {
      if (!loaded) { grid.replaceChildren(); loaded = true; }
      const names = new Set(bots.map((b) => b.name));
      for (const [n, c] of cards) if (!names.has(n)) { c.el.remove(); cards.delete(n); }
      bots.forEach((b, i) => {
        let c = cards.get(b.name);
        if (!c) { c = buildCard(b.name); cards.set(b.name, c); }
        if (grid.children[i] !== c.el) grid.insertBefore(c.el, grid.children[i] || null);
        c.update(b);
      });
      let empty = grid.querySelector('.empty');
      if (!bots.length && !empty) grid.append(F.empty('server', t('bots.none'), admin ? t('bots.noneAdmin') : null));
      else if (bots.length && empty) empty.remove();
    }
    return { onBots, onTick: () => { for (const c of cards.values()) c.tick(); } };
  };

  /** create (bot=null) or edit bot settings */
  function botForm(bot) {
    const isNew = !bot;
    const first = F.state.bots[0];
    const name = F.input({ maxlength: 24, placeholder: 'muzik2', pattern: '[A-Za-z0-9_-]{2,24}' });
    const address = F.input({ value: bot ? bot.address : first ? first.address : 'localhost:9987', placeholder: 'ts.example.com:9987' });
    const nickname = F.input({ value: bot ? bot.nickname : '', maxlength: 30, placeholder: 'Frix' });
    const channel = F.input({ value: bot ? bot.channel : '', placeholder: t('bots.f.channelPh') });
    const pw = F.pwInput({ autocomplete: 'new-password', placeholder: bot && bot.hasPassword ? t('bots.f.pwKeep') : t('bots.f.pwNone') });
    const clearPw = F.switchEl(false, t('bots.f.pwClear'));
    const commander = F.switchEl(bot ? bot.commander : true, t('bots.f.commander'));
    const run = F.switchEl(bot ? bot.run : true, t('bots.f.run'));
    const brVals = [32, 48, 64, 96];
    const cur = bot ? bot.bitrate : 64;
    if (!brVals.includes(cur)) brVals.push(cur);
    const bitrate = F.select(brVals.sort((a, b) => a - b).map((v) => [v, v + ' kbps' + (v === 64 ? ' · ' + t('bots.f.recommended') : '')]), cur);
    const start = F.switchEl(true, t('bots.f.startNow'));
    const moveNow = F.switchEl(false, t('bots.f.moveNow'));
    const moveField = h('div', { hidden: true }, moveNow);
    if (bot) channel.addEventListener('input', () => { moveField.hidden = channel.value.trim() === (bot.channel || '') || !bot.live; });

    const body = h('div', { class: 'form-grid' },
      isNew ? F.field(t('bots.f.name'), name, t('bots.f.nameHelp')) : null,
      F.field(t('bots.f.nickname'), nickname, t('bots.f.nickHelp')),
      F.field(t('bots.f.address'), address, t('bots.f.addressHelp'), { cls: isNew ? '' : 'span2' }),
      F.field(t('bots.f.channel'), channel, t('bots.f.channelHelp')),
      F.field(t('bots.f.password'), pw, null),
      F.field(t('bots.f.bitrate'), bitrate, t('bots.f.bitrateHelp')),
      h('div', { class: 'span2 switch-list' },
        commander, h('div', { class: 'help', text: t('bots.f.commanderHelp') }), run,
        bot && bot.hasPassword ? clearPw : null,
        isNew ? start : null, moveField),
      // bots have no "type": what a bot does is chosen per bot on the Plugins page
      isNew ? h('p', { class: 'span2 help', text: t('bots.f.pluginsHint') }) : null);
    F.modal({
      title: isNew ? t('bots.new') : t('bots.editTitle', { name: bot.name }), size: 'lg', body,
      actions: [
        { label: t('common.cancel') },
        {
          label: isNew ? t('common.create') : t('common.save'), kind: 'primary', primary: true, icon: 'check',
          onClick: async () => {
            const nick = nickname.value.trim();
            if (nick && (nick.length < 3 || nick.length > 30)) { F.toast(t('err.bad_nickname'), 'err'); return false; }
            if (!address.value.trim()) { F.toast(t('bots.f.addressReq'), 'err'); return false; }
            const payload = { address: address.value.trim(), channel: channel.value.trim(), commander: commander.input.checked, run: run.input.checked, bitrate: Number(bitrate.value) };
            if (nick) payload.nickname = nick;
            if (pw.input.value) payload.serverPassword = pw.input.value;
            else if (bot && bot.hasPassword && clearPw.input.checked) payload.serverPassword = '';
            try {
              if (isNew) {
                const n = name.value.trim();
                if (!/^[A-Za-z0-9_-]{2,24}$/.test(n)) { F.toast(t('err.bad_bot_name'), 'err'); name.focus(); return false; }
                payload.name = n; payload.start = start.input.checked;
                await F.api('POST', 'bots', payload);
                F.toast(t('bots.created', { name: n }));
                F.selectBot(n);
              } else {
                if (!moveField.hidden && moveNow.input.checked) payload.moveNow = true;
                const r = await F.api('PUT', 'bots/' + enc(bot.name), payload);
                F.toast(r && r.restarted ? t('bots.savedRestarted') : t('common.saved'));
              }
              await F.refreshBots();
            } catch (e) { F.fail(e); return false; }
          },
        },
      ],
    });
  }

  // ================================================================== PLAYER
  Pages.player = function (root) {
    const can = F.canControl();
    const name = F.state.selected;
    const destroyers = [];
    root.append(F.pageHead(t('player.title'), null, null));
    const body = h('div');
    root.append(body);
    let built = false, ui = null;

    function build(b) {
      built = true;
      body.replaceChildren();
      // --- now playing block (horizontal)
      const cover = h('div', { class: 'cover' });
      const title = h('h2', { class: 'np-big-title' });
      const sub = h('div', { class: 'np-big-sub' });
      const botLine = h('div', { class: 'np-bot' });
      const seek = h('input', { type: 'range', class: 'seek', min: 0, max: 100, step: 1, value: 0, 'aria-label': t('player.seek'), disabled: !can });
      const curT = h('span', { class: 'time' }), lenT = h('span', { class: 'time' });
      const liveBadge = h('span', { class: 'live-tag', text: t('player.live') });
      const liveLine = h('div', { class: 'live-line' });
      const seekRow = h('div', { class: 'seek-row' }, curT, seek, liveLine, lenT, liveBadge);
      let seeking = 0;
      seek.addEventListener('input', () => { seeking = Date.now(); curT.textContent = F.fmtTime(Number(seek.value)); paintRange(seek); });
      seek.addEventListener('change', async () => { seeking = Date.now(); await F.control(name, 'seek', Number(seek.value)); seeking = Date.now() - 1000; });

      const prevB = F.iconBtn('prev', t('player.prev'), () => F.control(name, 'previous'), 'ctl');
      const playB = F.iconBtn('play', t('player.play'), () => F.control(name, 'pause'), 'ctl ctl-main');
      const nextB = F.iconBtn('next', t('player.next'), () => F.control(name, 'next'), 'ctl');
      const stopB = F.iconBtn('stop', t('player.stop'), () => F.control(name, 'stop'), 'ctl');
      const repB = F.iconBtn('repeat', t('player.repeat'), () => {
        const lv = (F.bot(name) || {}).live; if (!lv) return;
        const nx = { off: 'one', one: 'all', all: 'off' }[lv.repeat] || 'off';
        F.control(name, 'repeat', nx).then((ok) => ok && F.toast(t('player.repeat.' + nx), 'info'));
      }, 'ctl');
      const repTag = h('span', { class: 'rep-tag' });
      repB.append(repTag);
      const shufB = F.iconBtn('shuffle', t('player.shuffle'), () => {
        const lv = (F.bot(name) || {}).live; if (!lv) return;
        F.control(name, 'random', lv.random ? 'false' : 'true').then((ok) => ok && F.toast(lv.random ? t('player.shuffleOff') : t('player.shuffleOn'), 'info'));
      }, 'ctl');

      const vol = h('input', { type: 'range', class: 'vol', min: 0, max: 100, step: 1, value: 0, 'aria-label': t('player.volume'), disabled: !can });
      const volVal = h('span', { class: 'vol-val' });
      let volT = null, volTouched = 0;
      vol.addEventListener('input', () => {
        volTouched = Date.now();
        volVal.textContent = vol.value + '%';
        paintRange(vol);
        clearTimeout(volT);
        volT = setTimeout(() => { volTouched = Date.now(); F.control(name, 'volume', Number(vol.value)); }, 300);
      });
      const volRow = h('div', { class: 'vol-row' }, h('span', { class: 'vol-ic' }, icon('volume')), vol, volVal);
      const ctrls = h('div', { class: 'controls' }, can ? [shufB, prevB, playB, nextB, stopB, repB] : null, volRow);

      const nowCard = h('section', { class: 'card now-card', 'aria-label': t('player.now') },
        cover,
        h('div', { class: 'now-main' }, botLine, title, sub, seekRow, ctrls,
          can ? null : h('p', { class: 'muted small', text: t('player.viewerNote') })));

      // --- add + queue
      let addCard = null;
      if (can) {
        const adder = F.buildAdder(name);
        destroyers.push(adder.destroy);
        addCard = h('section', { class: 'card add-card', 'aria-label': t('player.addTitle') },
          h('h2', { class: 'card-title', text: t('player.addTitle') }),
          h('p', { class: 'muted small', text: t('player.addHelp') }), adder.el);
      }
      const qList = h('ol', { class: 'queue' });
      const qTitle = h('h2', { class: 'card-title' });
      const clearB = can ? F.btn(t('player.clearQueue'), { kind: 'ghost', icon: 'trash', onClick: async () => {
        if (!(await F.confirm(t('player.clearConfirm'), { danger: true, ok: t('player.clearQueue') }))) return;
        if (await F.control(name, 'clear')) { F.toast(t('player.cleared')); loadQueue(); }
      } }) : null;
      const saveListB = can ? F.btn(t('playlists.save'), { kind: 'ghost', icon: 'save', onClick: () => F.savePlaylist(name) }) : null;
      const queueCard = h('section', { class: 'card queue-card', 'aria-label': t('player.queue') },
        h('div', { class: 'card-head' }, qTitle, can ? h('div', { class: 'row gap-s wrap' }, saveListB, clearB) : null), qList);

      body.append(nowCard, h('div', { class: 'player-grid' + (can ? '' : ' single') }, queueCard, addCard));
      F.fitPage(qList);

      ui = { cover, title, sub, botLine, seek, curT, lenT, liveBadge, liveLine, seekRow, playB, prevB, nextB, stopB, repB, repTag, shufB, vol, volVal, qList, qTitle, clearB, saveListB,
        seeking: () => Date.now() - seeking < 1500, volBusy: () => Date.now() - volTouched < 2500 };
      loadQueue();
      update(b);
    }

    function paintRange(r) {
      const max = Number(r.max) || 1;
      r.style.setProperty('--pct', (100 * Number(r.value) / max).toFixed(2) + '%');
    }

    // --- queue
    let qSig = '', qBusy = false;
    async function loadQueue() {
      if (qBusy) return; qBusy = true;
      try {
        const q = await F.api('GET', 'bots/' + enc(name) + '/queue');
        if (ui) renderQueue(q);
      } catch (e) { F.fail(e); }
      finally { qBusy = false; }
    }
    function renderQueue(q) {
      const items = (q && q.items) || [];
      ui.qTitle.textContent = t('player.queue') + ' · ' + items.length;
      if (ui.clearB) ui.clearB.disabled = !items.length;
      if (ui.saveListB) ui.saveListB.disabled = !items.length;
      if (!items.length) { ui.qList.replaceChildren(h('li', { class: 'queue-empty' }, F.empty('list', t('player.queueEmpty'), can ? t('player.queueEmptyHint') : null))); return; }
      const b = F.bot(name);
      const playingNow = !!(b && b.live && b.live.playing);
      ui.qList.replaceChildren(...items.map((it, i) => {
        const cur = it.index === q.index;
        it.title = F.cleanTitle(it.title);
        const title = it.link ? h('a', { href: it.link, target: '_blank', rel: 'noopener noreferrer', text: it.title, title: it.title }) : h('span', { text: it.title, title: it.title });
        return h('li', { class: 'q-item' + (cur ? ' current' : ''), 'aria-current': cur ? 'true' : null },
          h('span', { class: 'q-num', text: String(i + 1).padStart(2, '0') }),
          h('div', { class: 'q-main' }, h('div', { class: 'q-title' }, title), h('div', { class: 'q-sub' }, (it.type === 'youtube' ? 'youtube' : it.type) + (it.requestedBy ? ' · ' + it.requestedBy : ''))),
          can ? h('div', { class: 'q-actions' },
            F.iconBtn('play', t('player.jump'), () => F.control(name, 'jump', it.index).then(loadQueue), 'sm'),
            F.iconBtn('up', t('player.moveUp'), () => move(it.index, it.index - 1), 'sm' + (i === 0 ? ' invisible' : '')),
            F.iconBtn('down', t('player.moveDown'), () => move(it.index, it.index + 1), 'sm' + (i === items.length - 1 ? ' invisible' : '')),
            F.iconBtn('trash', t('player.remove'), () => F.control(name, 'remove', it.index).then((ok) => ok && loadQueue()), 'sm danger' + (cur && playingNow ? ' invisible' : ''))) : null);
      }));
    }
    async function move(from, to) {
      if (await F.control(name, 'move', undefined, { from, to })) loadQueue();
    }

    function update(b) {
      const lv = b.live, p = lv && lv.playing;
      const key = p ? p.thumbnail || (p.radio ? 'radio' : 'music') : 'none';
      if (ui.cover.dataset.key !== key) {
        ui.cover.dataset.key = key;
        if (p && p.thumbnail) ui.cover.replaceChildren(h('img', { src: p.thumbnail.replace('/mqdefault.jpg', '/hqdefault.jpg'), alt: '' }));
        else if (p && p.radio) ui.cover.replaceChildren(h('div', { class: 'cover-ph' }, icon('radio')));
        else ui.cover.replaceChildren(h('div', { class: 'cover-ph' }, h('img', { src: '/logo.svg', alt: '' })));
      }
      ui.botLine.replaceChildren(h('span', { class: 'dot dot-' + b.status }), h('span', { text: F.botLabel(b) + ' · ' + statusLabel(b.status) }));
      if (p) {
        if (p.link) ui.title.replaceChildren(h('a', { href: p.link, target: '_blank', rel: 'noopener noreferrer', text: p.title }));
        else ui.title.textContent = p.title;
        const parts = [];
        if (p.radio) parts.push(t('player.radio', { name: p.radio }));
        if (p.requestedBy) parts.push(t('player.requestedBy', { user: p.requestedBy }));
        if (p.paused) parts.push(t('player.paused'));
        ui.sub.textContent = parts.join(' · ');
      } else {
        ui.title.textContent = lv && lv.loading ? t('track.stream') + '…' : t('player.nothing');
        ui.sub.textContent = lv && lv.loading ? lv.loading.title || '' : can ? t('player.nothingHint') : '';
      }
      ui.seekRow.hidden = !p;
      tick();
      const ctl = can && !!lv;
      ui.playB.disabled = !ctl || !p; ui.stopB.disabled = !ctl || !p;
      ui.prevB.disabled = !ctl; ui.nextB.disabled = !ctl || !lv.queueCount;
      ui.repB.disabled = !ctl; ui.shufB.disabled = !ctl;
      const paused = !p || p.paused;
      if (ui.playB.dataset.st !== String(paused)) {
        ui.playB.dataset.st = String(paused);
        ui.playB.replaceChildren(icon(paused ? 'play' : 'pause'));
        ui.playB.setAttribute('aria-label', paused ? t('player.play') : t('player.pause'));
        ui.playB.title = ui.playB.getAttribute('aria-label');
      }
      if (lv) {
        ui.repB.classList.toggle('on', lv.repeat !== 'off');
        ui.repTag.textContent = lv.repeat === 'one' ? '1' : '';
        ui.repB.setAttribute('aria-label', t('player.repeat') + ': ' + t('player.repeat.' + lv.repeat));
        ui.repB.title = ui.repB.getAttribute('aria-label');
        ui.shufB.classList.toggle('on', !!lv.random);
        ui.shufB.setAttribute('aria-pressed', lv.random ? 'true' : 'false');
        if (!ui.volBusy()) { ui.vol.value = Math.round(lv.volume); ui.volVal.textContent = Math.round(lv.volume) + '%'; paintRange(ui.vol); }
        ui.vol.disabled = !can;
      } else { ui.vol.disabled = true; ui.volVal.textContent = '–'; }
      const sig = lv ? [lv.queueCount, lv.queueIndex, p ? p.title : '', !!p].join('|') : 'off';
      if (sig !== qSig) { const first = qSig === ''; qSig = sig; if (!first) loadQueue(); }
    }
    function tick() {
      if (!ui) return;
      const b = F.bot(name); const p = b && b.live && b.live.playing;
      if (!p) return;
      // LIVE (no seeking) only when live === true; length 0 without live = not known yet: idle bar, --:--
      const isLive = p.live === true, hasLen = p.length > 0;
      ui.liveBadge.hidden = !isLive; ui.liveLine.hidden = !isLive; ui.seek.hidden = isLive; ui.lenT.hidden = isLive;
      ui.seek.disabled = !can || !hasLen;
      if (!ui.seeking()) {
        const pos = F.livePos(b);
        ui.curT.textContent = F.fmtTime(pos);
        if (hasLen) { ui.seek.max = Math.max(1, Math.floor(p.length)); ui.seek.value = Math.floor(pos); }
        else if (!isLive) { ui.seek.max = 1; ui.seek.value = 0; }
        if (!isLive) paintRange(ui.seek);
      }
      ui.lenT.textContent = hasLen ? F.fmtTime(p.length) : '--:--';
    }

    function onBots() {
      const b = F.bot(name);
      if (!b) {
        built = false; ui = null;
        body.replaceChildren(F.empty('server', F.state.bots.length ? t('player.pickBot') : t('bots.none'), F.isAdmin() && !F.state.bots.length ? t('bots.noneAdmin') : null));
        return;
      }
      if (!b.live) {
        if (built || !body.querySelector('.offline-note')) {
          built = false; ui = null; qSig = '';
          body.replaceChildren(h('div', { class: 'card offline-note' }, F.empty('power', t('player.offline', { name: F.botLabel(b) }), t('player.offlineText'),
            F.isAdmin() ? F.btn(t('bots.start'), { kind: 'primary', icon: 'power', onClick: (e) => F.busy(e.currentTarget, async () => {
              try { await F.api('POST', 'bots/' + enc(b.name) + '/start'); F.toast(t('bots.started', { name: b.name })); await F.refreshBots(); } catch (er) { F.fail(er); }
            }) }) : null)));
        }
        return;
      }
      if (!built) build(b); else update(b);
    }
    if (!F.state.botsLoaded) body.append(F.skeleton(1, 'sk-big'), h('div', { class: 'player-grid' }, F.skeleton(2, 'sk-card')));
    return { onBots, onTick: tick, destroy: () => destroyers.forEach((d) => d()) };
  };

  // ================================================================== RADIOS
  Pages.radios = function (root) {
    const admin = F.isAdmin(), can = F.canControl();
    const fileIn = h('input', { type: 'file', accept: '.m3u,.m3u8,audio/x-mpegurl,audio/mpegurl,text/plain', hidden: true });
    fileIn.addEventListener('change', async () => {
      const f = fileIn.files && fileIn.files[0];
      fileIn.value = '';
      if (!f) return;
      if (f.size > 2 * 1024 * 1024) { F.toast(t('radios.tooBig'), 'err'); return; }
      try {
        const text = await f.text();
        const r = await F.api('POST', 'radios/import', { text });
        F.toast(t('radios.imported', { added: r.added, skipped: r.skipped }), r.added ? 'ok' : 'info');
        load();
      } catch (e) { F.fail(e); }
    });
    const playOn = h('span', { class: 'muted small' });
    root.append(F.pageHead(t('radios.title'), t('radios.sub'), admin ? [
      F.btn(t('radios.import'), { kind: 'ghost', icon: 'upload', onClick: () => fileIn.click() }),
      F.btn(t('radios.add'), { kind: 'primary', icon: 'plus', onClick: () => radioForm(null) }), fileIn] : null),
    h('div', { class: 'callout' }, icon('info'), h('div', null, h('p', null, F.rich(t('radios.hint'))), can ? playOn : null)));
    const list = h('div', { class: 'radio-grid' }, F.skeleton(4, 'sk-row'));
    root.append(list);
    F.fitPage(list);
    let radios = [];

    async function load() {
      try { radios = (await F.api('GET', 'radios')) || []; render(); }
      catch (e) { F.fail(e); list.replaceChildren(F.empty('alert', t('err.loadFailed'))); }
    }
    function render() {
      if (!radios.length) { list.replaceChildren(F.empty('radio', t('radios.none'), admin ? t('radios.noneAdmin') : null)); return; }
      const b = F.selectedBot();
      const playingRadio = b && b.live && b.live.playing && b.live.playing.radio;
      list.replaceChildren(...radios.map((r) => h('article', { class: 'card radio-card' + (playingRadio === r.name ? ' is-playing' : '') },
        h('div', { class: 'radio-ic' }, playingRadio === r.name ? h('span', { class: 'eq' }, h('i'), h('i'), h('i')) : icon('radio')),
        h('div', { class: 'radio-main' },
          h('div', { class: 'radio-name' }, h('strong', { text: r.name }), r.genre ? F.badge(r.genre) : null),
          h('div', { class: 'radio-url muted small', text: r.url, title: r.url })),
        h('div', { class: 'radio-actions' },
          can ? F.btn(t('radios.play'), { kind: 'primary', icon: 'play', onClick: (e) => playRadio(e.currentTarget, r) }) : null,
          admin ? F.iconBtn('edit', t('common.edit'), () => radioForm(r)) : null,
          admin ? F.iconBtn('trash', t('common.delete'), () => delRadio(r), 'danger') : null))));
    }
    async function playRadio(btn, r) {
      const b = F.selectedBot();
      if (!b) { F.toast(t('player.pickBot'), 'err'); return; }
      await F.busy(btn, async () => {
        try {
          const res = await F.api('POST', 'bots/' + enc(b.name) + '/radio', { id: r.id });
          F.toast(t('radios.playing', { name: (res && res.name) || r.name, bot: F.botLabel(b) }));
          F.refreshBots();
        } catch (e) { F.fail(e); }
      });
    }
    async function delRadio(r) {
      if (!(await F.confirm(t('radios.deleteConfirm', { name: r.name }), { danger: true, ok: t('common.delete') }))) return;
      try { await F.api('DELETE', 'radios/' + r.id); F.toast(t('common.deleted')); load(); } catch (e) { F.fail(e); }
    }
    function radioForm(r) {
      const name = F.input({ value: r ? r.name : '', maxlength: 40, placeholder: 'Power Türk' });
      const url = F.input({ value: r ? r.url : '', type: 'url', placeholder: 'https://…/stream.mp3' });
      const genre = F.input({ value: r && r.genre ? r.genre : '', maxlength: 40, placeholder: 'Pop' });
      F.modal({
        title: r ? t('radios.edit') : t('radios.add'),
        body: h('div', null, F.field(t('radios.f.name'), name, t('radios.f.nameHelp')), F.field(t('radios.f.url'), url, t('radios.f.urlHelp')), F.field(t('radios.f.genre'), genre)),
        actions: [{ label: t('common.cancel') }, {
          label: t('common.save'), kind: 'primary', primary: true, icon: 'check', onClick: async () => {
            const body = { name: name.value.trim(), url: url.value.trim(), genre: genre.value.trim() || null };
            if (!body.name) { F.toast(t('radios.f.nameReq'), 'err'); name.focus(); return false; }
            if (!/^https?:\/\//i.test(body.url)) { F.toast(t('radios.f.urlReq'), 'err'); url.focus(); return false; }
            try {
              if (r) await F.api('PUT', 'radios/' + r.id, body); else await F.api('POST', 'radios', body);
              F.toast(t('common.saved')); load();
            } catch (e) { F.fail(e); return false; }
          },
        }],
      });
    }
    let lastPlay = null;
    function onBots() {
      const b = F.selectedBot();
      playOn.textContent = b ? t('radios.playOn', { bot: F.botLabel(b) }) : '';
      const pr = b && b.live && b.live.playing ? b.live.playing.radio || '' : '';
      if (pr !== lastPlay) { lastPlay = pr; if (radios.length) render(); }
    }
    load();
    return { onBots };
  };
  // ================================================================== shared: song thumbnail + bar rows
  const thumb = (src, ic) => h('span', { class: 'st-thumb' }, src ? h('img', { src, alt: '', loading: 'lazy', referrerpolicy: 'no-referrer' }) : icon(ic || 'music'));
  /** one ranked row with a flat count bar: [#] [lead] [title/sub + bar] [value] [actions] */
  function barRow(rank, lead, main, value, frac, actions) {
    return h('li', { class: 'st-row' },
      h('span', { class: 'st-rank', text: String(rank).padStart(2, '0') }),
      lead,
      h('div', { class: 'st-main' }, main,
        h('div', { class: 'st-bar', 'aria-hidden': 'true' }, h('span', { style: { width: Math.max(2, Math.round(frac * 100)) + '%' } }))),
      h('span', { class: 'st-val', text: value }),
      actions || null);
  }
  const fmtDur = (sec) => {
    const m = Math.floor((sec || 0) / 60), hh = Math.floor(m / 60);
    return hh ? t('stats.hm', { h: hh, m: m % 60 }) : t('stats.m', { m });
  };

  // ================================================================== STATS
  Pages.stats = function (root) {
    const can = F.canControl();
    let days = 30, bot = '';
    const periods = [[7, t('stats.d7')], [30, t('stats.d30')], [365, t('stats.d365')], [0, t('stats.all')]];
    const seg = h('div', { class: 'tabs seg', role: 'group', 'aria-label': t('stats.period') });
    const segBtns = periods.map(([d, label]) => {
      const b = h('button', { type: 'button', class: 'tab', 'aria-pressed': 'false', text: label });
      b.addEventListener('click', () => { if (days !== d) { days = d; paintSeg(); load(); } });
      seg.append(b);
      return b;
    });
    const paintSeg = () => segBtns.forEach((b, i) => { const on = periods[i][0] === days; b.classList.toggle('on', on); b.setAttribute('aria-pressed', on ? 'true' : 'false'); });
    paintSeg();
    const botSel = h('select', { class: 'input', 'aria-label': t('stats.bot') });
    botSel.addEventListener('change', () => { bot = botSel.value; load(); });
    const plays = h('strong', { class: 'kpi-num', text: '–' });
    const songsL = h('ol', { class: 'st-list' }), usersL = h('ol', { class: 'st-list' }), onlineL = h('ol', { class: 'st-list' });
    const songsCard = h('section', { class: 'card st-songs' }, h('h2', { class: 'card-title' }, icon('music'), t('stats.topSongs')), songsL);
    const usersCard = h('section', { class: 'card' }, h('h2', { class: 'card-title' }, icon('users'), t('stats.topUsers')), usersL);
    const onlineCard = h('section', { class: 'card' }, h('h2', { class: 'card-title' }, icon('chart'), t('stats.online')),
      h('p', { class: 'muted small' }, F.rich(t('stats.onlineNote'))), onlineL);
    root.append(F.pageHead(t('stats.title'), t('stats.sub'), h('label', { class: 'inline-field' }, h('span', { class: 'label-inline', text: t('stats.bot') }), botSel)),
      h('div', { class: 'toolbar st-toolbar' }, h('div', { class: 'field' }, h('span', { class: 'label', text: t('stats.period') }), seg),
        h('div', { class: 'kpi' }, plays, h('span', { class: 'label', text: t('stats.plays') }))),
      h('div', { class: 'stats-grid' }, songsCard, h('div', { class: 'st-side' }, usersCard, onlineCard)));
    [songsL, usersL, onlineL].forEach(F.fitPage);

    let seq = 0;
    async function load() {
      const my = ++seq;
      [songsL, usersL, onlineL].forEach((l) => l.replaceChildren(...F.skeleton(3, 'sk-row')));
      try {
        const r = (await F.api('GET', 'stats?days=' + days + (bot ? '&bot=' + enc(bot) : ''))) || {};
        if (my !== seq) return;
        render(r);
      } catch (e) {
        if (my !== seq) return;
        F.fail(e);
        [songsL, usersL, onlineL].forEach((l) => l.replaceChildren(F.empty('alert', t('err.loadFailed'))));
      }
    }
    function render(r) {
      plays.textContent = String(r.plays || 0);
      const songs = r.topSongs || [], users = r.topUsers || [], online = r.online || [];
      const max = (a, k) => Math.max(1, ...a.map((x) => x[k] || 0));
      const target = F.selectedBot();
      const ms = max(songs, 'count');
      songsL.replaceChildren(...(songs.length ? songs.map((s, i) => {
        const title = F.cleanTitle(s.title);
        const tEl = s.link ? h('a', { href: s.link, target: '_blank', rel: 'noopener noreferrer', class: 'st-title', text: title, title }) : h('span', { class: 'st-title', text: title, title });
        const playB = can && s.id ? F.btn(t('stats.play'), { kind: 'ghost', icon: 'play', cls: 'btn-sm', disabled: !target,
          title: target ? t('stats.playHint', { bot: F.botLabel(target) }) : t('player.pickBot'),
          onClick: () => { const b = F.selectedBot(); if (b) F.addSong(b.name, { id: s.id, title: s.title }, false); } }) : null;
        return barRow(i + 1, thumb(s.thumbnail, s.type === 'radio' ? 'radio' : 'music'), h('div', { class: 'st-line' }, tEl, h('span', { class: 'st-sub', text: s.type || '' })),
          t('stats.count', { n: s.count }), s.count / ms, playB);
      }) : [h('li', null, F.empty('music', t('stats.empty')))]));
      const mu = max(users, 'count');
      usersL.replaceChildren(...(users.length ? users.map((u, i) => barRow(i + 1, h('span', { class: 'avatar sm', text: (String(u.user || '?')[0] || '?').toLowerCase() }),
        h('div', { class: 'st-line' }, h('span', { class: 'st-title', text: u.user, title: u.user })), t('stats.userCount', { n: u.count }), u.count / mu))
        : [h('li', null, F.empty('users', t('stats.empty')))]));
      const mo = max(online, 'seconds');
      onlineL.replaceChildren(...(online.length ? online.map((o, i) => barRow(i + 1, h('span', { class: 'avatar sm', text: (String(o.name || '?')[0] || '?').toLowerCase() }),
        h('div', { class: 'st-line' }, h('span', { class: 'st-title', text: o.name, title: o.name }),
          o.lastSeen ? h('span', { class: 'st-sub', text: t('stats.lastSeen', { d: F.fmtDate(o.lastSeen) }) }) : null),
        fmtDur(o.seconds), o.seconds / mo))
        : [h('li', null, F.empty('chart', t('stats.onlineEmpty')))]));
    }
    let botsSig = null;
    function onBots(bots) {
      const s = bots.map((b) => b.name + '|' + b.nickname).join(',');
      if (s !== botsSig) {
        botsSig = s;
        botSel.replaceChildren(h('option', { value: '', text: t('stats.allBots') }), ...bots.map((b) => h('option', { value: b.name, text: F.botLabel(b) })));
        if (bot && !bots.some((b) => b.name === bot)) bot = '';
        botSel.value = bot;
      }
    }
    load();
    return { onBots };
  };

  // ================================================================== PLAYLISTS
  /** "save the queue of <bot> as a playlist" dialog (Playlists page + Player queue card) */
  F.savePlaylist = function (botName, onDone) {
    const b = F.bot(botName);
    if (!b) { F.toast(t('player.pickBot'), 'info'); return; }
    const name = F.input({ maxlength: 40, placeholder: 'rock', autocomplete: 'off' });
    F.modal({
      title: t('playlists.saveTitle'),
      body: h('div', null, F.field(t('playlists.name'), name, t('playlists.nameHelp', { bot: F.botLabel(b) })),
        h('p', { class: 'help' }, F.rich(t('playlists.hint')))),
      actions: [{ label: t('common.cancel') }, {
        label: t('common.save'), kind: 'primary', primary: true, icon: 'check', onClick: async () => {
          const n = name.value.trim();
          if (!n) { F.toast(t('playlists.nameReq'), 'err'); name.focus(); return false; }
          try {
            await F.api('POST', 'playlists', { name: n, bot: botName });
            F.toast(t('playlists.saved', { name: n }));
            if (onDone) onDone();
          } catch (e) { F.fail(e); return false; }
        },
      }],
    });
  };

  Pages.playlists = function (root) {
    const can = F.canControl();
    const list = h('div', { class: 'pl-list' }, F.skeleton(3, 'sk-row'));
    const saveB = can ? F.btn(t('playlists.save'), { kind: 'primary', icon: 'save', onClick: () => F.savePlaylist(F.state.selected, load) }) : null;
    root.append(F.pageHead(t('playlists.title'), t('playlists.sub'), saveB),
      h('div', { class: 'callout' }, icon('terminal'), h('div', null, F.rich(t('playlists.hint')))),
      list);
    F.fitPage(list);

    async function load() {
      try {
        const pls = (await F.api('GET', 'playlists')) || [];
        if (!pls.length) { list.replaceChildren(F.empty('playlist', t('playlists.none'), F.rich(t('playlists.noneHint')))); return; }
        list.replaceChildren(...pls.map(card));
      } catch (e) { F.fail(e); list.replaceChildren(F.empty('alert', t('err.loadFailed'))); }
    }
    function card(p) {
      const items = h('ol', { class: 'queue pl-items', hidden: true });
      const bodyId = F.uid('pl');
      items.id = bodyId;
      const toggle = h('button', { type: 'button', class: 'pl-head', 'aria-expanded': 'false', 'aria-controls': bodyId, title: t('playlists.show') },
        h('span', { class: 'pl-ic' }, icon('playlist')),
        h('span', { class: 'pl-text' }, h('strong', { class: 'pl-name', text: p.name }),
          h('span', { class: 'muted small mono', text: t('playlists.count', { n: p.count }) + (p.createdBy ? ' · ' + t('playlists.by', { user: p.createdBy }) : '') })),
        h('span', { class: 'plugin-chev' }, icon('chevron')));
      let loaded = false;
      async function loadItems() {
        items.replaceChildren(...F.skeleton(2, 'sk-row'));
        try {
          const rows = (await F.api('GET', 'playlists/' + enc(String(p.id)))) || [];
          loaded = true;
          p.count = rows.length;
          if (!rows.length) { items.replaceChildren(h('li', { class: 'queue-empty' }, F.empty('list', t('playlists.empty')))); return; }
          items.replaceChildren(...rows.map((it, i) => {
            const title = F.cleanTitle(it.title);
            return h('li', { class: 'q-item' },
              h('span', { class: 'q-num', text: String(i + 1).padStart(2, '0') }),
              thumb(it.thumbnail, it.type === 'radio' ? 'radio' : 'music'),
              h('div', { class: 'q-main' }, h('div', { class: 'q-title' }, it.link ? h('a', { href: it.link, target: '_blank', rel: 'noopener noreferrer', text: title, title }) : h('span', { text: title, title })),
                h('div', { class: 'q-sub', text: it.type || '' })),
              can ? h('div', { class: 'q-actions' }, F.iconBtn('trash', t('playlists.removeItem'), async () => {
                try { await F.api('DELETE', 'playlists/' + enc(String(p.id)) + '/items/' + enc(String(it.index))); await loadItems(); load(); }
                catch (e) { F.fail(e); }
              }, 'sm danger')) : null);
          }));
        } catch (e) { F.fail(e); items.replaceChildren(h('li', null, F.empty('alert', t('err.loadFailed')))); }
      }
      // songs open in a dialog instead of expanding the page
      toggle.addEventListener('click', () => {
        items.hidden = false;
        F.modal({ title: [icon('playlist'), ' ', p.name], size: 'wide', body: items, actions: [{ label: t('common.close') }] });
        if (!loaded) loadItems();
      });
      const target = F.selectedBot();
      const actions = can ? h('div', { class: 'pl-actions' },
        F.btn(t('playlists.play'), { kind: 'ghost', icon: 'play', disabled: !target || !p.count,
          title: target ? t('playlists.playHint', { bot: F.botLabel(target) }) : t('player.pickBot'),
          onClick: async (e) => {
            const b = F.selectedBot(); if (!b) return;
            await F.busy(e.currentTarget, async () => {
              try { await F.api('POST', 'playlists/' + enc(String(p.id)) + '/play', { bot: b.name }); F.toast(t('playlists.queued', { name: p.name, bot: F.botLabel(b) })); F.refreshBots(); }
              catch (err) { F.fail(err); }
            });
          } }),
        F.iconBtn('trash', t('playlists.delete'), async () => {
          if (!(await F.confirm(t('playlists.deleteConfirm', { name: p.name }), { danger: true, ok: t('common.delete') }))) return;
          try { await F.api('DELETE', 'playlists/' + enc(String(p.id))); F.toast(t('common.deleted')); load(); } catch (e) { F.fail(e); }
        }, 'danger')) : null;
      const el = h('article', { class: 'card pl-card' }, h('div', { class: 'pl-row' }, toggle, actions));
      return el;
    }
    load();
    return {};
  };
})();
