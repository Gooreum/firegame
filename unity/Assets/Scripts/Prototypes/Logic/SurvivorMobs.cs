using System;
using System.Collections.Generic;

namespace FireGame.Prototypes.Logic
{
    /// <summary>습격: 마을 가장자리 한 곳에서 불곰·불쥐 줄·도깨비가 표적 집들로 몰려온다(링 대신, 강제 없음).</summary>
    public sealed class Raid
    {
        public Vec2 From;
        public readonly List<Structure> Targets = new List<Structure>();
        public float At;
    }

    /// <summary>
    /// 마을(수호자)의 몹(2026-10-07): 나보다 마을을 노린다. 종마다 마을에 하는 짓과 실루엣이 다르다.
    /// 불쥐 — 떼로 한 줄, 집에 닿으면 불. 도깨비 — 집 앞에 멈춰 예고 뒤 지붕에 횃불(맞으면 끊긴다).
    /// 불풍선 — 떠서 집 위로, 퓨즈 뒤 터져 둘레 집 여럿에 불. 불곰(엘리트) — 집들을 밀고 지나가며 불, 잡으면 보물상자.
    /// 화마(보스) — 3:00 랜드마크에서 일어나 마을 한가운데로, 걸음마다 불쥐를 쏟는다.
    /// </summary>
    public sealed partial class SurvivorSim
    {
        // --- 수치(조정 전: docs §21) ---
        public const float RatFrom = 25f;
        public const float RatEveryStart = 14f;
        public const float RatEveryEnd = 8f;
        public const int RatLineStart = 6;
        public const int RatLineEnd = 9;
        public const float RatGap = 0.45f;
        public const float RatIgnite = 0.12f;

        public const float GoblinFrom = 55f;
        public const float GoblinShareMax = 0.15f;
        public const float GoblinReach = 2.5f;
        public const float GoblinWindup = 1.4f;
        public const float GoblinIgnite = 0.35f;

        public const float FireBalloonFrom = 95f;
        public const float FireBalloonEvery = 18f;
        public const float FireBalloonFuse = 1.2f;
        public const float FireBalloonBlast = 4f;
        public const float FireBalloonHurtRange = 3f;
        public const float FireBalloonHurt = 12f;
        public const float FireBalloonIgnite = 0.3f;

        public const float BearIgnite = 0.3f;
        public const float BearBurnCool = 2f;

        public const float BossHp = 900f;
        public const float BossRatEvery = 3f;
        public const int BossRatLine = 5;
        public const int BossGems = 12;

        // --- 레벨 단계(2026-10-10, docs §25): 시간이 아니라 내 레벨이 몹을 부른다. 봇은 3:00에 Lv 19~21이라 단계 3이 2:40쯤. ---
        public const int TierLv1 = 6;
        public const int TierLv2 = 12;
        public const int TierLv3 = 18;

        /// <summary>단계 2부터 (Tier − 1) × 이만큼의 확률로 엘리트(체력 3배)로 나온다.</summary>
        public const float EliteChance = 0.15f;

        /// <summary>마을 아닌 스테이지의 손님 몹: 단계 2부터 가장자리 스폰의 도깨비 몫, 단계별 불쥐 줄·불풍선·불곰 간격(초).</summary>
        public const float GuestGoblinShare = 0.1f;
        public const float GuestRatEvery = 22f;
        public const float GuestBalloonEvery = 30f;
        public const float GuestBearEvery = 50f;

        /// <summary>몹 단계: 0 = Lv1~5, 1 = Lv6~(불쥐 줄), 2 = Lv12~(도깨비·불풍선·엘리트), 3 = Lv18~(불곰, 엘리트 2배).</summary>
        public int Tier
        {
            get { return Level >= TierLv3 ? 3 : Level >= TierLv2 ? 2 : Level >= TierLv1 ? 1 : 0; }
        }

