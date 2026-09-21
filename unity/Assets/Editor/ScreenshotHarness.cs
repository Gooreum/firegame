using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Sim;
using FireGame.UnityLayer;
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

        /// <summary>장면이 필요하면 바꾼다(넓은 화면 확인용). 장면마다 기본값으로 되돌린다.</summary>
        private static int _captureWidth = Width;

        /// <summary>장면 이름 → 준비 함수. 준비 함수는 찍을 카메라를 돌려준다.</summary>
        private static readonly List<KeyValuePair<string, Func<Camera>>> Scenes =
            new List<KeyValuePair<string, Func<Camera>>>
            {
                new KeyValuePair<string, Func<Camera>>("00_smoke", SmokeScene),
                new KeyValuePair<string, Func<Camera>>("10_residential_start", ResidentialStart),
                new KeyValuePair<string, Func<Camera>>("11_residential_fire", ResidentialFire),
                new KeyValuePair<string, Func<Camera>>("12_shopping_electric", ShoppingElectric),
                new KeyValuePair<string, Func<Camera>>("13_gasstation_oil", GasStationOil),
                new KeyValuePair<string, Func<Camera>>("14_spray_and_firebreak", SprayAndFirebreak),
                new KeyValuePair<string, Func<Camera>>("20_hud", MissionWithHud),
                new KeyValuePair<string, Func<Camera>>("30_map_new_game", MapNewGame),
                new KeyValuePair<string, Func<Camera>>("31_map_progress", MapProgress),
                new KeyValuePair<string, Func<Camera>>("32_briefing", Briefing),
                new KeyValuePair<string, Func<Camera>>("33_result_win", ResultWin),
                new KeyValuePair<string, Func<Camera>>("34_result_lose", ResultLose),
                new KeyValuePair<string, Func<Camera>>("35_shop", ShopOpen),
                new KeyValuePair<string, Func<Camera>>("36_map_wide_screen", MapWide),
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
                    _captureWidth = Width;
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
            int width = _captureWidth;
            var target = new RenderTexture(width, Height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;

            try
            {
                // 캔버스는 카메라 출력 크기에 맞춰 배치되므로, 렌더 대상을 먼저 지정한 뒤 레이아웃을 갱신한다.
                // 순서가 반대면 배치 모드의 기본 화면 크기로 배치돼 UI가 어긋난다.
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture.active = target;
                camera.aspect = (float)width / Height;
                var image = new Texture2D(width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, Height), 0, 0);
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

        /// <summary>현장용 카메라. 게임과 같은 크기로 보여 준다.</summary>
        private static Camera WorldCamera()
        {
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = GameRoot.CameraHalfHeight;
            camera.aspect = (float)Width / Height;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
            return camera;
        }

        /// <summary>판을 idle 상태로 진행시킨다(불이 번지게).</summary>
        private static void Advance(StageRunner runner, float seconds)
        {
            for (float t = 0f; t < seconds && !runner.IsOver; t += 0.1f)
            {
                runner.Update(0.1f, default(StageInput));
            }
        }

        private static Camera Show(StageRunner runner, float time, Vector2? focusCell = null)
        {
            Camera camera = WorldCamera();
            var root = new GameObject("Root").transform;
            var view = new MissionWorldView(root, runner);
            view.Refresh(time, 0.05f);

            Vector3 focus = focusCell.HasValue
                ? view.CellCenter((int)focusCell.Value.x, (int)focusCell.Value.y)
                : view.PlayerWorld;
            view.FrameCamera(camera, focus);
            return camera;
        }

        private static StageRunner Runner(StageDef stage, params int[] equipment)
        {
            return new StageRunner(stage, new List<int>(equipment));
        }

        private static Camera ResidentialStart()
        {
            return Show(Runner(StageCatalog.Residential, EquipmentId.Bucket), 0f);
        }

        private static Camera ResidentialFire()
        {
            StageRunner runner = Runner(StageCatalog.Residential, EquipmentId.Bucket);
            Advance(runner, 7f);
            return Show(runner, 7f, new Vector2(21, 9));
        }

        private static Camera ShoppingElectric()
        {
            StageRunner runner = Runner(StageCatalog.Shopping, EquipmentId.Bucket, EquipmentId.Extinguisher);
            Advance(runner, 4f);
            return Show(runner, 4f, new Vector2(10, 7));
        }

        private static Camera GasStationOil()
        {
            StageRunner runner = Runner(StageCatalog.GasStation, EquipmentId.Bucket, EquipmentId.FoamExtinguisher);
            Advance(runner, 3f);
            return Show(runner, 3f, new Vector2(22, 10));
        }

        // ---- 게임과 같은 조립(ScreenDirector)으로 찍는 장면 ----

        private static Camera Direct(GameFlow flow)
        {
            Camera camera = WorldCamera();
            camera.aspect = (float)_captureWidth / Height;
            var root = new GameObject("Root").transform;
            Canvas canvas = UiKit.CreateCanvas(root, camera, "Canvas", 0);
            var director = new ScreenDirector(root, camera, canvas, flow);
            director.Sync(0.05f);
            return camera;
        }

        private static Camera MapNewGame()
        {
            return Direct(new GameFlow(SaveData.NewGame()));
        }

        private static Camera MapProgress()
        {
            SaveData save = SaveData.NewGame();
            save.Money = 1250;
            save.RecordResult(0, 2);
            return Direct(new GameFlow(save));
        }

        private static Camera Briefing()
        {
            var flow = new GameFlow(SaveData.NewGame());
            flow.SelectMission(0);
            return Direct(flow);
        }

        /// <summary>불을 모두 끄고 시민을 출구로 옮겨 이긴 상태를 만든다(봇 없이).</summary>
        private static Camera ResultWin()
        {
            var flow = new GameFlow(SaveData.NewGame());
            flow.SelectMission(0);
            flow.BeginMission();
            for (int i = 0; i < 30; i++) flow.Update(0.1f);

            StageRunner runner = flow.Runner;
            Civilian civilian = runner.Civilians[0];
            runner.Player.X = civilian.X;
            runner.Player.Y = civilian.Y;
            flow.Update(0.02f);

            FireGame.Core.Grid.GridPoint exit = runner.Exits[0];
            runner.Player.X = exit.X + 0.5f;
            runner.Player.Y = exit.Y + 0.5f;

            for (int i = 0; i < runner.Grid.Count; i++)
            {
                if (runner.Grid.Cells[i].State == FireGame.Core.Grid.CellState.Burning)
                {
                    runner.Grid.Cells[i].State = FireGame.Core.Grid.CellState.Intact;
                    runner.Grid.Cells[i].Heat = 0f;
                    runner.Grid.Cells[i].Wet = 1f;
                }
            }

            flow.Update(0.02f);
            if (flow.Screen != GameScreen.Result) throw new InvalidOperationException("승리 상태를 만들지 못했다: " + runner.Outcome);
            return Direct(flow);
        }

        private static Camera ResultLose()
        {
            var flow = new GameFlow(SaveData.NewGame());
            flow.SelectMission(0);
            flow.BeginMission();
            for (int i = 0; i < 5000 && flow.Screen == GameScreen.Playing; i++) flow.Update(0.1f);
            if (flow.Screen != GameScreen.Result) throw new InvalidOperationException("패배 상태를 만들지 못했다");
            return Direct(flow);
        }

        private static Camera ShopOpen()
        {
            SaveData save = SaveData.NewGame();
            save.Money = 600;
            save.RecordResult(0, 1);
            var flow = new GameFlow(save);
            flow.OpenShop();
            return Direct(flow);
        }

        private static Camera MapWide()
        {
            _captureWidth = 2340;
            SaveData save = SaveData.NewGame();
            save.RecordResult(0, 3);
            save.RecordResult(1, 1);
            return Direct(new GameFlow(save));
        }

        /// <summary>
        /// HUD를 올린 플레이 화면. 장비 4종 전부, 체력 60%, 2번 장비(CO2) 선택,
        /// 4번(폼)은 다 쓴 상태, 조이스틱을 오른쪽으로 민 상태.
        /// </summary>
        private static Camera MissionWithHud()
        {
            SaveData save = SaveData.NewGame();
            save.Unlocked.Add(EquipmentId.Extinguisher);
            save.Unlocked.Add(EquipmentId.Hose);
            save.Unlocked.Add(EquipmentId.FoamExtinguisher);
            var flow = new GameFlow(save);
            flow.SelectMission(0);
            flow.BeginMission();
            for (int i = 0; i < 40; i++) flow.Update(0.1f);

            flow.Runner.Player.Hp = GameConfig.PlayerMaxHp * 0.6f;
            flow.SelectSlot(1);
            flow.Runner.Player.Charges[1] = 9;
            flow.Runner.Player.Charges[3] = 0;

            Camera camera = WorldCamera();
            var root = new GameObject("Root").transform;
            var view = new MissionWorldView(root, flow.Runner);
            view.Refresh(flow.Elapsed, 0f);
            view.FrameCamera(camera, view.PlayerWorld);

            Canvas canvas = UiKit.CreateCanvas(root, camera, "Canvas", 0);
            var hud = new MissionHud(canvas, flow);
            hud.Joystick.SetKnob(new Vector2(0.8f, 0.2f));
            hud.Refresh();
            return camera;
        }

        /// <summary>벽 몇 칸을 미리 적셔 방화선을 치고, 양동이를 벽 쪽으로 막 쏜 순간.</summary>
        private static Camera SprayAndFirebreak()
        {
            StageRunner runner = Runner(StageCatalog.Residential, EquipmentId.Bucket);
            Advance(runner, 2f);

            for (int x = 14; x <= 17; x++)
            {
                Suppression.Apply(runner.Grid, x, 8, EquipmentCatalog.Bucket.Agent);
            }

            // 칸막이 바로 아래 실내에서 북쪽 벽을 조준한다.
            runner.Player.X = 16.5f;
            runner.Player.Y = 9.5f;
            runner.Player.Aim = AimDirection.N;

            Camera camera = WorldCamera();
            var view = new MissionWorldView(new GameObject("Root").transform, runner);
            view.Refresh(2f, 0f);
            runner.Update(0.01f, new StageInput { Fire = true, Slot = 0 });
            view.Refresh(2.05f, 0.05f);
            view.FrameCamera(camera, view.PlayerWorld);
            return camera;
        }

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
