using FireGame.Core.Grid;
using FireGame.Prototypes.Logic;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 레벨마다 ★3 수순. 풀이기(설계 도구)가 찾은 것을 그대로 적었다.
    /// 수: "m x y" 이동, "s N|E|S|W" 분사, "d x y" 문, "r" 급수. 턴은 '|'로 나눈다.
    /// </summary>
    public static class TacticsSolutions
    {
        public static readonly string[] All =
        {
            "m 2 3; m 4 3 | s N; m 5 2 | m 6 2; m 6 1 | m 7 1; m 8 1",
            "m 5 4; m 5 2 | m 5 4; m 3 4 | s W; m 1 4 | s N; m 1 2 | m 1 4; s E | m 3 4; s E | m 4 4; m 5 4 | m 5 3; m 5 2 | m 5 1; m 6 1",
            "m 5 4; m 5 2 | m 5 4; m 3 4 | s W; m 1 4 | s N; m 1 2 | m 1 4; s E | m 1 3; m 1 2 | m 1 1; m 2 1 | m 2 2; m 2 1 | m 2 2; m 2 1",
        };

        public static bool Apply(TacticsGame g, string token)
        {
            string[] t = token.Trim().Split(' ');
            switch (t[0])
            {
                case "m": return g.Do(TAction.Move, new GridPoint(int.Parse(t[1]), int.Parse(t[2])));
                case "s": return g.Do(TAction.Spray, Dirs.Step(g.Player, (Dir)"NESW".IndexOf(t[1])));
                case "d": return g.Do(TAction.ToggleDoor, new GridPoint(int.Parse(t[1]), int.Parse(t[2])));
                case "r": return g.Do(TAction.Refill, default);
            }
            return false;
        }
    }
}
