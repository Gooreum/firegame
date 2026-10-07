'use strict';
// ============================================================================
// 레벨업 샘플 공용: 6단계 레벨 틀, 레벨 등급(색·빛·크기), 레벨업 순간 연출, HUD, 처치 속도 막대
// 아이템 파일은 item({...})으로 등록한다. 인터페이스는 맨 아래 주석 참고.
// ============================================================================

const ITEMS = [];
function item(def) { ITEMS.push(def); }

// ---------------------------------------------------------------- 레벨 등급
// 레벨마다 색·빛·크기가 한 단계씩 오른다. Lv1 파랑 → Lv3 하늘+흰 심 → Lv5 흰 빛+금테 → Lv6 무지개·금.
const TIER = [
  null,
  { scale: 1.00, core: '70,150,255', glow: '60,140,255', glowA: 0.00, white: 0.00, gold: 0, rainbow: 0, trail: 0, shake: 0, hit: 1.0 },
  { scale: 1.18, core: '90,170,255', glow: '80,160,255', glowA: 0.18, white: 0.15, gold: 0, rainbow: 0, trail: 0, shake: 0, hit: 1.25 },
  { scale: 1.40, core: '120,200,255', glow: '110,200,255', glowA: 0.32, white: 0.45, gold: 0, rainbow: 0, trail: 1, shake: 0.6, hit: 1.6 },
  { scale: 1.60, core: '150,220,255', glow: '130,215,255', glowA: 0.45, white: 0.65, gold: 0.25, rainbow: 0, trail: 1, shake: 1.2, hit: 2.0 },
  { scale: 1.80, core: '190,235,255', glow: '160,225,255', glowA: 0.60, white: 0.85, gold: 1, rainbow: 0, trail: 1, shake: 2.0, hit: 2.5 },
  { scale: 2.30, core: '230,248,255', glow: '255,220,140', glowA: 0.80, white: 1.00, gold: 1, rainbow: 1, trail: 1, shake: 3.5, hit: 3.5 },
];
const tierOf = lv => TIER[clamp(lv, 1, 6)];
const hue = (t, off = 0) => (t * 140 + off) % 360;

/** 등급 빛: 무기 몸 뒤에 깔리는 가산 빛. Lv1 없음 → Lv5 금 고리 → Lv6 무지개 고리. */
function tierGlow(g, x, y, r, lv, t) {
  const T = tierOf(lv);
  if (T.glowA <= 0) return;
  g.save(); g.globalCompositeOperation = 'lighter';
  const R = r * (1.4 + lv * 0.18);
  const gr = g.createRadialGradient(x, y, 0, x, y, R);
  gr.addColorStop(0, `rgba(${T.glow},${T.glowA})`); gr.addColorStop(1, `rgba(${T.glow},0)`);
  g.fillStyle = gr; g.beginPath(); g.arc(x, y, R, 0, TAU); g.fill();
  if (T.gold) {
    g.lineWidth = 1.6; g.strokeStyle = T.rainbow ? `hsla(${hue(t)},100%,72%,.9)` : 'rgba(255,214,110,.85)';
    g.beginPath(); g.arc(x, y, r * 1.12, t * 4 % TAU, t * 4 % TAU + 4.2); g.stroke();
    g.strokeStyle = T.rainbow ? `hsla(${hue(t, 180)},100%,75%,.8)` : 'rgba(255,240,190,.7)';
    g.beginPath(); g.arc(x, y, r * 1.26, -t * 3 % TAU, -t * 3 % TAU + 3); g.stroke();
  }
  g.restore();
}

/** 등급 물줄기: Lv마다 굵기·바깥 빛·흰 심·금 테·무지개가 붙는다. arc는 위로 휘는 정도. */
function tierStream(g, x0, y0, x1, y1, w, lv, t, arc = 14) {
  const T = tierOf(lv);
  const mx = (x0 + x1) / 2, my = (y0 + y1) / 2 - arc;
  const path = () => { g.beginPath(); g.moveTo(x0, y0); g.quadraticCurveTo(mx, my, x1, y1); };
  g.save(); g.lineCap = 'round'; g.globalCompositeOperation = 'lighter';
  g.strokeStyle = `rgba(${T.glow},${0.25 + T.glowA * 0.5})`; g.lineWidth = w * (2 + T.glowA * 2); path(); g.stroke();
  if (T.gold) { g.strokeStyle = T.rainbow ? `hsla(${hue(t)},100%,70%,.55)` : 'rgba(255,205,90,.5)'; g.lineWidth = w * 1.55; path(); g.stroke(); }
  g.globalCompositeOperation = 'source-over';
  const lg = g.createLinearGradient(x0, y0, x1, y1);
  lg.addColorStop(0, `rgba(${T.core},.95)`); lg.addColorStop(1, 'rgba(200,240,255,.92)');
  g.strokeStyle = lg; g.lineWidth = w; path(); g.stroke();
  if (T.white > 0) { g.globalCompositeOperation = 'lighter'; g.strokeStyle = `rgba(255,255,255,${T.white})`; g.lineWidth = w * 0.38; path(); g.stroke(); g.globalCompositeOperation = 'source-over'; }
  g.strokeStyle = 'rgba(255,255,255,.85)'; g.lineWidth = Math.max(1, w * 0.22); g.setLineDash([6, 9]); g.lineDashOffset = -t * (220 + lv * 60); path(); g.stroke(); g.setLineDash([]);
  g.restore();
}

