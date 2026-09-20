namespace FireGame.Core.Sim
{
    /// <summary>
    /// 시뮬레이션 튜닝 상수를 한 곳에 모아둔다.
    /// 게임의 재미는 대부분 이 숫자들에서 결정되므로,
    /// 코드 곳곳에 흩어놓지 않고 여기서만 조정한다.
    /// </summary>
    public static class SimConfig
    {
        /// <summary>시뮬레이션 고정 틱 주파수(Hz). 프레임레이트와 무관하게 결정론을 보장한다.</summary>
        public const float TickRate = 10f;

        /// <summary>고정 틱 1회의 시간(초).</summary>
        public const float TickDelta = 1f / TickRate;

        /// <summary>매 틱 열이 감쇠하는 비율. 1에 가까울수록 불이 잘 번진다.</summary>
        public const float HeatDecay = 0.92f;

        /// <summary>초당 젖음이 마르는 양.</summary>
        public const float WetDecay = 0.01f;

        /// <summary>대각선 이웃으로의 열 전파 감쇠 계수.</summary>
        public const float DiagonalWeight = 0.7f;

        /// <summary>이 값을 넘는 젖음이 남아 있으면 재점화되지 않는다.</summary>
        public const float WetExtinguishThreshold = 0.2f;
    }
}
