using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using FireGame.Core.Sim;
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

        private readonly SpriteRenderer[] _floor;
        private readonly SpriteRenderer[] _top;
        private readonly SpriteRenderer[] _overlay;
        private readonly SpriteRenderer[] _glow;
        private readonly SpriteRenderer[] _body;
        private readonly SpriteRenderer[] _flame;
        private readonly SpriteRenderer[] _tongue;
        private readonly SpriteRenderer[] _smoke;
        private readonly Color[] _topBaseColor;
        private readonly bool[] _isWall;

        private readonly SpriteRenderer _player;
        private readonly List<SpriteRenderer> _civilians = new List<SpriteRenderer>();

        /// <summary>지금 든 장비가 맞힐 칸을 바닥에 깔아 보여 주는 판. 쓰는 만큼만 늘린다.</summary>
        private readonly List<SpriteRenderer> _aimCells = new List<SpriteRenderer>();

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

            // 방화복 레벨마다 옷 색·헬멧이 다른 그림(tools/import-art.py가 만든다)
            _player = CreateRenderer("Player", Art.Get(SuitSprite(runner.Player.SuitLevel)), OrderPeople + 1);
            string[] civilianSprites = { "TopDown/civilian_woman", "TopDown/civilian_old", "TopDown/civilian_man" };
            for (int i = 0; i < runner.Civilians.Count; i++)
            {
                _civilians.Add(CreateRenderer("Civilian" + i, Art.Get(civilianSprites[i % civilianSprites.Length]), OrderPeople));
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

        /// <summary>격자 칸의 중심 월드 좌표.</summary>
        public Vector3 CellCenter(int x, int y)
        {
            return new Vector3(x + 0.5f, _grid.Height - y - 0.5f, 0f);
        }

        /// <summary>셀 단위 실수 좌표(StageRunner 좌표)를 월드 좌표로.</summary>
        public Vector3 ToWorld(float x, float y)
        {
            return new Vector3(x, _grid.Height - y, 0f);
        }

        public Vector3 PlayerWorld
        {
            get { return ToWorld(_runner.Player.X, _runner.Player.Y); }
        }

        public void Destroy()
        {
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
            bool[] outdoor = FindOutdoor();
            FloorTheme theme;
            if (!FloorThemes.TryGetValue(_runner.Def.Id, out theme)) theme = FloorThemes[0];

            string indoorA = "TopDown/" + theme.IndoorA;
            string indoorB = "TopDown/" + theme.IndoorB;
            string outdoorA = "TopDown/" + theme.OutdoorA;
            string outdoorB = "TopDown/" + theme.OutdoorB;

            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    int i = _grid.Index(x, y);
                    var material = (MaterialId)_grid[x, y].Material;
                    bool alternate = ((x * 7) + (y * 13)) % 5 == 0;

                    string floorSprite = outdoor[i]
                        ? (alternate ? outdoorB : outdoorA)
                        : (alternate ? indoorB : indoorA);

                    _floor[i] = CreateRenderer("Floor", Art.Get(floorSprite), OrderFloor);
                    _floor[i].transform.position = CellCenter(x, y);

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
                            top = Art.Wall(WallStyle.Concrete, WallMask(x, y));
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
                    }

                    if (top != null)
                    {
                        _top[i] = CreateRenderer("Top", top, order);
                        _top[i].transform.position = CellCenter(x, y);
                        _top[i].transform.rotation = Quaternion.Euler(0f, 0f, rotation);
                        _top[i].color = tint;
                        _topBaseColor[i] = tint;
                    }
                }
            }
        }

        /// <summary>
        /// 소방관 시작 위치에서 문을 지나지 않고 갈 수 있는 바닥 = 바깥.
        /// 바깥은 잔디·흙, 안쪽은 마루·타일로 깔아 건물 윤곽이 보이게 한다.
        /// </summary>
        private bool[] FindOutdoor()
        {
            var outdoor = new bool[_grid.Count];
            var queue = new Queue<GridPoint>();

            // 지금 소방관 위치가 아니라 맵에 정해진 시작 지점을 기준으로 한다.
            // 현장 도중에 화면을 다시 만들면 소방관이 실내에 있을 수 있기 때문이다.
            GridPoint start = MapLoader.Parse(_runner.Def.Map).PlayerSpawn;

            outdoor[_grid.Index(start.X, start.Y)] = true;
            queue.Enqueue(start);

            int[] dx = { 0, 0, -1, 1 };
            int[] dy = { -1, 1, 0, 0 };

            while (queue.Count > 0)
            {
                GridPoint p = queue.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    int nx = p.X + dx[k];
                    int ny = p.Y + dy[k];
                    if (!_grid.InBounds(nx, ny)) continue;

                    int n = _grid.Index(nx, ny);
                    if (outdoor[n]) continue;

                    var material = (MaterialId)_grid.Cells[n].Material;
                    if (material == MaterialId.Door) continue;
                    if (!Materials.Of(material).Walkable) continue;

                    outdoor[n] = true;
                    queue.Enqueue(new GridPoint(nx, ny));
                }
            }

            return outdoor;
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
            DetectShots();
            DetectExtinguished();
            AdvanceParticles(dt);
            AdvancePopups(dt);
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
                tongue.transform.position = CellCenter(x, y) + new Vector3(jitterX + (Mathf.Sin((time * 5f) + seed) * 0.06f), 0.2f + jitterY, 0f);
                tongue.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin((time * 6f) + seed) * 10f);
                tongue.enabled = true;

                SpriteRenderer smoke = Ensure(_smoke, i, x, y, OrderSmoke);
                smoke.sprite = _smokeSprites[Mathf.Abs(seed) % _smokeSprites.Length];
                float drift = Mathf.Repeat((time * 0.6f) + ((seed & 255) / 255f), 1f);
                smoke.transform.position = CellCenter(x, y) + new Vector3(0.2f * Mathf.Sin(time + seed), 0.5f + drift, 0f);
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
        }

        private SpriteRenderer Ensure(SpriteRenderer[] layer, int i, int x, int y, int order)
        {
            if (layer[i] == null)
            {
                layer[i] = CreateRenderer("Fx", Art.White, order);
                layer[i].transform.position = CellCenter(x, y);
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
            _player.transform.position = PlayerWorld;

            // 캐릭터 그림은 오른쪽을 본다. 조준 방향으로 돌린다.
            float ox = Aiming.OffsetX(player.Aim);
            float oy = -Aiming.OffsetY(player.Aim);
            _player.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(oy, ox) * Mathf.Rad2Deg);
            _player.transform.localScale = Vector3.one * 1.1f;
            _player.color = player.IsAlive ? Color.white : new Color(0.5f, 0.5f, 0.5f);

            for (int i = 0; i < _civilians.Count; i++)
            {
                Civilian civilian = _runner.Civilians[i];
                SpriteRenderer renderer = _civilians[i];
                renderer.enabled = civilian.Pending;
                if (!civilian.Pending) continue;

                if (civilian.Carried)
                {
                    // 업은 사람은 소방관 등 뒤에 붙는다.
                    renderer.transform.position = PlayerWorld - new Vector3(ox, oy, 0f).normalized * 0.35f;
                    renderer.transform.rotation = _player.transform.rotation;
                    renderer.transform.localScale = Vector3.one * 0.9f;
                    renderer.sortingOrder = OrderPeople;
                }
                else
                {
                    renderer.transform.position = ToWorld(civilian.X, civilian.Y);
                    renderer.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
                    renderer.transform.localScale = Vector3.one * 1.05f;
                }
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

            // 역효과 칸은 흰빛과 자홍을 오가며 깜빡인다. 불꽃 위에 얹혀도 확실히 눈에 띈다.
            float pulse = Mathf.Abs(Mathf.Sin(time * 8f));

            for (int i = 0; i < _hitBuffer.Count; i++)
            {
                GridPoint at = _hitBuffer[i];
                AgentVerdict verdict = AgentAdvice.ForCell(_grid, at.X, at.Y, def.Agent.Type);
                bool burning = _grid[at.X, at.Y].State == CellState.Burning;

                SpriteRenderer cell = EnsureAimCell(i);
                Color tint = FireLook.Verdict(verdict);
                if (verdict == AgentVerdict.Backfire && burning) tint = Color.Lerp(Color.white, tint, pulse);

                // 불 위에 얹히므로 진하면 불꽃을 가린다. 타는 칸만 또렷하게 한다.
                float alpha = !burning ? 0.14f
                    : verdict == AgentVerdict.Backfire ? 0.5f + (0.3f * pulse)
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
                created.transform.localScale = Vector3.one;
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
                Shadow = CreateText("PopupShadow", text, new Color(0f, 0f, 0f, 0.8f), OrderSmoke + 4),
                Text = CreateText("Popup", text, color, OrderSmoke + 5),
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

        /// <summary>
        /// 초점을 화면 중앙에 두되 격자 밖이 보이지 않게 멈춘다.
        /// 격자가 화면보다 작은 축은 가운데 정렬한다.
        /// </summary>
        public void FrameCamera(Camera camera, Vector3 focus)
        {
            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;

            float x = _grid.Width <= halfWidth * 2f
                ? _grid.Width / 2f
                : Mathf.Clamp(focus.x, halfWidth, _grid.Width - halfWidth);
            float y = _grid.Height <= halfHeight * 2f
                ? _grid.Height / 2f
                : Mathf.Clamp(focus.y, halfHeight, _grid.Height - halfHeight);

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
