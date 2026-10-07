/* 도트 스프라이트를 코드로 그리는 파일 — 브라우저/Node 둘 다에서 동작
 * 그림은 전부 픽셀 좌표로 찍어서 만들고, 화면에서는 보간 없이 확대해서 써요.
 * 식물을 추가하려면 SPECIES 에 항목을 하나 더 만들고 logic.js 의 SPECIES 에도 같은 id 를 넣으면 돼요. */
(function (root, factory) {
  if (typeof module === 'object' && module.exports) module.exports = factory();
  else root.Sprites = factory();
})(typeof self !== 'undefined' ? self : this, function () {
  'use strict';

  const W = 32;
  const H = 40;
  const OUT = '#3a2a33', SOIL = '#5b3a29', SOIL_L = '#7a5038', EYE = '#2b1d2a';

  /* ---------- 픽셀 그리기 도구 ---------- */
  const grid = (w = W, h = H) => ({ w, h, d: Array.from({ length: h }, () => Array(w).fill(null)) });
  const px = (g, x, y, c) => { x = Math.round(x); y = Math.round(y); if (x < 0 || y < 0 || x >= g.w || y >= g.h) return; g.d[y][x] = c; };
  const get = (g, x, y) => (x < 0 || y < 0 || x >= g.w || y >= g.h ? null : g.d[y][x]);
  const rect = (g, x0, y0, x1, y1, c) => { for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) px(g, x, y, c); };
  const disc = (g, cx, cy, r, c) => { for (let y = Math.floor(cy - r); y <= Math.ceil(cy + r); y++) for (let x = Math.floor(cx - r); x <= Math.ceil(cx + r); x++) if ((x - cx) ** 2 + (y - cy) ** 2 <= r * r) px(g, x, y, c); };
  const ell = (g, cx, cy, rx, ry, c) => { for (let y = Math.floor(cy - ry); y <= Math.ceil(cy + ry); y++) for (let x = Math.floor(cx - rx); x <= Math.ceil(cx + rx); x++) { const a = (x - cx) / rx, b = (y - cy) / ry; if (a * a + b * b <= 1) px(g, x, y, c); } };
  const outline = (g, c) => { const add = []; for (let y = 0; y < g.h; y++) for (let x = 0; x < g.w; x++) { if (g.d[y][x]) continue; if (get(g, x - 1, y) || get(g, x + 1, y) || get(g, x, y - 1) || get(g, x, y + 1)) add.push([x, y]); } add.forEach(([x, y]) => (g.d[y][x] = c)); };
  const hex = (c) => [parseInt(c.slice(1, 3), 16), parseInt(c.slice(3, 5), 16), parseInt(c.slice(5, 7), 16)];
  const mix = (c1, c2, t) => { const a = hex(c1), b = hex(c2); return '#' + [0, 1, 2].map((i) => Math.round(a[i] + (b[i] - a[i]) * t).toString(16).padStart(2, '0')).join(''); };
  const ring = (g, cx, cy, n, dist, r, col) => { for (let i = 0; i < n; i++) { const a = -Math.PI / 2 + (i * 2 * Math.PI) / n; disc(g, cx + Math.cos(a) * dist, cy + Math.sin(a) * dist, r, Array.isArray(col) ? col[i % col.length] : col); } };

  /* ---------- 화분 (색은 사용자가 고름) ---------- */
  const SKINS = {
    terra: { name: '테라코타', body: '#e58f5b', shade: '#c46b3c', light: '#f6b88a', rim: '#f0a06a', rimS: '#d27e4a' },
    cream: { name: '크림', body: '#f3e3c3', shade: '#d6bf95', light: '#fff6e0', rim: '#fbeed3', rimS: '#e3cfa6' },
    mint: { name: '민트', body: '#8fd9c2', shade: '#5fb59c', light: '#c6f3e4', rim: '#a5e6d1', rimS: '#79c6ad' },
    sky: { name: '하늘', body: '#8fc3f0', shade: '#659fd6', light: '#c9e4fb', rim: '#a7d0f5', rimS: '#7fb0e2' },
    rose: { name: '분홍', body: '#f5a3b9', shade: '#d97b97', light: '#fdd0dc', rim: '#f8b8c9', rimS: '#e695ac' },
    lilac: { name: '라일락', body: '#b9a3ec', shade: '#8f78cc', light: '#dcd0fa', rim: '#c8b6f2', rimS: '#a48ddc' },
  };

  /* ---------- 꾸미기 아이템 (화분 위에 얹어요. 화분 색과 상관없이 같은 자리) ---------- */
  const ITEM_DRAW = {
    glasses(g, look) {
      const F = '#7a4a2a', lx = 12 + look, rx = 18 + look;
      for (const [x0, x1, b0, b1] of [[lx - 1, lx + 2, lx - 1, lx + 1], [rx - 1, rx + 2, rx, rx + 2]]) {
        rect(g, x0, 29, x1, 29, F); rect(g, b0, 33, b1, 33, F);
        rect(g, x0, 30, x0, 32, F); rect(g, x1, 30, x1, 32, F);
      }
      px(g, lx + 3, 31, F); px(g, lx + 4, 31, F);
    },
    shades(g, look) {
      const K = '#2b2b3a', lx = 12 + look, rx = 18 + look;
      rect(g, lx - 1, 29, lx + 2, 32, K); rect(g, rx - 1, 29, rx + 2, 32, K);
      rect(g, lx + 3, 30, lx + 4, 30, K);
      px(g, lx, 30, '#7a7a9c'); px(g, rx, 30, '#7a7a9c'); px(g, lx - 1, 29, '#3a3a52'); px(g, rx - 1, 29, '#3a3a52');
    },
    scarf(g) {
      const A = '#ff6b6b', B = '#fff3f0';
      for (let x = 8; x <= 23; x++) { px(g, x, 27, Math.floor(x / 2) % 2 ? A : B); px(g, x, 28, Math.floor(x / 2) % 2 ? B : A); }
      rect(g, 20, 29, 22, 32, A); rect(g, 20, 31, 22, 31, B); px(g, 20, 29, '#ff9b9b');
    },
    bowtie(g) {
      const W = '#5b6ee1', D = '#3a46a8';
      rect(g, 12, 27, 14, 29, W); rect(g, 17, 27, 19, 29, W); rect(g, 15, 27, 16, 29, D);
      px(g, 12, 27, '#8a9bff'); px(g, 17, 27, '#8a9bff'); px(g, 12, 29, D); px(g, 19, 29, D);
    },
    bell(g) {
      rect(g, 8, 27, 23, 27, '#e8567c');
      rect(g, 15, 28, 16, 30, '#ffd23f'); px(g, 15, 28, '#fff3b0'); px(g, 16, 30, '#e0a800'); px(g, 15, 29, '#e0a800');
    },
  };
  /* 머리 아이템은 식물 위에 한 번 더 얹어요(잎에 가려지지 않게). 화분 왼쪽 모서리 */
  const HEAD_DRAW = {
    ribbon(g) {
      const R = '#ff5a7a', D = '#d63a5c', Lt = '#ff9bb0';
      rect(g, 4, 19, 6, 22, R); rect(g, 9, 19, 11, 22, R); rect(g, 7, 20, 8, 21, D);
      rect(g, 4, 19, 6, 19, Lt); rect(g, 9, 19, 11, 19, Lt);
      px(g, 5, 23, R); px(g, 5, 24, D); px(g, 10, 23, R); px(g, 10, 24, D);
    },
    flowerpin(g) {
      ring(g, 8, 20, 5, 2.6, 1.5, '#ff9bd0'); disc(g, 8, 20, 1.4, '#ffe066'); px(g, 8, 19, '#fff3b0');
    },
    crown(g) {
      const A = '#ffd23f', B = '#e0a800';
      [8, 4, 12].forEach((x) => { px(g, x, 18, A); px(g, x, 19, A); });
      px(g, 8, 17, '#fff3b0');
      rect(g, 4, 20, 12, 22, A); rect(g, 5, 19, 5, 19, A); rect(g, 7, 19, 7, 19, A); rect(g, 9, 19, 9, 19, A); rect(g, 11, 19, 11, 19, A);
      rect(g, 4, 22, 12, 22, B);
      px(g, 8, 21, '#ff5a7a'); px(g, 6, 21, '#ffffff'); px(g, 10, 21, '#7ad7ff');
    },
  };
  function drawItems(g, items, look) {
    for (const id of (items || '').split(',')) if (id && ITEM_DRAW[id]) ITEM_DRAW[id](g, look);
  }

  /* ---------- 장난감 ---------- */
  function buildToy(kind, frame) {
    const g = grid(12, 12);
    if (kind === 'ball') {
      disc(g, 5.5, 5.5, 5, '#ff6b6b');
      for (let y = 0; y < 12; y++) for (let x = 0; x < 12; x++) if (get(g, x, y) && Math.abs((frame ? x + y : x - y + 11) - 11) <= 1) px(g, x, y, '#fff6f0');
      px(g, 3, 3, '#ffb3b3'); px(g, 4, 2, '#ffb3b3');
    } else if (kind === 'yarn') {
      disc(g, 5.5, 5.5, 5, '#ff9bd0');
      for (let y = 0; y < 12; y++) for (let x = 0; x < 12; x++) if (get(g, x, y) && (frame ? x + y : x - y + 12) % 4 === 0) px(g, x, y, '#ff6fb8');
      px(g, 3, 3, '#ffd0ea'); px(g, 4, 2, '#ffd0ea'); px(g, 10, 10, '#ff6fb8'); px(g, 11, 11, '#ff6fb8');
    } else if (kind === 'butterfly') {
      const open = frame === 0, rx = open ? 3.2 : 1.6, ry = open ? 3.4 : 3.8;
      ell(g, 6 - rx - 0.5, 4.5, rx, ry, '#ffa94d'); ell(g, 6 + rx + 0.5, 4.5, rx, ry, '#ffa94d');
      ell(g, 6 - rx, 8, rx * 0.7, 2, '#ffd166'); ell(g, 6 + rx + 1, 8, rx * 0.7, 2, '#ffd166');
      if (open) { px(g, 2, 4, '#ffffff'); px(g, 9, 4, '#ffffff'); }
      rect(g, 5, 3, 6, 9, '#6b4a3a'); px(g, 4, 2, '#6b4a3a'); px(g, 7, 2, '#6b4a3a');
    } else {
      for (let a = 0; a < 40; a++) { const t = (a / 40) * Math.PI * 2; px(g, 5.5 + Math.cos(t) * 5, 5.5 + Math.sin(t) * 5, '#8fd0ff'); }
      px(g, 3, 3, '#ffffff'); px(g, 3, 4, '#ffffff'); px(g, 4, 2, '#ffffff'); px(g, 7, 8, '#c9ecff');
      return g;
    }
    outline(g, OUT);
    return g;
  }

  /** face: idle | blink | happy | love | sad | sleep,  look: -1/0/1,  feet: 0 서있음 / 1 왼발 / 2 오른발 */
  function buildPot(skin, face, look, feet, items) {
    const s = SKINS[skin] || SKINS.terra, g = grid();
    rect(g, 11, 37, 13, feet === 1 ? 37 : 38, s.shade);
    rect(g, 18, 37, 20, feet === 2 ? 37 : 38, s.shade);
    for (let y = 27; y <= 36; y++) {
      const l = y <= 29 ? 8 : y <= 32 ? 9 : 10, r = 31 - l;
      rect(g, l, y, r, y, s.body); px(g, r, y, s.shade); px(g, r - 1, y, s.shade);
      if (y >= 28 && y <= 31) px(g, l + 1, y, s.light);
      if (y === 27 || y === 36) rect(g, l, y, r, y, s.shade);
    }
    rect(g, 7, 24, 24, 25, s.rim); rect(g, 7, 26, 24, 26, s.rimS);
    px(g, 7, 23, s.rim); px(g, 24, 23, s.rim); rect(g, 9, 24, 11, 24, s.light);
    rect(g, 8, 23, 23, 23, SOIL); px(g, 10, 23, SOIL_L); px(g, 14, 23, SOIL_L); px(g, 19, 23, SOIL_L);
    const lx = 12 + look, rx = 18 + look, m = look;
    if (face === 'blink' || face === 'sleep') { rect(g, lx, 31, lx + 1, 31, EYE); rect(g, rx, 31, rx + 1, 31, EYE); }
    else if (face === 'happy' || face === 'love') { for (const bx of [12 + look, 17 + look]) { px(g, bx, 31, EYE); px(g, bx + 1, 30, EYE); px(g, bx + 2, 31, EYE); } }
    else if (face === 'sad') { px(g, lx, 31, EYE); px(g, lx + 1, 32, EYE); px(g, rx, 32, EYE); px(g, rx + 1, 31, EYE); }
    else { rect(g, lx, 30, lx + 1, 32, EYE); rect(g, rx, 30, rx + 1, 32, EYE); px(g, lx, 30, '#ffffff'); px(g, rx, 30, '#ffffff'); }
    if (face === 'happy' || face === 'love') { rect(g, 14 + m, 33, 17 + m, 33, EYE); rect(g, 15 + m, 34, 16 + m, 34, '#e85a72'); }
    else if (face === 'sad') { px(g, 14 + m, 34, EYE); px(g, 15 + m, 33, EYE); px(g, 16 + m, 33, EYE); px(g, 17 + m, 34, EYE); px(g, lx, 33, '#6ec6ff'); px(g, lx, 34, '#6ec6ff'); }
    else if (face === 'sleep') { rect(g, 15 + m, 34, 16 + m, 34, EYE); }
    else { px(g, 14 + m, 33, EYE); px(g, 15 + m, 34, EYE); px(g, 16 + m, 34, EYE); px(g, 17 + m, 33, EYE); }
    const b = face === 'love' ? '#ff5f86' : face === 'happy' ? '#ff7f9c' : '#ff9bb0';
    [10, 11, 20, 21].forEach((x) => px(g, x, 33, b));
    if (face === 'love') { px(g, 9, 33, b); px(g, 22, 33, b); }
    drawItems(g, items, look);
    outline(g, OUT);
    return g;
  }

  /* ---------- 식물 ---------- */
  const G = { lf: '#58b84e', lfL: '#8fe07a', s0: '#79d36a', s1: '#4da84a' };
  const stem = (g, top, c0 = G.s0, c1 = G.s1) => { for (let y = top; y <= 22; y++) { px(g, 15, y, c0); px(g, 16, y, c1); } };
  const leaf = (g, x, y, rx, ry, c = G.lf, l = G.lfL) => { ell(g, x, y, rx, ry, c); px(g, x - rx * 0.5, y - ry * 0.6, l); px(g, x - rx * 0.1, y - ry * 0.6, l); };
  const leaves = (g, y, rx, ry, dx, c, l) => { leaf(g, 15.5 - dx, y, rx, ry, c, l); leaf(g, 15.5 + dx, y, rx, ry, c, l); };

  function seed(g) {
    rect(g, 13, 22, 18, 22, SOIL); rect(g, 14, 21, 17, 21, SOIL); px(g, 15, 21, SOIL_L); px(g, 17, 22, SOIL_L);
    px(g, 15, 19, '#d9b77e'); px(g, 16, 19, '#c39a5e'); px(g, 15, 20, '#c39a5e'); px(g, 16, 20, '#d9b77e');
  }
  // 1~2단계는 모든 식물이 비슷해서 정체를 몰라요. 잎 색만 살짝 달라서 눈치 빠르면 맞힐 수 있어요.
  function generic(g, stage, tint) {
    const c = mix(G.lf, tint, 0.45), l = mix(G.lfL, tint, 0.45);
    if (stage === 1) { stem(g, 19); leaves(g, 18, 2.2, 1.2, 2.6, c, l); }
    else { stem(g, 13); leaves(g, 19, 3.2, 1.5, 3.6, c, l); leaves(g, 15, 2.6, 1.3, 3.0, c, l); leaves(g, 12, 1.8, 1.0, 2.0, c, l); }
  }

  function tomato(g, stage, gold) {
    stem(g, 9);
    leaf(g, 11, 19, 3.2, 1.6); leaf(g, 20.5, 17, 3.2, 1.6); leaf(g, 11.5, 12.5, 2.8, 1.4); leaf(g, 20, 10.5, 2.6, 1.3); leaf(g, 15.5, 8, 2, 2.2);
    const ripe = stage >= 4, c = ripe ? (gold ? '#ffc82e' : '#ef4a3d') : '#97d96c', hl = ripe ? (gold ? '#fff2a8' : '#ffa79e') : '#cdf2b0';
    [[9.5, 16], [21.5, 14], [14, 20.5]].forEach(([x, y]) => { disc(g, x, y, 2.3, c); px(g, x - 1, y - 1, hl); px(g, x - 1, y - 3, '#3f9b44'); px(g, x + 1, y - 3, '#3f9b44'); px(g, x, y - 3, '#3f9b44'); });
    if (stage === 3) { px(g, 18, 7, '#ffe45c'); px(g, 19, 7, '#ffe45c'); px(g, 13, 6, '#ffe45c'); }
  }
  function basil(g, stage, purple) {
    const A = purple ? '#7b4aa8' : '#3f9f4a', B = purple ? '#a374d0' : '#6fcf72';
    stem(g, 10, purple ? '#8b66b0' : '#6bbf5e', purple ? '#6a4890' : '#3f8f45');
    const tiers = [[19, 3.6, 1.9, 4.2], [15, 3.3, 1.8, 3.7], [11.5, 2.7, 1.6, 3.0]];
    tiers.slice(0, stage >= 4 ? 3 : 2).forEach(([y, rx, ry, dx]) => leaves(g, y, rx, ry, dx, A, B));
    leaf(g, 15.5, stage >= 4 ? 8 : 12, 2.2, 2.6, A, B);
    if (stage >= 4) { [[15, 4], [16, 3], [15, 2], [16, 5], [14, 5], [17, 4]].forEach(([x, y]) => px(g, x, y, '#ffffff')); px(g, 15, 3, '#ffe9a8'); }
  }
  function cactusBody(g, x0, x1, top, bot) {
    const B = '#6cc36a', BD = '#4a9f52', BL = '#93dc8a';
    rect(g, x0, top + 1, x1, bot, B); rect(g, x0 + 1, top, x1 - 1, top, B); rect(g, x1 - 1, top + 1, x1, bot, BD); px(g, x1 - 1, top, BD);
    for (let y = top + 2; y <= bot - 1; y += 3) px(g, x0 + 1, y, BL);
  }
  function cactus(g, stage) {
    cactusBody(g, 13, 18, 8, 22); rect(g, 11, 16, 13, 17, '#6cc36a'); cactusBody(g, 10, 11, 12, 17); rect(g, 18, 14, 20, 15, '#6cc36a'); cactusBody(g, 20, 21, 10, 15);
    [[14, 11], [16, 14], [15, 18], [16, 20], [10, 14], [21, 12]].forEach(([x, y]) => px(g, x, y, '#e6f8de'));
    if (stage === 3) ell(g, 15.5, 6.3, 1.3, 1.7, '#ff8fb1'); else { ring(g, 15.5, 5.5, 6, 2.6, 1.4, '#ff7aa8'); disc(g, 15.5, 5.5, 1.3, '#ffe066'); }
  }
  function sunflower(g, stage) {
    stem(g, 10); leaves(g, 19, 3.4, 1.6, 3.7); leaves(g, 15, 3.0, 1.4, 3.3);
    if (stage === 3) { ell(g, 15.5, 7, 2.4, 3.2, G.lf); ell(g, 15.5, 5.7, 1.9, 2.0, '#ffd43b'); px(g, 15, 4, '#fff2a0'); }
    else { ring(g, 15.5, 6.5, 12, 4.4, 1.9, '#ffd43b'); disc(g, 15.5, 6.5, 3.4, '#8a5a2f'); disc(g, 15.5, 6.5, 2.2, '#6b4423'); [[-1, -1], [1, 0], [0, 1]].forEach(([dx, dy]) => px(g, 15.5 + dx + 0.5, 6.5 + dy, '#4a2d17')); }
  }
  function lettuce(g, stage) {
    const R = stage >= 4 ? 7.4 : 5.6;
    for (let i = 0; i < 10; i++) { const a = (i / 10) * Math.PI * 2; disc(g, 15.5 + Math.cos(a) * R, 18.5 + Math.sin(a) * R * 0.42, R * 0.42, '#4fb04a'); }
    for (let i = 0; i < 7; i++) { const a = (i / 7) * Math.PI * 2 + 0.3; disc(g, 15.5 + Math.cos(a) * R * 0.55, 18.2 + Math.sin(a) * R * 0.3, R * 0.34, '#7fd36a'); }
    ell(g, 15.5, 18, R * 0.38, R * 0.3, '#b6ec8a'); px(g, 14, 17, '#e3f9c8'); px(g, 17, 18, '#e3f9c8');
  }
  function mint(g, stage) {
    stem(g, 9, '#7fe0a8', '#4fb883');
    [[20, 3.2], [17, 3.0], [14, 2.8], [11, 2.4]].forEach(([y, dx]) => leaves(g, y, 2.4, 1.3, dx, '#4fcf88', '#b6f5cf'));
    leaf(g, 15.5, 7.5, 1.6, 2.0, '#4fcf88', '#b6f5cf');
    if (stage >= 4) { disc(g, 15.5, 4.5, 1.3, '#d9b8ff'); disc(g, 13.5, 5.5, 1.1, '#e6ccff'); disc(g, 17.5, 5.5, 1.1, '#e6ccff'); }
  }
  function strawberry(g, stage) {
    const tri = (x, y) => { disc(g, x - 2.2, y, 1.9, '#49a655'); disc(g, x + 2.2, y, 1.9, '#49a655'); disc(g, x, y - 2, 2.1, '#5cc268'); px(g, x - 0.5, y - 3, '#9be58a'); };
    stem(g, 16, '#7fcf70', '#4da84a'); tri(9.5, 18); tri(22, 17); tri(15.5, 13.5);
    [[10, 21], [21.5, 20.5], [15.5, 21]].forEach(([x, y]) => {
      if (stage >= 4) { ell(g, x, y, 2.1, 2.5, '#ef4a5b'); px(g, x - 1, y - 1, '#ff9aa5'); px(g, x + 1, y + 1, '#ffe27a'); px(g, x - 1, y + 1, '#ffe27a'); px(g, x - 1, y - 3, '#3f9b44'); px(g, x, y - 3, '#3f9b44'); px(g, x + 1, y - 3, '#3f9b44'); }
      else { disc(g, x, y - 1, 1.3, '#ffffff'); px(g, x, y - 1, '#ffd84a'); }
    });
  }
  function lavender(g, stage) {
    leaves(g, 20, 2.4, 1.0, 3.2, '#8fb58a', '#b6d4ad');
    [[12, 11], [15, 6], [19, 12]].forEach(([x, top]) => {
      for (let y = top + 4; y <= 22; y++) px(g, x, y, '#8fb58a');
      const len = stage >= 4 ? 8 : 5;
      for (let k = 0; k < len; k++) { const y = top + k, c = stage >= 4 ? (k % 2 ? '#9b6bd8' : '#c19ef0') : (k % 2 ? '#a99bd0' : '#b9d1a8'); px(g, x, y, c); if (k > 0) { px(g, x - 1, y, c); px(g, x + 1, y, c); } }
    });
  }

  const SPECIES = {
    tomato: { tint: '#7fd05a', draw: (g, s) => tomato(g, s, false) },
    basil: { tint: '#2f8f4a', draw: (g, s) => basil(g, s, false) },
    cactus: { tint: '#4aa89a', draw: cactus },
    sunflower: { tint: '#a6c94a', draw: sunflower },
    lettuce: { tint: '#b4e36a', draw: lettuce },
    mint: { tint: '#5fdca0', draw: mint },
    strawberry: { tint: '#4fa860', draw: strawberry },
    lavender: { tint: '#8fb58a', draw: lavender },
    purplebasil: { tint: '#8a5ab0', draw: (g, s) => basil(g, s, true) },
    goldtomato: { tint: '#a8d04a', draw: (g, s) => tomato(g, s, true) },
  };

  /** 시든 모습: 줄기가 오른쪽으로 휘어 처지고 색이 누렇게 바램 */
  function wiltify(src) {
    const g = grid(src.w, src.h), tint = (c) => mix(c, '#a59a5c', 0.5);
    for (let y = 0; y < src.h; y++) for (let x = 0; x < src.w; x++) {
      const c = src.d[y][x]; if (!c) continue;
      const h = 22 - y;
      if (h <= 0) px(g, x, y, tint(c)); else px(g, x + Math.round((h * h) / 26), y + Math.round((h * h) / 80), tint(c));
    }
    return g;
  }
  function buildPlant(id, stage, wilt) {
    const sp = SPECIES[id] || SPECIES.tomato;
    let g = grid();
    if (stage <= 0) seed(g); else if (stage <= 2) generic(g, stage, sp.tint); else sp.draw(g, stage);
    if (wilt && stage >= 1) g = wiltify(g);
    outline(g, OUT);
    return g;
  }
  function buildCan() {
    const g = grid(17, 12), B = '#5aa9f0', D = '#3d84c6';
    rect(g, 7, 2, 14, 8, B); rect(g, 7, 8, 14, 8, D); rect(g, 7, 2, 14, 2, '#a3d4ff'); rect(g, 13, 3, 14, 7, D);
    [[6, 5], [5, 6], [4, 7], [3, 8], [6, 4], [5, 5], [4, 6], [3, 7]].forEach(([x, y]) => px(g, x, y, B));
    rect(g, 15, 3, 16, 3, D); rect(g, 16, 4, 16, 7, D); px(g, 15, 8, D);
    outline(g, OUT);
    return g;
  }

  /* ---------- 캐시 & 변환 ---------- */
  const cache = new Map();
  const memo = (k, fn) => { if (!cache.has(k)) cache.set(k, fn()); return cache.get(k); };
  const potGrid = (skin, face, look, feet, items) => memo(`pot|${skin}|${face}|${look}|${feet}|${items || ''}`, () => buildPot(skin, face, look, feet, items));
  const plantGrid = (id, stage, wilt) => memo(`pl|${id}|${stage}|${wilt ? 1 : 0}`, () => buildPlant(id, stage, wilt));
  const canGrid = () => memo('can', buildCan);
  /** 도감에서 아직 못 만난 식물의 검은 그림자 */
  const silhouetteGrid = (id) => memo(`silu|${id}`, () => {
    const a = buildPlant(id, 4, false), p = buildPot('terra', 'idle', 0, 0);
    for (let y = 0; y < p.h; y++) for (let x = 0; x < p.w; x++) if (p.d[y][x] && !a.d[y][x]) a.d[y][x] = p.d[y][x];
    return a;
  });

  function toRGBA(g, flat) {
    const out = new Uint8Array(g.w * g.h * 4);
    const f = flat ? hex(flat) : null;
    for (let y = 0; y < g.h; y++) for (let x = 0; x < g.w; x++) {
      const c = g.d[y][x]; if (!c) continue;
      const [r, gg, b] = f || hex(c), i = (y * g.w + x) * 4;
      out[i] = r; out[i + 1] = gg; out[i + 2] = b; out[i + 3] = 255;
    }
    return out;
  }

  const headGrid = (id) => memo(`head|${id}`, () => { const g = grid(); if (HEAD_DRAW[id]) HEAD_DRAW[id](g); outline(g, OUT); return g; });
  const toyGrid = (kind, frame) => memo(`toy|${kind}|${frame}`, () => buildToy(kind, frame));
  const canvases = new Map();
  function toCanvas(key, g, flat) {
    if (!canvases.has(key)) {
      const cv = document.createElement('canvas'); cv.width = g.w; cv.height = g.h;
      const c = cv.getContext('2d');
      for (let y = 0; y < g.h; y++) for (let x = 0; x < g.w; x++) { const v = g.d[y][x]; if (v) { c.fillStyle = flat || v; c.fillRect(x, y, 1, 1); } }
      canvases.set(key, cv);
    }
    return canvases.get(key);
  }
  const potCanvas = (skin, face, look, feet, items) => toCanvas(`pot|${skin}|${face}|${look}|${feet}|${items || ''}`, potGrid(skin, face, look, feet, items));
  const plantCanvas = (id, stage, wilt) => toCanvas(`pl|${id}|${stage}|${wilt ? 1 : 0}`, plantGrid(id, stage, wilt));
  const headCanvas = (id) => toCanvas(`head|${id}`, headGrid(id));
  const toyCanvas = (kind, frame) => toCanvas(`toy|${kind}|${frame}`, toyGrid(kind, frame));
  const canCanvas = () => toCanvas('can', canGrid());
  const silhouetteCanvas = (id) => toCanvas(`silu|${id}`, silhouetteGrid(id), '#3a4a41');

  return {
    W, H, SKINS, SKIN_IDS: Object.keys(SKINS), SPECIES_IDS: Object.keys(SPECIES),
    ITEM_IDS: Object.keys(ITEM_DRAW).concat(Object.keys(HEAD_DRAW)), headGrid, headCanvas, toyGrid, toyCanvas, potGrid, plantGrid, canGrid, silhouetteGrid, toRGBA,
    potCanvas, plantCanvas, canCanvas, silhouetteCanvas,
  };
});
