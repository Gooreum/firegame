'use strict';
// 거품 눈덩이 · 비눗방울 · 맨홀 간헐천 · 물 사슬 (items-a.js 짜임을 따른다)
// 다른 아이템 파일과 이름이 겹치지 않게 블록 안에 둔다.
{

const L5 = lv => Math.min(lv, 5);
/** 무지개 반짝이(Lv6) / 금 반짝이(Lv5) 한 점 */
function glint(s, x, y, lv, n = 1) {
  if (lv < 5) return;
  for (let i = 0; i < n; i++) part(s, { kind: 'star', x: x + rand(-6, 6), y: y + rand(-6, 6), vx: rand(-30, 30), vy: rand(-50, -10), life: rand(.3, .5), size: rand(2.5, 4), color: lv >= 6 ? null : '255,215,110' });
}

// ---------------------------------------------------------------- 3. 거품 눈덩이 → 거품 산사태
function foamPuff(g, x, y, r, lv, t) {
  const gr = g.createRadialGradient(x - r * .35, y - r * .4, r * .05, x, y, r);
  gr.addColorStop(0, '#ffffff'); gr.addColorStop(.65, lv >= 3 ? '#eef8ff' : '#f2f6f9'); gr.addColorStop(1, lv >= 5 ? '#f3dca0' : lv >= 3 ? '#a9d6f2' : '#c4d6e2');
  g.fillStyle = gr; g.beginPath(); g.arc(x, y, r, 0, TAU); g.fill();
}
function iconFoam(g, x, y, r, lv, t) {
  for (const [dx, dy, k] of [[-.45, .15, .55], [.45, .2, .55], [0, -.3, .62], [0, .25, .6]]) foamPuff(g, x + dx * r, y + dy * r, r * k, lv, t);
  g.fillStyle = 'rgba(255,255,255,.9)'; g.beginPath(); g.ellipse(x - r * .25, y - r * .5, r * .2, r * .1, -.5, 0, TAU); g.fill();
}
function foamBallStep(s, f, dt, lv) {
  f.k += dt;
  const c = s.nearest(f.x, f.y, 320); if (c) { const dx = c.x - f.x, dy = c.y - f.y, L = Math.hypot(dx, dy) || 1, sp = 110 + lv * 18; f.vx += (dx / L * sp - f.vx) * dt * 2.2; f.vy += (dy / L * sp - f.vy) * dt * 2.2; }
  f.x += f.vx * dt; f.y += f.vy * dt; f.roll += Math.hypot(f.vx, f.vy) * dt / f.r;
  f.x = clamp(f.x, 20, W - 20); f.y = clamp(f.y, 30, H - 10);
  for (const m of s.alive()) if (Math.hypot(m.x - f.x, m.y - f.y) < f.r + m.r) {
    // 삼킨 몹은 그 자리에서 처치로 친다(거품 안에 박힌 채 굴러간다).
    m.dead = true; f.in.push({ a: rand(0, TAU), d: rand(.3, .8), big: m.big });
    f.r = Math.min(f.max, f.r + (m.big ? 4 : 2)); hitFx(s, m.x, m.y, lv, .4);
  }
  if (Math.random() < dt * (20 + lv * 6)) part(s, { kind: 'foam', x: f.x + rand(-f.r, f.r) * .7, y: f.y + f.r * .4, life: .8, size: rand(3, 5 + lv) });
  glint(s, f.x, f.y - f.r, lv, Math.random() < dt * 12 ? 1 : 0);
  if (f.k > 1.7 || f.r >= f.max) {
    const R = f.r * (1.8 + lv * .1);
    for (let i = 0; i < 26 + lv * 8; i++) { const a = rand(0, TAU), v = rand(40, 160 + lv * 30); part(s, { kind: 'foam', x: f.x, y: f.y - f.r * .5, vx: Math.cos(a) * v, vy: Math.sin(a) * v * .6, life: rand(.6, 1.1), size: rand(4, 8 + lv) }); }
    part(s, { kind: 'ring', x: f.x, y: f.y, life: .4, size: R, color: lv >= 5 ? '255,220,130' : '255,255,255', width: 4 + lv });
    part(s, { kind: 'glow', x: f.x, y: f.y, life: .35, size: R * 1.1, color: tierOf(lv).glow });
    s.hitArea(f.x, f.y, R, [0, 4, 5, 6, 8, 10, 12][lv], 180);
    hitFx(s, f.x, f.y, lv, 1.4);
    s.shake = Math.max(s.shake, 3 + lv); if (lv >= 4) s.flash = Math.max(s.flash, .1);
    return false;
  }
  return true;
}
function drawFoamBall(g, f, lv, t, sg) {
  const r = f.r * sg;
  shadowAt(g, f.x, f.y + r * .5, r * 1.05, .3);
  const y = f.y - r * .55;
  tierGlow(g, f.x, y, r, lv, t);
  // 안쪽 몹(뒤쪽)
  for (const o of f.in) { const a = o.a + f.roll; if (Math.sin(a) <= .2) { const x = f.x + Math.cos(a) * r * o.d, yy = y + Math.sin(a) * r * o.d * .8; const gr = g.createRadialGradient(x - 1, yy - 1, .5, x, yy, 5); gr.addColorStop(0, '#b56cf0'); gr.addColorStop(1, '#4c1a85'); g.fillStyle = gr; g.beginPath(); g.arc(x, yy, o.big ? 6 : 4.5, 0, TAU); g.fill(); } }
  for (let i = 0; i < 9; i++) { const a = i / 9 * TAU + f.roll * .5, rr = r * (i ? .55 : 0); g.globalAlpha = .94; foamPuff(g, f.x + Math.cos(a) * rr, y + Math.sin(a) * rr * .8, r * (i ? .55 : .72), lv, t); g.globalAlpha = 1; }
  // 앞쪽 몹: 거품 밖으로 삐져나온 보라 머리
  for (const o of f.in) { const a = o.a + f.roll; if (Math.sin(a) > .2) { const x = f.x + Math.cos(a) * r * .92, yy = y + Math.sin(a) * r * .78; const gr = g.createRadialGradient(x - 1.5, yy - 1.5, .5, x, yy, 5); gr.addColorStop(0, '#c58af5'); gr.addColorStop(1, '#5a1f9a'); g.fillStyle = gr; g.strokeStyle = '#1b0830'; g.lineWidth = 1.1; g.beginPath(); g.arc(x, yy, o.big ? 5.5 : 4, 0, TAU); g.fill(); g.stroke(); g.fillStyle = '#fff'; g.beginPath(); g.arc(x - 1.3, yy - .6, 1.1, 0, TAU); g.arc(x + 1.3, yy - .6, 1.1, 0, TAU); g.fill(); } }
  // 광택
  g.fillStyle = 'rgba(255,255,255,.85)'; g.beginPath(); g.ellipse(f.x - r * .35, y - r * .5, r * .22, r * .1, -.5, 0, TAU); g.fill();
  if (lv >= 5) { g.save(); g.globalCompositeOperation = 'lighter'; g.strokeStyle = lv >= 6 ? `hsla(${hue(t)},100%,72%,.7)` : 'rgba(255,214,110,.7)'; g.lineWidth = 2; g.beginPath(); g.arc(f.x, y, r * 1.02, t * 3, t * 3 + 2.4); g.stroke(); g.restore(); }
}
item({
  id: 'foam', group: '무기', name: '거품 눈덩이', evoName: '거품 산사태', target: 'house', sides: ['top', 'topright'],
  lvText: [null, '굴러가며 요괴를 삼키고 펑 터지는 거품', '더 크게 부푼다 · 더 세게 터진다', '거품 2개 · 푸른 빛', '더 빨리 굴러간다 · 더 크게', '거품 3개 · 금빛 · 큰 폭발', '거품 산사태: 화면을 덮치는 거품 파도'],
  icon: iconFoam,
  setup(s) { s.st.f = []; s.st.cd = .3; s.st.waves = []; s.st.wcd = .9; s.player.x = 150; s.player.y = 170; },
  update(s, dt, lv) {
    const n = [0, 1, 1, 2, 2, 3, 3][lv];
    s.st.cd -= dt;
    if (s.st.cd <= 0) {
      for (let i = 0; i < n; i++) s.st.f.push({ x: s.player.x + (i - (n - 1) / 2) * 22, y: s.player.y - 10, vx: rand(-20, 20), vy: -60, r: 10 + lv * 1.5, max: [0, 22, 26, 30, 34, 40, 40][lv], k: 0, roll: 0, in: [] });
      s.st.cd = [0, 1.8, 1.4, 1.5, 1.2, 1.1, 1.1][lv];
    }
    s.st.f = s.st.f.filter(f => foamBallStep(s, f, dt, lv));
    if (lv < 6) return;
    // 거품 산사태: 왼쪽에서 오른쪽으로 화면을 덮치는 파도
    s.st.wcd -= dt;
    if (s.st.wcd <= 0) { s.st.waves.push({ x: -70, k: 0 }); s.st.wcd = 1.7; }
    for (const w of s.st.waves) {
      w.k += dt; w.x += 300 * dt;
      for (const m of s.alive()) if (m.x < w.x + 22 && m.x > w.x - 90) { m.hit(s, 99); if (Math.random() < .4) hitFx(s, m.x, m.y, 6, .5); }
      for (let i = 0; i < 5; i++) part(s, { kind: 'foam', x: w.x + rand(-10, 24), y: rand(0, H), vx: rand(80, 200), vy: rand(-30, 30), life: .6, size: rand(4, 10) });
      if (Math.random() < .5) glint(s, w.x + 10, rand(10, H - 10), 6, 1);
      s.shake = Math.max(s.shake, 2.5);
    }
    s.st.waves = s.st.waves.filter(w => w.x < W + 120);
  },
  ents(s, g, lv) { return s.st.f.map(f => ({ y: f.y, d: () => drawFoamBall(g, f, lv, s.t, (1.25 + (lv - 1) * .05) * (1 + s.surge * .5)) })); },
  drawAir(s, g, lv) {
    for (const w of s.st.waves || []) {
      g.save();
      const gr = g.createLinearGradient(w.x - 140, 0, w.x + 24, 0); gr.addColorStop(0, 'rgba(255,255,255,0)'); gr.addColorStop(.6, 'rgba(235,248,255,.8)'); gr.addColorStop(1, 'rgba(255,255,255,.98)');
      g.fillStyle = gr; g.fillRect(w.x - 140, 0, 164, H);
      // 무지개 물광
      g.globalCompositeOperation = 'lighter';
      const rg = g.createLinearGradient(0, 0, 0, H); for (let i = 0; i <= 6; i++) rg.addColorStop(i / 6, `hsla(${hue(s.t, i * 60)},100%,70%,.28)`);
      g.fillStyle = rg; g.fillRect(w.x - 30, 0, 26, H);
      g.globalCompositeOperation = 'source-over';
      for (let y = -10; y < H + 20; y += 15) { const r = 14 + Math.sin(y * .3 + s.t * 8) * 4; foamPuff(g, w.x + 16 + Math.sin(y + s.t * 6) * 6, y, r, 5, s.t); }
      g.restore();
    }
  },
});

// ---------------------------------------------------------------- 4. 비눗방울 → 방울 폭포
function bubbleDraw(g, x, y, r, t, lv) {
  const gr = g.createRadialGradient(x - r * .3, y - r * .35, r * .1, x, y, r);
  gr.addColorStop(0, 'rgba(255,255,255,.45)'); gr.addColorStop(.7, 'rgba(180,220,255,.12)');
  gr.addColorStop(.88, lv >= 5 && lv < 6 ? 'rgba(255,215,120,.55)' : `hsla(${(t * 120) % 360},90%,75%,.55)`);
  gr.addColorStop(1, lv >= 5 && lv < 6 ? 'rgba(255,240,180,.9)' : `hsla(${(t * 120 + 120) % 360},90%,80%,.85)`);
  g.fillStyle = gr; g.beginPath(); g.arc(x, y, r, 0, TAU); g.fill();
  g.strokeStyle = lv >= 5 ? 'rgba(255,240,200,.85)' : 'rgba(255,255,255,.7)'; g.lineWidth = lv >= 3 ? 1.6 : 1.2; g.stroke();
  g.fillStyle = 'rgba(255,255,255,.9)'; g.beginPath(); g.ellipse(x - r * .38, y - r * .45, r * .22, r * .12, -.6, 0, TAU); g.fill();
  g.fillStyle = 'rgba(255,255,255,.6)'; g.beginPath(); g.arc(x + r * .4, y + r * .35, r * .07, 0, TAU); g.fill();
}
function popBubble(s, x, y, lv) {
  for (let i = 0; i < 8 + lv * 2; i++) { const a = rand(0, TAU), v = rand(80, 160); part(s, { kind: 'spark', x, y, vx: Math.cos(a) * v, vy: Math.sin(a) * v, life: .35, size: 2.5, color: '220,240,255' }); }
  part(s, { kind: 'ring', x, y, life: .3, size: 22 + lv * 4, color: lv >= 5 ? '255,220,140' : '255,255,255', width: 2 + lv * .4 });
  for (let i = 0; i < 4 + lv; i++) part(s, { kind: 'drop', x: x + rand(-10, 10), y, z: 20, vz: -20, grav: 500, life: .7, size: 2.5 });
}
item({
  id: 'bubble', group: '무기', name: '비눗방울', evoName: '방울 폭포', target: 'house', sides: ['top', 'topright'],
  lvText: [null, '요괴를 방울에 가둬 띄웠다가 펑', '방울 2발 · 더 빠르게', '방울 3발 · 큰 요괴도 가둔다 · 터지면 물보라', '방울 3발 · 연사', '방울 4발 · 금빛 · 큰 물보라', '방울 폭포: 거대 방울로 모아 물폭포로 터뜨린다'],
  icon(g, x, y, r, lv, t) { bubbleDraw(g, x, y, r, t, lv); const gr = g.createRadialGradient(x - 1, y - 1, .5, x, y, r * .45); gr.addColorStop(0, '#b56cf0'); gr.addColorStop(1, '#4c1a85'); g.fillStyle = gr; g.beginPath(); g.arc(x, y + r * .1, r * .42, 0, TAU); g.fill(); },
  setup(s) { s.st.shots = []; s.st.cap = []; s.st.cd = .3; s.st.big = null; s.player.x = 150; s.player.y = 170; },
  onLevel(s, lv) { if (lv === 6) s.st.big = { x: 250, y: 64, r: 16, n: 0, k: 0 }; },
  update(s, dt, lv) {
    const P = s.player, l = L5(lv);
    s.st.cd -= dt;
    if (s.st.cd <= 0) {
      const n = [0, 1, 2, 3, 3, 4, 5][lv], used = new Set();
      for (let i = 0; i < n; i++) { const m = s.nearest(P.x, P.y, 230, used); if (!m) break; used.add(m); s.st.shots.push({ x: P.x + 4, y: P.y - 12, m, trail: [] }); }
      s.st.cd = [0, .9, .7, .7, .5, .42, .3][lv];
    }
    for (const b of s.st.shots) {
      if (b.m.dead || b.m.held || b.m.z > 0) { b.done = 1; continue; }
      const dx = b.m.x - b.x, dy = b.m.y - 6 - b.y, L = Math.hypot(dx, dy) || 1, sp = 300 + l * 30;
      b.trail.unshift({ x: b.x, y: b.y }); if (b.trail.length > 6) b.trail.pop();
      b.x += dx / L * sp * dt; b.y += dy / L * sp * dt;
      if (L < 10) {
        b.done = 1;
        if (b.m.big && l < 3) { b.m.hit(s, 3); popBubble(s, b.m.x, b.m.y - 6, lv); continue; }
        b.m.held = true; s.st.cap.push({ m: b.m, k: 0, x: b.m.x, y: b.m.y });
        part(s, { kind: 'ring', x: b.m.x, y: b.m.y - 6, life: .25, size: 20, color: '220,240,255', width: 2 });
      }
    }
    s.st.shots = s.st.shots.filter(b => !b.done);
    const B = s.st.big;
    for (const c of s.st.cap) {
      c.k += dt; const m = c.m; m.vx = m.vy = 0; m.vz = 0;
      if (lv === 6 && B) {
        // 거대 방울로 빨려 올라간다
        c.x += (B.x - c.x) * dt * 3; c.y += (B.y + 60 - c.y) * dt * 3; m.x = c.x; m.y = c.y; m.z = Math.min(60, m.z + dt * 120);
        if (Math.hypot(c.x - B.x, c.y - (B.y + 60)) < 14) { c.done = 1; B.n++; B.r = Math.min(56, B.r + 2.2); m.dead = true; glint(s, B.x, B.y, 6, 2); }
        continue;
      }
      m.z = Math.min(46, c.k * 55); m.x = c.x + Math.sin(c.k * 6) * 4;
      if (c.k > 1.0) {
        c.done = 1; const x = m.x, y = c.y - m.z; popBubble(s, x, y, lv);
        m.z = 0; m.held = false; m.kill(s);
        if (l >= 3) { splash(s, c.x, c.y, 18 + l * 3, 8); s.hitArea(c.x, c.y, 18 + l * 3, l >= 5 ? 3 : 2, 100); hitFx(s, c.x, c.y, lv, .7); }
      }
    }
    s.st.cap = s.st.cap.filter(c => !c.done);
    if (lv === 6 && B) {
      B.k += dt;
      if (B.k > 1.6) {
        // 펑 → 아래로 물폭포
        const fx = B.x, fy = B.y + 70;
        for (let i = 0; i < 50; i++) part(s, { kind: 'drop', x: B.x + rand(-B.r, B.r), y: fy, z: 70 + rand(0, 40), vx: rand(-70, 70), vy: rand(-10, 50), vz: rand(-40, 40), grav: 500, life: 1.2, size: rand(2.5, 4.5) });
        splash(s, fx, fy, 80, 26); s.hitArea(fx, fy, 120, 99, 260); hitFx(s, fx, fy, 6, 2);
        for (let i = 0; i < 26; i++) { const a = rand(0, TAU); part(s, { kind: 'spark', x: B.x, y: B.y, vx: Math.cos(a) * 240, vy: Math.sin(a) * 240, life: .5, size: 3, color: '220,240,255' }); }
        s.shake = 10; s.flash = Math.max(s.flash, .3);
        if (B.n) textPop(s, B.x, B.y - 30, `×${B.n}`, '#bfe8ff', 22);
        s.st.fall = { x: fx, k: 0, w: B.r * 1.6 };
        s.st.big = { x: rand(180, 320), y: 64, r: 16, n: 0, k: 0 };
      }
    }
    if (s.st.fall) { s.st.fall.k += dt; if (s.st.fall.k > .7) s.st.fall = null; }
  },
  drawAir(s, g, lv) {
    for (const b of s.st.shots) {
      g.save(); g.globalCompositeOperation = 'lighter';
      b.trail.forEach((p, i) => { g.fillStyle = `rgba(${tierOf(lv).glow},${.35 * (1 - i / 6)})`; g.beginPath(); g.arc(p.x, p.y, 5 - i * .6, 0, TAU); g.fill(); });
      g.restore(); bubbleDraw(g, b.x, b.y, 5 + L5(lv) * .6, s.t, lv);
    }
    const sg = 1 + s.surge * .5;
    for (const c of s.st.cap) {
      const m = c.m, y = c.y - m.z - 4, r = (m.r + 9 + L5(lv) * 1.2) * sg + Math.sin(c.k * 10) * 1.5;
      tierGlow(g, m.x, y, r, lv, s.t);
      bubbleDraw(g, m.x, y, r, s.t + c.k, lv);
    }
    const B = s.st.big;
    if (B) {
      const r = B.r + Math.sin(s.t * 8) * 2;
      shadowAt(g, B.x, B.y + 70, r, .25);
      tierGlow(g, B.x, B.y, r, 6, s.t);
      bubbleDraw(g, B.x, B.y, r, s.t, 6);
      for (let i = 0; i < B.n; i++) { const a = s.t * 2 + i * 2.4, rr = r * (.3 + (i % 3) * .15); const x = B.x + Math.cos(a) * rr, y = B.y + Math.sin(a) * rr; const gr = g.createRadialGradient(x - 1.5, y - 1.5, .5, x, y, 6); gr.addColorStop(0, '#c58af5'); gr.addColorStop(1, '#4c1a85'); g.fillStyle = gr; g.strokeStyle = '#1b0830'; g.lineWidth = 1; g.beginPath(); g.arc(x, y, 5.5, 0, TAU); g.fill(); g.stroke(); }
    }
    const F = s.st.fall;
    if (F) {
      const a = 1 - F.k / .7; g.save(); g.globalCompositeOperation = 'lighter';
      const gr = g.createLinearGradient(F.x - F.w, 0, F.x + F.w, 0); gr.addColorStop(0, 'rgba(80,170,255,0)'); gr.addColorStop(.3, `rgba(120,200,255,${.6 * a})`); gr.addColorStop(.5, `rgba(255,255,255,${.85 * a})`); gr.addColorStop(.7, `rgba(120,200,255,${.6 * a})`); gr.addColorStop(1, 'rgba(80,170,255,0)');
      g.fillStyle = gr; g.fillRect(F.x - F.w, 60, F.w * 2, 80);
      for (let j = 0; j < 3; j++) { g.strokeStyle = `hsla(${hue(s.t, j * 120)},100%,70%,${.5 * a})`; g.lineWidth = 2; g.beginPath(); g.moveTo(F.x - F.w * .6 + j * F.w * .6, 60); g.lineTo(F.x - F.w * .6 + j * F.w * .6, 140); g.stroke(); }
      g.restore();
    }
  },
});

// ---------------------------------------------------------------- 5. 맨홀 간헐천 → 수도관 폭발
const MH = [{ x: 128, y: 140 }, { x: 200, y: 100 }, { x: 185, y: 205 }, { x: 285, y: 160 }, { x: 300, y: 82 }, { x: 250, y: 245 }];
function mhGeyser(s, x, y, p, warn, lv) { s.st.g.push({ x, y, p, k: -warn, warn, lv }); }
function mhStep(s, dt) {
  for (const G of s.st.g) {
    const before = G.k; G.k += dt;
    if (before < 0 && G.k >= 0) {
      const R = 34 * G.p;
      for (const m of s.alive()) { const d = Math.hypot(m.x - G.x, m.y - G.y); if (d < R + m.r) m.launch(s, rand(330, 440) * Math.sqrt(G.p), (m.x - G.x) / (d || 1) * 90, (m.y - G.y) / (d || 1) * 55); }
      splash(s, G.x, G.y, 30 * G.p, 10 + G.lv * 2); hitFx(s, G.x, G.y, G.lv, 1);
      s.shake = Math.max(s.shake, 3 + G.p * 2.5); if (G.lv >= 4) s.flash = Math.max(s.flash, .08);
    }
    if (G.k > 0 && G.k < .5 && Math.random() < dt * (30 + G.lv * 8)) part(s, { kind: 'drop', x: G.x + rand(-8, 8) * G.p, y: G.y, z: rand(40, 120) * G.p, vx: rand(-90, 90), vy: rand(-30, 30), vz: rand(0, 100), grav: 600, life: 1, size: rand(2, 3.5) });
    if (G.k > 0 && G.k < .4) glint(s, G.x, G.y - 60 * G.p, G.lv, Math.random() < .4 ? 1 : 0);
  }
  s.st.g = s.st.g.filter(G => G.k < .8);
}
function drawManholes(s, g, lv) {
  for (const h of MH) {
    const G = s.st.g.find(G => Math.hypot(G.x - h.x, G.y - h.y) < 2);
    const shake = G && G.k < 0 ? rand(-1.5, 1.5) : 0, open = G && G.k >= 0;
    g.fillStyle = '#1a1b20'; g.beginPath(); g.ellipse(h.x, h.y, 17, 9, 0, 0, TAU); g.fill();
    if (open) { const gr = g.createRadialGradient(h.x, h.y, 1, h.x, h.y, 14); gr.addColorStop(0, '#bfe8ff'); gr.addColorStop(1, '#2d6aa8'); g.fillStyle = gr; g.beginPath(); g.ellipse(h.x, h.y, 14, 7, 0, 0, TAU); g.fill(); continue; }
    const gr = g.createLinearGradient(0, h.y - 9, 0, h.y + 9);
    if (lv >= 5) { gr.addColorStop(0, '#fff6c8'); gr.addColorStop(.45, '#ffd25a'); gr.addColorStop(1, '#9a6a0c'); } else { gr.addColorStop(0, '#a3a6b0'); gr.addColorStop(1, '#5d5f68'); }
    g.fillStyle = gr; g.strokeStyle = '#2b2c33'; g.lineWidth = 1.5; g.beginPath(); g.ellipse(h.x + shake, h.y - 1, 15, 8, 0, 0, TAU); g.fill(); g.stroke();
    g.strokeStyle = 'rgba(40,40,48,.6)'; g.lineWidth = 1.2; for (const o of [-7, -2.5, 2.5, 7]) { g.beginPath(); g.moveTo(h.x + shake + o, h.y - 6); g.lineTo(h.x + shake + o, h.y + 4); g.stroke(); }
    if (lv >= 3) { g.save(); g.globalCompositeOperation = 'lighter'; g.strokeStyle = `rgba(${tierOf(lv).glow},${.25 + .2 * Math.sin(s.t * 5 + h.x)})`; g.lineWidth = 2; g.beginPath(); g.ellipse(h.x, h.y, 19, 10, 0, 0, TAU); g.stroke(); g.restore(); }
  }
  for (const G of s.st.g) if (G.k < 0) { const k = 1 + G.k / G.warn; const r = lerp(56, 16, ease(k)) * G.p; g.strokeStyle = G.lv >= 5 ? `rgba(255,214,110,${.4 + .5 * k})` : `rgba(120,210,255,${.4 + .5 * k})`; g.lineWidth = 2.5; g.setLineDash([6, 5]); g.beginPath(); g.ellipse(G.x, G.y, r, r * .55, 0, 0, TAU); g.stroke(); g.setLineDash([]); }
}
function drawMhGeysers(s, g) {
  for (const G of s.st.g) {
    if (G.k < 0) continue;
    const k = G.k, h = 140 * G.p * (k < .12 ? ease(k / .12) : Math.max(0, 1 - (k - .35) / .45)), w = 12 * G.p * (1 + s.surge * .4);
    if (h < 2) continue;
    const T = tierOf(G.lv);
    g.save(); g.globalCompositeOperation = 'lighter';
    const gl = g.createRadialGradient(G.x, G.y - h * .5, 0, G.x, G.y - h * .5, h * .75); gl.addColorStop(0, `rgba(${T.glow},${.3 + T.glowA * .4})`); gl.addColorStop(1, `rgba(${T.glow},0)`); g.fillStyle = gl; g.fillRect(G.x - h, G.y - h * 1.3, h * 2, h * 1.5);
    if (G.lv >= 5) { g.strokeStyle = G.lv >= 6 ? `hsla(${hue(s.t, G.x)},100%,70%,.7)` : 'rgba(255,214,110,.65)'; g.lineWidth = 3; g.beginPath(); g.moveTo(G.x - w * 1.1, G.y); g.quadraticCurveTo(G.x - w * .8, G.y - h * .6, G.x - w * .55, G.y - h); g.moveTo(G.x + w * 1.1, G.y); g.quadraticCurveTo(G.x + w * .8, G.y - h * .6, G.x + w * .55, G.y - h); g.stroke(); }
    g.restore();
    const lg = g.createLinearGradient(G.x - w, 0, G.x + w, 0); lg.addColorStop(0, 'rgba(60,150,245,.9)'); lg.addColorStop(.35, `rgba(${T.core},.95)`); lg.addColorStop(.5, `rgba(255,255,255,${.6 + T.white * .4})`); lg.addColorStop(1, 'rgba(60,140,235,.9)');
    g.fillStyle = lg; g.beginPath(); g.moveTo(G.x - w, G.y); g.quadraticCurveTo(G.x - w * .7, G.y - h * .6, G.x - w * .5, G.y - h); g.lineTo(G.x + w * .5, G.y - h); g.quadraticCurveTo(G.x + w * .7, G.y - h * .6, G.x + w, G.y); g.fill();
    for (let i = 0; i < 6; i++) { const a = i / 6 * TAU + s.t * 6; foamPuff(g, G.x + Math.cos(a) * w * .7, G.y - h + Math.sin(a) * 4, w * .55, 3, s.t); }
    // 날아가는 뚜껑
    const ch = 190 * G.p * Math.sin(Math.PI * clamp(k / .75, 0, 1));
    g.save(); g.translate(G.x + k * 60, G.y - h - 8 - ch * .3); g.rotate(k * 14);
    const cg = g.createLinearGradient(0, -7, 0, 7); if (G.lv >= 5) { cg.addColorStop(0, '#fff6c8'); cg.addColorStop(.45, '#ffd25a'); cg.addColorStop(1, '#9a6a0c'); } else { cg.addColorStop(0, '#a3a6b0'); cg.addColorStop(1, '#5d5f68'); }
    g.fillStyle = cg; g.strokeStyle = '#2b2c33'; g.lineWidth = 1.5; g.beginPath(); g.ellipse(0, 0, 13, 7 * Math.abs(Math.cos(k * 9)) + 1, 0, 0, TAU); g.fill(); g.stroke(); g.restore();
  }
}
item({
  id: 'manhole', group: '무기', name: '맨홀 간헐천', evoName: '수도관 폭발', target: 'house', sides: ['top', 'left', 'topright'],
  lvText: [null, '요괴가 몰린 맨홀에서 물기둥이 솟는다', '맨홀 2곳 · 더 자주', '물기둥이 굵어진다 · 푸른 빛', '맨홀 3곳 · 하늘 높이 날린다', '맨홀 4곳 · 금빛 물기둥', '수도관 폭발: 땅이 갈라지며 물기둥이 도미노로'],
  icon(g, x, y, r, lv, t) {
    g.fillStyle = '#1a1b20'; g.beginPath(); g.ellipse(x, y + r * .55, r, r * .5, 0, 0, TAU); g.fill();
    const lg = g.createLinearGradient(x - r * .5, 0, x + r * .5, 0); lg.addColorStop(0, '#3d8bff'); lg.addColorStop(.5, '#ffffff'); lg.addColorStop(1, '#3d8bff');
    g.fillStyle = lg; g.beginPath(); g.moveTo(x - r * .5, y + r * .5); g.quadraticCurveTo(x - r * .35, y - r * .3, x - r * .25, y - r); g.lineTo(x + r * .25, y - r); g.quadraticCurveTo(x + r * .35, y - r * .3, x + r * .5, y + r * .5); g.fill();
    g.save(); g.translate(x + r * .55, y - r * .7); g.rotate(.6); const cg = g.createLinearGradient(0, -3, 0, 3); cg.addColorStop(0, '#c8cad2'); cg.addColorStop(1, '#5d5f68'); g.fillStyle = cg; g.beginPath(); g.ellipse(0, 0, r * .45, r * .2, 0, 0, TAU); g.fill(); g.restore();
  },
  setup(s) { s.st.g = []; s.st.cd = .6; s.st.cracks = []; s.st.ccd = .4; s.player.x = 100; s.player.y = 240; },
  update(s, dt, lv) {
    const l = L5(lv);
    s.st.cd -= dt;
    if (s.st.cd <= 0) {
      const score = h => s.alive().filter(m => Math.hypot(m.x - h.x, m.y - h.y) < 70).length;
      const busy = h => s.st.g.some(G => Math.hypot(G.x - h.x, G.y - h.y) < 2);
      const sorted = MH.filter(h => !busy(h)).sort((a, b) => score(b) - score(a));
      const n = [0, 1, 2, 2, 3, 4][l], p = [0, 1, 1.1, 1.3, 1.45, 1.8][l];
      for (let i = 0; i < Math.min(n, sorted.length); i++) if (score(sorted[i]) > 0) mhGeyser(s, sorted[i].x, sorted[i].y, p, .4, lv);
      s.st.cd = [0, 1.4, 1.2, .95, .85, .6][l];
    }
    if (lv === 6) {
      s.st.ccd -= dt;
      if (s.st.ccd <= 0) {
        // 맨홀에서 몹 떼 쪽으로 땅이 갈라지며 물기둥 도미노
        const a0 = s.alive();
        for (let c = 0; c < 2; c++) {
          const h = MH[(Math.floor(s.t * 3) + c * 2) % MH.length];
          const tgt = a0.length ? a0[Math.floor(Math.random() * a0.length)] : { x: 300, y: 100 };
          const a = Math.atan2(tgt.y - h.y, tgt.x - h.x), pts = [];
          for (let i = 0; i < 9; i++) { const x = h.x + Math.cos(a) * 30 * i, y = h.y + Math.sin(a) * 30 * i; if (x < 10 || x > W - 10 || y < 10 || y > H - 10) break; pts.push({ x, y }); mhGeyser(s, x, y, 1.15, .22 + i * .08, 6); }
          s.st.cracks.push({ pts, k: 0 });
        }
        s.st.ccd = 1.1;
      }
    }
    mhStep(s, dt);
    for (const c of s.st.cracks) c.k += dt; s.st.cracks = s.st.cracks.filter(c => c.k < 1.4);
  },
  drawGround(s, g, lv) {
    for (const c of s.st.cracks) {
      if (c.pts.length < 2) continue;
      const n = Math.min(c.pts.length, Math.floor(c.k / .08) + 1), a = 1 - c.k / 1.4;
      g.save(); g.lineJoin = 'round'; g.lineCap = 'round';
      g.strokeStyle = `rgba(30,20,10,${.85 * a})`; g.lineWidth = 6; g.beginPath(); g.moveTo(c.pts[0].x, c.pts[0].y); for (let i = 1; i < n; i++) g.lineTo(c.pts[i].x + (i % 2 ? 4 : -4), c.pts[i].y + (i % 2 ? -3 : 3)); g.stroke();
      g.globalCompositeOperation = 'lighter'; g.strokeStyle = `hsla(${hue(s.t)},100%,70%,${.8 * a})`; g.lineWidth = 2.5; g.stroke();
      g.restore();
    }
    drawManholes(s, g, lv);
  },
  drawAir(s, g) { drawMhGeysers(s, g); },
});

// ---------------------------------------------------------------- 6. 물 사슬 → 해일 사슬
function chainPick(s, x, y, hops, skip) {
  const pts = [{ x, y }]; let cx = x, cy = y;
  for (let i = 0; i < hops; i++) { const m = s.nearest(cx, cy, i ? 100 : 270, skip); if (!m) break; skip.add(m); pts.push({ x: m.x, y: m.y - 4, m }); cx = m.x; cy = m.y; }
  return pts;
}
function jagged(p0, p1, seg = 6, amp = 7) { const out = [p0]; for (let i = 1; i < seg; i++) { const u = i / seg, nx = -(p1.y - p0.y), ny = p1.x - p0.x, L = Math.hypot(nx, ny) || 1, o = rand(-amp, amp); out.push({ x: lerp(p0.x, p1.x, u) + nx / L * o, y: lerp(p0.y, p1.y, u) + ny / L * o }); } out.push(p1); return out; }
function drawBolt(g, sg, a, lv, t, wk) {
  const T = tierOf(lv);
  const outer = lv >= 6 ? `hsla(${hue(t)},100%,65%,${.45 * a})` : lv >= 5 ? `rgba(255,190,70,${.45 * a})` : `rgba(${T.glow},${(.25 + T.glowA * .3) * a})`;
  const mid = lv >= 5 ? `rgba(255,230,150,${.8 * a})` : `rgba(${T.core},${.75 * a})`;
  g.save(); g.globalCompositeOperation = 'lighter'; g.lineCap = 'round'; g.lineJoin = 'round';
  for (const [w, c] of [[14 * wk, outer], [7 * wk, mid], [2.5 * wk, `rgba(255,255,255,${a})`]]) { g.strokeStyle = c; g.lineWidth = w; g.beginPath(); g.moveTo(sg[0].x, sg[0].y); for (const p of sg) g.lineTo(p.x, p.y); g.stroke(); }
  g.restore();
}
item({
  id: 'chain', group: '무기', name: '물 사슬', evoName: '해일 사슬', target: 'house', sides: ['top', 'topright'],
  lvText: [null, '물줄기가 요괴 3마리를 번개처럼 튄다', '4마리 · 더 굵게', '두 줄기 · 하얀 심', '5마리 · 더 빠르게', '6마리 · 금빛 번개 · 끝에서 물 폭발', '해일 사슬: 세 줄기 · 맞은 요괴마다 물이 터진다'],
  icon(g, x, y, r, lv, t) {
    const pts = [{ x: x - r * .9, y: y - r * .8 }, { x: x - r * .1, y: y - r * .15 }, { x: x - r * .35, y: y + r * .1 }, { x: x + r * .9, y: y + r * .85 }];
    g.save(); g.globalCompositeOperation = 'lighter'; g.lineJoin = 'round'; g.lineCap = 'round';
    for (const [w, c] of [[r * .7, 'rgba(60,150,255,.5)'], [r * .35, lv >= 5 ? 'rgba(255,220,130,.95)' : 'rgba(130,205,255,.95)'], [r * .13, '#fff']]) { g.strokeStyle = c; g.lineWidth = w; g.beginPath(); pts.forEach((p, i) => i ? g.lineTo(p.x, p.y) : g.moveTo(p.x, p.y)); g.stroke(); }
    g.restore();
  },
  setup(s) { s.st.bolts = []; s.st.cd = .3; s.player.x = 150; s.player.y = 170; },
  update(s, dt, lv) {
    const P = s.player;
    s.st.cd -= dt;
    if (s.st.cd <= 0) {
      const skip = new Set(), chains = [0, 1, 1, 2, 2, 2, 3][lv], hops = [0, 3, 4, 4, 5, 6, 8][lv], dmg = [0, 3, 3.4, 3.8, 4.2, 5, 6][lv];
      for (let c = 0; c < chains; c++) {
        const pts = chainPick(s, P.x + 6, P.y - 10, hops, skip); if (pts.length < 2) continue;
        const segs = []; for (let i = 0; i + 1 < pts.length; i++) segs.push(jagged(pts[i], pts[i + 1], 6, 6 + lv));
        s.st.bolts.push({ segs, k: 0, lv });
        pts.slice(1).forEach((p, i) => {
          p.m.hit(s, dmg, rand(-40, 40), rand(-40, 40)); hitFx(s, p.x, p.y, lv, .6);
          if (lv === 6) { splash(s, p.x, p.y, 34, 10); s.hitArea(p.x, p.y, 34, 4, 160); part(s, { kind: 'prism', x: p.x, y: p.y, life: .35, size: 34 }); }
          else if (lv === 5 && i === pts.length - 2) { splash(s, p.x, p.y, 30, 10); s.hitArea(p.x, p.y, 30, 3, 140); }
        });
      }
      s.shake = Math.max(s.shake, [0, 1.5, 2, 2.5, 3, 4, 6][lv]);
      s.st.cd = [0, .95, .9, .85, .75, .65, .5][lv];
    }
    for (const b of s.st.bolts) { b.k += dt; if (Math.random() < .5) b.segs = b.segs.map(sg => jagged(sg[0], sg[sg.length - 1], 6, 6 + b.lv)); }
    s.st.bolts = s.st.bolts.filter(b => b.k < .45);
  },
  drawAir(s, g, lv) {
    const P = s.player, wk = tierOf(lv).scale * .75 * (1 + s.surge * .6);
    tierGlow(g, P.x + 6, P.y - 10, 7, lv, s.t);
    for (const b of s.st.bolts) { const a = 1 - b.k / .45; for (const sg of b.segs) drawBolt(g, sg, a, b.lv, s.t, wk); }
  },
});
}
