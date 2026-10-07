using System;
using System.Collections.Generic;

namespace FireGame.Prototypes.Logic
{
    /// <summary>소방견 한 마리: 노린 몹에게 달려가 물고, 없으면 타는 건물 문 앞에서 짖으며 적신다.</summary>
    public sealed class Dog
    {
        public Vec2 Pos;
        public Enemy Target;
        public Structure Barking;
        public float Think;
        public float Bite;
        public float Rescue;

        /// <summary>달리는 방향(그림용).</summary>
        public Vec2 Facing = new Vec2(1f, 0f);
    }

    /// <summary>벽에 튕기는 물풍선. Small은 폭우가 갈라 낸 작은 풍선(다시 갈라지지 않는다).</summary>
    public sealed class WaterBalloon
    {
        public Vec2 Pos;
        public Vec2 Vel;
        public int Bounces;
        public float Life;
        public bool Small;
        public Enemy Last;
        public bool Dead;
    }

    /// <summary>나갔다 돌아오는 소화기.</summary>
    public sealed class Boomerang
    {
        public Vec2 Pos;
        public Vec2 Dir;
        public float Range;
        public float Out;
        public bool Back;
        public float Spin;
        public readonly List<Enemy> Struck = new List<Enemy>();
        public bool Dead;
    }

    /// <summary>분말 회오리(소화기 진화): 떠돌며 둘레 몹을 끌어당긴다.</summary>
    public sealed class Tornado
    {
        public Vec2 Pos;
        public Vec2 Goal;
        public float Life;
        public float Think;
    }

    /// <summary>발밑 액체질소 지뢰.</summary>
    public sealed class Mine
    {
        public Vec2 Pos;
        public float Arm;
    }

    /// <summary>굴러가며 커지는 거품 눈덩이.</summary>
    public sealed class FoamBall
    {
        public Vec2 Pos;
        public Vec2 Dir;
        public float R;
        public float MaxR;
        public float Age;
        public readonly List<Enemy> Struck = new List<Enemy>();
        public bool Dead;
    }

    /// <summary>날아가는 비눗방울(닿으면 가두거나 친다).</summary>
    public sealed class BubbleShot
    {
        public Vec2 From;
        public Vec2 Pos;
        public Enemy Target;
        public float Age;
        public bool Dead;
    }

    /// <summary>맨홀 물기둥: Fuse가 다 되면 솟는다.</summary>
    public sealed class Geyser
    {
        public Vec2 Pos;
        public float Fuse;
        public float Radius;
        public bool Burst;
    }

    /// <summary>사다리: 뻗었다 내려찍는다. 다리(진화)는 Life 동안 남아 길을 막는다.</summary>
    public sealed class Ladder
    {
        public Vec2 From;
        public Vec2 Dir;
        public float Len;
        public float Age;
        public float Life;
        public bool Struck;

        public Vec2 Tip
        {
            get { return new Vec2(From.X + (Dir.X * Len), From.Y + (Dir.Y * Len)); }
        }
    }

    /// <summary>바닥에 남아 잠깐 도는 물 고리(물 회오리) 한 점.</summary>
    public sealed class WhirlMark
    {
        public Vec2 Pos;
        public float Life;
    }

    /// <summary>
    /// 새 무기 9종(2026-10-07): 하나는 공간 하나를 맡고, 모두 불 몹(Damage)과 건물 불(Soak)을 둘 다 맞힌다.
    /// 우선순위는 마을을 노리는 몹(Goal 있음) → 곁의 몹 → 타는 건물. 레벨업은 개수·크기·갈래로 보이게 오른다.
    /// </summary>
    public sealed partial class SurvivorSim
    {
        // --- 레벨별 표(0번은 없음) ---
        public static readonly int[] DogCount = { 0, 1, 1, 2, 2, 3 };
        public static readonly int[] BalloonCount = { 0, 1, 1, 2, 2, 3 };
        public static readonly int[] BalloonBounces = { 0, 3, 4, 4, 5, 6 };
        public static readonly int[] BoomCount = { 0, 1, 1, 2, 2, 3 };
        public static readonly int[] MineMax = { 0, 2, 3, 4, 5, 6 };
        public static readonly int[] FoamCount = { 0, 1, 1, 2, 2, 3 };
        public static readonly int[] BubbleCount = { 0, 1, 2, 2, 3, 4 };
        public static readonly int[] GeyserCount = { 0, 1, 1, 2, 3, 4 };
        public static readonly int[] LadderDirs = { 0, 1, 1, 2, 2, 3 };
        public static readonly int[] WhipArms = { 0, 1, 1, 2, 2, 3 };

        // --- 소방견 ---
        public const float DogSpeed = 5f;
        public const float DogSpeedStep = 0.5f;
        public const float DogBiteEvery = 0.4f;
        public const float DogBite = 6f;
        public const float DogSight = 14f;
        public const float DogBarkSoak = 0.3f;
        public const float DogRescueEvery = 2f;
        public const int DogPackCount = 4;

        // --- 물풍선 ---
        public const float BalloonEvery = 4f;
        public const float BalloonSpeed = 8f;
        public const float BalloonSplash = 1.2f;
        public const float BalloonHit = 8f;
        public const float BalloonSoak = 0.6f;
        public const float BalloonLife = 6f;

        // --- 소화기 ---
        public const float BoomEvery = 3f;
        public const float BoomOutTime = 0.5f;
        public const float BoomBackSpeed = 12f;
        public const float BoomHit = 10f;
        public const float BoomReach = 0.7f;
        public const float TornadoEvery = 8f;
        public const float TornadoLife = 6f;
        public const float TornadoRadius = 3f;
        public const float TornadoDps = 15f;

        // --- 지뢰 ---
        public const float MineEvery = 1.2f;
        public const float MineArm = 0.3f;
        public const float FreezeTime = 2f;
        public const float MineShatter = 10f;
        public const float IceLink = 6f;

        // --- 거품 ---
        public const float FoamEvery = 4f;
        public const float FoamSpeed = 3f;
        public const float FoamHit = 8f;
        public const float FoamBurst = 12f;
        public const float FoamLife = 5f;
        public const float AvalancheEvery = 12f;
        public const float AvalancheHit = 25f;

