using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>3스테이지 공단: 맵, 약품 드럼 연쇄 폭발, 기름 방울과 건물을 태우는 기름 불.</summary>
    public class FactoryTests
    {
        private static SurvivorSim Quiet()
        {
            var sim = new SurvivorSim(1, 3);
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        private static Structure Plant(SurvivorSim sim, float dx, float dy)
        {
            var s = new Structure { Kind = StructureKind.House, Name = "공장", Pos = new Vec2(sim.Player.X + dx, sim.Player.Y + dy), Half = new Vec2(2f, 1.5f), Residents = 1 };
            sim.Structures.Add(s);
            return s;
        }

        private static int OilCount(SurvivorSim sim)
        {
            return sim.BurningGround.FindAll(p => p.Oil && !p.Out && p.Life > 0f).Count;
        }

        [Fact]
        public void FactoryMap_HasEightPlants_ARefinery_TwelveDrums_AndAClearCenter()
        {
            var sim = new SurvivorSim(1, 3);
            Assert.Equal("공단", sim.Stage.Name);
            Assert.Equal(3, sim.Stage.Number);
            Assert.Equal(8, sim.Structures.FindAll(s => s.Kind == StructureKind.House).Count);
            Structure depot = Assert.Single(sim.Structures.FindAll(s => s.Kind == StructureKind.Depot));
            Assert.Equal("정유 저장소", depot.Name);
            Assert.Equal(12, sim.Structures.FindAll(s => s.Kind == StructureKind.Gas).Count);
            Assert.All(sim.Structures, s => Assert.True(s.DistanceTo(sim.Player) > 6f, s.Name + "가 출발점에 너무 가깝다"));
            Assert.All(sim.Structures, s => Assert.False(s.Burning));
        }

        [Fact]
        public void OneDrum_SetsOffItsPile_AndSpillsOil()
        {
            var sim = new SurvivorSim(1, 3);
            sim.Reports = false;
            Structure first = sim.Structures.Find(s => s.Kind == StructureKind.Gas);
            var pile = sim.Structures.FindAll(s => s.Kind == StructureKind.Gas && s.Pos.DistanceTo(first.Pos) < 3f);
            Assert.Equal(3, pile.Count);
            sim.Ignite(first, 0.5f);
            bool spilled = false;
            for (int i = 0; i < (int)(8f / SurvivorSim.Dt); i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                if (OilCount(sim) > 0) spilled = true;
            }
            Assert.All(pile, d => Assert.True(d.Collapsed, "무더기 드럼이 안 터졌다"));
            Assert.True(spilled, "드럼이 터졌는데 기름 불이 없다");
        }

        [Fact]
        public void OilBlob_LeavesOilFireBehind()
        {
            SurvivorSim sim = Quiet();
            Enemy blob = sim.Spawn(EnemyKind.Oil, new Vec2(sim.Player.X + 10f, sim.Player.Y));
            blob.Hp = blob.MaxHp = 1000f;
            for (int i = 0; i < (int)(3f / SurvivorSim.Dt); i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
            }
            Assert.True(OilCount(sim) >= 2, "3초 걸은 기름 방울이 남긴 기름 불 " + OilCount(sim));
            Assert.All(sim.BurningGround, p => Assert.True(p.Oil));
        }

        [Fact]
        public void OilBlob_SplashesOilWhenKilled()
        {
            SurvivorSim sim = Quiet();
            Enemy blob = sim.Spawn(EnemyKind.Oil, new Vec2(sim.Player.X + 4f, sim.Player.Y));
            blob.Speed = 0f;
            blob.Hp = 0.01f;
            for (int i = 0; i < 60 && !blob.Dead; i++)
            {
                sim.Aim = new Vec2(1f, 0f);
                sim.Spraying = true;
                sim.Step(0f, 0f);
            }
            Assert.True(blob.Dead, "물줄기로 기름 방울을 못 잡았다");
            sim.Spraying = false;
            sim.Step(0f, 0f);
            Assert.True(OilCount(sim) >= SurvivorSim.OilDeathSpill - 1, "잡은 자리 기름 불 " + OilCount(sim));
        }

        [Fact]
        public void OilFire_CatchesTheBuildingItTouches()
        {
            SurvivorSim sim = Quiet();
            Structure plant = Plant(sim, 8f, 0f);
            sim.AddOil(new Vec2(plant.Pos.X - plant.Half.X - 0.3f, plant.Pos.Y));
            sim.Step(0f, 0f);
            Assert.True(plant.Burning, "기름 불이 닿은 공장이 안 탔다");
            Assert.Contains(plant, sim.OilCaught);
            Assert.Equal(1, sim.Stats.OilFires);
        }

        [Fact]
        public void PlainGroundFire_DoesNotCatchBuildings()
        {
            SurvivorSim sim = Quiet();
            Structure plant = Plant(sim, 8f, 0f);
            sim.BurningGround.Add(new Puddle { Pos = new Vec2(plant.Pos.X - plant.Half.X - 0.3f, plant.Pos.Y), Radius = SurvivorSim.OilRadius, Life = 4f, MaxLife = 4f });
            for (int i = 0; i < 60; i++) sim.Step(0f, 0f);
            Assert.False(plant.Burning);
        }

        [Fact]
        public void WetBuilding_DoesNotCatchOilFire()
        {
            SurvivorSim sim = Quiet();
            Structure plant = Plant(sim, 8f, 0f);
            sim.AddOil(new Vec2(plant.Pos.X - plant.Half.X - 0.3f, plant.Pos.Y));
            for (int i = 0; i < 60; i++)
            {
                plant.Wet = 1f;
                sim.Step(0f, 0f);
            }
            Assert.False(plant.Burning);
            Assert.Empty(sim.OilCaught);
        }

        [Fact]
        public void OilBlobs_OnlyComeInTheFactory_AfterOilFrom()
        {
            var town = new SurvivorSim(1, 1);
            Assert.Equal(0f, town.Stage.OilShare);
            var factory = new SurvivorSim(3, 3);
            bool early = false;
            bool seen = false;
            while (factory.Time < 120f && factory.Outcome == SOutcome.Playing)
            {
                factory.Hp = factory.MaxHp;
                if (factory.PendingChoices != null) factory.Choose(0);
                factory.Step(0f, 0f);
                foreach (Enemy e in factory.Enemies)
                {
                    if (e.Kind != EnemyKind.Oil) continue;
                    seen = true;
                    if (factory.Time < SurvivorSim.OilFrom) early = true;
                }
            }
            Assert.False(early, "기름 방울이 OilFrom 전에 나왔다");
            Assert.True(seen, "2분 동안 기름 방울이 한 번도 안 나왔다");
        }
    }
}
