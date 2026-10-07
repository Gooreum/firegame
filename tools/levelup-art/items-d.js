'use strict';
/** 레벨이 바뀌면 깎인 몹 체력을 채운다: 앞 레벨에서 깎은 몹이 다음 레벨 처치로 몰리지 않게(처치 속도 공정 비교). */
function freshMobs(s) { for (const m of s.mobs) if (m.maxHp) m.hp = m.maxHp; }
// 호스 채찍 · 고압 펌프 · 장화 · 방화복 (items-a.js 짜임을 따른다)

// ---------------------------------------------------------------- 10. 호스 채찍 → 물 회오리
function iconWhip(g, x, y, r, lv, t) {
  g.save(); g.translate(x, y); g.lineCap = 'round';
  g.beginPath(); for (let i = 0; i <= 40; i++) { const u = i / 40, a = u * TAU * 1.6 + t * 2, rr = r * (.25 + u * .7); i ? g.lineTo(Math.cos(a) * rr, Math.sin(a) * rr * .8) : g.moveTo(Math.cos(a) * rr, Math.sin(a) * rr * .8); }
  g.strokeStyle = '#5a120d'; g.lineWidth = r * .42; g.stroke(); g.strokeStyle = lv >= 5 ? '#ff7a4a' : '#e0453a'; g.lineWidth = r * .26; g.stroke();
  g.strokeStyle = 'rgba(255,255,255,.35)'; g.lineWidth = r * .08; g.stroke();
  const a = TAU * 1.6 + t * 2, ex = Math.cos(a) * r * .95, ey = Math.sin(a) * r * .76;
  const ng = g.createRadialGradient(ex - 1, ey - 1, 0, ex, ey, r * .3); ng.addColorStop(0, '#fff6d0'); ng.addColorStop(1, '#b8861a');
  g.fillStyle = ng; g.beginPath(); g.arc(ex, ey, r * .26, 0, TAU); g.fill();
  g.restore();
}
const WHIP_ARMS = [0, 1, 1, 2, 2, 3, 3], WHIP_R = [0, 46, 56, 64, 72, 82, 86];
item({
  id: 'whip', group: '무기', name: '호스 채찍', evoName: '물 회오리', target: 'house', sides: ['top', 'topright'],
  lvText: [null, '호스를 휘둘러 둘레 요괴를 튕긴다', '더 넓게 · 더 세게', '두 갈래로 휘두른다', '더 빨리 · 물 리본이 길게', '세 갈래 · 금빛 · 맞으면 물보라', '물 회오리: 휘두른 자리에 물 고리가 남아 돈다'],
  icon: iconWhip,
  setup(s) { s.st.a = 0; s.st.trail = []; s.st.rings = []; s.st.drop = 0; s.player.x = 300; s.player.y = 170; },
  onLevel(s, lv) { s.st.trail = []; freshMobs(s); },
  update(s, dt, lv) {
    const P = s.player, arms = WHIP_ARMS[lv], R = WHIP_R[lv] * (1 + s.surge * .25);
    s.st.a += dt * [0, 3.6, 4.4, 5.2, 6.2, 6.8, 7.2][lv];
    const dmg = [0, 2, 2.4, 3, 3.6, 4.4, 5][lv], push = [0, 75, 100, 130, 165, 200, 230][lv], len = 10 + lv * 2;
    for (let i = 0; i < arms; i++) {
      const a = s.st.a + i * TAU / arms, tx = P.x + Math.cos(a) * R, ty = P.y - 4 + Math.sin(a) * R * .75;
      const tr = s.st.trail[i] = (s.st.trail[i] || []); tr.unshift({ x: tx, y: ty }); while (tr.length > len) tr.pop();
      for (const m of s.alive()) if (segDist(m.x, m.y, P.x, P.y - 4, tx, ty) < m.r + 5 + lv && !(m.cd.wp > s.t)) {
        m.cd.wp = s.t + .32; const L = Math.hypot(m.x - P.x, m.y - P.y) || 1;
        m.hit(s, dmg, (m.x - P.x) / L * push, (m.y - P.y) / L * push); hitFx(s, m.x, m.y - 4, lv, .8);
        if (lv >= 5) { splash(s, m.x, m.y, 20, 5); s.hitArea(m.x, m.y, 20, 1.2, 120); }
        s.shake = Math.max(s.shake, 1.5 + lv * .3);
      }
    }
    if (lv === 6) {
      s.st.drop -= dt;
      if (s.st.drop <= 0) { s.st.drop = .15; for (let i = 0; i < arms; i++) { const tr = s.st.trail[i]; if (tr && tr[0]) { s.st.rings.push({ x: tr[0].x, y: tr[0].y + 4, k: 0 }); part(s, { kind: 'prism', x: tr[0].x, y: tr[0].y + 4, life: .3, size: 18 }); } } }
      for (const r of s.st.rings) { r.k += dt; for (const m of s.alive()) { const d = Math.hypot(m.x - r.x, m.y - r.y); if (d < m.r + 28 && !(m.cd.rr > s.t)) { m.cd.rr = s.t + .12; const L = d || 1; m.hit(s, 4, -(m.y - r.y) / L * 140 + (m.x - r.x) / L * 40, (m.x - r.x) / L * 140 + (m.y - r.y) / L * 40); if (Math.random() < .4) part(s, { kind: 'drop', x: m.x, y: m.y, vx: rand(-90, 90), vy: rand(-60, 30), vz: rand(60, 140), grav: 500, life: .5, size: 2.4 }); } } }
      s.st.rings = s.st.rings.filter(r => r.k < 2.6);
      if (s.st.rings.length > 40) s.st.rings.shift();
    }
  },
  drawGround(s, g, lv) {
    if (lv !== 6) return;
    g.save(); g.globalCompositeOperation = 'lighter';
    for (const r of s.st.rings) {
      const a = Math.min(1, r.k * 5) * Math.min(1, (2.6 - r.k) * 2);
      for (let j = 0; j < 3; j++) { g.strokeStyle = `hsla(${hue(s.t, j * 120 + r.x)},100%,${70 - j * 6}%,${.7 * a})`; g.lineWidth = 5 - j * 1.3; const st = s.t * (9 + j * 3) + j * 2; g.beginPath(); g.ellipse(r.x, r.y, 28 - j * 6, (28 - j * 6) * .55, 0, st, st + 4.2); g.stroke(); }
      const gl = g.createRadialGradient(r.x, r.y, 0, r.x, r.y, 26); gl.addColorStop(0, `rgba(160,225,255,${.35 * a})`); gl.addColorStop(1, 'rgba(160,225,255,0)'); g.fillStyle = gl; g.beginPath(); g.arc(r.x, r.y, 26, 0, TAU); g.fill();
    }
    g.restore();
  },
  drawAir(s, g, lv) {
    const P = s.player, arms = WHIP_ARMS[lv], R = WHIP_R[lv] * (1 + s.surge * .25), T = tierOf(lv);
    for (let i = 0; i < arms; i++) {
      const tr = s.st.trail[i] || []; if (!tr.length) continue;
      g.save(); g.globalCompositeOperation = 'lighter'; g.lineCap = 'round';
      for (let j = 1; j < tr.length; j++) {
        const k = 1 - j / tr.length;
        g.strokeStyle = lv === 6 ? `hsla(${hue(s.t, j * 18)},100%,68%,${.55 * k})` : `rgba(${T.glow},${(.35 + T.glowA * .4) * k})`; g.lineWidth = (8 + lv * 2.5) * k;
        g.beginPath(); g.moveTo(tr[j - 1].x, tr[j - 1].y); g.lineTo(tr[j].x, tr[j].y); g.stroke();
        if (T.white > 0) { g.strokeStyle = `rgba(255,255,255,${T.white * .7 * k})`; g.lineWidth = (2 + lv * .6) * k; g.stroke(); }
        if (T.gold && j % 2) { g.strokeStyle = `rgba(255,214,110,${.5 * k})`; g.lineWidth = 1.2; g.stroke(); }
      }
      g.restore();
      const t0 = tr[0], a = s.st.a + i * TAU / arms, mx = P.x + Math.cos(a - .55) * R * .55, my = P.y - 4 + Math.sin(a - .55) * R * .45;
      g.save(); g.lineCap = 'round';
      g.strokeStyle = '#5a120d'; g.lineWidth = 5 + lv * .4; g.beginPath(); g.moveTo(P.x, P.y - 4); g.quadraticCurveTo(mx, my, t0.x, t0.y); g.stroke();
      const hg = g.createLinearGradient(P.x, P.y, t0.x, t0.y); hg.addColorStop(0, '#c4231b'); hg.addColorStop(1, lv >= 5 ? '#ff9a5a' : '#ff5a4a');
      g.strokeStyle = hg; g.lineWidth = 3 + lv * .3; g.stroke();
      if (lv >= 5) { g.strokeStyle = 'rgba(255,220,130,.8)'; g.lineWidth = 1; g.stroke(); }
      g.restore();
      tierGlow(g, t0.x, t0.y, 5 + lv, Math.max(2, lv), s.t);
      const ng = g.createRadialGradient(t0.x - 1, t0.y - 1, 0, t0.x, t0.y, 4.5); ng.addColorStop(0, '#fffbe0'); ng.addColorStop(1, lv >= 5 ? '#d99a12' : '#9aa0ae');
      g.fillStyle = ng; g.beginPath(); g.arc(t0.x, t0.y, 3.8 + lv * .25, 0, TAU); g.fill();
      if (Math.random() < .5 + lv * .1) part(s, { kind: 'drop', x: t0.x, y: t0.y, vx: rand(-40, 40), vy: rand(-40, 40), vz: rand(20, 60), grav: 400, life: .4, size: 2 + lv * .15 });
    }
  },
});

