using System;
using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 새 무기 9종(2026-10-07): 하나는 공간 하나를 맡고, 불 몹과 건물 불을 둘 다 맞힌다.
    /// 레벨업은 개수·크기·갈래로 보이게 오르고, 진화하면 모양이 바뀐다.
    /// </summary>
    public class ArsenalTests
    {
        private static SurvivorSim Quiet()
        {
            var sim = new SurvivorSim(1, 1) { Guardian = false };
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        /// <summary>레벨업 카드에서 이 카드를 times번 고른다(Choose 경로).</summary>
        private static void Take(SurvivorSim sim, UpgradeId id, int times = 1)
        {
            for (int i = 0; i < times; i++)
            {
                sim.PendingChoices = new List<UpgradeId> { id };
                sim.Choose(0);
            }
        }

        /// <summary>무기 Lv5 + 짝 보조로 진화까지(카드 경로).</summary>
        private static void Evolve(SurvivorSim sim, UpgradeId evolution)
        {
            UpgradeId weapon = Loadout.BaseOf(evolution);
            while (sim.Build.Level(weapon) < Loadout.MaxLevel) Take(sim, weapon);
            while (sim.Build.Level(Loadout.PairOf(evolution)) < Loadout.EvolvePair) Take(sim, Loadout.PairOf(evolution));
            Take(sim, evolution);
            Assert.Equal(1, sim.Build.Level(evolution));
        }

        private static Enemy Dummy(SurvivorSim sim, EnemyKind kind, float dx, float dy, float hp = 999f)
        {
            Enemy e = sim.Spawn(kind, new Vec2(sim.Player.X + dx, sim.Player.Y + dy));
            e.Speed = 0f;
            e.MaxHp = hp;
            e.Hp = hp;
            return e;
        }

        private static Structure Shop(SurvivorSim sim, float dx, float dy, float fire = 0f, int residents = 0)
        {
            var s = new Structure { Kind = StructureKind.House, Name = "가게", Pos = new Vec2(sim.Player.X + dx, sim.Player.Y + dy), Half = new Vec2(2f, 1.5f), Residents = residents };
            s.Integrity = 100f;
            sim.Structures.Add(s);
            if (fire > 0f) sim.Ignite(s, fire);
            return s;
        }

        /// <summary>seconds 동안 흘린다. 가장자리에서 새로 나온 몹은 지운다(놓아 둔 몹만 남긴다). until이 참이면 멈춘다.</summary>
        private static void Run(SurvivorSim sim, float seconds, Func<bool> until = null, List<Enemy> keep = null)
        {
            var mine = keep ?? new List<Enemy>(sim.Enemies);
            for (int i = 0; i < (int)(seconds / SurvivorSim.Dt) && sim.Outcome == SOutcome.Playing; i++)
            {
                sim.Enemies.RemoveAll(e => !mine.Contains(e));
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
                if (until != null && until()) return;
            }
        }

        // ------------------------------------------------------------------
        // 소방견
        // ------------------------------------------------------------------

        [Fact]
        public void Dog_RunsToAnEmber_AndBitesItDown()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Dog);
            Enemy ember = Dummy(sim, EnemyKind.Ember, 5f, 0f, 20f);
            bool bit = false;
            Run(sim, 4f, () => (bit |= sim.DogBites.Count > 0) && ember.Dead);
            Assert.True(bit, "개가 물지 않았다");
            Assert.True(ember.Dead, "개가 불씨를 잡지 못했다: 체력 " + ember.Hp);
            Assert.Contains(sim.Hits, h => h.Source == HitSource.Dog);
        }

        [Fact]
        public void Dog_PrefersTheRaider_ThatGoesForAHouse()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Dog);
            Enemy chaser = Dummy(sim, EnemyKind.Ember, 3f, 0f);
            Enemy raider = Dummy(sim, EnemyKind.Ember, -6f, 0f);
            raider.Goal = Shop(sim, -10f, 0f);
            Run(sim, 0.6f);
            Assert.Same(raider, sim.Dogs[0].Target);
        }

        [Fact]
        public void Dog_CountGrowsWithLevel_OneOneTwoTwoThree()
        {
            SurvivorSim sim = Quiet();
            var seen = new List<int>();
            for (int lv = 1; lv <= Loadout.MaxLevel; lv++)
            {
                Take(sim, UpgradeId.Dog);
                Run(sim, 0.1f);
                seen.Add(sim.Dogs.Count);
            }
            Assert.Equal(new List<int> { 1, 1, 2, 2, 3 }, seen);
        }

        [Fact]
        public void Dog_WithNothingToBite_BarksAtABurningShop_AndSoaksIt()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Dog);
            Structure shop = Shop(sim, 0f, 5f, 0.6f);
            float before = shop.Fire;
            Run(sim, 5f);
            Assert.True(shop.Fire < before - 0.2f, "개가 짖으며 적셔야: " + before + " → " + shop.Fire);
        }

        [Fact]
        public void DogPack_FourDogs_PullPeopleOut()
        {
            SurvivorSim sim = Quiet();
            Evolve(sim, UpgradeId.DogPack);
            Structure shop = Shop(sim, 0f, 6f, 1f, 2);
            Run(sim, 0.1f);
            Assert.Equal(SurvivorSim.DogPackCount, sim.Dogs.Count);
            int rescued = sim.Rescued;
            Run(sim, 8f);
            Assert.True(sim.Rescued > rescued, "구조견이 사람을 물고 나와야: 남은 " + shop.Residents + " 불 " + shop.Fire);
        }

        // ------------------------------------------------------------------
        // 호스 채찍
        // ------------------------------------------------------------------

        [Fact]
        public void Whip_HitsAndPushesAnEmberInItsCircle()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Whip);
            Enemy ember = Dummy(sim, EnemyKind.Ember, 2f, 0f);
            Run(sim, 1f, () => ember.Hp < ember.MaxHp);
            Assert.True(ember.Hp < ember.MaxHp, "채찍에 안 맞았다");
            Assert.Contains(sim.Hits, h => h.Source == HitSource.Whip);
            Run(sim, 0.2f);
            Assert.True(ember.Pos.DistanceTo(sim.Player) > 2.2f, "바깥으로 밀려나야: " + ember.Pos.DistanceTo(sim.Player));
        }

        [Fact]
        public void Whip_DoesNotReachBeyondItsRadius()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Whip);
            Enemy far = Dummy(sim, EnemyKind.Ember, 4f, 0f);
            Run(sim, 2f);
            Assert.Equal(far.MaxHp, far.Hp);
        }

        [Fact]
        public void Whip_WidensAndSplitsWithLevel()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Whip);
            float r1 = sim.WhipRadius;
            Take(sim, UpgradeId.Whip, 4);
            Assert.True(sim.WhipRadius > r1 + 1f, "반경 " + r1 + " → " + sim.WhipRadius);
            Assert.Equal(new[] { 0, 1, 1, 2, 2, 3 }, SurvivorSim.WhipArms);
        }

        [Fact]
        public void Whirl_LeavesSpinningRings_ThatBurnEmbersLater()
        {
            SurvivorSim sim = Quiet();
            Evolve(sim, UpgradeId.Whirl);
            Run(sim, 0.5f);
            Assert.NotEmpty(sim.WhirlMarks);
            // 고리가 남은 자리에 놓인 불씨는 채찍이 지나간 뒤에도 피해를 입는다.
            WhirlMark mark = sim.WhirlMarks[0];
            Enemy ember = sim.Spawn(EnemyKind.Ember, mark.Pos);
            ember.Speed = 0f;
            ember.MaxHp = ember.Hp = 999f;
            ember.WhipCool = 99f;
            Run(sim, 0.5f);
            Assert.True(ember.Hp < 999f, "고리가 지져야");
        }

        // ------------------------------------------------------------------
        // 사다리차
        // ------------------------------------------------------------------

        [Fact]
        public void Ladder_StrikesAWholeLineOfEmbers()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Ladder);
            var row = new List<Enemy>();
            for (int k = 0; k < 4; k++) row.Add(Dummy(sim, EnemyKind.Ember, 1.5f + (1.2f * k), 0f));
            Run(sim, 1f, () => sim.LadderStrikes.Count > 0);
            Assert.NotEmpty(sim.LadderStrikes);
            Assert.All(row, e => Assert.True(e.Hp < e.MaxHp, "한 줄이 다 맞아야"));
        }

        [Fact]
        public void Ladder_SoaksTheShopItLandsOn_AndBringsSomeoneDown()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Ladder);
            Structure shop = Shop(sim, 5f, 0f, 0.5f, 2);
            float fire = shop.Fire;
            Run(sim, 1f, () => sim.LadderStrikes.Count > 0);
            Assert.True(shop.Fire < fire, "사다리 물이 지붕에 닿아야");
            Assert.Equal(1, shop.Residents);
        }

        [Fact]
        public void Ladder_ReachesFurther_AndSplitsWithLevel()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Ladder, Loadout.MaxLevel);
            Run(sim, 1f, () => sim.LadderStrikes.Count > 0);
            Assert.Equal(3, sim.LadderStrikes.Count);
            Assert.All(sim.LadderStrikes, l => Assert.Equal(10f, l.Len, 2));
        }

        [Fact]
        public void LadderBridge_StaysAndPushesEmbersOffTheLine()
        {
            SurvivorSim sim = Quiet();
            Evolve(sim, UpgradeId.LadderBridge);
            Run(sim, 1f, () => sim.LadderStrikes.Count > 0);
            Ladder l = sim.Ladders[0];
            Assert.Equal(SurvivorSim.BridgeLife, l.Life);
            // 다리 위에 놓인 불씨는 옆으로 밀려나며 지진다.
            var on = new Vec2(l.From.X + (l.Dir.X * 4f), l.From.Y + (l.Dir.Y * 4f));
            Enemy ember = sim.Spawn(EnemyKind.Ember, on);
            ember.Speed = 0f;
            ember.MaxHp = ember.Hp = 999f;
            Run(sim, 0.6f);
            Assert.True(ember.Hp < 999f);
            float off = Math.Abs(((ember.Pos.X - l.From.X) * -l.Dir.Y) + ((ember.Pos.Y - l.From.Y) * l.Dir.X));
            Assert.True(off > 0.5f, "다리에서 밀려나야: " + off);
        }
    }
}
