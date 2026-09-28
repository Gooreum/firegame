using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using FireGame.UnityLayer;
using FireGame.UnityLayer.Feel;
using UnityEngine;
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
        private const int MaxParticles = 1100;
        private const int MaxNumbers = 60;
        private const int MaxScorch = 220;

        private static readonly string[] Sparks = { "Effects/spark_01", "Effects/spark_02", "Effects/spark_03", "Effects/spark_04" };
        private static readonly string[] Smokes = { "Effects/smoke_01", "Effects/smoke_02", "Effects/smoke_03", "Effects/smoke_04", "Effects/smoke_05" };
        private static readonly string[] Flames = { "Effects/flame_01", "Effects/flame_02", "Effects/flame_03", "Effects/flame_04", "Effects/flame_05", "Effects/flame_06" };
        private static readonly string[] Faces = { "TopDown/civilian_woman", "TopDown/civilian_old", "TopDown/civilian_man" };

        private readonly Transform _root;
        private readonly Transform _world;
        private readonly Camera _camera;
        private readonly RectTransform _hud;

        private SurvivorSim _sim;
        private readonly bool _maxGear;
        private int _seed = 3;

        /// <summary>지금 스테이지(1부터). 깨면 다음으로 넘어가고, 폰에서도 이어지게 PlayerPrefs에 남긴다.</summary>
        private int _stage;
        public const string StageKey = "firegame.proto.stage";

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
        private float _bossBannerAge = 99f;
        private float _xpPunch;
        private float _shownXp;
        private float _putOutClock;
        private float _gemClock;
        private float _comboClock;
        private int _combo;
        private float _hitStop;
        private float _zoomKick;
        private float _recoil;

        /// <summary>0 = 앞을 보고 걷는 자세, 1 = 옆으로 서서 노즐을 겨눈 자세.</summary>
        private float _stance;
        private float _cardsIn = -1f;
        private float _waveAge = 99f;
        private float _hurtClock;
        private int _comboShown;
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
        private int _groundStage;
        private Image _windArrow;
        private Text _windLabel;
        private Pool _gems;
        private Pool _truck;
        private Pool _siren;
        private Pool _cloud;
        private Pool _rainShade;
        private Pool _band;
        private Pool _plane;
        private static Sprite _planeSprite;
        private bool _truckShown;
        private float _planeAge = 99f;
        private Vector3 _planeFrom;
        private Vector3 _planeTo;
        private Pool _toolbox;
        private Pool _toolboxGlow;
        private int _toolboxesShown;
        private static Sprite _toolboxSprite;
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
        private Pool _nozzle;
        private Pool _reticle;
        private Pool _shadows;
        private Pool _hoseTube;
        private Pool _hoseTubeEdge;
        private Pool _bubbles;
        private Pool _auras;
        private Pool _tank;
        private Pool _radar;
        private Pool _bombs;

        // 노란 특수 장비: 헬기(그림자·몸통·로터), 동료.
        private Pool _heliShadow;
        private Pool _heli;
        private Pool _rotor;
        private Pool _partner;
        private TextMesh _partnerTag;
        private TextMesh _forecastTag;
        private Pool _sprayLines;
        private Pool _turretBase;
        private float _heatTextClock;
        private readonly Vector3[] _partnerLast = new Vector3[4];
        private readonly float[] _partnerDeg = new float[4];
        private Vector3 _heliExitFrom;
        private Vector3 _heliExitDir;
        private float _heliExitAge = 99f;
        private float _frameDt;
        private static Sprite _heliSprite;
        private static Sprite _rotorSprite;
        private AudioSource _splash;
        private Text _xpText;
        private Text _xpTease;
        private Pool _bombShadows;
        private Pool _drones;
        private Pool _droneGlow;
        private Pool _foam;
        private Pool _groundFire;
        private Pool _groundGlow;
        private Pool _civilians;
        private Pool _civilianRings;
        private Pool _houseShadows;
        private Pool _roofEdges;
        private Pool _roofs;
        private Pool _roofTrim;
        private Pool _props;
        private Pool _roofGlow;
        private Pool _roofFire;
        private Pool _bars;
        private readonly List<TextMesh> _signs = new List<TextMesh>();
        private readonly List<TextMesh> _helps = new List<TextMesh>();
        private Pool _edgeArrows;
        private readonly List<Image> _stars = new List<Image>();
        private static Sprite _arrowSprite;

        private SpriteRenderer _player;
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
        private Image _hpFill;
        private Text _hpText;
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
            _hud = UiKit.Node(canvas.transform, "SurvivorHud");
            UiKit.Stretch(_hud);

            BuildPools();
            BuildHud();
            // 조이스틱은 HUD 맨 위에(카드·결과창은 따로 켜고 끈다).
            foreach (Image stick in new[] { _leftRing, _leftKnob, _rightRing, _rightKnob }) stick.transform.SetAsLastSibling();
            BuildAudio();
            Restart(_seed);
        }

        public SurvivorSim Sim
        {
            get { return _sim; }
        }

        public void Restart(int seed)
        {
            _seed = seed;
            _sim = new SurvivorSim(seed, _stage);
            if (_maxGear) _sim.GiveMaxGear();
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
            _comboShown = 0;
            _toolboxesShown = 0;
            _chestsShown = 0;
            _planeAge = 99f;
            _truckShown = false;
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
            BuildSigns();
            HideCards();
            _resultBack.gameObject.SetActive(false);
            // 판 시작: "STAGE 2 · 산불 숲" 띠가 파랗게 지나간다.
            _bossBandText.text = "STAGE " + _sim.Stage.Number + " · " + _sim.Stage.Name;
            _bandTint = new Color(0.05f, 0.25f, 0.6f);
            _bossBannerAge = 0f;
            Refresh(0f);
        }

        /// <summary>저장해 둔 스테이지(없으면 1).</summary>
        public static int SavedStage()
        {
            int n = PlayerPrefs.GetInt(StageKey, 1);
            return n >= 1 && n <= SurvivorStages.Count ? n : 1;
        }

        /// <summary>스테이지를 바꾸고 새 판을 연다.</summary>
        private void GoToStage(int stage)
        {
            _stage = stage;
            PlayerPrefs.SetInt(StageKey, stage);
            PlayerPrefs.Save();
            Restart(_seed + 1);
        }

        public void Destroy()
        {
            GameAudio.SetFireLevel(0f, 1f);
            UiKit.Discard(_root.gameObject);
            UiKit.Discard(_hud.gameObject);
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

            if (_sim.Outcome != SOutcome.Playing)
            {
                if (_overAge > 1f && input.MouseClicked)
                {
                    // 이기면 다음 스테이지, 지면 같은 스테이지를 다시.
                    if (_sim.Outcome == SOutcome.Won) GoToStage(SurvivorStages.Next(_stage));
                    else Restart(_seed + 1);
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
                    Vector3 screen = new Vector3(input.Mouse.x, input.Mouse.y, 10f);
                    _mouseAt = _world.InverseTransformPoint(_camera.ScreenToWorldPoint(screen));
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
                Flash(gold, 0.7f);
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
                Flash(gold, 0.35f);
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
            Flash(gold, 0.45f);
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
            foreach (Hit h in _sim.Hits)
            {
                Vector3 at = W(h.Pos);
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

            foreach (Vec2 e in _sim.HeliDrops)
            {
                Vector3 at = W(e);
                WaterBlast(at, SurvivorSim.HeliRadius, Loadout.MaxLevel);
                Shockwave(at, new Color(0.9f, 0.97f, 1f, 0.9f), SurvivorSim.HeliRadius * 3f, 0.5f, 0.08f);
                _trauma = Mathf.Min(1f, _trauma + 0.35f);
                HitStop(0.04f);
                PlaySplash();
                // 헬기는 쏟고 나서 같은 방향으로 계속 날아가 화면 밖으로 빠진다.
                _heliExitFrom = at + HeliLift;
                _heliExitDir = (new Vector3(1.4f, -1f, 0f)).normalized;
                _heliExitAge = 0f;
            }
            foreach (Structure st in _sim.Sprinkled)
            {
                // 스프링클러: 지붕에서 물 고리가 터지고 물방울이 사방으로 흩날린다.
                Vector3 at = W(st.Pos) + new Vector3(0f, 0.4f, 0f);
                Shockwave(at, new Color(0.6f, 0.9f, 1f, 0.95f), 6f, 0.45f);
                Splash(at, 16, 1.4f);
                Steam(at, 4, 1.4f);
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI * 2f / 12f;
                    EmitFalling("Effects/water_drop", at, new Vector3(Mathf.Cos(a) * 3.5f, 4f + Mathf.Sin(a), 0f), 0.6f, 0.3f, new Color(0.75f, 0.95f, 1f, 1f));
                }
            }
            if (_sim.Sprinkled.Count > 0) GameAudio.Play(Cue.SprayFoam);
            if (_sim.JustRain)
            {
                // 먹구름이 오면 번개가 한 번 번쩍한다.
                Flash(new Color(0.9f, 0.95f, 1f), 0.3f);
                _trauma = Mathf.Min(1f, _trauma + 0.2f);
                GameAudio.Play(Cue.Backfire);
                if (_sim.RainAt.HasValue) SpawnText(W(_sim.RainAt.Value) + new Vector3(0f, 2.5f, 0f), "비구름!", new Color(0.7f, 0.85f, 1f), 1.4f);
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
                ShowAlert("방염제 살포!", new Color(1f, 0.45f, 0.45f));
                GameAudio.Play(Cue.SprayFoam);
            }
            if (_sim.JustCurtain)
            {
                Vector3 at = W(_sim.Player);
                float ring = _sim.CurtainRadiusNow * 2.4f;
                Shockwave(at, new Color(0.45f, 0.8f, 1f, 1f), ring, 0.4f);
                Shockwave(at, new Color(0.85f, 0.97f, 1f, 0.9f), ring * 0.8f, 0.35f, 0.1f);
                for (int i = 0; i < 36; i++)
                {
                    float a = i * Mathf.PI * 2f / 36f;
                    var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    Emit("Effects/water_drop", at + (dir * 0.8f), dir * Random.Range(9f, 13f), 3.5f, 0.4f, 0.45f, 0.1f,
                        new Color(0.75f, 0.95f, 1f, 1f), new Color(0.6f, 0.9f, 1f, 0f), 0f);
                }
                _trauma = Mathf.Min(1f, _trauma + 0.15f);
                GameAudio.Play(Cue.SprayFoam);
            }

            foreach (Vec2 e in _sim.Explosions)
            {
                WaterBlast(W(e), _sim.Build.BombRadius, _sim.Build.PowerOf(UpgradeId.WaterBomb));
                _trauma = Mathf.Min(1f, _trauma + 0.12f);
                HitStop(0.02f);
                GameAudio.Play(Cue.SprayFoam);
            }

            if (_sim.ShotsFired > 0)
            {
                Muzzles();
                // 호스 소리는 쥔 동안 도는 루프(UpdateSpraySound) 하나뿐이다. 발사마다 소리를 덧대면 "픽픽" 쏘는 소리가 된다.
            }

            if (_sim.GemsCollected > 0)
            {
                _xpPunch = 1f;
                _combo = _comboClock > 0f ? _combo + _sim.GemsCollected : 0;
                _comboClock = 0.5f;
                if (_gemClock <= 0f)
                {
                    PlayChime(1f + Mathf.Min(1f, _combo * 0.04f));
                    _gemClock = 0.04f;
                    Emit("Effects/glow", W(_sim.Player), Vector3.zero, 0f, 0.15f, 0.8f, 1.8f, new Color(0.4f, 0.8f, 1f, 0.6f), new Color(0.4f, 0.8f, 1f, 0f), 0f, true);
                }
                if (_combo < _comboShown) _comboShown = 0;
                if (_combo >= _comboShown + 10)
                {
                    _comboShown = _combo - (_combo % 10);
                    SpawnText(W(_sim.Player) + new Vector3(0.9f, 0.9f, 0f), "×" + _comboShown, new Color(0.5f, 0.85f, 1f), 1.2f);
                }
            }

            if (_sim.JustLeveled)
            {
                // 빛기둥 + 고리 + 위로 솟는 반짝이. 카드는 0.3초 뒤에 떠서 터지는 걸 먼저 보여 준다.
                Vector3 at = W(_sim.Player);
                Flash(Color.white, 0.45f);
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
                // 대형 신고: 그 건물에서 붉은 고리 + 불똥, 붉은 띠 "대형 화재! 3명 갇힘".
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
                // 대화재: 랜드마크가 확 타오르고, 붉은 고리 세 겹 + 검은 연기, 카메라가 물러난다.
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
                _bossBandText.text = "대화재! 끝까지 지켜라";
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
                Flash(gold, 0.4f);
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
                // 구급차: 흰·빨강 번쩍, 연기가 걷힌다.
                Vector3 at = W(_sim.AmbulanceAt.Door);
                Shockwave(at, new Color(1f, 1f, 1f, 0.9f), 5f, 0.4f);
                Shockwave(at, new Color(1f, 0.25f, 0.25f, 0.9f), 3.5f, 0.35f, 0.1f);
                Sparkle(at, 14, Color.white);
                Steam(W(_sim.AmbulanceAt.Pos), 8, 1.2f);
                SpawnText(at + new Vector3(0f, 1.8f, 0f), "구급차! 연기 걷힘", new Color(1f, 0.9f, 0.9f), 1.5f);
                GameAudio.Play(Cue.PickUp);
            }
            _heatTextClock -= SurvivorSim.Dt;
            if (_sim.HeatHurt > 0f && _heatTextClock <= 0f)
            {
                // 열기: 불 곁에 서 있으면 "뜨거워!"가 1.2초마다 뜬다(방화복이면 덜 붉다).
                _heatTextClock = 1.2f;
                bool suit = _sim.Build.Level(UpgradeId.Suit) > 0;
                SpawnText(W(_sim.Player) + new Vector3(0f, 1.3f, 0f), suit ? "방화복이 막는다" : "뜨거워!", suit ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.4f, 0.2f), 1f);
            }
            if (_sim.JustWindShift) ShowAlert("바람이 " + WindName(_sim.Wind) + "쪽으로!", new Color(0.8f, 0.9f, 1f));
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

            if (_sim.JustRescued)
            {
                var green = new Color(0.5f, 1f, 0.5f);
                Pillar(W(_sim.Player), green);
                Shockwave(W(_sim.Player), green, 7f, 0.45f);
                Sparkle(W(_sim.Player), 16, green);
                Burst(W(_sim.Player), 14, green, 5f);
                SpawnText(W(_sim.Player) + new Vector3(0f, 0.8f, 0f), "구조! +20", new Color(0.5f, 1f, 0.5f), 1.4f);
                GameAudio.Play(Cue.PickUp);
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
                    // 4:00까지 지켜 냈다: 번쩍 → (잠깐 뒤) 고리 세 겹 + 거대한 김 + 불똥 비.
                    Vector3 at = W(_sim.Player);
                    Flash(Color.white, 0.9f);
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

            foreach (Structure st in _sim.Ignited)
            {
                Vector3 at = W(st.Pos);
                if (st.IsBuilding)
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
            _trauma = Mathf.Max(0f, _trauma - (dt * 1.8f));
            _flash = Mathf.Max(0f, _flash - (dt * 2.2f));
            _hurt = Mathf.Max(0f, _hurt - (dt * 1.5f));
            _slowmo = Mathf.Max(0f, _slowmo - dt);
            _xpPunch = Mathf.Max(0f, _xpPunch - (dt * 5f));
            _zoomKick *= Mathf.Exp(-6f * dt);
            // 쥔 동안은 물살을 버티느라 살짝 뒤로 기댄 자세를 유지한다(쏠 때마다 튕기지 않는다).
            _recoil = Mathf.MoveTowards(_recoil, _sim.Spraying ? 0.35f : 0f, dt * 4f);
            _stance = Mathf.MoveTowards(_stance, _sim.Spraying ? 1f : 0f, dt * 6f);
            _hurtClock -= dt;
            _waveAge += dt;
            if (_cardsIn > 0f)
            {
                _cardsIn -= dt;
                if (_cardsIn <= 0f && _sim.PendingChoices != null) ShowCards();
            }
            _putOutClock -= dt;
            _igniteClock -= dt;
            _sizzleClock -= dt;
            _gemClock -= dt;
            _comboClock -= dt;
            _cardAge += dt;
            _alertAge += dt;
            _bossBannerAge += dt;
            if (_sim.Outcome != SOutcome.Playing) _overAge += dt;

            foreach (Pool p in _pools) p.Begin();
            foreach (RibbonPool r in _ribbons) r.Begin(_time);
            DrawTown();
            DrawPuddles();
            DrawGems();
            DrawToolboxes();
            DrawChests();
            DrawCivilians();
            DrawEnemies();
            DrawShots();
            DrawPlayer();
            DrawGear(dt);
            DrawSpecials(dt);
            DrawEdgeArrows();
            foreach (Pool p in _pools) p.End();
            foreach (RibbonPool r in _ribbons) r.End();

            UpdateSpraySound(dt);
            AdvanceParticles(dt);
            AdvanceNumbers(dt);
            RefreshHud(dt);
            FollowCamera(dt);

            float loud = _sim.Outcome == SOutcome.Playing ? 1f - Mathf.Exp(-_sim.Enemies.Count / 60f) : 0f;
            GameAudio.SetFireLevel(loud, Mathf.Max(dt, 0.001f));
        }

        private static Vector3 W(Vec2 p)
        {
            return new Vector3(p.X, p.Y, 0f);
        }

        private void DrawEnemies()
        {
            for (int i = 0; i < _sim.Enemies.Count; i++)
            {
                Enemy e = _sim.Enemies[i];
                Vector3 at = W(e.Pos);
                float flicker = 1f + (0.09f * Mathf.Sin((_time * 14f) + (i * 1.7f)));
                bool hit = e.HitFlash > 0f;
                float foot = e.Kind == EnemyKind.Blaze ? 1.8f : 1f;
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
                        _embers.Put(at + new Vector3(0f, 0.15f, 0f), 1.9f * flicker * punch, 0f, hit ? water : new Color(1f, 0.45f, 0.1f));
                        if (!hit) _enemyCore.Put(at + new Vector3(0f, 0.08f, 0f), 1.1f * flicker * life, 0f, new Color(1f, 0.8f, 0.35f, 0.9f));
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
                        _blazes.Put(at + new Vector3(0f, 0.25f, 0f), 3.1f * flicker * punch, 0f, hit ? water : new Color(0.95f, 0.28f, 0.06f));
                        if (!hit) _enemyCore.Put(at + new Vector3(0f, 0.12f, 0f), 1.8f * flicker * life, 0f, new Color(1f, 0.7f, 0.25f, 0.9f));
                        break;
                    case EnemyKind.Squirrel:
                    {
                        // 불다람쥐: 작은 불 몸통 + 뒤로 길게 흔들리는 불꼬리.
                        Vec2 goal = e.Goal != null ? e.Goal.Pos : _sim.Player;
                        float head = Mathf.Atan2(goal.Y - e.Pos.Y, goal.X - e.Pos.X);
                        var back = new Vector3(-Mathf.Cos(head), -Mathf.Sin(head), 0f);
                        var side = new Vector3(-back.y, back.x, 0f);
                        float wag = Mathf.Sin((_time * 14f) + i) * 0.25f;
                        _enemyGlow.Put(at, 1.5f * life, 0f, new Color(1f, 0.45f, 0.1f, 0.35f));
                        _darts.Put(at + (back * 0.55f) + (side * wag), 1.2f * flicker * punch, (Mathf.Atan2(back.y + (side.y * wag), back.x + (side.x * wag)) * Mathf.Rad2Deg) - 90f,
                            hit ? water : new Color(1f, 0.55f, 0.15f));
                        _embers.Put(at + new Vector3(0f, 0.05f, 0f), 1.15f * flicker * punch, 0f, hit ? water : new Color(0.95f, 0.4f, 0.1f));
                        if (!hit) _enemyCore.Put(at + (new Vector3(Mathf.Cos(head), Mathf.Sin(head), 0f) * 0.15f), 0.6f * life, 0f, new Color(1f, 0.85f, 0.4f, 0.9f));
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
                    case EnemyKind.Dart:
                        float toward = Mathf.Atan2(_sim.Player.Y - e.Pos.Y, _sim.Player.X - e.Pos.X) * Mathf.Rad2Deg;
                        _enemyGlow.Put(at, 1.5f, 0f, new Color(1f, 0.8f, 0.2f, 0.45f));
                        _darts.Put(at, 0.9f * flicker * punch, toward + 90f, hit ? water : new Color(1f, 0.9f, 0.35f));
                        break;
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
        private void DrawSpecials(float dt)
        {
            if ((_sim.Wind.X != 0f || _sim.Wind.Y != 0f) && _sim.Outcome == SOutcome.Playing && Random.value < 0.6f)
            {
                // 산불 숲: 화면 곳곳에서 재와 불티가 바람 쪽으로 날린다.
                float halfH = _camera.orthographicSize;
                float halfW = halfH * _camera.aspect;
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
                if (!_truckShown) GameAudio.Play(Cue.Critical);
                _shadows.Put(at + new Vector3(0f, -0.45f, 0f), 5f, 0f, new Color(0f, 0f, 0f, 0.4f), null, 0.4f);
                _truck.Put(at, 2.3f, dir > 0f ? -90f : 90f, new Color(1f, 0.55f, 0.5f));
                // 사이렌: 지붕 앞뒤가 빨강·파랑으로 번갈아 번쩍인다.
                bool flip = Mathf.Repeat(_time * 6f, 1f) < 0.5f;
                _siren.Put(at + new Vector3(dir * 0.7f, 0.15f, 0f), 2.2f, 0f, flip ? new Color(1f, 0.15f, 0.1f, 0.95f) : new Color(0.2f, 0.4f, 1f, 0.95f));
                _siren.Put(at + new Vector3(-dir * 0.1f, 0.15f, 0f), 1.7f, 0f, flip ? new Color(0.2f, 0.4f, 1f, 0.85f) : new Color(1f, 0.15f, 0.1f, 0.85f));
                // 양옆 물대포: 위아래로 물줄기가 뻗는다.
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int k = 0; k < 2; k++)
                    {
                        var v = new Vector3((-dir * Random.Range(0.5f, 2f)) + Random.Range(-1f, 1f), side * Random.Range(7f, 10f), 0f);
                        Emit("Effects/water_drop", at + new Vector3(0f, side * 0.4f, 0f), v, 3f, 0.3f, 0.4f, 0.12f, new Color(0.75f, 0.95f, 1f, 1f), new Color(0.6f, 0.9f, 1f, 0f), 0f);
                    }
                }
                if (Random.value < 0.4f) Splash(at - new Vector3(dir * 1.4f, 0f, 0f), 1, 0.4f);
            }
            _truckShown = _sim.Truck.HasValue;

            if (_sim.RainAt.HasValue)
            {
                Vector3 at = W(_sim.RainAt.Value);
                float r = SurvivorSim.RainRadius;
                float fade = Mathf.Clamp01(_sim.RainLeft / 0.4f) * Mathf.Clamp01((SurvivorSim.RainTime - _sim.RainLeft) / 0.3f);
                _rainShade.Put(at, r * 2.6f, 0f, new Color(0.05f, 0.1f, 0.2f, 0.45f * fade));
                // 먹구름: 연기 덩어리 여섯이 천천히 꿈틀거린다(위로 조금 떠 있다).
                for (int k = 0; k < 6; k++)
                {
                    float a = (k * 1.05f) + (_time * 0.3f);
                    Vector3 o = new Vector3(Mathf.Cos(a) * r * 0.45f, (Mathf.Sin(a) * r * 0.2f) + 3.2f, 0f);
                    _cloud.Put(at + o, r * 1.1f, (k * 60f) + (_time * 8f), new Color(0.3f, 0.33f, 0.4f, 0.85f * fade));
                }
                // 빗줄기: 구름에서 비스듬히 떨어지는 짧은 선.
                for (int k = 0; k < 6; k++)
                {
                    var p = at + new Vector3(Random.Range(-r, r), Random.Range(-r * 0.6f, r * 0.6f) + 3f, 0f);
                    EmitSprite(BeamSprite(), p, new Vector3(-2f, -16f, 0f), 0f, 0.22f, 0.08f, 0.08f, new Color(0.75f, 0.88f, 1f, 0.8f), new Color(0.75f, 0.88f, 1f, 0f), 0f, true, 0f, 8f, 7f);
                }
                if (Random.value < 0.5f) Splash(at + new Vector3(Random.Range(-r, r) * 0.8f, Random.Range(-r, r) * 0.6f, 0f), 2, 0.3f);
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
                _band.Put((start + end) * 0.5f, SurvivorSim.RetardantWidth, deg - 90f, new Color(0.9f, 0.25f, 0.3f, 0.35f * (0.3f + (0.7f * t))), null, len * laid / SurvivorSim.RetardantWidth);
                _band.Put((start + end) * 0.5f, SurvivorSim.RetardantWidth * 0.6f, deg - 90f, new Color(1f, 0.4f, 0.45f, 0.25f * (0.3f + (0.7f * t))), null, len * laid / (SurvivorSim.RetardantWidth * 0.6f));
            }
            if (_planeAge < 1.2f)
            {
                _planeAge += dt;
                float f = Mathf.Clamp01(_planeAge / 1.2f);
                Vector3 at = Vector3.Lerp(_planeFrom, _planeTo, f);
                Vector3 dir = (_planeTo - _planeFrom).normalized;
                float deg = (Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg) - 90f;
                _shadows.Put(at + new Vector3(0.8f, -1.2f, 0f), 3.2f, deg, new Color(0f, 0f, 0f, 0.3f), null, 0.6f);
                _plane.Put(at + new Vector3(0f, 2.2f, 0f), 3.4f, deg, Color.white);
                if (Random.value < 0.8f)
                {
                    Emit("Effects/glow", at + new Vector3(0f, 1.6f, 0f), new Vector3(Random.Range(-0.5f, 0.5f), -3f, 0f), 2f, 0.5f, 0.8f, 1.6f,
                        new Color(1f, 0.35f, 0.4f, 0.7f), new Color(1f, 0.35f, 0.4f, 0f), 0f);
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
                _civilians.Put(at + new Vector3(0f, 0.12f * Mathf.Abs(Mathf.Sin(_time * 16f)), 0f), 0.85f, 0f, new Color(1f, 1f, 1f, a), Art.Get(Faces[i % Faces.Length]));
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
                _groundGlow.Put(at, p.Radius * 3.4f * warn, 0f, new Color(1f, 0.3f, 0.05f, 0.6f * t));
                for (int k = 0; k < 3; k++)
                {
                    float a = (k * 2.1f) + i;
                    Vector3 o = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.4f;
                    float f = 0.6f + (0.15f * Mathf.Sin((_time * 16f) + (k * 2f) + i));
                    _groundFire.Put(at + (o * big), f * (0.6f + (0.5f * t)) * big, 0f, new Color(1f, 0.55f, 0.12f, t), Art.Get(k == 0 ? "Effects/fire_01" : "Effects/fire_02"));
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
                    case ShotKind.Bomb:
                        float t = Mathf.Clamp01(s.Age / s.Life);
                        float lift = Mathf.Sin(t * Mathf.PI) * 2f;
                        float bombSize = 0.8f + (0.08f * bombLevel);
                        _bombShadows.Put(at, 0.6f * bombSize, 0f, new Color(0f, 0f, 0f, 0.35f));
                        _bombs.Put(at + new Vector3(0f, lift, 0f), bombSize, t * 540f, new Color(0.45f, 0.78f, 1f));
                        _dropGlow.Put(at + new Vector3(0f, lift, 0f), bombSize * (1.2f + (0.15f * bombLevel)), 0f, new Color(0.55f, 0.8f, 1f, 0.25f + (0.04f * bombLevel)));
                        // 날아가며 물방울 꼬리를 흘린다.
                        if (Random.value < 0.4f)
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
                _drones.Put(at, 0.65f * droneScale, _time * 720f, Color.white);
                if (goldDrone) _auras.Put(at, 1.1f, 0f, new Color(1f, 0.85f, 0.35f, 0.85f));
                if (Random.value < 0.25f) Splash(at, 1, 0.15f);
                // 불난 지붕 위: 드론마다 지붕 가운데로 물줄기를 뿌린다.
                if (patrol != null && patrol.Burning && Vector3.Distance(center, W(patrol.Pos)) < 0.6f)
                {
                    SprayLine(at, Vector3.Lerp(at, center, 0.75f), 0.18f, new Color(0.6f, 0.9f, 1f, 0.8f));
                    if (Random.value < 0.2f) Splash(Vector3.Lerp(at, center, 0.75f), 1, 0.2f);
                }
            }
            // 구조 드론: 갇힌 사람이 있는 지붕 위에서 노란 구조 줄을 내려 끌어올린다.
            if (patrol != null && _sim.Build.Level(UpgradeId.RescueDrone) > 0 && patrol.DroneRescue > 0f)
            {
                float lift = Mathf.Clamp01(patrol.DroneRescue / SurvivorSim.DroneRescueTime);
                Vector3 top = center + new Vector3(0f, 2.2f, 0f);
                SprayLine(top, center, 0.08f, new Color(1f, 0.85f, 0.3f, 0.95f));
                _civilians.Put(Vector3.Lerp(center, top, lift), 0.6f, 10f * Mathf.Sin(_time * 8f), Color.white, Art.Get(Faces[0]));
            }

            DrawTurrets();
            DrawForecast();
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

        /// <summary>방수 포탑: 삼각대 위 노즐이 쏘는 곳을 향한다. 현장 구조소는 초록 영역과 흰 천막 십자.</summary>
        private void DrawTurrets()
        {
            bool post = _sim.Build.Level(UpgradeId.RescuePost) > 0;
            foreach (Turret tu in _sim.Turrets)
            {
                Vector3 at = W(tu.Pos);
                float life = Mathf.Clamp01(tu.Life / tu.MaxLife);
                float blink = tu.Life < 1.5f && Mathf.Sin(_time * 18f) < 0f ? 0.4f : 1f;
                if (post)
                {
                    _civilianRings.Put(at, SurvivorSim.PostRange * 2f, 0f, new Color(0.4f, 1f, 0.5f, 0.18f + (0.06f * Mathf.Sin(_time * 3f))));
                    _sprayLines.Put(at + new Vector3(0f, 0.9f, 0f), 1.4f, 0f, new Color(1f, 1f, 1f, 0.95f * blink), null, 0.9f / 1.4f);
                    _sprayLines.Put(at + new Vector3(0f, 0.9f, 0f), 0.9f, 0f, new Color(0.9f, 0.15f, 0.15f, blink), null, 0.25f / 0.9f);
                    _sprayLines.Put(at + new Vector3(0f, 0.9f, 0f), 0.25f, 0f, new Color(0.9f, 0.15f, 0.15f, blink), null, 0.9f / 0.25f);
                }
                _shadows.Put(at + new Vector3(0f, -0.2f, 0f), 0.9f, 0f, new Color(0f, 0f, 0f, 0.35f), null, 0.5f);
                // 남은 시간 고리(파랑이 줄어든다).
                _civilianRings.Put(at, 1.6f * life + 0.4f, 0f, new Color(0.4f, 0.8f, 1f, 0.35f * blink));
                Vector3 aim = tu.Aim.HasValue ? W(tu.Aim.Value) : at + new Vector3(1f, 0f, 0f);
                Vector3 dir = (aim - at).normalized;
                _turretBase.Put(at, 0.75f, 0f, new Color(0.85f, 0.2f, 0.15f, blink));
                _sprayLines.Put(at + (dir * 0.35f), 0.7f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, new Color(0.75f, 0.75f, 0.8f, blink), null, 0.18f / 0.7f);
                if (tu.Aim.HasValue && tu.Clock > 0.2f)
                {
                    SprayLine(at + (dir * 0.7f), aim, 0.2f, new Color(0.6f, 0.9f, 1f, 0.85f));
                    if (Random.value < 0.3f) Splash(aim, 1, 0.2f);
                }
            }
        }

        /// <summary>무전기: 곧 불날 건물 위에 주황 과녁과 "신고 예고 N"이 깜빡인다.</summary>
        private void DrawForecast()
        {
            Structure st = _sim.ForecastAt;
            bool show = st != null && !st.Burning && _sim.Outcome == SOutcome.Playing;
            if (_forecastTag != null) _forecastTag.gameObject.SetActive(show);
            if (!show) return;
            Vector3 at = W(st.Pos);
            float beat = 0.5f + (0.5f * Mathf.Abs(Mathf.Sin(_time * 6f)));
            _civilianRings.Put(at, Mathf.Max(st.Half.X, st.Half.Y) * 2.8f * (1f + (0.1f * beat)), 0f, new Color(1f, 0.6f, 0.1f, 0.3f + (0.3f * beat)));
            _forecastTag.text = "신고 예고 " + Mathf.CeilToInt(_sim.ForecastIn);
            _forecastTag.color = Color.Lerp(new Color(1f, 0.75f, 0.2f), Color.white, beat * 0.4f);
            _forecastTag.transform.localPosition = at + new Vector3(0f, st.Half.Y + 1.1f, -0.2f);
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
            _ribbonIn.Clear();
            foreach (Vector3 p in pts) _ribbonIn.Add(new Vec2(p.x, p.y));
            // 방수포 제트는 고압이라 덜 출렁인다.
            WaterRibbon.Build(_ribbonIn, w, _time, jet ? 0.6f : 1f, _ribbon, _blobs);
            if (_ribbon.Count < 2) return;

            _waterSheath.Put(_ribbon, 1.35f, 0f, new Color(0.5f, 0.78f, 1f, 0.35f), 5f);
            _waterBody.Put(_ribbon, 1f, 0f, new Color(0.55f, 0.8f, 1f, jet ? 0.8f : 0.92f), 8f);
            // 빛은 왼쪽 위에서 온다: 하이라이트를 진행 방향 왼쪽으로 치우친다.
            _waterShine.Put(_ribbon, 0.3f, 0.35f, new Color(0.9f, 0.97f, 1f, 0.55f), 14f);
            if (!jet && _sim.Build.Level(UpgradeId.Hose) >= Loadout.MaxLevel)
            {
                // 물대포 MAX: 줄기 한가운데 금빛-흰 심지가 흐르고 끝에서 금빛 반짝이가 튄다.
                _waterShine.Put(_ribbon, 0.16f, 0f, new Color(1f, 0.88f, 0.45f, 0.75f), 22f);
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

            Vector3 end = pts[pts.Count - 1];
            Vector3 endDir = (end - pts[pts.Count - 2]).normalized;
            EmitSpray(end, endDir, jet ? 1.6f : Mathf.Max(1f, last.Radius / 0.3f * 0.75f));
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

            // 노즐 뒤 이음쇠에서 나와 뒷손을 지나 몸 뒤로 끌린다.
            var points = new List<Vector3>(14) { Hand() - (look * 0.2f), Fist(RightFist), at };
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
                _hoseTubeEdge.Put(mid, 0.3f, deg, new Color(0.35f, 0.25f, 0.15f, fade), null, (span + 0.1f) / 0.3f);
                _hoseTube.Put(mid, 0.2f, deg, new Color(0.9f, 0.8f, 0.6f, fade), null, (span + 0.1f) / 0.2f);
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
        private Vector3 Hand() => Vector3.Lerp(Fist(RightFist), Fist(LeftFist), _stance);

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

        private void DrawPlayer()
        {
            Vector3 at = W(_sim.Player);
            Vector3 look = Look();
            Vector3 kick = look * (-0.1f * _recoil);
            float lookDeg = Mathf.Atan2(look.y, look.x) * Mathf.Rad2Deg;
            _player.transform.localPosition = at + kick + new Vector3(0f, 0f, -0.01f);
            _shadows.Put(at + new Vector3(0.05f, -0.15f, 0f), 1f, 0f, new Color(0f, 0f, 0f, 0.4f), null, 0.55f);
            // 몸을 조준에서 조금 오른쪽으로 틀어, 오른쪽 옆구리에 노즐을 끼고 버티는 자세.
            _player.transform.localRotation = Quaternion.Euler(0f, 0f, lookDeg + BodyTurn);
            DrawNozzle(look, lookDeg);
            DrawHoseLine(at, look);
            DrawReticle(at);
            // 방화복 레벨만큼 옷을 갈아입는다: 파랑 → 노란 헬멧 → 빨간 헬멧 → 빨간 방화복 → 은색 방열복.
            int suit = _sim.Build.Level(UpgradeId.Suit);
            int outfit = Mathf.Min(suit, 4);
            if (outfit != _suitShown)
            {
                _player.sprite = Art.Get("TopDown/player_suit_" + outfit);
                _player.transform.localScale = Vector3.one * Art.FitWidth(_player.sprite, 0.95f);
                if (_suitShown >= 0 && outfit > _suitShown) SuitUp(at);
                _suitShown = outfit;
            }
            // 최대 레벨이면 은색 방열복이 금빛으로 일렁인다.
            Color baseColor = suit >= Loadout.MaxLevel ? Color.Lerp(Color.white, new Color(1f, 0.85f, 0.4f), 0.25f + (0.15f * Mathf.Sin(_time * 4f))) : Color.white;
            _player.color = Color.Lerp(baseColor, new Color(1f, 0.35f, 0.3f), Mathf.Clamp01(_hurt * 2f));
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

            int radio = build.Level(UpgradeId.Radio);
            if (radio > 0)
            {
                float phase = Mathf.Repeat(_time / 1.6f, 1f);
                float width = _sim.Magnet * 2f / 0.85f * Mathf.Lerp(0.2f, 1f, phase);
                _auras.Put(at, width, 0f, new Color(0.45f, 1f, 0.55f, (1f - phase) * (0.15f + (0.07f * radio))));
                if (radio >= Loadout.MaxLevel)
                {
                    // 무전기 MAX: 끌어오는 범위를 레이더 빛살이 빙빙 훑는다.
                    float sweep = _time * 180f;
                    for (int k = 0; k < 4; k++)
                    {
                        float deg = sweep - (k * 6f);
                        Vector3 dir = new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad), 0f);
                        _radar.Put(at + (dir * _sim.Magnet * 0.5f), 0.12f, deg - 90f, new Color(0.45f, 1f, 0.55f, 0.45f - (k * 0.1f)), null, _sim.Magnet / 0.12f);
                    }
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
            // 목표 표시: 떨어질 자리가 옅은 파란 원으로 조여 든다.
            _reticle.Put(to, Mathf.Lerp(SurvivorSim.HeliRadius * 3f, SurvivorSim.HeliRadius * 2f, t), 0f, new Color(0.5f, 0.85f, 1f, 0.35f + (0.4f * t)));
            if (t > 0.6f)
            {
                // 쏟기 직전: 헬기 배에서 물이 쏟아진다.
                for (int i = 0; i < 3; i++)
                {
                    Vector3 belly = ground + HeliLift + new Vector3(Random.Range(-0.6f, 0.6f), -0.3f, 0f);
                    Emit("Effects/water_drop", belly, new Vector3(Random.Range(-1f, 1f), -Random.Range(6f, 9f), 0f), 0f, 0.3f,
                        0.5f, 0.3f, new Color(0.75f, 0.93f, 1f, 0.95f), new Color(0.6f, 0.9f, 1f, 0.2f), 0f);
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
            _heli.Put(ground + HeliLift, 3.2f, deg, new Color(1f, 1f, 1f, alpha));
            _rotor.Put(ground + HeliLift + (dir * 0.25f), 4.4f, _time * 1400f, new Color(1f, 1f, 1f, 0.55f * alpha));
            _rotor.Put(ground + HeliLift + (dir * 0.25f), 4.4f, (_time * 1400f) + 45f, new Color(1f, 1f, 1f, 0.3f * alpha));
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
                    SprayLine(hand, Vector3.Lerp(hand, roof, 0.8f), squad ? 0.26f : 0.18f, new Color(0.6f, 0.9f, 1f, 0.85f));
                    if (Random.value < 0.2f) Splash(Vector3.Lerp(hand, roof, 0.8f), 1, 0.2f);
                }
                if (squad) _auras.Put(at, 1.3f, 0f, new Color(1f, 0.85f, 0.35f, 0.5f));
                _partner.Put(at + new Vector3(0f, bob, 0f), 0.85f, _partnerDeg[i], squad ? new Color(1f, 0.9f, 0.5f) : Color.white);
                if (i == 0 && _partnerTag != null) _partnerTag.transform.localPosition = at + new Vector3(0f, 0.85f, -0.2f);
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

        /// <summary>소방 항공기(위에서 본 모습): 흰 동체, 빨간 날개와 꼬리.</summary>
        private static Sprite PlaneSprite()
        {
            if (_planeSprite != null) return _planeSprite;
            const int n = 96;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            var red = new Color32(210, 45, 40, 255);
            var white = new Color32(240, 240, 240, 255);
            var glass = new Color32(120, 200, 245, 255);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n) - 0.5f;
                    float v = ((y + 0.5f) / n) - 0.5f;
                    Color32 c = new Color32(0, 0, 0, 0);
                    bool body = ((u * u) / (0.07f * 0.07f)) + ((v * v) / (0.46f * 0.46f)) <= 1f;
                    bool wing = Mathf.Abs(v - 0.05f) < 0.07f - (Mathf.Abs(u) * 0.08f) && Mathf.Abs(u) < 0.47f;
                    bool tail = Mathf.Abs(v + 0.38f) < 0.04f && Mathf.Abs(u) < 0.17f;
                    if (wing || tail) c = red;
                    if (body) c = v > 0.3f ? glass : white;
                    pixels[(y * n) + x] = c;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _planeSprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return _planeSprite;
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

        /// <summary>돌아가는 로터: 가는 날개 두 개(십자)와 옅은 원판.</summary>
        private static Sprite RotorSprite()
        {
            if (_rotorSprite != null) return _rotorSprite;
            const int n = 96;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n) - 0.5f;
                    float v = ((y + 0.5f) / n) - 0.5f;
                    float r = Mathf.Sqrt((u * u) + (v * v));
                    byte a = 0;
                    if (r < 0.49f) a = 40;
                    if (r < 0.49f && (Mathf.Abs(u) < 0.025f || Mathf.Abs(v) < 0.025f)) a = 230;
                    if (r < 0.05f) a = 255;
                    byte g = (byte)(r < 0.05f ? 60 : 50);
                    pixels[(y * n) + x] = new Color32(g, g, g, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _rotorSprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return _rotorSprite;
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
            int column = level >= Loadout.MaxLevel ? 34 : 16;
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

        private void Flash(Color color, float strength)
        {
            _flashColor = color;
            _flash = Mathf.Max(_flash, strength);
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
        }

        // ------------------------------------------------------------------
        // 카메라·소리
        // ------------------------------------------------------------------

        private void FollowCamera(float dt)
        {
            if (_camera == null) return;
            // 줌 킥: +면 확 다가오고(레벨업·진화), −면 물러난다(보스 등장).
            _camera.orthographicSize = CameraSize * (1f - (0.12f * Mathf.Clamp(_zoomKick, -1.5f, 1.5f)));
            _camera.backgroundColor = new Color(0.05f, 0.05f, 0.07f);

            var target = new Vector3(_sim.Player.X, _sim.Player.Y, -10f);
            _cameraAt = dt <= 0f ? target : Vector3.Lerp(_cameraAt, target, 1f - Mathf.Exp(-10f * dt));

            float amp = _trauma * _trauma * 0.6f;
            float t = Time.realtimeSinceStartup * 25f;
            Vector3 shake = new Vector3((Mathf.PerlinNoise(t, 0f) * 2f) - 1f, (Mathf.PerlinNoise(0f, t) * 2f) - 1f, 0f) * amp;
            _camera.transform.position = _cameraAt + shake;
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
            int size = (int)SurvivorSim.ArenaSize;
            float mid = size / 2f;
            for (int y = 0; y < size; y += 2)
            {
                for (int x = 0; x < size; x += 2)
                {
                    string art;
                    Color color;
                    if (forest)
                    {
                        // 숲: 짙은 풀밭에 가운데 십자 흙길.
                        bool path = Mathf.Abs(x + 1f - mid) < SurvivorForest.PathHalf + 0.5f || Mathf.Abs(y + 1f - mid) < SurvivorForest.PathHalf + 0.5f;
                        bool alt = ((x * 7) + (y * 13)) % 5 == 0;
                        art = path ? (alt ? "TopDown/dirt_b" : "TopDown/dirt") : (alt ? "TopDown/grass_b" : "TopDown/grass_a");
                        float shade = (path ? 0.55f : 0.3f) + (0.05f * (((x * 3) + (y * 5)) % 4) / 3f);
                        color = path ? new Color(shade * 1.05f, shade * 0.85f, shade * 0.65f) : new Color(shade * 0.85f, shade * 1.05f, shade * 0.7f);
                    }
                    else
                    {
                        // 가운데 광장과 가게 앞 길은 돌바닥, 나머지는 풀밭.
                        bool plaza = Mathf.Abs(x + 1f - mid) < 8f && Mathf.Abs(y + 1f - mid) < 8f;
                        bool street = Mathf.Abs(y + 1f - 25f) < 1.5f || Mathf.Abs(y + 1f - 37f) < 1.5f || Mathf.Abs(x + 1f - 23f) < 1.5f || Mathf.Abs(x + 1f - 37f) < 1.5f;
                        bool alt = ((x * 7) + (y * 13)) % 5 == 0;
                        art = plaza || street ? (alt ? "TopDown/floor_stone_b" : "TopDown/floor_stone_a") : (alt ? "TopDown/grass_b" : "TopDown/grass_a");
                        float shade = (plaza || street ? 0.42f : 0.36f) + (0.05f * (((x * 3) + (y * 5)) % 4) / 3f);
                        color = plaza || street ? new Color(shade, shade, shade * 1.05f) : new Color(shade * 1.1f, shade, shade * 0.8f);
                    }
                    SpriteRenderer r = GroundSprite("Ground", art, 0);
                    r.transform.localPosition = new Vector3(x + 1f, y + 1f, 0.1f);
                    r.transform.localScale = Vector3.one * Art.FitWidth(r.sprite, 2f);
                    r.color = color;
                }
            }

            if (forest)
            {
                // 덤불과 바위(판정 없음): 길과 구조물을 피해 흩어 둔다.
                for (int i = 0; i < 70; i++)
                {
                    var at = new Vector3(1f + (Hash01(i * 3) * (size - 2f)), 1f + (Hash01((i * 3) + 1) * (size - 2f)), 0.08f);
                    var p = new Vec2(at.x, at.y);
                    if (Mathf.Abs(at.x - mid) < SurvivorForest.PathHalf + 1f || Mathf.Abs(at.y - mid) < SurvivorForest.PathHalf + 1f) continue;
                    if (_sim.Structures.Exists(st => st.Within(p, 0.9f))) continue;
                    bool rock = Hash01((i * 3) + 2) < 0.3f;
                    SpriteRenderer r = GroundSprite("Decor", rock ? "Map/rock" : "Map/bush", 1);
                    r.transform.localPosition = at;
                    r.transform.localScale = Vector3.one * Art.FitWidth(r.sprite, rock ? 0.8f : 1.2f);
                    r.color = rock ? new Color(0.6f, 0.6f, 0.62f) : new Color(0.55f, 0.75f, 0.5f);
                }
            }

            for (int i = -1; i <= size; i++)
            {
                Wall(i, -1);
                Wall(i, size);
                Wall(-1, i);
                Wall(size, i);
            }

            // 출동해 온 소방차(장식, 판정 없음).
            SpriteRenderer truck = GroundSprite("FireTruck", "Vehicles/firetruck", 2);
            truck.transform.localPosition = new Vector3(mid - 3.5f, mid - 2.5f, 0.05f);
            truck.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            truck.transform.localScale = Vector3.one * Art.FitWidth(truck.sprite, 1.3f);
        }

        /// <summary>바닥·벽·장식 스프라이트. 스테이지가 바뀌면 한꺼번에 지운다.</summary>
        private SpriteRenderer GroundSprite(string name, string art, int order)
        {
            SpriteRenderer r = NewSprite(_root, name, Art.Get(art), order);
            _ground.Add(r.gameObject);
            return r;
        }

        /// <summary>가게·창고 이름표. 판마다 동네를 새로 깔므로 다시 만든다.</summary>
        private void BuildSigns()
        {
            foreach (TextMesh t in _signs) UiKit.Discard(t.gameObject);
            foreach (TextMesh t in _helps) UiKit.Discard(t.gameObject);
            _signs.Clear();
            _helps.Clear();
            if (_partnerTag != null) UiKit.Discard(_partnerTag.gameObject);
            _partnerTag = NewText();
            _partnerTag.transform.SetParent(_root, false);
            _partnerTag.GetComponent<MeshRenderer>().sortingOrder = 18;
            _partnerTag.text = "구조대";
            _partnerTag.characterSize = 0.04f;
            _partnerTag.color = new Color(1f, 0.9f, 0.35f);
            _partnerTag.gameObject.SetActive(false);
            if (_forecastTag != null) UiKit.Discard(_forecastTag.gameObject);
            _forecastTag = NewText();
            _forecastTag.transform.SetParent(_root, false);
            _forecastTag.GetComponent<MeshRenderer>().sortingOrder = 18;
            _forecastTag.characterSize = 0.06f;
            _forecastTag.gameObject.SetActive(false);
            foreach (Structure st in _sim.Structures)
            {
                if (!st.IsBuilding) continue;
                // 이름은 앞면 간판 띠 위에 쓴다. 밝은 간판이면 글씨가 어둡다.
                ShopArt.Look look = ShopArt.For(st.Name, st.Half.X * 2f, st.Half.Y * 2f);
                TextMesh t = NewText();
                t.transform.SetParent(_root, false);
                t.GetComponent<MeshRenderer>().sortingOrder = 5;
                t.text = st.Name;
                t.characterSize = 0.055f;
                t.color = look.SignDark ? new Color(0.15f, 0.15f, 0.2f) : Color.white;
                t.transform.localPosition = new Vector3(st.Pos.X, st.Pos.Y + look.SignY, -0.1f);
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
            int sign = 0;
            for (int i = 0; i < _sim.Structures.Count; i++)
            {
                Structure st = _sim.Structures[i];
                Vector3 at = W(st.Pos);
                float w = st.Half.X * 2f;
                float h = st.Half.Y * 2f;
                float burnt = 1f - Mathf.Clamp01(st.Integrity);
                float wet = st.Wet > 0f ? Mathf.Min(1f, st.Wet / 2f) : 0f;

                if (st.IsBuilding)
                {
                    if (sign < _signs.Count)
                    {
                        _signs[sign].gameObject.SetActive(!st.Collapsed);
                        DrawTrapped(st, _helps[sign], i);
                        sign++;
                    }
                    DrawBuilding(st, at, w, h, burnt, wet, i);
                    continue;
                }

                if (st.Collapsed)
                {
                    // 탄 자리: 검은 그루터기·잔해만 남는다.
                    if (st.Kind != StructureKind.Gas) _houseShadows.Put(at, w * 0.8f, 0f, new Color(0.08f, 0.07f, 0.07f, 0.8f), null, h / w);
                    continue;
                }

                Color tint = Color.Lerp(Color.white, new Color(0.25f, 0.2f, 0.2f), burnt);
                if (wet > 0f) tint = Color.Lerp(tint, new Color(0.7f, 0.85f, 1f), 0.35f * wet);
                switch (st.Kind)
                {
                    case StructureKind.Tree:
                        _shadows.Put(at + new Vector3(0.3f, -0.4f, 0f), 2f, 0f, new Color(0f, 0f, 0f, 0.35f), null, 0.6f);
                        // 숲은 소나무·둥근 나무를 섞어 심는다.
                        string treeArt = _sim.Stage.Number == 2 ? (i % 3 == 0 ? "Map/tree_pine" : "Map/tree_round") : "Props/tree_large";
                        _props.Put(at, 1.8f, 0f, tint, Art.Get(treeArt));
                        break;
                    case StructureKind.Car:
                        _shadows.Put(at + new Vector3(0.15f, -0.3f, 0f), 2.3f, 0f, new Color(0f, 0f, 0f, 0.4f), null, 0.5f);
                        _props.Put(at, 1.08f, 90f, tint, Art.Get(i % 2 == 0 ? "Vehicles/car_blue" : "Vehicles/car_black"));
                        break;
                    case StructureKind.Gas:
                        // 퓨즈가 도는 동안 빨갛게 깜빡이며 부풀고 불똥이 튄다.
                        float fuse = st.Fuse >= 0f ? 1f - (st.Fuse / SurvivorSim.GasFuse) : 0f;
                        bool blink = st.Fuse >= 0f && Mathf.Sin(_time * (10f + (30f * fuse))) > 0f;
                        _shadows.Put(at + new Vector3(0.1f, -0.25f, 0f), 1f, 0f, new Color(0f, 0f, 0f, 0.4f), null, 0.5f);
                        if (st.Fuse >= 0f)
                        {
                            _roofGlow.Put(at, 2f + (2f * fuse), 0f, new Color(1f, 0.25f, 0.05f, blink ? 0.8f : 0.35f));
                            if (Random.value < 0.3f)
                            {
                                Emit(Sparks[Random.Range(0, Sparks.Length)], at + new Vector3(0f, 0.3f, 0f), new Vector3(Random.Range(-2f, 2f), Random.Range(2f, 4f), 0f), 1f, 0.35f,
                                    0.3f, 0.05f, new Color(1f, 0.9f, 0.4f), new Color(1f, 0.3f, 0.05f, 0f), 0f, true);
                            }
                        }
                        _props.Put(at, 0.85f * (1f + (0.25f * fuse)), 0f, blink ? new Color(1f, 0.55f, 0.45f) : tint, Art.Get("Props/barrel_red"));
                        break;
                }
                if (st.Burning && st.Kind != StructureKind.Gas) DrawRoofFire(st, at, w, h, i);
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

            // 가게 그림 한 장(지붕+앞면). 탈수록 검게 그을리고, 불빛에 붉게 일렁이고, 젖으면 파랗게 번들거린다.
            ShopArt.Look look = ShopArt.For(st.Name, w, h);
            Color tint = Color.Lerp(Color.white, new Color(0.2f, 0.16f, 0.15f), Mathf.Pow(burnt, 0.7f));
            if (st.Burning) tint = Color.Lerp(tint, new Color(1f, 0.6f, 0.4f), 0.15f * st.Fire * (0.7f + (0.3f * Mathf.Sin(_time * 9f + seed))));
            if (wet > 0f) tint = Color.Lerp(tint, new Color(0.65f, 0.82f, 1f), 0.3f * wet);
            _houseShadows.Put(at + new Vector3(0.35f, -0.4f, 0f), w + 0.3f, 0f, new Color(0f, 0f, 0f, 0.4f), null, (h + 0.3f) / (w + 0.3f));
            _roofs.Put(at, w, 0f, tint, look.Sprite, 1f);

            // 불이 나면 유리창 안쪽이 주황으로 일렁인다.
            if (st.Burning)
            {
                foreach (Rect r in look.Windows)
                {
                    float flick = 0.5f + (0.5f * Mathf.Sin((_time * 13f) + seed + r.x));
                    Vector3 c = at + new Vector3(r.center.x, r.center.y, 0f);
                    _roofTrim.Put(c, r.width, 0f, Color.Lerp(new Color(1f, 0.45f, 0.1f, 0.85f), new Color(1f, 0.8f, 0.35f, 0.9f), flick * st.Fire), null, r.height / r.width);
                    _roofGlow.Put(c, r.width * 1.8f, 0f, new Color(1f, 0.5f, 0.12f, 0.25f + (0.35f * st.Fire)));
                }
            }
            else if (look.Steam.HasValue && Random.value < 0.025f)
            {
                // 굴뚝·배기구·솥에서 가끔 흰 김이 오른다: 사람이 사는 가게.
                Vector3 from = at + new Vector3(look.Steam.Value.x, look.Steam.Value.y, 0f);
                Emit(Smokes[Random.Range(0, Smokes.Length)], from, new Vector3(Random.Range(0.1f, 0.4f), Random.Range(0.6f, 1f), 0f), 0.4f, Random.Range(1.2f, 1.8f),
                    0.25f, 0.9f, new Color(1f, 1f, 1f, 0.45f), new Color(1f, 1f, 1f, 0f), Random.Range(-40f, 40f));
            }

            if (st.Burning) DrawRoofFire(st, at, w, h, seed);

            // 한 번이라도 탔으면 위에 튼튼함 막대(초록→빨강).
            if (st.Integrity < 0.999f)
            {
                float bw = w * 0.8f;
                Vector3 bar = at + new Vector3(0f, st.Half.Y + 0.45f, 0f);
                _bars.Put(bar, bw + 0.08f, 0f, new Color(0f, 0f, 0f, 0.7f), null, 0.22f / (bw + 0.08f));
                float fill = Mathf.Clamp01(st.Integrity);
                _bars.Put(bar + new Vector3(-(bw * (1f - fill)) / 2f, 0f, -0.01f), Mathf.Max(0.01f, bw * fill), 0f,
                    Color.Lerp(new Color(1f, 0.25f, 0.15f), new Color(0.45f, 0.95f, 0.4f), fill), null, 0.14f / Mathf.Max(0.01f, bw * fill));
            }
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

            Vector3 at = W(st.Pos);
            float bob = Mathf.Abs(Mathf.Sin((_time * 7f) + seed));
            ShopArt.Look look = ShopArt.For(st.Name, st.Half.X * 2f, st.Half.Y * 2f);
            Vector2 pane = look.Windows.Length > 0 ? look.Windows[0].center : Vector2.zero;
            Vector3 win = at + new Vector3(pane.x, pane.y + (0.06f * bob), 0f);
            _civilians.Put(win, 0.55f, 8f * Mathf.Sin(_time * 9f + seed), Color.white, Art.Get(Faces[seed % Faces.Length]));

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
                    _civilians.Put(at + new Vector3(left + (k * gap), st.Half.Y + 0.35f + wob, -0.1f), 0.5f, 8f * Mathf.Sin((_time * 9f) + k), Color.white, Art.Get(Faces[(seed + k) % Faces.Length]));
                }
                _civilianRings.Put(at, Mathf.Max(st.Half.X, st.Half.Y) * (3f + (0.4f * Mathf.Sin(_time * 5f))), 0f, new Color(1f, 0.2f, 0.1f, 0.35f));
            }
            bool blink = choking && Mathf.Sin(_time * (8f + (16f * urgent))) > 0f;
            help.color = blink ? new Color(1f, 0.35f, 0.3f) : Color.white;
            help.transform.localPosition = at + new Vector3(0f, st.Half.Y + (big ? 1.5f : 1.1f) + (0.15f * bob), -0.2f);
            help.characterSize = 0.06f * (1f + (0.12f * bob));

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
        private void DrawRoofFire(Structure st, Vector3 at, float w, float h, int seed)
        {
            float f = st.Fire;
            float area = Mathf.Max(w, h);
            _roofGlow.Put(at, area * (1.4f + f), 0f, new Color(1f, 0.35f, 0.08f, 0.35f + (0.35f * f)));
            int n = st.IsBuilding ? 2 + Mathf.RoundToInt(f * (st.Kind == StructureKind.Depot ? 9f : 6f)) : 1 + Mathf.RoundToInt(f * 2f);
            for (int k = 0; k < n; k++)
            {
                float ox = (Hash01((seed * 31) + k) - 0.5f) * w * 0.85f;
                float oy = (Hash01((seed * 17) + (k * 5)) - 0.5f) * h * 0.75f;
                float flick = 0.85f + (0.2f * Mathf.Sin((_time * (11f + k)) + (k * 1.9f)));
                float size = (0.8f + (1.3f * f)) * flick * (st.IsBuilding ? 1f : 0.8f);
                _roofFire.Put(at + new Vector3(ox, oy + (size * 0.25f), 0f), size, 0f, new Color(1f, 0.5f + (0.2f * Hash01(k + seed)), 0.12f), Art.Get(k % 2 == 0 ? "Effects/fire_02" : "Effects/fire_01"));
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
            _roofGlow = AddPool("RoofGlow", "Effects/glow", 8, true);
            _roofFire = AddPool("RoofFire", "Effects/fire_02", 11);
            _bars = new Pool(_world, "Bar", Art.White, 19, null);
            _edgeArrows = new Pool(_world, "EdgeArrow", ArrowSprite(), 22, null);
            _pools.Add(_edgeArrows);
            _pools.Add(_houseShadows);
            _pools.Add(_roofEdges);
            _pools.Add(_roofs);
            _pools.Add(_roofTrim);
            _pools.Add(_bars);
            _groundGlow = AddPool("GroundGlow", "Effects/glow", 3, true);
            _shadows = AddPool("Shadow", "Effects/glow", 4);
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
            _truck = new Pool(_world, "Truck", Art.Get("Vehicles/firetruck"), 20, null);
            _pools.Add(_truck);
            _siren = AddPool("Siren", "Effects/glow", 21, true);
            _cloud = AddPool("Cloud", "Effects/smoke_03", 25);
            _plane = new Pool(_world, "Plane", PlaneSprite(), 26, null);
            _pools.Add(_plane);
            _toolbox = new Pool(_world, "Toolbox", ToolboxSprite(), 8, null);
            _chest = new Pool(_world, "Chest", ChestSprite(), 8, null);
            _pools.Add(_chest);
            _pools.Add(_toolbox);
            _pools.Add(_gems);
            _pools.Add(_gemCores);
            _civilians = AddPool("Civilian", "TopDown/civilian_man", 8);
            _enemyGlow = AddPool("EnemyGlow", "Effects/glow", 8, true);
            _embers = AddPool("Ember", "Effects/fire_01", 9);
            _blazes = AddPool("Blaze", "Effects/fire_02", 9);
            _darts = AddPool("Dart", "Effects/flame_05", 9, true);
            _bats = new Pool(_world, "Bat", BatSprite(), 9, null);
            _pools.Add(_bats);
            _enemyCore = AddPool("EnemyCore", "Effects/fire_01", 10, true);
            _bombShadows = AddPool("BombShadow", "Effects/glow", 11);
            _heliShadow = new Pool(_world, "HeliShadow", HeliSprite(), 11, null);
            _heli = new Pool(_world, "Heli", HeliSprite(), 23, null);
            _rotor = new Pool(_world, "Rotor", RotorSprite(), 24, null);
            _partner = AddPool("Partner", "TopDown/player_suit_1", 15);
            _sprayLines = new Pool(_world, "SprayLine", Art.White, 14, null);
            _pools.Add(_sprayLines);
            _turretBase = new Pool(_world, "TurretBase", DiscSprite(), 13, null);
            _pools.Add(_turretBase);
            _pools.Add(_heliShadow);
            _pools.Add(_heli);
            _pools.Add(_rotor);
            _droneGlow = AddPool("DroneGlow", "Effects/glow", 11, true);
            _drones = AddPool("Drone", "UI/button_blue_round", 12);
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
            _bombs = AddPool("Bomb", "Effects/water_drop", 13);

            _playerGlow = NewSprite(_root, "Magnet", Art.Get("Effects/glow"), 5);
            _playerGlow.color = new Color(0.4f, 0.7f, 1f, 0.08f);
            _player = NewSprite(_root, "Player", Art.Get("TopDown/player_suit_0"), 15);
            _player.transform.localScale = Vector3.one * Art.FitWidth(_player.sprite, 0.95f);
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
            private readonly Transform _parent;
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
                }
                SpriteRenderer r = _items[_used];
                if (sprite != null && r.sprite != sprite)
                {
                    r.sprite = sprite;
                    _units[_used] = Art.FitWidth(sprite, 1f);
                }
                float unit = _units[_used];
                _used++;

                if (!r.enabled) r.enabled = true;
                Transform t = r.transform;
                t.localPosition = at;
                t.localRotation = Quaternion.Euler(0f, 0f, degrees);
                t.localScale = new Vector3(unit * size, unit * size * stretch, 1f);
                r.color = color;
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
            public void Put(List<WaterRibbon.Point> pts, float widthScale, float offset, Color color, float flowSpeed)
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
                    var center = new Vector3(p.Pos.X, p.Pos.Y, 0f);
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

            Image hpBack = UiKit.Image(_hud, "HpBack", Art.White, new Color(0f, 0f, 0f, 0.6f));
            UiKit.Place(hpBack.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -110f), new Vector2(380f, 32f));
            _hpFill = UiKit.Image(hpBack.transform, "HpFill", Art.White, new Color(0.9f, 0.25f, 0.2f));
            _hpFill.rectTransform.anchorMin = Vector2.zero;
            _hpFill.rectTransform.anchorMax = Vector2.one;
            _hpFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _hpFill.rectTransform.offsetMin = new Vector2(3f, 3f);
            _hpFill.rectTransform.offsetMax = new Vector2(-3f, -3f);
            _hpText = UiKit.OutlinedLabel(hpBack.transform, "HpText", "", 24, Color.white, TextAnchor.MiddleCenter);

            // 산불 숲: 체력 아래 바람 화살표.
            _windLabel = UiKit.OutlinedLabel(_hud, "WindLabel", "바람", 28, new Color(0.85f, 0.92f, 1f), TextAnchor.MiddleLeft);
            UiKit.Place(_windLabel.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -168f), new Vector2(120f, 50f));
            _windArrow = UiKit.Image(_hud, "WindArrow", ArrowSprite(), new Color(0.85f, 0.92f, 1f));
            _windArrow.raycastTarget = false;
            UiKit.Place(_windArrow.rectTransform, new Vector2(0f, 1f), new Vector2(160f, -168f), new Vector2(56f, 56f));
            // 가운데를 축으로 돌게(모서리 축이면 돌 때 체력 막대로 올라간다).
            _windArrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _windArrow.rectTransform.anchoredPosition = new Vector2(160f, -193f);
            UiKit.Stretch(_hpText.rectTransform);

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

            float hp = Mathf.Clamp01(_sim.Hp / _sim.MaxHp);
            _hpFill.rectTransform.localScale = new Vector3(hp, 1f, 1f);
            _hpText.text = Mathf.CeilToInt(_sim.Hp) + " / " + Mathf.RoundToInt(_sim.MaxHp);

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

            _flashImage.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, _flash * 0.8f);

            _alert.color = new Color(_alert.color.r, _alert.color.g, _alert.color.b, _alertAge < 2f ? 1f : Mathf.Max(0f, 1f - ((_alertAge - 2f) * 2f)));
            float s = _alertAge < 0.15f ? Mathf.Lerp(1.6f, 1f, _alertAge / 0.15f) : 1f;
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
                _bossName.text = people ? "대화재 · " + mark.Name + "에 " + mark.Residents + "명 갇힘" : "대화재 · 끝까지 지켜라";
                float left = Mathf.Clamp01((SurvivorSim.RunTime - _sim.Time) / (SurvivorSim.RunTime - SurvivorSim.FinaleAt));
                _bossFill.rectTransform.localScale = new Vector3(left, 1f, 1f);
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
                string more = (played / 60).ToString("00") + ":" + (played % 60).ToString("00") + " 버팀  ·  처치 " + _sim.Kills + "  ·  Lv " + _sim.Level;
                _result.text = head + "\n\n" + (won ? "\n\n" : "") + stats + "\n" + more + "\n\n" + (_overAge > 1f ? (won ? "탭하면 다음: STAGE " + SurvivorStages.Next(_stage) + " " + SurvivorStages.Get(SurvivorStages.Next(_stage)).Name : "탭하면 다시") : "");
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

        /// <summary>화면 밖 타는 건물·가스통을 화면 가장자리 화살표로 가리킨다. 갇힌 사람이 있으면 초록으로 크게 뛴다.</summary>
        private void DrawEdgeArrows()
        {
            if (_sim.Outcome != SOutcome.Playing || _camera == null) return;
            // 카메라가 따라갈 자리 기준으로 잡는다(이번 프레임 FollowCamera 전이라).
            Vector3 eye = _cameraAt;
            float halfH = _camera.orthographicSize;
            float halfW = halfH * _camera.aspect;
            foreach (Structure st in _sim.Structures)
            {
                if (!st.Burning || !(st.IsBuilding || st.Kind == StructureKind.Gas)) continue;
                float dx = st.Pos.X - eye.x;
                float dy = st.Pos.Y - eye.y;
                if (Mathf.Abs(dx) < halfW * 0.96f && Mathf.Abs(dy) < halfH * 0.96f) continue;
                // 가장자리(위는 HUD를 피해 조금 더 안쪽)에 붙인다.
                float k = Mathf.Min((halfW * 0.92f) / Mathf.Max(Mathf.Abs(dx), 0.001f), (halfH * (dy > 0f ? 0.72f : 0.84f)) / Mathf.Max(Mathf.Abs(dy), 0.001f));
                var spot = new Vector3(eye.x + (dx * k), eye.y + (dy * k), 0f);
                bool people = st.Residents > 0;
                // 대형 신고·대화재 건물은 붉게, 가장 크게 뛴다.
                bool big = people && (st == _sim.BigReport || st == _sim.Landmark);
                float beat = 1f + ((people ? 0.25f : 0.1f) * Mathf.Abs(Mathf.Sin(_time * (people ? 8f : 5f))));
                Color c = big ? new Color(1f, 0.25f, 0.2f) : people ? new Color(0.45f, 1f, 0.45f) : st.Kind == StructureKind.Gas ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.55f, 0.2f);
                _edgeArrows.Put(spot, 1.1f * beat * (big ? 1.6f : people ? 1.3f : 1f), Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, c);
            }
            foreach (Pickup chest in _sim.Chests)
            {
                // 화면 밖 보물상자는 금빛 화살표로 크게 가리킨다.
                float dx = chest.Pos.X - eye.x;
                float dy = chest.Pos.Y - eye.y;
                if (Mathf.Abs(dx) < halfW * 0.96f && Mathf.Abs(dy) < halfH * 0.96f) continue;
                float k = Mathf.Min((halfW * 0.92f) / Mathf.Max(Mathf.Abs(dx), 0.001f), (halfH * (dy > 0f ? 0.72f : 0.84f)) / Mathf.Max(Mathf.Abs(dy), 0.001f));
                var spot = new Vector3(eye.x + (dx * k), eye.y + (dy * k), 0f);
                _edgeArrows.Put(spot, 1.5f * (1f + (0.2f * Mathf.Abs(Mathf.Sin(_time * 7f)))), Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, new Color(1f, 0.85f, 0.3f));
            }
            foreach (Pickup box in _sim.Toolboxes)
            {
                // 화면 밖 공구상자는 주황 화살표로 가리킨다.
                float dx = box.Pos.X - eye.x;
                float dy = box.Pos.Y - eye.y;
                if (Mathf.Abs(dx) < halfW * 0.96f && Mathf.Abs(dy) < halfH * 0.96f) continue;
                float k = Mathf.Min((halfW * 0.92f) / Mathf.Max(Mathf.Abs(dx), 0.001f), (halfH * (dy > 0f ? 0.72f : 0.84f)) / Mathf.Max(Mathf.Abs(dy), 0.001f));
                var spot = new Vector3(eye.x + (dx * k), eye.y + (dy * k), 0f);
                _edgeArrows.Put(spot, 1.2f * (1f + (0.15f * Mathf.Abs(Mathf.Sin(_time * 6f)))), Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, new Color(1f, 0.75f, 0.2f));
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

        private void ShowAlert(string text, Color color)
        {
            _alert.text = text;
            _alert.color = color;
            _alertAge = 0f;
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
                Image ray = UiKit.Image(_cardLayer, "Ray" + i, BeamSprite(), new Color(1f, 0.9f, 0.5f, 0.35f));
                ray.raycastTarget = false;
                UiKit.Place(ray.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 360f), new Vector2(90f, 900f));
                _rays.Add(ray.rectTransform);
            }

            bool yellow = choices.Exists(Loadout.IsSpecial);
            if (yellow)
            {
                Flash(new Color(1f, 0.85f, 0.3f), 0.35f);
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
                    Image glow = UiKit.Image(_cardLayer, "YellowGlow" + i, Art.Get("Effects/glow"), new Color(1f, 0.8f, 0.2f, 0.8f));
                    glow.raycastTarget = false;
                    UiKit.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - ((choices.Count - 1) / 2f)) * 470f, -20f), new Vector2(720f, 820f));
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
                g.color = new Color(1f, 0.8f, 0.2f, 0.45f + (0.4f * beat));
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
