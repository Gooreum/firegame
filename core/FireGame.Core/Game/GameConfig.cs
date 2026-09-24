namespace FireGame.Core.Game
{
    /// <summary>플레이어와 게임 루프의 튜닝 상수.</summary>
    public static class GameConfig
    {
        public const float PlayerMaxHp = 100f;

        /// <summary>초당 이동 칸 수. 목재 확산(1칸/초)의 4배라 우회와 측면 공격이 가능하다.</summary>
        public const float PlayerSpeed = 4f;

        /// <summary>불 위에 서 있을 때 초당 피해. 불 속은 여전히 치명적이다(약 6초).</summary>
        public const float FireDamageInCell = 16f;

        /// <summary>
        /// 인접 칸이 타고 있을 때 초당 피해.
        /// 회복이 없던 초기 설계(8/초)로는 한 판 전체에서 화염 옆에 머물 수 있는
        /// 시간이 12.5초뿐이라, 방화복을 입은 소방관이 불을 끄기도 전에 쓰러졌다.
        /// </summary>
        public const float FireDamageAdjacent = 5f;

        /// <summary>
        /// 피해를 받지 않은 채 이만큼 버티면 회복이 시작된다.
        /// 회복이 있어야 "치고 빠지기"가 성립하고, 없으면 한 판의 총 노출 시간이
        /// 고정 예산이 되어 실시간 액션이 아니라 자원 관리 게임이 된다.
        /// </summary>
        public const float RegenDelaySeconds = 3f;

        /// <summary>안전한 곳에서의 초당 회복량.</summary>
        public const float RegenPerSecond = 6f;

        /// <summary>시민을 업었을 때 이동 속도 배율.</summary>
        public const float CarrySpeedMultiplier = 0.6f;

        /// <summary>
        /// 등에 지고 나가는 물. 호스 기준 8초 연속 방수다.
        ///
        /// 이 값이 이 게임에서 <b>"무한히 쏘면 이긴다"를 끝내는 숫자</b>다.
        /// 너무 작으면 급수점 왕복만 하게 되고, 너무 크면 없는 것과 같다.
        /// </summary>
        public const float WaterTankMax = 240f;

        /// <summary>소화전·소방차 옆에서 초당 채우는 양. 가득 채우는 데 4초.</summary>
        public const float RefillPerSecond = 40f;

        /// <summary>급수를 받을 수 있는 거리(칸). 칸에 딱 올라서지 않아도 되게 넉넉히 잡았다.</summary>
        public const float RefillRadius = 1.6f;

        /// <summary>
        /// 연기 농도 1.0인 칸에서 초당 받는 피해. 방화복이 줄인다.
        /// 화염 인접(5/초)보다 조금 세다 — 불길은 피할 수 있어도 연기는 방을 다 채운다.
        /// </summary>
        public const float SmokeDamagePerSecond = 7f;

        /// <summary>
        /// 이 농도를 넘는 칸에서는 회복이 멈춘다.
        /// 회복이 무제한이던 것이 "물러났다 오면 언제나 풀피"를 만들었다 —
        /// 연기 속에서는 숨을 못 돌려야 건물 안이 실제로 위험해진다.
        /// </summary>
        public const float SmokeChokeThreshold = 0.3f;
    }
}
