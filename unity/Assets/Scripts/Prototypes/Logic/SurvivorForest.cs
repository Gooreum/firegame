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
        /// <summary>개울(2026-10-12 숲 3D 지형, docs §26): 동서로 가로지르는 줄의 가운데 y·반폭, 다리 반폭. 숲 첫 줄(35.5)과 둘째 줄(46) 사이.</summary>
        public const float CreekY = 40.5f, CreekHalf = 1.5f, BridgeHalf = 1.5f;

        /// <summary>다리 가운데 x: 십자 흙길(30)과 동쪽(45).</summary>
        public static readonly float[] BridgeXs = { 30f, 45f };

        /// <summary>북서 바위 언덕(높이 2칸 절벽, 못 올라간다).</summary>
        public const float RockX0 = 0f, RockX1 = 14f, RockY0 = 51f, RockY1 = 60f;

        /// <summary>
        /// 언덕(2026-10-12 "언덕에도 건물이 있고 올라갈 수 있어야"): 평평한 꼭대기 x0, x1, y0, y1 · 높이 · 비탈 폭. 비탈을 걸어 오른다.
        /// 첫 줄은 북서 바위 언덕(비탈 0.5 = 절벽, 못 올라간다). 전망대·산장 능선, 숲 식당 언덕, 숲 속 언덕 둘(나무), 남동 둔덕,
        /// 그리고 맵 밖 네 산등성이(가장자리가 솟는다). 뷰(ForestTerrain)와 규칙(Height)이 같은 표를 쓴다.
        /// </summary>
        public static readonly float[] Hills =
        {
            RockX0, RockX1, RockY0, RockY1, 3.2f, 0.6f,
            8f, 16f, 13f, 31f, 2.8f, 2.8f,
            44f, 52f, 12f, 20f, 2.4f, 2.6f,
            16f, 24f, 48f, 55f, 3.2f, 3f,
            36f, 44f, 49f, 55f, 2.6f, 2.8f,
            50f, 56f, 3f, 8f, 2f, 2.5f,
            -20f, -1f, -20f, 80f, 5f, 4f,
            61f, 80f, -20f, 80f, 5f, 4f,
            -20f, 80f, -20f, -1f, 5f, 4f,
            -20f, 80f, 61f, 80f, 5f, 4f,
        };

        /// <summary>개울 바닥 깊이(칸). 다리 자리는 0(다리 위를 걷는다).</summary>
        public const float CreekDepth = 0.9f;

        /// <summary>땅 높이(칸, 위가 +). 언덕은 꼭대기에서 비탈 폭만큼 부드럽게(smoothstep) 내려오고, 개울은 파인다.</summary>
        public static float Height(float x, float y)
        {
            float h = 0f;
            for (int i = 0; i < Hills.Length; i += 6)
            {
                float dx = Math.Max(Math.Max(Hills[i] - x, x - Hills[i + 1]), 0f);
                float dy = Math.Max(Math.Max(Hills[i + 2] - y, y - Hills[i + 3]), 0f);
                float t = 1f - ((float)Math.Sqrt((dx * dx) + (dy * dy)) / Hills[i + 5]);
                if (t <= 0f) continue;
                h = Math.Max(h, Hills[i + 4] * t * t * (3f - (2f * t)));
            }
            float c = Math.Abs(y - CreekY);
            if (c < CreekHalf + 0.4f)
            {
                foreach (float bx in BridgeXs)
                {
                    if (Math.Abs(x - bx) < BridgeHalf) return h;
                }
                float t = Math.Min(1f, (CreekHalf + 0.4f - c) / 0.8f);
                h -= CreekDepth * t * t * (3f - (2f * t));
            }
            return h;
        }

        private static readonly float[] Offsets =
        {
            0f, 0f, 1.8f, 0.6f, -1.6f, 1.2f, 0.7f, -1.8f, -1.3f, -1.4f, 2.2f, -1.6f, -2.4f, -0.2f,
        };

        public static List<Structure> Build()
        {
            var list = new List<Structure>();

            // 캠프 두 줄(남쪽). 첫 줄(y=28)은 숲 바람이 닿고, 둘째 줄(y=16)과 제재소는 불씨·다람쥐로만 위협받는다.
            // 주민 17: 마을(18)만큼 구할 사람이 있어야 뒤 스테이지도 할 일이 온다(재미 밀도 밴드).
            House(list, "산장", 12f, 28f, 3);
            House(list, "캠핑 매점", 21f, 28f, 2);
            House(list, "관리사무소", 39f, 28f, 3);
            House(list, "통나무 카페", 48f, 28f, 2);
            House(list, "전망대", 12f, 16f, 1);
            House(list, "목공소", 22f, 16f, 3);
            House(list, "야영 관리동", 38f, 16f, 2);
            House(list, "숲 식당", 48f, 16f, 1);
            list.Add(new Structure { Kind = StructureKind.Depot, Name = "제재소", Pos = new Vec2(30f, 8f), Half = new Vec2(3.5f, 2.5f) });

            Add(list, StructureKind.Gas, "캠핑 가스통", 16.5f, 28f, 0.4f, 0.4f);
            Add(list, StructureKind.Gas, "캠핑 가스통", 43.5f, 28f, 0.4f, 0.4f);
            Add(list, StructureKind.Gas, "캠핑 가스통", 27f, 16f, 0.4f, 0.4f);
            Add(list, StructureKind.Car, "캠핑카", 26f, 23f, 1f, 0.55f);
            Add(list, StructureKind.Car, "캠핑카", 34f, 23f, 1f, 0.55f);

            // 개울: 다리 자리만 비우고 세 토막. 땅 몹·소방관은 못 건너고(다리로 돈다) 산불도 못 넘는다.
            float x0 = 0f;
            foreach (float bx in BridgeXs)
            {
                Creek(list, x0, bx - BridgeHalf);
                x0 = bx + BridgeHalf;
            }
            Creek(list, x0, SurvivorSim.ArenaSize);
            // 바위 언덕만 절벽(못 올라간다). 나머지 언덕은 걸어 오르는 땅이다(Height).
            list.Add(new Structure
            {
                Kind = StructureKind.Water,
                Name = "바위 언덕",
                Cliff = true,
                Pos = new Vec2((RockX0 + RockX1) / 2f, (RockY0 + RockY1) / 2f),
                Half = new Vec2((RockX1 - RockX0) / 2f, (RockY1 - RockY0) / 2f),
            });

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
                    if (list.Exists(s => s.Kind == StructureKind.Water && s.Within(at, 0.8f))) continue;
                    if (list.Exists(s => s.Kind != StructureKind.Tree && s.Kind != StructureKind.Water && s.Within(at, 1.4f))) continue;
                    if (list.Exists(s => s.Kind == StructureKind.Tree && s.Pos.DistanceTo(at) < 1.2f)) continue;
                    Add(list, StructureKind.Tree, "나무", at.X, at.Y, 0.6f, 0.6f);
                }
            }
            return list;
        }

        /// <summary>다리 끝 둘(남·북 둑). 땅 몹·봇 길찾기(SurvivorSim.RouteAround)용.</summary>
        public static List<Bridge> Bridges()
        {
            var list = new List<Bridge>();
            foreach (float bx in BridgeXs) list.Add(new Bridge { A = new Vec2(bx, CreekY - CreekHalf - 0.8f), B = new Vec2(bx, CreekY + CreekHalf + 0.8f) });
            return list;
        }

        private static void Creek(List<Structure> list, float xa, float xb)
        {
            list.Add(new Structure { Kind = StructureKind.Water, Name = "개울", Pos = new Vec2((xa + xb) / 2f, CreekY), Half = new Vec2((xb - xa) / 2f, CreekHalf) });
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

    /// <summary>다리: 둑 끝 둘(A 남, B 북).</summary>
    public struct Bridge
    {
        public Vec2 A;
        public Vec2 B;
    }
}
