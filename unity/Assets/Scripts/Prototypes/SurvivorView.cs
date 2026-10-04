using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using FireGame.UnityLayer;
using FireGame.UnityLayer.Feel;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 시험판 C 화면. 규칙은 <see cref="SurvivorSim"/>에 있고, 여기는 60Hz로 돌리며 최대한 요란하게 보여 준다.
    ///
    /// 뱀서류의 재미는 화면이 꽉 차는 데서 온다: 맞으면 번쩍이고 숫자가 튀고, 꺼지면 불똥과 연기가 터지고,
    /// 구슬이 빨려 오며 소리 높이가 올라가고, 레벨업하면 화면이 하얗게 터지며 카드가 튀어 오른다.
    /// 그림은 매 프레임 풀에서 꺼내 다시 그린다(적·구슬·물은 수백 개라 오브젝트를 붙잡아 두지 않는다).
    /// </summary>
    public sealed class SurvivorView : IPrototype
    {
        private const float CameraSize = 9f;

        /// <summary>월드는 수직에서 이만큼 기운(땅에서 65°) 원근 카메라가 화면 해상도(세로 최대 MaxWorldHeight)로 그린다.</summary>
        private const float Tilt = 25f;
        private const float WorldFov = 30f;
        private const int MaxWorldHeight = 1080;

        /// <summary>세운 스프라이트·글자가 카메라를 보는 방향(카메라는 기울기만 있고 돌지 않으니 늘 같다).</summary>
        private static readonly Quaternion Billboard = Quaternion.LookRotation(new Vector3(0f, Mathf.Sin(Tilt * Mathf.Deg2Rad), Mathf.Cos(Tilt * Mathf.Deg2Rad)), Vector3.up);

        /// <summary>입체 면 방향: 앞벽(남쪽, 카메라 쪽)·왼벽·오른벽. 스프라이트 위쪽이 하늘(−Z)을 본다.</summary>
        private static readonly Quaternion Facade = Quaternion.LookRotation(Vector3.up, Vector3.back);
        private static readonly Quaternion SideW = Quaternion.LookRotation(Vector3.right, Vector3.back);
        private static readonly Quaternion SideE = Quaternion.LookRotation(Vector3.left, Vector3.back);
        private const float CarHeight = 0.7f;

        /// <summary>건물·차 상자(발자국, 높이). W()가 이 안의 점을 지붕 높이로 올린다(폭탄·드론 물이 상자 안에 묻히지 않게).</summary>
        private static readonly List<Rect> RoofRects = new List<Rect>();
        private static readonly List<float> RoofHeights = new List<float>();
        private static Material _cutout;

        /// <summary>HUD 카메라가 월드 화면을 보는 곳: 월드에서 멀리 떨어져 월드 스프라이트가 두 번 그려지지 않는다.</summary>
        private static readonly Vector3 ScreenSpot = new Vector3(-5000f, -5000f, -10f);
        private const int MaxParticles = 1100;
        private const int MaxNumbers = 60;
        private const int MaxScorch = 220;

        private static readonly string[] Sparks = { "Effects/spark_01", "Effects/spark_02", "Effects/spark_03", "Effects/spark_04" };
        private static readonly string[] Smokes = { "Effects/smoke_01", "Effects/smoke_02", "Effects/smoke_03", "Effects/smoke_04", "Effects/smoke_05" };
        private static readonly string[] Flames = { "Effects/flame_01", "Effects/flame_02", "Effects/flame_03", "Effects/flame_04", "Effects/flame_05", "Effects/flame_06" };
        private static readonly string[] Faces = { "TopDown/civilian_woman", "TopDown/civilian_old", "TopDown/civilian_man" };

        // 연출 언어: 무기마다 색 하나. 적중·리본·고리가 이 색을 쓴다.
        private static readonly Color HoseTint = new Color(0.85f, 0.97f, 1f, 1f);
        private static readonly Color BombTint = new Color(0.25f, 0.5f, 1f, 1f);
        private static readonly Color DroneTint = new Color(0.4f, 1f, 0.9f, 1f);
        private static readonly Color PartnerTint = new Color(1f, 0.75f, 0.3f, 1f);
        private static readonly Color CurtainTint = new Color(0.7f, 0.9f, 1f, 1f);
        private static readonly Color TurretTint = new Color(0.5f, 0.85f, 1f, 1f);
        private static readonly Color GoldTint = new Color(1f, 0.88f, 0.45f, 1f);

        // 불꽃 플립북(코드로 만든다): 불씨·큰 불·다트·기름. 둥근 구름 텍스처 대신 아래 둥글고 위 뾰족한 불꽃이 흔들린다.
        private Sprite[] _emberSheet;
        private Sprite[] _blazeSheet;
        private Sprite[] _dartSheet;
        private Sprite[] _oilSheet;

        private readonly Transform _root;
        private readonly Transform _world;
        private readonly Camera _camera;
        private readonly RectTransform _hud;
        private readonly Camera _worldCam;
        private readonly RenderTexture _worldRt;
        private readonly GameObject _worldScreen;
        private readonly UnityEngine.Rendering.VolumeProfile _worldPost;
        private readonly Vector3 _cameraHome;
        private readonly float _cameraHomeSize;
        private float _viewHalfW = CameraSize * 16f / 9f;
        private float _viewHalfH = CameraSize;

        private SurvivorSim _sim;
        private readonly bool _maxGear;
        private int _seed = 3;

        /// <summary>지금 스테이지(1부터). 깨면 다음으로 넘어가고, 폰에서도 이어지게 PlayerPrefs에 남긴다.</summary>
        private int _stage;
        public const string StageKey = "firegame.proto.stage";

        /// <summary>시작 스테이지를 이미 적용한 빌드(buildGUID). 같은 빌드를 다시 켜면 저장된 진행을 이어 간다.</summary>
        public const string StartBuildKey = "firegame.proto.startBuild";

        /// <summary>소방서(별 통장·해금·고른 소방관) 저장 글.</summary>
        public const string StationKey = "firegame.proto.station";
        private FireStation _station;

        /// <summary>false면 저장하지 않는다(캡처 하니스가 주입한 소방서).</summary>
        private bool _stationPersist = true;
        private RectTransform _stationLayer;
        private bool _stationOpen;

        /// <summary>이 판에서 번 별(결과창용).</summary>
        private int _earned;

        /// <summary>가운데 띠 색: 스테이지 시작은 파랑, 보스 등장은 빨강.</summary>
        private Color _bandTint = new Color(0.6f, 0f, 0f);
        private float _accumulator;
        private float _time;
        private float _trauma;
        private float _flash;
        private Color _flashColor = Color.white;
        private float _hurt;
        private float _slowmo;
        private float _cardAge;
        private float _overAge;
        private float _alertAge = 99f;

        /// <summary>판 시작 띠 뒤 할 일 한 줄을 띄울 때(음수면 예약 없음).</summary>
        private float _goalAlertAt = -1f;
        private static readonly Color GoalGreen = new Color(0.7f, 1f, 0.6f);
        /// <summary>배너 팝인 배율(보통 1.6, 노란 장비 출동은 2.2).</summary>
        private float _alertScale = 1.6f;
        private float _bossBannerAge = 99f;
        private float _xpPunch;
        private float _shownXp;
        private float _putOutClock;
        private float _gemClock;
        /// <summary>이 판에서 가장 길었던 콤보(결과창용).</summary>
        private int _comboPeak;
        private Text _comboText;
        private float _hitStop;
        private float _zoomKick;
        private float _recoil;

        /// <summary>0 = 앞을 보고 걷는 자세, 1 = 옆으로 서서 노즐을 겨눈 자세.</summary>
        private float _stance;
        private float _cardsIn = -1f;
        private float _waveAge = 99f;
        private float _hurtClock;
        private Vector3 _lastPlayer;
        private float _stepClock;
        private float _regenClock;
        private Vector3 _aim = Vector3.right;
        private float _aimAge = 99f;
        private int _suitShown = -1;
        private Vector3 _mouseAt;
        private bool _hasMouse;

        /// <summary>폰 물 방향. 조준 보정으로 부드럽게 돌고, 손을 떼도 남는다.</summary>
        private Vec2 _touchAim = new Vec2(1f, 0f);
        private readonly List<Vec2> _fires = new List<Vec2>();

        /// <summary>폰 두 엄지 조작. 손가락이 한 번이라도 닿으면 그때부터 마우스 흉내를 무시한다.</summary>
        private readonly TwinStick _stick = new TwinStick();
        private static readonly List<Finger> NoFingers = new List<Finger>();
        private bool _touch;
        private Image _leftRing;
        private Image _leftKnob;
        private Image _rightRing;
        private Image _rightKnob;
        private float _jetPulse;
        private float _igniteClock;
        private readonly List<Vector3> _trail = new List<Vector3>();
        private readonly List<RectTransform> _rays = new List<RectTransform>();

        /// <summary>노란 카드 뒤에서 맥동하는 금빛.</summary>
        private readonly List<Image> _yellowGlows = new List<Image>();
        private SOutcome _lastOutcome;
        private Vector3 _cameraAt;

        // --- 매 프레임 다시 그리는 풀 ---
        private Pool _enemyGlow;
        private Pool _enemyCore;
        private Pool _embers;
        private Pool _blazes;
        private Pool _darts;
        private Pool _bats;
        private static Sprite _batSprite;
        private readonly List<GameObject> _ground = new List<GameObject>();

        /// <summary>가게·창고·차 3D 모델(판마다 새로). 구조물 번호로 찾고, 크기는 (가로, 앞뒤, 높이) 칸.</summary>
        private static readonly string[] HouseModels =
        {
            "Houses/building-type-a", "Houses/building-type-c", "Houses/building-type-e", "Houses/building-type-h",
            "Houses/building-type-k", "Houses/building-type-m", "Houses/building-type-p", "Houses/building-type-t",
        };
        private static readonly string[] CarModels = { "Cars/sedan", "Cars/suv", "Cars/taxi", "Cars/van", "Cars/hatchback-sports" };

        /// <summary>공단(3스테이지) 공장 여덟 종과 컨테이너(차 자리). Kenney City Kit Industrial.</summary>
        private static readonly string[] FactoryModels =
        {
            "Industrial/building-b", "Industrial/building-c", "Industrial/building-e", "Industrial/building-f",
            "Industrial/building-k", "Industrial/building-l", "Industrial/building-m", "Industrial/building-r",
        };
        private static readonly string[] ContainerModels = { "Industrial/shipping-container-a", "Industrial/shipping-container-b", "Industrial/shipping-container-c" };
        private static readonly string[] TownTrees = { "Nature/tree_default", "Nature/tree_oak", "Nature/tree_fat" };
        private static readonly string[] ForestTrees = { "Nature/tree_pineTallA", "Nature/tree_pineRoundA", "Nature/tree_cone" };
        private const float MaxHouseHeight = 2.4f;
        private readonly List<GameObject> _models = new List<GameObject>();
        private GameObject[] _structModels = new GameObject[0];
        private Vector3[] _structSize = new Vector3[0];
        private int _groundStage;
        private Image _windArrow;
        private Text _windLabel;
        private Pool _gems;
        private Pool _siren;
        private Pool _cloud;
        private Pool _rainShade;
        private Pool _band;
        private bool _truckShown;
        private bool _fireboatShown;
        private Light _sun;

        /// <summary>큰 파도가 이번에 부두선에 닿는 충격파를 이미 냈다.</summary>
        private bool _waveHitQuay;
        /// <summary>비구름 번개까지 남은 시간, 거품 매트의 다음 거품 터짐까지 남은 시간.</summary>
        private float _lightningClock;
        private float _foamPopClock;
        private float _planeAge = 99f;
        private Vector3 _planeFrom;

        /// <summary>구급차: 화면 밖에서 문 앞까지 1.2초 달려와 2초 서 있다가 떠난다. 99면 없음.</summary>
        private float _ambulanceAge = 99f;

        /// <summary>캡처용: 구급차가 달려가는 곳(없으면 null).</summary>
        public Vector3? AmbulanceSpot
        {
            get { return _ambulanceAge < 4.4f ? _ambulanceTo : (Vector3?)null; }
        }
        private Vector3 _ambulanceFrom;
        private Vector3 _ambulanceTo;
        private Vector3 _ambulanceRoof;
        private bool _ambulanceArrived;
        private Vector3 _planeTo;
        private Pool _toolbox;
        private Pool _toolboxGlow;
        private int _toolboxesShown;
        private static Sprite _toolboxSprite;
        private Pool _kit;
        private int _kitsShown;
        private static Sprite _kitSprite;
        private Pool _chest;
        private int _chestsShown;
        private static Sprite _chestSprite;
        private Pool _gemCores;
        private Pool _dropGlow;
        private RibbonPool _waterSheath;
        private RibbonPool _waterBody;
        private RibbonPool _waterShine;
        private Pool _streamJoint;
        private Pool _blobShine;
        /// <summary>물줄기 위를 노즐에서 끝으로 달리는 밝은 결, 끝 물안개의 무지개.</summary>
        private Pool _streamGlint;
        private Pool _rainbow;
        private static Sprite _rainbowSprite;
        /// <summary>쥔 지 몇 초(놓으면 0). 쥐는 순간 분출·줄기 굵어짐·잔떨림의 시계.</summary>
        private float _sprayHeld;
        /// <summary>쥔 동안 화면 잔떨림 바닥(트라우마, 진폭 = t²·0.6). 거슬리면 낮추는 손잡이.</summary>
        private const float SprayTremor = 0.14f;
        /// <summary>바닥 물길·물결 고리를 다음에 남길 때(_time).</summary>
        private float _wetAt;
        private float _rippleAt;
        private Pool _nozzle;
        private Pool _reticle;
        private Pool _shadows;
        private Pool _motes;
        private Pool _hoseTube;
        private Pool _hoseTubeEdge;
        private Pool _bubbles;
        private Pool _auras;
        private Pool _tank;
        private Pool _radar;

        // 노란 특수 장비: 헬기(그림자·몸통·로터), 동료.
        private Pool _heliShadow;
        private TextMesh _partnerTag;
        private Pool _sprayLines;
        private Pool _airBombs;
        private Pool _wet;
        private static Sprite _airBombSprite;

        /// <summary>무기 탄환: 친 무기에서 맞은 곳으로 잠깐 뻗는 물 막대.</summary>
        private struct Tracer
        {
            public Vector3 From;
            public Vector3 To;
            public float Age;
            public float Life;
            public float Width;
            public Color Color;
        }

        /// <summary>땅에 잠깐 남는 젖은 물 자국(폭탄·장막).</summary>
        private struct WetMark
        {
            public Vector3 At;
            public float Size;
            public float Age;
            public float Life;
            public bool Ring;
        }

        private const int MaxTracers = 64;
        private const int MaxImpactsPerSource = 6;
        private readonly List<Tracer> _tracers = new List<Tracer>();
        private readonly List<WetMark> _wetMarks = new List<WetMark>();
        private readonly int[] _impacts = new int[8];
        private readonly float[] _turretKick = new float[8];
        private readonly List<Vector3> _turretLandings = new List<Vector3>();
        private readonly List<float> _turretLandingIn = new List<float>();
        private float _weaponSoundClock;
        private float _heatTextClock;
        private readonly Vector3[] _partnerLast = new Vector3[4];
        private readonly float[] _partnerDeg = new float[4];
        private Vector3 _heliExitFrom;
        private Vector3 _heliExitDir;
        private float _heliExitAge = 99f;
        private float _frameDt;
        private static Sprite _heliSprite;
        private AudioSource _splash;
        private AudioSource _steam;
        private Text _xpText;
        private Text _xpTease;
        private Pool _bombShadows;
        private Pool _droneGlow;
        private Pool _foam;
        private Pool _groundFire;
        private Pool _groundGlow;
        private Pool _civilianRings;
        private Pool _houseShadows;
        private Pool _roofEdges;
        private Pool _roofs;
        private Pool _roofTrim;
        private Pool _props;
        private Pool _walls;
        private Pool _roofGlow;
        private Pool _roofFire;
        private Pool _bars;
        private readonly List<TextMesh> _signs = new List<TextMesh>();
        private readonly List<TextMesh> _helps = new List<TextMesh>();

        /// <summary>간판 판(x, y, 높이, 폭): 글자 뒤에 세우는 짙은 띠.</summary>
        private readonly List<Vector4> _signBoards = new List<Vector4>();
        private Pool _edgeArrows;
        /// <summary>화살표 뒤 어두운 원반(가장자리·발밑 공통).</summary>
        private Pool _arrowBacks;

        /// <summary>발밑 안내 화살표: 새로 불난 건물·대형 신고·대화재 쪽을 몇 초 동안 가리킨다.</summary>
        private struct Guide
        {
            public Structure At;
            public float Ttl;
        }

        /// <summary>항구: 불배가 떴을 때 발밑 화살표가 가장 가까운 부두 끝을 가리키는 남은 시간.</summary>
        private float _pierGuideTtl;

        /// <summary>배(또는 소방관)에 가장 가까운 부두 끝 자리(봇이 서는 요격 지점과 같다).</summary>
        private static Vec2 PierTipFor(Vec2 near)
        {
            float bestX = SurvivorHarbor.PierX[0];
            foreach (float px in SurvivorHarbor.PierX)
            {
                if (Mathf.Abs(px - near.X) < Mathf.Abs(bestX - near.X)) bestX = px;
            }
            return new Vec2(bestX, SurvivorHarbor.PierTip - 1.5f);
        }

        /// <summary>떠 있는 불배 중 부두에 가장 가까운 것(없으면 null).</summary>
        private Structure FloatingBoat()
        {
            Structure best = null;
            foreach (Structure s in _sim.Structures)
            {
                if (s.Kind != StructureKind.Boat || s.Collapsed || s.Docked || !s.Burning || s.Tanker) continue;
                if (best == null || s.Pos.Y < best.Pos.Y) best = s;
            }
            return best;
        }

        private readonly List<Guide> _guides = new List<Guide>();
        private readonly List<Image> _stars = new List<Image>();
        private static Sprite _arrowSprite;

        private GameObject _player;

        /// <summary>대원·구해 낸 시민·갇힌 사람 3D 모델.</summary>
        private PersonPool _people;

        // 아이템 3D 모델: 드론·포탑·물폭탄·헬기·비행기(기본 도형 조립)와 달리는 소방차(Kenney).
        private ModelPool _droneModels;
        private ModelPool _turretModels;
        private ModelPool _bombModels;
        private ModelPool _heliModels;
        private ModelPool _planeModels;
        private ModelPool _truckModels;
        private ModelPool _ambulanceModels;
        private ModelPool _boatModels;
        private ModelPool _tankerModels;
        private ModelPool _fireboatModels;
        private readonly List<ModelPool> _modelPools = new List<ModelPool>();
        private static readonly string[] CivilianModels = { "People/Casual_Female", "People/OldClassy_Male", "People/Casual_Male" };
        private SpriteRenderer _playerGlow;
        private readonly List<Pool> _pools = new List<Pool>();
        private readonly List<RibbonPool> _ribbons = new List<RibbonPool>();

        // --- 수명이 있는 효과 ---
        private readonly List<Particle> _particles = new List<Particle>();
        private readonly Stack<SpriteRenderer> _particlePool = new Stack<SpriteRenderer>();
        private readonly List<Number> _numbers = new List<Number>();
        private readonly Stack<TextMesh> _numberPool = new Stack<TextMesh>();
        private readonly List<SpriteRenderer> _scorch = new List<SpriteRenderer>();
        private int _scorchNext;

        private static Material _additive;
        private static Sprite _ringSprite;
        private static Sprite _beamSprite;
        private static Sprite _bubbleSprite;
        private static Texture2D _flowTexture;
        private static Sprite _discSprite;
        private readonly List<Shot> _chain = new List<Shot>();
        private readonly List<Vector3> _streamPts = new List<Vector3>();
        private readonly List<Vec2> _ribbonIn = new List<Vec2>();
        private readonly List<WaterRibbon.Point> _ribbon = new List<WaterRibbon.Point>();
        private readonly List<WaterRibbon.Blob> _blobs = new List<WaterRibbon.Blob>();
        private Material _spriteMaterial;

        /// <summary>불·빛·불똥은 더해 그려야 겹칠수록 환하게 타오른다(기본 스프라이트는 겹치면 탁해진다).</summary>
        private static Material Additive
        {
            get
            {
                if (_additive == null)
                {
                    Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
                    if (shader != null) _additive = new Material(shader);
                }
                return _additive;
            }
        }

        private AudioSource _chime;
        private AudioClip _chimeClip;
        private AudioSource _spray;
        private AudioSource _sfx;
        private AudioClip _sizzleClip;
        private float _sizzleClock;

        // --- HUD ---
        private Image _vignette;
        private Image _flashImage;
        private Image _xpFill;
        private RectTransform _xpBack;
        private Text _level;
        private Text _timer;
        private Text _kills;
        /// <summary>월드 글자 태그(마감 게이지 "N초 · N명", 화살표 라벨). 프레임마다 쓴 만큼만 켜 둔다.</summary>
        private readonly List<TextMesh> _tags = new List<TextMesh>();
        private int _tagsUsed;
        private Text _build;
        private Text _alert;
        private Image _bossBack;
        private Text _bossName;
        private Image _bossFill;
        private Image _bossBand;
        private Text _bossBandText;
        private RectTransform _cardLayer;
        private readonly List<RectTransform> _cards = new List<RectTransform>();
        private Image _resultBack;
        private Text _result;

        private struct Particle
        {
            public SpriteRenderer R;
            public Vector3 Pos;
            public Vector3 Vel;
            public float Drag;
            public float Age;
            public float Life;
            public float Size0;
            public float Size1;
            public float Rot;
            public float Spin;
            public float Gravity;
            public float Stretch;
            public Color C0;
            public Color C1;
            public float Unit;
        }

        private struct Number
        {
            public TextMesh T;
            public Vector3 Pos;
            public Vector3 Vel;
            public float Age;
            public float Life;
            public float Size;
        }

        /// <param name="maxGear">true면 모든 아이템을 최대로 든 채 시작한다(연출 확인용).</param>
        public SurvivorView(Transform parent, Camera camera, Canvas canvas, bool maxGear = false, int stage = 1)
        {
            _stage = stage;
            _maxGear = maxGear;
            _camera = camera;
            _root = new GameObject("SurvivorWorld").transform;
            _root.SetParent(parent, false);
            _world = new GameObject("Dynamic").transform;
            _world.SetParent(_root, false);

            // 월드 카메라가 텍스처에 그리고, 원래 카메라는 멀리서 그 텍스처(보정 셰이더)와 HUD만 그린다.
            _cameraHome = camera.transform.position;
            _cameraHomeSize = camera.orthographicSize;
            float aspect = camera.aspect > 0.1f ? camera.aspect : 16f / 9f;
            int height = Mathf.Clamp(camera.pixelHeight, 540, MaxWorldHeight);
            _worldRt = new RenderTexture(Mathf.RoundToInt(height * aspect), height, 24, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Bilinear,
                antiAliasing = 4,
                name = "WorldScreen",
            };
            var eye = new GameObject("WorldCamera");
            eye.transform.SetParent(_root, false);
            _worldCam = eye.AddComponent<Camera>();
            _worldCam.orthographic = false;
            _worldCam.fieldOfView = WorldFov;
            _worldCam.nearClipPlane = 1f;
            _worldCam.farClipPlane = 300f;
            _worldCam.clearFlags = CameraClearFlags.SolidColor;
            _worldCam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
            _worldCam.targetTexture = _worldRt;
            camera.transform.position = ScreenSpot;
            _worldScreen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _worldScreen.name = "WorldScreen";
            Collider screenCollider = _worldScreen.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(screenCollider);
            else Object.DestroyImmediate(screenCollider);
            _worldScreen.transform.SetParent(camera.transform, false);
            _worldScreen.transform.localPosition = new Vector3(0f, 0f, 5f);
            _worldScreen.transform.localScale = new Vector3(camera.orthographicSize * 2f * aspect, camera.orthographicSize * 2f, 1f);
            // 화면 보정(채도·색조·햇빛 띠·비네트). 셰이더를 못 찾으면 보정 없이 그대로 옮긴다.
            Shader grade = Resources.Load<Shader>("Shaders/PixelGrade");
            if (grade == null)
            {
                Debug.LogWarning("[SurvivorView] Shaders/PixelGrade 없음: 보정 없이 그린다.");
                grade = Shader.Find("Unlit/Texture");
            }
            var screenMat = new Material(grade) { mainTexture = _worldRt };
            _worldScreen.GetComponent<MeshRenderer>().sharedMaterial = screenMat;
            // 블룸: 월드 카메라만 후처리한다(HUD는 선명하게). 문턱 1이라 밝은 지붕·차는 그대로 두고,
            // 가산으로 겹쳐 1을 넘는 불·빛·불티만 번진다(HDR 버퍼).
            _worldCam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var volume = new GameObject("WorldVolume").AddComponent<UnityEngine.Rendering.Volume>();
            volume.transform.SetParent(_root, false);
            volume.isGlobal = true;
            _worldPost = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            volume.sharedProfile = _worldPost;
            var bloom = _worldPost.Add<UnityEngine.Rendering.Universal.Bloom>(true);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.8f);
            bloom.scatter.Override(0.65f);
            // 해: 왼쪽 위에서 땅(+Z)으로 비스듬히 비춰 모델이 오른쪽 아래로 부드러운 그림자를 드리운다(야시장은 BuildGround가 밤으로 낮춘다).
            _sun = new GameObject("Sun").AddComponent<Light>();
            _sun.transform.SetParent(_root, false);
            _sun.type = LightType.Directional;
            _sun.intensity = 0.8f;
            _sun.color = new Color(1f, 0.96f, 0.88f);
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = 0.55f;
            _sun.transform.localRotation = Quaternion.LookRotation(new Vector3(-0.45f, 0.35f, 1f), Vector3.back);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.52f);
            _hud = UiKit.Node(canvas.transform, "SurvivorHud");
            UiKit.Stretch(_hud);

            BuildPools();
            BuildHud();
            // 조이스틱은 HUD 맨 위에(카드·결과창은 따로 켜고 끈다).
            foreach (Image stick in new[] { _leftRing, _leftKnob, _rightRing, _rightKnob }) stick.transform.SetAsLastSibling();
            _stationLayer = UiKit.Node(canvas.transform, "Station");
            UiKit.Stretch(_stationLayer);
            _station = FireStation.Parse(PlayerPrefs.GetString(StationKey, ""));
            BuildAudio();
            Restart(_seed);
            // 판은 소방서에서 소방관을 고르고 "출동"해야 시작한다.
            OpenStation();
        }

        public SurvivorSim Sim
        {
            get { return _sim; }
        }

        public void Restart(int seed)
        {
            _seed = seed;
            _sim = new SurvivorSim(seed, _stage, _station != null ? _station.StartFor(SurvivorStages.Get(_stage)) : null);
            if (_maxGear) _sim.GiveMaxGear();
            _earned = 0;
            if (_groundStage != _sim.Stage.Number)
            {
                // 스테이지가 바뀌면 바닥(마을 돌길·숲 흙길)을 새로 깐다.
                BuildGround();
                _groundStage = _sim.Stage.Number;
            }
            _accumulator = 0f;
            _trauma = 0f;
            _flash = 0f;
            _hurt = 0f;
            _slowmo = 0f;
            _overAge = 0f;
            _alertAge = 99f;
            _bossBannerAge = 99f;
            _shownXp = 0f;
            _heliExitAge = 99f;
            _hitStop = 0f;
            _zoomKick = 0f;
            _recoil = 0f;
            _stance = 0f;
            _cardsIn = -1f;
            _waveAge = 99f;
            _hurtClock = 0f;
            _comboPeak = 0;
            _toolboxesShown = 0;
            _chestsShown = 0;
            _kitsShown = 0;
            _guides.Clear();
            foreach (TextMesh t in _tags)
            {
                if (t != null) UiKit.Discard(t.gameObject);
            }
            _tags.Clear();
            _tagsUsed = 0;
            _planeAge = 99f;
            _ambulanceAge = 99f;
            _truckShown = false;
            _fireboatShown = false;
            _waveHitQuay = false;
            _lastPlayer = new Vector3(_sim.Player.X, _sim.Player.Y, 0f);
            _stepClock = 0f;
            _regenClock = 0f;
            _aim = Vector3.right;
            _aimAge = 99f;
            _touchAim = new Vec2(1f, 0f);
            _trail.Clear();
            _stick.Clear();
            _suitShown = -1;
            _lastOutcome = SOutcome.Playing;
            _cameraAt = new Vector3(_sim.Player.X, _sim.Player.Y, -10f);
            ClearEffects();
            BuildModels();
            BuildSigns();
            HideCards();
            _resultBack.gameObject.SetActive(false);
            // 판 시작: "STAGE 2 · 산불 숲" 띠가 파랗게 지나가고, 1.5초 뒤 할 일 한 줄이 알림으로 뜬다.
            _bossBandText.text = "STAGE " + _sim.Stage.Number + " · " + _sim.Stage.Name;
            _bandTint = new Color(0.05f, 0.25f, 0.6f);
            _bossBannerAge = 0f;
            _goalAlertAt = _time + 1.5f;
            Refresh(0f);
        }

        /// <summary>저장해 둔 스테이지(없으면 1).</summary>
        public static int SavedStage()
        {
            int n = PlayerPrefs.GetInt(StageKey, 1);
            // 시작 스테이지를 박아 넣은 빌드(tools/ios-install.sh --stage N)는 그 빌드의 첫 실행만 그 스테이지로 연다.
            var start = Resources.Load<TextAsset>("ProtoStartStage");
            if (start != null && int.TryParse(start.text.Trim(), out int forced) && PlayerPrefs.GetString(StartBuildKey, "") != Application.buildGUID)
            {
                n = forced;
                PlayerPrefs.SetString(StartBuildKey, Application.buildGUID);
                PlayerPrefs.SetInt(StageKey, n);
                PlayerPrefs.Save();
            }
            return n >= 1 && n <= SurvivorStages.Count ? n : 1;
        }

        // ------------------------------------------------------------------
        // 소방서: 판 사이에 남는 것. 별로 소방관을 해금하고 골라 출동한다.
        // ------------------------------------------------------------------

        public bool StationOpen
        {
            get { return _stationOpen; }
        }

        public FireStation Station
        {
            get { return _station; }
        }

        /// <summary>캡처·테스트용: 저장 글로 소방서를 바꿔 끼운다(저장하지 않는다). 열려 있으면 다시 그린다.</summary>
        public void LoadStation(string text)
        {
            _station = FireStation.Parse(text);
            _stationPersist = false;
            Restart(_seed);
            if (_stationOpen) OpenStation();
        }

        private void SaveStation()
        {
            if (!_stationPersist || !Application.isPlaying) return;
            PlayerPrefs.SetString(StationKey, _station.Serialize());
            PlayerPrefs.Save();
        }

        /// <summary>소방서를 연다(열려 있으면 다시 그린다). 판은 그동안 멈춘다.</summary>
        public void OpenStation()
        {
            ClearStation();
            _stationOpen = true;

            Image dim = UiKit.Image(_stationLayer, "Dim", Art.White, new Color(0.02f, 0.03f, 0.08f, 0.82f));
            UiKit.Stretch(dim.rectTransform);

            Text title = UiKit.OutlinedLabel(_stationLayer, "Title", "소방서", 84, new Color(1f, 0.9f, 0.4f), TextAnchor.MiddleCenter);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 400f), new Vector2(900f, 110f));

            // 스테이지별 최고 별은 아래 스테이지 버튼에 있다.
            Text stars = UiKit.OutlinedLabel(_stationLayer, "Stars", "모은 별 ★ " + _station.Stars, 36, Color.white, TextAnchor.MiddleCenter);
            UiKit.Place(stars.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 318f), new Vector2(1500f, 50f));

            // 스테이지 선택: 다섯 다 열려 있다. 고른 것은 노란 버튼, 각 버튼에 이름과 최고 별. 탭하면 그 스테이지의 브리핑·대비 장비로 다시 연다.
            // 280폭 × 5, 간격 290, 가운데 정렬: 4:3(폭 1440)에도 들어간다.
            StageRules rules = SurvivorStages.Get(_stage);
            const float stageStep = 290f;
            float stageX0 = -(SurvivorStages.Count - 1) * stageStep / 2f;
            for (int n = 1; n <= SurvivorStages.Count; n++)
            {
                int stage = n;
                bool on = stage == _stage;
                string label = n + " " + SurvivorStages.Get(n).Name + "  ★" + _station.Best[n];
                Button pick = UiKit.Button(_stationLayer, "Stage" + n, Art.Get(on ? "UI/button_yellow" : "UI/button_blue"), label, 28, () => TapStage(stage));
                UiKit.Place(pick.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(stageX0 + ((n - 1) * stageStep), 268f), new Vector2(280f, 56f));
            }

            // 브리핑: 다음 스테이지의 위협 한 줄과, 그 위협을 막는 대비 장비 둘(하나를 골라 Lv1로 들고 간다).
            // 위협·할 일 두 줄 + 대비 버튼이 스테이지 버튼(268)과 소방관 카드(위 150) 사이 약 110px에 들어간다.
            Text threat = UiKit.OutlinedLabel(_stationLayer, "Threat", "위협: " + rules.Threat, 28, new Color(1f, 0.75f, 0.55f), TextAnchor.MiddleCenter);
            UiKit.Place(threat.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 222f), new Vector2(1400f, 36f));
            // 할 일 한 줄: 이 스테이지에서 플레이어가 새로 배우는 결정("부두 끝에 서서 바다 위 배를 쏘아 끈다"). 판 시작에도 같은 줄이 알림으로 뜬다.
            Text goal = UiKit.OutlinedLabel(_stationLayer, "Goal", "할 일: " + rules.Goal, 28, GoalGreen, TextAnchor.MiddleCenter);
            UiKit.Place(goal.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(1400f, 36f));
            UpgradeId[] counters = rules.Counters ?? new UpgradeId[0];
            for (int k = 0; k < counters.Length; k++)
            {
                UpgradeId c = counters[k];
                bool on = _station.Prep == c;
                Button prep = UiKit.Button(_stationLayer, "Prep" + k, Art.Get(on ? "UI/button_yellow" : "UI/button_blue"), (on ? "대비 ✓ " : "대비: ") + SurvivorUpgrades.Name(c), 30, () => TapPrep(c));
                UiKit.Place(prep.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2((k - ((counters.Length - 1) / 2f)) * 420f, 140f), new Vector2(400f, 50f));
            }

            Firefighter[] roster = Roster.All;
            for (int i = 0; i < roster.Length; i++)
            {
                Firefighter f = roster[i];
                bool unlocked = _station.IsUnlocked(f.Id);
                bool selected = _station.Selected == f.Id;
                bool affordable = _station.CanUnlock(f.Id);
                string sprite = selected ? "UI/button_yellow" : unlocked ? "UI/button_blue" : affordable ? "UI/button_green" : "UI/button_grey";
                string id = f.Id;
                Button card = UiKit.Button(_stationLayer, "Firefighter" + i, Art.Get(sprite), "", 0, () => TapFirefighter(id));
                RectTransform rect = card.GetComponent<RectTransform>();
                float x = (i - ((roster.Length - 1) / 2f)) * 330f;
                // 카드는 위 110까지(위협·할 일·대비 버튼 세 줄 자리를 비운다).
                UiKit.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(x, -90f), new Vector2(310f, 400f));

                Text name = UiKit.OutlinedLabel(rect, "Name", f.Name, 42, Color.white, TextAnchor.MiddleCenter);
                UiKit.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(290f, 60f));
                string gear = "";
                foreach (UpgradeId u in f.Start) gear += (gear.Length > 0 ? " + " : "") + SurvivorUpgrades.Name(u);
                Text gearLabel = UiKit.OutlinedLabel(rect, "Gear", gear, 28, new Color(1f, 0.95f, 0.6f), TextAnchor.UpperCenter);
                UiKit.Place(gearLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(280f, 80f));
                gearLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
                Text story = UiKit.OutlinedLabel(rect, "Story", f.Story, 26, Color.white, TextAnchor.UpperCenter);
                UiKit.Place(story.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(270f, 120f));
                story.horizontalOverflow = HorizontalWrapMode.Wrap;
                string state = selected ? "출동 대기" : unlocked ? "탭하면 선택" : affordable ? "탭하면 해금  ★" + f.Cost : "해금  ★" + f.Cost + " 필요";
                Color stateColor = selected ? new Color(1f, 0.9f, 0.4f) : unlocked ? Color.white : affordable ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.6f, 0.6f);
                Text stateLabel = UiKit.OutlinedLabel(rect, "State", state, 30, stateColor, TextAnchor.MiddleCenter);
                UiKit.Place(stateLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(290f, 50f));
            }

            Button go = UiKit.Button(_stationLayer, "Go", Art.Get("UI/button_red"), "출동!", 56, CloseStation);
            UiKit.Place(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -375f), new Vector2(460f, 110f));
            Text hint = UiKit.OutlinedLabel(_stationLayer, "Hint", "스테이지 · 대비 장비 · 소방관을 골라 탭  ·  출동은 버튼 또는 Enter", 26, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -455f), new Vector2(1000f, 40f));
        }

        /// <summary>스테이지 칸을 눌렀다: 그 스테이지로 바꾸고(대비 장비는 내려놓는다) 소방서를 다시 연다. 전부 열려 있다.</summary>
        public void TapStage(int stage)
        {
            if (stage == _stage) return;
            _stage = stage;
            _station.Prep = null;
            PlayerPrefs.SetInt(StageKey, stage);
            PlayerPrefs.Save();
            Restart(_seed);
            OpenStation();
        }

        /// <summary>대비 장비 칸을 눌렀다: 같은 걸 누르면 내려놓고, 다른 걸 누르면 바꿔 든다. 판을 그 장비로 다시 연다.</summary>
        public void TapPrep(UpgradeId id)
        {
            _station.Prep = _station.Prep == id ? (UpgradeId?)null : id;
            Restart(_seed);
            OpenStation();
        }

        /// <summary>소방관 칸을 눌렀다: 해금했으면 고르고, 별이 모자라지 않으면 해금한다. 판을 그 장비로 다시 연다.</summary>
        public void TapFirefighter(string id)
        {
            bool changed = _station.IsUnlocked(id) ? _station.Select(id) : _station.Unlock(id);
            if (!changed) return;
            SaveStation();
            Restart(_seed);
            OpenStation();
        }

        /// <summary>출동: 소방서를 닫고 판을 시작한다.</summary>
        public void CloseStation()
        {
            if (!_stationOpen)
            {
                ClearStation();
                return;
            }
            ClearStation();
            _stationOpen = false;
            // 소방서에 있는 동안 흘러간 시작 띠와 할 일 알림을 다시 띄운다.
            _bossBandText.text = "STAGE " + _sim.Stage.Number + " · " + _sim.Stage.Name;
            _bandTint = new Color(0.05f, 0.25f, 0.6f);
            _bossBannerAge = 0f;
            _goalAlertAt = _time + 1.5f;
            _accumulator = 0f;
        }

        private void ClearStation()
        {
            if (_stationLayer == null) return;
            for (int i = _stationLayer.childCount - 1; i >= 0; i--) UiKit.Discard(_stationLayer.GetChild(i).gameObject);
        }

        /// <summary>스테이지를 바꾸고 새 판을 연다.</summary>
        private void GoToStage(int stage)
        {
            _stage = stage;
            // 대비 장비는 스테이지마다 다시 고른다(위협이 다르다).
            if (_station != null) _station.Prep = null;
            PlayerPrefs.SetInt(StageKey, stage);
            PlayerPrefs.Save();
            Restart(_seed + 1);
        }

        public void Destroy()
        {
            GameAudio.SetFireLevel(0f, 1f);
            // 공유 카메라(다른 시험판도 쓴다)를 원래 자리로 되돌린다.
            if (_camera != null)
            {
                _camera.transform.position = _cameraHome;
                _camera.orthographicSize = _cameraHomeSize;
            }
            if (_worldCam != null) _worldCam.targetTexture = null;
            if (_worldPost != null)
            {
                if (Application.isPlaying) Object.Destroy(_worldPost);
                else Object.DestroyImmediate(_worldPost);
            }
            UiKit.Discard(_worldScreen);
            UiKit.Discard(_root.gameObject);
            UiKit.Discard(_hud.gameObject);
            if (_stationLayer != null) UiKit.Discard(_stationLayer.gameObject);
            if (_worldRt != null)
            {
                _worldRt.Release();
                if (Application.isPlaying) Object.Destroy(_worldRt);
                else Object.DestroyImmediate(_worldRt);
            }
        }

        // ------------------------------------------------------------------
        // 진행
        // ------------------------------------------------------------------

        public void Tick(float dt, in ProtoInput input)
        {
            List<Finger> fingers = input.Fingers ?? NoFingers;
            if (fingers.Count > 0) _touch = true;
            // 카드·결과창 중에도 손가락을 먹여서, 그 사이 뗀 손가락이 계속 눌린 채 남지 않게 한다.
            if (_touch) _stick.Feed(fingers, _camera.pixelWidth, _camera.pixelHeight / UiKit.ReferenceHeight);

            if (input.NextStage)
            {
                GoToStage(SurvivorStages.Next(_stage));
                return;
            }

            if (_stationOpen)
            {
                // 소방서: 판은 멈춰 있고, 출동 버튼이나 Enter로 나간다. 소방관 칸은 버튼이 받는다.
                if (input.EndTurn) CloseStation();
                else
                {
                    Refresh(dt);
                    return;
                }
            }

            if (_sim.Outcome != SOutcome.Playing)
            {
                if (_overAge > 1f && input.MouseClicked)
                {
                    // 이기면 다음 스테이지, 지면 같은 스테이지. 어느 쪽이든 소방서를 거쳐 다시 출동한다.
                    if (_sim.Outcome == SOutcome.Won) GoToStage(SurvivorStages.Next(_stage));
                    else Restart(_seed + 1);
                    OpenStation();
                    return;
                }
            }
            else if (_sim.PendingChoices != null)
            {
                if (_cardAge > 0.35f)
                {
                    if (input.Key1) Choose(0);
                    else if (input.Key2) Choose(1);
                    else if (input.Key3) Choose(2);
                }
            }
            else if (_hitStop > 0f)
            {
                // 히트스톱: 아주 잠깐 멈춰 "쾅"을 느끼게 한다(화면 효과는 계속 움직인다).
                _hitStop -= dt;
            }
            else
            {
                _accumulator += dt * (_slowmo > 0f ? 0.3f : 1f);
                var move = new Vec2(input.Move.x, input.Move.y);
                if (_touch)
                {
                    // 폰: 왼손 스틱으로 걷고, 오른손이 닿아 있는 동안 쏜다. 끈 방향 근처 불로 당겨 주고,
                    // 끌지 않고 누르기만 하면 가장 가까운 불을 겨눈다. 방향은 부드럽게 돌고 손을 떼도 남는다.
                    if (_stick.LeftOn) move = _stick.Move;
                    _sim.AimTargets(_fires);
                    Vec2 want = AimAssist.Desired(_sim.Player, _touchAim, _stick.AimFresh, _stick.Aim, _fires);
                    _touchAim = AimAssist.Turn(_touchAim, want, AimAssist.TurnRate * dt);
                    _sim.Aim = _touchAim;
                    _sim.Spraying = _stick.Firing;
                    _hasMouse = false;
                }
                else
                {
                    // 호스: 마우스로 겨누고 왼쪽 버튼을 쥔 동안 쏜다.
                    // 기운 월드 카메라의 광선이 땅(z=0)에 닿는 곳을 겨눈다.
                    Ray ray = _worldCam.ViewportPointToRay(new Vector3(input.Mouse.x / Mathf.Max(1f, _camera.pixelWidth), input.Mouse.y / Mathf.Max(1f, _camera.pixelHeight), 0f));
                    _mouseAt = _world.InverseTransformPoint(ray.GetPoint(Mathf.Abs(ray.direction.z) > 0.0001f ? -ray.origin.z / ray.direction.z : 0f));
                    _mouseAt.z = 0f;
                    _hasMouse = true;
                    _sim.Aim = new Vec2(_mouseAt.x - _sim.Player.X, _mouseAt.y - _sim.Player.Y);
                    _sim.Spraying = input.MouseHeld;
                }
                while (_accumulator >= SurvivorSim.Dt && _sim.PendingChoices == null && _sim.Outcome == SOutcome.Playing)
                {
                    _accumulator -= SurvivorSim.Dt;
                    Step(move);
                }
                if (_sim.PendingChoices != null) _accumulator = 0f;
            }

            DrawSticks();
            Refresh(dt);
        }

        /// <summary>폰 조이스틱: 누른 자리에 받침 고리, 엄지 자리에 손잡이. 쏘는 동안 오른쪽 손잡이는 물빛.</summary>
        private void DrawSticks()
        {
            bool live = _touch && _sim.Outcome == SOutcome.Playing && _sim.PendingChoices == null;
            PlaceStick(_leftRing, _leftKnob, live && _stick.LeftOn, _stick.LeftBase, _stick.LeftKnob, Color.white);
            PlaceStick(_rightRing, _rightKnob, live && _stick.RightOn, _stick.RightBase, _stick.RightKnob, new Color(0.55f, 0.85f, 1f));
        }

        private void PlaceStick(Image ring, Image knob, bool on, Vec2 origin, Vec2 thumb, Color tint)
        {
            ring.gameObject.SetActive(on);
            knob.gameObject.SetActive(on);
            if (!on) return;
            ring.rectTransform.anchoredPosition = ToHud(origin);
            knob.rectTransform.anchoredPosition = ToHud(thumb);
            knob.color = new Color(tint.r, tint.g, tint.b, 0.6f);
        }

        /// <summary>
        /// 화면 픽셀 → HUD 좌표. 캔버스는 높이 1080 기준으로 맞추고(CanvasScaler) HUD 중심이 화면 중심이다.
        /// 카메라 광선으로 바꾸면 카메라가 따라 움직인 뒤 캔버스가 아직 안 따라온 프레임에 어긋난다.
        /// </summary>
        private Vector2 ToHud(Vec2 screen)
        {
            float k = UiKit.ReferenceHeight / _camera.pixelHeight;
            return new Vector2((screen.X - (_camera.pixelWidth * 0.5f)) * k, (screen.Y - (_camera.pixelHeight * 0.5f)) * k);
        }

        /// <summary>한 틱 진행하고 그 틱의 효과·소리를 만든다. 하니스도 이걸로 판을 굴린다.</summary>
        public void Step(Vec2 move)
        {
            _sim.Step(move.X, move.Y);
            React();
        }

        public void Choose(int index)
        {
            if (_sim.PendingChoices == null || index < 0 || index >= _sim.PendingChoices.Count) return;
            _lastPick = _sim.PendingChoices[index];
            _sim.Choose(index);
            _cardsIn = -1f;
            HideCards();
            // 보물상자: 한 장 고르면 다음 장이 곧바로 이어서 뜬다.
            if (_sim.PendingChoices != null)
            {
                _cardsIn = 0.25f;
                _cardAge = -1f;
            }
            if (_sim.JustEvolved)
            {
                // 진화: 금빛 물줄기가 사방으로 뻗고 고리가 두 겹 퍼진다. 화면이 확 다가왔다가 느리게 흐른다.
                var gold = new Color(1f, 0.85f, 0.3f);
                Vector3 at = W(_sim.Player);
                for (int i = 0; i < 24; i++)
                {
                    float a = i * Mathf.PI * 2f / 24f;
                    var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    EmitSprite(BeamSprite(), at + (dir * 1.2f), dir * 16f, 2.5f, 0.45f, 0.7f, 0.25f, new Color(1f, 0.9f, 0.5f, 1f), new Color(1f, 0.7f, 0.2f, 0f), 0f, true,
                        0f, 5f, (a * Mathf.Rad2Deg) - 90f);
                }
                Shockwave(at, gold, 10f, 0.6f);
                Shockwave(at, new Color(1f, 0.95f, 0.7f), 16f, 0.7f, 0.12f);
                Pillar(at, gold);
                Burst(at, 40, gold, 12f);
                _trauma = Mathf.Min(1f, _trauma + 0.8f);
                _slowmo = 0.8f;
                _zoomKick = 1f;
                Flash(gold, 0.3f);
                GameAudio.Play(Cue.Won);
                ShowAlert("진화! 고압 방수포", gold);
            }
            else if (_sim.JustMaxed.HasValue)
            {
                MaxBurst(_sim.JustMaxed.Value);
            }
            else
            {
                GameAudio.Play(Cue.PickUp);
            }
            bool joined = _lastPick == UpgradeId.Partner && _sim.Build.Level(UpgradeId.Partner) is 1 or 3 or 5;
            if ((Loadout.IsSpecial(_lastPick) && !Loadout.IsEvolution(_lastPick)) || joined)
            {
                // 특수 장비: 금빛 기둥과 알림. 구조대원은 한 명 늘 때마다 곁에서 번쩍이며 나타난다.
                var gold = new Color(1f, 0.85f, 0.3f);
                Vector3 at = W(_sim.Player);
                Pillar(at, gold);
                Shockwave(at, gold, 6f, 0.4f);
                Flash(gold, 0.12f);
                _zoomKick = Mathf.Max(_zoomKick, 0.5f);
                ShowAlert(joined ? "구조대원 합류!" : SurvivorUpgrades.Name(_lastPick) + " 출동!", gold);
            }
        }

        private UpgradeId _lastPick;

        /// <summary>무기·보조가 Lv5가 된 순간: 금빛 기둥, 두 겹 충격파, 불똥, 슬로모션, "○○ MAX!".</summary>
        private void MaxBurst(UpgradeId id)
        {
            var gold = new Color(1f, 0.85f, 0.3f);
            Vector3 at = W(_sim.Player);
            Pillar(at, gold);
            Shockwave(at, gold, 9f, 0.55f);
            Shockwave(at, new Color(1f, 0.97f, 0.8f), 14f, 0.65f, 0.12f);
            Burst(at, 30, gold, 11f);
            Sparkle(at, 14, new Color(1f, 0.95f, 0.6f));
            _trauma = Mathf.Min(1f, _trauma + 0.5f);
            _slowmo = Mathf.Max(_slowmo, 0.5f);
            _zoomKick = Mathf.Max(_zoomKick, 0.7f);
            Flash(gold, 0.2f);
            GameAudio.Play(Cue.Won);
            ShowAlert(SurvivorUpgrades.Name(id) + " MAX!", gold);
        }

        private void React()
        {
            int kills = 0;
            foreach (Hit h in _sim.Hits)
            {
                if (h.Killed) kills++;
            }
            // 한 틱에 너무 많이 꺼지면(방수포 한 바퀴) 수증기를 줄여 화면이 하얗게 덮이지 않게 한다.
            bool crowded = kills > 12;
            System.Array.Clear(_impacts, 0, _impacts.Length);
            foreach (Hit h in _sim.Hits)
            {
                Vector3 at = W(h.Pos);
                WeaponHit(h, at);
                if (h.Killed) DeathBurst(at, h.Kind, crowded);
                else
                {
                    HitSplash(at, Away(h.Pos));
                    Sizzle();
                }
                if (h.Damage >= 0.5f) SpawnNumber(at, h.Damage, h.Crit);
            }
            if (kills > 0)
            {
                _trauma = Mathf.Min(0.55f, _trauma + (0.012f * kills));
                if (_putOutClock <= 0f)
                {
                    GameAudio.Play(Cue.PutOut);
                    _putOutClock = 0.07f;
                }
            }

            // 물에 꺼진 바닥 불: 하얀 김 + 치익.
            foreach (Vec2 e in _sim.Extinguished)
            {
                Steam(W(e), 3, 0.9f);
                if (_putOutClock <= 0f)
                {
                    GameAudio.Play(Cue.PutOut);
                    _putOutClock = 0.07f;
                }
            }
            // 끄지 않은 바닥 불에서 불씨가 다시 일어났다: 주황 고리 + 불똥 + 점화음.
            foreach (Vec2 e in _sim.Reignited)
            {
                Shockwave(W(e), new Color(1f, 0.5f, 0.1f, 0.9f), 2.5f, 0.3f);
                Burst(W(e), 10, new Color(1f, 0.6f, 0.15f), 5f);
                if (_igniteClock <= 0f)
                {
                    GameAudio.Play(Cue.SecondIgnition);
                    _igniteClock = 0.25f;
                }
            }

            // 헬기 출동: 막 뜬 헬기(나이 한 틱)마다 금색 배너.
            if (_sim.Shots.Exists(s => s.Kind == ShotKind.Heli && s.Age < SurvivorSim.Dt * 1.5f)) SpecialBanner("소방 헬기 급수 투하!", new Color(0.5f, 0.85f, 1f));

            foreach (Vec2 e in _sim.HeliDrops)
            {
                Vector3 at = W(e);
                // 임팩트: 큰 물기둥·플레어·충격파 두 겹·흔들림·멈칫, 물보라 고리, 바닥에 오래 남는 큰 젖은 자국.
                WaterBlast(at, SurvivorSim.HeliRadius * 1.3f, Loadout.MaxLevel);
                Flare(at, SurvivorSim.HeliRadius * 3.1f, new Color(0.75f, 0.95f, 1f), 5);
                Shockwave(at, new Color(0.9f, 0.97f, 1f, 0.9f), SurvivorSim.HeliRadius * 4f, 0.6f, 0.08f);
                Shockwave(at, new Color(0.7f, 0.9f, 1f, 0.8f), SurvivorSim.HeliRadius * 2.7f, 0.5f, 0.23f);
                for (int i = 0; i < 24; i++)
                {
                    float a = i * Mathf.PI * 2f / 24f;
                    float sp = Random.Range(6f, 9f);
                    EmitFalling("Effects/water_drop", at + new Vector3(0f, 0.4f, 0f), new Vector3(Mathf.Cos(a) * sp, (Mathf.Sin(a) * sp * 0.6f) + 4f, 0f), 0.7f, 0.45f, new Color(0.8f, 0.95f, 1f, 1f));
                }
                AddWet(at, SurvivorSim.HeliRadius * 2f, 6f, true);
                _trauma = Mathf.Min(1f, _trauma + 0.5f);
                HitStop(0.06f);
                PlaySplash();
                // 헬기는 쏟고 나서 같은 방향으로 계속 날아가 화면 밖으로 빠진다.
                _heliExitFrom = at + HeliLift;
                _heliExitDir = (new Vector3(1.4f, -1f, 0f)).normalized;
                _heliExitAge = 0f;
            }

            // 증기 폭발: 물줄기를 버틴 보상. 지붕에서 김이 확 솟고 둘레로 충격파, 잠깐 멈칫, 치이익.
            foreach (Structure st in _sim.SteamBursts)
            {
                Vector3 at = W(st.Pos);
                Steam(at, 30, 2.6f);
                SteamPillar(at, 2f);
                Shockwave(at, new Color(0.95f, 0.98f, 1f, 0.9f), SurvivorSim.SteamRadius * 2.5f, 0.5f, 0.1f);
                Flare(at, SurvivorSim.SteamRadius * 1.5f, Color.white, 3);
                SpawnText(at + new Vector3(0f, 2.8f, 0f), "증기 폭발!", new Color(0.8f, 0.95f, 1f), 1.6f);
                _trauma = Mathf.Min(1f, _trauma + 0.3f);
                HitStop(0.05f);
                PlaySteam();
            }

            // 드론 물폭탄 착탄: 청록 물기둥 + 물결 + 물방울.
            foreach (Vec2 d in _sim.DroneDrops)
            {
                Vector3 at = W(d);
                Pillar(at, DroneTint);
                Shockwave(at, DroneTint, 3.6f, 0.3f);
                Splash(at, 10, 0.6f);
                AddWet(at, 1.6f, 1.5f, false);
                if (RoofAt(d)) Steam(at, 4, 1f);
                WeaponSound(Cue.SprayWater);
            }

            // 장화: 젖은 발자국이 남고, 밟아 끈 자리에선 김.
            foreach (Vec2 f in _sim.Footprints)
            {
                Vector3 at = W(f);
                AddWet(at + new Vector3(-0.12f, 0f, 0f), 0.28f, 1.5f, false);
                AddWet(at + new Vector3(0.12f, 0.1f, 0f), 0.28f, 1.5f, false);
            }

            // 방화복: 닿은 불 몹이 튕기며 불똥.
            foreach (Vec2 b in _sim.SuitBounces)
            {
                Vector3 at = W(b);
                Burst(at, 4, new Color(1f, 0.6f, 0.2f), 5f);
                Emit("Effects/glow", at, Vector3.zero, 0f, 0.06f, 0.6f, 1f, new Color(1f, 1f, 1f, 0.7f), new Color(1f, 0.8f, 0.4f, 0f), 0f, true);
            }

            if (_sim.Sprinkled.Count > 0) SpecialBanner("스프링클러 작동!", new Color(0.5f, 0.85f, 1f), quiet: true);
            foreach (Structure st in _sim.Sprinkled)
            {
                // 스프링클러: 지붕 가운데에서 8방향으로 물 돔이 솟고, 지붕 폭만큼 충격파가 퍼지고, 바닥이 파랗게 물들며 김이 오른다.
                Vector3 at = W(st.Pos);
                float width = Mathf.Max(st.Half.X, st.Half.Y) * 2f;
                Vector3 roof = at + new Vector3(0f, 0.6f, 0f);
                SteamPillar(roof, 1.4f);
                Steam(at, 6, 1.4f);
                Shockwave(roof, new Color(0.6f, 0.9f, 1f, 0.9f), width * 1.2f, 0.5f);
                _groundGlow.Put(at, width * 1.4f, 0f, new Color(0.5f, 0.8f, 1f, 0.3f));
                for (int c = 0; c < 8; c++)
                {
                    float a = c * Mathf.PI * 2f / 8f;
                    Vector3 d = new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.6f, 0f);
                    for (int i = 0; i < 3; i++)
                    {
                        var v = (d * Random.Range(4f, 6.5f)) + new Vector3(0f, 5f + Random.Range(0f, 2f), 0f);
                        EmitSprite(BeamSprite(), roof, v, 0f, 0.6f, 0.16f, 0.08f, new Color(0.75f, 0.95f, 1f, 0.95f), new Color(0.7f, 0.9f, 1f, 0f), 0f, true,
                            12f, 6f, (Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg) - 90f);
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        EmitFalling("Effects/water_drop", roof, (d * Random.Range(1.5f, 4f)) + new Vector3(0f, Random.Range(2f, 5f), 0f), 0.7f, 0.3f, new Color(0.75f, 0.95f, 1f, 1f));
                    }
                }
            }
            if (_sim.Sprinkled.Count > 0) GameAudio.Play(Cue.SprayFoam);
            if (_sim.JustRain)
            {
                // 먹구름이 오면 금색 배너와 함께 번개가 번쩍한다. 그 뒤로는 0.7초마다 친다(DrawSpecials).
                SpecialBanner("비구름 소환!", new Color(0.4f, 0.6f, 1f));
                Flash(new Color(0.9f, 0.95f, 1f), 0.3f);
                _trauma = Mathf.Min(1f, _trauma + 0.2f);
                _lightningClock = 0.35f;
                GameAudio.Play(Cue.Backfire);
            }
            if (_sim.JustRetardant && _sim.Retardants.Count > 0)
            {
                // 비행기가 띠를 따라 한 번 지나가며 뿌린다(띠보다 조금 더 길게 날아온다).
                Band b = _sim.Retardants[_sim.Retardants.Count - 1];
                Vector3 a = W(b.A);
                Vector3 c = W(b.B);
                Vector3 dir = (c - a).normalized;
                _planeFrom = a - (dir * 10f);
                _planeTo = c + (dir * 10f);
                _planeAge = 0f;
                SpecialBanner("방염제 살포!", new Color(1f, 0.4f, 0.5f));
                _trauma = Mathf.Min(1f, _trauma + 0.2f);
                GameAudio.Play(Cue.SprayFoam);
            }
            if (_sim.JustFoam && _sim.FoamAt.HasValue)
            {
                // 폼 포탄이 떨어져 터진다: 흰 번쩍 + 흰 거품이 사방으로 튄다.
                Vector3 at = W(_sim.FoamAt.Value);
                float r = SurvivorSim.FoamRadius;
                SpecialBanner("소화 거품 살포!", new Color(0.9f, 1f, 1f));
                Flare(at, r * 2.2f, new Color(0.95f, 1f, 1f), 4);
                Shockwave(at, new Color(1f, 1f, 1f, 0.9f), r * 3.1f, 0.5f);
                Shockwave(at, new Color(0.9f, 0.97f, 1f, 0.8f), r * 2f, 0.45f, 0.15f);
                for (int k = 0; k < 20; k++)
                {
                    float a = k * Mathf.PI / 10f;
                    EmitFalling("Effects/smoke_01", at, new Vector3(Mathf.Cos(a) * 4.5f, 3.5f + Mathf.Sin(a), 0f), 0.9f, 0.6f, new Color(1f, 1f, 1f, 0.95f));
                }
                // 큰 거품 원반 48개가 깔개 위에 천천히 부풀며 폼이 걷힐 때까지 남는다.
                for (int k = 0; k < 48; k++)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    float d = r * 0.9f * Mathf.Sqrt(Random.value);
                    Vector3 o = new Vector3(Mathf.Cos(a) * d, Mathf.Sin(a) * d, 0f);
                    float size = Random.Range(0.7f, 1.3f);
                    EmitSprite(BubbleSprite(), at + o, Vector3.zero, 0f, SurvivorSim.FoamTime, size * 0.8f, size * 1.5f,
                        new Color(1f, 1f, 1f, 0.92f), new Color(0.9f, 0.97f, 1f, 0f), Random.Range(-10f, 10f), false);
                }
                _foamPopClock = 0.2f;
                _trauma = Mathf.Min(1f, _trauma + 0.3f);
                GameAudio.Play(Cue.SprayFoam);
            }
            if (_sim.JustCurtain) CurtainBurst();

            foreach (Vec2 e in _sim.Explosions)
            {
                int lv = _sim.Build.PowerOf(UpgradeId.WaterBomb);
                Vector3 at = W(e);
                WaterBlast(at, _sim.Build.BombRadius, lv);
                Flare(at, _sim.Build.BombRadius * 2.2f, new Color(0.8f, 0.95f, 1f), 3);
                // 착지 순간: 흰 번쩍 + 땅에 남는 물 자국 + 건물이면 지붕에서 김 기둥.
                Emit("Effects/glow", at, Vector3.zero, 0f, 0.1f, _sim.Build.BombRadius * 2.6f, _sim.Build.BombRadius * 3.2f, new Color(1f, 1f, 1f, 0.95f), new Color(0.6f, 0.9f, 1f, 0f), 0f, true);
                // 착탄 자리는 3초 동안 젖은 웅덩이로 남는다.
                AddWet(at, _sim.Build.BombRadius * 2.2f, 3f, false);
                if (RoofAt(e)) SteamPillar(at, 1f);
                _trauma = Mathf.Min(1f, _trauma + 0.18f);
                _zoomKick = Mathf.Max(_zoomKick, 0.12f);
                HitStop(0.03f);
                WeaponSound(Cue.SprayFoam);
            }
            foreach (Vec2 e in _sim.AirBlasts)
            {
                // 공중 소화탄: 물폭탄보다 크고 금빛 고리, 김 기둥 두 배.
                Vector3 at = W(e);
                WaterBlast(at, 2.2f * 1.5f, Loadout.MaxLevel);
                Flare(at, 7f, new Color(1f, 0.9f, 0.6f), 4);
                Emit("Effects/glow", at, Vector3.zero, 0f, 0.12f, 7f, 9f, new Color(1f, 1f, 1f, 1f), new Color(1f, 0.85f, 0.4f, 0f), 0f, true);
                Shockwave(at, new Color(1f, 0.85f, 0.35f, 1f), 12f, 0.5f, 0.05f);
                Pillar(at, new Color(0.5f, 0.85f, 1f));
                AddWet(at, 6f, 1.6f, false);
                SteamPillar(at, 2f);
                _trauma = Mathf.Min(1f, _trauma + 0.28f);
                _zoomKick = Mathf.Max(_zoomKick, 0.2f);
                HitStop(0.04f);
                WeaponSound(Cue.SprayFoam);
            }
            foreach (Vec2 e in _sim.TurretsPlaced)
            {
                // 포탑은 위에서 떨어져 0.2초 뒤 "쿵" 하고 선다.
                _turretLandings.Add(W(e));
                _turretLandingIn.Add(TurretDropTime);
            }

            if (_sim.ShotsFired > 0)
            {
                Muzzles();
                // 호스 소리는 쥔 동안 도는 루프(UpdateSpraySound) 하나뿐이다. 발사마다 소리를 덧대면 "픽픽" 쏘는 소리가 된다.
            }

            if (_sim.GemsCollected > 0)
            {
                _xpPunch = 1f;
                if (_gemClock <= 0f)
                {
                    // 구슬 소리는 콤보가 길수록 높아진다.
                    PlayChime(1f + Mathf.Min(1f, _sim.Combo * 0.03f));
                    _gemClock = 0.04f;
                    Emit("Effects/glow", W(_sim.Player), Vector3.zero, 0f, 0.15f, 0.8f, 1.8f, new Color(0.4f, 0.8f, 1f, 0.6f), new Color(0.4f, 0.8f, 1f, 0f), 0f, true);
                }
            }

            // 연속 진압 콤보: 배율이 오르면 금색으로 알리고, 길게 잇다 끊기면 몇 연속이었는지 보여 준다.
            if (_sim.JustComboTier)
            {
                SpawnText(W(_sim.Player) + new Vector3(0f, 1.4f, 0f), "구슬 ×" + _sim.ComboMult + "!", new Color(1f, 0.85f, 0.3f), 1.8f);
                Flash(new Color(1f, 0.85f, 0.3f), 0.2f);
                _zoomKick = 0.5f;
                PlayChime(1.6f);
            }
            if (_sim.ComboEnded >= 10) SpawnText(W(_sim.Player) + new Vector3(0f, 1f, 0f), _sim.ComboEnded + "연속 진압!", new Color(0.6f, 0.9f, 1f), 1.4f);
            if (_sim.Combo > _comboPeak) _comboPeak = _sim.Combo;

            if (_sim.JustLeveled)
            {
                // 빛기둥 + 고리 + 위로 솟는 반짝이. 카드는 0.3초 뒤에 떠서 터지는 걸 먼저 보여 준다.
                Vector3 at = W(_sim.Player);
                Flash(Color.white, 0.15f);
                Pillar(at, new Color(0.8f, 0.95f, 1f));
                Shockwave(at, Color.white, 10f, 0.45f);
                Sparkle(at, 20, new Color(1f, 0.95f, 0.6f));
                SpawnText(at + new Vector3(0f, 1.4f, 0f), "LEVEL UP!", new Color(1f, 0.95f, 0.5f), 2f);
                _zoomKick = Mathf.Max(_zoomKick, 0.6f);
                _trauma = Mathf.Min(1f, _trauma + 0.25f);
                GameAudio.Play(Cue.Rescued);
                _cardsIn = 0.3f;
                _cardAge = -1f;
            }

            if (_sim.JustBigReport && _sim.BigReport != null)
            {
                // 대형 신고: 그 건물에서 붉은 고리 + 불똥, 붉은 띠 "대형 화재! 3명 갇힘", 발밑 안내 화살표 5초.
                PointAt(_sim.BigReport, 5f);
                Vector3 at = W(_sim.BigReport.Pos);
                var red = new Color(1f, 0.25f, 0.05f);
                for (int k = 0; k < 2; k++) Shockwave(at, red, 8f + (5f * k), 0.6f, k * 0.15f);
                Burst(at, 30, new Color(1f, 0.5f, 0.1f), 10f);
                _bossBandText.text = "대형 화재! " + _sim.BigReport.Name + " " + _sim.BigReport.Residents + "명 갇힘";
                _bandTint = new Color(0.6f, 0.05f, 0f);
                _bossBannerAge = 0f;
                _trauma = Mathf.Min(1f, _trauma + 0.5f);
                GameAudio.Play(Cue.Critical);
            }

            if (_sim.JustFinale)
            {
                // 대화재: 랜드마크가 확 타오르고, 붉은 고리 세 겹 + 검은 연기, 카메라가 물러난다. 발밑 안내 화살표 5초.
                PointAt(_sim.Landmark, 5f);
                Vector3 at = _sim.Landmark != null ? W(_sim.Landmark.Pos) : W(_sim.Player);
                var red = new Color(1f, 0.25f, 0.05f);
                for (int k = 0; k < 3; k++) Shockwave(at, red, 10f + (5f * k), 0.6f, k * 0.15f);
                for (int i = 0; i < 18; i++)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    Emit(Smokes[Random.Range(0, Smokes.Length)], at, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Random.Range(3f, 7f), 2f, Random.Range(0.9f, 1.4f),
                        2f, 5f, new Color(0.12f, 0.1f, 0.1f, 0.8f), new Color(0.1f, 0.08f, 0.08f, 0f), Random.Range(-90f, 90f));
                }
                Burst(at, 30, new Color(1f, 0.5f, 0.1f), 12f);
                _zoomKick = -0.8f;
                // 스테이지마다 다른 대화재: "불 전선! 내려오는 불의 띠를 끊어라".
                _bossBandText.text = _sim.Stage.FinaleName + "! " + _sim.Stage.FinaleGoal;
                _bandTint = new Color(0.6f, 0f, 0f);
                _bossBannerAge = 0f;
                _trauma = 1f;
                Flash(new Color(1f, 0.2f, 0.1f), 0.5f);
                GameAudio.Play(Cue.Critical);
                GameAudio.Play(Cue.Backfire);
            }

            if (_sim.JustBurst && _sim.Landmark != null)
            {
                // 대화재 랜드마크가 불씨를 사방으로 뿜는다: 붉은 고리 + 흔들림.
                Vector3 at = W(_sim.Landmark.Pos);
                Shockwave(at, new Color(1f, 0.35f, 0.08f, 0.9f), 8f, 0.45f);
                Burst(at, 20, new Color(1f, 0.5f, 0.1f), 9f);
                _trauma = Mathf.Min(1f, _trauma + 0.25f);
                GameAudio.Play(Cue.Backfire);
            }

            if (_sim.Chests.Count > _chestsShown)
            {
                Pickup chest = _sim.Chests[_sim.Chests.Count - 1];
                Pillar(W(chest.Pos), new Color(1f, 0.85f, 0.3f));
                ShowAlert("모두 구했다! 보물상자", new Color(1f, 0.85f, 0.3f));
                GameAudio.Play(Cue.Won);
            }
            _chestsShown = _sim.Chests.Count;
            if (_sim.JustChest)
            {
                // 상자가 열린다: 금빛 기둥·충격파·반짝이, 이어서 카드.
                Vector3 at = W(_sim.Player);
                var gold = new Color(1f, 0.85f, 0.3f);
                Pillar(at, gold);
                Shockwave(at, gold, 9f, 0.5f);
                Sparkle(at, 30, gold);
                Burst(at, 40, gold, 9f);
                Flash(gold, 0.2f);
                SpawnText(at + new Vector3(0f, 1.6f, 0f), "보물상자! 카드 " + SurvivorSim.ChestPicks + "장", gold, 1.6f);
                _slowmo = Mathf.Max(_slowmo, 0.4f);
                // 상자 카드도 레벨업 카드처럼 잠깐 뒤 튀어 오른다(안 띄우면 카드 대기로 판이 멈춘다).
                if (_sim.PendingChoices != null)
                {
                    _cardsIn = 0.5f;
                    _cardAge = -1f;
                }
                GameAudio.Play(Cue.Rescued);
            }

            ReactTown();

            if (_sim.AmbulanceAt != null)
            {
                // 구급차가 화면 밖에서 문 앞까지 달려온다. 연기 걷힘 연출은 도착 순간(DrawAmbulance)에.
                Vector3 door = W(_sim.AmbulanceAt.Door);
                _ambulanceTo = door + new Vector3(-1.6f, -1.3f, 0f);
                _ambulanceFrom = _ambulanceTo + new Vector3(-16f, -7f, 0f);
                _ambulanceRoof = W(_sim.AmbulanceAt.Pos);
                _ambulanceAge = 0f;
                _ambulanceArrived = false;
                SpecialBanner("구급차 출동!", new Color(1f, 0.3f, 0.3f));
                GameAudio.Play(Cue.Critical);
            }
            _heatTextClock -= SurvivorSim.Dt;
            if (_sim.HeatHurt > 0f && _heatTextClock <= 0f)
            {
                // 열기: 불 곁에 서 있으면 "뜨거워!"가 1.2초마다 뜬다(방화복이면 덜 붉다).
                _heatTextClock = 1.2f;
                bool suit = _sim.Build.Level(UpgradeId.Suit) > 0;
                SpawnText(W(_sim.Player) + new Vector3(0f, 1.3f, 0f), suit ? "방화복이 막는다" : "뜨거워!", suit ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.4f, 0.2f), 1f);
            }
            // 야시장: 등줄로 번짐·줄 불 꺼짐, 가판대 발사 시작, 로켓 착지.
            foreach (Lantern l in _sim.LanternCaught)
            {
                Structure to = l.Other(l.From);
                Vector3 at = W(to.Pos);
                SpawnText(at + new Vector3(0f, 2.4f, 0f), "등줄로 번졌다!", new Color(1f, 0.6f, 0.2f), 1.6f);
                Shockwave(at, new Color(1f, 0.5f, 0.1f, 0.9f), 3f, 0.3f);
                _trauma = Mathf.Min(1f, _trauma + 0.1f);
            }
            foreach (Lantern l in _sim.LanternDoused)
            {
                Vector3 at = W(_sim.LanternFire(l)) + Up(LanternHeight);
                Steam(at, 4, 0.8f);
            }
            // 맵 특색 몹: 갈매기가 지붕에 불을 떨어뜨림, 풍등이 점포에 내려앉음.
            foreach (Vec2 p in _sim.GullDrops)
            {
                Vector3 at = W(p);
                Burst(at + Up(0.5f), 12, new Color(1f, 0.55f, 0.15f), 6f);
                SpawnText(at + new Vector3(0f, 2.2f, 0f), "갈매기가 불을 떨어뜨렸다!", new Color(1f, 0.6f, 0.2f), 1.5f);
                _trauma = Mathf.Min(1f, _trauma + 0.05f);
            }
            foreach (Vec2 p in _sim.LanternLands)
            {
                Vector3 at = W(p);
                Flare(at + Up(0.6f), 4f, new Color(1f, 0.65f, 0.25f), 3);
                SpawnText(at + new Vector3(0f, 2.2f, 0f), "풍등이 내려앉았다!", new Color(1f, 0.7f, 0.3f), 1.5f);
            }
            if (_sim.JustLaunching != null)
            {
                ShowAlert("불꽃 가판대가 터진다! 하늘에서 불이 떨어진다", new Color(1f, 0.6f, 0.3f));
                Flare(W(_sim.JustLaunching.Pos) + Up(1.3f), 5f, new Color(1f, 0.8f, 0.4f), 3);
                _trauma = Mathf.Min(1f, _trauma + 0.2f);
                GameAudio.Play(Cue.SecondIgnition);
            }
            foreach (Vec2 p in _sim.RocketBursts)
            {
                Vector3 at = W(p);
                float pick = Random.value;
                Color c = pick < 0.33f ? new Color(1f, 0.45f, 0.65f) : pick < 0.66f ? new Color(1f, 0.85f, 0.35f) : new Color(0.45f, 0.9f, 0.9f);
                Flare(at, 6f, c, 4);
                Burst(at, 18, c, 7f);
                Shockwave(at, new Color(c.r, c.g, c.b, 0.9f), 4f, 0.3f);
                Flash(c, 0.08f);
                Scorch(at, 1.4f);
                _trauma = Mathf.Min(1f, _trauma + 0.12f);
            }
            // 물 불꽃놀이: 쏘아 올리는 순간 배너, 터지는 자리마다 청백 플레어·물방울 방사·젖은 자국.
            if (_sim.JustShells)
            {
                SpecialBanner("물 불꽃놀이!", new Color(0.55f, 0.9f, 1f));
                Structure stage = _sim.Landmark ?? _sim.Structures.Find(s => s.Kind == StructureKind.Depot && !s.Collapsed);
                if (stage != null) Flare(W(stage.Pos) + Up(2f), 6f, new Color(0.8f, 0.95f, 1f), 3);
                GameAudio.Play(Cue.Critical);
            }
            foreach (Vec2 p in _sim.ShellBursts)
            {
                Vector3 at = W(p);
                Flare(at + Up(1.5f), 8f, new Color(0.75f, 0.95f, 1f), 5);
                for (int i = 0; i < 24; i++)
                {
                    float a = i * Mathf.PI * 2f / 24f;
                    float sp = Random.Range(4f, 7f);
                    EmitFalling("Effects/water_drop", at + Up(1.5f), new Vector3(Mathf.Cos(a) * sp, (Mathf.Sin(a) * sp * 0.6f) + 3f, 0f), 0.7f, 0.4f, new Color(0.8f, 0.95f, 1f, 1f));
                }
                WaterBlast(at, SurvivorSim.ShellRadius, 3);
                Shockwave(at, new Color(0.8f, 0.95f, 1f, 0.9f), SurvivorSim.ShellRadius * 2.3f, 0.45f);
                Sparkle(at + Up(1.5f), 12, new Color(0.85f, 0.97f, 1f));
                AddWet(at, SurvivorSim.ShellRadius, 5f, true);
                _trauma = Mathf.Min(1f, _trauma + 0.2f);
                PlaySplash();
            }
            // 물안개: 피는 순간 흰 배너, 안개가 걷힐 때까지 DrawMist가 그린다.
            if (_sim.JustMist)
            {
                SpecialBanner("물안개 분무!", Color.white);
                Shockwave(W(_sim.MistAt.Value), new Color(1f, 1f, 1f, 0.8f), SurvivorSim.MistRadius * 2.2f, 0.5f);
                GameAudio.Play(Cue.SprayFoam);
            }
            // 큰 파도: 금색 배너 + 큰 흔들림 + 멈칫(바다가 통째로 밀려온다).
            if (_sim.JustSurge)
            {
                SpecialBanner("큰 파도!", new Color(0.5f, 0.85f, 1f));
                _trauma = Mathf.Min(1f, _trauma + 0.4f);
                HitStop(0.04f);
                GameAudio.Play(Cue.Backfire);
            }
            // 항구: 불배가 뜨면 알림, 닿으면 충격파·글자, 물 위에서 끄면 김·글자.
            if (_sim.JustBoat != null && !_sim.JustBoat.Tanker)
            {
                // 불배: 무엇을 하라는지가 띠에 바로 나온다. 발밑 화살표는 가장 가까운 부두 끝(요격 지점)을 가리킨다.
                _bossBandText.text = "불배! 부두 끝에서 쏘아 끄자";
                _bandTint = new Color(0.05f, 0.2f, 0.5f);
                _bossBannerAge = 0f;
                _pierGuideTtl = 5f;
                _trauma = Mathf.Min(1f, _trauma + 0.1f);
                GameAudio.Play(Cue.SecondIgnition);
            }
            foreach (Structure b in _sim.BoatsDocked)
            {
                Vector3 at = W(b.Pos);
                Shockwave(at, new Color(1f, 0.5f, 0.2f, 0.9f), 5f, 0.4f);
                Burst(at, 14, new Color(1f, 0.6f, 0.15f), 5f);
                SpawnText(at + new Vector3(0f, 1.6f, 0f), b.Tanker ? "좌초했다!" : "부두에 닿았다!", new Color(1f, 0.5f, 0.3f), 1.8f);
                if (!b.Tanker) ShowAlert("부두에 닿았다! 불이 옮는다", new Color(1f, 0.35f, 0.25f));
                _trauma = Mathf.Min(1f, _trauma + 0.2f);
            }
            foreach (Structure b in _sim.BoatsAway)
            {
                Vector3 at = W(b.Pos);
                Steam(at, 10, 1.2f);
                Shockwave(at, new Color(0.5f, 0.9f, 1f, 0.9f), 4f, 0.35f);
                SpawnText(at + new Vector3(0f, 1.4f, 0f), b.Tanker ? "배를 껐다!" : "요격! +" + SurvivorSim.BoatXp, new Color(0.6f, 0.95f, 1f), 1.8f);
            }
            if (_sim.JustWindShift) ShowAlert("바람이 " + WindName(_sim.Wind) + "쪽으로!", new Color(0.8f, 0.9f, 1f));
            if (_sim.JustPressureUp) ShowAlert("불길이 거세진다!", new Color(1f, 0.5f, 0.2f));
            if (_sim.JustBats)
            {
                ShowAlert("재 박쥐 떼가 날아온다!", new Color(1f, 0.55f, 0.4f));
                GameAudio.Play(Cue.SecondIgnition);
            }
            if (_sim.JustWave)
            {
                ShowAlert("불길이 사방에서 몰려온다!", new Color(1f, 0.6f, 0.3f));
                _waveAge = 0f;
                GameAudio.Play(Cue.SecondIgnition);
                _trauma = Mathf.Min(1f, _trauma + 0.3f);
            }
            // 대화재 종류별 신호: 드럼 점화(공단), 유조선 등장(항구), 등줄 폭주(야시장), 불 전선 한 줄(숲).
            if (_sim.JustChain != null)
            {
                ShowAlert("드럼 점화! " + Mathf.CeilToInt(SurvivorSim.ChainFuse) + "초 — 끄면 막는다", new Color(1f, 0.4f, 0.25f));
                PointAt(_sim.JustChain, 3f);
                Shockwave(W(_sim.JustChain.Pos), new Color(1f, 0.3f, 0.1f, 0.9f), 3f, 0.3f);
                GameAudio.Play(Cue.SecondIgnition);
            }
            if (_sim.JustTanker && _sim.TankerBoat != null)
            {
                _bossBandText.text = "유조선 좌초! 부두로 불기름이 흐른다";
                _bandTint = new Color(0.5f, 0.05f, 0f);
                _bossBannerAge = 0f;
                _trauma = Mathf.Min(1f, _trauma + 0.3f);
            }
            if (_sim.JustStorm) ShowAlert("등줄이 무대에서 번진다!", new Color(1f, 0.55f, 0.2f));
            if (_sim.JustFront && _sim.FrontY.HasValue)
            {
                _trauma = Mathf.Min(1f, _trauma + 0.05f);
                for (int k = 0; k < 6; k++)
                {
                    Vector3 at = W(new Vec2(2f + (Random.value * (SurvivorSim.ArenaSize - 4f)), _sim.FrontY.Value));
                    Emit(Smokes[Random.Range(0, Smokes.Length)], at, new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(1f, 2f), 0f), 0.6f, Random.Range(1.5f, 2.5f),
                        1.2f, 3f, new Color(0.2f, 0.17f, 0.15f, 0.6f), new Color(0.2f, 0.2f, 0.2f, 0f), Random.Range(-60f, 60f));
                }
            }

            if (_sim.JustRescued)
            {
                // 대원·드론이 구해도 그 건물 문 앞에서 연출한다.
                Vector3 at = _sim.RescuedFrom.Count > 0 ? W(_sim.RescuedFrom[0].Door) : W(_sim.Player);
                var green = new Color(0.5f, 1f, 0.5f);
                Pillar(at, green);
                Shockwave(at, green, 7f, 0.45f);
                Sparkle(at, 16, green);
                Burst(at, 14, green, 5f);
                SpawnText(at + new Vector3(0f, 0.8f, 0f), "구조! +20", new Color(0.5f, 1f, 0.5f), 1.4f);
                GameAudio.Play(Cue.PickUp);
            }

            // 곧 무너진다: 붉은 알림과 떨림. 지붕 위 초읽기는 DrawTrapped가 매 프레임 그린다.
            foreach (Structure st in _sim.CollapseWarnings)
            {
                ShowAlert(st.Name + " 곧 무너진다! " + st.Residents + "명 갇힘", new Color(1f, 0.3f, 0.25f));
                GameAudio.Play(Cue.Critical);
                _trauma = Mathf.Min(1f, _trauma + 0.25f);
            }

            // 아슬아슬 구조: 시간이 느려지고 화면이 당겨지며 금색 글자.
            foreach (Structure st in _sim.CloseCalls)
            {
                Vector3 at = W(st.Door);
                _slowmo = Mathf.Max(_slowmo, 0.7f);
                _zoomKick = 1f;
                Flash(Color.white, 0.5f);
                HitStop(0.06f);
                Pillar(at, new Color(1f, 0.85f, 0.3f));
                Sparkle(at, 30, new Color(1f, 0.9f, 0.4f));
                SpawnText(at + new Vector3(0f, 1.8f, 0f), "아슬아슬 구조! +" + SurvivorSim.CloseCallXp, new Color(1f, 0.85f, 0.3f), 2f);
                GameAudio.Play(Cue.Rescued);
            }

            if (_sim.JustLandmarkSaved && _sim.Landmark != null)
            {
                // 대화재의 절정: 랜드마크 사수. 금색 띠, 느려지는 시간, 불똥 비.
                Vector3 at = W(_sim.Landmark.Pos);
                _bossBandText.text = _sim.Landmark.Name + " 사수! 전원 구조 +" + SurvivorSim.LandmarkXp;
                _bandTint = new Color(0.6f, 0.45f, 0.05f);
                _bossBannerAge = 0f;
                _slowmo = Mathf.Max(_slowmo, 1f);
                _zoomKick = 1.2f;
                Flash(new Color(1f, 0.9f, 0.5f), 0.7f);
                for (int k = 0; k < 3; k++) Shockwave(at, new Color(1f, 0.9f, 0.5f), 8f + (5f * k), 0.8f, 0.12f * k);
                Burst(at, 120, new Color(1f, 0.8f, 0.3f), 12f);
                Pillar(at, new Color(1f, 0.9f, 0.5f));
                GameAudio.Play(Cue.Won);
            }

            if (_sim.PlayerHurt > 0f)
            {
                _hurt = Mathf.Min(1f, _hurt + (_sim.PlayerHurt * 0.1f));
                _trauma = Mathf.Min(1f, _trauma + (_sim.PlayerHurt * 0.01f));
                // 닿아 있는 동안 계속 깎이므로 0.4초마다 한 번만 "아야"를 보여 준다.
                if (_hurtClock <= 0f)
                {
                    _hurtClock = 0.4f;
                    Burst(W(_sim.Player), 8, new Color(1f, 0.3f, 0.2f), 6f);
                    _trauma = Mathf.Min(1f, _trauma + 0.15f);
                    HitStop(0.04f);
                }
            }

            if (_sim.Outcome != _lastOutcome)
            {
                _lastOutcome = _sim.Outcome;
                _overAge = 0f;
                if (_sim.Outcome == SOutcome.Won)
                {
                    // 별은 소방서 통장에 남는다.
                    _earned = _station.RecordResult(_stage, _sim.Stars);
                    SaveStation();
                    // 4:00까지 지켜 냈다: 번쩍 → (잠깐 뒤) 고리 세 겹 + 거대한 김 + 불똥 비.
                    Vector3 at = W(_sim.Player);
                    // 승리는 하얗게 덮어도 된다(한 판에 한 번). Flash의 0.5 상한을 넘기려고 직접 넣는다.
                    _flashColor = Color.white;
                    _flash = 0.9f;
                    for (int k = 0; k < 3; k++) Shockwave(at, new Color(0.6f, 0.9f, 1f), 12f + (6f * k), 0.9f, 0.15f + (k * 0.15f));
                    Steam(at, 20, 2.2f);
                    Burst(at, 120, new Color(1f, 0.7f, 0.3f), 14f);
                    Pillar(at, new Color(0.7f, 0.9f, 1f));
                    _zoomKick = 1.2f;
                    _slowmo = 1.2f;
                    _trauma = 1f;
                    GameAudio.Play(Cue.Won);
                }
                else
                {
                    _trauma = 0.8f;
                    GameAudio.Play(Cue.Failed);
                }
            }
        }

        /// <summary>동네 신호: 불남·꺼짐·무너짐·폭발·사람 잃음.</summary>
        private void ReactTown()
        {
            if (_sim.Toolboxes.Count > _toolboxesShown)
            {
                ShowAlert("공구상자가 떨어졌다! 주우면 건물 수리", new Color(1f, 0.75f, 0.25f));
                GameAudio.Play(Cue.PickUp);
            }
            _toolboxesShown = _sim.Toolboxes.Count;
            // 구급상자: 떨어지면 알림, 주우면 초록 "+35".
            if (_sim.Kits.Count > _kitsShown)
            {
                ShowAlert("구급상자가 떨어졌다! 체력 +" + (int)SurvivorSim.KitHeal, new Color(1f, 0.6f, 0.6f));
                GameAudio.Play(Cue.PickUp);
            }
            _kitsShown = _sim.Kits.Count;
            if (_sim.JustPickedKit)
            {
                Sparkle(W(_sim.Player), 12, new Color(0.5f, 1f, 0.5f));
                SpawnText(W(_sim.Player) + new Vector3(0f, 1.3f, 0f), "+" + (int)SurvivorSim.KitHeal, new Color(0.5f, 1f, 0.5f), 1.4f);
                GameAudio.Play(Cue.PickUp);
            }
            foreach (Structure st in _sim.Repaired)
            {
                // 수리: 소방관에서 건물로 초록 빛이 날아가고, 건물에서 반짝이·기둥이 솟는다.
                Vector3 from = W(_sim.Player);
                Vector3 to = W(st.Pos);
                var green = new Color(0.45f, 1f, 0.5f);
                for (int i = 0; i < 14; i++)
                {
                    float f = i / 13f;
                    EmitSprite(Art.Get("Effects/glow"), Vector3.Lerp(from, to, f), Vector3.zero, 0f, 0.25f + (0.35f * f), 0.9f, 0.3f, new Color(0.5f, 1f, 0.55f, 0.9f), new Color(0.5f, 1f, 0.55f, 0f),
                        0f, true, 0f, 1f, float.NaN, f * 0.25f);
                }
                Pillar(to, green);
                Sparkle(to, 16, green);
                Shockwave(to, green, 7f, 0.5f, 0.25f);
                SpawnText(to + new Vector3(0f, 1.8f, 0f), "수리!", green, 1.6f);
                ShowAlert(st.Name + " 수리!", green);
                GameAudio.Play(Cue.Rescued);
            }
            if (_sim.JustPickedToolbox && _sim.Repaired.Count == 0)
            {
                Sparkle(W(_sim.Player), 10, new Color(0.45f, 1f, 0.5f));
                ShowAlert("고칠 건물이 없어 체력 +" + (int)SurvivorSim.ToolboxHeal, new Color(0.45f, 1f, 0.5f));
                GameAudio.Play(Cue.PickUp);
            }

            ShowFireSignals();

            foreach (Structure st in _sim.Ignited)
            {
                Vector3 at = W(st.Pos);
                // 새로 불난 건물 쪽으로 발밑 안내 화살표 4초.
                if (st.IsBuilding) PointAt(st, 4f);
                if (st.IsBuilding && !_sim.Spread.Contains(st))
                {
                    ShowAlert(st.Name + "에 불!" + (st.Residents > 0 ? "  " + st.Residents + "명 갇힘" : ""), new Color(1f, 0.6f, 0.25f));
                    Shockwave(at, new Color(1f, 0.45f, 0.1f, 0.9f), 6f, 0.5f);
                    Burst(at, 18, new Color(1f, 0.55f, 0.15f), 6f);
                }
                else if (st.Kind == StructureKind.Gas)
                {
                    ShowAlert("가스통에 불! 곧 터진다", new Color(1f, 0.35f, 0.25f));
                    GameAudio.Play(Cue.Critical);
                }
                if (_igniteClock <= 0f)
                {
                    GameAudio.Play(Cue.SecondIgnition);
                    _igniteClock = 0.25f;
                }
            }

            foreach (Structure st in _sim.Doused)
            {
                Vector3 at = W(st.Pos);
                Steam(at, st.IsBuilding ? 10 : 4, st.IsBuilding ? 2f : 1.2f);
                if (st.IsBuilding)
                {
                    Shockwave(at, new Color(0.6f, 0.9f, 1f, 0.9f), 7f, 0.5f);
                    SpawnText(at + new Vector3(0f, 1.6f, 0f), "진화!", new Color(0.6f, 0.9f, 1f), 1.6f);
                }
                GameAudio.Play(Cue.PutOut);
            }

            foreach (Structure st in _sim.Fell)
            {
                Vector3 at = W(st.Pos);
                if (st.Kind == StructureKind.Gas) continue;
                bool big = st.IsBuilding;
                Scorch(at, big ? Mathf.Max(st.Half.X, st.Half.Y) * 3f : 2.5f);
                Burst(at, big ? 50 : 15, new Color(1f, 0.5f, 0.15f), big ? 9f : 5f);
                for (int k = 0; k < (big ? 10 : 3); k++)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    Emit(Smokes[Random.Range(0, Smokes.Length)], at, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Random.Range(1.5f, 4f), 1.5f, Random.Range(1.2f, 2f),
                        1.5f, 4f, new Color(0.15f, 0.13f, 0.13f, 0.75f), new Color(0.2f, 0.2f, 0.2f, 0f), Random.Range(-90f, 90f));
                }
                if (big)
                {
                    ShowAlert(st.Name + Josa(st.Name, "이", "가") + " 무너졌다", new Color(1f, 0.35f, 0.3f));
                    _trauma = Mathf.Min(1f, _trauma + 0.6f);
                    GameAudio.Play(Cue.Collapse);
                }
            }

            foreach (Vec2 p in _sim.GasBlasts)
            {
                Vector3 at = W(p);
                var orange = new Color(1f, 0.5f, 0.1f);
                Flash(orange, 0.5f);
                Shockwave(at, orange, SurvivorSim.GasRadius * 2.4f, 0.5f);
                Shockwave(at, new Color(1f, 0.85f, 0.4f), SurvivorSim.GasRadius * 1.6f, 0.35f, 0.08f);
                Burst(at, 70, orange, 13f);
                Scorch(at, SurvivorSim.GasRadius * 2f);
                for (int k = 0; k < 12; k++)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    Emit(Smokes[Random.Range(0, Smokes.Length)], at, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Random.Range(3f, 7f), 2f, Random.Range(1f, 1.6f),
                        1.5f, 4.5f, new Color(0.12f, 0.1f, 0.1f, 0.8f), new Color(0.1f, 0.1f, 0.1f, 0f), Random.Range(-90f, 90f));
                }
                _trauma = 1f;
                HitStop(0.06f);
                GameAudio.Play(Cue.Backfire);
            }

            foreach (Structure st in _sim.PeopleLost)
            {
                ShowAlert(st.Name + "에서 사람을 잃었다", new Color(0.85f, 0.75f, 0.75f));
                GameAudio.Play(Cue.CivilianLost);
            }
        }

        /// <summary>받침이 있으면 a(이/을), 없으면 b(가/를).</summary>
        private static string Josa(string word, string a, string b)
        {
            if (string.IsNullOrEmpty(word)) return b;
            char c = word[word.Length - 1];
            if (c < 0xAC00 || c > 0xD7A3) return b;
            return (c - 0xAC00) % 28 != 0 ? a : b;
        }

        // ------------------------------------------------------------------
        // 매 프레임
        // ------------------------------------------------------------------

        public void Refresh(float dt)
        {
            _time += dt;
            _frameDt = dt;
            if (_goalAlertAt >= 0f && _time >= _goalAlertAt && !_stationOpen)
            {
                _goalAlertAt = -1f;
                if (!string.IsNullOrEmpty(_sim.Stage.Goal)) ShowAlert("할 일: " + _sim.Stage.Goal, GoalGreen);
            }
            _trauma = Mathf.Max(0f, _trauma - (dt * 1.8f));
            _flash = Mathf.Max(0f, _flash - (dt * 2.2f));
            _hurt = Mathf.Max(0f, _hurt - (dt * 1.5f));
            _slowmo = Mathf.Max(0f, _slowmo - dt);
            _xpPunch = Mathf.Max(0f, _xpPunch - (dt * 5f));
            _zoomKick *= Mathf.Exp(-6f * dt);
            // 쥔 동안은 물살을 버티느라 살짝 뒤로 기댄 자세를 유지한다(쏠 때마다 튕기지 않는다).
            _recoil = Mathf.MoveTowards(_recoil, _sim.Spraying ? 0.35f : 0f, dt * 4f);
            UpdateSprayFeel(dt);
            _stance = Mathf.MoveTowards(_stance, _sim.Spraying ? 1f : 0f, dt * 6f);
            _hurtClock -= dt;
            _waveAge += dt;
            if (_cardsIn > 0f)
            {
                _cardsIn -= dt;
                if (_cardsIn <= 0f && _sim.PendingChoices != null) ShowCards();
            }
            _putOutClock -= dt;
            _weaponSoundClock -= dt;
            AdvanceWeaponFx(dt);
            _igniteClock -= dt;
            _sizzleClock -= dt;
            _gemClock -= dt;
            _cardAge += dt;
            _alertAge += dt;
            _bossBannerAge += dt;
            if (_sim.Outcome != SOutcome.Playing) _overAge += dt;

            foreach (Pool p in _pools) p.Begin();
            _people.Begin();
            foreach (ModelPool m in _modelPools) m.Begin();
            foreach (RibbonPool r in _ribbons) r.Begin(_time);
            DrawTown();
            DrawPuddles();
            DrawWetMarks();
            DrawGems();
            DrawToolboxes();
            DrawKits();
            DrawChests();
            DrawCivilians();
            DrawEnemies();
            DrawShots();
            DrawPlayer();
            DrawGear(dt);
            DrawSpecials(dt);
            DrawEdgeArrows();
            DrawGuides(dt);
            DrawWeather();
            foreach (Pool p in _pools) p.End();
            EndTags();
            _people.End();
            foreach (ModelPool m in _modelPools) m.End();
            foreach (RibbonPool r in _ribbons) r.End();

            UpdateSpraySound(dt);
            AdvanceParticles(dt);
            AdvanceNumbers(dt);
            RefreshHud(dt);
            FollowCamera(dt);

            float loud = _sim.Outcome == SOutcome.Playing ? 1f - Mathf.Exp(-_sim.Enemies.Count / 60f) : 0f;
            GameAudio.SetFireLevel(loud, Mathf.Max(dt, 0.001f));
        }

        private const int AshCount = 50;
        private const int EmberMax = 20;

        /// <summary>공기: 화면 안을 천천히 떨어지는 재 + 타는 구조물에서 떠오르는 불티. 해시 자리라 캡처가 결정적이다.</summary>
        private void DrawWeather()
        {
            Vector3 centre = GroundAt(new Vector3(0.5f, 0.5f, 0f));
            float spanX = (_viewHalfW + 2f) * 2f;
            float spanY = (_viewHalfH + 3f) * 2f;
            for (int i = 0; i < AshCount; i++)
            {
                float fall = 0.35f + (0.3f * Hash01((i * 7) + 11));
                float x = (Hash01((i * 7) + 12) * spanX) + (Mathf.Sin((_time * 0.7f) + i) * 0.6f) + (_time * 0.25f);
                float y = (Hash01((i * 7) + 13) * spanY) - (_time * fall);
                // 화면 영역 안에서 돌아 나온다.
                x = centre.x - (spanX / 2f) + Mathf.Repeat(x - centre.x, spanX);
                y = centre.y - (spanY / 2f) + Mathf.Repeat(y - centre.y, spanY);
                float lift = 0.3f + (2.7f * Hash01((i * 7) + 14));
                float size = Hash01((i * 7) + 15) < 0.3f ? 0.13f : 0.08f;
                _motes.Put(new Vector3(x, y, 0f) + Up(lift), size, 0f, new Color(0.85f, 0.82f, 0.78f, 0.55f));
            }

            int embers = 0;
            for (int s = 0; s < _sim.Structures.Count && embers < EmberMax; s++)
            {
                Structure st = _sim.Structures[s];
                if (!st.Burning) continue;
                float roof = st.IsBuilding ? RoofHeight(s) : 0.5f;
                int n = 2 + Mathf.RoundToInt(st.Fire * 4f);
                for (int k = 0; k < n && embers < EmberMax; k++, embers++)
                {
                    int seed = (s * 31) + (k * 7);
                    float speed = 0.8f + (0.6f * Hash01(seed + 1));
                    float rise = Mathf.Repeat((_time * speed) + (Hash01(seed + 2) * 4f), 4f);
                    float ox = ((Hash01(seed + 3) - 0.5f) * st.Half.X * 1.6f) + (Mathf.Sin((_time * 2f) + seed) * 0.3f * rise);
                    float oy = (Hash01(seed + 4) - 0.5f) * st.Half.Y * 1.2f;
                    float fade = 1f - (rise / 4f);
                    var at = new Vector3(st.Pos.X + ox, st.Pos.Y + oy, 0f) + Up(roof + 0.3f + rise);
                    _motes.Put(at, 0.1f, 0f, new Color(1f, 0.55f + (0.3f * Hash01(seed + 5)), 0.15f, fade));
                }
            }
        }

        private static Vector3 W(Vec2 p)
        {
            for (int i = 0; i < RoofRects.Count; i++)
            {
                if (RoofRects[i].Contains(new Vector2(p.X, p.Y))) return new Vector3(p.X, p.Y, -RoofHeights[i] - 0.05f);
            }
            return new Vector3(p.X, p.Y, 0f);
        }

        /// <summary>땅에서 z칸 위(월드 위쪽은 −Z).</summary>
        private static Vector3 Up(float z)
        {
            return new Vector3(0f, 0f, -z);
        }

        /// <summary>깊이를 남기는 컷아웃 머티리얼(입체 면·나무·차·사람).</summary>
        private static Material Cutout()
        {
            if (_cutout != null) return _cutout;
            Shader shader = Resources.Load<Shader>("Shaders/PixelCutout");
            if (shader == null)
            {
                Debug.LogWarning("[SurvivorView] PixelCutout 셰이더가 없어 Unlit/Transparent Cutout으로 대신한다(틴트 빠짐).");
                shader = Shader.Find("Unlit/Transparent Cutout");
            }
            _cutout = new Material(shader) { name = "PixelCutout" };
            return _cutout;
        }

        private void DrawEnemies()
        {
            for (int i = 0; i < _sim.Enemies.Count; i++)
            {
                Enemy e = _sim.Enemies[i];
                Vector3 at = W(e.Pos);
                float flicker = 1f + (0.09f * Mathf.Sin((_time * 14f) + (i * 1.7f)));
                bool hit = e.HitFlash > 0f;
                float foot = e.Kind == EnemyKind.Blaze ? 1.8f : e.Kind == EnemyKind.Oil ? 1.7f : 1f;
                _shadows.Put(at + new Vector3(0f, -0.1f, 0f), foot, 0f, new Color(0f, 0f, 0f, 0.35f), null, 0.5f);
                // 물을 먹을수록 불이 쪼그라든다(체력 비례).
                float life = 0.55f + (0.45f * Mathf.Clamp01(e.Hp / Mathf.Max(0.01f, e.MaxHp)));
                float punch = (hit ? 1.35f : 1f) * life;
                if (hit && Random.value < 0.12f) Steam(at, 1, 0.45f * life);
                var water = new Color(0.7f, 0.95f, 1f);

                switch (e.Kind)
                {
                    case EnemyKind.Ember:
                        _enemyGlow.Put(at, 1.4f * flicker * life, 0f, new Color(1f, 0.4f, 0.08f, 0.25f));
                        // 흔들리는 불꽃 한 장(심은 그림에 들어 있다). 물을 맞으면 파랗게.
                        _embers.Put(at + new Vector3(0f, 0.05f, 0f), 1.05f * flicker * punch, 0f, hit ? water : Color.white, FlameArt.Frame(_emberSheet, _time, i));
                        break;
                    case EnemyKind.Blaze:
                        _enemyGlow.Put(at, 2.4f * flicker * life, 0f, new Color(1f, 0.3f, 0.05f, 0.35f));
                        if (Random.value < 0.03f)
                        {
                            Emit(Smokes[Random.Range(0, Smokes.Length)], at + new Vector3(0f, 0.8f * life, 0f), new Vector3(Random.Range(-0.3f, 0.3f), 1.2f, 0f), 0.5f, 1.2f,
                                0.6f, 1.8f, new Color(0.25f, 0.22f, 0.22f, 0.45f), new Color(0.2f, 0.2f, 0.2f, 0f), Random.Range(-60f, 60f));
                        }
                        if (Random.value < 0.04f)
                        {
                            Emit(Sparks[Random.Range(0, Sparks.Length)], at, new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(1.5f, 3f), 0f), 1f, 0.6f,
                                0.35f, 0.05f, new Color(1f, 0.8f, 0.3f), new Color(1f, 0.3f, 0.05f, 0f), 0f, true);
                        }
                        // 큰 불: 어두운 밑동 위에 큰 불꽃 하나와 양옆 작은 불꽃 둘이 서로 다른 프레임으로 흔들린다.
                        // 대화재 고리의 질긴 큰 불(Heavy)은 1.25배 크고 발밑에 검붉은 고리가 돈다: 보통 큰 불과 구별된다.
                        float heavy = e.Heavy ? 1.25f : 1f;
                        if (e.Heavy) _auras.Put(at + new Vector3(0f, -0.05f, 0f), 2.4f * life, _time * 40f, new Color(0.7f, 0.05f, 0.05f, 0.7f));
                        _shadows.Put(at + new Vector3(0f, 0.05f, 0f), 1.9f * life * heavy, 0f, new Color(0.18f, 0.08f, 0.04f, 0.8f), null, 0.45f);
                        _blazes.Put(at + new Vector3(-0.55f * life, 0.05f, 0f), 1.5f * flicker * punch * heavy, 0f, hit ? water : Color.white, FlameArt.Frame(_blazeSheet, _time, i + 3));
                        _blazes.Put(at + new Vector3(0.55f * life, 0.05f, 0f), 1.4f * flicker * punch * heavy, 0f, hit ? water : Color.white, FlameArt.Frame(_blazeSheet, _time, i + 5));
                        _blazes.Put(at + new Vector3(0f, 0.1f, 0f), 2f * flicker * punch * heavy, 0f, hit ? water : Color.white, FlameArt.Frame(_blazeSheet, _time, i));
                        break;
                    case EnemyKind.Squirrel:
                    {
                        // 불다람쥐: 몸+말린 꼬리 실루엣(주황)이 달리고, 꼬리 끝에 불이 붙어 흔들린다.
                        Vec2 goal = e.Goal != null ? e.Goal.Pos : _sim.Player;
                        float head = Mathf.Atan2(goal.Y - e.Pos.Y, goal.X - e.Pos.X);
                        var back = new Vector3(-Mathf.Cos(head), -Mathf.Sin(head), 0f);
                        var side = new Vector3(-back.y, back.x, 0f);
                        float wag = Mathf.Sin((_time * 14f) + i) * 0.12f;
                        float bob = 1f + (0.08f * Mathf.Sin((_time * 18f) + i));
                        _enemyGlow.Put(at, 1.5f * life, 0f, new Color(1f, 0.45f, 0.1f, 0.35f));
                        _bats.Put(at + new Vector3(0f, 0.08f, 0f) + (side * wag), 1.45f * punch * bob, head * Mathf.Rad2Deg, hit ? water : new Color(0.95f, 0.5f, 0.15f), SquirrelSprite());
                        _darts.Put(at + (back * 0.5f) + new Vector3(0f, 0.35f, 0f) + (side * wag), 0.7f * flicker * punch, 0f, hit ? water : Color.white, FlameArt.Frame(_dartSheet, _time, i, 18f));
                        if (!hit) _enemyCore.Put(at + (new Vector3(Mathf.Cos(head), Mathf.Sin(head), 0f) * 0.25f), 0.4f * life, 0f, new Color(1f, 0.85f, 0.4f, 0.9f));
                        break;
                    }
                    case EnemyKind.Bat:
                    {
                        // 재 박쥐: 검붉은 날개가 퍼덕이고 재 가루가 떨어진다.
                        float head = Mathf.Atan2(_sim.Player.Y - e.Pos.Y, _sim.Player.X - e.Pos.X) * Mathf.Rad2Deg;
                        float flap = 0.35f + (0.65f * Mathf.Abs(Mathf.Sin((_time * 16f) + e.Phase)));
                        _enemyGlow.Put(at, 1.3f, 0f, new Color(1f, 0.3f, 0.05f, 0.3f));
                        _bats.Put(at, 1.3f * punch, head, hit ? water : new Color(0.55f, 0.2f, 0.15f), null, flap);
                        if (!hit) _enemyCore.Put(at, 0.45f, 0f, new Color(1f, 0.6f, 0.2f, 0.9f));
                        if (Random.value < 0.04f)
                        {
                            EmitFalling("Effects/smoke_01", at, new Vector3(Random.Range(-0.5f, 0.5f), 0.5f, 0f), 0.6f, 0.25f, new Color(0.25f, 0.22f, 0.22f, 0.7f));
                        }
                        break;
                    }
                    case EnemyKind.Oil:
                    {
                        // 기름 방울: 출렁이는 검보라 기름 덩어리 위로 보라·주황 불이 인다.
                        float wob = 1f + (0.08f * Mathf.Sin((_time * 5f) + i));
                        _foam.Put(at + new Vector3(0f, 0.05f, 0f), 2.2f * life * wob, 0f, hit ? water : new Color(0.08f, 0.05f, 0.1f, 1f), Art.Get("Effects/glow"), 0.75f);
                        _enemyGlow.Put(at, 2.2f * flicker * life, 0f, new Color(0.85f, 0.25f, 0.75f, 0.4f));
                        _blazes.Put(at + new Vector3(0f, 0.15f, 0f), 1.9f * flicker * punch, 0f, hit ? water : Color.white, FlameArt.Frame(_oilSheet, _time, i));
                        if (Random.value < 0.05f)
                        {
                            Emit(Smokes[Random.Range(0, Smokes.Length)], at + new Vector3(0f, 0.7f, 0f), new Vector3(Random.Range(-0.3f, 0.3f), 1f, 0f), 0.4f, 1.4f,
                                0.5f, 1.6f, new Color(0.12f, 0.1f, 0.12f, 0.55f), new Color(0.1f, 0.1f, 0.1f, 0f), Random.Range(-60f, 60f));
                        }
                        break;
                    }
                    case EnemyKind.Dart:
                        float toward = Mathf.Atan2(_sim.Player.Y - e.Pos.Y, _sim.Player.X - e.Pos.X) * Mathf.Rad2Deg;
                        _enemyGlow.Put(at, 1.5f, 0f, new Color(1f, 0.8f, 0.2f, 0.45f));
                        _darts.Put(at, 0.8f * flicker * punch, toward + 90f, hit ? water : Color.white, FlameArt.Frame(_dartSheet, _time, i, 18f));
                        break;
                    case EnemyKind.Popper:
                    {
                        // 폭죽: 빨간 원통 몸통이 통통 뛰고(한 주기 1초 중 앞 0.35초) 심지에서 불꽃이 탄다.
                        float ph = e.Phase - Mathf.Floor(e.Phase);
                        float hop = ph < SurvivorSim.PopperHop ? Mathf.Sin(ph / SurvivorSim.PopperHop * Mathf.PI) * 0.8f : 0f;
                        Vector3 body = at + Up(0.35f + hop);
                        _enemyGlow.Put(at, 1.4f * flicker, 0f, new Color(1f, 0.4f, 0.15f, 0.3f));
                        _enemyCore.Put(body, 0.5f * punch, 0f, hit ? water : new Color(0.9f, 0.15f, 0.1f, 1f), null, 1.9f);
                        _enemyCore.Put(body + Up(0.5f), 0.52f * punch, 0f, hit ? water : new Color(1f, 0.85f, 0.3f, 1f), null, 0.3f);
                        _darts.Put(body + Up(0.75f), 0.55f * flicker * punch, 0f, hit ? water : Color.white, FlameArt.Frame(_dartSheet, _time, i, 18f));
                        if (Random.value < 0.15f)
                        {
                            Emit(Sparks[Random.Range(0, Sparks.Length)], body + Up(0.8f), new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(1f, 3f), 0f), 1f, 0.4f, 0.3f, 0.05f, new Color(1f, 0.9f, 0.4f), new Color(1f, 0.3f, 0.05f, 0f), 0f, true);
                        }
                        break;
                    }
                    case EnemyKind.Gull:
                    {
                        // 불 갈매기(폭격기): 흰 갈매기가 불을 물고 지붕을 향해 내려오다(목표 6칸 안에서 급강하) 떨어뜨리고, 빈 몸으로 높이 바다로 돌아간다.
                        // 노리는 지붕에는 주황 고리가 돈다: 어느 집을 지켜야 하는지 보인다.
                        Vec2 aim = e.Dropped ? new Vec2(e.Pos.X, SurvivorSim.ArenaSize + 4f) : e.Goal != null ? e.Goal.Pos : _sim.Player;
                        float head = Mathf.Atan2(aim.Y - e.Pos.Y, aim.X - e.Pos.X) * Mathf.Rad2Deg;
                        float flap = 0.55f + (0.45f * Mathf.Abs(Mathf.Sin((_time * 7f) + e.Phase)));
                        float dive = e.Dropped || e.Goal == null ? 1f : Mathf.Clamp01(e.Goal.DistanceTo(e.Pos) / 6f);
                        Vector3 high = at + Up(e.Dropped ? 2.1f : Mathf.Lerp(0.6f, 1.6f, dive));
                        _shadows.Put(at + new Vector3(0f, -0.1f, 0f), 1.2f * (0.6f + (0.4f * dive)), 0f, new Color(0f, 0f, 0f, 0.2f), null, 0.5f);
                        _bats.Put(high, 2.0f * punch, head, hit ? water : Color.white, GullSprite(), flap);
                        if (!e.Dropped)
                        {
                            // 물고 있는 불: 몸 밑에서 흔들리고 연기를 끈다.
                            _enemyGlow.Put(high, 1.6f * flicker, 0f, new Color(1f, 0.5f, 0.1f, 0.3f));
                            _embers.Put(high + Up(-0.35f), 0.75f * flicker * punch, 0f, hit ? water : Color.white, FlameArt.Frame(_emberSheet, _time, i));
                            if (e.Goal != null && dive < 1f)
                            {
                                Vector3 roof = W(e.Goal.Pos) + Up(0.05f);
                                _reticle.Put(roof, Mathf.Max(e.Goal.Half.X, e.Goal.Half.Y) * 2.2f * (1f + (0.08f * Mathf.Sin(_time * 8f))), _time * 90f, new Color(1f, 0.55f, 0.15f, 0.35f + (0.3f * (1f - dive))));
                            }
                            if (Random.value < 0.08f)
                            {
                                Emit("Effects/smoke_01", high, new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.2f, 0.8f), 0f), 0.6f, 0.7f, 0.3f, 0.8f, new Color(0.3f, 0.25f, 0.25f, 0.6f), new Color(0.3f, 0.3f, 0.3f, 0f), 0f);
                            }
                        }
                        break;
                    }
                    case EnemyKind.Crab:
                    {
                        // 불 게: 넓적한 빨간 게가 옆걸음으로 꿈틀거리며 온다. 등딱지 금에서 불이 새고, 바다 위에선 물을 튀긴다.
                        Vec2 aim = e.Dropped ? _sim.Player : e.Goal != null ? e.Goal.Pos : _sim.Player;
                        float head = Mathf.Atan2(aim.Y - e.Pos.Y, aim.X - e.Pos.X) * Mathf.Rad2Deg;
                        float wiggle = 1f + (0.08f * Mathf.Sin(_time * 12f + i));
                        _shadows.Put(at + new Vector3(0f, -0.1f, 0f), 1.5f, 0f, new Color(0f, 0f, 0f, 0.35f), null, 0.5f);
                        _enemyGlow.Put(at, 1.8f * flicker * life, 0f, new Color(1f, 0.3f, 0.1f, 0.3f));
                        _bats.Put(at + Up(0.12f), 1.7f * punch, head, hit ? water : Color.white, CrabSprite(), wiggle);
                        if (!hit)
                        {
                            _enemyCore.Put(at + Up(0.3f) + new Vector3(0.15f, 0.1f, 0f), 0.45f * flicker * life, 0f, new Color(1f, 0.6f, 0.15f, 0.95f));
                            _enemyCore.Put(at + Up(0.3f) + new Vector3(-0.2f, -0.05f, 0f), 0.4f * flicker * life, 0f, new Color(1f, 0.6f, 0.15f, 0.95f));
                        }
                        if (_sim.Stage.Sea && e.Pos.Y > SurvivorHarbor.SeaFrom && Random.value < 0.1f) Splash(at, 1, 0.4f);
                        break;
                    }
                    case EnemyKind.SkyLantern:
                    {
                        // 풍등: 높이 떠서 천천히 출렁이는 따뜻한 종이등. 안에 불이 일렁이고 바닥에 둥근 빛이 따라다닌다(밤 골목에서 환하다).
                        float bobL = Mathf.Sin((_time * 1.6f) + i) * 0.2f;
                        Vector3 high = at + Up(3f + bobL);
                        _shadows.Put(at + new Vector3(0f, -0.1f, 0f), 0.7f, 0f, new Color(0f, 0f, 0f, 0.15f), null, 0.5f);
                        _groundGlow.Put(at, 4.0f * flicker, 0f, new Color(1f, 0.6f, 0.25f, 0.4f));
                        // 종이등 몸(불투명한 따뜻한 원) + 둘레 빛무리 + 안의 불꽃.
                        _foam.Put(high, 1.3f * punch, 0f, hit ? water : new Color(1f, 0.6f, 0.28f, 1f), Art.Get("Effects/glow"), 1.3f);
                        _siren.Put(high, 2.2f * punch * flicker, 0f, hit ? water : new Color(1f, 0.7f, 0.35f, 0.8f));
                        _embers.Put(high + Up(-0.1f), 0.7f * flicker * punch, 0f, hit ? water : Color.white, FlameArt.Frame(_emberSheet, _time, i));
                        if (e.Goal != null && e.Goal.DistanceTo(e.Pos) < 10f)
                        {
                            Vector3 roof = W(e.Goal.Pos) + Up(0.05f);
                            _reticle.Put(roof, Mathf.Max(e.Goal.Half.X, e.Goal.Half.Y) * 2.2f, -_time * 60f, new Color(1f, 0.65f, 0.3f, 0.3f));
                        }
                        if (Random.value < 0.06f)
                        {
                            EmitFalling("Effects/spark_01", high, new Vector3(Random.Range(-0.4f, 0.4f), -0.3f, 0f), 0.8f, 0.2f, new Color(1f, 0.7f, 0.3f, 0.8f));
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>바람 방향 이름(여덟 방향, 화면 위가 북).</summary>
        private static string WindName(Vec2 wind)
        {
            string[] names = { "동", "북동", "북", "북서", "서", "남서", "남", "남동" };
            int k = Mathf.RoundToInt(Mathf.Atan2(wind.Y, wind.X) / (Mathf.PI / 4f));
            return names[((k % 8) + 8) % 8];
        }

        /// <summary>특수 장비 모습: 달리는 소방차, 먹구름과 빗줄기, 붉은 방염제 띠와 지나가는 비행기.</summary>
        /// <summary>구급차: 1.2초 달려와 문 앞에 서서 경광등을 돌리고(도착 순간 연기가 걷힌다), 2초 뒤 떠난다.</summary>
        private void DrawAmbulance(float dt)
        {
            if (_ambulanceAge >= 4.4f) return;
            _ambulanceAge += dt;
            Vector3 dir = (_ambulanceTo - _ambulanceFrom).normalized;
            Vector3 at;
            if (_ambulanceAge < 1.2f)
            {
                float f = 1f - Mathf.Pow(1f - Mathf.Clamp01(_ambulanceAge / 1.2f), 2f);
                at = Vector3.Lerp(_ambulanceFrom, _ambulanceTo, f);
            }
            else if (_ambulanceAge < 3.2f) at = _ambulanceTo;
            else
            {
                float f = Mathf.Clamp01((_ambulanceAge - 3.2f) / 1.2f);
                at = _ambulanceTo + (dir * (f * f * 22f));
            }
            if (!_ambulanceArrived && _ambulanceAge >= 1.2f)
            {
                // 도착: 흰 김이 지붕에서 걷혀 올라가고 "구급차! 연기 걷힘".
                _ambulanceArrived = true;
                // 도착: 큰 흰 김이 지붕에서 걷혀 올라가고, 녹색 십자 플레어, 충격파, "연기 걷힘!".
                SteamPillar(_ambulanceRoof, 2f);
                Steam(_ambulanceRoof, 16, 1.5f);
                Flare(_ambulanceRoof, 6f, new Color(0.4f, 1f, 0.5f), 3, 0.3f);
                Shockwave(_ambulanceTo, new Color(1f, 1f, 1f, 0.9f), 9f, 0.5f);
                SpawnText(_ambulanceTo + new Vector3(0f, 2f, 0f), "연기 걷힘!", new Color(0.6f, 1f, 0.7f), 2.2f);
                _trauma = Mathf.Min(1f, _trauma + 0.2f);
                GameAudio.Play(Cue.PickUp);
            }
            _shadows.Put(at + new Vector3(0f, -0.3f, 0f), 2.6f, 0f, new Color(0f, 0f, 0f, 0.4f), null, 0.5f);
            GameObject model = _ambulanceModels.Get();
            if (model != null) ItemModels.Place(model, at, 0f, dir, 1f);
            // 사이렌 둘이 번갈아 번쩍이고 그 빛이 바닥을 빨강·파랑으로 물들인다.
            bool flip = Mathf.Repeat(_time * 6f, 1f) < 0.5f;
            Color sirenA = flip ? new Color(0.2f, 0.4f, 1f, 0.95f) : new Color(1f, 0.15f, 0.1f, 0.95f);
            Color sirenB = flip ? new Color(1f, 0.15f, 0.1f, 0.9f) : new Color(0.2f, 0.4f, 1f, 0.9f);
            _siren.Put(at + new Vector3(-0.35f, 0.95f, 0f), 2.4f, 0f, sirenA);
            _siren.Put(at + new Vector3(0.35f, 0.95f, 0f), 2f, 0f, sirenB);
            _groundGlow.Put(at, 7f, 0f, new Color(sirenA.r, sirenA.g, sirenA.b, 0.3f));
            if (_ambulanceAge < 1.2f || _ambulanceAge > 3.2f)
            {
                // 달릴 때 뒤로 먼지와 타이어 자국.
                Emit(Smokes[Random.Range(0, Smokes.Length)], at - (dir * 1.1f), -dir * 1.5f, 1.5f, 0.4f, 0.3f, 0.9f, new Color(0.8f, 0.78f, 0.7f, 0.4f), new Color(0.8f, 0.78f, 0.7f, 0f), 0f);
                Emit(Smokes[Random.Range(0, Smokes.Length)], at - (dir * 0.8f) + new Vector3(0f, -0.4f, 0f), -dir * 2.5f, 1.5f, 0.5f, 0.4f, 1.2f, new Color(0.75f, 0.72f, 0.65f, 0.35f), new Color(0.75f, 0.72f, 0.65f, 0f), 0f);
                if (Random.value < 0.6f) AddWet(at + new Vector3(0f, -0.5f, 0f), 0.8f, 4f, false);
            }
        }

        private void DrawSpecials(float dt)
        {
            if ((_sim.Wind.X != 0f || _sim.Wind.Y != 0f) && _sim.Outcome == SOutcome.Playing && Random.value < 0.6f)
            {
                // 산불 숲: 화면 곳곳에서 재와 불티가 바람 쪽으로 날린다.
                float halfH = _viewHalfH;
                float halfW = _viewHalfW;
                var at = new Vector3(_cameraAt.x + Random.Range(-halfW, halfW), _cameraAt.y + Random.Range(-halfH, halfH), 0f);
                var wind = new Vector3(_sim.Wind.X, _sim.Wind.Y, 0f) * Random.Range(3f, 5f);
                bool spark = Random.value < 0.3f;
                Emit(spark ? Sparks[Random.Range(0, Sparks.Length)] : "Effects/smoke_01", at, wind + new Vector3(0f, 0.3f, 0f), 0f, Random.Range(1f, 1.6f), spark ? 0.2f : 0.35f, 0.05f,
                    spark ? new Color(1f, 0.6f, 0.2f, 0.8f) : new Color(0.3f, 0.28f, 0.27f, 0.45f), new Color(0.3f, 0.28f, 0.27f, 0f), Random.Range(-200f, 200f), spark);
            }

            if (_sim.Truck.HasValue)
            {
                Vector3 at = W(_sim.Truck.Value);
                float dir = _sim.TruckDir;
                if (!_truckShown)
                {
                    SpecialBanner("소방차 출동!", new Color(1f, 0.3f, 0.3f));
                    GameAudio.Play(Cue.Critical);
                }
                // 차선 예고: 달릴 줄 앞쪽에 붉은 띠가 깔려 있다(지나간 쪽은 사라진다).
                float ahead = 14f;
                _band.Put(at + new Vector3(dir * ahead * 0.5f, 0f, 0f), 3.5f, -90f, new Color(1f, 0.3f, 0.25f, 0.18f + (0.06f * Mathf.Sin(_time * 10f))), null, ahead / 3.5f);
                _shadows.Put(at + new Vector3(0f, -0.45f, 0f), 5f, 0f, new Color(0f, 0f, 0f, 0.4f), null, 0.4f);
                GameObject truckModel = _truckModels.Get();
                if (truckModel != null)
                {
                    truckModel.transform.localPosition = new Vector3(at.x, at.y, 0f);
                    truckModel.transform.localRotation = Models3D.Stand * Quaternion.Euler(0f, dir > 0f ? -90f : 90f, 0f);
                }
                // 사이렌: 지붕 앞뒤가 빨강·파랑으로 번갈아 번쩍이고 그 빛이 바닥을 물들인다.
                bool flip = Mathf.Repeat(_time * 6f, 1f) < 0.5f;
                Color sirenA = flip ? new Color(1f, 0.15f, 0.1f, 0.95f) : new Color(0.2f, 0.4f, 1f, 0.95f);
                _siren.Put(at + new Vector3(dir * 0.7f, 0.15f, 0f), 3f, 0f, sirenA);
                _siren.Put(at + new Vector3(-dir * 0.1f, 0.15f, 0f), 2.4f, 0f, flip ? new Color(0.2f, 0.4f, 1f, 0.85f) : new Color(1f, 0.15f, 0.1f, 0.85f));
                _groundGlow.Put(at, 8f, 0f, new Color(sirenA.r, sirenA.g, sirenA.b, 0.28f));
                // 양옆 물대포: 위아래로 굵은 물줄기 리본이 뻗고 끝에서 물이 튄다. 지나간 자리는 젖는다.
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 hand = at + new Vector3(0f, side * 0.5f, 0f);
                    Vector3 tip = hand + new Vector3(-dir * 1.2f, side * SurvivorSim.TruckSoakRange * 1.4f, 0f);
                    SmallRibbon(hand, tip, 0.55f, new Color(0.75f, 0.95f, 1f, 0.95f), 0.3f);
                    Splash(tip, 2, 0.5f);
                    for (int k = 0; k < 2; k++)
                    {
                        var v = new Vector3((-dir * Random.Range(0.5f, 2f)) + Random.Range(-1f, 1f), side * Random.Range(7f, 10f), 0f);
                        Emit("Effects/water_drop", hand, v, 3f, 0.3f, 0.4f, 0.12f, new Color(0.75f, 0.95f, 1f, 1f), new Color(0.6f, 0.9f, 1f, 0f), 0f);
                    }
                }
                if (Random.value < 0.5f) AddWet(at, 2.5f, 5f, false);
                if (Random.value < 0.4f) Splash(at - new Vector3(dir * 1.4f, 0f, 0f), 1, 0.4f);
                _trauma = Mathf.Max(_trauma, 0.08f);
            }
            _truckShown = _sim.Truck.HasValue;

            DrawAmbulance(dt);
            DrawHarborSpecials();
            if (_sim.Rockets.Count > 0) DrawRockets();
            DrawMist();
            if (_sim.FrontY.HasValue) DrawFront();

            if (_sim.RainAt.HasValue)
            {
                Vector3 at = W(_sim.RainAt.Value);
                float r = SurvivorSim.RainRadius;
                float fade = Mathf.Clamp01(_sim.RainLeft / 0.4f) * Mathf.Clamp01((SurvivorSim.RainTime - _sim.RainLeft) / 0.3f);
                _rainShade.Put(at, r * 3.2f, 0f, new Color(0.05f, 0.1f, 0.2f, 0.5f * fade));
                // 먹구름: 크고 어두운 원반 셋이 겹치고 가장자리만 밝다. 연기 덩어리처럼 회색으로 번지지 않는다.
                for (int k = 0; k < 3; k++)
                {
                    float a = (k * 2.1f) + (_time * 0.25f);
                    Vector3 o = new Vector3(Mathf.Cos(a) * r * 0.4f, (Mathf.Sin(a) * r * 0.15f) + 3.4f, 0f);
                    float size = r * (1.8f - (0.2f * k));
                    _shadows.Put(at + o, size, 0f, new Color(0.08f, 0.1f, 0.16f, 0.92f * fade), DiscSprite(), 0.6f);
                    _cloud.Put(at + o + new Vector3(0f, 0.4f, 0f), size * 0.92f, 0f, new Color(0.6f, 0.65f, 0.75f, 0.5f * fade), DiscSprite(), 0.6f);
                }
                // 번개: 0.7초마다 구름에서 바닥으로 흰 줄기가 꽂히고 화면이 번쩍, 바닥 충격파.
                _lightningClock -= dt;
                if (_lightningClock <= 0f && fade > 0.5f)
                {
                    _lightningClock = 0.7f;
                    Vector3 g = at + new Vector3(Random.Range(-r, r) * 0.7f, Random.Range(-r, r) * 0.4f, 0f);
                    Vector3 top = g + new Vector3(Random.Range(-1f, 1f), 3.4f + (r * 0.3f), 0f);
                    Vector3 mid = Vector3.Lerp(top, g, 0.5f) + new Vector3(Random.Range(-1.2f, 1.2f), 0f, 0f);
                    Vector3[] pts = { top, mid, g };
                    for (int k = 0; k < 2; k++)
                    {
                        Vector3 seg = pts[k + 1] - pts[k];
                        float ang = (Mathf.Atan2(seg.y, seg.x) * Mathf.Rad2Deg) - 90f;
                        EmitSprite(BeamSprite(), (pts[k] + pts[k + 1]) * 0.5f, Vector3.zero, 0f, 0.14f, 0.3f, 0.12f, new Color(1f, 1f, 1f, 1f), new Color(0.8f, 0.9f, 1f, 0f), 0f, true, 0f, seg.magnitude / 0.3f, ang);
                        EmitSprite(BeamSprite(), (pts[k] + pts[k + 1]) * 0.5f, Vector3.zero, 0f, 0.2f, 0.9f, 0.3f, new Color(0.7f, 0.85f, 1f, 0.6f), new Color(0.7f, 0.85f, 1f, 0f), 0f, true, 0f, seg.magnitude / 0.9f, ang);
                    }
                    Flash(new Color(0.9f, 0.95f, 1f), 0.15f);
                    Shockwave(g, new Color(0.9f, 0.97f, 1f, 0.9f), 4f, 0.25f);
                    _trauma = Mathf.Min(1f, _trauma + 0.08f);
                }
                // 빗줄기: 구름 밑에서 곧게 떨어지는 가는 선, 땅에 닿으면 물결.
                for (int k = 0; k < 30; k++)
                {
                    var p = at + new Vector3(Random.Range(-r, r) * 0.95f, Random.Range(-r * 0.5f, r * 0.5f) + 2.8f, 0f);
                    EmitSprite(BeamSprite(), p, new Vector3(-1f, -18f, 0f), 0f, 0.18f, 0.07f, 0.07f, new Color(0.8f, 0.9f, 1f, 0.8f * fade), new Color(0.8f, 0.9f, 1f, 0f), 0f, true, 0f, 9f, 3f);
                }
                for (int k = 0; k < 8; k++)
                {
                    var g = at + new Vector3(Random.Range(-r, r) * 0.9f, Random.Range(-r, r) * 0.6f, 0f);
                    EmitSprite(RingSprite(), g, Vector3.zero, 0f, 0.35f, 0.2f, 1.1f, new Color(0.8f, 0.95f, 1f, 0.6f * fade), new Color(0.8f, 0.95f, 1f, 0f), 0f, true);
                }
            }

            if (_sim.FoamAt.HasValue)
            {
                // 폼 깔개: 흰 거품 덩어리가 원을 덮고, 걷히기 1초 전부터 옅어진다.
                Vector3 at = W(_sim.FoamAt.Value);
                float r = SurvivorSim.FoamRadius;
                float fade = Mathf.Clamp01(_sim.FoamLeft / 1f) * Mathf.Clamp01((SurvivorSim.FoamTime - _sim.FoamLeft) / 0.25f);
                // 깔개 바탕은 옅은 흰 원 하나. 거품 자체는 폼이 깔릴 때 뿌린 BubbleSprite 입자가 맡는다.
                _groundGlow.Put(at, r * 2.2f, 0f, new Color(0.85f, 0.95f, 1f, 0.4f * fade));
                // 매트 가장자리 흰 테가 폼이 걷힐 때까지 남고, 0.2초마다 작은 거품이 떠올라 터진다.
                _auras.Put(at, r * 2f, 0f, new Color(1f, 1f, 1f, 0.6f * fade));
                if (Random.value < 0.3f * fade) Steam(at + new Vector3(Random.Range(-r, r) * 0.6f, Random.Range(-r, r) * 0.5f, 0f), 1, 0.4f);
                _foamPopClock -= dt;
                if (_foamPopClock <= 0f)
                {
                    _foamPopClock = 0.2f;
                    for (int k = 0; k < 3; k++)
                    {
                        Vector3 o = new Vector3(Random.Range(-r, r) * 0.8f, Random.Range(-r, r) * 0.6f, 0f);
                        EmitSprite(BubbleSprite(), at + o, new Vector3(0f, 1.2f, 0f), 0f, 0.6f, 0.3f, 0.7f, new Color(1f, 1f, 1f, 0.9f * fade), new Color(1f, 1f, 1f, 0f), Random.Range(-5f, 5f), false);
                    }
                }
            }

            foreach (Band b in _sim.Retardants)
            {
                Vector3 a = W(b.A);
                Vector3 c = W(b.B);
                float len = Vector3.Distance(a, c);
                float deg = Mathf.Atan2(c.y - a.y, c.x - a.x) * Mathf.Rad2Deg;
                float t = Mathf.Clamp01(b.Life / b.MaxLife);
                // 비행기가 지나간 만큼만 띠가 칠해진다.
                float laid = _planeAge < 1.2f && b == _sim.Retardants[_sim.Retardants.Count - 1] ? Mathf.Clamp01(((_planeAge / 1.2f) * (len + 20f) - 10f) / len) : 1f;
                if (laid <= 0f) continue;
                Vector3 start = a;
                Vector3 end = Vector3.Lerp(a, c, laid);
                // 띠 3겹: 넓은 바탕, 중간, 밝은 핵. 걷히기 3초 전부터 깜빡인다.
                float life = 0.3f + (0.7f * t);
                if (b.Life < 3f) life *= 0.5f + (0.5f * Mathf.Sin(_time * 8f));
                Vector3 mid = (start + end) * 0.5f;
                float w0 = SurvivorSim.RetardantWidth * 1.15f, w1 = SurvivorSim.RetardantWidth * 0.8f, w2 = SurvivorSim.RetardantWidth * 0.33f;
                _band.Put(mid, w0, deg - 90f, new Color(0.9f, 0.25f, 0.3f, 0.45f * life), null, len * laid / w0);
                _band.Put(mid, w1, deg - 90f, new Color(1f, 0.4f, 0.45f, 0.6f * life), null, len * laid / w1);
                _band.Put(mid, w2, deg - 90f, new Color(1f, 0.8f, 0.85f, 0.9f * life), null, len * laid / w2);
            }
            if (_planeAge < 1.2f)
            {
                _planeAge += dt;
                float f = Mathf.Clamp01(_planeAge / 1.2f);
                Vector3 at = Vector3.Lerp(_planeFrom, _planeTo, f);
                Vector3 dir = (_planeTo - _planeFrom).normalized;
                Vector3 wing = new Vector3(-dir.y, dir.x, 0f);
                float deg = (Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg) - 90f;
                _shadows.Put(at + new Vector3(1f, -1.5f, 0f), 4.2f, deg, new Color(0f, 0f, 0f, 0.3f), null, 0.6f);
                GameObject planeModel = _planeModels.Get();
                if (planeModel != null) ItemModels.Place(planeModel, at, 5f, dir, 1.5f);
                // 양 날개 끝에서 분홍 분사 두 줄기가 쏟아지고, 지나간 자리에 분홍 연기가 깔린다.
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 tip = at + (wing * side * 1.6f) + new Vector3(0f, 2f, 0f);
                    for (int k = 0; k < 3; k++)
                    {
                        Emit("Effects/glow", tip, new Vector3(Random.Range(-0.6f, 0.6f), -Random.Range(3.5f, 5f), 0f), 1.5f, 0.5f, 0.7f, 1.5f,
                            new Color(1f, 0.4f, 0.5f, 0.8f), new Color(1f, 0.4f, 0.5f, 0f), 0f);
                    }
                }
                for (int k = 0; k < 2; k++)
                {
                    Emit("Effects/smoke_02", at + new Vector3(Random.Range(-1f, 1f), Random.Range(-0.5f, 0.5f), 0f), -dir * 1.5f, 1.5f, 1.2f, 0.9f, 2.2f,
                        new Color(1f, 0.55f, 0.65f, 0.35f), new Color(1f, 0.55f, 0.65f, 0f), Random.Range(-1f, 1f));
                }
            }
        }

        /// <summary>공구상자: 주황빛 위에서 통통 튀고, 사라지기 5초 전부터 깜빡인다.</summary>
        private void DrawToolboxes()
        {
            foreach (Pickup box in _sim.Toolboxes)
            {
                Vector3 at = W(box.Pos);
                float bob = Mathf.Abs(Mathf.Sin(_time * 4f)) * 0.3f;
                bool blink = box.Life < 5f && Mathf.Sin(_time * 18f) < 0f;
                float a = blink ? 0.35f : 1f;
                _shadows.Put(at + new Vector3(0f, -0.35f, 0f), 1f - (bob * 0.8f), 0f, new Color(0f, 0f, 0f, 0.35f), null, 0.5f);
                _toolboxGlow.Put(at, 3f + (0.4f * Mathf.Sin(_time * 6f)), 0f, new Color(1f, 0.6f, 0.2f, 0.55f * a));
                _toolbox.Put(at + new Vector3(0f, bob, 0f), 1.5f, 0f, new Color(1f, 1f, 1f, a));
            }
        }

        /// <summary>구급상자: 흰 상자에 빨간 십자. 통통 튀고 붉은 빛이 돌며, 사라지기 5초 전부터 깜빡인다.</summary>
        private void DrawKits()
        {
            foreach (Pickup kit in _sim.Kits)
            {
                Vector3 at = W(kit.Pos);
                float bob = Mathf.Abs(Mathf.Sin(_time * 4f)) * 0.3f;
                bool blink = kit.Life < 5f && Mathf.Sin(_time * 18f) < 0f;
                float a = blink ? 0.35f : 1f;
                _shadows.Put(at + new Vector3(0f, -0.35f, 0f), 1f - (bob * 0.8f), 0f, new Color(0f, 0f, 0f, 0.35f), null, 0.5f);
                _toolboxGlow.Put(at, 3f + (0.4f * Mathf.Sin(_time * 6f)), 0f, new Color(1f, 0.45f, 0.45f, 0.5f * a));
                _kit.Put(at + new Vector3(0f, bob, 0f), 1.4f, 0f, new Color(1f, 1f, 1f, a));
            }
        }

        /// <summary>월드 글자 태그 하나를 이번 프레임에 놓는다. 프레임 끝의 EndTags가 안 쓴 것을 끈다.</summary>
        private void Tag(Vector3 at, string text, Color color, float size)
        {
            TextMesh t;
            if (_tagsUsed < _tags.Count)
            {
                t = _tags[_tagsUsed];
            }
            else
            {
                t = NewText();
                t.GetComponent<MeshRenderer>().sortingOrder = 23;
                _tags.Add(t);
            }
            _tagsUsed++;
            t.gameObject.SetActive(true);
            t.text = text;
            t.color = color;
            t.characterSize = size;
            t.transform.localPosition = at;
        }

        private void EndTags()
        {
            for (int i = _tagsUsed; i < _tags.Count; i++) _tags[i].gameObject.SetActive(false);
            _tagsUsed = 0;
        }

        /// <summary>보물상자: 금빛 위에서 통통 튀고, 빛기둥이 서고, 사라지기 5초 전부터 깜빡인다.</summary>
        private void DrawChests()
        {
            foreach (Pickup chest in _sim.Chests)
            {
                Vector3 at = W(chest.Pos);
                float bob = Mathf.Abs(Mathf.Sin(_time * 4f)) * 0.35f;
                bool blink = chest.Life < 5f && Mathf.Sin(_time * 18f) < 0f;
                float a = blink ? 0.35f : 1f;
                _shadows.Put(at + new Vector3(0f, -0.4f, 0f), 1.2f - (bob * 0.8f), 0f, new Color(0f, 0f, 0f, 0.35f), null, 0.5f);
                _toolboxGlow.Put(at, 4f + (0.6f * Mathf.Sin(_time * 6f)), 0f, new Color(1f, 0.85f, 0.3f, 0.6f * a));
                _toolboxGlow.Put(at + new Vector3(0f, 2.2f, 0f), 1.4f, 0f, new Color(1f, 0.9f, 0.5f, 0.45f * a), null, 4f);
                _chest.Put(at + new Vector3(0f, bob, 0f), 1.8f, 0f, new Color(1f, 1f, 1f, a));
            }
        }

        private void DrawGems()
        {
            foreach (Gem g in _sim.Gems)
            {
                Color c = g.Value >= 25 ? new Color(1f, 0.3f, 0.35f) : g.Value >= 5 ? new Color(0.35f, 1f, 0.45f) : new Color(0.35f, 0.7f, 1f);
                float bob = g.Pulled ? 0f : 0.06f * Mathf.Sin((_time * 5f) + g.Pos.X);
                float size = g.Value >= 25 ? 0.95f : g.Value >= 5 ? 0.75f : 0.55f;
                Vector3 at = W(g.Pos) + new Vector3(0f, bob, 0f);
                _gems.Put(at, size * 0.42f, 45f, c, null, 1.4f);
                _gemCores.Put(at + new Vector3(-0.04f, 0.05f, -0.01f), size * 0.16f, 45f, new Color(1f, 1f, 1f, 0.85f), null, 1.4f);
            }
        }

        /// <summary>구해 낸 사람: 문 앞에서 아래로 뛰어 나가며 사라진다.</summary>
        private void DrawCivilians()
        {
            for (int i = 0; i < _sim.Civilians.Count; i++)
            {
                Civilian c = _sim.Civilians[i];
                float t = 1f - Mathf.Clamp01(c.Life / 1.5f);
                Vector3 at = W(c.Pos) + new Vector3(Mathf.Sin(i * 2.3f) * 1.2f * t, -2.6f * t, 0f);
                float a = t < 0.7f ? 1f : 1f - ((t - 0.7f) / 0.3f);
                _civilianRings.Put(at, 1.2f, 0f, new Color(0.4f, 1f, 0.4f, 0.4f * a));
                _shadows.Put(at + new Vector3(0f, -0.3f, 0f), 0.8f, 0f, new Color(0f, 0f, 0f, 0.35f * a), null, 0.5f);
                // 3D 시민은 반투명이 안 되니 거의 사라질 때 숨긴다.
                if (a < 0.2f) continue;
                GameObject person = _people.Get(CivilianModel(i));
                if (person == null) continue;
                Models3D.Pose(person, at, new Vector3(Mathf.Sin(i * 2.3f) * 1.2f, -2.6f, 0f));
                Models3D.Play(person, "Run", 1.2f, _time + i);
                Models3D.Tint(person, Color.white, CivilianColor(i), 10 + CivilianKind(i));
            }
        }

        private void DrawPuddles()
        {
            for (int i = 0; i < _sim.BurningGround.Count; i++)
            {
                Puddle p = _sim.BurningGround[i];
                float t = Mathf.Clamp01(p.Life / p.MaxLife);
                Vector3 at = W(p.Pos);
                // 끄지 않으면 곧 번진다: 마지막 1초는 크게 맥동하며 경고한다.
                float warn = p.Life < 1f ? 1f + (0.3f * Mathf.Abs(Mathf.Sin(_time * 12f))) : 1f;
                float big = 1.3f * warn * (p.Radius / 0.9f);
                t = Mathf.Max(t, p.Life < 1f ? 0.6f : 0f);
                if (p.Oil)
                {
                    // 기름 불: 무지갯빛 검은 기름 얼룩 위로 보라·주황 불. 닿은 건물에 옮겨붙는다.
                    // 불꽃은 하나만: 기름 불은 여럿이 한데 모여 불더미가 되기 쉬워, 검은 얼룩이 보이게 불을 적게 그린다.
                    _foam.Put(at, p.Radius * 3.4f, 0f, new Color(0.07f, 0.05f, 0.09f, 0.95f * Mathf.Max(t, 0.5f)), Art.Get("Effects/glow"), 0.8f);
                    _groundGlow.Put(at, p.Radius * 3f * warn, 0f, new Color(0.9f, 0.2f, 0.8f, 0.45f * t));
                    float flick = 0.55f + (0.15f * Mathf.Sin((_time * 14f) + i));
                    _groundFire.Put(at + new Vector3(0f, 0.25f * big, 0f), flick * (0.7f + (0.4f * t)) * big, 0f, new Color(1f, 1f, 1f, t), FlameArt.Frame(_oilSheet, _time, i), 0.8f);
                    continue;
                }
                _groundGlow.Put(at, p.Radius * 2.4f * warn, 0f, new Color(1f, 0.3f, 0.05f, 0.4f * t));
                for (int k = 0; k < 3; k++)
                {
                    float a = (k * 2.1f) + i;
                    Vector3 o = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.4f;
                    float f = 0.6f + (0.15f * Mathf.Sin((_time * 16f) + (k * 2f) + i));
                    // 납작하게 누운 불꽃 플립북 셋(불씨 시트). 세로를 눌러 바닥에 깔린 듯 보인다.
                    _groundFire.Put(at + (o * big) + new Vector3(0f, 0.2f * big, 0f), f * (0.55f + (0.45f * t)) * big, 0f, new Color(1f, 1f, 1f, t), FlameArt.Frame(_emberSheet, _time, i + (k * 3)), 0.75f);
                }
            }
        }

        private void DrawShots()
        {
            int hose = _sim.Build.Level(UpgradeId.Hose);
            int tank = _sim.Build.Level(UpgradeId.Tank);
            int bombLevel = _sim.Build.Level(UpgradeId.WaterBomb);

            // 조준: 가장 최근에 나간 물줄기 방향. 그 묶음은 노즐에 붙여 그린다.
            float newestDrop = float.MaxValue;
            float newestJet = float.MaxValue;
            foreach (Shot s in _sim.Shots)
            {
                if (s.Kind == ShotKind.Drop && s.Age < newestDrop) newestDrop = s.Age;
                if (s.Kind == ShotKind.Jet && s.Age < newestJet) newestJet = s.Age;
            }
            Vector3 aimSum = Vector3.zero;
            foreach (Shot s in _sim.Shots)
            {
                bool newest = (s.Kind == ShotKind.Drop && s.Age <= newestDrop + 0.001f) || (s.Kind == ShotKind.Jet && s.Age <= newestJet + 0.001f && newestDrop == float.MaxValue);
                if (newest) aimSum += new Vector3(s.Vel.X, s.Vel.Y, 0f).normalized;
            }
            float newestAny = Mathf.Min(newestDrop, newestJet);
            if (aimSum.sqrMagnitude > 0.0001f)
            {
                _aim = aimSum.normalized;
                _aimAge = newestAny;
            }
            else
            {
                _aimAge = 99f;
            }
            DrawHoseStream();

            foreach (Shot s in _sim.Shots)
            {
                Vector3 at = W(s.Pos);
                switch (s.Kind)
                {
                    case ShotKind.Drop:
                    case ShotKind.Jet:
                    {
                        // 호스 물은 DrawHoseStream이 한 줄기로 이어 그렸다. 여기는 방수포가 사방으로 휩쓰는 제트만.
                        if (s.Hose) break;
                        bool jet = s.Kind == ShotKind.Jet;
                        Vector3 dir = new Vector3(s.Vel.X, s.Vel.Y, 0f).normalized;
                        Vector3 tail = at - (dir * Mathf.Min(s.Pos.DistanceTo(s.From), jet ? 3.4f : 4.8f));
                        // 날아가는 한 토막도 호스와 같은 물 리본으로(끝 물보라는 DrawStream이 낸다).
                        _streamPts.Clear();
                        _streamPts.Add(tail);
                        _streamPts.Add(at);
                        DrawStream(_streamPts, s);
                        break;
                    }
                    case ShotKind.Heli:
                        DrawHeli(s);
                        break;
                    case ShotKind.Shell:
                        DrawShell(s);
                        break;
                    case ShotKind.Bomb when s.Drone:
                    {
                        // 드론 물폭탄: 드론 밑에서 곧장 떨어진다. 청록 꼬리.
                        float dt = Mathf.Clamp01(s.Age / s.Life);
                        float dlift = Mathf.Lerp(2.2f, 0f, dt * dt);
                        _bombShadows.Put(W(s.Target), Mathf.Lerp(0.5f, 0.9f, dt), 0f, new Color(0f, 0f, 0f, 0.2f + (0.25f * dt)));
                        GameObject drop = _bombModels.Get();
                        if (drop != null)
                        {
                            ItemModels.Place(drop, at, 0.4f + dlift, Vector3.right, 0.5f);
                            Models3D.Tint(drop, DroneTint);
                        }
                        _dropGlow.Put(at + new Vector3(0f, dlift, 0f), 0.8f, 0f, new Color(DroneTint.r, DroneTint.g, DroneTint.b, 0.3f));
                        if (Random.value < 0.6f)
                        {
                            Emit("Effects/water_drop", at + new Vector3(0f, dlift, 0f), new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0.5f, 1.5f), 0f), 3f, 0.25f,
                                0.18f, 0.04f, DroneTint, new Color(DroneTint.r, DroneTint.g, DroneTint.b, 0f), 0f);
                        }
                        break;
                    }
                    case ShotKind.Bomb when s.Air:
                    {
                        // 공중 소화탄: 하늘에서 비스듬히 떨어진다. 땅 그림자는 점점 작고 짙어진다.
                        float ft = Mathf.Clamp01(s.Age / s.Life);
                        Vector3 ground = W(s.Target);
                        _bombShadows.Put(ground, Mathf.Lerp(3f, 1.2f, ft), 0f, new Color(0f, 0f, 0f, Mathf.Lerp(0.1f, 0.5f, ft)));
                        _civilianRings.Put(ground, Mathf.Lerp(6f, 3f, ft), 0f, new Color(1f, 0.4f, 0.2f, 0.25f + (0.3f * ft)));
                        Vector3 fly = new Vector3(s.Target.X - s.From.X, s.Target.Y - s.From.Y, 0f);
                        _airBombs.Put(at, 1.3f, (Mathf.Atan2(fly.y, fly.x) * Mathf.Rad2Deg) - 90f, Color.white);
                        if (Random.value < 0.5f)
                        {
                            Emit(Smokes[Random.Range(0, Smokes.Length)], at - (fly.normalized * 0.6f), -fly.normalized, 1f, 0.35f, 0.3f, 0.8f,
                                new Color(1f, 1f, 1f, 0.5f), new Color(1f, 1f, 1f, 0f), 0f);
                        }
                        break;
                    }
                    case ShotKind.Bomb:
                        float t = Mathf.Clamp01(s.Age / s.Life);
                        float lift = Mathf.Sin(t * Mathf.PI) * 2f;
                        float bombSize = 0.8f + (0.08f * bombLevel);
                        _bombShadows.Put(at, 0.6f * bombSize, 0f, new Color(0f, 0f, 0f, 0.35f));
                        // 물풍선 모델: 포물선 높이로 떠서 날아가며 구른다.
                        GameObject bombModel = _bombModels.Get();
                        if (bombModel != null)
                        {
                            float roll = t * 9f;
                            ItemModels.Place(bombModel, at, 0.4f + lift, new Vector3(Mathf.Cos(roll), Mathf.Sin(roll), 0f), bombSize * 0.75f);
                        }
                        _dropGlow.Put(at + new Vector3(0f, lift, 0f), bombSize * (1.2f + (0.15f * bombLevel)), 0f, new Color(0.55f, 0.8f, 1f, 0.25f + (0.04f * bombLevel)));
                        // 날아가며 물방울 꼬리를 흘린다.
                        if (Random.value < 0.8f)
                        {
                            Emit("Effects/water_drop", at + new Vector3(0f, lift, 0f), new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-1.5f, 0f), 0f), 3f, 0.3f,
                                0.25f * bombSize, 0.05f, new Color(0.7f, 0.93f, 1f, 0.9f), new Color(0.6f, 0.9f, 1f, 0f), 0f);
                        }
                        break;
                }
            }

            DrawHeliExit();
            DrawPartner();

            int droneLevel = _sim.Build.PowerOf(UpgradeId.Drone);
            // 순찰 드론은 불난 건물 위를 돌고, 없으면 소방관 곁을 돈다.
            Vector3 center = W(_sim.DroneCenter);
            Structure patrol = _sim.DroneTarget;
            float orbit = patrol != null ? Mathf.Max(patrol.Half.X, patrol.Half.Y) + 0.6f : 2.3f;
            if (droneLevel >= 3)
            {
                // 드론 궤도를 잇는 물 고리. 최대 레벨이면 밝게 맥동한다.
                float pulse = droneLevel >= Loadout.MaxLevel ? 0.3f + (0.15f * Mathf.Sin(_time * 8f)) : 0.16f;
                _auras.Put(center, orbit * 2f / 0.85f, 0f, new Color(0.4f, 0.8f, 1f, pulse));
            }
            float droneScale = droneLevel >= Loadout.MaxLevel ? 1.3f : 1f;
            for (int i = 0; i < _sim.Drones.Count; i++)
            {
                Vector3 at = W(_sim.Drones[i]);
                // 지나온 궤도를 따라 옅어지는 물 꼬리
                float a = Mathf.Atan2(at.y - center.y, at.x - center.x);
                for (int j = 1; j <= 6; j++)
                {
                    float b = a - (j * 0.13f);
                    Vector3 trail = center + (new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0f) * orbit);
                    _droneGlow.Put(trail, (0.9f - (j * 0.1f)) * droneScale, 0f, new Color(0.55f, 0.8f, 1f, 0.45f - (j * 0.06f)));
                }
                _shadows.Put(at + new Vector3(0f, -0.35f, 0f), 0.6f * droneScale, 0f, new Color(0f, 0f, 0f, 0.3f), null, 0.5f);
                bool goldDrone = droneLevel >= Loadout.MaxLevel;
                _droneGlow.Put(at, 1.6f * droneScale, 0f, goldDrone ? new Color(1f, 0.8f, 0.3f, 0.55f) : new Color(0.55f, 0.8f, 1f, 0.45f));
                // 쿼드콥터: 궤도 진행 방향을 보고, 네 팔 끝 날개가 빠르게 돈다.
                float heading = (a * Mathf.Rad2Deg) + 90f;
                float body = 0.95f * droneScale;
                // 쿼드콥터 모델: 1.6칸 떠서 궤도 진행 방향을 보고, 네 날개가 빠르게 돈다. 최대 레벨이면 금빛.
                GameObject droneModel = _droneModels.Get();
                if (droneModel != null)
                {
                    float hd = heading * Mathf.Deg2Rad;
                    ItemModels.Place(droneModel, at, 1.6f + (0.08f * Mathf.Sin((_time * 5f) + i)), new Vector3(Mathf.Cos(hd), Mathf.Sin(hd), 0f), body * 1.15f);
                    ItemModels.Spin(droneModel, "Rotor", 2200f, _time);
                    Models3D.Tint(droneModel, goldDrone ? new Color(1f, 0.85f, 0.45f) : Color.white);
                }
                if (goldDrone) _auras.Put(at, 1.3f, 0f, new Color(1f, 0.85f, 0.35f, 0.85f));
                // 불난 지붕 위: 드론마다 지붕 가운데로 물줄기를 뿌린다.
                if (patrol != null && patrol.Burning && Vector3.Distance(center, W(patrol.Pos)) < 0.6f)
                {
                    Vector3 hitAt = Vector3.Lerp(at, center, 0.75f);
                    SprayLine(at, hitAt, 0.26f * droneScale, new Color(0.6f, 0.9f, 1f, 0.9f));
                    // 드론 아래로 떨어지는 물 커튼 + 지붕에서 올라오는 김.
                    if (Random.value < 0.6f)
                    {
                        EmitFalling("Effects/water_drop", at + new Vector3(Random.Range(-0.3f, 0.3f), 0f, 0f), new Vector3(Random.Range(-0.5f, 0.5f), -1f, 0f), 0.35f, 0.28f,
                            new Color(0.7f, 0.93f, 1f, 0.95f));
                    }
                    if (Random.value < 0.35f) Splash(hitAt, 1, 0.3f);
                    if (Random.value < 0.06f) Steam(hitAt + new Vector3(0f, 0.3f, 0f), 1, 1.1f);
                }
            }
            // 구조 드론: 갇힌 사람이 있는 지붕 위에서 노란 구조 줄을 내려 끌어올린다.
            if (patrol != null && _sim.Build.Level(UpgradeId.RescueDrone) > 0 && patrol.DroneRescue > 0f)
            {
                float lift = Mathf.Clamp01(patrol.DroneRescue / SurvivorSim.DroneRescueTime);
                Vector3 top = center + new Vector3(0f, 2.2f, 0f);
                SprayLine(top, center, 0.08f, new Color(1f, 0.85f, 0.3f, 0.95f));
                GameObject lifted = _people.Get(CivilianModel(0), 0.75f);
                if (lifted != null)
                {
                    Models3D.Pose(lifted, Vector3.Lerp(center, top, lift), Vector3.down);
                    Models3D.Play(lifted, "Jump", 1f, _time);
                    Models3D.Tint(lifted, Color.white, CivilianColor(0), 10 + CivilianKind(0));
                }
            }

            DrawTurrets();
            DrawSpreadWarnings();
            DrawTracers();
            DrawCurtainCharge();
        }

        /// <summary>from에서 to로 곧은 물줄기(흰 막대를 늘여 돌린다).</summary>
        private void SprayLine(Vector3 from, Vector3 to, float width, Color color)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 0.05f) return;
            float deg = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float wob = 1f + (0.15f * Mathf.Sin(_time * 30f));
            _sprayLines.Put((from + to) * 0.5f, len, deg, color, null, width * wob / len);
            _sprayLines.Put((from + to) * 0.5f, len, deg, new Color(1f, 1f, 1f, color.a * 0.6f), null, width * 0.35f / len);
        }

        private readonly List<Vec2> _smallIn = new List<Vec2>(2);
        private readonly List<WaterRibbon.Point> _smallRibbon = new List<WaterRibbon.Point>();
        private readonly List<WaterRibbon.Blob> _smallBlobs = new List<WaterRibbon.Blob>();

        /// <summary>대원·포탑의 짧은 물줄기: 호스와 같은 리본(몸통 + 하이라이트)을 작게. width는 리본 폭(칸).</summary>
        private void SmallRibbon(Vector3 from, Vector3 to, float width, Color color, float lift)
        {
            if ((to - from).sqrMagnitude < 0.01f) return;
            _smallIn.Clear();
            _smallIn.Add(new Vec2(from.x, from.y));
            _smallIn.Add(new Vec2(to.x, to.y));
            WaterRibbon.Build(_smallIn, width, _time, 0.8f, _smallRibbon, _smallBlobs);
            if (_smallRibbon.Count < 2) return;
            _waterBody.Put(_smallRibbon, 1f, 0f, new Color(color.r, color.g, color.b, 0.85f), 9f, lift);
            _waterShine.Put(_smallRibbon, 0.3f, 0.3f, new Color(1f, 1f, 1f, 0.5f), 14f, lift);
            foreach (WaterRibbon.Blob b in _smallBlobs)
            {
                _streamJoint.Put(new Vector3(b.Pos.X, b.Pos.Y, 0f), b.Radius * 0.9f, 0f, new Color(color.r, color.g, color.b, b.Alpha * 0.8f));
            }
        }

        /// <summary>방수 포탑: 삼각대 위 노즐이 쏘는 곳을 향한다. 현장 구조소는 초록 영역과 흰 천막 십자.</summary>
        private void DrawTurrets()
        {
            bool post = _sim.Build.Level(UpgradeId.RescuePost) > 0;
            for (int k = 0; k < _sim.Turrets.Count; k++)
            {
                Turret tu = _sim.Turrets[k];
                Vector3 at = W(tu.Pos);
                // 세운 지 0.2초 동안은 하늘에서 떨어지는 중(그림자만 먼저 커진다).
                float age = tu.MaxLife - tu.Life;
                float fall = Mathf.Clamp01(1f - (age / TurretDropTime));
                Vector3 drop = new Vector3(0f, fall * fall * 5f, 0f);
                float kick = k < _turretKick.Length ? _turretKick[k] : 0f;
                if (k < _turretKick.Length) _turretKick[k] = Mathf.Max(0f, _turretKick[k] - (_frameDt * 8f));
                float life = Mathf.Clamp01(tu.Life / tu.MaxLife);
                float blink = tu.Life < 1.5f && Mathf.Sin(_time * 18f) < 0f ? 0.4f : 1f;
                if (post)
                {
                    _civilianRings.Put(at, SurvivorSim.PostRange * 2f, 0f, new Color(0.4f, 1f, 0.5f, 0.18f + (0.06f * Mathf.Sin(_time * 3f))));
                    _sprayLines.Put(at + new Vector3(0f, 0.9f, 0f), 1.4f, 0f, new Color(1f, 1f, 1f, 0.95f * blink), null, 0.9f / 1.4f);
                    _sprayLines.Put(at + new Vector3(0f, 0.9f, 0f), 0.9f, 0f, new Color(0.9f, 0.15f, 0.15f, blink), null, 0.25f / 0.9f);
                    _sprayLines.Put(at + new Vector3(0f, 0.9f, 0f), 0.25f, 0f, new Color(0.9f, 0.15f, 0.15f, blink), null, 0.9f / 0.25f);
                }
                _shadows.Put(at + new Vector3(0f, -0.2f, 0f), 0.9f * (1f - (0.5f * fall)), 0f, new Color(0f, 0f, 0f, 0.35f), null, 0.5f);
                // 남은 시간 고리(파랑이 줄어든다).
                _civilianRings.Put(at, 1.6f * life + 0.4f, 0f, new Color(0.4f, 0.8f, 1f, 0.35f * blink));
                Vector3 aim = tu.Aim.HasValue ? W(tu.Aim.Value) : at + new Vector3(1f, 0f, 0f);
                Vector3 dir = (aim - at).normalized;
                int tlv = _sim.Build.PowerOf(UpgradeId.Turret);
                float tsize = 1.1f * LevelScale(tlv) * (post ? 1.15f : 1f);
                // 포탑 모델: 하늘에서 떨어져 서고, 머리가 쏘는 곳을 본다. 쏠 때마다 살짝 눌렸다 돌아오고, 꺼지기 전엔 깜빡인다.
                GameObject turretModel = _turretModels.Get();
                if (turretModel != null)
                {
                    ItemModels.Place(turretModel, at, drop.y, Vector3.down, tsize * (1f - (0.08f * kick)));
                    ItemModels.Aim(turretModel, dir);
                    Models3D.Tint(turretModel, blink < 1f ? new Color(0.55f, 0.55f, 0.6f) : tlv >= Loadout.MaxLevel ? new Color(1f, 0.9f, 0.6f) : Color.white);
                }
                at += drop;
                if (tlv >= Loadout.MaxLevel) _auras.Put(at, 1.5f * tsize, 0f, new Color(1f, 0.85f, 0.35f, 0.6f * blink));
                // 쏘는 중이면 노즐에서 과녁까지 리본 물줄기가 이어진다(틱마다 그리던 직선 트레이서 대신).
                if (tu.Aim.HasValue && fall <= 0f)
                {
                    Vector3 nozzle = at + (dir * 0.8f) + new Vector3(0f, 0.3f, 0f);
                    SmallRibbon(nozzle, aim, 0.4f * LevelScale(tlv), tlv >= Loadout.MaxLevel ? GoldTint : TurretTint, 0.4f);
                }
                // 곁에서 타는 건물: 포물선 물방울로 적신다.
                foreach (Structure st in _sim.Structures)
                {
                    if (!st.IsBuilding || !st.Burning || !st.Within(tu.Pos, SurvivorSim.TurretRange)) continue;
                    Vector3 roof = W(st.Pos);
                    if (Random.value < 0.35f)
                    {
                        Vector3 v = (roof - at) * 1.6f;
                        v.y += 3.5f;
                        EmitSprite(Art.Get("Effects/water_drop"), at + new Vector3(0f, 0.3f, 0f), v, 0f, 0.55f, 0.3f, 0.2f,
                            new Color(0.7f, 0.93f, 1f, 1f), new Color(0.6f, 0.9f, 1f, 0.2f), 0f, false, 12f);
                    }
                    if (Random.value < 0.04f) Steam(roof, 1, 1f);
                }
                if (tu.Life < 0.1f) Steam(at, 2, 0.8f);
            }
        }

        /// <summary>
        /// 호스 물: 규칙은 0.1초마다 물방울을 하나씩 내지만, 새것부터 순서대로 이어 노즐에서 끝까지 한 줄기로 그린다.
        /// 조준을 돌리면 먼저 나간 물이 옛 방향으로 날아가 줄기가 휘어진다. 손을 떼거나 물이 벽에 막히면 거기서 끊긴다.
        /// </summary>
        private void DrawHoseStream()
        {
            _chain.Clear();
            foreach (Shot s in _sim.Shots)
            {
                if (s.Hose && !s.Dead) _chain.Add(s);
            }
            if (_chain.Count == 0) return;
            _chain.Sort((a, b) => a.Age.CompareTo(b.Age));

            _streamPts.Clear();
            if (_sim.Spraying && _chain[0].Age < 0.2f) _streamPts.Add(NozzleTip());
            // 판정상 물은 몸 중심에서 나가지만, 그림은 손에 든 노즐에서 나와 과녁 쪽 진짜 물길로 모인다.
            Vector3 fromHand = NozzleTip() - W(_sim.Player);
            Shot prev = null;
            foreach (Shot s in _chain)
            {
                Vector3 p = W(s.Pos) + (fromHand * Mathf.Clamp01(1f - (s.Age / s.Life)));
                bool gap = prev != null && (s.Age - prev.Age > SurvivorSim.HoseInterval * 2.5f || Vector3.Distance(p, W(prev.Pos)) > 4.5f);
                if (gap)
                {
                    DrawStream(_streamPts, prev);
                    _streamPts.Clear();
                }
                _streamPts.Add(p);
                prev = s;
            }
            DrawStream(_streamPts, prev);
        }

        /// <summary>
        /// 점들을 잇는 한 줄기(모양은 WaterRibbon): 비치는 겉물 + 결이 흐르는 몸통 + 빛 쪽 하이라이트 세 겹.
        /// 끝은 물덩어리로 부서지고, 줄기 옆으로 물방울이 튀며, 맨 끝에서 물보라가 인다.
        /// </summary>
        private void DrawStream(List<Vector3> pts, Shot last)
        {
            if (pts.Count < 2 || last == null) return;
            bool jet = last.Kind == ShotKind.Jet;
            float w = jet ? 1.5f : Mathf.Max(0.95f, last.Radius * 2.3f);
            // 쥐는 순간 가늘게 나가다 0.25초에 확 굵어진다(압력이 차오르는 느낌). 놓은 뒤 날아가는 물은 굵은 채로.
            if (last.Hose) w *= _sim.Spraying ? Mathf.Lerp(0.55f, 1.15f, Mathf.Clamp01(_sprayHeld / 0.25f)) : 1.15f;
            _ribbonIn.Clear();
            foreach (Vector3 p in pts) _ribbonIn.Add(new Vec2(p.x, p.y));
            // 방수포 제트는 고압이라 덜 출렁인다.
            WaterRibbon.Build(_ribbonIn, w, _time, jet ? 0.6f : 1f, _ribbon, _blobs);
            if (_ribbon.Count < 2) return;

            float lift = last.Hose ? HandHeight : 0f;
            _waterSheath.Put(_ribbon, 1.35f, 0f, new Color(0.5f, 0.78f, 1f, 0.35f), 5f, lift);
            _waterBody.Put(_ribbon, 1f, 0f, new Color(0.55f, 0.8f, 1f, jet ? 0.8f : 0.92f), 8f, lift);
            // 빛은 왼쪽 위에서 온다: 하이라이트를 진행 방향 왼쪽으로 치우친다.
            _waterShine.Put(_ribbon, 0.3f, 0.35f, new Color(0.9f, 0.97f, 1f, 0.55f), 14f, lift);
            if (!jet && _sim.Build.Level(UpgradeId.Hose) >= Loadout.MaxLevel)
            {
                // 물대포 MAX: 줄기 한가운데 금빛-흰 심지가 흐르고 끝에서 금빛 반짝이가 튄다.
                _waterShine.Put(_ribbon, 0.16f, 0f, new Color(1f, 0.88f, 0.45f, 0.75f), 22f, lift);
                WaterRibbon.Point tipPt = _ribbon[_ribbon.Count - 1];
                if (Random.value < 0.35f) Sparkle(new Vector3(tipPt.Pos.X, tipPt.Pos.Y, 0f), 1, new Color(1f, 0.9f, 0.5f));
            }
            // 끝 물덩어리: 작은 물 알갱이 + 빛 쪽 반짝임. 크게 그리면 풍선처럼 보인다.
            foreach (WaterRibbon.Blob b in _blobs)
            {
                var at = new Vector3(b.Pos.X, b.Pos.Y, 0f);
                _streamJoint.Put(at, b.Radius * 1.1f, 0f, new Color(0.6f, 0.86f, 1f, b.Alpha * 0.85f));
                _blobShine.Put(at + (new Vector3(-0.2f, 0.2f, 0f) * b.Radius), b.Radius * 0.35f, 0f, new Color(1f, 1f, 1f, b.Alpha * 0.6f));
            }
            ShedDrops(_ribbon);
            if (last.Hose) StreamGlints(_ribbon, lift);

            Vector3 end = pts[pts.Count - 1];
            Vector3 endDir = (end - pts[pts.Count - 2]).normalized;
            EmitSpray(end, endDir, jet ? 1.6f : Mathf.Max(1f, last.Radius / 0.3f * 0.75f));
            if (last.Hose && _sim.Spraying)
            {
                Rainbow(end, endDir, _ribbon[_ribbon.Count - 1].Along);
                GroundWet(end);
            }
        }

        /// <summary>줄기 옆구리에서 가끔 물방울이 떨어져 나와 바깥으로 튄다. 노즐 1.5칸 안은 아직 뭉쳐 있어 안 튄다.</summary>
        private void ShedDrops(List<WaterRibbon.Point> ribbon)
        {
            WaterRibbon.Point tip = ribbon[ribbon.Count - 1];
            if (tip.Along < 2f || Random.value > 0.18f) return;
            WaterRibbon.Point p = ribbon[Random.Range(0, ribbon.Count)];
            if (p.Along < 1.5f) return;
            float side = Random.value < 0.5f ? -1f : 1f;
            var n = new Vector3(p.Normal.X, p.Normal.Y, 0f) * side;
            Vector3 at = new Vector3(p.Pos.X, p.Pos.Y, 0f) + (n * p.Half);
            EmitFalling("Effects/water_drop", at, (n * Random.Range(1.5f, 3.5f)) + new Vector3(0f, 1.5f, 0f), 0.3f, Random.Range(0.14f, 0.24f), new Color(0.75f, 0.95f, 1f, 0.9f));
        }

        /// <summary>
        /// 쥐는 순간: 노즐에서 흰 분출 + 앞쪽 부채꼴 물방울 + 한 번 툭. 쥔 동안: 손에 물살을 버티는 잔떨림과 노즐 끝 작은 물보라.
        /// </summary>
        private void UpdateSprayFeel(float dt)
        {
            bool on = _sim.Spraying && _sim.Outcome == SOutcome.Playing && _sim.PendingChoices == null;
            if (!on)
            {
                _sprayHeld = 0f;
                return;
            }
            Vector3 tip = NozzleTip();
            Vector3 look = Look();
            if (_sprayHeld <= 0f)
            {
                Emit("Effects/glow", tip, Vector3.zero, 0f, 0.1f, 0.5f, 1.1f, new Color(1f, 1f, 1f, 0.9f), new Color(0.6f, 0.85f, 1f, 0f), 0f, true);
                float baseDeg = Mathf.Atan2(look.y, look.x);
                for (int i = 0; i < 8; i++)
                {
                    float a = baseDeg + Random.Range(-0.5f, 0.5f);
                    EmitFalling("Effects/water_drop", tip, (new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Random.Range(4f, 7f)) + new Vector3(0f, 1.5f, 0f), 0.3f, Random.Range(0.2f, 0.3f), new Color(0.8f, 0.96f, 1f, 0.95f));
                }
                _trauma = Mathf.Min(1f, _trauma + 0.12f);
            }
            _sprayHeld += dt;
            _trauma = Mathf.Max(_trauma, SprayTremor + (0.015f * _sim.Build.Level(UpgradeId.Hose)));
            if (Random.value < 0.25f)
            {
                float a = Mathf.Atan2(look.y, look.x) + Random.Range(-0.35f, 0.35f);
                Emit(Smokes[Random.Range(0, Smokes.Length)], tip, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Random.Range(1.5f, 2.5f), 3f, 0.25f, 0.2f, 0.6f,
                    new Color(0.8f, 0.92f, 1f, 0.35f), new Color(0.7f, 0.85f, 1f, 0f), Random.Range(-90f, 90f), true);
            }
        }

        /// <summary>물줄기 위를 노즐에서 끝으로 달리는 밝은 결 넷: 물이 달려가는 게 보인다.</summary>
        private void StreamGlints(List<WaterRibbon.Point> ribbon, float lift)
        {
            float span = ribbon[ribbon.Count - 1].Along;
            if (span < 1f) return;
            Vector3 up = Up(lift) - Up(0f);
            for (int k = 0; k < 4; k++)
            {
                float along = ((_time * 14f) + (k * span / 4f)) % span;
                int i = 1;
                while (i < ribbon.Count - 1 && ribbon[i].Along < along) i++;
                WaterRibbon.Point p = ribbon[i];
                var travel = new Vector3(p.Normal.Y, -p.Normal.X, 0f);
                float deg = (Mathf.Atan2(travel.y, travel.x) * Mathf.Rad2Deg) - 90f;
                float fade = Mathf.Clamp01(along / 1.2f) * Mathf.Clamp01((span - along) / 1.2f);
                _streamGlint.Put(new Vector3(p.Pos.X, p.Pos.Y, 0f) + up, p.Half * 0.42f, deg, new Color(1f, 1f, 1f, 0.75f * fade), null, 4.5f);
            }
        }

        /// <summary>낮 스테이지에서 줄기 길이 3칸 이상이면 끝 물안개 위에 옅은 무지개 띠(줄기 방향으로 굽는다).</summary>
        private void Rainbow(Vector3 end, Vector3 dir, float span)
        {
            if (_sim.Stage.Number == 5 || span < 3f) return;
            float deg = (Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg) - 90f + (Mathf.Sin(_time * 2.5f) * 6f);
            float a = 0.3f + (0.05f * Mathf.Sin(_time * 7f));
            _rainbow.Put(end - (dir * 0.3f), 2.6f, deg, new Color(1f, 1f, 1f, a));
        }

        /// <summary>줄기 끝이 땅이면(건물 위가 아니면) 젖은 물길과 물결 고리를 남긴다.</summary>
        private void GroundWet(Vector3 end)
        {
            if (_time < _wetAt) return;
            foreach (Structure st in _sim.Structures)
            {
                if (st.IsBuilding && !st.Collapsed && Mathf.Abs(end.x - st.Pos.X) < st.Half.X && Mathf.Abs(end.y - st.Pos.Y) < st.Half.Y) return;
            }
            _wetAt = _time + 0.12f;
            AddWet(end + new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(-0.15f, 0.15f), 0f), 0.9f, 2.5f, false);
            if (_time >= _rippleAt)
            {
                _rippleAt = _time + 0.35f;
                AddWet(end, 1.1f, 0.8f, true);
            }
        }

        /// <summary>방화복을 갈아입는 순간: 금빛 고리 + 반짝이 + 글자.</summary>
        private void SuitUp(Vector3 at)
        {
            var gold = new Color(1f, 0.85f, 0.35f);
            Shockwave(at, gold, 2.5f, 0.35f);
            Sparkle(at, 12, gold);
            SpawnText(at + new Vector3(0f, 1.1f, 0f), "방화복 강화!", gold, 1.4f);
        }

        /// <summary>마지막 발사 뒤 이만큼은 조준 방향을 유지한다(연사 사이에 몸이 돌아가지 않게).</summary>
        private const float AimHold = 0.8f;

        /// <summary>조준점: 마우스 자리(하니스에서는 겨눈 쪽 4칸)에 고리 + 점. 쏘는 동안 살짝 조여든다.</summary>
        private void DrawReticle(Vector3 at)
        {
            if (_sim.Outcome != SOutcome.Playing) return;
            Vector3 aim = new Vector3(_sim.Aim.X, _sim.Aim.Y, 0f);
            Vector3 spot = _hasMouse ? _mouseAt : at + (aim.sqrMagnitude > 0.0001f ? aim.normalized * 4f : Vector3.right * 4f);
            float squeeze = _sim.Spraying ? 0.8f + (0.05f * Mathf.Sin(_time * 30f)) : 1f;
            var white = new Color(1f, 1f, 1f, 0.8f);
            _reticle.Put(spot, 0.75f * squeeze, 0f, white);
            _reticle.Put(spot, 0.16f, 0f, white, BubbleSprite());
            if (_touch && _stick.RightOn && aim.sqrMagnitude > 0.0001f)
            {
                // 폰: 오른손을 누르는 동안 노즐에서 겨눈 쪽으로 옅은 점선(5칸)을 그린다.
                Vector3 tip = NozzleTip();
                Vector3 dir = aim.normalized;
                for (int i = 1; i <= 8; i++)
                {
                    float f = i / 8f;
                    _reticle.Put(tip + (dir * (5f * f)), 0.14f, 0f, new Color(1f, 1f, 1f, 0.45f * (1f - (0.6f * f))), BubbleSprite());
                }
            }
        }

        /// <summary>몸이 보는 방향: 방금 쐈으면 조준 방향, 아니면 이동 방향.</summary>
        private Vector3 Look()
        {
            if (_sim.Spraying && (_sim.Aim.X != 0f || _sim.Aim.Y != 0f)) return new Vector3(_sim.Aim.X, _sim.Aim.Y, 0f).normalized;
            return _aimAge < AimHold ? _aim : new Vector3(_sim.Facing.X, _sim.Facing.Y, 0f);
        }

        /// <summary>소방관이 걸어온 길을 따라 뒤로 끌리는 캔버스 호스. 서 있으면 등 뒤로 곧게 뻗는다.</summary>
        private void DrawHoseLine(Vector3 at, Vector3 look)
        {
            if (_trail.Count == 0 || Vector3.Distance(_trail[0], at) > 0.3f)
            {
                _trail.Insert(0, at);
                if (_trail.Count > 12) _trail.RemoveAt(_trail.Count - 1);
            }

            // 발밑에서 몸 뒤로 끌린다(손에서 발까지는 서 있는 몸이 가린다).
            var points = new List<Vector3>(14) { at };
            for (int i = 1; i < _trail.Count; i++) points.Add(_trail[i]);
            Vector3 back = -new Vector3(_sim.Facing.X, _sim.Facing.Y, 0f);
            while (points.Count < 12) points.Add(points[points.Count - 1] + (back * 0.3f));

            for (int i = 1; i < points.Count; i++)
            {
                Vector3 a = points[i - 1];
                Vector3 b = points[i];
                Vector3 d = b - a;
                float span = d.magnitude;
                if (span < 0.01f) continue;
                float deg = (Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg) - 90f;
                float fade = 1f - Mathf.Clamp01((i - 8f) / 4f);
                Vector3 mid = (a + b) * 0.5f;
                // 도트 2픽셀 빨간 소방 호스 + 어두운 테.
                _hoseTubeEdge.Put(mid, 0.16f, deg, new Color(0.25f, 0.06f, 0.05f, fade), null, (span + 0.06f) / 0.16f);
                _hoseTube.Put(mid, 0.1f, deg, new Color(0.85f, 0.2f, 0.15f, fade), null, (span + 0.06f) / 0.1f);
            }
        }

        /// <summary>물줄기 끝: 앞쪽 부채꼴로 퍼지는 물안개 + 가끔 튀는 물방울.</summary>
        private void EmitSpray(Vector3 at, Vector3 dir, float scale)
        {
            float baseAngle = Mathf.Atan2(dir.y, dir.x);
            float a = baseAngle + Random.Range(-0.6f, 0.6f);
            var v = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Random.Range(2f, 4f);
            Emit(Smokes[Random.Range(0, Smokes.Length)], at, v, 3f, 0.35f, 0.5f * scale, 1.6f * scale,
                new Color(0.55f, 0.8f, 1f, 0.45f), new Color(0.5f, 0.75f, 1f, 0f), Random.Range(-90f, 90f), true);
            if (Random.value < 0.45f)
            {
                float b = baseAngle + Random.Range(-1.2f, 1.2f);
                EmitFalling("Effects/water_drop", at, new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0f) * Random.Range(2f, 5f) + new Vector3(0f, 2f, 0f),
                    0.3f, Random.Range(0.2f, 0.32f), new Color(0.75f, 0.95f, 1f, 0.95f));
            }
        }

        /// <summary>쏘는 동안 몸을 조준에서 트는 각도(음수 = 시계 방향). 몸이 옆으로 서서 두 손이 호스 한 줄에 놓인다.</summary>
        private const float StanceTurn = -75f;

        /// <summary>소방관 그림(앞 = +x)에서 두 주먹 자리(칸). 왼손이 +y.</summary>
        private static readonly Vector2 LeftFist = new Vector2(0.34f, 0.39f);
        private static readonly Vector2 RightFist = new Vector2(0.34f, -0.37f);

        /// <summary>지금 몸을 튼 각도. 쥐면 옆으로 서고, 놓으면 앞을 본다.</summary>
        private float BodyTurn => StanceTurn * _stance;

        /// <summary>몸 그림의 주먹 자리를 월드로(몸 회전 = 조준 + BodyTurn).</summary>
        private Vector3 Fist(Vector2 local)
        {
            Vector3 look = Look();
            float rad = (Mathf.Atan2(look.y, look.x) * Mathf.Rad2Deg + BodyTurn) * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float n = Mathf.Sin(rad);
            Vector3 kick = look * (-0.1f * _recoil);
            return W(_sim.Player) + kick + new Vector3((local.x * c) - (local.y * n), (local.x * n) + (local.y * c), 0f);
        }

        /// <summary>
        /// 노즐을 쥔 손: 들고 다닐 때는 오른손, 쏠 때는 옆으로 선 몸의 앞손(왼손).
        /// 뒷손(오른손)은 호스를 받친다.
        /// </summary>
        private Vector3 Hand() => Vector3.Lerp(Fist(RightFist), Fist(LeftFist), _stance) + Up(HandHeight);

        /// <summary>서 있는 소방관이 노즐을 쥔 높이(칸). 물줄기는 여기서 나와 4칸에 걸쳐 땅으로 떨어진다.</summary>
        private const float HandHeight = 0.55f;

        /// <summary>물이 나오는 노즐 끝. 물줄기·총구 물보라가 모두 여기서 시작한다.</summary>
        private Vector3 NozzleTip() => Hand() + (Look() * 0.5f);

        /// <summary>
        /// 손에 쥔 소방 노즐(뒤 → 앞): 놋쇠 이음쇠, 빨간 몸통과 옆으로 삐죽한 검은 손잡이, 은색 목, 놋쇠 끝.
        /// 앞손 장갑이 몸통을 감싸고, 쏘는 동안은 뒷손 장갑이 호스를 받친다.
        /// </summary>
        private void DrawNozzle(Vector3 look, float lookDeg)
        {
            Vector3 hand = Hand();
            Vector3 side = new Vector3(look.y, -look.x, 0f);
            float deg = lookDeg - 90f;
            var glove = new Color(0.2f, 0.18f, 0.16f);
            _nozzle.Put(hand - (look * 0.18f), 0.2f, deg, new Color(0.8f, 0.6f, 0.22f), null, 0.45f);
            _nozzle.Put(hand + (look * 0.02f), 0.15f, deg, new Color(0.78f, 0.12f, 0.1f), null, 0.36f / 0.15f);
            _nozzle.Put(hand - (look * 0.06f) + (side * 0.12f), 0.07f, lookDeg, new Color(0.12f, 0.12f, 0.13f), null, 0.18f / 0.07f);
            _nozzle.Put(hand + (look * 0.26f), 0.11f, deg, new Color(0.78f, 0.8f, 0.84f), null, 0.14f / 0.11f);
            _nozzle.Put(hand + (look * 0.41f), 0.16f, deg, new Color(0.88f, 0.68f, 0.26f), null, 0.18f / 0.16f);
            _nozzle.Put(hand - (look * 0.02f), 0.18f, 0f, glove, DiscSprite());
            if (_stance > 0.01f) _nozzle.Put(Fist(RightFist), 0.18f, 0f, new Color(glove.r, glove.g, glove.b, _stance), DiscSprite());
        }

        /// <summary>사람 모델 키(칸).</summary>
        private const float PersonTall = 1.6f;

        /// <summary>마감 게이지가 꽉 차 보이는 남은 시간(초). 이보다 많이 남았으면 꽉 찬 초록.</summary>
        private const float DeadlineShown = 30f;

        /// <summary>
        /// 방화복 단계별 소방관 옷(Worker 모델 머티리얼 이름): 0 회청 안전모·파랑 작업복(어두운 바닥에서도 보이게) → 1 노란 안전모 → 2 빨간 안전모·황갈 방화복
        /// → 3 빨간 방화복 → 4 은색 방열복. 피부·얼굴은 그대로.
        /// </summary>
        private Color? SuitColor(string material)
        {
            int outfit = Mathf.Clamp(_sim.Build.Level(UpgradeId.Suit), 0, 4);
            Color hat = outfit == 0 ? new Color(0.36f, 0.4f, 0.48f) : outfit == 1 ? new Color(1f, 0.82f, 0.15f) : outfit < 4 ? new Color(0.9f, 0.18f, 0.12f) : new Color(0.88f, 0.9f, 0.93f);
            Color coat = outfit < 2 ? new Color(0.2f, 0.45f, 0.85f) : outfit == 2 ? new Color(0.82f, 0.66f, 0.32f) : outfit == 3 ? new Color(0.85f, 0.2f, 0.14f) : new Color(0.72f, 0.75f, 0.8f);
            Color stripe = outfit < 2 ? new Color(0.45f, 0.7f, 1f) : outfit == 2 ? new Color(0.85f, 1f, 0.3f) : outfit == 3 ? new Color(1f, 0.95f, 0.45f) : new Color(1f, 0.85f, 0.35f);
            if (material.StartsWith("Hat")) return hat;
            if (material.StartsWith("Vest")) return stripe;
            if (material.StartsWith("Shirt")) return coat;
            if (material.StartsWith("Pants")) return outfit == 4 ? new Color(0.62f, 0.65f, 0.7f) : new Color(0.22f, 0.22f, 0.28f);
            return null;
        }

        /// <summary>구조되는 시민(여자·할아버지·남자)을 번갈아.</summary>
        private static string CivilianModel(int k)
        {
            return CivilianModels[CivilianKind(k)];
        }

        /// <summary>시민 옷(위에서도 보이게 밝게): 여자 분홍, 할아버지 황갈·흰머리, 남자 초록.</summary>
        private static int CivilianKind(int k)
        {
            return ((k % 3) + 3) % 3;
        }

        private static System.Func<string, Color?> CivilianColor(int k)
        {
            int kind = CivilianKind(k);
            Color shirt = kind == 0 ? new Color(0.92f, 0.45f, 0.6f) : kind == 1 ? new Color(0.7f, 0.52f, 0.34f) : new Color(0.3f, 0.65f, 0.35f);
            Color hair = kind == 0 ? new Color(0.55f, 0.32f, 0.16f) : kind == 1 ? new Color(0.85f, 0.85f, 0.85f) : new Color(0.3f, 0.22f, 0.16f);
            return material =>
            {
                if (material.StartsWith("Shirt")) return shirt;
                if (material.StartsWith("Hair") || material.StartsWith("Hat")) return hair;
                if (material.StartsWith("Pants")) return new Color(0.3f, 0.32f, 0.5f);
                return null;
            };
        }

        /// <summary>대원 옷: 노란 안전모, 주황 작업복, 연노랑 반사띠.</summary>
        private static Color? PartnerColor(string material)
        {
            if (material.StartsWith("Hat")) return new Color(1f, 0.82f, 0.15f);
            if (material.StartsWith("Shirt")) return new Color(0.95f, 0.5f, 0.15f);
            if (material.StartsWith("Vest")) return new Color(0.95f, 0.95f, 0.6f);
            if (material.StartsWith("Pants")) return new Color(0.22f, 0.22f, 0.28f);
            return null;
        }

        private void DrawPlayer()
        {
            Vector3 at = W(_sim.Player);
            Vector3 look = Look();
            Vector3 kick = look * (-0.1f * _recoil);
            float lookDeg = Mathf.Atan2(look.y, look.x) * Mathf.Rad2Deg;
            _shadows.Put(at + new Vector3(0.05f, -0.1f, 0f), 1f, 0f, new Color(0f, 0f, 0f, 0.45f), null, 0.5f);
            // 3D 소방관: 조준 쪽을 보고(쏘는 동안은 옆으로 틀어 선다), 움직이면 달리고 서 있으면 숨 고른다.
            bool moving = (at - _lastPlayer).sqrMagnitude > 0.00001f;
            float bodyRad = (lookDeg + BodyTurn) * Mathf.Deg2Rad;
            Models3D.Pose(_player, at + kick, new Vector3(Mathf.Cos(bodyRad), Mathf.Sin(bodyRad), 0f));
            Models3D.Play(_player, moving ? "Run" : "Idle", 1f, _time);
            DrawNozzle(look, lookDeg);
            DrawHoseLine(at, look);
            DrawReticle(at);
            // 방화복 레벨만큼 옷을 갈아입는다: 파랑 → 노란 헬멧 → 빨간 헬멧 → 빨간 방화복 → 은색 방열복.
            int suit = _sim.Build.Level(UpgradeId.Suit);
            int outfit = Mathf.Min(suit, 4);
            if (outfit != _suitShown)
            {
                if (_suitShown >= 0 && outfit > _suitShown) SuitUp(at);
                _suitShown = outfit;
            }
            // 최대 레벨이면 은색 방열복이 금빛으로 일렁인다.
            Color baseColor = suit >= Loadout.MaxLevel ? Color.Lerp(Color.white, new Color(1f, 0.85f, 0.4f), 0.25f + (0.15f * Mathf.Sin(_time * 4f))) : Color.white;
            Models3D.Tint(_player, Color.Lerp(baseColor, new Color(1f, 0.35f, 0.3f), Mathf.Clamp01(_hurt * 2f)), SuitColor, outfit);

            // 머리 위 체력 바(건물 내구도 바와 같은 풀). 최대 체력이 크면 조금 길다. 30% 밑이면 깜빡인다.
            float hpRatio = Mathf.Clamp01(_sim.Hp / _sim.MaxHp);
            float hpW = 1.5f * _sim.MaxHp / SurvivorSim.BaseMaxHp;
            Vector3 hpBar = at + Up(PersonTall + 0.45f);
            _bars.PutRot(hpBar, Billboard, hpW + 0.08f, 0.22f, new Color(0f, 0f, 0f, 0.7f));
            bool lowHp = hpRatio < 0.3f && Mathf.Sin(_time * 10f) < 0f;
            _bars.PutRot(hpBar + new Vector3(-(hpW * (1f - hpRatio)) / 2f, 0f, 0f) + (Billboard * new Vector3(0f, 0f, -0.01f)), Billboard, Mathf.Max(0.01f, hpW * hpRatio), 0.14f,
                lowHp ? new Color(1f, 0.6f, 0.5f) : new Color(0.9f, 0.25f, 0.2f));
            _playerGlow.transform.localPosition = at;
            float r = 2f * _sim.Magnet * 0.5f;
            _playerGlow.transform.localScale = Vector3.one * Art.FitWidth(_playerGlow.sprite, r * 2f);
        }

        /// <summary>
        /// 보조템이 몸에 드러나게 한다: 탱크는 등 뒤 물빛, 방화복은 금빛 보호막, 장화는 발밑 물 튀김,
        /// 무전기는 흡수 반경에서 퍼지는 레이더 고리. 모두 레벨이 오를수록 진해진다.
        /// </summary>
        private void DrawGear(float dt)
        {
            Loadout build = _sim.Build;
            Vector3 at = W(_sim.Player);
            var facing = new Vector3(_sim.Facing.X, _sim.Facing.Y, 0f);
            Vector3 moved = at - _lastPlayer;
            _lastPlayer = at;
            if (_sim.Outcome != SOutcome.Playing) return;

            int tank = build.Level(UpgradeId.Tank);
            if (tank > 0)
            {
                float slosh = 1f + (0.08f * Mathf.Sin(_time * 6f));
                bool goldTank = tank >= Loadout.MaxLevel;
                _tank.Put(at - (facing * 0.35f), (0.8f + (0.15f * tank)) * slosh, 0f, goldTank ? new Color(1f, 0.78f, 0.3f, 0.6f) : new Color(0.3f, 0.65f, 1f, 0.35f + (0.07f * tank)));
                if (goldTank && Random.value < 0.08f)
                {
                    // 펌프 MAX: 등 뒤 탱크에서 김이 칙칙 뿜어 나온다.
                    Emit(Smokes[Random.Range(0, Smokes.Length)], at - (facing * 0.5f), (-facing * 1.2f) + new Vector3(0f, 1.2f, 0f), 1f, 0.6f, 0.3f, 0.9f,
                        new Color(1f, 1f, 1f, 0.5f), new Color(1f, 1f, 1f, 0f), Random.Range(-90f, 90f));
                }
            }

            int suit = build.Level(UpgradeId.Suit);
            if (suit > 0)
            {
                float alpha = (0.15f + (0.1f * suit)) * (0.8f + (0.2f * Mathf.Sin(_time * 3f)));
                _auras.Put(at, 0.7f * 2f / 0.85f, 0f, new Color(1f, 0.8f, 0.35f, alpha));
                if (suit >= Loadout.MaxLevel)
                {
                    // 방화복 MAX: 금빛 갑옷 오라가 두 겹, 바깥 겹은 숨 쉬듯 커졌다 작아진다.
                    float breathe = 1f + (0.12f * Mathf.Sin(_time * 4f));
                    _auras.Put(at, 2.4f * breathe, 0f, new Color(1f, 0.88f, 0.45f, 0.4f));
                }
                _regenClock -= dt;
                if (_sim.Hp < _sim.MaxHp && _regenClock <= 0f)
                {
                    _regenClock = 1f;
                    Sparkle(at, 2 + suit, new Color(0.45f, 1f, 0.5f));
                }
            }

            int boots = build.Level(UpgradeId.Boots);
            if (boots > 0 && dt > 0f && moved.magnitude / dt > 0.5f)
            {
                _stepClock -= dt;
                if (_stepClock <= 0f)
                {
                    _stepClock = 0.28f / (1f + (0.15f * boots));
                    Vector3 back = -moved.normalized;
                    for (int i = 0; i < 1 + (boots / 2); i++)
                    {
                        var v = (back * Random.Range(1f, 2.5f)) + new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(-1.2f, 1.2f), 0f);
                        Emit("Effects/water_drop", at + (back * 0.3f), v, 5f, 0.3f, 0.26f, 0.05f, new Color(0.7f, 0.93f, 1f, 0.9f), new Color(0.6f, 0.9f, 1f, 0f), 0f);
                    }
                    if (boots >= Loadout.MaxLevel)
                    {
                        // 장화 MAX: 발자국마다 물결 고리가 남는다.
                        Shockwave(at + (back * 0.3f), new Color(0.6f, 0.9f, 1f, 0.55f), 1.6f, 0.45f);
                    }
                    if (boots >= 3)
                    {
                        float deg = (Mathf.Atan2(back.y, back.x) * Mathf.Rad2Deg) - 90f;
                        Vector3 sideways = new Vector3(-back.y, back.x, 0f) * Random.Range(-0.3f, 0.3f);
                        EmitSprite(BeamSprite(), at + (back * 0.9f) + sideways, back * 2f, 2f, 0.18f, 0.18f, 0.08f,
                            new Color(0.75f, 0.92f, 1f, 0.6f), new Color(0.75f, 0.92f, 1f, 0f), 0f, true, 0f, 6f, deg);
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // 효과
        // ------------------------------------------------------------------

        /// <summary>불이 꺼지는 순간: 흰 번쩍 + 하얀 수증기 + 불똥. 큰 불은 충격파와 짧은 멈춤까지.</summary>
        private void DeathBurst(Vector3 at, EnemyKind kind, bool crowded)
        {
            bool big = kind == EnemyKind.Blaze;
            Emit("Effects/glow", at, Vector3.zero, 0f, 0.1f, big ? 2.4f : 1.3f, big ? 3f : 1.7f, new Color(1f, 1f, 1f, 0.9f), new Color(0.7f, 0.9f, 1f, 0f), 0f, true);
            int sparks = big ? 13 : crowded ? 4 : 8;
            for (int i = 0; i < sparks; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                float speed = Random.Range(3f, big ? 10f : 7f);
                Emit(Sparks[Random.Range(0, Sparks.Length)], at, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * speed, 5f, Random.Range(0.25f, 0.45f),
                    Random.Range(0.45f, 0.75f), 0.05f, new Color(1f, 0.8f, 0.3f), new Color(1f, 0.25f, 0.02f, 0f), Random.Range(-600f, 600f), true);
            }
            Steam(at, big ? 5 : crowded ? 1 : 2, big ? 1.4f : 1f);
            Splash(at, big ? 8 : crowded ? 2 : 4, big ? 0.9f : 0.5f);
            Scorch(at, big ? 1.6f : 0.9f);
            if (big && kind == EnemyKind.Blaze)
            {
                Shockwave(at, new Color(0.6f, 0.9f, 1f, 0.9f), 3.5f, 0.3f);
                _trauma = Mathf.Min(1f, _trauma + 0.06f);
                HitStop(0.03f);
            }
            if (kind == EnemyKind.Popper)
            {
                // 폭죽은 펑 터진다: 빨간 파편과 작은 충격파(불씨 둘이 튀는 건 시뮬).
                Burst(at, 12, new Color(1f, 0.35f, 0.25f), 6f);
                Shockwave(at, new Color(1f, 0.5f, 0.3f, 0.9f), 2.2f, 0.25f);
                _trauma = Mathf.Min(1f, _trauma + 0.04f);
            }
            if (kind == EnemyKind.SkyLantern)
            {
                // 풍등은 종이가 타며 조각이 떨어진다.
                for (int k = 0; k < 6; k++)
                {
                    EmitFalling("Effects/spark_0" + (1 + (k % 4)), at + Up(2.5f), new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(-0.5f, 0.5f), 0f), Random.Range(0.8f, 1.4f), 0.3f, new Color(1f, 0.7f, 0.3f, 0.9f));
                }
            }
            if (kind == EnemyKind.Crab)
            {
                // 게는 등딱지가 쪼개진다: 빨간 조각.
                Burst(at, 10, new Color(1f, 0.3f, 0.2f), 5f);
            }
        }

        /// <summary>물이 불에 닿으면 하얀 김이 뭉게뭉게 올라간다.</summary>
        private void Steam(Vector3 at, int count, float scale)
        {
            for (int i = 0; i < count; i++)
            {
                var jitter = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.2f, 0.2f), 0f) * scale;
                var up = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(1.2f, 2.2f), 0f);
                Emit(Smokes[Random.Range(0, Smokes.Length)], at + jitter, up, 1.2f, Random.Range(0.6f, 0.9f),
                    0.8f * scale, 2.8f * scale, new Color(0.95f, 0.97f, 1f, 0.75f), new Color(0.85f, 0.9f, 0.95f, 0f), Random.Range(-120f, 120f));
            }
        }

        /// <summary>맞았는데 안 꺼졌을 때: 쏜 방향으로 튀는 물보라 + 흰 번쩍 + 작은 김.</summary>
        private void HitSplash(Vector3 at, Vector3 away)
        {
            float baseAngle = Mathf.Atan2(away.y, away.x);
            for (int i = 0; i < 5; i++)
            {
                float a = baseAngle + Random.Range(-0.9f, 0.9f);
                Emit("Effects/water_drop", at, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Random.Range(4f, 8f), 7f, Random.Range(0.18f, 0.3f),
                    Random.Range(0.28f, 0.42f), 0.05f, new Color(0.75f, 0.95f, 1f, 1f), new Color(0.6f, 0.9f, 1f, 0f), 0f);
            }
            Emit("Effects/glow", at, Vector3.zero, 0f, 0.07f, 0.7f, 1f, new Color(0.85f, 0.95f, 1f, 0.8f), new Color(0.6f, 0.9f, 1f, 0f), 0f, true);
            if (Random.value < 0.35f) Steam(at, 1, 0.55f);
        }

        /// <summary>헬기는 땅(그림자)보다 이만큼 위에 떠 있다.</summary>
        private static readonly Vector3 HeliLift = new Vector3(0f, 2.6f, 0f);

        /// <summary>소방 헬기: 화면 밖에서 목표로 날아오며 땅에 그림자를 끌고, 다 오면 물을 쏟아붓는다.</summary>
        private void DrawHeli(Shot s)
        {
            float t = Mathf.Clamp01(s.Age / s.Life);
            Vector3 from = W(s.From);
            Vector3 to = W(s.Target);
            // 빨리 날아와 목표 위에서 멈칫한다.
            Vector3 ground = Vector3.Lerp(from, to, 1f - ((1f - t) * (1f - t)));
            Vector3 dir = (to - from).normalized;
            DrawHeliAt(ground, dir, 1f);
            // 목표 표시: 금색 큰 고리가 조여 들고, 그 안에서 두 번째 고리가 반대로 돈다. 헬기 밑으론 탐조등이 바닥을 비춘다.
            float ring = Mathf.Lerp(SurvivorSim.HeliRadius * 4.4f, SurvivorSim.HeliRadius * 2.2f, t);
            _reticle.Put(to, ring, _time * 90f, new Color(1f, 0.85f, 0.35f, 0.45f + (0.45f * t)));
            _reticle.Put(to, ring * 0.72f, -_time * 140f, new Color(0.6f, 0.9f, 1f, 0.3f + (0.5f * t)));
            _groundGlow.Put(ground, 7f, 0f, new Color(1f, 0.98f, 0.9f, 0.3f));
            // 하강풍: 헬기 밑 바닥에서 연기가 바깥으로 밀린다.
            for (int i = 0; i < 6; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector3 o = new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.5f, 0f);
                Emit("Effects/smoke_02", ground + (o * 1.2f), o * Random.Range(4f, 7f), 3f, 0.45f, 0.5f, 1.1f, new Color(0.9f, 0.9f, 0.9f, 0.25f), new Color(0.9f, 0.9f, 0.9f, 0f), Random.Range(-2f, 2f));
            }
            if (t > 0.6f)
            {
                // 쏟기 직전: 헬기 배에서 물이 쏟아진다.
                for (int i = 0; i < 10; i++)
                {
                    Vector3 belly = ground + HeliLift + new Vector3(Random.Range(-0.9f, 0.9f), -0.3f, 0f);
                    Emit("Effects/water_drop", belly, new Vector3(Random.Range(-1.5f, 1.5f), -Random.Range(7f, 11f), 0f), 0f, 0.3f,
                        0.6f, 0.3f, new Color(0.75f, 0.93f, 1f, 0.95f), new Color(0.6f, 0.9f, 1f, 0.2f), 0f);
                }
            }
        }

        private void DrawHeliExit()
        {
            if (_heliExitAge > 1.4f) return;
            _heliExitAge += _frameDt;
            Vector3 ground = _heliExitFrom - HeliLift + (_heliExitDir * (_heliExitAge * _heliExitAge * 14f));
            DrawHeliAt(ground, _heliExitDir, 1f - Mathf.Clamp01((_heliExitAge - 1f) / 0.4f));
        }

        private void DrawHeliAt(Vector3 ground, Vector3 dir, float alpha)
        {
            float deg = (Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg) - 90f;
            _heliShadow.Put(ground + new Vector3(0.3f, -0.2f, 0f), 3.2f, deg, new Color(0f, 0f, 0f, 0.35f * alpha));
            // 헬기 모델: 2.2칸 떠서 나는 쪽을 보고 주·꼬리 날개가 돈다. 빠져나갈 땐 작아지며 사라진다.
            if (alpha < 0.05f) return;
            GameObject heli = _heliModels.Get();
            if (heli == null) return;
            ItemModels.Place(heli, ground, 2.2f, dir, 1.05f * Mathf.Lerp(0.6f, 1f, alpha));
            ItemModels.Spin(heli, "Rotor", 1400f, _time);
            ItemModels.Spin(heli, "TailRotor", 2400f, _time);
        }

        /// <summary>구조대원들: 노란 헬멧 소방관이 움직이는 쪽을 보고 달린다. 첫 대원 머리 위에 "구조대" 이름표.</summary>
        private void DrawPartner()
        {
            bool has = _sim.Partners.Count > 0;
            if (_partnerTag != null) _partnerTag.gameObject.SetActive(has);
            if (_partnerTag != null) _partnerTag.text = _sim.Build.Level(UpgradeId.Squad) > 0 ? "구조 분대" : "구조대";
            for (int i = 0; i < _sim.Partners.Count; i++)
            {
                Vector3 at = W(_sim.Partners[i]);
                Vector3 moved = at - _partnerLast[i];
                _partnerLast[i] = at;
                if (moved.sqrMagnitude > 0.0001f && moved.sqrMagnitude < 4f) _partnerDeg[i] = Mathf.Atan2(moved.y, moved.x) * Mathf.Rad2Deg;
                float bob = moved.sqrMagnitude > 0.0001f ? 0.05f * Mathf.Abs(Mathf.Sin((_time * 14f) + i)) : 0f;
                _shadows.Put(at + new Vector3(0.05f, -0.15f, 0f), 0.9f, 0f, new Color(0f, 0f, 0f, 0.4f), null, 0.55f);
                bool squad = _sim.Build.Level(UpgradeId.Squad) > 0;
                // 곁(3칸) 불난 건물에 물줄기를 뿜는다.
                Structure near = null;
                foreach (Structure st in _sim.Structures)
                {
                    if (st.IsBuilding && st.Burning && st.DistanceTo(_sim.Partners[i]) <= 3f) near = st;
                }
                if (near != null)
                {
                    Vector3 roof = W(near.Pos);
                    Vector3 hand = at + ((roof - at).normalized * 0.4f);
                    Vector3 wetAt = Vector3.Lerp(hand, roof, 0.8f);
                    SmallRibbon(hand, wetAt, squad ? 0.5f : 0.36f, squad ? GoldTint : PartnerTint, 0.35f);
                    if (Random.value < 0.4f) Splash(wetAt, 1, 0.3f);
                    if (Random.value < 0.3f) Splash(hand, 1, 0.1f);
                    if (Random.value < 0.05f) Steam(wetAt + new Vector3(0f, 0.3f, 0f), 1, 0.9f);
                }
                if (squad) _auras.Put(at, 1.3f, 0f, new Color(1f, 0.85f, 0.35f, 0.5f));
                float pd = _partnerDeg[i] * Mathf.Deg2Rad;
                bool walking = moved.sqrMagnitude > 0.0001f;
                GameObject mate = _people.Get("People/Worker_Female");
                if (mate != null)
                {
                    // 주황 옷·노란 안전모 대원. 구조 분대면 금빛이 돈다.
                    Models3D.Pose(mate, at, new Vector3(Mathf.Cos(pd), Mathf.Sin(pd), 0f));
                    Models3D.Play(mate, walking ? "Run" : "Idle", 1f, _time + i);
                    Models3D.Tint(mate, squad ? new Color(1f, 0.92f, 0.6f) : Color.white, PartnerColor, 1);
                }
                if (i == 0 && _partnerTag != null) _partnerTag.transform.localPosition = at + Up(PersonTall + 0.25f);
            }
        }

        /// <summary>위에서 본 소방 헬기: 빨간 동체, 파란 유리 조종석, 흰 띠, 꼬리. 위쪽이 앞.</summary>
        /// <summary>금빛 보물상자: 갈색 나무 몸통, 금테, 둥근 뚜껑, 가운데 자물쇠.</summary>
        private static Sprite ChestSprite()
        {
            if (_chestSprite != null) return _chestSprite;
            const int n = 64;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            var wood = new Color32(150, 90, 40, 255);
            var woodDark = new Color32(95, 55, 25, 255);
            var gold = new Color32(250, 200, 60, 255);
            var goldDark = new Color32(190, 130, 20, 255);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n) - 0.5f;
                    float v = ((y + 0.5f) / n) - 0.5f;
                    Color32 c = new Color32(0, 0, 0, 0);
                    bool body = Mathf.Abs(u) < 0.42f && v > -0.32f && v < 0.06f;
                    // 뚜껑: 위가 둥근 반원.
                    float lu = u / 0.42f;
                    float lv = (v - 0.06f) / 0.22f;
                    bool lid = v >= 0.06f && (lu * lu) + (lv * lv) < 1f;
                    if (body) c = wood;
                    if (lid) c = woodDark;
                    bool rim = (body || lid) && (Mathf.Abs(u) > 0.36f || Mathf.Abs(v - 0.06f) < 0.035f || v < -0.27f);
                    if (rim) c = gold;
                    bool band = (body || lid) && Mathf.Abs(Mathf.Abs(u) - 0.2f) < 0.035f;
                    if (band) c = goldDark;
                    if (Mathf.Abs(u) < 0.07f && v > -0.06f && v < 0.1f) c = gold;
                    pixels[(y * n) + x] = c;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _chestSprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return _chestSprite;
        }

        /// <summary>빨간 공구상자(위에서 비스듬히 본 모습): 몸통, 짙은 뚜껑 띠, 은색 잠금쇠, 검은 손잡이.</summary>
        private static Sprite ToolboxSprite()
        {
            if (_toolboxSprite != null) return _toolboxSprite;
            const int n = 64;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            var red = new Color32(214, 52, 40, 255);
            var dark = new Color32(120, 24, 20, 255);
            var lid = new Color32(170, 36, 30, 255);
            var metal = new Color32(205, 210, 215, 255);
            var handle = new Color32(40, 40, 45, 255);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n) - 0.5f;
                    float v = ((y + 0.5f) / n) - 0.5f;
                    Color32 c = new Color32(0, 0, 0, 0);
                    bool body = Mathf.Abs(u) < 0.42f && v > -0.3f && v < 0.14f;
                    bool edge = body && (Mathf.Abs(u) > 0.38f || v < -0.26f || v > 0.1f);
                    // 손잡이: 뚜껑 위 아치.
                    float ax = u / 0.2f;
                    float ay = (v - 0.14f) / 0.18f;
                    float arch = (ax * ax) + (ay * ay);
                    if (v > 0.14f && arch < 1f && arch > 0.45f) c = handle;
                    if (body) c = edge ? dark : red;
                    if (body && !edge && v > 0.0f) c = lid;
                    if (Mathf.Abs(u) < 0.08f && v > -0.06f && v < 0.06f) c = metal;
                    pixels[(y * n) + x] = c;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _toolboxSprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return _toolboxSprite;
        }

        /// <summary>구급상자: 흰 상자, 어두운 테두리, 가운데 빨간 십자, 위에 손잡이 아치.</summary>
        private static Sprite KitSprite()
        {
            if (_kitSprite != null) return _kitSprite;
            const int n = 64;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            var white = new Color32(245, 245, 240, 255);
            var rim = new Color32(90, 90, 95, 255);
            var cross = new Color32(220, 40, 40, 255);
            var handle = new Color32(60, 60, 65, 255);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n) - 0.5f;
                    float v = ((y + 0.5f) / n) - 0.5f;
                    Color32 c = new Color32(0, 0, 0, 0);
                    bool body = Mathf.Abs(u) < 0.4f && v > -0.32f && v < 0.16f;
                    bool edge = body && (Mathf.Abs(u) > 0.36f || v < -0.28f || v > 0.12f);
                    float ax = u / 0.18f;
                    float ay = (v - 0.16f) / 0.16f;
                    float arch = (ax * ax) + (ay * ay);
                    if (v > 0.16f && arch < 1f && arch > 0.5f) c = handle;
                    if (body) c = edge ? rim : white;
                    bool plus = (Mathf.Abs(u) < 0.06f && Mathf.Abs(v + 0.08f) < 0.17f) || (Mathf.Abs(v + 0.08f) < 0.06f && Mathf.Abs(u) < 0.17f);
                    if (body && !edge && plus) c = cross;
                    pixels[(y * n) + x] = c;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _kitSprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return _kitSprite;
        }

        /// <summary>재 박쥐: 머리가 +x, 날개가 위아래(y)로 펼쳐진다. 세로로 눌러 퍼덕임을 낸다.</summary>
        private static Sprite BatSprite()
        {
            if (_batSprite != null) return _batSprite;
            const int n = 64;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            var wing = new Color32(255, 255, 255, 255);
            var body = new Color32(200, 200, 200, 255);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n) - 0.5f;
                    float v = ((y + 0.5f) / n) - 0.5f;
                    Color32 c = new Color32(0, 0, 0, 0);
                    // 날개: 몸에서 위아래로 뻗고 끝이 뒤로 휜 삼각형, 가장자리는 톱니.
                    float span = Mathf.Abs(v);
                    float lead = 0.12f - (span * 0.25f);
                    float trail = -0.1f - (span * 0.35f) + (0.05f * Mathf.Abs(Mathf.Sin(span * 30f)));
                    if (span < 0.46f && u < lead && u > trail) c = wing;
                    if (((u * u) / (0.14f * 0.14f)) + ((v * v) / (0.08f * 0.08f)) <= 1f) c = body;
                    pixels[(y * n) + x] = c;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _batSprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return _batSprite;
        }

        private static Sprite _gullSprite;
        private static Sprite _crabSprite;
        private static Sprite _squirrelSprite;

        /// <summary>절차 스프라이트 공통: 64px, (u,v)∈[-0.5,0.5]로 픽셀을 칠한다. 앞은 +u(BatSprite와 같다), 날개·다리는 v 방향.</summary>
        private static Sprite PaintSprite(System.Func<float, float, Color32> paint)
        {
            const int n = 64;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    pixels[(y * n) + x] = paint(((x + 0.5f) / n) - 0.5f, ((y + 0.5f) / n) - 0.5f);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        private static bool InEllipse(float u, float v, float cu, float cv, float ru, float rv)
        {
            float du = (u - cu) / ru;
            float dv = (v - cv) / rv;
            return (du * du) + (dv * dv) <= 1f;
        }

        /// <summary>불 갈매기(맵 특색 몹): 흰 몸, 곧게 뻗은 가늘고 긴 날개(끝은 검정), 주황 부리. 박쥐 재탕이 아니라 갈매기로 읽힌다.</summary>
        private static Sprite GullSprite()
        {
            if (_gullSprite != null) return _gullSprite;
            _gullSprite = PaintSprite((u, v) =>
            {
                var none = new Color32(0, 0, 0, 0);
                float span = Mathf.Abs(v);
                // 날개: 몸에서 위아래로 길게, 바깥으로 갈수록 가늘고 살짝 뒤로 젖는다.
                float lead = 0.11f - (span * 0.12f) + (0.05f * span * span);
                float trail = -0.02f - (span * 0.1f);
                if (span < 0.48f && u < lead && u > trail) return span > 0.36f ? new Color32(45, 45, 50, 255) : new Color32(245, 245, 250, 255);
                if (InEllipse(u, v, 0.02f, 0f, 0.2f, 0.07f)) return new Color32(240, 240, 245, 255);
                if (InEllipse(u, v, 0.21f, 0f, 0.07f, 0.06f)) return new Color32(240, 240, 245, 255);
                if (u > 0.26f && u < 0.35f && Mathf.Abs(v) < 0.03f * (0.35f - u) / 0.09f + 0.005f) return new Color32(255, 150, 40, 255);
                if (InEllipse(u, v, 0.22f, 0.025f, 0.015f, 0.015f)) return new Color32(20, 20, 20, 255);
                return none;
            });
            return _gullSprite;
        }

        /// <summary>불 게(맵 특색 몹): 넓적한 빨간 몸, 앞으로 벌린 집게 둘, 양옆 다리 셋씩, 눈 둘.</summary>
        private static Sprite CrabSprite()
        {
            if (_crabSprite != null) return _crabSprite;
            _crabSprite = PaintSprite((u, v) =>
            {
                var none = new Color32(0, 0, 0, 0);
                var shell = new Color32(230, 60, 40, 255);
                var dark = new Color32(150, 30, 20, 255);
                float av = Mathf.Abs(v);
                // 다리 셋: 몸 옆에서 바깥으로 비스듬히.
                for (int k = -1; k <= 1; k++)
                {
                    float lu = (k * 0.12f) - 0.05f;
                    float t = (av - 0.12f) / 0.22f;
                    if (t >= 0f && t <= 1f && Mathf.Abs(u - (lu - (t * 0.1f * (k + 2)))) < 0.035f) return dark;
                }
                // 집게: 앞(+u) 양옆의 굵은 원과 벌어진 틈.
                if (InEllipse(u, v, 0.27f, 0.2f, 0.1f, 0.085f) || InEllipse(u, v, 0.27f, -0.2f, 0.1f, 0.085f))
                {
                    if (u > 0.3f && Mathf.Abs(av - 0.2f) < 0.025f) return none;
                    return shell;
                }
                if (Mathf.Abs(u - 0.17f) < 0.04f && av > 0.1f && av < 0.2f) return dark;
                if (InEllipse(u, v, 0f, 0f, 0.24f, 0.16f)) return InEllipse(u, v, -0.02f, 0f, 0.17f, 0.1f) ? new Color32(245, 90, 60, 255) : shell;
                if (InEllipse(u, v, 0.2f, 0.06f, 0.025f, 0.025f) || InEllipse(u, v, 0.2f, -0.06f, 0.025f, 0.025f)) return new Color32(20, 20, 20, 255);
                return none;
            });
            return _crabSprite;
        }

        /// <summary>불다람쥐: 작은 몸과 머리, 뒤로 굵게 말려 올라가는 꼬리(흰·회색: 뷰가 주황으로 물들인다).</summary>
        private static Sprite SquirrelSprite()
        {
            if (_squirrelSprite != null) return _squirrelSprite;
            _squirrelSprite = PaintSprite((u, v) =>
            {
                var none = new Color32(0, 0, 0, 0);
                var fur = new Color32(255, 255, 255, 255);
                var belly = new Color32(210, 210, 210, 255);
                // 꼬리: (-0.2, 0.08) 둘레 반지름 0.2 호, 두께 0.11, 뒤에서 위로 말린다.
                float tu = u + 0.2f;
                float tv = v - 0.08f;
                float r = Mathf.Sqrt((tu * tu) + (tv * tv));
                float ang = Mathf.Atan2(tv, tu) * Mathf.Rad2Deg;
                if (Mathf.Abs(r - 0.2f) < 0.06f && ang > -100f && ang < 110f) return fur;
                if (InEllipse(u, v, 0.03f, -0.02f, 0.15f, 0.09f)) return v < -0.03f ? belly : fur;
                if (InEllipse(u, v, 0.2f, 0.02f, 0.08f, 0.07f)) return fur;
                if (InEllipse(u, v, 0.19f, 0.09f, 0.03f, 0.04f)) return fur;
                if (InEllipse(u, v, 0.24f, 0.03f, 0.018f, 0.018f)) return new Color32(20, 20, 20, 255);
                return none;
            });
            return _squirrelSprite;
        }

        private static Sprite HeliSprite()
        {
            if (_heliSprite != null) return _heliSprite;
            const int n = 96;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            var red = new Color32(214, 48, 40, 255);
            var dark = new Color32(120, 22, 20, 255);
            var glass = new Color32(120, 200, 245, 255);
            var white = new Color32(245, 245, 245, 255);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n) - 0.5f;
                    float v = ((y + 0.5f) / n) - 0.5f;
                    Color32 c = new Color32(0, 0, 0, 0);
                    // 동체: 위쪽(앞)이 둥근 타원.
                    float body = ((u * u) / (0.16f * 0.16f)) + (((v - 0.12f) * (v - 0.12f)) / (0.28f * 0.28f));
                    // 꼬리 막대와 꼬리 날개.
                    bool boom = Mathf.Abs(u) < 0.035f && v < -0.05f && v > -0.46f;
                    bool fin = Mathf.Abs(u) < 0.13f && v < -0.38f && v > -0.45f;
                    if (boom || fin) c = dark;
                    if (body <= 1f)
                    {
                        c = body > 0.8f ? dark : red;
                        if (v > 0.22f && body < 0.8f) c = glass;
                        if (Mathf.Abs(v - 0.02f) < 0.03f && body < 0.9f) c = white;
                    }
                    pixels[(y * n) + x] = c;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _heliSprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return _heliSprite;
        }

        /// <summary>포탑이 하늘에서 떨어져 서는 데 걸리는 초.</summary>
        private const float TurretDropTime = 0.2f;

        /// <summary>레벨이 보이게: 탄환 굵기·번쩍임 크기 배율.</summary>
        private static float LevelScale(int level)
        {
            return 1f + (0.12f * Mathf.Max(0, level - 1));
        }

        /// <summary>무기마다 다른 적중: 어디서 날아왔는지(탄환·찌릿)와 맞은 자리(번쩍·고리).</summary>
        private void WeaponHit(Hit h, Vector3 at)
        {
            int slot = (int)h.Source;
            if (slot >= _impacts.Length || _impacts[slot] >= MaxImpactsPerSource) return;
            Vector3 from = W(h.From);
            Loadout b = _sim.Build;
            switch (h.Source)
            {
                case HitSource.Partner:
                {
                    int lv = b.PowerOf(UpgradeId.Partner);
                    bool squad = b.Level(UpgradeId.Squad) > 0;
                    Color c = squad ? GoldTint : PartnerTint;
                    // 대원의 물줄기는 DrawPartner가 리본으로 그린다. 여기선 맞은 자리만.
                    Impact(at, c, 0.8f * (squad ? 1.3f : 1f), squad ? Loadout.MaxLevel : lv);
                    break;
                }
                case HitSource.Turret:
                {
                    int lv = b.PowerOf(UpgradeId.Turret);
                    Vector3 dir = (at - from).normalized;
                    // 포탑의 물줄기는 DrawTurrets가 과녁까지 리본으로 잇는다. 여기선 노즐 번쩍임과 반동만.
                    Emit("Effects/glow", from + (dir * 0.9f), Vector3.zero, 0f, 0.06f, 0.5f, 0.8f, new Color(1f, 1f, 1f, 0.8f), new Color(TurretTint.r, TurretTint.g, TurretTint.b, 0f), 0f, true);
                    for (int k = 0; k < _sim.Turrets.Count && k < _turretKick.Length; k++)
                    {
                        if (_sim.Turrets[k].Pos.DistanceTo(h.From) < 0.1f) _turretKick[k] = 1f;
                    }
                    Impact(at, TurretTint, 0.9f, lv);
                    break;
                }
                case HitSource.Hose:
                {
                    // 물대포: 쏜 방향으로 튀는 물보라 부채꼴 + 가끔 작은 김. 호스 적중은 이제까지 아무 연출이 없었다.
                    Vector3 dir = (at - from).sqrMagnitude > 0.0001f ? (at - from).normalized : Vector3.right;
                    float baseDeg = Mathf.Atan2(dir.y, dir.x);
                    int lv = b.PowerOf(UpgradeId.Hose);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = baseDeg + Random.Range(-0.6f, 0.6f);
                        Emit("Effects/water_drop", at, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Random.Range(4f, 7f), 7f, Random.Range(0.18f, 0.28f),
                            0.22f * LevelScale(lv), 0.05f, HoseTint, new Color(HoseTint.r, HoseTint.g, HoseTint.b, 0f), 0f);
                    }
                    if (Random.value < 0.3f) Steam(at, 1, 0.5f);
                    break;
                }
                case HitSource.Drone:
                {
                    int lv = b.PowerOf(UpgradeId.Drone);
                    bool gold = lv >= Loadout.MaxLevel || b.Level(UpgradeId.RescueDrone) > 0;
                    Color c = gold ? GoldTint : DroneTint;
                    // 찌릿: 드론에서 짧은 번개 물줄기 + 별 모양 물방울.
                    AddTracer(from, at, 0.14f, new Color(0.9f, 1f, 1f, 1f), 0.08f);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = (i * Mathf.PI / 3f) + Random.Range(-0.2f, 0.2f);
                        EmitSprite(BeamSprite(), at, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 6f, 8f, 0.14f, 0.25f, 0.1f, c, new Color(c.r, c.g, c.b, 0f), 0f, true,
                            0f, 3f, (a * Mathf.Rad2Deg) - 90f);
                    }
                    Impact(at, c, 0.9f, lv);
                    break;
                }
                case HitSource.Steam:
                {
                    // 증기에 데어 건물 바깥으로 튕기는 흰 김 꼬리.
                    Vector3 away = (at - from).sqrMagnitude > 0.0001f ? (at - from).normalized : Vector3.up;
                    float deg = (Mathf.Atan2(away.y, away.x) * Mathf.Rad2Deg) - 90f;
                    EmitSprite(BeamSprite(), at - (away * 0.3f), away * 2.5f, 3f, 0.2f, 0.4f, 0.16f, new Color(1f, 1f, 1f, 0.9f), new Color(0.9f, 0.95f, 1f, 0f), 0f, true, 0f, 3f, deg);
                    Steam(at, 2, 0.7f);
                    break;
                }
                case HitSource.Curtain:
                {
                    // 장막에 밀려나는 물 꼬리.
                    Vector3 away = Away(h.Pos);
                    float deg = (Mathf.Atan2(away.y, away.x) * Mathf.Rad2Deg) - 90f;
                    EmitSprite(BeamSprite(), at - (away * 0.4f), away * 3f, 4f, 0.25f, 0.5f, 0.2f, new Color(CurtainTint.r, CurtainTint.g, CurtainTint.b, 0.9f), new Color(CurtainTint.r, CurtainTint.g, CurtainTint.b, 0f), 0f, true, 0f, 3.5f, deg);
                    break;
                }
                case HitSource.Bomb:
                    Impact(at, BombTint, 0.7f, b.PowerOf(UpgradeId.WaterBomb));
                    break;
                default:
                    return;
            }
            _impacts[slot]++;
        }

        /// <summary>맞은 자리: 흰 번쩍 + 고리 + 사방 물방울. MAX는 금빛 고리가 한 겹 더.</summary>
        private void Impact(Vector3 at, Color c, float size, int level)
        {
            float s = size * LevelScale(level);
            var clear = new Color(c.r, c.g, c.b, 0f);
            // 글로우는 한 겹, 고리는 무기 색: 무기마다 색으로 구분된다.
            Emit("Effects/glow", at, Vector3.zero, 0f, 0.07f, 0.7f * s, 1.1f * s, new Color(1f, 1f, 1f, 0.7f), clear, 0f, true);
            Shockwave(at, c, 1.8f * s, 0.2f);
            if (level >= Loadout.MaxLevel) Shockwave(at, new Color(1f, 0.85f, 0.35f, 0.95f), 2.5f * s, 0.25f, 0.04f);
            int n = 4 + level;
            for (int i = 0; i < n; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                Emit("Effects/water_drop", at, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Random.Range(4f, 8f) * s, 7f, Random.Range(0.2f, 0.3f),
                    0.3f * s, 0.05f, new Color(0.8f, 0.96f, 1f, 1f), clear, 0f);
            }
        }

        private void AddTracer(Vector3 from, Vector3 to, float width, Color color, float life)
        {
            if (_tracers.Count >= MaxTracers) _tracers.RemoveAt(0);
            _tracers.Add(new Tracer { From = from, To = to, Width = width, Color = color, Life = life });
        }

        private void AddWet(Vector3 at, float size, float life, bool ring)
        {
            if (_wetMarks.Count >= 60) _wetMarks.RemoveAt(0);
            _wetMarks.Add(new WetMark { At = at, Size = size, Life = life, Ring = ring });
        }

        private void WeaponSound(Cue cue)
        {
            if (_weaponSoundClock > 0f) return;
            GameAudio.Play(cue);
            _weaponSoundClock = 0.08f;
        }

        /// <summary>물이 불난 지붕에 떨어졌는지(김 기둥을 세운다).</summary>
        private bool RoofAt(Vec2 p)
        {
            foreach (Structure st in _sim.Structures)
            {
                if (st.IsBuilding && st.Within(p, 0.5f) && (st.Burning || st.Wet > 0f)) return true;
            }
            return false;
        }

        /// <summary>지붕에서 하얀 김이 기둥처럼 솟는다.</summary>
        private void SteamPillar(Vector3 at, float scale)
        {
            int n = Mathf.RoundToInt(6 * scale);
            for (int i = 0; i < n; i++)
            {
                var v = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(3f, 6f), 0f) * scale;
                Emit(Smokes[Random.Range(0, Smokes.Length)], at + new Vector3(Random.Range(-0.5f, 0.5f), 0f, 0f), v, 1.5f, Random.Range(0.7f, 1.1f),
                    1f * scale, 3.2f * scale, new Color(1f, 1f, 1f, 0.85f), new Color(0.9f, 0.95f, 1f, 0f), Random.Range(-90f, 90f));
            }
        }

        /// <summary>물의 장막 폭발: 흰 번쩍, 발밑에서 사방으로 솟는 물벽, 물방울, 흔들림·멈칫, 땅에 젖은 고리.</summary>
        private void CurtainBurst()
        {
            Vector3 at = W(_sim.Player);
            bool wall = _sim.Build.Level(UpgradeId.WaterWall) > 0;
            int lv = wall ? Loadout.MaxLevel : _sim.Build.PowerOf(UpgradeId.Curtain);
            float r = _sim.CurtainRadiusNow;
            float ring = r * 2.4f;
            Flash(new Color(0.75f, 0.92f, 1f), wall ? 0.22f : 0.14f);
            Flare(at, r * 1.4f, new Color(0.7f, 0.92f, 1f), wall ? 3 : 2, 0.14f);
            Shockwave(at, new Color(0.45f, 0.8f, 1f, 1f), ring, 0.4f);
            Shockwave(at, new Color(0.85f, 0.97f, 1f, 0.9f), ring * 0.8f, 0.35f, 0.08f);
            if (wall || lv >= Loadout.MaxLevel) Shockwave(at, new Color(1f, 0.85f, 0.35f, 1f), ring * 1.1f, 0.45f, 0.05f);
            // 물벽: 세로로 늘인 빛기둥이 발밑에서 바깥으로 달려 나간다.
            int beams = wall ? 36 : 24;
            for (int i = 0; i < beams; i++)
            {
                float a = i * Mathf.PI * 2f / beams;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                EmitSprite(BeamSprite(), at + (dir * 0.6f) + new Vector3(0f, 0.6f, 0f), dir * r * 2.6f, 2.5f, 0.38f, 0.7f, 0.3f,
                    wall ? new Color(0.85f, 0.95f, 1f, 0.95f) : new Color(0.6f, 0.9f, 1f, 0.9f), new Color(0.6f, 0.9f, 1f, 0f), 0f, true, 0f, 3.5f, 0f);
            }
            int drops = wall ? 64 : 48;
            for (int i = 0; i < drops; i++)
            {
                float a = i * Mathf.PI * 2f / drops;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Emit("Effects/water_drop", at + (dir * 0.8f), dir * Random.Range(9f, 14f) * (r / SurvivorSim.CurtainRadius), 3.5f, 0.45f, 0.5f, 0.1f,
                    new Color(0.8f, 0.96f, 1f, 1f), new Color(0.6f, 0.9f, 1f, 0f), 0f);
            }
            AddWet(at, r * 2.2f, 1f, true);
            _trauma = Mathf.Min(1f, _trauma + (wall ? 0.4f : 0.3f));
            _zoomKick = Mathf.Max(_zoomKick, 0.35f);
            HitStop(0.04f);
            WeaponSound(Cue.SprayFoam);
        }

        /// <summary>장막이 터지기 0.4초 전부터 발밑 빛이 모여든다.</summary>
        private void DrawCurtainCharge()
        {
            float left = _sim.CurtainIn;
            if (left > 0.4f || _sim.Outcome != SOutcome.Playing) return;
            float t = 1f - (left / 0.4f);
            Vector3 at = W(_sim.Player);
            float ring = _sim.CurtainRadiusNow * 2.4f;
            _auras.Put(at, Mathf.Lerp(ring, 1.2f, t), 0f, new Color(0.5f, 0.85f, 1f, 0.3f + (0.6f * t)));
            _groundGlow.Put(at, 1.5f + (2f * t), 0f, new Color(0.4f, 0.8f, 1f, 0.5f * t));
        }

        /// <summary>탄환 막대: 앞머리가 먼저 뻗고 꼬리가 따라가며 사라진다.</summary>
        private void DrawTracers()
        {
            foreach (Tracer tr in _tracers)
            {
                float t = Mathf.Clamp01(tr.Age / tr.Life);
                Vector3 head = Vector3.Lerp(tr.From, tr.To, Mathf.Clamp01(t * 2.2f));
                Vector3 tail = Vector3.Lerp(tr.From, tr.To, Mathf.Clamp01((t - 0.25f) * 1.6f));
                SprayLine(tail, head, tr.Width * (1f - (0.5f * t)), new Color(tr.Color.r, tr.Color.g, tr.Color.b, tr.Color.a * (1f - (t * t))));
                _dropGlow.Put(head, tr.Width * 4f, 0f, new Color(tr.Color.r, tr.Color.g, tr.Color.b, 0.7f * (1f - t)));
            }
        }

        private void DrawWetMarks()
        {
            foreach (WetMark m in _wetMarks)
            {
                float t = Mathf.Clamp01(m.Age / m.Life);
                float a = 0.35f * (1f - (t * t));
                if (m.Ring) _civilianRings.Put(m.At, m.Size * (1f + (0.1f * t)), 0f, new Color(0.35f, 0.7f, 1f, a));
                _wet.Put(m.At, m.Size * (m.Ring ? 0.7f : 1f), 0f, new Color(0.1f, 0.3f, 0.55f, a));
            }
        }

        private void AdvanceWeaponFx(float dt)
        {
            for (int i = _tracers.Count - 1; i >= 0; i--)
            {
                Tracer tr = _tracers[i];
                tr.Age += dt;
                if (tr.Age >= tr.Life) _tracers.RemoveAt(i);
                else _tracers[i] = tr;
            }
            for (int i = _wetMarks.Count - 1; i >= 0; i--)
            {
                WetMark m = _wetMarks[i];
                m.Age += dt;
                if (m.Age >= m.Life) _wetMarks.RemoveAt(i);
                else _wetMarks[i] = m;
            }
            for (int i = _turretLandingIn.Count - 1; i >= 0; i--)
            {
                _turretLandingIn[i] -= dt;
                if (_turretLandingIn[i] > 0f) continue;
                // 쿵: 먼지 고리 + 물 튀김 + 살짝 흔들림.
                Vector3 at = _turretLandings[i];
                bool post = _sim.Build.Level(UpgradeId.RescuePost) > 0;
                Shockwave(at, new Color(0.85f, 0.8f, 0.7f, 0.9f), 3.2f, 0.3f);
                if (post) Shockwave(at, new Color(0.4f, 1f, 0.5f, 0.9f), SurvivorSim.PostRange * 2f, 0.5f, 0.05f);
                for (int k = 0; k < 10; k++)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    Emit(Smokes[Random.Range(0, Smokes.Length)], at, new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.5f, 0f) * Random.Range(2f, 4f), 4f, 0.45f, 0.4f, 1.2f,
                        new Color(0.85f, 0.8f, 0.72f, 0.6f), new Color(0.85f, 0.8f, 0.72f, 0f), 0f);
                }
                Splash(at, 8, 0.8f);
                _trauma = Mathf.Min(1f, _trauma + 0.12f);
                WeaponSound(Cue.SprayFoam);
                _turretLandings.RemoveAt(i);
                _turretLandingIn.RemoveAt(i);
            }
        }

        /// <summary>공중 소화탄: 빨간 몸통, 흰 띠 두 줄, 꼬리 날개. 위쪽이 앞(떨어지는 쪽).</summary>
        private static Sprite AirBombSprite()
        {
            if (_airBombSprite != null) return _airBombSprite;
            const int n = 64;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            var red = new Color32(220, 50, 40, 255);
            var white = new Color32(245, 245, 245, 255);
            var dark = new Color32(110, 25, 20, 255);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n) - 0.5f;
                    float v = ((y + 0.5f) / n) - 0.5f;
                    Color32 c = new Color32(0, 0, 0, 0);
                    bool body = ((u * u) / (0.13f * 0.13f)) + (((v - 0.04f) * (v - 0.04f)) / (0.36f * 0.36f)) <= 1f;
                    bool fin = v < -0.2f && v > -0.44f && Mathf.Abs(u) < 0.06f + ((-0.2f - v) * 0.8f) && Mathf.Abs(u) > 0.02f;
                    if (fin) c = dark;
                    if (body)
                    {
                        c = red;
                        if (Mathf.Abs(v - 0.12f) < 0.03f || Mathf.Abs(v + 0.05f) < 0.03f) c = white;
                    }
                    pixels[(y * n) + x] = c;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _airBombSprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return _airBombSprite;
        }

        /// <summary>물폭탄: 퍼지는 물 고리 + 위로 솟았다 떨어지는 물기둥 + 김.</summary>
        private void WaterBlast(Vector3 at, float radius, int level)
        {
            // 고리 크기 = 실제 피해 범위. 레벨 3부터 고리 2겹, 최대 레벨은 물기둥이 굵어지고 물웅덩이 빛이 남는다.
            float ring = radius * 2.4f;
            Shockwave(at, new Color(0.55f, 0.85f, 1f, 1f), ring, 0.35f);
            if (level >= 3) Shockwave(at, new Color(0.8f, 0.95f, 1f, 0.9f), ring * 1.25f, 0.35f, 0.1f);
            if (level >= Loadout.MaxLevel)
            {
                Emit("Effects/glow", at, Vector3.zero, 0f, 0.8f, radius * 2.2f, radius * 2.4f, new Color(0.3f, 0.7f, 1f, 0.45f), new Color(0.3f, 0.7f, 1f, 0f), 0f, true);
                // MAX: 금빛 충격파 고리가 한 겹 더 퍼진다.
                Shockwave(at, new Color(1f, 0.85f, 0.35f, 0.95f), ring * 1.45f, 0.45f, 0.06f);
            }
            Emit("Effects/glow", at, Vector3.zero, 0f, 0.12f, radius * 1.7f, radius * 2.3f, new Color(0.9f, 0.97f, 1f, 0.9f), new Color(0.5f, 0.85f, 1f, 0f), 0f, true);
            int column = level >= Loadout.MaxLevel ? 40 : 20 + (4 * level);
            for (int i = 0; i < column; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                var v = new Vector3(Mathf.Cos(a) * Random.Range(1.5f, 4.5f), Random.Range(3f, 7f), 0f);
                EmitFalling("Effects/water_drop", at, v, Random.Range(0.45f, 0.7f), Random.Range(0.3f, 0.5f), new Color(0.7f, 0.93f, 1f, 1f));
            }
            Splash(at, 10, 1.2f);
            Steam(at, 3, 1.3f);
        }

        /// <summary>
        /// 노즐 끝에서 물이 조금씩 튄다(쥔 동안 이어지는 물보라, 쏠 때마다 번쩍이거나 튕기지 않는다).
        /// 방수포는 사방 제트가 돌 때 한 번 충격파.
        /// </summary>
        private void Muzzles()
        {
            Vector3 p = W(_sim.Player);
            bool jet = false;
            bool hose = false;
            foreach (Shot s in _sim.Shots)
            {
                if (s.Age > SurvivorSim.Dt * 1.5f) continue;
                if (s.Kind == ShotKind.Jet) jet = true;
                if (s.Hose) hose = true;
            }
            if (hose)
            {
                Vector3 tip = NozzleTip();
                Vector3 look = Look();
                float baseAngle = Mathf.Atan2(look.y, look.x);
                for (int i = 0; i < 2; i++)
                {
                    float a = baseAngle + Random.Range(-0.5f, 0.5f);
                    Emit("Effects/water_drop", tip, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Random.Range(3f, 6f), 6f, 0.2f,
                        0.18f, 0.05f, new Color(0.8f, 0.96f, 1f, 0.9f), new Color(0.6f, 0.9f, 1f, 0f), 0f);
                }
            }
            _jetPulse -= SurvivorSim.Dt * 3f;
            if (jet && _jetPulse <= 0f)
            {
                _jetPulse = 0.5f;
                Shockwave(p, new Color(0.55f, 0.85f, 1f, 0.9f), 5f, 0.25f);
                _recoil = 1f;
                _zoomKick = Mathf.Max(_zoomKick, 0.15f);
            }
        }

        /// <summary>플레이어에서 멀어지는 방향(물이 날아온 방향).</summary>
        private Vector3 Away(Vec2 from)
        {
            var d = new Vector3(from.X - _sim.Player.X, from.Y - _sim.Player.Y, 0f);
            return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.up;
        }

        private void HitStop(float seconds)
        {
            _hitStop = Mathf.Min(0.08f, Mathf.Max(_hitStop, seconds));
        }

        private void Splash(Vector3 at, int count, float spread)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                float speed = Random.Range(1.5f, 4f) * (0.5f + spread);
                Emit("Effects/water_drop", at, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * speed, 6f, Random.Range(0.2f, 0.35f),
                    Random.Range(0.22f, 0.38f), 0.05f, new Color(0.6f, 0.9f, 1f, 0.95f), new Color(0.6f, 0.9f, 1f, 0f), 0f);
            }
        }

        private void Burst(Vector3 at, int count, Color color, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                Emit(Sparks[Random.Range(0, Sparks.Length)], at, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Random.Range(speed * 0.4f, speed), 3f,
                    Random.Range(0.5f, 0.9f), 0.6f, 0.1f, color, new Color(color.r, color.g, color.b, 0f), Random.Range(-400f, 400f), true);
            }
        }

        /// <summary>속이 빈 고리가 퍼지는 파동 + 안쪽 은은한 빛.</summary>
        private void Shockwave(Vector3 at, Color color, float size, float life, float delay = 0f)
        {
            var clear = new Color(color.r, color.g, color.b, 0f);
            EmitSprite(RingSprite(), at, Vector3.zero, 0f, life, size * 0.15f, size, color, clear, 0f, true, 0f, 1f, float.NaN, delay);
            EmitSprite(Art.Get("Effects/glow"), at, Vector3.zero, 0f, life * 0.6f, size * 0.2f, size * 0.6f, new Color(color.r, color.g, color.b, color.a * 0.45f), clear, 0f, true,
                0f, 1f, float.NaN, delay);
        }

        /// <summary>발밑에서 하늘로 솟는 빛기둥(레벨업·구조·진화).</summary>
        private void Pillar(Vector3 at, Color color)
        {
            var clear = new Color(color.r, color.g, color.b, 0f);
            EmitSprite(BeamSprite(), at + new Vector3(0f, 5f, 0f), Vector3.zero, 0f, 0.55f, 1.4f, 3f, new Color(color.r, color.g, color.b, 0.95f), clear, 0f, true, 0f, 8f, 0f);
            EmitSprite(BeamSprite(), at + new Vector3(0f, 5f, 0f), Vector3.zero, 0f, 0.4f, 0.5f, 1f, Color.white, new Color(1f, 1f, 1f, 0f), 0f, true, 0f, 20f, 0f);
        }

        /// <summary>위로 흩날리며 사라지는 반짝이.</summary>
        private void Sparkle(Vector3 at, int count, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                var v = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(3f, 8f), 0f);
                Emit(Sparks[Random.Range(0, Sparks.Length)], at + new Vector3(Random.Range(-0.6f, 0.6f), 0f, 0f), v, 2f, Random.Range(0.6f, 1f),
                    Random.Range(0.4f, 0.7f), 0.1f, color, new Color(color.r, color.g, color.b, 0f), Random.Range(-500f, 500f), true);
            }
        }

        /// <summary>전면 번쩍임. 화면이 덮이지 않게 0.5를 넘지 않는다(승리만 직접 0.9).</summary>
        private void Flash(Color color, float strength)
        {
            _flashColor = color;
            _flash = Mathf.Max(_flash, Mathf.Min(strength, 0.5f));
        }

        private void Scorch(Vector3 at, float size)
        {
            SpriteRenderer r;
            if (_scorch.Count < MaxScorch)
            {
                r = NewSprite(_root, "Scorch", Art.Get("Effects/scorch_0" + (1 + (_scorch.Count % 3))), 1);
                _scorch.Add(r);
            }
            else
            {
                r = _scorch[_scorchNext];
                _scorchNext = (_scorchNext + 1) % MaxScorch;
            }
            r.enabled = true;
            r.transform.localPosition = at + new Vector3(0f, 0f, 0.05f);
            r.transform.localRotation = Quaternion.Euler(0f, 0f, Random.value * 360f);
            r.transform.localScale = Vector3.one * Art.FitWidth(r.sprite, size);
            r.color = new Color(0f, 0f, 0f, 0.22f);
        }

        private void Emit(string sprite, Vector3 at, Vector3 vel, float drag, float life, float size0, float size1, Color c0, Color c1, float spin, bool glow = false)
        {
            if (_particles.Count >= MaxParticles) return;
            EmitSprite(Art.Get(sprite), at, vel, drag, life, size0, size1, c0, c1, spin, glow);
        }

        /// <summary>떨어지는 물방울(중력을 받는다).</summary>
        private void EmitFalling(string sprite, Vector3 at, Vector3 vel, float life, float size, Color color)
        {
            if (_particles.Count >= MaxParticles) return;
            EmitSprite(Art.Get(sprite), at, vel, 0.5f, life, size, size * 0.6f, color, new Color(color.r, color.g, color.b, 0f), 0f, false, 14f);
        }

        /// <param name="stretch">세로 배율(빛기둥·물줄기).</param>
        /// <param name="rotation">NaN이면 아무 방향.</param>
        /// <param name="delay">이만큼 뒤에 나타난다(겹겹이 퍼지는 고리).</param>
        private void EmitSprite(Sprite sprite, Vector3 at, Vector3 vel, float drag, float life, float size0, float size1, Color c0, Color c1, float spin, bool glow,
            float gravity = 0f, float stretch = 1f, float rotation = float.NaN, float delay = 0f)
        {
            if (_particles.Count >= MaxParticles) return;
            SpriteRenderer r = _particlePool.Count > 0 ? _particlePool.Pop() : NewSprite(_world, "Particle", null, 14);
            if (_spriteMaterial == null) _spriteMaterial = r.sharedMaterial;
            r.sharedMaterial = glow && Additive != null ? Additive : _spriteMaterial;
            r.sprite = sprite;
            r.enabled = delay <= 0f;
            _particles.Add(new Particle
            {
                R = r,
                Pos = at,
                Vel = vel,
                Drag = drag,
                Life = life,
                Size0 = size0,
                Size1 = size1,
                Rot = float.IsNaN(rotation) ? Random.value * 360f : rotation,
                Age = -delay,
                Stretch = stretch,
                Spin = spin,
                Gravity = gravity,
                C0 = c0,
                C1 = c1,
                Unit = Art.FitWidth(r.sprite, 1f),
            });
        }

        private void AdvanceParticles(float dt)
        {
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                Particle p = _particles[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    p.R.enabled = false;
                    _particlePool.Push(p.R);
                    _particles.RemoveAt(i);
                    continue;
                }
                if (p.Age < 0f)
                {
                    _particles[i] = p;
                    continue;
                }
                if (!p.R.enabled) p.R.enabled = true;
                float t = p.Age / p.Life;
                p.Vel *= Mathf.Exp(-p.Drag * dt);
                p.Vel.y -= p.Gravity * dt;
                p.Pos += p.Vel * dt;
                p.Rot += p.Spin * dt;
                p.R.transform.localPosition = p.Pos + new Vector3(0f, 0f, -0.2f);
                p.R.transform.localRotation = Quaternion.Euler(0f, 0f, p.Rot);
                float size = p.Unit * Mathf.Lerp(p.Size0, p.Size1, t);
                p.R.transform.localScale = new Vector3(size, size * p.Stretch, 1f);
                p.R.color = Color.Lerp(p.C0, p.C1, t);
                _particles[i] = p;
            }
        }

        private void SpawnNumber(Vector3 at, float damage, bool crit)
        {
            if (_numbers.Count >= MaxNumbers && !crit) return;
            string text = Mathf.Max(1, Mathf.RoundToInt(damage)).ToString();
            SpawnText(at, crit ? text + "!" : text, crit ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 1f, 0.92f), crit ? 1.5f : 1f);
        }

        private void SpawnText(Vector3 at, string text, Color color, float size)
        {
            if (_numbers.Count >= MaxNumbers + 20) return;
            TextMesh t = _numberPool.Count > 0 ? _numberPool.Pop() : NewText();
            t.gameObject.SetActive(true);
            t.text = text;
            t.color = color;
            _numbers.Add(new Number
            {
                T = t,
                Pos = at + new Vector3(Random.Range(-0.3f, 0.3f), 0.3f, -0.5f),
                Vel = new Vector3(Random.Range(-0.8f, 0.8f), 4.5f, 0f),
                Life = size > 1.2f ? 0.9f : 0.6f,
                Size = size,
            });
        }

        private void AdvanceNumbers(float dt)
        {
            for (int i = _numbers.Count - 1; i >= 0; i--)
            {
                Number n = _numbers[i];
                n.Age += dt;
                if (n.Age >= n.Life)
                {
                    n.T.gameObject.SetActive(false);
                    _numberPool.Push(n.T);
                    _numbers.RemoveAt(i);
                    continue;
                }
                n.Vel.y -= 11f * dt;
                n.Pos += n.Vel * dt;
                float t = n.Age / n.Life;
                float pop = t < 0.12f ? Mathf.Lerp(1.7f, 1f, t / 0.12f) : 1f;
                n.T.transform.localPosition = n.Pos;
                n.T.characterSize = 0.07f * n.Size * pop;
                Color c = n.T.color;
                c.a = t < 0.6f ? 1f : 1f - ((t - 0.6f) / 0.4f);
                n.T.color = c;
                _numbers[i] = n;
            }
        }

        private void ClearEffects()
        {
            foreach (Particle p in _particles)
            {
                p.R.enabled = false;
                _particlePool.Push(p.R);
            }
            _particles.Clear();
            foreach (Number n in _numbers)
            {
                n.T.gameObject.SetActive(false);
                _numberPool.Push(n.T);
            }
            _numbers.Clear();
            foreach (SpriteRenderer s in _scorch) s.enabled = false;
            _tracers.Clear();
            _wetMarks.Clear();
            _turretLandings.Clear();
            _turretLandingIn.Clear();
        }

        // ------------------------------------------------------------------
        // 카메라·소리
        // ------------------------------------------------------------------

        private void FollowCamera(float dt)
        {
            if (_camera == null) return;
            // 줌 킥: +면 확 다가오고(레벨업·진화), −면 물러난다.
            float size = CameraSize * (1f - (0.12f * Mathf.Clamp(_zoomKick, -1.5f, 1.5f)));
            _camera.backgroundColor = new Color(0.05f, 0.05f, 0.07f);

            var target = new Vector3(_sim.Player.X, _sim.Player.Y, -10f);
            _cameraAt = dt <= 0f ? target : Vector3.Lerp(_cameraAt, target, 1f - Mathf.Exp(-10f * dt));

            float amp = _trauma * _trauma * 0.6f;
            float t = Time.realtimeSinceStartup * 25f;
            Vector3 shake = new Vector3((Mathf.PerlinNoise(t, 0f) * 2f) - 1f, (Mathf.PerlinNoise(0f, t) * 2f) - 1f, 0f) * amp;
            PlaceWorldCamera(_cameraAt + shake, size);
        }

        /// <summary>캡처용: 땅 위 center를 가운데 반높이 halfHeight로 보이게 월드 카메라를 옮긴다.</summary>
        public void Frame(Vector3 center, float halfHeight)
        {
            PlaceWorldCamera(center, halfHeight);
        }

        /// <summary>땅(z=0) 위 center를 Tilt도 기운 눈으로, 가운데 세로 반폭이 halfHeight가 되는 거리에서 본다.</summary>
        private void PlaceWorldCamera(Vector3 center, float halfHeight)
        {
            float dist = halfHeight / Mathf.Tan(WorldFov * 0.5f * Mathf.Deg2Rad);
            Vector3 forward = Billboard * Vector3.forward;
            center.z = 0f;
            _worldCam.transform.SetPositionAndRotation(center - (forward * dist), Billboard);
            // 화면 아래 변(가장 좁은 쪽)과 가운데를 땅에 비춰 보이는 반폭·반높이를 잰다.
            Vector3 mid = GroundAt(new Vector3(0.5f, 0.5f, 0f));
            Vector3 bottom = GroundAt(new Vector3(0.5f, 0f, 0f));
            Vector3 corner = GroundAt(new Vector3(0f, 0f, 0f));
            _viewHalfH = Mathf.Max(1f, mid.y - bottom.y);
            _viewHalfW = Mathf.Max(1f, bottom.x - corner.x);
        }

        /// <summary>월드 카메라 화면 한 점(뷰포트)이 땅(z=0)에 닿는 곳.</summary>
        private Vector3 GroundAt(Vector3 viewport)
        {
            Ray ray = _worldCam.ViewportPointToRay(viewport);
            return ray.GetPoint(Mathf.Abs(ray.direction.z) > 0.0001f ? -ray.origin.z / ray.direction.z : 0f);
        }

        private void BuildAudio()
        {
            if (!Application.isPlaying) return;
            _chimeClip = Resources.Load<AudioClip>("Audio/pickup");
            _chime = _root.gameObject.AddComponent<AudioSource>();
            _chime.playOnAwake = false;

            // 호스 소리: 쥐고 있는 동안만 볼륨을 올리는 반복 재생.
            _spray = _root.gameObject.AddComponent<AudioSource>();
            _spray.clip = MakeClip("HoseLoop", ProtoSounds.HoseLoop());
            _spray.loop = true;
            _spray.playOnAwake = false;
            _spray.volume = 0f;
            if (_spray.clip != null) _spray.Play();

            // 물이 불에 닿는 "치익".
            _sfx = _root.gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sizzleClip = Resources.Load<AudioClip>("Audio/putout");

            _splash = _root.gameObject.AddComponent<AudioSource>();
            _splash.playOnAwake = false;
            _splash.clip = MakeClip("HeliSplash", ProtoSounds.Splash());

            _steam = _root.gameObject.AddComponent<AudioSource>();
            _steam.playOnAwake = false;
            _steam.clip = MakeClip("SteamBurst", ProtoSounds.SteamBurst());
        }

        private void PlaySteam()
        {
            if (_steam == null || _steam.clip == null) return;
            _steam.pitch = Random.Range(0.9f, 1.1f);
            _steam.PlayOneShot(_steam.clip, 0.9f);
        }

        private static AudioClip MakeClip(string name, float[] samples)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, ProtoSounds.Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void PlaySplash()
        {
            if (_splash == null || _splash.clip == null) return;
            _splash.pitch = Random.Range(0.92f, 1.05f);
            _splash.PlayOneShot(_splash.clip, 0.9f);
        }

        /// <summary>호스 루프 볼륨을 쥔 상태에 맞춰 0.08초 만에 올리고 내린다. 방수포는 더 묵직하게.</summary>
        private void UpdateSpraySound(float dt)
        {
            if (_spray == null) return;
            bool on = _sim.Spraying && _sim.Outcome == SOutcome.Playing && _sim.PendingChoices == null;
            float target = on ? 0.35f : 0f;
            _spray.volume = Mathf.MoveTowards(_spray.volume, target, dt * (0.35f / 0.06f));
            // 물대포 레벨·펌프만큼 소리가 굵어진다(피치↓). 방수포는 가장 묵직하다.
            int power = _sim.Build.Level(UpgradeId.Hose) + _sim.Build.Level(UpgradeId.Tank);
            _spray.pitch = _sim.Build.Level(UpgradeId.Cannon) > 0 ? 0.8f : 1.05f - (0.025f * power);
        }

        /// <summary>물이 불에 닿는 동안 작은 "치익"(0.12초 간격).</summary>
        private void Sizzle()
        {
            if (_sfx == null || _sizzleClip == null || _sizzleClock > 0f) return;
            _sizzleClock = 0.12f;
            _sfx.pitch = Random.Range(0.9f, 1.2f);
            _sfx.PlayOneShot(_sizzleClip, 0.25f);
        }

        /// <summary>구슬 흡수음. 연달아 먹을수록 높아진다(뱀서의 "딩딩딩" 손맛).</summary>
        private void PlayChime(float pitch)
        {
            if (_chime == null || _chimeClip == null) return;
            _chime.pitch = pitch;
            _chime.PlayOneShot(_chimeClip, 0.3f);
        }

        // ------------------------------------------------------------------
        // 바닥·풀
        // ------------------------------------------------------------------

        private void BuildGround()
        {
            foreach (GameObject g in _ground) UiKit.Discard(g);
            _ground.Clear();
            bool forest = _sim.Stage.Number == 2;
            bool factory = _sim.Stage.Number == 3;
            bool harbor = _sim.Stage.Number == 4;
            bool night = _sim.Stage.Number == 5;
            // 야시장은 밤: 해를 낮추고 푸르게, 주변광을 어둡게. 다른 스테이지로 가면 되돌린다(등불·불꽃의 additive 글로우가 밤에 산다).
            // 밤 값(맵 특색 패스): 해 0.3·주변광 (0.2,0.22,0.34)·바닥 0.22·비네트 0.55가 겹쳐 "어둡기만 하고 보기 불편"했다 → 해 0.5, 주변광 (0.34,0.36,0.5), 비네트 0.3.
            if (_sun != null)
            {
                _sun.intensity = night ? 0.5f : 0.8f;
                _sun.color = night ? new Color(0.78f, 0.8f, 1f) : new Color(1f, 0.96f, 0.88f);
            }
            RenderSettings.ambientLight = night ? new Color(0.34f, 0.36f, 0.5f) : new Color(0.42f, 0.45f, 0.52f);
            if (_worldCam != null) _worldCam.backgroundColor = night ? new Color(0.04f, 0.04f, 0.09f) : new Color(0.05f, 0.05f, 0.07f);
            int size = (int)SurvivorSim.ArenaSize;
            float mid = size / 2f;
            // 바닥은 스테이지 전체를 그린 한 장(풀결·도로·광장·흙길이 이어진다). 해 그림자를 받는 Lit 쿼드에 깐다.
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floor.name = "Ground";
            Collider floorCollider = floor.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(floorCollider);
            else Object.DestroyImmediate(floorCollider);
            floor.transform.SetParent(_root, false);
            floor.transform.localPosition = new Vector3(mid, mid, 0.1f);
            floor.transform.localScale = new Vector3(size, size, 1f);
            var floorMat = new Material(Models3D.LitShader) { name = "GroundLit" };
            floorMat.SetTexture("_BaseMap", GroundArt.Paint(_sim.Stage.Number, size, mid, SurvivorForest.PathHalf).texture);
            floorMat.SetFloat("_Smoothness", 0f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;
            _ground.Add(floor);
            // 숲은 나무 사이로 햇빛 띠가 더 진하다.
            Material screen = _worldScreen.GetComponent<MeshRenderer>().sharedMaterial;
            if (screen.HasProperty("_ShaftColor")) screen.SetColor("_ShaftColor", new Color(1f, 0.92f, 0.7f, forest ? 0.1f : 0.06f));
            // 밤 골목의 구석이 비네트로 한 번 더 어두워지지 않게.
            if (screen.HasProperty("_Vignette")) screen.SetFloat("_Vignette", night ? 0.3f : 0.55f);

            if (factory)
            {
                // 공단 장식(판정 없음): 급수탑 하나와 작은 탱크들을 가장자리 빈터에 세운다.
                float[] decor = { 6f, 54f, 54f, 6f, 6f, 6f, 54f, 54f, 30f, 6f };
                for (int i = 0; i < decor.Length; i += 2)
                {
                    var p = new Vec2(decor[i], decor[i + 1]);
                    if (_sim.Structures.Exists(st => st.Within(p, 2f))) continue;
                    string kind = i == 0 ? "Industrial/water-tower" : "Industrial/detail-tank";
                    GameObject d = Models3D.Place(kind, _root, new Vector3(p.X, p.Y, 0f), 2.2f, 2.2f, Hash01(i + 40) * 360f, out _, i == 0 ? 4f : 1.8f);
                    if (d != null) _ground.Add(d);
                }
            }

            if (harbor)
            {
                // 항구 장식(판정 없음): 부두선에 크레인 둘, 안쪽에 컨테이너 더미, 부두 끝에 작은 탱크. 부두 옆 바다에 정박한 배 셋과 부표 다섯.
                float[] decor = { 4f, 33f, 30f, 38.2f, 56f, 33f, 15f, 46.5f, 45f, 46.5f, 24f, 18f, 36f, 18f };
                for (int i = 0; i < decor.Length; i += 2)
                {
                    var p = new Vec2(decor[i], decor[i + 1]);
                    if (_sim.Structures.Exists(st => st.Kind != StructureKind.Water && st.Within(p, 1.5f))) continue;
                    bool tank = i == 6 || i == 8;
                    bool crane = i == 0 || i == 2;
                    GameObject d;
                    if (crane)
                    {
                        d = StageModels.Crane(_root);
                        ItemModels.Place(d, new Vector3(p.X, p.Y, 0f), 0f, Vector3.down, 1f);
                    }
                    else
                    {
                        string kind = tank ? "Industrial/detail-tank" : ContainerModels[(i / 2) % ContainerModels.Length];
                        d = Models3D.Place(kind, _root, new Vector3(p.X, p.Y, 0f), tank ? 1.6f : 2.4f, tank ? 1.6f : 1.2f, tank ? 0f : (i % 4 == 0 ? 0f : 90f), out _, tank ? 1.6f : 1.2f);
                    }
                    if (d != null) _ground.Add(d);
                }
                float[] moored = { 18.6f, 43f, 41.4f, 44.5f, 11.4f, 45.5f };
                for (int i = 0; i < moored.Length; i += 2)
                {
                    GameObject b = StageModels.MooredBoat(_root, i / 2);
                    ItemModels.Place(b, new Vector3(moored[i], moored[i + 1], 0f), 0f, new Vector3(0f, i == 2 ? -1f : 1f, 0f), 0.8f);
                    _ground.Add(b);
                }
                for (int i = 0; i < 5; i++)
                {
                    GameObject b = StageModels.Buoy(_root);
                    ItemModels.Place(b, new Vector3(5f + (Hash01(i + 300) * 50f), 53.5f + (Hash01(i + 310) * 4f), 0f), 0f, Vector3.down, 1f);
                    _ground.Add(b);
                }
            }

            if (night)
            {
                // 야시장 장식(판정 없음): 골목 두 줄에 파라솔 탁자 여섯(등줄·점포·가판대를 피한다).
                float[] tables = { 11f, 27f, 11f, 33f, 30f, 27f, 30f, 33f, 49f, 27f, 49f, 33f };
                for (int i = 0; i < tables.Length; i += 2)
                {
                    var p = new Vec2(tables[i], tables[i + 1]);
                    if (_sim.Structures.Exists(st => st.Within(p, 1.5f))) continue;
                    GameObject t = StageModels.Table(_root, StageModels.StallPalette[(i / 2) % StageModels.StallPalette.Length]);
                    ItemModels.Place(t, new Vector3(p.X, p.Y, 0f), 0f, Vector3.down, 1f);
                    _ground.Add(t);
                }
            }

            if (!forest && !factory && !harbor && !night)
            {
                // 마을 풀밭 덤불(판정 없음): 도로·강·건물을 피해 흩어 세운다.
                for (int i = 0; i < 30; i++)
                {
                    var at = new Vector3(1f + (Hash01((i * 5) + 900) * (size - 2f)), 1f + (Hash01((i * 5) + 901) * (size - 2f)), 0f);
                    var p = new Vec2(at.x, at.y);
                    bool road = false;
                    foreach (float ry in GroundArt.TownRoadsY) road |= Mathf.Abs(at.y - ry) < 2f;
                    foreach (float rx in GroundArt.TownRoadsX) road |= Mathf.Abs(at.x - rx) < 2f;
                    bool river = Mathf.Abs(at.x - SurvivorTown.RiverX) < SurvivorTown.RiverHalf + 1f;
                    if (road || river || _sim.Structures.Exists(st => st.Within(p, 1.2f))) continue;
                    float width = 0.7f + (0.4f * Hash01((i * 5) + 902));
                    GameObject bush = Models3D.Place(i % 2 == 0 ? "Nature/plant_bush" : "Nature/plant_bushLarge", _root, at, width, width, Hash01((i * 5) + 903) * 360f, out _);
                    if (bush != null) _ground.Add(bush);
                    Models3D.Tint(bush, Color.white, NatureColor, 1);
                }
            }

            if (forest)
            {
                // 캠프 장식(판정 없음): 텐트 넷, 모닥불 둘, 장작 더미 셋을 캠프(남쪽) 빈터에.
                float[] camp = { 6f, 12f, 10f, 8f, 54f, 12f, 50f, 8f, 14f, 10f, 46f, 10f, 6f, 20f, 54f, 20f, 40f, 10f };
                for (int i = 0; i < camp.Length; i += 2)
                {
                    var p = new Vec2(camp[i], camp[i + 1]);
                    if (_sim.Structures.Exists(st => st.Within(p, 1.5f))) continue;
                    int k = i / 2;
                    GameObject d = k < 4 ? StageModels.Tent(_root, k % 2 == 0 ? new Color(0.9f, 0.5f, 0.2f) : new Color(0.3f, 0.55f, 0.8f)) : k < 6 ? StageModels.Campfire(_root) : StageModels.LogPile(_root);
                    ItemModels.Place(d, new Vector3(p.X, p.Y, 0f), 0f, k < 4 ? new Vector3(Hash01(i + 200) - 0.5f, -1f, 0f) : Vector3.down, 1f);
                    _ground.Add(d);
                }
                // 덤불과 바위(판정 없음): 길과 구조물을 피해 흩어 둔다.
                for (int i = 0; i < 70; i++)
                {
                    var at = new Vector3(1f + (Hash01(i * 3) * (size - 2f)), 1f + (Hash01((i * 3) + 1) * (size - 2f)), 0.08f);
                    var p = new Vec2(at.x, at.y);
                    if (Mathf.Abs(at.x - mid) < SurvivorForest.PathHalf + 1f || Mathf.Abs(at.y - mid) < SurvivorForest.PathHalf + 1f) continue;
                    if (_sim.Structures.Exists(st => st.Within(p, 0.9f))) continue;
                    bool rock = Hash01((i * 3) + 2) < 0.3f;
                    // 덤불·바위 모델(판정 없음).
                    string decor = rock ? (i % 2 == 0 ? "Nature/rock_largeA" : "Nature/rock_smallB") : (i % 2 == 0 ? "Nature/plant_bushLarge" : "Nature/plant_bush");
                    float width = rock ? 0.9f : 1.2f;
                    GameObject d = Models3D.Place(decor, _root, new Vector3(at.x, at.y, 0f), width, width, Hash01((i * 3) + 5) * 360f, out _);
                    if (d != null) _ground.Add(d);
                    Models3D.Tint(d, Color.white, rock ? (System.Func<string, Color?>)RockColor : NatureColor, 1);
                }
            }

            for (int i = -1; i <= size; i++)
            {
                Wall(i, -1);
                Wall(i, size);
                Wall(-1, i);
                Wall(size, i);
            }

            // 출동해 온 소방차(장식, 판정 없음): 광장 옆에 가로로 세운 소방차 모델.
            // 소방관이 지나다니는 한가운데를 가리지 않게 광장 왼쪽 위 모서리에 세운다.
            GameObject truck = Models3D.Place("Cars/firetruck", _root, new Vector3(mid - 6f, mid + 6f, 0f), 3.2f, 1.5f, 90f, out _);
            if (truck != null) _ground.Add(truck);
        }

        /// <summary>바닥·벽·장식 스프라이트. 스테이지가 바뀌면 한꺼번에 지운다.</summary>
        private SpriteRenderer GroundSprite(string name, string art, int order)
        {
            SpriteRenderer r = NewSprite(_root, name, Art.Get(art), order);
            _ground.Add(r.gameObject);
            return r;
        }

        /// <summary>판마다 가게·창고·차를 3D 모델로 한 번 세운다(규칙 크기 Half 안에 맞춤). 가게 모양은 이름으로 고른다.</summary>
        private void BuildModels()
        {
            foreach (GameObject g in _models) UiKit.Discard(g);
            _models.Clear();
            int n = _sim.Structures.Count;
            bool factory = _sim.Stage.Number == 3;
            // 항구도 컨테이너·드럼(연료 탱크)을 쓴다.
            bool yard = factory || _sim.Stage.Number == 4;
            _structModels = new GameObject[n];
            _structSize = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Structure st = _sim.Structures[i];
                var at = new Vector3(st.Pos.X, st.Pos.Y, 0f);
                float w = st.Half.X * 2f;
                float h = st.Half.Y * 2f;
                // 스테이지 전용 모델(숲 통나무 집·항구 창고·야시장 천막 점포)이 먼저. 없으면 지금처럼 Kenney 키트.
                GameObject go = StageModels.Build(_sim.Stage.Number, st, i, _root, out Vector3 size);
                bool custom = go != null;
                if (custom)
                {
                }
                else if (st.Kind == StructureKind.Depot && factory)
                {
                    // 정유 저장소: 큰 원통 탱크 둘을 나란히.
                    go = new GameObject("Refinery");
                    go.transform.SetParent(_root, false);
                    for (int k = 0; k < 2; k++)
                    {
                        Models3D.Place("Industrial/detail-tank-large", go.transform, at + new Vector3((k - 0.5f) * w * 0.5f, 0f, 0f), w * 0.48f, h * 0.95f, 0f, out Vector3 half, MaxHouseHeight);
                        size = new Vector3(w, Mathf.Max(size.y, half.y), Mathf.Max(size.z, half.z));
                    }
                }
                else if (st.Kind == StructureKind.Depot)
                {
                    // 창고(넓은 터): 주택 두 채를 나란히.
                    go = new GameObject("Depot");
                    go.transform.SetParent(_root, false);
                    for (int k = 0; k < 2; k++)
                    {
                        string path = HouseModels[(i + (k * 3)) % HouseModels.Length];
                        Models3D.Place(path, go.transform, at + new Vector3((k - 0.5f) * w * 0.5f, 0f, 0f), w * 0.48f, h * 0.95f, 0f, out Vector3 half, MaxHouseHeight);
                        size = new Vector3(w, Mathf.Max(size.y, half.y), Mathf.Max(size.z, half.z));
                    }
                }
                else if (st.IsBuilding && factory)
                {
                    // 공장: Kenney 공장 + 문 앞 이름별 마당 소품(종이 두루마리·페인트 통·리프트 위 차…)을 한 루트로.
                    int pick = 0;
                    foreach (char ch in st.Name) pick += ch;
                    go = new GameObject("Factory");
                    go.transform.SetParent(_root, false);
                    Models3D.Place(FactoryModels[pick % FactoryModels.Length], go.transform, at, w * 0.95f, h * 0.95f, 0f, out size, MaxHouseHeight);
                    StageModels.FactoryYard(go.transform, st, at);
                }
                else if (st.IsBuilding)
                {
                    int pick = 0;
                    foreach (char ch in st.Name) pick += ch;
                    go = Models3D.Place(HouseModels[pick % HouseModels.Length], _root, at, w * 0.95f, h * 0.95f, 0f, out size, MaxHouseHeight);
                }
                else if (st.Kind == StructureKind.Car)
                {
                    go = yard
                        ? Models3D.Place(ContainerModels[i % ContainerModels.Length], _root, at, w, h, 90f, out size)
                        : Models3D.Place(CarModels[i % CarModels.Length], _root, at, w, h, 90f, out size);
                }
                else if (st.Kind == StructureKind.Fireworks)
                {
                    // 불꽃 가판대: 기본 도형 모델(퓨즈 깜빡임·발사 스파크는 DrawTown이 칠한다).
                    go = ItemModels.FireworkStand(_root);
                    ItemModels.Place(go, at, 0f, Vector3.up, 1f);
                }
                else if (st.Kind == StructureKind.Gas && yard)
                {
                    // 약품 드럼: 기본 도형 모델(퓨즈 깜빡임은 DrawTown이 칠한다).
                    go = ItemModels.Drum(_root);
                    ItemModels.Place(go, at, 0f, Vector3.up, 0.8f);
                }
                else if (st.Kind == StructureKind.Tree)
                {
                    // 마을은 둥근 활엽수, 숲은 소나무. 키 약 2.3칸, 도는 각은 번호로 섞는다.
                    string[] kinds = _sim.Stage.Number == 2 ? ForestTrees : TownTrees;
                    go = Models3D.Place(kinds[i % kinds.Length], _root, at, 1.3f, 1.3f, Hash01(i + 70) * 360f, out size, 2.3f);
                }
                if (go == null) continue;
                _models.Add(go);
                _structModels[i] = go;
                _structSize[i] = size;
                // 주택 모델은 지붕이 모두 초록이라, 예전 가게 그림의 지붕색으로 지붕만 다시 칠해 가게를 구별한다.
                // 공장은 모델마다 모양이 달라 키트 원래 색(회보라·주황)을 둔다.
                if (st.IsBuilding && !factory && !custom)
                {
                    // 밝기는 가장 센 채널 0.9로 맞춰 지붕이 칙칙하지 않게. 창고는 두 채 묶음(루트)에 한 번.
                    Color shop = RoofColor(ShopArt.For(st.Name, w, h));
                    float top = Mathf.Max(0.01f, Mathf.Max(shop.r, Mathf.Max(shop.g, shop.b)));
                    Models3D.RoofColor(go, new Color(shop.r / top * 0.9f, shop.g / top * 0.9f, shop.b / top * 0.9f));
                }
            }
        }

        /// <summary>Kenney 자연 키트 머티리얼(청록 잎·주황 흙)을 바닥 풀빛에 맞춘 차분한 색으로.</summary>
        private static Color? NatureColor(string material)
        {
            if (material.StartsWith("leafsDark")) return new Color(0.2f, 0.42f, 0.24f);
            if (material.StartsWith("leafs")) return new Color(0.34f, 0.58f, 0.26f);
            if (material.StartsWith("woodBark")) return new Color(0.42f, 0.3f, 0.22f);
            if (material.StartsWith("grass")) return new Color(0.3f, 0.52f, 0.26f);
            if (material.StartsWith("dirt")) return new Color(0.45f, 0.36f, 0.26f);
            return null;
        }

        /// <summary>바위: 몸통(흙 머티리얼)은 회색, 이끼(풀)는 어두운 초록.</summary>
        private static Color? RockColor(string material)
        {
            if (material.StartsWith("dirt")) return new Color(0.55f, 0.55f, 0.57f);
            if (material.StartsWith("grass")) return new Color(0.28f, 0.45f, 0.25f);
            return null;
        }

        /// <summary>예전 가게 그림의 지붕 평균색(빵집 주황·꽃집 초록·문구점 파랑…).</summary>
        private static Color RoofColor(ShopArt.Look look)
        {
            Texture2D tex = look.Roof.texture;
            Rect r = look.Roof.textureRect;
            float cr = 0f, cg = 0f, cb = 0f;
            int n = 0;
            for (int y = (int)r.yMin; y < (int)r.yMax; y += 4)
            {
                for (int x = (int)r.xMin; x < (int)r.xMax; x += 4)
                {
                    Color c = tex.GetPixel(x, y);
                    if (c.a < 0.5f) continue;
                    cr += c.r;
                    cg += c.g;
                    cb += c.b;
                    n++;
                }
            }
            return n > 0 ? new Color(cr / n, cg / n, cb / n) : Color.white;
        }

        /// <summary>구조물 지붕 높이(칸): 모델 높이, 모델이 없으면 예전 상자 높이.</summary>
        private float RoofHeight(int i)
        {
            if (i >= 0 && i < _structModels.Length && _structModels[i] != null) return _structSize[i].z;
            Structure st = _sim.Structures[i];
            return st.IsBuilding ? ShopArt.For(st.Name, st.Half.X * 2f, st.Half.Y * 2f).Height : CarHeight;
        }

        /// <summary>건물 앞벽 창 자리 k(0 가운데, 1 왼쪽, 2 오른쪽): 높이 35%, 모델 앞면 바로 앞.</summary>
        private Vector3 WindowPoint(int i, int k)
        {
            Structure st = _sim.Structures[i];
            Vector3 size = i < _structSize.Length && _structSize[i].z > 0f ? _structSize[i] : new Vector3(st.Half.X * 2f, st.Half.Y * 2f, RoofHeight(i));
            float x = k == 0 ? 0f : (k == 1 ? -0.28f : 0.28f) * size.x;
            return new Vector3(st.Pos.X + x, st.Pos.Y - (size.y / 2f) - 0.04f, -size.z * 0.35f);
        }

        /// <summary>가게·창고 이름표. 판마다 동네를 새로 깔므로 다시 만든다.</summary>
        private void BuildSigns()
        {
            foreach (TextMesh t in _signs) UiKit.Discard(t.gameObject);
            foreach (TextMesh t in _helps) UiKit.Discard(t.gameObject);
            _signs.Clear();
            _helps.Clear();
            _signBoards.Clear();
            if (_partnerTag != null) UiKit.Discard(_partnerTag.gameObject);
            _partnerTag = NewText();
            _partnerTag.transform.SetParent(_root, false);
            _partnerTag.GetComponent<MeshRenderer>().sortingOrder = 18;
            _partnerTag.text = "구조대";
            _partnerTag.characterSize = 0.04f;
            _partnerTag.color = new Color(1f, 0.9f, 0.35f);
            _partnerTag.gameObject.SetActive(false);
            for (int i = 0; i < _sim.Structures.Count; i++)
            {
                Structure st = _sim.Structures[i];
                if (!st.IsBuilding) continue;
                // 이름은 지붕 앞 가장자리 위 간판에 쓴다.
                float roof = RoofHeight(i);
                TextMesh t = NewText();
                t.transform.SetParent(_root, false);
                t.GetComponent<MeshRenderer>().sortingOrder = 20;
                t.text = st.Name;
                // 도트 화면에서도 읽히게 지붕 앞 가장자리에 큰 간판으로 세운다(글자 한 줄 ≈ 10픽셀).
                t.characterSize = 0.11f;
                t.color = Color.white;
                // 야시장 천막 점포는 앞 좌판에 물건이 있어 이름표를 뒤(차양 뒤 끝)로 올린다.
                bool stall = i < _structModels.Length && _structModels[i] != null && _structModels[i].name == "Stall";
                float signY = stall ? st.Pos.Y + (st.Half.Y * 0.8f) : st.Pos.Y - (st.Half.Y * 0.75f);
                float signUp = stall ? roof + 0.1f : roof + 0.45f;
                t.transform.localPosition = new Vector3(st.Pos.X, signY, 0f) + Up(signUp);
                t.transform.localRotation = Billboard;
                // 간판 판: 글자 뒤 짙은 띠(가게 색 대신 읽기 쉬운 어두운 판).
                _signBoards.Add(new Vector4(st.Pos.X, signY, signUp, Mathf.Min(st.Half.X * 2f - 0.2f, (st.Name.Length * 0.62f) + 0.5f)));
                _signs.Add(t);

                // 갇힌 사람이 외치는 말풍선(불이 나야 보인다).
                TextMesh help = NewText();
                help.transform.SetParent(_root, false);
                help.GetComponent<MeshRenderer>().sortingOrder = 18;
                help.characterSize = 0.06f;
                help.gameObject.SetActive(false);
                _helps.Add(help);
            }
        }

        /// <summary>동네: 건물(그림자·벽·지붕·창·문·불·튼튼함 막대), 나무·차·가스통.</summary>
        private void DrawTown()
        {
            RoofRects.Clear();
            RoofHeights.Clear();
            for (int k = 0; k < _sim.Structures.Count; k++)
            {
                Structure st = _sim.Structures[k];
                if (st.Collapsed || !(st.IsBuilding || st.Kind == StructureKind.Car)) continue;
                float hgt = RoofHeight(k);
                RoofRects.Add(new Rect(st.Pos.X - st.Half.X + 0.05f, st.Pos.Y - st.Half.Y + 0.05f, (st.Half.X * 2f) - 0.1f, (st.Half.Y * 2f) - 0.1f));
                RoofHeights.Add(hgt);
            }
            int sign = 0;
            for (int i = 0; i < _sim.Structures.Count; i++)
            {
                Structure st = _sim.Structures[i];
                var at = new Vector3(st.Pos.X, st.Pos.Y, 0f);
                float w = st.Half.X * 2f;
                float h = st.Half.Y * 2f;
                float burnt = 1f - Mathf.Clamp01(st.Integrity);
                float wet = st.Wet > 0f ? Mathf.Min(1f, st.Wet / 2f) : 0f;
                GameObject model = i < _structModels.Length ? _structModels[i] : null;
                if (model != null && model.activeSelf == st.Collapsed) model.SetActive(!st.Collapsed);

                if (st.Kind == StructureKind.Boat)
                {
                    if (!st.Collapsed) DrawBoat(st, at, i);
                    continue;
                }

                if (st.IsBuilding)
                {
                    if (sign < _signs.Count)
                    {
                        _signs[sign].gameObject.SetActive(!st.Collapsed);
                        if (!st.Collapsed && sign < _signBoards.Count)
                        {
                            Vector4 b = _signBoards[sign];
                            Vector3 board = new Vector3(b.x, b.y, 0f) + Up(b.z) + (Billboard * new Vector3(0f, 0f, 0.02f));
                            _bars.PutRot(board, Billboard, b.w, 0.62f, new Color(0.12f, 0.1f, 0.1f, 0.85f));
                        }
                        DrawTrapped(st, _helps[sign], i);
                        sign++;
                    }
                    DrawBuilding(st, at, w, h, burnt, wet, i);
                    continue;
                }

                if (st.Collapsed)
                {
                    // 탄 자리: 검은 그루터기·잔해만 남는다.
                    if (st.Kind != StructureKind.Gas) _houseShadows.Put(at, w * 0.8f, 0f, new Color(0.05f, 0.06f, 0.12f, 0.55f), null, h / w);
                    continue;
                }
                // 강은 바닥 그림이 그린다(모델·그림자·불 없음).
                if (st.Kind == StructureKind.Water) continue;

                Color tint = Color.Lerp(Color.white, new Color(0.25f, 0.2f, 0.2f), burnt);
                if (wet > 0f) tint = Color.Lerp(tint, new Color(0.7f, 0.85f, 1f), 0.35f * wet);
                switch (st.Kind)
                {
                    case StructureKind.Tree:
                        // 나무 모델(그림자는 해가 드리운다): 탈수록 검게 그을린다.
                        Models3D.Tint(model, tint, NatureColor, 1);
                        break;
                    case StructureKind.Car:
                        // 차 모델(그림자는 해가 드리운다): 탈수록 검게, 젖으면 파랗게.
                        Models3D.Tint(model, tint);
                        break;
                    case StructureKind.Gas:
                        // 퓨즈가 도는 동안 빨갛게 깜빡이며 부풀고 불똥이 튄다. 연쇄 폭발(공단 대화재)의 긴 퓨즈는 남은 초를 빨간 숫자와 고리로 보여 준다.
                        bool chain = st.Fuse > SurvivorSim.GasFuse;
                        float fuse = st.Fuse >= 0f ? Mathf.Clamp01(1f - (st.Fuse / (chain ? SurvivorSim.ChainFuse : SurvivorSim.GasFuse))) : 0f;
                        bool blink = st.Fuse >= 0f && Mathf.Sin(_time * (10f + (30f * fuse))) > 0f;
                        _shadows.Put(at + new Vector3(0.1f, -0.25f, 0f), 1f, 0f, new Color(0f, 0f, 0f, 0.4f), null, 0.5f);
                        if (chain)
                        {
                            _reticle.Put(at + Up(0.05f), 2.6f + (1.5f * fuse), _time * 120f, new Color(1f, 0.25f, 0.1f, 0.7f));
                            Tag(at + Up(1.9f), Mathf.CeilToInt(st.Fuse) + "초", blink ? Color.white : new Color(1f, 0.35f, 0.2f), 0.05f);
                        }
                        if (st.Fuse >= 0f)
                        {
                            _roofGlow.Put(at, 2f + (2f * fuse), 0f, new Color(1f, 0.25f, 0.05f, blink ? 0.8f : 0.35f));
                            if (Random.value < 0.3f)
                            {
                                Emit(Sparks[Random.Range(0, Sparks.Length)], at + new Vector3(0f, 0.3f, 0f), new Vector3(Random.Range(-2f, 2f), Random.Range(2f, 4f), 0f), 1f, 0.35f,
                                    0.3f, 0.05f, new Color(1f, 0.9f, 0.4f), new Color(1f, 0.3f, 0.05f, 0f), 0f, true);
                            }
                        }
                        if (model != null)
                        {
                            // 약품 드럼 모델: 퓨즈가 돌면 빨갛게 깜빡이며 부푼다.
                            Models3D.Tint(model, blink ? new Color(1f, 0.45f, 0.35f) : tint);
                            model.transform.localScale = Vector3.one * (model.name == "DrumModel" ? 0.8f : 1f) * (1f + (0.2f * fuse));
                        }
                        else
                        {
                            _props.Put(at, 0.85f * (1f + (0.25f * fuse)), 0f, blink ? new Color(1f, 0.55f, 0.45f) : tint, Art.Get("Props/barrel_red"));
                        }
                        break;
                    case StructureKind.Fireworks:
                    {
                        // 불꽃 가판대: 퓨즈가 도는 동안 가스통처럼 빨갛게 깜빡이고, 쏘는 동안 발사관에서 금색 스파크가 치솟는다.
                        float wick = st.Fuse >= 0f ? 1f - (st.Fuse / SurvivorSim.GasFuse) : 0f;
                        bool blinkStand = st.Fuse >= 0f && Mathf.Sin(_time * (10f + (30f * wick))) > 0f;
                        if (st.Fuse >= 0f) _roofGlow.Put(at, 2.5f + (2f * wick), 0f, new Color(1f, 0.25f, 0.05f, blinkStand ? 0.8f : 0.35f));
                        if (st.Launching)
                        {
                            _roofGlow.Put(at + Up(1.2f), 3.5f, 0f, new Color(1f, 0.75f, 0.3f, 0.5f + (0.3f * Mathf.Sin(_time * 20f))));
                            for (int k = 0; k < 10; k++)
                            {
                                Emit(Sparks[Random.Range(0, Sparks.Length)], at + Up(1.3f) + new Vector3(Random.Range(-0.7f, 0.7f), 0f, 0f), new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(5f, 9f), 0f), 1.5f, 0.4f,
                                    0.35f, 0.05f, new Color(1f, 0.9f, 0.4f), new Color(1f, 0.4f, 0.1f, 0f), 0f, true);
                            }
                            _trauma = Mathf.Max(_trauma, 0.05f);
                        }
                        if (model != null) Models3D.Tint(model, blinkStand || st.Launching ? new Color(1f, 0.55f, 0.4f) : tint);
                        break;
                    }
                }
                if (st.Burning && st.Kind != StructureKind.Gas) DrawRoofFire(st, at, w, h, i, st.Kind == StructureKind.Car ? RoofHeight(i) : st.Kind == StructureKind.Tree ? RoofHeight(i) * 0.55f : 0f);
            }
            if (_sim.Lanterns.Count > 0) DrawLanterns();
        }

        /// <summary>등줄 높이(점포 지붕 위).</summary>
        private const float LanternHeight = 3f;

        /// <summary>야시장 등줄: 점포 사이 가는 줄에 1.4칸마다 주황 등불(additive, 바닥에 빛무리). 타는 줄은 불꽃이 줄을 따라 가고 지나간 등불은 꺼진다. 젖은 줄은 등불이 파랗고 물이 듣는다.</summary>
        private void DrawLanterns()
        {
            for (int n = 0; n < _sim.Lanterns.Count; n++)
            {
                Lantern l = _sim.Lanterns[n];
                if (l.A.Collapsed || l.B.Collapsed) continue;
                Vector3 a = W(l.A.Pos) + Up(LanternHeight);
                Vector3 b = W(l.B.Pos) + Up(LanternHeight);
                Vector3 d = b - a;
                float len = d.magnitude;
                if (len < 0.5f) continue;
                float deg = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                bool wet = l.Wet > 0f;
                _motes.Put((a + b) * 0.5f, 0.08f, deg - 90f, new Color(0.25f, 0.18f, 0.12f, 0.85f), null, len / 0.08f);
                // 타는 줄: From 쪽에서 Burn만큼 왔다.
                Vector3 from = l.From == l.B ? b : a;
                Vector3 to = l.From == l.B ? a : b;
                float burn = l.Burn;
                int count = Mathf.Max(1, (int)(len / 1.4f));
                for (int k = 0; k < count; k++)
                {
                    float t = (k + 0.5f) / count;
                    Vector3 p = a + (d * t);
                    // 줄 불이 지나간 등불은 꺼져 검게 남는다.
                    float along = l.From == l.B ? 1f - t : t;
                    bool dead = burn >= 0f && along <= burn;
                    float flick = 0.85f + (0.15f * Mathf.Sin((_time * 6f) + (k * 1.3f) + n));
                    Color lamp = wet ? new Color(0.5f, 0.75f, 1f, 0.8f) : dead ? new Color(0.15f, 0.1f, 0.08f, 0.9f) : new Color(1f, 0.6f, 0.25f, 0.95f * flick);
                    _siren.Put(p, dead ? 0.55f : 1.1f, 0f, lamp);
                    // 등불 빛이 바닥을 물들인다(전 2.2/α0.12는 밤바닥에 안 보였다).
                    if (!dead) _groundGlow.Put(W(new Vec2(p.x, p.y)), 3.6f, 0f, new Color(1f, 0.7f, 0.35f, 0.3f * flick));
                    if (wet && Random.value < 0.08f) EmitFalling("Effects/water_drop", p, new Vector3(0f, -1f, 0f), 0.5f, 0.3f, new Color(0.7f, 0.9f, 1f, 0.9f));
                }
                if (burn >= 0f)
                {
                    Vector3 fire = Vector3.Lerp(from, to, burn);
                    Vector3 dir = (to - from).normalized;
                    for (int k = -1; k <= 1; k++)
                    {
                        float flick = 0.85f + (0.2f * Mathf.Sin((_time * (12f + k)) + (k * 1.9f) + n));
                        _embers.Put(fire + (dir * k * 0.45f), 1f * flick, 0f, Color.white, FlameArt.Frame(_emberSheet, _time, (n * 3) + k + 1));
                    }
                    _enemyGlow.Put(fire, 2f, 0f, new Color(1f, 0.5f, 0.1f, 0.35f));
                    if (Random.value < 0.5f)
                    {
                        Emit(Sparks[Random.Range(0, Sparks.Length)], fire, new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(-2f, 1f), 0f), 1f, 0.6f, 0.3f, 0.05f, new Color(1f, 0.85f, 0.35f), new Color(1f, 0.3f, 0.05f, 0f), 0f, true);
                    }
                }
            }
        }

        /// <summary>물 불꽃(야시장 노란): 무대에서 높이 솟아 표적 위로 떨어지는 청백 포탄. 흰 줄기 꼬리와 반짝임, 표적엔 하늘색 고리가 조여 든다.</summary>
        private void DrawShell(Shot s)
        {
            float t = Mathf.Clamp01(s.Age / s.Life);
            Vector3 from = W(s.From);
            Vector3 to = W(s.Target);
            Vector3 ground = Vector3.Lerp(from, to, t);
            float lift = 9f * Mathf.Sin(t * Mathf.PI);
            Vector3 at = ground + Up(lift);
            _shadows.Put(ground, 0.6f + (0.5f * t), 0f, new Color(0f, 0f, 0f, 0.2f + (0.2f * t)));
            _enemyCore.Put(at, 0.7f, 0f, new Color(0.85f, 0.97f, 1f, 1f));
            _enemyGlow.Put(at, 2.2f, 0f, new Color(0.5f, 0.85f, 1f, 0.4f));
            Vector3 vel = (to - from) / Mathf.Max(0.01f, s.Life);
            float ang = (Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg) - 90f;
            EmitSprite(BeamSprite(), at - new Vector3(0f, 0.6f, 0f), Vector3.zero, 0f, 0.2f, 0.25f, 0.1f, new Color(1f, 1f, 1f, 0.9f), new Color(0.7f, 0.9f, 1f, 0f), 0f, true, 0f, 1.3f / 0.25f, ang);
            if (Random.value < 0.6f) Sparkle(at, 1, new Color(0.8f, 0.95f, 1f));
            float ring = Mathf.Lerp(SurvivorSim.ShellRadius * 3f, SurvivorSim.ShellRadius * 1.6f, t);
            _reticle.Put(to + Up(0.05f), ring, _time * 120f, new Color(0.6f, 0.9f, 1f, 0.3f + (0.5f * t)));
        }

        /// <summary>물안개(야시장 노란): 낮게 깔린 흰 구름 여덟 장이 천천히 돌며 숨 쉬고, 바닥은 희게, 가장자리 흰 고리, 물방울이 듣는다.</summary>
        private void DrawMist()
        {
            if (!_sim.MistAt.HasValue) return;
            Vector3 at = W(_sim.MistAt.Value);
            float r = SurvivorSim.MistRadius;
            float fade = Mathf.Clamp01(_sim.MistLeft / 0.6f) * Mathf.Clamp01((SurvivorSim.MistTime - _sim.MistLeft) / 0.4f);
            _groundGlow.Put(at, r * 2.1f, 0f, new Color(0.9f, 0.95f, 1f, 0.25f * fade));
            _auras.Put(at, r * 2f, 0f, new Color(1f, 1f, 1f, 0.5f * fade));
            for (int k = 0; k < 8; k++)
            {
                float a = (k * 0.8f) + (_time * (0.15f + (0.03f * k)));
                float breathe = 1f + (0.08f * Mathf.Sin((_time * 1.3f) + k));
                Vector3 o = new Vector3(Mathf.Cos(a) * r * 0.55f, Mathf.Sin(a) * r * 0.4f, 0f);
                _cloud.Put(at + o + Up(0.8f), r * 0.9f * breathe, k * 45f + (_time * 8f), new Color(1f, 1f, 1f, 0.35f * fade), DiscSprite(), 0.8f);
            }
            for (int k = 0; k < 10; k++)
            {
                var p = at + new Vector3(Random.Range(-r, r) * 0.9f, Random.Range(-r, r) * 0.7f, 0f) + Up(1.5f);
                EmitFalling("Effects/water_drop", p, new Vector3(0f, -0.5f, 0f), 0.5f, 0.2f, new Color(0.8f, 0.92f, 1f, 0.7f * fade));
            }
            if (Random.value < 0.4f * fade) Steam(at + new Vector3(Random.Range(-r, r) * 0.6f, Random.Range(-r, r) * 0.5f, 0f), 1, 0.5f);
        }

        /// <summary>불꽃 가판대 로켓: 포물선으로 솟아 떨어지며 금색 꼬리를 끈다.</summary>
        private void DrawRockets()
        {
            foreach (Rocket r in _sim.Rockets)
            {
                float t = Mathf.Clamp01(r.Age / r.Life);
                Vector3 ground = Vector3.Lerp(W(r.From), W(r.Target), t);
                float lift = 7f * Mathf.Sin(t * Mathf.PI);
                Vector3 at = ground + Up(lift);
                _shadows.Put(ground, 0.5f + (0.3f * (1f - lift / 7f)), 0f, new Color(0f, 0f, 0f, 0.25f));
                _enemyCore.Put(at, 0.55f, 0f, new Color(1f, 0.85f, 0.4f, 0.95f));
                _enemyGlow.Put(at, 1.6f, 0f, new Color(1f, 0.6f, 0.2f, 0.35f));
                for (int k = 0; k < 3; k++)
                {
                    Emit(Sparks[Random.Range(0, Sparks.Length)], at, new Vector3(Random.Range(-1f, 1f), Random.Range(-2.5f, -0.5f), 0f), 2f, 0.35f, 0.3f, 0.05f, new Color(1f, 0.9f, 0.5f), new Color(1f, 0.4f, 0.1f, 0f), 0f, true);
                }
            }
        }

        /// <summary>항구 노란 둘. 소방정: 바다 줄 예고 띠 + 흰 선체 모델 + 남쪽으로 두 물줄기 + 사이렌 + 뒤 물결. 큰 파도: 폭 60 흰·파랑 띠가 남쪽으로 쓸며 거품·젖은 자국을 남기고 부두선에서 충격파.</summary>
        /// <summary>
        /// 스테이지 모델의 빛(맵 특색): 야시장 점포 "Sign"은 색 네온(숨 쉬듯, 타거나 그을리면 꺼진다), 공연 무대 "Spot0..3"은 바닥에 4색 스포트라이트가
        /// 좌우로 스윕(대화재·불이 나면 빨간 깜빡임), 항구 등대 "Lamp"는 돌며 바다 쪽으로 빔 한 줄.
        /// </summary>
        private void DrawModelLights(Structure st, int seed, float burnt)
        {
            GameObject model = _structModels[seed];
            if (model == null || st.Collapsed) return;
            string kind = model.name;
            // 이발소·미용실 회전등: 띠가 올라가 보이게 돈다(무너지면 위에서 return).
            if (kind == "Barber" || kind == "Salon")
            {
                foreach (Transform child in model.transform)
                {
                    Transform pole = child.name == "Pole" ? child : child.name == "PoleMount" ? child.Find("Pole") : null;
                    if (pole != null) pole.localRotation = Quaternion.Euler(0f, _time * 140f, 0f);
                }
            }
            if (kind == "Stall")
            {
                Transform sign = model.transform.Find("Sign");
                if (sign == null) return;
                Color neon = StageModels.StallPalette[seed % StageModels.StallPalette.Length];
                float on = st.Burning || burnt > 0.3f ? 0f : 1f;
                float breath = 1f + (0.1f * Mathf.Sin((_time * 2.2f) + seed));
                Vector3 at = sign.position + (Billboard * new Vector3(0f, 0f, -0.05f));
                _siren.Put(at, 1.6f * breath * on, 0f, new Color(neon.r, neon.g, neon.b, 0.75f));
                _groundGlow.Put(new Vector3(st.Pos.X, st.Pos.Y - st.Half.Y - 0.6f, 0f), 2.4f * on, 0f, new Color(neon.r, neon.g, neon.b, 0.18f));
            }
            else if (kind == "Stage")
            {
                bool alarm = st.Burning || _sim.Finale;
                Color[] hues = { new Color(1f, 0.3f, 0.7f), new Color(0.3f, 0.85f, 1f), new Color(1f, 0.85f, 0.3f), new Color(0.5f, 1f, 0.4f) };
                for (int k = 0; k < 4; k++)
                {
                    Transform spot = model.transform.Find("Spot" + k);
                    if (spot == null) continue;
                    float sweep = Mathf.Sin((_time * 0.7f) + (k * 1.6f)) * 4f;
                    // 객석(무대 앞 광장)은 무대 북쪽: 빛이 그쪽 바닥을 쓴다.
                    Vector3 foot = new Vector3(st.Pos.X + ((k - 1.5f) * st.Half.X * 0.45f) + sweep, st.Pos.Y + st.Half.Y + 3f, 0f);
                    Color c = alarm ? (Mathf.Sin((_time * 8f) + k) > 0f ? new Color(1f, 0.2f, 0.1f) : new Color(0.4f, 0.05f, 0.02f)) : hues[k];
                    _groundGlow.Put(foot, 5f, 0f, new Color(c.r, c.g, c.b, 0.35f), null, 0.6f);
                    Vector3 head = spot.position;
                    Vector3 d = foot - head;
                    float len = d.magnitude;
                    if (len > 0.5f) _band.Put((head + foot) * 0.5f, 0.5f, (Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg) - 90f, new Color(c.r, c.g, c.b, 0.15f), null, len / 0.5f);
                }
            }
            else if (kind == "LighthouseCafe")
            {
                Transform lamp = model.transform.Find("Lamp");
                if (lamp == null) return;
                float deg = _time * 60f;
                lamp.localRotation = Quaternion.Euler(0f, deg, 0f);
                Vector3 head = lamp.position;
                var dir = new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Abs(Mathf.Sin(deg * Mathf.Deg2Rad)) + 0.2f, 0f).normalized;
                _siren.Put(head, 1.2f + (0.3f * Mathf.Abs(Mathf.Sin(deg * Mathf.Deg2Rad))), 0f, new Color(1f, 0.95f, 0.7f, 0.8f));
                _band.Put(head + (dir * 5f), 1.2f, (Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg) - 90f, new Color(1f, 0.95f, 0.7f, 0.12f), null, 10f / 1.2f);
            }
        }

        /// <summary>숲 불 전선: 폭 60 검붉은 띠가 내려오고 그 위에 불꽃 벽이 선다. 꺼 놓은 자리(Out)는 김만 오르고 벽이 비어 "끊었다"가 보인다.</summary>
        private void DrawFront()
        {
            float y = _sim.FrontY.Value;
            Vector3 mid = W(new Vec2(SurvivorSim.ArenaSize / 2f, y));
            float beat = 1f + (0.08f * Mathf.Sin(_time * 5f));
            // 띠는 -90° 규칙(0°면 세로로 선다).
            _band.Put(mid, 2.2f * beat, -90f, new Color(0.6f, 0.08f, 0.02f, 0.25f), null, SurvivorSim.ArenaSize / 2.2f);
            _band.Put(mid, 0.5f, -90f, new Color(1f, 0.45f, 0.1f, 0.5f), null, SurvivorSim.ArenaSize / 0.5f);
            for (int k = 0; k < 24; k++)
            {
                float x = 1.25f + (k * SurvivorSim.FrontGap);
                var p = new Vec2(x, y);
                bool cut = false;
                foreach (Puddle pd in _sim.FrontRowPuddles)
                {
                    if (pd.Out && Mathf.Abs(pd.Pos.X - x) < 0.5f) cut = true;
                }
                Vector3 at = W(p);
                if (cut)
                {
                    if (Random.value < 0.05f) Steam(at, 1, 0.6f);
                    continue;
                }
                float flick = 0.85f + (0.25f * Mathf.Sin((_time * (9f + (k % 4))) + (k * 1.3f)));
                _groundGlow.Put(at, 2.6f * flick, 0f, new Color(1f, 0.35f, 0.08f, 0.3f));
                _blazes.Put(at + new Vector3(0f, 0.1f, 0f), 1.9f * flick, 0f, Color.white, FlameArt.Frame(_blazeSheet, _time, k + 40));
                if (Random.value < 0.03f)
                {
                    Emit(Smokes[Random.Range(0, Smokes.Length)], at + new Vector3(0f, 0.8f, 0f), new Vector3(Random.Range(-0.3f, 0.3f), 1.4f, 0f), 0.5f, 1.4f,
                        0.7f, 2f, new Color(0.22f, 0.18f, 0.16f, 0.5f), new Color(0.2f, 0.2f, 0.2f, 0f), Random.Range(-60f, 60f));
                }
            }
        }

        private void DrawHarborSpecials()
        {
            // 떠 있는 불배마다: 가장 가까운 부두 끝에 청록 고리 "요격 지점", 배 위에 "N초 뒤 접안"(5초 밑이면 빨갛게).
            foreach (Structure b in _sim.Structures)
            {
                if (b.Kind != StructureKind.Boat || b.Collapsed || b.Docked || !b.Burning || b.Tanker) continue;
                Vec2 tip = PierTipFor(b.Pos);
                Vector3 tipAt = W(tip);
                float pulse = 1f + (0.1f * Mathf.Sin(_time * 5f));
                _reticle.Put(tipAt + Up(0.05f), 2.8f * pulse, -_time * 50f, new Color(0.4f, 0.95f, 1f, 0.6f));
                _groundGlow.Put(tipAt, 3.2f, 0f, new Color(0.4f, 0.9f, 1f, 0.2f));
                Tag(tipAt + Up(0.4f) + new Vector3(0f, -1.6f, 0f), "요격 지점", new Color(0.75f, 1f, 1f), 0.045f);
                float eta = _sim.BoatEta(b);
                bool soon = eta < 5f;
                Tag(W(b.Pos) + Up(1.6f) + new Vector3(0f, 0.9f, 0f), Mathf.CeilToInt(eta) + "초 뒤 접안", soon && Mathf.Sin(_time * 10f) > 0f ? Color.white : soon ? new Color(1f, 0.45f, 0.35f) : new Color(1f, 0.85f, 0.6f), 0.045f);
            }
            if (_sim.Fireboat.HasValue)
            {
                Vector3 at = W(_sim.Fireboat.Value);
                float dir = _sim.FireboatDir;
                var sea = new Color(0.35f, 0.75f, 1f);
                if (!_fireboatShown)
                {
                    SpecialBanner("소방정 출동!", sea);
                    GameAudio.Play(Cue.Critical);
                }
                float ahead = 16f;
                _band.Put(at + new Vector3(dir * ahead * 0.5f, 0f, 0f), 3f, -90f, new Color(0.4f, 0.75f, 1f, 0.16f + (0.05f * Mathf.Sin(_time * 10f))), null, ahead / 3f);
                var heading = new Vector3(dir, 0f, 0f);
                _shadows.Put(at + new Vector3(0.15f, -0.3f, 0f), 2.6f, 0f, new Color(0f, 0f, 0f, 0.3f), null, 0.5f);
                GameObject boat = _fireboatModels.Get();
                if (boat != null) ItemModels.Place(boat, at, 0.06f * Mathf.Sin(_time * 3f), heading, 1.15f);
                bool flip = Mathf.Repeat(_time * 6f, 1f) < 0.5f;
                _siren.Put(at + Up(1.2f), 2.6f, 0f, flip ? new Color(0.3f, 0.5f, 1f, 0.95f) : new Color(1f, 0.2f, 0.15f, 0.95f));
                _groundGlow.Put(at, 7f, 0f, flip ? new Color(0.3f, 0.5f, 1f, 0.22f) : new Color(1f, 0.2f, 0.15f, 0.22f));
                // 물대포 둘: 부두 쪽(남쪽)으로 길게 뿜는다.
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 hand = at + new Vector3(side * 0.45f, -0.3f, 0f) + Up(0.9f);
                    Vector3 tip = at + new Vector3((side * 2f) - (dir * 2.5f), -SurvivorHarbor.FireboatSpray, 0f);
                    SmallRibbon(hand, tip, 0.6f, new Color(0.75f, 0.95f, 1f, 0.95f), 0.5f);
                    Splash(tip, 2, 0.6f);
                    for (int k = 0; k < 2; k++)
                    {
                        var v = new Vector3((side * Random.Range(1f, 3f)) - (dir * Random.Range(1f, 3f)), -Random.Range(9f, 13f), 0f);
                        Emit("Effects/water_drop", hand, v, 3f, 0.4f, 0.4f, 0.12f, new Color(0.75f, 0.95f, 1f, 1f), new Color(0.6f, 0.9f, 1f, 0f), 0f);
                    }
                }
                // 뒤 물결: 흰 거품이 바다에 남는다.
                if (Random.value < 0.7f) AddWet(at - (heading * 1.4f) + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.5f, 0.5f), 0f), 1.6f, 2f, false);
                if (Random.value < 0.5f) Emit("Effects/smoke_01", at - (heading * 1.6f), -heading * 1f, 1.5f, 0.7f, 0.5f, 1.2f, new Color(1f, 1f, 1f, 0.55f), new Color(1f, 1f, 1f, 0f), 0f);
                _trauma = Mathf.Max(_trauma, 0.06f);
            }
            _fireboatShown = _sim.Fireboat.HasValue;

            if (_sim.WaveY.HasValue)
            {
                float y = _sim.WaveY.Value;
                float mid = SurvivorSim.ArenaSize / 2f;
                float width = SurvivorSim.ArenaSize + 4f;
                var crest = new Vector3(mid, y, 0f);
                // 두 겹 띠: 앞머리 흰 거품, 뒤로 넓은 파란 물마루(띠는 -90°로 눕혀 x로 늘인다: 소방차 차선과 같은 규칙).
                _band.Put(crest + new Vector3(0f, 1.6f, 0f), 3.5f, -90f, new Color(0.35f, 0.62f, 0.95f, 0.5f), null, width / 3.5f);
                _band.Put(crest, 1.2f, -90f, new Color(1f, 1f, 1f, 0.9f), null, width / 1.2f);
                _band.Put(crest + new Vector3(0f, 0.9f, 0f), 0.5f, -90f, new Color(0.8f, 0.95f, 1f, 0.8f), null, width / 0.5f);
                _groundGlow.Put(crest + new Vector3(0f, 1f, 0f), 6f, -90f, new Color(0.6f, 0.85f, 1f, 0.25f), null, width / 6f);
                for (int k = 0; k < 20; k++)
                {
                    float x = Random.Range(0f, SurvivorSim.ArenaSize);
                    Emit("Effects/smoke_01", new Vector3(x, y + Random.Range(-0.3f, 0.5f), 0f) + Up(0.4f), new Vector3(Random.Range(-0.5f, 0.5f), -Random.Range(2f, 5f), 0f), 2f, 0.45f, 0.5f, 1.3f,
                        new Color(1f, 1f, 1f, 0.75f), new Color(1f, 1f, 1f, 0f), Random.Range(-90f, 90f));
                }
                for (int k = 0; k < 6; k++) Splash(new Vector3(Random.Range(0f, SurvivorSim.ArenaSize), y, 0f), 1, 0.8f);
                for (int k = 0; k < 4; k++) AddWet(new Vector3(Random.Range(0f, SurvivorSim.ArenaSize), y + Random.Range(1.5f, 4f), 0f), Random.Range(1.5f, 3f), 4f, false);
                if (!_waveHitQuay && y <= SurvivorHarbor.SeaFrom + 0.5f)
                {
                    // 부두선에 부딪히는 순간: 폭 전체 충격파와 큰 물보라.
                    _waveHitQuay = true;
                    Shockwave(new Vector3(mid, SurvivorHarbor.SeaFrom, 0f), new Color(0.9f, 0.97f, 1f, 0.9f), SurvivorSim.ArenaSize, 0.6f);
                    for (int k = 0; k < 40; k++)
                    {
                        float x = Random.Range(0f, SurvivorSim.ArenaSize);
                        EmitFalling("Effects/water_drop", new Vector3(x, SurvivorHarbor.SeaFrom, 0f) + Up(0.3f), new Vector3(Random.Range(-2f, 2f), Random.Range(0f, 3f), 0f), 0.7f, 0.45f, new Color(0.8f, 0.95f, 1f, 1f));
                    }
                    _trauma = Mathf.Min(1f, _trauma + 0.3f);
                    PlaySplash();
                }
                _trauma = Mathf.Max(_trauma, 0.12f);
            }
            else
            {
                _waveHitQuay = false;
            }
        }

        /// <summary>불배(판 중간에 생기는 구조물이라 모델 풀에서 꺼낸다): 뱃머리는 떠가는 쪽, 닿았으면 남쪽. 타면 갑판 불꽃·연기, 뒤로 물결.
        /// 아직 안 닿은 불배는 노린 건물까지 빨간 점선과 지붕 고리(예고 표식)를 단다.</summary>
        private void DrawBoat(Structure st, Vector3 at, int i)
        {
            Vector3 dir = st.Docked ? Vector3.down : st.Drift.X != 0f || st.Drift.Y != 0f ? new Vector3(st.Drift.X, st.Drift.Y, 0f).normalized : Vector3.down;
            float bob = (st.Tanker ? 0.03f : 0.06f) * Mathf.Sin((_time * 2.2f) + i);
            _shadows.Put(at + new Vector3(0.15f, -0.3f, 0f), st.Tanker ? 6.4f : 2.6f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, new Color(0f, 0f, 0f, 0.3f), null, 0.5f);
            // 유조선(항구 대화재)은 제 모델 풀에서, 불배는 불배 풀에서.
            GameObject model = st.Tanker ? _tankerModels.Get() : _boatModels.Get();
            if (model != null)
            {
                // 불배는 1.4배: 멀리서도 "배"로 읽힌다(판정은 그대로).
                ItemModels.Place(model, at, bob, dir, st.Tanker ? 1f : 1.4f);
                float burnt = 1f - Mathf.Clamp01(st.Integrity);
                Models3D.Tint(model, Color.Lerp(Color.white, new Color(0.25f, 0.2f, 0.2f), burnt));
            }
            // 뒤 물결: 떠가는 동안 뱃고물에서 흰 거품이 퍼진다.
            if (!st.Docked && Random.value < 0.5f) AddWet(at - (dir * 1.1f) + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f), 0f), 1.2f, 1.5f, false);
            if (!st.Docked && Random.value < 0.3f) Emit("Effects/smoke_01", at - (dir * 1.3f), -dir * 0.6f, 1.5f, 0.6f, 0.4f, 0.9f, new Color(1f, 1f, 1f, 0.5f), new Color(1f, 1f, 1f, 0f), 0f);
            if (st.Burning)
            {
                float f = st.Fire;
                float hull = st.Tanker ? 2.4f : 1f;
                _roofGlow.Put(at + Up(0.5f), 2.4f * hull * (1f + (0.6f * f)), 0f, new Color(1f, 0.35f, 0.08f, 0.25f + (0.3f * f)));
                int n = (st.Tanker ? 3 : 1) + Mathf.RoundToInt(f * 2f);
                for (int k = 0; k < n; k++)
                {
                    Vector3 deck = at + (dir * ((k - ((n - 1) / 2f)) * 0.6f * hull)) + Up(0.55f);
                    float flick = 0.85f + (0.2f * Mathf.Sin((_time * (11f + k)) + (k * 1.9f)));
                    _roofFire.Put(deck, (0.9f + (1.2f * f)) * flick * 0.8f, 0f, Color.white, FlameArt.Frame(_blazeSheet, _time, i + (k * 5), 10f + k));
                }
                if (Random.value < 0.08f + (0.15f * f))
                {
                    Emit(Smokes[Random.Range(0, Smokes.Length)], at + Up(0.8f), new Vector3(Random.Range(0.2f, 0.9f), Random.Range(1.5f, 2.6f), 0f), 0.3f,
                        Random.Range(1.6f, 2.4f), 0.8f + f, 2.6f + (2f * f), new Color(0.2f, 0.18f, 0.18f, 0.5f), new Color(0.25f, 0.24f, 0.24f, 0f), Random.Range(-60f, 60f));
                }
                if (st.Docked)
                {
                    // 닿은 불배: 부두선 아래 바닥이 빨갛게 맥박친다(여기서 불이 옮는다).
                    float beat = 0.5f + (0.5f * Mathf.Abs(Mathf.Sin(_time * 5f)));
                    _groundGlow.Put(at + new Vector3(0f, -1.6f, 0f), 5f, 0f, new Color(1f, 0.3f, 0.1f, 0.18f + (0.2f * beat)));
                }
                else if (st.Target != null && !st.Target.Collapsed)
                {
                    // 예고: 노린 건물까지 빨간 점선과 지붕 고리.
                    Vector3 to = W(st.Target.Pos);
                    Vector3 d = to - at;
                    float len = d.magnitude;
                    if (len > 1f)
                    {
                        float deg = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                        int dashes = Mathf.Max(1, (int)(len / 1.2f));
                        for (int k = 0; k < dashes; k++)
                        {
                            float t = (k + 0.5f + Mathf.Repeat(_time * 0.6f, 1f)) / dashes;
                            if (t > 1f) continue;
                            _band.Put(at + (d * t), 0.3f, deg - 90f, new Color(1f, 0.3f, 0.2f, 0.3f), null, 0.6f / 0.3f);
                        }
                    }
                    float pulse = 1f + (0.15f * Mathf.Sin(_time * 6f));
                    _reticle.Put(to + Up(0.05f), Mathf.Max(st.Target.Half.X, st.Target.Half.Y) * 2.4f * pulse, _time * 60f, new Color(1f, 0.3f, 0.2f, 0.55f));
                }
            }
        }

        private void DrawBuilding(Structure st, Vector3 at, float w, float h, float burnt, float wet, int seed)
        {
            if (st.Collapsed)
            {
                // 무너진 자리: 잿더미와 검은 잔해 몇 덩이, 가는 연기.
                _roofEdges.Put(at, w + 0.2f, 0f, new Color(0.1f, 0.09f, 0.09f), null, (h + 0.2f) / (w + 0.2f));
                for (int k = 0; k < 5; k++)
                {
                    float ox = (Hash01(seed * 7 + k) - 0.5f) * w * 0.8f;
                    float oy = (Hash01(seed * 13 + k) - 0.5f) * h * 0.7f;
                    _roofs.Put(at + new Vector3(ox, oy, 0f), 0.5f + (0.5f * Hash01(seed + k)), Hash01(k + seed * 3) * 90f, new Color(0.18f, 0.16f, 0.15f));
                }
                if (Random.value < 0.03f)
                {
                    Emit(Smokes[Random.Range(0, Smokes.Length)], at, new Vector3(0.4f, 1f, 0f), 0.3f, 2.2f, 0.8f, 2.4f,
                        new Color(0.3f, 0.3f, 0.3f, 0.35f), new Color(0.3f, 0.3f, 0.3f, 0f), Random.Range(-40f, 40f));
                }
                return;
            }

            // 주택 모델: 탈수록 검게 그을리고, 불빛에 붉게 일렁이고, 젖으면 파랗게 번들거린다.
            ShopArt.Look look = ShopArt.For(st.Name, w, h);
            Color tint = Color.Lerp(Color.white, new Color(0.2f, 0.16f, 0.15f), Mathf.Pow(burnt, 0.7f));
            if (st.Burning) tint = Color.Lerp(tint, new Color(1f, 0.6f, 0.4f), 0.15f * st.Fire * (0.7f + (0.3f * Mathf.Sin(_time * 9f + seed))));
            if (wet > 0f) tint = Color.Lerp(tint, new Color(0.65f, 0.82f, 1f), 0.3f * wet);
            float hgt = RoofHeight(seed);
            Models3D.Tint(_structModels[seed], tint);
            DrawModelLights(st, seed, burnt);

            // 불이 나면 앞벽 창 자리(가운데 줄 셋)가 주황으로 일렁인다.
            if (st.Burning)
            {
                for (int k = 0; k < 3; k++)
                {
                    Vector3 c = WindowPoint(seed, k);
                    float flick = 0.5f + (0.5f * Mathf.Sin((_time * 13f) + seed + k));
                    _roofTrim.PutRot(c, Facade, 0.45f, 0.35f, Color.Lerp(new Color(1f, 0.45f, 0.1f, 0.85f), new Color(1f, 0.8f, 0.35f, 0.9f), flick * st.Fire));
                    _roofGlow.PutRot(c + new Vector3(0f, -0.02f, 0f), Facade, 0.8f, 0.8f, new Color(1f, 0.5f, 0.12f, 0.25f + (0.35f * st.Fire)));
                }
            }
            else if (_sim.Stage.Number == 5 && StageModels.SteamAt(st.Name, out bool smoky) is Vector3 spot && Random.value < 0.06f)
            {
                // 야시장 먹거리 점포: 철판·냄비·숯불 위로 흰 김(꼬치구이는 회색 연기).
                var from = new Vector3(st.Pos.X + spot.x, st.Pos.Y - spot.z, -spot.y);
                Color c = smoky ? new Color(0.6f, 0.6f, 0.62f, 0.5f) : new Color(1f, 1f, 1f, 0.5f);
                Emit(Smokes[Random.Range(0, Smokes.Length)], from, new Vector3(Random.Range(-0.2f, 0.3f), Random.Range(0.6f, 1f), 0f), 0.4f, Random.Range(1.2f, 1.8f),
                    0.3f, 1.1f, c, new Color(c.r, c.g, c.b, 0f), Random.Range(-40f, 40f));
            }
            else if (look.Steam.HasValue && Random.value < 0.025f)
            {
                // 굴뚝·배기구·솥에서 가끔 흰 김이 오른다: 사람이 사는 가게.
                Vector3 from = RoofPoint(at, h, hgt, look.Steam.Value);
                Emit(Smokes[Random.Range(0, Smokes.Length)], from, new Vector3(Random.Range(0.1f, 0.4f), Random.Range(0.6f, 1f), 0f), 0.4f, Random.Range(1.2f, 1.8f),
                    0.25f, 0.9f, new Color(1f, 1f, 1f, 0.45f), new Color(1f, 1f, 1f, 0f), Random.Range(-40f, 40f));
            }

            if (st.Burning) DrawRoofFire(st, at, w, h, seed, hgt);

            // 타는 동안은 위에 진압 게이지(파란 물이 차오른다 = 1 − 불 세기, 다 차면 꺼진다), 그 밑에 증기 충전 금,
            // 맨 밑에 얇은 마감 바(언제까지 꺼야 하나: 사람이 있으면 첫 사람을 잃기까지, 없으면 무너지기까지 ÷ 30초).
            // 꺼진 뒤 손상이 남았으면 튼튼함 막대(초록→빨강). 모두 카메라를 보고 선다.
            float bw = w * 0.8f;
            Vector3 bar = at + new Vector3(0f, st.Half.Y * 0.6f, 0f) + Up(hgt + 0.9f);
            if (st.Burning)
            {
                Vector3 front = Billboard * new Vector3(0f, 0f, -0.01f);
                float put = Mathf.Clamp01(1f - st.Fire);
                _bars.PutRot(bar, Billboard, bw + 0.08f, 0.3f, new Color(0f, 0f, 0f, 0.8f));
                _bars.PutRot(bar + new Vector3(-(bw * (1f - put)) / 2f, 0f, 0f) + front, Billboard, Mathf.Max(0.01f, bw * put), 0.22f,
                    Color.Lerp(new Color(0.3f, 0.6f, 1f), new Color(0.55f, 0.9f, 1f), put));
                // 안 찬 자리는 불빛으로 일렁인다: "아직 이만큼 탄다". 한 방 물(증기·물폭탄)이 들어오면 한 칸이 확 찬다.
                float rest = bw * (1f - put);
                if (rest > 0.02f) _bars.PutRot(bar + new Vector3((bw * put) / 2f, 0f, 0f) + front, Billboard, rest, 0.22f,
                    new Color(1f, 0.45f + (0.2f * Mathf.Sin((_time * 9f) + seed)), 0.15f, 0.9f));
                // 마감: 얇은 바(초록→빨강), 5초 밑이면 깜빡. 글자는 게이지 안 한 줄("30%", 10초 밑이면 "30% · 7초 · 2명" — 밑엔 "살려줘!" 말풍선이 있다).
                float left = _sim.Deadline(st);
                float fill = Mathf.Clamp01(left / DeadlineShown);
                bool urgent = left < 10f;
                string label = Mathf.RoundToInt(put * 100f) + "%" + (urgent ? " · " + Mathf.CeilToInt(left) + "초" + (st.Residents > 0 ? " · " + st.Residents + "명" : "") : "");
                Tag(bar + (Billboard * new Vector3(0f, 0f, -0.03f)), label, urgent ? new Color(1f, 0.75f, 0.65f) : Color.white, 0.032f);
                // 증기 충전: 게이지 바로 밑 흰 금. 다 차면 증기 폭발로 한 칸이 찬다.
                float charge = Mathf.Clamp01(st.HoseHold / SurvivorSim.SteamHold);
                if (charge > 0.02f) _bars.PutRot(bar + Up(-0.2f) + new Vector3(-(bw * (1f - charge)) / 2f, 0f, 0f) + front, Billboard, bw * charge, 0.07f, new Color(1f, 1f, 1f, 0.85f));
                Vector3 dl = bar + Up(-0.34f);
                bool blink = left < 5f && Mathf.Sin(_time * 10f) < 0f;
                _bars.PutRot(dl, Billboard, bw + 0.08f, 0.14f, new Color(0f, 0f, 0f, 0.7f));
                _bars.PutRot(dl + new Vector3(-(bw * (1f - fill)) / 2f, 0f, 0f) + front, Billboard, Mathf.Max(0.01f, bw * fill), 0.09f,
                    blink ? Color.white : Color.Lerp(new Color(1f, 0.2f, 0.1f), new Color(0.45f, 0.95f, 0.4f), fill));
            }
            else if (st.Integrity < 0.999f)
            {
                _bars.PutRot(bar, Billboard, bw + 0.08f, 0.22f, new Color(0f, 0f, 0f, 0.7f));
                float fill = Mathf.Clamp01(st.Integrity);
                _bars.PutRot(bar + new Vector3(-(bw * (1f - fill)) / 2f, 0f, 0f) + (Billboard * new Vector3(0f, 0f, -0.01f)), Billboard, Mathf.Max(0.01f, bw * fill), 0.14f,
                    Color.Lerp(new Color(1f, 0.25f, 0.15f), new Color(0.45f, 0.95f, 0.4f), fill));
            }
        }

        /// <summary>누운 가게 그림의 지붕 부분 좌표를 입체 지붕(발자국 전체, 높이 hgt) 위 점으로.</summary>
        private static Vector3 RoofPoint(Vector3 at, float h, float hgt, Vector2 local)
        {
            float bottom = -(h / 2f) + hgt;
            float y = -(h / 2f) + ((local.y - bottom) * h / Mathf.Max(0.01f, h - hgt));
            return new Vector3(at.x + local.x, at.y + y, -hgt - 0.02f);
        }

        /// <summary>
        /// 불난 가게에 갇힌 사람: 창문에서 얼굴이 흔들리고, 머리 위에 "살려줘!", 문 앞에 초록 원이 뜬다.
        /// 문 앞에 서 있으면 바깥 원이 조여 들며 구조가 차오른다. 큰 불 연기가 차면 말풍선이 빨갛게 급해진다.
        /// </summary>
        private void DrawTrapped(Structure st, TextMesh help, int seed)
        {
            bool trapped = st.Burning && st.Residents > 0;
            help.gameObject.SetActive(trapped);
            if (!trapped) return;

            var at = new Vector3(st.Pos.X, st.Pos.Y, 0f);
            float bob = Mathf.Abs(Mathf.Sin((_time * 7f) + seed));
            float hgt = RoofHeight(seed);
            // 갇힌 사람은 지붕 앞 가장자리에 서서 좌우로 몸을 뒤집으며 손을 흔들고, 첫 창은 불빛으로 번쩍인다.
            Vector3 win = WindowPoint(seed, 0) + new Vector3(0f, -0.06f, 0f);
            float front = seed < _structSize.Length && _structSize[seed].y > 0f ? _structSize[seed].y / 2f : st.Half.Y;
            _roofGlow.Put(win, 0.9f, 0f, new Color(1f, 0.6f, 0.2f, 0.4f + (0.3f * bob)));
            bool wave = Mathf.Repeat((_time * 4f) + seed, 2f) < 1f;
            // 앞 가장자리에 서면 간판에 가려 지붕 가운데 조금 뒤에 세운다.
            Vector3 edge = at + new Vector3(win.x - at.x, front * 0.15f, 0f) + Up(hgt * 0.8f + (0.08f * bob));
            GameObject waving = _people.Get(CivilianModel(seed));
            if (waving != null)
            {
                // 카메라 쪽을 보고 두 팔을 번쩍 들며 구해 달라고 손짓한다.
                Models3D.Pose(waving, edge, Vector3.down);
                Models3D.Play(waving, "Victory", 1.2f, _time + seed);
                Models3D.Tint(waving, Color.white, CivilianColor(seed), 10 + CivilianKind(seed));
            }

            bool choking = st.Fire >= SurvivorSim.SmokeFire;
            float urgent = choking ? Mathf.Clamp01(st.Smoke / SurvivorSim.SmokeTime) : 0f;
            bool big = st == _sim.BigReport || st == _sim.Landmark;
            help.text = big ? "대형 화재! " + st.Residents + "명" : "살려줘!" + (st.Residents > 1 ? " ×" + st.Residents : "");
            if (big)
            {
                // 갇힌 사람 수만큼 얼굴이 지붕 위에 줄지어 흔들리고, 둘레가 붉게 맥동한다.
                float gap = 0.62f;
                float left = -((st.Residents - 1) * gap) / 2f;
                for (int k = 0; k < st.Residents; k++)
                {
                    float wob = Mathf.Sin((_time * 8f) + k) * 0.06f;
                    GameObject crowd = _people.Get(CivilianModel(seed + k), 0.7f);
                    if (crowd == null) continue;
                    Models3D.Pose(crowd, at + new Vector3(left + (k * gap), -st.Half.Y * 0.5f, 0f) + Up(hgt + 0.05f + wob), Vector3.down);
                    Models3D.Play(crowd, "Victory", 1.2f, _time + k);
                    Models3D.Tint(crowd, Color.white, CivilianColor(seed + k), 10 + CivilianKind(seed + k));
                }
                _civilianRings.Put(at, Mathf.Max(st.Half.X, st.Half.Y) * (3f + (0.4f * Mathf.Sin(_time * 5f))), 0f, new Color(1f, 0.2f, 0.1f, 0.35f));
            }
            bool blink = choking && Mathf.Sin(_time * (8f + (16f * urgent))) > 0f;
            help.color = blink ? new Color(1f, 0.35f, 0.3f) : Color.white;
            // 곧 무너진다: 라벨이 초읽기로 바뀌고 빨갛게 뛰며, 둘레에 붉은 고리.
            float fall = _sim.TimeToFall(st);
            bool nearFall = fall <= SurvivorSim.CollapseWarnAt;
            if (nearFall)
            {
                help.text = "무너진다 " + Mathf.CeilToInt(fall) + "초 · " + st.Residents + "명";
                help.color = Color.Lerp(new Color(1f, 0.2f, 0.1f), Color.white, 0.5f + (0.5f * Mathf.Sin(_time * 12f)));
                _civilianRings.Put(at, Mathf.Max(st.Half.X, st.Half.Y) * (2.6f + (0.3f * Mathf.Sin(_time * 12f))), 0f, new Color(1f, 0.15f, 0.05f, 0.5f));
            }
            help.transform.localPosition = at + Up(hgt + (big ? 1.7f : 1.5f) + (0.15f * bob));
            help.characterSize = 0.06f * (1f + (0.12f * bob) + (nearFall ? 0.15f : 0f));

            Vector3 door = W(st.Door);
            float pulse = 1.5f + (0.25f * Mathf.Sin(_time * 6f));
            _civilianRings.Put(door, pulse * 1.3f, 0f, new Color(0.4f, 1f, 0.4f, 0.55f));
            _reticle.Put(door, SurvivorSim.RescueRange * 2f, 0f, new Color(0.5f, 1f, 0.5f, 0.8f));
            if (st.RescueHold > 0f)
            {
                float t = Mathf.Clamp01(st.RescueHold / SurvivorSim.RescueTime);
                _reticle.Put(door, Mathf.Lerp(SurvivorSim.RescueRange * 2.6f, 0.5f, t), 0f, new Color(0.8f, 1f, 0.6f, 0.95f));
                _civilianRings.Put(door, 2.5f * t, 0f, new Color(0.6f, 1f, 0.5f, 0.6f * t));
            }
        }

        /// <summary>타는 구조물 위 불꽃(세기만큼 많고 크게) + 밑빛 + 연기 기둥.</summary>
        private void DrawRoofFire(Structure st, Vector3 at, float w, float h, int seed, float baseZ)
        {
            float f = st.Fire;
            float area = Mathf.Max(w, h);
            at += Up(baseZ + 0.02f);
            _roofGlow.Put(at, area * (1f + (0.7f * f)), 0f, new Color(1f, 0.35f, 0.08f, 0.25f + (0.3f * f)));
            int n = st.IsBuilding ? 2 + Mathf.RoundToInt(f * (st.Kind == StructureKind.Depot ? 9f : 6f)) : 1 + Mathf.RoundToInt(f * 2f);
            for (int k = 0; k < n; k++)
            {
                float ox = (Hash01((seed * 31) + k) - 0.5f) * w * 0.85f;
                float oy = (Hash01((seed * 17) + (k * 5)) - 0.5f) * h * 0.75f;
                float flick = 0.85f + (0.2f * Mathf.Sin((_time * (11f + k)) + (k * 1.9f)));
                float size = (0.8f + (1.3f * f)) * flick * (st.IsBuilding ? 1f : 0.8f);
                // 지붕 위에 선 불꽃 플립북(큰 불 시트). 프레임은 불마다 다른 위상으로 돈다.
                _roofFire.Put(at + new Vector3(ox, oy, 0f), size * 0.8f, 0f, Color.white, FlameArt.Frame(_blazeSheet, _time, seed + (k * 5), 10f + k));
            }
            if (Random.value < 0.04f + (0.12f * f))
            {
                Emit(Smokes[Random.Range(0, Smokes.Length)], at + new Vector3(Random.Range(-w, w) * 0.3f, h * 0.3f, 0f), new Vector3(Random.Range(0.2f, 0.9f), Random.Range(1.5f, 2.6f), 0f), 0.3f,
                    Random.Range(1.6f, 2.4f), 0.8f + f, 2.6f + (2f * f), new Color(0.2f, 0.18f, 0.18f, 0.5f), new Color(0.25f, 0.24f, 0.24f, 0f), Random.Range(-60f, 60f));
            }
            if (Random.value < 0.08f * f)
            {
                Emit(Sparks[Random.Range(0, Sparks.Length)], at, new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(2f, 4f), 0f), 1f, 0.7f,
                    0.35f, 0.05f, new Color(1f, 0.85f, 0.35f), new Color(1f, 0.3f, 0.05f, 0f), 0f, true);
            }
        }

        private static float Hash01(int n)
        {
            unchecked
            {
                uint x = (uint)n * 747796405u + 2891336453u;
                x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
                return ((x >> 22) ^ x) / 4294967296f;
            }
        }

        private void Wall(int x, int y)
        {
            SpriteRenderer r = GroundSprite("Wall", ((x + y) & 1) == 0 ? "TopDown/brick_a" : "TopDown/brick_b", 2);
            r.transform.localPosition = new Vector3(x + 0.5f, y + 0.5f, 0.05f);
            r.transform.localScale = Vector3.one * Art.FitWidth(r.sprite, 1f);
            r.color = new Color(0.32f, 0.26f, 0.25f);
        }

        private void BuildPools()
        {
            _houseShadows = new Pool(_world, "HouseShadow", Art.White, 1, null);
            _roofEdges = new Pool(_world, "RoofEdge", Art.White, 2, null);
            _roofs = new Pool(_world, "Roof", Art.White, 3, null);
            _roofTrim = new Pool(_world, "RoofTrim", Art.White, 4, null);
            _props = AddPool("Prop", "Props/tree_large", 7);
            _walls = new Pool(_world, "Wall3D", Art.White, 3, Cutout());
            _pools.Add(_walls);
            _roofGlow = AddPool("RoofGlow", "Effects/glow", 8, true);
            _roofFire = AddPool("RoofFire", "Effects/fire_02", 11);
            _bars = new Pool(_world, "Bar", Art.White, 19, null);
            _edgeArrows = new Pool(_world, "EdgeArrow", ArrowSprite(), 22, null);
            _pools.Add(_edgeArrows);
            _arrowBacks = new Pool(_world, "ArrowBack", DiscSprite(), 21, null);
            _pools.Add(_arrowBacks);
            _pools.Add(_houseShadows);
            _pools.Add(_roofEdges);
            _pools.Add(_roofs);
            _pools.Add(_roofTrim);
            _pools.Add(_bars);
            _groundGlow = AddPool("GroundGlow", "Effects/glow", 3, true);
            _shadows = AddPool("Shadow", "Effects/glow", 4);
            _motes = new Pool(_world, "Mote", Art.White, 21, null);
            _pools.Add(_motes);
            _foam = AddPool("Foam", "Effects/smoke_01", 3);
            _groundFire = AddPool("GroundFire", "Effects/fire_01", 4);
            _civilianRings = AddPool("CivilianRing", "Effects/glow", 5, true);
            _gems = new Pool(_world, "Gem", Art.White, 6, null);
            _gemCores = new Pool(_world, "GemCore", Art.White, 7, null);
            _toolboxGlow = AddPool("ToolboxGlow", "Effects/glow", 7, true);
            // 방염제 띠는 땅바닥(구슬 아래), 비구름 그림자도 땅, 소방차·먹구름·비행기는 건물 위.
            _band = new Pool(_world, "Retardant", Art.White, 3, null);
            _pools.Add(_band);
            _rainShade = AddPool("RainShade", "Effects/glow", 3);
            _siren = AddPool("Siren", "Effects/glow", 21, true);
            _cloud = AddPool("Cloud", "Effects/smoke_03", 25);
            _toolbox = new Pool(_world, "Toolbox", ToolboxSprite(), 8, Cutout());
            _chest = new Pool(_world, "Chest", ChestSprite(), 8, Cutout());
            _pools.Add(_chest);
            _kit = new Pool(_world, "Kit", KitSprite(), 8, Cutout());
            _pools.Add(_kit);
            _pools.Add(_toolbox);
            _pools.Add(_gems);
            _pools.Add(_gemCores);
            _enemyGlow = AddPool("EnemyGlow", "Effects/glow", 8, true);
            _emberSheet = FlameArt.Sheet("EmberFlame", 8, 0.72f, 0.35f, new Color(1f, 0.42f, 0.08f), new Color(1f, 0.85f, 0.35f));
            _blazeSheet = FlameArt.Sheet("BlazeFlame", 12, 0.95f, 0.3f, new Color(0.95f, 0.28f, 0.06f), new Color(1f, 0.75f, 0.25f), new Color(0.3f, 0.1f, 0.05f));
            _dartSheet = FlameArt.Sheet("DartFlame", 8, 0.45f, 0.12f, new Color(1f, 0.75f, 0.15f), new Color(1f, 1f, 0.65f));
            _oilSheet = FlameArt.Sheet("OilFlame", 8, 0.9f, 0.3f, new Color(0.8f, 0.3f, 0.8f), new Color(1f, 0.6f, 0.3f), new Color(0.12f, 0.06f, 0.14f));
            _embers = AddPool("Ember", "Effects/fire_01", 9);
            _blazes = AddPool("Blaze", "Effects/fire_02", 9);
            _darts = AddPool("Dart", "Effects/flame_05", 9, true);
            _bats = new Pool(_world, "Bat", BatSprite(), 9, null);
            _pools.Add(_bats);
            _enemyCore = AddPool("EnemyCore", "Effects/fire_01", 10, true);
            // 픽셀 3D: 원래 서 있는 그림(불 몹·박쥐·지붕 불꽃·시민·상자)은 카메라를 보고 세운다.
            foreach (Pool standing in new[] { _embers, _blazes, _darts, _bats, _enemyCore, _roofFire, _chest, _toolbox, _kit }) standing.Upright = true;
            _bombShadows = AddPool("BombShadow", "Effects/glow", 11);
            _heliShadow = new Pool(_world, "HeliShadow", HeliSprite(), 11, null);
            _sprayLines = new Pool(_world, "SprayLine", Art.White, 14, null);
            _pools.Add(_sprayLines);
            _wet = AddPool("Wet", "Effects/glow", 2);
            _airBombs = new Pool(_world, "AirBomb", AirBombSprite(), 23, null);
            _pools.Add(_airBombs);
            _pools.Add(_heliShadow);
            _droneGlow = AddPool("DroneGlow", "Effects/glow", 11, true);
            _hoseTubeEdge = new Pool(_world, "HoseTubeEdge", Art.White, 9, null);
            _hoseTube = new Pool(_world, "HoseTube", Art.White, 10, null);
            // 물줄기: 비치는 겉물(11) 아래, 몸통(12), 더해 그리는 하이라이트(13). 끝 물덩어리는 몸통과 같은 층.
            Shader sprites = Shader.Find("Sprites/Default");
            var water = new Material(sprites) { mainTexture = FlowTexture() };
            Material shine = Additive != null ? new Material(Additive) { mainTexture = FlowTexture() } : water;
            _waterSheath = new RibbonPool(_world, "WaterSheath", water, 11);
            _waterBody = new RibbonPool(_world, "WaterBody", water, 12);
            _waterShine = new RibbonPool(_world, "WaterShine", shine, 13);
            _ribbons.Add(_waterSheath);
            _ribbons.Add(_waterBody);
            _ribbons.Add(_waterShine);
            _streamJoint = new Pool(_world, "WaterBlob", DiscSprite(), 12, null);
            _blobShine = new Pool(_world, "WaterBlobShine", DiscSprite(), 13, Additive);
            _pools.Add(_streamJoint);
            _pools.Add(_blobShine);
            _streamGlint = new Pool(_world, "WaterGlint", DiscSprite(), 14, Additive);
            _rainbow = new Pool(_world, "Rainbow", RainbowSprite(), 13, null);
            _pools.Add(_streamGlint);
            _pools.Add(_rainbow);
            _nozzle = new Pool(_world, "Nozzle", Art.White, 16, null);
            _pools.Add(_hoseTubeEdge);
            _pools.Add(_hoseTube);
            _pools.Add(_nozzle);
            _reticle = new Pool(_world, "Reticle", RingSprite(), 21, null);
            _pools.Add(_reticle);
            _bubbles = new Pool(_world, "Bubble", BubbleSprite(), 14, Additive);
            _pools.Add(_bubbles);
            _auras = new Pool(_world, "Aura", RingSprite(), 10, Additive);
            _pools.Add(_auras);
            _tank = AddPool("Tank", "Effects/glow", 10, true);
            _radar = new Pool(_world, "Radar", BeamSprite(), 10, Additive);
            _pools.Add(_radar);
            _dropGlow = AddPool("DropGlow", "Effects/glow", 12, true);

            _playerGlow = NewSprite(_root, "Magnet", Art.Get("Effects/glow"), 5);
            _playerGlow.color = new Color(0.4f, 0.7f, 1f, 0.08f);
            _player = Models3D.Person("People/Worker_Male", _root, PersonTall);
            _people = new PersonPool(_world, PersonTall);
            _droneModels = ModelPoolOf(ItemModels.Drone);
            _turretModels = ModelPoolOf(ItemModels.Turret);
            _bombModels = ModelPoolOf(ItemModels.Bomb);
            _heliModels = ModelPoolOf(ItemModels.Heli);
            _planeModels = ModelPoolOf(ItemModels.Plane);
            _truckModels = ModelPoolOf(p => Models3D.Place("Cars/firetruck", p, Vector3.zero, 1.5f, 3.2f, 0f, out _));
            _ambulanceModels = ModelPoolOf(ItemModels.Ambulance);
            _boatModels = ModelPoolOf(ItemModels.Boat);
            _tankerModels = ModelPoolOf(ItemModels.Tanker);
            _fireboatModels = ModelPoolOf(ItemModels.Fireboat);
        }

        private ModelPool ModelPoolOf(System.Func<Transform, GameObject> make)
        {
            var pool = new ModelPool(_world, make);
            _modelPools.Add(pool);
            return pool;
        }

        private Pool AddPool(string name, string sprite, int order, bool glow = false)
        {
            var pool = new Pool(_world, name, Art.Get(sprite), order, glow ? Additive : null);
            _pools.Add(pool);
            return pool;
        }

        private static SpriteRenderer NewSprite(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        private TextMesh NewText()
        {
            var go = new GameObject("Number");
            go.transform.SetParent(_world, false);
            go.transform.localRotation = Billboard;
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = Art.Font;
            MeshRenderer renderer = mesh.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Art.Font.material;
            renderer.sortingOrder = 20;
            mesh.fontSize = 64;
            mesh.fontStyle = FontStyle.Bold;
            mesh.anchor = TextAnchor.MiddleCenter;
            return mesh;
        }

        /// <summary>매 프레임 Begin → Put… → End. 이번 프레임에 안 쓴 것은 꺼 둔다.</summary>
        private sealed class Pool
        {
            private readonly List<SpriteRenderer> _items = new List<SpriteRenderer>();
            private readonly List<float> _units = new List<float>();
            private readonly List<float> _aspects = new List<float>();
            private readonly Transform _parent;

            /// <summary>카메라를 보고 선다(발이 땅 자리에 닿는다). 불 몹·시민·상자처럼 서 있는 그림.</summary>
            public bool Upright;
            private readonly string _name;
            private readonly Sprite _sprite;
            private readonly int _order;
            private readonly Material _material;
            private int _used;

            public Pool(Transform parent, string name, Sprite sprite, int order, Material material)
            {
                _parent = parent;
                _name = name;
                _sprite = sprite;
                _order = order;
                _material = material;
            }

            public void Begin()
            {
                _used = 0;
            }

            /// <param name="size">월드 폭(칸).</param>
            /// <param name="stretch">세로 배율(물줄기처럼 길쭉하게).</param>
            public void Put(Vector3 at, float size, float degrees, Color color, Sprite sprite = null, float stretch = 1f)
            {
                if (_used == _items.Count)
                {
                    SpriteRenderer created = NewSprite(_parent, _name, _sprite, _order);
                    if (_material != null) created.sharedMaterial = _material;
                    _items.Add(created);
                    _units.Add(Art.FitWidth(_sprite, 1f));
                    _aspects.Add(AspectOf(_sprite));
                }
                SpriteRenderer r = _items[_used];
                if (sprite != null && r.sprite != sprite)
                {
                    r.sprite = sprite;
                    _units[_used] = Art.FitWidth(sprite, 1f);
                    _aspects[_used] = AspectOf(sprite);
                }
                float unit = _units[_used];
                float aspect = _aspects[_used];
                _used++;

                if (!r.enabled) r.enabled = true;
                Transform t = r.transform;
                if (Upright)
                {
                    // 발이 땅 자리에 닿게 반 키만큼 카메라 위쪽으로 올리고, 흔들림은 살짝만 남긴다.
                    float half = Mathf.Abs(size) * stretch * aspect * 0.5f;
                    t.localPosition = at + (Billboard * Vector3.up * half);
                    t.localRotation = Billboard * Quaternion.Euler(0f, 0f, Mathf.Clamp(Mathf.DeltaAngle(0f, degrees), -15f, 15f));
                }
                else
                {
                    t.localPosition = at;
                    t.localRotation = Quaternion.Euler(0f, 0f, degrees);
                }
                t.localScale = new Vector3(unit * size, unit * Mathf.Abs(size) * stretch, 1f);
                r.color = color;
            }

            /// <summary>임의 회전 + 가로·세로 크기 따로(입체 면·서 있는 막대). sprite가 null이면 풀 기본 그림.</summary>
            public void PutRot(Vector3 at, Quaternion rotation, float width, float height, Color color, Sprite sprite = null)
            {
                Sprite want = sprite != null ? sprite : _sprite;
                if (_used == _items.Count)
                {
                    SpriteRenderer created = NewSprite(_parent, _name, want, _order);
                    if (_material != null) created.sharedMaterial = _material;
                    _items.Add(created);
                    _units.Add(Art.FitWidth(want, 1f));
                    _aspects.Add(AspectOf(want));
                }
                SpriteRenderer r = _items[_used];
                if (r.sprite != want)
                {
                    r.sprite = want;
                    _units[_used] = Art.FitWidth(want, 1f);
                    _aspects[_used] = AspectOf(want);
                }
                _used++;
                if (!r.enabled) r.enabled = true;
                Vector3 size = want.bounds.size;
                Transform t = r.transform;
                t.localPosition = at;
                t.localRotation = rotation;
                t.localScale = new Vector3(width / Mathf.Max(0.0001f, size.x), height / Mathf.Max(0.0001f, size.y), 1f);
                r.color = color;
            }

            private static float AspectOf(Sprite sprite)
            {
                return sprite != null && sprite.bounds.size.x > 0.0001f ? sprite.bounds.size.y / sprite.bounds.size.x : 1f;
            }

            public void End()
            {
                for (int i = _used; i < _items.Count; i++)
                {
                    if (_items[i].enabled) _items[i].enabled = false;
                }
            }
        }

        /// <summary>
        /// 물줄기 리본을 한 장의 메쉬로 그린다. 사각형 토막을 잇는 것과 달리 가장자리에 계단이 없다.
        /// 결 텍스처가 길이를 따라 흘러가고(UV 스크롤), 끝 15%는 정점색으로 옅어진다.
        /// </summary>
        private sealed class RibbonPool
        {
            /// <summary>결 텍스처 한 장이 덮는 길이(칸).</summary>
            private const float TexLength = 2f;

            private readonly List<MeshRenderer> _renderers = new List<MeshRenderer>();
            private readonly List<Mesh> _meshes = new List<Mesh>();
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Color> _colors = new List<Color>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<int> _triangles = new List<int>();
            private readonly Transform _parent;
            private readonly string _name;
            private readonly Material _material;
            private readonly int _order;
            private int _used;
            private float _time;

            public RibbonPool(Transform parent, string name, Material material, int order)
            {
                _parent = parent;
                _name = name;
                _material = material;
                _order = order;
            }

            public void Begin(float time)
            {
                _used = 0;
                _time = time;
            }

            /// <param name="widthScale">리본 반폭 배율.</param>
            /// <param name="offset">반폭 대비 법선 쪽으로 옮기는 양(+ = 진행 방향 왼쪽).</param>
            /// <param name="flowSpeed">결이 흐르는 속도(칸/초).</param>
            public void Put(List<WaterRibbon.Point> pts, float widthScale, float offset, Color color, float flowSpeed, float lift = 0f)
            {
                if (pts.Count < 2) return;
                if (_used == _meshes.Count)
                {
                    var go = new GameObject(_name);
                    go.transform.SetParent(_parent, false);
                    var mesh = new Mesh { name = _name };
                    mesh.MarkDynamic();
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    MeshRenderer created = go.AddComponent<MeshRenderer>();
                    created.sharedMaterial = _material;
                    created.sortingOrder = _order;
                    _renderers.Add(created);
                    _meshes.Add(mesh);
                }
                MeshRenderer r = _renderers[_used];
                Mesh m = _meshes[_used];
                _used++;
                if (!r.enabled) r.enabled = true;

                _vertices.Clear();
                _colors.Clear();
                _uvs.Clear();
                _triangles.Clear();
                float total = pts[pts.Count - 1].Along;
                float scroll = _time * flowSpeed / TexLength;
                for (int i = 0; i < pts.Count; i++)
                {
                    WaterRibbon.Point p = pts[i];
                    // 호스 줄기: 손 높이(lift)에서 나와 4칸에 걸쳐 땅으로 떨어진다.
                    float fall = Mathf.Clamp01(1f - (p.Along / 4f));
                    var center = new Vector3(p.Pos.X, p.Pos.Y, -lift * fall * (2f - fall));
                    var n = new Vector3(p.Normal.X, p.Normal.Y, 0f);
                    Vector3 shift = n * (p.Half * offset);
                    Vector3 half = n * (p.Half * widthScale);
                    _vertices.Add(center + shift + half);
                    _vertices.Add(center + shift - half);
                    float fade = total > 0f ? Mathf.Clamp01((total - p.Along) / (total * 0.15f)) : 1f;
                    var c = new Color(color.r, color.g, color.b, color.a * fade);
                    _colors.Add(c);
                    _colors.Add(c);
                    float v = (p.Along / TexLength) - scroll;
                    _uvs.Add(new Vector2(0f, v));
                    _uvs.Add(new Vector2(1f, v));
                    if (i == 0) continue;
                    int a = (i - 1) * 2;
                    _triangles.Add(a);
                    _triangles.Add(a + 1);
                    _triangles.Add(a + 2);
                    _triangles.Add(a + 1);
                    _triangles.Add(a + 3);
                    _triangles.Add(a + 2);
                }
                m.Clear();
                m.SetVertices(_vertices);
                m.SetColors(_colors);
                m.SetUVs(0, _uvs);
                m.SetTriangles(_triangles, 0);
                m.RecalculateBounds();
            }

            public void End()
            {
                for (int i = _used; i < _renderers.Count; i++)
                {
                    if (_renderers[i].enabled) _renderers[i].enabled = false;
                }
            }
        }

        // ------------------------------------------------------------------
        // HUD
        // ------------------------------------------------------------------

        private Image StickImage(string name, Sprite sprite, float size, Color color)
        {
            Image image = UiKit.Image(_hud, name, sprite, color);
            image.rectTransform.sizeDelta = new Vector2(size, size);
            image.gameObject.SetActive(false);
            return image;
        }

        private void BuildHud()
        {
            _leftRing = StickImage("StickRingL", RingSprite(), TwinStick.Radius * 2f, new Color(1f, 1f, 1f, 0.35f));
            _leftKnob = StickImage("StickKnobL", DiscSprite(), 90f, new Color(1f, 1f, 1f, 0.6f));
            _rightRing = StickImage("StickRingR", RingSprite(), TwinStick.Radius * 2f, new Color(0.7f, 0.9f, 1f, 0.35f));
            _rightKnob = StickImage("StickKnobR", DiscSprite(), 90f, new Color(0.55f, 0.85f, 1f, 0.6f));

            _vignette = UiKit.Image(_hud, "Vignette", VignetteSprite(), Color.clear);
            UiKit.Stretch(_vignette.rectTransform);
            _vignette.raycastTarget = false;

            // 경험치 게이지: 화면 맨 위 가로 전체. 두껍게 두고 "Lv · 경험치 a / b"를 적어야 경험치로 읽힌다.
            Image xpBack = UiKit.Image(_hud, "XpBack", Art.White, new Color(0.02f, 0.05f, 0.12f, 0.85f));
            _xpBack = xpBack.rectTransform;
            _xpBack.anchorMin = new Vector2(0f, 1f);
            _xpBack.anchorMax = new Vector2(1f, 1f);
            _xpBack.pivot = new Vector2(0.5f, 1f);
            _xpBack.sizeDelta = new Vector2(0f, XpBarHeight);
            _xpBack.anchoredPosition = Vector2.zero;
            _xpFill = UiKit.Image(_xpBack, "XpFill", Art.White, new Color(0.3f, 0.65f, 1f));
            _xpFill.rectTransform.anchorMin = Vector2.zero;
            _xpFill.rectTransform.anchorMax = Vector2.one;
            _xpFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _xpFill.rectTransform.offsetMin = new Vector2(4f, 5f);
            _xpFill.rectTransform.offsetMax = new Vector2(-4f, -5f);
            _xpText = UiKit.OutlinedLabel(_xpBack, "XpText", "", 28, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(_xpText.rectTransform);
            _xpTease = UiKit.OutlinedLabel(_xpBack, "XpTease", "", 28, new Color(1f, 0.85f, 0.25f), TextAnchor.MiddleRight);
            UiKit.Stretch(_xpTease.rectTransform, 16f);

            _level = UiKit.OutlinedLabel(_hud, "Level", "", 40, Color.white, TextAnchor.UpperLeft);
            UiKit.Place(_level.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -48f), new Vector2(300f, 56f));

            _timer = UiKit.OutlinedLabel(_hud, "Timer", "", 58, Color.white, TextAnchor.UpperCenter);
            UiKit.Place(_timer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(400f, 70f));
            _kills = UiKit.OutlinedLabel(_hud, "Kills", "", 30, new Color(1f, 0.85f, 0.6f), TextAnchor.UpperCenter);
            UiKit.Place(_kills.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -114f), new Vector2(900f, 40f));
            // 연속 진압 콤보: 체력 바 아래 왼쪽(가운데 위는 대화재 막대·알림이 쓴다). 3부터 보이고, 끊기기 전까지 남은 시간만큼 흐려진다.
            // 체력 바는 소방관 머리 위에 있다(DrawPlayer). HUD 왼쪽 위는 레벨·콤보·바람만.
            _comboText = UiKit.OutlinedLabel(_hud, "Combo", "", 44, Color.white, TextAnchor.UpperLeft);
            UiKit.Place(_comboText.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -110f), new Vector2(500f, 60f));

            // 산불 숲: 콤보 아래 바람 화살표.
            _windLabel = UiKit.OutlinedLabel(_hud, "WindLabel", "바람", 28, new Color(0.85f, 0.92f, 1f), TextAnchor.MiddleLeft);
            UiKit.Place(_windLabel.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -128f), new Vector2(120f, 50f));
            _windArrow = UiKit.Image(_hud, "WindArrow", ArrowSprite(), new Color(0.85f, 0.92f, 1f));
            _windArrow.raycastTarget = false;
            UiKit.Place(_windArrow.rectTransform, new Vector2(0f, 1f), new Vector2(160f, -128f), new Vector2(56f, 56f));
            // 가운데를 축으로 돌게(모서리 축이면 돌 때 위로 올라간다).
            _windArrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _windArrow.rectTransform.anchoredPosition = new Vector2(160f, -153f);

            _build = UiKit.OutlinedLabel(_hud, "Build", "", 26, new Color(0.85f, 0.92f, 1f), TextAnchor.UpperRight);
            UiKit.Place(_build.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -54f), new Vector2(520f, 400f));

            _alert = UiKit.OutlinedLabel(_hud, "Alert", "", 52, Color.white, TextAnchor.MiddleCenter);
            UiKit.Place(_alert.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1400f, 80f));

            _bossBack = UiKit.Image(_hud, "BossBack", Art.White, new Color(0f, 0f, 0f, 0.7f));
            UiKit.Place(_bossBack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -164f), new Vector2(900f, 34f));
            _bossFill = UiKit.Image(_bossBack.transform, "BossFill", Art.White, new Color(1f, 0.45f, 0.1f));
            _bossFill.rectTransform.anchorMin = Vector2.zero;
            _bossFill.rectTransform.anchorMax = Vector2.one;
            _bossFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _bossFill.rectTransform.offsetMin = new Vector2(3f, 3f);
            _bossFill.rectTransform.offsetMax = new Vector2(-3f, -3f);
            Text bossName = UiKit.OutlinedLabel(_bossBack.transform, "BossName", "대화재", 26, Color.white, TextAnchor.MiddleCenter);
            _bossName = bossName;
            UiKit.Stretch(bossName.rectTransform);

            _bossBand = UiKit.Image(_hud, "BossBand", Art.White, new Color(0.6f, 0f, 0f, 0.75f));
            _bossBand.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            _bossBand.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            _bossBand.rectTransform.sizeDelta = new Vector2(0f, 160f);
            _bossBand.rectTransform.anchoredPosition = new Vector2(0f, 60f);
            _bossBandText = UiKit.OutlinedLabel(_bossBand.transform, "Text", "대형 화재 접근!", 84, new Color(1f, 0.9f, 0.4f), TextAnchor.MiddleCenter);
            UiKit.Stretch(_bossBandText.rectTransform);

            // 폰(터치 화면)에서는 두 엄지 조작을 알려 준다.
            string controls = Input.touchSupported
                ? "왼손 끌어 이동 · 오른손 누르면 물(끌어서 겨누기) · 구슬을 모아 레벨업 · 카드는 탭 · 불난 가게 문 앞에 서 있으면 구조"
                : "WASD 이동 · 마우스로 겨누고 왼쪽 버튼을 누르면 물 · 구슬을 모아 레벨업 · 카드는 1/2/3 또는 클릭 · 불난 가게 문 앞에 서 있으면 구조      R 다시  N 스테이지  G " + (_maxGear ? "일반" : "풀장비") + "  Tab 시험판 전환";
            Text help = UiKit.OutlinedLabel(_hud, "Help", (_maxGear ? "[풀장비]  " : "") + controls, 24, new Color(0.8f, 0.8f, 0.85f), TextAnchor.LowerCenter);
            UiKit.Place(help.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(1850f, 40f));

            _flashImage = UiKit.Image(_hud, "Flash", Art.White, Color.clear);
            UiKit.Stretch(_flashImage.rectTransform);
            _flashImage.raycastTarget = false;

            _cardLayer = UiKit.Node(_hud, "Cards");
            UiKit.Stretch(_cardLayer);

            _resultBack = UiKit.Image(_hud, "ResultBack", Art.White, new Color(0f, 0f, 0f, 0.78f));
            UiKit.Place(_resultBack.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 560f));
            _result = UiKit.OutlinedLabel(_resultBack.transform, "Result", "", 42, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(_result.rectTransform, 20f);
            for (int i = 0; i < 3; i++)
            {
                Image star = UiKit.Image(_resultBack.transform, "Star", Art.Get("UI/star"), Color.white);
                UiKit.Place(star.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 130f, 115f), new Vector2(110f, 110f));
                _stars.Add(star);
            }
            _resultBack.gameObject.SetActive(false);
        }

        private const float XpBarHeight = 40f;

        private bool HasSpecialLeft()
        {
            foreach (UpgradeId id in _sim.Stage.Specials)
            {
                if (_sim.Build.CanTake(id)) return true;
            }
            return false;
        }

        private void RefreshHud(float dt)
        {
            bool windy = _sim.Wind.X != 0f || _sim.Wind.Y != 0f;
            _windLabel.gameObject.SetActive(windy);
            _windArrow.gameObject.SetActive(windy);
            if (windy)
            {
                _windArrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(_sim.Wind.Y, _sim.Wind.X) * Mathf.Rad2Deg);
                _windArrow.rectTransform.localScale = Vector3.one * (1f + (0.08f * Mathf.Sin(_time * 5f)));
            }

            // 남은 시간: 4:00까지 지키면 이긴다. 대화재(3:00~)부터는 붉게 뛴다.
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, SurvivorSim.RunTime - _sim.Time));
            _timer.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            bool lastMinute = _sim.Finale || _sim.Time >= SurvivorSim.FinaleAt - 10f;
            _timer.color = lastMinute ? Color.Lerp(new Color(1f, 0.35f, 0.25f), Color.white, _sim.Finale ? 0f : 0.5f + (0.5f * Mathf.Sin(_time * 12f))) : Color.white;
            _timer.rectTransform.localScale = Vector3.one * (_sim.Finale ? 1f + (0.06f * Mathf.Abs(Mathf.Sin(_time * 4f))) : 1f);
            int total = _sim.HousesTotal;
            _kills.text = "지킨 건물 " + (total - _sim.HousesLost) + "/" + total + "  ·  구조 " + _sim.Rescued + "  ·  잃음 " + _sim.CiviliansLost;
            // 하나만 더 무너지면 진다: 붉게 깜빡인다.
            bool edge = total > 0 && (_sim.HousesLost + 1) * 2 > total && _sim.Outcome == SOutcome.Playing;
            _kills.color = edge && Mathf.Sin(_time * 10f) > 0f ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.85f, 0.6f);
            _level.text = "Lv " + _sim.Level;

            bool combo = _sim.Combo >= 3 && _sim.Outcome == SOutcome.Playing;
            _comboText.gameObject.SetActive(combo);
            if (combo)
            {
                int mult = _sim.ComboMult;
                _comboText.text = _sim.Combo + " 연속" + (mult > 1 ? "  ×" + mult : "");
                float left = Mathf.Clamp01(_sim.ComboClock / SurvivorSim.ComboWindow);
                Color tint = mult >= 2 ? new Color(1f, 0.75f, 0.25f) : _sim.Combo >= SurvivorSim.ComboStep / 2 ? new Color(1f, 0.95f, 0.6f) : Color.white;
                _comboText.color = new Color(tint.r, tint.g, tint.b, 0.35f + (0.65f * left));
                _comboText.rectTransform.localScale = Vector3.one * (1f + (0.12f * (mult - 1)) + (0.08f * Mathf.Clamp01(1f - (_sim.ComboClock / SurvivorSim.ComboWindow) * 4f)));
            }

            float xp = Mathf.Clamp01(_sim.Xp / (float)_sim.XpToNext);
            _shownXp = dt <= 0f ? xp : Mathf.Lerp(_shownXp, xp, 1f - Mathf.Exp(-14f * dt));
            if (xp < _shownXp - 0.3f) _shownXp = xp;
            _xpFill.rectTransform.localScale = new Vector3(_shownXp, 1f, 1f);
            _xpBack.sizeDelta = new Vector2(0f, XpBarHeight + (10f * _xpPunch));
            // 곧 레벨업(90% 이상)이면 채움이 반짝인다.
            float almost = xp >= 0.9f ? 0.35f * (0.5f + (0.5f * Mathf.Sin(_time * 12f))) : 0f;
            _xpFill.color = Color.Lerp(new Color(0.3f, 0.65f, 1f), new Color(0.75f, 0.95f, 1f), Mathf.Max(_xpPunch, almost));
            _xpText.text = "경험치 " + _sim.Xp + " / " + _sim.XpToNext;
            bool yellowNext = SurvivorUpgrades.SpecialDue(_sim.Level + 1) && HasSpecialLeft();
            _xpTease.text = yellowNext ? "다음 레벨: 특수 장비!" : "";
            if (yellowNext) _xpTease.color = Color.Lerp(new Color(1f, 0.85f, 0.25f), Color.white, 0.3f * (0.5f + (0.5f * Mathf.Sin(_time * 6f))));

            // 체력 비율은 가장자리 붉은 비네트가 쓴다(바는 머리 위).
            float hp = Mathf.Clamp01(_sim.Hp / _sim.MaxHp);

            var build = new System.Text.StringBuilder();
            foreach (UpgradeId id in _sim.Build.Owned())
            {
                int lv = _sim.Build.Level(id);
                if (Loadout.IsSpecial(id))
                {
                    build.Append("<color=#FFD84A>").Append(SurvivorUpgrades.Name(id)).Append(Loadout.IsEvolution(id) ? "  진화" : "  특수").Append("</color>\n");
                    continue;
                }
                if (lv >= Loadout.MaxLevel) build.Append("<color=#FFD84A>").Append(SurvivorUpgrades.Name(id)).Append("  ★MAX</color>\n");
                else build.Append(SurvivorUpgrades.Name(id)).Append("  Lv").Append(lv).Append('\n');
            }
            _build.text = build.ToString();

            // 가장자리: 맞으면 붉게, 체력 30% 아래면 심장처럼 뛴다. 대화재 동안은 늘 붉게 일렁인다.
            float danger = _hurt * 0.6f;
            if (hp < 0.3f && _sim.Outcome == SOutcome.Playing) danger = Mathf.Max(danger, 0.35f + (0.25f * Mathf.Max(0f, Mathf.Sin(_time * 7f))));
            if (_sim.Finale && _sim.Outcome == SOutcome.Playing) danger = Mathf.Max(danger, 0.3f + (0.1f * Mathf.Sin(_time * 3f)));
            if (_waveAge < 1.5f)
            {
                // 사방 포위: 가장자리가 주황으로 세 번 맥동한다.
                float pulse = (1f - (_waveAge / 1.5f)) * (0.5f + (0.5f * Mathf.Sin(_waveAge * 13f)));
                _vignette.color = Color.Lerp(new Color(0.8f, 0.05f, 0f, Mathf.Clamp01(danger)), new Color(1f, 0.5f, 0.05f, 0.9f), pulse);
            }
            else
            {
                _vignette.color = new Color(0.8f, 0.05f, 0f, Mathf.Clamp01(danger));
            }

            _flashImage.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, _flash * 0.55f);

            _alert.color = new Color(_alert.color.r, _alert.color.g, _alert.color.b, _alertAge < 2f ? 1f : Mathf.Max(0f, 1f - ((_alertAge - 2f) * 2f)));
            float s = _alertAge < 0.15f ? Mathf.Lerp(_alertScale, 1f, _alertAge / 0.15f) : 1f;
            _alert.rectTransform.localScale = Vector3.one * s;
            float shake = _alertAge < 0.6f ? Mathf.Sin(_alertAge * 60f) * 14f * (1f - (_alertAge / 0.6f)) : 0f;
            _alert.rectTransform.anchoredPosition = new Vector2(shake, 250f);

            // 대화재 막대: 체력 막대가 아니라 "끝까지 남은 시간". 이름 칸엔 랜드마크에 갇힌 사람 수.
            bool finale = _sim.Finale && _sim.Outcome == SOutcome.Playing;
            _bossBack.gameObject.SetActive(finale);
            if (finale)
            {
                Structure mark = _sim.Landmark;
                bool people = mark != null && !mark.Collapsed && mark.Burning && mark.Residents > 0;
                if (people)
                {
                    // 사람이 갇혀 있는 동안은 "무너지기까지 남은 시간"이 막대다. 붉게 줄어든다.
                    float fall = _sim.TimeToFall(mark);
                    _bossName.text = _sim.Stage.FinaleName + " · " + mark.Name + " " + Mathf.CeilToInt(Mathf.Min(fall, 99f)) + "초 뒤 " + (mark.Tanker ? "침몰" : "무너짐") + " · " + mark.Residents + "명 갇힘";
                    _bossFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(fall / SurvivorSim.BurnBuilding), 1f, 1f);
                    bool urgent = fall <= SurvivorSim.CollapseWarnAt;
                    _bossFill.color = urgent && Mathf.Sin(_time * 12f) > 0f ? new Color(1f, 0.2f, 0.1f) : new Color(0.9f, 0.3f, 0.15f);
                }
                else
                {
                    // 감독 단계가 오르면 이름 칸에 보인다: "지금 몰아붙이는 중"을 알아야 버틸 각오가 선다.
                    string finaleName = _sim.Stage.FinaleName;
                    _bossName.text = _sim.FinalePressure > 0 ? finaleName + " · 불길 " + _sim.FinalePressure + "단계 · " + _sim.Stage.FinaleGoal : finaleName + " · " + _sim.Stage.FinaleGoal;
                    float left = Mathf.Clamp01((SurvivorSim.RunTime - _sim.Time) / (SurvivorSim.RunTime - SurvivorSim.FinaleAt));
                    _bossFill.rectTransform.localScale = new Vector3(left, 1f, 1f);
                    _bossFill.color = new Color(1f, 0.45f, 0.1f);
                }
            }

            bool band = _bossBannerAge < 2.8f;
            _bossBand.gameObject.SetActive(band);
            if (band)
            {
                float a = _bossBannerAge < 2.3f ? 1f : 1f - ((_bossBannerAge - 2.3f) / 0.5f);
                _bossBand.color = new Color(_bandTint.r, _bandTint.g, _bandTint.b, 0.75f * a * (0.8f + (0.2f * Mathf.Sin(_time * 20f))));
                _bossBandText.color = new Color(1f, 0.9f, 0.4f, a);
                // 띠는 가운데서 좌우로 펼쳐지고, 글자는 왼쪽에서 미끄러져 들어온다.
                float open = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_bossBannerAge / 0.25f));
                _bossBand.rectTransform.localScale = new Vector3(open, 1f, 1f);
                _bossBandText.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-1400f, 0f, open), 0f);
                _bossBandText.rectTransform.localScale = new Vector3(1f / Mathf.Max(open, 0.05f), 1f, 1f) * (1f + (0.04f * Mathf.Sin(_time * 14f)));
            }

            AnimateCards();

            bool over = _sim.Outcome != SOutcome.Playing;
            _resultBack.gameObject.SetActive(over);
            if (over)
            {
                bool won = _sim.Outcome == SOutcome.Won;
                string head = won ? "동네를 지켜 냈다!" : _sim.LostTown ? "동네가 다 타 버렸다" : "쓰러졌다";
                string stats = "지킨 건물 " + (total - _sim.HousesLost) + "/" + total + "\n구한 사람 " + _sim.Rescued + "   ·   잃은 사람 " + _sim.CiviliansLost;
                int played = Mathf.FloorToInt(_sim.Time);
                string more = (played / 60).ToString("00") + ":" + (played % 60).ToString("00") + " 버팀  ·  처치 " + _sim.Kills + "  ·  최고 " + _comboPeak + "연속  ·  Lv " + _sim.Level;
                string bank = won ? "★ +" + _earned + "  ·  모은 별 " + _station.Stars + "\n" : "";
                _result.text = head + "\n\n" + (won ? "\n\n" : "") + stats + "\n" + more + "\n" + bank + "\n" + (_overAge > 1f ? (won ? "탭하면 소방서로 · 다음: STAGE " + SurvivorStages.Next(_stage) + " " + SurvivorStages.Get(SurvivorStages.Next(_stage)).Name : "탭하면 소방서로") : "");
                for (int i = 0; i < _stars.Count; i++)
                {
                    _stars[i].gameObject.SetActive(won);
                    // 별은 하나씩 톡톡 튀어나온다.
                    float pop2 = Mathf.Clamp01((_overAge - 0.3f - (i * 0.25f)) / 0.2f);
                    _stars[i].sprite = Art.Get(i < _sim.Stars ? "UI/star" : "UI/star_empty");
                    _stars[i].rectTransform.localScale = Vector3.one * (pop2 < 1f ? Mathf.Lerp(0f, 1.3f, pop2) : 1f);
                }
                float pop = _overAge < 0.2f ? Mathf.Lerp(0.6f, 1f, _overAge / 0.2f) : 1f;
                _resultBack.rectTransform.localScale = Vector3.one * pop;
            }
        }

        /// <summary>화면 밖 타는 건물·가스통·상자를 화면 가장자리 화살표(어두운 원반 위, 라벨)로 가리킨다. 갇힌 사람이 있으면 초록으로 크게 뛴다.</summary>
        private void DrawEdgeArrows()
        {
            if (_sim.Outcome != SOutcome.Playing || _camera == null) return;
            // 화면 밖에서 떠오는 풍등: 어느 쪽에서 오는지 보여 준다(쏘아 떨어뜨리러 갈 수 있게).
            foreach (Enemy e in _sim.Enemies)
            {
                if (e.Dead || e.Kind != EnemyKind.SkyLantern) continue;
                EdgeArrow(new Vector3(e.Pos.X, e.Pos.Y, 0f), new Color(1f, 0.75f, 0.4f), 1.3f, "풍등");
            }
            foreach (Structure st in _sim.Structures)
            {
                if (st.Burning && st.Kind == StructureKind.Boat && !st.Docked)
                {
                    EdgeArrow(new Vector3(st.Pos.X, st.Pos.Y, 0f), new Color(1f, 0.4f, 0.25f), 1.6f * (1f + (0.2f * Mathf.Abs(Mathf.Sin(_time * 7f)))), st.Tanker ? "유조선" : "불배");
                    continue;
                }
                if (!st.Burning || !(st.IsBuilding || st.Kind == StructureKind.Gas)) continue;
                bool people = st.Residents > 0;
                // 대형 신고·대화재 건물은 붉게, 가장 크게 뛴다.
                bool big = people && (st == _sim.BigReport || st == _sim.Landmark);
                float beat = 1f + ((people ? 0.25f : 0.1f) * Mathf.Abs(Mathf.Sin(_time * (people ? 8f : 5f))));
                Color c = big ? new Color(1f, 0.25f, 0.2f) : people ? new Color(0.45f, 1f, 0.45f) : st.Kind == StructureKind.Gas ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.55f, 0.2f);
                string label = big ? "대형 화재 " + st.Residents + "명" : people ? st.Residents + "명 갇힘" : st.Kind == StructureKind.Gas ? "가스" : "불";
                EdgeArrow(new Vector3(st.Pos.X, st.Pos.Y, 0f), c, 1.6f * beat * (big ? 1.5f : people ? 1.25f : 1f), label);
            }
            foreach (Pickup chest in _sim.Chests) EdgeArrow(new Vector3(chest.Pos.X, chest.Pos.Y, 0f), new Color(1f, 0.85f, 0.3f), 1.9f * (1f + (0.2f * Mathf.Abs(Mathf.Sin(_time * 7f)))), "보물상자");
            foreach (Pickup box in _sim.Toolboxes) EdgeArrow(new Vector3(box.Pos.X, box.Pos.Y, 0f), new Color(1f, 0.75f, 0.2f), 1.6f * (1f + (0.15f * Mathf.Abs(Mathf.Sin(_time * 6f)))), "공구상자");
            foreach (Pickup kit in _sim.Kits) EdgeArrow(new Vector3(kit.Pos.X, kit.Pos.Y, 0f), new Color(1f, 0.55f, 0.55f), 1.6f * (1f + (0.15f * Mathf.Abs(Mathf.Sin(_time * 6f)))), "구급상자");
        }

        /// <summary>화면 안이면 안 그린다. 밖이면 가장자리(위는 HUD를 피해 조금 더 안쪽)에 원반·화살표·라벨. 카메라가 따라갈 자리 기준(이번 프레임 FollowCamera 전이라).</summary>
        private void EdgeArrow(Vector3 target, Color c, float size, string label)
        {
            Vector3 eye = _cameraAt;
            float halfH = _viewHalfH;
            float halfW = _viewHalfW;
            float dx = target.x - eye.x;
            float dy = target.y - eye.y;
            if (Mathf.Abs(dx) < halfW * 0.96f && Mathf.Abs(dy) < halfH * 0.96f) return;
            float k = Mathf.Min((halfW * 0.92f) / Mathf.Max(Mathf.Abs(dx), 0.001f), (halfH * (dy > 0f ? 0.72f : 0.84f)) / Mathf.Max(Mathf.Abs(dy), 0.001f));
            var spot = new Vector3(eye.x + (dx * k), eye.y + (dy * k), 0f);
            float len = Mathf.Max(0.001f, Mathf.Sqrt((dx * dx) + (dy * dy)));
            var dir = new Vector3(dx / len, dy / len, 0f);
            _arrowBacks.Put(spot, size * 1.5f, 0f, new Color(0f, 0f, 0f, 0.45f));
            _edgeArrows.Put(spot, size, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, c);
            // 라벨은 화살표에서 화면 안쪽으로.
            Tag(spot - (dir * ((size * 0.6f) + 0.8f)) + new Vector3(0f, 0f, -0.05f), label, Color.Lerp(c, Color.white, 0.55f), 0.055f);
        }

        /// <summary>발밑 안내 화살표를 ttl초 동안 건다(이미 있으면 시간만 늘린다). 캡처 하니스도 부른다.</summary>
        public void PointAt(Structure st, float ttl)
        {
            if (st == null) return;
            for (int i = 0; i < _guides.Count; i++)
            {
                if (_guides[i].At != st) continue;
                _guides[i] = new Guide { At = st, Ttl = Mathf.Max(_guides[i].Ttl, ttl) };
                return;
            }
            _guides.Add(new Guide { At = st, Ttl = ttl });
        }

        /// <summary>새로 불난 건물·대형 신고·대화재 쪽으로 소방관 발밑에서 큰 화살표가 몇 초 동안 뛴다. 4칸 안이거나 꺼지면 사라지고, 마지막 1초에 옅어진다.</summary>
        private void DrawGuides(float dt)
        {
            Vector3 me = W(_sim.Player);
            // 항구: 불배가 떠 있는 동안 발밑 화살표가 부두 끝(요격 지점)을 가리킨다. 부두 끝 4칸 안에 서면 사라진다.
            if (_pierGuideTtl > 0f)
            {
                _pierGuideTtl -= dt;
                Structure boat = FloatingBoat();
                if (boat != null && _sim.Outcome == SOutcome.Playing)
                {
                    Vec2 tip = PierTipFor(boat.Pos);
                    float dx = tip.X - _sim.Player.X;
                    float dy = tip.Y - _sim.Player.Y;
                    float len = Mathf.Sqrt((dx * dx) + (dy * dy));
                    if (len >= 4f)
                    {
                        float a = Mathf.Clamp01(_pierGuideTtl);
                        var c = new Color(0.5f, 0.95f, 1f, a);
                        var dir = new Vector3(dx / len, dy / len, 0f);
                        float size = 1.6f * (1f + (0.2f * Mathf.Abs(Mathf.Sin(_time * 8f))));
                        Vector3 spot = new Vector3(_sim.Player.X, _sim.Player.Y, 0f) + (dir * 2.4f);
                        _arrowBacks.Put(spot, size * 1.4f, 0f, new Color(0f, 0f, 0f, 0.4f * a));
                        _edgeArrows.Put(spot, size, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, c);
                        Tag(spot + new Vector3(0f, -1.2f, -0.05f), "부두 끝 요격 지점", new Color(0.8f, 1f, 1f, a), 0.055f);
                    }
                }
            }
            for (int i = _guides.Count - 1; i >= 0; i--)
            {
                Guide g = _guides[i];
                g.Ttl -= dt;
                Structure st = g.At;
                float dx = st.Pos.X - _sim.Player.X;
                float dy = st.Pos.Y - _sim.Player.Y;
                float len = Mathf.Sqrt((dx * dx) + (dy * dy));
                if (g.Ttl <= 0f || !st.Burning || st.Collapsed || len < 4f || _sim.Outcome != SOutcome.Playing)
                {
                    _guides.RemoveAt(i);
                    continue;
                }
                _guides[i] = g;
                bool people = st.Residents > 0;
                bool big = people && (st == _sim.BigReport || st == _sim.Landmark);
                Color c = big ? new Color(1f, 0.25f, 0.2f) : people ? new Color(0.45f, 1f, 0.45f) : new Color(1f, 0.55f, 0.2f);
                float a = Mathf.Clamp01(g.Ttl);
                c.a = a;
                var dir = new Vector3(dx / len, dy / len, 0f);
                float size = 1.6f * (1f + (0.2f * Mathf.Abs(Mathf.Sin(_time * 8f))));
                Vector3 spot = new Vector3(_sim.Player.X, _sim.Player.Y, 0f) + (dir * 2.4f);
                _arrowBacks.Put(spot, size * 1.4f, 0f, new Color(0f, 0f, 0f, 0.4f * a));
                _edgeArrows.Put(spot, size, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, c);
                Color tagColor = Color.Lerp(c, Color.white, 0.55f);
                tagColor.a = a;
                Tag(spot + new Vector3(0f, -1.2f, -0.05f), st.Name + (people ? " " + st.Residents + "명 갇힘" : " 불!"), tagColor, 0.055f);
            }
        }

        /// <summary>오른쪽을 가리키는 화살표(굵은 꼬리 + 뾰족한 머리). 테두리는 어둡다.</summary>
        private static Sprite ArrowSprite()
        {
            if (_arrowSprite != null) return _arrowSprite;
            const int n = 64;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n;
                    float v = Mathf.Abs(((y + 0.5f) / n) - 0.5f) * 2f;
                    // 머리: u 0.45~0.95에서 반폭 0.9→0. 꼬리: u 0.08~0.75에서 반폭 0.32(머리 안까지 이어 틈이 없게).
                    float head = Mathf.Min((0.9f * (0.95f - u) / 0.5f) - v, u - 0.45f);
                    float tail = Mathf.Min(0.32f - v, Mathf.Min(u - 0.08f, 0.75f - u));
                    float inside = Mathf.Max(head, tail);
                    float a = Mathf.Clamp01(inside * 25f);
                    float rim = Mathf.Clamp01((inside - 0.07f) * 25f);
                    byte c = (byte)Mathf.Lerp(40f, 255f, rim);
                    pixels[(y * n) + x] = new Color32(c, c, c, (byte)(a * 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _arrowSprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return _arrowSprite;
        }

        /// <summary>
        /// 한 번에 쏟는 물의 번쩍임: 가산 빛을 layers겹 겹쳐 1을 넘겨 블룸이 번지게 한다
        /// (스프라이트 색은 1에서 잘리므로 색을 올리는 대신 겹친다). 짧게 끝나는 번쩍임에만 쓴다.
        /// </summary>
        private void Flare(Vector3 at, float size, Color color, int layers, float life = 0.2f)
        {
            for (int k = 0; k < layers; k++)
            {
                Emit("Effects/glow", at, Vector3.zero, 0f, life * (1f + (0.25f * k)), size * (0.7f + (0.2f * k)), size * (1.1f + (0.25f * k)),
                    new Color(color.r, color.g, color.b, 1f), new Color(color.r, color.g, color.b, 0f), 0f, true);
            }
        }

        /// <summary>불 규칙 신호: 크게 줄인 불은 지붕 위 파란 "−N%", 옆 건물로 번진 불은 빨간 알림과 두 건물을 잇는 불길.</summary>
        private void ShowFireSignals()
        {
            foreach (FireKnock k in _sim.Knocked)
            {
                SpawnText(W(k.At.Pos) + new Vector3(0f, 2.2f, 0f), "−" + Mathf.RoundToInt(k.Amount * 100f) + "%", new Color(0.45f, 0.85f, 1f), 1.5f);
            }
            for (int i = 0; i < _sim.Spread.Count; i++)
            {
                Structure to = _sim.Spread[i];
                Vector3 a = W(_sim.SpreadFrom[i].Pos);
                Vector3 b = W(to.Pos);
                ShowAlert(_sim.SpreadFrom[i].Name + "에서 " + to.Name + "(으)로 번졌다!", new Color(1f, 0.3f, 0.2f));
                Shockwave(b, new Color(1f, 0.3f, 0.05f, 1f), 7f, 0.5f);
                Flare(b, 4f, new Color(1f, 0.45f, 0.1f), 3);
                // 옮긴 건물에서 옮겨붙은 건물까지 불길이 차례로 타오른다(가까운 쪽부터 오래 남는다).
                for (int k = 0; k < 20; k++)
                {
                    Vector3 p = Vector3.Lerp(a, b, k / 19f);
                    Emit(Flames[k % Flames.Length], p, new Vector3(Random.Range(-0.5f, 0.5f), 2f, 0f), 1f, 0.7f + (k * 0.03f), 1.8f, 0.6f,
                        new Color(1f, 0.6f, 0.2f, 1f), new Color(1f, 0.25f, 0.05f, 0f), Random.Range(-90f, 90f), true);
                    Emit("Effects/glow", p, Vector3.zero, 0f, 0.6f, 1.6f, 2.4f, new Color(1f, 0.4f, 0.1f, 0.9f), new Color(1f, 0.2f, 0f, 0f), 0f, true);
                }
                _trauma = Mathf.Min(1f, _trauma + 0.25f);
                GameAudio.Play(Cue.SecondIgnition);
            }
            foreach (Structure st in _sim.OilCaught)
            {
                Vector3 at = W(st.Pos);
                if (st.IsBuilding) ShowAlert("기름 불이 " + st.Name + "에 옮겨붙었다!", new Color(0.95f, 0.45f, 0.8f));
                Shockwave(at, new Color(0.85f, 0.3f, 0.7f, 1f), st.IsBuilding ? 6f : 3f, 0.45f);
                Flare(at, st.IsBuilding ? 3.5f : 2f, new Color(1f, 0.45f, 0.6f), 2);
                GameAudio.Play(Cue.SecondIgnition);
            }
        }

        /// <summary>곧 번질 불: 세기 0.8 넘고 3초 안에 옮겨붙을 건물까지 깜빡이는 빨간 줄(가까울수록 빨리 깜빡인다).</summary>
        private void DrawSpreadWarnings()
        {
            if (_sim.Stage.SpreadEvery <= 0f || _sim.Outcome != SOutcome.Playing) return;
            foreach (Structure st in _sim.Structures)
            {
                if (!st.IsBuilding || !st.Burning || st.Fire < SurvivorSim.SpreadFire || st.SpreadClock > 3f) continue;
                Structure next = _sim.NextBuilding(st);
                if (next == null) continue;
                float beat = 0.5f + (0.5f * Mathf.Sin(_time * (8f + (6f * (3f - st.SpreadClock)))));
                SprayLine(W(st.Pos), W(next.Pos), 0.18f, new Color(1f, 0.25f, 0.1f, 0.35f + (0.5f * beat)));
            }
        }

        private void ShowAlert(string text, Color color)
        {
            _alert.text = text;
            _alert.color = color;
            _alertAge = 0f;
            _alertScale = 1.6f;
        }

        private static readonly Color SpecialGold = new Color(1f, 0.84f, 0.3f);

        /// <summary>
        /// 노란 장비 출동 배너: 금색 큰 글자가 2.2배로 튀어오르고, 금빛 화면 플래시·줌 펀치·작은 흔들림이 따른다.
        /// 7종이 모두 이걸로 시작한다("출동 배너 → 예고 표식 → 임팩트"). quiet면 자주 오는 장비(스프링클러)라 글자만.
        /// </summary>
        private void SpecialBanner(string text, Color tint, bool quiet = false)
        {
            ShowAlert(text, SpecialGold);
            _alertScale = 2.2f;
            if (quiet) return;
            Flash(Color.Lerp(SpecialGold, tint, 0.5f), 0.25f);
            _zoomKick = Mathf.Max(_zoomKick, 0.06f);
            _trauma = Mathf.Min(1f, _trauma + 0.12f);
        }

        private void ShowCards()
        {
            HideCards();
            _cardAge = 0f;
            List<UpgradeId> choices = _sim.PendingChoices;
            if (choices == null) return;

            Image dim = UiKit.Image(_cardLayer, "Dim", Art.White, new Color(0f, 0f, 0.05f, 0.55f));
            UiKit.Stretch(dim.rectTransform);
            _cards.Add(dim.rectTransform);

            // 제목 뒤에서 천천히 도는 빛살.
            for (int i = 0; i < 4; i++)
            {
                Image ray = UiKit.Image(_cardLayer, "Ray" + i, BeamSprite(), new Color(1f, 0.9f, 0.5f, 0.12f));
                ray.raycastTarget = false;
                UiKit.Place(ray.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 360f), new Vector2(90f, 900f));
                _rays.Add(ray.rectTransform);
            }

            bool yellow = choices.Exists(Loadout.IsSpecial);
            if (yellow)
            {
                Flash(new Color(1f, 0.85f, 0.3f), 0.12f);
                GameAudio.Play(Cue.Rescued);
            }
            string head = _sim.ChoosingChest ? "보물상자!" : "레벨 업!";
            Text title = UiKit.OutlinedLabel(_cardLayer, "Title", yellow ? head + "  특수 장비 등장!" : head, yellow ? 70 : 80, new Color(1f, 0.9f, 0.4f), TextAnchor.MiddleCenter);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 360f), new Vector2(900f, 110f));
            _cards.Add(title.rectTransform);

            for (int i = 0; i < choices.Count; i++)
            {
                int index = i;
                UpgradeId id = choices[i];
                int next = _sim.Build.Level(id) + 1;
                bool toMax = !Loadout.IsSpecial(id) && id != UpgradeId.Heal && next == Loadout.MaxLevel;
                if (Loadout.IsSpecial(id) || toMax)
                {
                    Image glow = UiKit.Image(_cardLayer, "YellowGlow" + i, Art.Get("Effects/glow"), new Color(1f, 0.8f, 0.2f, 0.3f));
                    glow.raycastTarget = false;
                    UiKit.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - ((choices.Count - 1) / 2f)) * 470f, -20f), new Vector2(520f, 600f));
                    _yellowGlows.Add(glow);
                }
                string sprite = Loadout.IsSpecial(id) ? "UI/button_yellow" : id == UpgradeId.Heal ? "UI/button_green" : Loadout.IsWeapon(id) ? "UI/button_red" : "UI/button_blue";
                Button card = UiKit.Button(_cardLayer, "Card" + i, Art.Get(sprite), "", 0, () =>
                {
                    if (_cardAge > 0.35f) Choose(index);
                });
                RectTransform rect = card.GetComponent<RectTransform>();
                float x = (i - ((choices.Count - 1) / 2f)) * 470f;
                UiKit.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(x, -20f), new Vector2(430f, 520f));

                string kind = Loadout.IsEvolution(id) ? "진화" : Loadout.IsSpecial(id) ? "특수" : id == UpgradeId.Heal ? "회복" : Loadout.IsWeapon(id) ? "무기" : "보조";
                // 진화 카드는 어떤 조합으로 나왔는지 보여 준다("물폭탄 MAX + 무전기").
                string tag = Loadout.IsEvolution(id) ? SurvivorUpgrades.Name(Loadout.BaseOf(id)) + " MAX + " + SurvivorUpgrades.Name(Loadout.PairOf(id))
                    : id == UpgradeId.Heal ? "" : Loadout.IsSpecial(id) ? "★ 특수 장비 ★" : next <= 1 ? "새로 얻음!" : toMax ? "Lv 5 · MAX!" : "Lv " + next;

                Text key = UiKit.OutlinedLabel(rect, "Key", (i + 1).ToString(), 44, Color.white, TextAnchor.UpperLeft);
                UiKit.Place(key.rectTransform, new Vector2(0f, 1f), new Vector2(26f, -18f), new Vector2(80f, 60f));
                Text kindLabel = UiKit.OutlinedLabel(rect, "Kind", kind, 30, new Color(1f, 1f, 1f, 0.85f), TextAnchor.UpperRight);
                UiKit.Place(kindLabel.rectTransform, new Vector2(1f, 1f), new Vector2(-26f, -24f), new Vector2(200f, 44f));
                Text name = UiKit.OutlinedLabel(rect, "Name", SurvivorUpgrades.Name(id), 50, Color.white, TextAnchor.MiddleCenter);
                UiKit.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(400f, 80f));
                Text tagLabel = UiKit.OutlinedLabel(rect, "Tag", tag, 36, new Color(1f, 0.95f, 0.5f), TextAnchor.MiddleCenter);
                UiKit.Place(tagLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(400f, 50f));
                Text desc = UiKit.OutlinedLabel(rect, "Desc", SurvivorUpgrades.Describe(id, next), 32, Color.white, TextAnchor.UpperCenter);
                UiKit.Place(desc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(370f, 200f));
                desc.horizontalOverflow = HorizontalWrapMode.Wrap;

                _cards.Add(rect);
            }
        }

        private void HideCards()
        {
            foreach (RectTransform r in _cards) UiKit.Discard(r.gameObject);
            _cards.Clear();
            foreach (RectTransform r in _rays) UiKit.Discard(r.gameObject);
            _rays.Clear();
            foreach (Image g in _yellowGlows) UiKit.Discard(g.gameObject);
            _yellowGlows.Clear();
        }

        /// <summary>카드가 하나씩 튀어 오른다(0.07초 간격, 살짝 넘쳤다 돌아옴).</summary>
        private void AnimateCards()
        {
            for (int i = 0; i < _rays.Count; i++)
            {
                _rays[i].localRotation = Quaternion.Euler(0f, 0f, (_time * 25f) + (i * 45f));
            }
            foreach (Image g in _yellowGlows)
            {
                float beat = 0.5f + (0.5f * Mathf.Sin(_time * 5f));
                g.color = new Color(1f, 0.8f, 0.2f, 0.18f + (0.12f * beat));
                g.rectTransform.localScale = Vector3.one * Mathf.Clamp01(_cardAge / 0.3f) * (0.95f + (0.08f * beat));
            }
            if (_cards.Count > 1) _cards[1].localScale = Vector3.one * (1f + (0.05f * Mathf.Sin(_time * 6f)));
            for (int i = 2; i < _cards.Count; i++)
            {
                float t = Mathf.Clamp01((_cardAge - ((i - 2) * 0.07f)) / 0.25f);
                float s = t < 0.7f ? Mathf.Lerp(0.3f, 1.08f, t / 0.7f) : Mathf.Lerp(1.08f, 1f, (t - 0.7f) / 0.3f);
                float hover = 1f + (0.015f * Mathf.Sin((_time * 4f) + i));
                _cards[i].localScale = Vector3.one * s * hover;
            }
        }

        /// <summary>속이 빈 고리(충격파). 가장자리가 부드럽다.</summary>
        private static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            const int n = 128;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = ((x + 0.5f) / n * 2f) - 1f;
                    float dy = ((y + 0.5f) / n * 2f) - 1f;
                    float d = Mathf.Abs(Mathf.Sqrt((dx * dx) + (dy * dy)) - 0.85f);
                    float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / 0.13f));
                    pixels[(y * n) + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _ringSprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return _ringSprite;
        }

        /// <summary>물줄기 몸통. 가운데가 밝고 옆과 양 끝이 흐리다. 세로로 늘려 쓴다.</summary>
        /// <summary>물거품: 속이 비치고 테두리가 밝은 원 + 왼쪽 위 하이라이트.</summary>
        /// <summary>무지개 반원 띠(64×32): 바깥 빨강 → 안 보라, 가장자리는 부드럽게 빠진다. 위(+y)가 둥근 쪽.</summary>
        private static Sprite RainbowSprite()
        {
            if (_rainbowSprite != null) return _rainbowSprite;
            const int w = 64;
            const int h = 32;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[w * h];
            Color[] bands = { new Color(0.6f, 0.3f, 1f), new Color(0.3f, 0.5f, 1f), new Color(0.3f, 0.9f, 0.5f), new Color(1f, 0.95f, 0.3f), new Color(1f, 0.6f, 0.2f), new Color(1f, 0.25f, 0.25f) };
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = ((x + 0.5f) / w * 2f) - 1f;
                    float dy = (y + 0.5f) / h;
                    float r = Mathf.Sqrt((dx * dx) + (dy * dy));
                    float t = (r - 0.6f) / 0.35f;
                    if (t < 0f || t > 1f)
                    {
                        pixels[(y * w) + x] = new Color32(255, 255, 255, 0);
                        continue;
                    }
                    float f = t * (bands.Length - 1);
                    int i = Mathf.Min(bands.Length - 2, (int)f);
                    Color c = Color.Lerp(bands[i], bands[i + 1], f - i);
                    float edge = Mathf.Sin(t * Mathf.PI) * Mathf.Clamp01(dy * 3f);
                    pixels[(y * w) + x] = new Color(c.r, c.g, c.b, edge);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _rainbowSprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0f));
            return _rainbowSprite;
        }

        private static Sprite BubbleSprite()
        {
            if (_bubbleSprite != null) return _bubbleSprite;
            const int n = 32;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = ((x + 0.5f) / n * 2f) - 1f;
                    float dy = ((y + 0.5f) / n * 2f) - 1f;
                    float r = Mathf.Sqrt((dx * dx) + (dy * dy));
                    float rim = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Abs(r - 0.78f) / 0.18f));
                    float fill = r < 0.8f ? 0.18f : 0f;
                    float hx = dx + 0.32f;
                    float hy = dy - 0.32f;
                    float shine = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Sqrt((hx * hx) + (hy * hy)) / 0.22f));
                    float a = Mathf.Clamp01(Mathf.Max(rim, Mathf.Max(fill, shine)));
                    pixels[(y * n) + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _bubbleSprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return _bubbleSprite;
        }

        /// <summary>
        /// 물줄기 결 텍스처(가로 = 폭, 세로 = 길이 방향으로 이어 붙는다). 옆 가장자리는 부드럽게 빠지고 테두리가 살짝 밝다.
        /// 폭을 가로지르는 몇 가닥의 결마다 밝기 덩이가 다른 박자로 놓여 있어서, UV를 흘리면 물살이 앞으로 흘러가 보인다.
        /// </summary>
        private static Texture2D FlowTexture()
        {
            if (_flowTexture != null) return _flowTexture;
            const int w = 64;
            const int h = 128;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapModeU = TextureWrapMode.Clamp,
                wrapModeV = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[w * h];
            for (int x = 0; x < w; x++)
            {
                float dx = ((x + 0.5f) / w * 2f) - 1f;
                float ax = Mathf.Abs(dx);
                float edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((ax - 0.55f) / 0.45f));
                float rim = Mathf.Exp(-Mathf.Pow((ax - 0.72f) / 0.08f, 2f)) * 0.18f;
                // 가닥마다 박자(위상)와 세기가 다르다.
                float lane = Noise((dx * 7f) + 20f, 5);
                float phase = Noise((dx * 5f) + 40f, 6);
                for (int y = 0; y < h; y++)
                {
                    float ty = (y + 0.5f) / h;
                    // 정수 배 주기라 세로로 이어 붙여도 이음매가 없다.
                    float lump = (0.5f + (0.5f * Mathf.Sin(2f * Mathf.PI * ((ty * 2f) + phase))))
                        * (0.6f + (0.4f * Mathf.Sin(2f * Mathf.PI * ((ty * 5f) + (phase * 3f)))));
                    float a = edge * (0.62f + (0.38f * lane * lump)) + rim;
                    pixels[(y * w) + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _flowTexture = texture;
            return _flowTexture;
        }

        /// <summary>가장자리가 부드러운 꽉 찬 원. 물줄기 이음매와 장갑에 쓴다.</summary>
        private static Sprite DiscSprite()
        {
            if (_discSprite != null) return _discSprite;
            const int n = 64;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = ((x + 0.5f) / n * 2f) - 1f;
                    float dy = ((y + 0.5f) / n * 2f) - 1f;
                    float r = Mathf.Sqrt((dx * dx) + (dy * dy));
                    float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - 0.6f) / 0.4f));
                    pixels[(y * n) + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _discSprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return _discSprite;
        }

        private static float Hash(int x, int y)
        {
            uint n = (uint)((x * 374761393) + (y * 668265263));
            n = (n ^ (n >> 13)) * 1274126177u;
            return ((n ^ (n >> 16)) & 0xffff) / 65535f;
        }

        /// <summary>1차원 값 노이즈(0..1). 정수 격자 사이를 부드럽게 잇는다.</summary>
        private static float Noise(float t, int seed)
        {
            int i = Mathf.FloorToInt(t);
            float f = t - i;
            float u = f * f * (3f - (2f * f));
            return Mathf.Lerp(Hash(i, seed * 131), Hash(i + 1, seed * 131), u);
        }

        private static Sprite BeamSprite()
        {
            if (_beamSprite != null) return _beamSprite;
            const int n = 32;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = ((x + 0.5f) / n * 2f) - 1f;
                    float ty = (y + 0.5f) / n;
                    float side = Mathf.Pow(Mathf.Clamp01(1f - (dx * dx)), 1.5f);
                    float ends = Mathf.SmoothStep(0f, 1f, Mathf.Min(ty, 1f - ty) / 0.2f);
                    pixels[(y * n) + x] = new Color32(255, 255, 255, (byte)(side * ends * 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _beamSprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return _beamSprite;
        }

        /// <summary>가장자리만 진한 원형 그라데이션. 색은 Image.color로 입힌다.</summary>
        private static Sprite VignetteSprite()
        {
            const int n = 128;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = ((x + 0.5f) / n * 2f) - 1f;
                    float dy = ((y + 0.5f) / n * 2f) - 1f;
                    float d = Mathf.Sqrt((dx * dx) + (dy * dy)) / 1.414f;
                    float a = Mathf.Clamp01((d - 0.45f) / 0.55f);
                    pixels[(y * n) + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }
    }
}
