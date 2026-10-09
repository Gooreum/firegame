using System.Collections.Generic;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 아이템 3D 모델(드론·포탑·물폭탄·헬기·비행기)을 기본 도형으로 조립한다. 받아 올 모델이 없어도 되고, 저폴리 세상과 결이 맞는다.
    /// 모델 축은 Kenney·Quaternius와 같다(위 +Y, 앞 +Z, 크기 약 1칸). 월드에 세울 때는 Place로 Stand를 곱한다.
    /// 머티리얼은 받아 둔 Kenney 모델의 URP Lit을 복제해 텍스처만 흰색으로 바꾼다: 새로 만든 Lit은 폰 빌드에서 변형이 빠질 수 있다.
    /// </summary>
    public static class ItemModels
    {
        private static readonly Color FireRed = new Color(0.86f, 0.16f, 0.12f);
        private static readonly Color Paint = new Color(0.95f, 0.95f, 0.93f);
        private static readonly Color Steel = new Color(0.55f, 0.58f, 0.63f);
        private static readonly Color Dark = new Color(0.2f, 0.21f, 0.24f);
        private static readonly Color Water = new Color(0.3f, 0.65f, 1f);
        private static readonly Color Glass = new Color(0.55f, 0.85f, 1f);

        private static readonly Dictionary<Color, Material> Mats = new Dictionary<Color, Material>();
        private static Material _template;

        /// <summary>불배(항구): Kenney Watercraft 터그보트(2026-10-10 디자인 패스, 전엔 코드 선체). 길이 약 2.4칸(앞 +Z), 폭 약 1.2칸.</summary>
        public static GameObject Boat(Transform parent)
        {
            GameObject root = Root("BoatModel", parent);
            if (Models3D.Fit("Watercraft/boat-tug-a", root, Vector3.zero, 1.2f, 2.4f, out _, 0f, Models3D.BoatYaw) == null)
            {
                Hull(root, new Color(0.45f, 0.22f, 0.14f), new Color(0.62f, 0.5f, 0.36f), Dark);
            }
            return root;
        }

        /// <summary>유조선(항구 대화재): Kenney Watercraft 화물선 6×2.4(앞 +Z). 불배의 2.5배.</summary>
        public static GameObject Tanker(Transform parent)
        {
            GameObject root = Root("TankerModel", parent);
            Models3D.Fit("Watercraft/ship-cargo-a", root, Vector3.zero, 2.4f, 6.0f, out _, 0f, Models3D.BoatYaw);
            return root;
        }

        internal static GameObject Hull(Transform parent, string name, Color hull, Color deck, Color cabin)
        {
            GameObject root = Root(name, parent);
            Hull(root, hull, deck, cabin);
            return root;
        }

        /// <summary>코드 선체(모델이 없을 때): 상자 선체 + 뱃머리 쐐기 + 갑판 + 선실 + 굴뚝.</summary>
        private static void Hull(GameObject root, Color hull, Color deck, Color cabin)
        {
            Part(PrimitiveType.Cube, root, new Vector3(0f, 0.22f, -0.2f), new Vector3(1.1f, 0.44f, 1.9f), hull);
            Part(PrimitiveType.Cube, root, new Vector3(0f, 0.22f, 0.95f), new Vector3(0.78f, 0.44f, 0.78f), hull, new Vector3(0f, 45f, 0f));
            Part(PrimitiveType.Cube, root, new Vector3(0f, 0.46f, -0.2f), new Vector3(1.0f, 0.06f, 1.8f), deck);
            Part(PrimitiveType.Cube, root, new Vector3(0f, 0.72f, -0.55f), new Vector3(0.7f, 0.5f, 0.7f), cabin);
            Part(PrimitiveType.Cube, root, new Vector3(0f, 0.85f, -0.2f), new Vector3(0.6f, 0.22f, 0.1f), Glass);
            Part(PrimitiveType.Cylinder, root, new Vector3(0f, 1.1f, -0.75f), new Vector3(0.16f, 0.25f, 0.16f), Dark);
        }

        /// <summary>불꽃 가판대(야시장): 빨간 좌판 + 금색 천막 + 위로 선 발사관 다섯. 폭 약 1.8칸, 높이 약 1.4칸.</summary>
        public static GameObject FireworkStand(Transform parent)
        {
            GameObject root = Root("FireworkStandModel", parent);
            var gold = new Color(0.95f, 0.78f, 0.25f);
            Part(PrimitiveType.Cube, root, new Vector3(0f, 0.3f, 0f), new Vector3(1.8f, 0.6f, 1.3f), FireRed);
            Part(PrimitiveType.Cube, root, new Vector3(0f, 0.62f, 0f), new Vector3(1.9f, 0.05f, 1.4f), gold);
            for (int k = 0; k < 5; k++)
            {
                float ox = (k - 2) * 0.32f;
                Part(PrimitiveType.Cylinder, root, new Vector3(ox, 0.95f, (k % 2 == 0 ? 0.15f : -0.15f)), new Vector3(0.16f, 0.32f, 0.16f), k % 2 == 0 ? Dark : Steel);
                Part(PrimitiveType.Cylinder, root, new Vector3(ox, 1.28f, (k % 2 == 0 ? 0.15f : -0.15f)), new Vector3(0.14f, 0.03f, 0.14f), gold);
            }
            Part(PrimitiveType.Cube, root, new Vector3(0f, 1.25f, -0.75f), new Vector3(1.6f, 0.5f, 0.06f), gold);
            return root;
        }

        /// <summary>약품 드럼(공단): 노란 원통 + 검은 띠 둘 + 뚜껑 테. 지름 약 1칸, 높이 약 1.3칸.</summary>
        public static GameObject Drum(Transform parent)
        {
            GameObject root = Root("DrumModel", parent);
            var yellow = new Color(0.95f, 0.75f, 0.12f);
            // 원기둥은 높이 2(스케일 y=1이면 2칸)라 y 스케일을 반으로.
            Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.65f, 0f), new Vector3(1f, 0.65f, 1f), yellow);
            Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.42f, 0f), new Vector3(1.03f, 0.06f, 1.03f), Dark);
            Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.9f, 0f), new Vector3(1.03f, 0.06f, 1.03f), Dark);
            Part(PrimitiveType.Cylinder, root, new Vector3(0f, 1.31f, 0f), new Vector3(0.9f, 0.02f, 0.9f), Steel);
            Part(PrimitiveType.Cylinder, root, new Vector3(0.22f, 1.34f, 0.1f), new Vector3(0.14f, 0.03f, 0.14f), Dark);
            return root;
        }

        /// <summary>
        /// 모델을 땅 위 ground(XY)에서 height칸 떠서 두고 땅 방향 dir을 보게 한다(Models3D.Pose와 같은 yaw). size는 균일 축척.
        /// </summary>
        public static void Place(GameObject go, Vector3 ground, float height, Vector3 dir, float size)
        {
            float yaw = dir.sqrMagnitude > 0.0001f ? Mathf.Atan2(-dir.x, -dir.y) * Mathf.Rad2Deg : 0f;
            go.transform.localPosition = new Vector3(ground.x, ground.y, -height);
            go.transform.localRotation = Models3D.Stand * Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * size;
        }

        /// <summary>이름이 child로 시작하는 자식 날개들을 모델 위쪽 축으로 돌린다(재생 중이 아니어도 time으로 정해진 각).</summary>
        public static void Spin(GameObject go, string child, float degPerSec, float time)
        {
            if (go == null) return;
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform c = t.GetChild(i);
                if (!c.name.StartsWith(child)) continue;
                var axis = c.name.StartsWith("TailRotor") ? Vector3.right : Vector3.up;
                c.localRotation = Quaternion.AngleAxis((time * degPerSec) + (i * 37f), axis);
            }
        }

        internal static GameObject Root(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Models3D.Stand;
            return go;
        }

        /// <summary>날개 한 벌: 빈 축(name) 아래 가로 날개 하나(길이 len, 폭 width). side면 옆으로 선 꼬리 날개.</summary>
        private static GameObject Rotor(GameObject parent, string name, Vector3 at, float len, float width, Color color, bool side = false)
        {
            var axis = new GameObject(name);
            axis.transform.SetParent(parent.transform, false);
            axis.transform.localPosition = at;
            Part(PrimitiveType.Cube, axis, Vector3.zero, side ? new Vector3(0.03f, width, len) : new Vector3(len, 0.03f, width), color);
            return axis;
        }

        internal static GameObject Part(PrimitiveType type, GameObject parent, Vector3 at, Vector3 scale, Color color, Vector3 euler = default)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat(color);
            return go;
        }

        internal static Material Mat(Color color)
        {
            if (Mats.TryGetValue(color, out Material m) && m != null) return m;
            if (_template == null)
            {
                GameObject car = Models3D.Load("Cars/firetruck");
                Renderer r = car != null ? car.GetComponentInChildren<Renderer>() : null;
                _template = r != null ? r.sharedMaterial : null;
            }
            m = _template != null ? new Material(_template) : new Material(Models3D.LitShader);
            m.name = "Item_" + ColorUtility.ToHtmlStringRGB(color);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", Texture2D.whiteTexture);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.45f);
            Mats[color] = m;
            return m;
        }
    }
}
