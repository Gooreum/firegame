'use strict';
// 승인된 스킬 샘플(scratchpad/skills/index.html)에서 그대로 가져온 공용 그림·몹·파티클
const W = 480, H = 270;
const TAU = Math.PI * 2;
const rand = (a, b) => a + Math.random() * (b - a);
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
const lerp = (a, b, k) => a + (b - a) * k;
const ease = k => 1 - Math.pow(1 - clamp(k, 0, 1), 3);
const dist = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);
function segDist(px, py, x0, y0, x1, y1) {
  const vx = x1 - x0, vy = y1 - y0, L2 = vx * vx + vy * vy || 1;
  const u = clamp(((px - x0) * vx + (py - y0) * vy) / L2, 0, 1);
  return Math.hypot(px - (x0 + vx * u), py - (y0 + vy * u));
}

// ---------------------------------------------------------------- 배경(한 번만 그린다)
const HOUSE = { x: 372, y: 118, w: 92, h: 74, door: { x: 400, y: 196 } };
let BG = null;
function makeBg() {
  const c = document.createElement('canvas'); c.width = W * 2; c.height = H * 2;
  const g = c.getContext('2d'); g.scale(2, 2);
  const grd = g.createLinearGradient(0, 0, 0, H);
  grd.addColorStop(0, '#5f9447'); grd.addColorStop(1, '#4f8040');
  g.fillStyle = grd; g.fillRect(0, 0, W, H);
  for (let i = 0; i < 260; i++) {   // 풀 얼룩
    g.fillStyle = `rgba(${Math.random() < .5 ? '40,80,30' : '120,170,80'},${rand(.05, .14)})`;
    g.beginPath(); g.ellipse(rand(0, W), rand(0, H), rand(6, 26), rand(3, 12), rand(0, 3), 0, TAU); g.fill();
  }
  for (let i = 0; i < 160; i++) { g.fillStyle = `rgba(250,235,140,${rand(.3, .8)})`; g.fillRect(rand(0, W), rand(0, H), 1.4, 1.4); }
  // 길
  g.fillStyle = '#c9b48d'; g.fillRect(18, 0, 64, H);
  g.fillStyle = '#4a4d57'; g.fillRect(23, 0, 54, H);
  g.strokeStyle = '#f4cf47'; g.lineWidth = 3; g.setLineDash([14, 12]);
  g.beginPath(); g.moveTo(50, 0); g.lineTo(50, H); g.stroke(); g.setLineDash([]);
  // 집(분식집)
  const { x, y, w, h } = HOUSE;
  g.fillStyle = 'rgba(0,0,0,.28)'; g.beginPath(); g.ellipse(x + w / 2 + 6, y + h + 6, w * .62, 12, 0, 0, TAU); g.fill();
  g.fillStyle = '#efe2c4'; g.fillRect(x + 4, y + h * .45, w - 8, h * .58);
  g.fillStyle = '#7a4a2b'; g.fillRect(x + w / 2 - 9, y + h * .66, 18, h * .37);
  g.fillStyle = '#9fd2f0'; g.fillRect(x + 14, y + h * .62, 16, 12); g.fillRect(x + w - 30, y + h * .62, 16, 12);
  const rg = g.createLinearGradient(0, y, 0, y + h * .55);
  rg.addColorStop(0, '#f2554a'); rg.addColorStop(1, '#b8302b');
  g.fillStyle = rg; g.beginPath(); g.roundRect(x - 4, y, w + 8, h * .55, 6); g.fill();
  g.strokeStyle = 'rgba(0,0,0,.35)'; g.lineWidth = 2; g.stroke();
  g.fillStyle = 'rgba(255,255,255,.18)'; g.fillRect(x, y + 4, w, 5);
  g.fillStyle = '#222'; g.beginPath(); g.roundRect(x + 18, y + h * .5 - 2, w - 36, 13, 3); g.fill();
  g.fillStyle = '#fff'; g.font = 'bold 10px "Apple SD Gothic Neo",sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle';
  g.fillText('분식집', x + w / 2, y + h * .5 + 4.5);
  // 나무
  for (const [tx, ty] of [[130, 40], [300, 248], [455, 40]]) {
    g.fillStyle = 'rgba(0,0,0,.25)'; g.beginPath(); g.ellipse(tx + 4, ty + 10, 16, 7, 0, 0, TAU); g.fill();
    g.fillStyle = '#2f6b33'; g.beginPath(); g.arc(tx, ty, 15, 0, TAU); g.fill();
    g.fillStyle = '#3f8a3f'; g.beginPath(); g.arc(tx - 4, ty - 4, 10, 0, TAU); g.fill();
  }
  return c;
}

