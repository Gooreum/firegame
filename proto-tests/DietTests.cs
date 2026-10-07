using System.Collections.Generic;
using System.Linq;
using FireGame.Core.Sim;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 아이템 다이어트(docs §16): 무기 3칸·보조 2칸(노란 특수 장비는 2026-10-07에 없앴다), 구조대원은 늘 한 명(분대 둘).
    /// 3:00에 모든 판이 같은 풀장비가 되던 것을 막는다.
    /// </summary>
    public class DietTests
    {
        [Fact]
        public void ThreeWeaponsTwoPassives_CapTheBuild()
        {
            UpgradeId[] weapons = { UpgradeId.Hose, UpgradeId.WaterBomb, UpgradeId.Drone, UpgradeId.Partner, UpgradeId.Curtain, UpgradeId.Turret };
            int reached = 0;
            for (int seed = 1; seed <= 10; seed++)
            {
                var sim = new SurvivorSim(seed, 1) { Guardian = false };
                var bot = new SurvivorBot(sim) { Pro = true };
                while (sim.Outcome == SOutcome.Playing && !sim.Finale) bot.Play();
                // 3:00 전에 진 판은 상한을 볼 자리가 아니다(바닥은 CloseReport가 잰다).
                if (!sim.Finale) continue;
                reached++;
                var owned = sim.Build.Owned().ToList();
                // 진화하면 원래 무기 레벨은 0이 되므로 진화를 그 무기로 되돌려 센다.
                int baseWeapons = owned.Count(id => weapons.Contains(id)) + owned.Count(Loadout.IsEvolution);
                int passives = owned.Count(Loadout.IsPassive);
                Assert.True(baseWeapons <= Loadout.WeaponSlots, "시드 " + seed + ": 무기 " + baseWeapons + "종");
                Assert.True(passives <= Loadout.PassiveSlots, "시드 " + seed + ": 보조 " + passives + "종");
            }
            Assert.True(reached >= 5, "대화재까지 간 판이 너무 적다: " + reached + "/10");
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

        [Fact]
        public void Partner_IsAlwaysOne_AndLevelsSpeedNotBodies()
        {
            SurvivorSim sim = Quiet();
            sim.Build.Add(UpgradeId.Partner);
            Run(sim, 0.1f);
            Assert.Single(sim.Partners);
            Assert.Equal(1f, sim.PartnerSpeedScale);
            Assert.Equal(1f, sim.PartnerRescueBoost);

            while (sim.Build.Level(UpgradeId.Partner) < Loadout.MaxLevel) sim.Build.Add(UpgradeId.Partner);
            Run(sim, 0.1f);
            // 만렙이어도 한 명. 레벨은 달리기·구조 배율로 간다.
            Assert.Single(sim.Partners);
            Assert.Equal(1.25f, sim.PartnerSpeedScale);
            Assert.Equal(1.5f, sim.PartnerRescueBoost);
            Assert.Equal(1, sim.PartnerCount);

            sim.Build.Add(UpgradeId.Boots);
            sim.Build.Add(UpgradeId.Squad);
            Run(sim, 0.1f);
            Assert.Equal(2, sim.Partners.Count);
            Assert.Equal(1.3f, sim.PartnerSpeedScale);
            Assert.Equal(2f, sim.PartnerRescueBoost);

            // 카드 글에 사람 수가 없다.
            Assert.Equal("대원 달리기 +25%", SurvivorUpgrades.Describe(UpgradeId.Partner, 3));
            Assert.Equal("구조 +25%", SurvivorUpgrades.Describe(UpgradeId.Partner, 5));
        }

        [Fact]
        public void Partner_StillRescuesAtTheDoor()
        {
            SurvivorSim sim = Quiet();
            sim.Build.Add(UpgradeId.Partner);
            var house = new Structure { Kind = StructureKind.House, Name = "가게", Pos = new Vec2(sim.Player.X + 7f, sim.Player.Y), Half = new Vec2(2f, 1.5f), Residents = 2 };
            sim.Structures.Add(house);
            sim.Ignite(house, 0.5f);
            // 소방관은 가만히 서 있고 대원만 달려간다.
            Run(sim, 20f);
            Assert.True(sim.Rescued >= 1, "대원 한 명이 문 앞에서 아무도 못 구했다: 구조 " + sim.Rescued);
        }
    }
}
