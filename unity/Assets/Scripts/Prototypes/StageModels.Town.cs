using FireGame.Core.Sim;
using FireGame.Prototypes.Logic;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 마을(1스테이지) 가게 모델. Kenney 교외 주택에 지붕색만 바꾼 열두 채가 "편의점이 그냥 하얀 집"으로 보였다(사용자 지적).
    /// 카메라가 거의 위에서 보니(기울기 25°) 가게는 지붕과 앞마당으로 읽힌다: 가게마다 지붕 위 큰 상징물 하나(도로변 거대 간판처럼)와
    /// 앞마당 진열 하나를 준다. 몸체(ShopBody)는 뒤로 물려 앞 0.9칸을 마당으로 비운다. 문은 앞 가운데(구조 자리와 맞는다).
    /// 이름표는 지붕 앞 가장자리에 서니 상징물은 지붕 뒤쪽에 둔다.
    /// </summary>
    public static partial class StageModels
    {
        private static readonly Color Brick = new Color(0.62f, 0.36f, 0.26f);
        private static readonly Color Cream = new Color(0.96f, 0.9f, 0.74f);
        private static readonly Color Crust = new Color(0.78f, 0.5f, 0.22f);
        private static readonly Color Red = new Color(0.86f, 0.18f, 0.15f);
        private static readonly Color Leaf = new Color(0.25f, 0.55f, 0.25f);
        private static readonly Color Bone = new Color(0.97f, 0.95f, 0.88f);

        /// <summary>ShopBody가 돌려주는 자리: 지붕 윗면 y, 몸체 가운데 z·깊이, 앞벽 z, 마당 가운데 z.</summary>
        private struct ShopFrame
        {
            public float Top;
            public float Mid;
            public float Depth;
            public float Front;
            public float Yard;

            /// <summary>지붕 뒤쪽(이름표에 안 가리는 자리) z.</summary>
            public float Back => Mid - (Depth * 0.18f);
        }

        private static GameObject Town(Structure st, Transform parent, float w, float h, out float height)
        {
            height = 0f;
            if (st.Kind == StructureKind.Depot) return Depot(parent, w, h, out height);
            if (st.Kind != StructureKind.House) return null;
            switch (st.Name)
            {
                case "편의점": return ConvenienceStore(parent, w, h, out height);
                case "빵집": return Bakery(parent, w, h, out height);
                case "꽃집": return Florist(parent, w, h, out height);
                case "이발소": return Barber(parent, w, h, out height);
                case "세탁소": return Laundry(parent, w, h, out height);
                case "분식집": return SnackBar(parent, w, h, out height);
                case "카페": return Cafe(parent, w, h, out height);
                case "서점": return Bookstore(parent, w, h, out height);
                case "정육점": return Butcher(parent, w, h, out height);
                case "미용실": return Salon(parent, w, h, out height);
                case "철물점": return Hardware(parent, w, h, out height);
                case "치킨집": return ChickenShop(parent, w, h, out height);
            }
            return null;
        }

        // ------------------------------------------------------------------
        // 몸체와 공통 소품
        // ------------------------------------------------------------------

        /// <summary>
        /// 가게 몸체: 벽 상자(깊이 0.7h, 뒤로 물림) + 평지붕과 낮은 난간 + 앞 유리 진열창 둘 + 가운데 문 + 간판 띠.
        /// awning이 있으면 진열창 위로 앞이 낮은 줄무늬 차양(a·b 번갈아).
        /// </summary>
        private static ShopFrame ShopBody(GameObject root, float w, float h, Color wall, Color roof, Color sign, float wallTop, Color? awningA = null, Color? awningB = null)
        {
            float depth = h * 0.7f;
            float mid = -(h - depth) / 2f;
            float front = mid + (depth / 2f);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, wallTop / 2f, mid), new Vector3(w * 0.94f, wallTop, depth), wall);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, wallTop + 0.04f, mid), new Vector3(w * 0.96f, 0.08f, depth + 0.04f), roof);
            // 난간: 지붕 둘레 낮은 턱(벽색보다 어둡게).
            Color rim = wall * 0.82f;
            for (int side = -1; side <= 1; side += 2)
            {
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, wallTop + 0.12f, mid + (side * depth / 2f)), new Vector3(w * 0.96f, 0.14f, 0.06f), rim);
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(side * w * 0.48f, wallTop + 0.12f, mid), new Vector3(0.06f, 0.14f, depth), rim);
            }
            // 간판 띠 + 진열창 둘 + 문.
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, wallTop - 0.18f, front + 0.03f), new Vector3(w * 0.9f, 0.28f, 0.06f), sign);
            for (int side = -1; side <= 1; side += 2)
            {
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(side * w * 0.27f, 0.58f, front + 0.02f), new Vector3(w * 0.3f, 0.62f, 0.04f), Paint);
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(side * w * 0.27f, 0.58f, front + 0.04f), new Vector3(w * 0.27f, 0.54f, 0.03f), Glass);
            }
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.42f, front + 0.03f), new Vector3(0.5f, 0.84f, 0.05f), Dark);
            if (awningA.HasValue)
            {
                const int stripes = 8;
                float sw = w * 0.92f / stripes;
                for (int k = 0; k < stripes; k++)
                {
                    float x = (k - ((stripes - 1) / 2f)) * sw;
                    ItemModels.Part(PrimitiveType.Cube, root, new Vector3(x, wallTop - 0.42f, front + 0.22f), new Vector3(sw + 0.01f, 0.04f, 0.45f), k % 2 == 0 ? awningA.Value : (awningB ?? Paint), new Vector3(-22f, 0f, 0f));
                }
            }
            return new ShopFrame { Top = wallTop + 0.08f, Mid = mid, Depth = depth, Front = front, Yard = front + ((h / 2f - front) / 2f) };
        }

        /// <summary>실외기: 회색 상자 + 위 검은 팬.</summary>
        private static void AirUnit(GameObject root, Vector3 at)
        {
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.15f, 0f), new Vector3(0.5f, 0.3f, 0.4f), Steel);
            ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.31f, 0f), new Vector3(0.32f, 0.01f, 0.32f), Dark);
        }

        /// <summary>파라솔 탁자: 흰 상 + 기둥 + 색 우산 + 의자 둘.</summary>
        private static void ParasolTable(GameObject root, Vector3 at, Color umbrella, Color chair, float shade = 1f)
        {
            ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.25f, 0f), new Vector3(0.42f, 0.02f, 0.42f), Paint);
            ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.45f, 0f), new Vector3(0.04f, 0.45f, 0.04f), Steel);
            ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.88f, 0f), new Vector3(0.85f * shade, 0.04f, 0.85f * shade), umbrella);
            ItemModels.Part(PrimitiveType.Sphere, root, at + new Vector3(0f, 0.93f, 0f), new Vector3(0.1f, 0.08f, 0.1f), umbrella * 0.8f);
            for (int side = -1; side <= 1; side += 2) ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(side * 0.32f, 0.14f, 0f), new Vector3(0.2f, 0.28f, 0.2f), chair);
        }

        /// <summary>화분: 테라코타 통 + 초록 덤불.</summary>
        private static void Pot(GameObject root, Vector3 at, float size)
        {
            ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.12f * size, 0f), new Vector3(0.26f * size, 0.12f * size, 0.26f * size), new Color(0.72f, 0.4f, 0.25f));
            ItemModels.Part(PrimitiveType.Sphere, root, at + new Vector3(0f, 0.34f * size, 0f), new Vector3(0.36f * size, 0.3f * size, 0.36f * size), Leaf);
        }

        /// <summary>입간판: 앞뒤로 벌어진 판 두 장(위에서 보면 작은 A자).</summary>
        private static void SandwichBoard(GameObject root, Vector3 at, Color board)
        {
            for (int side = -1; side <= 1; side += 2) ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.2f, side * 0.07f), new Vector3(0.32f, 0.42f, 0.03f), side < 0 ? board : Dark, new Vector3(side * 12f, 0f, 0f));
        }

        /// <summary>이발소 회전등 "Pole": 흰 기둥 둘레에 기운 빨강·파랑 띠. 뷰가 Y로 돌려 띠가 올라가 보이게 한다.</summary>
        private static void BarberPole(GameObject root, Vector3 at, float tall, Color a, Color b, bool lying = false, float radius = 0.11f)
        {
            var pole = new GameObject("Pole");
            if (lying)
            {
                // 지붕에 눕힌 큰 회전등: 받침 축(PoleMount)이 눕히고, 안의 Pole이 자기 축으로 돈다(위에서 띠가 굴러 보인다).
                var mount = new GameObject("PoleMount");
                mount.transform.SetParent(root.transform, false);
                mount.transform.localPosition = at;
                mount.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                pole.transform.SetParent(mount.transform, false);
                pole.transform.localPosition = new Vector3(0f, -tall / 2f, 0f);
            }
            else
            {
                pole.transform.SetParent(root.transform, false);
                pole.transform.localPosition = at;
            }
            ItemModels.Part(PrimitiveType.Cylinder, pole, new Vector3(0f, tall / 2f, 0f), new Vector3(radius * 2f, tall / 2f, radius * 2f), Paint);
            // 나선 띠 둘: 짧은 조각을 나선을 따라 이어 붙인다(한 바퀴 12조각, 두 띠는 반 바퀴 어긋난다).
            const int perTurn = 12;
            float pitch = radius * 9f;
            int count = Mathf.CeilToInt(tall / pitch * perTurn);
            float arc = 2f * Mathf.PI * radius / perTurn;
            float rise = pitch / perTurn;
            float tilt = Mathf.Atan2(rise, arc) * Mathf.Rad2Deg;
            for (int band = 0; band < 2; band++)
            {
                for (int i = 0; i < count; i++)
                {
                    float y = (i + 0.5f) * rise;
                    if (y > tall - 0.02f) break;
                    float ang = (i * 360f / perTurn) + (band * 180f);
                    float rad = ang * Mathf.Deg2Rad;
                    ItemModels.Part(PrimitiveType.Cube, pole, new Vector3(Mathf.Sin(rad) * radius, y, Mathf.Cos(rad) * radius), new Vector3(arc * 1.25f, pitch * 0.22f, radius * 0.25f), band == 0 ? a : b, new Vector3(0f, ang, tilt));
                }
            }
            ItemModels.Part(PrimitiveType.Sphere, pole, new Vector3(0f, tall + (radius * 0.4f), 0f), new Vector3(radius * 2.2f, radius * 1.6f, radius * 2.2f), Glass);
            ItemModels.Part(PrimitiveType.Cylinder, pole, new Vector3(0f, 0.03f, 0f), new Vector3(radius * 2.4f, 0.03f, radius * 2.4f), Dark);
        }

        // ------------------------------------------------------------------
        // 가게
        // ------------------------------------------------------------------

        /// <summary>편의점: 흰 벽을 초록·주황·파랑 세 띠가 두르고, 앞면은 통유리(물건 줄), 지붕 실외기 셋, 마당에 줄무늬 기둥 간판·파라솔 탁자·냉동고·음료 상자.</summary>
        private static GameObject ConvenienceStore(Transform parent, float w, float h, out float height)
        {
            GameObject root = ItemModels.Root("ConvenienceStore", parent);
            const float wallTop = 1.35f;
            ShopFrame f = ShopBody(root, w, h, new Color(0.96f, 0.96f, 0.95f), new Color(0.72f, 0.74f, 0.78f), Paint, wallTop);
            Color[] stripes = { new Color(0.15f, 0.6f, 0.3f), new Color(0.98f, 0.55f, 0.12f), new Color(0.15f, 0.4f, 0.85f) };
            for (int k = 0; k < 3; k++)
            {
                float y = wallTop - 0.1f - (k * 0.1f);
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, y, f.Front + 0.05f), new Vector3(w * 0.95f, 0.09f, 0.04f), stripes[k]);
                for (int side = -1; side <= 1; side += 2) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(side * (w * 0.47f + 0.02f), y, f.Mid), new Vector3(0.04f, 0.09f, f.Depth), stripes[k]);
            }
            // 통유리 앞면 + 진열 물건 세 줄(유리 위 색 점).
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.55f, f.Front + 0.05f), new Vector3(w * 0.86f, 0.7f, 0.03f), new Color(0.65f, 0.9f, 1f));
            Color[] goods = { Red, new Color(0.98f, 0.8f, 0.2f), new Color(0.3f, 0.75f, 0.35f), new Color(0.3f, 0.55f, 0.95f), new Color(0.95f, 0.5f, 0.75f) };
            for (int row = 0; row < 3; row++)
            {
                for (int k = 0; k < 8; k++)
                {
                    float x = (k - 3.5f) * w * 0.1f;
                    if (Mathf.Abs(x) < 0.3f) continue;   // 문 자리
                    ItemModels.Part(PrimitiveType.Cube, root, new Vector3(x, 0.32f + (row * 0.2f), f.Front + 0.07f), new Vector3(0.14f, 0.12f, 0.03f), goods[(k + row * 2) % goods.Length]);
                }
            }
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.42f, f.Front + 0.08f), new Vector3(0.5f, 0.84f, 0.03f), new Color(0.5f, 0.8f, 0.95f));
            // 앞 차양: 세 색 띠가 앞으로 나온 평평한 캐노피(위에서 보이는 편의점 표식).
            for (int k = 0; k < 3; k++) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, wallTop + 0.02f, f.Front + 0.1f + (k * 0.16f)), new Vector3(w * 0.96f, 0.06f, 0.16f), stripes[k]);
            // 지붕 위 세 색 띠 간판(위에서 보이는 편의점 표식) + 실외기 둘(양끝).
            for (int k = 0; k < 3; k++) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, f.Top + 0.06f, f.Back - 0.25f + (k * 0.28f)), new Vector3(w * 0.62f, 0.12f, 0.26f), stripes[k]);
            for (int side = -1; side <= 1; side += 2) AirUnit(root, new Vector3(side * w * 0.4f, f.Top, f.Back));
            // 줄무늬 기둥 간판(마당 오른쪽 끝).
            float px = w * 0.43f;
            ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(px, 0.9f, f.Yard + 0.15f), new Vector3(0.1f, 0.9f, 0.1f), Steel);
            for (int k = 0; k < 3; k++) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(px, 2.0f - (k * 0.2f), f.Yard + 0.15f), new Vector3(0.8f, 0.2f, 0.45f), stripes[k]);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(px, 2.13f, f.Yard + 0.15f), new Vector3(0.8f, 0.06f, 0.45f), Paint);
            // 파라솔 탁자 둘(왼쪽), 냉동고와 음료 상자(오른쪽).
            ParasolTable(root, new Vector3(-w * 0.36f, 0f, f.Yard), new Color(0.2f, 0.62f, 0.32f), Red, 0.75f);
            ParasolTable(root, new Vector3(-w * 0.17f, 0f, f.Yard + 0.12f), new Color(0.2f, 0.62f, 0.32f), new Color(0.3f, 0.55f, 0.95f), 0.75f);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(w * 0.2f, 0.22f, f.Yard - 0.05f), new Vector3(0.7f, 0.44f, 0.42f), Paint);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(w * 0.2f, 0.45f, f.Yard - 0.05f), new Vector3(0.62f, 0.03f, 0.34f), new Color(0.6f, 0.88f, 1f));
            for (int k = 0; k < 4; k++) ItemModels.Part(PrimitiveType.Sphere, root, new Vector3(w * 0.2f + ((k - 1.5f) * 0.14f), 0.47f, f.Yard - 0.05f), new Vector3(0.1f, 0.05f, 0.1f), goods[k]);
            for (int k = 0; k < 3; k++) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(w * 0.33f, 0.11f + (k * 0.2f), f.Yard + 0.25f), new Vector3(0.36f, 0.18f, 0.28f), k % 2 == 0 ? Red : new Color(0.15f, 0.4f, 0.85f));
            ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(w * 0.08f + 0.35f, 0.2f, f.Yard + 0.3f), new Vector3(0.22f, 0.2f, 0.22f), new Color(0.2f, 0.45f, 0.3f));
            height = 2.25f;
            return root;
        }

        /// <summary>빵집: 벽돌 벽에 갈색·크림 차양, 지붕에 거대 식빵과 바게트, 굴뚝, 마당에 빵 바구니 진열대와 입간판.</summary>
        private static GameObject Bakery(Transform parent, float w, float h, out float height)
        {
            GameObject root = ItemModels.Root("Bakery", parent);
            ShopFrame f = ShopBody(root, w, h, Brick, new Color(0.42f, 0.27f, 0.2f), Cream, 1.3f, new Color(0.5f, 0.3f, 0.18f), Cream);
            // 거대 식빵: 누운 캡슐 몸 + 위 껍질 + 칼집 셋.
            Vector3 loaf = new Vector3(-w * 0.12f, f.Top + 0.3f, f.Back);
            ItemModels.Part(PrimitiveType.Capsule, root, loaf, new Vector3(0.75f, 0.85f, 0.65f), Crust, new Vector3(0f, 0f, 90f));
            ItemModels.Part(PrimitiveType.Capsule, root, loaf + new Vector3(0f, 0.12f, 0f), new Vector3(0.62f, 0.75f, 0.55f), new Color(0.88f, 0.62f, 0.3f), new Vector3(0f, 0f, 90f));
            for (int k = -1; k <= 1; k++) ItemModels.Part(PrimitiveType.Cube, root, loaf + new Vector3(k * 0.35f, 0.44f, 0f), new Vector3(0.06f, 0.04f, 0.45f), new Color(0.55f, 0.32f, 0.14f), new Vector3(0f, 25f, 0f));
            // 바게트 둘(비스듬히).
            for (int k = 0; k < 2; k++) ItemModels.Part(PrimitiveType.Capsule, root, new Vector3(w * 0.25f + (k * 0.22f), f.Top + 0.12f, f.Back + 0.1f), new Vector3(0.18f, 0.55f, 0.18f), Crust, new Vector3(90f, 30f + (k * 10f), 0f));
            // 굴뚝(뒤 왼쪽).
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(-w * 0.4f, f.Top + 0.3f, f.Mid - (f.Depth * 0.35f)), new Vector3(0.3f, 0.6f, 0.3f), new Color(0.45f, 0.3f, 0.25f));
            // 진열대 + 바구니 셋(둥근 빵·바게트·크루아상 색) + 입간판.
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(-w * 0.27f, 0.3f, f.Yard), new Vector3(w * 0.36f, 0.06f, 0.5f), DarkWood);
            for (int side = -1; side <= 1; side += 2) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(-w * 0.27f + (side * w * 0.16f), 0.15f, f.Yard), new Vector3(0.06f, 0.3f, 0.45f), DarkWood);
            for (int k = 0; k < 3; k++)
            {
                var b = new Vector3(-w * 0.27f + ((k - 1) * 0.42f), 0.38f, f.Yard);
                ItemModels.Part(PrimitiveType.Cylinder, root, b, new Vector3(0.36f, 0.06f, 0.36f), new Color(0.65f, 0.48f, 0.28f));
                for (int j = 0; j < 3; j++) ItemModels.Part(PrimitiveType.Sphere, root, b + new Vector3((j - 1) * 0.1f, 0.08f, (j % 2) * 0.06f), new Vector3(0.14f, 0.1f, 0.12f), k == 1 ? new Color(0.92f, 0.7f, 0.35f) : Crust);
            }
            SandwichBoard(root, new Vector3(w * 0.3f, 0f, f.Yard), Cream);
            Pot(root, new Vector3(w * 0.42f, 0f, f.Yard - 0.15f), 1f);
            height = f.Top + 0.75f;
            return root;
        }

        /// <summary>꽃집: 민트 벽에 초록 차양, 지붕에 거대 꽃 한 송이, 마당에 꽃 양동이 두 줄과 화분 계단.</summary>
        private static GameObject Florist(Transform parent, float w, float h, out float height)
        {
            GameObject root = ItemModels.Root("Florist", parent);
            ShopFrame f = ShopBody(root, w, h, new Color(0.62f, 0.82f, 0.68f), new Color(0.3f, 0.52f, 0.35f), Paint, 1.25f, new Color(0.2f, 0.55f, 0.3f), Paint);
            // 거대 꽃: 분홍 꽃잎 여섯 + 노란 가운데 + 잎 둘(지붕 위에 눕는다).
            var c = new Vector3(0f, f.Top + 0.18f, f.Back);
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f;
                ItemModels.Part(PrimitiveType.Sphere, root, c + new Vector3(Mathf.Cos(a) * 0.38f, 0f, Mathf.Sin(a) * 0.32f), new Vector3(0.5f, 0.16f, 0.42f), new Color(0.95f, 0.45f, 0.65f), new Vector3(0f, -k * 60f, 0f));
            }
            ItemModels.Part(PrimitiveType.Sphere, root, c + new Vector3(0f, 0.06f, 0f), new Vector3(0.4f, 0.2f, 0.4f), new Color(0.98f, 0.82f, 0.2f));
            for (int side = -1; side <= 1; side += 2) ItemModels.Part(PrimitiveType.Sphere, root, c + new Vector3(side * 0.85f, -0.05f, 0.1f), new Vector3(0.55f, 0.08f, 0.25f), Leaf, new Vector3(0f, side * 25f, 0f));
            // 꽃 양동이 두 줄(문 양옆).
            Color[] blooms = { Red, new Color(0.98f, 0.82f, 0.2f), new Color(0.95f, 0.5f, 0.75f), new Color(0.6f, 0.35f, 0.85f), Paint, new Color(0.98f, 0.55f, 0.15f) };
            int n = 0;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < 2; row++)
                {
                    for (int k = 0; k < 2; k++)
                    {
                        var b = new Vector3(side * (w * 0.2f + (k * 0.4f)), 0f, f.Yard - 0.18f + (row * 0.38f));
                        ItemModels.Part(PrimitiveType.Cylinder, root, b + new Vector3(0f, 0.14f, 0f), new Vector3(0.28f, 0.14f, 0.28f), new Color(0.5f, 0.58f, 0.65f));
                        ItemModels.Part(PrimitiveType.Sphere, root, b + new Vector3(0f, 0.32f, 0f), new Vector3(0.34f, 0.16f, 0.34f), Leaf);
                        Color bloom = blooms[n++ % blooms.Length];
                        for (int j = 0; j < 3; j++) ItemModels.Part(PrimitiveType.Sphere, root, b + new Vector3((j - 1) * 0.09f, 0.4f, (j % 2) * 0.07f - 0.03f), new Vector3(0.12f, 0.1f, 0.12f), bloom);
                    }
                }
            }
            height = f.Top + 0.35f;
            return root;
        }

        /// <summary>이발소: 흰·하늘 벽, 지붕에 큰 회전등, 문 옆에 작은 회전등과 대기 벤치.</summary>
        private static GameObject Barber(Transform parent, float w, float h, out float height)
        {
            GameObject root = ItemModels.Root("Barber", parent);
            var blue = new Color(0.2f, 0.4f, 0.85f);
            ShopFrame f = ShopBody(root, w, h, new Color(0.86f, 0.92f, 0.97f), new Color(0.3f, 0.45f, 0.7f), blue, 1.3f);
            BarberPole(root, new Vector3(-w * 0.08f, f.Top + 0.3f, f.Back), 1.9f, Red, blue, true, 0.26f);
            AirUnit(root, new Vector3(w * 0.38f, f.Top, f.Back));
            // 앞벽 세로 띠(이발소 줄무늬 간판).
            for (int k = 0; k < 3; k++) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(w * 0.42f, 0.7f, f.Front + 0.05f), new Vector3(0.1f, 0.9f, 0.03f), k == 0 ? Red : k == 1 ? Paint : blue, new Vector3(0f, 0f, 0f));
            BarberPole(root, new Vector3(0.45f, 0f, f.Front + 0.18f), 0.75f, Red, blue);
            // 대기 벤치(왼쪽) + 손님 둘(머리 구).
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(-w * 0.28f, 0.22f, f.Yard), new Vector3(1.1f, 0.06f, 0.32f), Wood);
            for (int side = -1; side <= 1; side += 2) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(-w * 0.28f + (side * 0.45f), 0.1f, f.Yard), new Vector3(0.06f, 0.2f, 0.28f), Dark);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(-w * 0.28f, 0.38f, f.Yard - 0.15f), new Vector3(1.1f, 0.28f, 0.05f), Wood);
            for (int side = -1; side <= 1; side += 2)
            {
                ItemModels.Part(PrimitiveType.Capsule, root, new Vector3(-w * 0.28f + (side * 0.25f), 0.42f, f.Yard), new Vector3(0.2f, 0.18f, 0.18f), side < 0 ? new Color(0.35f, 0.5f, 0.75f) : new Color(0.6f, 0.35f, 0.3f));
                ItemModels.Part(PrimitiveType.Sphere, root, new Vector3(-w * 0.28f + (side * 0.25f), 0.68f, f.Yard), new Vector3(0.16f, 0.16f, 0.16f), new Color(0.95f, 0.8f, 0.65f));
            }
            Pot(root, new Vector3(w * 0.32f, 0f, f.Yard), 1f);
            height = f.Top + 0.6f;
            return root;
        }

        /// <summary>세탁소: 하늘색 벽, 지붕에 거대 옷걸이와 배기구 둘, 마당에 비닐 씌운 옷이 걸린 옷걸이대 둘과 빨래 수레.</summary>
        private static GameObject Laundry(Transform parent, float w, float h, out float height)
        {
            GameObject root = ItemModels.Root("Laundry", parent);
            ShopFrame f = ShopBody(root, w, h, new Color(0.62f, 0.8f, 0.93f), new Color(0.42f, 0.56f, 0.7f), Paint, 1.3f, new Color(0.25f, 0.5f, 0.85f), Paint);
            // 거대 옷걸이(지붕에 눕는다): 가로대 + 비스듬한 어깨 둘 + 고리.
            var c = new Vector3(-w * 0.1f, f.Top + 0.08f, f.Back);
            ItemModels.Part(PrimitiveType.Cube, root, c + new Vector3(0f, 0f, 0.35f), new Vector3(1.6f, 0.08f, 0.08f), Steel);
            for (int side = -1; side <= 1; side += 2) ItemModels.Part(PrimitiveType.Cube, root, c + new Vector3(side * 0.4f, 0f, 0.05f), new Vector3(0.95f, 0.08f, 0.08f), Steel, new Vector3(0f, side * 38f, 0f));
            ItemModels.Part(PrimitiveType.Cylinder, root, c + new Vector3(0f, 0f, -0.35f), new Vector3(0.06f, 0.15f, 0.06f), Steel, new Vector3(90f, 0f, 0f));
            ItemModels.Part(PrimitiveType.Cylinder, root, c + new Vector3(0.08f, 0f, -0.52f), new Vector3(0.06f, 0.08f, 0.06f), Steel, new Vector3(90f, 40f, 0f));
            for (int k = 0; k < 2; k++) ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(w * (0.25f + (k * 0.13f)), f.Top + 0.22f, f.Back), new Vector3(0.2f, 0.22f, 0.2f), Steel);
            // 옷걸이대 둘: 기둥 둘 + 봉 + 셔츠 넷(색 판) 위에 비닐(흰 판).
            Color[] shirts = { Red, new Color(0.3f, 0.55f, 0.95f), new Color(0.98f, 0.82f, 0.2f), new Color(0.3f, 0.3f, 0.35f) };
            for (int side = -1; side <= 1; side += 2)
            {
                float rx = side * w * 0.3f;
                for (int e = -1; e <= 1; e += 2) ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(rx + (e * 0.5f), 0.45f, f.Yard), new Vector3(0.05f, 0.45f, 0.05f), Steel);
                ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(rx, 0.88f, f.Yard), new Vector3(0.04f, 0.52f, 0.04f), Steel, new Vector3(0f, 0f, 90f));
                for (int k = 0; k < 4; k++)
                {
                    float x = rx + ((k - 1.5f) * 0.22f);
                    ItemModels.Part(PrimitiveType.Cube, root, new Vector3(x, 0.58f, f.Yard), new Vector3(0.18f, 0.55f, 0.06f), shirts[(k + side + 2) % shirts.Length]);
                    ItemModels.Part(PrimitiveType.Cube, root, new Vector3(x, 0.55f, f.Yard + 0.03f), new Vector3(0.2f, 0.62f, 0.04f), new Color(0.88f, 0.92f, 0.95f));
                }
            }
            height = f.Top + 0.45f;
            return root;
        }

        /// <summary>분식집: 노란 벽에 빨강·노랑 차양, 지붕에 거대 김밥 세 조각, 마당에 떡볶이 포장마차와 플라스틱 의자.</summary>
        private static GameObject SnackBar(Transform parent, float w, float h, out float height)
        {
            GameObject root = ItemModels.Root("SnackBar", parent);
            ShopFrame f = ShopBody(root, w, h, new Color(0.95f, 0.78f, 0.25f), new Color(0.75f, 0.25f, 0.18f), Red, 1.3f, Red, new Color(0.98f, 0.82f, 0.2f));
            // 거대 김밥 조각 셋: 김(검정) + 밥(흰) + 가운데 단무지·당근·시금치.
            for (int k = 0; k < 3; k++)
            {
                var c = new Vector3((k - 1) * 0.95f, f.Top + 0.1f + (k == 1 ? 0.05f : 0f), f.Back);
                ItemModels.Part(PrimitiveType.Cylinder, root, c, new Vector3(0.8f, 0.1f, 0.8f), new Color(0.1f, 0.12f, 0.1f));
                ItemModels.Part(PrimitiveType.Cylinder, root, c + new Vector3(0f, 0.02f, 0f), new Vector3(0.68f, 0.1f, 0.68f), Paint);
                ItemModels.Part(PrimitiveType.Cube, root, c + new Vector3(-0.1f, 0.1f, 0f), new Vector3(0.12f, 0.04f, 0.12f), new Color(0.98f, 0.85f, 0.2f));
                ItemModels.Part(PrimitiveType.Cube, root, c + new Vector3(0.1f, 0.1f, 0.05f), new Vector3(0.1f, 0.04f, 0.1f), new Color(0.98f, 0.5f, 0.15f));
                ItemModels.Part(PrimitiveType.Cube, root, c + new Vector3(0f, 0.1f, -0.12f), new Vector3(0.12f, 0.04f, 0.08f), Leaf);
            }
            // 포장마차(왼쪽): 파란 수레 + 빨간 떡볶이 철판 + 흰 떡 + 어묵 솥.
            var cart = new Vector3(-w * 0.27f, 0f, f.Yard);
            ItemModels.Part(PrimitiveType.Cube, root, cart + new Vector3(0f, 0.3f, 0f), new Vector3(1.3f, 0.6f, 0.6f), new Color(0.2f, 0.4f, 0.75f));
            ItemModels.Part(PrimitiveType.Cylinder, root, cart + new Vector3(-0.25f, 0.62f, 0f), new Vector3(0.62f, 0.04f, 0.5f), Dark);
            ItemModels.Part(PrimitiveType.Cylinder, root, cart + new Vector3(-0.25f, 0.65f, 0f), new Vector3(0.55f, 0.03f, 0.43f), new Color(0.85f, 0.15f, 0.1f));
            for (int k = 0; k < 7; k++) ItemModels.Part(PrimitiveType.Capsule, root, cart + new Vector3(-0.25f + ((k % 4) - 1.5f) * 0.11f, 0.69f, (k / 4) * 0.12f - 0.06f), new Vector3(0.06f, 0.08f, 0.06f), Paint, new Vector3(90f, k * 25f, 0f));
            ItemModels.Part(PrimitiveType.Cube, root, cart + new Vector3(0.38f, 0.66f, 0f), new Vector3(0.42f, 0.12f, 0.4f), Steel);
            for (int k = 0; k < 4; k++) ItemModels.Part(PrimitiveType.Cylinder, root, cart + new Vector3(0.3f + (k % 2) * 0.16f, 0.8f, (k / 2) * 0.16f - 0.08f), new Vector3(0.02f, 0.14f, 0.02f), Wood, new Vector3(0f, 0f, 15f));
            // 플라스틱 의자 넷(오른쪽).
            for (int k = 0; k < 4; k++) ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(w * 0.15f + (k * 0.3f), 0.12f, f.Yard + ((k % 2) * 0.2f)), new Vector3(0.22f, 0.12f, 0.22f), k % 2 == 0 ? Red : new Color(0.2f, 0.4f, 0.85f));
            height = f.Top + 0.3f;
            return root;
        }

        /// <summary>카페: 짙은 나무 벽에 큰 창, 지붕에 거대 커피잔(라테 하트), 마당에 크림 파라솔 테라스와 화분.</summary>
        private static GameObject Cafe(Transform parent, float w, float h, out float height)
        {
            GameObject root = ItemModels.Root("Cafe", parent);
            ShopFrame f = ShopBody(root, w, h, new Color(0.45f, 0.3f, 0.2f), new Color(0.3f, 0.22f, 0.16f), Cream, 1.35f);
            // 거대 커피잔: 받침 + 흰 잔 + 커피 + 라테 하트 + 손잡이.
            var c = new Vector3(-w * 0.05f, f.Top, f.Back);
            ItemModels.Part(PrimitiveType.Cylinder, root, c + new Vector3(0f, 0.04f, 0f), new Vector3(1.3f, 0.04f, 1.1f), Paint);
            ItemModels.Part(PrimitiveType.Cylinder, root, c + new Vector3(0f, 0.32f, 0f), new Vector3(0.85f, 0.28f, 0.8f), Paint);
            ItemModels.Part(PrimitiveType.Cylinder, root, c + new Vector3(0f, 0.6f, 0f), new Vector3(0.72f, 0.01f, 0.68f), new Color(0.45f, 0.27f, 0.15f));
            for (int side = -1; side <= 1; side += 2) ItemModels.Part(PrimitiveType.Sphere, root, c + new Vector3(side * 0.07f, 0.62f, 0.04f), new Vector3(0.18f, 0.02f, 0.16f), Cream, new Vector3(0f, side * 35f, 0f));
            ItemModels.Part(PrimitiveType.Sphere, root, c + new Vector3(0f, 0.62f, -0.08f), new Vector3(0.12f, 0.02f, 0.12f), Cream);
            ItemModels.Part(PrimitiveType.Cube, root, c + new Vector3(0.5f, 0.35f, 0f), new Vector3(0.2f, 0.28f, 0.08f), Paint);
            // 테라스 둘 + 화분 둘.
            ParasolTable(root, new Vector3(-w * 0.32f, 0f, f.Yard), Cream, new Color(0.45f, 0.3f, 0.2f));
            ParasolTable(root, new Vector3(w * 0.28f, 0f, f.Yard), Cream, new Color(0.45f, 0.3f, 0.2f));
            Pot(root, new Vector3(-0.45f, 0f, f.Front + 0.2f), 1.1f);
            Pot(root, new Vector3(0.45f, 0f, f.Front + 0.2f), 1.1f);
            height = f.Top + 0.65f;
            return root;
        }

        /// <summary>서점: 짙은 초록 벽에 금빛 간판, 지붕에 거대 펼친 책, 마당에 책등이 줄지은 책 수레 둘.</summary>
        private static GameObject Bookstore(Transform parent, float w, float h, out float height)
        {
            GameObject root = ItemModels.Root("Bookstore", parent);
            ShopFrame f = ShopBody(root, w, h, new Color(0.2f, 0.42f, 0.3f), new Color(0.25f, 0.3f, 0.28f), new Color(0.85f, 0.7f, 0.3f), 1.4f);
            // 거대 펼친 책: 빨간 표지 두 장 위에 흰 쪽 두 장(바깥이 살짝 들린다), 가운데 골, 글줄.
            var c = new Vector3(0f, f.Top + 0.1f, f.Back);
            for (int side = -1; side <= 1; side += 2)
            {
                ItemModels.Part(PrimitiveType.Cube, root, c + new Vector3(side * 0.55f, 0f, 0f), new Vector3(1.1f, 0.05f, 0.95f), new Color(0.7f, 0.15f, 0.15f), new Vector3(0f, 0f, side * 8f));
                ItemModels.Part(PrimitiveType.Cube, root, c + new Vector3(side * 0.52f, 0.05f, 0f), new Vector3(1.0f, 0.06f, 0.85f), Paint, new Vector3(0f, 0f, side * 8f));
                for (int k = 0; k < 4; k++) ItemModels.Part(PrimitiveType.Cube, root, c + new Vector3(side * 0.52f, 0.1f + (0.07f * 0.5f), -0.28f + (k * 0.18f)), new Vector3(0.75f, 0.01f, 0.04f), new Color(0.55f, 0.55f, 0.6f), new Vector3(0f, 0f, side * 8f));
            }
            ItemModels.Part(PrimitiveType.Cylinder, root, c + new Vector3(0f, 0.02f, 0f), new Vector3(0.06f, 0.48f, 0.06f), new Color(0.5f, 0.1f, 0.1f), new Vector3(90f, 0f, 0f));
            // 책 수레 둘: 나무 상자 + 책등 줄(색 얇은 판).
            Color[] spines = { Red, new Color(0.2f, 0.4f, 0.8f), new Color(0.95f, 0.8f, 0.25f), new Color(0.25f, 0.6f, 0.35f), Paint, new Color(0.55f, 0.3f, 0.6f) };
            for (int side = -1; side <= 1; side += 2)
            {
                var cart = new Vector3(side * w * 0.27f, 0f, f.Yard);
                ItemModels.Part(PrimitiveType.Cube, root, cart + new Vector3(0f, 0.25f, 0f), new Vector3(1.1f, 0.3f, 0.55f), Wood);
                for (int e = -1; e <= 1; e += 2) ItemModels.Part(PrimitiveType.Cylinder, root, cart + new Vector3(e * 0.4f, 0.08f, 0.28f), new Vector3(0.16f, 0.03f, 0.16f), Dark, new Vector3(90f, 0f, 0f));
                for (int row = 0; row < 2; row++)
                {
                    for (int k = 0; k < 9; k++) ItemModels.Part(PrimitiveType.Cube, root, cart + new Vector3((k - 4f) * 0.115f, 0.5f, (row - 0.5f) * 0.24f), new Vector3(0.09f, 0.22f + (0.04f * ((k + row) % 3)), 0.2f), spines[(k + (row * 3)) % spines.Length]);
                }
            }
            height = f.Top + 0.25f;
            return root;
        }

        /// <summary>정육점: 흰 타일 벽에 빨강·흰 차양, 지붕에 거대 뼈다귀 고기와 냉각기, 마당에 고기가 든 유리 진열장.</summary>
        private static GameObject Butcher(Transform parent, float w, float h, out float height)
        {
            GameObject root = ItemModels.Root("Butcher", parent);
            ShopFrame f = ShopBody(root, w, h, new Color(0.93f, 0.93f, 0.92f), new Color(0.65f, 0.2f, 0.2f), Red, 1.3f, Red, Paint);
            // 거대 뼈다귀 고기: 붉은 살 + 흰 비계 테 + 양끝 뼈 혹.
            var c = new Vector3(-w * 0.08f, f.Top + 0.22f, f.Back);
            ItemModels.Part(PrimitiveType.Sphere, root, c, new Vector3(1.3f, 0.36f, 0.95f), new Color(0.98f, 0.85f, 0.8f));
            ItemModels.Part(PrimitiveType.Sphere, root, c + new Vector3(0f, 0.05f, 0f), new Vector3(1.15f, 0.34f, 0.82f), new Color(0.75f, 0.15f, 0.15f));
            ItemModels.Part(PrimitiveType.Capsule, root, c + new Vector3(0f, 0.06f, 0f), new Vector3(0.18f, 1.0f, 0.18f), Bone, new Vector3(0f, 0f, 90f));
            for (int side = -1; side <= 1; side += 2)
            {
                ItemModels.Part(PrimitiveType.Sphere, root, c + new Vector3(side * 1.0f, 0.06f, 0.08f), new Vector3(0.22f, 0.2f, 0.22f), Bone);
                ItemModels.Part(PrimitiveType.Sphere, root, c + new Vector3(side * 1.0f, 0.06f, -0.08f), new Vector3(0.22f, 0.2f, 0.22f), Bone);
            }
            AirUnit(root, new Vector3(w * 0.36f, f.Top, f.Mid - (f.Depth * 0.3f)));
            // 유리 진열장: 강철 상자 + 유리 윗면 아래 고기 판 여섯.
            var case0 = new Vector3(-w * 0.18f, 0f, f.Yard);
            ItemModels.Part(PrimitiveType.Cube, root, case0 + new Vector3(0f, 0.25f, 0f), new Vector3(1.5f, 0.5f, 0.55f), Steel);
            ItemModels.Part(PrimitiveType.Cube, root, case0 + new Vector3(0f, 0.505f, 0f), new Vector3(1.4f, 0.01f, 0.47f), Paint);
            for (int k = 0; k < 6; k++) ItemModels.Part(PrimitiveType.Cube, root, case0 + new Vector3(((k % 3) - 1) * 0.42f, 0.53f, ((k / 3) - 0.5f) * 0.22f), new Vector3(0.34f, 0.04f, 0.17f), k % 2 == 0 ? new Color(0.8f, 0.2f, 0.2f) : new Color(0.95f, 0.55f, 0.55f));
            ItemModels.Part(PrimitiveType.Cube, root, case0 + new Vector3(0f, 0.58f, 0f), new Vector3(1.45f, 0.02f, 0.5f), new Color(0.75f, 0.92f, 1f));
            SandwichBoard(root, new Vector3(w * 0.33f, 0f, f.Yard), Red);
            height = f.Top + 0.45f;
            return root;
        }

        /// <summary>미용실: 분홍 벽, 지붕에 거대 가위, 문 옆 분홍 회전등, 마당에 꽃 상자와 입간판.</summary>
        private static GameObject Salon(Transform parent, float w, float h, out float height)
        {
            GameObject root = ItemModels.Root("Salon", parent);
            var pink = new Color(0.95f, 0.5f, 0.72f);
            ShopFrame f = ShopBody(root, w, h, new Color(0.96f, 0.74f, 0.83f), new Color(0.8f, 0.45f, 0.6f), Paint, 1.3f, pink, Paint);
            // 거대 가위: 날 둘(강철, ±18°로 엇갈림) + 손잡이 고리 둘(분홍 테 + 지붕색 구멍) + 가운데 나사.
            var c = new Vector3(0f, f.Top + 0.1f, f.Back);
            for (int side = -1; side <= 1; side += 2)
            {
                ItemModels.Part(PrimitiveType.Cube, root, c + new Vector3(0.4f, 0.02f * side, side * 0.04f), new Vector3(1.4f, 0.05f, 0.16f), new Color(0.82f, 0.84f, 0.88f), new Vector3(0f, side * 18f, 0f));
                var ring = c + new Vector3(-0.6f, 0f, side * 0.32f);
                ItemModels.Part(PrimitiveType.Cylinder, root, ring, new Vector3(0.48f, 0.04f, 0.4f), pink);
                ItemModels.Part(PrimitiveType.Cylinder, root, ring + new Vector3(0f, 0.01f, 0f), new Vector3(0.28f, 0.04f, 0.22f), new Color(0.8f, 0.45f, 0.6f));
            }
            ItemModels.Part(PrimitiveType.Cylinder, root, c + new Vector3(-0.2f, 0.04f, 0f), new Vector3(0.12f, 0.04f, 0.12f), Dark);
            BarberPole(root, new Vector3(0.45f, 0f, f.Front + 0.18f), 0.7f, pink, Paint);
            // 꽃 상자(창 밑 길게) + 입간판.
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(-w * 0.27f, 0.12f, f.Front + 0.15f), new Vector3(w * 0.3f, 0.24f, 0.22f), Paint);
            for (int k = 0; k < 5; k++) ItemModels.Part(PrimitiveType.Sphere, root, new Vector3(-w * 0.27f + ((k - 2) * 0.22f), 0.28f, f.Front + 0.15f), new Vector3(0.2f, 0.16f, 0.2f), k % 2 == 0 ? pink : Leaf);
            SandwichBoard(root, new Vector3(-w * 0.25f, 0f, f.Yard + 0.2f), pink);
            Pot(root, new Vector3(w * 0.33f, 0f, f.Yard), 1.2f);
            height = f.Top + 0.75f;
            return root;
        }

        /// <summary>철물점: 강철 회색 벽에 노란 간판, 지붕에 거대 망치, 마당에 기댄 사다리·양동이 탑·호스 릴·목재 더미·손수레.</summary>
        private static GameObject Hardware(Transform parent, float w, float h, out float height)
        {
            GameObject root = ItemModels.Root("Hardware", parent);
            var yellow = new Color(0.98f, 0.8f, 0.15f);
            ShopFrame f = ShopBody(root, w, h, new Color(0.55f, 0.58f, 0.62f), new Color(0.4f, 0.42f, 0.45f), yellow, 1.35f);
            // 거대 망치: 나무 자루(대각) + 검은 쇠머리 + 은빛 타격면.
            var c = new Vector3(0f, f.Top + 0.12f, f.Back);
            ItemModels.Part(PrimitiveType.Cube, root, c, new Vector3(1.9f, 0.14f, 0.2f), Wood, new Vector3(0f, 18f, 0f));
            var head = c + new Vector3(0.85f, 0.06f, -0.28f);
            ItemModels.Part(PrimitiveType.Cube, root, head, new Vector3(0.36f, 0.3f, 0.85f), new Color(0.25f, 0.26f, 0.3f), new Vector3(0f, 18f, 0f));
            ItemModels.Part(PrimitiveType.Cylinder, root, head + new Vector3(-0.13f, 0f, 0.42f), new Vector3(0.3f, 0.06f, 0.3f), new Color(0.75f, 0.78f, 0.82f), new Vector3(90f, 18f, 0f));
            // 기댄 사다리 둘(앞벽에 기댐, 앞으로 15° 기운다).
            for (int k = 0; k < 2; k++)
            {
                float x = -w * 0.4f + (k * 0.55f);
                for (int e = -1; e <= 1; e += 2) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(x + (e * 0.16f), 0.6f, f.Front + 0.18f), new Vector3(0.05f, 1.2f, 0.05f), k == 0 ? new Color(0.8f, 0.6f, 0.3f) : Steel, new Vector3(-15f, 0f, 0f));
                for (int r = 0; r < 4; r++) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(x, 0.2f + (r * 0.28f), f.Front + 0.28f - (r * 0.075f)), new Vector3(0.32f, 0.04f, 0.04f), k == 0 ? new Color(0.8f, 0.6f, 0.3f) : Steel);
            }
            // 양동이 탑(셋 겹친 색 원통) + 호스 릴(초록 원반) + 목재 더미 + 손수레.
            Color[] pails = { Red, new Color(0.2f, 0.45f, 0.85f), yellow };
            for (int k = 0; k < 3; k++) ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(w * 0.08f + 0.35f, 0.12f + (k * 0.12f), f.Yard), new Vector3(0.36f - (k * 0.02f), 0.1f, 0.36f - (k * 0.02f)), pails[k]);
            ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(w * 0.2f + 0.35f, 0.3f, f.Yard + 0.1f), new Vector3(0.45f, 0.08f, 0.45f), new Color(0.2f, 0.6f, 0.3f), new Vector3(90f, 0f, 0f));
            ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(w * 0.2f + 0.35f, 0.3f, f.Yard + 0.1f), new Vector3(0.18f, 0.1f, 0.18f), Dark, new Vector3(90f, 0f, 0f));
            for (int k = 0; k < 4; k++) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(w * 0.36f, 0.05f + (k * 0.09f), f.Yard + ((k % 2) * 0.05f)), new Vector3(0.45f, 0.08f, 0.8f), new Color(0.85f, 0.68f, 0.45f));
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(-w * 0.08f, 0.28f, f.Yard + 0.15f), new Vector3(0.45f, 0.2f, 0.55f), new Color(0.25f, 0.55f, 0.3f), new Vector3(10f, 0f, 0f));
            ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(-w * 0.08f, 0.12f, f.Yard + 0.45f), new Vector3(0.24f, 0.04f, 0.24f), Dark, new Vector3(0f, 0f, 90f));
            height = f.Top + 0.3f;
            return root;
        }

        /// <summary>치킨집: 주황 벽, 지붕에 거대 닭다리, 마당에 배달 오토바이 둘과 플라스틱 탁자·맥주 상자.</summary>
        private static GameObject ChickenShop(Transform parent, float w, float h, out float height)
        {
            GameObject root = ItemModels.Root("ChickenShop", parent);
            ShopFrame f = ShopBody(root, w, h, new Color(0.96f, 0.62f, 0.22f), new Color(0.72f, 0.33f, 0.12f), new Color(0.98f, 0.85f, 0.2f), 1.3f, Red, new Color(0.98f, 0.85f, 0.2f));
            // 거대 닭다리: 튀김옷 살(두툼한 타원 둘) + 뼈 + 끝 혹 둘.
            var c = new Vector3(-w * 0.12f, f.Top + 0.25f, f.Back);
            ItemModels.Part(PrimitiveType.Sphere, root, c, new Vector3(1.15f, 0.48f, 0.8f), new Color(0.78f, 0.45f, 0.15f), new Vector3(0f, -12f, 0f));
            ItemModels.Part(PrimitiveType.Sphere, root, c + new Vector3(-0.12f, 0.1f, -0.05f), new Vector3(0.8f, 0.36f, 0.6f), new Color(0.88f, 0.58f, 0.22f), new Vector3(0f, -12f, 0f));
            ItemModels.Part(PrimitiveType.Capsule, root, c + new Vector3(0.75f, -0.02f, 0.15f), new Vector3(0.16f, 0.4f, 0.16f), Bone, new Vector3(0f, -12f, 90f));
            ItemModels.Part(PrimitiveType.Sphere, root, c + new Vector3(1.12f, -0.02f, 0.28f), new Vector3(0.2f, 0.18f, 0.2f), Bone);
            ItemModels.Part(PrimitiveType.Sphere, root, c + new Vector3(1.15f, -0.02f, 0.1f), new Vector3(0.2f, 0.18f, 0.2f), Bone);
            // 배달 오토바이 둘(왼쪽): 빨간 몸 + 검은 안장 + 흰 배달통 + 바퀴.
            for (int k = 0; k < 2; k++)
            {
                var m = new Vector3(-w * 0.38f + (k * 0.5f), 0f, f.Yard);
                ItemModels.Part(PrimitiveType.Cube, root, m + new Vector3(0f, 0.28f, 0f), new Vector3(0.24f, 0.22f, 0.7f), Red);
                ItemModels.Part(PrimitiveType.Cube, root, m + new Vector3(0f, 0.42f, -0.05f), new Vector3(0.2f, 0.06f, 0.3f), Dark);
                ItemModels.Part(PrimitiveType.Cube, root, m + new Vector3(0f, 0.55f, -0.28f), new Vector3(0.36f, 0.32f, 0.32f), Paint);
                ItemModels.Part(PrimitiveType.Cube, root, m + new Vector3(0f, 0.6f, -0.28f), new Vector3(0.37f, 0.08f, 0.33f), Red);
                ItemModels.Part(PrimitiveType.Cube, root, m + new Vector3(0f, 0.55f, 0.32f), new Vector3(0.36f, 0.04f, 0.04f), Dark);
                for (int e = -1; e <= 1; e += 2) ItemModels.Part(PrimitiveType.Cylinder, root, m + new Vector3(0f, 0.13f, e * 0.28f), new Vector3(0.26f, 0.04f, 0.26f), Dark, new Vector3(0f, 0f, 90f));
            }
            // 플라스틱 탁자 + 의자 + 초록 맥주 상자(오른쪽).
            var t = new Vector3(w * 0.25f, 0f, f.Yard);
            ItemModels.Part(PrimitiveType.Cube, root, t + new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.04f, 0.45f), Red);
            ItemModels.Part(PrimitiveType.Cube, root, t + new Vector3(0f, 0.15f, 0f), new Vector3(0.08f, 0.3f, 0.08f), Red);
            for (int side = -1; side <= 1; side += 2) ItemModels.Part(PrimitiveType.Cylinder, root, t + new Vector3(side * 0.45f, 0.12f, 0f), new Vector3(0.22f, 0.12f, 0.22f), Red);
            for (int k = 0; k < 2; k++) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(w * 0.42f, 0.1f + (k * 0.18f), f.Yard - 0.1f), new Vector3(0.36f, 0.16f, 0.28f), new Color(0.15f, 0.5f, 0.25f));
            height = f.Top + 0.6f;
            return root;
        }

        /// <summary>물류창고: 넓은 회청 창고 + 지붕 채광창 줄·환풍기, 앞에 셔터 셋과 하역장, 마당에 박스 팔레트 셋·지게차.</summary>
        private static GameObject Depot(Transform parent, float w, float h, out float height)
        {
            GameObject root = ItemModels.Root("TownDepot", parent);
            const float wallTop = 1.7f;
            float depth = h * 0.7f;
            float mid = -(h - depth) / 2f;
            float front = mid + (depth / 2f);
            var wall = new Color(0.5f, 0.56f, 0.62f);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, wallTop / 2f, mid), new Vector3(w * 0.95f, wallTop, depth), wall);
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, wallTop + 0.04f, mid), new Vector3(w * 0.97f, 0.08f, depth + 0.06f), new Color(0.72f, 0.74f, 0.76f));
            for (int k = -1; k <= 1; k++)
            {
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(k * w * 0.3f, wallTop + 0.1f, mid), new Vector3(0.5f, 0.06f, depth * 0.8f), new Color(0.55f, 0.82f, 0.95f));
                ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3((k * w * 0.3f) + (w * 0.15f), wallTop + 0.2f, mid - (depth * 0.2f)), new Vector3(0.4f, 0.12f, 0.4f), Steel);
            }
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, wallTop - 0.2f, front + 0.03f), new Vector3(w * 0.6f, 0.3f, 0.05f), new Color(0.95f, 0.75f, 0.15f));
            // 하역장 + 셔터 셋(가로 줄).
            ItemModels.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.18f, front + 0.25f), new Vector3(w * 0.85f, 0.36f, 0.5f), Dark);
            for (int k = -1; k <= 1; k++)
            {
                ItemModels.Part(PrimitiveType.Cube, root, new Vector3(k * w * 0.28f, 0.75f, front + 0.02f), new Vector3(w * 0.22f, 1.0f, 0.04f), Steel);
                for (int r = 0; r < 4; r++) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(k * w * 0.28f, 0.35f + (r * 0.22f), front + 0.05f), new Vector3(w * 0.22f, 0.02f, 0.02f), Steel * 0.75f);
            }
            // 박스 팔레트 셋 + 지게차.
            var cardboard = new Color(0.78f, 0.6f, 0.38f);
            float yard = front + ((h / 2f - front) * 0.6f);
            for (int k = 0; k < 3; k++)
            {
                var p = new Vector3(-w * 0.38f + (k * 0.85f), 0f, yard);
                ItemModels.Part(PrimitiveType.Cube, root, p + new Vector3(0f, 0.05f, 0f), new Vector3(0.75f, 0.1f, 0.6f), Wood);
                for (int j = 0; j < 2 + (k % 2); j++) ItemModels.Part(PrimitiveType.Cube, root, p + new Vector3(((j % 2) - 0.5f) * 0.32f, 0.22f + ((j / 2) * 0.3f), 0f), new Vector3(0.3f, 0.28f, 0.5f), j % 2 == 0 ? cardboard : cardboard * 0.9f);
            }
            var lift = new Vector3(w * 0.3f, 0f, yard);
            var yellow = new Color(0.98f, 0.75f, 0.12f);
            ItemModels.Part(PrimitiveType.Cube, root, lift + new Vector3(0f, 0.3f, 0f), new Vector3(0.55f, 0.36f, 0.8f), yellow);
            ItemModels.Part(PrimitiveType.Cube, root, lift + new Vector3(0f, 0.68f, -0.15f), new Vector3(0.5f, 0.05f, 0.45f), Dark);
            for (int e = -1; e <= 1; e += 2) ItemModels.Part(PrimitiveType.Cube, root, lift + new Vector3(e * 0.18f, 0.45f, -0.42f), new Vector3(0.05f, 0.9f, 0.05f), Dark);
            for (int e = -1; e <= 1; e += 2) ItemModels.Part(PrimitiveType.Cube, root, lift + new Vector3(e * 0.12f, 0.05f, -0.65f), new Vector3(0.08f, 0.04f, 0.45f), Steel);
            height = wallTop + 0.35f;
            return root;
        }
    }
}