// ---------------------------------------------------------------- 몹: 불 먹는 요괴(보라 몸, 머리 위 작은 불꽃)
class Mob {
  constructor(x, y, tx, ty, big) {
    this.x = x; this.y = y; this.tx = tx; this.ty = ty;
    this.r = big ? 15 : 10; this.hp = big ? 6 : 2; this.speed = big ? 26 : rand(34, 44);
    this.big = !!big; this.vx = 0; this.vy = 0; this.z = 0; this.vz = 0; this.spin = 0;
    this.flash = 0; this.frozen = 0; this.trap = null; this.dead = false; this.held = false;
    this.seed = rand(0, 10); this.cd = {};
  }
  update(s, dt) {
    if (this.dead) return;
    this.flash -= dt;
    if (this.z > 0 || this.vz > 0) {
      this.z += this.vz * dt; this.vz -= 700 * dt; this.spin += dt * 12;
      this.x += this.vx * dt; this.y += this.vy * dt;
      if (this.z <= 0) { this.z = 0; this.vz = 0; if (this.launched) { this.kill(s); return; } }
      return;
    }
    if (this.held) return;
    if (this.frozen > 0) { this.frozen -= dt; if (this.frozen <= 0) { shatter(s, this); this.kill(s, true); } return; }
    const dx = this.tx - this.x, dy = this.ty - this.y, L = Math.hypot(dx, dy) || 1;
    if (L > 6) { this.x += dx / L * this.speed * dt; this.y += dy / L * this.speed * dt; }
    else { s.burn(this); this.dead = true; return; }
    this.x += this.vx * dt; this.y += this.vy * dt;
    this.vx *= Math.pow(0.02, dt); this.vy *= Math.pow(0.02, dt);
  }
  hit(s, dmg, kx = 0, ky = 0) {
    if (this.dead || this.z > 0) return false;
    this.hp -= dmg; this.flash = 0.09; this.vx += kx; this.vy += ky;
    if (this.hp <= 0) this.kill(s);
    return true;
  }
  launch(s, vz, vx, vy) { if (this.dead) return; this.z = 1; this.vz = vz; this.vx = vx; this.vy = vy; this.launched = true; this.flash = .1; }
  kill(s, quiet) {
    if (this.dead) return; this.dead = true;
    if (!quiet) poof(s, this.x, this.y - this.z, this.big ? 1.6 : 1);
    s.gem(this.x, this.y);
  }
  draw(g, t) {
    if (this.dead) return;
    const bob = this.z > 0 ? 0 : Math.abs(Math.sin(t * 9 + this.seed)) * 2.2;
    const x = this.x, y = this.y - this.z - bob, r = this.r;
    shadowAt(g, this.x, this.y + r * .6, r * (1 - Math.min(.6, this.z / 200)), .35);
    g.save(); g.translate(x, y);
    if (this.z > 0) g.rotate(this.spin);
    const sq = 1 + Math.sin(t * 18 + this.seed) * .05;
    g.scale(sq, 1 / sq);
    // 머리 불꽃
    const fl = 1 + Math.sin(t * 22 + this.seed) * .18;
    g.globalCompositeOperation = 'lighter';
    flameTuft(g, 0, -r * .95, r * .55 * fl);
    g.globalCompositeOperation = 'source-over';
    // 몸
    g.lineWidth = 2.2; g.strokeStyle = '#1b0830';
    const bg = g.createRadialGradient(-r * .35, -r * .4, r * .2, 0, 0, r * 1.1);
    if (this.flash > 0) { bg.addColorStop(0, '#fff'); bg.addColorStop(1, '#fff'); }
    else { bg.addColorStop(0, '#b56cf0'); bg.addColorStop(.6, '#7b33c4'); bg.addColorStop(1, '#4c1a85'); }
    g.fillStyle = bg; g.beginPath(); g.ellipse(0, 0, r, r * .92, 0, 0, TAU); g.fill(); g.stroke();
    // 뿔
    if (this.big) { g.fillStyle = '#ffd36b'; for (const s of [-1, 1]) { g.beginPath(); g.moveTo(s * r * .5, -r * .7); g.lineTo(s * r * .85, -r * 1.25); g.lineTo(s * r * .2, -r * .85); g.fill(); } }
    // 눈(목표 쪽을 본다)
    const lx = clamp((this.tx - this.x) / 200, -1, 1) * r * .12;
    for (const s of [-1, 1]) {
      g.fillStyle = '#fff'; g.beginPath(); g.ellipse(s * r * .36 + lx, -r * .1, r * .26, r * .3, 0, 0, TAU); g.fill();
      g.fillStyle = '#ffde3a'; g.beginPath(); g.arc(s * r * .36 + lx * 2, -r * .06, r * .14, 0, TAU); g.fill();
      g.fillStyle = '#1b0830'; g.beginPath(); g.arc(s * r * .36 + lx * 2, -r * .06, r * .07, 0, TAU); g.fill();
      g.strokeStyle = '#1b0830'; g.lineWidth = 2; g.beginPath(); g.moveTo(s * r * .62, -r * .48); g.lineTo(s * r * .14, -r * .32); g.stroke();
    }
    g.fillStyle = '#1b0830'; g.beginPath(); g.ellipse(lx, r * .38, r * .22, r * .1, 0, 0, Math.PI); g.fill();
    g.restore();
    if (this.frozen > 0) iceBlock(g, x, y, r, t);
  }
}

