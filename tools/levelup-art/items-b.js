'use strict';
// 물풍선(+그림 후보 3안)·소화기 부메랑·액체질소 지뢰. 짜임은 items-a.js를 따른다.

// ============================================================================ 물풍선 그림 3안
// A(채택): 투명한 파란 고무 풍선 — 안에 물이 출렁이고(기울어진 수면), 묶은 매듭 + 짧은 끈, 하이라이트 두 점.
function balloonA(g, x, y, r, ang, stretch, slosh, lv, t) {
  const T = tierOf(lv || 1);
  g.save(); g.translate(x, y);
  // 늘어남(진행 방향)·찌그러짐
  g.rotate(ang); g.scale(stretch, 1 / stretch); g.rotate(-ang);
  // 등급 빛
  if (lv >= 3) { g.save(); g.globalCompositeOperation = 'lighter'; const gl = g.createRadialGradient(0, 0, r * .6, 0, 0, r * 2); gl.addColorStop(0, `rgba(${T.glow},${T.glowA * .8})`); gl.addColorStop(1, `rgba(${T.glow},0)`); g.fillStyle = gl; g.beginPath(); g.arc(0, 0, r * 2, 0, TAU); g.fill(); g.restore(); }
  // 몸(살짝 물방울 모양: 아래가 무겁다)
  const body = () => { g.beginPath(); g.moveTo(0, -r * 1.05); g.bezierCurveTo(r * 1.05, -r * 1.05, r * 1.1, r * .55, 0, r * 1.02); g.bezierCurveTo(-r * 1.1, r * .55, -r * 1.05, -r * 1.05, 0, -r * 1.05); g.closePath(); };
  // 고무(반투명)
  const rub = g.createRadialGradient(-r * .35, -r * .45, r * .1, 0, 0, r * 1.15);
  rub.addColorStop(0, 'rgba(225,245,255,.55)'); rub.addColorStop(.55, 'rgba(90,175,255,.35)'); rub.addColorStop(1, 'rgba(30,95,210,.65)');
  body(); g.fillStyle = rub; g.fill();
  // 안의 물: 몸 모양으로 자르고 수면을 기울여 출렁이게
  g.save(); body(); g.clip();
  const tilt = Math.sin(slosh) * .5, lvl = -r * .05;
  const wg = g.createLinearGradient(0, lvl - r * .3, 0, r);
  wg.addColorStop(0, 'rgba(120,210,255,.95)'); wg.addColorStop(1, 'rgba(20,90,200,.95)');
  g.fillStyle = wg; g.beginPath(); g.moveTo(-r * 1.3, lvl + tilt * r);
  for (let i = 0; i <= 8; i++) { const u = i / 8, xx = -r * 1.3 + u * r * 2.6; g.lineTo(xx, lvl + tilt * r * (1 - 2 * u) + Math.sin(u * 9 + slosh * 3) * r * .06); }
  g.lineTo(r * 1.3, r * 1.3); g.lineTo(-r * 1.3, r * 1.3); g.closePath(); g.fill();
  // 수면 빛줄
  g.strokeStyle = 'rgba(230,250,255,.85)'; g.lineWidth = Math.max(1, r * .09); g.beginPath();
  for (let i = 0; i <= 8; i++) { const u = i / 8, xx = -r * 1.3 + u * r * 2.6, yy = lvl + tilt * r * (1 - 2 * u) + Math.sin(u * 9 + slosh * 3) * r * .06; i ? g.lineTo(xx, yy) : g.moveTo(xx, yy); } g.stroke();
  // 물 속 기포
  g.fillStyle = 'rgba(255,255,255,.55)'; for (let i = 0; i < 3; i++) { g.beginPath(); g.arc(-r * .3 + i * r * .32, r * .45 - ((t * 1.5 + i * .33) % 1) * r * .4, r * .07, 0, TAU); g.fill(); }
  g.restore();
  // 테두리
  body(); g.lineWidth = Math.max(1.2, r * .13);
  g.strokeStyle = lv >= 6 ? `hsl(${hue(t)},95%,70%)` : lv >= 5 ? '#ffd25a' : 'rgba(15,60,140,.9)'; g.stroke();
  // 매듭 + 끈
  g.fillStyle = lv >= 5 ? '#e0a020' : '#2a6fd0'; g.strokeStyle = 'rgba(10,40,100,.9)'; g.lineWidth = 1;
  g.beginPath(); g.moveTo(-r * .2, r * 1.0); g.lineTo(r * .2, r * 1.0); g.lineTo(r * .12, r * 1.25); g.lineTo(-r * .12, r * 1.25); g.closePath(); g.fill(); g.stroke();
  g.strokeStyle = 'rgba(240,240,250,.85)'; g.lineWidth = Math.max(.8, r * .08); g.beginPath(); g.moveTo(0, r * 1.25); g.quadraticCurveTo(r * .35 + Math.sin(t * 9) * r * .2, r * 1.55, -r * .05, r * 1.85); g.stroke();
  // 하이라이트 두 점
  g.fillStyle = 'rgba(255,255,255,.95)'; g.beginPath(); g.ellipse(-r * .4, -r * .5, r * .24, r * .14, -.7, 0, TAU); g.fill();
  g.fillStyle = 'rgba(255,255,255,.8)'; g.beginPath(); g.arc(-r * .12, -r * .72, r * .08, 0, TAU); g.fill();
  g.restore();
}
// B: 꼭지가 위로 묶인 전통 물폭탄 — 불투명 광택 고무, 꼭지 주름.
function balloonB(g, x, y, r, ang, stretch, slosh, lv, t) {
  g.save(); g.translate(x, y); g.rotate(ang); g.scale(stretch, 1 / stretch); g.rotate(-ang);
  const gr = g.createRadialGradient(-r * .35, -r * .3, r * .1, 0, r * .1, r * 1.1); gr.addColorStop(0, '#bfe6ff'); gr.addColorStop(.4, '#3e9cf5'); gr.addColorStop(1, '#174f9e');
  g.fillStyle = gr; g.strokeStyle = '#0b2f66'; g.lineWidth = r * .12;
  g.beginPath(); g.moveTo(-r * .18, -r * .92); g.bezierCurveTo(-r * 1.2, -r * .7, -r * 1.1, r * 1.05, 0, r * 1.02); g.bezierCurveTo(r * 1.1, r * 1.05, r * 1.2, -r * .7, r * .18, -r * .92); g.closePath(); g.fill(); g.stroke();
  // 꼭지 주름
  g.fillStyle = '#2a7ee0'; g.beginPath(); g.moveTo(-r * .22, -r * .9); g.lineTo(0, -r * 1.35); g.lineTo(r * .22, -r * .9); g.closePath(); g.fill(); g.stroke();
  g.strokeStyle = 'rgba(10,40,100,.6)'; g.lineWidth = 1; for (const o of [-.08, .08]) { g.beginPath(); g.moveTo(r * o, -r * .95); g.lineTo(r * o * .3, -r * 1.25); g.stroke(); }
  g.fillStyle = 'rgba(255,255,255,.9)'; g.beginPath(); g.ellipse(-r * .42, -r * .2, r * .16, r * .3, .3, 0, TAU); g.fill();
  g.restore();
}
// C: 유리 물방울 구슬 — 맑은 물 구 안에 소용돌이, 무지개 테.
function balloonC(g, x, y, r, ang, stretch, slosh, lv, t) {
  g.save(); g.translate(x, y); g.rotate(ang); g.scale(stretch, 1 / stretch); g.rotate(-ang);
  const gr = g.createRadialGradient(-r * .3, -r * .35, r * .05, 0, 0, r); gr.addColorStop(0, 'rgba(255,255,255,.9)'); gr.addColorStop(.5, 'rgba(110,200,255,.55)'); gr.addColorStop(1, 'rgba(20,110,220,.85)');
  g.fillStyle = gr; g.beginPath(); g.arc(0, 0, r, 0, TAU); g.fill();
  g.save(); g.beginPath(); g.arc(0, 0, r, 0, TAU); g.clip(); g.strokeStyle = 'rgba(230,250,255,.7)'; g.lineWidth = r * .14;
  for (let i = 0; i < 2; i++) { g.beginPath(); g.arc(0, r * .1, r * (.35 + i * .25), slosh + i * 2, slosh + i * 2 + 2.6); g.stroke(); } g.restore();
  g.lineWidth = r * .12; g.strokeStyle = `hsla(${hue(t)},90%,75%,.9)`; g.beginPath(); g.arc(0, 0, r, 0, TAU); g.stroke();
  g.fillStyle = 'rgba(255,255,255,.95)'; g.beginPath(); g.ellipse(-r * .38, -r * .45, r * .22, r * .12, -.6, 0, TAU); g.fill();
  g.restore();
}

