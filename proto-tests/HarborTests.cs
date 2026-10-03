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

        /// <summary>플레이어가 레벨업 카드에서 이 장비를 골랐을 때(Choose 경로).</summary>
        private static void Take(SurvivorSim sim, UpgradeId id)
        {
            sim.PendingChoices = new List<UpgradeId> { id };
            sim.Choose(0);
        }

        [Fact]
        public void Fireboat_CrossesTheSea_AndSoaksTheQuayRowAndBoats()
        {
            var sim = Quiet();
            Take(sim, UpgradeId.Fireboat);
            Structure shop = House(sim, 30f, SurvivorHarbor.QuayRow);
            Structure inland = House(sim, 30f, 24f);
            Structure boat = Boat(sim, 30f, SurvivorHarbor.BoatLane, false);
            sim.Ignite(shop, 0.9f);
            sim.Ignite(inland, 0.9f);
            bool sailed = false;
            float shopBefore = 0f;
            for (int i = 0; i < 60 * 10; i++)
            {
                sim.Step(0f, 0f);
                sim.Enemies.Clear();
                if (sim.Fireboat.HasValue && !sailed) { sailed = true; shopBefore = shop.Fire; Assert.True(sim.Time < 6.1f, "부둣가 불이 있으면 6초 안에 나선다"); }
            }
            Assert.True(sailed, "소방정이 안 나섰다");
            Assert.False(sim.Fireboat.HasValue, "4초 뒤엔 지나갔다");
            Assert.True(shop.Fire < shopBefore - 0.3f, "부둣가 집은 한 방 0.6을 맞는다: " + shopBefore + " → " + shop.Fire);
            Assert.False(boat.Burning, "떠가는 불배는 꺼진다");
            Assert.True(inland.Fire >= 0.9f, "둘째 줄 집은 사거리 밖: " + inland.Fire);
        }

        [Fact]
        public void Fireboat_WaitsWhenOnlyInlandBuildingsBurn()
        {
            var sim = Quiet();
            Take(sim, UpgradeId.Fireboat);
            Structure inland = House(sim, 30f, 24f);
            sim.Ignite(inland, 0.9f);
            for (int i = 0; i < 60 * 20; i++)
            {
                sim.Step(0f, 0f);
                sim.Enemies.Clear();
                Assert.False(sim.Fireboat.HasValue, "부둣가 불이 없으면 소방정은 기다린다");
            }
        }

        [Fact]
        public void Wave_PutsOutDockedBoats_PushesThemBackToSea_AndWetsTheQuay()
        {
            var sim = Quiet();
            Take(sim, UpgradeId.Wave);
            Structure shop = House(sim, 30f, SurvivorHarbor.QuayRow);
            Structure boat = Boat(sim, 30f, SurvivorHarbor.SeaFrom + 0.6f, true);
            sim.Ignite(shop, 0.9f);
            Enemy onSea = sim.Spawn(EnemyKind.Blaze, new Vec2(30f, 50f));
            onSea.Speed = 0f;
            onSea.MaxHp = onSea.Hp = 999f;
            bool surged = false;
            float surgedAt = 0f;
            for (int i = 0; i < 60 * 8 && !surged; i++)
            {
                sim.Step(0f, 0f);
                sim.Enemies.RemoveAll(e => e != onSea);
                if (sim.JustSurge) { surged = true; surgedAt = sim.Time; }
            }
            Assert.True(surged, "큰 파도가 안 일었다");
            Assert.True(sim.WaveY.HasValue);
            // 파도가 부두선까지 내려올 때까지(22칸 / 14 ≈ 1.6초).
            for (int i = 0; i < 60 * 3; i++)
            {
                sim.Step(0f, 0f);
                sim.Enemies.RemoveAll(e => e != onSea);
            }
            Assert.False(sim.WaveY.HasValue, "파도는 WaveEnd에서 잦아든다");
            Assert.False(boat.Burning, "닿은 불배가 꺼진다");
            Assert.False(boat.Docked);
            Assert.True(boat.Pos.Y > SurvivorHarbor.SeaFrom + 4f, "북쪽으로 밀렸다: " + boat.Pos.Y);
            Assert.True(shop.Fire < 0.9f + (0.04f * 4f) - 0.5f, "부둣가 집은 0.8 끈다: " + shop.Fire);
            Assert.True(shop.Wet >= 4f, "부둣가 집은 젖는다: " + shop.Wet);
            Assert.True(onSea.Hp <= 999f - 12f + 0.01f, "띠 안 적은 12 피해: " + onSea.Hp);
            Assert.True(onSea.Pos.Y < 50f, "남쪽으로 밀린다: " + onSea.Pos.Y);
            // 20초 전엔 다시 안 온다.
            sim.Ignite(shop, 0.9f);
            for (int i = 0; i < 60 * 12; i++)
            {
                sim.Step(0f, 0f);
                sim.Enemies.Clear();
                Assert.False(sim.JustSurge && sim.Time < surgedAt + SurvivorSim.WaveInterval - 0.1f, "파도 간격 20초");
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
