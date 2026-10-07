'use strict';
// 구조대원 합류: 타는 집 문 앞에서 구한 사람이 방화복 대원으로 바뀌어 내 뒤에 줄을 서고, 가까운 요괴에 물을 쏜다(1 → 8명).

// ---------------------------------------------------------------- 두 번째 집(작은 빵집)
const HOUSE2 = { x: 118, y: 150, w: 74, h: 58, door: { x: 155, y: 210 } };
function drawHouse2(g) {
  const { x, y, w, h } = HOUSE2;
  g.fillStyle = 'rgba(0,0,0,.28)'; g.beginPath(); g.ellipse(x + w / 2 + 5, y + h + 4, w * .6, 9, 0, 0, TAU); g.fill();
  const wall = g.createLinearGradient(0, y + h * .45, 0, y + h); wall.addColorStop(0, '#f6ead0'); wall.addColorStop(1, '#d9c69f');
  g.fillStyle = wall; g.fillRect(x + 4, y + h * .45, w - 8, h * .58);
  g.fillStyle = '#6b3f24'; g.fillRect(x + w / 2 - 7, y + h * .68, 14, h * .35);
  g.fillStyle = '#ffe9a8'; g.fillRect(x + 11, y + h * .62, 12, 10); g.fillRect(x + w - 23, y + h * .62, 12, 10);
  const rg = g.createLinearGradient(0, y, 0, y + h * .52); rg.addColorStop(0, '#5a8fe0'); rg.addColorStop(1, '#2e5aa8');
  g.fillStyle = rg; g.beginPath(); g.roundRect(x - 3, y, w + 6, h * .52, 6); g.fill();
  g.strokeStyle = 'rgba(0,0,0,.35)'; g.lineWidth = 2; g.stroke();
  g.fillStyle = 'rgba(255,255,255,.18)'; g.fillRect(x, y + 3, w, 4);
  g.fillStyle = '#222'; g.beginPath(); g.roundRect(x + 15, y + h * .48 - 2, w - 30, 11, 3); g.fill();
  g.fillStyle = '#fff'; g.font = 'bold 8.5px "Apple SD Gothic Neo",sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle';
  g.fillText('빵집', x + w / 2, y + h * .48 + 3.5);
}

/** 집 불: 지붕·창 위 불꽃 + 주황 빛 + 연기. k = 불 세기(0~1). */
function houseFire(s, g, hx, hy, w, h, k, seed) {
  if (k <= 0.02) return;
  g.save(); g.globalCompositeOperation = 'lighter';
  const cx = hx + w / 2, cy = hy + h * .3;
  const gl = g.createRadialGradient(cx, cy, 0, cx, cy, w * 1.1);
  gl.addColorStop(0, `rgba(255,140,40,${.45 * k})`); gl.addColorStop(1, 'rgba(255,60,0,0)');
  g.fillStyle = gl; g.beginPath(); g.arc(cx, cy, w * 1.1, 0, TAU); g.fill();
  const n = 5;
  for (let i = 0; i < n; i++) {
    const fx = hx + 6 + (w - 12) * (i / (n - 1)), fl = 1 + Math.sin(s.t * (17 + i * 3) + seed + i * 1.7) * .22;
    flameTuft(g, fx, hy + 4, (7 + (i % 2) * 3) * k * fl);
  }
  // 창에서 새는 불
  flameTuft(g, hx + 16, hy + h * .62, 5 * k * (1 + Math.sin(s.t * 20 + seed) * .2));
  flameTuft(g, hx + w - 16, hy + h * .62, 5 * k * (1 + Math.cos(s.t * 19 + seed) * .2));
  g.restore();
  if (Math.random() < .25 * k) part(s, { kind: 'smoke', x: hx + rand(8, w - 8), y: hy - 4, vx: rand(-6, 6), vy: rand(-30, -18), life: rand(.8, 1.3), size: rand(5, 9), color: '90,80,95' });
  if (Math.random() < .4 * k) part(s, { kind: 'spark', x: hx + rand(6, w - 6), y: hy, vx: rand(-20, 20), vy: rand(-70, -30), life: rand(.4, .8), size: rand(1.4, 2.4), color: '255,170,60' });
}

// ---------------------------------------------------------------- 사람 그림
const SHIRTS = [['#ff8fb1', '#d64f7c'], ['#8fe3a0', '#3f9c58'], ['#ffd36b', '#cf9420'], ['#a99bff', '#6450d6'], ['#7fd6ff', '#2f8dc9'], ['#ffab7a', '#d4642a'], ['#f2f2f2', '#a9b0bd'], ['#c8f07a', '#79a830']];
const HAIRS = ['#3b2414', '#1c1410', '#7a4a22', '#2a1f3d'];

