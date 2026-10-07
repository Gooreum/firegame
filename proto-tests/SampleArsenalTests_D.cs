using System.Collections.Generic;
using FireGame.Core.Sim;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 숲 샘플 아이템 D(items-d.js 그대로): 호스 채찍 · 고압 펌프 · 장화 · 방화복의 레벨별 수치.
    /// 무기 없이 시작해(호스 없음) 아이템 하나만 든 채 시험한다.
    /// </summary>
    public class SampleArsenalTests_D
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

        /// <summary>돌린다: 감독이 낸 몹은 지우고(keep만 남긴다) 체력은 가득.</summary>
        private static void Run(SurvivorSim sim, float seconds, HashSet<Enemy> keep = null, float mx = 0f, float my = 0f, bool keepHp = true)
        {
            int ticks = (int)System.Math.Round(seconds / SurvivorSim.Dt);
            for (int i = 0; i < ticks; i++)
            {
                sim.Enemies.RemoveAll(e => keep == null || !keep.Contains(e));
                if (keepHp) sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(mx, my);
            }
        }

        /// <summary>소방관에서 샘플 px(dx, dy)만큼 떨어진 곳에 멈춘 튼튼한 몹.</summary>
        private static Enemy Mob(SurvivorSim sim, float dx, float dy, HashSet<Enemy> keep)
        {
            Vec2 at = SurvivorSim.FromS(sim.PX + dx, sim.PY + dy);
            Enemy e = sim.Spawn(EnemyKind.Ember, at);
            e.Speed = 0f;
            e.MaxHp = e.Hp = 1e6f;
            keep.Add(e);
            return e;
        }

        private static T Item<T>(SurvivorSim sim, UpgradeId id) where T : SampleItem
        {
            return Assert.IsType<T>(sim.SampleItemOf(id));
        }

        // ---------------------------------------------------------------- 호스 채찍
        [Theory]
        [InlineData(1, 1, 46f, 3.6f)]
        [InlineData(2, 1, 56f, 4.4f)]
        [InlineData(3, 2, 64f, 5.2f)]
        [InlineData(4, 2, 72f, 6.2f)]
        [InlineData(5, 3, 82f, 6.8f)]
        [InlineData(6, 3, 86f, 7.2f)]
        public void Whip_ArmsRadiusSpin_PerLevel(int lv, int arms, float rad, float spin)
        {
            var sim = Bare();
            To(sim, UpgradeId.Whip, lv);
            var w = Item<SampleWhip>(sim, UpgradeId.Whip);
            Run(sim, 1f);
            Assert.Equal(arms, SampleWhip.Arms[lv]);
            Assert.Equal(rad, SampleWhip.Rad[lv]);
            float a0 = w.A;
            Run(sim, 0.5f);
            Assert.Equal(spin * 0.5f, w.A - a0, 2);
            // 갈래마다 끝이 반지름 R(세로 .75) 자리에 있다.
            for (int i = 0; i < arms; i++)
            {
                SamplePtD tip = w.Trail[i][0];
                float dx = tip.X - sim.PX, dy = (tip.Y - (sim.PY - 4f)) / 0.75f;
                Assert.Equal(w.RadiusAt(lv), SurvivorSim.Hypot(dx, dy), 1);
            }
            Assert.Equal(10 + (lv * 2), w.Trail[0].Count);
        }

        [Fact]
        public void Whip_HitsMobOnArc()
        {
            var sim = Bare();
            To(sim, UpgradeId.Whip, 1);
            var keep = new HashSet<Enemy>();
            Run(sim, 1f);
            Enemy e = Mob(sim, 40f, -4f, keep);
            Run(sim, 2f, keep);
            Assert.True(e.Hp < e.MaxHp, "46px 채찍이 40px 몹을 쳐야");
        }

        [Fact]
        public void Whip_Lv6_LeavesSpinningRings()
        {
            var sim = Bare();
            To(sim, UpgradeId.Whip, 6);
            var w = Item<SampleWhip>(sim, UpgradeId.Whip);
            Run(sim, 1f);
            Assert.True(w.Rings.Count > 10, "0.15초마다 세 갈래 고리: " + w.Rings.Count);
            Assert.True(w.Rings.Count <= 40);
            Assert.All(w.Rings, r => Assert.True(r.K < 2.6f));

            var sim5 = Bare();
            To(sim5, UpgradeId.Whip, 5);
            Run(sim5, 1f);
            Assert.Empty(Item<SampleWhip>(sim5, UpgradeId.Whip).Rings);
        }

        // ---------------------------------------------------------------- 고압 펌프
        [Theory]
        [InlineData(3, 1)]
        [InlineData(4, 2)]
        [InlineData(6, 2)]
        public void Tank_StreamsOneThenTwoAtLv4(int lv, int streams)
        {
            var sim = Bare();
            To(sim, UpgradeId.Tank, lv);
            Run(sim, 1.5f);
            var keep = new HashSet<Enemy>();
            Mob(sim, 60f, 0f, keep);
            Mob(sim, -70f, 0f, keep);
            Mob(sim, 0f, 80f, keep);
            Run(sim, 0.1f, keep);
            Assert.Equal(streams, Item<SampleTank>(sim, UpgradeId.Tank).Targets.Count);
        }

        [Fact]
        public void Tank_Range_130Plus32PerLevel()
        {
            var sim = Bare();
            To(sim, UpgradeId.Tank, 2);
            Run(sim, 1f);
            float range = SampleTank.RangeOf(2);
            Assert.Equal(194f, range);
            var keep = new HashSet<Enemy>();
            Enemy far = Mob(sim, range + 6f, 0f, keep);
            Run(sim, 0.1f, keep);
            Assert.Empty(Item<SampleTank>(sim, UpgradeId.Tank).Targets);
            far.Pos = SurvivorSim.FromS(sim.PX + range - 6f, sim.PY);
            Run(sim, SurvivorSim.Dt, keep);
            Assert.Contains(far, Item<SampleTank>(sim, UpgradeId.Tank).Targets);
            Run(sim, 0.25f, keep);
            Assert.True(far.Hp < far.MaxHp);
        }

        [Fact]
        public void Tank_Lv6_NovaEvery2_2s_Hits140px()
        {
            var sim = Bare();
            To(sim, UpgradeId.Tank, 6);
            var t = Item<SampleTank>(sim, UpgradeId.Tank);
            // 레벨업 폭발(0.55초)이 지나간 뒤 몹을 둔다. 첫 대폭발은 Lv6 1.2초 뒤.
            Run(sim, 0.9f);
            Assert.Equal(0, t.Novas);
            var keep = new HashSet<Enemy>();
            // 물줄기는 오른쪽 가까운 두 몹을 겨눈다. 왼쪽 130px·170px 몹은 대폭발만 닿는다.
            Mob(sim, 20f, 0f, keep);
            Mob(sim, 24f, 10f, keep);
            Enemy inside = Mob(sim, -126f, 4f, keep);
            Enemy outside = Mob(sim, -170f, 4f, keep);
            // 대폭발 틱의 피해만 잰다(물줄기 피해 1틱분보다 대폭발 12가 훨씬 크다).
            float nova = 12f * sim.SDamageScale;
            for (int i = 0; i < 60 && t.Novas == 0; i++)
            {
                float hi = inside.Hp, ho = outside.Hp;
                Run(sim, SurvivorSim.Dt, keep);
                if (t.Novas == 1)
                {
                    Assert.True(hi - inside.Hp >= nova * 0.99f, "140px 안 몹은 대폭발에 맞아야");
                    Assert.True(ho - outside.Hp < nova * 0.5f, "170px 몹은 대폭발 밖");
                }
            }
            Assert.Equal(1, t.Novas);
            Run(sim, (2.2f * 2f) + 0.1f, keep);
            Assert.Equal(3, t.Novas);
        }

        // ---------------------------------------------------------------- 장화
        [Fact]
        public void Boots_SpeedTable_SetsSampleSpeed()
        {
            float[] want = { 0f, 1f, 1.25f, 1.5f, 1.8f, 2.1f, 2.6f };
            var sim = Bare();
            for (int lv = 1; lv <= 6; lv++)
            {
                To(sim, UpgradeId.Boots, lv);
                Run(sim, 1f);
                Assert.Equal(want[lv], sim.SampleSpeed, 3);
            }
        }

        [Fact]
        public void Boots_Lv6_RunsFarther_ThanNoBoots()
        {
            var a = Bare();
            var b = Bare();
            To(b, UpgradeId.Boots, 6);
            Run(b, 1f);
            Vec2 a0 = a.Player, b0 = b.Player;
            Run(a, 0.5f, null, 1f, 0f);
            Run(b, 0.5f, null, 1f, 0f);
            float da = a.Player.X - a0.X, db = b.Player.X - b0.X;
            Assert.True(db > da * 2.4f, "제트 장화 2.6배: " + da + " vs " + db);
        }

        [Theory]
        [InlineData(1, 15f)]
        [InlineData(3, 19f)]
        [InlineData(6, 30f)]
        public void Boots_BodyBump_Radius(int lv, float r)
        {
            var sim = Bare();
            To(sim, UpgradeId.Boots, lv);
            Run(sim, 1f);
            Assert.Equal(r, SampleBoots.BumpR[lv]);
            var keep = new HashSet<Enemy>();
            Enemy near = Mob(sim, 0f, -(r + 10f), keep);
            Enemy far = Mob(sim, 0f, r + 18f, keep);
            float rr = SurvivorSim.SR(near);
            near.Pos = SurvivorSim.FromS(sim.PX, sim.PY - (r + rr - 2f));
            far.Pos = SurvivorSim.FromS(sim.PX, sim.PY + r + rr + 3f);
            Run(sim, 0.1f, keep);
            Assert.True(near.Hp < near.MaxHp, "몸에 닿은 몹은 튕겨야");
            Assert.Equal(far.MaxHp, far.Hp);
        }

        [Fact]
        public void Boots_Lv6_LeavesTrailWhileRunning_FadesIn1_6s()
        {
            var sim = Bare();
            To(sim, UpgradeId.Boots, 6);
            var b = Item<SampleBoots>(sim, UpgradeId.Boots);
            Run(sim, 2f);
            Assert.Empty(b.Trail);
            Run(sim, 0.5f, null, 1f, 0f);
            Assert.InRange(b.Trail.Count, 15, 25);
            Run(sim, 1.7f);
            Assert.Empty(b.Trail);

            var sim5 = Bare();
            To(sim5, UpgradeId.Boots, 5);
            Run(sim5, 0.5f, null, 1f, 0f);
            Assert.Empty(Item<SampleBoots>(sim5, UpgradeId.Boots).Trail);
        }

        // ---------------------------------------------------------------- 방화복
        [Fact]
        public void Suit_ShieldRadiusTable_AndContact()
        {
            float[] want = { 0f, 19f, 22f, 25f, 28f, 32f, 46f };
            for (int lv = 1; lv <= 6; lv++) Assert.Equal(want[lv], SampleSuit.Rad[lv]);
            var sim = Bare();
            To(sim, UpgradeId.Suit, 4);
            Run(sim, 1f);
            var keep = new HashSet<Enemy>();
            Enemy near = Mob(sim, 0f, 0f, keep);
            float rr = SurvivorSim.SR(near);
            near.Pos = SurvivorSim.FromS(sim.PX + 28f + rr - 2f, sim.PY);
            Run(sim, 0.05f, keep);
            Assert.True(near.Hp < near.MaxHp);
            Assert.True(Item<SampleSuit>(sim, UpgradeId.Suit).Shield > 0f);
        }

        [Theory]
        [InlineData(3, 1.5f)]
        [InlineData(4, 1.15f)]
        [InlineData(5, 0.85f)]
        public void Suit_RippleInterval_Lv3to5(int lv, float every)
        {
            var sim = Bare();
            To(sim, UpgradeId.Suit, lv);
            var s = Item<SampleSuit>(sim, UpgradeId.Suit);
            int n0 = s.Pulses;
            int ticks = 0;
            while (s.Pulses == n0 && ticks++ < 1000) Run(sim, SurvivorSim.Dt);
            float t0 = sim.ST;
            n0 = s.Pulses;
            ticks = 0;
            while (s.Pulses == n0 && ticks++ < 1000) Run(sim, SurvivorSim.Dt);
            Assert.InRange(sim.ST - t0, every - 0.02f, every + 0.02f);
        }

        [Fact]
        public void Suit_Lv1to2_NoRipple()
        {
            var sim = Bare();
            To(sim, UpgradeId.Suit, 2);
            Run(sim, 3f);
            Assert.Equal(0, Item<SampleSuit>(sim, UpgradeId.Suit).Pulses);
        }

        [Fact]
        public void Suit_ReducesFireDamage_PerLevel()
        {
            var sim = Bare();
            float last = float.MaxValue;
            for (int lv = 1; lv <= 5; lv++)
            {
                To(sim, UpgradeId.Suit, lv);
                Run(sim, 0.1f);
                float total = sim.SampleHurt * sim.Build.HeatScale;
                Assert.Equal(SampleSuit.HurtT[lv] / 0.1f, total, 3);
                Assert.True(total < last);
                last = total;
            }
        }

        [Fact]
        public void Suit_Lv6_RevivesOnce_ThenSecondDeathLoses()
        {
            var sim = Bare();
            To(sim, UpgradeId.Suit, 6);
            var s = Item<SampleSuit>(sim, UpgradeId.Suit);
            Run(sim, 1f);
            sim.Hp = 0f;
            sim.Step(0f, 0f);
            Assert.Equal(SOutcome.Playing, sim.Outcome);
            Assert.True(s.Ko > 0f, "쓰러짐 0.45초");
            Assert.False(s.Revived);
            // 쓰러진 동안 또 0이 돼도 버틴다.
            sim.Hp = 0f;
            sim.Step(0f, 0f);
            Assert.Equal(SOutcome.Playing, sim.Outcome);
            Run(sim, 0.5f, null, 0f, 0f, false);
            Assert.True(s.Revived);
            Assert.True(s.Wing >= 0f);
            Assert.Equal(0.6f * sim.MaxHp, sim.Hp, 0);
            Assert.Equal(SOutcome.Playing, sim.Outcome);

            sim.Hp = 0f;
            sim.Step(0f, 0f);
            Assert.Equal(SOutcome.Lost, sim.Outcome);
        }

        [Fact]
        public void Suit_Lv6_FlapsEvery0_8s_AfterRevive()
        {
            var sim = Bare();
            To(sim, UpgradeId.Suit, 6);
            var s = Item<SampleSuit>(sim, UpgradeId.Suit);
            Run(sim, 1f);
            sim.Hp = 0f;
            sim.Step(0f, 0f);
            Run(sim, 0.5f);
            Assert.True(s.Revived);
            // 첫 날갯짓은 부활 1초 뒤, 그 뒤 0.8초마다: 125px 안 몹을 친다.
            var keep = new HashSet<Enemy>();
            Run(sim, 1.2f);
            Enemy e = Mob(sim, -110f, 0f, keep);
            Run(sim, 0.85f, keep);
            Assert.True(e.Hp < e.MaxHp, "날갯짓 125px");
        }

        [Fact]
        public void Suit_Lv5_NoRevive()
        {
            var sim = Bare();
            To(sim, UpgradeId.Suit, 5);
            Run(sim, 1f);
            sim.Hp = 0f;
            sim.Step(0f, 0f);
            Assert.Equal(SOutcome.Lost, sim.Outcome);
        }
    }
}
