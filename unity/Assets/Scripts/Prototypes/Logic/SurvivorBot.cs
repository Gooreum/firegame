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
            UpgradeId.Cannon, UpgradeId.Squad, UpgradeId.AirBomb, UpgradeId.RescueDrone, UpgradeId.WaterWall, UpgradeId.RescuePost,
            UpgradeId.Heli, UpgradeId.Partner, UpgradeId.Curtain, UpgradeId.Hose, UpgradeId.Ambulance, UpgradeId.Sprinkler, UpgradeId.Rain, UpgradeId.Truck, UpgradeId.Retardant, UpgradeId.Foam,
            UpgradeId.Tank, UpgradeId.Drone, UpgradeId.WaterBomb, UpgradeId.Turret,
            UpgradeId.Suit, UpgradeId.Boots,
        };

        private readonly SurvivorSim _sim;

        /// <summary>측정용: 이 카드가 나오면 Lv3까지 무엇보다 먼저 고른다(아이템 하나의 힘을 잴 때).</summary>
        public UpgradeId? Favorite;

        /// <summary>
        /// 숙련: 사람처럼 문 앞에 서서 끄고, 큰 불·기름만 피하고, 체력이 ProRetreatHp 밑일 때만 물러난다.
        /// 기본 봇은 적을 1/d²로 피해 건물 곁에 안 서므로 "아슬아슬한 끝"을 못 잰다 — 그건 이 모드로 잰다(docs §15). 기본 봇은 바닥 측정용으로 그대로.
        /// </summary>
        public bool Pro;
        public const float ProRetreatHp = 0.35f;
        public const float ProStandOff = 2.5f;

        public SurvivorBot(SurvivorSim sim)
        {
            _sim = sim;
        }

        /// <summary>카드 대기 중이면 고르고, 아니면 이번 틱 이동 방향을 돌려준다.</summary>
        public void Play()
        {
            if (_sim.PendingChoices != null)
            {
                // 좋아하는 카드는 Lv3까지만 먼저 챙기고, 그 뒤엔 평소 순서대로(보조만 계속 고르면 초반 무기가 빈다).
                int fav = Favorite.HasValue && _sim.Build.Level(Favorite.Value) < 3 ? _sim.PendingChoices.IndexOf(Favorite.Value) : -1;
                // 좋아하는 무기의 진화도 먼저 고른다.
                UpgradeId? evo = Favorite.HasValue ? Loadout.EvolutionOf(Favorite.Value) : null;
                int evoAt = evo.HasValue ? _sim.PendingChoices.IndexOf(evo.Value) : -1;
                _sim.Choose(evoAt >= 0 ? evoAt : fav >= 0 ? fav : PickCard(_sim.PendingChoices, _sim.Build));
                return;
            }
            Vec2 move = Move();
            AimHose();
            _sim.Step(move.X, move.Y);
        }

        /// <summary>
        /// 진화를 노린다: 진화 카드가 먼저, 그다음 Lv3 넘은 무기의 짝 보조, 그다음 정해진 순서.
        /// </summary>
        public static int PickCard(List<UpgradeId> cards, Loadout build)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (Loadout.IsEvolution(cards[i])) return i;
            }
            for (int i = 0; i < cards.Count; i++)
            {
                if (!Loadout.IsPassive(cards[i]) || build.Level(cards[i]) > 0) continue;
                foreach (UpgradeId evo in build.ReadyEvolutionsFor(cards[i]))
                {
                    if (build.Level(Loadout.BaseOf(evo)) >= 3) return i;
                }
            }
            return PickCard(cards);
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
            Vec2? target;
            if (Pro)
            {
                // 숙련: 코앞(1.5칸) 큰 불·기름 → 4칸 안 타는 건물(증기를 모은다) → 3칸 안 아무 불 → 10칸 안 타는 건물.
                target = NearestEnemy(p, 1.5f, true);
                if (!target.HasValue)
                {
                    Structure near = NearestBurning(p, 4f);
                    if (near != null) target = near.Pos;
                }
                if (!target.HasValue) target = NearestEnemy(p, 3f);
                if (!target.HasValue)
                {
                    Structure far = NearestBurning(p, 10f);
                    if (far != null) target = far.Pos;
                }
            }
            else
            {
                target = NearestEnemy(p, 4f);
                if (!target.HasValue)
                {
                    Structure fire = NearestBurning(p, 10f);
                    if (fire != null) target = fire.Pos;
                }
                if (!target.HasValue) target = NearestEnemy(p, 10f);
            }

            _sim.Spraying = target.HasValue;
            if (target.HasValue) _sim.Aim = new Vec2(target.Value.X - p.X, target.Value.Y - p.Y);
        }

        /// <summary>range 안 가장 가까운 불 몹. heavyOnly면 큰 불·기름(접촉 10)만.</summary>
        private Vec2? NearestEnemy(Vec2 p, float range, bool heavyOnly = false)
        {
            Vec2? best = null;
            float bestD = range;
            foreach (Enemy e in _sim.Enemies)
            {
                if (e.Dead) continue;
                if (heavyOnly && e.Kind != EnemyKind.Blaze && e.Kind != EnemyKind.Oil) continue;
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

            // 불 떼에서 멀어진다(가까울수록 세게). 숙련: 다쳤을 때만 전부, 아니면 2.5칸 안 큰 불·기름만(불씨·다트는 쏴서 잡는다).
            bool hurt = Pro && _sim.Hp < _sim.MaxHp * ProRetreatHp;
            float avoid2 = Pro && !hurt ? ProStandOff * ProStandOff : 49f;
            foreach (Enemy e in _sim.Enemies)
            {
                if (Pro && e.Dead) continue;
                if (Pro && !hurt && e.Kind != EnemyKind.Blaze && e.Kind != EnemyKind.Oil) continue;
                float dx = p.X - e.Pos.X;
                float dy = p.Y - e.Pos.Y;
                float d2 = (dx * dx) + (dy * dy);
                if (d2 > avoid2 || d2 < 0.0001f) continue;
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
            if (Pro)
            {
                // 숙련: 다쳤으면 구급상자 → 상자 → 주민 있는 불의 문 앞(구조 + 물, 열기를 몸으로 받는다) → 가장자리 2.5칸(Lv1 호스 사거리 안) → 공구상자 → 구슬.
                pull = 1f;
                if (hurt && _sim.Kits.Count > 0) goal = _sim.Kits[0].Pos;
                else if (hurt) goal = danger < 0.3f ? NearestLoot(p, 12f) : null;   // 다쳤고 상자도 없으면 불로 안 간다: 피하기만(구슬은 안전할 때만).
                else if (_sim.Chests.Count > 0) goal = _sim.Chests[0].Pos;
                else if (fire != null && fire.Residents > 0) goal = fire.Door;
                else if (fire != null) goal = StandOff(fire, p, ProStandOff);
                else if (_sim.Toolboxes.Count > 0) goal = _sim.Toolboxes[0].Pos;
                else goal = NearestLoot(p, 12f);
            }
            else if (_sim.Chests.Count > 0 && danger < 1.5f)
            {
                // 대형 신고를 다 구해 떨어진 보물상자: 카드 두 장이라 무엇보다 먼저 줍는다(30초면 사라진다).
                goal = _sim.Chests[0].Pos;
            }
            else if (_sim.Kits.Count > 0 && _sim.Hp < _sim.MaxHp * 0.75f && danger < 1.2f)
            {
                // 다쳤으면 구급상자부터(25초면 사라진다). 체력이 넉넉하면 그냥 둔다.
                goal = _sim.Kits[0].Pos;
            }
            else if (fire != null && fire.Residents > 0 && danger < 1.5f)
            {
                // 활활 타는데 체력이 모자라면(열기) 문 앞 대신 4칸 밖에서 먼저 끈다(AimHose가 건물을 겨눈다).
                bool hot = fire.Fire >= 0.6f && _sim.Hp < _sim.MaxHp * 0.5f;
                goal = hot ? StandOff(fire, p, 4f) : fire.Door;
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
                // 강: 목표가 건너편이면 다리 가운데(30,30)를 먼저 밟는다. 다리 줄(|y-30|<3.5)에 있으면 그냥 간다.
                float mid = SurvivorSim.ArenaSize / 2f;
                if (_sim.HasWater && Math.Sign(goal.Value.X - mid) != Math.Sign(p.X - mid) && Math.Abs(p.Y - mid) > 3.5f) goal = new Vec2(mid, mid);
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

        /// <summary>건물 가장자리에서 d칸 떨어진, 소방관 쪽 자리.</summary>
        private static Vec2 StandOff(Structure s, Vec2 p, float d)
        {
            float dx = p.X - s.Pos.X;
            float dy = p.Y - s.Pos.Y;
            float len = (float)Math.Sqrt((dx * dx) + (dy * dy));
            if (len < 0.01f) return s.Door;
            float reach = Math.Max(s.Half.X, s.Half.Y) + d;
            return new Vec2(s.Pos.X + (dx / len * reach), s.Pos.Y + (dy / len * reach));
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
