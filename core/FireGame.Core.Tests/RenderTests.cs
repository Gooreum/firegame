using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using FireGame.Core.Render;
using Xunit;

namespace FireGame.Core.Tests
{
    public class RenderTests
    {
        private static int CountNonBlack(FrameBuffer buffer, int x, int y, int width, int height)
        {
            int count = 0;
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++)
                {
                    if (buffer.Get(px, py) != Palette.Black) count++;
                }
            }
            return count;
        }

        // --- TC-1 ---
        [Fact]
        public void FrameBuffer_IsExactlyVgaModeThirteenAndStartsBlack()
        {
            var buffer = new FrameBuffer();

            Assert.Equal(320, FrameBuffer.Width);
            Assert.Equal(200, FrameBuffer.Height);
            Assert.Equal(320 * 200, buffer.Pixels.Length);

            // 격자 176 + HUD 24 = 200
            Assert.Equal(FrameBuffer.Height, FrameBuffer.PlayfieldHeight + FrameBuffer.HudHeight);

            foreach (byte pixel in buffer.Pixels)
            {
                Assert.Equal(Palette.Black, pixel);
            }
        }

        // --- TC-2 ---
        [Fact]
        public void SetAndFillRect_TouchOnlyTheRequestedArea()
        {
            var buffer = new FrameBuffer();

            buffer.Set(10, 20, Palette.Red);
            Assert.Equal(Palette.Red, buffer.Get(10, 20));
            Assert.Equal(Palette.Black, buffer.Get(11, 20));

            buffer.FillRect(100, 50, 4, 3, Palette.Yellow);
            Assert.Equal(12, CountNonBlack(buffer, 100, 50, 4, 3));
            Assert.Equal(Palette.Black, buffer.Get(104, 50));
            Assert.Equal(Palette.Black, buffer.Get(100, 53));
        }

        // --- TC-3 ---
        [Fact]
        public void OutOfBoundsDrawing_IsIgnoredWithoutThrowing()
        {
            var buffer = new FrameBuffer();

            buffer.Set(-1, 0, Palette.White);
            buffer.Set(0, -1, Palette.White);
            buffer.Set(FrameBuffer.Width, 0, Palette.White);
            buffer.Set(0, FrameBuffer.Height, Palette.White);
            buffer.FillRect(-50, -50, 10, 10, Palette.White);
            buffer.FillRect(400, 300, 10, 10, Palette.White);

            Assert.Equal(Palette.Black, buffer.Get(-1, 0));
            foreach (byte pixel in buffer.Pixels)
            {
                Assert.Equal(Palette.Black, pixel);
            }
        }

        // --- TC-4 ---
        [Fact]
        public void PartiallyOffscreenRect_IsClipped()
        {
            var buffer = new FrameBuffer();

            buffer.FillRect(-2, -2, 5, 5, Palette.Green);

            // 화면 안에 걸친 3x3만 칠해진다.
            Assert.Equal(9, CountNonBlack(buffer, 0, 0, 5, 5));
            Assert.Equal(Palette.Green, buffer.Get(2, 2));
            Assert.Equal(Palette.Black, buffer.Get(3, 3));
        }

        // --- TC-5 ---
        [Fact]
        public void DrawGlyph_PaintsAnEightByEightBitmap()
        {
            var buffer = new FrameBuffer();

            buffer.DrawGlyph(16, 24, 'A', Palette.White, Palette.Blue);

            int lit = CountNonBlack(buffer, 16, 24, 8, 8);
            Assert.Equal(64, lit);   // 배경을 지정했으므로 8x8 전체가 칠해진다

            int white = 0;
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    if (buffer.Get(16 + x, 24 + y) == Palette.White) white++;
                }
            }

            Assert.True(white > 0 && white < 64, "글자 모양이 나와야 한다. 흰 픽셀 " + white);
        }

        // --- TC-6 ---
        [Fact]
        public void DrawText_SpacesCharactersEightPixelsApart()
        {
            var buffer = new FrameBuffer();

            buffer.DrawText(0, 0, "II", Palette.White);

            int firstGlyph = CountNonBlack(buffer, 0, 0, 8, 8);
            int secondGlyph = CountNonBlack(buffer, 8, 0, 8, 8);

            Assert.True(firstGlyph > 0);
            Assert.Equal(firstGlyph, secondGlyph);
            Assert.Equal(0, CountNonBlack(buffer, 16, 0, 8, 8));
        }

        // --- TC-7 ---
        [Fact]
        public void UnknownCharacters_FallBackToBlankWithoutThrowing()
        {
            var buffer = new FrameBuffer();

            buffer.DrawText(0, 0, "éü☃", Palette.White);

            Assert.Equal(0, CountNonBlack(buffer, 0, 0, 24, 8));
            Assert.False(Glyphs.Has('☃'));

            // 소문자는 대문자로 대체된다.
            Assert.True(Glyphs.Has('a'));
            Assert.Equal(Glyphs.Get('A'), Glyphs.Get('a'));
        }

        // --- TC-8 ---
        [Fact]
        public void Palette_IsTheSixteenColourEgaSet()
        {
            Assert.Equal(16, Palette.ColorCount);

            Assert.Equal(0x00, Palette.R(Palette.Black));
            Assert.Equal(0xFF, Palette.R(Palette.White));
            Assert.Equal(0xFF, Palette.G(Palette.White));
            Assert.Equal(0xFF, Palette.B(Palette.White));

            Assert.Equal(0xAA, Palette.R(Palette.Brown));
            Assert.Equal(0x55, Palette.G(Palette.Brown));
            Assert.Equal(0x00, Palette.B(Palette.Brown));

            Assert.Equal(0xFF, Palette.R(Palette.Yellow));
            Assert.Equal(0xFF, Palette.G(Palette.Yellow));
            Assert.Equal(0x55, Palette.B(Palette.Yellow));
        }

        // --- TC-9 ---
        [Fact]
        public void EachCell_LandsAtEightTimesItsGridCoordinate()
        {
            var grid = new FireGrid(40, 22);
            for (int i = 0; i < grid.Count; i++)
            {
                grid.Cells[i].Material = (byte)MaterialId.Floor;
                grid.Cells[i].State = CellState.Intact;
            }

            grid[5, 3].Material = (byte)MaterialId.Concrete;

            var buffer = new FrameBuffer();
            SceneRenderer.DrawGrid(buffer, grid, 0);

            // 콘크리트는 배경이 있으므로 8x8이 전부 칠해진다.
            Assert.Equal(64, CountNonBlack(buffer, 5 * 8, 3 * 8, 8, 8));
        }

        // --- TC-10 ---
        [Fact]
        public void FlameColour_TracksHeat()
        {
            Assert.Equal(Palette.Brown, SceneRenderer.HeatColor(0.3f));
            Assert.Equal(Palette.Red, SceneRenderer.HeatColor(0.9f));
            Assert.Equal(Palette.LightRed, SceneRenderer.HeatColor(1.5f));
            Assert.Equal(Palette.Yellow, SceneRenderer.HeatColor(3.0f));

            var grid = new FireGrid(4, 4);
            for (int i = 0; i < grid.Count; i++)
            {
                grid.Cells[i].Material = (byte)MaterialId.Wood;
                grid.Cells[i].State = CellState.Intact;
            }

            grid[1, 1].State = CellState.Burning;
            grid[1, 1].Heat = 0.3f;
            grid[2, 2].State = CellState.Burning;
            grid[2, 2].Heat = 3.0f;

            var buffer = new FrameBuffer();
            SceneRenderer.DrawGrid(buffer, grid, 0);

            bool sawBrown = false;
            bool sawYellow = false;
            for (int y = 8; y < 16; y++)
            {
                for (int x = 8; x < 16; x++)
                {
                    if (buffer.Get(x, y) == Palette.Brown) sawBrown = true;
                }
            }
            for (int y = 16; y < 24; y++)
            {
                for (int x = 16; x < 24; x++)
                {
                    if (buffer.Get(x, y) == Palette.Yellow) sawYellow = true;
                }
            }

            Assert.True(sawBrown, "약한 불은 갈색이어야 한다");
            Assert.True(sawYellow, "강한 불은 노란색이어야 한다");
        }

        // --- TC-11 ---
        [Fact]
        public void FlameAnimation_ChangesBetweenFrames()
        {
            var grid = new FireGrid(4, 4);
            for (int i = 0; i < grid.Count; i++)
            {
                grid.Cells[i].Material = (byte)MaterialId.Wood;
                grid.Cells[i].State = CellState.Intact;
            }
            grid[1, 1].State = CellState.Burning;
            grid[1, 1].Heat = 1.0f;

            var first = new FrameBuffer();
            var second = new FrameBuffer();
            SceneRenderer.DrawGrid(first, grid, 0);
            SceneRenderer.DrawGrid(second, grid, 1);

            bool different = false;
            for (int y = 8; y < 16 && !different; y++)
            {
                for (int x = 8; x < 16; x++)
                {
                    if (first.Get(x, y) != second.Get(x, y))
                    {
                        different = true;
                        break;
                    }
                }
            }

            Assert.True(different, "프레임이 바뀌면 불꽃이 움직여야 한다");
        }

        // --- TC-12 ---
        [Fact]
        public void WetCells_LookDifferentFromDryOnes_SoFirebreaksAreVisible()
        {
            var grid = new FireGrid(4, 4);
            for (int i = 0; i < grid.Count; i++)
            {
                grid.Cells[i].Material = (byte)MaterialId.Wood;
                grid.Cells[i].State = CellState.Intact;
            }

            grid[1, 1].Wet = 0.8f;

            var buffer = new FrameBuffer();
            SceneRenderer.DrawGrid(buffer, grid, 0);

            bool different = false;
            for (int y = 0; y < 8 && !different; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    if (buffer.Get(8 + x, 8 + y) != buffer.Get(16 + x, 8 + y))
                    {
                        different = true;
                        break;
                    }
                }
            }

            Assert.True(different, "젖은 칸은 마른 칸과 달라 보여야 방화선이 눈에 보인다");
        }

        // --- TC-13 ---
        [Fact]
        public void PlayerAndCivilians_AreDrawnAtTheirPositions()
        {
            var runner = new StageRunner(StageCatalog.Residential, new List<int> { EquipmentId.Bucket });
            var buffer = new FrameBuffer();

            SceneRenderer.Render(buffer, runner, 0);

            int px = (int)(runner.Player.X * 8) - 4;
            int py = (int)(runner.Player.Y * 8) - 4;

            bool sawWhite = false;
            for (int y = 0; y < 8 && !sawWhite; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    if (buffer.Get(px + x, py + y) == Palette.White)
                    {
                        sawWhite = true;
                        break;
                    }
                }
            }

            Assert.True(sawWhite, "플레이어 타일이 흰 배경으로 눈에 띄어야 한다");
            Assert.NotEmpty(runner.Civilians);
        }

        // --- TC-14 ---
        [Fact]
        public void GridRendering_NeverTouchesTheHudStrip()
        {
            var runner = new StageRunner(StageCatalog.Shopping, new List<int> { EquipmentId.Bucket });
            var buffer = new FrameBuffer();

            // HUD 영역을 표식으로 칠해두고 격자를 그린 뒤 그대로인지 본다.
            buffer.FillRect(0, FrameBuffer.PlayfieldHeight, FrameBuffer.Width, FrameBuffer.HudHeight, Palette.Magenta);
            SceneRenderer.Render(buffer, runner, 0);

            for (int y = FrameBuffer.PlayfieldHeight; y < FrameBuffer.Height; y++)
            {
                for (int x = 0; x < FrameBuffer.Width; x++)
                {
                    Assert.Equal(Palette.Magenta, buffer.Get(x, y));
                }
            }
        }

        // --- TC-15 ---
        [Fact]
        public void RenderingAgain_DoesNotLeaveTheOldFrameBehind()
        {
            var grid = new FireGrid(4, 4);
            for (int i = 0; i < grid.Count; i++)
            {
                grid.Cells[i].Material = (byte)MaterialId.Wood;
                grid.Cells[i].State = CellState.Intact;
            }
            grid[1, 1].State = CellState.Burning;
            grid[1, 1].Heat = 3.0f;

            var buffer = new FrameBuffer();
            SceneRenderer.DrawGrid(buffer, grid, 0);

            bool sawYellow = false;
            for (int y = 8; y < 16 && !sawYellow; y++)
            {
                for (int x = 8; x < 16; x++)
                {
                    if (buffer.Get(x, y) == Palette.Yellow) sawYellow = true;
                }
            }
            Assert.True(sawYellow);

            // 불을 끄고 다시 그리면 화염 픽셀이 남아 있으면 안 된다.
            grid[1, 1].State = CellState.Intact;
            grid[1, 1].Heat = 0f;
            SceneRenderer.DrawGrid(buffer, grid, 0);

            for (int y = 8; y < 16; y++)
            {
                for (int x = 8; x < 16; x++)
                {
                    Assert.NotEqual(Palette.Yellow, buffer.Get(x, y));
                }
            }
        }
    }
}
