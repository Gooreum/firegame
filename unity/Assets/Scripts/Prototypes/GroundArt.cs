using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 픽셀 3D 바닥 한 장: 타일 격자 대신 스테이지 전체를 그린 절차 텍스처(칸당 Ppu 픽셀).
    /// 마을은 풀밭 + 도로 4줄(연석·가운데 점선) + 석판 광장, 숲은 짙은 풀 + 십자 흙길, 공단은 콘크리트 판 + 노란 차선 + 기름 얼룩.
    /// 해시 노이즈라 같은 입력이면 같은 그림.
    /// </summary>
    public static class GroundArt
    {
        public const int Ppu = 16;

        /// <summary>마을 도로 가운데 줄(칸): 가로 y=25·37, 세로 x=23·37.</summary>
        private static readonly float[] RoadsY = { 25f, 37f };
        private static readonly float[] RoadsX = { 23f, 37f };
        private const float RoadHalf = 1.1f;
        private const float Curb = 0.25f;
        private const float PlazaHalf = 8f;

        /// <summary>스테이지 번호별로 한 장씩 만들어 둔다(0은 비움).</summary>
        private static readonly Sprite[] Cached = new Sprite[4];

        /// <summary>스테이지 바닥 한 장(가운데 = mid). 스테이지마다 한 번만 만들고 재사용한다. 모르는 번호는 마을.</summary>
        public static Sprite Paint(int stage, int size, float mid, float pathHalf)
        {
            if (stage < 1 || stage >= Cached.Length) stage = 1;
            if (Cached[stage] != null) return Cached[stage];

            int n = size * Ppu;
            var pixels = new Color32[n * n];
            for (int py = 0; py < n; py++)
            {
                for (int px = 0; px < n; px++)
                {
                    float x = (px + 0.5f) / Ppu;
                    float y = (py + 0.5f) / Ppu;
                    Color c = stage == 2 ? Forest(px, py, x, y, mid, pathHalf) : stage == 3 ? Factory(px, py, x, y, mid) : Town(px, py, x, y, mid);
                    pixels[(py * n) + px] = c;
                }
            }
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Ground" + stage,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), Ppu);
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Cached[stage] = sprite;
            return sprite;
        }

        private static Color Town(int px, int py, float x, float y, float mid)
        {
            // 도로: 가장 가까운 도로 가운데 줄까지 거리.
            float roadY = Nearest(y, RoadsY);
            float roadX = Nearest(x, RoadsX);
            float road = Mathf.Min(Mathf.Abs(y - roadY), Mathf.Abs(x - roadX));
            bool plaza = Mathf.Abs(x - mid) < PlazaHalf && Mathf.Abs(y - mid) < PlazaHalf;
            float grain = Hash(px, py);

            if (road < RoadHalf)
            {
                var asphalt = new Color(0.29f, 0.29f, 0.32f);
                asphalt *= 0.92f + (0.1f * Noise(px, py, 24)) + (0.05f * grain);
                // 가운데 노란 점선: 교차로 안은 비운다.
                bool alongY = Mathf.Abs(y - roadY) < Mathf.Abs(x - roadX);
                float centre = alongY ? Mathf.Abs(y - roadY) : Mathf.Abs(x - roadX);
                float run = alongY ? x : y;
                bool crossing = Mathf.Abs(y - roadY) < RoadHalf && Mathf.Abs(x - roadX) < RoadHalf;
                if (!crossing && centre < 0.07f && Mathf.Repeat(run, 1.5f) < 0.8f) return new Color(0.78f, 0.68f, 0.35f);
                return Opaque(asphalt);
            }
            if (road < RoadHalf + Curb)
            {
                // 연석: 밝은 돌 줄, 도로 쪽 한 줄은 어둡게.
                var curb = new Color(0.58f, 0.56f, 0.52f) * (0.95f + (0.06f * grain));
                if (road < RoadHalf + (1f / Ppu)) curb *= 0.7f;
                return Opaque(curb);
            }
            if (plaza)
            {
                // 석판: 0.5칸 줄눈, 판마다 밝기가 조금씩 다르다.
                const int slab = Ppu / 2;
                int tx = px / slab;
                int sy = py + ((tx & 1) * (slab / 2));
                int ty = sy / slab;
                bool joint = (px % slab) == 0 || (sy % slab) == 0;
                var stone = new Color(0.5f, 0.46f, 0.41f) * (0.9f + (0.12f * Hash(tx, ty)) + (0.04f * grain));
                if (joint) stone *= 0.78f;
                return Opaque(stone);
            }
            return Grass(px, py, grain, 1f);
        }

        /// <summary>공단: 2칸 콘크리트 판(줄눈·판마다 밝기) + 마을 자리의 아스팔트 길(노란 실선 두 줄) + 짙은 기름 얼룩.</summary>
        private static Color Factory(int px, int py, float x, float y, float mid)
        {
            float grain = Hash(px, py);
            float roadY = Nearest(y, RoadsY);
            float roadX = Nearest(x, RoadsX);
            float road = Mathf.Min(Mathf.Abs(y - roadY), Mathf.Abs(x - roadX));
            Color c;
            if (road < RoadHalf + Curb)
            {
                c = new Color(0.25f, 0.25f, 0.27f) * (0.92f + (0.1f * Noise(px, py, 24)) + (0.05f * grain));
                bool alongY = Mathf.Abs(y - roadY) < Mathf.Abs(x - roadX);
                float centre = alongY ? Mathf.Abs(y - roadY) : Mathf.Abs(x - roadX);
                bool crossing = Mathf.Abs(y - roadY) < RoadHalf && Mathf.Abs(x - roadX) < RoadHalf;
                // 가운데 노란 실선 두 줄(공장 지대 차선).
                if (!crossing && centre > 0.06f && centre < 0.14f) c = new Color(0.85f, 0.7f, 0.2f);
            }
            else
            {
                const int slab = Ppu * 2;
                int tx = px / slab;
                int ty = py / slab;
                bool joint = (px % slab) == 0 || (py % slab) == 0;
                c = new Color(0.56f, 0.56f, 0.54f) * (0.9f + (0.1f * Hash(tx, ty)) + (0.06f * Noise(px, py, 30)) + (0.04f * grain));
                if (joint) c *= 0.8f;
                // 광장 가장자리: 노랑·검정 빗금 안전선.
                float edge = Mathf.Max(Mathf.Abs(x - mid), Mathf.Abs(y - mid));
                if (Mathf.Abs(edge - PlazaHalf) < 0.18f) c = Mathf.Repeat(x + y, 1f) < 0.5f ? new Color(0.9f, 0.75f, 0.15f) : new Color(0.15f, 0.15f, 0.15f);
            }
            // 기름 얼룩: 큰 노이즈 봉우리만 어둡게.
            float stain = Noise(px + 431, py + 97, 40);
            if (stain > 0.8f) c = Color.Lerp(c, new Color(0.2f, 0.19f, 0.22f), Mathf.Clamp01((stain - 0.8f) * 6f) * 0.45f);
            return Opaque(c);
        }

        private static Color Forest(int px, int py, float x, float y, float mid, float pathHalf)
        {
            float grain = Hash(px, py);
            float path = Mathf.Min(Mathf.Abs(x - mid), Mathf.Abs(y - mid));
            // 흙길 가장자리는 노이즈로 들쭉날쭉 풀과 섞인다.
            float edge = pathHalf + ((Noise(px, py, 10) - 0.5f) * 0.8f);
            Color grass = Grass(px, py, grain, 0.82f);
            if (path > edge) return grass;
            var dirt = new Color(0.45f, 0.36f, 0.26f) * (0.88f + (0.14f * Noise(px, py, 20)) + (0.05f * grain));
            if (grain > 0.97f) dirt = new Color(0.58f, 0.5f, 0.4f);
            float blend = Mathf.Clamp01((edge - path) / 0.35f);
            return Opaque(Color.Lerp(grass, dirt, blend));
        }

        /// <summary>풀밭: 큰 얼룩(밝고 어두운 무리) + 풀잎 점.</summary>
        private static Color Grass(int px, int py, float grain, float light)
        {
            float patch = Noise(px, py, 64);
            float mid = Noise(px + 911, py + 377, 18);
            var grass = Color.Lerp(new Color(0.22f, 0.33f, 0.2f), new Color(0.33f, 0.43f, 0.23f), (patch * 0.7f) + (mid * 0.3f));
            if (grain > 0.975f) grass *= 1.12f;
            else if (grain < 0.03f) grass *= 0.88f;
            // 드문 꽃·잎 점.
            if (grain > 0.9985f) grass = new Color(0.85f, 0.8f, 0.45f);
            return Opaque(grass * light);
        }

        private static float Nearest(float v, float[] lines)
        {
            float best = lines[0];
            for (int i = 1; i < lines.Length; i++)
            {
                if (Mathf.Abs(v - lines[i]) < Mathf.Abs(v - best)) best = lines[i];
            }
            return best;
        }

        private static Color Opaque(Color c)
        {
            c.a = 1f;
            return c;
        }

        /// <summary>값 노이즈: cell 픽셀 격자 꼭짓점 해시를 부드럽게 보간(0~1).</summary>
        private static float Noise(int x, int y, int cell)
        {
            int gx = x / cell;
            int gy = y / cell;
            float fx = (x % cell) / (float)cell;
            float fy = (y % cell) / (float)cell;
            fx = fx * fx * (3f - (2f * fx));
            fy = fy * fy * (3f - (2f * fy));
            float a = Mathf.Lerp(Hash(gx, gy), Hash(gx + 1, gy), fx);
            float b = Mathf.Lerp(Hash(gx, gy + 1), Hash(gx + 1, gy + 1), fx);
            return Mathf.Lerp(a, b, fy);
        }

        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = ((uint)x * 374761393u) + ((uint)y * 668265263u);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
