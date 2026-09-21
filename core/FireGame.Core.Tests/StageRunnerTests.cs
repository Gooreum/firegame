using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using FireGame.Core.Sim;
using Xunit;

namespace FireGame.Core.Tests
{
    public class StageRunnerTests
    {
        private static readonly StageInput Idle = default;

        private static StageDef Stage(string[] map, float timeLimit = 60f, Wind? wind = null)
        {
            return new StageDef(
                id: 99,
                name: "TEST",
                map: map,
                wind: wind ?? Wind.None,
                timeLimitSeconds: timeLimit,
                basePayout: 300);
        }

        private static StageRunner Runner(string[] map, float timeLimit = 60f, Wind? wind = null)
        {
            return new StageRunner(Stage(map, timeLimit, wind), EquipmentCatalog.StartingEquipment);
        }

        /// <summary>격자의 모든 불을 강제로 끈다. 승패 판정만 검증할 때 쓴다.</summary>
        private static void ExtinguishEverything(FireGrid grid)
        {
            for (int i = 0; i < grid.Count; i++)
            {
                if (grid.Cells[i].State == CellState.Burning)
                {
                    grid.Cells[i].State = CellState.Intact;
                    grid.Cells[i].Heat = 0f;
                }
            }
        }

        // --- TC-1 ---
        [Fact]
        public void Stage_IsWon_WhenFireIsOutAndEveryoneIsRescued()
        {
            var runner = Runner(new[]
            {
                "#####",
                "#@!X#",
                "##*##",
                "#####",
            });

            // 시민을 업어 출구로 옮긴다.
            for (int i = 0; i < 40 && runner.RescuedCount == 0; i++)
            {
                runner.Update(0.1f, new StageInput { MoveX = 1f });
            }

            Assert.Equal(1, runner.RescuedCount);

            ExtinguishEverything(runner.Grid);
            runner.Update(0.1f, Idle);

            Assert.Equal(StageOutcome.Won, runner.Outcome);
        }

        // --- TC-2 ---
        [Fact]
        public void Stage_IsNotWon_WhileACivilianStillWaits()
        {
            var runner = Runner(new[]
            {
                "#######",
                "#@...!#",
                "###X###",
                "#######",
            });

            ExtinguishEverything(runner.Grid);
            runner.Update(0.1f, Idle);

            Assert.Equal(0, runner.Grid.CountBurning());
            Assert.Equal(1, runner.PendingCivilianCount);
            Assert.Equal(StageOutcome.InProgress, runner.Outcome);
        }

        // --- TC-3 ---
        [Fact]
        public void Stage_IsNotWon_WhileFireStillBurns()
        {
            var runner = Runner(new[]
            {
                "#####",
                "#@.X#",
                "##*##",
                "#####",
            });

            runner.Update(0.1f, Idle);

            Assert.Equal(0, runner.PendingCivilianCount);
            Assert.True(runner.Grid.CountBurning() > 0);
            Assert.Equal(StageOutcome.InProgress, runner.Outcome);
        }

        // --- TC-4 ---
        [Fact]
        public void Stage_IsLost_WhenTooMuchOfTheBuildingBurnsDown()
        {
            // 가연 셀이 전부 목재라 방치하면 순식간에 무결성이 무너진다.
            var runner = Runner(new[]
            {
                "WWWWWWW",
                "W*WWWWW",
                "WWWWWWW",
                "..@....",
            }, timeLimit: 600f);

            for (int i = 0; i < 2000 && !runner.IsOver; i++)
            {
                runner.Update(0.1f, Idle);
            }

            Assert.Equal(StageOutcome.LostBuildingDestroyed, runner.Outcome);
            Assert.True(runner.Grid.IntactRatio() < StageRunner.BuildingLostThreshold);
        }

