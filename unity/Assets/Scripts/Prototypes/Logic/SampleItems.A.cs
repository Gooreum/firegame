using System;
using System.Collections.Generic;

namespace FireGame.Prototypes.Logic
{
    /// <summary>
    /// 숲 개편(2026-10-08, 승인 샘플 그대로): tools/levelup-art/items-a.js의 물대포·회전 스프링클러를 줄 단위로 옮겼다.
    /// 수치·순서·파티클 호출은 샘플과 같다. 좌표는 샘플 px(SampleCore), 샘플 m.cd[key] > s.t 는 Cd(e,key) > 0.
    /// 그림은 SurvivorView.SampleDraw.A.cs.
    /// </summary>
    public static partial class SampleItemsRegistry
    {
        static partial void MakeA(UpgradeId id, ref SampleItem made)
        {
            if (id == UpgradeId.Hose) made = new HoseItem();
            else if (id == UpgradeId.Sprinkler) made = new SprinklerItem();
        }
    }

    // ---------------------------------------------------------------- 1. 물대포 → 고압 방수포 (items-a.js:15-69)
    public sealed class HoseItem : SampleItem
    {
        private static readonly float[] CdTab = { 0f, 0.2f, 0.18f, 0.16f, 0.13f, 0.11f };
        private static readonly float[] DmgTab = { 0f, 1.6f, 1.6f, 1.8f, 2f, 2.4f };

        /// <summary>샘플 s.st.tg: 이번 틱 물줄기가 노리는 몹(가까운 순, 최대 min(lv,5)).</summary>
        public readonly List<Enemy> Tg = new List<Enemy>();

        /// <summary>샘플 s.st.cd(물줄기와 방수포가 함께 쓴다 — 샘플 그대로).</summary>
        public float CdT;

        /// <summary>샘플 s.st.a: 방수포 각도(라디안, 샘플 y 아래가 +). 아직 없으면 HasA=false(그림은 −1).</summary>
        public float A;
        public bool HasA;

        private readonly HashSet<Enemy> _used = new HashSet<Enemy>();

        // items-a.js:19
        public override void Setup(SurvivorSim s)
        {
            Tg.Clear();
            CdT = 0f;
        }

        // items-a.js:20-49
        public override void Update(SurvivorSim s, float dt, int lv)
        {
            float px = s.PX, py = s.PY;
            {
                int n = Math.Min(lv, 5), L5 = Math.Min(lv, 5);
                _used.Clear();
                Tg.Clear();
                for (int i = 0; i < n; i++)
                {
                    Enemy m = s.SNearest(px, py, 210f + (L5 * 12f), _used);
                    if (m != null)
                    {
                        _used.Add(m);
                        Tg.Add(m);
                    }
                }
                CdT -= dt;
                if (CdT <= 0f)
                {
                    CdT = CdTab[L5];
                    float dmg = DmgTab[L5], reach = L5 >= 3 ? 60f + (L5 * 14f) : 0f, w = 3f + (L5 * 1.3f);
                    foreach (Enemy m in Tg)
                    {
                        float mx = SurvivorSim.SX(m.Pos), my = SurvivorSim.SY(m.Pos);
                        float dx = mx - px, dy = my - py, L = SurvivorSim.Hypot(dx, dy);
                        if (L == 0f) L = 1f;
                        float ex = mx + (dx / L * reach), ey = my + (dy / L * reach);
                        foreach (Enemy o in s.SAlive().ToArray())
                        {
                            if (o == m || (reach > 0f && SurvivorSim.SegDist(SurvivorSim.SX(o.Pos), SurvivorSim.SY(o.Pos), px, py, ex, ey) < SurvivorSim.SR(o) + (w * 0.5f)))
                                s.SHit(o, dmg, dx / L * (30f + (lv * 14f)), dy / L * (30f + (lv * 14f)));
                        }
                        s.HitFx(mx, my - 4f, lv, 0.6f);
                        if (L5 >= 5 && !(SurvivorSim.Cd(m, "hs") > 0f))
                        {
                            SurvivorSim.SetCd(m, "hs", 0.3f);
                            s.SplashFx(ex, ey, 26f, 6);
                            s.SHitArea(ex, ey, 26f, 1.5f, 120f);
                        }
                    }
                }
                if (lv < 6) return;
            }
            // 고압 방수포: 굵은 물기둥이 부채꼴로 쓸고, 양옆 물줄기 4개가 같이 쏜다.
            // 몹이 없을 때 샘플 { x: P.x + 100, y: 60 }(소방관 y 170 기준) → 소방관 기준 (+100, −110).
            if (!s.SCrowd(out float cx, out float cy))
            {
                cx = px + 100f;
                cy = py - 110f;
            }
            float bas = (float)Math.Atan2(cy - py, cx - px);
            A = bas + ((float)Math.Sin(s.ST * 3.2f) * 1.1f);
            HasA = true;
            float a = A, len = 330f;
            float x1 = px + ((float)Math.Cos(a) * len), y1 = py + ((float)Math.Sin(a) * len);
            CdT -= dt;
            if (CdT <= 0f)
            {
                CdT = 0.07f;
                foreach (Enemy m in s.SAlive().ToArray())
                {
                    float mx = SurvivorSim.SX(m.Pos), my = SurvivorSim.SY(m.Pos);
                    if (SurvivorSim.SegDist(mx, my, px, py, x1, y1) < 36f + SurvivorSim.SR(m))
                    {
                        s.SHit(m, 6f, (float)Math.Cos(a) * 110f, (float)Math.Sin(a) * 110f);
                        if (s.Rnd(0f, 1f) < 0.5f) s.HitFx(mx, my - 4f, 6, 0.5f);
                    }
                }
            }
            for (int i = 0; i < 4; i++)
            {
                float u = s.Rnd(0.15f, 1f);
                float x = px + ((float)Math.Cos(a) * len * u) + s.Rnd(-10f, 10f);
                float y = py + ((float)Math.Sin(a) * len * u) + s.Rnd(-10f, 10f);
                float vz = s.Rnd(40f, 120f), vx = s.Rnd(-60f, 60f), vy = s.Rnd(-60f, 60f);
                s.Drop(x, y, vx, vy, vz, 500f, 0.6f, s.Rnd(2.4f, 4f));
            }
            s.SShake = Math.Max(s.SShake, 2.2f);
        }
    }

