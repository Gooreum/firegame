using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using FireGame.Core.Sim;
using FireGame.UnityLayer.Feel;
using UnityEngine;

namespace FireGame.UnityLayer
{
    /// <summary>
    /// 현장 격자를 스프라이트로 그린다. 로직은 전혀 없고 StageRunner의 상태를 비출 뿐이다.
    ///
    /// MonoBehaviour가 아니라 일반 클래스라서, 게임에서는 GameRoot가 매 프레임 Refresh를 부르고
    /// 에디터 스크린샷 하네스는 원하는 순간에 직접 부른다.
    ///
    /// 좌표: 격자 한 칸 = 월드 1단위. 격자는 위가 0행이지만 월드는 위가 +y라 세로를 뒤집는다.
    /// </summary>
    public sealed class MissionWorldView
    {
        // 그리는 순서. 숫자가 클수록 위에 그려진다.
        private const int OrderFloor = 0;
        private const int OrderProp = 1;
        private const int OrderWall = 2;
        private const int OrderOverlay = 4;
        private const int OrderGlow = 5;
        private const int OrderPeople = 10;
        private const int OrderSpray = 25;   // 물은 불꽃 위에 보여야 "불에 뿌린다"로 읽힌다
        private const int OrderFire = 20;
        private const int OrderAim = 23;     // 조준 표시는 불 위. 불 밑에 깔면 정작 불타는 칸에서 안 보인다
        private const int OrderSmoke = 30;

        /// <summary>
        /// 연기 장막. 불꽃 연출(20~30) 위에 깔아야 불이 연기 너머로 흐릿해 보인다.
        /// 이건 <b>칸에 실제로 찬 연기 농도</b>고, OrderSmoke의 뭉게구름은 연소 칸의 연출이다.
        /// </summary>
        private const int OrderHaze = 32;

        /// <summary>
        /// 안개. 아직 못 본 건물 칸을 덮는다.
        ///
        /// <b>반드시 지붕(BuildingOverlay 36~45)보다 아래여야 한다.</b>
        /// 위에 두면 밖에서 볼 때 건물이 통째로 검은 사각형이 된다 —
        /// 지붕도, 앞벽도, 창문도 전부 안개에 묻힌다.
        /// 밖에서 건물 속이 안 보이는 것은 이미 지붕이 하는 일이고,
        /// 안개가 할 일은 <b>들어갔을 때</b> 아직 못 본 곳을 덮는 것뿐이다.
        /// </summary>
        private const int OrderFog = 34;

        /// <summary>팝업은 지붕(BuildingOverlay.OrderRoof = 40)보다 위여야 건물 위에서도 읽힌다.</summary>
        private const int OrderPopup = 60;

        /// <summary>"불이 코앞"으로 보는 거리(칸). <see cref="FireNearby"/> 주석 참고.</summary>
        private const int FireWarnRadius = 2;

        /// <summary>
        /// 시선 기울기. 정수리에서 수직으로 내려다보던 것을 이만큼 눕혀 건물 앞면이 보이게 한다.
        ///
        /// 카메라 오브젝트는 돌리지 않는다. 정사영 카메라를 x축으로 θ만큼 기울인 그림은
        /// <b>땅을 cosθ만큼 누르고 높이를 sinθ만큼 위로 세운 그림과 수학적으로 같다</b>.
        /// 이 방식이면 (a) 스프라이트가 저절로 화면을 마주 봐 빌보딩이 필요 없고,
        /// (b) 조준·이동이 쓰는 격자 좌표계가 한 줄도 안 바뀐다.
        /// 45도를 넘기면 땅이 너무 눌려 격자가 안 읽히고, 30도 밑이면 벽이 안 보인다.
        /// </summary>
        private const float TiltDegrees = 38f;

        /// <summary>격자 한 칸의 화면 세로 길이. 땅에 깔리는 것은 전부 이만큼 눌린다.</summary>
        public static readonly float Squash = Mathf.Cos(TiltDegrees * Mathf.Deg2Rad);

        /// <summary>높이 한 칸이 화면에서 위로 서는 길이.</summary>
        public static readonly float Rise = Mathf.Sin(TiltDegrees * Mathf.Deg2Rad);

        /// <summary>서 있는 것의 밑동을 눌린 칸 아래 모서리에 맞추는 보정.</summary>
        public static readonly float StandLift = (1f - Squash) * 0.5f;

        // 방수 연출 수명(초)
        private const float StreakLifetime = 0.22f;
        private const float DropLifetime = 0.45f;
        private const float SplashLifetime = 0.45f;
        private const float SteamLifetime = 0.9f;

        /// <summary>맞은 칸 하나에 날리는 물방울 수. 1개일 때는 "찔끔찔끔"으로 보였다.</summary>
        private const int DropsPerCell = 4;

        private readonly StageRunner _runner;
        private readonly FireGrid _grid;
        private readonly Transform _root;

        /// <summary>맵에서 되찾아 낸 건물들. 실내·실외 판정과 지붕·카메라의 근거다.</summary>
        private BuildingMap _buildings;

        /// <summary>건물을 덮는 지붕. 소방관이 들어간 건물만 걷힌다.</summary>
        private BuildingOverlay _roofView;

        private readonly SpriteRenderer[] _floor;
        private readonly SpriteRenderer[] _top;
        private readonly SpriteRenderer[] _overlay;
        private readonly SpriteRenderer[] _glow;
        private readonly SpriteRenderer[] _body;
        private readonly SpriteRenderer[] _flame;
        private readonly SpriteRenderer[] _tongue;
        private readonly SpriteRenderer[] _smoke;
        private readonly SpriteRenderer[] _haze;
        private readonly SpriteRenderer[] _fog;
        private readonly Color[] _topBaseColor;
        private readonly bool[] _isWall;

        private readonly SpriteRenderer _player;

        /// <summary>
        /// 지붕 뒤로 들어간 소방관의 윤곽. 시선을 눕히면 건물 북쪽 한두 줄이 지붕에 가리는데,
        /// 상가 아래 블록은 하필 스폰이 있는 줄 위에 있어 소방관이 통째로 사라진다.
        /// </summary>
        private readonly SpriteRenderer _playerGhost;
        private readonly List<SpriteRenderer> _civilians = new List<SpriteRenderer>();

        /// <summary>지금 든 장비가 맞힐 칸을 바닥에 깔아 보여 주는 판. 쓰는 만큼만 늘린다.</summary>
        private readonly List<SpriteRenderer> _aimCells = new List<SpriteRenderer>();

        /// <summary>구조를 기다리는 시민 머리 위 표식. 후광 위에 글자를 얹는다.</summary>
        private readonly List<SpriteRenderer> _civilianMarks = new List<SpriteRenderer>();
        private readonly List<TextMesh> _civilianCalls = new List<TextMesh>();

        /// <summary>업고 있을 때만 켜지는 출구 길잡이. 점 세 개가 출구 쪽으로 차례로 밝아진다.</summary>
        private const int ExitDotCount = 3;

        /// <summary>이 농도부터 연기를 그린다. 더 옅은 것까지 그리면 화면만 뿌예진다.</summary>
        private const float HazeFloor = 0.12f;
        private readonly SpriteRenderer[] _exitDots = new SpriteRenderer[ExitDotCount];
        private SpriteRenderer _exitPulse;

        /// <summary>
        /// 아직 못 찾은 시민 쪽을 가리키는 점들. 출구 길잡이와 같은 방식이다.
        /// <b>방향만 준다</b> — 거리를 주면 찾을 필요가 없어지고, 아예 안 주면
        /// 40x22 맵을 벽마다 더듬어야 해서 탐색이 아니라 노동이 된다.
        /// </summary>
        private readonly SpriteRenderer[] _searchDots = new SpriteRenderer[ExitDotCount];

        private readonly Sprite[] _flameSprites;

        /// <summary>전기 불(C급)의 질감. 불꽃 대신 튀는 스파크를 얹어 한눈에 구분되게 한다.</summary>
        private readonly Sprite[] _sparkSprites;
        private readonly Sprite _tongueSprite;
        private readonly Sprite[] _smokeSprites;
        private readonly Sprite[] _scorchSprites;

        private readonly List<Particle> _particles = new List<Particle>();
        private readonly Stack<SpriteRenderer> _particlePool = new Stack<SpriteRenderer>();
        private readonly List<GridPoint> _hitBuffer = new List<GridPoint>();
        private readonly float[] _lastCooldowns = new float[PlayerState.SlotCount];

        // "N칸 진압!" 글자. 한 발에 여러 칸을 끄면 레벨업(특히 넓어진 부채꼴)이 손에 잡힌다.
        private readonly List<Popup> _popups = new List<Popup>();
        private int _lastShotsFired;

        // 소리와 화면 흔들림. 언제 무엇을 낼지는 FeelTracker가 정하고 여기는 틀기만 한다.
        private readonly FeelTracker _feel;
        private float _trauma;

        private struct Popup
        {
            public TextMesh Text;
            public TextMesh Shadow;
            public Vector3 From;
            public float Age;
        }

        private const float PopupLifetime = 0.9f;

        /// <summary>물보라가 레벨 따라 커지는 건 Lv11까지. 그 뒤로 더 키우면 화면을 덮는다.</summary>
        private const int MaxSprayBoost = 10;
        private readonly bool[] _wasBurning;

        // 흩뿌림 위치. 시드를 고정해 같은 장면은 같은 모양으로 찍힌다.
        private readonly System.Random _jitter = new System.Random(11);

