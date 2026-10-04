using System;
using System.Collections.Generic;

namespace FireGame.Prototypes.Logic
{
    /// <summary>
    /// 스테이지 하나의 세계 쪽 규칙. 소방관 성장(경험치 곡선·카드 수치)은 모든 스테이지가 같고,
    /// 난이도는 여기 수치로만 올린다. 정책은 docs/prototype-c-balance.md.
    /// </summary>
    public sealed class StageRules
    {
        public int Number;

        /// <summary>띠·결과창에 쓰는 이름("마을").</summary>
        public string Name;

        /// <summary>지킬 동네를 깐다. 판마다 새로 깐다.</summary>
        public Func<List<Structure>> Map;

        /// <summary>이 시각마다 안 탄 건물 하나에 불이 난다(신고). 같은 시각이 둘이면 두 곳.</summary>
        public float[] ReportTimes;

        /// <summary>적 체력 배율(시간에 따른 성장 위에 곱한다).</summary>
        public float EnemyHp = 1f;

        /// <summary>가장자리 스폰 속도 배율.</summary>
        public float SpawnRate = 1f;

        /// <summary>건물 불이 초당 커지는 양.</summary>
        public float FireGrowth = SurvivorSim.FireGrowth;

        /// <summary>건물이 꾸준한 물(호스·대원·포탑)을 먹는 비율. 한 방 물은 영향 없다.</summary>
        public float BuildingWater = SurvivorSim.BuildingWater;

        /// <summary>대화재(3:00~) 동안 바람 번짐 확률. 바람이 없는 스테이지에선 쓰지 않는다.</summary>
        public float FinaleWindChance = SurvivorSim.WindSpreadChance;

        /// <summary>가장자리 스폰에서 큰 불 비율의 상한(시간이 갈수록 이만큼까지 오른다).</summary>
        public float BlazeMax = 0.3f;

        /// <summary>1분 뒤부터 가장자리 스폰에서 다트 비율.</summary>
        public float DartShare = 0.2f;

        /// <summary>가장자리 스폰에서 불다람쥐 비율(나무를 노린다).</summary>
        public float SquirrelShare;

        /// <summary>타는 나무가 불씨를 뱉는 간격 배율(건물 기준). 0이면 안 뱉는다(바람으로만 번진다).</summary>
        public float TreeSpit = 2f;

        /// <summary>이 간격마다 재 박쥐 무리가 가장자리에서 날아온다(0이면 안 온다).</summary>
        public float BatFlockEvery;

        /// <summary>크게 타는 건물(세기 0.8~)이 이 간격마다 가장 가까운 건물로 불을 옮긴다(초). 0이면 안 옮긴다.</summary>
        public float SpreadEvery = 8f;

        /// <summary>산불: 바람이 불어 타는 나무가 바람 쪽 이웃에 불을 옮긴다.</summary>
        public bool Wind;

        /// <summary>바람이 고를 수 있는 방향(여덟 방향 번호, 0=동 … 2=북 … 6=남). null이면 여덟 방향 모두.</summary>
        public int[] WindArc;

        /// <summary>공단: 가장자리 스폰에서 기름 방울 비율(SurvivorSim.OilFrom초부터).</summary>
        public float OilShare;

        /// <summary>공단: 가스통(약품 드럼)이 터질 때 흩뿌리는 기름 불 수. 0이면 안 흩뿌린다.</summary>
        public int DrumSpill;

        /// <summary>항구: 이 간격마다 불붙은 배가 바다 가장자리에서 떠내려온다(0이면 안 온다). 첫 배는 SurvivorSim.FirstBoat초.</summary>
        public float BoatEvery;

        /// <summary>항구: 가장자리 스폰에서 불 갈매기 비율(바다 쪽에서 날아온다).</summary>
        public float GullShare;

        /// <summary>바다 스테이지(항구): 물줄기가 물을 넘어 날고(바다 위 불배를 부두에서 쏜다), 바다에 떨어진 가장자리 스폰은 뭍에서 다시 뽑는다
        /// (좁은 강은 둑으로 밀면 되지만 넓은 바다는 북쪽 끝에 갇혀 적의 절반이 사라졌다). 마을 강은 예전처럼 물줄기를 막는다:
        /// 전 스테이지에 켜자 마을 기본 봇의 레벨업이 빨라져 숲의 밀도 밴드(마을 ×1.2)가 깨졌다(docs §17).</summary>
        public bool Sea;

        /// <summary>야시장: 가장자리 스폰에서 폭죽 비율(30초부터).</summary>
        public float PopperShare;

        /// <summary>야시장: 등줄. 맵을 깐 뒤 (구조물 인덱스 a, b) 쌍을 돌려준다. null이면 등줄 없음.</summary>
        public Func<List<Structure>, List<int[]>> Links;