function flameTuft(g, x, y, s) {
  const gr = g.createRadialGradient(x, y, 0, x, y, s * 1.6);
  gr.addColorStop(0, 'rgba(255,240,170,.95)'); gr.addColorStop(.4, 'rgba(255,140,40,.8)'); gr.addColorStop(1, 'rgba(255,60,0,0)');
  g.fillStyle = gr; g.beginPath(); g.moveTo(x - s, y + s * .4);
  g.quadraticCurveTo(x - s * .4, y - s * 1.6, x, y - s * 2.1); g.quadraticCurveTo(x + s * .5, y - s * 1.4, x + s, y + s * .4);
  g.closePath(); g.fill();
}
function shadowAt(g, x, y, r, a = .3) { g.fillStyle = `rgba(0,0,0,${a})`; g.beginPath(); g.ellipse(x, y, r, r * .38, 0, 0, TAU); g.fill(); }
function iceBlock(g, x, y, r, t) {
  g.save(); g.translate(x, y);
  const R = r * 1.45;
  const gr = g.createLinearGradient(-R, -R, R, R);
  gr.addColorStop(0, 'rgba(220,250,255,.85)'); gr.addColorStop(1, 'rgba(110,200,255,.55)');
  g.fillStyle = gr; g.strokeStyle = 'rgba(240,255,255,.95)'; g.lineWidth = 1.6;
  g.beginPath(); g.moveTo(-R, -R * .3); g.lineTo(-R * .4, -R * 1.05); g.lineTo(R * .7, -R * .9); g.lineTo(R, R * .2); g.lineTo(R * .3, R); g.lineTo(-R * .8, R * .75); g.closePath(); g.fill(); g.stroke();
  g.fillStyle = 'rgba(255,255,255,.7)'; g.fillRect(-R * .5, -R * .7, R * .2, R * .9);
  g.restore();
}

