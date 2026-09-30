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
        /// yawDeg는 땅 위에서 도는 각(모델 위쪽 축 기준). maxHeight(0이면 제한 없음)보다 높으면 키만 눌러 낮춘다.
        /// size는 놓인 크기(가로, 앞뒤, 높이 칸). 모델이 없으면 null.
        /// </summary>
        public static GameObject Place(string path, Transform parent, Vector3 at, float w, float h, float yawDeg, out Vector3 size, float maxHeight = 0f)
        {
            size = Vector3.zero;
            GameObject prefab = Load(path);
            if (prefab == null) return null;
            GameObject go = Object.Instantiate(prefab, parent, false);
            go.transform.localRotation = Stand * Quaternion.Euler(0f, yawDeg, 0f);
            Bounds b = Measure(go);
            float s = Mathf.Min(w / Mathf.Max(0.001f, b.size.x), h / Mathf.Max(0.001f, b.size.y));
            go.transform.localScale = Vector3.one * s;
            b = Measure(go);
            if (maxHeight > 0f && b.size.z > maxHeight)
            {
                // 모델 위쪽 축(Y)이 월드 높이다.
                go.transform.localScale = new Vector3(s, s * maxHeight / b.size.z, s);
                b = Measure(go);
            }
            // 위쪽이 −Z라 바닥은 bounds.max.z.
            go.transform.localPosition += new Vector3(at.x - b.center.x, at.y - b.center.y, at.z - b.max.z);
            size = b.size;
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

        /// <summary>
        /// Idle·Walk·Run·Victory 같은 클립으로 부드럽게 바꾼다(이미 틀고 있으면 그대로).
        /// 플레이 중이 아니면(편집기 캡처) 애니메이션이 저절로 돌지 않으므로 time(초)으로 직접 자세를 뽑는다.
        /// </summary>
        public static void Play(GameObject go, string clip, float speed = 1f, float time = 0f)
        {
            var anim = go != null ? go.GetComponent<Animation>() : null;
            if (anim == null) return;
            string name = Armature + clip;
            AnimationState state = anim[name];
            if (state == null) return;
            state.speed = speed;
            if (Application.isPlaying)
            {
                if (!anim.IsPlaying(name)) anim.CrossFade(name, 0.15f);
                return;
            }
            foreach (AnimationState other in anim)
            {
                other.enabled = other == state;
                other.weight = other == state ? 1f : 0f;
            }
            state.time = state.length > 0f ? Mathf.Repeat(time * speed, state.length) : 0f;
            anim.Sample();
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

        private static readonly Dictionary<string, Texture2D> Recolored = new Dictionary<string, Texture2D>();

        /// <summary>
        /// Kenney 주택 colormap의 초록 지붕 칸만 target 색으로 바꾼 텍스처(밝기 비율은 유지). 같은 색이면 재사용.
        /// 모델 머티리얼 대신 MaterialPropertyBlock의 _BaseMap으로 넣는다.
        /// </summary>
        public static void RoofColor(GameObject go, Color target)
        {
            if (go == null) return;
            var t = go.GetComponent<ModelTint>();
            if (t == null) t = go.AddComponent<ModelTint>();
            Renderer first = go.GetComponentInChildren<Renderer>();
            var src = first != null && first.sharedMaterial != null ? first.sharedMaterial.GetTexture("_BaseMap") as Texture2D : null;
            if (src == null || !src.isReadable) return;
            string key = src.GetInstanceID() + ":" + ColorUtility.ToHtmlStringRGB(target);
            if (!Recolored.TryGetValue(key, out Texture2D tex) || tex == null)
            {
                Color32[] px = src.GetPixels32();
                const float RoofLum = (97f * 0.3f) + (203f * 0.59f) + (139f * 0.11f);
                for (int i = 0; i < px.Length; i++)
                {
                    Color32 p = px[i];
                    if (p.g <= p.r + 40 || p.g <= p.b + 20) continue;
                    float k = ((p.r * 0.3f) + (p.g * 0.59f) + (p.b * 0.11f)) / RoofLum;
                    px[i] = new Color32((byte)Mathf.Min(255f, target.r * 255f * k), (byte)Mathf.Min(255f, target.g * 255f * k), (byte)Mathf.Min(255f, target.b * 255f * k), p.a);
                }
                tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, true) { name = src.name + "_" + ColorUtility.ToHtmlStringRGB(target), filterMode = src.filterMode, hideFlags = HideFlags.DontUnloadUnusedAsset };
                tex.SetPixels32(px);
                tex.Apply(true);
                Recolored[key] = tex;
            }
            t.SetBaseMap(tex);
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

    /// <summary>
    /// 사람 모델 풀: 매 프레임 Begin → Get(모델)… → End. 쓴 만큼 켜 두고 남은 것은 끈다(대원·시민·갇힌 사람).
    /// </summary>
    public sealed class PersonPool
    {
        private readonly Transform _parent;
        private readonly float _tall;
        private readonly Dictionary<string, List<GameObject>> _items = new Dictionary<string, List<GameObject>>();
        private readonly Dictionary<string, int> _used = new Dictionary<string, int>();

        public PersonPool(Transform parent, float tall)
        {
            _parent = parent;
            _tall = tall;
        }

        public void Begin()
        {
            var keys = new List<string>(_used.Keys);
            foreach (string k in keys) _used[k] = 0;
        }

        /// <summary>이번 프레임에 쓸 path 모델 하나(없으면 null). 크기 scale은 키 배율.</summary>
        public GameObject Get(string path, float scale = 1f)
        {
            if (!_items.TryGetValue(path, out List<GameObject> list))
            {
                list = new List<GameObject>();
                _items[path] = list;
                _used[path] = 0;
            }
            int n = _used[path];
            if (n == list.Count)
            {
                GameObject made = Models3D.Person(path, _parent, _tall);
                if (made == null) return null;
                list.Add(made);
            }
            GameObject go = list[n];
            _used[path] = n + 1;
            if (!go.activeSelf) go.SetActive(true);
            var mark = go.GetComponent<PersonScale>() ?? go.AddComponent<PersonScale>();
            if (mark.Base == 0f) mark.Base = go.transform.localScale.x;
            go.transform.localScale = Vector3.one * mark.Base * scale;
            return go;
        }

        public void End()
        {
            foreach (KeyValuePair<string, List<GameObject>> kv in _items)
            {
                int used = _used[kv.Key];
                for (int i = used; i < kv.Value.Count; i++)
                {
                    if (kv.Value[i].activeSelf) kv.Value[i].SetActive(false);
                }
            }
        }
    }

    /// <summary>풀에서 꺼낸 사람의 원래 축척(키 배율을 곱하는 기준).</summary>
    public sealed class PersonScale : MonoBehaviour
    {
        public float Base;
    }

    /// <summary>모델 하나의 렌더러·머티리얼 원래 색을 기억해 틴트를 바뀔 때만 넣는다.</summary>
    public sealed class ModelTint : MonoBehaviour
    {
        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private Color _last = new Color(-1f, 0f, 0f, 0f);
        private int _lastKey = -1;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private Texture _baseMap;

        /// <summary>머티리얼 텍스처 대신 쓸 텍스처(지붕 색 바꾼 colormap). 다음 Apply에서 넣는다.</summary>
        public void SetBaseMap(Texture tex)
        {
            _baseMap = tex;
            _lastKey = -2;
            Apply(_last.r < 0f ? Color.white : _last, null, 0);
        }

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
                    if (_baseMap != null) _block.SetTexture(BaseMap, _baseMap);
                    r.SetPropertyBlock(_block, i);
                }
            }
        }
    }
}
