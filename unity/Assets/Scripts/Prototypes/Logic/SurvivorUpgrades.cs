using System;
using System.Collections.Generic;
using FireGame.Core.Sim;

namespace FireGame.Prototypes.Logic
{
    /// <summary>
    /// 레벨업 카드. 게임의 목표(건물과 사람 지키기)에 맞춰, 모든 아이템이 불 끄기·사람 구하기·현장에 빨리 가기 중 하나 이상을 돕는다.
    /// 무기 6 중 4칸, 보조 6 중 4칸만 들 수 있어 판마다 고른다. 무기마다 짝 보조가 있어 무기 Lv5 + 짝 보조면 진화한다.
    /// 모든 스테이지가 같은 풀이고, 노란 특수 카드는 없다(2026-10-07).
    /// </summary>
    public enum UpgradeId
    {
        // --- 무기 6 ---
        Hose,
        WaterBomb,

        /// <summary>순찰 드론: 주변 타는 건물로 날아가 지붕 위를 돌며 물을 뿌린다.</summary>
        Drone,

        /// <summary>구조대원: 갇힌 사람이 있는 건물로 달려가 문 앞 불을 끄며 구한다.</summary>
        Partner,
        Curtain,

        /// <summary>방수 포탑: 선 자리에 세우면 몇 초 동안 곁 불 몹과 건물에 물을 쏜다.</summary>
        Turret,

        // --- 보조 3 ---
        Tank,
        Boots,

        /// <summary>방화복: 열기·바닥 불 피해를 줄이고 최대 체력을 올린다.</summary>
        Suit,

        // --- 진화 6 (무기 Lv5 + 짝 보조) ---
        Cannon,
        Squad,
        AirBomb,
        RescueDrone,
        WaterWall,
        RescuePost,

        Heal,
    }

    /// <summary>한 판 동안 모은 무기·보조와 레벨. 수치 공식도 여기 모은다.</summary>
    public sealed class Loadout
    {
        public const int MaxLevel = 5;
        /// <summary>무기 6 중 3, 보조 3 중 2. 넷·셋이면 3:00에 모든 판이 같은 풀장비가 됐다(docs §16).</summary>
        public const int WeaponSlots = 3;
        public const int PassiveSlots = 2;


        /// <summary>진화 표: (진화, 원래 무기, 짝 보조).</summary>
        private static readonly UpgradeId[,] Evolutions =
        {
            { UpgradeId.Cannon, UpgradeId.Hose, UpgradeId.Tank },
            { UpgradeId.Squad, UpgradeId.Partner, UpgradeId.Boots },
            { UpgradeId.AirBomb, UpgradeId.WaterBomb, UpgradeId.Tank },
            { UpgradeId.RescueDrone, UpgradeId.Drone, UpgradeId.Boots },
            { UpgradeId.WaterWall, UpgradeId.Curtain, UpgradeId.Suit },
            { UpgradeId.RescuePost, UpgradeId.Turret, UpgradeId.Suit },
        };

        private readonly int[] _levels = new int[(int)UpgradeId.Heal + 1];

        public int Level(UpgradeId id)
        {
            return _levels[(int)id];
        }

        public static int MaxLevelOf(UpgradeId id)
        {
            return IsEvolution(id) ? 1 : id == UpgradeId.Heal ? 0 : MaxLevel;
        }

        /// <summary>진화 카드인가.</summary>
        public static bool IsEvolution(UpgradeId id)
        {
            return id >= UpgradeId.Cannon && id <= UpgradeId.RescuePost;
        }

        /// <summary>진화의 원래 무기(진화가 아니면 자기 자신).</summary>
        public static UpgradeId BaseOf(UpgradeId evolution)
        {
            for (int i = 0; i < Evolutions.GetLength(0); i++)
            {
                if (Evolutions[i, 0] == evolution) return Evolutions[i, 1];
            }
            return evolution;
        }

