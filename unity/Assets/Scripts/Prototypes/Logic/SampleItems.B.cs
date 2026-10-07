using System;
using System.Collections.Generic;
using System.Globalization;

namespace FireGame.Prototypes.Logic
{
    /// <summary>
    /// 숲 개편(2026-10-08, 승인 샘플 그대로): tools/levelup-art/items-b.js의 물풍선(그림 A안)·소화기 부메랑·액체질소 지뢰를 줄 단위로 옮겼다.
    /// 샘플 화면 끝(W=480, H=270, 고정 카메라)은 소방관을 화면 가운데로 둔 끝(PX±240, PY±135)으로 바꿨다.
    /// 그림은 SurvivorView.SampleDraw.B.cs.
    /// </summary>
    public static partial class SampleItemsRegistry
    {
        static partial void MakeB(UpgradeId id, ref SampleItem made)
        {
            if (id == UpgradeId.Balloon) made = new BalloonItem();
            else if (id == UpgradeId.Extinguisher) made = new ExtinguisherItem();
            else if (id == UpgradeId.Mine) made = new MineItem();
        }
    }

    /// <summary>items-b.js 도우미(base.js clamp).</summary>
    internal static class SampleB
    {
        public static float Clamp(float v, float a, float b)
        {
            return v < a ? a : v > b ? b : v;
        }
    }

    // ============================================================================ 3. 물풍선 → 물풍선 폭우 (items-b.js:69-157)
    public sealed class BalloonItem : SampleItem
    {
        /// <summary>샘플 화면 반폭·반높이(px): 벽 튕김은 소방관 둘레 이 상자.</summary>
        public const float HalfW = 240f, HalfH = 135f;

        // items-b.js:91
        public static readonly int[] BalN = { 0, 1, 2, 2, 3, 3, 3 };
        public static readonly int[] BalBounce = { 0, 4, 5, 6, 7, 8, 8 };
        public static readonly float[] BalR = { 0f, 10f, 11f, 12.5f, 13.5f, 15f, 15f };
        public static readonly float[] BalDmg = { 0f, 2f, 2.4f, 3f, 3.6f, 4.5f, 4.5f };
        public static readonly float[] BalRR = { 0f, 24f, 28f, 33f, 38f, 46f, 46f };
        public static readonly float[] BalCd = { 0f, 1.5f, 1.4f, 1.25f, 1.1f, 1f, 1f };
        public static readonly float[] BalSpeed = { 0f, 230f, 245f, 260f, 275f, 290f, 290f };

        public sealed class Bal
        {
            public float X, Y, Vx, Vy, R, Sq, Slosh;
            public int B, Max, Lv;
            public bool Split;
            public readonly List<float> TrailX = new List<float>(8);
            public readonly List<float> TrailY = new List<float>(8);
        }

        public sealed class Shred
        {
            public float X, Y, Z, Vx, Vy, Vz, Rot, Vr, K, Sz;
            public bool Gold;
        }

        public sealed class RainDrop
        {
            public float X, Y, K, Sl;
            public bool Done;
        }

        /// <summary>샘플 s.st.b·s.st.cd·s.st.rain·s.st.rc·s.st.shreds.</summary>
        public readonly List<Bal> B = new List<Bal>();
        public float CdT;
        public readonly List<RainDrop> Rain = new List<RainDrop>();
        public float Rc;
        public readonly List<Shred> Shreds = new List<Shred>();

        /// <summary>Lv6 갈라진 작은 풍선을 남길까(샘플은 filter에 묻혀 사라진다 → 기본 false).</summary>
        public static bool KeepSplits;

        /// <summary>시험용: 지금까지 떨어진 폭우 풍선 수.</summary>
        public int RainBursts;

        // items-b.js:130
        public override void Setup(SurvivorSim s)
        {
            B.Clear();
            CdT = 0.3f;
            Rain.Clear();
            Rc = 0f;
        }

        // items-b.js:131
        public override void OnLevel(SurvivorSim s, int lv)
        {
            CdT = 0f;
        }

