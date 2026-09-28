using System;
using System.Collections.Generic;

namespace FireGame.Prototypes.Logic
{
    /// <summary>
    /// 폰 물 조준 보정. 엄지로 끈 방향은 대충이어도 된다: 그 근처 불 쪽으로 살짝 끌어당기고,
    /// 끌지 않고 누르기만 하면 가장 가까운 불을 겨눈다. 방향은 한 번에 튀지 않고 부드럽게 돈다.
    /// </summary>
    public static class AimAssist
    {
        /// <summary>이 거리 안의 불만 노린다.</summary>
        public const float Range = 10f;

        /// <summary>끈 방향에서 이 각도(도) 안에 있는 불만 끌어당긴다.</summary>
        public const float Cone = 30f;

        /// <summary>끌어당기는 세기. 0이면 끈 방향 그대로, 1이면 정확히 불 쪽.</summary>
        public const float Pull = 0.75f;

        /// <summary>초당 최대 회전(도).</summary>
        public const float TurnRate = 900f;

        /// <summary>
        /// 원하는 조준 방향(단위 벡터). dragged가 false(누르기만)면 가장 가까운 불, 불이 없으면 지금 방향 그대로.
        /// dragged가 true면 끈 방향 ±Cone 안에서 "거리 + 각도×0.1"이 가장 작은 불 쪽으로 Pull만큼 당긴다.
        /// </summary>
        public static Vec2 Desired(Vec2 player, Vec2 current, bool dragged, Vec2 stick, IReadOnlyList<Vec2> fires)
        {
            if (!dragged)
            {
                Vec2? nearest = null;
                float best = Range;
                foreach (Vec2 f in fires)
                {
                    float d = player.DistanceTo(f);
                    if (d < 0.01f || d > best) continue;
                    best = d;
                    nearest = f;
                }
                return nearest.HasValue ? Unit(new Vec2(nearest.Value.X - player.X, nearest.Value.Y - player.Y), current) : Unit(current, new Vec2(1f, 0f));
            }

            Vec2 aim = Unit(stick, current);
            float aimDeg = Deg(aim);
            float bestScore = float.MaxValue;
            float bestOff = 0f;
            foreach (Vec2 f in fires)
            {
                float d = player.DistanceTo(f);
                if (d < 0.01f || d > Range) continue;
                float off = Wrap(Deg(new Vec2(f.X - player.X, f.Y - player.Y)) - aimDeg);
                if (Math.Abs(off) > Cone) continue;
                float score = d + (Math.Abs(off) * 0.1f);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestOff = off;
                }
            }
            return bestScore < float.MaxValue ? FromDeg(aimDeg + (bestOff * Pull)) : aim;
        }

        /// <summary>current에서 desired 쪽으로 최대 maxDeg만 돈 단위 벡터.</summary>
        public static Vec2 Turn(Vec2 current, Vec2 desired, float maxDeg)
        {
            Vec2 from = Unit(current, desired);
            float a = Deg(from);
            float off = Wrap(Deg(Unit(desired, from)) - a);
            if (Math.Abs(off) <= maxDeg) return Unit(desired, from);
            return FromDeg(a + (Math.Sign(off) * maxDeg));
        }

        private static Vec2 Unit(Vec2 v, Vec2 fallback)
        {
            float d = (float)Math.Sqrt((v.X * v.X) + (v.Y * v.Y));
            if (d < 0.0001f) return fallback;
            return new Vec2(v.X / d, v.Y / d);
        }

        private static float Deg(Vec2 v)
        {
            return (float)(Math.Atan2(v.Y, v.X) * 180.0 / Math.PI);
        }

        private static Vec2 FromDeg(float deg)
        {
            double r = deg * Math.PI / 180.0;
            return new Vec2((float)Math.Cos(r), (float)Math.Sin(r));
        }

        /// <summary>-180~180도로 접는다.</summary>
        private static float Wrap(float deg)
        {
            while (deg > 180f) deg -= 360f;
            while (deg < -180f) deg += 360f;
            return deg;
        }
    }
}
