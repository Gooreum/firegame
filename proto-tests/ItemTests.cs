using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 아이템 표(2026-10-07): 무기 10 · 보조 3 · 진화 10, 무기·보조 합쳐 4칸, 무기마다 짝 보조로 진화.
    /// 새 무기 9종의 동작은 ArsenalTests가 본다.
    /// </summary>
    public class ItemTests
    {
        private static SurvivorSim Quiet(int stage = 1)
        {
            var sim = new SurvivorSim(1, stage) { Guardian = false };
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
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
            }
        }

        private static Structure Shop(SurvivorSim sim, float dx, float dy, int residents = 0)
        {
            var s = new Structure { Kind = StructureKind.House, Name = "가게", Pos = new Vec2(sim.Player.X + dx, sim.Player.Y + dy), Half = new Vec2(2f, 1.5f), Residents = residents };
            sim.Structures.Add(s);
            return s;
        }

        /// <summary>무기를 Lv5까지, 짝 보조를 Lv3까지 고른 뒤, 구슬을 먹어 레벨업한 카드에서 진화를 고른다.</summary>
        private static void Evolve(SurvivorSim sim, UpgradeId evolution)
        {
            UpgradeId weapon = Loadout.BaseOf(evolution);
            while (sim.Build.Level(weapon) < Loadout.MaxLevel) Take(sim, weapon);
            while (sim.Build.Level(Loadout.PairOf(evolution)) < Loadout.EvolvePair) Take(sim, Loadout.PairOf(evolution));
            sim.DropGem(sim.Player, sim.XpToNext);
            for (int i = 0; i < 3 && sim.PendingChoices == null; i++) sim.Step(0f, 0f);
            Assert.NotNull(sim.PendingChoices);
            int pick = sim.PendingChoices.IndexOf(evolution);
            Assert.True(pick >= 0, evolution + " 진화 카드가 안 나왔다: " + string.Join(",", sim.PendingChoices));
            sim.Choose(pick);
        }

        [Fact]
        public void Items_AreTenWeaponsThreePassivesTenEvolutions()
        {
            UpgradeId[] weapons = { UpgradeId.Hose, UpgradeId.Dog, UpgradeId.Balloon, UpgradeId.Extinguisher, UpgradeId.Mine, UpgradeId.Foam, UpgradeId.Bubble, UpgradeId.Manhole, UpgradeId.Ladder, UpgradeId.Whip };
            UpgradeId[] passives = { UpgradeId.Tank, UpgradeId.Boots, UpgradeId.Suit };
            var pairs = new Dictionary<UpgradeId, int>();
            foreach (UpgradeId id in weapons)
            {
                Assert.True(Loadout.IsWeapon(id) && !Loadout.IsEvolution(id), id + "는 무기");
                UpgradeId? evo = Loadout.EvolutionOf(id);
                Assert.NotNull(evo);
                Assert.True(Loadout.IsEvolution(evo.Value) && Loadout.IsWeapon(evo.Value));
                Assert.Equal(id, Loadout.BaseOf(evo.Value));
                UpgradeId pair = Loadout.PairOf(evo.Value);
                Assert.Contains(pair, passives);
                pairs[pair] = pairs.TryGetValue(pair, out int n) ? n + 1 : 1;
            }
            // 짝 보조: 펌프 넷, 장화 셋, 방화복 셋.
            Assert.Equal(4, pairs[UpgradeId.Tank]);
            Assert.Equal(3, pairs[UpgradeId.Boots]);
            Assert.Equal(3, pairs[UpgradeId.Suit]);
            int evolutions = 0;
            for (int i = 0; i < (int)UpgradeId.Heal; i++)
            {
                var id = (UpgradeId)i;
                Assert.Equal(System.Array.IndexOf(passives, id) >= 0, Loadout.IsPassive(id));
                Assert.True(Loadout.IsWeapon(id) || Loadout.IsPassive(id), id + "는 무기도 보조도 아니다");
                if (Loadout.IsEvolution(id)) evolutions++;
            }
            Assert.Equal(10, evolutions);
            Assert.Equal(4, Loadout.Slots);
        }

        [Fact]
        public void Loadout_HoldsFourItems_WeaponsAndPassivesTogether()
        {
            var l = new Loadout();
            l.Add(UpgradeId.Hose);
            l.Add(UpgradeId.Whip);
            l.Add(UpgradeId.Tank);
            Assert.True(l.CanTake(UpgradeId.Dog), "셋일 땐 넷째를 들 수 있어야 한다");
            Assert.True(l.CanTake(UpgradeId.Suit), "넷째는 보조여도 된다");
            l.Add(UpgradeId.Suit);
            Assert.Equal(4, l.WeaponCount + l.PassiveCount);
            // 넷을 들면 올릴 수는 있어도 새로 들 것은 없다.
            Assert.True(l.CanTake(UpgradeId.Whip));
            Assert.True(l.CanTake(UpgradeId.Tank));
            for (int i = 0; i < (int)UpgradeId.Heal; i++)
            {
                var id = (UpgradeId)i;
                if (Loadout.IsEvolution(id) || l.Level(id) > 0) continue;
                Assert.False(l.CanTake(id), "네 칸이 찼는데 " + id + "를 새로 들 수 있다");
            }
            var rng = new FireGame.Core.Sim.Rng(7);
            for (int k = 0; k < 30; k++)
            {
                foreach (UpgradeId id in SurvivorUpgrades.Roll(l, 5, ref rng)) Assert.True(l.Level(id) > 0, "넷 찬 뒤 새 카드 " + id);
            }
        }

        [Fact]
        public void Loadout_WithThreeItems_CanStillPickANewOne()
        {
            var l = new Loadout();
            l.Add(UpgradeId.Hose);
            l.Add(UpgradeId.Mine);
            l.Add(UpgradeId.Boots);
            var rng = new FireGame.Core.Sim.Rng(3);
            bool fresh = false;
            for (int k = 0; k < 30 && !fresh; k++)
            {
                foreach (UpgradeId id in SurvivorUpgrades.Roll(l, 5, ref rng)) fresh |= l.Level(id) == 0;
            }
            Assert.True(fresh, "셋일 땐 새 아이템 카드가 나와야 한다");
        }

        [Theory]
        [InlineData(UpgradeId.Cannon)]
        [InlineData(UpgradeId.DogPack)]
        [InlineData(UpgradeId.BalloonStorm)]
        [InlineData(UpgradeId.Tornado)]
        [InlineData(UpgradeId.IceField)]
        [InlineData(UpgradeId.Avalanche)]
        [InlineData(UpgradeId.BubbleFall)]
        [InlineData(UpgradeId.Waterline)]
        [InlineData(UpgradeId.LadderBridge)]
        [InlineData(UpgradeId.Whirl)]
        public void MaxWeaponPlusPair_OffersItsEvolution_ThatTakesTheWeaponSlot(UpgradeId evolution)
        {
            SurvivorSim sim = Quiet();
            Evolve(sim, evolution);
            Assert.Equal(1, sim.Build.Level(evolution));
            Assert.Equal(0, sim.Build.Level(Loadout.BaseOf(evolution)));
            Assert.True(sim.Build.Has(Loadout.BaseOf(evolution)));
            Assert.False(sim.Build.CanTake(Loadout.BaseOf(evolution)), "진화한 무기를 다시 얻으면 안 된다");
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
            Take(pumped, UpgradeId.Tank, Loadout.MaxLevel - Loadout.EvolvePair);   // Evolve가 짝 보조를 EvolvePair까지 준다 → Lv5
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
            // Evolve가 짝 보조(펌프)를 EvolvePair까지 주므로 plain은 1 + 0.15·EvolvePair, pumped는 Lv5(1.75).
            float ratio = 1.75f / (1f + (0.15f * Loadout.EvolvePair));
            Assert.InRange(b.Damage / a.Damage, ratio - 0.01f, ratio + 0.01f);
            Assert.InRange(b.Life / a.Life, ratio - 0.01f, ratio + 0.01f);
        }

        [Fact]
        public void Describe_ShowsTheVisibleChange_NotPercentages()
        {
            for (int i = 0; i < (int)UpgradeId.Heal; i++)
            {
                var id = (UpgradeId)i;
                for (int lv = 1; lv <= Loadout.MaxLevelOf(id); lv++)
                {
                    string text = SurvivorUpgrades.Describe(id, lv);
                    Assert.False(string.IsNullOrEmpty(text), id + " Lv" + lv);
                    Assert.DoesNotContain("%", text);
                    Assert.NotEqual("응급 처치", SurvivorUpgrades.Name(id));
                }
            }
            Assert.Contains("2마리", SurvivorUpgrades.Describe(UpgradeId.Dog, 3));
            Assert.Contains("두 갈래", SurvivorUpgrades.Describe(UpgradeId.Whip, 3));
            Assert.Contains("세 방향", SurvivorUpgrades.Describe(UpgradeId.Ladder, 5));
            Assert.Contains("증기", SurvivorUpgrades.Describe(UpgradeId.Tank, 1));
            Assert.Contains("불 바닥", SurvivorUpgrades.Describe(UpgradeId.Boots, 1));
            Assert.Contains("튕겨", SurvivorUpgrades.Describe(UpgradeId.Suit, 1));
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

    }
}
