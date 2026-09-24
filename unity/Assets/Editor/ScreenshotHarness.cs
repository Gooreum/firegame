using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
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
                new KeyValuePair<string, Func<Camera>>("01_generated_art", GeneratedArt),
                new KeyValuePair<string, Func<Camera>>("10_residential_start", ResidentialStart),
                new KeyValuePair<string, Func<Camera>>("11_residential_fire", ResidentialFire),
                new KeyValuePair<string, Func<Camera>>("12_shopping_electric", ShoppingElectric),
                new KeyValuePair<string, Func<Camera>>("13_gasstation_oil", GasStationOil),
                new KeyValuePair<string, Func<Camera>>("14_spray_and_firebreak", SprayAndFirebreak),
                new KeyValuePair<string, Func<Camera>>("15_suit_co2", SuitAndCo2),
                new KeyValuePair<string, Func<Camera>>("16_suits_lineup", SuitsLineup),
                new KeyValuePair<string, Func<Camera>>("17_steam", SteamAfterPuttingOut),
                new KeyValuePair<string, Func<Camera>>("20_hud", MissionWithHud),
                new KeyValuePair<string, Func<Camera>>("30_map_new_game", MapNewGame),
                new KeyValuePair<string, Func<Camera>>("31_map_progress", MapProgress),
                new KeyValuePair<string, Func<Camera>>("32_briefing", Briefing),
                new KeyValuePair<string, Func<Camera>>("33_result_win", ResultWin),
                new KeyValuePair<string, Func<Camera>>("34_result_lose", ResultLose),
                new KeyValuePair<string, Func<Camera>>("35_shop", ShopOpen),
                new KeyValuePair<string, Func<Camera>>("36_map_wide_screen", MapWide),
                new KeyValuePair<string, Func<Camera>>("37_briefing_need_gear", BriefingNeedGear),
                new KeyValuePair<string, Func<Camera>>("38_briefing_ready", BriefingReady),
                new KeyValuePair<string, Func<Camera>>("39_result_need_gear", ResultNeedGear),
                new KeyValuePair<string, Func<Camera>>("43_map_need_gear", MapNeedGear),
                new KeyValuePair<string, Func<Camera>>("44_spray_level10", SprayLevel10),
                new KeyValuePair<string, Func<Camera>>("45_result_record", ResultRecord),
                new KeyValuePair<string, Func<Camera>>("46_aim_good", AimGood),
                new KeyValuePair<string, Func<Camera>>("47_aim_backfire", AimBackfire),
                new KeyValuePair<string, Func<Camera>>("48_rescue_ready", RescueReady),
                new KeyValuePair<string, Func<Camera>>("49_rescue_carry", RescueCarry),
                new KeyValuePair<string, Func<Camera>>("50_shop_high_level", ShopHighLevel),
                new KeyValuePair<string, Func<Camera>>("52_inside_shop", InsideShop),
                new KeyValuePair<string, Func<Camera>>("53_inside_rescue", InsideRescue),
                new KeyValuePair<string, Func<Camera>>("40_warehouse_fire", WarehouseFire),
                new KeyValuePair<string, Func<Camera>>("41_factory_mixed", FactoryMixed),
                new KeyValuePair<string, Func<Camera>>("42_harbor_finale", HarborFinale),
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

        /// <summary>폼 소화기로 기름 불을 겨눈다 — 조준 칸이 초록(잘 듣는다).</summary>
        private static Camera AimGood()
        {
            return AimAtOil(EquipmentId.FoamExtinguisher);
        }

        /// <summary>양동이(물)로 같은 기름 불을 겨눈다 — 조준 칸이 빨강(역효과).</summary>
        private static Camera AimBackfire()
        {
            return AimAtOil(EquipmentId.Bucket);
        }

        /// <summary>주유소 기름 불 바로 서쪽에 서서 동쪽을 겨눈 장면.</summary>
        private static Camera AimAtOil(int equipmentId)
        {
            StageRunner runner = Runner(StageCatalog.GasStation, equipmentId);
            Advance(runner, 3f);

            // 부채꼴 세 칸이 모두 타는 기름이 되도록, 위아래도 기름인 자리를 고른다.
            for (int y = 1; y < runner.Grid.Height - 1; y++)
            {
                for (int x = 1; x < runner.Grid.Width; x++)
                {
                    if (!BurningOil(runner, x, y) || !BurningOil(runner, x, y - 1) || !BurningOil(runner, x, y + 1)) continue;
                    if (!FireGame.Core.Grid.Materials.Of(runner.Grid[x - 1, y].Material).Walkable) continue;

                    runner.Player.X = x - 1 + 0.5f;
                    runner.Player.Y = y + 0.5f;
                    runner.Update(0.016f, new StageInput { MoveX = 1f, Slot = 0 });
                    return Show(runner, 0f, new Vector2(x, y));
                }
            }

            throw new InvalidOperationException("주유소에서 타는 기름 칸을 찾지 못했다");
        }

        private static bool BurningOil(StageRunner runner, int x, int y)
        {
            FireGame.Core.Grid.Cell cell = runner.Grid[x, y];
            return cell.State == FireGame.Core.Grid.CellState.Burning
                   && FireGame.Core.Grid.Materials.Of(cell.Material).Class == FireGame.Core.Grid.FireClass.B;
        }

        private static Camera GasStationOil()
        {
            StageRunner runner = Runner(StageCatalog.GasStation, EquipmentId.Bucket, EquipmentId.FoamExtinguisher);
            Advance(runner, 3f);
            return Show(runner, 3f, new Vector2(22, 10));
        }

        private static Camera WarehouseFire()
        {
            StageRunner runner = Runner(StageCatalog.Warehouse, EquipmentId.Bucket, EquipmentId.Hose);
            Advance(runner, 6f);
            return Show(runner, 6f, new Vector2(24, 8));
        }

        private static Camera FactoryMixed()
        {
            StageRunner runner = Runner(StageCatalog.Factory, EquipmentId.Bucket, EquipmentId.Extinguisher, EquipmentId.FoamExtinguisher);
            Advance(runner, 4f);
            return Show(runner, 4f, new Vector2(19, 6));
        }

        /// <summary>
        /// 사람이 남은 점포 안으로 들어간 시점. 지붕이 걷히면서 Help! 말풍선과 입구 표시가
        /// 함께 사라지고, 대신 시민 머리 위의 "!" 표식이 보인다.
        /// </summary>
        private static Camera InsideRescue()
        {
            StageRunner runner = Runner(StageCatalog.Shopping, EquipmentId.Extinguisher);

            // 시민이 있는 아래쪽 가운데 점포(15~22열, 12~17행) 안에 세운다.
            runner.Player.Spawn(new GridPoint(20, 14));
            Advance(runner, 1f);
            return Show(runner, 1f);
        }

        /// <summary>
        /// 점포 안으로 들어간 시점. 그 점포의 지붕만 걷히고 카메라가 건물 상자에 맞는다.
        /// 나머지 다섯 채는 지붕을 쓴 채로 남아 "지금 이 건물"만 보인다.
        /// </summary>
        private static Camera InsideShop()
        {
            StageRunner runner = Runner(StageCatalog.Shopping, EquipmentId.Extinguisher);

            // 걸어 들어가는 과정을 재현하지 않고 안쪽 한 칸에 곧바로 세운다.
            // 캡처는 같은 그림이 나와야 해서, 이동 시뮬레이션보다 좌표 지정이 낫다.
            runner.Player.Spawn(new GridPoint(8, 5));
            Advance(runner, 1f);
            return Show(runner, 1f);
        }

        private static Camera HarborFinale()
        {
            StageRunner runner = Runner(StageCatalog.Harbor,
                EquipmentId.Bucket, EquipmentId.Extinguisher, EquipmentId.Hose, EquipmentId.FoamExtinguisher);
            Advance(runner, 4f);
            return Show(runner, 4f, new Vector2(18, 9));
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
            // 1~3을 깨고 4번(창고)에 신고가 들어온 상태. 길이 6번까지 이어진 모습을 본다.
            save.Money = 1250;
            save.RecordResult(0, 3);
            save.RecordResult(1, 2);
            save.RecordResult(2, 1);
            return Direct(new GameFlow(save));
        }

        private static Camera Briefing()
        {
            var flow = new GameFlow(SaveData.NewGame());
            flow.SelectMission(0);
            return Direct(flow);
        }

        /// <summary>주택가를 깨고 CO2가 없는 채로 상가 브리핑. 빨간 칩·경고·상점 버튼이 나와야 한다.</summary>
        private static Camera BriefingNeedGear()
        {
            SaveData save = SaveData.NewGame();
            save.Money = 320;
            save.RecordResult(0, 2);
            var flow = new GameFlow(save);
            flow.SelectMission(1);
            return Direct(flow);
        }

        /// <summary>CO2 Lv2를 갖춘 상가 브리핑. 초록 칩만 나오고 상점 버튼은 없다.</summary>
        /// <summary>주택가를 깨고 $944를 번 지도. 상가에 "장비 부족", 상점 버튼에 살 수 있는 수 배지.</summary>
        private static Camera MapNeedGear()
        {
            SaveData save = SaveData.NewGame();
            save.Money = 944;
            save.RecordResult(0, 2);
            return Direct(new GameFlow(save));
        }

        private static Camera BriefingReady()
        {
            SaveData save = SaveData.NewGame();
            save.RecordResult(0, 2);
            save.SetLevel(EquipmentId.Extinguisher, 2);
            var flow = new GameFlow(save);
            flow.SelectMission(1);
            return Direct(flow);
        }

        /// <summary>폼 Lv1로 주유소에 나가 끝내 진 결과. "폼 소화기 Lv2 이상이 있어야" 안내가 나와야 한다.</summary>
        private static Camera ResultNeedGear()
        {
            SaveData save = SaveData.NewGame();
            save.RecordResult(0, 2);
            save.RecordResult(1, 2);
            save.SetLevel(EquipmentId.Extinguisher, 2);
            save.SetLevel(EquipmentId.FoamExtinguisher, 1);
            var flow = new GameFlow(save);
            flow.SelectMission(2);
            flow.BeginMission();
            for (int i = 0; i < 5000 && flow.Screen == GameScreen.Playing; i++) flow.Update(0.1f);
            if (flow.Screen != GameScreen.Result) throw new InvalidOperationException("패배 상태를 만들지 못했다");
            return Direct(flow);
        }

        /// <summary>불을 모두 끄고 시민을 출구로 옮겨 이긴 상태를 만든다(봇 없이).</summary>
        private static Camera ResultWin()
        {
            return WinResidential(SaveData.NewGame());
        }

        private static Camera WinResidential(SaveData save)
        {
            var flow = new GameFlow(save);
            flow.SelectMission(0);
            flow.BeginMission();
            for (int i = 0; i < 30; i++) flow.Update(0.1f);

            StageRunner runner = flow.Runner;
            Civilian civilian = runner.Civilians[0];
            runner.Player.X = civilian.X;
            runner.Player.Y = civilian.Y;

            // 시민은 이제 저절로 업히지 않는다. 구조 버튼을 눌러야 한다.
            flow.SetRescue(true);
            flow.Update(0.02f);
            flow.SetRescue(false);

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

        /// <summary>
        /// 레벨이 섞인 상점: 양동이 Lv3, CO2 Lv1, 호스 잠김, 폼 Lv4(다음이 특성), 방화복 Lv2, 소방화 없음, 보유금 $650.
        /// 해금·레벨업·잔액 부족 버튼과 "특성!" 줄, 게임 말 효과가 한 화면에 다 나온다.
        /// </summary>
        private static Camera ShopOpen()
        {
            SaveData save = SaveData.NewGame();
            save.Money = 650;
            save.RecordResult(0, 1);
            save.SetLevel(EquipmentId.Bucket, 3);
            save.SetLevel(EquipmentId.Extinguisher, 1);
            save.SetLevel(EquipmentId.FoamExtinguisher, 4);
            save.SetLevel(GearId.Suit, 2);
            var flow = new GameFlow(save);
            flow.OpenShop();
            return Direct(flow);
        }

        /// <summary>예전이면 "최대"였을 레벨. 상한이 없어 여전히 값이 찍힌다.</summary>
        private static Camera ShopHighLevel()
        {
            SaveData save = SaveData.NewGame();
            save.Money = 40000;
            save.RecordResult(0, 3);
            save.RecordResult(1, 2);
            save.SetLevel(EquipmentId.Bucket, 12);
            save.SetLevel(EquipmentId.Extinguisher, 9);
            save.SetLevel(EquipmentId.Hose, 10);
            save.SetLevel(EquipmentId.FoamExtinguisher, 7);
            save.SetLevel(GearId.Suit, 10);
            save.SetLevel(GearId.Boots, 14);
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
            save.SetLevel(EquipmentId.Bucket, 3);
            save.SetLevel(EquipmentId.Extinguisher, 2);
            save.SetLevel(EquipmentId.Hose, 1);
            save.SetLevel(EquipmentId.FoamExtinguisher, 1);
            save.SetLevel(GearId.Suit, 2);
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

        /// <summary>시민 옆에 선 순간 — 표식이 초록이고 HUD 구조 버튼이 켜진다.</summary>
        private static Camera RescueReady()
        {
            return RescueScene(false);
        }

        /// <summary>시민을 업고 출구로 가는 중 — 어깨 위 시민과 출구 안내.</summary>
        private static Camera RescueCarry()
        {
            return RescueScene(true);
        }

        private static Camera RescueScene(bool carry)
        {
            SaveData save = SaveData.NewGame();
            save.SetLevel(EquipmentId.Bucket, 3);
            save.SetLevel(EquipmentId.Extinguisher, 2);
            save.SetLevel(GearId.Suit, 2);

            var flow = new GameFlow(save);
            flow.SelectMission(0);
            flow.BeginMission();
            for (int i = 0; i < 20; i++) flow.Update(0.1f);

            StageRunner runner = flow.Runner;
            Civilian civilian = runner.Civilians[0];
            runner.Player.X = civilian.X - 0.5f;
            runner.Player.Y = civilian.Y;

            flow.SetRescue(carry);
            flow.Update(0.02f);
            flow.SetRescue(false);

            if (carry)
            {
                // 업은 채 몇 걸음 걸어 나온 모습.
                for (int i = 0; i < 12; i++)
                {
                    flow.SetMove(-1f, 0f);
                    flow.Update(0.05f);
                }
                flow.SetMove(0f, 0f);
            }

            Camera camera = WorldCamera();
            var root = new GameObject("Root").transform;
            var view = new MissionWorldView(root, runner);
            view.Refresh(flow.Elapsed, 0f);
            view.FrameCamera(camera, view.PlayerWorld);

            Canvas canvas = UiKit.CreateCanvas(root, camera, "Canvas", 0);
            var hud = new MissionHud(canvas, flow);
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
            for (int i = 1; i <= 3; i++) view.Refresh(2f + (i * 0.05f), 0.05f);   // 물이 날아가는 중간
            view.FrameCamera(camera, view.PlayerWorld);
            return camera;
        }

        /// <summary>
        /// 양동이 Lv10(특성 두 개: 앞 9칸)을 불길에 끼얹은 순간. 굵어진 물보라와 "N칸 진압!"이 보여야 한다.
        /// </summary>
        private static Camera SprayLevel10()
        {
            SaveData save = SaveData.NewGame();
            save.SetLevel(EquipmentId.Bucket, 10);
            var runner = new StageRunner(StageCatalog.Warehouse, Loadout.From(save));
            Advance(runner, 5f);

            // 서쪽 칸이 걸을 수 있는, 불타는 칸을 찾아 그 옆에 세우고 동쪽(불 쪽)을 조준한다.
            FireGrid grid = runner.Grid;
            for (int y = 1; y < grid.Height - 1; y++)
            {
                for (int x = 1; x < grid.Width - 1; x++)
                {
                    if (grid[x, y].State != CellState.Burning) continue;
                    if (!Materials.Of(grid[x - 1, y].Material).Walkable || grid[x - 1, y].State == CellState.Burning) continue;
                    runner.Player.X = x - 0.5f;
                    runner.Player.Y = y + 0.5f;
                    runner.Player.Aim = AimDirection.E;
                    y = grid.Height;
                    break;
                }
            }

            Camera camera = WorldCamera();
            var view = new MissionWorldView(new GameObject("Root").transform, runner);
            view.Refresh(5f, 0f);
            runner.Update(0.01f, new StageInput { Fire = true, Slot = 0 });
            for (int i = 1; i <= 4; i++) view.Refresh(5f + (i * 0.05f), 0.05f);
            if (runner.LastShotExtinguished < 2) throw new InvalidOperationException("한 발에 여러 칸을 끄지 못했다: " + runner.LastShotExtinguished);
            view.FrameCamera(camera, view.PlayerWorld);
            return camera;
        }

        /// <summary>같은 현장을 두 번째로 깨서 최고 기록을 줄인 결과 화면.</summary>
        private static Camera ResultRecord()
        {
            SaveData save = SaveData.NewGame();
            save.RecordResult(0, 2);
            save.RecordTime(0, 48.6f);
            save.Money = 700;
            return WinResidential(save);
        }

        /// <summary>방열복(Lv4)을 입은 소방관이 전기실 안에서 CO2 소화기(Lv3)를 막 뿜은 순간.</summary>
        private static Camera SuitAndCo2()
        {
            SaveData save = SaveData.NewGame();
            save.SetLevel(GearId.Suit, 4);
            save.SetLevel(EquipmentId.Extinguisher, 3);
            var runner = new StageRunner(StageCatalog.Shopping, Loadout.From(save));
            Advance(runner, 3f);

            runner.Player.X = 8.5f;
            runner.Player.Y = 5.5f;
            runner.Player.Aim = AimDirection.W;

            Camera camera = WorldCamera();
            var view = new MissionWorldView(new GameObject("Root").transform, runner);
            view.Refresh(3f, 0f);
            runner.Update(0.01f, new StageInput { Fire = true, Slot = 1 });
            for (int i = 1; i <= 3; i++) view.Refresh(3f + (i * 0.05f), 0.05f);
            view.FrameCamera(camera, view.PlayerWorld);
            return camera;
        }

        /// <summary>불붙은 벽 세 칸을 양동이 한 발로 끈 직후. 꺼진 칸마다 김이 피어올라야 한다.</summary>
        private static Camera SteamAfterPuttingOut()
        {
            StageRunner runner = Runner(StageCatalog.Residential, EquipmentId.Bucket);
            Advance(runner, 2f);

            runner.Player.X = 16.5f;
            runner.Player.Y = 9.5f;
            runner.Player.Aim = AimDirection.N;
            for (int x = 15; x <= 17; x++)
            {
                runner.Grid[x, 8].State = CellState.Burning;
                runner.Grid[x, 8].Heat = 0.2f;   // 한 발이면 꺼질 만큼
            }

            Camera camera = WorldCamera();
            var view = new MissionWorldView(new GameObject("Root").transform, runner);
            view.Refresh(2f, 0f);
            runner.Update(0.01f, new StageInput { Fire = true, Slot = 0 });
            for (int i = 1; i <= 6; i++) view.Refresh(2f + (i * 0.05f), 0.05f);
            view.FrameCamera(camera, view.PlayerWorld);
            return camera;
        }

        /// <summary>방화복 0~4단계를 나란히 세워 옷 색·헬멧이 구분되는지 본다.</summary>
        private static Camera SuitsLineup()
        {
            Camera camera = WorldCamera();
            camera.orthographicSize = 3f;
            camera.backgroundColor = new Color(0.18f, 0.62f, 0.35f);
            camera.transform.position = new Vector3(0f, 0f, -10f);

            // 옷 그림은 다섯 장뿐이다(Lv4 방열복이 마지막). 레벨엔 끝이 없지만 보여줄 그림은 여기까지다.
            for (int level = 0; level <= 4; level++)
            {
                var suit = new GameObject("Suit" + level).AddComponent<SpriteRenderer>();
                suit.sprite = Require<Sprite>("Art/" + MissionWorldView.SuitSprite(level));
                suit.transform.position = new Vector3((level - 2) * 2f, 0.4f, 0f);
                suit.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                suit.transform.localScale = Vector3.one * 2f;
            }
            return camera;
        }

        /// <summary>
        /// 파이프라인 확인용: 스프라이트 로드, 카메라 렌더, 캔버스 렌더, 주아체 한글.
        /// </summary>
        /// <summary>
        /// 코드로 찍은 그림을 한 장에 모아 본다.
        ///
        /// 지붕·바닥·테두리 재질은 어디에도 쓰이기 전에는 눈으로 확인할 방법이 없다.
        /// 여기서 먼저 걸러야 "현장에 깔았더니 전부 회색이더라"를 반복하지 않는다.
        /// 지붕은 현장 색을 곱한 상태로 보여 준다 — 밝은 바탕이 아니면 여기서 색이 죽는다.
        /// </summary>
        private static Camera GeneratedArt()
        {
            var cameraObject = new GameObject("Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 16f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.10f, 0.12f);
            camera.transform.position = new Vector3(16f, -6f, -10f);

            var roofColors = new[]
            {
                new Color(0.82f, 0.34f, 0.28f), new Color(0.32f, 0.54f, 0.82f),
                new Color(0.93f, 0.95f, 0.96f), new Color(0.56f, 0.62f, 0.70f),
                new Color(0.34f, 0.46f, 0.62f), new Color(0.70f, 0.42f, 0.30f),
            };

            // 1줄: 지붕 6종(현장 색을 곱한 상태)
            for (int i = 0; i < 6; i++)
            {
                Swatch(Art.RoofTexture((RoofStyle)i), roofColors[i], i * 5f, 6f, 4f);
                Caption(((RoofStyle)i).ToString(), (i * 5f) + 2f, 8.4f);
            }

            // 2~3줄: 바닥 6종 x 4변형
            for (int i = 0; i < 6; i++)
            {
                for (int v = 0; v < 4; v++)
                {
                    Swatch(Art.GroundTexture((GroundStyle)i, v), Color.white, (i * 5f) + (v % 2 * 2.2f), 1f - (v / 2 * 2.2f), 2f);
                }

                Caption(((GroundStyle)i).ToString(), (i * 5f) + 1.1f, 3.4f);
            }

            // 4줄: 테두리 5종
            for (int i = 0; i < 5; i++)
            {
                Swatch(Art.BorderTexture((BorderStyle)i), Color.white, i * 5f, -7f, 4f);
                Caption(((BorderStyle)i).ToString(), (i * 5f) + 2f, -4.6f);
            }

            // 5줄: 코드로 찍는 소품 5종. 투명 배경이 보이게 회색 판을 깔아 준다.
            for (int i = 0; i < 5; i++)
            {
                var pad = new GameObject("Pad").AddComponent<SpriteRenderer>();
                pad.sprite = Art.White;
                pad.color = new Color(0.45f, 0.45f, 0.48f);
                pad.transform.position = new Vector3((i * 5f) + 2f, -11f, 0f);
                pad.transform.localScale = Vector3.one * 4f;

                Swatch(Art.PropTexture((PropStyle)i), Color.white, i * 5f, -13f, 4f);
                Caption(((PropStyle)i).ToString(), (i * 5f) + 2f, -8.6f);
            }

            // 6줄: 켄니에서 새로 가져온 소품 8장. 자홍색 네모가 보이면 임포트가 실패한 것이다.
            string[] imported =
            {
                "Props/tree_large", "Props/tree_small", "Props/barrel_red", "Props/barrel_blue",
                "Props/tires", "Props/barrier", "Vehicles/car_blue", "Vehicles/car_black",
            };

            for (int i = 0; i < imported.Length; i++)
            {
                Sprite sprite = Require<Sprite>("Art/" + imported[i]);
                var pad = new GameObject("Pad").AddComponent<SpriteRenderer>();
                pad.sprite = Art.White;
                pad.color = new Color(0.45f, 0.45f, 0.48f);
                pad.transform.position = new Vector3((i * 4f) + 2f, -20f, 0f);
                pad.transform.localScale = Vector3.one * 3.6f;

                Swatch(sprite, Color.white, (i * 4f) + 0.2f, -21.8f, 3.6f);
                Caption(imported[i].Substring(imported[i].IndexOf('/') + 1), (i * 4f) + 2f, -17.6f);
            }

            return camera;
        }

        private static void Swatch(Sprite sprite, Color tint, float x, float y, float size)
        {
            var go = new GameObject("Swatch");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = tint;
            renderer.sortingOrder = 1;
            go.transform.position = new Vector3(x + (size / 2f), y + (size / 2f), 0f);
            go.transform.localScale = Vector3.one * (size / sprite.bounds.size.x);
        }

        private static void Caption(string text, float x, float y)
        {
            var go = new GameObject("Caption");
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = Art.Font;
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = 0.08f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.color = Color.white;
            go.GetComponent<MeshRenderer>().sharedMaterial = Art.Font.material;
            go.GetComponent<MeshRenderer>().sortingOrder = 5;
            go.transform.position = new Vector3(x, y, 0f);
        }

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
