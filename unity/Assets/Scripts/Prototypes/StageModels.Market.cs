using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 야시장 점포 좌판 물건. 열두 점포가 색만 다른 천막에 아무 색 상자 셋이라 "과일가게인데 과일이 안 보인다"(사용자 지적).
    /// 좌판(폭 w·깊이 0.8, 윗면 y)에 점포 이름에 맞는 물건을 올리고, 몇 점포는 차양 위에 큰 상징물을 하나 둔다.
    /// 축은 StageModels와 같다(위 +Y, 앞 +Z). 김·연기가 나는 점포는 SteamAt이 그 자리를 알려 준다(뷰가 뿜는다).
    /// </summary>
    public static partial class StageModels
    {
        private static readonly Color Apple = new Color(0.85f, 0.12f, 0.12f);
        private static readonly Color Orange = new Color(0.98f, 0.55f, 0.1f);
        private static readonly Color Banana = new Color(0.98f, 0.85f, 0.25f);
        private static readonly Color Melon = new Color(0.15f, 0.5f, 0.2f);
        private static readonly Color Grape = new Color(0.45f, 0.2f, 0.6f);
        private static readonly Color Griddle = new Color(0.16f, 0.16f, 0.18f);
        private static readonly Color Sauce = new Color(0.85f, 0.15f, 0.08f);
        private static readonly Color Broth = new Color(0.75f, 0.55f, 0.3f);
        private static readonly Color Ember = new Color(1f, 0.45f, 0.1f);

        /// <summary>좌판 위 물건. y = 좌판 윗면, z = 좌판 가운데. 좌판은 x ±0.45w, z ±0.38.</summary>
        private static void Goods(GameObject root, string name, float w, float y, float z)
        {
            float half = w * 0.45f;
            switch (name)
            {
                case "과일 노점":
                    Fruit(root, half, y, z);
                    break;
                case "호떡집":
                {
                    Plate(root, new Vector3(-half * 0.35f, y, z), half * 1.1f, 0.6f, Griddle);
                    for (int k = 0; k < 8; k++) ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(-half * 0.35f + (((k % 4) - 1.5f) * 0.24f), y + 0.05f, z + (((k / 4) - 0.5f) * 0.26f)), new Vector3(0.2f, 0.02f, 0.2f), k % 3 == 0 ? new Color(0.62f, 0.38f, 0.18f) : Crust);
                    Stack(root, new Vector3(half * 0.6f, y, z), 0.24f, 5, Paint);   // 종이컵 묶음
                    break;
                }
                case "떡볶이":
                {
                    Plate(root, new Vector3(-half * 0.2f, y, z), half * 1.3f, 0.66f, Griddle);
                    ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(-half * 0.2f, y + 0.045f, z), new Vector3(half * 1.2f, 0.01f, 0.58f), Sauce);
                    for (int k = 0; k < 14; k++) ItemModels.Part(PrimitiveType.Capsule, root, new Vector3(-half * 0.2f + (((k % 7) - 3f) * 0.13f), y + 0.07f, z + (((k / 7) - 0.5f) * 0.2f)), new Vector3(0.06f, 0.08f, 0.06f), new Color(0.98f, 0.92f, 0.85f), new Vector3(90f, k * 37f, 0f));
                    Skewers(root, new Vector3(half * 0.75f, y, z), 4, new Color(0.88f, 0.75f, 0.5f));
                    break;
                }
                case "전집":
                {
                    Plate(root, new Vector3(-half * 0.3f, y, z), half * 1.1f, 0.6f, Griddle);
                    for (int k = 0; k < 4; k++) ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(-half * 0.3f + (((k % 2) - 0.5f) * 0.42f), y + 0.05f, z + (((k / 2) - 0.5f) * 0.28f)), new Vector3(0.34f, 0.015f, 0.26f), new Color(0.95f, 0.78f, 0.3f));
                    for (int k = 0; k < 3; k++) ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(half * 0.55f + (k * 0.13f), y + 0.16f, z - 0.1f + (k % 2) * 0.15f), new Vector3(0.1f, 0.16f, 0.1f), Paint);   // 막걸리 병
                    break;
                }
                case "어묵 바":
                {
                    var pot = new Vector3(-half * 0.15f, y, z);
                    ItemModels.Part(PrimitiveType.Cube, root, pot + new Vector3(0f, 0.1f, 0f), new Vector3(half * 1.3f, 0.2f, 0.62f), Steel);
                    ItemModels.Part(PrimitiveType.Cube, root, pot + new Vector3(0f, 0.2f, 0f), new Vector3(half * 1.2f, 0.01f, 0.54f), Broth);
                    for (int k = 0; k < 12; k++)
                    {
                        var at = pot + new Vector3((((k % 6) - 2.5f) * 0.18f), 0.32f, (((k / 6) - 0.5f) * 0.24f));
                        ItemModels.Part(PrimitiveType.Cube, root, at, new Vector3(0.12f, 0.18f, 0.04f), new Color(0.88f, 0.72f, 0.45f), new Vector3(0f, 0f, 8f));
                        ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.14f, 0f), new Vector3(0.015f, 0.08f, 0.015f), Wood);
                    }
                    Stack(root, new Vector3(half * 0.8f, y, z), 0.2f, 4, Paint);
                    break;
                }
                case "꼬치구이":
                {
                    var grill = new Vector3(-half * 0.1f, y, z);
                    ItemModels.Part(PrimitiveType.Cube, root, grill + new Vector3(0f, 0.08f, 0f), new Vector3(half * 1.5f, 0.16f, 0.5f), Griddle);
                    ItemModels.Part(PrimitiveType.Cube, root, grill + new Vector3(0f, 0.165f, 0f), new Vector3(half * 1.4f, 0.01f, 0.42f), Ember);
                    for (int k = 0; k < 8; k++)
                    {
                        float x = ((k - 3.5f) * 0.17f);
                        ItemModels.Part(PrimitiveType.Cylinder, root, grill + new Vector3(x, 0.2f, 0f), new Vector3(0.015f, 0.3f, 0.015f), Wood, new Vector3(90f, 0f, 0f));
                        for (int j = 0; j < 3; j++) ItemModels.Part(PrimitiveType.Cube, root, grill + new Vector3(x, 0.22f, (j - 1) * 0.12f), new Vector3(0.09f, 0.08f, 0.09f), j == 1 ? new Color(0.35f, 0.6f, 0.25f) : new Color(0.55f, 0.25f, 0.12f));
                    }
                    break;
                }
                case "분식집":
                {
                    // 김밥 줄(검은 원통 둘, 썬 단면 줄) + 순대 + 튀김 바구니.
                    for (int k = 0; k < 2; k++) ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(-half * 0.5f, y + 0.08f, z - 0.15f + (k * 0.3f)), new Vector3(0.16f, 0.4f, 0.16f), new Color(0.1f, 0.12f, 0.1f), new Vector3(0f, 0f, 90f));
                    for (int k = 0; k < 5; k++)
                    {
                        var at = new Vector3(half * 0.05f + (k * 0.17f), y + 0.03f, z - 0.15f);
                        ItemModels.Part(PrimitiveType.Cylinder, root, at, new Vector3(0.15f, 0.03f, 0.15f), new Color(0.1f, 0.12f, 0.1f));
                        ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.01f, 0f), new Vector3(0.12f, 0.03f, 0.12f), Paint);
                        ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.04f, 0f), new Vector3(0.04f, 0.01f, 0.04f), k % 2 == 0 ? Banana : Orange);
                    }
                    ItemModels.Part(PrimitiveType.Capsule, root, new Vector3(half * 0.3f, y + 0.06f, z + 0.18f), new Vector3(0.11f, 0.32f, 0.11f), new Color(0.3f, 0.12f, 0.12f), new Vector3(0f, 70f, 90f));
                    ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(half * 0.8f, y + 0.06f, z + 0.15f), new Vector3(0.36f, 0.06f, 0.3f), new Color(0.65f, 0.5f, 0.3f));
                    for (int k = 0; k < 4; k++) ItemModels.Part(PrimitiveType.Capsule, root, new Vector3(half * 0.8f + ((k - 1.5f) * 0.07f), y + 0.12f, z + 0.15f), new Vector3(0.07f, 0.1f, 0.07f), Crust, new Vector3(90f, k * 40f, 0f));
                    break;
                }
                case "솜사탕":
                {
                    // 솜사탕 기계(은 통) + 막대 꽂이에 분홍·하늘 솜 여섯.
                    var machine = new Vector3(-half * 0.55f, y, z);
                    ItemModels.Part(PrimitiveType.Cylinder, root, machine + new Vector3(0f, 0.12f, 0f), new Vector3(0.5f, 0.12f, 0.5f), Steel);
                    ItemModels.Part(PrimitiveType.Sphere, root, machine + new Vector3(0f, 0.28f, 0f), new Vector3(0.36f, 0.22f, 0.36f), new Color(1f, 0.75f, 0.85f));
                    ItemModels.Part(PrimitiveType.Cube, root, new Vector3(half * 0.3f, y + 0.05f, z), new Vector3(1.0f, 0.1f, 0.2f), Wood);
                    for (int k = 0; k < 6; k++)
                    {
                        var stick = new Vector3(half * 0.3f + ((k - 2.5f) * 0.17f), y + 0.1f, z + ((k % 2) * 0.1f) - 0.05f);
                        ItemModels.Part(PrimitiveType.Cylinder, root, stick + new Vector3(0f, 0.15f, 0f), new Vector3(0.015f, 0.15f, 0.015f), Paint);
                        ItemModels.Part(PrimitiveType.Sphere, root, stick + new Vector3(0f, 0.38f, 0f), new Vector3(0.24f, 0.22f, 0.24f), k % 2 == 0 ? new Color(1f, 0.6f, 0.8f) : new Color(0.6f, 0.85f, 1f));
                    }
                    break;
                }
                case "음료 노점":
                {
                    // 색 음료 컵 여덟(빨대) + 얼음 아이스박스.
                    Color[] drinks = { Apple, Banana, new Color(0.4f, 0.8f, 0.3f), Orange, new Color(0.55f, 0.3f, 0.75f), new Color(0.35f, 0.7f, 1f) };
                    for (int k = 0; k < 8; k++)
                    {
                        var cup = new Vector3(-half * 0.75f + ((k % 4) * 0.22f), y, z + (((k / 4) - 0.5f) * 0.28f));
                        ItemModels.Part(PrimitiveType.Cylinder, root, cup + new Vector3(0f, 0.12f, 0f), new Vector3(0.14f, 0.12f, 0.14f), new Color(0.88f, 0.95f, 1f));
                        ItemModels.Part(PrimitiveType.Cylinder, root, cup + new Vector3(0f, 0.2f, 0f), new Vector3(0.12f, 0.04f, 0.12f), drinks[k % drinks.Length]);
                        ItemModels.Part(PrimitiveType.Cylinder, root, cup + new Vector3(0.03f, 0.32f, 0f), new Vector3(0.015f, 0.1f, 0.015f), Paint, new Vector3(0f, 0f, 10f));
                    }
                    var box = new Vector3(half * 0.55f, y, z);
                    ItemModels.Part(PrimitiveType.Cube, root, box + new Vector3(0f, 0.12f, 0f), new Vector3(0.6f, 0.24f, 0.45f), new Color(0.2f, 0.45f, 0.85f));
                    ItemModels.Part(PrimitiveType.Cube, root, box + new Vector3(0f, 0.245f, 0f), new Vector3(0.52f, 0.01f, 0.37f), new Color(0.85f, 0.95f, 1f));
                    for (int k = 0; k < 4; k++) ItemModels.Part(PrimitiveType.Cylinder, root, box + new Vector3((k - 1.5f) * 0.12f, 0.3f, 0.05f), new Vector3(0.07f, 0.08f, 0.07f), k % 2 == 0 ? new Color(0.3f, 0.7f, 0.35f) : Apple);
                    break;
                }
                case "기념품":
                {
                    // 걸이대에 매달린 부채·열쇠고리 + 인형 셋.
                    ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(-half * 0.3f, y + 0.45f, z - 0.2f), new Vector3(0.03f, half * 0.6f, 0.03f), Steel, new Vector3(0f, 0f, 90f));
                    for (int side = -1; side <= 1; side += 2) ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(-half * 0.3f + (side * half * 0.6f), y + 0.22f, z - 0.2f), new Vector3(0.03f, 0.22f, 0.03f), Steel);
                    for (int k = 0; k < 6; k++) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(-half * 0.8f + (k * 0.2f), y + 0.3f, z - 0.2f), new Vector3(0.14f, 0.2f, 0.02f), StallPalette[k % StallPalette.Length], new Vector3(0f, 0f, (k % 2 == 0 ? 10f : -10f)));
                    for (int k = 0; k < 3; k++)
                    {
                        var doll = new Vector3(half * 0.25f + (k * 0.28f), y, z + 0.12f);
                        ItemModels.Part(PrimitiveType.Sphere, root, doll + new Vector3(0f, 0.12f, 0f), new Vector3(0.22f, 0.22f, 0.2f), StallPalette[(k + 2) % StallPalette.Length]);
                        ItemModels.Part(PrimitiveType.Sphere, root, doll + new Vector3(0f, 0.3f, 0f), new Vector3(0.17f, 0.16f, 0.16f), StallPalette[(k + 2) % StallPalette.Length]);
                        for (int e = -1; e <= 1; e += 2) ItemModels.Part(PrimitiveType.Sphere, root, doll + new Vector3(e * 0.06f, 0.4f, 0f), new Vector3(0.06f, 0.06f, 0.05f), StallPalette[(k + 2) % StallPalette.Length]);
                    }
                    break;
                }
                case "수공예":
                {
                    // 도자기 꽃병 넷(청자·백자·갈색) + 엮은 바구니 둘.
                    Color[] glaze = { new Color(0.55f, 0.75f, 0.7f), new Color(0.92f, 0.9f, 0.85f), new Color(0.6f, 0.38f, 0.25f), new Color(0.3f, 0.45f, 0.7f) };
                    for (int k = 0; k < 4; k++)
                    {
                        var vase = new Vector3(-half * 0.75f + (k * 0.3f), y, z + ((k % 2) * 0.14f) - 0.07f);
                        ItemModels.Part(PrimitiveType.Sphere, root, vase + new Vector3(0f, 0.14f, 0f), new Vector3(0.24f, 0.26f, 0.24f), glaze[k]);
                        ItemModels.Part(PrimitiveType.Cylinder, root, vase + new Vector3(0f, 0.32f, 0f), new Vector3(0.1f, 0.08f, 0.1f), glaze[k]);
                    }
                    for (int k = 0; k < 2; k++)
                    {
                        var basket = new Vector3(half * 0.45f + (k * 0.42f), y, z);
                        ItemModels.Part(PrimitiveType.Cylinder, root, basket + new Vector3(0f, 0.1f, 0f), new Vector3(0.36f, 0.1f, 0.36f), new Color(0.75f, 0.6f, 0.35f));
                        ItemModels.Part(PrimitiveType.Cylinder, root, basket + new Vector3(0f, 0.2f, 0f), new Vector3(0.3f, 0.01f, 0.3f), new Color(0.55f, 0.42f, 0.25f));
                    }
                    break;
                }
                case "잡화":
                {
                    // 모자 넷(챙 원반 + 둥근 머리) + 가방 셋 + 양말 묶음.
                    for (int k = 0; k < 4; k++)
                    {
                        var hat = new Vector3(-half * 0.75f + ((k % 2) * 0.36f), y, z + (((k / 2) - 0.5f) * 0.36f));
                        Color c = StallPalette[(k * 2 + 1) % StallPalette.Length];
                        ItemModels.Part(PrimitiveType.Cylinder, root, hat + new Vector3(0f, 0.02f, 0f), new Vector3(0.32f, 0.01f, 0.32f), c);
                        ItemModels.Part(PrimitiveType.Sphere, root, hat + new Vector3(0f, 0.06f, 0f), new Vector3(0.2f, 0.14f, 0.2f), c * 0.85f);
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        var bag = new Vector3(half * 0.05f + (k * 0.26f), y, z - 0.05f);
                        ItemModels.Part(PrimitiveType.Cube, root, bag + new Vector3(0f, 0.12f, 0f), new Vector3(0.2f, 0.22f, 0.1f), StallPalette[(k + 3) % StallPalette.Length]);
                        ItemModels.Part(PrimitiveType.Cube, root, bag + new Vector3(0f, 0.27f, 0f), new Vector3(0.12f, 0.06f, 0.02f), Dark);
                    }
                    for (int k = 0; k < 4; k++) ItemModels.Part(PrimitiveType.Capsule, root, new Vector3(half * 0.85f, y + 0.04f + (k * 0.06f), z + 0.15f), new Vector3(0.08f, 0.14f, 0.08f), k % 2 == 0 ? Paint : new Color(0.3f, 0.3f, 0.35f), new Vector3(0f, 0f, 90f));
                    break;
                }
                default:
                    for (int k = -1; k <= 1; k++) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(k * half * 0.55f, y + 0.12f, z), new Vector3(0.3f, 0.22f, 0.3f), StallPalette[(k + 4) % StallPalette.Length]);
                    break;
            }
        }

        /// <summary>과일 노점: 나무 상자 여섯(3×2)에 사과·귤·바나나·수박·포도·딸기.</summary>
        private static void Fruit(GameObject root, float half, float y, float z)
        {
            float cw = half * 2f / 3f;
            const float cd = 0.36f;
            for (int k = 0; k < 6; k++)
            {
                var c = new Vector3(-half + (cw * ((k % 3) + 0.5f)), y, z + (((k / 3) - 0.5f) * (cd + 0.03f)));
                ItemModels.Part(PrimitiveType.Cube, root, c + new Vector3(0f, 0.06f, 0f), new Vector3(cw - 0.05f, 0.12f, cd), Wood);
                ItemModels.Part(PrimitiveType.Cube, root, c + new Vector3(0f, 0.121f, 0f), new Vector3(cw - 0.11f, 0.01f, cd - 0.06f), DarkWood);
                float top = y + 0.13f;
                switch (k)
                {
                    case 0:
                        Pile(root, c, top, cw, cd, 0.15f, Apple, new Color(0.3f, 0.5f, 0.15f));
                        break;
                    case 1:
                        Pile(root, c, top, cw, cd, 0.15f, Orange, new Color(0.25f, 0.45f, 0.15f));
                        break;
                    case 2:
                        // 바나나 송이 셋: 굽은 노란 캡슐 셋이 부채꼴로.
                        for (int b = 0; b < 3; b++)
                        {
                            var at = c + new Vector3((b - 1) * cw * 0.28f, 0f, 0f);
                            for (int f = -1; f <= 1; f++) ItemModels.Part(PrimitiveType.Capsule, root, new Vector3(at.x + (f * 0.035f), top + 0.04f, at.z), new Vector3(0.07f, 0.14f, 0.07f), Banana, new Vector3(90f + (f * 14f), 0f, 20f + (f * 18f)));
                            ItemModels.Part(PrimitiveType.Sphere, root, new Vector3(at.x, top + 0.05f, at.z - 0.13f), new Vector3(0.05f, 0.05f, 0.05f), new Color(0.35f, 0.3f, 0.15f));
                        }
                        break;
                    case 3:
                        // 수박 둘: 짙은 초록 큰 구 + 연한 줄 띠.
                        for (int m = -1; m <= 1; m += 2)
                        {
                            var at = new Vector3(c.x + (m * cw * 0.22f), top + 0.1f, c.z);
                            ItemModels.Part(PrimitiveType.Sphere, root, at, new Vector3(0.28f, 0.24f, 0.26f), Melon);
                            for (int s = -1; s <= 1; s++) ItemModels.Part(PrimitiveType.Sphere, root, at + new Vector3(s * 0.07f, 0.005f, 0f), new Vector3(0.04f, 0.245f, 0.265f), new Color(0.35f, 0.65f, 0.3f));
                        }
                        break;
                    case 4:
                        // 포도 송이 셋: 보라 작은 구 무더기(위가 넓은 송이).
                        for (int g = 0; g < 3; g++)
                        {
                            var at = new Vector3(c.x + ((g - 1) * cw * 0.3f), top + 0.03f, c.z);
                            for (int j = 0; j < 7; j++) ItemModels.Part(PrimitiveType.Sphere, root, at + new Vector3(((j % 3) - 1) * 0.045f, (j / 3) * 0.035f, ((j / 3) - 1) * 0.05f), new Vector3(0.065f, 0.065f, 0.065f), Grape);
                            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.08f, -0.09f), new Vector3(0.06f, 0.02f, 0.06f), Leaf);
                        }
                        break;
                    default:
                        // 딸기 팩 둘: 흰 팩 + 빨간 알 + 초록 꼭지.
                        for (int p = -1; p <= 1; p += 2)
                        {
                            var at = new Vector3(c.x + (p * cw * 0.22f), top, c.z);
                            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.03f, 0f), new Vector3(cw * 0.4f, 0.06f, cd * 0.7f), Paint);
                            for (int j = 0; j < 6; j++)
                            {
                                var b = at + new Vector3(((j % 3) - 1) * 0.06f, 0.08f, ((j / 3) - 0.5f) * 0.09f);
                                ItemModels.Part(PrimitiveType.Sphere, root, b, new Vector3(0.06f, 0.055f, 0.06f), new Color(0.9f, 0.1f, 0.15f));
                                ItemModels.Part(PrimitiveType.Cube, root, b + new Vector3(0f, 0.025f, -0.02f), new Vector3(0.03f, 0.01f, 0.03f), Leaf);
                            }
                        }
                        break;
                }
            }
        }

        /// <summary>상자 안 둥근 과일 더미: 두 층(아래 3×2, 위 2×1) + 꼭지 점.</summary>
        private static void Pile(GameObject root, Vector3 c, float top, float cw, float cd, float r, Color fruit, Color stem)
        {
            for (int j = 0; j < 6; j++)
            {
                var at = new Vector3(c.x + (((j % 3) - 1) * cw * 0.28f), top + (r * 0.45f), c.z + (((j / 3) - 0.5f) * cd * 0.45f));
                ItemModels.Part(PrimitiveType.Sphere, root, at, new Vector3(r, r * 0.92f, r), fruit);
            }
            for (int j = 0; j < 2; j++)
            {
                var at = new Vector3(c.x + ((j - 0.5f) * cw * 0.28f), top + (r * 1.2f), c.z);
                ItemModels.Part(PrimitiveType.Sphere, root, at, new Vector3(r, r * 0.92f, r), fruit * 1.08f);
                ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, r * 0.45f, 0f), new Vector3(0.02f, 0.03f, 0.02f), stem);
            }
        }

        /// <summary>둥근 철판: 넓은 원반 + 테.</summary>
        private static void Plate(GameObject root, Vector3 at, float width, float depth, Color color)
        {
            ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.02f, 0f), new Vector3(width, 0.02f, depth), color);
            ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.035f, 0f), new Vector3(width * 0.93f, 0.005f, depth * 0.9f), color * 1.4f);
        }

        /// <summary>쌓은 컵 기둥(종이컵·그릇).</summary>
        private static void Stack(GameObject root, Vector3 at, float size, int count, Color color)
        {
            for (int k = 0; k < count; k++) ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.02f + (k * 0.05f), 0f), new Vector3(size, 0.025f, size), k % 2 == 0 ? color : color * 0.9f);
        }

        /// <summary>꼬치 몇 개를 비스듬히 꽂은 통.</summary>
        private static void Skewers(GameObject root, Vector3 at, int count, Color food)
        {
            ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.1f, 0f), new Vector3(0.18f, 0.1f, 0.18f), Steel);
            for (int k = 0; k < count; k++)
            {
                float a = k * 360f / count;
                ItemModels.Part(PrimitiveType.Capsule, root, at + new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 0.05f, 0.32f, Mathf.Cos(a * Mathf.Deg2Rad) * 0.05f), new Vector3(0.06f, 0.12f, 0.06f), food, new Vector3(15f, a, 0f));
            }
        }

        /// <summary>차양 위 상징물: 과일 노점 수박 반쪽, 솜사탕 큰 솜, 음료 노점 큰 컵, 떡볶이 떡꼬치, 어묵 바 붉은 등. 없으면 아무것도.</summary>
        private static void Topper(GameObject root, string name, Vector3 at)
        {
            switch (name)
            {
                case "과일 노점":
                    ItemModels.Part(PrimitiveType.Sphere, root, at, new Vector3(0.95f, 0.36f, 0.75f), Melon);
                    ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.12f, 0f), new Vector3(0.88f, 0.03f, 0.68f), new Color(0.35f, 0.7f, 0.3f));
                    ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.15f, 0f), new Vector3(0.78f, 0.03f, 0.6f), new Color(0.95f, 0.25f, 0.3f));
                    for (int k = 0; k < 7; k++) ItemModels.Part(PrimitiveType.Sphere, root, at + new Vector3(((k % 4) - 1.5f) * 0.16f, 0.19f, ((k / 4) - 0.4f) * 0.2f), new Vector3(0.05f, 0.02f, 0.07f), Dark);
                    break;
                case "솜사탕":
                    ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.1f, 0.25f), new Vector3(0.04f, 0.2f, 0.04f), Paint, new Vector3(60f, 0f, 0f));
                    ItemModels.Part(PrimitiveType.Sphere, root, at + new Vector3(0f, 0.25f, -0.1f), new Vector3(0.75f, 0.55f, 0.65f), new Color(1f, 0.62f, 0.82f));
                    ItemModels.Part(PrimitiveType.Sphere, root, at + new Vector3(0.2f, 0.32f, -0.15f), new Vector3(0.4f, 0.35f, 0.4f), new Color(0.65f, 0.85f, 1f));
                    break;
                case "음료 노점":
                    ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.25f, 0f), new Vector3(0.5f, 0.28f, 0.5f), new Color(0.88f, 0.95f, 1f));
                    ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.53f, 0f), new Vector3(0.44f, 0.01f, 0.44f), Orange);
                    ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0.08f, 0.75f, 0f), new Vector3(0.05f, 0.25f, 0.05f), Apple, new Vector3(0f, 0f, 15f));
                    break;
                case "떡볶이":
                    ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.1f, 0f), new Vector3(0.04f, 0.6f, 0.04f), Wood, new Vector3(0f, 0f, 90f));
                    for (int k = 0; k < 4; k++) ItemModels.Part(PrimitiveType.Capsule, root, at + new Vector3((k - 1.5f) * 0.24f, 0.1f, 0f), new Vector3(0.18f, 0.12f, 0.18f), Sauce, new Vector3(0f, 0f, 90f));
                    break;
                case "어묵 바":
                case "꼬치구이":
                    for (int side = -1; side <= 1; side += 2) ItemModels.Part(PrimitiveType.Capsule, root, at + new Vector3(side * 0.6f, -0.1f, 0.45f), new Vector3(0.24f, 0.2f, 0.24f), new Color(0.95f, 0.2f, 0.12f));
                    break;
            }
        }

        /// <summary>불 안 난 점포에서 김·연기가 오르는 자리(모델 축). 없으면 null. 연기면 smoky = true(회색).</summary>
        public static Vector3? SteamAt(string name, out bool smoky)
        {
            smoky = name == "꼬치구이";
            switch (name)
            {
                case "호떡집":
                case "전집":
                    return new Vector3(-0.5f, 0.8f, 0.6f);
                case "떡볶이":
                    return new Vector3(-0.3f, 0.8f, 0.6f);
                case "어묵 바":
                    return new Vector3(-0.2f, 0.95f, 0.6f);
                case "꼬치구이":
                    return new Vector3(-0.1f, 0.9f, 0.6f);
            }
            return null;
        }
    }
}
