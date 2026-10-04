using System.Collections.Generic;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 시험판 C 동네 가게 그림. 켄니 팩에는 건물 그림이 없어 코드로 픽셀을 찍는다.
    ///
    /// 흰 네모를 겹쳐 그렸더니 여덟 채가 색만 다른 컨테이너 상자로 보였다(사용자 지적).
    /// 그래서 3/4 탑다운으로 그린다: 위쪽은 위에서 본 지붕(기와·평지붕·함석), 아래쪽은 앞면(간판·차양·유리창·문).
    /// 가게마다 한눈에 알아볼 표식을 하나씩 준다: 편의점 세 줄 간판, 약국 초록 십자, 빵집 줄무늬 차양…
    /// 문은 앞면 아래 가운데라 규칙의 문 자리(Structure.Door)와 맞는다.
    /// </summary>
    public static class ShopArt
    {
        /// <summary>한 칸 = 48픽셀.</summary>
        public const int Ppu = 48;

        public sealed class Look
        {
            public Sprite Sprite;

            /// <summary>유리창 자리(건물 가운데 기준 월드 좌표). 불이 나면 불빛, 첫 창에는 갇힌 사람 얼굴.</summary>
            public Rect[] Windows;

            /// <summary>간판 띠 가운데 높이(건물 가운데 기준).</summary>
            public float SignY;

            /// <summary>간판이 밝아 글씨를 어둡게 써야 한다.</summary>
            public bool SignDark;

            /// <summary>김이 오르는 자리(굴뚝·배기구·솥). 없으면 null.</summary>
            public Vector2? Steam;

            /// <summary>픽셀 3D 상자용: 앞벽(아래 FrontRows 행)과 지붕(그 위) 그림, 옆벽 색.</summary>
            public Sprite Front;
            public Sprite Roof;
            public int FrontRows;
            public Color WallColor = new Color(0.6f, 0.58f, 0.55f);

            /// <summary>앞벽 높이(칸) = 상자 높이.</summary>
            public float Height
            {
                get { return (float)FrontRows / Ppu; }
            }
        }

        private static readonly Dictionary<string, Look> Cache = new Dictionary<string, Look>();

        public static Look For(string name, float width, float height)
        {
            string key = name + width + "x" + height;
            if (Cache.TryGetValue(key, out Look look) && look.Sprite != null) return look;
            var c = new Canvas(Mathf.RoundToInt(width * Ppu), Mathf.RoundToInt(height * Ppu));
            look = new Look();
            Paint(c, name, look);
            look.Sprite = c.ToSprite(name);
            Texture2D texture = look.Sprite.texture;
            look.FrontRows = Mathf.Clamp(look.FrontRows, 1, c.H - 1);
            look.Front = Sprite.Create(texture, new Rect(0, 0, c.W, look.FrontRows), new Vector2(0.5f, 0.5f), Ppu);
            look.Roof = Sprite.Create(texture, new Rect(0, look.FrontRows, c.W, c.H - look.FrontRows), new Vector2(0.5f, 0.5f), Ppu);
            look.Front.hideFlags = HideFlags.DontUnloadUnusedAsset;
            look.Roof.hideFlags = HideFlags.DontUnloadUnusedAsset;
            // 옆벽: 앞벽 아래쪽 줄의 평균 색을 조금 어둡게.
            float r = 0f, g = 0f, b = 0f;
            int n = 0;
            for (int x = 0; x < c.W; x += 3)
            {
                Color32 p = c.Get(x, look.FrontRows / 3);
                if (p.a < 128) continue;
                r += p.r;
                g += p.g;
                b += p.b;
                n++;
            }
            if (n > 0) look.WallColor = new Color(r / n / 255f * 0.72f, g / n / 255f * 0.72f, b / n / 255f * 0.72f);
            Cache[key] = look;
            return look;
        }

        // ------------------------------------------------------------------
        // 가게별
        // ------------------------------------------------------------------

        private static void Paint(Canvas c, string name, Look look)
        {
            if (name == "물류창고" || name == "제재소")
            {
                Depot(c, look);
                return;
            }

            // 앞면 높이(픽셀): 건물 높이의 절반. 그 위가 지붕(40%로는 가게 유리창이 작아 알아보기 어려웠다).
            int front = Mathf.RoundToInt(c.H * 0.5f);
            look.FrontRows = front;
            int sign0 = front - 16;
            var windows = new List<RectInt>();

            switch (name)
            {
                case "편의점":
                    FlatRoof(c, front, Rgb(0.72f, 0.74f, 0.76f));
                    AirCon(c, c.W * 0.22f, front + ((c.H - front) * 0.55f));
                    AirCon(c, c.W * 0.4f, front + ((c.H - front) * 0.55f));
                    Vent(c, c.W * 0.78f, front + ((c.H - front) * 0.6f));
                    Wall(c, front, Rgb(0.93f, 0.93f, 0.9f));
                    // 흰 간판에 초록·파랑·주황 세 줄.
                    c.Fill(0, sign0, c.W, front, Rgb(0.98f, 0.98f, 0.97f));
                    c.Fill(0, sign0 + 11, c.W, sign0 + 14, Rgb(0.2f, 0.7f, 0.35f));
                    c.Fill(0, sign0 + 7, c.W, sign0 + 10, Rgb(0.2f, 0.45f, 0.85f));
                    c.Fill(0, sign0 + 2, c.W, sign0 + 5, Rgb(0.95f, 0.55f, 0.15f));
                    look.SignDark = true;
                    // 통유리 두 칸과 안쪽 진열대, 가운데 자동문.
                    windows.Add(Glass(c, 6, 5, (c.W / 2) - 24, sign0 - 3, true));
                    windows.Add(Glass(c, (c.W / 2) + 24, 5, c.W - 6, sign0 - 3, true));
                    GlassDoor(c, c.W / 2, 44, sign0 - 3);
                    break;

                case "약국":
                    FlatRoof(c, front, Rgb(0.9f, 0.9f, 0.88f));
                    Tank(c, c.W * 0.75f, front + ((c.H - front) * 0.55f));
                    Wall(c, front, Rgb(0.97f, 0.97f, 0.97f));
                    TileGrid(c, 0, 0, c.W, sign0, Rgb(0.85f, 0.87f, 0.87f));
                    c.Fill(0, sign0, c.W, front, Rgb(0.15f, 0.62f, 0.38f));
                    // 초록 간판 양쪽의 흰 십자 + 문 옆 큰 초록 십자.
                    Cross(c, 12, sign0 + 8, 5, Rgb(1f, 1f, 1f));
                    Cross(c, c.W - 12, sign0 + 8, 5, Rgb(1f, 1f, 1f));
                    windows.Add(Glass(c, 8, 8, (c.W / 2) - 26, sign0 - 5, false));
                    windows.Add(Glass(c, (c.W / 2) + 26, 8, c.W - 8, sign0 - 5, false));
                    c.Fill(c.W - 30, 12, c.W - 12, 30, Rgb(1f, 1f, 1f));
                    Cross(c, c.W - 21, 21, 7, Rgb(0.15f, 0.7f, 0.4f));
                    GlassDoor(c, c.W / 2, 34, sign0 - 3);
                    break;

                case "빵집":
                    TileRoof(c, front, Rgb(0.86f, 0.46f, 0.26f));
                    Chimney(c, c.W * 0.78f, front + ((c.H - front) * 0.62f));
                    look.Steam = Local(c, c.W * 0.78f, front + ((c.H - front) * 0.62f) + 8);
                    Wall(c, front, Rgb(0.96f, 0.9f, 0.78f));
                    c.Fill(0, sign0 + 2, c.W, front, Rgb(0.45f, 0.27f, 0.15f));
                    Awning(c, sign0 - 14, sign0 + 2, Rgb(0.88f, 0.2f, 0.2f), Rgb(1f, 1f, 1f));
                    // 진열창 안 빵(갈색 둥근 덩이).
                    RectInt left = Glass(c, 8, 6, (c.W / 2) - 24, sign0 - 16, false);
                    RectInt right = Glass(c, (c.W / 2) + 24, 6, c.W - 8, sign0 - 16, false);
                    Breads(c, left);
                    Breads(c, right);
                    windows.Add(left);
                    windows.Add(right);
                    WoodDoor(c, c.W / 2, 30, sign0 - 15, Rgb(0.5f, 0.3f, 0.16f));
                    break;

                case "꽃집":
                    TileRoof(c, front, Rgb(0.35f, 0.62f, 0.4f));
                    Wall(c, front, Rgb(0.92f, 0.96f, 0.9f));
                    c.Fill(0, sign0 + 2, c.W, front, Rgb(0.95f, 0.95f, 0.92f));
                    look.SignDark = true;
                    Awning(c, sign0 - 14, sign0 + 2, Rgb(0.25f, 0.6f, 0.35f), Rgb(0.95f, 0.98f, 0.95f));
                    windows.Add(Glass(c, 8, 12, (c.W / 2) - 24, sign0 - 16, false));
                    windows.Add(Glass(c, (c.W / 2) + 24, 12, c.W - 8, sign0 - 16, false));
                    WoodDoor(c, c.W / 2, 30, sign0 - 15, Rgb(0.55f, 0.75f, 0.55f));
                    // 가게 앞 화분 줄.
                    Pots(c, 4, (c.W / 2) - 20);
                    Pots(c, (c.W / 2) + 20, c.W - 4);
                    break;

                case "문구점":
                    FlatRoof(c, front, Rgb(0.35f, 0.55f, 0.85f));
                    Vent(c, c.W * 0.25f, front + ((c.H - front) * 0.55f));
                    Wall(c, front, Rgb(0.95f, 0.93f, 0.85f));
                    c.Fill(0, sign0 + 2, c.W, front, Rgb(0.2f, 0.35f, 0.7f));
                    Awning(c, sign0 - 14, sign0 + 2, Rgb(0.98f, 0.8f, 0.2f), Rgb(1f, 1f, 1f));
                    RectInt w1 = Glass(c, 8, 6, (c.W / 2) - 24, sign0 - 16, false);
                    RectInt w2 = Glass(c, (c.W / 2) + 24, 6, c.W - 8, sign0 - 16, false);
                    Posters(c, w1);
                    Posters(c, w2);
                    windows.Add(w1);
                    windows.Add(w2);
                    WoodDoor(c, c.W / 2, 30, sign0 - 15, Rgb(0.3f, 0.45f, 0.75f));
                    break;

                case "세탁소":
                    FlatRoof(c, front, Rgb(0.62f, 0.8f, 0.88f));
                    Vent(c, c.W * 0.7f, front + ((c.H - front) * 0.55f));
                    look.Steam = Local(c, c.W * 0.7f, front + ((c.H - front) * 0.55f) + 6);
                    Wall(c, front, Rgb(0.9f, 0.94f, 0.97f));
                    c.Fill(0, sign0, c.W, front, Rgb(0.18f, 0.42f, 0.78f));
                    // 창 안 옷걸이에 걸린 옷.
                    RectInt l1 = Glass(c, 6, 5, (c.W / 2) - 24, sign0 - 3, false);
                    RectInt l2 = Glass(c, (c.W / 2) + 24, 5, c.W - 6, sign0 - 3, false);
                    Clothes(c, l1);
                    Clothes(c, l2);
                    windows.Add(l1);
                    windows.Add(l2);
                    GlassDoor(c, c.W / 2, 34, sign0 - 3);
                    break;

                case "분식집":
                    TileRoof(c, front, Rgb(0.8f, 0.25f, 0.22f));
                    Wall(c, front, Rgb(0.98f, 0.92f, 0.8f));
                    c.Fill(0, sign0 + 2, c.W, front, Rgb(0.85f, 0.2f, 0.15f));
                    Awning(c, sign0 - 14, sign0 + 2, Rgb(0.98f, 0.55f, 0.15f), Rgb(1f, 0.95f, 0.85f));
                    RectInt p1 = Glass(c, 8, 6, (c.W / 2) - 24, sign0 - 16, false);
                    RectInt p2 = Glass(c, (c.W / 2) + 24, 6, c.W - 8, sign0 - 16, false);
                    // 창 안 떡볶이 솥(검은 원 안 빨간 떡볶이).
                    Vector2 pot = new Vector2((p2.xMin + p2.xMax) / 2f, p2.yMin + 8);
                    c.Disc(pot.x, pot.y, 9, Rgb(0.2f, 0.2f, 0.22f));
                    c.Disc(pot.x, pot.y, 7, Rgb(0.9f, 0.25f, 0.15f));
                    look.Steam = Local(c, pot.x, pot.y + 10);
                    windows.Add(p1);
                    windows.Add(p2);
                    WoodDoor(c, c.W / 2, 30, sign0 - 15, Rgb(0.6f, 0.35f, 0.2f));
                    // 입간판.
                    c.Fill(p1.xMin + 4, 1, p1.xMin + 16, 14, Rgb(0.2f, 0.2f, 0.2f));
                    c.Fill(p1.xMin + 5, 3, p1.xMin + 15, 13, Rgb(0.95f, 0.85f, 0.3f));
                    break;

                case "이발소":
                    TileRoof(c, front, Rgb(0.5f, 0.36f, 0.28f));
                    Wall(c, front, Rgb(0.88f, 0.84f, 0.8f));
                    c.Fill(0, sign0, c.W, front, Rgb(0.2f, 0.22f, 0.3f));
                    windows.Add(Glass(c, 8, 8, (c.W / 2) - 36, sign0 - 5, false));
                    windows.Add(Glass(c, (c.W / 2) + 36, 8, c.W - 8, sign0 - 5, false));
                    WoodDoor(c, c.W / 2, 32, sign0 - 4, Rgb(0.4f, 0.28f, 0.2f));
                    // 문 옆 빨강·흰·파랑 사선 이발소 기둥.
                    BarberPole(c, (c.W / 2) + 26, 3, sign0 - 3);
                    BarberPole(c, (c.W / 2) - 26, 3, sign0 - 3);
                    break;

                // 산불 숲: 통나무 벽에 기와 지붕. 지붕 색으로 서로 구분한다.
                case "산장":
                case "캠핑 매점":
                case "관리사무소":
                case "통나무 카페":
                case "전망대":
                case "목공소":
                    Color32 roof = name == "산장" ? Rgb(0.55f, 0.2f, 0.15f) : name == "캠핑 매점" ? Rgb(0.2f, 0.45f, 0.3f) : name == "관리사무소" ? Rgb(0.25f, 0.35f, 0.55f)
                        : name == "통나무 카페" ? Rgb(0.6f, 0.4f, 0.2f) : name == "전망대" ? Rgb(0.35f, 0.35f, 0.4f) : Rgb(0.45f, 0.3f, 0.2f);
                    TileRoof(c, front, roof);
                    for (int y = 0; y < front; y++)
                    {
                        // 통나무: 6픽셀마다 한 줄, 줄 사이는 어두운 틈.
                        float k = (y % 6) == 0 ? 0.6f : (y % 6) == 1 ? 0.8f : 1f;
                        for (int x = 0; x < c.W; x++) c.Set(x, y, Scale(Rgb(0.58f, 0.4f, 0.24f), k));
                    }
                    c.Fill(0, sign0, c.W, front, Rgb(0.3f, 0.2f, 0.12f));
                    windows.Add(Glass(c, 8, 8, (c.W / 2) - 24, sign0 - 5, false));
                    windows.Add(Glass(c, (c.W / 2) + 24, 8, c.W - 8, sign0 - 5, false));
                    WoodDoor(c, c.W / 2, 30, sign0 - 4, Rgb(0.35f, 0.22f, 0.12f));
                    break;

                // 마을 나머지 여섯 가게: 3D 주택은 지붕색으로만 구별되므로 default 회색 대신 저마다의 색(맵 특색 패스).
                case "카페":
                case "서점":
                case "정육점":
                case "미용실":
                case "철물점":
                case "치킨집":
                    Color32 shopRoof = name == "카페" ? Rgb(0.45f, 0.3f, 0.2f) : name == "서점" ? Rgb(0.2f, 0.45f, 0.3f) : name == "정육점" ? Rgb(0.75f, 0.2f, 0.2f)
                        : name == "미용실" ? Rgb(0.9f, 0.5f, 0.7f) : name == "철물점" ? Rgb(0.4f, 0.45f, 0.55f) : Rgb(0.9f, 0.5f, 0.15f);
                    FlatRoof(c, front, shopRoof);
                    Wall(c, front, Rgb(0.9f, 0.9f, 0.9f));
                    c.Fill(0, sign0, c.W, front, Rgb(0.3f, 0.3f, 0.35f));
                    windows.Add(Glass(c, 8, 8, (c.W / 2) - 24, sign0 - 5, false));
                    windows.Add(Glass(c, (c.W / 2) + 24, 8, c.W - 8, sign0 - 5, false));
                    WoodDoor(c, c.W / 2, 30, sign0 - 4, Rgb(0.45f, 0.3f, 0.2f));
                    break;

                default:
                    FlatRoof(c, front, Rgb(0.7f, 0.7f, 0.7f));
                    Wall(c, front, Rgb(0.9f, 0.9f, 0.9f));
                    c.Fill(0, sign0, c.W, front, Rgb(0.3f, 0.3f, 0.35f));
                    windows.Add(Glass(c, 8, 8, (c.W / 2) - 24, sign0 - 5, false));
                    windows.Add(Glass(c, (c.W / 2) + 24, 8, c.W - 8, sign0 - 5, false));
                    WoodDoor(c, c.W / 2, 30, sign0 - 4, Rgb(0.45f, 0.3f, 0.2f));
                    break;
            }

            if (look.SignY == 0f) look.SignY = Local(c, 0f, (sign0 + front) / 2f).y;
            Ledge(c, front);
            look.Windows = ToWorld(c, windows);
        }

        /// <summary>물류창고: 함석 골지붕과 채광창, 골벽 앞면에 셔터 세 짝(가운데가 문).</summary>
        private static void Depot(Canvas c, Look look)
        {
            int front = Mathf.RoundToInt(c.H * 0.42f);
            look.FrontRows = front;
            Color32 metal = Rgb(0.62f, 0.66f, 0.7f);
            for (int x = 0; x < c.W; x++)
            {
                // 세로 골: 6픽셀마다 밝고 어두운 줄.
                float k = (x % 8) < 3 ? 1.08f : (x % 8) < 5 ? 0.92f : 1f;
                for (int y = front; y < c.H; y++) c.Set(x, y, Scale(metal, k * (y > c.H - 5 ? 1.12f : 1f)));
            }
            for (int i = 0; i < 3; i++)
            {
                int x0 = (c.W / 6) + (i * c.W / 3) - 18;
                int y0 = front + ((c.H - front) / 2) - 8;
                c.Fill(x0, y0, x0 + 36, y0 + 16, Rgb(0.55f, 0.75f, 0.85f));
                c.Fill(x0 + 2, y0 + 9, x0 + 34, y0 + 14, Rgb(0.75f, 0.9f, 0.97f));
            }
            // 앞면: 골벽.
            for (int x = 0; x < c.W; x++)
            {
                float k = (x % 10) < 5 ? 1f : 0.9f;
                for (int y = 0; y < front; y++) c.Set(x, y, Scale(Rgb(0.55f, 0.58f, 0.6f), k));
            }
            int sign0 = front - 14;
            c.Fill(0, sign0, c.W, front, Rgb(0.95f, 0.75f, 0.15f));
            look.SignDark = true;
            look.SignY = Local(c, 0f, (sign0 + front) / 2f).y;
            var windows = new List<RectInt>();
            for (int i = 0; i < 3; i++)
            {
                int cx = (c.W / 6) + (i * c.W / 3);
                int half = i == 1 ? 40 : 32;
                int top = sign0 - 6;
                c.Fill(cx - half - 2, 0, cx + half + 2, top + 2, Rgb(0.25f, 0.26f, 0.28f));
                for (int y = 0; y < top; y++)
                {
                    Color32 slat = (y % 5) == 0 ? Rgb(0.45f, 0.47f, 0.5f) : Rgb(0.7f, 0.72f, 0.74f);
                    for (int x = cx - half; x < cx + half; x++) c.Set(x, y, slat);
                }
                // 셔터 위 쪽창(불이 나면 불빛이 새어 나온다).
                if (i != 1) windows.Add(new RectInt(cx - half + 4, top - 12, (half * 2) - 8, 8));
            }
            // 가운데 셔터 위에 위험 줄무늬.
            for (int x = (c.W / 2) - 40; x < (c.W / 2) + 40; x++)
            {
                for (int y = sign0 - 6; y < sign0 - 2; y++) c.Set(x, y, ((x + y) / 4) % 2 == 0 ? Rgb(0.95f, 0.8f, 0.1f) : Rgb(0.15f, 0.15f, 0.15f));
            }
            Ledge(c, front);
            look.Windows = ToWorld(c, windows);
        }

        // ------------------------------------------------------------------
        // 붓
        // ------------------------------------------------------------------

        /// <summary>평지붕: 가장자리 턱(밝게) + 옅은 얼룩 + 아래쪽 처마 그림자.</summary>
        private static void FlatRoof(Canvas c, int front, Color32 color)
        {
            for (int y = front; y < c.H; y++)
            {
                for (int x = 0; x < c.W; x++)
                {
                    bool rim = x < 4 || x >= c.W - 4 || y >= c.H - 4 || y < front + 4;
                    float n = 0.96f + (0.06f * Noise(x / 3, y / 3));
                    c.Set(x, y, Scale(color, rim ? 1.12f : n));
                }
            }
        }

        /// <summary>박공 기와지붕: 가로 기와 줄이 한 단씩 엇갈리고, 가운데 용마루를 경계로 윗면은 밝고 아랫면은 어둡다.</summary>
        private static void TileRoof(Canvas c, int front, Color32 color)
        {
            int ridge = front + ((c.H - front) / 2);
            for (int y = front; y < c.H; y++)
            {
                int row = (y - front) / 7;
                bool seam = (y - front) % 7 == 0;
                for (int x = 0; x < c.W; x++)
                {
                    bool gap = ((x + ((row % 2) * 6)) % 12) == 0;
                    float k = y >= ridge ? 1.1f : 0.86f;
                    if (seam || gap) k *= 0.78f;
                    if (Mathf.Abs(y - ridge) <= 1) k = 0.62f;
                    if (x < 3 || x >= c.W - 3) k *= 0.85f;
                    c.Set(x, y, Scale(color, k));
                }
            }
        }

        private static void Wall(Canvas c, int front, Color32 color)
        {
            c.Fill(0, 0, c.W, front, color);
        }

        /// <summary>지붕 처마 그림자와 앞면 밑 턱: 앞면이 지붕 아래로 쑥 들어가 보인다.</summary>
        private static void Ledge(Canvas c, int front)
        {
            for (int x = 0; x < c.W; x++)
            {
                for (int y = front - 3; y < front; y++) c.Set(x, y, Scale(c.Get(x, y), 0.6f));
                c.Set(x, front, Rgb(0.15f, 0.12f, 0.12f));
                for (int y = 0; y < 3; y++) c.Set(x, y, Rgb(0.35f, 0.33f, 0.32f));
            }
            // 외곽선.
            for (int x = 0; x < c.W; x++)
            {
                c.Set(x, 0, Rgb(0.12f, 0.1f, 0.1f));
                c.Set(x, c.H - 1, Rgb(0.12f, 0.1f, 0.1f));
            }
            for (int y = 0; y < c.H; y++)
            {
                c.Set(0, y, Rgb(0.12f, 0.1f, 0.1f));
                c.Set(c.W - 1, y, Rgb(0.12f, 0.1f, 0.1f));
            }
        }

        /// <summary>유리창: 짙은 틀 + 하늘빛 유리 + 사선 반사. 진열대가 있으면 안쪽에 선반 줄과 물건.</summary>
        private static RectInt Glass(Canvas c, int x0, int y0, int x1, int y1, bool shelves)
        {
            c.Fill(x0 - 2, y0 - 2, x1 + 2, y1 + 2, Rgb(0.25f, 0.25f, 0.28f));
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    float t = (float)(y - y0) / Mathf.Max(1, y1 - y0);
                    Color32 g = Lerp(Rgb(0.45f, 0.62f, 0.75f), Rgb(0.7f, 0.85f, 0.95f), t);
                    if (((x - x0) + (y - y0)) % 22 < 3) g = Scale(g, 1.18f);
                    c.Set(x, y, g);
                }
            }
            if (shelves)
            {
                for (int y = y0 + 6; y < y1 - 2; y += 9)
                {
                    c.Fill(x0 + 2, y, x1 - 2, y + 1, Rgb(0.35f, 0.35f, 0.4f));
                    for (int x = x0 + 3; x < x1 - 4; x += 5)
                    {
                        Color32 item = Palette[((x * 7) + (y * 3)) % Palette.Length];
                        c.Fill(x, y + 1, x + 3, y + 5, item);
                    }
                }
            }
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        private static void GlassDoor(Canvas c, int cx, int width, int top)
        {
            int x0 = cx - (width / 2);
            int x1 = cx + (width / 2);
            c.Fill(x0 - 2, 0, x1 + 2, top + 2, Rgb(0.3f, 0.3f, 0.32f));
            c.Fill(x0, 0, x1, top, Rgb(0.55f, 0.72f, 0.82f));
            c.Fill(cx - 1, 0, cx + 1, top, Rgb(0.3f, 0.3f, 0.32f));
            c.Fill(x0 + 3, top - 6, x0 + 6, top - 2, Rgb(0.85f, 0.95f, 1f));
        }

        private static void WoodDoor(Canvas c, int cx, int width, int top, Color32 color)
        {
            int x0 = cx - (width / 2);
            int x1 = cx + (width / 2);
            c.Fill(x0 - 2, 0, x1 + 2, top + 2, Rgb(0.2f, 0.15f, 0.12f));
            c.Fill(x0, 0, x1, top, color);
            c.Fill(x0 + 4, (top / 2) + 2, x1 - 4, top - 4, Rgb(0.65f, 0.82f, 0.9f));
            c.Disc(x1 - 5, top / 2f - 2, 2, Rgb(0.95f, 0.8f, 0.3f));
        }

        /// <summary>줄무늬 차양: 세로 줄무늬 + 아래쪽 물결 가장자리 + 그 밑 그림자.</summary>
        private static void Awning(Canvas c, int y0, int y1, Color32 a, Color32 b)
        {
            for (int x = 0; x < c.W; x++)
            {
                int scallop = (int)(3f * Mathf.Abs(Mathf.Sin(x * Mathf.PI / 12f)));
                for (int y = y0 - scallop; y < y1; y++)
                {
                    Color32 s = (x / 12) % 2 == 0 ? a : b;
                    float shade = y > y1 - 4 ? 1.08f : 0.95f + (0.08f * (y - y0) / Mathf.Max(1f, y1 - y0));
                    c.Set(x, y, Scale(s, shade));
                }
                for (int y = y0 - scallop - 3; y < y0 - scallop; y++) c.Set(x, y, Scale(c.Get(x, y), 0.7f));
            }
        }

        private static void TileGrid(Canvas c, int x0, int y0, int x1, int y1, Color32 line)
        {
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    if (x % 8 == 0 || y % 8 == 0) c.Set(x, y, line);
                }
            }
        }

        private static void Cross(Canvas c, float cx, float cy, int arm, Color32 color)
        {
            int t = Mathf.Max(2, arm / 2);
            c.Fill((int)cx - t, (int)cy - arm, (int)cx + t, (int)cy + arm, color);
            c.Fill((int)cx - arm, (int)cy - t, (int)cx + arm, (int)cy + t, color);
        }

        private static void AirCon(Canvas c, float cx, float cy)
        {
            c.Fill((int)cx - 13, (int)cy - 10, (int)cx + 13, (int)cy + 10, Rgb(0.45f, 0.47f, 0.5f));
            c.Fill((int)cx - 12, (int)cy - 9, (int)cx + 12, (int)cy + 9, Rgb(0.85f, 0.86f, 0.88f));
            c.Disc(cx, cy, 7, Rgb(0.35f, 0.37f, 0.4f));
            c.Fill((int)cx - 6, (int)cy - 1, (int)cx + 6, (int)cy + 1, Rgb(0.7f, 0.72f, 0.75f));
            c.Fill((int)cx - 1, (int)cy - 6, (int)cx + 1, (int)cy + 6, Rgb(0.7f, 0.72f, 0.75f));
        }

        private static void Vent(Canvas c, float cx, float cy)
        {
            c.Disc(cx, cy, 7, Rgb(0.35f, 0.35f, 0.38f));
            c.Disc(cx, cy, 5, Rgb(0.6f, 0.62f, 0.65f));
            c.Disc(cx, cy, 2, Rgb(0.2f, 0.2f, 0.22f));
        }

        private static void Tank(Canvas c, float cx, float cy)
        {
            c.Disc(cx + 2, cy - 2, 14, Rgb(0.4f, 0.4f, 0.42f));
            c.Disc(cx, cy, 14, Rgb(0.3f, 0.55f, 0.85f));
            c.Disc(cx - 3, cy + 3, 6, Rgb(0.5f, 0.72f, 0.95f));
        }

        private static void Chimney(Canvas c, float cx, float cy)
        {
            c.Fill((int)cx - 9, (int)cy - 12, (int)cx + 11, (int)cy + 8, Rgb(0.25f, 0.15f, 0.12f));
            c.Fill((int)cx - 10, (int)cy - 10, (int)cx + 10, (int)cy + 10, Rgb(0.6f, 0.3f, 0.22f));
            for (int y = (int)cy - 10; y < (int)cy + 10; y += 5) c.Fill((int)cx - 10, y, (int)cx + 10, y + 1, Rgb(0.45f, 0.22f, 0.16f));
            c.Fill((int)cx - 6, (int)cy - 6, (int)cx + 6, (int)cy + 6, Rgb(0.12f, 0.1f, 0.1f));
        }

        private static void Breads(Canvas c, RectInt w)
        {
            for (int x = w.xMin + 7; x < w.xMax - 5; x += 11)
            {
                c.Ellipse(x, w.yMin + 7, 5, 4, Rgb(0.75f, 0.48f, 0.2f));
                c.Ellipse(x - 1, w.yMin + 8, 3, 2, Rgb(0.9f, 0.68f, 0.35f));
            }
            c.Fill(w.xMin, w.yMin + 2, w.xMax, w.yMin + 3, Rgb(0.6f, 0.45f, 0.3f));
        }

        private static void Posters(Canvas c, RectInt w)
        {
            int i = 0;
            for (int x = w.xMin + 3; x < w.xMax - 10; x += 13)
            {
                Color32 p = Palette[(i++ * 3 + w.xMin) % Palette.Length];
                c.Fill(x, w.yMin + 4, x + 10, w.yMax - 3, p);
                c.Fill(x + 2, w.yMax - 8, x + 8, w.yMax - 6, Rgb(1f, 1f, 1f));
            }
        }

        private static void Clothes(Canvas c, RectInt w)
        {
            int rail = w.yMax - 4;
            c.Fill(w.xMin + 2, rail, w.xMax - 2, rail + 1, Rgb(0.3f, 0.3f, 0.32f));
            int i = 0;
            for (int x = w.xMin + 5; x < w.xMax - 8; x += 10)
            {
                Color32 cloth = Palette[(i++ * 5 + 2) % Palette.Length];
                c.Fill(x, rail - 14, x + 8, rail - 2, cloth);
                c.Fill(x - 2, rail - 6, x + 10, rail - 2, cloth);
            }
        }

        private static void Pots(Canvas c, int x0, int x1)
        {
            int i = 0;
            for (int x = x0 + 5; x < x1 - 4; x += 9)
            {
                c.Fill(x - 4, 2, x + 4, 8, Rgb(0.6f, 0.35f, 0.2f));
                Color32 flower = Flowers[i++ % Flowers.Length];
                c.Disc(x, 11, 4, Rgb(0.25f, 0.55f, 0.25f));
                c.Disc(x - 1, 12, 2.5f, flower);
                c.Disc(x + 2, 10, 2f, flower);
            }
        }

        private static void BarberPole(Canvas c, int cx, int y0, int y1)
        {
            c.Fill(cx - 5, y0 - 1, cx + 5, y1 + 2, Rgb(0.2f, 0.2f, 0.22f));
            for (int y = y0; y < y1; y++)
            {
                for (int x = cx - 4; x < cx + 4; x++)
                {
                    int band = ((y + (x - cx)) / 3) % 4;
                    Color32 col = band == 0 ? Rgb(0.9f, 0.15f, 0.15f) : band == 2 ? Rgb(0.2f, 0.35f, 0.85f) : Rgb(1f, 1f, 1f);
                    // 원기둥처럼 가장자리를 어둡게.
                    float k = 1f - (0.25f * Mathf.Abs(x - cx + 0.5f) / 4f);
                    c.Set(x, y, Scale(col, k));
                }
            }
            c.Disc(cx, y1 + 1, 4, Rgb(0.85f, 0.85f, 0.85f));
        }

        // ------------------------------------------------------------------
        // 바탕
        // ------------------------------------------------------------------

        private static readonly Color32[] Palette =
        {
            Rgb(0.95f, 0.35f, 0.3f), Rgb(0.3f, 0.6f, 0.95f), Rgb(0.98f, 0.8f, 0.25f), Rgb(0.4f, 0.8f, 0.45f),
            Rgb(0.8f, 0.45f, 0.85f), Rgb(0.98f, 0.6f, 0.2f), Rgb(0.95f, 0.95f, 0.95f),
        };

        private static readonly Color32[] Flowers =
        {
            Rgb(0.95f, 0.3f, 0.35f), Rgb(1f, 0.85f, 0.2f), Rgb(1f, 0.55f, 0.75f), Rgb(0.7f, 0.45f, 0.95f), Rgb(1f, 1f, 1f),
        };

        /// <summary>픽셀 좌표 → 건물 가운데 기준 월드 좌표.</summary>
        private static Vector2 Local(Canvas c, float px, float py)
        {
            return new Vector2((px - (c.W / 2f)) / Ppu, (py - (c.H / 2f)) / Ppu);
        }

        private static Rect[] ToWorld(Canvas c, List<RectInt> rects)
        {
            var result = new Rect[rects.Count];
            for (int i = 0; i < rects.Count; i++)
            {
                Vector2 min = Local(c, rects[i].xMin, rects[i].yMin);
                result[i] = new Rect(min, new Vector2((float)rects[i].width / Ppu, (float)rects[i].height / Ppu));
            }
            return result;
        }

        private static Color32 Rgb(float r, float g, float b)
        {
            return new Color32((byte)(Mathf.Clamp01(r) * 255), (byte)(Mathf.Clamp01(g) * 255), (byte)(Mathf.Clamp01(b) * 255), 255);
        }

        private static Color32 Scale(Color32 c, float k)
        {
            return new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), c.a);
        }

        private static Color32 Lerp(Color32 a, Color32 b, float t)
        {
            return Color32.Lerp(a, b, t);
        }

        private static float Noise(int x, int y)
        {
            int h = ((x * 73856093) ^ (y * 19349663)) & 0x7fffffff;
            return (h % 1000) / 1000f;
        }

        /// <summary>픽셀 한 장(아래가 y=0). 밖을 칠하면 무시한다.</summary>
        private sealed class Canvas
        {
            public readonly int W;
            public readonly int H;
            private readonly Color32[] _px;

            public Canvas(int w, int h)
            {
                W = w;
                H = h;
                _px = new Color32[w * h];
            }

            public void Set(int x, int y, Color32 c)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) return;
                _px[(y * W) + x] = c;
            }

            public Color32 Get(int x, int y)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) return new Color32(0, 0, 0, 0);
                return _px[(y * W) + x];
            }

            public void Fill(int x0, int y0, int x1, int y1, Color32 c)
            {
                for (int y = y0; y < y1; y++)
                {
                    for (int x = x0; x < x1; x++) Set(x, y, c);
                }
            }

            public void Disc(float cx, float cy, float r, Color32 c)
            {
                Ellipse(cx, cy, r, r, c);
            }

            public void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
            {
                for (int y = (int)(cy - ry - 1); y <= (int)(cy + ry + 1); y++)
                {
                    for (int x = (int)(cx - rx - 1); x <= (int)(cx + rx + 1); x++)
                    {
                        float dx = (x + 0.5f - cx) / rx;
                        float dy = (y + 0.5f - cy) / ry;
                        if ((dx * dx) + (dy * dy) <= 1f) Set(x, y, c);
                    }
                }
            }

            public Sprite ToSprite(string name)
            {
                var texture = new Texture2D(W, H, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = name,
                };
                texture.SetPixels32(_px);
                texture.Apply(false);
                // 씬이 바뀌어도 내려가지 않게 잡아 둔다(Art.KeepAlive와 같은 이유).
                texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), Ppu);
                sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
                return sprite;
            }
        }
    }
}
