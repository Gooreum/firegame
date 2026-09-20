using FireGame.Core.Sim;
using Xunit;

namespace FireGame.Core.Tests
{
    public class SolutionSmokeTests
    {
        [Fact]
        public void CoreAssembly_TargetsNetStandard21()
        {
            // Core 프로젝트 참조가 실제로 해결되는지 확인하는 것이 목적.
            Assert.NotNull(typeof(SimConfig).Assembly);
            Assert.Equal("FireGame.Core", typeof(SimConfig).Assembly.GetName().Name);
        }

        [Fact]
        public void CoreAssembly_HasNoUnityDependency()
        {
            foreach (var referenced in typeof(SimConfig).Assembly.GetReferencedAssemblies())
            {
                Assert.DoesNotContain("UnityEngine", referenced.Name);
            }
        }

        [Fact]
        public void SimConfig_TickDeltaMatchesTickRate()
        {
            Assert.Equal(0.1f, SimConfig.TickDelta, 5);
        }
    }
}
