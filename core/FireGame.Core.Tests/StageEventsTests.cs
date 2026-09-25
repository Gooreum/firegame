using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using Xunit;

namespace FireGame.Core.Tests
{
    /// <summary>
    /// 진행 중 사건. 외운 순서를 무너뜨리되, 예고 없이 등 뒤에서 터지지 않고
    /// 시민을 가두지 않아야 한다. 시드 0에서는 하나도 일어나지 않는다.
    /// </summary>
    public class StageEventsTests
    {
        private const int Seed = 4812;

        private static StageRunner Runner(StageDef stage, int seed)
        {
            return new StageRunner(stage, Loadout.FromIds(new[] { EquipmentId.Bucket }), seed);
        }

        private static float FireAt(StageRunner runner)
        {
            return runner.Def.TimeLimitSeconds * StageEvents.SecondIgnitionAt;
        }

        /// <summary>여러 채짜리 현장에서 2차 발화 예고가 뜰 때까지 가만히 서 있는다.</summary>
        private static StageRunner UntilWarning(int seed)
        {
            StageRunner runner = Runner(StageCatalog.Shopping, seed);
            while (!runner.IsOver && runner.Events.PendingIgnition == null && runner.TimeLeft > FireAt(runner))
            {
                runner.Update(0.05f, default);
            }
            return runner;
        }

        // --- TC-1 ---
        [Fact]
        public void SeedZero_HasNoEvents()
        {
            foreach (StageDef stage in StageCatalog.All)
            {
                StageRunner runner = Runner(stage, 0);
                while (!runner.IsOver)
                {
                    runner.Update(0.1f, default);
                    Assert.Equal(StageEventKind.None, runner.Events.JustHappened);
                    Assert.Null(runner.Events.PendingIgnition);
                }

                for (int i = 0; i < runner.Grid.Count; i++) Assert.False(runner.Grid.Cells[i].Jammed);
            }
        }

        // --- TC-2 ---
        [Fact]
        public void SecondIgnition_IsAnnounced_ThreeSecondsAhead()
        {
            StageRunner runner = UntilWarning(Seed);

            Assert.NotNull(runner.Events.PendingIgnition);
            Assert.InRange(runner.Events.SecondsUntilIgnition, 0f, StageEvents.WarningSeconds);
            Assert.True(runner.TimeLeft > FireAt(runner), "예고 없이 바로 붙었다");

            GridPoint at = runner.Events.PendingIgnition.Value;
            // 불이 없는 건물에서 골라야 "옮겨 붙는다"가 된다.
            Assert.Equal(0, runner.Buildings.Of(at.X, at.Y).BurningCells(runner.Grid));
            Assert.Equal((byte)MaterialId.Wood, runner.Grid[at.X, at.Y].Material);
        }

        // --- TC-3 ---
        [Fact]
        public void SecondIgnition_LightsTheAnnouncedCell_OnTime()
        {
            StageRunner runner = UntilWarning(Seed);
            GridPoint at = runner.Events.PendingIgnition.Value;

            bool happened = false;
            while (!runner.IsOver && !happened)
            {
                runner.Update(0.05f, default);
                happened = runner.Events.JustHappened == StageEventKind.SecondIgnition;
            }

            Assert.True(happened, "2차 발화가 안 일어났다");
            Assert.Equal(at, runner.Events.JustHappenedAt);
            Assert.Equal(CellState.Burning, runner.Grid[at.X, at.Y].State);
            Assert.True(runner.TimeLeft <= FireAt(runner) + 0.06f);
            Assert.Null(runner.Events.PendingIgnition);
        }

        // --- TC-4 ---
        [Fact]
        public void SecondIgnition_IsStopped_ByWettingTheCellDuringTheWarning()
        {
            StageRunner runner = UntilWarning(Seed);
            GridPoint at = runner.Events.PendingIgnition.Value;

            // 경고를 보고 달려가 미리 적셔 두면 막힌다 — 경고를 본 보람이 있어야 한다.
            runner.Grid[at.X, at.Y].Wet = 1f;

            while (!runner.IsOver && runner.TimeLeft > FireAt(runner) - 0.5f) runner.Update(0.05f, default);

            Assert.NotEqual(CellState.Burning, runner.Grid[at.X, at.Y].State);
        }

        // --- TC-5 ---
        [Fact]
        public void SingleBuildingSite_HasNoSecondIgnition()
        {
            // 주택은 한 채뿐이고 이미 타고 있다. 옮겨 붙을 곳이 없으면 사건도 없다.
            StageRunner runner = Runner(StageCatalog.Residential, Seed);
            while (!runner.IsOver)
            {
                runner.Update(0.1f, default);
                Assert.NotEqual(StageEventKind.SecondIgnition, runner.Events.JustHappened);
            }
        }

        /// <summary>문이 둘 이상인 건물을 찾는다. 없으면 null.</summary>
        private static Building MultiDoor(StageRunner runner)
        {
            foreach (Building building in runner.Buildings.All)
            {
                if (building.Entrances.Length >= 2) return building;
            }
            return null;
        }

        private static void BurnDown(StageRunner runner, Building building)
        {
            foreach (GridPoint p in building.Cells)
            {
                if (Materials.Of(runner.Grid[p.X, p.Y].Material).Flammable) runner.Grid[p.X, p.Y].State = CellState.Burnt;
            }
        }

        /// <summary>
        /// 상가에서 문이 둘인 점포. 주택은 한 채가 현장 전부라 태우면 판이 바로 끝난다.
        /// </summary>
        private static StageRunner WithMultiDoorBuilding(int seed, out Building building)
        {
            StageRunner runner = Runner(StageCatalog.Shopping, seed);
            building = MultiDoor(runner);
            return runner;
        }

