using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using Xunit;
using Xunit.Abstractions;

namespace FireGame.Core.Tests
{
    /// <summary>
    /// 여섯 현장에 실제로 놓인 장식물이 게임을 바꾸지 않았는지.
    ///
    /// 장식물은 순전히 "여기가 주유소구나"를 보이게 하려고 넣은 것이다.
    /// 불연성이라 불과 소실률은 구조적으로 안 바뀌지만, <b>통행은 실제로 막는다</b>.
    /// 그래서 길을 막지 않았는지, 건물로 둔갑하지 않았는지를 여기서 못 박는다.
    /// </summary>
    public class SceneryPlacementTests
    {
        private readonly ITestOutputHelper _out;

        public SceneryPlacementTests(ITestOutputHelper output)
        {
            _out = output;
        }

        // --- TC-1 ---
        [Fact]
        public void EveryMap_IsStillTwentyTwoRowsOfForty()
        {
            foreach (StageDef stage in StageCatalog.All)
            {
                Assert.Equal(22, stage.Map.Length);
                foreach (string row in stage.Map) Assert.Equal(40, row.Length);
            }
        }

        // --- TC-2 ---
        [Fact]
        public void Scenery_NeverTurnsIntoABuilding()
        {
            var expected = new Dictionary<int, int>
            {
                { 0, 1 }, { 1, 6 }, { 2, 4 }, { 3, 2 }, { 4, 2 }, { 5, 4 },
            };

            foreach (StageDef stage in StageCatalog.All)
            {
                ParsedMap map = MapLoader.Parse(stage.Map);
                BuildingMap buildings = BuildingMap.From(map.Grid, map.PlayerSpawn);

                Assert.Equal(expected[stage.Id], buildings.All.Length);

                // 지붕은 건물 칸을 전부 덮는다. 장식물이 한 칸이라도 섞이면 나무 위에 지붕이 얹힌다.
                foreach (Building building in buildings.All)
                {
                    foreach (GridPoint cell in building.Cells)
                    {
                        Assert.NotEqual(
                            MaterialId.Scenery,
                            (MaterialId)map.Grid[cell.X, cell.Y].Material);
                    }
                }
            }
        }

        // --- TC-3 ---
        [Fact]
        public void EveryStage_HasSceneryAndAllOfItStandsInTheYard()
        {
            foreach (StageDef stage in StageCatalog.All)
            {
                ParsedMap map = MapLoader.Parse(stage.Map);
                BuildingMap buildings = BuildingMap.From(map.Grid, map.PlayerSpawn);

                int count = 0;
                foreach (GridPoint cell in SceneryCells(map.Grid))
                {
                    count++;
                    Assert.Equal(BuildingMap.None, buildings.At(cell.X, cell.Y));
                    Assert.True(buildings.IsOutdoor(cell.X, cell.Y), stage.Name + " 장식물이 실외가 아니다");
                }

                Assert.True(count > 0, stage.Name + " 에 장식물이 하나도 없다 — 마당이 여전히 비었다");
                _out.WriteLine(stage.Name + ": 장식물 " + count + "칸");
            }
        }

        // --- TC-4 ---
        [Fact]
        public void Scenery_NeverBlocksTheWayToAnyoneOrAnything()
        {
            foreach (StageDef stage in StageCatalog.All)
            {
                ParsedMap map = MapLoader.Parse(stage.Map);
                bool[] reached = WalkFrom(map.Grid, map.PlayerSpawn);

                foreach (GridPoint civilian in map.Civilians)
                {
                    Assert.True(
                        reached[map.Grid.Index(civilian.X, civilian.Y)],
                        stage.Name + " 시민 (" + civilian.X + "," + civilian.Y + ") 에게 못 간다");
                }

                foreach (GridPoint exit in map.Exits)
                {
                    Assert.True(
                        reached[map.Grid.Index(exit.X, exit.Y)],
                        stage.Name + " 출구 (" + exit.X + "," + exit.Y + ") 에 못 간다");
                }

                // 급수전은 통행 불가라 그 칸이 아니라 옆에 설 자리가 있어야 한다.
                foreach (GridPoint hydrant in map.Hydrants)
                {
                    Assert.True(
                        HasStandingRoom(map.Grid, reached, hydrant),
                        stage.Name + " 급수전 (" + hydrant.X + "," + hydrant.Y + ") 옆에 설 자리가 없다");
                }
            }
        }

