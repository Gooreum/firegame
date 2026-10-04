using FireGame.Core.Sim;
using FireGame.Prototypes.Logic;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 스테이지별 건물·장식 모델(기본 도형). 다섯 스테이지가 전부 같은 Kenney 교외 주택이라 "맵과 건물만 바뀐 노잼"이던 것을,
    /// 숲은 통나무 동네, 항구는 창고·등대·크레인 부두, 야시장은 천막 점포 골목으로 보이게 한다(맵 특색 패스).
    /// 축은 ItemModels와 같다(위 +Y, 앞(카메라 쪽·남) +Z, 1 = 1칸). 발자국 w×h에 맞춰 세우고 높이는 주택 상한(2.4) 아래.
    /// 월드에 세울 때 Place(…, Vector3.down)으로 yaw 0을 준다(Vector3.up은 180° 돌아 앞뒤가 바뀐다).
    /// </summary>
    public static class StageModels
    {
        private static readonly Color Wood = new Color(0.58f, 0.4f, 0.24f);
        private static readonly Color DarkWood = new Color(0.35f, 0.22f, 0.12f);
        private static readonly Color PineRoof = new Color(0.18f, 0.32f, 0.2f);
        private static readonly Color Stone = new Color(0.5f, 0.5f, 0.48f);
        private static readonly Color Steel = new Color(0.55f, 0.58f, 0.63f);
        private static readonly Color Dark = new Color(0.2f, 0.21f, 0.24f);
        private static readonly Color Glass = new Color(0.55f, 0.85f, 1f);
        private static readonly Color Paint = new Color(0.95f, 0.95f, 0.93f);
        private static readonly Color Tarp = new Color(0.2f, 0.45f, 0.85f);

        /// <summary>야시장 간판·차양 색. 뷰의 네온 글로우도 같은 색을 쓴다.</summary>
        public static readonly Color[] StallPalette =
        {
            new Color(0.9f, 0.2f, 0.2f), new Color(0.95f, 0.8f, 0.2f), new Color(0.3f, 0.75f, 0.35f),
            new Color(0.25f, 0.5f, 0.95f), new Color(0.95f, 0.45f, 0.7f), new Color(0.95f, 0.55f, 0.15f),
        };

        /// <summary>스테이지 전용 모델이 있으면 st 자리에 세워서 돌려주고 size=(w,h,높이). 없으면 null(BuildModels가 Kenney로 간다).</summary>
        public static GameObject Build(int stage, Structure st, int index, Transform parent, out Vector3 size)
        {
            size = Vector3.zero;
            float w = st.Half.X * 2f;
            float h = st.Half.Y * 2f;
            var at = new Vector3(st.Pos.X, st.Pos.Y, 0f);
            GameObject go;
            float height;
            switch (stage)
            {
                case 2:
                    go = Forest(st, index, parent, at, w, h, out height, out size);
                    break;
                case 4:
                    go = Harbor(st, index, parent, w, h, out height);
                    break;
                case 5:
                    go = Market(st, index, parent, w, h, out height);
                    break;
                default:
                    return null;
            }
            if (go == null) return null;
            if (size == Vector3.zero)
            {
                ItemModels.Place(go, at, 0f, Vector3.down, 1f);
                size = new Vector3(w, h, height);
            }
            return go;
        }

        // ------------------------------------------------------------------
        // 숲
        // ------------------------------------------------------------------

        private static GameObject Forest(Structure st, int index, Transform parent, Vector3 at, float w, float h, out float height, out Vector3 placed)
        {
            height = 0f;
            placed = Vector3.zero;
            switch (st.Kind)
            {
                case StructureKind.Depot:
                    return Sawmill(parent, w, h, out height);
                case StructureKind.House:
                    switch (st.Name)
                    {
                        case "전망대": return Watchtower(parent, w, h, out height);
                        case "관리사무소": return Lodge(parent, w, h, new Color(0.3f, 0.45f, 0.3f), out height);
                        case "야영 관리동": return Lodge(parent, w, h, new Color(0.45f, 0.5f, 0.35f), out height);
                        case "캠핑 매점": return Kiosk(parent, w, h, out height);
                        default:
                            Color roof = st.Name == "산장" ? new Color(0.55f, 0.2f, 0.15f) : st.Name == "통나무 카페" ? new Color(0.6f, 0.4f, 0.2f)
                                : st.Name == "숲 식당" ? new Color(0.35f, 0.35f, 0.4f) : PineRoof;
                            return Cabin(parent, w, h, roof, out height);
                    }
                case StructureKind.Car:
                    // 캠핑카: Kenney 밴(세단은 캠핑카로 안 보였다).
                    return Models3D.Place("Cars/van", parent, at, w, h, 90f, out placed);
            }
            return null;
        }

        /// <summary>통나무 집: 벽마다 가로 통나무 5단(끝이 번갈아 튀어나온다), 맞배 지붕, 돌 굴뚝, 앞문·창. 높이 약 2.2.</summary>
        private static GameObject Cabin(Transform parent, float w, float h, Color roof, out float height)
        {
            GameObject root = ItemModels.Root("Cabin", parent);
            const float pitch = 0.27f;
            const float log = 0.28f;
            float wallTop = pitch * 5f;
            for (int k = 0; k < 5; k++)
            {
                float y = (k + 0.5f) * pitch;
                float over = k % 2 == 0 ? 0.22f : 0f;
                // 앞·뒤 벽: X 방향 통나무. 옆 벽: Z 방향 통나무.
                for (int side = -1; side <= 1; side += 2)
                {
                    ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(0f, y, side * (h / 2f - 0.14f)), new Vector3(log, (w + over) / 2f, log), Wood, new Vector3(0f, 0f, 90f));
                    ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(side * (w / 2f - 0.14f), y, 0f), new Vector3(log, (h + (0.22f - over)) / 2f, log), Wood, new Vector3(90f, 0f, 0f));
                }
            }
            // 벽 안을 막는 상자(통나무 틈으로 바닥이 안 비치게).
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, wallTop / 2f, 0f), new Vector3(w - 0.3f, wallTop, h - 0.3f), DarkWood);
            // 맞배 지붕: 두 장이 가운데 마루에서 만난다(용마루는 X 방향).
            float ridge = 0.8f;
            float slope = Mathf.Sqrt((h / 2f) * (h / 2f) + (ridge * ridge)) + 0.25f;
            float tilt = Mathf.Atan2(ridge, h / 2f) * Mathf.Rad2Deg;
            for (int side = -1; side <= 1; side += 2)
            {
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, wallTop + (ridge / 2f) - 0.02f, side * (h / 4f)), new Vector3(w + 0.4f, 0.08f, slope), roof, new Vector3(-side * tilt, 0f, 0f));
            }
            ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(0f, wallTop + ridge, 0f), new Vector3(0.14f, (w + 0.5f) / 2f, 0.14f), DarkWood, new Vector3(0f, 0f, 90f));
            // 돌 굴뚝(뒤 오른쪽), 앞문, 창 둘.
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(w * 0.3f, wallTop + 0.55f, -h * 0.2f), new Vector3(0.32f, 1.1f, 0.32f), Stone);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.38f, h / 2f + 0.02f), new Vector3(0.5f, 0.76f, 0.06f), DarkWood);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(-w * 0.3f, 0.75f, h / 2f + 0.02f), new Vector3(0.45f, 0.35f, 0.05f), Glass);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(w * 0.3f, 0.75f, h / 2f + 0.02f), new Vector3(0.45f, 0.35f, 0.05f), Glass);
            height = wallTop + ridge + 0.1f;
            return root;
        }

        /// <summary>망루: 기둥 넷 위 판과 난간, 작은 지붕, 앞 사다리. 높이 2.4.</summary>
        private static GameObject Watchtower(Transform parent, float w, float h, out float height)
        {
            GameObject root = ItemModels.Root("Watchtower", parent);
            float px = w * 0.3f;
            float pz = h * 0.3f;
            const float deck = 1.5f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(sx * px, deck / 2f, sz * pz), new Vector3(0.14f, deck / 2f, 0.14f), DarkWood);
                    ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(sx * px, deck + 0.4f, sz * pz), new Vector3(0.08f, 0.4f, 0.08f), DarkWood);
                }
            }
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, deck, 0f), new Vector3(px * 2.4f, 0.1f, pz * 2.4f), Wood);
            for (int side = -1; side <= 1; side += 2)
            {
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, deck + 0.4f, side * pz * 1.15f), new Vector3(px * 2.4f, 0.05f, 0.05f), Wood);
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(side * px * 1.15f, deck + 0.4f, 0f), new Vector3(0.05f, 0.05f, pz * 2.4f), Wood);
            }
            // 작은 지붕(판보다 좁다): 난간과 판이 지붕 둘레로 보인다.
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, deck + 0.85f, 0f), new Vector3(px * 1.6f, 0.08f, pz * 1.6f), PineRoof);
            for (int sx = -1; sx <= 1; sx += 2) ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(sx * px * 0.7f, deck + 0.45f, 0f), new Vector3(0.06f, 0.4f, 0.06f), DarkWood);
            // 사다리(앞).
            for (int k = 0; k < 5; k++) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.25f + (k * 0.28f), pz + 0.12f), new Vector3(0.4f, 0.04f, 0.04f), Wood);
            height = deck + 0.9f;
            return root;
        }

        /// <summary>산림 사무소: 초록 판벽 상자 + 흰 창틀 + 맞배 지붕. 높이 약 2.1.</summary>
        private static GameObject Lodge(Transform parent, float w, float h, Color wall, out float height)
        {
            GameObject root = ItemModels.Root("Lodge", parent);
            const float wallTop = 1.3f;
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, wallTop / 2f, 0f), new Vector3(w * 0.95f, wallTop, h * 0.95f), wall);
            // 판벽 줄: 앞벽에 가는 어두운 띠 셋.
            for (int k = 1; k <= 3; k++) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, k * 0.32f, h * 0.475f + 0.01f), new Vector3(w * 0.95f, 0.03f, 0.02f), wall * 0.7f);
            float ridge = 0.7f;
            float slope = Mathf.Sqrt((h / 2f) * (h / 2f) + (ridge * ridge)) + 0.2f;
            float tilt = Mathf.Atan2(ridge, h / 2f) * Mathf.Rad2Deg;
            for (int side = -1; side <= 1; side += 2)
            {
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, wallTop + (ridge / 2f) - 0.02f, side * (h / 4f)), new Vector3(w + 0.3f, 0.08f, slope), DarkWood, new Vector3(-side * tilt, 0f, 0f));
            }
            for (int side = -1; side <= 1; side += 2)
            {
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(side * w * 0.28f, 0.8f, h * 0.475f + 0.03f), new Vector3(0.6f, 0.5f, 0.04f), Paint);
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(side * w * 0.28f, 0.8f, h * 0.475f + 0.05f), new Vector3(0.48f, 0.38f, 0.03f), Glass);
            }
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.4f, h * 0.475f + 0.03f), new Vector3(0.5f, 0.8f, 0.05f), DarkWood);
            // 앞 계단 하나.
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.08f, h * 0.475f + 0.3f), new Vector3(0.9f, 0.16f, 0.5f), Stone);
            height = wallTop + ridge + 0.1f;
            return root;
        }

        /// <summary>캠핑 매점: 작은 통나무 상자 + 주황 차양 + 간판 + 앞 좌판. 높이 약 1.6.</summary>
        private static GameObject Kiosk(Transform parent, float w, float h, out float height)
        {
            GameObject root = ItemModels.Root("Kiosk", parent);
            var orange = new Color(0.95f, 0.55f, 0.15f);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.55f, -h * 0.1f), new Vector3(w * 0.8f, 1.1f, h * 0.7f), Wood);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 1.15f, -h * 0.1f), new Vector3(w * 0.85f, 0.08f, h * 0.75f), DarkWood);
            // 차양: 앞으로 기운 주황 천.
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 1.3f, h * 0.3f), new Vector3(w * 0.9f, 0.04f, h * 0.55f), orange, new Vector3(-18f, 0f, 0f));
            for (int side = -1; side <= 1; side += 2) ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(side * w * 0.42f, 0.6f, h * 0.5f), new Vector3(0.07f, 0.6f, 0.07f), DarkWood);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.35f, h * 0.3f), new Vector3(w * 0.75f, 0.7f, 0.35f), DarkWood);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.72f, h * 0.3f), new Vector3(w * 0.8f, 0.05f, 0.45f), Paint);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 1.0f, h * 0.26f), new Vector3(w * 0.5f, 0.3f, 0.05f), Paint);
            // 좌판 위 물건 셋.
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(-w * 0.25f, 0.85f, h * 0.3f), new Vector3(0.25f, 0.22f, 0.25f), new Color(0.9f, 0.3f, 0.3f));
            ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.85f, h * 0.3f), new Vector3(0.22f, 0.12f, 0.22f), new Color(0.3f, 0.6f, 0.9f));
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(w * 0.25f, 0.85f, h * 0.3f), new Vector3(0.25f, 0.22f, 0.25f), new Color(0.95f, 0.8f, 0.3f));
            height = 1.6f;
            return root;
        }

        /// <summary>제재소(숲 창고): 기둥 넷 위 녹슨 평지붕, 왼쪽에 통나무 더미 여섯, 오른쪽에 톱날 작업대. 높이 약 1.9.</summary>
        private static GameObject Sawmill(Transform parent, float w, float h, out float height)
        {
            GameObject root = ItemModels.Root("Sawmill", parent);
            var rust = new Color(0.55f, 0.3f, 0.2f);
            const float top = 1.7f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(sx * w * 0.45f, top / 2f, sz * h * 0.45f), new Vector3(0.18f, top, 0.18f), DarkWood);
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(sx * 0f, top / 2f, sx * h * 0.45f), new Vector3(0.18f, top, 0.18f), DarkWood);
            }
            // 지붕은 뒤쪽 절반만 덮는다: 위에서 내려다보는 카메라에 통나무 더미와 톱날이 보여야 제재소로 읽힌다.
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, top, -h * 0.25f), new Vector3(w * 1.02f, 0.1f, h * 0.52f), rust);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, top + 0.08f, -h * 0.25f), new Vector3(w * 1.02f, 0.06f, 0.1f), rust * 0.8f);
            // 뒷벽(판자).
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.6f, -h * 0.45f), new Vector3(w * 0.95f, 1.2f, 0.08f), Wood);
            // 통나무 더미 3-2-1(왼쪽, X 방향으로 눕는다).
            float lx = -w * 0.25f;
            float len = w * 0.4f;
            int[] rows = { 3, 2, 1 };
            for (int r = 0; r < rows.Length; r++)
            {
                for (int k = 0; k < rows[r]; k++)
                {
                    float z = (k - ((rows[r] - 1) / 2f)) * 0.36f;
                    ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(lx, 0.18f + (r * 0.31f), z), new Vector3(0.36f, len / 2f, 0.36f), Wood, new Vector3(0f, 0f, 90f));
                }
            }
            // 톱날 작업대(오른쪽): 상 + 세로로 선 강철 원반.
            float sx2 = w * 0.25f;
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(sx2, 0.4f, 0f), new Vector3(w * 0.35f, 0.1f, h * 0.5f), DarkWood);
            for (int side = -1; side <= 1; side += 2) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(sx2 + (side * w * 0.14f), 0.18f, 0f), new Vector3(0.1f, 0.36f, h * 0.45f), DarkWood);
            ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(sx2, 0.65f, 0f), new Vector3(0.75f, 0.02f, 0.75f), Steel, new Vector3(0f, 0f, 90f));
            height = top + 0.15f;
            return root;
        }

        // ------------------------------------------------------------------
        // 항구
        // ------------------------------------------------------------------

        private static GameObject Harbor(Structure st, int index, Transform parent, float w, float h, out float height)
        {
            height = 0f;
            switch (st.Kind)
            {
                case StructureKind.Depot:
                    return ColdStore(parent, w, h, out height);
                case StructureKind.House:
                    switch (st.Name)
                    {
                        case "어시장": return FishHall(parent, w, h, out height);
                        case "등대 카페": return LighthouseCafe(parent, w, h, out height);
                        case "횟집": return Warehouse(parent, w, h, new Color(0.6f, 0.6f, 0.58f), true, out height);
                        case "선원 숙소": return null;   // 사람이 사는 집 한 채는 Kenney 주택으로 남긴다.
                        default:
                            int pick = 0;
                            foreach (char ch in st.Name) pick += ch;
                            Color[] walls = { new Color(0.42f, 0.5f, 0.56f), new Color(0.55f, 0.3f, 0.2f), new Color(0.6f, 0.6f, 0.58f) };
                            return Warehouse(parent, w, h, walls[pick % walls.Length], false, out height);
                    }
            }
            return null;
        }

        /// <summary>골함석 창고: 상자 벽 + 반원통 아치 지붕 + 큰 문 + 배기관. 횟집은 빨간 간판과 유리 수조가 붙는다. 높이 약 2.0.</summary>
        private static GameObject Warehouse(Transform parent, float w, float h, Color wall, bool sushi, out float height)
        {
            GameObject root = ItemModels.Root("Warehouse", parent);
            const float wallTop = 1.2f;
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, wallTop / 2f, 0f), new Vector3(w * 0.95f, wallTop, h * 0.95f), wall);
            // 아치 지붕: X 방향으로 누운 원통. 아래 반은 벽 상자 속에 숨는다.
            ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(0f, wallTop, 0f), new Vector3(1.5f, w * 0.95f / 2f, h * 0.95f), wall * 0.85f, new Vector3(0f, 0f, 90f));
            // 골: 지붕 위 가는 띠 셋.
            for (int k = -1; k <= 1; k++) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(k * w * 0.3f, wallTop + 0.74f, 0f), new Vector3(0.05f, 0.03f, h * 0.9f), wall * 0.7f);
            // 큰 문(앞) + 작은 창 + 배기관(뒤).
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(-w * 0.15f, 0.5f, h * 0.475f + 0.02f), new Vector3(w * 0.4f, 1.0f, 0.05f), Dark);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(w * 0.3f, 0.8f, h * 0.475f + 0.02f), new Vector3(0.5f, 0.35f, 0.04f), Glass);
            ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(w * 0.35f, wallTop + 0.9f, -h * 0.3f), new Vector3(0.14f, 0.35f, 0.14f), Steel);
            if (sushi)
            {
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, wallTop + 0.15f, h * 0.475f + 0.04f), new Vector3(w * 0.7f, 0.35f, 0.06f), new Color(0.86f, 0.16f, 0.12f));
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(w * 0.3f, 0.35f, h * 0.5f + 0.25f), new Vector3(0.7f, 0.5f, 0.45f), new Color(0.4f, 0.8f, 0.95f));
            }
            height = wallTop + 0.78f;
            return root;
        }

        /// <summary>어시장: 기둥 여섯 위 파란 천막 맞배, 아래 생선 상자 넷과 좌판. 옆이 트여 있다. 높이 약 1.9.</summary>
        private static GameObject FishHall(Transform parent, float w, float h, out float height)
        {
            GameObject root = ItemModels.Root("FishHall", parent);
            const float post = 1.4f;
            for (int sx = -1; sx <= 1; sx++)
            {
                for (int sz = -1; sz <= 1; sz += 2) ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(sx * w * 0.45f, post / 2f, sz * h * 0.45f), new Vector3(0.12f, post / 2f, 0.12f), Steel);
            }
            float ridge = 0.5f;
            float slope = Mathf.Sqrt((h / 2f) * (h / 2f) + (ridge * ridge)) + 0.3f;
            float tilt = Mathf.Atan2(ridge, h / 2f) * Mathf.Rad2Deg;
            for (int side = -1; side <= 1; side += 2)
            {
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, post + (ridge / 2f), side * (h / 4f)), new Vector3(w * 1.05f, 0.05f, slope), Tarp, new Vector3(-side * tilt, 0f, 0f));
            }
            ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(0f, post + ridge, 0f), new Vector3(0.1f, w * 1.05f / 2f, 0.1f), Steel, new Vector3(0f, 0f, 90f));
            // 좌판(앞)과 생선 상자(흰 상자 위 파란 생선).
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.3f, h * 0.2f), new Vector3(w * 0.85f, 0.6f, 0.6f), Paint);
            for (int k = 0; k < 4; k++)
            {
                float x = (k - 1.5f) * w * 0.2f;
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(x, 0.72f, h * 0.2f), new Vector3(0.5f, 0.24f, 0.45f), new Color(0.85f, 0.88f, 0.9f));
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(x, 0.88f, h * 0.2f), new Vector3(0.4f, 0.1f, 0.3f), k % 2 == 0 ? new Color(0.3f, 0.5f, 0.75f) : new Color(0.85f, 0.4f, 0.3f));
            }
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.45f, -h * 0.3f), new Vector3(w * 0.6f, 0.9f, 0.6f), Steel);
            height = post + ridge + 0.1f;
            return root;
        }

        /// <summary>등대 카페: 작은 카페 상자 옆에 흰 등대(빨간 띠 둘, 등실 "Lamp"는 뷰가 돌린다). 높이 2.45.</summary>
        private static GameObject LighthouseCafe(Transform parent, float w, float h, out float height)
        {
            GameObject root = ItemModels.Root("LighthouseCafe", parent);
            float cafeW = w * 0.55f;
            float cx = w * 0.2f;
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(cx, 0.55f, 0f), new Vector3(cafeW, 1.1f, h * 0.9f), Paint);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(cx, 1.12f, 0f), new Vector3(cafeW + 0.2f, 0.08f, h * 0.95f), new Color(0.25f, 0.4f, 0.6f));
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(cx, 0.6f, h * 0.45f + 0.02f), new Vector3(cafeW * 0.7f, 0.5f, 0.04f), Glass);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(cx, 1.0f, h * 0.45f + 0.03f), new Vector3(cafeW * 0.6f, 0.2f, 0.04f), new Color(0.25f, 0.4f, 0.6f));
            // 등대(왼쪽): 흰 원기둥 + 빨간 띠 + 발코니 + 등실 + 빨간 지붕.
            float lx = -w * 0.3f;
            ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(lx, 1.0f, 0f), new Vector3(0.75f, 1.0f, 0.75f), Paint);
            ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(lx, 0.55f, 0f), new Vector3(0.78f, 0.1f, 0.78f), new Color(0.86f, 0.16f, 0.12f));
            ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(lx, 1.35f, 0f), new Vector3(0.78f, 0.1f, 0.78f), new Color(0.86f, 0.16f, 0.12f));
            ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(lx, 2.0f, 0f), new Vector3(0.95f, 0.03f, 0.95f), Dark);
            GameObject lamp = ItemModels.Part(PrimitiveType.Cube, root, new Vector3(lx, 2.2f, 0f), new Vector3(0.45f, 0.32f, 0.45f), Glass);
            lamp.name = "Lamp";
            ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(lx, 2.4f, 0f), new Vector3(0.55f, 0.05f, 0.55f), new Color(0.86f, 0.16f, 0.12f));
            height = 2.45f;
            return root;
        }

        /// <summary>수산 냉동창고(항구 창고): 긴 흰 상자 + 하늘색 띠 + 지붕 냉각기 셋 + 하차장과 셔터 둘. 높이 약 1.95.</summary>
        private static GameObject ColdStore(Transform parent, float w, float h, out float height)
        {
            GameObject root = ItemModels.Root("ColdStore", parent);
            var white = new Color(0.92f, 0.94f, 0.96f);
            var ice = new Color(0.55f, 0.8f, 0.95f);
            const float wallTop = 1.5f;
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, wallTop / 2f, 0f), new Vector3(w * 0.95f, wallTop, h * 0.9f), white);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 1.05f, 0f), new Vector3(w * 0.96f, 0.22f, h * 0.91f), ice);
            for (int k = -1; k <= 1; k++)
            {
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(k * w * 0.3f, wallTop + 0.18f, -h * 0.15f), new Vector3(0.9f, 0.36f, 0.9f), Steel);
                ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(k * w * 0.3f, wallTop + 0.37f, -h * 0.15f), new Vector3(0.7f, 0.02f, 0.7f), Dark);
            }
            // 하차장(앞): 어두운 단 + 셔터 둘.
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.15f, h * 0.45f + 0.2f), new Vector3(w * 0.8f, 0.3f, 0.5f), Dark);
            for (int side = -1; side <= 1; side += 2) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(side * w * 0.22f, 0.65f, h * 0.45f + 0.02f), new Vector3(w * 0.25f, 0.75f, 0.05f), Steel);
            height = wallTop + 0.45f;
            return root;
        }

        // ------------------------------------------------------------------
        // 야시장
        // ------------------------------------------------------------------

        private static GameObject Market(Structure st, int index, Transform parent, float w, float h, out float height)
        {
            height = 0f;
            switch (st.Kind)
            {
                case StructureKind.Depot:
                    return Stage(parent, w, h, out height);
                case StructureKind.House:
                    return Stall(parent, w, h, StallPalette[index % StallPalette.Length], out height);
                case StructureKind.Car:
                    return FoodTruck(parent, w, h, StallPalette[(index + 2) % StallPalette.Length], out height);
                case StructureKind.Gas:
                    return GasCans(parent, w, h, out height);
            }
            return null;
        }

        /// <summary>천막 점포: 기둥 넷, 줄무늬 차양(앞이 낮다), 좌판과 물건, 뒷판, 색 간판 "Sign", 등불 "Lamp". 높이 약 1.75.</summary>
        private static GameObject Stall(Transform parent, float w, float h, Color color, out float height)
        {
            GameObject root = ItemModels.Root("Stall", parent);
            const float pole = 1.5f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2) ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(sx * w * 0.45f, pole / 2f, sz * h * 0.45f), new Vector3(0.08f, pole / 2f, 0.08f), Steel);
            }
            // 차양: 일곱 장이 번갈아 색·흰색, 앞으로 12° 기운다.
            const int stripes = 7;
            float stripeW = w * 1.1f / stripes;
            for (int k = 0; k < stripes; k++)
            {
                float x = (k - ((stripes - 1) / 2f)) * stripeW;
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(x, pole + 0.08f, 0f), new Vector3(stripeW + 0.01f, 0.04f, h * 0.8f), k % 2 == 0 ? color : Paint, new Vector3(-12f, 0f, 0f));
            }
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, pole + 0.14f, -h * 0.4f), new Vector3(w * 1.1f, 0.12f, 0.05f), color * 0.8f);
            // 좌판(앞)과 물건, 뒷판.
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.3f, h * 0.22f), new Vector3(w * 0.9f, 0.6f, 0.5f), Wood);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.62f, h * 0.22f), new Vector3(w * 0.95f, 0.05f, 0.6f), DarkWood);
            for (int k = -1; k <= 1; k++) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(k * w * 0.26f, 0.76f, h * 0.22f), new Vector3(0.3f, 0.22f, 0.3f), StallPalette[(k + 4) % StallPalette.Length]);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.6f, -h * 0.44f), new Vector3(w * 0.9f, 1.2f, 0.06f), new Color(0.3f, 0.25f, 0.25f));
            GameObject sign = ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 1.32f, h * 0.5f + 0.02f), new Vector3(w * 0.7f, 0.28f, 0.05f), color);
            sign.name = "Sign";
            GameObject lamp = ItemModels.Part(PrimitiveType.Sphere, root, new Vector3(w * 0.3f, 1.2f, h * 0.5f), new Vector3(0.26f, 0.26f, 0.26f), new Color(1f, 0.78f, 0.4f));
            lamp.name = "Lamp";
            height = pole + 0.25f;
            return root;
        }

        /// <summary>공연 무대(야시장 창고): 단 + 양쪽 탑 + 트러스 + 스포트라이트 "Spot0..3" + 뒷막 + 스피커 둘. 높이 약 2.3.</summary>
        private static GameObject Stage(Transform parent, float w, float h, out float height)
        {
            GameObject root = ItemModels.Root("Stage", parent);
            var truss = new Color(0.45f, 0.47f, 0.5f);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.2f, 0f), new Vector3(w * 0.95f, 0.4f, h * 0.95f), new Color(0.25f, 0.22f, 0.28f));
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.41f, 0f), new Vector3(w * 0.9f, 0.02f, h * 0.9f), new Color(0.35f, 0.3f, 0.38f));
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 1.2f, -h * 0.44f), new Vector3(w * 0.9f, 1.6f, 0.08f), new Color(0.2f, 0.1f, 0.3f));
            for (int side = -1; side <= 1; side += 2)
            {
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(side * w * 0.47f, 1.1f, 0f), new Vector3(0.28f, 2.2f, 0.28f), truss);
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(side * w * 0.4f, 0.8f, h * 0.28f), new Vector3(0.5f, 0.8f, 0.4f), Dark);
                ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(side * w * 0.4f, 0.95f, h * 0.28f + 0.21f), new Vector3(0.3f, 0.02f, 0.3f), Steel, new Vector3(90f, 0f, 0f));
            }
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 2.22f, 0f), new Vector3(w * 1.0f, 0.12f, 0.12f), truss);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 2.22f, -0.15f), new Vector3(w * 1.0f, 0.12f, 0.12f), truss);
            for (int k = 0; k < 4; k++)
            {
                float x = (k - 1.5f) * w * 0.22f;
                GameObject spot = ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(x, 2.05f, 0.05f), new Vector3(0.24f, 0.12f, 0.24f), Dark, new Vector3(25f, 0f, 0f));
                spot.name = "Spot" + k;
                ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(x, 1.95f, 0.1f), new Vector3(0.2f, 0.02f, 0.2f), StallPalette[(k * 2) % StallPalette.Length], new Vector3(25f, 0f, 0f));
            }
            height = 2.3f;
            return root;
        }

        /// <summary>푸드트럭: 색 차체 + 흰 운전석 + 바퀴 넷 + 서빙 창과 차양. 높이 약 1.1.</summary>
        private static GameObject FoodTruck(Transform parent, float w, float h, Color color, out float height)
        {
            GameObject root = ItemModels.Root("FoodTruck", parent);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(-w * 0.12f, 0.58f, 0f), new Vector3(w * 0.74f, 0.75f, h * 0.9f), color);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(w * 0.37f, 0.5f, 0f), new Vector3(w * 0.26f, 0.58f, h * 0.85f), Paint);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(w * 0.5f, 0.6f, 0f), new Vector3(0.04f, 0.3f, h * 0.7f), Glass);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2) ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(sx * w * 0.32f, 0.16f, sz * h * 0.45f), new Vector3(0.32f, 0.08f, 0.32f), Dark, new Vector3(90f, 0f, 0f));
            }
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(-w * 0.12f, 0.65f, h * 0.45f + 0.02f), new Vector3(w * 0.5f, 0.32f, 0.04f), Glass);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(-w * 0.12f, 0.98f, h * 0.55f), new Vector3(w * 0.56f, 0.03f, 0.4f), Paint, new Vector3(-20f, 0f, 0f));
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(-w * 0.12f, 0.99f, h * 0.55f), new Vector3(w * 0.56f, 0.032f, 0.14f), color * 0.8f, new Vector3(-20f, 0f, 0f));
            height = 1.1f;
            return root;
        }

        /// <summary>부탄가스 묶음: 나무 상자 위 파란 작은 통 셋(빨간 꼭지). 높이 약 0.65.</summary>
        private static GameObject GasCans(Transform parent, float w, float h, out float height)
        {
            GameObject root = ItemModels.Root("GasCans", parent);
            var blue = new Color(0.25f, 0.45f, 0.85f);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.1f, 0f), new Vector3(w * 1.1f, 0.2f, h * 1.1f), Wood);
            for (int k = 0; k < 3; k++)
            {
                float a = k * Mathf.PI * 2f / 3f;
                var p = new Vector3(Mathf.Cos(a) * w * 0.28f, 0.42f, Mathf.Sin(a) * h * 0.28f);
                ItemModels.Part(PrimitiveType.Cylinder, root, p, new Vector3(0.24f, 0.22f, 0.24f), blue);
                ItemModels.Part(PrimitiveType.Cylinder, root, p + new Vector3(0f, 0.25f, 0f), new Vector3(0.1f, 0.04f, 0.1f), new Color(0.86f, 0.16f, 0.12f));
            }
            height = 0.7f;
            return root;
        }
    }
}