        /// <summary>한 틱 신호: 단계가 올랐다(뷰가 띠로 알린다). TierShown은 마지막으로 알린 단계.</summary>
        public bool JustTier;
        public int TierShown;

        private float _guestRatClock;
        private float _guestBalloonClock;
        private float _guestBearClock;

        /// <summary>엘리트가 될 수 있는 종류: 작은 불씨·쥐·풍등은 빼고 몸이 있는 놈들.</summary>
        public static bool IsEliteKind(EnemyKind k)
        {
            return k == EnemyKind.Blaze || k == EnemyKind.Goblin || k == EnemyKind.Oil || k == EnemyKind.Crab || k == EnemyKind.Squirrel || k == EnemyKind.Gull || k == EnemyKind.Popper;
        }

        /// <summary>엘리트: 체력 3배·몸 1.3배·조금 느리고 경험치 4배. Heavy(잘 안 밀린다)도 켠다.</summary>
        public void MakeElite(Enemy e)
        {
            e.Elite = true;
            e.Heavy = true;
            e.MaxHp *= 3f;
            e.Hp = e.MaxHp;
            e.Radius *= 1.3f;
            e.Speed *= 0.9f;
            e.Xp *= 4;
            Stats.Elites++;
        }

        /// <summary>단계 띠 문구(뷰).</summary>
        public static string TierText(int tier)
        {
            switch (tier)
            {
                case 1: return "불쥐 떼가 온다";
                case 2: return "도깨비·불풍선이 섞인다 · 엘리트 출현";
                case 3: return "불곰이 온다";
            }
            return "";
        }

        /// <summary>마을 아닌 스테이지의 손님 몹(Direct가 부른다): 단계 1 불쥐 줄(22초, 단계만큼 길다), 단계 2 불풍선(30초), 단계 3 불곰(50초, 잡으면 상자). 도깨비는 PickKind 몫.</summary>
        private void DirectGuestMobs()
        {
            int tier = Tier;
            if (tier >= 1 && Time >= _guestRatClock)
            {
                _guestRatClock = Time + GuestRatEvery;
                Vec2 from = SpawnPoint(SpawnDistance);
                Structure goal = RaidGoal(from, null);
                if (goal != null) SpawnRatLine(from, goal, RatLineStart + tier);
            }
            if (tier >= 2 && Time >= _guestBalloonClock)
            {
                _guestBalloonClock = Time + GuestBalloonEvery;
                Structure goal = RandomUnburnt(Player, 20f);
                if (goal != null && Enemies.Count < MaxEnemies) Spawn(EnemyKind.FireBalloon, SpawnPoint(SpawnDistance)).Goal = goal;
            }
            if (tier >= 3 && Time >= _guestBearClock)
            {
                _guestBearClock = Time + GuestBearEvery;
                Vec2 from = SpawnPoint(SpawnDistance);
                Structure goal = RaidGoal(from, null);
                if (goal != null && Enemies.Count < MaxEnemies) Spawn(EnemyKind.Bear, from).Goal = goal;
            }
        }

        /// <summary>이번 판의 습격(그림·측정용). 지난 것도 남는다.</summary>
        public readonly List<Raid> Raids = new List<Raid>();

        /// <summary>화마(3:00 대화재의 보스). 없으면 null.</summary>
        public Enemy Boss;

        // --- 한 틱 신호 ---
        public Raid JustRaid;
        public bool JustBossRise;
        public bool JustBossDown;

        /// <summary>화마가 쓰러진 자리(그림용).</summary>
        public Vec2 BossDownAt;
        public readonly List<Vec2> RatLines = new List<Vec2>();

        /// <summary>도깨비가 횃불을 던진 줄(어디서 → 어느 지붕).</summary>
        public readonly List<Structure> TorchThrows = new List<Structure>();
        public readonly List<Vec2> TorchFrom = new List<Vec2>();

