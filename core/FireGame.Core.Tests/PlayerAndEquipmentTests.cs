using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using FireGame.Core.Sim;
using Xunit;

namespace FireGame.Core.Tests
{
    public class PlayerAndEquipmentTests
    {
        private static readonly List<GridPoint> Hits = new List<GridPoint>();

        private static FireGrid OpenFloor(int size)
        {
            var grid = new FireGrid(size, size);
            for (int i = 0; i < grid.Count; i++)
            {
                grid.Cells[i].Material = (byte)MaterialId.Floor;
                grid.Cells[i].State = CellState.Intact;
            }
            return grid;
        }

        // --- TC-1 ---
        [Fact]
        public void SinglePattern_HitsOnlyTheCellAhead()
        {
            var grid = OpenFloor(7);

            Aiming.Resolve(grid, 3, 3, AimDirection.E, AimPattern.Single, 1, Hits);

            Assert.Single(Hits);
            Assert.Equal(new GridPoint(4, 3), Hits[0]);
        }

        // --- TC-2 ---
        [Fact]
        public void ConePattern_HitsThreeCellsInAFan()
        {
            var grid = OpenFloor(7);

            Aiming.Resolve(grid, 3, 3, AimDirection.E, AimPattern.Cone, 1, Hits);

            Assert.Equal(3, Hits.Count);
            Assert.Contains(new GridPoint(4, 2), Hits);   // 북동
            Assert.Contains(new GridPoint(4, 3), Hits);   // 동
            Assert.Contains(new GridPoint(4, 4), Hits);   // 남동
        }

        // --- TC-3 ---
        [Fact]
        public void LinePattern_HitsFiveCellsInOrder()
        {
            var grid = OpenFloor(9);

            Aiming.Resolve(grid, 1, 4, AimDirection.E, AimPattern.Line, 5, Hits);

            Assert.Equal(5, Hits.Count);
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(new GridPoint(2 + i, 4), Hits[i]);
            }
        }

        // --- TC-4 ---
        [Fact]
        public void LinePattern_StopsAtAWall_HittingItButNotPassingThrough()
        {
            var grid = OpenFloor(9);

            // 조준선 3칸째에 목재 벽을 세운다.
            grid[4, 4].Material = (byte)MaterialId.Wood;

            Aiming.Resolve(grid, 1, 4, AimDirection.E, AimPattern.Line, 5, Hits);

            // 벽 자체는 맞아야 한다. 불타는 벽을 끌 수 없으면 게임이 성립하지 않는다.
            Assert.Equal(3, Hits.Count);
            Assert.Equal(new GridPoint(4, 4), Hits[2]);
            Assert.DoesNotContain(new GridPoint(5, 4), Hits);
        }

        // --- TC-5 ---
        [Fact]
        public void Patterns_NeverReturnCoordinatesOutsideTheGrid()
        {
            var grid = OpenFloor(5);

            Aiming.Resolve(grid, 0, 0, AimDirection.NW, AimPattern.Cone, 1, Hits);
            foreach (GridPoint p in Hits) Assert.True(grid.InBounds(p.X, p.Y));

            Aiming.Resolve(grid, 0, 0, AimDirection.W, AimPattern.Line, 5, Hits);
            Assert.Empty(Hits);

            Aiming.Resolve(grid, 4, 4, AimDirection.SE, AimPattern.Single, 1, Hits);
            Assert.Empty(Hits);
        }

        // --- TC-6 ---
        [Fact]
        public void Player_MovesAtConfiguredSpeedOverWalkableFloor()
        {
            var grid = OpenFloor(9);
            var player = new PlayerState();
            player.Spawn(new GridPoint(4, 4));

            float startX = player.X;
            player.Update(0.5f, grid, 1f, 0f);

            Assert.Equal(startX + (GameConfig.PlayerSpeed * 0.5f), player.X, 4);
            Assert.Equal(4.5f, player.Y, 4);
        }

        // --- TC-7 ---
        [Fact]
        public void Player_CannotWalkThroughWalls()
        {
            var grid = OpenFloor(9);
            grid[5, 4].Material = (byte)MaterialId.Concrete;

            var player = new PlayerState();
            player.Spawn(new GridPoint(4, 4));

            for (int i = 0; i < 40; i++) player.Update(0.1f, grid, 1f, 0f);

            Assert.True(player.X < 5f, "콘크리트 벽을 통과하면 안 된다. X=" + player.X);
        }

        // --- TC-8 ---
        [Fact]
        public void Player_SlidesAlongAWall_WhenMovingDiagonallyIntoIt()
        {
            var grid = OpenFloor(9);

            // 동쪽 한 줄 전체를 막아 대각 입력 중 X축만 차단되게 한다.
            for (int y = 0; y < 9; y++) grid[5, y].Material = (byte)MaterialId.Concrete;

            var player = new PlayerState();
            player.Spawn(new GridPoint(4, 4));
            float startY = player.Y;

            for (int i = 0; i < 20; i++) player.Update(0.1f, grid, 1f, 1f);

            Assert.True(player.X < 5f, "막힌 X축은 멈춰야 한다");
            Assert.True(player.Y > startY, "열린 Y축으로는 계속 미끄러져야 한다");
        }