/** 구한 사람(평상복): 동그란 얼굴·머리카락·셔츠·바지. 두 손을 흔든다. */
function drawCivilian(g, x, y, idx, t, wave = 1) {
  const [c0, c1] = SHIRTS[idx % SHIRTS.length];
  g.save(); g.translate(x, y);
  g.lineWidth = 1.6; g.strokeStyle = '#1d1a26';
  // 다리
  g.fillStyle = '#34405a'; g.beginPath(); g.roundRect(-5.5, 2, 4.5, 8, 2); g.fill(); g.stroke(); g.beginPath(); g.roundRect(1, 2, 4.5, 8, 2); g.fill(); g.stroke();
  // 팔(흔들기)
  const wv = Math.sin(t * 16) * .5 * wave;
  for (const sd of [-1, 1]) { g.save(); g.translate(sd * 7, -4); g.rotate(sd * (-2.3 + wv * sd)); const ag = g.createLinearGradient(0, 0, 0, 8); ag.addColorStop(0, c0); ag.addColorStop(1, c1); g.fillStyle = ag; g.beginPath(); g.roundRect(-2, 0, 4, 8, 2); g.fill(); g.stroke(); g.fillStyle = '#ffd9b8'; g.beginPath(); g.arc(0, 9, 2.1, 0, TAU); g.fill(); g.stroke(); g.restore(); }
  // 몸
  const bg = g.createLinearGradient(0, -7, 0, 4); bg.addColorStop(0, c0); bg.addColorStop(1, c1);
  g.fillStyle = bg; g.beginPath(); g.roundRect(-7, -7, 14, 11, 5); g.fill(); g.stroke();
  // 머리
  const hg = g.createRadialGradient(-2, -15, 1, 0, -13, 8); hg.addColorStop(0, '#fff0e0'); hg.addColorStop(1, '#f2b98e');
  g.fillStyle = hg; g.beginPath(); g.arc(0, -13, 7, 0, TAU); g.fill(); g.stroke();
  g.fillStyle = HAIRS[idx % HAIRS.length]; g.beginPath(); g.arc(0, -14.5, 7, Math.PI * 1.05, Math.PI * 1.95); g.closePath(); g.fill();
  g.fillStyle = '#1d1a26'; g.beginPath(); g.arc(-2.4, -12.5, 1.1, 0, TAU); g.arc(2.4, -12.5, 1.1, 0, TAU); g.fill();
  g.strokeStyle = '#a0452e'; g.lineWidth = 1.2; g.beginPath(); g.arc(0, -10.5, 2, .2, Math.PI - .2); g.stroke();
  g.fillStyle = 'rgba(255,120,120,.5)'; g.beginPath(); g.arc(-4.4, -10.8, 1.4, 0, TAU); g.arc(4.4, -10.8, 1.4, 0, TAU); g.fill();
  g.restore();
}

