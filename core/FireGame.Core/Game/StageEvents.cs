using System;
using System.Collections.Generic;
using FireGame.Core.Grid;
using FireGame.Core.Sim;

namespace FireGame.Core.Game
{
    /// <summary>판이 도는 동안 한 번씩 터지는 일.</summary>
    public enum StageEventKind : byte
    {
        None = 0,

        /// <summary>아직 안 탄 건물에 새 불이 붙는다.</summary>
        SecondIgnition = 1,

        /// <summary>많이 탄 건물의 문이 무너져 막힌다. 출구 쪽 문 하나는 남는다.</summary>
        Collapse = 2,
    }

    /// <summary>
    /// 진행 중 사건. <b>시드가 0이면 하나도 일어나지 않는다</b> — 첫 클리어는 언제나
    /// 설계된 판 그대로여야 필요 장비 표가 맞는다.
    ///
    /// 외운 순서를 무너뜨리는 것이 목적이므로 <b>예고가 있어야 한다</b> —
    /// 아무 경고 없이 등 뒤에서 불이 붙으면 변주가 아니라 사고다.
    /// </summary>
    public sealed class StageEvents
    {
        /// <summary>남은 시간이 제한 시간의 이 비율이 되면 2차 발화가 난다.</summary>
        public const float SecondIgnitionAt = 0.6f;

        /// <summary>2차 발화 예고 시간(초).</summary>
        public const float WarningSeconds = 3f;

        /// <summary>건물 가연 칸 중 이 비율 넘게 타 버리면 무너진다.</summary>
        public const float CollapseAt = 0.7f;

        private readonly bool _enabled;
        private Rng _rng;
        private bool _ignitionScheduled;
        private bool _ignitionDone;
        private readonly bool[] _collapsed;

        /// <summary>예고 중인 2차 발화 자리. 없으면 null.</summary>
        public GridPoint? PendingIgnition { get; private set; }

        /// <summary>예고 중인 2차 발화까지 남은 초. 예고가 없으면 0.</summary>
        public float SecondsUntilIgnition { get; private set; }

        /// <summary>이번 프레임에 일어난 사건. 화면이 읽고 다음 프레임에 비워진다.</summary>
        public StageEventKind JustHappened { get; private set; }

        /// <summary>이번 프레임 사건이 난 자리(2차 발화 칸, 또는 막힌 문 하나).</summary>
        public GridPoint JustHappenedAt { get; private set; }

        public StageEvents(int seed, int buildingCount)
        {
            _enabled = seed != 0;
            // 발화점 선정과 다른 수열을 쓰게 시드를 섞는다. 같은 시드면 여전히 같은 판이다.
            _rng = new Rng(unchecked((seed * 31) + 7));
            _collapsed = new bool[buildingCount];
        }

        public void Update(StageRunner runner)
        {
            JustHappened = StageEventKind.None;
            if (!_enabled) return;

            UpdateSecondIgnition(runner);
            UpdateCollapse(runner);
        }

        private void UpdateSecondIgnition(StageRunner runner)
        {
            if (_ignitionDone) return;

            float fireAt = runner.Def.TimeLimitSeconds * SecondIgnitionAt;

            if (!_ignitionScheduled)
            {
                if (runner.TimeLeft > fireAt + WarningSeconds) return;

                _ignitionScheduled = true;
                PendingIgnition = PickIgnition(runner);
                // 옮겨 붙을 건물이 없으면(주택처럼 한 채뿐이거나 전부 탔으면) 이 사건은 없다.
                if (PendingIgnition == null)
                {
                    _ignitionDone = true;
                    return;
                }
            }

            SecondsUntilIgnition = Math.Max(0f, runner.TimeLeft - fireAt);
            if (runner.TimeLeft > fireAt) return;

            GridPoint at = PendingIgnition.Value;
            ref Cell cell = ref runner.Grid[at.X, at.Y];
            // 예고 동안 물을 뿌려 두면 막을 수 있다 — 경고를 본 보람이 있어야 한다.
            if (cell.State == CellState.Intact && cell.Wet <= 0f) cell.State = CellState.Burning;

            _ignitionDone = true;
            PendingIgnition = null;
            SecondsUntilIgnition = 0f;
            JustHappened = StageEventKind.SecondIgnition;
            JustHappenedAt = at;
        }

