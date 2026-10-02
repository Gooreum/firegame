using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 열기: 타는 건물 곁은 뜨겁다. 몸 압박이 목표(건물·사람)를 하는 자리에서 생기고, 방화복이 그걸 줄인다.
    /// </summary>
    public class HeatTests
    {
        private static SurvivorSim Quiet()
        {
            var sim = new SurvivorSim(1);
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        private static void Take(SurvivorSim sim, UpgradeId id, int times = 1)
        {
            for (int i = 0; i < times; i++)
            {
                sim.PendingChoices = new List<UpgradeId> { id };
                sim.Choose(0);
            }
        }

        /// <summary>불 1.0 건물 가장자리에서 gap칸 떨어져 1초 서 있을 때 받은 피해.</summary>
        private static float HeatFor(float gap, int suit)
        {
            SurvivorSim sim = Quiet();
            var shop = new Structure { Kind = StructureKind.House, Name = "가게", Pos = new Vec2(sim.Player.X, sim.Player.Y + 1.5f + gap), Half = new Vec2(2f, 1.5f), Integrity = 100f };
            sim.Structures.Add(shop);
            sim.Ignite(shop, 1f);
            if (suit > 0) Take(sim, UpgradeId.Suit, suit);
            float hp = sim.Hp;
            bool signal = false;
            for (int i = 0; i < 60; i++)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
                signal |= sim.HeatHurt > 0f;
            }
            Assert.Equal(gap <= SurvivorSim.HeatRange, signal);
            return hp - sim.Hp;
        }

        [Fact]
        public void StandingByABlazingBuilding_Hurts()
        {
            Assert.InRange(HeatFor(2f, 0), SurvivorSim.HeatDps * 0.95f, SurvivorSim.HeatDps * 1.05f);
        }

        [Fact]
        public void OutsideTheHeatRange_NoHarm()
        {
            Assert.Equal(0f, HeatFor(3f, 0), 3);
        }

        [Fact]
        public void Suit_CutsTheHeat()
        {
            // 방화복 Lv2: 레벨마다 −10%라 80%.
            float plain = HeatFor(2f, 0);
            float suit = HeatFor(2f, 2);
            Assert.InRange(suit / plain, 0.78f, 0.82f);
        }

        [Fact]
        public void Rescue_HealsOnlyALittle()
        {
            SurvivorSim sim = Quiet();
            var shop = new Structure { Kind = StructureKind.House, Name = "가게", Pos = new Vec2(sim.Player.X, sim.Player.Y + 20f), Half = new Vec2(2f, 1.5f), Residents = 1 };
            sim.Structures.Add(shop);
            sim.Ignite(shop, 0.2f);
            sim.Player = shop.Door;
            sim.Hp = 50f;
            for (int i = 0; i < 120 && sim.Rescued == 0; i++)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
            }
            Assert.Equal(1, sim.Rescued);
            Assert.InRange(sim.Hp, 50f, 50f + SurvivorSim.RescueHeal + 0.01f);
        }
    }
}
