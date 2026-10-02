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

        /// <summary>드론 level로 seconds 동안 돌며 물폭탄을 몇 번 떨어뜨렸나. shop은 9칸 앞 불 0.8 가게.</summary>
        private static int DroneDrops(int level, float seconds, out Structure shop, out SurvivorSim sim)
        {
            sim = Quiet();
            shop = Shop(sim, 9f, 0f);
            sim.Ignite(shop, 0.8f);
            Take(sim, UpgradeId.Drone, level);
            int drops = 0;
            int ticks = (int)(seconds / SurvivorSim.Dt);
            for (int i = 0; i < ticks; i++)
            {
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(NoPartner(sim.PendingChoices));
                sim.Step(0f, 0f);
                drops += sim.DroneDrops.Count;
            }
            return drops;
        }

        [Fact]
        public void PatrolDrone_DivesAndDropsWater()
        {
            SurvivorSim control = Quiet();
            Structure c = Shop(control, 9f, 0f);
            control.Ignite(c, 0.8f);
            Run(control, 4f);

            int drops = DroneDrops(1, 4f, out Structure shop, out SurvivorSim sim);
            Assert.Same(shop, sim.DroneTarget);
            Assert.True(drops >= 1, "4초 동안 물폭탄을 한 번도 안 떨어뜨렸다");
            Assert.True(shop.Fire < c.Fire - 0.15f, "드론 불 " + shop.Fire + " / 없을 때 " + c.Fire);

            // 드론이 많을수록 자주 떨어뜨린다.
            int many = DroneDrops(5, 4f, out _, out _);
            Assert.True(many > drops, "Lv5 투하 " + many + "회가 Lv1 " + drops + "회보다 많아야 한다");
        }

        [Fact]
        public void PatrolDrone_WithoutAFire_CirclesThePlayer_AndDropsNothing()
        {
            int drops = 0;
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Drone);
            for (int i = 0; i < 120; i++)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
                drops += sim.DroneDrops.Count;
            }
            Assert.Null(sim.DroneTarget);
            Assert.Equal(0, drops);
            Assert.True(sim.Drones[0].DistanceTo(sim.Player) < 4f);
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

        /// <summary>펌프 level로 세기 1.0 가게에 물대포를 쏴 첫 증기 폭발까지 걸린 틱. 폭발 틱에 dx칸 밖 불씨가 맞았는지도 돌려준다.</summary>
        private static int TicksToSteam(int tank, float emberDx, out bool emberHit)
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 6f, 0f);
            sim.Ignite(shop, 1f);
            if (tank > 0) Take(sim, UpgradeId.Tank, tank);
            Enemy ember = sim.Spawn(EnemyKind.Ember, new Vec2(shop.Pos.X + shop.Half.X + emberDx, shop.Pos.Y));
            ember.Speed = 0f;
            ember.MaxHp = 999f;
            ember.Hp = 999f;
            emberHit = false;
            for (int i = 0; i < 600; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Aim = new Vec2(shop.Pos.X - sim.Player.X, shop.Pos.Y - sim.Player.Y);
                sim.Spraying = true;
                sim.Step(0f, 0f);
                if (sim.SteamBursts.Count > 0)
                {
                    // 스폰된 다른 불씨가 맞은 것과 구분하려고 우리 불씨의 체력으로 본다.
                    emberHit = ember.Hp < 999f;
                    return i;
                }
            }
            return int.MaxValue;
        }

        [Fact]
        public void Tank_ChargesSteamFaster_AndWidensTheBurst()
        {
            int plain = TicksToSteam(0, 5f, out bool plainHit);
            int pump = TicksToSteam(3, 5f, out bool pumpHit);
            Assert.True(pump < plain, "펌프 Lv3 " + pump + "틱이 펌프 없음 " + plain + "틱보다 빨라야 한다");
            Assert.False(plainHit, "펌프 없이는 5칸 밖 불씨에 증기가 안 닿아야 한다(반경 4)");
            Assert.True(pumpHit, "펌프 Lv3이면 5칸 밖 불씨에 증기가 닿아야 한다(반경 5.5)");
            Assert.Equal(1f, new Loadout().SteamScale);
            Assert.Equal(0f, new Loadout().SteamRadiusBonus);
        }

        [Fact]
        public void Tank_AlsoPowersTheCannon()
        {
            SurvivorSim plain = Quiet();
            Evolve(plain, UpgradeId.Cannon);
            SurvivorSim pumped = Quiet();
            Evolve(pumped, UpgradeId.Cannon);
            Take(pumped, UpgradeId.Tank, 4);   // Evolve가 짝 보조를 하나 준다 → Lv5
            Assert.Equal(5, pumped.Build.Level(UpgradeId.Tank));
            foreach (SurvivorSim sim in new[] { plain, pumped })
            {
                sim.Enemies.Clear();
                sim.Aim = new Vec2(1f, 0f);
                sim.Spraying = true;
                sim.Step(0f, 0f);
            }
            Shot a = plain.Shots.Find(sh => sh.Hose);
            Shot b = pumped.Shots.Find(sh => sh.Hose);
            Assert.NotNull(a);
            Assert.NotNull(b);
            // Evolve가 짝 보조(펌프)를 하나 주므로 plain은 Lv1(1.15), pumped는 Lv5(1.75).
            Assert.InRange(b.Damage / a.Damage, 1.75f / 1.15f - 0.01f, 1.75f / 1.15f + 0.01f);
            Assert.InRange(b.Life / a.Life, 1.75f / 1.15f - 0.01f, 1.75f / 1.15f + 0.01f);
        }

        /// <summary>무전기 Lv1로 첫 신고를 맞는다. near면 예보 건물 곁(문 앞 3칸)으로 미리 옮겨 둔다.</summary>
        private static (Structure lit, float fire, int earlyCalls, int gemAtDoor) FirstReport(bool radio, bool near)
        {
            var sim = new SurvivorSim(1);
            sim.Enemies.Clear();
            if (radio) Take(sim, UpgradeId.Radio);
            float report = sim.Stage.ReportTimes[0];
            Structure lit = null;
            float fire = 0f;
            int gem = 0;
            while (sim.Time < report + 0.5f && lit == null)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                if (near && sim.ForecastAt != null) sim.Player = new Vec2(sim.ForecastAt.Door.X, sim.ForecastAt.Door.Y - 3f);
                else if (!near) sim.Player = new Vec2(SurvivorSim.ArenaSize / 2f, SurvivorSim.ArenaSize / 2f);
                sim.Step(0f, 0f);
                if (sim.Ignited.Count > 0)
                {
                    lit = sim.Ignited[0];
                    fire = lit.Fire;
                    Gem g = sim.Gems.Find(x => x.Pos.DistanceTo(lit.Door) < 0.01f);
                    gem = g != null ? g.Value : 0;
                }
            }
            Assert.NotNull(lit);
            return (lit, fire, sim.Stats.EarlyCalls, gem);
        }

        [Fact]
        public void Radio_EarlyCall_ShrinksTheReportFire()
        {
            var near = FirstReport(true, true);
            Assert.Equal(SurvivorSim.EarlyFire, near.fire, 2);
            Assert.Equal(1, near.earlyCalls);
            Assert.Equal(SurvivorSim.EarlyGem, near.gemAtDoor);

            var far = FirstReport(true, false);
            Assert.Equal(SurvivorSim.ReportFire, far.fire, 2);
            Assert.Equal(0, far.earlyCalls);

            var none = FirstReport(false, true);
            Assert.Equal(SurvivorSim.ReportFire, none.fire, 2);
            Assert.Equal(0, none.earlyCalls);
        }

        [Fact]
        public void Describe_MentionsTheVisibleAction()
        {
            Assert.Contains("투하", SurvivorUpgrades.Describe(UpgradeId.Drone, 1));
            Assert.Contains("물줄기 +25%", SurvivorUpgrades.Describe(UpgradeId.Partner, 4));
            Assert.DoesNotContain("구조", SurvivorUpgrades.Describe(UpgradeId.Partner, 4));
            foreach (int lv in new[] { 2, 3, 4, 5 }) Assert.Contains("지속 +1초", SurvivorUpgrades.Describe(UpgradeId.Turret, lv));
            Assert.Contains("증기", SurvivorUpgrades.Describe(UpgradeId.Tank, 1));
            Assert.Contains("불 바닥", SurvivorUpgrades.Describe(UpgradeId.Boots, 1));
            Assert.Contains("선제 출동", SurvivorUpgrades.Describe(UpgradeId.Radio, 1));
            Assert.Contains("한 번에 2명", SurvivorUpgrades.Describe(UpgradeId.Axe, 1));
            Assert.Contains("산소통을 던져", SurvivorUpgrades.Describe(UpgradeId.Oxygen, 1));
            Assert.Contains("튕겨", SurvivorUpgrades.Describe(UpgradeId.Suit, 1));
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
        public void Axe_BringsOutTwoAtOnce()
        {
            // 도끼 없이는 한 명씩, 구조 시간은 그대로 1.2초.
            float plain = TimeToRescue(0);
            Assert.InRange(plain, SurvivorSim.RescueTime - 0.05f, SurvivorSim.RescueTime + 0.1f);
            Assert.Equal(1f, new Loadout().RescueScale);

            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 0f, 5f, 3);
            sim.Ignite(shop, 0.3f);
            sim.Player = shop.Door;
            Take(sim, UpgradeId.Axe);
            int xp = sim.Xp;
            int level = sim.Level;
            int need = sim.XpToNext;
            bool burst = false;
            for (int i = 0; i < 600 && sim.Rescued == 0; i++)
            {
                sim.Hp = sim.MaxHp;
                xp = sim.Xp;
                level = sim.Level;
                need = sim.XpToNext;
                sim.Step(0f, 0f);
                burst = sim.DoorBursts.Contains(shop);
            }
            Assert.Equal(2, sim.Rescued);
            Assert.Equal(1, shop.Residents);
            Assert.True(burst, "문을 부순 신호가 없다");
            Assert.Equal(40, sim.Xp - xp + (sim.Level > level ? need : 0));
            Assert.Equal(2, sim.Civilians.Count);

            // 남은 사람보다 많이 세지 않는다: Lv5 도끼(+3)라도 2명 남았으면 2명.
            SurvivorSim two = Quiet();
            Structure small = Shop(two, 0f, 5f, 2);
            two.Ignite(small, 0.3f);
            two.Player = small.Door;
            Take(two, UpgradeId.Axe, 5);
            for (int i = 0; i < 600 && two.Rescued == 0; i++)
            {
                two.Hp = two.MaxHp;
                if (two.PendingChoices != null) two.Choose(NoPartner(two.PendingChoices));
                two.Step(0f, 0f);
            }
            Assert.Equal(2, two.Rescued);
            Assert.Equal(0, small.Residents);
        }

        [Fact]
        public void Boots_WalkOverGroundFire_PutsItOutUnharmed()
        {
            SurvivorSim sim = Quiet();
            sim.BurningGround.Add(new Puddle { Pos = sim.Player, Radius = 0.9f, Life = 5f, MaxLife = 5f });
            Take(sim, UpgradeId.Boots);
            int prints = 0;
            int outs = 0;
            for (int i = 0; i < 60; i++)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
                prints += sim.Footprints.Count;
                outs += sim.Extinguished.Count;
            }
            Assert.Equal(0f, sim.Stats.DamageTaken);
            Assert.True(prints >= 1, "발자국이 없다");
            Assert.True(outs >= 1, "밟은 바닥 불이 꺼졌다는 신호가 없다");
            Assert.DoesNotContain(sim.BurningGround, p => !p.Out && p.Life > 0f);

            // 장화 없이는 데고, 불도 남는다.
            SurvivorSim plain = Quiet();
            plain.BurningGround.Add(new Puddle { Pos = plain.Player, Radius = 0.9f, Life = 5f, MaxLife = 5f });
            for (int i = 0; i < 60; i++)
            {
                plain.Enemies.Clear();
                plain.Step(0f, 0f);
            }
            Assert.True(plain.Stats.DamageTaken > 0f);
            Assert.Contains(plain.BurningGround, p => !p.Out && p.Life > 0f);
        }

        [Fact]
        public void Suit_BouncesTouchingEmbers()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Suit);
            Enemy ember = sim.Spawn(EnemyKind.Ember, new Vec2(sim.Player.X + 0.3f, sim.Player.Y));
            ember.Speed = 0f;
            ember.MaxHp = 999f;
            ember.Hp = 999f;
            sim.Step(0f, 0f);
            Assert.Single(sim.SuitBounces);
            Assert.True(ember.Knock.X > 0f, "불씨가 플레이어 반대쪽으로 튕겨야 한다");
            // 0.3초 안에는 같은 불씨를 다시 안 튕긴다.
            ember.Pos = new Vec2(sim.Player.X + 0.3f, sim.Player.Y);
            sim.Step(0f, 0f);
            Assert.Empty(sim.SuitBounces);

            SurvivorSim plain = Quiet();
            Enemy e2 = plain.Spawn(EnemyKind.Ember, new Vec2(plain.Player.X + 0.3f, plain.Player.Y));
            e2.Speed = 0f;
            e2.MaxHp = 999f;
            e2.Hp = 999f;
            plain.Step(0f, 0f);
            Assert.Empty(plain.SuitBounces);
            Assert.Equal(0f, e2.Knock.X);
        }

        /// <summary>큰 불 속에 갇힌 한 명을 연기로 잃기까지 걸린 초.</summary>
        [Fact]
        public void Oxygen_ThrowsATank_ThatResetsTheSmoke()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 8f, 0f, 1);
            sim.Ignite(shop, 1f);
            shop.Integrity = 100f;
            shop.Smoke = 12f;
            Take(sim, UpgradeId.Oxygen);
            bool hit = false;
            for (int i = 0; i < 60 * 6 && !hit; i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                if (sim.OxygenHits.Contains(shop))
                {
                    hit = true;
                    Assert.Equal(0f, shop.Smoke);
                    Assert.True(shop.Shield > 0f);
                }
            }
            Assert.True(hit, "6초 안에 산소통이 안 떨어졌다");
            Assert.Equal(0, sim.CiviliansLost);
            // 연기 시계가 되돌려졌으니 15초 동안은 아무도 잃지 않는다(원래는 3초 뒤에 잃었다).
            for (int i = 0; i < 60 * 14; i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
            }
            Assert.Equal(0, sim.CiviliansLost);
        }

        [Fact]
        public void Oxygen_WaitsWhenNoOneIsTrapped()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 8f, 0f, 0);
            sim.Ignite(shop, 1f);
            Take(sim, UpgradeId.Oxygen);
            int hits = 0;
            for (int i = 0; i < 60 * 10; i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                hits += sim.OxygenHits.Count;
            }
            Assert.Equal(0, hits);
            Assert.DoesNotContain(sim.Shots, s => s.Kind == ShotKind.Oxygen);
            Assert.Equal(0f, new Loadout().OxygenEvery);
            Take(sim, UpgradeId.Oxygen, 4);
            Assert.Equal(4f, sim.Build.OxygenEvery, 3);
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
