using System;
using FireGame.Core.Data;

namespace FireGame.Core.Game
{
    /// <summary>상점 항목의 종류.</summary>
    public enum UpgradeKind : byte
    {
        Equipment = 0,

        /// <summary>방화복. 불 피해를 줄이고 소방관 겉모습을 바꾼다.</summary>
        Suit = 1,

        /// <summary>소방화. 이동 속도를 올린다.</summary>
        Boots = 2,
    }

    /// <summary>장비가 아닌 상점 항목의 id. 세이브에 숫자로 남으므로 값을 바꾸면 안 된다.</summary>
    public static class GearId
    {
        public const int Suit = 100;
        public const int Boots = 101;
    }

    /// <summary>
    /// 레벨이 있는 상점 항목 하나. 장비·방화복·소방화를 모두 같은 방식으로 산다:
    /// Lv0(없음) → Lv1(해금) → … → MaxLevel.
    /// </summary>
    public sealed class UpgradeTrack
    {
        public readonly int Id;
        public readonly string Name;
        public readonly UpgradeKind Kind;

        /// <summary>새 게임에서의 레벨. 양동이만 1이다.</summary>
        public readonly int StartLevel;

        /// <summary>가격표가 끝난 뒤 레벨마다 값이 오르는 배율.</summary>
        public const double CostGrowth = 1.3;

        /// <summary>
        /// 가격 상한. ×1.3이 수십 번 쌓이면 int를 넘어 음수가 된다.
        /// Lv30 무렵이면 이미 수백만 달러라 실제로 닿을 일은 없다.
        /// </summary>
        public const int MaxCost = 99999950;

        public readonly int MaxLevel;

        // _costs[i] = 레벨 i+1이 되는 값. 표 뒤로는 CostGrowth씩 오른다.
        private readonly int[] _costs;

        public UpgradeTrack(int id, string name, UpgradeKind kind, int startLevel, int maxLevel, params int[] costs)
        {
            Id = id;
            Name = name;
            Kind = kind;
            StartLevel = startLevel;
            MaxLevel = maxLevel;
            _costs = costs;
        }

        /// <summary>
        /// 이 레벨이 되는 값. 시작 레벨 이하이거나 최대 레벨을 넘으면 -1(살 수 없음).
        /// 가격표 뒤 레벨은 마지막 값에서 레벨마다 ×1.3, $50 단위로 반올림한다.
        /// 후반 한 판 보상이 한 레벨 값쯤 되어 "뛰고 → 올리고"가 계속 이어진다.
        /// </summary>
        public int CostToReach(int level)
        {
            if (level <= StartLevel || level > MaxLevel) return -1;
            if (level <= _costs.Length) return _costs[level - 1];

            double cost = _costs[_costs.Length - 1] * Math.Pow(CostGrowth, level - _costs.Length);
            if (cost >= MaxCost) return MaxCost;
            return (int)(Math.Round(cost / 50.0) * 50);
        }
    }

    /// <summary>
    /// 상점 가격표.
    ///
    /// 한 판 보상이 $800~$1,500이라 현장 한 번이면 새 장비 하나를 해금하거나
    /// 레벨업 두세 번을 할 수 있게 잡았다. 예전 가격(호스 $3,000, 폼 $10,000)은
    /// 같은 현장을 열 번 가까이 반복해야 해서 지루했다.
    ///
    /// 장비는 끝없이 올릴 수 있다(사실상 무제한). 돈을 쓸 곳이 끝나지 않아야 벌 이유도 끝나지 않는다.
    /// 방화복·소방화는 피해 감소·속도가 끝없이 늘면 게임이 깨지므로 Lv10에서 멈춘다.
    /// </summary>
    public static class UpgradeCatalog
    {
        /// <summary>장비의 최대 레벨. 사실상 무제한이다.</summary>
        public const int EndlessLevel = 99;

        public const int GearMaxLevel = 10;

        public static readonly UpgradeTrack Bucket =
            new UpgradeTrack(EquipmentId.Bucket, "양동이", UpgradeKind.Equipment, 1, EndlessLevel, 0, 150, 300, 500, 800);

