using System;
using FireGame.Core.Game;
using FireGame.Core.Grid;

namespace FireGame.Core.Render
{
    /// <summary>
    /// 격자와 등장인물을 프레임버퍼에 그린다.
    ///
    /// 셀 하나가 8x8 픽셀이므로 40x22 격자가 정확히 320x176이 된다.
    /// UnityEngine에 전혀 의존하지 않아 그리기 결과를 테스트로 검증할 수 있다.
    /// </summary>
    public static class SceneRenderer
    {
        public const int TileSize = 8;

        /// <summary>화염 애니메이션 프레임 수.</summary>
        public const int FlameFrames = 3;

        // 재질별 기본 색: 전경, 배경.
        private static readonly byte[] MaterialForeground =
        {
            Palette.DarkGray,    // Floor
            Palette.Brown,       // Wood
            Palette.LightGray,   // Concrete
            Palette.Magenta,     // Oil
            Palette.LightCyan,   // Electric
            Palette.LightRed,    // Hydrant
            Palette.Yellow,      // Door
            Palette.LightGreen,  // Exit
        };

        private static readonly byte[] MaterialBackground =
        {
            Palette.Black,       // Floor
            Palette.Black,       // Wood
            Palette.DarkGray,    // Concrete
            Palette.Black,       // Oil
            Palette.Blue,        // Electric
            Palette.Red,         // Hydrant
            Palette.Brown,       // Door
            Palette.Green,       // Exit
        };

        private static readonly char[] MaterialGlyph =
        {
            '.',  // Floor
            '#',  // Wood
            '#',  // Concrete
            '~',  // Oil
            'E',  // Electric
            'H',  // Hydrant
            'D',  // Door
            'X',  // Exit
        };

        /// <summary>한 판의 화면을 통째로 그린다.</summary>
        public static void Render(FrameBuffer buffer, StageRunner runner, int animationFrame)
        {
            if (buffer == null || runner == null) return;

            DrawGrid(buffer, runner.Grid, animationFrame);

            foreach (Civilian civilian in runner.Civilians)
            {
                if (!civilian.Pending) continue;

                DrawEntity(buffer, civilian.X, civilian.Y, '!', Palette.Black, Palette.Yellow);
            }

            // 사람은 배경색까지 칠해 화염과 바닥 무늬 위에서도 즉시 눈에 띄게 한다.
            // 선만 그리면 불길 한가운데서 자기 캐릭터를 놓친다.
            DrawEntity(buffer, runner.Player.X, runner.Player.Y, '@', Palette.Black, Palette.White);
        }

        public static void DrawGrid(FrameBuffer buffer, FireGrid grid, int animationFrame)
        {
            if (buffer == null || grid == null) return;

            // 격자 영역만 지운다. HUD는 따로 그려지므로 건드리지 않는다.
            buffer.FillRect(0, 0, FrameBuffer.Width, FrameBuffer.PlayfieldHeight, Palette.Black);

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    DrawCell(buffer, grid, x, y, animationFrame);
                }
            }
        }

        private static void DrawCell(FrameBuffer buffer, FireGrid grid, int cellX, int cellY, int animationFrame)
        {
            int px = cellX * TileSize;
            int py = cellY * TileSize;
            if (py >= FrameBuffer.PlayfieldHeight) return;

            ref Cell cell = ref grid[cellX, cellY];
            int material = cell.Material;

            if (cell.State == CellState.Burning)
            {
                DrawFlame(buffer, px, py, cell.Heat, cellX, cellY, animationFrame);
                return;
            }

            byte foreground = MaterialForeground[material];
            byte background = MaterialBackground[material];

            if (cell.State == CellState.Burnt)
            {
                // 타버린 자리는 재만 남는다.
                foreground = Palette.DarkGray;
                background = Palette.Black;
            }
            else if (cell.Wet > 0f)
            {
                // 젖은 칸은 파랗게 보인다. 방화선이 눈에 보여야 전술이 성립한다.
                foreground = Palette.LightBlue;
                background = Palette.Blue;
            }
            else if (cell.Inert > 0f)
            {
                foreground = Palette.LightCyan;
            }

            buffer.DrawGlyph(px, py, MaterialGlyph[material], foreground, background);
        }

        /// <summary>
        /// 화염. 열이 높을수록 갈색 → 빨강 → 밝은 빨강 → 노랑으로 간다.
        /// 애니메이션은 셀 좌표를 섞어 만들어서, 이웃한 불이 한 몸처럼 깜빡이지 않게 한다.
        /// </summary>
        private static void DrawFlame(
            FrameBuffer buffer, int px, int py, float heat, int cellX, int cellY, int animationFrame)
        {
            byte core = HeatColor(heat);
            byte edge = heat > 1.5f ? Palette.LightRed : Palette.Red;

            buffer.FillRect(px, py, TileSize, TileSize, Palette.Black);

            int phase = ((cellX * 7) + (cellY * 13) + animationFrame) % FlameFrames;

            for (int y = 0; y < TileSize; y++)
            {
                // 위로 갈수록 좁아지는 불꽃 모양.
                int halfWidth = 1 + (y / 2);
                int wobble = ((y + phase) % 3) - 1;
                int centerX = (TileSize / 2) + wobble;

                for (int x = centerX - halfWidth; x <= centerX + halfWidth; x++)
                {
                    if (x < 0 || x >= TileSize) continue;

                    bool isCore = Math.Abs(x - centerX) <= halfWidth / 2 && y >= TileSize / 2;
                    buffer.Set(px + x, py + y, isCore ? core : edge);
                }
            }
        }

        public static byte HeatColor(float heat)
        {
            if (heat < 0.6f) return Palette.Brown;
            if (heat < 1.2f) return Palette.Red;
            if (heat < 2.0f) return Palette.LightRed;
            return Palette.Yellow;
        }

        private static void DrawEntity(
            FrameBuffer buffer, float worldX, float worldY, char glyph, byte color, int background = -1)
        {
            int px = (int)Math.Floor(worldX * TileSize) - (TileSize / 2);
            int py = (int)Math.Floor(worldY * TileSize) - (TileSize / 2);

            if (py >= FrameBuffer.PlayfieldHeight) return;

            buffer.DrawGlyph(px, py, glyph, color, background);
        }
    }
}
