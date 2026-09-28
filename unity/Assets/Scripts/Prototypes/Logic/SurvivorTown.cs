using System;
using System.Collections.Generic;

namespace FireGame.Prototypes.Logic
{
    public enum StructureKind
    {
        House,
        Depot,
        Tree,
        Car,
        Gas,
    }

    /// <summary>동네에 놓인 탈 것 하나. 판정은 축 정렬 사각형이다.</summary>
    public sealed class Structure
    {
        public StructureKind Kind;

        /// <summary>알림에 쓰는 이름("빵집").</summary>
        public string Name;
        public Vec2 Pos;

        /// <summary>반 폭·반 높이.</summary>
        public Vec2 Half;

        /// <summary>0이면 안 탄다. 0~1 불 세기.</summary>
        public float Fire;

        /// <summary>1에서 줄어 0이 되면 무너진다.</summary>
        public float Integrity = 1f;

        /// <summary>0보다 크면 젖어서 불이 붙지 않는다(초).</summary>
        public float Wet;

        /// <summary>가스통: 불이 붙은 뒤 터지기까지 남은 시간. 음수면 퓨즈가 안 돈다.</summary>
        public float Fuse = -1f;

        /// <summary>안에 있는 주민. 불이 나면 갇힌다.</summary>
        public int Residents;

        /// <summary>갇힌 사람이 큰 불 연기를 마신 시간. 쌓이면 한 명씩 잃는다.</summary>
        public float Smoke;

        /// <summary>소방관이 문 앞에 서 있던 시간.</summary>
        public float RescueHold;

        /// <summary>구조 드론이 지붕 위에 머문 시간(2초마다 한 명).</summary>
        public float DroneRescue;
        public bool Collapsed;

        /// <summary>다음 불씨를 뱉기까지 남은 시간.</summary>
        public float SpitClock;

        /// <summary>다음 큰 불을 뱉기까지 남은 시간.</summary>
        public float BlazeClock;

        /// <summary>산불: 바람 쪽 이웃에 불을 옮기기까지 남은 시간(나무만).</summary>
        public float WindClock;

        public bool Burning
        {
            get { return Fire > 0f && !Collapsed; }
        }

        /// <summary>건물(가게·창고)인가. 지킨 건물 수를 셀 때 쓴다.</summary>
        public bool IsBuilding
        {
            get { return Kind == StructureKind.House || Kind == StructureKind.Depot; }
        }

        /// <summary>안 타고, 안 무너지고, 안 젖었다 = 불씨가 노린다.</summary>
        public bool Flammable
        {
            get { return !Collapsed && Fire <= 0f && Wet <= 0f; }
        }

        /// <summary>아래쪽 문 앞.</summary>
        public Vec2 Door
        {
            get { return new Vec2(Pos.X, Pos.Y - Half.Y - 0.6f); }
        }

        /// <summary>점이 사각형에서 r 안에 있는가(제곱근 없이 먼저 거른다).</summary>
        public bool Within(Vec2 p, float r)
        {
            float dx = Math.Abs(p.X - Pos.X) - Half.X;
            float dy = Math.Abs(p.Y - Pos.Y) - Half.Y;
            if (dx > r || dy > r) return false;
            if (dx <= 0f || dy <= 0f) return true;
            return (dx * dx) + (dy * dy) <= r * r;
        }

        /// <summary>사각형 가장자리까지 거리. 안이면 0.</summary>
        public float DistanceTo(Vec2 p)
        {
            float dx = Math.Max(Math.Abs(p.X - Pos.X) - Half.X, 0f);
            float dy = Math.Max(Math.Abs(p.Y - Pos.Y) - Half.Y, 0f);
            return (float)Math.Sqrt((dx * dx) + (dy * dy));
        }
    }

    /// <summary>
    /// 시험판 C의 동네. 시드와 상관없이 늘 같은 자리다.
    /// 가운데(소방관 출발점) 반경 6칸은 비워 둔다.
    /// </summary>
    public static class SurvivorTown
    {
        public static List<Structure> Build()
        {
            var list = new List<Structure>();

            House(list, "빵집", 19f, 41f, 2);
            House(list, "꽃집", 30f, 42f, 1);
            House(list, "문구점", 41f, 41f, 1);
            House(list, "세탁소", 43f, 30f, 2);
            House(list, "약국", 41f, 19f, 1);
            House(list, "분식집", 30f, 18f, 2);
            House(list, "편의점", 19f, 19f, 1);
            House(list, "이발소", 17f, 30f, 1);
            list.Add(new Structure { Kind = StructureKind.Depot, Name = "물류창고", Pos = new Vec2(30f, 52f), Half = new Vec2(3.5f, 2.5f) });

            Add(list, StructureKind.Gas, "가스통", 24.5f, 41.5f, 0.4f, 0.4f);
            Add(list, StructureKind.Gas, "가스통", 35.5f, 18.5f, 0.4f, 0.4f);
            Add(list, StructureKind.Gas, "가스통", 43f, 33.3f, 0.4f, 0.4f);

            float[] trees = { 12f, 46f, 24f, 48f, 36f, 48f, 48f, 46f, 48f, 12f, 36f, 12f, 24f, 12f, 12f, 14f };
            for (int i = 0; i < trees.Length; i += 2) Add(list, StructureKind.Tree, "나무", trees[i], trees[i + 1], 0.6f, 0.6f);

            float[] cars = { 13f, 37f, 47f, 24f, 37f, 36f, 23f, 24f, 25f, 46f };
            for (int i = 0; i < cars.Length; i += 2) Add(list, StructureKind.Car, "차", cars[i], cars[i + 1], 1f, 0.55f);

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
