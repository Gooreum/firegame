using System;
using System.Collections.Generic;

namespace FireGame.Prototypes.Logic
{
    /// <summary>샘플 색(0~255). null 대신 Rainbow면 화면이 시간에 따라 무지개로 칠한다.</summary>
    public struct Rgb
    {
        public float R, G, B;
        public bool Rainbow;

        public Rgb(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
            Rainbow = false;
        }

        public static readonly Rgb None = new Rgb { Rainbow = true };
    }

    public enum SPartKind : byte
    {
        Spark, Smoke, Powder, Foam, Ring, Glow, Drop, Shard, Gem, Paw, Frost, Text, Star, Confetti, Prism, Mote,
    }

    /// <summary>샘플 base.js part(): 좌표는 샘플 px(월드 X/Px, −월드 Y/Px), z는 높이 px.</summary>
    public sealed class SPart
    {
        public SPartKind Kind;
        public float X, Y, Z, Vx, Vy, Vz, Life = 0.5f, Age, Size = 3f, Grav;
        public Rgb Color;
        public bool HasColor;
        public float Width;
        public float Rot, Vr;
        public string Str;
        public float Sx, Sy, Tx, Ty;
        public bool Seek;
    }

    /// <summary>샘플 core.js TIER 한 줄.</summary>
    public struct Tier
    {
        public float Scale;
        public Rgb Core, Glow;
        public float GlowA, White;
        public bool Gold, Rainbow, Trail;
        public float Shake, Hit;
    }

    /// <summary>레벨업 순간 연출 상태(샘플 s.fx).</summary>
    public sealed class SLevelFx
    {
        public int Lv;
        public float Age;
        public UpgradeId Item;
        public bool Fly;
        public float Burst = -1f;
        public bool BurstDone;
        public float BoomAt;
        public bool BoomDone;
    }

    /// <summary>
    /// 숲 개편(2026-10-08, 승인 샘플 그대로): 샘플 장면(core.js Scene·base.js 파티클)의 상태를 규칙 쪽에 둔다.
    /// 아이템(SampleItems)·레벨업이 샘플처럼 part()/hitFx()/splash()를 부르고, 화면(SurvivorView.Sample*)은 그 데이터를 샘플 그리기 그대로 그린다.
    /// 좌표는 샘플 px: sx = 월드 X / Px, sy = −월드 Y / Px(아래가 +). 샘플 몹 지름 20px = 0.75칸.
    /// </summary>
    public sealed partial class SurvivorSim
    {
        /// <summary>샘플 1px = 0.0375칸(샘플 몹 지름 20px ↔ Unity 몹 0.75칸). 숲 카메라도 이 비율(화면 높이 270px)로 맞춘다.</summary>
        public const float Px = 0.0375f;

        public static readonly Tier[] Tiers =
        {
            default,
            new Tier { Scale = 1.00f, Core = new Rgb(70, 150, 255), Glow = new Rgb(60, 140, 255), GlowA = 0.00f, White = 0.00f, Shake = 0f, Hit = 1.0f },
            new Tier { Scale = 1.18f, Core = new Rgb(90, 170, 255), Glow = new Rgb(80, 160, 255), GlowA = 0.18f, White = 0.15f, Shake = 0f, Hit = 1.25f },
            new Tier { Scale = 1.40f, Core = new Rgb(120, 200, 255), Glow = new Rgb(110, 200, 255), GlowA = 0.32f, White = 0.45f, Trail = true, Shake = 0.6f, Hit = 1.6f },
            new Tier { Scale = 1.60f, Core = new Rgb(150, 220, 255), Glow = new Rgb(130, 215, 255), GlowA = 0.45f, White = 0.65f, Trail = true, Shake = 1.2f, Hit = 2.0f },
            new Tier { Scale = 1.80f, Core = new Rgb(190, 235, 255), Glow = new Rgb(160, 225, 255), GlowA = 0.60f, White = 0.85f, Gold = true, Trail = true, Shake = 2.0f, Hit = 2.5f },
            new Tier { Scale = 2.30f, Core = new Rgb(230, 248, 255), Glow = new Rgb(255, 220, 140), GlowA = 0.80f, White = 1.00f, Gold = true, Rainbow = true, Trail = true, Shake = 3.5f, Hit = 3.5f },
        };

        public static Tier TierOf(int lv)
        {
            return Tiers[Math.Max(1, Math.Min(6, lv))];
        }

