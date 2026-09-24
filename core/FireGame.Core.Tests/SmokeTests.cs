using FireGame.Core.Grid;
using FireGame.Core.Sim;
using Xunit;

namespace FireGame.Core.Tests
{
    /// <summary>
    /// 연기 확산. 열과 갈라지는 지점만 본다 —
    /// <b>연기는 통행 가능한 칸으로만 흐르고, 실외에서는 고이지 않는다.</b>
    /// 이 두 성질이 나중에 "문을 닫으면 방이 지켜진다"와 "밖으로 나가면 숨을 돌린다"가 된다.
    /// </summary>
    public class SmokeTests
    {
        /// <summary>바닥 격자를 만들고 가운데 한 칸만 목재로 두고 불을 붙인다.</summary>
        private static FireSim FloorFieldWithCenterFire(int size, out int center)
        {
            var grid = new FireGrid(size, size);
            for (int i = 0; i < grid.Count; i++)
            {
                grid.Cells[i].Material = (byte)MaterialId.Floor;
                grid.Cells[i].Fuel = 0f;
                grid.Cells[i].State = CellState.Intact;
            }

            center = size / 2;
            grid[center, center].Material = (byte)MaterialId.Wood;
            grid[center, center].Fuel = 1000f;   // 측정 중에 다 타버리지 않게
            grid[center, center].State = CellState.Burning;

            return new FireSim(grid);
        }

        private static void Run(FireSim sim, int ticks)
        {
            for (int t = 0; t < ticks; t++) sim.Tick();
        }

        // --- TC-1 ---
        [Fact]
        public void BurningWall_PushesSmokeIntoTheRoom()
        {
            FireSim sim = FloorFieldWithCenterFire(9, out int c);
            sim.Outdoor = AllIndoor(sim.Grid);

            Run(sim, 100);

            // 타는 칸은 목재 벽이라 서 있을 수 없다. 연기는 제자리에 쌓이지 않고
            // 맞닿은 방으로 나온다 — 안 그러면 불타는 창고 한가운데가 맑은 채로 남는다.
            Assert.Equal(0f, sim.Grid[c, c].Smoke);
            Assert.True(sim.Grid[c, c - 1].Smoke > 0.15f,
                "방 쪽 연기 " + sim.Grid[c, c - 1].Smoke);   // 실측 0.28
        }

        // --- TC-2 ---
        [Fact]
        public void Smoke_SpreadsToOrthogonalNeighbour()
        {
            FireSim sim = FloorFieldWithCenterFire(9, out int c);
            sim.Outdoor = AllIndoor(sim.Grid);

            Run(sim, 100);

            Assert.True(sim.Grid[c, c - 1].Smoke > 0f,
                "이웃 칸 연기 " + sim.Grid[c, c - 1].Smoke);
        }

        // --- TC-3 ---
        [Fact]
        public void Smoke_DoesNotCrossWall()
        {
            FireSim sim = FloorFieldWithCenterFire(9, out int c);
            sim.Outdoor = AllIndoor(sim.Grid);

            // 연소 칸 바로 위 한 줄을 콘크리트로 막는다.
            for (int x = 0; x < sim.Grid.Width; x++)
            {
                sim.Grid[x, c - 1].Material = (byte)MaterialId.Concrete;
            }

            Run(sim, 60);

            // 벽 너머는 연기가 닿을 길이 없다.
            Assert.Equal(0f, sim.Grid[c, c - 2].Smoke);
            Assert.Equal(0f, sim.Grid[c, c - 1].Smoke);
        }

        // --- TC-4 ---
        [Fact]
        public void Outdoor_VentsSmoke_FarFasterThanIndoor()
        {
            FireSim outside = FloorFieldWithCenterFire(9, out int c);
            outside.Outdoor = AllOutdoor(outside.Grid);

            FireSim inside = FloorFieldWithCenterFire(9, out int _);
            inside.Outdoor = AllIndoor(inside.Grid);

            Run(outside, 100);
            Run(inside, 100);

            // 실측 실외 0.10 vs 실내 0.28. 밖에서는 연기가 고이지 않는다.
            Assert.True(outside.Grid[c, c - 1].Smoke < inside.Grid[c, c - 1].Smoke * 0.8f,
                "실외 " + outside.Grid[c, c - 1].Smoke + " vs 실내 " + inside.Grid[c, c - 1].Smoke);
        }

        // --- TC-5 ---
        [Fact]
        public void Smoke_StaysWithinZeroToOne_OverLongBurn()
        {
            FireSim sim = FloorFieldWithCenterFire(9, out int _);
            sim.Outdoor = AllIndoor(sim.Grid);

            Run(sim, 600);

            for (int i = 0; i < sim.Grid.Count; i++)
            {
                float smoke = sim.Grid.Cells[i].Smoke;
                Assert.InRange(smoke, 0f, 1f);
            }
        }

        // --- TC-6 ---
        [Fact]
        public void Smoke_IsDeterministic_AcrossTwoIdenticalRuns()
        {
            FireSim a = FloorFieldWithCenterFire(9, out int _);
            FireSim b = FloorFieldWithCenterFire(9, out int _);
            a.Outdoor = AllIndoor(a.Grid);
            b.Outdoor = AllIndoor(b.Grid);

            Run(a, 100);
            Run(b, 100);

            for (int i = 0; i < a.Grid.Count; i++)
            {
                Assert.Equal(a.Grid.Cells[i].Smoke, b.Grid.Cells[i].Smoke);
            }
        }

        // --- TC-7 ---
        [Fact]
        public void SmokePass_DoesNotChangeFireSpreadTiming()
        {
            // 연기를 붙이기 전 실측값: 목재 직교 이웃은 10틱(1.0초) 안에 붙는다.
            var grid = new FireGrid(9, 9);
            for (int i = 0; i < grid.Count; i++)
            {
                grid.Cells[i].Material = (byte)MaterialId.Wood;
                grid.Cells[i].Fuel = 1f;
                grid.Cells[i].State = CellState.Intact;
            }
            grid[4, 4].State = CellState.Burning;

            var sim = new FireSim(grid);
            Run(sim, 10);

            Assert.Equal(CellState.Burning, sim.Grid[4, 3].State);
            Assert.Equal(CellState.Burning, sim.Grid[3, 4].State);
        }

        private static bool[] AllIndoor(FireGrid grid)
        {
            return new bool[grid.Count];
        }

        private static bool[] AllOutdoor(FireGrid grid)
        {
            var mask = new bool[grid.Count];
            for (int i = 0; i < mask.Length; i++) mask[i] = true;
            return mask;
        }
    }
}
