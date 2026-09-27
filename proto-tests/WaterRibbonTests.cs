using System;
using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>물줄기 모양. 노즐에서 곧게 가늘게 나와 끝으로 갈수록 굵어지고 출렁이다 물덩어리로 부서진다.</summary>
    public class WaterRibbonTests
    {
        private readonly List<WaterRibbon.Point> _ribbon = new List<WaterRibbon.Point>();
        private readonly List<WaterRibbon.Blob> _blobs = new List<WaterRibbon.Blob>();

        /// <summary>노즐(0,0)에서 오른쪽으로 곧게 뻗은 사슬. 0.1초마다 한 방울씩 나온 물처럼 1칸 간격.</summary>
        private static List<Vec2> Straight(float length = 10f)
        {
            var pts = new List<Vec2>();
            for (int i = 0; i <= (int)length; i++) pts.Add(new Vec2(i, 0f));
            return pts;
        }

        [Fact]
        public void RibbonStartsAtNozzleAndIsSampledFinely()
        {
            WaterRibbon.Build(Straight(), 1f, 0.37f, 1f, _ribbon, _blobs);

            Assert.True(_ribbon.Count > 10);
            Assert.Equal(0f, _ribbon[0].Pos.X, 3);
            Assert.Equal(0f, _ribbon[0].Pos.Y, 3);
            for (int i = 1; i < _ribbon.Count; i++) Assert.True(_ribbon[i].Pos.DistanceTo(_ribbon[i - 1].Pos) <= 0.16f, $"{i}번째 간격 {_ribbon[i].Pos.DistanceTo(_ribbon[i - 1].Pos)}");
        }

        [Fact]
        public void StreamIsThinAtNozzleAndWideAtTip()
        {
            WaterRibbon.Build(Straight(), 1f, 0.37f, 1f, _ribbon, _blobs);

            Assert.True(_ribbon[0].Half < _ribbon[_ribbon.Count - 1].Half, $"노즐 {_ribbon[0].Half} / 끝 {_ribbon[_ribbon.Count - 1].Half}");
        }

        [Fact]
        public void NozzleEndStaysStraightWhileTipSways()
        {
            float tipMax = 0f;
            for (float t = 0f; t < 2f; t += 0.07f)
            {
                WaterRibbon.Build(Straight(), 1f, t, 1f, _ribbon, _blobs);
                foreach (WaterRibbon.Point p in _ribbon)
                {
                    if (p.Along <= 1f) Assert.True(Math.Abs(p.Pos.Y) < 0.02f, $"t={t} along={p.Along} 벗어남 {p.Pos.Y}");
                    else tipMax = Math.Max(tipMax, Math.Abs(p.Pos.Y));
                }
            }
            // 끝 쪽은 눈에 보이게 출렁인다(뻣뻣한 막대가 아니다).
            Assert.True(tipMax > 0.08f, $"끝 최대 출렁임 {tipMax}");
        }

        [Fact]
        public void TipBreaksIntoFadingBlobsBeyondRibbon()
        {
            WaterRibbon.Build(Straight(), 1f, 0.37f, 1f, _ribbon, _blobs);

            Assert.NotEmpty(_blobs);
            float ribbonEnd = _ribbon[_ribbon.Count - 1].Pos.DistanceTo(new Vec2(0f, 0f));
            for (int i = 0; i < _blobs.Count; i++)
            {
                Assert.True(_blobs[i].Pos.DistanceTo(new Vec2(0f, 0f)) > ribbonEnd, $"{i}번째 덩어리가 리본 안쪽");
                Assert.True(_blobs[i].Radius > 0f);
                if (i > 0) Assert.True(_blobs[i].Alpha < _blobs[i - 1].Alpha, $"{i}번째 덩어리가 더 진함");
            }
        }

        [Fact]
        public void DegenerateChainsDrawNothing()
        {
            var cases = new[]
            {
                new List<Vec2> { new Vec2(1f, 1f) },
                new List<Vec2> { new Vec2(1f, 1f), new Vec2(1f, 1f), new Vec2(1f, 1f) },
                new List<Vec2> { new Vec2(0f, 0f), new Vec2(0.01f, 0f) },
                new List<Vec2>(),
            };
            foreach (List<Vec2> pts in cases)
            {
                WaterRibbon.Build(pts, 1f, 0.5f, 1f, _ribbon, _blobs);
                Assert.Empty(_ribbon);
                Assert.Empty(_blobs);
            }
        }

        [Fact]
        public void DuplicatePointsInChainNeverProduceNaN()
        {
            var pts = new List<Vec2> { new Vec2(0f, 0f), new Vec2(1f, 0f), new Vec2(1f, 0f), new Vec2(2f, 0.5f), new Vec2(2f, 0.5f), new Vec2(3f, 1.5f), new Vec2(4f, 1.5f) };
            WaterRibbon.Build(pts, 1.2f, 1.3f, 1f, _ribbon, _blobs);

            Assert.NotEmpty(_ribbon);
            foreach (WaterRibbon.Point p in _ribbon)
            {
                Assert.True(float.IsFinite(p.Pos.X) && float.IsFinite(p.Pos.Y) && float.IsFinite(p.Half));
                Assert.True(float.IsFinite(p.Normal.X) && float.IsFinite(p.Normal.Y));
                Assert.Equal(1f, (float)Math.Sqrt((p.Normal.X * p.Normal.X) + (p.Normal.Y * p.Normal.Y)), 3);
            }
            foreach (WaterRibbon.Blob b in _blobs) Assert.True(float.IsFinite(b.Pos.X) && float.IsFinite(b.Pos.Y));
        }

        [Fact]
        public void SameInputGivesSameShape()
        {
            var pts = new List<Vec2> { new Vec2(0f, 0f), new Vec2(2f, 0.3f), new Vec2(4f, 1.2f), new Vec2(6f, 2.8f) };
            WaterRibbon.Build(pts, 1f, 0.9f, 1f, _ribbon, _blobs);
            var first = new List<WaterRibbon.Point>(_ribbon);
            WaterRibbon.Build(pts, 1f, 0.9f, 1f, _ribbon, _blobs);

            Assert.Equal(first.Count, _ribbon.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.Equal(first[i].Pos.X, _ribbon[i].Pos.X);
                Assert.Equal(first[i].Pos.Y, _ribbon[i].Pos.Y);
            }
        }
    }
}
