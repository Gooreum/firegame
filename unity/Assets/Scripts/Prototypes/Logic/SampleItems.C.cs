using System;
using System.Collections.Generic;

namespace FireGame.Prototypes.Logic
{
    /// <summary>
    /// 숲 샘플 아이템 C(승인 샘플 tools/levelup-art/items-c.js를 줄 단위로 옮김): 비눗방울·맨홀 간헐천·물 사슬(거품 눈덩이는 2026-10-08 숲에서 뺐다).
    /// 수치·순서·파티클 호출은 샘플 그대로. 샘플 화면(480×270, 고정 카메라) 경계는 소방관 둘레 숲 화면(SurvivorSim.SViewHalfW/H)으로 옮긴다.
    /// 그림은 SurvivorView.SampleDraw.C.cs가 이 상태를 읽어 그린다.
    /// </summary>
    public static partial class SampleItemsRegistry
    {
        static partial void MakeC(UpgradeId id, ref SampleItem made)
        {
            switch (id)
            {
                case UpgradeId.Bubble: made = new SampleBubbleItem(); break;
                case UpgradeId.Manhole: made = new SampleManholeItem(); break;
                case UpgradeId.Chain: made = new SampleChainItem(); break;
            }
        }
    }

    /// <summary>items-c.js 공용 도우미(L5·glint) + 샘플 화면 경계.</summary>
    public static class SampleC
    {
        /// <summary>숲 화면 반폭·반높이(SurvivorSim.SViewHalfW/H): 소방관이 화면 가운데라고 본다.</summary>
        public const float HalfW = SurvivorSim.SViewHalfW, HalfH = SurvivorSim.SViewHalfH;

        // items-c.js:6
        public static int L5(int lv)
        {
            return Math.Min(lv, 5);
        }

        public static float Clamp(float v, float a, float b)
        {
            return Math.Max(a, Math.Min(b, v));
        }

        /// <summary>items-c.js:8-11 glint: 무지개 반짝이(Lv6) / 금 반짝이(Lv5) 한 점.</summary>
        public static void Glint(SurvivorSim s, float x, float y, int lv, int n = 1)
        {
            if (lv < 5) return;
            for (int i = 0; i < n; i++) s.Star(x + s.Rnd(-6f, 6f), y + s.Rnd(-6f, 6f), s.Rnd(-30f, 30f), s.Rnd(-50f, -10f), s.Rnd(0.3f, 0.5f), s.Rnd(2.5f, 4f), lv >= 6 ? Rgb.None : new Rgb(255, 215, 110));
        }

        public static float Chance(SurvivorSim s)
        {
            return s.Rnd(0f, 1f);
        }

        /// <summary>샘플 m.kill(s): 잡아 둔(삼킴·방울) 몹을 풀어 처치한다(공중이면 SHit가 안 먹으니 땅에 내린다).</summary>
        public static void KillHeld(SurvivorSim s, Enemy m)
        {
            if (m.Dead) return;
            m.AirZ = 0f;
            m.AirVz = 0f;
            m.Held = false;
            m.SHide = false;
            s.SHit(m, 99f);
        }
    }

    // ---------------------------------------------------------------- 4. 비눗방울 → 방울 폭포 (items-c.js:112-209)
    public sealed class SampleBubbleItem : SampleItem
    {
        public struct Pt
        {
            public float X, Y;
        }

        public sealed class Shot
        {
            public float X, Y;
            public Enemy M;
            public readonly List<Pt> Trail = new List<Pt>();
            public bool Done;
        }

        public sealed class Cap
        {
            public Enemy M;
            public float K, X, Y;
            public bool Done;
        }

        /// <summary>거대 방울(s.st.big): 샘플 화면 고정 자리 → 소방관 기준 자리(Ox,Oy)를 따라간다. X,Y는 이번 틱 자리.</summary>
        public sealed class BigBubble
        {
            public float Ox, Oy, X, Y, R = 16f, K;
            public int N;
            public readonly List<Enemy> Inside = new List<Enemy>();
        }

