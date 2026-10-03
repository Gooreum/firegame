using System.Collections.Generic;

namespace FireGame.Prototypes.Logic
{
    /// <summary>
    /// 5스테이지 야시장(밤). 시드와 상관없이 늘 같은 자리다.
    /// 점포 열둘이 두 줄(y=38·22)로 다닥다닥 서 있고, 같은 줄 이웃과 줄 양 끝이 등줄로 이어진다(Links):
    /// 불은 점포끼리 직접 번지지 않고 등줄을 타고 건너간다. 불꽃 가판대 셋은 불이 붙으면 하늘로 로켓을 쏜다.
    /// 가운데(소방관 출발점) 반경 6칸은 비워 두고, 남쪽에 공연 무대(창고)가 있다.
    /// </summary>
    public static class SurvivorMarket
    {
        /// <summary>점포 반폭·반높이(집보다 작다).</summary>
        public static readonly Vec2 StallHalf = new Vec2(1.6f, 1.2f);

        public const float RowNorth = 38f;
        public const float RowSouth = 22f;

        /// <summary>점포 x 자리(가운데 반경 6은 비운다).</summary>
        public static readonly float[] StallX = { 8f, 15f, 22f, 38f, 45f, 52f };

        private static readonly string[] NorthNames = { "호떡집", "떡볶이", "전집", "어묵 바", "과일 노점", "기념품" };
        private static readonly int[] NorthResidents = { 2, 1, 2, 1, 1, 2 };
        private static readonly string[] SouthNames = { "꼬치구이", "분식집", "솜사탕", "수공예", "잡화", "음료 노점" };
        private static readonly int[] SouthResidents = { 2, 1, 1, 2, 2, 1 };

        public static List<Structure> Build()
        {
            var list = new List<Structure>();

            // 점포 12(주민 18). 북쪽 줄 0~5, 남쪽 줄 6~11: Links가 이 순서를 믿는다.
            for (int i = 0; i < StallX.Length; i++) Stall(list, NorthNames[i], StallX[i], RowNorth, NorthResidents[i]);
            for (int i = 0; i < StallX.Length; i++) Stall(list, SouthNames[i], StallX[i], RowSouth, SouthResidents[i]);
            list.Add(new Structure { Kind = StructureKind.Depot, Name = "공연 무대", Pos = new Vec2(30f, 8f), Half = new Vec2(3.5f, 2.5f) });

            // 불꽃 가판대: 가운데 골목 양쪽과 북쪽 끝.
            Add(list, StructureKind.Fireworks, "불꽃 가판대", 18f, 30f, 0.9f, 0.7f);
            Add(list, StructureKind.Fireworks, "불꽃 가판대", 42f, 30f, 0.9f, 0.7f);
            Add(list, StructureKind.Fireworks, "불꽃 가판대", 30f, 50f, 0.9f, 0.7f);

            Add(list, StructureKind.Gas, "부탄가스", 11.5f, RowNorth, 0.4f, 0.4f);
            Add(list, StructureKind.Gas, "부탄가스", 48.5f, RowSouth, 0.4f, 0.4f);
            Add(list, StructureKind.Gas, "부탄가스", 34f, 50f, 0.4f, 0.4f);

            Add(list, StructureKind.Car, "푸드트럭", 30f, 43f, 1f, 0.55f);
            Add(list, StructureKind.Car, "푸드트럭", 30f, 17f, 1f, 0.55f);

            return list;
        }

        /// <summary>등줄: 같은 줄 이웃 점포끼리(5+5)와 줄 양 끝 위아래(2) = 12쌍. 구조물 인덱스 쌍.</summary>
        public static List<int[]> Links(List<Structure> list)
        {
            var links = new List<int[]>();
            int n = StallX.Length;
            for (int i = 0; i + 1 < n; i++)
            {
                links.Add(new[] { i, i + 1 });
                links.Add(new[] { n + i, n + i + 1 });
            }
            links.Add(new[] { 0, n });
            links.Add(new[] { n - 1, (2 * n) - 1 });
            return links;
        }

        private static void Stall(List<Structure> list, string name, float x, float y, int residents)
        {
            list.Add(new Structure { Kind = StructureKind.House, Name = name, Pos = new Vec2(x, y), Half = StallHalf, Residents = residents });
        }

        private static void Add(List<Structure> list, StructureKind kind, string name, float x, float y, float hx, float hy)
        {
            list.Add(new Structure { Kind = kind, Name = name, Pos = new Vec2(x, y), Half = new Vec2(hx, hy) });
        }
    }
}