        // --- TC-9 ---
        [Fact]
        public void Player_StandingInFire_TakesTheHigherDamage()
        {
            var grid = OpenFloor(9);
            grid[4, 4].Material = (byte)MaterialId.Wood;
            grid[4, 4].State = CellState.Burning;

            var player = new PlayerState();
            player.Spawn(new GridPoint(4, 4));

            player.Update(1f, grid, 0f, 0f);

            Assert.Equal(GameConfig.PlayerMaxHp - GameConfig.FireDamageInCell, player.Hp, 3);
        }

        // --- TC-10 ---
        [Fact]
        public void Player_NextToFire_TakesLessDamageThanStandingInIt()
        {
            var grid = OpenFloor(9);
            grid[5, 4].Material = (byte)MaterialId.Wood;
            grid[5, 4].State = CellState.Burning;

            var player = new PlayerState();
            player.Spawn(new GridPoint(4, 4));

            player.Update(1f, grid, 0f, 0f);

            Assert.Equal(GameConfig.PlayerMaxHp - GameConfig.FireDamageAdjacent, player.Hp, 3);
            Assert.True(GameConfig.FireDamageAdjacent < GameConfig.FireDamageInCell);
        }

        // --- TC-11 ---
        [Fact]
        public void Player_AwayFromAnyFire_TakesNoDamage()
        {
            var grid = OpenFloor(9);
            grid[8, 8].Material = (byte)MaterialId.Wood;
            grid[8, 8].State = CellState.Burning;

            var player = new PlayerState();
            player.Spawn(new GridPoint(1, 1));

            for (int i = 0; i < 100; i++) player.Update(0.1f, grid, 0f, 0f);

            Assert.Equal(GameConfig.PlayerMaxHp, player.Hp);
        }

        // --- TC-12 ---
        [Fact]
        public void PlayerHp_ClampsAtZero()
        {
            var grid = OpenFloor(5);
            grid[2, 2].Material = (byte)MaterialId.Wood;
            grid[2, 2].State = CellState.Burning;

            var player = new PlayerState();
            player.Spawn(new GridPoint(2, 2));

            for (int i = 0; i < 500; i++) player.Update(0.1f, grid, 0f, 0f);

            Assert.Equal(0f, player.Hp);
            Assert.False(player.IsAlive);
        }

        // --- TC-13 ---
        [Fact]
        public void CooldownEquipment_BlocksRefire_UntilTheCooldownElapses()
        {
            var grid = OpenFloor(5);
            var player = new PlayerState();
            player.Spawn(new GridPoint(2, 2));
            player.Equip(0, EquipmentCatalog.Bucket);

            Assert.True(player.CanFire(0, EquipmentCatalog.Bucket, null));
            player.ConsumeFire(0, EquipmentCatalog.Bucket);
            Assert.False(player.CanFire(0, EquipmentCatalog.Bucket, null));

            // 쿨다운의 절반만 지나면 아직 못 쏜다.
            player.Update(EquipmentCatalog.Bucket.CooldownSeconds * 0.5f, grid, 0f, 0f);
            Assert.False(player.CanFire(0, EquipmentCatalog.Bucket, null));

            player.Update(EquipmentCatalog.Bucket.CooldownSeconds, grid, 0f, 0f);
            Assert.True(player.CanFire(0, EquipmentCatalog.Bucket, null));
        }

        // --- TC-14 ---
        [Fact]
        public void ChargeEquipment_FiresExactlyMaxChargesTimes()
        {
            var grid = OpenFloor(5);
            var player = new PlayerState();
            player.Spawn(new GridPoint(2, 2));

            EquipmentDef ext = EquipmentCatalog.Extinguisher;
            player.Equip(0, ext);

            int fired = 0;
            for (int i = 0; i < ext.MaxCharges + 5; i++)
            {
                if (player.CanFire(0, ext, null))
                {
                    player.ConsumeFire(0, ext);
                    fired++;
                }
                player.Update(ext.CooldownSeconds, grid, 0f, 0f);
            }

            Assert.Equal(ext.MaxCharges, fired);
            Assert.False(player.CanFire(0, ext, null));
        }

