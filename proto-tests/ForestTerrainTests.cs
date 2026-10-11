using System.Linq;
using FireGame.Core.Sim;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 숲 3D 지형(2026-10-12, docs §26): 개울(다리 둘)과 바위 언덕이 판정에 들어간다.
    /// 땅 몹·소방관은 개울을 못 건너 다리로 돌고, 산불은 개울을 못 넘는다. 마을 강 규칙은 그대로다.
    /// </summary>
    public class ForestTerrainTests
    {
        private const int Forest = 2;

        private static bool InWater(SurvivorSim sim, Vec2 p)
        {
            return sim.Structures.Exists(s => s.Kind == StructureKind.Water && s.Within(p, 0f));
        }

        [Fact]
        public void Creek_LeavesTwoBridgeGaps()
        {
            var sim = new SurvivorSim(1, Forest);
            Assert.True(InWater(sim, new Vec2(10f, SurvivorForest.CreekY)));
            Assert.True(InWater(sim, new Vec2(55f, SurvivorForest.CreekY)));
            Assert.False(InWater(sim, new Vec2(30f, SurvivorForest.CreekY)));
            Assert.False(InWater(sim, new Vec2(45f, SurvivorForest.CreekY)));
            Assert.True(InWater(sim, new Vec2(5f, 55f)));
            Assert.Equal(2, sim.Bridges.Count);
        }

        [Fact]
        public void NoTrees_InCreekOrOnTheRock()
        {
            var sim = new SurvivorSim(1, Forest);
            var water = sim.Structures.Where(s => s.Kind == StructureKind.Water).ToList();
            Assert.NotEmpty(sim.Structures.Where(s => s.Kind == StructureKind.Tree));
            foreach (Structure t in sim.Structures.Where(s => s.Kind == StructureKind.Tree))
            {
                Assert.DoesNotContain(water, w => w.Within(t.Pos, 0.3f));
            }
        }

        [Fact]
        public void RouteAround_SameBank_KeepsTheGoal_OtherBank_UsesABridgeEnd()
        {
            var sim = new SurvivorSim(1, Forest);
            var goal = new Vec2(20f, 55f);
            Assert.Equal(goal, sim.RouteAround(new Vec2(30f, 50f), goal));
            Vec2 step = sim.RouteAround(new Vec2(30f, 50f), new Vec2(22f, 16f));
            Assert.Equal(30f, step.X);
            Assert.True(step.Y > SurvivorForest.CreekY, "먼저 북쪽 둑 끝으로 간다");
            Vec2 cross = sim.RouteAround(step, new Vec2(22f, 16f));
            Assert.True(cross.Y < SurvivorForest.CreekY, "둑 끝에 닿으면 건너편 끝으로 간다");
        }

        [Fact]
        public void Blaze_CrossesByTheBridge_AndReachesTheFirefighter()
        {
            var sim = new SurvivorSim(1, Forest) { Reports = false };
            sim.Enemies.Clear();
            sim.Player = new Vec2(15f, 30f);
            Enemy e = sim.Spawn(EnemyKind.Blaze, new Vec2(15f, 50f));
            e.MaxHp = e.Hp = 1e6f;
            float best = float.MaxValue;
            int ticks = (int)(30f / SurvivorSim.Dt);
            for (int i = 0; i < ticks && sim.Outcome == SOutcome.Playing && !e.Dead; i++)
            {
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Player = new Vec2(15f, 30f);
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                best = System.Math.Min(best, e.Pos.DistanceTo(sim.Player));
                if (best <= 3f) break;
            }
            Assert.True(best <= 3f, $"큰 불이 개울에 갇혔다(가장 가까이 {best:F1}칸, 마지막 {e.Pos.X:F1},{e.Pos.Y:F1})");
        }

        [Fact]
        public void Wildfire_DoesNotJumpTheCreek()
        {
            var sim = new SurvivorSim(1, Forest);
            Structure north = sim.Structures.First(s => s.Kind == StructureKind.Tree && s.Pos.Y > SurvivorForest.CreekY + 2f);
            Structure south = sim.Structures.First(s => s.Kind == StructureKind.Tree && s.Pos.Y < SurvivorForest.CreekY - 2f);
            Assert.True(sim.CrossesWater(north.Pos, south.Pos, true));
        }

        [Fact]
        public void Town_HasNoBridges_KeepsItsRiverRule()
        {
            var sim = new SurvivorSim(1, 1);
            Assert.Empty(sim.Bridges);
            var p = new Vec2(10f, 10f);
            Assert.Equal(p, sim.RouteAround(new Vec2(50f, 10f), p));
        }

        [Fact]
        public void Heights_HillTopsHoldBuildings_CampIsFlat_CreekIsLow_BridgeIsLevel()
        {
            Assert.InRange(SurvivorForest.Height(12f, 16f), 2.7f, 2.9f);   // 전망대
            Assert.InRange(SurvivorForest.Height(12f, 28f), 2.7f, 2.9f);   // 산장
            Assert.InRange(SurvivorForest.Height(48f, 16f), 2.3f, 2.5f);   // 숲 식당
            Assert.Equal(0f, SurvivorForest.Height(30f, 30f));
            Assert.True(SurvivorForest.Height(10f, SurvivorForest.CreekY) < -0.5f);
            Assert.Equal(0f, SurvivorForest.Height(30f, SurvivorForest.CreekY));
        }

        [Fact]
        public void Slope_UphillIsSlower_DownhillIsFaster_FlatIsOne()
        {
            var sim = new SurvivorSim(1, Forest);
            var side = new Vec2(17.5f, 22f);   // 능선 동쪽 비탈
            Assert.True(sim.SlopeSpeed(side, -1f, 0f) < 0.9f, "오르막은 느리다");
            Assert.True(sim.SlopeSpeed(side, 1f, 0f) > 1.05f, "내리막은 빠르다");
            Assert.Equal(1f, sim.SlopeSpeed(new Vec2(30f, 30f), 1f, 0f));
            Assert.Equal(1f, new SurvivorSim(1, 1).SlopeSpeed(side, -1f, 0f));
        }

        [Fact]
        public void HighGround_ReachesFarther()
        {
            var sim = new SurvivorSim(1, Forest);
            sim.Player = new Vec2(30f, 30f);
            float flat = sim.SWide;
            sim.Player = new Vec2(12f, 20f);
            Assert.True(sim.SWide > flat * 1.15f, $"언덕 위 {sim.SWide:F2} vs 평지 {flat:F2}");
        }
    }
}
