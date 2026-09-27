using System;
using System.Collections.Generic;
using FireGame.Core.Sim;

namespace FireGame.Prototypes.Logic
{
    public enum SOutcome
    {
        Playing,
        Won,
        Lost,
    }

    public enum EnemyKind
    {
        Ember,
        Blaze,
        Dart,
        Boss,
    }

    public enum ShotKind
    {
        Drop,
        Bomb,
        Jet,

        /// <summary>소방 헬기가 쏟는 물. 폭탄처럼 날아가 떨어지지만 훨씬 크다.</summary>
        Heli,
    }

    public sealed class Enemy
    {
        public EnemyKind Kind;
        public Vec2 Pos;
        public Vec2 Knock;
        public float Hp;
        public float MaxHp;
        public float Speed;
        public float Radius;
        public float Touch;
        public int Xp;
        public float HitFlash;
        public float DroneCooldown;
        public float Slowed;
        public float Dot;

        /// <summary>마지막으로 발밑에 불을 남긴 뒤 걸어온 거리(큰 불만).</summary>
        public float Trail;

        /// <summary>불씨가 노리는 탈 것(없으면 소방관을 쫓는다).</summary>
        public Structure Goal;
        public float GoalClock;

        /// <summary>건물에서 튀어나온 불씨: 탈 것을 노린다. 가장자리에서 오는 불씨는 소방관을 쫓는다.</summary>
        public bool Seeker;

        /// <summary>노릴 탈 것 없이 떠돈 시간. 오래되면 사그라든다.</summary>
        public float Idle;
        public bool Dead;
    }

    public sealed class Gem
    {
        public Vec2 Pos;
        public int Value;
        public bool Pulled;
        public float Speed;
    }

    public sealed class Shot
    {
        public ShotKind Kind;
        public Vec2 Pos;
        public Vec2 From;
        public Vec2 Vel;
        public Vec2 Target;
        public float Age;
        public float Life;
        public float Damage;
        public float Radius;
        public int Pierce;
        public bool Dead;
        public List<Enemy> Struck;

        /// <summary>방수포 제트가 이미 적신 구조물(한 줄기에 한 번씩).</summary>
        public List<Structure> Soaked;
    }

    public sealed class Puddle
    {
        /// <summary>물로 꺼졌다(다시 번지지 않는다).</summary>
        public bool Out;
        public Vec2 Pos;
        public float Radius;
        public float Life;
        public float MaxLife;
    }

    public sealed class Civilian
    {
        public Vec2 Pos;
        public float Life;
    }

    /// <summary>화면용 한 틱 기록. 피해 숫자·파편을 그린다.</summary>
    public struct Hit
    {
        public Vec2 Pos;
        public float Damage;
        public bool Crit;
        public bool Killed;
        public EnemyKind Kind;
    }

    /// <summary>
    /// 시험판 C(뱀서라이크) 규칙. 60Hz 고정 스텝, 시드 Rng로 결정적이다.
    /// 소방관은 움직이기만 하고 무기는 알아서 쏜다. 불 괴물을 끄면 구슬이 떨어지고, 구슬이 모이면 카드 3장 중 하나를 고른다.
    /// 4:00에 나오는 화염 거인을 끄면 이기고, 체력이 0이 되면 진다.
    /// </summary>
    public sealed class SurvivorSim
    {
        public const float Dt = 1f / 60f;
        public const float ArenaSize = 60f;
        public const float BossAt = 240f;
        public const int MaxEnemies = 350;
        public const int MaxGems = 400;
        public const float PlayerRadius = 0.4f;
        public const float BaseSpeed = 4f;
        public const float BaseMagnet = 1.8f;
        public const float BaseMaxHp = 100f;
        public const float SpawnDistance = 17f;

        private const float CellSize = 2f;
        private const int Cells = (int)(ArenaSize / CellSize);

        public readonly List<Enemy> Enemies = new List<Enemy>();
        public readonly List<Gem> Gems = new List<Gem>();
        public readonly List<Shot> Shots = new List<Shot>();
        public readonly List<Puddle> Foam = new List<Puddle>();
        public readonly List<Puddle> BurningGround = new List<Puddle>();
        public readonly List<Civilian> Civilians = new List<Civilian>();
        public readonly List<Vec2> Drones = new List<Vec2>();

        /// <summary>구조대원 동료가 서 있는 곳. 동료가 없으면 null.</summary>
        public Vec2? Partner;

        /// <summary>지켜야 하는 동네. 생성자에서 고정 배치로 깐다.</summary>
        public readonly List<Structure> Structures = SurvivorTown.Build();
        public readonly Loadout Build = new Loadout();

        public Vec2 Player = new Vec2(ArenaSize / 2f, ArenaSize / 2f);
        public Vec2 Facing = new Vec2(1f, 0f);

        /// <summary>호스를 겨눈 방향(길이는 상관없다). 뷰·봇·테스트가 Step 전에 넣는다.</summary>
        public Vec2 Aim = new Vec2(1f, 0f);

        /// <summary>호스 손잡이를 쥐고 있는지. 쥔 동안만 물대포·방수포가 나간다.</summary>
        public bool Spraying;

        /// <summary>호스 연사 간격. 촘촘해야 끊김 없는 물줄기로 보인다.</summary>
        public const float HoseInterval = 0.1f;
        public float Time;
        public float Hp = BaseMaxHp;
        public int Level = 1;
        public int Xp;
        public int Kills;
        public int Rescued;
        public Enemy Boss;
        public SOutcome Outcome;

        /// <summary>null이 아니면 레벨업 카드를 고르는 중이다. 이때 Step은 시간을 멈춘다.</summary>
        public List<UpgradeId> PendingChoices;

        // --- 한 틱 신호(화면·소리용) ---
        public readonly List<Hit> Hits = new List<Hit>();
        public readonly List<Vec2> Explosions = new List<Vec2>();

        /// <summary>이번 틱에 물·폭탄·거품에 꺼진 바닥 불 자리.</summary>
        public readonly List<Vec2> Extinguished = new List<Vec2>();

        /// <summary>이번 틱에 끄지 않은 바닥 불에서 새 불씨가 일어난 자리(불이 번졌다).</summary>
        public readonly List<Vec2> Reignited = new List<Vec2>();

        /// <summary>큰 불이 이만큼 걸을 때마다 발밑에 불을 남긴다.</summary>
        public const float TrailStep = 1.5f;
        public const int MaxBurningGround = 60;

        /// <summary>끄지 않은 바닥 불이 수명을 다했을 때 불씨로 다시 일어날 확률.</summary>
        public const float ReigniteChance = 0.5f;

