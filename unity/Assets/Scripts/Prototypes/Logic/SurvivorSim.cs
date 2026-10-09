using System;
using System.Collections.Generic;
using FireGame.Core.Sim;

namespace FireGame.Prototypes.Logic
{
    public enum SOutcome
    {
        Playing,
        Won,
        Lost,
    }

    public enum EnemyKind
    {
        Ember,
        Blaze,
        Dart,

        /// <summary>산불 숲: 나무를 노려 달려가 불을 붙이는 불다람쥐.</summary>
        Squirrel,

        /// <summary>산불 숲: 무리 지어 날아와 소방관을 쫓는 재 박쥐.</summary>
        Bat,

        /// <summary>공단: 불붙은 폐유 덩어리. 느리고 튼튼하며, 걸어온 자리와 죽은 자리에 건물을 태우는 기름 불을 남긴다.</summary>
        Oil,

        /// <summary>항구: 불 갈매기(폭격기). 바다 쪽에서 날아와(물을 건넌다) 건물 지붕에 불을 떨어뜨리고 바다로 돌아간다.</summary>
        Gull,

        /// <summary>야시장: 폭죽. 깡충깡충 뛰어오고, 잡으면 불씨 둘이 튀어 점포를 노린다(점포 곁에서 터뜨리지 마라).</summary>
        Popper,

        /// <summary>항구: 불 게. 바다에서 기어 올라 옆걸음으로 건물까지 가서 불을 지피고, 그 뒤 소방관을 쫓는다. 단단하고 잘 안 밀린다.</summary>
        Crab,

        /// <summary>야시장: 풍등. 하늘을 천천히 떠서 점포 지붕에 내려앉아 불을 내고 타 없어진다. 쏘아 떨어뜨린다.</summary>
        SkyLantern,

        /// <summary>마을: 불쥐. 떼로 한 줄, 앞 쥐를 따라 집으로 달려가 닿으면 불을 붙이고 사라진다.</summary>
        Rat,

        /// <summary>마을: 횃불 도깨비. 집 앞에 멈춰 머리 불을 키우고(예고) 지붕에 횃불을 던진다. 맞으면 예고가 끊긴다.</summary>
        Goblin,

        /// <summary>마을: 불풍선. 떠서 집 위로 가 퓨즈 뒤 터지며 둘레 집 여럿에 불을 낸다. 퓨즈 전에 터뜨리면 그냥 꺼진다.</summary>
        FireBalloon,

        /// <summary>마을(엘리트): 불곰. 습격을 이끈다. 집들을 밀고 지나가며 불을 내고, 잡으면 보물상자.</summary>
        Bear,

        /// <summary>마을(보스): 화마. 3:00 랜드마크에서 일어나 마을 한가운데로 걸으며 불쥐를 쏟는다.</summary>
        Hwama,
    }

    public enum ShotKind
    {
        Drop,
        Jet,
    }

    public sealed class Enemy
    {
        public EnemyKind Kind;
        public Vec2 Pos;
        public Vec2 Knock;
        public float Hp;
        public float MaxHp;
        public float Speed;
        public float Radius;
        public float Touch;
        public int Xp;
        public float HitFlash;

        /// <summary>방화복에 튕긴 뒤 다시 튕기기까지(초).</summary>
        public float BounceCool;

        /// <summary>액체질소에 언 남은 시간(초). 언 동안 못 움직이고 닿아도 안 덴다. 풀릴 때 깨지며 피해.</summary>
        public float Frozen;

        /// <summary>비눗방울에 갇힌 남은 시간(초). 갇힌 동안 떠 있고, 다 되면 터지며 잡힌다.</summary>
        public float Captured;

        /// <summary>채찍에 다시 맞기까지(초).</summary>
        public float WhipCool;

        /// <summary>스프링클러·물 왕관에 맞은 뒤 다시 맞을 때까지(초).</summary>
        public float SprayCool;

        /// <summary>불쥐: 따라가는 앞 쥐(맨 앞이면 null).</summary>
        public Enemy Leader;

        /// <summary>불곰: 마지막으로 불을 낸 집(같은 집에 연달아 내지 않는다).</summary>
        public Structure LastBurnt;
        public float Slowed;
        public float Dot;

        /// <summary>마지막으로 발밑에 불을 남긴 뒤 걸어온 거리(큰 불만).</summary>
        public float Trail;

        /// <summary>불씨가 노리는 탈 것(없으면 소방관을 쫓는다).</summary>
        public Structure Goal;
        public float GoalClock;

        /// <summary>건물에서 튀어나온 불씨: 탈 것을 노린다. 가장자리에서 오는 불씨는 소방관을 쫓는다.</summary>
        public bool Seeker;

        /// <summary>노릴 탈 것 없이 떠돈 시간. 오래되면 사그라든다.</summary>
        public float Idle;

        /// <summary>대화재 고리의 큰 불: 레벨만큼 질기고 물줄기·장막·방화복에 거의 안 밀린다(3:00의 장비를 뚫고 몸에 닿는 압력).</summary>
        public bool Heavy;

        /// <summary>갈매기: 불을 떨어뜨리고 바다로 돌아가는 중. 게: 건물에 불을 지핀 뒤 소방관을 쫓는 중.</summary>
        public bool Dropped;

        /// <summary>지그재그·출렁임 위상(다람쥐·박쥐).</summary>
        public float Phase;
        public bool Dead;

        // --- 숲 샘플 상태(SampleCore): 공중(px 높이·속도), 잡힘(삼킨 거품·갇힌 방울), 샘플 얼음(녹으면 깨져 처치) ---
        public float AirZ, AirVz, AirVx, AirVy, AirSpin;
        public bool Launched;
        public bool Held;
        public float SFrozen;

        /// <summary>샘플 그림이 직접 그리는 요괴(삼켜짐·방울 속): 평소 요괴 그림을 숨긴다.</summary>
        public bool SHide;

        /// <summary>샘플 m.cd: 아이템별 다시 맞기까지(초). 없으면 null(처음 쓸 때 만든다).</summary>
        public Dictionary<string, float> SCd;

        /// <summary>샘플 m.flash(피격 번쩍)·m.seed(흔들림 위상).</summary>
        public float SSeed = -1f;
    }

    public sealed class Gem
    {
        public Vec2 Pos;
        public int Value;
        public bool Pulled;
        public float Speed;
    }

    public sealed class Shot
    {
        public ShotKind Kind;
        public Vec2 Pos;
        public Vec2 From;
        public Vec2 Vel;
        public Vec2 Target;
        public float Age;
        public float Life;
        public float Damage;
        public float Radius;
        public int Pierce;
        public bool Dead;
        public List<Enemy> Struck;

        /// <summary>방수포 제트가 이미 적신 구조물(한 줄기에 한 번씩).</summary>
        public List<Structure> Soaked;

        /// <summary>호스(손에 든 노즐)에서 나간 물. 뷰가 이것들을 한 줄기로 이어 그린다.</summary>
        public bool Hose;

        /// <summary>쥐고 쏜 물(집중 분사). 이 물만 건물에 쌓여 증기 폭발을 낸다. 수호자 자동 분사는 false.</summary>
        public bool Focus = true;

        /// <summary>산소통이 떨어질 건물.</summary>
        public Structure At;
    }

    public sealed class Puddle
    {
        /// <summary>물로 꺼졌다(다시 번지지 않는다).</summary>
        public bool Out;

        /// <summary>기름 불: 오래 타고, 닿은 탈 것에 옮겨붙는다.</summary>
        public bool Oil;
        public Vec2 Pos;
        public float Radius;
        public float Life;
        public float MaxLife;
    }

    public sealed class Pickup
    {
        public Vec2 Pos;
        public float Life;
    }

    /// <summary>야시장 등줄: 점포 A–B를 잇는다. 한쪽이 크게 타면 불이 줄을 타고 건너간다(Burn 0→1, From에서 출발). 젖으면 꺼지고 한동안 안 탄다.</summary>
    public sealed class Lantern
    {
        public Structure A;
        public Structure B;

        /// <summary>줄 불의 진행(0 = From 쪽 끝, 1 = 반대쪽). 음수면 안 탄다.</summary>
        public float Burn = -1f;
        public Structure From;

        /// <summary>젖어서 안 타는 남은 시간.</summary>
        public float Wet;

        /// <summary>건너간 뒤 다시 타기까지 쉬는 시간.</summary>
        public float Cool;

        /// <summary>출발 점포가 크게 탄 뒤 줄에 불이 붙기까지(SurvivorSim.LanternDelay에서 센다).</summary>
        public float Delay = SurvivorSim.LanternDelay;

        /// <summary>불꽃 폭주(야시장 대화재)가 붙인 줄 불: 출발 점포가 안 타도 끝까지 간다(젖으면 꺼진다).</summary>
        public bool Storm;

        public Structure Other(Structure s)
        {
            return s == A ? B : A;
        }
    }

    /// <summary>야시장 불꽃 가판대가 쏜 로켓: From에서 Target으로 Life초 날아가 떨어진 자리에 불을 낸다.</summary>
    public sealed class Rocket
    {
        public Vec2 From;
        public Vec2 Target;
        public float Age;
        public float Life;
        public bool Dead;
    }

    public sealed class Civilian
    {
        public Vec2 Pos;
        public float Life;
    }

    /// <summary>누가 쳤는지: 화면이 무기마다 다른 탄환·번쩍임을 그린다(판정과 무관).</summary>
    public enum HitSource : byte
    {
        Hose,
        Sprinkler,
        Balloon,
        Extinguisher,
        Mine,
        Foam,
        Bubble,
        Geyser,
        Chain,
        Whip,

        /// <summary>증기 폭발: 건물 불에 물줄기를 이어 맞혀 터진 김.</summary>
        Steam,

        /// <summary>구조대원 물줄기(숲).</summary>
        Crew,

        /// <summary>숲 샘플 무기(SampleItems): 맞힌 자리 그림은 규칙이 샘플 hitFx로 낸다.</summary>
        Sample,
    }

    /// <summary>화면용: 한 번에 크게 줄인 건물 불(물폭탄·헬기·장막 등). 지붕 위에 "−N%"를 띄운다.</summary>
    public struct FireKnock
    {
        public Structure At;

        /// <summary>줄어든 불 세기(0~1).</summary>
        public float Amount;
    }

    /// <summary>화면용 한 틱 기록. 피해 숫자·파편을 그린다.</summary>
    public struct Hit
    {
        public Vec2 Pos;
        public float Damage;
        public bool Crit;
        public bool Killed;
        public EnemyKind Kind;
        public HitSource Source;

        /// <summary>친 무기가 있던 곳(탄환이 여기서 날아간다).</summary>
        public Vec2 From;
    }

    /// <summary>소방관이 받은 피해의 출처(RunStats.HurtBy 칸).</summary>
    public enum HurtKind
    {
        Contact,
        Heat,
        Ground,
        Blast,
    }

    /// <summary>재미 밀도 계측: 봇 판을 스테이지끼리 비교한다(docs/prototype-c-balance.md).</summary>
    public sealed class RunStats
    {
        /// <summary>레벨업한 시각들.</summary>
        public readonly List<float> LevelTimes = new List<float>();

        /// <summary>진화한 수와 첫 진화 시각(없으면 -1).</summary>
        public int Evolutions;
        public float FirstEvolveAt = -1f;

        /// <summary>3:00(대화재 시작) 때 쥔 아이템 수(무기·보조, 진화는 그 무기 칸).</summary>
        public int FinaleItems;

        /// <summary>7칸 안에 불 몹도, 8칸 안에 타는 구조물도 없던 시간(걷기만 한 시간).</summary>
        public float IdleTime;

        /// <summary>건물이 하나라도 타던 시간.</summary>
        public float BuildingFire;

        /// <summary>나무·차만 타고 건물은 안 타던 시간.</summary>
        public float TreeFireOnly;

        /// <summary>신고 + 구조(+ 대형 신고·상자).</summary>
        public int Events;

        /// <summary>건물에서 건물로 불이 옮겨붙은 횟수.</summary>
        public int Spreads;

        /// <summary>기름 불이 탈 것에 옮겨붙은 횟수.</summary>
        public int OilFires;
        public float DamageTaken;

        /// <summary>구조로 찬 체력.</summary>
        public float HealRescue;
        public float MinHpRatio = 1f;

        /// <summary>증기 폭발 횟수.</summary>
        public int SteamBursts;

        /// <summary>한 판에서 가장 길게 이어진 콤보.</summary>
        public int MaxCombo;

        /// <summary>무너지기 직전에 구한 횟수(아슬아슬 구조).</summary>
        public int CloseCalls;

        /// <summary>방화복을 안 입었다면 받았을 불 피해(열기·바닥 불·불 몹 접촉의 원값). DamageTaken과 비교하면 방화복이 얼마나 막았는지 나온다.</summary>
        public float FireDamageRaw;

        /// <summary>주운 구급상자 수.</summary>
        public int KitsPicked;

        /// <summary>대화재 감독이 올린 가장 높은 압력 단계(0~3). 높을수록 그 판은 여유가 있었다.</summary>
        public int PressurePeak;

        /// <summary>대화재(3:00) 뒤 최저 체력 비율. 끝이 아슬아슬했는지는 이것과 HousesRoom으로 잰다.</summary>
        public float FinaleMinHp = 1f;

        /// <summary>대화재가 시작될 때의 레벨(아이템 다이어트가 보이는 숫자, docs §16).</summary>
        public int FinaleLevel;

        /// <summary>받은 피해를 출처별로(HurtKind 순서: 닿음·열기·바닥 불·폭발). 무엇에 쓰러지는지 잰다.</summary>
        public readonly float[] HurtBy = new float[4];

        /// <summary>수호자 쉼터(지킨 집 곁)에서 찬 체력.</summary>
        public float HealHaven;

        /// <summary>수호자: 쥐지 않아 저절로 나간 물줄기 수와 쥐고 쏜 물줄기 수.</summary>
        public int AutoShots;
        public int FocusShots;
    }

    /// <summary>
    /// 시험판 C(뱀서라이크) 규칙. 60Hz 고정 스텝, 시드 Rng로 결정적이다.
    /// 소방관은 움직이기만 하고 무기는 알아서 쏜다. 불 괴물을 끄면 구슬이 떨어지고, 구슬이 모이면 카드 3장 중 하나를 고른다.
    /// 1:20·2:40에 대형 신고, 3:00부터 대화재. 4:00까지 동네를 절반 넘게 지키면 이기고, 체력이 0이 되거나 동네를 잃으면 진다.
    /// </summary>
    public sealed partial class SurvivorSim
    {
        public const float Dt = 1f / 60f;
        public const float ArenaSize = 60f;
        /// <summary>한 판 길이. 이때까지 동네를 지키면 이긴다(보스는 없다: 목표는 건물과 사람).</summary>
        public const float RunTime = 240f;

        /// <summary>이때부터 끝까지 대화재: 랜드마크가 크게 타고 신고가 몰린다.</summary>
        public const float FinaleAt = 180f;
        public const float FinaleReportEvery = 8f;
        public const int FinalePeople = 5;
        public const float FinaleBurstEvery = 4f;
        public const int FinaleBurst = 8;

        /// <summary>
        /// 대화재 감독: 신고 틱마다 여유를 보고 압력 단계를 한 칸 올리거나 내린다(0~PressureMax).
        /// 여유 = 체력 PressureHp 이상이고 건물을 둘 이상 더 잃어도 되는 상태. 위험 = 체력 PressureLowHp 밑이거나 한 채만 더 잃으면 패배.
        /// 단계가 오르면 신고가 둘씩(2단계부터), 간격이 8→6.5→5→3.5초, 불씨가 8→12→16→20, 숲은 바람이 +0.1씩 거세진다.
        /// 잘하는 사람에겐 몰아붙이고 무너질 사람에겐 숨을 주어, 누가 하든 "한두 채 여유·체력 간당간당"으로 끝나게 한다.
        /// </summary>
        public const int PressureMax = 3;
        public const float PressureHp = 0.6f;
        /// <summary>이 밑이면 한 단계 내린다. 0.3이면 체력 30~60%에 머무는 서툰 판이 1단계에 묶여 더 쓰러졌다(기본 봇 마을 9 → 3/30). 0.45로 더 일찍 숨을 준다.</summary>
        public const float PressureLowHp = 0.45f;
        public const float PressureGapStep = 1.5f;
        public const int PressureBurstStep = 4;
        public const float PressureWindStep = 0.1f;
        public const float PressureFireStep = 0.15f;
        public const float PressureHeatStep = 0.5f;

        /// <summary>2단계부터 이 간격마다 큰 불 고리가 소방관을 에워싼다(8 + 4×단계 마리, 9칸). 3:00의 장비는 불을 다 끄므로 압력은 몸으로 간다.</summary>
        public const float FinaleRingEvery = 8f;
        public const int FinaleRingFrom = 2;
        public const int FinaleRingBase = 8;
        public const int PressureRingStep = 4;
        /// <summary>고리 반경: 9칸이면 스무 마리 사이 틈이 1.6칸이라 빠른 소방관이 빠져나간다(숙련 봇 체력 100%). 6칸이면 틈 0.7.</summary>
        public const float FinaleRingRadius = 6f;

        /// <summary>고리 큰 불의 속도 배율: 보통 큰 불(1.5)은 장화 신은 소방관을 못 따라간다.</summary>
        public const float HeavySpeed = 1.5f;

        public int FinaleRingCount
        {
            get { return FinaleRingBase + (PressureRingStep * FinalePressure); }
        }

        /// <summary>고리 큰 불의 체력 배율: 1 + 레벨 × 0.15(Lv20이면 4배). 레벨이 높을수록(장비가 셀수록) 질겨 장비로 녹이지 못한다.</summary>
        public const float HeavyPerLevel = 0.15f;
        public const float HeavyKnock = 0.15f;

        public float FinaleRingToughness
        {
            get { return 1f + (HeavyPerLevel * Level); }
        }

        /// <summary>지금 압력 단계(0~PressureMax). 대화재 밖에선 0.</summary>
        public int FinalePressure;

        /// <summary>이번 틱에 압력 단계가 올랐다(알림용).</summary>
        public bool JustPressureUp;

        /// <summary>이 단계에서 대화재 신고 사이 간격(초).</summary>
        public float FinaleReportGap
        {
            get { return FinaleReportEvery - (PressureGapStep * FinalePressure); }
        }

        /// <summary>이 단계에서 랜드마크가 한 번에 뿜는 불씨 수.</summary>
        public int FinaleBurstCount
        {
            get { return FinaleBurst + (PressureBurstStep * FinalePressure); }
        }

        /// <summary>건물을 몇 채 더 잃어도 되는지. 패배 조건(HousesLost*2 > HousesTotal)에서 거꾸로 센다: 0이면 한 채만 더 잃어도 패배.</summary>
        public int HousesRoom
        {
            get { return (HousesTotal / 2) - HousesLost; }
        }

        /// <summary>이번 틱에 랜드마크가 불씨를 뿜었다.</summary>
        public bool JustBurst;

        /// <summary>대형 신고: 큰 불에 여럿이 갇힌다. 다 구하면 보물상자.</summary>
        public static readonly float[] BigReportTimes = { 80f, 160f };

        /// <summary>이 판의 대형 신고 시각: 수호자 규칙이면 스테이지 것(있으면), 아니면 BigReportTimes.</summary>
        public float[] BigTimes
        {
            get { return Guardian && Stage.BigReportTimes != null ? Stage.BigReportTimes : BigReportTimes; }
        }
        public const float BigReportFire = 0.7f;
        public const int BigReportPeople = 3;
        public const float ChestLife = 30f;

        /// <summary>보물상자: 탕탕처럼 1~3번 연속 레벨업(1번 60%, 2번 30%, 3번 10%). 진화할 수 있으면 첫 장에 진화가 뜬다(Roll).</summary>
        public int RollChestPicks()
        {
            float r = Rand();
            return r < 0.6f ? 1 : r < 0.9f ? 2 : 3;
        }

        /// <summary>방금 연 상자가 준 연속 레벨업 수(그림용).</summary>
        public int LastChestPicks;

        public const int MaxEnemies = 350;
        public const int MaxGems = 400;
        public const float PlayerRadius = 0.4f;
        public const float BaseSpeed = 4f;
        public const float BaseMagnet = 1.8f;
        public const float BaseMaxHp = 100f;
        public const float SpawnDistance = 17f;

        /// <summary>0:00의 가장자리 스폰 배율(초당 SpawnRate × 이 값). 4:00엔 8배까지 오른다.</summary>
        public const float EarlySpawn = 1.5f;

