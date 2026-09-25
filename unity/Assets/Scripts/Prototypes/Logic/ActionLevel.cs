namespace FireGame.Prototypes.Logic
{
    /// <summary>
    /// 시험판 A의 건물. 글자: # 벽, . 바닥(탐), D 문(탐, 방 경계), * 불, P 소방관, C 사람, X 출구, H 소화전.
    /// </summary>
    public sealed class ActionLevel
    {
        public readonly string Name;
        public readonly string[] Rows;

        public ActionLevel(string name, params string[] rows)
        {
            Name = name;
            Rows = rows;
        }

        public static readonly ActionLevel Building = new ActionLevel(
            "3층 사무실",
            "########################",
            "#......#.......#.......#",
            "#..C...#.......#...C...#",
            "#......D.......#.......#",
            "#.***..#.......D..***..#",
            "#.***..#.......#..***..#",
            "##D#####.......####D####",
            "#.....................H#",
            "#....................P.X",
            "#......................#",
            "####D######D#######D####",
            "#.......#.***...#......#",
            "#.......#.***...#......#",
            "#.......D.......#......#",
            "#.......#....C..D......#",
            "########################");
    }
}
