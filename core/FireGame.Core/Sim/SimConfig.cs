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

        /// <summary>
        /// 초당 불활성 상태가 풀리는 양.
        /// 1.0이므로 <see cref="Suppression"/>의 Inerting 값이 곧 '초'가 된다.
        /// 단위를 어긋나게 두면 "2.5초 보호"라고 적어놓고 실제로는 6초가 보호되는 식으로
        /// 문서와 동작이 조용히 갈라진다.
        /// </summary>
        public const float InertDecay = 1.0f;

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

        /// <summary>
        /// 연소 셀이 자기 자신에게 되돌리는 열의 비율.
        /// 이게 없으면 모든 연소 셀의 열이 0 근처에 머물러
        /// 물 한 방울에 다 꺼져버린다. 이 값 덕분에
        /// "불이 클수록(이웃 화염이 많을수록) 끄기 어렵다"가 성립한다.
        ///
        /// 1.0 기준 단독 연소 시 유지 열량:
        ///   목재 0.625 / 유류 1.25 / 전기 0.375 / 문 0.5
        /// </summary>
        public const float SelfHeatFactor = 1.0f;

        /// <summary>
        /// 연소 칸이 초당 뿜는 연기.
        ///
        /// 실측(실내, 단독 연소 한 칸, 60초 평형):
        ///   맞닿은 칸 0.31 / 두 칸 0.20 / 네 칸 0.13
        /// 한 칸짜리 불이 앞을 못 보게 만들면 안 되고, 방 하나가 타면 막혀야 한다.
        /// 여섯 현장을 60초 태운 실측에서 진한 칸(0.6 초과)이 9~74칸 나온다.
        /// </summary>
        public const float SmokeOutput = 2.2f;

        /// <summary>
        /// 이웃으로 번지는 비율. 농도 차이에 곱한다.
        /// 통행 가능한 칸끼리만 오가므로 벽은 이 값과 무관하게 연기를 완전히 막는다.
        ///
        /// 0.55로 뒀을 때는 확산이 감쇠보다 느려 <b>연기가 불에서 세 칸도 못 갔다</b> —
        /// 방 안이 맑은 채로 남아 시야를 가릴 수가 없었다.
        /// 상한은 10이다(틱당 이웃 하나로 나가는 비율이 <c>SmokeSpread * TickDelta * 0.25</c>라
        /// 그 위로는 네 이웃 합이 원래 농도를 넘어 발산한다). 여유를 두고 8.
        /// </summary>
        public const float SmokeSpread = 8f;

        /// <summary>
        /// 실외 칸에서 초당 빠지는 비율. 밖은 연기가 고이지 않는다.
        /// 실내의 9배라 문 하나만 열어도 방이 눈에 띄게 걷힌다.
        /// </summary>
        public const float SmokeVent = 1.6f;

        /// <summary>
        /// 실내 자연 감쇠.
        /// 0에 가깝게 두면 한 번 찬 연기가 영영 안 빠져, 불을 다 끄고도
        /// 그 방에 두 번 다시 못 들어가는 잠긴 판이 된다.
        /// </summary>
        public const float SmokeDecay = 0.18f;
    }
}
