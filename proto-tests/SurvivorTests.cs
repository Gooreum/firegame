using System;
using System.Collections.Generic;
using FireGame.Core.Sim;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>시험판 C(뱀서라이크) 규칙. 무기가 맞히고, 구슬이 레벨을 올리고, 카드 뽑기가 규칙대로 나온다.</summary>
    [Collection("Heavy")]
    public class SurvivorTests
    {
        private static void Run(SurvivorSim sim, float seconds, float mx = 0f, float my = 0f)
        {
            int ticks = (int)(seconds / SurvivorSim.Dt);
            for (int i = 0; i < ticks; i++) sim.Step(mx, my);
        }

        /// <summary>스폰 감독이 끼어들지 않게 적을 모두 지운 판(테스트는 원하는 적만 놓는다).</summary>
        private static SurvivorSim Quiet(int seed = 1)
        {
            var sim = new SurvivorSim(seed) { Guardian = false };
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        /// <summary>플레이어가 하듯 과녁을 겨누고 누른 채로 시간을 보낸다.</summary>
        private static void Spray(SurvivorSim sim, Vec2 at, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                sim.Aim = new Vec2(at.X - sim.Player.X, at.Y - sim.Player.Y);
                sim.Spraying = true;
                sim.Step(0f, 0f);
            }
        }

        // --- 물대포: 직접 겨눈다 ---
        [Fact]
        public void Hose_SpraysWhereYouAim_AndAKillDropsAGem()
        {
            SurvivorSim sim = Quiet();
            Enemy e = sim.Spawn(EnemyKind.Blaze, new Vec2(sim.Player.X + 3f, sim.Player.Y));
            e.Speed = 0f;

            Spray(sim, e.Pos, 60);
            Assert.True(e.Hp < e.MaxHp, "겨누고 1초 쐈는데 한 번도 못 맞혔다");

            Enemy ember = sim.Spawn(EnemyKind.Ember, new Vec2(sim.Player.X - 3f, sim.Player.Y));
            ember.Speed = 0f;
            e.Dead = true;
            for (int i = 0; i < 120 && !ember.Dead; i++) Spray(sim, ember.Pos, 1);
            Assert.True(ember.Dead, "불씨를 2초 안에 못 껐다");
            Assert.Contains(sim.Gems, g => g.Pos.DistanceTo(ember.Pos) < 1f);
        }

        [Fact]
        public void Hose_DoesNothing_WhenNotSpraying()
        {
            SurvivorSim sim = Quiet();
            Enemy e = sim.Spawn(EnemyKind.Blaze, new Vec2(sim.Player.X + 3f, sim.Player.Y));
            e.Speed = 0f;
            sim.Aim = new Vec2(1f, 0f);
            sim.Spraying = false;
            Run(sim, 1f);
            Assert.Empty(sim.Shots);
            Assert.Equal(e.MaxHp, e.Hp);
        }

        [Fact]
        public void Hose_MissesAFire_BehindYou()
        {
            SurvivorSim sim = Quiet();
            Enemy e = sim.Spawn(EnemyKind.Blaze, new Vec2(sim.Player.X + 3f, sim.Player.Y));
            e.Speed = 0f;
            Spray(sim, new Vec2(sim.Player.X - 5f, sim.Player.Y), 60);
            Assert.Equal(e.MaxHp, e.Hp);
        }

        // --- TC-2 ---
        [Fact]
        public void FillingXp_PausesWithThreeCards()
        {
            SurvivorSim sim = Quiet();
            sim.DropGem(sim.Player, sim.XpToNext);
            sim.Step(0f, 0f);
            sim.Step(0f, 0f);

            Assert.Equal(2, sim.Level);
            Assert.NotNull(sim.PendingChoices);
            Assert.Equal(3, sim.PendingChoices.Count);
            Assert.True(sim.JustLeveled || sim.PendingChoices != null);

            float frozen = sim.Time;
            Run(sim, 1f);
            Assert.Equal(frozen, sim.Time);
        }

        // --- TC-3 ---
        [Fact]
        public void Choosing_RaisesTheLevel_AndTimeFlowsAgain()
        {
            SurvivorSim sim = Quiet();
            sim.DropGem(sim.Player, sim.XpToNext);
            for (int i = 0; i < 3 && sim.PendingChoices == null; i++) sim.Step(0f, 0f);

            UpgradeId pick = sim.PendingChoices[0];
            int before = sim.Build.Level(pick);
            sim.Choose(0);

            Assert.Null(sim.PendingChoices);
            Assert.Equal(before + 1, sim.Build.Level(pick));
            float t = sim.Time;
            sim.Step(0f, 0f);
            Assert.True(sim.Time > t);
        }

        // --- TC-4 ---
        [Fact]
        public void MaxedItems_AreNeverOffered()
        {
            var l = new Loadout();
            for (int i = 0; i < Loadout.MaxLevel; i++) l.Add(UpgradeId.Hose);
            var rng = new Rng(7);
            for (int i = 0; i < 1000; i++)
            {
                Assert.DoesNotContain(UpgradeId.Hose, SurvivorUpgrades.Roll(l, 2, ref rng));
            }
        }

        // --- TC-5 ---
        [Fact]
        public void Evolution_IsAlwaysOffered_AndReplacesTheHose()
        {
            var l = new Loadout();
            for (int i = 0; i < Loadout.MaxLevel; i++) l.Add(UpgradeId.Hose);
            // 짝 보조가 EvolvePair 레벨 미만이면 진화 카드가 안 나오고, 채우면 반드시 나온다.
            var rng = new Rng(3);
            for (int i = 0; i < 50; i++) Assert.DoesNotContain(UpgradeId.Cannon, SurvivorUpgrades.Roll(l, 2, ref rng));
            for (int i = 0; i < Loadout.EvolvePair; i++) l.Add(UpgradeId.Tank);
            for (int i = 0; i < 200; i++) Assert.Contains(UpgradeId.Cannon, SurvivorUpgrades.Roll(l, 2, ref rng));

            l.Add(UpgradeId.Cannon);
            Assert.Equal(0, l.Level(UpgradeId.Hose));
            Assert.Equal(1, l.Level(UpgradeId.Cannon));
            for (int i = 0; i < 200; i++)
            {
                List<UpgradeId> cards = SurvivorUpgrades.Roll(l, 2, ref rng);
                Assert.DoesNotContain(UpgradeId.Cannon, cards);
                Assert.DoesNotContain(UpgradeId.Hose, cards);
            }
        }

        // --- TC-6 ---
        [Fact]
        public void NothingLeft_SkipsTheCardScreen()
        {
            SurvivorSim sim = Quiet();
            // 여섯 무기를 모두 진화시킨다(보조도 최대): 더 뽑을 게 없다.
            sim.Build.EvolveAll();

            var rng = new Rng(5);
            Assert.Empty(SurvivorUpgrades.Roll(sim.Build, 2, ref rng));

            // 레벨은 오르지만 카드 화면은 안 열리고, 체력도 그대로다(레벨업 공짜 회복 없음).
            sim.Hp = sim.MaxHp - 10f;
            sim.DropGem(sim.Player, sim.XpToNext);
            bool leveled = false;
            for (int i = 0; i < 3 && !leveled; i++)
            {
                sim.Step(0f, 0f);
                leveled = sim.JustLeveled;
            }
            Assert.True(leveled);
            Assert.Equal(2, sim.Level);
            Assert.Null(sim.PendingChoices);
            Assert.Equal(sim.MaxHp - 10f, sim.Hp);
        }

        [Fact]
        public void Roll_HasNoYellowCards_EvenOnFifthLevels()
        {
            // 노란 특수 장비는 없앴다(2026-10-07): 5·10·15레벨에도 일반 카드(또는 진화)만 나온다.
            var l = new Loadout();
            l.Add(UpgradeId.Hose);
            var rng = new Rng(11);
            foreach (int level in new[] { 5, 10, 15 })
            {
                for (int i = 0; i < 30; i++)
                {
                    foreach (UpgradeId id in SurvivorUpgrades.Roll(l, level, ref rng))
                    {
                        Assert.True(Loadout.IsWeapon(id) || Loadout.IsPassive(id), level + "레벨에 " + id);
                        Assert.False(Loadout.IsEvolution(id), "진화 조건이 안 됐는데 " + id);
                    }
                }
            }
        }

        [Fact]
        public void Roll_NeverPadsWithHeal()
        {
            // 무기·보조 칸이 다 찼고 모두 최대라 진화만 남았다: 진화 한 장만 나온다. 전엔 회복 카드가 끼어 두 장이었다.
            var l = new Loadout();
            foreach (UpgradeId id in new[] { UpgradeId.Hose, UpgradeId.WaterBomb, UpgradeId.Drone, UpgradeId.Partner, UpgradeId.Tank, UpgradeId.Boots, UpgradeId.Suit })
                for (int k = 0; k < Loadout.MaxLevel; k++) l.Add(id);
            var rng = new Rng(3);
            for (int i = 0; i < 50; i++)
            {
                List<UpgradeId> cards = SurvivorUpgrades.Roll(l, 7, ref rng);
                Assert.Single(cards);
                Assert.True(Loadout.IsEvolution(cards[0]));
                Assert.DoesNotContain(UpgradeId.Heal, cards);
            }
        }

        // --- TC-7 ---
        [Fact]
        public void FullWeaponSlots_OfferNoNewWeapon()
        {
            var l = new Loadout();
            l.Add(UpgradeId.Hose);
            l.Add(UpgradeId.WaterBomb);
            l.Add(UpgradeId.Drone);
            Assert.Equal(3, l.WeaponCount);

            var rng = new Rng(11);
            var seen = new HashSet<UpgradeId>();
            for (int i = 0; i < 500; i++) foreach (UpgradeId id in SurvivorUpgrades.Roll(l, 2, ref rng)) seen.Add(id);
            Assert.DoesNotContain(UpgradeId.Cannon, seen);
            Assert.DoesNotContain(UpgradeId.Turret, seen);
            Assert.Contains(UpgradeId.Tank, seen);
            Assert.Contains(UpgradeId.Hose, seen);

            var full = new Loadout();
            full.Add(UpgradeId.Tank);
            full.Add(UpgradeId.Suit);
            full.Add(UpgradeId.Hose);
            full.Add(UpgradeId.WaterBomb);
            Assert.True(full.CanTake(UpgradeId.Drone), "무기 2개일 땐 세 번째 무기를 얻을 수 있어야 한다");
            full.Add(UpgradeId.Drone);
            foreach (UpgradeId id in new[] { UpgradeId.Hose, UpgradeId.WaterBomb, UpgradeId.Drone }) Assert.True(full.CanTake(id));
            // 칸보다 종류가 많다: 무기 3칸이 차면 포탑·구조대원·물의 장막은 못 얻는다.
            Assert.False(full.CanTake(UpgradeId.Turret));
            Assert.False(full.CanTake(UpgradeId.Partner));
            Assert.False(full.CanTake(UpgradeId.Curtain));
        }

        // --- TC-8 ---
        [Fact]
        public void SameSeed_SamePlay()
        {
            var a = new SurvivorSim(42) { Guardian = false };
            var b = new SurvivorSim(42) { Guardian = false };
            var aimA = new SurvivorBot(a);
            var aimB = new SurvivorBot(b);
            for (int i = 0; i < 1200; i++)
            {
                float mx = (float)System.Math.Sin(i * 0.01);
                float my = (float)System.Math.Cos(i * 0.013);
                aimA.AimHose();
                aimB.AimHose();
                a.Step(mx, my);
                b.Step(mx, my);
                if (a.PendingChoices != null) a.Choose(0);
                if (b.PendingChoices != null) b.Choose(0);
            }
            Assert.Equal(a.Enemies.Count, b.Enemies.Count);
            Assert.Equal(a.Kills, b.Kills);
            Assert.Equal(a.Player.X, b.Player.X);
            Assert.Equal(a.Player.Y, b.Player.Y);
            Assert.Equal(a.Hp, b.Hp);
            Assert.True(a.Kills > 0);
        }
            // ================= Step 2: 나머지 무기·보스·밸런스 =================

        private static Enemy Dummy(SurvivorSim sim, EnemyKind kind, float dx, float dy, float hp = 999f)
        {
            Enemy e = sim.Spawn(kind, new Vec2(sim.Player.X + dx, sim.Player.Y + dy));
            e.Speed = 0f;
            e.MaxHp = hp;
            e.Hp = hp;
            return e;
        }

        // --- S2 TC-1 ---
        [Fact]
        public void WaterBomb_ExplodesOnACrowd()
        {
            var sim = new SurvivorSim(1) { Guardian = false };
            sim.Enemies.Clear();
            sim.Build.Add(UpgradeId.WaterBomb);
            var crowd = new List<Enemy>();
            for (int i = 0; i < 5; i++) crowd.Add(Dummy(sim, EnemyKind.Blaze, 6f + (i * 0.3f), 0.2f * i));

            bool exploded = false;
            for (int i = 0; i < 180; i++)
            {
                sim.Step(0f, 0f);
                if (sim.Explosions.Count > 0) exploded = true;
            }
            Assert.True(exploded, "3초 동안 물폭탄이 한 번도 안 터졌다");
            Assert.True(crowd.FindAll(e => e.Hp < e.MaxHp).Count >= 3, "폭발이 무리 여럿을 맞히지 못했다");
        }

        // --- S2 TC-2 ---
        [Fact]
        public void Drones_OrbitAndHitWhatTheyTouch()
        {
            SurvivorSim sim = Quiet();
            sim.Build.Add(UpgradeId.Drone);
            sim.Build.Add(UpgradeId.Drone);
            Enemy e = Dummy(sim, EnemyKind.Ember, 0f, 2.3f);
            e.Pos = new Vec2(sim.Player.X, sim.Player.Y + 2.3f);

            sim.Step(0f, 0f);
            Assert.Equal(2, sim.Drones.Count);
            Assert.All(sim.Drones, d => Assert.InRange(d.DistanceTo(sim.Player), 2.2f, 2.4f));

            // 물대포도 이 적을 쏘므로, 드론이 맞혔다는 표시(드론 재타격 대기)로 확인한다.
            bool struck = false;
            for (int i = 0; i < 180 && !struck; i++)
            {
                sim.Step(0f, 0f);
                if (e.DroneCooldown > 0f) struck = true;
            }
            Assert.True(struck, "3초 동안 드론이 궤도 위 적을 한 번도 안 맞혔다");
        }

        [Fact]
        public void Cannon_PiercesALineOfFires()
        {
            SurvivorSim sim = Quiet();
            for (int i = 0; i < Loadout.MaxLevel; i++) sim.Build.Add(UpgradeId.Hose);
            for (int i = 0; i < Loadout.EvolvePair; i++) sim.Build.Add(UpgradeId.Tank);
            sim.DropGem(sim.Player, sim.XpToNext);
            for (int i = 0; i < 3 && sim.PendingChoices == null; i++) sim.Step(0f, 0f);
            sim.Choose(sim.PendingChoices.IndexOf(UpgradeId.Cannon));
            Assert.Equal(1, sim.Build.Level(UpgradeId.Cannon));

            // 사방 여덟 줄에 적을 세 마리씩 세우면, 한 바퀴 휩쓸 때 한 줄 전체가 맞는다.
            var lines = new List<List<Enemy>>();
            for (int k = 0; k < 8; k++)
            {
                double a = System.Math.PI * 2 * k / 8;
                var line = new List<Enemy>();
                for (int r = 2; r <= 6; r += 2) line.Add(Dummy(sim, EnemyKind.Blaze, (float)System.Math.Cos(a) * r, (float)System.Math.Sin(a) * r));
                lines.Add(line);
            }
            Run(sim, 1f);
            Assert.Contains(lines, line => line.TrueForAll(e => e.Hp < e.MaxHp));
        }

        // --- S2 TC-5 ---
        [Fact]
        public void BlazeLeavesBurningGround_ThatHurts()
        {
            SurvivorSim sim = Quiet();
            Enemy e = Dummy(sim, EnemyKind.Blaze, 1.5f, 0f, 1f);
            for (int i = 0; i < 60 && !e.Dead; i++) Spray(sim, e.Pos, 1);
            sim.Spraying = false;
            Assert.True(e.Dead);
            Assert.Single(sim.BurningGround);

            Puddle fire = sim.BurningGround[0];
            sim.Player = fire.Pos;
            float hp = sim.Hp;
            sim.Step(0f, 0f);
            Assert.True(sim.Hp < hp, "타는 바닥 위인데 체력이 안 줄었다");

            // 그때 있던 바닥 불(처치 자리 3초, 물에 밀려 걸으며 남긴 흔적 4초)은 모두 타서 사라진다.
            var before = new List<Puddle>(sim.BurningGround);
            Run(sim, 4.1f);
            Assert.All(before, q => Assert.DoesNotContain(q, sim.BurningGround));
        }

        /// <summary>
        /// 봇이 대화재(3:00)까지 가는 첫 시드(성능·끝까지 가는 테스트용). 규칙이 난수 순서를 바꿔도 깨지지 않게 매번 찾는다.
        /// </summary>
        private static readonly int LongSeed = FindLongSeed();

        private static int FindLongSeed()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var sim = new SurvivorSim(seed) { Guardian = false };
                var bot = new SurvivorBot(sim);
                while (sim.Outcome == SOutcome.Playing && sim.Time < SurvivorSim.FinaleAt + 1f) bot.Play();
                if (sim.Outcome == SOutcome.Playing) return seed;
            }
            return 1;
        }

        // --- S2 TC-6 ---
        [Fact]
        public void Finale_AtThreeMinutes_ThenSurvivingToFourWins()
        {
            var sim = new SurvivorSim(LongSeed) { Guardian = false };
            var bot = new SurvivorBot(sim);
            bool finale = false;
            while (sim.Outcome == SOutcome.Playing && sim.Time < SurvivorSim.FinaleAt + 1f)
            {
                bot.Play();
                if (sim.JustFinale) finale = true;
            }
            Assert.Equal(SOutcome.Playing, sim.Outcome);
            Assert.True(finale);
            Assert.NotNull(sim.Landmark);

            // 끝까지 버틴다: 불은 치우고 체력은 채우고 건물도 무너지지 않게 붙들어 규칙(4:00 승리)만 본다.
            // (건물 불이 더 오래 버티게 된 뒤로는 봇 혼자 마지막 1분을 지키지 못하는 시드가 있다.)
            while (sim.Outcome == SOutcome.Playing)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                foreach (Structure st in sim.Structures)
                {
                    if (st.IsBuilding && !st.Collapsed && st.Integrity < 0.5f) st.Integrity = 0.5f;
                }
                bot.Play();
            }
            Assert.Equal(SOutcome.Won, sim.Outcome);
            Assert.InRange(sim.Time, SurvivorSim.RunTime, SurvivorSim.RunTime + 0.1f);
        }

        // --- S2 TC-7 ---
        [Fact]
        public void StandingStill_LosesWithin150Seconds()
        {
            var sim = new SurvivorSim(1) { Guardian = false };
            while (sim.Outcome == SOutcome.Playing && sim.Time < 150f)
            {
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
            }
            Assert.Equal(SOutcome.Lost, sim.Outcome);
        }

        // --- S2 TC-8, TC-9 ---
        [Fact]
        public void Bot_ReachesTheFinaleOften_WinsSometimes_AndTheCrowdIsCapped()
        {
            int reached = 0;
            int won = 0;
            int seeds = FunTests.Seeds;
            var log = new System.Text.StringBuilder();
            for (int seed = 1; seed <= seeds; seed++)
            {
                var sim = new SurvivorSim(seed) { Guardian = false };
                var bot = new SurvivorBot(sim);
                int guard = 0;
                while (sim.Outcome == SOutcome.Playing && guard++ < 60 * 400)
                {
                    bot.Play();
                    Assert.True(sim.Enemies.Count <= SurvivorSim.MaxEnemies, "적이 상한을 넘었다");
                }
                if (sim.Finale) reached++;
                if (sim.Outcome == SOutcome.Won) won++;
                log.AppendLine("seed " + seed + ": " + sim.Outcome + " t=" + (int)sim.Time + " lv=" + sim.Level);
            }
            // 봇 밴드는 바닥이다(docs §14): 봇은 건물에 거의 안 뿌려 끄는 시간이 15초인 판을 사람처럼 못 넘긴다.
            // 30판 기준 대화재 도달 13, 승 10(2026-10-02). 아이템 다이어트(무기 3·보조 2·대원 한 명) 뒤 기본 봇은 몹을 못 치워
            // 도달 5·승 3(2026-10-04, docs §16). 사람 기준은 숙련 봇 CloseReport(22승)다. 여기 하한은 "전멸이 아니다": 도달 1/10, 승 2.
            Assert.True(reached * 10 >= seeds, "대화재까지 간 판이 " + reached + "/" + seeds + "개\n" + log);
            Assert.InRange(won * 10f / seeds, 0.5f, 9f);
        }

        // --- S2 TC-10 ---
        [Fact]
        public void AFullRun_IsCheapToSimulate()
        {
            int seed = LongSeed;
            // 전체 스위트 끝(측정 리포트 뒤)에선 GC·JIT 부하로 한 번은 느릴 수 있다: 두 번 재서 빠른 쪽을 본다.
            double best = double.MaxValue;
            float played = 0f;
            for (int run = 0; run < 2; run++)
            {
                System.GC.Collect();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var sim = new SurvivorSim(seed) { Guardian = false };
                var bot = new SurvivorBot(sim);
                int guard = 0;
                while (sim.Outcome == SOutcome.Playing && guard++ < 60 * 400) bot.Play();
                watch.Stop();
                played = sim.Time;
                best = Math.Min(best, watch.Elapsed.TotalSeconds);
                if (best < 3.0) break;
            }
            Assert.True(played > SurvivorSim.FinaleAt, "시드 " + LongSeed + "가 대화재까지 못 가 성능 측정이 짧아졌다");
            // 건물이 오래 타면(끄는 시간 15초) 큰 불 몹·불씨가 더 나와 적이 상한(350)에 자주 닿는다: 그런 판이 이 기계(부하 7~9)에서 3.2~3.4초.
            Assert.True(best < 4.0, "한 판에 " + best + "초");
        }

        [Fact]
        public void Cannon_SpraysPiercingJets_WhereYouAim()
        {
            SurvivorSim sim = Quiet();
            sim.GiveMaxGear();
            var line = new List<Enemy>();
            for (int r = 2; r <= 6; r += 2) line.Add(Dummy(sim, EnemyKind.Blaze, 0f, r));

            int aimed = 0;
            for (int i = 0; i < 40; i++)
            {
                Spray(sim, new Vec2(sim.Player.X, sim.Player.Y + 10f), 1);
                foreach (Shot s in sim.Shots)
                {
                    if (s.Kind == ShotKind.Jet && s.Age <= SurvivorSim.Dt && s.Vel.Y > 0f && System.Math.Abs(s.Vel.X) < 0.2f * s.Vel.Y) aimed++;
                }
            }
            Assert.True(aimed >= 5, "위로 겨눈 방수포 제트가 " + aimed + "줄뿐");
            Assert.True(line.TrueForAll(e => e.Hp < e.MaxHp), "겨눈 제트가 한 줄을 꿰뚫지 못했다");
        }

        // --- 번지는 불 ---
        [Fact]
        public void MovingBlaze_LeavesATrailOfFire()
        {
            SurvivorSim sim = Quiet();
            Enemy e = sim.Spawn(EnemyKind.Blaze, new Vec2(sim.Player.X + 8f, sim.Player.Y));
            e.MaxHp = 999f;
            e.Hp = 999f;
            Run(sim, 3f);
            Assert.True(sim.BurningGround.Count >= 2, "3초 걸어온 큰 불이 남긴 바닥 불이 " + sim.BurningGround.Count + "개");
        }

        [Fact]
        public void WaterPutsOutBurningGround()
        {
            SurvivorSim sim = Quiet();
            var patch = new Puddle { Pos = new Vec2(sim.Player.X + 3f, sim.Player.Y), Radius = 0.6f, Life = 4f, MaxLife = 4f };
            sim.BurningGround.Add(patch);
            bool signalled = false;
            for (int i = 0; i < 40 && sim.BurningGround.Contains(patch); i++)
            {
                Spray(sim, patch.Pos, 1);
                if (sim.Extinguished.Count > 0) signalled = true;
            }
            Assert.DoesNotContain(patch, sim.BurningGround);
            Assert.True(signalled, "꺼진 자리를 알리지 않았다");
        }

        [Fact]
        public void UnwateredGround_CanReignite()
        {
            SurvivorSim sim = Quiet();
            for (int k = 0; k < 10; k++)
            {
                double a = System.Math.PI * 2 * k / 10;
                var at = new Vec2(sim.Player.X + (float)(System.Math.Cos(a) * 8), sim.Player.Y + (float)(System.Math.Sin(a) * 8));
                sim.BurningGround.Add(new Puddle { Pos = at, Radius = 0.6f, Life = 0.05f, MaxLife = 4f });
            }
            int reignited = 0;
            for (int i = 0; i < 10; i++)
            {
                sim.Step(0f, 0f);
                reignited += sim.Reignited.Count;
            }
            Assert.InRange(reignited, 1, 10);
            Assert.Contains(sim.Enemies, e => e.Kind == EnemyKind.Ember);
        }

        // --- 동네: 지킬 것 ---

        /// <summary>조용한 판에 가게 하나를 소방관 기준 (dx, dy)에 세운다.</summary>
        private static Structure Shop(SurvivorSim sim, float dx, float dy, int residents = 1)
        {
            var s = new Structure { Kind = StructureKind.House, Name = "가게", Pos = new Vec2(sim.Player.X + dx, sim.Player.Y + dy), Half = new Vec2(2f, 1.5f), Residents = residents };
            sim.Structures.Add(s);
            return s;
        }

        [Fact]
        public void Town_HasShopsDepotGasAndAClearCenter()
        {
            var sim = new SurvivorSim(1) { Guardian = false };
            var houses = sim.Structures.FindAll(s => s.Kind == StructureKind.House);
            Assert.Equal(12, houses.Count);
            Assert.Single(sim.Structures.FindAll(s => s.Kind == StructureKind.Depot));
            Assert.Equal(3, sim.Structures.FindAll(s => s.Kind == StructureKind.Gas).Count);
            Assert.Equal(13, sim.HousesTotal);
            // 강이 둘로 가른다: 서쪽 여섯, 동쪽 여섯. 강은 다리(가운데)만 비운다.
            Assert.Equal(6, houses.FindAll(h => h.Pos.X < SurvivorTown.RiverX).Count);
            Assert.Equal(6, houses.FindAll(h => h.Pos.X > SurvivorTown.RiverX).Count);
            var river = sim.Structures.FindAll(s => s.Kind == StructureKind.Water);
            Assert.Equal(2, river.Count);
            Assert.True(sim.HasWater);
            Assert.All(river, w => Assert.False(w.Within(sim.Player, 0f)));
            Assert.All(sim.Structures, s => Assert.True(s.Kind == StructureKind.Water || s.DistanceTo(sim.Player) > 6f, s.Name + "가 출발점에 너무 가깝다"));
            Assert.All(sim.Structures, s => Assert.False(s.Burning));
        }

        [Fact]
        public void Ember_SeeksAndIgnitesAHouse()
        {
            // 불난 가게가 불씨를 뱉고, 그 불씨가 옆 가게로 굴러가 불을 옮긴다.
            SurvivorSim sim = Quiet();
            Structure burning = Shop(sim, 9f, 0f);
            Structure next = Shop(sim, 9f, 8f);
            sim.Ignite(burning, 0.5f);
            bool signalled = false;
            for (int i = 0; i < 60 * 30 && !next.Burning; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                if (sim.Ignited.Contains(next)) signalled = true;
            }
            Assert.True(next.Burning, "30초 동안 옆 가게로 불이 안 번졌다");
            Assert.True(signalled);
            Assert.Equal(0, sim.Kills);
        }

        [Fact]
        public void EdgeEmber_ChasesYou_NotTheHouse()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 8f, 0f);
            Enemy ember = sim.Spawn(EnemyKind.Ember, new Vec2(sim.Player.X + 8f, sim.Player.Y + 4f));
            float before = ember.Pos.DistanceTo(sim.Player);
            Run(sim, 0.5f);
            Assert.True(ember.Pos.DistanceTo(sim.Player) < before);
            Assert.False(shop.Burning);
        }

        [Fact]
        public void BurningHouse_GrowsAndCollapses_LosingItsResidents()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 10f, 0f, 2);
            sim.Ignite(shop, 0.15f);
            // 쏟아지는 불씨에 쓰러지지 않게 매 틱 체력을 채운다(가게만 본다).
            for (int i = 0; i < 60 * 25; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
            }
            Assert.Equal(1f, shop.Fire, 2);
            Assert.True(shop.Integrity < 1f);
            Assert.Contains(sim.Enemies, e => e.Kind == EnemyKind.Ember);

            bool fell = false;
            for (int i = 0; i < 60 * 60 && !fell; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                if (sim.Fell.Contains(shop)) fell = true;
            }
            Assert.True(fell, "방치한 가게가 85초 안에 안 무너졌다");
            Assert.True(shop.Collapsed);
            Assert.Equal(1, sim.HousesLost);
            Assert.Equal(2, sim.CiviliansLost);
        }

        [Fact]
        public void SprayingABurningHouse_PutsItOut_AndItStaysWet()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 6f, 0f);
            sim.Ignite(shop, 1f);
            bool doused = false;
            // 큰 불은 물이 덜 먹히고(FireResist) 건물은 꾸준한 물을 BuildingWater만큼만 먹어, Lv1 물대포로 다 탄 가게를 끄는 데 15초쯤 걸린다.
            // 그동안 가장자리에서 오는 불 몹이 물줄기를 가로채지 않게 매 틱 치운다.
            for (int i = 0; i < 60 * 25 && shop.Burning; i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                Spray(sim, shop.Pos, 1);
                if (sim.Doused.Contains(shop)) doused = true;
            }
            Assert.False(shop.Burning, "다 탄 가게를 25초 뿌려도 안 꺼졌다");
            Assert.True(doused);
            Assert.True(shop.Wet > 0f);
            Assert.False(sim.Ignite(shop, 0.5f), "젖은 가게에 불이 다시 붙었다");
        }

        [Fact]
        public void Drop_IsBlockedByAWall_JetPiercesIt()
        {
            SurvivorSim sim = Quiet();
            Shop(sim, 4f, 0f);
            Enemy behind = Dummy(sim, EnemyKind.Blaze, 8f, 0f);
            Spray(sim, behind.Pos, 90);
            Assert.Equal(behind.MaxHp, behind.Hp);

            sim.GiveMaxGear();
            sim.Build.Level(UpgradeId.WaterBomb);
            for (int i = 0; i < 60 && behind.Hp >= behind.MaxHp; i++)
            {
                Spray(sim, behind.Pos, 1);
                sim.Explosions.Clear();
            }
            Assert.True(behind.Hp < behind.MaxHp, "방수포 제트가 가게를 뚫지 못했다");
        }

        [Fact]
        public void Player_CannotWalkThroughAHouse()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 3f, 0f);
            Run(sim, 3f, 1f, 0f);
            Assert.True(sim.Player.X <= shop.Pos.X - shop.Half.X - SurvivorSim.PlayerRadius + 0.01f, "소방관이 가게 벽 안으로 들어갔다: x=" + sim.Player.X);
        }

        // --- 구조·가스통·신고·승패 ---

        [Fact]
        public void StandingAtTheDoor_RescuesTrappedPeople()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 0f, 3.2f, 2);
            Structure calm = Shop(sim, 9f, 3.2f, 1);
            Assert.True(shop.Door.DistanceTo(sim.Player) <= SurvivorSim.RescueRange);
            sim.Ignite(shop, 0.2f);

            bool signalled = false;
            for (int i = 0; i < (int)(((SurvivorSim.RescueTime * 2f) + 0.5f) / SurvivorSim.Dt); i++)
            {
                // 구조 경험치로 레벨이 오르면 카드를 고르고 계속 선다. 무기(물폭탄 등)는 불을 꺼서 구조를 끊으니 보조를 고른다.
                if (sim.PendingChoices != null) sim.Choose(Math.Max(0, sim.PendingChoices.FindIndex(Loadout.IsPassive)));
                sim.Step(0f, 0f);
                if (sim.JustRescued && sim.RescuedFrom.Contains(shop)) signalled = true;
            }
            Assert.Equal(2, sim.Rescued);
            Assert.Equal(0, shop.Residents);
            Assert.True(signalled);
            Assert.Equal(1, calm.Residents);

            // 안 타는 가게 문 앞에서는 아무도 안 나온다.
            sim.Player = new Vec2(calm.Door.X, calm.Door.Y);
            Run(sim, 2f);
            Assert.Equal(1, calm.Residents);
        }

        [Fact]
        public void TrappedPeople_ChokeInABigFire()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 12f, 0f, 2);
            sim.Ignite(shop, 1f);
            bool signalled = false;
            for (int i = 0; i < (int)((SurvivorSim.SmokeTime + 0.5f) * 60); i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                if (sim.PeopleLost.Contains(shop)) signalled = true;
            }
            Assert.Equal(1, sim.CiviliansLost);
            Assert.Equal(1, shop.Residents);
            Assert.True(signalled);
        }

        [Fact]
        public void GasTank_ExplodesAndSpreads_UnlessWetFirst()
        {
            SurvivorSim sim = Quiet();
            var gas = new Structure { Kind = StructureKind.Gas, Name = "가스통", Pos = new Vec2(sim.Player.X + 7f, sim.Player.Y), Half = new Vec2(0.4f, 0.4f) };
            sim.Structures.Add(gas);
            Structure shop = Shop(sim, 9.6f, 0f);
            sim.Ignite(gas, 0.2f);
            bool blasted = false;
            for (int i = 0; i < (int)((SurvivorSim.GasFuse + 0.2f) * 60); i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                if (sim.GasBlasts.Count > 0) blasted = true;
            }
            Assert.True(blasted, "불붙은 가스통이 안 터졌다");
            Assert.True(gas.Collapsed);
            Assert.True(shop.Burning, "터진 가스통 옆 가게에 불이 안 옮았다");
            Assert.True(shop.Fire >= 0.5f);

            SurvivorSim wet = Quiet();
            var gas2 = new Structure { Kind = StructureKind.Gas, Name = "가스통", Pos = new Vec2(wet.Player.X + 5f, wet.Player.Y), Half = new Vec2(0.4f, 0.4f) };
            wet.Structures.Add(gas2);
            Spray(wet, gas2.Pos, 20);
            wet.Spraying = false;
            Assert.True(gas2.Wet > 0f, "물을 뿌린 가스통이 안 젖었다");
            Assert.False(wet.Ignite(gas2, 0.5f), "젖은 가스통에 불이 붙었다");
            Run(wet, 3f);
            Assert.False(gas2.Collapsed);
        }

        [Fact]
        public void Reports_IgniteShopsOnSchedule()
        {
            var sim = new SurvivorSim(1) { Guardian = false };
            var times = new List<float>();
            int pairs = 0;
            // 대형 신고와 대화재(3:00~)는 따로 본다(FinaleTests): 여기선 3:00 전 신고 표만.
            float until = SurvivorSim.FinaleAt - 0.5f;
            while (sim.Time < until)
            {
                // 신고로 난 불만 보려고 매 틱 다른 불을 치운다.
                sim.Enemies.Clear();
                foreach (Structure s in sim.Structures) s.Fire = 0f;
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
                int shops = sim.Ignited.FindAll(s => s.Kind == StructureKind.House && !(sim.JustBigReport && s == sim.BigReport)).Count;
                for (int k = 0; k < shops; k++) times.Add(sim.Time);
                if (shops >= 2) pairs++;
            }
            Assert.Equal(Array.FindAll(SurvivorSim.ReportTimes, r => r < until).Length, times.Count);
            Assert.InRange(times[0], SurvivorSim.ReportTimes[0], SurvivorSim.ReportTimes[0] + 0.05f);
            Assert.Equal(1, pairs);
        }

        [Fact]
        public void LosingHalfTheTown_LosesTheRun()
        {
            var sim = new SurvivorSim(1) { Guardian = false };
            sim.Enemies.Clear();
            List<Structure> shops = sim.Structures.FindAll(s => s.Kind == StructureKind.House);
            // 딱 절반(13채면 6채)까지는 버티고, 한 채 더 잃으면 진다.
            int half = sim.HousesTotal / 2;
            for (int k = 0; k < half; k++)
            {
                sim.Ignite(shops[k], 1f);
                shops[k].Integrity = 0.0001f;
            }
            sim.Step(0f, 0f);
            Assert.Equal(half, sim.HousesLost);
            Assert.Equal(SOutcome.Playing, sim.Outcome);

            sim.Ignite(shops[half], 1f);
            shops[half].Integrity = 0.0001f;
            sim.Step(0f, 0f);
            Assert.Equal(SOutcome.Lost, sim.Outcome);
            Assert.True(sim.LostTown);
            Assert.True(sim.CiviliansLost > 0);
        }

        /// <summary>불은 치우고 체력은 채우며 4:00까지 버틴다.</summary>
        private static void SurviveToTheEnd(SurvivorSim sim)
        {
            while (sim.Outcome == SOutcome.Playing)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
            }
        }

        [Fact]
        public void Finale_SetsTheDepotAblaze_WithPeopleInside()
        {
            var sim = new SurvivorSim(1) { Guardian = false };
            while (!sim.Finale && sim.Outcome == SOutcome.Playing)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                // 신고된 가게는 바로 끈다(대화재 규칙만 본다).
                foreach (Structure s in sim.Structures) if (s.Burning) s.Fire = 0f;
                sim.Step(0f, 0f);
            }
            Structure depot = sim.Structures.Find(s => s.Kind == StructureKind.Depot);
            Assert.Same(depot, sim.Landmark);
            Assert.True(depot.Burning);
            Assert.True(depot.Residents >= SurvivorSim.FinalePeople);
        }

        [Fact]
        public void WinningStars_CountSavedHousesAndLostPeople()
        {
            var perfect = new SurvivorSim(1) { Guardian = false };
            perfect.Reports = false;
            SurviveToTheEnd(perfect);
            Assert.Equal(SOutcome.Won, perfect.Outcome);
            Assert.Equal(3, perfect.Stars);

            var oneLost = new SurvivorSim(1) { Guardian = false };
            oneLost.Reports = false;
            Structure shop = oneLost.Structures.Find(s => s.Kind == StructureKind.House && s.Residents > 0);
            oneLost.Ignite(shop, 1f);
            shop.Integrity = 0.0001f;
            SurviveToTheEnd(oneLost);
            Assert.Equal(SOutcome.Won, oneLost.Outcome);
            Assert.Equal(1, oneLost.HousesLost);
            Assert.Equal(2, oneLost.Stars);
        }

        // --- 풀장비 시작 ---
        [Fact]
        public void GiveMaxGear_MaxesEveryItem_AndEvolvesTheHose()
        {
            var sim = new SurvivorSim(1) { Guardian = false };
            sim.GiveMaxGear();

            UpgradeId[] maxed = { UpgradeId.WaterBomb, UpgradeId.Drone, UpgradeId.Partner, UpgradeId.Curtain, UpgradeId.Turret, UpgradeId.Tank, UpgradeId.Boots, UpgradeId.Suit };
            foreach (UpgradeId id in maxed) Assert.Equal(Loadout.MaxLevel, sim.Build.Level(id));
            Assert.Equal(1, sim.Build.Level(UpgradeId.Cannon));
            Assert.Equal(0, sim.Build.Level(UpgradeId.Hose));
            Assert.Equal(150f, sim.MaxHp);
            Assert.Equal(sim.MaxHp, sim.Hp);
        }

        [Fact]
        public void MaxGear_FiresJetsBombsAndFiveDrones_FromTheStart()
        {
            SurvivorSim sim = Quiet();
            sim.GiveMaxGear();
            sim.Spawn(EnemyKind.Blaze, new Vec2(sim.Player.X + 4f, sim.Player.Y)).Speed = 0f;

            bool jet = false;
            bool bomb = false;
            for (int i = 0; i < 30; i++)
            {
                sim.Step(0f, 0f);
                foreach (Shot s in sim.Shots)
                {
                    if (s.Kind == ShotKind.Jet) jet = true;
                    if (s.Kind == ShotKind.Bomb) bomb = true;
                }
            }
            Assert.True(jet, "방수포 제트가 나가지 않았다");
            Assert.True(bomb, "물폭탄이 나가지 않았다");
            Assert.Equal(5, sim.Drones.Count);
        }

        // --- 노란 특수 장비·펌프·한 줄기 물대포 ---

        /// <summary>플레이어가 레벨업 카드에서 이 장비를 골랐을 때(Choose 경로).</summary>
        private static void Take(SurvivorSim sim, UpgradeId id)
        {
            sim.PendingChoices = new List<UpgradeId> { id };
            sim.Choose(0);
        }

        /// <summary>이번 틱에 새로 나간 물대포 물방울.</summary>
        private static List<Shot> FreshDrops(SurvivorSim sim)
        {
            return sim.Shots.FindAll(s => s.Kind == ShotKind.Drop && s.Age <= SurvivorSim.Dt);
        }

        [Fact]
        public void Curtain_PushesFiresAway()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Curtain);
            var embers = new List<Enemy>();
            var blazes = new List<Enemy>();
            for (int k = 0; k < 6; k++)
            {
                double a = System.Math.PI * 2 * k / 6;
                float dx = (float)System.Math.Cos(a) * 3f;
                float dy = (float)System.Math.Sin(a) * 3f;
                embers.Add(Dummy(sim, EnemyKind.Ember, dx, dy, 2f));
                blazes.Add(Dummy(sim, EnemyKind.Blaze, dx * 0.8f, dy * 0.8f, 999f));
            }
            bool burst = false;
            for (int i = 0; i < 60 * 2 && !burst; i++)
            {
                sim.Step(0f, 0f);
                burst |= sim.JustCurtain;
            }
            Assert.True(burst, "장막이 2초 안에 안 터졌다");
            Run(sim, 0.5f);
            Assert.True(embers.TrueForAll(e => e.Dead), "장막 안 불씨가 남았다");
            Assert.True(blazes.TrueForAll(e => e.Hp < e.MaxHp && e.Pos.DistanceTo(sim.Player) > 3f), "큰 불이 밀려나지 않았다");
        }

        [Fact]
        public void Partner_RescuesWhileYouStandFar()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 12f, 0f, 2);
            sim.Ignite(shop, 0.3f);
            Take(sim, UpgradeId.Partner);
            sim.Step(0f, 0f);
            Assert.Single(sim.Partners);
            for (int i = 0; i < 60 * 10 && sim.Rescued == 0; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
            }
            Assert.True(sim.Rescued >= 1, "동료가 10초 안에 한 명도 못 구했다");
            Assert.True(shop.Door.DistanceTo(sim.Player) > SurvivorSim.RescueRange, "소방관이 문 앞에 간 게 아니어야 한다");
        }

        // --- 소방서: 시작 장비 ---

        [Fact]
        public void StartingGear_IsWhatTheStationGives()
        {
            var sim = new SurvivorSim(1, 1, Roster.Get("veteran").Start) { Guardian = false };
            Assert.Equal(1, sim.Build.Level(UpgradeId.Hose));
            Assert.Equal(1, sim.Build.Level(UpgradeId.Curtain));
            Assert.Equal(1, sim.Build.Level(UpgradeId.Suit));
            Assert.Equal(0, sim.Build.Level(UpgradeId.Partner));
            Assert.Equal(sim.MaxHp, sim.Hp);
            Assert.True(sim.MaxHp > SurvivorSim.BaseMaxHp, "방화복이 최대 체력을 올려야 한다");

            var rookie = new SurvivorSim(1) { Guardian = false };
            Assert.Equal(1, rookie.Build.Level(UpgradeId.Hose));
            Assert.Equal(0, rookie.Build.Level(UpgradeId.Suit));
            Assert.Equal(SurvivorSim.BaseMaxHp, rookie.Hp);

            // 빈 손으로도 터지지 않는다: 쏴도 아무 일이 없다.
            var bare = new SurvivorSim(1, 1, new UpgradeId[0]) { Guardian = false };
            bare.Enemies.Clear();
            bare.Aim = new Vec2(1f, 0f);
            bare.Spraying = true;
            bare.Step(0f, 0f);
            Assert.Empty(bare.Shots);
        }

        // --- 연속 진압 콤보 ---

        private static Enemy Far(SurvivorSim sim, float dx)
        {
            Enemy e = sim.Spawn(EnemyKind.Ember, new Vec2(sim.Player.X + dx, sim.Player.Y + 8f));
            e.Speed = 0f;
            return e;
        }

        [Fact]
        public void Combo_GrowsWithKills_AndBreaksAfterTheWindow()
        {
            SurvivorSim sim = Quiet();
            for (int i = 0; i < 3; i++) sim.Kill(Far(sim, i));
            Assert.Equal(3, sim.Combo);
            Assert.Equal(3, sim.Stats.MaxCombo);
            Assert.Equal(1, sim.ComboMult);

            int ended = 0;
            for (int i = 0; i < (int)((SurvivorSim.ComboWindow + 0.1f) / SurvivorSim.Dt); i++)
            {
                sim.Step(0f, 0f);
                if (sim.ComboEnded > 0) ended = sim.ComboEnded;
            }
            Assert.Equal(3, ended);
            Assert.Equal(0, sim.Combo);
        }

        [Fact]
        public void Combo_MultipliesGems_UpToThree()
        {
            SurvivorSim sim = Quiet();
            for (int i = 0; i < SurvivorSim.ComboStep - 1; i++) sim.Kill(Far(sim, i));
            Assert.False(sim.JustComboTier);
            Assert.Equal(1, sim.ComboMult);

            sim.Kill(Far(sim, -5f));
            Assert.True(sim.JustComboTier, "ComboStep번째 처치에 배율이 올라야 한다");
            Assert.Equal(2, sim.ComboMult);

            Enemy ninth = Far(sim, -8f);
            sim.Kill(ninth);
            Gem gem = sim.Gems.Find(g => g.Pos.X == ninth.Pos.X && g.Pos.Y == ninth.Pos.Y);
            Assert.NotNull(gem);
            Assert.Equal(2, gem.Value);

            for (int i = 0; i < 2 * SurvivorSim.ComboStep; i++) sim.Kill(Far(sim, 20f + i));
            Assert.Equal(SurvivorSim.ComboMaxMult, sim.ComboMult);
        }

        [Fact]
        public void DousingABuilding_AddsFiveCombo()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 6f, 0f);
            sim.Ignite(shop, 0.05f);
            for (int i = 0; i < 120 && shop.Burning; i++) Spray(sim, shop.Pos, 1);
            Assert.False(shop.Burning, "작은 불을 2초 안에 못 껐다");
            Assert.Equal(SurvivorSim.ComboPerDouse, sim.Combo);
            Gem gem = sim.Gems.Find(g => g.Pos.DistanceTo(shop.Door) < 0.01f);
            Assert.NotNull(gem);
            Assert.Equal(8, gem.Value);
        }

        [Fact]
        public void Hose_PushesTheCrowdBack()
        {
            SurvivorSim sim = Quiet();
            // 불씨는 2.4로 다가온다. 노즐 가까이의 물줄기 밀치기가 그보다 세면 거리가 벌어진다.
            Enemy ember = sim.Spawn(EnemyKind.Ember, new Vec2(sim.Player.X + 2.5f, sim.Player.Y));
            ember.MaxHp = 999f;
            ember.Hp = 999f;
            float before = ember.Pos.DistanceTo(sim.Player);
            for (int i = 0; i < 90; i++)
            {
                sim.Aim = new Vec2(1f, 0f);
                sim.Spraying = true;
                sim.Step(0f, 0f);
            }
            Assert.True(ember.Pos.DistanceTo(sim.Player) > before, "물줄기를 맞은 불씨가 밀려나지 않았다: " + before + " → " + ember.Pos.DistanceTo(sim.Player));
        }

        [Fact]
        public void Hose_StaysOneStream_ButGrowsThickerAndStronger()
        {
            SurvivorSim sim = Quiet();
            var ahead = new Vec2(sim.Player.X + 10f, sim.Player.Y);
            Spray(sim, ahead, 1);
            List<Shot> lv1 = FreshDrops(sim);
            Assert.Single(lv1);

            SurvivorSim big = Quiet();
            for (int i = 1; i < Loadout.MaxLevel; i++) Take(big, UpgradeId.Hose);
            Spray(big, ahead, 1);
            List<Shot> lv5 = FreshDrops(big);
            Assert.Single(lv5);
            Assert.True(lv5[0].Radius > lv1[0].Radius * 2f, "Lv5 물줄기가 충분히 굵지 않다");
            Assert.True(lv5[0].Damage > lv1[0].Damage * 2.5f, "Lv5 물줄기가 충분히 세지 않다");
            Assert.True(lv5[0].Life > lv1[0].Life, "Lv5 물줄기가 더 멀리 가지 않는다");
        }

        [Fact]
        public void Pump_ShootsFartherAndHarder()
        {
            SurvivorSim plain = Quiet();
            SurvivorSim pumped = Quiet();
            for (int i = 0; i < 3; i++) Take(pumped, UpgradeId.Tank);
            var ahead = new Vec2(plain.Player.X + 10f, plain.Player.Y);
            Spray(plain, ahead, 1);
            Spray(pumped, ahead, 1);
            Shot a = FreshDrops(plain)[0];
            Shot b = FreshDrops(pumped)[0];
            Assert.Equal(1.45f, b.Life / a.Life, 2);
            Assert.Equal(1.45f, b.Damage / a.Damage, 2);
        }

        [Fact]
        public void HoseShots_AreMarked_SweepJetsAreNot()
        {
            SurvivorSim sim = Quiet();
            var ahead = new Vec2(sim.Player.X + 10f, sim.Player.Y);
            Spray(sim, ahead, 1);
            Assert.True(FreshDrops(sim)[0].Hose, "물대포 물방울이 호스 물로 표시되지 않았다");

            // 방수포 진화(카드 경로): 쥔 호스 제트는 호스 물, 사방으로 도는 제트는 아니다.
            SurvivorSim big = Quiet();
            for (int i = 1; i < Loadout.MaxLevel; i++) Take(big, UpgradeId.Hose);
            Take(big, UpgradeId.Tank);
            Take(big, UpgradeId.Cannon);
            Spray(big, ahead, 10);
            List<Shot> jets = big.Shots.FindAll(s => s.Kind == ShotKind.Jet);
            Assert.Contains(jets, s => s.Hose);
            Assert.Contains(jets, s => !s.Hose);
            // 사방 제트 수명 0.8초 × 펌프 사거리(Lv1 1.15).
            Assert.All(jets.FindAll(s => !s.Hose), s => Assert.Equal(0.8f * big.Build.HoseRange, s.Life, 3));
        }
    }
}
