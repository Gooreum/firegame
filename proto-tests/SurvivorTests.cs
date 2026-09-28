using System.Collections.Generic;
using FireGame.Core.Sim;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>시험판 C(뱀서라이크) 규칙. 무기가 맞히고, 구슬이 레벨을 올리고, 카드 뽑기가 규칙대로 나온다.</summary>
    [Collection("Heavy")]
    public class SurvivorTests
    {
        private static void Run(SurvivorSim sim, float seconds, float mx = 0f, float my = 0f)
        {
            int ticks = (int)(seconds / SurvivorSim.Dt);
            for (int i = 0; i < ticks; i++) sim.Step(mx, my);
        }

        /// <summary>스폰 감독이 끼어들지 않게 적을 모두 지운 판(테스트는 원하는 적만 놓는다).</summary>
        private static SurvivorSim Quiet(int seed = 1)
        {
            var sim = new SurvivorSim(seed);
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        /// <summary>플레이어가 하듯 과녁을 겨누고 누른 채로 시간을 보낸다.</summary>
        private static void Spray(SurvivorSim sim, Vec2 at, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                sim.Aim = new Vec2(at.X - sim.Player.X, at.Y - sim.Player.Y);
                sim.Spraying = true;
                sim.Step(0f, 0f);
            }
        }

        // --- 물대포: 직접 겨눈다 ---
        [Fact]
        public void Hose_SpraysWhereYouAim_AndAKillDropsAGem()
        {
            SurvivorSim sim = Quiet();
            Enemy e = sim.Spawn(EnemyKind.Blaze, new Vec2(sim.Player.X + 3f, sim.Player.Y));
            e.Speed = 0f;

            Spray(sim, e.Pos, 60);
            Assert.True(e.Hp < e.MaxHp, "겨누고 1초 쐈는데 한 번도 못 맞혔다");

            Enemy ember = sim.Spawn(EnemyKind.Ember, new Vec2(sim.Player.X - 3f, sim.Player.Y));
            ember.Speed = 0f;
            e.Dead = true;
            for (int i = 0; i < 120 && !ember.Dead; i++) Spray(sim, ember.Pos, 1);
            Assert.True(ember.Dead, "불씨를 2초 안에 못 껐다");
            Assert.Contains(sim.Gems, g => g.Pos.DistanceTo(ember.Pos) < 1f);
        }

        [Fact]
        public void Hose_DoesNothing_WhenNotSpraying()
        {
            SurvivorSim sim = Quiet();
            Enemy e = sim.Spawn(EnemyKind.Blaze, new Vec2(sim.Player.X + 3f, sim.Player.Y));
            e.Speed = 0f;
            sim.Aim = new Vec2(1f, 0f);
            sim.Spraying = false;
            Run(sim, 1f);
            Assert.Empty(sim.Shots);
            Assert.Equal(e.MaxHp, e.Hp);
        }

        [Fact]
        public void Hose_MissesAFire_BehindYou()
        {
            SurvivorSim sim = Quiet();
            Enemy e = sim.Spawn(EnemyKind.Blaze, new Vec2(sim.Player.X + 3f, sim.Player.Y));
            e.Speed = 0f;
            Spray(sim, new Vec2(sim.Player.X - 5f, sim.Player.Y), 60);
            Assert.Equal(e.MaxHp, e.Hp);
        }

        // --- TC-2 ---
        [Fact]
        public void FillingXp_PausesWithThreeCards()
        {
            SurvivorSim sim = Quiet();
            sim.DropGem(sim.Player, sim.XpToNext);
            sim.Step(0f, 0f);
            sim.Step(0f, 0f);

            Assert.Equal(2, sim.Level);
            Assert.NotNull(sim.PendingChoices);
            Assert.Equal(3, sim.PendingChoices.Count);
            Assert.True(sim.JustLeveled || sim.PendingChoices != null);

            float frozen = sim.Time;
            Run(sim, 1f);
            Assert.Equal(frozen, sim.Time);
        }

        // --- TC-3 ---
        [Fact]
        public void Choosing_RaisesTheLevel_AndTimeFlowsAgain()
        {
            SurvivorSim sim = Quiet();
            sim.DropGem(sim.Player, sim.XpToNext);
            for (int i = 0; i < 3 && sim.PendingChoices == null; i++) sim.Step(0f, 0f);

            UpgradeId pick = sim.PendingChoices[0];
            int before = sim.Build.Level(pick);
            sim.Choose(0);

            Assert.Null(sim.PendingChoices);
            Assert.Equal(before + 1, sim.Build.Level(pick));
            float t = sim.Time;
            sim.Step(0f, 0f);
            Assert.True(sim.Time > t);
        }

        // --- TC-4 ---
        [Fact]
        public void MaxedItems_AreNeverOffered()
        {
            var l = new Loadout();
            for (int i = 0; i < Loadout.MaxLevel; i++) l.Add(UpgradeId.Hose);
            var rng = new Rng(7);
            for (int i = 0; i < 1000; i++)
            {
                Assert.DoesNotContain(UpgradeId.Hose, SurvivorUpgrades.Roll(l, 2, ref rng));
            }
        }

        // --- TC-5 ---
        [Fact]
        public void Evolution_IsAlwaysOffered_AndReplacesTheHose()
        {
            var l = new Loadout();
            for (int i = 0; i < Loadout.MaxLevel; i++) l.Add(UpgradeId.Hose);
            l.Add(UpgradeId.Tank);

            var rng = new Rng(3);
            for (int i = 0; i < 200; i++) Assert.Contains(UpgradeId.Cannon, SurvivorUpgrades.Roll(l, 2, ref rng));

            l.Add(UpgradeId.Cannon);
            Assert.Equal(0, l.Level(UpgradeId.Hose));
            Assert.Equal(1, l.Level(UpgradeId.Cannon));
            for (int i = 0; i < 200; i++)
            {
                List<UpgradeId> cards = SurvivorUpgrades.Roll(l, 2, ref rng);
                Assert.DoesNotContain(UpgradeId.Cannon, cards);
                Assert.DoesNotContain(UpgradeId.Hose, cards);
            }
        }

        // --- TC-6 ---
        [Fact]
        public void NothingLeft_OffersOnlyHeal_ThatCapsAtMaxHp()
        {
            SurvivorSim sim = Quiet();
            foreach (UpgradeId id in new[] { UpgradeId.Hose, UpgradeId.WaterBomb, UpgradeId.Drone, UpgradeId.Foam, UpgradeId.Tank, UpgradeId.Suit, UpgradeId.Boots, UpgradeId.Radio })
            {
                while (sim.Build.Level(id) < Loadout.MaxLevel) sim.Build.Add(id);
            }
            sim.Build.Add(UpgradeId.Cannon);
            sim.Build.Add(UpgradeId.Heli);
            sim.Build.Add(UpgradeId.Curtain);
            sim.Build.Add(UpgradeId.Partner);

            var rng = new Rng(5);
            Assert.Equal(new List<UpgradeId> { UpgradeId.Heal }, SurvivorUpgrades.Roll(sim.Build, 2, ref rng));

            sim.Hp = sim.MaxHp - 10f;
            sim.DropGem(sim.Player, sim.XpToNext);
            for (int i = 0; i < 3 && sim.PendingChoices == null; i++) sim.Step(0f, 0f);
            Assert.Equal(new List<UpgradeId> { UpgradeId.Heal }, sim.PendingChoices);
            sim.Choose(0);
            Assert.Equal(sim.MaxHp, sim.Hp);
        }

        // --- TC-7 ---
        [Fact]
        public void FullWeaponSlots_OfferNoNewWeapon()
        {
            var l = new Loadout();
            l.Add(UpgradeId.Hose);
            l.Add(UpgradeId.WaterBomb);
            l.Add(UpgradeId.Drone);
            l.Add(UpgradeId.Foam);
            Assert.Equal(4, l.WeaponCount);

            var rng = new Rng(11);
            var seen = new HashSet<UpgradeId>();
            for (int i = 0; i < 500; i++) foreach (UpgradeId id in SurvivorUpgrades.Roll(l, 2, ref rng)) seen.Add(id);
            Assert.DoesNotContain(UpgradeId.Cannon, seen);
            Assert.Contains(UpgradeId.Tank, seen);
            Assert.Contains(UpgradeId.Hose, seen);

            var full = new Loadout();
            full.Add(UpgradeId.Tank);
            full.Add(UpgradeId.Suit);
            full.Add(UpgradeId.Boots);
            full.Add(UpgradeId.Radio);
            full.Add(UpgradeId.Hose);
            full.Add(UpgradeId.WaterBomb);
            full.Add(UpgradeId.Drone);
            Assert.True(full.CanTake(UpgradeId.Foam), "무기 3개일 땐 네 번째 무기를 얻을 수 있어야 한다");
            full.Add(UpgradeId.Foam);
            foreach (UpgradeId id in new[] { UpgradeId.Hose, UpgradeId.WaterBomb, UpgradeId.Drone, UpgradeId.Foam }) Assert.True(full.CanTake(id));
        }

        // --- TC-8 ---
        [Fact]
        public void SameSeed_SamePlay()
        {
            var a = new SurvivorSim(42);
            var b = new SurvivorSim(42);
            var aimA = new SurvivorBot(a);
            var aimB = new SurvivorBot(b);
            for (int i = 0; i < 1200; i++)
            {
                float mx = (float)System.Math.Sin(i * 0.01);
                float my = (float)System.Math.Cos(i * 0.013);
                aimA.AimHose();
                aimB.AimHose();
                a.Step(mx, my);
                b.Step(mx, my);
                if (a.PendingChoices != null) a.Choose(0);
                if (b.PendingChoices != null) b.Choose(0);
            }
            Assert.Equal(a.Enemies.Count, b.Enemies.Count);
            Assert.Equal(a.Kills, b.Kills);
            Assert.Equal(a.Player.X, b.Player.X);
            Assert.Equal(a.Player.Y, b.Player.Y);
            Assert.Equal(a.Hp, b.Hp);
            Assert.True(a.Kills > 0);
        }
            // ================= Step 2: 나머지 무기·보스·밸런스 =================

        private static Enemy Dummy(SurvivorSim sim, EnemyKind kind, float dx, float dy, float hp = 999f)
        {
            Enemy e = sim.Spawn(kind, new Vec2(sim.Player.X + dx, sim.Player.Y + dy));
            e.Speed = 0f;
            e.MaxHp = hp;
            e.Hp = hp;
            return e;
        }

        // --- S2 TC-1 ---
        [Fact]
        public void WaterBomb_ExplodesOnACrowd()
        {
            var sim = new SurvivorSim(1);
            sim.Enemies.Clear();
            sim.Build.Add(UpgradeId.WaterBomb);
            var crowd = new List<Enemy>();
            for (int i = 0; i < 5; i++) crowd.Add(Dummy(sim, EnemyKind.Blaze, 6f + (i * 0.3f), 0.2f * i));

            bool exploded = false;
            for (int i = 0; i < 180; i++)
            {
                sim.Step(0f, 0f);
                if (sim.Explosions.Count > 0) exploded = true;
            }
            Assert.True(exploded, "3초 동안 물폭탄이 한 번도 안 터졌다");
            Assert.True(crowd.FindAll(e => e.Hp < e.MaxHp).Count >= 3, "폭발이 무리 여럿을 맞히지 못했다");
        }

        // --- S2 TC-2 ---
        [Fact]
        public void Drones_OrbitAndHitWhatTheyTouch()
        {
            SurvivorSim sim = Quiet();
            sim.Build.Add(UpgradeId.Drone);
            sim.Build.Add(UpgradeId.Drone);
            Enemy e = Dummy(sim, EnemyKind.Ember, 0f, 2.3f);
            e.Pos = new Vec2(sim.Player.X, sim.Player.Y + 2.3f);

            sim.Step(0f, 0f);
            Assert.Equal(2, sim.Drones.Count);
            Assert.All(sim.Drones, d => Assert.InRange(d.DistanceTo(sim.Player), 2.2f, 2.4f));

            // 물대포도 이 적을 쏘므로, 드론이 맞혔다는 표시(드론 재타격 대기)로 확인한다.
            bool struck = false;
            for (int i = 0; i < 180 && !struck; i++)
            {
                sim.Step(0f, 0f);
                if (e.DroneCooldown > 0f) struck = true;
            }
            Assert.True(struck, "3초 동안 드론이 궤도 위 적을 한 번도 안 맞혔다");
        }

        // --- S2 TC-3 ---
        [Fact]
        public void Foam_LeavesPuddlesThatSlowAndHurt()
        {
            SurvivorSim sim = Quiet();
            sim.Build.Add(UpgradeId.Foam);
            Run(sim, 1f, 1f, 0f);
            Assert.True(sim.Foam.Count >= 2, "움직였는데 거품이 안 남았다");

            Puddle p = sim.Foam[0];
            Enemy e = Dummy(sim, EnemyKind.Blaze, 0f, 0f);
            e.Pos = p.Pos;
            e.Speed = 1f;
            sim.Player = new Vec2(sim.Player.X + 12f, sim.Player.Y);   // 물대포 사거리 밖
            Run(sim, 0.8f);
            Assert.True(e.Slowed > 0f, "거품 위 적이 느려지지 않았다");
            Assert.True(e.Hp < e.MaxHp, "거품 위 적이 피해를 안 입었다");
        }

        // --- S2 TC-4 ---
        [Fact]
        public void Cannon_PiercesALineOfFires()
        {
            SurvivorSim sim = Quiet();
            for (int i = 0; i < Loadout.MaxLevel; i++) sim.Build.Add(UpgradeId.Hose);
            sim.Build.Add(UpgradeId.Tank);
            sim.DropGem(sim.Player, sim.XpToNext);
            for (int i = 0; i < 3 && sim.PendingChoices == null; i++) sim.Step(0f, 0f);
            sim.Choose(sim.PendingChoices.IndexOf(UpgradeId.Cannon));
            Assert.Equal(1, sim.Build.Level(UpgradeId.Cannon));

            // 사방 여덟 줄에 적을 세 마리씩 세우면, 한 바퀴 휩쓸 때 한 줄 전체가 맞는다.
            var lines = new List<List<Enemy>>();
            for (int k = 0; k < 8; k++)
            {
                double a = System.Math.PI * 2 * k / 8;
                var line = new List<Enemy>();
                for (int r = 2; r <= 6; r += 2) line.Add(Dummy(sim, EnemyKind.Blaze, (float)System.Math.Cos(a) * r, (float)System.Math.Sin(a) * r));
                lines.Add(line);
            }
            Run(sim, 1f);
            Assert.Contains(lines, line => line.TrueForAll(e => e.Hp < e.MaxHp));
        }

        // --- S2 TC-5 ---
        [Fact]
        public void BlazeLeavesBurningGround_ThatHurts()
        {
            SurvivorSim sim = Quiet();
            Enemy e = Dummy(sim, EnemyKind.Blaze, 1.5f, 0f, 1f);
            for (int i = 0; i < 60 && !e.Dead; i++) Spray(sim, e.Pos, 1);
            sim.Spraying = false;
            Assert.True(e.Dead);
            Assert.Single(sim.BurningGround);

            Puddle fire = sim.BurningGround[0];
            sim.Player = fire.Pos;
            float hp = sim.Hp;
            sim.Step(0f, 0f);
            Assert.True(sim.Hp < hp, "타는 바닥 위인데 체력이 안 줄었다");

            // 그때 있던 바닥 불(처치 자리 3초, 물에 밀려 걸으며 남긴 흔적 4초)은 모두 타서 사라진다.
            var before = new List<Puddle>(sim.BurningGround);
            Run(sim, 4.1f);
            Assert.All(before, q => Assert.DoesNotContain(q, sim.BurningGround));
        }

        /// <summary>
        /// 봇이 보스까지 가는 첫 시드(보스·성능 테스트용). 규칙이 난수 순서를 바꿔도 테스트가 깨지지 않게 매번 찾는다.
        /// </summary>
        private static readonly int BossSeed = FindBossSeed();

        private static int FindBossSeed()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var sim = new SurvivorSim(seed);
                var bot = new SurvivorBot(sim);
                while (sim.Outcome == SOutcome.Playing && sim.Time < SurvivorSim.BossAt + 1f) bot.Play();
                if (sim.Outcome == SOutcome.Playing) return seed;
            }
            return 1;
        }

        // --- S2 TC-6 ---
        [Fact]
        public void Boss_ArrivesAtFourMinutes_AndPuttingItOutWins()
        {
            var sim = new SurvivorSim(BossSeed);
            var bot = new SurvivorBot(sim);
            bool arrived = false;
            while (sim.Outcome == SOutcome.Playing && sim.Time < SurvivorSim.BossAt + 1f)
            {
                bot.Play();
                if (sim.JustBossArrived) arrived = true;
            }
            Assert.Equal(SOutcome.Playing, sim.Outcome);
            Assert.True(arrived);
            Assert.NotNull(sim.Boss);
            Assert.InRange(sim.Time, SurvivorSim.BossAt, SurvivorSim.BossAt + 1.1f);

            sim.Boss.Hp = 0.1f;
            for (int i = 0; i < 600 && sim.Outcome == SOutcome.Playing; i++) bot.Play();
            Assert.Equal(SOutcome.Won, sim.Outcome);
        }

        // --- S2 TC-7 ---
        [Fact]
        public void StandingStill_LosesWithin150Seconds()
        {
            var sim = new SurvivorSim(1);
            while (sim.Outcome == SOutcome.Playing && sim.Time < 150f)
            {
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
            }
            Assert.Equal(SOutcome.Lost, sim.Outcome);
        }

        // --- S2 TC-8, TC-9 ---
        [Fact]
        public void Bot_ReachesTheBossOften_WinsSometimes_AndTheCrowdIsCapped()
        {
            int reached = 0;
            int won = 0;
            var log = new System.Text.StringBuilder();
            for (int seed = 1; seed <= 10; seed++)
            {
                var sim = new SurvivorSim(seed);
                var bot = new SurvivorBot(sim);
                int guard = 0;
                while (sim.Outcome == SOutcome.Playing && guard++ < 60 * 400)
                {
                    bot.Play();
                    Assert.True(sim.Enemies.Count <= SurvivorSim.MaxEnemies, "적이 상한을 넘었다");
                }
                if (sim.Boss != null) reached++;
                if (sim.Outcome == SOutcome.Won) won++;
                log.AppendLine("seed " + seed + ": " + sim.Outcome + " t=" + (int)sim.Time + " lv=" + sim.Level);
            }
            Assert.True(reached >= 6, "보스까지 간 판이 " + reached + "개\n" + log);
            Assert.InRange(won, 3, 9);
        }

        // --- S2 TC-10 ---
        [Fact]
        public void AFullRun_IsCheapToSimulate()
        {
            int seed = BossSeed;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var sim = new SurvivorSim(seed);
            var bot = new SurvivorBot(sim);
            int guard = 0;
            while (sim.Outcome == SOutcome.Playing && guard++ < 60 * 400) bot.Play();
            watch.Stop();
            Assert.True(sim.Time > SurvivorSim.BossAt, "시드 " + BossSeed + "가 보스까지 못 가 성능 측정이 짧아졌다");
            Assert.True(watch.Elapsed.TotalSeconds < 3.0, "한 판에 " + watch.Elapsed.TotalSeconds + "초");
        }

        [Fact]
        public void Cannon_SpraysPiercingJets_WhereYouAim()
        {
            SurvivorSim sim = Quiet();
            sim.GiveMaxGear();
            var line = new List<Enemy>();
            for (int r = 2; r <= 6; r += 2) line.Add(Dummy(sim, EnemyKind.Blaze, 0f, r));

            int aimed = 0;
            for (int i = 0; i < 40; i++)
            {
                Spray(sim, new Vec2(sim.Player.X, sim.Player.Y + 10f), 1);
                foreach (Shot s in sim.Shots)
                {
                    if (s.Kind == ShotKind.Jet && s.Age <= SurvivorSim.Dt && s.Vel.Y > 0f && System.Math.Abs(s.Vel.X) < 0.2f * s.Vel.Y) aimed++;
                }
            }
            Assert.True(aimed >= 5, "위로 겨눈 방수포 제트가 " + aimed + "줄뿐");
            Assert.True(line.TrueForAll(e => e.Hp < e.MaxHp), "겨눈 제트가 한 줄을 꿰뚫지 못했다");
        }

        // --- 번지는 불 ---
        [Fact]
        public void MovingBlaze_LeavesATrailOfFire()
        {
            SurvivorSim sim = Quiet();
            Enemy e = sim.Spawn(EnemyKind.Blaze, new Vec2(sim.Player.X + 8f, sim.Player.Y));
            e.MaxHp = 999f;
            e.Hp = 999f;
            Run(sim, 3f);
            Assert.True(sim.BurningGround.Count >= 2, "3초 걸어온 큰 불이 남긴 바닥 불이 " + sim.BurningGround.Count + "개");
        }

        [Fact]
        public void WaterPutsOutBurningGround()
        {
            SurvivorSim sim = Quiet();
            var patch = new Puddle { Pos = new Vec2(sim.Player.X + 3f, sim.Player.Y), Radius = 0.6f, Life = 4f, MaxLife = 4f };
            sim.BurningGround.Add(patch);
            bool signalled = false;
            for (int i = 0; i < 40 && sim.BurningGround.Contains(patch); i++)
            {
                Spray(sim, patch.Pos, 1);
                if (sim.Extinguished.Count > 0) signalled = true;
            }
            Assert.DoesNotContain(patch, sim.BurningGround);
            Assert.True(signalled, "꺼진 자리를 알리지 않았다");
        }

        [Fact]
        public void UnwateredGround_CanReignite()
        {
            SurvivorSim sim = Quiet();
            for (int k = 0; k < 10; k++)
            {
                double a = System.Math.PI * 2 * k / 10;
                var at = new Vec2(sim.Player.X + (float)(System.Math.Cos(a) * 8), sim.Player.Y + (float)(System.Math.Sin(a) * 8));
                sim.BurningGround.Add(new Puddle { Pos = at, Radius = 0.6f, Life = 0.05f, MaxLife = 4f });
            }
            int reignited = 0;
            for (int i = 0; i < 10; i++)
            {
                sim.Step(0f, 0f);
                reignited += sim.Reignited.Count;
            }
            Assert.InRange(reignited, 1, 10);
            Assert.Contains(sim.Enemies, e => e.Kind == EnemyKind.Ember);
        }

        // --- 동네: 지킬 것 ---

        /// <summary>조용한 판에 가게 하나를 소방관 기준 (dx, dy)에 세운다.</summary>
        private static Structure Shop(SurvivorSim sim, float dx, float dy, int residents = 1)
        {
            var s = new Structure { Kind = StructureKind.House, Name = "가게", Pos = new Vec2(sim.Player.X + dx, sim.Player.Y + dy), Half = new Vec2(2f, 1.5f), Residents = residents };
            sim.Structures.Add(s);
            return s;
        }

        [Fact]
        public void Town_HasShopsDepotGasAndAClearCenter()
        {
            var sim = new SurvivorSim(1);
            Assert.Equal(8, sim.Structures.FindAll(s => s.Kind == StructureKind.House).Count);
            Assert.Single(sim.Structures.FindAll(s => s.Kind == StructureKind.Depot));
            Assert.Equal(3, sim.Structures.FindAll(s => s.Kind == StructureKind.Gas).Count);
            Assert.Equal(9, sim.HousesTotal);
            Assert.All(sim.Structures, s => Assert.True(s.DistanceTo(sim.Player) > 6f, s.Name + "가 출발점에 너무 가깝다"));
            Assert.All(sim.Structures, s => Assert.False(s.Burning));
        }

        [Fact]
        public void Ember_SeeksAndIgnitesAHouse()
        {
            // 불난 가게가 불씨를 뱉고, 그 불씨가 옆 가게로 굴러가 불을 옮긴다.
            SurvivorSim sim = Quiet();
            Structure burning = Shop(sim, 9f, 0f);
            Structure next = Shop(sim, 9f, 8f);
            sim.Ignite(burning, 0.5f);
            bool signalled = false;
            for (int i = 0; i < 60 * 30 && !next.Burning; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                if (sim.Ignited.Contains(next)) signalled = true;
            }
            Assert.True(next.Burning, "30초 동안 옆 가게로 불이 안 번졌다");
            Assert.True(signalled);
            Assert.Equal(0, sim.Kills);
        }

        [Fact]
        public void EdgeEmber_ChasesYou_NotTheHouse()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 8f, 0f);
            Enemy ember = sim.Spawn(EnemyKind.Ember, new Vec2(sim.Player.X + 8f, sim.Player.Y + 4f));
            float before = ember.Pos.DistanceTo(sim.Player);
            Run(sim, 0.5f);
            Assert.True(ember.Pos.DistanceTo(sim.Player) < before);
            Assert.False(shop.Burning);
        }

        [Fact]
        public void BurningHouse_GrowsAndCollapses_LosingItsResidents()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 10f, 0f, 2);
            sim.Ignite(shop, 0.15f);
            // 쏟아지는 불씨에 쓰러지지 않게 매 틱 체력을 채운다(가게만 본다).
            for (int i = 0; i < 60 * 25; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
            }
            Assert.Equal(1f, shop.Fire, 2);
            Assert.True(shop.Integrity < 1f);
            Assert.Contains(sim.Enemies, e => e.Kind == EnemyKind.Ember);

            bool fell = false;
            for (int i = 0; i < 60 * 60 && !fell; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                if (sim.Fell.Contains(shop)) fell = true;
            }
            Assert.True(fell, "방치한 가게가 85초 안에 안 무너졌다");
            Assert.True(shop.Collapsed);
            Assert.Equal(1, sim.HousesLost);
            Assert.Equal(2, sim.CiviliansLost);
        }

        [Fact]
        public void SprayingABurningHouse_PutsItOut_AndItStaysWet()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 6f, 0f);
            sim.Ignite(shop, 1f);
            bool doused = false;
            for (int i = 0; i < 60 * 5 && shop.Burning; i++)
            {
                Spray(sim, shop.Pos, 1);
                if (sim.Doused.Contains(shop)) doused = true;
            }
            Assert.False(shop.Burning, "다 탄 가게를 5초 뿌려도 안 꺼졌다");
            Assert.True(doused);
            Assert.True(shop.Wet > 0f);
            Assert.False(sim.Ignite(shop, 0.5f), "젖은 가게에 불이 다시 붙었다");
        }

        [Fact]
        public void Drop_IsBlockedByAWall_JetPiercesIt()
        {
            SurvivorSim sim = Quiet();
            Shop(sim, 4f, 0f);
            Enemy behind = Dummy(sim, EnemyKind.Blaze, 8f, 0f);
            Spray(sim, behind.Pos, 90);
            Assert.Equal(behind.MaxHp, behind.Hp);

            sim.GiveMaxGear();
            sim.Build.Level(UpgradeId.WaterBomb);
            for (int i = 0; i < 60 && behind.Hp >= behind.MaxHp; i++)
            {
                Spray(sim, behind.Pos, 1);
                sim.Explosions.Clear();
            }
            Assert.True(behind.Hp < behind.MaxHp, "방수포 제트가 가게를 뚫지 못했다");
        }

        [Fact]
        public void Player_CannotWalkThroughAHouse()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 3f, 0f);
            Run(sim, 3f, 1f, 0f);
            Assert.True(sim.Player.X <= shop.Pos.X - shop.Half.X - SurvivorSim.PlayerRadius + 0.01f, "소방관이 가게 벽 안으로 들어갔다: x=" + sim.Player.X);
        }

        // --- 구조·가스통·신고·승패 ---

        [Fact]
        public void StandingAtTheDoor_RescuesTrappedPeople()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 0f, 3.2f, 2);
            Structure calm = Shop(sim, 9f, 3.2f, 1);
            Assert.True(shop.Door.DistanceTo(sim.Player) <= SurvivorSim.RescueRange);
            sim.Ignite(shop, 0.2f);

            bool signalled = false;
            for (int i = 0; i < 60 * 3; i++)
            {
                // 구조 구슬로 레벨이 오르면 카드를 고르고 계속 선다.
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
                if (sim.JustRescued && sim.RescuedFrom.Contains(shop)) signalled = true;
            }
            Assert.Equal(2, sim.Rescued);
            Assert.Equal(0, shop.Residents);
            Assert.True(signalled);
            Assert.Equal(1, calm.Residents);

            // 안 타는 가게 문 앞에서는 아무도 안 나온다.
            sim.Player = new Vec2(calm.Door.X, calm.Door.Y);
            Run(sim, 2f);
            Assert.Equal(1, calm.Residents);
        }

        [Fact]
        public void TrappedPeople_ChokeInABigFire()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 12f, 0f, 2);
            sim.Ignite(shop, 1f);
            bool signalled = false;
            for (int i = 0; i < (int)((SurvivorSim.SmokeTime + 0.5f) * 60); i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                if (sim.PeopleLost.Contains(shop)) signalled = true;
            }
            Assert.Equal(1, sim.CiviliansLost);
            Assert.Equal(1, shop.Residents);
            Assert.True(signalled);
        }

        [Fact]
        public void GasTank_ExplodesAndSpreads_UnlessWetFirst()
        {
            SurvivorSim sim = Quiet();
            var gas = new Structure { Kind = StructureKind.Gas, Name = "가스통", Pos = new Vec2(sim.Player.X + 7f, sim.Player.Y), Half = new Vec2(0.4f, 0.4f) };
            sim.Structures.Add(gas);
            Structure shop = Shop(sim, 9.6f, 0f);
            sim.Ignite(gas, 0.2f);
            bool blasted = false;
            for (int i = 0; i < (int)((SurvivorSim.GasFuse + 0.2f) * 60); i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                if (sim.GasBlasts.Count > 0) blasted = true;
            }
            Assert.True(blasted, "불붙은 가스통이 안 터졌다");
            Assert.True(gas.Collapsed);
            Assert.True(shop.Burning, "터진 가스통 옆 가게에 불이 안 옮았다");
            Assert.True(shop.Fire >= 0.5f);

            SurvivorSim wet = Quiet();
            var gas2 = new Structure { Kind = StructureKind.Gas, Name = "가스통", Pos = new Vec2(wet.Player.X + 5f, wet.Player.Y), Half = new Vec2(0.4f, 0.4f) };
            wet.Structures.Add(gas2);
            Spray(wet, gas2.Pos, 20);
            wet.Spraying = false;
            Assert.True(gas2.Wet > 0f, "물을 뿌린 가스통이 안 젖었다");
            Assert.False(wet.Ignite(gas2, 0.5f), "젖은 가스통에 불이 붙었다");
            Run(wet, 3f);
            Assert.False(gas2.Collapsed);
        }

        [Fact]
        public void Reports_IgniteShopsOnSchedule()
        {
            var sim = new SurvivorSim(1);
            var times = new List<float>();
            int pairs = 0;
            while (sim.Time < SurvivorSim.ReportTimes[SurvivorSim.ReportTimes.Length - 1] + 0.5f)
            {
                // 신고로 난 불만 보려고 매 틱 다른 불을 치운다.
                sim.Enemies.Clear();
                foreach (Structure s in sim.Structures) s.Fire = 0f;
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                int shops = sim.Ignited.FindAll(s => s.Kind == StructureKind.House).Count;
                for (int k = 0; k < shops; k++) times.Add(sim.Time);
                if (shops >= 2) pairs++;
            }
            Assert.Equal(SurvivorSim.ReportTimes.Length, times.Count);
            Assert.InRange(times[0], SurvivorSim.ReportTimes[0], SurvivorSim.ReportTimes[0] + 0.05f);
            Assert.Equal(2, pairs);
        }

        [Fact]
        public void LosingHalfTheTown_LosesTheRun()
        {
            var sim = new SurvivorSim(1);
            sim.Enemies.Clear();
            List<Structure> shops = sim.Structures.FindAll(s => s.Kind == StructureKind.House);
            for (int k = 0; k < 4; k++)
            {
                sim.Ignite(shops[k], 1f);
                shops[k].Integrity = 0.0001f;
            }
            sim.Step(0f, 0f);
            Assert.Equal(4, sim.HousesLost);
            Assert.Equal(SOutcome.Playing, sim.Outcome);

            sim.Ignite(shops[4], 1f);
            shops[4].Integrity = 0.0001f;
            sim.Step(0f, 0f);
            Assert.Equal(SOutcome.Lost, sim.Outcome);
            Assert.True(sim.LostTown);
            Assert.True(sim.CiviliansLost > 0);
        }

        /// <summary>4:00까지 건너뛰고 창고에서 나온 거인을 끈다.</summary>
        private static void BeatTheBoss(SurvivorSim sim)
        {
            while (sim.Boss == null)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
            }
            Enemy boss = sim.Boss;
            boss.Hp = 0.1f;
            sim.Player = new Vec2(boss.Pos.X, boss.Pos.Y - 4f);
            for (int i = 0; i < 300 && sim.Outcome == SOutcome.Playing; i++) Spray(sim, boss.Pos, 1);
        }

        [Fact]
        public void Boss_ComesOutOfTheBurningDepot()
        {
            var sim = new SurvivorSim(1);
            sim.Reports = false;
            while (sim.Boss == null)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
            }
            Structure depot = sim.Structures.Find(s => s.Kind == StructureKind.Depot);
            Assert.True(depot.Burning);
            Assert.True(sim.Boss.Pos.DistanceTo(depot.Door) < 0.5f);
        }

        [Fact]
        public void WinningStars_CountSavedHousesAndLostPeople()
        {
            var perfect = new SurvivorSim(1);
            perfect.Reports = false;
            BeatTheBoss(perfect);
            Assert.Equal(SOutcome.Won, perfect.Outcome);
            Assert.Equal(3, perfect.Stars);

            var oneLost = new SurvivorSim(1);
            oneLost.Reports = false;
            Structure shop = oneLost.Structures.Find(s => s.Kind == StructureKind.House && s.Residents > 0);
            oneLost.Ignite(shop, 1f);
            shop.Integrity = 0.0001f;
            BeatTheBoss(oneLost);
            Assert.Equal(SOutcome.Won, oneLost.Outcome);
            Assert.Equal(1, oneLost.HousesLost);
            Assert.Equal(2, oneLost.Stars);
        }

        // --- 풀장비 시작 ---
        [Fact]
        public void GiveMaxGear_MaxesEveryItem_AndEvolvesTheHose()
        {
            var sim = new SurvivorSim(1);
            sim.GiveMaxGear();

            UpgradeId[] maxed = { UpgradeId.WaterBomb, UpgradeId.Drone, UpgradeId.Foam, UpgradeId.Tank, UpgradeId.Suit, UpgradeId.Boots, UpgradeId.Radio };
            foreach (UpgradeId id in maxed) Assert.Equal(Loadout.MaxLevel, sim.Build.Level(id));
            Assert.Equal(1, sim.Build.Level(UpgradeId.Cannon));
            Assert.Equal(0, sim.Build.Level(UpgradeId.Hose));
            Assert.Equal(200f, sim.MaxHp);
            Assert.Equal(sim.MaxHp, sim.Hp);
        }

        [Fact]
        public void MaxGear_FiresJetsBombsAndFiveDrones_FromTheStart()
        {
            SurvivorSim sim = Quiet();
            sim.GiveMaxGear();
            sim.Spawn(EnemyKind.Blaze, new Vec2(sim.Player.X + 4f, sim.Player.Y)).Speed = 0f;

            bool jet = false;
            bool bomb = false;
            for (int i = 0; i < 30; i++)
            {
                sim.Step(0f, 0f);
                foreach (Shot s in sim.Shots)
                {
                    if (s.Kind == ShotKind.Jet) jet = true;
                    if (s.Kind == ShotKind.Bomb) bomb = true;
                }
            }
            Assert.True(jet, "방수포 제트가 나가지 않았다");
            Assert.True(bomb, "물폭탄이 나가지 않았다");
            Assert.Equal(5, sim.Drones.Count);
        }

        // --- 노란 특수 장비·펌프·한 줄기 물대포 ---

        /// <summary>플레이어가 레벨업 카드에서 이 장비를 골랐을 때(Choose 경로).</summary>
        private static void Take(SurvivorSim sim, UpgradeId id)
        {
            sim.PendingChoices = new List<UpgradeId> { id };
            sim.Choose(0);
        }

        /// <summary>이번 틱에 새로 나간 물대포 물방울.</summary>
        private static List<Shot> FreshDrops(SurvivorSim sim)
        {
            return sim.Shots.FindAll(s => s.Kind == ShotKind.Drop && s.Age <= SurvivorSim.Dt);
        }

        [Fact]
        public void ReachingLevel5_AlwaysOffersAYellowCard()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                SurvivorSim sim = Quiet(seed);
                while (sim.Level < 5)
                {
                    sim.DropGem(sim.Player, sim.XpToNext);
                    for (int i = 0; i < 3 && sim.PendingChoices == null; i++) sim.Step(0f, 0f);
                    Assert.NotNull(sim.PendingChoices);
                    if (sim.Level == 5) Assert.Contains(sim.PendingChoices, Loadout.IsSpecial);
                    sim.Choose(0);
                }
            }
        }

        [Fact]
        public void Heli_DousesAFullyBurningShop()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 8f, 0f, 0);
            sim.Ignite(shop, 1f);
            Take(sim, UpgradeId.Heli);
            bool doused = false;
            bool dropped = false;
            for (int i = 0; i < 60 * 10 && !doused; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                if (sim.HeliDrops.Count > 0) dropped = true;
                if (sim.Doused.Contains(shop)) doused = true;
            }
            Assert.True(dropped, "10초 동안 헬기가 물을 안 쏟았다");
            Assert.True(doused, "헬기 물이 다 탄 가게를 못 껐다");
            Assert.True(shop.Wet > 0f);
        }

        [Fact]
        public void Curtain_PushesFiresAway()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Curtain);
            var embers = new List<Enemy>();
            var blazes = new List<Enemy>();
            for (int k = 0; k < 6; k++)
            {
                double a = System.Math.PI * 2 * k / 6;
                float dx = (float)System.Math.Cos(a) * 3f;
                float dy = (float)System.Math.Sin(a) * 3f;
                embers.Add(Dummy(sim, EnemyKind.Ember, dx, dy, 2f));
                blazes.Add(Dummy(sim, EnemyKind.Blaze, dx * 0.8f, dy * 0.8f, 999f));
            }
            bool burst = false;
            for (int i = 0; i < 60 * 2 && !burst; i++)
            {
                sim.Step(0f, 0f);
                burst |= sim.JustCurtain;
            }
            Assert.True(burst, "장막이 2초 안에 안 터졌다");
            Run(sim, 0.5f);
            Assert.True(embers.TrueForAll(e => e.Dead), "장막 안 불씨가 남았다");
            Assert.True(blazes.TrueForAll(e => e.Hp < e.MaxHp && e.Pos.DistanceTo(sim.Player) > 3f), "큰 불이 밀려나지 않았다");
        }

        [Fact]
        public void Partner_RescuesWhileYouStandFar()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 12f, 0f, 2);
            sim.Ignite(shop, 0.3f);
            Take(sim, UpgradeId.Partner);
            Assert.NotNull(sim.Partner);
            for (int i = 0; i < 60 * 10 && sim.Rescued == 0; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
            }
            Assert.True(sim.Rescued >= 1, "동료가 10초 안에 한 명도 못 구했다");
            Assert.True(shop.Door.DistanceTo(sim.Player) > SurvivorSim.RescueRange, "소방관이 문 앞에 간 게 아니어야 한다");
        }

        [Fact]
        public void Hose_StaysOneStream_ButGrowsThickerAndStronger()
        {
            SurvivorSim sim = Quiet();
            var ahead = new Vec2(sim.Player.X + 10f, sim.Player.Y);
            Spray(sim, ahead, 1);
            List<Shot> lv1 = FreshDrops(sim);
            Assert.Single(lv1);

            SurvivorSim big = Quiet();
            for (int i = 1; i < Loadout.MaxLevel; i++) Take(big, UpgradeId.Hose);
            Spray(big, ahead, 1);
            List<Shot> lv5 = FreshDrops(big);
            Assert.Single(lv5);
            Assert.True(lv5[0].Radius > lv1[0].Radius * 2f, "Lv5 물줄기가 충분히 굵지 않다");
            Assert.True(lv5[0].Damage > lv1[0].Damage * 2.5f, "Lv5 물줄기가 충분히 세지 않다");
            Assert.True(lv5[0].Life > lv1[0].Life, "Lv5 물줄기가 더 멀리 가지 않는다");
        }

        [Fact]
        public void Pump_ShootsFartherAndHarder()
        {
            SurvivorSim plain = Quiet();
            SurvivorSim pumped = Quiet();
            for (int i = 0; i < 3; i++) Take(pumped, UpgradeId.Tank);
            var ahead = new Vec2(plain.Player.X + 10f, plain.Player.Y);
            Spray(plain, ahead, 1);
            Spray(pumped, ahead, 1);
            Shot a = FreshDrops(plain)[0];
            Shot b = FreshDrops(pumped)[0];
            Assert.Equal(1.45f, b.Life / a.Life, 2);
            Assert.Equal(1.45f, b.Damage / a.Damage, 2);
        }

        [Fact]
        public void HoseShots_AreMarked_SweepJetsAreNot()
        {
            SurvivorSim sim = Quiet();
            var ahead = new Vec2(sim.Player.X + 10f, sim.Player.Y);
            Spray(sim, ahead, 1);
            Assert.True(FreshDrops(sim)[0].Hose, "물대포 물방울이 호스 물로 표시되지 않았다");

            // 방수포 진화(카드 경로): 쥔 호스 제트는 호스 물, 사방으로 도는 제트는 아니다.
            SurvivorSim big = Quiet();
            for (int i = 1; i < Loadout.MaxLevel; i++) Take(big, UpgradeId.Hose);
            Take(big, UpgradeId.Tank);
            Take(big, UpgradeId.Cannon);
            Spray(big, ahead, 10);
            List<Shot> jets = big.Shots.FindAll(s => s.Kind == ShotKind.Jet);
            Assert.Contains(jets, s => s.Hose);
            Assert.Contains(jets, s => !s.Hose);
            Assert.All(jets.FindAll(s => !s.Hose), s => Assert.Equal(0.8f, s.Life));
        }
    }
}
