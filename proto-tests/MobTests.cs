using System;
using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 마을(수호자)의 몹(2026-10-07): 나보다 마을을 노린다. 불쥐 줄·횃불 도깨비·불풍선·불곰(엘리트)·화마(보스),
    /// 큰 신고 대신 습격. 다른 스테이지엔 없다.
    /// </summary>
    public class MobTests
    {
        /// <summary>마을, 신고·스폰 없이(직접 놓은 몹만).</summary>
        private static SurvivorSim Town()
        {
            var sim = new SurvivorSim(1, 1);
            sim.Enemies.Clear();
            sim.Reports = false;
            return sim;
        }

        private static Structure House(SurvivorSim sim, Func<Structure, bool> where = null)
        {
            return sim.Structures.Find(s => s.IsBuilding && !s.Burning && !s.Collapsed && (where == null || where(s)));
        }

        /// <summary>seconds 동안 흘린다(소방관은 멀리 서서 안 다친다, 쏘지 않는다, 카드는 첫 장). 놓아 둔 몹 말고 새로 나온 적은 지운다.</summary>
        private static void Run(SurvivorSim sim, float seconds, Func<bool> until = null, bool keepNew = false)
        {
            var mine = new List<Enemy>(sim.Enemies);
            for (int i = 0; i < (int)(seconds / SurvivorSim.Dt) && sim.Outcome == SOutcome.Playing; i++)
            {
                if (!keepNew) sim.Enemies.RemoveAll(e => !mine.Contains(e) && e.Kind != EnemyKind.Rat);
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
                if (until != null && until()) return;
            }
        }

        /// <summary>소방관을 맵 구석(무기가 닿지 않는 곳)으로.</summary>
        private static void Away(SurvivorSim sim)
        {
            sim.Player = new Vec2(2f, 2f);
        }

        private static SurvivorSim Unarmed()
        {
            var sim = new SurvivorSim(1, 1, new UpgradeId[0]);
            sim.Enemies.Clear();
            sim.Reports = false;
            Away(sim);
            return sim;
        }

        // ------------------------------------------------------------------
        // 불쥐
        // ------------------------------------------------------------------

        [Fact]
        public void RatLine_AllGoForOneHouse_FollowingTheOneAhead()
        {
            SurvivorSim sim = Unarmed();
            Structure goal = House(sim);
            var from = new Vec2(goal.Pos.X, goal.Pos.Y + 12f);
            if (from.Y > SurvivorSim.ArenaSize - 1f) from = new Vec2(goal.Pos.X, goal.Pos.Y - 12f);
            sim.SpawnRatLine(from, goal, 6);
            List<Enemy> rats = sim.Enemies.FindAll(e => e.Kind == EnemyKind.Rat);
            Assert.Equal(6, rats.Count);
            Assert.All(rats, r => Assert.Same(goal, r.Goal));
            Assert.Null(rats[0].Leader);
            for (int i = 1; i < rats.Count; i++) Assert.Same(rats[i - 1], rats[i].Leader);
            Run(sim, 1f);
            // 한 줄: 뒤 쥐는 앞 쥐 곁에 붙어 있다.
            for (int i = 1; i < rats.Count; i++)
            {
                if (rats[i].Dead || rats[i - 1].Dead) continue;
                Assert.True(rats[i].Pos.DistanceTo(rats[i - 1].Pos) < 1.2f, i + "번 쥐가 줄에서 떨어졌다: " + rats[i].Pos.DistanceTo(rats[i - 1].Pos));
            }
        }

        [Fact]
        public void Rat_ReachingTheHouse_SetsItOnFire_AndIsGone()
        {
            SurvivorSim sim = Unarmed();
            Structure goal = House(sim);
            sim.SpawnRatLine(new Vec2(goal.Pos.X + goal.Half.X + 3f, goal.Pos.Y), goal, 1);
            Enemy rat = sim.Enemies.Find(e => e.Kind == EnemyKind.Rat);
            Run(sim, 3f, () => goal.Burning);
            Assert.True(goal.Burning, "쥐가 집에 불을 붙여야");
            Assert.True(rat.Dead);
            Assert.True(sim.RaiderIgnites >= 1);
        }

        [Fact]
        public void RatLines_StartAt25s_InTheTown()
        {
            var sim = new SurvivorSim(1, 1);
            bool line = false;
            float at = 0f;
            for (int i = 0; i < 60 * 40 && !line; i++)
            {
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
                if (sim.RatLines.Count > 0)
                {
                    line = true;
                    at = sim.Time;
                }
            }
            Assert.True(line, "불쥐 줄이 안 왔다");
            Assert.InRange(at, SurvivorSim.RatFrom - 0.1f, SurvivorSim.RatFrom + 0.1f);
        }

        // ------------------------------------------------------------------
        // 횃불 도깨비
        // ------------------------------------------------------------------

        [Fact]
        public void Goblin_StopsAtTheHouse_WindsUp_ThenThrowsATorch()
        {
            SurvivorSim sim = Unarmed();
            Structure goal = House(sim);
            Enemy g = sim.Spawn(EnemyKind.Goblin, new Vec2(goal.Pos.X, goal.Pos.Y - goal.Half.Y - 2f));
            g.Goal = goal;
            Run(sim, 0.5f);
            Assert.False(goal.Burning, "예고 전엔 불이 안 붙는다");
            Assert.True(g.Phase > 0f, "예고 중이어야");
            bool thrown = false;
            Run(sim, SurvivorSim.GoblinWindup + 0.2f, () => thrown |= sim.TorchThrows.Count > 0);
            Assert.True(thrown);
            Assert.True(goal.Burning);
            Assert.NotSame(goal, g.Goal);
        }

        [Fact]
        public void Goblin_HitDuringWindup_StartsOver()
        {
            // 물대포를 쥐고 계속 맞힌다: 맞을 때마다 예고가 처음부터라 끝내 못 던진다.
            var sim = new SurvivorSim(1, 1);
            sim.Enemies.Clear();
            sim.Reports = false;
            Structure goal = House(sim);
            Enemy g = sim.Spawn(EnemyKind.Goblin, new Vec2(goal.Pos.X, goal.Pos.Y - goal.Half.Y - 2f));
            g.Goal = goal;
            g.MaxHp = g.Hp = 99999f;
            sim.Player = new Vec2(g.Pos.X + 4f, g.Pos.Y - 1f);
            for (int i = 0; i < 60 * 4; i++)
            {
                sim.Enemies.RemoveAll(e => e != g);
                sim.Hp = sim.MaxHp;
                sim.Spraying = true;
                sim.Aim = new Vec2(g.Pos.X - sim.Player.X, g.Pos.Y - sim.Player.Y);
                sim.Step(0f, 0f);
            }
            Assert.True(g.Hp < g.MaxHp, "물을 맞고 있어야");
            Assert.False(goal.Burning, "맞을 때마다 예고가 끊겨야");
        }

        // ------------------------------------------------------------------
        // 불풍선
        // ------------------------------------------------------------------

        [Fact]
        public void FireBalloon_HoversOverAHouse_AndBurstsIntoSeveralFires()
        {
            SurvivorSim sim = Unarmed();
            Structure goal = House(sim);
            Enemy b = sim.Spawn(EnemyKind.FireBalloon, new Vec2(goal.Pos.X + 3f, goal.Pos.Y + 3f));
            b.Goal = goal;
            bool burst = false;
            Run(sim, 10f, () => burst |= sim.FireBalloonBlasts.Count > 0);
            Assert.True(burst);
            Assert.True(b.Dead);
            Assert.True(goal.Burning);
            int burning = sim.Structures.FindAll(s => s.Burning && s.Within(goal.Pos, SurvivorSim.FireBalloonBlast + 2f)).Count;
            Assert.True(burning >= 2, "둘레 여럿에 불이 붙어야: " + burning);
        }

        [Fact]
        public void FireBalloon_PoppedBeforeItsFuse_SetsNothingOnFire()
        {
            SurvivorSim sim = Unarmed();
            Structure goal = House(sim);
            Enemy b = sim.Spawn(EnemyKind.FireBalloon, new Vec2(goal.Pos.X + 4f, goal.Pos.Y));
            b.Goal = goal;
            Run(sim, 1f);
            sim.Kill(b);
            Run(sim, 3f);
            Assert.False(goal.Burning);
            Assert.Empty(sim.Structures.FindAll(s => s.Burning));
        }

        // ------------------------------------------------------------------
        // 다른 스테이지
        // ------------------------------------------------------------------

        [Fact]
        public void Forest_HasNoTownMobs()
        {
            var sim = new SurvivorSim(1, 2);
            for (int i = 0; i < 60 * 130; i++)
            {
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
                Assert.DoesNotContain(sim.Enemies, e => SurvivorSim.IsRaider(e.Kind));
            }
            Assert.Empty(sim.Raids);
        }

        [Fact]
        public void Goblins_JoinTheTownCrowd_After55s()
        {
            var sim = new SurvivorSim(3, 1);
            bool before = false;
            bool after = false;
            for (int i = 0; i < 60 * 150; i++)
            {
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
                bool goblin = sim.Enemies.Exists(e => e.Kind == EnemyKind.Goblin);
                // 50초 습격의 도깨비 둘은 따로: 무리에 섞이는 건 GoblinFrom부터.
                if (sim.Raids.Count == 0) before |= goblin;
                else if (sim.Time > 100f) after |= goblin;
            }
            Assert.False(before, "첫 습격 전엔 도깨비가 없다");
            Assert.True(after, "55초 뒤엔 도깨비가 섞인다");
        }

        // ------------------------------------------------------------------
        // 불곰 · 습격
        // ------------------------------------------------------------------

        [Fact]
        public void Bear_PlowsThroughHouses_SettingEachOnFire()
        {
            SurvivorSim sim = Unarmed();
            Structure a = House(sim);
            Enemy bear = sim.Spawn(EnemyKind.Bear, new Vec2(a.Pos.X - a.Half.X - 3f, a.Pos.Y));
            bear.Goal = a;
            bear.MaxHp = bear.Hp = 99999f;
            Run(sim, 12f);
            int burning = sim.Structures.FindAll(s => s.IsBuilding && (s.Burning || s.Collapsed)).Count;
            Assert.True(a.Burning || a.Collapsed, "첫 집에 불");
            Assert.True(burning >= 2, "지나간 집마다 불이 붙어야: " + burning);
        }

        [Fact]
        public void Bear_DropsAChest_WhenKilled()
        {
            SurvivorSim sim = Unarmed();
            Enemy bear = sim.Spawn(EnemyKind.Bear, new Vec2(20f, 20f));
            sim.Kill(bear);
            Assert.Single(sim.Chests);
            Assert.True(sim.Chests[0].Pos.DistanceTo(new Vec2(20f, 20f)) < 0.01f);
            Assert.Single(sim.BearsDown);
            Assert.Equal(1, sim.RaidersKilled);
        }

        [Fact]
        public void Town_At50s_ARaidComesFromOneEdge_NotABigReportFire()
        {
            var sim = new SurvivorSim(1, 1);
            Raid raid = null;
            for (int i = 0; i < 60 * 52 && raid == null; i++)
            {
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
                raid = sim.JustRaid;
            }
            Assert.NotNull(raid);
            Assert.InRange(sim.Time, 49.9f, 50.1f);
            Assert.Null(sim.BigReport);
            Assert.InRange(raid.Targets.Count, 2, 3);
            // 안 탄 집을 먼저 노린다(다 타고 있으면 타는 집이라도).
            if (sim.Structures.Exists(x => x.IsBuilding && !x.Burning && !x.Collapsed)) Assert.False(raid.Targets[0].Burning, "첫 표적은 안 탄 집");
            // 가장자리 한 곳에 곰 하나, 쥐 줄 둘(6+6), 도깨비 둘.
            List<Enemy> near = sim.Enemies.FindAll(e => e.Pos.DistanceTo(raid.From) < 4f);
            Assert.Equal(1, near.FindAll(e => e.Kind == EnemyKind.Bear).Count);
            Assert.Equal(2, near.FindAll(e => e.Kind == EnemyKind.Goblin).Count);
            Assert.True(near.FindAll(e => e.Kind == EnemyKind.Rat).Count >= 2 * SurvivorSim.RatLineStart - 2);
            Assert.All(near.FindAll(e => SurvivorSim.IsRaider(e.Kind)), e => Assert.Contains(e.Goal, raid.Targets));
        }

        [Fact]
        public void Raid_LeftAlone_BurnsItsTargets()
        {
            var sim = new SurvivorSim(1, 1, new UpgradeId[0]);
            Raid raid = null;
            for (int i = 0; i < 60 * 80; i++)
            {
                sim.Hp = sim.MaxHp;
                // 소방관은 습격 반대편 구석에 숨어 있다.
                if (raid != null) sim.Player = new Vec2(raid.From.X < 30f ? 57f : 3f, raid.From.Y < 30f ? 57f : 3f);
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
                raid = raid ?? sim.JustRaid;
            }
            Assert.True(raid != null, "습격이 안 왔다: " + sim.Time + " " + sim.Outcome + " 레벨 " + sim.Level);
            int hit = raid.Targets.FindAll(t => t.Burning || t.Collapsed || t.Integrity < 1f).Count;
            Assert.True(hit >= 2, "막지 않으면 표적이 타야: " + hit + "/" + raid.Targets.Count);
        }

    }
}
