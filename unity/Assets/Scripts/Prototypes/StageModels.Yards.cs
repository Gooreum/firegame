using FireGame.Core.Sim;
using FireGame.Prototypes.Logic;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 숲·항구·공단 건물 앞마당 소품. 같은 종류 건물(목공소·산장·숲 식당은 같은 통나무집, 선구점·어구 창고·통조림 공장은 같은 창고,
    /// 공단은 Kenney 공장)이 이름 말고는 똑같아, 문 앞(남쪽, 모델 +Z) 0.8칸에 이름에 맞는 소품을 놓는다.
    /// 문 자리(|x| &lt; 0.45)는 비운다. 판정 없는 그림이다.
    /// </summary>
    public static partial class StageModels
    {
        /// <summary>건물 모델(root, 모델 축) 앞에 이름별 마당 소품. 숲(2)·항구(4)만.</summary>
        private static void Yard(GameObject root, int stage, string name, float w, float h)
        {
            float z = (h / 2f) + 0.45f;
            float x = w * 0.32f;
            switch (name)
            {
                // 숲
                case "산장":
                    Firewood(root, new Vector3(-x, 0f, z));
                    Canoe(root, new Vector3(x, 0f, z), new Color(0.85f, 0.25f, 0.15f));
                    break;
                case "통나무 카페":
                    ParasolTable(root, new Vector3(-x, 0f, z), new Color(0.95f, 0.9f, 0.75f), Wood, 0.8f);
                    ParasolTable(root, new Vector3(x, 0f, z), new Color(0.3f, 0.55f, 0.35f), Wood, 0.8f);
                    break;
                case "목공소":
                    Workbench(root, new Vector3(-x, 0f, z));
                    Sawhorse(root, new Vector3(x - 0.3f, 0f, z));
                    Planks(root, new Vector3(x + 0.35f, 0f, z));
                    break;
                case "숲 식당":
                    Grill(root, new Vector3(-x, 0f, z));
                    PicnicTable(root, new Vector3(x, 0f, z));
                    break;
                case "관리사무소":
                    Flagpole(root, new Vector3(-x - 0.2f, 0f, z));
                    NoticeBoard(root, new Vector3(x, 0f, z));
                    break;
                case "야영 관리동":
                    WaterTank(root, new Vector3(-x, 0f, z));
                    for (int k = 0; k < 2; k++) Bicycle(root, new Vector3(x - 0.2f + (k * 0.4f), 0f, z), k == 0 ? new Color(0.2f, 0.45f, 0.85f) : Red);
                    break;
                // 항구
                case "선구점":
                    for (int k = 0; k < 3; k++) RopeCoil(root, new Vector3(-x - 0.3f + (k * 0.42f), 0f, z + ((k % 2) * 0.12f)));
                    Anchor(root, new Vector3(x, 0f, z));
                    break;
                case "수산 창고":
                    FishCrates(root, new Vector3(-x, 0f, z));
                    IceBox(root, new Vector3(x, 0f, z));
                    break;
                case "어구 창고":
                    NetPile(root, new Vector3(-x, 0f, z));
                    TrapTower(root, new Vector3(x, 0f, z));
                    break;
                case "통조림 공장":
                    CanPyramid(root, new Vector3(-x, 0f, z));
                    Conveyor(root, new Vector3(x, 0f, z), new Color(0.75f, 0.78f, 0.82f));
                    ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(-w * 0.35f, 1.6f, -h * 0.3f), new Vector3(0.36f, 0.75f, 0.36f), new Color(0.55f, 0.35f, 0.28f));
                    break;
                case "횟집":
                    FishTank(root, new Vector3(-x, 0f, z));
                    break;
            }
        }

        private static readonly System.Collections.Generic.HashSet<string> FactoryYardNames = new System.Collections.Generic.HashSet<string>
        {
            "인쇄소", "페인트 공장", "정비소", "도금 공장", "식품 공장", "섬유 공장", "부품 공장", "포장 공장",
        };

        /// <summary>공단 공장 앞마당: Kenney 공장 옆에 세울 소품 루트(이름별). 없으면 null. 월드 자리(at)에 세워 돌려준다.</summary>
        public static GameObject FactoryYard(Transform parent, Structure st, Vector3 at)
        {
            if (!FactoryYardNames.Contains(st.Name)) return null;
            float w = st.Half.X * 2f;
            float h = st.Half.Y * 2f;
            GameObject root = ItemModels.Root("FactoryYard", parent);
            float z = (h / 2f) + 0.45f;
            float x = w * 0.32f;
            switch (st.Name)
            {
                case "인쇄소":
                    for (int k = 0; k < 3; k++) ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(-x - 0.3f + (k * 0.32f), 0.16f, z), new Vector3(0.32f, 0.3f, 0.32f), Paint, new Vector3(90f, 0f, 0f));
                    Pallet(root, new Vector3(x, 0f, z), new Color(0.95f, 0.95f, 0.9f), 3);
                    break;
                case "페인트 공장":
                    Color[] paints = { Red, new Color(0.98f, 0.8f, 0.15f), new Color(0.2f, 0.5f, 0.9f), new Color(0.3f, 0.7f, 0.35f), Paint, new Color(0.9f, 0.45f, 0.7f) };
                    for (int k = 0; k < 6; k++) ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(-x - 0.25f + ((k % 3) * 0.26f), 0.13f + ((k / 3) * 0.26f), z + ((k / 3) * 0.06f)), new Vector3(0.24f, 0.12f, 0.24f), paints[k]);
                    for (int k = 0; k < 3; k++) ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(x - 0.25f + (k * 0.3f), 0.25f, z), new Vector3(0.28f, 0.25f, 0.28f), paints[(k + 2) % paints.Length]);
                    break;
                case "정비소":
                    CarOnLift(root, new Vector3(-x, 0f, z));
                    for (int k = 0; k < 4; k++) ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3(x + 0.2f, 0.06f + (k * 0.11f), z), new Vector3(0.42f, 0.055f, 0.42f), Dark);
                    break;
                case "도금 공장":
                    for (int k = 0; k < 5; k++) ItemModels.Part(PrimitiveType.Cube, root, new Vector3(-x, 0.04f + (k * 0.06f), z + ((k % 2) * 0.04f)), new Vector3(0.9f, 0.04f, 0.55f), k % 2 == 0 ? new Color(0.82f, 0.84f, 0.88f) : new Color(0.9f, 0.75f, 0.35f));
                    Pallet(root, new Vector3(x, 0f, z), Steel, 2);
                    break;
                case "식품 공장":
                    BoxTruck(root, new Vector3(0f, 0f, z + 0.1f), Paint);
                    break;
                case "섬유 공장":
                    Color[] cloth = { new Color(0.85f, 0.25f, 0.35f), new Color(0.25f, 0.5f, 0.85f), new Color(0.95f, 0.8f, 0.3f), new Color(0.4f, 0.7f, 0.45f) };
                    for (int k = 0; k < 4; k++) ItemModels.Part(PrimitiveType.Cylinder, root, new Vector3((k < 2 ? -x : x) + (((k % 2) - 0.5f) * 0.32f), 0.15f, z), new Vector3(0.3f, 0.4f, 0.3f), cloth[k], new Vector3(90f, 0f, 0f));
                    break;
                case "부품 공장":
                    for (int k = 0; k < 2; k++) Gear(root, new Vector3(-x - 0.2f + (k * 0.5f), 0.05f + (k * 0.02f), z), 0.25f + (k * 0.08f));
                    Pallet(root, new Vector3(x, 0f, z), new Color(0.78f, 0.6f, 0.38f), 2);
                    break;
                case "포장 공장":
                    Pallet(root, new Vector3(-x, 0f, z), new Color(0.78f, 0.6f, 0.38f), 4);
                    Conveyor(root, new Vector3(x, 0f, z), new Color(0.78f, 0.6f, 0.38f));
                    break;
                default:
                    Object.Destroy(root);
                    return null;
            }
            // 공장은 크고 소품은 작아 1.2배(마당이 문 앞으로 조금 더 나온다).
            ItemModels.Place(root, at, 0f, Vector3.down, 1.2f);
            return root;
        }

        // ------------------------------------------------------------------
        // 숲 소품
        // ------------------------------------------------------------------

        private static void Firewood(GameObject root, Vector3 at)
        {
            for (int r = 0; r < 3; r++)
            {
                for (int k = 0; k < 3 - r; k++) ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3((k - ((2 - r) / 2f)) * 0.2f, 0.09f + (r * 0.16f), 0f), new Vector3(0.18f, 0.3f, 0.18f), k % 2 == 0 ? Wood : Wood * 1.1f, new Vector3(90f, 0f, 0f));
            }
            ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0.5f, 0.12f, 0f), new Vector3(0.3f, 0.12f, 0.3f), DarkWood);
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0.5f, 0.32f, 0f), new Vector3(0.05f, 0.25f, 0.05f), DarkWood, new Vector3(0f, 0f, 25f));
        }

        private static void Canoe(GameObject root, Vector3 at, Color hull)
        {
            ItemModels.Part(PrimitiveType.Capsule, root, at + new Vector3(0f, 0.15f, 0f), new Vector3(0.38f, 0.75f, 0.24f), hull, new Vector3(0f, 0f, 90f));
            ItemModels.Part(PrimitiveType.Capsule, root, at + new Vector3(0f, 0.22f, 0f), new Vector3(0.28f, 0.65f, 0.18f), DarkWood, new Vector3(0f, 0f, 90f));
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0.1f, 0.3f, 0.12f), new Vector3(0.9f, 0.03f, 0.06f), Wood, new Vector3(0f, 20f, 0f));
        }

        private static void Workbench(GameObject root, Vector3 at)
        {
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.42f, 0f), new Vector3(1.0f, 0.08f, 0.5f), Wood);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2) ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(sx * 0.42f, 0.2f, sz * 0.18f), new Vector3(0.07f, 0.4f, 0.07f), DarkWood);
            }
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0.35f, 0.52f, 0f), new Vector3(0.16f, 0.12f, 0.2f), Steel);
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(-0.15f, 0.48f, 0.05f), new Vector3(0.45f, 0.04f, 0.14f), new Color(0.88f, 0.72f, 0.48f), new Vector3(0f, 15f, 0f));
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(-0.2f, 0.48f, -0.12f), new Vector3(0.25f, 0.03f, 0.06f), Red);
        }

        private static void Sawhorse(GameObject root, Vector3 at)
        {
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.4f, 0f), new Vector3(0.08f, 0.08f, 0.7f), Wood);
            for (int sz = -1; sz <= 1; sz += 2)
            {
                for (int sx = -1; sx <= 1; sx += 2) ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(sx * 0.1f, 0.2f, sz * 0.28f), new Vector3(0.05f, 0.42f, 0.05f), Wood, new Vector3(0f, 0f, -sx * 15f));
            }
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.47f, 0f), new Vector3(0.7f, 0.05f, 0.22f), new Color(0.88f, 0.72f, 0.48f), new Vector3(0f, 80f, 0f));
        }

        private static void Planks(GameObject root, Vector3 at)
        {
            for (int k = 0; k < 4; k++) ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3((k % 2) * 0.03f, 0.04f + (k * 0.07f), 0f), new Vector3(0.3f, 0.06f, 0.75f), new Color(0.85f, 0.68f, 0.45f));
        }

        private static void Grill(GameObject root, Vector3 at)
        {
            ItemModels.Part(PrimitiveType.Capsule, root, at + new Vector3(0f, 0.45f, 0f), new Vector3(0.38f, 0.3f, 0.32f), Dark, new Vector3(0f, 0f, 90f));
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.55f, 0f), new Vector3(0.55f, 0.02f, 0.26f), new Color(1f, 0.45f, 0.1f));
            for (int k = -1; k <= 1; k += 2) ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(k * 0.2f, 0.17f, 0f), new Vector3(0.04f, 0.34f, 0.3f), Dark);
            for (int k = 0; k < 3; k++) ItemModels.Part(PrimitiveType.Capsule, root, at + new Vector3((k - 1) * 0.15f, 0.59f, 0f), new Vector3(0.07f, 0.09f, 0.07f), new Color(0.6f, 0.3f, 0.15f), new Vector3(90f, 0f, 0f));
        }

        private static void PicnicTable(GameObject root, Vector3 at)
        {
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.4f, 0f), new Vector3(0.9f, 0.05f, 0.4f), Wood);
            for (int side = -1; side <= 1; side += 2)
            {
                ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.22f, side * 0.35f), new Vector3(0.9f, 0.05f, 0.16f), Wood);
                ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(side * 0.35f, 0.2f, 0f), new Vector3(0.05f, 0.4f, 0.8f), DarkWood, new Vector3(0f, 0f, 0f));
            }
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0.1f, 0.44f, 0f), new Vector3(0.4f, 0.02f, 0.3f), new Color(0.85f, 0.2f, 0.2f));
        }

        private static void Flagpole(GameObject root, Vector3 at)
        {
            ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 1.1f, 0f), new Vector3(0.05f, 1.1f, 0.05f), Paint);
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0.3f, 1.95f, 0f), new Vector3(0.55f, 0.35f, 0.02f), new Color(0.2f, 0.55f, 0.3f));
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0.3f, 1.95f, 0.012f), new Vector3(0.2f, 0.2f, 0.02f), Paint);
            ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.05f, 0f), new Vector3(0.3f, 0.05f, 0.3f), Stone);
        }

        private static void NoticeBoard(GameObject root, Vector3 at)
        {
            for (int side = -1; side <= 1; side += 2) ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(side * 0.42f, 0.45f, 0f), new Vector3(0.07f, 0.9f, 0.07f), DarkWood);
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.62f, 0f), new Vector3(0.9f, 0.5f, 0.05f), new Color(0.75f, 0.58f, 0.38f), new Vector3(-20f, 0f, 0f));
            Color[] notes = { Paint, new Color(0.98f, 0.9f, 0.4f), new Color(0.6f, 0.85f, 1f), Paint };
            for (int k = 0; k < 4; k++) ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(((k % 2) - 0.5f) * 0.4f, 0.55f + ((k / 2) * 0.18f), 0.04f - ((k / 2) * 0.06f)), new Vector3(0.28f, 0.14f, 0.02f), notes[k], new Vector3(-20f, 0f, 0f));
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.92f, -0.05f), new Vector3(1.05f, 0.05f, 0.25f), new Color(0.3f, 0.45f, 0.3f));
        }

        private static void WaterTank(GameObject root, Vector3 at)
        {
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2) ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(sx * 0.22f, 0.3f, sz * 0.18f), new Vector3(0.06f, 0.6f, 0.06f), Steel);
            }
            ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.85f, 0f), new Vector3(0.6f, 0.28f, 0.6f), new Color(0.25f, 0.5f, 0.85f));
            ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 1.14f, 0f), new Vector3(0.5f, 0.02f, 0.5f), Paint);
        }

        private static void Bicycle(GameObject root, Vector3 at, Color frame)
        {
            for (int e = -1; e <= 1; e += 2) ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.2f, e * 0.25f), new Vector3(0.38f, 0.02f, 0.38f), Dark, new Vector3(0f, 0f, 90f));
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.32f, 0f), new Vector3(0.04f, 0.04f, 0.5f), frame);
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.42f, -0.12f), new Vector3(0.06f, 0.04f, 0.14f), Dark);
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.45f, 0.22f), new Vector3(0.3f, 0.03f, 0.03f), Dark);
        }

        // ------------------------------------------------------------------
        // 항구 소품
        // ------------------------------------------------------------------

        private static void RopeCoil(GameObject root, Vector3 at)
        {
            var rope = new Color(0.85f, 0.72f, 0.48f);
            for (int k = 0; k < 3; k++) ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.04f + (k * 0.06f), 0f), new Vector3(0.4f - (k * 0.04f), 0.03f, 0.4f - (k * 0.04f)), k % 2 == 0 ? rope : rope * 0.85f);
            ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.2f, 0f), new Vector3(0.14f, 0.02f, 0.14f), Dark);
        }

        private static void Anchor(GameObject root, Vector3 at)
        {
            var iron = new Color(0.25f, 0.26f, 0.3f);
            // 바닥에 눕힌 닻: 축 + 가로대 + 굽은 팔 둘 + 고리.
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.06f, 0f), new Vector3(0.1f, 0.1f, 0.85f), iron);
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.06f, -0.28f), new Vector3(0.55f, 0.08f, 0.08f), iron);
            for (int side = -1; side <= 1; side += 2) ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(side * 0.2f, 0.06f, 0.32f), new Vector3(0.45f, 0.1f, 0.1f), iron, new Vector3(0f, side * 40f, 0f));
            ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.06f, -0.48f), new Vector3(0.18f, 0.03f, 0.18f), iron);
        }

        private static void FishCrates(GameObject root, Vector3 at)
        {
            var blue = new Color(0.25f, 0.5f, 0.8f);
            for (int k = 0; k < 4; k++)
            {
                var c = at + new Vector3(((k % 2) - 0.5f) * 0.48f, 0.1f + ((k / 2) * 0.2f), (k / 2) * -0.1f);
                ItemModels.Part(PrimitiveType.Cube, root, c, new Vector3(0.44f, 0.18f, 0.32f), blue);
                if (k >= 2)
                {
                    for (int f = -1; f <= 1; f++) ItemModels.Part(PrimitiveType.Capsule, root, c + new Vector3(f * 0.12f, 0.1f, 0f), new Vector3(0.08f, 0.13f, 0.06f), f == 0 ? new Color(0.85f, 0.45f, 0.35f) : new Color(0.6f, 0.65f, 0.75f), new Vector3(90f, 0f, 0f));
                }
            }
        }

        private static void IceBox(GameObject root, Vector3 at)
        {
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.2f, 0f), new Vector3(0.7f, 0.4f, 0.45f), Paint);
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.41f, 0f), new Vector3(0.6f, 0.02f, 0.36f), new Color(0.82f, 0.95f, 1f));
            for (int k = 0; k < 5; k++) ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3((k - 2f) * 0.11f, 0.44f, (k % 2) * 0.08f - 0.04f), new Vector3(0.08f, 0.05f, 0.08f), new Color(0.9f, 0.97f, 1f), new Vector3(0f, k * 30f, 0f));
        }

        private static void NetPile(GameObject root, Vector3 at)
        {
            var net = new Color(0.2f, 0.5f, 0.35f);
            for (int k = 0; k < 4; k++) ItemModels.Part(PrimitiveType.Sphere, root, at + new Vector3(((k % 2) - 0.5f) * 0.35f, 0.12f + ((k / 2) * 0.08f), ((k / 2) - 0.5f) * 0.2f), new Vector3(0.5f, 0.24f, 0.4f), k % 2 == 0 ? net : net * 1.2f);
            for (int k = 0; k < 4; k++) ItemModels.Part(PrimitiveType.Sphere, root, at + new Vector3((k - 1.5f) * 0.2f, 0.28f, 0.15f), new Vector3(0.09f, 0.09f, 0.09f), new Color(0.98f, 0.55f, 0.15f));
        }

        private static void TrapTower(GameObject root, Vector3 at)
        {
            for (int k = 0; k < 3; k++)
            {
                var c = at + new Vector3(0f, 0.13f + (k * 0.25f), 0f);
                ItemModels.Part(PrimitiveType.Cube, root, c, new Vector3(0.5f, 0.22f, 0.4f), new Color(0.25f, 0.27f, 0.3f));
                ItemModels.Part(PrimitiveType.Cube, root, c + new Vector3(0f, 0.115f, 0f), new Vector3(0.52f, 0.02f, 0.42f), new Color(0.98f, 0.55f, 0.15f));
            }
        }

        private static void CanPyramid(GameObject root, Vector3 at)
        {
            int[] rows = { 4, 3, 2, 1 };
            for (int r = 0; r < rows.Length; r++)
            {
                for (int k = 0; k < rows[r]; k++)
                {
                    var c = at + new Vector3((k - ((rows[r] - 1) / 2f)) * 0.17f, 0.08f + (r * 0.15f), 0f);
                    ItemModels.Part(PrimitiveType.Cylinder, root, c, new Vector3(0.15f, 0.075f, 0.15f), new Color(0.78f, 0.8f, 0.84f));
                    ItemModels.Part(PrimitiveType.Cylinder, root, c, new Vector3(0.155f, 0.035f, 0.155f), Red);
                }
            }
        }

        private static void Conveyor(GameObject root, Vector3 at, Color cargo)
        {
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.3f, 0f), new Vector3(1.1f, 0.08f, 0.32f), Dark);
            for (int k = 0; k < 5; k++) ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3((k - 2f) * 0.22f, 0.35f, 0f), new Vector3(0.05f, 0.15f, 0.05f), Steel, new Vector3(90f, 0f, 0f));
            for (int e = -1; e <= 1; e += 2) ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(e * 0.45f, 0.13f, 0f), new Vector3(0.06f, 0.26f, 0.26f), Steel);
            for (int k = 0; k < 2; k++) ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(-0.25f + (k * 0.45f), 0.46f, 0f), new Vector3(0.22f, 0.16f, 0.2f), cargo);
        }

        private static void FishTank(GameObject root, Vector3 at)
        {
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.22f, 0f), new Vector3(0.95f, 0.44f, 0.5f), Steel);
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.45f, 0f), new Vector3(0.88f, 0.02f, 0.43f), new Color(0.35f, 0.7f, 0.9f));
            Color[] fish = { new Color(0.95f, 0.5f, 0.2f), new Color(0.6f, 0.65f, 0.72f), new Color(0.95f, 0.5f, 0.2f), new Color(0.85f, 0.3f, 0.3f), new Color(0.6f, 0.65f, 0.72f) };
            for (int k = 0; k < 5; k++) ItemModels.Part(PrimitiveType.Capsule, root, at + new Vector3((k - 2f) * 0.16f, 0.47f, ((k % 2) - 0.5f) * 0.16f), new Vector3(0.07f, 0.11f, 0.05f), fish[k], new Vector3(90f, k * 40f, 0f));
        }

        // ------------------------------------------------------------------
        // 공단 소품
        // ------------------------------------------------------------------

        private static void Pallet(GameObject root, Vector3 at, Color load, int boxes)
        {
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.05f, 0f), new Vector3(0.8f, 0.1f, 0.6f), Wood);
            for (int k = 0; k < boxes; k++) ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(((k % 2) - 0.5f) * 0.36f, 0.22f + ((k / 2) * 0.28f), 0f), new Vector3(0.34f, 0.26f, 0.5f), k % 2 == 0 ? load : load * 0.9f);
        }

        private static void CarOnLift(GameObject root, Vector3 at)
        {
            for (int side = -1; side <= 1; side += 2) ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(side * 0.42f, 0.4f, 0f), new Vector3(0.1f, 0.8f, 0.1f), new Color(0.95f, 0.75f, 0.15f));
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.4f, 0f), new Vector3(0.8f, 0.05f, 0.5f), Dark);
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0f, 0.56f, 0f), new Vector3(0.75f, 0.22f, 0.42f), new Color(0.2f, 0.45f, 0.8f));
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(-0.05f, 0.74f, 0f), new Vector3(0.42f, 0.16f, 0.38f), new Color(0.6f, 0.85f, 1f));
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2) ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(sx * 0.25f, 0.46f, sz * 0.22f), new Vector3(0.18f, 0.04f, 0.18f), Dark, new Vector3(90f, 0f, 0f));
            }
        }

        private static void BoxTruck(GameObject root, Vector3 at, Color box)
        {
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(-0.2f, 0.45f, 0f), new Vector3(1.2f, 0.6f, 0.55f), box);
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(-0.2f, 0.46f, 0.28f), new Vector3(1.0f, 0.12f, 0.01f), new Color(0.3f, 0.6f, 0.95f));
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0.6f, 0.35f, 0f), new Vector3(0.4f, 0.45f, 0.52f), new Color(0.2f, 0.5f, 0.85f));
            ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(0.81f, 0.45f, 0f), new Vector3(0.02f, 0.2f, 0.42f), Glass);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2) ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(sx * 0.45f, 0.12f, sz * 0.26f), new Vector3(0.24f, 0.05f, 0.24f), Dark, new Vector3(90f, 0f, 0f));
            }
        }

        private static void Gear(GameObject root, Vector3 at, float r)
        {
            ItemModels.Part(PrimitiveType.Cylinder, root, at, new Vector3(r * 2f, 0.05f, r * 2f), Steel);
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f;
                ItemModels.Part(PrimitiveType.Cube, root, at + new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * r, 0f, Mathf.Cos(a * Mathf.Deg2Rad) * r), new Vector3(0.1f, 0.09f, 0.12f), Steel, new Vector3(0f, a, 0f));
            }
            ItemModels.Part(PrimitiveType.Cylinder, root, at + new Vector3(0f, 0.01f, 0f), new Vector3(r * 0.6f, 0.06f, r * 0.6f), Dark);
        }
    }
}