        // --- 비눗방울 ---
        public const float BubbleEvery = 2.5f;
        public const float BubbleFlight = 0.35f;
        public const float BubbleHold = 1.5f;
        public const float BubbleHit = 10f;
        public const float BubbleSight = 9f;

        // --- 맨홀 ---
        public const float GeyserEvery = 3f;
        public const float GeyserFuse = 0.5f;
        public const float GeyserHit = 14f;
        public const float GeyserSight = 10f;
        public const float ManholeGrid = 6f;

        // --- 사다리차 ---
        public const float LadderEvery = 4f;
        public const float LadderReach = 0.25f;
        public const float LadderHit = 16f;
        public const float LadderWidth = 0.6f;
        public const float BridgeLife = 6f;
        public const float BridgeDps = 8f;

        // --- 채찍 ---
        public const float WhipTurn = 1.2f;
        public const float WhipHit = 7f;
        public const float WhipCool = 0.35f;
        public const float WhirlLife = 3f;
        public const float WhirlDps = 10f;

        public readonly List<Dog> Dogs = new List<Dog>();
        public readonly List<WaterBalloon> Balloons = new List<WaterBalloon>();
        public readonly List<Boomerang> Boomerangs = new List<Boomerang>();
        public readonly List<Tornado> Tornadoes = new List<Tornado>();
        public readonly List<Mine> Mines = new List<Mine>();
        public readonly List<FoamBall> FoamBalls = new List<FoamBall>();
        public readonly List<BubbleShot> BubbleShots = new List<BubbleShot>();
        public readonly List<Vec2> Manholes = new List<Vec2>();
        public readonly List<Geyser> Geysers = new List<Geyser>();
        public readonly List<Ladder> Ladders = new List<Ladder>();
        public readonly List<WhirlMark> WhirlMarks = new List<WhirlMark>();

        /// <summary>채찍 갈래의 지금 각도(라디안, 첫 갈래).</summary>
        public float WhipAngle;

        /// <summary>지금 지나가는 거품 파도(산사태)의 앞머리 x와 방향. 없으면 null.</summary>
        public float? AvalancheX;
        public float AvalancheDir = 1f;
        public float AvalancheY;

        // --- 한 틱 신호(그림용) ---
        public readonly List<Vec2> DogBites = new List<Vec2>();
        public readonly List<Vec2> BalloonSplashes = new List<Vec2>();
        public readonly List<Vec2> MineFreezes = new List<Vec2>();
        public readonly List<Vec2> FoamBursts = new List<Vec2>();
        public readonly List<Vec2> BubblePops = new List<Vec2>();
        public readonly List<Vec2> GeyserBursts = new List<Vec2>();
        public readonly List<Ladder> LadderStrikes = new List<Ladder>();

        private float _dogClock;
        private float _balloonClock = 1f;
        private float _boomClock = 0.5f;
        private float _tornadoClock = 1f;
        private float _mineClock;
        private float _foamClock = 1f;
        private float _avalancheClock = 2f;
        private float _bubbleClock = 0.5f;
        private float _geyserClock = 1f;
        private float _ladderClock = 0.5f;
        private float _whirlMarkClock;
        private readonly List<Enemy> _avalancheHit = new List<Enemy>();
        private readonly List<Structure> _avalancheSoaked = new List<Structure>();

        private void ClearWeaponSignals()
        {
            DogBites.Clear();
            BalloonSplashes.Clear();
            MineFreezes.Clear();
            FoamBursts.Clear();
            BubblePops.Clear();
            GeyserBursts.Clear();
            LadderStrikes.Clear();
        }

        /// <summary>맨홀: 맵의 ManholeGrid 격자 점 중 건물·물 밖인 곳(맵을 깐 뒤 한 번).</summary>
        private void PlaceManholes()
        {
            Manholes.Clear();
            for (float y = ManholeGrid / 2f; y < ArenaSize; y += ManholeGrid)
            {
                for (float x = ManholeGrid / 2f; x < ArenaSize; x += ManholeGrid)
                {
                    var p = new Vec2(x, y);
                    bool blocked = false;
                    foreach (Structure s in Structures)
                    {
                        if (s.Within(p, 0.8f))
                        {
                            blocked = true;
                            break;
                        }
                    }
                    if (!blocked) Manholes.Add(p);
                }
            }
        }

        /// <summary>카드를 고른 순간: 그 무기가 곧바로 한 번 나간다(고른 게 바로 보인다).</summary>
        private void PrimeWeapon(UpgradeId id)
        {
            switch (Loadout.IsEvolution(id) ? Loadout.BaseOf(id) : id)
            {
                case UpgradeId.Balloon: _balloonClock = 0.05f; break;
                case UpgradeId.Extinguisher: _boomClock = 0.05f; break;
                case UpgradeId.Foam: _foamClock = 0.05f; break;
                case UpgradeId.Bubble: _bubbleClock = 0.05f; break;
                case UpgradeId.Manhole: _geyserClock = 0.05f; break;
                case UpgradeId.Ladder: _ladderClock = 0.05f; break;
                case UpgradeId.Mine: _mineClock = 0f; break;
            }
            if (id == UpgradeId.Tornado) _tornadoClock = 0.5f;
            if (id == UpgradeId.Avalanche) _avalancheClock = 0.5f;
        }

        private void TickNewWeapons()
        {
            TickDogs();
            TickBalloons();
            TickBoomerangs();
            TickMines();
            TickFoam();
            TickBubbles();
            TickGeysers();
            TickLadders();
            TickWhip();
        }

        // ------------------------------------------------------------------
        // 공통
        // ------------------------------------------------------------------

        /// <summary>at 둘레 r에 물을 쏟는다: 바닥 불 끄기·등줄 적시기·구조물 적시기(저항 없이)·몹 피해.</summary>
        private void Splash(Vec2 at, float r, float hit, float soak, float knock, HitSource source)
        {
            Douse(at, r);
            if (Lanterns.Count > 0) WetLanterns(at, r);
            foreach (Structure st in Structures)
            {
                if (st.Burning && st.Within(at, r)) Soak(st, soak, false);
            }
            Near(at, r, _near);
            foreach (Enemy e in _near) Damage(e, hit, Knockback(at, e.Pos, knock), true, source, at);
        }

