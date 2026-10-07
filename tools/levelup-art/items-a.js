'use strict';
// 기준 아이템 둘: 물대포·회전 스프링클러. 다른 아이템 파일은 이 둘의 짜임을 따른다.

// ---------------------------------------------------------------- 1. 물대포 → 고압 방수포
function iconNozzle(g, x, y, r, lv, t) {
  g.save(); g.translate(x, y); g.rotate(-.6);
  const gr = g.createLinearGradient(0, -r * .4, 0, r * .4); gr.addColorStop(0, '#fff2b8'); gr.addColorStop(.5, '#d9a531'); gr.addColorStop(1, '#8a5c0e');
  g.fillStyle = '#b8231b'; g.strokeStyle = '#3b0805'; g.lineWidth = 1.2; g.beginPath(); g.roundRect(-r, -r * .32, r * .9, r * .64, r * .2); g.fill(); g.stroke();
  g.fillStyle = gr; g.strokeStyle = '#4a3505'; g.beginPath(); g.moveTo(-r * .15, -r * .38); g.lineTo(r * .75, -r * .2); g.lineTo(r * .75, r * .2); g.lineTo(-r * .15, r * .38); g.closePath(); g.fill(); g.stroke();
  g.globalCompositeOperation = 'lighter';
  const w = g.createLinearGradient(r * .7, 0, r * 1.4, 0); w.addColorStop(0, 'rgba(160,220,255,.95)'); w.addColorStop(1, 'rgba(160,220,255,0)');
  g.fillStyle = w; g.beginPath(); g.moveTo(r * .75, -r * .15); g.lineTo(r * 1.5, -r * .35); g.lineTo(r * 1.5, r * .35); g.lineTo(r * .75, r * .15); g.fill();
  g.restore();
}
item({
  id: 'hose', group: '무기', name: '물대포', evoName: '고압 방수포', target: 'house', sides: ['top', 'topright'],
  lvText: [null, '가장 가까운 요괴에 물줄기 1개', '물줄기 2개 · 더 굵게', '물줄기 3개 · 뒤까지 꿰뚫는다', '물줄기 4개 · 더 빠르게', '물줄기 5개 · 끝에서 물보라 폭발', '고압 방수포: 화면을 쓸어 내는 거대한 물기둥'],
  icon: iconNozzle,
  setup(s) { s.st.tg = []; s.st.cd = 0; s.player.x = 150; s.player.y = 170; },
  update(s, dt, lv) {
    const P = s.player, T = tierOf(lv);
    {
      const n = Math.min(lv, 5), L5 = Math.min(lv, 5), used = new Set(); s.st.tg = [];
      for (let i = 0; i < n; i++) { const m = s.nearest(P.x, P.y, 210 + L5 * 12, used); if (m) { used.add(m); s.st.tg.push(m); } }
      s.st.cd -= dt;
      if (s.st.cd <= 0) {
        s.st.cd = [0, .2, .18, .16, .13, .11][L5];
        const dmg = [0, 1.6, 1.6, 1.8, 2, 2.4][L5], reach = L5 >= 3 ? 60 + L5 * 14 : 0, w = 3 + L5 * 1.3;
        for (const m of s.st.tg) {
          const dx = m.x - P.x, dy = m.y - P.y, L = Math.hypot(dx, dy) || 1, ex = m.x + dx / L * reach, ey = m.y + dy / L * reach;
          for (const o of s.alive()) if (o === m || (reach && segDist(o.x, o.y, P.x, P.y, ex, ey) < o.r + w * .5)) { o.hit(s, dmg, dx / L * (30 + lv * 14), dy / L * (30 + lv * 14)); }
          hitFx(s, m.x, m.y - 4, lv, .6);
          if (L5 >= 5 && !(m.cd.hs > s.t)) { m.cd.hs = s.t + .3; splash(s, ex, ey, 26, 6); s.hitArea(ex, ey, 26, 1.5, 120); }
        }
      }
      if (lv < 6) return;
    }
    // 고압 방수포: 굵은 물기둥이 부채꼴로 쓸고, 양옆 물줄기 4개가 같이 쏜다.
    const c = s.crowd() || { x: P.x + 100, y: 60 }; const base = Math.atan2(c.y - P.y, c.x - P.x);
    s.st.a = base + Math.sin(s.t * 3.2) * 1.1; const a = s.st.a, len = 330;
    const x1 = P.x + Math.cos(a) * len, y1 = P.y + Math.sin(a) * len;
    s.st.cd -= dt;
    if (s.st.cd <= 0) {
      s.st.cd = .07;
      for (const m of s.alive()) if (segDist(m.x, m.y, P.x, P.y, x1, y1) < 36 + m.r) { m.hit(s, 6, Math.cos(a) * 110, Math.sin(a) * 110); if (Math.random() < .5) hitFx(s, m.x, m.y - 4, 6, .5); }
    }
        for (let i = 0; i < 4; i++) { const u = rand(.15, 1); part(s, { kind: 'drop', x: P.x + Math.cos(a) * len * u + rand(-10, 10), y: P.y + Math.sin(a) * len * u + rand(-10, 10), vz: rand(40, 120), grav: 500, vx: rand(-60, 60), vy: rand(-60, 60), life: .6, size: rand(2.4, 4) }); }
    s.shake = Math.max(s.shake, 2.2);
  },
  drawAir(s, g, lv) {
    const P = s.player, sx = P.x + 6, sy = P.y - 6, sg = 1 + s.surge * .9;
    for (const m of s.st.tg) tierStream(g, sx, sy, m.x, m.y - 4, (3 + lv * 1.3) * sg * (lv >= 6 ? .8 : 1), Math.min(lv, 5), s.t);
    // 꿰뚫는 꼬리(Lv3~)
    if (lv >= 3) for (const m of s.st.tg) { const L5 = Math.min(lv, 5), dx = m.x - P.x, dy = m.y - P.y, L = Math.hypot(dx, dy) || 1, reach = 60 + L5 * 14; g.save(); g.globalCompositeOperation = 'lighter'; const lg = g.createLinearGradient(m.x, m.y, m.x + dx / L * reach, m.y + dy / L * reach); lg.addColorStop(0, `rgba(${tierOf(lv).core},.8)`); lg.addColorStop(1, `rgba(${tierOf(lv).core},0)`); g.strokeStyle = lg; g.lineWidth = 2 + L5; g.lineCap = 'round'; g.beginPath(); g.moveTo(m.x, m.y - 4); g.lineTo(m.x + dx / L * reach, m.y - 4 + dy / L * reach); g.stroke(); g.restore(); }
    tierGlow(g, sx, sy, 6 + lv, lv, s.t);
    if (lv < 6) return;
    const a = s.st.a ?? -1, x1 = P.x + Math.cos(a) * 330, y1 = P.y + Math.sin(a) * 330;
    g.save(); g.lineCap = 'round'; g.globalCompositeOperation = 'lighter';
    g.strokeStyle = `hsla(${hue(s.t)},100%,70%,.35)`; g.lineWidth = 64; g.beginPath(); g.moveTo(sx, sy); g.lineTo(x1, y1); g.stroke();
    g.strokeStyle = 'rgba(80,170,255,.55)'; g.lineWidth = 44; g.stroke();
    g.strokeStyle = 'rgba(160,220,255,.8)'; g.lineWidth = 26; g.stroke();
    g.strokeStyle = 'rgba(255,255,255,.95)'; g.lineWidth = 10; g.stroke();
    g.globalCompositeOperation = 'source-over';
    g.strokeStyle = 'rgba(255,255,255,.9)'; g.lineWidth = 2.5; g.setLineDash([14, 16]); g.lineDashOffset = -s.t * 700; g.beginPath(); g.moveTo(sx, sy); g.lineTo(x1, y1); g.stroke(); g.setLineDash([]);
    // 노즐 앞 빛 덩어리
    g.globalCompositeOperation = 'lighter'; const gl = g.createRadialGradient(sx, sy, 0, sx, sy, 40); gl.addColorStop(0, 'rgba(255,255,255,.95)'); gl.addColorStop(.4, 'rgba(255,230,160,.5)'); gl.addColorStop(1, 'rgba(255,200,100,0)'); g.fillStyle = gl; g.beginPath(); g.arc(sx, sy, 40, 0, TAU); g.fill();
    g.restore();
  },
});

