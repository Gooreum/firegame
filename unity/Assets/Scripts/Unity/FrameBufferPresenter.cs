using FireGame.Core.Render;
using UnityEngine;

namespace FireGame.UnityLayer
{
    /// <summary>
    /// 팔레트 인덱스 프레임버퍼를 Texture2D 한 장에 올려 스프라이트로 보여준다.
    /// 게임 화면 전체가 이 스프라이트 하나라 드로우콜은 1회다.
    /// </summary>
    public sealed class FrameBufferPresenter
    {
        /// <summary>스프라이트 1 월드 단위에 들어가는 픽셀 수. ScreenLayout.OrthographicSize와 맞춰야 한다.</summary>
        public const float PixelsPerUnit = 100f;

        private readonly Texture2D _texture;
        private readonly Color32[] _pixels = new Color32[FrameBuffer.Width * FrameBuffer.Height];
        private readonly Color32[] _palette = new Color32[Palette.ColorCount];

        public FrameBufferPresenter(SpriteRenderer target)
        {
            // 도트가 뭉개지지 않게 점 필터링. 밉맵도 쓰지 않는다.
            _texture = new Texture2D(FrameBuffer.Width, FrameBuffer.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            for (int i = 0; i < Palette.ColorCount; i++)
            {
                _palette[i] = new Color32(Palette.R(i), Palette.G(i), Palette.B(i), 255);
            }

            target.sprite = Sprite.Create(
                _texture,
                new Rect(0, 0, FrameBuffer.Width, FrameBuffer.Height),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit);
        }

        public void Present(FrameBuffer buffer)
        {
            byte[] source = buffer.Pixels;

            // Unity 텍스처는 아랫줄부터 채우므로 위아래를 뒤집어 옮긴다.
            for (int y = 0; y < FrameBuffer.Height; y++)
            {
                int sourceRow = y * FrameBuffer.Width;
                int targetRow = (FrameBuffer.Height - 1 - y) * FrameBuffer.Width;

                for (int x = 0; x < FrameBuffer.Width; x++)
                {
                    _pixels[targetRow + x] = _palette[source[sourceRow + x]];
                }
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
        }
    }
}
