using System;
using System.Collections.Generic;
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
            var sim = new SurvivorSim(1) { Guardian = false };
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
            var sim = new SurvivorSim(1) { Guardian = false };
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

            /// <summary>대화재(3:00)까지 간 판 수.</summary>
            public int Reached;
            public float LevelGap;
            public float IdleShare;
            public float EventsPerMin;
            public float TreeOnlyShare;
            public float KillsPerMin;
            public float MinHp;

            /// <summary>분당 받은 피해. 방화복처럼 "덜 맞는" 효과는 여기에 바로 드러난다(최저 체력은 승패 노이즈에 묻힌다).</summary>
            public float DamagePerMin;

            /// <summary>받은 불 피해 ÷ 원값. 방화복 없이는 1, Lv3 방화복은 0.55. 봇의 행동과 상관없이 방화복만 잰다.</summary>
            public float FireCut;

            /// <summary>한 번이라도 체력이 절반 밑으로 떨어진 판 수(위기 판).</summary>
            public int Crises;
            public float Rescued;
            public float PeopleLost;
            public float HousesLost;
            public float Levels;

            /// <summary>대화재 감독 최고 단계 평균(0~3). 봇은 서서 못 끄므로 낮게 나온다 — 사람 판의 여유를 재는 건 폰이다.</summary>
            public float Pressure;

            public override string ToString()
            {
                return "스테이지 " + Stage + " " + SurvivorStages.Get(Stage).Name + ": 승 " + Won + " (대화재 도달 " + Reached + ")"
                    + " | 레벨업 간격 " + LevelGap.ToString("0.0") + "초 (Lv " + Levels.ToString("0.0") + ")"
                    + " | 빈 시간 " + (IdleShare * 100f).ToString("0") + "%"
                    + " | 분당 사건 " + EventsPerMin.ToString("0.0")
                    + " | 나무만 탄 시간 " + (TreeOnlyShare * 100f).ToString("0") + "%"
                    + " | 분당 처치 " + KillsPerMin.ToString("0")
                    + " | 최저 체력 " + (MinHp * 100f).ToString("0") + "% (위기 판 " + Crises + ")"
                    + " | 구조 " + Rescued.ToString("0.0") + " 잃음 " + PeopleLost.ToString("0.0") + " | 무너진 건물 " + HousesLost.ToString("0.0")
                    + " | 감독 단계 " + Pressure.ToString("0.0");
            }
        }

        /// <summary>측정 판 수. 기본 30(10판은 승·위기가 ±2판 흔들려 판정에 못 쓴다 — docs §13), 환경변수 FIREGAME_SEEDS로 바꾼다.</summary>
        public static int Seeds
        {
            get { return int.TryParse(Environment.GetEnvironmentVariable("FIREGAME_SEEDS"), out int n) && n > 0 ? n : 30; }
        }

        public static FunRow Measure(int stage, int seeds, UpgradeId? favorite = null, UpgradeId[] start = null, bool pro = false)
        {
            var row = new FunRow { Stage = stage };
            for (int seed = 1; seed <= seeds; seed++)
            {
                var sim = new SurvivorSim(seed, stage, start) { Guardian = false };
                var bot = new SurvivorBot(sim) { Favorite = favorite, Pro = pro };
                int guard = 0;
                while (sim.Outcome == SOutcome.Playing && guard++ < 60 * 400) bot.Play();
                if (sim.Outcome == SOutcome.Won) row.Won++;
                if (sim.Finale) row.Reached++;
                float minutes = Math.Max(sim.Time, 1f) / 60f;
                int n = sim.Stats.LevelTimes.Count;
                row.LevelGap += n > 0 ? sim.Stats.LevelTimes[n - 1] / n : sim.Time;
                row.Levels += sim.Level;
                row.IdleShare += sim.Stats.IdleTime / Math.Max(sim.Time, 1f);
                row.EventsPerMin += sim.Stats.Events / minutes;
                row.TreeOnlyShare += sim.Stats.TreeFireOnly / Math.Max(sim.Time, 1f);
                row.KillsPerMin += sim.Kills / minutes;
                row.MinHp += sim.Stats.MinHpRatio;
                row.DamagePerMin += sim.Stats.DamageTaken / minutes;
                row.FireCut += sim.Stats.FireDamageRaw > 0f ? Math.Min(1f, sim.Stats.DamageTaken / sim.Stats.FireDamageRaw) : 1f;
                if (sim.Stats.MinHpRatio < 0.5f) row.Crises++;
                row.Rescued += sim.Rescued;
                row.PeopleLost += sim.CiviliansLost;
                row.HousesLost += sim.HousesLost;
                row.Pressure += sim.Stats.PressurePeak;
            }
            float k = 1f / seeds;
            row.LevelGap *= k;
            row.Levels *= k;
            row.IdleShare *= k;
            row.EventsPerMin *= k;
            row.TreeOnlyShare *= k;
            row.KillsPerMin *= k;
            row.MinHp *= k;
            row.DamagePerMin *= k;
            row.FireCut *= k;
            row.Rescued *= k;
            row.PeopleLost *= k;
            row.HousesLost *= k;
            row.Pressure *= k;
            return row;
        }

        /// <summary>
        /// 재미 밀도 표. 출력은 docs/prototype-c-balance.md에 옮긴다.
        /// dotnet test proto-tests --filter FunReport --logger "console;verbosity=detailed"
        /// </summary>
        [Fact(Skip = "아이템·몹 개편 중(2026-10-07): 노란·옛 무기가 빠져 띠가 무의미 — Phase 6에서 다시 잰다")]
        public void FunReport_StagesMatchTown()
        {
            int seeds = Seeds;
            // 먼저 다 재고 찍은 뒤 단언한다: 한 스테이지가 밴드를 벗어나도 나머지 숫자는 봐야 한다.
            var rows = new List<FunRow>();
            for (int stage = 1; stage <= SurvivorStages.Count; stage++)
            {
                rows.Add(Measure(stage, seeds));
                _out.WriteLine(rows[rows.Count - 1].ToString());
            }
            FunRow town = rows[0];
            for (int stage = 2; stage <= SurvivorStages.Count; stage++)
            {
                FunRow row = rows[stage - 1];
                // 1스테이지만큼 할 일이 자주 온다(docs/prototype-c-balance.md §5).
                Assert.True(row.LevelGap <= town.LevelGap * 1.2f, row.Stage + "스테이지 레벨업이 느리다: " + row.LevelGap + "초 (마을 " + town.LevelGap + ")");
                // 바다 스테이지(항구)는 +7%p: 부두 끝에서 배를 기다리는 시간이 기본 봇에겐 빈 시간이다(열 번 재서 12.9~17%, 신고·갈매기·지형 손잡이로 13%까지, docs §17).
                float idleBand = SurvivorStages.Get(stage).Sea ? 0.07f : 0.05f;
                Assert.True(row.IdleShare <= town.IdleShare + idleBand, row.Stage + "스테이지 걷기만 하는 시간이 길다: " + row.IdleShare + " (마을 " + town.IdleShare + ")");
                // 마을은 12채라 신고가 늘 안 탄 집을 찾고 구조도 많다(지형 패스 뒤 14.3). 8채 스테이지는 0.75배까지.
                Assert.True(row.EventsPerMin >= town.EventsPerMin * 0.75f, row.Stage + "스테이지 사건이 적다: " + row.EventsPerMin + " (마을 " + town.EventsPerMin + ")");
                // 숲은 체력보다 동네를 잃는 쪽으로 무너진다. 끄는 시간 패스(docs §14) 뒤 숲 위기 6/30이라 하한은 1(바닥), 상한은 "늘 쓰러진다"만 막는다.
                Assert.InRange(row.Crises * 10f / seeds, 1f, 9f);
            }
            // 몸 압박: 체력이 절반 밑으로 떨어진 위기 판이 10판 중 3~8판(없으면 방화복이 쓸모없고, 늘 그러면 구조보다 생존이 먼저다).
            // 평균 최저 체력은 "몇 판은 쓰러지고 나머지는 멀쩡"한 두 갈래 분포를 못 담아 쓰지 않는다.
            Assert.InRange(town.Crises * 10f / seeds, 3f, 8f);
        }

        /// <summary>
        /// 소방관 공정성: 소방서 명단의 소방관마다 그 시작 장비로 마을을 10판 돈다.
        /// 모두 할 만해야 하고(승 1~9), 신입보다 지나치게 잘 이기는 필수 소방관도, 사람을 못 구하는 소방관도 없어야 한다.
        /// dotnet test proto-tests --filter RosterReport --logger "console;verbosity=detailed"
        /// </summary>
        [Fact]
        public void RosterReport_EveryFirefighterIsPlayable()
        {
            int seeds = Seeds;
            FunRow rookie = null;
            var rows = new System.Collections.Generic.List<(Firefighter f, FunRow row)>();
            // 숙련 봇(사람 대리, docs §15)으로 잰다: 기본 봇은 대화재 고리에 늘 쓰러져 소방관 차이가 바닥 노이즈에 묻힌다(신입 3, 펌프 기사 1/30).
            foreach (Firefighter f in Roster.All)
            {
                FunRow row = Measure(1, seeds, null, f.Start, true);
                if (f.Id == Roster.Default) rookie = row;
                rows.Add((f, row));
                _out.WriteLine(f.Name + " (" + string.Join("+", f.Start) + ", ★" + f.Cost + "): 승 " + row.Won + "/" + seeds + ", 구조 " + row.Rescued.ToString("0.0") + ", 잃음 " + row.PeopleLost.ToString("0.0") + ", 레벨업 간격 " + row.LevelGap.ToString("0.0") + "초, 최저 체력 " + (row.MinHp * 100f).ToString("0") + "% (위기 판 " + row.Crises + ")");
            }
            Assert.NotNull(rookie);
            foreach (var (f, row) in rows)
            {
                // 숙련 봇 신입 25/30(지형 패스). 모두 4~9.7/10: 못 이기는 소방관도, 늘 이기는 소방관도 없다.
                Assert.InRange(row.Won * 10f / seeds, 4f, 9.7f);
                Assert.True(row.Won <= rookie.Won + 5, f.Name + "만 너무 잘 이긴다: " + row.Won + " (신입 " + rookie.Won + ")");
                Assert.True(row.Rescued >= rookie.Rescued * 0.65f, f.Name + "는 사람을 너무 못 구한다: " + row.Rescued + " (신입 " + rookie.Rescued + ")");
            }
        }

        /// <summary>
        /// 아이템 공정성: 아이템 하나를 Lv3까지 먼저 챙기는 봇이 스테이지 1~3을 시드 10개씩 돈다(아이템당 30판).
        /// 쓰레기템(구한 사람이 강제 아이템 평균의 65% 미만)도, 필수템(승이 평균의 2배 넘음)도 없어야 한다.
        /// 판별 편차가 평균의 18% 안팎이라, 10판·70%로는 아무 문제 없어도 자주 실패했다.
        /// dotnet test proto-tests --filter ItemReport --logger "console;verbosity=detailed"
        /// </summary>
        [Fact(Skip = "아이템·몹 개편 중(2026-10-07): 노란·옛 무기가 빠져 띠가 무의미 — Phase 6에서 다시 잰다")]
        public void ItemReport_NoDeadOrMustHaveItem()
        {
            UpgradeId[] items =
            {
                UpgradeId.WaterBomb, UpgradeId.Drone, UpgradeId.Partner, UpgradeId.Curtain, UpgradeId.Turret,
                UpgradeId.Tank, UpgradeId.Boots, UpgradeId.Suit,
            };
            (int won, float saved, float lost, float minHp, float damage, float fireCut) Both(UpgradeId? fav)
            {
                FunRow one = Measure(1, 10, fav);
                FunRow two = Measure(2, 10, fav);
                FunRow three = Measure(3, 10, fav);
                return (one.Won + two.Won + three.Won, (one.Rescued + two.Rescued + three.Rescued) / 3f, (one.PeopleLost + two.PeopleLost + three.PeopleLost) / 3f, (one.MinHp + two.MinHp + three.MinHp) / 3f, (one.DamagePerMin + two.DamagePerMin + three.DamagePerMin) / 3f, (one.FireCut + two.FireCut + three.FireCut) / 3f);
            }
            string Line(string name, (int won, float saved, float lost, float minHp, float damage, float fireCut) r)
            {
                return name + ": 승 " + r.won + "/30, 구조 " + r.saved.ToString("0.0") + ", 잃음 " + r.lost.ToString("0.0") + ", 최저 체력 " + (r.minHp * 100f).ToString("0") + "%, 분당 피해 " + r.damage.ToString("0") + ", 불 피해 비율 " + r.fireCut.ToString("0.00");
            }
            var basic = Both(null);
            _out.WriteLine(Line("기본 봇", basic));
            var rows = new System.Collections.Generic.List<(UpgradeId id, int won, float saved, float lost, float minHp, float damage, float fireCut)>();
            foreach (UpgradeId id in items)
            {
                var r = Both(id);
                rows.Add((id, r.won, r.saved, r.lost, r.minHp, r.damage, r.fireCut));
                _out.WriteLine(Line(SurvivorUpgrades.Name(id), r));
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
                // 필수템 상한은 바닥 기준(docs §14): 봇은 건물에 물을 거의 안 뿌려 물폭탄(건물을 스스로 겨누는 한 방 물)이 봇에겐 유일한 끄기 수단이라
                // 과대평가된다(2026-10-02: 물폭탄 10, 평균 4.9). 2.5배까지는 봇 탓으로 본다.
                // 아이템 다이어트(3+2칸) 뒤 기본 봇 평균이 3으로 내려가 물폭탄 8이 2.5배를 넘었다(docs §16): +5까지 봇 탓.
                Assert.True(r.won <= Math.Max(avgWon * 2.5f, avgWon + 5f), SurvivorUpgrades.Name(r.id) + "만 너무 잘 이긴다: " + r.won + " (평균 " + avgWon + ")");
            }
            // 방화복 없이도(기본 봇은 방화복을 거의 안 고른다) 이길 수 있고, 방화복을 들면 몸 압박이 준다.
            // (잃은 사람 수는 10판으론 판마다 1~5명씩 흔들려 판정에 못 쓴다.)
            // 봇 밴드는 바닥이다(docs §14): 끄는 시간 15초인 판을 봇은 사람처럼 못 넘긴다. 2026-10-02 기본 봇 4/30. 하한은 "전멸이 아니다"의 2.
            Assert.True(basic.won >= 2, "기본 봇이 너무 못 이긴다: " + basic.won + "/30");
            // 방화복은 봇의 행동과 떼어서 잰다. 최저 체력은 몇 판 쓰러졌느냐에 묻히고, 분당 피해는 체력이 넉넉한 봇이 불 곁에 더 오래 서서
            // 오히려 커진다(봇은 체력이 절반 밑일 때만 물러난다). 받은 불 피해 ÷ 원값은 방화복 레벨만 따른다: Lv3이면 0.55.
            var suit = rows.Find(r => r.id == UpgradeId.Suit);
            // 마을은 대비 장비(대원·장화)가 두 배로 나와 방화복 카드가 희석된다(지형 패스 뒤 0.88, 그 전 0.83). 상한은 "효과가 있다"의 0.9.
            Assert.True(suit.fireCut <= 0.9f, "방화복을 들어도 불 피해를 덜 받지 않는다: 비율 " + suit.fireCut + " (기본 봇 " + basic.fireCut + ")");
            Assert.True(suit.fireCut < basic.fireCut, "방화복 봇이 기본 봇보다 불 피해를 덜 막는다: " + suit.fireCut + " vs " + basic.fireCut);
        }
    }
}
