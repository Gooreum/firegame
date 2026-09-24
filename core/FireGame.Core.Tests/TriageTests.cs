using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using Xunit;

namespace FireGame.Core.Tests
{
    /// <summary>
    /// 철수. "전부 끄기" 하나였던 승리가 <b>"무엇을 버릴까"</b>로 갈리는 지점이다.
    ///
    /// 자동이 아니라 선언이어야 한다. 마지막 한 명을 내려놓는 순간 저절로 이기면
    /// 불을 끌 이유가 통째로 사라지고, 30초 만에 끝나는 게임이 된다.
    /// </summary>
    public class TriageTests
    {
        private static StageRunner Runner()
        {
            return new StageRunner(StageCatalog.Residential, new[] { EquipmentId.Bucket });
        }

        /// <summary>시민을 전부 구조한 것으로 처리하고 소방관을 출구에 세운다.</summary>
        private static StageRunner AtExitWithEveryoneOut()
        {
            StageRunner runner = Runner();

            foreach (Civilian civilian in runner.Civilians)
            {
                civilian.Rescued = true;
            }

            GridPoint exit = runner.Exits[0];
            runner.Player.X = exit.X + 0.5f;
            runner.Player.Y = exit.Y + 0.5f;
            return runner;
        }

        // --- TC-1 ---
        [Fact]
        public void RescuingEveryone_DoesNotWinOnItsOwn()
        {
            StageRunner runner = Runner();
            foreach (Civilian civilian in runner.Civilians) civilian.Rescued = true;

            runner.Update(0.05f, default);

            // 불이 아직 타고 있고 철수를 선언하지도 않았다.
            Assert.True(runner.Grid.CountBurning() > 0, "이 검사는 불이 남아 있어야 의미가 있다");
            Assert.Equal(StageOutcome.InProgress, runner.Outcome);
        }

        // --- TC-2 ---
        [Fact]
        public void WithdrawingAtTheExit_WinsWithFireStillBurning()
        {
            StageRunner runner = AtExitWithEveryoneOut();
            Assert.True(runner.CanWithdraw, "출구에서 전원 구조 상태면 철수할 수 있어야 한다");

            runner.Update(0.05f, new StageInput { Rescue = true });

            Assert.True(runner.Withdrew);
            Assert.Equal(StageOutcome.Won, runner.Outcome);
            Assert.True(runner.BuildResult().BurningCells > 0, "불을 남긴 채 이겼다");
        }

        // --- TC-3 ---
        [Fact]
        public void WalkingAwayEarnsOneStar_NotEnoughToOpenTheNextCall()
        {
            StageRunner runner = AtExitWithEveryoneOut();
            runner.Update(0.05f, new StageInput { Rescue = true });

            StageResult result = runner.BuildResult();
            int stars = StarRating.For(result);

            Assert.Equal(1, stars);
            Assert.True(stars < StarRating.StarsToUnlockNext,
                "사람만 구하고 나왔는데 다음 신고가 열렸다");
        }

        // --- TC-4 ---
        [Fact]
        public void AwayFromTheExit_YouCannotWithdraw()
        {
            StageRunner runner = Runner();
            foreach (Civilian civilian in runner.Civilians) civilian.Rescued = true;

            // 스폰 자리는 출구가 아니다.
            Assert.False(runner.CanWithdraw);

            runner.Update(0.05f, new StageInput { Rescue = true });

            Assert.False(runner.Withdrew);
            Assert.Equal(StageOutcome.InProgress, runner.Outcome);
        }

        // --- TC-5 ---
        [Fact]
        public void WithSomeoneStillInside_YouCannotWithdraw()
        {
            StageRunner runner = Runner();
            GridPoint exit = runner.Exits[0];
            runner.Player.X = exit.X + 0.5f;
            runner.Player.Y = exit.Y + 0.5f;

            Assert.True(runner.PendingCivilianCount > 0, "이 검사는 남은 시민이 있어야 의미가 있다");
            Assert.False(runner.CanWithdraw);

            runner.Update(0.05f, new StageInput { Rescue = true });

            Assert.False(runner.Withdrew);
        }

        // --- TC-6 ---
        [Fact]
        public void TimeRunningOut_IsStillALoss_EvenWithEveryoneSafe()
        {
            // 전원 생환만으로 이기게 하면 아무것도 안 하고 시계만 보내는 것이
            // 모든 현장의 공략이 된다. 이기고 싶으면 출구까지 걸어가 선언해야 한다.
            StageRunner runner = Runner();
            foreach (Civilian civilian in runner.Civilians) civilian.Rescued = true;

            runner.TimeLeft = 0.01f;
            runner.Update(0.05f, default);

            Assert.Equal(StageOutcome.LostTimeUp, runner.Outcome);
        }

        // --- TC-7 ---
        [Fact]
        public void FullSuppression_StillEarnsThreeStars()
        {
            // 철수가 생겼다고 제대로 끈 판의 보상이 깎이면 안 된다.
            var result = new StageResult
            {
                Won = true,
                Rescued = 3,
                CiviliansTotal = 3,
                BurningCells = 0,
                IntactRatio = 0.8f,
            };

            Assert.Equal(3, StarRating.For(result));
        }

        // --- TC-8 ---
        [Fact]
        public void LosingSomeone_NeverOpensTheNextCall()
        {
            // 불을 다 끄고 건물을 지켜도 한 명을 잃었으면 현장을 해결한 것이 아니다.
            var result = new StageResult
            {
                Won = true,
                Rescued = 2,
                CiviliansTotal = 3,
                BurningCells = 0,
                IntactRatio = 1f,
            };

            Assert.True(StarRating.For(result) < StarRating.StarsToUnlockNext);
        }

        // --- TC-9 ---
        [Fact]
        public void TheBot_NeverWithdraws_SoGearGatesStillHold()
        {
            // 봇은 구조 대상이 있을 때만 구조 버튼을 누른다.
            // 철수로 빠져나가 버리면 "장비가 모자라면 못 깬다"를 재는 모든 검사가 무의미해진다.
            foreach (StageDef stage in StageCatalog.All)
            {
                var runner = new StageRunner(stage, new[] { EquipmentId.Bucket });
                new GreedyBot(runner).Play();

                Assert.False(runner.Withdrew, stage.Name + "에서 봇이 철수했다");
            }
        }
    }
}
