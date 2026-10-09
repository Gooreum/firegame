using System.Collections.Generic;
using FireGame.Core.Sim;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 몹 다양성·강해짐(2026-10-10, docs §25): 내 레벨 단계(Lv6/12/18)가 모든 스테이지에 손님 몹(불쥐 줄·도깨비·불풍선·불곰)을 섞고
    /// 엘리트(체력 3배)를 승격한다. 마을의 제 시간표와 테스트용 직접 스폰은 그대로다.
    /// </summary>
    public class MobVarietyTests
    {
        private const int Forest = 2;

        private static SurvivorSim Quiet(int stage = Forest)
        {
            var sim = new SurvivorSim(1, stage) { Guardian = false };
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        /// <summary>seconds초 돌린다. 레벨업 카드가 뜨면 첫 장을 고르고, 판이 끝나면 멈춘다. 틱마다 신호를 모아 돌려준다.</summary>
        private static (int ratLines, bool tierSignal) Run(SurvivorSim sim, float seconds, int holdLevel = 0)
        {
            int lines = 0;
            bool tier = false;
            int ticks = (int)(seconds / SurvivorSim.Dt);
            for (int i = 0; i < ticks && sim.Outcome == SOutcome.Playing; i++)
            {
                if (holdLevel > 0) sim.Level = holdLevel;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
                lines += sim.RatLines.Count;
                tier |= sim.JustTier;
            }
            return (lines, tier);
        }

        [Fact]
        public void Tier_FollowsLevelBoundaries()
        {
            SurvivorSim sim = Quiet();
            foreach ((int level, int tier) in new[] { (1, 0), (5, 0), (6, 1), (11, 1), (12, 2), (17, 2), (18, 3), (30, 3) })
            {
                sim.Level = level;
                Assert.Equal(tier, sim.Tier);
            }
        }

        [Fact]
        public void Forest_LowLevel_PicksNoGoblins()
        {
            SurvivorSim sim = Quiet();
            sim.Level = 4;
            for (int i = 0; i < 2000; i++) Assert.NotEqual(EnemyKind.Goblin, sim.PickKind());
        }

        [Fact]
        public void Forest_Tier2_PicksGuestGoblins_AboutTenPercent()
        {
            SurvivorSim sim = Quiet();
            sim.Level = 12;
            int goblins = 0;
            for (int i = 0; i < 2000; i++) if (sim.PickKind() == EnemyKind.Goblin) goblins++;
            Assert.InRange(goblins / 2000f, 0.07f, 0.13f);
        }

        [Fact]
        public void Elite_PromotesOnlyDirectedSpawns_FromTier2()
        {
            SurvivorSim sim = Quiet();
            sim.Level = 18;   // 단계 3: 확률 0.3
            float plainHp = sim.Spawn(EnemyKind.Blaze, new Vec2(10f, 10f)).MaxHp;
            int elites = 0;
            for (int i = 0; i < 300; i++)
            {
                Enemy e = sim.Spawn(EnemyKind.Blaze, new Vec2(10f, 10f), true);
                if (!e.Elite) continue;
                elites++;
                Assert.Equal(plainHp * 3f, e.MaxHp, 3);
                Assert.True(e.Heavy);
            }
            Assert.InRange(elites, 60, 120);
            Assert.Equal(elites, sim.Stats.Elites);
            // 테스트용 직접 스폰은 승격 안 함.
            for (int i = 0; i < 100; i++) Assert.False(sim.Spawn(EnemyKind.Blaze, new Vec2(10f, 10f)).Elite);
            // 단계 1까진 감독 스폰도 승격 없음. 불씨는 엘리트 종류가 아니다.
            sim.Level = 11;
            for (int i = 0; i < 100; i++) Assert.False(sim.Spawn(EnemyKind.Blaze, new Vec2(10f, 10f), true).Elite);
            sim.Level = 18;
            for (int i = 0; i < 100; i++) Assert.False(sim.Spawn(EnemyKind.Ember, new Vec2(10f, 10f), true).Elite);
        }

        [Fact]
        public void Forest_Tier1_GuestRatLines_ReachHouses()
        {
            var sim = new SurvivorSim(3, Forest);
            Assert.False(sim.Guardian);
            (int lines, _) = Run(sim, 40f, holdLevel: 6);
            Assert.True(lines >= 1, "숲 단계 1에 손님 불쥐 줄이 없다");
            Assert.True(sim.RaiderIgnites > 0, "불쥐가 마을 밖에서는 집에 못 닿는다(MoveRaider)");
        }

        [Fact]
        public void Forest_Tier0_NoGuests()
        {
            var sim = new SurvivorSim(3, Forest);
            (int lines, bool tier) = Run(sim, 40f, holdLevel: 1);
            Assert.Equal(0, lines);
            Assert.False(tier);
            Assert.DoesNotContain(sim.Enemies, e => e.Kind == EnemyKind.Goblin || e.Kind == EnemyKind.FireBalloon || e.Kind == EnemyKind.Bear || e.Elite);
        }

        [Fact]
        public void Forest_Tier3_BearsAndBalloonsArrive()
        {
            var sim = new SurvivorSim(5, Forest);
            Run(sim, 35f, holdLevel: 18);
            Assert.True(sim.RaidersSpawned > 0);
            Assert.Contains(sim.Enemies, e => e.Kind == EnemyKind.Bear || e.Kind == EnemyKind.FireBalloon);
        }

        [Fact]
        public void Town_KeepsItsOwnSchedule_AtLevelOne()
        {
            var sim = new SurvivorSim(1, 1);
            Assert.True(sim.Guardian);
            (int lines, _) = Run(sim, 30f, holdLevel: 1);
            Assert.True(lines >= 1, "마을은 25초부터 제 시간표로 불쥐 줄");
            Assert.Equal(0, sim.Stats.Elites);
        }

        [Fact]
        public void JustTier_FiresOncePerStep()
        {
            SurvivorSim sim = Quiet();
            sim.Level = 6;
            sim.Step(0f, 0f);
            Assert.True(sim.JustTier);
            Assert.Equal(1, sim.TierShown);
            sim.Step(0f, 0f);
            Assert.False(sim.JustTier);
            sim.Level = 12;
            sim.Step(0f, 0f);
            Assert.True(sim.JustTier);
            Assert.Equal(2, sim.TierShown);
            Assert.Equal("도깨비·불풍선이 섞인다 · 엘리트 출현", SurvivorSim.TierText(2));
        }
    }
}