        public sealed class Fall
        {
            public float X, Y, K, W;
        }

        public readonly List<Shot> Shots = new List<Shot>();
        public readonly List<Cap> Caps = new List<Cap>();
        public float Cd;
        public BigBubble Big;
        public Fall F;

        public static readonly int[] Count = { 0, 1, 2, 3, 3, 4, 5 };
        public static readonly float[] Cool = { 0f, 0.9f, 0.7f, 0.7f, 0.5f, 0.42f, 0.3f };

        /// <summary>샘플 거대 방울 자리(250,64) − 소방관(150,170).</summary>
        public const float BigOx = 100f, BigOy = -106f;

        // items-c.js:133
        public override void Setup(SurvivorSim s)
        {
            Shots.Clear();
            Caps.Clear();
            Cd = 0.3f;
            Big = null;
        }

        // items-c.js:134
        public override void OnLevel(SurvivorSim s, int lv)
        {
            if (lv == 6) Big = new BigBubble { Ox = BigOx, Oy = BigOy, X = s.PX + BigOx, Y = s.PY + BigOy };
        }

        // items-c.js:122-128 popBubble
        public static void PopBubble(SurvivorSim s, float x, float y, int lv)
        {
            for (int i = 0; i < 8 + (lv * 2); i++)
            {
                float a = s.Rnd(0f, SurvivorSim.Tau), v = s.Rnd(80f, 160f);
                SPart p = s.Part(SPartKind.Spark, x, y);
                p.Vx = (float)Math.Cos(a) * v;
                p.Vy = (float)Math.Sin(a) * v;
                p.Life = 0.35f;
                p.Size = 2.5f;
                p.Color = new Rgb(220, 240, 255);
                p.HasColor = true;
            }
            s.Ring(x, y, 0.3f, 22f + (lv * 4f), lv >= 5 ? new Rgb(255, 220, 140) : new Rgb(255, 255, 255), 2f + (lv * 0.4f));
            for (int i = 0; i < 4 + lv; i++)
            {
                SPart d = s.Drop(x + s.Rnd(-10f, 10f), y, 0f, 0f, -20f, 500f, 0.7f, 2.5f);
                d.Z = 20f;
            }
        }

