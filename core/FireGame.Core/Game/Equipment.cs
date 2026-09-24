using System;
using System.Collections.Generic;
using FireGame.Core.Grid;
using FireGame.Core.Sim;

namespace FireGame.Core.Game
{
    /// <summary>조준 방향 8방위. 인덱스 순서가 시계방향이라 ±1로 인접 방위를 구할 수 있다.</summary>
    public enum AimDirection : byte
    {
        N = 0,
        NE = 1,
        E = 2,
        SE = 3,
        S = 4,
        SW = 5,
        W = 6,
        NW = 7,
    }

    /// <summary>장비가 약제를 뿌리는 모양.</summary>
    public enum AimPattern : byte
    {
        /// <summary>조준 방향 1칸.</summary>
        Single = 0,

        /// <summary>조준 방향과 그 양옆 3갈래를 사거리만큼. 사거리 1이면 앞 3칸, 2면 최대 6칸.</summary>
        Cone = 1,

        /// <summary>조준 방향 직선. 벽에 막힌다.</summary>
        Line = 2,
    }

    /// <summary>장비의 자원 소모 방식.</summary>
    public enum ResourceKind : byte
    {
        /// <summary>쿨다운만 있고 총량 제한은 없다.</summary>
        Cooldown = 0,

        /// <summary>정해진 횟수만 쓸 수 있다.</summary>
        Charges = 1,
    }

    /// <summary>장비 한 종류의 Lv1 정의. 값이 바뀌지 않는 데이터다. 가격은 <see cref="UpgradeCatalog"/>에 있다.</summary>
    public sealed class EquipmentDef
    {
        public readonly int Id;
        public readonly string Name;
        public readonly Agent Agent;
        public readonly AimPattern Pattern;
        public readonly int Range;
        public readonly ResourceKind Resource;
        public readonly float CooldownSeconds;
        public readonly int MaxCharges;

        /// <summary>
        /// 직선 끝에서 양옆으로 퍼지는 칸 수. 0이면 곧은 물줄기, 1이면 끝이 T자가 된다(호스 Lv5 특성).
        /// </summary>
        public readonly int EndSpread;

        public EquipmentDef(
            int id,
            string name,
            Agent agent,
            AimPattern pattern,
            int range,
            ResourceKind resource,
            float cooldownSeconds,
            int maxCharges,
            int endSpread = 0)
        {
            Id = id;
            Name = name;
            Agent = agent;
            Pattern = pattern;
            Range = range;
            Resource = resource;
            CooldownSeconds = cooldownSeconds;
            MaxCharges = maxCharges;
            EndSpread = endSpread;
        }

        /// <summary>레벨 하나당 위력 증가율.</summary>
        public const float PowerPerLevel = 0.2f;

        /// <summary>레벨 하나당 사용 횟수 증가율(소화기류).</summary>
        public const float ChargesPerLevel = 0.25f;

        /// <summary>레벨 하나당 쿨다운 감소율(양동이).</summary>
        public const float CooldownCutPerLevel = 0.1f;

        /// <summary>
        /// 이 레벨마다 한 번씩 눈에 보이는 특성이 붙는다(Lv5, Lv10, …).
        /// 숫자만 오르면 레벨업이 느껴지지 않아서, 뿌리는 모양 자체를 키운다.
        /// </summary>
        public const int MilestoneEvery = 5;

        /// <summary>호스 사거리 상한. 화면 너비(40칸)를 넘겨 쏘면 조준이 의미가 없어진다.</summary>
        public const int MaxLineRange = 12;

        /// <summary>
        /// 부채꼴 사거리 상한(앞 18칸). 레벨 상한이 아니라 조준 상한이다.
        /// 한 발이 방 하나를 덮고 나면 어디를 겨누든 같아져서 조준이 사라진다.
        /// 여기 닿은 뒤에도 위력과 사용 횟수는 천장 없이 계속 오른다.
        /// </summary>
        public const int MaxConeRange = 6;

        /// <summary>양동이 연사 하한(초).</summary>
        public const float MinCooldownSeconds = 0.4f;

        /// <summary>호스 Lv10 특성: 더 빠른 연사.</summary>
        public const float FastLineCooldown = 0.15f;

        /// <summary>지금 레벨까지 받은 특성 수. Lv5 → 1, Lv10 → 2.</summary>
        public static int Milestones(int level)
        {
            return Math.Max(0, level) / MilestoneEvery;
        }

