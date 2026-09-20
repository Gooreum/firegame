namespace FireGame.Core.Render
{
    /// <summary>
    /// EGA 16색 팔레트. 도스 시절 표준 색이라 그대로 쓰면 그 시절 화면이 된다.
    /// UnityEngine에 의존하지 않도록 RGB를 바이트 배열로 들고 있고,
    /// Unity 쪽에서 Color32로 변환한다.
    /// </summary>
    public static class Palette
    {
        public const int ColorCount = 16;

        public const byte Black = 0;
        public const byte Blue = 1;
        public const byte Green = 2;
        public const byte Cyan = 3;
        public const byte Red = 4;
        public const byte Magenta = 5;
        public const byte Brown = 6;
        public const byte LightGray = 7;
        public const byte DarkGray = 8;
        public const byte LightBlue = 9;
        public const byte LightGreen = 10;
        public const byte LightCyan = 11;
        public const byte LightRed = 12;
        public const byte LightMagenta = 13;
        public const byte Yellow = 14;
        public const byte White = 15;

        /// <summary>색 인덱스 → RGB. 3바이트씩 16색.</summary>
        private static readonly byte[] Rgb =
        {
            0x00, 0x00, 0x00, // 0  검정
            0x00, 0x00, 0xAA, // 1  파랑
            0x00, 0xAA, 0x00, // 2  초록
            0x00, 0xAA, 0xAA, // 3  청록
            0xAA, 0x00, 0x00, // 4  빨강
            0xAA, 0x00, 0xAA, // 5  자홍
            0xAA, 0x55, 0x00, // 6  갈색
            0xAA, 0xAA, 0xAA, // 7  밝은 회색
            0x55, 0x55, 0x55, // 8  어두운 회색
            0x55, 0x55, 0xFF, // 9  밝은 파랑
            0x55, 0xFF, 0x55, // 10 밝은 초록
            0x55, 0xFF, 0xFF, // 11 밝은 청록
            0xFF, 0x55, 0x55, // 12 밝은 빨강
            0xFF, 0x55, 0xFF, // 13 밝은 자홍
            0xFF, 0xFF, 0x55, // 14 노랑
            0xFF, 0xFF, 0xFF, // 15 흰색
        };

        public static byte R(int colorIndex)
        {
            return Rgb[(colorIndex * 3) + 0];
        }

        public static byte G(int colorIndex)
        {
            return Rgb[(colorIndex * 3) + 1];
        }

        public static byte B(int colorIndex)
        {
            return Rgb[(colorIndex * 3) + 2];
        }
    }
}
