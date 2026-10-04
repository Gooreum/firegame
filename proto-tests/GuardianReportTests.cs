using System;
using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using Xunit;
using Xunit.Abstractions;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 수호자 마을 측정(docs §20). 숙련 봇, 이동만 하는 봇(쥐지 않음: 사람 대리), 옛 마을 숙련 봇을 같은 씨앗으로 끝까지 돌려
    /// 승패·지킨 비율·버티기·최저 체력·쓰러진 까닭을 나란히 본다.
    /// dotnet test proto-tests --filter GuardianReport --logger "console;verbosity=detailed"
    /// </summary>
    [Collection("Heavy")]
    public class GuardianReportTests
    {
        private readonly ITestOutputHelper _out;

        public GuardianReportTests(ITestOutputHelper output)
        {
            _out = output;
        }

        public sealed class GuardRow
        {
            public string Name;
            public int Runs;
            public int Won;
            public int Down;
            public int LostTown;
            public float Saved;
            public float SavedMin = 1f;
            public readonly List<float> SavedAll = new List<float>();
            public float SiegesWon;
            public float SiegesLost;
            public float MinHp;
            public float FinaleHp;
            public float SiegeHp;
            public float SiegeTime;
            public int SiegeClose;
            public float Rescued;
            public float PeopleLost;
            public float Haven;
            public float Auto;
            public float Focus;
            public float Level;
            public float Pressure;
            public readonly float[] Hurt = new float[4];

            /// <summary>쓰러진 판에서 가장 많이 받은 피해 출처별 판 수.</summary>
            public readonly int[] DownBy = new int[4];

            public float Spread
            {
                get
                {
                    if (SavedAll.Count == 0) return 0f;
                    var sorted = new List<float>(SavedAll);
                    sorted.Sort();
                    return sorted[(int)(sorted.Count * 0.9f) - 1 < 0 ? 0 : (int)(sorted.Count * 0.9f) - 1] - sorted[(int)(sorted.Count * 0.1f)];
                }
            }

            public override string ToString()
            {
                float hurt = Hurt[0] + Hurt[1] + Hurt[2] + Hurt[3];
                string share = hurt <= 0f ? "-" : "닿음 " + Pct(Hurt[0] / hurt) + " 열기 " + Pct(Hurt[1] / hurt) + " 바닥 " + Pct(Hurt[2] / hurt) + " 폭발 " + Pct(Hurt[3] / hurt);
                return Name + ": 승 " + Won + "/" + Runs + " 쓰러짐 " + Down + (LostTown > 0 ? " 동네 " + LostTown : "")
                    + " | 지킨 비율 평균 " + Pct(Saved) + " 최저 " + Pct(SavedMin) + " (10~90% 폭 " + Pct(Spread) + ")"
                    + " | 버티기 " + SiegesWon.ToString("0.0") + "성공 " + SiegesLost.ToString("0.0") + "실패 한 번 " + (SiegesWon + SiegesLost > 0f ? SiegeTime / (SiegesWon + SiegesLost) : 0f).ToString("0.0") + "초 링 최저 체력 " + Pct(SiegeHp) + " 아슬(≤35%) " + SiegeClose + "판"
                    + " | 최저 체력 " + Pct(MinHp) + " 대화재 최저 " + Pct(FinaleHp)
                    + " | 구조 " + Rescued.ToString("0.0") + " 잃음 " + PeopleLost.ToString("0.0")
                    + " | 쉼터 회복 " + Haven.ToString("0") + " | 자동/쥠 " + Auto.ToString("0") + "/" + Focus.ToString("0")
                    + " | 끝 레벨 " + Level.ToString("0.0") + " 감독 " + Pressure.ToString("0.0")
                    + " | 피해 " + share + " | 쓰러진 까닭 닿음 " + DownBy[0] + " 열기 " + DownBy[1] + " 바닥 " + DownBy[2] + " 폭발 " + DownBy[3];
            }

            private static string Pct(float v)
            {
                return (v * 100f).ToString("0") + "%";
            }
        }

        /// <summary>마을을 seeds판 끝까지. guardian=false면 옛 마을.</summary>
        public static GuardRow Measure(string name, int seeds, bool guardian, bool pro, bool moveOnly)
        {
            var row = new GuardRow { Name = name, Runs = seeds };
            for (int seed = 1; seed <= seeds; seed++)
            {
                var sim = new SurvivorSim(seed, 1) { Guardian = guardian };
                var bot = new SurvivorBot(sim) { Pro = pro, MoveOnly = moveOnly };
                while (sim.Outcome == SOutcome.Playing) bot.Play();
                bool won = sim.Outcome == SOutcome.Won;
                if (won) row.Won++;
                else if (sim.LostTown) row.LostTown++;
                else
                {
                    row.Down++;
                    int worst = 0;
                    for (int k = 1; k < 4; k++) if (sim.Stats.HurtBy[k] > sim.Stats.HurtBy[worst]) worst = k;
                    row.DownBy[worst]++;
                }
                float saved = sim.VillageSaved;
                row.Saved += saved;
                row.SavedMin = Math.Min(row.SavedMin, saved);
                row.SavedAll.Add(saved);
                row.SiegesWon += sim.Stats.SiegesWon;
                row.SiegesLost += sim.Stats.SiegesLost;
                row.MinHp += sim.Stats.MinHpRatio;
                row.SiegeHp += sim.Stats.SiegeMinHp;
                row.SiegeTime += sim.Stats.SiegeTime;
                if (sim.Stats.SiegeMinHp <= 0.35f) row.SiegeClose++;
                row.FinaleHp += sim.Finale ? sim.Stats.FinaleMinHp : 0f;
                row.Rescued += sim.Rescued;
                row.PeopleLost += sim.CiviliansLost;
                row.Haven += sim.Stats.HealHaven;
                row.Auto += sim.Stats.AutoShots;
                row.Focus += sim.Stats.FocusShots;
                row.Level += sim.Level;
                row.Pressure += sim.Stats.PressurePeak;
                for (int k = 0; k < 4; k++) row.Hurt[k] += sim.Stats.HurtBy[k];
            }
            float d = 1f / seeds;
            row.Saved *= d;
            row.SiegesWon *= d;
            row.SiegesLost *= d;
            row.MinHp *= d;
            row.SiegeHp *= d;
            row.SiegeTime *= d;
            row.FinaleHp *= d;
            row.Rescued *= d;
            row.PeopleLost *= d;
            row.Haven *= d;
            row.Auto *= d;
            row.Focus *= d;
            row.Level *= d;
            row.Pressure *= d;
            return row;
        }

        [Fact]
        public void GuardianReport_MoveOnlySurvives_SkillSavesMore()
        {
            int seeds = FunTests.Seeds;
            GuardRow pro = Measure("수호자 숙련", seeds, true, true, false);
            GuardRow move = Measure("수호자 이동만(숙련 걸음)", seeds, true, true, true);
            GuardRow basic = Measure("수호자 이동만(기본 걸음)", seeds, true, false, true);
            GuardRow old = Measure("옛 마을 숙련", seeds, false, true, false);
            foreach (GuardRow r in new[] { pro, move, basic, old }) _out.WriteLine(r.ToString());

            Assert.Equal(0, pro.LostTown + move.LostTown + basic.LostTown);
            Assert.Equal(0, (int)move.Focus);
            // 이동만으로 버틴다(30판에 ±4 잡음): 다가가는 걸음이면 대부분 살아남는다.
            Assert.True(move.Won >= seeds * 0.6f, "이동만으로 못 버틴다: " + move.Won + "/" + seeds);
            // 상처는 있지만 버텨냈다: 지킨 비율이 평균 55~85%에 넓게 퍼진다.
            Assert.InRange(move.Saved, 0.55f, 0.85f);
            Assert.True(move.Spread >= 0.3f, "판마다 상처가 비슷하다: 폭 " + move.Spread);
            // 서는 곳이 실력: 불에 안 다가가는 걸음은 확실히 덜 지킨다.
            Assert.True(basic.Saved <= move.Saved - 0.15f, "다가가는 걸음이 더 지켜야: 기본 " + basic.Saved + " 숙련 걸음 " + move.Saved);
            // 버티기는 몸에 닿는다: 링 안 최저 체력이 평균 35~80%.
            Assert.InRange(move.SiegeHp, 0.35f, 0.8f);
            Assert.True(move.SiegesWon >= 1.5f, "버티기를 거의 안 한다: " + move.SiegesWon);
        }
    }
}