        /// <summary>
        /// 불이 한 번도 닿지 않은 건물인지. "지금 안 타는 건물"로 고르면 애써 끈 건물이나
        /// 다 타 버린 건물에 다시 불이 붙는다 — 변주가 아니라 벌이다.
        /// </summary>
        private static bool Untouched(FireGrid grid, Building building)
        {
            foreach (GridPoint p in building.Cells)
            {
                CellState state = grid[p.X, p.Y].State;
                if (state == CellState.Burning || state == CellState.Burnt) return false;
                if (grid[p.X, p.Y].Wet > 0f) return false;
            }
            return true;
        }

        /// <summary>
        /// 불이 닿지 않은 건물의 나무 칸 하나. 시민 두 칸 안은 뺀다.
        /// 나무로만 고르는 이유: 기름·전기 불이 새로 나면 양동이만 든 판은 못 끈다.
        /// </summary>
        private GridPoint? PickIgnition(StageRunner runner)
        {
            var candidates = new List<GridPoint>();

            foreach (Building building in runner.Buildings.All)
            {
                if (!Untouched(runner.Grid, building)) continue;

                foreach (GridPoint p in building.Cells)
                {
                    Cell cell = runner.Grid[p.X, p.Y];
                    if (cell.Material != (byte)MaterialId.Wood || cell.State != CellState.Intact) continue;
                    if (runner.NearCivilian(p.X, p.Y)) continue;
                    candidates.Add(p);
                }
            }

            if (candidates.Count == 0) return null;
            return candidates[_rng.Next(candidates.Count)];
        }

        /// <summary>
        /// 많이 탄 건물은 문이 무너진다. 들어갔던 길이 막힐 수 있으므로
        /// 시민을 안에 두고 미루면 대가를 치른다.
        ///
        /// <b>출구에 가장 가까운 문 하나는 남긴다.</b> 전부 막으면 안에 남은 시민이 갇혀
        /// 판을 깰 수 없게 된다. 문이 하나뿐인 건물은 무너지지 않는다.
        /// </summary>
        private void UpdateCollapse(StageRunner runner)
        {
            foreach (Building building in runner.Buildings.All)
            {
                if (_collapsed[building.Id] || building.Entrances.Length < 2) continue;
                if (BurntRatio(runner.Grid, building) <= CollapseAt) continue;

                _collapsed[building.Id] = true;

                GridPoint keep = NearestToExit(building.Entrances, runner.Exits);
                foreach (GridPoint door in building.Entrances)
                {
                    if (door.Equals(keep)) continue;

                    ref Cell cell = ref runner.Grid[door.X, door.Y];
                    cell.Shut = true;
                    cell.Jammed = true;

                    JustHappened = StageEventKind.Collapse;
                    JustHappenedAt = door;
                }
            }
        }

        private static float BurntRatio(FireGrid grid, Building building)
        {
            int flammable = 0;
            int burnt = 0;
            foreach (GridPoint p in building.Cells)
            {
                Cell cell = grid[p.X, p.Y];
                if (!Materials.Of(cell.Material).Flammable) continue;
                flammable++;
                if (cell.State == CellState.Burnt) burnt++;
            }
            return flammable == 0 ? 0f : (float)burnt / flammable;
        }

        private static GridPoint NearestToExit(GridPoint[] doors, List<GridPoint> exits)
        {
            GridPoint best = doors[0];
            int bestDistance = int.MaxValue;

            foreach (GridPoint door in doors)
            {
                foreach (GridPoint exit in exits)
                {
                    int dx = door.X - exit.X;
                    int dy = door.Y - exit.Y;
                    int distance = (dx * dx) + (dy * dy);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = door;
                    }
                }
            }

            return best;
        }
    }
}
