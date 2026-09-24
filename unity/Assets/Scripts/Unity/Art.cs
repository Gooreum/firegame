using System.Collections.Generic;
using UnityEngine;

namespace FireGame.UnityLayer
{
    /// <summary>
    /// 지붕 재질. 결이 다르면 같은 색이라도 다른 건물로 읽힌다.
    /// </summary>
    public enum RoofStyle
    {
        /// <summary>기와 — 가로 단이 한 단씩 엇갈린다. 주택.</summary>
        Tile = 0,

        /// <summary>차양 — 굵은 세로 줄무늬. 상가.</summary>
        Awning = 1,

        /// <summary>캐노피 — 큰 패널 이음매만 있는 평지붕. 주유소.</summary>
        Canopy = 2,

        /// <summary>함석 — 세로 골이 촘촘하다. 창고.</summary>
        Metal = 3,

        /// <summary>슬레이트 — 리벳 박힌 금속 패널. 공장.</summary>
        Panel = 4,

        /// <summary>널판 — 가로 판자에 나뭇결. 항구.</summary>
        Plank = 5,
    }

    /// <summary>실외 바닥. 현장이 어디인지를 가장 넓은 면적으로 말한다.</summary>
    public enum GroundStyle
    {
        /// <summary>잔디 — 주택가 마당.</summary>
        Lawn = 0,

        /// <summary>보도블록 — 상가 앞 광장.</summary>
        Paving = 1,

        /// <summary>아스팔트 + 차선 도색 — 주유소.</summary>
        Asphalt = 2,

        /// <summary>야적장 — 이음매와 하역 표시가 있는 콘크리트. 창고.</summary>
        Yard = 3,

        /// <summary>공장 콘크리트 — 금 가고 위험 줄무늬가 있다.</summary>
        Concrete = 4,

        /// <summary>부두 널판 — 이음매 사이로 물이 비친다. 항구.</summary>
        Dock = 5,
    }

    /// <summary>
    /// 켄니 팩에 없어서 코드로 찍는 현장 소품.
    /// 나무·드럼통·타이어·차단봉·차량은 Racing Pack에 있으니 여기 넣지 않는다.
    /// </summary>
    public enum PropStyle
    {
        /// <summary>주유기 섬 — 낮은 단 위에 기계 두 대. 주유소.</summary>
        Pump = 0,

        /// <summary>해상 컨테이너 — 세로 골이 진 상자. 창고·항구.</summary>
        Container = 1,

        /// <summary>팔레트 더미 — 각재를 엇갈려 쌓았다. 창고.</summary>
        Pallet = 2,

        /// <summary>화단 — 벽돌 턱 안의 관목. 상가.</summary>
        Planter = 3,

        /// <summary>계선주 — 밧줄을 감는 쇠기둥. 항구.</summary>
        Bollard = 4,
    }

    /// <summary>맵 가장자리. 격자 맨 바깥 한 줄이라 게임에 영향이 없다.</summary>
    public enum BorderStyle
    {
        /// <summary>생울타리 — 주택가.</summary>
        Hedge = 0,

        /// <summary>화단 담 — 상가.</summary>
        Planter = 1,

        /// <summary>가드레일 — 주유소.</summary>
        Guardrail = 2,

        /// <summary>철망 — 창고·공장.</summary>
        Fence = 3,

        /// <summary>방파제 — 항구. 이것 하나로 맵을 안 고치고 물가가 생긴다.</summary>
        Seawall = 4,
    }

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
        private static readonly Dictionary<int, Sprite> Generated = new Dictionary<int, Sprite>();
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

        // ------------------------------------------------------------------
        // 지붕·바닥·테두리 — 코드로 찍는다
        //
        // 켄니 팩에는 건물 외형 그림이 없다. 짙은 brick_a에 현장 색을 곱했더니
        // 여섯 현장 지붕이 전부 회색으로 죽었다(캡처로 확인). 그래서 Wall()과 같은 방식으로
        // 결을 직접 그린다. 밝기 1에 가까운 회백색 바탕에 어두운 줄만 넣어야
        // 색을 곱했을 때 현장 색이 살아남는다.
        // ------------------------------------------------------------------

        private const int GenSize = 64;

        /// <summary>지붕 재질 한 장. 색은 입히는 쪽에서 곱한다.</summary>
        public static Sprite RoofTexture(RoofStyle style)
        {
            return Make(('R' << 16) | (int)style, (x, y) => Roof(style, x, y));
        }

