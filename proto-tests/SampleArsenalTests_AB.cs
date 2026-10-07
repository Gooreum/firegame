using System;
using System.Collections.Generic;
using System.Linq;
using FireGame.Core.Sim;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 숲 샘플 아이템 A·B(items-a.js·items-b.js 그대로): 물대포 · 회전 스프링클러 · 물풍선 · 소화기 부메랑 · 액체질소 지뢰의 레벨별 수치.
    /// 무기 없이 시작해(호스 없음) 아이템 하나만 든 채, 카드 경로(Choose)로 올려 시험한다.
    /// </summary>
    public class SampleArsenalTests_AB
    {
        private const int Forest = 2;

        private static SurvivorSim Bare()
        {
            var sim = new SurvivorSim(1, Forest, new List<UpgradeId>()) { Guardian = false };
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

        /// <summary>lv까지 올린다(6이면 Lv5 + 최고급 카드).</summary>
        private static void To(SurvivorSim sim, UpgradeId id, int lv)
        {
            int now = sim.SampleLevel(id);
            if (lv <= 5) Take(sim, id, lv - now);
            else
            {
                Take(sim, id, 5 - now);
                Take(sim, Loadout.EvolutionOf(id).Value);
            }
            Assert.Equal(lv, sim.SampleLevel(id));
        }

        /// <summary>돌린다: 감독이 낸 몹은 지우고(keep만 남긴다) 체력은 가득. each는 틱마다(멈추려면 true).</summary>
        private static void Run(SurvivorSim sim, float seconds, HashSet<Enemy> keep = null, Func<bool> each = null)
        {
            int ticks = (int)Math.Round(seconds / SurvivorSim.Dt);
            for (int i = 0; i < ticks; i++)
            {
                sim.Enemies.RemoveAll(e => keep == null || !keep.Contains(e));
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
                if (each != null && each()) return;
            }
        }

        /// <summary>소방관에서 샘플 px(dx, dy)만큼 떨어진 곳에 멈춘 튼튼한 몹.</summary>
        private static Enemy Mob(SurvivorSim sim, float dx, float dy, HashSet<Enemy> keep)
        {
            Enemy e = sim.Spawn(EnemyKind.Ember, SurvivorSim.FromS(sim.PX + dx, sim.PY + dy));
            e.Speed = 0f;
            e.MaxHp = e.Hp = 1e6f;
            keep.Add(e);
            return e;
        }

        private static T Item<T>(SurvivorSim sim, UpgradeId id) where T : SampleItem
        {
            return Assert.IsType<T>(sim.SampleItemOf(id));
        }

        private static float DistS(SurvivorSim sim, Enemy e)
        {
            return SurvivorSim.Hypot(SurvivorSim.SX(e.Pos) - sim.PX, SurvivorSim.SY(e.Pos) - sim.PY);
        }

        // ---------------------------------------------------------------- 물대포
        [Theory]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        [InlineData(3, 3)]
        [InlineData(4, 4)]
        [InlineData(5, 5)]
        [InlineData(6, 5)]
        public void Hose_StreamCountIsLevel_TargetsNearest(int lv, int streams)
        {
            var sim = Bare();
            To(sim, UpgradeId.Hose, lv);
            var h = Item<HoseItem>(sim, UpgradeId.Hose);
            var keep = new HashSet<Enemy>();
            var mobs = new List<Enemy>();
            for (int i = 0; i < 7; i++) mobs.Add(Mob(sim, (i % 2 == 0 ? 1f : -1f) * (40f + (i * 22f)), 10f, keep));
            Run(sim, SurvivorSim.Dt, keep);
            Assert.Equal(streams, h.Tg.Count);
            List<Enemy> nearest = mobs.OrderBy(e => DistS(sim, e)).Take(streams).ToList();
            Assert.Equal(nearest.ToHashSet(), h.Tg.ToHashSet());
        }

        [Fact]
        public void Hose_Lv3_PiercesMobBehindTarget()
        {
            var sim = Bare();
            To(sim, UpgradeId.Hose, 3);
            var keep = new HashSet<Enemy>();
            Mob(sim, 60f, 0f, keep);
            Enemy behind = Mob(sim, 140f, 0f, keep);
            Run(sim, SurvivorSim.Dt, keep);
            Assert.True(behind.Hp < behind.MaxHp, "Lv3 꼬리(60+3×14=102px)가 뒤 몹을 꿰뚫어야");
        }

        [Fact]
        public void Hose_Lv6_BeamSweepsPlusMinus1_1RadAroundCrowd()
        {
            var sim = Bare();
            To(sim, UpgradeId.Hose, 6);
            var h = Item<HoseItem>(sim, UpgradeId.Hose);
            var keep = new HashSet<Enemy>();
            Enemy e = Mob(sim, 150f, -30f, keep);
            float lo = 9f, hi = -9f;
            Run(sim, 2.2f, keep, () =>
            {
                float bas = (float)Math.Atan2(SurvivorSim.SY(e.Pos) - sim.PY, SurvivorSim.SX(e.Pos) - sim.PX);
                float d = h.A - bas;
                lo = Math.Min(lo, d);
                hi = Math.Max(hi, d);
                SurvivorSim.SetS(e, sim.PX + 150f, sim.PY - 30f);
                return false;
            });
            Assert.True(h.HasA);
            Assert.InRange(hi, 1.05f, 1.1f + 1e-3f);
            Assert.InRange(lo, -1.1f - 1e-3f, -1.05f);
        }

        // ---------------------------------------------------------------- 회전 스프링클러
        [Theory]
        [InlineData(1, 2, 44f)]
        [InlineData(2, 2, 52f)]
        [InlineData(3, 3, 60f)]
        [InlineData(4, 3, 68f)]
        [InlineData(5, 4, 76f)]
        [InlineData(6, 4, 80f)]
        public void Sprinkler_HeadsAndRadius_PerLevel(int lv, int heads, float R)
        {
            var sim = Bare();
            To(sim, UpgradeId.Sprinkler, lv);
            var sp = Item<SprinklerItem>(sim, UpgradeId.Sprinkler);
            Run(sim, 0.5f);
            Assert.Equal(heads, sp.Heads.Count);
            foreach (SprinklerItem.Head hd in sp.Heads) Assert.Equal(R, SurvivorSim.Hypot(hd.X - sim.PX, (hd.Y - sim.PY) / 0.72f), 1);
            Assert.Equal(lv >= 3 && lv < 6, sp.Heads[0].HasJet);
        }

        [Fact]
        public void Sprinkler_Lv1_KnocksMobOnRing()
        {
            var sim = Bare();
            To(sim, UpgradeId.Sprinkler, 1);
            var keep = new HashSet<Enemy>();
            Enemy e = Mob(sim, 44f, 0f, keep);
            Run(sim, 2f, keep);
            Assert.True(e.Hp < e.MaxHp, "44px 머리 고리 위 몹을 쳐야");
        }

        // ---------------------------------------------------------------- 물풍선
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void Balloon_VolleyCountBouncesAndCooldown(int lv)
        {
            var sim = Bare();
            To(sim, UpgradeId.Balloon, lv);
            var b = Item<BalloonItem>(sim, UpgradeId.Balloon);
            Run(sim, SurvivorSim.Dt);
            int n = new[] { 0, 1, 2, 2, 3, 3 }[lv];
            Assert.Equal(n, b.B.Count);
            Assert.All(b.B, o => Assert.Equal(new[] { 0, 4, 5, 6, 7, 8 }[lv], o.Max));
            float cd = new[] { 0f, 1.5f, 1.4f, 1.25f, 1.1f, 1f }[lv];
            Run(sim, cd - 0.05f);
            Assert.Equal(n, b.B.Count);
            Run(sim, 0.1f);
            Assert.Equal(2 * n, b.B.Count);
        }

        [Fact]
        public void Balloon_BurstsAfterMaxBounces()
        {
            var sim = Bare();
            To(sim, UpgradeId.Balloon, 1);
            var b = Item<BalloonItem>(sim, UpgradeId.Balloon);
            Run(sim, SurvivorSim.Dt);
            BalloonItem.Bal first = b.B[0];
            Run(sim, 12f, null, () => !b.B.Contains(first));
            Assert.DoesNotContain(first, b.B);
            Assert.Equal(4, first.B);
            Assert.NotEmpty(b.Shreds);
        }

        [Fact]
        public void Balloon_Lv6_RainAtLeast15PerSecond()
        {
            var sim = Bare();
            To(sim, UpgradeId.Balloon, 6);
            var b = Item<BalloonItem>(sim, UpgradeId.Balloon);
            var keep = new HashSet<Enemy>();
            for (int i = 0; i < 4; i++) Mob(sim, -90f + (i * 60f), -60f, keep);
            Run(sim, 1f, keep);
            int before = b.RainBursts;
            Run(sim, 1f, keep);
            Assert.True(b.RainBursts - before >= 15, "폭우(0.06초마다 하나)는 초당 15개 이상: " + (b.RainBursts - before));
        }

        // ---------------------------------------------------------------- 소화기 부메랑
        [Theory]
        [InlineData(1, 1)]
        [InlineData(2, 1)]
        [InlineData(3, 2)]
        [InlineData(4, 2)]
        [InlineData(5, 3)]
        [InlineData(6, 4)]
        public void Extinguisher_VolleyCount(int lv, int n)
        {
            var sim = Bare();
            To(sim, UpgradeId.Extinguisher, lv);
            var e = Item<ExtinguisherItem>(sim, UpgradeId.Extinguisher);
            Run(sim, SurvivorSim.Dt);
            Assert.Equal(n, e.B.Count);
        }

        [Fact]
        public void Extinguisher_FlysFigureEight_AndReturns()
        {
            var sim = Bare();
            To(sim, UpgradeId.Extinguisher, 1);
            var e = Item<ExtinguisherItem>(sim, UpgradeId.Extinguisher);
            Run(sim, SurvivorSim.Dt);
            ExtinguisherItem.Ext b = e.B[0];
            Assert.Equal(-1.4f, b.A, 3);
            float maxOut = 0f, maxSide = 0f, minSide = 0f;
            Run(sim, 1.3f, null, () =>
            {
                if (!e.B.Contains(b)) return true;
                float dx = b.X - sim.PX, dy = b.Y - (sim.PY - 6f);
                float o = (dx * (float)Math.Cos(b.A)) + (dy * (float)Math.Sin(b.A));
                float side = (-dx * (float)Math.Sin(b.A)) + (dy * (float)Math.Cos(b.A));
                maxOut = Math.Max(maxOut, o);
                maxSide = Math.Max(maxSide, side);
                minSide = Math.Min(minSide, side);
                return false;
            });
            Assert.DoesNotContain(b, e.B);
            Assert.InRange(maxOut, 100f, 105.01f);
            Assert.True(maxSide > 30f && minSide < -30f, "8자: 옆으로 양쪽 R×.35(≈37px)까지: " + minSide + "~" + maxSide);
        }

        [Fact]
        public void Extinguisher_Lv6_TornadoLaunchesMobs()
        {
            var sim = Bare();
            To(sim, UpgradeId.Extinguisher, 6);
            var e = Item<ExtinguisherItem>(sim, UpgradeId.Extinguisher);
            Assert.NotNull(e.Tw);
            var keep = new HashSet<Enemy>();
            var mobs = new List<Enemy>();
            for (int i = 0; i < 4; i++) mobs.Add(Mob(sim, 40f + (i * 8f), -40f, keep));
            bool launched = false;
            Run(sim, 2f, keep, () => launched = mobs.Any(m => m.AirZ > 0f));
            Assert.True(launched, "회오리 44px 안 몹은 하늘로 뜬다");
        }

        // ---------------------------------------------------------------- 액체질소 지뢰
        [Theory]
        [InlineData(1, 2)]
        [InlineData(2, 3)]
        [InlineData(3, 4)]
        [InlineData(4, 5)]
        [InlineData(5, 6)]
        [InlineData(6, 9)]
        public void Mine_MaxCount_PerLevel(int lv, int max)
        {
            var sim = Bare();
            To(sim, UpgradeId.Mine, lv);
            var m = Item<MineItem>(sim, UpgradeId.Mine);
            Run(sim, 6f);
            Assert.Equal(max, m.Mines.Count);
        }

        [Fact]
        public void Mine_Lv1_TriggersOnce_AndIsGone()
        {
            var sim = Bare();
            To(sim, UpgradeId.Mine, 1);
            var m = Item<MineItem>(sim, UpgradeId.Mine);
            var keep = new HashSet<Enemy>();
            Run(sim, 0.4f, keep);
            Assert.Single(m.Mines);
            Enemy e = Mob(sim, 0f, 6f, keep);
            Run(sim, SurvivorSim.Dt, keep);
            Assert.Equal(1, m.Triggers);
            Assert.Empty(m.Mines);
            Assert.True(e.SFrozen > 0f);
        }

        [Fact]
        public void Mine_Lv6_PersistsAndRetriggers()
        {
            var sim = Bare();
            To(sim, UpgradeId.Mine, 6);
            var m = Item<MineItem>(sim, UpgradeId.Mine);
            var keep = new HashSet<Enemy>();
            Run(sim, 0.4f, keep);
            int mines = m.Mines.Count;
            Assert.True(mines >= 1);
            Enemy e1 = Mob(sim, 0f, 6f, keep);
            Run(sim, SurvivorSim.Dt, keep);
            Assert.Equal(1, m.Triggers);
            Assert.True(e1.SFrozen > 0f);
            Assert.True(m.Mines.Count >= mines, "Lv6 지뢰는 터져도 남는다");
            // 첫 몹이 깨져 사라진 뒤 새 몹이 밟으면 같은 지뢰가 다시 터진다.
            Run(sim, 1.2f, keep);
            Assert.True(e1.Dead);
            keep.Remove(e1);
            Mob(sim, 0f, 6f, keep);
            Run(sim, SurvivorSim.Dt, keep);
            Assert.Equal(2, m.Triggers);
        }
    }
}
