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
            var sim = new SurvivorSim(1, stage) { Guardian = false };
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
                // 요격 보상(BoatXp)으로 레벨업하면 카드를 고를 때까지 판이 멈춘다: 첫 카드를 집는다.
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
                sim.Enemies.Clear();
            }
        }

        [Fact]
        public void FirstBoat_AppearsAtTwentySeconds_DriftsTheLane_TurnsAtItsTarget_AndDocks()
        {
            var sim = new SurvivorSim(3, 4) { Guardian = false };
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
            if (sim.PendingChoices != null) sim.Choose(0);
            sim.Step(0f, 0f);
            Assert.True(boat.Drift.Y > 0f, "북쪽으로 돌아간다");
            Run(sim, 60 * 12);
            Assert.True(boat.Collapsed);
            Assert.True(boat.Pos.Y > SurvivorSim.ArenaSize);
            Assert.Equal(0, sim.HousesLost);
            Assert.DoesNotContain(boat, sim.Fell);
        }

        [Fact]
        public void HoseWater_FliesOverTheSeaInTheHarbor_ButTheTownRiverStillBlocksIt()
        {
            foreach (int stage in new[] { 4, 1 })
            {
                var sim = Quiet(stage);
                sim.Structures.Add(new Structure { Kind = StructureKind.Water, Name = "물", Pos = new Vec2(30f, 30f), Half = new Vec2(3f, 3f) });
                Structure shop = House(sim, 30f, 37f);
                sim.Shots.Add(new Shot { Kind = ShotKind.Drop, Pos = new Vec2(30f, 25f), Vel = new Vec2(0f, 20f), Life = 1f, Damage = 10f, Radius = 0.6f });
                Run(sim, 60);
                if (stage == 4) Assert.True(shop.Wet > 0f, "항구: 물은 바다를 건너 건물을 적신다");
                else Assert.True(shop.Wet <= 0f, "마을: 강이 물줄기를 막는다(예전 그대로)");
            }
        }

        [Fact]
        public void EdgeSpawns_LandAshore_NotStrandedBeyondTheSea()
        {
            var sim = new SurvivorSim(2, 4) { Guardian = false };
            sim.Reports = false;
            int land = 0;
            int total = 0;
            for (int i = 0; i < 60 * 40; i++)
            {
                sim.Step(0f, 0f);
                foreach (Enemy e in sim.Enemies)
                {
                    // 갈매기·게는 바다에서 오는 몹이라 뭍 비율에서 뺀다.
                    if (e.Kind == EnemyKind.Gull || e.Kind == EnemyKind.Crab) continue;
                    total++;
                    if (e.Pos.Y < SurvivorHarbor.SeaFrom + 0.5f) land++;
                }
                sim.Enemies.Clear();
            }
            Assert.True(total > 30, "40초면 스폰이 수십 번 난다: " + total);
            Assert.True(land * 100 >= total * 95, "바다에 떨어진 스폰은 뭍에서 다시 뽑는다: 뭍 " + land + " / " + total);
        }

        [Fact]
        public void Gulls_FlyOverWater_AndAreNotPushedAshore()
        {
            var sim = new SurvivorSim(1, 4) { Guardian = false };
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
        public void Bot_StandsOnThePierTip_WhenOnlyAFloatingBoatBurns()
        {
            // 본 맵(바다·부두가 있다)에서 뭍의 불은 없고 불배만 떠 있다.
            var sim = new SurvivorSim(1, 4) { Guardian = false };
            sim.Enemies.Clear();
            sim.Reports = false;
            Boat(sim, 30f, SurvivorHarbor.BoatLane, false);
            var bot = new SurvivorBot(sim) { Pro = true };
            for (int i = 0; i < 60 * 12; i++)
            {
                bot.Play();
                sim.Enemies.Clear();
                Assert.DoesNotContain(sim.Structures, w => w.Kind == StructureKind.Water && w.Within(sim.Player, 0f));
            }
            bool onPier = false;
            foreach (float x in SurvivorHarbor.PierX) onPier |= System.Math.Abs(sim.Player.X - x) < SurvivorHarbor.PierHalf && sim.Player.Y > SurvivorHarbor.SeaFrom && sim.Player.Y < SurvivorHarbor.PierTip;
            Assert.True(onPier, "봇은 부두 끝에 서서 배를 쏜다: " + sim.Player.X + "," + sim.Player.Y);
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
            // 소방정이 끈 배의 요격 보상(BoatXp)으로 레벨업하면 집은 카드가 집을 적셔 사거리 판정이 흐려진다: 이 테스트는 경험치를 묻어 둔다.
            sim.Xp = -1000;
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
                if (sim.PendingChoices != null) sim.Choose(0);
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
            var sim = new SurvivorSim(1, 4) { Guardian = false };
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
            /// <summary>맵 특색 몹: 갈매기는 폭격기 — 건물을 노려 날아가 지붕에 불 0.25를 떨어뜨리고(GullDrops) 바다로 돌아가 북쪽 끝에서 구슬 없이 사라진다.</summary>
        [Fact]
        public void Gull_FliesToAQuayBuilding_DropsFire_AndLeavesForTheSea()
        {
            SurvivorSim sim = Quiet();
            Structure house = House(sim, 30f, 36f);
            Enemy gull = sim.Spawn(EnemyKind.Gull, new Vec2(30f, 50f));
            Assert.True(gull.Seeker);
            int drops = 0;
            int ticks = 0;
            while (!gull.Dropped && ticks++ < (int)(8f / SurvivorSim.Dt))
            {
                sim.Step(0f, 0f);
                drops += sim.GullDrops.Count;
                sim.Enemies.RemoveAll(e => e != gull);
            }
            Assert.True(gull.Dropped, "8초 안에 지붕에 닿아야 한다: " + gull.Pos.Y);
            Assert.Equal(1, drops);
            Assert.True(house.Burning && house.Fire >= 0.25f, "지붕 불 " + house.Fire);
            Assert.False(gull.Dead);
            ticks = 0;
            while (!gull.Dead && ticks++ < (int)(10f / SurvivorSim.Dt))
            {
                sim.Step(0f, 0f);
                sim.Enemies.RemoveAll(e => e != gull);
            }
            Assert.True(gull.Dead, "10초 안에 바다 끝에서 사라져야 한다: " + gull.Pos.Y);
            Assert.True(gull.Pos.Y >= SurvivorSim.ArenaSize - 1f, "북쪽 끝: " + gull.Pos.Y);
            Assert.Empty(sim.Gems);
        }

        /// <summary>불 게: 바다 위에서 안 밀리고 기어 올라와 건물에 불 0.4를 지핀 뒤(Dropped) 소방관을 쫓는다.</summary>
        [Fact]
        public void Crab_CrawlsAshore_IgnitesABuilding_ThenChasesTheFirefighter()
        {
            var sim = new SurvivorSim(1, 4) { Guardian = false };
            sim.Enemies.Clear();
            sim.Reports = false;
            Enemy crab = sim.Spawn(EnemyKind.Crab, new Vec2(30f, 50f));
            Assert.True(crab.Seeker && crab.Touch >= 10f && crab.MaxHp >= 9f);
            bool wetStep = false;
            int ticks = 0;
            while (!crab.Dropped && ticks++ < (int)(25f / SurvivorSim.Dt))
            {
                sim.Step(0f, 0f);
                sim.Enemies.RemoveAll(e => e != crab);
                if (ticks < (int)(2f / SurvivorSim.Dt)) wetStep |= sim.Structures.Exists(w => w.Kind == StructureKind.Water && w.Within(crab.Pos, 0f));
            }
            Assert.True(wetStep, "처음 2초는 바다 위에 있어야 한다(밀리지 않는다)");
            Assert.True(crab.Dropped, "25초 안에 상륙해 건물에 닿아야 한다: " + crab.Pos);
            Assert.Contains(sim.Structures, s => s.IsBuilding && s.Burning && s.Fire >= 0.4f);
            float before = crab.Pos.DistanceTo(sim.Player);
            for (int i = 0; i < (int)(2f / SurvivorSim.Dt); i++)
            {
                sim.Step(0f, 0f);
                sim.Enemies.RemoveAll(e => e != crab);
            }
            Assert.True(crab.Pos.DistanceTo(sim.Player) < before - 1f, "불을 지핀 뒤엔 소방관을 쫓는다: " + before + " → " + crab.Pos.DistanceTo(sim.Player));
        }

        /// <summary>불 게는 큰 불 고리처럼 밀치기가 HeavyKnock배만 먹힌다.</summary>
        [Fact]
        public void Crab_BarelyMoves_WhenKnocked()
        {
            SurvivorSim sim = Quiet();
            Vec2 p = sim.Player;
            Enemy light = sim.Spawn(EnemyKind.Blaze, new Vec2(p.X + 2f, p.Y));
            Enemy crab = sim.Spawn(EnemyKind.Crab, new Vec2(p.X + 2f, p.Y + 0.01f));
            crab.Seeker = false;
            crab.Dropped = true;
            light.Speed = crab.Speed = 0f;
            light.Hp = light.MaxHp = crab.Hp = crab.MaxHp = 999f;
            sim.Aim = new Vec2(1f, 0f);
            sim.Spraying = true;
            for (int i = 0; i < 90; i++) sim.Step(0f, 0f);
            float lightPush = light.Pos.X - (p.X + 2f);
            float crabPush = crab.Pos.X - (p.X + 2f);
            Assert.True(lightPush > 0.5f, "보통 큰 불은 밀린다: " + lightPush);
            Assert.True(crabPush < lightPush * 0.5f, "게가 많이 밀렸다: " + crabPush + " (보통 " + lightPush + ")");
        }
            /// <summary>요격 보상: 부두에 닿기 전에 바다 위에서 끈 배는 BoatXp(15)를 준다. 닿은 배·유조선은 아니다.</summary>
        [Fact]
        public void DousingABoatAtSea_GivesInterceptXp()
        {
            SurvivorSim sim = Quiet();
            int before = sim.Xp;
            int level = sim.Level;
            Structure boat = Boat(sim, 30f, 48f, false, new Vec2(-SurvivorSim.BoatSpeed, 0f));
            sim.Shots.Add(new Shot { Kind = ShotKind.Drop, Pos = boat.Pos, Life = 0.5f, Damage = 100f, Radius = 1f });
            sim.Step(0f, 0f);
            Assert.Contains(boat, sim.BoatsAway);
            // 15면 1레벨 필요치(11)를 넘겨 레벨업한다(경험치는 넘긴 만큼만 남는다).
            Assert.True(sim.Level > level || sim.Xp == before + SurvivorSim.BoatXp, "요격 보상: 레벨 " + level + "→" + sim.Level + ", 경험치 " + before + "→" + sim.Xp);

            SurvivorSim docked = Quiet();
            int before2 = docked.Xp;
            Structure moored = Boat(docked, 30f, SurvivorHarbor.SeaFrom + 0.6f, true);
            docked.Shots.Add(new Shot { Kind = ShotKind.Drop, Pos = moored.Pos, Life = 0.5f, Damage = 100f, Radius = 1f });
            docked.Step(0f, 0f);
            Assert.False(moored.Burning);
            Assert.Equal(before2, docked.Xp);
            Assert.Equal(1, docked.Level);

            SurvivorSim tank = Quiet();
            int before3 = tank.Xp;
            Structure tanker = Boat(tank, 30f, 48f, false, new Vec2(-SurvivorSim.TankerSpeed, 0f));
            tanker.Tanker = true;
            tank.Shots.Add(new Shot { Kind = ShotKind.Drop, Pos = tanker.Pos, Life = 0.5f, Damage = 100f, Radius = 1f });
            tank.Step(0f, 0f);
            Assert.False(tanker.Burning);
            Assert.Equal(before3, tank.Xp);
            Assert.Equal(1, tank.Level);
        }

        /// <summary>접안까지 남은 초: 줄을 따라 목표 x까지 + 남쪽으로 부두선까지를 속도로 나눈다. 틱마다 줄고 닿으면 0.</summary>
        [Fact]
        public void BoatEta_CountsDownToZeroAtTheQuay()
        {
            SurvivorSim sim = Quiet();
            Structure house = House(sim, 30f, SurvivorHarbor.QuayRow);
            Structure boat = Boat(sim, 10f, SurvivorHarbor.BoatLane, false, new Vec2(SurvivorSim.BoatSpeed, 0f));
            boat.Target = house;
            float expect = (20f + (SurvivorHarbor.BoatLane - 0.6f - SurvivorHarbor.SeaFrom)) / SurvivorSim.BoatSpeed;
            Assert.Equal(expect, sim.BoatEta(boat), 1);
            float first = sim.BoatEta(boat);
            Run(sim, 60);
            float later = sim.BoatEta(boat);
            Assert.True(later < first - 0.8f, "1초 지나면 약 1초 준다: " + first + " → " + later);
            int ticks = 0;
            while (!boat.Docked && ticks++ < 60 * 30) Run(sim, 1);
            Assert.True(boat.Docked, "줄을 따라 가 닿는다");
            Assert.Equal(0f, sim.BoatEta(boat));
        }
    }
}
