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
    }

    public sealed class Puddle
    {
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
        public readonly Loadout Build = new Loadout();

        public Vec2 Player = new Vec2(ArenaSize / 2f, ArenaSize / 2f);
        public Vec2 Facing = new Vec2(1f, 0f);
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
        private int _jetQueue;
        private float _jetAngle;
        private float _droneAngle;
        private float _civilianClock = 20f;
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
            Direct();
            RebuildHash();
            MoveEnemies();
            FireWeapons();
            MoveShots();
            TickPuddles();
            TouchPlayer();
            CollectGems();
            TickCivilians();
            TickBoss();
            Sweep();

            Hp = Math.Min(MaxHp, Hp + (Build.Regen * Dt));
            if (Hp <= 0f)
            {
                Hp = 0f;
                Outcome = SOutcome.Lost;
                return;
            }
            if (Boss != null && Boss.Dead)
            {
                Outcome = SOutcome.Won;
                return;
            }

            if (Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                PendingChoices = SurvivorUpgrades.Roll(Build, ref _rng);
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
            float rate = Time < BossAt ? 3f + (32f * (float)Math.Pow(Time / BossAt, 1.5)) : 14f;
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
                int n = 20 + (_wavesDone * 10);
                for (int i = 0; i < n && Enemies.Count < MaxEnemies; i++)
                {
                    double a = (Math.PI * 2 * i) / n;
                    var at = new Vec2(Player.X + (float)(Math.Cos(a) * 11.5), Player.Y + (float)(Math.Sin(a) * 11.5));
                    Spawn(_wavesDone == 3 ? EnemyKind.Blaze : EnemyKind.Ember, ClampToArena(at));
                }
            }

            if (Boss == null && Time >= BossAt)
            {
                Boss = Spawn(EnemyKind.Boss, SpawnPoint(12f));
                JustBossArrived = true;
                _bossBurstClock = 3f;
            }
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

                float dx = Player.X - e.Pos.X;
                float dy = Player.Y - e.Pos.Y;
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
                e.Pos.X += (vx + (sx * 3f) + (e.Knock.X * heavy)) * Dt;
                e.Pos.Y += (vy + (sy * 3f) + (e.Knock.Y * heavy)) * Dt;
                e.Pos = ClampToArena(e.Pos);
                e.Knock.X *= knockDecay;
                e.Knock.Y *= knockDecay;
            }
        }

        private void FireWeapons()
        {
            float cd = Build.CooldownScale;

            int hose = Build.Level(UpgradeId.Hose);
            if (hose > 0)
            {
                _hoseClock -= Dt;
                if (_hoseClock <= 0f)
                {
                    Enemy target = Nearest(Player, 9f);
                    if (target != null)
                    {
                        _hoseClock = 0.32f * cd;
                        float baseAngle = (float)Math.Atan2(target.Pos.Y - Player.Y, target.Pos.X - Player.X);
                        float damage = 3f * (1f + (0.25f * (hose - 1)));
                        for (int k = 0; k < hose; k++)
                        {
                            float spread = (k - ((hose - 1) / 2f)) * 0.16f;
                            FireDrop(baseAngle + spread, damage, 14f, 0.3f, 2, 0.75f, ShotKind.Drop);
                        }
                    }
                }
            }

            if (Build.Level(UpgradeId.Cannon) > 0)
            {
                _jetClock -= Dt;
                if (_jetClock <= 0f && _jetQueue == 0)
                {
                    _jetClock = 2.4f * cd;
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
                    _bombClock = 2.2f * cd;
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

                if (s.Kind == ShotKind.Bomb)
                {
                    float t = Math.Min(1f, s.Age / s.Life);
                    s.Pos = new Vec2(s.From.X + ((s.Target.X - s.From.X) * t), s.From.Y + ((s.Target.Y - s.From.Y) * t));
                    if (t >= 1f)
                    {
                        s.Dead = true;
                        Explosions.Add(s.Target);
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

        private void TickCivilians()
        {
            _civilianClock -= Dt;
            if (_civilianClock <= 0f)
            {
                _civilianClock = 25f;
                Civilians.Add(new Civilian { Pos = SpawnPoint(8f + (Rand() * 4f)), Life = 20f });
            }

            foreach (Civilian c in Civilians)
            {
                c.Life -= Dt;
                if (c.Life > 0f && c.Pos.DistanceTo(Player) <= 0.9f)
                {
                    c.Life = 0f;
                    Xp += 20;
                    Hp = Math.Min(MaxHp, Hp + 20f);
                    Rescued++;
                    JustRescued = true;
                }
            }
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
            BurningGround.RemoveAll(p => p.Life <= 0f);
            Civilians.RemoveAll(c => c.Life <= 0f);
        }

        private static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }
    }
}
