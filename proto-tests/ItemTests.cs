using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 아이템 재구성: 모든 아이템이 건물 불 끄기·사람 구하기·현장에 빨리 가기를 돕는다.
    /// 무기 6 중 4칸, 보조 6 중 4칸, 무기마다 짝 보조로 진화.
    /// </summary>
    public class ItemTests
    {
        private static SurvivorSim Quiet(int stage = 1)
        {
            var sim = new SurvivorSim(1, stage);
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        /// <summary>플레이어가 레벨업 카드에서 이 카드를 골랐을 때(Choose 경로).</summary>
        private static void Take(SurvivorSim sim, UpgradeId id, int times = 1)
        {
            for (int i = 0; i < times; i++)
            {
                sim.PendingChoices = new List<UpgradeId> { id };
                sim.Choose(0);
            }
        }

        private static void Run(SurvivorSim sim, float seconds)
        {
            int ticks = (int)(seconds / SurvivorSim.Dt);
            for (int i = 0; i < ticks; i++)
            {
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(NoPartner(sim.PendingChoices));
                sim.Step(0f, 0f);
            }
        }

        /// <summary>도중 레벨업에선 대원 카드를 피한다: 대원이 구하면 드론·도끼 같은 다른 아이템의 구조를 가린다.</summary>
        private static int NoPartner(List<UpgradeId> cards)
        {
            int k = cards.FindIndex(c => c != UpgradeId.Partner && c != UpgradeId.Squad);
            return k >= 0 ? k : 0;
        }

        private static Structure Shop(SurvivorSim sim, float dx, float dy, int residents = 0)
        {
            var s = new Structure { Kind = StructureKind.House, Name = "가게", Pos = new Vec2(sim.Player.X + dx, sim.Player.Y + dy), Half = new Vec2(2f, 1.5f), Residents = residents };
            sim.Structures.Add(s);
            return s;
        }

        /// <summary>무기를 Lv5까지, 짝 보조를 하나 고른 뒤, 구슬을 먹어 레벨업한 카드에서 진화를 고른다.</summary>
        private static void Evolve(SurvivorSim sim, UpgradeId evolution)
        {
            UpgradeId weapon = Loadout.BaseOf(evolution);
            while (sim.Build.Level(weapon) < Loadout.MaxLevel) Take(sim, weapon);
            Take(sim, Loadout.PairOf(evolution));
            sim.DropGem(sim.Player, sim.XpToNext);
            for (int i = 0; i < 3 && sim.PendingChoices == null; i++) sim.Step(0f, 0f);
            Assert.NotNull(sim.PendingChoices);
            int pick = sim.PendingChoices.IndexOf(evolution);
            Assert.True(pick >= 0, evolution + " 진화 카드가 안 나왔다: " + string.Join(",", sim.PendingChoices));
            sim.Choose(pick);
        }

        [Fact]
        public void Items_AreSixWeaponsSixPassivesSixEvolutions()
        {
            UpgradeId[] weapons = { UpgradeId.Hose, UpgradeId.WaterBomb, UpgradeId.Drone, UpgradeId.Partner, UpgradeId.Curtain, UpgradeId.Turret };
            UpgradeId[] passives = { UpgradeId.Tank, UpgradeId.Boots, UpgradeId.Radio, UpgradeId.Axe, UpgradeId.Oxygen, UpgradeId.Suit };
            UpgradeId[] evolutions = { UpgradeId.Cannon, UpgradeId.Squad, UpgradeId.AirBomb, UpgradeId.RescueDrone, UpgradeId.WaterWall, UpgradeId.RescuePost };
            foreach (UpgradeId id in weapons)
            {
                Assert.True(Loadout.IsWeapon(id) && !Loadout.IsSpecial(id), id + "는 무기");
                Assert.NotNull(Loadout.EvolutionOf(id));
            }
            foreach (UpgradeId id in passives) Assert.True(Loadout.IsPassive(id) && !Loadout.IsSpecial(id), id + "는 보조");
            var pairs = new HashSet<UpgradeId>();
            foreach (UpgradeId id in evolutions)
            {
                Assert.True(Loadout.IsEvolution(id) && Loadout.IsSpecial(id));
                Assert.Contains(Loadout.BaseOf(id), weapons);
                Assert.Contains(Loadout.PairOf(id), passives);
                pairs.Add(Loadout.PairOf(id));
            }
            Assert.Equal(6, pairs.Count);
            Assert.Equal(Loadout.WeaponSlots, 4);
            Assert.Equal(Loadout.PassiveSlots, 4);
        }

        [Theory]
        [InlineData(UpgradeId.Cannon)]
        [InlineData(UpgradeId.Squad)]
        [InlineData(UpgradeId.AirBomb)]
        [InlineData(UpgradeId.RescueDrone)]
        [InlineData(UpgradeId.WaterWall)]
        [InlineData(UpgradeId.RescuePost)]
        public void MaxWeaponPlusPair_OffersItsEvolution_ThatTakesTheWeaponSlot(UpgradeId evolution)
        {
            SurvivorSim sim = Quiet();
            Evolve(sim, evolution);
            Assert.Equal(1, sim.Build.Level(evolution));
            Assert.Equal(0, sim.Build.Level(Loadout.BaseOf(evolution)));
            Assert.True(sim.Build.Has(Loadout.BaseOf(evolution)));
            Assert.False(sim.Build.CanTake(Loadout.BaseOf(evolution)), "진화한 무기를 다시 얻으면 안 된다");
        }

        [Fact]
        public void WaterBomb_GoesForTheBurningBuildingFirst()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 6f, 0f);
            sim.Ignite(shop, 0.8f);
            sim.Spawn(EnemyKind.Blaze, new Vec2(sim.Player.X - 5f, sim.Player.Y));
            Take(sim, UpgradeId.WaterBomb);
            Shot bomb = null;
            for (int i = 0; i < 60 * 3 && bomb == null; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                bomb = sim.Shots.Find(s => s.Kind == ShotKind.Bomb);
            }
            Assert.NotNull(bomb);
            Assert.Equal(shop.Pos, bomb.Target);
            float before = shop.Fire;
            Run(sim, 0.6f);
            Assert.True(shop.Fire < before, "물폭탄이 건물 불을 못 줄였다");
        }

        [Fact]
        public void PatrolDrone_FliesToTheBurningBuilding_AndDousesIt()
        {
            SurvivorSim control = Quiet();
            Structure c = Shop(control, 9f, 0f);
            control.Ignite(c, 0.8f);
            Run(control, 4f);

            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 9f, 0f);
            sim.Ignite(shop, 0.8f);
            Take(sim, UpgradeId.Drone, 3);
            Run(sim, 4f);
            Assert.Same(shop, sim.DroneTarget);
            Assert.True(sim.DroneCenter.DistanceTo(shop.Pos) < 0.6f, "드론이 건물 위로 안 갔다");
            Assert.True(shop.Fire < c.Fire - 0.1f, "드론 불 " + shop.Fire + " / 없을 때 " + c.Fire);
        }

        [Fact]
        public void Partners_GrowWithLevel_AndDouseWhileRescuing()
        {
            SurvivorSim control = Quiet();
            Structure c = Shop(control, 12f, 0f, 3);
            control.Ignite(c, 0.5f);
            Run(control, 8f);

            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 12f, 0f, 3);
            sim.Ignite(shop, 0.5f);
            Take(sim, UpgradeId.Partner);
            Run(sim, 8f);
            Assert.Single(sim.Partners);
            Assert.True(sim.Rescued >= 1, "대원이 못 구했다");
            Assert.True(shop.Fire < c.Fire, "대원이 불을 안 줄였다");

            Take(sim, UpgradeId.Partner, 2);
            Run(sim, 0.1f);
            Assert.Equal(2, sim.Partners.Count);
            Take(sim, UpgradeId.Partner, 2);
            Run(sim, 0.1f);
            Assert.Equal(3, sim.Partners.Count);
        }

        [Fact]
        public void Turret_StandsWhereYouWere_AndShootsNearbyFire()
        {
            SurvivorSim sim = Quiet();
            Vec2 spot = sim.Player;
            Enemy blaze = sim.Spawn(EnemyKind.Blaze, new Vec2(spot.X + 2f, spot.Y));
            blaze.Speed = 0f;
            Take(sim, UpgradeId.Turret);
            for (int i = 0; i < 60; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
            }
            Assert.Single(sim.Turrets);
            Assert.True(sim.Turrets[0].Pos.DistanceTo(spot) < 0.1f);
            Assert.True(blaze.Hp < blaze.MaxHp || blaze.Dead, "포탑이 곁 불을 안 쐈다");
        }

        [Fact]
        public void Radio_ForecastsTheNextReport()
        {
            var sim = new SurvivorSim(1);
            sim.Enemies.Clear();
            Take(sim, UpgradeId.Radio);
            float report = sim.Stage.ReportTimes[0];
            Structure told = null;
            float lead = 0f;
            Structure lit = null;
            while (sim.Time < report + 0.5f && lit == null)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                if (told == null && sim.ForecastAt != null)
                {
                    told = sim.ForecastAt;
                    lead = report - sim.Time;
                }
                lit = sim.Ignited.Find(s => s.Kind == StructureKind.House);
            }
            Assert.NotNull(told);
            Assert.InRange(lead, 1.9f, 2.05f);
            Assert.Same(told, lit);
        }

        /// <summary>문 앞에 서서 첫 사람을 구하기까지 걸린 초.</summary>
        private static float TimeToRescue(int axe)
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 0f, 5f, 2);
            sim.Ignite(shop, 0.3f);
            sim.Player = shop.Door;
            if (axe > 0) Take(sim, UpgradeId.Axe, axe);
            float start = sim.Time;
            for (int i = 0; i < 600 && sim.Rescued == 0; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
            }
            return sim.Time - start;
        }

        [Fact]
        public void Axe_MakesRescueFaster()
        {
            float plain = TimeToRescue(0);
            float axe = TimeToRescue(2);
            Assert.InRange(axe / plain, 0.62f, 0.72f);
        }

        /// <summary>큰 불 속에 갇힌 한 명을 연기로 잃기까지 걸린 초.</summary>
        private static float TimeToLose(int oxygen)
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 20f, 0f, 1);
            sim.Ignite(shop, 1f);
            shop.Integrity = 100f;
            if (oxygen > 0) Take(sim, UpgradeId.Oxygen, oxygen);
            for (int i = 0; i < 60 * 40 && sim.CiviliansLost == 0; i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
            }
            return sim.Time;
        }

        [Fact]
        public void Oxygen_KeepsTrappedPeopleAliveLonger()
        {
            float plain = TimeToLose(0);
            float oxygen = TimeToLose(1);
            Assert.InRange(oxygen / plain, 1.15f, 1.25f);
        }

        [Fact]
        public void Squad_FieldsFourPartners()
        {
            SurvivorSim sim = Quiet();
            Evolve(sim, UpgradeId.Squad);
            Run(sim, 0.1f);
            Assert.Equal(4, sim.Partners.Count);
        }

        [Fact]
        public void AirBomb_HitsBurningBuildingsAnywhere()
        {
            SurvivorSim sim = Quiet();
            Evolve(sim, UpgradeId.AirBomb);
            Structure far = Shop(sim, 25f, 20f);
            sim.Ignite(far, 0.8f);
            bool hit = false;
            for (int i = 0; i < 60 * 4 && !hit; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                hit = sim.AirBlasts.Exists(p => p.DistanceTo(far.Pos) < 0.1f);
            }
            Assert.True(hit, "멀리 불난 건물에 소화탄이 안 떨어졌다");
        }

        [Fact]
        public void RescueDrone_LiftsPeopleOut()
        {
            SurvivorSim sim = Quiet();
            Evolve(sim, UpgradeId.RescueDrone);
            // 큰 불: 드론이 끄기 전에 먼저 끌어올리는 걸 본다(불이 꺼지면 갇힌 사람도 풀린다).
            Structure shop = Shop(sim, 8f, 0f, 2);
            sim.Ignite(shop, 1f);
            Run(sim, 4f);
            Assert.True(sim.Rescued >= 1, "구조 드론이 한 명도 못 구했다");
            Assert.True(shop.Door.DistanceTo(sim.Player) > SurvivorSim.RescueRange);
            Assert.Empty(sim.Partners);
        }

        [Fact]
        public void WaterWall_IsABiggerRing()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Curtain);
            float ring = sim.CurtainRadiusNow;
            Evolve(sim, UpgradeId.WaterWall);
            Assert.True(sim.CurtainRadiusNow > ring + 2f);
        }

        [Fact]
        public void RescuePost_StopsSmokeNearTheTurret()
        {
            SurvivorSim sim = Quiet();
            Evolve(sim, UpgradeId.RescuePost);
            Structure shop = Shop(sim, 0f, 5f, 1);
            sim.Ignite(shop, 1f);
            shop.Integrity = 100f;
            // 소방관은 가만히 서 있고(포탑이 여기 선다), 문 앞엔 안 간다.
            sim.Player = new Vec2(shop.Pos.X + 3.5f, shop.Pos.Y - 2f);
            Run(sim, SurvivorSim.SmokeTime + 5f);
            Assert.Equal(0, sim.CiviliansLost);
            Assert.Equal(1, shop.Residents);
        }

        [Fact]
        public void Ambulance_ClearsTheThickestSmoke()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 20f, 0f, 2);
            sim.Ignite(shop, 1f);
            shop.Integrity = 100f;
            Take(sim, UpgradeId.Ambulance);
            Structure came = null;
            for (int i = 0; i < 60 * 6 && came == null; i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                came = sim.AmbulanceAt;
            }
            Assert.Same(shop, came);
            Assert.True(shop.Smoke < 0.1f);
        }

        [Fact]
        public void Truck_DrivesTheRowOfTheWorstFire()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 3f, 10f);
            sim.Ignite(shop, 0.9f);
            Take(sim, UpgradeId.Truck);
            for (int i = 0; i < 60 * 3 && !sim.Truck.HasValue; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
            }
            Assert.True(sim.Truck.HasValue);
            Assert.Equal(shop.Pos.Y, sim.Truck.Value.Y, 2);
        }
    }
}
