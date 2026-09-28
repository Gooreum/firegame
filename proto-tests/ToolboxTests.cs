using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>공구상자: 부서진 건물이 있으면 40초마다 떨어지고, 주우면 가장 약한 건물을 고친다.</summary>
    public class ToolboxTests
    {
        private static SurvivorSim Quiet()
        {
            var sim = new SurvivorSim(1);
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        private static Structure Shop(SurvivorSim sim, float dx, float dy, float integrity)
        {
            var s = new Structure { Kind = StructureKind.House, Name = "가게", Pos = new Vec2(sim.Player.X + dx, sim.Player.Y + dy), Half = new Vec2(2f, 1.5f), Integrity = integrity };
            sim.Structures.Add(s);
            return s;
        }

        /// <summary>스폰 감독이 끼어들지 않게 적을 치우며 시간을 보낸다.</summary>
        private static void Run(SurvivorSim sim, float seconds)
        {
            int ticks = (int)(seconds / SurvivorSim.Dt);
            for (int i = 0; i < ticks; i++)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
            }
        }

        /// <summary>플레이어처럼 상자 쪽으로 걸어간다(최대 5초).</summary>
        private static void WalkTo(SurvivorSim sim, Vec2 at)
        {
            for (int i = 0; i < 300 && sim.Toolboxes.Count > 0; i++)
            {
                sim.Enemies.Clear();
                float dx = at.X - sim.Player.X;
                float dy = at.Y - sim.Player.Y;
                float d = (float)System.Math.Sqrt((dx * dx) + (dy * dy));
                sim.Step(dx / d, dy / d);
            }
        }

        [Fact]
        public void NoDamage_NoToolbox()
        {
            SurvivorSim sim = Quiet();
            Shop(sim, 20f, 20f, 1f);
            Run(sim, SurvivorSim.ToolboxEvery + 1f);
            Assert.Empty(sim.Toolboxes);
        }

        [Fact]
        public void DamagedBuilding_DropsAToolboxAt40s_OnFreeGround()
        {
            SurvivorSim sim = Quiet();
            Shop(sim, 20f, 20f, 0.5f);
            Run(sim, SurvivorSim.ToolboxEvery - 1f);
            Assert.Empty(sim.Toolboxes);
            Run(sim, 1.5f);
            Pickup box = Assert.Single(sim.Toolboxes);
            float d = box.Pos.DistanceTo(sim.Player);
            Assert.InRange(d, 4.8f, 12.01f);
            Assert.DoesNotContain(sim.Structures, st => st.Within(box.Pos, 0.8f));
        }

        [Fact]
        public void PickingUp_RepairsTheWeakest_BurningFirst()
        {
            SurvivorSim sim = Quiet();
            Structure cracked = Shop(sim, 20f, 20f, 0.3f);
            Structure burning = Shop(sim, -20f, 20f, 1f);
            Run(sim, SurvivorSim.ToolboxEvery + 0.5f);
            Pickup box = Assert.Single(sim.Toolboxes);
            // 상자가 떨어진 뒤 다른 가게에 불이 붙어 반쯤 탔다.
            burning.Integrity = 0.6f;
            burning.Fire = 0.8f;
            float before = burning.Integrity;
            WalkTo(sim, box.Pos);
            Assert.Empty(sim.Toolboxes);
            Assert.True(burning.Integrity >= System.Math.Min(1f, before + SurvivorSim.ToolboxRepair) - 0.05f, "타는 건물이 먼저 고쳐져야 한다: " + before + " → " + burning.Integrity);
            Assert.True(burning.Fire < 0.5f);
            Assert.True(burning.Wet > 0f);
            Assert.Equal(0.3f, cracked.Integrity, 3);
        }

        [Fact]
        public void Toolbox_VanishesAfter20s()
        {
            SurvivorSim sim = Quiet();
            Shop(sim, 20f, 20f, 0.5f);
            Run(sim, SurvivorSim.ToolboxEvery + 0.5f);
            Assert.Single(sim.Toolboxes);
            Run(sim, SurvivorSim.ToolboxLife + 0.1f);
            Assert.Empty(sim.Toolboxes);
        }

        [Fact]
        public void NothingToRepair_HealsInstead()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 20f, 20f, 0.5f);
            Run(sim, SurvivorSim.ToolboxEvery + 0.5f);
            Pickup box = Assert.Single(sim.Toolboxes);
            shop.Integrity = 1f;
            sim.Hp = 50f;
            WalkTo(sim, box.Pos);
            Assert.Empty(sim.Toolboxes);
            Assert.True(sim.Hp >= 50f + SurvivorSim.ToolboxHeal - 0.1f, "체력 " + sim.Hp);
        }
    }
}
