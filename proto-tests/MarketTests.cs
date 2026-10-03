using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>5스테이지 야시장: 맵, 등줄을 타는 불, 불꽃 가판대 로켓, 폭죽, 물 불꽃놀이·물안개.</summary>
    public class MarketTests
    {
        [Fact]
        public void MarketMap_HasTwelveStalls_AStage_ThreeFireworkStands_AndTwelveLanternLinks()
        {
            var sim = new SurvivorSim(1, 5);
            Assert.Equal("야시장", sim.Stage.Name);
            Assert.Equal(5, sim.Stage.Number);
            List<Structure> stalls = sim.Structures.FindAll(s => s.Kind == StructureKind.House);
            Assert.Equal(12, stalls.Count);
            Assert.All(stalls, s => Assert.Equal(SurvivorMarket.StallHalf, s.Half));
            Structure depot = Assert.Single(sim.Structures.FindAll(s => s.Kind == StructureKind.Depot));
            Assert.Equal("공연 무대", depot.Name);
            Assert.Equal(3, sim.Structures.FindAll(s => s.Kind == StructureKind.Fireworks).Count);
            Assert.Equal(3, sim.Structures.FindAll(s => s.Kind == StructureKind.Gas).Count);
            Assert.False(sim.HasWater);
            Assert.All(sim.Structures, s => Assert.True(s.DistanceTo(sim.Player) > 6f, s.Name + "가 출발점에 너무 가깝다"));
            Assert.All(sim.Structures, s => Assert.False(s.Burning));
            int residents = 0;
            foreach (Structure s in stalls) residents += s.Residents;
            Assert.Equal(18, residents);

            // 등줄 12쌍: 모두 점포 인덱스이고, 같은 줄 이웃(x 차이 7, 가운데 광장을 건너는 한 줄은 16)이거나 줄 양 끝의 위아래(y 차이 16)다.
            List<int[]> links = sim.Stage.Links(sim.Structures);
            Assert.Equal(12, links.Count);
            foreach (int[] ab in links)
            {
                Structure a = sim.Structures[ab[0]];
                Structure b = sim.Structures[ab[1]];
                Assert.Equal(StructureKind.House, a.Kind);
                Assert.Equal(StructureKind.House, b.Kind);
                bool neighbour = a.Pos.Y == b.Pos.Y && System.Math.Abs(a.Pos.X - b.Pos.X) <= 16.01f
                    && !stalls.Exists(s => s.Pos.Y == a.Pos.Y && s != a && s != b && (s.Pos.X - a.Pos.X) * (s.Pos.X - b.Pos.X) < 0f);
                bool endPost = a.Pos.X == b.Pos.X && System.Math.Abs(a.Pos.Y - b.Pos.Y) == SurvivorMarket.RowNorth - SurvivorMarket.RowSouth;
                Assert.True(neighbour || endPost, a.Name + "-" + b.Name + " 등줄이 이웃이 아니다");
            }
        }
    }
}