/** 맞힌 자리 연출: Lv1 물방울 몇 개 → Lv3 물보라 고리 → Lv5 큰 물보라+금 불티+흔들림 → Lv6 무지개 폭발+짧은 멈춤. */
function hitFx(s, x, y, lv, k = 1) {
  const T = tierOf(lv);
  const n = Math.round((3 + lv * 2) * k);
  for (let i = 0; i < n; i++) part(s, { kind: 'drop', x, y, vx: rand(-70, 70) * T.hit * .6, vy: rand(-50, 30) * T.hit * .6, vz: rand(50, 130), grav: 520, life: .6, size: rand(2, 3) });
  if (lv >= 2) part(s, { kind: 'ring', x, y, life: .28, size: 10 + lv * 4, color: T.core, width: 1.5 + lv * .5 });
  if (lv >= 3) part(s, { kind: 'glow', x, y, life: .22, size: 12 + lv * 5, color: T.glow });
  if (lv >= 5) { for (let i = 0; i < 6; i++) { const a = rand(0, TAU), v = rand(80, 170); part(s, { kind: 'star', x, y, vx: Math.cos(a) * v, vy: Math.sin(a) * v, life: .35, size: rand(3, 5), color: T.rainbow ? null : '255,215,110' }); } s.shake = Math.max(s.shake, T.shake * k); }
  if (lv >= 6) { part(s, { kind: 'prism', x, y, life: .35, size: 30 * k }); s.stop = Math.max(s.stop, .018 * k); }
}

// ---------------------------------------------------------------- 추가 파티클(별·꽃가루·무지개 고리)
const _drawParts0 = drawParts;
drawParts = function (s, g, layer) {
  _drawParts0(s, g, layer);
  if (layer !== 'mid') return;
  for (const p of s.parts) {
    const k = p.age / p.life, y = p.y - p.z;
    if (p.kind === 'star') {
      const c = p.color || `${hsl2rgb(hue(s.t, p.x * 3))}`;
      g.save(); g.globalCompositeOperation = 'lighter'; g.translate(p.x, y); g.rotate(p.age * 6);
      const r = p.size * (1 - k * .5);
      g.fillStyle = `rgba(${c},${1 - k})`;
      g.beginPath(); for (let i = 0; i < 8; i++) { const a = i * Math.PI / 4, rr = i % 2 ? r * .28 : r; g.lineTo(Math.cos(a) * rr, Math.sin(a) * rr); } g.closePath(); g.fill();
      g.fillStyle = `rgba(255,255,255,${1 - k})`; g.beginPath(); g.arc(0, 0, r * .25, 0, TAU); g.fill();
      g.restore();
    } else if (p.kind === 'confetti') {
      g.save(); g.translate(p.x, y); g.rotate(p.rot + p.age * p.vr); g.scale(1, Math.cos(p.age * 9 + p.rot));
      g.fillStyle = p.color ? `rgba(${p.color},${1 - k * k})` : `hsla(${hue(s.t, p.rot * 90)},95%,65%,${1 - k * k})`; g.fillRect(-p.size, -p.size * .45, p.size * 2, p.size * .9);
      g.restore();
    } else if (p.kind === 'prism') {
      g.save(); g.globalCompositeOperation = 'lighter';
      for (let j = 0; j < 3; j++) { g.strokeStyle = `hsla(${hue(s.t, j * 120)},100%,70%,${(1 - k) * .9})`; g.lineWidth = 3 - j * .6; g.beginPath(); g.ellipse(p.x, y, p.size * ease(k) * (1 + j * .18), p.size * ease(k) * (1 + j * .18) * .62, 0, 0, TAU); g.stroke(); }
      g.restore();
    } else if (p.kind === 'mote') {
      // 빛 알갱이: 진화 직전 플레이어에게 빨려 들어간다.
      g.save(); g.globalCompositeOperation = 'lighter'; g.fillStyle = p.color ? `rgba(${p.color},${.9 * Math.min(1, k * 4)})` : `hsla(${hue(s.t, p.size * 40)},100%,75%,${.9 * Math.min(1, k * 4)})`;
      g.beginPath(); g.arc(p.x, y, p.size, 0, TAU); g.fill(); g.restore();
    }
  }
};
const _stepParts0 = stepParts;
stepParts = function (s, dt) {
  for (const p of s.parts) if (p.kind === 'mote' && p.tx !== undefined) { const u = ease(p.age / p.life); p.x = lerp(p.sx, p.tx, u); p.y = lerp(p.sy, p.ty, u); p.vx = p.vy = 0; }
  for (const p of s.parts) if (p.kind === 'star' || p.kind === 'confetti') { p.vx *= .94; p.vy *= .94; if (p.kind === 'confetti') p.vy += 60 * dt; }
  _stepParts0(s, dt);
};
function hsl2rgb(h, sat = 1, l = .7) {
  const a = sat * Math.min(l, 1 - l), f = n => { const k = (n + h / 30) % 12; return Math.round(255 * (l - a * Math.max(-1, Math.min(k - 3, 9 - k, 1)))); };
  return `${f(0)},${f(8)},${f(4)}`;
}

