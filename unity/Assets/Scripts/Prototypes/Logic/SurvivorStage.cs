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

        public EnemyKind BossKind = EnemyKind.Boss;
        public float BossHp = 1100f;

        /// <summary>가장자리 스폰에서 큰 불 비율의 상한(시간이 갈수록 이만큼까지 오른다).</summary>
        public float BlazeMax = 0.3f;

        /// <summary>1분 뒤부터 가장자리 스폰에서 다트 비율.</summary>
        public float DartShare = 0.2f;

        /// <summary>이 스테이지 레벨업에서 나오는 노란 특수 카드들.</summary>
        public UpgradeId[] Specials;
    }

    /// <summary>
    /// 스테이지 표. 스테이지마다 기본 압력(적 체력·스폰·불 속도·보스 체력)을 25~35% 올리고,
    /// 새 위협 하나와 그 대응책(레어 아이템)을 함께 넣는다.
    /// </summary>
    public static class SurvivorStages
    {
        public const int Count = 2;

        private static readonly StageRules Town = new StageRules
        {
            Number = 1,
            Name = "마을",
            Map = SurvivorTown.Build,
            ReportTimes = SurvivorSim.ReportTimes,
            Specials = new[] { UpgradeId.Heli, UpgradeId.Curtain, UpgradeId.Partner, UpgradeId.Truck, UpgradeId.Sprinkler },
        };

        private static readonly StageRules Forest = new StageRules
        {
            Number = 2,
            Name = "산불 숲",
            Map = SurvivorTown.Build,
            ReportTimes = SurvivorSim.ReportTimes,
            EnemyHp = 1.3f,
            SpawnRate = 1.2f,
            FireGrowth = SurvivorSim.FireGrowth * 1.1f,
            BossHp = 1100f * 1.4f,
            Specials = new[] { UpgradeId.Heli, UpgradeId.Curtain, UpgradeId.Partner, UpgradeId.Rain, UpgradeId.Retardant },
        };

        /// <summary>n번 스테이지(1부터). 범위를 벗어나면 1스테이지.</summary>
        public static StageRules Get(int n)
        {
            return n == 2 ? Forest : Town;
        }

        /// <summary>깬 뒤 넘어갈 스테이지. 마지막을 깨면 처음으로 돌아간다.</summary>
        public static int Next(int n)
        {
            return n >= Count ? 1 : n + 1;
        }
    }
}