        // --- TC-6 ---
        [Fact]
        public void BurntOutBuilding_Collapses_ButKeepsTheDoorNearestTheExit()
        {
            StageRunner runner = WithMultiDoorBuilding(Seed, out Building building);
            Assert.NotNull(building);

            BurnDown(runner, building);
            runner.Update(0.05f, default);

            Assert.Equal(StageEventKind.Collapse, runner.Events.JustHappened);

            int open = 0;
            GridPoint kept = default;
            foreach (GridPoint door in building.Entrances)
            {
                if (runner.Grid[door.X, door.Y].Jammed) Assert.True(runner.Grid[door.X, door.Y].Shut);
                else { open++; kept = door; }
            }

            // 전부 막히면 안에 남은 사람이 갇힌다.
            Assert.Equal(1, open);

            int keptDistance = NearestExitDistance(runner, kept);
            foreach (GridPoint door in building.Entrances)
            {
                Assert.True(NearestExitDistance(runner, door) >= keptDistance, "출구에서 먼 문을 남겼다");
            }
        }

        private static int NearestExitDistance(StageRunner runner, GridPoint door)
        {
            int best = int.MaxValue;
            foreach (GridPoint exit in runner.Exits)
            {
                int dx = door.X - exit.X;
                int dy = door.Y - exit.Y;
                best = System.Math.Min(best, (dx * dx) + (dy * dy));
            }
            return best;
        }

        // --- TC-7 ---
        [Fact]
        public void BurntOutBuilding_DoesNotCollapse_OnSeedZero()
        {
            StageRunner runner = WithMultiDoorBuilding(0, out Building building);
            BurnDown(runner, building);
            runner.Update(0.05f, default);

            Assert.Equal(StageEventKind.None, runner.Events.JustHappened);
            foreach (GridPoint door in building.Entrances) Assert.False(runner.Grid[door.X, door.Y].Jammed);
        }

        // --- TC-8 ---
        [Fact]
        public void JammedDoor_CannotBeOpened()
        {
            StageRunner runner = WithMultiDoorBuilding(Seed, out Building building);
            BurnDown(runner, building);
            runner.Update(0.05f, default);

            GridPoint jammed = default;
            foreach (GridPoint door in building.Entrances)
            {
                if (runner.Grid[door.X, door.Y].Jammed) jammed = door;
            }

            // 막힌 문 바로 곁 실외 칸에 서서 문을 본다.
            foreach (AimDirection aim in new[] { AimDirection.N, AimDirection.E, AimDirection.S, AimDirection.W })
            {
                int x = jammed.X - Aiming.OffsetX(aim);
                int y = jammed.Y - Aiming.OffsetY(aim);
                if (!runner.Buildings.IsOutdoor(x, y) || !Materials.Of(runner.Grid[x, y].Material).Walkable) continue;

                runner.Player.X = x + 0.5f;
                runner.Player.Y = y + 0.5f;
                runner.Player.Aim = aim;
                break;
            }

            runner.Update(0.05f, default);
            Assert.False(runner.IsOver);
            Assert.Equal(jammed, runner.DoorAtHand);

            runner.Update(0.05f, new StageInput { Interact = true });

            Assert.Equal(InteractResult.Jammed, runner.LastInteract);
            Assert.True(runner.Grid[jammed.X, jammed.Y].Shut);
        }

        // --- TC-9 ---
        [Fact]
        public void SameSeed_SameEvents()
        {
            StageRunner a = UntilWarning(Seed);
            StageRunner b = UntilWarning(Seed);

            Assert.Equal(a.Events.PendingIgnition, b.Events.PendingIgnition);
            Assert.Equal(a.TicksElapsed, b.TicksElapsed);
        }

        // --- TC-10 ---
        [Fact]
        public void FirstRun_IsTheDesignedMap_ReplaysAreVaried()
        {
            var flow = new GameFlow(SaveData.NewGame()) { SeedSource = () => 777 };

            Assert.True(flow.SelectMission(0));
            flow.BeginMission();
            Assert.Equal(0, flow.Runner.Seed);

            // 한 번 깨서 다음 현장을 열었다.
            flow.Save.RecordResult(0, StarRating.StarsToUnlockNext);
            var replay = new GameFlow(flow.Save) { SeedSource = () => 777 };
            Assert.True(replay.SelectMission(0));
            replay.BeginMission();
            Assert.Equal(777, replay.Runner.Seed);
        }

        // --- TC-11 ---
        [Fact]
        public void ZeroFromTheSeedSource_NeverTurnsOffTheVariation()
        {
            var save = SaveData.NewGame();
            save.RecordResult(0, StarRating.StarsToUnlockNext);
            var flow = new GameFlow(save) { SeedSource = () => 0 };

            Assert.True(flow.SelectMission(0));
            flow.BeginMission();
            Assert.NotEqual(0, flow.Runner.Seed);
        }

        // --- TC-12 ---
        [Fact]
        public void DefaultSeedSource_IsDeterministic_SoTestsNeverFlake()
        {
            // 기본값이 시계면 깬 현장을 GameFlow로 다시 뛰는 테스트가 가끔만 진다.
            var save = SaveData.NewGame();
            save.RecordResult(0, StarRating.StarsToUnlockNext);

            var flow = new GameFlow(save);
            Assert.True(flow.SelectMission(0));
            flow.BeginMission();
            Assert.Equal(1, flow.Runner.Seed);

            new GreedyBot(flow.Runner).Play();
            flow.Update(0.016f);
            flow.RetryMission();
            Assert.Equal(2, flow.Runner.Seed);
        }
    }
}