        // --- TC-5 ---
        [Fact]
        public void Stage_IsLost_WhenThePlayerGoesDown()
        {
            // 가연 셀이 40칸이 넘는 목재 복도라 건물은 한참 버틴다.
            // 여기서 검증할 것은 "HP가 0이 되면 플레이어 사망으로 진다"는 전이다.
            //
            // 멀쩡한 HP로 불 옆에 서 있는 것만으로는 죽지 않는다.
            // 옆 칸이 다 타고 나면 전선이 멀어져 피해가 멎기 때문이다(불길이 지나간 자리는 안전).
            // 그래서 이미 손상이 누적된 상태에서 시작한다.
            var runner = Runner(new[]
            {
                "WWWWWWWWWWWWWWWWWWWW",
                "*@..................",
                "WWWWWWWWWWWWWWWWWWWW",
            }, timeLimit: 600f);

            runner.Player.Hp = 10f;

            for (int i = 0; i < 2000 && !runner.IsOver; i++)
            {
                runner.Update(0.1f, default);
            }

            Assert.Equal(StageOutcome.LostPlayerDown, runner.Outcome);
            Assert.Equal(0f, runner.Player.Hp);
            Assert.True(runner.Grid.IntactRatio() > StageRunner.BuildingLostThreshold,
                "건물은 아직 멀쩡해야 한다. 무결성=" + runner.Grid.IntactRatio());
        }

        [Fact]
        public void StandingStillBesideFire_StopsHurting_OnceTheFrontMovesPast()
        {
            // 불이 번져 나가면 옆 칸은 연료를 다 쓰고 꺼진다.
            // 그때부터는 제자리에 서 있어도 더 이상 피해를 받지 않는다.
            var runner = Runner(new[]
            {
                "WWWWWWWWWWWWWWWWWWWW",
                "*@..................",
                "WWWWWWWWWWWWWWWWWWWW",
            }, timeLimit: 600f);

            float lowest = GameConfig.PlayerMaxHp;
            for (int i = 0; i < 100; i++)
            {
                runner.Update(0.1f, default);
                if (runner.Player.Hp < lowest) lowest = runner.Player.Hp;
            }

            Assert.True(lowest > 0f, "전선이 지나가면 살아남아야 한다");
            Assert.True(lowest < GameConfig.PlayerMaxHp, "지나가는 동안에는 다쳐야 한다");

            // 전선이 멀어지면 더 이상 다치지 않고, 안전해진 뒤에는 회복까지 된다.
            float beforeRecovery = runner.Player.Hp;
            for (int i = 0; i < 50 && !runner.IsOver; i++) runner.Update(0.1f, default);

            Assert.True(runner.Player.Hp >= beforeRecovery,
                "안전해진 뒤에는 체력이 줄지 않아야 한다");
            Assert.True(runner.Player.Hp > lowest, "불에서 벗어나면 회복돼야 한다");
        }

        // --- TC-6 ---
        [Fact]
        public void Stage_IsLost_WhenTimeRunsOut()
        {
            var runner = Runner(new[]
            {
                "#####",
                "#@.X#",
                "##*##",
                "#####",
            }, timeLimit: 2f);

            for (int i = 0; i < 30 && !runner.IsOver; i++)
            {
                runner.Update(0.1f, Idle);
            }

            Assert.Equal(StageOutcome.LostTimeUp, runner.Outcome);
            Assert.Equal(0f, runner.TimeLeft);
        }

        // --- TC-7 ---
        [Fact]
        public void Player_PicksUpACivilianOnContact_AndSlowsDown()
        {
            var runner = Runner(new[]
            {
                "#######",
                "#@..!X#",
                "#######",
            });

            Assert.False(runner.Player.CarryingCivilian);

            for (int i = 0; i < 20 && !runner.Player.CarryingCivilian; i++)
            {
                runner.Update(0.05f, new StageInput { MoveX = 1f });
            }

            Assert.True(runner.Player.CarryingCivilian);

            // 업은 뒤에는 같은 시간에 더 적게 움직인다.
            float before = runner.Player.X;
            runner.Update(0.1f, new StageInput { MoveX = 1f });
            float carriedStep = runner.Player.X - before;

            Assert.True(carriedStep < GameConfig.PlayerSpeed * 0.1f,
                "업은 상태에서는 속도가 느려져야 한다");
            Assert.Equal(GameConfig.PlayerSpeed * GameConfig.CarrySpeedMultiplier * 0.1f, carriedStep, 3);
        }

