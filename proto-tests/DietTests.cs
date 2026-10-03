using System.Collections.Generic;
using System.Linq;
using FireGame.Core.Sim;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 아이템 다이어트(docs §16): 무기 3칸·보조 2칸, 노란 특수 장비는 한 판에 하나, 구조대원은 늘 한 명(분대 둘).
    /// 3:00에 모든 판이 같은 풀장비가 되던 것을 막는다.
    /// </summary>
    public class DietTests
    {
        private static readonly List<UpgradeId> TownPool = new List<UpgradeId> { UpgradeId.Heli, UpgradeId.Ambulance, UpgradeId.Truck, UpgradeId.Sprinkler };

        private static bool IsYellow(UpgradeId id)
        {
            return Loadout.IsSpecial(id) && !Loadout.IsEvolution(id);
        }

        [Fact]
        public void OneYellowPerRun()
        {
            var bare = new Loadout();
            bare.Add(UpgradeId.Hose);
            Assert.True(bare.CanTake(UpgradeId.Ambulance), "노란 카드가 없을 땐 들 수 있어야 한다");
            Assert.Equal(0, bare.SpecialCount);

            var held = new Loadout();
            held.Add(UpgradeId.Hose);
            held.Add(UpgradeId.Heli);
            Assert.Equal(1, held.SpecialCount);
            Assert.False(held.CanTake(UpgradeId.Ambulance));
            Assert.False(held.CanTake(UpgradeId.Heli));

            // 상자 첫 장(노란 보장)도, 5의 배수 레벨도 둘째 노란을 내지 않는다.
            var rng = new Rng(21);
            int yellows = 0;
            for (int i = 0; i < 500; i++)
            {
                yellows += SurvivorUpgrades.Roll(held, 5, ref rng, TownPool, true).Count(IsYellow);
                yellows += SurvivorUpgrades.Roll(held, 10, ref rng, TownPool).Count(IsYellow);
            }
            Assert.Equal(0, yellows);

            int bareYellows = 0;
            for (int i = 0; i < 50; i++) bareYellows += SurvivorUpgrades.Roll(bare, 5, ref rng, TownPool, true).Count(IsYellow);
            Assert.Equal(50, bareYellows);
        }

        [Fact]
        public void Evolution_StillOffered_WithYellowHeld()
        {
            var l = new Loadout();
            for (int i = 0; i < Loadout.MaxLevel; i++) l.Add(UpgradeId.Hose);
            l.Add(UpgradeId.Tank);
            l.Add(UpgradeId.Heli);
            var rng = new Rng(4);
            for (int i = 0; i < 20; i++)
            {
                List<UpgradeId> cards = SurvivorUpgrades.Roll(l, 6, ref rng, TownPool);
                Assert.Contains(UpgradeId.Cannon, cards);
                Assert.DoesNotContain(cards, IsYellow);
            }
        }

        [Fact]
        public void ThreeWeaponsTwoPassives_CapTheBuild()
        {
            UpgradeId[] weapons = { UpgradeId.Hose, UpgradeId.WaterBomb, UpgradeId.Drone, UpgradeId.Partner, UpgradeId.Curtain, UpgradeId.Turret };
            int reached = 0;
            for (int seed = 1; seed <= 10; seed++)
            {
                var sim = new SurvivorSim(seed, 1);
                var bot = new SurvivorBot(sim) { Pro = true };
                while (sim.Outcome == SOutcome.Playing && !sim.Finale) bot.Play();
                // 3:00 전에 진 판은 상한을 볼 자리가 아니다(바닥은 CloseReport가 잰다).
                if (!sim.Finale) continue;
                reached++;
                var owned = sim.Build.Owned().ToList();
                // 진화하면 원래 무기 레벨은 0이 되므로 진화를 그 무기로 되돌려 센다.
                int baseWeapons = owned.Count(id => weapons.Contains(id)) + owned.Count(Loadout.IsEvolution);
                int passives = owned.Count(Loadout.IsPassive);
                int yellows = owned.Count(IsYellow);
                Assert.True(baseWeapons <= Loadout.WeaponSlots, "시드 " + seed + ": 무기 " + baseWeapons + "종");
                Assert.True(passives <= Loadout.PassiveSlots, "시드 " + seed + ": 보조 " + passives + "종");
                Assert.True(yellows <= Loadout.SpecialSlots, "시드 " + seed + ": 노란 " + yellows + "장");
            }
            Assert.True(reached >= 5, "대화재까지 간 판이 너무 적다: " + reached + "/10");
        }
    }
}
