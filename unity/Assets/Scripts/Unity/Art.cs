using System.Collections.Generic;
using UnityEngine;

namespace FireGame.UnityLayer
{
    /// <summary>벽 테두리 색 계열.</summary>
    public enum WallStyle
    {
        /// <summary>나무벽 — 주황 테두리.</summary>
        Wood = 0,

        /// <summary>콘크리트 — 청회색 테두리.</summary>
        Concrete = 1,
    }

    /// <summary>
    /// 그림과 글꼴을 한 곳에서 불러온다.
    ///
    /// Resources 경로 문자열이 코드 곳곳에 흩어지면 파일 이름 하나 바꿀 때 어디가 깨지는지 알 수 없다.
    /// 또 없는 그림을 조용히 null로 넘기면 화면에 아무것도 안 그려져 원인을 찾기 어려우므로,
    /// 여기서 한 번에 경고를 남기고 눈에 띄는 대체 그림(자홍색 네모)을 돌려준다.
    /// </summary>
    public static class Art
    {
        /// <summary>타일 한 칸 = 64픽셀 = 월드 1단위.</summary>
        public const float PixelsPerUnit = 64f;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<int, Sprite> Walls = new Dictionary<int, Sprite>();
        private static Sprite _white;
        private static Sprite _missing;
        private static Font _font;

        public static Sprite Get(string path)
        {
            // 씬이 바뀌며 Unity가 안 쓰는 에셋을 내리면 C# 참조는 남고 실제 객체는 파괴된다.
            // Unity의 == null은 파괴된 객체도 null로 보므로, 그때는 다시 불러온다.
            if (Cache.TryGetValue(path, out Sprite sprite) && sprite != null) return sprite;

            sprite = Resources.Load<Sprite>("Art/" + path);
            if (sprite == null)
            {
                Debug.LogError("[Art] 그림을 찾지 못했다: Art/" + path);
                sprite = Missing;
            }

            Cache[path] = sprite;
            return sprite;
        }

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.Load<Font>("Fonts/Jua-Regular");
                if (_font == null) Debug.LogError("[Art] 주아체를 찾지 못했다: Fonts/Jua-Regular");
                return _font;
            }
        }

        /// <summary>색을 입혀 덮개·막대로 쓰는 흰 네모.</summary>
        public static Sprite White
        {
            get
            {
                if (_white == null) _white = Solid(new Color32(255, 255, 255, 255));
                return _white;
            }
        }

        private static Sprite Missing
        {
            get
            {
                if (_missing == null) _missing = Solid(new Color32(255, 0, 255, 255));
                return _missing;
            }
        }

        private static Sprite Solid(Color32 color)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels32(pixels);
            texture.Apply(false);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            KeepAlive(texture, sprite);
            return sprite;
        }

        /// <summary>
        /// 코드로 만든 그림은 어떤 씬도 참조하지 않아서, 씬이 바뀔 때 Unity가 "안 쓰는 에셋"으로 보고 내린다.
        /// 캐시에서 꺼냈는데 이미 파괴돼 있으면 그 칸이 투명해진다(벽이 통째로 사라지는 버그로 확인됨).
        /// </summary>
        private static void KeepAlive(Texture2D texture, Sprite sprite)
        {
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        }

        /// <summary>그림이 월드에서 가로 <paramref name="cells"/>칸을 차지하게 하는 배율.</summary>
        public static float FitWidth(Sprite sprite, float cells)
        {
            float width = sprite.bounds.size.x;
            return width > 0f ? cells / width : 1f;
        }

        // ------------------------------------------------------------------
        // 벽
        // ------------------------------------------------------------------

        public const int ConnectNorth = 1;
        public const int ConnectEast = 2;
        public const int ConnectSouth = 4;
        public const int ConnectWest = 8;

        /// <summary>
        /// 이웃 벽과의 연결 모양(<paramref name="mask"/>)에 맞는 벽 조각.
        ///
        /// Top-down Shooter 팩의 벽은 연결 모양(가로·세로·모서리·T자·십자)마다 조각이 따로 있어
        /// 번호로 일일이 맞추면 틀리기 쉽다. 같은 색과 두께로 조각을 직접 그린다:
        /// 어두운 면 위에, 이웃 벽이 없는 쪽에만 테두리를 두른다.
        /// </summary>
        public static Sprite Wall(WallStyle style, int mask)
        {
            int key = ((int)style << 4) | (mask & 15);
            if (Walls.TryGetValue(key, out Sprite sprite) && sprite != null) return sprite;

            // 팩 벽 타일(tile_109, tile_280)에서 뽑은 색
            Color32 face = new Color32(74, 74, 74, 255);
            Color32 inner = new Color32(86, 86, 86, 255);
            Color32 trim = style == WallStyle.Wood ? new Color32(232, 106, 23, 255) : new Color32(166, 201, 203, 255);
            Color32 shadow = style == WallStyle.Wood ? new Color32(166, 74, 15, 255) : new Color32(100, 133, 135, 255);

            const int size = 64;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 연결되지 않은 변까지의 거리. 연결된 변은 무한히 먼 것으로 친다.
                    int distance = int.MaxValue;
                    if ((mask & ConnectNorth) == 0) distance = Mathf.Min(distance, size - 1 - y);
                    if ((mask & ConnectSouth) == 0) distance = Mathf.Min(distance, y);
                    if ((mask & ConnectEast) == 0) distance = Mathf.Min(distance, size - 1 - x);
                    if ((mask & ConnectWest) == 0) distance = Mathf.Min(distance, x);

                    Color32 color;
                    if (distance < 3) color = shadow;
                    else if (distance < 12) color = trim;
                    else if (distance < 17) color = inner;
                    else color = face;

                    pixels[(y * size) + x] = color;
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false);

            sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            KeepAlive(texture, sprite);
            Walls[key] = sprite;
            return sprite;
        }
    }
}