/** 구조대원(방화복): 주황 방화복 + 반사띠 + 노란 헬멧 + 손에 노즐. aim이 있으면 그쪽으로 노즐. */
function drawCrewman(g, x, y, idx, t, face, run, aimA) {
  g.save(); g.translate(x, y);
  shadowAt(g, 0, 9, 9, .32);
  g.scale(face, 1);
  g.lineWidth = 1.6; g.strokeStyle = '#2a1206';
  const r = run ? Math.sin(t * 18 + idx) : 0;
  // 장화
  g.fillStyle = '#1c1c22'; g.beginPath(); g.ellipse(-3.5, 8 + r * 1.5, 3.2, 3.4, 0, 0, TAU); g.fill(); g.beginPath(); g.ellipse(3.5, 8 - r * 1.5, 3.2, 3.4, 0, 0, TAU); g.fill();
  // 몸(방화복)
  const bg = g.createLinearGradient(0, -8, 0, 7); bg.addColorStop(0, '#ffb347'); bg.addColorStop(.6, '#f27a12'); bg.addColorStop(1, '#b44d06');
  g.fillStyle = bg; g.beginPath(); g.roundRect(-7.5, -7, 15, 14, 5); g.fill(); g.stroke();
  // 반사띠
  g.fillStyle = '#e9f0f2'; g.fillRect(-7.5, 0, 15, 2.4); g.fillStyle = '#d6ff3d'; g.fillRect(-7.5, 2.4, 15, 1.2);
  // 팔 + 노즐(겨눈 쪽)
  g.save(); g.translate(5, -3);
  const la = aimA === null || aimA === undefined ? .7 : (face > 0 ? aimA : Math.PI - aimA);
  g.rotate(la);
  g.fillStyle = '#f27a12'; g.beginPath(); g.roundRect(0, -2, 8, 4, 2); g.fill(); g.stroke();
  const ng = g.createLinearGradient(0, -2, 0, 2); ng.addColorStop(0, '#fff2b8'); ng.addColorStop(1, '#b3801a');
  g.fillStyle = ng; g.beginPath(); g.moveTo(7, -2.2); g.lineTo(12, -1.3); g.lineTo(12, 1.3); g.lineTo(7, 2.2); g.closePath(); g.fill(); g.stroke();
  g.restore();
  // 얼굴
  const hg = g.createRadialGradient(-2, -14, 1, 0, -12, 7); hg.addColorStop(0, '#fff0e0'); hg.addColorStop(1, '#f2b98e');
  g.fillStyle = hg; g.beginPath(); g.arc(0, -12, 6, 0, TAU); g.fill(); g.stroke();
  g.fillStyle = '#1d1a26'; g.beginPath(); g.arc(1, -11.5, 1, 0, TAU); g.arc(4, -11.5, 1, 0, TAU); g.fill();
  // 헬멧
  const hl = g.createRadialGradient(-2, -19, 1, 0, -15, 9); hl.addColorStop(0, '#fff6b0'); hl.addColorStop(.5, '#ffd21f'); hl.addColorStop(1, '#c79400');
  g.fillStyle = hl; g.beginPath(); g.arc(0, -14.5, 7.4, Math.PI, 0); g.closePath(); g.fill(); g.stroke();
  g.beginPath(); g.ellipse(1, -14.2, 10, 2.6, 0, 0, TAU); g.fill(); g.stroke();
  g.fillStyle = '#e8352a'; g.beginPath(); g.arc(0, -18.5, 2, 0, TAU); g.fill();
  g.fillStyle = 'rgba(255,255,255,.7)'; g.beginPath(); g.ellipse(-3, -18.5, 2.2, 1.1, -.5, 0, TAU); g.fill();
  g.restore();
}

/** HUD용 헬멧 아이콘. on=false면 빈 자리. */
function helmetIcon(g, x, y, r, on, t, glow) {
  g.save(); g.translate(x, y);
  if (on && glow > 0) { g.globalCompositeOperation = 'lighter'; const gl = g.createRadialGradient(0, 0, 0, 0, 0, r * 2.4); gl.addColorStop(0, `rgba(255,210,80,${.7 * glow})`); gl.addColorStop(1, 'rgba(255,160,40,0)'); g.fillStyle = gl; g.beginPath(); g.arc(0, 0, r * 2.4, 0, TAU); g.fill(); g.globalCompositeOperation = 'source-over'; }
  g.lineWidth = 1.2; g.strokeStyle = on ? '#4a3300' : 'rgba(120,130,150,.6)';
  const hl = g.createRadialGradient(-r * .3, -r * .5, 1, 0, 0, r * 1.2);
  if (on) { hl.addColorStop(0, '#fff6b0'); hl.addColorStop(.5, '#ffd21f'); hl.addColorStop(1, '#c79400'); } else { hl.addColorStop(0, 'rgba(70,78,98,.9)'); hl.addColorStop(1, 'rgba(40,46,62,.9)'); }
  g.fillStyle = hl; g.beginPath(); g.arc(0, r * .2, r, Math.PI, 0); g.closePath(); g.fill(); g.stroke();
  g.beginPath(); g.ellipse(0, r * .25, r * 1.35, r * .32, 0, 0, TAU); g.fill(); g.stroke();
  if (on) { g.fillStyle = '#e8352a'; g.beginPath(); g.arc(0, -r * .45, r * .26, 0, TAU); g.fill(); }
  g.restore();
}

// ---------------------------------------------------------------- 장면
// 줄 자리: 내 뒤(아래쪽) 반원 두 줄.
const CREW_SLOTS = [
  [30, 0.55], [30, 2.59], [32, 1.25], [32, 1.89],
  [50, 0.32], [50, 2.82], [52, 0.95], [52, 2.19],
];
const CREW_MAX = 8;
const DOOR_A = { x: HOUSE.door.x, y: HOUSE.door.y + 14 }, DOOR_B = { x: HOUSE2.door.x, y: HOUSE2.door.y + 16 };