// ---------------------------------------------------------------- 2. 회전 스프링클러 → 물 왕관
function drawSprinklerHead(g, x, y, spin, lv, t, sc = 1) {
  const T = tierOf(lv);
  shadowAt(g, x, y + 8, 9 * sc, .3);
  g.save(); g.translate(x, y - 6); g.scale(sc, sc);
  tierGlow(g, 0, 0, 9, Math.max(2, lv), t);
  const gr = g.createRadialGradient(-2, -3, 1, 0, 0, 8);
  if (lv >= 5) { gr.addColorStop(0, '#fffbe0'); gr.addColorStop(1, '#d99a12'); } else { gr.addColorStop(0, '#f2f6ff'); gr.addColorStop(1, lv >= 3 ? '#7aa8d8' : '#8d97a8'); }
  g.fillStyle = gr; g.strokeStyle = lv >= 5 ? '#5a3a00' : '#26303f'; g.lineWidth = 1.5; g.beginPath(); g.arc(0, 0, 7, 0, TAU); g.fill(); g.stroke();
  g.rotate(spin); g.lineCap = 'round';
  const arms = lv >= 4 ? 4 : 3;
  for (let i = 0; i < arms; i++) { const a = i * TAU / arms; g.strokeStyle = lv >= 5 ? '#ffe9a0' : '#d9dde6'; g.lineWidth = 3; g.beginPath(); g.moveTo(0, 0); g.lineTo(Math.cos(a) * 11, Math.sin(a) * 11); g.stroke(); g.fillStyle = `rgb(${T.core})`; g.beginPath(); g.arc(Math.cos(a) * 11, Math.sin(a) * 11, 2.6, 0, TAU); g.fill(); }
  g.restore();
}
item({
  id: 'sprinkler', group: '무기', name: '회전 스프링클러', evoName: '물 왕관', target: 'house', sides: ['top', 'topright', 'left'],
  lvText: [null, '스프링클러 2개가 내 둘레를 돈다', '더 넓게 · 더 세게 튕긴다', '스프링클러 3개 · 물줄기를 뿜는다', '4갈래 노즐 · 더 빨리 돈다', '스프링클러 4개 · 금빛 · 물보라 폭발', '물 왕관: 물 고리 + 8갈래 무지개 분사'],
  icon(g, x, y, r, lv, t) { drawSprinklerHead(g, x, y + r * .5, t * 10, lv, t, r / 11); },
  setup(s) { s.st.a = 0; s.player.x = 250; s.player.y = 165; s.st.heads = []; s.st.jet = 0; },
  onLevel(s, lv) {
    // 새로 는 스프링클러가 빛나며 나타난다.
    const n = [0, 2, 2, 3, 3, 4, 4][lv], R = [0, 44, 52, 60, 68, 76, 80][lv];
    for (let i = 0; i < n; i++) { const a = s.st.a + i * TAU / n; const x = s.player.x + Math.cos(a) * R, y = s.player.y + Math.sin(a) * R * .72; part(s, { kind: 'glow', x, y: y - 6, life: .5, size: 30, color: tierOf(lv).glow }); part(s, { kind: 'ring', x, y: y - 6, life: .45, size: 26, color: '255,255,255', width: 3 }); }
  },
  update(s, dt, lv) {
    const P = s.player, n = [0, 2, 2, 3, 3, 4, 4][lv], R = [0, 44, 52, 60, 68, 76, 80][lv];
    s.st.a += dt * [0, 2.4, 2.6, 2.8, 3.3, 3.6, 3.8][lv]; s.st.heads = [];
    const dmg = [0, 2.1, 2.1, 2.2, 2.6, 4.2, 4.2][lv], push = 160 + lv * 40;
    for (let i = 0; i < n; i++) {
      const a = s.st.a + i * TAU / n, x = P.x + Math.cos(a) * R, y = P.y + Math.sin(a) * R * .72; s.st.heads.push({ x, y, a });
      if (Math.random() < dt * (14 + lv * 8)) { const sa = a + s.t * 8 + rand(-.3, .3); part(s, { kind: 'drop', x, y: y - 6, vx: Math.cos(sa) * (100 + lv * 20), vy: Math.sin(sa) * (70 + lv * 15), vz: rand(30, 80), grav: 400, life: .6, size: rand(2, 2.4 + lv * .25) }); }
      for (const m of s.alive()) if (Math.hypot(m.x - x, m.y - y) < m.r + 15 + lv * 1.5 && !(m.cd.sp > s.t)) { m.cd.sp = s.t + [0, .34, .3, .25, .2, .15, .15][lv]; const L = Math.hypot(m.x - P.x, m.y - P.y) || 1; m.hit(s, dmg, (m.x - P.x) / L * push, (m.y - P.y) / L * push); hitFx(s, m.x, m.y - 4, lv, .8); if (lv >= 5) { splash(s, m.x, m.y, 30, 6); s.hitArea(m.x, m.y, 30, 2, 140); } }
      // Lv3~: 머리에서 바깥으로 짧은 물줄기
      if (lv >= 3 && lv < 6) { const ja = a + s.t * 5, x1 = x + Math.cos(ja) * (30 + lv * 6), y1 = y + Math.sin(ja) * (30 + lv * 6) * .72; for (const m of s.alive()) if (segDist(m.x, m.y, x, y, x1, y1) < m.r + 3 && !(m.cd['sj' + i] > s.t)) { m.cd['sj' + i] = s.t + .3; m.hit(s, dmg * .6, Math.cos(ja) * 120, Math.sin(ja) * 120); } s.st.heads[i].jet = { x1, y1 }; }
    }
    if (lv === 6) {
      s.st.jet += dt * 1.6;
      for (let j = 0; j < 8; j++) { const a = s.st.jet + j * TAU / 8, x0 = P.x + Math.cos(a) * R, y0 = P.y + Math.sin(a) * R * .72, x1 = P.x + Math.cos(a) * (R + 150), y1 = P.y + Math.sin(a) * (R + 150) * .72;
        for (const m of s.alive()) if (segDist(m.x, m.y, x0, y0, x1, y1) < m.r + 7 && !(m.cd['j' + j] > s.t)) { m.cd['j' + j] = s.t + .2; m.hit(s, 4, Math.cos(a) * 260, Math.sin(a) * 200); hitFx(s, m.x, m.y - 4, 6, .5); } }
      for (const m of s.alive()) { const d = Math.hypot(m.x - P.x, (m.y - P.y) / .72); if (Math.abs(d - R) < 12 && !(m.cd.rg > s.t)) { m.cd.rg = s.t + .2; m.hit(s, 3, (m.x - P.x) * 4, (m.y - P.y) * 4); } }
    }
  },
  drawGround(s, g, lv) {
    const P = s.player, R = [0, 44, 52, 60, 68, 76, 80][lv], T = tierOf(lv);
    g.save();
    g.strokeStyle = `rgba(${T.glow},${.18 + lv * .04})`; g.lineWidth = 2; g.setLineDash([4, 6]); g.lineDashOffset = -s.t * 30; g.beginPath(); g.ellipse(P.x, P.y, R, R * .72, 0, 0, TAU); g.stroke(); g.setLineDash([]);
    if (lv >= 5) { g.globalCompositeOperation = 'lighter'; for (let j = 0; j < (lv === 6 ? 3 : 1); j++) { g.strokeStyle = lv === 6 ? `hsla(${hue(s.t, j * 120)},100%,68%,${.6 - j * .12})` : 'rgba(255,214,110,.45)'; g.lineWidth = (lv === 6 ? 9 : 4) - j * 2; const st = s.t * (4 + j * 2); g.beginPath(); g.ellipse(P.x, P.y - 4, R, R * .72, 0, st, st + TAU * .85); g.stroke(); } }
    g.restore();
  },
  ents(s, g, lv) { return (s.st.heads || []).map(h => ({ y: h.y, d: () => drawSprinklerHead(g, h.x, h.y, s.t * (12 + lv * 2), lv, s.t, (1 + (lv - 1) * .1) * (1 + s.surge * .6)) })); },
  drawAir(s, g, lv) {
    const P = s.player;
    for (const h of s.st.heads || []) if (h.jet) tierStream(g, h.x, h.y - 6, h.jet.x1, h.jet.y1 - 6, 2 + lv * .6, lv, s.t, 4);
    if (lv !== 6) return;
    const R = 80;
    g.save(); g.lineCap = 'round';
    for (let j = 0; j < 8; j++) { const a = s.st.jet + j * TAU / 8, x0 = P.x + Math.cos(a) * R, y0 = P.y - 6 + Math.sin(a) * R * .72, x1 = P.x + Math.cos(a) * (R + 150), y1 = P.y - 6 + Math.sin(a) * (R + 150) * .72;
      g.globalCompositeOperation = 'lighter'; g.strokeStyle = `hsla(${hue(s.t, j * 45)},100%,65%,.45)`; g.lineWidth = 16; g.beginPath(); g.moveTo(x0, y0); g.lineTo(x1, y1); g.stroke();
      const lg = g.createLinearGradient(x0, y0, x1, y1); lg.addColorStop(0, 'rgba(255,255,255,1)'); lg.addColorStop(.6, 'rgba(160,220,255,.7)'); lg.addColorStop(1, 'rgba(120,200,255,0)'); g.strokeStyle = lg; g.lineWidth = 6; g.beginPath(); g.moveTo(x0, y0); g.lineTo(x1, y1); g.stroke(); }
    // 왕관: 머리 위 금빛 왕관이 돈다
    g.globalCompositeOperation = 'source-over';
    g.translate(P.x, P.y - 40); const k = 1 + Math.sin(s.t * 6) * .05; g.scale(k, k);
    const cg = g.createLinearGradient(0, -10, 0, 8); cg.addColorStop(0, '#fffbe0'); cg.addColorStop(.5, '#ffd25a'); cg.addColorStop(1, '#c58a12');
    g.fillStyle = cg; g.strokeStyle = '#6a4200'; g.lineWidth = 1.4; g.beginPath(); g.moveTo(-12, 6); g.lineTo(-14, -6); g.lineTo(-7, 0); g.lineTo(0, -10); g.lineTo(7, 0); g.lineTo(14, -6); g.lineTo(12, 6); g.closePath(); g.fill(); g.stroke();
    for (const [x, c] of [[-7, '#5cc2ff'], [0, '#ff6fd8'], [7, '#7dffb0']]) { g.fillStyle = c; g.beginPath(); g.arc(x, 2, 2, 0, TAU); g.fill(); }
    g.restore();
  },
});
