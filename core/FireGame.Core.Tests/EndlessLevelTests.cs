using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using Xunit;

namespace FireGame.Core.Tests
{
    /// <summary>끝없는 레벨업: 가격이 계속 오르고, 5레벨마다 뿌리는 모양이 커지며, Lv1~4는 예전과 똑같다.</summary>
    public class EndlessLevelTests
    {
        // --- TC-1 ---
        [Fact]
        public void Costs_UpToTheTableAreUnchanged()
        {
            Assert.Equal(new[] { 300, 200, 400, 600, 900 }, CostsOf(UpgradeCatalog.Extinguisher, 1, 5));
            Assert.Equal(new[] { 800, 300, 500, 800, 1200 }, CostsOf(UpgradeCatalog.Hose, 1, 5));
            Assert.Equal(new[] { 1500, 400, 700, 1000, 1500 }, CostsOf(UpgradeCatalog.FoamExtinguisher, 1, 5));
            Assert.Equal(new[] { 150, 300, 500, 800 }, CostsOf(UpgradeCatalog.Bucket, 2, 5));
            Assert.Equal(new[] { 200, 400, 700, 1100 }, CostsOf(UpgradeCatalog.Suit, 1, 4));
            Assert.Equal(new[] { 150, 300, 500, 800 }, CostsOf(UpgradeCatalog.Boots, 1, 4));
        }

        // --- TC-2 ---
        [Fact]
        public void Costs_AfterTheTable_GrowBy30Percent_RoundedToFifty()
        {
            Assert.Equal(1950, UpgradeCatalog.FoamExtinguisher.CostToReach(6));
            Assert.Equal(2550, UpgradeCatalog.FoamExtinguisher.CostToReach(7));
            Assert.Equal(1050, UpgradeCatalog.Bucket.CostToReach(6));
            Assert.Equal(1450, UpgradeCatalog.Suit.CostToReach(5));

            foreach (UpgradeTrack track in UpgradeCatalog.All)
            {
                int previous = 0;
                for (int level = 6; level <= 200; level++)
                {
                    int cost = track.CostToReach(level);
                    Assert.True(cost > previous || cost == UpgradeTrack.MaxCost, track.Name + " Lv" + level + " 값이 오르지 않았다");
                    Assert.True(cost > 0, track.Name + " Lv" + level + " 값이 넘쳤다");
                    Assert.Equal(0, cost % 50);
                    previous = cost;
                }
            }
        }

        // --- TC-3 ---
        [Fact]
        public void Levels_HaveNoCeiling_ForEveryTrack()
        {
            // 여섯 트랙 전부 Lv200까지 계속 살 수 있어야 한다. 돈 쓸 곳이 끝나면 벌 이유도 끝난다.
            foreach (UpgradeTrack track in UpgradeCatalog.All)
            {
                SaveData save = SaveData.NewGame();

                for (int level = save.LevelOf(track.Id); level < 200; level++)
                {
                    // 후반 값은 가격 상한($99,999,950)에 붙으므로 지갑을 매번 채워 준다.
                    save.Money = int.MaxValue;
                    Assert.Equal(PurchaseResult.Success, Shop.Upgrade(save, track.Id));
                }

                Assert.Equal(200, save.LevelOf(track.Id));
                Assert.True(Shop.NextCost(save, track.Id) > 0, track.Name + " Lv201을 살 수 없다");
            }

            // 상한이 없으니 SetLevel도 자르지 않는다.
            SaveData raw = SaveData.NewGame();
            raw.SetLevel(EquipmentId.Hose, 500);
            Assert.Equal(500, raw.LevelOf(EquipmentId.Hose));
        }

        // --- TC-4 ---
        [Fact]
        public void Foam_CanBeLevelledAllTheWayToTwenty_ThroughTheShop()
        {
            SaveData save = SaveData.NewGame();
            save.Money = 10000000;
            for (int level = 1; level <= 20; level++)
            {
                Assert.Equal(PurchaseResult.Success, Shop.Upgrade(save, EquipmentId.FoamExtinguisher));
            }
            Assert.Equal(20, save.LevelOf(EquipmentId.FoamExtinguisher));
        }