        // items-b.js:70-76
        private void RubberBurst(SurvivorSim s, float x, float y, float r, int lv)
        {
            int n = 7 + lv;
            for (int i = 0; i < n; i++)
            {
                float a = s.Rnd(0f, SurvivorSim.Tau), v = s.Rnd(90f, 200f);
                Shreds.Add(new Shred { X = x, Y = y, Z = 6f, Vx = (float)Math.Cos(a) * v, Vy = (float)Math.Sin(a) * v * 0.7f, Vz = s.Rnd(80f, 200f), Rot = s.Rnd(0f, SurvivorSim.Tau), Vr = s.Rnd(-14f, 14f), K = 0f, Sz = r * s.Rnd(0.35f, 0.6f), Gold = lv >= 5 });
            }
            s.SplashFx(x, y, r * 3.4f, 14 + (lv * 2));
            s.Glow(x, y, 0.3f, r * 4f, SurvivorSim.TierOf(lv).Glow);
        }

        // items-b.js:77-80
        private void StepShreds(float dt)
        {
            foreach (Shred p in Shreds)
            {
                p.K += dt;
                p.X += p.Vx * dt;
                p.Y += p.Vy * dt;
                p.Z += p.Vz * dt;
                p.Vz -= 600f * dt;
                if (p.Z < 0f)
                {
                    p.Z = 0f;
                    p.Vz *= -0.3f;
                    p.Vx *= 0.5f;
                    p.Vy *= 0.5f;
                }
                p.Rot += p.Vr * dt;
            }
            Shreds.RemoveAll(p => p.K >= 0.9f);
        }

        // items-b.js:92-95
        private static Bal MkBal(SurvivorSim s, int lv, float a, bool small)
        {
            float v = BalSpeed[lv];
            return new Bal { X = s.PX, Y = s.PY - 10f, Vx = (float)Math.Cos(a) * v, Vy = (float)Math.Sin(a) * v, B = 0, Max = small ? 2 : BalBounce[lv], R = small ? BalR[lv] * 0.7f : BalR[lv], Sq = 0f, Slosh = s.Rnd(0f, 6f), Lv = lv, Split = lv >= 6 && !small };
        }

        // items-b.js:96-115 (샘플 HOUSE 벽은 숲에 없어 뺐다)
        private bool BalStep(SurvivorSim s, Bal b, float dt, List<Bal> born)
        {
            b.X += b.Vx * dt;
            b.Y += b.Vy * dt;
            b.Sq = Math.Max(0f, b.Sq - (dt * 4f));
            b.Slosh += dt * (6f + (b.Sq * 20f));
            b.TrailX.Insert(0, b.X);
            b.TrailY.Insert(0, b.Y);
            if (b.TrailX.Count > 7)
            {
                b.TrailX.RemoveAt(b.TrailX.Count - 1);
                b.TrailY.RemoveAt(b.TrailY.Count - 1);
            }
            bool hit = false;
            float l = s.PX - HalfW, r = s.PX + HalfW, t = s.PY - HalfH, bo = s.PY + HalfH;
            if (b.X < l + 12f || b.X > r - 12f)
            {
                b.Vx *= -1f;
                b.X = SampleB.Clamp(b.X, l + 12f, r - 12f);
                hit = true;
            }
            if (b.Y < t + 14f || b.Y > bo - 12f)
            {
                b.Vy *= -1f;
                b.Y = SampleB.Clamp(b.Y, t + 14f, bo - 12f);
                hit = true;
            }
            foreach (Enemy m in s.SAlive())
            {
                float mx = SurvivorSim.SX(m.Pos), my = SurvivorSim.SY(m.Pos);
                if (SurvivorSim.Hypot(mx - b.X, my - b.Y) < SurvivorSim.SR(m) + b.R && !(SurvivorSim.Cd(m, "bl") > 0f))
                {
                    SurvivorSim.SetCd(m, "bl", 0.2f);
                    hit = true;
                    float a = (float)Math.Atan2(my - b.Y, mx - b.X), v = SurvivorSim.Hypot(b.Vx, b.Vy);
                    b.Vx = -(float)Math.Cos(a + s.Rnd(-0.4f, 0.4f)) * v;
                    b.Vy = -(float)Math.Sin(a + s.Rnd(-0.4f, 0.4f)) * v;
                    break;
                }
            }
            if (hit)
            {
                b.B++;
                b.Sq = 1f;
                int lv = b.Lv;
                float R = BalRR[lv] * (b.Max == 2 ? 0.75f : 1f);
                s.SHitArea(b.X, b.Y, R, BalDmg[lv], 100f + (lv * 20f));
                s.SplashFx(b.X, b.Y, R, 6 + (lv * 2));
                s.HitFx(b.X, b.Y, lv, 0.7f);
                if (lv >= 5)
                {
                    s.Ring(b.X, b.Y, 0.35f, R * 1.8f, lv >= 6 ? new Rgb(255, 255, 255) : new Rgb(255, 215, 110), 4f);
                    s.SShake = Math.Max(s.SShake, 2.5f);
                }
                // 최고급: 튕길 때마다 작은 풍선 둘로 갈라진다
                if (b.Split && B.Count + born.Count < 26)
                {
                    foreach (float o in new[] { -0.6f, 0.6f })
                    {
                        Bal nb = MkBal(s, lv, (float)Math.Atan2(b.Vy, b.Vx) + o, true);
                        nb.X = b.X;
                        nb.Y = b.Y;
                        born.Add(nb);
                    }
                }
            }
            if (b.B >= b.Max)
            {
                RubberBurst(s, b.X, b.Y, b.R, b.Lv);
                s.SHitArea(b.X, b.Y, BalRR[b.Lv] * 1.3f, BalDmg[b.Lv], 160f);
                return false;
            }
            return true;
        }

