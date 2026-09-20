namespace FireGame.Core.Render
{
    /// <summary>
    /// 320x200 팔레트 인덱스 프레임버퍼. 정확히 VGA 모드 13h 해상도다.
    ///
    /// 격자도 HUD도 상점 화면도 전부 이 버퍼 하나에 그린다.
    /// Unity 쪽에서는 이걸 Texture2D 한 장에 올려 스프라이트 하나로 그리므로
    /// 드로우콜 1회, 에셋 파일 0개로 끝난다. 모바일에서 사실상 공짜다.
    /// </summary>
    public sealed class FrameBuffer
    {
        public const int Width = 320;
        public const int Height = 200;

        /// <summary>격자가 쓰는 세로 픽셀 수. 아래 24픽셀은 HUD 몫이다.</summary>
        public const int PlayfieldHeight = 176;

        public const int HudHeight = Height - PlayfieldHeight;

        private readonly byte[] _pixels = new byte[Width * Height];

        /// <summary>팔레트 인덱스 배열. Unity가 여기서 바로 텍스처를 만든다.</summary>
        public byte[] Pixels
        {
            get { return _pixels; }
        }

        public void Clear(byte color = Palette.Black)
        {
            for (int i = 0; i < _pixels.Length; i++)
            {
                _pixels[i] = color;
            }
        }

        public byte Get(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return Palette.Black;

            return _pixels[(y * Width) + x];
        }

        /// <summary>범위 밖은 조용히 무시한다. 그리기 코드마다 경계 검사를 흩뿌리지 않기 위함.</summary>
        public void Set(int x, int y, byte color)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;

            _pixels[(y * Width) + x] = color;
        }

        public void FillRect(int x, int y, int width, int height, byte color)
        {
            int left = x < 0 ? 0 : x;
            int top = y < 0 ? 0 : y;
            int right = x + width > Width ? Width : x + width;
            int bottom = y + height > Height ? Height : y + height;

            for (int py = top; py < bottom; py++)
            {
                int rowStart = py * Width;
                for (int px = left; px < right; px++)
                {
                    _pixels[rowStart + px] = color;
                }
            }
        }

        /// <summary>
        /// 글리프 한 자를 그린다. <paramref name="background"/>가 음수면 배경을 비워둔다.
        /// </summary>
        public void DrawGlyph(int x, int y, char c, byte foreground, int background = -1)
        {
            ulong bitmap = Glyphs.Get(c);

            for (int gy = 0; gy < Glyphs.Height; gy++)
            {
                for (int gx = 0; gx < Glyphs.Width; gx++)
                {
                    if (Glyphs.Pixel(bitmap, gx, gy))
                    {
                        Set(x + gx, y + gy, foreground);
                    }
                    else if (background >= 0)
                    {
                        Set(x + gx, y + gy, (byte)background);
                    }
                }
            }
        }

        public void DrawText(int x, int y, string text, byte foreground, int background = -1)
        {
            if (text == null) return;

            for (int i = 0; i < text.Length; i++)
            {
                DrawGlyph(x + (i * Glyphs.Width), y, text[i], foreground, background);
            }
        }
    }
}
