using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>4스테이지 항구: 맵, 바다를 떠내려오는 불배, 불 갈매기, 소방정·큰 파도.</summary>
    public class HarborTests
    {
        [Fact]
        public void HarborMap_HasEightBuildings_AColdStore_FuelTanks_AndASeaWithTwoPiers()
        {
            var sim = new SurvivorSim(1, 4);
            Assert.Equal("항구", sim.Stage.Name);
            Assert.Equal(4, sim.Stage.Number);
            Assert.Equal(8, sim.Structures.FindAll(s => s.Kind == StructureKind.House).Count);
            Structure depot = Assert.Single(sim.Structures.FindAll(s => s.Kind == StructureKind.Depot));
            Assert.Equal("수산 냉동창고", depot.Name);
            Assert.Equal(3, sim.Structures.FindAll(s => s.Kind == StructureKind.Gas).Count);
            Assert.Equal(5, sim.Structures.FindAll(s => s.Kind == StructureKind.Water).Count);
            Assert.True(sim.HasWater);
            Assert.All(sim.Structures, s => Assert.True(s.DistanceTo(sim.Player) > 6f, s.Name + "가 출발점에 너무 가깝다"));
            Assert.All(sim.Structures, s => Assert.False(s.Burning));
            int residents = 0;
            foreach (Structure s in sim.Structures) residents += s.Residents;
            Assert.Equal(18, residents);

            // 바다는 SeaFrom 북쪽에만 있고, 부두 자리(y < PierTip)는 비어 있어 걸을 수 있다.
            Assert.All(sim.Structures.FindAll(s => s.Kind == StructureKind.Water), w => Assert.True(w.Pos.Y - w.Half.Y >= SurvivorHarbor.SeaFrom - 0.01f));
            foreach (float px in SurvivorHarbor.PierX)
            {
                var onPier = new Vec2(px, SurvivorHarbor.PierTip - 1f);
                Assert.DoesNotContain(sim.Structures, s => s.Kind == StructureKind.Water && s.Within(onPier, 0f));
                var beyond = new Vec2(px, SurvivorHarbor.PierTip + 1f);
                Assert.Contains(sim.Structures, s => s.Kind == StructureKind.Water && s.Within(beyond, 0f));
            }
            // 부둣가 줄 지붕 윗변과 부두선 사이 틈은 배가 불을 옮기는 거리 안이다.
            Assert.All(sim.Structures.FindAll(s => s.Kind == StructureKind.House && s.Pos.Y > 32f), h => Assert.InRange(SurvivorHarbor.SeaFrom - (h.Pos.Y + h.Half.Y), 1f, 4f));
            // 아직 배는 없다(TickBoats가 띄운다).
            Assert.Empty(sim.Structures.FindAll(s => s.Kind == StructureKind.Boat));
        }
    }
}
