using System;

namespace FireGame.Core.Grid
{
    /// <summary>
    /// 지금 소방관에게 무엇이 보이는가.
    ///
    /// <b>건물에 속하지 않은 칸은 언제나 보인다</b> — 마당도, 맵 테두리도.
    /// 마당까지 안개로 덮으면 탐색이 아니라 더듬기가 되고, 테두리를 덮으면
    /// 화면 가장자리가 까맣게 죽기만 한다. 가리는 것은 건물 안뿐이고,
    /// 그래서 "들어가야 안다"가 성립한다.
    ///
    /// 한 번 본 칸은 <see cref="Known"/>으로 남는다. 화면은 이걸 어둡게 그려
    /// "아까 봤을 때는 이랬다"를 보여준다 — 같은 방을 매번 다시 더듬게 하지 않는다.
    ///
    /// 난수를 쓰지 않는다. 같은 자리에 서면 언제나 같은 것이 보인다.
    /// </summary>
    public sealed class VisionField
    {
        /// <summary>연기 없는 실내에서 보이는 거리(칸).</summary>
        public const float ClearSight = 9f;

        /// <summary>
        /// 연기가 가득한 칸을 지날 때 한 칸이 잡아먹는 시야.
        /// <c>ClearSight / (1 + SmokeCost)</c>가 연기 속 실효 사거리라
        /// 4.6이면 1.6칸이 된다 — 손 뻗으면 닿는 데까지만 보인다.
        /// </summary>
        public const float SmokeCost = 4.6f;

        private readonly int _width;
        private readonly int _height;
        private readonly bool[] _visible;
        private readonly bool[] _known;

        public VisionField(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            _width = width;
            _height = height;
            _visible = new bool[width * height];
            _known = new bool[width * height];
        }

        /// <summary>지금 이 순간 보이는 칸인지.</summary>
        public bool Visible(int x, int y)
        {
            return InBounds(x, y) && _visible[(y * _width) + x];
        }

        /// <summary>한 번이라도 본 적 있는 칸인지. 지금 안 보여도 참일 수 있다.</summary>
        public bool Known(int x, int y)
        {
            return InBounds(x, y) && _known[(y * _width) + x];
        }

        /// <summary>
        /// 소방관 자리에서 다시 계산한다.
        ///
        /// 건물 칸마다 소방관까지 직선을 그어(브레젠험) 벽에 막히는지 보고,
        /// 지나온 칸의 연기만큼 남은 시야를 깎는다. 막는 칸 자체는 보인다 —
        /// 앞을 가로막은 벽이 안 보이면 길을 찾을 수가 없다.
        ///
        /// 시뮬레이션 틱(10Hz)에서만 부른다. 프레임마다 돌릴 이유가 없고,
        /// 그래야 프레임레이트가 달라도 같은 것이 보인다.
        /// </summary>
        public void Refresh(FireGrid grid, BuildingMap buildings, int px, int py)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));

            Array.Clear(_visible, 0, _visible.Length);

            int reach = (int)ClearSight;

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int index = (y * _width) + x;

                    // 건물 밖은 늘 보인다. 마당·테두리·마당에 홀로 선 설비가 여기 든다.
                    if (buildings == null || buildings.At(x, y) == BuildingMap.None)
                    {
                        _visible[index] = true;
                        _known[index] = true;
                        continue;
                    }

                    if (x < px - reach || x > px + reach) continue;
                    if (y < py - reach || y > py + reach) continue;

                    if (!Sees(grid, px, py, x, y)) continue;

                    _visible[index] = true;
                    _known[index] = true;
                }
            }
        }

        /// <summary>
        /// (px,py)에서 (tx,ty)가 보이는지. 브레젠험으로 한 칸씩 나아가며
        /// 남은 시야를 깎고, 통행 불가 칸을 만나면 거기서 끊는다.
        /// </summary>
        private static bool Sees(FireGrid grid, int px, int py, int tx, int ty)
        {
            if (px == tx && py == ty) return true;

            int dx = Math.Abs(tx - px);
            int dy = Math.Abs(ty - py);
            int stepX = tx > px ? 1 : -1;
            int stepY = ty > py ? 1 : -1;
            int error = dx - dy;

            int x = px;
            int y = py;
            float budget = ClearSight;

            while (true)
            {
                int doubled = error * 2;
                if (doubled > -dy)
                {
                    error -= dy;
                    x += stepX;
                }
                if (doubled < dx)
                {
                    error += dx;
                    y += stepY;
                }

                if (!grid.InBounds(x, y)) return false;

                // 지나온 만큼 시야를 깎는다. 연기가 짙을수록 한 칸이 비싸다.
                budget -= 1f + (grid[x, y].Smoke * SmokeCost);
                if (budget < 0f) return false;

                // 목표에 닿았다. 그 칸이 벽이어도 보인다 — 앞을 막은 벽은 보여야 한다.
                if (x == tx && y == ty) return true;

                // 가는 길을 막는 것이 있으면 그 너머는 못 본다. 닫힌 문도 막는다.
                if (!Materials.Of(grid[x, y].Material).Walkable) return false;
                if (grid[x, y].Shut) return false;
            }
        }

        private bool InBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < _width && y < _height;
        }
    }
}
