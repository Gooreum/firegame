using FireGame.Core.Data;
using FireGame.Core.Game;
using Xunit;

namespace FireGame.Core.Tests
{
    public class GameFlowTests
    {
        /// <summary>현장을 골라 출동까지 한다.</summary>
        private static GameFlow Playing(int missionId = 0, SaveData save = null)
        {
            var flow = new GameFlow(save ?? SaveData.NewGame());
            Assert.True(flow.SelectMission(missionId));
            flow.BeginMission();
            Assert.Equal(GameScreen.Playing, flow.Screen);
            return flow;
        }

        /// <summary>봇에게 대신 플레이시키고, 마지막 한 번은 GameFlow로 넘겨 정산을 일으킨다.</summary>
        private static void FinishWithBot(GameFlow flow)
        {
            new GreedyBot(flow.Runner).Play();
            Assert.True(flow.Runner.IsOver);
            flow.Update(0.016f);
        }

        // --- TC-1 ---
        [Fact]
        public void NewGame_StartsOnTheMapWithTheShopClosed()
        {
            var flow = new GameFlow(SaveData.NewGame());

            Assert.Equal(GameScreen.Map, flow.Screen);
            Assert.False(flow.ShopOpen);
            Assert.Null(flow.CurrentMission);
            Assert.Null(flow.Runner);
        }

        // --- TC-2 ---
        [Fact]
        public void SelectingAnOpenMission_GoesToTheBriefing()
        {
            var flow = new GameFlow(SaveData.NewGame());

            Assert.True(flow.SelectMission(0));

            Assert.Equal(GameScreen.Briefing, flow.Screen);
            Assert.Same(Campaign.Missions[0], flow.CurrentMission);
            Assert.Null(flow.Runner);
        }

        // --- TC-3 ---
        [Fact]
        public void SelectingALockedMission_IsRefused()
        {
            var flow = new GameFlow(SaveData.NewGame());

            Assert.False(flow.SelectMission(1));
            Assert.False(flow.SelectMission(99));

            Assert.Equal(GameScreen.Map, flow.Screen);
            Assert.Null(flow.CurrentMission);
        }

        // --- TC-4 ---
        [Fact]
        public void BeginningTheMission_StartsTheStageRun()
        {
            GameFlow flow = Playing(0);

            Assert.NotNull(flow.Runner);
            Assert.Same(StageCatalog.Residential, flow.Runner.Def);
        }

        // --- TC-5 ---
        [Fact]
        public void BackingOutOfTheBriefing_ReturnsToTheMapWithoutStarting()
        {
            var flow = new GameFlow(SaveData.NewGame());
            flow.SelectMission(0);

            flow.BackToMap();

            Assert.Equal(GameScreen.Map, flow.Screen);
            Assert.Null(flow.Runner);
        }

        // --- TC-6 ---
        [Fact]
        public void FinishingAMission_PaysRatesAndSaves()
        {
            string written = null;
            var flow = new GameFlow(SaveData.NewGame()) { SaveWriter = text => written = text };
            flow.SelectMission(0);
            flow.BeginMission();

            FinishWithBot(flow);

            Assert.Equal(GameScreen.Result, flow.Screen);
            Assert.Equal(StageOutcome.Won, flow.LastOutcome);
            Assert.InRange(flow.LastStars, 1, 3);
            Assert.True(flow.LastWasNewBest);
            Assert.Equal(flow.LastPayout.Total, flow.Save.Money);
            Assert.Equal(flow.LastStars, flow.Save.StarsFor(0));

            Assert.NotNull(written);
            Assert.True(SaveData.TryDeserialize(written, out SaveData restored));
            Assert.Equal(flow.LastStars, restored.StarsFor(0));
            Assert.Equal(flow.Save.Money, restored.Money);
        }

        // --- TC-7 ---
        [Fact]
        public void RetryingFromTheResult_StartsTheSameMissionAgain()
        {
            GameFlow flow = Playing(0);
            FinishWithBot(flow);
            StageRunner previous = flow.Runner;

            flow.RetryMission();

            Assert.Equal(GameScreen.Playing, flow.Screen);
            Assert.NotSame(previous, flow.Runner);
            Assert.Same(StageCatalog.Residential, flow.Runner.Def);
        }

        // --- TC-8 ---
        [Fact]
        public void ClearingAMission_OpensTheNextOneOnTheMap()
        {
            GameFlow flow = Playing(0);
            FinishWithBot(flow);

            flow.BackToMap();
            Assert.Equal(GameScreen.Map, flow.Screen);

            Assert.True(flow.SelectMission(1));
            Assert.Same(Campaign.Missions[1], flow.CurrentMission);

            // 세 번째는 아직 잠겨 있다.
            flow.BackToMap();
            Assert.False(flow.SelectMission(2));
        }

