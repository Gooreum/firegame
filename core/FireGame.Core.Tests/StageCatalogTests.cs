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

        // 필요 장비로 이기는지는 RequiredGearTests가 현장마다 확인한다.

        [Fact]
        public void GatedCalls_CannotBeBeatenWithEverythingAtLevelOne()
        {
            // 해금만 하고 레벨업을 안 하면 못 끄는 신고가 있어야 돈을 모아 장비를 올릴 이유가 생긴다.
            foreach (StageDef stage in new[] { StageCatalog.Shopping, StageCatalog.GasStation, StageCatalog.Factory, StageCatalog.Harbor })
            {
                Assert.True(PlayWith(stage, AllFour) != StageOutcome.Won, stage.Name + " 이 장비 4종 Lv1로 깨졌다");
            }
        }

        [Fact]
        public void Harbor_CannotBeBeatenWithoutFoam()
        {
            Assert.NotEqual(StageOutcome.Won,
                PlayWith(StageCatalog.Harbor,
                    EquipmentId.Bucket, EquipmentId.Extinguisher, EquipmentId.Hose));
        }

        [Fact]
        public void Harbor_MaxedGearEarnsMoreThanTheBareMinimum()
        {
            SaveData basic = RequiredGearTests.SaveWithRequiredGear(StageCatalog.Harbor.Id);

            SaveData upgraded = SaveData.NewGame();
            foreach (int id in AllFour) upgraded.SetLevel(id, 5);
            upgraded.SetLevel(GearId.Suit, 4);
            upgraded.SetLevel(GearId.Boots, 4);

            StageResult before = PlayLoadout(StageCatalog.Harbor, Loadout.From(basic));
            StageResult after = PlayLoadout(StageCatalog.Harbor, Loadout.From(upgraded));

            Assert.True(before.Won);
            Assert.True(after.Won);
            Assert.True(Economy.Payout(after) > Economy.Payout(before),
                "최대 장비 " + Economy.Payout(after) + " vs 필요 장비만 " + Economy.Payout(before));
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
            int replays = 0;

            for (int index = 0; index < Campaign.Missions.Length; index++)
            {
                MissionDef mission = Campaign.Missions[index];

                // 필요 장비를 살 돈이 모자라면 직전 현장을 한 번 더 뛰어 번다.
                while (!BuyRequirements(save, mission))
                {
                    Assert.True(index > 0, "첫 현장부터 장비를 살 돈이 모자라다");
                    Assert.True(PlayAndSettle(Campaign.Missions[index - 1].Stage, save).Won);
                    replays++;
                    Assert.True(replays <= 2, "반복이 너무 많다. " + mission.Title + " 앞에서 보유 " + save.Money);
                }

                Assert.True(save.IsMissionUnlocked(mission), mission.Title + " 이 열려 있어야 한다");
                StageResult result = PlayAndSettle(mission.Stage, save);
                Assert.True(result.Won, mission.Title + " 을 깰 수 있어야 한다");
            }

            foreach (StageDef stage in StageCatalog.All) Assert.True(save.StarsFor(stage.Id) >= 1);
            System.Console.WriteLine("반복 " + replays + "회, 끝난 뒤 보유 " + save.Money);
        }

        /// <summary>필요 장비를 전부 산다. 돈이 모자라면 아무것도 사지 않고 false.</summary>
        private static bool BuyRequirements(SaveData save, MissionDef mission)
        {
            if (save.Money < Readiness.CostToReady(save, mission)) return false;

            foreach (Requirement requirement in mission.Requirements)
            {
                while (save.LevelOf(requirement.TrackId) < requirement.Level)
                {
                    Assert.Equal(PurchaseResult.Success, Shop.Upgrade(save, requirement.TrackId));
                }
            }
            return true;
        }

        /// <summary>세이브의 레벨대로 한 판 돌리고 정산까지 반영한다.</summary>
        private static StageResult PlayAndSettle(StageDef stage, SaveData save)
        {
            var runner = new StageRunner(stage, Loadout.From(save));
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