// ---------------------------------------------------------------- 11. 고압 펌프 → 초고압 펌프
function drawPumpTank(g, x, y, lv, t, sc = 1, surge = 0) {
  // 등에 멘 압력 탱크 + 게이지. 레벨마다 바늘이 오르고 빛난다.
  g.save(); g.translate(x, y); g.scale(sc, sc);
  const T = tierOf(lv);
  if (lv >= 2) { g.globalCompositeOperation = 'lighter'; const gl = g.createRadialGradient(0, 0, 0, 0, 0, 10 + lv * 3 + surge * 10); gl.addColorStop(0, lv >= 6 ? `hsla(${hue(t)},100%,70%,.6)` : `rgba(${T.gold ? '255,210,110' : T.glow},${.2 + lv * .07})`); gl.addColorStop(1, 'rgba(0,0,0,0)'); g.fillStyle = gl; g.beginPath(); g.arc(0, 0, 10 + lv * 3 + surge * 10, 0, TAU); g.fill(); g.globalCompositeOperation = 'source-over'; }
  const bg = g.createLinearGradient(-6, 0, 6, 0);
  if (lv >= 5) { bg.addColorStop(0, '#8a5c0e'); bg.addColorStop(.4, '#fff2b8'); bg.addColorStop(1, '#b07a12'); } else { bg.addColorStop(0, '#8d1510'); bg.addColorStop(.4, '#ff7466'); bg.addColorStop(1, '#a31a12'); }
  g.fillStyle = bg; g.strokeStyle = '#2a0805'; g.lineWidth = 1.2; g.beginPath(); g.roundRect(-6, -9, 12, 18, 5); g.fill(); g.stroke();
  // 게이지
  const dg = g.createRadialGradient(-1, -1, 0, 0, 0, 5); dg.addColorStop(0, '#ffffff'); dg.addColorStop(1, '#c9d3e0');
  g.fillStyle = dg; g.strokeStyle = '#333'; g.beginPath(); g.arc(0, -11, 5, 0, TAU); g.fill(); g.stroke();
  g.strokeStyle = lv >= 5 ? '#ff3a2a' : '#e8a020'; g.lineWidth = 1.4; g.beginPath(); g.arc(0, -11, 3.6, Math.PI * .75, Math.PI * .75 + Math.PI * 1.5 * (lv / 6)); g.stroke();
  const na = Math.PI * .75 + Math.PI * 1.5 * Math.min(1, lv / 6 + Math.sin(t * 30) * .02 * lv);
  g.strokeStyle = '#111'; g.lineWidth = 1; g.beginPath(); g.moveTo(0, -11); g.lineTo(Math.cos(na) * 4, -11 + Math.sin(na) * 4); g.stroke();
  g.restore();
}
item({
  id: 'tank', group: '보조', name: '고압 펌프', evoName: '초고압 펌프', target: 'house', sides: ['top', 'topright'],
  lvText: [null, '물대포가 굵어지고 멀리 간다', '압력 ↑ · 더 굵게 · 더 밀어낸다', '흰 심 물줄기 · 뒤까지 꿰뚫는다', '물줄기 2개 · 끝에서 물보라', '금빛 고압 · 큰 물보라 폭발', '초고압 펌프: 몇 초마다 내 둘레 물 대폭발'],
  icon(g, x, y, r, lv, t) { drawPumpTank(g, x, y + r * .25, lv, t, r / 11); },
  onLevel(s) { freshMobs(s); },
  setup(s) { s.st.tg = []; s.st.cd = 0; s.st.boom = 1.2; s.st.booms = []; s.player.x = 160; s.player.y = 165; },
  update(s, dt, lv) {
    const P = s.player, n = lv >= 4 ? 2 : 1, used = new Set(); s.st.tg = [];
    const range = 130 + lv * 32;
    for (let i = 0; i < n; i++) { const m = s.nearest(P.x, P.y, range, used); if (m) { used.add(m); s.st.tg.push(m); } }
    s.st.cd -= dt;
    if (s.st.cd <= 0) {
      s.st.cd = [0, .22, .19, .16, .14, .12, .1][lv];
      const dmg = [0, 1.4, 2, 2.4, 2.8, 3.4, 4][lv], reach = lv >= 3 ? 40 + lv * 16 : 0, w = 3 + lv * 1.7, push = 30 + lv * 30;
      for (const m of s.st.tg) {
        const dx = m.x - P.x, dy = m.y - P.y, L = Math.hypot(dx, dy) || 1, ex = m.x + dx / L * reach, ey = m.y + dy / L * reach;
        for (const o of s.alive()) if (o === m || (reach && segDist(o.x, o.y, m.x, m.y, ex, ey) < o.r + w * .5)) o.hit(s, dmg, dx / L * push, dy / L * push);
        hitFx(s, m.x, m.y - 4, lv, .6);
        if (lv >= 4 && !(m.cd.ts > s.t)) { m.cd.ts = s.t + .35; splash(s, ex, ey, 18 + lv * 3, 6); s.hitArea(ex, ey, 18 + lv * 3, 1.5, 140); }
      }
    }
    if (lv === 6) {
      s.st.boom -= dt;
      if (s.st.boom <= 0) {
        s.st.boom = 2.2; const x = P.x, y = P.y - 4;
        s.st.booms.push({ x, y, k: 0 });
        s.hitArea(x, y, 140, 12, 380);
        splash(s, x, y, 90, 40); part(s, { kind: 'prism', x, y, life: .6, size: 150 }); part(s, { kind: 'glow', x, y, life: .4, size: 150, color: '200,235,255' });
        for (let i = 0; i < 30; i++) { const a = rand(0, TAU), v = rand(150, 320); part(s, { kind: 'star', x, y, vx: Math.cos(a) * v, vy: Math.sin(a) * v * .7, life: .6, size: rand(3, 6), color: null }); }
        s.shake = Math.max(s.shake, 10); s.stop = Math.max(s.stop, .07); s.flash = Math.max(s.flash, .25); s.flashColor = '220,240,255';
      }
      for (const b of s.st.booms) b.k += dt; s.st.booms = s.st.booms.filter(b => b.k < .8);
    }
  },
  drawGround(s, g, lv) {
    if (lv !== 6) return;
    const P = s.player, k = 1 - Math.max(0, s.st.boom) / 2.2;
    // 차오르는 압력 고리: 터지기 직전 빨라지고 밝아진다.
    g.save(); g.globalCompositeOperation = 'lighter';
    const r = 140 * (1 - k * .75);
    g.strokeStyle = `hsla(${hue(s.t)},100%,70%,${.15 + k * .6})`; g.lineWidth = 2 + k * 4; g.setLineDash([8, 6]); g.lineDashOffset = -s.t * 80; g.beginPath(); g.ellipse(P.x, P.y, r, r * .62, 0, 0, TAU); g.stroke(); g.setLineDash([]);
    for (const b of s.st.booms) { const e = ease(b.k / .8), a = 1 - b.k / .8; for (let j = 0; j < 3; j++) { g.strokeStyle = `hsla(${hue(s.t, j * 120)},100%,70%,${a})`; g.lineWidth = 10 - j * 3; g.beginPath(); g.ellipse(b.x, b.y + 4, 150 * e * (1 - j * .12), 150 * e * .62 * (1 - j * .12), 0, 0, TAU); g.stroke(); } const gl = g.createRadialGradient(b.x, b.y, 0, b.x, b.y, 150 * e); gl.addColorStop(0, `rgba(160,220,255,${.35 * a})`); gl.addColorStop(1, 'rgba(160,220,255,0)'); g.fillStyle = gl; g.beginPath(); g.arc(b.x, b.y, 150 * e, 0, TAU); g.fill(); }
    g.restore();
  },
  ents(s, g, lv) { const P = s.player; return [{ y: P.y - .5, d: () => drawPumpTank(g, P.x - 9, P.y - 4, lv, s.t, .8 + lv * .06, s.surge) }]; },
  drawAir(s, g, lv) {
    const P = s.player, sx = P.x + 11, sy = P.y - 5, sg = 1 + s.surge * .5, w = (2.4 + lv * .8) * sg;
    for (const m of s.st.tg) {
      tierStream(g, sx, sy, m.x, m.y - 4, w, lv, s.t, 10);
      if (lv >= 3) { const L5 = lv, dx = m.x - P.x, dy = m.y - P.y, L = Math.hypot(dx, dy) || 1, reach = 40 + L5 * 16; g.save(); g.globalCompositeOperation = 'lighter'; const lg = g.createLinearGradient(m.x, m.y, m.x + dx / L * reach, m.y + dy / L * reach); lg.addColorStop(0, lv >= 6 ? `hsla(${hue(s.t)},100%,75%,.85)` : `rgba(${tierOf(lv).core},.85)`); lg.addColorStop(1, 'rgba(160,220,255,0)'); g.strokeStyle = lg; g.lineWidth = w * .7; g.lineCap = 'round'; g.beginPath(); g.moveTo(m.x, m.y - 4); g.lineTo(m.x + dx / L * reach, m.y - 4 + dy / L * reach); g.stroke(); g.restore(); }
    }
    tierGlow(g, sx, sy, 3 + lv * .5, lv, s.t);
  },
});

