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
    }
}
