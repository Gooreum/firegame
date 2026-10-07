using System;
using System.Collections.Generic;
using FireGame.Core.Sim;

namespace FireGame.Prototypes.Logic
{
    /// <summary>
    /// 레벨업 카드(2026-10-07 개편). 무기 10 · 보조 3 · 진화 10. 무기·보조를 합쳐 4칸만 든다(탕탕처럼).
    /// 무기는 하나가 맡는 공간 하나(앞·둘레·하늘·바닥·마을)이고, 모두 불 몹과 건물 불을 둘 다 맞힌다.
    /// 레벨업은 개수·크기·갈래로 눈에 보이게 오른다. 무기 Lv5 + 짝 보조면 진화한다.
    /// 모든 스테이지가 같은 풀이고 노란 특수 카드는 없다. enum 순서가 IsWeapon/IsPassive/IsEvolution의 범위다.
    /// </summary>
    public enum UpgradeId
    {
        // --- 무기 10 ---
        /// <summary>물대포: 시작 무기. 겨눈 쪽(수호자는 저절로) 한 줄기.</summary>
        Hose,

        /// <summary>소방견: 마을을 노리는 몹에게 달려가 문다. 없으면 타는 건물 곁에서 짖으며 적신다.</summary>
        Dog,

        /// <summary>물풍선: 건물 벽과 경기장 끝에 튕기며 닿을 때마다 물보라.</summary>
        Balloon,

        /// <summary>소화기 부메랑: 나갔다 돌아오며 지나는 몹을 꿰뚫고 분말을 남긴다.</summary>
        Extinguisher,

        /// <summary>액체질소 지뢰: 발밑에 깔리고, 밟은 몹 둘레가 얼어붙는다.</summary>
        Mine,

        /// <summary>거품 눈덩이: 몹 무리 쪽으로 굴러가며 삼킬수록 커지다 터진다.</summary>
        Foam,

        /// <summary>비눗방울: 몹을 가둬 띄웠다가 터뜨린다(아래로 물이 쏟아진다).</summary>
        Bubble,

        /// <summary>맨홀 간헐천: 몹이 몰린 맨홀에서 물기둥이 솟아 날려 보낸다.</summary>
        Manhole,

        /// <summary>사다리차: 몹이 많은 쪽으로 사다리를 뻗어 한 줄을 내려찍는다. 지붕 위 사람도 구한다.</summary>
        Ladder,

        /// <summary>소방 호스 채찍: 내 둘레를 휘둘러 닿은 몹을 밀어낸다.</summary>
        Whip,

        // --- 보조 3 ---
        Tank,
        Boots,

        /// <summary>방화복: 열기·바닥 불 피해를 줄이고 최대 체력을 올린다.</summary>
        Suit,

        // --- 진화 10 (무기 Lv5 + 짝 보조) ---
        Cannon,
        DogPack,
        BalloonStorm,
        Tornado,
        IceField,
        Avalanche,
        BubbleFall,
        Waterline,
        LadderBridge,
        Whirl,

        Heal,
    }

    /// <summary>한 판 동안 모은 무기·보조와 레벨. 수치 공식도 여기 모은다.</summary>
    public sealed class Loadout
    {
        public const int MaxLevel = 5;

        /// <summary>무기·보조를 합쳐 4칸(탕탕처럼). 4칸 × Lv5 = 20레벨 ≈ 4분 판의 레벨업 수라 판 끝에 풀장비가 된다.</summary>
        public const int Slots = 4;

