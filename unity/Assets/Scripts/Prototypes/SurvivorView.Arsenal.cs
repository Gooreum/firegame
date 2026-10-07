using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using FireGame.UnityLayer;
using FireGame.UnityLayer.Feel;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 새 무기 9종의 그림(2026-10-07): 무기마다 움직임이 다르게 보인다 — 달리는 개, 튕기는 풍선, 돌아오는 소화기,
    /// 바닥의 지뢰·맨홀, 굴러가는 거품, 떠오르는 방울, 뻗는 사다리, 휘두르는 호스. 레벨은 개수·크기·갈래로 보인다.
    /// 규칙은 SurvivorSim(SurvivorWeapons)이 정하고 여기선 읽기만 한다.
    /// </summary>
    public sealed partial class SurvivorView
    {
        private static readonly Color DogTint = new Color(1f, 0.85f, 0.5f, 1f);
        private static readonly Color BalloonTint = new Color(0.35f, 0.6f, 1f, 1f);
        private static readonly Color PowderTint = new Color(0.95f, 0.97f, 1f, 1f);
        private static readonly Color IceTint = new Color(0.6f, 0.92f, 1f, 1f);
        private static readonly Color FoamTint = new Color(1f, 1f, 1f, 1f);
        private static readonly Color BubbleTint = new Color(0.85f, 0.75f, 1f, 1f);
        private static readonly Color GeyserTint = new Color(0.55f, 0.85f, 1f, 1f);
        private static readonly Color LadderTint = new Color(1f, 0.82f, 0.2f, 1f);

        /// <summary>바닥에 눕는 그림(지뢰·맨홀·얼음 길), 머리 방향으로 도는 그림(개·소화기·사다리), 떠 있는 그림(풍선·방울).</summary>
        private Pool _gearGround;
        private Pool _gear;
        private Pool _gearGlow;
        private Pool _gearRing;

        private static Sprite _dogSprite;
        private static Sprite _balloonSprite;
        private static Sprite _extinguisherSprite;
        private static Sprite _mineSprite;
        private static Sprite _manholeSprite;
        private static Sprite _ladderSprite;
        private static Sprite _iceSprite;
        private static Sprite _foamSprite;

        /// <summary>막 벽에 튕긴 풍선(그림용: 잠깐 납작해진다).</summary>
        private readonly Dictionary<WaterBalloon, float> _squash = new Dictionary<WaterBalloon, float>();
        private float _barkClock;

        private void BuildArsenalPools()
        {
            _gearGround = new Pool(_world, "KitGround", MineSprite(), 4, null);
            _pools.Add(_gearGround);
            _gear = new Pool(_world, "Kit", DogSprite(), 9, null);
            _pools.Add(_gear);
            _gearGlow = AddPool("KitGlow", "Effects/glow", 8, true);
            _gearRing = new Pool(_world, "KitRing", RingSprite(), 8, Additive);
            _pools.Add(_gearRing);
        }

        private void ResetArsenal()
        {
            _squash.Clear();
            _barkClock = 0f;
        }

        /// <summary>갇힌 몹이 방울 속에서 떠오른 높이(칸).</summary>
        private static float CaptureLift(Enemy e)
        {
            float t = 1f - Mathf.Clamp01(e.Captured / SurvivorSim.BubbleHold);
            return 0.4f + (1.4f * Mathf.Sin(Mathf.Min(1f, t * 1.3f) * Mathf.PI * 0.5f));
        }

        // ------------------------------------------------------------------
        // 신호
        // ------------------------------------------------------------------

        private void ReactArsenal()
        {
            foreach (Vec2 p in _sim.DogBites)
            {
                Vector3 at = W(p);
                Burst(at + Up(0.3f), 5, DogTint, 4f);
                if (Random.value < 0.25f) SpawnText(at + Up(1.2f), "왕!", DogTint, 1f);
            }
            foreach (Vec2 p in _sim.BalloonSplashes)
            {
                Vector3 at = W(p);
                Splash(at, 8, 0.7f);
                Shockwave(at, BalloonTint, SurvivorSim.BalloonSplash * 2.4f, 0.25f);
                AddWet(at, SurvivorSim.BalloonSplash * 1.6f, 2f, false);
                WeaponSound(Cue.SprayWater);
                foreach (WaterBalloon b in _sim.Balloons)
                {
                    if (b.Pos.DistanceTo(p) < 0.6f) _squash[b] = 0f;
                }
            }
            foreach (Vec2 p in _sim.MineFreezes)
            {
                // 얼어붙는 순간: 하늘색 충격파 + 얼음 조각이 튄다.
                Vector3 at = W(p);
                Shockwave(at, IceTint, 3.2f, 0.3f);
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI / 4f;
                    EmitSprite(IceSprite(), at + Up(0.2f), new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Random.Range(2f, 4f), 4f, 0.4f, 0.35f, 0.1f, new Color(0.8f, 0.97f, 1f, 1f), new Color(0.8f, 0.97f, 1f, 0f), Random.Range(-300f, 300f), false);
                }
                WeaponSound(Cue.PutOut);
            }
            foreach (Vec2 p in _sim.FoamBursts)
            {
                Vector3 at = W(p);
                Shockwave(at, FoamTint, 5f, 0.35f);
                for (int k = 0; k < 14; k++)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    float size = Random.Range(0.5f, 1.1f);
                    EmitSprite(FoamSprite(), at + Up(0.3f), new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Random.Range(2f, 5f), 5f, 0.8f, size, size * 1.4f,
                        new Color(1f, 1f, 1f, 0.95f), new Color(1f, 1f, 1f, 0f), Random.Range(-40f, 40f), false);
                }
                AddWet(at, 3f, 3f, false);
                WeaponSound(Cue.SprayFoam);
            }
            foreach (Vec2 p in _sim.BubblePops)
            {
                Vector3 at = W(p) + Up(1.6f);
                Sparkle(at, 10, BubbleTint);
                Shockwave(W(p), new Color(0.85f, 0.8f, 1f, 0.8f), 2.5f, 0.25f);
                for (int k = 0; k < 12; k++)
                {
                    EmitFalling("Effects/water_drop", at, new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(-0.5f, 1.5f), 0f), 0.55f, 0.24f, new Color(0.8f, 0.95f, 1f, 1f));
                }
                AddWet(W(p), 1.6f, 2f, false);
                WeaponSound(Cue.SprayWater);
            }
            foreach (Vec2 p in _sim.GeyserBursts)
            {
                // 물기둥: 뚜껑이 튀고, 하얀 기둥이 솟아 사방으로 쏟아진다.
                Vector3 at = W(p);
                Pillar(at, GeyserTint);
                SteamPillar(at, 1.2f);
                Shockwave(at, GeyserTint, 4.5f, 0.35f);
                EmitSprite(ManholeSprite(), at + Up(0.2f), new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f), 1f, 0.7f, 0.9f, 0.9f, Color.white, new Color(1f, 1f, 1f, 0f), Random.Range(-500f, 500f), false);
                for (int k = 0; k < 18; k++)
                {
                    float a = k * Mathf.PI * 2f / 18f;
                    EmitFalling("Effects/water_drop", at + Up(2.4f), new Vector3(Mathf.Cos(a) * Random.Range(2f, 4f), Mathf.Sin(a) * Random.Range(2f, 4f), 0f), 0.7f, 0.3f, new Color(0.8f, 0.95f, 1f, 1f));
                }
                AddWet(at, 2.6f, 3f, true);
                _trauma = Mathf.Min(1f, _trauma + 0.08f);
                WeaponSound(Cue.SprayFoam);
            }
            foreach (Ladder l in _sim.LadderStrikes)
            {
                // 사다리가 내려찍힌다: 끝에 쿵, 줄을 따라 물보라와 먼지.
                Vector3 tip = W(l.Tip);
                Shockwave(tip, LadderTint, 3.2f, 0.3f);
                for (int k = 1; k <= 5; k++)
                {
                    var p = new Vec2(l.From.X + (l.Dir.X * l.Len * k / 5f), l.From.Y + (l.Dir.Y * l.Len * k / 5f));
                    Splash(W(p), 3, 0.4f);
                }
                _trauma = Mathf.Min(1f, _trauma + 0.12f);
                HitStop(0.02f);
                WeaponSound(Cue.PutOut);
            }
            if (_sim.JustAvalanche)
            {
                ShowAlert("거품 산사태!", FoamTint);
                GameAudio.Play(Cue.SprayFoam);
            }
        }

        // ------------------------------------------------------------------
        // 그리기
        // ------------------------------------------------------------------

        private void DrawArsenal(float dt)
        {
            DrawManholes();
            DrawMines();
            DrawDogs(dt);
            DrawWhip();
            DrawLadders();
            DrawBalloons(dt);
            DrawBoomerangs();
            DrawFoam();
            DrawBubbles();
            DrawCaptives();
        }

        private void DrawDogs(float dt)
        {
            _barkClock -= dt;
            bool gold = _sim.Build.Level(UpgradeId.Crown) > 0;
            for (int i = 0; i < _sim.Dogs.Count; i++)
            {
                Dog d = _sim.Dogs[i];
                Vector3 at = W(d.Pos);
                float heading = Mathf.Atan2(d.Facing.Y, d.Facing.X) * Mathf.Rad2Deg;
                // 달리면 몸이 통통 튀고 앞뒤로 출렁인다(다리 대신 몸짓).
                float run = Mathf.Abs(Mathf.Sin((_time * 16f) + (i * 1.3f)));
                _shadows.Put(at + new Vector3(0f, -0.15f, 0f), 1.1f, 0f, new Color(0f, 0f, 0f, 0.3f), null, 0.5f);
                _gear.Put(at + Up(0.25f + (0.12f * run)), 1.8f * (gold ? 1.1f : 1f), heading, gold ? new Color(1f, 0.93f, 0.75f) : Color.white, DogSprite(), 1f - (0.08f * run));
                if (gold) _gearGlow.Put(at, 1.6f, 0f, new Color(1f, 0.85f, 0.3f, 0.35f));
                // 짖기: 타는 건물 문 앞이면 가끔 "멍!"과 물방울.
                if (d.Barking != null && d.Barking.Burning && d.Pos.DistanceTo(d.Barking.Door) < 0.9f)
                {
                    if (Random.value < 0.4f) EmitFalling("Effects/water_drop", at + Up(0.6f), new Vector3(Random.Range(-1f, 1f), 1.5f, 0f), 0.4f, 0.2f, new Color(0.75f, 0.93f, 1f, 1f));
                    if (_barkClock <= 0f)
                    {
                        _barkClock = 0.9f;
                        SpawnText(at + Up(1.3f), d.Barking.Residents > 0 && gold ? "구조!" : "멍!", DogTint, 1f);
                    }
                }
            }
        }

        private void DrawWhip()
        {
            int lv = _sim.Build.PowerOf(UpgradeId.Whip);
            if (lv > 0 && _sim.Outcome == SOutcome.Playing)
            {
                bool whirl = _sim.Build.Level(UpgradeId.Whirl) > 0;
                int arms = SurvivorSim.WhipArms[lv];
                float r = _sim.WhipRadius;
                Vector3 me = W(_sim.Player) + Up(0.6f);
                for (int k = 0; k < arms; k++)
                {
                    float arm = _sim.WhipAngle + (k * Mathf.PI * 2f / arms);
                    // 호스가 손에서 끝까지 휘어 나가고, 끝 뒤로 물 꼬리가 호를 그린다.
                    Vector3 tip = me + (new Vector3(Mathf.Cos(arm), Mathf.Sin(arm), 0f) * r);
                    Vector3 bend = me + (new Vector3(Mathf.Cos(arm - 0.5f), Mathf.Sin(arm - 0.5f), 0f) * (r * 0.55f));
                    SmallRibbon(me, bend, 0.32f, new Color(0.85f, 0.25f, 0.2f, 1f), 0f);
                    SmallRibbon(bend, tip, 0.28f, new Color(0.85f, 0.25f, 0.2f, 1f), 0f);
                    for (int j = 0; j < 8; j++)
                    {
                        float a = arm - (j * 0.09f);
                        Vector3 p = me + (new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r);
                        _gearGlow.Put(p, (0.9f - (j * 0.08f)) * LevelScale(lv), 0f, new Color(HoseTint.r, HoseTint.g, HoseTint.b, 0.7f - (j * 0.08f)));
                    }
                    if (Random.value < 0.5f) Emit("Effects/water_drop", tip, new Vector3(-Mathf.Sin(arm), Mathf.Cos(arm), 0f) * 4f, 4f, 0.3f, 0.22f, 0.05f, HoseTint, new Color(HoseTint.r, HoseTint.g, HoseTint.b, 0f), 0f);
                    if (whirl) _gearRing.Put(tip, 1f, _time * 400f, new Color(0.6f, 0.9f, 1f, 0.6f));
                }
            }
            foreach (WhirlMark m in _sim.WhirlMarks)
            {
                float life = Mathf.Clamp01(m.Life / SurvivorSim.WhirlLife);
                _gearRing.Put(W(m.Pos), 1.4f, (_time * 300f) + (m.Pos.X * 40f), new Color(0.55f, 0.88f, 1f, 0.55f * life));
            }
        }

        private void DrawLadders()
        {
            bool bridge = _sim.Build.Level(UpgradeId.Surge) > 0;
            foreach (Ladder l in _sim.Ladders)
            {
                // 뻗는 동안 길어지고(0.25초), 다 뻗으면 땅에 누워 있다(다리면 남아서 반짝인다).
                float grow = Mathf.Clamp01(l.Age / SurvivorSim.LadderReach);
                float len = l.Len * grow;
                Vector3 from = W(l.From);
                Vector3 dir = new Vector3(l.Dir.X, l.Dir.Y, 0f);
                Vector3 mid = from + (dir * (len * 0.5f)) + Up(grow < 1f ? 1.2f * (1f - grow) : 0.05f);
                float deg = (Mathf.Atan2(l.Dir.Y, l.Dir.X) * Mathf.Rad2Deg) - 90f;
                float fade = bridge ? Mathf.Clamp01((l.Life - l.Age) / 0.6f) : Mathf.Clamp01((l.Life - l.Age) / 0.3f);
                _shadows.Put(from + (dir * (len * 0.5f)), 0.9f, deg, new Color(0f, 0f, 0f, 0.25f * fade), null, Mathf.Max(0.1f, len / 0.9f));
                _gear.Put(mid, 0.9f, deg, new Color(1f, 1f, 1f, fade), LadderSprite(), Mathf.Max(0.1f, len / 0.9f));
                if (bridge && l.Struck && Random.value < 0.3f) Sparkle(from + (dir * Random.Range(0f, len)), 1, LadderTint);
            }
        }

        private void DrawBalloons(float dt)
        {
            foreach (WaterBalloon b in _sim.Balloons)
            {
                Vector3 at = W(b.Pos);
                float squash = 0f;
                if (_squash.TryGetValue(b, out float age))
                {
                    age += dt;
                    _squash[b] = age;
                    squash = Mathf.Clamp01(1f - (age / 0.15f));
                }
                float size = (b.Small ? 0.65f : 1.1f) * (1f + (0.35f * squash));
                float bob = 0.6f + (0.15f * Mathf.Sin((_time * 9f) + b.Pos.X));
                _shadows.Put(at, size * 0.8f, 0f, new Color(0f, 0f, 0f, 0.3f), null, 0.5f);
                _gear.Put(at + Up(bob), size, b.Vel.X * 8f, Color.white, BalloonSprite(), 1f - (0.45f * squash));
                if (Random.value < 0.3f) Emit("Effects/water_drop", at + Up(bob), new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0f), 3f, 0.25f, 0.16f, 0.05f, BalloonTint, new Color(BalloonTint.r, BalloonTint.g, BalloonTint.b, 0f), 0f);
            }
            if (_squash.Count > 32) _squash.Clear();
        }

        private void DrawBoomerangs()
        {
            foreach (Boomerang b in _sim.Boomerangs)
            {
                Vector3 at = W(b.Pos) + Up(0.7f);
                _shadows.Put(W(b.Pos), 0.7f, 0f, new Color(0f, 0f, 0f, 0.3f), null, 0.5f);
                _gear.Put(at, 0.9f, b.Spin * Mathf.Rad2Deg, Color.white, ExtinguisherSprite());
                // 하얀 분말 꼬리.
                Emit(Smokes[Random.Range(0, Smokes.Length)], at, new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(-0.6f, 0.6f), 0f), 2f, 0.6f, 0.4f, 1.2f, new Color(1f, 1f, 1f, 0.7f), new Color(1f, 1f, 1f, 0f), Random.Range(-60f, 60f));
            }
            foreach (Tornado t in _sim.Tornadoes)
            {
                // 하얀 회오리: 바닥 고리가 돌고, 분말이 소용돌이치며 오른다.
                Vector3 at = W(t.Pos);
                float fade = Mathf.Clamp01(t.Life / 0.6f);
                _gearRing.Put(at, SurvivorSim.TornadoRadius * 2f / 0.85f, _time * 500f, new Color(1f, 1f, 1f, 0.35f * fade));
                _gearRing.Put(at, SurvivorSim.TornadoRadius * 1.2f / 0.85f, -_time * 700f, new Color(0.9f, 0.95f, 1f, 0.5f * fade));
                for (int k = 0; k < 3; k++)
                {
                    float a = (_time * 9f) + (k * 2.1f);
                    float h = Random.Range(0.2f, 3f);
                    Vector3 p = at + (new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (0.4f + (h * 0.35f))) + Up(h);
                    Emit(Smokes[Random.Range(0, Smokes.Length)], p, new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f) * 3f, 2f, 0.5f, 0.5f, 1.1f, new Color(1f, 1f, 1f, 0.6f * fade), new Color(1f, 1f, 1f, 0f), Random.Range(-90f, 90f));
                }
            }
        }

        private void DrawMines()
        {
            bool field = _sim.Build.Level(UpgradeId.IceField) > 0;
            float blink = 0.6f + (0.4f * Mathf.Sin(_time * 8f));
            for (int i = 0; i < _sim.Mines.Count; i++)
            {
                Mine m = _sim.Mines[i];
                Vector3 at = W(m.Pos);
                _gearGround.Put(at, 0.85f, 0f, m.Arm > 0f ? new Color(0.6f, 0.7f, 0.8f, 1f) : Color.white, MineSprite());
                _gearGlow.Put(at, 0.8f, 0f, new Color(IceTint.r, IceTint.g, IceTint.b, 0.35f * blink));
                if (!field) continue;
                for (int j = i + 1; j < _sim.Mines.Count; j++)
                {
                    Vec2 a = m.Pos;
                    Vec2 b = _sim.Mines[j].Pos;
                    float len = a.DistanceTo(b);
                    if (len > SurvivorSim.IceLink) continue;
                    var mid = new Vector3((a.X + b.X) / 2f, (a.Y + b.Y) / 2f, 0f);
                    float deg = (Mathf.Atan2(b.Y - a.Y, b.X - a.X) * Mathf.Rad2Deg) - 90f;
                    _band.Put(mid, 0.8f, deg, new Color(0.7f, 0.95f, 1f, 0.55f), null, len / 0.8f);
                    _band.Put(mid, 0.3f, deg, new Color(1f, 1f, 1f, 0.6f + (0.3f * blink)), null, len / 0.3f);
                }
            }
        }

        private void DrawFoam()
        {
            foreach (FoamBall f in _sim.FoamBalls)
            {
                // 굴러가는 거품 구름: 반경만큼 거품 방울이 뭉쳐 돈다.
                Vector3 at = W(f.Pos);
                _shadows.Put(at, f.R * 2f, 0f, new Color(0f, 0f, 0f, 0.25f), null, 0.5f);
                int n = 5 + Mathf.RoundToInt(f.R * 4f);
                for (int k = 0; k < n; k++)
                {
                    float a = (k * 2.4f) + (f.Age * 5f);
                    float d = f.R * 0.6f * (0.3f + (0.7f * ((k * 37) % 10 / 10f)));
                    Vector3 p = at + (new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * d) + Up(f.R * 0.6f);
                    _gear.Put(p, f.R * 0.9f, a * 30f, new Color(1f, 1f, 1f, 0.95f), FoamSprite());
                }
            }
            if (_sim.AvalancheX.HasValue)
            {
                // 거품 파도: 위아래 9칸 흰 벽이 지나간다.
                float x = _sim.AvalancheX.Value;
                for (int k = -9; k <= 9; k++)
                {
                    float y = _sim.AvalancheY + k;
                    float wob = Mathf.Sin((_time * 8f) + k) * 0.3f;
                    var p = new Vector3(x + wob, y, 0f);
                    _gear.Put(p + Up(0.8f), 2.2f, k * 20f, new Color(1f, 1f, 1f, 0.97f), FoamSprite());
                    if (Random.value < 0.2f) Emit(Smokes[Random.Range(0, Smokes.Length)], p, new Vector3(-_sim.AvalancheDir * 2f, 0f, 0f), 2f, 0.5f, 0.6f, 1.4f, new Color(1f, 1f, 1f, 0.7f), new Color(1f, 1f, 1f, 0f), 0f);
                }
            }
        }

        private void DrawBubbles()
        {
            foreach (BubbleShot b in _sim.BubbleShots)
            {
                Vector3 at = W(b.Pos) + Up(0.8f);
                _gear.Put(at, 0.55f, _time * 90f, new Color(0.9f, 0.85f, 1f, 0.9f), BubbleSprite());
            }
        }

        /// <summary>언 몹 위의 얼음 덩어리, 갇힌 몹을 감싼 무지갯빛 방울.</summary>
        private void DrawCaptives()
        {
            foreach (Enemy e in _sim.Enemies)
            {
                if (e.Dead) continue;
                if (e.Frozen > 0f)
                {
                    Vector3 at = W(e.Pos);
                    float s = (e.Radius * 3f) + 0.6f;
                    float crack = e.Frozen < 0.4f ? Mathf.Sin(_time * 60f) * 0.06f : 0f;
                    _gear.Put(at + Up(0.35f) + new Vector3(crack, 0f, 0f), s, 0f, new Color(0.85f, 0.97f, 1f, 0.8f), IceSprite());
                    _gearGlow.Put(at, s * 1.2f, 0f, new Color(IceTint.r, IceTint.g, IceTint.b, 0.25f));
                }
                else if (e.Captured > 0f)
                {
                    Vector3 at = W(e.Pos) + Up(CaptureLift(e));
                    float s = (e.Radius * 3.4f) + 0.8f;
                    float hue = Mathf.Repeat(_time * 0.6f, 1f);
                    Color rim = Color.HSVToRGB(hue, 0.35f, 1f);
                    _gear.Put(at, s, _time * 40f, new Color(rim.r, rim.g, rim.b, 0.65f), BubbleSprite());
                    _gearRing.Put(at, s * 1.05f, 0f, new Color(1f, 1f, 1f, 0.35f));
                    _shadows.Put(W(e.Pos), s * 0.6f, 0f, new Color(0f, 0f, 0f, 0.2f), null, 0.5f);
                }
            }
        }

        private void DrawManholes()
        {
            if (!_sim.Build.Has(UpgradeId.Manhole)) return;
            foreach (Vec2 m in _sim.Manholes)
            {
                if (Mathf.Abs(m.X - _cameraAt.x) > _viewHalfW + 2f || Mathf.Abs(m.Y - _cameraAt.y) > _viewHalfH + 3f) continue;
                Geyser g = _sim.Geysers.Find(x => x.Pos.DistanceTo(m) < 0.1f);
                float shake = g != null ? Mathf.Sin(_time * 70f) * 0.08f : 0f;
                _gearGround.Put(W(m) + new Vector3(shake, 0f, 0f), 1f, 0f, Color.white, ManholeSprite());
            }
            foreach (Geyser g in _sim.Geysers)
            {
                // 솟기 전: 뚜껑이 들썩이고, 둘레에 물빛 고리가 조여 든다.
                float t = 1f - Mathf.Clamp01(g.Fuse / SurvivorSim.GeyserFuse);
                Vector3 at = W(g.Pos);
                _gearRing.Put(at, g.Radius * 2f * (1.4f - (0.4f * t)) / 0.85f, 0f, new Color(GeyserTint.r, GeyserTint.g, GeyserTint.b, 0.3f + (0.5f * t)));
                if (Random.value < 0.4f) Emit("Effects/water_drop", at + Up(0.2f), new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0f), 2f, 0.3f, 0.2f, 0.05f, GeyserTint, new Color(GeyserTint.r, GeyserTint.g, GeyserTint.b, 0f), 0f);
            }
        }

        // ------------------------------------------------------------------
        // 적중
        // ------------------------------------------------------------------

        /// <summary>새 무기의 맞은 자리: 무기 색 고리. 처리했으면 true.</summary>
        private bool ArsenalHit(Hit h, Vector3 at)
        {
            Loadout b = _sim.Build;
            switch (h.Source)
            {
                case HitSource.Dog: Impact(at, DogTint, 0.7f, b.PowerOf(UpgradeId.Sprinkler)); return true;
                case HitSource.Balloon: Impact(at, BalloonTint, 0.8f, b.PowerOf(UpgradeId.Balloon)); return true;
                case HitSource.Extinguisher: Impact(at, PowderTint, 0.8f, b.PowerOf(UpgradeId.Extinguisher)); return true;
                case HitSource.Mine: Impact(at, IceTint, 0.9f, b.PowerOf(UpgradeId.Mine)); return true;
                case HitSource.Foam: Impact(at, FoamTint, 0.8f, b.PowerOf(UpgradeId.Foam)); return true;
                case HitSource.Bubble: Impact(at, BubbleTint, 0.8f, b.PowerOf(UpgradeId.Bubble)); return true;
                case HitSource.Geyser: Impact(at, GeyserTint, 1f, b.PowerOf(UpgradeId.Manhole)); return true;
                case HitSource.Ladder: Impact(at, LadderTint, 0.9f, b.PowerOf(UpgradeId.Chain)); return true;
                case HitSource.Whip:
                {
                    Vector3 away = Away(h.Pos);
                    float deg = (Mathf.Atan2(away.y, away.x) * Mathf.Rad2Deg) - 90f;
                    EmitSprite(BeamSprite(), at - (away * 0.3f), away * 3f, 4f, 0.2f, 0.4f, 0.16f, HoseTint, new Color(HoseTint.r, HoseTint.g, HoseTint.b, 0f), 0f, true, 0f, 3f, deg);
                    Impact(at, HoseTint, 0.7f, b.PowerOf(UpgradeId.Whip));
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------
        // 그림(절차 스프라이트, 64px)
        // ------------------------------------------------------------------

        /// <summary>달마시안(위에서 본 모습, 머리가 +u): 흰 몸에 검은 점, 검은 귀, 꼬리.</summary>
        private static Sprite DogSprite()
        {
            if (_dogSprite != null) return _dogSprite;
            _dogSprite = PaintSprite((u, v) =>
            {
                var white = new Color32(250, 250, 248, 255);
                var black = new Color32(25, 25, 28, 255);
                if (InEllipse(u, v, 0.31f, 0.09f, 0.05f, 0.08f) || InEllipse(u, v, 0.31f, -0.09f, 0.05f, 0.08f)) return black;
                if (InEllipse(u, v, 0.27f, 0f, 0.12f, 0.1f))
                {
                    if (InEllipse(u, v, 0.39f, 0f, 0.025f, 0.03f)) return black;
                    return white;
                }
                if (InEllipse(u, v, 0.4f, 0f, 0.035f, 0.035f)) return black;
                bool body = InEllipse(u, v, -0.02f, 0f, 0.26f, 0.15f);
                bool legs = InEllipse(u, v, 0.15f, 0.15f, 0.05f, 0.05f) || InEllipse(u, v, 0.15f, -0.15f, 0.05f, 0.05f) || InEllipse(u, v, -0.18f, 0.15f, 0.05f, 0.05f) || InEllipse(u, v, -0.18f, -0.15f, 0.05f, 0.05f);
                bool tail = u < -0.26f && u > -0.42f && Mathf.Abs(v - ((u + 0.26f) * 0.4f)) < 0.025f;
                if (body)
                {
                    bool spot = InEllipse(u, v, 0.05f, 0.06f, 0.04f, 0.035f) || InEllipse(u, v, -0.1f, -0.05f, 0.05f, 0.04f) || InEllipse(u, v, -0.15f, 0.08f, 0.03f, 0.03f) || InEllipse(u, v, 0.12f, -0.07f, 0.03f, 0.025f);
                    return spot ? black : white;
                }
                if (legs) return new Color32(235, 235, 232, 255);
                if (tail) return black;
                return new Color32(0, 0, 0, 0);
            });
            return _dogSprite;
        }

        /// <summary>파란 물풍선: 둥근 몸, 위 하이라이트, 아래 묶은 매듭.</summary>
        private static Sprite BalloonSprite()
        {
            if (_balloonSprite != null) return _balloonSprite;
            _balloonSprite = PaintSprite((u, v) =>
            {
                if (InEllipse(u, v, -0.1f, 0.1f, 0.08f, 0.06f)) return new Color32(220, 240, 255, 255);
                if (InEllipse(u, v, 0f, 0.03f, 0.36f, 0.4f))
                {
                    byte shade = (byte)(200 + (int)(55 * Mathf.Clamp01(0.5f + v)));
                    return new Color32(60, (byte)(120 + (int)(60 * Mathf.Clamp01(0.5f + v))), shade, 245);
                }
                if (InEllipse(u, v, 0f, -0.42f, 0.06f, 0.05f)) return new Color32(40, 90, 200, 255);
                return new Color32(0, 0, 0, 0);
            });
            return _balloonSprite;
        }

        /// <summary>빨간 소화기(위에서): 둥근 통, 검은 손잡이와 호스.</summary>
        private static Sprite ExtinguisherSprite()
        {
            if (_extinguisherSprite != null) return _extinguisherSprite;
            _extinguisherSprite = PaintSprite((u, v) =>
            {
                if (u > 0.14f && u < 0.36f && Mathf.Abs(v - 0.12f) < 0.035f) return new Color32(30, 30, 30, 255);
                if (InEllipse(u, v, 0.12f, 0f, 0.07f, 0.12f)) return new Color32(40, 40, 40, 255);
                if (InEllipse(u, v, -0.08f, 0f, 0.22f, 0.17f))
                {
                    bool shine = InEllipse(u, v, -0.12f, 0.08f, 0.1f, 0.03f);
                    return shine ? new Color32(255, 150, 140, 255) : new Color32(215, 35, 30, 255);
                }
                return new Color32(0, 0, 0, 0);
            });
            return _extinguisherSprite;
        }

        /// <summary>액체질소 지뢰(바닥): 은빛 원판, 하늘색 고리, 가운데 불빛.</summary>
        private static Sprite MineSprite()
        {
            if (_mineSprite != null) return _mineSprite;
            _mineSprite = PaintSprite((u, v) =>
            {
                float d = Mathf.Sqrt((u * u) + (v * v));
                if (d < 0.08f) return new Color32(200, 245, 255, 255);
                if (d < 0.2f) return new Color32(90, 110, 130, 255);
                if (d < 0.27f) return new Color32(120, 220, 255, 255);
                if (d < 0.42f) return new Color32(150, 160, 175, 255);
                if (d < 0.46f) return new Color32(70, 80, 95, 255);
                return new Color32(0, 0, 0, 0);
            });
            return _mineSprite;
        }

        /// <summary>맨홀 뚜껑: 짙은 회색 원에 격자 홈.</summary>
        private static Sprite ManholeSprite()
        {
            if (_manholeSprite != null) return _manholeSprite;
            _manholeSprite = PaintSprite((u, v) =>
            {
                float d = Mathf.Sqrt((u * u) + (v * v));
                if (d > 0.45f) return new Color32(0, 0, 0, 0);
                if (d > 0.4f) return new Color32(45, 45, 50, 255);
                bool groove = Mathf.Repeat((u + v) * 10f, 1f) < 0.18f || Mathf.Repeat((u - v) * 10f, 1f) < 0.18f;
                return groove ? new Color32(55, 55, 60, 255) : new Color32(95, 95, 100, 255);
            });
            return _manholeSprite;
        }

        /// <summary>노란 사다리(세로로 길다): 양쪽 기둥과 가로대.</summary>
        private static Sprite LadderSprite()
        {
            if (_ladderSprite != null) return _ladderSprite;
            _ladderSprite = PaintSprite((u, v) =>
            {
                bool rail = Mathf.Abs(Mathf.Abs(u) - 0.33f) < 0.07f;
                bool rung = Mathf.Abs(u) < 0.33f && Mathf.Repeat(v * 4f, 1f) < 0.22f;
                if (rail) return new Color32(255, 205, 40, 255);
                if (rung) return new Color32(230, 170, 30, 255);
                return new Color32(0, 0, 0, 0);
            });
            return _ladderSprite;
        }

        /// <summary>얼음 덩어리: 반투명 하늘색 각진 덩어리와 흰 결.</summary>
        private static Sprite IceSprite()
        {
            if (_iceSprite != null) return _iceSprite;
            _iceSprite = PaintSprite((u, v) =>
            {
                float d = Mathf.Abs(u) + (Mathf.Abs(v) * 0.85f);
                if (d > 0.44f) return new Color32(0, 0, 0, 0);
                bool streak = Mathf.Abs(u - (v * 0.6f) + 0.08f) < 0.025f || Mathf.Abs(u + (v * 0.3f) - 0.15f) < 0.02f;
                if (streak) return new Color32(255, 255, 255, 230);
                if (d > 0.38f) return new Color32(170, 235, 255, 230);
                return new Color32(140, 215, 245, 170);
            });
            return _iceSprite;
        }
        /// <summary>거품 덩어리: 흰 구름 몇 송이가 뭉친 모양, 아래는 옅은 하늘색 그늘.</summary>
        private static Sprite FoamSprite()
        {
            if (_foamSprite != null) return _foamSprite;
            _foamSprite = PaintSprite((u, v) =>
            {
                bool puff = InEllipse(u, v, -0.12f, -0.05f, 0.22f, 0.2f) || InEllipse(u, v, 0.14f, -0.06f, 0.2f, 0.19f) || InEllipse(u, v, 0f, 0.12f, 0.24f, 0.22f);
                if (!puff) return new Color32(0, 0, 0, 0);
                bool shade = v < -0.12f;
                bool shine = InEllipse(u, v, -0.06f, 0.2f, 0.08f, 0.05f);
                if (shine) return new Color32(255, 255, 255, 255);
                return shade ? new Color32(205, 230, 245, 245) : new Color32(245, 250, 255, 245);
            });
            return _foamSprite;
        }
    }
}