        private const float CellSize = 2f;
        private const int Cells = (int)(ArenaSize / CellSize);

        public readonly List<Enemy> Enemies = new List<Enemy>();
        public readonly List<Gem> Gems = new List<Gem>();
        public readonly List<Shot> Shots = new List<Shot>();
        public readonly List<Puddle> BurningGround = new List<Puddle>();
        public readonly List<Civilian> Civilians = new List<Civilian>();

        /// <summary>이 판의 스테이지 규칙(맵·신고·배율·보스·특수 카드 풀).</summary>
        public readonly StageRules Stage;

        /// <summary>지켜야 하는 동네. 생성자에서 스테이지 맵대로 깐다.</summary>
        public readonly List<Structure> Structures;
        public readonly Loadout Build = new Loadout();
        public readonly RunStats Stats = new RunStats();

        public Vec2 Player = new Vec2(ArenaSize / 2f, ArenaSize / 2f);
        public Vec2 Facing = new Vec2(1f, 0f);

        /// <summary>호스를 겨눈 방향(길이는 상관없다). 뷰·봇·테스트가 Step 전에 넣는다.</summary>
        public Vec2 Aim = new Vec2(1f, 0f);

        /// <summary>호스 손잡이를 쥐고 있는지. 쥔 동안만 물대포·방수포가 나간다.</summary>
        public bool Spraying;

        /// <summary>
        /// 수호자 규칙(StageRules.Guardian에서 온다): 쥐지 않아도 가까운 불을 쏘고(자동 분사), 큰 신고에선 버티고,
        /// 동네를 잃어도 끝나지 않으며, 레벨만큼 곁의 불이 자라지 못한다. 테스트가 꺼서 옛 규칙을 잰다.
        /// </summary>
        public bool Guardian;

        /// <summary>이번 틱에 쥐지 않고 저절로 쏘고 있었다(그림용: 물줄기는 나가되 쥐는 손맛은 없다).</summary>
        public bool AutoFiring;

        /// <summary>물대포에서 물이 나가는 중(쥐었거나 자동).</summary>
        public bool HoseOn
        {
            get { return Spraying || AutoFiring; }
        }

        /// <summary>
        /// 자동 분사의 힘(쥐고 쏜 물 대비). 쥐면 온전한 힘에 증기까지(집중 분사).
        /// 수호 반경 밖에선 0.7로 보통 신고(0.35)도 28.5초가 걸리지만, 반경 안에선 불이 안 자라 자동 물로 꺼진다 — 끄려면 다가가라.
        /// 1.0이면 쥔 봇과 이동만 봇이 똑같았다(지킨 비율 81%, 최저 체력 30·33%). 0.7이면 쥐는 쪽이 더 버틴다(승 26·22, 최저 체력 42·25%, docs §20).
        /// </summary>
        public const float AutoPower = 0.7f;

        /// <summary>자동 분사가 노리는 가장 먼 불(물줄기 사거리 16칸/초 × 0.6초 ≈ 9.6).</summary>
        public const float AutoReach = 9.5f;

        /// <summary>호스 연사 간격. 촘촘해야 끊김 없는 물줄기로 보인다.</summary>
        public const float HoseInterval = 0.1f;
        public float Time;
        public float Hp = BaseMaxHp;
        public int Level = 1;
        public int Xp;
        public int Kills;
        public int Rescued;

        /// <summary>이어진 처치·진화 수. ComboWindow 안에 다음이 없으면 끊긴다.</summary>
        public int Combo;

        /// <summary>콤보가 끊기기까지 남은 시간(초).</summary>
        public float ComboClock;
        public const float ComboWindow = 1f;

        /// <summary>콤보가 이만큼 쌓일 때마다 구슬 배율이 +1 (최대 ComboMaxMult).</summary>
        public const int ComboStep = 20;
        public const int ComboMaxMult = 2;

        /// <summary>건물 불을 끄면 콤보가 이만큼 오른다(처치 하나는 1).</summary>
        public const int ComboPerDouse = 5;

        /// <summary>지금 콤보의 구슬 배율(1~ComboMaxMult).</summary>
        public int ComboMult
        {
            get { return Math.Min(ComboMaxMult, 1 + (Combo / ComboStep)); }
        }

        /// <summary>이번 틱에 구슬 배율이 올랐다.</summary>
        public bool JustComboTier;

        /// <summary>이번 틱에 끊긴 콤보 수(없으면 0).</summary>
        public int ComboEnded;

        /// <summary>떨어져 있는 공구상자(많아야 하나).</summary>
        public readonly List<Pickup> Toolboxes = new List<Pickup>();

        /// <summary>이 간격마다 부서진 건물이 있으면 공구상자가 하나 떨어진다(초).</summary>
        public const float ToolboxEvery = 40f;
        public const float ToolboxLife = 20f;

        /// <summary>공구상자로 되찾는 튼튼함(과 줄어드는 불 세기).</summary>
        public const float ToolboxRepair = 0.5f;

        /// <summary>튼튼함이 이 아래인 건물이 있어야 공구상자가 떨어진다.</summary>
        public const float DamagedBelow = 0.9f;

        /// <summary>고칠 건물이 없을 때 공구상자가 대신 채우는 체력.</summary>
        public const float ToolboxHeal = 20f;
        public const float PickupRange = 0.9f;

        /// <summary>이번 틱에 공구상자로 고친 건물.</summary>
        public readonly List<Structure> Repaired = new List<Structure>();

        /// <summary>이번 틱에 공구상자를 주웠다(고칠 건물이 없어 체력을 채운 경우도).</summary>
        public bool JustPickedToolbox;

        /// <summary>
        /// 구급상자: 40~80초 무작위 간격으로, 바닥에 없고 체력이 90% 미만일 때만 소방관 5~10칸 빈 땅에 떨어진다.
        /// 25초 뒤 사라지고 밟으면 체력 +35. 카드로는 안 나온다(레벨업 공짜 회복을 없앤 자리).
        /// </summary>
        public const float KitMin = 40f;
        public const float KitMax = 80f;
        public const float KitLife = 25f;
        public const float KitHeal = 35f;
        public const float KitBelow = 0.9f;
        public readonly List<Pickup> Kits = new List<Pickup>();

        /// <summary>이번 틱에 구급상자를 주웠다.</summary>
        public bool JustPickedKit;
        private float _kitClock;
        /// <summary>구급상자 타이머 전용 난수. 본 난수를 건드리면 같은 시드의 판이 바뀌어 측정·캡처가 흔들린다.</summary>
        private Rng _kitRng;
        /// <summary>지금 진행 중인 대형 신고 건물(없으면 null).</summary>
        public Structure BigReport;

        /// <summary>대화재 건물(물류창고·제재소). 3:00 전에는 null.</summary>
        public Structure Landmark;
        public bool Finale;

        /// <summary>대형 신고를 다 구하면 문 앞에 떨어지는 보물상자.</summary>
        public readonly List<Pickup> Chests = new List<Pickup>();
        public bool JustBigReport;

        /// <summary>잿더미 둥지: 무너진 집이 이 간격마다 불씨 RuinSpit개를 뱉는다(무너진 순간부터 한 간격 뒤).</summary>
        public const float RuinSpitEvery = 7f;

        /// <summary>수호 반경: Lv1 GuardBase칸에서 레벨마다 GuardPerLevel씩, GuardMax까지. 강해질수록 지켜지는 범위가 넓어진다.</summary>
        public const float GuardBase = 3f;
        public const float GuardPerLevel = 0.25f;
        public const float GuardMax = 8f;

        /// <summary>지금 수호 반경(칸, 구조물 가장자리까지). 수호자 규칙이 아니면 0.</summary>
        public float GuardRadius
        {
            get { return Guardian ? Math.Min(GuardMax, GuardBase + (GuardPerLevel * (Level - 1))) : 0f; }
        }
        public const int RuinSpit = 2;

        /// <summary>이번 틱 불씨를 뱉은 잿더미(그림용).</summary>
        public readonly List<Structure> RuinSpat = new List<Structure>();

        /// <summary>쉼터: 지켜 낸 집(안 타는) 가장자리 이 거리 안에 서 있으면 초당 HavenHeal씩 찬다.</summary>
        public const float HavenRange = 3f;
        public const float HavenHeal = 3f;

        /// <summary>이번 틱 쉼터에서 찼다(그림용). 어느 집 곁인지.</summary>
        public Structure Haven;

        /// <summary>수호자 대화재 감독: 지킨 비율이 이 이상이면 여유, 이 밑이면 위험.</summary>
        public const float GuardRoomy = 0.6f;
        public const float GuardTight = 0.45f;

        /// <summary>지킨 건물 비율(0~1). 건물이 없으면 1.</summary>
        public float VillageSaved
        {
            get
            {
                int houses = HousesTotal;
                return houses > 0 ? (houses - HousesLost) / (float)houses : 1f;
            }
        }

        /// <summary>쉼터: 방금 꺼진 건물은 지킨 집이 되고, 안 타는 지킨 집 곁에 서 있으면 찬다.</summary>
        private void TickHaven()
        {
            foreach (Structure d in Doused)
            {
                if (d.IsBuilding && !d.Collapsed) d.Guarded = true;
            }
            Haven = null;
            if (Hp >= MaxHp) return;
            foreach (Structure s in Structures)
            {
                if (!s.Guarded || s.Burning || s.Collapsed || s.DistanceTo(Player) > HavenRange) continue;
                Haven = s;
                float heal = Math.Min(MaxHp - Hp, HavenHeal * Dt);
                Hp += heal;
                Stats.HealHaven += heal;
                return;
            }
        }
        public bool JustFinale;
        public bool JustChest;

        /// <summary>지금 고르는 카드가 보물상자 카드인가(레벨업 카드가 아니라). 화면 제목용.</summary>
        public bool ChoosingChest;
        public SOutcome Outcome;

        /// <summary>null이 아니면 레벨업 카드를 고르는 중이다. 이때 Step은 시간을 멈춘다.</summary>
        public List<UpgradeId> PendingChoices;

        // --- 한 틱 신호(화면·소리용) ---
        public readonly List<Hit> Hits = new List<Hit>();

        /// <summary>이번 틱에 장화가 남긴 젖은 발자국 자리(밟은 바닥 불을 끌 때, 걷는 동안 간간이).</summary>
        public readonly List<Vec2> Footprints = new List<Vec2>();

        /// <summary>이번 틱에 방화복에 닿아 튕겨 나간 불 몹 자리.</summary>
        public readonly List<Vec2> SuitBounces = new List<Vec2>();

        /// <summary>이번 틱에 물·폭탄·거품에 꺼진 바닥 불 자리.</summary>
        public readonly List<Vec2> Extinguished = new List<Vec2>();

        /// <summary>이번 틱에 끄지 않은 바닥 불에서 새 불씨가 일어난 자리(불이 번졌다).</summary>
        public readonly List<Vec2> Reignited = new List<Vec2>();

        /// <summary>큰 불이 이만큼 걸을 때마다 발밑에 불을 남긴다.</summary>
        public const float TrailStep = 1.5f;
        public const int MaxBurningGround = 60;

        /// <summary>끄지 않은 바닥 불이 수명을 다했을 때 불씨로 다시 일어날 확률.</summary>
        public const float ReigniteChance = 0.5f;

        /// <summary>물 1 피해가 건물 불 세기를 줄이는 양. 나무는 Lv1 물대포로 금방 꺼지고, 건물은 BuildingWater만큼만 먹어 오래 걸린다.</summary>
        public const float WaterPerDamage = 0.035f;
        /// <summary>건물 불이 초당 커지는 양. 신고 불(0.35)이 약 16초면 다 탄다.</summary>
        public const float FireGrowth = 0.04f;

        /// <summary>끈 자리가 젖어 있는 시간. 짧아서 끄고 떠나면 금방 다시 탈 수 있다.</summary>
        public const float WetTime = 8f;

        /// <summary>큰 불일수록 물이 덜 먹힌다: 물 효과 = 1 − FireResist × 불 세기(0.3이면 83%, 1.0이면 45%).</summary>
        public const float FireResist = 0.55f;

        /// <summary>
        /// 건물이 꾸준한 물(호스·대원·포탑 적중)을 먹는 비율. 한 방 물(물폭탄·투하·증기·헬기)은 영향 없다.
        /// "건물 불 끄는 시간"의 손잡이 1: 0.3이면 Lv1 물대포로 신고 불(0.35)이 약 6초, 다 탄 건물이 약 15초(증기 포함). 아직 쉬우면 0.25, 지루하면 0.4.
        /// </summary>
        public const float BuildingWater = 0.3f;

        /// <summary>물대포 한 방울이 불 몹을 미는 힘(예전 2.5). 큰 불·기름 방울은 무거워서 절반.</summary>
        public const float HoseKnock = 4f;

        /// <summary>건물 불에 물대포 물을 이만큼(초 분량) 맞히면 증기 폭발. 저항은 이렇게 뚫는다. 끄는 시간의 손잡이 2: 3이면 식는 것까지 약 6초마다 한 번(예전 2는 3초).</summary>
        public const float SteamHold = 3f;

        /// <summary>맞은 물이 식는 속도(초당). 코앞 불씨를 잡고 돌아와도 쌓인 게 남는다.</summary>
        public const float SteamCool = 0.25f;

        /// <summary>증기 폭발이 한 번에 줄이는 불 세기(저항 무시).</summary>
        public const float SteamDouse = 0.35f;

        /// <summary>증기 폭발이 건물 가장자리에서 미치는 범위·피해·밀치기.</summary>
        public const float SteamRadius = 4f;
        public const float SteamHit = 6f;
        public const float SteamPush = 6f;

        /// <summary>신고로 붙는 불 세기.</summary>
        public const float ReportFire = 0.35f;

        /// <summary>이 세기 이상 타는 건물은 StageRules.SpreadEvery마다 가장 가까운 건물로 불을 옮긴다.</summary>
        public const float SpreadFire = 0.8f;

        /// <summary>건물 사이 가장자리 거리가 이 안이어야 옮겨붙는다(마을 가게 사이는 7~8칸).</summary>
        public const float SpreadRange = 9f;

        /// <summary>옮겨붙은 불 세기.</summary>
        public const float SpreadIgnite = 0.3f;

        /// <summary>불 세기 1로 이만큼 타면 건물이 무너진다(초). 나무·차는 BurnSmall.</summary>
        public const float BurnBuilding = 32f;

        /// <summary>
        /// 건물이 무너지기까지 BurnBuilding의 이만큼 배(2026-10-07, 모든 스테이지). 노란 장비(스프링클러·헬기·소방차…)와
        /// 옛 무기(물폭탄·드론·대원)의 "맵 곳곳 건물 물"이 빠져 소방관이 달려갈 여유를 준다.
        /// 마을 이동만 봇 지킨 비율: 1배 28%, 2배 40%, 2.5배(+신고 여섯·수호 반경 식힘) 64%, 3배 56%.
        /// </summary>
        public const float BuildingBurnScale = 2.5f;

        /// <summary>수호 반경 안 타는 구조물이 초당 잦아드는 불 세기(곁에 서 있으면 지켜진다).</summary>
        public const float GuardCool = 0.03f;
        public const float BurnSmall = 20f;
        public const float EmberSight = 9f;
        public const float SpreadAt = 0.4f;

        /// <summary>false면 신고(가게 점화)를 하지 않는다. 테스트가 끈다.</summary>
        public bool Reports = true;
        public int HousesLost;
        public int CiviliansLost;

        /// <summary>건물이 절반 넘게 무너져서 졌다.</summary>
        public bool LostTown;

        /// <summary>이긴 판의 별(1~3). 지면 0.</summary>
        public int Stars;

        /// <summary>이 시각마다 안 탄 가게 하나에 불이 난다(신고). 같은 시각이 둘이면 동시에 두 곳.</summary>
        public static readonly float[] ReportTimes = { 10f, 30f, 50f, 70f, 90f, 110f, 120f, 120f, 140f, 160f, 180f, 180f, 200f, 215f, 230f };
        public const float GasFuse = 2.5f;
        public const float GasRadius = 3.5f;

        /// <summary>공단 기름 방울은 이 시각부터 가장자리에서 나온다(첫 구간은 마을처럼 쉽게).</summary>
        public const float OilFrom = 40f;
        public const float OilLife = 8f;
        public const float OilRadius = 0.8f;

        /// <summary>기름 불이 탈 것에 붙이는 불 세기.</summary>
        public const float OilIgnite = 0.3f;

        /// <summary>기름 방울이 죽을 때 튀는 기름 불 수.</summary>
        public const int OilDeathSpill = 3;

        /// <summary>이번 틱에 기름 불이 옮겨붙은 탈 것.</summary>
        public readonly List<Structure> OilCaught = new List<Structure>();
        public const float RescueRange = 1.3f;
        /// <summary>문 앞에서 한 명을 데리고 나오는 시간. 이 동안 열기를 몸으로 받는다(예전 1.2).</summary>
        public const float RescueTime = 2f;

        /// <summary>큰 불(이 세기 이상) 속에 갇힌 사람은 SmokeTime마다 한 명씩 잃는다.</summary>
        public const float SmokeFire = 0.6f;
        public const float SmokeTime = 13f;

        /// <summary>이번 틱에 사람을 잃은 건물(연기·무너짐).</summary>
        public readonly List<Structure> PeopleLost = new List<Structure>();

        /// <summary>이번 틱에 터진 가스통 자리.</summary>
        public readonly List<Vec2> GasBlasts = new List<Vec2>();

        /// <summary>이번 틱에 누군가를 구해 낸 건물.</summary>
        public readonly List<Structure> RescuedFrom = new List<Structure>();

        /// <summary>이번 틱에 옆 건물에서 불이 옮겨붙은 건물. SpreadFrom[i]가 옮긴 건물이다.</summary>
        public readonly List<Structure> Spread = new List<Structure>();
        public readonly List<Structure> SpreadFrom = new List<Structure>();

        /// <summary>이번 틱에 한 번에 KnockShown 넘게 줄어든 건물 불.</summary>
        public readonly List<FireKnock> Knocked = new List<FireKnock>();

        /// <summary>이만큼 넘게 한 번에 줄어야 Knocked에 든다(꾸준한 물줄기의 한 틱은 훨씬 작다).</summary>
        public const float KnockShown = 0.15f;

        /// <summary>이번 틱에 증기 폭발이 난 건물.</summary>
        public readonly List<Structure> SteamBursts = new List<Structure>();

        /// <summary>갇힌 사람이 있는 건물이 이만큼 안에 무너지면 경고(초).</summary>
        public const float CollapseWarnAt = 12f;

        /// <summary>무너지기 이만큼 전의 구조는 아슬아슬 구조: 경험치 보너스.</summary>
        public const float CloseCallAt = 6f;
        public const int CloseCallXp = 40;

        /// <summary>대화재 랜드마크의 갇힌 사람을 다 구하면 받는 경험치.</summary>
        public const int LandmarkXp = 80;

        /// <summary>이번 틱에 "곧 무너진다" 경고가 시작된 건물(건물마다 한 번, 꺼지면 다시).</summary>
        public readonly List<Structure> CollapseWarnings = new List<Structure>();

        /// <summary>이번 틱에 무너지기 직전에 사람을 구한 건물.</summary>
        public readonly List<Structure> CloseCalls = new List<Structure>();

        /// <summary>이번 틱에 대화재 랜드마크의 갇힌 사람을 다 구했다.</summary>
        public bool JustLandmarkSaved;

        /// <summary>이번 틱에 새로 불붙은 구조물.</summary>
        public readonly List<Structure> Ignited = new List<Structure>();

        /// <summary>이번 틱에 무너진 구조물.</summary>
        public readonly List<Structure> Fell = new List<Structure>();

        /// <summary>이번 틱에 물로 완전히 꺼진 구조물.</summary>
        public readonly List<Structure> Doused = new List<Structure>();

        /// <summary>항구: 이번 틱에 바다에 나타난 불배(없으면 null).</summary>
        public Structure JustBoat;

        /// <summary>항구: 이번 틱에 부두에 닿은 배 / 물 위에서 꺼져 바다로 돌아가기 시작한 배.</summary>
        public readonly List<Structure> BoatsDocked = new List<Structure>();
        public readonly List<Structure> BoatsAway = new List<Structure>();

        /// <summary>첫 불배가 뜨는 시각(그 뒤는 Stage.BoatEvery마다).</summary>
        public const float FirstBoat = 20f;

        /// <summary>2.2면 숙련 봇이 배를 다 요격해 마을보다 쉬웠다(21승, 잃은 건물 1.8): 요격 시간을 줄여 부두에 더 자주 닿게 한다(docs §17).</summary>
        public const float BoatSpeed = 3f;
        public const float BoatFire = 0.5f;

