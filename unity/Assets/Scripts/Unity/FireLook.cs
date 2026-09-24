using FireGame.Core.Game;
using FireGame.Core.Grid;
using FireGame.Core.Sim;
using UnityEngine;

namespace FireGame.UnityLayer
{
    /// <summary>불 한 등급을 그리는 데 쓰는 색 묶음.</summary>
    public readonly struct FirePalette
    {
        /// <summary>HUD 띠·조준 표시에 쓰는 대표 색.</summary>
        public readonly Color Mark;

        /// <summary>몸통. 열이 낮을 때와 높을 때.</summary>
        public readonly Color BodyDim;
        public readonly Color BodyHot;

        /// <summary>위로 솟는 불길. 열이 낮을 때와 높을 때.</summary>
        public readonly Color TongueDim;
        public readonly Color TongueHot;

        /// <summary>겹쳐 얹는 질감.</summary>
        public readonly Color Texture;

        /// <summary>연기.</summary>
        public readonly Color Smoke;

        public FirePalette(Color mark, Color bodyDim, Color bodyHot, Color tongueDim, Color tongueHot, Color texture, Color smoke)
        {
            Mark = mark;
            BodyDim = bodyDim;
            BodyHot = bodyHot;
            TongueDim = tongueDim;
            TongueHot = tongueHot;
            Texture = texture;
            Smoke = smoke;
        }
    }

    /// <summary>
    /// 화재 등급과 약제의 색.
    ///
    /// 불꽃·조준 미리보기·HUD 장비 버튼이 모두 여기서 색을 받는다.
    /// 그래야 "슬롯 띠와 같은 색 불에 쏜다"가 규칙이 되어,
    /// 설명을 읽지 않아도 어떤 소화기를 들어야 하는지 알 수 있다.
    ///
    /// 색은 등급마다 손으로 잡았다. 대표 색 하나에서 계산으로 뽑으면
    /// 파랑이 섞여 들어가 주황이 살구색으로, 빨강이 분홍으로 빠진다.
    /// </summary>
    public static class FireLook
    {
        /// <summary>나무·종이. 흔한 불이라 예전 주황을 한 톤도 바꾸지 않았다.</summary>
        public static readonly FirePalette A = new FirePalette(
            new Color(1f, 0.55f, 0.15f),
            new Color(0.95f, 0.28f, 0.05f, 0.75f), new Color(1f, 0.5f, 0.08f, 0.85f),
            new Color(1f, 0.45f, 0.08f), new Color(1f, 0.8f, 0.3f),
            new Color(1f, 0.85f, 0.3f),
            new Color(0.25f, 0.25f, 0.27f, 0.4f));

        /// <summary>유류. 진한 핏빛에 검은 연기. 파랑을 바짝 눌러 분홍으로 빠지지 않게 한다.</summary>
        public static readonly FirePalette B = new FirePalette(
            new Color(0.92f, 0.13f, 0.14f),
            new Color(0.7f, 0.05f, 0.04f, 0.8f), new Color(1f, 0.22f, 0.08f, 0.9f),
            new Color(0.85f, 0.1f, 0.06f), new Color(1f, 0.42f, 0.14f),
            new Color(1f, 0.45f, 0.25f),
            new Color(0.08f, 0.07f, 0.07f, 0.62f));

        /// <summary>전기. 푸르게 튄다. 연기는 옅다.</summary>
        public static readonly FirePalette C = new FirePalette(
            new Color(0.3f, 0.7f, 1f),
            new Color(0.1f, 0.35f, 0.85f, 0.72f), new Color(0.35f, 0.7f, 1f, 0.85f),
            new Color(0.2f, 0.5f, 1f), new Color(0.65f, 0.9f, 1f),
            new Color(0.75f, 0.95f, 1f),
            new Color(0.3f, 0.32f, 0.36f, 0.3f));

        public static FirePalette Palette(FireClass fireClass)
        {
            switch (fireClass)
            {
                case FireClass.B: return B;
                case FireClass.C: return C;
                default: return A;
            }
        }

        /// <summary>등급의 대표 색.</summary>
        public static Color Of(FireClass fireClass)
        {
            return Palette(fireClass).Mark;
        }

        /// <summary>그 약제가 잡는 등급의 색. 슬롯 띠에 쓴다.</summary>
        public static Color OfAgent(AgentType agent)
        {
            return Of(AgentAdvice.BestClassFor(agent));
        }

        /// <summary>조준 미리보기 칸 색. 초록 잘 듣는다 / 노랑 약하다 / 회색 안 듣는다 / 빨강 역효과.</summary>
        public static Color Verdict(AgentVerdict verdict)
        {
            switch (verdict)
            {
                case AgentVerdict.Good: return new Color(0.25f, 1f, 0.45f);
                case AgentVerdict.Weak: return new Color(1f, 0.9f, 0.3f);
                case AgentVerdict.Backfire: return new Color(1f, 0.15f, 0.15f);
                default: return new Color(0.7f, 0.7f, 0.72f);
            }
        }
    }
}