        /// <summary>실외 바닥 한 장. <paramref name="variant"/> 0~3을 섞어 깔아 넓은 마당을 덜 심심하게 한다.</summary>
        public static Sprite GroundTexture(GroundStyle style, int variant)
        {
            int v = ((variant % 4) + 4) % 4;
            return Make(('G' << 16) | ((int)style << 4) | v, (x, y) => Ground(style, v, x, y));
        }

        /// <summary>맵 가장자리 한 장.</summary>
        public static Sprite BorderTexture(BorderStyle style)
        {
            return Make(('B' << 16) | (int)style, (x, y) => Border(style, x, y));
        }

        /// <summary>켄니 팩에 없는 현장 소품 한 장. 투명 배경이라 마당 위에 얹힌다.</summary>
        public static Sprite PropTexture(PropStyle style)
        {
            return Make(('P' << 16) | (int)style, (x, y) => Prop(style, x, y));
        }

        private static Sprite Make(int key, System.Func<int, int, Color32> paint)
        {
            if (Generated.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            var pixels = new Color32[GenSize * GenSize];
            for (int y = 0; y < GenSize; y++)
            {
                for (int x = 0; x < GenSize; x++) pixels[(y * GenSize) + x] = paint(x, y);
            }

            var texture = new Texture2D(GenSize, GenSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false);

            Sprite sprite = Sprite.Create(
                texture, new Rect(0, 0, GenSize, GenSize), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            KeepAlive(texture, sprite);
            Generated[key] = sprite;
            return sprite;
        }

        /// <summary>회색 한 단계. 1이 흰색이다.</summary>
        private static Color32 Grey(float level)
        {
            byte v = (byte)Mathf.Clamp(Mathf.RoundToInt(level * 255f), 0, 255);
            return new Color32(v, v, v, 255);
        }

        private static Color32 Rgb(float r, float g, float b)
        {
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(b * 255f), 0, 255),
                255);
        }

        /// <summary>같은 자리는 늘 같은 값. 얼룩을 흩뿌리는 데 쓴다.</summary>
        private static float Noise(int x, int y, int salt)
        {
            int h = ((x * 73856093) ^ (y * 19349663) ^ (salt * 83492791)) & 0x7fffffff;
            return (h % 1000) / 1000f;
        }

        private static Color32 Roof(RoofStyle style, int x, int y)
        {
            switch (style)
            {
                case RoofStyle.Tile:
                {
                    // 16px 가로 단. 한 단씩 엇갈려 기와처럼 보이게 한다.
                    int course = y / 16;
                    int shift = (course % 2) * 8;
                    if (y % 16 < 2) return Grey(0.64f);                 // 단이 겹치는 그늘
                    if (((x + shift) % 16) < 2) return Grey(0.79f);      // 기와 사이 골
                    return Grey(y % 16 < 5 ? 1f : 0.95f);               // 단 위쪽이 살짝 밝다
                }

                case RoofStyle.Awning:
                {
                    // 굵은 세로 줄무늬. 상가 차양은 이것만으로 알아본다.
                    return Grey((x / 16) % 2 == 0 ? 1f : 0.74f);
                }

                case RoofStyle.Canopy:
                {
                    // 거의 평평한 금속판. 큰 이음매만.
                    if (x % 32 < 2 || y % 32 < 2) return Grey(0.78f);
                    return Grey(0.97f);
                }

                case RoofStyle.Metal:
                {
                    // 8px 세로 골. 골 바닥이 어둡고 마루가 밝다.
                    int c = x % 8;
                    if (c == 0) return Grey(0.58f);
                    if (c == 4) return Grey(1f);
                    return Grey(c < 4 ? 0.82f : 0.90f);
                }

                case RoofStyle.Panel:
                {
                    // 32x16 패널 + 리벳.
                    if (x % 32 < 2 || y % 16 < 2) return Grey(0.68f);
                    int rx = x % 32;
                    int ry = y % 16;
                    if ((rx == 6 || rx == 26) && (ry == 5 || ry == 12)) return Grey(0.56f);
                    return Grey(0.95f);
                }

                default:
                {
                    // 12px 가로 판자 + 나뭇결.
                    if (y % 12 < 1) return Grey(0.60f);
                    if (Noise(x, y / 12, 7) < 0.10f) return Grey(0.80f);
                    return Grey(y % 12 < 4 ? 0.98f : 0.92f);
                }
            }
        }

