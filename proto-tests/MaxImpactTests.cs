using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>최대 레벨(Lv5) 신호: 카드로 Lv5가 되는 그 순간에만 JustMaxed가 온다.</summary>
    public class MaxImpactTests
    {
        private static SurvivorSim Quiet(int seed = 1)
        {
            var sim = new SurvivorSim(seed) { Guardian = false };
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        /// <summary>구슬을 먹어 레벨업하고, 카드에 want가 있으면 고르고 없으면 첫 장을 고른다(플레이어 경로).</summary>
        private static UpgradeId LevelUp(SurvivorSim sim, UpgradeId want)
        {
            sim.DropGem(sim.Player, sim.XpToNext);
            for (int i = 0; i < 3 && sim.PendingChoices == null; i++) sim.Step(0f, 0f);
            Assert.NotNull(sim.PendingChoices);
            int k = sim.PendingChoices.IndexOf(want);
            UpgradeId pick = sim.PendingChoices[k >= 0 ? k : 0];
            sim.Choose(k >= 0 ? k : 0);
            return pick;
        }

        [Fact]
        public void ReachingLevel5_SignalsMaxOnThatPick_Only()
        {
            SurvivorSim sim = Quiet();
            int maxedSignals = 0;
            for (int guard = 0; guard < 60 && sim.Build.Level(UpgradeId.Whip) < Loadout.MaxLevel; guard++)
            {
                int before = sim.Build.Level(UpgradeId.Whip);
                UpgradeId pick = LevelUp(sim, UpgradeId.Whip);
                bool maxedNow = pick == UpgradeId.Whip && before == Loadout.MaxLevel - 1;
                if (maxedNow) Assert.Equal(UpgradeId.Whip, sim.JustMaxed);
                else if (sim.JustMaxed == UpgradeId.Whip) Assert.Fail("채찍 Lv" + (before + 1) + "에서 MAX 신호가 왔다");
                if (sim.JustMaxed == UpgradeId.Whip) maxedSignals++;
            }
            Assert.Equal(Loadout.MaxLevel, sim.Build.Level(UpgradeId.Whip));
            Assert.Equal(1, maxedSignals);
            sim.Step(0f, 0f);
            Assert.Null(sim.JustMaxed);
        }

        [Fact]
        public void BelowMax_NoSignal()
        {
            SurvivorSim sim = Quiet();
            sim.PendingChoices = new List<UpgradeId> { UpgradeId.Hose };
            sim.Choose(0);
            Assert.Equal(2, sim.Build.Level(UpgradeId.Hose));
            Assert.Null(sim.JustMaxed);
        }
    }
}