        // --- 샘플 장면 상태 ---
        public readonly List<SPart> Parts = new List<SPart>(1024);

        /// <summary>화면 흔들림(px, 샘플 s.shake). 화면이 실제 시간으로 줄인다.</summary>
        public float SShake;

        /// <summary>멈춤·슬로모션 남은 시간(샘플 s.stop·s.slow). 화면이 시뮬 틱을 멈추거나 1/4로 돌리며 줄인다.</summary>
        public float SStop, SSlow;

        public float SFlash;
        public Rgb SFlashColor = new Rgb(255, 255, 255);

        /// <summary>Lv6 직전 어두움(샘플 s.dark, 0~0.72).</summary>
        public float SDark;

        public SLevelFx LevelFx;

        /// <summary>샘플 시각(s.t): 숲 그림의 무지개·회전이 이 시계를 쓴다.</summary>
        public float ST;

        /// <summary>
        /// 숲 화면 반폭·반높이(샘플 px, 소방관이 가운데). 숲 카메라가 세로 380px를 보고 폰 화면비(19.5:9)라 반높이 190 · 반폭 190×19.5/9.
        /// 물풍선 벽 튕김·회오리·거품 파도·맨홀 찾기 같은 "화면 끝"이 이 상자다(예전 샘플 480×270 고정 화면 대신).
        /// </summary>
        public const float SViewHalfH = 190f, SViewHalfW = 190f * 19.5f / 9f;

        /// <summary>
        /// 숲 레벨 피해 배율(2026-10-08): 아이템 Update가 도는 동안 그 아이템 레벨(SCurLv)로 SHit 피해를 곱한다.
        /// 샘플 표는 레벨마다 갈래·크기만 늘어 피해가 거의 같았다 — 레벨이 오르면 한 방이 확실히 세진다. 대원 물(SCurLv 0)은 1배.
        /// </summary>
        public static readonly float[] SLvMul = { 1f, 1f, 1.4f, 1.9f, 2.4f, 3f, 3.6f };

        /// <summary>지금 Update 중인 샘플 아이템 레벨(1~6). 아이템 밖이면 0.</summary>
        public int SCurLv;

        /// <summary>광각 노즐: 숲 무기 범위·반경 배율. 규칙과 그림이 같은 값을 곱한다.</summary>
        public float SWide
        {
            get { return Build.WideScale; }
        }

        /// <summary>샘플 s.player.moving: 이번 틱 소방관이 움직였나.</summary>
        public bool PlayerMoving;

        private Vec2 _sLastPlayer;

        /// <summary>샘플 m.cd[key]: 아이템별 몹 쿨다운(남은 초). 아직 없으면 0.</summary>
        public static float Cd(Enemy e, string key)
        {
            return e.SCd != null && e.SCd.TryGetValue(key, out float v) ? v : 0f;
        }

        public static void SetCd(Enemy e, string key, float v)
        {
            if (e.SCd == null) e.SCd = new Dictionary<string, float>();
            e.SCd[key] = v;
        }

        /// <summary>몹 쿨다운을 dt만큼 줄인다(숲 틱마다).</summary>
        private void TickCds()
        {
            foreach (Enemy e in Enemies)
            {
                if (e.SCd == null || e.SCd.Count == 0) continue;
                var keys = new List<string>(e.SCd.Keys);
                foreach (string k in keys) e.SCd[k] -= Dt;
            }
        }

        /// <summary>몹 위치를 샘플 px로 옮긴다(잡힌 몹을 끌고 다닐 때).</summary>
        public static void SetS(Enemy e, float x, float y)
        {
            e.Pos = FromS(x, y);
        }

        /// <summary>샘플 px 자리.</summary>
        public static float SX(Vec2 w)
        {
            return w.X / Px;
        }

        public static float SY(Vec2 w)
        {
            return -w.Y / Px;
        }

        public static Vec2 FromS(float x, float y)
        {
            return new Vec2(x * Px, -y * Px);
        }

        public float PX
        {
            get { return Player.X / Px; }
        }

        public float PY
        {
            get { return -Player.Y / Px; }
        }

        // --- 샘플 유틸(base.js) ---
        public float Rnd(float a, float b)
        {
            return a + (Rand() * (b - a));
        }

        public static float Lerp(float a, float b, float k)
        {
            return a + ((b - a) * k);
        }

        public static float Ease(float k)
        {
            return 1f - (float)Math.Pow(1f - Clamp(k, 0f, 1f), 3);
        }