        private readonly Sprite _dropSprite;
        private readonly Sprite _streakSprite;
        private readonly Sprite _splashSprite;
        private readonly Sprite _cloudSprite;
        private readonly Sprite _steamSprite;

        private enum ParticleKind : byte
        {
            /// <summary>손에서 목표 칸으로 날아가는 물방울·가스 덩어리.</summary>
            Drop,

            /// <summary>손과 목표 칸을 잇는 물줄기 선.</summary>
            Streak,

            /// <summary>목표 칸에서 퍼지는 물보라 고리.</summary>
            Splash,

            /// <summary>불이 꺼진 칸에서 피어오르는 김.</summary>
            Steam,
        }

        private struct Particle
        {
            public SpriteRenderer Renderer;
            public ParticleKind Kind;
            public float Age;
            public float Delay;
            public float Lifetime;
            public Vector3 From;
            public Vector3 To;
            public float StartSize;
            public float EndSize;
            public float Alpha;
        }

        public MissionWorldView(Transform parent, StageRunner runner)
        {
            _runner = runner;
            _grid = runner.Grid;
            _feel = new FeelTracker(runner);

            _root = new GameObject("MissionWorld").transform;
            _root.SetParent(parent, false);

            int count = _grid.Count;
            _floor = new SpriteRenderer[count];
            _top = new SpriteRenderer[count];
            _overlay = new SpriteRenderer[count];
            _glow = new SpriteRenderer[count];
            _body = new SpriteRenderer[count];
            _flame = new SpriteRenderer[count];
            _tongue = new SpriteRenderer[count];
            _smoke = new SpriteRenderer[count];
            _haze = new SpriteRenderer[count];
            _fog = new SpriteRenderer[count];
            _topBaseColor = new Color[count];
            _isWall = new bool[count];

            // flame_01~04는 희미한 연기 같은 모양이라 불로 읽히지 않는다.
            // 불의 몸통은 fire_01/02(밝게 타오르는 덩어리), 위로 솟는 불길은 flame_05로 만든다.
            _flameSprites = new[] { Art.Get("Effects/fire_01"), Art.Get("Effects/fire_02") };
            _sparkSprites = LoadSeries("Effects/spark_0", 1, 4);
            _tongueSprite = Art.Get("Effects/flame_05");
            _smokeSprites = LoadSeries("Effects/smoke_0", 1, 5);
            _scorchSprites = LoadSeries("Effects/scorch_0", 1, 3);

            _dropSprite = Art.Get("Effects/water_drop");
            _streakSprite = Art.Get("Effects/water_trace");
            _splashSprite = Art.Get("Effects/glow");
            _cloudSprite = Art.Get("Effects/smoke_01");
            _steamSprite = Art.Get("Effects/smoke_01");

            _wasBurning = new bool[count];
            for (int i = 0; i < count; i++) _wasBurning[i] = _grid.Cells[i].State == CellState.Burning;

            BuildTiles();

            // 지붕은 바닥·벽을 다 깐 뒤에 덮는다.
            _roofView = new BuildingOverlay(_root, runner, _buildings, this);

            // 방화복 레벨마다 옷 색·헬멧이 다른 그림(tools/import-art.py가 만든다)
            _player = CreateRenderer("Player", Art.Get(SuitSprite(runner.Player.SuitLevel)), OrderPeople + 1);
            _playerGhost = CreateRenderer("PlayerGhost", _player.sprite, BuildingOverlay.OrderGhost);
            _playerGhost.enabled = false;
            string[] civilianSprites = { "TopDown/civilian_woman", "TopDown/civilian_old", "TopDown/civilian_man" };
            for (int i = 0; i < runner.Civilians.Count; i++)
            {
                _civilians.Add(CreateRenderer("Civilian" + i, Art.Get(civilianSprites[i % civilianSprites.Length]), OrderPeople));
                _civilianMarks.Add(CreateRenderer("CivilianMark" + i, Art.Get("Effects/glow"), OrderAim + 1));
                _civilianCalls.Add(CreateText("CivilianCall" + i, "!", Color.white, OrderAim + 2));
            }

            for (int i = 0; i < PlayerState.SlotCount; i++) _lastCooldowns[i] = runner.Player.Cooldowns[i];
        }

        public Transform Root
        {
            get { return _root; }
        }

        public int Width
        {
            get { return _grid.Width; }
        }

        public int Height
        {
            get { return _grid.Height; }
        }

        /// <summary>
        /// 격자 칸의 중심 월드 좌표. 세로만 <see cref="Squash"/>만큼 눌린다 —
        /// 가로는 그대로라 격자 x가 곧 화면 x다.
        /// </summary>
        public Vector3 CellCenter(int x, int y)
        {
            return new Vector3(x + 0.5f, (_grid.Height - y - 0.5f) * Squash, 0f);
        }

        /// <summary>셀 단위 실수 좌표(StageRunner 좌표)를 월드 좌표로.</summary>
        public Vector3 ToWorld(float x, float y)
        {
            return new Vector3(x, (_grid.Height - y) * Squash, 0f);
        }

        /// <summary>지면 좌표에서 <paramref name="height"/>칸만큼 높은 자리.</summary>
        public static Vector3 Lift(Vector3 ground, float height)
        {
            return new Vector3(ground.x, ground.y + (height * Rise), ground.z);
        }

        /// <summary>
        /// 땅에 깔리는 것. 세로를 <see cref="Squash"/>만큼 눌러야 칸과 칸이 딱 맞물린다.
        /// 누르지 않으면 타일이 서로 겹쳐 격자가 두 겹으로 보인다.
        /// 바닥·벽 윗면·기름·문·출구·급수전·마당 소품·조준 표시가 여기 든다.
        /// </summary>
        public static void LayFlat(Transform target)
        {
            Vector3 scale = target.localScale;
            target.localScale = new Vector3(scale.x, scale.y * Squash, scale.z);
        }

        /// <summary>
        /// 서 있는 것. 세로를 누르지 않고 눌린 칸의 아래 모서리에 밑동만 맞춘다.
        /// 불꽃·연기·소방관·시민은 위로 서 있어야지 바닥에 누우면 안 된다.
        /// </summary>
        public static Vector3 StandOn(Vector3 ground)
        {
            return new Vector3(ground.x, ground.y + StandLift, ground.z);
        }

        public Vector3 PlayerWorld
        {
            get { return ToWorld(_runner.Player.X, _runner.Player.Y); }
        }

        public void Destroy()
        {
            // 지도로 돌아가면 불 소리가 남으면 안 된다.
            GameAudio.SetFireLevel(0f, 1f);
            UiKit.Discard(_root.gameObject);
        }

        // ------------------------------------------------------------------
        // 바닥·벽 구성
        // ------------------------------------------------------------------

        /// <summary>실내·실외 바닥 그림. a/b는 섞어 깔아 단조로움을 깬다(변형이 없으면 같은 그림).</summary>
        private readonly struct FloorTheme
        {
            public readonly string IndoorA;
            public readonly string IndoorB;
            public readonly string OutdoorA;
            public readonly string OutdoorB;

            public FloorTheme(string indoorA, string indoorB, string outdoorA, string outdoorB)
            {
                IndoorA = indoorA;
                IndoorB = indoorB;
                OutdoorA = outdoorA;
                OutdoorB = outdoorB;
            }
        }

        /// <summary>현장 id별 바닥 분위기. 없는 id는 주택(0)을 쓴다.</summary>
        private static readonly Dictionary<int, FloorTheme> FloorThemes = new Dictionary<int, FloorTheme>
        {
            { 0, new FloorTheme("floor_wood_a", "floor_wood_b", "grass_a", "grass_b") },               // 주택: 나무 마루 + 잔디
            { 1, new FloorTheme("floor_tile_a", "floor_tile_b", "grass_a", "grass_b") },               // 상가: 타일
            { 2, new FloorTheme("floor_wood_a", "floor_wood_b", "dirt", "dirt") },                     // 주유소: 흙 마당
            { 3, new FloorTheme("floor_stone_a", "floor_stone_b", "dirt", "dirt_b") },                 // 창고: 돌바닥 + 흙 마당
            { 4, new FloorTheme("floor_tile_a", "floor_tile_b", "brick_a", "brick_b") },               // 공장: 타일 + 벽돌 마당
            { 5, new FloorTheme("floor_wood_a", "floor_wood_b", "floor_stone_a", "floor_stone_b") },   // 항구: 목조 창고 + 돌 부두
        };

