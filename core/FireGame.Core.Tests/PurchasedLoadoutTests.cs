using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using Xunit;

namespace FireGame.Core.Tests
{
    /// <summary>
    /// 상점에서 실제로 산 순서 그대로 현장에 나가는 경로를 검증한다.
    ///
    /// 밸런스 테스트는 [양동이, 폼]처럼 장비를 골라서 넣었기 때문에,
    /// 장비 칸이 3개라 네 번째로 산 폼이 빠지는 버그를 잡지 못했다.
    /// </summary>
    public class PurchasedLoadoutTests
    {
        private static GameFlow FlowWithEverythingBought()
        {
            SaveData save = SaveData.NewGame();
            save.Money = 100000;
            for (int id = 0; id < Campaign.Missions.Length; id++) save.RecordResult(id, 1);

            var flow = new GameFlow(save);
            flow.OpenShop();
            Assert.Equal(PurchaseResult.Success, flow.Buy(EquipmentId.Extinguisher));
            Assert.Equal(PurchaseResult.Success, flow.Buy(EquipmentId.Hose));
            Assert.Equal(PurchaseResult.Success, flow.Buy(EquipmentId.FoamExtinguisher));
            flow.CloseShop();
            return flow;
        }

        // --- TC-1 ---
        [Fact]
        public void EveryPurchasedItem_IsInASlot_WhenTheMissionStarts()
        {
            GameFlow flow = FlowWithEverythingBought();

            Assert.True(flow.SelectMission(2));
            flow.BeginMission();

            var slots = new List<int>(flow.Runner.Player.Slots);
            Assert.Contains(EquipmentId.Bucket, slots);
            Assert.Contains(EquipmentId.Extinguisher, slots);
            Assert.Contains(EquipmentId.Hose, slots);
            Assert.Contains(EquipmentId.FoamExtinguisher, slots);
        }

        // --- TC-2 ---
        [Fact]
        public void GasStation_IsBeaten_WithEverythingBoughtInShopOrder()
        {
            GameFlow flow = FlowWithEverythingBought();
            Assert.True(flow.SelectMission(2));
            flow.BeginMission();

            Assert.Equal(StageOutcome.Won, new GreedyBot(flow.Runner).Play());
        }

        // --- TC-3 & TC-4 ---
        /// <summary>
        /// 소화전과 멀리 떨어진 곳에서 호스만으로 목조 화재 군집을 끈다.
        /// 예전엔 소화전 8칸 밖에서 호스가 조용히 무시돼 "써도 효과가 없었다".
        /// </summary>
        [Fact]
        public void Hose_PutsOutAWoodFire_FarFromAnyHydrant()
        {
            var stage = new StageDef(
                id: 99,
                name: "HOSE",
                map: new[]
                {
                    "##############################",
                    "#H...........................#",
                    "#............................#",
                    "#............................#",
                    "#....................WWW.....#",
                    "#.................@..W*W.....#",
                    "#....................WWW.....#",
                    "#............................#",
                    "##############################",
                },
                wind: Sim.Wind.None,
                timeLimitSeconds: 120f,
                basePayout: 100);

            var runner = new StageRunner(stage, new[] { EquipmentId.Hose });
            var hydrant = runner.Hydrants[0];
            float far = (runner.Player.X - hydrant.X) * (runner.Player.X - hydrant.X);
            Assert.True(far > 8f * 8f, "소화전에서 8칸 넘게 떨어진 곳에서 시작해야 한다");

            int burningAtStart = runner.Grid.CountBurning();
            Assert.True(burningAtStart > 0);

            // 오른쪽을 보고(조준 E) 호스를 계속 쏜다.
            runner.Update(0.05f, new StageInput { MoveX = 0.01f, Slot = 0 });
            for (int i = 0; i < 400 && runner.Grid.CountBurning() > 0; i++)
            {
                runner.Update(0.05f, new StageInput { Fire = true, Slot = 0 });
            }

            Assert.Equal(0, runner.Grid.CountBurning());
        }

        // --- TC-6 ---
        [Fact]
        public void MoreEquipmentThanSlots_FillsOnlyTheSlots()
        {
            var ids = new[] { 0, 1, 2, 3, 0, 1, 99 };
            var runner = new StageRunner(StageCatalog.Residential, ids);

            Assert.Equal(PlayerState.SlotCount, runner.Player.Slots.Length);
            Assert.Equal(new[] { 0, 1, 2, 3 }, runner.Player.Slots);
        }
    }
}
