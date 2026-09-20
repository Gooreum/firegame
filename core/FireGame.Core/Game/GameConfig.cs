namespace FireGame.Core.Game
{
    /// <summary>플레이어와 게임 루프의 튜닝 상수.</summary>
    public static class GameConfig
    {
        public const float PlayerMaxHp = 100f;

        /// <summary>초당 이동 칸 수. 목재 확산(1칸/초)의 4배라 우회와 측면 공격이 가능하다.</summary>
        public const float PlayerSpeed = 4f;

        /// <summary>불 위에 서 있을 때 초당 피해. 100 HP 기준 약 6초 생존.</summary>
        public const float FireDamageInCell = 16f;

        /// <summary>인접 칸이 타고 있을 때 초당 피해. 약 12초 생존.</summary>
        public const float FireDamageAdjacent = 8f;

        /// <summary>시민을 업었을 때 이동 속도 배율.</summary>
        public const float CarrySpeedMultiplier = 0.6f;
    }
}