// ---------------------------------------------------------------- 파티클
function part(s, o) { s.parts.push(Object.assign({ x: 0, y: 0, z: 0, vx: 0, vy: 0, vz: 0, life: .5, age: 0, size: 3, grav: 0 }, o)); }
function poof(s, x, y, k = 1) {
  for (let i = 0; i < 12 * k; i++) { const a = rand(0, TAU), v = rand(60, 170) * k; part(s, { kind: 'spark', x, y, vx: Math.cos(a) * v, vy: Math.sin(a) * v, life: rand(.25, .5), size: rand(2, 4) * k, color: Math.random() < .5 ? '255,180,60' : '200,120,255' }); }
  for (let i = 0; i < 5 * k; i++) part(s, { kind: 'smoke', x: x + rand(-6, 6), y: y + rand(-6, 6), vy: -20, life: rand(.4, .7), size: rand(8, 14) * k, color: '190,170,210' });
  part(s, { kind: 'ring', x, y, life: .3, size: 26 * k, color: '255,255,255' });
}
function splash(s, x, y, R = 30, n = 16) {
  part(s, { kind: 'ring', x, y, life: .38, size: R * 1.4, color: '130,210,255', width: 5 });
  part(s, { kind: 'ring', x, y, life: .5, size: R * 2, color: '220,245,255', width: 2 });
  for (let i = 0; i < n; i++) { const a = rand(0, TAU), v = rand(50, 160) * R / 30; part(s, { kind: 'drop', x, y, vx: Math.cos(a) * v, vy: Math.sin(a) * v * .6, vz: rand(80, 220), grav: 600, life: 1, size: rand(2.2, 3.8) }); }
  part(s, { kind: 'glow', x, y, life: .25, size: R * 1.2, color: '160,225,255' });
}
function shatter(s, m) {
  for (let i = 0; i < 12; i++) { const a = rand(0, TAU), v = rand(80, 200); part(s, { kind: 'shard', x: m.x, y: m.y, vx: Math.cos(a) * v, vy: Math.sin(a) * v * .7, vz: rand(60, 200), grav: 700, life: .8, size: rand(3, 7), rot: rand(0, TAU), vr: rand(-12, 12) }); }
  part(s, { kind: 'ring', x: m.x, y: m.y, life: .3, size: 34, color: '200,245,255', width: 4 });
  s.shake = Math.max(s.shake, 3);
}
function textPop(s, x, y, str, color = '#ffe46b', size = 15) { part(s, { kind: 'text', x, y, vy: -40, life: .8, str, color, size }); }

