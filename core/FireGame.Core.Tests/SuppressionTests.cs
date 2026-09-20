using FireGame.Core.Grid;
using FireGame.Core.Sim;
using Xunit;

namespace FireGame.Core.Tests
{
    public class SuppressionTests
    {
        private static readonly Agent Water = new Agent(AgentType.Water, 0.6f, 0.5f, 0.5f);
        private static readonly Agent Foam = new Agent(AgentType.Foam, 0.8f, 0.6f, 3.0f);
        private static readonly Agent Co2 = new Agent(AgentType.CO2, 0.5f, 0f, 2.5f);

        /// <summary>해당 셀이 자기 발열로 안정될 때까지 돌린다.</summary>
        private static FireSim Settled(string[] rows, int ticks = 60)
        {
            var sim = new FireSim(MapLoader.Parse(rows).Grid);
            for (int t = 0; t < ticks; t++) sim.Tick();
            return sim;
        }

        // --- TC-1 ---
        [Fact]
        public void Water_OnBurningWood_LowersHeatAndAddsWetness()
        {
            var sim = Settled(new[] { "###", "#*#", "###" }, 20);

            float heatBefore = sim.Grid[1, 1].Heat;
            Assert.True(heatBefore > 0f, "연소 셀은 자기 발열로 열을 가지고 있어야 한다");

            Suppression.Apply(sim.Grid, 1, 1, Water);

            Assert.True(sim.Grid[1, 1].Heat < heatBefore);
            Assert.True(sim.Grid[1, 1].Wet > 0f);
        }

        // --- TC-2 ---
        [Fact]
        public void Water_OnBurningWood_ExtinguishesAfterEnoughApplications()
        {
            var sim = Settled(new[] { "###", "#*#", "###" }, 20);
            Assert.Equal(1, sim.Grid.CountBurning());

            SuppressionOutcome last = SuppressionOutcome.NoEffect;
            for (int i = 0; i < 5 && last != SuppressionOutcome.Extinguished; i++)
            {
                last = Suppression.Apply(sim.Grid, 1, 1, Water);
            }

            Assert.Equal(SuppressionOutcome.Extinguished, last);
            Assert.Equal(CellState.Intact, sim.Grid[1, 1].State);
            Assert.Equal(0, sim.Grid.CountBurning());
        }

        // --- TC-3 ---
        [Fact]
        public void Water_OnBurningOil_BackfiresAndSpreadsToANeighbor()
        {
            // 가운데 유류가 연소 중이고 주변에 목재가 있다.
            var grid = MapLoader.Parse(new[]
            {
                "#W#",
                "W~W",
                "#W#",
            }).Grid;

            grid[1, 1].State = CellState.Burning;
            int burningBefore = grid.CountBurning();

            var outcome = Suppression.Apply(grid, 1, 1, Water);

            Assert.Equal(SuppressionOutcome.Backfired, outcome);
            Assert.Equal(CellState.Burning, grid[1, 1].State);
            Assert.True(grid.CountBurning() > burningBefore,
                "물을 뿌렸는데 불이 오히려 늘어야 한다");
        }

        // --- TC-4 ---
        [Fact]
        public void Water_OnBurningElectric_NeverExtinguishes()
        {
            var grid = MapLoader.Parse(new[] { "###", "#E#", "###" }).Grid;
            grid[1, 1].State = CellState.Burning;

            for (int i = 0; i < 20; i++)
            {
                Suppression.Apply(grid, 1, 1, Water);
            }

            Assert.Equal(CellState.Burning, grid[1, 1].State);
        }

        // --- TC-5 ---
        [Fact]
        public void Co2_OnBurningElectric_ExtinguishesInOneShot()
        {
            var sim = Settled(new[] { "###", "#E#", "###" }, 60);
            sim.Grid[1, 1].State = CellState.Burning;

            // 전기 셀의 자기 발열 유지량(0.375)보다 CO2 1회(0.5 x 1.2 = 0.6)가 크다.
            for (int t = 0; t < 60; t++) sim.Tick();

            var outcome = Suppression.Apply(sim.Grid, 1, 1, Co2);

            Assert.Equal(SuppressionOutcome.Extinguished, outcome);
            Assert.Equal(CellState.Intact, sim.Grid[1, 1].State);
        }

