using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using Xunit;

namespace FireGame.Core.Tests
{
    public class StageCatalogTests
    {
        private static StageOutcome PlayWith(StageDef stage, params int[] loadout)
        {
            var runner = new StageRunner(stage, new List<int>(loadout));
            return new GreedyBot(runner).Play();
        }

        // --- TC-1 ---
        [Fact]
        public void EveryMap_MatchesTheScreenGrid()
        {
            foreach (StageDef stage in StageCatalog.All)
            {
                Assert.Equal(22, stage.Map.Length);

                foreach (string row in stage.Map)
                {
                    Assert.Equal(40, row.Length);
                }
            }
        }

        // --- TC-2 ---
        [Fact]
        public void EveryMap_ParsesWithoutUnknownCharacters()
        {
            foreach (StageDef stage in StageCatalog.All)
            {
                ParsedMap map = MapLoader.Parse(stage.Map);
                Assert.Equal(40, map.Grid.Width);
                Assert.Equal(22, map.Grid.Height);
            }
        }

        // --- TC-3 ---
        [Fact]
        public void EveryCivilianExitAndHydrant_IsReachableFromTheSpawn()
        {
            // 갇힌 시민은 스테이지를 클리어 불가능하게 만드는데, 맵을 눈으로 봐서는
            // 문 하나 빠진 것을 놓치기 쉽다. 실제로 개발 중 세 번 발생했다.
            foreach (StageDef stage in StageCatalog.All)
            {
                ParsedMap map = MapLoader.Parse(stage.Map);
                bool[] reachable = FloodFill(map.Grid, map.PlayerSpawn);

                foreach (GridPoint civilian in map.Civilians)
                {
                    Assert.True(reachable[map.Grid.Index(civilian.X, civilian.Y)],
                        stage.Name + ": 시민 " + civilian + " 에 도달할 수 없다");
                }

                foreach (GridPoint exit in map.Exits)
                {
                    Assert.True(reachable[map.Grid.Index(exit.X, exit.Y)],
                        stage.Name + ": 출구 " + exit + " 에 도달할 수 없다");
                }

                // 급수전은 통행 불가 셀이므로, 옆에 설 자리가 있는지를 본다.
                foreach (GridPoint hydrant in map.Hydrants)
                {
                    Assert.True(HasReachableNeighbor(map.Grid, reachable, hydrant),
                        stage.Name + ": 급수전 " + hydrant + " 옆에 설 자리가 없다");
                }
            }
        }

        private static bool[] FloodFill(FireGrid grid, GridPoint start)
        {
            var reachable = new bool[grid.Count];
            var queue = new Queue<GridPoint>();

            reachable[grid.Index(start.X, start.Y)] = true;
            queue.Enqueue(start);

            int[] dx = { 0, 0, -1, 1 };
            int[] dy = { -1, 1, 0, 0 };

            while (queue.Count > 0)
            {
                GridPoint current = queue.Dequeue();

                for (int i = 0; i < 4; i++)
                {
                    int nx = current.X + dx[i];
                    int ny = current.Y + dy[i];
                    if (!grid.InBounds(nx, ny)) continue;

                    int index = grid.Index(nx, ny);
                    if (reachable[index]) continue;
                    if (!Materials.Of(grid.Cells[index].Material).Walkable) continue;

                    reachable[index] = true;
                    queue.Enqueue(new GridPoint(nx, ny));
                }
            }

            return reachable;
        }

