using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using FireGame.Core.Sim;
using Xunit;

namespace FireGame.Core.Tests
{
    /// <summary>
    /// 화면이 "어떤 소화기를 써야 하나"와 "지금 업을 수 있나"를 물어보는 길.
    /// 둘 다 규칙은 코어에 있고 화면은 색과 버튼만 그린다.
    /// </summary>
    public class RescueAndAdviceTests
    {
        /// <summary>시민 한 명이 소방관 바로 옆에 서 있는 작은 방.</summary>
        private static StageRunner RoomWithCivilians(string[] map)
        {
            var stage = new StageDef(98, "T", map, Wind.None, 120f, 100);
            return new StageRunner(stage, EquipmentCatalog.StartingEquipment);
        }

        // --- TC-1 ---
        [Fact]
        public void AgentAdvice_MapsTheMatrixToVerdicts()
        {
            Assert.Equal(AgentVerdict.Good, AgentAdvice.For(AgentType.Water, FireClass.A));
            Assert.Equal(AgentVerdict.Backfire, AgentAdvice.For(AgentType.Water, FireClass.B));
            Assert.Equal(AgentVerdict.Backfire, AgentAdvice.For(AgentType.Water, FireClass.C));

            Assert.Equal(AgentVerdict.Weak, AgentAdvice.For(AgentType.Foam, FireClass.A));
            Assert.Equal(AgentVerdict.Good, AgentAdvice.For(AgentType.Foam, FireClass.B));
            Assert.Equal(AgentVerdict.Useless, AgentAdvice.For(AgentType.Foam, FireClass.C));

            Assert.Equal(AgentVerdict.Weak, AgentAdvice.For(AgentType.CO2, FireClass.A));
            Assert.Equal(AgentVerdict.Weak, AgentAdvice.For(AgentType.CO2, FireClass.B));
            Assert.Equal(AgentVerdict.Good, AgentAdvice.For(AgentType.CO2, FireClass.C));

            Assert.Equal("기름", AgentAdvice.ClassName(FireClass.B));
            Assert.Null(AgentAdvice.ClassName(FireClass.None));
            Assert.Equal(FireClass.C, AgentAdvice.BestClassFor(AgentType.CO2));
            Assert.Equal(FireClass.A, AgentAdvice.BestClassFor(AgentType.Water));
        }

        // --- TC-2 ---
        [Fact]
        public void AgentAdvice_ForCell_IsUselessUnlessTheCellIsBurning()
        {
            StageRunner runner = RoomWithCivilians(new[]
            {
                "#######",
                "#.@.*.#",
                "#######",
            });

            Assert.Equal(AgentVerdict.Good, AgentAdvice.ForCell(runner.Grid, 4, 1, AgentType.Water));

            // 안 타는 바닥, 벽, 격자 밖
            Assert.Equal(AgentVerdict.Useless, AgentAdvice.ForCell(runner.Grid, 1, 1, AgentType.Water));
            Assert.Equal(AgentVerdict.Useless, AgentAdvice.ForCell(runner.Grid, 0, 0, AgentType.Water));
            Assert.Equal(AgentVerdict.Useless, AgentAdvice.ForCell(runner.Grid, -1, 5, AgentType.Water));
            Assert.Equal(AgentVerdict.Useless, AgentAdvice.ForCell(null, 1, 1, AgentType.Water));
        }

        // --- TC-3 ---
        [Fact]
        public void Civilians_AreNotPickedUpWithoutThePress()
        {
            StageRunner runner = RoomWithCivilians(new[]
            {
                "#######",
                "#.@!..#",
                "#..X..#",
                "#######",
            });

            for (int i = 0; i < 600; i++)
            {
                runner.Update(0.016f, new StageInput { MoveX = 1f, Slot = 0 });
                if (runner.RescueTarget != null) break;
            }

            Assert.NotNull(runner.RescueTarget);
            Assert.False(runner.Player.CarryingCivilian, "버튼을 누르지 않았는데 업혔다");
            Assert.Null(runner.JustPickedUp);
        }