        private readonly List<Bal> _born = new List<Bal>();
        private readonly List<Bal> _keep = new List<Bal>();

        // items-b.js:132-147
        public override void Update(SurvivorSim s, float dt, int lv)
        {
            CdT -= dt;
            if (CdT <= 0f)
            {
                float a = s.SCrowd(out float cx, out float cy) ? (float)Math.Atan2(cy - s.PY, cx - s.PX) : -1.2f;
                int n = BalN[lv];
                for (int i = 0; i < n; i++) B.Add(MkBal(s, lv, a + ((i - ((n - 1) / 2f)) * 0.55f), false));
                CdT = BalCd[lv];
            }
            // 샘플 s.st.b = s.st.b.filter(balStep): balStep이 갈라진 풍선을 옛 배열에 push하지만 filter 결과(새 배열)로 갈아 끼워
            // 그 풍선은 곧바로 사라진다 — 승인 샘플 화면에는 갈라진 작은 풍선이 안 보인다. 화면을 샘플과 같게 하려고 그대로 버린다(KeepSplits로 켤 수 있다).
            _born.Clear();
            _keep.Clear();
            foreach (Bal b in B)
            {
                if (BalStep(s, b, dt, _born)) _keep.Add(b);
            }
            B.Clear();
            B.AddRange(_keep);
            if (KeepSplits) B.AddRange(_born);
            StepShreds(dt);
            if (lv < 6) return;
            // 폭우: 몹 위에 그림자가 먼저 깔리고 풍선이 떨어져 터진다.
            Rc -= dt;
            while (Rc <= 0f)
            {
                List<Enemy> al = s.SAlive();
                if (al.Count > 0)
                {
                    int k = Math.Min(al.Count - 1, (int)Math.Floor(s.Rnd(0f, 1f) * al.Count));
                    Enemy m = al[k];
                    Rain.Add(new RainDrop { X = SurvivorSim.SX(m.Pos) + s.Rnd(-12f, 12f), Y = SurvivorSim.SY(m.Pos) + s.Rnd(-8f, 8f), K = 0f, Sl = s.Rnd(0f, 6f) });
                }
                Rc += 0.06f;
            }
            foreach (RainDrop r in Rain)
            {
                r.K += dt;
                if (r.K >= 0.45f && !r.Done)
                {
                    r.Done = true;
                    RainBursts++;
                    RubberBurst(s, r.X, r.Y, 11f, 6);
                    s.SHitArea(r.X, r.Y, 40f, 6f, 160f);
                    s.HitFx(r.X, r.Y, 6, 0.5f);
                    s.SShake = Math.Max(s.SShake, 3f);
                }
            }
            Rain.RemoveAll(r => r.Done);
        }
    }