        /// <summary>진화 표: (진화, 원래 무기, 짝 보조). 펌프 넷, 장화 셋, 방화복 셋.</summary>
        private static readonly UpgradeId[,] Evolutions =
        {
            { UpgradeId.Cannon, UpgradeId.Hose, UpgradeId.Tank },
            { UpgradeId.DogPack, UpgradeId.Dog, UpgradeId.Boots },
            { UpgradeId.BalloonStorm, UpgradeId.Balloon, UpgradeId.Tank },
            { UpgradeId.Tornado, UpgradeId.Extinguisher, UpgradeId.Tank },
            { UpgradeId.IceField, UpgradeId.Mine, UpgradeId.Suit },
            { UpgradeId.Avalanche, UpgradeId.Foam, UpgradeId.Suit },
            { UpgradeId.BubbleFall, UpgradeId.Bubble, UpgradeId.Suit },
            { UpgradeId.Waterline, UpgradeId.Manhole, UpgradeId.Boots },
            { UpgradeId.LadderBridge, UpgradeId.Ladder, UpgradeId.Boots },
            { UpgradeId.Whirl, UpgradeId.Whip, UpgradeId.Tank },
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
            return id >= UpgradeId.Cannon && id <= UpgradeId.Whirl;
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
            return id <= UpgradeId.Whip || IsEvolution(id);
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

        /// <summary>시험용 풀장비: 4칸을 최대로 채우고 진화 둘까지(캡처·풀장비 측정).</summary>
        public void MaxAll()
        {
            // 4칸: 물대포·채찍·사다리차 + 펌프 → 고압 방수포·물 회오리 진화.
            foreach (UpgradeId id in new[] { UpgradeId.Hose, UpgradeId.Whip, UpgradeId.Ladder, UpgradeId.Tank }) _levels[(int)id] = MaxLevel;
            Add(UpgradeId.Cannon);
            Add(UpgradeId.Whirl);
        }

        /// <summary>시험용: 칸을 무시하고 열 무기를 모두 진화시킨다(짝 보조도 채운다).</summary>
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
            return WeaponCount + PassiveCount < Slots;
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
                case UpgradeId.Dog: return "소방견";
                case UpgradeId.Balloon: return "물풍선";
                case UpgradeId.Extinguisher: return "소화기 부메랑";
                case UpgradeId.Mine: return "액체질소 지뢰";
                case UpgradeId.Foam: return "거품 눈덩이";
                case UpgradeId.Bubble: return "비눗방울";
                case UpgradeId.Manhole: return "맨홀 간헐천";
                case UpgradeId.Ladder: return "사다리차";
                case UpgradeId.Whip: return "호스 채찍";
                case UpgradeId.Tank: return "고압 펌프";
                case UpgradeId.Boots: return "장화";
                case UpgradeId.Suit: return "방화복";
                case UpgradeId.Cannon: return "고압 방수포";
                case UpgradeId.DogPack: return "구조견 무리";
                case UpgradeId.BalloonStorm: return "물풍선 폭우";
                case UpgradeId.Tornado: return "분말 회오리";
                case UpgradeId.IceField: return "빙결 지대";
                case UpgradeId.Avalanche: return "거품 산사태";
                case UpgradeId.BubbleFall: return "방울 폭포";
                case UpgradeId.Waterline: return "수도관 폭발";
                case UpgradeId.LadderBridge: return "사다리 다리";
                case UpgradeId.Whirl: return "물 회오리";
                default: return "응급 처치";
            }
        }