        /// <summary>노릴 몹: 마을을 노리는 몹(Goal 있음) 중 from에서 가장 가까운 것, 없으면 아무 몹. range 안, Heavy 포함.</summary>
        private Enemy PickTarget(Vec2 from, float range, Func<Enemy, bool> skip = null)
        {
            Enemy raider = null;
            Enemy any = null;
            float rd = range;
            float ad = range;
            foreach (Enemy e in Enemies)
            {
                if (e.Dead || e.Captured > 0f || (skip != null && skip(e))) continue;
                float d = e.Pos.DistanceTo(from);
                if (e.Goal != null && d < rd)
                {
                    rd = d;
                    raider = e;
                }
                if (d < ad)
                {
                    ad = d;
                    any = e;
                }
            }
            return raider ?? any;
        }

        /// <summary>from에서 range 안 몹이 가장 많이 몰린 방향(8방위 중). 몹이 없으면 바라보는 쪽.</summary>
        private Vec2 CrowdDirection(Vec2 from, float range, float skipAngle = 99f)
        {
            int best = -1;
            int bestN = 0;
            var counts = new int[8];
            foreach (Enemy e in Enemies)
            {
                if (e.Dead) continue;
                float dx = e.Pos.X - from.X;
                float dy = e.Pos.Y - from.Y;
                if ((dx * dx) + (dy * dy) > range * range) continue;
                double a = Math.Atan2(dy, dx);
                int k = (int)Math.Round(a / (Math.PI / 4)) & 7;
                counts[k] += e.Goal != null ? 2 : 1;
            }
            for (int k = 0; k < 8; k++)
            {
                if (counts[k] > bestN && Math.Abs(AngleDiff((float)(k * Math.PI / 4), skipAngle)) > 0.5f)
                {
                    bestN = counts[k];
                    best = k;
                }
            }
            if (best < 0) return Facing;
            double ang = best * Math.PI / 4;
            return new Vec2((float)Math.Cos(ang), (float)Math.Sin(ang));
        }

        private static float AngleDiff(float a, float b)
        {
            float d = a - b;
            while (d > Math.PI) d -= (float)(Math.PI * 2);
            while (d < -Math.PI) d += (float)(Math.PI * 2);
            return d;
        }

        private static Vec2 Toward(Vec2 from, Vec2 to)
        {
            float dx = to.X - from.X;
            float dy = to.Y - from.Y;
            float d = (float)Math.Sqrt((dx * dx) + (dy * dy));
            return d < 0.0001f ? new Vec2(1f, 0f) : new Vec2(dx / d, dy / d);
        }

        private static Vec2 Step(Vec2 from, Vec2 to, float dist)
        {
            float d = from.DistanceTo(to);
            if (d <= dist || d < 0.0001f) return to;
            return new Vec2(from.X + ((to.X - from.X) / d * dist), from.Y + ((to.Y - from.Y) / d * dist));
        }

        /// <summary>소방관 range 안에서 가장 센 불이 난 건물(없으면 null).</summary>
        private Structure HottestNear(Vec2 at, float range)
        {
            Structure best = null;
            foreach (Structure st in Structures)
            {
                if (!st.IsBuilding || !st.Burning || st.Collapsed || st.DistanceTo(at) > range) continue;
                if (best == null || st.Fire > best.Fire) best = st;
            }
            return best;
        }

        // ------------------------------------------------------------------
        // 소방견
        // ------------------------------------------------------------------

        private void TickDogs()
        {
            int lv = Build.PowerOf(UpgradeId.Dog);
            bool pack = Build.Level(UpgradeId.DogPack) > 0;
            int want = lv == 0 ? 0 : pack ? DogPackCount : DogCount[lv];
            while (Dogs.Count < want) Dogs.Add(new Dog { Pos = new Vec2(Player.X - 0.8f, Player.Y - 0.5f - (0.4f * Dogs.Count)) });
            while (Dogs.Count > want) Dogs.RemoveAt(Dogs.Count - 1);
            if (want == 0) return;
            float speed = pack ? DogSpeed + (DogSpeedStep * 4f) + 0.5f : DogSpeed + (DogSpeedStep * (lv - 1));
            float bite = DogBite * (1f + (0.25f * (lv >= 4 ? 2 : lv >= 2 ? 1 : 0))) * (pack ? 1.25f : 1f);
            for (int i = 0; i < Dogs.Count; i++)
            {
                Dog d = Dogs[i];
                d.Think -= Dt;
                if (d.Target != null && (d.Target.Dead || d.Target.Pos.DistanceTo(Player) > DogSight + 4f)) d.Target = null;
                if (d.Think <= 0f)
                {
                    d.Think = 0.5f;
                    // 개마다 다른 몹: 이미 다른 개가 문 몹은 피한다.
                    Dog me = d;
                    d.Target = PickTarget(d.Pos, DogSight, e => e.Pos.DistanceTo(Player) > DogSight || Dogs.Exists(o => o != me && o.Target == e));
                    d.Barking = d.Target == null ? HottestNear(Player, 8f) : null;
                    // 구조견 무리: 갇힌 사람이 있는 타는 건물이 가까우면 그쪽이 먼저.
                    if (pack && i % 2 == 1)
                    {
                        Structure trapped = null;
                        foreach (Structure st in Structures)
                        {
                            if (st.Burning && st.Residents > 0 && st.DistanceTo(Player) <= 12f && (trapped == null || st.DistanceTo(d.Pos) < trapped.DistanceTo(d.Pos))) trapped = st;
                        }
                        if (trapped != null)
                        {
                            d.Target = null;
                            d.Barking = trapped;
                        }
                    }
                }
                Vec2 goal;
                if (d.Target != null) goal = d.Target.Pos;
                else if (d.Barking != null && d.Barking.Burning) goal = d.Barking.Door;
                else
                {
                    // 곁을 따른다: 소방관 뒤쪽 둘레에 줄지어.
                    double a = Math.PI + (i * 0.7f);
                    goal = new Vec2(Player.X + (float)(Math.Cos(a) * 1.4f), Player.Y + (float)(Math.Sin(a) * 1.1f));
                }
                Vec2 before = d.Pos;
                d.Pos = ClampToArena(Step(d.Pos, goal, speed * Dt));
                if (d.Pos.DistanceTo(before) > 0.001f) d.Facing = Toward(before, d.Pos);
                d.Bite -= Dt;
                if (d.Target != null && d.Pos.DistanceTo(d.Target.Pos) <= d.Target.Radius + 0.5f && d.Bite <= 0f)
                {
                    d.Bite = DogBiteEvery;
                    Damage(d.Target, bite, Knockback(d.Pos, d.Target.Pos, 3f), true, HitSource.Dog, d.Pos);
                    DogBites.Add(d.Pos);
                }
                else if (d.Barking != null && d.Barking.Burning && d.Pos.DistanceTo(d.Barking.Door) < 0.8f)
                {
                    // 구조견: 갇힌 사람이 있으면 물 대신 사람을 문다(도착하고 0.5초 뒤 첫 명, 그 뒤 DogRescueEvery마다).
                    if (pack && d.Barking.Residents > 0)
                    {
                        d.Rescue += Dt;
                        if (d.Rescue >= DogRescueEvery)
                        {
                            d.Rescue = 0f;
                            RescueOne(d.Barking);
                        }
                    }
                    else Soak(d.Barking, DogBarkSoak * Dt, false);
                }
                else d.Rescue = DogRescueEvery - 0.5f;
            }
        }

