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
  function tile(ic, label) {
    const value = h('div', { class: 'tile-value' });
    const foot = h('div', { class: 'tile-foot' });
    const el = h('section', { class: 'card tile' },
      h('div', { class: 'tile-head' }, h('span', { class: 'tile-ic' }, icon(ic)), h('span', { class: 'tile-label', text: label })),
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
    const tBots = tile('server', t('dash.botsOnline'));
    const tToday = tile('music', t('dash.playsToday'));
    const tWeek = tile('chart', t('dash.playsWeek'));
    const tUsers = tile('users', t('dash.onlineNow'));
    const tUp = tile('clock', t('dash.uptime'));

    // system (CPU line + memory figures in one card)
    const chart = lineChart([{ key: 'cpu', cls: 'main' }, { key: 'appCpu', cls: 'second' }]);
    const cpuNow = h('span', { class: 'big-num' });
    const legend = h('div', { class: 'legend' }, h('span', { class: 'lg main' }, t('dash.cpuSystem')), h('span', { class: 'lg second' }, t('dash.cpuApp')));
    const ramRing = ring(112);
    const memList = h('dl', { class: 'mem-list' });
    const sysOs = h('div', { class: 'muted small sys-os' });
    const sysCard = card('cpu', t('dash.system'), h('div', { class: 'row gap' }, legend, cpuNow),
      h('div', { class: 'sys-body' }, h('div', { class: 'chart-box' }, chart.el), h('div', { class: 'mem-wrap' }, ramRing.el, memList)), sysOs);

    // server activity
    const usersLine = usersChart();
    const heat = heatMap();
    const actInfo = h('span', { class: 'muted small' });
    const actBody = h('div', { class: 'act-body' }, h('div', { class: 'chart-box sm' }, usersLine.el), heat.el);
    const actEmpty = h('p', { class: 'muted small act-empty', text: t('dash.activityOff') });
    const actCard = card('users', t('dash.activity'), actInfo, actBody, actEmpty);

    // plays, top songs, bots
    const bars = barChart();
    const playsCard = card('chart', t('dash.plays14'), h('span', { class: 'muted small dash-total' }), bars.el);
    const topSongs = h('ol', { class: 'top-songs' });
    const songsCard = card('music', t('dash.topSongs'), h('a', { href: '/stats', 'data-link': '', class: 'small link-arrow', text: t('dash.seeAll') + ' →' }), topSongs);
    const botList = h('div', { class: 'dash-bots' });
    const botsCard = card('server', t('nav.bots'), h('a', { href: '/bots', 'data-link': '', class: 'small link-arrow', text: t('dash.manage') + ' →' }), botList);

    root.append(h('div', { class: 'dash' },
      h('div', { class: 'tile-grid' }, tBots.el, tToday.el, tWeek.el, tUsers.el, tUp.el),
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
      try { d = await F.api('GET', 'dashboard' + (first ? '?history=1' : '')); } catch (e) { if (first) F.fail(e); return; }
      if (!alive) return;
      const s = d.system;
      if (first && s.history) history = s.history;
      else if (s.now && (!history.length || history[history.length - 1].t !== s.now.t)) { history.push(s.now); if (history.length > 90) history.shift(); }
      chart.set(history);
      const n = s.now;
      cpuNow.textContent = n ? Math.round(n.cpu) + '%' : '–';
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
        actBody.hidden = !a.any; actEmpty.hidden = !!a.any;
        if (a.any) {
          usersLine.set((a.last24 || []).map((x) => x.users));
          heat.set(a.heat || []);
          const pk = a.peakAt ? new Date(a.peakAt) : null;
          actInfo.textContent = pk ? t('dash.peak', { n: a.peak, when: pk.toLocaleString(undefined, { weekday: 'short', hour: '2-digit', minute: '2-digit' }) }) : '';
        }
        tUsers.set(a.any ? F.fmtNum(a.now) : '–', null, a.any ? t('dash.peakShort', { n: a.peak }) : t('dash.activityShort'));
      }
      first = false;
    }

    load();
    timer = setInterval(load, 2000);
    const slow = setInterval(() => { first = true; }, 60000); // refresh slow-changing parts every minute
    if (F.state.botsLoaded) paintBots(F.state.bots);
    return {
      onBots: paintBots,
      destroy() { alive = false; clearInterval(timer); clearInterval(slow); root.classList.remove('dash-page'); },
    };
  };
})();
