using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 코드로 그리는 불꽃 플립북. 외부 그림 없이 눈물방울 모양(아래 둥글고 위 뾰족) + 프레임마다 다른 흔들림으로 시트를 만든다.
    /// 불씨·큰 불·다트·기름·지붕·바닥 불이 모두 이 시트를 쓴다. 나중에 손그림 시트로 바꿀 때는 Sheet를 그림 로더로 갈아 끼우면 된다.
    /// </summary>
    public static class FlameArt
    {
        public const int Width = 96;
        public const int Height = 128;

        /// <summary>
        /// frames장짜리 불꽃 시트. width는 밑동 반폭(0~1, 1이면 그림 폭 끝까지), wobble은 끝이 좌우로 휘는 양(0~1).
        /// outer는 바깥 불꽃 색, inner는 안쪽 심 색(위로 갈수록 흰빛). 밑동은 base 색으로 어둡게(큰 불·기름).
        /// </summary>
        public static Sprite[] Sheet(string name, int frames, float width, float wobble, Color outer, Color inner, Color? baseTint = null, bool point = false)
        {
            var sheet = new Sprite[frames];
            for (int f = 0; f < frames; f++)
            {
                float phase = f / (float)frames * Mathf.PI * 2f;
                var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { name = name + f, filterMode = point ? FilterMode.Point : FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[Width * Height];
                for (int y = 0; y < Height; y++)
                {
                    float v = y / (float)(Height - 1);          // 0 밑동 → 1 끝
                    // 눈물방울 반폭: 아래 둥글고(4v(1−v)의 제곱근) 위로 갈수록 가늘다.
                    float profile = Mathf.Sqrt(Mathf.Max(0f, 4f * v * (1f - v))) * (1f - (0.45f * v));
                    // 흔들림: 끝으로 갈수록 좌우로 휘고, 두 파장의 노이즈가 프레임마다 돈다.
                    float sway = wobble * v * v * (Mathf.Sin(phase + (v * 4f)) * 0.7f + Mathf.Sin((phase * 2f) + (v * 9f) + 1.3f) * 0.3f);
                    float ripple = 1f + (0.12f * Mathf.Sin((phase * 3f) + (v * 14f)));
                    float half = width * profile * ripple;
                    float tip = 1f - Mathf.Clamp01((v - 0.92f) / 0.08f);  // 끝 2% 는 투명하게 닫는다
                    for (int x = 0; x < Width; x++)
                    {
                        float u = ((x + 0.5f) / Width * 2f) - 1f;   // −1 ~ 1
                        float d = Mathf.Abs(u - sway);
                        float edge = Mathf.Clamp01((half - d) / 0.08f) * tip;
                        if (y == 0) edge = 0f;                       // 맨 아랫줄은 비워 발이 뜨지 않게
                        if (edge <= 0f)
                        {
                            pixels[(y * Width) + x] = new Color32(0, 0, 0, 0);
                            continue;
                        }
                        // 안쪽 심: 밑동 가까이에서 더 넓고 위로 갈수록 희다.
                        float coreHalf = half * (0.55f - (0.15f * v));
                        float core = Mathf.Clamp01((coreHalf - d) / 0.1f);
                        Color c = Color.Lerp(outer, inner, core);
                        c = Color.Lerp(c, Color.white, core * v * 0.6f);
                        if (baseTint.HasValue) c = Color.Lerp(c, baseTint.Value, Mathf.Clamp01((0.22f - v) / 0.22f));
                        // 바깥 가장자리는 조금 어둡고 투명해 테두리가 생긴다.
                        float rim = Mathf.Clamp01((half - d) / 0.2f);
                        c = Color.Lerp(c * 0.75f, c, rim);
                        c.a = edge * Mathf.Lerp(0.85f, 1f, rim);
                        pixels[(y * Width) + x] = c;
                    }
                }
                tex.SetPixels32(pixels);
                tex.Apply(false, true);
                sheet[f] = Sprite.Create(tex, new Rect(0f, 0f, Width, Height), new Vector2(0.5f, 0.5f), 64f);
                sheet[f].name = name + f;
            }
            return sheet;
        }

        /// <summary>시간과 씨앗으로 프레임을 고른다(초당 fps장).</summary>
        public static Sprite Frame(Sprite[] sheet, float time, int seed, float fps = 12f)
        {
            int n = sheet.Length;
            int i = (int)((time * fps) + (seed * 7)) % n;
            return sheet[i < 0 ? i + n : i];
        }
    }
}