        /// <summary>진화의 짝 보조.</summary>
        public static UpgradeId PairOf(UpgradeId evolution)
        {
            for (int i = 0; i < Evolutions.GetLength(0); i++)
            {
                if (Evolutions[i, 0] == evolution) return Evolutions[i, 2];
            }
            return evolution;
        }

        /// <summary>무기의 진화(없으면 null).</summary>
        public static UpgradeId? EvolutionOf(UpgradeId weapon)
        {
            for (int i = 0; i < Evolutions.GetLength(0); i++)
            {
                if (Evolutions[i, 1] == weapon) return Evolutions[i, 0];
            }
            return null;
        }

        public static bool IsWeapon(UpgradeId id)
        {
            return id <= UpgradeId.Turret || IsEvolution(id);
        }

        public static bool IsPassive(UpgradeId id)
        {
            return id >= UpgradeId.Tank && id <= UpgradeId.Suit;
        }

        public int WeaponCount
        {
            get { return Count(true); }
        }

        public int PassiveCount
        {
            get { return Count(false); }
        }

        /// <summary>물대포 최대 + 탱크 보유 + 아직 진화 전(방수포 하나만 볼 때).</summary>
        public bool EvolutionReady
        {
            get { return Ready(UpgradeId.Cannon); }
        }

        /// <summary>진화에 필요한 짝 보조 레벨(보유). Lv3을 요구해 봤더니 봇이 진화를 늦게 받아 마을이 무너졌다(10판 1승) — docs §13.</summary>
        public const int EvolvePair = 1;

        /// <summary>이 진화를 지금 할 수 있나: 원래 무기 Lv5 + 짝 보조 보유 + 아직 안 함.</summary>
        public bool Ready(UpgradeId evolution)
        {
            return IsEvolution(evolution) && Level(evolution) == 0 && Level(BaseOf(evolution)) >= MaxLevel && Level(PairOf(evolution)) >= EvolvePair;
        }

        /// <summary>지금 할 수 있는 진화들.</summary>
        public List<UpgradeId> ReadyEvolutions()
        {
            var list = new List<UpgradeId>();
            for (int i = 0; i < Evolutions.GetLength(0); i++)
            {
                if (Ready(Evolutions[i, 0])) list.Add(Evolutions[i, 0]);
            }
            return list;
        }

        /// <summary>이 보조가 짝인 진화들(아직 안 한 것).</summary>
        public List<UpgradeId> ReadyEvolutionsFor(UpgradeId passive)
        {
            var list = new List<UpgradeId>();
            for (int i = 0; i < Evolutions.GetLength(0); i++)
            {
                if (Evolutions[i, 2] == passive && Level(Evolutions[i, 0]) == 0) list.Add(Evolutions[i, 0]);
            }
            return list;
        }

        /// <summary>이 무기(또는 그 진화)를 쥐고 있나. 진화하면 원래 무기 레벨은 0이 된다.</summary>
        public bool Has(UpgradeId weapon)
        {
            UpgradeId? evo = EvolutionOf(weapon);
            return Level(weapon) > 0 || (evo.HasValue && Level(evo.Value) > 0);
        }

        /// <summary>이 무기의 쓰는 레벨: 진화했으면 최대로 친다.</summary>
        public int PowerOf(UpgradeId weapon)
        {
            UpgradeId? evo = EvolutionOf(weapon);
            if (evo.HasValue && Level(evo.Value) > 0) return MaxLevel;
            return Level(weapon);
        }

        /// <summary>카드 하나를 반영한다. 진화는 원래 무기 자리를 대신한다. 회복은 여기서 아무것도 안 한다(체력은 Sim이 올린다).</summary>
        public void Add(UpgradeId id)
        {
            if (id == UpgradeId.Heal) return;
            if (IsEvolution(id)) _levels[(int)BaseOf(id)] = 0;
            int i = (int)id;
            if (_levels[i] < MaxLevelOf(id)) _levels[i]++;
        }

