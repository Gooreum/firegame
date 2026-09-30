using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>스테이지 전용 노란 카드: 소방차·스프링클러(마을), 비구름·방염제(산불 숲).</summary>
    public class SpecialTests
    {
        private static SurvivorSim Quiet(int stage = 1)
        {
            var sim = new SurvivorSim(1, stage);
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        /// <summary>플레이어가 레벨업 카드에서 이 장비를 골랐을 때(Choose 경로).</summary>
        private static void Take(SurvivorSim sim, UpgradeId id)
        {
            sim.PendingChoices = new List<UpgradeId> { id };
            sim.Choose(0);
        }

        private static void Run(SurvivorSim sim, float seconds)
        {
            int ticks = (int)(seconds / SurvivorSim.Dt);
            for (int i = 0; i < ticks; i++) sim.Step(0f, 0f);
        }

        private static Structure Shop(SurvivorSim sim, float dx, float dy)
        {
            var s = new Structure { Kind = StructureKind.House, Name = "가게", Pos = new Vec2(sim.Player.X + dx, sim.Player.Y + dy), Half = new Vec2(2f, 1.5f) };
            sim.Structures.Add(s);
            return s;
        }

        private static Structure Tree(SurvivorSim sim, float dx, float dy)
        {
            var s = new Structure { Kind = StructureKind.Tree, Name = "나무", Pos = new Vec2(sim.Player.X + dx, sim.Player.Y + dy), Half = new Vec2(0.6f, 0.6f) };
            sim.Structures.Add(s);
            return s;
        }

        [Fact]
        public void Truck_SweepsTheRow_HittingFiresBesideIt()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Truck);
            Enemy onRow = sim.Spawn(EnemyKind.Blaze, new Vec2(sim.Player.X + 7f, sim.Player.Y + 1f));
            Enemy farAway = sim.Spawn(EnemyKind.Blaze, new Vec2(sim.Player.X + 7f, sim.Player.Y + 6f));
            onRow.Speed = farAway.Speed = 0f;
            onRow.MaxHp = onRow.Hp = farAway.MaxHp = farAway.Hp = 999f;
            bool drove = false;
            for (int i = 0; i < 60 * 6; i++)
            {
                sim.Step(0f, 0f);
                if (sim.Truck.HasValue) drove = true;
            }
            Assert.True(drove, "소방차가 나오지 않았다");
            Assert.True(onRow.Hp <= 999f - 20f + 0.01f, "줄 위 불이 안 맞았다: " + onRow.Hp);
            Assert.Equal(999f, farAway.Hp);
        }

        [Fact]
        public void Sprinkler_KnocksDownEveryBurningBuilding()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Sprinkler);
            Structure near = Shop(sim, 8f, 0f);
            Structure far = Shop(sim, -20f, 20f);
            Run(sim, 0.5f);
            near.Fire = far.Fire = 0.9f;
            bool burst = false;
            for (int i = 0; i < 60 * (int)SurvivorSim.SprinklerInterval + 60; i++)
            {
                sim.Step(0f, 0f);
                if (sim.Sprinkled.Count == 2) burst = true;
            }
            Assert.True(burst, "스프링클러가 두 건물에서 한꺼번에 터져야 한다");
            // 불은 초당 0.04씩 커지지만(최대 1) 스프링클러가 한 번 이상 0.25를 뺐다.
            Assert.True(near.Fire < 1f - SurvivorSim.SprinklerDouse + 0.1f && far.Fire < 1f - SurvivorSim.SprinklerDouse + 0.1f, near.Fire + " / " + far.Fire);
        }

        [Fact]
        public void Rain_PutsOutGroundFire_AndSoaksUnderTheCloud()
        {
            SurvivorSim sim = Quiet(2);
            Take(sim, UpgradeId.Rain);
            Structure shop = Shop(sim, 6f, 0f);
            shop.Fire = 0.6f;
            sim.BurningGround.Add(new Puddle { Pos = new Vec2(shop.Pos.X, shop.Pos.Y - 3f), Radius = 0.6f, Life = 60f, MaxLife = 60f });
            bool rained = false;
            for (int i = 0; i < 60 * 4; i++)
            {
                sim.Step(0f, 0f);
                if (sim.RainAt.HasValue) rained = true;
            }
            Assert.True(rained);
            Assert.Empty(sim.BurningGround);
            Assert.True(shop.Fire < 0.6f, "비 아래 건물 불이 줄어야 한다: " + shop.Fire);
        }

        [Fact]
        public void Retardant_KeepsTreesInTheBandFromCatchingFor20s()
        {
            SurvivorSim sim = Quiet(2);
            Take(sim, UpgradeId.Retardant);
            Structure burning = Tree(sim, 6f, 0f);
            burning.Fire = 0.5f;
            bool dropped = false;
            for (int i = 0; i < 60 * 4 && !dropped; i++)
            {
                sim.Step(0f, 0f);
                if (sim.JustRetardant) dropped = true;
            }
            Assert.True(dropped);
            Band band = Assert.Single(sim.Retardants);
            Assert.False(burning.Burning);
            Assert.True(burning.Wet >= SurvivorSim.RetardantWet - 0.1f);
            Run(sim, 15f);
            Assert.True(burning.Wet > 0f, "20초 안에는 계속 젖어 있다");
            Assert.False(burning.Flammable);
            Assert.True(band.Life > 0f);
        }

        [Fact]
        public void StagePools_HaveFourYellowCardsEach_WithoutEvolutions()
        {
            // 구조대원·물의 장막은 일반 무기로 내려왔다: 노란 카드는 판을 바꾸는 보너스 네 장.
            Assert.Equal(4, SurvivorStages.Get(1).Specials.Length);
            Assert.Equal(4, SurvivorStages.Get(2).Specials.Length);
            Assert.Contains(UpgradeId.Ambulance, SurvivorStages.Get(1).Specials);
            Assert.DoesNotContain(UpgradeId.Partner, SurvivorStages.Get(1).Specials);
            foreach (UpgradeId id in SurvivorStages.Get(1).Specials) Assert.False(Loadout.IsEvolution(id));
            Assert.Contains(UpgradeId.Truck, SurvivorStages.Get(1).Specials);
            Assert.DoesNotContain(UpgradeId.Truck, SurvivorStages.Get(2).Specials);
            Assert.Contains(UpgradeId.Rain, SurvivorStages.Get(2).Specials);
            foreach (UpgradeId id in SurvivorStages.Get(2).Specials) Assert.True(Loadout.IsSpecial(id));
            Assert.Equal(4, SurvivorStages.Get(3).Specials.Length);
            Assert.Contains(UpgradeId.Foam, SurvivorStages.Get(3).Specials);
            Assert.DoesNotContain(UpgradeId.Foam, SurvivorStages.Get(1).Specials);
            Assert.DoesNotContain(UpgradeId.Foam, SurvivorStages.Get(2).Specials);
            foreach (UpgradeId id in SurvivorStages.Get(3).Specials) Assert.True(Loadout.IsSpecial(id));
        }
    }
}
