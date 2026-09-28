using System;
using System.Collections.Generic;
using System.IO;
using FireGame.Core.Grid;
using FireGame.Prototypes.Logic;
using FireGame.UnityLayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FireGame.Prototypes.EditorTools
{
    /// <summary>
    /// 시험판 화면을 배치 모드에서 PNG로 찍는다. 본 게임 하니스와 따로 둬서 시험판을 버릴 때 같이 지운다.
    /// 실행: tools/unity-check.sh proto-shots
    /// </summary>
    public static class PrototypeShots
    {
        private const int Width = 1920;
        private const int Height = 1080;

        public static void CaptureAll()
        {
            string dir = ArgValue("-shotDir") ?? Path.Combine(Application.dataPath, "../../tools/.shots-proto");
            Directory.CreateDirectory(dir);
            int failures = 0;

            failures += Shot(dir, "b1_level1_start", view => { });
            failures += Shot(dir, "b2_level2_spray_aim", view =>
            {
                view.LoadLevel(1);
                view.Game.Do(TAction.Move, new GridPoint(5, 4));
                view.Game.Do(TAction.Move, new GridPoint(5, 2));
                view.Game.EndTurn();
                view.Game.Do(TAction.Move, new GridPoint(5, 4));
                view.Game.Do(TAction.Move, new GridPoint(3, 4));
                view.Game.EndTurn();
                view.AimSpray(Dir.W);
            });
            failures += Shot(dir, "b3_level3_gas", view =>
            {
                view.LoadLevel(2);
                for (int i = 0; i < 6; i++) view.Game.EndTurn();
            });

            failures += ActionShot(dir, "a1_action_fight", 7f);
            failures += ActionShot(dir, "a2_action_later", 16f);

            // 동네 전체를 한 화면에: 가게 아홉 채가 저마다 알아볼 수 있게 그려졌는지 본다.
            failures += SurvivorShot(dir, "c0_town", view => view.Sim.Time >= 1f, 5, false, camera =>
            {
                camera.transform.position = new Vector3(SurvivorSim.ArenaSize / 2f, 31f, -10f);
                camera.orthographicSize = 14f;
            });
            failures += SurvivorShot(dir, "c1_early", view => view.Sim.Time >= 40f);
            // 레벨 5로 오르는 카드: 노란 특수 장비가 반드시 한 장 있다.
            failures += SurvivorShot(dir, "c2_levelup_cards", view => view.Sim.Level == 5 && view.Sim.PendingChoices != null, 45);
            failures += SurvivorShot(dir, "c3_boss", view => view.Sim.Boss != null && view.Sim.Time >= SurvivorSim.BossAt + 5f);
            failures += SurvivorShot(dir, "c4_evolved", view => view.Sim.JustEvolved, 9);
            failures += SurvivorShot(dir, "c5_levelup_burst", view => view.Sim.Time >= 30f && view.Sim.JustLeveled, 8);
            failures += SurvivorShot(dir, "c6_arsenal", view => view.Sim.Time >= 150f && view.Sim.PendingChoices == null);
            // 보는 용도: 봇이 잘 안 고르는 아이템까지 전부 최대 레벨로 쥐여 주고 레벨별 연출을 한 화면에서 본다.
            failures += SurvivorShot(dir, "c7_gear_maxed", view => view.Sim.Time >= 25f && view.Sim.PendingChoices == null, 20, true);

            // 동네: 불난 가게에 사람이 갇혀 있고 소방관이 가까이 있는 장면, 그리고 판이 끝난 결과창.
            failures += SurvivorShot(dir, "c8_house_fire", view => view.Sim.Structures.Exists(s => s.IsBuilding && s.Burning && s.Residents > 0 && s.DistanceTo(view.Sim.Player) < 6f), 30);
            failures += SurvivorShot(dir, "c9_result", view => view.Sim.Outcome != SOutcome.Playing, 90);
            // 특수 장비 셋(풀장비): 헬기가 목표 위에서 물을 쏟기 직전, 동료, 물의 장막.
            failures += SurvivorShot(dir, "c10_specials", view => view.Sim.Time >= 20f && view.Sim.PendingChoices == null && view.Sim.Shots.Exists(s => s.Kind == ShotKind.Heli && s.Age > s.Life * 0.7f), 3, true);
            // 호스를 쥔 손과 물줄기를 확대: 노즐이 옆구리에 있고 물이 끝까지 한 줄로 이어지는지 본다.
            failures += SurvivorShot(dir, "c11_hose_closeup", view => view.Sim.Time >= 12f && view.Sim.Spraying && view.Sim.PendingChoices == null && view.Sim.Shots.FindAll(s => s.Hose).Count >= 6, 2, false, camera =>
            {
                camera.transform.position = new Vector3(camera.transform.position.x, camera.transform.position.y, -10f);
                camera.orthographicSize = 4.5f;
            });

            failures += TouchShot(dir, "c12_touch_sticks");
            failures += AimShot(dir, "c13_aim_assist");
            // 무기·보조가 Lv5가 되는 순간(금빛 기둥, "○○ MAX!").
            failures += SurvivorShot(dir, "c14_max_burst", view => view.Sim.JustMaxed.HasValue, 8);
            failures += NextStageShot(dir, "c14b_next_stage");
            // 공구상자를 주워 건물을 고치는 순간(초록 빛줄기·"수리!").
            failures += SurvivorShot(dir, "c15_toolbox", view => view.Sim.Toolboxes.Exists(b => b.Pos.DistanceTo(view.Sim.Player) < 5f), 5);
            failures += SurvivorShot(dir, "c15b_toolbox_repair", view => view.Sim.Repaired.Count > 0 || view.Sim.JustPickedToolbox, 12);
            // 마을 전용 노란 카드(풀장비): 소방차가 줄을 가로지르고 스프링클러가 터진다.
            failures += SurvivorShot(dir, "c16_town_specials", view => view.Sim.Truck.HasValue && System.Math.Abs(view.Sim.Truck.Value.X - view.Sim.Player.X) < 5f, 3, true);
            // 2스테이지 산불 숲: 흙길·소나무, 불다람쥐, 막 날아온 재 박쥐 떼, 바람 화살표.
            failures += SurvivorShot(dir, "c17_forest", view => view.Sim.Time >= 40.5f && view.Sim.Enemies.Exists(e => (e.Kind == EnemyKind.Bat || e.Kind == EnemyKind.Squirrel) && e.Pos.DistanceTo(view.Sim.Player) < 6f), 10, false, null, 2);
            // 산불 숲 전용 노란 카드(풀장비): 먹구름 비와 방염제 띠·비행기.
            failures += SurvivorShot(dir, "c19_forest_specials", view => view.Sim.RainAt.HasValue && view.Sim.Retardants.Count > 0, 45, true, null, 2);

            Debug.Log("[ProtoShots] 완료, 실패 " + failures);
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int Shot(string dir, string name, Action<TacticsView> prepare)
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Prototype");
                Camera camera = PrototypeHost.SetUpCamera();
                camera.aspect = (float)Width / Height;
                Canvas canvas = UiKit.CreateCanvas(root.transform, camera, "Canvas", 0);

                var view = new TacticsView(root.transform, camera, canvas);
                view.LoadLevel(0);
                prepare(view);
                view.Refresh();

                Canvas.ForceUpdateCanvases();
                Capture(camera, Path.Combine(dir, name + ".png"));
                Debug.Log("[ProtoShots] " + name);
                return 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[ProtoShots] 실패: " + name + " — " + e);
                return 1;
            }
        }

        /// <summary>봇에게 몇 초 맡겨 교전 중인 화면을 찍는다.</summary>
        private static int ActionShot(string dir, string name, float seconds)
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Prototype");
                Camera camera = PrototypeHost.SetUpCamera();
                camera.aspect = (float)Width / Height;
                Canvas canvas = UiKit.CreateCanvas(root.transform, camera, "Canvas", 0);

                var view = new ActionView(root.transform, camera, canvas);
                var bot = new ActionBot(view.Sim);
                int ticks = (int)(seconds / ActionSim.Dt);
                for (int i = 0; i < ticks && view.Sim.Outcome == AOutcome.Playing; i++)
                {
                    view.Step(bot.Next());
                    view.Refresh(ActionSim.Dt);
                }

                Canvas.ForceUpdateCanvases();
                Capture(camera, Path.Combine(dir, name + ".png"));
                Debug.Log("[ProtoShots] " + name + " (" + view.Sim.Outcome + ", 구조 " + view.Sim.Rescued + ")");
                return 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[ProtoShots] 실패: " + name + " — " + e);
                return 1;
            }
        }

        /// <summary>봇에게 판을 맡겨 조건이 될 때까지 굴린 뒤 찍는다. 카드 장면은 카드를 고르지 않고 멈춘다.</summary>
        /// <param name="settle">조건에 닿은 뒤 효과를 흘려 보낼 프레임 수(60 = 1초).</param>
        private static string GearOf(SurvivorSim sim)
        {
            var parts = new List<string>();
            foreach (UpgradeId id in sim.Build.Owned()) parts.Add(id + " " + sim.Build.Level(id));
            return string.Join(" · ", parts);
        }

        /// <param name="frame">찍기 직전에 카메라를 옮긴다(동네 전체 보기 등).</param>
        private static int SurvivorShot(string dir, string name, Func<SurvivorView, bool> until, int settle = 20, bool maxGear = false, Action<Camera> frame = null, int stage = 1)
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Prototype");
                Camera camera = PrototypeHost.SetUpCamera();
                camera.aspect = (float)Width / Height;
                Canvas canvas = UiKit.CreateCanvas(root.transform, camera, "Canvas", 0);

                var view = new SurvivorView(root.transform, camera, canvas, maxGear, stage);
                var bot = new SurvivorBot(view.Sim);
                int guard = 0;
                while (!until(view) && view.Sim.Outcome == SOutcome.Playing && guard++ < 60 * 400)
                {
                    if (view.Sim.PendingChoices != null)
                    {
                        view.Choose(SurvivorBot.PickCard(view.Sim.PendingChoices));
                        continue;
                    }
                    bot.AimHose();
                    view.Step(bot.Move());
                    view.Refresh(SurvivorSim.Dt);
                }
                if (!until(view)) throw new Exception("조건에 닿기 전에 판이 끝났다: " + view.Sim.Outcome + " t=" + view.Sim.Time);
                // 카드는 0.3초 뒤에 튀어 오르니 다 뜬 뒤를 찍는다. 효과도 조금 흐르게 둔다.
                for (int i = 0; i < settle; i++) view.Refresh(SurvivorSim.Dt);
                frame?.Invoke(camera);

                Canvas.ForceUpdateCanvases();
                Capture(camera, Path.Combine(dir, name + ".png"));
                Debug.Log("[ProtoShots] " + name + " (" + view.Sim.Outcome + " 별 " + view.Sim.Stars + " 무너짐 " + view.Sim.HousesLost + " 구조 " + view.Sim.Rescued + ", t=" + (int)view.Sim.Time + ", Lv " + view.Sim.Level + ", 적 " + view.Sim.Enemies.Count + ", 카드 " + (view.Sim.PendingChoices != null) + ", " + GearOf(view.Sim) + ")");
                return 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[ProtoShots] 실패: " + name + " — " + e);
                return 1;
            }
        }

        /// <summary>
        /// 폰 조작: 봇이 몇 초 굴린 판에 가짜 두 손가락(왼손 오른쪽으로 끌기, 오른손 위로 끌기)을 Tick으로 넣는다.
        /// 조이스틱 두 개가 보이고, 소방관이 걸으면서 끈 쪽으로 물을 쏘는지 본다.
        /// </summary>
        private static int TouchShot(string dir, string name)
        {
            RenderTexture screen = null;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Prototype");
                Camera camera = PrototypeHost.SetUpCamera();
                // 손가락 좌표와 캔버스가 찍을 화면(1920×1080)과 같은 크기를 보게 한다.
                screen = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = screen;
                Canvas canvas = UiKit.CreateCanvas(root.transform, camera, "Canvas", 0);

                var view = new SurvivorView(root.transform, camera, canvas);
                var bot = new SurvivorBot(view.Sim);
                while (view.Sim.Time < 8f && view.Sim.Outcome == SOutcome.Playing)
                {
                    if (view.Sim.PendingChoices != null)
                    {
                        view.Choose(SurvivorBot.PickCard(view.Sim.PendingChoices));
                        continue;
                    }
                    bot.AimHose();
                    view.Step(bot.Move());
                    view.Refresh(SurvivorSim.Dt);
                }

                var left = new Vec2(Width * 0.18f, Height * 0.3f);
                var right = new Vec2(Width * 0.78f, Height * 0.35f);
                for (int frame = 0; frame < 45; frame++)
                {
                    if (view.Sim.PendingChoices != null) view.Choose(SurvivorBot.PickCard(view.Sim.PendingChoices));
                    FingerPhase phase = frame == 0 ? FingerPhase.Down : FingerPhase.Held;
                    float t = Mathf.Clamp01(frame / 10f);
                    var input = new ProtoInput
                    {
                        Fingers = new List<Finger>
                        {
                            new Finger { Id = 0, Phase = phase, At = new Vec2(left.X + (80f * t), left.Y) },
                            new Finger { Id = 1, Phase = phase, At = new Vec2(right.X + (40f * t), right.Y + (90f * t)) },
                        },
                    };
                    view.Tick(SurvivorSim.Dt, input);
                }
                if (!view.Sim.Spraying) throw new Exception("오른손 스틱을 눌렀는데 물이 안 나간다");
                foreach (RectTransform r in canvas.GetComponentsInChildren<RectTransform>(true))
                {
                    if (r.name.StartsWith("Stick")) Debug.Log("[ProtoShots] 스틱 " + r.name + " 켜짐 " + r.gameObject.activeInHierarchy + " 자리 " + r.anchoredPosition + " 크기 " + r.rect.size + " 부모 " + r.parent.name);
                }

                Canvas.ForceUpdateCanvases();
                Capture(camera, Path.Combine(dir, name + ".png"));
                Debug.Log("[ProtoShots] " + name + " (조준 " + view.Sim.Aim.X.ToString("0.00") + "," + view.Sim.Aim.Y.ToString("0.00") + ", 쏨 " + view.Sim.Spraying + ")");
                return 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[ProtoShots] 실패: " + name + " — " + e);
                return 1;
            }
            finally
            {
                if (screen != null) UnityEngine.Object.DestroyImmediate(screen);
            }
        }

        /// <summary>
        /// 폰 조준 보정: 불 하나를 소방관 오른쪽 위 20°에 두고, 가짜 오른손은 정확히 오른쪽(0°)으로 끈다.
        /// 물줄기가 끈 쪽이 아니라 불 쪽으로 휘어 들어가는지 본다.
        /// </summary>
        private static int AimShot(string dir, string name)
        {
            RenderTexture screen = null;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Prototype");
                Camera camera = PrototypeHost.SetUpCamera();
                screen = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = screen;
                Canvas canvas = UiKit.CreateCanvas(root.transform, camera, "Canvas", 0);

                var view = new SurvivorView(root.transform, camera, canvas);
                view.Sim.Reports = false;
                var bot = new SurvivorBot(view.Sim);
                while (view.Sim.Time < 3f && view.Sim.Outcome == SOutcome.Playing)
                {
                    bot.AimHose();
                    view.Step(bot.Move());
                    view.Refresh(SurvivorSim.Dt);
                }
                view.Sim.Enemies.Clear();
                Vec2 p = view.Sim.Player;
                double a = 20.0 * Math.PI / 180.0;
                Enemy fire = view.Sim.Spawn(EnemyKind.Blaze, new Vec2(p.X + (float)(Math.Cos(a) * 6.0), p.Y + (float)(Math.Sin(a) * 6.0)));
                fire.MaxHp = fire.Hp = 9999f;

                var right = new Vec2(Width * 0.78f, Height * 0.4f);
                for (int frame = 0; frame < 40; frame++)
                {
                    FingerPhase phase = frame == 0 ? FingerPhase.Down : FingerPhase.Held;
                    float t = Mathf.Clamp01(frame / 8f);
                    var input = new ProtoInput { Fingers = new List<Finger> { new Finger { Id = 1, Phase = phase, At = new Vec2(right.X + (120f * t), right.Y) } } };
                    view.Tick(SurvivorSim.Dt, input);
                }
                Vec2 me = view.Sim.Player;
                float toFire = Mathf.Atan2(fire.Pos.Y - me.Y, fire.Pos.X - me.X) * Mathf.Rad2Deg;
                float aimed = Mathf.Atan2(view.Sim.Aim.Y, view.Sim.Aim.X) * Mathf.Rad2Deg;
                float miss = Mathf.Abs(Mathf.DeltaAngle(toFire, aimed));
                if (miss > 8f) throw new Exception("조준 보정이 불 쪽으로 안 당긴다: 불 " + toFire + "° 조준 " + aimed + "°");

                Canvas.ForceUpdateCanvases();
                Capture(camera, Path.Combine(dir, name + ".png"));
                Debug.Log("[ProtoShots] " + name + " (끈 방향 0°, 불 " + toFire.ToString("0.0") + "°, 조준 " + aimed.ToString("0.0") + "°)");
                return 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[ProtoShots] 실패: " + name + " — " + e);
                return 1;
            }
            finally
            {
                if (screen != null) UnityEngine.Object.DestroyImmediate(screen);
            }
        }

        /// <summary>1스테이지를 이긴 결과창에서 탭하면 2스테이지가 "STAGE 2" 띠와 함께 열린다(플레이어 경로: Tick에 클릭).</summary>
        private static int NextStageShot(string dir, string name)
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Prototype");
                Camera camera = PrototypeHost.SetUpCamera();
                camera.aspect = (float)Width / Height;
                Canvas canvas = UiKit.CreateCanvas(root.transform, camera, "Canvas", 0);

                var view = new SurvivorView(root.transform, camera, canvas);
                var bot = new SurvivorBot(view.Sim);
                int guard = 0;
                while (view.Sim.Outcome == SOutcome.Playing && guard++ < 60 * 400)
                {
                    if (view.Sim.PendingChoices != null)
                    {
                        view.Choose(SurvivorBot.PickCard(view.Sim.PendingChoices));
                        continue;
                    }
                    bot.AimHose();
                    view.Step(bot.Move());
                }
                if (view.Sim.Outcome != SOutcome.Won) throw new Exception("1스테이지를 못 이겼다: " + view.Sim.Outcome);
                for (int i = 0; i < 80; i++) view.Tick(SurvivorSim.Dt, new ProtoInput());
                view.Tick(SurvivorSim.Dt, new ProtoInput { MouseClicked = true });
                if (view.Sim.Stage.Number != 2) throw new Exception("이긴 뒤 탭했는데 스테이지 " + view.Sim.Stage.Number);
                for (int i = 0; i < 40; i++) view.Tick(SurvivorSim.Dt, new ProtoInput());

                Canvas.ForceUpdateCanvases();
                Capture(camera, Path.Combine(dir, name + ".png"));
                Debug.Log("[ProtoShots] " + name + " (스테이지 " + view.Sim.Stage.Number + " " + view.Sim.Stage.Name + ", t=" + view.Sim.Time.ToString("0.0") + ")");
                return 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[ProtoShots] 실패: " + name + " — " + e);
                return 1;
            }
        }

        private static void Capture(Camera camera, string path)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(target);
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