        // --- TC-6 ---
        [Fact]
        public void Foam_OnBurningOil_ExtinguishesWithoutSpreading()
        {
            var grid = MapLoader.Parse(new[]
            {
                "#W#",
                "W~W",
                "#W#",
            }).Grid;

            grid[1, 1].State = CellState.Burning;

            SuppressionOutcome last = SuppressionOutcome.NoEffect;
            for (int i = 0; i < 6 && last != SuppressionOutcome.Extinguished; i++)
            {
                last = Suppression.Apply(grid, 1, 1, Foam);
            }

            Assert.Equal(SuppressionOutcome.Extinguished, last);
            Assert.Equal(0, grid.CountBurning());
        }

        // --- TC-7 ---
        [Fact]
        public void Foam_OnElectric_HasExactlyNoEffect()
        {
            var grid = MapLoader.Parse(new[] { "###", "#E#", "###" }).Grid;
            grid[1, 1].State = CellState.Burning;
            grid[1, 1].Heat = 0.4f;

            var outcome = Suppression.Apply(grid, 1, 1, Foam);

            Assert.Equal(SuppressionOutcome.NoEffect, outcome);
            Assert.Equal(0.4f, grid[1, 1].Heat);
            Assert.Equal(0f, grid[1, 1].Wet);
            Assert.Equal(0f, Suppression.EffectivenessOf(AgentType.Foam, FireClass.C));
        }

        // --- TC-8 ---
        [Fact]
        public void PreemptiveWater_OnDryWood_CreatesAFirebreak()
        {
            var sim = new FireSim(MapLoader.Parse(new[]
            {
                "#####",
                "#*WW#",
                "#####",
            }).Grid);

            // 아직 불이 닿지 않은 옆 칸을 미리 적셔 둔다.
            var outcome = Suppression.Apply(sim.Grid, 2, 1, Water);
            Assert.Equal(SuppressionOutcome.Cooled, outcome);
            Assert.True(sim.Grid[2, 1].Wet > 0f);

            for (int t = 0; t < 100; t++) sim.Tick();

            Assert.Equal(CellState.Intact, sim.Grid[2, 1].State);
            Assert.Equal(CellState.Intact, sim.Grid[3, 1].State);
        }

        // --- TC-9 ---
        [Fact]
        public void Wetness_ClampsAtOne()
        {
            var grid = MapLoader.Parse(new[] { "###", "#W#", "###" }).Grid;

            for (int i = 0; i < 20; i++)
            {
                Suppression.Apply(grid, 1, 1, Water);
            }

            Assert.Equal(1f, grid[1, 1].Wet);
        }

        // --- TC-10 ---
        [Fact]
        public void OutOfBoundsCoordinates_ReturnOutOfBounds_WithoutThrowing()
        {
            var grid = new FireGrid(3, 3);

            Assert.Equal(SuppressionOutcome.OutOfBounds, Suppression.Apply(grid, -1, 0, Water));
            Assert.Equal(SuppressionOutcome.OutOfBounds, Suppression.Apply(grid, 0, -1, Water));
            Assert.Equal(SuppressionOutcome.OutOfBounds, Suppression.Apply(grid, 3, 0, Water));
            Assert.Equal(SuppressionOutcome.OutOfBounds, Suppression.Apply(grid, 0, 3, Water));
        }

        // --- TC-11 ---
        [Fact]
        public void BurntCell_IsUnaffectedByAnyAgent()
        {
            var grid = MapLoader.Parse(new[] { "###", "#W#", "###" }).Grid;
            grid[1, 1].State = CellState.Burnt;
            grid[1, 1].Fuel = 0f;

            Assert.Equal(SuppressionOutcome.NoEffect, Suppression.Apply(grid, 1, 1, Water));
            Assert.Equal(CellState.Burnt, grid[1, 1].State);
            Assert.Equal(0f, grid[1, 1].Wet);
        }

