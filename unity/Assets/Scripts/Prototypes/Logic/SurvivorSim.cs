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
    }

    public enum ShotKind
    {
        Drop,
        Bomb,
        Jet,

        /// <summary>소방 헬기가 쏟는 물. 폭탄처럼 날아가 떨어지지만 훨씬 크다.</summary>
        Heli,
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
        public float DroneCooldown;

        /// <summary>방화복에 튕긴 뒤 다시 튕기기까지(초).</summary>
        public float BounceCool;
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

        /// <summary>지그재그·출렁임 위상(다람쥐·박쥐).</summary>
        public float Phase;
        public bool Dead;
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

        /// <summary>공중 소화탄(진화): 하늘에서 떨어진다.</summary>
        public bool Air;

        /// <summary>순찰 드론이 급강하해 떨어뜨린 물폭탄. 저항 없이 DroneDropWater만큼 끈다.</summary>
        public bool Drone;

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

    /// <summary>맵에 떨어진 공구상자. 주우면 가장 약한 건물을 고친다(일회성).</summary>
    /// <summary>방수 포탑: 선 자리에서 몇 초 동안 곁 불 몹과 건물에 물을 쏜다.</summary>
    public sealed class Turret
    {
        public Vec2 Pos;
        public float Life;
        public float MaxLife;
        public float Clock;

        /// <summary>지금 쏘는 곳(그림용). 없으면 null.</summary>
        public Vec2? Aim;
    }

    public sealed class Pickup
    {
        public Vec2 Pos;
        public float Life;
    }

    /// <summary>땅에 뿌려진 띠(방염제). A에서 B까지 폭만큼.</summary>
    public sealed class Band
    {
        public Vec2 A;
        public Vec2 B;
        public float Life;
        public float MaxLife;
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
        Bomb,
        Drone,
        Partner,
        Curtain,
        Turret,
        Special,

        /// <summary>증기 폭발: 건물 불에 물줄기를 이어 맞혀 터진 김.</summary>
        Steam,
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

    /// <summary>재미 밀도 계측: 봇 판을 스테이지끼리 비교한다(docs/prototype-c-balance.md).</summary>
    public sealed class RunStats
    {
        /// <summary>레벨업한 시각들.</summary>
        public readonly List<float> LevelTimes = new List<float>();

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
    }

    /// <summary>
    /// 시험판 C(뱀서라이크) 규칙. 60Hz 고정 스텝, 시드 Rng로 결정적이다.
    /// 소방관은 움직이기만 하고 무기는 알아서 쏜다. 불 괴물을 끄면 구슬이 떨어지고, 구슬이 모이면 카드 3장 중 하나를 고른다.
    /// 1:20·2:40에 대형 신고, 3:00부터 대화재. 4:00까지 동네를 절반 넘게 지키면 이기고, 체력이 0이 되거나 동네를 잃으면 진다.
    /// </summary>
    public sealed class SurvivorSim
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

        /// <summary>이번 틱에 랜드마크가 불씨를 뿜었다.</summary>
        public bool JustBurst;

        /// <summary>대형 신고: 큰 불에 여럿이 갇힌다. 다 구하면 보물상자.</summary>
        public static readonly float[] BigReportTimes = { 80f, 160f };
        public const float BigReportFire = 0.7f;
        public const int BigReportPeople = 3;
        public const float ChestLife = 30f;

        /// <summary>보물상자 하나로 고르는 카드 수(첫 장은 노란 카드).</summary>
        public const int ChestPicks = 2;
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
        public readonly List<Vec2> Drones = new List<Vec2>();

        /// <summary>구조대원들이 서 있는 곳(레벨에 따라 1~3명, 구조 분대는 4명).</summary>
        public readonly List<Vec2> Partners = new List<Vec2>();

        /// <summary>순찰 드론이 도는 가운데와 노리는 건물(없으면 소방관 곁을 돈다).</summary>
        public Vec2 DroneCenter = new Vec2(ArenaSize / 2f, ArenaSize / 2f);
        public Structure DroneTarget;
        public readonly List<Turret> Turrets = new List<Turret>();

        /// <summary>구급차가 연기를 걷어 낸 건물(이번 틱 신호).</summary>
        public Structure AmbulanceAt;

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
        /// <summary>지금 진행 중인 대형 신고 건물(없으면 null).</summary>
        public Structure BigReport;

        /// <summary>대화재 건물(물류창고·제재소). 3:00 전에는 null.</summary>
        public Structure Landmark;
        public bool Finale;

        /// <summary>대형 신고를 다 구하면 문 앞에 떨어지는 보물상자.</summary>
        public readonly List<Pickup> Chests = new List<Pickup>();
        public bool JustBigReport;
        public bool JustFinale;
        public bool JustChest;

        /// <summary>지금 고르는 카드가 보물상자 카드인가(레벨업 카드가 아니라). 화면 제목용.</summary>
        public bool ChoosingChest;
        public SOutcome Outcome;

        /// <summary>null이 아니면 레벨업 카드를 고르는 중이다. 이때 Step은 시간을 멈춘다.</summary>
        public List<UpgradeId> PendingChoices;

        // --- 한 틱 신호(화면·소리용) ---
        public readonly List<Hit> Hits = new List<Hit>();
        public readonly List<Vec2> Explosions = new List<Vec2>();

        /// <summary>이번 틱에 순찰 드론의 물폭탄이 떨어진 자리.</summary>
        public readonly List<Vec2> DroneDrops = new List<Vec2>();

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

        /// <summary>물 1 피해가 건물 불 세기를 줄이는 양. 물대포 Lv1이면 다 탄 가게를 약 3초에 끈다.</summary>
        public const float WaterPerDamage = 0.035f;
        /// <summary>건물 불이 초당 커지는 양. 신고 불(0.5)이 약 8초면 다 탄다.</summary>
        public const float FireGrowth = 0.04f;

        /// <summary>끈 자리가 젖어 있는 시간. 짧아서 끄고 떠나면 금방 다시 탈 수 있다.</summary>
        public const float WetTime = 8f;

        /// <summary>큰 불일수록 물이 덜 먹힌다: 물 효과 = 1 − FireResist × 불 세기(0.3이면 83%, 1.0이면 45%).</summary>
        public const float FireResist = 0.55f;

        /// <summary>물대포 한 방울이 불 몹을 미는 힘(예전 2.5). 큰 불·기름 방울은 무거워서 절반.</summary>
        public const float HoseKnock = 4f;

        /// <summary>건물 불에 물대포 물을 이만큼(초 분량) 맞히면 증기 폭발. 저항은 이렇게 뚫는다.</summary>
        public const float SteamHold = 2f;

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
        public const float RescueTime = 1.2f;

        /// <summary>큰 불(이 세기 이상) 속에 갇힌 사람은 SmokeTime마다 한 명씩 잃는다.</summary>
        public const float SmokeFire = 0.6f;
        public const float SmokeTime = 15f;

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

        /// <summary>이번 틱에 헬기 물이 떨어진 자리.</summary>
        public readonly List<Vec2> HeliDrops = new List<Vec2>();

        /// <summary>이번 틱에 물의 장막이 터졌다(소방관 자리에서).</summary>
        public bool JustCurtain;

        /// <summary>이번 틱에 방수 포탑을 세운 자리.</summary>
        public readonly List<Vec2> TurretsPlaced = new List<Vec2>();

        /// <summary>이번 틱에 공중 소화탄이 떨어진 자리(일반 물폭탄은 Explosions).</summary>
        public readonly List<Vec2> AirBlasts = new List<Vec2>();

        /// <summary>물의 장막이 다음에 터지기까지 남은 초(없으면 MaxValue). 화면이 발밑 빛을 채운다.</summary>
        public float CurtainIn
        {
            get { return Build.Has(UpgradeId.Curtain) ? _curtainClock : float.MaxValue; }
        }

        /// <summary>달리고 있는 소방차 자리(없으면 null)와 달리는 방향(±x).</summary>
        public Vec2? Truck;
        public float TruckDir = 1f;

        /// <summary>이번 틱에 스프링클러가 터진 건물.</summary>
        public readonly List<Structure> Sprinkled = new List<Structure>();

        /// <summary>비가 오는 자리(없으면 null)와 남은 시간.</summary>
        public Vec2? RainAt;
        public float RainLeft;

        /// <summary>이번 틱에 먹구름이 새로 왔다.</summary>
        public bool JustRain;

        /// <summary>뿌려진 방염제 띠(A→B, 폭 RetardantWidth). 수명이 다하면 사라진다.</summary>
        public readonly List<Band> Retardants = new List<Band>();

        /// <summary>이번 틱에 방염제 띠가 새로 뿌려졌다.</summary>
        public bool JustRetardant;

        /// <summary>폼 깔개 자리(없으면 null)와 남은 시간. 그 안에 생기는 기름 불은 바로 꺼진다.</summary>
        public Vec2? FoamAt;
        public float FoamLeft;

        /// <summary>이번 틱에 폼이 새로 깔렸다.</summary>
        public bool JustFoam;
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
        private float _bombClock;
        private float _turretClock = 1f;
        private float _airClock;
        private float _ambulanceClock = 5f;
        private float[] _partnerClocks = new float[4];
        private float _jetClock;
        private float _heliClock;
        private float _curtainClock;
        private int _jetQueue;
        private float _jetAngle;
        private float _droneAngle;
        private float _droneDrop;
        private int _droneTurn;
        private int _reportsDone;
        private int _bigDone;
        private bool _bigFailed;
        private float _finaleClock;
        private float _burstClock;
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
            Structures = Stage.Map();
            _rng = new Rng(seed == 0 ? 1 : seed);
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
            Build.MaxAll(Stage.Specials);
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
            RebuildHash();
            MoveEnemies();
            FireWeapons();
            MoveShots();
            TickPuddles();
            TickStructures();
            TouchPlayer();
            TickHeat();
            CollectGems();
            TickToolboxes();
            TickRescue();
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
            if (houses > 0 && HousesLost * 2 > houses)
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
                PendingChoices = SurvivorUpgrades.Roll(Build, Level, ref _rng, Stage.Specials);
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

        /// <summary>보물상자 카드 한 번. 상자의 첫 장은 노란 카드를 보장한다.</summary>
        private void OpenBonusPick()
        {
            bool special = _bonusPicks == ChestPicks;
            _bonusPicks--;
            ChoosingChest = true;
            PendingChoices = SurvivorUpgrades.Roll(Build, Level, ref _rng, Stage.Specials, special);
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
            if (!Loadout.IsSpecial(id) && had < Loadout.MaxLevel && Build.Level(id) == Loadout.MaxLevel) JustMaxed = id;
            if (Loadout.IsEvolution(id)) JustEvolved = true;
            if (id == UpgradeId.Cannon) _jetClock = 0f;
            if (id == UpgradeId.Heli) _heliClock = 1f;
            if (id == UpgradeId.Curtain || id == UpgradeId.WaterWall) _curtainClock = 0.5f;
            if (id == UpgradeId.Turret) _turretClock = 0.3f;
        }

        /// <summary>테스트용: 적을 직접 놓는다.</summary>
        public Enemy Spawn(EnemyKind kind, Vec2 at)
        {
            var e = new Enemy { Kind = kind, Pos = at };
            float scale = Stage.EnemyHp * (1f + ((Time / 120f) * (Time / 120f)));
            switch (kind)
            {
                case EnemyKind.Ember: e.MaxHp = 2f * scale; e.Speed = 2.4f; e.Radius = 0.35f; e.Touch = 3f; e.Xp = 1; break;
                case EnemyKind.Blaze: e.MaxHp = 14f * scale; e.Speed = 1.5f; e.Radius = 0.6f; e.Touch = 10f; e.Xp = 5; break;
                case EnemyKind.Dart: e.MaxHp = 2f * scale; e.Speed = 4.2f; e.Radius = 0.3f; e.Touch = 3f; e.Xp = 1; break;
                case EnemyKind.Squirrel: e.MaxHp = 3f * scale; e.Speed = 3.6f; e.Radius = 0.3f; e.Touch = 3f; e.Xp = 1; e.Seeker = true; break;
                case EnemyKind.Bat: e.MaxHp = 1.5f * scale; e.Speed = 3.2f; e.Radius = 0.3f; e.Touch = 3f; e.Xp = 1; break;
                case EnemyKind.Oil: e.MaxHp = 6f * scale; e.Speed = 1.3f; e.Radius = 0.55f; e.Touch = 10f; e.Xp = 3; break;
            }
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
            Hits.Clear();
            Explosions.Clear();
            DroneDrops.Clear();
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
            HeliDrops.Clear();
            JustCurtain = false;
            TurretsPlaced.Clear();
            AirBlasts.Clear();
            Sprinkled.Clear();
            JustRain = false;
            JustRetardant = false;
            JustFoam = false;
            GasBlasts.Clear();
            RescuedFrom.Clear();
            PeopleLost.Clear();
            JustLeveled = false;
            JustEvolved = false;
            JustMaxed = null;
            Repaired.Clear();
            JustPickedToolbox = false;
            JustBigReport = false;
            JustFinale = false;
            JustBurst = false;
            JustChest = false;
            AmbulanceAt = null;
            JustRescued = false;
            JustWave = false;
            JustComboTier = false;
            ComboEnded = 0;
            JustWindShift = false;
            JustBats = false;
            GemsCollected = 0;
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
                Spawn(PickKind(), SpawnPoint(SpawnDistance));
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
                // 바뀔 때는 늘 다른 방향으로(여덟 방향 중 지금 것 빼고).
                _windIndex = first ? _rng.Next(8) : (_windIndex + 1 + _rng.Next(7)) % 8;
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

            while (Reports && _bigDone < BigReportTimes.Length && Time >= BigReportTimes[_bigDone])
            {
                _bigDone++;
                StartBigReport();
            }

            if (Reports && !Finale && Time >= FinaleAt) StartFinale();
            if (Finale)
            {
                // 대화재: 8초마다 신고가 들어오고, 그 가게엔 한 명이 더 갇힌다.
                _finaleClock -= Dt;
                if (_finaleClock <= 0f)
                {
                    _finaleClock = FinaleReportEvery;
                    Structure hit = Report();
                    if (hit != null) hit.Residents++;
                    Stats.Events++;
                }
                // 불타는 랜드마크가 사방으로 불씨를 뿜는다(예전 보스가 하던 절정의 몸 압박).
                _burstClock -= Dt;
                if (_burstClock <= 0f && Landmark != null && Landmark.Burning)
                {
                    _burstClock = FinaleBurstEvery;
                    for (int k = 0; k < FinaleBurst && Enemies.Count < MaxEnemies; k++)
                    {
                        double a = Math.PI * 2 * k / FinaleBurst;
                        Vec2 at = EdgePoint(Landmark, 0.5f);
                        Enemy e = Spawn(EnemyKind.Ember, at);
                        e.Knock = new Vec2((float)Math.Cos(a) * 6f, (float)Math.Sin(a) * 6f);
                    }
                    JustBurst = true;
                }
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
            _finaleClock = FinaleReportEvery;
            Stats.Events++;
            Structure mark = Structures.Find(x => x.Kind == StructureKind.Depot && !x.Collapsed) ?? PickUnburntHouse();
            if (mark == null) return;
            Landmark = mark;
            mark.Wet = 0f;
            Ignite(mark, 1f);
            mark.Residents += FinalePeople;
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
        private Structure Report()
        {
            Structure pick = PickUnburntHouse();
            if (pick == null) return null;
            pick.Wet = 0f;
            Ignite(pick, ReportFire);
            return pick;
        }

        private EnemyKind PickKind()
        {
            float r = Rand();
            float blaze = Math.Min(Stage.BlazeMax, 0.05f + (Time / 600f));
            float dart = Time < 60f ? 0f : Stage.DartShare;
            if (r < blaze) return EnemyKind.Blaze;
            if (r < blaze + dart) return EnemyKind.Dart;
            if (r < blaze + dart + Stage.SquirrelShare) return EnemyKind.Squirrel;
            if (Time >= OilFrom && r < blaze + dart + Stage.SquirrelShare + Stage.OilShare) return EnemyKind.Oil;
            return EnemyKind.Ember;
        }

        private Vec2 SpawnPoint(float distance)
        {
            double a = Rand() * Math.PI * 2;
            var at = new Vec2(Player.X + (float)(Math.Cos(a) * distance), Player.Y + (float)(Math.Sin(a) * distance));
            return ClampToArena(at);
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
                if (e.DroneCooldown > 0f) e.DroneCooldown -= Dt;
                if (e.BounceCool > 0f) e.BounceCool -= Dt;
                if (e.Slowed > 0f) e.Slowed -= Dt;

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
                    e.GoalClock -= Dt;
                    if (e.Goal != null && !e.Goal.Flammable) e.Goal = null;
                    if (e.Goal == null && e.GoalClock <= 0f)
                    {
                        e.GoalClock = 0.5f;
                        e.Goal = NearestFlammable(e.Pos, EmberSight);
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
                float dx = chase.X - e.Pos.X;
                float dy = chase.Y - e.Pos.Y;
                float d = (float)Math.Sqrt((dx * dx) + (dy * dy));
                float speed = e.Speed * (e.Slowed > 0f ? 0.5f : 1f);
                float vx = d > 0.01f ? dx / d * speed : 0f;
                float vy = d > 0.01f ? dy / d * speed : 0f;
                if (e.Kind == EnemyKind.Squirrel || e.Kind == EnemyKind.Bat)
                {
                    // 다람쥐는 지그재그로, 박쥐는 크게 출렁이며 온다(진행 방향에 수직으로 흔든다).
                    e.Phase += Dt * (e.Kind == EnemyKind.Squirrel ? 9f : 5f);
                    float sway = (float)Math.Sin(e.Phase) * (e.Kind == EnemyKind.Squirrel ? 0.9f : 1.3f);
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
                // 건물에서 나온 불씨와 다람쥐만 옮겨붙인다. 소방관을 쫓는 불(가장자리 불씨·큰 불)은 발밑에 불을 흘릴 뿐이다.
                if (e.Seeker) TouchStructures(e);
                e.Knock.X *= knockDecay;
                e.Knock.Y *= knockDecay;
            }
        }

        private void FireWeapons()
        {
            // 물대포: 겨눈 쪽으로, 쥐고 있을 때만. 예전 자동 조준(0.32초)과 초당 피해를 맞췄다.
            int hose = Build.Level(UpgradeId.Hose);
            bool cannon = Build.Level(UpgradeId.Cannon) > 0;
            _hoseClock -= Dt;
            if (Spraying && (hose > 0 || cannon) && _hoseClock <= 0f && (Aim.X != 0f || Aim.Y != 0f))
            {
                _hoseClock = HoseInterval;
                float baseAngle = (float)Math.Atan2(Aim.Y, Aim.X);
                if (cannon)
                {
                    // 진화 후: 한 줄기로 모든 불을 꿰뚫는 고압 제트.
                    FireDrop(baseAngle, 13.2f * (HoseInterval / 0.4f) * Build.HosePower, 18f, 0.55f, 999, 0.6f * Build.HoseRange, ShotKind.Jet, true);
                }
                else
                {
                    // 늘 한 줄기. 레벨이 오를수록 굵고(반경) 세고(피해) 멀리(수명) 나가며 더 많이 꿰뚫는다.
                    float damage = 3f * (HoseInterval / 0.32f) * HosePower(hose) * Build.HosePower;
                    FireDrop(baseAngle, damage, 16f, HoseRadius(hose), 1 + hose, 0.6f * (1f + (0.1f * (hose - 1))) * Build.HoseRange, ShotKind.Drop, true);
                }
            }

            if (Build.Level(UpgradeId.Cannon) > 0)
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

            int bomb = Build.PowerOf(UpgradeId.WaterBomb);
            if (bomb > 0)
            {
                _bombClock -= Dt;
                if (_bombClock <= 0f)
                {
                    _bombClock = 2.2f;
                    float radius = Build.BombRadius;
                    for (int k = 0; k < bomb; k++)
                    {
                        // 불난 건물을 먼저(센 불부터, 폭탄마다 다른 건물), 없으면 불 떼 한가운데.
                        Structure building = BombBuilding(k);
                        Vec2 target = building != null ? building.Pos : RandomEnemyNear(10f) ?? new Vec2(Player.X + ((Rand() - 0.5f) * 8f), Player.Y + ((Rand() - 0.5f) * 8f));
                        Shots.Add(new Shot { Kind = ShotKind.Bomb, From = Player, Pos = Player, Target = target, Life = 0.5f, Damage = 9f, Radius = radius });
                        ShotsFired++;
                    }
                }
            }

            TickDrones();
            TickAirBombs();
            TickTurrets();
            if (Build.Level(UpgradeId.Ambulance) > 0) TickAmbulance();

            if (Build.Level(UpgradeId.Heli) > 0)
            {
                _heliClock -= Dt;
                if (_heliClock <= 0f)
                {
                    _heliClock = HeliInterval;
                    Vec2 target = HeliTarget();
                    // 헬기는 소방관 뒤쪽 화면 밖에서 날아온다(From은 그림용).
                    var from = new Vec2(target.X - 14f, target.Y + 10f);
                    Shots.Add(new Shot { Kind = ShotKind.Heli, From = from, Pos = from, Target = target, Life = HeliFlight, Damage = 30f, Radius = HeliRadius });
                }
            }

            if (Build.Has(UpgradeId.Curtain))
            {
                _curtainClock -= Dt;
                if (_curtainClock <= 0f)
                {
                    bool wall = Build.Level(UpgradeId.WaterWall) > 0;
                    int lv = Build.PowerOf(UpgradeId.Curtain);
                    float radius = CurtainRadiusNow;
                    _curtainClock = CurtainInterval - (0.4f * (lv - 1));
                    JustCurtain = true;
                    Douse(Player, radius);
                    foreach (Structure st in Structures)
                    {
                        if (st.Within(Player, radius)) Soak(st, wall ? 0.8f : 0.5f, false);
                    }
                    Near(Player, radius, _near);
                    foreach (Enemy e in _near) Damage(e, wall ? 20f : 12f, Knockback(Player, e.Pos, wall ? 12f : 8f), true, HitSource.Curtain, Player);
                }
            }

            if (Build.Level(UpgradeId.Truck) > 0) TickTruck();
            if (Build.Level(UpgradeId.Sprinkler) > 0) TickSprinkler();
            if (Build.Level(UpgradeId.Rain) > 0) TickRain();
            if (Build.Level(UpgradeId.Retardant) > 0) TickRetardant();
            if (Build.Level(UpgradeId.Foam) > 0) TickFoam();
        }

        private float _truckClock = 2f;
        private float _truckLeft;
        private readonly List<Enemy> _truckHit = new List<Enemy>();
        private readonly List<Structure> _truckSoaked = new List<Structure>();

        /// <summary>소방차: 12초마다 소방관이 선 가로줄을 3초 동안 가로지른다. 곁 1.5칸 불은 20 피해와 밀림, 3칸 안 구조물은 적신다.</summary>
        /// <summary>물폭탄 k번째가 노릴 불난 건물: 10칸 안에서 불이 센 순서로 k번째. 없으면 null.</summary>
        private Structure BombBuilding(int k)
        {
            var hot = new List<Structure>();
            foreach (Structure st in Structures)
            {
                if (st.IsBuilding && st.Burning && st.DistanceTo(Player) <= 10f) hot.Add(st);
            }
            if (k >= hot.Count) return null;
            hot.Sort((a, b) => b.Fire.CompareTo(a.Fire));
            return hot[k];
        }

        /// <summary>
        /// 순찰 드론: 소방관 12칸 안에서 불이 센(구조 드론은 사람 갇힌 곳 먼저) 건물로 날아가 지붕 위를 돌며 물을 뿌린다.
        /// 없으면 소방관 곁을 돈다. 도는 길에 닿은 불 몹은 친다. 구조 드론은 건물 위 2초마다 한 명씩 끌어올린다.
        /// </summary>
        private void TickDrones()
        {
            int drones = Build.PowerOf(UpgradeId.Drone);
            Drones.Clear();
            if (drones <= 0)
            {
                DroneTarget = null;
                DroneCenter = Player;
                return;
            }
            bool rescue = Build.Level(UpgradeId.RescueDrone) > 0;
            Structure target = null;
            float best = float.MinValue;
            foreach (Structure st in Structures)
            {
                if (!st.IsBuilding || !st.Burning || st.DistanceTo(Player) > DroneRange) continue;
                float score = st.Fire + (rescue && st.Residents > 0 ? 10f : 0f);
                if (score > best)
                {
                    best = score;
                    target = st;
                }
            }
            DroneTarget = target;
            Vec2 home = target != null ? target.Pos : Player;
            float dx = home.X - DroneCenter.X;
            float dy = home.Y - DroneCenter.Y;
            float len = (float)Math.Sqrt((dx * dx) + (dy * dy));
            float step = 9f * Dt;
            if (len > step) DroneCenter = new Vec2(DroneCenter.X + (dx / len * step), DroneCenter.Y + (dy / len * step));
            else DroneCenter = home;
            bool over = target != null && len <= 0.5f;

            float radius = target != null ? Math.Max(target.Half.X, target.Half.Y) + 0.6f : 2.3f;
            _droneAngle += 3f * Dt;
            for (int k = 0; k < drones; k++)
            {
                double a = _droneAngle + (Math.PI * 2 * k / drones);
                var at = new Vec2(DroneCenter.X + (float)(Math.Cos(a) * radius), DroneCenter.Y + (float)(Math.Sin(a) * radius));
                Drones.Add(at);
                Near(at, 0.5f, _near);
                foreach (Enemy e in _near)
                {
                    if (e.DroneCooldown > 0f) continue;
                    e.DroneCooldown = 0.5f;
                    Damage(e, 6f, Knockback(at, e.Pos, 3f), true, HitSource.Drone, at);
                }
            }
            if (!over) return;
            // 지붕 위: 드론마다 DroneDropEvery에 한 번씩, 번갈아 급강하해 물폭탄을 떨어뜨린다(저항 무시 한 방).
            _droneDrop -= Dt;
            if (_droneDrop <= 0f && target.Burning)
            {
                _droneDrop = DroneDropEvery / drones;
                Vec2 from = Drones[_droneTurn % Drones.Count];
                _droneTurn++;
                Shots.Add(new Shot { Kind = ShotKind.Bomb, From = from, Pos = from, Target = target.Pos, Life = 0.35f, Damage = 6f, Radius = 1.6f, Drone = true });
            }
            if (rescue && target.Burning && target.Residents > 0)
            {
                target.DroneRescue += Dt;
                if (target.DroneRescue >= DroneRescueTime)
                {
                    target.DroneRescue = 0f;
                    RescueOne(target);
                }
            }
        }

        /// <summary>공중 소화탄: 3초마다 맵 어디든 불난 건물마다 소화탄이 떨어진다.</summary>
        private void TickAirBombs()
        {
            if (Build.Level(UpgradeId.AirBomb) == 0) return;
            _airClock -= Dt;
            if (_airClock > 0f) return;
            _airClock = AirBombEvery;
            foreach (Structure st in Structures)
            {
                if (!st.IsBuilding || !st.Burning) continue;
                var from = new Vec2(st.Pos.X - 6f, st.Pos.Y + 14f);
                Shots.Add(new Shot { Kind = ShotKind.Bomb, From = from, Pos = from, Target = st.Pos, Life = 0.9f, Damage = 10f, Radius = 2.2f, Air = true });
                ShotsFired++;
            }
        }

        /// <summary>
        /// 방수 포탑: 7초마다 선 자리에 세운다(Lv3·5에 동시 +1). 0.4초마다 4칸 안 가장 가까운 불 몹을 쏘고, 곁 건물 불을 줄인다.
        /// 현장 구조소는 두 배 오래 서 있고, 6칸 안 건물은 연기로 사람을 잃지 않는다.
        /// </summary>
        private void TickTurrets()
        {
            int lv = Build.PowerOf(UpgradeId.Turret);
            if (lv <= 0)
            {
                Turrets.Clear();
                return;
            }
            bool post = Build.Level(UpgradeId.RescuePost) > 0;
            int most = 1 + (lv >= 3 ? 1 : 0) + (lv >= 5 ? 1 : 0);
            _turretClock -= Dt;
            if (_turretClock <= 0f)
            {
                _turretClock = TurretEvery;
                float life = (5f + (lv - 1)) * (post ? 2f : 1f);
                Turrets.Add(new Turret { Pos = Player, Life = life, MaxLife = life });
                TurretsPlaced.Add(Player);
                while (Turrets.Count > most) Turrets.RemoveAt(0);
            }
            for (int i = Turrets.Count - 1; i >= 0; i--)
            {
                Turret tu = Turrets[i];
                tu.Life -= Dt;
                if (tu.Life <= 0f)
                {
                    Turrets.RemoveAt(i);
                    continue;
                }
                foreach (Structure st in Structures)
                {
                    if (st.IsBuilding && st.Burning && st.Within(tu.Pos, TurretRange)) Soak(st, TurretWater * Dt);
                }
                tu.Clock -= Dt;
                if (tu.Clock > 0f) continue;
                tu.Clock = 0.4f;
                Near(tu.Pos, TurretRange, _near);
                Enemy target = null;
                float close = float.MaxValue;
                foreach (Enemy e in _near)
                {
                    float d = e.Pos.DistanceTo(tu.Pos);
                    if (d < close)
                    {
                        close = d;
                        target = e;
                    }
                }
                tu.Aim = target != null ? target.Pos : (Vec2?)null;
                if (target != null) Damage(target, TurretHit, Knockback(tu.Pos, target.Pos, 3f), true, HitSource.Turret, tu.Pos);
            }
        }

        /// <summary>현장 구조소 곁(6칸)의 건물: 연기가 차지 않는다.</summary>
        private bool Sheltered(Structure s)
        {
            if (Build.Level(UpgradeId.RescuePost) == 0) return false;
            foreach (Turret tu in Turrets)
            {
                if (s.Within(tu.Pos, PostRange)) return true;
            }
            return false;
        }

        /// <summary>구급차: 20초마다 갇힌 사람 연기가 가장 짙은 건물의 연기를 걷어 낸다.</summary>
        private void TickAmbulance()
        {
            _ambulanceClock -= Dt;
            if (_ambulanceClock > 0f) return;
            Structure worst = null;
            foreach (Structure st in Structures)
            {
                if (!st.Burning || st.Residents <= 0) continue;
                if (worst == null || st.Smoke > worst.Smoke) worst = st;
            }
            if (worst == null) return;
            _ambulanceClock = AmbulanceEvery;
            worst.Smoke = 0f;
            AmbulanceAt = worst;
        }

        private void TickTruck()
        {
            if (!Truck.HasValue)
            {
                _truckClock -= Dt;
                if (_truckClock > 0f) return;
                _truckClock = TruckInterval;
                TruckDir = Rand() < 0.5f ? -1f : 1f;
                // 가장 센 불난 건물의 줄을 달린다(없으면 소방관 줄).
                Structure hot = null;
                foreach (Structure st in Structures)
                {
                    if (st.IsBuilding && st.Burning && (hot == null || st.Fire > hot.Fire)) hot = st;
                }
                Vec2 row = hot != null ? hot.Pos : Player;
                Truck = new Vec2(row.X - (TruckDir * TruckReach), row.Y);
                _truckLeft = TruckTime;
                _truckHit.Clear();
                _truckSoaked.Clear();
            }

            Vec2 at = Truck.Value;
            at.X += TruckDir * (TruckReach * 2f / TruckTime) * Dt;
            Truck = at;
            Near(at, TruckHitRange + 1f, _near);
            foreach (Enemy e in _near)
            {
                if (_truckHit.Contains(e) || Math.Abs(e.Pos.X - at.X) > 1.2f) continue;
                _truckHit.Add(e);
                // 차 옆으로 튕겨 낸다.
                Damage(e, 20f, new Vec2(TruckDir * 3f, e.Pos.Y >= at.Y ? 8f : -8f), true, HitSource.Special, at);
            }
            foreach (Structure st in Structures)
            {
                if (_truckSoaked.Contains(st) || !st.Within(at, TruckSoakRange)) continue;
                _truckSoaked.Add(st);
                Soak(st, 0.4f, false);
            }
            Douse(at, TruckHitRange);
            _truckLeft -= Dt;
            if (_truckLeft <= 0f) Truck = null;
        }

        private float _sprinklerClock = 1f;

        /// <summary>스프링클러: 6초마다 모든 타는 건물 불을 0.25 줄이고, 둘레 2.5칸 불에 8 피해를 준다.</summary>
        private void TickSprinkler()
        {
            _sprinklerClock -= Dt;
            if (_sprinklerClock > 0f) return;
            _sprinklerClock = SprinklerInterval;
            foreach (Structure st in Structures)
            {
                if (!st.IsBuilding || !st.Burning) continue;
                Sprinkled.Add(st);
                Soak(st, SprinklerDouse, false);
                Douse(st.Pos, Math.Max(st.Half.X, st.Half.Y) + 2.5f);
                foreach (Enemy e in Enemies)
                {
                    if (!e.Dead && st.Within(e.Pos, 2.5f)) Damage(e, 8f, Knockback(st.Pos, e.Pos, 3f), true, HitSource.Special, st.Pos);
                }
            }
        }

        private float _rainClock = 2f;

        /// <summary>비구름: 12초마다 14칸 안에서 불이 가장 몰린 곳에 3초 동안 비. 바닥 불을 끄고, 구조물을 적시고, 적에게 초당 6 피해.</summary>
        private void TickRain()
        {
            if (RainAt.HasValue)
            {
                Vec2 at = RainAt.Value;
                Douse(at, RainRadius);
                foreach (Structure st in Structures)
                {
                    if (st.Within(at, RainRadius)) Soak(st, 0.3f * Dt);
                }
                Near(at, RainRadius, _near);
                foreach (Enemy e in _near) Damage(e, 6f * Dt, default, false, HitSource.Special);
                RainLeft -= Dt;
                if (RainLeft <= 0f) RainAt = null;
                return;
            }
            _rainClock -= Dt;
            if (_rainClock > 0f) return;
            _rainClock = RainInterval;
            RainAt = FireCenter(14f);
            RainLeft = RainTime;
            JustRain = true;
        }

        private float _foamClock = 2f;

        /// <summary>폼 살포: 10초마다 14칸 안에서 바닥 불이 가장 몰린 곳(없으면 가장 큰 불, 둘 다 없으면 생길 때까지 기다린다)에 반경 4.5 폼. 바닥 불을 끄고, 탈 것에 한 번에 물을 붓고, 적은 8 피해. 8초 동안 그 안 새 기름 불을 막는다.</summary>
        private void TickFoam()
        {
            if (FoamAt.HasValue)
            {
                FoamLeft -= Dt;
                if (FoamLeft <= 0f) FoamAt = null;
            }
            _foamClock -= Dt;
            if (_foamClock > 0f) return;
            // 덮을 불이 없으면 쏘지 않고 기다린다(빈 땅에 쏘면 아무 일도 없다).
            Vec2? target = GroundFireCenter(14f) ?? BurningCenter(14f);
            if (!target.HasValue) return;
            _foamClock = FoamInterval;
            Vec2 at = target.Value;
            FoamAt = at;
            FoamLeft = FoamTime;
            JustFoam = true;
            Douse(at, FoamRadius);
            foreach (Structure st in Structures)
            {
                if (st.Burning && st.Within(at, FoamRadius)) Soak(st, FoamDouse, false);
            }
            Near(at, FoamRadius, _near);
            foreach (Enemy e in _near) Damage(e, 8f, Knockback(at, e.Pos, 3f), true, HitSource.Special, at);
        }

        /// <summary>range 안에서 가장 센 불난 탈 것(가스통 빼고). 없으면 null.</summary>
        private Vec2? BurningCenter(float range)
        {
            Structure best = null;
            foreach (Structure st in Structures)
            {
                if (!st.Burning || st.Kind == StructureKind.Gas || st.DistanceTo(Player) > range) continue;
                if (best == null || st.Fire > best.Fire) best = st;
            }
            return best?.Pos;
        }

        /// <summary>range 안 바닥 불 중 FoamRadius 안에 가장 많은 바닥 불을 거느린 자리. 없으면 null.</summary>
        private Vec2? GroundFireCenter(float range)
        {
            Vec2? best = null;
            int bestN = 0;
            foreach (Puddle p in BurningGround)
            {
                if (p.Out || p.Life <= 0f || p.Pos.DistanceTo(Player) > range) continue;
                int n = 0;
                foreach (Puddle q in BurningGround)
                {
                    if (!q.Out && q.Life > 0f && q.Pos.DistanceTo(p.Pos) <= FoamRadius) n++;
                }
                if (n > bestN)
                {
                    bestN = n;
                    best = p.Pos;
                }
            }
            return best;
        }

        private float _retardantClock = 3f;

        /// <summary>방염제: 15초마다 가장 큰 불을 가로지르는 폭 3·길이 14 띠. 띠 안 구조물은 20초 동안 안 타고, 바닥 불은 꺼지고, 적은 15 피해.</summary>
        private void TickRetardant()
        {
            foreach (Band b in Retardants) b.Life -= Dt;
            Retardants.RemoveAll(b => b.Life <= 0f);
            _retardantClock -= Dt;
            if (_retardantClock > 0f) return;
            _retardantClock = RetardantInterval;

            Vec2 mid = FireCenter(14f);
            double a = Rand() * Math.PI;
            var half = new Vec2((float)(Math.Cos(a) * RetardantLength * 0.5), (float)(Math.Sin(a) * RetardantLength * 0.5));
            var band = new Band { A = new Vec2(mid.X - half.X, mid.Y - half.Y), B = new Vec2(mid.X + half.X, mid.Y + half.Y), Life = RetardantWet, MaxLife = RetardantWet };
            Retardants.Add(band);
            JustRetardant = true;
            float r = RetardantWidth * 0.5f;
            foreach (Structure st in Structures)
            {
                if (st.Collapsed || SegmentDistance(st.Pos, band.A, band.B) > r + Math.Max(st.Half.X, st.Half.Y)) continue;
                // 방염제는 띠 안 불을 완전히 누르고 한동안 안 타게 한다.
                Soak(st, 1f, false);
                st.Wet = Math.Max(st.Wet, RetardantWet);
            }
            foreach (Puddle p in BurningGround)
            {
                if (p.Out || p.Life <= 0f || SegmentDistance(p.Pos, band.A, band.B) > r + p.Radius) continue;
                p.Out = true;
                p.Life = 0f;
                Extinguished.Add(p.Pos);
            }
            foreach (Enemy e in Enemies)
            {
                if (!e.Dead && SegmentDistance(e.Pos, band.A, band.B) <= r + e.Radius) Damage(e, 15f, default, true, HitSource.Special);
            }
        }

        /// <summary>range 안에서 가장 크게 타는 구조물 → 불이 몰린 곳 → 소방관 앞.</summary>
        private Vec2 FireCenter(float range)
        {
            Structure best = null;
            foreach (Structure st in Structures)
            {
                if (!st.Burning || st.Kind == StructureKind.Gas || st.DistanceTo(Player) > range) continue;
                if (best == null || st.Fire > best.Fire) best = st;
            }
            if (best != null) return best.Pos;
            return RandomEnemyNear(range) ?? new Vec2(Player.X + (Facing.X * 6f), Player.Y + (Facing.Y * 6f));
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

        public const float TruckInterval = 12f;
        public const float TruckTime = 3f;
        public const float TruckReach = 16f;
        public const float TruckHitRange = 1.5f;
        public const float TruckSoakRange = 3f;
        public const float SprinklerInterval = 6f;
        public const float SprinklerDouse = 0.25f;
        public const float FoamInterval = 10f;
        public const float FoamTime = 8f;
        public const float FoamRadius = 4.5f;
        public const float FoamDouse = 0.3f;
        public const float RainInterval = 12f;
        public const float RainTime = 3f;
        public const float RainRadius = 6f;
        public const float RetardantInterval = 15f;
        public const float RetardantLength = 14f;
        public const float RetardantWidth = 3f;
        public const float RetardantWet = 20f;

        public const float HeliInterval = 9f;
        public const float HeliFlight = 1.2f;
        public const float HeliRadius = 4.5f;
        public const float CurtainInterval = 4f;
        public const float CurtainRadius = 4.5f;

        /// <summary>지금 물의 장막 고리 반경: Lv1 4.5칸, 레벨마다 +0.5칸, 물의 방벽 7칸.</summary>
        public float CurtainRadiusNow
        {
            get { return Build.Level(UpgradeId.WaterWall) > 0 ? 7f : CurtainRadius + (0.5f * (Build.PowerOf(UpgradeId.Curtain) - 1)); }
        }
        public const float PartnerSpeed = 4.5f;
        public const float PartnerWaterBase = 0.05f;
        public const float PartnerHit = 4f;
        public const float DroneWater = 0.05f;

        /// <summary>순찰 드론 한 대의 투하 간격(초. 드론이 n대면 n배 자주)과 물폭탄 하나가 끄는 불 세기(저항 무시).</summary>
        public const float DroneDropEvery = 2.5f;
        public const float DroneDropWater = 0.25f;

        public const float DroneRange = 12f;
        public const float DroneRescueTime = 2f;
        public const float AirBombEvery = 3f;
        public const float TurretEvery = 7f;
        public const float TurretRange = 4f;
        public const float TurretHit = 5f;
        public const float TurretWater = 0.05f;
        public const float PostRange = 6f;
        public const float AmbulanceEvery = 20f;
        public const float AmbulanceHeal = 10f;

        /// <summary>한 명 구할 때 차는 체력(예전 20: 구조만 하면 체력이 늘 차서 방화복이 쓸모없었다).</summary>
        public const float RescueHeal = 5f;

        /// <summary>열기: 타는 건물 가장자리 이 칸 안에서 불 세기만큼 초당 피해를 받는다(방화복이 줄인다).</summary>
        public const float HeatRange = 2.5f;
        public const float HeatDps = 5f;
        public const float TreeHeatRange = 1.5f;

        /// <summary>12칸 안에서 가장 크게 타는 건물 → 불이 몰린 곳 → 소방관 앞.</summary>
        private Vec2 HeliTarget()
        {
            Structure best = null;
            foreach (Structure st in Structures)
            {
                if (!st.Burning || st.Kind == StructureKind.Gas || st.DistanceTo(Player) > 12f) continue;
                if (best == null || st.Fire > best.Fire) best = st;
            }
            if (best != null) return best.Pos;
            return RandomEnemyNear(12f) ?? new Vec2(Player.X + (Facing.X * 6f), Player.Y + (Facing.Y * 6f));
        }

        private void FireDrop(float angle, float damage, float speed, float radius, int pierce, float life, ShotKind kind, bool hose = false)
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
            });
            ShotsFired++;
        }

        private void MoveShots()
        {
            foreach (Shot s in Shots)
            {
                if (s.Dead) continue;
                s.Age += Dt;

                if (s.Kind == ShotKind.Bomb || s.Kind == ShotKind.Heli)
                {
                    float t = Math.Min(1f, s.Age / s.Life);
                    s.Pos = new Vec2(s.From.X + ((s.Target.X - s.From.X) * t), s.From.Y + ((s.Target.Y - s.From.Y) * t));
                    if (t >= 1f)
                    {
                        s.Dead = true;
                        (s.Kind == ShotKind.Heli ? HeliDrops : s.Air ? AirBlasts : s.Drone ? DroneDrops : Explosions).Add(s.Target);
                        Douse(s.Target, s.Radius);
                        foreach (Structure st in Structures)
                        {
                            if (st.Within(s.Target, s.Radius)) Soak(st, s.Drone ? DroneDropWater : s.Damage * WaterPerDamage, false);
                        }
                        Near(s.Target, s.Radius, _near);
                        HitSource source = s.Kind == ShotKind.Heli ? HitSource.Special : HitSource.Bomb;
                        foreach (Enemy e in _near) Damage(e, s.Damage, Knockback(s.Target, e.Pos, 7f), true, source, s.Target);
                    }
                    continue;
                }

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
                    else Burn(10f * Dt);
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
            // 폼이 깔린 곳에는 기름 불이 서지 못한다.
            if (FoamAt.HasValue && FoamAt.Value.DistanceTo(at) <= FoamRadius)
            {
                Extinguished.Add(at);
                return;
            }
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
            if (s.Collapsed || s.Wet > 0f) return false;
            bool fresh = s.Fire <= 0f;
            s.Fire = Math.Min(1f, Math.Max(s.Fire, amount));
            if (fresh)
            {
                Ignited.Add(s);
                s.SpitClock = 2f;
                s.BlazeClock = 6f;
                s.SpreadClock = Stage.SpreadEvery;
                s.RescueHold = 0f;
                if (s.Kind == StructureKind.Gas) s.Fuse = GasFuse;
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
                float before = s.Fire;
                s.Fire -= resist ? water * (1f - (FireResist * s.Fire)) : water;
                if (before - Math.Max(0f, s.Fire) > KnockShown && s.IsBuilding) Knocked.Add(new FireKnock { At = s, Amount = before - Math.Max(0f, s.Fire) });
                if (s.Fire > 0f) return;
                s.Fire = 0f;
                s.Fuse = -1f;
                s.HoseHold = 0f;
                s.Warned = false;
                Doused.Add(s);
                // 불을 끈 보상: 건물은 큰 구슬, 작은 것은 작은 구슬. 건물 진화는 콤보를 크게 잇는다.
                if (s.IsBuilding) ComboAdd(ComboPerDouse);
                DropGem(s.Door, (s.IsBuilding ? 8 : 3) * ComboMult);
            }
            s.Wet = WetTime;
        }

        /// <summary>물줄기가 구조물에 닿았는지. 물대포 물방울은 막혀서 사라지면 true, 제트는 뚫고 간다.</summary>
        private bool SoakStructures(Shot s)
        {
            foreach (Structure st in Structures)
            {
                if (st.Collapsed || !st.Within(s.Pos, s.Radius * 0.5f)) continue;
                // 제트는 뚫고 가고, 나무는 물이 잎 사이로 빠진다(나무 밑에서 쏴도 막히지 않게).
                if (s.Kind == ShotKind.Jet || st.Kind == StructureKind.Tree)
                {
                    if (s.Soaked == null) s.Soaked = new List<Structure>();
                    if (s.Soaked.Contains(st)) continue;
                    s.Soaked.Add(st);
                    if (s.Hose) Warm(st);
                    Soak(st, s.Damage * WaterPerDamage);
                    continue;
                }
                if (s.Hose) Warm(st);
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
                // 다람쥐는 나무에만 불을 붙인다: 가는 길의 건물까지 태우면 건물당 불이 마을의 1.5배라 동네를 늘 잃었다.
                // 나무 불은 바람을 타고 건물을 위협하므로 숲다운 압박은 남는다.
                if (e.Kind == EnemyKind.Squirrel && st.Kind != StructureKind.Tree) continue;
                if (!st.Within(e.Pos, e.Radius)) continue;
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
                if (s.Collapsed) continue;
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

                s.Fire = Math.Min(1f, s.Fire + (Stage.FireGrowth * Dt));
                s.Integrity -= s.Fire * Dt / (s.IsBuilding ? BurnBuilding : BurnSmall);
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

                if (Stage.Wind && s.Kind == StructureKind.Tree && s.Fire >= SpreadAt)
                {
                    s.WindClock -= Dt;
                    if (s.WindClock <= 0f)
                    {
                        s.WindClock = WindSpreadEvery;
                        // 반쯤만 옮는다: 늘 옮기면 한 그루가 두세 그루를 태워 숲 전체가 순식간에 탄다.
                        Structure next = Downwind(s);
                        // 대화재 동안 숲은 바람이 거세진다(캠프를 덮치는 산불).
                        float chance = Finale ? Stage.FinaleWindChance : WindSpreadChance;
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
            if (Player.DistanceTo(gas.Pos) <= GasRadius) Hurt(25f);
        }

        private void Fall(Structure s)
        {
            s.Collapsed = true;
            s.Fire = 0f;
            s.Integrity = 0f;
            s.Fuse = -1f;
            Fell.Add(s);
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
                Burn(e.Touch * Dt);
                if (push <= 0f || e.BounceCool > 0f) continue;
                e.BounceCool = SuitBounceCool;
                Vec2 k = Knockback(Player, e.Pos, push);
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
        /// 열기: 타는 건물 곁(가장자리 2.5칸)에 서 있으면 가장 센 불 하나만큼 초당 피해. 방화복이 줄이고, 물의 방벽 안에선 절반.
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
            float wall = Build.Level(UpgradeId.WaterWall) > 0 ? 0.5f : 1f;
            HeatHurt = HeatDps * worst * Build.HeatScale * wall * Dt;
            Burn(HeatDps * worst * wall * Dt);
        }

        /// <summary>불에 데는 피해: 방화복(HeatScale)만큼 덜 받는다. 원값은 통계에 남긴다.</summary>
        private void Burn(float raw)
        {
            Stats.FireDamageRaw += raw;
            Hurt(raw * Build.HeatScale);
        }

        private void Hurt(float amount)
        {
            Hp -= amount;
            PlayerHurt += amount;
            Stats.DamageTaken += amount;
        }

        /// <summary>한 틱의 재미 밀도 통계를 쌓는다.</summary>
        private void Measure()
        {
            Stats.MinHpRatio = Math.Min(Stats.MinHpRatio, Math.Max(0f, Hp) / MaxHp);
            bool building = false;
            bool other = false;
            bool fireNear = false;
            foreach (Structure s in Structures)
            {
                if (!s.Burning) continue;
                if (s.IsBuilding) building = true;
                else other = true;
                if (!fireNear && s.DistanceTo(Player) <= 8f) fireNear = true;
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
                    _bonusPicks = ChestPicks;
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
        /// <summary>구조대원 수: Lv1~2 한 명, Lv3~4 두 명, Lv5 세 명, 구조 분대 네 명.</summary>
        public int PartnerCount
        {
            get
            {
                if (Build.Level(UpgradeId.Squad) > 0) return 4;
                int lv = Build.Level(UpgradeId.Partner);
                return lv >= 5 ? 3 : lv >= 3 ? 2 : lv >= 1 ? 1 : 0;
            }
        }

        /// <summary>문 앞에 대원이 있을 때 구조가 빨라지는 배율(Lv2부터 1.25, 구조 분대 2).</summary>
        public float PartnerRescueBoost
        {
            get { return Build.Level(UpgradeId.Squad) > 0 ? 2f : Build.Level(UpgradeId.Partner) >= 2 ? 1.25f : 1f; }
        }

        /// <summary>대원 한 명이 곁 건물 불을 초당 줄이는 양(Lv2·Lv4에 +25%, 구조 분대 두 배).</summary>
        public float PartnerWater
        {
            get
            {
                if (Build.Level(UpgradeId.Squad) > 0) return PartnerWaterBase * 1.5625f * 2f;
                int lv = Build.Level(UpgradeId.Partner);
                return PartnerWaterBase * (lv >= 2 ? 1.25f : 1f) * (lv >= 4 ? 1.25f : 1f);
            }
        }

        private bool PartnerAt(Vec2 door)
        {
            foreach (Vec2 p in Partners)
            {
                if (door.DistanceTo(p) <= RescueRange) return true;
            }
            return false;
        }

        /// <summary>
        /// 구조대원: 저마다 다른 불난 건물을 맡는다. 갇힌 사람이 있는 건물의 문이 먼저, 없으면 소방관 10칸 안 불난 건물 곁,
        /// 그것도 없으면 소방관 곁을 따른다. 건물 곁에선 물을 뿌려 불을 줄이고, 곁 불 몹을 쏜다.
        /// </summary>
        private void MovePartners()
        {
            int want = PartnerCount;
            while (Partners.Count < want) Partners.Add(new Vec2(Player.X - 1.2f, Player.Y - (0.6f * Partners.Count)));
            while (Partners.Count > want) Partners.RemoveAt(Partners.Count - 1);
            if (want == 0) return;

            float speed = PartnerSpeed * (Build.Level(UpgradeId.Squad) > 0 ? 1.3f : 1f);
            var taken = new List<Structure>();
            for (int i = 0; i < Partners.Count; i++)
            {
                Vec2 at = Partners[i];
                Structure job = null;
                float best = float.MaxValue;
                foreach (Structure s in Structures)
                {
                    if (!s.Burning || !s.IsBuilding || taken.Contains(s)) continue;
                    bool people = s.Residents > 0;
                    if (!people && s.DistanceTo(Player) > 10f) continue;
                    float d = s.Door.DistanceTo(at) - (people ? 100f : 0f);
                    if (d < best)
                    {
                        best = d;
                        job = s;
                    }
                }
                Vec2 goal = new Vec2(Player.X - 1.2f + (0.8f * i), Player.Y - 0.6f);
                if (job != null)
                {
                    taken.Add(job);
                    goal = job.Door;
                }
                float dx = goal.X - at.X;
                float dy = goal.Y - at.Y;
                float len = (float)Math.Sqrt((dx * dx) + (dy * dy));
                float step = speed * Dt;
                if (len > 0.2f)
                {
                    at.X += dx / len * Math.Min(step, len);
                    at.Y += dy / len * Math.Min(step, len);
                }
                at = ClampToArena(at);
                Partners[i] = at;

                // 곁(가장자리 3칸) 불난 건물에 물을 뿌린다.
                if (job != null && job.DistanceTo(at) <= 3f) Soak(job, PartnerWater * Dt);

                // 곁 불 몹을 0.5초마다 쏜다.
                _partnerClocks[i] -= Dt;
                if (_partnerClocks[i] <= 0f)
                {
                    _partnerClocks[i] = 0.5f;
                    Near(at, 2.5f, _near);
                    Enemy target = null;
                    float close = float.MaxValue;
                    foreach (Enemy e in _near)
                    {
                        float d = e.Pos.DistanceTo(at);
                        if (d < close)
                        {
                            close = d;
                            target = e;
                        }
                    }
                    if (target != null) Damage(target, PartnerHit * (Build.Level(UpgradeId.Squad) > 0 ? 2f : 1f), Knockback(at, target.Pos, 4f), true, HitSource.Partner, at);
                }
            }
        }

        private void TickRescue()
        {
            MovePartners();
            foreach (Structure s in Structures)
            {
                // 큰 불 속에 오래 갇혀 있으면 연기에 한 명씩 잃는다: 멀리서 끄기만 할 게 아니라 빨리 가야 한다.
                if (s.Burning && s.Residents > 0 && s.Fire >= SmokeFire && !Sheltered(s))
                {
                    s.Smoke += Dt;
                    if (s.Smoke >= SmokeTime)
                    {
                        s.Smoke = 0f;
                        s.Residents--;
                        CiviliansLost++;
                        PeopleLost.Add(s);
                        if (s == BigReport) _bigFailed = true;
                    }
                }

                bool player = s.Door.DistanceTo(Player) <= RescueRange;
                bool partner = PartnerAt(s.Door);
                if (!s.Burning || s.Residents <= 0 || !(player || partner))
                {
                    s.RescueHold = 0f;
                    continue;
                }
                // 문 앞에 대원이 있으면 대원 레벨만큼 더 빠르다.
                s.RescueHold += Dt * (partner ? PartnerRescueBoost : 1f);
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
            float heal = RescueHeal + (Build.Level(UpgradeId.Ambulance) > 0 ? AmbulanceHeal : 0f);
            Stats.HealRescue += Math.Min(MaxHp, Hp + heal) - Hp;
            Hp = Math.Min(MaxHp, Hp + heal);
            JustRescued = true;
            Stats.Events++;
            RescuedFrom.Add(s);
            for (int k = 0; k < n; k++) Civilians.Add(new Civilian { Pos = new Vec2(s.Door.X + ((k - ((n - 1) / 2f)) * 0.5f), s.Door.Y), Life = 1.5f });
        }

        private void Damage(Enemy e, float amount, Vec2 knock, bool show, HitSource source = HitSource.Hose, Vec2 from = default)
        {
            if (e.Dead) return;
            bool crit = Rand() < 0.1f;
            if (crit) amount *= 2f;
            e.Hp -= amount;
            e.HitFlash = 0.08f;
            e.Knock.X += knock.X;
            e.Knock.Y += knock.Y;

            bool killed = e.Hp <= 0f;
            if (killed) Kill(e);
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
            if (e.Kind == EnemyKind.Blaze)
            {
                BurningGround.Add(new Puddle { Pos = e.Pos, Radius = 0.9f, Life = 3f, MaxLife = 3f });
            }
            // 기름 방울은 터지며 기름을 튀긴다: 어디서 잡느냐가 중요하다.
            if (e.Kind == EnemyKind.Oil) Spill(e.Pos, OilDeathSpill);
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