        // items-c.js:135-182
        public override void Update(SurvivorSim s, float dt, int lv)
        {
            float px = s.PX, py = s.PY;
            int l = SampleC.L5(lv);
            if (Big != null)
            {
                Big.X = px + Big.Ox;
                Big.Y = py + Big.Oy;
            }
            Cd -= dt;
            if (Cd <= 0f)
            {
                int n = Count[lv];
                var used = new HashSet<Enemy>();
                for (int i = 0; i < n; i++)
                {
                    Enemy m = s.SNearest(px, py, 230f, used);
                    if (m == null) break;
                    used.Add(m);
                    Shots.Add(new Shot { X = px + 4f, Y = py - 12f, M = m });
                }
                Cd = Cool[lv];
            }
            foreach (Shot b in Shots)
            {
                if (b.M.Dead || b.M.Held || b.M.AirZ > 0f)
                {
                    b.Done = true;
                    continue;
                }
                float mx = SurvivorSim.SX(b.M.Pos), my = SurvivorSim.SY(b.M.Pos);
                float dx = mx - b.X, dy = my - 6f - b.Y, L = SurvivorSim.Hypot(dx, dy), sp = 300f + (l * 30f);
                if (L == 0f) L = 1f;
                b.Trail.Insert(0, new Pt { X = b.X, Y = b.Y });
                if (b.Trail.Count > 6) b.Trail.RemoveAt(b.Trail.Count - 1);
                b.X += dx / L * sp * dt;
                b.Y += dy / L * sp * dt;
                if (L < 10f)
                {
                    b.Done = true;
                    if (SurvivorSim.SBig(b.M) && l < 3)
                    {
                        s.SHit(b.M, 3f);
                        PopBubble(s, mx, my - 6f, lv);
                        continue;
                    }
                    b.M.Held = true;
                    Caps.Add(new Cap { M = b.M, X = mx, Y = my });
                    s.Ring(mx, my - 6f, 0.25f, 20f, new Rgb(220, 240, 255), 2f);
                }
            }
            Shots.RemoveAll(b => b.Done);
            BigBubble B = Big;
            foreach (Cap c in Caps)
            {
                c.K += dt;
                Enemy m = c.M;
                m.AirVx = m.AirVy = 0f;
                m.AirVz = 0f;
                m.Knock = default;
                if (m.Dead)
                {
                    c.Done = true;
                    continue;
                }
                if (lv == 6 && B != null)
                {
                    // 거대 방울로 빨려 올라간다
                    c.X += (B.X - c.X) * dt * 3f;
                    c.Y += (B.Y + 60f - c.Y) * dt * 3f;
                    SurvivorSim.SetS(m, c.X, c.Y);
                    m.AirZ = Math.Min(60f, m.AirZ + (dt * 120f));
                    if (SurvivorSim.Hypot(c.X - B.X, c.Y - (B.Y + 60f)) < 14f)
                    {
                        c.Done = true;
                        B.N++;
                        B.R = Math.Min(56f, B.R + 2.2f);
                        // 샘플은 m.dead = true(방울 속 보라 점으로 그린다) → 게임은 숨겨 두었다가 물폭포 때 처치.
                        m.AirZ = 0f;
                        m.SHide = true;
                        B.Inside.Add(m);
                        SampleC.Glint(s, B.X, B.Y, 6, 2);
                    }
                    continue;
                }
                m.AirZ = Math.Min(46f, c.K * 55f);
                float x = c.X + ((float)Math.Sin(c.K * 6f) * 4f);
                SurvivorSim.SetS(m, x, c.Y);
                if (c.K > 1.0f)
                {
                    c.Done = true;
                    PopBubble(s, x, c.Y - m.AirZ, lv);
                    SampleC.KillHeld(s, m);
                    if (l >= 3)
                    {
                        s.SplashFx(c.X, c.Y, (18f + (l * 3f)) * s.SWide, 8);
                        s.SHitArea(c.X, c.Y, 18f + (l * 3f), l >= 5 ? 3f : 2f, 100f);
                        s.HitFx(c.X, c.Y, lv, 0.7f);
                    }
                }
            }
            Caps.RemoveAll(c => c.Done);
            if (lv == 6 && B != null)
            {
                foreach (Enemy e in B.Inside)
                {
                    if (!e.Dead) SurvivorSim.SetS(e, B.X, B.Y + 60f);
                }
                B.K += dt;
                if (B.K > 1.6f)
                {
                    // 펑 → 아래로 물폭포
                    float fx = B.X, fy = B.Y + 70f;
                    for (int i = 0; i < 50; i++)
                    {
                        SPart d = s.Drop(B.X + s.Rnd(-B.R, B.R), fy, 0f, 0f, 0f, 500f, 1.2f, 0f);
                        d.Z = 70f + s.Rnd(0f, 40f);
                        d.Vx = s.Rnd(-70f, 70f);
                        d.Vy = s.Rnd(-10f, 50f);
                        d.Vz = s.Rnd(-40f, 40f);
                        d.Size = s.Rnd(2.5f, 4.5f);
                    }
                    s.SplashFx(fx, fy, 80f * s.SWide, 26);
                    foreach (Enemy e in B.Inside)
                    {
                        if (e.Dead) continue;
                        SurvivorSim.SetS(e, fx, fy);
                        SampleC.KillHeld(s, e);
                    }
                    s.SHitArea(fx, fy, 120f, 99f, 260f);
                    s.HitFx(fx, fy, 6, 2f);
                    for (int i = 0; i < 26; i++)
                    {
                        float a = s.Rnd(0f, SurvivorSim.Tau);
                        SPart p = s.Part(SPartKind.Spark, B.X, B.Y);
                        p.Vx = (float)Math.Cos(a) * 240f;
                        p.Vy = (float)Math.Sin(a) * 240f;
                        p.Life = 0.5f;
                        p.Size = 3f;
                        p.Color = new Rgb(220, 240, 255);
                        p.HasColor = true;
                    }
                    s.SShake = 10f;
                    s.SFlash = Math.Max(s.SFlash, 0.3f);
                    if (B.N > 0) s.TextPop(B.X, B.Y - 30f, "×" + B.N, new Rgb(191, 232, 255), 22f);
                    F = new Fall { X = fx, Y = B.Y, K = 0f, W = B.R * 1.6f * s.SWide };
                    // 샘플 x: rand(180, 320) − 소방관 150
                    float ox = s.Rnd(180f, 320f) - 150f;
                    Big = new BigBubble { Ox = ox, Oy = BigOy, X = px + ox, Y = py + BigOy };
                }
            }
            if (F != null)
            {
                F.K += dt;
                if (F.K > 0.7f) F = null;
            }
        }
    }

