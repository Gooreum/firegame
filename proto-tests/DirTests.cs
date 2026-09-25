using FireGame.Core.Grid;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    public class DirTests
    {
        [Fact]
        public void Step_MovesOneCell_AndOppositeComesBack()
        {
            var p = new GridPoint(3, 3);
            foreach (Dir d in Dirs.All)
            {
                GridPoint moved = Dirs.Step(p, d);
                Assert.Equal(1, System.Math.Abs(moved.X - p.X) + System.Math.Abs(moved.Y - p.Y));
                Assert.Equal(p, Dirs.Step(moved, Dirs.Opposite(d)));
            }
            Assert.Equal(new GridPoint(3, 2), Dirs.Step(p, Dir.N));
        }
    }
}
