using System;
using FireGame.Core.Grid;
using Xunit;

namespace FireGame.Core.Tests
{
    public class GridAndMapTests
    {
        // 5행 8열. 파싱 관련 TC 대부분이 이 맵 하나를 공유한다.
        private static readonly string[] SampleMap =
        {
            "########",
            "#@..W..#",
            "#.~.E.!#",
            "#*..D.H#",
            "###XX###",
        };

        // --- TC-1 ---
        [Fact]
        public void Parse_ProducesGridMatchingMapDimensions()
        {
            var map = MapLoader.Parse(SampleMap);

            Assert.Equal(8, map.Grid.Width);
            Assert.Equal(5, map.Grid.Height);
            Assert.Equal(40, map.Grid.Count);
        }

        // --- TC-2 ---
        [Fact]
        public void Parse_MapsEachCharacterToItsMaterial()
        {
            var g = MapLoader.Parse(SampleMap).Grid;

            Assert.Equal((byte)MaterialId.Concrete, g[0, 0].Material);
            Assert.Equal((byte)MaterialId.Floor, g[2, 1].Material);
            Assert.Equal((byte)MaterialId.Wood, g[4, 1].Material);
            Assert.Equal((byte)MaterialId.Oil, g[2, 2].Material);
            Assert.Equal((byte)MaterialId.Electric, g[4, 2].Material);
            Assert.Equal((byte)MaterialId.Door, g[4, 3].Material);
            Assert.Equal((byte)MaterialId.Hydrant, g[6, 3].Material);
            Assert.Equal((byte)MaterialId.Exit, g[3, 4].Material);
        }

        // --- TC-3 ---
        [Fact]
        public void Parse_ExtractsPlayerSpawnAndLaysFloorUnderIt()
        {
            var map = MapLoader.Parse(SampleMap);

            Assert.Equal(new GridPoint(1, 1), map.PlayerSpawn);
            Assert.Equal((byte)MaterialId.Floor, map.Grid[1, 1].Material);
        }

        // --- TC-4 ---
        [Fact]
        public void Parse_CollectsCivilianExitAndHydrantMarkers()
        {
            var map = MapLoader.Parse(SampleMap);

            Assert.Single(map.Civilians);
            Assert.Equal(new GridPoint(6, 2), map.Civilians[0]);

            Assert.Equal(2, map.Exits.Count);
            Assert.Contains(new GridPoint(3, 4), map.Exits);
            Assert.Contains(new GridPoint(4, 4), map.Exits);

            Assert.Single(map.Hydrants);
            Assert.Equal(new GridPoint(6, 3), map.Hydrants[0]);
        }

        // --- TC-5 ---
        [Fact]
        public void Parse_TurnsIgnitionMarkerIntoBurningWood()
        {
            var map = MapLoader.Parse(SampleMap);

            ref Cell cell = ref map.Grid[1, 3];
            Assert.Equal((byte)MaterialId.Wood, cell.Material);
            Assert.Equal(CellState.Burning, cell.State);
            Assert.Equal(1f, cell.Fuel);

            Assert.Single(map.IgnitionPoints);
            Assert.Equal(new GridPoint(1, 3), map.IgnitionPoints[0]);
        }

        // --- TC-6 ---
        [Fact]
        public void Parse_RaggedRows_ThrowsWithRowNumber()
        {
            string[] ragged = { "####", "#..#", "###" };

            var ex = Assert.Throws<ArgumentException>(() => MapLoader.Parse(ragged));
            Assert.Contains("2행", ex.Message);
        }

        // --- TC-7 ---
        [Fact]
        public void Parse_UnknownCharacter_ThrowsNamingTheCharacter()
        {
            string[] bad = { "####", "#Z.#", "####" };

            var ex = Assert.Throws<ArgumentException>(() => MapLoader.Parse(bad));
            Assert.Contains("'Z'", ex.Message);
        }

        // --- TC-8 ---
        [Fact]
        public void Parse_NullOrEmptyInput_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => MapLoader.Parse(null));
            Assert.Throws<ArgumentException>(() => MapLoader.Parse(new string[0]));
            Assert.Throws<ArgumentException>(() => MapLoader.Parse(new[] { "" }));
        }

        // --- TC-9 ---
        [Fact]
        public void InBounds_RejectsCoordinatesOutsideTheGrid()
        {
            var g = new FireGrid(8, 5);

            Assert.True(g.InBounds(0, 0));
            Assert.True(g.InBounds(7, 4));

            Assert.False(g.InBounds(-1, 0));
            Assert.False(g.InBounds(0, -1));
            Assert.False(g.InBounds(8, 0));
            Assert.False(g.InBounds(0, 5));
        }

        // --- TC-10 ---
        [Fact]
        public void Counters_ReportBurningCellsAndIntactRatio()
        {
            string[] rows =
            {
                "WWWW",
                "**..",
            };

            var g = MapLoader.Parse(rows).Grid;

            Assert.Equal(2, g.CountBurning());

            // 가연 셀은 윗줄 목재 4 + 발화점 목재 2 = 6, 아직 소실된 것은 없다.
            Assert.Equal(1f, g.IntactRatio());

            // 한 칸을 소실시키면 6분의 5가 남는다.
            g[0, 1].State = CellState.Burnt;
            Assert.Equal(5f / 6f, g.IntactRatio(), 5);
        }

        // --- TC-11 ---
        [Fact]
        public void IntactRatio_WithNoFlammableCells_ReturnsOne()
        {
            string[] rows = { "####", "..##" };

            var g = MapLoader.Parse(rows).Grid;

            Assert.Equal(1f, g.IntactRatio());
        }

        // --- TC-12 ---
        [Fact]
        public void Indexer_ReturnsByReference_SoMutationsPersist()
        {
            var g = new FireGrid(4, 4);

            g[2, 3].Heat += 1.5f;
            g[2, 3].Wet = 0.4f;

            Assert.Equal(1.5f, g[2, 3].Heat);
            Assert.Equal(0.4f, g[2, 3].Wet);
            Assert.Equal(1, g.CountWet());

            // 백킹 배열의 동일 인덱스에도 반영돼 있어야 한다.
            Assert.Equal(1.5f, g.Cells[g.Index(2, 3)].Heat);
        }

        [Fact]
        public void FireGrid_RejectsNonPositiveDimensions()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FireGrid(0, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FireGrid(4, -1));
        }

        [Fact]
        public void Materials_TableIndicesMatchMaterialIdEnum()
        {
            Assert.Equal("Floor", Materials.Of(MaterialId.Floor).Name);
            Assert.Equal("Wood", Materials.Of(MaterialId.Wood).Name);
            Assert.Equal("Exit", Materials.Of(MaterialId.Exit).Name);

            Assert.True(Materials.Of(MaterialId.Wood).Flammable);
            Assert.False(Materials.Of(MaterialId.Concrete).Flammable);
            Assert.Equal(FireClass.B, Materials.Of(MaterialId.Oil).Class);
            Assert.Equal(FireClass.C, Materials.Of(MaterialId.Electric).Class);
        }
    }
}