    // ---------------------------------------------------------------- 5. 맨홀 간헐천 → 수도관 폭발 (items-c.js:211-317)
    public sealed class SampleManholeItem : SampleItem
    {
        public struct Hole
        {
            public float X, Y;
        }

        public sealed class Geyser
        {
            public float X, Y, P, K, Warn;
            public int Lv;

            /// <summary>광각 노즐 배율(솟을 때의 SWide): 맞힘 반경·물기둥 굵기에 곱한다(그림도).</summary>
            public float Wd = 1f;
        }

        public struct Pt
        {
            public float X, Y;
        }

        public sealed class Crack
        {
            public readonly List<Pt> Pts = new List<Pt>();
            public float K;
        }

        public readonly List<Geyser> G = new List<Geyser>();
        public readonly List<Crack> Cracks = new List<Crack>();
        public float Cd, Ccd;

        public static readonly int[] Count = { 0, 1, 2, 2, 3, 4 };
        public static readonly float[] Power = { 0f, 1f, 1.1f, 1.3f, 1.45f, 1.8f };
        public static readonly float[] Cool = { 0f, 1.4f, 1.2f, 0.95f, 0.85f, 0.6f };

        /// <summary>
        /// 샘플 MH(화면에 박힌 맨홀 6개) → 게임: 월드에 박힌 격자(칸 100px마다 한 개, 칸 안 자리는 칸 번호로 정해진다).
        /// 소방관이 걸어도 맨홀은 땅에 그대로 있다. 샘플 화면 한 장(480×270)에 10~13개.
        /// </summary>
        public const float Cell = 100f;

        public static void HolesIn(float x0, float y0, float x1, float y1, List<Hole> into)
        {
            into.Clear();
            int cx0 = (int)Math.Floor(x0 / Cell), cx1 = (int)Math.Floor(x1 / Cell);
            int cy0 = (int)Math.Floor(y0 / Cell), cy1 = (int)Math.Floor(y1 / Cell);
            for (int cy = cy0; cy <= cy1; cy++)
            {
                for (int cx = cx0; cx <= cx1; cx++)
                {
                    uint h = unchecked(((uint)cx * 73856093u) ^ ((uint)cy * 19349663u) ^ 0x9e3779b9u);
                    h ^= h << 13;
                    h ^= h >> 17;
                    h ^= h << 5;
                    float ux = (h & 0xffff) / 65535f, uy = ((h >> 16) & 0xffff) / 65535f;
                    float x = (cx * Cell) + 15f + (ux * (Cell - 30f)), y = (cy * Cell) + 15f + (uy * (Cell - 30f));
                    if (x < x0 || x > x1 || y < y0 || y > y1) continue;
                    into.Add(new Hole { X = x, Y = y });
                }
            }
        }

        private readonly List<Hole> _mh = new List<Hole>();