        // ------------------------------------------------------------------
        // 물풍선
        // ------------------------------------------------------------------

        private void TickBalloons()
        {
            int lv = Build.PowerOf(UpgradeId.Balloon);
            bool storm = Build.Level(UpgradeId.BalloonStorm) > 0;
            if (lv > 0)
            {
                _balloonClock -= Dt;
                if (_balloonClock <= 0f)
                {
                    _balloonClock = BalloonEvery;
                    int n = BalloonCount[lv];
                    Enemy target = PickTarget(Player, 10f);
                    Vec2 dir = target != null ? Toward(Player, target.Pos) : Facing;
                    double baseA = Math.Atan2(dir.Y, dir.X);
                    for (int k = 0; k < n; k++)
                    {
                        double a = baseA + ((k - ((n - 1) / 2f)) * 0.5f);
                        Balloons.Add(new WaterBalloon { Pos = Player, Vel = new Vec2((float)Math.Cos(a) * BalloonSpeed, (float)Math.Sin(a) * BalloonSpeed), Bounces = BalloonBounces[lv], Life = BalloonLife });
                    }
                    ShotsFired += n;
                }
            }
            int count = Balloons.Count;
            for (int i = 0; i < count; i++)
            {
                WaterBalloon b = Balloons[i];
                if (b.Dead) continue;
                b.Life -= Dt;
                var next = new Vec2(b.Pos.X + (b.Vel.X * Dt), b.Pos.Y + (b.Vel.Y * Dt));
                bool bounced = false;
                // 경기장 끝.
                if (next.X < 0.5f || next.X > ArenaSize - 0.5f)
                {
                    b.Vel.X = -b.Vel.X;
                    bounced = true;
                }
                if (next.Y < 0.5f || next.Y > ArenaSize - 0.5f)
                {
                    b.Vel.Y = -b.Vel.Y;
                    bounced = true;
                }
                // 건물 벽: 덜 파고든 축으로 튕긴다.
                if (!bounced)
                {
                    foreach (Structure s in Structures)
                    {
                        if (s.Collapsed || s.Kind == StructureKind.Tree || s.Kind == StructureKind.Water) continue;
                        float ox = s.Half.X + 0.3f - Math.Abs(next.X - s.Pos.X);
                        float oy = s.Half.Y + 0.3f - Math.Abs(next.Y - s.Pos.Y);
                        if (ox <= 0f || oy <= 0f) continue;
                        if (ox < oy) b.Vel.X = -b.Vel.X;
                        else b.Vel.Y = -b.Vel.Y;
                        bounced = true;
                        break;
                    }
                }
                // 몹에 맞으면 그 몹에서 튕긴다(같은 몹에 연달아 맞지 않는다).
                Enemy hitE = null;
                if (!bounced)
                {
                    Near(next, 0.35f, _near);
                    foreach (Enemy e in _near)
                    {
                        if (e != b.Last && e.Captured <= 0f)
                        {
                            hitE = e;
                            break;
                        }
                    }
                    if (hitE != null)
                    {
                        Vec2 away = Toward(hitE.Pos, b.Pos);
                        float sp = (float)Math.Sqrt((b.Vel.X * b.Vel.X) + (b.Vel.Y * b.Vel.Y));
                        b.Vel = new Vec2(away.X * sp, away.Y * sp);
                        b.Last = hitE;
                        bounced = true;
                    }
                }
                if (bounced)
                {
                    float small = b.Small ? 0.6f : 1f;
                    Splash(b.Pos, BalloonSplash * small, BalloonHit * small, BalloonSoak * small, 4f, HitSource.Balloon);
                    BalloonSplashes.Add(b.Pos);
                    b.Bounces--;
                    if (storm && !b.Small)
                    {
                        double a = Math.Atan2(b.Vel.Y, b.Vel.X);
                        for (int k = -1; k <= 1; k += 2)
                        {
                            double sa = a + (k * 0.6f);
                            Balloons.Add(new WaterBalloon { Pos = b.Pos, Vel = new Vec2((float)Math.Cos(sa) * BalloonSpeed, (float)Math.Sin(sa) * BalloonSpeed), Bounces = 2, Life = 2f, Small = true, Last = b.Last });
                        }
                    }
                }
                else b.Pos = next;
                if (b.Bounces < 0 || b.Life <= 0f) b.Dead = true;
            }
            Balloons.RemoveAll(b => b.Dead);
        }

