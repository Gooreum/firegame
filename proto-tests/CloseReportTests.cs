using System;
using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using Xunit;
using Xunit.Abstractions;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 숙련 봇(사람처럼 서서 끄는 봇)으로 "끝이 아슬아슬한가"를 잰다. 기본 봇은 건물 곁에 안 서서 감독이 늘 숨을 주는 쪽에 머문다(docs §15).
    /// 아슬아슬 판 = 이겼고 (건물 여유 ≤ 1 또는 대화재 뒤 최저 체력 ≤ 35%).
    /// </summary>
    public class CloseReportTests
    {
        private readonly ITestOutputHelper _out;

        public CloseReportTests(ITestOutputHelper output)
        {
            _out = output;
        }

        public const float CloseHp = 0.35f;

        private static SurvivorSim Quiet(int stage = 1)
        {
            var sim = new SurvivorSim(1, stage);
            sim.Reports = false;
            sim.Structures.Clear();
            sim.Enemies.Clear();
            return sim;
        }

        private static Structure House(SurvivorSim sim, float dx, float dy, int residents)
        {
            var s = new Structure { Kind = StructureKind.House, Name = "가게", Pos = new Vec2(sim.Player.X + dx, sim.Player.Y + dy), Half = new Vec2(2f, 1.5f), Residents = residents };
            sim.Structures.Add(s);
            return s;
        }

        /// <summary>적 없이 봇을 seconds 동안 돌린다(카드는 봇이 고른다).</summary>
        private static void Run(SurvivorSim sim, SurvivorBot bot, float seconds, bool clearEnemies = true)
        {
            for (int i = 0; i < (int)(seconds / SurvivorSim.Dt) && sim.Outcome == SOutcome.Playing; i++)
            {
                if (clearEnemies) sim.Enemies.Clear();
                bot.Play();
            }
        }

        [Fact]
        public void ProBot_StandsAndDouses_WhereTheBasicBotWontStand()
        {
            // 1.0 불 집 10칸 앞: 숙련 봇은 가장자리 2.5칸에 서서 20초 안에 끈다.
            var sim = Quiet();
            Structure shop = House(sim, 0f, 10f, 0);
            sim.Ignite(shop, 1f);
            var pro = new SurvivorBot(sim) { Pro = true };
            float doused = -1f;
            for (int i = 0; i < (int)(20f / SurvivorSim.Dt); i++)
            {
                sim.Enemies.Clear();
                pro.Play();
                if (!shop.Burning) { doused = sim.Time; break; }
            }
            Assert.True(doused > 0f, "숙련 봇이 20초 안에 못 껐다: 불 " + shop.Fire);
            Assert.True(shop.DistanceTo(sim.Player) <= SurvivorBot.ProStandOff + 1f, "끌 때 서 있던 거리 " + shop.DistanceTo(sim.Player));

            // 큰 불 하나가 집 곁에 있어도(2.5칸 밖) 물러나지 않고 계속 끈다: 기본 봇은 7칸 안 적을 피해 서지 못한다.
            sim = Quiet();
            shop = House(sim, 0f, 10f, 0);
            sim.Ignite(shop, 1f);
            Enemy guard = sim.Spawn(EnemyKind.Blaze, new Vec2(sim.Player.X + 6f, sim.Player.Y + 10f));
            guard.Speed = 0f;
            guard.Hp = guard.MaxHp = 999f;
            pro = new SurvivorBot(sim) { Pro = true };
            for (int i = 0; i < (int)(25f / SurvivorSim.Dt) && shop.Burning; i++)
            {
                sim.Enemies.RemoveAll(e => e != guard);
                sim.Hp = sim.MaxHp;
                pro.Play();
            }
            Assert.False(shop.Burning, "큰 불 하나 곁에서 숙련 봇이 못 껐다: 불 " + shop.Fire);
        }

        [Fact]
        public void ProBot_RescuesAtTheDoor()
        {
            var sim = Quiet();
            Structure shop = House(sim, 0f, 8f, 2);
            sim.Ignite(shop, 0.5f);
            var pro = new SurvivorBot(sim) { Pro = true };
            Run(sim, pro, 15f);
            Assert.True(sim.Rescued >= 1, "15초 안에 구조 0 (주민 " + shop.Residents + ", 불 " + shop.Fire + ")");
        }

        [Fact]
        public void ProBot_RetreatsWhenHurt()
        {
            var sim = Quiet();
            Structure shop = House(sim, 0f, 5f, 0);
            sim.Ignite(shop, 1f);
            sim.Hp = sim.MaxHp * 0.25f;
            Vec2 p = sim.Player;
            var blazes = new List<Enemy>();
            // 큰 불 넷이 북동쪽에(타는 집도 북쪽): 다친 숙련 봇은 불로 가지 않고 남서로 물러난다.
            foreach (Vec2 at in new[] { new Vec2(p.X + 3f, p.Y), new Vec2(p.X, p.Y + 3f), new Vec2(p.X + 2f, p.Y + 2f), new Vec2(p.X + 3f, p.Y + 3f) })
            {
                Enemy b = sim.Spawn(EnemyKind.Blaze, at);
                b.Speed = 0f;
                b.Hp = b.MaxHp = 999f;
                blazes.Add(b);
            }
            float before = Nearest(blazes, sim.Player);
            var pro = new SurvivorBot(sim) { Pro = true };
            for (int i = 0; i < (int)(2f / SurvivorSim.Dt); i++)
            {
                pro.Play();
                sim.Hp = Math.Max(sim.Hp, sim.MaxHp * 0.25f);
            }
            float after = Nearest(blazes, sim.Player);
            Assert.True(after > before + 1f, "다쳤는데 안 물러났다: " + before + " → " + after);
        }

        private static float Nearest(List<Enemy> list, Vec2 p)
        {
            float best = 99f;
            foreach (Enemy e in list) best = Math.Min(best, e.Pos.DistanceTo(p));
            return best;
        }

        [Fact]
        public void FinaleMinHp_TracksOnlyTheFinale()
        {
            var sim = new SurvivorSim(1);
            sim.Reports = false;
            // 3:00 전 체력 10%: 전체 최저엔 잡히지만 대화재 최저엔 안 잡힌다.
            sim.Hp = sim.MaxHp * 0.1f;
            sim.Enemies.Clear();
            sim.Step(0f, 0f);
            Assert.InRange(sim.Stats.MinHpRatio, 0.09f, 0.11f);
            Assert.Equal(1f, sim.Stats.FinaleMinHp);
            sim.Hp = sim.MaxHp;
            sim.Reports = true;
            while (!sim.Finale && sim.Outcome == SOutcome.Playing)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                foreach (Structure s in sim.Structures) s.Fire = 0f;
                sim.Step(0f, 0f);
            }
            Assert.True(sim.Finale);
            sim.Hp = sim.MaxHp * 0.4f;
            sim.Enemies.Clear();
            sim.Step(0f, 0f);
            Assert.InRange(sim.Stats.FinaleMinHp, 0.38f, 0.41f);
        }

        public sealed class CloseRow
        {
            public int Stage;
            public int Won;
            public int LostHp;
            public int LostTown;
            public int Close;
            public float Pressure;
            public float FinaleHp;
            public float Room;
            public float Rescued;
            public float PeopleLost;
            public float HousesLost;
            /// <summary>3:00 레벨 평균과 끝에 쥔 장비 종류 수(진화·노란 포함) 평균: 아이템 다이어트가 보이는 숫자.</summary>
            public float Level3;
            public float Items;

            public override string ToString()
            {
                return "스테이지 " + Stage + " " + SurvivorStages.Get(Stage).Name + ": 승 " + Won + " (아슬 " + Close + ") | 패 체력 " + LostHp + " 동네 " + LostTown
                    + " | 감독 단계 " + Pressure.ToString("0.0") + " | 대화재 최저 체력 " + (FinaleHp * 100f).ToString("0") + "% | 남은 여유 " + Room.ToString("0.0") + "채"
                    + " | 구조 " + Rescued.ToString("0.0") + " 잃음 " + PeopleLost.ToString("0.0") + " | 무너진 건물 " + HousesLost.ToString("0.0")
                    + " | 3:00 레벨 " + Level3.ToString("0.0") + " 장비 " + Items.ToString("0.0") + "종";
            }
        }

        /// <summary>숙련 봇으로 끝까지. 끝에서의 승패·여유·대화재 최저 체력을 센다.</summary>
        public static CloseRow Measure(int stage, int seeds)
        {
            var row = new CloseRow { Stage = stage };
            for (int seed = 1; seed <= seeds; seed++)
            {
                var sim = new SurvivorSim(seed, stage);
                var bot = new SurvivorBot(sim) { Pro = true };
                while (sim.Outcome == SOutcome.Playing) bot.Play();
                bool won = sim.Outcome == SOutcome.Won;
                if (won) row.Won++;
                else if (sim.LostTown) row.LostTown++;
                else row.LostHp++;
                if (won && (sim.HousesRoom <= 1 || sim.Stats.FinaleMinHp <= CloseHp)) row.Close++;
                row.Pressure += sim.Stats.PressurePeak;
                row.FinaleHp += sim.Finale ? sim.Stats.FinaleMinHp : 0f;
                row.Room += sim.HousesRoom;
                row.Rescued += sim.Rescued;
                row.PeopleLost += sim.CiviliansLost;
                row.HousesLost += sim.HousesLost;
                row.Level3 += sim.Finale ? sim.Stats.FinaleLevel : sim.Level;
                foreach (UpgradeId id in sim.Build.Owned()) row.Items += 1f;
            }
            float k = 1f / seeds;
            row.Pressure *= k;
            row.FinaleHp *= k;
            row.Room *= k;
            row.Rescued *= k;
            row.PeopleLost *= k;
            row.HousesLost *= k;
            row.Level3 *= k;
            row.Items *= k;
            return row;
        }

        /// <summary>
        /// 아슬아슬 표. dotnet test proto-tests --filter CloseReport --logger "console;verbosity=detailed"
        /// 목표: 마을 승 50~90%, 아슬 승 ≥ 승의 절반, 감독 평균 ≥ 1.5(감독이 실제로 몰아붙인다). 숲·공단: 승 ≥ 20%, 아슬 ≥ 승의 ⅓.
        /// </summary>
        [Fact]
        public void CloseReport_ProBotFinishesOnTheEdge()
        {
            int seeds = FunTests.Seeds;
            var rows = new List<CloseRow>();
            for (int stage = 1; stage <= SurvivorStages.Count; stage++)
            {
                CloseRow row = Measure(stage, seeds);
                rows.Add(row);
                _out.WriteLine(row.ToString());
            }
            CloseRow town = rows[0];
            Assert.InRange(town.Won * 10f / seeds, 5f, 9f);
            Assert.True(town.Close * 2 >= town.Won, "마을 끝이 싱겁다: 아슬 " + town.Close + " / 승 " + town.Won);
            Assert.True(town.Pressure >= 1.5f, "감독이 안 몰아붙인다: 평균 " + town.Pressure);
            for (int i = 1; i < rows.Count; i++)
            {
                Assert.True(rows[i].Won * 5 >= seeds, rows[i].Stage + "스테이지 승이 너무 적다: " + rows[i].Won + "/" + seeds);
                Assert.True(rows[i].Close * 3 >= rows[i].Won, rows[i].Stage + "스테이지 끝이 싱겁다: 아슬 " + rows[i].Close + " / 승 " + rows[i].Won);
            }
            // 항구는 승이 마을 수준이어도 더 아슬아슬해야 한다: 아슬 비율이 마을 이상이거나 대화재 최저 체력이 마을 이하(docs §17).
            if (rows.Count >= 4)
            {
                CloseRow harbor = rows[3];
                bool tenser = harbor.Close * town.Won >= town.Close * harbor.Won || harbor.FinaleHp <= town.FinaleHp + 0.02f;
                Assert.True(tenser, "항구 끝이 마을보다 싱겁다: 아슬 " + harbor.Close + "/" + harbor.Won + " 최저 체력 " + harbor.FinaleHp + " (마을 " + town.Close + "/" + town.Won + " " + town.FinaleHp + ")");
            }
        }
    }
}
