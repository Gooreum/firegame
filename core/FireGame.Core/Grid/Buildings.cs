using System;
using System.Collections.Generic;

namespace FireGame.Core.Grid
{
    /// <summary>
    /// 건물 한 채. 벽·문·그 안의 칸을 한 덩어리로 묶은 것이다.
    ///
    /// 불 확산이나 점수에는 쓰지 않는다 — 화면이 지붕을 덮고 카메라를 맞추는 데만 쓴다.
    /// 맵 문자열에 "건물"이라는 표시가 따로 없어서, 벽으로 둘러싸인 모양에서 되찾아 낸다.
    /// </summary>
    public sealed class Building
    {
        public readonly int Id;

        /// <summary>경계 상자. 벽을 포함하고 양끝을 포함한다.</summary>
        public readonly int MinX;
        public readonly int MinY;
        public readonly int MaxX;
        public readonly int MaxY;

        /// <summary>벽·문·내부를 모두 포함한 칸들.</summary>
        public readonly GridPoint[] Cells;

        /// <summary>
        /// 밖으로 통하는 문. 실외 칸과 맞닿은 문만 들어간다.
        /// 지붕으로 건물을 덮을 때 이 칸만 비워야 어디로 들어갈지 보인다.
        /// </summary>
        public readonly GridPoint[] Entrances;

        public Building(int id, GridPoint[] cells, GridPoint[] entrances, int minX, int minY, int maxX, int maxY)
        {
            Id = id;
            Cells = cells;
            Entrances = entrances;
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public int Width
        {
            get { return MaxX - MinX + 1; }
        }

        public int Height
        {
            get { return MaxY - MinY + 1; }
        }

        /// <summary>이 건물에서 타고 있는 칸 수.</summary>
        public int BurningCells(FireGrid grid)
        {
            if (grid == null) return 0;

            int count = 0;
            for (int i = 0; i < Cells.Length; i++)
            {
                if (grid[Cells[i].X, Cells[i].Y].State == CellState.Burning) count++;
            }
            return count;
        }

        /// <summary>
        /// 가장 많이 타고 있는 화재 등급. 타는 칸이 없으면 <see cref="FireClass.None"/>.
        /// 지붕 위에 세울 불꽃 색을 여기서 고른다 — 한 건물에 여러 등급이 섞여도 하나로 알린다.
        /// </summary>
        public FireClass DominantFireClass(FireGrid grid)
        {
            if (grid == null) return FireClass.None;

            int a = 0;
            int b = 0;
            int c = 0;

            for (int i = 0; i < Cells.Length; i++)
            {
                ref Cell cell = ref grid[Cells[i].X, Cells[i].Y];
                if (cell.State != CellState.Burning) continue;

                switch (Materials.Of(cell.Material).Class)
                {
                    case FireClass.A: a++; break;
                    case FireClass.B: b++; break;
                    case FireClass.C: c++; break;
                }
            }

            if (a == 0 && b == 0 && c == 0) return FireClass.None;
            if (a >= b && a >= c) return FireClass.A;
            return b >= c ? FireClass.B : FireClass.C;
        }
    }

    /// <summary>맵 한 장의 실내·실외 구분과 건물 목록.</summary>
    public sealed class BuildingMap
    {
        /// <summary>어느 건물에도 속하지 않는다(실외이거나 맵 테두리이거나 격자 밖).</summary>
        public const int None = -1;

        private static readonly int[] Dx = { 0, 0, -1, 1 };
        private static readonly int[] Dy = { -1, 1, 0, 0 };

        public readonly Building[] All;

        private readonly int _width;
        private readonly int _height;
        private readonly int[] _cellBuilding;
        private readonly bool[] _outdoor;

        private BuildingMap(FireGrid grid, Building[] all, int[] cellBuilding, bool[] outdoor)
        {
            _width = grid.Width;
            _height = grid.Height;
            All = all;
            _cellBuilding = cellBuilding;
            _outdoor = outdoor;
        }

        /// <summary>소방관이 걸어서 닿을 수 있는 바깥 마당인지. 문 너머는 실외가 아니다.</summary>
        public bool IsOutdoor(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            return _outdoor[(y * _width) + x];
        }

        /// <summary>그 칸이 속한 건물 id. 실외·맵 테두리·격자 밖이면 <see cref="None"/>.</summary>
        public int At(int x, int y)
        {
            if (!InBounds(x, y)) return None;
            return _cellBuilding[(y * _width) + x];
        }

        /// <summary>그 칸이 속한 건물. 없으면 null.</summary>
        public Building Of(int x, int y)
        {
            int id = At(x, y);
            return id == None ? null : All[id];
        }

        /// <summary>
        /// 맵에서 건물을 되찾아 낸다.
        ///
        /// 1. 소방관 시작 지점에서 4방향으로 퍼져 실외를 구한다. 문에서 멈추므로 문 너머는 실외가 아니다.
        /// 2. 실외가 아닌 칸을 4방향으로 묶는다. 벽·문·실내 바닥이 한 덩어리가 된다.
        ///    내부 칸막이는 실내 바닥과 맞닿아 있어 저절로 그 건물에 흡수된다.
        /// 3. (0,0)이 든 덩어리는 맵 바깥 테두리이므로 건물로 세지 않는다.
        ///    6개 맵 모두 건물이 테두리에서 한 칸 이상 떨어져 있어 이 한 번으로 갈린다.
        /// 4. 안이 없는 덩어리도 건물이 아니다. 마당에 홀로 선 급수전은 통행 불가라
        ///    실외로 칠해지지 않지만, 지붕을 씌울 건물은 아니다.
        /// </summary>
        public static BuildingMap From(FireGrid grid, GridPoint spawn)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));

