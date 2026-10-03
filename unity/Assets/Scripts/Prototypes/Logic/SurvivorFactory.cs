using System.Collections.Generic;

namespace FireGame.Prototypes.Logic
{
    /// <summary>
    /// 3스테이지 공단. 시드와 상관없이 늘 같은 자리다.
    /// 공장 두 줄(y=38·22)이 가운데 골목(y 25.5~34.5, 폭 9)을 마주 보고, 공장 앞에 약품 드럼이 넷씩 줄지어 있다:
    /// 드럼 하나가 터지면 줄이 연쇄로 터지고(간격 1.2 < 폭발 반경 3.5) 바로 앞 공장(틈 1.6)에 불이 붙으며 골목에 기름이 깔린다.
    /// 가운데(소방관 출발점) 반경 6칸은 비워 둔다.
    /// </summary>
    public static class SurvivorFactory
    {
        /// <summary>드럼 줄 안 드럼 사이(가스 폭발 반경 안).</summary>
        public const float DrumGap = 1.2f;

        /// <summary>줄 하나의 드럼 수.</summary>
        public const int DrumsPerLine = 4;

        /// <summary>골목 반폭(y=30 기준).</summary>
        public const float AlleyHalf = 4.5f;

        public static List<Structure> Build()
        {
            var list = new List<Structure>();

            // 북쪽 줄. 주민 18: 마을만큼 구할 사람이 있어야 뒤 스테이지도 할 일이 온다(재미 밀도 밴드).
            House(list, "인쇄소", 10f, 38f, 3);
            House(list, "페인트 공장", 18f, 38f, 2);
            House(list, "정비소", 42f, 38f, 2);
            House(list, "도금 공장", 50f, 38f, 3);
            // 남쪽 줄.
            House(list, "식품 공장", 10f, 22f, 2);
            House(list, "섬유 공장", 18f, 22f, 3);
            House(list, "부품 공장", 42f, 22f, 2);
            House(list, "포장 공장", 50f, 22f, 1);
            list.Add(new Structure { Kind = StructureKind.Depot, Name = "정유 저장소", Pos = new Vec2(30f, 52f), Half = new Vec2(3.5f, 2.5f) });

            // 드럼 줄: 공장 앞 골목 가장자리.
            Drums(list, 10f, 34.5f);
            Drums(list, 50f, 34.5f);
            Drums(list, 18f, 25.5f);
            Drums(list, 42f, 25.5f);

            float[] trees = { 12f, 46f, 48f, 46f, 48f, 12f, 12f, 14f };
            for (int i = 0; i < trees.Length; i += 2) Add(list, StructureKind.Tree, "나무", trees[i], trees[i + 1], 0.6f, 0.6f);

            // 컨테이너(차 규칙): 골목 안에 세워 둔 장애물.
            float[] cars = { 14f, 30f, 46f, 30f };
            for (int i = 0; i < cars.Length; i += 2) Add(list, StructureKind.Car, "컨테이너", cars[i], cars[i + 1], 1f, 0.55f);

            return list;
        }

        private static void Drums(List<Structure> list, float x, float y)
        {
            for (int i = 0; i < DrumsPerLine; i++) Add(list, StructureKind.Gas, "약품 드럼", x + ((i - ((DrumsPerLine - 1) / 2f)) * DrumGap), y, 0.4f, 0.4f);
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