// ---------------------------------------------------------------- 금속 글자
/** 금속 광택 글자: 위아래 그라데이션 + 두꺼운 테 + 빛줄기가 훑고 지나간다. kind: 'silver' | 'gold' | 'rainbow' */
function metalText(g, str, x, y, size, kind, t, alpha = 1) {
  g.save(); g.globalAlpha = alpha; g.font = `900 ${size}px "Apple SD Gothic Neo","Noto Sans KR",sans-serif`; g.textAlign = 'center'; g.textBaseline = 'middle';
  const w = g.measureText(str).width;
  g.lineJoin = 'round';
  g.lineWidth = size * .34; g.strokeStyle = 'rgba(10,6,20,.9)'; g.strokeText(str, x, y + size * .06);
  const gr = g.createLinearGradient(0, y - size * .5, 0, y + size * .5);
  if (kind === 'gold') { gr.addColorStop(0, '#fffbe0'); gr.addColorStop(.45, '#ffd25a'); gr.addColorStop(.55, '#c58a12'); gr.addColorStop(1, '#ffe27a'); }
  else if (kind === 'rainbow') { for (let i = 0; i <= 6; i++) gr.addColorStop(i / 6, `hsl(${hue(t, i * 50)},95%,${i === 3 ? 62 : 72}%)`); }
  else { gr.addColorStop(0, '#ffffff'); gr.addColorStop(.48, '#d6ecff'); gr.addColorStop(.55, '#6aa6d8'); gr.addColorStop(1, '#e8f6ff'); }
  g.lineWidth = size * .12; g.strokeStyle = kind === 'gold' ? '#7a4a00' : kind === 'rainbow' ? '#3a1060' : '#173a66'; g.strokeText(str, x, y);
  g.fillStyle = gr; g.fillText(str, x, y);
  // 빛줄기
  const sx = x - w / 2 + ((t * 1.6) % 1.6) / 1.2 * (w + 40) - 20;
  // 빛줄기는 글자 모양 안에만: 가운데만 밝은 가로 그라데이션으로 글자를 한 번 더 가산 칠한다.
  g.globalCompositeOperation = 'lighter';
  const sh = g.createLinearGradient(sx - 14, 0, sx + 14, 0); sh.addColorStop(0, 'rgba(255,255,255,0)'); sh.addColorStop(.5, 'rgba(255,255,255,.75)'); sh.addColorStop(1, 'rgba(255,255,255,0)');
  g.fillStyle = sh; g.fillText(str, x, y);
  g.restore();
}

// ---------------------------------------------------------------- 레벨업 순간 연출
// 레벨이 오를수록 광선 수·고리 수·입자 수·흔들림·슬로모션이 한 단계씩 커진다. Lv6은 화면 전체.
function levelUp(s, lv) {
  const P = s.player, T = tierOf(lv);
  s.fx = { lv, age: 0 };
  s.surge = 1;
  s.slow = [0, .55, .6, .7, .8, 1.1, 1.9][lv];
  const gold = lv >= 5;
  const col = lv >= 6 ? null : gold ? '255,215,110' : T.core;
  if (lv === 1) {
    // 카드가 화면 아래에서 빛 꼬리를 끌고 날아와 박힌다(박히는 순간은 fx.age 0.35에 터진다).
    s.fx.fly = { sx: W / 2, sy: H + 30 };
  }
  if (lv < 6) burstAt(s, lv, lv === 1 ? .35 : 0);
  else {
    // Lv6: 0~0.55초 화면이 어두워지고 빛 알갱이가 빨려 들어온다 → 0.55초에 폭발.
    for (let i = 0; i < 90; i++) { const a = rand(0, TAU), r = rand(120, 300); part(s, { kind: 'mote', sx: P.x + Math.cos(a) * r, sy: P.y + Math.sin(a) * r * .7, tx: P.x, ty: P.y - 8, x: P.x, y: P.y, life: rand(.35, .55), size: rand(1.5, 3.2) }); }
    s.fx.boomAt = .55;
  }
  s.pips = lv;
}
/** 터지는 순간: 고리·별·꽃가루·흔들림·번쩍. delay초 뒤에 터진다. */
function burstAt(s, lv, delay) { s.fx.burst = delay; s.fx.burstDone = false; }
function doBurst(s, lv) {
  const P = s.player, T = tierOf(lv), x = P.x, y = P.y - 6;
  const gold = lv >= 5, rb = lv >= 6;
  const col = rb ? null : gold ? '255,215,110' : T.core;
  for (let i = 0; i < Math.min(lv, 4); i++) part(s, { kind: rb ? 'prism' : 'ring', x, y, life: .45 + i * .12, size: 60 + i * 34 + lv * 10, color: col || '255,255,255', width: 6 - i });
  part(s, { kind: 'glow', x, y, life: lv === 6 ? .3 : .45, size: lv === 6 ? 110 : 70 + lv * 22, color: rb ? '255,240,200' : gold ? '255,220,140' : T.glow });
  const nStar = [0, 18, 28, 40, 56, 90, 160][lv];
  for (let i = 0; i < nStar; i++) { const a = rand(0, TAU), v = rand(90, 220 + lv * 40); part(s, { kind: 'star', x, y, vx: Math.cos(a) * v, vy: Math.sin(a) * v * .75, life: rand(.5, .9 + lv * .1), size: rand(3, 4 + lv), color: col }); }
  if (lv >= 4) for (let i = 0; i < (lv - 3) * 40; i++) { const a = rand(-Math.PI, 0), v = rand(120, 300); part(s, { kind: 'confetti', x, y: y - 10, vx: Math.cos(a) * v, vy: Math.sin(a) * v, life: rand(1, 1.8), size: rand(2, 3.6), rot: rand(0, TAU), vr: rand(-8, 8), color: lv === 6 ? null : lv === 5 ? (Math.random() < .5 ? '255,215,110' : '255,250,220') : null }); }
  for (let i = 0; i < 6 + lv * 3; i++) { const a = rand(0, TAU), v = rand(60, 200); part(s, { kind: 'drop', x, y, vx: Math.cos(a) * v, vy: Math.sin(a) * v * .6, vz: rand(120, 260), grav: 600, life: 1, size: rand(2.4, 4) }); }
  // 둘레 몹을 밀어낸다(레벨업 밀치기).
  for (const m of s.alive()) { const d = Math.hypot(m.x - x, m.y - y); if (d < 70 + lv * 18) { const L = d || 1; m.vx += (m.x - x) / L * (160 + lv * 40); m.vy += (m.y - y) / L * (160 + lv * 40); if (lv >= 6) m.hit(s, 99); } }
  s.shake = Math.max(s.shake, [0, 3, 4, 5, 6, 9, 14][lv]);
  s.flash = Math.max(s.flash, [0, .25, .3, .35, .4, .45, .45][lv]);
  s.flashColor = rb ? '255,250,235' : gold ? '255,235,170' : '235,248,255';
  s.stop = Math.max(s.stop, [0, .04, .05, .06, .07, .1, .16][lv]);
}