        private static Color32 Ground(GroundStyle style, int variant, int x, int y)
        {
            switch (style)
            {
                case GroundStyle.Lawn:
                {
                    float n = Noise(x / 3, y / 3, variant);
                    Color32 turf = Rgb(0.29f + (n * 0.07f), 0.58f + (n * 0.10f), 0.30f + (n * 0.06f));
                    // 한 변형은 흙이 드러난 오솔길이다.
                    if (variant == 3 && Mathf.Abs(x - 32) < 10) return Rgb(0.62f, 0.53f, 0.38f);
                    if (Noise(x, y, variant + 5) < 0.04f) return Rgb(0.24f, 0.50f, 0.26f);
                    return turf;
                }

                case GroundStyle.Paving:
                {
                    // 32x32 보도블록. 줄눈이 보인다.
                    bool joint = (x % 32) < 2 || (y % 32) < 2;
                    float n = Noise(x / 32, y / 32, variant) * 0.05f;
                    if (joint) return Rgb(0.62f, 0.61f, 0.60f);
                    return Rgb(0.78f + n, 0.77f + n, 0.75f + n);
                }

                case GroundStyle.Asphalt:
                {
                    float n = Noise(x, y, variant) * 0.06f;
                    Color32 road = Rgb(0.26f + n, 0.26f + n, 0.28f + n);
                    // 변형 2에만 차선 도색을 넣는다. 4칸 중 1칸이라 마당에 도로가 흐른다.
                    if (variant == 2 && y > 26 && y < 38) return Rgb(0.92f, 0.84f, 0.28f);
                    if (variant == 3 && Noise(x / 8, y / 8, 11) < 0.12f) return Rgb(0.33f, 0.33f, 0.34f);
                    return road;
                }

                case GroundStyle.Yard:
                {
                    bool joint = (y % 32) < 2 || (x % 64) < 2;
                    float n = Noise(x / 4, y / 4, variant) * 0.05f;
                    // 변형 1은 하역 구획선이다.
                    if (variant == 1 && (x < 4 || x > 59)) return Rgb(0.86f, 0.78f, 0.36f);
                    if (joint) return Rgb(0.49f, 0.48f, 0.47f);
                    return Rgb(0.63f + n, 0.62f + n, 0.60f + n);
                }

                case GroundStyle.Concrete:
                {
                    float n = Noise(x / 3, y / 3, variant) * 0.05f;
                    // 변형 3은 노랑·검정 위험 줄무늬. 공장이라는 표시다.
                    if (variant == 3)
                    {
                        return ((x + y) / 10) % 2 == 0 ? Rgb(0.88f, 0.74f, 0.20f) : Rgb(0.20f, 0.20f, 0.20f);
                    }

                    // 변형 2는 금이 갔다.
                    if (variant == 2 && Mathf.Abs(((x * 3) % 64) - y) < 2) return Rgb(0.44f, 0.43f, 0.42f);
                    if ((x % 64) < 2 || (y % 64) < 2) return Rgb(0.48f, 0.47f, 0.46f);
                    return Rgb(0.58f + n, 0.57f + n, 0.56f + n);
                }

                default:
                {
                    // 부두 널판. 16px 판자 사이 틈으로 아래 물빛이 비친다.
                    int gap = y % 16;
                    if (gap < 2) return Rgb(0.20f, 0.34f, 0.40f);
                    float n = Noise(x / 6, y / 16, variant) * 0.07f;
                    // 변형 1에는 판자를 가로지르는 이음쇠가 있다.
                    if (variant == 1 && (x % 64) > 56) return Rgb(0.42f, 0.40f, 0.38f);
                    return Rgb(0.55f + n, 0.44f + n, 0.33f + n);
                }
            }
        }

