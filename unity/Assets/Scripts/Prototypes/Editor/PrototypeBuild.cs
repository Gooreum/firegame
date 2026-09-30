using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FireGame.Prototypes.EditorTools
{
    /// <summary>
    /// 시험판 C를 아이폰용 Xcode 프로젝트로 뽑는다. 빌드 표시(FIREGAME_PROTO_BUILD)가 붙어 켜자마자 시험판 C가 뜬다.
    /// 실행: tools/ios-install.sh (빌드 → 서명 → 폰 설치 → 실행). 따로 쓸 때는 tools/unity-check.sh ios.
    /// 실제 프로젝트가 아니라 복사본(tools/.unity-ios)에서 돌아서 PlayerSettings·빌드 대상 변경이 저장소에 남지 않는다.
    /// </summary>
    public static class PrototypeBuild
    {
        /// <summary>빈 부트 씬. 시험판은 RuntimeInitializeOnLoad로 뜨므로 씬은 비어 있으면 된다.</summary>
        private const string BootScene = "Assets/Scripts/Prototypes/ProtoBoot.unity";

        public const string BundleId = "com.mingu.firegame.proto";
        public const string AppleTeam = "LHW4ZX343L";

        public static void BuildIOS()
        {
            try
            {
                string path = ArgValue("-buildPath") ?? System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Builds/ios"));
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScene) == null)
                {
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    EditorSceneManager.SaveScene(scene, BootScene);
                }

                PlayerSettings.companyName = "MINGU";
                PlayerSettings.productName = "불끄기 시험판";
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
                // 가로 두 방향만(폰을 어느 쪽으로 눕혀도 된다).
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
                PlayerSettings.iOS.appleDeveloperTeamID = AppleTeam;
                PlayerSettings.iOS.appleEnableAutomaticSigning = true;
                PlayerSettings.iOS.targetOSVersionString = "15.0";

                // tools/ios-install.sh --stage N: 첫 실행 스테이지를 박아 넣는다. 복사본 프로젝트라 저장소엔 안 남고,
                // 다음 빌드의 rsync --delete가 지운다.
                const string startFile = "Assets/Resources/ProtoStartStage.txt";
                string start = ArgValue("-startStage");
                if (!string.IsNullOrEmpty(start))
                {
                    System.IO.File.WriteAllText(startFile, start.Trim());
                    AssetDatabase.ImportAsset(startFile);
                    Debug.Log("[ProtoBuild] 시작 스테이지 " + start);
                }

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { BootScene },
                    target = BuildTarget.iOS,
                    targetGroup = BuildTargetGroup.iOS,
                    locationPathName = path,
                    extraScriptingDefines = new[] { "FIREGAME_PROTO_BUILD" },
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                Debug.Log("[ProtoBuild] " + report.summary.result + " → " + path + " (" + report.summary.totalErrors + " 오류)");
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogError("[ProtoBuild] 실패 — " + e);
                EditorApplication.Exit(1);
            }
        }

        private static string ArgValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
