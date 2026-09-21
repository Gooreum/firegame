using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace FireGame.EditorTools
{
    /// <summary>
    /// 배치 모드에서 화면을 띄워 PNG로 찍는다. 사람이 에디터를 열지 않아도
    /// "지금 화면이 어떻게 보이는가"를 확인할 수 있게 하는 검증 도구다.
    ///
    /// 실행: Unity -batchmode -projectPath unity -executeMethod FireGame.EditorTools.ScreenshotHarness.CaptureAll -shotDir 경로
    /// (-nographics를 붙이면 렌더링이 안 되므로 붙이지 않는다.)
    ///
    /// 장면마다 새 씬을 만들고, 준비 함수가 화면을 구성한 뒤 카메라를 RenderTexture로 렌더해 저장한다.
    /// </summary>
    public static class ScreenshotHarness
    {
        public const int Width = 1920;
        public const int Height = 1080;

        /// <summary>장면 이름 → 준비 함수. 준비 함수는 찍을 카메라를 돌려준다.</summary>
        private static readonly List<KeyValuePair<string, Func<Camera>>> Scenes =
            new List<KeyValuePair<string, Func<Camera>>>
            {
                new KeyValuePair<string, Func<Camera>>("00_smoke", SmokeScene),
            };

        public static void CaptureAll()
        {
            string directory = ArgumentAfter("-shotDir") ?? Path.Combine(Path.GetTempPath(), "firegame-shots");
            Directory.CreateDirectory(directory);

            int failures = 0;
            foreach (KeyValuePair<string, Func<Camera>> scene in Scenes)
            {
                try
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    Camera camera = scene.Value();
                    string path = Path.Combine(directory, scene.Key + ".png");
                    Capture(camera, path);
                    Debug.Log("[Harness] 저장: " + path);
                }
                catch (Exception e)
                {
                    failures++;
                    Debug.LogError("[Harness] 실패: " + scene.Key + " — " + e);
                }
            }

            Debug.Log("[Harness] 완료: " + (Scenes.Count - failures) + "/" + Scenes.Count);
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static void Capture(Camera camera, string path)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;

            try
            {
                // 캔버스는 카메라 출력 크기에 맞춰 배치되므로, 렌더 대상을 먼저 지정한 뒤 레이아웃을 갱신한다.
                // 순서가 반대면 배치 모드의 기본 화면 크기로 배치돼 UI가 어긋난다.
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture.active = target;
                var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static string ArgumentAfter(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }
            return null;
        }

        /// <summary>반드시 있어야 하는 리소스. 없으면 조용히 빈 화면을 찍지 않고 실패시킨다.</summary>
        public static T Require<T>(string path) where T : UnityEngine.Object
        {
            T asset = Resources.Load<T>(path);
            if (asset == null) throw new InvalidOperationException("리소스를 찾지 못했다: " + path);
            return asset;
        }

        // ------------------------------------------------------------------
        // 장면
        // ------------------------------------------------------------------

        /// <summary>
        /// 파이프라인 확인용: 스프라이트 로드, 카메라 렌더, 캔버스 렌더, 주아체 한글.
        /// </summary>
        private static Camera SmokeScene()
        {
            var cameraObject = new GameObject("Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1080f / 64f / 2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.transform.position = new Vector3(0f, 0f, -10f);

            var background = new GameObject("Map");
            background.AddComponent<SpriteRenderer>().sprite = Require<Sprite>("Art/Map/map_background");

            var truck = new GameObject("Truck");
            truck.AddComponent<SpriteRenderer>().sprite = Require<Sprite>("Art/Vehicles/firetruck");
            truck.transform.position = new Vector3(-10f, -5f, -1f);

            var canvasObject = new GameObject("Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Width, Height);

            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(canvasObject.transform, false);
            var text = textObject.AddComponent<Text>();
            text.font = Require<Font>("Fonts/Jua-Regular");
            text.fontSize = 72;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = "출동! 햇살동 주택가 화재 신고 1234";
            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = new Vector2(0f, 0.8f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            textObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);

            return camera;
        }
    }
}