        // --- TC-9 ---
        [Fact]
        public void TheShop_OpensFromTheMapAndPersistsPurchases()
        {
            SaveData save = SaveData.NewGame();
            save.Money = 800;
            string written = null;
            var flow = new GameFlow(save) { SaveWriter = text => written = text };

            flow.OpenShop();
            Assert.True(flow.ShopOpen);

            Assert.Equal(PurchaseResult.Success, flow.Upgrade(EquipmentId.Extinguisher));
            Assert.Equal(800 - UpgradeCatalog.Extinguisher.CostToReach(1), save.Money);
            Assert.True(save.Owns(EquipmentId.Extinguisher));
            Assert.NotNull(written);
            Assert.StartsWith("v3", written);
            Assert.Contains("1:1", written);

            // 상점을 닫으면 레벨업도 거절된다.
            flow.CloseShop();
            Assert.NotEqual(PurchaseResult.Success, flow.Upgrade(EquipmentId.Extinguisher));
            Assert.Equal(1, save.LevelOf(EquipmentId.Extinguisher));
            flow.OpenShop();

            written = null;
            Assert.Equal(PurchaseResult.NotEnoughMoney, flow.Upgrade(EquipmentId.FoamExtinguisher));
            Assert.Null(written);

            flow.CloseShop();
            Assert.False(flow.ShopOpen);
            Assert.Equal(GameScreen.Map, flow.Screen);
        }

        // --- TC-10 ---
        [Fact]
        public void ShopAndMapCommands_AreIgnoredInTheWrongState()
        {
            SaveData save = SaveData.NewGame();
            save.Money = 99999;

            // 상점이 열린 채로 현장을 누를 수 없다.
            var flow = new GameFlow(save);
            flow.OpenShop();
            Assert.False(flow.SelectMission(0));
            flow.CloseShop();

            // 플레이 중에는 상점을 열 수도, 살 수도 없다.
            GameFlow playing = Playing(0, save);
            playing.OpenShop();
            Assert.False(playing.ShopOpen);
            Assert.NotEqual(PurchaseResult.Success, playing.Upgrade(EquipmentId.Hose));
            Assert.False(save.Owns(EquipmentId.Hose));
            Assert.Equal(GameScreen.Playing, playing.Screen);

            // 플레이 중에는 지도로 빠질 수 없다(결과를 거쳐야 한다).
            playing.BackToMap();
            Assert.Equal(GameScreen.Playing, playing.Screen);
        }

        // --- TC-11 ---
        [Fact]
        public void JoystickInput_MovesProportionally_AndIsClampedToTheUnitCircle()
        {
            GameFlow flow = Playing(0);
            float x0 = flow.Runner.Player.X;

            flow.SetMove(0.5f, 0f);
            flow.Update(0.1f);
            Assert.Equal(GameConfig.PlayerSpeed * 0.1f * 0.5f, flow.Runner.Player.X - x0, 3);

            float x1 = flow.Runner.Player.X;
            flow.SetMove(5f, 0f);
            flow.Update(0.1f);
            Assert.Equal(GameConfig.PlayerSpeed * 0.1f, flow.Runner.Player.X - x1, 3);

            float x2 = flow.Runner.Player.X;
            flow.SetMove(0f, 0f);
            flow.Update(0.1f);
            Assert.Equal(x2, flow.Runner.Player.X, 4);
        }

        // --- TC-12 ---
        [Fact]
        public void FireButton_FiresOnlyWhileHeld()
        {
            GameFlow flow = Playing(0);
            PlayerState player = flow.Runner.Player;

            flow.SetFire(true);
            flow.Update(0.016f);
            Assert.True(player.Cooldowns[0] > 0f);

            flow.SetFire(false);
            player.Cooldowns[0] = 0f;
            flow.Update(0.016f);
            Assert.Equal(0f, player.Cooldowns[0]);
        }

        // --- TC-13 ---
        [Fact]
        public void SelectingAnEmptyOrInvalidSlot_KeepsTheCurrentOne()
        {
            SaveData save = SaveData.NewGame();
            save.SetLevel(EquipmentId.Extinguisher, 1);
            GameFlow flow = Playing(0, save);

            flow.SelectSlot(1);
            Assert.Equal(1, flow.ActiveSlot);

            flow.SelectSlot(2);   // 비어 있음
            flow.SelectSlot(-1);
            flow.SelectSlot(7);
            Assert.Equal(1, flow.ActiveSlot);

            flow.Update(0.016f);
            Assert.Equal(1, flow.Runner.Player.ActiveSlot);
        }

        // --- TC-14 ---
        [Fact]
        public void KeyboardAndJoystick_AddUpButStayWithinOne()
        {
            GameFlow flow = Playing(0);
            float x0 = flow.Runner.Player.X;

            flow.SetMove(1f, 0f);
            flow.SetKeyboard(1f, 0f, false, -1);
            flow.Update(0.1f);

            // 둘을 더해도 최대 속도를 넘지 않는다.
            Assert.Equal(GameConfig.PlayerSpeed * 0.1f, flow.Runner.Player.X - x0, 3);
        }

        // --- TC-15 ---
        [Fact]
        public void ALostMission_PaysNothingAndUnlocksNothing()
        {
            GameFlow flow = Playing(0);

            // 불 옆에 가만히 두면 결국 진다.
            for (int i = 0; i < 20000 && flow.Screen == GameScreen.Playing; i++)
            {
                flow.Update(0.05f);
            }

            Assert.Equal(GameScreen.Result, flow.Screen);
            Assert.NotEqual(StageOutcome.Won, flow.LastOutcome);
            Assert.Equal(0, flow.LastStars);
            Assert.False(flow.LastWasNewBest);
            Assert.Equal(0, flow.Save.Money);

            flow.BackToMap();
            Assert.False(flow.SelectMission(1));
        }
    }
}