    // ============================================================================ 4. 소화기 부메랑 → 분말 회오리 (items-b.js:204-284)
    public sealed class ExtinguisherItem : SampleItem
    {
        // items-b.js:219
        public static readonly int[] ExtN = { 0, 1, 1, 2, 2, 3, 4 };
        public static readonly float[] ExtR = { 0f, 105f, 125f, 140f, 160f, 185f, 185f };
        public static readonly float[] ExtDmg = { 0f, 2.2f, 2.6f, 3f, 3.4f, 3.8f, 4.2f };
        public static readonly float[] ExtHitR = { 0f, 13f, 15f, 17f, 19f, 22f, 22f };
        public static readonly float[] ExtCd = { 0f, 1.3f, 1.2f, 1.15f, 1.05f, 1f, 1f };
        public static readonly float[] ExtSpd = { 0f, 1.15f, 1.1f, 1.05f, 1f, 0.95f, 0.95f };

        public sealed class Ext
        {
            public float A, K, X, Y, Spin;
            public int Lv;
            public string Key;
            public readonly List<float> TrailX = new List<float>(20);
            public readonly List<float> TrailY = new List<float>(20);
        }

        public sealed class Twister
        {
            public float X, Y, Vx, Vy;
        }

        /// <summary>샘플 s.st.b·s.st.cd·s.st.tw(Lv6 회오리, 없으면 null).</summary>
        public readonly List<Ext> B = new List<Ext>();
        public float CdT;
        public Twister Tw;

        // items-b.js:239
        public override void Setup(SurvivorSim s)
        {
            B.Clear();
            CdT = 0.2f;
        }

        // items-b.js:240 (샘플 {x:240,y:100}은 소방관(200,165) 기준 (+40, −65))
        public override void OnLevel(SurvivorSim s, int lv)
        {
            if (lv == 6) Tw = new Twister { X = s.PX + 40f, Y = s.PY - 65f };
            CdT = 0f;
        }

        // items-b.js:220
        private static Ext MkExt(SurvivorSim s, float a, int lv)
        {
            return new Ext { A = a, K = 0f, Lv = lv, X = s.PX, Y = s.PY, Spin = 0f, Key = "bm" + a.ToString("R", CultureInfo.InvariantCulture) };
        }