function drawParts(s, g, layer) {
  for (const p of s.parts) {
    const k = p.age / p.life;
    if ((p.kind === 'text') !== (layer === 'top')) continue;
    const y = p.y - p.z;
    switch (p.kind) {
      case 'spark': g.globalCompositeOperation = 'lighter'; g.fillStyle = `rgba(${p.color},${1 - k})`; g.beginPath(); g.arc(p.x, y, p.size * (1 - k * .6), 0, TAU); g.fill(); g.globalCompositeOperation = 'source-over'; break;
      case 'smoke': g.fillStyle = `rgba(${p.color},${.55 * (1 - k)})`; g.beginPath(); g.arc(p.x, y, p.size * (1 + k), 0, TAU); g.fill(); break;
      case 'powder': g.fillStyle = `rgba(255,255,255,${.75 * (1 - k)})`; g.beginPath(); g.arc(p.x, y, p.size * (1 + k * 1.5), 0, TAU); g.fill(); break;
      case 'foam': g.fillStyle = `rgba(255,255,255,${.95 * (1 - k * k)})`; g.strokeStyle = `rgba(180,210,230,${.8 * (1 - k)})`; g.lineWidth = 1; g.beginPath(); g.arc(p.x, y, p.size * (1 - k * .3), 0, TAU); g.fill(); g.stroke(); break;
      case 'ring': g.strokeStyle = `rgba(${p.color},${1 - k})`; g.lineWidth = (p.width || 3) * (1 - k * .5); g.beginPath(); g.ellipse(p.x, y, p.size * ease(k), p.size * ease(k) * .62, 0, 0, TAU); g.stroke(); break;
      case 'glow': { g.globalCompositeOperation = 'lighter'; const gr = g.createRadialGradient(p.x, y, 0, p.x, y, p.size); gr.addColorStop(0, `rgba(${p.color},${.8 * (1 - k)})`); gr.addColorStop(1, `rgba(${p.color},0)`); g.fillStyle = gr; g.beginPath(); g.arc(p.x, y, p.size, 0, TAU); g.fill(); g.globalCompositeOperation = 'source-over'; break; }
      case 'drop': if (p.z > 0) { shadowAt(g, p.x, p.y, p.size * .8, .15); } g.fillStyle = `rgba(110,200,255,${1 - k * .5})`; g.beginPath(); g.arc(p.x, y, p.size, 0, TAU); g.fill(); g.fillStyle = 'rgba(255,255,255,.8)'; g.beginPath(); g.arc(p.x - p.size * .3, y - p.size * .3, p.size * .35, 0, TAU); g.fill(); break;
      case 'shard': g.save(); g.translate(p.x, y); g.rotate(p.rot); g.fillStyle = `rgba(200,240,255,${1 - k * .5})`; g.strokeStyle = 'rgba(255,255,255,.9)'; g.lineWidth = 1; g.beginPath(); g.moveTo(0, -p.size); g.lineTo(p.size * .6, p.size * .5); g.lineTo(-p.size * .6, p.size * .4); g.closePath(); g.fill(); g.stroke(); g.restore(); break;
      case 'gem': { const a = 1 - Math.max(0, (k - .8) * 5); g.globalCompositeOperation = 'lighter'; g.fillStyle = `rgba(90,220,255,${.35 * a})`; g.beginPath(); g.arc(p.x, y, 8, 0, TAU); g.fill(); g.globalCompositeOperation = 'source-over'; g.fillStyle = `rgba(110,230,255,${a})`; g.strokeStyle = `rgba(255,255,255,${a})`; g.lineWidth = 1.2; g.beginPath(); g.moveTo(p.x, y - 6); g.lineTo(p.x + 4.5, y); g.lineTo(p.x, y + 6); g.lineTo(p.x - 4.5, y); g.closePath(); g.fill(); g.stroke(); break; }
      case 'paw': g.fillStyle = `rgba(150,215,255,${.6 * (1 - k)})`; g.beginPath(); g.arc(p.x, y, 2.4, 0, TAU); g.fill(); break;
      case 'frost': g.fillStyle = `rgba(210,245,255,${.5 * (1 - k)})`; g.beginPath(); g.arc(p.x, y, p.size, 0, TAU); g.fill(); break;
      case 'text': g.font = `900 ${p.size}px "Apple SD Gothic Neo",sans-serif`; g.textAlign = 'center'; g.textBaseline = 'middle'; g.lineWidth = 4; g.strokeStyle = `rgba(20,10,30,${1 - k})`; g.strokeText(p.str, p.x, y); g.fillStyle = p.color; g.globalAlpha = 1 - k * k; g.fillText(p.str, p.x, y); g.globalAlpha = 1; break;
    }
  }
}
function stepParts(s, dt) {
  for (const p of s.parts) {
    p.age += dt; p.x += p.vx * dt; p.y += p.vy * dt;
    if (p.grav) { p.z += p.vz * dt; p.vz -= p.grav * dt; if (p.z < 0) { p.z = 0; p.vz *= -.3; p.vx *= .5; p.vy *= .5; } }
    else if (p.vz) p.z += p.vz * dt;
    if (p.kind === 'spark') { p.vx *= .92; p.vy *= .92; }
    if (p.kind === 'shard') p.rot += p.vr * dt;
    if (p.kind === 'gem' && p.age > .35) { const dx = s.player.x - p.x, dy = s.player.y - p.y, L = Math.hypot(dx, dy) || 1; const v = Math.min(420, 80 + p.age * 500); p.x += dx / L * v * dt; p.y += dy / L * v * dt; if (L < 10) p.age = p.life; }
  }
  s.parts = s.parts.filter(p => p.age < p.life);
}

