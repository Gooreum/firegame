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
        public void ForestMap_HasSevenBuildings_ManyTrees_AndAClearCenter()
        {
            var sim = new SurvivorSim(1, 2);
            Assert.Equal("산불 숲", sim.Stage.Name);
            Assert.Equal(7, sim.HousesTotal);
            Assert.Single(sim.Structures.FindAll(s => s.Kind == StructureKind.Depot));
            Assert.True(sim.Structures.FindAll(s => s.Kind == StructureKind.Tree).Count >= 60, "나무 " + sim.Structures.FindAll(s => s.Kind == StructureKind.Tree).Count);
            Assert.All(sim.Structures, s => Assert.True(s.DistanceTo(sim.Player) > 6f, s.Name + "가 출발점에 너무 가깝다"));
            Assert.All(sim.Structures, s => Assert.False(s.Burning));
        }

        [Fact]
        public void BurningTree_SpreadsDownwind()
        {
            SurvivorSim sim = Quiet();
            RunClear(sim, 0.1f);
            sim.Wind = new Vec2(1f, 0f);
            Structure fire = Tree(sim, 10f, 10f);
            Structure downwind = Tree(sim, 13f, 10f);
            sim.Ignite(fire, 0.5f);
            RunClear(sim, SurvivorSim.WindSpreadEvery + 0.5f);
            Assert.True(downwind.Burning, "바람 쪽 나무에 불이 옮아야 한다");
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
            for (int i = 0; i < 21 * 60 && !came; i++)
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
            Assert.InRange(sim.Time, 19.9f, 20.1f);
        }
    }
}
