namespace FireGame.Core.Sim
{
    /// <summary>
    /// 시드를 받는 선형 합동 생성기.
    ///
    /// System.Random을 쓰지 않는 이유는 런타임마다 구현이 달라질 수 있어서다 —
    /// .NET과 Unity(Mono/IL2CPP)에서 같은 시드가 다른 판을 만들면 안 된다.
    /// 같은 시드는 어디서 돌려도 언제나 같은 판이어야 한다.
    /// </summary>
    public struct Rng
    {
        private uint _state;

        public Rng(int seed)
        {
            // 상태 0에서도 LCG는 돌지만, 시드 0은 "변주 없음"이라는 뜻이라 여기까지 올 일이 없다.
            // 그래도 들어오면 1로 바꿔 다른 시드와 겹치지 않는 판을 만든다.
            _state = seed == 0 ? 1u : unchecked((uint)seed);
        }

        /// <summary>[0, maxExclusive) 범위의 정수. maxExclusive가 1 이하면 0.</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 1) return 0;

            // Numerical Recipes 계수. 하위 비트는 주기가 짧아서 위쪽 비트를 쓴다.
            _state = unchecked((_state * 1664525u) + 1013904223u);
            return (int)((_state >> 8) % (uint)maxExclusive);
        }
    }
}