// ---------------------------------------------------------------- 소방관
function drawPlayer(s, g) {
  const p = s.player, t = s.t;
  const run = p.moving ? Math.sin(t * 16) : 0;
  shadowAt(g, p.x, p.y + 8, 12, .35);
  if (s.shield > 0) { g.globalCompositeOperation = 'lighter'; const a = Math.min(1, s.shield * 3); const gr = g.createRadialGradient(p.x, p.y - 4, 8, p.x, p.y - 4, 24); gr.addColorStop(0, 'rgba(255,220,120,0)'); gr.addColorStop(.8, `rgba(255,200,90,${.45 * a})`); gr.addColorStop(1, `rgba(255,240,180,${.9 * a})`); g.fillStyle = gr; g.beginPath(); g.arc(p.x, p.y - 4, 24, 0, TAU); g.fill(); g.globalCompositeOperation = 'source-over'; }
  g.save(); g.translate(p.x, p.y);
  g.fillStyle = '#1b2742';
  g.beginPath(); g.ellipse(-4, 7 + run * 2, 3.5, 4, 0, 0, TAU); g.fill();
  g.beginPath(); g.ellipse(4, 7 - run * 2, 3.5, 4, 0, 0, TAU); g.fill();
  const bg = g.createLinearGradient(0, -6, 0, 8); bg.addColorStop(0, '#3f7ff0'); bg.addColorStop(1, '#2353b8');
  g.fillStyle = bg; g.strokeStyle = '#0f1d3d'; g.lineWidth = 2;
  g.beginPath(); g.roundRect(-9, -6, 18, 15, 6); g.fill(); g.stroke();
  g.fillStyle = '#ffd84a'; g.fillRect(-9, 2, 18, 3);
  const hg = g.createRadialGradient(-3, -16, 1, 0, -12, 11); hg.addColorStop(0, '#ff7b6b'); hg.addColorStop(1, '#c4231b');
  g.fillStyle = hg; g.beginPath(); g.arc(0, -12, 9.5, 0, TAU); g.fill(); g.stroke();
  g.fillStyle = '#c4231b'; g.beginPath(); g.ellipse(0, -7, 12, 3.4, 0, 0, TAU); g.fill(); g.stroke();
  g.fillStyle = '#ffd84a'; g.beginPath(); g.arc(0, -15, 2.6, 0, TAU); g.fill();
  g.restore();
}
function waterStream(g, x0, y0, x1, y1, w, t, arc = 18) {
  const mx = (x0 + x1) / 2, my = (y0 + y1) / 2 - arc;
  g.lineCap = 'round';
  g.globalCompositeOperation = 'lighter';
  g.strokeStyle = 'rgba(70,160,255,.35)'; g.lineWidth = w * 2.2; g.beginPath(); g.moveTo(x0, y0); g.quadraticCurveTo(mx, my, x1, y1); g.stroke();
  g.globalCompositeOperation = 'source-over';
  const lg = g.createLinearGradient(x0, y0, x1, y1); lg.addColorStop(0, 'rgba(120,200,255,.95)'); lg.addColorStop(1, 'rgba(190,235,255,.9)');
  g.strokeStyle = lg; g.lineWidth = w; g.beginPath(); g.moveTo(x0, y0); g.quadraticCurveTo(mx, my, x1, y1); g.stroke();
  g.strokeStyle = 'rgba(255,255,255,.85)'; g.lineWidth = Math.max(1, w * .3); g.setLineDash([6, 9]); g.lineDashOffset = -t * 220;
  g.beginPath(); g.moveTo(x0, y0); g.quadraticCurveTo(mx, my, x1, y1); g.stroke(); g.setLineDash([]);
}