        public static float Hypot(float x, float y)
        {
            return (float)Math.Sqrt((x * x) + (y * y));
        }

        public static float SegDist(float px, float py, float x0, float y0, float x1, float y1)
        {
            float vx = x1 - x0, vy = y1 - y0, l2 = (vx * vx) + (vy * vy);
            if (l2 == 0f) l2 = 1f;
            float u = Clamp((((px - x0) * vx) + ((py - y0) * vy)) / l2, 0f, 1f);
            return Hypot(px - (x0 + (vx * u)), py - (y0 + (vy * u)));
        }

        public const float Tau = (float)(Math.PI * 2);

        // --- 파티클(base.js part/poof/splash/shatter/textPop) ---
        public SPart Part(SPartKind kind, float x, float y)
        {
            var p = new SPart { Kind = kind, X = x, Y = y };
            if (Parts.Count < 1600) Parts.Add(p);
            return p;
        }

        public void Poof(float x, float y, float k = 1f)
        {
            for (int i = 0; i < 12 * k; i++)
            {
                float a = Rnd(0f, Tau), v = Rnd(60f, 170f) * k;
                SPart p = Part(SPartKind.Spark, x, y);
                p.Vx = (float)Math.Cos(a) * v;
                p.Vy = (float)Math.Sin(a) * v;
                p.Life = Rnd(0.25f, 0.5f);
                p.Size = Rnd(2f, 4f) * k;
                p.Color = Rand() < 0.5f ? new Rgb(255, 180, 60) : new Rgb(200, 120, 255);
                p.HasColor = true;
            }
            for (int i = 0; i < 5 * k; i++)
            {
                SPart p = Part(SPartKind.Smoke, x + Rnd(-6f, 6f), y + Rnd(-6f, 6f));
                p.Vy = -20f;
                p.Life = Rnd(0.4f, 0.7f);
                p.Size = Rnd(8f, 14f) * k;
                p.Color = new Rgb(190, 170, 210);
                p.HasColor = true;
            }
            Ring(x, y, 0.3f, 26f * k, new Rgb(255, 255, 255), 3f);
        }

        public SPart Ring(float x, float y, float life, float size, Rgb color, float width = 3f)
        {
            SPart p = Part(SPartKind.Ring, x, y);
            p.Life = life;
            p.Size = size;
            p.Color = color;
            p.HasColor = !color.Rainbow;
            p.Width = width;
            return p;
        }

        public SPart Glow(float x, float y, float life, float size, Rgb color)
        {
            SPart p = Part(SPartKind.Glow, x, y);
            p.Life = life;
            p.Size = size;
            p.Color = color;
            p.HasColor = true;
            return p;
        }

        public SPart Drop(float x, float y, float vx, float vy, float vz, float grav, float life, float size)
        {
            SPart p = Part(SPartKind.Drop, x, y);
            p.Vx = vx;
            p.Vy = vy;
            p.Vz = vz;
            p.Grav = grav;
            p.Life = life;
            p.Size = size;
            return p;
        }

        public void SplashFx(float x, float y, float r = 30f, int n = 16)
        {
            Ring(x, y, 0.38f, r * 1.4f, new Rgb(130, 210, 255), 5f);
            Ring(x, y, 0.5f, r * 2f, new Rgb(220, 245, 255), 2f);
            for (int i = 0; i < n; i++)
            {
                float a = Rnd(0f, Tau), v = Rnd(50f, 160f) * r / 30f;
                Drop(x, y, (float)Math.Cos(a) * v, (float)Math.Sin(a) * v * 0.6f, Rnd(80f, 220f), 600f, 1f, Rnd(2.2f, 3.8f));
            }
            Glow(x, y, 0.25f, r * 1.2f, new Rgb(160, 225, 255));
        }

        public void ShatterFx(float x, float y)
        {
            for (int i = 0; i < 12; i++)
            {
                float a = Rnd(0f, Tau), v = Rnd(80f, 200f);
                SPart p = Part(SPartKind.Shard, x, y);
                p.Vx = (float)Math.Cos(a) * v;
                p.Vy = (float)Math.Sin(a) * v * 0.7f;
                p.Vz = Rnd(60f, 200f);
                p.Grav = 700f;
                p.Life = 0.8f;
                p.Size = Rnd(3f, 7f);
                p.Rot = Rnd(0f, Tau);
                p.Vr = Rnd(-12f, 12f);
            }
            Ring(x, y, 0.3f, 34f, new Rgb(200, 245, 255), 4f);
            SShake = Math.Max(SShake, 3f);
        }