        /// <summary>모든 무기·보조를 최대로 올리고 물대포는 방수포로 진화시킨다. 특수 장비는 주어진 풀 전부(시험용 풀장비).</summary>
        public void MaxAll()
        {
            for (int i = 0; i <= (int)UpgradeId.Suit; i++) _levels[i] = MaxLevelOf((UpgradeId)i);
            Add(UpgradeId.Cannon);
        }

        /// <summary>시험용: 여섯 무기를 모두 진화시킨다(짝 보조도 채운다).</summary>
        public void EvolveAll()
        {
            for (int i = 0; i <= (int)UpgradeId.Suit; i++) _levels[i] = MaxLevelOf((UpgradeId)i);
            for (int i = 0; i < Evolutions.GetLength(0); i++) Add(Evolutions[i, 0]);
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

        /// <summary>고압 펌프: 증기 충전 속도 배율(레벨마다 +25%)과 증기 폭발 반경 보너스(레벨마다 +0.5칸).</summary>
        public float SteamScale { get { return 1f + (0.25f * Level(UpgradeId.Tank)); } }
        public float SteamRadiusBonus { get { return 0.5f * Level(UpgradeId.Tank); } }

        /// <summary>방화복: 최대 체력 +10/레벨.</summary>
        public float MaxHpBonus { get { return 10f * Level(UpgradeId.Suit); } }

        /// <summary>방화복: 불에 받는 피해 배율(열기·바닥 불·불 몹 접촉, 레벨마다 −10%). 보조가 셋뿐이라 모든 판에 들어오므로 −15%면 체력 압력이 사라졌다(봇 위기 판 6 → 1).</summary>
        public float HeatScale { get { return 1f - (0.10f * Level(UpgradeId.Suit)); } }

        /// <summary>장화: 이동 +12%/레벨.</summary>
        public float SpeedScale { get { return 1f + (0.12f * Level(UpgradeId.Boots)); } }

        /// <summary>방화복: 닿은 불 몹을 튕겨 내는 힘. 없으면 0.</summary>
        public float SuitPush { get { int l = Level(UpgradeId.Suit); return l > 0 ? 3f + l : 0f; } }

        /// <summary>장화: 불 바닥을 밟아도 안 다치고 밟은 자리를 끈다.</summary>
        public bool WetBoots { get { return Level(UpgradeId.Boots) > 0; } }

        public float BombRadius { get { return 1.5f * (1f + (0.15f * (PowerOf(UpgradeId.WaterBomb) - 1))); } }

        /// <summary>이 카드를 지금 뽑을 수 있나(최대 레벨·빈 슬롯·진화 조건).</summary>
        public bool CanTake(UpgradeId id)
        {
            if (id == UpgradeId.Heal) return false;
            if (IsEvolution(id)) return Ready(id);
            int level = Level(id);
            if (level >= MaxLevelOf(id)) return false;
            if (level > 0) return true;
            UpgradeId? evo = EvolutionOf(id);
            if (evo.HasValue && Level(evo.Value) > 0) return false;
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
        /// 카드 3장을 뽑는다. 진화할 수 있으면 그 진화 하나를 반드시 넣고, 나머지는 쥔 것의 레벨업·빈 칸의 새 아이템에서 겹치지 않게 채운다.
        /// 모든 스테이지가 같은 풀이다. 뽑을 게 모자라면 그만큼만 준다(0장일 수도). 회복으로 채우지 않는다(회복은 바닥 구급상자로).
        /// </summary>
        public static List<UpgradeId> Roll(Loadout loadout, int level, ref Rng rng)
        {
            var pool = new List<UpgradeId>();
            for (int i = 0; i < (int)UpgradeId.Heal; i++)
            {
                var id = (UpgradeId)i;
                if (Loadout.IsEvolution(id) || !loadout.CanTake(id)) continue;
                pool.Add(id);
            }

            var picks = new List<UpgradeId>(3);
            List<UpgradeId> ready = loadout.ReadyEvolutions();
            if (ready.Count > 0) picks.Add(ready[rng.Next(ready.Count)]);
            while (picks.Count < 3 && pool.Count > 0)
            {
                int k = rng.Next(pool.Count);
                picks.Add(pool[k]);
                pool.RemoveAt(k);
            }
            return picks;
        }

        public static string Name(UpgradeId id)
        {
            switch (id)
            {
                case UpgradeId.Hose: return "물대포";
                case UpgradeId.WaterBomb: return "물폭탄";
                case UpgradeId.Drone: return "순찰 드론";
                case UpgradeId.Partner: return "구조대원";
                case UpgradeId.Curtain: return "물의 장막";
                case UpgradeId.Turret: return "방수 포탑";
                case UpgradeId.Tank: return "고압 펌프";
                case UpgradeId.Boots: return "장화";
                case UpgradeId.Suit: return "방화복";
                case UpgradeId.Cannon: return "고압 방수포";
                case UpgradeId.Squad: return "구조 분대";
                case UpgradeId.AirBomb: return "공중 소화탄";
                case UpgradeId.RescueDrone: return "구조 드론";
                case UpgradeId.WaterWall: return "물의 방벽";
                case UpgradeId.RescuePost: return "현장 구조소";
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
                case UpgradeId.WaterBomb: return fresh ? "불난 건물(없으면 불 떼)에 물폭탄을 던진다" : "폭탄 +1, 범위 +15%";
                case UpgradeId.Drone: return fresh ? "드론이 가장 센 불난 건물로 날아가 2.5초마다 물폭탄을 투하한다" : "드론 +1 (투하가 그만큼 잦아진다)";
                case UpgradeId.Partner: return fresh ? "대원 한 명이 갇힌 사람에게 달려가 불을 끄며 구한다" : nextLevel == 2 ? "구조·물줄기 +25%" : nextLevel == 3 ? "대원 달리기 +25%" : nextLevel == 4 ? "물줄기 +25%" : "구조 +25%";
                case UpgradeId.Curtain: return fresh ? "몇 초마다 몸 주위로 물 고리가 터져 불을 밀어낸다" : "고리 범위 +0.5칸, 간격 −0.4초";
                case UpgradeId.Turret: return fresh ? "7초마다 선 자리에 포탑을 세운다. 곁 불을 쏜다" : nextLevel == 3 || nextLevel == 5 ? "포탑 +1, 지속 +1초" : "포탑 지속 +1초";
                case UpgradeId.Tank: return "물줄기 사거리·세기 +15%, 증기 폭발이 25% 빨리 차고 0.5칸 넓어진다";
                case UpgradeId.Boots: return fresh ? "이동 +12%. 불 바닥을 밟아도 안 다치고 밟은 자리를 끈다" : "이동 속도 +12%";
                case UpgradeId.Suit: return fresh ? "불 피해 −10%, 최대 체력 +10. 닿은 불 몹이 튕겨 나간다" : "불 피해 −10%, 최대 체력 +10, 더 세게 튕긴다";
                case UpgradeId.Cannon: return "진화! 관통하는 물줄기가 사방을 휩쓴다";
                case UpgradeId.Squad: return "진화! 대원 둘이 흩어져 두 건물을 동시에 구하고 물·구조가 두 배";
                case UpgradeId.AirBomb: return "진화! 맵 어디든 불난 건물마다 소화탄이 떨어진다";
                case UpgradeId.RescueDrone: return "진화! 드론이 갇힌 사람을 끌어올려 구한다";
                case UpgradeId.WaterWall: return "진화! 커다란 물 고리가 건물 불을 크게 줄인다";
                case UpgradeId.RescuePost: return "진화! 포탑 곁 건물에선 연기로 사람을 잃지 않는다";
                default: return "체력 30 회복";
            }
        }
    }
}