// 고무 조각(터질 때) — 이 파일 안에서만 쓰는 조각 목록
function rubberBurst(s, x, y, r, lv) {
  s.st.shreds = s.st.shreds || [];
  const n = 7 + lv;
  for (let i = 0; i < n; i++) { const a = rand(0, TAU), v = rand(90, 200); s.st.shreds.push({ x, y, z: 6, vx: Math.cos(a) * v, vy: Math.sin(a) * v * .7, vz: rand(80, 200), rot: rand(0, TAU), vr: rand(-14, 14), k: 0, sz: r * rand(.35, .6), gold: lv >= 5 }); }
  splash(s, x, y, r * 3.4, 14 + lv * 2);
  part(s, { kind: 'glow', x, y, life: .3, size: r * 4, color: tierOf(lv).glow });
}
function stepShreds(s, dt) {
  for (const p of s.st.shreds || []) { p.k += dt; p.x += p.vx * dt; p.y += p.vy * dt; p.z += p.vz * dt; p.vz -= 600 * dt; if (p.z < 0) { p.z = 0; p.vz *= -.3; p.vx *= .5; p.vy *= .5; } p.rot += p.vr * dt; }
  if (s.st.shreds) s.st.shreds = s.st.shreds.filter(p => p.k < .9);
}
function drawShreds(s, g) {
  for (const p of s.st.shreds || []) {
    const a = 1 - p.k / .9; g.save(); g.translate(p.x, p.y - p.z); g.rotate(p.rot); g.globalAlpha = a;
    const gr = g.createLinearGradient(-p.sz, 0, p.sz, 0); gr.addColorStop(0, p.gold ? '#ffe9a0' : '#9fd8ff'); gr.addColorStop(1, p.gold ? '#c58a12' : '#1d63c4');
    g.fillStyle = gr; g.beginPath(); g.moveTo(-p.sz, 0); g.quadraticCurveTo(0, -p.sz * .9, p.sz, 0); g.quadraticCurveTo(0, -p.sz * .3, -p.sz, 0); g.fill();
    g.restore();
  }
}

