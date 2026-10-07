using System.Collections.Generic;
using System.Linq;
using FireGame.Core.Sim;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 숲 개편(2026-10-08, 승인 샘플 그대로): 칸 제한 없음 · 13종 모두 Lv5 다음 Lv6 최고급(짝 조건 없음) · 보조 Lv6 셋 · 구조대원.
    /// 숲(2스테이지)에서만 켜지고 다른 스테이지는 그대로다.
    /// </summary>
    public class ForestLevelUpTests
    {
        private const int Forest = 2;

        private static SurvivorSim Quiet(int stage = Forest)
        {
            var sim = new SurvivorSim(1, stage) { Guardian = false };
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        private static void Take(SurvivorSim sim, UpgradeId id, int times = 1)
        {
            for (int i = 0; i < times; i++)
            {
                sim.PendingChoices = new List<UpgradeId> { id };
                sim.Choose(0);
            }
        }

        private static HashSet<UpgradeId> Offered(Loadout l, int rolls = 500)
        {
            var rng = new Rng(11);
            var seen = new HashSet<UpgradeId>();
            for (int i = 0; i < rolls; i++) foreach (UpgradeId id in SurvivorUpgrades.Roll(l, 2, ref rng)) seen.Add(id);
            return seen;
        }

        // --- Step 1 ---
        [Fact]
        public void Forest_IsFree_OtherStagesAreNot()
        {
            Assert.True(new SurvivorSim(1, Forest).Build.Free);
            foreach (int stage in new[] { 1, 3, 4, 5 }) Assert.False(new SurvivorSim(1, stage).Build.Free);
        }

        [Fact]
        public void Forest_NoSlotCap_EveryItemStillOffered()
        {
            var l = new Loadout { Free = true };
            foreach (UpgradeId id in new[] { UpgradeId.Hose, UpgradeId.Sprinkler, UpgradeId.Balloon, UpgradeId.Mine, UpgradeId.Foam, UpgradeId.Chain, UpgradeId.Tank, UpgradeId.Boots, UpgradeId.Suit }) l.Add(id);
            HashSet<UpgradeId> seen = Offered(l);
            foreach (UpgradeId id in new[] { UpgradeId.Extinguisher, UpgradeId.Bubble, UpgradeId.Manhole, UpgradeId.Whip }) Assert.Contains(id, seen);
        }

        [Fact]
        public void Town_FourSlots_StillCap()
        {
            var l = new Loadout();
            foreach (UpgradeId id in new[] { UpgradeId.Hose, UpgradeId.Chain, UpgradeId.Mine, UpgradeId.Suit }) l.Add(id);
            Assert.Equal(new HashSet<UpgradeId> { UpgradeId.Hose, UpgradeId.Chain, UpgradeId.Mine, UpgradeId.Suit }, Offered(l));
        }

        [Fact]
        public void Forest_WeaponLv5_WithoutPair_ForcesLv6Card()
        {
            var l = new Loadout { Free = true };
            for (int i = 0; i < Loadout.MaxLevel; i++) l.Add(UpgradeId.Hose);
            Assert.Equal(0, l.Level(UpgradeId.Tank));
            var rng = new Rng(3);
            for (int i = 0; i < 50; i++) Assert.Contains(UpgradeId.Cannon, SurvivorUpgrades.Roll(l, 2, ref rng));
        }

        [Fact]
        public void Forest_PassiveLv5_OffersPassiveLv6_AndKeepsItsPower()
        {
            var sim = Quiet();
            Take(sim, UpgradeId.Tank, Loadout.MaxLevel);
            Take(sim, UpgradeId.Boots, Loadout.MaxLevel);
            Take(sim, UpgradeId.Suit, Loadout.MaxLevel);
            HashSet<UpgradeId> seen = Offered(sim.Build);
            Assert.Contains(UpgradeId.OverPump, seen);
            Assert.Contains(UpgradeId.JetBoots, seen);
            Assert.Contains(UpgradeId.PhoenixSuit, seen);

            float range = sim.Build.HoseRange;
            float speed = sim.Build.SpeedScale;
            float hp = sim.MaxHp;
            Take(sim, UpgradeId.OverPump);
            Take(sim, UpgradeId.JetBoots);
            Take(sim, UpgradeId.PhoenixSuit);
            Assert.Equal(range, sim.Build.HoseRange);
            Assert.Equal(speed, sim.Build.SpeedScale);
            Assert.Equal(hp, sim.MaxHp);
            Assert.Equal(3, sim.Build.PassiveCount);
            Assert.Equal(1, sim.Build.WeaponCount);
        }

        [Fact]
        public void Town_NeverOffersPassiveLv6()
        {
            var l = new Loadout();
            for (int i = 0; i < Loadout.MaxLevel; i++)
            {
                l.Add(UpgradeId.Tank);
                l.Add(UpgradeId.Boots);
            }
            HashSet<UpgradeId> seen = Offered(l);
            Assert.DoesNotContain(UpgradeId.OverPump, seen);
            Assert.DoesNotContain(UpgradeId.JetBoots, seen);
            Assert.False(l.Ready(UpgradeId.PhoenixSuit));
        }

        private static void Run(SurvivorSim sim, float seconds, bool keepHp = true)
        {
            int ticks = (int)(seconds / SurvivorSim.Dt);
            for (int i = 0; i < ticks; i++)
            {
                if (keepHp) sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
            }
        }

        private static Structure BurningShop(SurvivorSim sim, int residents)
        {
            var s = new Structure { Kind = StructureKind.House, Name = "가게", Pos = new Vec2(sim.Player.X + 6f, sim.Player.Y + 6f), Half = new Vec2(2f, 1.5f), Residents = residents, Fire = 0.2f };
            sim.Structures.Add(s);
            return s;
        }

        /// <summary>문 앞에 서서 n명을 구한다(불은 작게 유지해 연기로 잃지 않게).</summary>
        private static void RescueN(SurvivorSim sim, int n)
        {
            Structure shop = BurningShop(sim, n);
            int start = sim.Rescued;
            for (int i = 0; i < 6000 && sim.Rescued < start + n; i++)
            {
                sim.Player = shop.Door;
                shop.Fire = 0.2f;
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
            }
            Assert.Equal(start + n, sim.Rescued);
        }

        // --- 무기 레벨별 동작은 SampleArsenalTests(승인 샘플 수치) ---
        [Fact]
        public void Forest_Rescue_JoinsCrew_UpToEight()
        {
            var sim = Quiet();
            RescueN(sim, 1);
            Assert.Single(sim.CrewList);
            RescueN(sim, 8);
            Assert.Equal(SurvivorSim.MaxCrew, sim.CrewList.Count);
        }

        [Fact]
        public void Forest_Crew_SpraysNearbyMob()
        {
            var sim = Quiet();
            RescueN(sim, 1);
            sim.Structures.Clear();
            Run(sim, 1f);
            Crew c = sim.CrewList[0];
            var e = sim.Spawn(EnemyKind.Ember, new Vec2(c.Pos.X + 4f, c.Pos.Y));
            e.Speed = 0f;
            e.MaxHp = e.Hp = 999f;
            Run(sim, 2f);
            Assert.True(e.Hp < 999f, "대원이 4칸 앞 몹에 물을 쏴야");
        }

        [Fact]
        public void Town_Rescue_NoCrew()
        {
            var sim = Quiet(1);
            RescueN(sim, 2);
            Assert.Empty(sim.CrewList);
        }

        [Fact]
        public void Forest_BotRun_FinishesWithManyItems()
        {
            var sim = new SurvivorSim(1, Forest);
            var bot = new SurvivorBot(sim) { Pro = true };
            while (sim.Outcome == SOutcome.Playing) bot.Play();
            int kinds = sim.Build.Owned().Count();
            Assert.True(kinds >= 5, "숲은 칸이 없으니 5종 이상 들어야: " + kinds + " (" + string.Join(",", sim.Build.Owned()) + ")");
        }
    }
}
