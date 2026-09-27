using System;

namespace FireGame.Prototypes.Logic
{
    /// <summary>
    /// 시험판 소리를 코드로 만든다(샘플 배열만, 유니티 없이). 뷰가 AudioClip으로 감싼다.
    ///
    /// 0.24초짜리 spray_water.wav를 반복 재생했더니 초당 네 번 끊겨 "픽픽" 쏘는 소리로 들렸다.
    /// 호스 물줄기는 끊김 없는 "쏴아아"여야 해서, 걸러 낸 잡음을 길게 만들고 끝과 처음을 겹쳐 이음매를 없앤다.
    /// </summary>
    public static class ProtoSounds
    {
        public const int Rate = 44100;

        /// <summary>호스 루프 2초. 끝 샘플 다음이 첫 샘플로 자연스럽게 이어진다.</summary>
        public static float[] HoseLoop()
        {
            const float seconds = 2f;
            const float fade = 0.25f;
            int n = (int)(Rate * seconds);
            int f = (int)(Rate * fade);
            float[] raw = new float[n + f];

            var rng = new Random(7);
            float lo1 = 0f, lo2 = 0f, rumble = 0f, dc = 0f;
            // 한 극 저역 통과 계수: 약 2.5kHz 두 단(쏴아), 약 250Hz 한 단(묵직한 밑소리).
            float a = 1f - (float)Math.Exp(-2 * Math.PI * 2500 / Rate);
            float b = 1f - (float)Math.Exp(-2 * Math.PI * 250 / Rate);
            float d = 1f - (float)Math.Exp(-2 * Math.PI * 60 / Rate);
            for (int i = 0; i < raw.Length; i++)
            {
                float white = ((float)rng.NextDouble() * 2f) - 1f;
                lo1 += a * (white - lo1);
                lo2 += a * (lo1 - lo2);
                rumble += b * (white - rumble);
                float x = lo2 + (rumble * 1.6f);
                // 아주 낮은 울림은 빼서 스피커가 웅웅거리지 않게 한다.
                dc += d * (x - dc);
                x -= dc;
                // 물살이 울렁인다: 11Hz·17Hz 떨림(2초 주기에 딱 맞아 루프 이음매에서도 어긋나지 않는다).
                double t = (double)i / Rate;
                float wobble = 1f + (0.12f * (float)Math.Sin(2 * Math.PI * 11 * t)) + (0.08f * (float)Math.Sin(2 * Math.PI * 17 * t));
                raw[i] = x * wobble;
            }

            float[] loop = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (i < f)
                {
                    // 앞머리를 꼬리 다음 샘플들과 섞는다: 끝(n-1) 다음에 오는 0번은 원래 n번 자리와 거의 같다.
                    float w = (float)i / f;
                    loop[i] = (raw[i] * w) + (raw[n + i] * (1f - w));
                }
                else
                {
                    loop[i] = raw[i];
                }
            }
            Normalize(loop, 0.8f);
            return loop;
        }

        /// <summary>헬기가 쏟는 물 "촤악" 0.9초: 확 터졌다 잦아드는 거친 물소리 + 굵은 물방울 튀는 소리.</summary>
        public static float[] Splash()
        {
            int n = (int)(Rate * 0.9f);
            float[] s = new float[n];
            var rng = new Random(11);
            float lo = 0f, drop = 0f;
            float a = 1f - (float)Math.Exp(-2 * Math.PI * 1800 / Rate);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float white = ((float)rng.NextDouble() * 2f) - 1f;
                lo += a * (white - lo);
                float env = Math.Min(1f, t / 0.015f) * (float)Math.Exp(-t * 4.5f);
                // 굵은 물방울: 드문드문 짧게 튀는 소리.
                if (rng.NextDouble() < 0.0009) drop = 0.9f;
                drop *= 0.9985f;
                float plip = drop * (float)Math.Sin(2 * Math.PI * (500 + (300 * drop)) * t);
                s[i] = (lo * env * 2.2f) + (plip * 0.35f * (1f - (t / 0.9f)));
            }
            Normalize(s, 0.9f);
            return s;
        }

        private static void Normalize(float[] s, float peak)
        {
            float max = 0f;
            foreach (float v in s) max = Math.Max(max, Math.Abs(v));
            if (max <= 0f) return;
            float k = peak / max;
            for (int i = 0; i < s.Length; i++) s[i] *= k;
        }
    }
}
