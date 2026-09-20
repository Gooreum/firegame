using FireGame.Core.Grid;
using FireGame.Core.Sim;
using Xunit;

namespace FireGame.Core.Tests
{
    public class FireSimTests
    {
        /// <summary>전부 목재인 격자를 만들고 가운데 한 칸에 불을 붙인다.</summary>
        private static FireSim WoodFieldWithCenterFire(int size, out int center)
        {
            var grid = new FireGrid(size, size);
            for (int i = 0; i < grid.Count; i++)
            {
                grid.Cells[i].Material = (byte)MaterialId.Wood;
                grid.Cells[i].Fuel = 1f;
                grid.Cells[i].State = CellState.Intact;
            }

            center = size / 2;
            grid[center, center].State = CellState.Burning;

            return new FireSim(grid);
        }

        /// <summary>해당 셀이 점화되는 틱 번호를 반환한다. 끝까지 안 붙으면 null.</summary>
        private static int? TicksUntilBurning(FireSim sim, int x, int y, int maxTicks)
        {
            for (int t = 1; t <= maxTicks; t++)
            {
                sim.Tick();
                if (sim.Grid[x, y].State == CellState.Burning) return t;
            }
            return null;
        }

        // --- TC-1 ---
        [Fact]
        public void Fire_SpreadsToFourOrthogonalNeighbors_InAboutOneSecond()
        {
            var sim = WoodFieldWithCenterFire(9, out int c);

            for (int t = 0; t < 10; t++) sim.Tick();

            Assert.Equal(CellState.Burning, sim.Grid[c, c - 1].State);
            Assert.Equal(CellState.Burning, sim.Grid[c, c + 1].State);
            Assert.Equal(CellState.Burning, sim.Grid[c - 1, c].State);
            Assert.Equal(CellState.Burning, sim.Grid[c + 1, c].State);
        }

        // --- TC-2 ---
        [Fact]
        public void DiagonalNeighbor_IgnitesLaterThanOrthogonal_FromASingleSource()
        {
            // 열을 주는 셀이 하나뿐이도록 나머지를 콘크리트로 막는다.
            // 이래야 거리 감쇠(DiagonalWeight)의 순수한 효과를 잴 수 있다.
            var orthogonalMap = MapLoader.Parse(new[]
            {
                "#####",
                "##*W#",
                "#####",
            });
            var diagonalMap = MapLoader.Parse(new[]
            {
                "#####",
                "##*##",
                "###W#",
            });

            int? orthogonal = TicksUntilBurning(new FireSim(orthogonalMap.Grid), 3, 1, 300);
            int? diagonal = TicksUntilBurning(new FireSim(diagonalMap.Grid), 3, 2, 300);

            Assert.NotNull(orthogonal);
            Assert.NotNull(diagonal);
            Assert.True(diagonal.Value >= orthogonal.Value * 2,
                "단일 발화원 기준 대각선(" + diagonal + ")은 직교(" + orthogonal + ")의 2배 이상 걸려야 한다");
        }

        // --- TC-2b ---
        [Fact]
        public void InAnOpenField_FireFrontRoundsOut_DiagonalStillTrailsOrthogonal()
        {
            // 열린 목재밭에서는 직교 이웃이 먼저 붙은 뒤 그 이웃이 대각 셀에
            // 다시 직교로 열을 공급한다. 그래서 격차가 2배까지 벌어지지 않고
            // 불의 전선이 둥글게 퍼진다. 이것이 의도된 동작이다.
            var a = WoodFieldWithCenterFire(9, out int c);
            int? orthogonal = TicksUntilBurning(a, c + 1, c, 300);

            var b = WoodFieldWithCenterFire(9, out _);
            int? diagonal = TicksUntilBurning(b, c + 1, c + 1, 300);

            Assert.NotNull(orthogonal);
            Assert.NotNull(diagonal);
            Assert.True(diagonal.Value > orthogonal.Value,
                "대각선(" + diagonal + ")은 여전히 직교(" + orthogonal + ")보다 늦어야 한다");
        }

        // --- TC-3 ---
        [Fact]
        public void ConcreteRing_ContainsFireCompletely()
        {
            // 가운데 목재 1칸을 콘크리트가 완전히 둘러싼 형태.
            var map = MapLoader.Parse(new[]
            {
                "#####",
                "#####",
                "##*##",
                "#####",
                "#####",
            });

            var sim = new FireSim(map.Grid);
            for (int t = 0; t < 200; t++) sim.Tick();

            // 발화점은 연료를 다 쓰고 소실되고, 바깥으로는 한 칸도 번지지 않는다.
            Assert.Equal(0, sim.Grid.CountBurning());
            Assert.Equal(CellState.Burnt, sim.Grid[2, 2].State);
        }

