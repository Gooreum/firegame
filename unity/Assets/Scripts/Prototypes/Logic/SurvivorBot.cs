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
            UpgradeId.Cannon, UpgradeId.Hose, UpgradeId.Tank, UpgradeId.Drone, UpgradeId.WaterBomb,
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
                float w = (e.Kind == EnemyKind.Boss ? 6f : 1f) / d2;
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

            // 위험이 적으면 구슬·시민 쪽으로.
            float danger = (float)Math.Sqrt((fx * fx) + (fy * fy));
            Vec2? loot = NearestLoot(p, 8f);
            if (loot.HasValue && danger < 0.6f)
            {
                float dx = loot.Value.X - p.X;
                float dy = loot.Value.Y - p.Y;
                float d = (float)Math.Sqrt((dx * dx) + (dy * dy));
                if (d > 0.01f)
                {
                    fx += dx / d * 0.7f;
                    fy += dy / d * 0.7f;
                }
            }

            float len = (float)Math.Sqrt((fx * fx) + (fy * fy));
            if (len < 0.05f) return default;
            return new Vec2(fx / len, fy / len);
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