        /// <summary>불풍선이 터진 자리.</summary>
        public readonly List<Vec2> FireBalloonBlasts = new List<Vec2>();

        /// <summary>불곰이 쓰러진 자리(상자가 떨어진다).</summary>
        public readonly List<Vec2> BearsDown = new List<Vec2>();

        // --- 측정 ---
        public int RaidersSpawned;
        public int RaidersKilled;
        public int RaiderIgnites;
        public bool BossKilled;

        private float _ratClock = RatFrom;
        private float _fireBalloonClock = FireBalloonFrom;
        private float _bossRatClock;

        private void ClearMobSignals()
        {
            JustRaid = null;
            JustBossRise = false;
            JustBossDown = false;
            RatLines.Clear();
            TorchThrows.Clear();
            TorchFrom.Clear();
            FireBalloonBlasts.Clear();
            BearsDown.Clear();
        }

        /// <summary>마을을 노리는 몹인가(MoveRaider가 움직인다).</summary>
        public static bool IsRaider(EnemyKind kind)
        {
            return kind == EnemyKind.Rat || kind == EnemyKind.Goblin || kind == EnemyKind.FireBalloon || kind == EnemyKind.Bear || kind == EnemyKind.Hwama;
        }

        // ------------------------------------------------------------------
        // 시간표(Direct의 수호자 몫)
        // ------------------------------------------------------------------

        private void DirectTownMobs()
        {
            if (Time >= _ratClock)
            {
                float k = Clamp((Time - RatFrom) / (FinaleAt - RatFrom), 0f, 1f);
                _ratClock = Time + RatEveryStart + ((RatEveryEnd - RatEveryStart) * k);
                Vec2 from = SpawnPoint(SpawnDistance);
                Structure goal = RaidGoal(from, null);
                if (goal != null) SpawnRatLine(from, goal, RatLineStart + (int)Math.Round((RatLineEnd - RatLineStart) * k));
            }
            if (Time >= _fireBalloonClock)
            {
                _fireBalloonClock = Time + FireBalloonEvery;
                int n = Time >= 150f ? 2 : 1;
                for (int i = 0; i < n; i++)
                {
                    Structure goal = RandomUnburnt(Player, 20f);
                    if (goal == null) break;
                    Enemy b = Spawn(EnemyKind.FireBalloon, SpawnPoint(SpawnDistance));
                    b.Goal = goal;
                }
            }
        }

        /// <summary>도깨비 몫(PickKind가 쓴다): 55초부터 3:00까지 0 → GoblinShareMax.</summary>
        private float GoblinShare
        {
            get { return !Guardian || Time < GoblinFrom ? 0f : GoblinShareMax * Clamp((Time - GoblinFrom) / (FinaleAt - GoblinFrom), 0f, 1f); }
        }

        /// <summary>
        /// 습격(큰 신고 대신): 가장자리 한 곳에서 불곰 하나, 불쥐 줄 둘, 도깨비 둘이 그쪽 안 탄 집 2~3채로 온다.
        /// 막으면 보석과 상자, 못 막으면 그 집들이 탄다. 링도 봉인도 없다.
        /// </summary>
        public void StartRaid()
        {
            Vec2 from = SpawnPoint(SpawnDistance);
            var raid = new Raid { From = from, At = Time };
            Structure first = RaidGoal(from, null);
            if (first == null) return;
            raid.Targets.Add(first);
            for (int i = 0; i < 2; i++)
            {
                Structure next = RaidGoal(from, raid.Targets);
                if (next != null) raid.Targets.Add(next);
            }
            Raids.Add(raid);
            JustRaid = raid;
            Stats.Events++;
            Structure t0 = raid.Targets[0];
            Structure t1 = raid.Targets[Math.Min(1, raid.Targets.Count - 1)];
            Structure t2 = raid.Targets[raid.Targets.Count - 1];
            Enemy bear = Spawn(EnemyKind.Bear, from);
            bear.Goal = t0;
            SpawnRatLine(new Vec2(from.X + 1.2f, from.Y), t0, RatLineStart);
            SpawnRatLine(new Vec2(from.X - 1.2f, from.Y), t1, RatLineStart);
            for (int i = 0; i < 2; i++)
            {
                Enemy g = Spawn(EnemyKind.Goblin, ClampToArena(new Vec2(from.X + (i == 0 ? 0.8f : -0.8f), from.Y + 1f)));
                g.Goal = i == 0 ? t1 : t2;
            }
        }