/** 광선(플레이어 뒤에서 도는 빛줄기): 개수와 길이가 레벨마다 는다. */
function drawRays(s, g) {
  const f = s.fx; if (!f) return;
  const lv = f.lv, P = s.player, x = P.x, y = P.y - 8;
  const life = [0, 1.0, 1.1, 1.2, 1.3, 1.7, 2.6][lv];
  const start = lv === 1 ? .35 : lv === 6 ? f.boomAt : 0;
  const a = f.age - start; if (a < 0 || a > life) return;
  const k = a / life, alpha = (a < .12 ? a / .12 : 1) * (1 - k * k);
  const n = [0, 8, 12, 16, 20, 28, 40][lv], len = (80 + lv * 40) * (0.6 + ease(a / .3) * .4) * (lv === 6 ? 2.6 : 1);
  g.save(); g.globalCompositeOperation = 'lighter'; g.translate(x, y);
  g.rotate(f.age * (0.6 + lv * .15));
  for (let i = 0; i < n; i++) {
    const ang = i / n * TAU, wdt = (lv >= 5 ? .09 : .07);
    const c = lv === 6 ? hsl2rgb(hue(s.t, i * 360 / n)) : lv === 5 ? (i % 2 ? '255,214,110' : '255,248,220') : (i % 2 ? tierOf(lv).core : '235,248,255');
    const gr = g.createLinearGradient(0, 0, Math.cos(ang) * len, Math.sin(ang) * len);
    gr.addColorStop(0, `rgba(${c},${(lv === 6 ? .45 : .75) * alpha})`); gr.addColorStop(1, `rgba(${c},0)`);
    g.fillStyle = gr; g.beginPath(); g.moveTo(0, 0);
    g.lineTo(Math.cos(ang - wdt) * len, Math.sin(ang - wdt) * len * .8); g.lineTo(Math.cos(ang + wdt) * len, Math.sin(ang + wdt) * len * .8); g.closePath(); g.fill();
  }
  g.restore();
}

