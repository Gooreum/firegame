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
    /// 숲(Loadout.Free, 2026-10-08)만 따로: 무기 8 + 수치형 보조 5(고압 노즐·급수 펌프·광각 노즐은 숲 전용), 칸 없음, 짝은 FreePairs.
    /// </summary>
    public enum UpgradeId
    {
        // --- 무기 10 ---
        /// <summary>물대포: 시작 무기. 겨눈 쪽(수호자는 저절로) 한 줄기.</summary>
        Hose,

        /// <summary>회전 스프링클러: 내 둘레를 돌며 닿은 몹을 튕겨 내고 사방으로 물을 뿌린다.</summary>
        Sprinkler,

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

        /// <summary>물 사슬: 물줄기가 몹에서 몹으로 번개처럼 튀며 꿰뚫는다.</summary>
        Chain,

        /// <summary>소방 호스 채찍: 내 둘레를 휘둘러 닿은 몹을 밀어낸다.</summary>
        Whip,

        // --- 보조 3 ---
        Tank,
        Boots,

        /// <summary>방화복: 열기·바닥 불 피해를 줄이고 최대 체력을 올린다.</summary>
        Suit,

        // --- 진화 10 (무기 Lv5 + 짝 보조) ---
        Cannon,
        Crown,
        BalloonStorm,
        Tornado,
        IceField,
        Avalanche,
        BubbleFall,
        Waterline,
        Surge,
        Whirl,

        // --- 보조 Lv6(숲 개편 2026-10-08: 짝 = 자기 자신, Free일 때만 나온다) ---
        /// <summary>초고압 펌프: 몇 초마다 내 둘레 물 대폭발.</summary>
        OverPump,

        /// <summary>제트 장화: 달린 자리에 요괴를 녹이는 물길.</summary>
        JetBoots,

        /// <summary>불사조 방화복: 쓰러지면 한 번 물 날개로 부활하며 폭발.</summary>
        PhoenixSuit,

        // --- 숲 보조(2026-10-08 아이템 정리: 수치만 올리고 무기의 진화 짝이 된다. 숲에서만 나온다) ---
        /// <summary>고압 노즐: 모든 물 피해 +10%/Lv. 짝: 물대포.</summary>
        Nozzle,

        /// <summary>급수 펌프: 무기 시계 +8%/Lv(재사용이 빨라진다). 짝: 물 사슬.</summary>
        Feed,

        /// <summary>광각 노즐: 무기 범위·반경 +10%/Lv. 짝: 물풍선·소화기.</summary>
        Wide,

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
            { UpgradeId.Crown, UpgradeId.Sprinkler, UpgradeId.Boots },
            { UpgradeId.BalloonStorm, UpgradeId.Balloon, UpgradeId.Tank },
            { UpgradeId.Tornado, UpgradeId.Extinguisher, UpgradeId.Tank },
            { UpgradeId.IceField, UpgradeId.Mine, UpgradeId.Suit },
            { UpgradeId.Avalanche, UpgradeId.Foam, UpgradeId.Suit },
            { UpgradeId.BubbleFall, UpgradeId.Bubble, UpgradeId.Suit },
            { UpgradeId.Waterline, UpgradeId.Manhole, UpgradeId.Boots },
            { UpgradeId.Surge, UpgradeId.Chain, UpgradeId.Boots },
            { UpgradeId.Whirl, UpgradeId.Whip, UpgradeId.Tank },
            { UpgradeId.OverPump, UpgradeId.Tank, UpgradeId.Tank },
            { UpgradeId.JetBoots, UpgradeId.Boots, UpgradeId.Boots },
            { UpgradeId.PhoenixSuit, UpgradeId.Suit, UpgradeId.Suit },
        };

        /// <summary>숲 진화 짝(2026-10-08 탕탕식): (무기, 짝 보조). 무기 Lv5 + 짝 보조 Lv1이면 Lv6(진화)이 나온다.</summary>
        private static readonly UpgradeId[,] FreePairs =
        {
            { UpgradeId.Hose, UpgradeId.Nozzle },
            { UpgradeId.Sprinkler, UpgradeId.Boots },
            { UpgradeId.Balloon, UpgradeId.Wide },
            { UpgradeId.Extinguisher, UpgradeId.Wide },
            { UpgradeId.Mine, UpgradeId.Suit },
            { UpgradeId.Bubble, UpgradeId.Suit },
            { UpgradeId.Manhole, UpgradeId.Boots },
            { UpgradeId.Chain, UpgradeId.Feed },
        };

        /// <summary>숲에서 빠진 것(2026-10-08): 채찍·거품·펌프와 그 진화, 보조 진화. 숲 카드에 안 나온다.</summary>
        private static readonly UpgradeId[] FreeCut =
        {
            UpgradeId.Tank, UpgradeId.OverPump, UpgradeId.Whip, UpgradeId.Whirl, UpgradeId.Foam, UpgradeId.Avalanche, UpgradeId.JetBoots, UpgradeId.PhoenixSuit,
        };

        /// <summary>숲 무기의 진화 짝 보조(숲 무기가 아니면 null).</summary>
        public static UpgradeId? FreePairOf(UpgradeId weapon)
        {
            for (int i = 0; i < FreePairs.GetLength(0); i++)
            {
                if (FreePairs[i, 0] == weapon) return FreePairs[i, 1];
            }
            return null;
        }

        /// <summary>숲 전용 보조(고압 노즐·급수 펌프·광각 노즐): 다른 스테이지 카드에 안 나온다.</summary>
        public static bool IsFreeOnly(UpgradeId id)
        {
            return id >= UpgradeId.Nozzle && id <= UpgradeId.Wide;
        }

        /// <summary>숲에서 빠진 아이템인가.</summary>
        public static bool IsFreeCut(UpgradeId id)
        {
            return Array.IndexOf(FreeCut, id) >= 0;
        }

        /// <summary>
        /// 숲 개편(2026-10-08): 칸 제한 없음 · 무기 8 + 수치형 보조 5 · 무기 Lv5 + 짝 보조(FreePairs)면 Lv6 최고급.
        /// 숲에서만 켠다(사용자: "일단 숲 스테이지만"). 꺼져 있으면 4칸·짝 진화 그대로.
        /// </summary>
        public bool Free;

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
            return id >= UpgradeId.Cannon && id <= UpgradeId.PhoenixSuit;
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
            return id <= UpgradeId.Whip || (IsEvolution(id) && !IsPassive(BaseOf(id)));
        }

        public static bool IsPassive(UpgradeId id)
        {
            return (id >= UpgradeId.Tank && id <= UpgradeId.Suit) || IsFreeOnly(id);
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
            if (!IsEvolution(evolution) || Level(evolution) != 0 || Level(BaseOf(evolution)) < MaxLevel) return false;
            if (Free) return FreePairOf(BaseOf(evolution)) is UpgradeId p && Level(p) >= EvolvePair;   // 탕탕식: 짝 보조를 들어야 진화
            return !IsPassive(BaseOf(evolution)) && Level(PairOf(evolution)) >= EvolvePair;
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
                UpgradeId pair = Free ? (FreePairOf(Evolutions[i, 1]) ?? Evolutions[i, 1]) : Evolutions[i, 2];
                if (pair == passive && Evolutions[i, 1] != passive && Level(Evolutions[i, 0]) == 0) list.Add(Evolutions[i, 0]);
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

        /// <summary>시험·캡처용: 이 아이템을 내려놓는다(샘플 벤치는 아이템 하나만 든다).</summary>
        public void Drop(UpgradeId id)
        {
            _levels[(int)id] = 0;
        }

        /// <summary>시험용 풀장비: 4칸을 최대로 채우고 진화 둘까지(캡처·풀장비 측정).</summary>
        public void MaxAll()
        {
            // 4칸: 물대포·채찍·사다리차 + 펌프 → 고압 방수포·물 회오리 진화.
            foreach (UpgradeId id in new[] { UpgradeId.Hose, UpgradeId.Whip, UpgradeId.Chain, UpgradeId.Tank }) _levels[(int)id] = MaxLevel;
            Add(UpgradeId.Cannon);
            Add(UpgradeId.Whirl);
        }

        /// <summary>시험용: 칸을 무시하고 열 무기를 모두 진화시킨다(짝 보조도 채운다).</summary>
        public void EvolveAll()
        {
            for (int i = 0; i <= (int)UpgradeId.Suit; i++) _levels[i] = MaxLevelOf((UpgradeId)i);
            for (int i = 0; i < Evolutions.GetLength(0); i++)
            {
                if (Free || !IsPassive(Evolutions[i, 1])) Add(Evolutions[i, 0]);
            }
        }

        public IEnumerable<UpgradeId> Owned()
        {
            for (int i = 0; i < _levels.Length; i++)
            {
                if (_levels[i] > 0) yield return (UpgradeId)i;
            }
        }

        // --- 수치 ---
        /// <summary>고압 펌프: 물대포 사거리. 재장전보다 눈에 띄게 멀리 나간다. 숲은 펌프가 물대포에 합쳐져 물대포 레벨이 맡는다.</summary>
        public float HoseRange { get { return 1f + (0.15f * PowerOf(Free ? UpgradeId.Hose : UpgradeId.Tank)); } }

        /// <summary>고압 펌프: 물대포 위력(숲은 물대포 레벨).</summary>
        public float HosePower { get { return 1f + (0.15f * PowerOf(Free ? UpgradeId.Hose : UpgradeId.Tank)); } }

        /// <summary>숲 고압 노즐: 모든 물 피해 배율(+10%/Lv).</summary>
        public float NozzleScale { get { return 1f + (0.10f * PowerOf(UpgradeId.Nozzle)); } }

        /// <summary>숲 급수 펌프: 무기 시계 배율(+8%/Lv) — 쿨다운이 그만큼 빨리 돈다.</summary>
        public float FeedScale { get { return 1f + (0.08f * PowerOf(UpgradeId.Feed)); } }

        /// <summary>숲 광각 노즐: 무기 범위·반경 배율(+10%/Lv).</summary>
        public float WideScale { get { return 1f + (0.10f * PowerOf(UpgradeId.Wide)); } }

        /// <summary>고압 펌프: 증기 충전 속도 배율(레벨마다 +25%)과 증기 폭발 반경 보너스(레벨마다 +0.5칸).</summary>
        public float SteamScale { get { return 1f + (0.25f * PowerOf(UpgradeId.Tank)); } }
        public float SteamRadiusBonus { get { return 0.5f * PowerOf(UpgradeId.Tank); } }

        /// <summary>방화복: 최대 체력 +10/레벨.</summary>
        public float MaxHpBonus { get { return 10f * PowerOf(UpgradeId.Suit); } }

        /// <summary>방화복: 불에 받는 피해 배율(열기·바닥 불·불 몹 접촉, 레벨마다 −10%). 보조가 셋뿐이라 모든 판에 들어오므로 −15%면 체력 압력이 사라졌다(봇 위기 판 6 → 1).</summary>
        public float HeatScale { get { return 1f - (0.10f * PowerOf(UpgradeId.Suit)); } }

        /// <summary>장화: 이동 +12%/레벨(숲은 +10%/레벨).</summary>
        public float SpeedScale { get { return 1f + ((Free ? 0.10f : 0.12f) * PowerOf(UpgradeId.Boots)); } }

        /// <summary>방화복: 닿은 불 몹을 튕겨 내는 힘. 없으면 0.</summary>
        public float SuitPush { get { int l = PowerOf(UpgradeId.Suit); return l > 0 ? 3f + l : 0f; } }

        /// <summary>장화: 불 바닥을 밟아도 안 다치고 밟은 자리를 끈다.</summary>
        public bool WetBoots { get { return PowerOf(UpgradeId.Boots) > 0; } }

        /// <summary>이 카드를 지금 뽑을 수 있나(최대 레벨·빈 슬롯·진화 조건).</summary>
        public bool CanTake(UpgradeId id)
        {
            if (id == UpgradeId.Heal) return false;
            if (Free ? IsFreeCut(id) : IsFreeOnly(id)) return false;
            if (IsEvolution(id)) return Ready(id);
            int level = Level(id);
            if (level >= MaxLevelOf(id)) return false;
            if (level > 0) return true;
            UpgradeId? evo = EvolutionOf(id);
            if (evo.HasValue && Level(evo.Value) > 0) return false;
            return Free || WeaponCount + PassiveCount < Slots;
        }

        private int Count(bool weapons)
        {
            int n = 0;
            for (int i = 0; i < _levels.Length; i++)
            {
                var id = (UpgradeId)i;
                if (_levels[i] > 0 && (weapons ? IsWeapon(id) : IsPassive(id) || (IsEvolution(id) && IsPassive(BaseOf(id))))) n++;
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
                case UpgradeId.Sprinkler: return "회전 스프링클러";
                case UpgradeId.Balloon: return "물풍선";
                case UpgradeId.Extinguisher: return "소화기 부메랑";
                case UpgradeId.Mine: return "액체질소 지뢰";
                case UpgradeId.Foam: return "거품 눈덩이";
                case UpgradeId.Bubble: return "비눗방울";
                case UpgradeId.Manhole: return "맨홀 간헐천";
                case UpgradeId.Chain: return "물 사슬";
                case UpgradeId.Whip: return "호스 채찍";
                case UpgradeId.Tank: return "고압 펌프";
                case UpgradeId.Boots: return "장화";
                case UpgradeId.Suit: return "방화복";
                case UpgradeId.Cannon: return "고압 방수포";
                case UpgradeId.Crown: return "물 왕관";
                case UpgradeId.BalloonStorm: return "물풍선 폭우";
                case UpgradeId.Tornado: return "분말 회오리";
                case UpgradeId.IceField: return "빙결 지대";
                case UpgradeId.Avalanche: return "거품 산사태";
                case UpgradeId.BubbleFall: return "방울 폭포";
                case UpgradeId.Waterline: return "수도관 폭발";
                case UpgradeId.Surge: return "해일 사슬";
                case UpgradeId.Whirl: return "물 회오리";
                case UpgradeId.OverPump: return "초고압 펌프";
                case UpgradeId.JetBoots: return "제트 장화";
                case UpgradeId.PhoenixSuit: return "불사조 방화복";
                case UpgradeId.Nozzle: return "고압 노즐";
                case UpgradeId.Feed: return "급수 펌프";
                case UpgradeId.Wide: return "광각 노즐";
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
                case UpgradeId.Sprinkler: return fresh ? "스프링클러 2개가 내 둘레를 돌며 닿은 몹을 튕겨 낸다" : n == 3 ? "스프링클러 3개" : n == 5 ? "스프링클러 4개 · 더 넓게" : "더 넓게 돈다";
                case UpgradeId.Balloon: return fresh ? "벽에 튕기는 물풍선 · 닿을 때마다 물보라" : n == 3 ? "물풍선 2개 · 4번 튕긴다" : n == 5 ? "물풍선 3개 · 6번 튕긴다" : "더 오래 튕긴다";
                case UpgradeId.Extinguisher: return fresh ? "소화기가 나갔다 돌아오며 불을 꿰뚫는다" : n == 3 ? "소화기 2개 · 더 멀리" : n == 5 ? "소화기 3개 · 9칸까지" : "더 멀리 날아간다";
                case UpgradeId.Mine: return fresh ? "발밑에 지뢰 · 밟은 불이 얼어붙는다" : "지뢰 " + (1 + n) + "개까지 · 어는 범위가 넓다";
                case UpgradeId.Foam: return fresh ? "굴러가며 불을 삼키고 커지다 터지는 거품" : n == 3 ? "거품 2개" : n == 5 ? "거품 3개 · 더 크게" : "더 크게 부푼다";
                case UpgradeId.Bubble: return fresh ? "불을 방울에 가둬 띄웠다가 터뜨린다" : "방울 " + (n == 2 || n == 3 ? 2 : n == 4 ? 3 : 4) + "발 · 더 큰 불도 가둔다";
                case UpgradeId.Manhole: return fresh ? "불이 몰린 맨홀에서 물기둥이 솟는다" : n == 3 ? "맨홀 2곳에서" : n == 4 ? "맨홀 3곳에서" : n == 5 ? "맨홀 4곳 · 더 큰 물기둥" : "물기둥이 굵어진다";
                case UpgradeId.Chain: return fresh ? "물줄기가 몹 3마리를 번개처럼 튀며 꿰뚫는다 · 타는 집의 사람도 끌어낸다" : n == 3 ? "두 줄기" : n == 5 ? "6마리 · 두 줄기" : "한 마리 더 튄다";
                case UpgradeId.Whip: return fresh ? "호스를 휘둘러 둘레 불을 밀어낸다" : n == 3 ? "두 갈래로 휘두른다" : n == 5 ? "세 갈래 · 더 넓게" : "더 넓게 휘두른다";
                case UpgradeId.Tank: return "모든 물이 굵고 세진다 · 증기 폭발이 빨리 찬다";
                case UpgradeId.Boots: return fresh ? "더 빨리 달린다 · 불 바닥을 밟아 끈다" : "더 빨리 달린다";
                case UpgradeId.Suit: return fresh ? "불에 덜 다치고 체력이 는다 · 닿은 불이 튕겨 나간다" : "더 단단해진다 · 더 세게 튕긴다";
                case UpgradeId.Cannon: return "진화! 관통하는 물줄기가 사방을 휩쓴다";
                case UpgradeId.Crown: return "진화! 둘레에 물 고리 · 8갈래 물줄기가 돌며 뿜는다";
                case UpgradeId.BalloonStorm: return "진화! 튕길 때마다 풍선이 둘로 갈라진다";
                case UpgradeId.Tornado: return "진화! 하얀 회오리가 떠돌며 불을 빨아들인다";
                case UpgradeId.IceField: return "진화! 지뢰끼리 얼음 길로 이어진다";
                case UpgradeId.Avalanche: return "진화! 화면을 가로지르는 거품 파도";
                case UpgradeId.BubbleFall: return "진화! 갇힌 불을 한데 모아 큰 방울로 터뜨린다";
                case UpgradeId.Waterline: return "진화! 맨홀이 줄지어 도미노처럼 터진다";
                case UpgradeId.Surge: return "진화! 맞은 몹마다 물이 터져 둘레까지 휩쓴다";
                case UpgradeId.Whirl: return "진화! 휘두른 자리에 물 고리가 남아 돈다";
                case UpgradeId.OverPump: return "진화! 몇 초마다 내 둘레 물 대폭발";
                case UpgradeId.JetBoots: return "진화! 달린 자리에 요괴를 녹이는 물길";
                case UpgradeId.PhoenixSuit: return "진화! 쓰러지면 물 날개로 부활하며 폭발";
                case UpgradeId.Nozzle: return "모든 물이 세진다";
                case UpgradeId.Feed: return "무기가 더 자주 나간다";
                case UpgradeId.Wide: return "무기가 더 넓게 닿는다";
                default: return "체력 30 회복";
            }
        }

        /// <summary>숲 카드 설명: 승인 샘플의 레벨별 문구. nextLevel 6 = 최고급(Lv6 카드는 진화 id로 온다). 보조는 수치만, 무기 Lv5는 진화 짝 한 줄.</summary>
        public static string DescribeFree(UpgradeId id, int nextLevel)
        {
            int n = Math.Max(1, Math.Min(5, nextLevel));
            string[] t;
            switch (Loadout.BaseOf(id))
            {
                case UpgradeId.Hose: t = new[] { "겨눈 쪽으로 물줄기 · 펌프가 붙어 레벨마다 굵고 멀리", "더 굵게 · 더 멀리 · 3마리 꿰뚫는다", "더 굵게 · 더 멀리 · 4마리 꿰뚫는다", "더 굵게 · 더 멀리 · 5마리 꿰뚫는다", "가장 굵고 멀리 · 6마리 꿰뚫는다", "고압 방수포: 모두 꿰뚫는 굵은 물기둥 · 몇 초마다 내 둘레 물 대폭발" }; break;
                case UpgradeId.Sprinkler: t = new[] { "스프링클러 2개가 내 둘레를 돈다", "더 넓게 · 더 세게 튕긴다", "스프링클러 3개 · 물줄기를 뿜는다", "4갈래 노즐 · 더 빨리 돈다", "스프링클러 4개 · 금빛 · 물보라 폭발", "물 왕관: 물 고리 + 8갈래 무지개 분사" }; break;
                case UpgradeId.Balloon: t = new[] { "물풍선 1개 · 4번 튕긴다", "물풍선 2개 · 5번", "더 큰 풍선 · 6번 튕긴다", "물풍선 3개 · 7번", "금빛 풍선 · 튕길 때마다 물보라 폭발", "물풍선 폭우: 튕길 때마다 둘로 갈라진다" }; break;
                case UpgradeId.Extinguisher: t = new[] { "소화기 1개가 나갔다 돌아온다", "더 멀리 · 더 세게", "소화기 2개", "더 멀리 · 분말 꼬리가 길게", "금빛 소화기 3개 · 9칸까지", "분말 회오리: 떠도는 하얀 회오리가 요괴를 빨아올린다" }; break;
                case UpgradeId.Mine: t = new[] { "지나간 자리에 지뢰 2개", "지뢰 3개 · 더 넓게 언다", "지뢰 4개 · 얼음이 터지며 둘레도 언다", "지뢰 5개 · 더 넓게", "금빛 지뢰 6개 · 거대한 냉기 폭발", "빙결 지대: 지뢰끼리 얼음 길 · 건너는 요괴는 그대로 언다" }; break;
                case UpgradeId.Bubble: t = new[] { "요괴를 방울에 가둬 띄웠다가 펑", "방울 2발 · 더 빠르게", "방울 3발 · 큰 요괴도 가둔다", "방울 3발 · 연사", "방울 4발 · 금빛 · 큰 물보라", "방울 폭포: 거대 방울로 모아 물폭포로 터뜨린다" }; break;
                case UpgradeId.Manhole: t = new[] { "요괴가 몰린 맨홀에서 물기둥이 솟는다", "맨홀 2곳 · 더 자주", "물기둥이 굵어진다 · 푸른 빛", "맨홀 3곳 · 하늘 높이 날린다", "맨홀 4곳 · 금빛 물기둥", "수도관 폭발: 물기둥이 도미노로 터진다" }; break;
                case UpgradeId.Chain: t = new[] { "물줄기가 요괴 3마리를 번개처럼 튄다", "4마리 · 더 굵게", "두 줄기 · 하얀 심", "5마리 · 더 빠르게", "6마리 · 금빛 번개 · 끝에서 물 폭발", "해일 사슬: 맞은 요괴마다 물이 터진다" }; break;
                case UpgradeId.Boots: return "이동 속도 +" + (10 * n) + "%";
                case UpgradeId.Suit: return "받는 피해 −" + (10 * n) + "% · 최대 체력 +" + (10 * n);
                case UpgradeId.Nozzle: return "모든 물 피해 +" + (10 * n) + "%";
                case UpgradeId.Feed: return "무기 재사용 −" + (8 * n) + "% · 물대포 연사";
                case UpgradeId.Wide: return "무기 범위·반경 +" + (10 * n) + "%";
                default: return Describe(id, nextLevel);
            }
            if (Loadout.IsEvolution(id)) return t[5];
            // 무기 Lv5 카드: 무엇을 들어야 Lv6이 나오는지 한 줄.
            UpgradeId? pair = Loadout.FreePairOf(id);
            if (n == Loadout.MaxLevel && pair.HasValue) return t[n - 1] + "\n진화 짝: " + Name(pair.Value);
            return t[n - 1];
        }

        /// <summary>카드·연출 아이콘 이름(Art/LevelUp/icon_*): 최고급은 원래 아이템 아이콘의 Lv6 판.</summary>
        public static string IconOf(UpgradeId id)
        {
            switch (Loadout.BaseOf(id))
            {
                case UpgradeId.Hose: return "hose";
                case UpgradeId.Sprinkler: return "sprinkler";
                case UpgradeId.Balloon: return "balloon";
                case UpgradeId.Extinguisher: return "extinguisher";
                case UpgradeId.Mine: return "mine";
                case UpgradeId.Foam: return "foam";
                case UpgradeId.Bubble: return "bubble";
                case UpgradeId.Manhole: return "manhole";
                case UpgradeId.Chain: return "chain";
                case UpgradeId.Whip: return "whip";
                case UpgradeId.Tank: return "tank";
                case UpgradeId.Boots: return "boots";
                case UpgradeId.Suit: return "suit";
                // 숲 새 보조(2026-10-08): 그림을 굽기 전까지 비슷한 아이콘을 빌려 쓴다.
                case UpgradeId.Nozzle: return "hose";
                case UpgradeId.Feed: return "tank";
                case UpgradeId.Wide: return "sprinkler";
                default: return null;
            }
        }
    }
}