        /// <summary>불쥐 n마리를 from에서 한 줄로: 맨 앞이 goal로 길을 잡고, 뒤는 앞 쥐를 따른다.</summary>
        public void SpawnRatLine(Vec2 from, Structure goal, int n)
        {
            Vec2 dir = Toward(goal.Pos, from);
            Enemy ahead = null;
            for (int i = 0; i < n && Enemies.Count < MaxEnemies; i++)
            {
                var at = ClampToArena(new Vec2(from.X + (dir.X * RatGap * i), from.Y + (dir.Y * RatGap * i)));
                Enemy r = Spawn(EnemyKind.Rat, at);
                r.Goal = goal;
                r.Leader = ahead;
                ahead = r;
            }
            RatLines.Add(from);
        }

        /// <summary>
        /// from에서 노릴 건물: 안 탄 집 → 강을 건너지 않는 쪽 → 가까운 순. 안 탄 집이 하나도 없으면 타는(무너지지 않은) 집이라도.
        /// skip에 든 건 빼고, 없으면 null.
        /// </summary>
        private Structure RaidGoal(Vec2 from, List<Structure> skip)
        {
            Structure best = null;
            int bestRank = -1;
            float bestD = float.MaxValue;
            foreach (Structure s in Structures)
            {
                if (!s.IsBuilding || s.Collapsed || (skip != null && skip.Contains(s))) continue;
                int rank = (s.Burning ? 0 : 2) + (CrossesWater(from, s.Pos) ? 0 : 1);
                float d = s.DistanceTo(from);
                if (rank > bestRank || (rank == bestRank && d < bestD))
                {
                    best = s;
                    bestRank = rank;
                    bestD = d;
                }
            }
            return best;
        }

        /// <summary>at 둘레 range 안 안 탄 건물 하나(무작위). 없으면 null.</summary>
        private Structure RandomUnburnt(Vec2 at, float range)
        {
            Structure pick = null;
            int seen = 0;
            foreach (Structure s in Structures)
            {
                if (!s.IsBuilding || s.Collapsed || s.Burning || s.DistanceTo(at) > range) continue;
                seen++;
                if (_rng.Next(seen) == 0) pick = s;
            }
            return pick;
        }

        // ------------------------------------------------------------------
        // 움직임(MoveEnemies가 부른다)
        // ------------------------------------------------------------------