/** 앞쪽 연출: 날아오는 카드, 머리 위 아이콘·별, 레벨 글자, Lv6 이름 띠. */
function drawFxFront(s, g) {
  const f = s.fx; if (!f) return;
  const it = s.item, lv = f.lv, P = s.player, t = s.t;
  // Lv1: 카드가 날아온다
  if (f.fly && f.age < .35) {
    const u = ease(f.age / .35), x = lerp(f.fly.sx, P.x, u), y = lerp(f.fly.sy, P.y - 30, u) - Math.sin(u * Math.PI) * 60;
    for (let i = 1; i <= 8; i++) { const uu = ease(Math.max(0, f.age - i * .02) / .35); const tx = lerp(f.fly.sx, P.x, uu), ty = lerp(f.fly.sy, P.y - 30, uu) - Math.sin(uu * Math.PI) * 60; g.save(); g.globalCompositeOperation = 'lighter'; g.fillStyle = `rgba(150,215,255,${.5 * (1 - i / 9)})`; g.beginPath(); g.arc(tx, ty, 12 - i, 0, TAU); g.fill(); g.restore(); }
    card(g, it, x, y, 1.1, f.age * 14, t);
  }
  // 머리 위 아이콘 + 별 칸
  const a = f.age - (lv === 1 ? .35 : lv === 6 ? f.boomAt : 0);
  const show = lv === 6 ? 1.8 : 1.25;
  if (a > 0 && a < show) {
    const pop = a < .14 ? ease(a / .14) * 1.3 : a < .26 ? lerp(1.3, 1, (a - .14) / .12) : 1, alpha = a > show - .3 ? (show - a) / .3 : 1;
    g.save(); g.globalAlpha = alpha; g.translate(P.x, P.y - 52 - a * 6); g.scale(pop, pop);
    tierGlow(g, 0, 0, 13, Math.max(3, lv), t);
    it.icon(g, 0, 0, 12, lv, t);
    // 별 칸: 지금 레벨 칸이 막 차오른다
    for (let i = 1; i <= 6; i++) {
      const sx = (i - 3.5) * 11, sy = 21, on = i <= lv, just = i === lv;
      const sc = just ? 1 + Math.max(0, .6 - a) * 1.5 : 1;
      starShape(g, sx, sy, (i === 6 ? 5.4 : 4.4) * sc, on ? (i === 6 ? 'rainbow' : lv >= 5 ? 'gold' : 'blue') : 'off', t + i);
    }
    g.restore();
    // 레벨 글자
    const label = lv === 6 ? '' : lv === 5 ? 'Lv5  MAX' : lv === 1 ? 'NEW!' : 'Lv' + lv;
    if (label) metalText(g, label, P.x, P.y - 92 - a * 8, 18 + lv * 2.2, lv === 5 ? 'gold' : 'silver', t, alpha);
    if (lv === 5 && a > .35) metalText(g, '다음은 최고급!', P.x, P.y + 34, 11, 'gold', t, alpha * Math.min(1, (a - .35) * 4));
  }
  // Lv6 이름 띠
  if (lv === 6 && a > 0 && a < 1.8) {
    const k = a < .2 ? ease(a / .2) : 1, alpha = a > 1.4 ? (1.8 - a) / .4 : 1;
    g.save(); g.globalAlpha = alpha;
    const bh = 54 * k;
    const bg = g.createLinearGradient(0, H / 2 - bh / 2, 0, H / 2 + bh / 2); bg.addColorStop(0, 'rgba(20,8,40,0)'); bg.addColorStop(.5, 'rgba(20,8,40,.85)'); bg.addColorStop(1, 'rgba(20,8,40,0)');
    g.fillStyle = bg; g.fillRect(0, H / 2 - bh / 2, W, bh);
    g.globalCompositeOperation = 'lighter';
    for (const yy of [H / 2 - bh / 2 + 3, H / 2 + bh / 2 - 3]) { const lg = g.createLinearGradient(0, 0, W, 0); for (let i = 0; i <= 6; i++) lg.addColorStop(i / 6, `hsla(${hue(t, i * 60)},100%,70%,.9)`); g.fillStyle = lg; g.fillRect(0, yy - 1, W * k, 2); }
    g.restore();
    metalText(g, '최고급!', W / 2, H / 2 - 13, 12, 'gold', t, alpha);
    metalText(g, it.evoName, W / 2 + (1 - k) * 200, H / 2 + 8, 28, 'rainbow', t, alpha);
  }
}
function starShape(g, x, y, r, kind, t) {
  g.save(); g.translate(x, y);
  if (kind !== 'off') { g.globalCompositeOperation = 'lighter'; const gl = g.createRadialGradient(0, 0, 0, 0, 0, r * 2.2); gl.addColorStop(0, kind === 'gold' ? 'rgba(255,210,100,.6)' : kind === 'rainbow' ? `hsla(${hue(t)},100%,70%,.7)` : 'rgba(120,200,255,.55)'); gl.addColorStop(1, 'rgba(0,0,0,0)'); g.fillStyle = gl; g.beginPath(); g.arc(0, 0, r * 2.2, 0, TAU); g.fill(); g.globalCompositeOperation = 'source-over'; }
  g.beginPath(); for (let i = 0; i < 10; i++) { const a = -Math.PI / 2 + i * Math.PI / 5, rr = i % 2 ? r * .45 : r; g.lineTo(Math.cos(a) * rr, Math.sin(a) * rr); } g.closePath();
  const gr = g.createLinearGradient(0, -r, 0, r);
  if (kind === 'gold') { gr.addColorStop(0, '#fff6c0'); gr.addColorStop(1, '#e09a10'); }
  else if (kind === 'rainbow') { gr.addColorStop(0, `hsl(${hue(t)},100%,80%)`); gr.addColorStop(1, `hsl(${hue(t, 160)},100%,60%)`); }
  else if (kind === 'blue') { gr.addColorStop(0, '#e6f6ff'); gr.addColorStop(1, '#3d8bff'); }
  else { gr.addColorStop(0, 'rgba(60,70,90,.8)'); gr.addColorStop(1, 'rgba(30,35,50,.8)'); }
  g.fillStyle = gr; g.fill(); g.lineWidth = 1.2; g.strokeStyle = 'rgba(10,10,25,.85)'; g.stroke();
  g.restore();
}
/** 아이템 카드(날아오는 것·HUD): 둥근 사각 판 + 아이콘. */
function card(g, it, x, y, sc, rot, t) {
  g.save(); g.translate(x, y); g.rotate(Math.sin(rot) * .2); g.scale(sc, sc);
  const gr = g.createLinearGradient(0, -18, 0, 18); gr.addColorStop(0, '#2f4f86'); gr.addColorStop(1, '#16274a');
  g.fillStyle = gr; g.strokeStyle = '#bfe3ff'; g.lineWidth = 2; g.beginPath(); g.roundRect(-15, -18, 30, 36, 6); g.fill(); g.stroke();
  it.icon(g, 0, -1, 11, 1, t);
  g.restore();
}

