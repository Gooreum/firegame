using FireGame.Core.Grid;

namespace FireGame.Prototypes.Logic
{
    /// <summary>격자 네 방향. 격자는 아래로 갈수록 y가 커진다(N이 -y).</summary>
    public enum Dir : byte
    {
        N = 0,
        E = 1,
        S = 2,
        W = 3,
    }

    public static class Dirs
    {
        public static readonly Dir[] All = { Dir.N, Dir.E, Dir.S, Dir.W };

        public static int Dx(Dir d)
        {
            return d == Dir.E ? 1 : d == Dir.W ? -1 : 0;
        }

        public static int Dy(Dir d)
        {
            return d == Dir.S ? 1 : d == Dir.N ? -1 : 0;
        }

        public static GridPoint Step(GridPoint p, Dir d, int n = 1)
        {
            return new GridPoint(p.X + (Dx(d) * n), p.Y + (Dy(d) * n));
        }

        public static Dir Opposite(Dir d)
        {
            return (Dir)(((int)d + 2) % 4);
        }
    }
}
