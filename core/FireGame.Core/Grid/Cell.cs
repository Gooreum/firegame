namespace FireGame.Core.Grid
{
    /// <summary>한 셀의 연소 상태.</summary>
    public enum CellState : byte
    {
        /// <summary>격자 밖이거나 의미 없는 셀.</summary>
        Empty = 0,

        /// <summary>아직 타지 않은 멀쩡한 상태.</summary>
        Intact = 1,

        /// <summary>연소 중.</summary>
        Burning = 2,

        /// <summary>연료를 다 써서 소실된 상태. 다시 탈 수 없다.</summary>
        Burnt = 3,
    }

    /// <summary>
    /// 격자 한 칸. 시뮬레이션이 매 틱 수십만 번 건드리므로
    /// 클래스가 아닌 struct로 두고 FireGrid의 배열에 연속 배치한다.
    /// </summary>
    public struct Cell
    {
        /// <summary>재질 id. <see cref="Materials.All"/> 의 인덱스.</summary>
        public byte Material;

        /// <summary>남은 연료 0..1. 0이 되면 Burnt로 전이한다.</summary>
        public float Fuel;

        /// <summary>누적된 열. 재질의 발화점을 넘으면 점화한다.</summary>
        public float Heat;

        /// <summary>
        /// 머금은 물 0..1. 남아 있는 동안은 재점화되지 않는다.
        /// 정산에서 수손 피해로 차감되므로 물 계열 약제만 이 값을 올린다.
        /// </summary>
        public float Wet;

        /// <summary>
        /// 약제로 덮여 일시적으로 점화되지 않는 시간(초).
        /// CO2처럼 젖음을 남기지 않는 약제도 잠깐은 재점화를 막아야 한다.
        /// 이게 없으면 군집 화재에서 한 칸을 꺼도 옆 칸이 즉시 되살려
        /// CO2 소화기가 아무 쓸모가 없어진다.
        /// </summary>
        public float Inert;

        public CellState State;
    }
}