        private static bool HasReachableNeighbor(FireGrid grid, bool[] reachable, GridPoint point)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int nx = point.X + dx;
                    int ny = point.Y + dy;
                    if (!grid.InBounds(nx, ny)) continue;
                    if (reachable[grid.Index(nx, ny)]) return true;
                }
            }

            return false;
        }

        // --- TC-4 ---
        [Fact]
        public void Residential_IsBeatableWithNothingButTheFreeBucket()
        {
            Assert.Equal(
                StageOutcome.Won,
                PlayWith(StageCatalog.Residential, EquipmentId.Bucket));
        }

        // --- TC-5 ---
        [Fact]
        public void ShoppingMall_CannotBeBeatenWithWaterAlone()
        {
            // 전기 화재는 물이 역효과이고 스스로 꺼지지도 않는다.
            StageOutcome outcome = PlayWith(StageCatalog.Shopping, EquipmentId.Bucket);

            Assert.NotEqual(StageOutcome.Won, outcome);
        }

        // --- TC-6 ---
        [Fact]
        public void ShoppingMall_IsBeatenOnceYouOwnTheCo2Extinguisher()
        {
            Assert.Equal(
                StageOutcome.Won,
                PlayWith(StageCatalog.Shopping, EquipmentId.Bucket, EquipmentId.Extinguisher));
        }

        // --- TC-7 ---
        [Fact]
        public void GasStation_CannotBeBeatenWithoutFoam()
        {
            Assert.NotEqual(StageOutcome.Won,
                PlayWith(StageCatalog.GasStation, EquipmentId.Bucket));

            Assert.NotEqual(StageOutcome.Won,
                PlayWith(StageCatalog.GasStation, EquipmentId.Bucket, EquipmentId.Extinguisher));

            Assert.NotEqual(StageOutcome.Won,
                PlayWith(StageCatalog.GasStation,
                    EquipmentId.Bucket, EquipmentId.Extinguisher, EquipmentId.Hose));
        }

        // --- TC-8 ---
        [Fact]
        public void GasStation_IsBeatenOnceYouOwnFoam()
        {
            Assert.Equal(
                StageOutcome.Won,
                PlayWith(StageCatalog.GasStation,
                    EquipmentId.Bucket, EquipmentId.Extinguisher, EquipmentId.FoamExtinguisher));
        }

        // --- 창고 ---
        [Fact]
        public void Warehouse_IsBeatableWithTheStartingBucket_AndAllThreeAreRescued()
        {
            var runner = new StageRunner(StageCatalog.Warehouse, new List<int> { EquipmentId.Bucket });
            Assert.Equal(StageOutcome.Won, new GreedyBot(runner).Play());
            Assert.Equal(3, runner.BuildResult().Rescued);
        }

        [Fact]
        public void Warehouse_OpensOnlyAfterTheGasStation()
        {
            MissionDef warehouse = Campaign.ById(StageCatalog.Warehouse.Id);
            SaveData save = SaveData.NewGame();
            save.RecordResult(0, 3);
            save.RecordResult(1, 3);
            Assert.False(save.IsMissionUnlocked(warehouse));

            save.RecordResult(2, 1);
            Assert.True(save.IsMissionUnlocked(warehouse));
        }

        // --- 공장 ---
        [Fact]
        public void Factory_IsBeatenOnceYouHaveBothCo2AndFoam()
        {
            Assert.Equal(StageOutcome.Won,
                PlayWith(StageCatalog.Factory,
                    EquipmentId.Bucket, EquipmentId.Extinguisher, EquipmentId.FoamExtinguisher));
        }

        [Fact]
        public void Factory_CannotBeBeatenWithoutFoam()
        {
            // 기름은 CO2로 조금 누그러질 뿐 꺼지지 않는다.
            Assert.NotEqual(StageOutcome.Won,
                PlayWith(StageCatalog.Factory,
                    EquipmentId.Bucket, EquipmentId.Extinguisher, EquipmentId.Hose));
        }

        [Fact]
        public void Factory_CannotBeBeatenWithoutCo2()
        {
            // 배전반은 폼이 듣지 않고 물은 역효과다.
            Assert.NotEqual(StageOutcome.Won,
                PlayWith(StageCatalog.Factory,
                    EquipmentId.Bucket, EquipmentId.Hose, EquipmentId.FoamExtinguisher));
        }

        // --- 항구 ---
        private static readonly int[] AllFour =
        {
            EquipmentId.Bucket, EquipmentId.Extinguisher, EquipmentId.Hose, EquipmentId.FoamExtinguisher,
        };

        [Fact]
        public void Harbor_IsBeatenWithAllFourAtLevelOne()
        {
            // 레벨업 없이도 깰 수 있어야 한다. 같은 현장을 반복해 돈을 모으게 하지 않는다.
            Assert.Equal(StageOutcome.Won, PlayWith(StageCatalog.Harbor, AllFour));
        }

        [Fact]
        public void Harbor_CannotBeBeatenWithoutFoam()
        {
            Assert.NotEqual(StageOutcome.Won,
                PlayWith(StageCatalog.Harbor,
                    EquipmentId.Bucket, EquipmentId.Extinguisher, EquipmentId.Hose));
        }

        [Fact]
        public void Harbor_UpgradedGearEarnsMoreThanLevelOne()
        {
            SaveData basic = SaveData.NewGame();
            foreach (int id in AllFour) basic.SetLevel(id, 1);

            SaveData upgraded = SaveData.NewGame();
            foreach (int id in AllFour) upgraded.SetLevel(id, 3);
            upgraded.SetLevel(GearId.Suit, 2);
            upgraded.SetLevel(GearId.Boots, 2);

            StageResult before = PlayLoadout(StageCatalog.Harbor, Loadout.From(basic));
            StageResult after = PlayLoadout(StageCatalog.Harbor, Loadout.From(upgraded));

            Assert.True(before.Won);
            Assert.True(after.Won);
            Assert.True(Economy.Payout(after) > Economy.Payout(before),
                "레벨업 " + Economy.Payout(after) + " vs Lv1 " + Economy.Payout(before));
        }

        private static StageResult PlayLoadout(StageDef stage, Loadout loadout)
        {
            var runner = new StageRunner(stage, loadout);
            new GreedyBot(runner).Play();
            return runner.BuildResult();
        }

        // --- TC-9 ---
        [Fact]
        public void StageUnlockRequirements_FormAStraightProgression()
        {
            for (int i = 0; i < StageCatalog.All.Length; i++)
            {
                Assert.Equal(i, StageCatalog.All[i].Id);
                Assert.Same(StageCatalog.All[i], StageCatalog.ById(i));
            }
            Assert.Null(StageCatalog.ById(StageCatalog.All.Length));
        }

        // --- TC-10 ---
        [Fact]
        public void AFreshSave_CanEarnItsWayThroughAllSixStages()
        {
            SaveData save = SaveData.NewGame();

            // 현장마다 들어가기 전에 새로 필요한 장비를 산다. 같은 현장을 반복하지 않는다.
            (StageDef Stage, int Buy)[] route =
            {
                (StageCatalog.Residential, -1),
                (StageCatalog.Shopping, EquipmentId.Extinguisher),
                (StageCatalog.GasStation, EquipmentId.FoamExtinguisher),
                (StageCatalog.Warehouse, EquipmentId.Hose),
                (StageCatalog.Factory, -1),
                (StageCatalog.Harbor, -1),
            };

            foreach ((StageDef stage, int buy) in route)
            {
                if (buy >= 0)
                {
                    Assert.True(save.Money >= Shop.NextCost(save, buy),
                        stage.Name + " 전에 장비 " + buy + "를 살 돈이 있어야 한다. 보유 " + save.Money);
                    Assert.Equal(PurchaseResult.Success, Shop.Upgrade(save, buy));
                }

                Assert.True(save.IsMissionUnlocked(Campaign.ById(stage.Id)), stage.Name + " 이 열려 있어야 한다");
                StageResult result = PlayAndSettle(stage, save);
                Assert.True(result.Won, stage.Name + " 을 깰 수 있어야 한다");
            }

            foreach (StageDef stage in StageCatalog.All) Assert.True(save.StarsFor(stage.Id) >= 1);
        }

        /// <summary>보유 장비로 한 판 돌리고 정산까지 반영한다.</summary>
        private static StageResult PlayAndSettle(StageDef stage, SaveData save)
        {
            var runner = new StageRunner(stage, save.OwnedEquipment());
            new GreedyBot(runner).Play();

            StageResult result = runner.BuildResult();
            save.Money += Economy.Payout(result);
            save.RecordResult(stage.Id, StarRating.For(result));

            return result;
        }

        // --- TC-11 ---
        [Fact]
        public void EveryStage_StartsWithAtLeastOneFire()
        {
            foreach (StageDef stage in StageCatalog.All)
            {
                ParsedMap map = MapLoader.Parse(stage.Map);
                Assert.True(map.IgnitionPoints.Count >= 1,
                    stage.Name + ": 발화점이 없다");
                Assert.True(map.Grid.CountBurning() >= 1);
            }
        }

        // --- TC-12 ---
        [Fact]
        public void OnlyClassAFires_BurnThemselvesOutWithinAStage()
        {
            // 이 규칙이 장비 진행의 근거다. 유류·전기가 알아서 꺼지면
            // 맞는 약제를 살 이유가 사라진다.
            float longestStage = 0f;
            foreach (StageDef stage in StageCatalog.All)
            {
                if (stage.TimeLimitSeconds > longestStage) longestStage = stage.TimeLimitSeconds;
            }

            float WoodLifetime(MaterialId id)
            {
                return 1f / Materials.Of(id).BurnRate;
            }

            Assert.True(WoodLifetime(MaterialId.Wood) < longestStage,
                "목재는 한 판 안에 스스로 소진돼야 한다");

            Assert.True(WoodLifetime(MaterialId.Oil) > longestStage,
                "유류는 한 판 안에 스스로 꺼지면 안 된다");

            Assert.True(WoodLifetime(MaterialId.Electric) > longestStage,
                "전기는 한 판 안에 스스로 꺼지면 안 된다");
        }

        // --- TC-13 ---
        [Fact]
        public void WrongEquipment_BurnsThroughShotsWithoutPuttingTheFireOut()
        {
            var runner = new StageRunner(StageCatalog.Shopping, new List<int> { EquipmentId.Bucket });
            var bot = new GreedyBot(runner);

            bot.Play();

            Assert.True(bot.ShotsFired > 200,
                "양동이로는 계속 시도하게 된다. 실제 " + bot.ShotsFired + "발");
            Assert.True(runner.Grid.CountBurning() > 0,
                "그래도 전기 화재는 남아 있어야 한다");
        }
    }
}