            bool[] outdoor = FloodOutdoor(grid, spawn);

            var cellBuilding = new int[grid.Count];
            for (int i = 0; i < cellBuilding.Length; i++) cellBuilding[i] = None;

            // 맵 테두리부터 지워 둔다. 건물이 아니지만 실외도 아니라서 따로 걸러야 한다.
            var taken = new bool[grid.Count];
            var group = new List<GridPoint>();
            FloodGroup(grid, outdoor, taken, new GridPoint(0, 0), group);

            var buildings = new List<Building>();

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    int i = grid.Index(x, y);
                    if (outdoor[i] || taken[i]) continue;

                    FloodGroup(grid, outdoor, taken, new GridPoint(x, y), group);
                    if (!HasInside(grid, group)) continue;

                    buildings.Add(MakeBuilding(grid, outdoor, group, buildings.Count, cellBuilding));
                }
            }

            return new BuildingMap(grid, buildings.ToArray(), cellBuilding, outdoor);
        }

        private bool InBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < _width && y < _height;
        }

        /// <summary>
        /// 시작 지점에서 걸어 닿는 바깥 마당. 문은 지나지 않는다 —
        /// 문을 열고 들어간 곳은 실내라고 봐야 지붕을 덮을 수 있다.
        /// </summary>
        private static bool[] FloodOutdoor(FireGrid grid, GridPoint spawn)
        {
            var outdoor = new bool[grid.Count];
            if (!grid.InBounds(spawn.X, spawn.Y)) return outdoor;

            var queue = new Queue<GridPoint>();
            outdoor[grid.Index(spawn.X, spawn.Y)] = true;
            queue.Enqueue(spawn);

            while (queue.Count > 0)
            {
                GridPoint p = queue.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    int nx = p.X + Dx[k];
                    int ny = p.Y + Dy[k];
                    if (!grid.InBounds(nx, ny)) continue;

                    int n = grid.Index(nx, ny);
                    if (outdoor[n]) continue;

                    var material = (MaterialId)grid.Cells[n].Material;
                    if (material == MaterialId.Door) continue;

                    // 장식물은 마당에 놓인 물건이지 건물이 아니다. 못 지나가도 실외로 치고 계속 퍼진다.
                    // 여기서 멈추면 건물 벽에 붙여 심은 나무가 그 건물 덩어리에 흡수되어
                    // 지붕이 나무 위에까지 덮인다 — 마당의 급수전이 건물로 잡혔던 것과 같은 함정이다.
                    if (material != MaterialId.Scenery && !Materials.Of(material).Walkable) continue;

                    outdoor[n] = true;
                    queue.Enqueue(new GridPoint(nx, ny));
                }
            }

            return outdoor;
        }

        /// <summary>실외가 아닌 칸 하나에서 시작해 맞닿은 덩어리를 전부 모은다.</summary>
        private static void FloodGroup(FireGrid grid, bool[] outdoor, bool[] taken, GridPoint start, List<GridPoint> group)
        {
            group.Clear();

            int first = grid.Index(start.X, start.Y);
            if (outdoor[first] || taken[first]) return;

            var queue = new Queue<GridPoint>();
            taken[first] = true;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                GridPoint p = queue.Dequeue();
                group.Add(p);

                for (int k = 0; k < 4; k++)
                {
                    int nx = p.X + Dx[k];
                    int ny = p.Y + Dy[k];
                    if (!grid.InBounds(nx, ny)) continue;

                    int n = grid.Index(nx, ny);
                    if (outdoor[n] || taken[n]) continue;

                    taken[n] = true;
                    queue.Enqueue(new GridPoint(nx, ny));
                }
            }
        }

        /// <summary>
        /// 이 덩어리에 들어갈 수 있는 안이 있는지. 문이 아닌 통행 가능한 칸이 하나라도 있어야 한다.
        /// 마당의 급수전이나 홀로 선 담장은 여기서 걸러진다 — 안이 없으면 건물이 아니다.
        /// </summary>
        private static bool HasInside(FireGrid grid, List<GridPoint> group)
        {
            for (int i = 0; i < group.Count; i++)
            {
                var material = (MaterialId)grid[group[i].X, group[i].Y].Material;
                if (material != MaterialId.Door && Materials.Of(material).Walkable) return true;
            }
            return false;
        }

        private static Building MakeBuilding(
            FireGrid grid,
            bool[] outdoor,
            List<GridPoint> group,
            int id,
            int[] cellBuilding)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            var cells = new GridPoint[group.Count];
            var entrances = new List<GridPoint>();

            for (int i = 0; i < group.Count; i++)
            {
                GridPoint p = group[i];
                cells[i] = p;
                cellBuilding[grid.Index(p.X, p.Y)] = id;

                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;

                if ((MaterialId)grid[p.X, p.Y].Material == MaterialId.Door && TouchesOutdoor(grid, outdoor, p))
                {
                    entrances.Add(p);
                }
            }

            return new Building(id, cells, entrances.ToArray(), minX, minY, maxX, maxY);
        }

        private static bool TouchesOutdoor(FireGrid grid, bool[] outdoor, GridPoint p)
        {
            for (int k = 0; k < 4; k++)
            {
                int nx = p.X + Dx[k];
                int ny = p.Y + Dy[k];
                if (!grid.InBounds(nx, ny)) continue;
                if (outdoor[grid.Index(nx, ny)]) return true;
            }
            return false;
        }
    }
}