class CrewScene {
  constructor(it) { this.item = it; this.dur = 19; this.reset(); }
  reset() {
    this.t = 0; this.lv = 1; this.mobs = []; this.parts = []; this.shake = 0; this.flash = 0; this.flashColor = '255,240,200'; this.slow = 0; this.stop = 0; this.warn = 0; this.shield = 0;
    this.player = { x: DOOR_A.x - 60, y: DOOR_A.y + 10, moving: false, face: 1 };
    this.civ = []; this.crew = []; this.ring = 0; this.rest = 0; this.rescued = 0;
    this.left = { A: 4, B: 4 }; this.banner = null; this.joinFx = []; this.spawnClock = 0; this.spawned = 0; this.hudGlow = 0;
    for (let i = 0; i < 10; i++) { this.spawnOne(); const m = this.mobs[this.mobs.length - 1]; m.x = rand(110, 300); m.y = rand(110, 250); }
  }
  burn(m) { for (let i = 0; i < 6; i++) part(this, { kind: 'spark', x: m.x, y: m.y, vx: rand(-60, 60), vy: rand(-90, -20), life: .5, size: 3, color: '255,140,40' }); this.warn = .6; }
  gem(x, y) { if (Math.random() > .35) return; part(this, { kind: 'gem', x, y, z: 0, vz: 120, grav: 500, life: 1.6 }); }
  spawnOne() {
    const sides = ['left', 'top', 'bottom', 'right'], side = sides[this.spawned % 4];
    let x, y;
    if (side === 'left') { x = rand(220, 330); y = -14; } else if (side === 'top') { x = rand(260, 360); y = -14; } else if (side === 'bottom') { x = rand(120, 360); y = H + 14; } else { x = W + 14; y = rand(60, 250); }
    // 지금 구하는 집 쪽 문을 노린다.
    const d = this.rescued < 4 ? HOUSE.door : HOUSE2.door;
    const big = this.spawned % 6 === 5;
    const m = new Mob(x, y, d.x + rand(-30, 30), d.y + rand(-8, 10), big);
    m.hp = big ? 7 : 3; m.speed *= 1.25;
    this.mobs.push(m); this.spawned++;
  }
  alive() { return this.mobs.filter(m => !m.dead && m.z <= 0 && !m.held && m.x > -4 && m.x < W + 4 && m.y > -4 && m.y < H + 4); }
  nearest(x, y, max, skip) { let b = null, bd = max; for (const m of this.alive()) { if (skip && skip.has(m)) continue; const d = Math.hypot(m.x - x, m.y - y); if (d < bd) { b = m; bd = d; } } return b; }
  hitArea(x, y, R, dmg, push = 0) { let n = 0; for (const m of this.alive()) { const d = Math.hypot(m.x - x, m.y - y); if (d < R + m.r) { const L = d || 1; m.hit(this, dmg, (m.x - x) / L * push, (m.y - y) / L * push); n++; } } return n; }

