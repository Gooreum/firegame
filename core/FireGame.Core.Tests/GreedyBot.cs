using System;
using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;

namespace FireGame.Core.Tests
{
    /// <summary>
    /// 스테이지가 실제로 클리어 가능한지 재는 자동 플레이어.
    ///
    /// 사람의 실력을 흉내내려는 게 아니라 "평범하게 플레이하면 이기는가"의
    /// 하한선을 재기 위한 것이다. 이 봇이 못 깨면 사람도 어렵다고 본다.
    ///
    /// 정책: 가장 가까운 불을 BFS로 찾아가 인접해서 끄고,
    ///       불이 다 꺼지면 시민을 업어 출구로 옮긴다.
    /// </summary>
    internal sealed class GreedyBot
    {
        private readonly StageRunner _runner;
        private readonly int[] _distance;
        private readonly int[] _cameFrom;
        private readonly Queue<int> _queue = new Queue<int>();

        private static readonly int[] StepDx = { 0, 0, -1, 1 };
        private static readonly int[] StepDy = { -1, 1, 0, 0 };

        public GreedyBot(StageRunner runner)
        {
            _runner = runner;
            _distance = new int[runner.Grid.Count];
            _cameFrom = new int[runner.Grid.Count];
        }

        /// <summary>봇이 고른 슬롯 사용 횟수. 장비별 소모를 관찰할 때 쓴다.</summary>
        public int ShotsFired;

        /// <summary>이 아래로 떨어지면 물러나 회복한다.</summary>
        private const float RetreatHp = 35f;

        /// <summary>이만큼 회복하면 다시 달려든다.</summary>
        private const float ResumeHp = 80f;

        private bool _retreating;

        public StageOutcome Play(float dt = 0.05f, float maxSeconds = 300f)
        {
            int maxSteps = (int)(maxSeconds / dt);

            for (int step = 0; step < maxSteps && !_runner.IsOver; step++)
            {
                _runner.Update(dt, NextInput());
            }

            return _runner.Outcome;
        }

        private StageInput NextInput()
        {
            FireGrid grid = _runner.Grid;
            PlayerState player = _runner.Player;

            // 사람이라면 체력이 바닥나기 전에 물러선다. 봇도 그렇게 해야
            // 측정값이 "이 스테이지가 클리어 가능한가"를 제대로 반영한다.
            if (player.Hp <= RetreatHp) _retreating = true;
            if (player.Hp >= ResumeHp) _retreating = false;

            if (_retreating) return Retreat();

            // 구조가 먼저다. 유류·전기 화재는 스스로 꺼지지 않으므로
            // 불부터 붙들면 시민을 영영 데리러 가지 못한다.
            if (_runner.PendingCivilianCount > 0)
            {
                StageInput rescue = player.CarryingCivilian
                    ? MoveToward(NearestExit())
                    : MoveToward(NearestCivilian());

                // 시민은 저절로 업히지 않는다. 손이 닿으면 버튼을 누른다.
                rescue.Rescue = _runner.RescueTarget != null;

                // 길이 불로 막혀 갈 수 없으면 가만히 서 있지 말고 불부터 끈다.
                // 그래야 길이 열린다.
                if (rescue.Rescue || rescue.MoveX != 0f || rescue.MoveY != 0f) return rescue;
            }

            if (grid.CountBurning() > 0)
            {
                return FightFire();
            }

            return default;
        }

        /// <summary>가장 가까운 불에서 멀어지는 방향으로 도망친다.</summary>
        private StageInput Retreat()
        {
            FireGrid grid = _runner.Grid;
            PlayerState player = _runner.Player;

            float awayX = 0f;
            float awayY = 0f;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (grid[x, y].State != CellState.Burning) continue;

                    float dx = player.X - (x + 0.5f);
                    float dy = player.Y - (y + 0.5f);
                    float distanceSquared = (dx * dx) + (dy * dy);
                    if (distanceSquared > 64f) continue;

                    // 가까운 불일수록 더 세게 밀어낸다.
                    float weight = 1f / Math.Max(distanceSquared, 0.25f);
                    awayX += dx * weight;
                    awayY += dy * weight;
                }
            }

            if (awayX == 0f && awayY == 0f) return default;

