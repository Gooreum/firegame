using FireGame.Core.Data;
using FireGame.Core.Game;
using Xunit;

namespace FireGame.Core.Tests
{
    public class ReadinessTests
    {
        private static MissionDef Mall
        {
            get { return Campaign.ById(1); }
        }

        // --- TC-1 ---
        [Fact]
        public void ANewGame_IsShortOfCo2LevelTwoForTheMall()
        {
            SaveData save = SaveData.NewGame();

            var missing = Readiness.Missing(save, Mall);

            Assert.Single(missing);
            Assert.Equal(EquipmentId.Extinguisher, missing[0].TrackId);
            Assert.Equal(0, missing[0].CurrentLevel);
            Assert.Equal(2, missing[0].RequiredLevel);
            Assert.Equal(UpgradeCatalog.Extinguisher.CostToReach(1) + UpgradeCatalog.Extinguisher.CostToReach(2), missing[0].Cost);
            Assert.Equal(missing[0].Cost, Readiness.CostToReady(save, Mall));
            Assert.False(Readiness.IsReady(save, Mall));
        }

        // --- TC-2 ---
        [Fact]
        public void NoRequirements_OrMoreThanEnough_IsReady()
        {
            SaveData save = SaveData.NewGame();
            Assert.True(Readiness.IsReady(save, Campaign.ById(0)));
            Assert.Equal(0, Readiness.CostToReady(save, Campaign.ById(0)));

            save.SetLevel(EquipmentId.Extinguisher, 5);
            Assert.Empty(Readiness.Missing(save, Mall));
            Assert.Equal(0, Readiness.CostToReady(save, Mall));

            Assert.Empty(Readiness.Missing(save, null));
        }

        // --- TC-3 ---
        [Fact]
        public void NextCall_IsTheFirstOpenMissionWithoutStars()
        {
            SaveData save = SaveData.NewGame();
            Assert.Same(Campaign.Missions[0], Readiness.NextCall(save));

            save.RecordResult(0, 1);
            Assert.Same(Campaign.Missions[1], Readiness.NextCall(save));

            foreach (MissionDef mission in Campaign.Missions) save.RecordResult(mission.Id, 1);
            Assert.Null(Readiness.NextCall(save));
        }

        // --- TC-4 ---
        [Fact]
        public void AffordableUpgrades_CountsWhatTheMoneyCanBuyNow()
        {
            SaveData save = SaveData.NewGame();
            Assert.Equal(0, Readiness.AffordableUpgrades(save));

            // $150이면 양동이 Lv2와 소방화 해금을 살 수 있다.
            save.Money = 150;
            Assert.Equal(2, Readiness.AffordableUpgrades(save));

            // 상한이 없으니 아무리 올려도 살 게 남는다. 돈이 있으면 여섯 칸 모두 살 수 있다.
            foreach (UpgradeTrack track in UpgradeCatalog.All) save.SetLevel(track.Id, 10);
            save.Money = 1000000;
            Assert.Equal(UpgradeCatalog.All.Length, Readiness.AffordableUpgrades(save));

            // 막히는 건 돈이 없을 때뿐이다.
            save.Money = 0;
            Assert.Equal(0, Readiness.AffordableUpgrades(save));
        }

        // --- TC-7 ---
        /// <summary>
        /// 플레이어가 실제로 거치는 길: 지도에서 신고를 보고, 브리핑에서 모자란 장비를 확인하고,
        /// 상점으로 가서 올리고, 출동한다. 돈이 모자라면 직전 현장을 다시 뛴다.
        /// </summary>
        [Fact]
        public void AFreshGame_FollowsEveryCallThroughTheShop_ToTheEnd()
        {
            var flow = new GameFlow(SaveData.NewGame());
            int replays = 0;
            MissionDef previous = null;

            for (MissionDef call = Readiness.NextCall(flow.Save); call != null; call = Readiness.NextCall(flow.Save))
            {
                Assert.True(flow.SelectMission(call.Id));

                while (!Readiness.IsReady(flow.Save, call))
                {
                    if (flow.Save.Money >= Readiness.CostToReady(flow.Save, call))
                    {
                        flow.GoToShop();
                        foreach (Shortfall shortfall in Readiness.Missing(flow.Save, call))
                        {
                            for (int level = shortfall.CurrentLevel; level < shortfall.RequiredLevel; level++)
                            {
                                Assert.Equal(PurchaseResult.Success, flow.Upgrade(shortfall.TrackId));
                            }
                        }
                        flow.CloseShop();
                        Assert.True(flow.SelectMission(call.Id));
                        continue;
                    }

                    // 모자라면 직전 현장을 한 번 더 뛰어 번다.
                    Assert.NotNull(previous);
                    flow.BackToMap();
                    Assert.True(flow.SelectMission(previous.Id));
                    flow.BeginMission();
                    PlayToTheEnd(flow);
                    Assert.Equal(StageOutcome.Won, flow.LastOutcome);
                    replays++;
                    Assert.True(replays <= 2, call.Title + " 앞에서 반복이 너무 많다. 보유 " + flow.Save.Money);
                    flow.BackToMap();
                    Assert.True(flow.SelectMission(call.Id));
                }

                flow.BeginMission();
                PlayToTheEnd(flow);
                Assert.True(flow.LastOutcome == StageOutcome.Won, call.Title + " 을 필요 장비로 깨야 한다");
                flow.BackToMap();
                previous = call;
            }

            foreach (MissionDef mission in Campaign.Missions) Assert.True(flow.Save.StarsFor(mission.Id) >= 1);
            System.Console.WriteLine("반복 " + replays + "회, 끝난 뒤 보유 " + flow.Save.Money);
        }

        private static void PlayToTheEnd(GameFlow flow)
        {
            new GreedyBot(flow.Runner).Play();
            flow.Update(0.016f);
            Assert.Equal(GameScreen.Result, flow.Screen);
        }
    }
}
