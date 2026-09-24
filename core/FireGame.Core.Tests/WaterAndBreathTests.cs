using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using Xunit;

namespace FireGame.Core.Tests
{
    /// <summary>
    /// 물탱크와 숨. 이 게임에서 <b>무한 자원과 무한 체력이 끝나는 지점</b>이다.
    /// </summary>
    public class WaterAndBreathTests
    {
        private static StageRunner Runner(params int[] equipment)
        {
            return new StageRunner(StageCatalog.Residential, equipment);
        }

        private static StageInput FireWith(int slot)
        {
            return new StageInput { Slot = slot, Fire = true };
        }

        // --- TC-1 ---
        [Fact]
        public void TankStartsFull()
        {
            StageRunner runner = Runner(EquipmentId.Bucket);

            Assert.Equal(GameConfig.WaterTankMax, runner.Player.Water);
            Assert.Equal(1f, runner.Player.WaterRatio, 4);
        }

        // --- TC-2 ---
        [Fact]
        public void EveryShot_DrainsTheTank()
        {
            StageRunner runner = Runner(EquipmentId.Bucket);
            EquipmentDef bucket = runner.SlotEquipment(0);

            float before = runner.Player.Water;
            runner.Update(0.05f, FireWith(0));

            Assert.Equal(before - bucket.WaterCost, runner.Player.Water, 3);
        }

        // --- TC-3 ---
        [Fact]
        public void EmptyTank_StopsWaterGear_ButNotCo2()
        {
            StageRunner runner = Runner(EquipmentId.Bucket, EquipmentId.Extinguisher);

            EquipmentDef bucket = runner.SlotEquipment(0);
            EquipmentDef co2 = runner.SlotEquipment(1);

            Assert.True(bucket.WaterCost > 0f, "양동이는 물을 써야 한다");
            Assert.Equal(0f, co2.WaterCost);

            runner.Player.Water = 0f;

            Assert.False(runner.Player.CanFire(0, bucket), "물 없이 양동이가 나갔다");
            Assert.True(runner.Player.CanFire(1, co2), "CO2는 물과 무관해야 한다");
        }

        // --- TC-4 ---
        [Fact]
        public void HoseRunsForAboutTwelveSeconds_OnOneTank()
        {
            StageRunner runner = Runner(EquipmentId.Hose);
            EquipmentDef hose = runner.SlotEquipment(0);

            // 물탱크 / (한 발 비용 / 쿨다운) = 연속 방수 초
            float secondsOfWater = GameConfig.WaterTankMax / (hose.WaterCost / hose.CooldownSeconds);

            // 처음엔 8초(탱크 160)로 잡았더니 봇이 급수 왕복에 시간을 다 써
            // 상가·공장을 제한 시간 안에 못 깼다. 12초가 "관리해야 하지만
            // 심부름이 되지는 않는" 경계다.
            Assert.InRange(secondsOfWater, 10f, 14f);
        }

        // --- TC-5 ---
        [Fact]
        public void StandingByAHydrant_RefillsTheTank()
        {
            StageRunner runner = Runner(EquipmentId.Bucket);
            Assert.NotEmpty(runner.Hydrants);

            GridPoint tap = runner.Hydrants[0];
            runner.Player.X = tap.X + 0.5f;
            runner.Player.Y = tap.Y + 0.5f;
            runner.Player.Water = 0f;

            for (int frame = 0; frame < 20; frame++) runner.Update(0.05f, default);

            // 1초 동안 RefillPerSecond만큼.
            Assert.InRange(runner.Player.Water, GameConfig.RefillPerSecond * 0.9f, GameConfig.RefillPerSecond * 1.1f);
        }

        // --- TC-6 ---
        [Fact]
        public void TheFireTruck_IsAWaterPointToo()
        {
            // 맵마다 소화전이 하나뿐이라 출구도 급수점으로 친다.
            // 아니면 판이 맵 끝까지 왕복하기가 된다.
            StageRunner runner = Runner(EquipmentId.Bucket);
            Assert.NotEmpty(runner.Exits);

            GridPoint exit = runner.Exits[0];
            Assert.True(runner.NearWaterPoint(exit.X + 0.5f, exit.Y + 0.5f));
        }

