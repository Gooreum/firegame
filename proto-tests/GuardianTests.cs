using System;
using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 수호자 마을 샘플(docs §20): 자동 분사·버티기·생명줄·수호 반경. 마을(StageRules.Guardian)에서만 켜지고,
    /// 다른 스테이지와 Guardian=false 마을은 옛 규칙 그대로다.
    /// </summary>
    public class GuardianTests
    {
        private static SurvivorSim Quiet(int stage = 1)
        {
            var sim = new SurvivorSim(1, stage);
            sim.Reports = false;
            sim.Structures.Clear();
            sim.Enemies.Clear();
            return sim;
        }

        private static Structure House(SurvivorSim sim, float dx, float dy, int residents = 0)
        {
            var s = new Structure { Kind = StructureKind.House, Name = "가게", Pos = new Vec2(sim.Player.X + dx, sim.Player.Y + dy), Half = new Vec2(2f, 1.5f), Residents = residents };
            sim.Structures.Add(s);
            return s;
        }

        private static void Run(SurvivorSim sim, float seconds, float mx = 0f, float my = 0f)
        {
            for (int i = 0; i < (int)(seconds / SurvivorSim.Dt) && sim.Outcome == SOutcome.Playing; i++)
            {
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(mx, my);
            }
        }

        // --- 자동 분사 ---

        [Fact]
        public void Town_IsGuardian_OtherStagesAreNot()
        {
            StageRules town = SurvivorStages.Get(1);
            Assert.True(town.Guardian);
            Assert.Equal(new[] { 50f, 115f }, town.BigReportTimes);
            for (int n = 2; n <= SurvivorStages.Count; n++) Assert.False(SurvivorStages.Get(n).Guardian);
            Assert.True(new SurvivorSim(1, 1).Guardian);
            Assert.Equal(new[] { 50f, 115f }, new SurvivorSim(1, 1).BigTimes);
            // 옛 마을(테스트가 끈 것)은 옛 대형 신고 시각.
            var old = new SurvivorSim(1, 1) { Guardian = false };
            Assert.Equal(SurvivorSim.BigReportTimes, old.BigTimes);
        }

        [Fact]
        public void AutoSpray_ShootsNearestBurningHouse_WithoutHolding()
        {
            var sim = Quiet();
            Structure shop = House(sim, 0f, 5f);
            sim.Ignite(shop, 0.5f);
            sim.Spraying = false;
            float before = shop.Fire;
            Run(sim, 1f);
            Assert.True(sim.Stats.AutoShots > 0, "자동 분사가 안 나갔다");
            Assert.Equal(0, sim.Stats.FocusShots);
            Assert.True(sim.AutoFiring);
            Assert.True(sim.HoseOn);
            Assert.True(sim.Aim.Y > 0f && Math.Abs(sim.Aim.X) < 0.5f, "집 쪽을 겨눠야 한다: " + sim.Aim.X + "," + sim.Aim.Y);
            Assert.True(shop.Fire < before + (sim.Stage.FireGrowth * 1f), "자동 물이 불을 눌러야 한다: " + shop.Fire);
        }

        [Fact]
        public void AutoSpray_DropIsAutoPowerOfAHeldDrop()
        {
            var auto = Quiet();
            Structure a = House(auto, 0f, 5f);
            auto.Ignite(a, 0.5f);
            auto.Step(0f, 0f);
            Shot autoShot = auto.Shots.Find(s => s.Hose);

            var held = Quiet();
            Structure b = House(held, 0f, 5f);
            held.Ignite(b, 0.5f);
            held.Aim = new Vec2(0f, 1f);
            held.Spraying = true;
            held.Step(0f, 0f);
            Shot heldShot = held.Shots.Find(s => s.Hose);

            Assert.NotNull(autoShot);
            Assert.NotNull(heldShot);
            Assert.Equal(heldShot.Damage * SurvivorSim.AutoPower, autoShot.Damage, 3);
            Assert.False(autoShot.Focus);
            Assert.True(heldShot.Focus);
        }

        [Fact]
        public void Holding_AimsWhereThePlayerPoints_NotAtTheAutoTarget()
        {
            var sim = Quiet();
            Structure shop = House(sim, 0f, 5f);
            sim.Ignite(shop, 0.5f);
            sim.Aim = new Vec2(-1f, 0f);
            sim.Spraying = true;
            sim.Step(0f, 0f);
            Shot shot = sim.Shots.Find(s => s.Hose);
            Assert.NotNull(shot);
            Assert.True(shot.Vel.X < 0f && Math.Abs(shot.Vel.Y) < 0.01f, "쥐면 겨눈 쪽(왼쪽)으로: " + shot.Vel.X + "," + shot.Vel.Y);
            Assert.False(sim.AutoFiring);
            Assert.True(sim.HoseOn);
            Assert.Equal(1, sim.Stats.FocusShots);
        }

        [Fact]
        public void AutoWater_BuildsNoSteam_HeldWaterDoes()
        {
            var sim = Quiet();
            Structure shop = House(sim, 0f, 4f);
            sim.Ignite(shop, 1f);
            Run(sim, 3f);
            Assert.Equal(0f, shop.HoseHold);

            sim = Quiet();
            shop = House(sim, 0f, 4f);
            sim.Ignite(shop, 1f);
            for (int i = 0; i < 60; i++)
            {
                sim.Aim = new Vec2(0f, 1f);
                sim.Spraying = true;
                sim.Step(0f, 0f);
            }
            Assert.True(shop.HoseHold > 0f, "쥐고 쏜 물은 증기를 쌓는다");
        }

        [Fact]
        public void Forest_DoesNotAutoSpray()
        {
            var sim = Quiet(2);
            Assert.False(sim.Guardian);
            Structure shop = House(sim, 0f, 5f);
            sim.Ignite(shop, 0.5f);
            Run(sim, 1f);
            Assert.Equal(0, sim.ShotsFired);
            Assert.False(sim.HoseOn);
        }

        [Fact]
        public void AutoSpray_HoldsFire_WhenNothingBurns()
        {
            var sim = Quiet();
            House(sim, 0f, 5f);
            Run(sim, 1f);
            Assert.Equal(0, sim.ShotsFired);
            Assert.False(sim.AutoFiring);
        }

        [Fact]
        public void AutoSpray_ReachesNoFurtherThanTheStream()
        {
            var sim = Quiet();
            Structure far = House(sim, 0f, SurvivorSim.AutoReach + 3f);
            sim.Ignite(far, 0.5f);
            Run(sim, 0.5f);
            Assert.Equal(0, sim.ShotsFired);
        }
    }
}