// ---------------------------------------------------------------- 12. 장화 → 제트 장화
function iconBoot(g, x, y, r, lv, t) {
  g.save(); g.translate(x, y);
  const bg = g.createLinearGradient(0, -r, 0, r); bg.addColorStop(0, lv >= 5 ? '#fff2b0' : '#ffe680'); bg.addColorStop(1, lv >= 5 ? '#d99a12' : '#e0a51a');
  g.fillStyle = bg; g.strokeStyle = '#4a3005'; g.lineWidth = 1.3;
  g.beginPath(); g.moveTo(-r * .45, -r); g.lineTo(r * .2, -r); g.lineTo(r * .2, r * .25); g.quadraticCurveTo(r * .95, r * .3, r * .95, r * .75); g.lineTo(-r * .55, r * .75); g.closePath(); g.fill(); g.stroke();
  g.fillStyle = '#2a3140'; g.fillRect(-r * .6, r * .62, r * 1.6, r * .25);
  g.fillStyle = 'rgba(255,255,255,.6)'; g.fillRect(-r * .3, -r * .85, r * .15, r * .9);
  g.fillStyle = '#3d8bff'; g.fillRect(-r * .45, -r * .2, r * .65, r * .18);
  g.restore();
}
const BOOT_SPEED = [0, 1, 1.25, 1.5, 1.8, 2.1, 2.6];
item({
  id: 'boots', group: '보조', name: '장화', evoName: '제트 장화', target: 'house', sides: ['top', 'topright'],
  lvText: [null, '빨라진다 · 몸으로 요괴를 밀친다', '더 빨리 · 물 발자국', '발자국마다 물이 튄다', '잔상이 남는다 · 더 세게 밀친다', '금빛 질주 · 부딪히면 물보라 폭발', '제트 장화: 달린 자리에 요괴를 녹이는 물길'],
  icon: iconBoot,
  onLevel(s) { freshMobs(s); },
  setup(s) { s.st.path = 0; s.st.ghost = []; s.st.trail = []; s.st.step = 0; s.st.drop = 0; s.player.x = 220; s.player.y = 150; },
  update(s, dt, lv) {
    const P = s.player, sp = BOOT_SPEED[lv] * (1 + s.surge * .3);
    s.st.path += dt * .85 * sp; const a = s.st.path;
    const nx = 245 + Math.cos(a) * 130, ny = 145 + Math.sin(a * 2) * 70;
    const vx = (nx - P.x) / Math.max(dt, 1e-4), vy = (ny - P.y) / Math.max(dt, 1e-4);
    P.x = nx; P.y = ny; P.moving = true;
    s.st.ghost.unshift({ x: P.x, y: P.y }); while (s.st.ghost.length > 30) s.st.ghost.pop();
    // 몸 부딪치기
    const R = [0, 15, 17, 19, 22, 26, 30][lv], dmg = [0, 2, 2.4, 3, 3.6, 4.4, 5.2][lv], push = 90 + lv * 25;
    for (const m of s.alive()) { const d = Math.hypot(m.x - P.x, m.y - P.y); if (d < R + m.r && !(m.cd.bt > s.t)) { m.cd.bt = s.t + .3; const L = d || 1; m.hit(s, dmg, (m.x - P.x) / L * push + vx * .15, (m.y - P.y) / L * push + vy * .15); hitFx(s, m.x, m.y - 4, lv, .7); if (lv >= 5) { splash(s, m.x, m.y, 22, 6); s.hitArea(m.x, m.y, 22, 1.4, 150); } } }
    // 발자국 물튀김
    s.st.step -= dt;
    if (s.st.step <= 0 && lv >= 2) {
      s.st.step = [0, 0, .12, .1, .08, .07, .05][lv];
      part(s, { kind: 'paw', x: P.x + rand(-4, 4), y: P.y + 8, life: .8 + lv * .15 });
      for (let i = 0; i < lv; i++) part(s, { kind: 'drop', x: P.x, y: P.y + 6, vx: rand(-50, 50), vy: rand(-15, 15), vz: rand(40, 90), grav: 450, life: .5, size: rand(2, 2.8) });
      if (lv >= 3) { part(s, { kind: 'ring', x: P.x, y: P.y + 8, life: .3, size: 10 + lv * 3, color: tierOf(lv).core, width: 2 }); s.hitArea(P.x, P.y + 6, 10 + lv * 3, .5 + lv * .2, 60); }
    }
    // Lv6: 물길
    if (lv === 6) {
      s.st.drop -= dt;
      if (s.st.drop <= 0) { s.st.drop = .025; s.st.trail.push({ x: P.x, y: P.y + 6, k: 0 }); }
      for (const q of s.st.trail) q.k += dt;
      s.st.trail = s.st.trail.filter(q => q.k < 1.6);
      for (const m of s.alive()) { if (m.cd.wt > s.t) continue; for (const q of s.st.trail) if (Math.hypot(m.x - q.x, m.y - q.y) < m.r + 14) { m.cd.wt = s.t + .12; m.hit(s, 5, rand(-40, 40), -60); if (Math.random() < .5) part(s, { kind: 'drop', x: m.x, y: m.y, vx: rand(-60, 60), vy: rand(-40, 20), vz: rand(80, 160), grav: 500, life: .5, size: 2.5 }); break; } }
      if (Math.random() < .6) part(s, { kind: 'star', x: P.x + rand(-6, 6), y: P.y + 4, vx: rand(-30, 30), vy: rand(-30, 30), life: .4, size: 3, color: null });
    }
  },
  drawGround(s, g, lv) {
    const T = tierOf(lv);
    // 발밑 장화 빛(몸 아래에 깐다)
    tierGlow(g, s.player.x, s.player.y + 8, 4 + lv * .6, Math.max(2, lv), s.t);
    // 잔상(Lv4~)·발밑 물보라 꼬리
    if (lv === 6 && s.st.trail.length > 1) {
      g.save(); g.globalCompositeOperation = 'lighter'; g.lineCap = 'round'; g.lineJoin = 'round';
      const tr = s.st.trail;
      for (const [w, a0] of [[30, .25], [18, .5], [7, .9]]) {
        for (let i = 1; i < tr.length; i++) { const k = 1 - tr[i].k / 1.6; if (Math.hypot(tr[i].x - tr[i - 1].x, tr[i].y - tr[i - 1].y) > 30) continue; g.strokeStyle = w === 7 ? `rgba(255,255,255,${a0 * k})` : `hsla(${hue(s.t, i * 4)},100%,${w === 30 ? 60 : 72}%,${a0 * k})`; g.lineWidth = w * (.5 + k * .5); g.beginPath(); g.moveTo(tr[i - 1].x, tr[i - 1].y); g.lineTo(tr[i].x, tr[i].y); g.stroke(); }
      }
      g.restore();
    } else if (lv >= 2) {
      const gh = s.st.ghost; g.save(); g.globalCompositeOperation = 'lighter'; g.lineCap = 'round';
      for (let i = 1; i < Math.min(gh.length, 6 + lv * 3); i++) { const k = 1 - i / (6 + lv * 3); g.strokeStyle = `rgba(${T.glow},${.35 * k})`; g.lineWidth = (6 + lv * 2) * k; g.beginPath(); g.moveTo(gh[i - 1].x, gh[i - 1].y + 8); g.lineTo(gh[i].x, gh[i].y + 8); g.stroke(); }
      g.restore();
    }
  },
  ents(s, g, lv) {
    if (lv < 4) return [];
    // 잔상: 몸 실루엣이 뒤에 겹쳐 남는다.
    const P = s.player, gh = s.st.ghost, out = [], n = lv === 4 ? 2 : lv === 5 ? 3 : 4;
    for (let i = 1; i <= n; i++) { const q = gh[Math.min(gh.length - 1, i * 4)]; if (!q) continue; const k = 1 - i / (n + 1);
      out.push({ y: q.y - .1, d: () => { g.save(); g.globalAlpha = .4 * k; const ox = P.x, oy = P.y, sh = s.shield; s.shield = 0; P.x = q.x; P.y = q.y; drawPlayer(s, g); P.x = ox; P.y = oy; s.shield = sh; g.restore(); g.save(); g.globalCompositeOperation = 'lighter'; const gl = g.createRadialGradient(q.x, q.y - 6, 0, q.x, q.y - 6, 18); gl.addColorStop(0, lv >= 6 ? `hsla(${hue(s.t, i * 60)},100%,70%,${.5 * k})` : lv >= 5 ? `rgba(255,214,110,${.45 * k})` : `rgba(${tierOf(lv).glow},${.45 * k})`); gl.addColorStop(1, 'rgba(0,0,0,0)'); g.fillStyle = gl; g.beginPath(); g.arc(q.x, q.y - 6, 18, 0, TAU); g.fill(); g.restore(); } }); }
    return out;
  },
  drawAir(s, g, lv) {
    const P = s.player;
    if (lv >= 6) { g.save(); g.globalCompositeOperation = 'lighter'; for (const sd of [-1, 1]) { const gl = g.createRadialGradient(P.x + sd * 4, P.y + 12, 0, P.x + sd * 4, P.y + 12, 10); gl.addColorStop(0, 'rgba(255,255,255,.9)'); gl.addColorStop(.4, `hsla(${hue(s.t)},100%,70%,.6)`); gl.addColorStop(1, 'rgba(0,0,0,0)'); g.fillStyle = gl; g.beginPath(); g.arc(P.x + sd * 4, P.y + 12, 10, 0, TAU); g.fill(); } g.restore(); }
  },
});

