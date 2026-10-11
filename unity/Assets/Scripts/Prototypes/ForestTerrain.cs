using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using FireGame.UnityLayer;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 숲 3D 땅(2026-10-12 샘플, docs §26): 그린 바닥 쿼드 대신 Kenney Nature Kit 타일을 1칸 격자에 깐다.
    /// 평지는 얇은 풀 타일, 개울 둑과 바위 언덕은 흙 옆면이 있는 블록(cliff_block_rock)이라 높낮이가 보인다.
    /// 개울 칸은 블록을 반 칸 내려 물을 채우고, 다리 둘·흙길·풀·꽃·돌 장식을 얹은 뒤 머티리얼별로 메시 하나로 합친다(폰: 타일 수천 개 → 드로우 콜 머티리얼 수만큼).
    /// 자리는 로직(SurvivorForest의 CreekY·Rock*·PathHalf)과 같다 — 걸을 수 있는 땅은 모두 높이 0이라 몸·불·바닥 그림 층은 그대로다.
    /// 위쪽은 −Z(Models3D.Place와 같다).
    /// </summary>
    public static class ForestTerrain
    {
        /// <summary>바위 언덕 높이(칸)와 개울 바닥 깊이.</summary>
        public const float RockHeight = 2f, CreekDepth = 0.9f, WaterDepth = 0.45f;

        private static readonly string[] Decor =
        {
            "grass", "grass_large", "grass_leafs", "grass_large", "grass", "flower_redA", "flower_yellowA", "flower_purpleA",
            "stone_smallA", "stone_smallC", "mushroom_red", "mushroom_tanGroup", "stump_round", "log", "plant_bush", "stone_largeA",
        };

        public static List<GameObject> Build(Transform parent, List<Structure> structures)
        {
            var temp = new GameObject("TerrainParts").transform;
            temp.SetParent(parent, false);
            int n = (int)SurvivorSim.ArenaSize;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    var c = new Vector3(x + 0.5f, y + 0.5f, 0f);
                    if (InRock(c))
                    {
                        // 언덕: 블록 두 단 + 바닥 한 단(가장자리 흙 옆면이 보인다).
                        for (int k = 0; k < (int)RockHeight; k++) Block(temp, c, -RockHeight + k);
                        continue;
                    }
                    if (InCreek(c))
                    {
                        Block(temp, c, CreekDepth);
                        continue;
                    }
                    if (NextToCreek(c))
                    {
                        // 둑: 옆면이 있는 블록(개울 쪽으로 흙벽이 드러난다).
                        Block(temp, c, 0f);
                        if (OnPath(c)) Models3D.Place("Nature/ground_pathTile", temp, c + new Vector3(0f, 0f, -0.005f), 1f, 1f, 0f, out _);
                        continue;
                    }
                    Models3D.Place(OnPath(c) ? "Nature/ground_pathTile" : "Nature/ground_grass", temp, c, 1f, 1f, 0f, out _);
                }
            }
            foreach (float bx in SurvivorForest.BridgeXs)
            {
                // 다리: 개울을 남북으로 건너는 나무 다리(폭 3, 길이 개울 + 둑 한 칸씩).
                Models3D.Place("Nature/bridge_wood", temp, new Vector3(bx, SurvivorForest.CreekY, 0.12f), SurvivorForest.BridgeHalf * 2f + 0.2f, (SurvivorForest.CreekHalf * 2f) + 1.6f, 90f, out _, 0.5f);
            }
            Scatter(temp, structures);
            List<GameObject> merged = Merge(temp, parent);
            UiKit.Discard(temp.gameObject);

            // 물: 개울 바닥 위 반투명 아닌 판(Lit, 반짝임). 다리 아래까지 한 장.
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Quad);
            water.name = "Creek";
            Collider col = water.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(col);
            else Object.DestroyImmediate(col);
            water.transform.SetParent(parent, false);
            water.transform.localPosition = new Vector3(SurvivorSim.ArenaSize / 2f, SurvivorForest.CreekY, WaterDepth);
            water.transform.localScale = new Vector3(SurvivorSim.ArenaSize, SurvivorForest.CreekHalf * 2f, 1f);
            var wm = new Material(Models3D.LitShader) { name = "CreekWater" };
            wm.SetColor("_BaseColor", new Color(0.28f, 0.58f, 0.82f));
            wm.SetFloat("_Smoothness", 0.85f);
            water.GetComponent<MeshRenderer>().sharedMaterial = wm;
            merged.Add(water);

            // 맵 밖: 짙은 숲색 바닥을 넓게(가장자리가 허공으로 안 보이게).
            GameObject outside = GameObject.CreatePrimitive(PrimitiveType.Quad);
            outside.name = "Outside";
            col = outside.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(col);
            else Object.DestroyImmediate(col);
            outside.transform.SetParent(parent, false);
            // 개울 블록 바닥(CreekDepth + 1)보다 아래: 위에 두면 개울을 덮는다.
            outside.transform.localPosition = new Vector3(SurvivorSim.ArenaSize / 2f, SurvivorSim.ArenaSize / 2f, CreekDepth + 1.05f);
            outside.transform.localScale = new Vector3(SurvivorSim.ArenaSize * 3f, SurvivorSim.ArenaSize * 3f, 1f);
            var om = new Material(Models3D.LitShader) { name = "Outside" };
            om.SetColor("_BaseColor", new Color(0.16f, 0.3f, 0.16f));
            om.SetFloat("_Smoothness", 0f);
            outside.GetComponent<MeshRenderer>().sharedMaterial = om;
            merged.Add(outside);
            return merged;
        }

        /// <summary>윗면이 top(−Z가 위)에 오는 1×1×1 흙 블록. Place의 at.z는 바닥이라 한 칸 아래(+1)를 준다.</summary>
        private static void Block(Transform p, Vector3 c, float top)
        {
            Models3D.Place("Nature/cliff_block_rock", p, new Vector3(c.x, c.y, top + 1f), 1f, 1f, 0f, out _);
        }

        private static bool InRock(Vector3 c)
        {
            return c.x > SurvivorForest.RockX0 && c.x < SurvivorForest.RockX1 && c.y > SurvivorForest.RockY0 && c.y < SurvivorForest.RockY1;
        }

        private static bool InCreek(Vector3 c)
        {
            return Mathf.Abs(c.y - SurvivorForest.CreekY) < SurvivorForest.CreekHalf;
        }

        private static bool NextToCreek(Vector3 c)
        {
            return !InCreek(c) && Mathf.Abs(c.y - SurvivorForest.CreekY) < SurvivorForest.CreekHalf + 1f;
        }

        private static bool OnPath(Vector3 c)
        {
            float mid = SurvivorSim.ArenaSize / 2f;
            return Mathf.Abs(c.x - mid) < SurvivorForest.PathHalf || Mathf.Abs(c.y - mid) < SurvivorForest.PathHalf;
        }

        /// <summary>풀·꽃·돌·버섯·그루터기를 해시로 흩뿌린다: 길·개울·건물·나무 발자국 밖. 언덕 위엔 돌·덤불.</summary>
        private static void Scatter(Transform p, List<Structure> structures)
        {
            int n = (int)SurvivorSim.ArenaSize;
            for (int i = 0; i < n * n * 3; i++)
            {
                float x = Hash(i * 3 + 1) * n;
                float y = Hash(i * 3 + 2) * n;
                var c = new Vector3(x, y, 0f);
                if (Hash(i * 3 + 3) > 0.22f) continue;
                if (InCreek(c) || NextToCreek(c) && Mathf.Abs(y - SurvivorForest.CreekY) < SurvivorForest.CreekHalf + 0.3f || OnPath(c)) continue;
                var at = new Vec2(x, y);
                if (structures.Exists(s => s.Kind != StructureKind.Water && s.Within(at, s.Kind == StructureKind.Tree ? 0.2f : 0.6f))) continue;
                bool rock = InRock(c);
                string kind = rock ? (Hash(i + 7) < 0.5f ? "stone_largeA" : "plant_bush") : Decor[(int)(Hash(i * 7 + 5) * Decor.Length) % Decor.Length];
                float k = 2.2f + Hash(i * 11) * 1.2f;
                float size = kind == "stone_largeA" || kind == "log" || kind == "plant_bush" ? 1.0f : 0.45f;
                c.z = rock ? -RockHeight : 0f;
                Models3D.Place("Nature/" + kind, p, c, size * k * 0.5f, size * k * 0.5f, Hash(i * 13) * 360f, out _);
            }
        }

        private static readonly Dictionary<Material, Material> Recolored = new Dictionary<Material, Material>();

        /// <summary>Nature Kit 풀·흙 색이 화면 보정(PixelGrade)을 지나면 민트색으로 떠서, 땅 색만 숲답게 눌러 쓴다(원본 머티리얼은 나무가 같이 쓰니 복사본).</summary>
        private static Material Recolor(Material m)
        {
            if (m == null) return null;
            if (Recolored.TryGetValue(m, out Material done) && done != null) return done;
            Color? c = m.name.StartsWith("grass") ? new Color(0.36f, 0.56f, 0.24f)
                : m.name.StartsWith("dirtDark") ? new Color(0.36f, 0.25f, 0.16f)
                : m.name.StartsWith("dirt") ? new Color(0.55f, 0.39f, 0.25f)
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

        private static float Hash(int i)
        {
            uint h = (uint)i * 2654435761u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }

        /// <summary>MeshFilter를 머티리얼별로 모아 CombineMeshes(32비트 인덱스). 머티리얼 하나당 GameObject 하나(그림자 받기·드리우기).</summary>
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
            if (Models3D.LogFit) Debug.Log("[ForestTerrain] 합친 메시 " + result.Count + "개");
            return result;
        }
    }
}
