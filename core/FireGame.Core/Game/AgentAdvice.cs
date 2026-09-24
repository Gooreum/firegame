using FireGame.Core.Grid;
using FireGame.Core.Sim;

namespace FireGame.Core.Game
{
    /// <summary>약제를 그 칸에 썼을 때의 판정. 화면이 색을 고르는 데 쓴다.</summary>
    public enum AgentVerdict : byte
    {
        /// <summary>타지 않는 칸이거나 아무 효과가 없다.</summary>
        Useless = 0,

        /// <summary>듣긴 하지만 약하다.</summary>
        Weak = 1,

        /// <summary>잘 듣는다.</summary>
        Good = 2,

        /// <summary>역효과 — 불이 오히려 옆 칸으로 번진다.</summary>
        Backfire = 3,
    }

    /// <summary>
    /// "이 장비로 저 불을 끌 수 있나"를 화면이 물어보는 곳.
    ///
    /// 상성 규칙은 <see cref="Suppression"/>의 매트릭스 하나에만 있다.
    /// 여기서는 그 숫자를 색으로 옮길 수 있는 등급으로만 바꾼다 —
    /// 화면이 매트릭스를 따로 복사해 두면 규칙이 두 군데가 되어 반드시 어긋난다.
    /// </summary>
    public static class AgentAdvice
    {
        public static AgentVerdict For(AgentType agent, FireClass fireClass)
        {
            float effect = Suppression.EffectivenessOf(agent, fireClass);
            if (effect < 0f) return AgentVerdict.Backfire;
            if (effect == 0f) return AgentVerdict.Useless;
            return effect >= 1f ? AgentVerdict.Good : AgentVerdict.Weak;
        }

        /// <summary>격자 칸 기준. 타고 있지 않은 칸은 판정할 게 없으므로 늘 Useless다.</summary>
        public static AgentVerdict ForCell(FireGrid grid, int x, int y, AgentType agent)
        {
            if (grid == null || !grid.InBounds(x, y)) return AgentVerdict.Useless;

            ref Cell cell = ref grid[x, y];
            if (cell.State != CellState.Burning) return AgentVerdict.Useless;

            return For(agent, Materials.Of(cell.Material).Class);
        }

        /// <summary>"나무" / "기름" / "전기". 등급이 없으면 null.</summary>
        public static string ClassName(FireClass fireClass)
        {
            switch (fireClass)
            {
                case FireClass.A: return "나무";
                case FireClass.B: return "기름";
                case FireClass.C: return "전기";
                default: return null;
            }
        }

        /// <summary>이 약제가 잡는 화재 등급. HUD 슬롯이 "기름 불"을 적는 데 쓴다.</summary>
        public static FireClass BestClassFor(AgentType agent)
        {
            switch (agent)
            {
                case AgentType.Foam: return FireClass.B;
                case AgentType.CO2: return FireClass.C;
                default: return FireClass.A;
            }
        }
    }
}
