using System;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 보스 대신 건물과 사람을 지키는 절정: 1:20·2:40 대형 신고(다 구하면 보물상자), 3:00 대화재, 4:00 생존 승리.
    /// </summary>
    public class FinaleTests
    {
        /// <summary>
        /// 한 틱: 불 몹은 치우고 체력은 채우고 카드는 첫 장. 대형 신고·대화재 건물이 아닌 불은 끈다
        /// (다른 소방관이 맡았다고 치고, 이 테스트가 보는 규칙만 남긴다).
        /// </summary>
        private static void Tick(SurvivorSim sim, float mx = 0f, float my = 0f)
        {
            sim.Enemies.Clear();
            sim.Hp = sim.MaxHp;
            if (sim.PendingChoices != null) sim.Choose(0);
            foreach (Structure s in sim.Structures)
            {
                if (s.Burning && s != sim.BigReport && s != sim.Landmark) s.Fire = 0f;
            }
            sim.Step(mx, my);
        }

        private static void RunTo(SurvivorSim sim, float t)
        {
            while (sim.Outcome == SOutcome.Playing && sim.Time < t) Tick(sim);
        }

        /// <summary>불을 끄지 않고 간다(번짐·신고가 쌓이는 걸 본다).</summary>
        private static void RunRaw(SurvivorSim sim, float t)
        {
            while (sim.Outcome == SOutcome.Playing && sim.Time < t)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
            }
        }

        /// <summary>대형 신고가 나는 틱까지 가서 그 건물을 돌려준다.</summary>
        private static Structure ToBigReport(SurvivorSim sim)
        {
            while (sim.BigReport == null && sim.Outcome == SOutcome.Playing) Tick(sim);
            Assert.NotNull(sim.BigReport);
            return sim.BigReport;
        }

        /// <summary>플레이어처럼 걸어간다(최대 maxTicks).</summary>
        private static void WalkTo(SurvivorSim sim, Vec2 at, int maxTicks, Func<bool> done)
        {
            for (int i = 0; i < maxTicks && !done() && sim.Outcome == SOutcome.Playing; i++)
            {
                float dx = at.X - sim.Player.X;
                float dy = at.Y - sim.Player.Y;
                float d = (float)Math.Sqrt((dx * dx) + (dy * dy));
                if (d < 0.05f) Tick(sim);
                else Tick(sim, dx / d, dy / d);
            }
        }

        [Fact]
        public void BigReport_At80s_BigFireAndThreeMorePeople()
        {
            var sim = new SurvivorSim(1);
            RunTo(sim, SurvivorSim.BigReportTimes[0] - 0.5f);
            Assert.Null(sim.BigReport);
            int[] before = sim.Structures.ConvertAll(s => s.Residents).ToArray();
            bool signal = false;
            while (sim.BigReport == null && sim.Outcome == SOutcome.Playing)
            {
                Tick(sim);
                signal |= sim.JustBigReport;
            }
            Structure big = sim.BigReport;
            Assert.True(signal);
            Assert.Equal(StructureKind.House, big.Kind);
            Assert.True(big.Fire >= SurvivorSim.BigReportFire - 0.01f);
            Assert.Equal(before[sim.Structures.IndexOf(big)] + SurvivorSim.BigReportPeople, big.Residents);
            Assert.InRange(sim.Time, SurvivorSim.BigReportTimes[0], SurvivorSim.BigReportTimes[0] + 0.1f);
        }

        [Fact]
        public void RescuingEveryone_DropsAChest_AndItOpensTwoPicksStartingYellow()
        {
            var sim = new SurvivorSim(1);
            Structure big = ToBigReport(sim);
            // 다른 가게의 사람은 이 테스트와 상관없다: 대형 신고 건물만 끈질기게 탄다.
            WalkTo(sim, big.Door, 60 * 30, () => big.Residents <= 0);
            Assert.Null(sim.BigReport);
            // 마지막 사람을 구한 틱에 문 앞(발밑)에 상자가 떨어지고, 서 있던 소방관이 바로 줍는다.
            bool opened = sim.JustChest;
            if (!opened)
            {
                Assert.Single(sim.Chests);
                Assert.True(sim.Chests[0].Pos.DistanceTo(big.Door) < 0.01f);
                Vec2 chest = sim.Chests[0].Pos;
                for (int i = 0; i < 300 && !opened; i++)
                {
                    sim.Enemies.Clear();
                    if (sim.PendingChoices != null) sim.Choose(0);
                    float dx = chest.X - sim.Player.X;
                    float dy = chest.Y - sim.Player.Y;
                    float d = (float)Math.Sqrt((dx * dx) + (dy * dy));
                    sim.Step(d < 0.05f ? 0f : dx / d, d < 0.05f ? 0f : dy / d);
                    opened = sim.JustChest;
                }
            }
            Assert.Empty(sim.Chests);
            Assert.True(opened);
            Assert.NotNull(sim.PendingChoices);
            Assert.Contains(sim.PendingChoices, id => Loadout.IsSpecial(id));
            sim.Choose(0);
            Assert.NotNull(sim.PendingChoices);
            sim.Choose(0);
            Assert.Null(sim.PendingChoices);
        }

        [Fact]
        public void CollapsedBigReport_GivesNoChest()
        {
            var sim = new SurvivorSim(1);
            Structure big = ToBigReport(sim);
            big.Integrity = 0.0001f;
            RunTo(sim, sim.Time + 1f);
            Assert.True(big.Collapsed);
            Assert.Null(sim.BigReport);
            Assert.Empty(sim.Chests);
        }

        [Fact]
        public void Finale_ReportsEveryEightSeconds_AddingAPerson()
        {
            var sim = new SurvivorSim(1);
            RunTo(sim, SurvivorSim.FinaleAt + 0.1f);
            Assert.True(sim.Finale);
            Assert.Equal(1f, sim.Landmark.Fire, 2);
            // 신고를 받을 안 탄 가게를 넉넉히 만든다.
            foreach (Structure s in sim.Structures)
            {
                if (s.Kind == StructureKind.House && !s.Collapsed && s != sim.Landmark) s.Fire = 0f;
            }
            int[] before = sim.Structures.ConvertAll(s => s.Residents).ToArray();
            int burning = sim.Structures.FindAll(s => s.Kind == StructureKind.House && s.Burning).Count;
            RunRaw(sim, sim.Time + SurvivorSim.FinaleReportEvery);
            Structure hit = sim.Structures.Find(s => s.Kind == StructureKind.House && s.Burning && s.Residents > before[sim.Structures.IndexOf(s)]);
            Assert.NotNull(hit);
            Assert.True(sim.Structures.FindAll(s => s.Kind == StructureKind.House && s.Burning).Count > burning);
        }

        [Fact]
        public void SurvivingToFourMinutes_Wins()
        {
            var sim = new SurvivorSim(1);
            sim.Reports = false;
            RunTo(sim, SurvivorSim.RunTime + 1f);
            Assert.Equal(SOutcome.Won, sim.Outcome);
            Assert.Equal(3, sim.Stars);
        }

        [Fact]
        public void LosingHalfTheTown_StillLosesBeforeTheEnd()
        {
            var sim = new SurvivorSim(1);
            sim.Reports = false;
            int n = 0;
            foreach (Structure s in sim.Structures)
            {
                if (!s.IsBuilding || n >= 5) continue;
                sim.Ignite(s, 1f);
                s.Integrity = 0.0001f;
                n++;
            }
            RunRaw(sim, 5f);
            Assert.Equal(SOutcome.Lost, sim.Outcome);
            Assert.True(sim.LostTown);
            Assert.True(sim.Time < SurvivorSim.RunTime);
        }

        [Fact]
        public void ForestFinale_WindSpreadsHarder()
        {
            // 같은 나무 쌍 여럿을 대화재 전과 대화재 중에 태워 옮은 수를 센다.
            int Spread(bool finale)
            {
                var sim = new SurvivorSim(3, 2);
                sim.Structures.Clear();
                sim.Reports = false;
                if (finale)
                {
                    sim.Structures.Add(new Structure { Kind = StructureKind.Depot, Name = "제재소", Pos = new Vec2(55f, 55f), Half = new Vec2(1f, 1f) });
                    sim.Reports = true;
                    RunTo(sim, SurvivorSim.FinaleAt + 0.1f);
                    sim.Reports = false;
                }
                else
                {
                    RunTo(sim, 0.1f);
                }
                sim.Wind = new Vec2(1f, 0f);
                var next = new System.Collections.Generic.List<Structure>();
                for (int k = 0; k < 20; k++)
                {
                    float y = 5f + (k * 2.5f);
                    var fire = new Structure { Kind = StructureKind.Tree, Name = "나무", Pos = new Vec2(3f, y), Half = new Vec2(0.6f, 0.6f) };
                    var tree = new Structure { Kind = StructureKind.Tree, Name = "나무", Pos = new Vec2(6f, y), Half = new Vec2(0.6f, 0.6f) };
                    sim.Structures.Add(fire);
                    sim.Structures.Add(tree);
                    next.Add(tree);
                    sim.Ignite(fire, 0.5f);
                }
                RunRaw(sim, sim.Time + SurvivorSim.WindSpreadEvery + 0.5f);
                return next.FindAll(t => t.Burning || t.Collapsed).Count;
            }

            Assert.True(Spread(true) > Spread(false), "대화재 " + Spread(true) + " / 평소 " + Spread(false));
        }
    }
}