        public void TextPop(float x, float y, string str, Rgb color, float size = 15f)
        {
            SPart p = Part(SPartKind.Text, x, y);
            p.Vy = -40f;
            p.Life = 0.8f;
            p.Str = str;
            p.Color = color;
            p.HasColor = true;
            p.Size = size;
        }

        public SPart Star(float x, float y, float vx, float vy, float life, float size, Rgb color)
        {
            SPart p = Part(SPartKind.Star, x, y);
            p.Vx = vx;
            p.Vy = vy;
            p.Life = life;
            p.Size = size;
            p.Color = color;
            p.HasColor = !color.Rainbow;
            return p;
        }

        public SPart Prism(float x, float y, float life, float size)
        {
            SPart p = Part(SPartKind.Prism, x, y);
            p.Life = life;
            p.Size = size;
            return p;
        }

        // --- core.js hitFx: 맞힌 자리(Lv1 물방울 → Lv3 물보라 고리 → Lv5 금 별·흔들림 → Lv6 무지개·멈춤) ---
        public void HitFx(float x, float y, int lv, float k = 1f)
        {
            Tier t = TierOf(lv);
            int n = (int)Math.Round((3 + (lv * 2)) * k);
            for (int i = 0; i < n; i++) Drop(x, y, Rnd(-70f, 70f) * t.Hit * 0.6f, Rnd(-50f, 30f) * t.Hit * 0.6f, Rnd(50f, 130f), 520f, 0.6f, Rnd(2f, 3f));
            if (lv >= 2) Ring(x, y, 0.28f, 10f + (lv * 4f), t.Core, 1.5f + (lv * 0.5f));
            if (lv >= 3) Glow(x, y, 0.22f, 12f + (lv * 5f), t.Glow);
            if (lv >= 5)
            {
                for (int i = 0; i < 6; i++)
                {
                    float a = Rnd(0f, Tau), v = Rnd(80f, 170f);
                    Star(x, y, (float)Math.Cos(a) * v, (float)Math.Sin(a) * v, 0.35f, Rnd(3f, 5f), t.Rainbow ? Rgb.None : new Rgb(255, 215, 110));
                }
                SShake = Math.Max(SShake, t.Shake * k);
            }
            if (lv >= 6)
            {
                Prism(x, y, 0.35f, 30f * k);
                SStop = Math.Max(SStop, 0.018f * k);
            }
        }

        // --- 파티클 진행(base.js stepParts + core.js 덧붙임) ---
        private void StepParts(float dt)
        {
            for (int i = 0; i < Parts.Count; i++)
            {
                SPart p = Parts[i];
                if (p.Kind == SPartKind.Mote && p.Seek)
                {
                    float u = Ease(p.Age / p.Life);
                    p.X = Lerp(p.Sx, p.Tx, u);
                    p.Y = Lerp(p.Sy, p.Ty, u);
                    p.Vx = p.Vy = 0f;
                }
                if (p.Kind == SPartKind.Star || p.Kind == SPartKind.Confetti)
                {
                    p.Vx *= 0.94f;
                    p.Vy *= 0.94f;
                    if (p.Kind == SPartKind.Confetti) p.Vy += 60f * dt;
                }
                p.Age += dt;
                p.X += p.Vx * dt;
                p.Y += p.Vy * dt;
                if (p.Grav != 0f)
                {
                    p.Z += p.Vz * dt;
                    p.Vz -= p.Grav * dt;
                    if (p.Z < 0f)
                    {
                        p.Z = 0f;
                        p.Vz *= -0.3f;
                        p.Vx *= 0.5f;
                        p.Vy *= 0.5f;
                    }
                }
                else if (p.Vz != 0f) p.Z += p.Vz * dt;
                if (p.Kind == SPartKind.Spark)
                {
                    p.Vx *= 0.92f;
                    p.Vy *= 0.92f;
                }
                if (p.Kind == SPartKind.Shard || p.Kind == SPartKind.Confetti) p.Rot += p.Vr * dt;
            }
            Parts.RemoveAll(p => p.Age >= p.Life);
        }

        // --- 샘플 몹 도우미(core.js Scene.alive/nearest/crowd/hitArea, base.js Mob.hit/launch) ---
        private readonly List<Enemy> _sAlive = new List<Enemy>(256);

