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

        /// <summary>물대포 없이(맨손) 조용한 판: 불이 저절로 어떻게 되는지 본다.</summary>
        private static SurvivorSim Bare(int stage = 1)
        {
            var sim = new SurvivorSim(1, stage, new UpgradeId[0]);
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
        /// <summary>
        /// 끄는 시간: 수호 반경 밖에선 자동 물로 보통 신고도 오래 걸린다(다가가라). 반경 안에선 자동 물로 꺼진다.
        /// 쥐고 쏘면(증기) 반경 밖에서도 자동보다 훨씬 빠르다.
        /// </summary>
        [Fact]
        public void DouseTime_AutoNeedsTheRadius_HoldingIsFaster()
        {
            float autoFar = DouseTime(SurvivorSim.ReportFire, false, 5.5f);
            float autoNear = DouseTime(SurvivorSim.ReportFire, false, 3.5f);
            float held = DouseTime(SurvivorSim.ReportFire, true, 5.5f);
            Assert.True(autoFar < 0f || autoFar > 20f, "반경 밖 자동은 오래 걸려야: " + autoFar);
            Assert.InRange(autoNear, 1f, 15f);
            Assert.InRange(held, 1f, 8f);
            Assert.True(DouseTime(1f, true, 5.5f) > 0f, "큰 불은 쥐면 끈다");
        }

        /// <param name="dy">집 중심까지 거리(집 반높이 1.5): 5.5면 가장자리 4칸(반경 밖), 3.5면 2칸(반경 안).</param>
        private static float DouseTime(float fire, bool hold, float dy)
        {
            var sim = Quiet();
            sim.Guardian = true;
            Structure s = House(sim, 0f, dy);
            sim.Ignite(s, fire);
            for (int i = 0; i < 60 * 60; i++)
            {
                sim.Enemies.Clear();
                sim.Spraying = hold;
                sim.Aim = new Vec2(0f, 1f);
                sim.Step(0f, 0f);
                if (!s.Burning) return sim.Time;
            }
            return -1f;
        }

        // --- 링 없음(2026-10-07 삭제: 이동을 뺏는 강제는 금지) ---

        /// <summary>대형 신고까지 시간을 보내고(적 없이) 신고 건물을 돌려준다.</summary>
        private static Structure ToBigReport(SurvivorSim sim)
        {
            while (sim.BigReport == null && sim.Time < 60f)
            {
                sim.Enemies.Clear();
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
            }
            sim.Enemies.Clear();
            return sim.BigReport;
        }

        [Fact]
        public void BigReport_HasNoRing_TheFirefighterWalksAwayFreely()
        {
            var sim = new SurvivorSim(1, 1);
            Structure big = ToBigReport(sim);
            Assert.NotNull(big);
            sim.Player = new Vec2(big.Pos.X, big.Pos.Y - big.Half.Y - 2f);
            sim.Step(0f, 0f);
            Run(sim, 1f);
            // 옛 링 반경(8칸) 밖 한 걸음: 끌려 돌아오지 않는다.
            Vec2 before = sim.Player;
            Run(sim, 1f, 0f, -1f);
            Assert.True(sim.Player.Y < before.Y - 1f, "걸어 나가야: " + before.Y + " → " + sim.Player.Y);
        }

        [Fact]
        public void BigReport_WaterWorksFromAfar()
        {
            var sim = new SurvivorSim(1, 1);
            Structure big = ToBigReport(sim);
            float fire = big.Fire;
            // 8칸 아래에서 쥐고 3초 쏜다: 봉인이 없으니 먹힌다.
            sim.Player = new Vec2(big.Pos.X, big.Pos.Y - big.Half.Y - 8f);
            for (int i = 0; i < 180; i++)
            {
                sim.Enemies.Clear();
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Spraying = true;
                sim.Aim = new Vec2(big.Pos.X - sim.Player.X, big.Pos.Y - sim.Player.Y);
                sim.Step(0f, 0f);
            }
            Assert.True(big.Fire < fire, "멀리서 쏜 물도 먹혀야: " + fire + " → " + big.Fire);
        }

        // --- 생명줄 ---

        private static void Collapse(SurvivorSim sim, int count)
        {
            int n = 0;
            foreach (Structure s in sim.Structures)
            {
                if (!s.IsBuilding || s.Collapsed || n >= count) continue;
                s.Fire = 1f;
                s.Integrity = 0.00001f;
                n++;
            }
        }

        [Fact]
        public void Guardian_LosingHalfTheTown_DoesNotEndTheRun()
        {
            var sim = new SurvivorSim(1, 1);
            sim.Reports = false;
            Collapse(sim, (sim.HousesTotal / 2) + 1);
            Run(sim, 0.5f);
            Assert.Equal(SOutcome.Playing, sim.Outcome);
            Assert.False(sim.LostTown);
            Assert.True(sim.HousesLost * 2 > sim.HousesTotal);

            var old = new SurvivorSim(1, 1) { Guardian = false };
            old.Reports = false;
            Collapse(old, (old.HousesTotal / 2) + 1);
            Run(old, 0.5f);
            Assert.Equal(SOutcome.Lost, old.Outcome);
            Assert.True(old.LostTown);
        }

        [Fact]
        public void Ruin_SpitsEmbers_EverySevenSeconds()
        {
            var sim = Quiet();
            Structure shop = House(sim, 0f, 15f);
            shop.Fire = 1f;
            shop.Integrity = 0.00001f;
            sim.Step(0f, 0f);
            Assert.True(shop.Collapsed);
            sim.Enemies.Clear();
            bool spat = false;
            for (int i = 0; i < (int)((SurvivorSim.RuinSpitEvery - 0.1f) / SurvivorSim.Dt); i++)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
                spat |= sim.RuinSpat.Contains(shop);
            }
            Assert.False(spat, "7초가 안 됐는데 뱉었다");
            for (int i = 0; i < 12 && !spat; i++)
            {
                sim.Step(0f, 0f);
                spat = sim.RuinSpat.Contains(shop);
            }
            Assert.True(spat);
            Assert.Equal(SurvivorSim.RuinSpit, sim.Enemies.FindAll(e => e.Kind == EnemyKind.Ember && shop.DistanceTo(e.Pos) <= 2f).Count);
        }

        [Fact]
        public void GuardedHouse_IsAHaven()
        {
            var sim = Quiet();
            Structure shop = House(sim, 0f, 3.5f);
            sim.Ignite(shop, 0.05f);
            for (int i = 0; i < 600 && shop.Burning; i++)
            {
                sim.Aim = new Vec2(0f, 1f);
                sim.Spraying = true;
                sim.Step(0f, 0f);
            }
            Assert.False(shop.Burning);
            Assert.True(shop.Guarded);
            sim.Spraying = false;
            sim.Hp = 50f;
            Run(sim, 1f);
            Assert.InRange(sim.Hp, 52.5f, 53.5f);
            Assert.InRange(sim.Stats.HealHaven, 2.5f, 3.5f);
            Assert.Same(shop, sim.Haven);
        }

        [Fact]
        public void NoHaven_BesideAnUntouchedHouse_OrABurningGuardedOne()
        {
            var sim = Quiet();
            House(sim, 0f, 3.5f);
            sim.Hp = 50f;
            Run(sim, 1f);
            Assert.Equal(50f, sim.Hp);

            sim = Quiet();
            Structure shop = House(sim, 0f, 3.5f);
            shop.Guarded = true;
            sim.Ignite(shop, 0.3f);
            sim.Hp = 50f;
            Run(sim, 1f);
            Assert.True(sim.Hp <= 50f, "타는 집은 쉼터가 아니다: " + sim.Hp);
        }

        [Fact]
        public void Director_ReadsVillageSaved()
        {
            var sim = new SurvivorSim(1, 1);
            sim.Reports = true;
            sim.Time = SurvivorSim.FinaleAt - SurvivorSim.Dt;
            sim.Step(0f, 0f);
            Assert.True(sim.Finale);
            int houses = sim.HousesTotal;
            // 지킨 비율 0.5 미만~0.6 미만: 여유가 아니라 안 오른다.
            sim.HousesLost = (int)Math.Ceiling(houses * 0.42f);
            Assert.InRange(sim.VillageSaved, SurvivorSim.GuardTight, SurvivorSim.GuardRoomy - 0.001f);
            for (int i = 0; i < (int)(20f / SurvivorSim.Dt); i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Enemies.Clear();
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
            }
            Assert.Equal(0, sim.FinalePressure);
            // 지킨 비율이 충분하면 오른다.
            sim.HousesLost = 0;
            for (int i = 0; i < (int)(10f / SurvivorSim.Dt); i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Enemies.Clear();
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
            }
            Assert.True(sim.FinalePressure > 0);
        }

        [Fact]
        public void HurtBy_SortsDamageBySource()
        {
            var sim = Quiet();
            Enemy e = sim.Spawn(EnemyKind.Blaze, sim.Player);
            e.Speed = 0f;
            e.Hp = e.MaxHp = 9999f;
            sim.Step(0f, 0f);
            Assert.True(sim.Stats.HurtBy[(int)HurtKind.Contact] > 0f);

            sim = Quiet();
            Structure shop = House(sim, 0f, 2.5f);
            sim.Ignite(shop, 1f);
            shop.Wet = 0f;
            sim.Step(0f, 0f);
            Assert.True(sim.Stats.HurtBy[(int)HurtKind.Heat] > 0f);
        }

        // --- 수호 반경 ---

        [Fact]
        public void GuardRadius_GrowsWithLevel_UpToEight()
        {
            var sim = Quiet();
            Assert.Equal(3f, sim.GuardRadius, 3);
            sim.Level = 5;
            Assert.Equal(4f, sim.GuardRadius, 3);
            sim.Level = 21;
            Assert.Equal(8f, sim.GuardRadius, 3);
            sim.Level = 40;
            Assert.Equal(SurvivorSim.GuardMax, sim.GuardRadius, 3);
            sim.Guardian = false;
            Assert.Equal(0f, sim.GuardRadius);
        }

        [Fact]
        public void InsideTheRadius_FireDoesNotGrow_ButStillBurnsDown()
        {
            var sim = Bare();
            Structure near = House(sim, 0f, 3.5f);
            Structure far = House(sim, 0f, -7.5f);
            sim.Ignite(near, 0.4f);
            sim.Ignite(far, 0.4f);
            Run(sim, 5f);
            Assert.Equal(0.4f, near.Fire, 3);
            Assert.True(near.Integrity < 1f);
            Assert.Equal(0.4f + (sim.Stage.FireGrowth * 5f), far.Fire, 2);
        }

        [Fact]
        public void InsideTheRadius_NoEmbersNoBlazeNoSpread()
        {
            var sim = Bare();
            Structure near = House(sim, 0f, 3.5f);
            Structure next = House(sim, 6f, 3.5f);
            sim.Ignite(near, 0.9f);
            // 가장자리 스폰(17칸 밖)은 그대로 온다: 이번 틱 새로 생긴 불씨(Seeker)·큰 불이 집 가장자리 1.5칸 안이면 집이 뱉은 것이다.
            var seen = new HashSet<Enemy>();
            for (int i = 0; i < (int)(20f / SurvivorSim.Dt); i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                foreach (Enemy e in sim.Enemies)
                {
                    if (!seen.Add(e)) continue;
                    // 집이 뱉는 것: 불씨(Seeker) 또는 큰 불. 가장자리에서 몰려와 가르는 작은 불은 아니다.
                    bool fromHouse = (e.Seeker || e.Kind == EnemyKind.Blaze) && near.DistanceTo(e.Pos) <= 1.5f;
                    Assert.False(fromHouse, "반경 안 집이 불 몹을 뱉었다: " + e.Kind + " t=" + sim.Time);
                }
            }
            Assert.False(next.Burning);
            Assert.Empty(sim.Spread);
        }

        [Fact]
        public void InsideTheRadius_AutoWaterPutsOutABigFire()
        {
            var sim = Quiet();
            Structure big = House(sim, 0f, 3.5f);
            sim.Ignite(big, SurvivorSim.BigReportFire);
            float t = -1f;
            for (int i = 0; i < 60 * 60; i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                if (!big.Burning) { t = sim.Time; break; }
            }
            Assert.True(t > 0f, "반경 안 큰 불도 자동 물로 꺼야: " + big.Fire);
            Assert.True(t < 30f, "끄는 데 " + t + "초");
        }

        [Fact]
        public void Forest_HasNoGuardRadius()
        {
            var sim = Bare(2);
            Structure near = House(sim, 0f, 3.5f);
            sim.Ignite(near, 0.4f);
            Run(sim, 5f);
            Assert.True(near.Fire > 0.45f);
        }

        [Fact]
        public void MoveOnlyBot_NeverHolds_ButStillShoots()
        {
            var sim = new SurvivorSim(3, 1);
            var bot = new SurvivorBot(sim) { MoveOnly = true, Pro = true };
            for (int i = 0; i < (int)(30f / SurvivorSim.Dt) && sim.Outcome == SOutcome.Playing; i++)
            {
                bot.Play();
                Assert.False(sim.Spraying);
            }
            Assert.Equal(0, sim.Stats.FocusShots);
            Assert.True(sim.Stats.AutoShots > 0);
        }

        [Fact]
        public void MoveOnlyProBot_DousesAHouse_ByAutoWaterAlone()
        {
            var sim = Quiet();
            Structure shop = House(sim, 0f, 5f);
            sim.Ignite(shop, SurvivorSim.ReportFire);
            var bot = new SurvivorBot(sim) { MoveOnly = true, Pro = true };
            for (int i = 0; i < (int)(40f / SurvivorSim.Dt) && shop.Burning; i++)
            {
                sim.Enemies.Clear();
                bot.Play();
            }
            Assert.False(shop.Burning, "자동 물로 40초 안에 못 껐다: " + shop.Fire + " 거리 " + shop.DistanceTo(sim.Player));
        }

        [Fact]
        public void ProBot_StillHolds()
        {
            var sim = Quiet();
            Structure shop = House(sim, 0f, 5f);
            sim.Ignite(shop, 0.6f);
            var bot = new SurvivorBot(sim) { Pro = true };
            for (int i = 0; i < 120; i++) bot.Play();
            Assert.True(sim.Stats.FocusShots > 0);
        }
    }
}