        /// <summary>마을 몹 한 마리를 움직이고 마을에 하는 짓을 한다. 죽거나 사라졌으면 false.</summary>
        private bool MoveRaider(Enemy e)
        {
            Vec2 chase = Player;
            float speed = e.Speed * (e.Slowed > 0f ? 0.5f : 1f);
            switch (e.Kind)
            {
                case EnemyKind.Rat:
                {
                    if (e.Goal == null || e.Goal.Collapsed) e.Goal = RaidGoal(e.Pos, null);
                    if (e.Goal == null) return Fizzle(e);
                    if (e.Leader != null && !e.Leader.Dead)
                    {
                        // 앞 쥐 꼬리에 붙어 따라간다(줄이 흐트러지지 않는다).
                        chase = e.Leader.Pos;
                        if (e.Pos.DistanceTo(chase) < RatGap) speed = 0f;
                    }
                    else chase = e.Goal.Pos;
                    if (e.Goal.Within(e.Pos, e.Radius))
                    {
                        Ignite(e.Goal, RatIgnite);
                        RaiderIgnites++;
                        e.Dead = true;
                        return false;
                    }
                    break;
                }
                case EnemyKind.Goblin:
                {
                    if (e.Goal == null || e.Goal.Collapsed) e.Goal = RaidGoal(e.Pos, null);
                    if (e.Goal == null) break;
                    chase = e.Goal.Pos;
                    if (e.Goal.DistanceTo(e.Pos) <= GoblinReach)
                    {
                        // 집 앞에 멈춰 머리 불을 키운다(예고). 다 차면 지붕에 횃불을 던지고 다른 집으로.
                        speed = 0f;
                        e.Phase += Dt;
                        if (e.Phase >= GoblinWindup)
                        {
                            e.Phase = 0f;
                            Ignite(e.Goal, GoblinIgnite);
                            RaiderIgnites++;
                            TorchThrows.Add(e.Goal);
                            TorchFrom.Add(e.Pos);
                            var done = new List<Structure> { e.Goal };
                            e.Goal = RaidGoal(e.Pos, done);
                        }
                    }
                    else e.Phase = 0f;
                    break;
                }
                case EnemyKind.FireBalloon:
                {
                    if (e.Goal == null || e.Goal.Collapsed) e.Goal = RandomUnburnt(e.Pos, 30f);
                    if (e.Goal == null) return Fizzle(e);
                    chase = e.Goal.Pos;
                    if (e.Goal.Within(e.Pos, 0.3f))
                    {
                        // 지붕 위에 멈춰 퓨즈가 탄다(깜빡임은 뷰). 다 타면 터져 둘레 집 여럿에 불.
                        speed = 0f;
                        e.Phase += Dt;
                        if (e.Phase >= FireBalloonFuse)
                        {
                            BurstFireBalloon(e.Pos);
                            e.Dead = true;
                            return false;
                        }
                    }
                    break;
                }
                case EnemyKind.Bear:
                {
                    if (e.Goal == null || e.Goal.Collapsed || e.Goal.Within(e.Pos, e.Radius * 0.5f))
                    {
                        var done = e.Goal != null ? new List<Structure> { e.Goal } : null;
                        e.Goal = RaidGoal(e.Pos, done);
                    }
                    if (e.Goal != null) chase = e.Goal.Pos;
                    // 지나가는 집마다 불(같은 집은 BearBurnCool마다 한 번).
                    if (e.GoalClock > 0f) e.GoalClock -= Dt;
                    foreach (Structure st in Structures)
                    {
                        if (!st.IsBuilding || st.Collapsed || !st.Within(e.Pos, e.Radius)) continue;
                        if (st == e.LastBurnt && e.GoalClock > 0f) continue;
                        if (Ignite(st, BearIgnite) || st.Fire < BearIgnite) RaiderIgnites++;
                        e.LastBurnt = st;
                        e.GoalClock = BearBurnCool;
                    }
                    LeaveTrail(e, 0f);
                    break;
                }
                case EnemyKind.Hwama:
                {
                    // 마을 한가운데로 걷고, 닿으면 소방관을 쫓는다. 3초마다 안 탄 집 하나에 불쥐 줄을 쏟는다.
                    var center = new Vec2(ArenaSize / 2f, ArenaSize / 2f);
                    chase = e.Phase < 1f && e.Pos.DistanceTo(center) > 2f ? center : Player;
                    if (e.Pos.DistanceTo(center) <= 2f) e.Phase = 1f;
                    _bossRatClock -= Dt;
                    if (_bossRatClock <= 0f)
                    {
                        _bossRatClock = BossRatEvery;
                        Structure goal = RandomUnburnt(e.Pos, 25f);
                        if (goal != null) SpawnRatLine(e.Pos, goal, BossRatLine);
                    }
                    LeaveTrail(e, 0f);
                    break;
                }
            }

            float dx = chase.X - e.Pos.X;
            float dy = chase.Y - e.Pos.Y;
            float d = (float)Math.Sqrt((dx * dx) + (dy * dy));
            float vx = d > 0.01f ? dx / d * speed : 0f;
            float vy = d > 0.01f ? dy / d * speed : 0f;
            e.Pos.X += (vx + e.Knock.X) * Dt;
            e.Pos.Y += (vy + e.Knock.Y) * Dt;
            e.Pos = ClampToArena(e.Pos);
            // 땅을 걷는 몹은 강을 못 건넌다(다리로 돈다: RaidGoal이 같은 둑의 집을 먼저 고른다). 불풍선은 날아 건넌다.
            if (HasWater && e.Kind != EnemyKind.FireBalloon) PushOutOfWater(ref e.Pos, e.Radius);
            return true;
        }