        /// <summary>닿은 배가 불을 옮기는 틈(배 가장자리에서 탈 것 가장자리까지)과 간격(초).</summary>
        public const float BoatDockGap = 4f;

        /// <summary>4초면 숙련 봇이 마을과 같은 수준(23 대 22승)이었다: 닿은 배가 더 자주 옮긴다(docs §17).</summary>
        public const float BoatSpreadEvery = 2.5f;

        /// <summary>배가 다 타서 가라앉기까지(초, 불 세기 1 기준). 떠내려오는 동안 가라앉지 않게 건물보다 길다.</summary>
        public const float BurnBoat = 45f;
        private float _nextBoat = FirstBoat;

        /// <summary>바다 위에서 끈 불배의 경험치(요격 보상). "부두 끝에서 쏘라"가 할 일이 되게.</summary>
        public const int BoatXp = 15;

        /// <summary>배가 부두에 닿기까지 초: 줄을 따라 목표 x까지 + 남쪽으로 부두선까지, 속도로 나눈다. 닿았으면 0, 목표가 없으면 줄 끝까지.</summary>
        public float BoatEta(Structure b)
        {
            if (b.Docked) return 0f;
            float speed = b.Tanker ? TankerSpeed : BoatSpeed;
            float south = Math.Max(0f, b.Pos.Y - b.Half.Y - SurvivorHarbor.SeaFrom);
            float along = b.Drift.Y != 0f || b.Target == null ? 0f : Math.Abs(b.Target.Pos.X - b.Pos.X);
            return (along + south) / speed;
        }

        // ------------------------------------------------------------------
        // 대화재 종류(맵 특색 패스): 공통 감독 위에 스테이지마다 하나씩.
        // ------------------------------------------------------------------

        /// <summary>숲 불 전선: 북쪽 숲(FrontStart)에서 캠프 줄(FrontEnd)까지 FrontSpeed로 내려오며 FrontRow칸마다 바닥 불 한 줄(FrontGap 간격)을 깔고 지나는 탈 것에 불을 붙인다.</summary>
        public const float FrontStart = 36f;
        public const float FrontEnd = 12f;
        public const float FrontSpeed = 0.22f;
        public const float FrontRow = 1.0f;
        public const float FrontGap = 2.5f;
        public const float FrontIgnite = 0.3f;
        public const float FrontPuddleLife = 6f;
        /// <summary>마지막 줄을 다 꺼 놓으면 전선이 이만큼 느려진다.</summary>
        public const float FrontHoldMax = 0.6f;
        public float? FrontY;
        public readonly List<Puddle> FrontRowPuddles = new List<Puddle>();
        public bool JustFront;
        private float _lastRowY;

        /// <summary>공단 연쇄 폭발: ChainEvery(−ChainStep×압력, 최소 ChainMin)초마다 드럼 하나에 점화, 퓨즈 ChainFuse초 뒤 터진다. 끄면 막는다.</summary>
        // 측정 ①(2026-10-04): 12초·시작 둘·퓨즈 6이면 숙련 봇 5승(동네 패 17) — 드럼이 공장 곁이라 터질 때마다 공장이 붙는다 → 18초·시작 하나·퓨즈 8.
        public const float ChainEvery = 18f;
        public const float ChainStep = 1.5f;
        public const float ChainMin = 8f;
        public const float ChainFuse = 8f;
        public Structure JustChain;
        private float _chainClock;

        /// <summary>항구 유조선 좌초: 큰 배가 TankerSpeed로 와 부두 가운데 닿고, 타는 동안 TankerLeakEvery초마다 부두 위에 불기름(반지름 TankerOilRadius)을 좌우로 번갈아 흘린다. 선원 FinalePeople.</summary>
        public const float TankerSpeed = 1.8f;
        public const float TankerLeakEvery = 3f;
        public const float TankerLeakStep = 1.6f;
        public const float TankerOilRadius = 1.6f;
        public const float BurnTanker = 150f;
        public Structure TankerBoat;
        public bool JustTanker;
        private float _leakClock;
        private int _leakCount;

        /// <summary>야시장 불꽃 폭주: 가판대가 전부 쏘고, StormEvery(−StormStep×압력, 최소 2)초마다 꺼진 가판대 하나가 다시 붙고 무대에서 가장 가까운 등줄이 탄다. 무대는 StageRocketEvery초마다 로켓.</summary>
        // 측정 ①(2026-10-04): 가판대 셋이 동시에 쏘고(초당 3발) 6초마다 등줄이면 숙련 봇 3승(동네 패 26) → 시작은 무대 가까운 하나, 8초, 무대 로켓 3초.
        public const float StormEvery = 8f;
        public const float StormStep = 1f;
        public const float StageRocketEvery = 8f;
        public bool JustStorm;
        private float _stormClock;
        private float _stageRocketClock;

        /// <summary>불 전선이 지금 내려오는 속도: 기본 × (1 + 0.25×압력) × (1 − FrontHoldMax × 마지막 줄의 꺼진 비율).</summary>
        public float FrontSpeedNow
        {
            get { return FrontSpeed * (1f + (0.25f * FinalePressure)) * (1f - (FrontHoldMax * FrontHold)); }
        }

        /// <summary>마지막 줄 바닥 불 중 꺼진(Out) 비율.</summary>
        public float FrontHold
        {
            get
            {
                if (FrontRowPuddles.Count == 0) return 0f;
                int outCount = 0;
                foreach (Puddle p in FrontRowPuddles) if (p.Out) outCount++;
                return outCount / (float)FrontRowPuddles.Count;
            }
        }

        /// <summary>야시장: 등줄(생성 때 Stage.Links로 깐다).</summary>
        public readonly List<Lantern> Lanterns = new List<Lantern>();

        /// <summary>이번 틱에 불이 줄을 다 건너 반대쪽에 붙은 줄 / 물에 꺼진(또는 젖은) 줄.</summary>
        public readonly List<Lantern> LanternCaught = new List<Lantern>();
        public readonly List<Lantern> LanternDoused = new List<Lantern>();

        /// <summary>등줄: 출발 점포 불 세기 문턱, 붙기까지, 건너는 시간, 쉬는 시간, 젖어 있는 시간, 물줄기가 줄을 적시는 거리.</summary>
        public const float LanternCatch = 0.6f;
        public const float LanternDelay = 2f;
        public const float LanternRun = 4f;
        public const float LanternCool = 12f;
        public const float LanternWet = 6f;
        public const float LanternReach = 0.7f;
        public const float LanternIgnite = 0.3f;

        /// <summary>야시장: 날고 있는 로켓, 이번 틱에 떨어진 자리, 이번 틱에 쏘기 시작한 가판대.</summary>
        public readonly List<Rocket> Rockets = new List<Rocket>();
        public readonly List<Vec2> RocketBursts = new List<Vec2>();
        public Structure JustLaunching;
        public const float RocketEvery = 1f;
        public const float RocketFlight = 1.1f;
        public const float RocketRange = 14f;
        public const float RocketBurn = 8f;
        public const float RocketIgnite = 0.3f;
        public const float RocketHit = 0.8f;

        /// <summary>폭죽: 한 주기(1초) 중 움직이는 비율, 잡혔을 때 튀는 불씨 수.</summary>
        public const float PopperHop = 0.35f;
        public const int PopperEmbers = 2;

        public bool JustLeveled;
        public bool JustEvolved;

        /// <summary>방금 고른 카드로 최대 레벨(Lv5)이 된 무기·보조. 다음 Step까지 남는다(진화 신호와 같다).</summary>
        public UpgradeId? JustMaxed;
        public bool JustRescued;
        public bool JustWave;

        /// <summary>산불 숲: 바람 방향(단위 벡터). 바람이 없는 스테이지는 (0,0).</summary>
        public Vec2 Wind;

        /// <summary>이 간격마다 바람 방향이 바뀐다(초).</summary>
        public const float WindShiftEvery = 60f;

        /// <summary>타는 나무가 바람 쪽 이웃에 불을 옮기는 간격·거리.</summary>
        public const float WindSpreadEvery = 7f;
        public const float WindSpreadRange = 4f;
        public const float WindSpreadChance = 0.3f;

        /// <summary>이번 틱에 바람이 바뀌었다.</summary>
        public bool JustWindShift;

        /// <summary>이번 틱에 재 박쥐 무리가 왔다.</summary>
        public bool JustBats;
        public int GemsCollected;
        public int ShotsFired;
        public float PlayerHurt;

        private Rng _rng;
        private float _spawnDebt;
        private float _hoseClock;
        private float _jetClock;
        private int _jetQueue;
        private float _jetAngle;
        private int _reportsDone;
        private int _bigDone;
        private bool _bigFailed;
        private float _finaleClock;
        private float _burstClock;
        private float _ringClock;
        private int _bonusPicks;
        private int _wavesDone;
        private float _nextWind;
        private int _windIndex;
        private float _nextBats = FirstBats;

        /// <summary>첫 재 박쥐 무리가 오는 시각.</summary>
        public const float FirstBats = 30f;

        private readonly int[] _head = new int[Cells * Cells];
        private int[] _next = new int[MaxEnemies * 2];

        /// <summary>시작 장비를 주지 않으면 물대포 하나(신입 소방관).</summary>
        public static readonly UpgradeId[] DefaultStart = { UpgradeId.Hose };

        /// <param name="start">소방서에서 고른 소방관의 시작 장비(Lv1씩). null이면 DefaultStart.</param>
        public SurvivorSim(int seed, int stage = 1, IReadOnlyList<UpgradeId> start = null)
        {
            Stage = SurvivorStages.Get(stage);
            Guardian = Stage.Guardian;
            Structures = Stage.Map();
            HasWater = Structures.Exists(s => s.Kind == StructureKind.Water);
            PlaceManholes();
            if (Stage.Links != null)
            {
                foreach (int[] ab in Stage.Links(Structures)) Lanterns.Add(new Lantern { A = Structures[ab[0]], B = Structures[ab[1]] });
            }
            _rng = new Rng(seed == 0 ? 1 : seed);
            _kitRng = new Rng(((seed == 0 ? 1 : seed) * 7919) + 13);
            _kitClock = NextKitWait();
            // 숲 개편(2026-10-08): 칸 없음 · 모든 아이템 Lv6 · 구조대원. 다른 스테이지는 그대로.
            Build.Free = Stage.Number == 2;
            foreach (UpgradeId id in start ?? DefaultStart) Build.Add(id);
            // 방화복으로 시작하면 최대 체력이 다르다.
            Hp = MaxHp;
        }

        public int XpToNext
        {
            get { return 6 + (Level * 5) + (Level * Level / 4); }
        }

        public float MaxHp
        {
            get { return BaseMaxHp + Build.MaxHpBonus; }
        }

        /// <summary>풀장비로 시작한다: 모든 아이템 최대 + 체력 가득.</summary>
        public void GiveMaxGear()
        {
            Build.MaxAll();
            Hp = MaxHp;
        }

        /// <summary>가게 + 창고 수.</summary>
        public int HousesTotal
        {
            get
            {
                int n = 0;
                foreach (Structure s in Structures) if (s.IsBuilding) n++;
                return n;
            }
        }

        public float Magnet
        {
            get { return BaseMagnet; }
        }

        /// <summary>폰 조준 보정이 노릴 불: 살아 있는 적(보스 포함)과 타는 구조물 자리.</summary>
        public void AimTargets(List<Vec2> into)
        {
            into.Clear();
            foreach (Enemy e in Enemies)
            {
                if (!e.Dead) into.Add(e.Pos);
            }
            foreach (Structure s in Structures)
            {
                if (s.Burning) into.Add(s.Pos);
            }
        }

        // ------------------------------------------------------------------
        // 진행
        // ------------------------------------------------------------------

        public void Step(float moveX, float moveY)
        {
            ClearSignals();
            if (PendingChoices != null || Outcome != SOutcome.Playing) return;

            Time += Dt;
            MovePlayer(moveX, moveY);
            BlockPlayer();
            Direct();
            if (Stage.BoatEvery > 0f) TickBoats();
            RebuildHash();
            MoveEnemies();
            FireWeapons();
            MoveShots();
            TickPuddles();
            TickStructures();
            if (Lanterns.Count > 0) TickLanterns();
            if (Rockets.Count > 0) TickRockets();
            FlushPops();
            TouchPlayer();
            TickHeat();
            CollectGems();
            TickToolboxes();
            TickKits();
            TickRescue();
            if (Guardian) TickHaven();
            TickChests();
            if (Combo > 0)
            {
                ComboClock -= Dt;
                if (ComboClock <= 0f)
                {
                    ComboEnded = Combo;
                    Combo = 0;
                }
            }
            // Sweep 전에 잰다: 죽은 적을 치우면 해시 번호가 어긋난다.
            Measure();
            Sweep();

            if (Hp <= 0f)
            {
                Hp = 0f;
                Outcome = SOutcome.Lost;
                return;
            }
            int houses = HousesTotal;
            // 수호자: 동네를 잃어도 끝나지 않는다. 쓰러질 때까지 지킨다(대신 무너진 집은 잿더미 둥지가 된다).
            if (!Guardian && houses > 0 && HousesLost * 2 > houses)
            {
                LostTown = true;
                Outcome = SOutcome.Lost;
                return;
            }
            if (Time >= RunTime)
            {
                Outcome = SOutcome.Won;
                // 별: 이기면 1, 건물 75% 이상 지키면 +1, 한 명도 안 잃으면 +1.
                float saved = houses > 0 ? (houses - HousesLost) / (float)houses : 1f;
                Stars = 1 + (saved >= 0.75f ? 1 : 0) + (CiviliansLost == 0 ? 1 : 0);
                return;
            }

            if (PendingChoices == null && Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                Stats.LevelTimes.Add(Time);
                // 고를 게 없으면 카드 화면을 열지 않는다(레벨업 밀치기·연출은 그대로).
                List<UpgradeId> cards = SurvivorUpgrades.Roll(Build, Level, ref _rng);
                PendingChoices = cards.Count > 0 ? cards : null;
                ChoosingChest = false;
                JustLeveled = true;
                PushAway(Player, 4f, 3f);
            }
        }

        public void Choose(int index)
        {
            if (PendingChoices == null || index < 0 || index >= PendingChoices.Count) return;
            UpgradeId id = PendingChoices[index];
            PendingChoices = null;
            Take(id);
            // 보물상자: 남은 고르기가 있으면 바로 다음 카드.
            if (_bonusPicks > 0) OpenBonusPick();
        }

        /// <summary>보물상자 카드 한 번(남은 _bonusPicks만큼 이어서 연다).</summary>
        private void OpenBonusPick()
        {
            // 뽑을 게 없으면 남은 상자 카드를 버린다.
            while (_bonusPicks > 0)
            {
                _bonusPicks--;
                List<UpgradeId> cards = SurvivorUpgrades.Roll(Build, Level, ref _rng);
                if (cards.Count == 0) continue;
                ChoosingChest = true;
                PendingChoices = cards;
                return;
            }
        }

        private void Take(UpgradeId id)
        {

            if (id == UpgradeId.Heal)
            {
                Hp = Math.Min(MaxHp, Hp + SurvivorUpgrades.HealAmount);
                return;
            }

            float before = MaxHp;
            int had = Build.Level(id);
            Build.Add(id);
            Hp += MaxHp - before;
            if (!Loadout.IsEvolution(id) && had < Loadout.MaxLevel && Build.Level(id) == Loadout.MaxLevel) JustMaxed = id;
            if (Loadout.IsEvolution(id))
            {
                JustEvolved = true;
                Stats.Evolutions++;
                if (Stats.FirstEvolveAt < 0f) Stats.FirstEvolveAt = Time;
            }
            if (id == UpgradeId.Cannon) _jetClock = 0f;
            PrimeWeapon(id);
            if (Build.Free) SampleLevelUp(id, SampleLevel(Loadout.BaseOf(id)));
        }

        /// <summary>작은 불 몹(불씨·다트·다람쥐·박쥐)이 닿아 있을 때 초당 피해. 큰 불·기름 방울은 10.</summary>
        public const float SmallTouch = 5f;

        /// <summary>테스트용: 적을 직접 놓는다.</summary>
        public Enemy Spawn(EnemyKind kind, Vec2 at)
        {
            var e = new Enemy { Kind = kind, Pos = at };
            float scale = Stage.EnemyHp * (1f + ((Time / 120f) * (Time / 120f)));
            switch (kind)
            {
                // 작은 불 몹의 접촉은 초당 SmallTouch(예전 3): 건물을 오래 끄느라 서 있으면 뒤에서 닿는다.
                case EnemyKind.Ember: e.MaxHp = 2f * scale; e.Speed = 2.4f; e.Radius = 0.35f; e.Touch = SmallTouch; e.Xp = 1; break;
                case EnemyKind.Blaze: e.MaxHp = 14f * scale; e.Speed = 1.5f; e.Radius = 0.6f; e.Touch = 10f; e.Xp = 5; break;
                case EnemyKind.Dart: e.MaxHp = 2f * scale; e.Speed = 4.2f; e.Radius = 0.3f; e.Touch = SmallTouch; e.Xp = 1; break;
                case EnemyKind.Squirrel: e.MaxHp = 3f * scale; e.Speed = 3.6f; e.Radius = 0.3f; e.Touch = SmallTouch; e.Xp = 1; e.Seeker = true; break;
                case EnemyKind.Bat: e.MaxHp = 1.5f * scale; e.Speed = 3.2f; e.Radius = 0.3f; e.Touch = SmallTouch; e.Xp = 1; break;
                case EnemyKind.Oil: e.MaxHp = 6f * scale; e.Speed = 1.3f; e.Radius = 0.55f; e.Touch = 10f; e.Xp = 3; break;
                // 갈매기는 폭격기: 건물(Seeker)을 노려 날아가 지붕에 불을 떨어뜨리고 바다로 돌아간다.
                case EnemyKind.Gull: e.MaxHp = 2.5f * scale; e.Speed = 3.4f; e.Radius = 0.3f; e.Touch = SmallTouch; e.Xp = 1; e.Seeker = true; break;
                // 폭죽의 Speed는 뛰는 순간 속도(한 주기 1초 중 0.35초만 움직인다 → 평균 2.6).
                case EnemyKind.Popper: e.MaxHp = 2f * scale; e.Speed = 7.5f; e.Radius = 0.3f; e.Touch = SmallTouch; e.Xp = 1; break;
                // 불 게: 단단하고 느리고(옆걸음) 잘 안 밀린다. 건물에 불을 지핀 뒤 소방관을 쫓는다.
                case EnemyKind.Crab: e.MaxHp = 9f * scale; e.Speed = 1.1f; e.Radius = 0.5f; e.Touch = 10f; e.Xp = 4; e.Seeker = true; break;
                // 풍등: 하늘을 떠서 점포 지붕에 내려앉는다. 닿아도 소방관을 안 태운다(Touch 0) — 떨어뜨려야 할 표적.
                case EnemyKind.SkyLantern: e.MaxHp = 1.5f * scale; e.Speed = 0.9f; e.Radius = 0.35f; e.Touch = 0f; e.Xp = 2; e.Seeker = true; break;
                // 마을 몹(수호자): 나보다 집을 노린다. 움직임은 MoveRaider.
                case EnemyKind.Rat: e.MaxHp = 1.5f * scale; e.Speed = 3.6f; e.Radius = 0.28f; e.Touch = SmallTouch; e.Xp = 1; break;
                case EnemyKind.Goblin: e.MaxHp = 8f * scale; e.Speed = 1.6f; e.Radius = 0.45f; e.Touch = 8f; e.Xp = 4; break;
                case EnemyKind.FireBalloon: e.MaxHp = 4f * scale; e.Speed = 1f; e.Radius = 0.5f; e.Touch = 0f; e.Xp = 3; break;
                case EnemyKind.Bear: e.MaxHp = 60f * scale; e.Speed = 2f; e.Radius = 0.9f; e.Touch = 15f; e.Xp = 20; e.Heavy = true; break;
                case EnemyKind.Hwama: e.MaxHp = BossHp; e.Speed = 0.9f; e.Radius = 1.8f; e.Touch = 25f; e.Xp = 100; e.Heavy = true; break;
            }
            if (IsRaider(kind)) RaidersSpawned++;
            e.Hp = e.MaxHp;
            Enemies.Add(e);
            return e;
        }

        /// <summary>테스트용: 구슬을 직접 놓는다.</summary>
        public void DropGem(Vec2 at, int value)
        {
            if (Gems.Count >= MaxGems)
            {
                Gems[_rng.Next(Gems.Count)].Value += value;
                return;
            }
            Gems.Add(new Gem { Pos = at, Value = value });
        }

