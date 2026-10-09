using System.Collections.Generic;
using System.Linq;
using FireGame.Core.Sim;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 숲 개편(2026-10-08): 칸 제한 없음 · 무기 8 + 수치형 보조 5 · 무기 Lv5 + 짝 보조면 Lv6 최고급 · 레벨 피해 배율 · 구조대원.
    /// 숲(2스테이지)에서만 켜지고 다른 스테이지는 그대로다.
    /// </summary>
    public class ForestLevelUpTests
    {
        private const int Forest = 2;

        private static SurvivorSim Quiet(int stage = Forest)
        {
            var sim = new SurvivorSim(1, stage) { Guardian = false };
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        private static void Take(SurvivorSim sim, UpgradeId id, int times = 1)
        {
            for (int i = 0; i < times; i++)
            {
                sim.PendingChoices = new List<UpgradeId> { id };
                sim.Choose(0);
            }
        }

        private static HashSet<UpgradeId> Offered(Loadout l, int rolls = 500)
        {
            var rng = new Rng(11);
            var seen = new HashSet<UpgradeId>();
            for (int i = 0; i < rolls; i++) foreach (UpgradeId id in SurvivorUpgrades.Roll(l, 2, ref rng)) seen.Add(id);
            return seen;
        }

        // --- Step 1 ---
        [Fact]
        public void Forest_IsFree_OtherStagesAreNot()
        {
            Assert.True(new SurvivorSim(1, Forest).Build.Free);
            foreach (int stage in new[] { 1, 3, 4, 5 }) Assert.False(new SurvivorSim(1, stage).Build.Free);
        }

        [Fact]
        public void Forest_NoSlotCap_EveryItemStillOffered()
        {
            var l = new Loadout { Free = true };
            foreach (UpgradeId id in new[] { UpgradeId.Hose, UpgradeId.Sprinkler, UpgradeId.Balloon, UpgradeId.Mine, UpgradeId.Chain, UpgradeId.Boots, UpgradeId.Suit, UpgradeId.Nozzle }) l.Add(id);
            HashSet<UpgradeId> seen = Offered(l);
            foreach (UpgradeId id in new[] { UpgradeId.Extinguisher, UpgradeId.Bubble, UpgradeId.Manhole, UpgradeId.Feed, UpgradeId.Wide }) Assert.Contains(id, seen);
        }

        [Fact]
        public void Town_FourSlots_StillCap()
        {
            var l = new Loadout();
            foreach (UpgradeId id in new[] { UpgradeId.Hose, UpgradeId.Chain, UpgradeId.Mine, UpgradeId.Suit }) l.Add(id);
            Assert.Equal(new HashSet<UpgradeId> { UpgradeId.Hose, UpgradeId.Chain, UpgradeId.Mine, UpgradeId.Suit }, Offered(l));
        }

        // --- 아이템 정리(2026-10-08): 무기 8 + 수치형 보조 5 ---
        private static readonly UpgradeId[] FreeCut = { UpgradeId.Tank, UpgradeId.OverPump, UpgradeId.Whip, UpgradeId.Whirl, UpgradeId.Foam, UpgradeId.Avalanche, UpgradeId.JetBoots, UpgradeId.PhoenixSuit };
        private static readonly UpgradeId[] NewPassives = { UpgradeId.Nozzle, UpgradeId.Feed, UpgradeId.Wide };

        [Fact]
        public void Forest_Offers_NoCutItems_AndTheNewPassives()
        {
            // 판 내내 고른다: 처음(물대포만)부터 무기·보조 다 Lv5까지.
            var l = new Loadout { Free = true };
            l.Add(UpgradeId.Hose);
            var seen = new HashSet<UpgradeId>();
            var rng = new Rng(5);
            for (int i = 0; i < 400; i++)
            {
                List<UpgradeId> cards = SurvivorUpgrades.Roll(l, 2, ref rng);
                foreach (UpgradeId id in cards) seen.Add(id);
                if (cards.Count > 0) l.Add(cards[i % cards.Count]);
            }
            foreach (UpgradeId id in FreeCut) Assert.DoesNotContain(id, seen);
            foreach (UpgradeId id in NewPassives) Assert.Contains(id, seen);
            foreach (UpgradeId id in FreeCut) Assert.False(l.CanTake(id), id + "는 숲에서 못 든다");
        }

        [Fact]
        public void Town_NeverOffersNewPassives()
        {
            var l = new Loadout();
            l.Add(UpgradeId.Hose);
            Assert.All(NewPassives, id => Assert.DoesNotContain(id, Offered(l)));
            Assert.All(NewPassives, id => Assert.False(l.CanTake(id)));
            Assert.All(NewPassives, id => Assert.True(Loadout.IsPassive(id)));
        }

        [Fact]
        public void Forest_PassivesHaveNoLv6()
        {
            var sim = Quiet();
            foreach (UpgradeId p in new[] { UpgradeId.Boots, UpgradeId.Suit, UpgradeId.Nozzle, UpgradeId.Feed, UpgradeId.Wide }) Take(sim, p, Loadout.MaxLevel);
            HashSet<UpgradeId> seen = Offered(sim.Build);
            Assert.DoesNotContain(UpgradeId.JetBoots, seen);
            Assert.DoesNotContain(UpgradeId.PhoenixSuit, seen);
            Assert.Empty(sim.Build.ReadyEvolutions());
            Assert.Equal(5, sim.Build.PassiveCount);
        }

        /// <summary>구슬을 먹어 레벨업한 카드(화면 경로).</summary>
        private static List<UpgradeId> NextCards(SurvivorSim sim)
        {
            sim.DropGem(sim.Player, sim.XpToNext);
            for (int i = 0; i < 3 && sim.PendingChoices == null; i++) sim.Step(0f, 0f);
            Assert.NotNull(sim.PendingChoices);
            List<UpgradeId> cards = sim.PendingChoices;
            return cards;
        }

        [Fact]
        public void Forest_BalloonLv5_NeedsWide_ForBalloonStorm()
        {
            var sim = Quiet();
            Take(sim, UpgradeId.Balloon, Loadout.MaxLevel);
            Assert.False(sim.Build.Ready(UpgradeId.BalloonStorm), "광각 노즐 없이 물풍선 폭우가 나오면 안 된다");
            Assert.DoesNotContain(UpgradeId.BalloonStorm, NextCards(sim));
            sim.Choose(0);
            if (sim.Build.Level(UpgradeId.Wide) == 0) Take(sim, UpgradeId.Wide);
            Assert.True(sim.Build.Ready(UpgradeId.BalloonStorm));
            List<UpgradeId> cards = NextCards(sim);
            Assert.Contains(UpgradeId.BalloonStorm, cards);
            sim.Choose(cards.IndexOf(UpgradeId.BalloonStorm));
            Assert.Equal(6, sim.SampleLevel(UpgradeId.Balloon));
        }

        [Fact]
        public void Forest_WeaponLv5_CardNamesItsPair()
        {
            Assert.Contains("진화 짝: 광각 노즐", SurvivorUpgrades.DescribeFree(UpgradeId.Balloon, 5));
            Assert.Contains("진화 짝: 고압 노즐", SurvivorUpgrades.DescribeFree(UpgradeId.Hose, 5));
            Assert.DoesNotContain("진화 짝", SurvivorUpgrades.DescribeFree(UpgradeId.Balloon, 4));
            Assert.DoesNotContain("진화 짝", SurvivorUpgrades.DescribeFree(UpgradeId.BalloonStorm, 6));
        }

        /// <summary>몹 없이 오른쪽으로 쥐고 쏜다(감독 몹은 지운다).</summary>
        private static void Spray(SurvivorSim sim, float seconds, System.Action each = null)
        {
            int ticks = (int)System.Math.Round(seconds / SurvivorSim.Dt);
            for (int i = 0; i < ticks; i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                sim.Aim = new Vec2(1f, 0f);
                sim.Spraying = true;
                sim.Step(0f, 0f);
                each?.Invoke();
            }
        }

        [Fact]
        public void Forest_HoseLv1to5_IsTheAimedStreamOnly()
        {
            var sim = Quiet();
            for (int lv = 1; lv <= Loadout.MaxLevel; lv++)
            {
                if (lv > 1) Take(sim, UpgradeId.Hose);
                bool shot = false;
                Spray(sim, 0.5f, () => shot |= sim.Shots.Exists(s => s.Kind == ShotKind.Drop));
                Assert.True(shot, "Lv" + lv + " 겨눈 물줄기");
                Assert.DoesNotContain(sim.SampleItems, it => it is HoseItem);
                Assert.Null(sim.SampleItemOf(UpgradeId.Hose));
            }
            // 펌프 몫은 물대포 레벨이 갖는다.
            Assert.Equal(1f + (0.15f * 5f), sim.Build.HoseRange, 3);
        }

        [Fact]
        public void Forest_Cannon_FiresJet_AndNovaEvery2_2s()
        {
            var sim = Quiet();
            Take(sim, UpgradeId.Hose, Loadout.MaxLevel - 1);
            Take(sim, UpgradeId.Nozzle);
            List<UpgradeId> cards = NextCards(sim);
            Assert.Contains(UpgradeId.Cannon, cards);
            sim.Choose(cards.IndexOf(UpgradeId.Cannon));
            bool jet = false, drop = false;
            Spray(sim, 1f, () =>
            {
                jet |= sim.Shots.Exists(s => s.Kind == ShotKind.Jet);
                drop |= sim.Shots.Exists(s => s.Kind == ShotKind.Drop);
            });
            Assert.True(jet, "고압 방수포는 모두 꿰뚫는 Jet 한 줄기");
            Assert.False(drop);
            var h = Assert.IsType<HoseItem>(sim.SampleItemOf(UpgradeId.Hose));
            // 첫 대폭발 1.2초, 그 뒤 2.2초마다(급수 펌프 없음): 1.2 · 3.4 · 5.6초.
            Spray(sim, 1.2f + (HoseItem.BoomEvery * 2f) + 0.25f - 1f);
            Assert.Equal(3, h.Novas);
        }

        [Fact]
        public void Forest_LevelDamageMultiplier_BalloonLv3_OneShotsEmber()
        {
            Assert.Equal(new[] { 1f, 1f, 1.4f, 1.9f, 2.4f, 3f, 3.6f }, SurvivorSim.SLvMul);
            var sim = new SurvivorSim(1, Forest, new List<UpgradeId>()) { Guardian = false, Reports = false };
            sim.Enemies.Clear();
            sim.Structures.Clear();
            Take(sim, UpgradeId.Balloon, 3);
            // 레벨업 밀치기(DoBurst)가 지나간 뒤에 세운다.
            for (int i = 0; i < 30; i++)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
            }
            Enemy e = sim.Spawn(EnemyKind.Ember, SurvivorSim.FromS(sim.PX + 70f, sim.PY - 10f));
            e.Speed = 0f;
            bool partial = false;
            for (int i = 0; i < 600 && !e.Dead; i++)
            {
                sim.Enemies.RemoveAll(o => o != e);
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                partial |= !e.Dead && e.Hp < e.MaxHp;
            }
            Assert.True(e.Dead, "물풍선이 닿아야");
            Assert.False(partial, "Lv3 물풍선(3 × 1.4) 한 방에 작은 요괴가 쓰러져야");
        }

        [Fact]
        public void Forest_NozzleLv5_HitsHalfAgainHarder()
        {
            float Lost(int nozzle)
            {
                var sim = Quiet();
                Take(sim, UpgradeId.Nozzle, nozzle);
                Enemy e = sim.Spawn(EnemyKind.Ember, sim.Player);
                e.MaxHp = e.Hp = 1e6f;
                sim.SHit(e, 1f);
                return (1e6f - e.Hp) / sim.SDamageScale;
            }
            float plain = Lost(0), strong = Lost(Loadout.MaxLevel);
            // 치명(2배)이 어느 쪽에든 낄 수 있다.
            float ratio = strong / plain;
            Assert.True(System.Math.Abs(ratio - 1.5f) < 0.01f || System.Math.Abs(ratio - 3f) < 0.01f || System.Math.Abs(ratio - 0.75f) < 0.01f, "고압 노즐 Lv5 = 1.5배: " + ratio);
        }

        [Fact]
        public void Forest_WideLv5_BalloonBlastIsHalfAgainBigger()
        {
            var sim = Quiet();
            Take(sim, UpgradeId.Wide, Loadout.MaxLevel);
            Assert.Equal(1.5f, sim.SWide, 3);
            // 폭발(SHitArea) 반경 30px → 45px: 45px 떨어진 몹이 맞는다(광각 노즐 없으면 안 맞는다).
            foreach (bool wide in new[] { false, true })
            {
                var s2 = wide ? sim : Quiet();
                s2.Enemies.Clear();
                // 불씨 반지름 ≈ 9px: 가운데가 45px 떨어지면 30+9엔 안 닿고 45+9엔 닿는다.
                Enemy e = s2.Spawn(EnemyKind.Ember, SurvivorSim.FromS(s2.PX + 45f, s2.PY));
                e.MaxHp = e.Hp = 1e6f;
                int n = s2.SHitArea(s2.PX, s2.PY, 30f, 1f);
                Assert.Equal(wide ? 1 : 0, n);
            }
            // 물풍선 몸도 1.5배.
            Take(sim, UpgradeId.Balloon);
            var b = Assert.IsType<BalloonItem>(sim.SampleItemOf(UpgradeId.Balloon));
            for (int i = 0; i < 40 && b.B.Count == 0; i++)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
            }
            Assert.NotEmpty(b.B);
            Assert.Equal(BalloonItem.BalR[1] * 1.5f, b.B[0].R, 3);
        }

        [Fact]
        public void Forest_BootsAndSuit_ArePureStats_NoSampleItem()
        {
            var sim = Quiet();
            float hp = sim.MaxHp;
            Take(sim, UpgradeId.Boots, Loadout.MaxLevel);
            Take(sim, UpgradeId.Suit, Loadout.MaxLevel);
            Spray(sim, 0.2f);
            Assert.Null(sim.SampleItemOf(UpgradeId.Boots));
            Assert.Null(sim.SampleItemOf(UpgradeId.Suit));
            Assert.DoesNotContain(sim.SampleItems, it => it.Base == UpgradeId.Boots || it.Base == UpgradeId.Suit);
            Assert.Equal(1.5f, sim.Build.SpeedScale, 3);
            Assert.Equal(0.5f, sim.Build.HeatScale, 3);
            Assert.Equal(hp + 50f, sim.MaxHp, 3);
            // 이동은 SpeedScale 그대로: 같은 시간 1.5배 멀리.
            float Walk(SurvivorSim s)
            {
                float x0 = s.Player.X;
                s.Spraying = false;
                for (int i = 0; i < 10; i++)
                {
                    s.Enemies.Clear();
                    s.Hp = s.MaxHp;
                    s.Step(1f, 0f);
                }
                return s.Player.X - x0;
            }
            float slow = Walk(Quiet()), fast = Walk(sim);
            Assert.Equal(1.5f, fast / slow, 2);
        }

        [Fact]
        public void Town_NeverOffersPassiveLv6()
        {
            var l = new Loadout();
            for (int i = 0; i < Loadout.MaxLevel; i++)
            {
                l.Add(UpgradeId.Tank);
                l.Add(UpgradeId.Boots);
            }
            HashSet<UpgradeId> seen = Offered(l);
            Assert.DoesNotContain(UpgradeId.OverPump, seen);
            Assert.DoesNotContain(UpgradeId.JetBoots, seen);
            Assert.False(l.Ready(UpgradeId.PhoenixSuit));
        }

        private static void Run(SurvivorSim sim, float seconds, bool keepHp = true)
        {
            int ticks = (int)(seconds / SurvivorSim.Dt);
            for (int i = 0; i < ticks; i++)
            {
                if (keepHp) sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
            }
        }

        private static Structure BurningShop(SurvivorSim sim, int residents)
        {
            var s = new Structure { Kind = StructureKind.House, Name = "가게", Pos = new Vec2(sim.Player.X + 6f, sim.Player.Y + 6f), Half = new Vec2(2f, 1.5f), Residents = residents, Fire = 0.2f };
            sim.Structures.Add(s);
            return s;
        }

        /// <summary>문 앞에 서서 n명을 구한다(불은 작게 유지해 연기로 잃지 않게).</summary>
        private static void RescueN(SurvivorSim sim, int n)
        {
            Structure shop = BurningShop(sim, n);
            int start = sim.Rescued;
            for (int i = 0; i < 6000 && sim.Rescued < start + n; i++)
            {
                sim.Player = shop.Door;
                shop.Fire = 0.2f;
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
            }
            Assert.Equal(start + n, sim.Rescued);
            // 샘플: 튀어나옴 0.45초 + 변신 0.3초 뒤에 대원이 된다.
            for (int i = 0; i < 60; i++)
            {
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
            }
        }

        // --- 무기 레벨별 동작은 SampleArsenalTests(승인 샘플 수치) ---
        [Fact]
        public void Forest_Rescue_JoinsCrew_UpToEight()
        {
            var sim = Quiet();
            RescueN(sim, 1);
            Assert.Single(sim.CrewList);
            RescueN(sim, 8);
            Assert.Equal(SurvivorSim.MaxCrew, sim.CrewList.Count);
        }

        [Fact]
        public void Forest_Crew_SpraysNearbyMob()
        {
            var sim = Quiet();
            RescueN(sim, 1);
            sim.Structures.Clear();
            Run(sim, 1f);
            Crew c = sim.CrewList[0];
            var e = sim.Spawn(EnemyKind.Ember, new Vec2(c.Pos.X + 3f, c.Pos.Y));
            e.Speed = 0f;
            e.MaxHp = e.Hp = 999f;
            Run(sim, 2f);
            Assert.True(e.Hp < 999f, "대원이 4칸 앞 몹에 물을 쏴야");
        }

        [Fact]
        public void Town_Rescue_NoCrew()
        {
            var sim = Quiet(1);
            RescueN(sim, 2);
            Assert.Empty(sim.CrewList);
        }

        [Fact]
        public void Forest_BotRun_FinishesWithManyItems()
        {
            // 칸 제한이 없으니 판이 길게 가면 5종 넘게 든다(한 판은 일찍 질 수 있어 세 판 중 가장 많이 든 판을 본다).
            int best = 0;
            string log = "";
            for (int seed = 1; seed <= 3; seed++)
            {
                var sim = new SurvivorSim(seed, Forest);
                var bot = new SurvivorBot(sim) { Pro = true };
                while (sim.Outcome == SOutcome.Playing) bot.Play();
                int kinds = sim.Build.Owned().Count();
                best = System.Math.Max(best, kinds);
                log += " " + seed + ":" + kinds + "(" + (int)sim.Time + "초)";
            }
            Assert.True(best >= 5, "숲은 칸이 없으니 5종 이상 들어야:" + log);
        }

        // --- 건물 적시기(2026-10-09): 숲 아이템이 건물 불을 줄인다 ---
        /// <summary>플레이어에서 (dx, dy)칸 떨어진 타는 집.</summary>
        private static Structure BurningHouse(SurvivorSim sim, float dx, float dy, float fire = 1f)
        {
            var s = new Structure { Kind = StructureKind.House, Name = "산장", Pos = new Vec2(sim.Player.X + dx, sim.Player.Y + dy), Half = new Vec2(2f, 1.5f), Integrity = 100f, Fire = fire };
            sim.Structures.Add(s);
            return s;
        }

        [Fact]
        public void Forest_SSoakArea_ScalesWithLevelAndNozzle()
        {
            var sim = Quiet();
            Structure house = BurningHouse(sim, 0f, 4f);
            float hx = SurvivorSim.SX(house.Pos), hy = SurvivorSim.SY(house.Pos);
            // 레벨 밖(SCurLv 0): 그대로 0.2.
            sim.SCurLv = 0;
            Assert.Equal(1, sim.SSoakArea(hx, hy, 10f, 0.2f));
            Assert.Equal(0.8f, house.Fire, 3);
            // Lv3(×1.9)·Lv5(×3.0).
            sim.SCurLv = 3;
            sim.SSoakArea(hx, hy, 10f, 0.2f);
            Assert.Equal(0.8f - (0.2f * 1.9f), house.Fire, 3);
            sim.SCurLv = 5;
            sim.SSoakArea(hx, hy, 10f, 0.1f);
            Assert.Equal(0.8f - (0.2f * 1.9f) - (0.1f * 3f), house.Fire, 3);
            sim.SCurLv = 0;
            // 둘레 밖(집 가장자리에서 3칸 = 80px, 반경 10px)이면 안 닿는다.
            float before = house.Fire;
            Assert.Equal(0, sim.SSoakArea(hx, hy + (4.5f / SurvivorSim.Px), 10f, 0.2f));
            Assert.Equal(before, house.Fire);
            // 안 타는 집은 세지 않고 젖지도 않는다.
            house.Fire = 0f;
            house.Wet = 0f;
            Assert.Equal(0, sim.SSoakArea(hx, hy, 10f, 0.2f));
            Assert.Equal(0f, house.Fire);
            Assert.Equal(0f, house.Wet);
            // 고압 노즐 Lv5(×1.5)가 물에도 곱해진다.
            var nz = Quiet();
            Take(nz, UpgradeId.Nozzle, Loadout.MaxLevel);
            Structure h2 = BurningHouse(nz, 0f, 4f);
            nz.SCurLv = 0;
            nz.SSoakArea(SurvivorSim.SX(h2.Pos), SurvivorSim.SY(h2.Pos), 10f, 0.2f);
            Assert.Equal(1f - (0.2f * 1.5f), h2.Fire, 3);
        }

        [Fact]
        public void Forest_STimeScale_IsTheTimePartOfDamageScale()
        {
            var sim = Quiet();
            Assert.Equal(1f, sim.STimeScale, 3);
            Assert.Equal(0.5f, sim.SDamageScale, 3);
            sim.Time = 120f;
            Assert.Equal(2f, sim.STimeScale, 3);
            Assert.Equal(1f, sim.SDamageScale, 3);
            sim.Time = 180f;
            Assert.Equal(3.25f, sim.STimeScale, 3);
        }
    }
}
