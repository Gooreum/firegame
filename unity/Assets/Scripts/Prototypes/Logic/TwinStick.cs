using System;
using System.Collections.Generic;

namespace FireGame.Prototypes.Logic
{
    public enum FingerPhase { Down, Held, Up }

    /// <summary>화면에 닿은 손가락 하나(화면 픽셀, 아래가 y=0).</summary>
    public struct Finger
    {
        public int Id;
        public FingerPhase Phase;
        public Vec2 At;
    }

    /// <summary>
    /// 폰 조작: 화면 왼쪽 반을 누르면 이동 조이스틱, 오른쪽 반을 누르면 물 조이스틱.
    /// 누른 자리가 스틱 중심이 되는 떠 있는 조이스틱이다. 물 스틱은 닿아 있는 동안 쏘고, 끈 쪽으로 겨눈다.
    /// 한번 잡은 손가락은 화면 가운데를 넘어가도 제 스틱이다.
    /// </summary>
    public sealed class TwinStick
    {
        /// <summary>스틱 반지름(기준 1080 높이에서 픽셀). 이만큼 끌면 최대 속도.</summary>
        public const float Radius = 110f;

        /// <summary>물 스틱을 이만큼 넘게 끌어야 조준이 바뀐다(살짝 누른 떨림 무시).</summary>
        public const float AimDeadZone = 14f;

        /// <summary>이동 방향과 세기(길이 0~1).</summary>
        public Vec2 Move;

        /// <summary>오른손이 닿아 있다(물을 쏜다).</summary>
        public bool Firing;

        /// <summary>끈 방향(단위 벡터). AimFresh가 false면 쓰지 않는다.</summary>
        public Vec2 Aim = new Vec2(1f, 0f);

        /// <summary>이번 누름에서 끌었다. 안 끌고 누르기만 했으면 뷰가 걷는 방향으로 쏜다.</summary>
        public bool AimFresh;

        public bool LeftOn;
        public bool RightOn;
        public Vec2 LeftBase;
        public Vec2 LeftKnob;
        public Vec2 RightBase;
        public Vec2 RightKnob;

        private int _left = -1;
        private int _right = -1;

        /// <param name="scale">화면 높이 / 1080. 반지름·데드존을 화면 크기에 맞춘다.</param>
        public void Feed(IReadOnlyList<Finger> fingers, float screenWidth, float scale)
        {
            float radius = Radius * scale;
            foreach (Finger f in fingers)
            {
                if (f.Phase == FingerPhase.Down)
                {
                    if (f.At.X < screenWidth * 0.5f)
                    {
                        if (_left >= 0) continue;
                        _left = f.Id;
                        LeftBase = f.At;
                        LeftKnob = f.At;
                    }
                    else
                    {
                        if (_right >= 0) continue;
                        _right = f.Id;
                        RightBase = f.At;
                        RightKnob = f.At;
                        AimFresh = false;
                    }
                    continue;
                }

                bool up = f.Phase == FingerPhase.Up;
                if (f.Id == _left)
                {
                    if (up) _left = -1;
                    else LeftKnob = Clamp(LeftBase, f.At, radius);
                }
                else if (f.Id == _right)
                {
                    if (up)
                    {
                        _right = -1;
                        AimFresh = false;
                        continue;
                    }
                    RightKnob = Clamp(RightBase, f.At, radius);
                    float dx = f.At.X - RightBase.X;
                    float dy = f.At.Y - RightBase.Y;
                    float d = (float)Math.Sqrt((dx * dx) + (dy * dy));
                    if (d > AimDeadZone * scale)
                    {
                        Aim = new Vec2(dx / d, dy / d);
                        AimFresh = true;
                    }
                }
            }

            LeftOn = _left >= 0;
            RightOn = _right >= 0;
            Firing = RightOn;
            Move = LeftOn ? new Vec2((LeftKnob.X - LeftBase.X) / radius, (LeftKnob.Y - LeftBase.Y) / radius) : new Vec2(0f, 0f);
        }

        /// <summary>손가락이 모두 떨어진 것처럼 비운다(판을 다시 시작할 때).</summary>
        public void Clear()
        {
            _left = -1;
            _right = -1;
            LeftOn = RightOn = Firing = AimFresh = false;
            Move = new Vec2(0f, 0f);
        }

        private static Vec2 Clamp(Vec2 from, Vec2 to, float max)
        {
            float dx = to.X - from.X;
            float dy = to.Y - from.Y;
            float d = (float)Math.Sqrt((dx * dx) + (dy * dy));
            if (d <= max) return to;
            return new Vec2(from.X + (dx / d * max), from.Y + (dy / d * max));
        }
    }
}