        // ------------------------------------------------------------------
        // 내부
        // ------------------------------------------------------------------

        private void ClearSignals()
        {
            ClearWeaponSignals();
            ClearMobSignals();
            ClearFreeSignals();
            RuinSpat.Clear();
            Hits.Clear();
            Footprints.Clear();
            SuitBounces.Clear();
            Extinguished.Clear();
            Reignited.Clear();
            Ignited.Clear();
            Spread.Clear();
            SpreadFrom.Clear();
            OilCaught.Clear();
            Knocked.Clear();
            SteamBursts.Clear();
            CollapseWarnings.Clear();
            CloseCalls.Clear();
            JustLandmarkSaved = false;
            Fell.Clear();
            Doused.Clear();
            GasBlasts.Clear();
            RescuedFrom.Clear();
            PeopleLost.Clear();
            JustLeveled = false;
            JustEvolved = false;
            JustMaxed = null;
            Repaired.Clear();
            JustPickedToolbox = false;
            JustPickedKit = false;
            JustBigReport = false;
            JustFinale = false;
            JustBurst = false;
            JustChest = false;
            JustRescued = false;
            JustWave = false;
            JustComboTier = false;
            ComboEnded = 0;
            JustWindShift = false;
            JustBats = false;
            JustPressureUp = false;
            JustBoat = null;
            BoatsDocked.Clear();
            BoatsAway.Clear();
            LanternCaught.Clear();
            GullDrops.Clear();
            LanternLands.Clear();
            JustFront = false;
            JustChain = null;
            JustTanker = false;
            JustStorm = false;
            LanternDoused.Clear();
            RocketBursts.Clear();
            JustLaunching = null;
            GemsCollected = 0;
            // 틱마다 한 번 센다(테스트가 판 도중 구조물을 넣는다).
            HasWater = Structures.Exists(s => s.Kind == StructureKind.Water);
            ShotsFired = 0;
            PlayerHurt = 0f;
            HeatHurt = 0f;
        }

        private float Rand()
        {
            return _rng.Next(10000) / 10000f;
        }

        private void MovePlayer(float mx, float my)
        {
            float len = (float)Math.Sqrt((mx * mx) + (my * my));
            if (len > 1f)
            {
                mx /= len;
                my /= len;
            }
            if (len > 0.01f) Facing = new Vec2(mx / Math.Max(len, 1f), my / Math.Max(len, 1f));
            // 숲도 장화는 수치형(+10%/Lv, Build.SpeedScale).
            float speed = BaseSpeed * Build.SpeedScale;
            Player.X = Clamp(Player.X + (mx * speed * Dt), 0.5f, ArenaSize - 0.5f);
            Player.Y = Clamp(Player.Y + (my * speed * Dt), 0.5f, ArenaSize - 0.5f);
            // 장화: 걷는 동안 젖은 발자국을 남긴다(연출용, 한 칸마다).
            if (Build.WetBoots && len > 0.01f)
            {
                _stepDist += speed * Dt;
                if (_stepDist >= FootprintEvery)
                {
                    _stepDist = 0f;
                    Footprints.Add(Player);
                }
            }
        }

        /// <summary>장화 발자국 간격(칸).</summary>
        public const float FootprintEvery = 1.2f;
        private float _stepDist;

        /// <summary>스폰 감독: 시간이 갈수록 많이, 1·2·3분엔 포위, 4분엔 보스.</summary>
        private void Direct()
        {
            // 불은 이제 주로 건물에서 나온다. 가장자리에서 몰려오는 불은 예전(3→35)보다 훨씬 적다.
            // 첫 1분이 한산하지 않게 시작은 EarlySpawn배, 끝(4:00)은 그대로 8배다.
            float rate = Stage.SpawnRate * (EarlySpawn + ((8f - EarlySpawn) * (float)Math.Pow(Math.Min(Time / RunTime, 1f), 1.5)));
            _spawnDebt += rate * Dt;
            while (_spawnDebt >= 1f)
            {
                _spawnDebt -= 1f;
                if (Enemies.Count >= MaxEnemies) continue;
                EnemyKind kind = PickKind();
                // 불 갈매기·불 게는 바다 쪽에서 온다(물 위를 건너니 부두에서 쏴야 한다).
                Spawn(kind, kind == EnemyKind.Gull || kind == EnemyKind.Crab ? SeaPoint() : SpawnPoint(SpawnDistance));
            }

            if (_wavesDone < 3 && Time >= 60f * (_wavesDone + 1))
            {
                _wavesDone++;
                JustWave = true;
                int n = 10 + (_wavesDone * 5);
                for (int i = 0; i < n && Enemies.Count < MaxEnemies; i++)
                {
                    double a = (Math.PI * 2 * i) / n;
                    var at = new Vec2(Player.X + (float)(Math.Cos(a) * 11.5), Player.Y + (float)(Math.Sin(a) * 11.5));
                    Spawn(_wavesDone == 3 ? EnemyKind.Blaze : EnemyKind.Ember, ClampToArena(at));
                }
            }

            if (Stage.BatFlockEvery > 0f && Time >= _nextBats)
            {
                // 재 박쥐 무리: 가장자리 한 곳에서 여섯 마리가 뭉쳐 날아온다.
                _nextBats = Time + Stage.BatFlockEvery;
                JustBats = true;
                Vec2 from = SpawnPoint(SpawnDistance);
                for (int i = 0; i < 6 && Enemies.Count < MaxEnemies; i++)
                {
                    Enemy bat = Spawn(EnemyKind.Bat, ClampToArena(new Vec2(from.X + ((Rand() - 0.5f) * 2.5f), from.Y + ((Rand() - 0.5f) * 2.5f))));
                    bat.Phase = i * 1.1f;
                }
            }

            if (Stage.Wind && Time >= _nextWind)
            {
                // 바람: 판 시작에 한 번 정하고 60초마다 여덟 방향 중 하나로 바뀐다.
                bool first = _nextWind <= 0f;
                _nextWind = (first ? 0f : Time) + WindShiftEvery;
                // 바뀔 때는 늘 다른 방향으로(지금 것 빼고). 스테이지가 방향을 정해 두면(WindArc) 그 안에서만.
                int[] arc = Stage.WindArc;
                if (arc != null && arc.Length > 0)
                {
                    int at = Math.Max(0, Array.IndexOf(arc, _windIndex));
                    int k = first ? _rng.Next(arc.Length) : arc.Length == 1 ? 0 : (at + 1 + _rng.Next(arc.Length - 1)) % arc.Length;
                    _windIndex = arc[k];
                }
                else
                {
                    _windIndex = first ? _rng.Next(8) : (_windIndex + 1 + _rng.Next(7)) % 8;
                }
                double a = _windIndex * Math.PI / 4;
                Wind = new Vec2((float)Math.Cos(a), (float)Math.Sin(a));
                if (!first) JustWindShift = true;
            }

            while (Reports && _reportsDone < Stage.ReportTimes.Length && Time >= Stage.ReportTimes[_reportsDone])
            {
                _reportsDone++;
                Stats.Events++;
                Report();
            }

            if (Guardian && Reports) DirectTownMobs();

            float[] bigTimes = BigTimes;
            while (Reports && _bigDone < bigTimes.Length && Time >= bigTimes[_bigDone])
            {
                _bigDone++;
                // 수호자 마을: 큰 신고 대신 습격(가장자리에서 몰려오는 무리, 강제 없음).
                if (Guardian) StartRaid();
                else StartBigReport();
            }

            if (Reports && !Finale && Time >= FinaleAt) StartFinale();
            if (Finale)
            {
                // 대화재: 신고 틱마다 감독이 압력 단계를 정하고, 그 단계만큼 신고가 들어온다. 신고 난 가게엔 한 명이 더 갇힌다.
                _finaleClock -= Dt;
                if (_finaleClock <= 0f)
                {
                    // 감독: 여유가 있으면 한 단계 올리고, 위험하면 한 단계 내린다. 그 단계로 다음 간격을 정한다.
                    float hp = Hp / MaxHp;
                    int before = FinalePressure;
                    // 수호자 마을은 동네를 잃어도 안 지므로 "건물 여유" 대신 지킨 비율로 본다.
                    bool roomy = Guardian ? VillageSaved >= GuardRoomy : HousesRoom >= 2;
                    bool tight = Guardian ? VillageSaved < GuardTight : HousesRoom <= 1;
                    if (hp >= PressureHp && roomy) FinalePressure = Math.Min(PressureMax, FinalePressure + 1);
                    else if (hp < PressureLowHp || tight) FinalePressure = Math.Max(0, FinalePressure - 1);
                    if (FinalePressure > before)
                    {
                        JustPressureUp = true;
                        Stats.PressurePeak = Math.Max(Stats.PressurePeak, FinalePressure);
                    }
                    _finaleClock = FinaleReportGap;
                    int reports = FinalePressure >= 2 ? 2 : 1;
                    for (int r = 0; r < reports; r++)
                    {
                        Structure hit = Report(FinaleReportFire);
                        if (hit != null) hit.Residents++;
                        Stats.Events++;
                    }
                }
                // 2단계부터: 큰 불 고리가 소방관을 에워싼다. 서서 끄지 못하게 하는 몸 압박(3:00의 장비는 불만으론 못 누른다).
                _ringClock -= Dt;
                // 버티기 중엔 쉰다: 링 파도가 이미 몸 압박이다.
                // 화마가 살아 있는 동안 큰 불 고리는 쉰다: 보스가 그 압력을 맡는다.
                if (FinalePressure >= FinaleRingFrom && _ringClock <= 0f && (Boss == null || Boss.Dead))
                {
                    _ringClock = FinaleRingEvery;
                    int ring = FinaleRingCount;
                    for (int k = 0; k < ring && Enemies.Count < MaxEnemies; k++)
                    {
                        double a = Math.PI * 2 * k / ring;
                        Vec2 at = ClampToArena(new Vec2(Player.X + (float)(Math.Cos(a) * FinaleRingRadius), Player.Y + (float)(Math.Sin(a) * FinaleRingRadius)));
                        if (HasWater) PushOutOfWater(ref at, 0.6f);
                        Enemy heavy = Spawn(EnemyKind.Blaze, at);
                        heavy.Heavy = true;
                        heavy.MaxHp *= FinaleRingToughness;
                        heavy.Hp = heavy.MaxHp;
                        heavy.Speed *= HeavySpeed;
                    }
                    JustWave = true;
                }
                // 불타는 랜드마크가 사방으로 불씨를 뿜는다(예전 보스가 하던 절정의 몸 압박). 단계가 오르면 더 많이.
                _burstClock -= Dt;
                if (_burstClock <= 0f && Landmark != null && Landmark.Burning)
                {
                    _burstClock = FinaleBurstEvery;
                    int burst = FinaleBurstCount;
                    for (int k = 0; k < burst && Enemies.Count < MaxEnemies; k++)
                    {
                        double a = Math.PI * 2 * k / burst;
                        Vec2 at = EdgePoint(Landmark, 0.5f);
                        Enemy e = Spawn(EnemyKind.Ember, at);
                        e.Knock = new Vec2((float)Math.Cos(a) * 6f, (float)Math.Sin(a) * 6f);
                    }
                    JustBurst = true;
                }
                TickFinaleKind();
            }
        }

        /// <summary>대형 신고: 안 탄 가게 하나에 큰 불, 셋이 더 갇힌다. 모두 구하면 보물상자.</summary>
        private void StartBigReport()
        {
            Structure pick = PickUnburntHouse();
            if (pick == null) return;
            pick.Wet = 0f;
            Ignite(pick, BigReportFire);
            pick.Residents += BigReportPeople;
            BigReport = pick;
            _bigFailed = false;
            JustBigReport = true;
            Stats.Events++;
        }

        /// <summary>대화재: 랜드마크(물류창고·제재소)가 크게 타고 다섯이 갇힌다. 없으면 안 탄 가게 하나.</summary>
        private void StartFinale()
        {
            Finale = true;
            JustFinale = true;
            FinalePressure = 0;
            _finaleClock = FinaleReportEvery;
            Stats.Events++;
            Stats.FinaleLevel = Level;
            Stats.FinaleItems = Build.WeaponCount + Build.PassiveCount;
            // 항구는 창고 대신 유조선이 랜드마크(부두 가운데 닿아 불기름을 흘리고 선원이 갇혀 있다).
            Structure mark = Stage.Finale == FinaleKind.Tanker ? SpawnTanker() : Structures.Find(x => x.Kind == StructureKind.Depot && !x.Collapsed) ?? PickUnburntHouse();
            if (mark == null) return;
            Landmark = mark;
            mark.Wet = 0f;
            Ignite(mark, 1f);
            mark.Residents += FinalePeople;
            if (Guardian) RaiseBoss();
            switch (Stage.Finale)
            {
                case FinaleKind.FireFront:
                    FrontY = FrontStart;
                    _lastRowY = FrontStart + FrontRow;
                    break;
                case FinaleKind.ChainBlast:
                    // 시작과 함께 드럼 하나가 점화된다(둘은 과했다: 측정 ①).
                    ChainIgnite();
                    _chainClock = ChainEvery;
                    break;
                case FinaleKind.RocketStorm:
                    // 무대에서 가장 가까운 가판대 하나부터(셋이 한꺼번에 쏘면 초당 3발: 측정 ①). 나머지는 폭주 틱이 차례로 붙인다.
                    Structure firstStand = null;
                    foreach (Structure s in Structures)
                    {
                        if (s.Kind != StructureKind.Fireworks || s.Collapsed) continue;
                        if (firstStand == null || s.DistanceTo(mark.Pos) < firstStand.DistanceTo(mark.Pos)) firstStand = s;
                    }
                    if (firstStand != null)
                    {
                        firstStand.Wet = 0f;
                        Ignite(firstStand, 0.6f);
                    }
                    _stormClock = StormEvery;
                    _stageRocketClock = StageRocketEvery;
                    break;
            }
        }

        /// <summary>유조선: 바다 왼쪽·오른쪽 끝에서 배 줄을 따라 와 x가 30에 가장 가까운 부둣가 건물 앞에 닿는다.</summary>
        private Structure SpawnTanker()
        {
            Structure quay = null;
            float best = float.MaxValue;
            foreach (Structure s in Structures)
            {
                if (!s.IsBuilding || s.Collapsed || s.Pos.Y <= SurvivorHarbor.QuayRow - 4f) continue;
                float d = Math.Abs(s.Pos.X - (ArenaSize / 2f));
                if (d < best)
                {
                    best = d;
                    quay = s;
                }
            }
            bool fromLeft = Rand() < 0.5f;
            var t = new Structure
            {
                Kind = StructureKind.Boat,
                Tanker = true,
                Name = "유조선",
                Pos = new Vec2(fromLeft ? -3f : ArenaSize + 3f, SurvivorHarbor.BoatLane),
                Half = new Vec2(3f, 1.2f),
                Target = quay,
                Drift = new Vec2(fromLeft ? TankerSpeed : -TankerSpeed, 0f),
            };
            Structures.Add(t);
            TankerBoat = t;
            JustTanker = true;
            _leakClock = TankerLeakEvery;
            _leakCount = 0;
            return t;
        }

        /// <summary>연쇄 폭발: 안 타는 드럼 하나에 점화하고 퓨즈를 ChainFuse로 늘린다(기본 가스 퓨즈 2.5초는 달려갈 틈이 없다).</summary>
        private void ChainIgnite()
        {
            var cold = new List<Structure>();
            foreach (Structure s in Structures)
            {
                if (s.Kind == StructureKind.Gas && !s.Collapsed && !s.Burning) cold.Add(s);
            }
            if (cold.Count == 0) return;
            Structure gas = cold[(int)(Rand() * cold.Count) % cold.Count];
            gas.Wet = 0f;
            if (!Ignite(gas, 0.3f)) return;
            gas.Fuse = ChainFuse;
            JustChain = gas;
        }

        /// <summary>대화재 종류별 사건(공통 감독 뒤에 돈다).</summary>
        private void TickFinaleKind()
        {
            switch (Stage.Finale)
            {
                case FinaleKind.FireFront:
                    TickFront();
                    break;
                case FinaleKind.ChainBlast:
                    _chainClock -= Dt;
                    if (_chainClock <= 0f)
                    {
                        _chainClock = Math.Max(ChainMin, ChainEvery - (ChainStep * FinalePressure));
                        ChainIgnite();
                    }
                    break;
                case FinaleKind.RocketStorm:
                    TickStorm();
                    break;
            }
        }

        /// <summary>불 전선: 내려오다 FrontRow칸마다 바닥 불 한 줄을 깔고 줄 가까이의 탈 것에 불을 붙인다. 마지막 줄을 꺼 놓은 만큼 느려지고, 캠프 줄에서 멈춘다.</summary>
        private void TickFront()
        {
            if (!FrontY.HasValue) return;
            float y = FrontY.Value - (FrontSpeedNow * Dt);
            if (y <= FrontEnd) y = FrontEnd;
            FrontY = y;
            if (_lastRowY - y < FrontRow) return;
            _lastRowY = y;
            FrontRowPuddles.Clear();
            // 한 줄(24개)이 들어갈 자리가 없으면 그 줄은 건너뛴다(큰 불 흔적이 한도를 차지한 때).
            int count = (int)((ArenaSize - 2.5f) / FrontGap) + 1;
            if (BurningGround.Count + count <= MaxBurningGround)
            {
                for (float x = 1.25f; x < ArenaSize; x += FrontGap)
                {
                    var p = new Puddle { Pos = new Vec2(x, y), Radius = 1f, Life = FrontPuddleLife, MaxLife = FrontPuddleLife };
                    BurningGround.Add(p);
                    FrontRowPuddles.Add(p);
                }
            }
            foreach (Structure st in Structures)
            {
                if (!st.Flammable || Math.Abs(st.Pos.Y - y) > st.Half.Y + 1.2f) continue;
                // Spread/SpreadFrom은 짝이라(출발 구조물이 없다) 번짐 신호엔 안 넣고 통계만 센다.
                if (Ignite(st, FrontIgnite)) Stats.Spreads++;
            }
            JustFront = true;
        }

        /// <summary>불꽃 폭주: 꺼진 가판대 하나가 다시 붙고 무대에서 가장 가까운 안 타는 등줄이 탄다(Storm: 출발 점포가 안 타도 간다). 무대는 로켓을 쏜다.</summary>
        private void TickStorm()
        {
            if (Landmark != null && Landmark.Burning && Landmark.Kind == StructureKind.Depot)
            {
                _stageRocketClock -= Dt;
                if (_stageRocketClock <= 0f)
                {
                    _stageRocketClock = StageRocketEvery;
                    LaunchRocket(Landmark);
                }
            }
            _stormClock -= Dt;
            if (_stormClock > 0f) return;
            _stormClock = Math.Max(2f, StormEvery - (StormStep * FinalePressure));
            // 가판대는 한 번에 하나만 쏜다: 꺼진 것을 틱마다 다시 붙이면 16초 뒤 셋이 다 쏜다(측정 ③: 5승 → 6승, 동네 패 23 그대로 — 주범은 풍등이었다).
            if (!Structures.Exists(s => s.Kind == StructureKind.Fireworks && s.Burning))
            {
                foreach (Structure s in Structures)
                {
                    if (s.Kind != StructureKind.Fireworks || s.Collapsed || s.Burning) continue;
                    s.Wet = 0f;
                    Ignite(s, 0.4f);
                    break;
                }
            }
            if (Landmark == null) return;
            Lantern pick = null;
            float best = float.MaxValue;
            foreach (Lantern l in Lanterns)
            {
                if (l.Burn >= 0f || l.Wet > 0f || !l.A.Flammable && !l.B.Flammable) continue;
                float d = Math.Min(l.A.DistanceTo(Landmark.Pos), l.B.DistanceTo(Landmark.Pos));
                if (d < best)
                {
                    best = d;
                    pick = l;
                }
            }
            if (pick == null) return;
            pick.From = pick.A.DistanceTo(Landmark.Pos) <= pick.B.DistanceTo(Landmark.Pos) ? pick.A : pick.B;
            if (!pick.Other(pick.From).Flammable) pick.From = pick.Other(pick.From);
            pick.Burn = 0f;
            pick.Cool = 0f;
            pick.Storm = true;
            JustStorm = true;
        }

        private Structure PickUnburntHouse()
        {
            var pool = new List<Structure>();
            foreach (Structure s in Structures)
            {
                if (s.Kind == StructureKind.House && !s.Collapsed && !s.Burning) pool.Add(s);
            }
            return pool.Count == 0 ? null : pool[_rng.Next(pool.Count)];
        }

