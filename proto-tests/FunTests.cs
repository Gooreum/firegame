using System;
using FireGame.Prototypes.Logic;
using Xunit;
using Xunit.Abstractions;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 재미 밀도: 재밌다고 확인된 1스테이지(마을)를 기준점으로 뒤 스테이지를 잰다.
    /// 봇은 재미를 못 재지만, "할 일이 얼마나 자주 오나"(레벨업 간격, 걷기만 한 시간, 사건 수)는 잰다.
    /// </summary>
    [Collection("Heavy")]
    public class FunTests
    {
        private readonly ITestOutputHelper _out;

        public FunTests(ITestOutputHelper output)
        {
            _out = output;
        }

        private static SurvivorSim Quiet()
        {
            var sim = new SurvivorSim(1);
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        private static void Run(SurvivorSim sim, float seconds)
        {
            int ticks = (int)(seconds / SurvivorSim.Dt);
            for (int i = 0; i < ticks; i++)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
            }
        }

        [Fact]
        public void Stats_CountLevelUps()
        {
            SurvivorSim sim = Quiet();
            sim.DropGem(sim.Player, sim.XpToNext);
            for (int i = 0; i < 3 && sim.PendingChoices == null; i++) sim.Step(0f, 0f);
            Assert.NotNull(sim.PendingChoices);
            Assert.Single(sim.Stats.LevelTimes);
            Assert.Equal(sim.Time, sim.Stats.LevelTimes[0], 3);
        }

        [Fact]
        public void Stats_CountDamageAndLowestHp()
        {
            SurvivorSim sim = Quiet();
            sim.Spawn(EnemyKind.Blaze, sim.Player);
            for (int i = 0; i < 30; i++) sim.Step(0f, 0f);
            Assert.True(sim.Stats.DamageTaken > 0f);
            Assert.True(sim.Stats.MinHpRatio < 1f);
            Assert.Equal(sim.Stats.DamageTaken, sim.MaxHp - sim.Hp, 1);
        }

        [Fact]
        public void Stats_EmptyTownIsIdle()
        {
            SurvivorSim sim = Quiet();
            Run(sim, 1f);
            Assert.InRange(sim.Stats.IdleTime, 0.95f, 1.05f);
            Assert.Equal(0f, sim.Stats.BuildingFire);
        }

        [Fact]
        public void Stats_BurningBuildingIsNotIdle()
        {
            SurvivorSim sim = Quiet();
            var shop = new Structure { Kind = StructureKind.House, Name = "가게", Pos = new Vec2(sim.Player.X + 5f, sim.Player.Y), Half = new Vec2(2f, 1.5f) };
            sim.Structures.Add(shop);
            sim.Ignite(shop, 0.3f);
            Run(sim, 1f);
            Assert.InRange(sim.Stats.BuildingFire, 0.95f, 1.05f);
            Assert.Equal(0f, sim.Stats.IdleTime);
        }

        [Fact]
        public void Stats_CountReportsAsEvents()
        {
            var sim = new SurvivorSim(1);
            float first = sim.Stage.ReportTimes[0];
            int ticks = (int)((first + 0.5f) / SurvivorSim.Dt);
            for (int i = 0; i < ticks && sim.PendingChoices == null; i++)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
            }
            Assert.True(sim.Stats.Events >= 1);
        }

        /// <summary>스테이지 하나를 봇으로 여러 판 돈 평균.</summary>
        public sealed class FunRow
        {
            public int Stage;
            public int Won;
            public float LevelGap;
            public float IdleShare;
            public float EventsPerMin;
            public float TreeOnlyShare;
            public float KillsPerMin;
            public float MinHp;
            public float Rescued;
            public float PeopleLost;
            public float HousesLost;
            public float Levels;

            public override string ToString()
            {
                return "스테이지 " + Stage + " " + SurvivorStages.Get(Stage).Name + ": 승 " + Won
                    + " | 레벨업 간격 " + LevelGap.ToString("0.0") + "초 (Lv " + Levels.ToString("0.0") + ")"
                    + " | 빈 시간 " + (IdleShare * 100f).ToString("0") + "%"
                    + " | 분당 사건 " + EventsPerMin.ToString("0.0")
                    + " | 나무만 탄 시간 " + (TreeOnlyShare * 100f).ToString("0") + "%"
                    + " | 분당 처치 " + KillsPerMin.ToString("0")
                    + " | 최저 체력 " + (MinHp * 100f).ToString("0") + "%"
                    + " | 구조 " + Rescued.ToString("0.0") + " 잃음 " + PeopleLost.ToString("0.0") + " | 무너진 건물 " + HousesLost.ToString("0.0");
            }
        }

        public static FunRow Measure(int stage, int seeds)
        {
            var row = new FunRow { Stage = stage };
            for (int seed = 1; seed <= seeds; seed++)
            {
                var sim = new SurvivorSim(seed, stage);
                var bot = new SurvivorBot(sim);
                int guard = 0;
                while (sim.Outcome == SOutcome.Playing && guard++ < 60 * 400) bot.Play();
                if (sim.Outcome == SOutcome.Won) row.Won++;
                float minutes = Math.Max(sim.Time, 1f) / 60f;
                int n = sim.Stats.LevelTimes.Count;
                row.LevelGap += n > 0 ? sim.Stats.LevelTimes[n - 1] / n : sim.Time;
                row.Levels += sim.Level;
                row.IdleShare += sim.Stats.IdleTime / Math.Max(sim.Time, 1f);
                row.EventsPerMin += sim.Stats.Events / minutes;
                row.TreeOnlyShare += sim.Stats.TreeFireOnly / Math.Max(sim.Time, 1f);
                row.KillsPerMin += sim.Kills / minutes;
                row.MinHp += sim.Stats.MinHpRatio;
                row.Rescued += sim.Rescued;
                row.PeopleLost += sim.CiviliansLost;
                row.HousesLost += sim.HousesLost;
            }
            float k = 1f / seeds;
            row.LevelGap *= k;
            row.Levels *= k;
            row.IdleShare *= k;
            row.EventsPerMin *= k;
            row.TreeOnlyShare *= k;
            row.KillsPerMin *= k;
            row.MinHp *= k;
            row.Rescued *= k;
            row.PeopleLost *= k;
            row.HousesLost *= k;
            return row;
        }

        /// <summary>
        /// 재미 밀도 표. 출력은 docs/prototype-c-balance.md에 옮긴다.
        /// dotnet test proto-tests --filter FunReport --logger "console;verbosity=detailed"
        /// </summary>
        [Fact]
        public void FunReport_StagesMatchTown()
        {
            FunRow town = Measure(1, 10);
            _out.WriteLine(town.ToString());
            for (int stage = 2; stage <= SurvivorStages.Count; stage++)
            {
                FunRow row = Measure(stage, 10);
                _out.WriteLine(row.ToString());
                // 1스테이지만큼 할 일이 자주 온다(docs/prototype-c-balance.md §5).
                Assert.True(row.LevelGap <= town.LevelGap * 1.2f, row.Stage + "스테이지 레벨업이 느리다: " + row.LevelGap + "초 (마을 " + town.LevelGap + ")");
                Assert.True(row.IdleShare <= town.IdleShare + 0.05f, row.Stage + "스테이지 걷기만 하는 시간이 길다: " + row.IdleShare + " (마을 " + town.IdleShare + ")");
                Assert.True(row.EventsPerMin >= town.EventsPerMin * 0.8f, row.Stage + "스테이지 사건이 적다: " + row.EventsPerMin + " (마을 " + town.EventsPerMin + ")");
            }
        }
    }
}