        /// <summary>물 1 피해가 건물 불 세기를 줄이는 양. 물대포 Lv1이면 다 탄 가게를 약 3초에 끈다.</summary>
        public const float WaterPerDamage = 0.035f;
        public const float FireGrowth = 0.04f;
        public const float WetTime = 10f;

        /// <summary>불 세기 1로 이만큼 타면 건물이 무너진다(초). 나무·차는 BurnSmall.</summary>
        public const float BurnBuilding = 40f;
        public const float BurnSmall = 20f;
        public const float EmberSight = 9f;
        public const float SpreadAt = 0.4f;

        /// <summary>false면 신고(가게 점화)를 하지 않는다. 테스트가 끈다.</summary>
        public bool Reports = true;
        public int HousesLost;
        public int CiviliansLost;

        /// <summary>건물이 절반 넘게 무너져서 졌다.</summary>
        public bool LostTown;

        /// <summary>이긴 판의 별(1~3). 지면 0.</summary>
        public int Stars;

        /// <summary>이 시각마다 안 탄 가게 하나에 불이 난다(신고). 같은 시각이 둘이면 동시에 두 곳.</summary>
        public static readonly float[] ReportTimes = { 10f, 30f, 50f, 70f, 90f, 110f, 120f, 120f, 140f, 160f, 180f, 180f, 200f, 215f, 230f };
        public const float GasFuse = 2.5f;
        public const float GasRadius = 3.5f;
        public const float RescueRange = 1.3f;
        public const float RescueTime = 1.2f;

        /// <summary>큰 불(이 세기 이상) 속에 갇힌 사람은 SmokeTime마다 한 명씩 잃는다.</summary>
        public const float SmokeFire = 0.6f;
        public const float SmokeTime = 15f;

        /// <summary>이번 틱에 사람을 잃은 건물(연기·무너짐).</summary>
        public readonly List<Structure> PeopleLost = new List<Structure>();

        /// <summary>이번 틱에 터진 가스통 자리.</summary>
        public readonly List<Vec2> GasBlasts = new List<Vec2>();

        /// <summary>이번 틱에 누군가를 구해 낸 건물.</summary>
        public readonly List<Structure> RescuedFrom = new List<Structure>();

        /// <summary>이번 틱에 새로 불붙은 구조물.</summary>
        public readonly List<Structure> Ignited = new List<Structure>();

        /// <summary>이번 틱에 무너진 구조물.</summary>
        public readonly List<Structure> Fell = new List<Structure>();

        /// <summary>이번 틱에 물로 완전히 꺼진 구조물.</summary>
        public readonly List<Structure> Doused = new List<Structure>();

        /// <summary>이번 틱에 헬기 물이 떨어진 자리.</summary>
        public readonly List<Vec2> HeliDrops = new List<Vec2>();

        /// <summary>이번 틱에 물의 장막이 터졌다(소방관 자리에서).</summary>
        public bool JustCurtain;
        public bool JustLeveled;
        public bool JustEvolved;
        public bool JustBossArrived;
        public bool JustRescued;
        public bool JustWave;
        public int GemsCollected;
        public int ShotsFired;
        public float PlayerHurt;

        private Rng _rng;
        private float _spawnDebt;
        private float _hoseClock;
        private float _bombClock;
        private float _foamClock;
        private float _jetClock;
        private float _heliClock;
        private float _curtainClock;
        private int _jetQueue;
        private float _jetAngle;
        private float _droneAngle;
        private int _reportsDone;
        private float _bossBurstClock;
        private int _wavesDone;

        private readonly int[] _head = new int[Cells * Cells];
        private int[] _next = new int[MaxEnemies * 2];

        public SurvivorSim(int seed)
        {
            _rng = new Rng(seed == 0 ? 1 : seed);
            Build.Add(UpgradeId.Hose);
        }

        public int XpToNext
        {
            get { return 6 + (Level * 5) + (Level * Level / 4); }
        }

        public float MaxHp
        {
            get { return BaseMaxHp + Build.MaxHpBonus; }
        }

        /// <summary>풀장비로 시작한다: 모든 아이템 최대 + 체력 가득.</summary>
        public void GiveMaxGear()
        {
            Build.MaxAll();
            Partner = new Vec2(Player.X - 1.2f, Player.Y);
            Hp = MaxHp;
        }

        /// <summary>가게 + 창고 수.</summary>
        public int HousesTotal
        {
            get
            {
                int n = 0;
                foreach (Structure s in Structures) if (s.IsBuilding) n++;
                return n;
            }
        }

        public float Magnet
        {
            get { return BaseMagnet * Build.MagnetScale; }
        }

        // ------------------------------------------------------------------
        // 진행
        // ------------------------------------------------------------------

        public void Step(float moveX, float moveY)
        {
            ClearSignals();
            if (PendingChoices != null || Outcome != SOutcome.Playing) return;

            Time += Dt;
            MovePlayer(moveX, moveY);
            BlockPlayer();
            Direct();
            RebuildHash();
            MoveEnemies();
            FireWeapons();
            MoveShots();
            TickPuddles();
            TickStructures();
            TouchPlayer();
            CollectGems();
            TickRescue();
            TickBoss();
            Sweep();

            Hp = Math.Min(MaxHp, Hp + (Build.Regen * Dt));
            if (Hp <= 0f)
            {
                Hp = 0f;
                Outcome = SOutcome.Lost;
                return;
            }
            int houses = HousesTotal;
            if (houses > 0 && HousesLost * 2 > houses)
            {
                LostTown = true;
                Outcome = SOutcome.Lost;
                return;
            }
            if (Boss != null && Boss.Dead)
            {
                Outcome = SOutcome.Won;
                // 별: 이기면 1, 건물 75% 이상 지키면 +1, 한 명도 안 잃으면 +1.
                float saved = houses > 0 ? (houses - HousesLost) / (float)houses : 1f;
                Stars = 1 + (saved >= 0.75f ? 1 : 0) + (CiviliansLost == 0 ? 1 : 0);
                return;
            }

            if (Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                PendingChoices = SurvivorUpgrades.Roll(Build, Level, ref _rng);
                JustLeveled = true;
                PushAway(Player, 4f, 3f);
            }
        }

