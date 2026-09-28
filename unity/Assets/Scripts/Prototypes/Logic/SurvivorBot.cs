using System;
using System.Collections.Generic;

namespace FireGame.Prototypes.Logic
{
    /// <summary>
    /// 시험판 C를 대신 뛰는 단순한 봇. 밸런스("해볼 만한가")를 재고 캡처 장면을 만든다.
    /// 불 떼의 반대쪽으로 도망치면서 가까운 구슬과 시민을 줍고, 카드는 정해진 순서로 고른다.
    /// </summary>
    public sealed class SurvivorBot
    {
        private static readonly UpgradeId[] Priority =
        {
            UpgradeId.Cannon, UpgradeId.Heli, UpgradeId.Partner, UpgradeId.Curtain, UpgradeId.Hose, UpgradeId.Sprinkler, UpgradeId.Rain, UpgradeId.Truck, UpgradeId.Retardant, UpgradeId.Tank, UpgradeId.Drone, UpgradeId.WaterBomb,
            UpgradeId.Suit, UpgradeId.Foam, UpgradeId.Boots, UpgradeId.Radio, UpgradeId.Heal,
        };

        private readonly SurvivorSim _sim;

        public SurvivorBot(SurvivorSim sim)
        {
            _sim = sim;
        }

        /// <summary>카드 대기 중이면 고르고, 아니면 이번 틱 이동 방향을 돌려준다.</summary>
        public void Play()
        {
            if (_sim.PendingChoices != null)
            {
                _sim.Choose(PickCard(_sim.PendingChoices));
                return;
            }
            Vec2 move = Move();
            AimHose();
            _sim.Step(move.X, move.Y);
        }

        public static int PickCard(List<UpgradeId> cards)
        {
            foreach (UpgradeId want in Priority)
            {
                int i = cards.IndexOf(want);
                if (i >= 0) return i;
            }
            return 0;
        }

        /// <summary>
        /// 코앞(4칸)의 불 → 10칸 안에서 타는 건물 → 10칸 안의 불 순서로 겨누고 쏜다. 없으면 손을 뗀다.
        /// </summary>
        public void AimHose()
        {
            Vec2 p = _sim.Player;
            Vec2? target = NearestEnemy(p, 4f);
            if (!target.HasValue)
            {
                Structure fire = NearestBurning(p, 10f);
                if (fire != null) target = fire.Pos;
            }
            if (!target.HasValue) target = NearestEnemy(p, 10f);

            _sim.Spraying = target.HasValue;
            if (target.HasValue) _sim.Aim = new Vec2(target.Value.X - p.X, target.Value.Y - p.Y);
        }

        private Vec2? NearestEnemy(Vec2 p, float range)
        {
            Vec2? best = null;
            float bestD = range;
            foreach (Enemy e in _sim.Enemies)
            {
                if (e.Dead) continue;
                float d = e.Pos.DistanceTo(p);
                if (d < bestD)
                {
                    bestD = d;
                    best = e.Pos;
                }
            }
            return best;
        }

        private Structure NearestBurning(Vec2 p, float range)
        {
            Structure best = null;
            float bestD = range;
            foreach (Structure s in _sim.Structures)
            {
                if (!s.Burning) continue;
                float d = s.DistanceTo(p);
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }
            return best;
        }

