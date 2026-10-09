using Xunit;
using Xunit.Abstractions;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 숲만 빠르게 재는 표(2026-10-10, 건물 적시기 패스): 숙련 봇(CloseRow)과 기본 봇(FunRow)을 숲(2)에서만 돌린다.
    /// 손잡이 하나를 바꿀 때마다 다섯 스테이지를 다 돌리지 않으려고. 밴드 판정은 CloseReport·FunReport·BalanceReport가 한다.
    /// dotnet test proto-tests --filter ForestReport --logger "console;verbosity=detailed"
    /// </summary>
    public class ForestBalanceTests
    {
        private readonly ITestOutputHelper _out;

        public ForestBalanceTests(ITestOutputHelper output)
        {
            _out = output;
        }

        [Fact]
        public void ForestReport_ProAndBasicBot()
        {
            int seeds = FunTests.Seeds;
            _out.WriteLine("숙련 " + CloseReportTests.Measure(2, seeds));
            _out.WriteLine("기본 " + FunTests.Measure(2, seeds));
        }
    }
}
