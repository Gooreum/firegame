using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Sim;
using Xunit;

namespace FireGame.Core.Tests
{
    public class EconomyAndSaveTests
    {
        private static StageResult WinResult(
            int rescued = 2,
            float intact = 0.8f,
            float timeLeft = 20f,
            int wetCells = 10,
            int basePayout = 300)
        {
            return new StageResult
            {
                StageId = 0,
                BasePayout = basePayout,
                Won = true,
                Rescued = rescued,
                CiviliansTotal = rescued,
                IntactRatio = intact,
                TimeLeft = timeLeft,
                WetCellCount = wetCells,
            };
        }

        // --- TC-1 ---
        [Fact]
        public void Payout_SumsEveryComponentExactly()
        {
            StageResult result = WinResult();

            int expected = 300                    // 기본
                           + (2 * Economy.RescueBonus)
                           + (int)(0.8f * Economy.IntactBonusMax)
                           + (int)(20f * Economy.TimeBonusPerSecond)
                           - (10 * Economy.WaterDamagePerCell);

            Assert.Equal(expected, Economy.Payout(result));
        }

        // --- TC-2 ---
        [Fact]
        public void Payout_IsZero_WhenTheStageWasLost()
        {
            StageResult result = WinResult();
            result.Won = false;

            Assert.Equal(0, Economy.Payout(result));
        }

        // --- TC-3 ---
        [Fact]
        public void WaterDamage_ActuallyReducesThePayout()
        {
            int careful = Economy.Payout(WinResult(wetCells: 10));
            int wasteful = Economy.Payout(WinResult(wetCells: 40));

            Assert.True(wasteful < careful,
                "물을 많이 쓸수록 보상이 줄어야 한다. 정밀 " + careful + " vs 낭비 " + wasteful);
            Assert.Equal((40 - 10) * Economy.WaterDamagePerCell, careful - wasteful);
        }

        // --- TC-4 ---
        [Fact]
        public void Payout_NeverGoesNegative()
        {
            StageResult drenched = WinResult(
                rescued: 0, intact: 0.25f, timeLeft: 0f, wetCells: 5000, basePayout: 100);

            Assert.Equal(0, Economy.Payout(drenched));
        }

        // --- TC-5 ---
        [Fact]
        public void RescuingMoreCivilians_PaysMore()
        {
            int none = Economy.Payout(WinResult(rescued: 0));
            int three = Economy.Payout(WinResult(rescued: 3));

            Assert.Equal(3 * Economy.RescueBonus, three - none);
        }

        // --- TC-6 ---
        [Fact]
        public void SavingMoreOfTheBuilding_PaysMore()
        {
            int wrecked = Economy.Payout(WinResult(intact: 0.3f));
            int pristine = Economy.Payout(WinResult(intact: 1.0f));

            Assert.True(pristine > wrecked);
        }

        // --- TC-7 ---
        [Fact]
        public void Breakdown_ComponentsAddUpToTheTotal()
        {
            PayoutBreakdown b = Economy.Breakdown(WinResult());

            Assert.Equal(b.Base + b.Rescue + b.Integrity + b.Time + b.WaterDamage, b.Total);
            Assert.True(b.WaterDamage < 0, "수손은 차감 항목이므로 음수여야 한다");
            Assert.Equal(Economy.Payout(WinResult()), b.Total);
        }

        // --- TC-8 ---
        [Fact]
        public void Buying_WithEnoughMoney_DeductsAndUnlocks()
        {
            SaveData save = SaveData.NewGame();
            save.Money = 1000;

            PurchaseResult result = Shop.Buy(save, EquipmentId.Extinguisher);

            Assert.Equal(PurchaseResult.Success, result);
            Assert.Equal(1000 - EquipmentCatalog.Extinguisher.Price, save.Money);
            Assert.True(save.Owns(EquipmentId.Extinguisher));
        }

        // --- TC-9 ---
        [Fact]
        public void Buying_WithoutEnoughMoney_ChangesNothing()
        {
            SaveData save = SaveData.NewGame();
            save.Money = 100;

            PurchaseResult result = Shop.Buy(save, EquipmentId.Hose);

            Assert.Equal(PurchaseResult.NotEnoughMoney, result);
            Assert.Equal(100, save.Money);
            Assert.False(save.Owns(EquipmentId.Hose));
        }

        // --- TC-10 ---
        [Fact]
        public void BuyingSomethingAlreadyOwned_DoesNotChargeAgain()
        {
            SaveData save = SaveData.NewGame();
            save.Money = 5000;

            PurchaseResult result = Shop.Buy(save, EquipmentId.Bucket);

            Assert.Equal(PurchaseResult.AlreadyOwned, result);
            Assert.Equal(5000, save.Money);
        }

        // --- TC-11 ---
        [Fact]
        public void BuyingAnUnknownEquipmentId_Fails()
        {
            SaveData save = SaveData.NewGame();
            save.Money = 99999;

            Assert.Equal(PurchaseResult.UnknownEquipment, Shop.Buy(save, 777));
            Assert.Equal(99999, save.Money);
        }

