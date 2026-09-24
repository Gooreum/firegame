using FireGame.Core.Grid;
using FireGame.Core.Sim;
using Xunit;

namespace FireGame.Core.Tests
{
    /// <summary>
    /// 마당에 놓는 설비·수목(<see cref="MaterialId.Scenery"/>)이 게임 규칙을 건드리지 않는지.
    ///
    /// 이 재질을 넣은 이유는 현장을 현장답게 채우기 위해서지 난이도를 바꾸기 위해서가 아니다.
    /// 불연성이라는 성질 하나로 소실률·별 기준·불 확산이 전부 그대로 유지되는데,
    /// 그 성질이 깨지면 여섯 현장의 밸런스가 소리 없이 무너진다.
    /// </summary>
    public class SceneryTests
    {
        // --- TC-1 ---
        [Fact]
        public void Scenery_BlocksTheWayButNeverBurns()
        {
            CellMaterial scenery = Materials.Of(MaterialId.Scenery);

            Assert.False(scenery.Flammable);
            Assert.True(float.IsPositiveInfinity(scenery.Ignite));
            Assert.Equal(0f, scenery.BurnRate);
            Assert.Equal(0f, scenery.HeatOutput);
            Assert.Equal(FireClass.None, scenery.Class);

            // 통행은 막는다 — 그래야 마당에 실제로 놓인 물건이 된다.
            Assert.False(scenery.Walkable);
        }

        // --- TC-2 ---
        [Fact]
        public void TheMapCharacterO_MeansScenery()
        {
            Assert.True(Materials.TryFromMapChar('o', out MaterialId material));
            Assert.Equal(MaterialId.Scenery, material);
        }

        // --- TC-3 ---
        [Fact]
        public void AddingScenery_DidNotShiftAnyExistingMaterialId()
        {
            // Materials.All의 인덱스와 일치해야 한다. 하나라도 밀리면 저장된 맵이 전부 다른 재질로 읽힌다.
            Assert.Equal(0, (int)MaterialId.Floor);
            Assert.Equal(1, (int)MaterialId.Wood);
            Assert.Equal(2, (int)MaterialId.Concrete);
            Assert.Equal(3, (int)MaterialId.Oil);
            Assert.Equal(4, (int)MaterialId.Electric);
            Assert.Equal(5, (int)MaterialId.Hydrant);
            Assert.Equal(6, (int)MaterialId.Door);
            Assert.Equal(7, (int)MaterialId.Exit);
            Assert.Equal(8, (int)MaterialId.Scenery);

            Assert.Equal(9, Materials.All.Length);
            Assert.Equal("Scenery", Materials.Of(MaterialId.Scenery).Name);
        }

        // --- TC-4 ---
        [Fact]
        public void AMapWithScenery_ParsesWithoutComplaint()
        {
            ParsedMap map = MapLoader.Parse(new[]
            {
                "#####",
                "#@.o#",
                "#...#",
                "#####",
            });

            Assert.Equal(MaterialId.Scenery, (MaterialId)map.Grid[3, 1].Material);
            Assert.Equal(MaterialId.Floor, (MaterialId)map.Grid[2, 1].Material);
        }

        // --- TC-5 ---
        [Fact]
        public void AYardFullOfScenery_DoesNotCountAsSomethingThatCouldBurn()
        {
            // 장식물만 있는 맵은 "탈 것이 없는 맵"이라 언제나 온전하다.
            // 여기서 1f가 안 나오면 마당을 채울 때마다 별 기준이 흔들린다는 뜻이다.
            ParsedMap map = MapLoader.Parse(new[]
            {
                "......",
                ".@oooo",
                ".ooooo",
                "......",
            });

            Assert.Equal(1f, map.Grid.IntactRatio());
        }

        // --- TC-6 ---
        [Fact]
        public void NoAmountOfHeat_EverSetsSceneryOnFire()
        {
            // 목재 한 칸을 태워 장식물을 사방에서 지지게 둔다.
            ParsedMap map = MapLoader.Parse(new[]
            {
                "ooooo",
                "oo*oo",
                "ooooo",
            });

            FireGrid grid = map.Grid;
            var sim = new FireSim(grid);

            for (int step = 0; step < 600; step++) sim.Tick();

            int scenery = 0;
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if ((MaterialId)grid[x, y].Material != MaterialId.Scenery) continue;
                    scenery++;
                    Assert.Equal(CellState.Intact, grid[x, y].State);
                }
            }

            Assert.Equal(14, scenery);
        }

        // --- TC-7 ---
        [Fact]
        public void SceneryTouchingAWall_StaysOutOfTheBuilding()
        {
            // 나무를 건물 외벽에 바짝 붙여 심었다. 지붕이 그 위까지 덮이면 안 된다.
            ParsedMap map = MapLoader.Parse(new[]
            {
                "..........",
                "..WWWWWW..",
                "..W....W..",
                "..W....W..",
                "..WWDWWW..",
                "..oo......",
                "...@......",
                "..........",
            });

            BuildingMap buildings = BuildingMap.From(map.Grid, map.PlayerSpawn);

            Assert.Single(buildings.All);
            foreach (GridPoint cell in buildings.All[0].Cells)
            {
                Assert.NotEqual(MaterialId.Scenery, (MaterialId)map.Grid[cell.X, cell.Y].Material);
            }

            // 벽에 닿아 있어도 마당이다 — 밑에 깔리는 바닥도 마당 바닥이 된다.
            Assert.Equal(BuildingMap.None, buildings.At(2, 5));
            Assert.Equal(BuildingMap.None, buildings.At(3, 5));
            Assert.True(buildings.IsOutdoor(2, 5));
            Assert.True(buildings.IsOutdoor(3, 5));
        }

        // --- TC-8 ---
        [Fact]
        public void SceneryStandingAloneInTheYard_IsNotABuildingOfItsOwn()
        {
            ParsedMap map = MapLoader.Parse(new[]
            {
                "..........",
                "..WWWWWW..",
                "..W....W..",
                "..WWDWWW..",
                "..........",
                ".o...o..o.",
                "...@......",
                ".o......o.",
            });

            BuildingMap buildings = BuildingMap.From(map.Grid, map.PlayerSpawn);

            // 마당에 홀로 선 나무 여섯 그루가 건물 여섯 채로 둔갑하면 지붕이 나무 위에 덮인다.
            Assert.Single(buildings.All);
        }
    }
}
