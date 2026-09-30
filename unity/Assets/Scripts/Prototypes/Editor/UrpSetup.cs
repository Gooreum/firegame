using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FireGame.Prototypes.EditorTools
{
    /// <summary>
    /// URP 파이프라인 애셋·렌더러를 만들고 그래픽 설정과 모든 품질 단계에 꽂는다. 여러 번 돌려도 같은 애셋을 쓴다.
    /// 실행: tools/unity-check.sh urp-setup. 만든 Assets/Settings와 ProjectSettings는 저장소에 커밋한다.
    /// </summary>
    public static class UrpSetup
    {
        private const string Folder = "Assets/Settings";
        private const string RendererPath = Folder + "/FireGameRenderer.asset";
        private const string PipelinePath = Folder + "/FireGameURP.asset";
        private const string PostProcessPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        public static void Apply()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "Settings");

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            // 블룸 등 후처리 셰이더·텍스처 묶음(없으면 후처리가 꺼진다).
            if (renderer.postProcessData == null) renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessPath);
            EditorUtility.SetDirty(renderer);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            pipeline.msaaSampleCount = 4;
            pipeline.supportsHDR = true;
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
            Debug.Log("[UrpSetup] 완료: " + PipelinePath + (renderer.postProcessData != null ? " (후처리 있음)" : " (후처리 없음)"));
        }
    }
}