// ============================================================================ 3. 물풍선 → 물풍선 폭우
const BAL = { n: [0, 1, 2, 2, 3, 3, 3], bounce: [0, 4, 5, 6, 7, 8, 8], r: [0, 10, 11, 12.5, 13.5, 15, 15], dmg: [0, 2, 2.4, 3, 3.6, 4.5, 4.5], R: [0, 24, 28, 33, 38, 46, 46], cd: [0, 1.5, 1.4, 1.25, 1.1, 1, 1], speed: [0, 230, 245, 260, 275, 290, 290] };
function mkBal(s, lv, a, small) {
  const P = s.player, v = BAL.speed[lv];
  return { x: P.x, y: P.y - 10, vx: Math.cos(a) * v, vy: Math.sin(a) * v, b: 0, max: small ? 2 : BAL.bounce[lv], r: small ? BAL.r[lv] * .7 : BAL.r[lv], sq: 0, slosh: rand(0, 6), lv, split: lv >= 6 && !small, trail: [] };
}
function balStep(s, b, dt) {
  b.x += b.vx * dt; b.y += b.vy * dt; b.sq = Math.max(0, b.sq - dt * 4); b.slosh += dt * (6 + b.sq * 20);
  b.trail.unshift({ x: b.x, y: b.y }); if (b.trail.length > 7) b.trail.pop();
  let hit = null;
  if (b.x < 12 || b.x > W - 12) { b.vx *= -1; b.x = clamp(b.x, 12, W - 12); hit = 'wall'; }
  if (b.y < 14 || b.y > H - 12) { b.vy *= -1; b.y = clamp(b.y, 14, H - 12); hit = 'wall'; }
  if (b.x > HOUSE.x - 6 && b.x < HOUSE.x + HOUSE.w + 6 && b.y > HOUSE.y - 6 && b.y < HOUSE.y + HOUSE.h + 6) { b.vy *= -1; b.y += b.vy * dt * 2; hit = 'wall'; }
  for (const m of s.alive()) if (Math.hypot(m.x - b.x, m.y - b.y) < m.r + b.r && !(m.cd.bl > s.t)) { m.cd.bl = s.t + .2; hit = 'mob'; const a = Math.atan2(m.y - b.y, m.x - b.x), v = Math.hypot(b.vx, b.vy); b.vx = -Math.cos(a + rand(-.4, .4)) * v; b.vy = -Math.sin(a + rand(-.4, .4)) * v; break; }
  if (hit) {
    b.b++; b.sq = 1;
    const lv = b.lv, R = BAL.R[lv] * (b.max === 2 ? .75 : 1);
    s.hitArea(b.x, b.y, R, BAL.dmg[lv], 100 + lv * 20);
    splash(s, b.x, b.y, R, 6 + lv * 2); hitFx(s, b.x, b.y, lv, .7);
    if (lv >= 5) { part(s, { kind: 'ring', x: b.x, y: b.y, life: .35, size: R * 1.8, color: lv >= 6 ? '255,255,255' : '255,215,110', width: 4 }); s.shake = Math.max(s.shake, 2.5); }
    // 최고급: 튕길 때마다 작은 풍선 둘로 갈라진다
    if (b.split && (s.st.b.length < 26)) for (const o of [-.6, .6]) { const nb = mkBal(s, lv, Math.atan2(b.vy, b.vx) + o, true); nb.x = b.x; nb.y = b.y; s.st.b.push(nb); }
  }
  if (b.b >= b.max) { rubberBurst(s, b.x, b.y, b.r, b.lv); s.hitArea(b.x, b.y, BAL.R[b.lv] * 1.3, BAL.dmg[b.lv], 160); return false; }
  return true;
}
function drawBal(s, g, b, draw = balloonA) {
  const sp = Math.hypot(b.vx, b.vy), ang = Math.atan2(b.vy, b.vx);
  shadowAt(g, b.x, b.y + b.r + 6, b.r * .9, .25);
  // 물방울 꼬리
  g.save(); g.globalCompositeOperation = 'lighter';
  for (let i = 1; i < b.trail.length; i++) { const p = b.trail[i], k = 1 - i / b.trail.length; g.fillStyle = `rgba(${tierOf(b.lv).glow},${.45 * k})`; g.beginPath(); g.arc(p.x, p.y - 6, b.r * .55 * k, 0, TAU); g.fill(); }
  g.restore();
  const stretch = b.sq > 0 ? 1 - b.sq * .4 : 1 + Math.min(.18, sp / 1600);
  draw(g, b.x, b.y - 6 + Math.sin(s.t * 9 + b.slosh) * 1.2, b.r * (1 + s.surge * .5), ang, stretch, b.slosh, b.lv, s.t);
}
item({
  id: 'balloon', group: '무기', name: '물풍선', evoName: '물풍선 폭우', target: 'house', sides: ['top', 'topright'],
  lvText: [null, '물풍선 1개 · 4번 튕긴다', '물풍선 2개 · 5번', '더 큰 풍선 · 6번 튕긴다', '물풍선 3개 · 7번', '금빛 풍선 · 튕길 때마다 물보라 폭발', '물풍선 폭우: 하늘에서 쏟아지고 튕길 때마다 둘로 갈라진다'],
  icon(g, x, y, r, lv, t) { balloonA(g, x, y - r * .1, r * .72, 0, 1, t * 3, lv, t); },
  setup(s) { s.st.b = []; s.st.cd = .3; s.st.rain = []; s.st.rc = 0; s.player.x = 150; s.player.y = 170; },
  onLevel(s, lv) { s.st.cd = 0; },
  update(s, dt, lv) {
    s.st.cd -= dt;
    if (s.st.cd <= 0) {
      const c = s.crowd(), a = c ? Math.atan2(c.y - s.player.y, c.x - s.player.x) : -1.2, n = BAL.n[lv];
      for (let i = 0; i < n; i++) s.st.b.push(mkBal(s, lv, a + (i - (n - 1) / 2) * .55, false));
      s.st.cd = BAL.cd[lv];
    }
    s.st.b = s.st.b.filter(b => balStep(s, b, dt));
    stepShreds(s, dt);
    if (lv < 6) return;
    // 폭우: 몹 위에 그림자가 먼저 깔리고 풍선이 떨어져 터진다.
    s.st.rc -= dt;
    while (s.st.rc <= 0) { const al = s.alive(); if (al.length) { const m = al[Math.floor(Math.random() * al.length)]; s.st.rain.push({ x: m.x + rand(-12, 12), y: m.y + rand(-8, 8), k: 0, sl: rand(0, 6) }); } s.st.rc += .06; }
    for (const r of s.st.rain) { r.k += dt; if (r.k >= .45 && !r.done) { r.done = 1; rubberBurst(s, r.x, r.y, 11, 6); s.hitArea(r.x, r.y, 40, 6, 160); hitFx(s, r.x, r.y, 6, .5); s.shake = Math.max(s.shake, 3); } }
    s.st.rain = s.st.rain.filter(r => !r.done);
  },
  drawGround(s, g, lv) {
    for (const r of s.st.rain || []) { const k = r.k / .45; g.save(); g.fillStyle = `rgba(20,40,90,${.12 + .3 * k})`; g.beginPath(); g.ellipse(r.x, r.y, 6 + 12 * k, (6 + 12 * k) * .45, 0, 0, TAU); g.fill();
      g.globalCompositeOperation = 'lighter'; g.strokeStyle = `hsla(${hue(s.t, r.x)},100%,70%,${.5 * k})`; g.lineWidth = 1.5; g.beginPath(); g.ellipse(r.x, r.y, 20 - 10 * k, (20 - 10 * k) * .45, 0, 0, TAU); g.stroke(); g.restore(); }
  },
  drawAir(s, g, lv) {
    drawShreds(s, g);
    for (const b of s.st.b) drawBal(s, g, b);
    for (const r of s.st.rain || []) { const z = 260 * (1 - ease(r.k / .45)); drawBal(s, g, { x: r.x, y: r.y - z, vx: 0, vy: 400, b: 0, r: 14, sq: 0, slosh: r.sl + s.t * 5, lv: 6, trail: [] }); }
  },
});

