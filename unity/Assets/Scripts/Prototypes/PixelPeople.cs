using System.Collections.Generic;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 픽셀 3D 화면에 서 있는 사람: 옆에서 본 12×16 도트(문자 지도 → 텍스처).
    /// 앞(카메라 쪽)·뒤·옆(오른쪽, 왼쪽은 좌우 뒤집기) × 걷기 2프레임. 종류마다 헬멧·옷 색이 다르다.
    /// </summary>
    public static class PixelPeople
    {
        public enum Face
        {
            Down,
            Up,
            Side,
        }

        public enum Kind
        {
            Firefighter,
            Partner,
            Woman,
            Old,
            Man,
        }

        public const int Width = 12;
        public const int Height = 16;

        // K 테두리, H 헬멧·머리, h 챙·머리 그늘, S 피부, E 눈, C 옷, Y 반사띠·옷 무늬, G 장갑·손, P 바지, B 신발, T 산소통.
        private static readonly string[] Down0 =
        {
            "....KKKK....",
            "...KHHHHK...",
            "..KHHHHHHK..",
            "..KhhhhhhK..",
            "...KSSSSK...",
            "...KESSEK...",
            "...KSSSSK...",
            "..KCCYYCCK..",
            ".KCCCCCCCCK.",
            ".GCYYYYYYCG.",
            ".GCCCCCCCCG.",
            "..KCCCCCCK..",
            "..KPPKKPPK..",
            "..KPP..PPK..",
            "..KBB..BBK..",
            "..KKK..KKK..",
        };

        private static readonly string[] DownLegs1 =
        {
            "..KPPKKPPK..",
            "..KPP.KPPK..",
            "..KBB.KKKK..",
            "..KKK.......",
        };

        private static readonly string[] Up0 =
        {
            "....KKKK....",
            "...KHHHHK...",
            "..KHHHHHHK..",
            "..KhhhhhhK..",
            "...KHHHHK...",
            "...KhhhhK...",
            "...KSSSSK...",
            "..KCCCCCCK..",
            ".KCTTTTTTCK.",
            ".GCTTTTTTCG.",
            ".GCYYYYYYCG.",
            "..KCCCCCCK..",
            "..KPPKKPPK..",
            "..KPP..PPK..",
            "..KBB..BBK..",
            "..KKK..KKK..",
        };

        private static readonly string[] Side0 =
        {
            "....KKKK....",
            "...KHHHHK...",
            "..KHHHHHHKK.",
            "..KhhhhhhhK.",
            "...KSSSSSK..",
            "...KSSSESK..",
            "...KSSSSK...",
            "...KCCCCK...",
            "..KTCCCCCK..",
            "..KTYYYYGGK.",
            "..KTCCCCCK..",
            "...KCCCCK...",
            "...KPPPPK...",
            "...KPKKPK...",
            "...KBK.KBK..",
            "...KKK.KKK..",
        };

        private static readonly string[] SideLegs1 =
        {
            "...KPPPPK...",
            "...KPPPPK...",
            "...KBBBBK...",
            "...KKKKKK...",
        };

        private static readonly Dictionary<int, Sprite> Cache = new Dictionary<int, Sprite>();

        /// <summary>
        /// 사람 한 컷. outfit은 소방관의 방화복 단계(0 파랑 셔츠 → 1 노란 헬멧 → 2 빨간 헬멧·황갈 방화복 → 3 빨간 방화복 → 4 은색 방열복).
        /// </summary>
        public static Sprite Get(Kind kind, int outfit, Face face, int frame)
        {
            int key = ((((int)kind * 8) + outfit) * 4 + (int)face) * 2 + (frame & 1);
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            string[] map = face == Face.Down ? Down0 : face == Face.Up ? Up0 : Side0;
            string[] legs = face == Face.Side ? SideLegs1 : DownLegs1;
            Dictionary<char, Color32> palette = Palette(kind, outfit);
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Person" + kind + outfit + face + frame,
            };
            var pixels = new Color32[Width * Height];
            for (int row = 0; row < Height; row++)
            {
                string line = map[row];
                if ((frame & 1) == 1 && row >= Height - 4) line = legs[row - (Height - 4)];
                int y = Height - 1 - row;
                for (int x = 0; x < Width; x++)
                {
                    char ch = x < line.Length ? line[x] : '.';
                    pixels[(y * Width) + x] = palette.TryGetValue(ch, out Color32 c) ? c : new Color32(0, 0, 0, 0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, Width, Height), new Vector2(0.5f, 0.5f), Width);
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Cache[key] = sprite;
            return sprite;
        }

        private static Dictionary<char, Color32> Palette(Kind kind, int outfit)
        {
            Color32 outline = Rgb(0.1f, 0.08f, 0.08f);
            Color32 skin = Rgb(0.96f, 0.78f, 0.6f);
            Color32 helmet;
            Color32 coat;
            Color32 stripe;
            Color32 hands = Rgb(0.25f, 0.22f, 0.2f);
            Color32 pants = Rgb(0.22f, 0.22f, 0.28f);
            Color32 boots = Rgb(0.12f, 0.1f, 0.1f);
            Color32 tank = Rgb(0.78f, 0.8f, 0.84f);
            switch (kind)
            {
                case Kind.Firefighter:
                    switch (Mathf.Clamp(outfit, 0, 4))
                    {
                        case 0:
                            helmet = Rgb(0.15f, 0.12f, 0.1f);
                            coat = Rgb(0.2f, 0.45f, 0.85f);
                            stripe = Rgb(0.45f, 0.7f, 1f);
                            break;
                        case 1:
                            helmet = Rgb(1f, 0.82f, 0.15f);
                            coat = Rgb(0.2f, 0.45f, 0.85f);
                            stripe = Rgb(0.45f, 0.7f, 1f);
                            break;
                        case 2:
                            helmet = Rgb(0.9f, 0.18f, 0.12f);
                            coat = Rgb(0.82f, 0.66f, 0.32f);
                            stripe = Rgb(0.85f, 1f, 0.3f);
                            break;
                        case 3:
                            helmet = Rgb(0.9f, 0.18f, 0.12f);
                            coat = Rgb(0.85f, 0.2f, 0.14f);
                            stripe = Rgb(1f, 0.95f, 0.45f);
                            break;
                        default:
                            helmet = Rgb(0.88f, 0.9f, 0.93f);
                            coat = Rgb(0.72f, 0.75f, 0.8f);
                            stripe = Rgb(1f, 0.85f, 0.35f);
                            break;
                    }
                    break;
                case Kind.Partner:
                    helmet = Rgb(1f, 0.82f, 0.15f);
                    coat = Rgb(0.95f, 0.5f, 0.15f);
                    stripe = Rgb(0.95f, 0.95f, 0.6f);
                    break;
                case Kind.Woman:
                    helmet = Rgb(0.45f, 0.25f, 0.12f);
                    coat = Rgb(0.92f, 0.45f, 0.6f);
                    stripe = Rgb(1f, 0.75f, 0.82f);
                    hands = skin;
                    pants = Rgb(0.3f, 0.3f, 0.5f);
                    tank = coat;
                    break;
                case Kind.Old:
                    helmet = Rgb(0.8f, 0.8f, 0.8f);
                    coat = Rgb(0.55f, 0.4f, 0.28f);
                    stripe = Rgb(0.65f, 0.5f, 0.36f);
                    hands = skin;
                    pants = Rgb(0.35f, 0.33f, 0.3f);
                    tank = coat;
                    break;
                default:
                    helmet = Rgb(0.12f, 0.1f, 0.1f);
                    coat = Rgb(0.3f, 0.65f, 0.35f);
                    stripe = Rgb(0.45f, 0.78f, 0.5f);
                    hands = skin;
                    pants = Rgb(0.25f, 0.3f, 0.45f);
                    tank = coat;
                    break;
            }
            return new Dictionary<char, Color32>
            {
                { 'K', outline },
                { 'H', helmet },
                { 'h', Scale(helmet, 0.75f) },
                { 'S', skin },
                { 'E', Rgb(0.1f, 0.1f, 0.15f) },
                { 'C', coat },
                { 'Y', stripe },
                { 'G', hands },
                { 'P', pants },
                { 'B', boots },
                { 'T', tank },
            };
        }

        private static Color32 Rgb(float r, float g, float b)
        {
            return new Color32((byte)(r * 255f), (byte)(g * 255f), (byte)(b * 255f), 255);
        }

        private static Color32 Scale(Color32 c, float k)
        {
            return new Color32((byte)Mathf.Min(255f, c.r * k), (byte)Mathf.Min(255f, c.g * k), (byte)Mathf.Min(255f, c.b * k), 255);
        }
    }
}
