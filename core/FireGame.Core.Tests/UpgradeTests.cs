using FireGame.Core.Data;
using FireGame.Core.Game;
using Xunit;

namespace FireGame.Core.Tests
{
    public class UpgradeTests
    {
        // --- TC-1 ---
        [Fact]
        public void Catalog_HasFourEquipmentTracksPlusSuitAndBoots()
        {
            Assert.Equal(6, UpgradeCatalog.All.Length);
            foreach (EquipmentDef def in EquipmentCatalog.All)
            {
                Assert.Equal(UpgradeKind.Equipment, UpgradeCatalog.ById(def.Id).Kind);
            }
            Assert.Equal(UpgradeKind.Suit, UpgradeCatalog.ById(GearId.Suit).Kind);
            Assert.Equal(UpgradeKind.Boots, UpgradeCatalog.ById(GearId.Boots).Kind);
            Assert.Null(UpgradeCatalog.ById(777));
        }

        // --- TC-2 ---
        [Fact]
        public void Prices_AreTheCheaperLevelUpTable()
        {
            Assert.Equal(300, UpgradeCatalog.Extinguisher.CostToReach(1));
            Assert.Equal(800, UpgradeCatalog.Hose.CostToReach(1));
            Assert.Equal(1500, UpgradeCatalog.FoamExtinguisher.CostToReach(1));
            Assert.Equal(150, UpgradeCatalog.Bucket.CostToReach(2));
            Assert.Equal(200, UpgradeCatalog.Suit.CostToReach(1));
            Assert.Equal(150, UpgradeCatalog.Boots.CostToReach(1));

            Assert.Equal(1, UpgradeCatalog.Bucket.StartLevel);
            Assert.Equal(0, UpgradeCatalog.Hose.StartLevel);
        }

        // --- TC-3 ---
        [Fact]
        public void CostToReach_IsMinusOne_ForLevelsYouCannotBuy()
        {
            // 이제 -1은 "시작 레벨 이하"뿐이다. 위로는 끝이 없다.
            Assert.Equal(-1, UpgradeCatalog.Hose.CostToReach(0));
            Assert.Equal(-1, UpgradeCatalog.Bucket.CostToReach(1));   // 처음부터 가진 레벨
            Assert.Equal(-1, UpgradeCatalog.Suit.CostToReach(-3));
            Assert.True(UpgradeCatalog.Hose.CostToReach(500) > 0);
            Assert.True(UpgradeCatalog.Suit.CostToReach(11) > 0);
        }

        // --- TC-4 ---
        [Fact]
        public void Bucket_AtLevel5_HitsHarderAndFasterScoops()
        {
            EquipmentDef lv5 = EquipmentCatalog.Bucket.AtLevel(5);
            Assert.Equal(EquipmentCatalog.Bucket.Agent.Power * 1.8f, lv5.Agent.Power, 3);
            Assert.Equal(EquipmentCatalog.Bucket.CooldownSeconds * 0.6f, lv5.CooldownSeconds, 3);
            Assert.Equal(EquipmentCatalog.Bucket.Id, lv5.Id);
        }

        // --- TC-5 ---
        [Fact]
        public void Extinguisher_AtLevel3_HasMoreChargesAndPower()
        {
            EquipmentDef lv3 = EquipmentCatalog.Extinguisher.AtLevel(3);
            Assert.Equal(EquipmentCatalog.Extinguisher.Agent.Power * 1.4f, lv3.Agent.Power, 3);
            Assert.Equal((int)System.Math.Round(EquipmentCatalog.Extinguisher.MaxCharges * 1.5), lv3.MaxCharges);
            Assert.Equal(EquipmentCatalog.Extinguisher.CooldownSeconds, lv3.CooldownSeconds, 3);
        }

        // --- TC-6 ---
        [Fact]
        public void Hose_AtLevel4_ReachesFurther()
        {
            EquipmentDef lv4 = EquipmentCatalog.Hose.AtLevel(4);
            Assert.Equal(EquipmentCatalog.Hose.Range + 3, lv4.Range);
            Assert.Equal(EquipmentCatalog.Hose.CooldownSeconds, lv4.CooldownSeconds, 3);
        }

        // --- TC-7 ---
        [Fact]
        public void Level1AndLevel0_AreTheBaseDefinition()
        {
            Assert.Same(EquipmentCatalog.Hose, EquipmentCatalog.Hose.AtLevel(1));
            Assert.Same(EquipmentCatalog.Hose, EquipmentCatalog.Hose.AtLevel(0));
        }

        // --- TC-8 ---
        [Fact]
        public void Suit_CutsFireDamage_AndChangesItsName()
        {
            float[] expected = { 1f, 0.8f, 0.65f, 0.5f, 0.35f };
            for (int level = 0; level < expected.Length; level++)
            {
                Assert.Equal(expected[level], GearStats.DamageMultiplier(level), 3);
            }
            Assert.Equal("근무복", GearStats.SuitName(0));
            Assert.Equal("방열복", GearStats.SuitName(4));
        }

        // --- TC-9 ---
        [Fact]
        public void Boots_AddTenPercentSpeedPerLevel()
        {
            Assert.Equal(1f, GearStats.SpeedMultiplier(0), 3);
            Assert.Equal(1.4f, GearStats.SpeedMultiplier(4), 3);
        }

        // --- TC-10 ---
        [Fact]
        public void GearStats_ClampNegativeLevels_ButNotHighOnes()
        {
            // 음수만 자른다. 위로는 상한이 없고, 대신 효과가 수렴한다.
            Assert.Equal(1f, GearStats.DamageMultiplier(-1), 3);
            Assert.Equal(1f, GearStats.SpeedMultiplier(-5), 3);

            Assert.True(GearStats.DamageMultiplier(99) < GearStats.DamageMultiplier(10));
            Assert.Equal(GearStats.MinSuitDamage, GearStats.DamageMultiplier(99), 3);
            Assert.True(GearStats.SpeedMultiplier(99) > GearStats.SpeedMultiplier(10));

            Assert.Equal("방열복 +95", GearStats.SuitName(99));

            // 옷 그림은 다섯 장뿐이라 그림 번호만 마지막으로 자른다.
            Assert.Equal(4, GearStats.SuitLook(99));
        }
    }
}