        // --- TC-12 ---
        [Fact]
        public void SaveData_SurvivesASerializeDeserializeRoundTrip()
        {
            SaveData original = SaveData.NewGame();
            original.Money = 7350;
            original.RecordResult(0, 3);
            original.RecordResult(1, 2);
            Shop.Buy(original, EquipmentId.Extinguisher);
            Shop.Buy(original, EquipmentId.Hose);

            Assert.True(SaveData.TryDeserialize(original.Serialize(), out SaveData restored));

            Assert.Equal(original.Money, restored.Money);
            Assert.Equal(3, restored.StarsFor(0));
            Assert.Equal(2, restored.StarsFor(1));
            Assert.Equal(0, restored.StarsFor(2));
            Assert.StartsWith("v2", original.Serialize());
            Assert.Equal(original.Unlocked.Count, restored.Unlocked.Count);

            foreach (int id in original.Unlocked)
            {
                Assert.True(restored.Owns(id), "장비 " + id + " 가 복원되지 않았다");
            }
        }

        // --- TC-13 ---
        [Fact]
        public void CorruptedSaveData_FailsWithoutThrowing()
        {
            Assert.False(SaveData.TryDeserialize(null, out _));
            Assert.False(SaveData.TryDeserialize("", out _));
            Assert.False(SaveData.TryDeserialize("garbage", out _));
            Assert.False(SaveData.TryDeserialize("v1\nmoney=abc\n", out _));
            Assert.False(SaveData.TryDeserialize("v1\nnokeyvalue\n", out _));
            Assert.False(SaveData.TryDeserialize("v9\nmoney=10\n", out _));

            // 저장 중 앱이 죽어 파일이 잘린 경우: money 줄이 없으면 실패로 본다.
            Assert.False(SaveData.TryDeserialize("v1\ncleared=1\n", out _));

            // 별점 항목이 깨져 있으면 실패
            Assert.False(SaveData.TryDeserialize("v2\nmoney=10\nstars=0:x\n", out _));
            Assert.False(SaveData.TryDeserialize("v2\nmoney=10\nstars=0-3\n", out _));

            // 모르는 키는 무시하고 살아남는다. 나중에 항목이 늘어도 구버전이 깨지지 않는다.
            Assert.True(SaveData.TryDeserialize("v1\nmoney=50\nfuturefield=x\n", out SaveData ok));
            Assert.Equal(50, ok.Money);
        }

        // --- TC-14 ---
        [Fact]
        public void NewGame_StartsWithOnlyTheBucketAndNoMoney()
        {
            SaveData save = SaveData.NewGame();

            Assert.Equal(0, save.Money);
            Assert.Equal(0, save.TotalStars);
            foreach (MissionDef m in Campaign.Missions) Assert.Equal(0, save.StarsFor(m.Id));
            Assert.Single(save.Unlocked);
            Assert.True(save.Owns(EquipmentId.Bucket));
            Assert.False(save.Owns(EquipmentId.Hose));
        }

        // --- TC-15 (v2: 최고 기록만 유지) ---
        [Fact]
        public void RecordingAWorseResult_KeepsTheBestStars()
        {
            SaveData save = SaveData.NewGame();

            save.RecordResult(0, 2);
            save.RecordResult(0, 1);
            Assert.Equal(2, save.StarsFor(0));

            save.RecordResult(0, 3);
            Assert.Equal(3, save.StarsFor(0));

            // 범위를 벗어난 값은 0..3으로 잘린다.
            save.RecordResult(1, 5);
            Assert.Equal(3, save.StarsFor(1));
            save.RecordResult(2, -1);
            Assert.Equal(0, save.StarsFor(2));
        }

        // --- TC-16 (v2: 캠페인 해금) ---
        [Fact]
        public void Missions_UnlockOnlyAfterThePreviousOneIsCleared()
        {
            SaveData save = SaveData.NewGame();
            MissionDef first = Campaign.Missions[0];
            MissionDef second = Campaign.Missions[1];
            MissionDef third = Campaign.Missions[2];

            Assert.True(save.IsMissionUnlocked(first));
            Assert.False(save.IsMissionUnlocked(second));

            // 패배(별 0)는 클리어가 아니다.
            save.RecordResult(first.Id, 0);
            Assert.False(save.IsMissionUnlocked(second));

            save.RecordResult(first.Id, 1);
            Assert.True(save.IsMissionUnlocked(second));
            Assert.False(save.IsMissionUnlocked(third));
            Assert.False(save.IsMissionUnlocked(null));
        }

        [Fact]
        public void LegacyV1Save_IsMigratedWithoutLosingProgress()
        {
            string v1 = "v1\nmoney=2500\ncleared=2\nunlocked=0,1\n";

            Assert.True(SaveData.TryDeserialize(v1, out SaveData save));

            Assert.Equal(2500, save.Money);
            Assert.True(save.Owns(EquipmentId.Extinguisher));
            Assert.Equal(1, save.StarsFor(0));
            Assert.Equal(1, save.StarsFor(1));
            Assert.Equal(0, save.StarsFor(2));
            Assert.True(save.IsMissionUnlocked(Campaign.Missions[2]));

            // 다시 저장하면 v2로 바뀐다.
            Assert.StartsWith("v2", save.Serialize());
        }

        [Fact]
        public void PreciseWorkOutEarnsMoreThanHosingEverythingDown()
        {
            // 같은 판을 "정밀하게" 끈 경우와 "물을 퍼부어" 끈 경우.
            int precise = Economy.Payout(WinResult(intact: 0.95f, timeLeft: 25f, wetCells: 12));
            int sloppy = Economy.Payout(WinResult(intact: 0.70f, timeLeft: 5f, wetCells: 120));

            Assert.True(precise > sloppy,
                "정밀함이 더 벌어야 한다. 정밀 " + precise + " vs 난사 " + sloppy);
        }
    }
}