        /// <summary>이 스테이지 레벨업에서 나오는 노란 특수 카드들.</summary>
        public UpgradeId[] Specials;

        /// <summary>소방서 브리핑에 보이는 이 스테이지의 위협 한 줄.</summary>
        public string Threat;

        /// <summary>그 위협을 막는 일반 장비 둘: 소방서에서 하나를 골라 Lv1로 들고 간다. 레벨업 카드에도 두 배로 나온다.</summary>
        public UpgradeId[] Counters;

        /// <summary>이 스테이지에선 카드로 안 나오는 일반 장비(위협과 상관없는 것). 노란 카드는 Specials가 정한다.</summary>
        public UpgradeId[] Excluded = new UpgradeId[0];

        /// <summary>소방서와 판 시작에 보여 주는 할 일 한 줄("부두 끝에 서서 바다 위 배를 쏘아 끈다").</summary>
        public string Goal;

        /// <summary>이 스테이지의 대화재 종류(3:00 뒤 SurvivorSim의 공통 감독 위에 하나씩 덧붙는다).</summary>
        public FinaleKind Finale = FinaleKind.Warehouse;

        /// <summary>대화재 띠에 뜨는 이름("불 전선")과 그때 할 일 한 줄.</summary>
        public string FinaleName = "대화재";
        public string FinaleGoal = "끝까지 지켜라";

        /// <summary>항구: 가장자리 스폰에서 불 게 비율(바다에서 기어 올라온다).</summary>
        public float CrabShare;

        /// <summary>야시장: 가장자리 스폰에서 풍등 비율(하늘을 떠서 점포 지붕에 내려앉는다).</summary>
        public float LanternShare;
    }

    /// <summary>대화재 종류: 공통 감독(압력·신고·불씨·큰 불 고리) 위에 스테이지마다 다른 사건 하나.</summary>
    public enum FinaleKind
    {
        /// <summary>마을: 창고가 타오른다(기본 틀).</summary>
        Warehouse,

        /// <summary>숲: 북쪽 숲에서 불의 띠가 캠프로 내려온다. 줄의 불을 끄면 늦춰진다.</summary>
        FireFront,

        /// <summary>공단: 드럼이 하나씩 점화되어 카운트다운 뒤 터진다. 끄면 막는다.</summary>
        ChainBlast,

        /// <summary>항구: 큰 유조선이 부두 가운데 닿아 바다 위로 불기름을 흘린다. 선원이 갇혀 있다.</summary>
        Tanker,

        /// <summary>야시장: 가판대가 전부 쏘고 무대도 쏘며 등줄이 무대에서부터 차례로 탄다.</summary>
        RocketStorm,
    }

    /// <summary>
    /// 스테이지 표. 1스테이지의 재미 밀도(레벨업·사건 빈도)를 기준으로 맞추고,
    /// 스테이지마다 새 위협 하나와 그 대응책(레어 아이템)을 넣는다(docs/prototype-c-balance.md).
    /// </summary>
    public static class SurvivorStages
    {
        public const int Count = 5;

        private static readonly StageRules Town = new StageRules
        {
            Number = 1,
            Name = "마을",
            Map = SurvivorTown.Build,
            ReportTimes = SurvivorSim.ReportTimes,
            // 불 규칙 강화(측정 5): 불이 오래 버티는 만큼 몰려오는 불 몹을 늘려 몸 압박(위기 판)을 되살린다.
            SpawnRate = 1.3f,
            Specials = new[] { UpgradeId.Heli, UpgradeId.Ambulance, UpgradeId.Truck, UpgradeId.Sprinkler },
            // 배우는 판: 모든 장비가 나온다. 대비는 구조와 발(큰 불에 갇힌 주민, 멀리 떨어진 신고).
            Threat = "큰 불에 주민이 갇힌다 · 신고가 멀리서 온다",
            Counters = new[] { UpgradeId.Partner, UpgradeId.Boots },
            Goal = "큰 불부터, 갇힌 사람 먼저",
        };

