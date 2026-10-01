using FireGame.Core.Sim;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>불 규칙: 큰 불은 물이 덜 먹히고, 크게 타는 건물은 옆 건물로 옮겨붙고, 신고 불은 세게 붙는다.</summary>
    public class FireRuleTests
    {
        private static SurvivorSim Quiet(int seed = 1)
        {
            var sim = new SurvivorSim(seed);
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        private static Structure Shop(SurvivorSim sim, float dx, float dy)
        {
            var s = new Structure { Kind = StructureKind.House, Name = "가게", Pos = new Vec2(sim.Player.X + dx, sim.Player.Y + dy), Half = new Vec2(2f, 1.5f), Residents = 1 };
            sim.Structures.Add(s);
            return s;
        }

        /// <summary>불 세기 start인 가게에 물대포를 ticks만큼 쏘고 줄어든 양을 돌려준다(적은 매 틱 치운다).</summary>
        private static float Knockdown(float start, int ticks)
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 6f, 0f);
            sim.Ignite(shop, start);
            for (int i = 0; i < ticks; i++)
            {
                sim.Enemies.Clear();
                sim.Aim = new Vec2(shop.Pos.X - sim.Player.X, shop.Pos.Y - sim.Player.Y);
                sim.Spraying = true;
                sim.Step(0f, 0f);
            }
            return start - shop.Fire;
        }

        [Fact]
        public void BigFire_TakesLessFromTheSameWater()
        {
            float small = Knockdown(0.5f, 40);
            float big = Knockdown(1f, 40);
            Assert.True(big < small, "같은 물로 큰 불(" + big + ")이 작은 불(" + small + ")보다 덜 줄어야 한다");
            Assert.True(big > 0f, "큰 불도 물을 맞으면 줄어야 한다");
        }

        [Fact]
        public void BigBuildingFire_SpreadsToTheNearestBuilding()
        {
            SurvivorSim sim = Quiet();
            Structure burning = Shop(sim, 8f, 0f);
            Structure next = Shop(sim, 8f, 7f);
            sim.Ignite(burning, 0.9f);
            bool spread = false;
            for (int i = 0; i < (int)((sim.Stage.SpreadEvery + 0.2f) / SurvivorSim.Dt) && !spread; i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                if (sim.Spread.Contains(next)) spread = true;
            }
            Assert.True(spread, "크게 타는 가게에서 옆 가게로 안 옮겨붙었다");
            Assert.True(next.Burning);
            Assert.Equal(1, sim.Stats.Spreads);
        }

        [Fact]
        public void WetBuilding_DoesNotCatchTheSpread()
        {
            SurvivorSim sim = Quiet();
            Structure burning = Shop(sim, 8f, 0f);
            Structure next = Shop(sim, 8f, 7f);
            sim.Ignite(burning, 0.9f);
            int ticks = (int)((sim.Stage.SpreadEvery + 0.5f) / SurvivorSim.Dt);
            for (int i = 0; i < ticks; i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                next.Wet = 1f;
                sim.Step(0f, 0f);
            }
            Assert.False(next.Burning, "젖은 가게에 번졌다");
        }

        [Fact]
        public void FarBuilding_IsOutOfSpreadRange()
        {
            SurvivorSim sim = Quiet();
            Structure burning = Shop(sim, 0f, 8f);
            // 가장자리 거리 = 20 − 4 = 16칸 > SpreadRange.
            Structure far = Shop(sim, 20f, 8f);
            sim.Ignite(burning, 0.9f);
            Assert.Null(sim.NextBuilding(burning));
            int ticks = (int)((sim.Stage.SpreadEvery + 0.5f) / SurvivorSim.Dt);
            for (int i = 0; i < ticks; i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
            }
            Assert.False(far.Burning);
        }

        [Fact]
        public void HeliDrop_KnocksABigFireDown_AndSignalsIt()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 8f, 0f);
            sim.Ignite(shop, 1f);
            sim.PendingChoices = new System.Collections.Generic.List<UpgradeId> { UpgradeId.Heli };
            sim.Choose(0);
            FireKnock? knock = null;
            for (int i = 0; i < 60 * 4 && knock == null; i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                foreach (FireKnock k in sim.Knocked)
                {
                    if (k.At == shop) knock = k;
                }
            }
            Assert.NotNull(knock);
            Assert.True(knock.Value.Amount > SurvivorSim.KnockShown, "헬기 물이 줄인 양 " + knock.Value.Amount);
        }

        [Fact]
        public void HoseTicks_AreTooSmallToSignal()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 6f, 0f);
            sim.Ignite(shop, 1f);
            for (int i = 0; i < 60; i++)
            {
                sim.Enemies.Clear();
                sim.Aim = new Vec2(shop.Pos.X - sim.Player.X, shop.Pos.Y - sim.Player.Y);
                sim.Spraying = true;
                sim.Step(0f, 0f);
                Assert.Empty(sim.Knocked);
            }
        }

        /// <summary>가게에 ticks 동안 물대포를 쏘고(spraying이 false면 쉬고), 증기 폭발 횟수와 폭발 틱의 가장 큰 Knocked 양을 센다.</summary>
        private static void Hose(SurvivorSim sim, Structure shop, int ticks, bool spraying, ref int bursts, ref float biggestKnock)
        {
            for (int i = 0; i < ticks; i++)
            {
                sim.Aim = new Vec2(shop.Pos.X - sim.Player.X, shop.Pos.Y - sim.Player.Y);
                sim.Spraying = spraying;
                sim.Step(0f, 0f);
                if (sim.SteamBursts.Count == 0) continue;
                bursts += sim.SteamBursts.Count;
                foreach (FireKnock k in sim.Knocked) if (k.Amount > biggestKnock) biggestKnock = k.Amount;
            }
        }

        [Fact]
        public void Steam_BurstsAfterTwoSecondsOfHose_AndIgnoresResist()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 6f, 0f);
            sim.Ignite(shop, 1f);
            int bursts = 0;
            float knock = 0f;
            // 물이 닿기까지 0.4초, 맞는 동안에도 초당 SteamCool만큼 식어 2초 분량을 채우려면 3초 남짓 걸린다.
            Hose(sim, shop, 240, true, ref bursts, ref knock);
            Assert.True(bursts >= 1, "4초를 이어 쐈는데 증기 폭발이 없다");
            Assert.True(knock >= 0.3f, "증기 폭발은 저항을 무시하고 한 번에 크게 줄여야 한다: " + knock);
            Assert.Equal(bursts, sim.Stats.SteamBursts);
        }

        [Fact]
        public void Steam_CoolsSlowly_SoAShortBreakKeepsTheCharge()
        {
            // 코앞 불씨를 잡으러 1초 손을 떼도 쌓인 물은 남는다: 2초 + 1초 쉼 + 2초면 터진다.
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 6f, 0f);
            sim.Ignite(shop, 1f);
            int bursts = 0;
            float knock = 0f;
            Hose(sim, shop, 120, true, ref bursts, ref knock);
            Hose(sim, shop, 60, false, ref bursts, ref knock);
            Hose(sim, shop, 120, true, ref bursts, ref knock);
            Assert.True(bursts >= 1, "짧게 쉬었다고 쌓인 물이 날아갔다");

            // 오래 쉬면 다 식는다: 1.5초 + 8초 쉼 + 1.5초로는 안 터진다.
            sim = Quiet();
            shop = Shop(sim, 6f, 0f);
            sim.Ignite(shop, 1f);
            bursts = 0;
            Hose(sim, shop, 90, true, ref bursts, ref knock);
            Hose(sim, shop, 480, false, ref bursts, ref knock);
            Hose(sim, shop, 90, true, ref bursts, ref knock);
            Assert.Equal(0, bursts);
        }

        [Fact]
        public void Steam_ScaldsAndPushesTheCrowdBesideTheBuilding()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 6f, 0f);
            sim.Ignite(shop, 1f);
            // 가게 건너편(물줄기가 닿지 않는 쪽)에 불씨 하나.
            Enemy ember = sim.Spawn(EnemyKind.Ember, new Vec2(shop.Pos.X + 3f, shop.Pos.Y));
            ember.Speed = 0f;
            ember.MaxHp = 999f;
            ember.Hp = 999f;
            bool scalded = false;
            for (int i = 0; i < 240 && !scalded; i++)
            {
                sim.Aim = new Vec2(shop.Pos.X - sim.Player.X, shop.Pos.Y - sim.Player.Y);
                sim.Spraying = true;
                sim.Step(0f, 0f);
                if (sim.SteamBursts.Count == 0) continue;
                scalded = sim.Hits.Exists(h => h.Source == HitSource.Steam);
                Assert.True(ember.Knock.X > 0f, "증기는 불씨를 건물 바깥으로 밀어야 한다");
            }
            Assert.True(scalded, "증기 폭발이 곁의 불씨를 데우지 않았다");
            Assert.True(ember.Hp < 999f);
        }

        [Fact]
        public void Spread_TellsWhereItCameFrom()
        {
            SurvivorSim sim = Quiet();
            Structure burning = Shop(sim, 8f, 0f);
            Structure next = Shop(sim, 8f, 7f);
            sim.Ignite(burning, 0.9f);
            for (int i = 0; i < (int)((sim.Stage.SpreadEvery + 0.2f) / SurvivorSim.Dt); i++)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
                int k = sim.Spread.IndexOf(next);
                if (k < 0) continue;
                Assert.Same(burning, sim.SpreadFrom[k]);
                return;
            }
            Assert.Fail("번짐이 안 일어났다");
        }

        [Fact]
        public void Report_IgnitesAtReportFire()
        {
            var sim = new SurvivorSim(1);
            Structure hit = null;
            while (hit == null && sim.Time < SurvivorSim.ReportTimes[0] + 0.1f)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
                hit = sim.Ignited.Find(s => s.Kind == StructureKind.House);
            }
            Assert.NotNull(hit);
            Assert.True(hit.Fire >= SurvivorSim.ReportFire, "신고 불 세기 " + hit.Fire);
        }
    }
}
