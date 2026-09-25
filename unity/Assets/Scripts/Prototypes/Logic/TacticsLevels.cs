namespace FireGame.Prototypes.Logic
{
    /// <summary>
    /// 시험판 B의 한 판.
    /// 맵 글자: # 벽, . 나무 바닥(탐), , 콘크리트(안 탐), * 불, P 소방관, C 사람, X 출구,
    /// H 소화전, G 가스통, D 열린 문, d 닫힌 문.
    /// </summary>
    public sealed class TacticsLevel
    {
        public readonly string Name;
        public readonly string Hint;
        public readonly string[] Rows;
        public readonly int TurnLimit;

        /// <summary>턴별 바람. 배열보다 긴 턴은 마지막 값을 쓴다. null이면 무풍.</summary>
        private readonly Dir?[] _wind;

        public TacticsLevel(string name, string hint, int turnLimit, Dir?[] wind, params string[] rows)
        {
            Name = name;
            Hint = hint;
            TurnLimit = turnLimit;
            _wind = wind;
            Rows = rows;
        }

        public Dir? WindAt(int turn)
        {
            if (_wind == null || _wind.Length == 0) return null;
            int i = turn - 1;
            if (i < 0) i = 0;
            if (i >= _wind.Length) i = _wind.Length - 1;
            return _wind[i];
        }
    }

    public static class TacticsLevels
    {
        /// <summary>
        /// 레벨마다 풀이기로 확인한 것: ★3 풀이가 있다, 사람에게 곧장 달려가는 수로는 ★3이 안 된다(2·3),
        /// 물·문 없이는 ★3이 안 된다, 턴 제한을 하나 줄이면 ★3이 안 된다(2·3).
        /// </summary>
        public static readonly TacticsLevel[] All =
        {
            new TacticsLevel(
                "1. 길을 연다",
                "문 앞에 불이 있다. 그대로 두면 사람 쪽으로 번진다 — 물로 길을 열고 곁에 서면 사람이 스스로 나간다.",
                5,
                new Dir?[] { Dir.E },
                "##########",
                "#....#...#",
                "#P.*.D.C.#",
                "#....#...#",
                "#H...#...X",
                "##########"),

            new TacticsLevel(
                "2. 누구부터",
                "복도 끝 불이 출구 쪽으로 밀려온다. 한 사람은 지금 길이 열려 있고, 다른 사람은 불 너머에 있다.",
                9,
                new Dir?[] { Dir.E },
                "##########",
                "#C.#.C...#",
                "#..#.....#",
                "#D###D####",
                "#*..P....X",
                "#H.......#",
                "##########"),

            new TacticsLevel(
                "3. 가스통과 바람",
                "4턴째에 바람이 남쪽으로 바뀐다. 출구 앞 가스통에 불이 닿으면 다음 턴에 터진다.",
                9,
                new Dir?[] { Dir.E, Dir.E, Dir.E, Dir.S },
                "##########",
                "#C.#.C...#",
                "#..#.....#",
                "#D###D####",
                "#*..P....X",
                "#H.....G.#",
                "##########"),
        };
    }
}
