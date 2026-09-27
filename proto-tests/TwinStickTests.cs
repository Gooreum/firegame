using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>폰 두 엄지 조작. 화면 1920×1080(scale 1)에서 손가락을 누르고 끌고 뗀다.</summary>
    public class TwinStickTests
    {
        private const float Width = 1920f;

        private static Finger F(int id, FingerPhase phase, float x, float y)
        {
            return new Finger { Id = id, Phase = phase, At = new Vec2(x, y) };
        }

        private static void Feed(TwinStick stick, params Finger[] fingers)
        {
            stick.Feed(new List<Finger>(fingers), Width, 1f);
        }

        [Fact]
        public void LeftThumb_DragsToMove()
        {
            var stick = new TwinStick();
            Feed(stick, F(0, FingerPhase.Down, 300, 500));
            Feed(stick, F(0, FingerPhase.Held, 355, 500));
            Assert.Equal(0.5f, stick.Move.X, 2);
            Feed(stick, F(0, FingerPhase.Held, 600, 500));
            Assert.Equal(1f, stick.Move.X, 2);
            Assert.Equal(0f, stick.Move.Y, 2);
            Assert.False(stick.Firing);
            Feed(stick, F(0, FingerPhase.Up, 600, 500));
            Assert.Equal(0f, stick.Move.X);
        }

        [Fact]
        public void RightThumb_HoldsToSpray_AndDragAims()
        {
            var stick = new TwinStick();
            Feed(stick, F(1, FingerPhase.Down, 1500, 400));
            Assert.True(stick.Firing);
            Assert.False(stick.AimFresh);
            Feed(stick, F(1, FingerPhase.Held, 1500, 460));
            Assert.True(stick.AimFresh);
            Assert.Equal(0f, stick.Aim.X, 2);
            Assert.Equal(1f, stick.Aim.Y, 2);
            Feed(stick, F(1, FingerPhase.Up, 1500, 460));
            Assert.False(stick.Firing);
            Assert.False(stick.AimFresh);
        }

        [Fact]
        public void TwoThumbs_AtOnce_ThirdIgnored()
        {
            var stick = new TwinStick();
            Feed(stick, F(0, FingerPhase.Down, 300, 500), F(1, FingerPhase.Down, 1500, 400), F(2, FingerPhase.Down, 1700, 700));
            Feed(stick, F(0, FingerPhase.Held, 300, 610), F(1, FingerPhase.Held, 1400, 400), F(2, FingerPhase.Held, 1800, 900));
            Assert.Equal(1f, stick.Move.Y, 2);
            Assert.True(stick.Firing);
            Assert.Equal(-1f, stick.Aim.X, 2);
            Assert.Equal(1500f, stick.RightBase.X);
        }

        [Fact]
        public void SmallJitter_DoesNotAim()
        {
            var stick = new TwinStick();
            Feed(stick, F(1, FingerPhase.Down, 1500, 400));
            Feed(stick, F(1, FingerPhase.Held, 1508, 406));
            Assert.True(stick.Firing);
            Assert.False(stick.AimFresh);
        }

        [Fact]
        public void ThumbCrossingMiddle_StaysItsStick()
        {
            var stick = new TwinStick();
            Feed(stick, F(0, FingerPhase.Down, 900, 500));
            Feed(stick, F(0, FingerPhase.Held, 1200, 500));
            Assert.Equal(1f, stick.Move.X, 2);
            Assert.False(stick.Firing);
            Assert.True(stick.LeftOn);
            Assert.False(stick.RightOn);
        }
    }
}
