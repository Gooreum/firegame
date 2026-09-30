using System.Text;
using UnityEditor;
using UnityEngine;

namespace FireGame.Prototypes.EditorTools
{
    /// <summary>
    /// Resources/Models 아래 FBX(Kenney·Quaternius) 가져오기 설정. 머티리얼은 URP가 Lit으로 바꿔 만들고,
    /// 사람은 Legacy 애니메이션(Animation 컴포넌트, 모든 클립 반복)으로 가져와 런타임에 CrossFade로 바꾼다.
    /// 확인: tools/unity-check.sh models (모델마다 머티리얼·셰이더·클립·크기를 찍는다).
    /// </summary>
    public sealed class ModelImport : AssetPostprocessor
    {
        private const string Root = "Assets/Resources/Models/";

        private bool Ours
        {
            get { return assetPath.StartsWith(Root) && assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase); }
        }

        private bool Person
        {
            get { return assetPath.Contains("/People/"); }
        }

        private void OnPreprocessModel()
        {
            if (!Ours) return;
            var m = (ModelImporter)assetImporter;
            m.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            m.materialLocation = ModelImporterMaterialLocation.InPrefab;
            m.importCameras = false;
            m.importLights = false;
            m.importBlendShapes = false;
            m.importAnimation = Person;
            m.animationType = Person ? ModelImporterAnimationType.Legacy : ModelImporterAnimationType.None;
        }

        private void OnPreprocessAnimation()
        {
            if (!Ours || !Person) return;
            var m = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = m.defaultClipAnimations;
            foreach (ModelImporterClipAnimation c in clips)
            {
                c.loopTime = true;
                c.wrapMode = WrapMode.Loop;
            }
            m.clipAnimations = clips;
        }

        /// <summary>배치 확인용: 모델마다 머티리얼(셰이더)·클립·크기를 로그로.</summary>
        public static void Report()
        {
            var sb = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Resources/Models" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null)
                {
                    sb.AppendLine("[Models] 로드 실패 " + path);
                    continue;
                }
                var bounds = new Bounds();
                bool first = true;
                sb.Append("[Models] ").Append(path.Substring(Root.Length));
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                {
                    Bounds b = r is SkinnedMeshRenderer s ? s.sharedMesh.bounds : r.bounds;
                    if (first) bounds = b;
                    else bounds.Encapsulate(b);
                    first = false;
                    foreach (Material mat in r.sharedMaterials)
                    {
                        if (mat == null) continue;
                        sb.Append(" | ").Append(mat.name).Append('(').Append(mat.shader.name).Append(')');
                    }
                }
                sb.Append(" | 크기 ").Append(bounds.size.ToString("F2"));
                if (go.GetComponent<Animation>() != null)
                {
                    sb.Append(" | 클립");
                    foreach (Object a in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        if (a is AnimationClip clip && !clip.name.StartsWith("__preview__")) sb.Append(' ').Append(clip.name).Append(clip.isLooping ? "(반복)" : "");
                    }
                }
                sb.AppendLine();
            }
            Debug.Log(sb.ToString());
            Debug.Log("[Models] 완료");
        }
    }
}
