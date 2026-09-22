using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using Xunit;

namespace FireGame.Core.Tests
{
    /// <summary>
    /// 현장마다 적어 둔 "필요 장비"가 거짓말이 아닌지 봇으로 확인한다.
    /// 화면은 이 표를 보고 "장비 부족"을 띄우므로, 틀리면 플레이어는 필요 없는 걸 사거나
    /// 사도 못 깨는 현장에 갇힌다.
    /// </summary>
    public class RequiredGearTests
    {
        public static IEnumerable<object[]> MissionIndexes()
        {
            for (int i = 0; i < Campaign.Missions.Length; i++) yield return new object[] { i };
        }

        /// <summary>
        /// 이 현장까지의 필요 장비만 갖춘 세이브. 앞 현장에서 요구한 레벨도 이미 샀다고 본다.
        /// 필요 없는 장비(호스·방화복·소방화)는 없다.
        /// </summary>
        internal static SaveData SaveWithRequiredGear(int missionIndex)
        {
            SaveData save = SaveData.NewGame();
            for (int i = 0; i <= missionIndex; i++)
            {
                foreach (Requirement requirement in Campaign.Missions[i].Requirements)
                {
                    if (save.LevelOf(requirement.TrackId) < requirement.Level) save.SetLevel(requirement.TrackId, requirement.Level);
                }
            }
            return save;
        }

        private static StageOutcome Play(MissionDef mission, SaveData save)
        {
            var runner = new StageRunner(mission.Stage, Loadout.From(save));
            return new GreedyBot(runner).Play();
        }

        [Theory]
        [MemberData(nameof(MissionIndexes))]
        public void EveryMission_IsBeatenWithExactlyItsRequiredGear(int index)
        {
            MissionDef mission = Campaign.Missions[index];
            Assert.Equal(StageOutcome.Won, Play(mission, SaveWithRequiredGear(index)));
        }

        [Theory]
        [MemberData(nameof(MissionIndexes))]
        public void EveryMission_IsLostWithAnyRequirementOneLevelLower(int index)
        {
            MissionDef mission = Campaign.Missions[index];

            foreach (Requirement requirement in mission.Requirements)
            {
                SaveData save = SaveWithRequiredGear(index);
                save.SetLevel(requirement.TrackId, requirement.Level - 1);

                StageOutcome outcome = Play(mission, save);
                Assert.True(outcome != StageOutcome.Won,
                    mission.Title + ": " + requirement.TrackId + " Lv" + (requirement.Level - 1) + "로도 이겼다. 필요 장비 표가 틀렸다");
            }
        }

        [Theory]
        [MemberData(nameof(MissionIndexes))]
        public void EveryMission_IsStillBeaten_WhenYouAlsoBoughtOptionalGear(int index)
        {
            // 호스는 장비 칸 하나를 차지한다. 필요 없는 걸 더 샀다고 못 깨게 되면 안 된다.
            SaveData save = SaveWithRequiredGear(index);
            save.SetLevel(EquipmentId.Hose, 1);
            save.SetLevel(GearId.Suit, 1);
            save.SetLevel(GearId.Boots, 1);

            Assert.Equal(StageOutcome.Won, Play(Campaign.Missions[index], save));
        }

        [Fact]
        public void LaterCalls_NeedLevelledGear_NotJustUnlocks()
        {
            // 성장 루프의 핵심: 해금만으로는 부족하고 레벨업을 해야 풀리는 신고가 있어야 한다.
            int levelledGates = 0;
            foreach (MissionDef mission in Campaign.Missions)
            {
                foreach (Requirement requirement in mission.Requirements)
                {
                    if (requirement.Level >= 2) levelledGates++;
                }
            }

            Assert.True(levelledGates >= 3, "레벨업을 요구하는 신고가 " + levelledGates + "개뿐이다");
        }

        [Fact]
        public void Requirements_OnlyNameRealShopItems_WithinTheirLevelRange()
        {
            foreach (MissionDef mission in Campaign.Missions)
            {
                foreach (Requirement requirement in mission.Requirements)
                {
                    UpgradeTrack track = UpgradeCatalog.ById(requirement.TrackId);
                    Assert.NotNull(track);
                    Assert.InRange(requirement.Level, 1, track.MaxLevel);
                }
            }
        }
    }
}
