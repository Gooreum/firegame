using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using FireGame.Core.Sim;
using Xunit;

namespace FireGame.Core.Tests
{
    /// <summary>
    /// 문. 이 게임에서 <b>쏘는 것 말고 처음으로 생긴 동사</b>다.
    /// 닫으면 방이 지켜지고, 열면 숨통이 트이는 대신 불이 자란다.
    /// </summary>
    public class DoorTests
    {
        // 방 둘을 칸막이 하나로 가르고 가운데에 문을 둔다.
        // (3,3)은 왼쪽 방 안에 놓인 목재다. 여기 불을 붙이면 연기가 왼쪽 방에만 찬다.
        // (5,3)은 문 바로 위 칸막이다. 여기 불을 붙이면 문을 사이에 둔 열 전달을 잴 수 있다.
        //          01234567890
        private static readonly string[] TwoRooms =
        {
            "###########",
            "#.........#",
            "#.WWWWWWW.#",
            "#.WW.W..W.#",
            "#.W..D..W.#",
            "#.W..W..W.#",
            "#.WWWWWWW.#",
            "#....@....#",
            "###########",
        };

        private const int DoorX = 5;
        private const int DoorY = 4;

        /// <summary>왼쪽 방 안에 홀로 선 목재. 연기가 방 밖으로 나가려면 문을 지나야 한다.</summary>
        private static readonly GridPoint InRoom = new GridPoint(3, 3);

        /// <summary>문 바로 위 칸막이. 문을 사이에 둔 열 전달을 재는 자리다.</summary>
        private static readonly GridPoint AtDoor = new GridPoint(5, 3);

        private static StageRunner Room(bool shutDoor, GridPoint ignite)
        {
            var def = new StageDef(
                id: 900, name: "DOORLAB", map: TwoRooms,
                wind: Wind.None, timeLimitSeconds: 600f, basePayout: 0);

            var runner = new StageRunner(def, new[] { EquipmentId.Bucket });

            Assert.True(Materials.Of(runner.Grid[ignite.X, ignite.Y].Material).Flammable,
                "발화점이 안 타는 재질이다");

            runner.Grid[ignite.X, ignite.Y].State = CellState.Burning;
            runner.Grid[ignite.X, ignite.Y].Fuel = 1000f;   // 재는 동안 다 타버리지 않게

            if (shutDoor) runner.Grid[DoorX, DoorY].Shut = true;
            return runner;
        }

        private static void Burn(StageRunner runner, int ticks)
        {
            for (int t = 0; t < ticks; t++) runner.Sim.Tick();
        }

        // --- TC-1 ---
        [Fact]
        public void DoorsStartOpen()
        {
            foreach (StageDef stage in StageCatalog.All)
            {
                var runner = new StageRunner(stage, new[] { EquipmentId.Bucket });

                for (int i = 0; i < runner.Grid.Count; i++)
                {
                    Assert.False(runner.Grid.Cells[i].Shut,
                        stage.Name + "의 문이 닫힌 채로 시작한다");
                }
            }
        }

        // --- TC-2 ---
        [Fact]
        public void ShutDoor_KeepsSmokeOutOfTheNextRoom()
        {
            StageRunner open = Room(shutDoor: false, InRoom);
            StageRunner shut = Room(shutDoor: true, InRoom);

            Burn(open, 400);
            Burn(shut, 400);

            // 문 오른쪽 방 한가운데.
            float openSide = open.Grid[6, 4].Smoke;
            float shutSide = shut.Grid[6, 4].Smoke;

            Assert.True(openSide > 0.05f, "열어 두면 옆방이 차야 한다. " + openSide);
            Assert.True(shutSide < openSide * 0.35f,
                "열림 " + openSide + " vs 닫힘 " + shutSide);
        }

        // --- TC-3 ---
        [Fact]
        public void ShutDoor_SlowsHeatButDoesNotStopIt()
        {
            StageRunner open = Room(shutDoor: false, AtDoor);
            StageRunner shut = Room(shutDoor: true, AtDoor);

            Burn(open, 40);
            Burn(shut, 40);

            float openHeat = open.Grid[DoorX, DoorY].Heat;
            float shutHeat = shut.Grid[DoorX, DoorY].Heat;

            // 닫아도 결국 데워진다 — 문은 시간을 벌 뿐 해결책이 아니다.
            Assert.True(shutHeat > 0f, "닫힌 문에도 열은 전해져야 한다");
            Assert.True(shutHeat < openHeat * 0.4f,
                "열림 " + openHeat + " vs 닫힘 " + shutHeat);
        }

        // --- TC-4 ---
        [Fact]
        public void ShutDoor_BlocksMovement()
        {
            StageRunner runner = Room(shutDoor: true, InRoom);

            runner.Player.X = DoorX - 0.5f;
            runner.Player.Y = DoorY + 0.5f;

            float startX = runner.Player.X;
            for (int t = 0; t < 40; t++)
            {
                runner.Player.Update(0.05f, runner.Grid, 1f, 0f);
            }

            Assert.True(runner.Player.X < startX + 0.6f,
                "닫힌 문을 통과했다. x=" + runner.Player.X);
        }

        // --- TC-5 ---
        [Fact]
        public void OpenDoor_LetsPlayerThrough()
        {
            StageRunner runner = Room(shutDoor: false, InRoom);

            runner.Player.X = DoorX - 0.5f;
            runner.Player.Y = DoorY + 0.5f;

            for (int t = 0; t < 40; t++)
            {
                runner.Player.Update(0.05f, runner.Grid, 1f, 0f);
            }

            Assert.True(runner.Player.X > DoorX + 1f,
                "열린 문을 못 지났다. x=" + runner.Player.X);
        }

