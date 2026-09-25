using System.Collections.Generic;
using FireGame.Core.Grid;
using FireGame.Prototypes.Logic;
using FireGame.UnityLayer;
using FireGame.UnityLayer.Feel;
using UnityEngine;
using UnityEngine.UI;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 시험판 A 화면. 규칙은 <see cref="ActionSim"/>에 있고, 여기는 60Hz로 돌리며 보여 주고 소리를 낸다.
    ///
    /// 손맛을 위해: 물방울이 조준 방향으로 쏟아지고, 달아오른 칸은 불이 붙기 전부터 붉게 달아오르며,
    /// 솟음·플래시오버는 예고(붉은 맥동)와 함께 오고 터질 때 화면이 흔들린다.
    /// </summary>
    public sealed class ActionView : IPrototype
    {
        private readonly Transform _root;
        private readonly Camera _camera;
        private readonly RectTransform _hud;

        private ActionSim _sim;
        private float _accumulator;
        private float _time;
        private float _trauma;
        private float _sprayClock;
        private float _putOutClock;
        private int _lastRescued;
        private int _lastLost;
        private AOutcome _lastOutcome;
        private int _seed = 1;

        private SpriteRenderer[] _tint;
        private SpriteRenderer[] _fire;
        private SpriteRenderer[] _tongue;
        private SpriteRenderer[] _warn;
        private SpriteRenderer _player;
        private readonly List<SpriteRenderer> _people = new List<SpriteRenderer>();

        private readonly List<Drop> _drops = new List<Drop>();
        private readonly Stack<SpriteRenderer> _dropPool = new Stack<SpriteRenderer>();

        private Text _title;
        private Text _status;
        private Text _help;
        private Text _alert;
        private Image _hpFill;
        private Image _tankFill;
        private Image _vignette;
        private Image _bannerBack;
        private Text _banner;

        private struct Drop
        {
            public SpriteRenderer Sprite;
            public Vector3 From;
            public Vector3 To;
            public float Age;
            public float Life;
        }

        public ActionView(Transform parent, Camera camera, Canvas canvas)
        {
            _camera = camera;
            _root = new GameObject("ActionWorld").transform;
            _root.SetParent(parent, false);
            _hud = UiKit.Node(canvas.transform, "ActionHud");
            UiKit.Stretch(_hud);
            BuildHud();
            Restart(_seed);
        }

        public ActionSim Sim
        {
            get { return _sim; }
        }

        public void Restart(int seed)
        {
            _seed = seed;
            _sim = new ActionSim(ActionLevel.Building, seed);
            _lastRescued = 0;
            _lastLost = 0;
            _lastOutcome = AOutcome.Playing;
            _trauma = 0f;
            BuildWorld();
        }

        public void Destroy()
        {
            GameAudio.SetFireLevel(0f, 1f);
            UiKit.Discard(_root.gameObject);
            UiKit.Discard(_hud.gameObject);
        }

        // ------------------------------------------------------------------
        // 매 프레임
        // ------------------------------------------------------------------

        public void Tick(float dt, in ProtoInput input)
        {
            if (_sim.Outcome != AOutcome.Playing && input.MouseClicked)
            {
                Restart(_seed + 1);
                return;
            }

            AInput sim = ToSim(input);
            _accumulator += dt;
            while (_accumulator >= ActionSim.Dt)
            {
                _accumulator -= ActionSim.Dt;
                Step(sim);
            }
            Refresh(dt);
        }

        /// <summary>한 틱 진행하고 그 틱의 소리·흔들림·물방울을 만든다. 하니스도 이걸로 판을 굴린다.</summary>
        public void Step(in AInput input)
        {
            _sim.Step(input);
            React();
        }

        private AInput ToSim(in ProtoInput input)
        {
            var result = new AInput { MoveX = input.Move.x, MoveY = -input.Move.y, Spraying = input.MouseHeld };
            if (_camera != null)
            {
                Vector3 world = _camera.ScreenToWorldPoint(new Vector3(input.Mouse.x, input.Mouse.y, 10f));
                // 월드는 위가 +y, 격자는 아래가 +y.
                float gx = world.x - _sim.Player.X;
                float gy = -world.y - _sim.Player.Y;
                result.AimRadians = Mathf.Atan2(gy, gx);
            }
            return result;
        }

        private void React()
        {
            _sprayClock -= ActionSim.Dt;
            _putOutClock -= ActionSim.Dt;

            if (_sim.SprayingNow)
            {
                foreach (Vec2 hit in _sim.SprayHits) SpawnDrop(ToWorld(_sim.Player), ToWorld(hit));
                if (_sprayClock <= 0f)
                {
                    GameAudio.Play(Cue.SprayWater);
                    _sprayClock = 0.12f;
                }
            }

            if (_sim.JustPutOut > 0 && _putOutClock <= 0f)
            {
                GameAudio.Play(Cue.PutOut);
                _putOutClock = 0.1f;
                _trauma = Mathf.Min(1f, _trauma + 0.12f);
            }
            if (_sim.JustFlaredUp)
            {
                GameAudio.Play(Cue.SecondIgnition);
                _trauma = Mathf.Min(1f, _trauma + 0.45f);
            }
            if (_sim.JustFlashover)
            {
                GameAudio.Play(Cue.Backfire);
                _trauma = 1f;
            }
            if (_sim.Rescued > _lastRescued) GameAudio.Play(Cue.Rescued);
            if (_sim.LostCount > _lastLost) GameAudio.Play(Cue.CivilianLost);
            _lastRescued = _sim.Rescued;
            _lastLost = _sim.LostCount;

            if (_sim.Outcome != _lastOutcome && _lastOutcome == AOutcome.Playing)
            {
                GameAudio.Play(_sim.Outcome == AOutcome.Won ? Cue.Won : Cue.Failed);
            }
            _lastOutcome = _sim.Outcome;
        }

        public void Refresh(float dt)
        {
            _time += dt;
            _trauma = Mathf.Max(0f, _trauma - (dt * 1.6f));

            RefreshCells();
            RefreshWarnings();
            RefreshActors();
            AdvanceDrops(dt);
            RefreshHud();
            FollowCamera();

            GameAudio.SetFireLevel(_sim.Outcome == AOutcome.Playing ? FireLoudness() : 0f, dt);
        }

        // ------------------------------------------------------------------
        // 월드
        // ------------------------------------------------------------------

        private static Vector3 CellCenter(int x, int y)
        {
            return new Vector3(x + 0.5f, -(y + 0.5f), 0f);
        }

        private static Vector3 ToWorld(Vec2 p)
        {
            return new Vector3(p.X, -p.Y, 0f);
        }

        private void BuildWorld()
        {
            for (int i = _root.childCount - 1; i >= 0; i--) UiKit.Discard(_root.GetChild(i).gameObject);
            _people.Clear();
            _drops.Clear();
            _dropPool.Clear();

            int n = _sim.Width * _sim.Height;
            _tint = new SpriteRenderer[n];
            _fire = new SpriteRenderer[n];
            _tongue = new SpriteRenderer[n];
            _warn = new SpriteRenderer[n];

            for (int y = 0; y < _sim.Height; y++)
            {
                for (int x = 0; x < _sim.Width; x++)
                {
                    int i = (y * _sim.Width) + x;
                    Vector3 at = CellCenter(x, y);
                    char t = _sim.Tile(x, y);
                    bool alt = ((x * 7) + (y * 13)) % 3 == 0;

                    if (t == '#')
                    {
                        Sprite("Wall", Art.Wall(WallStyle.Concrete, WallMask(x, y)), at, 0, 1f);
                    }
                    else
                    {
                        string floor = t == 'X' ? "TopDown/grass_a" : _sim.Room(x, y) >= 0 && RoomIsHall(x, y) ? (alt ? "TopDown/floor_stone_b" : "TopDown/floor_stone_a") : (alt ? "TopDown/floor_wood_b" : "TopDown/floor_wood_a");
                        SpriteRenderer f = Sprite("Floor", Art.Get(floor), at, 0, 1f);
                        // 복도 돌바닥이 푸르스름해 젖은 칸과 헷갈린다. 회색으로 눌러 준다.
                        if (RoomIsHall(x, y) && t != 'X') f.color = new Color(0.72f, 0.7f, 0.66f);
                    }
                    if (t == 'D') Sprite("Door", Art.Get(DoorVertical(x, y) ? "TopDown/door_vertical" : "TopDown/door_horizontal"), at, 1, 1f);
                    if (t == 'H') Sprite("Hydrant", Art.Get("TopDown/hydrant"), at, 3, 0.8f);
                    if (t == 'X') Label("출구", at, new Color(0.6f, 1f, 0.6f));

                    _tint[i] = Sprite("Tint", Art.White, at, 2, 1f);
                    _tint[i].color = Color.clear;
                    _warn[i] = Sprite("Warn", Art.White, at, 3, 1f);
                    _warn[i].color = Color.clear;
                    _fire[i] = Sprite("Fire", Art.Get("Effects/fire_01"), at, 6, 1f);
                    _fire[i].enabled = false;
                    _tongue[i] = Sprite("Tongue", Art.Get("Effects/flame_05"), at + new Vector3(0f, 0.3f, 0f), 7, 0.8f);
                    _tongue[i].enabled = false;
                }
            }

            string[] faces = { "TopDown/civilian_woman", "TopDown/civilian_old", "TopDown/civilian_man" };
            for (int c = 0; c < _sim.People.Count; c++) _people.Add(Sprite("Person", Art.Get(faces[c % faces.Length]), Vector3.zero, 10, 0.8f));
            _player = Sprite("Player", Art.Get("TopDown/player_suit_0"), Vector3.zero, 11, 0.85f);
        }

        /// <summary>복도(가장 큰 공간)는 타일 바닥으로 칠해 방과 구분한다.</summary>
        private bool RoomIsHall(int x, int y)
        {
            return y >= 7 && y <= 9;
        }

        private bool DoorVertical(int x, int y)
        {
            bool left = x > 0 && _sim.Tile(x - 1, y) == '#';
            bool right = x < _sim.Width - 1 && _sim.Tile(x + 1, y) == '#';
            return !(left && right);
        }

        private int WallMask(int x, int y)
        {
            int mask = 0;
            if (IsWall(x, y - 1)) mask |= Art.ConnectNorth;
            if (IsWall(x + 1, y)) mask |= Art.ConnectEast;
            if (IsWall(x, y + 1)) mask |= Art.ConnectSouth;
            if (IsWall(x - 1, y)) mask |= Art.ConnectWest;
            return mask;
        }

        private bool IsWall(int x, int y)
        {
            return _sim.InBounds(x, y) && _sim.Tile(x, y) == '#';
        }

        private SpriteRenderer Sprite(string name, Sprite sprite, Vector3 at, int order, float cells)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = at;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            float s = Art.FitWidth(sprite, cells);
            go.transform.localScale = new Vector3(s, s, 1f);
            return r;
        }

        private void Label(string text, Vector3 at, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = at + new Vector3(0f, 0f, -0.1f);
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = Art.Font;
            mesh.GetComponent<MeshRenderer>().sharedMaterial = Art.Font.material;
            mesh.GetComponent<MeshRenderer>().sortingOrder = 20;
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = 0.06f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.color = color;
        }

        private void RefreshCells()
        {
            for (int y = 0; y < _sim.Height; y++)
            {
                for (int x = 0; x < _sim.Width; x++)
                {
                    int i = (y * _sim.Width) + x;
                    float heat = _sim.Heat(x, y);
                    bool burning = _sim.Burning(x, y);

                    Color tint = Color.clear;
                    if (_sim.Burnt(x, y)) tint = new Color(0.05f, 0.04f, 0.04f, 0.7f);
                    else if (burning) tint = new Color(0.95f, 0.3f + (0.1f * Mathf.Sin((_time * 7f) + x + (y * 1.3f))), 0.05f, 0.45f + (0.25f * heat));
                    else if (heat > 0.05f) tint = new Color(1f, 0.35f, 0.1f, heat * 0.55f);   // 달아오르는 칸: 곧 붙는다
                    if (!burning && _sim.Wet(x, y) > 0f) tint = Color.Lerp(tint, new Color(0.3f, 0.6f, 1f, 0.45f), 0.7f);
                    _tint[i].color = tint;

                    _fire[i].enabled = burning;
                    _tongue[i].enabled = burning;
                    if (!burning) continue;

                    float phase = (x * 1.7f) + (y * 2.3f);
                    float wobble = 1f + (0.1f * Mathf.Sin((_time * 10f) + phase));
                    float size = (0.6f + (0.7f * heat)) * wobble;
                    float fs = Art.FitWidth(_fire[i].sprite, size);
                    _fire[i].transform.localScale = new Vector3(fs, fs, 1f);
                    _fire[i].color = Color.Lerp(new Color(1f, 0.85f, 0.35f), new Color(1f, 0.4f, 0.1f), heat);
                    float ts = Art.FitWidth(_tongue[i].sprite, (0.5f + (0.5f * heat)) * wobble);
                    _tongue[i].transform.localScale = new Vector3(ts, ts * (1.1f + (0.2f * Mathf.Sin((_time * 14f) + phase))), 1f);
                    _tongue[i].color = new Color(1f, 0.75f, 0.25f, 0.9f);
                }
            }
        }

        private void RefreshWarnings()
        {
            for (int i = 0; i < _warn.Length; i++) _warn[i].color = Color.clear;
            string alert = "";
            float pulse = 0.5f + (0.5f * Mathf.Sin(_time * 18f));

            foreach (Warning w in _sim.Warnings)
            {
                if (w.Kind == WarningKind.FlareUp)
                {
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            if ((dx * dx) + (dy * dy) > 5) continue;
                            int x = w.At.X + dx, y = w.At.Y + dy;
                            if (_sim.InBounds(x, y) && _sim.Tile(x, y) != '#') _warn[(y * _sim.Width) + x].color = new Color(1f, 0.9f, 0.2f, 0.25f + (0.3f * pulse));
                        }
                    }
                    if (alert == "") alert = "불길이 솟는다! 물러서거나 그 불을 꺼라";
                }
                else
                {
                    for (int y = 0; y < _sim.Height; y++)
                    {
                        for (int x = 0; x < _sim.Width; x++)
                        {
                            if (_sim.Room(x, y) == w.Room) _warn[(y * _sim.Width) + x].color = new Color(1f, 0.1f, 0.05f, 0.25f + (0.35f * pulse));
                        }
                    }
                    alert = "플래시오버 " + w.SecondsLeft.ToString("0.0") + "초! 방에서 나와라";
                }
            }

            _alert.text = alert;
            _vignette.color = new Color(1f, 0.1f, 0f, Mathf.Clamp01(_trauma * 0.35f + (alert.StartsWith("플래시") ? 0.15f * pulse : 0f)));
        }

        private void RefreshActors()
        {
            for (int c = 0; c < _people.Count; c++)
            {
                Person p = _sim.People[c];
                bool shown = !p.Rescued && !p.Lost;
                _people[c].enabled = shown;
                if (!shown) continue;
                _people[c].transform.localPosition = ToWorld(p.Pos);
                float hurt = 1f - (p.Hp / 100f);
                _people[c].color = p.Following ? Color.Lerp(new Color(0.8f, 1f, 0.8f), Color.red, hurt) : Color.Lerp(Color.white, Color.red, hurt);
            }

            _player.transform.localPosition = ToWorld(_sim.Player);
            // 그림은 동쪽을 본다. 격자 각도(아래가 +)를 월드 각도로 뒤집는다.
            _player.transform.localRotation = Quaternion.Euler(0f, 0f, -_sim.Aim * Mathf.Rad2Deg);
        }

        private void SpawnDrop(Vector3 from, Vector3 to)
        {
            SpriteRenderer sprite;
            if (_dropPool.Count > 0)
            {
                sprite = _dropPool.Pop();
                sprite.enabled = true;
            }
            else
            {
                sprite = Sprite("Drop", Art.Get("Effects/water_drop"), from, 12, 0.3f);
            }
            sprite.color = new Color(0.55f, 0.85f, 1f, 0.9f);
            float life = 0.08f + (0.04f * (to - from).magnitude);
            _drops.Add(new Drop { Sprite = sprite, From = from, To = to, Age = 0f, Life = life });
        }

        private void AdvanceDrops(float dt)
        {
            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                Drop d = _drops[i];
                d.Age += dt;
                if (d.Age >= d.Life)
                {
                    d.Sprite.enabled = false;
                    _dropPool.Push(d.Sprite);
                    _drops.RemoveAt(i);
                    continue;
                }
                float t = d.Age / d.Life;
                d.Sprite.transform.localPosition = Vector3.Lerp(d.From, d.To, t) + new Vector3(0f, 0f, -0.05f);
                _drops[i] = d;
            }
        }

        /// <summary>건물 전체를 한 화면에 담는다 — 다른 방의 플래시오버 예고가 보여야 판단할 수 있다.</summary>
        private void FollowCamera()
        {
            if (_camera == null) return;
            float aspect = _camera.aspect > 0f ? _camera.aspect : 16f / 9f;
            _camera.orthographicSize = Mathf.Max((_sim.Height * 0.5f) + 1.6f, ((_sim.Width * 0.5f) + 0.3f) / aspect);
            _camera.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
            float x = _sim.Width * 0.5f;
            float y = (-_sim.Height * 0.5f) + 0.5f;

            float amp = _trauma * _trauma * 0.4f;
            float t = Time.realtimeSinceStartup * 25f;
            x += amp * ((Mathf.PerlinNoise(t, 0f) * 2f) - 1f);
            y += amp * ((Mathf.PerlinNoise(0f, t) * 2f) - 1f);
            _camera.transform.position = new Vector3(x, y, -10f);
        }

        private float FireLoudness()
        {
            float sum = 0f;
            int cx = (int)_sim.Player.X, cy = (int)_sim.Player.Y;
            for (int y = cy - 7; y <= cy + 7; y++)
            {
                for (int x = cx - 7; x <= cx + 7; x++)
                {
                    if (!_sim.InBounds(x, y) || !_sim.Burning(x, y)) continue;
                    float dx = (x + 0.5f) - _sim.Player.X, dy = (y + 0.5f) - _sim.Player.Y;
                    sum += 1f / (1f + (((dx * dx) + (dy * dy)) * 0.15f));
                }
            }
            return 1f - Mathf.Exp(-sum / FeelMath.FullFire);
        }

        // ------------------------------------------------------------------
        // HUD
        // ------------------------------------------------------------------

        private void BuildHud()
        {
            _vignette = UiKit.Image(_hud, "Vignette", Art.Get("Effects/glow"), Color.clear);
            UiKit.Stretch(_vignette.rectTransform);

            _title = UiKit.OutlinedLabel(_hud, "Title", "시험판 A · 3층 사무실", 40, Color.white, TextAnchor.UpperLeft);
            UiKit.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -24f), new Vector2(800f, 60f));

            _status = UiKit.OutlinedLabel(_hud, "Status", "", 34, new Color(1f, 0.95f, 0.8f), TextAnchor.UpperRight);
            UiKit.Place(_status.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -24f), new Vector2(900f, 60f));

            _hpFill = Bar("Hp", new Vector2(40f, -96f), new Color(0.9f, 0.25f, 0.2f), "체력");
            _tankFill = Bar("Tank", new Vector2(40f, -140f), new Color(0.3f, 0.7f, 1f), "물");

            _alert = UiKit.OutlinedLabel(_hud, "Alert", "", 44, new Color(1f, 0.85f, 0.3f), TextAnchor.UpperCenter);
            UiKit.Place(_alert.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1400f, 60f));

            _help = UiKit.OutlinedLabel(_hud, "Help", "WASD 이동 · 마우스 조준 · 왼쪽 버튼 누르고 있으면 분사 · 소화전 옆에 서면 물 충전 · 사람에게 닿으면 따라온다      R 다시  Tab 시험판 B", 26, new Color(0.8f, 0.8f, 0.85f), TextAnchor.LowerCenter);
            UiKit.Place(_help.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1850f, 50f));

            _bannerBack = UiKit.Image(_hud, "BannerBack", Art.White, new Color(0f, 0f, 0f, 0.7f));
            UiKit.Place(_bannerBack.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 240f));
            _banner = UiKit.OutlinedLabel(_bannerBack.transform, "Banner", "", 52, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(_banner.rectTransform);
        }

        private Image Bar(string name, Vector2 at, Color color, string label)
        {
            Image back = UiKit.Image(_hud, name + "Back", Art.White, new Color(0f, 0f, 0f, 0.55f));
            UiKit.Place(back.rectTransform, new Vector2(0f, 1f), at + new Vector2(70f, 0f), new Vector2(360f, 30f));
            Image fill = UiKit.Image(back.transform, name + "Fill", Art.White, color);
            fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            fill.rectTransform.anchorMax = new Vector2(1f, 1f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.offsetMin = new Vector2(3f, 3f);
            fill.rectTransform.offsetMax = new Vector2(-3f, -3f);
            Text text = UiKit.OutlinedLabel(_hud, name + "Label", label, 26, Color.white, TextAnchor.MiddleLeft);
            UiKit.Place(text.rectTransform, new Vector2(0f, 1f), at, new Vector2(70f, 30f));
            return fill;
        }

        private void RefreshHud()
        {
            float left = Mathf.Max(0f, ActionSim.TimeLimit - _sim.Elapsed);
            _status.text = "구조 " + _sim.Rescued + "/" + _sim.People.Count + "   잃음 " + _sim.LostCount + "   남은 시간 " + Mathf.CeilToInt(left) + "초";
            _hpFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(_sim.Hp / 100f), 1f, 1f);
            _tankFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(_sim.Tank), 1f, 1f);

            bool over = _sim.Outcome != AOutcome.Playing;
            _bannerBack.gameObject.SetActive(over);
            if (over)
            {
                _banner.text = _sim.Outcome == AOutcome.Won
                    ? "구조 성공! " + _sim.Rescued + "/" + _sim.People.Count + "명 · " + Mathf.RoundToInt(_sim.Elapsed) + "초\n클릭하면 다른 판"
                    : "실패 · 구조 " + _sim.Rescued + "/" + _sim.People.Count + "\n클릭하면 다른 판";
            }
        }
    }
}
