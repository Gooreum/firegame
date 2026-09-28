using System;
using System.Collections.Generic;

namespace FireGame.Prototypes.Logic
{
    /// <summary>
    /// 2스테이지 산불 숲. 시드와 상관없이 늘 같은 자리다.
    /// 가운데(소방관 출발점) 반경 7칸과 십자 흙길은 비우고, 나무는 무더기로 모아 심는다.
    /// </summary>
    public static class SurvivorForest
    {
        /// <summary>십자 흙길의 반폭. 가운데 x=30, y=30 줄.</summary>
        public const float PathHalf = 1.6f;

        /// <summary>나무 무더기 중심들.</summary>
        private static readonly float[] Clumps =
        {
            22f, 38f, 38f, 38f, 22f, 22f, 38f, 22f,
            7f, 7f, 53f, 7f, 7f, 53f, 53f, 53f,
            22f, 53f, 38f, 53f, 21f, 6f, 39f, 6f,
            6f, 23f, 54f, 38f,
        };

        /// <summary>무더기 안에서 나무 하나하나의 자리(중심에서 떨어진 칸).</summary>
        private static readonly float[] Offsets =
        {
            0f, 0f, 1.8f, 0.6f, -1.6f, 1.2f, 0.7f, -1.8f, -1.3f, -1.4f, 2.2f, -1.6f, -2.4f, -0.2f,
        };

        public static List<Structure> Build()
        {
            var list = new List<Structure>();

            House(list, "산장", 15f, 44f, 2);
            House(list, "캠핑 매점", 30f, 46f, 1);
            House(list, "관리사무소", 45f, 44f, 2);
            House(list, "통나무 카페", 47f, 30f, 1);
            House(list, "전망대", 44f, 16f, 1);
            House(list, "목공소", 15f, 16f, 2);
            list.Add(new Structure { Kind = StructureKind.Depot, Name = "제재소", Pos = new Vec2(30f, 12f), Half = new Vec2(3.5f, 2.5f) });

            Add(list, StructureKind.Gas, "캠핑 가스통", 25f, 42f, 0.4f, 0.4f);
            Add(list, StructureKind.Gas, "캠핑 가스통", 36f, 17f, 0.4f, 0.4f);
            Add(list, StructureKind.Gas, "캠핑 가스통", 12f, 34f, 0.4f, 0.4f);
            Add(list, StructureKind.Car, "캠핑카", 13f, 29f, 1f, 0.55f);
            Add(list, StructureKind.Car, "캠핑카", 50f, 37f, 1f, 0.55f);

            var mid = new Vec2(SurvivorSim.ArenaSize / 2f, SurvivorSim.ArenaSize / 2f);
            for (int c = 0; c < Clumps.Length; c += 2)
            {
                for (int o = 0; o < Offsets.Length; o += 2)
                {
                    var at = new Vec2(Clumps[c] + Offsets[o], Clumps[c + 1] + Offsets[o + 1]);
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
