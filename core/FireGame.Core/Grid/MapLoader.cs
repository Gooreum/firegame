using System;
using System.Collections.Generic;

namespace FireGame.Core.Grid
{
    /// <summary>맵 문자열을 파싱한 결과. 격자와 배치 마커 좌표를 함께 담는다.</summary>
    public sealed class ParsedMap
    {
        public FireGrid Grid;

        /// <summary>플레이어 시작 위치. 맵에 `@`가 없으면 (0,0).</summary>
        public GridPoint PlayerSpawn;

        public readonly List<GridPoint> Civilians = new List<GridPoint>();
        public readonly List<GridPoint> Exits = new List<GridPoint>();
        public readonly List<GridPoint> Hydrants = new List<GridPoint>();

        /// <summary>맵에 `*`로 표시된 초기 발화점.</summary>
        public readonly List<GridPoint> IgnitionPoints = new List<GridPoint>();
    }

    /// <summary>
    /// 문자열 배열로 정의된 레벨을 격자로 변환한다.
    /// 맵을 소스 코드에 두면 별도 에디터 툴 없이 레벨을 읽고 고칠 수 있고,
    /// 도스풍 타일 그래픽과도 표현이 1:1로 맞는다.
    /// </summary>
    public static class MapLoader
    {
        /// <summary>
        /// 맵 문자 정의:
        /// <c>.</c> 바닥, <c>#</c> 콘크리트, <c>W</c> 목재, <c>~</c> 유류,
        /// <c>E</c> 전기, <c>H</c> 급수전, <c>D</c> 문, <c>X</c> 출구,
        /// <c>@</c> 플레이어, <c>!</c> 시민, <c>*</c> 초기 발화점.
        /// </summary>
        /// <param name="rows">위에서 아래 순서의 맵 행. 모든 행의 길이가 같아야 한다.</param>
        public static ParsedMap Parse(string[] rows)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows), "맵 데이터가 null입니다.");
            }

            if (rows.Length == 0)
            {
                throw new ArgumentException("맵에 행이 하나도 없습니다.", nameof(rows));
            }

            int width = rows[0] == null ? 0 : rows[0].Length;
            if (width == 0)
            {
                throw new ArgumentException("맵의 첫 행이 비어 있습니다.", nameof(rows));
            }

            // 길이가 다른 행이 하나라도 있으면 격자 인덱싱이 조용히 어긋나므로
            // 파싱 시점에 행 번호를 밝혀서 즉시 실패시킨다.
            for (int y = 0; y < rows.Length; y++)
            {
                if (rows[y] == null)
                {
                    throw new ArgumentException("맵 " + y + "행이 null입니다.", nameof(rows));
                }

                if (rows[y].Length != width)
                {
                    throw new ArgumentException(
                        "맵 " + y + "행의 길이가 " + rows[y].Length + "로, 첫 행의 " + width + "과 다릅니다.",
                        nameof(rows));
                }
            }

            var result = new ParsedMap { Grid = new FireGrid(width, rows.Length) };

            for (int y = 0; y < rows.Length; y++)
            {
                string row = rows[y];

                for (int x = 0; x < width; x++)
                {
                    char c = row[x];

                    if (!Materials.TryFromMapChar(c, out MaterialId material))
                    {
                        throw new ArgumentException(
                            "맵 " + y + "행 " + x + "열에 알 수 없는 문자 '" + c + "' 가 있습니다.",
                            nameof(rows));
                    }

                    ref Cell cell = ref result.Grid[x, y];
                    cell.Material = (byte)material;
                    cell.Fuel = 1f;
                    cell.Heat = 0f;
                    cell.Wet = 0f;
                    cell.State = CellState.Intact;

                    var point = new GridPoint(x, y);

                    switch (c)
                    {
                        case '@':
                            result.PlayerSpawn = point;
                            break;
                        case '!':
                            result.Civilians.Add(point);
                            break;
                        case 'X':
                            result.Exits.Add(point);
                            break;
                        case 'H':
                            result.Hydrants.Add(point);
                            break;
                        case '*':
                            cell.State = CellState.Burning;
                            result.IgnitionPoints.Add(point);
                            break;
                    }
                }
            }

            return result;
        }
    }
}
