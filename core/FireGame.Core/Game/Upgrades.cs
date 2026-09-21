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

        // _costs[i] = 레벨 i+1이 되는 값
        private readonly int[] _costs;

        public UpgradeTrack(int id, string name, UpgradeKind kind, int startLevel, params int[] costs)
        {
            Id = id;
            Name = name;
            Kind = kind;
            StartLevel = startLevel;
            _costs = costs;
        }

        public int MaxLevel
        {
            get { return _costs.Length; }
        }

        /// <summary>
        /// 이 레벨이 되는 값. 시작 레벨 이하이거나 최대 레벨을 넘으면 -1(살 수 없음).
        /// </summary>
        public int CostToReach(int level)
        {
            if (level <= StartLevel || level > MaxLevel) return -1;
            return _costs[level - 1];
        }
    }

    /// <summary>
    /// 상점 가격표.
    ///
    /// 한 판 보상이 $800~$1,500이라 현장 한 번이면 새 장비 하나를 해금하거나
    /// 레벨업 두세 번을 할 수 있게 잡았다. 예전 가격(호스 $3,000, 폼 $10,000)은
    /// 같은 현장을 열 번 가까이 반복해야 해서 지루했다.
    /// </summary>
    public static class UpgradeCatalog
    {
        public static readonly UpgradeTrack Bucket =
            new UpgradeTrack(EquipmentId.Bucket, "양동이", UpgradeKind.Equipment, 1, 0, 150, 300, 500, 800);

        public static readonly UpgradeTrack Extinguisher =
            new UpgradeTrack(EquipmentId.Extinguisher, "CO2 소화기", UpgradeKind.Equipment, 0, 300, 200, 400, 600, 900);

        public static readonly UpgradeTrack Hose =
            new UpgradeTrack(EquipmentId.Hose, "소방 호스", UpgradeKind.Equipment, 0, 800, 300, 500, 800, 1200);

        public static readonly UpgradeTrack FoamExtinguisher =
            new UpgradeTrack(EquipmentId.FoamExtinguisher, "폼 소화기", UpgradeKind.Equipment, 0, 1500, 400, 700, 1000, 1500);

        public static readonly UpgradeTrack Suit =
            new UpgradeTrack(GearId.Suit, "방화복", UpgradeKind.Suit, 0, 200, 400, 700, 1100);

        public static readonly UpgradeTrack Boots =
            new UpgradeTrack(GearId.Boots, "소방화", UpgradeKind.Boots, 0, 150, 300, 500, 800);

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

        /// <summary>소방화 한 레벨당 이동 속도 증가율.</summary>
        public const float SpeedPerBootsLevel = 0.1f;

        /// <summary>불 피해에 곱하는 배율. 방열복(Lv4)이면 불 속 피해가 16 → 5.6/초.</summary>
        public static float DamageMultiplier(int suitLevel)
        {
            return SuitDamage[ClampSuit(suitLevel)];
        }

        public static string SuitName(int suitLevel)
        {
            return SuitNames[ClampSuit(suitLevel)];
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
