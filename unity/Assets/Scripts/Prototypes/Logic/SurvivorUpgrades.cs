using System.Collections.Generic;
using FireGame.Core.Sim;

namespace FireGame.Prototypes.Logic
{
    /// <summary>레벨업 카드. 무기 4 + 보조 4 + 진화 1 + 뽑을 게 없을 때의 회복.</summary>
    public enum UpgradeId
    {
        Hose,
        WaterBomb,
        Drone,
        Foam,
        Tank,
        Suit,
        Boots,
        Radio,
        Cannon,
        Heal,
    }

    /// <summary>한 판 동안 모은 무기·보조와 레벨. 수치 공식도 여기 모은다.</summary>
    public sealed class Loadout
    {
        public const int MaxLevel = 5;
        public const int WeaponSlots = 4;
        public const int PassiveSlots = 4;

        private readonly int[] _levels = new int[(int)UpgradeId.Heal + 1];

        public int Level(UpgradeId id)
        {
            return _levels[(int)id];
        }

        public static int MaxLevelOf(UpgradeId id)
        {
            return id == UpgradeId.Cannon ? 1 : id == UpgradeId.Heal ? 0 : MaxLevel;
        }

        public static bool IsWeapon(UpgradeId id)
        {
            return id == UpgradeId.Hose || id == UpgradeId.WaterBomb || id == UpgradeId.Drone || id == UpgradeId.Foam || id == UpgradeId.Cannon;
        }

        public static bool IsPassive(UpgradeId id)
        {
            return id == UpgradeId.Tank || id == UpgradeId.Suit || id == UpgradeId.Boots || id == UpgradeId.Radio;
        }

        public int WeaponCount
        {
            get { return Count(true); }
        }

        public int PassiveCount
        {
            get { return Count(false); }
        }

        /// <summary>물대포 최대 + 탱크 보유 + 아직 진화 전.</summary>
        public bool EvolutionReady
        {
            get { return Level(UpgradeId.Hose) >= MaxLevel && Level(UpgradeId.Tank) >= 1 && Level(UpgradeId.Cannon) == 0; }
        }

        /// <summary>카드 하나를 반영한다. 방수포는 물대포 자리를 대신한다. 회복은 여기서 아무것도 안 한다(체력은 Sim이 올린다).</summary>
        public void Add(UpgradeId id)
        {
            if (id == UpgradeId.Heal) return;
            if (id == UpgradeId.Cannon) _levels[(int)UpgradeId.Hose] = 0;
            int i = (int)id;
            if (_levels[i] < MaxLevelOf(id)) _levels[i]++;
        }

        public IEnumerable<UpgradeId> Owned()
        {
            for (int i = 0; i < _levels.Length; i++)
            {
                if (_levels[i] > 0) yield return (UpgradeId)i;
            }
        }

        // --- 수치 ---
        public float CooldownScale { get { return 1f - (0.08f * Level(UpgradeId.Tank)); } }
        public float MaxHpBonus { get { return 20f * Level(UpgradeId.Suit); } }
        public float Regen { get { return 0.5f * Level(UpgradeId.Suit); } }
        public float SpeedScale { get { return 1f + (0.1f * Level(UpgradeId.Boots)); } }
        public float MagnetScale { get { return 1f + (0.3f * Level(UpgradeId.Radio)); } }
        public float BombRadius { get { return 1.5f * (1f + (0.15f * (Level(UpgradeId.WaterBomb) - 1))); } }

        /// <summary>이 카드를 지금 뽑을 수 있나(최대 레벨·빈 슬롯).</summary>
        public bool CanTake(UpgradeId id)
        {
            if (id == UpgradeId.Heal) return false;
            if (id == UpgradeId.Cannon) return EvolutionReady;
            int level = Level(id);
            if (level >= MaxLevelOf(id)) return false;
            if (level > 0) return true;
            if (id == UpgradeId.Hose && Level(UpgradeId.Cannon) > 0) return false;
            return IsWeapon(id) ? WeaponCount < WeaponSlots : PassiveCount < PassiveSlots;
        }

        private int Count(bool weapons)
        {
            int n = 0;
            for (int i = 0; i < _levels.Length; i++)
            {
                var id = (UpgradeId)i;
                if (_levels[i] > 0 && (weapons ? IsWeapon(id) : IsPassive(id))) n++;
            }
            return n;
        }
    }

    public static class SurvivorUpgrades
    {
        public const float HealAmount = 30f;

        /// <summary>
        /// 카드 3장을 뽑는다. 진화할 수 있으면 방수포를 반드시 넣고, 뽑을 게 모자라면 회복으로 채운다.
        /// </summary>
        public static List<UpgradeId> Roll(Loadout loadout, ref Rng rng)
        {
            var pool = new List<UpgradeId>();
            for (int i = 0; i <= (int)UpgradeId.Radio; i++)
            {
                if (loadout.CanTake((UpgradeId)i)) pool.Add((UpgradeId)i);
            }

            var picks = new List<UpgradeId>(3);
            if (loadout.EvolutionReady) picks.Add(UpgradeId.Cannon);
            while (picks.Count < 3 && pool.Count > 0)
            {
                int k = rng.Next(pool.Count);
                picks.Add(pool[k]);
                pool.RemoveAt(k);
            }
            if (picks.Count < 3) picks.Add(UpgradeId.Heal);
            return picks;
        }

        public static string Name(UpgradeId id)
        {
            switch (id)
            {
                case UpgradeId.Hose: return "물대포";
                case UpgradeId.WaterBomb: return "물폭탄";
                case UpgradeId.Drone: return "스프링클러 드론";
                case UpgradeId.Foam: return "거품 장판";
                case UpgradeId.Tank: return "대용량 탱크";
                case UpgradeId.Suit: return "방화복";
                case UpgradeId.Boots: return "장화";
                case UpgradeId.Radio: return "무전기";
                case UpgradeId.Cannon: return "고압 방수포";
                default: return "응급 처치";
            }
        }

        /// <summary>카드 설명. nextLevel은 고르면 될 레벨(1이면 새로 얻음).</summary>
        public static string Describe(UpgradeId id, int nextLevel)
        {
            bool fresh = nextLevel <= 1;
            switch (id)
            {
                case UpgradeId.Hose: return fresh ? "가까운 불에 물을 연사한다" : "물줄기 +1, 위력 +25%";
                case UpgradeId.WaterBomb: return fresh ? "불 떼 한가운데 물폭탄을 던진다" : "폭탄 +1, 범위 +15%";
                case UpgradeId.Drone: return fresh ? "주위를 도는 드론이 불을 끈다" : "드론 +1";
                case UpgradeId.Foam: return fresh ? "지나간 자리에 거품이 남아 불을 늦춘다" : "거품 지속 +1초";
                case UpgradeId.Tank: return "모든 무기 재장전 -8%";
                case UpgradeId.Suit: return "최대 체력 +20, 초당 회복 +0.5";
                case UpgradeId.Boots: return "이동 속도 +10%";
                case UpgradeId.Radio: return "경험치 끌어오는 범위 +30%";
                case UpgradeId.Cannon: return "진화! 관통하는 물줄기가 사방을 휩쓴다";
                default: return "체력 30 회복";
            }
        }
    }
}