        private static readonly StageRules Forest = new StageRules
        {
            Number = 2,
            Name = "산불 숲",
            Map = SurvivorForest.Build,
            // 신고는 건물 수에 맞춰 12번(마을 9채에 15번, 숲 7채): 9번은 할 일이 적어 지루했고(분당 사건 2.9 대 3.8),
            // 15번은 건물 한 채에 불이 너무 자주 나 동네를 늘 잃었다(재미 밀도 측정).
            ReportTimes = new[] { 10f, 30f, 50f, 70f, 90f, 110f, 120f, 140f, 160f, 180f, 200f, 230f },
            // 숲의 난이도는 배율이 아니라 새 규칙(바람·다람쥐·박쥐)에서 온다.
            EnemyHp = 1f,
            // 1.2였을 땐 아이템 다이어트 뒤 숙련 봇이 마을(22승)보다 숲(24승)을 더 쉽게 깼다(docs §16): 다른 두 맵과 같은 1.3.
            SpawnRate = 1.3f,
            // 대화재 동안 바람이 거세져 산불이 캠프를 덮친다.
            FinaleWindChance = 0.35f,
            BlazeMax = 0.25f,
            DartShare = 0f,
            SquirrelShare = 0.01f,
            BatFlockEvery = 20f,
            Wind = true,
            // 숲은 북쪽, 캠프는 남쪽: 바람은 늘 남서·남·남동으로만 분다(산불이 캠프를 향한다).
            WindArc = new[] { 5, 6, 7 },
            // 숲은 바람이 나무·건물로 불을 옮기므로 건물끼리 직접 번지는 규칙은 끈다(둘 다 켜면 봇 0/10).
            SpreadEvery = 0f,
            // 숲의 압력은 번짐이다. 건물 물을 마을만큼 줄이면 집 7채 중 4채가 금방 무너져 봇이 0승이었다(docs §13) → 마을의 두 배.
            BuildingWater = 0.5f,
            // 나무 수십 그루가 보통(×2) 간격으로 뱉으면 불씨 떼가 동네를 덮는다.
            TreeSpit = 4f,
            Specials = new[] { UpgradeId.Heli, UpgradeId.Ambulance, UpgradeId.Rain, UpgradeId.Retardant },
            // 산불은 옮겨다닌다: 몸 둘레의 장막(박쥐·불씨)과 멀리 날아가는 드론이 답이고, 선 자리만 지키는 포탑은 못 따라간다.
            Threat = "바람이 불을 캠프로 민다 · 재 박쥐 떼",
            Counters = new[] { UpgradeId.Curtain, UpgradeId.Drone },
            Excluded = new[] { UpgradeId.Turret },
            Goal = "바람 위쪽 나무부터 끈다",
            // 숲의 대화재: 북쪽 숲에서 불의 띠가 캠프로 내려온다(맵 특색 패스).
            Finale = FinaleKind.FireFront,
            FinaleName = "불 전선",
            FinaleGoal = "내려오는 불의 띠를 끊어라",
        };

        private static readonly StageRules Factory = new StageRules
        {
            Number = 3,
            Name = "공단",
            Map = SurvivorFactory.Build,
            // 공장 8채가 마을과 같은 고리에 있어 신고 표도 마을과 같다.
            ReportTimes = SurvivorSim.ReportTimes,
            // 마을과 같은 배율: 1.2면 레벨업 간격이 마을의 1.36배라 할 일이 느리게 왔다(지형 패스 측정). 봇 밴드는 바닥이다.
            SpawnRate = 1.3f,
            BlazeMax = 0.3f,
            DartShare = 0.1f,
            OilShare = 0.12f,
            DrumSpill = 4,
            Specials = new[] { UpgradeId.Heli, UpgradeId.Ambulance, UpgradeId.Sprinkler, UpgradeId.Foam },
            // 기름 불은 바닥에 깔린다: 밟아 끄는 장화와 골목을 지키는 포탑이 답이고, 건물에만 떨어뜨리는 드론은 무력하다.
            Threat = "드럼이 줄줄이 터져 기름 불이 깔린다",
            Counters = new[] { UpgradeId.Boots, UpgradeId.Turret },
            Excluded = new[] { UpgradeId.Drone },
            Goal = "드럼 곁에서 기름 방울을 터뜨리지 마라",
            // 공단의 대화재: 드럼이 하나씩 점화되어 카운트다운 뒤 터진다. 끄면 막는다(맵 특색 패스).
            Finale = FinaleKind.ChainBlast,
            FinaleName = "연쇄 폭발",
            FinaleGoal = "점화된 드럼을 끄면 막는다",
        };

