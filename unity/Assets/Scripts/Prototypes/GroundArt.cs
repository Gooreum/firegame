using FireGame.Prototypes.Logic;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 픽셀 3D 바닥 한 장: 타일 격자 대신 스테이지 전체를 그린 절차 텍스처(칸당 Ppu 픽셀).
    /// 마을은 풀밭 + 도로 3줄(연석·가운데 점선) + 세로 강과 판자 다리, 숲은 짙은 풀 + 십자 흙길,
    /// 공단은 콘크리트 판 + 가운데 넓은 골목(노란 차선·안전선) + 기름 얼룩. 해시 노이즈라 같은 입력이면 같은 그림.
    /// </summary>
    public static class GroundArt
    {
        public const int Ppu = 16;

        /// <summary>마을 도로 가운데 줄(칸): 가로 y=30(다리를 지난다), 세로 x=18·42(블록 가운데). 덤불 배치도 이 줄을 피한다.</summary>
        public static readonly float[] TownRoadsY = { 30f };
        public static readonly float[] TownRoadsX = { 18f, 42f };
        private const float RoadHalf = 1.1f;
        private const float Curb = 0.25f;

        /// <summary>공단 골목(y=30) 반폭에서 도로 반폭을 뺀 값: 골목은 폭 9의 아스팔트.</summary>
        private const float AlleyExtra = SurvivorFactory.AlleyHalf - RoadHalf;

        /// <summary>스테이지 번호별로 한 장씩 만들어 둔다(0은 비움).</summary>
        private static readonly Sprite[] Cached = new Sprite[FireGame.Prototypes.Logic.SurvivorStages.Count + 1];

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
                    Color c = stage == 2 ? Forest(px, py, x, y, mid, pathHalf) : stage == 3 ? Factory(px, py, x, y, mid) : stage == 4 ? Harbor(px, py, x, y, mid) : stage == 5 ? Market(px, py, x, y, mid) : Town(px, py, x, y, mid);
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
            float grain = Hash(px, py);

            // 강(세로, x=30)과 다리(가운데): 다리 판자가 도로 위에 놓인다.
            float river = Mathf.Abs(x - SurvivorTown.RiverX);
            bool bridgeRow = Mathf.Abs(y - mid) < SurvivorTown.BridgeHalf;
            if (bridgeRow && river < SurvivorTown.RiverHalf + 0.5f)
            {
                // 판자: 가로 줄무늬, 0.5칸마다 줄눈, 양 끝은 난간(어둡게).
                var plank = new Color(0.52f, 0.37f, 0.22f) * (0.9f + (0.1f * Noise(px, py, 14)) + (0.05f * grain));
                if (Mathf.Repeat(y, 0.5f) < 0.07f) plank *= 0.7f;
                if (river > SurvivorTown.RiverHalf + 0.3f) plank *= 0.75f;
                return Opaque(plank);
            }
            if (river < SurvivorTown.RiverHalf)
            {
                // 물: 짙은 파랑에 흐르는 결(비스듬한 밝은 줄).
                var water = new Color(0.16f, 0.38f, 0.62f) * (0.92f + (0.1f * Noise(px, py, 18)));
                float ripple = Mathf.Repeat(y + (x * 0.35f) + (Noise(px, py, 9) * 0.4f), 1.4f);
                if (ripple < 0.1f) water = Color.Lerp(water, new Color(0.45f, 0.68f, 0.9f), 0.7f);
                return Opaque(water);
            }
            if (river < SurvivorTown.RiverHalf + 0.3f)
            {
                // 둑: 모래빛 띠.
                return Opaque(new Color(0.62f, 0.56f, 0.42f) * (0.92f + (0.1f * grain)));
            }

            // 도로: 가장 가까운 도로 가운데 줄까지 거리.
            float roadY = Nearest(y, TownRoadsY);
            float roadX = Nearest(x, TownRoadsX);
            float road = Mathf.Min(Mathf.Abs(y - roadY), Mathf.Abs(x - roadX));

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
            return Grass(px, py, grain, 1f);
        }

        /// <summary>공단: 2칸 콘크리트 판(줄눈·판마다 밝기) + 가운데 넓은 골목(y=30, 폭 9)과 저장소로 가는 세로 길(노란 실선 두 줄) + 골목 가장자리 안전선 + 짙은 기름 얼룩.</summary>
        private static Color Factory(int px, int py, float x, float y, float mid)
        {
            float grain = Hash(px, py);
            // 골목은 폭 9: 가운데 줄에서 AlleyExtra만큼은 "거리 0"으로 친다.
            float alley = Mathf.Max(Mathf.Abs(y - mid) - AlleyExtra, 0f);
            float lane = Mathf.Abs(x - mid);
            float road = Mathf.Min(alley, lane);
            Color c;
            if (road < RoadHalf + Curb)
            {
                c = new Color(0.25f, 0.25f, 0.27f) * (0.92f + (0.1f * Noise(px, py, 24)) + (0.05f * grain));
                bool alongY = alley < lane;
                float centre = alongY ? Mathf.Abs(y - mid) : lane;
                bool crossing = alley < RoadHalf && lane < RoadHalf;
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
                // 골목 가장자리(드럼 줄이 선 자리): 노랑·검정 빗금 안전선.
                if (Mathf.Abs(Mathf.Abs(y - mid) - (SurvivorFactory.AlleyHalf + Curb)) < 0.18f) c = Mathf.Repeat(x + y, 1f) < 0.5f ? new Color(0.9f, 0.75f, 0.15f) : new Color(0.15f, 0.15f, 0.15f);
            }
            // 기름 얼룩: 큰 노이즈 봉우리만 어둡게.
            float stain = Noise(px + 431, py + 97, 40);
            if (stain > 0.8f) c = Color.Lerp(c, new Color(0.2f, 0.19f, 0.22f), Mathf.Clamp01((stain - 0.8f) * 6f) * 0.45f);
            return Opaque(c);
        }

        /// <summary>항구: 북쪽(y ≥ SeaFrom) 짙은 바다에 비스듬한 물결 + 부두선 거품 띠, 바다로 뻗은 부두 둘(세로 판자), 부두선 돌 테, 남쪽은 콘크리트 판 + y=30 도로.</summary>
        private static Color Harbor(int px, int py, float x, float y, float mid)
        {
            float grain = Hash(px, py);
            bool pier = false;
            foreach (float pxc in SurvivorHarbor.PierX) pier |= Mathf.Abs(x - pxc) < SurvivorHarbor.PierHalf;
            if (y >= SurvivorHarbor.SeaFrom)
            {
                if (pier && y < SurvivorHarbor.PierTip)
                {
                    // 부두 판자: 세로 줄눈(0.5칸), 가장자리 두 줄은 난간(어둡게).
                    var plank = new Color(0.5f, 0.36f, 0.22f) * (0.9f + (0.1f * Noise(px, py, 14)) + (0.05f * grain));
                    if (Mathf.Repeat(x, 0.5f) < 0.07f) plank *= 0.7f;
                    float edge = float.MaxValue;
                    foreach (float pxc in SurvivorHarbor.PierX) edge = Mathf.Min(edge, SurvivorHarbor.PierHalf - Mathf.Abs(x - pxc));
                    if (edge < 0.25f || y > SurvivorHarbor.PierTip - 0.25f) plank *= 0.75f;
                    return Opaque(plank);
                }
                // 바다: 마을 강보다 짙고, 결은 가로로 길게 흐른다. 부두선·부두 가장자리엔 흰 거품.
                var sea = new Color(0.12f, 0.32f, 0.58f) * (0.92f + (0.1f * Noise(px, py, 22)));
                float ripple = Mathf.Repeat(x + (y * 0.25f) + (Noise(px, py, 9) * 0.5f), 1.6f);
                if (ripple < 0.1f) sea = Color.Lerp(sea, new Color(0.42f, 0.66f, 0.9f), 0.65f);
                float foam = y - SurvivorHarbor.SeaFrom;
                if (pier && y < SurvivorHarbor.PierTip + 0.4f)
                {
                    foreach (float pxc in SurvivorHarbor.PierX) foam = Mathf.Min(foam, Mathf.Abs(x - pxc) - SurvivorHarbor.PierHalf);
                }
                else if (pier) foam = Mathf.Min(foam, y - SurvivorHarbor.PierTip);
                else
                {
                    foreach (float pxc in SurvivorHarbor.PierX)
                    {
                        if (y < SurvivorHarbor.PierTip + 0.4f) foam = Mathf.Min(foam, Mathf.Abs(x - pxc) - SurvivorHarbor.PierHalf);
                    }
                }
                if (foam < 0.5f && Noise(px + 77, py + 13, 6) > 0.35f + (foam * 0.8f)) sea = Color.Lerp(sea, Color.white, 0.75f);
                return Opaque(sea);
            }
            if (y >= SurvivorHarbor.SeaFrom - 1f)
            {
                // 부두선: 밝은 돌 테, 바다 쪽 한 줄은 어둡게.
                var kerb = new Color(0.5f, 0.5f, 0.48f) * (0.95f + (0.06f * grain));
                if (y > SurvivorHarbor.SeaFrom - (2f / Ppu)) kerb *= 0.7f;
                if (Mathf.Repeat(x, 2f) < 0.08f) kerb *= 0.8f;
                return Opaque(kerb);
            }
            float road = Mathf.Abs(y - mid);
            if (road < RoadHalf)
            {
                var asphalt = new Color(0.29f, 0.29f, 0.32f) * (0.92f + (0.1f * Noise(px, py, 24)) + (0.05f * grain));
                if (road < 0.07f && Mathf.Repeat(x, 1.5f) < 0.8f) return new Color(0.78f, 0.68f, 0.35f);
                return Opaque(asphalt);
            }
            if (road < RoadHalf + Curb) return Opaque(new Color(0.58f, 0.56f, 0.52f) * (0.95f + (0.06f * grain)));
            // 콘크리트 판(공단보다 밝고 소금기 있는 회백색), 큰 노이즈 봉우리는 물 얼룩.
            const int slab = Ppu * 2;
            int tx = px / slab;
            int ty = py / slab;
            bool joint = (px % slab) == 0 || (py % slab) == 0;
            Color c = new Color(0.6f, 0.6f, 0.57f) * (0.9f + (0.1f * Hash(tx, ty)) + (0.06f * Noise(px, py, 30)) + (0.04f * grain));
            if (joint) c *= 0.82f;
            float stain = Noise(px + 211, py + 307, 36);
            if (stain > 0.82f) c = Color.Lerp(c, new Color(0.38f, 0.42f, 0.46f), Mathf.Clamp01((stain - 0.82f) * 6f) * 0.4f);
            return Opaque(c);
        }

        /// <summary>야시장(밤): 어두운 자갈 돌바닥(1칸 격자, 돌마다 밝기) + 두 점포 줄 사이 골목(y 24~36)은 매끈한 포장, 점포 앞 노란 안내선, 무대 앞 반원 광장, 바닥에 떨어진 색종이 점.</summary>
        private static Color Market(int px, int py, float x, float y, float mid)
        {
            float grain = Hash(px, py);
            float aisleHalf = (SurvivorMarket.RowNorth - SurvivorMarket.RowSouth) / 2f - SurvivorMarket.StallHalf.Y - 0.3f;
            bool aisle = Mathf.Abs(y - mid) < aisleHalf;
            // 무대 앞 광장: 반지름 6 반원(무대 y=8 위쪽).
            float dx = x - mid;
            float dy = y - 10.5f;
            bool apron = dy > 0f && ((dx * dx) + (dy * dy)) < 36f;
            Color c;
            if (aisle || apron)
            {
                // 골목은 따뜻한 벽돌(등불 빛을 받은 듯), 무대 앞은 보랏빛 돌. 점포 앞 1칸은 더 밝다(맵 특색 패스: 전엔 0.27 돌이라 밤에 바닥이 안 읽혔다).
                float edge = aisleHalf - Mathf.Abs(y - mid);
                float lit = edge < 1f ? 0.08f : 0f;
                c = (apron ? new Color(0.42f, 0.38f, 0.42f) : new Color(0.5f + lit, 0.4f + lit, 0.33f + lit)) * (0.92f + (0.1f * Noise(px, py, 26)) + (0.04f * grain));
                if (!apron && ((px / 8) + ((py / 4) % 2 == 0 ? 0 : 4)) % 8 == 0) c *= 0.85f;   // 벽돌 줄눈
                // 점포 앞 노란 안내선(골목 양 가장자리).
                if (Mathf.Abs(Mathf.Abs(y - mid) - (aisleHalf - 0.25f)) < 0.07f) c = new Color(0.8f, 0.68f, 0.25f);
            }
            else
            {
                int tx = px / Ppu;
                int ty = py / Ppu;
                bool joint = (px % Ppu) == 0 || (py % Ppu) == 0;
                c = new Color(0.36f, 0.34f, 0.38f) * (0.86f + (0.16f * Hash(tx, ty)) + (0.05f * Noise(px, py, 20)) + (0.04f * grain));
                if (joint) c *= 0.75f;
            }
            // 색종이: 드문 점이 분홍·금·청록으로 반짝인다.
            if (grain > 0.9975f)
            {
                float pick = Hash(px + 5, py + 9);
                c = pick < 0.33f ? new Color(0.9f, 0.4f, 0.6f) : pick < 0.66f ? new Color(0.95f, 0.8f, 0.3f) : new Color(0.4f, 0.85f, 0.85f);
            }
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