    // ---------------------------------------------------------------- 2. 회전 스프링클러 → 물 왕관 (items-a.js:85-138)
    public sealed class SprinklerItem : SampleItem
    {
        public static readonly int[] N = { 0, 2, 2, 3, 3, 4, 4 };
        public static readonly float[] RTab = { 0f, 44f, 52f, 60f, 68f, 76f, 80f };
        private static readonly float[] Spin = { 0f, 2.4f, 2.6f, 2.8f, 3.3f, 3.6f, 3.8f };
        private static readonly float[] Dmg = { 0f, 2.1f, 2.1f, 2.2f, 2.6f, 4.2f, 4.2f };
        private static readonly float[] SpCd = { 0f, 0.34f, 0.3f, 0.25f, 0.2f, 0.15f, 0.15f };
        private static readonly string[] SjKey = { "sj0", "sj1", "sj2", "sj3" };
        private static readonly string[] JKey = { "j0", "j1", "j2", "j3", "j4", "j5", "j6", "j7" };

        public sealed class Head
        {
            public float X, Y, A;
            public bool HasJet;
            public float JetX1, JetY1;
        }

        /// <summary>샘플 s.st.a(머리 회전각)·s.st.heads·s.st.jet(Lv6 8갈래 분사 각).</summary>
        public float Ang;
        public readonly List<Head> Heads = new List<Head>();
        public float Jet;

        // items-a.js:89
        public override void Setup(SurvivorSim s)
        {
            Ang = 0f;
            Heads.Clear();
            Jet = 0f;
        }

        // items-a.js:90-94
        public override void OnLevel(SurvivorSim s, int lv)
        {
            // 새로 는 스프링클러가 빛나며 나타난다.
            int n = N[lv];
            float R = RTab[lv];
            for (int i = 0; i < n; i++)
            {
                float a = Ang + (i * SurvivorSim.Tau / n);
                float x = s.PX + ((float)Math.Cos(a) * R), y = s.PY + ((float)Math.Sin(a) * R * 0.72f);
                s.Glow(x, y - 6f, 0.5f, 30f, SurvivorSim.TierOf(lv).Glow);
                s.Ring(x, y - 6f, 0.45f, 26f, new Rgb(255, 255, 255), 3f);
            }
        }