        private static readonly StageRules Harbor = new StageRules
        {
            Number = 4,
            Name = "항구",
            Map = SurvivorHarbor.Build,
            // 부둣가 8채 + 창고: 마을과 같은 신고 표. 앞쪽 신고 둘을 더해 봤더니 경험치만 늘어 쉬워졌다(24승, 최저 체력 41%)(docs §17).
            ReportTimes = SurvivorSim.ReportTimes,
            // 바다가 스폰 방향의 40%를 지운다(바다에 떨어진 스폰은 뭍에서 다시 뽑지만 부둣가 줄은 뭍 쪽에서만 공격받는다):
            // 1.3이면 숙련 봇이 마을과 같은 수준(19~23 대 22승)이라 그만큼 뭍 스폰을 늘린다(docs §17).
            SpawnRate = 1.5f,
            BlazeMax = 0.3f,
            // 다트는 마을 몹이다: 항구는 제 몹(갈매기·게)으로 채운다(맵 특색 패스, 전 0.1).
            DartShare = 0f,
            // 항구의 난이도는 새 규칙(불배·갈매기)에서 온다(docs §17).
            // 0.08이면 기본 봇이 부두 끝에서 배를 기다리는 동안 곁에 아무것도 없어 빈 시간이 17%(마을 7%)였다: 바다에서 오는 갈매기를 늘린다(docs §17).
            // 갈매기가 폭격기(지붕에 불을 떨어뜨린다)가 되며 0.16 → 0.12, 그 자리에 불 게 0.06(맵 특색 패스).
            GullShare = 0.12f,
            CrabShare = 0.06f,
            // 14로 자주 띄우자 오히려 쉬워졌다(21 → 27승): 배가 늘 떠 있으면 소방정·파도가 쉬지 않고 부둣가를 적셔 준다. 18로 두고 배 속도(BoatSpeed)를 올린다(docs §17).
            BoatEvery = 18f,
            Sea = true,
            // 부둣가 줄은 서로 가까워 번진다(바다는 못 건넌다: CrossesWater).
            SpreadEvery = 8f,
            Specials = new[] { UpgradeId.Heli, UpgradeId.Ambulance, UpgradeId.Fireboat, UpgradeId.Wave },
            // 불배는 물 위에 있다: 멀리 던지는 물폭탄과 부두 끝에 세우는 포탑이 답이고, 몸 둘레 장막은 배에 못 닿는다.
            Threat = "불붙은 배가 떠내려와 부두에 닿는다 · 바다에서 불 갈매기",
            Counters = new[] { UpgradeId.WaterBomb, UpgradeId.Turret },
            Excluded = new[] { UpgradeId.Curtain },
            Goal = "부두 끝에 서서 바다 위 배를 쏘아 끈다",
            // 항구의 대화재: 큰 유조선이 부두 가운데 닿아 바다 위로 불기름을 흘린다(맵 특색 패스).
            Finale = FinaleKind.Tanker,
            FinaleName = "유조선 좌초",
            FinaleGoal = "유조선을 끄고 선원을 구하라",
        };

        private static readonly StageRules Market = new StageRules
        {
            Number = 5,
            Name = "야시장",
            Map = SurvivorMarket.Build,
            Links = SurvivorMarket.Links,
            ReportTimes = SurvivorSim.ReportTimes,
            SpawnRate = 1.3f,
            BlazeMax = 0.3f,
            // 다트는 마을 몹이다: 야시장은 폭죽·풍등으로 채운다(맵 특색 패스, 전 0.1 / 폭죽 0.1).
            DartShare = 0f,
            PopperShare = 0.08f,
            // 풍등 하나 = 점포 불 하나: 0.08이면 10초마다 점포가 붙어 숙련 봇 6승(동네 패 23). 0.04로(측정 ⑤).
            LanternShare = 0.04f,
            // 점포끼리는 등줄로만 옮긴다(둘 다 켜면 다닥다닥 붙은 점포가 한 번에 탄다).
            SpreadEvery = 0f,
            TreeSpit = 0f,
            Specials = new[] { UpgradeId.Heli, UpgradeId.Ambulance, UpgradeId.Shells, UpgradeId.Mist },
            // 등줄을 적시며 골목을 걷는 장막과 사람 많은 점포를 구하는 대원이 답이고, 선 자리만 지키는 포탑은 줄 따라 달리는 불을 못 쫓는다.
            Threat = "등줄을 타고 불이 점포를 건넌다 · 불꽃 가판대가 하늘로 불을 쏜다",
            Counters = new[] { UpgradeId.Curtain, UpgradeId.Partner },
            Excluded = new[] { UpgradeId.Turret },
            Goal = "등줄을 적셔 끊고 가판대부터 끈다",
            // 야시장의 대화재: 가판대가 전부 쏘고 무대도 쏘며 등줄이 무대에서부터 차례로 탄다(맵 특색 패스).
            Finale = FinaleKind.RocketStorm,
            FinaleName = "불꽃 폭주",
            FinaleGoal = "무대부터 끄고 등줄을 적셔라",
        };

        /// <summary>번호순. 정적 초기화 순서상 위 다섯 뒤에 있어야 한다.</summary>
        private static readonly StageRules[] All = { Town, Forest, Factory, Harbor, Market };

        /// <summary>n번 스테이지(1부터). 범위를 벗어나면 1스테이지.</summary>
        public static StageRules Get(int n)
        {
            return n >= 1 && n <= Count ? All[n - 1] : Town;
        }

        /// <summary>깬 뒤 넘어갈 스테이지. 마지막을 깨면 처음으로 돌아간다.</summary>
        public static int Next(int n)
        {
            return n >= Count ? 1 : n + 1;
        }
    }
}
