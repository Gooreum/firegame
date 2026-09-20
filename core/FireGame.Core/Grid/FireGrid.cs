using System;

namespace FireGame.Core.Grid
{
    /// <summary>격자 좌표. 튜플 대신 명시적 struct를 쓰면 Unity 쪽에서도 다루기 쉽다.</summary>
    public readonly struct GridPoint : IEquatable<GridPoint>
    {
        public readonly int X;
        public readonly int Y;

        public GridPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(GridPoint other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is GridPoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (X * 397) ^ Y;
        }

        public override string ToString()
        {
            return "(" + X + "," + Y + ")";
        }
    }

    /// <summary>
    /// 화재 시뮬레이션의 격자. 셀을 1차원 배열에 연속 배치해
    /// 매 틱 전체 순회가 캐시 친화적으로 돌게 한다.
    /// </summary>
    public sealed class FireGrid
    {
        public readonly int Width;
        public readonly int Height;

        private readonly Cell[] _cells;

        public FireGrid(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            Width = width;
            Height = height;
            _cells = new Cell[width * height];
        }

        /// <summary>시뮬레이션 내부 루프에서 직접 훑기 위한 백킹 배열.</summary>
        public Cell[] Cells
        {
            get { return _cells; }
        }

        public int Count
        {
            get { return _cells.Length; }
        }

        public int Index(int x, int y)
        {
            return (y * Width) + x;
        }

        public bool InBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        /// <summary>
        /// ref 반환이라 <c>grid[x, y].Heat += 1f</c> 처럼 복사 없이 제자리 수정이 된다.
        /// </summary>
        public ref Cell this[int x, int y]
        {
            get { return ref _cells[(y * Width) + x]; }
        }

        public ref Cell At(GridPoint point)
        {
            return ref _cells[(point.Y * Width) + point.X];
        }

        public int CountBurning()
        {
            int n = 0;
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i].State == CellState.Burning) n++;
            }
            return n;
        }

        /// <summary>수손 피해 정산에 쓰이는, 물을 머금은 셀 수.</summary>
        public int CountWet()
        {
            int n = 0;
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i].Wet > 0f) n++;
            }
            return n;
        }

        /// <summary>
        /// 건물 무결성. 가연 재질 중 아직 소실되지 않은 비율이다.
        /// 바닥·콘크리트는 애초에 타지 않으므로 분모에서 제외해야
        /// "얼마나 지켜냈는가"가 제대로 측정된다.
        /// </summary>
        public float IntactRatio()
        {
            int flammable = 0;
            int surviving = 0;

            for (int i = 0; i < _cells.Length; i++)
            {
                if (!Materials.Of(_cells[i].Material).Flammable) continue;

                flammable++;
                if (_cells[i].State != CellState.Burnt) surviving++;
            }

            // 탈 것이 없는 맵은 언제나 온전한 것으로 본다(0 나눗셈 방지).
            if (flammable == 0) return 1f;

            return (float)surviving / flammable;
        }
    }
}
