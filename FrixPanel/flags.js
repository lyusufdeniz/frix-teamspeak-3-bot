/* Frix panel – small inline flags for the language pickers (Windows does not render emoji flags).
   Drawn here as SVG and built with createElementNS (no innerHTML, no external files).
   Every flag uses a 30×21 grid (10:7 = the 20×14 px display size); CSS (.flag) adds radius and border. */
'use strict';
(() => {
  const NS = 'http://www.w3.org/2000/svg';
  let seq = 0;

  // five-pointed star centred at cx,cy (outer radius r), first point at angle a (degrees, 0 = right, -90 = up)
  function star(cx, cy, r, a) {
    const pts = [];
    for (let i = 0; i < 10; i++) {
      const rad = ((a + i * 36) * Math.PI) / 180, rr = i % 2 ? r * 0.382 : r;
      pts.push((cx + rr * Math.cos(rad)).toFixed(2) + ',' + (cy + rr * Math.sin(rad)).toFixed(2));
    }
    return pts.join(' ');
  }
  const rect = (x, y, w, hgt, fill) => ['rect', { x, y, width: w, height: hgt, fill }];
  const hBands = (...c) => c.map((f, i) => rect(0, (21 / c.length) * i, 30, 21 / c.length + 0.01, f));
  const vBands = (...c) => c.map((f, i) => rect((30 / c.length) * i, 0, 30 / c.length + 0.01, 21, f));

  // each entry: () => list of [tag, attrs, children?]; `id` is a per-instance prefix for clip paths
  const DEFS = {
    tr: () => [
      rect(0, 0, 30, 21, '#e30a17'),
      ['circle', { cx: 10.5, cy: 10.5, r: 5.25, fill: '#fff' }],
      ['circle', { cx: 11.8, cy: 10.5, r: 4.2, fill: '#e30a17' }],
      ['polygon', { points: star(16.4, 10.5, 2.6, 180), fill: '#fff' }],
    ],
    en: (id) => [
      ['clipPath', { id: id + 'c' }, [['path', { d: 'M15,10.5 h15 v10.5 z v10.5 h-15 z h-15 v-10.5 z v-10.5 h15 z' }]]],
      rect(0, 0, 30, 21, '#012169'),
      ['path', { d: 'M0,0 L30,21 M30,0 L0,21', stroke: '#fff', 'stroke-width': 4.2 }],
      ['path', { d: 'M0,0 L30,21 M30,0 L0,21', stroke: '#c8102e', 'stroke-width': 2.8, 'clip-path': 'url(#' + id + 'c)' }],
      ['path', { d: 'M15,0 v21 M0,10.5 h30', stroke: '#fff', 'stroke-width': 7 }],
      ['path', { d: 'M15,0 v21 M0,10.5 h30', stroke: '#c8102e', 'stroke-width': 4.2 }],
    ],
    de: () => hBands('#000', '#dd0000', '#ffce00'),
    es: () => [rect(0, 0, 30, 21, '#aa151b'), rect(0, 5.25, 30, 10.5, '#f1bf00')],
    fr: () => vBands('#002395', '#fff', '#ed2939'),
    ru: () => hBands('#fff', '#0039a6', '#d52b1e'),
    pt: () => [
      rect(0, 0, 30, 21, '#009c3b'),
      ['polygon', { points: '3,10.5 15,2.2 27,10.5 15,18.8', fill: '#ffdf00' }],
      ['circle', { cx: 15, cy: 10.5, r: 4.6, fill: '#002776' }],
      ['path', { d: 'M10.6,9.4 Q15,8.3 19.5,11.6', stroke: '#fff', 'stroke-width': 0.9, fill: 'none' }],
    ],
    pl: () => hBands('#fff', '#dc143c'),
    it: () => vBands('#009246', '#fff', '#ce2b37'),
  };

  function build(parent, nodes) {
    for (const [tag, attrs, kids] of nodes) {
      const el = document.createElementNS(NS, tag);
      for (const k of Object.keys(attrs)) el.setAttribute(k, String(attrs[k]));
      if (kids) build(el, kids);
      parent.appendChild(el);
    }
  }

  /** <span class="flag flag-xx"><svg…></span>; unknown codes get a neutral box with the code */
  function flag(code) {
    const wrap = document.createElement('span');
    wrap.className = 'flag flag-' + code;
    wrap.setAttribute('aria-hidden', 'true');
    const def = DEFS[code];
    if (!def) { wrap.classList.add('flag-none'); wrap.textContent = String(code || '?').slice(0, 2); return wrap; }
    const svg = document.createElementNS(NS, 'svg');
    svg.setAttribute('viewBox', '0 0 30 21');
    svg.setAttribute('width', '20');
    svg.setAttribute('height', '14');
    svg.setAttribute('focusable', 'false');
    svg.setAttribute('preserveAspectRatio', 'none');
    build(svg, def('fl' + ++seq));
    wrap.appendChild(svg);
    return wrap;
  }

  window.Flags = { el: flag, has: (code) => !!DEFS[code], codes: Object.keys(DEFS) };
})();
