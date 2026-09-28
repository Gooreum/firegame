using System.Collections.Generic;
using FireGame.Core.Sim;

namespace FireGame.Prototypes.Logic
{
    /// <summary>레벨업 카드. 무기 4 + 보조 4 + 진화 1 + 노란 특수 7(스테이지별 풀) + 뽑을 게 없을 때의 회복.</summary>
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
        Heli,
        Curtain,
        Partner,

        /// <summary>1스테이지 전용: 소방차가 소방관 줄을 가로지르며 양옆으로 물을 뿜는다.</summary>
        Truck,

        /// <summary>1스테이지 전용: 모든 건물 지붕 스프링클러가 타는 건물을 적신다.</summary>
        Sprinkler,

        /// <summary>2스테이지 전용: 불이 몰린 곳에 먹구름이 비를 뿌린다.</summary>
        Rain,

        /// <summary>2스테이지 전용: 비행기가 붉은 방염제 띠를 뿌려 그 안은 한동안 불이 안 붙는다.</summary>
        Retardant,
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
            return IsSpecial(id) ? 1 : id == UpgradeId.Heal ? 0 : MaxLevel;
        }

        /// <summary>노란 카드: 진화와 특수 장비. 레벨 1짜리이고 무기·보조 칸을 쓰지 않는다.</summary>
        public static bool IsSpecial(UpgradeId id)
        {
            return id == UpgradeId.Cannon || id == UpgradeId.Heli || id == UpgradeId.Curtain || id == UpgradeId.Partner
                || id == UpgradeId.Truck || id == UpgradeId.Sprinkler || id == UpgradeId.Rain || id == UpgradeId.Retardant;
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

        /// <summary>모든 무기·보조를 최대로 올리고 물대포는 방수포로 진화시킨다. 특수 장비는 주어진 풀 전부(시험용 풀장비).</summary>
        public void MaxAll(IEnumerable<UpgradeId> specials)
        {
            for (int i = 0; i <= (int)UpgradeId.Radio; i++) _levels[i] = MaxLevelOf((UpgradeId)i);
            Add(UpgradeId.Cannon);
            foreach (UpgradeId id in specials) Add(id);
        }

        public IEnumerable<UpgradeId> Owned()
        {
            for (int i = 0; i < _levels.Length; i++)
            {
                if (_levels[i] > 0) yield return (UpgradeId)i;
            }
        }

        // --- 수치 ---
        /// <summary>고압 펌프: 물대포 사거리. 재장전보다 눈에 띄게 멀리 나간다.</summary>
        public float HoseRange { get { return 1f + (0.15f * Level(UpgradeId.Tank)); } }

        /// <summary>고압 펌프: 물대포 위력.</summary>
        public float HosePower { get { return 1f + (0.15f * Level(UpgradeId.Tank)); } }
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
            if (IsSpecial(id)) return level == 0;
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

        /// <summary>이 확률(%)로 보장 레벨이 아니어도 노란 특수 장비가 한 장 섞인다.</summary>
        public const int SpecialChance = 15;

        /// <summary>
        /// 카드 3장을 뽑는다. 진화할 수 있으면 방수포를 반드시 넣는다.
        /// 아니면 <paramref name="level"/>(새 레벨)이 5의 배수일 때 노란 특수 장비를 반드시 한 장 넣는다.
        /// 뽑을 게 모자라면 회복으로 채운다. <paramref name="specialPool"/>가 있으면 노란 카드는 그 안에서만 나온다.
        /// </summary>
        public static List<UpgradeId> Roll(Loadout loadout, int level, ref Rng rng, IList<UpgradeId> specialPool = null)
        {
            var pool = new List<UpgradeId>();
            var specials = new List<UpgradeId>();
            for (int i = 0; i < (int)UpgradeId.Heal; i++)
            {
                var id = (UpgradeId)i;
                if (id == UpgradeId.Cannon || !loadout.CanTake(id)) continue;
                if (Loadout.IsSpecial(id) && specialPool != null && !specialPool.Contains(id)) continue;
                (Loadout.IsSpecial(id) ? specials : pool).Add(id);
            }

            var picks = new List<UpgradeId>(3);
            if (loadout.EvolutionReady) picks.Add(UpgradeId.Cannon);
            else if (specials.Count > 0 && (SpecialDue(level) || rng.Next(100) < SpecialChance)) picks.Add(specials[rng.Next(specials.Count)]);
            while (picks.Count < 3 && pool.Count > 0)
            {
                int k = rng.Next(pool.Count);
                picks.Add(pool[k]);
                pool.RemoveAt(k);
            }
            if (picks.Count < 3) picks.Add(UpgradeId.Heal);
            return picks;
        }

        /// <summary>이 레벨로 오를 때는 노란 카드가 반드시 나온다(5, 10, 15…).</summary>
        public static bool SpecialDue(int level)
        {
            return level > 0 && level % 5 == 0;
        }

        public static string Name(UpgradeId id)
        {
            switch (id)
            {
                case UpgradeId.Hose: return "물대포";
                case UpgradeId.WaterBomb: return "물폭탄";
                case UpgradeId.Drone: return "스프링클러 드론";
                case UpgradeId.Foam: return "거품 장판";
                case UpgradeId.Tank: return "고압 펌프";
                case UpgradeId.Suit: return "방화복";
                case UpgradeId.Boots: return "장화";
                case UpgradeId.Radio: return "무전기";
                case UpgradeId.Cannon: return "고압 방수포";
                case UpgradeId.Heli: return "소방 헬기";
                case UpgradeId.Curtain: return "물의 장막";
                case UpgradeId.Partner: return "구조대원 동료";
                case UpgradeId.Truck: return "소방차 출동";
                case UpgradeId.Sprinkler: return "스프링클러";
                case UpgradeId.Rain: return "비구름";
                case UpgradeId.Retardant: return "방염제 살포";
                default: return "응급 처치";
            }
        }

        /// <summary>카드 설명. nextLevel은 고르면 될 레벨(1이면 새로 얻음).</summary>
        public static string Describe(UpgradeId id, int nextLevel)
        {
            bool fresh = nextLevel <= 1;
            switch (id)
            {
                case UpgradeId.Hose: return fresh ? "겨눈 쪽으로 물줄기를 뿜는다" : "물줄기 굵기·세기 +45%, 사거리 +10%";
                case UpgradeId.WaterBomb: return fresh ? "불 떼 한가운데 물폭탄을 던진다" : "폭탄 +1, 범위 +15%";
                case UpgradeId.Drone: return fresh ? "주위를 도는 드론이 불을 끈다" : "드론 +1";
                case UpgradeId.Foam: return fresh ? "지나간 자리에 거품이 남아 불을 늦춘다" : "거품 지속 +1초";
                case UpgradeId.Tank: return "물줄기가 더 멀리, 더 세게 (+15%)";
                case UpgradeId.Suit: return "최대 체력 +20, 초당 회복 +0.5";
                case UpgradeId.Boots: return "이동 속도 +10%";
                case UpgradeId.Radio: return "경험치 끌어오는 범위 +30%";
                case UpgradeId.Cannon: return "진화! 관통하는 물줄기가 사방을 휩쓴다";
                case UpgradeId.Heli: return "9초마다 헬기가 가장 큰 불에 물을 쏟는다";
                case UpgradeId.Curtain: return "5초마다 몸 주위로 물 고리가 터져 불을 밀어낸다";
                case UpgradeId.Partner: return "동료가 불난 가게로 달려가 사람을 구한다";
                case UpgradeId.Truck: return "12초마다 소방차가 내 줄을 가로지르며 양옆 불을 쓸어낸다";
                case UpgradeId.Sprinkler: return "6초마다 모든 건물 지붕에서 물이 터져 불을 줄인다";
                case UpgradeId.Rain: return "12초마다 불이 몰린 곳에 먹구름이 3초 동안 비를 뿌린다";
                case UpgradeId.Retardant: return "15초마다 비행기가 방염제 띠를 뿌린다. 띠 안은 20초 동안 불이 안 붙는다";
                default: return "체력 30 회복";
            }
        }
    }
}
