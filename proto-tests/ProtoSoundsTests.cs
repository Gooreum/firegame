using System;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>코드로 만드는 소리: 길이·진폭·모양이 설명대로다.</summary>
    public class ProtoSoundsTests
    {
        private static float Loudness(float[] s, float from, float to)
        {
            int a = (int)(from * ProtoSounds.Rate);
            int b = Math.Min(s.Length, (int)(to * ProtoSounds.Rate));
            float sum = 0f;
            for (int i = a; i < b; i++) sum += Math.Abs(s[i]);
            return sum / Math.Max(1, b - a);
        }

        [Fact]
        public void SteamBurst_IsAShortHiss_ThatFadesOut()
        {
            float[] s = ProtoSounds.SteamBurst();
            Assert.InRange(s.Length, (int)(ProtoSounds.Rate * 0.4f), (int)(ProtoSounds.Rate * 0.8f));
            float peak = 0f;
            foreach (float v in s) peak = Math.Max(peak, Math.Abs(v));
            Assert.True(peak <= 1f && peak > 0.5f, "진폭이 0.5~1 사이여야 한다: " + peak);
            float head = Loudness(s, 0f, 0.05f);
            float tail = Loudness(s, s.Length / (float)ProtoSounds.Rate - 0.1f, s.Length / (float)ProtoSounds.Rate);
            Assert.True(head > tail * 3f, "치익 뒤 잦아들어야 한다: 앞 " + head + " 뒤 " + tail);
        }

        [Fact]
        public void SameCode_SameSound()
        {
            Assert.Equal(ProtoSounds.SteamBurst(), ProtoSounds.SteamBurst());
        }
    }
}
