using System.Collections.Generic;

namespace FireGame.Core.Render
{
    /// <summary>
    /// 8x8 비트맵 폰트.
    ///
    /// 글리프 하나를 ulong 하나에 담는다. 상위 바이트가 윗줄이고,
    /// 각 바이트의 최상위 비트가 왼쪽 픽셀이다.
    /// 폰트 파일이 따로 없으므로 에셋 없이 어디서나 같은 글자가 나온다.
    /// </summary>
    public static class Glyphs
    {
        public const int Width = 8;
        public const int Height = 8;

        private static readonly Dictionary<char, ulong> Bitmaps = new Dictionary<char, ulong>
        {
            { ' ', 0x0000000000000000UL },
            { '!', 0x3030303030003000UL },
            { '#', 0x4848FC48FC484800UL },
            { '$', 0x2078A07014F02000UL },
            { '%', 0xC4C81020408C8C00UL },
            { '\'', 0x3030102000000000UL },
            { '(', 0x1820404040201800UL },
            { ')', 0xC02010101020C000UL },
            { '*', 0x00925438FE385492UL },
            { '+', 0x002020F820200000UL },
            { ',', 0x0000000030301020UL },
            { '-', 0x000000FC00000000UL },
            { '.', 0x0000000000303000UL },
            { '/', 0x0408102040800000UL },
            { '0', 0x78848C94C4847800UL },
            { '1', 0x2060202020207000UL },
            { '2', 0x788404182040FC00UL },
            { '3', 0xFC08103008847800UL },
            { '4', 0x18284888FC081C00UL },
            { '5', 0xF880F80404847800UL },
            { '6', 0x384080F884847800UL },
            { '7', 0xFC84081020202000UL },
            { '8', 0x7884847884847800UL },
            { '9', 0x7884847C04087000UL },
            { ':', 0x0030300030300000UL },
            { '<', 0x0810204020100800UL },
            { '=', 0x0000FC00FC000000UL },
            { '>', 0x2010080408102000UL },
            { '?', 0x7884081020002000UL },
            { '@', 0x7884BCA4B8807800UL },
            { 'A', 0x30488484FC848400UL },
            { 'B', 0xF88484F88484F800UL },
            { 'C', 0x7884808080847800UL },
            { 'D', 0xF08884848488F000UL },
            { 'E', 0xFC8080F88080FC00UL },
            { 'F', 0xFC8080F880808000UL },
            { 'G', 0x7884809C84847800UL },
            { 'H', 0x848484FC84848400UL },
            { 'I', 0x7020202020207000UL },
            { 'J', 0x3C08080808887000UL },
            { 'K', 0x848890E090888400UL },
            { 'L', 0x808080808080FC00UL },
            { 'M', 0x82C6AA9282828200UL },
            { 'N', 0x84C4A4948C848400UL },
            { 'O', 0x7884848484847800UL },
            { 'P', 0xF88484F880808000UL },
            { 'Q', 0x7884848494887400UL },
            { 'R', 0xF88484F890888400UL },
            { 'S', 0x7884807804847800UL },
            { 'T', 0xFE10101010101000UL },
            { 'U', 0x8484848484847800UL },
            { 'V', 0x8484848448303000UL },
            { 'W', 0x82828292AAC68200UL },
            { 'X', 0x8448303030488400UL },
            { 'Y', 0x8448301010101000UL },
            { 'Z', 0xFC0408304080FC00UL },
            { '[', 0x3820202020203800UL },
            { ']', 0xE02020202020E000UL },
            { '^', 0x10386CAA38100000UL },
            { '~', 0x000062948C000000UL },
        };

        /// <summary>글리프 비트맵을 얻는다. 없는 문자는 공백으로 대체한다.</summary>
        public static ulong Get(char c)
        {
            if (c >= 'a' && c <= 'z') c = (char)(c - 'a' + 'A');

            return Bitmaps.TryGetValue(c, out ulong bitmap) ? bitmap : 0UL;
        }

        public static bool Has(char c)
        {
            if (c >= 'a' && c <= 'z') c = (char)(c - 'a' + 'A');
            return Bitmaps.ContainsKey(c);
        }

        /// <summary>글리프의 (x, y) 픽셀이 켜져 있는지.</summary>
        public static bool Pixel(ulong bitmap, int x, int y)
        {
            int row = (int)((bitmap >> (8 * (7 - y))) & 0xFF);
            return (row & (1 << (7 - x))) != 0;
        }
    }
}