        // --- TC-8 ---
        [Fact]
        public void CarriedCivilian_IsRescued_OnReachingAnExit()
        {
            var runner = Runner(new[]
            {
                "#######",
                "#@..!X#",
                "#######",
            });

            for (int i = 0; i < 100 && runner.RescuedCount == 0; i++)
            {
                runner.Update(0.05f, new StageInput { MoveX = 1f });
            }

            Assert.Equal(1, runner.RescuedCount);
            Assert.False(runner.Player.CarryingCivilian);
            Assert.Equal(0, runner.PendingCivilianCount);
        }

        // --- TC-9 ---
        [Fact]
        public void PlayerCarriesOnlyOneCivilianAtATime()
        {
            var runner = Runner(new[]
            {
                "#######",
                "#@!!.X#",
                "#######",
            });

            for (int i = 0; i < 40; i++)
            {
                runner.Update(0.05f, new StageInput { MoveX = 1f });

                int carried = 0;
                foreach (Civilian c in runner.Civilians)
                {
                    if (c.Carried) carried++;
                }

                Assert.True(carried <= 1, "동시에 두 명 이상 업을 수 없다");
            }
        }

        // --- TC-10 ---
        [Fact]
        public void Civilian_CaughtByFire_IsLostAndNoLongerBlocksVictory()
        {
            var runner = Runner(new[]
            {
                "#####",
                "#@#X#",
                "###!#",
                "#####",
            });

            // 시민이 서 있는 칸에 직접 불을 붙인다.
            runner.Grid[3, 2].Material = (byte)MaterialId.Wood;
            runner.Grid[3, 2].State = CellState.Burning;

            runner.Update(0.1f, Idle);

            Assert.True(runner.Civilians[0].Lost);
            Assert.Equal(0, runner.PendingCivilianCount);
            Assert.Equal(0, runner.RescuedCount);

            ExtinguishEverything(runner.Grid);
            runner.Update(0.1f, Idle);

            Assert.Equal(StageOutcome.Won, runner.Outcome);
        }

        // --- TC-11 ---
        [Fact]
        public void Simulation_AdvancesIdentically_RegardlessOfFrameRate()
        {
            string[] map =
            {
                "WWWWWWWWWW",
                "W..*....WW",
                "W.WWWW..~W",
                "W...@...WW",
                "WWWWWWWWWW",
            };

            // 총 5.05초를 서로 다른 프레임레이트로 진행한다.
            // 틱 경계(0.1의 배수)를 정확히 밟으면 부동소수점 오차 하나로
            // 틱 수가 1개 갈리므로, 경계에서 0.05초 떨어진 지점에서 비교한다.
            var coarse = Runner(map, timeLimit: 600f);
            for (int i = 0; i < 101; i++) coarse.Update(0.05f, Idle);

            var fine = Runner(map, timeLimit: 600f);
            for (int i = 0; i < 505; i++) fine.Update(0.01f, Idle);

            Assert.Equal(50, coarse.TicksElapsed);
            Assert.Equal(coarse.TicksElapsed, fine.TicksElapsed);
            Assert.Equal(coarse.Grid.Count, fine.Grid.Count);
            for (int i = 0; i < coarse.Grid.Count; i++)
            {
                Assert.Equal(coarse.Grid.Cells[i].State, fine.Grid.Cells[i].State);
                Assert.Equal(coarse.Grid.Cells[i].Heat, fine.Grid.Cells[i].Heat, 4);
            }
        }

