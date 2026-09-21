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
        private const int OrderOverlay = 3;
        private const int OrderGlow = 4;
        private const int OrderPeople = 10;
        private const int OrderSpray = 12;
        private const int OrderFire = 20;
        private const int OrderSmoke = 30;

        private const float SprayLifetime = 0.35f;

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

        private readonly Sprite[] _flameSprites;
        private readonly Sprite _tongueSprite;
        private readonly Sprite[] _smokeSprites;
        private readonly Sprite[] _scorchSprites;

        private readonly List<Spray> _sprays = new List<Spray>();
        private readonly Stack<SpriteRenderer> _sprayPool = new Stack<SpriteRenderer>();
        private readonly List<GridPoint> _hitBuffer = new List<GridPoint>();
        private readonly float[] _lastCooldowns = new float[PlayerState.SlotCount];

        private struct Spray
        {
            public SpriteRenderer Renderer;
            public float Age;
            public Vector3 From;
            public Vector3 To;
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
            _tongueSprite = Art.Get("Effects/flame_05");
            _smokeSprites = LoadSeries("Effects/smoke_0", 1, 5);
            _scorchSprites = LoadSeries("Effects/scorch_0", 1, 3);

            BuildTiles();

            _player = CreateRenderer("Player", Art.Get("TopDown/player_hold"), OrderPeople + 1);
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
            Object.Destroy(_root.gameObject);
        }

        // ------------------------------------------------------------------
        // 바닥·벽 구성
        // ------------------------------------------------------------------

        private void BuildTiles()
        {
            bool[] outdoor = FindOutdoor();
            int stage = _runner.Def.Id;

            // 현장마다 바닥 분위기를 다르게 한다: 주택=나무 마루, 상가=타일, 주유소=흙 마당
            string indoorA = stage == 1 ? "TopDown/floor_tile_a" : "TopDown/floor_wood_a";
            string indoorB = stage == 1 ? "TopDown/floor_tile_b" : "TopDown/floor_wood_b";
            string outdoorA = stage == 2 ? "TopDown/dirt" : "TopDown/grass_a";
            string outdoorB = stage == 2 ? "TopDown/dirt" : "TopDown/grass_b";

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

            RefreshPeople();
            DetectShots();
            AdvanceSprays(dt);
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

                SpriteRenderer glow = Ensure(_glow, i, x, y, OrderGlow);
                glow.sprite = Art.Get("Effects/glow");
                glow.color = new Color(1f, 0.45f, 0.1f, 0.3f + (0.25f * intensity));
                glow.transform.localScale = Vector3.one * Art.FitWidth(glow.sprite, 1.8f + flicker);
                glow.enabled = true;

                // 몸통: 진한 주황 원을 칸보다 살짝 크게 깔아 이웃 불과 이어지게 한다.
                // 팩의 불덩어리 그림(fire_01/02)은 평균 불투명도가 47/255로 너무 옅어 몸통으로는 안 보인다.
                SpriteRenderer body = Ensure(_body, i, x, y, OrderFire);
                body.sprite = Art.Get("Effects/water_drop");
                body.color = Color.Lerp(new Color(0.95f, 0.28f, 0.05f, 0.75f), new Color(1f, 0.5f, 0.08f, 0.85f), intensity);
                body.transform.localScale = Vector3.one * Art.FitWidth(body.sprite, 1.7f + (flicker * 0.4f));
                body.enabled = true;

                // 질감: 옅은 불덩어리를 노랗게 얹고 천천히 돌린다.
                SpriteRenderer flame = Ensure(_flame, i, x, y, OrderFire + 1);
                flame.sprite = _flameSprites[Mathf.Abs(seed) % _flameSprites.Length];
                flame.color = new Color(1f, 0.85f, 0.3f, 1f);
                flame.transform.localScale = Vector3.one * Art.FitWidth(flame.sprite, 1.3f);
                flame.transform.rotation = Quaternion.Euler(0f, 0f, (time * 40f) + (seed % 360));
                flame.enabled = true;

                // 불길: 위로 솟아 흔들리는 혀. 칸마다 위치·키·흔들림 위상을 달리해
                // 줄 맞춘 촛불처럼 보이지 않게 한다. 그림 여백이 커서 높이 기준으로 맞춘다.
                float jitterX = (((seed >> 3) & 15) / 15f - 0.5f) * 0.45f;
                float jitterY = (((seed >> 7) & 15) / 15f - 0.5f) * 0.3f;
                float variety = 0.75f + ((((seed >> 11) & 15) / 15f) * 0.5f);

                SpriteRenderer tongue = Ensure(_tongue, i, x, y, OrderFire + 2);
                tongue.sprite = _tongueSprite;
                tongue.color = Color.Lerp(new Color(1f, 0.45f, 0.08f), new Color(1f, 0.8f, 0.3f), intensity * 0.7f);
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
                smoke.color = new Color(0.25f, 0.25f, 0.27f, 0.4f * (1f - drift));
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
        /// 쿨다운이 새로 걸린 슬롯이 있으면 방금 쏜 것이다. 코어는 연출을 모르므로
        /// 화면 쪽에서 상태 변화를 보고 물줄기를 만든다.
        /// </summary>
        private void DetectShots()
        {
            PlayerState player = _runner.Player;

            for (int slot = 0; slot < PlayerState.SlotCount; slot++)
            {
                float now = player.Cooldowns[slot];
                bool fired = now > _lastCooldowns[slot] + 0.001f;
                _lastCooldowns[slot] = now;
                if (!fired) continue;

                EquipmentDef def = EquipmentCatalog.ById(player.Slots[slot]);
                if (def == null) continue;

                Aiming.Resolve(_grid, player.CellX, player.CellY, player.Aim, def.Pattern, def.Range, _hitBuffer);
                Color color = SprayColor(def.Agent.Type);

                foreach (GridPoint hit in _hitBuffer)
                {
                    SpawnSpray(PlayerWorld, CellCenter(hit.X, hit.Y), color);
                }
            }
        }

        private static Color SprayColor(AgentType agent)
        {
            switch (agent)
            {
                case AgentType.CO2: return new Color(0.95f, 0.97f, 1f, 0.9f);
                case AgentType.Foam: return new Color(1f, 0.98f, 0.85f, 0.95f);
                default: return new Color(0.35f, 0.7f, 1f, 0.9f);
            }
        }

        private void SpawnSpray(Vector3 from, Vector3 to, Color color)
        {
            SpriteRenderer renderer = _sprayPool.Count > 0
                ? _sprayPool.Pop()
                : CreateRenderer("Spray", Art.Get("Effects/water_drop"), OrderSpray);

            renderer.color = color;
            renderer.enabled = true;
            _sprays.Add(new Spray { Renderer = renderer, Age = 0f, From = from, To = to });
            PlaceSpray(_sprays[_sprays.Count - 1]);
        }

        private void AdvanceSprays(float dt)
        {
            for (int i = _sprays.Count - 1; i >= 0; i--)
            {
                Spray spray = _sprays[i];
                spray.Age += dt;

                if (spray.Age >= SprayLifetime)
                {
                    spray.Renderer.enabled = false;
                    _sprayPool.Push(spray.Renderer);
                    _sprays.RemoveAt(i);
                    continue;
                }

                _sprays[i] = spray;
                PlaceSpray(spray);
            }
        }

        private static void PlaceSpray(Spray spray)
        {
            // 물방울이 소방관 손에서 목표 칸으로 날아가며 퍼진다.
            float t = Mathf.Clamp01((spray.Age / SprayLifetime) + 0.35f);
            spray.Renderer.transform.position = Vector3.Lerp(spray.From, spray.To, t);
            spray.Renderer.transform.localScale = Vector3.one * Art.FitWidth(spray.Renderer.sprite, 0.3f + (0.5f * t));
            Color color = spray.Renderer.color;
            color.a = 0.9f * (1f - (spray.Age / SprayLifetime) * 0.6f);
            spray.Renderer.color = color;
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