        /// <summary>샘플 alive(): 살아 있고, 땅에 있고, 잡혀 있지 않고, 화면 안(소방관 둘레 숲 화면 + 여백 60×25px — 샘플 480×270 화면의 ±300×±160과 같은 여백).</summary>
        public List<Enemy> SAlive()
        {
            _sAlive.Clear();
            float px = PX, py = PY;
            foreach (Enemy e in Enemies)
            {
                if (e.Dead || e.AirZ > 0f || e.Held) continue;
                float x = SX(e.Pos), y = SY(e.Pos);
                if (Math.Abs(x - px) > SViewHalfW + 60f || Math.Abs(y - py) > SViewHalfH + 25f) continue;
                _sAlive.Add(e);
            }
            return _sAlive;
        }

        /// <summary>가장 가까운 몹. 찾는 거리 max에 광각 노즐(SWide)이 곱해진다.</summary>
        public Enemy SNearest(float x, float y, float max = 200f, HashSet<Enemy> skip = null)
        {
            Enemy best = null;
            float bd = max * SWide;
            foreach (Enemy e in SAlive())
            {
                if (skip != null && skip.Contains(e)) continue;
                float d = Hypot(SX(e.Pos) - x, SY(e.Pos) - y);
                if (d < bd)
                {
                    best = e;
                    bd = d;
                }
            }
            return best;
        }

        /// <summary>소방관 260px 안 몹 무리의 가운데(없으면 false).</summary>
        public bool SCrowd(out float cx, out float cy)
        {
            cx = cy = 0f;
            int n = 0;
            float px = PX, py = PY;
            foreach (Enemy e in SAlive())
            {
                float x = SX(e.Pos), y = SY(e.Pos);
                if (Hypot(x - px, y - py) >= 260f) continue;
                cx += x;
                cy += y;
                n++;
            }
            if (n == 0) return false;
            cx /= n;
            cy /= n;
            return true;
        }

        /// <summary>샘플 몹 반지름(px).</summary>
        public static float SR(Enemy e)
        {
            return e.Radius / Px;
        }

        /// <summary>샘플 큰 몹(뿔 달린 요괴): 반지름 15px 쪽.</summary>
        public static bool SBig(Enemy e)
        {
            return e.Radius >= 0.5f;
        }

        /// <summary>둘레 맞힘: 반경 r에 광각 노즐(SWide)이 곱해진다(넘기는 쪽은 곱하지 않은 값을 준다).</summary>
        public int SHitArea(float x, float y, float r, float dmg, float push = 0f)
        {
            int n = 0;
            r *= SWide;
            foreach (Enemy e in SAlive().ToArray())
            {
                float mx = SX(e.Pos), my = SY(e.Pos);
                float d = Hypot(mx - x, my - y);
                if (d < r + SR(e))
                {
                    float l = d == 0f ? 1f : d;
                    SHit(e, dmg, (mx - x) / l * push, (my - y) / l * push);
                    n++;
                }
            }
            return n;
        }

        /// <summary>몹이 질겨지는 시간 배율(1 → 2:00에 2 → 3:00에 3.25). SDamageScale의 시간 몫.</summary>
        public float STimeScale
        {
            get { return 1f + ((Time / 120f) * (Time / 120f)); }
        }

        /// <summary>샘플 피해 → 게임 피해: 샘플 작은 몹 체력 4 = 지금 불씨 체력(시간이 갈수록 질겨지는 몫까지).</summary>
        public float SDamageScale
        {
            get { return 2f * Stage.EnemyHp * STimeScale / 4f; }
        }

        /// <summary>샘플 m.hit(s, dmg, kx, ky): kx·ky는 px/초 밀치기. 공중이거나 죽었으면 false. 피해에 레벨 배율(SLvMul)·고압 노즐이 곱해진다.</summary>
        public bool SHit(Enemy e, float dmg, float kx = 0f, float ky = 0f, HitSource source = HitSource.Sample)
        {
            if (e.Dead || e.AirZ > 0f) return false;
            // 샘플 밀치기(px/초, 초당 0.02배로 줄어듦)를 게임 Knock(칸/초, e^-8t)으로: 같은 거리를 밀린다.
            var knock = new Vec2(kx * Px * 2.05f, -ky * Px * 2.05f);
            Damage(e, dmg * SDamageScale * SLvMul[SCurLv] * Build.NozzleScale, knock, false, source, Player);
            return true;
        }