        public Vec2 Move()
        {
            Vec2 p = _sim.Player;
            float fx = 0f;
            float fy = 0f;

            // 불 떼에서 멀어진다(가까울수록 세게).
            foreach (Enemy e in _sim.Enemies)
            {
                float dx = p.X - e.Pos.X;
                float dy = p.Y - e.Pos.Y;
                float d2 = (dx * dx) + (dy * dy);
                if (d2 > 49f || d2 < 0.0001f) continue;
                float w = 1f / d2;
                fx += dx * w;
                fy += dy * w;
            }

            foreach (Puddle b in _sim.BurningGround)
            {
                float dx = p.X - b.Pos.X;
                float dy = p.Y - b.Pos.Y;
                float d2 = (dx * dx) + (dy * dy);
                if (d2 > 4f || d2 < 0.0001f) continue;
                fx += dx / d2;
                fy += dy / d2;
            }

            // 벽에 몰리지 않게 가운데로.
            float margin = 8f;
            float size = SurvivorSim.ArenaSize;
            if (p.X < margin) fx += (margin - p.X) * 0.15f;
            if (p.X > size - margin) fx -= (p.X - (size - margin)) * 0.15f;
            if (p.Y < margin) fy += (margin - p.Y) * 0.15f;
            if (p.Y > size - margin) fy -= (p.Y - (size - margin)) * 0.15f;

            // 벽에 붙지 않게 건물·차에서 떨어진다.
            foreach (Structure s in _sim.Structures)
            {
                if (s.Collapsed || s.Kind == StructureKind.Tree || !s.Within(p, 1.2f)) continue;
                float dx = p.X - Clamp(p.X, s.Pos.X - s.Half.X, s.Pos.X + s.Half.X);
                float dy = p.Y - Clamp(p.Y, s.Pos.Y - s.Half.Y, s.Pos.Y + s.Half.Y);
                float d = (float)Math.Sqrt((dx * dx) + (dy * dy));
                if (d < 0.01f) continue;
                fx += dx / d * 0.5f;
                fy += dy / d * 0.5f;
            }

            // 불난 곳으로 간다(갇힌 사람이 있으면 문 앞까지, 아니면 5칸까지). 위험이 적을 때만 구슬을 줍는다.
            float danger = (float)Math.Sqrt((fx * fx) + (fy * fy));
            Structure fire = FireToFight(p);
            Vec2? goal = null;
            float pull = 0.7f;
            if (_sim.Chests.Count > 0 && danger < 1.5f)
            {
                // 대형 신고를 다 구해 떨어진 보물상자: 카드 두 장이라 무엇보다 먼저 줍는다(30초면 사라진다).
                goal = _sim.Chests[0].Pos;
            }
            else if (fire != null && fire.Residents > 0 && danger < 1.5f)
            {
                goal = fire.Door;
            }
            else if (_sim.Toolboxes.Count > 0 && danger < 1f)
            {
                // 갇힌 사람이 없으면 곧 사라질 공구상자를 줍는다.
                goal = _sim.Toolboxes[0].Pos;
            }
            else if (fire != null && fire.DistanceTo(p) > 5f && danger < 1.5f)
            {
                goal = fire.Pos;
            }
            else if (danger < 0.6f)
            {
                goal = NearestLoot(p, 8f);
            }

            if (goal.HasValue)
            {
                float dx = goal.Value.X - p.X;
                float dy = goal.Value.Y - p.Y;
                float d = (float)Math.Sqrt((dx * dx) + (dy * dy));
                if (d > 0.3f)
                {
                    fx += dx / d * pull;
                    fy += dy / d * pull;
                }
            }

            float len = (float)Math.Sqrt((fx * fx) + (fy * fy));
            if (len < 0.05f) return default;
            return new Vec2(fx / len, fy / len);
        }

        /// <summary>갈 불: 갇힌 사람이 있는 불난 가게가 먼저, 없으면 가장 가까운 타는 건물.</summary>
        private Structure FireToFight(Vec2 p)
        {
            Structure best = null;
            float bestD = float.MaxValue;
            foreach (Structure s in _sim.Structures)
            {
                if (!s.Burning) continue;
                float d = s.DistanceTo(p) - (s.Residents > 0 ? 15f : 0f) - (s.IsBuilding ? 5f : 0f);
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }
            return best;
        }

        private static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }

        private Vec2? NearestLoot(Vec2 p, float range)
        {
            Vec2? best = null;
            float bestD = range;
            foreach (Civilian c in _sim.Civilians)
            {
                float d = c.Pos.DistanceTo(p) * 0.5f;
                if (d < bestD) { bestD = d; best = c.Pos; }
            }
            foreach (Gem g in _sim.Gems)
            {
                float d = g.Pos.DistanceTo(p);
                if (d < bestD) { bestD = d; best = g.Pos; }
            }
            return best;
        }
    }
}
