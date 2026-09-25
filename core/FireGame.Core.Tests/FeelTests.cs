using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using FireGame.Core.Sim;
using FireGame.UnityLayer.Feel;
using Xunit;

namespace FireGame.Core.Tests
{
    /// <summary>
    /// 연출 판정. 언제 무슨 소리를 내고 얼마나 흔들지를 실제 판으로 확인한다.
    /// 소리가 엉뚱할 때 나거나 두 번 나면 손맛이 아니라 소음이다.
    /// </summary>
    public class FeelTests
    {
        private const float Dt = 0.05f;

        /// <summary>봇이 판을 끝낼 때까지 뛰며 나온 큐를 모두 모은다.</summary>
        private static List<Cue> PlayAndCollect(StageRunner runner, out List<float> shakes)
        {
            var bot = new GreedyBot(runner);
            var tracker = new FeelTracker(runner);
            var cues = new List<Cue>();
            shakes = new List<float>();

            for (int step = 0; step < 6000 && !runner.IsOver; step++)
            {
                bot.Step(Dt);
                tracker.Update(runner);
                cues.AddRange(tracker.Cues);
                shakes.Add(tracker.Shake);
            }

            // 끝난 뒤 한 프레임 더 — 결과 소리가 여러 번 나면 안 된다.
            tracker.Update(runner);
            cues.AddRange(tracker.Cues);
            return cues;
        }

        private static int Count(List<Cue> cues, Cue cue)
        {
            int n = 0;
            foreach (Cue c in cues) if (c == cue) n++;
            return n;
        }

        private static StageRunner OneShotStage(string row, int foamLevel)
        {
            var stage = new StageDef(99, "T", new[]
            {
                "########",
                row,
                "########",
            }, Wind.None, 60f, 100);

            SaveData save = SaveData.NewGame();
            if (foamLevel > 0)
            {
                save.SetLevel(EquipmentId.Bucket, 0);
                save.SetLevel(EquipmentId.FoamExtinguisher, foamLevel);
            }
            return new StageRunner(stage, Loadout.From(save));
        }

        // --- TC-1 ---
        [Fact]
        public void BotClearingTheHouse_HearsSprayPutOutRescueAndOneWin()
        {
            MissionDef house = Campaign.Missions[0];
            var runner = new StageRunner(house.Stage, Loadout.From(RequiredGearTests.SaveWithRequiredGear(house.Id)));

            List<Cue> cues = PlayAndCollect(runner, out _);

            Assert.Equal(StageOutcome.Won, runner.Outcome);
            Assert.Contains(Cue.SprayWater, cues);
            Assert.Contains(Cue.PutOut, cues);
            Assert.Contains(Cue.PickUp, cues);
            Assert.Contains(Cue.Rescued, cues);
            Assert.Equal(1, Count(cues, Cue.Won));
            Assert.Equal(0, Count(cues, Cue.Failed));
        }

        // --- TC-2 ---
        [Fact]
        public void FirstFrame_IsSilentAndStill()
        {
            foreach (StageDef stage in StageCatalog.All)
            {
                var runner = new StageRunner(stage, Loadout.FromIds(new[] { EquipmentId.Bucket }));
                var tracker = new FeelTracker(runner);
                runner.Update(0f, default);
                tracker.Update(runner);

                Assert.Empty(tracker.Cues);
                Assert.Equal(0f, tracker.Shake);
            }
        }

        // --- TC-3 ---
        [Fact]
        public void WaterOnOil_IsABackfire_WithAHardShake()
        {
            // 역효과는 옆 칸으로 옮겨붙는 것이라 번질 기름(~)이 있어야 한다.
            StageRunner runner = OneShotStage("#.@%~.X#", 0);
            var tracker = new FeelTracker(runner);

            runner.Update(0.001f, new StageInput { Fire = true, Slot = 0 });
            tracker.Update(runner);

            Assert.Equal(1, Count(tracker.Cues, Cue.Backfire));
            Assert.DoesNotContain(Cue.PutOut, tracker.Cues);
            Assert.Contains(Cue.SprayWater, tracker.Cues);
            Assert.Equal(FeelTracker.BackfireShake, tracker.Shake, 3);
        }

