using FireGame.Core.Sim;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>불 규칙: 큰 불은 물이 덜 먹히고, 크게 타는 건물은 옆 건물로 옮겨붙고, 신고 불은 세게 붙는다.</summary>
    public class FireRuleTests
    {
        private static SurvivorSim Quiet(int seed = 1)
        {
            var sim = new SurvivorSim(seed);
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        private static Structure Shop(SurvivorSim sim, float dx, float dy)
        {
            var s = new Structure { Kind = StructureKind.House, Name = "가게", Pos = new Vec2(sim.Player.X + dx, sim.Player.Y + dy), Half = new Vec2(2f, 1.5f), Residents = 1 };
            sim.Structures.Add(s);
            return s;
        }

        /// <summary>불 세기 start인 가게에 물대포를 ticks만큼 쏘고 줄어든 양을 돌려준다(적은 매 틱 치운다).</summary>
        private static float Knockdown(float start, int ticks)
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 6f, 0f);
            sim.Ignite(shop, start);
            for (int i = 0; i < ticks; i++)
            {
                sim.Enemies.Clear();
                sim.Aim = new Vec2(shop.Pos.X - sim.Player.X, shop.Pos.Y - sim.Player.Y);
                sim.Spraying = true;
                sim.Step(0f, 0f);
            }
            return start - shop.Fire;
        }

        [Fact]
        public void BigFire_TakesLessFromTheSameWater()
        {
            float small = Knockdown(0.5f, 40);
            float big = Knockdown(1f, 40);
            Assert.True(big < small, "같은 물로 큰 불(" + big + ")이 작은 불(" + small + ")보다 덜 줄어야 한다");
            Assert.True(big > 0f, "큰 불도 물을 맞으면 줄어야 한다");
        }

        [Fact]
        public void BigBuildingFire_SpreadsToTheNearestBuilding()
        {
            SurvivorSim sim = Quiet();
            Structure burning = Shop(sim, 8f, 0f);
            Structure next = Shop(sim, 8f, 7f);
            sim.Ignite(burning, 0.9f);
            bool spread = false;
            for (int i = 0; i < (int)((sim.Stage.SpreadEvery + 0.2f) / SurvivorSim.Dt) && !spread; i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                if (sim.Spread.Contains(next)) spread = true;
            }
            Assert.True(spread, "크게 타는 가게에서 옆 가게로 안 옮겨붙었다");
            Assert.True(next.Burning);
            Assert.Equal(1, sim.Stats.Spreads);
        }

        [Fact]
        public void WetBuilding_DoesNotCatchTheSpread()
        {
            SurvivorSim sim = Quiet();
            Structure burning = Shop(sim, 8f, 0f);
            Structure next = Shop(sim, 8f, 7f);
            sim.Ignite(burning, 0.9f);
            int ticks = (int)((sim.Stage.SpreadEvery + 0.5f) / SurvivorSim.Dt);
            for (int i = 0; i < ticks; i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                next.Wet = 1f;
                sim.Step(0f, 0f);
            }
            Assert.False(next.Burning, "젖은 가게에 번졌다");
        }

        [Fact]
        public void FarBuilding_IsOutOfSpreadRange()
        {
            SurvivorSim sim = Quiet();
            Structure burning = Shop(sim, 0f, 8f);
            // 가장자리 거리 = 20 − 4 = 16칸 > SpreadRange.
            Structure far = Shop(sim, 20f, 8f);
            sim.Ignite(burning, 0.9f);
            Assert.Null(sim.NextBuilding(burning));
            int ticks = (int)((sim.Stage.SpreadEvery + 0.5f) / SurvivorSim.Dt);
            for (int i = 0; i < ticks; i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
            }
            Assert.False(far.Burning);
        }

        [Fact]
        public void Report_IgnitesAtReportFire()
        {
            var sim = new SurvivorSim(1);
            Structure hit = null;
            while (hit == null && sim.Time < SurvivorSim.ReportTimes[0] + 0.1f)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
                hit = sim.Ignited.Find(s => s.Kind == StructureKind.House);
            }
            Assert.NotNull(hit);
            Assert.True(hit.Fire >= SurvivorSim.ReportFire, "신고 불 세기 " + hit.Fire);
        }
    }
}
