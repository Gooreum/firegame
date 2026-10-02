using System;
using System.Collections.Generic;

namespace FireGame.Prototypes.Logic
{
    /// <summary>
    /// 2스테이지 산불 숲. 시드와 상관없이 늘 같은 자리다.
    /// 숲은 북쪽(y ≥ 33), 캠프는 남쪽 두 줄(y=28·16). 바람은 늘 숲에서 캠프 쪽으로 분다(StageRules.WindArc):
    /// 첫 줄 지붕과 첫 나무 줄 틈이 바람 사거리(4) 안이라 산불이 캠프 첫 줄로 넘어온다. 둘째 줄은 불씨·다람쥐로만.
    /// 가운데(소방관 출발점) 반경 7칸과 십자 흙길은 비우고, 나무는 무더기로 모아 심는다.
    /// </summary>
    public static class SurvivorForest
    {
        /// <summary>십자 흙길의 반폭. 가운데 x=30, y=30 줄.</summary>
        public const float PathHalf = 1.6f;

        /// <summary>이 줄부터 북쪽이 숲이다(나무는 모두 이 위).</summary>
        public const float ForestFrom = 33f;

        /// <summary>나무 무더기 중심들(모두 북쪽). 첫 줄 y=35.5는 가장 낮은 나무가 33.7에 오게: 캠프 첫 줄(지붕 29.5)과 틈 3.6.</summary>
        private static readonly float[] Clumps =
        {
            8f, 35.5f, 16f, 35.5f, 24f, 35.5f, 36f, 35.5f, 44f, 35.5f, 52f, 35.5f,
            6f, 46f, 14f, 47f, 22f, 46f, 38f, 46f, 46f, 47f, 54f, 46f,
            10f, 56f, 20f, 56f, 30f, 45f, 30f, 55f, 40f, 56f, 50f, 56f,
        };

        /// <summary>무더기 안에서 나무 하나하나의 자리(중심에서 떨어진 칸).</summary>
        private static readonly float[] Offsets =
        {
            0f, 0f, 1.8f, 0.6f, -1.6f, 1.2f, 0.7f, -1.8f, -1.3f, -1.4f, 2.2f, -1.6f, -2.4f, -0.2f,
        };

        public static List<Structure> Build()
        {
            var list = new List<Structure>();

            // 캠프 두 줄(남쪽). 첫 줄(y=28)은 숲 바람이 닿고, 둘째 줄(y=16)과 제재소는 불씨·다람쥐로만 위협받는다.
            House(list, "산장", 12f, 28f, 2);
            House(list, "캠핑 매점", 21f, 28f, 1);
            House(list, "관리사무소", 39f, 28f, 2);
            House(list, "통나무 카페", 48f, 28f, 1);
            House(list, "전망대", 12f, 16f, 1);
            House(list, "목공소", 22f, 16f, 2);
            House(list, "야영 관리동", 38f, 16f, 1);
            House(list, "숲 식당", 48f, 16f, 1);
            list.Add(new Structure { Kind = StructureKind.Depot, Name = "제재소", Pos = new Vec2(30f, 8f), Half = new Vec2(3.5f, 2.5f) });

            Add(list, StructureKind.Gas, "캠핑 가스통", 16.5f, 28f, 0.4f, 0.4f);
            Add(list, StructureKind.Gas, "캠핑 가스통", 43.5f, 28f, 0.4f, 0.4f);
            Add(list, StructureKind.Gas, "캠핑 가스통", 27f, 16f, 0.4f, 0.4f);
            Add(list, StructureKind.Car, "캠핑카", 26f, 23f, 1f, 0.55f);
            Add(list, StructureKind.Car, "캠핑카", 34f, 23f, 1f, 0.55f);

            var mid = new Vec2(SurvivorSim.ArenaSize / 2f, SurvivorSim.ArenaSize / 2f);
            for (int c = 0; c < Clumps.Length; c += 2)
            {
                for (int o = 0; o < Offsets.Length; o += 2)
                {
                    var at = new Vec2(Clumps[c] + Offsets[o], Clumps[c + 1] + Offsets[o + 1]);
                    if (at.Y < ForestFrom) continue;
                    if (at.DistanceTo(mid) < 7.5f) continue;
                    if (Math.Abs(at.X - mid.X) < PathHalf + 0.7f || Math.Abs(at.Y - mid.Y) < PathHalf + 0.7f) continue;
                    if (at.X < 1f || at.Y < 1f || at.X > SurvivorSim.ArenaSize - 1f || at.Y > SurvivorSim.ArenaSize - 1f) continue;
                    if (list.Exists(s => s.Kind != StructureKind.Tree && s.Within(at, 1.4f))) continue;
                    if (list.Exists(s => s.Kind == StructureKind.Tree && s.Pos.DistanceTo(at) < 1.2f)) continue;
                    Add(list, StructureKind.Tree, "나무", at.X, at.Y, 0.6f, 0.6f);
                }
            }
            return list;
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
