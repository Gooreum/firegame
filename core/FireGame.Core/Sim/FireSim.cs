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

        /// <summary>
        /// 연기 증가분. <see cref="_heatDelta"/>와 같은 이유로 따로 모은다 —
        /// 격자에 곧바로 더하면 방금 흘러든 연기가 같은 틱에 또 흘러
        /// 확산이 순회 순서에 의존하게 되고 결정론이 깨진다.
        /// </summary>
        private readonly float[] _smokeDelta;

        /// <summary>
        /// 칸별 실외 여부. 실외는 연기가 고이지 않고 빠져나간다.
        /// null이면 전부 실외로 본다 — 건물 개념이 없는 격자 단위 테스트를 위한 것이다.
        /// </summary>
        public bool[] Outdoor;

        public Wind Wind;

        /// <summary>
        /// 화재 규모. 타는 칸이 스스로 유지하는 열에만 곱한다.
        /// 이웃에게 주는 열은 그대로라 번지는 속도는 같고, 한 칸을 끄는 데 드는 약제만 늘어난다.
        /// </summary>
        public float Intensity = 1f;

        public FireSim(FireGrid grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));

            _grid = grid;
            _heatDelta = new float[grid.Count];
            _smokeDelta = new float[grid.Count];
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
            SpreadSmoke(dt);
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

                    // 열린 문이 옆에 있으면 바람이 들어 불이 자란다.
                    // 문을 열어 연기를 빼는 대가가 여기서 나온다.
                    if (NextToOpenDoor(x, y)) output *= SimConfig.DraftBoost;

                    // 자기 자신도 데운다. 이웃에게 주는 열에는 영향이 없으므로
                    // 확산 타이밍은 그대로이고, 대신 연소 셀이 고유의 열량을 유지해
                    // 진압에 필요한 방수량이 불의 규모에 비례하게 된다.
                    _heatDelta[index] += output * SimConfig.SelfHeatFactor * Intensity;

                    for (int n = 0; n < 8; n++)
                    {
                        int nx = x + NeighborDx[n];
                        int ny = y + NeighborDy[n];
                        if (!_grid.InBounds(nx, ny)) continue;

                        int target = _grid.Index(nx, ny);
                        float through = cells[target].Shut ? SimConfig.ShutDoorHeat : 1f;

                        _heatDelta[target] += output * NeighborWeight[n] * WindFactor(n) * through;
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
        /// 직교로 맞닿은 칸에 열린 문이 있는지. 닫힌 문은 세지 않는다 —
        /// 닫아야 바람이 끊긴다는 것이 문을 닫는 이유의 절반이다.
        /// </summary>
        private bool NextToOpenDoor(int x, int y)
        {
            for (int n = 0; n < 4; n++)
            {
                int nx = x + NeighborDx[n];
                int ny = y + NeighborDy[n];
                if (!_grid.InBounds(nx, ny)) continue;

                ref Cell neighbor = ref _grid[nx, ny];
                if (neighbor.Material == (byte)MaterialId.Door && !neighbor.Shut) return true;
            }

            return false;
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

                if (cells[i].Inert > 0f)
                {
                    cells[i].Inert -= SimConfig.InertDecay * dt;
                    if (cells[i].Inert < 0f) cells[i].Inert = 0f;
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
                if (cells[i].Inert > 0f) continue;
                if (cells[i].Fuel <= 0f) continue;

                CellMaterial material = Materials.Of(cells[i].Material);
                if (!material.Flammable) continue;

                if (cells[i].Heat > material.Ignite)
                {
                    cells[i].State = CellState.Burning;
                }
            }
        }

        /// <summary>
        /// 4단계: 연기. 열과 달리 <b>통행 가능한 칸끼리만</b> 오간다 —
        /// 연기는 벽을 뚫지 않는다. 이 한 줄이 "문을 닫으면 방이 지켜진다"를 성립시킨다.
        ///
        /// 대각선으로는 번지지 않는다. 벽 모서리가 맞닿은 두 방 사이로 연기가 새면
        /// 플레이어가 막을 방법이 없는 경로가 생긴다.
        /// </summary>
        private void SpreadSmoke(float dt)
        {
            Array.Clear(_smokeDelta, 0, _smokeDelta.Length);

            Cell[] cells = _grid.Cells;

            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    int index = _grid.Index(x, y);

                    if (cells[index].State == CellState.Burning)
                    {
                        EmitSmoke(x, y, index, SimConfig.SmokeOutput * dt);
                    }

                    if (!Passable(index)) continue;

                    float here = cells[index].Smoke;
                    if (here <= 0f) continue;

                    // 직교 이웃만. NeighborDx/Dy의 앞 네 자리가 직교다.
                    for (int n = 0; n < 4; n++)
                    {
                        int nx = x + NeighborDx[n];
                        int ny = y + NeighborDy[n];
                        if (!_grid.InBounds(nx, ny)) continue;

                        int neighbor = _grid.Index(nx, ny);
                        if (!Passable(neighbor)) continue;

                        // 농도가 높은 쪽에서 낮은 쪽으로만 보낸다. 반대 방향은
                        // 그 이웃을 순회할 때 제 손으로 보내므로 여기서 또 하면 두 배가 된다.
                        float flow = (here - cells[neighbor].Smoke) * SimConfig.SmokeSpread * dt * 0.25f;
                        if (flow <= 0f) continue;

                        _smokeDelta[index] -= flow;
                        _smokeDelta[neighbor] += flow;
                    }
                }
            }

            for (int i = 0; i < cells.Length; i++)
            {
                float decay = Outdoor == null || Outdoor[i] ? SimConfig.SmokeVent : SimConfig.SmokeDecay;

                float value = (cells[i].Smoke + _smokeDelta[i]) * (1f - (decay * dt));
                if (value < 0f) value = 0f;
                else if (value > 1f) value = 1f;

                cells[i].Smoke = value;
            }
        }

        /// <summary>
        /// 연소 칸이 뿜은 연기를 어디에 놓을지 정한다.
        ///
        /// 이 게임에서 타는 것은 대부분 <b>벽</b>(목재 선반·칸막이)이고 벽은 통행 불가다.
        /// 연기를 타는 칸 제자리에 쌓으면 갇혀서 방으로 한 톨도 안 나온다 —
        /// 불타는 창고 한가운데가 맑은 채로 남는다.
        /// 그래서 서 있을 수 없는 칸에서 난 연기는 <b>맞닿은 통행 가능한 칸으로 흘려보낸다</b>.
        /// 나갈 곳이 아예 없으면(벽 속에 박힌 칸) 버린다 — 아무도 그 안에 못 들어간다.
        /// </summary>
        private void EmitSmoke(int x, int y, int index, float amount)
        {
            if (Passable(index))
            {
                _smokeDelta[index] += amount;
                return;
            }

            int outlets = 0;
            for (int n = 0; n < 4; n++)
            {
                int nx = x + NeighborDx[n];
                int ny = y + NeighborDy[n];
                if (!_grid.InBounds(nx, ny)) continue;
                if (Passable(_grid.Index(nx, ny))) outlets++;
            }

            if (outlets == 0) return;

            float share = amount / outlets;
            for (int n = 0; n < 4; n++)
            {
                int nx = x + NeighborDx[n];
                int ny = y + NeighborDy[n];
                if (!_grid.InBounds(nx, ny)) continue;

                int neighbor = _grid.Index(nx, ny);
                if (Passable(neighbor)) _smokeDelta[neighbor] += share;
            }
        }

        /// <summary>
        /// 연기가 지나갈 수 있는 칸인지. 벽과 설비는 막고, 닫힌 문도 막는다.
        /// 문 한 장이 방화 장벽이 되는 지점이 이 한 줄이다.
        /// </summary>
        private bool Passable(int index)
        {
            return Materials.Of(_grid.Cells[index].Material).Walkable && !_grid.Cells[index].Shut;
        }
    }
}
