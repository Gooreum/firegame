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
    /// 시험판 B 화면. 규칙은 전부 <see cref="TacticsGame"/>에 있고 여기는 보여 주고 입력을 옮기기만 한다.
    ///
    /// 핵심은 미리보기다: 다음 턴에 불이 붙을 칸(주황 + 어느 불에서 오는지 선), 터질 가스통 범위(빨강),
    /// 대피하는 사람이 걸을 길(초록 발자국), 그리고 분사를 겨누면 "이 수로 막히는 확산"(회색 X).
    /// </summary>
    public sealed class TacticsView : IPrototype
    {
        public enum Mode
        {
            Move,
            Spray,
            Door,
        }

        // 레벨 진행은 다시 시작(R)해도 유지한다.
        private static int _levelIndex;

        private readonly Transform _root;
        private readonly Camera _camera;
        private readonly Canvas _canvas;
        private readonly RectTransform _hud;

        private TacticsGame _game;
        private Mode _mode = Mode.Move;
        private Dir _aim = Dir.E;
        private GridPoint? _hover;
        private string _message = "";
        private float _messageAge = 99f;
        private float _time;

        // 칸마다 고정 그림
        private SpriteRenderer[] _floor;
        private SpriteRenderer[] _tint;
        private SpriteRenderer[] _fire;
        private SpriteRenderer[] _tongue;
        private SpriteRenderer[] _mark;
        private SpriteRenderer _player;
        private readonly List<SpriteRenderer> _civs = new List<SpriteRenderer>();
        private readonly List<TextMesh> _civLabels = new List<TextMesh>();

        // 매 프레임 다시 그리는 표시(선·X·발자국)
        private readonly List<SpriteRenderer> _lines = new List<SpriteRenderer>();
        private readonly List<TextMesh> _texts = new List<TextMesh>();
        private int _lineUsed;
        private int _textUsed;

        private Text _title;
        private Text _status;
        private Text _help;
        private Text _hint;
        private Text _toast;
        private Text _banner;
        private Image _bannerBack;

        private static readonly Color PreviewFire = new Color(1f, 0.55f, 0.1f, 0.55f);
        private static readonly Color PreviewBlast = new Color(1f, 0.15f, 0.1f, 0.5f);
        private static readonly Color ReachColor = new Color(0.15f, 0.55f, 1f, 0.42f);
        private static readonly Color SprayColor = new Color(0.4f, 0.85f, 1f, 0.55f);
        private static readonly Color WalkColor = new Color(0.35f, 1f, 0.45f, 0.9f);

        public TacticsView(Transform parent, Camera camera, Canvas canvas)
        {
            _camera = camera;
            _canvas = canvas;
            _root = new GameObject("TacticsWorld").transform;
            _root.SetParent(parent, false);

            _hud = UiKit.Node(canvas.transform, "TacticsHud");
            UiKit.Stretch(_hud);
            BuildHud();

            LoadLevel(_levelIndex);
        }

        public TacticsGame Game
        {
            get { return _game; }
        }

        public void LoadLevel(int index)
        {
            _levelIndex = Mathf.Clamp(index, 0, TacticsLevels.All.Length - 1);
            _game = TacticsGame.Load(TacticsLevels.All[_levelIndex]);
            _mode = Mode.Move;
            BuildWorld();
            FrameCamera();
        }

        /// <summary>스크린샷 하니스용: 분사 모드로 이 방향을 겨눈 화면.</summary>
        public void AimSpray(Dir dir)
        {
            _mode = Mode.Spray;
            _aim = dir;
        }

        public void Destroy()
        {
            UiKit.Discard(_root.gameObject);
            UiKit.Discard(_hud.gameObject);
        }

        // ------------------------------------------------------------------
        // 입력
        // ------------------------------------------------------------------

        public void Tick(float dt, in ProtoInput input)
        {
            _time += dt;
            _messageAge += dt;
            HandleInput(input);
            Refresh();
        }

        private void HandleInput(in ProtoInput input)
        {
            _hover = MouseCell(input.Mouse);

            if (_game.Outcome != TOutcome.Playing)
            {
                if (input.EndTurn || input.MouseClicked)
                {
                    bool won = _game.Outcome == TOutcome.Won;
                    LoadLevel(won ? (_levelIndex + 1) % TacticsLevels.All.Length : _levelIndex);
                }
                return;
            }

            if (input.Cancel || input.RightClicked) _mode = Mode.Move;
            if (input.Key1) _mode = _mode == Mode.Spray ? Mode.Move : Mode.Spray;
            if (input.Key2) _mode = _mode == Mode.Door ? Mode.Move : Mode.Door;
            if (input.Key3) Try(TAction.Refill, _game.Player);

            if (input.Undo)
            {
                _game.UndoTurn();
                _mode = Mode.Move;
                Say("이번 턴을 되돌렸다", 1.5f);
            }

            if (input.EndTurn)
            {
                int lostBefore = _game.LostCount;
                int rescuedBefore = _game.Rescued;
                _game.EndTurn();
                _mode = Mode.Move;
                if (_game.LostCount > lostBefore) { GameAudio.Play(Cue.CivilianLost); Say("사람을 잃었다…", 2f); }
                if (_game.Rescued > rescuedBefore) GameAudio.Play(Cue.Rescued);
                if (_game.Outcome == TOutcome.Won) GameAudio.Play(Cue.Won);
                if (_game.Outcome == TOutcome.Lost) GameAudio.Play(Cue.Failed);
                return;
            }

            if (_mode == Mode.Spray && _hover.HasValue) _aim = AimToward(_hover.Value);

            if (!input.MouseClicked || !_hover.HasValue) return;
            GridPoint cell = _hover.Value;

            switch (_mode)
            {
                case Mode.Move:
                    if (cell.Equals(_game.Player)) { _mode = Mode.Spray; break; }
                    int evacBefore = CountEvacuating();
                    if (Try(TAction.Move, cell) && CountEvacuating() > evacBefore)
                    {
                        GameAudio.Play(Cue.PickUp);
                        Say("대피 시작! 초록 발자국 길을 지켜 줘라", 2.5f);
                    }
                    break;

                case Mode.Spray:
                    if (Try(TAction.Spray, Dirs.Step(_game.Player, _aim)))
                    {
                        GameAudio.Play(Cue.SprayWater);
                        GameAudio.Play(Cue.PutOut);
                        _mode = Mode.Move;
                    }
                    break;

                case Mode.Door:
                    if (Try(TAction.ToggleDoor, cell)) _mode = Mode.Move;
                    break;
            }
        }

        private bool Try(TAction action, GridPoint target)
        {
            if (_game.CanDo(action, target, out string why)) return _game.Do(action, target);
            Say(why, 1.8f);
            return false;
        }

        private int CountEvacuating()
        {
            int n = 0;
            foreach (CivState s in _game.CivilianStates) if (s == CivState.Evacuating) n++;
            return n;
        }

        private Dir AimToward(GridPoint cell)
        {
            int dx = cell.X - _game.Player.X;
            int dy = cell.Y - _game.Player.Y;
            if (dx == 0 && dy == 0) return _aim;
            if (Mathf.Abs(dx) >= Mathf.Abs(dy)) return dx > 0 ? Dir.E : Dir.W;
            return dy > 0 ? Dir.S : Dir.N;
        }

        private GridPoint? MouseCell(Vector2 screen)
        {
            if (_camera == null) return null;
            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));
            int x = Mathf.FloorToInt(world.x + (_game.Width * 0.5f));
            int y = Mathf.FloorToInt((_game.Height * 0.5f) - world.y);
            return _game.InBounds(x, y) ? new GridPoint(x, y) : (GridPoint?)null;
        }

        private void Say(string text, float seconds)
        {
            _message = text ?? "";
            _messageAge = -seconds;
        }

        // ------------------------------------------------------------------
        // 월드
        // ------------------------------------------------------------------

        private Vector3 CellCenter(int x, int y)
        {
            return new Vector3(x - (_game.Width * 0.5f) + 0.5f, (_game.Height * 0.5f) - y - 0.5f, 0f);
        }

        private void FrameCamera()
        {
            if (_camera == null) return;
            float aspect = _camera.aspect > 0f ? _camera.aspect : 16f / 9f;
            // 위아래로 HUD 자리를 남긴다.
            _camera.orthographicSize = Mathf.Max((_game.Height * 0.5f) + 1.9f, ((_game.Width * 0.5f) + 0.6f) / aspect);
            _camera.transform.position = new Vector3(0f, 0.35f, -10f);
            _camera.backgroundColor = new Color(0.09f, 0.09f, 0.11f);
        }

        private void BuildWorld()
        {
            for (int i = _root.childCount - 1; i >= 0; i--) UiKit.Discard(_root.GetChild(i).gameObject);
            _civs.Clear();
            _civLabels.Clear();
            _lines.Clear();
            _texts.Clear();

            int n = _game.Width * _game.Height;
            _floor = new SpriteRenderer[n];
            _tint = new SpriteRenderer[n];
            _fire = new SpriteRenderer[n];
            _tongue = new SpriteRenderer[n];
            _mark = new SpriteRenderer[n];

            for (int y = 0; y < _game.Height; y++)
            {
                for (int x = 0; x < _game.Width; x++)
                {
                    int i = (y * _game.Width) + x;
                    Vector3 at = CellCenter(x, y);
                    TTile tile = _game.Tile(x, y);

                    _floor[i] = Sprite("Floor", FloorSprite(tile == TTile.Door ? TTile.Floor : tile, x, y), at, 0, 1f);
                    if (tile == TTile.Door) Sprite("Door", Art.Get(DoorVertical(x, y) ? "TopDown/door_vertical" : "TopDown/door_horizontal"), at, 1, 1f);
                    if (tile == TTile.Wall) _floor[i].sprite = Art.Wall(WallStyle.Wood, WallMask(x, y));
                    _tint[i] = Sprite("Tint", Art.White, at, 1, 1f);
                    _tint[i].color = Color.clear;

                    if (tile == TTile.Hydrant) Sprite("Hydrant", Art.Get("TopDown/hydrant"), at, 3, 0.8f);
                    if (tile == TTile.Gas) Sprite("Gas", Art.Get("Props/barrel_red"), at, 3, 0.7f);
                    if (tile == TTile.Exit) Label("출구", at + new Vector3(0f, 0f, -0.1f), 0.07f, new Color(0.6f, 1f, 0.6f), 22);

                    _mark[i] = Sprite("Mark", Art.White, at, 2, 0.92f);
                    _mark[i].color = Color.clear;
                    _fire[i] = Sprite("Fire", Art.Get("Effects/fire_01"), at, 6, 0.95f);
                    _fire[i].enabled = false;
                    _tongue[i] = Sprite("Tongue", Art.Get("Effects/flame_05"), at + new Vector3(0f, 0.25f, 0f), 7, 0.7f);
                    _tongue[i].enabled = false;
                }
            }

            string[] faces = { "TopDown/civilian_woman", "TopDown/civilian_old", "TopDown/civilian_man" };
            for (int c = 0; c < _game.CivilianPositions.Count; c++)
            {
                _civs.Add(Sprite("Civilian", Art.Get(faces[c % faces.Length]), Vector3.zero, 10, 0.8f));
                _civLabels.Add(Label("", Vector3.zero, 0.06f, Color.white, 30));
            }

            _player = Sprite("Player", Art.Get("TopDown/player_suit_0"), Vector3.zero, 11, 0.85f);
        }

        private Sprite FloorSprite(TTile tile, int x, int y)
        {
            bool alt = ((x * 7) + (y * 13)) % 3 == 0;
            switch (tile)
            {
                case TTile.Concrete: return Art.Get(alt ? "TopDown/floor_stone_b" : "TopDown/floor_stone_a");
                case TTile.Exit: return Art.Get("TopDown/grass_a");
                default: return Art.Get(alt ? "TopDown/floor_wood_b" : "TopDown/floor_wood_a");
            }
        }

        private bool DoorVertical(int x, int y)
        {
            // 좌우가 벽이면 가로 벽에 난 문이다.
            bool wallLeft = _game.InBounds(x - 1, y) && _game.Tile(x - 1, y) == TTile.Wall;
            bool wallRight = _game.InBounds(x + 1, y) && _game.Tile(x + 1, y) == TTile.Wall;
            return !(wallLeft && wallRight);
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
            return _game.InBounds(x, y) && _game.Tile(x, y) == TTile.Wall;
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

        private TextMesh Label(string text, Vector3 at, float size, Color color, int order)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = at;
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = Art.Font;
            mesh.GetComponent<MeshRenderer>().sharedMaterial = Art.Font.material;
            mesh.GetComponent<MeshRenderer>().sortingOrder = order;
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = size;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            return mesh;
        }

        // ------------------------------------------------------------------
        // 매 프레임
        // ------------------------------------------------------------------

        public void Refresh()
        {
            _lineUsed = 0;
            _textUsed = 0;

            RefreshCells();
            RefreshPreview();
            RefreshPeople();
            RefreshHud();

            for (int i = _lineUsed; i < _lines.Count; i++) _lines[i].enabled = false;
            for (int i = _textUsed; i < _texts.Count; i++) _texts[i].gameObject.SetActive(false);
        }

        private void RefreshCells()
        {
            for (int y = 0; y < _game.Height; y++)
            {
                for (int x = 0; x < _game.Width; x++)
                {
                    int i = (y * _game.Width) + x;
                    TTile tile = _game.Tile(x, y);

                    Color tint = Color.clear;
                    if (_game.Burnt(x, y)) tint = new Color(0.05f, 0.04f, 0.04f, 0.65f);
                    if (_game.Burning(x, y)) tint = new Color(0.9f, 0.25f + (0.08f * Mathf.Sin((_time * 6f) + x + y)), 0.05f, 0.55f);
                    else if (_game.Wet(x, y) > 0) tint = new Color(0.25f, 0.55f, 1f, 0.25f + (0.12f * _game.Wet(x, y)));
                    if (tile == TTile.Door && _game.DoorClosed(x, y)) tint = new Color(0.35f, 0.2f, 0.1f, 0.85f);
                    _tint[i].color = tint;

                    bool burning = _game.Burning(x, y);
                    _fire[i].enabled = burning;
                    _tongue[i].enabled = burning;
                    if (burning)
                    {
                        // 오래 탄 불일수록 크고 어둡다. 흔들림은 칸마다 위상을 다르게.
                        int age = _game.FireAge(x, y);
                        float phase = (x * 1.7f) + (y * 2.3f);
                        float wobble = 1f + (0.08f * Mathf.Sin((_time * 9f) + phase));
                        float size = (1.05f + (0.12f * age)) * wobble;
                        float fs = Art.FitWidth(_fire[i].sprite, size);
                        _fire[i].transform.localScale = new Vector3(fs, fs, 1f);
                        _fire[i].color = Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.35f, 0.1f), age / 3f);
                        float ts = Art.FitWidth(_tongue[i].sprite, 0.8f * wobble);
                        _tongue[i].transform.localScale = new Vector3(ts, ts * (1.1f + (0.15f * Mathf.Sin((_time * 13f) + phase))), 1f);
                        _tongue[i].color = new Color(1f, 0.7f, 0.2f, 0.85f);
                    }

                    if (_game.GasArmed(x, y))
                    {
                        _fire[i].enabled = true;
                        _fire[i].color = new Color(1f, 0.2f, 0.1f, 0.5f + (0.5f * Mathf.Abs(Mathf.Sin(_time * 8f))));
                    }

                    _mark[i].color = Color.clear;
                }
            }
        }

        private void RefreshPreview()
        {
            float pulse = 0.75f + (0.25f * Mathf.Sin(_time * 5f));

            // 이동 가능 칸
            if (_mode == Mode.Move && _game.Outcome == TOutcome.Playing && _game.Actions > 0)
            {
                foreach (KeyValuePair<GridPoint, int> kv in _game.Reachable()) Mark(kv.Key, ReachColor);
                if (_hover.HasValue && _game.Reachable().ContainsKey(_hover.Value)) Mark(_hover.Value, new Color(0.5f, 0.85f, 1f, 0.55f));
            }

            // 문 모드: 닿는 문
            if (_mode == Mode.Door)
            {
                foreach (Dir d in Dirs.All)
                {
                    GridPoint p = Dirs.Step(_game.Player, d);
                    if (_game.InBounds(p.X, p.Y) && _game.Tile(p.X, p.Y) == TTile.Door) Mark(p, new Color(1f, 0.9f, 0.3f, 0.6f));
                }
            }

            // 다음 턴 확산: 칸을 주황으로 칠하고, 어느 불에서 오는지 선을 긋는다.
            foreach (Spread s in _game.PreviewSpread())
            {
                Mark(s.To, new Color(PreviewFire.r, PreviewFire.g, PreviewFire.b, PreviewFire.a * pulse));
                GridPoint from = Dirs.Step(s.To, s.From);
                Arrow(CellCenter(from.X, from.Y), CellCenter(s.To.X, s.To.Y), new Color(1f, 0.6f, 0.15f, 0.95f));
                if (_game.Tile(s.To.X, s.To.Y) == TTile.Gas) Text("점화!", CellCenter(s.To.X, s.To.Y) + new Vector3(0f, 0.35f, 0f), new Color(1f, 0.4f, 0.2f), 0.05f);
            }

            List<GridPoint> blast = _game.PreviewExplosions();
            foreach (GridPoint p in blast) Mark(p, new Color(PreviewBlast.r, PreviewBlast.g, PreviewBlast.b, PreviewBlast.a * pulse));
            if (blast.Count > 0) Text("폭발 예고", CellCenter(blast[blast.Count / 2].X, blast[blast.Count / 2].Y) + new Vector3(0f, 0.2f, 0f), Color.white, 0.06f);

            // 대피하는 사람의 다음 걸음
            List<GridPoint>[] walks = _game.PreviewWalks();
            for (int c = 0; c < walks.Length; c++)
            {
                if (walks[c] == null) continue;
                Vector3 prev = CellCenter(_game.CivilianPositions[c].X, _game.CivilianPositions[c].Y);
                foreach (GridPoint p in walks[c])
                {
                    Vector3 next = CellCenter(p.X, p.Y);
                    Arrow(prev, next, WalkColor);
                    prev = next;
                }
            }

            // 분사 조준: 맞을 칸 + 이 수로 막히는 확산(회색 X)
            if (_mode == Mode.Spray && _game.Outcome == TOutcome.Playing)
            {
                foreach (GridPoint p in _game.SprayCells(_aim)) Mark(p, SprayColor);
                foreach (GridPoint p in _game.SprayBlocks(_aim)) Text("X", CellCenter(p.X, p.Y), new Color(0.85f, 0.85f, 0.85f), 0.11f);
            }
        }

        private void RefreshPeople()
        {
            var danger = new HashSet<GridPoint>();
            foreach (Spread s in _game.PreviewSpread()) danger.Add(s.To);
            foreach (GridPoint p in _game.PreviewExplosions()) danger.Add(p);
            List<GridPoint>[] walks = _game.PreviewWalks();

            for (int c = 0; c < _civs.Count; c++)
            {
                CivState state = _game.CivilianStates[c];
                bool shown = state == CivState.Waiting || state == CivState.Evacuating;
                _civs[c].enabled = shown;
                _civLabels[c].gameObject.SetActive(shown);
                if (!shown) continue;

                GridPoint at = _game.CivilianPositions[c];
                Vector3 world = CellCenter(at.X, at.Y);
                _civs[c].transform.localPosition = world;
                _civs[c].color = state == CivState.Evacuating ? new Color(0.8f, 1f, 0.8f) : Color.white;

                // 다음 턴에 설 자리가 불에 먹히면 경고한다.
                GridPoint end = walks[c] != null && walks[c].Count > 0 ? walks[c][walks[c].Count - 1] : at;
                bool threatened = danger.Contains(end);
                _civLabels[c].transform.localPosition = world + new Vector3(0f, 0.6f, 0f);
                _civLabels[c].text = threatened ? "위험!" : state == CivState.Evacuating ? "대피 중" : "살려줘!";
                _civLabels[c].color = threatened ? new Color(1f, 0.3f, 0.25f) : state == CivState.Evacuating ? WalkColor : new Color(1f, 0.95f, 0.6f);
            }

            _player.transform.localPosition = CellCenter(_game.Player.X, _game.Player.Y);
            float angle = _aim == Dir.E ? 0f : _aim == Dir.N ? 90f : _aim == Dir.W ? 180f : -90f;
            _player.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void Mark(GridPoint p, Color color)
        {
            int i = (p.Y * _game.Width) + p.X;
            // 여러 표시가 겹치면 진한 쪽을 쓴다.
            if (_mark[i].color.a < color.a) _mark[i].color = color;
        }

        private void Arrow(Vector3 from, Vector3 to, Color color)
        {
            SpriteRenderer line = NextLine();
            Vector3 mid = (from + to) * 0.5f;
            Vector3 d = to - from;
            line.transform.localPosition = new Vector3(mid.x, mid.y, -0.05f);
            line.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            line.transform.localScale = new Vector3(d.magnitude * 0.7f, 0.09f, 1f);
            line.color = color;

            // 화살촉 대신 도착 칸 쪽 끝에 점을 찍는다.
            SpriteRenderer tip = NextLine();
            Vector3 head = from + (d * 0.82f);
            tip.transform.localPosition = new Vector3(head.x, head.y, -0.05f);
            tip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tip.transform.localScale = new Vector3(0.2f, 0.2f, 1f);
            tip.color = color;
        }

        private SpriteRenderer NextLine()
        {
            if (_lineUsed == _lines.Count)
            {
                var go = new GameObject("Line");
                go.transform.SetParent(_root, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = Art.White;
                r.sortingOrder = 15;
                _lines.Add(r);
            }
            SpriteRenderer line = _lines[_lineUsed++];
            line.enabled = true;
            return line;
        }

        private void Text(string text, Vector3 at, Color color, float size)
        {
            if (_textUsed == _texts.Count) _texts.Add(Label("", Vector3.zero, size, color, 20));
            TextMesh mesh = _texts[_textUsed++];
            mesh.gameObject.SetActive(true);
            mesh.transform.localPosition = at + new Vector3(0f, 0f, -0.1f);
            mesh.text = text;
            mesh.color = color;
            mesh.characterSize = size;
        }

        // ------------------------------------------------------------------
        // HUD
        // ------------------------------------------------------------------

        private void BuildHud()
        {
            _title = UiKit.OutlinedLabel(_hud, "Title", "", 40, Color.white, TextAnchor.UpperLeft);
            UiKit.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -24f), new Vector2(800f, 60f));

            _status = UiKit.OutlinedLabel(_hud, "Status", "", 34, new Color(1f, 0.95f, 0.8f), TextAnchor.UpperRight);
            UiKit.Place(_status.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -24f), new Vector2(1100f, 110f));

            _hint = UiKit.OutlinedLabel(_hud, "Hint", "", 26, new Color(0.85f, 0.9f, 1f), TextAnchor.UpperLeft);
            UiKit.Place(_hint.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -84f), new Vector2(1000f, 50f));

            _help = UiKit.OutlinedLabel(_hud, "Help", "", 26, new Color(0.8f, 0.8f, 0.85f), TextAnchor.LowerCenter);
            UiKit.Place(_help.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1800f, 50f));

            _toast = UiKit.OutlinedLabel(_hud, "Toast", "", 32, new Color(1f, 0.85f, 0.4f), TextAnchor.LowerCenter);
            UiKit.Place(_toast.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(1600f, 50f));

            _bannerBack = UiKit.Image(_hud, "BannerBack", Art.White, new Color(0f, 0f, 0f, 0.7f));
            UiKit.Place(_bannerBack.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 240f));
            _banner = UiKit.OutlinedLabel(_bannerBack.transform, "Banner", "", 52, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(_banner.rectTransform);
        }

        private void RefreshHud()
        {
            TacticsLevel level = TacticsLevels.All[_levelIndex];
            _title.text = "시험판 B · " + level.Name;

            string wind = WindText(_game.Wind);
            string next = _game.NextWind != _game.Wind ? "  (다음 턴 " + WindText(_game.NextWind) + ")" : "";
            _status.text = "턴 " + Mathf.Min(_game.Turn, _game.TurnLimit) + "/" + _game.TurnLimit
                           + "   행동 " + _game.Actions + "/" + TacticsGame.ActionsPerTurn
                           + "   물 " + _game.Tank + "/" + TacticsGame.TankMax
                           + "   체력 " + _game.Hp
                           + "   구조 " + _game.Rescued + "/" + _game.Total
                           + "\n바람 " + wind + next;

            _hint.text = level.Hint;

            string mode = _mode == Mode.Spray ? "[분사] 마우스로 방향 · 클릭 발사 · 회색 X = 이 수로 막는 불"
                        : _mode == Mode.Door ? "[문] 옆 문 클릭"
                        : "[이동] 파란 칸 클릭 · 사람 곁에 서면 스스로 대피";
            _help.text = mode + "      1 분사  2 문  3 급수  Space 턴 넘기기  Z 되돌리기  R 처음부터  Tab 시험판 A";

            _toast.text = _messageAge < 0f ? _message : "";

            bool over = _game.Outcome != TOutcome.Playing;
            _bannerBack.gameObject.SetActive(over);
            if (over)
            {
                _banner.text = _game.Outcome == TOutcome.Won
                    ? "별 " + _game.Stars + "개 · 구조 성공!\n클릭하면 다음 현장"
                    : "실패 · 구조 " + _game.Rescued + "/" + _game.Total + "\n클릭하면 다시";
            }
        }

        private static string WindText(Dir? wind)
        {
            // 주아체에 화살표 글자가 없어 방위로 쓴다.
            if (wind == null) return "없음";
            switch (wind.Value)
            {
                case Dir.N: return "북쪽으로";
                case Dir.E: return "동쪽으로";
                case Dir.S: return "남쪽으로";
                default: return "서쪽으로";
            }
        }
    }
}