            return new StageInput { MoveX = awayX, MoveY = awayY };
        }

        private StageInput FightFire()
        {
            FireGrid grid = _runner.Grid;
            PlayerState player = _runner.Player;

            BuildDistances(player.CellX, player.CellY);

            // 물 계열 장비를 들었으면 불꽃을 쫓지 말고 방화선을 친다.
            //
            // 타는 칸을 끄려면 여러 번 때려야 하는데 그 사이 불은 계속 전진한다.
            // 반면 아직 안 붙은 칸을 미리 적시면 젖음이 점화를 막아 한 번에 영구 차단된다.
            // 실제 소방 전술이기도 하고, 이 게임에서 양동이가 쓸모 있어지는 유일한 길이다.
            StageInput firebreak = TryFirebreak();
            if (firebreak.Fire) return firebreak;

            // 불의 가장자리부터 친다.
            //
            // 타는 이웃이 많은 칸일수록 열이 빨리 되살아나 진압력이 밀린다.
            // 목재 기준 재생은 가장자리 1.00/초, 1칸 폭 벽 한가운데 1.50/초,
            // 빽빽한 군집 1.85/초다. 양동이(1.60/초)는 가장자리를 쳐야만 이긴다.
            // 사람이라면 당연히 그렇게 하므로, 봇도 그래야 측정이 공정하다.
            int bestStand = -1;
            int bestNeighbors = int.MaxValue;
            int bestDistance = int.MaxValue;
            GridPoint bestTarget = default;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (grid[x, y].State != CellState.Burning) continue;

                    int burningNeighbors = CountBurningNeighbors(x, y);

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;

                            int sx = x + dx;
                            int sy = y + dy;
                            if (!grid.InBounds(sx, sy)) continue;

                            int index = grid.Index(sx, sy);
                            if (_distance[index] < 0) continue;

                            bool better = burningNeighbors < bestNeighbors
                                          || (burningNeighbors == bestNeighbors
                                              && _distance[index] < bestDistance);
                            if (!better) continue;

                            bestNeighbors = burningNeighbors;
                            bestDistance = _distance[index];
                            bestStand = index;
                            bestTarget = new GridPoint(x, y);
                        }
                    }
                }
            }

            if (bestStand < 0) return default;

            int standX = bestStand % grid.Width;
            int standY = bestStand / grid.Width;

            // 아직 자리에 도착하지 못했으면 이동한다.
            if (standX != player.CellX || standY != player.CellY)
            {
                return MoveToward(new GridPoint(standX, standY));
            }

            // 조준을 직접 맞춘다. 이동 입력으로 조준을 돌리면 셀 안에서의
            // 소수점 위치에 따라 방위가 한 칸씩 빗나가 발사 조건을 못 맞춘다.
            // 이동 입력을 0으로 두어야 PlayerState가 조준을 덮어쓰지 않는다.
            player.Aim = DirectionTo(player.CellX, player.CellY, bestTarget);

            ShotsFired++;
            return new StageInput { Fire = true, Slot = PickSlot(bestTarget) };
        }

        /// <summary>화재 등급에 맞는 장비를 고른다. 없으면 가진 것 중 첫 번째.</summary>
        private int PickSlot(GridPoint target)
        {
            FireClass fireClass = Materials.Of(_runner.Grid[target.X, target.Y].Material).Class;
            PlayerState player = _runner.Player;

            int bestSlot = 0;
            float bestEffect = float.NegativeInfinity;

            for (int slot = 0; slot < PlayerState.SlotCount; slot++)
            {
                EquipmentDef def = _runner.SlotEquipment(slot);
                if (def == null) continue;
                if (!player.CanFire(slot, def)) continue;

                float effect = Sim.Suppression.EffectivenessOf(def.Agent.Type, fireClass) * def.Agent.Power;
                if (effect > bestEffect)
                {
                    bestEffect = effect;
                    bestSlot = slot;
                }
            }

            return bestSlot;
        }

        /// <summary>
        /// 불에 막 닿으려는 칸을 미리 적셔 확산을 끊는다.
        /// 젖음을 만들지 않는 약제(CO2)로는 할 수 없다.
        /// </summary>
        private StageInput TryFirebreak()
        {
            FireGrid grid = _runner.Grid;
            PlayerState player = _runner.Player;

            int slot = PickWettingSlot();
            if (slot < 0) return default;

            int bestStand = -1;
            int bestDistance = int.MaxValue;
            GridPoint bestTarget = default;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    ref Cell cell = ref grid[x, y];
                    if (cell.State != CellState.Intact) continue;
                    if (cell.Wet > 0f) continue;
                    if (!Materials.Of(cell.Material).Flammable) continue;
                    if (CountBurningNeighbors(x, y) == 0) continue;

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;

                            int sx = x + dx;
                            int sy = y + dy;
                            if (!grid.InBounds(sx, sy)) continue;

                            int index = grid.Index(sx, sy);
                            if (_distance[index] < 0 || _distance[index] >= bestDistance) continue;

                            bestDistance = _distance[index];
                            bestStand = index;
                            bestTarget = new GridPoint(x, y);
                        }
                    }
                }
            }

            if (bestStand < 0) return default;

            int standX = bestStand % grid.Width;
            int standY = bestStand / grid.Width;

            if (standX != player.CellX || standY != player.CellY)
            {
                StageInput move = MoveToward(new GridPoint(standX, standY));
                // 이동이 불가능하면 방화선을 포기하고 호출자가 진압으로 넘어가게 한다.
                if (move.MoveX == 0f && move.MoveY == 0f) return default;
                return move;
            }

            player.Aim = DirectionTo(player.CellX, player.CellY, bestTarget);
            ShotsFired++;
            return new StageInput { Fire = true, Slot = slot };
        }

        /// <summary>젖음을 남기는 장비 중 지금 쏠 수 있는 슬롯. 없으면 -1.</summary>
        private int PickWettingSlot()
        {
            PlayerState player = _runner.Player;

            for (int slot = 0; slot < PlayerState.SlotCount; slot++)
            {
                EquipmentDef def = _runner.SlotEquipment(slot);
                if (def == null) continue;
                if (def.Agent.Wetness <= 0f) continue;
                if (!player.CanFire(slot, def)) continue;

                return slot;
            }

            return -1;
        }

        private int CountBurningNeighbors(int x, int y)
        {
            FireGrid grid = _runner.Grid;
            int count = 0;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int nx = x + dx;
                    int ny = y + dy;
                    if (!grid.InBounds(nx, ny)) continue;
                    if (grid[nx, ny].State == CellState.Burning) count++;
                }
            }

            return count;
        }

        /// <summary>인접한 목표 칸을 가리키는 8방위를 구한다.</summary>
        private static AimDirection DirectionTo(int fromX, int fromY, GridPoint target)
        {
            int dx = Math.Sign(target.X - fromX);
            int dy = Math.Sign(target.Y - fromY);

            for (int i = 0; i < 8; i++)
            {
                var direction = (AimDirection)i;
                if (Aiming.OffsetX(direction) == dx && Aiming.OffsetY(direction) == dy) return direction;
            }

            return AimDirection.E;
        }

        private GridPoint NearestCivilian()
        {
            GridPoint best = default;
            float bestDistance = float.MaxValue;

            foreach (Civilian civilian in _runner.Civilians)
            {
                if (!civilian.Pending) continue;

                float dx = civilian.X - _runner.Player.X;
                float dy = civilian.Y - _runner.Player.Y;
                float distance = (dx * dx) + (dy * dy);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = new GridPoint((int)Math.Floor(civilian.X), (int)Math.Floor(civilian.Y));
                }
            }

            return best;
        }

        private GridPoint NearestExit()
        {
            GridPoint best = default;
            float bestDistance = float.MaxValue;

            foreach (GridPoint exit in _runner.Exits)
            {
                float dx = (exit.X + 0.5f) - _runner.Player.X;
                float dy = (exit.Y + 0.5f) - _runner.Player.Y;
                float distance = (dx * dx) + (dy * dy);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = exit;
                }
            }

            return best;
        }

        /// <summary>BFS 경로를 따라 다음 칸으로 향하는 입력을 만든다.</summary>
        private StageInput MoveToward(GridPoint goal)
        {
            FireGrid grid = _runner.Grid;
            PlayerState player = _runner.Player;

            BuildDistances(player.CellX, player.CellY);

            int goalIndex = grid.Index(goal.X, goal.Y);
            if (!grid.InBounds(goal.X, goal.Y) || _distance[goalIndex] < 0) return default;

            // 목표에서 시작점 방향으로 되짚어 첫 한 걸음을 찾는다.
            int cursor = goalIndex;
            int start = grid.Index(player.CellX, player.CellY);

            while (_cameFrom[cursor] != start && _cameFrom[cursor] >= 0)
            {
                cursor = _cameFrom[cursor];
            }

            if (cursor == start) return default;

            int nextX = cursor % grid.Width;
            int nextY = cursor / grid.Width;

            return new StageInput
            {
                MoveX = (nextX + 0.5f) - player.X,
                MoveY = (nextY + 0.5f) - player.Y,
            };
        }

        /// <summary>통행 가능하고 불타지 않는 칸만 지나는 BFS.</summary>
        private void BuildDistances(int startX, int startY)
        {
            FireGrid grid = _runner.Grid;

            for (int i = 0; i < _distance.Length; i++)
            {
                _distance[i] = -1;
                _cameFrom[i] = -1;
            }

            if (!grid.InBounds(startX, startY)) return;

            int start = grid.Index(startX, startY);
            _distance[start] = 0;
            _queue.Clear();
            _queue.Enqueue(start);

            while (_queue.Count > 0)
            {
                int current = _queue.Dequeue();
                int cx = current % grid.Width;
                int cy = current / grid.Width;

                for (int i = 0; i < 4; i++)
                {
                    int nx = cx + StepDx[i];
                    int ny = cy + StepDy[i];
                    if (!grid.InBounds(nx, ny)) continue;

                    int next = grid.Index(nx, ny);
                    if (_distance[next] >= 0) continue;
                    if (!Materials.Of(grid.Cells[next].Material).Walkable) continue;

                    // 불길 속으로는 걸어 들어가지 않는다.
                    if (grid.Cells[next].State == CellState.Burning) continue;

                    _distance[next] = _distance[current] + 1;
                    _cameFrom[next] = current;
                    _queue.Enqueue(next);
                }
            }
        }
    }
}