        /// <summary>샘플 m.launch: 하늘로 띄워 떨어지면 처치.</summary>
        public void SLaunch(Enemy e, float vz, float vx, float vy)
        {
            if (e.Dead) return;
            e.AirZ = 1f;
            e.AirVz = vz;
            e.AirVx = vx;
            e.AirVy = vy;
            e.Launched = true;
            e.HitFlash = 0.1f;
        }

        /// <summary>샘플 m.frozen: 다 녹으면 깨지며 처치(base.js Mob.update).</summary>
        public void SFreeze(Enemy e, float seconds)
        {
            if (e.Dead) return;
            e.SFrozen = Math.Max(e.SFrozen, seconds);
        }

        /// <summary>
        /// 숲 몹 진행(base.js Mob.update의 공중·얼음·잡힘): 처리했으면 true(이번 틱 원래 이동을 건너뛴다).
        /// </summary>
        private bool SampleMobStep(Enemy e)
        {
            if (e.AirZ > 0f || e.AirVz > 0f)
            {
                e.AirZ += e.AirVz * Dt;
                e.AirVz -= 700f * Dt;
                e.AirSpin += Dt * 12f;
                Vec2 d = new Vec2(e.AirVx * Px * Dt, -e.AirVy * Px * Dt);
                e.Pos = new Vec2(e.Pos.X + d.X, e.Pos.Y + d.Y);
                if (e.AirZ <= 0f)
                {
                    e.AirZ = 0f;
                    e.AirVz = 0f;
                    if (e.Launched)
                    {
                        e.Launched = false;
                        Damage(e, e.Hp + 1f, default, false, HitSource.Sample, e.Pos);
                    }
                }
                return true;
            }
            if (e.Held) return true;
            if (e.SFrozen > 0f)
            {
                e.SFrozen -= Dt;
                if (e.SFrozen <= 0f)
                {
                    ShatterFx(SX(e.Pos), SY(e.Pos));
                    Damage(e, e.Hp + 1f, default, false, HitSource.Sample, e.Pos);
                }
                return true;
            }
            return false;
        }

        /// <summary>숲 처치 그림: 샘플 Mob.kill → poof.</summary>
        private void SampleKilled(Enemy e)
        {
            Poof(SX(e.Pos), SY(e.Pos) - e.AirZ, SBig(e) ? 1.6f : 1f);
        }

        // --- 레벨업(core.js levelUp/doBurst) ---
        private static readonly float[] LvSlow = { 0f, 0.55f, 0.6f, 0.7f, 0.8f, 1.1f, 1.9f };
        private static readonly int[] LvStars = { 0, 18, 28, 40, 56, 90, 160 };
        private static readonly float[] LvShake = { 0f, 3f, 4f, 5f, 6f, 9f, 14f };
        private static readonly float[] LvFlash = { 0f, 0.25f, 0.3f, 0.35f, 0.4f, 0.45f, 0.45f };
        private static readonly float[] LvStop = { 0f, 0.04f, 0.05f, 0.06f, 0.07f, 0.1f, 0.16f };

        /// <summary>샘플 levelUp(s, lv): 숲에서 카드를 고른 순간.</summary>
        private void SampleLevelUp(UpgradeId id, int lv)
        {
            float px = PX, py = PY;
            LevelFx = new SLevelFx { Lv = lv, Item = id };
            JustLeveledItem = id;
            JustLeveledTo = lv;
            SampleItem item = SampleItemOf(id);
            if (item != null)
            {
                item.Surge = 1f;
                item.OnLevel(this, lv);
            }
            SSlow = Math.Max(SSlow, LvSlow[lv]);
            if (lv == 1) LevelFx.Fly = true;
            if (lv < 6) LevelFx.Burst = lv == 1 ? 0.35f : 0f;
            else
            {
                // Lv6: 0~0.55초 화면이 어두워지고 빛 알갱이가 빨려 들어온다 → 0.55초에 폭발.
                for (int i = 0; i < 90; i++)
                {
                    float a = Rnd(0f, Tau), r = Rnd(120f, 300f);
                    SPart p = Part(SPartKind.Mote, px, py);
                    p.Sx = px + ((float)Math.Cos(a) * r);
                    p.Sy = py + ((float)Math.Sin(a) * r * 0.7f);
                    p.Tx = px;
                    p.Ty = py - 8f;
                    p.Seek = true;
                    p.Life = Rnd(0.35f, 0.55f);
                    p.Size = Rnd(1.5f, 3.2f);
                }
                LevelFx.BoomAt = 0.55f;
            }
        }