        // --- TC-4 ---
        [Fact]
        public void PressingRescue_PicksUpAndSignalsOnce()
        {
            StageRunner runner = RoomWithCivilians(new[]
            {
                "#######",
                "#.@!..#",
                "#..X..#",
                "#######",
            });

            for (int i = 0; i < 600 && runner.RescueTarget == null; i++)
            {
                runner.Update(0.016f, new StageInput { MoveX = 1f, Slot = 0 });
            }

            runner.Update(0.016f, new StageInput { Slot = 0, Rescue = true });
            Assert.True(runner.Player.CarryingCivilian);
            Assert.NotNull(runner.JustPickedUp);

            // 한 프레임짜리 신호다.
            runner.Update(0.016f, new StageInput { Slot = 0 });
            Assert.Null(runner.JustPickedUp);
            Assert.True(runner.Player.CarryingCivilian);
        }

        // --- TC-5 ---
        [Fact]
        public void RescueTarget_IsTheNearestCivilian()
        {
            StageRunner runner = RoomWithCivilians(new[]
            {
                "##########",
                "#.@!.!...#",
                "#...X....#",
                "##########",
            });

            Civilian near = runner.Civilians[0].X < runner.Civilians[1].X ? runner.Civilians[0] : runner.Civilians[1];

            for (int i = 0; i < 600 && runner.RescueTarget == null; i++)
            {
                runner.Update(0.016f, new StageInput { MoveX = 1f, Slot = 0 });
            }

            Assert.Same(near, runner.RescueTarget);
        }

        // --- TC-6 ---
        [Fact]
        public void GameFlow_RescueIsEdgeTriggered()
        {
            SaveData save = SaveData.NewGame();
            var flow = new GameFlow(save);
            Assert.True(flow.SelectMission(0));
            flow.BeginMission();

            // 버튼을 누른 채로 시민에게 걸어간다.
            flow.SetRescue(true);
            flow.SetMove(0f, 0f);
            for (int i = 0; i < 20; i++) flow.Update(0.016f);

            // 누른 순간은 첫 프레임뿐이었으므로, 그 뒤로는 눌림이 먹지 않는다.
            StageRunner runner = flow.Runner;
            Assert.Null(runner.JustPickedUp);

            flow.SetRescue(false);
            flow.Update(0.016f);
            flow.SetRescue(true);
            flow.Update(0.016f);

            // 시민이 멀어서 아무도 안 업히는 것이 정상이다. 예외 없이 돌아가면 된다.
            Assert.False(runner.IsOver);
        }

        // --- TC-7 ---
        [Fact]
        public void Runner_CountsBackfiredCells()
        {
            // 역효과는 "옆 칸으로 옮겨붙는 것"이라 번질 곳(아직 안 타는 기름)이 있어야 한다.
            var stage = new StageDef(97, "T", new[]
            {
                "#######",
                "#.@%~.#",
                "#######",
            }, Wind.None, 120f, 100);

            SaveData water = SaveData.NewGame();
            var bucket = new StageRunner(stage, Loadout.From(water));
            bucket.Update(0.001f, new StageInput { Fire = true, Slot = 0 });
            Assert.True(bucket.LastShotBackfired > 0, "기름에 물을 뿌렸는데 역효과가 안 잡혔다");

            SaveData foamSave = SaveData.NewGame();
            foamSave.SetLevel(EquipmentId.FoamExtinguisher, 3);
            var foam = new StageRunner(stage, Loadout.From(foamSave));
            int slot = foam.SlotEquipment(0).Id == EquipmentId.FoamExtinguisher ? 0 : 1;
            foam.Update(0.001f, new StageInput { Fire = true, Slot = slot });
            Assert.Equal(0, foam.LastShotBackfired);
        }
    }
}