        // ------------------------------------------------------------------
        // 소화기 부메랑 · 분말 회오리
        // ------------------------------------------------------------------

        private void TickBoomerangs()
        {
            int lv = Build.PowerOf(UpgradeId.Extinguisher);
            bool tornado = Build.Level(UpgradeId.Tornado) > 0;
            if (lv > 0)
            {
                _boomClock -= Dt;
                if (_boomClock <= 0f)
                {
                    _boomClock = BoomEvery;
                    int n = BoomCount[lv];
                    float range = 6f + (0.75f * (lv - 1));
                    Enemy target = PickTarget(Player, range + 2f);
                    Vec2 dir = target != null ? Toward(Player, target.Pos) : Facing;
                    double baseA = Math.Atan2(dir.Y, dir.X);
                    for (int k = 0; k < n; k++)
                    {
                        double a = baseA + (k * Math.PI * 2 / n);
                        Boomerangs.Add(new Boomerang { Pos = Player, Dir = new Vec2((float)Math.Cos(a), (float)Math.Sin(a)), Range = range });
                    }
                    ShotsFired += n;
                }
            }
            foreach (Boomerang b in Boomerangs)
            {
                b.Spin += Dt * 14f;
                if (!b.Back)
                {
                    b.Out += Dt;
                    float t = Math.Min(1f, b.Out / BoomOutTime);
                    // 나갈 땐 빠르게, 끝에서 느려진다.
                    float reach = b.Range * (1f - ((1f - t) * (1f - t)));
                    var to = new Vec2(Player.X + (b.Dir.X * reach), Player.Y + (b.Dir.Y * reach));
                    b.Pos = ClampToArena(Step(b.Pos, to, 40f * Dt));
                    if (t >= 1f)
                    {
                        b.Back = true;
                        b.Struck.Clear();
                    }
                }
                else
                {
                    b.Pos = Step(b.Pos, Player, BoomBackSpeed * Dt);
                    if (b.Pos.DistanceTo(Player) < 0.4f) b.Dead = true;
                }
                Douse(b.Pos, BoomReach);
                foreach (Structure st in Structures)
                {
                    if (st.Burning && st.Within(b.Pos, BoomReach)) Soak(st, 0.6f * Dt, false);
                }
                Near(b.Pos, BoomReach, _near);
                foreach (Enemy e in _near)
                {
                    if (b.Struck.Contains(e)) continue;
                    b.Struck.Add(e);
                    Damage(e, BoomHit, Knockback(b.Pos, e.Pos, 4f), true, HitSource.Extinguisher, b.Pos);
                }
            }
            Boomerangs.RemoveAll(b => b.Dead);

            if (tornado)
            {
                _tornadoClock -= Dt;
                if (_tornadoClock <= 0f)
                {
                    _tornadoClock = TornadoEvery;
                    Tornadoes.Add(new Tornado { Pos = new Vec2(Player.X + (Facing.X * 3f), Player.Y + (Facing.Y * 3f)), Goal = Player, Life = TornadoLife });
                }
            }
            foreach (Tornado tw in Tornadoes)
            {
                tw.Life -= Dt;
                tw.Think -= Dt;
                if (tw.Think <= 0f)
                {
                    tw.Think = 1f;
                    Vec2 crowd = CrowdDirection(tw.Pos, 10f);
                    tw.Goal = ClampToArena(new Vec2(tw.Pos.X + (crowd.X * 6f), tw.Pos.Y + (crowd.Y * 6f)));
                }
                tw.Pos = Step(tw.Pos, tw.Goal, 2.5f * Dt);
                Near(tw.Pos, TornadoRadius, _near);
                foreach (Enemy e in _near)
                {
                    Vec2 pull = Knockback(e.Pos, tw.Pos, 6f * Dt * 10f);
                    e.Knock.X += pull.X * Dt * 6f;
                    e.Knock.Y += pull.Y * Dt * 6f;
                    Damage(e, TornadoDps * Dt, default, false, HitSource.Extinguisher, tw.Pos);
                }
                Douse(tw.Pos, 2f);
                foreach (Structure st in Structures)
                {
                    if (st.Burning && st.Within(tw.Pos, 2f)) Soak(st, 1f * Dt, false);
                }
            }
            Tornadoes.RemoveAll(t => t.Life <= 0f);
        }

        // ------------------------------------------------------------------
        // 액체질소 지뢰 · 빙결 지대
        // ------------------------------------------------------------------

        private void TickMines()
        {
            int lv = Build.PowerOf(UpgradeId.Mine);
            if (lv == 0)
            {
                Mines.Clear();
                return;
            }
            bool field = Build.Level(UpgradeId.IceField) > 0;
            float radius = 1.5f + (0.25f * (lv - 1));
            _mineClock -= Dt;
            if (_mineClock <= 0f)
            {
                _mineClock = MineEvery;
                // 같은 자리에 겹쳐 깔지 않는다: 가장 가까운 지뢰에서 1.5칸 넘게 떨어졌을 때만.
                bool near = Mines.Exists(m => m.Pos.DistanceTo(Player) < 1.5f);
                if (!near)
                {
                    Mines.Add(new Mine { Pos = Player, Arm = MineArm });
                    while (Mines.Count > MineMax[lv]) Mines.RemoveAt(0);
                }
            }
            for (int i = Mines.Count - 1; i >= 0; i--)
            {
                Mine m = Mines[i];
                if (m.Arm > 0f)
                {
                    m.Arm -= Dt;
                    continue;
                }
                Near(m.Pos, 0.45f, _near);
                bool stepped = _near.Exists(e => e.Frozen <= 0f && e.Captured <= 0f);
                if (!stepped) continue;
                Freeze(m.Pos, radius);
                Mines.RemoveAt(i);
            }
            if (!field) return;
            // 빙결 지대: IceLink 안 지뢰끼리 얼음 선. 선을 밟은 몹이 언다(지뢰는 그대로).
            for (int a = 0; a < Mines.Count; a++)
            {
                for (int b = a + 1; b < Mines.Count; b++)
                {
                    Vec2 pa = Mines[a].Pos;
                    Vec2 pb = Mines[b].Pos;
                    if (pa.DistanceTo(pb) > IceLink) continue;
                    var mid = new Vec2((pa.X + pb.X) / 2f, (pa.Y + pb.Y) / 2f);
                    Near(mid, (pa.DistanceTo(pb) / 2f) + 0.5f, _near);
                    foreach (Enemy e in _near)
                    {
                        if (e.Frozen > 0f || e.Captured > 0f || e.Heavy) continue;
                        if (SegmentDistance(e.Pos, pa, pb) <= 0.4f + e.Radius)
                        {
                            e.Frozen = FreezeTime;
                            MineFreezes.Add(e.Pos);
                        }
                    }
                }
            }
        }

