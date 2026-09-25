using System.Collections.Generic;
using FireGame.Core.Grid;
using FireGame.Prototypes.Logic;
using Xunit;
using Xunit.Abstractions;

namespace FireGame.Prototypes.Tests
{
    /// <summary>시험판 A 규칙. 쏘면 꺼지고, 덜 끄면 살아나고, 가만있으면 지고, 제대로 뛰면 이긴다.</summary>
    public class ActionTests
    {
        private readonly ITestOutputHelper _out;
        public ActionTests(ITestOutputHelper output) { _out = output; }

        private static ActionSim Small(params string[] rows)
        {
            return new ActionSim(new ActionLevel("t", rows), 1);
        }

        private static void Run(ActionSim sim, AInput input, float seconds)
        {
            int ticks = (int)(seconds / ActionSim.Dt);
            for (int i = 0; i < ticks; i++) sim.Step(input);
        }

        private static AInput AimAt(ActionSim sim, int x, int y)
        {
            float a = (float)System.Math.Atan2((y + 0.5f) - sim.Player.Y, (x + 0.5f) - sim.Player.X);
            return new AInput { AimRadians = a, Spraying = true };
        }

        // 사람이 멀리 있어 판이 바로 끝나지 않는 연습 맵
        private static readonly string[] Room =
        {
            "############",
            "#P..*.....C#",
            "#..........#",
            "#.........H#",
            "###########X",
        };

        // --- TC-1 ---
        [Fact]
        public void SprayingAFireForOneSecond_PutsItOut()
        {
            ActionSim sim = Small(Room);
            Run(sim, AimAt(sim, 4, 1), 1f);
            Assert.False(sim.Burning(4, 1));
        }

        // --- TC-2 ---
        [Fact]
        public void AFireHalfPutOut_ComesBack()
        {
            ActionSim sim = Small(Room);
            Run(sim, AimAt(sim, 4, 1), 0.12f);
            Assert.True(sim.Burning(4, 1), "잠깐 쐈는데 꺼졌다");
            float after = sim.Heat(4, 1);
            Run(sim, default, 3f);
            Assert.True(sim.Burning(4, 1));
            Assert.True(sim.Heat(4, 1) > after, "덜 끈 불이 다시 커지지 않는다");
        }

        // --- TC-3 ---
        [Fact]
        public void LeftAlone_TheFireGrows()
        {
            var sim = new ActionSim(ActionLevel.Building, 3);
            int before = sim.BurningCount;
            Run(sim, default, 10f);
            Assert.True(sim.BurningCount > before, before + " → " + sim.BurningCount);
        }

        // --- TC-4 ---
        [Fact]
        public void FlareUpsAndFlashovers_AreAnnouncedAhead()
        {
            var sim = new ActionSim(ActionLevel.Building, 5);
            var seen = new Dictionary<WarningKind, float>();
            bool flare = false, flash = false;
            float flareLead = 0f, flashLead = 0f;

            for (int i = 0; i < (int)(120f / ActionSim.Dt) && sim.Outcome == AOutcome.Playing; i++)
            {
                foreach (Warning w in sim.Warnings)
                {
                    if (!seen.ContainsKey(w.Kind) || w.SecondsLeft > seen[w.Kind]) seen[w.Kind] = w.SecondsLeft;
                }
                // 가만히 서 있되 체력은 채워 둬서(테스트용) 판이 끝나지 않게 한다.
                sim.Hp = 100f;
                sim.Step(default);
                if (sim.JustFlaredUp && !flare) { flare = true; flareLead = seen.TryGetValue(WarningKind.FlareUp, out float f) ? f : 0f; }
                if (sim.JustFlashover && !flash) { flash = true; flashLead = seen.TryGetValue(WarningKind.Flashover, out float g) ? g : 0f; }
            }

            Assert.True(flare, "솟음이 한 번도 없었다");
            Assert.True(flash, "플래시오버가 한 번도 없었다");
            Assert.True(flareLead >= ActionSim.FlareWarning - 0.05f, "솟음 예고 " + flareLead + "초");
            Assert.True(flashLead >= ActionSim.FlashoverWarning - 0.05f, "플래시오버 예고 " + flashLead + "초");
        }

        // --- TC-5 ---
        [Fact]
        public void TankEmptiesInEightSeconds_AndRefillsAtTheHydrant()
        {
            ActionSim sim = Small(Room);
            Run(sim, new AInput { AimRadians = 1.57f, Spraying = true }, ActionSim.TankSeconds + 0.1f);
            Assert.Equal(0f, sim.Tank);
            Assert.False(sim.SprayingNow);

            // 소화전(10,3) 옆으로 걸어간다.
            for (int i = 0; i < 600 && sim.Player.DistanceTo(new Vec2(10.5f, 3.5f)) > 1.2f; i++)
            {
                float dx = 9.5f - sim.Player.X, dy = 3.5f - sim.Player.Y;
                float len = (float)System.Math.Sqrt((dx * dx) + (dy * dy));
                sim.Hp = 100f;
                sim.Step(new AInput { MoveX = dx / len, MoveY = dy / len });
            }
            Run(sim, default, ActionSim.RefillSeconds + 0.1f);
            Assert.Equal(1f, sim.Tank);
        }

        // --- TC-6 ---
        [Fact]
        public void APersonFollows_AndIsRescuedAtTheExit()
        {
            ActionSim sim = Small(
                "#########",
                "#PC....X#",
                "#########");
            for (int i = 0; i < 600 && sim.Outcome == AOutcome.Playing; i++) sim.Step(new AInput { MoveX = 1f });
            Assert.Equal(1, sim.Rescued);
            Assert.Equal(AOutcome.Won, sim.Outcome);
        }

        // --- TC-7 ---
        [Fact]
        public void StandingStill_Loses()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                var sim = new ActionSim(ActionLevel.Building, seed);
                while (sim.Outcome == AOutcome.Playing) sim.Step(default);
                Assert.Equal(AOutcome.Lost, sim.Outcome);
            }
        }

        // --- TC-8 ---
        [Fact]
        public void ScriptedBot_WinsMostSeeds()
        {
            int wins = 0;
            string log = "";
            for (int seed = 1; seed <= 10; seed++)
            {
                var sim = new ActionSim(ActionLevel.Building, seed);
                var bot = new ActionBot(sim);
                while (sim.Outcome == AOutcome.Playing) sim.Step(bot.Next());
                if (sim.Outcome == AOutcome.Won) wins++;
                log += " " + seed + ":" + sim.Outcome + "(구조 " + sim.Rescued + ", 잃음 " + sim.LostCount + ", 체력 " + (int)sim.Hp + ", " + (int)sim.Elapsed + "초)";
            }
            _out.WriteLine(log);
            Assert.True(wins >= 6, "봇이 10판 중 " + wins + "판만 이겼다." + log);
        }

        // --- TC-9 ---
        [Fact]
        public void SameSeedSameInputs_SameRun()
        {
            var a = new ActionSim(ActionLevel.Building, 42);
            var b = new ActionSim(ActionLevel.Building, 42);
            var botA = new ActionBot(a);
            var botB = new ActionBot(b);
            for (int i = 0; i < 1800; i++)
            {
                a.Step(botA.Next());
                b.Step(botB.Next());
                Assert.Equal(a.BurningCount, b.BurningCount);
                Assert.Equal(a.Player.X, b.Player.X);
                Assert.Equal(a.Tank, b.Tank);
            }
        }
    }
}