        // items-b.js:221-234
        private static bool ExtStep(SurvivorSim s, Ext b, float dt)
        {
            int lv = b.Lv;
            float R = ExtR[lv];
            b.K += dt / ExtSpd[lv];
            b.Spin += dt * (16f + (lv * 3f));
            float o = (float)Math.Sin(Math.PI * SampleB.Clamp(b.K, 0f, 1f)) * R, side = (float)Math.Sin(SurvivorSim.Tau * b.K) * R * 0.35f;
            b.X = s.PX + ((float)Math.Cos(b.A) * o) - ((float)Math.Sin(b.A) * side);
            b.Y = s.PY - 6f + ((float)Math.Sin(b.A) * o) + ((float)Math.Cos(b.A) * side);
            b.TrailX.Insert(0, b.X);
            b.TrailY.Insert(0, b.Y);
            if (b.TrailX.Count > 6 + (lv * 2))
            {
                b.TrailX.RemoveAt(b.TrailX.Count - 1);
                b.TrailY.RemoveAt(b.TrailY.Count - 1);
            }
            int l5 = Math.Min(lv, 5);
            if (s.Rnd(0f, 1f) < dt * (22f + (l5 * 4f)))
            {
                SPart p = s.Part(SPartKind.Powder, b.X + s.Rnd(-4f, 4f), b.Y + s.Rnd(-4f, 4f));
                p.Vx = s.Rnd(-15f, 15f);
                p.Vy = s.Rnd(-15f, 15f);
                p.Life = 0.45f + (l5 * 0.05f);
                p.Size = s.Rnd(3f, 3.5f + (l5 * 0.6f));
            }
            foreach (Enemy m in s.SAlive().ToArray())
            {
                float mx = SurvivorSim.SX(m.Pos), my = SurvivorSim.SY(m.Pos);
                if (SurvivorSim.Hypot(mx - b.X, my - b.Y) < SurvivorSim.SR(m) + ExtHitR[lv] && !(SurvivorSim.Cd(m, b.Key) > 0f))
                {
                    SurvivorSim.SetCd(m, b.Key, 0.25f);
                    float L = SurvivorSim.Hypot(mx - b.X, my - b.Y);
                    if (L == 0f) L = 1f;
                    s.SHit(m, ExtDmg[lv], (mx - b.X) / L * (60f + (lv * 25f)), (my - b.Y) / L * (60f + (lv * 25f)));
                    for (int i = 0; i < 2 + (lv >> 1); i++)
                    {
                        SPart p = s.Part(SPartKind.Powder, mx, my);
                        p.Vx = s.Rnd(-60f, 60f);
                        p.Vy = s.Rnd(-60f, 60f);
                        p.Life = 0.4f;
                        p.Size = 3.5f + (l5 * 0.4f);
                    }
                    s.HitFx(mx, my - 4f, lv, 0.5f);
                }
            }
            return b.K < 1f;
        }

        // items-b.js:241-259
        public override void Update(SurvivorSim s, float dt, int lv)
        {
            CdT -= dt;
            if (CdT <= 0f)
            {
                float a = s.SCrowd(out float ccx, out float ccy) ? (float)Math.Atan2(ccy - s.PY, ccx - s.PX) : -1.4f;
                int n = ExtN[lv];
                for (int i = 0; i < n; i++) B.Add(MkExt(s, a + (n == 1 ? 0f : (i - ((n - 1) / 2f)) * (SurvivorSim.Tau / n) * 0.55f), lv));
                CdT = ExtCd[lv];
            }
            B.RemoveAll(b => !ExtStep(s, b, dt));
            if (lv < 6 || Tw == null) return;
            // 회오리: 몹 떼를 쫓아 떠돌며 빨아들이고 하늘로 띄운다.
            // 몹이 없을 때 샘플 {x:260,y:120} → 소방관(200,165) 기준 (+60, −45).
            Twister T = Tw;
            if (!s.SCrowd(out float cx, out float cy))
            {
                cx = s.PX + 60f;
                cy = s.PY - 45f;
            }
            T.Vx += (((cx - T.X) * 1.2f) - T.Vx) * dt;
            T.Vy += (((cy - T.Y) * 1.2f) - T.Vy) * dt;
            // 샘플 clamp(130, W−70)·clamp(90, H−30): 화면 끝 여백. 숲은 소방관이 가운데라 왼쪽 여백(샘플 HUD 자리 130)을 오른쪽과 같은 70으로.
            T.X = SampleB.Clamp(T.X + (T.Vx * dt), s.PX - BalloonItem.HalfW + 70f, s.PX + BalloonItem.HalfW - 70f);
            T.Y = SampleB.Clamp(T.Y + (T.Vy * dt), s.PY - BalloonItem.HalfH + 90f, s.PY + BalloonItem.HalfH - 30f);
            foreach (Enemy m in s.SAlive().ToArray())
            {
                float mx = SurvivorSim.SX(m.Pos), my = SurvivorSim.SY(m.Pos);
                float dx = T.X - mx, dy = T.Y - my, L = SurvivorSim.Hypot(dx, dy);
                if (L == 0f) L = 1f;
                if (L < 200f)
                {
                    float k = 1f - (L / 200f);
                    mx += ((dx / L * 200f) - (dy / L * 200f)) * k * dt;
                    my += ((dy / L * 200f) + (dx / L * 200f)) * k * dt;
                    SurvivorSim.SetS(m, mx, my);
                    if (L < 44f)
                    {
                        float vz = s.Rnd(320f, 420f), vx = s.Rnd(-120f, 120f), vy = s.Rnd(-60f, 60f);
                        s.SLaunch(m, vz, vx, vy);
                        s.Ring(mx, my, 0.3f, 24f, new Rgb(255, 255, 255), 3f);
                    }
                }
            }
            for (int i = 0; i < 2; i++)
            {
                float a = s.Rnd(0f, SurvivorSim.Tau), r = s.Rnd(10f, 60f);
                SPart p = s.Part(SPartKind.Powder, T.X + ((float)Math.Cos(a) * r), T.Y + ((float)Math.Sin(a) * r * 0.4f));
                p.Vz = s.Rnd(40f, 120f);
                p.Life = 0.8f;
                p.Size = s.Rnd(3f, 7f);
            }
            s.SShake = Math.Max(s.SShake, 1.5f);
        }
    }

