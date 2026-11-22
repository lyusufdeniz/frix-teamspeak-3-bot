/* Frix panel – dashboard (home): stat cards, live CPU/RAM, memory ring, plays per day, top songs/requesters, bots */
'use strict';
(() => {
  const F = window.F;
  const Pages = window.Pages;
  const h = F.h, t = F.t, icon = F.icon;
  const SVGNS = 'http://www.w3.org/2000/svg';
  const sv = (tag, attrs, ...kids) => {
    const el = document.createElementNS(SVGNS, tag);
    for (const k in attrs || {}) if (attrs[k] != null) el.setAttribute(k, attrs[k]);
    kids.forEach((c) => c && el.appendChild(c));
    return el;
  };

  F.fmtBytes = function (n) {
    n = Number(n) || 0;
    const u = ['B', 'KB', 'MB', 'GB', 'TB'];
    let i = 0;
    while (n >= 1024 && i < u.length - 1) { n /= 1024; i++; }
    return (i >= 3 ? n.toFixed(1) : Math.round(n)) + ' ' + u[i];
  };
  const fmtUptime = (s) => {
    const d = Math.floor(s / 86400), hh = Math.floor((s % 86400) / 3600), m = Math.floor((s % 3600) / 60);
    return d ? t('dash.uptimeD', { d, h: hh }) : hh ? t('dash.uptimeH', { h: hh, m }) : t('dash.uptimeM', { m: Math.max(1, m) });
  };
  const delta = (cur, prev) => {
    if (!prev) return cur ? { cls: 'up', text: t('dash.new') } : null;
    const p = Math.round(((cur - prev) / prev) * 100);
    return { cls: p >= 0 ? 'up' : 'down', text: (p >= 0 ? '+' : '') + p + '%' };
  };

  /** stat tile: icon, label, big value, foot (delta / hint) */
  /** stat tile; `go` = page path or a function, shown as an "open" button in the corner */
  function tile(ic, label, go) {
    const value = h('div', { class: 'tile-value' });
    const foot = h('div', { class: 'tile-foot' });
    const open = go ? h('button', { type: 'button', class: 'tile-go', title: t('dash.open'), 'aria-label': t('dash.open') + ': ' + label,
      onclick: (e) => { e.stopPropagation(); if (typeof go === 'function') go(); else F.go(go); } }, icon('link')) : null;
    const el = h('section', { class: 'card tile' },
      h('div', { class: 'tile-head' }, h('span', { class: 'tile-ic' }, icon(ic)), h('span', { class: 'tile-label', text: label }), open),
      value, foot);
    return {
      el,
      set(v, d, hint) {
        value.textContent = v;
        foot.replaceChildren(d ? h('span', { class: 'delta ' + d.cls }, icon(d.cls === 'up' ? 'arrowUp' : 'arrowDown', 'sm'), d.text) : null,
          hint ? h('span', { class: 'muted', text: hint }) : null);
      },
    };
  }

  /** smooth line chart (0..100) with gradient fill; series: [{key, cls}] */
  function lineChart(series) {
    const W = 600, H = 180, P = 6;
    const defs = sv('defs', null, sv('linearGradient', { id: 'cpuFill', x1: 0, y1: 0, x2: 0, y2: 1 },
      sv('stop', { offset: '0%', 'stop-color': 'var(--accent)', 'stop-opacity': '.35' }),
      sv('stop', { offset: '100%', 'stop-color': 'var(--accent)', 'stop-opacity': '0' })));
    const grid = sv('g', { class: 'chart-grid' }, ...[0, 25, 50, 75, 100].map((v) => {
      const y = P + (H - 2 * P) * (1 - v / 100);
      return sv('g', null, sv('line', { x1: 0, x2: W, y1: y, y2: y }), sv('text', { x: W - 2, y: y - 3, 'text-anchor': 'end' }, document.createTextNode(v + '%')));
    }));
    const fill = sv('path', { class: 'chart-fill', fill: 'url(#cpuFill)' });
    const lines = series.map((s) => sv('path', { class: 'chart-line ' + s.cls }));
    const svg = sv('svg', { viewBox: `0 0 ${W} ${H}`, preserveAspectRatio: 'none', class: 'chart', role: 'img' }, defs, grid, fill, ...lines);
    function path(vals) {
      if (!vals.length) return '';
      const n = Math.max(vals.length - 1, 1);
      const pts = vals.map((v, i) => [(W * i) / n, P + (H - 2 * P) * (1 - Math.min(100, Math.max(0, v)) / 100)]);
      let d = 'M' + pts[0][0].toFixed(1) + ',' + pts[0][1].toFixed(1);
      for (let i = 1; i < pts.length; i++) {
        const [x0, y0] = pts[i - 1], [x1, y1] = pts[i], cx = (x0 + x1) / 2;
        d += ` C${cx.toFixed(1)},${y0.toFixed(1)} ${cx.toFixed(1)},${y1.toFixed(1)} ${x1.toFixed(1)},${y1.toFixed(1)}`;
      }
      return d;
    }
    return {
      el: svg,
      set(rows) {
        series.forEach((s, i) => {
          const d = path(rows.map((r) => r[s.key]));
          lines[i].setAttribute('d', d);
          if (i === 0) fill.setAttribute('d', d ? d + ` L${W},${H} L0,${H} Z` : '');
        });
      },
    };
  }

  /** ring gauge */
  function ring(size) {
    const r = size / 2 - 9, c = 2 * Math.PI * r;
    const bg = sv('circle', { cx: size / 2, cy: size / 2, r, class: 'ring-bg' });
    const fg = sv('circle', { cx: size / 2, cy: size / 2, r, class: 'ring-fg', 'stroke-dasharray': c.toFixed(1), 'stroke-dashoffset': c.toFixed(1), transform: `rotate(-90 ${size / 2} ${size / 2})` });
    const val = h('div', { class: 'ring-val' });
    const sub = h('div', { class: 'ring-sub' });
    const el = h('div', { class: 'ring', style: `width:${size}px;height:${size}px` }, sv('svg', { viewBox: `0 0 ${size} ${size}`, width: size, height: size }, bg, fg), h('div', { class: 'ring-label' }, val, sub));
    return { el, set(p, v, s) { fg.setAttribute('stroke-dashoffset', (c * (1 - Math.min(1, Math.max(0, p)))).toFixed(1)); val.textContent = v; sub.textContent = s || ''; } };
  }

  /** vertical bar chart for plays per day */
  function barChart() {
    const el = h('div', { class: 'bars' });
    return {
      el,
      set(days) {
        const max = Math.max(1, ...days.map((d) => d.count));
        el.replaceChildren(...days.map((d) => {
          const date = new Date(d.day + 'T00:00:00Z');
          const label = date.toLocaleDateString(undefined, { day: 'numeric', month: 'short', timeZone: 'UTC' });
          return h('div', { class: 'bar-col', title: label + ': ' + d.count },
            h('div', { class: 'bar-track' }, h('div', { class: 'bar-fill' + (d.count ? '' : ' zero'), style: `height:${Math.max(d.count ? 6 : 2, (d.count / max) * 100)}%` },
              d.count ? h('span', { class: 'bar-num', text: String(d.count) }) : null)),
            h('div', { class: 'bar-label', text: date.getUTCDate() }));
        }));
      },
    };
  }

  const card = (ic, title, extra, ...kids) => h('section', { class: 'card dash-card' },
    h('div', { class: 'dash-card-head' }, h('h2', { class: 'card-title' }, h('span', { class: 'tile-ic sm' }, icon(ic)), title), extra || null), kids);

  /** users-per-hour line (scaled to its own max) */
  function usersChart() {
    const W = 600, H = 120, P = 6;
    const id = F.uid('uf');
    const defs = sv('defs', null, sv('linearGradient', { id, x1: 0, y1: 0, x2: 0, y2: 1 },
      sv('stop', { offset: '0%', 'stop-color': 'var(--accent)', 'stop-opacity': '.3' }),
      sv('stop', { offset: '100%', 'stop-color': 'var(--accent)', 'stop-opacity': '0' })));
    const fill = sv('path', { class: 'chart-fill', fill: `url(#${id})` });
    const line = sv('path', { class: 'chart-line main' });
    const svg = sv('svg', { viewBox: `0 0 ${W} ${H}`, preserveAspectRatio: 'none', class: 'chart' }, defs, fill, line);
    return {
      el: svg,
      set(vals) {
        const max = Math.max(1, ...vals);
        const n = Math.max(vals.length - 1, 1);
        const pts = vals.map((v, i) => [(W * i) / n, P + (H - 2 * P) * (1 - v / max)]);
        let d = pts.length ? 'M' + pts[0].join(',') : '';
        for (let i = 1; i < pts.length; i++) { const [x0, y0] = pts[i - 1], [x1, y1] = pts[i], cx = (x0 + x1) / 2; d += ` C${cx},${y0} ${cx},${y1} ${x1},${y1}`; }
        line.setAttribute('d', d);
        fill.setAttribute('d', d ? d + ` L${W},${H} L0,${H} Z` : '');
      },
    };
  }

  /** 7 × 24 heat map (Mon..Sun × hours) */
  function heatMap() {
    const el = h('div', { class: 'heat' });
    return {
      el,
      set(heat) {
        const max = Math.max(1, ...heat.flat());
        const days = t('dash.days').split(',');
        el.replaceChildren(...heat.map((row, d) => h('div', { class: 'heat-row' },
          h('span', { class: 'heat-day', text: days[d] || '' }),
          ...row.map((v, hr) => h('span', { class: 'heat-cell', title: `${days[d]} ${String(hr).padStart(2, '0')}:00 · ${v}`, style: `--o:${v ? 0.15 + 0.85 * (v / max) : 0}` })))),
        h('div', { class: 'heat-row heat-hours' }, h('span', { class: 'heat-day' }), ...Array.from({ length: 24 }, (_, i) => h('span', { class: 'heat-h', text: i % 6 === 0 ? String(i) : '' }))));
      },
    };
  }

  Pages.dashboard = function (root) {
    root.classList.add('dash-page');
    const tBots = tile('server', t('dash.botsOnline'), '/bots');
    const tToday = tile('music', t('dash.playsToday'), '/stats');
    const tWeek = tile('chart', t('dash.playsWeek'), '/stats');
    const tUsers = tile('users', t('dash.onlineNow'), () => openOnline());
    const tUp = tile('clock', t('dash.uptime'), '/logs');
    const tNet = tile('zap', t('dash.connection'), () => openConnection());
    // scope: the bot chosen in the sidebar, or every bot together (remembered)
    const lsGet = (k) => { try { return localStorage.getItem(k); } catch (_) { return null; } };
    const lsSet = (k, v) => { try { localStorage.setItem(k, v); } catch (_) { /* */ } };
    let scope = lsGet('frix.dashScope') === 'all' ? 'all' : 'bot';
    let scopeBot = F.state.selected || '';
    const scopeName = h('span');
    const segBot = h('button', { type: 'button', class: 'tab' }, icon('server'), scopeName);
    const segAll = h('button', { type: 'button', class: 'tab', text: t('dash.allBots') });
    const seg = h('div', { class: 'tabs seg dash-scope', role: 'group', 'aria-label': t('dash.scope') }, segBot, segAll);
    const paintScope = () => {
      const b = F.bot(scopeBot);
      scopeName.textContent = b ? F.botLabel(b) : t('dash.selectedBot');
      segBot.setAttribute('aria-pressed', String(scope === 'bot')); segBot.classList.toggle('on', scope === 'bot');
      segAll.setAttribute('aria-pressed', String(scope === 'all')); segAll.classList.toggle('on', scope === 'all');
    };
    const setScope = (v) => { if (scope === v) return; scope = v; lsSet('frix.dashScope', v); paintScope(); first = true; load(); };
    segBot.addEventListener('click', () => setScope('bot'));
    segAll.addEventListener('click', () => setScope('all'));
    const tpa = F.$('#top-page-actions');
    if (tpa) tpa.prepend(seg);
    paintScope();
    const botParam = () => (scope === 'bot' && scopeBot ? 'bot=' + F.enc(scopeBot) : '');
    // detailed connection state of one bot, refreshed while the dialog is open
    const openConnection = () => {
      const bots = F.state.bots || [];
      if (!bots.length) { F.toast(t('bots.none'), 'err'); return; }
      let cur = scope === 'bot' && scopeBot ? scopeBot : (bots.find((b) => b.live) || bots[0]).name;
      const pick = F.select(bots.map((b) => [b.name, F.botLabel(b)]), cur);
      const body = h('div', { class: 'conn' }, h('div', { class: 'conn-top' }, pick), h('div', { class: 'conn-body' }, F.skeleton(3, 'sk-row')));
      const out = body.querySelector('.conn-body');
      const kbps = (b) => (b == null ? '–' : ((b * 8) / 1000).toFixed(1) + ' kbit/s');
      let timer2 = null;
      const paint = async () => {
        let r;
        try { r = await F.api('GET', 'bots/' + F.enc(cur) + '/connection'); } catch (e) { out.replaceChildren(F.empty('alert', F.errMsg(e))); return; }
        if (r.state !== 'connected') {
          out.replaceChildren(h('div', { class: 'conn-state ' + r.state }, h('span', { class: 'dot dot-' + (r.state === 'offline' ? 'offline' : 'connecting') }),
            h('strong', { text: t('conn.state.' + r.state) }), h('span', { class: 'muted small mono', text: r.address })));
          return;
        }
        const pingCls = r.ping == null ? '' : r.ping > 150 ? 'bad' : r.ping > 80 ? 'mid' : 'good';
        const lossCls = r.loss == null ? '' : r.loss > 2 ? 'bad' : r.loss > 0.5 ? 'mid' : 'good';
        const big = (label, value, cls, sub) => h('div', { class: 'conn-big ' + (cls || '') }, h('span', { class: 'muted small', text: label }), h('strong', { text: value }), sub ? h('span', { class: 'muted small', text: sub }) : null);
        // ping history (last 60 keepalives)
        const pts = r.pings || [];
        const W = 600, H = 90, max = Math.max(50, ...pts) * 1.15;
        const path = pts.map((v, i) => (i ? 'L' : 'M') + ((W * i) / Math.max(1, pts.length - 1)).toFixed(1) + ',' + (H - (H * v) / max).toFixed(1)).join(' ');
        const svgNS = 'http://www.w3.org/2000/svg';
        const svg = document.createElementNS(svgNS, 'svg');
        svg.setAttribute('viewBox', `0 0 ${W} ${H}`); svg.setAttribute('preserveAspectRatio', 'none'); svg.setAttribute('class', 'conn-chart');
        if (path) {
          const fillP = document.createElementNS(svgNS, 'path'); fillP.setAttribute('d', path + ` L${W},${H} L0,${H} Z`); fillP.setAttribute('class', 'chart-fill conn-fill'); svg.append(fillP);
          const line = document.createElementNS(svgNS, 'path'); line.setAttribute('d', path); line.setAttribute('class', 'chart-line main'); svg.append(line);
        }
        const fmtDur = (sec) => { if (sec == null) return '–'; const hh = Math.floor(sec / 3600), mm = Math.floor((sec % 3600) / 60); return hh ? hh + ' sa ' + mm + ' dk' : mm + ' dk'; };
        out.replaceChildren(
          h('div', { class: 'conn-state connected' }, h('span', { class: 'dot dot-connected' }), h('strong', { text: r.server ? r.server.name : t('conn.state.connected') }),
            h('span', { class: 'muted small mono', text: r.address })),
          h('div', { class: 'conn-grid' },
            big(t('conn.ping'), r.ping == null ? '–' : Math.round(r.ping) + ' ms', pingCls, r.avgPing == null ? null : t('conn.avg', { n: Math.round(r.avgPing) }) + (r.jitter != null ? ' · ' + t('conn.jitter', { n: r.jitter }) : '')),
            big(t('conn.loss'), r.loss == null ? '–' : r.loss.toFixed(1) + '%', lossCls, r.loss == null ? t('dash.lossNa') : null),
            big(t('conn.down'), kbps(r.inSec), '', t('conn.perMin', { v: F.fmtBytes(r.inMin || 0) })),
            big(t('conn.up'), kbps(r.outSec), '', t('conn.perMin', { v: F.fmtBytes(r.outMin || 0) }))),
          h('div', { class: 'conn-chart-box' }, h('span', { class: 'muted small', text: t('conn.pingHistory') }), svg),
          h('dl', { class: 'kv conn-kv' },
            h('dt', { text: t('conn.connectedFor') }), h('dd', { text: fmtDur(r.connectedFor) }),
            h('dt', { text: t('conn.server') }), h('dd', { text: r.server ? r.server.version + ' · ' + r.server.platform : '–' }),
            h('dt', { text: t('conn.clients') }), h('dd', { text: r.clients + (r.server ? ' / ' + r.server.maxClients : '') }),
            h('dt', { text: t('conn.me') }), h('dd', { text: r.me ? r.me.nickname + ' (#' + r.me.id + ')' : '–' }),
            h('dt', { text: t('conn.channel') }), h('dd', { text: r.me && r.me.channel ? r.me.channel + (r.me.channelCodec ? ' · ' + r.me.channelCodec + ' q' + r.me.codecQuality : '') : '–' }),
            h('dt', { text: t('conn.audio') }), h('dd', { text: r.bitrate + ' kbps' + (r.playing ? ' · ' + t('conn.playing') : '') }),
            h('dt', { text: t('conn.packets') }), h('dd', { text: F.fmtNum(r.packetsIn || 0) + ' ↓ · ' + F.fmtNum(r.packetsOut || 0) + ' ↑' })));
      };
      pick.addEventListener('change', () => { cur = pick.value; out.replaceChildren(...F.skeleton(3, 'sk-row')); paint(); });
      F.modal({ title: [icon('zap'), ' ', t('conn.title')], size: 'wide', body, actions: [{ label: t('common.close') }],
        onClose: () => clearInterval(timer2) });
      paint();
      timer2 = setInterval(() => { if (!document.body.contains(body)) { clearInterval(timer2); return; } paint(); }, 2000);
    };
    // click "Online now" to see who is online
    tUsers.el.classList.add('tile-click');
    tUsers.el.tabIndex = 0;
    tUsers.el.setAttribute('role', 'button');
    tUsers.el.title = t('dash.onlineList');
    const openOnline = async () => {
      let r;
      try { r = await F.api('GET', 'online' + (botParam() ? '?' + botParam() : '')); } catch (e) { F.fail(e); return; }
      const ppl = (r && r.people) || [];
      const multi = new Set(ppl.map((p) => p.server)).size > 1;
      const st = (p) => [p.away ? h('span', { class: 'badge', text: t('dash.away') }) : null,
        p.micOff ? h('span', { class: 'ol-ic', title: t('dash.micOff') }, icon('micOff')) : null,
        p.soundOff ? h('span', { class: 'ol-ic', title: t('dash.soundOff') }, icon('volumeOff')) : null];
      const body = !r.online ? F.empty('server', t('dash.onlineNone'))
        : !ppl.length ? F.empty('users', t('dash.onlineEmpty'))
        : h('div', { class: 'ol-list' },
          h('div', { class: 'tbl-row ol-row tbl-head' }, h('span', { text: t('dash.olName') }), h('span', { text: t('dash.olChannel') }), h('span')),
          ppl.map((p) => h('div', { class: 'tbl-row ol-row' + (p.away ? ' away' : '') },
            h('span', { class: 'ol-name' }, p.country ? h('span', { class: 'ol-cc', text: p.country }) : null, h('span', { text: p.name })),
            h('span', { class: 'muted small tbl-ellipsis', text: (multi ? p.server + ' › ' : '') + p.channel }),
            h('span', { class: 'ol-st' }, st(p)))));
      F.modal({ title: t('dash.onlineList') + (r.online ? ' · ' + ppl.length : ''), size: 'wide', body, actions: [{ label: t('common.close') }] });
    };
    tUsers.el.addEventListener('click', openOnline);
    tUsers.el.addEventListener('keydown', (e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); openOnline(); } });

    // system (CPU line + memory figures in one card)
    const chart = lineChart([{ key: 'cpu', cls: 'main' }, { key: 'appCpu', cls: 'second' }]);
    // each legend item carries its own current value, so it is clear which number is which
    const cpuSys = h('b', { class: 'lg-val', text: '–' });
    const cpuApp = h('b', { class: 'lg-val', text: '–' });
    const legend = h('div', { class: 'legend' }, h('span', { class: 'lg sys' }, t('dash.cpuSystem'), cpuSys), h('span', { class: 'lg second' }, t('dash.cpuApp'), cpuApp));
    const ramRing = ring(112);
    const memList = h('dl', { class: 'mem-list' });
    const sysOs = h('div', { class: 'muted small sys-os' });
    const sysCard = card('cpu', t('dash.system'), legend,
      h('div', { class: 'sys-body' }, h('div', { class: 'chart-box' }, chart.el), h('div', { class: 'mem-wrap' }, ramRing.el, memList)), sysOs);

    // server activity
    const usersLine = usersChart();
    const heat = heatMap();
    const actInfo = h('span', { class: 'muted small' });
    const actBody = h('div', { class: 'act-body' }, h('div', { class: 'chart-box sm' }, usersLine.el), heat.el);
    const actEmpty = h('p', { class: 'muted small act-empty', text: t('dash.activityOff') });
    // activity recording off: the card stays, blurred, with why and how to turn it on
    const actOff = h('div', { class: 'act-off', hidden: true },
      h('div', { class: 'act-off-box' },
        h('span', { class: 'act-off-ic' }, icon('chart')),
        h('strong', { text: t('dash.actOffTitle') }),
        h('p', { class: 'muted small', text: t('dash.actOffWhy') }),
        F.btn(t('dash.actOffOpen'), { kind: 'primary', icon: 'link', cls: 'btn-sm', onClick: () => F.go('/plugins') })));
    const actWrap = h('div', { class: 'act-wrap' }, actBody, actOff);
    const actCard = card('users', t('dash.activity'), actInfo, actWrap, actEmpty);

    // plays, top songs, bots
    const bars = barChart();
    const playsCard = card('chart', t('dash.plays14'), h('span', { class: 'muted small dash-total' }), bars.el);
    const topSongs = h('ol', { class: 'top-songs' });
    const songsCard = card('music', t('dash.topSongs'), h('a', { href: '/stats', 'data-link': '', class: 'small link-arrow', text: t('dash.seeAll') + ' →' }), topSongs);
    const botList = h('div', { class: 'dash-bots' });
    const botsCard = card('server', t('nav.bots'), h('a', { href: '/bots', 'data-link': '', class: 'small link-arrow', text: t('dash.manage') + ' →' }), botList);

    root.append(h('div', { class: 'dash' },
      h('div', { class: 'tile-grid' }, tBots.el, tToday.el, tWeek.el, tUsers.el, tNet.el, tUp.el),
      h('div', { class: 'dash-row' }, sysCard, actCard),
      h('div', { class: 'dash-row three' }, playsCard, songsCard, botsCard)));

    let history = [];
    let alive = true, timer = null, first = true;

    function paintBots(bots) {
      const on = bots.filter((b) => b.status === 'connected' || b.live).length;
      tBots.set(on + ' / ' + bots.length, null, bots.length ? t('dash.botsHint', { n: bots.filter((b) => b.live && b.live.playing).length }) : t('bots.none'));
      botList.replaceChildren(...(bots.length ? bots.map((b) => {
        const p = b.live && b.live.playing;
        return h('a', { href: '/player', 'data-link': '', class: 'dash-bot', onclick: () => F.selectBot(b.name) },
          h('span', { class: 'dash-bot-cover' }, p && p.thumbnail ? h('img', { src: p.thumbnail, alt: '', loading: 'lazy' }) : icon(p ? 'music' : 'server')),
          h('span', { class: 'dash-bot-info' }, h('strong', { text: F.botLabel(b) }),
            h('span', { class: 'muted small', text: p ? p.title : t('status.' + b.status) })),
          h('span', { class: 'dot dot-' + b.status, title: t('status.' + b.status) }));
      }) : [h('p', { class: 'muted small', text: t('bots.none') })]));
    }

    async function load() {
      let d;
      const qs = [first ? 'history=1' : '', botParam()].filter(Boolean).join('&');
      try { d = await F.api('GET', 'dashboard' + (qs ? '?' + qs : '')); } catch (e) { if (first) F.fail(e); return; }
      if (!alive) return;
      const s = d.system;
      if (first && s.history) history = s.history;
      else if (s.now && (!history.length || history[history.length - 1].t !== s.now.t)) { history.push(s.now); if (history.length > 90) history.shift(); }
      chart.set(history);
      const n = s.now;
      cpuSys.textContent = n ? Math.round(n.cpu) + '%' : '–';
      cpuApp.textContent = n ? n.appCpu.toFixed(1) + '%' : '–';
      sysOs.textContent = s.os + ' · ' + t('dash.cores', { n: s.cores }) + ' · ' + t('dash.last3');
      if (n) {
        ramRing.set(n.ramTotal ? n.ramUsed / n.ramTotal : 0, Math.round((100 * n.ramUsed) / Math.max(1, n.ramTotal)) + '%', t('dash.ram'));
        memList.replaceChildren(
          h('dt', { text: t('dash.ramUsed') }), h('dd', { text: F.fmtBytes(n.ramUsed) + ' / ' + F.fmtBytes(n.ramTotal) }),
          h('dt', { text: t('dash.appMem') }), h('dd', { class: 'accent', text: F.fmtBytes(n.appMem) }),
          h('dt', { text: t('dash.appCpu') }), h('dd', { text: n.appCpu.toFixed(1) + '%' }),
          h('dt', { text: t('dash.threads') }), h('dd', { text: F.fmtNum(s.threads) }));
      }
      tUp.set(fmtUptime(s.uptime), null, 'frix v' + s.version);
      // connection quality of the bots (worst one when there are several)
      const nets = (d.net || []).filter((x) => x.ping != null);
      if (!nets.length) tNet.set('–', null, (d.net || []).length ? t('dash.netWait') : t('dash.onlineNone'));
      else {
        const w = nets.reduce((a, b) => ((b.ping || 0) > (a.ping || 0) ? b : a));
        const lossTxt = w.loss == null ? t('dash.lossNa') : t('dash.loss', { n: w.loss.toFixed(1) });
        tNet.set(Math.round(w.ping) + ' ms', null, lossTxt + (nets.length > 1 ? ' · ' + w.bot : ''));
        tNet.el.title = t('conn.title');
        tNet.el.classList.toggle('warn', w.ping > 150 || (w.loss || 0) > 2);
      }
      if (first) {
        tToday.set(F.fmtNum(d.plays.today), delta(d.plays.today, d.plays.yesterday), t('dash.vsYesterday'));
        tWeek.set(F.fmtNum(d.plays.week), delta(d.plays.week, d.plays.prevWeek), t('dash.vsLastWeek'));
        bars.set(d.perDay);
        playsCard.querySelector('.dash-total').textContent = t('dash.totalPlays', { n: F.fmtNum(d.plays.total) });
        topSongs.replaceChildren(...(d.topSongs.length ? d.topSongs.map((x, i) => h('li', null,
          h('span', { class: 'ts-rank', text: String(i + 1) }),
          h('span', { class: 'ts-thumb' }, x.thumbnail ? h('img', { src: x.thumbnail, alt: '', loading: 'lazy' }) : icon('music')),
          h('span', { class: 'ts-title', text: F.cleanTitle ? F.cleanTitle(x.title) : x.title }),
          h('span', { class: 'ts-count', text: '×' + x.count }))) : [h('li', { class: 'muted small', text: t('dash.noPlays') })]));
        const a = d.activity || {};
        const off = a.enabled === false;
        actWrap.classList.toggle('off', off); actOff.hidden = !off;
        actBody.hidden = !a.any && !off; actEmpty.hidden = !!a.any || off;
        if (off && !a.any) {
          // nothing recorded yet: a sample shape behind the blur
          usersLine.set(Array.from({ length: 24 }, (_, i) => 4 + Math.round(6 * Math.sin(i / 3.5) + (i % 5))));
          heat.set(Array.from({ length: 7 }, (_, d2) => Array.from({ length: 24 }, (_, hr) => Math.max(0, Math.sin((hr - 6) / 4) * 6 + d2))));
          actInfo.textContent = '';
        }
        if (a.any) {
          usersLine.set((a.last24 || []).map((x) => x.users));
          heat.set(a.heat || []);
          const pk = a.peakAt ? new Date(a.peakAt) : null;
          actInfo.textContent = pk ? t('dash.peak', { n: a.peak, when: pk.toLocaleString(undefined, { weekday: 'short', hour: '2-digit', minute: '2-digit' }) }) : '';
        }
      }
      // live count from the connected bots; '–' only when no bot is online and there is no recent sample
      const act = d.activity || {};
      tUsers.set(act.now != null ? F.fmtNum(act.now) : '–', null,
        act.now == null ? t('dash.onlineNone') : act.peak ? t('dash.peakShort', { n: act.peak }) : t('dash.onlineLive'));
      first = false;
    }

    load();
    timer = setInterval(load, 2000);
    const slow = setInterval(() => { first = true; }, 60000); // refresh slow-changing parts every minute
    if (F.state.botsLoaded) paintBots(F.state.bots);
    // connection tile opens the details too
    tNet.el.classList.add('tile-click');
    tNet.el.tabIndex = 0;
    tNet.el.setAttribute('role', 'button');
    tNet.el.addEventListener('click', openConnection);
    tNet.el.addEventListener('keydown', (e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); openConnection(); } });
    return {
      onBots(bots) {
        paintBots(bots);
        // the sidebar picked another bot: show its data
        if (F.state.selected && F.state.selected !== scopeBot) { scopeBot = F.state.selected; paintScope(); if (scope === 'bot') { first = true; load(); } }
        else paintScope();
      },
      destroy() { alive = false; clearInterval(timer); clearInterval(slow); root.classList.remove('dash-page'); seg.remove(); },
    };
  };
})();
