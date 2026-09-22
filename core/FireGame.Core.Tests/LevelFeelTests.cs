using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using FireGame.Core.Sim;
using Xunit;

namespace FireGame.Core.Tests
{
    /// <summary>레벨업이 느껴지는지: 상점 문구, 출동 통계, 최고 기록, 그리고 봇으로 잰 실제 속도 차이.</summary>
    public class LevelFeelTests
    {
        // --- TC-1 ---
        [Fact]
        public void ShotsToPutOut_FallAsFoamLevelsUp()
        {
            int previous = int.MaxValue;
            for (int level = 1; level <= 10; level++)
            {
                int shots = LevelPreview.ShotsToPutOut(EquipmentCatalog.FoamExtinguisher.AtLevel(level), FireClass.B, 3f);
                Assert.True(shots <= previous, "Lv" + level + " 발 수가 늘었다");
                previous = shots;
            }

            Assert.True(LevelPreview.ShotsToPutOut(EquipmentCatalog.FoamExtinguisher.AtLevel(10), FireClass.B, 3f)
                        < LevelPreview.ShotsToPutOut(EquipmentCatalog.FoamExtinguisher.AtLevel(1), FireClass.B, 3f));

            // 안 듣는 약제는 "못 끈다"(99).
            Assert.Equal(99, LevelPreview.ShotsToPutOut(EquipmentCatalog.FoamExtinguisher, FireClass.C, 1f));
        }

        // --- TC-2 ---
        [Fact]
        public void ReferenceStage_IsTheFiercestOpenStageWithThatFire()
        {
            SaveData save = SaveData.NewGame();
            save.RecordResult(0, 1);
            Assert.Same(StageCatalog.Shopping, LevelPreview.ReferenceStage(save, FireClass.C));

            foreach (MissionDef mission in Campaign.Missions) save.RecordResult(mission.Id, 1);
            StageDef oil = LevelPreview.ReferenceStage(save, FireClass.B);
            Assert.Equal(6f, oil.FireIntensity);
        }

        // --- TC-3 ---
        [Fact]
        public void FoamLevelFour_Card_AnnouncesTheNextMilestoneFirst()
        {
            SaveData save = SaveData.NewGame();
            save.SetLevel(EquipmentId.FoamExtinguisher, 4);

            List<string> lines = LevelPreview.NextLevelLines(save, UpgradeCatalog.FoamExtinguisher);

            Assert.Equal("특성! 앞 9칸에 뿜기", lines[0]);
            Assert.Contains(lines, line => line.StartsWith("기름불 한가운데(") && line.Contains("발"));
            Assert.Contains(lines, line => line.StartsWith("횟수 "));
            Assert.Equal("Lv5 · 앞 9칸에 뿜기", LevelPreview.NextMilestone(UpgradeCatalog.FoamExtinguisher, 4));
            Assert.Equal("Lv10 · 앞 12칸에 뿜기", LevelPreview.NextMilestone(UpgradeCatalog.FoamExtinguisher, 5));
            Assert.Equal("Lv5 · 물줄기 끝이 T자로 퍼진다", LevelPreview.NextMilestone(UpgradeCatalog.Hose, 1));
        }

        // --- TC-4 ---
        [Fact]
        public void SuitAndBoots_Cards_SpeakInSecondsAndCells()
        {
            SaveData save = SaveData.NewGame();
            save.SetLevel(GearId.Suit, 2);

            List<string> suit = LevelPreview.NextLevelLines(save, UpgradeCatalog.Suit);
            Assert.Equal("특수 방화복", suit[0]);
            Assert.StartsWith("불 속에서 버티는 시간 ", suit[1]);
            Assert.True(LevelPreview.SecondsInFire(3) > LevelPreview.SecondsInFire(2));

            List<string> boots = LevelPreview.NextLevelLines(save, UpgradeCatalog.Boots);
            Assert.Equal("초당 4.0 → 4.4칸 달린다", boots[0]);
        }

        // --- TC-5 ---
        [Fact]
        public void LockedAndMaxedCards_ShowUnlockHintOrCurrentValues()
        {
            SaveData save = SaveData.NewGame();
            List<string> locked = LevelPreview.NextLevelLines(save, UpgradeCatalog.Hose);
            Assert.Equal("해금하면 현장에 들고 간다", locked[0]);

            save.SetLevel(GearId.Suit, UpgradeCatalog.Suit.MaxLevel);
            List<string> maxed = LevelPreview.NextLevelLines(save, UpgradeCatalog.Suit);
            Assert.DoesNotContain("→", maxed[1]);
            Assert.Null(LevelPreview.NextMilestone(UpgradeCatalog.Suit, 3));
            Assert.Null(LevelPreview.NextMilestone(UpgradeCatalog.Hose, 10));
        }

