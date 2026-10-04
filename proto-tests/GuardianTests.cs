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
        /// <summary>끄는 시간: 자동 물은 보통 신고를 12초 안에, 쥐고 쏘면(증기) 그 절반 남짓에. 큰 불(1.0)은 쥐어야 끈다(수호 반경 밖).</summary>
        [Fact]
        public void DouseTime_AutoHandlesReports_HoldingIsTwiceAsFast()
        {
            float auto = DouseTime(SurvivorSim.ReportFire, false);
            float held = DouseTime(SurvivorSim.ReportFire, true);
            Assert.InRange(auto, 5f, 12f);
            Assert.True(held * 1.8f <= auto, "쥐면 1.8배 넘게 빨라야: 쥠 " + held + " 자동 " + auto);
            Assert.True(DouseTime(1f, true) > 0f, "큰 불은 쥐면 끈다");
        }

        private static float DouseTime(float fire, bool hold)
        {
            var sim = Quiet();
            sim.Guardian = true;
            Structure s = House(sim, 0f, 5.5f);
            sim.Ignite(s, fire);
            // 수호 반경 밖에 서서 잰다(반경 안이면 불이 안 자라 다른 숫자가 나온다).
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

        // --- 버티기 ---

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

        /// <summary>건물 가장자리 dist칸 아래(남쪽)로 순간이동.</summary>
        private static void StandBelow(SurvivorSim sim, Structure s, float dist)
        {
            sim.Player = new Vec2(s.Pos.X, s.Pos.Y - s.Half.Y - dist);
        }

        [Fact]
        public void Siege_ClosesWhenTheFirefighterArrives()
        {
            var sim = new SurvivorSim(1, 1);
            Structure big = ToBigReport(sim);
            Assert.NotNull(big);
            Assert.InRange(sim.Time, 50f, 50.1f);
            Assert.Contains(big, sim.SiegeTargets);
            StandBelow(sim, big, 6.5f);
            sim.Step(0f, 0f);
            Assert.Null(sim.Siege);
            StandBelow(sim, big, 4.5f);
            sim.Step(0f, 0f);
            Assert.NotNull(sim.Siege);
            Assert.Same(big, sim.Siege.Target);
            Assert.True(sim.JustSiegeStart);
        }

        [Fact]
        public void Siege_KeepsTheFirefighterInsideTheRing()
        {
            var sim = new SurvivorSim(1, 1);
            Structure big = ToBigReport(sim);
            StandBelow(sim, big, 4f);
            sim.Step(0f, 0f);
            Assert.NotNull(sim.Siege);
            for (int i = 0; i < 180; i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                sim.Step(0f, -1f);
            }
            Assert.NotNull(sim.Siege);
            Assert.True(sim.Player.DistanceTo(sim.Siege.Center) <= SurvivorSim.SiegeRadius + 0.01f, "링 밖으로 나갔다: " + sim.Player.DistanceTo(sim.Siege.Center));
        }

        [Fact]
        public void Siege_SendsWavesFromTheRingEdge()
        {
            var sim = new SurvivorSim(1, 1);
            Structure big = ToBigReport(sim);
            StandBelow(sim, big, 4f);
            sim.Step(0f, 0f);
            sim.Enemies.Clear();
            bool waved = false;
            for (int i = 0; i < 160 && !waved; i++)
            {
                sim.Step(0f, 0f);
                waved = sim.JustSiegeWave;
            }
            Assert.True(waved);
            int edge = sim.Enemies.FindAll(e => e.Kind == EnemyKind.Ember && e.Pos.DistanceTo(sim.Siege.Center) >= SurvivorSim.SiegeRadius - 0.5f).Count;
            Assert.True(edge >= SurvivorSim.SiegeWaveBase, "가장자리 불씨 " + edge);
        }

        [Fact]
        public void Siege_DousingTheTarget_ReleasesAndClearsTheBlock()
        {
            var sim = new SurvivorSim(1, 1);
            Structure big = ToBigReport(sim);
            StandBelow(sim, big, 4f);
            sim.Step(0f, 0f);
            Vec2 c = sim.Siege.Center;
            Enemy near = sim.Spawn(EnemyKind.Blaze, new Vec2(c.X + 6f, c.Y));
            Enemy far = sim.Spawn(EnemyKind.Ember, new Vec2(c.X + 20f, c.Y));
            Structure neighbour = sim.Structures.Find(s => s != big && s.IsBuilding && !s.Collapsed && s.DistanceTo(c) <= SurvivorSim.SiegeReliefRange);
            int xp = sim.Xp;
            int level = sim.Level;
            sim.Hp = 50f;
            big.Fire = 0.001f;
            big.Residents = 2;
            sim.Spraying = false;
            for (int i = 0; i < 30 && sim.Siege != null; i++) sim.Step(0f, 0f);
            Assert.Null(sim.Siege);
            Assert.Equal(1, sim.Stats.SiegesWon);
            Assert.True(near.Dead, "링 안 큰 불이 사그라들어야");
            Assert.False(far.Dead, "멀리 있는 불씨는 그대로");
            Assert.True(sim.Level > level || sim.PendingChoices != null || sim.Xp >= xp + SurvivorSim.SiegeXp - 1, "경험치 " + xp + " → " + sim.Xp);
            Assert.True(sim.Hp >= 50f + SurvivorSim.SiegeHeal - 1f);
            if (neighbour != null) Assert.True(neighbour.Wet >= SurvivorSim.SiegeReliefWet - 1f);
            Assert.DoesNotContain(big, sim.SiegeTargets);
        }

        [Fact]
        public void Siege_CollapseOpensTheRing_AsALoss()
        {
            var sim = new SurvivorSim(1, 1);
            Structure big = ToBigReport(sim);
            StandBelow(sim, big, 4f);
            sim.Step(0f, 0f);
            big.Integrity = 0.0001f;
            big.Fire = 1f;
            for (int i = 0; i < 10 && sim.Siege != null; i++)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
            }
            Assert.Null(sim.Siege);
            Assert.True(big.Collapsed);
            Assert.Equal(1, sim.Stats.SiegesLost);
            Assert.True(sim.JustSiegeLost || sim.Stats.SiegesLost == 1);
        }

        [Fact]
        public void Siege_RestsTheFinaleRing()
        {
            var sim = new SurvivorSim(1, 1);
            Structure big = ToBigReport(sim);
            StandBelow(sim, big, 4f);
            sim.Step(0f, 0f);
            sim.Finale = true;
            sim.FinalePressure = 3;
            sim.Reports = false;
            for (int i = 0; i < 60 * 9; i++)
            {
                sim.Hp = sim.MaxHp;
                big.Fire = 0.9f;
                big.Integrity = 1f;
                sim.Step(0f, 0f);
                Assert.DoesNotContain(sim.Enemies, e => e.Heavy);
            }
        }

        [Fact]
        public void Forest_HasNoSiege()
        {
            var sim = new SurvivorSim(1, 2);
            Structure big = null;
            while (sim.BigReport == null && sim.Time < 90f)
            {
                sim.Enemies.Clear();
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
            }
            big = sim.BigReport;
            Assert.NotNull(big);
            StandBelow(sim, big, 3f);
            sim.Step(0f, 0f);
            Assert.Null(sim.Siege);
            Assert.Empty(sim.SiegeTargets);
        }

        [Fact]
        public void Siege_LandmarkIsATarget()
        {
            var sim = new SurvivorSim(1, 1);
            sim.Time = SurvivorSim.FinaleAt - SurvivorSim.Dt;
            sim.Step(0f, 0f);
            Assert.True(sim.Finale);
            Assert.NotNull(sim.Landmark);
            Assert.Contains(sim.Landmark, sim.SiegeTargets);
            StandBelow(sim, sim.Landmark, 3f);
            sim.Step(0f, 0f);
            Assert.NotNull(sim.Siege);
            Assert.Same(sim.Landmark, sim.Siege.Target);
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
            for (int i = 0; i < (int)(20f / SurvivorSim.Dt) && shop.Burning; i++)
            {
                sim.Enemies.Clear();
                bot.Play();
            }
            Assert.False(shop.Burning, "자동 물로 20초 안에 못 껐다: " + shop.Fire);
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
