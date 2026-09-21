using System;
using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using Xunit;
using Xunit.Abstractions;

namespace FireGame.Core.Tests
{
    /// <summary>스테이지 난이도를 실측해 콘솔에 표로 뽑는다. 튜닝용.</summary>
    public class StageBalanceProbe
    {
        private readonly ITestOutputHelper _output;

        public StageBalanceProbe(ITestOutputHelper output)
        {
            _output = output;
        }

        private static readonly (string Label, int[] Loadout)[] Loadouts =
        {
            ("양동이만", new[] { EquipmentId.Bucket }),
            ("+소화기", new[] { EquipmentId.Bucket, EquipmentId.Extinguisher }),
            ("+호스", new[] { EquipmentId.Bucket, EquipmentId.Extinguisher, EquipmentId.Hose }),
            ("+폼", new[] { EquipmentId.Bucket, EquipmentId.Extinguisher, EquipmentId.FoamExtinguisher }),
            ("4종 전부", new[] { EquipmentId.Bucket, EquipmentId.Extinguisher, EquipmentId.Hose, EquipmentId.FoamExtinguisher }),
        };

        [Fact]
        public void MeasureEveryStageAgainstEveryLoadout()
        {
            _output.WriteLine(string.Format("{0,-14} {1,-10} {2,-24} {3,7} {4,8} {5,7} {6,7}",
                "스테이지", "장비", "결과", "남은초", "무결성", "구조", "보상"));
            _output.WriteLine(new string('-', 88));

            foreach (StageDef stage in StageCatalog.All)
            {
                foreach ((string label, int[] loadout) in Loadouts)
                {
                    var runner = new StageRunner(stage, new List<int>(loadout));
                    var bot = new GreedyBot(runner);
                    StageOutcome outcome = bot.Play();

                    StageResult result = runner.BuildResult();
                    int payout = Economy.Payout(result);

                    _output.WriteLine(string.Format(
                        "{0,-14} {1,-10} {2,-24} {3,7:F1} {4,8:P0} {5,3}/{6,-3} {7,7}  연소{8,3} 대기{9,2} HP{10,4:F0}",
                        stage.Name, label, outcome, runner.TimeLeft,
                        result.IntactRatio, result.Rescued, result.CiviliansTotal, payout,
                        runner.Grid.CountBurning(), runner.PendingCivilianCount, runner.Player.Hp)
                        + string.Format("  발사{0,5}", bot.ShotsFired));
                }
                _output.WriteLine("");
            }
        }
    }
}