    // ============================================================================ 5. 액체질소 지뢰 → 빙결 지대 (items-b.js:286-350)
    public sealed class MineItem : SampleItem
    {
        // items-b.js:287
        public static readonly int[] MineMax = { 0, 2, 3, 4, 5, 6, 9 };
        public static readonly float[] MineR = { 0f, 22f, 30f, 38f, 46f, 56f, 62f };
        public static readonly float[] MineDrop = { 0f, 0.8f, 0.6f, 0.48f, 0.4f, 0.34f, 0.28f };

        public sealed class Mine
        {
            public float X, Y, Arm, Cool;
            public int Lv;
            public bool Used;
        }

        /// <summary>샘플 s.st.mines·s.st.drop.</summary>
        public readonly List<Mine> Mines = new List<Mine>();
        public float DropT;

        /// <summary>시험용: 지금까지 터진 횟수.</summary>
        public int Triggers;

        // items-b.js:305 (샘플은 소방관을 8자 길로 걷게 했다 — 숲은 사람이 걷는다)
        public override void Setup(SurvivorSim s)
        {
            Mines.Clear();
            DropT = 0f;
        }

        // items-b.js:306-330
        public override void Update(SurvivorSim s, float dt, int lv)
        {
            float px = s.PX, py = s.PY;
            DropT -= dt;
            if (DropT <= 0f)
            {
                Mines.Add(new Mine { X = px, Y = py + 6f, Arm = 0.25f, Lv = lv });
                while (Mines.Count > MineMax[lv]) Mines.RemoveAt(0);
                DropT = MineDrop[lv];
            }
            float R = MineR[lv];
            foreach (Mine mi in Mines)
            {
                mi.Arm -= dt;
                mi.Cool -= dt;
                if (mi.Arm <= 0f && !mi.Used && !(mi.Cool > 0f))
                {
                    foreach (Enemy m in s.SAlive().ToArray())
                    {
                        if (!(SurvivorSim.Hypot(SurvivorSim.SX(m.Pos) - mi.X, SurvivorSim.SY(m.Pos) - mi.Y) < SurvivorSim.SR(m) + 10f + lv && !(m.SFrozen > 0f))) continue;
                        if (lv >= 6) mi.Cool = 0.35f;
                        else mi.Used = true;
                        Triggers++;
                        s.Ring(mi.X, mi.Y, 0.45f, R * 1.4f, lv >= 5 ? new Rgb(255, 240, 190) : new Rgb(150, 230, 255), 4f + lv);
                        s.Glow(mi.X, mi.Y, 0.4f, R * 1.5f, new Rgb(150, 230, 255));
                        if (lv >= 3) s.Ring(mi.X, mi.Y, 0.6f, R * 2f, new Rgb(220, 250, 255), 2f);
                        for (int i = 0; i < 10 + (lv * 4); i++)
                        {
                            float a2 = s.Rnd(0f, SurvivorSim.Tau);
                            float fx = mi.X + ((float)Math.Cos(a2) * s.Rnd(0f, R)), fy = mi.Y + ((float)Math.Sin(a2) * s.Rnd(0f, R) * 0.6f);
                            SPart p = s.Part(SPartKind.Frost, fx, fy);
                            p.Life = 1.2f + (lv * 0.1f);
                            p.Size = s.Rnd(4f, 7f + lv);
                        }
                        for (int i = 0; i < 4 + (lv * 2); i++)
                        {
                            float a2 = s.Rnd(0f, SurvivorSim.Tau), v = s.Rnd(60f, 120f + (lv * 20f));
                            SPart p = s.Part(SPartKind.Shard, mi.X, mi.Y);
                            p.Vx = (float)Math.Cos(a2) * v;
                            p.Vy = (float)Math.Sin(a2) * v * 0.7f;
                            p.Vz = s.Rnd(60f, 200f);
                            p.Grav = 700f;
                            p.Life = 0.7f;
                            p.Size = s.Rnd(3f, 5f + (lv * 0.5f));
                            p.Rot = s.Rnd(0f, SurvivorSim.Tau);
                            p.Vr = s.Rnd(-12f, 12f);
                        }
                        // 샘플 o.frozen = …: 덮어쓴다(Max가 아니다).
                        foreach (Enemy o in s.SAlive().ToArray())
                        {
                            if (SurvivorSim.Hypot(SurvivorSim.SX(o.Pos) - mi.X, SurvivorSim.SY(o.Pos) - mi.Y) < R + SurvivorSim.SR(o)) o.SFrozen = s.Rnd(0.7f, 1f) - (lv * 0.05f);
                        }
                        s.HitFx(mi.X, mi.Y, lv, 1f);
                        s.SShake = Math.Max(s.SShake, 2f + (lv * 0.7f));
                        break;
                    }
                }
            }
            Mines.RemoveAll(m => m.Used);
            // 최고급: 얼음이 깨질 때 곁의 요괴도 얼린다(연쇄).
            // 게임은 몹 이동(얼음 녹음)이 아이템보다 먼저라, 다음 틱에 깨질 몹(남은 0~dt)을 본다 — 샘플과 같은 순간.
            if (lv >= 6)
            {
                foreach (Enemy m in s.Enemies.ToArray())
                {
                    if (m.Dead || !(m.SFrozen > 0f) || !(m.SFrozen <= dt * 1.01f)) continue;
                    float mx = SurvivorSim.SX(m.Pos), my = SurvivorSim.SY(m.Pos);
                    foreach (Enemy o in s.SAlive().ToArray())
                    {
                        if (o != m && !(o.SFrozen > 0f) && SurvivorSim.Hypot(SurvivorSim.SX(o.Pos) - mx, SurvivorSim.SY(o.Pos) - my) < 38f)
                        {
                            o.SFrozen = 0.3f;
                            s.Glow(SurvivorSim.SX(o.Pos), SurvivorSim.SY(o.Pos), 0.25f, 22f, new Rgb(200, 245, 255));
                        }
                    }
                }
            }
            // 최고급: 지뢰끼리 얼음 길 — 건너는 몹은 그 자리에서 언다
            if (lv >= 6)
            {
                for (int i = 0; i + 1 < Mines.Count; i++)
                {
                    Mine A = Mines[i], B = Mines[i + 1];
                    if (A.Arm > 0f || B.Arm > 0f) continue;
                    foreach (Enemy m in s.SAlive().ToArray())
                    {
                        float mx = SurvivorSim.SX(m.Pos), my = SurvivorSim.SY(m.Pos);
                        if (!(m.SFrozen > 0f) && SurvivorSim.SegDist(mx, my, A.X, A.Y, B.X, B.Y) < SurvivorSim.SR(m) + 13f)
                        {
                            m.SFrozen = 0.6f;
                            s.Glow(mx, my, 0.3f, 26f, new Rgb(170, 240, 255));
                            s.Star(mx, my - 8f, 0f, 0f, 0.4f, 5f, Rgb.None);
                        }
                    }
                }
            }
        }
    }
}
