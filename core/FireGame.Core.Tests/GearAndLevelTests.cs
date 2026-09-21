using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using FireGame.Core.Sim;
using Xunit;

namespace FireGame.Core.Tests
{
    /// <summary>상점에서 올린 레벨이 현장에서 실제로 효과를 내는지.</summary>
    public class GearAndLevelTests
    {
        private static FireGrid OpenFloor(int size)
        {
            var grid = new FireGrid(size, size);
            for (int i = 0; i < grid.Count; i++)
            {
                grid.Cells[i].Material = (byte)MaterialId.Floor;
                grid.Cells[i].State = CellState.Intact;
            }
            return grid;
        }

        private static StageRunner RunnerWith(SaveData save)
        {
            return new StageRunner(StageCatalog.Residential, Loadout.From(save));
        }

        // --- TC-1 ---
        [Fact]
        public void HeatResistantSuit_CutsFireDamageToAThird()
        {
            SaveData save = SaveData.NewGame();
            save.SetLevel(GearId.Suit, 4);
            PlayerState player = RunnerWith(save).Player;

            var grid = OpenFloor(9);
            grid[4, 4].Material = (byte)MaterialId.Wood;
            grid[4, 4].State = CellState.Burning;
            player.Spawn(new GridPoint(4, 4));
            player.Hp = GameConfig.PlayerMaxHp;

            player.Update(1f, grid, 0f, 0f);

            Assert.Equal(GameConfig.PlayerMaxHp - (GameConfig.FireDamageInCell * 0.35f), player.Hp, 3);
            Assert.Equal(4, player.SuitLevel);
        }

        // --- TC-2 ---
        [Fact]
        public void Boots_MakeTheFirefighterFaster()
        {
            SaveData save = SaveData.NewGame();
            save.SetLevel(GearId.Boots, 4);
            PlayerState player = RunnerWith(save).Player;

            var grid = OpenFloor(12);
            player.Spawn(new GridPoint(2, 5));
            float startX = player.X;

            player.Update(0.5f, grid, 1f, 0f);

            Assert.Equal(GameConfig.PlayerSpeed * 1.4f * 0.5f, player.X - startX, 3);
        }

        // --- TC-3 ---
        [Fact]
        public void LevelledExtinguisher_HasMorePowerAndCharges()
        {
            SaveData save = SaveData.NewGame();
            save.SetLevel(EquipmentId.Extinguisher, 3);
            StageRunner runner = RunnerWith(save);

            EquipmentDef slot1 = runner.SlotEquipment(1);
            Assert.Equal(EquipmentId.Extinguisher, slot1.Id);
            Assert.Equal(EquipmentCatalog.Extinguisher.Agent.Power * 1.4f, slot1.Agent.Power, 3);
            Assert.Equal(slot1.MaxCharges, runner.Player.Charges[1]);
            Assert.True(runner.Player.Charges[1] > EquipmentCatalog.Extinguisher.MaxCharges);
        }

        // --- TC-4 ---
        [Fact]
        public void LevelledBucket_CoolsAFireHarder()
        {
            float Cooled(int level)
            {
                var stage = new StageDef(99, "T", new[]
                {
                    "#######",
                    "#.@*..#",
                    "#.....#",
                    "#######",
                }, Wind.None, 60f, 100);

                SaveData save = SaveData.NewGame();
                save.SetLevel(EquipmentId.Bucket, level);
                var runner = new StageRunner(stage, Loadout.From(save));
                runner.Grid[3, 1].Heat = 10f;

                runner.Update(0.001f, new StageInput { Fire = true, Slot = 0 });
                return 10f - runner.Grid[3, 1].Heat;
            }

            float lv1 = Cooled(1);
            float lv5 = Cooled(5);
            Assert.True(lv1 > 0f, "양동이가 앞 칸(동쪽)을 맞혀야 한다");
            Assert.Equal(lv1 * 1.8f, lv5, 2);
        }

        // --- TC-5 ---
        [Fact]
        public void GearBoughtInTheShop_ShowsUpInTheNextMission()
        {
            SaveData save = SaveData.NewGame();
            save.Money = 5000;
            var flow = new GameFlow(save);
            flow.OpenShop();
            Assert.Equal(PurchaseResult.Success, flow.Upgrade(GearId.Suit));
            Assert.Equal(PurchaseResult.Success, flow.Upgrade(GearId.Suit));
            Assert.Equal(PurchaseResult.Success, flow.Upgrade(GearId.Boots));
            flow.CloseShop();

            Assert.True(flow.SelectMission(0));
            flow.BeginMission();

            PlayerState player = flow.Runner.Player;
            Assert.Equal(2, player.SuitLevel);
            Assert.Equal(GearStats.DamageMultiplier(2), player.DamageMultiplier, 3);
            Assert.Equal(GearStats.SpeedMultiplier(1), player.SpeedMultiplier, 3);
        }

        // --- TC-6 ---
        [Fact]
        public void Levels_SurviveARestart()
        {
            SaveData save = SaveData.NewGame();
            save.SetLevel(EquipmentId.Bucket, 3);
            save.SetLevel(GearId.Suit, 1);
            save.SetLevel(GearId.Boots, 2);

            Assert.True(SaveData.TryDeserialize(save.Serialize(), out SaveData restored));
            var flow = new GameFlow(restored);
            flow.SelectMission(0);
            flow.BeginMission();

            Assert.Equal(EquipmentCatalog.Bucket.AtLevel(3).Agent.Power, flow.Runner.SlotEquipment(0).Agent.Power, 3);
            Assert.Equal(1, flow.Runner.Player.SuitLevel);
            Assert.Equal(GearStats.SpeedMultiplier(2), flow.Runner.Player.SpeedMultiplier, 3);
        }

        // --- TC-7 ---
        [Fact]
        public void SlotEquipment_IsNullForEmptyOrOutOfRangeSlots()
        {
            var runner = new StageRunner(StageCatalog.Residential, EquipmentCatalog.StartingEquipment);
            Assert.Null(runner.SlotEquipment(1));
            Assert.Null(runner.SlotEquipment(-1));
            Assert.Null(runner.SlotEquipment(PlayerState.SlotCount));
        }

        // --- TC-8 ---
        [Fact]
        public void IdListConstructor_IsLevel1WithNoGear()
        {
            var runner = new StageRunner(StageCatalog.Residential, new List<int> { EquipmentId.Bucket, EquipmentId.Hose });
            Assert.Same(EquipmentCatalog.Hose, runner.SlotEquipment(1));
            Assert.Equal(0, runner.Player.SuitLevel);
            Assert.Equal(1f, runner.Player.SpeedMultiplier);
            Assert.Equal(1f, runner.Player.DamageMultiplier);
        }
    }
}