        // items-a.js:95-112
        public override void Update(SurvivorSim s, float dt, int lv)
        {
            float px = s.PX, py = s.PY;
            int n = N[lv];
            float R = RTab[lv];
            Ang += dt * Spin[lv];
            Heads.Clear();
            float dmg = Dmg[lv], push = 160f + (lv * 40f);
            for (int i = 0; i < n; i++)
            {
                float a = Ang + (i * SurvivorSim.Tau / n), x = px + ((float)Math.Cos(a) * R), y = py + ((float)Math.Sin(a) * R * 0.72f);
                var h = new Head { X = x, Y = y, A = a };
                Heads.Add(h);
                if (s.Rnd(0f, 1f) < dt * (14f + (lv * 8f)))
                {
                    float sa = a + (s.ST * 8f) + s.Rnd(-0.3f, 0.3f);
                    float vz = s.Rnd(30f, 80f);
                    s.Drop(x, y - 6f, (float)Math.Cos(sa) * (100f + (lv * 20f)), (float)Math.Sin(sa) * (70f + (lv * 15f)), vz, 400f, 0.6f, s.Rnd(2f, 2.4f + (lv * 0.25f)));
                }
                foreach (Enemy m in s.SAlive().ToArray())
                {
                    float mx = SurvivorSim.SX(m.Pos), my = SurvivorSim.SY(m.Pos);
                    if (SurvivorSim.Hypot(mx - x, my - y) < SurvivorSim.SR(m) + 15f + (lv * 1.5f) && !(SurvivorSim.Cd(m, "sp") > 0f))
                    {
                        SurvivorSim.SetCd(m, "sp", SpCd[lv]);
                        float L = SurvivorSim.Hypot(mx - px, my - py);
                        if (L == 0f) L = 1f;
                        s.SHit(m, dmg, (mx - px) / L * push, (my - py) / L * push);
                        s.HitFx(mx, my - 4f, lv, 0.8f);
                        if (lv >= 5)
                        {
                            s.SplashFx(mx, my, 30f, 6);
                            s.SHitArea(mx, my, 30f, 2f, 140f);
                        }
                    }
                }
                // Lv3~: 머리에서 바깥으로 짧은 물줄기
                if (lv >= 3 && lv < 6)
                {
                    float ja = a + (s.ST * 5f), x1 = x + ((float)Math.Cos(ja) * (30f + (lv * 6f))), y1 = y + ((float)Math.Sin(ja) * (30f + (lv * 6f)) * 0.72f);
                    foreach (Enemy m in s.SAlive().ToArray())
                    {
                        float mx = SurvivorSim.SX(m.Pos), my = SurvivorSim.SY(m.Pos);
                        if (SurvivorSim.SegDist(mx, my, x, y, x1, y1) < SurvivorSim.SR(m) + 3f && !(SurvivorSim.Cd(m, SjKey[i]) > 0f))
                        {
                            SurvivorSim.SetCd(m, SjKey[i], 0.3f);
                            s.SHit(m, dmg * 0.6f, (float)Math.Cos(ja) * 120f, (float)Math.Sin(ja) * 120f);
                        }
                    }
                    h.HasJet = true;
                    h.JetX1 = x1;
                    h.JetY1 = y1;
                }
            }
            if (lv == 6)
            {
                Jet += dt * 1.6f;
                for (int j = 0; j < 8; j++)
                {
                    float a = Jet + (j * SurvivorSim.Tau / 8f), x0 = px + ((float)Math.Cos(a) * R), y0 = py + ((float)Math.Sin(a) * R * 0.72f), x1 = px + ((float)Math.Cos(a) * (R + 150f)), y1 = py + ((float)Math.Sin(a) * (R + 150f) * 0.72f);
                    foreach (Enemy m in s.SAlive().ToArray())
                    {
                        float mx = SurvivorSim.SX(m.Pos), my = SurvivorSim.SY(m.Pos);
                        if (SurvivorSim.SegDist(mx, my, x0, y0, x1, y1) < SurvivorSim.SR(m) + 7f && !(SurvivorSim.Cd(m, JKey[j]) > 0f))
                        {
                            SurvivorSim.SetCd(m, JKey[j], 0.2f);
                            s.SHit(m, 4f, (float)Math.Cos(a) * 260f, (float)Math.Sin(a) * 200f);
                            s.HitFx(mx, my - 4f, 6, 0.5f);
                        }
                    }
                }
                foreach (Enemy m in s.SAlive().ToArray())
                {
                    float mx = SurvivorSim.SX(m.Pos), my = SurvivorSim.SY(m.Pos);
                    float d = SurvivorSim.Hypot(mx - px, (my - py) / 0.72f);
                    if (Math.Abs(d - R) < 12f && !(SurvivorSim.Cd(m, "rg") > 0f))
                    {
                        SurvivorSim.SetCd(m, "rg", 0.2f);
                        s.SHit(m, 3f, (mx - px) * 4f, (my - py) * 4f);
                    }
                }
            }
        }
    }
}
