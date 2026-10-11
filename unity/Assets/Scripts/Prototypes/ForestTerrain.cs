using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using FireGame.UnityLayer;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 숲 3D 땅(2026-10-12, docs §26): 높이 있는 지면 메시 한 장. 높이는 로직과 같은 SurvivorForest.Height
    /// (걸어 오르는 언덕 위 건물, 북서 바위 절벽, 솟은 맵 가장자리, 파인 개울). 색은 높이·기울기로 칠한 텍스처
    /// (평지 풀, 비탈 흙, 바위 꼭대기 돌, 개울 바닥 젖은 흙, 십자 흙길). 위에 다리 둘과 Nature Kit 풀·꽃·돌 장식을 얹어 머티리얼별로 합친다.
    /// 위쪽은 −Z(Models3D.Place와 같다).
    /// </summary>
    public static class ForestTerrain
    {
        /// <summary>메시가 덮는 범위(맵 밖까지 산등성이가 이어진다)와 격자 간격.</summary>
        private const float From = -14f, To = 74f, Step = 0.5f;

        /// <summary>개울 물 높이(땅 아래 칸).</summary>
        public const float WaterDepth = 0.5f;

        private const int TexSize = 1024;

        /// <summary>음영용 빛 방향(땅 좌표 x 동, y 북, z 위): 북서쪽 위에서.</summary>
        private static readonly Vector3 LightDir = new Vector3(-0.55f, 0.55f, 0.63f).normalized;

        private static readonly string[] Decor =
        {
            "grass", "grass_large", "grass_leafs", "grass_large", "grass", "flower_redA", "flower_yellowA", "flower_purpleA",
            "stone_smallA", "stone_smallC", "mushroom_red", "mushroom_tanGroup", "stump_round", "log", "plant_bush", "stone_largeA",
        };

        private static Texture2D _tex;

        public static List<GameObject> Build(Transform parent, List<Structure> structures)
        {
            var result = new List<GameObject> { Ground(parent), Water(parent) };
            var temp = new GameObject("TerrainParts").transform;
            temp.SetParent(parent, false);
            foreach (float bx in SurvivorForest.BridgeXs)
            {
                // 다리: 개울을 남북으로 건너는 나무 다리(폭 3, 길이 개울 + 둑).
                Models3D.Place("Nature/bridge_wood", temp, new Vector3(bx, SurvivorForest.CreekY, 0.12f), (SurvivorForest.BridgeHalf * 2f) + 0.2f, (SurvivorForest.CreekHalf * 2f) + 1.6f, 90f, out _, 0.5f);
            }
            Scatter(temp, structures);
            result.AddRange(Merge(temp, parent));
            UiKit.Discard(temp.gameObject);
            return result;
        }

        private static float H(float x, float y)
        {
            return SurvivorForest.Height(x, y);
        }

        /// <summary>기울기 크기(칸당 높이).</summary>
        private static float Slope(float x, float y)
        {
            const float e = 0.3f;
            float gx = (H(x + e, y) - H(x - e, y)) / (2f * e);
            float gy = (H(x, y + e) - H(x, y - e)) / (2f * e);
            return Mathf.Sqrt((gx * gx) + (gy * gy));
        }

        private static GameObject Ground(Transform parent)
        {
            int n = Mathf.RoundToInt((To - From) / Step) + 1;
            var verts = new Vector3[n * n];
            var uvs = new Vector2[n * n];
            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    float x = From + (i * Step), y = From + (j * Step);
                    verts[(j * n) + i] = new Vector3(x, y, -H(x, y));
                    uvs[(j * n) + i] = new Vector2((x - From) / (To - From), (y - From) / (To - From));
                }
            }
            var tris = new int[(n - 1) * (n - 1) * 6];
            int t = 0;
            for (int j = 0; j < n - 1; j++)
            {
                for (int i = 0; i < n - 1; i++)
                {
                    int a = (j * n) + i, b = a + 1, c = a + n, d = c + 1;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = "ForestGround" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("ForestGround");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mat = new Material(Models3D.LitShader) { name = "ForestGround" };
            mat.SetTexture("_BaseMap", Paint());
            mat.SetFloat("_Smoothness", 0f);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>높이·기울기로 칠한 땅 텍스처(한 번만 만든다).</summary>
        private static Texture2D Paint()
        {
            if (_tex != null) return _tex;
            var px = new Color32[TexSize * TexSize];
            float mid = SurvivorSim.ArenaSize / 2f;
            var grassLo = new Color(0.27f, 0.42f, 0.2f);
            var grassHi = new Color(0.4f, 0.55f, 0.26f);
            var forest = new Color(0.17f, 0.3f, 0.15f);
            var dirt = new Color(0.5f, 0.37f, 0.24f);
            var wet = new Color(0.25f, 0.2f, 0.15f);
            var rock = new Color(0.52f, 0.52f, 0.5f);
            for (int v = 0; v < TexSize; v++)
            {
                for (int u = 0; u < TexSize; u++)
                {
                    float x = From + ((u + 0.5f) / TexSize * (To - From));
                    float y = From + ((v + 0.5f) / TexSize * (To - From));
                    float h = H(x, y);
                    float slope = Slope(x, y);
                    float n1 = Noise(x * 0.25f, y * 0.25f), n2 = Noise(x * 1.3f + 50f, y * 1.3f + 50f);
                    Color c = Color.Lerp(grassLo, grassHi, (n1 * 0.7f) + (n2 * 0.3f));
                    // 높을수록 볕이 들어 밝고, 맵 밖 산등성이는 짙은 숲.
                    c = Color.Lerp(c, c * 1.15f, Mathf.Clamp01(h / 2f));
                    bool outside = x < 0f || y < 0f || x > SurvivorSim.ArenaSize || y > SurvivorSim.ArenaSize;
                    if (outside) c = Color.Lerp(c, forest, Mathf.Clamp01(h / 2.5f));
                    // 흙길(맵 안): 가장자리는 노이즈로 들쭉날쭉.
                    float path = Mathf.Min(Mathf.Abs(x - mid), Mathf.Abs(y - mid));
                    float edge = SurvivorForest.PathHalf + ((n2 - 0.5f) * 0.6f);
                    if (!outside && path < edge) c = Color.Lerp(c, dirt * (0.9f + (0.15f * n1)), Mathf.Clamp01((edge - path) / 0.4f));
                    // 비탈은 흙이 드러나고, 가파르면(절벽) 바위.
                    // 완만한 비탈은 풀 그대로(음영이 높이를 말한다), 가파른 곳만 흙이 군데군데 드러난다.
                    c = Color.Lerp(c, dirt * (0.85f + (0.2f * n2)), Mathf.Clamp01((slope - 1.1f) / 0.6f) * (0.35f + (0.5f * n2)));
                    c = Color.Lerp(c, rock * (0.85f + (0.25f * n1)), Mathf.Clamp01((slope - 2.4f) / 1f));
                    // 바위 언덕 꼭대기는 돌밭.
                    if (x > SurvivorForest.RockX0 && x < SurvivorForest.RockX1 && y > SurvivorForest.RockY0 && y < SurvivorForest.RockY1)
                        c = Color.Lerp(c, rock * (0.8f + (0.3f * n2)), 0.75f);
                    // 지도식 음영: 북서쪽에서 해가 든다고 보고 비탈을 밝히고 어둡게 굽는다(평지 = 1).
                    const float e = 0.3f;
                    float gx = (H(x + e, y) - H(x - e, y)) / (2f * e), gy = (H(x, y + e) - H(x, y - e)) / (2f * e);
                    var nrm = new Vector3(-gx, -gy, 1f).normalized;
                    float shade = Vector3.Dot(nrm, LightDir) / LightDir.z;
                    c *= Mathf.Clamp(0.4f + (0.6f * shade), 0.4f, 1.45f);
                    // 개울 바닥은 젖은 흙.
                    if (h < -0.05f) c = Color.Lerp(c, wet, Mathf.Clamp01(-h / 0.5f));
                    c.a = 1f;
                    px[(v * TexSize) + u] = c;
                }
            }
            _tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, true)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "ForestGround",
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            _tex.SetPixels32(px);
            _tex.Apply(true);
            return _tex;
        }

        private static GameObject Water(Transform parent)
        {
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Quad);
            water.name = "Creek";
            Collider col = water.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(col);
            else Object.DestroyImmediate(col);
            water.transform.SetParent(parent, false);
            water.transform.localPosition = new Vector3(SurvivorSim.ArenaSize / 2f, SurvivorForest.CreekY, WaterDepth);
            water.transform.localScale = new Vector3(To - From, (SurvivorForest.CreekHalf * 2f) + 0.6f, 1f);
            var wm = new Material(Models3D.LitShader) { name = "CreekWater" };
            wm.SetColor("_BaseColor", new Color(0.28f, 0.58f, 0.82f));
            wm.SetFloat("_Smoothness", 0.85f);
            water.GetComponent<MeshRenderer>().sharedMaterial = wm;
            return water;
        }

        /// <summary>풀·꽃·돌·버섯·그루터기를 해시로 흩뿌린다(땅 높이에): 길·개울·가파른 비탈·건물·나무 발자국 밖. 바위 언덕 위엔 돌·덤불.</summary>
        private static void Scatter(Transform p, List<Structure> structures)
        {
            float n = SurvivorSim.ArenaSize;
            float mid = n / 2f;
            for (int i = 0; i < 9000; i++)
            {
                if (Hash(i * 3 + 3) > 0.25f) continue;
                float x = Hash(i * 3 + 1) * n;
                float y = Hash(i * 3 + 2) * n;
                float h = H(x, y);
                if (h < -0.02f || Slope(x, y) > 1.2f) continue;
                if (Mathf.Abs(y - SurvivorForest.CreekY) < SurvivorForest.CreekHalf + 0.5f) continue;
                if (Mathf.Min(Mathf.Abs(x - mid), Mathf.Abs(y - mid)) < SurvivorForest.PathHalf + 0.2f) continue;
                var at = new Vec2(x, y);
                if (structures.Exists(s => s.Kind != StructureKind.Water && s.Within(at, s.Kind == StructureKind.Tree ? 0.2f : 0.6f))) continue;
                bool rock = x > SurvivorForest.RockX0 && x < SurvivorForest.RockX1 && y > SurvivorForest.RockY0 && y < SurvivorForest.RockY1;
                string kind = rock ? (Hash(i + 7) < 0.5f ? "stone_largeA" : "plant_bush") : Decor[(int)(Hash(i * 7 + 5) * Decor.Length) % Decor.Length];
                float k = 2.2f + Hash(i * 11) * 1.2f;
                float size = kind == "stone_largeA" || kind == "log" || kind == "plant_bush" ? 1.0f : 0.45f;
                Models3D.Place("Nature/" + kind, p, new Vector3(x, y, -h + 0.05f), size * k * 0.5f, size * k * 0.5f, Hash(i * 13) * 360f, out _);
            }
        }

        private static float Noise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - (2f * fx));
            fy = fy * fy * (3f - (2f * fy));
            float a = Mathf.Lerp(Hash2(ix, iy), Hash2(ix + 1, iy), fx);
            float b = Mathf.Lerp(Hash2(ix, iy + 1), Hash2(ix + 1, iy + 1), fx);
            return Mathf.Lerp(a, b, fy);
        }

        private static float Hash2(int x, int y)
        {
            return Hash((x * 73856093) ^ (y * 19349663));
        }

        private static float Hash(int i)
        {
            uint h = (uint)i * 2654435761u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }

        private static readonly Dictionary<Material, Material> Recolored = new Dictionary<Material, Material>();

        /// <summary>Nature Kit 풀·돌·나무 색이 화면 보정(PixelGrade)을 지나면 민트·흰색으로 떠서 숲답게 눌러 쓴다(원본은 나무가 같이 쓰니 복사본).</summary>
        private static Material Recolor(Material m)
        {
            if (m == null) return null;
            if (Recolored.TryGetValue(m, out Material done) && done != null) return done;
            Color? c = m.name.StartsWith("grass") ? new Color(0.36f, 0.56f, 0.24f)
                : m.name.StartsWith("stone") ? new Color(0.56f, 0.57f, 0.6f)
                : m.name == "wood" || m.name.StartsWith("wood ") ? new Color(0.62f, 0.43f, 0.26f)
                : (Color?)null;
            Material result = m;
            if (c.HasValue)
            {
                result = new Material(m) { name = m.name + "_Forest" };
                result.SetColor("_BaseColor", c.Value);
            }
            Recolored[m] = result;
            return result;
        }

        /// <summary>MeshFilter를 머티리얼별로 모아 CombineMeshes(32비트 인덱스). 머티리얼 하나당 GameObject 하나.</summary>
        private static List<GameObject> Merge(Transform parts, Transform parent)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            Matrix4x4 toParent = parent.worldToLocalMatrix;
            foreach (MeshFilter mf in parts.GetComponentsInChildren<MeshFilter>())
            {
                var r = mf.GetComponent<MeshRenderer>();
                if (r == null || mf.sharedMesh == null) continue;
                for (int s = 0; s < mf.sharedMesh.subMeshCount && s < r.sharedMaterials.Length; s++)
                {
                    Material m = Recolor(r.sharedMaterials[s]);
                    if (m == null) continue;
                    if (!groups.TryGetValue(m, out List<CombineInstance> list))
                    {
                        list = new List<CombineInstance>();
                        groups[m] = list;
                    }
                    list.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = s, transform = toParent * mf.transform.localToWorldMatrix });
                }
            }
            var result = new List<GameObject>();
            foreach (KeyValuePair<Material, List<CombineInstance>> g in groups)
            {
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = "Terrain_" + g.Key.name };
                mesh.CombineMeshes(g.Value.ToArray(), true, true);
                mesh.RecalculateBounds();
                var go = new GameObject(mesh.name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = g.Key;
                result.Add(go);
            }
            return result;
        }
    }
}
