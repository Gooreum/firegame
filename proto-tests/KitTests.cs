using FireGame.Core.Sim;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>구급상자(바닥 회복 아이템)와 건물 마감 시계.</summary>
    public class KitTests
    {
        private static SurvivorSim Quiet()
        {
            var sim = new SurvivorSim(1) { Guardian = false };
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        private static void StepQuiet(SurvivorSim sim, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
            }
        }

        [Fact]
        public void Kit_DropsBetween40And80s_OnlyWhenHurt()
        {
            SurvivorSim sim = Quiet();
            sim.Hp = 60f;
            float dropped = -1f;
            for (int i = 0; i < 60 * 80 && dropped < 0f; i++)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
                if (sim.Kits.Count > 0) dropped = sim.Time;
            }
            Assert.True(dropped >= 0f, "80초 안에 구급상자가 안 떨어졌다");
            Assert.InRange(dropped, SurvivorSim.KitMin - 0.1f, SurvivorSim.KitMax + 0.1f);
            Assert.InRange(sim.Kits[0].Pos.DistanceTo(sim.Player), 5f - 0.01f, 10f + 0.01f);

            // 체력이 90% 이상이면 안 떨어진다.
            SurvivorSim full = Quiet();
            StepQuiet(full, 60 * 100);
            Assert.Empty(full.Kits);
        }

        [Fact]
        public void Kit_Heals35_AndSignals()
        {
            SurvivorSim sim = Quiet();
            sim.Hp = 50f;
            sim.Kits.Add(new Pickup { Pos = sim.Player, Life = SurvivorSim.KitLife });
            sim.Step(0f, 0f);
            Assert.Equal(50f + SurvivorSim.KitHeal, sim.Hp, 2);
            Assert.True(sim.JustPickedKit);
            Assert.Equal(1, sim.Stats.KitsPicked);
            Assert.Empty(sim.Kits);
            sim.Step(0f, 0f);
            Assert.False(sim.JustPickedKit);

            // 최대 체력을 넘지 않는다.
            SurvivorSim near = Quiet();
            near.Hp = near.MaxHp - 5f;
            near.Kits.Add(new Pickup { Pos = near.Player, Life = SurvivorSim.KitLife });
            near.Step(0f, 0f);
            Assert.Equal(near.MaxHp, near.Hp);
        }

        [Fact]
        public void Kit_ExpiresAfter25s()
        {
            SurvivorSim sim = Quiet();
            sim.Kits.Add(new Pickup { Pos = new Vec2(sim.Player.X + 20f, sim.Player.Y), Life = SurvivorSim.KitLife });
            StepQuiet(sim, (int)(SurvivorSim.KitLife * 60) - 6);
            Assert.Single(sim.Kits);
            StepQuiet(sim, 12);
            Assert.Empty(sim.Kits);
            Assert.Equal(0, sim.Stats.KitsPicked);
        }

        [Fact]
        public void Kit_NeverTwoAtOnce()
        {
            SurvivorSim sim = Quiet();
            sim.Hp = 10f;
            int seen = 0;
            for (int i = 0; i < 60 * 200; i++)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
                Assert.True(sim.Kits.Count <= 1, "구급상자가 둘 이상 바닥에 있다");
                if (sim.Kits.Count == 1) seen++;
            }
            Assert.True(seen > 0, "200초 동안 구급상자가 한 번도 안 떨어졌다");
            Assert.Equal(10f, sim.Hp);
        }

        [Fact]
        public void Kit_IsNotACard()
        {
            var l = new Loadout();
            Assert.False(l.CanTake(UpgradeId.Heal));
            var rng = new Rng(9);
            for (int level = 1; level <= 30; level++)
            {
                for (int i = 0; i < 20; i++) Assert.DoesNotContain(UpgradeId.Heal, SurvivorUpgrades.Roll(l, level, ref rng));
            }
        }

        [Fact]
        public void Bot_WalksToTheKit_WhenHurt()
        {
            SurvivorSim sim = Quiet();
            sim.Hp = 50f;
            sim.Kits.Add(new Pickup { Pos = new Vec2(sim.Player.X + 6f, sim.Player.Y), Life = SurvivorSim.KitLife });
            var bot = new SurvivorBot(sim);
            for (int i = 0; i < 60 * 5 && sim.Stats.KitsPicked == 0; i++)
            {
                sim.Enemies.Clear();
                bot.Play();
            }
            Assert.Equal(1, sim.Stats.KitsPicked);
            Assert.Equal(50f + SurvivorSim.KitHeal, sim.Hp, 2);
        }

        [Fact]
        public void Deadline_IsSmokeClock_WithPeople_AndCollapse_Without()
        {
            SurvivorSim sim = Quiet();
            var shop = new Structure { Kind = StructureKind.House, Name = "가게", Pos = new Vec2(sim.Player.X, sim.Player.Y + 20f), Half = new Vec2(2f, 1.5f), Residents = 1 };
            sim.Structures.Add(shop);
            Assert.Equal(float.PositiveInfinity, sim.Deadline(shop));

            // 연기가 쌓이는 중: 첫 사람을 잃기까지 남은 시간.
            sim.Ignite(shop, 1f);
            shop.Smoke = 5f;
            Assert.Equal(SurvivorSim.SmokeTime - 5f, sim.Deadline(shop), 2);

            // 아직 연기 전(불 0.3): 연기 시작까지 + 첫 사람.
            shop.Fire = 0.3f;
            shop.Smoke = 0f;
            float expect = ((SurvivorSim.SmokeFire - 0.3f) / sim.Stage.FireGrowth) + SurvivorSim.SmokeTime;
            Assert.Equal(expect, sim.Deadline(shop), 2);

            // 사람이 없으면 무너지기까지.
            shop.Residents = 0;
            shop.Fire = 1f;
            Assert.Equal(sim.TimeToFall(shop), sim.Deadline(shop), 2);

            // 무너지는 쪽이 더 빠르면 그쪽.
            shop.Residents = 2;
            shop.Integrity = 0.1f;
            Assert.Equal(sim.TimeToFall(shop), sim.Deadline(shop), 2);
        }
    }
}
