using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using Xunit;

namespace FireGame.Core.Tests
{
    public class CampaignTests
    {
        private static StageResult Result(bool won, float intact, int wet)
        {
            return new StageResult { Won = won, IntactRatio = intact, WetCellCount = wet };
        }

        // --- TC-1 ---
        [Fact]
        public void Campaign_HasOneMissionPerStage_InStageOrder()
        {
            Assert.Equal(StageCatalog.All.Length, Campaign.Missions.Length);
            for (int i = 0; i < Campaign.Missions.Length; i++)
            {
                Assert.Equal(i, Campaign.Missions[i].Id);
                Assert.Same(StageCatalog.All[i], Campaign.Missions[i].Stage);
            }
        }

        // --- TC-2 ---
        [Fact]
        public void Missions_UnlockOneAfterAnother()
        {
            Assert.Equal(-1, Campaign.Missions[0].RequiresMission);
            for (int i = 1; i < Campaign.Missions.Length; i++)
            {
                Assert.Equal(Campaign.Missions[i - 1].Id, Campaign.Missions[i].RequiresMission);
            }
        }

        // --- TC-3 ---
        [Fact]
        public void ById_ReturnsNullForUnknownIds()
        {
            Assert.Same(Campaign.Missions[1], Campaign.ById(1));
            Assert.Null(Campaign.ById(99));
            Assert.Null(Campaign.ById(-1));
        }

        // --- TC-4 ---
        [Fact]
        public void EveryMission_HasKoreanTextFilledIn()
        {
            foreach (MissionDef m in Campaign.Missions)
            {
                Assert.False(string.IsNullOrWhiteSpace(m.Title));
                Assert.False(string.IsNullOrWhiteSpace(m.Location));
                Assert.NotEmpty(m.Briefing);
                Assert.NotEmpty(m.Debrief);
                foreach (string line in m.Briefing) Assert.False(string.IsNullOrWhiteSpace(line));
                foreach (string line in m.Debrief) Assert.False(string.IsNullOrWhiteSpace(line));
            }
        }

        // --- TC-5 ---
        [Fact]
        public void MapPositions_AreInsideTheMapAndDoNotOverlap()
        {
            var seen = new HashSet<(float, float)>();
            foreach (MissionDef m in Campaign.Missions)
            {
                Assert.InRange(m.MapX, 0f, 1f);
                Assert.InRange(m.MapY, 0f, 1f);
                Assert.True(seen.Add((m.MapX, m.MapY)), m.Title + " 의 지도 위치가 다른 현장과 겹친다");
            }
        }

        // --- TC-6 ---
        [Fact]
        public void PerfectRun_EarnsThreeStars()
        {
            Assert.Equal(3, StarRating.For(Result(true, 0.8f, 12)));
            Assert.Equal(3, StarRating.For(Result(true, StarRating.IntactForStar, StarRating.MaxWetCellsForStar)));
        }

        // --- TC-7 ---
        [Fact]
        public void LosingTooMuchOfTheBuilding_CostsAStar()
        {
            Assert.Equal(2, StarRating.For(Result(true, 0.5f, 10)));
        }

        // --- TC-8 ---
        [Fact]
        public void AnyWin_IsWorthAtLeastOneStar()
        {
            Assert.Equal(1, StarRating.For(Result(true, 0.3f, 80)));
        }

        // --- TC-9 ---
        [Fact]
        public void ALoss_IsWorthNoStars()
        {
            Assert.Equal(0, StarRating.For(Result(false, 1f, 0)));
        }

        // --- TC-10 ---
        [Fact]
        public void TheTutorial_CanBeClearedForAtLeastOneStarWithTheFreeBucket()
        {
            var runner = new StageRunner(Campaign.Missions[0].Stage, new List<int> { EquipmentId.Bucket });
            new GreedyBot(runner).Play();

            Assert.True(StarRating.For(runner.BuildResult()) >= 1);
        }
    }
}
