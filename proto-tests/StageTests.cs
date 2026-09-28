using System;
using System.Collections.Generic;
using FireGame.Core.Sim;
using FireGame.Prototypes.Logic;
using Xunit;
using Xunit.Abstractions;

namespace FireGame.Prototypes.Tests
{
    /// <summary>스테이지 표: 1스테이지는 예전 그대로, 뒤 스테이지는 세계 쪽 수치로만 어려워진다.</summary>
    public class StageTests
    {
        private readonly ITestOutputHelper _out;

        public StageTests(ITestOutputHelper output)
        {
            _out = output;
        }

        [Fact]
        public void Stage1_IsTheOriginalTown()
        {
            var sim = new SurvivorSim(1);
            Assert.Equal(1, sim.Stage.Number);
            Assert.Equal(SurvivorSim.ReportTimes, sim.Stage.ReportTimes);
            Assert.Equal(SurvivorSim.FireGrowth, sim.Stage.FireGrowth);
            Assert.Equal(1f, sim.Stage.EnemyHp);
            Assert.Equal(1f, sim.Stage.SpawnRate);
            Assert.Equal(SurvivorTown.Build().Count, sim.Structures.Count);
            sim.Enemies.Clear();
            Assert.Equal(2f, sim.Spawn(EnemyKind.Ember, sim.Player).MaxHp);
            Assert.Equal(1100f, sim.Spawn(EnemyKind.Boss, sim.Player).MaxHp);
        }

        [Fact]
        public void Stage2_ScalesEnemiesAndBoss()
        {
            var one = new SurvivorSim(1, 1);
            var two = new SurvivorSim(1, 2);
            Assert.Equal(2, two.Stage.Number);
            float ember1 = one.Spawn(EnemyKind.Ember, one.Player).MaxHp;
            float ember2 = two.Spawn(EnemyKind.Ember, two.Player).MaxHp;
            Assert.Equal(ember1 * two.Stage.EnemyHp, ember2, 3);
            Assert.True(two.Stage.EnemyHp > 1f && two.Stage.SpawnRate >= 1f && two.Stage.FireGrowth >= one.Stage.FireGrowth);
            Assert.True(two.Spawn(EnemyKind.Boss, two.Player).MaxHp > one.Spawn(EnemyKind.Boss, one.Player).MaxHp);
        }

        [Fact]
        public void YellowCards_ComeOnlyFromTheStagePool()
        {
            for (int stage = 1; stage <= SurvivorStages.Count; stage++)
            {
                UpgradeId[] pool = SurvivorStages.Get(stage).Specials;
                var seen = new HashSet<UpgradeId>();
                for (int seed = 1; seed <= 20; seed++)
                {
                    var sim = new SurvivorSim(seed, stage);
                    sim.Enemies.Clear();
                    sim.Structures.Clear();
                    sim.Reports = false;
                    // 플레이어 경로: 구슬을 먹어 레벨업하고 카드를 고른다(노란 카드가 있으면 그걸).
                    while (sim.Level < 16)
                    {
                        sim.DropGem(sim.Player, sim.XpToNext);
                        for (int i = 0; i < 3 && sim.PendingChoices == null; i++) sim.Step(0f, 0f);
                        Assert.NotNull(sim.PendingChoices);
                        int pick = 0;
                        for (int k = 0; k < sim.PendingChoices.Count; k++)
                        {
                            UpgradeId id = sim.PendingChoices[k];
                            if (!Loadout.IsSpecial(id) || id == UpgradeId.Cannon) continue;
                            Assert.Contains(id, pool);
                            seen.Add(id);
                            pick = k;
                        }
                        sim.Choose(pick);
                    }
                }
                Assert.Equal(pool.Length, seen.Count);
            }
        }

        [Fact]
        public void Stages_WrapAround()
        {
            Assert.Equal(2, SurvivorStages.Next(1));
            Assert.Equal(1, SurvivorStages.Next(SurvivorStages.Count));
            Assert.Equal(1, SurvivorStages.Get(99).Number);
        }

        /// <summary>
        /// 밸런스 측정: 봇이 스테이지마다 시드 10개를 돈다. 표는 출력만 하고(목표는 docs/prototype-c-balance.md),
        /// 검사는 "뒤 스테이지가 더 어렵다" 하나만 한다.
        /// </summary>
        [Fact]
        public void BalanceReport_LaterStageIsHarder()
        {
            var lost = new int[SurvivorStages.Count + 1];
            var housesLost = new int[SurvivorStages.Count + 1];
            for (int stage = 1; stage <= SurvivorStages.Count; stage++)
            {
                int won = 0;
                int town = 0;
                float time = 0f;
                for (int seed = 1; seed <= 10; seed++)
                {
                    var sim = new SurvivorSim(seed, stage);
                    var bot = new SurvivorBot(sim);
                    int guard = 0;
                    while (sim.Outcome == SOutcome.Playing && guard++ < 60 * 400) bot.Play();
                    if (sim.Outcome == SOutcome.Won) won++;
                    else lost[stage]++;
                    if (sim.LostTown) town++;
                    housesLost[stage] += sim.HousesLost;
                    time += sim.Time;
                }
                _out.WriteLine("스테이지 " + stage + " " + SurvivorStages.Get(stage).Name + ": 승 " + won + "/10 (동네 잃음 " + town + ", 쓰러짐 " + (lost[stage] - town) + "), 평균 " + (int)(time / 10f) + "초, 잃은 건물 평균 " + (housesLost[stage] / 10f).ToString("0.0"));
            }
            Assert.True(lost[2] > lost[1] || housesLost[2] > housesLost[1], "2스테이지가 1스테이지보다 쉽다");
        }
    }
}