        // --- TC-6 ---
        [Fact]
        public void Runner_CountsShotsAndPutOutCells()
        {
            var stage = new StageDef(99, "T", new[]
            {
                "#######",
                "#@%%.X#",
                "#######",
            }, Wind.None, 60f, 100);

            SaveData save = SaveData.NewGame();
            save.SetLevel(EquipmentId.Bucket, 0);
            save.SetLevel(EquipmentId.FoamExtinguisher, 1);
            var runner = new StageRunner(stage, Loadout.From(save));

            runner.Update(0.001f, new StageInput { Fire = true, Slot = 0 });

            Assert.Equal(1, runner.ShotsFired);
            Assert.Equal(2, runner.LastShotExtinguished);
            Assert.Equal(2, runner.CellsExtinguished);
            Assert.Equal(1, runner.BuildResult().ShotsFired);
        }

        // --- TC-7 ---
        [Fact]
        public void BestTime_KeepsOnlyTheFastest()
        {
            SaveData save = SaveData.NewGame();
            Assert.Equal(-1, save.BestTimeFor(2));
            Assert.True(save.RecordTime(2, 60f));
            Assert.False(save.RecordTime(2, 70f));
            Assert.Equal(600, save.BestTimeFor(2));
            Assert.True(save.RecordTime(2, 50.04f));
            Assert.Equal(500, save.BestTimeFor(2));
        }

        // --- TC-8 ---
        [Fact]
        public void BestTime_SurvivesSaveLoad_AndOldSavesStillLoad()
        {
            SaveData save = SaveData.NewGame();
            save.RecordTime(1, 42.5f);
            Assert.True(SaveData.TryDeserialize(save.Serialize(), out SaveData restored));
            Assert.Equal(425, restored.BestTimeFor(1));

            Assert.True(SaveData.TryDeserialize("v3\nmoney=10\nlevels=0:1\nstars=0:2\n", out SaveData old));
            Assert.Equal(-1, old.BestTimeFor(0));
            Assert.DoesNotContain("best=", SaveData.NewGame().Serialize());
        }

        // --- TC-9 ---
        [Fact]
        public void GameFlow_RemembersThePreviousBest_ForTheResultScreen()
        {
            var flow = new GameFlow(SaveData.NewGame());

            Assert.True(flow.SelectMission(0));
            flow.BeginMission();
            new GreedyBot(flow.Runner).Play();
            flow.Update(0.016f);
            Assert.Equal(StageOutcome.Won, flow.LastOutcome);
            Assert.Equal(-1, flow.LastPreviousBest);
            Assert.True(flow.LastWasFastest);
            int first = flow.Save.BestTimeFor(0);
            Assert.True(first > 0);
            Assert.True(flow.LastResult.ShotsFired > 0);

            flow.RetryMission();
            new GreedyBot(flow.Runner).Play();
            flow.Update(0.016f);
            Assert.Equal(first, flow.LastPreviousBest);
        }

        // --- TC-10 ---
        /// <summary>
        /// 체감 보증: 같은 신고를 장비를 올려 가면 눈에 띄게 빨리 끝난다.
        /// 이게 깨지면 "돈 모아 올렸는데 별 차이 없네"가 된다.
        /// </summary>
        [Fact]
        public void HigherLevels_ClearTheSameCallNoticeablyFaster()
        {
            MissionDef gas = Campaign.ById(2);

            SaveData basic = RequiredGearTests.SaveWithRequiredGear(gas.Id);
            SaveData strong = SaveData.NewGame();
            foreach (UpgradeTrack track in UpgradeCatalog.All) strong.SetLevel(track.Id, 10);

            float slow = Elapsed(gas, basic);
            float fast = Elapsed(gas, strong);

            System.Console.WriteLine("주유소: 필요 장비 " + slow + "초, 전부 Lv10 " + fast + "초");
            Assert.True(fast <= slow * 0.8f, "Lv10이 " + fast + "초, 필요 장비가 " + slow + "초 — 20% 이상 빨라야 한다");
        }

        private static float Elapsed(MissionDef mission, SaveData save)
        {
            var runner = new StageRunner(mission.Stage, Loadout.From(save));
            Assert.Equal(StageOutcome.Won, new GreedyBot(runner).Play());
            return runner.BuildResult().ElapsedSeconds;
        }
    }
}
