/* 장난감 창: 화면 전체를 덮는 투명한 창(클릭은 통과)에서 장난감 하나가 떨어지고 튕겨요.
 * 화분이 받아치면(toy-kick) 튀어 오르고, 위치는 계속 화분 쪽으로 알려줘요. */
(() => {
  'use strict';
  const S = window.Sprites, api = window.toyApi;
  if (!api) return;
  const cv = document.getElementById('c'), ctx = cv.getContext('2d');
  const W = window.innerWidth, H = window.innerHeight;
  cv.width = W; cv.height = H;
  ctx.imageSmoothingEnabled = false;
  const SC = 4, R = 6 * SC; // 스프라이트 12x12 를 4배로, 반지름 약 24px
  const rand = (a, b) => a + Math.random() * (b - a);

  let toy = null, parts = [], last = performance.now(), lastSend = 0, running = false;

  api.onSpawn((o) => {
    const kind = o.kind;
    toy = {
      kind, x: Math.min(W - R, Math.max(R, o.x)), y: -R * 2, vx: rand(-50, 50), vy: kind === 'butterfly' ? 40 : 0,
      floor: o.floorY, t: 0, life: kind === 'bubble' ? 18 : 26, hits: 0, hover: rand(60, 100), run: 0, dead: false, deadT: 0, trail: [], trailT: 0,
    };
    if (!running) { running = true; last = performance.now(); requestAnimationFrame(loop); }
  });
  api.onKick((o) => {
    if (!toy || toy.dead) return;
    toy.hits++;
    if (toy.kind === 'bubble') { pop(); return; }
    if (toy.kind === 'butterfly') { toy.vx = o.vx * 0.5; toy.vy = -Math.abs(o.vy) * 0.45; } else { toy.vx = o.vx; toy.vy = o.vy; }
    for (let i = 0; i < 5; i++) parts.push({ x: toy.x, y: toy.y, vx: rand(-90, 90), vy: rand(-120, -20), life: 0.4, c: '#fff3b0' });
    if (toy.hits >= 14) toy.life = Math.min(toy.life, toy.t + 0.2);
  });

  function pop() {
    for (let i = 0; i < 14; i++) parts.push({ x: toy.x, y: toy.y, vx: rand(-160, 160), vy: rand(-160, 60), life: rand(0.3, 0.6), c: i % 2 ? '#bfe6ff' : '#ffffff' });
    toy.dead = true; toy.popped = true;
  }

  function step(dt) {
    const t = toy;
    t.t += dt;
    if (t.dead) { t.deadT += dt; return; }
    if (t.t > t.life) { if (t.kind === 'bubble') pop(); else t.dead = true; return; }
    if (t.kind === 'ball' || t.kind === 'yarn') {
      const bounce = t.kind === 'ball' ? 0.62 : 0.3, fric = t.kind === 'ball' ? 1.1 : 3;
      t.vy += 1500 * dt;
      t.x += t.vx * dt; t.y += t.vy * dt;
      if (t.y + R > t.floor) {
        t.y = t.floor - R;
        t.vy = Math.abs(t.vy) > 140 ? -t.vy * bounce : 0;
        t.vx -= t.vx * Math.min(1, fric * dt);
      }
    } else if (t.kind === 'butterfly') {
      const target = t.floor - t.hover + Math.sin(t.t * 3) * 14;
      t.vx += (Math.sin(t.t * 1.3) * 70 - t.vx) * Math.min(1, dt * 1.2);
      t.vy += ((target - t.y) * 2.4 - t.vy * 1.7) * dt;
      t.vy = Math.max(-300, Math.min(170, t.vy));
      t.x += t.vx * dt; t.y += t.vy * dt;
    } else {
      t.vx = Math.sin(t.t * 2) * 40; t.vy = 52;
      t.x += t.vx * dt; t.y += t.vy * dt;
      if (t.y + R >= t.floor) pop();
    }
    if (t.x < R) { t.x = R; t.vx = Math.abs(t.vx) * 0.8; }
    if (t.x > W - R) { t.x = W - R; t.vx = -Math.abs(t.vx) * 0.8; }
    t.run += Math.abs(t.vx) * dt;
    t.trailT += dt;
    if (t.kind === 'yarn' && t.trailT > 0.03) { t.trailT = 0; t.trail.push([t.x, t.y]); if (t.trail.length > 16) t.trail.shift(); }
  }

  function draw() {
    ctx.clearRect(0, 0, W, H);
    for (const p of parts) { ctx.globalAlpha = Math.max(0, Math.min(1, p.life * 3)); ctx.fillStyle = p.c; ctx.fillRect(Math.round(p.x / 4) * 4, Math.round(p.y / 4) * 4, 4, 4); }
    ctx.globalAlpha = 1;
    const t = toy;
    if (!t || (t.dead && t.popped)) return;
    if (t.dead) ctx.globalAlpha = Math.max(0, 1 - t.deadT / 0.4);
    if (t.kind === 'yarn' && t.trail.length > 1) {
      ctx.strokeStyle = '#ff9bd0'; ctx.lineWidth = 3; ctx.beginPath();
      t.trail.forEach(([x, y], i) => (i ? ctx.lineTo(x, y + 8) : ctx.moveTo(x, y + 8)));
      ctx.stroke();
    }
    const frame = t.kind === 'butterfly' ? (Math.floor(t.t * 8) % 2) : (Math.floor(t.run / 26) % 2);
    const img = S.toyCanvas(t.kind, frame);
    ctx.drawImage(img, Math.round(t.x - R), Math.round(t.y - R), 12 * SC, 12 * SC);
    ctx.globalAlpha = 1;
  }

  function loop(ts) {
    const dt = Math.min(0.05, Math.max(0.001, (ts - last) / 1000));
    last = ts;
    for (let i = parts.length - 1; i >= 0; i--) { const p = parts[i]; p.vy += 400 * dt; p.x += p.vx * dt; p.y += p.vy * dt; p.life -= dt; if (p.life <= 0) parts.splice(i, 1); }
    step(dt);
    draw();
    if (toy && !toy.dead && ts - lastSend > 33) { lastSend = ts; api.pos({ x: toy.x, y: toy.y, vx: toy.vx, vy: toy.vy, kind: toy.kind }); }
    if (toy && toy.dead && toy.deadT > 0.5 && !parts.length) { toy = null; running = false; api.end(); return; }
    requestAnimationFrame(loop);
  }
})();
