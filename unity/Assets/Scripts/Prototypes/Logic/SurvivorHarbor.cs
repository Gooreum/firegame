using System.Collections.Generic;

namespace FireGame.Prototypes.Logic
{
    /// <summary>
    /// 4스테이지 항구. 시드와 상관없이 늘 같은 자리다.
    /// 북쪽(y ≥ SeaFrom)은 바다. 부두 둘(x 13~17, 43~47)이 y PierTip까지 바다로 뻗어 걸을 수 있고, 그 너머는 다시 바다다.
    /// 불배는 부두 끝 너머 BoatLane 줄을 따라 떠내려와 노린 부둣가 건물의 x에서 남쪽으로 꺾어 부두선에 닿는다.
    /// 부둣가 줄(y=36)은 지붕 윗변 37.5라 부두선 40과 틈 2.5: 닿은 배가 불을 옮긴다. 연료 탱크는 부두선 바로 안쪽이다.
    /// 가운데(소방관 출발점) 반경 6칸은 비워 둔다.
    /// </summary>
    public static class SurvivorHarbor
    {
        /// <summary>이 줄부터 북쪽이 바다다.</summary>
        public const float SeaFrom = 40f;

        /// <summary>부두 반폭.</summary>
        public const float PierHalf = 2f;

        /// <summary>부두 끝(여기까지 걸을 수 있다).</summary>
        public const float PierTip = 48f;

        /// <summary>부두 가운데 x 둘.</summary>
        public static readonly float[] PierX = { 15f, 45f };

        /// <summary>불배가 떠내려오는 줄(부두 끝 너머).</summary>
        public const float BoatLane = 52f;

        /// <summary>부둣가 줄 y(이 위 건물이 배의 표적이다).</summary>
        public const float QuayRow = 36f;

        /// <summary>소방정 물줄기가 남쪽으로 닿는 길이(그림용: 바다 줄 50에서 부둣가 지붕 37.5까지).</summary>
        public const float FireboatSpray = 13f;

        public static List<Structure> Build()
        {
            var list = new List<Structure>();

            // 바다: 부두 자리만 비운 사각형 다섯(중심·반폭). 부두 끝 너머(y 48~60)는 부두 x에도 바다.
            Water(list, 6.5f, 50f, 6.5f, 10f);
            Water(list, 30f, 50f, 13f, 10f);
            Water(list, 53.5f, 50f, 6.5f, 10f);
            Water(list, PierX[0], 54f, PierHalf, 6f);
            Water(list, PierX[1], 54f, PierHalf, 6f);

            // 부둣가 줄(배가 노린다). 주민 18: 마을만큼 구할 사람이 있어야 뒤 스테이지도 할 일이 온다(재미 밀도 밴드).
            House(list, "어시장", 8f, QuayRow, 3);
            House(list, "선구점", 22f, QuayRow, 2);
            House(list, "수산 창고", 38f, QuayRow, 2);
            House(list, "횟집", 52f, QuayRow, 3);
            // 둘째 줄: 불씨·갈매기·번짐으로만.
            House(list, "선원 숙소", 10f, 24f, 3);
            House(list, "통조림 공장", 20f, 24f, 2);
            House(list, "어구 창고", 40f, 24f, 2);
            House(list, "등대 카페", 50f, 24f, 1);
            list.Add(new Structure { Kind = StructureKind.Depot, Name = "수산 냉동창고", Pos = new Vec2(30f, 10f), Half = new Vec2(3.5f, 2.5f) });

            // 연료 탱크: 부두선 바로 안쪽(배가 닿으면 터진다)과 창고 곁.
            Add(list, StructureKind.Gas, "연료 탱크", 11.5f, 38.6f, 0.4f, 0.4f);
            Add(list, StructureKind.Gas, "연료 탱크", 48.5f, 38.6f, 0.4f, 0.4f);
            Add(list, StructureKind.Gas, "연료 탱크", 26f, 10f, 0.4f, 0.4f);

            // 컨테이너(차 규칙): 길에 세워 둔 장애물.
            Add(list, StructureKind.Car, "컨테이너", 18f, 30f, 1f, 0.55f);
            Add(list, StructureKind.Car, "컨테이너", 42f, 30f, 1f, 0.55f);

            float[] trees = { 4f, 20f, 56f, 20f, 6f, 30f, 54f, 30f };
            for (int i = 0; i < trees.Length; i += 2) Add(list, StructureKind.Tree, "나무", trees[i], trees[i + 1], 0.6f, 0.6f);

            return list;
        }

        private static void Water(List<Structure> list, float x, float y, float hx, float hy)
        {
            list.Add(new Structure { Kind = StructureKind.Water, Name = "바다", Pos = new Vec2(x, y), Half = new Vec2(hx, hy) });
        }

        private static void House(List<Structure> list, string name, float x, float y, int residents)
        {
            list.Add(new Structure { Kind = StructureKind.House, Name = name, Pos = new Vec2(x, y), Half = new Vec2(2f, 1.5f), Residents = residents });
        }

        private static void Add(List<Structure> list, StructureKind kind, string name, float x, float y, float hx, float hy)
        {
            list.Add(new Structure { Kind = kind, Name = name, Pos = new Vec2(x, y), Half = new Vec2(hx, hy) });
        }
    }
}