        public void Choose(int index)
        {
            if (PendingChoices == null || index < 0 || index >= PendingChoices.Count) return;
            UpgradeId id = PendingChoices[index];
            PendingChoices = null;

            if (id == UpgradeId.Heal)
            {
                Hp = Math.Min(MaxHp, Hp + SurvivorUpgrades.HealAmount);
                return;
            }

            float before = MaxHp;
            Build.Add(id);
            Hp += MaxHp - before;
            if (id == UpgradeId.Cannon)
            {
                JustEvolved = true;
                _jetClock = 0f;
            }
            if (id == UpgradeId.Heli) _heliClock = 1f;
            if (id == UpgradeId.Curtain) _curtainClock = 0.5f;
            if (id == UpgradeId.Partner) Partner = new Vec2(Player.X - 1.2f, Player.Y);
        }

        /// <summary>테스트용: 적을 직접 놓는다.</summary>
        public Enemy Spawn(EnemyKind kind, Vec2 at)
        {
            var e = new Enemy { Kind = kind, Pos = at };
            float scale = 1f + ((Time / 120f) * (Time / 120f));
            switch (kind)
            {
                case EnemyKind.Ember: e.MaxHp = 2f * scale; e.Speed = 2.4f; e.Radius = 0.35f; e.Touch = 3f; e.Xp = 1; break;
                case EnemyKind.Blaze: e.MaxHp = 14f * scale; e.Speed = 1.5f; e.Radius = 0.6f; e.Touch = 10f; e.Xp = 5; break;
                case EnemyKind.Dart: e.MaxHp = 2f * scale; e.Speed = 4.2f; e.Radius = 0.3f; e.Touch = 3f; e.Xp = 1; break;
                case EnemyKind.Boss: e.MaxHp = 1100f; e.Speed = 1.1f; e.Radius = 1.4f; e.Touch = 35f; e.Xp = 0; break;
            }
            e.Hp = e.MaxHp;
            Enemies.Add(e);
            return e;
        }

        /// <summary>테스트용: 구슬을 직접 놓는다.</summary>
        public void DropGem(Vec2 at, int value)
        {
            if (Gems.Count >= MaxGems)
            {
                Gems[_rng.Next(Gems.Count)].Value += value;
                return;
            }
            Gems.Add(new Gem { Pos = at, Value = value });
        }

        // ------------------------------------------------------------------
        // 내부
        // ------------------------------------------------------------------

        private void ClearSignals()
        {
            Hits.Clear();
            Explosions.Clear();
            Extinguished.Clear();
            Reignited.Clear();
            Ignited.Clear();
            Fell.Clear();
            Doused.Clear();
            HeliDrops.Clear();
            JustCurtain = false;
            GasBlasts.Clear();
            RescuedFrom.Clear();
            PeopleLost.Clear();
            JustLeveled = false;
            JustEvolved = false;
            JustBossArrived = false;
            JustRescued = false;
            JustWave = false;
            GemsCollected = 0;
            ShotsFired = 0;
            PlayerHurt = 0f;
        }

        private float Rand()
        {
            return _rng.Next(10000) / 10000f;
        }

        private void MovePlayer(float mx, float my)
        {
            float len = (float)Math.Sqrt((mx * mx) + (my * my));
            if (len > 1f)
            {
                mx /= len;
                my /= len;
            }
            if (len > 0.01f) Facing = new Vec2(mx / Math.Max(len, 1f), my / Math.Max(len, 1f));
            float speed = BaseSpeed * Build.SpeedScale;
            Player.X = Clamp(Player.X + (mx * speed * Dt), 0.5f, ArenaSize - 0.5f);
            Player.Y = Clamp(Player.Y + (my * speed * Dt), 0.5f, ArenaSize - 0.5f);
        }

        /// <summary>스폰 감독: 시간이 갈수록 많이, 1·2·3분엔 포위, 4분엔 보스.</summary>
        private void Direct()
        {
            // 불은 이제 주로 건물에서 나온다. 가장자리에서 몰려오는 불은 예전(3→35)보다 훨씬 적다.
            float rate = Time < BossAt ? 1f + (7f * (float)Math.Pow(Time / BossAt, 1.5)) : 6f;
            _spawnDebt += rate * Dt;
            while (_spawnDebt >= 1f)
            {
                _spawnDebt -= 1f;
                if (Enemies.Count >= MaxEnemies) continue;
                Spawn(PickKind(), SpawnPoint(SpawnDistance));
            }

            if (_wavesDone < 3 && Time >= 60f * (_wavesDone + 1))
            {
                _wavesDone++;
                JustWave = true;
                int n = 10 + (_wavesDone * 5);
                for (int i = 0; i < n && Enemies.Count < MaxEnemies; i++)
                {
                    double a = (Math.PI * 2 * i) / n;
                    var at = new Vec2(Player.X + (float)(Math.Cos(a) * 11.5), Player.Y + (float)(Math.Sin(a) * 11.5));
                    Spawn(_wavesDone == 3 ? EnemyKind.Blaze : EnemyKind.Ember, ClampToArena(at));
                }
            }

            while (Reports && _reportsDone < ReportTimes.Length && Time >= ReportTimes[_reportsDone])
            {
                _reportsDone++;
                Report();
            }

            if (Boss == null && Time >= BossAt)
            {
                // 상한이 꽉 찼어도 거인은 나온다: 불씨 하나를 조용히 치운다.
                if (Enemies.Count >= MaxEnemies)
                {
                    Enemy spare = Enemies.Find(e => e.Kind == EnemyKind.Ember && !e.Dead);
                    if (spare != null) Enemies.Remove(spare);
                }
                // 물류창고가 확 타오르고 그 문 앞에서 거인이 나온다(창고가 없으면 가까운 곳에서).
                Structure depot = Structures.Find(x => x.Kind == StructureKind.Depot);
                Vec2 at = SpawnPoint(12f);
                if (depot != null)
                {
                    at = depot.Door;
                    if (!depot.Collapsed)
                    {
                        depot.Wet = 0f;
                        Ignite(depot, 1f);
                    }
                }
                Boss = Spawn(EnemyKind.Boss, at);
                JustBossArrived = true;
                _bossBurstClock = 3f;
            }
        }

        /// <summary>신고: 안 타고 안 무너진 가게 하나에 불을 낸다(젖어 있어도 난다).</summary>
        private void Report()
        {
            var pool = new List<Structure>();
            foreach (Structure s in Structures)
            {
                if (s.Kind == StructureKind.House && !s.Collapsed && !s.Burning) pool.Add(s);
            }
            if (pool.Count == 0) return;
            Structure pick = pool[_rng.Next(pool.Count)];
            pick.Wet = 0f;
            Ignite(pick, 0.35f);
        }