        /// <summary>
        /// 레벨이 반영된 정의. Lv1이 기본값이고, 레벨마다
        /// 위력 +20%, 소화기는 사용 횟수 +25%, 호스는 사거리 +1(12칸까지), 양동이는 쿨다운 −10%(0.4초까지).
        /// 5레벨마다 특성: 부채꼴은 한 줄 더 멀리(앞 3칸 → 6칸 → 9칸…),
        /// 호스는 Lv5에 끝이 T자로 퍼지고 Lv10에 연사가 빨라진다.
        /// Lv1~4는 특성이 없어 현장별 필요 장비 표가 그대로 맞는다.
        /// </summary>
        public EquipmentDef AtLevel(int level)
        {
            int steps = Math.Max(0, level - 1);
            if (steps == 0) return this;

            var agent = new Agent(Agent.Type, Agent.Power * (1f + (PowerPerLevel * steps)), Agent.Wetness, Agent.Inerting);
            int milestones = Milestones(level);
            bool line = Pattern == AimPattern.Line;

            int range = line
                ? Math.Min(Range + steps, MaxLineRange)
                : Math.Min(Range + milestones, MaxConeRange);

            // 호스는 이미 0.2초마다 나가므로 사거리로 키우고, 쿨다운 장비만 연사를 빠르게 한다.
            float cooldown;
            if (line) cooldown = milestones >= 2 ? Math.Min(CooldownSeconds, FastLineCooldown) : CooldownSeconds;
            else if (Resource == ResourceKind.Cooldown) cooldown = Math.Max(MinCooldownSeconds, CooldownSeconds * (1f - (CooldownCutPerLevel * steps)));
            else cooldown = CooldownSeconds;

            int charges = MaxCharges == 0 ? 0 : (int)Math.Round(MaxCharges * (1f + (ChargesPerLevel * steps)));
            int spread = line && milestones >= 1 ? 1 : EndSpread;

            return new EquipmentDef(Id, Name, agent, Pattern, range, Resource, cooldown, charges, spread);
        }
    }

    /// <summary>조준 방향과 패턴으로 실제 타격 셀을 계산한다.</summary>
    public static class Aiming
    {
        private static readonly int[] DirX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] DirY = { -1, -1, 0, 1, 1, 1, 0, -1 };

        public static int OffsetX(AimDirection direction)
        {
            return DirX[(int)direction];
        }

        public static int OffsetY(AimDirection direction)
        {
            return DirY[(int)direction];
        }

        /// <summary>시계방향으로 <paramref name="steps"/>칸 돌린 방위.</summary>
        public static AimDirection Rotate(AimDirection direction, int steps)
        {
            int value = ((int)direction + steps) % 8;
            if (value < 0) value += 8;
            return (AimDirection)value;
        }

        /// <summary>
        /// (originX, originY)에 선 플레이어가 지정 방향으로 장비를 쏠 때 맞는 셀들.
        /// 격자 밖은 제외하고, Line은 통행 불가 셀을 만나면 그 칸까지만 포함한다.
        /// 벽 너머로 물을 쏠 수 없어야 하되, 불타는 벽 자체는 맞아야 하기 때문이다.
        /// </summary>
        public static void Resolve(
            FireGrid grid,
            int originX,
            int originY,
            AimDirection direction,
            AimPattern pattern,
            int range,
            List<GridPoint> results,
            int endSpread = 0)
        {
            results.Clear();
            if (grid == null) return;

            switch (pattern)
            {
                case AimPattern.Single:
                    AddIfInBounds(grid, originX + OffsetX(direction), originY + OffsetY(direction), results);
                    break;

                case AimPattern.Cone:
                    for (int turn = -1; turn <= 1; turn++)
                    {
                        AddRay(grid, originX, originY, Rotate(direction, turn), Math.Max(1, range), results);
                    }
                    break;

                case AimPattern.Line:
                    AddRay(grid, originX, originY, direction, range, results);
                    if (endSpread > 0 && results.Count > 0) AddEndSpread(grid, direction, endSpread, results);
                    break;
            }
        }

        /// <summary>
        /// 한 방향으로 사거리만큼 칸을 모은다. 막힌 칸은 맞히되 그 너머로는 나아가지 않는다 —
        /// 벽 너머로 물을 쏠 수 없어야 하지만, 불타는 벽 자체는 맞아야 한다.
        /// </summary>
        private static void AddRay(FireGrid grid, int originX, int originY, AimDirection direction, int range, List<GridPoint> results)
        {
            int dx = OffsetX(direction);
            int dy = OffsetY(direction);

            for (int step = 1; step <= range; step++)
            {
                int x = originX + (dx * step);
                int y = originY + (dy * step);
                if (!grid.InBounds(x, y)) break;

                results.Add(new GridPoint(x, y));

                if (!Materials.Of(grid[x, y].Material).Walkable) break;
            }
        }

        /// <summary>직선의 마지막 칸 양옆(조준 방향에 수직)으로 퍼진다. 호스 끝이 T자가 된다.</summary>
        private static void AddEndSpread(FireGrid grid, AimDirection direction, int spread, List<GridPoint> results)
        {
            GridPoint end = results[results.Count - 1];
            AimDirection left = Rotate(direction, -2);
            AimDirection right = Rotate(direction, 2);

            for (int step = 1; step <= spread; step++)
            {
                AddIfInBounds(grid, end.X + (OffsetX(left) * step), end.Y + (OffsetY(left) * step), results);
                AddIfInBounds(grid, end.X + (OffsetX(right) * step), end.Y + (OffsetY(right) * step), results);
            }
        }

        private static void AddIfInBounds(FireGrid grid, int x, int y, List<GridPoint> results)
        {
            if (grid.InBounds(x, y)) results.Add(new GridPoint(x, y));
        }
    }
}
