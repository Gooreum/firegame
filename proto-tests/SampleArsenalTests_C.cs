using System.Collections.Generic;
using System.Linq;
using FireGame.Core.Sim;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 숲 샘플 아이템 C(items-c.js 그대로): 비눗방울·맨홀 간헐천·물 사슬의 샘플 수치(거품은 숲에서 뺐다).
    /// 아이템을 직접 돌려(Update) 다른 아이템·감독 스폰과 섞이지 않게 잰다.
    /// </summary>
    public class SampleArsenalTests_C
    {
        private const int Forest = 2;
        private const float Dt = SurvivorSim.Dt;

        private static SurvivorSim Quiet()
        {
            var sim = new SurvivorSim(1, Forest) { Guardian = false };
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        private static T Make<T>(SurvivorSim sim, UpgradeId id) where T : SampleItem
        {
            SampleItem it = SampleItemsRegistry.Make(id);
            Assert.IsType<T>(it);
            it.Setup(sim);
            return (T)it;
        }

        /// <summary>소방관 기준 샘플 px(dx, dy)에 서 있는 몹(움직이지 않고 질기게).</summary>
        private static Enemy Mob(SurvivorSim sim, float dx, float dy, EnemyKind kind = EnemyKind.Ember, bool tough = false)
        {
            Enemy e = sim.Spawn(kind, SurvivorSim.FromS(sim.PX + dx, sim.PY + dy));
            e.Speed = 0f;
            if (tough) e.MaxHp = e.Hp = 1e5f;
            return e;
        }

        private static void Tick(SurvivorSim sim, SampleItem it, int lv, float seconds)
        {
            int n = (int)System.Math.Round(seconds / Dt);
            for (int i = 0; i < n; i++) it.Update(sim, Dt, lv);
        }

        // ------------------------------------------------------------ 등록
        [Fact]
        public void Registry_MakesItemC()
        {
            Assert.Null(SampleItemsRegistry.Make(UpgradeId.Foam));
            Assert.IsType<SampleBubbleItem>(SampleItemsRegistry.Make(UpgradeId.Bubble));
            Assert.IsType<SampleManholeItem>(SampleItemsRegistry.Make(UpgradeId.Manhole));
            Assert.IsType<SampleChainItem>(SampleItemsRegistry.Make(UpgradeId.Chain));
            var sim = Quiet();
            sim.PendingChoices = new List<UpgradeId> { UpgradeId.Chain };
            sim.Choose(0);
            Assert.IsType<SampleChainItem>(sim.SampleItemOf(UpgradeId.Chain));
        }

        // ------------------------------------------------------------ 비눗방울
        [Theory]
        [InlineData(1, 1, 0.9f)]
        [InlineData(2, 2, 0.7f)]
        [InlineData(3, 3, 0.7f)]
        [InlineData(4, 3, 0.5f)]
        [InlineData(5, 4, 0.42f)]
        [InlineData(6, 5, 0.3f)]
        public void Bubble_ShotsAndCooldown_PerLevel(int lv, int n, float cd)
        {
            var sim = Quiet();
            var it = Make<SampleBubbleItem>(sim, UpgradeId.Bubble);
            for (int i = 0; i < 8; i++) Mob(sim, 150f + (i * 8f), -60f + (i * 15f));
            Tick(sim, it, lv, 0.32f);
            Assert.Equal(n, it.Shots.Count);
            Assert.Equal(n, it.Shots.Select(s => s.M).Distinct().Count());
            Assert.InRange(it.Cd, cd - 0.04f, cd);
        }

        [Fact]
        public void Bubble_Capture_Lifts1s_ThenPops()
        {
            var sim = Quiet();
            var it = Make<SampleBubbleItem>(sim, UpgradeId.Bubble);
            Enemy e = Mob(sim, 60f, 0f);
            Tick(sim, it, 1, 0.3f);
            for (int i = 0; i < 60 && it.Caps.Count == 0; i++) it.Update(sim, Dt, 1);
            SampleBubbleItem.Cap c = Assert.Single(it.Caps);
            Assert.True(e.Held);
            Tick(sim, it, 1, 0.5f);
            Assert.InRange(e.AirZ, 25f, 30f);
            Assert.False(e.Dead);
            Tick(sim, it, 1, 0.45f);
            Assert.Equal(46f, e.AirZ, 1);
            Tick(sim, it, 1, 0.1f);
            Assert.True(e.Dead, "1초 뒤 방울이 터지며 처치");
            Assert.False(e.Held);
        }

        [Fact]
        public void Bubble_BigMob_Lv1_JustHit_Lv3_Captured()
        {
            var sim = Quiet();
            var it = Make<SampleBubbleItem>(sim, UpgradeId.Bubble);
            Enemy big = Mob(sim, 60f, 0f, EnemyKind.Blaze, tough: true);
            Assert.True(SurvivorSim.SBig(big));
            Tick(sim, it, 1, 0.6f);
            Assert.False(big.Held);
            Assert.True(big.Hp < 1e5f);

            var sim3 = Quiet();
            var it3 = Make<SampleBubbleItem>(sim3, UpgradeId.Bubble);
            Enemy big3 = Mob(sim3, 60f, 0f, EnemyKind.Blaze, tough: true);
            Tick(sim3, it3, 3, 0.6f);
            Assert.True(big3.Held);
        }

        [Fact]
        public void Bubble_Lv6_GiantBubble_GrowsPerMob_BurstsEvery1_6s()
        {
            var sim = Quiet();
            var it = Make<SampleBubbleItem>(sim, UpgradeId.Bubble);
            it.OnLevel(sim, 6);
            Assert.NotNull(it.Big);
            Assert.Equal(16f, it.Big.R);
            Assert.Equal(sim.PX + 100f, it.Big.X, 1);
            Assert.Equal(sim.PY - 106f, it.Big.Y, 1);
            var mobs = new List<Enemy>();
            for (int i = 0; i < 3; i++) mobs.Add(Mob(sim, 30f + (i * 20f), 10f));
            SampleBubbleItem.BigBubble first = it.Big;
            for (int i = 0; i < (int)(1.55f / Dt); i++) it.Update(sim, Dt, 6);
            Assert.Same(first, it.Big);
            Assert.Equal(3, first.N);
            Assert.Equal(16f + (3 * 2.2f), first.R, 3);
            Assert.All(mobs, m => Assert.True(m.Held && m.SHide && !m.Dead, "거대 방울 속에 갇혀 있다"));
            Tick(sim, it, 6, 0.1f);
            Assert.NotSame(first, it.Big);
            Assert.NotNull(it.F);
            Assert.All(mobs, m => Assert.True(m.Dead, "물폭포로 터진다"));
            Assert.Equal(16f, it.Big.R);

            // 최대 56
            var b = new SampleBubbleItem.BigBubble { R = 55f };
            Assert.Equal(56f, System.Math.Min(56f, b.R + 2.2f));
        }

        // ------------------------------------------------------------ 맨홀 간헐천
        private static SampleManholeItem.Hole NearestHole(SurvivorSim sim, SampleManholeItem it)
        {
            return it.ScreenHoles(sim).OrderBy(h => SurvivorSim.Hypot(h.X - sim.PX, h.Y - sim.PY)).First();
        }

        [Fact]
        public void Manhole_HolesAreFixedInWorld()
        {
            var a = new List<SampleManholeItem.Hole>();
            var b = new List<SampleManholeItem.Hole>();
            SampleManholeItem.HolesIn(0f, 0f, 480f, 270f, a);
            SampleManholeItem.HolesIn(-100f, -50f, 600f, 400f, b);
            Assert.InRange(a.Count, 8, 15);
            Assert.All(a, h => Assert.Contains(h, b));
        }

        [Theory]
        [InlineData(1, 1, 1f, 1.4f)]
        [InlineData(2, 2, 1.1f, 1.2f)]
        [InlineData(3, 2, 1.3f, 0.95f)]
        [InlineData(4, 3, 1.45f, 0.85f)]
        [InlineData(5, 4, 1.8f, 0.6f)]
        public void Manhole_CountPowerCooldown_PerLevel(int lv, int n, float p, float cd)
        {
            var sim = Quiet();
            var it = Make<SampleManholeItem>(sim, UpgradeId.Manhole);
            // 화면 안 모든 맨홀 위에 몹 한 마리씩: 점수 > 0인 맨홀이 넉넉하다.
            foreach (SampleManholeItem.Hole h in it.ScreenHoles(sim).ToList()) sim.Spawn(EnemyKind.Ember, SurvivorSim.FromS(h.X + 5f, h.Y)).Speed = 0f;
            Tick(sim, it, lv, 0.6f);
            Assert.Equal(n, it.G.Count);
            Assert.All(it.G, g => Assert.Equal(p, g.P, 3));
            Assert.All(it.G, g => Assert.InRange(g.K, -0.4f, -0.38f));
            Assert.InRange(it.Cd, cd - 0.04f, cd);
        }

        [Fact]
        public void Manhole_NoMobs_NoGeyser()
        {
            var sim = Quiet();
            var it = Make<SampleManholeItem>(sim, UpgradeId.Manhole);
            Tick(sim, it, 3, 0.6f);
            Assert.Empty(it.G);
        }

        [Fact]
        public void Manhole_Geyser_Warns0_4_Launches_Stands0_8()
        {
            var sim = Quiet();
            var it = Make<SampleManholeItem>(sim, UpgradeId.Manhole);
            SampleManholeItem.Hole h = NearestHole(sim, it);
            Enemy e = sim.Spawn(EnemyKind.Ember, SurvivorSim.FromS(h.X + 10f, h.Y + 5f));
            e.Speed = 0f;
            for (int i = 0; i < 60 && it.G.Count == 0; i++) it.Update(sim, Dt, 1);
            SampleManholeItem.Geyser g = Assert.Single(it.G);
            Tick(sim, it, 1, 0.35f);
            Assert.False(e.Launched);
            Tick(sim, it, 1, 0.05f);
            Assert.True(e.Launched && e.AirZ > 0f, "물기둥이 솟으면 몹이 하늘로");
            Assert.InRange(e.AirVz, 330f, 440f);
            Tick(sim, it, 1, 0.7f);
            Assert.Contains(g, it.G);
            Tick(sim, it, 1, 0.1f);
            Assert.DoesNotContain(g, it.G);
        }

        [Fact]
        public void Manhole_Launched_DiesOnLanding()
        {
            var sim = Quiet();
            Enemy e = sim.Spawn(EnemyKind.Ember, new Vec2(sim.Player.X + 3f, sim.Player.Y));
            e.Speed = 0f;
            sim.SLaunch(e, 400f, 0f, 0f);
            for (int i = 0; i < 120 && !e.Dead; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
            }
            Assert.True(e.Dead);
        }

        [Fact]
        public void Manhole_Lv6_Crack_TwoLines_NinePoints_Domino()
        {
            var sim = Quiet();
            var it = Make<SampleManholeItem>(sim, UpgradeId.Manhole);
            Tick(sim, it, 6, 0.4f);
            Assert.Equal(2, it.Cracks.Count);
            Assert.All(it.Cracks, c => Assert.InRange(c.Pts.Count, 1, 9));
            // 도미노: 같은 금의 물기둥은 0.22 + i·0.08초 뒤에 차례로 솟는다.
            var crackG = it.G.Where(g => g.P == 1.15f && g.Lv == 6).OrderByDescending(g => g.K).ToList();
            Assert.Equal(it.Cracks.Sum(c => c.Pts.Count), crackG.Count);
            Assert.Contains(crackG, g => System.Math.Abs(g.Warn - 0.22f) < 1e-4f);
            Assert.All(crackG, g => Assert.True(((g.Warn - 0.22f) / 0.08f) % 1f < 1e-3f || ((g.Warn - 0.22f) / 0.08f) % 1f > 0.999f));
            Assert.Equal(1.1f, it.Ccd, 2);
            // 한쪽 몹 떼 쪽으로 9칸(30px 간격): 화면이 넓으면 9점이 다 선다.
            var sim2 = Quiet();
            var it2 = Make<SampleManholeItem>(sim2, UpgradeId.Manhole);
            Tick(sim2, it2, 6, 0.4f);
            Assert.Contains(it2.Cracks, c => c.Pts.Count >= 2);
            foreach (SampleManholeItem.Crack c in it2.Cracks)
            {
                for (int i = 1; i < c.Pts.Count; i++) Assert.Equal(30f, SurvivorSim.Hypot(c.Pts[i].X - c.Pts[i - 1].X, c.Pts[i].Y - c.Pts[i - 1].Y), 2);
            }
        }

        // ------------------------------------------------------------ 물 사슬
        [Theory]
        [InlineData(1, 1, 3, 0.95f)]
        [InlineData(2, 1, 4, 0.9f)]
        [InlineData(3, 2, 4, 0.85f)]
        [InlineData(4, 2, 5, 0.75f)]
        [InlineData(5, 2, 6, 0.65f)]
        [InlineData(6, 3, 8, 0.5f)]
        public void Chain_HopsStreamsCooldown_PerLevel(int lv, int streams, int hops, float cd)
        {
            var sim = Quiet();
            var it = Make<SampleChainItem>(sim, UpgradeId.Chain);
            // 40px 간격 격자 60마리(100px 안에서 튄다)
            var mobs = new List<Enemy>();
            for (int i = 0; i < 60; i++) mobs.Add(Mob(sim, -210f + ((i % 10) * 40f), -100f + ((i / 10) * 40f), EnemyKind.Ember, tough: true));
            Tick(sim, it, lv, 0.32f);
            Assert.Equal(streams, it.Bolts.Count);
            Assert.All(it.Bolts, b => Assert.InRange(b.Segs.Count, 1, hops));
            Assert.All(it.Bolts, b => Assert.All(b.Segs, s => Assert.Equal(7, s.Count)));
            Assert.InRange(it.Cd, cd - 0.04f, cd);
            Assert.Equal(hops, it.Bolts[0].Segs.Count);
            if (lv < 5) Assert.Equal(streams * hops, mobs.Count(m => m.Hp < 1e5f));
        }

        [Theory]
        [InlineData(1, 3f)]
        [InlineData(3, 3.8f)]
        [InlineData(4, 4.2f)]
        public void Chain_Damage_PerLevel(int lv, float dmg)
        {
            var sim = Quiet();
            var it = Make<SampleChainItem>(sim, UpgradeId.Chain);
            Enemy e = Mob(sim, 50f, 0f, EnemyKind.Ember, tough: true);
            Tick(sim, it, lv, 0.32f);
            float lost = 1e5f - e.Hp;
            float unit = dmg * sim.SDamageScale;
            Assert.True(System.Math.Abs(lost - unit) < unit * 0.01f || System.Math.Abs(lost - (unit * 2f)) < unit * 0.01f, "샘플 피해 " + dmg + " (치명 2배 허용): " + lost / sim.SDamageScale);
        }

        [Fact]
        public void Chain_Lv6_SplashAtEveryVertex()
        {
            var sim = Quiet();
            var it = Make<SampleChainItem>(sim, UpgradeId.Chain);
            for (int i = 0; i < 4; i++) Mob(sim, 50f + (i * 70f), 0f, EnemyKind.Ember, tough: true);
            // 꼭짓점마다 34px 물 폭발: 사슬이 닿지 않은(꼭짓점 옆 20px) 몹도 맞는다.
            Enemy side = Mob(sim, 50f, 25f, EnemyKind.Ember, tough: true);
            Enemy side2 = Mob(sim, 190f, 25f, EnemyKind.Ember, tough: true);
            int prisms0 = sim.Parts.Count(p => p.Kind == SPartKind.Prism && p.Size == 34f);
            Tick(sim, it, 6, 0.32f);
            Assert.True(side.Hp < 1e5f && side2.Hp < 1e5f);
            int prisms = sim.Parts.Count(p => p.Kind == SPartKind.Prism && p.Size == 34f) - prisms0;
            int vertices = it.Bolts.Sum(b => b.Segs.Count);
            Assert.Equal(vertices, prisms);
        }

        [Fact]
        public void Chain_Lv5_SplashOnlyAtEnd()
        {
            var sim = Quiet();
            var it = Make<SampleChainItem>(sim, UpgradeId.Chain);
            for (int i = 0; i < 3; i++) Mob(sim, 50f + (i * 70f), 0f, EnemyKind.Ember, tough: true);
            Enemy nearFirst = Mob(sim, 50f, 22f, EnemyKind.Ember, tough: true);
            Enemy nearLast = Mob(sim, 190f, 22f, EnemyKind.Ember, tough: true);
            Tick(sim, it, 5, 0.32f);
            // 사슬이 6마리까지 튀니 옆 몹도 사슬로 맞을 수 있다 → 프리즘 없음만 본다(Lv5는 끝 물 폭발, Lv6 꼭짓점 프리즘 없음).
            Assert.DoesNotContain(sim.Parts, p => p.Kind == SPartKind.Prism && p.Size == 34f);
        }

        [Fact]
        public void Chain_BoltsFadeAfter0_45s()
        {
            var sim = Quiet();
            var it = Make<SampleChainItem>(sim, UpgradeId.Chain);
            Mob(sim, 50f, 0f, EnemyKind.Ember, tough: true);
            for (int i = 0; i < 30 && it.Bolts.Count == 0; i++) it.Update(sim, Dt, 1);
            Assert.Single(it.Bolts);
            Tick(sim, it, 1, 0.42f);
            Assert.Single(it.Bolts);
            Tick(sim, it, 1, 0.05f);
            Assert.Empty(it.Bolts);
        }
    }
}