        /// <summary>카드 설명: 퍼센트가 아니라 화면에서 무엇이 달라지는지. nextLevel은 고르면 될 레벨(1이면 새로 얻음).</summary>
        public static string Describe(UpgradeId id, int nextLevel)
        {
            bool fresh = nextLevel <= 1;
            int n = nextLevel;
            switch (id)
            {
                case UpgradeId.Hose: return fresh ? "겨눈 쪽으로 물줄기를 뿜는다" : "물줄기가 더 굵고 멀리 · 한 번에 " + (1 + n) + "마리 꿰뚫는다";
                case UpgradeId.Dog: return fresh ? "소방견이 마을을 노리는 불에게 달려가 문다" : n == 3 ? "소방견 2마리" : n == 5 ? "소방견 3마리" : "더 빨리 달리고 세게 문다";
                case UpgradeId.Balloon: return fresh ? "벽에 튕기는 물풍선 · 닿을 때마다 물보라" : n == 3 ? "물풍선 2개 · 4번 튕긴다" : n == 5 ? "물풍선 3개 · 6번 튕긴다" : "더 오래 튕긴다";
                case UpgradeId.Extinguisher: return fresh ? "소화기가 나갔다 돌아오며 불을 꿰뚫는다" : n == 3 ? "소화기 2개 · 더 멀리" : n == 5 ? "소화기 3개 · 9칸까지" : "더 멀리 날아간다";
                case UpgradeId.Mine: return fresh ? "발밑에 지뢰 · 밟은 불이 얼어붙는다" : "지뢰 " + (1 + n) + "개까지 · 어는 범위가 넓다";
                case UpgradeId.Foam: return fresh ? "굴러가며 불을 삼키고 커지다 터지는 거품" : n == 3 ? "거품 2개" : n == 5 ? "거품 3개 · 더 크게" : "더 크게 부푼다";
                case UpgradeId.Bubble: return fresh ? "불을 방울에 가둬 띄웠다가 터뜨린다" : "방울 " + (n == 2 || n == 3 ? 2 : n == 4 ? 3 : 4) + "발 · 더 큰 불도 가둔다";
                case UpgradeId.Manhole: return fresh ? "불이 몰린 맨홀에서 물기둥이 솟는다" : n == 3 ? "맨홀 2곳에서" : n == 4 ? "맨홀 3곳에서" : n == 5 ? "맨홀 4곳 · 더 큰 물기둥" : "물기둥이 굵어진다";
                case UpgradeId.Ladder: return fresh ? "사다리를 뻗어 한 줄을 내려찍는다 · 지붕 위 사람도 구한다" : n == 3 ? "두 방향으로 뻗는다" : n == 5 ? "세 방향 · 10칸" : "사다리가 길어진다";
                case UpgradeId.Whip: return fresh ? "호스를 휘둘러 둘레 불을 밀어낸다" : n == 3 ? "두 갈래로 휘두른다" : n == 5 ? "세 갈래 · 더 넓게" : "더 넓게 휘두른다";
                case UpgradeId.Tank: return "모든 물이 굵고 세진다 · 증기 폭발이 빨리 찬다";
                case UpgradeId.Boots: return fresh ? "더 빨리 달린다 · 불 바닥을 밟아 끈다" : "더 빨리 달린다";
                case UpgradeId.Suit: return fresh ? "불에 덜 다치고 체력이 는다 · 닿은 불이 튕겨 나간다" : "더 단단해진다 · 더 세게 튕긴다";
                case UpgradeId.Cannon: return "진화! 관통하는 물줄기가 사방을 휩쓴다";
                case UpgradeId.DogPack: return "진화! 구조견 4마리 · 갇힌 사람을 물고 나온다";
                case UpgradeId.BalloonStorm: return "진화! 튕길 때마다 풍선이 둘로 갈라진다";
                case UpgradeId.Tornado: return "진화! 하얀 회오리가 떠돌며 불을 빨아들인다";
                case UpgradeId.IceField: return "진화! 지뢰끼리 얼음 길로 이어진다";
                case UpgradeId.Avalanche: return "진화! 화면을 가로지르는 거품 파도";
                case UpgradeId.BubbleFall: return "진화! 갇힌 불을 한데 모아 큰 방울로 터뜨린다";
                case UpgradeId.Waterline: return "진화! 맨홀이 줄지어 도미노처럼 터진다";
                case UpgradeId.LadderBridge: return "진화! 사다리가 남아 불이 지나는 길을 막는다";
                case UpgradeId.Whirl: return "진화! 휘두른 자리에 물 고리가 남아 돈다";
                default: return "체력 30 회복";
            }
        }
    }
}