        /// <summary>신고: 안 타고 안 무너진 가게 하나에 불을 낸다(젖어 있어도 난다). 불낸 가게를 돌려준다.</summary>
        private Structure Report(float fire = ReportFire)
        {
            Structure pick = PickUnburntHouse();
            if (pick == null) return null;
            pick.Wet = 0f;
            Ignite(pick, fire);
            return pick;
        }

        /// <summary>대화재 신고의 불 세기: 단계마다 커진다(0.35 → 0.5 → 0.65 → 0.8). 큰 불은 끄는 데 오래 걸려 여러 채가 동시에 타고 무너진다.</summary>
        public float FinaleReportFire
        {
            get { return Math.Min(1f, ReportFire + (PressureFireStep * FinalePressure)); }
        }

        /// <summary>가장자리 스폰의 종류(테스트용으로 공개). 스테이지 비율을 하나의 주사위에 차례로 쌓는다.</summary>
        public EnemyKind PickKind()
        {
            float r = Rand();
            float blaze = Math.Min(Stage.BlazeMax, 0.05f + (Time / 600f));
            float dart = Time < 60f ? 0f : Stage.DartShare;
            if (r < blaze) return EnemyKind.Blaze;
            if (r < blaze + dart) return EnemyKind.Dart;
            if (r < blaze + dart + Stage.SquirrelShare) return EnemyKind.Squirrel;
            if (Time >= OilFrom && r < blaze + dart + Stage.SquirrelShare + Stage.OilShare) return EnemyKind.Oil;
            // 아직 안 나오는 몫(40초 전 기름)은 누적에서 뺀다: 전엔 그 몫이 갈매기로 떨어져 공단 초반 스폰의 12%가 갈매기였다
            // (쫓아오기만 할 땐 티가 안 났지만 폭격기가 되자 공단이 15승 → 5승으로 무너졌다, 2026-10-04 맵 특색 측정 ②).
            float sea = blaze + dart + Stage.SquirrelShare + (Time >= OilFrom ? Stage.OilShare : 0f);
            if (r < sea + Stage.GullShare) return EnemyKind.Gull;
            if (r < sea + Stage.GullShare + Stage.CrabShare) return EnemyKind.Crab;
            float shore = sea + Stage.GullShare + Stage.CrabShare;
            if (Time >= PopperFrom && r < shore + Stage.PopperShare) return EnemyKind.Popper;
            if (Time >= PopperFrom && r < shore + Stage.PopperShare + Stage.LanternShare) return EnemyKind.SkyLantern;
            // 마을(수호자): 55초부터 횃불 도깨비가 섞인다.
            if (r > 1f - GoblinShare) return EnemyKind.Goblin;
            return EnemyKind.Ember;
        }

        /// <summary>폭죽이 섞이기 시작하는 시각.</summary>
        public const float PopperFrom = 30f;

        /// <summary>바다 가장자리(북쪽 끝) 아무 x: 불 갈매기 스폰.</summary>
        private Vec2 SeaPoint()
        {
            return new Vec2(1f + (Rand() * (ArenaSize - 2f)), ArenaSize - 1.5f);
        }

        private Vec2 SpawnPoint(float distance)
        {
            // 바다 스테이지: 바다에 떨어지면 뭍에서 다시 뽑는다(넓은 바다는 둑으로 밀면 북쪽 끝에 갇혀 적이 사라진다). 강은 둑으로 민다.
            int tries = Stage.Sea ? 8 : 1;
            Vec2 at;
            do
            {
                double a = Rand() * Math.PI * 2;
                at = ClampToArena(new Vec2(Player.X + (float)(Math.Cos(a) * distance), Player.Y + (float)(Math.Sin(a) * distance)));
            }
            while (--tries > 0 && InWater(at));
            // 강에 떨어진 스폰은 둑으로 민다(물 위에 서 있는 불은 없다).
            if (HasWater) PushOutOfWater(ref at, 0.5f);
            return at;
        }

        private bool InWater(Vec2 p)
        {
            foreach (Structure s in Structures)
            {
                if (s.Kind == StructureKind.Water && s.Within(p, 0.5f)) return true;
            }
            return false;
        }

        /// <summary>강이 있는 맵인가(생성 때 한 번 센다).</summary>
        public bool HasWater { get; private set; }

        /// <summary>p가 물 사각형(반지름 r만큼 넓힌) 안이면 가장 가까운 축으로 밀어낸다(BlockPlayer와 같은 규칙, 물만).</summary>
        private void PushOutOfWater(ref Vec2 p, float r)
        {
            foreach (Structure s in Structures)
            {
                if (s.Kind != StructureKind.Water) continue;
                float dx = p.X - s.Pos.X;
                float dy = p.Y - s.Pos.Y;
                float ox = s.Half.X + r - Math.Abs(dx);
                float oy = s.Half.Y + r - Math.Abs(dy);
                if (ox <= 0f || oy <= 0f) continue;
                if (ox < oy) p.X += dx >= 0f ? ox : -ox;
                else p.Y += dy >= 0f ? oy : -oy;
            }
        }

        /// <summary>a→b 선분이 물 사각형을 지나는가(slab 검사). 건물 불과 바람 번짐은 강을 못 건넌다.</summary>
        private bool CrossesWater(Vec2 a, Vec2 b)
        {
            if (!HasWater) return false;
            foreach (Structure s in Structures)
            {
                if (s.Kind != StructureKind.Water) continue;
                float t0 = 0f;
                float t1 = 1f;
                if (!Slab(a.X, b.X - a.X, s.Pos.X - s.Half.X, s.Pos.X + s.Half.X, ref t0, ref t1)) continue;
                if (!Slab(a.Y, b.Y - a.Y, s.Pos.Y - s.Half.Y, s.Pos.Y + s.Half.Y, ref t0, ref t1)) continue;
                return true;
            }
            return false;
        }

        private static bool Slab(float p, float d, float lo, float hi, ref float t0, ref float t1)
        {
            if (Math.Abs(d) < 1e-6f) return p >= lo && p <= hi;
            float a = (lo - p) / d;
            float b = (hi - p) / d;
            if (a > b) { float tmp = a; a = b; b = tmp; }
            t0 = Math.Max(t0, a);
            t1 = Math.Min(t1, b);
            return t0 <= t1;
        }

        private static Vec2 ClampToArena(Vec2 p)
        {
            return new Vec2(Clamp(p.X, 0.5f, ArenaSize - 0.5f), Clamp(p.Y, 0.5f, ArenaSize - 0.5f));
        }

        private void RebuildHash()
        {
            for (int i = 0; i < _head.Length; i++) _head[i] = -1;
            if (_next.Length < Enemies.Count) _next = new int[Enemies.Count * 2];
            for (int i = 0; i < Enemies.Count; i++)
            {
                int c = CellOf(Enemies[i].Pos);
                _next[i] = _head[c];
                _head[c] = i;
            }
        }

        private static int CellOf(Vec2 p)
        {
            int cx = Math.Min(Cells - 1, Math.Max(0, (int)(p.X / CellSize)));
            int cy = Math.Min(Cells - 1, Math.Max(0, (int)(p.Y / CellSize)));
            return (cy * Cells) + cx;
        }

        /// <summary>반경 안 적(해시 기준, 지난 이동 전 칸)을 모은다.</summary>
        private void Near(Vec2 p, float radius, List<Enemy> into)
        {
            into.Clear();
            int x0 = Math.Max(0, (int)((p.X - radius) / CellSize));
            int x1 = Math.Min(Cells - 1, (int)((p.X + radius) / CellSize));
            int y0 = Math.Max(0, (int)((p.Y - radius) / CellSize));
            int y1 = Math.Min(Cells - 1, (int)((p.Y + radius) / CellSize));
            for (int cy = y0; cy <= y1; cy++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    for (int i = _head[(cy * Cells) + cx]; i >= 0; i = _next[i])
                    {
                        Enemy e = Enemies[i];
                        if (!e.Dead && e.Pos.DistanceTo(p) <= radius + e.Radius) into.Add(e);
                    }
                }
            }
        }

        private readonly List<Enemy> _near = new List<Enemy>();

        private void MoveEnemies()
        {
            float knockDecay = (float)Math.Exp(-8f * Dt);
            for (int i = 0; i < Enemies.Count; i++)
            {
                Enemy e = Enemies[i];
                if (e.Dead) continue;
                if (e.HitFlash > 0f) e.HitFlash -= Dt;
                if (e.BounceCool > 0f) e.BounceCool -= Dt;
                if (e.Slowed > 0f) e.Slowed -= Dt;
                if (e.WhipCool > 0f) e.WhipCool -= Dt;
                if (e.SprayCool > 0f) e.SprayCool -= Dt;
                if (Build.Free && SampleMobStep(e)) continue;
                // 언 몹은 제자리에 서 있다가 풀리는 순간 깨진다. 갇힌 몹은 방울 속에 떠 있다가 터진다.
                if (e.Frozen > 0f)
                {
                    e.Frozen -= Dt;
                    if (e.Frozen <= 0f) Damage(e, MineShatter, default, true, HitSource.Mine, e.Pos);
                    continue;
                }
                if (e.Captured > 0f)
                {
                    e.Captured -= Dt;
                    if (e.Captured <= 0f) PopBubble(e);
                    continue;
                }
                if (IsRaider(e.Kind))
                {
                    if (MoveRaider(e))
                    {
                        e.Knock.X *= knockDecay;
                        e.Knock.Y *= knockDecay;
                    }
                    continue;
                }

                Vec2 chase = Player;
                if (e.Kind == EnemyKind.Squirrel)
                {
                    // 불다람쥐: 가까운 안 탄 나무로 달려간다. 나무가 없으면 소방관을 쫓는다(사그라들지 않는다).
                    e.GoalClock -= Dt;
                    if (e.Goal != null && !e.Goal.Flammable) e.Goal = null;
                    if (e.Goal == null && e.GoalClock <= 0f)
                    {
                        e.GoalClock = 0.5f;
                        e.Goal = NearestTree(e.Pos, 20f);
                    }
                    if (e.Goal != null) chase = e.Goal.Pos;
                }
                else if (e.Seeker)
                {
                    // 건물에서 나온 불씨는 가까운 탈 것을 노린다. 없으면 소방관을 쫓는다.
                    // 갈매기·게·풍등은 멀리서도 건물만 노린다(맵 특색 몹: 어느 지붕을 노리는지 뷰가 보여 준다).
                    e.GoalClock -= Dt;
                    if (e.Goal != null && !e.Goal.Flammable) e.Goal = null;
                    if (e.Goal == null && e.GoalClock <= 0f)
                    {
                        e.GoalClock = 0.5f;
                        e.Goal = TargetsBuildings(e) ? NearestBuilding(e.Pos, BuildingSight) : NearestFlammable(e.Pos, EmberSight);
                    }
                    if (e.Goal != null) chase = e.Goal.Pos;
                    else
                    {
                        // 탈 것을 못 찾은 불씨는 소방관 쪽으로 굴러가다 4초 뒤 사그라든다(구슬 없음).
                        e.Idle += Dt;
                        if (e.Idle >= 4f)
                        {
                            e.Dead = true;
                            continue;
                        }
                    }
                }
                if (e.Kind == EnemyKind.Gull && e.Dropped)
                {
                    // 불을 떨어뜨린 갈매기는 바다로 돌아가 북쪽 끝에서 사라진다(구슬 없음).
                    chase = new Vec2(e.Pos.X, ArenaSize + 4f);
                    if (e.Pos.Y >= ArenaSize - 0.5f)
                    {
                        e.Dead = true;
                        continue;
                    }
                }
                float dx = chase.X - e.Pos.X;
                float dy = chase.Y - e.Pos.Y;
                float d = (float)Math.Sqrt((dx * dx) + (dy * dy));
                // 풍등은 하늘을 떠서 안개·장막에 안 느려진다.
                float speed = e.Speed * (e.Slowed > 0f && e.Kind != EnemyKind.SkyLantern ? 0.5f : 1f);
                if (e.Kind == EnemyKind.Popper)
                {
                    // 폭죽은 깡충깡충: 한 주기 1초 중 앞 PopperHop만 움직인다.
                    e.Phase += Dt;
                    if (e.Phase - (float)Math.Floor(e.Phase) >= PopperHop) speed = 0f;
                }
                float vx = d > 0.01f ? dx / d * speed : 0f;
                float vy = d > 0.01f ? dy / d * speed : 0f;
                if (e.Kind == EnemyKind.Squirrel || e.Kind == EnemyKind.Bat || e.Kind == EnemyKind.Gull || e.Kind == EnemyKind.Crab)
                {
                    // 다람쥐는 지그재그로, 박쥐는 크게, 갈매기는 더 느리고 넓게 출렁이며, 게는 옆걸음으로 온다(진행 방향에 수직으로 흔든다).
                    e.Phase += Dt * (e.Kind == EnemyKind.Squirrel ? 9f : e.Kind == EnemyKind.Gull ? 4f : e.Kind == EnemyKind.Crab ? 2.5f : 5f);
                    float sway = (float)Math.Sin(e.Phase) * (e.Kind == EnemyKind.Squirrel ? 0.9f : e.Kind == EnemyKind.Gull ? 1.6f : e.Kind == EnemyKind.Crab ? 0.8f : 1.3f);
                    float px = -vy;
                    float py = vx;
                    vx += px * sway;
                    vy += py * sway;
                }

                // 서로 겹치지 않게 살짝 민다(떼가 덩어리가 아니라 무리로 보이게).
                float sx = 0f;
                float sy = 0f;
                int c = CellOf(e.Pos);
                int ccx = c % Cells;
                int ccy = c / Cells;
                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int nx = ccx + ox;
                        int ny = ccy + oy;
                        if (nx < 0 || ny < 0 || nx >= Cells || ny >= Cells) continue;
                        for (int j = _head[(ny * Cells) + nx]; j >= 0; j = _next[j])
                        {
                            if (j == i) continue;
                            Enemy o = Enemies[j];
                            float ex = e.Pos.X - o.Pos.X;
                            float ey = e.Pos.Y - o.Pos.Y;
                            float min = e.Radius + o.Radius;
                            float d2 = (ex * ex) + (ey * ey);
                            if (d2 >= min * min || d2 < 0.0001f) continue;
                            float dd = (float)Math.Sqrt(d2);
                            float push = (min - dd) / min;
                            sx += ex / dd * push;
                            sy += ey / dd * push;
                        }
                    }
                }

                float stepX = (vx + (sx * 3f) + e.Knock.X) * Dt;
                float stepY = (vy + (sy * 3f) + e.Knock.Y) * Dt;
                e.Pos.X += stepX;
                e.Pos.Y += stepY;

