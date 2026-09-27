using System.Collections.Generic;
using FireGame.Core.Sim;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>시험판 C(뱀서라이크) 규칙. 무기가 맞히고, 구슬이 레벨을 올리고, 카드 뽑기가 규칙대로 나온다.</summary>
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
            return sim;
        }

        // --- TC-1 ---
        [Fact]
        public void Hose_HitsTheNearestFire_AndAKillDropsAGem()
        {
            SurvivorSim sim = Quiet();
            Enemy e = sim.Spawn(EnemyKind.Blaze, new Vec2(sim.Player.X + 3f, sim.Player.Y));
            e.Speed = 0f;

            bool hurt = false;
            for (int i = 0; i < 60 && !e.Dead; i++)
            {
                sim.Step(0f, 0f);
                if (e.Hp < e.MaxHp) hurt = true;
            }
            Assert.True(hurt, "물대포가 1초 동안 한 번도 못 맞혔다");

            Enemy ember = sim.Spawn(EnemyKind.Ember, new Vec2(sim.Player.X - 3f, sim.Player.Y));
            ember.Speed = 0f;
            e.Dead = true;
            for (int i = 0; i < 120 && !ember.Dead; i++) sim.Step(0f, 0f);
            Assert.True(ember.Dead, "불씨를 2초 안에 못 껐다");
            Assert.Contains(sim.Gems, g => g.Pos.DistanceTo(ember.Pos) < 1f);
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
                Assert.DoesNotContain(UpgradeId.Hose, SurvivorUpgrades.Roll(l, ref rng));
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
            for (int i = 0; i < 200; i++) Assert.Contains(UpgradeId.Cannon, SurvivorUpgrades.Roll(l, ref rng));

            l.Add(UpgradeId.Cannon);
            Assert.Equal(0, l.Level(UpgradeId.Hose));
            Assert.Equal(1, l.Level(UpgradeId.Cannon));
            for (int i = 0; i < 200; i++)
            {
                List<UpgradeId> cards = SurvivorUpgrades.Roll(l, ref rng);
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

            var rng = new Rng(5);
            Assert.Equal(new List<UpgradeId> { UpgradeId.Heal }, SurvivorUpgrades.Roll(sim.Build, ref rng));

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
            for (int i = 0; i < 500; i++) foreach (UpgradeId id in SurvivorUpgrades.Roll(l, ref rng)) seen.Add(id);
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
            for (int i = 0; i < 1200; i++)
            {
                float mx = (float)System.Math.Sin(i * 0.01);
                float my = (float)System.Math.Cos(i * 0.013);
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
            for (int i = 0; i < 60 && !e.Dead; i++) sim.Step(0f, 0f);
            Assert.True(e.Dead);
            Assert.Single(sim.BurningGround);

            Puddle fire = sim.BurningGround[0];
            sim.Player = fire.Pos;
            float hp = sim.Hp;
            sim.Step(0f, 0f);
            Assert.True(sim.Hp < hp, "타는 바닥 위인데 체력이 안 줄었다");

            Run(sim, 3.1f);
            Assert.Empty(sim.BurningGround);
        }

        // --- S2 TC-6 ---
        [Fact]
        public void Boss_ArrivesAtFourMinutes_AndPuttingItOutWins()
        {
            var sim = new SurvivorSim(3);
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
        public void StandingStill_LosesWithin90Seconds()
        {
            var sim = new SurvivorSim(1);
            while (sim.Outcome == SOutcome.Playing && sim.Time < 90f)
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
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var sim = new SurvivorSim(3);
            var bot = new SurvivorBot(sim);
            int guard = 0;
            while (sim.Outcome == SOutcome.Playing && guard++ < 60 * 400) bot.Play();
            watch.Stop();
            Assert.True(sim.Time > SurvivorSim.BossAt, "시드 3이 보스까지 못 가 성능 측정이 짧아졌다");
            Assert.True(watch.Elapsed.TotalSeconds < 3.0, "한 판에 " + watch.Elapsed.TotalSeconds + "초");
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
    }
}