// ---------------------------------------------------------------- 물풍선 그림 후보(3안 나란히)
item({
  id: 'balloon_pick', group: '후보', tab: '물풍선 그림 후보', name: '물풍선 그림 후보', evoName: '',
  icon(g, x, y, r, lv, t) { balloonA(g, x, y, r * .7, 0, 1, t * 3, 1, t); },
  custom() {
    const DRAW = [balloonA, balloonB, balloonC], NAME = ['A · 투명 고무 + 출렁이는 물 (채택)', 'B · 꼭지 묶은 물폭탄', 'C · 유리 물방울 구슬'];
    const o = {
      item: this, lv: 0, t: 0, dur: 9,
      reset() { this.t = 0; this.lanes = DRAW.map((d, i) => ({ s: { st: { shreds: [] }, parts: [], t: 0, surge: 0, shake: 0 }, b: null, cd: .2 * i, cx: 80 + i * 160 })); },
      update(dt) {
        this.t += dt; if (this.t > this.dur) this.reset();
        for (const L of this.lanes) {
          const s = L.s; s.t = this.t;
          L.cd -= dt;
          if (!L.b && L.cd <= 0) L.b = { x: L.cx - 50, y: 220, vx: 120, vy: -260, b: 0, r: 16, sq: 0, slosh: 0, lv: 1, trail: [] };
          const b = L.b;
          if (b) {
            b.x += b.vx * dt; b.y += b.vy * dt; b.vy += 260 * dt; b.sq = Math.max(0, b.sq - dt * 4); b.slosh += dt * (5 + b.sq * 20);
            b.trail.unshift({ x: b.x, y: b.y }); if (b.trail.length > 7) b.trail.pop();
            if (b.x < L.cx - 66 || b.x > L.cx + 66) { b.vx *= -1; b.x = clamp(b.x, L.cx - 66, L.cx + 66); b.sq = 1; b.b++; splash(s, b.x, b.y, 22, 8); }
            if (b.y > 225) { b.vy = -Math.abs(b.vy) * .9; b.y = 225; b.sq = 1; b.b++; splash(s, b.x, b.y, 26, 10); }
            if (b.b >= 6) { rubberBurst(s, b.x, b.y, b.r, 1); L.b = null; L.cd = 1.2; }
          }
          stepParts(s, dt); stepShreds(s, dt);
        }
      },
      draw(g) {
        const bgr = g.createLinearGradient(0, 0, 0, H); bgr.addColorStop(0, '#1a2a44'); bgr.addColorStop(1, '#0d1626'); g.fillStyle = bgr; g.fillRect(0, 0, W, H); g.fillStyle = 'rgba(60,120,80,.35)'; g.fillRect(0, 232, W, H - 232);
        for (let i = 0; i < 3; i++) {
          const L = this.lanes[i];
          g.fillStyle = 'rgba(255,255,255,.06)'; g.strokeStyle = i === 0 ? 'rgba(255,214,110,.7)' : 'rgba(150,200,255,.25)'; g.lineWidth = 1.5; g.beginPath(); g.roundRect(L.cx - 76, 36, 152, 206, 10); g.fill(); g.stroke();
          // 정지 크게 보기
          DRAW[i](g, L.cx, 82, 22, 0, 1, this.t * 3, 1, this.t);
          drawShreds(L.s, g);
          if (L.b) drawBal(L.s, g, L.b, DRAW[i]);
          drawParts(L.s, g, 'mid');
          g.font = '800 11px "Apple SD Gothic Neo",sans-serif'; g.textAlign = 'center'; g.fillStyle = i === 0 ? '#ffe9a8' : '#e8f4ff'; g.fillText(NAME[i], L.cx, 254);
        }
        metalText(g, '물풍선 그림 후보 — 마음에 드는 걸 골라 주세요', W / 2, 20, 14, 'silver', this.t);
      },
    };
    o.reset(); return o;
  },
});

