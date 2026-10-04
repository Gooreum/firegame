using System.Collections.Generic;
using FireGame.Core.Sim;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 스테이지별 준비: 위협을 막는 장비(Counters)는 소방서에서 하나 들고 가고 카드로 두 배 나오며,
    /// 위협과 상관없는 장비(Excluded)는 그 스테이지 카드에서 안 나온다.
    /// </summary>
    public class PrepTests
    {
        private static readonly UpgradeId[] Regular =
        {
            UpgradeId.Hose, UpgradeId.WaterBomb, UpgradeId.Drone, UpgradeId.Partner, UpgradeId.Curtain, UpgradeId.Turret,
            UpgradeId.Tank, UpgradeId.Boots, UpgradeId.Suit,
        };

        /// <summary>빈 장비(물대포 Lv1)로 n번 뽑아 일반 카드가 몇 번 나왔는지 센다. 노란 카드는 안 센다.</summary>
        private static Dictionary<UpgradeId, int> Count(StageRules stage, int n)
        {
            var seen = new Dictionary<UpgradeId, int>();
            foreach (UpgradeId id in Regular) seen[id] = 0;
            var rng = new Rng(7);
            for (int i = 0; i < n; i++)
            {
                var loadout = new Loadout();
                loadout.Add(UpgradeId.Hose);
                foreach (UpgradeId id in SurvivorUpgrades.Roll(loadout, 2, ref rng, stage.Specials, false, stage))
                {
                    if (seen.ContainsKey(id)) seen[id]++;
                }
            }
            return seen;
        }

        private static float AverageOfOthers(Dictionary<UpgradeId, int> seen, params UpgradeId[] except)
        {
            int sum = 0;
            int k = 0;
            foreach (KeyValuePair<UpgradeId, int> pair in seen)
            {
                if (System.Array.IndexOf(except, pair.Key) >= 0 || pair.Value == 0) continue;
                sum += pair.Value;
                k++;
            }
            return k == 0 ? 0f : sum / (float)k;
        }

        [Fact]
        public void Forest_NeverRollsTurret_AndRollsCountersTwiceAsOften()
        {
            StageRules forest = SurvivorStages.Get(2);
            Assert.Equal(new[] { UpgradeId.Curtain, UpgradeId.Drone }, forest.Counters);
            Assert.Equal(new[] { UpgradeId.Turret }, forest.Excluded);
            Dictionary<UpgradeId, int> seen = Count(forest, 600);
            Assert.Equal(0, seen[UpgradeId.Turret]);
            float others = AverageOfOthers(seen, UpgradeId.Curtain, UpgradeId.Drone, UpgradeId.Turret);
            Assert.True(seen[UpgradeId.Curtain] >= others * 1.5f, "장막 " + seen[UpgradeId.Curtain] + " 대 평균 " + others);
            Assert.True(seen[UpgradeId.Drone] >= others * 1.5f, "드론 " + seen[UpgradeId.Drone] + " 대 평균 " + others);
            // 한 번에 같은 카드가 둘 나오지는 않는다.
            var rng = new Rng(3);
            for (int i = 0; i < 100; i++)
            {
                var loadout = new Loadout();
                loadout.Add(UpgradeId.Hose);
                List<UpgradeId> cards = SurvivorUpgrades.Roll(loadout, 2, ref rng, forest.Specials, false, forest);
                Assert.Equal(cards.Count, new HashSet<UpgradeId>(cards).Count);
            }
        }

        [Fact]
        public void Factory_NeverRollsDrone()
        {
            StageRules factory = SurvivorStages.Get(3);
            Assert.Equal(new[] { UpgradeId.Boots, UpgradeId.Turret }, factory.Counters);
            Assert.Equal(new[] { UpgradeId.Drone }, factory.Excluded);
            Dictionary<UpgradeId, int> seen = Count(factory, 600);
            Assert.Equal(0, seen[UpgradeId.Drone]);
            Assert.True(seen[UpgradeId.Turret] > 0);
            Assert.True(seen[UpgradeId.Boots] > 0);
        }

        [Fact]
        public void Town_RollsEverything_AndTheSimUsesItsStage()
        {
            StageRules town = SurvivorStages.Get(1);
            Assert.Empty(town.Excluded);
            Assert.Equal(new[] { UpgradeId.Partner, UpgradeId.Boots }, town.Counters);
            Assert.False(string.IsNullOrEmpty(town.Threat));
            Dictionary<UpgradeId, int> seen = Count(town, 400);
            foreach (UpgradeId id in Regular) Assert.True(seen[id] > 0, SurvivorUpgrades.Name(id) + "이 한 번도 안 나왔다");

            // 실제 판(숲)에서 레벨업해도 포탑은 안 나온다: 플레이어 경로(구슬 → 레벨업 → 카드).
            for (int seed = 1; seed <= 10; seed++)
            {
                var sim = new SurvivorSim(seed, 2) { Guardian = false };
                sim.Reports = false;
                for (int level = 0; level < 12; level++)
                {
                    sim.Xp += sim.XpToNext;
                    sim.Enemies.Clear();
                    sim.Step(0f, 0f);
                    if (sim.PendingChoices == null) break;
                    Assert.DoesNotContain(UpgradeId.Turret, sim.PendingChoices);
                    sim.Choose(0);
                }
            }
        }

        [Fact]
        public void Prep_AddsOneCounterAtLv1_OnlyIfNotHeld()
        {
            var station = new FireStation();
            StageRules town = SurvivorStages.Get(1);
            Assert.Equal(new[] { UpgradeId.Hose }, station.StartFor(town));

            station.Prep = UpgradeId.Partner;
            Assert.Equal(new[] { UpgradeId.Hose, UpgradeId.Partner }, station.StartFor(town));
            var sim = new SurvivorSim(1, 1, station.StartFor(town)) { Guardian = false };
            Assert.Equal(1, sim.Build.Level(UpgradeId.Partner));
            Assert.Equal(1, sim.Build.Level(UpgradeId.Hose));

            // 구조반장은 이미 대원을 들었다: 두 번 더하지 않는다.
            station.Unlocked.Add("rescue");
            station.Selected = "rescue";
            Assert.Equal(new[] { UpgradeId.Hose, UpgradeId.Partner }, station.StartFor(town));
            sim = new SurvivorSim(1, 1, station.StartFor(town)) { Guardian = false };
            Assert.Equal(1, sim.Build.Level(UpgradeId.Partner));
        }

        [Fact]
        public void Prep_IgnoresNonCounter_AndClearsPerStage()
        {
            var station = new FireStation();
            StageRules town = SurvivorStages.Get(1);
            StageRules forest = SurvivorStages.Get(2);
            // 마을의 대비는 대원·장화뿐: 포탑을 넣어도 무시한다.
            station.Prep = UpgradeId.Turret;
            Assert.Equal(new[] { UpgradeId.Hose }, station.StartFor(town));
            // 숲의 대비(장막)는 숲에서만 붙는다.
            station.Prep = UpgradeId.Curtain;
            Assert.Equal(new[] { UpgradeId.Hose }, station.StartFor(town));
            Assert.Equal(new[] { UpgradeId.Hose, UpgradeId.Curtain }, station.StartFor(forest));
            station.Prep = null;
            Assert.Equal(new[] { UpgradeId.Hose }, station.StartFor(forest));
        }

        [Fact]
        public void EveryStage_HasAThreat_TwoCounters_AndBotKnowsFoam()
        {
            for (int n = 1; n <= SurvivorStages.Count; n++)
            {
                StageRules stage = SurvivorStages.Get(n);
                Assert.False(string.IsNullOrEmpty(stage.Threat), n + " 위협 없음");
                Assert.Equal(2, stage.Counters.Length);
                foreach (UpgradeId c in stage.Counters)
                {
                    Assert.False(Loadout.IsSpecial(c));
                    Assert.DoesNotContain(c, stage.Excluded);
                }
            }
            // 봇 카드 우선순위에 거품(공단 노란 카드)이 있어야 공단에서 노란 카드를 버리지 않는다.
            Assert.Equal(0, SurvivorBot.PickCard(new List<UpgradeId> { UpgradeId.Foam, UpgradeId.Boots, UpgradeId.Suit }));
        }
    }
}
