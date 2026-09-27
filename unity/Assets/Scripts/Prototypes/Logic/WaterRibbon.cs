using System;
using System.Collections.Generic;

namespace FireGame.Prototypes.Logic
{
    /// <summary>
    /// 물줄기 모양: 물방울 사슬 점을 매끈한 리본(가운데 선·법선·반폭)과 끝에서 부서지는 물덩어리로 바꾼다.
    /// 그림만 정하고 판정에는 쓰지 않는다. 노즐 쪽은 곧고 끝으로 갈수록 크게 출렁인다.
    /// </summary>
    public static class WaterRibbon
    {
        public struct Point
        {
            public Vec2 Pos;
            /// <summary>진행 방향의 왼쪽(단위 벡터).</summary>
            public Vec2 Normal;
            public float Half;
            /// <summary>노즐에서 이 점까지 가운데 선 길이(칸).</summary>
            public float Along;
        }

        public struct Blob
        {
            public Vec2 Pos;
            public float Radius;
            public float Alpha;
        }

        /// <summary>재샘플 간격(칸).</summary>
        public const float Step = 0.15f;

        /// <summary>전체 길이에서 이 비율부터 리본 대신 물덩어리로 부서진다.</summary>
        public const float BreakAt = 0.78f;

        /// <summary>물덩어리 사이 간격(칸).</summary>
        public const float BlobGap = 0.35f;

        /// <summary>물 속도(칸/초). 물덩어리가 이 속도로 끝을 향해 흘러간다.</summary>
        public const float FlowSpeed = 16f;

        private static readonly List<Vec2> Clean = new List<Vec2>();
        private static readonly List<Vec2> Center = new List<Vec2>();
        private static readonly List<float> Alongs = new List<float>();
        private static readonly List<Point> All = new List<Point>();

        /// <param name="pts">노즐 → 끝 순서의 사슬 점(월드 좌표).</param>
        /// <param name="width">기준 굵기(칸).</param>
        /// <param name="wobble">출렁임 세기(1 = 호스, 낮을수록 곧다).</param>
        /// <remarks>점이 2개 미만이거나 총길이가 0.05칸 미만이면 둘 다 비운다.</remarks>
        public static void Build(IReadOnlyList<Vec2> pts, float width, float time, float wobble, List<Point> ribbon, List<Blob> blobs)
        {
            ribbon.Clear();
            blobs.Clear();
            if (pts == null) return;

            // 같은 자리에 겹친 점은 방향을 못 정하니 뺀다.
            Clean.Clear();
            foreach (Vec2 p in pts)
            {
                if (Clean.Count == 0 || p.DistanceTo(Clean[Clean.Count - 1]) > 0.001f) Clean.Add(p);
            }
            if (Clean.Count < 2) return;
            float rough = 0f;
            for (int i = 1; i < Clean.Count; i++) rough += Clean[i].DistanceTo(Clean[i - 1]);
            if (rough < 0.05f) return;

            // 1) 꺾인 사슬을 Catmull-Rom 곡선으로 잇고 촘촘히 다시 찍는다.
            Center.Clear();
            for (int i = 0; i < Clean.Count - 1; i++)
            {
                Vec2 p1 = Clean[i];
                Vec2 p2 = Clean[i + 1];
                Vec2 p0 = i > 0 ? Clean[i - 1] : Sub(Scale(p1, 2f), p2);
                Vec2 p3 = i + 2 < Clean.Count ? Clean[i + 2] : Sub(Scale(p2, 2f), p1);
                int n = Math.Max(1, (int)Math.Ceiling(p1.DistanceTo(p2) / (Step * 0.9f)));
                for (int k = 0; k < n; k++) Center.Add(CatmullRom(p0, p1, p2, p3, (float)k / n));
            }
            Center.Add(Clean[Clean.Count - 1]);

            Alongs.Clear();
            float total = 0f;
            for (int i = 0; i < Center.Count; i++)
            {
                if (i > 0) total += Center[i].DistanceTo(Center[i - 1]);
                Alongs.Add(total);
            }

            // 2) 가운데 선 법선으로 옆 출렁임: 느린 큰 굽이 + 빠른 잔물결이 끝 쪽으로 흘러간다.
            All.Clear();
            for (int i = 0; i < Center.Count; i++)
            {
                float along = Alongs[i];
                Vec2 n = NormalAt(Center, i);
                float sway = Sway(along, total, width, time, wobble);
                All.Add(new Point
                {
                    Pos = Add(Center[i], Scale(n, sway)),
                    Half = HalfAt(width, along, total, time),
                    Along = along,
                });
            }

            // 3) 출렁인 선 기준으로 법선을 다시 잡아야 굽이에서 폭이 일정하다.
            for (int i = 0; i < All.Count; i++)
            {
                Point p = All[i];
                p.Normal = NormalAt(All, i);
                All[i] = p;
            }

            // 4) 끝 22%는 리본이 아니라 흘러가며 작아지고 옅어지는 물덩어리.
            float breakAlong = total * BreakAt;
            foreach (Point p in All)
            {
                if (p.Along <= breakAlong) ribbon.Add(p);
            }
            if (ribbon.Count < 2)
            {
                ribbon.Clear();
                return;
            }

            float tail = total - breakAlong;
            int index = 0;
            for (float d = breakAlong + Repeat(time * FlowSpeed, BlobGap) + (BlobGap * 0.5f); d < total; d += BlobGap, index++)
            {
                float k = (d - breakAlong) / tail;
                Point at = Sample(All, d);
                float half = HalfAt(width, d, total, time);
                // 덩어리마다 옆으로 다르게 흩어진다. 멀수록 더 벌어진다.
                float scatter = (float)Math.Sin((time * 9f) + (index * 2.3f)) * half * 1.6f * k;
                blobs.Add(new Blob
                {
                    Pos = Add(at.Pos, Scale(at.Normal, scatter)),
                    Radius = half * Lerp(0.9f, 0.35f, k),
                    Alpha = 1f - (0.85f * k),
                });
            }
        }