        // --- TC-5 ---
        [Fact]
        public void Scenery_DidNotMoveOrRemoveAnyPieceOfTheStage()
        {
            var civilians = new Dictionary<int, int> { { 0, 1 }, { 1, 1 }, { 2, 2 }, { 3, 3 }, { 4, 2 }, { 5, 4 } };
            var fires = new Dictionary<int, int> { { 0, 1 }, { 1, 2 }, { 2, 1 }, { 3, 1 }, { 4, 3 }, { 5, 3 } };

            foreach (StageDef stage in StageCatalog.All)
            {
                ParsedMap map = MapLoader.Parse(stage.Map);

                Assert.Equal(civilians[stage.Id], map.Civilians.Count);
                Assert.Equal(fires[stage.Id], map.IgnitionPoints.Count);
                Assert.Equal(2, map.Exits.Count);
                Assert.Single(map.Hydrants);
            }
        }

        // --- TC-6 ---
        [Fact]
        public void Scenery_DidNotChangeHowMuchThereIsToBurn()
        {
            // 배치 전 실측값. 장식물이 불연성이 아니게 되면 여기서 먼저 걸린다 —
            // 소실률(IntactRatio)의 분모가 곧 이 숫자다.
            var flammable = new Dictionary<int, int> { { 0, 40 }, { 1, 170 }, { 2, 125 }, { 3, 203 }, { 4, 77 }, { 5, 113 } };

            foreach (StageDef stage in StageCatalog.All)
            {
                ParsedMap map = MapLoader.Parse(stage.Map);

                int count = 0;
                for (int i = 0; i < map.Grid.Count; i++)
                {
                    if (Materials.Of(map.Grid.Cells[i].Material).Flammable) count++;
                }

                Assert.Equal(flammable[stage.Id], count);
            }
        }

        // --- TC-9 ---
        [Fact]
        public void Scenery_StaysOffTheCellsTheScreenshotHarnessAimsAt()
        {
            // 하니스가 좌표로 직접 겨누는 칸들. 여기에 장식물을 놓으면 캡처가 엉뚱한 것을 찍거나
            // 플레이어를 못 세워 예외가 난다. 빌드도 테스트도 통과하므로 여기서만 잡힌다.
            var aimed = new Dictionary<int, GridPoint[]>
            {
                { 0, new[]
                    {
                        new GridPoint(21, 9),                                        // 11_residential_fire 포커스
                        new GridPoint(14, 8), new GridPoint(15, 8),
                        new GridPoint(16, 8), new GridPoint(17, 8),                   // 14/17 방화선·강제 발화
                        new GridPoint(16, 9),                                        // 플레이어 (16.5, 9.5)
                    }
                },
                { 1, new[]
                    {
                        new GridPoint(10, 7),                                        // 12_shopping_electric 포커스
                        new GridPoint(8, 5),                                         // 52_inside_shop 스폰
                        new GridPoint(20, 14),                                       // 53_inside_rescue 스폰
                    }
                },
                { 2, new[] { new GridPoint(22, 10) } },                              // 13_gasstation_oil 포커스
                { 3, new[] { new GridPoint(24, 8) } },                               // 40_warehouse_fire 포커스
                { 4, new[] { new GridPoint(19, 6) } },                               // 41_factory_mixed 포커스
                { 5, new[] { new GridPoint(18, 9) } },                               // 42_harbor_finale 포커스
            };

            foreach (StageDef stage in StageCatalog.All)
            {
                ParsedMap map = MapLoader.Parse(stage.Map);
                foreach (GridPoint cell in aimed[stage.Id])
                {
                    Assert.NotEqual(
                        MaterialId.Scenery,
                        (MaterialId)map.Grid[cell.X, cell.Y].Material);
                }
            }
        }

        private static IEnumerable<GridPoint> SceneryCells(FireGrid grid)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if ((MaterialId)grid[x, y].Material == MaterialId.Scenery) yield return new GridPoint(x, y);
                }
            }
        }

        /// <summary>스폰에서 걸어 닿는 칸. 통행 가능한 칸만 지난다.</summary>
        private static bool[] WalkFrom(FireGrid grid, GridPoint spawn)
        {
            var reached = new bool[grid.Count];
            var queue = new Queue<GridPoint>();
            reached[grid.Index(spawn.X, spawn.Y)] = true;
            queue.Enqueue(spawn);

            int[] dx = { 0, 0, -1, 1 };
            int[] dy = { -1, 1, 0, 0 };

            while (queue.Count > 0)
            {
                GridPoint p = queue.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    int nx = p.X + dx[k];
                    int ny = p.Y + dy[k];
                    if (!grid.InBounds(nx, ny)) continue;

                    int n = grid.Index(nx, ny);
                    if (reached[n]) continue;
                    if (!Materials.Of(grid.Cells[n].Material).Walkable) continue;

                    reached[n] = true;
                    queue.Enqueue(new GridPoint(nx, ny));
                }
            }

            return reached;
        }

        private static bool HasStandingRoom(FireGrid grid, bool[] reached, GridPoint at)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = at.X + dx;
                    int ny = at.Y + dy;
                    if (!grid.InBounds(nx, ny)) continue;
                    if (reached[grid.Index(nx, ny)]) return true;
                }
            }

            return false;
        }
    }
}
