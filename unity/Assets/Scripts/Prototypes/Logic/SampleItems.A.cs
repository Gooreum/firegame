using System;
using System.Collections.Generic;

namespace FireGame.Prototypes.Logic
{
    /// <summary>
    /// 숲 개편(2026-10-08, 승인 샘플 그대로): tools/levelup-art/items-a.js의 회전 스프링클러를 줄 단위로 옮겼다(물대포는 2026-10-08 겨누는 한 줄기 + 방수포 대폭발만).
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

    /// <summary>샘플 {x, y, k} 한 점(대폭발 고리).</summary>
    public sealed class SamplePtD
    {
        public float X, Y, K;

        public SamplePtD(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    // ---------------------------------------------------------------- 1. 물대포 → 고압 방수포
    /// <summary>
    /// 숲 물대포(2026-10-08 아이템 정리): 물줄기는 겨누는 한 줄기(SurvivorSim.FireWeapons, 고압 방수포면 모두 꿰뚫는 Jet)다.
    /// 이 샘플 아이템은 고압 방수포를 들었을 때만 생기고, 펌프에서 옮겨 온 대폭발(2.2초마다 내 둘레, items-d.js:120-133)만 맡는다.
    /// </summary>
    public sealed class HoseItem : SampleItem
    {
        public const float BoomEvery = 2.2f;
        public const float BoomR = 140f;

        /// <summary>대폭발 한 번이 둘레 건물 불을 줄이는 양(Lv6 배율 ×3.6 = 0.9를 2.2초마다).</summary>
        public const float NovaSoak = 0.25f;

        public float Boom;
        public readonly List<SamplePtD> Booms = new List<SamplePtD>();

        /// <summary>대폭발 횟수(시험용).</summary>
        public int Novas;

        public override void Setup(SurvivorSim s)
        {
            Boom = 1.2f;
            Booms.Clear();
        }

        public override void Update(SurvivorSim s, float dt, int lv)
        {
            if (lv < 6) return;
            Boom -= dt;
            if (Boom <= 0f)
            {
                Boom = BoomEvery;
                float x = s.PX, y = s.PY - 4f, w = s.SWide;
                Booms.Add(new SamplePtD(x, y));
                Novas++;
                s.SHitArea(x, y, BoomR, 12f, 380f);
                s.SSoakArea(x, y, BoomR, NovaSoak);
                s.SplashFx(x, y, 90f * w, 40);
                s.Prism(x, y, 0.6f, 150f * w);
                s.Glow(x, y, 0.4f, 150f * w, new Rgb(200, 235, 255));
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

    // ---------------------------------------------------------------- 2. 회전 스프링클러 → 물 왕관 (items-a.js:85-138)
    public sealed class SprinklerItem : SampleItem
    {
        public static readonly int[] N = { 0, 2, 2, 3, 3, 4, 4 };
        public static readonly float[] RTab = { 0f, 44f, 52f, 60f, 68f, 76f, 80f };
        private static readonly float[] Spin = { 0f, 2.4f, 2.6f, 2.8f, 3.3f, 3.6f, 3.8f };
        private static readonly float[] Dmg = { 0f, 2.1f, 2.1f, 2.2f, 2.6f, 4.2f, 4.2f };
        private static readonly float[] SpCd = { 0f, 0.34f, 0.3f, 0.25f, 0.2f, 0.15f, 0.15f };

        /// <summary>머리가 타는 건물 위를 지나는 동안 초당 줄이는 불(마을 0.4/s의 절반 — 레벨 배율로 자란다).</summary>
        public const float SprinklerSoak = 0.2f;
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
            float R = RTab[lv] * s.SWide;
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
            // 광각 노즐(SWide): 도는 반경·맞힘 반경·물줄기 길이가 함께 커진다(그림도 같은 배율).
            int n = N[lv];
            float w = s.SWide, R = RTab[lv] * w;
            Ang += dt * Spin[lv];
            Heads.Clear();
            float dmg = Dmg[lv], push = 160f + (lv * 40f);
            for (int i = 0; i < n; i++)
            {
                float a = Ang + (i * SurvivorSim.Tau / n), x = px + ((float)Math.Cos(a) * R), y = py + ((float)Math.Sin(a) * R * 0.72f);
                var h = new Head { X = x, Y = y, A = a };
                Heads.Add(h);
                // 머리가 타는 집 위를 지나면 적신다(숲 건물 적시기, docs §23).
                s.SSoakArea(x, y, 15f + (lv * 1.5f), SprinklerSoak * dt);
                if (s.Rnd(0f, 1f) < dt * (14f + (lv * 8f)))
                {
                    float sa = a + (s.ST * 8f) + s.Rnd(-0.3f, 0.3f);
                    float vz = s.Rnd(30f, 80f);
                    s.Drop(x, y - 6f, (float)Math.Cos(sa) * (100f + (lv * 20f)), (float)Math.Sin(sa) * (70f + (lv * 15f)), vz, 400f, 0.6f, s.Rnd(2f, 2.4f + (lv * 0.25f)));
                }
                foreach (Enemy m in s.SAlive().ToArray())
                {
                    float mx = SurvivorSim.SX(m.Pos), my = SurvivorSim.SY(m.Pos);
                    if (SurvivorSim.Hypot(mx - x, my - y) < SurvivorSim.SR(m) + ((15f + (lv * 1.5f)) * w) && !(SurvivorSim.Cd(m, "sp") > 0f))
                    {
                        SurvivorSim.SetCd(m, "sp", SpCd[lv]);
                        float L = SurvivorSim.Hypot(mx - px, my - py);
                        if (L == 0f) L = 1f;
                        s.SHit(m, dmg, (mx - px) / L * push, (my - py) / L * push);
                        s.HitFx(mx, my - 4f, lv, 0.8f);
                        if (lv >= 5)
                        {
                            s.SplashFx(mx, my, 30f * w, 6);
                            s.SHitArea(mx, my, 30f, 2f, 140f);
                        }
                    }
                }
                // Lv3~: 머리에서 바깥으로 짧은 물줄기
                if (lv >= 3 && lv < 6)
                {
                    float ja = a + (s.ST * 5f), x1 = x + ((float)Math.Cos(ja) * (30f + (lv * 6f)) * w), y1 = y + ((float)Math.Sin(ja) * (30f + (lv * 6f)) * w * 0.72f);
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
                    float a = Jet + (j * SurvivorSim.Tau / 8f), x0 = px + ((float)Math.Cos(a) * R), y0 = py + ((float)Math.Sin(a) * R * 0.72f), x1 = px + ((float)Math.Cos(a) * (R + (150f * w))), y1 = py + ((float)Math.Sin(a) * (R + (150f * w)) * 0.72f);
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
                    if (Math.Abs(d - R) < 12f * w && !(SurvivorSim.Cd(m, "rg") > 0f))
                    {
                        SurvivorSim.SetCd(m, "rg", 0.2f);
                        s.SHit(m, 3f, (mx - px) * 4f, (my - py) * 4f);
                    }
                }
            }
        }
    }
}
