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

        /// <summary>이 스테이지 레벨업에서 나오는 노란 특수 카드들.</summary>
        public UpgradeId[] Specials;

        /// <summary>소방서 브리핑에 보이는 이 스테이지의 위협 한 줄.</summary>
        public string Threat;

        /// <summary>그 위협을 막는 일반 장비 둘: 소방서에서 하나를 골라 Lv1로 들고 간다. 레벨업 카드에도 두 배로 나온다.</summary>
        public UpgradeId[] Counters;

        /// <summary>이 스테이지에선 카드로 안 나오는 일반 장비(위협과 상관없는 것). 노란 카드는 Specials가 정한다.</summary>
        public UpgradeId[] Excluded = new UpgradeId[0];
    }

    /// <summary>
    /// 스테이지 표. 1스테이지의 재미 밀도(레벨업·사건 빈도)를 기준으로 맞추고,
    /// 스테이지마다 새 위협 하나와 그 대응책(레어 아이템)을 넣는다(docs/prototype-c-balance.md).
    /// </summary>
    public static class SurvivorStages
    {
        public const int Count = 3;

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
            SpawnRate = 1.2f,
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
        };

        /// <summary>n번 스테이지(1부터). 범위를 벗어나면 1스테이지.</summary>
        public static StageRules Get(int n)
        {
            return n == 3 ? Factory : n == 2 ? Forest : Town;
        }

        /// <summary>깬 뒤 넘어갈 스테이지. 마지막을 깨면 처음으로 돌아간다.</summary>
        public static int Next(int n)
        {
            return n >= Count ? 1 : n + 1;
        }
    }
}
