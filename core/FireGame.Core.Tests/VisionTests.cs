using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using Xunit;

namespace FireGame.Core.Tests
{
    /// <summary>
    /// 시야. 이 게임에서 정보가 처음으로 공짜가 아니게 되는 지점이다.
    /// 가리는 것은 <b>건물 안뿐</b>이고 마당과 테두리는 늘 보인다.
    /// </summary>
    public class VisionTests
    {
        // 가운데에 12x3짜리 방 하나. 아래쪽 벽 가운데에 문이 있다.
        private static readonly string[] RoomMap =
        {
            "####################",
            "#..................#",
            "#..WWWWWWWWWWWWWW..#",
            "#..W............W..#",
            "#..W............W..#",
            "#..W............W..#",
            "#..WWWWWWWDWWWWWW..#",
            "#..................#",
            "#........@.........#",
            "#..................#",
            "#..................#",
            "####################",
        };

        private static VisionField Look(out FireGrid grid, out BuildingMap buildings, int px, int py)
        {
            ParsedMap map = MapLoader.Parse(RoomMap);
            grid = map.Grid;
            buildings = BuildingMap.From(grid, map.PlayerSpawn);

            var vision = new VisionField(grid.Width, grid.Height);
            vision.Refresh(grid, buildings, px, py);
            return vision;
        }

        // --- TC-1 ---
        [Fact]
        public void OutsideTheBuilding_IsAlwaysVisible()
        {
            VisionField vision = Look(out FireGrid _, out BuildingMap buildings, 4, 4);

            // 소방관이 방 안 깊숙이 있어도 마당과 테두리는 보인다.
            Assert.True(vision.Visible(1, 1));
            Assert.True(vision.Visible(18, 10));
            Assert.True(vision.Visible(0, 0));
            Assert.True(buildings.At(4, 4) != BuildingMap.None, "방 안이 건물로 잡혀야 한다");
        }

        // --- TC-2 ---
        [Fact]
        public void InsideTheRoom_SeesNearbyCells()
        {
            VisionField vision = Look(out FireGrid _, out BuildingMap _, 5, 4);

            Assert.True(vision.Visible(5, 4), "선 자리");
            Assert.True(vision.Visible(6, 4), "바로 옆");
            Assert.True(vision.Visible(8, 3), "세 칸 앞 대각");
        }

        // --- TC-3 ---
        [Fact]
        public void FromOutside_TheRoomInteriorIsHidden()
        {
            // 마당(9,8)에서 본다. 문(10,6) 바로 앞이 아니라 두 칸 아래다.
            VisionField vision = Look(out FireGrid _, out BuildingMap _, 4, 9);

            Assert.False(vision.Visible(5, 4), "벽 뒤 방 안");
            Assert.False(vision.Visible(13, 3), "방 반대편");
        }

        // --- TC-4 ---
        [Fact]
        public void Smoke_ShortensSight()
        {
            ParsedMap map = MapLoader.Parse(RoomMap);
            FireGrid grid = map.Grid;
            BuildingMap buildings = BuildingMap.From(grid, map.PlayerSpawn);

            var clear = new VisionField(grid.Width, grid.Height);
            clear.Refresh(grid, buildings, 5, 4);
            Assert.True(clear.Visible(12, 4), "연기가 없으면 일곱 칸 앞이 보인다");

            for (int i = 0; i < grid.Count; i++) grid.Cells[i].Smoke = 1f;

            var choked = new VisionField(grid.Width, grid.Height);
            choked.Refresh(grid, buildings, 5, 4);

            Assert.True(choked.Visible(6, 4), "연기 속에서도 손 뻗는 데까지는 보인다");
            Assert.False(choked.Visible(12, 4), "연기가 차면 일곱 칸 앞은 안 보인다");
        }

        // --- TC-5 ---
        [Fact]
        public void OnceSeen_StaysKnown()
        {
            ParsedMap map = MapLoader.Parse(RoomMap);
            FireGrid grid = map.Grid;
            BuildingMap buildings = BuildingMap.From(grid, map.PlayerSpawn);

            var vision = new VisionField(grid.Width, grid.Height);
            vision.Refresh(grid, buildings, 5, 4);
            Assert.True(vision.Visible(6, 4));

            // 방에서 나와 마당으로. 방 안은 더 이상 안 보이지만 기억은 남는다.
            vision.Refresh(grid, buildings, 4, 9);

            Assert.False(vision.Visible(6, 4), "나오면 안 보인다");
            Assert.True(vision.Known(6, 4), "본 적은 있다");
        }

        // --- TC-6 ---
        [Fact]
        public void StageRunner_BuildsVisionAndRefreshesEveryTick()
        {
            var runner = new StageRunner(StageCatalog.Residential, new[] { EquipmentId.Bucket });

            Assert.NotNull(runner.Buildings);
            Assert.NotNull(runner.Vision);
            Assert.True(runner.Vision.Visible(runner.Player.CellX, runner.Player.CellY),
                "선 자리는 언제나 보인다");

            int before = runner.TicksElapsed;
            runner.Update(0.5f, default);
            Assert.True(runner.TicksElapsed > before, "틱이 돌아야 시야도 갱신된다");
        }

        // --- TC-7 ---
        [Fact]
        public void CiviliansOutside_AreSpottedImmediately()
        {
            int outdoorCivilians = 0;

            foreach (StageDef stage in StageCatalog.All)
            {
                var runner = new StageRunner(stage, new[] { EquipmentId.Bucket });
                runner.Update(0.2f, default);

                foreach (Civilian civilian in runner.Civilians)
                {
                    int cx = (int)civilian.X;
                    int cy = (int)civilian.Y;
                    if (!runner.Buildings.IsOutdoor(cx, cy)) continue;

                    outdoorCivilians++;
                    Assert.True(civilian.Spotted,
                        stage.Name + " 마당의 시민 (" + cx + "," + cy + ")이 안 보인다");
                }
            }

            Assert.True(outdoorCivilians > 0, "마당에 선 시민이 한 명도 없으면 이 검사는 무의미하다");
        }

        // --- TC-8 ---
        [Fact]
        public void CiviliansDeepInside_AreNotSpottedAtStart()
        {
            int hidden = 0;

            foreach (StageDef stage in StageCatalog.All)
            {
                var runner = new StageRunner(stage, new[] { EquipmentId.Bucket });
                runner.Update(0.2f, default);

                foreach (Civilian civilian in runner.Civilians)
                {
                    if (!civilian.Spotted) hidden++;
                }
            }

            // 한 명도 안 숨으면 시야를 가린 의미가 없다.
            Assert.True(hidden > 0, "시작 시점에 못 찾는 시민이 한 명은 있어야 한다");
        }
    }
}
