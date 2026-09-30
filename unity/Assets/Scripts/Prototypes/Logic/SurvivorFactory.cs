using System.Collections.Generic;

namespace FireGame.Prototypes.Logic
{
    /// <summary>
    /// 3스테이지 공단. 시드와 상관없이 늘 같은 자리다.
    /// 공장은 마을 가게와 같은 고리(중심에서 13~16칸)에 두어 걷는 거리와 번짐 거리를 마을과 맞춘다.
    /// 약품 드럼은 셋씩 붙여 두어 하나가 터지면 무더기가 연쇄로 터진다.
    /// </summary>
    public static class SurvivorFactory
    {
        /// <summary>드럼 무더기 안 드럼 사이(가스 폭발 반경 안).</summary>
        public const float DrumGap = 1.2f;

        public static List<Structure> Build()
        {
            var list = new List<Structure>();

            House(list, "인쇄소", 19f, 41f, 2);
            House(list, "페인트 공장", 30f, 42f, 1);
            House(list, "정비소", 41f, 41f, 1);
            House(list, "도금 공장", 43f, 30f, 2);
            House(list, "식품 공장", 41f, 19f, 1);
            House(list, "섬유 공장", 30f, 18f, 2);
            House(list, "부품 공장", 19f, 19f, 1);
            House(list, "포장 공장", 17f, 30f, 1);
            list.Add(new Structure { Kind = StructureKind.Depot, Name = "정유 저장소", Pos = new Vec2(30f, 52f), Half = new Vec2(3.5f, 2.5f) });

            Drums(list, 24.5f, 41.5f);
            Drums(list, 35.5f, 18.5f);
            Drums(list, 46.5f, 34f);
            Drums(list, 13.5f, 25f);

            float[] trees = { 12f, 46f, 48f, 46f, 48f, 12f, 12f, 14f };
            for (int i = 0; i < trees.Length; i += 2) Add(list, StructureKind.Tree, "나무", trees[i], trees[i + 1], 0.6f, 0.6f);

            // 컨테이너·트럭(차 규칙): 길가에 세워 둔다.
            float[] cars = { 13f, 37f, 47f, 24f, 23f, 24f, 37f, 36f };
            for (int i = 0; i < cars.Length; i += 2) Add(list, StructureKind.Car, "컨테이너", cars[i], cars[i + 1], 1f, 0.55f);

            return list;
        }

        private static void Drums(List<Structure> list, float x, float y)
        {
            for (int i = 0; i < 3; i++) Add(list, StructureKind.Gas, "약품 드럼", x + ((i - 1) * DrumGap), y, 0.4f, 0.4f);
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
