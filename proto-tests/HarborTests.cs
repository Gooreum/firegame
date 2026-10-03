using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>4스테이지 항구: 맵, 바다를 떠내려오는 불배, 불 갈매기, 소방정·큰 파도.</summary>
    public class HarborTests
    {
        private static SurvivorSim Quiet(int stage = 4)
        {
            var sim = new SurvivorSim(1, stage);
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        private static Structure House(SurvivorSim sim, float x, float y, int residents = 1)
        {
            var s = new Structure { Kind = StructureKind.House, Name = "집", Pos = new Vec2(x, y), Half = new Vec2(2f, 1.5f), Residents = residents };
            sim.Structures.Add(s);
            return s;
        }

        private static Structure Boat(SurvivorSim sim, float x, float y, bool docked, Vec2 drift = default)
        {
            var b = new Structure { Kind = StructureKind.Boat, Name = "불배", Pos = new Vec2(x, y), Half = new Vec2(1.2f, 0.6f), Docked = docked, Drift = drift };
            sim.Structures.Add(b);
            sim.Ignite(b, SurvivorSim.BoatFire);
            // 닿은 배는 TickBoats가 닿는 순간 1초로 맞춘다(Ignite가 SpreadEvery로 되돌리므로 그 뒤에).
            b.SpreadClock = 1f;
            return b;
        }

        /// <summary>적 없이 n틱 돌린다(소방관은 가만히).</summary>
        private static void Run(SurvivorSim sim, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                sim.Step(0f, 0f);
                sim.Enemies.Clear();
            }
        }

        [Fact]
        public void FirstBoat_AppearsAtTwentySeconds_DriftsTheLane_TurnsAtItsTarget_AndDocks()
        {
            var sim = new SurvivorSim(3, 4);
            sim.Reports = false;
            Structure boat = null;
            int guard = 0;
            while (boat == null && guard++ < 60 * 25)
            {
                sim.Step(0f, 0f);
                sim.Enemies.Clear();
                boat = sim.JustBoat;
            }
            Assert.NotNull(boat);
            Assert.InRange(sim.Time, SurvivorSim.FirstBoat - 0.05f, SurvivorSim.FirstBoat + 0.05f);
            Assert.Equal(SurvivorHarbor.BoatLane, boat.Pos.Y);
            Assert.True(boat.Burning);
            Assert.NotNull(boat.Target);
            Assert.True(boat.Target.IsBuilding && boat.Target.Pos.Y > 32f, "표적은 부둣가 건물");
            Assert.True(boat.Pos.X < 0f || boat.Pos.X > SurvivorSim.ArenaSize, "바다 끝에서 뜬다");

            bool docked = false;
            guard = 0;
            while (!docked && guard++ < 60 * 40)
            {
                sim.Step(0f, 0f);
                sim.Enemies.Clear();
                docked = sim.BoatsDocked.Contains(boat);
            }
            Assert.True(docked, "40초 안에 부두에 닿는다");
            Assert.True(boat.Docked);
            Assert.InRange(boat.Pos.Y, SurvivorHarbor.SeaFrom + 0.59f, SurvivorHarbor.SeaFrom + 0.61f);
            Assert.InRange(boat.Pos.X, boat.Target.Pos.X - 0.6f, boat.Target.Pos.X + 0.6f);
        }

        [Fact]
        public void DockedBurningBoat_SpreadsToTheQuayBuilding_WithinFourSeconds()
        {
            var sim = Quiet();
            Structure shop = House(sim, 30f, SurvivorHarbor.QuayRow);
            Structure boat = Boat(sim, 30f, SurvivorHarbor.SeaFrom + 0.6f, true);
            bool spread = false;
            for (int i = 0; i < 60 * 4.5f && !spread; i++)
            {
                sim.Step(0f, 0f);
                sim.Enemies.Clear();
                if (sim.Spread.Contains(shop)) { spread = true; Assert.Contains(boat, sim.SpreadFrom); }
            }
            Assert.True(spread, "닿은 불배가 4초 안에 곁 건물에 불을 옮긴다");
            Assert.True(shop.Burning);
        }

        [Fact]
        public void BoatDousedOnTheWater_TurnsBackToSea_AndLeavesWithoutLosingABuilding()
        {
            var sim = Quiet();
            Structure boat = Boat(sim, 30f, 48f, false, new Vec2(-SurvivorSim.BoatSpeed, 0f));
            sim.Shots.Add(new Shot { Kind = ShotKind.Drop, Pos = boat.Pos, Life = 0.5f, Damage = 100f, Radius = 1f });
            sim.Step(0f, 0f);
            Assert.Contains(boat, sim.BoatsAway);
            Assert.False(boat.Burning);
            sim.Step(0f, 0f);
            Assert.True(boat.Drift.Y > 0f, "북쪽으로 돌아간다");
            Run(sim, 60 * 12);
            Assert.True(boat.Collapsed);
            Assert.True(boat.Pos.Y > SurvivorSim.ArenaSize);
            Assert.Equal(0, sim.HousesLost);
            Assert.DoesNotContain(boat, sim.Fell);
        }

        [Fact]
        public void HoseWater_FliesOverWater_AndWetsTheBuildingBeyond()
        {
            var sim = Quiet(1);
            sim.Structures.Add(new Structure { Kind = StructureKind.Water, Name = "강", Pos = new Vec2(30f, 30f), Half = new Vec2(3f, 3f) });
            Structure shop = House(sim, 30f, 37f);
            sim.Shots.Add(new Shot { Kind = ShotKind.Drop, Pos = new Vec2(30f, 25f), Vel = new Vec2(0f, 20f), Life = 1f, Damage = 10f, Radius = 0.6f });
            Run(sim, 60);
            Assert.True(shop.Wet > 0f, "물은 강을 건너 건물을 적신다");
        }

        [Fact]
        public void Gulls_FlyOverWater_AndAreNotPushedAshore()
        {
            var sim = new SurvivorSim(1, 4);
            sim.Enemies.Clear();
            sim.Reports = false;
            Enemy gull = sim.Spawn(EnemyKind.Gull, new Vec2(30f, 50f));
            Enemy ember = sim.Spawn(EnemyKind.Ember, new Vec2(30f, 50f));
            sim.Step(0f, 0f);
            Assert.False(gull.Dead);
            Assert.True(gull.Pos.Y >= 45f, "갈매기는 물 위를 난다: " + gull.Pos.Y);
            Assert.DoesNotContain(sim.Structures, w => w.Kind == StructureKind.Water && w.Within(ember.Pos, 0f));
            Assert.Contains(sim.Structures, w => w.Kind == StructureKind.Water && w.Within(gull.Pos, 0f));
            Assert.True(gull.Speed > 3f);
        }

        [Fact]
        public void Bot_DoesNotWalkIntoTheSeaAfterABurningBoat()
        {
            var sim = Quiet();
            Boat(sim, 30f, 52f, false);
            var bot = new SurvivorBot(sim) { Pro = true };
            for (int i = 0; i < 120; i++)
            {
                bot.Play();
                sim.Enemies.Clear();
            }
            Assert.True(sim.Player.Y < 36f, "봇이 물 위 배를 쫓아 북쪽으로 가지 않는다: " + sim.Player.Y);
        }

        [Fact]
        public void Bot_PicksTheHarborAndMarketYellowCards()
        {
            foreach (UpgradeId id in new[] { UpgradeId.Fireboat, UpgradeId.Wave, UpgradeId.Shells, UpgradeId.Mist })
            {
                Assert.Equal(0, SurvivorBot.PickCard(new List<UpgradeId> { id, UpgradeId.Boots, UpgradeId.Suit }));
            }
        }

        [Fact]
        public void BurningBoat_LastsLongerThanASmallStructure()
        {
            var sim = Quiet();
            Structure boat = Boat(sim, 30f, 48f, false);
            Run(sim, 60 * 30);
            Assert.False(boat.Collapsed, "배는 45초 기준이라 30초엔 안 가라앉는다");
            Assert.True(boat.Burning);
        }
        [Fact]
        public void HarborMap_HasEightBuildings_AColdStore_FuelTanks_AndASeaWithTwoPiers()
        {
            var sim = new SurvivorSim(1, 4);
            Assert.Equal("항구", sim.Stage.Name);
            Assert.Equal(4, sim.Stage.Number);
            Assert.Equal(8, sim.Structures.FindAll(s => s.Kind == StructureKind.House).Count);
            Structure depot = Assert.Single(sim.Structures.FindAll(s => s.Kind == StructureKind.Depot));
            Assert.Equal("수산 냉동창고", depot.Name);
            Assert.Equal(3, sim.Structures.FindAll(s => s.Kind == StructureKind.Gas).Count);
            Assert.Equal(5, sim.Structures.FindAll(s => s.Kind == StructureKind.Water).Count);
            Assert.True(sim.HasWater);
            Assert.All(sim.Structures, s => Assert.True(s.DistanceTo(sim.Player) > 6f, s.Name + "가 출발점에 너무 가깝다"));
            Assert.All(sim.Structures, s => Assert.False(s.Burning));
            int residents = 0;
            foreach (Structure s in sim.Structures) residents += s.Residents;
            Assert.Equal(18, residents);

            // 바다는 SeaFrom 북쪽에만 있고, 부두 자리(y < PierTip)는 비어 있어 걸을 수 있다.
            Assert.All(sim.Structures.FindAll(s => s.Kind == StructureKind.Water), w => Assert.True(w.Pos.Y - w.Half.Y >= SurvivorHarbor.SeaFrom - 0.01f));
            foreach (float px in SurvivorHarbor.PierX)
            {
                var onPier = new Vec2(px, SurvivorHarbor.PierTip - 1f);
                Assert.DoesNotContain(sim.Structures, s => s.Kind == StructureKind.Water && s.Within(onPier, 0f));
                var beyond = new Vec2(px, SurvivorHarbor.PierTip + 1f);
                Assert.Contains(sim.Structures, s => s.Kind == StructureKind.Water && s.Within(beyond, 0f));
            }
            // 부둣가 줄 지붕 윗변과 부두선 사이 틈은 배가 불을 옮기는 거리 안이다.
            Assert.All(sim.Structures.FindAll(s => s.Kind == StructureKind.House && s.Pos.Y > 32f), h => Assert.InRange(SurvivorHarbor.SeaFrom - (h.Pos.Y + h.Half.Y), 1f, 4f));
            // 아직 배는 없다(TickBoats가 띄운다).
            Assert.Empty(sim.Structures.FindAll(s => s.Kind == StructureKind.Boat));
        }
    }
}