        private void BuildTiles()
        {
            // 실내·실외 판정과 건물 목록의 출처는 코어 하나다.
            // 현장 도중에 화면을 다시 만들면 소방관이 실내에 있을 수 있어, 지금 위치가 아니라
            // 맵에 정해진 시작 지점을 기준으로 삼는다.
            _buildings = BuildingMap.From(_grid, MapLoader.Parse(_runner.Def.Map).PlayerSpawn);

            FloorTheme theme;
            if (!FloorThemes.TryGetValue(_runner.Def.Id, out theme)) theme = FloorThemes[0];

            SiteTheme site = SiteTheme.Of(_runner.Def.Id);

            // 실내 바닥은 켄니 타일 그대로 쓴다 — 마루·타일·돌은 이미 실내답다.
            string indoorA = "TopDown/" + theme.IndoorA;
            string indoorB = "TopDown/" + theme.IndoorB;

            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    int i = _grid.Index(x, y);
                    var material = (MaterialId)_grid[x, y].Material;

                    // 실외는 4변형을 섞어 깐다. 2변형뿐일 때는 넓은 마당이 같은 그림으로 뒤덮였다.
                    int variant = Art.GroundVariant(site.Ground, x, y);
                    bool alternate = ((x * 7) + (y * 13)) % 5 == 0;

                    // 어떤 건물에도 안 속하면 바깥 마당이다. 마당의 급수전처럼 통행 불가라
                    // 실외 탐색이 닿지 못한 칸도 여기서는 마당으로 친다 — 잔디 위 급수전이
                    // 저 혼자 마루를 깔고 서 있으면 안 된다.
                    bool outdoors = _buildings.At(x, y) == BuildingMap.None;
                    // 어떤 건물에도 안 속하면 바깥 마당이다. 마당의 급수전처럼 통행 불가라
                    // 실외 탐색이 닿지 못한 칸도 여기서는 마당으로 친다 — 잔디 위 급수전이
                    // 저 혼자 마루를 깔고 서 있으면 안 된다.
                    Sprite floor = outdoors
                        ? Art.GroundTexture(site.Ground, variant)
                        : Art.Get(alternate ? indoorB : indoorA);

                    _floor[i] = CreateRenderer("Floor", floor, OrderFloor);
                    _floor[i].transform.position = CellCenter(x, y);
                    LayFlat(_floor[i].transform);

                    Sprite top = null;
                    int order = OrderProp;
                    Color tint = Color.white;
                    float rotation = 0f;

                    switch (material)
                    {
                        case MaterialId.Wood:
                            top = Art.Wall(WallStyle.Wood, WallMask(x, y));
                            order = OrderWall;
                            _isWall[i] = true;
                            break;

                        case MaterialId.Concrete:
                            // 격자 맨 바깥 한 줄은 벽이 아니라 그 현장의 경계다.
                            // 게임에 아무 영향이 없는 칸이라, 항구 방파제를 여기에 그리면
                            // 맵을 한 칸도 안 고치고 물가가 생긴다.
                            bool onBorder = x == 0 || y == 0 || x == _grid.Width - 1 || y == _grid.Height - 1;
                            top = onBorder
                                ? Art.BorderTexture(site.Border)
                                : Art.Wall(WallStyle.Concrete, WallMask(x, y));
                            order = OrderWall;
                            _isWall[i] = true;
                            break;

                        case MaterialId.Oil:
                            top = Art.Get("TopDown/oil_puddle");
                            break;

                        case MaterialId.Electric:
                            top = Art.Get("TopDown/electric_panel");
                            order = OrderWall;
                            break;

                        case MaterialId.Hydrant:
                            top = Art.Get("TopDown/hydrant");
                            tint = new Color(0.95f, 0.2f, 0.2f);
                            break;

                        case MaterialId.Door:
                            // 좌우에 벽이 있으면 가로로 놓인 문이다.
                            bool horizontal = IsWallAt(x - 1, y) || IsWallAt(x + 1, y);
                            top = Art.Get(horizontal ? "TopDown/door_horizontal" : "TopDown/door_vertical");
                            order = OrderWall;
                            break;

                        case MaterialId.Exit:
                            // 잔디 위 초록은 눈에 안 띄므로 노란빛으로 칠한다.
                            top = Art.White;
                            tint = new Color(1f, 0.92f, 0.35f, 0.6f);
                            break;

                        case MaterialId.Scenery:
                            // 칸 하나로 그리지 않는다. 붙어 있는 칸을 덩어리로 묶어
                            // 물건 하나를 얹는다 — BuildScenery()가 한다.
                            break;
                    }

                    if (top != null)
                    {
                        _top[i] = CreateRenderer("Top", top, order);
                        _top[i].transform.position = CellCenter(x, y);
                        _top[i].transform.rotation = Quaternion.Euler(0f, 0f, rotation);
                        LayFlat(_top[i].transform);
                        _top[i].color = tint;
                        _topBaseColor[i] = tint;
                    }
                }
            }