// ---------------------------------------------------------------- 장면
const SEG = [0, 3.6, 3.4, 3.4, 3.4, 3.8, 6.0];   // 레벨별 길이(초). Lv6은 길게.
const AT = [0, 0]; for (let i = 2; i <= 6; i++) AT[i] = AT[i - 1] + SEG[i - 1];   // AT[lv] = 그 레벨이 시작하는 시각
const DUR = AT[6] + SEG[6];

class Scene {
  constructor(it) { this.item = it; this.reset(); }
  reset() {
    this.t = 0; this.mobs = []; this.parts = []; this.shake = 0; this.flash = 0; this.flashColor = '255,255,255'; this.slow = 0; this.stop = 0;
    this.player = { x: 200, y: 160, moving: false }; this.lv = 0; this.st = {}; this.shield = 0; this.warn = 0; this.hp = 1;
    this.fx = null; this.surge = 0; this.pips = 0; this.dark = 0;
    this.kills = [0, 0, 0, 0, 0, 0, 0]; this.spawnClock = 0; this.spawned = 0;
    this.item.setup && this.item.setup(this);
    // 처음부터 화면 안에 몹 떼를 깔아 둔다(Lv1부터 바로 싸운다).
    const pre = this.item.pre ?? 18;
    for (let i = 0; i < pre; i++) { this.spawnOne(); const m = this.mobs[this.mobs.length - 1]; const a = rand(0, TAU), r = rand(90, 170); m.x = clamp(this.player.x + Math.cos(a) * r, 100, W - 20); m.y = clamp(this.player.y + Math.sin(a) * r * .7, 30, H - 20); }
  }
  burn(m) { for (let i = 0; i < 6; i++) part(this, { kind: 'spark', x: m.x, y: m.y, vx: rand(-60, 60), vy: rand(-90, -20), life: .5, size: 3, color: '255,140,40' }); this.warn = .6; m.reached = true; }
  gem(x, y) { if (Math.random() > .3) return; part(this, { kind: 'gem', x, y, z: 0, vz: 120, grav: 500, life: 1.6 }); }
  spawnOne() {
    const it = this.item, sides = it.sides || ['top', 'topright'], side = sides[this.spawned % sides.length];
    let x, y;
    if (side === 'top') { x = -14; y = rand(70, 240); }
    else if (side === 'left') { x = -14; y = rand(30, 250); }
    else if (side === 'topright') { x = rand(150, 340); y = -14; }
    else if (side === 'bottom') { x = rand(120, 360); y = H + 14; }
    else if (side === 'right') { x = W + 14; y = rand(30, 250); }
    else { x = W + 14; y = rand(20, 110); }
    const big = this.spawned % 7 === 6;
    const tg = it.target === 'player' ? this.player : HOUSE.door;
    const m = new Mob(x, y, tg.x + rand(-26, 26), tg.y + rand(-6, 4), big);
    m.hp = big ? 12 : 4; m.maxHp = m.hp; m.speed *= 1.5;
    this.mobs.push(m); this.spawned++;
  }
  alive() { return this.mobs.filter(m => !m.dead && m.z <= 0 && !m.held && m.x > -4 && m.x < W + 4 && m.y > -4 && m.y < H + 4); }
  crowd() { const a = this.alive().filter(m => Math.hypot(m.x - this.player.x, m.y - this.player.y) < 260); if (!a.length) return null; let x = 0, y = 0; for (const m of a) { x += m.x; y += m.y; } return { x: x / a.length, y: y / a.length }; }
  nearest(x, y, max = 200, skip) { let b = null, bd = max; for (const m of this.alive()) { if (skip && skip.has(m)) continue; const d = Math.hypot(m.x - x, m.y - y); if (d < bd) { b = m; bd = d; } } return b; }
  hitArea(x, y, R, dmg, push = 0) { let n = 0; for (const m of this.alive()) { const d = Math.hypot(m.x - x, m.y - y); if (d < R + m.r) { const L = d || 1; m.hit(this, dmg, (m.x - x) / L * push, (m.y - y) / L * push); n++; } } return n; }
  update(dt) {
    const real = dt;
    if (this.stop > 0) { this.stop -= real; dt = 0; }
    else if (this.slow > 0) { this.slow -= real; dt *= .25; }
    this.t += dt;
    // 레벨 진행
    const want = this.t >= AT[6] ? 6 : this.t >= AT[5] ? 5 : this.t >= AT[4] ? 4 : this.t >= AT[3] ? 3 : this.t >= AT[2] ? 2 : 1;
    if (want !== this.lv) { this.lv = want; for (const m of this.mobs) m.hp = m.maxHp || m.hp; levelUp(this, want); this.item.onLevel && this.item.onLevel(this, want); }
    if (this.fx) {
      this.fx.age += real;
      if (this.fx.burst !== undefined && !this.fx.burstDone && this.fx.age >= this.fx.burst) { this.fx.burstDone = true; doBurst(this, this.fx.lv); }
      if (this.fx.boomAt && !this.fx.boomDone && this.fx.age >= this.fx.boomAt) { this.fx.boomDone = true; doBurst(this, 6); }
      if (this.fx.age > 3.2) this.fx = null;
    }
    // Lv6 직전 어두워짐
    const f = this.fx; this.dark = f && f.lv === 6 ? (f.age < f.boomAt ? f.age / f.boomAt * .72 : Math.max(0, .72 - (f.age - f.boomAt) * 1.4)) : 0;
    this.surge = Math.max(0, this.surge - real * 1.6);
    // 몹: 화면에 늘 cap(기본 30)마리가 있도록 채운다(초당 최대 45마리). 처치 속도가 몹 공급이 아니라 무기 힘을 잰다.
    this.spawnClock -= dt;
    while (this.spawnClock <= 0) { if (this.mobs.length < (this.item.cap || 30)) this.spawnOne(); this.spawnClock += 1 / (this.item.rate || 45); }
    this.item.update(this, dt, this.lv);
    for (const m of this.mobs) {
      if (this.item.target === 'player') { m.tx = this.player.x; m.ty = this.player.y; }
      m.update(this, dt);
    }
    for (const m of this.mobs) if (m.dead && !m.counted) { m.counted = true; if (!m.reached) this.kills[this.lv]++; }
    this.mobs = this.mobs.filter(m => !m.dead);
    stepParts(this, dt);
    this.shake *= Math.pow(.002, real); this.flash = Math.max(0, this.flash - real * 1.4); this.shield -= dt; this.warn -= dt;
    if (this.t > DUR) this.reset();
  }
  draw(g) {
    g.save();
    if (this.shake > .3) g.translate(rand(-1, 1) * this.shake, rand(-1, 1) * this.shake);
    g.drawImage(BG, 0, 0, W, H);
    if (this.warn > 0) { g.globalCompositeOperation = 'lighter'; const gr = g.createRadialGradient(HOUSE.door.x, HOUSE.door.y - 20, 0, HOUSE.door.x, HOUSE.door.y - 20, 60); gr.addColorStop(0, `rgba(255,120,30,${this.warn})`); gr.addColorStop(1, 'rgba(255,60,0,0)'); g.fillStyle = gr; g.fillRect(HOUSE.x - 40, HOUSE.y - 40, 180, 160); g.globalCompositeOperation = 'source-over'; }
    this.item.drawGround && this.item.drawGround(this, g, this.lv);
    if (this.dark > 0) { g.fillStyle = `rgba(8,4,20,${this.dark})`; g.fillRect(-20, -20, W + 40, H + 40); }
    drawRays(this, g);
    const ents = [...this.mobs.map(m => ({ y: m.y, d: () => m.draw(g, this.t) })), { y: this.player.y, d: () => { tierAura(this, g); drawPlayer(this, g); } }];
    if (this.item.ents) for (const e of this.item.ents(this, g, this.lv)) ents.push(e);
    ents.sort((a, b) => a.y - b.y).forEach(e => e.d());
    this.item.drawAir && this.item.drawAir(this, g, this.lv);
    drawParts(this, g, 'mid');
    drawParts(this, g, 'top');
    g.restore();
    if (this.flash > 0) { g.fillStyle = `rgba(${this.flashColor},${this.flash * .55})`; g.fillRect(0, 0, W, H); }
    drawFxFront(this, g);
    drawHud(this, g);
  }
}
/** 플레이어 발밑 등급 고리: 지금 아이템 레벨이 몸에도 보인다(Lv3부터). */
function tierAura(s, g) {
  const lv = s.lv; if (lv < 3) return;
  const P = s.player, T = tierOf(lv);
  g.save(); g.globalCompositeOperation = 'lighter';
  const r = 14 + lv * 2 + s.surge * 10;
  g.strokeStyle = T.rainbow ? `hsla(${hue(s.t)},100%,70%,.8)` : T.gold ? 'rgba(255,214,110,.75)' : `rgba(${T.glow},.55)`;
  g.lineWidth = 2; g.beginPath(); g.ellipse(P.x, P.y + 8, r, r * .4, 0, 0, TAU); g.stroke();
  if (lv >= 5) { g.lineWidth = 1; g.beginPath(); g.ellipse(P.x, P.y + 8, r + 5, (r + 5) * .4, 0, s.t * 3, s.t * 3 + 4); g.stroke(); }
  g.restore();
}