        /// <summary>샘플 MH: 지금 숲 화면(소방관 둘레 SViewHalfW×SViewHalfH) 안 맨홀.</summary>
        public List<Hole> ScreenHoles(SurvivorSim s)
        {
            HolesIn(s.PX - SampleC.HalfW, s.PY - SampleC.HalfH, s.PX + SampleC.HalfW, s.PY + SampleC.HalfH, _mh);
            return _mh;
        }

        // items-c.js:213
        private void MhGeyser(SurvivorSim s, float x, float y, float p, float warn, int lv)
        {
            G.Add(new Geyser { X = x, Y = y, P = p, K = -warn, Warn = warn, Lv = lv, Wd = s.SWide });
        }

        // items-c.js:214-226
        private void MhStep(SurvivorSim s, float dt)
        {
            foreach (Geyser g in G)
            {
                float before = g.K;
                g.K += dt;
                if (before < 0f && g.K >= 0f)
                {
                    float R = 34f * g.P * g.Wd;
                    foreach (Enemy m in s.SAlive().ToArray())
                    {
                        float mx = SurvivorSim.SX(m.Pos), my = SurvivorSim.SY(m.Pos);
                        float d = SurvivorSim.Hypot(mx - g.X, my - g.Y);
                        if (d < R + SurvivorSim.SR(m))
                        {
                            float dl = d == 0f ? 1f : d;
                            s.SLaunch(m, s.Rnd(330f, 440f) * (float)Math.Sqrt(g.P), (mx - g.X) / dl * 90f, (my - g.Y) / dl * 55f);
                        }
                    }
                    s.SplashFx(g.X, g.Y, 30f * g.P * g.Wd, 10 + (g.Lv * 2));
                    s.HitFx(g.X, g.Y, g.Lv, 1f);
                    s.SShake = Math.Max(s.SShake, 3f + (g.P * 2.5f));
                    if (g.Lv >= 4) s.SFlash = Math.Max(s.SFlash, 0.08f);
                }
                if (g.K > 0f && g.K < 0.5f && SampleC.Chance(s) < dt * (30f + (g.Lv * 8f)))
                {
                    SPart d = s.Drop(g.X + (s.Rnd(-8f, 8f) * g.P), g.Y, 0f, 0f, 0f, 600f, 1f, 0f);
                    d.Z = s.Rnd(40f, 120f) * g.P;
                    d.Vx = s.Rnd(-90f, 90f);
                    d.Vy = s.Rnd(-30f, 30f);
                    d.Vz = s.Rnd(0f, 100f);
                    d.Size = s.Rnd(2f, 3.5f);
                }
                if (g.K > 0f && g.K < 0.4f) SampleC.Glint(s, g.X, g.Y - (60f * g.P), g.Lv, SampleC.Chance(s) < 0.4f ? 1 : 0);
            }
            G.RemoveAll(g => g.K >= 0.8f);
        }

        // items-c.js:274
        public override void Setup(SurvivorSim s)
        {
            G.Clear();
            Cd = 0.6f;
            Cracks.Clear();
            Ccd = 0.4f;
        }