        /// <summary>at 둘레 r 안 몹을 FreezeTime 동안 얼린다(질긴 큰 불은 절반). 구조물 불도 식힌다.</summary>
        private void Freeze(Vec2 at, float r)
        {
            MineFreezes.Add(at);
            Near(at, r, _near);
            foreach (Enemy e in _near)
            {
                if (e.Captured > 0f) continue;
                e.Frozen = e.Heavy ? FreezeTime * 0.5f : FreezeTime;
                e.Knock = default;
            }
            Douse(at, r);
            foreach (Structure st in Structures)
            {
                if (st.Burning && st.Within(at, r)) Soak(st, 0.4f, false);
            }
        }

        // ------------------------------------------------------------------
        // 거품 눈덩이 · 거품 산사태
        // ------------------------------------------------------------------

        private void TickFoam()
        {
            int lv = Build.PowerOf(UpgradeId.Foam);
            bool avalanche = Build.Level(UpgradeId.Avalanche) > 0;
            if (lv > 0)
            {
                _foamClock -= Dt;
                if (_foamClock <= 0f)
                {
                    _foamClock = FoamEvery;
                    float maxR = 1.2f + (0.3f * (lv - 1));
                    Vec2 first = CrowdDirection(Player, 10f);
                    float skip = (float)Math.Atan2(first.Y, first.X);
                    for (int k = 0; k < FoamCount[lv]; k++)
                    {
                        Vec2 dir = k == 0 ? first : CrowdDirection(Player, 10f, skip + (k * 2.1f));
                        FoamBalls.Add(new FoamBall { Pos = Player, Dir = dir, R = 0.6f, MaxR = maxR });
                    }
                    ShotsFired += FoamCount[lv];
                }
            }
            foreach (FoamBall f in FoamBalls)
            {
                f.Age += Dt;
                f.Pos = ClampToArena(new Vec2(f.Pos.X + (f.Dir.X * FoamSpeed * Dt), f.Pos.Y + (f.Dir.Y * FoamSpeed * Dt)));
                Douse(f.Pos, f.R);
                Near(f.Pos, f.R, _near);
                foreach (Enemy e in _near)
                {
                    if (f.Struck.Contains(e)) continue;
                    f.Struck.Add(e);
                    Damage(e, FoamHit, Knockback(f.Pos, e.Pos, 2f), true, HitSource.Foam, f.Pos);
                    // 삼킨 만큼 부푼다.
                    if (e.Dead) f.R = Math.Min(f.MaxR, f.R + 0.15f);
                }
                bool wall = false;
                foreach (Structure st in Structures)
                {
                    if (st.Collapsed || st.Kind == StructureKind.Tree || st.Kind == StructureKind.Water) continue;
                    if (st.Within(f.Pos, f.R * 0.5f)) wall = true;
                }
                if (wall || f.Age >= FoamLife || f.R >= f.MaxR)
                {
                    f.Dead = true;
                    Splash(f.Pos, f.R + 1f, FoamBurst, 0.5f, 6f, HitSource.Foam);
                    FoamBursts.Add(f.Pos);
                }
            }
            FoamBalls.RemoveAll(f => f.Dead);

            if (!avalanche)
            {
                AvalancheX = null;
                return;
            }
            _avalancheClock -= Dt;
            if (_avalancheClock <= 0f && !AvalancheX.HasValue)
            {
                _avalancheClock = AvalancheEvery;
                Vec2 crowd = CrowdDirection(Player, 14f);
                AvalancheDir = crowd.X >= 0f ? 1f : -1f;
                AvalancheX = Player.X - (AvalancheDir * 14f);
                AvalancheY = Player.Y;
                _avalancheHit.Clear();
                _avalancheSoaked.Clear();
            }
            if (!AvalancheX.HasValue) return;
            float x = AvalancheX.Value + (AvalancheDir * 14f * Dt);
            AvalancheX = x;
            // 앞머리 폭 1.5칸, 위아래 9칸 띠.
            foreach (Enemy e in Enemies)
            {
                if (e.Dead || _avalancheHit.Contains(e) || Math.Abs(e.Pos.X - x) > 1.5f || Math.Abs(e.Pos.Y - AvalancheY) > 9f) continue;
                _avalancheHit.Add(e);
                Damage(e, AvalancheHit, new Vec2(AvalancheDir * 10f, 0f), true, HitSource.Foam, new Vec2(x, e.Pos.Y));
            }
            foreach (Structure st in Structures)
            {
                if (!st.Burning || _avalancheSoaked.Contains(st) || Math.Abs(st.Pos.X - x) > st.Half.X + 1.5f || Math.Abs(st.Pos.Y - AvalancheY) > 9f + st.Half.Y) continue;
                _avalancheSoaked.Add(st);
                Soak(st, 0.8f, false);
            }
            if (Math.Abs(x - Player.X) > 14f && Math.Sign(x - Player.X) == Math.Sign(AvalancheDir)) AvalancheX = null;
        }

        // ------------------------------------------------------------------
        // 비눗방울 · 방울 폭포
        // ------------------------------------------------------------------