// ============================================================================ 4. 소화기 부메랑 → 분말 회오리
function drawExtLv(g, x, y, a, lv, t, sc = 1) {
  g.save(); g.translate(x, y); g.scale(sc, sc);
  tierGlow(g, 0, 0, 10, lv, t);
  g.rotate(a);
  const gr = g.createLinearGradient(-5, 0, 5, 0);
  if (lv >= 5) { gr.addColorStop(0, '#fff2b8'); gr.addColorStop(.5, '#e2a12a'); gr.addColorStop(1, '#8a5c0e'); }
  else { gr.addColorStop(0, '#ff7a6a'); gr.addColorStop(.5, '#e2241b'); gr.addColorStop(1, '#8c120d'); }
  g.fillStyle = gr; g.strokeStyle = lv >= 5 ? '#4a3005' : '#3b0805'; g.lineWidth = 1.5; g.beginPath(); g.roundRect(-5, -10, 10, 20, 4); g.fill(); g.stroke();
  g.fillStyle = 'rgba(255,255,255,.55)'; g.fillRect(-3.5, -8, 1.6, 15);
  g.fillStyle = '#2b2b33'; g.fillRect(-3, -14, 6, 4);
  const lb = g.createLinearGradient(-6, -2, 6, 2); lb.addColorStop(0, '#f4f4f8'); lb.addColorStop(1, '#b8bcc8'); g.fillStyle = lb; g.fillRect(-6, -2, 12, 4);
  g.strokeStyle = '#222'; g.lineWidth = 2; g.beginPath(); g.moveTo(2, -13); g.quadraticCurveTo(10, -16, 9, -6); g.stroke();
  g.restore();
}
const EXT = { n: [0, 1, 1, 2, 2, 3, 4], R: [0, 105, 125, 140, 160, 185, 185], dmg: [0, 2.2, 2.6, 3, 3.4, 3.8, 4.2], hitR: [0, 13, 15, 17, 19, 22, 22], cd: [0, 1.3, 1.2, 1.15, 1.05, 1, 1], spd: [0, 1.15, 1.1, 1.05, 1, .95, .95] };
function mkExt(s, a, lv) { return { a, k: 0, lv, x: s.player.x, y: s.player.y, spin: 0, trail: [] }; }
function extStep(s, b, dt) {
  const lv = b.lv, R = EXT.R[lv];
  b.k += dt / EXT.spd[lv]; b.spin += dt * (16 + lv * 3);
  const out = Math.sin(Math.PI * clamp(b.k, 0, 1)) * R, side = Math.sin(TAU * b.k) * R * .35;
  b.x = s.player.x + Math.cos(b.a) * out - Math.sin(b.a) * side; b.y = s.player.y - 6 + Math.sin(b.a) * out + Math.cos(b.a) * side;
  b.trail.unshift({ x: b.x, y: b.y }); if (b.trail.length > 6 + lv * 2) b.trail.pop();
  if (Math.random() < dt * (22 + Math.min(lv, 5) * 4)) part(s, { kind: 'powder', x: b.x + rand(-4, 4), y: b.y + rand(-4, 4), vx: rand(-15, 15), vy: rand(-15, 15), life: .45 + Math.min(lv, 5) * .05, size: rand(3, 3.5 + Math.min(lv, 5) * .6) });
  for (const m of s.alive()) if (Math.hypot(m.x - b.x, m.y - b.y) < m.r + EXT.hitR[lv] && !(m.cd['bm' + b.a] > s.t)) {
    m.cd['bm' + b.a] = s.t + .25; const L = Math.hypot(m.x - b.x, m.y - b.y) || 1; m.hit(s, EXT.dmg[lv], (m.x - b.x) / L * (60 + lv * 25), (m.y - b.y) / L * (60 + lv * 25));
    for (let i = 0; i < 2 + (lv >> 1); i++) part(s, { kind: 'powder', x: m.x, y: m.y, vx: rand(-60, 60), vy: rand(-60, 60), life: .4, size: 3.5 + Math.min(lv, 5) * .4 });
    hitFx(s, m.x, m.y - 4, lv, .5);
  }
  return b.k < 1;
}
item({
  id: 'extinguisher', group: '무기', name: '소화기 부메랑', evoName: '분말 회오리', target: 'house', sides: ['top', 'topright', 'left'],
  lvText: [null, '소화기 1개가 나갔다 돌아온다', '더 멀리 · 더 세게', '소화기 2개', '더 멀리 · 분말 꼬리가 길게', '금빛 소화기 3개 · 9칸까지', '분말 회오리: 떠도는 하얀 회오리가 요괴를 빨아올린다'],
  icon(g, x, y, r, lv, t) { drawExtLv(g, x, y, .5, lv, t, r / 12); },
  setup(s) { s.st.b = []; s.st.cd = .2; s.player.x = 200; s.player.y = 165; },
  onLevel(s, lv) { if (lv === 6) s.st.tw = { x: 240, y: 100, vx: 0, vy: 0 }; s.st.cd = 0; },
  update(s, dt, lv) {
    s.st.cd -= dt;
    if (s.st.cd <= 0) {
      const c = s.crowd(), a = c ? Math.atan2(c.y - s.player.y, c.x - s.player.x) : -1.4, n = EXT.n[lv];
      for (let i = 0; i < n; i++) s.st.b.push(mkExt(s, a + (n === 1 ? 0 : (i - (n - 1) / 2) * (TAU / n) * .55), lv));
      s.st.cd = EXT.cd[lv];
    }
    s.st.b = s.st.b.filter(b => extStep(s, b, dt));
    if (lv < 6 || !s.st.tw) return;
    // 회오리: 몹 떼를 쫓아 떠돌며 빨아들이고 하늘로 띄운다.
    const T = s.st.tw, c = s.crowd() || { x: 260, y: 120 };
    T.vx += ((c.x - T.x) * 1.2 - T.vx) * dt; T.vy += ((c.y - T.y) * 1.2 - T.vy) * dt; T.x = clamp(T.x + T.vx * dt, 130, W - 70); T.y = clamp(T.y + T.vy * dt, 90, H - 30);
    for (const m of s.alive()) {
      const dx = T.x - m.x, dy = T.y - m.y, L = Math.hypot(dx, dy) || 1;
      if (L < 200) { const k = 1 - L / 200; m.x += (dx / L * 200 - dy / L * 200) * k * dt; m.y += (dy / L * 200 + dx / L * 200) * k * dt; if (L < 44) { m.launch(s, rand(320, 420), rand(-120, 120), rand(-60, 60)); part(s, { kind: 'ring', x: m.x, y: m.y, life: .3, size: 24, color: '255,255,255', width: 3 }); } }
    }
    for (let i = 0; i < 2; i++) { const a = rand(0, TAU), r = rand(10, 60); part(s, { kind: 'powder', x: T.x + Math.cos(a) * r, y: T.y + Math.sin(a) * r * .4, vz: rand(40, 120), life: .8, size: rand(3, 7) }); }
    s.shake = Math.max(s.shake, 1.5);
  },
  drawAir(s, g, lv) {
    for (const b of s.st.b) {
      // 잔상 꼬리
      g.save(); g.globalCompositeOperation = 'lighter';
      for (let i = 1; i < b.trail.length; i++) { const p = b.trail[i], k = 1 - i / b.trail.length; g.strokeStyle = lv >= 5 ? `rgba(255,220,140,${.4 * k})` : `rgba(255,255,255,${.35 * k})`; g.lineWidth = (4 + lv * 1.5) * k; g.lineCap = 'round'; g.beginPath(); g.moveTo(b.trail[i - 1].x, b.trail[i - 1].y); g.lineTo(p.x, p.y); g.stroke(); }
      g.restore();
      shadowAt(g, b.x, b.y + 14, 7, .25);
      drawExtLv(g, b.x, b.y, b.spin, lv, s.t, (1 + (Math.min(lv, 5) - 1) * .12) * (1 + s.surge * .6));
    }
    if (lv < 6 || !s.st.tw) return;
    const T = s.st.tw;
    shadowAt(g, T.x, T.y + 6, 56, .32);
    g.save(); g.lineCap = 'round';
    for (let i = 0; i < 12; i++) {
      const h = i * 13, r = 14 + i * 6.5, a = s.t * 10 + i * .8, wob = Math.sin(s.t * 3 + i * .6) * 8;
      g.globalCompositeOperation = 'lighter';
      g.strokeStyle = `hsla(${hue(s.t, i * 30)},100%,75%,${.35})`; g.lineWidth = 10 - i * .4; g.beginPath(); g.ellipse(T.x + wob, T.y - h, r, r * .3, 0, a % TAU, a % TAU + 4.4); g.stroke();
      g.globalCompositeOperation = 'source-over';
      g.strokeStyle = `rgba(250,252,255,${.9 - i * .04})`; g.lineWidth = 6 - i * .3; g.beginPath(); g.ellipse(T.x + wob, T.y - h, r, r * .3, 0, a % TAU, a % TAU + 4.4); g.stroke();
      g.strokeStyle = 'rgba(190,215,235,.6)'; g.lineWidth = 2; g.beginPath(); g.ellipse(T.x + wob, T.y - h, r * .8, r * .24, 0, (a + 3) % TAU, (a + 3) % TAU + 3); g.stroke();
    }
    g.globalCompositeOperation = 'lighter'; const gl = g.createRadialGradient(T.x, T.y - 70, 0, T.x, T.y - 70, 110); gl.addColorStop(0, 'rgba(255,250,230,.35)'); gl.addColorStop(1, 'rgba(255,250,230,0)'); g.fillStyle = gl; g.beginPath(); g.arc(T.x, T.y - 70, 110, 0, TAU); g.fill();
    g.restore();
  },
});

