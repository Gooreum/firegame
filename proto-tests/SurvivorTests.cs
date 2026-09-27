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
    }
}