        // --- TC-7 ---
        [Fact]
        public void FarFromAnyTap_TheTankStaysEmpty()
        {
            StageRunner runner = Runner(EquipmentId.Bucket);
            runner.Player.Water = 0f;

            // 소방관 스폰 자리는 소화전·출구에서 떨어져 있다.
            Assert.False(runner.NearWaterPoint(runner.Player.X, runner.Player.Y),
                "스폰이 급수점 옆이면 이 검사는 무의미하다");

            for (int frame = 0; frame < 20; frame++) runner.Update(0.05f, default);

            Assert.Equal(0f, runner.Player.Water);
        }

        // --- TC-8 ---
        [Fact]
        public void Smoke_HurtsEvenWithoutFlames()
        {
            StageRunner runner = Runner(EquipmentId.Bucket);

            int cx = runner.Player.CellX;
            int cy = runner.Player.CellY;

            // 맵에 박힌 발화점을 먼저 꺼서 불 피해를 지우고, 연기만 채운다.
            for (int i = 0; i < runner.Grid.Count; i++)
            {
                if (runner.Grid.Cells[i].State == CellState.Burning) runner.Grid.Cells[i].State = CellState.Intact;
                runner.Grid.Cells[i].Smoke = 1f;
            }
            Assert.Equal(0, runner.Grid.CountBurning());

            float before = runner.Player.Hp;
            for (int frame = 0; frame < 20; frame++) runner.Update(0.05f, default);

            Assert.True(runner.Player.Hp < before,
                "불이 없는데도 연기가 (" + cx + "," + cy + ")에서 아프지 않았다");
        }

        // --- TC-9 ---
        [Fact]
        public void Smoke_StopsYouCatchingYourBreath()
        {
            StageRunner smoky = Runner(EquipmentId.Bucket);
            StageRunner clear = Runner(EquipmentId.Bucket);

            smoky.Player.Hp = 50f;
            clear.Player.Hp = 50f;
            smoky.Player.TimeSinceDamage = 999f;
            clear.Player.TimeSinceDamage = 999f;

            // 임계를 겨우 넘는 연기. 피해는 거의 없지만 회복은 막혀야 한다.
            float thin = GameConfig.SmokeChokeThreshold + 0.01f;
            for (int i = 0; i < smoky.Grid.Count; i++) smoky.Grid.Cells[i].Smoke = thin;

            for (int frame = 0; frame < 20; frame++)
            {
                smoky.Update(0.05f, default);
                clear.Update(0.05f, default);
            }

            Assert.True(clear.Player.Hp > 50f, "맑은 곳에서는 회복해야 한다");
            Assert.True(smoky.Player.Hp <= 50f, "연기 속에서 회복했다. " + smoky.Player.Hp);
        }

        // --- TC-10 ---
        [Fact]
        public void TheSuit_SoftensSmokeToo()
        {
            var bare = new StageRunner(StageCatalog.Residential,
                Loadout.FromIds(new[] { EquipmentId.Bucket }));

            var suited = new StageRunner(StageCatalog.Residential,
                new Loadout(new[] { EquipmentCatalog.Bucket }, suitLevel: 5, bootsLevel: 0));

            for (int i = 0; i < bare.Grid.Count; i++) bare.Grid.Cells[i].Smoke = 1f;
            for (int i = 0; i < suited.Grid.Count; i++) suited.Grid.Cells[i].Smoke = 1f;

            for (int frame = 0; frame < 20; frame++)
            {
                bare.Update(0.05f, default);
                suited.Update(0.05f, default);
            }

            Assert.True(suited.Player.Hp > bare.Player.Hp,
                "방화복 " + suited.Player.Hp + " vs 맨몸 " + bare.Player.Hp);
        }

        // --- TC-11 ---
        [Fact]
        public void LevellingGear_DoesNotRaiseWaterCost()
        {
            // 위력만 오르므로 같은 물로 더 많이 끈다 — 그게 장비를 올리는 이유다.
            EquipmentDef lv1 = EquipmentCatalog.Hose;
            EquipmentDef lv9 = EquipmentCatalog.Hose.AtLevel(9);

            Assert.Equal(lv1.WaterCost, lv9.WaterCost);
            Assert.True(lv9.Agent.Power > lv1.Agent.Power);
        }
    }
}
