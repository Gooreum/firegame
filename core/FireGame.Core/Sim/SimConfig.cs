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

        /// <summary>
        /// 확산 속도 전역 계수.
        /// 재질 테이블의 HeatOutput은 재질 간 "상대" 비율(유류는 목재의 2배)만 담당하고,
        /// 게임 전체의 체감 속도는 이 값 하나로 조절한다.
        ///
        /// 0.5 기준 실측(무풍, 단일 발화원):
        ///   목재 직교 1.0초 / 대각 2.0초  → 불이 초당 1칸 전진
        ///   유류 직교 0.2초              → 초당 5칸. 플레이어(4칸/초)보다 빨라
        ///                                  쫓아가면 지고 차단선을 쳐야 한다
        ///   문   직교 2.0초              → 목재의 2배 저항. 임시 방화 장벽이 된다
        /// </summary>
        public const float SpreadScale = 0.5f;

        /// <summary>매 틱 열이 감쇠하는 비율. 1에 가까울수록 불이 잘 번진다.</summary>
        public const float HeatDecay = 0.92f;

        /// <summary>초당 젖음이 마르는 양.</summary>
        public const float WetDecay = 0.01f;

        /// <summary>대각선 이웃으로의 열 전파 감쇠 계수.</summary>
        public const float DiagonalWeight = 0.7f;

        /// <summary>
        /// 풍상 방향 전파 계수의 하한.
        /// 0에 가깝게 두면 바람 반대쪽이 완전 안전지대가 되어 플레이어가 무시하고 지나갈 수 있다.
        /// 0.6은 "풍상도 결국 번지지만 풍하보다 6배 이상 느리다"가 성립하는 값이다.
        /// </summary>
        public const float WindFactorMin = 0.6f;

        /// <summary>풍하 방향 전파 계수의 상한.</summary>
        public const float WindFactorMax = 2.0f;

        /// <summary>이 값을 넘는 젖음이 남아 있으면 재점화되지 않는다.</summary>
        public const float WetExtinguishThreshold = 0.2f;
    }
}