        private void TickBubbles()
        {
            int lv = Build.PowerOf(UpgradeId.Bubble);
            bool fall = Build.Level(UpgradeId.BubbleFall) > 0;
            if (lv > 0)
            {
                _bubbleClock -= Dt;
                if (_bubbleClock <= 0f)
                {
                    _bubbleClock = BubbleEvery;
                    for (int k = 0; k < BubbleCount[lv]; k++)
                    {
                        Enemy target = PickTarget(Player, BubbleSight, e => BubbleShots.Exists(b => b.Target == e));
                        if (target == null) break;
                        BubbleShots.Add(new BubbleShot { From = Player, Pos = Player, Target = target });
                        ShotsFired++;
                    }
                }
            }
            float cap = 6f + (3.5f * (lv - 1));
            foreach (BubbleShot b in BubbleShots)
            {
                b.Age += Dt;
                Enemy t = b.Target;
                if (t == null || t.Dead)
                {
                    b.Dead = true;
                    continue;
                }
                float k = Math.Min(1f, b.Age / BubbleFlight);
                b.Pos = new Vec2(b.From.X + ((t.Pos.X - b.From.X) * k), b.From.Y + ((t.Pos.Y - b.From.Y) * k));
                if (k < 1f) continue;
                b.Dead = true;
                if (!t.Heavy && t.Hp <= cap * (fall ? 1.5f : 1f))
                {
                    t.Captured = BubbleHold;
                    t.Frozen = 0f;
                    t.Knock = default;
                }
                else Damage(t, BubbleHit, default, true, HitSource.Bubble, t.Pos);
            }
            BubbleShots.RemoveAll(b => b.Dead);
        }

        /// <summary>갇힌 몹의 방울이 다 됐다(MoveEnemies에서): 터지며 잡히고, 아래로 물이 쏟아진다. 방울 폭포면 둘레 방울도 모아 한꺼번에.</summary>
        private void PopBubble(Enemy e)
        {
            bool fall = Build.Level(UpgradeId.BubbleFall) > 0;
            if (fall)
            {
                // 둘레 8칸 안 갇힌 몹을 한데 모아 큰 방울로 터뜨린다.
                float sx = e.Pos.X;
                float sy = e.Pos.Y;
                int n = 1;
                foreach (Enemy o in Enemies)
                {
                    if (o == e || o.Dead || o.Captured <= 0f || o.Pos.DistanceTo(e.Pos) > 8f) continue;
                    sx += o.Pos.X;
                    sy += o.Pos.Y;
                    n++;
                    o.Captured = 0f;
                    Kill(o);
                }
                var mid = new Vec2(sx / n, sy / n);
                Kill(e);
                Splash(mid, 3f, 20f, 0.6f, 8f, HitSource.Bubble);
                BubblePops.Add(mid);
                return;
            }
            Kill(e);
            Splash(e.Pos, 1.5f, 0f, 0.5f, 0f, HitSource.Bubble);
            BubblePops.Add(e.Pos);
        }

        // ------------------------------------------------------------------
        // 맨홀 간헐천 · 수도관 폭발
        // ------------------------------------------------------------------

        private void TickGeysers()
        {
            int lv = Build.PowerOf(UpgradeId.Manhole);
            bool line = Build.Level(UpgradeId.Waterline) > 0;
            float radius = 1.6f + (0.15f * (lv - 1));
            if (lv > 0)
            {
                _geyserClock -= Dt;
                if (_geyserClock <= 0f)
                {
                    _geyserClock = GeyserEvery;
                    var used = new List<Vec2>();
                    for (int k = 0; k < GeyserCount[lv]; k++)
                    {
                        Vec2? pick = BusiestManhole(used);
                        if (!pick.HasValue) break;
                        used.Add(pick.Value);
                        if (line)
                        {
                            // 수도관 폭발: 그 맨홀에서 몹 쪽으로 6칸 줄이 차례로 터진다.
                            Vec2 dir = CrowdDirection(pick.Value, 8f);
                            for (int j = 0; j < 6; j++)
                            {
                                var at = ClampToArena(new Vec2(pick.Value.X + (dir.X * 1.6f * j), pick.Value.Y + (dir.Y * 1.6f * j)));
                                Geysers.Add(new Geyser { Pos = at, Fuse = GeyserFuse + (0.12f * j), Radius = radius });
                            }
                        }
                        else Geysers.Add(new Geyser { Pos = pick.Value, Fuse = GeyserFuse, Radius = radius });
                    }
                }
            }
            foreach (Geyser g in Geysers)
            {
                g.Fuse -= Dt;
                if (g.Fuse > 0f || g.Burst) continue;
                g.Burst = true;
                Splash(g.Pos, g.Radius, GeyserHit, 0.6f, 10f, HitSource.Geyser);
                GeyserBursts.Add(g.Pos);
            }
            Geysers.RemoveAll(g => g.Burst);
        }