// ============================================================================ 5. 액체질소 지뢰 → 빙결 지대
const MINE = { max: [0, 2, 3, 4, 5, 6, 9], R: [0, 22, 30, 38, 46, 56, 62], drop: [0, .8, .6, .48, .4, .34, .28] };
function drawMineLv(g, x, y, on, lv, t, sc = 1) {
  const T = tierOf(lv), bl = on ? (Math.sin(t * 10) > 0 ? 1 : .35) : .2;
  g.save(); g.translate(x, y); g.scale(sc, sc);
  g.fillStyle = 'rgba(0,0,0,.3)'; g.beginPath(); g.ellipse(0, 2, 11, 4.8, 0, 0, TAU); g.fill();
  if (on) tierGlow(g, 0, 0, 9, lv, t);
  const gr = g.createRadialGradient(-3, -3, 1, 0, 0, 10);
  if (lv >= 5) { gr.addColorStop(0, '#fffbe6'); gr.addColorStop(.5, '#9fe0ff'); gr.addColorStop(1, '#2c6aa8'); } else { gr.addColorStop(0, '#e6f6ff'); gr.addColorStop(1, '#3d7fb8'); }
  g.fillStyle = gr; g.strokeStyle = lv >= 5 ? '#c58a12' : '#123a5c'; g.lineWidth = 1.6; g.beginPath(); g.ellipse(0, 0, 9.5, 5.8, 0, 0, TAU); g.fill(); g.stroke();
  g.strokeStyle = 'rgba(255,255,255,.6)'; g.lineWidth = 1; g.beginPath(); g.ellipse(0, -.5, 6, 3.2, 0, Math.PI * 1.1, Math.PI * 1.9); g.stroke();
  g.globalCompositeOperation = 'lighter'; g.fillStyle = `rgba(${T.core},${bl})`; g.beginPath(); g.arc(0, -1, 2.6, 0, TAU); g.fill();
  if (on) { g.fillStyle = `rgba(${T.glow},${.25 * bl})`; g.beginPath(); g.arc(0, 0, 13, 0, TAU); g.fill(); }
  g.restore();
}
item({
  id: 'mine', group: '무기', name: '액체질소 지뢰', evoName: '빙결 지대', target: 'house', sides: ['top', 'topright', 'left'],
  lvText: [null, '지나간 자리에 지뢰 2개', '지뢰 3개 · 더 넓게 언다', '지뢰 4개 · 얼음이 터지며 둘레도 언다', '지뢰 5개 · 더 넓게', '금빛 지뢰 6개 · 거대한 냉기 폭발', '빙결 지대: 지뢰끼리 얼음 길 · 건너는 요괴는 그대로 언다'],
  icon(g, x, y, r, lv, t) { drawMineLv(g, x, y + r * .1, true, Math.max(lv, 2), t, r / 9); },
  setup(s) { s.st.mines = []; s.st.drop = 0; s.player.x = 230; s.player.y = 165; s.st.path = 0; },
  update(s, dt, lv) {
    const P = s.player; s.st.path += dt; const a = s.st.path * 1.0;
    P.x = 215 + Math.cos(a) * 95; P.y = 150 + Math.sin(a * 2) * 55; P.moving = true;
    s.st.drop -= dt;
    if (s.st.drop <= 0) { s.st.mines.push({ x: P.x, y: P.y + 6, arm: .25, lv }); while (s.st.mines.length > MINE.max[lv]) s.st.mines.shift(); s.st.drop = MINE.drop[lv]; }
    const R = MINE.R[lv];
    for (const mi of s.st.mines) {
      mi.arm -= dt; mi.cool = (mi.cool || 0) - dt;
      if (mi.arm <= 0 && !mi.used && !(mi.cool > 0)) for (const m of s.alive()) if (Math.hypot(m.x - mi.x, m.y - mi.y) < m.r + 10 + lv && !m.frozen) {
        if (lv >= 6) mi.cool = .35; else mi.used = true;
        part(s, { kind: 'ring', x: mi.x, y: mi.y, life: .45, size: R * 1.4, color: lv >= 5 ? '255,240,190' : '150,230,255', width: 4 + lv });
        part(s, { kind: 'glow', x: mi.x, y: mi.y, life: .4, size: R * 1.5, color: '150,230,255' });
        if (lv >= 3) part(s, { kind: 'ring', x: mi.x, y: mi.y, life: .6, size: R * 2, color: '220,250,255', width: 2 });
        for (let i = 0; i < 10 + lv * 4; i++) { const a2 = rand(0, TAU); part(s, { kind: 'frost', x: mi.x + Math.cos(a2) * rand(0, R), y: mi.y + Math.sin(a2) * rand(0, R) * .6, life: 1.2 + lv * .1, size: rand(4, 7 + lv) }); }
        for (let i = 0; i < 4 + lv * 2; i++) { const a2 = rand(0, TAU), v = rand(60, 120 + lv * 20); part(s, { kind: 'shard', x: mi.x, y: mi.y, vx: Math.cos(a2) * v, vy: Math.sin(a2) * v * .7, vz: rand(60, 200), grav: 700, life: .7, size: rand(3, 5 + lv * .5), rot: rand(0, TAU), vr: rand(-12, 12) }); }
        for (const o of s.alive()) if (Math.hypot(o.x - mi.x, o.y - mi.y) < R + o.r) o.frozen = rand(.7, 1.0) - lv * .05;
        hitFx(s, mi.x, mi.y, lv, 1); s.shake = Math.max(s.shake, 2 + lv * .7); break;
      }
    }
    s.st.mines = s.st.mines.filter(m => !m.used);
    // 최고급: 얼음이 깨질 때 곁의 요괴도 얼린다(연쇄)
    if (lv >= 6) for (const m of s.mobs) if (!m.dead && m.frozen > 0 && m.frozen <= dt * 1.01) for (const o of s.alive()) if (o !== m && !o.frozen && Math.hypot(o.x - m.x, o.y - m.y) < 38) { o.frozen = .3; part(s, { kind: 'glow', x: o.x, y: o.y, life: .25, size: 22, color: '200,245,255' }); }
    // 최고급: 지뢰끼리 얼음 길 — 건너는 몹은 그 자리에서 언다
    if (lv >= 6) for (let i = 0; i + 1 < s.st.mines.length; i++) { const A = s.st.mines[i], B = s.st.mines[i + 1]; if (A.arm > 0 || B.arm > 0) continue; for (const m of s.alive()) if (!m.frozen && segDist(m.x, m.y, A.x, A.y, B.x, B.y) < m.r + 13) { m.frozen = .6; part(s, { kind: 'glow', x: m.x, y: m.y, life: .3, size: 26, color: '170,240,255' }); part(s, { kind: 'star', x: m.x, y: m.y - 8, life: .4, size: 5, color: null }); } }
  },
  drawGround(s, g, lv) {
    const M = s.st.mines;
    if (lv >= 6) for (let i = 0; i + 1 < M.length; i++) {
      const A = M[i], B = M[i + 1]; if (A.arm > 0 || B.arm > 0) continue;
      g.save(); g.lineCap = 'round'; g.globalCompositeOperation = 'lighter';
      g.strokeStyle = `hsla(${hue(s.t, i * 40)},100%,72%,.3)`; g.lineWidth = 22; g.beginPath(); g.moveTo(A.x, A.y); g.lineTo(B.x, B.y); g.stroke();
      g.strokeStyle = 'rgba(120,210,255,.5)'; g.lineWidth = 12; g.stroke();
      g.strokeStyle = 'rgba(235,252,255,.95)'; g.lineWidth = 3.5; g.stroke();
      g.restore();
      const L = Math.hypot(B.x - A.x, B.y - A.y), n = Math.floor(L / 10);
      for (let k = 1; k < n; k++) { const u = k / n, x = lerp(A.x, B.x, u), y = lerp(A.y, B.y, u), h = 6 + ((k * 7) % 6) + Math.sin(s.t * 6 + k) * 1.2;
        const gr = g.createLinearGradient(0, y - h, 0, y); gr.addColorStop(0, 'rgba(255,255,255,.98)'); gr.addColorStop(1, 'rgba(120,200,255,.85)');
        g.fillStyle = gr; g.strokeStyle = 'rgba(30,90,150,.6)'; g.lineWidth = .8; g.beginPath(); g.moveTo(x - 2.4, y); g.lineTo(x, y - h); g.lineTo(x + 2.4, y); g.closePath(); g.fill(); g.stroke(); }
    }
    // 냉기 범위 미리 보기(Lv3~)
    for (const mi of M) { const on = mi.arm <= 0;
      if (on && lv >= 3) { g.save(); g.strokeStyle = `rgba(${tierOf(lv).glow},${.15 + .1 * Math.sin(s.t * 6)})`; g.lineWidth = 1.2; g.setLineDash([3, 5]); g.beginPath(); g.ellipse(mi.x, mi.y, MINE.R[lv], MINE.R[lv] * .6, 0, 0, TAU); g.stroke(); g.restore(); }
      drawMineLv(g, mi.x, mi.y, on, lv, s.t, (1 + (Math.min(lv, 5) - 1) * .1) * (1 + s.surge * .6)); }
  },
});