        private EnemyKind PickKind()
        {
            float r = Rand();
            float blaze = Math.Min(0.3f, 0.05f + (Time / 600f));
            float dart = Time < 60f ? 0f : 0.2f;
            if (r < blaze) return EnemyKind.Blaze;
            if (r < blaze + dart) return EnemyKind.Dart;
            return EnemyKind.Ember;
        }

        private Vec2 SpawnPoint(float distance)
        {
            double a = Rand() * Math.PI * 2;
            var at = new Vec2(Player.X + (float)(Math.Cos(a) * distance), Player.Y + (float)(Math.Sin(a) * distance));
            return ClampToArena(at);
        }

        private static Vec2 ClampToArena(Vec2 p)
        {
            return new Vec2(Clamp(p.X, 0.5f, ArenaSize - 0.5f), Clamp(p.Y, 0.5f, ArenaSize - 0.5f));
        }

        private void RebuildHash()
        {
            for (int i = 0; i < _head.Length; i++) _head[i] = -1;
            if (_next.Length < Enemies.Count) _next = new int[Enemies.Count * 2];
            for (int i = 0; i < Enemies.Count; i++)
            {
                int c = CellOf(Enemies[i].Pos);
                _next[i] = _head[c];
                _head[c] = i;
            }
        }

        private static int CellOf(Vec2 p)
        {
            int cx = Math.Min(Cells - 1, Math.Max(0, (int)(p.X / CellSize)));
            int cy = Math.Min(Cells - 1, Math.Max(0, (int)(p.Y / CellSize)));
            return (cy * Cells) + cx;
        }

        /// <summary>반경 안 적(해시 기준, 지난 이동 전 칸)을 모은다.</summary>
        private void Near(Vec2 p, float radius, List<Enemy> into)
        {
            into.Clear();
            int x0 = Math.Max(0, (int)((p.X - radius) / CellSize));
            int x1 = Math.Min(Cells - 1, (int)((p.X + radius) / CellSize));
            int y0 = Math.Max(0, (int)((p.Y - radius) / CellSize));
            int y1 = Math.Min(Cells - 1, (int)((p.Y + radius) / CellSize));
            for (int cy = y0; cy <= y1; cy++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    for (int i = _head[(cy * Cells) + cx]; i >= 0; i = _next[i])
                    {
                        Enemy e = Enemies[i];
                        if (!e.Dead && e.Pos.DistanceTo(p) <= radius + e.Radius) into.Add(e);
                    }
                }
            }
        }

        private readonly List<Enemy> _near = new List<Enemy>();

        private void MoveEnemies()
        {
            float knockDecay = (float)Math.Exp(-8f * Dt);
            for (int i = 0; i < Enemies.Count; i++)
            {
                Enemy e = Enemies[i];
                if (e.Dead) continue;
                if (e.HitFlash > 0f) e.HitFlash -= Dt;
                if (e.DroneCooldown > 0f) e.DroneCooldown -= Dt;
                if (e.Slowed > 0f) e.Slowed -= Dt;

                Vec2 chase = Player;
                if (e.Seeker)
                {
                    // 건물에서 나온 불씨는 가까운 탈 것을 노린다. 없으면 소방관을 쫓는다.
                    e.GoalClock -= Dt;
                    if (e.Goal != null && !e.Goal.Flammable) e.Goal = null;
                    if (e.Goal == null && e.GoalClock <= 0f)
                    {
                        e.GoalClock = 0.5f;
                        e.Goal = NearestFlammable(e.Pos, EmberSight);
                    }
                    if (e.Goal != null) chase = e.Goal.Pos;
                    else
                    {
                        // 탈 것을 못 찾은 불씨는 소방관 쪽으로 굴러가다 4초 뒤 사그라든다(구슬 없음).
                        e.Idle += Dt;
                        if (e.Idle >= 4f)
                        {
                            e.Dead = true;
                            continue;
                        }
                    }
                }
                float dx = chase.X - e.Pos.X;
                float dy = chase.Y - e.Pos.Y;
                float d = (float)Math.Sqrt((dx * dx) + (dy * dy));
                float speed = e.Speed * (e.Slowed > 0f ? 0.5f : 1f);
                float vx = d > 0.01f ? dx / d * speed : 0f;
                float vy = d > 0.01f ? dy / d * speed : 0f;

                // 서로 겹치지 않게 살짝 민다(떼가 덩어리가 아니라 무리로 보이게).
                float sx = 0f;
                float sy = 0f;
                int c = CellOf(e.Pos);
                int ccx = c % Cells;
                int ccy = c / Cells;
                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int nx = ccx + ox;
                        int ny = ccy + oy;
                        if (nx < 0 || ny < 0 || nx >= Cells || ny >= Cells) continue;
                        for (int j = _head[(ny * Cells) + nx]; j >= 0; j = _next[j])
                        {
                            if (j == i) continue;
                            Enemy o = Enemies[j];
                            float ex = e.Pos.X - o.Pos.X;
                            float ey = e.Pos.Y - o.Pos.Y;
                            float min = e.Radius + o.Radius;
                            float d2 = (ex * ex) + (ey * ey);
                            if (d2 >= min * min || d2 < 0.0001f) continue;
                            float dd = (float)Math.Sqrt(d2);
                            float push = (min - dd) / min;
                            sx += ex / dd * push;
                            sy += ey / dd * push;
                        }
                    }
                }

                float heavy = e.Kind == EnemyKind.Boss ? 0.1f : 1f;
                float stepX = (vx + (sx * 3f) + (e.Knock.X * heavy)) * Dt;
                float stepY = (vy + (sy * 3f) + (e.Knock.Y * heavy)) * Dt;
                e.Pos.X += stepX;
                e.Pos.Y += stepY;