        private static Color32 Border(BorderStyle style, int x, int y)
        {
            switch (style)
            {
                case BorderStyle.Hedge:
                {
                    float n = Noise(x / 2, y / 2, 3);
                    if (n < 0.12f) return Rgb(0.13f, 0.30f, 0.15f);
                    return Rgb(0.17f + (n * 0.10f), 0.40f + (n * 0.14f), 0.19f + (n * 0.08f));
                }

                case BorderStyle.Planter:
                {
                    // 위아래 테두리는 벽돌 턱, 가운데는 관목.
                    if (y < 10 || y > 53) return Rgb(0.60f, 0.42f, 0.34f);
                    float n = Noise(x / 2, y / 2, 4);
                    return Rgb(0.20f + (n * 0.10f), 0.42f + (n * 0.12f), 0.24f + (n * 0.08f));
                }

                case BorderStyle.Guardrail:
                {
                    // 가운데 한 줄 가로대 + 일정 간격 지주.
                    if (y > 22 && y < 42) return Rgb(0.84f, 0.85f, 0.87f);
                    if ((x % 32) < 6) return Rgb(0.52f, 0.53f, 0.55f);
                    return Rgb(0.32f, 0.32f, 0.34f);
                }

                case BorderStyle.Fence:
                {
                    // 마름모 철망. 기둥은 굵게.
                    if ((x % 32) < 4) return Rgb(0.44f, 0.45f, 0.47f);
                    bool mesh = ((x + y) % 10) < 2 || ((x - y + 640) % 10) < 2;
                    return mesh ? Rgb(0.70f, 0.72f, 0.74f) : Rgb(0.30f, 0.31f, 0.33f);
                }

                default:
                {
                    // 방파제 — 안쪽은 돌 턱, 바깥은 물이다. 항구 맵에 바다를 주는 유일한 수단이다.
                    if (y > 40)
                    {
                        float n = Noise(x / 3, y / 3, 6);
                        return Rgb(0.52f + (n * 0.10f), 0.50f + (n * 0.10f), 0.47f + (n * 0.10f));
                    }

                    float w = Noise(x / 5, y / 4, 9);
                    if (w < 0.10f) return Rgb(0.74f, 0.88f, 0.92f);      // 물마루
                    return Rgb(0.18f + (w * 0.12f), 0.42f + (w * 0.16f), 0.54f + (w * 0.16f));
                }
            }
        }

        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        /// <summary>
        /// 소품은 칸을 꽉 채우지 않는다. 가장자리를 비워야 마당 위에 놓인 물건으로 보이고,
        /// 두 칸짜리 덩어리에 늘여 붙여도 옆 칸과 이어 붙지 않는다.
        /// </summary>
        private static Color32 Prop(PropStyle style, int x, int y)
        {
            switch (style)
            {
                case PropStyle.Pump:
                {
                    // 낮은 콘크리트 단(가로로 넓다) 위에 주유기 두 대.
                    if (y < 8 || y > 55 || x < 3 || x > 60) return Clear;
                    if (y < 14 || y > 49) return Rgb(0.72f, 0.70f, 0.66f);        // 단
                    if (x < 8 || x > 55) return Rgb(0.72f, 0.70f, 0.66f);

                    bool body = (x > 12 && x < 28) || (x > 35 && x < 51);
                    if (!body) return Rgb(0.80f, 0.78f, 0.74f);
                    if (y > 18 && y < 30) return Rgb(0.16f, 0.17f, 0.20f);        // 표시창
                    return Rgb(0.88f, 0.24f, 0.20f);                              // 붉은 기계
                }

                case PropStyle.Container:
                {
                    if (x < 2 || x > 61 || y < 4 || y > 59) return Clear;
                    // 가장자리 프레임
                    if (x < 6 || x > 57 || y < 8 || y > 55) return Rgb(0.20f, 0.30f, 0.38f);
                    // 세로 골
                    return (x % 6) < 2 ? Rgb(0.22f, 0.42f, 0.52f) : Rgb(0.30f, 0.56f, 0.68f);
                }

                case PropStyle.Pallet:
                {
                    if (x < 4 || x > 59 || y < 6 || y > 57) return Clear;
                    // 8px 각재를 가로로 쌓고 사이를 비운다.
                    int slat = (y - 6) % 11;
                    if (slat > 7) return Rgb(0.34f, 0.26f, 0.18f);                // 틈
                    float n = Noise(x / 5, y / 11, 2) * 0.08f;
                    return Rgb(0.70f + n, 0.55f + n, 0.33f + n);
                }

                case PropStyle.Planter:
                {
                    // 둥근 화분. 모서리를 잘라 원에 가깝게 만든다.
                    int dx = x - 32;
                    int dy = y - 32;
                    int r2 = (dx * dx) + (dy * dy);
                    if (r2 > 27 * 27) return Clear;
                    if (r2 > 21 * 21) return Rgb(0.62f, 0.40f, 0.31f);            // 벽돌 턱
                    float n = Noise(x / 2, y / 2, 8);
                    return Rgb(0.18f + (n * 0.12f), 0.44f + (n * 0.16f), 0.22f + (n * 0.10f));
                }

                default:
                {
                    // 계선주 — 짧고 굵은 쇠기둥에 밧줄 자국.
                    int dx = x - 32;
                    int dy = y - 34;
                    int r2 = (dx * dx) + (dy * dy);
                    if (r2 > 17 * 17) return Clear;
                    if (r2 > 13 * 13) return Rgb(0.18f, 0.20f, 0.22f);
                    if (y > 26 && y < 32) return Rgb(0.52f, 0.46f, 0.36f);        // 감긴 밧줄
                    return Rgb(0.34f, 0.36f, 0.39f);
                }
            }
        }

    }
}
