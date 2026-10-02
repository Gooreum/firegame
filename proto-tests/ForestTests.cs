using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>2스테이지 산불 숲: 맵, 바람 따라 나무끼리 번지는 불, 불다람쥐, 재 박쥐 무리.</summary>
    public class ForestTests
    {
        private static SurvivorSim Quiet()
        {
            var sim = new SurvivorSim(1, 2);
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        private static Structure Tree(SurvivorSim sim, float dx, float dy)
        {
            var s = new Structure { Kind = StructureKind.Tree, Name = "나무", Pos = new Vec2(sim.Player.X + dx, sim.Player.Y + dy), Half = new Vec2(0.6f, 0.6f) };
            sim.Structures.Add(s);
            return s;
        }

        /// <summary>불씨가 끼어들지 않게 적을 치우며 시간을 보낸다(바람 번짐만 본다).</summary>
        private static void RunClear(SurvivorSim sim, float seconds)
        {
            int ticks = (int)(seconds / SurvivorSim.Dt);
            for (int i = 0; i < ticks; i++)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
            }
        }

        [Fact]
        public void ForestMap_HasEightBuildings_ManyTrees_AndAClearCenter()
        {
            var sim = new SurvivorSim(1, 2);
            Assert.Equal("산불 숲", sim.Stage.Name);
            Assert.Equal(9, sim.HousesTotal);
            Assert.Single(sim.Structures.FindAll(s => s.Kind == StructureKind.Depot));
            var trees = sim.Structures.FindAll(s => s.Kind == StructureKind.Tree);
            Assert.True(trees.Count >= 60, "나무 " + trees.Count);
            // 숲은 북쪽, 캠프는 남쪽: 나무는 모두 y ≥ 33, 건물은 모두 y ≤ 30.
            Assert.All(trees, t => Assert.True(t.Pos.Y >= SurvivorForest.ForestFrom, "나무가 캠프 쪽에 있다: " + t.Pos.Y));
            Assert.All(sim.Structures.FindAll(s => s.IsBuilding), b => Assert.True(b.Pos.Y <= 30f, b.Name + "이 숲 쪽에 있다"));
            // 첫 줄 지붕과 첫 나무 줄 틈은 바람 사거리 안(산불이 캠프로 넘어온다).
            float lowestTree = 99f;
            foreach (Structure t in trees) lowestTree = System.Math.Min(lowestTree, t.Pos.Y);
            Assert.InRange(lowestTree - 29.5f - 0.6f, 2f, SurvivorSim.WindSpreadRange);
            Assert.All(sim.Structures, s => Assert.True(s.DistanceTo(sim.Player) > 6f, s.Name + "가 출발점에 너무 가깝다"));
            Assert.All(sim.Structures, s => Assert.False(s.Burning));
        }

        [Fact]
        public void BurningTree_SpreadsDownwind()
        {
            SurvivorSim sim = Quiet();
            RunClear(sim, 0.1f);
            sim.Wind = new Vec2(1f, 0f);
            // 옮을 확률이 30%라 서로 멀리 떨어진 나무 쌍 여섯을 두고, 끄지 않은 채 다 탈 때까지 본다.
            var downwind = new System.Collections.Generic.List<Structure>();
            for (int k = 0; k < 6; k++)
            {
                float dy = -25f + (k * 10f);
                Structure fire = Tree(sim, -20f, dy);
                downwind.Add(Tree(sim, -17f, dy));
                sim.Ignite(fire, 0.5f);
            }
            RunClear(sim, 30f);
            Assert.Contains(downwind, t => t.Burning || t.Collapsed);
        }

        [Fact]
        public void BurningTree_SpreadsDownwind_IntoABuilding()
        {
            SurvivorSim sim = Quiet();
            RunClear(sim, 0.1f);
            sim.Wind = new Vec2(1f, 0f);
            // 나무 불은 지킬 건물을 위협한다: 바람 쪽 건물로도 옮는다(30%라 여섯 쌍).
            var shops = new System.Collections.Generic.List<Structure>();
            for (int k = 0; k < 6; k++)
            {
                float dy = -25f + (k * 10f);
                // 가장자리 스폰(17칸)이 닿지 않게 22칸 밖에 둔다.
                Structure fire = Tree(sim, -26f, dy);
                var shop = new Structure { Kind = StructureKind.House, Name = "산장", Pos = new Vec2(sim.Player.X - 22f, sim.Player.Y + dy), Half = new Vec2(2f, 1.5f) };
                sim.Structures.Add(shop);
                shops.Add(shop);
                sim.Ignite(fire, 0.5f);
            }
            RunClear(sim, 30f);
            Assert.Contains(shops, s => s.Burning || s.Collapsed);
        }

        [Fact]
        public void BurningTree_DoesNotSpreadUpwind_IntoABuilding()
        {
            SurvivorSim sim = Quiet();
            RunClear(sim, 0.1f);
            sim.Wind = new Vec2(1f, 0f);
            var shops = new System.Collections.Generic.List<Structure>();
            for (int k = 0; k < 6; k++)
            {
                float dy = -25f + (k * 10f);
                Structure fire = Tree(sim, 26f, dy);
                var shop = new Structure { Kind = StructureKind.House, Name = "산장", Pos = new Vec2(sim.Player.X + 22f, sim.Player.Y + dy), Half = new Vec2(2f, 1.5f) };
                sim.Structures.Add(shop);
                shops.Add(shop);
                sim.Ignite(fire, 0.5f);
            }
            RunClear(sim, 30f);
            Assert.DoesNotContain(shops, s => s.Burning || s.Collapsed);
        }

        [Fact]
        public void ForestBuildings_FormACampWithinReach()
        {
            // 캠프는 남쪽 두 줄(y=28·16) + 제재소: 출발점에서 걸어서 7~24칸(멀리 흩어 두면 걷기만 하는 시간이 늘어 지루하다).
            var sim = new SurvivorSim(1, 2);
            foreach (Structure s in sim.Structures)
            {
                if (!s.IsBuilding) continue;
                float d = s.Pos.DistanceTo(sim.Player);
                Assert.True(d >= 7f && d <= 24f, s.Name + " 거리 " + d);
                Assert.True(s.Pos.Y < 30f, s.Name + "이 숲 쪽에 있다");
            }
            Assert.True(sim.Stage.ReportTimes.Length >= 12, "숲 신고 " + sim.Stage.ReportTimes.Length);
        }

        [Fact]
        public void BurningTree_DoesNotSpreadUpwind()
        {
            SurvivorSim sim = Quiet();
            RunClear(sim, 0.1f);
            sim.Wind = new Vec2(1f, 0f);
            Structure fire = Tree(sim, 10f, 10f);
            Structure upwind = Tree(sim, 7f, 10f);
            sim.Ignite(fire, 0.5f);
            RunClear(sim, (SurvivorSim.WindSpreadEvery * 2f) + 0.5f);
            Assert.False(upwind.Burning, "바람 반대쪽으로는 번지지 않는다");
        }

        [Fact]
        public void Wind_ShiftsEveryMinute_ToANewDirection()
        {
            SurvivorSim sim = Quiet();
            RunClear(sim, 0.1f);
            Vec2 before = sim.Wind;
            Assert.Equal(1f, (before.X * before.X) + (before.Y * before.Y), 3);
            bool shifted = false;
            float at = 0f;
            for (int i = 0; i < 61 * 60 && !shifted; i++)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
                if (sim.JustWindShift)
                {
                    shifted = true;
                    at = sim.Time;
                }
            }
            Assert.True(shifted);
            Assert.InRange(at, SurvivorSim.WindShiftEvery - 0.05f, SurvivorSim.WindShiftEvery + 0.05f);
            Assert.False(before.X == sim.Wind.X && before.Y == sim.Wind.Y, "바람이 같은 방향으로 다시 뽑혔다");
        }

        [Fact]
        public void Squirrel_RunsToATree_AndSetsItOnFire()
        {
            SurvivorSim sim = Quiet();
            Structure tree = Tree(sim, 7f, 0f);
            sim.Spawn(EnemyKind.Squirrel, new Vec2(sim.Player.X + 1.5f, sim.Player.Y - 2f));
            for (int i = 0; i < 60 * 5 && !tree.Burning; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
            }
            Assert.True(tree.Burning, "다람쥐가 나무에 불을 붙여야 한다");
        }

        [Fact]
        public void Bats_ArriveAsAFlockOfSix()
        {
            SurvivorSim sim = Quiet();
            bool came = false;
            for (int i = 0; i < (int)((SurvivorSim.FirstBats + 1f) * 60) && !came; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                if (sim.JustBats)
                {
                    came = true;
                    Assert.Equal(6, sim.Enemies.FindAll(e => e.Kind == EnemyKind.Bat).Count);
                }
            }
            Assert.True(came);
            Assert.InRange(sim.Time, SurvivorSim.FirstBats - 0.1f, SurvivorSim.FirstBats + 0.1f);
        }
    }
}
