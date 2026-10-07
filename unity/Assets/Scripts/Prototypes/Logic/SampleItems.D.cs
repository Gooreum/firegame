using System;
using System.Collections.Generic;

namespace FireGame.Prototypes.Logic
{
    // 승인 샘플 tools/levelup-art/items-d.js를 한 줄씩 옮긴다: 호스 채찍 · 고압 펌프 · 장화 · 방화복.
    // 좌표는 샘플 px(SampleCore 참고). 샘플 m.cd.key > s.t 는 남은 초(SurvivorSim.Cd > 0)로 옮겼다.
    // 샘플 freshMobs(레벨마다 몹 체력 채우기)는 처치 속도 비교용이라 옮기지 않는다.
    public static partial class SampleItemsRegistry
    {
        static partial void MakeD(UpgradeId id, ref SampleItem made)
        {
            switch (id)
            {
                case UpgradeId.Whip: made = new SampleWhip(); break;
                case UpgradeId.Tank: made = new SampleTank(); break;
                case UpgradeId.Boots: made = new SampleBoots(); break;
                case UpgradeId.Suit: made = new SampleSuit(); break;
            }
        }
    }

    /// <summary>샘플 {x, y, k} 한 점(채찍 꼬리·물 고리·대폭발·물길).</summary>
    public sealed class SamplePtD
    {
        public float X, Y, K;

