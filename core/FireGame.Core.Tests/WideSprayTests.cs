using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using FireGame.Core.Sim;
using Xunit;

namespace FireGame.Core.Tests
{
    /// <summary>"찔끔찔끔" 대신 한 번에 넓게 맞히는 방수.</summary>
    public class WideSprayTests
    {
        private readonly List<GridPoint> _hits = new List<GridPoint>();

        private static FireGrid OpenFloor(int size)
        {
            var grid = new FireGrid(size, size);
            for (int i = 0; i < grid.Count; i++)
            {
                grid.Cells[i].Material = (byte)MaterialId.Floor;
                grid.Cells[i].State = CellState.Intact;
            }
            return grid;
        }

        // --- TC-1 ---
        [Fact]
        public void ConeRange2_CoversSixCells()
        {
            var grid = OpenFloor(9);
            Aiming.Resolve(grid, 4, 4, AimDirection.E, AimPattern.Cone, 2, _hits);

            Assert.Equal(6, _hits.Count);
            Assert.Contains(new GridPoint(5, 3), _hits);
            Assert.Contains(new GridPoint(6, 2), _hits);
            Assert.Contains(new GridPoint(5, 4), _hits);
            Assert.Contains(new GridPoint(6, 4), _hits);
            Assert.Contains(new GridPoint(5, 5), _hits);
            Assert.Contains(new GridPoint(6, 6), _hits);
        }

        // --- TC-2 ---
        [Fact]
        public void ConeRange1_IsStillThreeCells()
        {
            var grid = OpenFloor(9);
            Aiming.Resolve(grid, 4, 4, AimDirection.E, AimPattern.Cone, 1, _hits);
            Assert.Equal(3, _hits.Count);
        }

        // --- TC-3 ---
        [Fact]
        public void Cone_StopsAtAWall()
        {
            var grid = OpenFloor(9);
            grid[5, 4].Material = (byte)MaterialId.Concrete;

            Aiming.Resolve(grid, 4, 4, AimDirection.E, AimPattern.Cone, 2, _hits);

            Assert.Contains(new GridPoint(5, 4), _hits);          // 벽 자체는 맞는다
            Assert.DoesNotContain(new GridPoint(6, 4), _hits);    // 그 너머는 안 맞는다
            Assert.Equal(5, _hits.Count);
        }

        // --- TC-4 ---
        [Fact]
        public void Cone_AtTheEdge_SkipsOutOfBoundsCells()
        {
            var grid = OpenFloor(5);
            Aiming.Resolve(grid, 4, 2, AimDirection.E, AimPattern.Cone, 2, _hits);
            Assert.Empty(_hits);

            Aiming.Resolve(grid, 3, 0, AimDirection.E, AimPattern.Cone, 2, _hits);
            Assert.Equal(new[] { new GridPoint(4, 0), new GridPoint(4, 1) }, _hits.ToArray());
        }

        // --- TC-6 ---
        [Fact]
        public void OneBucketThrow_CoolsThreeBurningCells()
        {
            var stage = new StageDef(99, "T", new[]
            {
                "#######",
                "#...*.#",
                "#..@*.#",
                "#...*.#",
                "#######",
            }, Wind.None, 60f, 100);

            var runner = new StageRunner(stage, EquipmentCatalog.StartingEquipment);
            float[] before = { runner.Grid[4, 1].Heat, runner.Grid[4, 2].Heat, runner.Grid[4, 3].Heat };

            runner.Update(0.001f, new StageInput { Fire = true, Slot = 0 });

            Assert.True(runner.Grid[4, 1].Heat < before[0] || runner.Grid[4, 1].State != CellState.Burning);
            Assert.True(runner.Grid[4, 2].Heat < before[1] || runner.Grid[4, 2].State != CellState.Burning);
            Assert.True(runner.Grid[4, 3].Heat < before[2] || runner.Grid[4, 3].State != CellState.Burning);
            Assert.True(runner.Grid[4, 1].Wet > 0f && runner.Grid[4, 3].Wet > 0f, "양옆 칸도 젖어야 한다");
        }
    }
}
