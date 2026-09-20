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
            original.RecordClear(2);
            Shop.Buy(original, EquipmentId.Extinguisher);
            Shop.Buy(original, EquipmentId.Hose);

            Assert.True(SaveData.TryDeserialize(original.Serialize(), out SaveData restored));

            Assert.Equal(original.Money, restored.Money);
            Assert.Equal(original.ClearedStages, restored.ClearedStages);
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
            Assert.Equal(0, save.ClearedStages);
            Assert.Single(save.Unlocked);
            Assert.True(save.Owns(EquipmentId.Bucket));
            Assert.False(save.Owns(EquipmentId.Hose));
        }

        // --- TC-15 ---
        [Fact]
        public void RecordingAnEarlierStageClear_DoesNotRollBackProgress()
        {
            SaveData save = SaveData.NewGame();

            save.RecordClear(0);
            Assert.Equal(1, save.ClearedStages);

            save.RecordClear(2);
            Assert.Equal(3, save.ClearedStages);

            // 1번 스테이지를 다시 깨도 진행도가 뒤로 가면 안 된다.
            save.RecordClear(1);
            Assert.Equal(3, save.ClearedStages);
        }

        // --- TC-16 ---
        [Fact]
        public void StageUnlocking_RespectsRequiredClears()
        {
            var gated = new StageDef(2, "GAS", new[] { "##", "##" }, Wind.None, 120f, 1200, requiredClears: 2);
            SaveData save = SaveData.NewGame();

            Assert.False(save.IsStageUnlocked(gated));

            save.RecordClear(0);
            Assert.False(save.IsStageUnlocked(gated));

            save.RecordClear(1);
            Assert.True(save.IsStageUnlocked(gated));
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