        // items-c.js:275-302
        public override void Update(SurvivorSim s, float dt, int lv)
        {
            int l = SampleC.L5(lv);
            Cd -= dt;
            if (Cd <= 0f)
            {
                Enemy[] alive = s.SAlive().ToArray();
                int Score(Hole h)
                {
                    int k = 0;
                    foreach (Enemy m in alive)
                    {
                        if (SurvivorSim.Hypot(SurvivorSim.SX(m.Pos) - h.X, SurvivorSim.SY(m.Pos) - h.Y) < 70f * s.SWide) k++;
                    }
                    return k;
                }
                bool Busy(Hole h)
                {
                    foreach (Geyser g in G)
                    {
                        if (SurvivorSim.Hypot(g.X - h.X, g.Y - h.Y) < 2f) return true;
                    }
                    return false;
                }
                var sorted = new List<Hole>();
                foreach (Hole h in ScreenHoles(s))
                {
                    if (!Busy(h)) sorted.Add(h);
                }
                var scores = new Dictionary<int, int>();
                for (int i = 0; i < sorted.Count; i++) scores[i] = Score(sorted[i]);
                // 샘플 sort((a,b) => score(b) - score(a))는 안정 정렬: 점수 같으면 원래 순서.
                var order = new List<int>();
                for (int i = 0; i < sorted.Count; i++) order.Add(i);
                order.Sort((a, b) => scores[b] != scores[a] ? scores[b] - scores[a] : a - b);
                int n = Count[l];
                float p = Power[l];
                for (int i = 0; i < Math.Min(n, order.Count); i++)
                {
                    Hole h = sorted[order[i]];
                    if (scores[order[i]] > 0) MhGeyser(s, h.X, h.Y, p, 0.4f, lv);
                }
                Cd = Cool[l];
            }
            if (lv == 6)
            {
                Ccd -= dt;
                if (Ccd <= 0f)
                {
                    // 맨홀에서 몹 떼 쪽으로 땅이 갈라지며 물기둥 도미노
                    Enemy[] a0 = s.SAlive().ToArray();
                    List<Hole> mh = ScreenHoles(s);
                    float left = s.PX - SampleC.HalfW, top = s.PY - SampleC.HalfH;
                    for (int c = 0; c < 2 && mh.Count > 0; c++)
                    {
                        Hole h = mh[(int)((((long)Math.Floor(s.ST * 3f)) + (c * 2)) % mh.Count)];
                        float tx, ty;
                        if (a0.Length > 0)
                        {
                            Enemy t = a0[Math.Min(a0.Length - 1, (int)Math.Floor(SampleC.Chance(s) * a0.Length))];
                            tx = SurvivorSim.SX(t.Pos);
                            ty = SurvivorSim.SY(t.Pos);
                        }
                        else
                        {
                            tx = left + 300f;
                            ty = top + 100f;
                        }
                        float a = (float)Math.Atan2(ty - h.Y, tx - h.X);
                        var crack = new Crack();
                        for (int i = 0; i < 9; i++)
                        {
                            float x = h.X + ((float)Math.Cos(a) * 30f * i), y = h.Y + ((float)Math.Sin(a) * 30f * i);
                            if (x < left + 10f || x > left + (2f * SampleC.HalfW) - 10f || y < top + 10f || y > top + (2f * SampleC.HalfH) - 10f) break;
                            crack.Pts.Add(new Pt { X = x, Y = y });
                            MhGeyser(s, x, y, 1.15f, 0.22f + (i * 0.08f), 6);
                        }
                        Cracks.Add(crack);
                    }
                    Ccd = 1.1f;
                }
            }
            MhStep(s, dt);
            foreach (Crack c in Cracks) c.K += dt;
            Cracks.RemoveAll(c => c.K >= 1.4f);
        }
    }

    // ---------------------------------------------------------------- 6. 물 사슬 → 해일 사슬 (items-c.js:319-366)
    public sealed class SampleChainItem : SampleItem
    {
        public struct Pt
        {
            public float X, Y;
            public Enemy M;
        }

        public sealed class Bolt
        {
            public List<List<Pt>> Segs = new List<List<Pt>>();
            public float K;
            public int Lv;
        }

        public readonly List<Bolt> Bolts = new List<Bolt>();
        public float Cd;

        public static readonly int[] Chains = { 0, 1, 1, 2, 2, 2, 3 };
        public static readonly int[] Hops = { 0, 3, 4, 4, 5, 6, 8 };
        public static readonly float[] Dmg = { 0f, 3f, 3.4f, 3.8f, 4.2f, 5f, 6f };
        public static readonly float[] Shake = { 0f, 1.5f, 2f, 2.5f, 3f, 4f, 6f };
        public static readonly float[] Cool = { 0f, 0.95f, 0.9f, 0.85f, 0.75f, 0.65f, 0.5f };

