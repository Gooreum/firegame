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
        private int _seed = 3;
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
        private float _sprayClock;
        private float _putOutClock;
        private float _gemClock;
        private float _comboClock;
        private int _combo;
        private float _hitStop;
        private float _zoomKick;
        private float _recoil;
        private float _cardsIn = -1f;
        private float _waveAge = 99f;
        private float _hurtClock;
        private int _comboShown;
        private readonly List<RectTransform> _rays = new List<RectTransform>();
        private SOutcome _lastOutcome;
        private Vector3 _cameraAt;

        // --- 매 프레임 다시 그리는 풀 ---
        private Pool _enemyGlow;
        private Pool _enemyCore;
        private Pool _embers;
        private Pool _blazes;
        private Pool _darts;
        private Pool _bossBody;
        private Pool _bossTongues;
        private Pool _gems;
        private Pool _gemCores;
        private Pool _drops;
        private Pool _dropGlow;
        private Pool _beams;
        private Pool _beamCores;
        private Pool _bubbles;
        private Pool _bombs;
        private Pool _bombShadows;
        private Pool _drones;
        private Pool _droneGlow;
        private Pool _foam;
        private Pool _groundFire;
        private Pool _groundGlow;
        private Pool _civilians;
        private Pool _civilianRings;
        private SpriteRenderer _player;
        private SpriteRenderer _playerGlow;
        private readonly List<Pool> _pools = new List<Pool>();

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

        public SurvivorView(Transform parent, Camera camera, Canvas canvas)
        {
            _camera = camera;
            _root = new GameObject("SurvivorWorld").transform;
            _root.SetParent(parent, false);
            _world = new GameObject("Dynamic").transform;
            _world.SetParent(_root, false);
            _hud = UiKit.Node(canvas.transform, "SurvivorHud");
            UiKit.Stretch(_hud);

            BuildGround();
            BuildPools();
            BuildHud();
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
            _sim = new SurvivorSim(seed);
            _accumulator = 0f;
            _trauma = 0f;
            _flash = 0f;
            _hurt = 0f;
            _slowmo = 0f;
            _overAge = 0f;
            _alertAge = 99f;
            _bossBannerAge = 99f;
            _shownXp = 0f;
            _hitStop = 0f;
            _zoomKick = 0f;
            _recoil = 0f;
            _cardsIn = -1f;
            _waveAge = 99f;
            _hurtClock = 0f;
            _comboShown = 0;
            _lastOutcome = SOutcome.Playing;
            _cameraAt = new Vector3(_sim.Player.X, _sim.Player.Y, -10f);
            ClearEffects();
            HideCards();
            _resultBack.gameObject.SetActive(false);
            Refresh(0f);
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
            if (_sim.Outcome != SOutcome.Playing)
            {
                if (_overAge > 1f && input.MouseClicked)
                {
                    Restart(_seed + 1);
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
                while (_accumulator >= SurvivorSim.Dt && _sim.PendingChoices == null && _sim.Outcome == SOutcome.Playing)
                {
                    _accumulator -= SurvivorSim.Dt;
                    Step(move);
                }
                if (_sim.PendingChoices != null) _accumulator = 0f;
            }

            Refresh(dt);
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
            _sim.Choose(index);
            _cardsIn = -1f;
            HideCards();
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
            else
            {
                GameAudio.Play(Cue.PickUp);
            }
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
                else HitSplash(at, Away(h.Pos));
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

            foreach (Vec2 e in _sim.Explosions)
            {
                WaterBlast(W(e));
                _trauma = Mathf.Min(1f, _trauma + 0.12f);
                HitStop(0.02f);
                GameAudio.Play(Cue.SprayFoam);
            }

            if (_sim.ShotsFired > 0)
            {
                Muzzles();
                if (_sprayClock <= 0f)
                {
                    GameAudio.Play(Cue.SprayWater);
                    _sprayClock = 0.28f;
                }
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

            if (_sim.JustBossArrived)
            {
                // 보스 자리에서 붉은 고리 세 겹 + 검은 연기. 카메라는 뒤로 물러나 큰 놈을 보여 준다.
                Vector3 at = W(_sim.Boss.Pos);
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
                _bossBannerAge = 0f;
                _trauma = 1f;
                Flash(new Color(1f, 0.2f, 0.1f), 0.5f);
                GameAudio.Play(Cue.Critical);
                GameAudio.Play(Cue.Backfire);
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
                    // 거인이 꺼지는 순간: 번쩍 → (잠깐 뒤) 고리 세 겹 + 거대한 김 + 불똥 비.
                    Vector3 at = W(_sim.Boss.Pos);
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

        // ------------------------------------------------------------------
        // 매 프레임
        // ------------------------------------------------------------------

        public void Refresh(float dt)
        {
            _time += dt;
            _trauma = Mathf.Max(0f, _trauma - (dt * 1.8f));
            _flash = Mathf.Max(0f, _flash - (dt * 2.2f));
            _hurt = Mathf.Max(0f, _hurt - (dt * 1.5f));
            _slowmo = Mathf.Max(0f, _slowmo - dt);
            _xpPunch = Mathf.Max(0f, _xpPunch - (dt * 5f));
            _zoomKick *= Mathf.Exp(-6f * dt);
            _recoil = Mathf.Max(0f, _recoil - (dt * 12f));
            _hurtClock -= dt;
            _waveAge += dt;
            if (_cardsIn > 0f)
            {
                _cardsIn -= dt;
                if (_cardsIn <= 0f && _sim.PendingChoices != null) ShowCards();
            }
            _sprayClock -= dt;
            _putOutClock -= dt;
            _gemClock -= dt;
            _comboClock -= dt;
            _cardAge += dt;
            _alertAge += dt;
            _bossBannerAge += dt;
            if (_sim.Outcome != SOutcome.Playing) _overAge += dt;

            foreach (Pool p in _pools) p.Begin();
            DrawPuddles();
            DrawGems();
            DrawCivilians();
            DrawEnemies();
            DrawShots();
            DrawPlayer();
            foreach (Pool p in _pools) p.End();

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
                float punch = hit ? 1.35f : 1f;
                var water = new Color(0.7f, 0.95f, 1f);

                if (e.Kind == EnemyKind.Boss)
                {
                    float pulse = 1f + (0.05f * Mathf.Sin(_time * 5f));
                    _enemyGlow.Put(at, 12f * pulse, 0f, new Color(1f, 0.3f, 0.05f, 0.75f));
                    _bossBody.Put(at + new Vector3(0f, 0.5f, 0f), 8f * pulse * punch, 0f, hit ? water : new Color(0.95f, 0.25f, 0.05f));
                    _enemyCore.Put(at + new Vector3(0f, 0.3f, 0f), 5f * flicker, 0f, new Color(1f, 0.8f, 0.3f, 0.95f));
                    for (int k = 0; k < 10; k++)
                    {
                        float a = (_time * 1.3f) + (k * Mathf.PI / 5f);
                        Vector3 o = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (2.4f + (0.3f * Mathf.Sin((_time * 6f) + k)));
                        _bossTongues.Put(at + o, 1.6f * flicker, (a * Mathf.Rad2Deg) - 90f, new Color(1f, 0.5f, 0.1f, 0.9f));
                    }
                    continue;
                }

                switch (e.Kind)
                {
                    case EnemyKind.Ember:
                        _enemyGlow.Put(at, 1.4f * flicker, 0f, new Color(1f, 0.4f, 0.08f, 0.25f));
                        _embers.Put(at + new Vector3(0f, 0.15f, 0f), 1.9f * flicker * punch, 0f, hit ? water : new Color(1f, 0.45f, 0.1f));
                        if (!hit) _enemyCore.Put(at + new Vector3(0f, 0.08f, 0f), 1.1f * flicker, 0f, new Color(1f, 0.8f, 0.35f, 0.9f));
                        break;
                    case EnemyKind.Blaze:
                        _enemyGlow.Put(at, 2.4f * flicker, 0f, new Color(1f, 0.3f, 0.05f, 0.35f));
                        _blazes.Put(at + new Vector3(0f, 0.25f, 0f), 3.1f * flicker * punch, 0f, hit ? water : new Color(0.95f, 0.28f, 0.06f));
                        if (!hit) _enemyCore.Put(at + new Vector3(0f, 0.12f, 0f), 1.8f * flicker, 0f, new Color(1f, 0.7f, 0.25f, 0.9f));
                        break;
                    case EnemyKind.Dart:
                        float toward = Mathf.Atan2(_sim.Player.Y - e.Pos.Y, _sim.Player.X - e.Pos.X) * Mathf.Rad2Deg;
                        _enemyGlow.Put(at, 1.5f, 0f, new Color(1f, 0.8f, 0.2f, 0.45f));
                        _darts.Put(at, 0.9f * flicker * punch, toward + 90f, hit ? water : new Color(1f, 0.9f, 0.35f));
                        break;
                }
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

        private void DrawCivilians()
        {
            for (int i = 0; i < _sim.Civilians.Count; i++)
            {
                Civilian c = _sim.Civilians[i];
                Vector3 at = W(c.Pos);
                float blink = c.Life < 5f && Mathf.Sin(_time * 18f) < 0f ? 0.3f : 1f;
                float ring = 1.6f + (0.3f * Mathf.Sin(_time * 6f));
                _civilianRings.Put(at, ring, 0f, new Color(0.4f, 1f, 0.4f, 0.45f * blink));
                _civilians.Put(at + new Vector3(0f, 0.08f * Mathf.Abs(Mathf.Sin(_time * 8f)), 0f), 0.85f, 0f, new Color(1f, 1f, 1f, blink), Art.Get(Faces[i % Faces.Length]));
            }
        }

        private void DrawPuddles()
        {
            for (int i = 0; i < _sim.Foam.Count; i++)
            {
                Puddle p = _sim.Foam[i];
                float t = Mathf.Clamp01(p.Life / p.MaxLife);
                _foam.Put(W(p.Pos), p.Radius * 2.2f * (0.8f + (0.2f * t)), i * 37f, new Color(0.85f, 0.95f, 1f, 0.5f * t), Art.Get(Smokes[i % Smokes.Length]));
            }

            for (int i = 0; i < _sim.BurningGround.Count; i++)
            {
                Puddle p = _sim.BurningGround[i];
                float t = Mathf.Clamp01(p.Life / p.MaxLife);
                Vector3 at = W(p.Pos);
                _groundGlow.Put(at, p.Radius * 3.4f, 0f, new Color(1f, 0.3f, 0.05f, 0.6f * t));
                for (int k = 0; k < 3; k++)
                {
                    float a = (k * 2.1f) + i;
                    Vector3 o = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.4f;
                    float f = 0.6f + (0.15f * Mathf.Sin((_time * 16f) + (k * 2f) + i));
                    _groundFire.Put(at + o, f * (0.6f + (0.5f * t)), 0f, new Color(1f, 0.55f, 0.12f, t), Art.Get(k == 0 ? "Effects/fire_01" : "Effects/fire_02"));
                }
            }
        }

        private void DrawShots()
        {
            foreach (Shot s in _sim.Shots)
            {
                Vector3 at = W(s.Pos);
                switch (s.Kind)
                {
                    case ShotKind.Drop:
                    case ShotKind.Jet:
                    {
                        // 소방관 쪽 꼬리에서 머리까지 마디로 쪼갠 물줄기가 출렁이고, 거품이 앞으로 흘러간다.
                        bool jet = s.Kind == ShotKind.Jet;
                        Vector3 dir = new Vector3(s.Vel.X, s.Vel.Y, 0f).normalized;
                        float len = Mathf.Min(s.Pos.DistanceTo(s.From), jet ? 3f : 4.5f);
                        float seed = (s.From.X * 1.7f) + (s.Vel.Y * 0.9f);
                        Vector3 tail = at - (dir * len);
                        if (len > 0.05f) DrawStream(tail, at, dir, seed, jet ? 1.1f : 0.62f, jet ? 6 : 8, jet ? 4 : 5);
                        _dropGlow.Put(at, jet ? 2f : 1.3f, 0f, new Color(0.35f, 0.75f, 1f, jet ? 0.5f : 0.45f));
                        if (!jet) _drops.Put(at, 0.7f, (Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg) - 90f, new Color(0.7f, 0.93f, 1f));
                        // 머리 둘레에서 빙글 도는 거품 뭉치
                        for (int b = 0; b < 2; b++)
                        {
                            float a = (_time * 14f) + seed + (b * Mathf.PI);
                            Vector3 o = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (jet ? 0.35f : 0.2f);
                            _bubbles.Put(at + o, (jet ? 0.5f : 0.34f) + (0.06f * b), 0f, new Color(0.95f, 1f, 1f, 0.9f));
                        }
                        // 머리에서 물이 부서져 옆·뒤로 떨어져 나간다.
                        if (Random.value < 0.25f)
                        {
                            Vector3 side = new Vector3(-dir.y, dir.x, 0f) * Random.Range(-3f, 3f);
                            Emit("Effects/water_drop", at, side - (dir * Random.Range(1f, 3f)), 5f, 0.25f,
                                Random.Range(0.18f, 0.3f), 0.05f, new Color(0.75f, 0.95f, 1f, 0.95f), new Color(0.6f, 0.9f, 1f, 0f), 0f);
                        }
                        break;
                    }
                    case ShotKind.Bomb:
                        float t = Mathf.Clamp01(s.Age / s.Life);
                        float lift = Mathf.Sin(t * Mathf.PI) * 2f;
                        _bombShadows.Put(at, 0.6f, 0f, new Color(0f, 0f, 0f, 0.35f));
                        _bombs.Put(at + new Vector3(0f, lift, 0f), 0.95f, t * 540f, new Color(0.45f, 0.78f, 1f));
                        break;
                }
            }

            for (int i = 0; i < _sim.Drones.Count; i++)
            {
                Vector3 at = W(_sim.Drones[i]);
                _droneGlow.Put(at, 1.8f, 0f, new Color(0.3f, 0.7f, 1f, 0.6f));
                _drones.Put(at, 0.65f, _time * 720f, Color.white);
                if (Random.value < 0.25f) Splash(at, 1, 0.15f);
            }
        }

        /// <summary>
        /// 줄기를 마디로 쪼개 그린다. 각 마디는 앞 마디와 조금씩 겹쳐 이음매를 숨기고,
        /// 흔들림은 노즐 쪽에서 0이다가 머리로 갈수록 커지며 시간에 따라 앞으로 흘러간다.
        /// </summary>
        private void DrawStream(Vector3 tail, Vector3 head, Vector3 dir, float seed, float width, int segments, int bubbles)
        {
            Vector3 side = new Vector3(-dir.y, dir.x, 0f);
            Vector3 prev = Bend(tail, head, side, seed, 0f);
            for (int k = 1; k <= segments; k++)
            {
                float u = k / (float)segments;
                Vector3 next = Bend(tail, head, side, seed, u);
                Vector3 d = next - prev;
                float deg = (Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg) - 90f;
                float w = width * Mathf.Lerp(0.7f, 1.15f, u) * (1f + (0.12f * Mathf.Sin((_time * 30f) + seed + k)));
                float alpha = Mathf.Lerp(0.35f, 1f, u);
                Vector3 mid = (prev + next) * 0.5f;
                float span = d.magnitude * 1.35f;
                _beams.Put(mid, w, deg, new Color(0.25f, 0.6f, 1f, alpha), null, span / w);
                _beamCores.Put(mid, w * 0.3f, deg, new Color(0.8f, 0.95f, 1f, alpha * 0.8f), null, span / (w * 0.3f));
                prev = next;
            }

            for (int b = 0; b < bubbles; b++)
            {
                float u = Mathf.Repeat((b * 0.23f) + (_time * 2.2f) + seed, 1f);
                Vector3 at = Bend(tail, head, side, seed, u) + (side * (Mathf.Sin((_time * 25f) + (b * 3f)) * 0.1f * (width / 0.62f)));
                _bubbles.Put(at, Mathf.Lerp(0.14f, 0.3f, u) * Mathf.Sqrt(width / 0.62f), 0f, new Color(0.9f, 0.98f, 1f, 0.85f));
            }
        }

        /// <summary>u=0 꼬리(노즐), u=1 머리. 흐르는 사인 두 개를 더해 옆으로 휜다.</summary>
        private Vector3 Bend(Vector3 tail, Vector3 head, Vector3 side, float seed, float u)
        {
            float wave = (Mathf.Sin((u * 7f) - (_time * 18f) + seed) * 0.22f) + (Mathf.Sin((u * 3f) - (_time * 9f) + (seed * 2f)) * 0.16f);
            return Vector3.Lerp(tail, head, u) + (side * (wave * u));
        }

        private void DrawPlayer()
        {
            Vector3 at = W(_sim.Player);
            Vector3 kick = new Vector3(_sim.Facing.X, _sim.Facing.Y, 0f) * (-0.1f * _recoil);
            _player.transform.localPosition = at + kick + new Vector3(0f, 0f, -0.01f);
            _player.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_sim.Facing.Y, _sim.Facing.X) * Mathf.Rad2Deg);
            _player.color = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.3f), Mathf.Clamp01(_hurt * 2f));
            _playerGlow.transform.localPosition = at;
            float r = 2f * _sim.Magnet * 0.5f;
            _playerGlow.transform.localScale = Vector3.one * Art.FitWidth(_playerGlow.sprite, r * 2f);
        }

        // ------------------------------------------------------------------
        // 효과
        // ------------------------------------------------------------------

        /// <summary>불이 꺼지는 순간: 흰 번쩍 + 하얀 수증기 + 불똥. 큰 불은 충격파와 짧은 멈춤까지.</summary>
        private void DeathBurst(Vector3 at, EnemyKind kind, bool crowded)
        {
            bool big = kind == EnemyKind.Blaze || kind == EnemyKind.Boss;
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

        /// <summary>물폭탄: 퍼지는 물 고리 + 위로 솟았다 떨어지는 물기둥 + 김.</summary>
        private void WaterBlast(Vector3 at)
        {
            Shockwave(at, new Color(0.55f, 0.85f, 1f, 1f), 4.2f, 0.35f);
            Emit("Effects/glow", at, Vector3.zero, 0f, 0.12f, 2.5f, 3.5f, new Color(0.9f, 0.97f, 1f, 0.9f), new Color(0.5f, 0.85f, 1f, 0f), 0f, true);
            for (int i = 0; i < 16; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                var v = new Vector3(Mathf.Cos(a) * Random.Range(1.5f, 4.5f), Random.Range(3f, 7f), 0f);
                EmitFalling("Effects/water_drop", at, v, Random.Range(0.45f, 0.7f), Random.Range(0.3f, 0.5f), new Color(0.7f, 0.93f, 1f, 1f));
            }
            Splash(at, 10, 1.2f);
            Steam(at, 3, 1.3f);
        }

        /// <summary>이번 틱에 새로 나간 물줄기마다 총구에서 물이 부채꼴로 튄다(방수포는 사방 한 번에).</summary>
        private void Muzzles()
        {
            Vector3 p = W(_sim.Player);
            int shown = 0;
            bool jet = false;
            foreach (Shot s in _sim.Shots)
            {
                if (s.Age > SurvivorSim.Dt * 1.5f) continue;
                if (s.Kind == ShotKind.Jet)
                {
                    jet = true;
                    continue;
                }
                if (s.Kind != ShotKind.Drop || shown >= 3) continue;
                shown++;
                var dir = new Vector3(s.Vel.X, s.Vel.Y, 0f).normalized;
                Vector3 nozzle = p + (dir * 0.55f);
                float baseAngle = Mathf.Atan2(dir.y, dir.x);
                for (int i = 0; i < 4; i++)
                {
                    float a = baseAngle + Random.Range(-0.45f, 0.45f);
                    Emit("Effects/water_drop", nozzle, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Random.Range(6f, 9f), 9f, 0.15f,
                        0.3f, 0.08f, new Color(0.8f, 0.96f, 1f, 1f), new Color(0.6f, 0.9f, 1f, 0f), 0f);
                }
                Emit("Effects/glow", nozzle, Vector3.zero, 0f, 0.08f, 1.1f, 0.6f, new Color(0.7f, 0.92f, 1f, 0.8f), new Color(0.5f, 0.85f, 1f, 0f), 0f, true);
            }
            if (shown > 0) _recoil = 1f;
            if (jet)
            {
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
            int size = (int)SurvivorSim.ArenaSize;
            for (int y = 0; y < size; y += 2)
            {
                for (int x = 0; x < size; x += 2)
                {
                    bool alt = ((x * 7) + (y * 13)) % 5 == 0;
                    SpriteRenderer r = NewSprite(_root, "Ground", Art.Get(alt ? "TopDown/floor_stone_b" : "TopDown/floor_stone_a"), 0);
                    r.transform.localPosition = new Vector3(x + 1f, y + 1f, 0.1f);
                    r.transform.localScale = Vector3.one * Art.FitWidth(r.sprite, 2f);
                    float shade = 0.3f + (0.04f * (((x * 3) + (y * 5)) % 4) / 3f);
                    r.color = new Color(shade, shade, shade * 1.08f);
                }
            }

            for (int i = -1; i <= size; i++)
            {
                Wall(i, -1);
                Wall(i, size);
                Wall(-1, i);
                Wall(size, i);
            }

            // 도시 광장 느낌만 내는 장식(판정 없음). 늘 같은 자리에 놓는다.
            string[] props = { "Props/tree_large", "Props/tree_small", "Props/barrel_red", "Props/barrel_blue", "Vehicles/cone", "Props/barrier" };
            float[] sizes = { 1.6f, 1.1f, 0.6f, 0.6f, 0.5f, 1f };
            var rng = new FireGame.Core.Sim.Rng(12345);
            for (int i = 0; i < 45; i++)
            {
                int k = rng.Next(props.Length);
                float px = 2f + rng.Next((size - 4) * 10) / 10f;
                float py = 2f + rng.Next((size - 4) * 10) / 10f;
                if (Mathf.Abs(px - (size / 2f)) < 4f && Mathf.Abs(py - (size / 2f)) < 4f) continue;
                SpriteRenderer r = NewSprite(_root, "Prop", Art.Get(props[k]), 2);
                r.transform.localPosition = new Vector3(px, py, 0.05f);
                r.transform.localRotation = Quaternion.Euler(0f, 0f, k == 5 ? rng.Next(4) * 90f : 0f);
                r.transform.localScale = Vector3.one * Art.FitWidth(r.sprite, sizes[k]);
                r.color = new Color(0.55f, 0.55f, 0.6f);
            }
        }

        private void Wall(int x, int y)
        {
            SpriteRenderer r = NewSprite(_root, "Wall", Art.Get(((x + y) & 1) == 0 ? "TopDown/brick_a" : "TopDown/brick_b"), 2);
            r.transform.localPosition = new Vector3(x + 0.5f, y + 0.5f, 0.05f);
            r.transform.localScale = Vector3.one * Art.FitWidth(r.sprite, 1f);
            r.color = new Color(0.32f, 0.26f, 0.25f);
        }

        private void BuildPools()
        {
            _groundGlow = AddPool("GroundGlow", "Effects/glow", 3, true);
            _foam = AddPool("Foam", "Effects/smoke_01", 3);
            _groundFire = AddPool("GroundFire", "Effects/fire_01", 4);
            _civilianRings = AddPool("CivilianRing", "Effects/glow", 5, true);
            _gems = new Pool(_world, "Gem", Art.White, 6, null);
            _gemCores = new Pool(_world, "GemCore", Art.White, 7, null);
            _pools.Add(_gems);
            _pools.Add(_gemCores);
            _civilians = AddPool("Civilian", "TopDown/civilian_man", 8);
            _enemyGlow = AddPool("EnemyGlow", "Effects/glow", 8, true);
            _embers = AddPool("Ember", "Effects/fire_01", 9);
            _blazes = AddPool("Blaze", "Effects/fire_02", 9);
            _darts = AddPool("Dart", "Effects/flame_05", 9, true);
            _bossBody = AddPool("Boss", "Effects/fire_02", 9);
            _enemyCore = AddPool("EnemyCore", "Effects/fire_01", 10, true);
            _bossTongues = AddPool("BossTongue", "Effects/flame_05", 10, true);
            _bombShadows = AddPool("BombShadow", "Effects/glow", 11);
            _droneGlow = AddPool("DroneGlow", "Effects/glow", 11, true);
            _drones = AddPool("Drone", "UI/button_blue_round", 12);
            _beams = new Pool(_world, "Beam", BeamSprite(), 12, Additive);
            _beamCores = new Pool(_world, "BeamCore", BeamSprite(), 13, Additive);
            _pools.Add(_beams);
            _pools.Add(_beamCores);
            _bubbles = new Pool(_world, "Bubble", BubbleSprite(), 14, Additive);
            _pools.Add(_bubbles);
            _dropGlow = AddPool("DropGlow", "Effects/glow", 12, true);
            _drops = AddPool("Drop", "Effects/water_drop", 13);
            _bombs = AddPool("Bomb", "Effects/water_drop", 13);

            _playerGlow = NewSprite(_root, "Magnet", Art.Get("Effects/glow"), 5);
            _playerGlow.color = new Color(0.4f, 0.7f, 1f, 0.08f);
            _player = NewSprite(_root, "Player", Art.Get("TopDown/player_suit_0"), 11);
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

        // ------------------------------------------------------------------
        // HUD
        // ------------------------------------------------------------------

        private void BuildHud()
        {
            _vignette = UiKit.Image(_hud, "Vignette", VignetteSprite(), Color.clear);
            UiKit.Stretch(_vignette.rectTransform);
            _vignette.raycastTarget = false;

            // 경험치 바: 화면 맨 위 가로 전체.
            Image xpBack = UiKit.Image(_hud, "XpBack", Art.White, new Color(0f, 0f, 0f, 0.65f));
            _xpBack = xpBack.rectTransform;
            _xpBack.anchorMin = new Vector2(0f, 1f);
            _xpBack.anchorMax = new Vector2(1f, 1f);
            _xpBack.pivot = new Vector2(0.5f, 1f);
            _xpBack.sizeDelta = new Vector2(0f, 26f);
            _xpBack.anchoredPosition = Vector2.zero;
            _xpFill = UiKit.Image(_xpBack, "XpFill", Art.White, new Color(0.3f, 0.65f, 1f));
            _xpFill.rectTransform.anchorMin = Vector2.zero;
            _xpFill.rectTransform.anchorMax = Vector2.one;
            _xpFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _xpFill.rectTransform.offsetMin = new Vector2(2f, 3f);
            _xpFill.rectTransform.offsetMax = new Vector2(-2f, -3f);

            _level = UiKit.OutlinedLabel(_hud, "Level", "", 40, Color.white, TextAnchor.UpperLeft);
            UiKit.Place(_level.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -34f), new Vector2(300f, 56f));

            _timer = UiKit.OutlinedLabel(_hud, "Timer", "", 58, Color.white, TextAnchor.UpperCenter);
            UiKit.Place(_timer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(400f, 70f));
            _kills = UiKit.OutlinedLabel(_hud, "Kills", "", 30, new Color(1f, 0.85f, 0.6f), TextAnchor.UpperCenter);
            UiKit.Place(_kills.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(400f, 40f));

            Image hpBack = UiKit.Image(_hud, "HpBack", Art.White, new Color(0f, 0f, 0f, 0.6f));
            UiKit.Place(hpBack.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -96f), new Vector2(380f, 32f));
            _hpFill = UiKit.Image(hpBack.transform, "HpFill", Art.White, new Color(0.9f, 0.25f, 0.2f));
            _hpFill.rectTransform.anchorMin = Vector2.zero;
            _hpFill.rectTransform.anchorMax = Vector2.one;
            _hpFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _hpFill.rectTransform.offsetMin = new Vector2(3f, 3f);
            _hpFill.rectTransform.offsetMax = new Vector2(-3f, -3f);
            _hpText = UiKit.OutlinedLabel(hpBack.transform, "HpText", "", 24, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(_hpText.rectTransform);

            _build = UiKit.OutlinedLabel(_hud, "Build", "", 26, new Color(0.85f, 0.92f, 1f), TextAnchor.UpperRight);
            UiKit.Place(_build.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -40f), new Vector2(520f, 400f));

            _alert = UiKit.OutlinedLabel(_hud, "Alert", "", 52, Color.white, TextAnchor.MiddleCenter);
            UiKit.Place(_alert.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1400f, 80f));

            _bossBack = UiKit.Image(_hud, "BossBack", Art.White, new Color(0f, 0f, 0f, 0.7f));
            UiKit.Place(_bossBack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(900f, 34f));
            _bossFill = UiKit.Image(_bossBack.transform, "BossFill", Art.White, new Color(1f, 0.45f, 0.1f));
            _bossFill.rectTransform.anchorMin = Vector2.zero;
            _bossFill.rectTransform.anchorMax = Vector2.one;
            _bossFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _bossFill.rectTransform.offsetMin = new Vector2(3f, 3f);
            _bossFill.rectTransform.offsetMax = new Vector2(-3f, -3f);
            Text bossName = UiKit.OutlinedLabel(_bossBack.transform, "BossName", "화염 거인", 26, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(bossName.rectTransform);

            _bossBand = UiKit.Image(_hud, "BossBand", Art.White, new Color(0.6f, 0f, 0f, 0.75f));
            _bossBand.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            _bossBand.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            _bossBand.rectTransform.sizeDelta = new Vector2(0f, 160f);
            _bossBand.rectTransform.anchoredPosition = new Vector2(0f, 60f);
            _bossBandText = UiKit.OutlinedLabel(_bossBand.transform, "Text", "대형 화재 접근!", 84, new Color(1f, 0.9f, 0.4f), TextAnchor.MiddleCenter);
            UiKit.Stretch(_bossBandText.rectTransform);

            Text help = UiKit.OutlinedLabel(_hud, "Help", "WASD 이동 · 무기는 자동 발사 · 구슬을 모아 레벨업 · 카드는 1/2/3 또는 클릭 · 초록 원 시민에게 가면 구조      R 다시  Tab 시험판 전환", 24, new Color(0.8f, 0.8f, 0.85f), TextAnchor.LowerCenter);
            UiKit.Place(help.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(1850f, 40f));

            _flashImage = UiKit.Image(_hud, "Flash", Art.White, Color.clear);
            UiKit.Stretch(_flashImage.rectTransform);
            _flashImage.raycastTarget = false;

            _cardLayer = UiKit.Node(_hud, "Cards");
            UiKit.Stretch(_cardLayer);

            _resultBack = UiKit.Image(_hud, "ResultBack", Art.White, new Color(0f, 0f, 0f, 0.78f));
            UiKit.Place(_resultBack.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 460f));
            _result = UiKit.OutlinedLabel(_resultBack.transform, "Result", "", 46, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(_result.rectTransform, 20f);
            _resultBack.gameObject.SetActive(false);
        }

        private void RefreshHud(float dt)
        {
            int seconds = Mathf.FloorToInt(_sim.Time);
            _timer.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            _timer.color = _sim.Time >= SurvivorSim.BossAt - 10f && _sim.Boss == null && Mathf.Sin(_time * 12f) > 0f ? new Color(1f, 0.4f, 0.3f) : Color.white;
            _kills.text = "처치 " + _sim.Kills;
            _level.text = "Lv " + _sim.Level;

            float xp = Mathf.Clamp01(_sim.Xp / (float)_sim.XpToNext);
            _shownXp = dt <= 0f ? xp : Mathf.Lerp(_shownXp, xp, 1f - Mathf.Exp(-14f * dt));
            if (xp < _shownXp - 0.3f) _shownXp = xp;
            _xpFill.rectTransform.localScale = new Vector3(_shownXp, 1f, 1f);
            _xpBack.sizeDelta = new Vector2(0f, 26f + (10f * _xpPunch));
            _xpFill.color = Color.Lerp(new Color(0.3f, 0.65f, 1f), new Color(0.75f, 0.95f, 1f), _xpPunch);

            float hp = Mathf.Clamp01(_sim.Hp / _sim.MaxHp);
            _hpFill.rectTransform.localScale = new Vector3(hp, 1f, 1f);
            _hpText.text = Mathf.CeilToInt(_sim.Hp) + " / " + Mathf.RoundToInt(_sim.MaxHp);

            var build = new System.Text.StringBuilder();
            foreach (UpgradeId id in _sim.Build.Owned())
            {
                int lv = _sim.Build.Level(id);
                build.Append(SurvivorUpgrades.Name(id)).Append(id == UpgradeId.Cannon ? "  진화" : lv >= Loadout.MaxLevel ? "  MAX" : "  Lv" + lv).Append('\n');
            }
            _build.text = build.ToString();

            // 가장자리: 맞으면 붉게, 체력 30% 아래면 심장처럼 뛴다. 보스가 있으면 늘 은은히 붉다.
            float danger = _hurt * 0.6f;
            if (hp < 0.3f && _sim.Outcome == SOutcome.Playing) danger = Mathf.Max(danger, 0.35f + (0.25f * Mathf.Max(0f, Mathf.Sin(_time * 7f))));
            if (_sim.Boss != null && !_sim.Boss.Dead) danger = Mathf.Max(danger, 0.25f);
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

            bool bossAlive = _sim.Boss != null && !_sim.Boss.Dead;
            _bossBack.gameObject.SetActive(bossAlive);
            if (bossAlive) _bossFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(_sim.Boss.Hp / _sim.Boss.MaxHp), 1f, 1f);

            bool band = _bossBannerAge < 2.8f;
            _bossBand.gameObject.SetActive(band);
            if (band)
            {
                float a = _bossBannerAge < 2.3f ? 1f : 1f - ((_bossBannerAge - 2.3f) / 0.5f);
                _bossBand.color = new Color(0.6f, 0f, 0f, 0.75f * a * (0.8f + (0.2f * Mathf.Sin(_time * 20f))));
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
                string head = _sim.Outcome == SOutcome.Won ? "승리! 화염 거인을 껐다" : "쓰러졌다";
                _result.text = head + "\n\n" + _timer.text + " 버팀   처치 " + _sim.Kills + "   Lv " + _sim.Level + "   구조 " + _sim.Rescued + "\n\n" + (_overAge > 1f ? "클릭하면 다른 판" : "");
                float pop = _overAge < 0.2f ? Mathf.Lerp(0.6f, 1f, _overAge / 0.2f) : 1f;
                _resultBack.rectTransform.localScale = Vector3.one * pop;
            }
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

            Text title = UiKit.OutlinedLabel(_cardLayer, "Title", "레벨 업!", 80, new Color(1f, 0.9f, 0.4f), TextAnchor.MiddleCenter);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 360f), new Vector2(900f, 110f));
            _cards.Add(title.rectTransform);

            for (int i = 0; i < choices.Count; i++)
            {
                int index = i;
                UpgradeId id = choices[i];
                int next = _sim.Build.Level(id) + 1;
                string sprite = id == UpgradeId.Cannon ? "UI/button_yellow" : id == UpgradeId.Heal ? "UI/button_green" : Loadout.IsWeapon(id) ? "UI/button_red" : "UI/button_blue";
                Button card = UiKit.Button(_cardLayer, "Card" + i, Art.Get(sprite), "", 0, () =>
                {
                    if (_cardAge > 0.35f) Choose(index);
                });
                RectTransform rect = card.GetComponent<RectTransform>();
                float x = (i - ((choices.Count - 1) / 2f)) * 470f;
                UiKit.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(x, -20f), new Vector2(430f, 520f));

                string kind = id == UpgradeId.Cannon ? "진화" : id == UpgradeId.Heal ? "회복" : Loadout.IsWeapon(id) ? "무기" : "보조";
                string tag = id == UpgradeId.Cannon || id == UpgradeId.Heal ? "" : next <= 1 ? "새로 얻음!" : "Lv " + next;

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
        }

        /// <summary>카드가 하나씩 튀어 오른다(0.07초 간격, 살짝 넘쳤다 돌아옴).</summary>
        private void AnimateCards()
        {
            for (int i = 0; i < _rays.Count; i++)
            {
                _rays[i].localRotation = Quaternion.Euler(0f, 0f, (_time * 25f) + (i * 45f));
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