        // --- TC-4 ---
        [Fact]
        public void BurningCell_TransitionsToBurnt_WhenFuelRunsOut()
        {
            var map = MapLoader.Parse(new[] { "###", "#*#", "###" });
            var sim = new FireSim(map.Grid);

            Assert.Equal(CellState.Burning, sim.Grid[1, 1].State);

            // 목재 BurnRate 0.25/초 → 연료 1.0은 4초, 즉 40틱이면 소진된다.
            for (int t = 0; t < 45; t++) sim.Tick();

            Assert.Equal(CellState.Burnt, sim.Grid[1, 1].State);
            Assert.Equal(0f, sim.Grid[1, 1].Fuel);
            Assert.Equal(0, sim.Grid.CountBurning());
        }

        // --- TC-5 ---
        [Fact]
        public void BurntCell_NeverReignites_NoMatterHowMuchHeat()
        {
            var sim = WoodFieldWithCenterFire(5, out int c);

            sim.Grid[c + 1, c].State = CellState.Burnt;
            sim.Grid[c + 1, c].Fuel = 0f;

            for (int t = 0; t < 200; t++) sim.Tick();

            Assert.Equal(CellState.Burnt, sim.Grid[c + 1, c].State);
        }

        // --- TC-6 ---
        [Fact]
        public void Wind_MakesDownwindIgniteBeforeUpwind()
        {
            var sim = WoodFieldWithCenterFire(9, out int c);
            sim.Wind = Wind.From(1f, 0f, 0.8f);

            int? downwind = null;
            int? upwind = null;

            for (int t = 1; t <= 300; t++)
            {
                sim.Tick();
                if (downwind == null && sim.Grid[c + 1, c].State == CellState.Burning) downwind = t;
                if (upwind == null && sim.Grid[c - 1, c].State == CellState.Burning) upwind = t;
            }

            Assert.NotNull(downwind);
            Assert.NotNull(upwind);
            Assert.True(downwind.Value < upwind.Value,
                "풍하(" + downwind + ")가 풍상(" + upwind + ")보다 먼저 붙어야 한다");
        }

        // --- TC-7 ---
        [Fact]
        public void FireAtGridCorner_DoesNotReachOutOfBounds()
        {
            var grid = new FireGrid(6, 6);
            for (int i = 0; i < grid.Count; i++)
            {
                grid.Cells[i].Material = (byte)MaterialId.Wood;
                grid.Cells[i].Fuel = 1f;
                grid.Cells[i].State = CellState.Intact;
            }
            grid[0, 0].State = CellState.Burning;

            var sim = new FireSim(grid) { Wind = Wind.From(-1f, -1f, 1f) };

            // 경계 밖으로 나가면 여기서 예외가 난다.
            for (int t = 0; t < 200; t++) sim.Tick();

            Assert.True(sim.Grid[1, 1].State == CellState.Burning
                        || sim.Grid[1, 1].State == CellState.Burnt);
        }

        // --- TC-8 ---
        [Fact]
        public void WetCell_DoesNotIgnite_WhileStillWet()
        {
            var sim = WoodFieldWithCenterFire(5, out int c);
            sim.Grid[c + 1, c].Wet = 1f;

            for (int t = 0; t < 100; t++) sim.Tick();

            Assert.Equal(CellState.Intact, sim.Grid[c + 1, c].State);
            Assert.True(sim.Grid[c + 1, c].Wet > 0f);
        }

        // --- TC-9 ---
        [Fact]
        public void Wetness_DriesToZeroAndClampsThere()
        {
            var grid = new FireGrid(3, 3);
            grid[1, 1].Material = (byte)MaterialId.Concrete;
            grid[1, 1].State = CellState.Intact;
            grid[1, 1].Wet = 0.05f;

            var sim = new FireSim(grid);

            // WetDecay 0.01/초 → 0.05는 5초(50틱)면 마른다. 넉넉히 더 돌린다.
            for (int t = 0; t < 300; t++) sim.Tick();

            Assert.Equal(0f, sim.Grid[1, 1].Wet);
        }

        // --- TC-10 ---
        [Fact]
        public void Simulation_IsDeterministic_AcrossIndependentRuns()
        {
            string[] rows =
            {
                "WWWWWWWW",
                "W~W..WEW",
                "W..*..WW",
                "WWDW..~W",
                "WWWWWWWW",
            };

            Cell[] RunOnce()
            {
                var sim = new FireSim(MapLoader.Parse(rows).Grid)
                {
                    Wind = Wind.From(1f, 0.5f, 0.6f),
                };
                for (int t = 0; t < 120; t++) sim.Tick();
                return (Cell[])sim.Grid.Cells.Clone();
            }

            Cell[] first = RunOnce();
            Cell[] second = RunOnce();

            Assert.Equal(first.Length, second.Length);
            for (int i = 0; i < first.Length; i++)
            {
                Assert.Equal(first[i].State, second[i].State);
                Assert.Equal(first[i].Fuel, second[i].Fuel, 6);
                Assert.Equal(first[i].Heat, second[i].Heat, 6);
                Assert.Equal(first[i].Wet, second[i].Wet, 6);
            }
        }