  /** 구조: 문에서 사람이 튀어나온다. */
  rescue(door, key) {
    this.left[key]--; this.rescued++;
    const idx = this.rescued - 1;
    this.civ.push({ x: door.x, y: door.y - 2, sx: door.x, sy: door.y - 2, tx: door.x + (key === 'A' ? -26 : 26), ty: door.y + 14, age: 0, idx, state: 'pop' });
    for (let i = 0; i < 16; i++) { const a = rand(0, TAU), v = rand(60, 150); part(this, { kind: 'star', x: door.x, y: door.y - 8, vx: Math.cos(a) * v, vy: Math.sin(a) * v * .7, life: rand(.4, .7), size: rand(2.5, 4), color: '255,240,190' }); }
    part(this, { kind: 'ring', x: door.x, y: door.y, life: .35, size: 30, color: '255,230,160', width: 3 });
    for (let i = 0; i < 6; i++) part(this, { kind: 'smoke', x: door.x + rand(-6, 6), y: door.y - 6, vx: rand(-20, 20), vy: rand(-30, -10), life: .6, size: rand(5, 8), color: '120,110,120' });
  }
  /** 변신: 평상복 → 방화복. 합류 수가 늘수록 더 화려하다. */
  transform(c) {
    const n = this.crew.length + 1, x = c.x, y = c.y - 8;
    const gold = n >= 5, rb = n >= CREW_MAX;
    const col = rb ? null : gold ? '255,215,110' : '255,236,180';
    this.joinFx.push({ x, y: c.y, age: 0, n });
    for (let i = 0; i < Math.min(4, 1 + Math.floor(n / 2)); i++) part(this, { kind: rb ? 'prism' : 'ring', x, y: c.y, life: .45 + i * .12, size: 34 + i * 18 + n * 3, color: col || '255,255,255', width: 5 - i });
    part(this, { kind: 'glow', x, y, life: .45, size: 50 + n * 7, color: rb ? '255,240,210' : '255,210,120' });
    const ns = 18 + n * 10;
    for (let i = 0; i < ns; i++) { const a = rand(0, TAU), v = rand(70, 170 + n * 15); part(this, { kind: 'star', x, y, vx: Math.cos(a) * v, vy: Math.sin(a) * v * .75, life: rand(.45, .9), size: rand(2.5, 3.5 + n * .35), color: col }); }
    if (n >= 4) for (let i = 0; i < (n - 3) * 14; i++) { const a = rand(-Math.PI, 0), v = rand(100, 230); part(this, { kind: 'confetti', x, y: y - 8, vx: Math.cos(a) * v, vy: Math.sin(a) * v, life: rand(.9, 1.5), size: rand(1.8, 3), rot: rand(0, TAU), vr: rand(-8, 8), color: rb ? null : (Math.random() < .5 ? '255,140,40' : '255,215,110') }); }
    // 둘레 요괴를 밀어낸다
    for (const m of this.alive()) { const d = Math.hypot(m.x - x, m.y - y); if (d < 50 + n * 6) { const L = d || 1; m.vx += (m.x - x) / L * 220; m.vy += (m.y - y) / L * 220; m.hit(this, 1); } }
    this.shake = Math.max(this.shake, 2.5 + n * .7);
    this.flash = Math.max(this.flash, .12 + n * .035); this.flashColor = rb ? '255,250,240' : '255,235,190';
    this.stop = Math.max(this.stop, .03 + n * .008);
    this.slow = Math.max(this.slow, .25 + n * .05);
    this.banner = { n, age: 0 };
    this.hudGlow = 1;
    const slot = this.crew.length;
    this.crew.push({ x: c.x, y: c.y, idx: c.idx, slot, cd: rand(.2, .5), aim: null, aimAge: 0, face: 1, run: true, born: this.t });
  }
  update(dt) {
    const real = dt;
    if (this.stop > 0) { this.stop -= real; dt = 0; } else if (this.slow > 0) { this.slow -= real; dt *= .35; }
    this.t += dt;
    const P = this.player;
    // 소방관: 아직 사람이 남은 문으로 간다. 다 구하면 두 문 사이를 오간다.
    let goal, key;
    if (this.left.A > 0) { goal = DOOR_A; key = 'A'; } else if (this.left.B > 0) { goal = DOOR_B; key = 'B'; }
    else { const k = Math.floor(this.t / 2.2) % 2; goal = k ? { x: 300, y: 150 } : { x: 250, y: 200 }; key = null; }
    const dx = goal.x - P.x, dy = goal.y - P.y, L = Math.hypot(dx, dy);
    if (L > 3) { const v = Math.min(L, 185 * dt); P.x += dx / L * v; P.y += dy / L * v; P.moving = true; if (Math.abs(dx) > 1) P.face = dx > 0 ? 1 : -1; this.ring = 0; }
    else {
      P.moving = false;
      if (key) {
        if (this.rest > 0) this.rest -= dt;
        else { this.ring += dt / .85; if (this.ring >= 1) { this.ring = 0; this.rest = .35; this.rescue(key === 'A' ? HOUSE.door : HOUSE2.door, key); } }
      }
    }
    // 구한 사람: 튀어나옴(0.45초) → 변신(0.3초) → 대원
    for (const c of this.civ) {
      c.age += dt;
      if (c.state === 'pop') { const u = ease(c.age / .45); c.x = lerp(c.sx, c.tx, u); c.y = lerp(c.sy, c.ty, u); c.z = Math.sin(Math.min(1, c.age / .45) * Math.PI) * 22; if (c.age >= .45) { c.state = 'morph'; c.age = 0; c.z = 0; } }
      else if (c.state === 'morph') { if (Math.random() < .6) part(this, { kind: 'star', x: c.x + rand(-8, 8), y: c.y - rand(0, 24), vy: -30, life: .4, size: 2.5, color: '255,250,220' }); if (c.age >= .3) { c.state = 'done'; this.transform(c); } }
    }
    this.civ = this.civ.filter(c => c.state !== 'done');
    // 대원: 자리로 달려가 따라오고, 가까운 요괴에 물을 쏜다.
    const n = this.crew.length;
    const lvl = clamp(1 + Math.floor(n / 2), 1, 5);
    for (const c of this.crew) {
      const [R, a] = CREW_SLOTS[c.slot];
      const tx = P.x + Math.cos(a) * R, ty = P.y + 6 + Math.sin(a) * R * .62;
      const ddx = tx - c.x, ddy = ty - c.y, dl = Math.hypot(ddx, ddy);
      const sp = c.run && this.t - c.born < 1.2 ? 260 : 200;
      if (dl > 2) { const v = Math.min(dl, sp * dt); c.x += ddx / dl * v; c.y += ddy / dl * v; c.moving = true; } else c.moving = false;
      c.cd -= dt; c.aimAge -= dt;
      if (c.cd <= 0) {
        const m = this.nearest(c.x, c.y, 125 + n * 3);
        if (m) {
          c.aim = m; c.aimAge = .26; c.cd = .42 - n * .015;
          m.hit(this, 1.5, (m.x - c.x) * .6, (m.y - c.y) * .6);
          hitFx(this, m.x, m.y - 4, lvl, .55);
        } else c.cd = .15;
      }
      if (c.aim && (c.aim.dead || c.aimAge <= 0)) { if (c.aimAge <= 0) c.aim = null; }
      const fx = c.aim && !c.aim.dead ? c.aim.x - c.x : ddx;
      if (Math.abs(fx) > 1) c.face = fx > 0 ? 1 : -1;
    }
    // 요괴
    this.spawnClock -= dt;
    while (this.spawnClock <= 0) { if (this.alive().length < 22) this.spawnOne(); this.spawnClock += 1 / 3.2; }
    for (const m of this.mobs) m.update(this, dt);
    this.mobs = this.mobs.filter(m => !m.dead);
    stepParts(this, dt);
    for (const j of this.joinFx) j.age += real;
    this.joinFx = this.joinFx.filter(j => j.age < 1.6);
    if (this.banner) { this.banner.age += real; if (this.banner.age > 1.5) this.banner = null; }
    this.hudGlow = Math.max(0, this.hudGlow - real * 1.2);
    this.shake *= Math.pow(.002, real); this.flash = Math.max(0, this.flash - real * 1.4); this.warn -= dt;
    if (this.t > this.dur) this.reset();
  }