                // 큰 불은 걸어온 자리에 불을 흘린다.
                if (e.Kind == EnemyKind.Blaze || e.Kind == EnemyKind.Boss)
                {
                    e.Trail += (float)Math.Sqrt((stepX * stepX) + (stepY * stepY));
                    if (e.Trail >= TrailStep)
                    {
                        e.Trail = 0f;
                        if (BurningGround.Count < MaxBurningGround)
                        {
                            float r = e.Kind == EnemyKind.Boss ? 1.1f : 0.6f;
                            BurningGround.Add(new Puddle { Pos = e.Pos, Radius = r, Life = 4f, MaxLife = 4f });
                        }
                    }
                }
                e.Pos = ClampToArena(e.Pos);
                // 건물에서 나온 불씨와 거인만 옮겨붙인다. 소방관을 쫓는 불(가장자리 불씨·큰 불)은 발밑에 불을 흘릴 뿐이다.
                if (e.Seeker || e.Kind == EnemyKind.Boss) TouchStructures(e);
                e.Knock.X *= knockDecay;
                e.Knock.Y *= knockDecay;
            }
        }

        private void FireWeapons()
        {
            // 물대포: 겨눈 쪽으로, 쥐고 있을 때만. 예전 자동 조준(0.32초)과 초당 피해를 맞췄다.
            int hose = Build.Level(UpgradeId.Hose);
            bool cannon = Build.Level(UpgradeId.Cannon) > 0;
            _hoseClock -= Dt;
            if (Spraying && (hose > 0 || cannon) && _hoseClock <= 0f && (Aim.X != 0f || Aim.Y != 0f))
            {
                _hoseClock = HoseInterval;
                float baseAngle = (float)Math.Atan2(Aim.Y, Aim.X);
                if (cannon)
                {
                    // 진화 후: 한 줄기로 모든 불을 꿰뚫는 고압 제트.
                    FireDrop(baseAngle, 13.2f * (HoseInterval / 0.4f), 18f, 0.55f, 999, 0.6f, ShotKind.Jet);
                }
                else
                {
                    // 늘 한 줄기. 레벨이 오를수록 굵고(반경) 세고(피해) 멀리(수명) 나가며 더 많이 꿰뚫는다.
                    float damage = 3f * (HoseInterval / 0.32f) * HosePower(hose) * Build.HosePower;
                    FireDrop(baseAngle, damage, 16f, HoseRadius(hose), 1 + hose, 0.6f * (1f + (0.1f * (hose - 1))) * Build.HoseRange, ShotKind.Drop);
                }
            }

            if (Build.Level(UpgradeId.Cannon) > 0)
            {
                _jetClock -= Dt;
                if (_jetClock <= 0f && _jetQueue == 0)
                {
                    _jetClock = 2.4f;
                    _jetQueue = 18;
                }
                if (_jetQueue > 0)
                {
                    // 한 틱에 한 줄기씩, 18줄기로 한 바퀴를 휩쓴다.
                    _jetAngle += (float)(Math.PI * 2 / 18);
                    _jetQueue--;
                    FireDrop(_jetAngle, 2.2f * 2f * 3f, 16f, 0.55f, 999, 0.8f, ShotKind.Jet);
                }
            }

            int bomb = Build.Level(UpgradeId.WaterBomb);
            if (bomb > 0)
            {
                _bombClock -= Dt;
                if (_bombClock <= 0f)
                {
                    _bombClock = 2.2f;
                    float radius = Build.BombRadius;
                    for (int k = 0; k < bomb; k++)
                    {
                        Vec2 target = RandomEnemyNear(10f) ?? new Vec2(Player.X + ((Rand() - 0.5f) * 8f), Player.Y + ((Rand() - 0.5f) * 8f));
                        Shots.Add(new Shot { Kind = ShotKind.Bomb, From = Player, Pos = Player, Target = target, Life = 0.5f, Damage = 9f, Radius = radius });
                        ShotsFired++;
                    }
                }
            }

            int drones = Build.Level(UpgradeId.Drone);
            Drones.Clear();
            if (drones > 0)
            {
                _droneAngle += 3f * Dt;
                for (int k = 0; k < drones; k++)
                {
                    double a = _droneAngle + (Math.PI * 2 * k / drones);
                    var at = new Vec2(Player.X + (float)(Math.Cos(a) * 2.3), Player.Y + (float)(Math.Sin(a) * 2.3));
                    Drones.Add(at);
                    Near(at, 0.5f, _near);
                    foreach (Enemy e in _near)
                    {
                        if (e.DroneCooldown > 0f) continue;
                        e.DroneCooldown = 0.5f;
                        Damage(e, 6f, Knockback(at, e.Pos, 3f), true);
                    }
                }
            }

            int foam = Build.Level(UpgradeId.Foam);
            if (foam > 0)
            {
                _foamClock -= Dt;
                if (_foamClock <= 0f)
                {
                    _foamClock = 0.3f;
                    float life = 2f + (foam - 1);
                    Foam.Add(new Puddle { Pos = Player, Radius = 1f, Life = life, MaxLife = life });
                }
            }

            if (Build.Level(UpgradeId.Heli) > 0)
            {
                _heliClock -= Dt;
                if (_heliClock <= 0f)
                {
                    _heliClock = HeliInterval;
                    Vec2 target = HeliTarget();
                    // 헬기는 소방관 뒤쪽 화면 밖에서 날아온다(From은 그림용).
                    var from = new Vec2(target.X - 14f, target.Y + 10f);
                    Shots.Add(new Shot { Kind = ShotKind.Heli, From = from, Pos = from, Target = target, Life = HeliFlight, Damage = 30f, Radius = HeliRadius });
                }
            }

            if (Build.Level(UpgradeId.Curtain) > 0)
            {
                _curtainClock -= Dt;
                if (_curtainClock <= 0f)
                {
                    _curtainClock = CurtainInterval;
                    JustCurtain = true;
                    Douse(Player, CurtainRadius);
                    foreach (Structure st in Structures)
                    {
                        if (st.Within(Player, CurtainRadius)) Soak(st, 0.5f);
                    }
                    Near(Player, CurtainRadius, _near);
                    foreach (Enemy e in _near) Damage(e, 12f, Knockback(Player, e.Pos, 8f), true);
                }
            }
        }

        /// <summary>물대포 레벨별 위력 배수: Lv5면 2.8배(예전 다섯 줄기의 총량과 비슷).</summary>
        public static float HosePower(int level)
        {
            return 1f + (0.45f * (level - 1));
        }

        /// <summary>물대포 레벨별 물줄기 반경(굵기).</summary>
        public static float HoseRadius(int level)
        {
            return 0.3f + (0.12f * (level - 1));
        }

        public const float HeliInterval = 9f;
        public const float HeliFlight = 1.2f;
        public const float HeliRadius = 4.5f;
        public const float CurtainInterval = 5f;
        public const float CurtainRadius = 5f;
        public const float PartnerSpeed = 4.5f;

        /// <summary>12칸 안에서 가장 크게 타는 건물 → 불이 몰린 곳 → 소방관 앞.</summary>
        private Vec2 HeliTarget()
        {
            Structure best = null;
            foreach (Structure st in Structures)
            {
                if (!st.Burning || st.Kind == StructureKind.Gas || st.DistanceTo(Player) > 12f) continue;
                if (best == null || st.Fire > best.Fire) best = st;
            }
            if (best != null) return best.Pos;
            return RandomEnemyNear(12f) ?? new Vec2(Player.X + (Facing.X * 6f), Player.Y + (Facing.Y * 6f));
        }

        private void FireDrop(float angle, float damage, float speed, float radius, int pierce, float life, ShotKind kind)
        {
            var vel = new Vec2((float)Math.Cos(angle) * speed, (float)Math.Sin(angle) * speed);
            Shots.Add(new Shot
            {
                Kind = kind,
                From = Player,
                Pos = Player,
                Vel = vel,
                Life = life,
                Damage = damage,
                Radius = radius,
                Pierce = pierce,
                Struck = pierce > 1 ? new List<Enemy>() : null,
            });
            ShotsFired++;
        }

        private void MoveShots()
        {
            foreach (Shot s in Shots)
            {
                if (s.Dead) continue;
                s.Age += Dt;

                if (s.Kind == ShotKind.Bomb || s.Kind == ShotKind.Heli)
                {
                    float t = Math.Min(1f, s.Age / s.Life);
                    s.Pos = new Vec2(s.From.X + ((s.Target.X - s.From.X) * t), s.From.Y + ((s.Target.Y - s.From.Y) * t));
                    if (t >= 1f)
                    {
                        s.Dead = true;
                        (s.Kind == ShotKind.Heli ? HeliDrops : Explosions).Add(s.Target);
                        Douse(s.Target, s.Radius);
                        foreach (Structure st in Structures)
                        {
                            if (st.Within(s.Target, s.Radius)) Soak(st, s.Damage * WaterPerDamage);
                        }
                        Near(s.Target, s.Radius, _near);
                        foreach (Enemy e in _near) Damage(e, s.Damage, Knockback(s.Target, e.Pos, 7f), true);
                    }
                    continue;
                }

                s.Pos.X += s.Vel.X * Dt;
                s.Pos.Y += s.Vel.Y * Dt;
                if (s.Age >= s.Life)
                {
                    s.Dead = true;
                    continue;
                }

                Douse(s.Pos, s.Radius);
                if (SoakStructures(s)) continue;
                Near(s.Pos, s.Radius, _near);
                foreach (Enemy e in _near)
                {
                    if (s.Struck != null)
                    {
                        if (s.Struck.Contains(e)) continue;
                        s.Struck.Add(e);
                    }
                    var dir = new Vec2(s.Vel.X / 14f, s.Vel.Y / 14f);
                    Damage(e, s.Damage, new Vec2(dir.X * 2.5f, dir.Y * 2.5f), true);
                    s.Pierce--;
                    if (s.Pierce <= 0)
                    {
                        s.Dead = true;
                        break;
                    }
                }
            }
        }

        private void TickPuddles()
        {
            foreach (Puddle p in Foam)
            {
                p.Life -= Dt;
                Douse(p.Pos, p.Radius);
                foreach (Structure st in Structures)
                {
                    if (st.Within(p.Pos, p.Radius)) Soak(st, 0.15f * Dt);
                }
                Near(p.Pos, p.Radius, _near);
                foreach (Enemy e in _near)
                {
                    e.Slowed = 0.2f;
                    e.Dot += 5f * Dt;
                    if (e.Dot >= 1.5f)
                    {
                        Damage(e, e.Dot, default, true);
                        e.Dot = 0f;
                    }
                }
            }

            foreach (Puddle p in BurningGround)
            {
                p.Life -= Dt;
                if (p.Pos.DistanceTo(Player) <= p.Radius + PlayerRadius) Hurt(10f * Dt);
            }
        }

        // ------------------------------------------------------------------
        // 동네
        // ------------------------------------------------------------------

        /// <summary>구조물에 불을 붙인다(젖었거나 무너졌으면 안 붙는다). 새로 붙었으면 true.</summary>
        public bool Ignite(Structure s, float amount)
        {
            if (s.Collapsed || s.Wet > 0f) return false;
            bool fresh = s.Fire <= 0f;
            s.Fire = Math.Min(1f, Math.Max(s.Fire, amount));
            if (fresh)
            {
                Ignited.Add(s);
                s.SpitClock = 2f;
                s.BlazeClock = 6f;
                s.RescueHold = 0f;
                if (s.Kind == StructureKind.Gas) s.Fuse = GasFuse;
            }
            return fresh;
        }

        /// <summary>물을 붓는다: 타면 불 세기를 줄이고, 다 꺼지거나 안 타면 한동안 젖는다.</summary>
        private void Soak(Structure s, float water)
        {
            if (s.Collapsed) return;
            if (s.Burning)
            {
                s.Fire -= water;
                if (s.Fire > 0f) return;
                s.Fire = 0f;
                s.Fuse = -1f;
                Doused.Add(s);
                // 불을 끈 보상: 건물은 큰 구슬, 작은 것은 작은 구슬.
                DropGem(s.Door, s.IsBuilding ? 8 : 3);
            }
            s.Wet = WetTime;
        }

        /// <summary>물줄기가 구조물에 닿았는지. 물대포 물방울은 막혀서 사라지면 true, 제트는 뚫고 간다.</summary>
        private bool SoakStructures(Shot s)
        {
            foreach (Structure st in Structures)
            {
                if (st.Collapsed || !st.Within(s.Pos, s.Radius * 0.5f)) continue;
                // 제트는 뚫고 가고, 나무는 물이 잎 사이로 빠진다(나무 밑에서 쏴도 막히지 않게).
                if (s.Kind == ShotKind.Jet || st.Kind == StructureKind.Tree)
                {
                    if (s.Soaked == null) s.Soaked = new List<Structure>();
                    if (s.Soaked.Contains(st)) continue;
                    s.Soaked.Add(st);
                    Soak(st, s.Damage * WaterPerDamage);
                    continue;
                }
                Soak(st, s.Damage * WaterPerDamage);
                s.Dead = true;
                return true;
            }
            return false;
        }

        /// <summary>불이 탈 것에 닿으면 옮겨붙는다. 불씨는 불을 옮기고 사라진다(젖은 곳에 닿아도 꺼진다).</summary>
        private void TouchStructures(Enemy e)
        {
            foreach (Structure st in Structures)
            {
                if (st.Collapsed || st.Burning) continue;
                if (!st.Within(e.Pos, e.Radius)) continue;
                Ignite(st, 0.15f);
                if (e.Kind == EnemyKind.Ember)
                {
                    e.Dead = true;
                    return;
                }
            }
        }

        private Structure NearestFlammable(Vec2 p, float range)
        {
            Structure best = null;
            float bestD = range;
            foreach (Structure s in Structures)
            {
                if (!s.Flammable) continue;
                float d = s.DistanceTo(p);
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }
            return best;
        }

        /// <summary>건물·차·가스통은 소방관이 지나갈 수 없다(나무 밑은 지나간다).</summary>
        private void BlockPlayer()
        {
            foreach (Structure s in Structures)
            {
                if (s.Collapsed || s.Kind == StructureKind.Tree) continue;
                float dx = Player.X - s.Pos.X;
                float dy = Player.Y - s.Pos.Y;
                float ox = s.Half.X + PlayerRadius - Math.Abs(dx);
                float oy = s.Half.Y + PlayerRadius - Math.Abs(dy);
                if (ox <= 0f || oy <= 0f) continue;
                if (ox < oy) Player.X += dx >= 0f ? ox : -ox;
                else Player.Y += dy >= 0f ? oy : -oy;
            }
        }

        /// <summary>타는 구조물이 커지고, 불씨·큰 불을 뱉고, 다 타면 무너진다.</summary>
        private void TickStructures()
        {
            foreach (Structure s in Structures)
            {
                if (s.Collapsed) continue;
                if (s.Wet > 0f) s.Wet -= Dt;
                if (!s.Burning) continue;

                if (s.Kind == StructureKind.Gas && s.Fuse >= 0f)
                {
                    s.Fuse -= Dt;
                    if (s.Fuse <= 0f)
                    {
                        Blow(s);
                        continue;
                    }
                }

                s.Fire = Math.Min(1f, s.Fire + (FireGrowth * Dt));
                s.Integrity -= s.Fire * Dt / (s.IsBuilding ? BurnBuilding : BurnSmall);
                if (s.Integrity <= 0f)
                {
                    Fall(s);
                    continue;
                }

                // 막 붙은 작은 불은 아직 번지지 않는다(0.4까지 약 5초) — 일찍 잡으면 막을 수 있다.
                if (s.Fire >= SpreadAt) s.SpitClock -= Dt;
                if (s.SpitClock <= 0f)
                {
                    s.SpitClock = (9f - (5f * s.Fire)) * (s.IsBuilding ? 1f : 2f);
                    SpitEmber(s, 3f);
                }
                if (s.IsBuilding && s.Fire >= 0.8f)
                {
                    s.BlazeClock -= Dt;
                    if (s.BlazeClock <= 0f && Enemies.Count < MaxEnemies)
                    {
                        s.BlazeClock = 16f;
                        Spawn(EnemyKind.Blaze, EdgePoint(s, 0.8f));
                    }
                }
            }
        }

        /// <summary>가스통 폭발: 둘레 탈 것에 불을 크게 붙이고, 불씨를 튀기고, 가까우면 소방관도 다친다.</summary>
        private void Blow(Structure gas)
        {
            gas.Collapsed = true;
            gas.Fire = 0f;
            gas.Integrity = 0f;
            gas.Fuse = -1f;
            GasBlasts.Add(gas.Pos);
            foreach (Structure st in Structures)
            {
                if (st == gas || st.Collapsed || !st.Within(gas.Pos, GasRadius)) continue;
                if (st.Burning) st.Fire = Math.Min(1f, st.Fire + 0.5f);
                else Ignite(st, 0.5f);
            }
            for (int k = 0; k < 8; k++) SpitEmber(gas, 9f);
            if (Player.DistanceTo(gas.Pos) <= GasRadius) Hurt(25f);
        }

        private void Fall(Structure s)
        {
            s.Collapsed = true;
            s.Fire = 0f;
            s.Integrity = 0f;
            s.Fuse = -1f;
            Fell.Add(s);
            if (s.IsBuilding)
            {
                HousesLost++;
                if (s.Residents > 0) PeopleLost.Add(s);
                CiviliansLost += s.Residents;
                s.Residents = 0;
            }
            for (int k = 0; k < 6; k++) SpitEmber(s, 6f);
        }

        /// <summary>구조물 가장자리 바깥에서 불씨 하나를 튕겨 낸다.</summary>
        private void SpitEmber(Structure s, float kick)
        {
            if (Enemies.Count >= MaxEnemies) return;
            Vec2 at = EdgePoint(s, 0.5f);
            Enemy e = Spawn(EnemyKind.Ember, at);
            Vec2 k = Knockback(s.Pos, at, kick);
            e.Knock = k;
            e.GoalClock = 0.4f;
            e.Seeker = true;
        }

        private Vec2 EdgePoint(Structure s, float margin)
        {
            double a = Rand() * Math.PI * 2;
            float cx = (float)Math.Cos(a);
            float cy = (float)Math.Sin(a);
            float scale = Math.Min((s.Half.X + margin) / Math.Max(Math.Abs(cx), 0.001f), (s.Half.Y + margin) / Math.Max(Math.Abs(cy), 0.001f));
            return ClampToArena(new Vec2(s.Pos.X + (cx * scale), s.Pos.Y + (cy * scale)));
        }

        private void TouchPlayer()
        {
            Near(Player, PlayerRadius, _near);
            foreach (Enemy e in _near) Hurt(e.Touch * Dt);
        }

        private void Hurt(float amount)
        {
            Hp -= amount;
            PlayerHurt += amount;
        }

        private void CollectGems()
        {
            float magnet = Magnet;
            foreach (Gem g in Gems)
            {
                float d = g.Pos.DistanceTo(Player);
                if (!g.Pulled && d <= magnet)
                {
                    g.Pulled = true;
                    g.Speed = 3f;
                }
                if (!g.Pulled) continue;

                g.Speed += 40f * Dt;
                float step = Math.Min(d, g.Speed * Dt);
                if (d > 0.001f)
                {
                    g.Pos.X += (Player.X - g.Pos.X) / d * step;
                    g.Pos.Y += (Player.Y - g.Pos.Y) / d * step;
                }
                if (g.Pos.DistanceTo(Player) <= 0.45f)
                {
                    Xp += g.Value;
                    g.Value = 0;
                    GemsCollected++;
                }
            }
        }

        /// <summary>
        /// 불난 가게 문 앞에 잠깐 서 있으면 갇힌 사람을 한 명씩 데리고 나온다.
        /// 나온 사람은 잠깐 뛰어 나가는 모습으로만 남는다(Civilians는 화면용).
        /// </summary>
        /// <summary>동료: 갇힌 사람이 있는 가장 가까운 불난 가게 문으로 달려간다. 없으면 소방관 곁을 따른다.</summary>
        private void MovePartner()
        {
            if (!Partner.HasValue) return;
            Vec2 at = Partner.Value;
            Vec2 goal = new Vec2(Player.X - 1.2f, Player.Y - 0.6f);
            float best = float.MaxValue;
            foreach (Structure s in Structures)
            {
                if (!s.Burning || s.Residents <= 0) continue;
                float d = s.Door.DistanceTo(at);
                if (d < best)
                {
                    best = d;
                    goal = s.Door;
                }
            }
            float dx = goal.X - at.X;
            float dy = goal.Y - at.Y;
            float len = (float)Math.Sqrt((dx * dx) + (dy * dy));
            float step = PartnerSpeed * Dt;
            if (len > 0.2f)
            {
                at.X += dx / len * Math.Min(step, len);
                at.Y += dy / len * Math.Min(step, len);
            }
            Partner = ClampToArena(at);
        }

        private void TickRescue()
        {
            MovePartner();
            foreach (Structure s in Structures)
            {
                // 큰 불 속에 오래 갇혀 있으면 연기에 한 명씩 잃는다: 멀리서 끄기만 할 게 아니라 빨리 가야 한다.
                if (s.Burning && s.Residents > 0 && s.Fire >= SmokeFire)
                {
                    s.Smoke += Dt;
                    if (s.Smoke >= SmokeTime)
                    {
                        s.Smoke = 0f;
                        s.Residents--;
                        CiviliansLost++;
                        PeopleLost.Add(s);
                    }
                }

                bool atDoor = s.Door.DistanceTo(Player) <= RescueRange || (Partner.HasValue && s.Door.DistanceTo(Partner.Value) <= RescueRange);
                if (!s.Burning || s.Residents <= 0 || !atDoor)
                {
                    s.RescueHold = 0f;
                    continue;
                }
                s.RescueHold += Dt;
                if (s.RescueHold < RescueTime) continue;
                s.RescueHold = 0f;
                s.Residents--;
                Rescued++;
                Xp += 20;
                Hp = Math.Min(MaxHp, Hp + 20f);
                JustRescued = true;
                RescuedFrom.Add(s);
                Civilians.Add(new Civilian { Pos = s.Door, Life = 1.5f });
            }

            foreach (Civilian c in Civilians) c.Life -= Dt;
        }

        private void TickBoss()
        {
            if (Boss == null || Boss.Dead) return;
            _bossBurstClock -= Dt;
            if (_bossBurstClock > 0f) return;
            _bossBurstClock = 3f;
            for (int k = 0; k < 12 && Enemies.Count < MaxEnemies; k++)
            {
                double a = Math.PI * 2 * k / 12;
                var at = new Vec2(Boss.Pos.X + (float)(Math.Cos(a) * 1.8), Boss.Pos.Y + (float)(Math.Sin(a) * 1.8));
                Enemy e = Spawn(EnemyKind.Ember, ClampToArena(at));
                e.Knock = new Vec2((float)Math.Cos(a) * 6f, (float)Math.Sin(a) * 6f);
            }
        }

        private void Damage(Enemy e, float amount, Vec2 knock, bool show)
        {
            if (e.Dead) return;
            bool crit = Rand() < 0.1f;
            if (crit) amount *= 2f;
            e.Hp -= amount;
            e.HitFlash = 0.08f;
            e.Knock.X += knock.X;
            e.Knock.Y += knock.Y;

            bool killed = e.Hp <= 0f;
            if (killed) Kill(e);
            if (show || killed) Hits.Add(new Hit { Pos = e.Pos, Damage = amount, Crit = crit, Killed = killed, Kind = e.Kind });
        }

        private void Kill(Enemy e)
        {
            e.Dead = true;
            Kills++;
            if (e.Kind == EnemyKind.Boss) return;
            DropGem(e.Pos, e.Xp);
            if (e.Kind == EnemyKind.Blaze)
            {
                BurningGround.Add(new Puddle { Pos = e.Pos, Radius = 0.9f, Life = 3f, MaxLife = 3f });
            }
        }

        /// <summary>물이 닿은 자리의 바닥 불을 끈다(샷의 관통 수는 쓰지 않는다).</summary>
        private void Douse(Vec2 at, float radius)
        {
            foreach (Puddle p in BurningGround)
            {
                if (p.Out || p.Life <= 0f) continue;
                if (p.Pos.DistanceTo(at) > radius + p.Radius) continue;
                p.Out = true;
                p.Life = 0f;
                Extinguished.Add(p.Pos);
            }
        }

        private static Vec2 Knockback(Vec2 from, Vec2 to, float strength)
        {
            float dx = to.X - from.X;
            float dy = to.Y - from.Y;
            float d = (float)Math.Sqrt((dx * dx) + (dy * dy));
            if (d < 0.01f) return default;
            return new Vec2(dx / d * strength, dy / d * strength);
        }

        private void PushAway(Vec2 from, float radius, float strength)
        {
            foreach (Enemy e in Enemies)
            {
                if (e.Dead || e.Kind == EnemyKind.Boss) continue;
                float d = e.Pos.DistanceTo(from);
                if (d > radius) continue;
                Vec2 k = Knockback(from, e.Pos, strength * 4f);
                e.Knock.X += k.X;
                e.Knock.Y += k.Y;
            }
        }

        private Enemy Nearest(Vec2 p, float range)
        {
            Enemy best = null;
            float bestD = range;
            foreach (Enemy e in Enemies)
            {
                if (e.Dead) continue;
                float d = e.Pos.DistanceTo(p);
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
        }

        private Vec2? RandomEnemyNear(float range)
        {
            int seen = 0;
            Vec2? pick = null;
            foreach (Enemy e in Enemies)
            {
                if (e.Dead || e.Pos.DistanceTo(Player) > range) continue;
                seen++;
                if (_rng.Next(seen) == 0) pick = e.Pos;
            }
            return pick;
        }

        private void Sweep()
        {
            Enemies.RemoveAll(e => e.Dead);
            Shots.RemoveAll(s => s.Dead);
            Gems.RemoveAll(g => g.Value == 0);
            Foam.RemoveAll(p => p.Life <= 0f);
            // 끄지 않은 채 다 탄 바닥 불은 확률로 새 불씨를 일으킨다(불이 번진다). 물로 끈 자리는 Out이라 번지지 않는다.
            foreach (Puddle p in BurningGround)
            {
                if (p.Life > 0f || p.Out) continue;
                if (Rand() < ReigniteChance && Enemies.Count < MaxEnemies)
                {
                    Spawn(EnemyKind.Ember, p.Pos);
                    Reignited.Add(p.Pos);
                }
            }
            BurningGround.RemoveAll(p => p.Life <= 0f);
            Civilians.RemoveAll(c => c.Life <= 0f);
        }

        private static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }
    }
}