        // items-c.js:320-324 chainPick
        public static List<Pt> ChainPick(SurvivorSim s, float x, float y, int hops, HashSet<Enemy> skip)
        {
            var pts = new List<Pt> { new Pt { X = x, Y = y } };
            float cx = x, cy = y;
            for (int i = 0; i < hops; i++)
            {
                Enemy m = s.SNearest(cx, cy, i > 0 ? 100f : 270f, skip);
                if (m == null) break;
                skip.Add(m);
                float mx = SurvivorSim.SX(m.Pos), my = SurvivorSim.SY(m.Pos);
                pts.Add(new Pt { X = mx, Y = my - 4f, M = m });
                cx = mx;
                cy = my;
            }
            return pts;
        }

        // items-c.js:325 jagged
        public static List<Pt> Jagged(SurvivorSim s, Pt p0, Pt p1, int seg = 6, float amp = 7f)
        {
            var o = new List<Pt>(seg + 1) { p0 };
            for (int i = 1; i < seg; i++)
            {
                float u = i / (float)seg, nx = -(p1.Y - p0.Y), ny = p1.X - p0.X, L = SurvivorSim.Hypot(nx, ny), off = s.Rnd(-amp, amp);
                if (L == 0f) L = 1f;
                o.Add(new Pt { X = SurvivorSim.Lerp(p0.X, p1.X, u) + (nx / L * off), Y = SurvivorSim.Lerp(p0.Y, p1.Y, u) + (ny / L * off) });
            }
            o.Add(p1);
            return o;
        }

        // items-c.js:344
        public override void Setup(SurvivorSim s)
        {
            Bolts.Clear();
            Cd = 0.3f;
        }

        // items-c.js:345-360
        public override void Update(SurvivorSim s, float dt, int lv)
        {
            Cd -= dt;
            if (Cd <= 0f)
            {
                var skip = new HashSet<Enemy>();
                int chains = Chains[lv], hops = Hops[lv];
                float dmg = Dmg[lv];
                for (int c = 0; c < chains; c++)
                {
                    List<Pt> pts = ChainPick(s, s.PX + 6f, s.PY - 10f, hops, skip);
                    if (pts.Count < 2) continue;
                    var b = new Bolt { K = 0f, Lv = lv };
                    for (int i = 0; i + 1 < pts.Count; i++) b.Segs.Add(Jagged(s, pts[i], pts[i + 1], 6, 6f + lv));
                    Bolts.Add(b);
                    for (int i = 0; i + 1 < pts.Count; i++)
                    {
                        Pt p = pts[i + 1];
                        s.SHit(p.M, dmg, s.Rnd(-40f, 40f), s.Rnd(-40f, 40f));
                        s.HitFx(p.X, p.Y, lv, 0.6f);
                        if (lv == 6)
                        {
                            s.SplashFx(p.X, p.Y, 34f * s.SWide, 10);
                            s.SHitArea(p.X, p.Y, 34f, 4f, 160f);
                            s.Prism(p.X, p.Y, 0.35f, 34f * s.SWide);
                        }
                        else if (lv == 5 && i == pts.Count - 2)
                        {
                            s.SplashFx(p.X, p.Y, 30f * s.SWide, 10);
                            s.SHitArea(p.X, p.Y, 30f, 3f, 140f);
                        }
                    }
                }
                s.SShake = Math.Max(s.SShake, Shake[lv]);
                Cd = Cool[lv];
            }
            foreach (Bolt b in Bolts)
            {
                b.K += dt;
                if (SampleC.Chance(s) < 0.5f)
                {
                    var next = new List<List<Pt>>(b.Segs.Count);
                    foreach (List<Pt> sg in b.Segs) next.Add(Jagged(s, sg[0], sg[sg.Count - 1], 6, 6f + b.Lv));
                    b.Segs = next;
                }
            }
            Bolts.RemoveAll(b => b.K >= 0.45f);
        }
    }
}