                // 큰 불과 기름 방울은 걸어온 자리에 불을 흘린다(몸만 덴다). 건물을 태우는 기름은 기름 방울이 터질 때만 튄다:
                // 흔적까지 건물을 태우면 소방관을 쫓아 불난 건물 곁을 도는 방울이 한 판에 열 번씩 불을 냈다(측정 6).
                if (e.Kind == EnemyKind.Blaze || e.Kind == EnemyKind.Oil)
                {
                    e.Trail += (float)Math.Sqrt((stepX * stepX) + (stepY * stepY));
                    if (e.Trail >= TrailStep)
                    {
                        e.Trail = 0f;
                        if (BurningGround.Count < MaxBurningGround)
                        {
                            BurningGround.Add(new Puddle { Pos = e.Pos, Radius = 0.6f, Life = 4f, MaxLife = 4f });
                        }
                    }
                }
                e.Pos = ClampToArena(e.Pos);
                // 땅을 기는 불은 강을 못 건넌다(둑에 멈춘다). 불씨·박쥐·갈매기·풍등은 날아 건너고, 게는 바다에서 기어 나온다.
                if (HasWater && !e.Seeker && e.Kind != EnemyKind.Bat && e.Kind != EnemyKind.Gull && e.Kind != EnemyKind.Crab && e.Kind != EnemyKind.SkyLantern) PushOutOfWater(ref e.Pos, e.Radius);
                // 건물에서 나온 불씨와 다람쥐만 옮겨붙인다. 소방관을 쫓는 불(가장자리 불씨·큰 불)은 발밑에 불을 흘릴 뿐이다.
                if (e.Seeker) TouchStructures(e);
                e.Knock.X *= knockDecay;
                e.Knock.Y *= knockDecay;
            }
        }

        private void FireWeapons()
        {
            // 물대포: 겨눈 쪽으로, 쥐고 있을 때만. 예전 자동 조준(0.32초)과 초당 피해를 맞췄다.
            // 수호자: 쥐지 않아도 가까운 불을 AutoPower배로 쏜다(증기는 안 쌓는다). 쥐면 겨눈 쪽으로, 증기까지(집중 분사).
            // 숲(2026-10-08): 물대포는 겨누는 한 줄기뿐이다(펌프가 합쳐져 레벨마다 굵고 멀리, 고압 노즐·급수 펌프가 곱해진다). 방수포 대폭발만 샘플 아이템(HoseItem)이다.
            int hose = Build.Free ? Build.PowerOf(UpgradeId.Hose) : Build.Level(UpgradeId.Hose);
            // 숲 Lv6(고압 방수포)도 모두 꿰뚫는 굵은 한 줄기다(18갈래 회전은 숲에서 끈다).
            bool cannon = Build.Level(UpgradeId.Cannon) > 0;
            float nozzle = Build.Free ? Build.NozzleScale : 1f;
            _hoseClock -= Dt;
            bool auto = false;
            if (Guardian && !Spraying && (hose > 0 || cannon))
            {
                Vec2? target = AutoTarget();
                if (target.HasValue)
                {
                    auto = true;
                    Aim = new Vec2(target.Value.X - Player.X, target.Value.Y - Player.Y);
                }
            }
            AutoFiring = auto;
            if ((Spraying || auto) && (hose > 0 || cannon) && _hoseClock <= 0f && (Aim.X != 0f || Aim.Y != 0f))
            {
                // 숲 급수 펌프: 물대포도 더 자주 나간다.
                _hoseClock = HoseInterval / (Build.Free ? Build.FeedScale : 1f);
                float baseAngle = (float)Math.Atan2(Aim.Y, Aim.X);
                float power = auto ? AutoPower : 1f;
                if (auto) Stats.AutoShots++;
                else Stats.FocusShots++;
                if (cannon)
                {
                    // 진화 후: 한 줄기로 모든 불을 꿰뚫는 고압 제트.
                    FireDrop(baseAngle, 13.2f * (HoseInterval / 0.4f) * Build.HosePower * power * nozzle, 18f, 0.55f, 999, 0.6f * Build.HoseRange, ShotKind.Jet, true, !auto);
                }
                else
                {
                    // 늘 한 줄기. 레벨이 오를수록 굵고(반경) 세고(피해) 멀리(수명) 나가며 더 많이 꿰뚫는다.
                    float damage = 3f * (HoseInterval / 0.32f) * HosePower(hose) * Build.HosePower * power * nozzle;
                    FireDrop(baseAngle, damage, 16f, HoseRadius(hose), 1 + hose, 0.6f * (1f + (0.1f * (hose - 1))) * Build.HoseRange, ShotKind.Drop, true, !auto);
                }
            }

            if (!Build.Free && Build.Level(UpgradeId.Cannon) > 0)
            {
                _jetClock -= Dt;
                if (_jetClock <= 0f && _jetQueue == 0)
                {
                    _jetClock = 2.4f;
                    _jetQueue = 18;
                }
                if (_jetQueue > 0)
                {
                    // 한 틱에 한 줄기씩, 18줄기로 한 바퀴를 휩쓴다.
                    _jetAngle += (float)(Math.PI * 2 / 18);
                    _jetQueue--;
                    FireDrop(_jetAngle, 2.2f * 2f * 3f * Build.HosePower, 16f, 0.55f, 999, 0.8f * Build.HoseRange, ShotKind.Jet);
                }
            }

            if (Build.Free) TickFree();
            else TickNewWeapons();
        }

        private static float SegmentDistance(Vec2 p, Vec2 a, Vec2 b)
        {
            float vx = b.X - a.X;
            float vy = b.Y - a.Y;
            float len2 = (vx * vx) + (vy * vy);
            float t = len2 > 0f ? Clamp((((p.X - a.X) * vx) + ((p.Y - a.Y) * vy)) / len2, 0f, 1f) : 0f;
            return p.DistanceTo(new Vec2(a.X + (vx * t), a.Y + (vy * t)));
        }

        /// <summary>물대포 레벨별 위력 배수: Lv5면 2.8배(예전 다섯 줄기의 총량과 비슷).</summary>
        public static float HosePower(int level)
        {
            return 1f + (0.45f * (level - 1));
        }

        /// <summary>물대포 레벨별 물줄기 반경(굵기).</summary>
        public static float HoseRadius(int level)
        {
            return 0.3f + (0.12f * (level - 1));
        }

        /// <summary>한 명 구할 때 차는 체력(예전 20: 구조만 하면 체력이 늘 차서 방화복이 쓸모없었다).</summary>
        public const float RescueHeal = 5f;

        /// <summary>열기: 타는 건물 가장자리 이 칸 안에서 불 세기 × HeatDps만큼 초당 피해를 받는다(방화복이 줄인다). 오래 끄느라 서 있는 자리가 뜨겁다(예전 2.5칸·5).</summary>
        public const float HeatRange = 4f;
        public const float HeatDps = 7f;
        public const float TreeHeatRange = 1.5f;

        /// <summary>
        /// 자동 분사가 노릴 곳(숙련 봇의 순서): 코앞 1.5칸 큰 불·기름 → 4칸 안 타는 구조물 → 3칸 안 아무 불 → 사거리 안 타는 구조물 → 사거리 안 아무 불.
        /// 없으면 null(쏘지 않는다).
        /// </summary>
        public Vec2? AutoTarget()
        {
            Vec2? heavy = NearestFire(1.5f, true);
            if (heavy.HasValue) return heavy;
            Structure near = NearestBurningStructure(4f);
            if (near != null) return near.Pos;
            Vec2? close = NearestFire(3f, false);
            if (close.HasValue) return close;
            Structure far = NearestBurningStructure(AutoReach);
            if (far != null) return far.Pos;
            return NearestFire(AutoReach, false);
        }

        private Vec2? NearestFire(float range, bool heavyOnly)
        {
            Vec2? best = null;
            float bestD = range;
            foreach (Enemy e in Enemies)
            {
                if (e.Dead) continue;
                if (heavyOnly && e.Kind != EnemyKind.Blaze && e.Kind != EnemyKind.Oil) continue;
                float d = e.Pos.DistanceTo(Player);
                if (d < bestD)
                {
                    bestD = d;
                    best = e.Pos;
                }
            }
            return best;
        }

        private Structure NearestBurningStructure(float range)
        {
            Structure best = null;
            float bestD = range;
            foreach (Structure s in Structures)
            {
                if (!s.Burning) continue;
                float d = s.DistanceTo(Player);
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }
            return best;
        }

        private void FireDrop(float angle, float damage, float speed, float radius, int pierce, float life, ShotKind kind, bool hose = false, bool focus = true)
        {
            var vel = new Vec2((float)Math.Cos(angle) * speed, (float)Math.Sin(angle) * speed);
            Shots.Add(new Shot
            {
                Kind = kind,
                From = Player,
                Pos = Player,
                Vel = vel,
                Life = life,
                Damage = damage,
                Radius = radius,
                Pierce = pierce,
                Struck = pierce > 1 ? new List<Enemy>() : null,
                Hose = hose,
                Focus = focus,
            });
            ShotsFired++;
        }

        private void MoveShots()
        {
            foreach (Shot s in Shots)
            {
                if (s.Dead) continue;
                s.Age += Dt;

                s.Pos.X += s.Vel.X * Dt;
                s.Pos.Y += s.Vel.Y * Dt;
                if (s.Age >= s.Life)
                {
                    s.Dead = true;
                    continue;
                }

                Douse(s.Pos, s.Radius);
                if (SoakStructures(s)) continue;
                Near(s.Pos, s.Radius, _near);
                foreach (Enemy e in _near)
                {
                    if (s.Struck != null)
                    {
                        if (s.Struck.Contains(e)) continue;
                        s.Struck.Add(e);
                    }
                    var dir = new Vec2(s.Vel.X / 14f, s.Vel.Y / 14f);
                    // 물줄기는 불 떼를 민다: 노즐 가까이는 세고 끝에선 약하다(끝까지 밀어내면 물줄기 끝에 산 채로 쌓인다).
                    // 큰 불·기름 방울은 무거워서 절반만 밀린다.
                    float push = s.Hose ? HoseKnock * (1f - (s.Age / s.Life)) : 2.5f;
                    if (e.Kind == EnemyKind.Blaze || e.Kind == EnemyKind.Oil) push *= 0.5f;
                    Damage(e, s.Damage, new Vec2(dir.X * push, dir.Y * push), true, HitSource.Hose, s.From);
                    s.Pierce--;
                    if (s.Pierce <= 0)
                    {
                        s.Dead = true;
                        break;
                    }
                }
            }
        }

        private void TickPuddles()
        {
            foreach (Puddle p in BurningGround)
            {
                p.Life -= Dt;
                if (p.Pos.DistanceTo(Player) <= p.Radius + PlayerRadius)
                {
                    // 장화: 불 바닥을 밟아도 안 다치고, 밟은 자리는 꺼진다.
                    if (Build.WetBoots)
                    {
                        if (!p.Out)
                        {
                            p.Out = true;
                            p.Life = 0f;
                            Extinguished.Add(p.Pos);
                            Footprints.Add(Player);
                        }
                    }
                    else Burn(10f * Dt, HurtKind.Ground);
                }
                if (!p.Oil || p.Out || p.Life <= 0f) continue;
                // 기름 불은 닿은 탈 것에 옮겨붙는다: 건물 곁에서 기름 방울을 터뜨리면 건물이 탄다.
                foreach (Structure st in Structures)
                {
                    if (st.Kind == StructureKind.Tree || !st.Flammable || !st.Within(p.Pos, p.Radius)) continue;
                    if (!Ignite(st, OilIgnite)) continue;
                    OilCaught.Add(st);
                    Stats.OilFires++;
                }
            }
        }

        /// <summary>기름 불 하나를 놓는다(바닥 불 한도 안).</summary>
        public void AddOil(Vec2 at)
        {
            if (BurningGround.Count >= MaxBurningGround) return;
            BurningGround.Add(new Puddle { Pos = ClampToArena(at), Radius = OilRadius, Life = OilLife, MaxLife = OilLife, Oil = true });
        }

        /// <summary>at 둘레 1.5칸 안에 기름 불 n개를 흩뿌린다.</summary>
        private void Spill(Vec2 at, int n)
        {
            for (int k = 0; k < n; k++)
            {
                double a = (Math.PI * 2 * k / n) + (Rand() * 0.8);
                float d = 0.6f + (Rand() * 0.9f);
                AddOil(new Vec2(at.X + (float)(Math.Cos(a) * d), at.Y + (float)(Math.Sin(a) * d)));
            }
        }

        // ------------------------------------------------------------------
        // 동네
        // ------------------------------------------------------------------

        /// <summary>구조물에 불을 붙인다(젖었거나 무너졌으면 안 붙는다). 새로 붙었으면 true.</summary>
        public bool Ignite(Structure s, float amount)
        {
            if (s.Kind == StructureKind.Water || s.Collapsed || s.Wet > 0f) return false;
            bool fresh = s.Fire <= 0f;
            s.Fire = Math.Min(1f, Math.Max(s.Fire, amount));
            if (fresh)
            {
                Ignited.Add(s);
                s.SpitClock = 2f;
                s.BlazeClock = 6f;
                s.SpreadClock = Stage.SpreadEvery;
                s.RescueHold = 0f;
                s.Smoke = 0f;
                // 가스통은 터지고, 불꽃 가판대는 로켓을 쏜다: 둘 다 같은 퓨즈.
                if (s.Kind == StructureKind.Gas || s.Kind == StructureKind.Fireworks) s.Fuse = GasFuse;
            }
            return fresh;
        }

        /// <summary>
        /// 물을 붓는다: 타면 불 세기를 줄이고, 다 꺼지거나 안 타면 한동안 젖는다.
        /// 꾸준히 뿌리는 물(호스·드론·포탑·대원·비)은 큰 불에 덜 먹힌다(resist). 한 번에 쏟는 물(폭탄·헬기·장막·소방차·스프링클러)은
        /// 불 세기와 상관없이 다 먹힌다: 놓친 큰 불을 잡는 건 그런 아이템의 몫이다.
        /// </summary>
        private void Soak(Structure s, float water, bool resist = true)
        {
            if (s.Collapsed) return;
            if (s.Burning)
            {
                // 큰 불일수록 물이 덜 먹힌다: 일찍 잡으면 쉽고, 놓치면 오래 걸린다.
                // 건물은 꾸준한 물(호스·대원·포탑)이 BuildingWater만큼만 먹힌다. 한 방(물폭탄·투하·증기·헬기, resist=false)은 그대로.
                float before = s.Fire;
                if (resist && s.IsBuilding) water *= Stage.BuildingWater;
                s.Fire -= resist ? water * (1f - (FireResist * s.Fire)) : water;
                if (before - Math.Max(0f, s.Fire) > KnockShown && s.IsBuilding) Knocked.Add(new FireKnock { At = s, Amount = before - Math.Max(0f, s.Fire) });
                if (s.Fire > 0f) return;
                s.Fire = 0f;
                s.Fuse = -1f;
                s.HoseHold = 0f;
                // 연기 계수도 지운다: 안 지우면 껐다 다시 붙은 집이 3초 만에 사람을 잃었다(2026-10-10, docs §24).
                s.Smoke = 0f;
                s.Warned = false;
                Doused.Add(s);
                // 물 위에서 끈 배는 바다로 돌아간다(TickBoats가 북쪽으로 돌린다). 끈 가판대는 로켓을 멈춘다.
                if (s.Kind == StructureKind.Boat && !s.Docked)
                {
                    BoatsAway.Add(s);
                    // 요격 보상: 부두에 닿기 전에 바다 위에서 끈 배(유조선은 어차피 좌초한다).
                    if (!s.Tanker) Xp += BoatXp;
                }
                s.Launching = false;
                // 숲(2026-10-10): 불을 끈 집의 갇힌 사람은 스스로 나온다(구조로 센다, 대원 합류). 숲엔 아이템 구조가 없어
                // 문 앞 2초를 못 채우고 끄면 사람이 집 안에 남아 다음 신고 때 연기를 마셨다(숙련 봇 구조 0.5 · 잃음 11.4/17, docs §24).
                if (Build.Free) ReleaseResidents(s);
                // 불을 끈 보상: 건물은 큰 구슬, 작은 것은 작은 구슬. 건물 진화는 콤보를 크게 잇는다.
                if (s.IsBuilding) ComboAdd(ComboPerDouse);
                DropGem(s.Door, (s.IsBuilding ? 8 : 3) * ComboMult);
            }
            s.Wet = WetTime;
        }

        /// <summary>물줄기가 구조물에 닿았는지. 물대포 물방울은 막혀서 사라지면 true, 제트는 뚫고 간다.</summary>
        private bool SoakStructures(Shot s)
        {
            if (Lanterns.Count > 0) WetLanterns(s.Pos, LanternReach + (s.Radius * 0.5f));
            foreach (Structure st in Structures)
            {
                // 항구의 바다는 물줄기를 막지 않는다(바다 위 불배를 부두에서 쏜다). 마을 강은 예전처럼 막는다(Stage.Sea).
                if (st.Collapsed || (st.Kind == StructureKind.Water && Stage.Sea) || !st.Within(s.Pos, s.Radius * 0.5f)) continue;
                // 제트는 뚫고 가고, 나무는 물이 잎 사이로 빠진다(나무 밑에서 쏴도 막히지 않게).
                if (s.Kind == ShotKind.Jet || st.Kind == StructureKind.Tree)
                {
                    if (s.Soaked == null) s.Soaked = new List<Structure>();
                    if (s.Soaked.Contains(st)) continue;
                    s.Soaked.Add(st);
                    if (s.Hose && s.Focus) Warm(st);
                    Soak(st, s.Damage * WaterPerDamage);
                    continue;
                }
                if (s.Hose && s.Focus) Warm(st);
                Soak(st, s.Damage * WaterPerDamage);
                s.Dead = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 물대포 물줄기가 건물 불에 닿았다: 맞은 물이 쌓이고(틱마다 SteamCool씩 식는다), SteamHold에 닿으면 증기 폭발.
        /// 증기 폭발은 큰 불에도 다 먹히는 한 방이라, 저항(FireResist)을 "버티는 손"으로 뚫는 길이다.
        /// </summary>
        private void Warm(Structure st)
        {
            if (!st.IsBuilding || !st.Burning) return;
            st.HoseHold += HoseInterval * Build.SteamScale;
            if (st.HoseHold < SteamHold) return;
            st.HoseHold = 0f;
            Soak(st, SteamDouse, false);
            SteamBursts.Add(st);
            Stats.SteamBursts++;
            // 곁의 불 몹을 데우고 바깥으로 민다(펌프가 반경을 넓힌다).
            float reach = SteamRadius + Build.SteamRadiusBonus;
            foreach (Enemy e in Enemies)
            {
                if (e.Dead || st.DistanceTo(e.Pos) > reach) continue;
                Damage(e, SteamHit, Knockback(st.Pos, e.Pos, SteamPush), true, HitSource.Steam, st.Pos);
            }
        }

        /// <summary>불이 탈 것에 닿으면 옮겨붙는다. 불씨는 불을 옮기고 사라진다(젖은 곳에 닿아도 꺼진다).</summary>
        private void TouchStructures(Enemy e)
        {
            foreach (Structure st in Structures)
            {
                if (st.Collapsed || st.Burning) continue;
                // 물 위를 나는 불씨는 그대로 건넌다(붙을 것도, 사라질 일도 없다).
                if (st.Kind == StructureKind.Water) continue;
                // 다람쥐는 나무에만 불을 붙인다: 가는 길의 건물까지 태우면 건물당 불이 마을의 1.5배라 동네를 늘 잃었다.
                // 나무 불은 바람을 타고 건물을 위협하므로 숲다운 압박은 남는다.
                if (e.Kind == EnemyKind.Squirrel && st.Kind != StructureKind.Tree) continue;
                if (!st.Within(e.Pos, e.Radius)) continue;
                // 맵 특색 몹: 갈매기는 지붕에 불을 떨어뜨리고 바다로, 게는 불을 지피고 소방관을 쫓고, 풍등은 내려앉아 타 없어진다(구슬 없음).
                if (e.Kind == EnemyKind.Gull || e.Kind == EnemyKind.Crab)
                {
                    Ignite(st, e.Kind == EnemyKind.Gull ? 0.25f : 0.4f);
                    e.Dropped = true;
                    e.Seeker = false;
                    e.Goal = null;
                    if (e.Kind == EnemyKind.Gull) GullDrops.Add(e.Pos);
                    return;
                }
                if (e.Kind == EnemyKind.SkyLantern)
                {
                    Ignite(st, 0.3f);
                    e.Dead = true;
                    LanternLands.Add(e.Pos);
                    return;
                }
                Ignite(st, 0.15f);
                // 불씨와 다람쥐는 불을 붙이며 그 속으로 사라진다(다람쥐가 살아남으면 한 마리가 숲을 다 태운다).
                if (e.Kind == EnemyKind.Ember || e.Kind == EnemyKind.Squirrel)
                {
                    e.Dead = true;
                    return;
                }
            }
        }

        /// <summary>s와 가장자리 거리가 SpreadRange 안인 가장 가까운 불붙을 수 있는 건물(젖었거나 타면 건너뛴다).</summary>
        public Structure NextBuilding(Structure s)
        {
            Structure best = null;
            float bestD = SpreadRange;
            foreach (Structure t in Structures)
            {
                if (t == s || !t.IsBuilding || !t.Flammable) continue;
                // 강 건너로는 안 옮는다.
                if (CrossesWater(s.Pos, t.Pos)) continue;
                float dx = Math.Max(Math.Abs(t.Pos.X - s.Pos.X) - t.Half.X - s.Half.X, 0f);
                float dy = Math.Max(Math.Abs(t.Pos.Y - s.Pos.Y) - t.Half.Y - s.Half.Y, 0f);
                float gap = (float)Math.Sqrt((dx * dx) + (dy * dy));
                if (gap <= bestD)
                {
                    bestD = gap;
                    best = t;
                }
            }
            return best;
        }

        /// <summary>s에서 바람이 부는 쪽(내적 &gt; 0.3) 4칸 안의 가장 가까운 안 탄 나무나 건물.</summary>
        private Structure Downwind(Structure s)
        {
            Structure best = null;
            float bestD = WindSpreadRange;
            foreach (Structure t in Structures)
            {
                // 나무와 건물로 옮는다: 나무에만 번지면 지킬 것 없는 불이라 끌 이유가 없다(차·가스통은 제외).
                if (t == s || !(t.Kind == StructureKind.Tree || t.IsBuilding) || !t.Flammable) continue;
                float dx = t.Pos.X - s.Pos.X;
                float dy = t.Pos.Y - s.Pos.Y;
                float d = (float)Math.Sqrt((dx * dx) + (dy * dy));
                if (d < 0.01f || ((dx * Wind.X) + (dy * Wind.Y)) / d <= 0.3f) continue;
                if (CrossesWater(s.Pos, t.Pos)) continue;
                float gap = t.DistanceTo(s.Pos) - Math.Max(s.Half.X, s.Half.Y);
                if (gap < bestD)
                {
                    bestD = gap;
                    best = t;
                }
            }
            return best;
        }

        private Structure NearestTree(Vec2 p, float range)
        {
            Structure best = null;
            float bestD = range;
            foreach (Structure s in Structures)
            {
                if (s.Kind != StructureKind.Tree || !s.Flammable) continue;
                float d = s.DistanceTo(p);
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }
            return best;
        }

        /// <summary>
        /// 등줄: 한쪽 점포가 LanternCatch 이상 타면 LanternDelay 뒤 불이 줄에 붙어 LanternRun초에 건너가 반대쪽에 붙는다(LanternIgnite).
        /// 출발 점포가 꺼지면 줄 불도 꺼진다. 젖은 줄(물줄기·장막·비·투하·안개)은 꺼지고 LanternWet초 동안 안 탄다. 건너간 줄은 LanternCool초 쉰다.
        /// </summary>
        private void TickLanterns()
        {
            foreach (Lantern l in Lanterns)
            {
                if (l.Wet > 0f)
                {
                    l.Wet -= Dt;
                    continue;
                }
                if (l.Cool > 0f) l.Cool -= Dt;
                if (l.Burn < 0f)
                {
                    if (l.Cool > 0f) continue;
                    Structure from = l.A.Burning && l.A.Fire >= LanternCatch && l.B.Flammable ? l.A
                        : l.B.Burning && l.B.Fire >= LanternCatch && l.A.Flammable ? l.B : null;
                    if (from == null)
                    {
                        l.Delay = LanternDelay;
                        continue;
                    }
                    l.Delay -= Dt;
                    if (l.Delay > 0f) continue;
                    l.From = from;
                    l.Burn = 0f;
                }
                if (!l.From.Burning && !l.Storm)
                {
                    l.Burn = -1f;
                    continue;
                }
                l.Burn += Dt / LanternRun;
                if (l.Burn < 1f) continue;
                Structure to = l.Other(l.From);
                l.Burn = -1f;
                l.Storm = false;
                l.Cool = LanternCool;
                if (Ignite(to, LanternIgnite))
                {
                    LanternCaught.Add(l);
                    Spread.Add(to);
                    SpreadFrom.Add(l.From);
                    Stats.Spreads++;
                }
            }
        }

        /// <summary>at에서 r 안을 지나는 등줄을 적신다: 줄 불이 꺼지고 LanternWet초 동안 안 탄다.</summary>
        public void WetLanterns(Vec2 at, float r)
        {
            foreach (Lantern l in Lanterns)
            {
                if (SegmentDistance(at, l.A.Pos, l.B.Pos) > r) continue;
                if (l.Burn >= 0f || l.Wet <= 0f) LanternDoused.Add(l);
                l.Burn = -1f;
                l.Storm = false;
                l.Wet = LanternWet;
            }
        }

        /// <summary>줄 불이 지금 있는 자리(그림용). 안 타면 From 쪽 끝.</summary>
        public Vec2 LanternFire(Lantern l)
        {
            Structure from = l.From ?? l.A;
            Structure to = l.Other(from);
            float t = Math.Max(0f, l.Burn);
            return new Vec2(from.Pos.X + ((to.Pos.X - from.Pos.X) * t), from.Pos.Y + ((to.Pos.Y - from.Pos.Y) * t));
        }

        /// <summary>불꽃 가판대가 로켓 한 발을 쏜다: 반은 RocketRange 안 안 탄 탈 것을, 반은 아무 데나 노린다.</summary>
        private void LaunchRocket(Structure s)
        {
            Vec2 target;
            Structure pick = null;
            if (Rand() < 0.5f)
            {
                var near = new List<Structure>();
                foreach (Structure t in Structures)
                {
                    if (t != s && t.Flammable && t.Kind != StructureKind.Tree && t.DistanceTo(s.Pos) <= RocketRange) near.Add(t);
                }
                if (near.Count > 0) pick = near[Math.Min(near.Count - 1, (int)(Rand() * near.Count))];
            }
            if (pick != null) target = pick.Pos;
            else
            {
                double a = Rand() * Math.PI * 2;
                float d = 3f + (Rand() * (RocketRange - 3f));
                target = ClampToArena(new Vec2(s.Pos.X + (float)(Math.Cos(a) * d), s.Pos.Y + (float)(Math.Sin(a) * d)));
            }
            Rockets.Add(new Rocket { From = s.Pos, Target = target, Life = RocketFlight });
        }

        /// <summary>로켓이 떨어진다: 그 자리 탈 것에 불(RocketIgnite), 없으면 바닥 불 4초. 곁 소방관은 RocketBurn. 물안개 안에 떨어지면 꺼진다.</summary>
        private void TickRockets()
        {
            foreach (Rocket r in Rockets)
            {
                r.Age += Dt;
                if (r.Age < r.Life) continue;
                r.Dead = true;
                RocketBursts.Add(r.Target);
                Structure hit = null;
                foreach (Structure st in Structures)
                {
                    if (st.Collapsed || st.Kind == StructureKind.Water || !st.Within(r.Target, RocketHit)) continue;
                    hit = st;
                    break;
                }
                if (hit != null) Ignite(hit, RocketIgnite);
                else if (BurningGround.Count < MaxBurningGround) BurningGround.Add(new Puddle { Pos = r.Target, Radius = 0.8f, Life = 4f, MaxLife = 4f });
                if (Player.DistanceTo(r.Target) <= 1.5f) Burn(RocketBurn, HurtKind.Blast);
            }
            Rockets.RemoveAll(r => r.Dead);
        }

        /// <summary>s 가장자리에서 틈 gap 안의 가장 가까운 안 탄 탈 것(물·s 자신 제외).</summary>
        private Structure NearestFlammableTo(Structure s, float gap)
        {
            Structure best = null;
            float bestD = gap;
            float reach = Math.Max(s.Half.X, s.Half.Y);
            foreach (Structure t in Structures)
            {
                if (t == s || !t.Flammable) continue;
                float d = t.DistanceTo(s.Pos) - reach;
                if (d < bestD)
                {
                    bestD = d;
                    best = t;
                }
            }
            return best;
        }

        /// <summary>
        /// 항구: BoatEvery마다 불붙은 배가 바다 왼쪽·오른쪽 끝에서 BoatLane 줄을 따라 떠내려온다. 노린 부둣가 건물의 x에 오면 남쪽으로 꺾어
        /// 부두선(SeaFrom)에 닿아 멈추고(Docked), 타는 동안 BoatSpreadEvery마다 틈 BoatDockGap 안의 가장 가까운 탈 것에 불을 옮긴다.
        /// 물 위에서 꺼지면 북쪽으로 돌아가 바다 밖으로 사라진다(Collapsed, 건물 손실 아님). 다 타면 가라앉는다(Fall).
        /// </summary>
        private void TickBoats()
        {
            if (Time >= _nextBoat)
            {
                _nextBoat = Time + Stage.BoatEvery;
                Structure target = PickQuayTarget();
                bool fromLeft = Rand() < 0.5f;
                var boat = new Structure
                {
                    Kind = StructureKind.Boat,
                    Name = "불배",
                    Pos = new Vec2(fromLeft ? -1f : ArenaSize + 1f, SurvivorHarbor.BoatLane),
                    Half = new Vec2(1.2f, 0.6f),
                    Target = target,
                    Drift = new Vec2(fromLeft ? BoatSpeed : -BoatSpeed, 0f),
                };
                Structures.Add(boat);
                Ignite(boat, BoatFire);
                JustBoat = boat;
            }

            foreach (Structure b in Structures)
            {
                if (b.Kind != StructureKind.Boat || b.Collapsed) continue;
                float speed = b.Tanker ? TankerSpeed : BoatSpeed;
                // 유조선은 꺼도 돌아가지 않고 좌초한다(바다 위에서 끄면 끝나는 대화재는 대화재가 아니다).
                if (!b.Burning && !b.Docked && !b.Tanker)
                {
                    // 꺼진 배는 바다로 돌아간다.
                    b.Drift = new Vec2(0f, speed);
                    if (b.Pos.Y - b.Half.Y > ArenaSize + 2f)
                    {
                        b.Collapsed = true;
                        continue;
                    }
                }
                else if (!b.Docked && b.Drift.Y == 0f && b.Target != null && Math.Abs(b.Pos.X - b.Target.Pos.X) < 0.5f)
                {
                    // 노린 건물 앞: 남쪽으로 꺾는다.
                    b.Drift = new Vec2(0f, -speed);
                }
                b.Pos = new Vec2(b.Pos.X + (b.Drift.X * Dt), b.Pos.Y + (b.Drift.Y * Dt));
                if (!b.Docked && b.Drift.Y < 0f && b.Pos.Y - b.Half.Y <= SurvivorHarbor.SeaFrom)
                {
                    b.Docked = true;
                    b.Drift = default;
                    b.Pos = new Vec2(b.Pos.X, SurvivorHarbor.SeaFrom + b.Half.Y);
                    b.SpreadClock = 1f;
                    BoatsDocked.Add(b);
                    if (b.Tanker)
                    {
                        // 좌초하는 충격에 불이 다시 솟는다: 누출과 구조는 늘 부두에서 벌어진다.
                        b.Wet = 0f;
                        if (!Ignite(b, 0.8f)) b.Fire = Math.Max(b.Fire, 0.8f);
                    }
                }
                if (b.Docked && b.Burning)
                {
                    b.SpreadClock -= Dt;
                    if (b.SpreadClock <= 0f)
                    {
                        b.SpreadClock = BoatSpreadEvery;
                        Structure near = NearestFlammableTo(b, BoatDockGap);
                        if (near != null && Ignite(near, 0.4f))
                        {
                            Spread.Add(near);
                            SpreadFrom.Add(b);
                            Stats.Spreads++;
                        }
                    }
                    if (b.Tanker)
                    {
                        // 유조선 누출: 부두 위에 불기름이 배 양옆으로 번갈아 퍼진다(부두 줄 건물·연료 탱크에 닿는다).
                        _leakClock -= Dt;
                        if (_leakClock <= 0f)
                        {
                            _leakClock = TankerLeakEvery;
                            int side = _leakCount % 2 == 0 ? 1 : -1;
                            int step = _leakCount / 2;
                            _leakCount++;
                            var at = ClampToArena(new Vec2(b.Pos.X + (side * step * TankerLeakStep), SurvivorHarbor.SeaFrom - 1.2f));
                            if (BurningGround.Count < MaxBurningGround)
                            {
                                BurningGround.Add(new Puddle { Pos = at, Radius = TankerOilRadius, Life = OilLife, MaxLife = OilLife, Oil = true });
                            }
                        }
                    }
                }
            }
        }

        /// <summary>불배가 노릴 부둣가 건물: 아직 안 타는 것 중 하나, 없으면 아무 부둣가 건물, 그것도 없으면 null(줄 따라 떠간다).</summary>
        private Structure PickQuayTarget()
        {
            var quay = new List<Structure>();
            foreach (Structure s in Structures)
            {
                if (s.IsBuilding && !s.Collapsed && s.Pos.Y > SurvivorHarbor.QuayRow - 4f) quay.Add(s);
            }
            if (quay.Count == 0) return null;
            var calm = quay.FindAll(s => !s.Burning);
            List<Structure> pool = calm.Count > 0 ? calm : quay;
            return pool[Math.Min(pool.Count - 1, (int)(Rand() * pool.Count))];
        }

        /// <summary>대화재 고리의 큰 불과 불 게는 밀치기가 HeavyKnock배만 먹힌다.</summary>
        private static float KnockScale(Enemy e)
        {
            return e.Heavy || e.Kind == EnemyKind.Crab ? HeavyKnock : 1f;
        }

        /// <summary>갈매기·게·풍등: 멀리서도 건물만 노린다.</summary>
        private static bool TargetsBuildings(Enemy e)
        {
            return e.Kind == EnemyKind.Gull || e.Kind == EnemyKind.Crab || e.Kind == EnemyKind.SkyLantern;
        }

        /// <summary>갈매기·게·풍등이 건물을 찾는 거리(맵 어디서든 하나는 보인다).</summary>
        public const float BuildingSight = 40f;

        /// <summary>갈매기가 이번 틱 불을 떨어뜨린 자리 / 풍등이 내려앉은 자리(뷰 신호).</summary>
        public readonly List<Vec2> GullDrops = new List<Vec2>();
        public readonly List<Vec2> LanternLands = new List<Vec2>();

        /// <summary>range 안 가장 가까운 불붙을 수 있는 건물(집·창고).</summary>
        private Structure NearestBuilding(Vec2 p, float range)
        {
            Structure best = null;
            float bestD = range;
            foreach (Structure s in Structures)
            {
                if (!s.IsBuilding || !s.Flammable) continue;
                float d = s.DistanceTo(p);
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }
            return best;
        }

        private Structure NearestFlammable(Vec2 p, float range)
        {
            Structure best = null;
            float bestD = range;
            foreach (Structure s in Structures)
            {
                if (!s.Flammable) continue;
                float d = s.DistanceTo(p);
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }
            return best;
        }

        /// <summary>건물·차·가스통은 소방관이 지나갈 수 없다(나무 밑은 지나간다).</summary>
        private void BlockPlayer()
        {
            foreach (Structure s in Structures)
            {
                if (s.Collapsed || s.Kind == StructureKind.Tree) continue;
                float dx = Player.X - s.Pos.X;
                float dy = Player.Y - s.Pos.Y;
                float ox = s.Half.X + PlayerRadius - Math.Abs(dx);
                float oy = s.Half.Y + PlayerRadius - Math.Abs(dy);
                if (ox <= 0f || oy <= 0f) continue;
                if (ox < oy) Player.X += dx >= 0f ? ox : -ox;
                else Player.Y += dy >= 0f ? oy : -oy;
            }
        }

        /// <summary>타는 구조물이 커지고, 불씨·큰 불을 뱉고, 다 타면 무너진다.</summary>
        private void TickStructures()
        {
            foreach (Structure s in Structures)
            {
                if (s.Collapsed)
                {
                    // 수호자: 무너진 집은 잿더미 둥지 — 판 끝까지 RuinSpitEvery마다 불씨 RuinSpit개. 잃은 만큼 세상이 거칠어진다.
                    if (Guardian && s.IsBuilding)
                    {
                        s.RuinClock -= Dt;
                        if (s.RuinClock <= 0f)
                        {
                            s.RuinClock = RuinSpitEvery;
                            for (int k = 0; k < RuinSpit; k++) SpitEmber(s, 4f);
                            RuinSpat.Add(s);
                        }
                    }
                    continue;
                }
                if (s.Wet > 0f) s.Wet -= Dt;
                if (!s.Burning) continue;

                if (s.Kind == StructureKind.Gas && s.Fuse >= 0f)
                {
                    s.Fuse -= Dt;
                    if (s.Fuse <= 0f)
                    {
                        Blow(s);
                        continue;
                    }
                }
                if (s.Kind == StructureKind.Fireworks)
                {
                    // 퓨즈가 다 타면 로켓을 쏘기 시작한다. 타는 동안 RocketEvery마다 한 발(끄면 Soak이 Launching을 끈다).
                    if (s.Fuse >= 0f)
                    {
                        s.Fuse -= Dt;
                        if (s.Fuse <= 0f)
                        {
                            s.Fuse = -1f;
                            s.Launching = true;
                            s.LaunchClock = 0.3f;
                            JustLaunching = s;
                        }
                    }
                    if (s.Launching)
                    {
                        s.LaunchClock -= Dt;
                        if (s.LaunchClock <= 0f)
                        {
                            s.LaunchClock = RocketEvery;
                            LaunchRocket(s);
                        }
                    }
                }

                // 수호 반경 안: 불이 자라지도, 번지지도, 불씨·큰 불을 뱉지도 못하고 GuardCool만큼 잦아든다(타는 동안 무너짐은 그대로).
                bool held = Guardian && s.DistanceTo(Player) <= GuardRadius;
                if (!held) s.Fire = Math.Min(1f, s.Fire + (Stage.FireGrowth * Dt));
                // 잦아들되 0.02 밑으로는 스스로 꺼지지 않는다(끄는 건 물). 물을 맞아 이미 그 밑이면 끌어올리지 않는다.
                else s.Fire = Math.Max(Math.Min(s.Fire, 0.02f), s.Fire - (GuardCool * Dt));
                s.Integrity -= s.Fire * Dt / (s.IsBuilding ? BurnBuilding * BuildingBurnScale : s.Kind == StructureKind.Boat ? (s.Tanker ? BurnTanker : BurnBoat) : BurnSmall);
                if (s.HoseHold > 0f) s.HoseHold = Math.Max(0f, s.HoseHold - (SteamCool * Dt));
                if (s.Integrity <= 0f)
                {
                    Fall(s);
                    continue;
                }
                // 사람이 갇힌 건물이 곧 무너진다: 건물마다 한 번 알린다(끄면 풀린다).
                if (s.IsBuilding && s.Residents > 0 && !s.Warned && TimeToFall(s) <= CollapseWarnAt)
                {
                    s.Warned = true;
                    CollapseWarnings.Add(s);
                }

                if (held) continue;

                if (Stage.Wind && s.Kind == StructureKind.Tree && s.Fire >= SpreadAt)
                {
                    s.WindClock -= Dt;
                    if (s.WindClock <= 0f)
                    {
                        s.WindClock = WindSpreadEvery;
                        // 반쯤만 옮는다: 늘 옮기면 한 그루가 두세 그루를 태워 숲 전체가 순식간에 탄다.
                        Structure next = Downwind(s);
                        // 대화재 동안 숲은 바람이 거세진다(캠프를 덮치는 산불). 감독 단계마다 더.
                        float chance = Finale ? Stage.FinaleWindChance + (PressureWindStep * FinalePressure) : WindSpreadChance;
                        if (next != null && Rand() < chance) Ignite(next, 0.3f);
                    }
                }

                // 크게 타는 건물은 옆 건물로 직접 옮겨붙는다: 놓친 불 하나가 동네를 번진다.
                if (s.IsBuilding && s.Fire >= SpreadFire && Stage.SpreadEvery > 0f)
                {
                    s.SpreadClock -= Dt;
                    if (s.SpreadClock <= 0f)
                    {
                        s.SpreadClock = Stage.SpreadEvery;
                        Structure next = NextBuilding(s);
                        if (next != null && Ignite(next, SpreadIgnite))
                        {
                            Spread.Add(next);
                            SpreadFrom.Add(s);
                            Stats.Spreads++;
                        }
                    }
                }

                // 막 붙은 작은 불은 아직 번지지 않는다(0.4까지 약 5초) — 일찍 잡으면 막을 수 있다.
                // 나무가 불씨를 뱉는 간격은 스테이지가 정한다(숲은 느리게: 수십 그루가 보통 속도로 뱉으면 불씨 떼가 동네를 덮는다).
                float spit = s.IsBuilding ? 1f : s.Kind == StructureKind.Tree ? Stage.TreeSpit : 2f;
                if (s.Fire >= SpreadAt && spit > 0f) s.SpitClock -= Dt;
                if (s.SpitClock <= 0f)
                {
                    s.SpitClock = (9f - (5f * s.Fire)) * spit;
                    SpitEmber(s, 3f);
                }
                if (s.IsBuilding && s.Fire >= 0.8f)
                {
                    s.BlazeClock -= Dt;
                    if (s.BlazeClock <= 0f && Enemies.Count < MaxEnemies)
                    {
                        s.BlazeClock = 16f;
                        Spawn(EnemyKind.Blaze, EdgePoint(s, 0.8f));
                    }
                }
            }
        }

        /// <summary>가스통 폭발: 둘레 탈 것에 불을 크게 붙이고, 불씨를 튀기고, 가까우면 소방관도 다친다.</summary>
        private void Blow(Structure gas)
        {
            gas.Collapsed = true;
            gas.Fire = 0f;
            gas.Integrity = 0f;
            gas.Fuse = -1f;
            GasBlasts.Add(gas.Pos);
            foreach (Structure st in Structures)
            {
                if (st == gas || st.Collapsed || !st.Within(gas.Pos, GasRadius)) continue;
                if (st.Burning) st.Fire = Math.Min(1f, st.Fire + 0.5f);
                else Ignite(st, 0.5f);
            }
            for (int k = 0; k < 8; k++) SpitEmber(gas, 9f);
            // 공단 약품 드럼은 터지며 기름을 흩뿌린다.
            if (Stage.DrumSpill > 0) Spill(gas.Pos, Stage.DrumSpill);
            if (Player.DistanceTo(gas.Pos) <= GasRadius) Hurt(25f, HurtKind.Blast);
        }

        private void Fall(Structure s)
        {
            s.Collapsed = true;
            s.Fire = 0f;
            s.Integrity = 0f;
            s.Fuse = -1f;
            Fell.Add(s);
            s.RuinClock = RuinSpitEvery;
            if (s.IsBuilding)
            {
                HousesLost++;
                if (s.Residents > 0) PeopleLost.Add(s);
                CiviliansLost += s.Residents;
                s.Residents = 0;
            }
            for (int k = 0; k < 6; k++) SpitEmber(s, 6f);
        }

        /// <summary>구조물 가장자리 바깥에서 불씨 하나를 튕겨 낸다.</summary>
        private void SpitEmber(Structure s, float kick)
        {
            if (Enemies.Count >= MaxEnemies) return;
            Vec2 at = EdgePoint(s, 0.5f);
            Enemy e = Spawn(EnemyKind.Ember, at);
            Vec2 k = Knockback(s.Pos, at, kick);
            // 산불: 튀는 불씨가 바람에 밀린다.
            e.Knock = new Vec2(k.X + (Wind.X * kick * 0.6f), k.Y + (Wind.Y * kick * 0.6f));
            e.GoalClock = 0.4f;
            e.Seeker = true;
        }

        private Vec2 EdgePoint(Structure s, float margin)
        {
            double a = Rand() * Math.PI * 2;
            float cx = (float)Math.Cos(a);
            float cy = (float)Math.Sin(a);
            float scale = Math.Min((s.Half.X + margin) / Math.Max(Math.Abs(cx), 0.001f), (s.Half.Y + margin) / Math.Max(Math.Abs(cy), 0.001f));
            return ClampToArena(new Vec2(s.Pos.X + (cx * scale), s.Pos.Y + (cy * scale)));
        }

        private void TouchPlayer()
        {
            Near(Player, PlayerRadius, _near);
            // 적은 전부 불이다: 방화복이 닿는 피해도 줄이고, 닿은 불 몹을 튕겨 낸다.
            float push = Build.SuitPush;
            foreach (Enemy e in _near)
            {
                // 숲 샘플: 잡힌(거품·방울 속)·하늘로 날아간·샘플 얼음 요괴는 닿아도 안 덴다.
                if (e.Frozen > 0f || e.Captured > 0f || e.Held || e.AirZ > 0f || e.SFrozen > 0f) continue;
                Burn(e.Touch * Dt, HurtKind.Contact);
                if (push <= 0f || e.BounceCool > 0f) continue;
                e.BounceCool = SuitBounceCool;
                Vec2 k = Knockback(Player, e.Pos, push * KnockScale(e));
                e.Knock.X += k.X;
                e.Knock.Y += k.Y;
                SuitBounces.Add(e.Pos);
            }
        }

        /// <summary>방화복에 튕긴 불 몹이 다시 튕기기까지(초).</summary>
        public const float SuitBounceCool = 0.3f;

        /// <summary>이번 틱 열기로 받은 피해(그림용).</summary>
        public float HeatHurt;

        /// <summary>
        /// 열기: 타는 건물 곁(가장자리 2.5칸)에 서 있으면 가장 센 불 하나만큼 초당 피해. 방화복이 줄인다.
        /// 활활 타는 건물에 갇힌 사람은 "먼저 끄고 들어갈지, 몸으로 버티며 바로 들어갈지" 고르게 된다.
        /// </summary>
        private void TickHeat()
        {
            float worst = 0f;
            foreach (Structure s in Structures)
            {
                // 타는 건물은 2.5칸, 타는 나무(산불)는 1.5칸까지 뜨겁다.
                if (!s.Burning || s.Fire <= worst) continue;
                float range = s.IsBuilding ? HeatRange : s.Kind == StructureKind.Tree ? TreeHeatRange : -1f;
                if (range > 0f && s.DistanceTo(Player) <= range) worst = s.Fire;
            }
            HeatHurt = 0f;
            if (worst <= 0f) return;
            // 대화재 감독: 단계마다 열기가 세진다(1 → 1.5 → 2 → 2.5배). 3:00의 장비는 불을 다 끄므로, 서서 끄는 몸이 압력을 받는다.
            float heat = HeatDps * (1f + (PressureHeatStep * FinalePressure));
            HeatHurt = heat * worst * Build.HeatScale * Dt;
            Burn(heat * worst * Dt, HurtKind.Heat);
        }

        /// <summary>불에 데는 피해: 방화복(HeatScale)만큼 덜 받는다. 원값은 통계에 남긴다.</summary>
        private void Burn(float raw, HurtKind kind)
        {
            Stats.FireDamageRaw += raw;
            Hurt(raw * Build.HeatScale, kind);
        }

        private void Hurt(float amount, HurtKind kind)
        {
            Hp -= amount;
            PlayerHurt += amount;
            Stats.DamageTaken += amount;
            Stats.HurtBy[(int)kind] += amount;
        }

        /// <summary>한 틱의 재미 밀도 통계를 쌓는다.</summary>
        private void Measure()
        {
            Stats.MinHpRatio = Math.Min(Stats.MinHpRatio, Math.Max(0f, Hp) / MaxHp);
            if (Finale) Stats.FinaleMinHp = Math.Min(Stats.FinaleMinHp, Math.Max(0f, Hp) / MaxHp);
            bool building = false;
            bool other = false;
            bool fireNear = false;
            foreach (Structure s in Structures)
            {
                if (!s.Burning) continue;
                if (s.IsBuilding) building = true;
                else other = true;
                // 떠가는 불배는 멀어도 "할 일"이다(부두 끝에 서서 쏜다): 바다 줄에서 부두까지(12칸)를 가까운 불로 친다.
                if (!fireNear && s.DistanceTo(Player) <= (s.Kind == StructureKind.Boat && !s.Docked ? 16f : 8f)) fireNear = true;
            }
            if (building) Stats.BuildingFire += Dt;
            else if (other) Stats.TreeFireOnly += Dt;
            if (fireNear) return;
            // 해시(이번 틱 이동 전 칸)로 가까운 적만 본다: 350마리를 매 틱 다 재지 않는다.
            Near(Player, 7f, _near);
            foreach (Enemy e in _near)
            {
                if (!e.Dead) return;
            }
            Stats.IdleTime += Dt;
        }

        private float _toolboxClock = ToolboxEvery;

        private float NextKitWait()
        {
            return KitMin + ((_kitRng.Next(1000) / 1000f) * (KitMax - KitMin));
        }

        /// <summary>구급상자: 무작위 간격(40~80초)마다, 바닥에 없고 다쳤을 때만 소방관 5~10칸 빈 땅에 떨군다. 25초 뒤 사라지고 밟으면 +35.</summary>
        private void TickKits()
        {
            _kitClock -= Dt;
            if (_kitClock <= 0f)
            {
                _kitClock = NextKitWait();
                if (Kits.Count == 0 && Hp < MaxHp * KitBelow)
                {
                    Vec2? at = FreeSpot(5f, 10f);
                    if (at.HasValue) Kits.Add(new Pickup { Pos = at.Value, Life = KitLife });
                }
            }

            for (int i = Kits.Count - 1; i >= 0; i--)
            {
                Pickup kit = Kits[i];
                kit.Life -= Dt;
                if (Player.DistanceTo(kit.Pos) <= PickupRange + PlayerRadius)
                {
                    Kits.RemoveAt(i);
                    Hp = Math.Min(MaxHp, Hp + KitHeal);
                    JustPickedKit = true;
                    Stats.KitsPicked++;
                    Stats.Events++;
                    continue;
                }
                if (kit.Life <= 0f) Kits.RemoveAt(i);
            }
        }

        /// <summary>공구상자: 40초마다 부서진 건물이 있고 떨어진 상자가 없으면 소방관 6~12칸 빈 땅에 떨군다. 20초 뒤 사라진다.</summary>
        private void TickToolboxes()
        {
            _toolboxClock -= Dt;
            if (_toolboxClock <= 0f)
            {
                _toolboxClock = ToolboxEvery;
                if (Toolboxes.Count == 0 && Structures.Exists(st => st.IsBuilding && !st.Collapsed && st.Integrity < DamagedBelow))
                {
                    Vec2? at = FreeSpot(6f, 12f);
                    if (at.HasValue) Toolboxes.Add(new Pickup { Pos = at.Value, Life = ToolboxLife });
                }
            }

            for (int i = Toolboxes.Count - 1; i >= 0; i--)
            {
                Pickup box = Toolboxes[i];
                box.Life -= Dt;
                if (Player.DistanceTo(box.Pos) <= PickupRange + PlayerRadius)
                {
                    Toolboxes.RemoveAt(i);
                    UseToolbox();
                    continue;
                }
                if (box.Life <= 0f) Toolboxes.RemoveAt(i);
            }
        }

        /// <summary>
        /// 대형 신고가 끝났는지 보고(모두 구했으면 문 앞에 상자), 상자를 줍게 한다.
        /// 연기로 한 명이라도 잃었거나 건물이 무너지면 상자는 없다.
        /// </summary>
        private void TickChests()
        {
            if (BigReport != null)
            {
                if (BigReport.Collapsed)
                {
                    BigReport = null;
                }
                else if (BigReport.Residents <= 0 || !BigReport.Burning)
                {
                    // 다 구했거나 불을 다 꺼서 안의 사람이 안전해졌다. 한 명도 잃지 않았으면 상자.
                    if (!_bigFailed) Chests.Add(new Pickup { Pos = BigReport.Door, Life = ChestLife });
                    BigReport = null;
                }
            }

            for (int i = Chests.Count - 1; i >= 0; i--)
            {
                Pickup chest = Chests[i];
                chest.Life -= Dt;
                if (Player.DistanceTo(chest.Pos) <= PickupRange + PlayerRadius)
                {
                    Chests.RemoveAt(i);
                    JustChest = true;
                    Stats.Events++;
                    _bonusPicks = RollChestPicks();
                    LastChestPicks = _bonusPicks;
                    // 레벨업 카드를 고르는 중이면 그걸 고른 뒤에 이어서 연다(Choose).
                    if (PendingChoices == null) OpenBonusPick();
                    continue;
                }
                if (chest.Life <= 0f) Chests.RemoveAt(i);
            }
        }

        /// <summary>가장 많이 부서진 건물(타는 곳 먼저)을 고치고 불을 줄이고 적신다. 고칠 곳이 없으면 체력을 채운다.</summary>
        private void UseToolbox()
        {
            JustPickedToolbox = true;
            Structure worst = null;
            foreach (Structure st in Structures)
            {
                if (!st.IsBuilding || st.Collapsed || st.Integrity >= 1f) continue;
                if (worst == null || (st.Burning && !worst.Burning) || (st.Burning == worst.Burning && st.Integrity < worst.Integrity)) worst = st;
            }
            if (worst == null)
            {
                Hp = Math.Min(MaxHp, Hp + ToolboxHeal);
                return;
            }
            worst.Integrity = Math.Min(1f, worst.Integrity + ToolboxRepair);
            if (worst.Burning)
            {
                worst.Fire = Math.Max(0f, worst.Fire - ToolboxRepair);
                if (worst.Fire <= 0f) Doused.Add(worst);
            }
            worst.Wet = Math.Max(worst.Wet, 6f);
            Repaired.Add(worst);
        }

        /// <summary>소방관에게서 min~max칸, 구조물과 겹치지 않는 자리. 못 찾으면 null.</summary>
        private Vec2? FreeSpot(float min, float max)
        {
            for (int tries = 0; tries < 24; tries++)
            {
                double a = Rand() * Math.PI * 2;
                float d = min + (Rand() * (max - min));
                Vec2 p = ClampToArena(new Vec2(Player.X + (float)(Math.Cos(a) * d), Player.Y + (float)(Math.Sin(a) * d)));
                if (p.DistanceTo(Player) < min * 0.8f) continue;
                if (!Structures.Exists(st => !st.Collapsed && st.Within(p, 0.8f))) return p;
            }
            return null;
        }

        private void CollectGems()
        {
            float magnet = Magnet;
            foreach (Gem g in Gems)
            {
                float d = g.Pos.DistanceTo(Player);
                if (!g.Pulled && d <= magnet)
                {
                    g.Pulled = true;
                    g.Speed = 3f;
                }
                if (!g.Pulled) continue;

                g.Speed += 40f * Dt;
                float step = Math.Min(d, g.Speed * Dt);
                if (d > 0.001f)
                {
                    g.Pos.X += (Player.X - g.Pos.X) / d * step;
                    g.Pos.Y += (Player.Y - g.Pos.Y) / d * step;
                }
                if (g.Pos.DistanceTo(Player) <= 0.45f)
                {
                    Xp += g.Value;
                    g.Value = 0;
                    GemsCollected++;
                }
            }
        }

        /// <summary>
        /// 불난 가게 문 앞에 잠깐 서 있으면 갇힌 사람을 한 명씩 데리고 나온다.
        /// 나온 사람은 잠깐 뛰어 나가는 모습으로만 남는다(Civilians는 화면용).
        /// </summary>
        private void TickRescue()
        {
            foreach (Structure s in Structures)
            {
                // 큰 불 속에 오래 갇혀 있으면 연기에 한 명씩 잃는다: 멀리서 끄기만 할 게 아니라 빨리 가야 한다.
                if (s.Burning && s.Residents > 0 && s.Fire >= SmokeFire)
                {
                    s.Smoke += Dt;
                    if (s.Smoke >= Stage.SmokeTime)
                    {
                        s.Smoke = 0f;
                        s.Residents--;
                        CiviliansLost++;
                        PeopleLost.Add(s);
                        if (s == BigReport) _bigFailed = true;
                    }
                }

                bool player = s.Door.DistanceTo(Player) <= RescueRange;
                if (!s.Burning || s.Residents <= 0 || !player)
                {
                    s.RescueHold = 0f;
                    continue;
                }
                s.RescueHold += Dt;
                if (s.RescueHold < RescueTime) continue;
                s.RescueHold = 0f;
                RescueOne(s);
            }

            foreach (Civilian c in Civilians) c.Life -= Dt;
        }

        /// <summary>갇힌 사람 한 명을 데리고 나온다(문 앞 구조·구조 드론).</summary>
        /// <summary>이대로 타면 몇 초 뒤 무너지나(안 타면 무한). 불이 아직 크는 중이면 위쪽 어림이다.</summary>
        public float TimeToFall(Structure s)
        {
            if (s.Collapsed || s.Fire <= 0f) return float.PositiveInfinity;
            return s.Integrity * (s.IsBuilding ? BurnBuilding : BurnSmall) / s.Fire;
        }

        /// <summary>
        /// 이 건물을 언제까지 꺼야 하나(초). 사람이 있으면 첫 사람을 연기로 잃기까지(연기가 아직이면 연기 시작까지 + 첫 사람),
        /// 없으면 무너지기까지. 둘 중 빠른 쪽. 안 타면 무한. 화면의 마감 게이지가 쓴다.
        /// </summary>
        public float Deadline(Structure s)
        {
            float fall = TimeToFall(s);
            if (s.Collapsed || !s.Burning || s.Residents <= 0) return fall;
            float smoke = s.Fire >= SmokeFire ? SmokeTime - s.Smoke : ((SmokeFire - s.Fire) / Stage.FireGrowth) + SmokeTime;
            return Math.Min(fall, Math.Max(0f, smoke));
        }

        /// <summary>갇힌 사람을 데리고 나온다(문 앞 구조·구조 드론). 구조 도끼가 있으면 문을 부수고 한 번에 여럿.</summary>
        private void RescueOne(Structure s)
        {
            int n = 1;
            if (n <= 0) return;
            s.Residents -= n;
            Rescued += n;
            Xp += 20 * n;
            // 무너지기 직전의 구조는 더 값지다.
            if (TimeToFall(s) <= CloseCallAt)
            {
                Xp += CloseCallXp;
                CloseCalls.Add(s);
                Stats.CloseCalls++;
            }
            // 대화재의 절정: 랜드마크에 갇힌 사람을 다 구했다.
            if (Finale && s == Landmark && s.Residents == 0)
            {
                Xp += LandmarkXp;
                JustLandmarkSaved = true;
                Stats.Events++;
            }
            float heal = RescueHeal;
            Stats.HealRescue += Math.Min(MaxHp, Hp + heal) - Hp;
            Hp = Math.Min(MaxHp, Hp + heal);
            JustRescued = true;
            Stats.Events++;
            RescuedFrom.Add(s);
            // 숲: 구한 사람이 방화복 대원이 되어 뒤에 줄을 선다(8명까지). 다 찼으면 예전처럼 뛰어 나간다.
            if (JoinCrew(s.Door)) return;
            for (int k = 0; k < n; k++) Civilians.Add(new Civilian { Pos = new Vec2(s.Door.X + ((k - ((n - 1) / 2f)) * 0.5f), s.Door.Y), Life = 1.5f });
        }

        /// <summary>
        /// 숲: 꺼진 집의 갇힌 사람이 스스로 나온다. 구조로 세고 대원이 되지만, 문 앞 구조(RescueOne)와 달리 경험치·체력은 안 준다 —
        /// 대화재 신고마다 주민이 하나씩 늘어 한 판에 44명이 나오며 체력 220·경험치 880을 주니 숲이 싱거워졌다(아슬 14 → 3, docs §24).
        /// 랜드마크(제재소)를 다 비우는 절정 보상만 그대로.
        /// </summary>
        private void ReleaseResidents(Structure s)
        {
            int n = s.Residents;
            if (n <= 0) return;
            s.Residents = 0;
            Rescued += n;
            JustRescued = true;
            RescuedFrom.Add(s);
            if (Finale && s == Landmark)
            {
                Xp += LandmarkXp;
                JustLandmarkSaved = true;
                Stats.Events++;
            }
            for (int k = 0; k < n; k++)
            {
                if (JoinCrew(s.Door)) continue;
                Civilians.Add(new Civilian { Pos = new Vec2(s.Door.X + ((k - ((n - 1) / 2f)) * 0.5f), s.Door.Y), Life = 1.5f });
            }
        }

        private void Damage(Enemy e, float amount, Vec2 knock, bool show, HitSource source = HitSource.Hose, Vec2 from = default)
        {
            if (e.Dead) return;
            bool crit = Rand() < 0.1f;
            if (crit) amount *= 2f;
            e.Hp -= amount;
            e.HitFlash = 0.08f;
            // 도깨비는 맞으면 던지려던 횃불을 놓친다(예고가 처음부터).
            if (e.Kind == EnemyKind.Goblin) e.Phase = 0f;
            float heavy = KnockScale(e);
            e.Knock.X += knock.X * heavy;
            e.Knock.Y += knock.Y * heavy;

            bool killed = e.Hp <= 0f;
            if (killed) Kill(e);
            if (killed && Build.Free) SampleKilled(e);
            if (show || killed) Hits.Add(new Hit { Pos = e.Pos, Damage = amount, Crit = crit, Killed = killed, Kind = e.Kind, Source = source, From = from });
        }

        /// <summary>이어진 처치·진화: 콤보를 올리고 배율이 오르면 알린다.</summary>
        private void ComboAdd(int n)
        {
            int before = ComboMult;
            Combo += n;
            ComboClock = ComboWindow;
            if (Combo > Stats.MaxCombo) Stats.MaxCombo = Combo;
            if (ComboMult > before) JustComboTier = true;
        }

        /// <summary>적을 잡는다(테스트도 쓴다): 콤보를 잇고 배율만큼 큰 구슬을 떨군다.</summary>
        public void Kill(Enemy e)
        {
            if (e.Dead) return;
            e.Dead = true;
            Kills++;
            ComboAdd(1);
            DropGem(e.Pos, e.Xp * ComboMult);
            if (IsRaider(e.Kind)) RaiderKilled(e);
            if (e.Kind == EnemyKind.Blaze)
            {
                BurningGround.Add(new Puddle { Pos = e.Pos, Radius = 0.9f, Life = 3f, MaxLife = 3f });
            }
            // 기름 방울은 터지며 기름을 튀긴다: 어디서 잡느냐가 중요하다.
            if (e.Kind == EnemyKind.Oil) Spill(e.Pos, OilDeathSpill);
            // 폭죽은 터지며 불씨를 튀긴다(탈 것을 노린다): 점포 곁에서 잡지 마라.
            // Kill은 적 목록을 도는 중(증기 폭발·장막)에도 불리므로 여기서 스폰하지 않고 틱 끝에 FlushPops가 튀긴다.
            if (e.Kind == EnemyKind.Popper) _pops.Add(e.Pos);
        }

        private readonly List<Vec2> _pops = new List<Vec2>();

        /// <summary>이번 틱에 터진 폭죽마다 Seeker 불씨 PopperEmbers개(적 목록을 아무도 돌지 않는 때).</summary>
        private void FlushPops()
        {
            if (_pops.Count == 0) return;
            foreach (Vec2 pos in _pops)
            {
                for (int k = 0; k < PopperEmbers && Enemies.Count < MaxEnemies; k++)
                {
                    var at = new Vec2(pos.X + (k == 0 ? -0.4f : 0.4f), pos.Y);
                    Enemy ember = Spawn(EnemyKind.Ember, at);
                    ember.Seeker = true;
                    ember.GoalClock = 0.4f;
                    ember.Knock = Knockback(pos, at, 4f);
                }
            }
            _pops.Clear();
        }

        /// <summary>물이 닿은 자리의 바닥 불을 끈다(샷의 관통 수는 쓰지 않는다).</summary>
        private void Douse(Vec2 at, float radius)
        {
            foreach (Puddle p in BurningGround)
            {
                if (p.Out || p.Life <= 0f) continue;
                if (p.Pos.DistanceTo(at) > radius + p.Radius) continue;
                p.Out = true;
                p.Life = 0f;
                Extinguished.Add(p.Pos);
            }
        }

        private static Vec2 Knockback(Vec2 from, Vec2 to, float strength)
        {
            float dx = to.X - from.X;
            float dy = to.Y - from.Y;
            float d = (float)Math.Sqrt((dx * dx) + (dy * dy));
            if (d < 0.01f) return default;
            return new Vec2(dx / d * strength, dy / d * strength);
        }

        private void PushAway(Vec2 from, float radius, float strength)
        {
            foreach (Enemy e in Enemies)
            {
                if (e.Dead) continue;
                float d = e.Pos.DistanceTo(from);
                if (d > radius) continue;
                Vec2 k = Knockback(from, e.Pos, strength * 4f);
                e.Knock.X += k.X;
                e.Knock.Y += k.Y;
            }
        }

        private Enemy Nearest(Vec2 p, float range)
        {
            Enemy best = null;
            float bestD = range;
            foreach (Enemy e in Enemies)
            {
                if (e.Dead) continue;
                float d = e.Pos.DistanceTo(p);
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
        }

        private Vec2? RandomEnemyNear(float range)
        {
            int seen = 0;
            Vec2? pick = null;
            foreach (Enemy e in Enemies)
            {
                if (e.Dead || e.Pos.DistanceTo(Player) > range) continue;
                seen++;
                if (_rng.Next(seen) == 0) pick = e.Pos;
            }
            return pick;
        }

        private void Sweep()
        {
            Enemies.RemoveAll(e => e.Dead);
            Shots.RemoveAll(s => s.Dead);
            Gems.RemoveAll(g => g.Value == 0);
            // 끄지 않은 채 다 탄 바닥 불은 확률로 새 불씨를 일으킨다(불이 번진다). 물로 끈 자리는 Out이라 번지지 않는다.
            foreach (Puddle p in BurningGround)
            {
                if (p.Life > 0f || p.Out) continue;
                if (Rand() < ReigniteChance && Enemies.Count < MaxEnemies)
                {
                    Spawn(EnemyKind.Ember, p.Pos);
                    Reignited.Add(p.Pos);
                }
            }
            BurningGround.RemoveAll(p => p.Life <= 0f);
            Civilians.RemoveAll(c => c.Life <= 0f);
        }

        private static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }
    }
}