        /// <summary>샘플 doBurst(s, lv): 고리·별·꽃가루·물방울, 둘레 몹 밀치기(Lv6은 처치), 흔들림·번쩍·멈춤.</summary>
        private void DoBurst(int lv)
        {
            float x = PX, y = PY - 6f;
            Tier t = TierOf(lv);
            bool gold = lv >= 5, rb = lv >= 6;
            Rgb col = rb ? Rgb.None : gold ? new Rgb(255, 215, 110) : t.Core;
            for (int i = 0; i < Math.Min(lv, 4); i++)
            {
                if (rb) Prism(x, y, 0.45f + (i * 0.12f), 60f + (i * 34f) + (lv * 10f));
                else Ring(x, y, 0.45f + (i * 0.12f), 60f + (i * 34f) + (lv * 10f), col, 6f - i);
            }
            Glow(x, y, lv == 6 ? 0.3f : 0.45f, lv == 6 ? 110f : 70f + (lv * 22f), rb ? new Rgb(255, 240, 200) : gold ? new Rgb(255, 220, 140) : t.Glow);
            for (int i = 0; i < LvStars[lv]; i++)
            {
                float a = Rnd(0f, Tau), v = Rnd(90f, 220f + (lv * 40f));
                Star(x, y, (float)Math.Cos(a) * v, (float)Math.Sin(a) * v * 0.75f, Rnd(0.5f, 0.9f + (lv * 0.1f)), Rnd(3f, 4f + lv), col);
            }
            if (lv >= 4)
            {
                for (int i = 0; i < (lv - 3) * 40; i++)
                {
                    float a = Rnd(-(float)Math.PI, 0f), v = Rnd(120f, 300f);
                    SPart p = Part(SPartKind.Confetti, x, y - 10f);
                    p.Vx = (float)Math.Cos(a) * v;
                    p.Vy = (float)Math.Sin(a) * v;
                    p.Life = Rnd(1f, 1.8f);
                    p.Size = Rnd(2f, 3.6f);
                    p.Rot = Rnd(0f, Tau);
                    p.Vr = Rnd(-8f, 8f);
                    if (lv == 5)
                    {
                        p.Color = Rand() < 0.5f ? new Rgb(255, 215, 110) : new Rgb(255, 250, 220);
                        p.HasColor = true;
                    }
                }
            }
            for (int i = 0; i < 6 + (lv * 3); i++)
            {
                float a = Rnd(0f, Tau), v = Rnd(60f, 200f);
                Drop(x, y, (float)Math.Cos(a) * v, (float)Math.Sin(a) * v * 0.6f, Rnd(120f, 260f), 600f, 1f, Rnd(2.4f, 4f));
            }
            // 둘레 몹을 밀어낸다(레벨업 밀치기). Lv6은 쓰러뜨린다.
            foreach (Enemy e in SAlive().ToArray())
            {
                float mx = SX(e.Pos), my = SY(e.Pos);
                float d = Hypot(mx - x, my - y);
                if (d < 70f + (lv * 18f))
                {
                    float l = d == 0f ? 1f : d;
                    float v = 160f + (lv * 40f);
                    if (lv >= 6) SHit(e, 99f, (mx - x) / l * v, (my - y) / l * v);
                    else e.Knock = new Vec2(e.Knock.X + ((mx - x) / l * v * Px * 2.05f), e.Knock.Y - ((my - y) / l * v * Px * 2.05f));
                }
            }
            SShake = Math.Max(SShake, LvShake[lv]);
            SFlash = Math.Max(SFlash, LvFlash[lv]);
            SFlashColor = rb ? new Rgb(255, 250, 235) : gold ? new Rgb(255, 235, 170) : new Rgb(235, 248, 255);
            SStop = Math.Max(SStop, LvStop[lv]);
        }