            BuildScenery();
        }

        /// <summary>
        /// 소방관 시작 위치에서 문을 지나지 않고 갈 수 있는 바닥 = 바깥.
        /// 바깥은 잔디·흙, 안쪽은 마루·타일로 깔아 건물 윤곽이 보이게 한다.
        /// </summary>

        /// <summary>
        /// 마당의 장식물을 물건으로 세운다.
        ///
        /// 맵 문자에는 <c>o</c> 한 종류뿐이다. 무엇으로 보이는가는 현장과
        /// <b>붙어 있는 덩어리의 칸 수</b>가 정한다 — 맵 문자열의 <c>o</c>와 <c>oo</c>가
        /// 눈에 보이는 그대로 다른 물건이 된다.
        /// 1칸이면 나무·콘·계선주, 2~3칸이면 주유기 섬·차량, 4칸 이상이면 컨테이너·탱크.
        /// </summary>
        private void BuildScenery()
        {
            SiteTheme theme = SiteTheme.Of(_runner.Def.Id);
            var taken = new bool[_grid.Count];
            var clumps = new List<List<GridPoint>>();

            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    int i = _grid.Index(x, y);
                    if (taken[i] || (MaterialId)_grid[x, y].Material != MaterialId.Scenery) continue;

                    clumps.Add(SceneryClump(new GridPoint(x, y), taken));
                }
            }

            // 스폰에 가장 가까운 덩어리는 어느 현장이든 소방차다. 출동해서 내린 자리가 된다.
            // 칸 수로 고르면 두 칸짜리 주유기 섬과 구별되지 않는다.
            GridPoint spawn = MapLoader.Parse(_runner.Def.Map).PlayerSpawn;
            int nearest = -1;
            float best = float.MaxValue;

            for (int i = 0; i < clumps.Count; i++)
            {
                foreach (GridPoint c in clumps[i])
                {
                    float d = Mathf.Abs(c.X - spawn.X) + Mathf.Abs(c.Y - spawn.Y);
                    if (d >= best) continue;
                    best = d;
                    nearest = i;
                }
            }

            for (int i = 0; i < clumps.Count; i++)
            {
                PropLook look = i == nearest ? SiteTheme.FireTruck : theme.Props.For(clumps[i].Count);
                PlaceProp(look, clumps[i]);
            }
        }

        /// <summary>맞닿은 장식물 칸을 4방향으로 모은다.</summary>
        private List<GridPoint> SceneryClump(GridPoint start, bool[] taken)
        {
            var clump = new List<GridPoint>();
            var queue = new Queue<GridPoint>();
            taken[_grid.Index(start.X, start.Y)] = true;
            queue.Enqueue(start);

            int[] dx = { 0, 0, -1, 1 };
            int[] dy = { -1, 1, 0, 0 };

            while (queue.Count > 0)
            {
                GridPoint p = queue.Dequeue();
                clump.Add(p);

                for (int k = 0; k < 4; k++)
                {
                    int nx = p.X + dx[k];
                    int ny = p.Y + dy[k];
                    if (!_grid.InBounds(nx, ny)) continue;

                    int n = _grid.Index(nx, ny);
                    if (taken[n] || (MaterialId)_grid[nx, ny].Material != MaterialId.Scenery) continue;

                    taken[n] = true;
                    queue.Enqueue(new GridPoint(nx, ny));
                }
            }

            return clump;
        }

        /// <summary>덩어리가 차지한 자리에 물건 하나와 바닥 그림자를 놓는다.</summary>
        private void PlaceProp(PropLook look, List<GridPoint> clump)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            foreach (GridPoint c in clump)
            {
                if (c.X < minX) minX = c.X;
                if (c.Y < minY) minY = c.Y;
                if (c.X > maxX) maxX = c.X;
                if (c.Y > maxY) maxY = c.Y;
            }

            float w = maxX - minX + 1;
            float h = maxY - minY + 1;
            var center = new Vector3(
                (minX + maxX + 1) * 0.5f,
                (_grid.Height - ((minY + maxY + 1) * 0.5f)) * Squash,
                0f);

            // 위아래가 있는 그림은 덩어리가 누운 방향에 맞춰 돌린다.
            // 그냥 "세로로 긴 덩어리면 돌린다"로 뒀더니, 세로로 긴 차량 그림이
            // 가로 두 칸짜리 자리에 선 채로 들어가 반 칸으로 쪼그라들었다(캡처로 확인).
            Vector3 raw = look.Sprite.bounds.size;
            bool tallSprite = raw.y > raw.x;
            bool tallBox = h > w;
            bool turn = look.Upright && tallSprite != tallBox;
            float alongX = turn ? h : w;
            float alongY = turn ? w : h;

            Sprite sprite = look.Sprite;
            Vector3 size = raw;
            float fitX = size.x > 0f ? (alongX * look.Fill) / size.x : 1f;
            float fitY = size.y > 0f ? (alongY * look.Fill) / size.y : 1f;

            // 코드로 찍은 소품은 칸을 채우라고 만든 것이라 상자에 늘여 맞춘다.
            // 켄니 그림은 제 비율(차량은 1:2)이 있어 늘이면 찌그러지므로 작은 쪽에 맞춘다.
            if (!look.Stretch)
            {
                float fit = Mathf.Min(fitX, fitY);
                fitX = fit;
                fitY = fit;
            }

            // 그림자를 먼저 깐다. 물건이 땅에 닿아 보인다 — 처마 그림자와 같은 원리다.
            // 네모난 판으로 깔았더니 둥근 소품(계선주·드럼통) 뒤로 네모가 드러나 맨홀처럼 보였다.
            // 가장자리가 흐려지는 원판을 써서 어떤 모양에도 맞게 한다.
            Sprite blob = Art.Get("Effects/glow");
            SpriteRenderer shade = CreateRenderer("PropShadow", blob, OrderProp);
            shade.color = new Color(0f, 0f, 0f, 0.22f);
            shade.transform.position = center + new Vector3(0.12f, -0.12f * Squash, 0f);
            shade.transform.localScale = new Vector3(
                Art.FitWidth(blob, w * 1.15f), Art.FitWidth(blob, h * 1.15f) * Squash, 1f);

            // 켄니 소품은 전부 정수리 시점으로 그린 그림이라 땅에 눕는다.
            // 90도 돌린 소품은 화면 세로가 그림의 가로 축이므로 누르는 축도 따라 바뀐다 —
            // 그냥 localScale.y를 누르면 돌아간 소품이 가로로 납작해진다.
            SpriteRenderer prop = CreateRenderer("Prop", sprite, OrderWall);
            prop.color = look.Tint;
            prop.transform.position = center;
            prop.transform.localScale = turn
                ? new Vector3(fitX * Squash, fitY, 1f)
                : new Vector3(fitX, fitY * Squash, 1f);
            prop.transform.rotation = Quaternion.Euler(0f, 0f, turn ? 90f : 0f);
        }

        private bool IsWallAt(int x, int y)
        {
            if (!_grid.InBounds(x, y)) return false;
            var material = (MaterialId)_grid[x, y].Material;
            return material == MaterialId.Wood || material == MaterialId.Concrete;
        }

        private int WallMask(int x, int y)
        {
            int mask = 0;
            if (IsWallAt(x, y - 1)) mask |= Art.ConnectNorth;
            if (IsWallAt(x + 1, y)) mask |= Art.ConnectEast;
            if (IsWallAt(x, y + 1)) mask |= Art.ConnectSouth;
            if (IsWallAt(x - 1, y)) mask |= Art.ConnectWest;
            return mask;
        }

        // ------------------------------------------------------------------
        // 매 프레임 갱신
        // ------------------------------------------------------------------

        /// <param name="time">누적 시간(애니메이션 위상).</param>
        /// <param name="dt">지난 갱신 이후 시간(물줄기 수명).</param>
        public void Refresh(float time, float dt)
        {
            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    RefreshCell(x, y, time);
                }
            }

            RefreshAimPreview(time);
            RefreshPeople();
            RefreshRescueSigns(time);
            AnnounceRescues();
            DetectShots();
            DetectExtinguished();
            PlayFeel(dt);
            AdvanceParticles(dt);
            AdvancePopups(dt);
            _roofView.Refresh(time, PlayerBuildingId);
        }

        /// <summary>소방관이 지금 들어가 있는 건물. 밖이면 <see cref="BuildingMap.None"/>.</summary>
        public int PlayerBuildingId
        {
            get { return _buildings.At((int)_runner.Player.X, (int)_runner.Player.Y); }
        }

        private void RefreshCell(int x, int y, float time)
        {
            int i = _grid.Index(x, y);
            ref Cell cell = ref _grid[x, y];
            int seed = (x * 73856093) ^ (y * 19349663);

            // 타버린 자리는 어둡게, 젖은 자리는 파랗게 물들인다.
            // 젖음을 반투명 판으로 덮으면 벽·바닥 무늬가 뭉개져 회색 덩어리처럼 보인다.
            // 방화선이 눈에 보여야 그 전술을 쓸 수 있으므로 무늬는 살리고 색만 바꾼다.
            bool burnt = cell.State == CellState.Burnt;
            Color tone = burnt ? new Color(0.4f, 0.4f, 0.4f) : Color.white;
            if (!burnt && cell.Wet > 0f)
            {
                tone = Color.Lerp(Color.white, new Color(0.45f, 0.72f, 1f), Mathf.Clamp01(0.55f + (cell.Wet * 0.45f)));
            }

            _floor[i].color = tone;
            if (_top[i] != null)
            {
                Color baseColor = _topBaseColor[i];
                _top[i].color = new Color(baseColor.r * tone.r, baseColor.g * tone.g, baseColor.b * tone.b, baseColor.a);
            }

            // 덮개: 그을음, 없으면 CO2가 남긴 옅은 안개
            if (burnt)
            {
                SpriteRenderer overlay = Ensure(_overlay, i, x, y, OrderOverlay);
                overlay.sprite = _scorchSprites[Mathf.Abs(seed) % _scorchSprites.Length];
                overlay.color = new Color(0.1f, 0.08f, 0.06f, 0.7f);
                overlay.transform.localScale = Vector3.one * Art.FitWidth(overlay.sprite, 1.2f);
                overlay.enabled = true;
            }
            else if (cell.Inert > 0f)
            {
                SpriteRenderer overlay = Ensure(_overlay, i, x, y, OrderOverlay);
                overlay.sprite = Art.White;
                overlay.color = new Color(0.9f, 0.95f, 1f, Mathf.Clamp01(cell.Inert * 0.12f));
                overlay.transform.localScale = Vector3.one;
                overlay.enabled = true;
            }
            else if (_overlay[i] != null)
            {
                _overlay[i].enabled = false;
            }

            if (cell.State == CellState.Burning)
            {
                float flicker = Mathf.Sin((time * 9f) + (seed % 100)) * 0.12f;
                float intensity = Mathf.Clamp01(cell.Heat / 2f);

                // 불은 등급 색으로 탄다. 전에는 무엇이 타든 같은 주황이라
                // 화면만 보고는 어떤 소화기를 들어야 하는지 알 길이 없었다.
                FireClass fireClass = Materials.Of(cell.Material).Class;
                FirePalette palette = FireLook.Palette(fireClass);
                Color hue = palette.Mark;

                SpriteRenderer glow = Ensure(_glow, i, x, y, OrderGlow);
                glow.sprite = Art.Get("Effects/glow");
                glow.color = new Color(hue.r, hue.g * 0.8f, hue.b * 0.8f, 0.3f + (0.25f * intensity));
                glow.transform.localScale = Vector3.one * Art.FitWidth(glow.sprite, 1.8f + flicker);
                glow.enabled = true;

                // 몸통: 진한 주황 원을 칸보다 살짝 크게 깔아 이웃 불과 이어지게 한다.
                // 팩의 불덩어리 그림(fire_01/02)은 평균 불투명도가 47/255로 너무 옅어 몸통으로는 안 보인다.
                SpriteRenderer body = Ensure(_body, i, x, y, OrderFire);
                body.sprite = Art.Get("Effects/water_drop");
                body.color = Color.Lerp(palette.BodyDim, palette.BodyHot, intensity);
                // 불꽃 크기가 열을 따라간다. 맞을 때마다 줄어드는 게 보여서 "몇 발이면 꺼지는지"가 눈에 들어온다.
                float size = 0.85f + (0.5f * intensity);   // 평소(열 ~0.6) 1.0배, 막 맞으면 0.85배, 한창이면 1.35배
                body.transform.localScale = Vector3.one * Art.FitWidth(body.sprite, (1.7f + (flicker * 0.4f)) * size);
                body.enabled = true;

                // 질감: 옅은 불덩어리를 노랗게 얹고 천천히 돌린다.
                SpriteRenderer flame = Ensure(_flame, i, x, y, OrderFire + 1);
                flame.sprite = fireClass == FireClass.C
                    ? _sparkSprites[Mathf.Abs(seed) % _sparkSprites.Length]
                    : _flameSprites[Mathf.Abs(seed) % _flameSprites.Length];
                flame.color = palette.Texture;
                flame.transform.localScale = Vector3.one * Art.FitWidth(flame.sprite, 1.3f * size);
                flame.transform.rotation = Quaternion.Euler(0f, 0f, (time * 40f) + (seed % 360));
                flame.enabled = true;

                // 불길: 위로 솟아 흔들리는 혀. 칸마다 위치·키·흔들림 위상을 달리해
                // 줄 맞춘 촛불처럼 보이지 않게 한다. 그림 여백이 커서 높이 기준으로 맞춘다.
                float jitterX = (((seed >> 3) & 15) / 15f - 0.5f) * 0.45f;
                float jitterY = (((seed >> 7) & 15) / 15f - 0.5f) * 0.3f;
                float variety = 0.75f + ((((seed >> 11) & 15) / 15f) * 0.5f);

                SpriteRenderer tongue = Ensure(_tongue, i, x, y, OrderFire + 2);
                tongue.sprite = _tongueSprite;
                tongue.color = Color.Lerp(palette.TongueDim, palette.TongueHot, intensity * 0.7f);
                float height = (1.3f + (0.7f * intensity)) * variety * (1f + flicker);
                tongue.transform.localScale = Vector3.one * (height / Mathf.Max(_tongueSprite.bounds.size.y, 0.01f));
                tongue.transform.position = StandOn(CellCenter(x, y))
                    + new Vector3(jitterX + (Mathf.Sin((time * 5f) + seed) * 0.06f), 0.2f + jitterY, 0f);
                tongue.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin((time * 6f) + seed) * 10f);
                tongue.enabled = true;

                SpriteRenderer smoke = Ensure(_smoke, i, x, y, OrderSmoke);
                smoke.sprite = _smokeSprites[Mathf.Abs(seed) % _smokeSprites.Length];
                float drift = Mathf.Repeat((time * 0.6f) + ((seed & 255) / 255f), 1f);
                smoke.transform.position = StandOn(CellCenter(x, y))
                    + new Vector3(0.2f * Mathf.Sin(time + seed), 0.5f + drift, 0f);
                smoke.transform.localScale = Vector3.one * Art.FitWidth(smoke.sprite, 1.0f + drift);
                // 유류 화재는 검은 연기가 특징이다. 멀리서도 "저건 기름"을 알 수 있다.
                Color smokeTint = palette.Smoke;
                smoke.color = new Color(smokeTint.r, smokeTint.g, smokeTint.b, smokeTint.a * (1f - drift));
                smoke.enabled = true;
            }
            else
            {
                if (_body[i] != null) _body[i].enabled = false;
                if (_flame[i] != null) _flame[i].enabled = false;
                if (_tongue[i] != null) _tongue[i].enabled = false;
                if (_glow[i] != null) _glow[i].enabled = false;
                if (_smoke[i] != null) _smoke[i].enabled = false;
            }

            RefreshHaze(i, x, y, cell.Smoke);
            RefreshFog(i, x, y);
        }

        /// <summary>
        /// 칸에 찬 연기를 회색 장막으로 덮는다.
        /// 아주 옅은 연기까지 그리면 화면 전체가 뿌옇게 죽으므로 문턱을 둔다 —
        /// <b>가려지기 시작하는 농도부터</b> 보여야 "저기는 못 들어가겠다"가 읽힌다.
        /// </summary>
        private void RefreshHaze(int i, int x, int y, float smoke)
        {
            if (smoke < HazeFloor)
            {
                if (_haze[i] != null) _haze[i].enabled = false;
                return;
            }

            SpriteRenderer haze = Ensure(_haze, i, x, y, OrderHaze);
            float depth = Mathf.InverseLerp(HazeFloor, 1f, smoke);
            haze.color = new Color(0.36f, 0.37f, 0.40f, 0.20f + (0.62f * depth));
            haze.enabled = true;
        }

        /// <summary>
        /// 아직 못 본 건물 칸을 덮는다. 한 번 본 칸은 옅게 남겨
        /// "아까 봤을 때는 이랬다"를 보여준다 — 같은 방을 매번 다시 더듬게 하지 않는다.
        /// 건물 밖은 덮지 않는다. 마당까지 가리면 탐색이 아니라 더듬기가 된다.
        /// </summary>
        private void RefreshFog(int i, int x, int y)
        {
            if (_buildings.At(x, y) == BuildingMap.None || _runner.Vision.Visible(x, y))
            {
                if (_fog[i] != null) _fog[i].enabled = false;
                return;
            }

            SpriteRenderer fog = Ensure(_fog, i, x, y, OrderFog);
            fog.color = _runner.Vision.Known(x, y)
                ? new Color(0.05f, 0.06f, 0.09f, 0.58f)
                : new Color(0.04f, 0.05f, 0.07f, 1f);
            fog.enabled = true;
        }

        private SpriteRenderer Ensure(SpriteRenderer[] layer, int i, int x, int y, int order)
        {
            if (layer[i] == null)
            {
                layer[i] = CreateRenderer("Fx", Art.White, order);
                layer[i].transform.position = CellCenter(x, y);
                LayFlat(layer[i].transform);
            }
            return layer[i];
        }

        /// <summary>방화복 레벨의 소방관 그림 경로. 범위 밖 레벨은 가까운 끝으로 자른다.</summary>
        public static string SuitSprite(int suitLevel)
        {
            return "TopDown/player_suit_" + GearStats.SuitLook(suitLevel);
        }

        private void RefreshPeople()
        {
            PlayerState player = _runner.Player;
            _player.transform.position = StandOn(PlayerWorld);

            // 캐릭터 그림은 오른쪽을 본다. 조준 방향으로 돌린다.
            float ox = Aiming.OffsetX(player.Aim);
            float oy = -Aiming.OffsetY(player.Aim);
            _player.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(oy, ox) * Mathf.Rad2Deg);
            _player.transform.localScale = Vector3.one * 1.1f;
            _player.color = player.IsAlive ? Color.white : new Color(0.5f, 0.5f, 0.5f);

            RefreshPlayerGhost(player);

            for (int i = 0; i < _civilians.Count; i++)
            {
                Civilian civilian = _runner.Civilians[i];
                SpriteRenderer renderer = _civilians[i];
                renderer.enabled = civilian.Pending;
                if (!civilian.Pending) continue;

                if (civilian.Carried)
                {
                    // 업은 사람은 소방관 어깨 위에서 걸음에 맞춰 흔들린다.
                    float bob = Mathf.Sin(Time.time * 9f) * 0.06f;
                    renderer.transform.position = StandOn(PlayerWorld)
                                                  - (new Vector3(ox, oy, 0f).normalized * 0.28f)
                                                  + new Vector3(0f, 0.22f + bob, 0f);
                    renderer.transform.rotation = _player.transform.rotation;
                    renderer.transform.localScale = Vector3.one * 0.9f;
                    renderer.sortingOrder = OrderPeople;
                }
                else
                {
                    renderer.transform.position = StandOn(ToWorld(civilian.X, civilian.Y));
                    renderer.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
                    renderer.transform.localScale = Vector3.one * 1.05f;
                }
            }
        }

        /// <summary>
        /// 지붕에 가린 소방관을 지붕 위에 윤곽으로 한 번 더 그린다.
        ///
        /// 시선을 눕히면 지붕이 벽 높이만큼 위로 솟아 건물 북쪽 줄을 덮는다. 2.5D의 본질이라
        /// 피할 수 없고, 상가는 소방관 스폰이 바로 그 줄에 있어 실제로 사라진다.
        /// 지붕을 반투명하게 만들면 실내가 비쳐 "들어가야 안다"가 무너지므로, 대신 윤곽만 얹는다.
        /// 건물에 들어갔을 때는 그 지붕이 이미 걷혀 있으니 끈다 — 안에서까지 유령이 따라다니면 안 된다.
        /// </summary>
        private void RefreshPlayerGhost(PlayerState player)
        {
            bool indoors = _buildings.At(player.CellX, player.CellY) != BuildingMap.None;
            bool hidden = !indoors && RoofCovers(player.CellX, player.CellY);

            _playerGhost.enabled = hidden;
            if (!hidden) return;

            _playerGhost.sprite = _player.sprite;
            _playerGhost.transform.position = _player.transform.position;
            _playerGhost.transform.rotation = _player.transform.rotation;
            _playerGhost.transform.localScale = _player.transform.localScale;
            _playerGhost.color = new Color(0.22f, 0.26f, 0.32f, 0.9f);
        }

        /// <summary>
        /// 이 칸이 북쪽 건물의 지붕 밑에 들어가는지. 지붕이 <see cref="BuildingOverlay.WallRise"/>만큼
        /// 솟은 만큼, 눌린 칸 기준 그 몇 줄 뒤까지가 가려진다.
        /// </summary>
        private bool RoofCovers(int x, int y)
        {
            int reach = Mathf.CeilToInt(BuildingOverlay.WallRise / Squash);
            for (int back = 1; back <= reach; back++)
            {
                if (_buildings.At(x, y + back) != BuildingMap.None) return true;
            }

            return false;
        }

        /// <summary>
        /// 구조와 관련된 표식들.
        ///
        /// 기다리는 시민 머리 위에 표식을 띄운다. 평소 노랑, 옆 칸에 불이 붙으면 빨갛게
        /// 빠르게 깜빡여 "저 사람부터"를 알리고, 업을 수 있는 거리면 초록으로 커진다.
        /// 업고 있을 때는 대신 가장 가까운 출구가 빛나고 방향 화살표가 뜬다 —
        /// 사람을 업은 채 출구를 찾아 헤매는 건 긴장이 아니라 짜증이다.
        /// </summary>
        private void RefreshRescueSigns(float time)
        {
            bool carrying = _runner.Player.CarryingCivilian;

            for (int i = 0; i < _civilianMarks.Count; i++)
            {
                Civilian civilian = _runner.Civilians[i];
                SpriteRenderer mark = _civilianMarks[i];

                TextMesh call = _civilianCalls[i];

                // 아직 못 찾은 사람의 자리를 화면이 먼저 알려주면 찾을 이유가 없어진다.
                // 대신 아래 RefreshSearchGuide가 방향만 가리킨다.
                if (!civilian.Pending || civilian.Carried || !civilian.Spotted)
                {
                    mark.enabled = false;
                    call.gameObject.SetActive(false);
                    continue;
                }

                bool ready = ReferenceEquals(_runner.RescueTarget, civilian);

                // 위독은 불이 가까운 것과 다르다. 연기만으로도 사람은 쓰러진다 —
                // 불길이 안 보이는데 급한 사람이 있다는 것이 순서를 고르게 만든다.
                bool danger = civilian.Critical
                    || FireNearby((int)Mathf.Floor(civilian.X), (int)Mathf.Floor(civilian.Y));

                Color color = ready ? new Color(0.3f, 1f, 0.45f)
                    : danger ? new Color(1f, 0.25f, 0.2f)
                    : new Color(1f, 0.85f, 0.2f);

                float pulse = danger ? Mathf.Abs(Mathf.Sin(time * 10f)) : Mathf.Abs(Mathf.Sin(time * 3f));
                Vector3 above = ToWorld(civilian.X, civilian.Y) + new Vector3(0f, 0.6f, 0f);

                mark.color = new Color(color.r, color.g, color.b, 0.45f + (0.4f * pulse));
                mark.transform.position = above;
                mark.transform.localScale = Vector3.one * Art.FitWidth(mark.sprite, ready ? 1.15f : 0.9f);
                mark.enabled = true;

                // 후광만으로는 흐릿해서 글자를 얹는다. 지금 누르면 되는 시민만 "구조"라고 알린다.
                call.text = ready ? "구조" : civilian.Critical ? "위독" : "!";
                call.characterSize = ready || civilian.Critical ? 0.055f : 0.085f;
                call.color = Color.Lerp(Color.white, color, 0.35f + (0.5f * pulse));
                call.transform.position = above;
                call.gameObject.SetActive(true);
            }

            RefreshExitGuide(carrying, time);
            RefreshSearchGuide(carrying, time);
        }

        /// <summary>
        /// 아직 못 찾은 시민 쪽으로 점을 흘려보낸다.
        ///
        /// 시야를 가리고 나니 항구는 시작 시점에 건물 속이 한 칸도 안 보인다.
        /// 아무 실마리 없이 40x22를 벽마다 더듬게 하면 탐색이 아니라 노동이다.
        /// 그래서 <b>방향만</b> 준다 — 거리도 자리도 주지 않으므로 들어가 봐야 안다.
        /// 사람을 업고 있을 때는 끈다. 그때 할 일은 찾기가 아니라 나가기다.
        /// </summary>
        private void RefreshSearchGuide(bool carrying, float time)
        {
            if (_searchDots[0] == null)
            {
                for (int i = 0; i < ExitDotCount; i++)
                {
                    _searchDots[i] = CreateRenderer("SearchDot" + i, Art.Get("Effects/glow"), OrderPeople + 2);
                }
            }

            Civilian target = carrying ? null : NearestUnspotted();
            if (target == null)
            {
                for (int i = 0; i < ExitDotCount; i++) _searchDots[i].enabled = false;
                return;
            }

            Vector3 toTarget = ToWorld(target.X, target.Y) - PlayerWorld;
            Vector3 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.up;

            for (int i = 0; i < ExitDotCount; i++)
            {
                SpriteRenderer dot = _searchDots[i];
                float chase = Mathf.Abs(Mathf.Sin((time * 2.2f) - (i * 0.7f)));

                // 출구 길잡이는 초록이다. 수색은 노랑으로 갈라 둘을 헷갈리지 않게 한다.
                dot.color = new Color(1f, 0.82f, 0.25f, 0.18f + (0.42f * chase));
                dot.transform.position = PlayerWorld + (direction * (0.85f + (i * 0.5f)));
                dot.transform.localScale = Vector3.one * Art.FitWidth(dot.sprite, 0.34f + (0.16f * chase));
                dot.enabled = true;
            }
        }

        /// <summary>아직 못 찾은 시민 중 가장 가까운 한 명. 없으면 null.</summary>
        private Civilian NearestUnspotted()
        {
            Civilian best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < _runner.Civilians.Count; i++)
            {
                Civilian civilian = _runner.Civilians[i];
                if (!civilian.Pending || civilian.Spotted) continue;

                float dx = civilian.X - _runner.Player.X;
                float dy = civilian.Y - _runner.Player.Y;
                float distance = (dx * dx) + (dy * dy);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = civilian;
            }

            return best;
        }

        private void RefreshExitGuide(bool carrying, float time)
        {
            if (_exitPulse == null)
            {
                _exitPulse = CreateRenderer("ExitPulse", Art.Get("Effects/glow"), OrderAim);
                for (int i = 0; i < ExitDotCount; i++)
                {
                    _exitDots[i] = CreateRenderer("ExitDot" + i, Art.Get("Effects/glow"), OrderPeople + 2);
                }
            }

            if (!carrying || _runner.Exits.Count == 0)
            {
                _exitPulse.enabled = false;
                for (int i = 0; i < ExitDotCount; i++) _exitDots[i].enabled = false;
                return;
            }

            GridPoint exit = NearestExit();
            Vector3 exitWorld = CellCenter(exit.X, exit.Y);

            float breath = Mathf.Abs(Mathf.Sin(time * 4f));
            _exitPulse.color = new Color(0.35f, 1f, 0.5f, 0.35f + (0.35f * breath));
            _exitPulse.transform.position = exitWorld;
            _exitPulse.transform.localScale = Vector3.one * Art.FitWidth(_exitPulse.sprite, 1.6f + (0.3f * breath));
            _exitPulse.enabled = true;

            // 출구는 대개 화면 밖이라 소방관 옆의 길잡이가 유일한 안내다.
            // 화살표 그림이 없어 점 세 개를 출구 쪽으로 늘어놓고 차례로 밝힌다.
            // 흐르는 방향 자체가 "이쪽"을 말해 주므로 그림 모양에 기대지 않아도 된다.
            Vector3 toExit = exitWorld - PlayerWorld;
            Vector3 direction = toExit.sqrMagnitude > 0.0001f ? toExit.normalized : Vector3.up;

            for (int i = 0; i < ExitDotCount; i++)
            {
                SpriteRenderer dot = _exitDots[i];
                float chase = Mathf.Abs(Mathf.Sin((time * 3.5f) - (i * 0.7f)));

                dot.color = new Color(0.3f, 1f, 0.45f, 0.3f + (0.6f * chase));
                dot.transform.position = PlayerWorld + (direction * (0.85f + (i * 0.5f)));
                dot.transform.localScale = Vector3.one * Art.FitWidth(dot.sprite, 0.45f + (0.2f * chase));
                dot.enabled = true;
            }
        }

        private GridPoint NearestExit()
        {
            GridPoint best = _runner.Exits[0];
            float bestDistance = float.MaxValue;

            for (int i = 0; i < _runner.Exits.Count; i++)
            {
                GridPoint exit = _runner.Exits[i];
                float dx = exit.X + 0.5f - _runner.Player.X;
                float dy = exit.Y + 0.5f - _runner.Player.Y;
                float distance = (dx * dx) + (dy * dy);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = exit;
            }

            return best;
        }

        /// <summary>
        /// 불이 이 칸 코앞까지 왔는지. 시민 머리 위 표식을 빨갛게 바꾸는 판정이다.
        ///
        /// 반경이 1칸이던 때는 사실상 켜지지 않았다. 바닥은 타지 않는 재질이고 시민은 늘
        /// 트인 바닥에 서 있어서, 바로 옆 칸이 타는 일이 없었기 때문이다. 불은 벽·기름·배전반을
        /// 타고 오므로 두 칸까지 봐야 "저 사람이 위험하다"가 실제로 켜진다.
        /// </summary>
        private bool FireNearby(int x, int y)
        {
            for (int dy = -FireWarnRadius; dy <= FireWarnRadius; dy++)
            {
                for (int dx = -FireWarnRadius; dx <= FireWarnRadius; dx++)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (!_grid.InBounds(nx, ny)) continue;
                    if (_grid[nx, ny].State == CellState.Burning) return true;
                }
            }

            return false;
        }

        /// <summary>코어가 낸 한 프레임짜리 구조 신호를 팝업으로 옮긴다.</summary>
        private void AnnounceRescues()
        {
            if (_runner.JustPickedUp != null) SpawnPopup("구조!", PlayerWorld, PopupGood);
            if (_runner.JustRescued != null) SpawnPopup("구조 완료! +$" + Economy.RescueBonus, PlayerWorld, PopupGood);
            if (_runner.JustLost != null) SpawnPopup("구조 실패…", ToWorld(_runner.JustLost.X, _runner.JustLost.Y), PopupBad);

            // 사건이 난 자리에 바로 띄운다. HUD 배너는 무슨 일인지, 팝업은 어디인지를 맡는다.
            GridPoint at = _runner.Events.JustHappenedAt;
            switch (_runner.Events.JustHappened)
            {
                case StageEventKind.SecondIgnition:
                    SpawnPopup("불이 옮겨 붙었다!", ToWorld(at.X + 0.5f, at.Y + 0.5f), PopupBad);
                    break;
                case StageEventKind.Collapse:
                    SpawnPopup("무너짐!", ToWorld(at.X + 0.5f, at.Y + 0.5f), PopupBad);
                    break;
            }
        }

        /// <summary>
        /// 지금 든 장비가 맞힐 칸을 바닥에 깔아 보여 준다.
        ///
        /// 타는 칸은 상성 판정 색으로 칠한다 — 버튼을 누르기 전에 "이게 듣나"를 알 수 있어야
        /// 장비를 바꿔 드는 판단이 생긴다. 안 타는 칸은 아주 옅게만 칠해 모양만 알린다.
        /// 역효과(물→기름·전기) 칸은 빨갛게 깜빡여 실수를 막는다.
        /// </summary>
        private void RefreshAimPreview(float time)
        {
            PlayerState player = _runner.Player;
            EquipmentDef def = _runner.SlotEquipment(player.ActiveSlot);

            if (def == null || _runner.IsOver || !player.IsAlive)
            {
                HideAimCells(0);
                return;
            }

            Aiming.Resolve(_grid, player.CellX, player.CellY, player.Aim, def.Pattern, def.Range, _hitBuffer, def.EndSpread);

            // 역효과 칸은 옅은 자홍과 진한 자홍을 오가며 깜빡인다.
            // 흰빛까지 갔다 오게 했더니 깜빡임의 절반이 흐릿한 흰 판이라, 맵 전체를 보는 줌에서는
            // 역효과 경고가 그냥 사라진 것처럼 보였다(캡처로 확인).
            float pulse = Mathf.Abs(Mathf.Sin(time * 8f));

            for (int i = 0; i < _hitBuffer.Count; i++)
            {
                GridPoint at = _hitBuffer[i];
                AgentVerdict verdict = AgentAdvice.ForCell(_grid, at.X, at.Y, def.Agent.Type);
                bool burning = _grid[at.X, at.Y].State == CellState.Burning;

                SpriteRenderer cell = EnsureAimCell(i);
                Color tint = FireLook.Verdict(verdict);
                if (verdict == AgentVerdict.Backfire && burning)
                {
                    tint = Color.Lerp(new Color(1f, 0.55f, 0.95f), tint, 0.35f + (0.65f * pulse));
                }

                // 불 위에 얹히므로 진하면 불꽃을 가린다. 타는 칸만 또렷하게 한다.
                float alpha = !burning ? 0.14f
                    : verdict == AgentVerdict.Backfire ? 0.62f + (0.25f * pulse)
                    : 0.42f;

                cell.color = new Color(tint.r, tint.g, tint.b, alpha);
                cell.transform.position = CellCenter(at.X, at.Y);
                cell.enabled = true;
            }

            HideAimCells(_hitBuffer.Count);
        }

        private SpriteRenderer EnsureAimCell(int index)
        {
            while (_aimCells.Count <= index)
            {
                SpriteRenderer created = CreateRenderer("AimCell", Art.White, OrderAim);
                created.transform.localScale = new Vector3(1f, Squash, 1f);
                _aimCells.Add(created);
            }

            return _aimCells[index];
        }

        private void HideAimCells(int from)
        {
            for (int i = from; i < _aimCells.Count; i++) _aimCells[i].enabled = false;
        }

        /// <summary>
        /// 쿨다운이 새로 걸린 슬롯이 있으면 방금 쏜 것이다. 코어는 연출을 모르므로
        /// 화면 쪽에서 상태 변화를 보고 물줄기를 만든다.
        /// </summary>
        private void DetectShots()
        {
            PlayerState player = _runner.Player;
            bool newShot = _runner.ShotsFired != _lastShotsFired;
            _lastShotsFired = _runner.ShotsFired;

            for (int slot = 0; slot < PlayerState.SlotCount; slot++)
            {
                float now = player.Cooldowns[slot];
                bool fired = now > _lastCooldowns[slot] + 0.001f;
                _lastCooldowns[slot] = now;
                if (!fired) continue;

                EquipmentDef def = _runner.SlotEquipment(slot);
                if (def == null) continue;

                Aiming.Resolve(_grid, player.CellX, player.CellY, player.Aim, def.Pattern, def.Range, _hitBuffer, def.EndSpread);
                SpawnShot(def.Agent.Type, PlayerWorld, _hitBuffer, Mathf.Min(LevelSteps(def), MaxSprayBoost));

                if (newShot && _hitBuffer.Count > 0)
                {
                    // 역효과를 먼저 알린다. 물을 기름에 뿌리면 전에는 조용히 번지기만 했다.
                    if (_runner.LastShotBackfired > 0)
                    {
                        SpawnPopup("역효과! 불이 번진다", Centroid(_hitBuffer), PopupBad);
                        newShot = false;
                    }
                    else if (_runner.LastShotExtinguished >= 2)
                    {
                        SpawnPopup(_runner.LastShotExtinguished + "칸 진압!", Centroid(_hitBuffer));
                        newShot = false;
                    }
                }
            }
        }

        /// <summary>
        /// 이번 프레임의 소리를 틀고 흔들림을 쌓는다. 불 소리는 소방관 주변 불의 크기를 따라가고,
        /// 판이 끝나면(결과 화면 뒤) 잦아든다.
        /// </summary>
        private void PlayFeel(float dt)
        {
            _feel.Update(_runner);
            foreach (Cue cue in _feel.Cues) GameAudio.Play(cue);

            _trauma = Mathf.Min(1f, FeelMath.DecayTrauma(_trauma, dt) + _feel.Shake);

            float level = _runner.IsOver ? 0f : FeelMath.FireLoudness(_grid, _runner.Player.X, _runner.Player.Y);
            GameAudio.SetFireLevel(level, dt);
        }

        /// <summary>
        /// 한 발의 연출: 칸마다 물줄기 선 + 흩뿌린 물방울 여러 개 + 도착한 칸의 물보라.
        /// CO2는 물방울 대신 흰 가스 덩어리를 뿜는다.
        /// </summary>
        private void SpawnShot(AgentType agent, Vector3 hand, List<GridPoint> hits, int boost)
        {
            Color color = SprayColor(agent);
            bool gas = agent == AgentType.CO2;

            // 레벨이 오를수록 물방울이 많아지고 물보라가 커진다. 같은 버튼인데 "세졌다"가 눈에 보이게.
            int drops = DropsPerCell + (boost / 2);
            float grow = 1f + (0.05f * boost);

            foreach (GridPoint hit in hits)
            {
                Vector3 target = CellCenter(hit.X, hit.Y);

                if (!gas) Spawn(ParticleKind.Streak, _streakSprite, color, hand, target, 0f, StreakLifetime, 1f, 1f, 0.8f);

                for (int i = 0; i < drops; i++)
                {
                    Vector3 scatter = new Vector3(Jitter(0.35f), Jitter(0.35f), 0f);
                    float delay = i * 0.03f;
                    if (gas)
                    {
                        Spawn(ParticleKind.Drop, _cloudSprite, color, hand, target + scatter, delay, DropLifetime, 0.9f, 2.2f * grow, 1f);
                    }
                    else
                    {
                        Spawn(ParticleKind.Drop, _dropSprite, color, hand, target + scatter, delay, DropLifetime, 1.0f, 1.7f * grow, 0.95f);
                    }
                }

                Spawn(ParticleKind.Splash, _splashSprite, color, target, target, 0.08f, SplashLifetime, 0.6f, 1.6f * grow, 0.85f);
            }
        }

        /// <summary>불이 막 꺼진 칸에서 흰 김이 피어오른다. 진압했다는 손맛을 준다.</summary>
        private void DetectExtinguished()
        {
            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    int i = _grid.Index(x, y);
                    bool burning = _grid.Cells[i].State == CellState.Burning;
                    bool putOut = _wasBurning[i] && _grid.Cells[i].State == CellState.Intact;
                    _wasBurning[i] = burning;
                    if (!putOut) continue;

                    Vector3 at = CellCenter(x, y);
                    for (int puff = 0; puff < 2; puff++)
                    {
                        Spawn(ParticleKind.Steam, _steamSprite, Color.white, at, at + new Vector3(Jitter(0.3f), 0.9f, 0f), puff * 0.12f, SteamLifetime, 1.0f, 2.2f, 0.9f);
                    }
                }
            }
        }

        /// <summary>레벨 − 1. 장비 정의에는 레벨이 없어서 기본 위력 대비 배율로 되짚는다.</summary>
        private static int LevelSteps(EquipmentDef def)
        {
            EquipmentDef base_ = EquipmentCatalog.ById(def.Id);
            if (base_ == null || base_.Agent.Power <= 0f) return 0;
            return Mathf.Max(0, Mathf.RoundToInt(((def.Agent.Power / base_.Agent.Power) - 1f) / EquipmentDef.PowerPerLevel));
        }

        private Vector3 Centroid(List<GridPoint> cells)
        {
            Vector3 sum = Vector3.zero;
            foreach (GridPoint cell in cells) sum += CellCenter(cell.X, cell.Y);
            return sum / cells.Count;
        }

        /// <summary>좋은 소식은 노랑, 나쁜 소식은 빨강, 구조는 초록.</summary>
        private static readonly Color PopupDefault = new Color(1f, 0.92f, 0.3f);
        private static readonly Color PopupBad = new Color(1f, 0.35f, 0.3f);
        private static readonly Color PopupGood = new Color(0.45f, 1f, 0.55f);

        private void SpawnPopup(string text, Vector3 at)
        {
            SpawnPopup(text, at, PopupDefault);
        }

        private void SpawnPopup(string text, Vector3 at, Color color)
        {
            var popup = new Popup
            {
                Shadow = CreateText("PopupShadow", text, new Color(0f, 0f, 0f, 0.8f), OrderPopup),
                Text = CreateText("Popup", text, color, OrderPopup + 1),
                From = at + new Vector3(0f, 0.6f, 0f),
            };
            _popups.Add(popup);
            PlacePopup(popup);
        }

        private TextMesh CreateText(string name, string text, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = Art.Font;
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = 0.1f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Art.Font.material;
            renderer.sortingOrder = order;
            return mesh;
        }

        private void AdvancePopups(float dt)
        {
            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                Popup popup = _popups[i];
                popup.Age += dt;
                if (popup.Age >= PopupLifetime)
                {
                    UiKit.Discard(popup.Text.gameObject);
                    UiKit.Discard(popup.Shadow.gameObject);
                    _popups.RemoveAt(i);
                    continue;
                }

                _popups[i] = popup;
                PlacePopup(popup);
            }
        }

        private static void PlacePopup(Popup popup)
        {
            float t = popup.Age / PopupLifetime;
            Vector3 at = popup.From + new Vector3(0f, 0.8f * t, 0f);
            float alpha = t < 0.6f ? 1f : 1f - ((t - 0.6f) / 0.4f);

            popup.Text.transform.position = at;
            popup.Shadow.transform.position = at + new Vector3(0.05f, -0.05f, 0f);

            Color text = popup.Text.color;
            text.a = alpha;
            popup.Text.color = text;
            Color shadow = popup.Shadow.color;
            shadow.a = 0.8f * alpha;
            popup.Shadow.color = shadow;
        }

        private float Jitter(float amount)
        {
            return ((float)_jitter.NextDouble() * 2f - 1f) * amount;
        }

        private static Color SprayColor(AgentType agent)
        {
            switch (agent)
            {
                // 순백은 밝은 타일 바닥에서 안 보여 살짝 푸른 흰색으로 쓴다.
                case AgentType.CO2: return new Color(0.8f, 0.9f, 1f);
                case AgentType.Foam: return new Color(1f, 0.98f, 0.85f);
                default: return new Color(0.35f, 0.7f, 1f);
            }
        }

        private void Spawn(ParticleKind kind, Sprite sprite, Color color, Vector3 from, Vector3 to, float delay, float lifetime, float startSize, float endSize, float alpha)
        {
            SpriteRenderer renderer = _particlePool.Count > 0
                ? _particlePool.Pop()
                : CreateRenderer("Particle", sprite, OrderSpray);

            renderer.sprite = sprite;
            renderer.color = color;
            // 김은 불꽃 위로, 물은 사람 위·불꽃 아래로 그린다.
            renderer.sortingOrder = kind == ParticleKind.Steam ? OrderSmoke + 1 : OrderSpray;
            renderer.enabled = false;

            var particle = new Particle
            {
                Renderer = renderer,
                Kind = kind,
                Delay = delay,
                Lifetime = lifetime,
                From = from,
                To = to,
                StartSize = startSize,
                EndSize = endSize,
                Alpha = alpha,
            };
            _particles.Add(particle);
            Place(particle);
        }

        private void AdvanceParticles(float dt)
        {
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                Particle particle = _particles[i];
                particle.Age += dt;

                if (particle.Age >= particle.Delay + particle.Lifetime)
                {
                    particle.Renderer.enabled = false;
                    _particlePool.Push(particle.Renderer);
                    _particles.RemoveAt(i);
                    continue;
                }

                _particles[i] = particle;
                Place(particle);
            }
        }

        private static void Place(Particle particle)
        {
            float local = particle.Age - particle.Delay;
            SpriteRenderer renderer = particle.Renderer;
            renderer.enabled = local >= 0f;
            if (local < 0f) return;

            float t = Mathf.Clamp01(local / particle.Lifetime);
            Transform transform = renderer.transform;
            float size = Mathf.Lerp(particle.StartSize, particle.EndSize, t);
            float fade;

            switch (particle.Kind)
            {
                case ParticleKind.Streak:
                {
                    // 가는 세로선 그림을 손→목표 방향으로 눕혀 길이만큼 늘인다.
                    Vector3 delta = particle.To - particle.From;
                    float unit = Art.FitWidth(renderer.sprite, 1f);
                    transform.position = particle.From + (delta * 0.5f);
                    transform.rotation = Quaternion.Euler(0f, 0f, (Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg) - 90f);
                    transform.localScale = new Vector3(unit * 6f, unit * (delta.magnitude / 0.8f), 1f);
                    fade = 1f - t;
                    break;
                }

                case ParticleKind.Drop:
                {
                    // 빨리 날아가 목표에 닿은 뒤 그 자리에서 퍼지며 사라진다.
                    float travel = Mathf.Clamp01(t / 0.45f);
                    transform.position = Vector3.Lerp(particle.From, particle.To, 1f - ((1f - travel) * (1f - travel)));
                    transform.rotation = Quaternion.identity;
                    transform.localScale = Vector3.one * Art.FitWidth(renderer.sprite, size);
                    fade = t < 0.45f ? 1f : 1f - ((t - 0.45f) / 0.55f);
                    break;
                }

                case ParticleKind.Steam:
                    transform.position = Vector3.Lerp(particle.From, particle.To, t);
                    transform.rotation = Quaternion.Euler(0f, 0f, t * 40f);
                    transform.localScale = Vector3.one * Art.FitWidth(renderer.sprite, size);
                    fade = Mathf.Sin(t * Mathf.PI);
                    break;

                default: // Splash
                    transform.position = particle.To;
                    transform.rotation = Quaternion.identity;
                    transform.localScale = Vector3.one * Art.FitWidth(renderer.sprite, size);
                    fade = 1f - t;
                    break;
            }

            Color color = renderer.color;
            color.a = particle.Alpha * fade;
            renderer.color = color;
        }

        // ------------------------------------------------------------------
        // 카메라
        // ------------------------------------------------------------------

        /// <summary>건물 안에서 벽 바깥으로 남겨 두는 여백(칸). 벽에 코를 박은 것처럼 보이지 않게.</summary>
        private const float IndoorMargin = 1.5f;

        /// <summary>아무리 좁은 건물이라도 이만큼은 보여 준다. 한 칸이 화면을 덮으면 어디가 어딘지 모른다.</summary>
        private const float MinCameraSize = 5f;

        /// <summary>
        /// 아무리 넓어도 이 이상 물러서지 않는다.
        /// 세로로 긴 화면에서는 맵 전체를 담으려면 한없이 멀어지는데, 그러면 소방관이 점이 된다.
        /// 못 담은 축은 아래 클램프가 소방관을 따라가며 채운다.
        /// </summary>
        private const float MaxCameraSize = 12f;

        /// <summary>시점이 바뀔 때 옮겨 가는 데 걸리는 시간(초). 딱 끊기면 멀미가 난다.</summary>
        private const float CameraEase = 0.35f;

        private float _cameraSize;
        private bool _cameraReady;

        /// <summary>
        /// 밖에 있으면 맵 전체를, 건물 안에 있으면 그 건물만 담는다.
        ///
        /// 밖에서는 어느 건물이 타고 어디에 사람이 남았는지 한눈에 골라야 하고,
        /// 안에서는 그 건물에만 집중해야 한다. 담을 사각형만 바꾸고 나머지 계산은 같다.
        /// 사각형이 화면보다 작은 축은 가운데 정렬하고, 큰 축만 소방관을 따라간다.
        /// </summary>
        public void FrameCamera(Camera camera, Vector3 focus)
        {
            Building inside = _buildings.Of((int)_runner.Player.X, (int)_runner.Player.Y);

            float minX;
            float minY;
            float maxX;
            float maxY;

            if (inside == null)
            {
                minX = 0f;
                minY = 0f;
                maxX = _grid.Width;

                // 땅이 눌린 만큼 담을 사각형도 눌린다. 안 고치면 위아래로 빈 띠가 생긴다.
                // 건물 지붕이 벽 높이만큼 위로 솟으므로 그만큼 여유를 더 준다.
                maxY = (_grid.Height * Squash) + BuildingOverlay.WallRise;
            }
            else
            {
                // 격자는 위가 0행이고 월드는 위가 +y라 세로를 뒤집는다.
                minX = inside.MinX - IndoorMargin;
                maxX = inside.MaxX + 1f + IndoorMargin;
                minY = ((_grid.Height - (inside.MaxY + 1)) * Squash) - IndoorMargin;
                maxY = ((_grid.Height - inside.MinY) * Squash) + IndoorMargin;
            }

            float target = Mathf.Clamp(
                Mathf.Max((maxY - minY) * 0.5f, (maxX - minX) * 0.5f / camera.aspect),
                MinCameraSize,
                MaxCameraSize);

            // 장면을 새로 만든 첫 프레임은 곧바로 맞춘다.
            // 보간부터 시작하면 캡처가 줌 도중 상태로 찍힌다.
            _cameraSize = _cameraReady
                ? Mathf.Lerp(_cameraSize, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime / CameraEase))
                : target;
            _cameraReady = true;
            camera.orthographicSize = _cameraSize;

            float halfHeight = _cameraSize;
            float halfWidth = halfHeight * camera.aspect;

            float x = (maxX - minX) <= halfWidth * 2f
                ? (minX + maxX) * 0.5f
                : Mathf.Clamp(focus.x, minX + halfWidth, maxX - halfWidth);
            float y = (maxY - minY) <= halfHeight * 2f
                ? (minY + maxY) * 0.5f
                : Mathf.Clamp(focus.y, minY + halfHeight, maxY - halfHeight);

            // 좁은 건물은 화면보다 작아서 가운데 정렬하면 격자 밖이 딸려 들어온다.
            // 건물이 조금 치우치더라도 검은 여백을 보이지 않는 쪽이 낫다.
            if (_grid.Width >= halfWidth * 2f) x = Mathf.Clamp(x, halfWidth, _grid.Width - halfWidth);
            if (_grid.Height >= halfHeight * 2f) y = Mathf.Clamp(y, halfHeight, _grid.Height - halfHeight);

            // 흔들림. 제곱이라 작은 충격은 거의 안 흔들리고 큰 충격만 확 흔들린다.
            // 펄린 노이즈라 떨림이 매끄럽고, trauma가 0이면 오프셋도 0이라 캡처 구도가 그대로다.
            float amp = _trauma * _trauma * FeelMath.MaxShakeOffset;
            float t = Time.unscaledTime * 25f;
            x += amp * ((Mathf.PerlinNoise(t, 0f) * 2f) - 1f);
            y += amp * ((Mathf.PerlinNoise(0f, t) * 2f) - 1f);

            camera.transform.position = new Vector3(x, y, -10f);
        }

        // ------------------------------------------------------------------

        private SpriteRenderer CreateRenderer(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Sprite[] LoadSeries(string prefix, int first, int last)
        {
            var sprites = new Sprite[last - first + 1];
            for (int i = first; i <= last; i++) sprites[i - first] = Art.Get(prefix + i);
            return sprites;
        }
    }
}
