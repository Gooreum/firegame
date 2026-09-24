using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using Xunit;

namespace FireGame.Core.Tests
{
    /// <summary>
    /// 맵 문자열에서 "건물 한 채"를 되찾아 내는지. 화면이 지붕을 덮고 카메라를 맞추는 근거라서,
    /// 건물 수가 한 채라도 어긋나면 지붕이 엉뚱한 곳을 덮는다.
    /// </summary>
    public class BuildingMapTests
    {
        private static BuildingMap MapOf(StageDef stage)
        {
            ParsedMap parsed = MapLoader.Parse(stage.Map);
            return BuildingMap.From(parsed.Grid, parsed.PlayerSpawn);
        }

        // --- TC-1 ---
        [Fact]
        public void EveryStage_FindsTheBuildingsThatAreDrawnInTheMap()
        {
            var expected = new Dictionary<int, int>
            {
                { StageCatalog.Residential.Id, 1 },   // 칸막이로 방을 나눈 집 한 채
                { StageCatalog.Shopping.Id, 6 },      // 점포 6채
                { StageCatalog.GasStation.Id, 4 },
                { StageCatalog.Warehouse.Id, 2 },     // 큰 창고 + 사무동
                { StageCatalog.Factory.Id, 2 },       // 배전동 + 공장동
                { StageCatalog.Harbor.Id, 4 },
            };

            foreach (StageDef stage in StageCatalog.All)
            {
                BuildingMap map = MapOf(stage);
                Assert.Equal(expected[stage.Id], map.All.Length);
                System.Console.WriteLine(stage.Name + ": 건물 " + map.All.Length + "채");
            }
        }

        // --- TC-2 ---
        [Fact]
        public void TheMapBorder_IsNotABuilding()
        {
            BuildingMap map = MapOf(StageCatalog.Residential);

            // 테두리는 실외도 아니고(통행 불가) 건물도 아니다. 덮으면 화면 가장자리가 막힌다.
            Assert.Equal(BuildingMap.None, map.At(0, 0));
            Assert.Equal(BuildingMap.None, map.At(39, 0));
            Assert.Equal(BuildingMap.None, map.At(0, 21));
            Assert.False(map.IsOutdoor(0, 0));
        }

        // --- TC-3 ---
        [Fact]
        public void SpawnExitAndHydrant_AreAlwaysOutdoors()
        {
            foreach (StageDef stage in StageCatalog.All)
            {
                ParsedMap parsed = MapLoader.Parse(stage.Map);
                BuildingMap map = BuildingMap.From(parsed.Grid, parsed.PlayerSpawn);

                Assert.True(map.IsOutdoor(parsed.PlayerSpawn.X, parsed.PlayerSpawn.Y), stage.Name + " 스폰이 실내다");
                Assert.Equal(BuildingMap.None, map.At(parsed.PlayerSpawn.X, parsed.PlayerSpawn.Y));

                foreach (GridPoint exit in parsed.Exits)
                {
                    Assert.True(map.IsOutdoor(exit.X, exit.Y), stage.Name + " 출구가 실내다");
                    Assert.Equal(BuildingMap.None, map.At(exit.X, exit.Y));
                }

                // 급수전은 통행 불가라 실외로 칠해지지 않지만, 마당에 홀로 선 설비지 건물이 아니다.
                // 여기서 건물이 되면 지붕이 급수전 한 칸을 덮어 버린다.
                foreach (GridPoint hydrant in parsed.Hydrants)
                {
                    Assert.Equal(BuildingMap.None, map.At(hydrant.X, hydrant.Y));
                }
            }
        }

        // --- TC-4 ---
        [Fact]
        public void EveryBuilding_HasAtLeastOneWayIn()
        {
            foreach (StageDef stage in StageCatalog.All)
            {
                BuildingMap map = MapOf(stage);
                foreach (Building building in map.All)
                {
                    // 지붕으로 덮어 놓고 못 들어가는 건물이 있으면 그 현장은 깰 수 없다.
                    Assert.True(
                        building.Entrances.Length >= 1,
                        stage.Name + " " + building.Id + "번 건물에 밖으로 통하는 문이 없다");
                }
            }
        }

        // --- TC-5 ---
        [Fact]
        public void Warehouse_HasOneCivilianStandingOutInTheYard()
        {
            ParsedMap parsed = MapLoader.Parse(StageCatalog.Warehouse.Map);
            BuildingMap map = BuildingMap.From(parsed.Grid, parsed.PlayerSpawn);

            int indoors = 0;
            int outdoors = 0;
            foreach (GridPoint civilian in parsed.Civilians)
            {
                if (map.At(civilian.X, civilian.Y) == BuildingMap.None) outdoors++;
                else indoors++;
            }

            // 마당에 선 사람도 있다. "실내 시민만 센다"로 굳으면 Help! 표시가 한 명을 빠뜨린다.
            Assert.Equal(3, parsed.Civilians.Count);
            Assert.Equal(2, indoors);
            Assert.Equal(1, outdoors);
        }

        // --- TC-6 ---
        [Fact]
        public void InnerPartitions_BelongToTheirBuilding_NotTheirOwn()
        {
            BuildingMap map = MapOf(StageCatalog.Factory);

            // 공장동 5행: `#.#.........#..W..WWW......W...` — 18~20열 WWW는 공중에 뜬 칸막이다.
            int wall = map.At(15, 5);       // 공장동 왼쪽 외벽
            int partition = map.At(19, 5);  // 그 안의 칸막이

            Assert.NotEqual(BuildingMap.None, wall);
            Assert.Equal(wall, partition);
            Assert.Equal(2, map.All.Length);
        }

        // --- TC-7 ---
        [Fact]
        public void BurningCellsAndClass_FollowTheGrid()
        {
            ParsedMap parsed = MapLoader.Parse(StageCatalog.Shopping.Map);
            BuildingMap map = BuildingMap.From(parsed.Grid, parsed.PlayerSpawn);

            // 상가 5행 6열은 `$` — 불붙은 배전반이다.
            Building shop = map.Of(6, 5);
            Assert.NotNull(shop);
            Assert.Equal(1, shop.BurningCells(parsed.Grid));
            Assert.Equal(FireClass.C, shop.DominantFireClass(parsed.Grid));

            // 불이 없는 점포는 0과 None이다.
            Building quiet = map.Of(17, 4);
            Assert.NotNull(quiet);
            Assert.NotEqual(shop.Id, quiet.Id);
            Assert.Equal(0, quiet.BurningCells(parsed.Grid));
            Assert.Equal(FireClass.None, quiet.DominantFireClass(parsed.Grid));
        }

        // --- TC-8 ---
        [Fact]
        public void OutOfBoundsLookups_AnswerInsteadOfThrowing()
        {
            BuildingMap map = MapOf(StageCatalog.Residential);

            Assert.Equal(BuildingMap.None, map.At(-1, 5));
            Assert.Equal(BuildingMap.None, map.At(40, 5));
            Assert.Equal(BuildingMap.None, map.At(5, -1));
            Assert.Equal(BuildingMap.None, map.At(5, 22));
            Assert.Null(map.Of(-1, -1));
            Assert.False(map.IsOutdoor(-1, -1));
        }
    }
}
