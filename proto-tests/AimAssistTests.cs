using System;
using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>폰 물 조준 보정: 대충 끌어도 불 쪽으로, 누르기만 하면 가장 가까운 불로, 방향은 부드럽게 돈다.</summary>
    public class AimAssistTests
    {
        private static readonly Vec2 Player = new Vec2(30f, 30f);

        private static float Deg(Vec2 v)
        {
            return (float)(Math.Atan2(v.Y, v.X) * 180.0 / Math.PI);
        }

        private static Vec2 At(float deg, float dist)
        {
            double r = deg * Math.PI / 180.0;
            return new Vec2(Player.X + (float)(Math.Cos(r) * dist), Player.Y + (float)(Math.Sin(r) * dist));
        }

        [Fact]
        public void TapWithoutDrag_AimsAtNearestFire()
        {
            var fires = new List<Vec2> { At(90f, 8f), At(200f, 4f) };
            Vec2 aim = AimAssist.Desired(Player, new Vec2(1f, 0f), false, new Vec2(1f, 0f), fires);
            Assert.Equal(200f - 360f, Deg(aim), 1);
        }

        [Fact]
        public void NoFires_KeepsCurrentDirection()
        {
            Vec2 aim = AimAssist.Desired(Player, new Vec2(0f, -2f), false, new Vec2(1f, 0f), new List<Vec2>());
            Assert.Equal(0f, aim.X, 3);
            Assert.Equal(-1f, aim.Y, 3);
        }

        [Fact]
        public void Drag_PullsTowardFireInsideCone()
        {
            var fires = new List<Vec2> { At(20f, 6f) };
            Vec2 aim = AimAssist.Desired(Player, new Vec2(1f, 0f), true, new Vec2(1f, 0f), fires);
            float off = 20f - Deg(aim);
            Assert.True(off <= 8f + 0.01f, "끈 방향보다 불 쪽으로 당겨져야 한다: 남은 차이 " + off);
            Assert.True(off > 0f, "끝까지 스냅하지는 않는다");
        }

        [Fact]
        public void Drag_IgnoresFireOutsideCone()
        {
            var fires = new List<Vec2> { At(45f, 5f) };
            Vec2 aim = AimAssist.Desired(Player, new Vec2(0f, 1f), true, new Vec2(1f, 0f), fires);
            Assert.Equal(0f, Deg(aim), 2);
        }

        [Fact]
        public void Turn_IsRateLimited()
        {
            Vec2 turned = AimAssist.Turn(new Vec2(1f, 0f), new Vec2(-1f, 0f), 30f);
            Assert.Equal(30f, Math.Abs(Deg(turned)), 2);
            Vec2 close = AimAssist.Turn(new Vec2(1f, 0f), new Vec2(1f, 0.1f), 30f);
            Assert.Equal(Deg(new Vec2(1f, 0.1f)), Deg(close), 2);
        }

        [Fact]
        public void SimTargets_IncludeBossAndBurningBuildings()
        {
            var sim = new SurvivorSim(1);
            sim.Enemies.Clear();
            sim.Reports = false;
            Enemy boss = sim.Spawn(EnemyKind.Boss, new Vec2(10f, 10f));
            Structure shop = sim.Structures.Find(s => s.IsBuilding);
            shop.Fire = 0.5f;
            var into = new List<Vec2>();
            sim.AimTargets(into);
            Assert.Contains(boss.Pos, into);
            Assert.Contains(shop.Pos, into);
            Assert.Equal(2, into.Count);
        }
    }
}
