using System.Collections.Generic;
using System.Linq;
using FireGame.Core.Sim;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 칸 상한(2026-10-07): 무기·보조를 합쳐 4칸. 실제 판(숙련 봇)에서도 넘치지 않는다.
    /// </summary>
    public class DietTests
    {
        [Fact]
        public void FourSlots_CapTheBuild_InRealRuns()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                var sim = new SurvivorSim(seed, 1) { Guardian = false };
                var bot = new SurvivorBot(sim) { Pro = true };
                while (sim.Outcome == SOutcome.Playing) bot.Play();
                var owned = sim.Build.Owned().ToList();
                // 진화하면 원래 무기 레벨은 0이 되므로 진화를 그 무기 칸으로 센다.
                int weapons = owned.Count(Loadout.IsWeapon);
                int passives = owned.Count(Loadout.IsPassive);
                Assert.True(weapons + passives <= Loadout.Slots, "시드 " + seed + ": " + weapons + " + " + passives + "칸");
            }
        }


        private static SurvivorSim Quiet()
        {
            var sim = new SurvivorSim(1, 1) { Guardian = false };
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        private static void Run(SurvivorSim sim, float seconds)
        {
            for (int i = 0; i < (int)(seconds / SurvivorSim.Dt); i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Enemies.Clear();
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
            }
        }

    }
}
