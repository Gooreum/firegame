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

        /// <summary>조준 방향과 그 양옆, 부채꼴 3칸.</summary>
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

    /// <summary>장비 한 종류의 정의. 값이 바뀌지 않는 데이터다.</summary>
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
        public readonly int Price;

        public EquipmentDef(
            int id,
            string name,
            Agent agent,
            AimPattern pattern,
            int range,
            ResourceKind resource,
            float cooldownSeconds,
            int maxCharges,
            int price)
        {
            Id = id;
            Name = name;
            Agent = agent;
            Pattern = pattern;
            Range = range;
            Resource = resource;
            CooldownSeconds = cooldownSeconds;
            MaxCharges = maxCharges;
            Price = price;
        }

        /// <summary>레벨 하나당 위력 증가율.</summary>
        public const float PowerPerLevel = 0.2f;

        /// <summary>레벨 하나당 사용 횟수 증가율(소화기류).</summary>
        public const float ChargesPerLevel = 0.25f;

        /// <summary>레벨 하나당 쿨다운 감소율(양동이).</summary>
        public const float CooldownCutPerLevel = 0.1f;

        /// <summary>
        /// 레벨이 반영된 정의. Lv1이 기본값이고, 레벨마다
        /// 위력 +20%, 소화기는 사용 횟수 +25%, 호스는 사거리 +1, 양동이는 쿨다운 −10%.
        /// </summary>
        public EquipmentDef AtLevel(int level)
        {
            int steps = Math.Max(0, level - 1);
            if (steps == 0) return this;

            var agent = new Agent(Agent.Type, Agent.Power * (1f + (PowerPerLevel * steps)), Agent.Wetness, Agent.Inerting);

            int range = Pattern == AimPattern.Line ? Range + steps : Range;

            // 호스는 이미 0.2초마다 나가므로 사거리로 키우고, 쿨다운 장비만 연사를 빠르게 한다.
            float cooldown = Resource == ResourceKind.Cooldown && Pattern != AimPattern.Line
                ? CooldownSeconds * (1f - (CooldownCutPerLevel * steps))
                : CooldownSeconds;

            int charges = MaxCharges == 0 ? 0 : (int)Math.Round(MaxCharges * (1f + (ChargesPerLevel * steps)));

            return new EquipmentDef(Id, Name, agent, Pattern, range, Resource, cooldown, charges, Price);
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
            List<GridPoint> results)
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
                        AimDirection spoke = Rotate(direction, turn);
                        AddIfInBounds(grid, originX + OffsetX(spoke), originY + OffsetY(spoke), results);
                    }
                    break;

                case AimPattern.Line:
                    int dx = OffsetX(direction);
                    int dy = OffsetY(direction);

                    for (int step = 1; step <= range; step++)
                    {
                        int x = originX + (dx * step);
                        int y = originY + (dy * step);
                        if (!grid.InBounds(x, y)) break;

                        results.Add(new GridPoint(x, y));

                        // 막힌 칸은 맞히되 그 너머로는 나아가지 않는다.
                        if (!Materials.Of(grid[x, y].Material).Walkable) break;
                    }
                    break;
            }
        }

        private static void AddIfInBounds(FireGrid grid, int x, int y, List<GridPoint> results)
        {
            if (grid.InBounds(x, y)) results.Add(new GridPoint(x, y));
        }
    }
}
