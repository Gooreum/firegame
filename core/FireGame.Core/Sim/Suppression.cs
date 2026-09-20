using FireGame.Core.Grid;

namespace FireGame.Core.Sim
{
    /// <summary>소화 약제 종류.</summary>
    public enum AgentType : byte
    {
        Water = 0,
        Foam = 1,
        CO2 = 2,
    }

    /// <summary>한 번의 방수/분사가 셀에 가하는 효과.</summary>
    public readonly struct Agent
    {
        public readonly AgentType Type;

        /// <summary>제거하는 열량.</summary>
        public readonly float Power;

        /// <summary>
        /// 부여하는 젖음. CO2는 기체라 0이다.
        /// 젖음은 오래 남아 재점화를 확실히 막지만 정산에서 수손 피해로 차감된다.
        /// "물은 확실하지만 돈이 깎이고, CO2는 깨끗하지만 보호가 짧다"는
        /// 선택이 여기서 생긴다.
        /// </summary>
        public readonly float Wetness;

        /// <summary>부여하는 불활성 시간(초). 수손 피해로 세지 않는다.</summary>
        public readonly float Inerting;

        public Agent(AgentType type, float power, float wetness, float inerting)
        {
            Type = type;
            Power = power;
            Wetness = wetness;
            Inerting = inerting;
        }
    }

    /// <summary>진압 시도 1회의 결과.</summary>
    public enum SuppressionOutcome : byte
    {
        /// <summary>격자 밖.</summary>
        OutOfBounds = 0,

        /// <summary>아무 일도 일어나지 않음.</summary>
        NoEffect = 1,

        /// <summary>열을 식혔지만 아직 꺼지지 않음.</summary>
        Cooled = 2,

        /// <summary>불이 꺼짐.</summary>
        Extinguished = 3,

        /// <summary>역효과. 불이 오히려 인접 셀로 번짐.</summary>
        Backfired = 4,
    }

    /// <summary>
    /// 소화 약제를 셀에 적용한다.
    ///
    /// 이 게임의 전술적 재미는 "더 강한 장비"가 아니라 "맞는 장비"를 고르는 데서 나온다.
    /// 그 규칙이 전부 <see cref="Effectiveness"/> 매트릭스 하나에 들어 있다.
    /// </summary>
    public static class Suppression
    {
        // [약제, 화재등급] — 열에 FireClass(None/A/B/C)가 순서대로 대응한다.
        // 음수는 역효과(불이 번짐), 0은 무효과를 뜻한다.
        private static readonly float[,] Effectiveness =
        {
            //         None    A      B      C
            /* Water */ { 1.0f,  1.0f, -1.0f, -0.5f },
            /* Foam  */ { 0.8f,  0.8f,  1.2f,  0.0f },
            /* CO2   */ { 0.5f,  0.5f,  0.6f,  1.2f },
        };

        // 역효과로 불이 번질 때 살펴보는 순서. 난수를 쓰면 결정론이 깨지므로
        // 항상 북 → 동 → 남 → 서 순으로 첫 유효 셀을 고른다.
        private static readonly int[] SplashDx = { 0, 1, 0, -1 };
        private static readonly int[] SplashDy = { -1, 0, 1, 0 };

        public static float EffectivenessOf(AgentType agent, FireClass fireClass)
        {
            return Effectiveness[(int)agent, (int)fireClass];
        }

        /// <summary>
        /// (x, y) 셀에 약제를 1회 적용한다.
        /// </summary>
        public static SuppressionOutcome Apply(FireGrid grid, int x, int y, in Agent agent)
        {
            if (grid == null || !grid.InBounds(x, y)) return SuppressionOutcome.OutOfBounds;

            ref Cell cell = ref grid[x, y];

            // 이미 다 타버린 셀에는 할 일이 없다.
            if (cell.State == CellState.Burnt) return SuppressionOutcome.NoEffect;

            CellMaterial material = Materials.Of(cell.Material);
            float effectiveness = EffectivenessOf(agent.Type, material.Class);

            if (effectiveness < 0f)
            {
                // 유류에 물을 뿌리는 경우. 타고 있을 때만 실제로 번진다.
                if (cell.State != CellState.Burning) return SuppressionOutcome.NoEffect;

                return SplashToNeighbor(grid, x, y)
                    ? SuppressionOutcome.Backfired
                    : SuppressionOutcome.NoEffect;
            }

            if (effectiveness == 0f) return SuppressionOutcome.NoEffect;

            cell.Heat -= agent.Power * effectiveness;

            if (agent.Wetness > 0f)
            {
                cell.Wet += agent.Wetness * effectiveness;
                if (cell.Wet > 1f) cell.Wet = 1f;
            }

            if (agent.Inerting > 0f && agent.Inerting > cell.Inert)
            {
                cell.Inert = agent.Inerting;
            }

            if (cell.State == CellState.Burning && cell.Heat <= 0f)
            {
                cell.Heat = 0f;
                cell.State = CellState.Intact;
                return SuppressionOutcome.Extinguished;
            }

            if (cell.Heat < 0f) cell.Heat = 0f;

            return SuppressionOutcome.Cooled;
        }

        /// <summary>
        /// 역효과로 인접 셀에 불을 옮긴다. 옮길 곳이 없으면 false.
        /// </summary>
        private static bool SplashToNeighbor(FireGrid grid, int x, int y)
        {
            for (int i = 0; i < 4; i++)
            {
                int nx = x + SplashDx[i];
                int ny = y + SplashDy[i];
                if (!grid.InBounds(nx, ny)) continue;

                ref Cell target = ref grid[nx, ny];
                if (target.State != CellState.Intact) continue;
                if (target.Wet > 0f) continue;
                if (target.Inert > 0f) continue;
                if (target.Fuel <= 0f) continue;

                CellMaterial targetMaterial = Materials.Of(target.Material);
                if (!targetMaterial.Flammable) continue;

                target.State = CellState.Burning;
                target.Heat = targetMaterial.Ignite;
                return true;
            }

            return false;
        }
    }
}