        // --- TC-12 ---
        [Fact]
        public void FireInput_AppliesTheEquippedAgentToTheAimedCell()
        {
            var runner = Runner(new[]
            {
                "#####",
                "#@*.#",
                "#####",
            });

            // 동쪽을 바라보게 한 뒤 열이 쌓이길 기다린다.
            runner.Update(0.05f, new StageInput { MoveX = 1f });
            for (int i = 0; i < 20; i++) runner.Update(0.1f, Idle);

            Assert.Equal(AimDirection.E, runner.Player.Aim);
            float heatBefore = runner.Grid[2, 1].Heat;
            Assert.True(heatBefore > 0f);

            runner.Update(0.01f, new StageInput { Fire = true, Slot = 0 });

            Assert.True(runner.Grid[2, 1].Heat < heatBefore, "조준한 셀의 열이 줄어야 한다");
            Assert.True(runner.Grid[2, 1].Wet > 0f, "물을 맞았으니 젖어야 한다");
        }

        // --- TC-13 ---
        [Fact]
        public void FireInput_DoesNothing_WhenTheEquipmentHasNoChargesLeft()
        {
            var runner = new StageRunner(
                Stage(new[] { "#####", "#@*.#", "#####" }),
                new List<int> { EquipmentId.Extinguisher });

            // 충전량을 모두 소진시킨다.
            runner.Player.Charges[0] = 0;

            for (int i = 0; i < 20; i++) runner.Update(0.1f, Idle);

            float heatBefore = runner.Grid[2, 1].Heat;
            float wetBefore = runner.Grid[2, 1].Wet;

            runner.Update(0.001f, new StageInput { Fire = true, Slot = 0 });

            Assert.Equal(heatBefore, runner.Grid[2, 1].Heat, 4);
            Assert.Equal(wetBefore, runner.Grid[2, 1].Wet, 4);
        }

        // --- TC-14 ---
        [Fact]
        public void UpdatesAfterTheStageEnds_ChangeNothing()
        {
            var runner = Runner(new[]
            {
                "#####",
                "#@.X#",
                "##*##",
                "#####",
            }, timeLimit: 1f);

            for (int i = 0; i < 20 && !runner.IsOver; i++) runner.Update(0.1f, Idle);
            Assert.True(runner.IsOver);

            Cell[] before = (Cell[])runner.Grid.Cells.Clone();
            float timeBefore = runner.TimeLeft;

            for (int i = 0; i < 20; i++) runner.Update(0.1f, new StageInput { MoveX = 1f, Fire = true });

            Assert.Equal(timeBefore, runner.TimeLeft);
            for (int i = 0; i < before.Length; i++)
            {
                Assert.Equal(before[i].State, runner.Grid.Cells[i].State);
            }
        }

        // --- TC-15 ---
        [Fact]
        public void StageResult_ReportsTheActualTallies()
        {
            var runner = Runner(new[]
            {
                "#######",
                "#@..!X#",
                "#######",
            }, timeLimit: 30f);

            for (int i = 0; i < 100 && runner.RescuedCount == 0; i++)
            {
                runner.Update(0.05f, new StageInput { MoveX = 1f });
            }

            // 바닥 한 칸을 적셔 수손 집계가 반영되는지 본다.
            Suppression.Apply(runner.Grid, 2, 1, EquipmentCatalog.Bucket.Agent);

            StageResult result = runner.BuildResult();

            Assert.Equal(99, result.StageId);
            Assert.Equal(300, result.BasePayout);
            Assert.Equal(1, result.Rescued);
            Assert.Equal(1, result.CiviliansTotal);
            Assert.Equal(runner.Grid.IntactRatio(), result.IntactRatio, 5);
            Assert.Equal(runner.TimeLeft, result.TimeLeft, 5);
            Assert.Equal(runner.Grid.CountWet(), result.WetCellCount);
            Assert.True(result.WetCellCount > 0);
        }

        // --- TC-16 ---
        [Fact]
        public void StageWithNoCivilians_IsWonByPuttingOutTheFireAlone()
        {
            var runner = Runner(new[]
            {
                "#####",
                "#@.##",
                "##*##",
                "#####",
            });

            Assert.Empty(runner.Civilians);

            ExtinguishEverything(runner.Grid);
            runner.Update(0.1f, Idle);

            Assert.Equal(StageOutcome.Won, runner.Outcome);
        }
    }
}