        // --- TC-4 ---
        [Fact]
        public void PuttingOutOneCell_SoundsButDoesNotShake()
        {
            StageRunner runner = OneShotStage("#@%...X#", 1);
            var tracker = new FeelTracker(runner);

            runner.Update(0.001f, new StageInput { Fire = true, Slot = 0 });
            tracker.Update(runner);

            Assert.Equal(1, runner.LastShotExtinguished);
            Assert.Contains(Cue.SprayFoam, tracker.Cues);
            Assert.Contains(Cue.PutOut, tracker.Cues);
            Assert.Equal(0f, tracker.Shake);
        }

        // --- TC-5 ---
        [Fact]
        public void PuttingOutSeveralCells_ShakesByHowMany()
        {
            StageRunner runner = OneShotStage("#@%%..X#", 1);
            var tracker = new FeelTracker(runner);

            runner.Update(0.001f, new StageInput { Fire = true, Slot = 0 });
            tracker.Update(runner);

            int n = runner.LastShotExtinguished;
            Assert.True(n >= 2, "두 칸을 못 껐다: " + n);
            Assert.Contains(Cue.PutOut, tracker.Cues);
            Assert.Equal(System.Math.Min(FeelTracker.MaxPutOutShake, 0.1f + (0.06f * n)), tracker.Shake, 3);
            Assert.True(tracker.Shake <= FeelTracker.MaxPutOutShake);
        }

        // --- TC-6 ---
        [Fact]
        public void Critical_SoundsOncePerCivilian()
        {
            var runner = new StageRunner(StageCatalog.Residential, Loadout.FromIds(new[] { EquipmentId.Bucket }));
            var tracker = new FeelTracker(runner);
            Civilian civilian = runner.Civilians[0];

            int critical = 0;
            civilian.Stamina = 0.3f;
            for (int i = 0; i < 20; i++)
            {
                runner.Update(Dt, default);
                tracker.Update(runner);
                critical += Count(tracker.Cues, Cue.Critical);
            }

            Assert.Equal(1, critical);
        }

        // --- TC-7 ---
        [Fact]
        public void FireLoudness_IsZeroWithoutFire_AndLouderWhenCloser()
        {
            var runner = new StageRunner(new StageDef(98, "T", new[]
            {
                "##################",
                "#@..............X#",
                "##################",
            }, Wind.None, 60f, 100), Loadout.FromIds(new[] { EquipmentId.Bucket }));
            FireGrid grid = runner.Grid;

            Assert.Equal(0f, FeelMath.FireLoudness(grid, 1.5f, 1.5f));

            grid[3, 1].State = CellState.Burning;
            float near = FeelMath.FireLoudness(grid, 1.5f, 1.5f);
            float far = FeelMath.FireLoudness(grid, 8.5f, 1.5f);
            float outOfReach = FeelMath.FireLoudness(grid, 16.5f, 1.5f);

            Assert.True(near > far, "가까운 불이 " + near + ", 먼 불이 " + far);
            Assert.Equal(0f, outOfReach);
            Assert.InRange(near, 0f, 1f);

            for (int x = 1; x < 17; x++) grid[x, 1].State = CellState.Burning;
            Assert.InRange(FeelMath.FireLoudness(grid, 8.5f, 1.5f), near, 1f);
        }

        // --- TC-8 ---
        [Fact]
        public void Trauma_DecaysToZero_AndNeverBelow()
        {
            float trauma = 1f;
            float previous = trauma;
            for (int i = 0; i < 20; i++)
            {
                trauma = FeelMath.DecayTrauma(trauma, 0.1f);
                Assert.True(trauma <= previous);
                Assert.True(trauma >= 0f);
                previous = trauma;
            }
            Assert.Equal(0f, trauma);
        }

        // --- TC-9 ---
        [Fact]
        public void Tracker_NeverChangesTheRun()
        {
            MissionDef gas = Campaign.ById(2);
            SaveData save = RequiredGearTests.SaveWithRequiredGear(gas.Id);

            var watched = new StageRunner(gas.Stage, Loadout.From(save));
            var plain = new StageRunner(gas.Stage, Loadout.From(save));

            PlayAndCollect(watched, out List<float> shakes);
            new GreedyBot(plain).Play(Dt);

            Assert.Equal(plain.Outcome, watched.Outcome);
            Assert.Equal(plain.TicksElapsed, watched.TicksElapsed);
            Assert.Equal(plain.Grid.IntactRatio(), watched.Grid.IntactRatio());
            foreach (float shake in shakes) Assert.InRange(shake, 0f, 1f);
        }
    }
}
