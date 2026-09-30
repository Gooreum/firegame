using System.Collections.Generic;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// Resources/Models의 3D 모델(Kenney 주택·차·자연, Quaternius 사람)을 월드에 세운다.
    /// 월드는 땅이 XY, 위쪽이 −Z라서 모델(위 +Y, 앞 +Z)을 Stand로 돌려 앞이 카메라 쪽(−Y)을 보게 한다.
    /// 부모는 원점·무회전·축척 1이라고 본다(SurvivorWorld 루트).
    /// </summary>
    public static class Models3D
    {
        public static readonly Quaternion Stand = Quaternion.LookRotation(Vector3.down, Vector3.back);
        private const string Armature = "CharacterArmature|";

        private static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();

        /// <summary>모델 머티리얼과 같은 URP Lit(모델이 빌드에 넣어 두므로 폰에서도 있다).</summary>
        public static Shader LitShader
        {
            get { return Shader.Find("Universal Render Pipeline/Lit"); }
        }

        public static GameObject Load(string path)
        {
            if (!Prefabs.TryGetValue(path, out GameObject prefab) || prefab == null)
            {
                prefab = Resources.Load<GameObject>("Models/" + path);
                if (prefab == null) Debug.LogWarning("[Models3D] 없음: Models/" + path);
                Prefabs[path] = prefab;
            }
            return prefab;
        }

        /// <summary>
        /// 모델을 세워 발자국 w×h(칸) 안에 들게 균일 축척하고, 가운데를 at에, 바닥을 땅(z=0)에 둔다.
        /// yawDeg는 땅 위에서 도는 각(모델 위쪽 축 기준). 높이(칸)를 돌려준다. 모델이 없으면 null.
        /// </summary>
        public static GameObject Place(string path, Transform parent, Vector3 at, float w, float h, float yawDeg, out float height)
        {
            height = 0f;
            GameObject prefab = Load(path);
            if (prefab == null) return null;
            GameObject go = Object.Instantiate(prefab, parent, false);
            go.transform.localRotation = Stand * Quaternion.Euler(0f, yawDeg, 0f);
            Bounds b = Measure(go);
            float s = Mathf.Min(w / Mathf.Max(0.001f, b.size.x), h / Mathf.Max(0.001f, b.size.y));
            go.transform.localScale = Vector3.one * s;
            b = Measure(go);
            // 위쪽이 −Z라 바닥은 bounds.max.z.
            go.transform.localPosition += new Vector3(at.x - b.center.x, at.y - b.center.y, at.z - b.max.z);
            height = b.size.z;
            go.AddComponent<ModelTint>();
            return go;
        }

        /// <summary>키 tall(칸)인 사람. 발이 원점, 앞(+Z)이 카메라 쪽.</summary>
        public static GameObject Person(string path, Transform parent, float tall)
        {
            GameObject prefab = Load(path);
            if (prefab == null) return null;
            GameObject go = Object.Instantiate(prefab, parent, false);
            go.transform.localRotation = Stand;
            Bounds b = Measure(go);
            go.transform.localScale = Vector3.one * (tall / Mathf.Max(0.001f, b.size.z));
            go.AddComponent<ModelTint>();
            Play(go, "Idle");
            return go;
        }

        /// <summary>사람을 땅 위 at에 두고 땅 방향 dir(XY)을 보게 한다(Stand에서 모델 X는 월드 −X라 yaw = atan2(−x, −y)).</summary>
        public static void Pose(GameObject go, Vector3 at, Vector3 dir)
        {
            go.transform.localPosition = at;
            float yaw = dir.sqrMagnitude > 0.0001f ? Mathf.Atan2(-dir.x, -dir.y) * Mathf.Rad2Deg : 0f;
            go.transform.localRotation = Stand * Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>Idle·Walk·Run·Victory 같은 클립으로 부드럽게 바꾼다(이미 틀고 있으면 그대로).</summary>
        public static void Play(GameObject go, string clip, float speed = 1f)
        {
            var anim = go.GetComponent<Animation>();
            if (anim == null) return;
            string name = Armature + clip;
            AnimationState state = anim[name];
            if (state == null) return;
            state.speed = speed;
            if (!anim.IsPlaying(name)) anim.CrossFade(name, 0.15f);
        }

        /// <summary>
        /// 모델 전체에 색을 곱한다(탈수록 검게, 젖으면 파랗게). part가 머티리얼 이름에 색을 주면 원래 색 대신 그 색을 쓴다(방화복).
        /// 바뀔 때만 MaterialPropertyBlock을 다시 넣는다.
        /// </summary>
        public static void Tint(GameObject go, Color tint, System.Func<string, Color?> part = null, int partKey = 0)
        {
            if (go == null) return;
            var t = go.GetComponent<ModelTint>();
            if (t == null) t = go.AddComponent<ModelTint>();
            t.Apply(tint, part, partKey);
        }

        private static Bounds Measure(GameObject go)
        {
            var b = new Bounds(go.transform.position, Vector3.zero);
            bool first = true;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                Bounds rb;
                if (r is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                {
                    // 스킨 메시는 아직 갱신 전이라 원래 메시 경계를 뼈대 루트 기준으로 옮겨 잰다.
                    rb = TransformBounds(skin.transform, skin.sharedMesh.bounds);
                }
                else rb = r.bounds;
                if (first) b = rb;
                else b.Encapsulate(rb);
                first = false;
            }
            return b;
        }

        private static Bounds TransformBounds(Transform t, Bounds local)
        {
            var b = new Bounds(t.TransformPoint(local.center), Vector3.zero);
            Vector3 e = local.extents;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                b.Encapsulate(t.TransformPoint(local.center + corner));
            }
            return b;
        }
    }

    /// <summary>모델 하나의 렌더러·머티리얼 원래 색을 기억해 틴트를 바뀔 때만 넣는다.</summary>
    public sealed class ModelTint : MonoBehaviour
    {
        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private Color _last = new Color(-1f, 0f, 0f, 0f);
        private int _lastKey = -1;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public void Apply(Color tint, System.Func<string, Color?> part, int partKey)
        {
            if (tint == _last && partKey == _lastKey) return;
            _last = tint;
            _lastKey = partKey;
            if (_renderers == null)
            {
                _renderers = GetComponentsInChildren<Renderer>(true);
                _block = new MaterialPropertyBlock();
            }
            foreach (Renderer r in _renderers)
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    Color baseColor = mats[i].HasProperty(BaseColor) ? mats[i].GetColor(BaseColor) : Color.white;
                    Color? over = part != null ? part(mats[i].name) : null;
                    Color c = (over ?? baseColor) * tint;
                    c.a = 1f;
                    _block.Clear();
                    _block.SetColor(BaseColor, c);
                    r.SetPropertyBlock(_block, i);
                }
            }
        }
    }
}