// ---------------------------------------------------------------- HUD
function drawHud(s, g) {
  const it = s.item, lv = s.lv;
  // 왼쪽 위: 아이콘 + 이름 + 별 칸
  g.save();
  g.fillStyle = 'rgba(10,14,28,.78)'; g.beginPath(); g.roundRect(6, 6, 168, 40, 9); g.fill();
  g.strokeStyle = lv >= 6 ? `hsla(${hue(s.t)},100%,70%,.9)` : lv >= 5 ? 'rgba(255,214,110,.9)' : 'rgba(150,200,255,.35)'; g.lineWidth = 1.5; g.stroke();
  tierGlow(g, 26, 26, 12, lv, s.t); it.icon(g, 26, 26, 12, lv, s.t);
  g.font = '800 12px "Apple SD Gothic Neo",sans-serif'; g.textAlign = 'left'; g.textBaseline = 'middle';
  g.fillStyle = lv >= 6 ? '#ffe9a8' : '#fff'; g.fillText(lv >= 6 ? it.evoName : it.name, 44, 18);
  for (let i = 1; i <= 6; i++) starShape(g, 48 + (i - 1) * 13, 33, i === 6 ? 5.2 : 4.6, i <= lv ? (i === 6 ? 'rainbow' : lv >= 5 ? 'gold' : 'blue') : 'off', s.t + i);
  g.font = '700 9px "Apple SD Gothic Neo",sans-serif'; g.fillStyle = '#a7c4e6'; g.fillText(lv >= 6 ? '최고급' : 'Lv' + lv, 128, 33);
  // 아래: 이 레벨에서 무엇이 달라졌나
  const txt = it.lvText && it.lvText[lv];
  if (txt) { g.font = '700 11px "Apple SD Gothic Neo",sans-serif'; const w = g.measureText(txt).width; g.fillStyle = 'rgba(10,14,28,.72)'; g.beginPath(); g.roundRect(W / 2 - w / 2 - 10, H - 26, w + 20, 20, 8); g.fill(); g.fillStyle = lv >= 6 ? '#ffe9a8' : '#e8f4ff'; g.textAlign = 'center'; g.fillText(txt, W / 2, H - 16); }
  // 오른쪽 위: 처치 속도 막대(레벨별 초당 처치)
  const rates = []; for (let i = 1; i <= 6; i++) { const span = i < lv ? SEG[i] : i === lv ? Math.max(.5, s.t - AT[i]) : 0; rates[i] = span > 0 ? s.kills[i] / span : 0; }
  const max = Math.max(4, ...rates.slice(1));
  const bx = W - 112, by = 8;
  g.fillStyle = 'rgba(10,14,28,.72)'; g.beginPath(); g.roundRect(bx - 6, by - 2, 112, 60, 8); g.fill();
  g.font = '700 8.5px "Apple SD Gothic Neo",sans-serif'; g.textAlign = 'left'; g.fillStyle = '#a7c4e6'; g.fillText('처치 속도(마리/초)', bx, by + 6);
  for (let i = 1; i <= 6; i++) {
    const y = by + 13 + (i - 1) * 7.4, w = rates[i] / max * 70;
    g.fillStyle = '#7f93b3'; g.font = '700 7px sans-serif'; g.fillText(i === 6 ? '최고' : 'Lv' + i, bx, y + 3);
    if (w > 0) { const gr = g.createLinearGradient(bx + 22, 0, bx + 22 + w, 0); if (i === 6) { gr.addColorStop(0, `hsl(${hue(s.t)},90%,65%)`); gr.addColorStop(1, `hsl(${hue(s.t, 140)},90%,65%)`); } else if (i === 5) { gr.addColorStop(0, '#c58a12'); gr.addColorStop(1, '#ffe27a'); } else { gr.addColorStop(0, '#2c6fd6'); gr.addColorStop(1, `rgb(${tierOf(i).core})`); } g.fillStyle = gr; g.fillRect(bx + 22, y - 1, w, 5); g.fillStyle = '#dfefff'; g.font = '700 7px sans-serif'; g.fillText(rates[i].toFixed(1), bx + 25 + w, y + 3); }
  }
  g.restore();
}

/*
 * 아이템 인터페이스 — item({...})
 *   id, name(Lv1~5 이름), evoName(Lv6 최고급 이름), group('무기'|'보조')
 *   lvText: [null, 'Lv1 문구', ..., 'Lv6 문구']  화면 아래 한 줄
 *   sides: 몹이 들어오는 쪽 목록('top'=왼쪽, 'topright'=위, 'left', 'bottom', 'right')
 *   target: 'house'(기본) | 'player'
 *   icon(g, x, y, r, lv, t)  HUD·카드·머리 위 아이콘(r=반지름 기준)
 *   setup(s)  onLevel(s, lv)  update(s, dt, lv)
 *   drawGround(s, g, lv)  ents(s, g, lv) → [{y, d()}]  drawAir(s, g, lv)
 * 쓰는 도구: tierOf(lv) · tierGlow · tierStream · hitFx(s,x,y,lv,k) · s.surge(레벨업 직후 1→0, 무기를 순간 키우는 데 쓴다)
 * 규칙: Lv1→Lv5 처치 속도가 3배 이상, Lv6은 그보다 확 뛴다. Lv마다 크기·개수·색 등급·빛·타격감이 모두 오른다.
 */
