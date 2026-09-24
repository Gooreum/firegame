using FireGame.Core.Data;
using FireGame.Core.Game;
using Xunit;

namespace FireGame.Core.Tests
{
    /// <summary>
    /// 레벨에 천장이 없다는 약속. 대신 게임이 깨질 수 있는 효과에만 천장이 있고,
    /// Lv1~4는 예전과 한 치도 다르지 않아야 현장별 필요 장비 표가 그대로 맞는다.
    /// </summary>
    public class NoLevelCeilingTests
    {
        // --- TC-2 ---
        [Fact]
        public void CostsKeepRising_AndStayPositive()
        {
            foreach (UpgradeTrack track in UpgradeCatalog.All)
            {
                // 해금값(Lv1)은 첫 레벨업보다 비싸게 잡혀 있으므로 단조 검사는 그 다음부터.
                Assert.True(track.CostToReach(track.StartLevel + 1) > 0, track.Name + " 해금값이 0 이하다");

                int previous = 0;
                for (int level = track.StartLevel + 2; level <= 200; level++)
                {
                    int cost = track.CostToReach(level);
                    Assert.True(cost > 0, track.Name + " Lv" + level + " 값이 0 이하다");
                    Assert.True(cost <= UpgradeTrack.MaxCost, track.Name + " Lv" + level + " 값이 상한을 넘었다");
                    Assert.True(cost >= previous, track.Name + " Lv" + level + " 값이 내려갔다");
                    previous = cost;
                }
            }
        }

        // --- TC-3 ---
        [Fact]
        public void BootsSpeed_IsUnchangedThroughLevelTen()
        {
            for (int level = 0; level <= GearStats.BootsLinearLevels; level++)
            {
                Assert.Equal(1f + (0.1f * level), GearStats.SpeedMultiplier(level), 3);
            }
        }

        // --- TC-4 ---
        [Fact]
        public void BootsSpeed_KeepsRisingPastTen_ButConverges()
        {
            float lv10 = GearStats.SpeedMultiplier(10);
            float lv11 = GearStats.SpeedMultiplier(11);
            float lv20 = GearStats.SpeedMultiplier(20);
            float lv30 = GearStats.SpeedMultiplier(30);

            Assert.True(lv11 > lv10, "Lv11이 Lv10보다 빠르지 않다");
            Assert.True(lv20 > lv11, "Lv20이 Lv11보다 빠르지 않다");
            Assert.True(lv30 > lv20, "Lv30이 Lv20보다 빠르지 않다");

            // 한 프레임 이동이 한 칸에 가까워지면 얇은 벽을 스쳐 지나간다. 2.234배(초당 8.9칸)가 천장이다.
            Assert.True(GearStats.SpeedMultiplier(1000) < 2.234f, "속도가 천장을 넘었다");
        }

        // --- TC-5 ---
        [Fact]
        public void SuitDamage_KeepsFallingPastTen_ButNeverBelowTenPercent()
        {
            Assert.True(GearStats.DamageMultiplier(20) < GearStats.DamageMultiplier(10), "Lv20이 Lv10보다 안 단단하다");

            for (int level = 0; level <= 500; level++)
            {
                Assert.True(GearStats.DamageMultiplier(level) >= GearStats.MinSuitDamage, "Lv" + level + " 피해가 바닥 밑으로 갔다");
            }

            Assert.Equal("방열복 +16", GearStats.SuitName(20));
        }

        // --- TC-6 ---
        [Fact]
        public void ConeRange_StopsAtTheCap_ButPowerKeepsGrowing()
        {
            EquipmentDef lv30 = EquipmentCatalog.Bucket.AtLevel(30);
            EquipmentDef lv60 = EquipmentCatalog.Bucket.AtLevel(60);

            Assert.Equal(EquipmentDef.MaxConeRange, lv30.Range);
            Assert.Equal(EquipmentDef.MaxConeRange, lv60.Range);
            Assert.True(lv60.Agent.Power > lv30.Agent.Power, "위력까지 멈췄다");
        }

        // --- TC-7 ---
        [Fact]
        public void LevelsOneToFour_AreExactlyAsBefore()
        {
            foreach (EquipmentDef def in EquipmentCatalog.All)
            {
                for (int level = 1; level <= 4; level++)
                {
                    EquipmentDef at = def.AtLevel(level);
                    int steps = level - 1;

                    Assert.Equal(def.Agent.Power * (1f + (0.2f * steps)), at.Agent.Power, 3);

                    int expectedRange = def.Pattern == AimPattern.Line ? def.Range + steps : def.Range;
                    Assert.Equal(expectedRange, at.Range);
                    Assert.Equal(0, at.EndSpread);

                    if (def.MaxCharges > 0) Assert.Equal((int)System.Math.Round(def.MaxCharges * (1f + (0.25f * steps))), at.MaxCharges);
                }
            }
        }

        // --- TC-8 ---
        [Fact]
        public void SetLevel_ClampsNegativesOnly_AndSurvivesSaveLoad()
        {
            SaveData save = SaveData.NewGame();
            save.SetLevel(EquipmentId.Hose, -5);
            Assert.Equal(0, save.LevelOf(EquipmentId.Hose));

            save.SetLevel(EquipmentId.Hose, 500);
            save.SetLevel(GearId.Suit, 42);
            Assert.Equal(500, save.LevelOf(EquipmentId.Hose));

            Assert.True(SaveData.TryDeserialize(save.Serialize(), out SaveData restored));
            Assert.Equal(500, restored.LevelOf(EquipmentId.Hose));
            Assert.Equal(42, restored.LevelOf(GearId.Suit));
        }
    }
}