        // --- TC-12 ---
        [Fact]
        public void Backfire_WithNoValidNeighbor_DoesNotSpread()
        {
            // 유류가 콘크리트로 완전히 둘러싸여 번질 곳이 없다.
            var grid = MapLoader.Parse(new[]
            {
                "###",
                "#~#",
                "###",
            }).Grid;

            grid[1, 1].State = CellState.Burning;

            var outcome = Suppression.Apply(grid, 1, 1, Water);

            Assert.Equal(SuppressionOutcome.NoEffect, outcome);
            Assert.Equal(1, grid.CountBurning());
        }

        // --- TC-13 ---
        [Fact]
        public void Backfire_PicksTheSameNeighborEveryRun()
        {
            GridPoint RunOnce()
            {
                var grid = MapLoader.Parse(new[]
                {
                    "#W#",
                    "W~W",
                    "#W#",
                }).Grid;

                grid[1, 1].State = CellState.Burning;
                Suppression.Apply(grid, 1, 1, Water);

                for (int y = 0; y < grid.Height; y++)
                {
                    for (int x = 0; x < grid.Width; x++)
                    {
                        if ((x != 1 || y != 1) && grid[x, y].State == CellState.Burning)
                        {
                            return new GridPoint(x, y);
                        }
                    }
                }

                return new GridPoint(-1, -1);
            }

            GridPoint first = RunOnce();
            GridPoint second = RunOnce();

            Assert.Equal(new GridPoint(1, 0), first);   // 북쪽이 첫 후보
            Assert.Equal(first, second);
        }

        // --- TC-14 ---
        [Fact]
        public void WaterLeavesTheCellWet_Co2LeavesItDry_SoOnlyCo2AllowsReignition()
        {
            FireSim Build()
            {
                return new FireSim(MapLoader.Parse(new[]
                {
                    "#####",
                    "#*W.#",
                    "#####",
                }).Grid);
            }

            // 물로 끈 경우: 젖어 있어 한 번도 다시 붙지 않는다.
            var wet = Build();
            Suppression.Apply(wet.Grid, 2, 1, Water);

            for (int t = 0; t < 60; t++)
            {
                wet.Tick();
                Assert.NotEqual(CellState.Burning, wet.Grid[2, 1].State);
            }

            Assert.Equal(CellState.Intact, wet.Grid[2, 1].State);
            Assert.True(wet.Grid[2, 1].Wet > 0f);

            // CO2로 식힌 경우: 젖음이 0이라 이웃 화염에 다시 붙는다.
            // 한번 붙으면 연료를 다 쓰고 Burnt까지 가므로, 최종 상태가 아니라
            // "도중에 다시 붙었는가"를 관측해야 한다.
            var dry = Build();
            Suppression.Apply(dry.Grid, 2, 1, Co2);
            Assert.Equal(0f, dry.Grid[2, 1].Wet);

            bool reignited = false;
            for (int t = 0; t < 60; t++)
            {
                dry.Tick();
                if (dry.Grid[2, 1].State == CellState.Burning) reignited = true;
            }

            Assert.True(reignited, "CO2로 끈 셀은 말라 있어 다시 붙어야 한다");
        }

        [Fact]
        public void EffectivenessMatrix_EncodesTheClassCounters()
        {
            // 물은 일반화재에 최고, 유류엔 역효과, 전기엔 위험하다.
            Assert.Equal(1.0f, Suppression.EffectivenessOf(AgentType.Water, FireClass.A));
            Assert.True(Suppression.EffectivenessOf(AgentType.Water, FireClass.B) < 0f);
            Assert.True(Suppression.EffectivenessOf(AgentType.Water, FireClass.C) < 0f);

            // 폼은 유류 전용, CO2는 전기 전용이 가장 강하다.
            Assert.True(Suppression.EffectivenessOf(AgentType.Foam, FireClass.B)
                        > Suppression.EffectivenessOf(AgentType.Water, FireClass.B));
            Assert.True(Suppression.EffectivenessOf(AgentType.CO2, FireClass.C)
                        > Suppression.EffectivenessOf(AgentType.Foam, FireClass.C));
        }
    }
}