        // --- TC-15 & TC-16 ---
        [Fact]
        public void Hose_OnlyFires_WhenWithinReachOfAHydrant()
        {
            var grid = OpenFloor(30);
            var hydrants = new List<GridPoint> { new GridPoint(25, 25) };

            EquipmentDef hose = EquipmentCatalog.Hose;
            var player = new PlayerState();
            player.Equip(0, hose);

            // 급수전에서 멀리 떨어진 곳
            player.Spawn(new GridPoint(2, 2));
            Assert.False(player.CanFire(0, hose, hydrants));

            // 급수전 바로 옆
            player.Spawn(new GridPoint(24, 25));
            Assert.True(player.CanFire(0, hose, hydrants));

            // 급수전이 아예 없는 맵
            Assert.False(player.CanFire(0, hose, new List<GridPoint>()));
            Assert.False(player.CanFire(0, hose, null));
        }

        // --- TC-17 ---
        [Fact]
        public void Catalog_MatchesTheDesignedProgression()
        {
            Assert.Equal(4, EquipmentCatalog.All.Length);

            Assert.Equal(0, EquipmentCatalog.Bucket.Price);
            Assert.Equal(500, EquipmentCatalog.Extinguisher.Price);
            Assert.Equal(3000, EquipmentCatalog.Hose.Price);
            Assert.Equal(10000, EquipmentCatalog.FoamExtinguisher.Price);

            Assert.Equal(AgentType.Water, EquipmentCatalog.Bucket.Agent.Type);
            Assert.Equal(AgentType.CO2, EquipmentCatalog.Extinguisher.Agent.Type);
            Assert.Equal(AgentType.Water, EquipmentCatalog.Hose.Agent.Type);
            Assert.Equal(AgentType.Foam, EquipmentCatalog.FoamExtinguisher.Agent.Type);

            Assert.Equal(AimPattern.Single, EquipmentCatalog.Bucket.Pattern);
            Assert.Equal(AimPattern.Cone, EquipmentCatalog.Extinguisher.Pattern);
            Assert.Equal(AimPattern.Line, EquipmentCatalog.Hose.Pattern);

            Assert.True(EquipmentCatalog.Hose.RequiresHydrant);
            Assert.False(EquipmentCatalog.Bucket.RequiresHydrant);

            // CO2는 기체라 수손 피해를 만들지 않는다.
            Assert.Equal(0f, EquipmentCatalog.Extinguisher.Agent.Wetness);
            Assert.True(EquipmentCatalog.Bucket.Agent.Wetness > 0f);

            Assert.Contains(EquipmentId.Bucket, EquipmentCatalog.StartingEquipment);
            Assert.DoesNotContain(EquipmentId.Hose, EquipmentCatalog.StartingEquipment);
        }

        // --- TC-18 ---
        [Fact]
        public void CatalogValues_ActuallyExtinguishTheirIntendedFireClass()
        {
            // 소화기(CO2)는 전기 화재를 한 방에 끈다.
            var electric = MapLoader.Parse(new[] { "###", "#E#", "###" }).Grid;
            electric[1, 1].State = CellState.Burning;
            var electricSim = new FireSim(electric);
            for (int t = 0; t < 80; t++) electricSim.Tick();

            Assert.Equal(
                SuppressionOutcome.Extinguished,
                Suppression.Apply(electric, 1, 1, EquipmentCatalog.Extinguisher.Agent));

            // 폼은 유류 화재를 번지게 하지 않고 끈다.
            // 유류는 연소속도 0.5/초라 2초면 다 타버리므로, 아직 타고 있는
            // 10틱(1초) 시점에 진압해야 한다.
            var oil = MapLoader.Parse(new[] { "#W#", "W~W", "#W#" }).Grid;
            oil[1, 1].State = CellState.Burning;
            var oilSim = new FireSim(oil);
            for (int t = 0; t < 10; t++) oilSim.Tick();
            Assert.Equal(CellState.Burning, oil[1, 1].State);

            SuppressionOutcome last = SuppressionOutcome.NoEffect;
            for (int i = 0; i < 8 && last != SuppressionOutcome.Extinguished; i++)
            {
                last = Suppression.Apply(oil, 1, 1, EquipmentCatalog.FoamExtinguisher.Agent);
            }
            Assert.Equal(SuppressionOutcome.Extinguished, last);

            // 같은 유류 화재에 양동이를 쓰면 오히려 번진다.
            var oil2 = MapLoader.Parse(new[] { "#W#", "W~W", "#W#" }).Grid;
            oil2[1, 1].State = CellState.Burning;

            Assert.Equal(
                SuppressionOutcome.Backfired,
                Suppression.Apply(oil2, 1, 1, EquipmentCatalog.Bucket.Agent));
        }

        [Fact]
        public void AimDirection_FollowsMovementInput()
        {
            var grid = OpenFloor(9);
            var player = new PlayerState();
            player.Spawn(new GridPoint(4, 4));

            player.Update(0.01f, grid, 1f, 0f);
            Assert.Equal(AimDirection.E, player.Aim);

            player.Update(0.01f, grid, 0f, -1f);
            Assert.Equal(AimDirection.N, player.Aim);

            player.Update(0.01f, grid, -1f, 1f);
            Assert.Equal(AimDirection.SW, player.Aim);
        }
    }
}
