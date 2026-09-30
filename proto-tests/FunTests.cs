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

            /// <summary>한 번이라도 체력이 절반 밑으로 떨어진 판 수(위기 판).</summary>
            public int Crises;
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
                    + " | 최저 체력 " + (MinHp * 100f).ToString("0") + "% (위기 판 " + Crises + ")"
                    + " | 구조 " + Rescued.ToString("0.0") + " 잃음 " + PeopleLost.ToString("0.0") + " | 무너진 건물 " + HousesLost.ToString("0.0");
            }
        }

        public static FunRow Measure(int stage, int seeds, UpgradeId? favorite = null)
        {
            var row = new FunRow { Stage = stage };
            for (int seed = 1; seed <= seeds; seed++)
            {
                var sim = new SurvivorSim(seed, stage);
                var bot = new SurvivorBot(sim) { Favorite = favorite };
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
                if (sim.Stats.MinHpRatio < 0.5f) row.Crises++;
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
                // 숲은 체력보다 동네를 잃는 쪽으로 무너진다: 봇 60판 위기 판 평균이 10판당 3.5라 10판 하한은 2로 둔다.
                Assert.InRange(row.Crises, 2, 8);
            }
            // 몸 압박: 체력이 절반 밑으로 떨어진 위기 판이 10판 중 3~8판(없으면 방화복이 쓸모없고, 늘 그러면 구조보다 생존이 먼저다).
            // 평균 최저 체력은 "몇 판은 쓰러지고 나머지는 멀쩡"한 두 갈래 분포를 못 담아 쓰지 않는다.
            Assert.InRange(town.Crises, 3, 8);
        }

        /// <summary>
        /// 아이템 공정성: 아이템 하나를 Lv3까지 먼저 챙기는 봇이 스테이지 1·2를 시드 10개씩 돈다(아이템당 20판).
        /// 쓰레기템(구한 사람이 강제 아이템 평균의 65% 미만)도, 필수템(승이 평균의 2배 넘음)도 없어야 한다.
        /// 판별 편차가 평균의 18% 안팎이라, 10판·70%로는 아무 문제 없어도 자주 실패했다.
        /// dotnet test proto-tests --filter ItemReport --logger "console;verbosity=detailed"
        /// </summary>
        [Fact]
        public void ItemReport_NoDeadOrMustHaveItem()
        {
            UpgradeId[] items =
            {
                UpgradeId.WaterBomb, UpgradeId.Drone, UpgradeId.Partner, UpgradeId.Curtain, UpgradeId.Turret,
                UpgradeId.Tank, UpgradeId.Boots, UpgradeId.Radio, UpgradeId.Axe, UpgradeId.Oxygen, UpgradeId.Suit,
            };
            (int won, float saved, float lost, float minHp) Both(UpgradeId? fav)
            {
                FunRow one = Measure(1, 10, fav);
                FunRow two = Measure(2, 10, fav);
                return (one.Won + two.Won, (one.Rescued + two.Rescued) / 2f, (one.PeopleLost + two.PeopleLost) / 2f, (one.MinHp + two.MinHp) / 2f);
            }
            var basic = Both(null);
            _out.WriteLine("기본 봇: 승 " + basic.won + "/20, 구조 " + basic.saved.ToString("0.0") + ", 잃음 " + basic.lost.ToString("0.0") + ", 최저 체력 " + (basic.minHp * 100f).ToString("0") + "%");
            var rows = new System.Collections.Generic.List<(UpgradeId id, int won, float saved, float lost, float minHp)>();
            foreach (UpgradeId id in items)
            {
                var r = Both(id);
                rows.Add((id, r.won, r.saved, r.lost, r.minHp));
                _out.WriteLine(SurvivorUpgrades.Name(id) + ": 승 " + r.won + "/20, 구조 " + r.saved.ToString("0.0") + ", 잃음 " + r.lost.ToString("0.0") + ", 최저 체력 " + (r.minHp * 100f).ToString("0") + "%");
            }
            // 아이템 하나를 강제로 들면 그만큼 다른 카드를 포기하므로, 기본 봇이 아니라 강제한 아이템들의 평균과 비교한다.
            float avgWon = 0f;
            float avgSaved = 0f;
            foreach (var r in rows)
            {
                avgWon += r.won;
                avgSaved += r.saved;
            }
            avgWon /= rows.Count;
            avgSaved /= rows.Count;
            foreach (var r in rows)
            {
                Assert.True(r.saved >= avgSaved * 0.65f, SurvivorUpgrades.Name(r.id) + "를 들면 구한 사람이 너무 적다: " + r.saved + " (평균 " + avgSaved + ")");
                Assert.True(r.won <= Math.Max(avgWon * 2f, avgWon + 2f), SurvivorUpgrades.Name(r.id) + "만 너무 잘 이긴다: " + r.won + " (평균 " + avgWon + ")");
            }
            // 방화복 없이도(기본 봇은 방화복을 거의 안 고른다) 이길 수 있고, 방화복을 들면 몸 압박이 준다.
            // (잃은 사람 수는 10판으론 판마다 1~5명씩 흔들려 판정에 못 쓴다.)
            // 불 규칙 강화(측정 5) 뒤 목표 승률이 마을 30~40%, 숲 20%대라 20판 기대 승은 5~6이다. 하한은 4.
            Assert.True(basic.won >= 4, "기본 봇이 너무 못 이긴다: " + basic.won + "/20");
            // 같은 보조끼리 비교한다: 보조를 먼저 챙기면 무기가 늦게 차서 몸 압박이 커진다(무기 강제와 섞으면 불공정).
            float passiveHp = 0f;
            int passives = 0;
            foreach (var r in rows)
            {
                if (!Loadout.IsPassive(r.id)) continue;
                passiveHp += r.minHp;
                passives++;
            }
            passiveHp /= passives;
            var suit = rows.Find(r => r.id == UpgradeId.Suit);
            Assert.True(suit.minHp >= passiveHp, "방화복을 들어도 체력이 더 깎인다: " + suit.minHp + " (보조 평균 " + passiveHp + ")");
        }
    }
}