        // --- TC-5 ---
        [Fact]
        public void LevelsOneToFour_BehaveExactlyAsBefore()
        {
            // 예전 공식: 위력 +20%, 호스 사거리 +1, 양동이 쿨다운 −10%, 횟수 +25%. 특성 없음.
            foreach (EquipmentDef def in EquipmentCatalog.All)
            {
                for (int level = 1; level <= 4; level++)
                {
                    int steps = level - 1;
                    EquipmentDef at = def.AtLevel(level);
                    Assert.Equal(def.Agent.Power * (1f + (0.2f * steps)), at.Agent.Power, 4);
                    Assert.Equal(def.Pattern == AimPattern.Line ? def.Range + steps : def.Range, at.Range);
                    float cooldown = def.Resource == ResourceKind.Cooldown && def.Pattern != AimPattern.Line
                        ? def.CooldownSeconds * (1f - (0.1f * steps))
                        : def.CooldownSeconds;
                    Assert.Equal(cooldown, at.CooldownSeconds, 4);
                    Assert.Equal(def.MaxCharges == 0 ? 0 : (int)System.Math.Round(def.MaxCharges * (1f + (0.25f * steps))), at.MaxCharges);
                    Assert.Equal(0, at.EndSpread);
                }
            }
        }

        private static int CellsHit(EquipmentDef def, int level)
        {
            var grid = new FireGrid(40, 22);
            var hits = new List<GridPoint>();
            EquipmentDef at = def.AtLevel(level);
            Aiming.Resolve(grid, 10, 10, AimDirection.E, at.Pattern, at.Range, hits, at.EndSpread);
            return hits.Count;
        }

        // --- TC-6 ---
        [Fact]
        public void Milestones_WidenTheSprayAtFiveAndTen()
        {
            Assert.Equal(3, CellsHit(EquipmentCatalog.Bucket, 4));
            Assert.Equal(6, CellsHit(EquipmentCatalog.Bucket, 5));
            Assert.Equal(9, CellsHit(EquipmentCatalog.Bucket, 10));

            Assert.Equal(6, CellsHit(EquipmentCatalog.Extinguisher, 4));
            Assert.Equal(9, CellsHit(EquipmentCatalog.Extinguisher, 5));
            Assert.Equal(12, CellsHit(EquipmentCatalog.FoamExtinguisher, 10));
        }

        // --- TC-7 ---
        [Fact]
        public void Hose_Lv5_HitsATShapedEnd()
        {
            var grid = new FireGrid(40, 22);
            var hits = new List<GridPoint>();

            EquipmentDef lv4 = EquipmentCatalog.Hose.AtLevel(4);
            Aiming.Resolve(grid, 10, 10, AimDirection.E, lv4.Pattern, lv4.Range, hits, lv4.EndSpread);
            Assert.Equal(8, hits.Count);
            Assert.DoesNotContain(new GridPoint(18, 9), hits);

            EquipmentDef lv5 = EquipmentCatalog.Hose.AtLevel(5);
            Aiming.Resolve(grid, 10, 10, AimDirection.E, lv5.Pattern, lv5.Range, hits, lv5.EndSpread);
            Assert.Equal(11, hits.Count);
            Assert.Contains(new GridPoint(19, 9), hits);
            Assert.Contains(new GridPoint(19, 11), hits);
        }

        // --- TC-8 ---
        [Fact]
        public void Caps_HoseRangeTwelve_BucketCooldownFloor_HoseFastAtTen()
        {
            Assert.Equal(EquipmentDef.MaxLineRange, EquipmentCatalog.Hose.AtLevel(20).Range);
            Assert.Equal(EquipmentDef.MinCooldownSeconds, EquipmentCatalog.Bucket.AtLevel(20).CooldownSeconds, 4);
            Assert.Equal(EquipmentDef.FastLineCooldown, EquipmentCatalog.Hose.AtLevel(10).CooldownSeconds, 4);
            Assert.Equal(EquipmentCatalog.Hose.CooldownSeconds, EquipmentCatalog.Hose.AtLevel(9).CooldownSeconds, 4);
            Assert.True(EquipmentCatalog.FoamExtinguisher.AtLevel(20).Agent.Power > EquipmentCatalog.FoamExtinguisher.AtLevel(19).Agent.Power);
        }

        // --- TC-9 ---
        [Fact]
        public void Suit_KeepsCuttingDamage_ButNeverBelowTenPercent_AndBootsTopOutAtTen()
        {
            float previous = GearStats.DamageMultiplier(4);
            Assert.Equal(0.35f, previous, 3);
            for (int level = 5; level <= 10; level++)
            {
                float now = GearStats.DamageMultiplier(level);
                Assert.True(now < previous);
                Assert.True(now >= GearStats.MinSuitDamage);
                previous = now;
            }

            Assert.Equal(2.0f, GearStats.SpeedMultiplier(10), 3);
            Assert.Equal("방열복 +1", GearStats.SuitName(5));
        }

        private static int[] CostsOf(UpgradeTrack track, int from, int to)
        {
            var costs = new int[to - from + 1];
            for (int level = from; level <= to; level++) costs[level - from] = track.CostToReach(level);
            return costs;
        }
    }
}