        /// <summary>물줄기 굵기: 노즐 구멍만큼 가늘게 나와 2칸 안에 제 굵기가 되고, 끝으로 갈수록 조금 더 벌어진다.</summary>
        public static float Width(float w, float along, float total)
        {
            float open = Lerp(0.16f, w * 0.75f, SmoothStep(Clamp01(along / 2f)));
            return open * Lerp(1f, 1.4f, total > 0f ? Clamp01(along / total) : 0f);
        }

        /// <summary>반폭: 굵기 곡선 × 앞으로 흘러가는 맥동(노즐 1칸 안은 맥동이 없다).</summary>
        private static float HalfAt(float width, float along, float total, float time)
        {
            float pulse = 1f + (0.12f * (float)Math.Sin((time * 15f) - (along * 2.2f)) * Clamp01(along));
            return Width(width, along, total) * 0.5f * pulse;
        }

        /// <summary>옆으로 벗어나는 거리(칸). 끝으로 갈수록 제곱으로 커지고, 노즐 2칸 안은 거의 곧다.</summary>
        private static float Sway(float along, float total, float width, float time, float wobble)
        {
            float u = total > 0f ? along / total : 0f;
            float wave = ((float)Math.Sin((time * 7f) - (along * 0.9f)) * 0.22f) + ((float)Math.Sin((time * 23f) - (along * 3.1f)) * 0.06f);
            return wave * width * wobble * u * u * Clamp01(along / 2f);
        }

        private static Vec2 NormalAt(List<Vec2> line, int i)
        {
            Vec2 d = Sub(line[Math.Min(i + 1, line.Count - 1)], line[Math.Max(i - 1, 0)]);
            return LeftOf(d);
        }

        private static Vec2 NormalAt(List<Point> line, int i)
        {
            Vec2 d = Sub(line[Math.Min(i + 1, line.Count - 1)].Pos, line[Math.Max(i - 1, 0)].Pos);
            Vec2 n = LeftOf(d);
            return n.X == 0f && n.Y == 0f && i > 0 ? line[i - 1].Normal : n;
        }

        private static Vec2 LeftOf(Vec2 d)
        {
            float len = (float)Math.Sqrt((d.X * d.X) + (d.Y * d.Y));
            return len < 1e-6f ? new Vec2(0f, 0f) : new Vec2(-d.Y / len, d.X / len);
        }

        /// <summary>가운데 선 길이 d 자리의 점(앞뒤 점 사이를 잇는다).</summary>
        private static Point Sample(List<Point> line, float d)
        {
            for (int i = 1; i < line.Count; i++)
            {
                if (line[i].Along < d) continue;
                Point a = line[i - 1];
                Point b = line[i];
                float span = b.Along - a.Along;
                float t = span > 1e-6f ? (d - a.Along) / span : 0f;
                return new Point { Pos = Add(a.Pos, Scale(Sub(b.Pos, a.Pos), t)), Normal = b.Normal, Half = Lerp(a.Half, b.Half, t), Along = d };
            }
            return line[line.Count - 1];
        }

        private static Vec2 CatmullRom(Vec2 p0, Vec2 p1, Vec2 p2, Vec2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            float x = 0.5f * ((2f * p1.X) + ((-p0.X + p2.X) * t) + (((2f * p0.X) - (5f * p1.X) + (4f * p2.X) - p3.X) * t2) + ((-p0.X + (3f * p1.X) - (3f * p2.X) + p3.X) * t3));
            float y = 0.5f * ((2f * p1.Y) + ((-p0.Y + p2.Y) * t) + (((2f * p0.Y) - (5f * p1.Y) + (4f * p2.Y) - p3.Y) * t2) + ((-p0.Y + (3f * p1.Y) - (3f * p2.Y) + p3.Y) * t3));
            return new Vec2(x, y);
        }

        private static Vec2 Add(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        private static Vec2 Sub(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        private static Vec2 Scale(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);
        private static float Lerp(float a, float b, float t) => a + ((b - a) * t);
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        private static float SmoothStep(float t) => t * t * (3f - (2f * t));
        private static float Repeat(float v, float len) => v - ((float)Math.Floor(v / len) * len);
    }
}