        // --- TC-11 ---
        [Fact]
        public void Oil_IgnitesFasterThanWood_ButKeepsBurningLonger()
        {
            // 왼쪽은 유류, 오른쪽은 목재. 각각 발화점과 직교로 맞닿아 있다.
            var map = MapLoader.Parse(new[]
            {
                "#####",
                "#~*W#",
                "#####",
            });

            var sim = new FireSim(map.Grid);

            int? oilIgnite = null;
            int? woodIgnite = null;
            int? oilBurnt = null;
            int? woodBurnt = null;

            for (int t = 1; t <= 400; t++)
            {
                sim.Tick();
                if (oilIgnite == null && sim.Grid[1, 1].State == CellState.Burning) oilIgnite = t;
                if (woodIgnite == null && sim.Grid[3, 1].State == CellState.Burning) woodIgnite = t;
                if (oilBurnt == null && sim.Grid[1, 1].State == CellState.Burnt) oilBurnt = t;
                if (woodBurnt == null && sim.Grid[3, 1].State == CellState.Burnt) woodBurnt = t;
            }

            Assert.NotNull(oilIgnite);
            Assert.NotNull(woodIgnite);
            Assert.True(oilIgnite.Value < woodIgnite.Value,
                "유류(" + oilIgnite + ")가 목재(" + woodIgnite + ")보다 먼저 붙어야 한다");

            // 목재는 연료를 다 쓰고 스스로 꺼지지만, 유류는 40초가 지나도 계속 탄다.
            // 유류가 금방 꺼져버리면 물이 역효과여도 그냥 기다리면 되기 때문에
            // 폼 소화기를 살 이유가 사라진다. A급만 스스로 꺼진다.
            Assert.NotNull(woodBurnt);
            Assert.Null(oilBurnt);
            Assert.Equal(CellState.Burning, sim.Grid[1, 1].State);
        }

        // --- TC-12 ---
        [Fact]
        public void ZeroDeltaTime_LeavesStateUntouched()
        {
            var sim = WoodFieldWithCenterFire(5, out int c);
            sim.Tick();

            Cell[] before = (Cell[])sim.Grid.Cells.Clone();
            sim.Tick(0f);

            for (int i = 0; i < before.Length; i++)
            {
                Assert.Equal(before[i].State, sim.Grid.Cells[i].State);
                Assert.Equal(before[i].Heat, sim.Grid.Cells[i].Heat, 6);
                Assert.Equal(before[i].Fuel, sim.Grid.Cells[i].Fuel, 6);
            }

            Assert.Equal(CellState.Burning, sim.Grid[c, c].State);
        }

        // --- TC-13 ---
        [Fact]
        public void DoubleBuffering_PreventsChainSpreadWithinASingleTick()
        {
            // *(발화) A B — B는 발화점과 맞닿지 않고 A를 통해서만 열을 받는다.
            var map = MapLoader.Parse(new[]
            {
                "#####",
                "#*WW#",
                "#####",
            });

            var sim = new FireSim(map.Grid);

            for (int t = 1; t <= 400; t++)
            {
                sim.Tick();

                if (sim.Grid[2, 1].State == CellState.Burning)
                {
                    // A가 붙은 바로 그 틱에는 A가 아직 열을 뿌리지 않았으므로
                    // B는 열을 한 방울도 받지 않은 상태여야 한다.
                    Assert.Equal(0f, sim.Grid[3, 1].Heat);
                    Assert.Equal(CellState.Intact, sim.Grid[3, 1].State);
                    return;
                }
            }

            Assert.Fail("A가 끝내 점화되지 않아 이중 버퍼를 검증하지 못했다");
        }

        // --- TC-14 ---
        [Fact]
        public void StrongWind_StillLetsUpwindIgniteEventually_NoSafeZone()
        {
            var sim = WoodFieldWithCenterFire(9, out int c);
            sim.Wind = Wind.From(1f, 0f, 0.8f);

            int? upwind = TicksUntilBurning(sim, c - 1, c, 300);

            Assert.True(upwind.HasValue,
                "풍상도 결국 번져야 한다. 완전 안전지대가 생기면 플레이어가 무시하고 지나간다");
        }
    }
}