        public SamplePtD(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    // ---------------------------------------------------------------- 10. 호스 채찍 → 물 회오리 (items-d.js:17-82)
    public sealed class SampleWhip : SampleItem
    {
        public static readonly int[] Arms = { 0, 1, 1, 2, 2, 3, 3 };
        public static readonly float[] Rad = { 0f, 46f, 56f, 64f, 72f, 82f, 86f };
        public static readonly float[] Spin = { 0f, 3.6f, 4.4f, 5.2f, 6.2f, 6.8f, 7.2f };
        public static readonly float[] Dmg = { 0f, 2f, 2.4f, 3f, 3.6f, 4.4f, 5f };
        public static readonly float[] Push = { 0f, 75f, 100f, 130f, 165f, 200f, 230f };

        /// <summary>샘플 s.st.a: 휘두르는 각.</summary>
        public float A;

        /// <summary>샘플 s.st.trail[i]: 갈래마다 끝 자리(0이 최신).</summary>
        public readonly List<List<SamplePtD>> Trail = new List<List<SamplePtD>>();

        /// <summary>샘플 s.st.rings(Lv6 물 고리).</summary>
        public readonly List<SamplePtD> Rings = new List<SamplePtD>();

        public float DropT;

        /// <summary>샘플 R = WHIP_R[lv]·(1 + surge·.25).</summary>
        public float RadiusAt(int lv)
        {
            return Rad[lv] * (1f + (Surge * 0.25f));
        }

        // items-d.js:22
        public override void Setup(SurvivorSim s)
        {
            A = 0f;
            Trail.Clear();
            Rings.Clear();
            DropT = 0f;
        }

        // items-d.js:23
        public override void OnLevel(SurvivorSim s, int lv)
        {
            Trail.Clear();
        }

        // items-d.js:24-45
        public override void Update(SurvivorSim s, float dt, int lv)
        {
            float px = s.PX, py = s.PY;
            int arms = Arms[lv];
            float R = RadiusAt(lv);
            A += dt * Spin[lv];
            float dmg = Dmg[lv], push = Push[lv];
            int len = 10 + (lv * 2);
            while (Trail.Count < arms) Trail.Add(new List<SamplePtD>());
            for (int i = 0; i < arms; i++)
            {
                float a = A + (i * SurvivorSim.Tau / arms);
                float tx = px + ((float)Math.Cos(a) * R), ty = py - 4f + ((float)Math.Sin(a) * R * 0.75f);
                List<SamplePtD> tr = Trail[i];
                tr.Insert(0, new SamplePtD(tx, ty));
                while (tr.Count > len) tr.RemoveAt(tr.Count - 1);
                foreach (Enemy m in s.SAlive().ToArray())
                {
                    if (m.Dead) continue;
                    float mx = SurvivorSim.SX(m.Pos), my = SurvivorSim.SY(m.Pos);
                    if (SurvivorSim.SegDist(mx, my, px, py - 4f, tx, ty) < SurvivorSim.SR(m) + 5f + lv && !(SurvivorSim.Cd(m, "wp") > 0f))
                    {
                        SurvivorSim.SetCd(m, "wp", 0.32f);
                        float L = SurvivorSim.Hypot(mx - px, my - py);
                        if (L == 0f) L = 1f;
                        s.SHit(m, dmg, (mx - px) / L * push, (my - py) / L * push);
                        s.HitFx(mx, my - 4f, lv, 0.8f);
                        if (lv >= 5)
                        {
                            s.SplashFx(mx, my, 20f, 5);
                            s.SHitArea(mx, my, 20f, 1.2f, 120f);
                        }
                        s.SShake = Math.Max(s.SShake, 1.5f + (lv * 0.3f));
                    }
                }
                // items-d.js:79 (샘플은 drawAir에서 뿌린다. 화면이 규칙 난수를 건드리지 않게 여기로 옮겼다. 둘 다 1/60초마다 한 번)
                SamplePtD t0 = tr[0];
                if (s.Rnd(0f, 1f) < 0.5f + (lv * 0.1f)) s.Drop(t0.X, t0.Y, s.Rnd(-40f, 40f), s.Rnd(-40f, 40f), s.Rnd(20f, 60f), 400f, 0.4f, 2f + (lv * 0.15f));
            }
            if (lv == 6)
            {
                DropT -= dt;
                if (DropT <= 0f)
                {
                    DropT = 0.15f;
                    for (int i = 0; i < arms; i++)
                    {
                        List<SamplePtD> tr = i < Trail.Count ? Trail[i] : null;
                        if (tr != null && tr.Count > 0)
                        {
                            Rings.Add(new SamplePtD(tr[0].X, tr[0].Y + 4f));
                            s.Prism(tr[0].X, tr[0].Y + 4f, 0.3f, 18f);
                        }
                    }
                }
                Enemy[] alive = s.SAlive().ToArray();
                foreach (SamplePtD r in Rings)
                {
                    r.K += dt;
                    foreach (Enemy m in alive)
                    {
                        if (m.Dead) continue;
                        float mx = SurvivorSim.SX(m.Pos), my = SurvivorSim.SY(m.Pos);
                        float d = SurvivorSim.Hypot(mx - r.X, my - r.Y);
                        if (d < SurvivorSim.SR(m) + 28f && !(SurvivorSim.Cd(m, "rr") > 0f))
                        {
                            SurvivorSim.SetCd(m, "rr", 0.12f);
                            float L = d == 0f ? 1f : d;
                            s.SHit(m, 4f, (-(my - r.Y) / L * 140f) + ((mx - r.X) / L * 40f), ((mx - r.X) / L * 140f) + ((my - r.Y) / L * 40f));
                            if (s.Rnd(0f, 1f) < 0.4f) s.Drop(mx, my, s.Rnd(-90f, 90f), s.Rnd(-60f, 30f), s.Rnd(60f, 140f), 500f, 0.5f, 2.4f);
                        }
                    }
                }
                Rings.RemoveAll(r => r.K >= 2.6f);
                if (Rings.Count > 40) Rings.RemoveAt(0);
            }
        }
    }

    // ---------------------------------------------------------------- 11. 고압 펌프 → 초고압 펌프 (items-d.js:85-154)
    public sealed class SampleTank : SampleItem
    {
        public static readonly float[] CdT = { 0f, 0.22f, 0.19f, 0.16f, 0.14f, 0.12f, 0.1f };
        public static readonly float[] Dmg = { 0f, 1.4f, 2f, 2.4f, 2.8f, 3.4f, 4f };
        public const float BoomEvery = 2.2f;
        public const float BoomR = 140f;

        /// <summary>샘플 s.st.tg: 이번 틱 물줄기 대상(Lv4부터 둘).</summary>
        public readonly List<Enemy> Targets = new List<Enemy>();

        public float Cd;
        public float Boom;
        public readonly List<SamplePtD> Booms = new List<SamplePtD>();

        /// <summary>Lv6 대폭발 횟수(시험용).</summary>
        public int Novas;

        public static float RangeOf(int lv)
        {
            return 130f + (lv * 32f);
        }

        // items-d.js:106
        public override void Setup(SurvivorSim s)
        {
            Targets.Clear();
            Cd = 0f;
            Boom = 1.2f;
            Booms.Clear();
        }

        // items-d.js:107-134
        public override void Update(SurvivorSim s, float dt, int lv)
        {
            float px = s.PX, py = s.PY;
            int n = lv >= 4 ? 2 : 1;
            var used = new HashSet<Enemy>();
            Targets.Clear();
            float range = RangeOf(lv);
            for (int i = 0; i < n; i++)
            {
                Enemy m = s.SNearest(px, py, range, used);
                if (m != null)
                {
                    used.Add(m);
                    Targets.Add(m);
                }
            }
            Cd -= dt;
            if (Cd <= 0f)
            {
                Cd = CdT[lv];
                float dmg = Dmg[lv], reach = lv >= 3 ? 40f + (lv * 16f) : 0f, w = 3f + (lv * 1.7f), push = 30f + (lv * 30f);
                foreach (Enemy m in Targets)
                {
                    float mx = SurvivorSim.SX(m.Pos), my = SurvivorSim.SY(m.Pos);
                    float dx = mx - px, dy = my - py, L = SurvivorSim.Hypot(dx, dy);
                    if (L == 0f) L = 1f;
                    float ex = mx + (dx / L * reach), ey = my + (dy / L * reach);
                    foreach (Enemy o in s.SAlive().ToArray())
                    {
                        if (o.Dead) continue;
                        if (o == m || (reach > 0f && SurvivorSim.SegDist(SurvivorSim.SX(o.Pos), SurvivorSim.SY(o.Pos), mx, my, ex, ey) < SurvivorSim.SR(o) + (w * 0.5f))) s.SHit(o, dmg, dx / L * push, dy / L * push);
                    }
                    s.HitFx(mx, my - 4f, lv, 0.6f);
                    if (lv >= 4 && !(SurvivorSim.Cd(m, "ts") > 0f))
                    {
                        SurvivorSim.SetCd(m, "ts", 0.35f);
                        s.SplashFx(ex, ey, 18f + (lv * 3f), 6);
                        s.SHitArea(ex, ey, 18f + (lv * 3f), 1.5f, 140f);
                    }
                }
            }
            if (lv == 6)
            {
                Boom -= dt;
                if (Boom <= 0f)
                {
                    Boom = BoomEvery;
                    float x = px, y = py - 4f;
                    Booms.Add(new SamplePtD(x, y));
                    Novas++;
                    s.SHitArea(x, y, BoomR, 12f, 380f);
                    s.SplashFx(x, y, 90f, 40);
                    s.Prism(x, y, 0.6f, 150f);
                    s.Glow(x, y, 0.4f, 150f, new Rgb(200, 235, 255));
                    for (int i = 0; i < 30; i++)
                    {
                        float a = s.Rnd(0f, SurvivorSim.Tau), v = s.Rnd(150f, 320f);
                        s.Star(x, y, (float)Math.Cos(a) * v, (float)Math.Sin(a) * v * 0.7f, 0.6f, s.Rnd(3f, 6f), Rgb.None);
                    }
                    s.SShake = Math.Max(s.SShake, 10f);
                    s.SStop = Math.Max(s.SStop, 0.07f);
                    s.SFlash = Math.Max(s.SFlash, 0.25f);
                    s.SFlashColor = new Rgb(220, 240, 255);
                }
                foreach (SamplePtD b in Booms) b.K += dt;
                Booms.RemoveAll(b => b.K >= 0.8f);
            }
        }
    }

    // ---------------------------------------------------------------- 12. 장화 → 제트 장화 (items-d.js:157-232)
    public sealed class SampleBoots : SampleItem
    {
        public static readonly float[] Speed = { 0f, 1f, 1.25f, 1.5f, 1.8f, 2.1f, 2.6f };
        public static readonly float[] BumpR = { 0f, 15f, 17f, 19f, 22f, 26f, 30f };
        public static readonly float[] Dmg = { 0f, 2f, 2.4f, 3f, 3.6f, 4.4f, 5.2f };
        public static readonly float[] StepT = { 0f, 0f, 0.12f, 0.1f, 0.08f, 0.07f, 0.05f };

        /// <summary>샘플 s.st.ghost: 소방관이 지나온 자리(0이 최신, 30개).</summary>
        public readonly List<SamplePtD> Ghost = new List<SamplePtD>();

        /// <summary>샘플 s.st.trail: Lv6 물길(1.6초).</summary>
        public readonly List<SamplePtD> Trail = new List<SamplePtD>();

        public float StepClock;
        public float DropT;
        private float _lastX, _lastY;
        private bool _hasLast;

        // items-d.js:173 (샘플 s.st.path는 시연용 자동 달리기라 쓰지 않는다: 숲은 내가 움직인다)
        public override void Setup(SurvivorSim s)
        {
            Ghost.Clear();
            Trail.Clear();
            StepClock = 0f;
            DropT = 0f;
            _hasLast = false;
        }

        // items-d.js:174-201
        public override void Update(SurvivorSim s, float dt, int lv)
        {
            float px = s.PX, py = s.PY;
            float sp = Speed[lv] * (1f + (Surge * 0.3f));
            s.SampleSpeed = sp;
            float vx = _hasLast ? (px - _lastX) / Math.Max(dt, 1e-4f) : 0f, vy = _hasLast ? (py - _lastY) / Math.Max(dt, 1e-4f) : 0f;
            _lastX = px;
            _lastY = py;
            _hasLast = true;
            Ghost.Insert(0, new SamplePtD(px, py));
            while (Ghost.Count > 30) Ghost.RemoveAt(Ghost.Count - 1);
            // 몸 부딪치기
            float R = BumpR[lv], dmg = Dmg[lv], push = 90f + (lv * 25f);
            foreach (Enemy m in s.SAlive().ToArray())
            {
                if (m.Dead) continue;
                float mx = SurvivorSim.SX(m.Pos), my = SurvivorSim.SY(m.Pos);
                float d = SurvivorSim.Hypot(mx - px, my - py);
                if (d < R + SurvivorSim.SR(m) && !(SurvivorSim.Cd(m, "bt") > 0f))
                {
                    SurvivorSim.SetCd(m, "bt", 0.3f);
                    float L = d == 0f ? 1f : d;
                    s.SHit(m, dmg, ((mx - px) / L * push) + (vx * 0.15f), ((my - py) / L * push) + (vy * 0.15f));
                    s.HitFx(mx, my - 4f, lv, 0.7f);
                    if (lv >= 5)
                    {
                        s.SplashFx(mx, my, 22f, 6);
                        s.SHitArea(mx, my, 22f, 1.4f, 150f);
                    }
                }
            }
            // 발자국 물튀김(샘플은 늘 달린다: 숲은 달릴 때만 찍는다)
            StepClock -= dt;
            if (StepClock <= 0f && lv >= 2 && s.PlayerMoving)
            {
                StepClock = StepT[lv];
                SPart paw = s.Part(SPartKind.Paw, px + s.Rnd(-4f, 4f), py + 8f);
                paw.Life = 0.8f + (lv * 0.15f);
                for (int i = 0; i < lv; i++) s.Drop(px, py + 6f, s.Rnd(-50f, 50f), s.Rnd(-15f, 15f), s.Rnd(40f, 90f), 450f, 0.5f, s.Rnd(2f, 2.8f));
                if (lv >= 3)
                {
                    s.Ring(px, py + 8f, 0.3f, 10f + (lv * 3f), SurvivorSim.TierOf(lv).Core, 2f);
                    s.SHitArea(px, py + 6f, 10f + (lv * 3f), 0.5f + (lv * 0.2f), 60f);
                }
            }
            // Lv6: 물길
            if (lv == 6)
            {
                DropT -= dt;
                if (DropT <= 0f && s.PlayerMoving)
                {
                    DropT = 0.025f;
                    Trail.Add(new SamplePtD(px, py + 6f));
                }
                foreach (SamplePtD q in Trail) q.K += dt;
                Trail.RemoveAll(q => q.K >= 1.6f);
                foreach (Enemy m in s.SAlive().ToArray())
                {
                    if (m.Dead || SurvivorSim.Cd(m, "wt") > 0f) continue;
                    float mx = SurvivorSim.SX(m.Pos), my = SurvivorSim.SY(m.Pos);
                    foreach (SamplePtD q in Trail)
                    {
                        if (SurvivorSim.Hypot(mx - q.X, my - q.Y) < SurvivorSim.SR(m) + 14f)
                        {
                            SurvivorSim.SetCd(m, "wt", 0.12f);
                            s.SHit(m, 5f, s.Rnd(-40f, 40f), -60f);
                            if (s.Rnd(0f, 1f) < 0.5f) s.Drop(mx, my, s.Rnd(-60f, 60f), s.Rnd(-40f, 20f), s.Rnd(80f, 160f), 500f, 0.5f, 2.5f);
                            break;
                        }
                    }
                }
                if (s.Rnd(0f, 1f) < 0.6f) s.Star(px + s.Rnd(-6f, 6f), py + 4f, s.Rnd(-30f, 30f), s.Rnd(-30f, 30f), 0.4f, 3f, Rgb.None);
            }
        }
    }

    // ---------------------------------------------------------------- 13. 방화복 → 불사조 방화복 (items-d.js:244-333)
    public sealed class SampleSuit : SampleItem
    {
        public static readonly float[] Rad = { 0f, 19f, 22f, 25f, 28f, 32f, 46f };
        public static readonly float[] Dmg = { 0f, 1.4f, 2f, 3f, 3.6f, 4.4f, 8f };
        public static readonly float[] Push = { 0f, 75f, 100f, 130f, 165f, 200f, 240f };

        /// <summary>샘플 hurt: 닿을 때 깎이는 체력(샘플 체력 1 기준).</summary>
        public static readonly float[] HurtT = { 0f, 0.07f, 0.055f, 0.04f, 0.03f, 0.015f, 0f };

        public static readonly float[] PulseT = { 0f, 0f, 0f, 1.5f, 1.15f, 0.85f };

        /// <summary>샘플 s.st.wing: 부활 뒤 시간(−1이면 날개 없음).</summary>
        public float Wing = -1f;

        public bool Revived;

        /// <summary>샘플 s.st.ko: 쓰러짐 남은 시간(0.45초 뒤 부활).</summary>
        public float Ko;

        public float Pulse = 1f;
        public float Flap = 1f;

        /// <summary>샘플 s.shield: 닿은 직후 0.3 → 0(보호막이 번쩍인다).</summary>
        public float Shield;

        /// <summary>Lv3~5 물결 횟수(시험용).</summary>
        public int Pulses;

        public float RadiusAt(int lv)
        {
            return Rad[lv] * (1f + (Surge * 0.3f));
        }

        // items-d.js:261
        public override void Setup(SurvivorSim s)
        {
            Wing = -1f;
            Revived = false;
            Ko = 0f;
            s.SampleRevive = () => TryRevive(s);
        }

        // items-d.js:262
        public override void OnLevel(SurvivorSim s, int lv)
        {
            if (lv == 6)
            {
                Revived = false;
                Wing = -1f;
            }
        }

        /// <summary>
        /// 샘플 받는 피해(hurt)를 숲 피해 배율로: 샘플 표 ÷ 0.1(Lv1 0.7 → Lv5 0.15).
        /// Lv6 샘플 0(+부활 전 .09)은 부활을 보여 주려는 시연 값이라 Lv5와 같게 둔다. 방화복 HeatScale과 겹치지 않게 그만큼 나눈다.
        /// </summary>
        public static float HurtScale(SurvivorSim s, int lv)
        {
            float ratio = HurtT[Math.Min(lv, 5)] / 0.1f;
            return ratio / Math.Max(0.05f, s.Build.HeatScale);
        }

        // items-d.js:288 (체력 0 → 쓰러짐 0.45초). 숲 체력이 0이 되면 SurvivorSim이 부른다.
        private bool TryRevive(SurvivorSim s)
        {
            if (s.SampleLevel(Base) != 6 || Revived) return false;
            if (Ko > 0f)
            {
                s.Hp = 0.01f;
                return true;
            }
            float px = s.PX, py = s.PY;
            s.Hp = 0.01f;
            Ko = 0.45f;
            s.SSlow = Math.Max(s.SSlow, 0.4f);
            for (int i = 0; i < 50; i++)
            {
                float a = s.Rnd(0f, SurvivorSim.Tau), r = s.Rnd(80f, 200f);
                SPart p = s.Part(SPartKind.Mote, px, py);
                p.Sx = px + ((float)Math.Cos(a) * r);
                p.Sy = py + ((float)Math.Sin(a) * r * 0.7f);
                p.Tx = px;
                p.Ty = py - 8f;
                p.Seek = true;
                p.Life = 0.45f;
                p.Size = s.Rnd(1.5f, 3f);
            }
            // 샘플 metalText('쓰러짐…', silver): 구운 글자가 없어 글자 파티클로 띄운다.
            s.TextPop(px, py - 52f, "쓰러짐…", new Rgb(220, 226, 236), 12f);
            return true;
        }

        // items-d.js:324-333
        private void Revive(SurvivorSim s)
        {
            float x = s.PX, y = s.PY - 8f;
            Revived = true;
            Wing = 0f;
            s.Hp = 0.6f * s.MaxHp;
            s.SHitArea(x, y, 170f, 99f, 420f);
            s.SplashFx(x, y, 110f, 50);
            s.Prism(x, y, 0.7f, 190f);
            s.Prism(x, y, 0.9f, 120f);
            s.Glow(x, y, 0.5f, 170f, new Rgb(255, 240, 210));
            for (int i = 0; i < 80; i++)
            {
                float a = s.Rnd(0f, SurvivorSim.Tau), v = s.Rnd(150f, 360f);
                s.Star(x, y, (float)Math.Cos(a) * v, (float)Math.Sin(a) * v * 0.7f, s.Rnd(0.6f, 1f), s.Rnd(3f, 6f), Rgb.None);
            }
            for (int i = 0; i < 40; i++)
            {
                float a = s.Rnd(-(float)Math.PI, 0f), v = s.Rnd(120f, 300f);
                SPart p = s.Part(SPartKind.Confetti, x, y);
                p.Vx = (float)Math.Cos(a) * v;
                p.Vy = (float)Math.Sin(a) * v;
                p.Life = s.Rnd(1f, 1.6f);
                p.Size = s.Rnd(2f, 3.4f);
                p.Rot = s.Rnd(0f, SurvivorSim.Tau);
                p.Vr = s.Rnd(-8f, 8f);
            }
            s.SShake = Math.Max(s.SShake, 14f);
            s.SStop = Math.Max(s.SStop, 0.14f);
            s.SFlash = Math.Max(s.SFlash, 0.4f);
            s.SFlashColor = new Rgb(255, 250, 235);
        }

        // items-d.js:263-291
        public override void Update(SurvivorSim s, float dt, int lv)
        {
            Shield -= dt;
            s.SampleHurt = HurtScale(s, lv);
            float px = s.PX, py = s.PY, R = RadiusAt(lv);
            float dmg = Dmg[lv], push = Push[lv];
            if (Wing >= 0f) Wing += dt;
            if (Ko > 0f)
            {
                Ko -= dt;
                if (Ko <= 0f) Revive(s);
                return;
            }
            Tier T = SurvivorSim.TierOf(lv);
            foreach (Enemy m in s.SAlive().ToArray())
            {
                if (m.Dead) continue;
                float mx = SurvivorSim.SX(m.Pos), my = SurvivorSim.SY(m.Pos);
                float d = SurvivorSim.Hypot(mx - px, my - py);
                if (d < R + SurvivorSim.SR(m) && !(SurvivorSim.Cd(m, "su") > 0f))
                {
                    SurvivorSim.SetCd(m, "su", 0.3f);
                    float L = d == 0f ? 1f : d;
                    s.SHit(m, dmg, (mx - px) / L * push, (my - py) / L * push);
                    // 샘플 s.hp -= hurt: 숲은 몹이 닿는 피해를 따로 받으니 배율(SampleHurt)로 옮겼다.
                    Shield = 0.3f;
                    s.Ring(px, py - 4f, 0.3f, R + 6f, lv >= 5 ? new Rgb(255, 215, 110) : T.Core, 2f + (lv * 0.5f));
                    if (lv >= 3) s.HitFx(mx, my - 4f, lv, 0.7f);
                    if (lv >= 4)
                    {
                        s.SplashFx(mx, my, 16f + (lv * 2f), 5);
                        s.SHitArea(mx, my, 16f + (lv * 2f), 1f, 120f);
                    }
                    if (lv < 3) s.Ring(px, py, 0.25f, 16f, new Rgb(255, 90, 80), 2f);
                }
            }
            // Lv3~: 보호막이 주기적으로 물결을 밀어낸다(반격). 레벨마다 더 자주·더 넓게.
            if (lv >= 3 && lv < 6)
            {
                Pulse -= dt;
                if (Pulse <= 0f)
                {
                    Pulse = PulseT[lv];
                    Pulses++;
                    float PR = R + 18f + (lv * 8f);
                    s.SHitArea(px, py - 4f, PR, 1f + (lv * 0.8f), 160f);
                    s.Ring(px, py - 4f, 0.4f, PR * 1.1f, lv >= 5 ? new Rgb(255, 215, 110) : T.Core, 3f + (lv * 0.4f));
                    s.Glow(px, py - 4f, 0.3f, PR, lv >= 5 ? new Rgb(255, 220, 140) : T.Glow);
                    for (int i = 0; i < 6 + (lv * 2); i++)
                    {
                        float a2 = s.Rnd(0f, SurvivorSim.Tau);
                        s.Drop(px + ((float)Math.Cos(a2) * R), py + ((float)Math.Sin(a2) * R * 0.7f), (float)Math.Cos(a2) * 120f, (float)Math.Sin(a2) * 80f, s.Rnd(40f, 100f), 500f, 0.5f, 2.4f);
                    }
                }
            }
            // 샘플 284-287줄(천천히 회복·Lv6 2.8초 뒤 쓰러지기)은 시연용이라 옮기지 않는다.
            if (lv == 6 && Revived)
            {
                Flap -= dt;
                if (Flap <= 0f)
                {
                    Flap = 0.8f;
                    s.SHitArea(px, py - 6f, 125f, 8f, 260f);
                    s.Prism(px, py - 6f, 0.45f, 135f);
                    s.SplashFx(px, py, 70f, 16);
                    s.SShake = Math.Max(s.SShake, 4f);
                }
            }
            if (lv == 6 && Revived && s.Rnd(0f, 1f) < dt * 12f) s.Star(px + s.Rnd(-30f, 30f), py - 10f + s.Rnd(-14f, 6f), s.Rnd(-20f, 20f), s.Rnd(-40f, -10f), 0.5f, 3f, Rgb.None);
        }
    }
}