// ---------------------------------------------------------------- 13. 방화복 → 불사조 방화복
function iconSuit(g, x, y, r, lv, t) {
  g.save(); g.translate(x, y);
  const bg = g.createLinearGradient(0, -r, 0, r); bg.addColorStop(0, lv >= 5 ? '#ffe9a0' : '#e8c46a'); bg.addColorStop(1, lv >= 5 ? '#b8800e' : '#9a7228');
  g.fillStyle = bg; g.strokeStyle = '#3a2605'; g.lineWidth = 1.3;
  g.beginPath(); g.moveTo(-r * .35, -r * .85); g.lineTo(r * .35, -r * .85); g.lineTo(r * .95, -r * .45); g.lineTo(r * .75, r * .1); g.lineTo(r * .5, 0); g.lineTo(r * .5, r * .9); g.lineTo(-r * .5, r * .9); g.lineTo(-r * .5, 0); g.lineTo(-r * .75, r * .1); g.lineTo(-r * .95, -r * .45); g.closePath(); g.fill(); g.stroke();
  g.fillStyle = '#e9f4ff'; g.fillRect(-r * .5, r * .25, r, r * .16); g.fillRect(-r * .06, -r * .8, r * .12, r * 1.7);
  g.globalCompositeOperation = 'lighter'; g.fillStyle = 'rgba(255,255,200,.5)'; g.fillRect(-r * .5, r * .25, r, r * .06);
  g.restore();
}
const SUIT_R = [0, 19, 22, 25, 28, 32, 46];
function drawWing(g, x, y, side, span, k, t) {
  // 물 불사조 날개: 깃털 5장이 겹친 가산 그라데이션
  g.save(); g.translate(x, y); g.scale(side, 1); g.globalCompositeOperation = 'lighter';
  for (let i = 0; i < 6; i++) {
    const a = -.9 + i * .32, L = span * (1 - i * .09) * k, wdt = span * .2 * k;
    const ex = Math.cos(a) * L, ey = Math.sin(a) * L * .8;
    const gr = g.createLinearGradient(0, 0, ex, ey); gr.addColorStop(0, 'rgba(255,255,255,.9)'); gr.addColorStop(.4, `hsla(${hue(t, i * 40)},100%,72%,.7)`); gr.addColorStop(1, 'rgba(120,200,255,0)');
    g.fillStyle = gr; g.beginPath(); g.moveTo(0, 0); g.quadraticCurveTo(ex * .5 - ey * .3 * wdt / span * 4, ey * .5 - wdt, ex, ey); g.quadraticCurveTo(ex * .5, ey * .5 + wdt * .5, 0, 0); g.fill();
  }
  g.restore();
}
item({
  id: 'suit', group: '보조', name: '방화복', evoName: '불사조 방화복', target: 'house', sides: ['top', 'topright'],
  lvText: [null, '불에 덜 다치고 닿은 요괴를 튕긴다', '보호막 두껍게 · 더 세게 튕긴다', '보호막이 물결을 밀어낸다', '보호막이 넓어진다 · 물결이 잦다', '금빛 보호막 · 거의 안 다친다', '불사조 방화복: 쓰러지면 물 날개로 부활하며 폭발'],
  icon: iconSuit,
  pre: 22,
  setup(s) { s.player.x = 330; s.player.y = 185; s.hp = 1; s.st.wing = -1; s.st.revived = false; s.st.ko = 0; },
  onLevel(s, lv) { freshMobs(s); if (lv === 6) { s.hp = Math.min(s.hp, .35); s.st.revived = false; s.st.wing = -1; } },
  update(s, dt, lv) {
    const P = s.player, R = SUIT_R[lv] * (1 + s.surge * .3);
    const dmg = [0, 1.4, 2, 3, 3.6, 4.4, 8][lv], push = [0, 75, 100, 130, 165, 200, 240][lv];
    const hurt = [0, .07, .055, .04, .03, .015, .0][lv] + (lv === 6 && !s.st.revived ? .09 : 0);
    if (s.st.wing >= 0) { s.st.wing += dt; }
    if (s.st.ko > 0) { s.st.ko -= dt; if (s.st.ko <= 0) revive(s); return; }
    for (const m of s.alive()) {
      const d = Math.hypot(m.x - P.x, m.y - P.y);
      if (d < R + m.r && !(m.cd.su > s.t)) {
        m.cd.su = s.t + .3; const L = d || 1;
        m.hit(s, dmg, (m.x - P.x) / L * push, (m.y - P.y) / L * push);
        s.hp -= hurt; s.shield = .3;
        part(s, { kind: 'ring', x: P.x, y: P.y - 4, life: .3, size: R + 6, color: lv >= 5 ? '255,215,110' : tierOf(lv).core, width: 2 + lv * .5 });
        if (lv >= 3) hitFx(s, m.x, m.y - 4, lv, .7);
        if (lv >= 4) { splash(s, m.x, m.y, 16 + lv * 2, 5); s.hitArea(m.x, m.y, 16 + lv * 2, 1, 120); }
        if (lv < 3) { part(s, { kind: 'ring', x: P.x, y: P.y, life: .25, size: 16, color: '255,90,80', width: 2 }); }
      }
    }
    // Lv3~: 보호막이 주기적으로 물결을 밀어낸다(반격). 레벨마다 더 자주·더 넓게.
    if (lv >= 3 && lv < 6) { s.st.pulse = (s.st.pulse ?? 1) - dt; if (s.st.pulse <= 0) { s.st.pulse = [0, 0, 0, 1.5, 1.15, .85][lv]; const PR = R + 18 + lv * 8; s.hitArea(P.x, P.y - 4, PR, 1 + lv * .8, 160); part(s, { kind: 'ring', x: P.x, y: P.y - 4, life: .4, size: PR * 1.1, color: lv >= 5 ? '255,215,110' : tierOf(lv).core, width: 3 + lv * .4 }); part(s, { kind: 'glow', x: P.x, y: P.y - 4, life: .3, size: PR, color: lv >= 5 ? '255,220,140' : tierOf(lv).glow }); for (let i = 0; i < 6 + lv * 2; i++) { const a2 = rand(0, TAU); part(s, { kind: 'drop', x: P.x + Math.cos(a2) * R, y: P.y + Math.sin(a2) * R * .7, vx: Math.cos(a2) * 120, vy: Math.sin(a2) * 80, vz: rand(40, 100), grav: 500, life: .5, size: 2.4 }); } } }
    // 판이 끝나지 않게 Lv5까지는 천천히 회복
    if (lv < 6 || s.st.revived) s.hp = Math.min(1, s.hp + dt * (.05 + lv * .02));
    if (s.hp <= 0 && lv < 6) s.hp = .3;
    // Lv6: 보여 주려고 2.8초 뒤 체력이 바닥난다(요괴에 둘러싸여 쓰러짐 → 부활).
    if (lv === 6 && !s.st.revived && s.st.ko <= 0) { const k = (s.t - AT[6] - 1.2) / 1.6; s.hp = Math.min(s.hp, .35 * (1 - clamp(k, 0, 1))); }
    if (lv === 6 && !s.st.revived && s.hp <= 0) { s.hp = 0; s.st.ko = .45; s.slow = Math.max(s.slow, .4); for (let i = 0; i < 50; i++) { const a = rand(0, TAU), r = rand(80, 200); part(s, { kind: 'mote', sx: P.x + Math.cos(a) * r, sy: P.y + Math.sin(a) * r * .7, tx: P.x, ty: P.y - 8, x: P.x, y: P.y, life: .45, size: rand(1.5, 3) }); } }
    if (lv === 6 && s.st.revived) { s.st.flap = (s.st.flap ?? 1) - dt; if (s.st.flap <= 0) { s.st.flap = .8; s.hitArea(P.x, P.y - 6, 125, 8, 260); part(s, { kind: "prism", x: P.x, y: P.y - 6, life: .45, size: 135 }); splash(s, P.x, P.y, 70, 16); s.shake = Math.max(s.shake, 4); } }
    if (lv === 6 && s.st.revived && Math.random() < dt * 12) part(s, { kind: 'star', x: P.x + rand(-30, 30), y: P.y - 10 + rand(-14, 6), vx: rand(-20, 20), vy: rand(-40, -10), life: .5, size: 3, color: null });
  },
  ents(s, g, lv) {
    const P = s.player;
    return [{ y: P.y + .2, d: () => {
      const R = SUIT_R[lv] * (1 + s.surge * .3), T = tierOf(lv), hitk = Math.max(0, s.shield) * 3;
      g.save(); g.globalCompositeOperation = 'lighter';
      // 보호막 구
      const gr = g.createRadialGradient(P.x, P.y - 6, R * .4, P.x, P.y - 6, R);
      const c = lv >= 6 ? hsl2rgb(hue(s.t)) : lv >= 5 ? '255,210,110' : T.glow;
      gr.addColorStop(0, `rgba(${c},0)`); gr.addColorStop(.75, `rgba(${c},${.12 + lv * .05 + hitk * .2})`); gr.addColorStop(1, `rgba(255,255,255,${.25 + lv * .08 + hitk * .3})`);
      g.fillStyle = gr; g.beginPath(); g.ellipse(P.x, P.y - 6, R, R * .92, 0, 0, TAU); g.fill();
      // 두께: 도는 고리 수가 레벨마다 는다
      for (let j = 0; j < Math.ceil(lv / 2) + 1; j++) { g.strokeStyle = lv >= 6 ? `hsla(${hue(s.t, j * 90)},100%,72%,.8)` : lv >= 5 ? `rgba(255,${220 - j * 20},${130 - j * 30},.8)` : `rgba(${T.core},${.5 + lv * .06})`; g.lineWidth = 1 + lv * .35; const st = s.t * (2.5 + j) * (j % 2 ? -1 : 1) + j; g.beginPath(); g.ellipse(P.x, P.y - 6, R * (1 - j * .06), R * .92 * (1 - j * .06), 0, st, st + 2.6 + lv * .4); g.stroke(); }
      // 육각 무늬 반짝(Lv3~)
      if (lv >= 3) { for (let i = 0; i < 6 + lv; i++) { const a = s.t * .8 + i * TAU / (6 + lv), x = P.x + Math.cos(a) * R * .8, y = P.y - 6 + Math.sin(a) * R * .72; g.fillStyle = `rgba(255,255,255,${.25 + .25 * Math.sin(s.t * 6 + i)})`; g.beginPath(); for (let k = 0; k < 6; k++) { const b = k * Math.PI / 3; g.lineTo(x + Math.cos(b) * 2.6, y + Math.sin(b) * 2.6); } g.closePath(); g.fill(); } }
      g.restore();
      // 불사조 날개(부활 뒤 상시 + 부활 순간 크게)
      if (lv === 6 && s.st.wing >= 0) { const w = s.st.wing, big = w < 1.4 ? 1 + (1 - ease(w / 1.4)) * 3.2 : 1, flap = 1 + Math.sin(s.t * 7) * .15; drawWing(g, P.x - 5, P.y - 12, -1, 52 * big * flap, 1, s.t); drawWing(g, P.x + 5, P.y - 12, 1, 52 * big * flap, 1, s.t); }
      // 쓰러짐 중엔 몸을 어둡게
      if (s.st.ko > 0) { g.save(); g.globalAlpha = .55; g.fillStyle = '#000'; g.beginPath(); g.ellipse(P.x, P.y - 4, 16, 18, 0, 0, TAU); g.fill(); g.restore(); }
    } }];
  },
  drawAir(s, g, lv) {
    const P = s.player, hp = Math.max(0, s.hp);
    g.save(); g.fillStyle = 'rgba(0,0,0,.65)'; g.beginPath(); g.roundRect(P.x - 17, P.y - 40, 34, 6, 3); g.fill();
    const gr = g.createLinearGradient(P.x - 16, 0, P.x + 16, 0);
    if (hp > .5) { gr.addColorStop(0, '#2fbf55'); gr.addColorStop(1, '#8cff9e'); } else if (hp > .25) { gr.addColorStop(0, '#d99a12'); gr.addColorStop(1, '#ffe27a'); } else { gr.addColorStop(0, '#c4231b'); gr.addColorStop(1, '#ff7a6a'); }
    g.fillStyle = gr; g.beginPath(); g.roundRect(P.x - 16, P.y - 39, 32 * hp, 4, 2); g.fill();
    g.restore();
    if (s.st.ko > 0) metalText(g, '쓰러짐…', P.x, P.y - 52, 12, 'silver', s.t, .9);
    if (lv === 6 && s.st.wing >= 0 && s.st.wing < 1.6) metalText(g, '부활!', P.x, P.y - 60 - s.st.wing * 10, 22, 'rainbow', s.t, s.st.wing > 1.2 ? (1.6 - s.st.wing) / .4 : 1);
  },
});
function revive(s) {
  const P = s.player, x = P.x, y = P.y - 8;
  s.st.revived = true; s.st.wing = 0; s.hp = .6;
  s.hitArea(x, y, 170, 99, 420);
  splash(s, x, y, 110, 50); part(s, { kind: 'prism', x, y, life: .7, size: 190 }); part(s, { kind: 'prism', x, y, life: .9, size: 120 });
  part(s, { kind: 'glow', x, y, life: .5, size: 170, color: '255,240,210' });
  for (let i = 0; i < 80; i++) { const a = rand(0, TAU), v = rand(150, 360); part(s, { kind: 'star', x, y, vx: Math.cos(a) * v, vy: Math.sin(a) * v * .7, life: rand(.6, 1), size: rand(3, 6), color: null }); }
  for (let i = 0; i < 40; i++) { const a = rand(-Math.PI, 0), v = rand(120, 300); part(s, { kind: 'confetti', x, y, vx: Math.cos(a) * v, vy: Math.sin(a) * v, life: rand(1, 1.6), size: rand(2, 3.4), rot: rand(0, TAU), vr: rand(-8, 8), color: null }); }
  s.shake = Math.max(s.shake, 14); s.stop = Math.max(s.stop, .14); s.flash = Math.max(s.flash, .4); s.flashColor = "255,250,235";
}