        // --- TC-6 ---
        [Fact]
        public void ShutDoor_BlocksSight()
        {
            StageRunner open = Room(shutDoor: false, InRoom);
            StageRunner shut = Room(shutDoor: true, InRoom);

            open.Vision.Refresh(open.Grid, open.Buildings, DoorX - 1, DoorY);
            shut.Vision.Refresh(shut.Grid, shut.Buildings, DoorX - 1, DoorY);

            Assert.True(open.Vision.Visible(DoorX + 1, DoorY), "열린 문 너머는 보여야 한다");
            Assert.False(shut.Vision.Visible(DoorX + 1, DoorY), "닫힌 문 너머는 안 보여야 한다");
        }

        // --- TC-8 ---
        [Fact]
        public void Interact_TogglesTheDoorBeside()
        {
            StageRunner runner = Room(shutDoor: false, InRoom);

            // 문 왼쪽 칸에 서서 오른쪽을 본다.
            runner.Player.X = DoorX - 0.5f;
            runner.Player.Y = DoorY + 0.5f;
            runner.Player.Aim = AimDirection.E;
            runner.Update(0.05f, default);

            Assert.NotNull(runner.DoorAtHand);

            runner.Update(0.05f, new StageInput { Interact = true });
            Assert.True(runner.Grid[DoorX, DoorY].Shut, "닫히지 않았다");
            Assert.Equal(InteractResult.Shut, runner.LastInteract);

            runner.Update(0.05f, new StageInput { Interact = true });
            Assert.False(runner.Grid[DoorX, DoorY].Shut, "다시 안 열렸다");
            Assert.Equal(InteractResult.Opened, runner.LastInteract);
        }

        // --- TC-9 ---
        [Fact]
        public void Interact_DoesNothingWhenNoDoorIsNear()
        {
            StageRunner runner = Room(shutDoor: false, InRoom);

            // 마당 한가운데. 사방에 문이 없다.
            runner.Player.X = 5.5f;
            runner.Player.Y = 7.5f;
            runner.Update(0.05f, new StageInput { Interact = true });

            Assert.Null(runner.DoorAtHand);
            Assert.Equal(InteractResult.None, runner.LastInteract);
        }

        // --- TC-10 ---
        [Fact]
        public void Interact_RefusesABurningDoor()
        {
            StageRunner runner = Room(shutDoor: false, InRoom);
            runner.Grid[DoorX, DoorY].State = CellState.Burning;

            runner.Player.X = DoorX - 0.5f;
            runner.Player.Y = DoorY + 0.5f;
            runner.Update(0.05f, new StageInput { Interact = true });

            Assert.False(runner.Grid[DoorX, DoorY].Shut, "불타는 문을 닫았다");
            Assert.Equal(InteractResult.Burning, runner.LastInteract);
        }

        // --- TC-11 ---
        [Fact]
        public void Interact_ReachesDiagonally()
        {
            StageRunner runner = Room(shutDoor: false, InRoom);

            // 문에서 대각으로 한 칸 떨어진 자리. 칸을 맞춰 서지 않아도 잡혀야 한다.
            runner.Player.X = DoorX - 0.5f;
            runner.Player.Y = DoorY - 0.5f;
            runner.Update(0.05f, new StageInput { Interact = true });

            Assert.True(runner.Grid[DoorX, DoorY].Shut, "대각으로 선 문을 못 잡았다");
        }

        // --- TC-12 ---
        [Fact]
        public void HoldingTheDoorButton_TogglesOnlyOnce()
        {
            // 구조 버튼과 같은 함정이다. 모서리를 안 거르면 누르고 있는 동안
            // 문이 매 프레임 여닫히며 떨린다.
            var flow = new GameFlow(SaveData.NewGame());
            Assert.True(flow.SelectMission(0));
            flow.BeginMission();

            StageRunner runner = flow.Runner;
            GridPoint? door = FirstDoor(runner);
            Assert.NotNull(door);

            // 문 왼쪽에 붙여 세운다.
            runner.Player.X = door.Value.X - 0.5f;
            runner.Player.Y = door.Value.Y + 0.5f;

            flow.SetInteract(true);
            for (int frame = 0; frame < 20; frame++) flow.Update(0.05f);

            Assert.True(runner.Grid[door.Value.X, door.Value.Y].Shut,
                "누르고 있는 1초 동안 문이 여닫히기를 반복해 도로 열렸다");
        }

        private static GridPoint? FirstDoor(StageRunner runner)
        {
            for (int y = 0; y < runner.Grid.Height; y++)
            {
                for (int x = 0; x < runner.Grid.Width; x++)
                {
                    if (runner.Grid[x, y].Material == (byte)MaterialId.Door) return new GridPoint(x, y);
                }
            }
            return null;
        }

        // --- TC-7 ---
        [Fact]
        public void OpenDoorNearby_MakesFireBurnHotter()
        {
            StageRunner open = Room(shutDoor: false, AtDoor);
            StageRunner shut = Room(shutDoor: true, AtDoor);

            // 8틱(0.8초)만 돌린다. 더 두면 (5,2)가 제 불로 타기 시작해
            // 자기 열이 대부분을 차지하고 바람 차이가 묻힌다.
            Burn(open, 8);
            Burn(shut, 8);

            // (5,2)는 불 위쪽 칸막이다. 문과 맞닿아 있지 않으므로
            // 이 칸의 차이는 오직 DraftBoost에서만 나온다.
            float openHeat = open.Grid[5, 2].Heat;
            float shutHeat = shut.Grid[5, 2].Heat;

            Assert.Equal(SimConfig.DraftBoost, openHeat / shutHeat, 3);
        }
    }
}