        /// <summary>소방관 GeyserSight 안 맨홀 중 2.5칸 안 몹이 가장 많은 곳(마을을 노리는 몹은 두 배). 한 마리도 없으면 null.</summary>
        private Vec2? BusiestManhole(List<Vec2> used)
        {
            Vec2? best = null;
            int bestN = 0;
            foreach (Vec2 m in Manholes)
            {
                if (m.DistanceTo(Player) > GeyserSight || used.Exists(u => u.DistanceTo(m) < 0.1f)) continue;
                Near(m, 2.5f, _near);
                int n = 0;
                foreach (Enemy e in _near) n += e.Goal != null ? 2 : 1;
                if (n > bestN)
                {
                    bestN = n;
                    best = m;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------
        // 사다리차 · 사다리 다리
        // ------------------------------------------------------------------

        private void TickLadders()
        {
            int lv = Build.PowerOf(UpgradeId.Ladder);
            bool bridge = Build.Level(UpgradeId.LadderBridge) > 0;
            if (lv > 0)
            {
                _ladderClock -= Dt;
                if (_ladderClock <= 0f)
                {
                    _ladderClock = LadderEvery;
                    float len = 6f + (lv - 1);
                    Vec2 first = CrowdDirection(Player, len);
                    double a0 = Math.Atan2(first.Y, first.X);
                    int n = LadderDirs[lv];
                    for (int k = 0; k < n; k++)
                    {
                        double a = a0 + (k * Math.PI * 2 / n);
                        Ladders.Add(new Ladder { From = Player, Dir = new Vec2((float)Math.Cos(a), (float)Math.Sin(a)), Len = len, Life = bridge ? BridgeLife : 0.6f });
                    }
                }
            }
            foreach (Ladder l in Ladders)
            {
                l.Age += Dt;
                if (!l.Struck && l.Age >= LadderReach)
                {
                    l.Struck = true;
                    LadderStrikes.Add(l);
                    Vec2 tip = l.Tip;
                    var mid = new Vec2((l.From.X + tip.X) / 2f, (l.From.Y + tip.Y) / 2f);
                    Near(mid, (l.Len / 2f) + LadderWidth, _near);
                    var side = new Vec2(-l.Dir.Y, l.Dir.X);
                    foreach (Enemy e in _near)
                    {
                        if (SegmentDistance(e.Pos, l.From, tip) > LadderWidth + e.Radius) continue;
                        float s = ((e.Pos.X - l.From.X) * side.X) + ((e.Pos.Y - l.From.Y) * side.Y) >= 0f ? 1f : -1f;
                        Damage(e, LadderHit, new Vec2(side.X * s * 6f, side.Y * s * 6f), true, HitSource.Ladder, e.Pos);
                    }
                    foreach (Structure st in Structures)
                    {
                        if (st.Collapsed || !st.IsBuilding) continue;
                        if (SegmentDistance(st.Pos, l.From, tip) > Math.Max(st.Half.X, st.Half.Y) + 0.3f) continue;
                        // 지붕 위 사다리: 갇힌 사람 한 명을 내리고(불이 꺼지기 전에) 지붕을 적신다.
                        if (st.Residents > 0 && st.Burning) RescueOne(st);
                        if (st.Burning) Soak(st, 0.8f, false);
                    }
                    Douse(mid, l.Len / 2f);
                }
                // 사다리 다리: 남아서 몹이 못 지나가게 밀어내고 지진다.
                if (bridge && l.Struck)
                {
                    Vec2 tip = l.Tip;
                    var mid = new Vec2((l.From.X + tip.X) / 2f, (l.From.Y + tip.Y) / 2f);
                    Near(mid, (l.Len / 2f) + 0.8f, _near);
                    var side = new Vec2(-l.Dir.Y, l.Dir.X);
                    foreach (Enemy e in _near)
                    {
                        if (SegmentDistance(e.Pos, l.From, tip) > 0.7f + e.Radius) continue;
                        float s = ((e.Pos.X - l.From.X) * side.X) + ((e.Pos.Y - l.From.Y) * side.Y) >= 0f ? 1f : -1f;
                        e.Knock.X += side.X * s * 30f * Dt;
                        e.Knock.Y += side.Y * s * 30f * Dt;
                        Damage(e, BridgeDps * Dt, default, false, HitSource.Ladder, e.Pos);
                    }
                }
            }
            Ladders.RemoveAll(l => l.Age >= l.Life);
        }

        // ------------------------------------------------------------------
        // 호스 채찍 · 물 회오리
        // ------------------------------------------------------------------

        /// <summary>채찍 반경(레벨마다 +0.35칸, Lv5 3.6칸).</summary>
        public float WhipRadius
        {
            get { return 2.2f + (0.35f * (Build.PowerOf(UpgradeId.Whip) - 1)); }
        }

        private void TickWhip()
        {
            int lv = Build.PowerOf(UpgradeId.Whip);
            if (lv == 0)
            {
                WhirlMarks.Clear();
                return;
            }
            bool whirl = Build.Level(UpgradeId.Whirl) > 0;
            WhipAngle += (float)(Math.PI * 2 * WhipTurn * Dt);
            if (WhipAngle > Math.PI * 2) WhipAngle -= (float)(Math.PI * 2);
            float r = WhipRadius;
            int arms = WhipArms[lv];
            Near(Player, r + 0.6f, _near);
            foreach (Enemy e in _near)
            {
                if (e.WhipCool > 0f) continue;
                float d = e.Pos.DistanceTo(Player);
                if (d < 0.5f) continue;
                float a = (float)Math.Atan2(e.Pos.Y - Player.Y, e.Pos.X - Player.X);
                for (int k = 0; k < arms; k++)
                {
                    float arm = WhipAngle + (float)(k * Math.PI * 2 / arms);
                    // 호 끝이 휩쓴 부채꼴(이번 틱 회전 + 여유): 끝에서 안쪽 몸통까지 맞는다.
                    if (Math.Abs(AngleDiff(a, arm)) > 0.35f) continue;
                    e.WhipCool = WhipCool;
                    Damage(e, WhipHit, Knockback(Player, e.Pos, 7f), true, HitSource.Whip, Player);
                    break;
                }
            }
            // 호 끝에 닿은 건물 불도 조금씩 꺼진다.
            for (int k = 0; k < arms; k++)
            {
                float arm = WhipAngle + (float)(k * Math.PI * 2 / arms);
                var tip = new Vec2(Player.X + ((float)Math.Cos(arm) * r), Player.Y + ((float)Math.Sin(arm) * r));
                Douse(tip, 0.5f);
                foreach (Structure st in Structures)
                {
                    if (st.Burning && st.Within(tip, 0.4f)) Soak(st, 0.5f * Dt, false);
                }
            }
            if (whirl)
            {
                _whirlMarkClock -= Dt;
                if (_whirlMarkClock <= 0f)
                {
                    _whirlMarkClock = 0.15f;
                    for (int k = 0; k < arms; k++)
                    {
                        float arm = WhipAngle + (float)(k * Math.PI * 2 / arms);
                        WhirlMarks.Add(new WhirlMark { Pos = new Vec2(Player.X + ((float)Math.Cos(arm) * r), Player.Y + ((float)Math.Sin(arm) * r)), Life = WhirlLife });
                    }
                }
            }
            foreach (WhirlMark m in WhirlMarks)
            {
                m.Life -= Dt;
                Near(m.Pos, 0.8f, _near);
                foreach (Enemy e in _near) Damage(e, WhirlDps * Dt, default, false, HitSource.Whip, m.Pos);
            }
            WhirlMarks.RemoveAll(m => m.Life <= 0f);
        }
    }
}