        public static readonly UpgradeTrack Extinguisher =
            new UpgradeTrack(EquipmentId.Extinguisher, "CO2 소화기", UpgradeKind.Equipment, 0, EndlessLevel, 300, 200, 400, 600, 900);

        public static readonly UpgradeTrack Hose =
            new UpgradeTrack(EquipmentId.Hose, "소방 호스", UpgradeKind.Equipment, 0, EndlessLevel, 800, 300, 500, 800, 1200);

        public static readonly UpgradeTrack FoamExtinguisher =
            new UpgradeTrack(EquipmentId.FoamExtinguisher, "폼 소화기", UpgradeKind.Equipment, 0, EndlessLevel, 1500, 400, 700, 1000, 1500);

        public static readonly UpgradeTrack Suit =
            new UpgradeTrack(GearId.Suit, "방화복", UpgradeKind.Suit, 0, GearMaxLevel, 200, 400, 700, 1100);

        public static readonly UpgradeTrack Boots =
            new UpgradeTrack(GearId.Boots, "소방화", UpgradeKind.Boots, 0, GearMaxLevel, 150, 300, 500, 800);

        /// <summary>상점에 보이는 순서.</summary>
        public static readonly UpgradeTrack[] All =
        {
            Bucket,
            Extinguisher,
            Hose,
            FoamExtinguisher,
            Suit,
            Boots,
        };

        public static UpgradeTrack ById(int id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id) return All[i];
            }
            return null;
        }
    }

    /// <summary>방화복·소방화 레벨이 실제로 주는 효과.</summary>
    public static class GearStats
    {
        private static readonly float[] SuitDamage = { 1f, 0.8f, 0.65f, 0.5f, 0.35f };
        private static readonly string[] SuitNames = { "근무복", "방화복", "고급 방화복", "특수 방화복", "방열복" };

        /// <summary>방열복(Lv4) 뒤로 레벨마다 피해에 곱하는 값.</summary>
        public const float SuitDamagePerExtraLevel = 0.85f;

        /// <summary>아무리 올려도 불 피해는 이만큼은 남는다. 불 속이 안전지대가 되면 안 된다.</summary>
        public const float MinSuitDamage = 0.1f;

        /// <summary>소방화 한 레벨당 이동 속도 증가율.</summary>
        public const float SpeedPerBootsLevel = 0.1f;

        /// <summary>불 피해에 곱하는 배율. 방열복(Lv4)이면 불 속 피해가 16 → 5.6/초.</summary>
        public static float DamageMultiplier(int suitLevel)
        {
            int level = Math.Max(0, Math.Min(suitLevel, UpgradeCatalog.Suit.MaxLevel));
            if (level < SuitDamage.Length) return SuitDamage[level];

            float extra = SuitDamage[SuitDamage.Length - 1] * (float)Math.Pow(SuitDamagePerExtraLevel, level - (SuitDamage.Length - 1));
            return Math.Max(MinSuitDamage, extra);
        }

        /// <summary>옷 이름. 방열복 뒤로는 "방열복 +2"처럼 붙인다.</summary>
        public static string SuitName(int suitLevel)
        {
            int level = Math.Max(0, Math.Min(suitLevel, UpgradeCatalog.Suit.MaxLevel));
            int last = SuitNames.Length - 1;
            return level <= last ? SuitNames[level] : SuitNames[last] + " +" + (level - last);
        }

        /// <summary>옷 그림 번호(0~4). 방열복 뒤로는 방열복 그림을 그대로 쓴다.</summary>
        public static int SuitLook(int suitLevel)
        {
            return ClampSuit(suitLevel);
        }

        public static float SpeedMultiplier(int bootsLevel)
        {
            int level = Math.Max(0, Math.Min(bootsLevel, UpgradeCatalog.Boots.MaxLevel));
            return 1f + (SpeedPerBootsLevel * level);
        }

        private static int ClampSuit(int level)
        {
            return Math.Max(0, Math.Min(level, SuitDamage.Length - 1));
        }
    }
}