  /** 합류 빛기둥 + 광선(뒤쪽). */
  drawJoinBack(g) {
    for (const j of this.joinFx) {
      const k = j.age / 1.4, a = (j.age < .1 ? j.age / .1 : 1) * Math.max(0, 1 - k * k);
      if (a <= 0) continue;
      const gold = j.n >= 5, rb = j.n >= CREW_MAX;
      g.save(); g.globalCompositeOperation = 'lighter';
      // 빛기둥
      const pw = 14 + j.n * 1.5;
      const pc = rb ? hsl2rgb(hue(this.t)) : gold ? '255,214,110' : '255,240,200';
      const top = j.y - 160;
      for (const [ww, al, c] of [[pw, .22, pc], [pw * .55, .35, pc], [2.5, .8, '255,255,255']]) {
        const vg = g.createLinearGradient(0, top, 0, j.y); vg.addColorStop(0, `rgba(${c},0)`); vg.addColorStop(.7, `rgba(${c},${al * a})`); vg.addColorStop(1, `rgba(${c},${al * a * 1.2})`);
        g.fillStyle = vg; g.beginPath(); g.roundRect(j.x - ww, top, ww * 2, 160, ww); g.fill();
      }
      // 광선
      const nr = 10 + j.n * 4, len = (60 + j.n * 14) * (.6 + ease(j.age / .3) * .4);
      g.translate(j.x, j.y - 10); g.rotate(j.age * 1.2);
      for (let i = 0; i < nr; i++) {
        const ang = i / nr * TAU, c = rb ? hsl2rgb(hue(this.t, i * 360 / nr)) : gold ? (i % 2 ? '255,214,110' : '255,248,220') : (i % 2 ? '255,200,120' : '255,245,225');
        const gr = g.createLinearGradient(0, 0, Math.cos(ang) * len, Math.sin(ang) * len);
        gr.addColorStop(0, `rgba(${c},${.7 * a})`); gr.addColorStop(1, `rgba(${c},0)`);
        g.fillStyle = gr; g.beginPath(); g.moveTo(0, 0); g.lineTo(Math.cos(ang - .07) * len, Math.sin(ang - .07) * len * .8); g.lineTo(Math.cos(ang + .07) * len, Math.sin(ang + .07) * len * .8); g.closePath(); g.fill();
      }
      g.restore();
    }
  }
  draw(g) {
    g.save();
    if (this.shake > .3) g.translate(rand(-1, 1) * this.shake, rand(-1, 1) * this.shake);
    g.drawImage(BG, 0, 0, W, H);
    drawHouse2(g);
    // 집 불: 남은 사람이 많을수록 세다.
    houseFire(this, g, HOUSE.x, HOUSE.y, HOUSE.w, HOUSE.h, this.left.A > 0 ? .55 + this.left.A * .12 : .15, 1);
    houseFire(this, g, HOUSE2.x, HOUSE2.y, HOUSE2.w, HOUSE2.h, this.left.B > 0 ? .5 + this.left.B * .12 : .12, 4);
    if (this.warn > 0) { g.globalCompositeOperation = 'lighter'; const d = this.rescued < 4 ? HOUSE.door : HOUSE2.door; const gr = g.createRadialGradient(d.x, d.y - 20, 0, d.x, d.y - 20, 60); gr.addColorStop(0, `rgba(255,120,30,${Math.min(.6, this.warn)})`); gr.addColorStop(1, 'rgba(255,60,0,0)'); g.fillStyle = gr; g.fillRect(d.x - 70, d.y - 90, 140, 140); g.globalCompositeOperation = 'source-over'; }
    // 갇힌 사람 수 표시(문 위 사람 아이콘)
    for (const [d, k] of [[HOUSE.door, 'A'], [HOUSE2.door, 'B']]) {
      const left = this.left[k]; if (left <= 0) continue;
      const ly = (k === 'A' ? HOUSE.y : HOUSE2.y) - 26; g.fillStyle = 'rgba(20,10,10,.75)'; g.beginPath(); g.roundRect(d.x - 20, ly - 7, 40, 14, 6); g.fill();
      g.strokeStyle = `rgba(255,90,60,${.6 + Math.sin(this.t * 8) * .3})`; g.lineWidth = 1.5; g.stroke();
      g.fillStyle = '#fff'; g.font = '800 9px "Apple SD Gothic Neo",sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle';
      g.fillText('갇힘 ' + left + '명', d.x, ly);
    }
    // 구조 진행 고리
    const P = this.player;
    if (this.ring > 0) {
      const d = this.left.A > 0 ? HOUSE.door : HOUSE2.door;
      g.save(); g.lineCap = 'round';
      g.strokeStyle = 'rgba(0,0,0,.45)'; g.lineWidth = 5; g.beginPath(); g.arc(d.x, d.y - 16, 13, 0, TAU); g.stroke();
      g.globalCompositeOperation = 'lighter';
      const rg = g.createLinearGradient(d.x - 13, 0, d.x + 13, 0); rg.addColorStop(0, '#7fe0ff'); rg.addColorStop(1, '#fff6b0');
      g.strokeStyle = rg; g.lineWidth = 3.5; g.beginPath(); g.arc(d.x, d.y - 16, 13, -Math.PI / 2, -Math.PI / 2 + TAU * this.ring); g.stroke();
      const ga = -Math.PI / 2 + TAU * this.ring, gx = d.x + Math.cos(ga) * 13, gy = d.y - 16 + Math.sin(ga) * 13;
      const gl = g.createRadialGradient(gx, gy, 0, gx, gy, 7); gl.addColorStop(0, 'rgba(255,255,255,.95)'); gl.addColorStop(1, 'rgba(160,230,255,0)'); g.fillStyle = gl; g.beginPath(); g.arc(gx, gy, 7, 0, TAU); g.fill();
      g.restore();
    }
    this.drawJoinBack(g);
    // 엔티티(깊이순)
    const ents = this.mobs.map(m => ({ y: m.y, d: () => m.draw(g, this.t) }));
    ents.push({ y: P.y, d: () => { g.save(); if (P.face < 0) { g.translate(P.x * 2, 0); g.scale(-1, 1); } drawPlayer(this, g); g.restore(); } });
    for (const c of this.civ) ents.push({ y: c.y, d: () => {
      shadowAt(g, c.x, c.y + 9, 8 * (1 - Math.min(.5, (c.z || 0) / 60)), .3);
      if (c.state === 'morph') {
        const u = c.age / .3;
        drawCivilian(g, c.x, c.y, c.idx, this.t, 1);
        // 흰 실루엣으로 차오른다
        g.save(); g.globalCompositeOperation = 'lighter'; const gl = g.createRadialGradient(c.x, c.y - 6, 0, c.x, c.y - 6, 26); gl.addColorStop(0, `rgba(255,255,240,${.9 * u})`); gl.addColorStop(1, 'rgba(255,220,140,0)'); g.fillStyle = gl; g.beginPath(); g.arc(c.x, c.y - 6, 26, 0, TAU); g.fill(); g.restore();
      } else drawCivilian(g, c.x, c.y - (c.z || 0), c.idx, this.t, 1);
    } });
    for (const c of this.crew) ents.push({ y: c.y, d: () => {
      // 갓 합류한 대원은 몸에서 빛이 빠져나간다
      const born = this.t - c.born;
      if (born < .6) { g.save(); g.globalCompositeOperation = 'lighter'; const a = 1 - born / .6; const gl = g.createRadialGradient(c.x, c.y - 6, 0, c.x, c.y - 6, 22); gl.addColorStop(0, `rgba(255,250,230,${a})`); gl.addColorStop(1, 'rgba(255,200,100,0)'); g.fillStyle = gl; g.beginPath(); g.arc(c.x, c.y - 6, 22, 0, TAU); g.fill(); g.restore(); }
      const aimA = c.aim && !c.aim.dead && c.aimAge > 0 ? Math.atan2(c.aim.y - 4 - (c.y - 3), c.aim.x - c.x) : null;
      drawCrewman(g, c.x, c.y, c.idx, this.t, c.face, c.moving, aimA);
    } });
    ents.sort((a, b) => a.y - b.y).forEach(e => e.d());
    // 대원 물줄기
    const lvl = clamp(1 + Math.floor(this.crew.length / 2), 1, 5);
    for (const c of this.crew) if (c.aim && !c.aim.dead && c.aimAge > 0) tierStream(g, c.x + c.face * 9, c.y - 3, c.aim.x, c.aim.y - 4, 2.6 + lvl * .35, lvl, this.t, 8);
    drawParts(this, g, 'mid');
    drawParts(this, g, 'top');
    g.restore();
    if (this.flash > 0) { g.fillStyle = `rgba(${this.flashColor},${this.flash * .5})`; g.fillRect(0, 0, W, H); }
    this.drawBanner(g);
    this.drawHud(g);
  }
  drawBanner(g) {
    const b = this.banner; if (!b) return;
    const a = b.age, pop = a < .14 ? ease(a / .14) * 1.25 : a < .28 ? lerp(1.25, 1, (a - .14) / .14) : 1, alpha = a > 1.2 ? (1.5 - a) / .3 : 1;
    const rb = b.n >= CREW_MAX, kind = rb ? 'rainbow' : b.n >= 5 ? 'gold' : 'silver';
    g.save(); g.translate(W / 2, 64); g.scale(pop, pop);
    // 뒤 띠
    const bh = 30;
    const bg = g.createLinearGradient(0, -bh, 0, bh); bg.addColorStop(0, 'rgba(30,12,4,0)'); bg.addColorStop(.5, `rgba(30,12,4,${.7 * alpha})`); bg.addColorStop(1, 'rgba(30,12,4,0)');
    g.fillStyle = bg; g.fillRect(-170, -bh, 340, bh * 2);
    g.restore();
    metalText(g, rb ? '전원 집결!' : '구조대원 합류!', W / 2, 58, 22 * pop, kind, this.t, alpha);
    metalText(g, '×' + b.n, W / 2 + 92 * pop, 76, 17 * pop, b.n >= 5 ? 'gold' : 'silver', this.t + .4, alpha);
  }
  drawHud(g) {
    const n = this.crew.length;
    g.save();
    g.fillStyle = 'rgba(10,14,28,.8)'; g.beginPath(); g.roundRect(6, 6, 232, 42, 9); g.fill();
    g.strokeStyle = n >= CREW_MAX ? `hsla(${hue(this.t)},100%,70%,.9)` : n >= 5 ? 'rgba(255,214,110,.9)' : 'rgba(255,170,80,.5)'; g.lineWidth = 1.5; g.stroke();
    g.font = '800 12px "Apple SD Gothic Neo",sans-serif'; g.textAlign = 'left'; g.textBaseline = 'middle';
    g.fillStyle = '#fff'; g.fillText('구조대원', 14, 18);
    g.fillStyle = n >= 5 ? '#ffe27a' : '#ffd9a8'; g.font = '900 12px "Apple SD Gothic Neo",sans-serif'; g.fillText(n + ' / ' + CREW_MAX, 70, 18);
    g.fillStyle = '#a7c4e6'; g.font = '700 9px "Apple SD Gothic Neo",sans-serif'; g.fillText('구한 사람이 대원이 된다', 108, 18);
    for (let i = 0; i < CREW_MAX; i++) helmetIcon(g, 20 + i * 20, 34, 6.2, i < n, this.t, i === n - 1 ? this.hudGlow : 0);
    // 아래 한 줄
    const txt = n === 0 ? '타는 집 문 앞에 서면 갇힌 사람을 끌어낸다' : n < CREW_MAX ? '구한 사람이 방화복으로 갈아입고 내 뒤에 줄을 선다 · 가까운 요괴에 물을 쏜다' : '대원 8명이 나를 따라다니며 사방의 요괴를 쓸어 낸다';
    g.font = '700 11px "Apple SD Gothic Neo",sans-serif'; const w = g.measureText(txt).width; g.fillStyle = 'rgba(10,14,28,.72)'; g.beginPath(); g.roundRect(W / 2 - w / 2 - 10, H - 26, w + 20, 20, 8); g.fill();
    g.fillStyle = '#e8f4ff'; g.textAlign = 'center'; g.fillText(txt, W / 2, H - 16);
    g.restore();
  }
}

item({ id: 'crew', group: '구조', tab: '구조대원 합류', name: '구조대원', evoName: '구조대원', icon(g, x, y, r) { helmetIcon(g, x, y, r * .8, true, 0, 0); }, custom() { return new CrewScene(this); } });