        /// <summary>노릴 집이 없는 몹은 사그라든다(구슬 없음).</summary>
        private bool Fizzle(Enemy e)
        {
            e.Idle += Dt;
            if (e.Idle < 2f) return true;
            e.Dead = true;
            return false;
        }

        /// <summary>큰 몸이 지나간 자리에 TrailStep마다 불 바닥(몸만 덴다).</summary>
        private void LeaveTrail(Enemy e, float unused)
        {
            float step = e.Speed * Dt;
            e.Trail += step;
            if (e.Trail < TrailStep) return;
            e.Trail = 0f;
            if (BurningGround.Count < MaxBurningGround) BurningGround.Add(new Puddle { Pos = e.Pos, Radius = e.Kind == EnemyKind.Hwama ? 1.2f : 0.7f, Life = 4f, MaxLife = 4f });
        }

        /// <summary>불풍선이 터진다: 둘레 FireBalloonBlast 안 탈 것마다 불, 가까운 소방관은 덴다.</summary>
        private void BurstFireBalloon(Vec2 at)
        {
            FireBalloonBlasts.Add(at);
            foreach (Structure st in Structures)
            {
                if (st.Within(at, FireBalloonBlast) && Ignite(st, FireBalloonIgnite)) RaiderIgnites++;
            }
            if (Player.DistanceTo(at) <= FireBalloonHurtRange) Hurt(FireBalloonHurt, HurtKind.Blast);
        }

        // ------------------------------------------------------------------
        // 보스·처치 보상
        // ------------------------------------------------------------------

        /// <summary>화마: 3:00 대화재 랜드마크 곁에서 일어난다.</summary>
        private void RaiseBoss()
        {
            Vec2 at = Landmark != null ? Landmark.Door : SpawnPoint(SpawnDistance);
            Enemy boss = Spawn(EnemyKind.Hwama, at);
            boss.MaxHp = BossHp * (1f + (0.05f * Level));
            boss.Hp = boss.MaxHp;
            Boss = boss;
            JustBossRise = true;
            _bossRatClock = 1.5f;
            Stats.Events++;
        }

        /// <summary>마을 몹이 잡혔다(Kill이 부른다): 불곰은 상자, 화마는 보석 비와 상자.</summary>
        private void RaiderKilled(Enemy e)
        {
            RaidersKilled++;
            if (e.Kind == EnemyKind.Bear)
            {
                Chests.Add(new Pickup { Pos = e.Pos, Life = ChestLife });
                BearsDown.Add(e.Pos);
            }
            else if (e.Kind == EnemyKind.Hwama)
            {
                BossKilled = true;
                JustBossDown = true;
                BossDownAt = e.Pos;
                Chests.Add(new Pickup { Pos = e.Pos, Life = ChestLife });
                for (int i = 0; i < BossGems; i++)
                {
                    double a = i * Math.PI * 2 / BossGems;
                    DropGem(new Vec2(e.Pos.X + (float)(Math.Cos(a) * 2f), e.Pos.Y + (float)(Math.Sin(a) * 2f)), 10);
                }
            }
        }
    }
}
