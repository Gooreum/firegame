using System;
using FireGame.Core.Grid;

namespace FireGame.Core.Sim
{
    /// <summary>
    /// 바람 설정. 방향은 정규화해서 보관하며, 세기 0이면 무풍이다.
    /// 스테이지마다 이 값만 바꿔도 난이도가 크게 달라지므로
    /// 사실상 게임의 난이도 다이얼 역할을 한다.
    /// </summary>
    public struct Wind
    {
        public float DirX;
        public float DirY;

        /// <summary>0 = 무풍. 클수록 풍하/풍상 격차가 커진다.</summary>
        public float Strength;

        public static Wind None
        {
            get { return new Wind { DirX = 0f, DirY = 0f, Strength = 0f }; }
        }

        /// <summary>방향 벡터를 정규화해서 바람을 만든다. 길이가 0이면 무풍 처리.</summary>
        public static Wind From(float dirX, float dirY, float strength)
        {
            float len = (float)Math.Sqrt((dirX * dirX) + (dirY * dirY));
            if (len <= 0f) return None;

            return new Wind { DirX = dirX / len, DirY = dirY / len, Strength = strength };
        }
    }

    /// <summary>
    /// 격자 기반 화재 확산 시뮬레이션.
    ///
    /// 물리 엔진이 아니라 셀룰러 오토마타다. 매 틱 각 연소 셀이 이웃에 열을 뿌리고,
    /// 누적된 열이 재질의 발화점을 넘으면 점화한다.
    /// 결정론이 보장되어야 테스트로 튜닝할 수 있으므로 난수를 쓰지 않는다.
    /// </summary>
    public sealed class FireSim
    {
        // 8방향 이웃: dx, dy, 거리 감쇠 가중치, 정규화된 방향(바람 내적용).
        private static readonly int[] NeighborDx = { 0, 0, -1, 1, -1, 1, -1, 1 };
        private static readonly int[] NeighborDy = { -1, 1, 0, 0, -1, -1, 1, 1 };
        private static readonly float[] NeighborWeight =
        {
            1f, 1f, 1f, 1f,
            SimConfig.DiagonalWeight, SimConfig.DiagonalWeight,
            SimConfig.DiagonalWeight, SimConfig.DiagonalWeight,
        };

        private const float Diag = 0.70710678f;
        private static readonly float[] NeighborNormX = { 0f, 0f, -1f, 1f, -Diag, Diag, -Diag, Diag };
        private static readonly float[] NeighborNormY = { -1f, 1f, 0f, 0f, -Diag, -Diag, Diag, Diag };

        private readonly FireGrid _grid;

        /// <summary>
        /// 이번 틱에 발생한 열 증가분을 따로 모은다.
        /// 격자에 곧바로 더하면 같은 틱 안에서 방금 붙은 불이 다시 번져
        /// 확산이 순회 순서에 의존하게 되고 결정론이 깨진다.
        /// </summary>
        private readonly float[] _heatDelta;

        public Wind Wind;

        public FireSim(FireGrid grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));

            _grid = grid;
            _heatDelta = new float[grid.Count];
            Wind = Wind.None;
        }

        public FireGrid Grid
        {
            get { return _grid; }
        }

        /// <summary>고정 틱 1회(=<see cref="SimConfig.TickDelta"/>초)를 진행한다.</summary>
        public void Tick()
        {
            Tick(SimConfig.TickDelta);
        }

        /// <summary>
        /// 시뮬레이션을 dt초만큼 진행한다.
        /// 프레임레이트 독립을 위해 호출자는 고정 틱으로 나눠서 호출해야 한다.
        /// </summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;

            Array.Clear(_heatDelta, 0, _heatDelta.Length);

            EmitHeatAndConsumeFuel(dt);
            ApplyHeatAndDecay(dt);
            Ignite();
        }

        /// <summary>1단계: 연소 셀이 이웃에 열을 뿌리고 자기 연료를 태운다.</summary>
        private void EmitHeatAndConsumeFuel(float dt)
        {
            Cell[] cells = _grid.Cells;

            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    int index = _grid.Index(x, y);
                    if (cells[index].State != CellState.Burning) continue;

                    CellMaterial material = Materials.Of(cells[index].Material);
                    float output = material.HeatOutput * SimConfig.SpreadScale * dt;

                    for (int n = 0; n < 8; n++)
                    {
                        int nx = x + NeighborDx[n];
                        int ny = y + NeighborDy[n];
                        if (!_grid.InBounds(nx, ny)) continue;

                        _heatDelta[_grid.Index(nx, ny)] += output * NeighborWeight[n] * WindFactor(n);
                    }

                    // 열을 뿌린 뒤 연료를 소모한다. 소진되면 이번 틱까지만 태우고 소실된다.
                    cells[index].Fuel -= material.BurnRate * dt;
                    if (cells[index].Fuel <= 0f)
                    {
                        cells[index].Fuel = 0f;
                        cells[index].State = CellState.Burnt;
                    }
                }
            }
        }

        /// <summary>
        /// 이웃 방향 n으로의 바람 보정 계수.
        /// 바람과 같은 방향이면 증폭, 반대면 감쇠한다.
        /// </summary>
        private float WindFactor(int n)
        {
            if (Wind.Strength <= 0f) return 1f;

            float dot = (Wind.DirX * NeighborNormX[n]) + (Wind.DirY * NeighborNormY[n]);
            float factor = 1f + (dot * Wind.Strength);

            if (factor < SimConfig.WindFactorMin) return SimConfig.WindFactorMin;
            if (factor > SimConfig.WindFactorMax) return SimConfig.WindFactorMax;
            return factor;
        }

        /// <summary>2단계: 기존 열을 감쇠시키고 이번 틱 증가분을 반영한다. 젖음도 마른다.</summary>
        private void ApplyHeatAndDecay(float dt)
        {
            Cell[] cells = _grid.Cells;

            for (int i = 0; i < cells.Length; i++)
            {
                // 감쇠를 먼저 적용해야 이번 틱에 새로 들어온 열이 즉시 깎이지 않는다.
                cells[i].Heat = (cells[i].Heat * SimConfig.HeatDecay) + _heatDelta[i];

                if (cells[i].Wet > 0f)
                {
                    cells[i].Wet -= SimConfig.WetDecay * dt;
                    if (cells[i].Wet < 0f) cells[i].Wet = 0f;
                }
            }
        }

        /// <summary>3단계: 발화 판정. 이번 틱에 붙은 불은 다음 틱부터 열을 뿌린다.</summary>
        private void Ignite()
        {
            Cell[] cells = _grid.Cells;

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].State != CellState.Intact) continue;
                if (cells[i].Wet > 0f) continue;
                if (cells[i].Fuel <= 0f) continue;

                CellMaterial material = Materials.Of(cells[i].Material);
                if (!material.Flammable) continue;

                if (cells[i].Heat > material.Ignite)
                {
                    cells[i].State = CellState.Burning;
                }
            }
        }
    }
}
