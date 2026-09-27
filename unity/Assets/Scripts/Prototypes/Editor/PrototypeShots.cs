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

            failures += SurvivorShot(dir, "c1_early", view => view.Sim.Time >= 40f);
            failures += SurvivorShot(dir, "c2_levelup_cards", view => view.Sim.Time >= 60f && view.Sim.PendingChoices != null, 45);
            failures += SurvivorShot(dir, "c3_boss", view => view.Sim.Boss != null && view.Sim.Time >= SurvivorSim.BossAt + 5f);
            failures += SurvivorShot(dir, "c4_evolved", view => view.Sim.JustEvolved, 9);
            failures += SurvivorShot(dir, "c5_levelup_burst", view => view.Sim.Time >= 30f && view.Sim.JustLeveled, 8);
            failures += SurvivorShot(dir, "c6_arsenal", view => view.Sim.Time >= 150f && view.Sim.PendingChoices == null);
            // 보는 용도: 봇이 잘 안 고르는 아이템까지 전부 최대 레벨로 쥐여 주고 레벨별 연출을 한 화면에서 본다.
            failures += SurvivorShot(dir, "c7_gear_maxed", view => view.Sim.Time >= 25f && view.Sim.PendingChoices == null, 20, true);

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

        private static int SurvivorShot(string dir, string name, Func<SurvivorView, bool> until, int settle = 20, bool maxGear = false)
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Prototype");
                Camera camera = PrototypeHost.SetUpCamera();
                camera.aspect = (float)Width / Height;
                Canvas canvas = UiKit.CreateCanvas(root.transform, camera, "Canvas", 0);

                var view = new SurvivorView(root.transform, camera, canvas, maxGear);
                var bot = new SurvivorBot(view.Sim);
                int guard = 0;
                while (!until(view) && view.Sim.Outcome == SOutcome.Playing && guard++ < 60 * 400)
                {
                    if (view.Sim.PendingChoices != null)
                    {
                        view.Choose(SurvivorBot.PickCard(view.Sim.PendingChoices));
                        continue;
                    }
                    view.Step(bot.Move());
                    view.Refresh(SurvivorSim.Dt);
                }
                if (!until(view)) throw new Exception("조건에 닿기 전에 판이 끝났다: " + view.Sim.Outcome + " t=" + view.Sim.Time);
                // 카드는 0.3초 뒤에 튀어 오르니 다 뜬 뒤를 찍는다. 효과도 조금 흐르게 둔다.
                for (int i = 0; i < settle; i++) view.Refresh(SurvivorSim.Dt);

                Canvas.ForceUpdateCanvases();
                Capture(camera, Path.Combine(dir, name + ".png"));
                Debug.Log("[ProtoShots] " + name + " (t=" + (int)view.Sim.Time + ", Lv " + view.Sim.Level + ", 적 " + view.Sim.Enemies.Count + ", 카드 " + (view.Sim.PendingChoices != null) + ", " + GearOf(view.Sim) + ")");
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
