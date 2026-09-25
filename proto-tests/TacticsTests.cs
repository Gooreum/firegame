using System.Collections.Generic;
using FireGame.Core.Grid;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>시험판 B 규칙. 미리보기가 거짓말하지 않고, 되돌리기가 정확하고, 레벨이 긴장 있고 풀린다.</summary>
    public class TacticsTests
    {
        private static TacticsGame Custom(Dir? wind, params string[] rows)
        {
            return TacticsGame.Load(new TacticsLevel("t", "", 20, new[] { wind }, rows));
        }

        private static HashSet<GridPoint> Burning(TacticsGame g)
        {
            var set = new HashSet<GridPoint>();
            for (int y = 0; y < g.Height; y++)
                for (int x = 0; x < g.Width; x++)
                    if (g.Burning(x, y)) set.Add(new GridPoint(x, y));
            return set;
        }

        public static IEnumerable<object[]> LevelIndexes()
        {
            for (int i = 0; i < TacticsLevels.All.Length; i++) yield return new object[] { i };
        }

        // --- TC-1 ---
        [Theory]
        [MemberData(nameof(LevelIndexes))]
        public void Preview_IsExactlyWhatHappens(int index)
        {
            TacticsGame g = TacticsGame.Load(TacticsLevels.All[index]);
            for (int turn = 0; turn < 4 && g.Outcome == TOutcome.Playing; turn++)
            {
                HashSet<GridPoint> before = Burning(g);
                var expected = new HashSet<GridPoint>();
                foreach (Spread s in g.PreviewSpread())
                {
                    if (g.Tile(s.To.X, s.To.Y) != TTile.Gas) expected.Add(s.To);
                }
                foreach (GridPoint p in g.PreviewExplosions())
                {
                    TTile t = g.Tile(p.X, p.Y);
                    if ((t == TTile.Floor || (t == TTile.Door && !g.DoorClosed(p.X, p.Y))) && !g.Burning(p.X, p.Y) && !g.Burnt(p.X, p.Y)) expected.Add(p);
                }

                g.EndTurn();

                var lit = new HashSet<GridPoint>();
                foreach (GridPoint p in Burning(g))
                {
                    if (!before.Contains(p)) lit.Add(p);
                }
                Assert.True(expected.SetEquals(lit), TacticsLevels.All[index].Name + " 턴 " + turn + ": 미리보기와 실제 확산이 다르다\n" + g.Draw());
            }
        }

        // --- TC-2 ---
        [Fact]
        public void Spraying_StopsTheSpread_AndWetCellsHoldForTwoTurns()
        {
            TacticsGame g = Custom(Dir.E,
                "#######",
                "#P*...#",
                "#######");

            Assert.Contains(g.PreviewSpread(), s => s.To.Equals(new GridPoint(3, 1)));
            List<GridPoint> blocked = g.SprayBlocks(Dir.E);
            Assert.Contains(new GridPoint(3, 1), blocked);

            Assert.True(g.Do(TAction.Spray, new GridPoint(2, 1)));
            Assert.False(g.Burning(2, 1));
            Assert.Equal(TacticsGame.WetTurns, g.Wet(3, 1));
            Assert.Empty(g.PreviewSpread());

            g.EndTurn();
            g.EndTurn();
            Assert.Empty(Burning(g));
        }

        // --- TC-3 ---
        [Fact]
        public void ClosedDoor_StopsTheFire()
        {
            TacticsGame g = Custom(Dir.E,
                "#######",
                "#P.*D.#",
                "#######");
            Assert.Contains(g.PreviewSpread(), s => s.To.Equals(new GridPoint(4, 1)));

            Assert.True(g.Do(TAction.Move, new GridPoint(2, 1)));
            Assert.False(g.Do(TAction.ToggleDoor, new GridPoint(4, 1)), "문 바로 옆이 아닌데 닫혔다");

            TacticsGame h = Custom(Dir.E,
                "#######",
                "#..*DP#",
                "#######");
            Assert.True(h.Do(TAction.ToggleDoor, new GridPoint(4, 1)));
            Assert.Empty(h.PreviewSpread());
            h.EndTurn();
            h.EndTurn();
            Assert.False(h.Burning(4, 1));
            Assert.False(h.Burning(5, 1));
        }

        // --- TC-4 ---
        [Theory]
        [MemberData(nameof(LevelIndexes))]
        public void UndoTurn_RestoresTheTurnStartExactly(int index)
        {
            TacticsGame g = TacticsGame.Load(TacticsLevels.All[index]);
            g.EndTurn();
            string start = g.Signature();

            foreach (KeyValuePair<GridPoint, int> kv in g.Reachable())
            {
                if (g.Do(TAction.Move, kv.Key)) break;
            }
            foreach (Dir d in Dirs.All)
            {
                if (g.Do(TAction.Spray, Dirs.Step(g.Player, d))) break;
            }
            Assert.NotEqual(start, g.Signature());

            g.UndoTurn();
            Assert.Equal(start, g.Signature());
        }

        // --- TC-5 ---
        [Fact]
        public void ImpossibleActions_AreRefused_AndChangeNothing()
        {
            TacticsGame g = Custom(null,
                "##########",
                "#P......H#",
                "##########");
            string start = g.Signature();

            Assert.False(g.CanDo(TAction.Refill, default, out string why));
            Assert.False(string.IsNullOrEmpty(why));
            Assert.False(g.Do(TAction.Move, new GridPoint(7, 1)), "3칸 넘게 움직였다");
            Assert.False(g.Do(TAction.Spray, new GridPoint(3, 3)), "대각선으로 쐈다");
            Assert.Equal(start, g.Signature());

            // 물 4번을 다 쓰면 다섯 번째는 거부된다(턴을 넘겨 행동을 채운다).
            for (int i = 0; i < TacticsGame.TankMax; i++)
            {
                Assert.True(g.Do(TAction.Spray, new GridPoint(2, 1)));
                if (g.Actions == 0) g.EndTurn();
            }
            Assert.Equal(0, g.Tank);
            Assert.False(g.CanDo(TAction.Spray, new GridPoint(2, 1), out why));

            // 행동을 다 쓰면 이동도 거부된다.
            Assert.True(g.Do(TAction.Move, new GridPoint(3, 1)));
            Assert.True(g.Do(TAction.Move, new GridPoint(5, 1)));
            Assert.Equal(0, g.Actions);
            string spent = g.Signature();
            Assert.False(g.Do(TAction.Move, new GridPoint(6, 1)));
            Assert.Equal(spent, g.Signature());

            // 소화전 옆이면 급수된다.
            g.EndTurn();
            Assert.True(g.Do(TAction.Move, new GridPoint(7, 1)));
            Assert.True(g.Do(TAction.Refill, default));
            Assert.Equal(TacticsGame.TankMax, g.Tank);
        }

        // --- TC-6 ---
        [Fact]
        public void GasCan_IsAnnounced_ThenExplodesNextTurn()
        {
            TacticsGame g = Custom(Dir.E,
                "#########",
                "#P.*G...#",
                "#.......#",
                "#########");

            Assert.Contains(g.PreviewSpread(), s => s.To.Equals(new GridPoint(4, 1)));
            g.EndTurn();
            Assert.True(g.GasArmed(4, 1));

            List<GridPoint> blast = g.PreviewExplosions();
            Assert.Contains(new GridPoint(5, 2), blast);
            Assert.Contains(new GridPoint(3, 2), blast);

            g.EndTurn();
            Assert.False(g.GasArmed(4, 1));
            Assert.True(g.Burnt(4, 1));
            Assert.True(g.Burning(5, 2));
            Assert.True(g.Burning(3, 2));
        }

        // --- TC-6b ---
        [Fact]
        public void SprayingAnArmedGasCan_Defuses()
        {
            TacticsGame g = Custom(Dir.E,
                "#########",
                "#..*G..P#",
                "#.......#",
                "#########");
            g.EndTurn();
            Assert.True(g.GasArmed(4, 1));

            Assert.True(g.Do(TAction.Spray, new GridPoint(6, 1)));
            Assert.False(g.GasArmed(4, 1));
            Assert.Empty(g.PreviewExplosions());
        }

        // --- TC-7 ---
        [Theory]
        [MemberData(nameof(LevelIndexes))]
        public void DoingNothing_Loses(int index)
        {
            TacticsGame g = TacticsGame.Load(TacticsLevels.All[index]);
            for (int i = 0; i < 50 && g.Outcome == TOutcome.Playing; i++) g.EndTurn();
            Assert.Equal(TOutcome.Lost, g.Outcome);
        }

        // --- TC-8 ---
        [Theory]
        [MemberData(nameof(LevelIndexes))]
        public void WrittenSolution_WinsThreeStars(int index)
        {
            TacticsGame g = TacticsGame.Load(TacticsLevels.All[index]);
            string[] turns = TacticsSolutions.All[index].Split('|');

            foreach (string turn in turns)
            {
                foreach (string token in turn.Split(';', System.StringSplitOptions.RemoveEmptyEntries))
                {
                    Assert.True(TacticsSolutions.Apply(g, token), TacticsLevels.All[index].Name + ": 수 '" + token + "' 실패\n" + g.Draw());
                }
                if (g.Outcome != TOutcome.Playing) break;
                g.EndTurn();
            }

            Assert.Equal(TOutcome.Won, g.Outcome);
            Assert.Equal(3, g.Stars);
        }

        // --- TC-9 ---
        [Fact]
        public void StandingNextToSomeone_SendsThemWalkingToTheExit()
        {
            TacticsGame g = Custom(null,
                "#########",
                "#P.C...X#",
                "#########");

            Assert.True(g.Do(TAction.Move, new GridPoint(2, 1)));
            Assert.Equal(CivState.Evacuating, g.CivilianStates[0]);

            // 다음 턴에 걸을 길이 미리 보인다(2칸).
            List<GridPoint>[] walks = g.PreviewWalks();
            Assert.Equal(new[] { new GridPoint(4, 1), new GridPoint(5, 1) }, walks[0]);

            g.EndTurn();
            Assert.Equal(new GridPoint(5, 1), g.CivilianPositions[0]);
            g.EndTurn();

            Assert.Equal(1, g.Rescued);
            Assert.Equal(TOutcome.Won, g.Outcome);
            Assert.Equal(3, g.Stars);
        }

        // --- TC-9b ---
        [Fact]
        public void Evacuees_WaitWhenFireBlocksTheWay_AndWalkOnceItIsOut()
        {
            TacticsGame g = Custom(null,
                "#########",
                "#P.C*..X#",
                "#########");

            Assert.True(g.Do(TAction.Move, new GridPoint(2, 1)));
            Assert.Equal(CivState.Evacuating, g.CivilianStates[0]);
            Assert.Null(g.PreviewWalks()[0]);

            Assert.True(g.Do(TAction.Spray, new GridPoint(3, 1)));
            Assert.NotNull(g.PreviewWalks()[0]);
        }
    }
}