        /// <summary>숲 매 틱: 레벨업 연출 시계·어두움·파티클·아이템(core.js Scene.update에서 숲에 맞는 부분).</summary>
        private void TickSample()
        {
            // 슬로모션(s.slow) 동안 시뮬 한 틱은 실제 시간 4틱이다: 레벨업 연출 시계와 슬로모션 남은 시간은 실제 시간으로 흐른다(샘플 fx.age += real).
            float real = SSlow > 0f ? Dt * 4f : Dt;
            SSlow = Math.Max(0f, SSlow - real);
            ST += Dt;
            PlayerMoving = Hypot(Player.X - _sLastPlayer.X, Player.Y - _sLastPlayer.Y) > 0.0005f;
            _sLastPlayer = Player;
            SLevelFx f = LevelFx;
            if (f != null)
            {
                f.Age += real;
                if (f.Burst >= 0f && !f.BurstDone && f.Age >= f.Burst)
                {
                    f.BurstDone = true;
                    DoBurst(f.Lv);
                }
                if (f.BoomAt > 0f && !f.BoomDone && f.Age >= f.BoomAt)
                {
                    f.BoomDone = true;
                    DoBurst(6);
                }
                if (f.Age > 3.2f) LevelFx = null;
            }
            f = LevelFx;
            SDark = f != null && f.Lv == 6 ? (f.Age < f.BoomAt ? f.Age / f.BoomAt * 0.72f : Math.Max(0f, 0.72f - ((f.Age - f.BoomAt) * 1.4f))) : 0f;
            TickCds();
            // 처음부터 든 아이템(시작 물대포 등)도 샘플 동작을 갖는다.
            foreach (UpgradeId id in Build.Owned())
            {
                if (id != UpgradeId.Heal) SampleItemOf(id);
            }
            foreach (SampleItem it in SampleItems)
            {
                it.Surge = Math.Max(0f, it.Surge - (Dt * 1.6f));
                int lv = SampleLevel(it.Base);
                if (lv > 0)
                {
                    // 급수 펌프: 아이템 시계가 빨리 돈다(쿨다운이 그만큼 줄어든다).
                    SCurLv = lv;
                    it.Update(this, Dt * Build.FeedScale, lv);
                    SCurLv = 0;
                }
            }
            StepParts(Dt);
            SFlash = Math.Max(0f, SFlash - (real * 1.4f));
        }

        // --- 아이템 ---
        /// <summary>숲에서 든 아이템들의 샘플 동작(처음 고를 때 만든다).</summary>
        public readonly List<SampleItem> SampleItems = new List<SampleItem>();

        public SampleItem SampleItemOf(UpgradeId id)
        {
            UpgradeId b = Loadout.BaseOf(id);
            foreach (SampleItem it in SampleItems)
            {
                if (it.Base == b) return it;
            }
            // 숲 물대포는 겨누는 한 줄기(FireWeapons)다. 샘플 아이템은 고압 방수포의 대폭발만 맡는다.
            if (b == UpgradeId.Hose && Build.Level(UpgradeId.Cannon) == 0) return null;
            SampleItem made = SampleItem.Create(b);
            if (made == null) return null;
            SampleItems.Add(made);
            made.Setup(this);
            return made;
        }

        /// <summary>샘플 레벨: 1~5, 최고급(진화)이면 6. 안 들었으면 0.</summary>
        public int SampleLevel(UpgradeId baseId)
        {
            UpgradeId? evo = Loadout.EvolutionOf(baseId);
            if (evo.HasValue && Build.Level(evo.Value) > 0) return 6;
            return Build.Level(baseId);
        }
    }

    /// <summary>
    /// 샘플 아이템 하나(items-*.js item({...})의 setup·onLevel·update). 그림은 화면(SurvivorView.SampleDraw*)이 이 상태를 읽어 그린다.
    /// </summary>
    public abstract class SampleItem
    {
        public UpgradeId Base;

        /// <summary>레벨업 직후 1 → 0(초당 1.6): 무기를 순간 키운다(샘플 s.surge).</summary>
        public float Surge;

        public virtual void Setup(SurvivorSim s)
        {
        }

        public virtual void OnLevel(SurvivorSim s, int lv)
        {
        }

        public abstract void Update(SurvivorSim s, float dt, int lv);

        /// <summary>아이템별 클래스(SampleItems*.cs)를 만든다. 아직 옮기지 않은 아이템은 null.</summary>
        public static SampleItem Create(UpgradeId id)
        {
            return SampleItemFactory != null ? SampleItemFactory(id) : null;
        }

        /// <summary>아이템 파일들이 정적 생성자에서 채운다(파일을 나눠 옮기려고).</summary>
        public static Func<UpgradeId, SampleItem> SampleItemFactory = SampleItemsRegistry.Make;
    }
}
