using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using FireGame.UnityLayer;
using FireGame.UnityLayer.Feel;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 마을(수호자)의 몹 그림(2026-10-07): 실루엣만 봐도 누가 무슨 짓을 하는지 읽힌다 — 꼬리 불 끌며 줄지어 달리는 불쥐,
    /// 머리 위 횃불을 키우는 도깨비, 바구니 단 불풍선, 등에 불을 진 불곰, 거대한 화마. 노리는 집 지붕에는 붉은 표식이 돈다.
    /// 규칙은 SurvivorSim(SurvivorMobs)이 정하고 여기선 읽기만 한다.
    /// </summary>
    public sealed partial class SurvivorView
    {
        private static readonly Color RaidRed = new Color(1f, 0.25f, 0.1f, 1f);

        /// <summary>서 있는 몹 그림(카메라를 본다).</summary>
        private Pool _mobs;
        private Pool _mobGlow;
        private Pool _mobBar;

        private static Sprite _ratSprite;
        private static Sprite _goblinSprite;
        private static Sprite _fireBalloonSprite;
        private static Sprite _bearSprite;
        private static Sprite _eyesSprite;

        /// <summary>이번 프레임에 표식을 그린 집(겹쳐 그리지 않는다).</summary>
        private readonly HashSet<Structure> _marked = new HashSet<Structure>();

        /// <summary>날아가는 횃불(그림용): 어디서 → 어느 지붕, 지난 시간.</summary>
        private readonly List<Vector3> _torchFrom = new List<Vector3>();
        private readonly List<Vector3> _torchTo = new List<Vector3>();
        private readonly List<float> _torchAge = new List<float>();
        private const float TorchFlight = 0.35f;

        private void BuildMobPools()
        {
            _mobs = new Pool(_world, "Mob", RatSprite(), 9, null);
            _mobs.Upright = true;
            _pools.Add(_mobs);
            _mobGlow = AddPool("MobGlow", "Effects/glow", 8, true);
            _mobBar = new Pool(_world, "MobBar", Art.White, 15, null);
            _mobBar.Upright = true;
            _pools.Add(_mobBar);
        }

        private void ResetMobs()
        {
            _torchFrom.Clear();
            _torchTo.Clear();
            _torchAge.Clear();
        }

        // ------------------------------------------------------------------
        // 신호
        // ------------------------------------------------------------------

        private void ReactMobs()
        {
            if (_sim.JustRaid != null)
            {
                // 습격: 붉은 띠 "습격! 북쪽에서 온다 · 빵집, 꽃집", 가장자리에 붉은 충격파, 표적 지붕마다 고리.
                Raid raid = _sim.JustRaid;
                var names = new List<string>();
                foreach (Structure t in raid.Targets) names.Add(t.Name);
                _bossBandText.text = "습격! " + Compass(raid.From) + "에서 온다 · " + string.Join(", ", names);
                _bandTint = new Color(0.6f, 0.08f, 0f);
                _bossBannerAge = 0f;
                Shockwave(W(raid.From), RaidRed, 8f, 0.6f);
                foreach (Structure t in raid.Targets) Shockwave(W(t.Pos), RaidRed, (Mathf.Max(t.Half.X, t.Half.Y) * 2.6f) + 1f, 0.5f);
                _trauma = Mathf.Min(1f, _trauma + 0.4f);
                GameAudio.Play(Cue.Critical);
            }
            for (int k = 0; k < _sim.TorchThrows.Count; k++)
            {
                _torchFrom.Add(W(_sim.TorchFrom[k]) + Up(1.6f));
                _torchTo.Add(W(_sim.TorchThrows[k].Pos) + Up(0.5f));
                _torchAge.Add(0f);
            }
            foreach (Vec2 p in _sim.FireBalloonBlasts)
            {
                Vector3 at = W(p) + Up(1f);
                Shockwave(W(p), new Color(1f, 0.45f, 0.1f), SurvivorSim.FireBalloonBlast * 2.2f, 0.45f);
                Flare(at, 5f, new Color(1f, 0.6f, 0.2f), 3);
                Burst(at, 40, new Color(1f, 0.55f, 0.15f), 10f);
                _trauma = Mathf.Min(1f, _trauma + 0.35f);
                HitStop(0.04f);
                GameAudio.Play(Cue.Backfire);
            }
            foreach (Vec2 p in _sim.BearsDown)
            {
                Vector3 at = W(p);
                var gold = new Color(1f, 0.85f, 0.3f);
                Shockwave(at, gold, 7f, 0.5f);
                Burst(at, 30, gold, 8f);
                Steam(at, 12, 1.6f);
                SpawnText(at + Up(2f), "불곰 퇴치!", gold, 1.6f);
                HitStop(0.06f);
                _zoomKick = Mathf.Max(_zoomKick, 0.4f);
            }
            if (_sim.JustBossRise && _sim.Boss != null)
            {
                // 화마가 깨어난다: 붉은 띠, 큰 흔들림, 카메라가 한 번 물러난다.
                Vector3 at = W(_sim.Boss.Pos);
                _bossBandText.text = "화마가 깨어났다! 마을 한가운데로 온다";
                _bandTint = new Color(0.5f, 0.02f, 0f);
                _bossBannerAge = 0f;
                for (int k = 0; k < 3; k++) Shockwave(at, new Color(1f, 0.3f, 0.05f), 10f + (6f * k), 0.7f, k * 0.15f);
                Burst(at, 60, new Color(1f, 0.45f, 0.1f), 12f);
                _trauma = Mathf.Min(1f, _trauma + 0.9f);
                _zoomKick = Mathf.Min(_zoomKick, -0.8f);
                GameAudio.Play(Cue.Critical);
            }
            if (_sim.JustBossDown)
            {
                Vector3 at = W(_sim.BossDownAt);
                var gold = new Color(1f, 0.85f, 0.3f);
                for (int k = 0; k < 3; k++) Shockwave(at, gold, 12f + (8f * k), 0.8f, k * 0.12f);
                SteamPillar(at, 3f);
                Steam(at, 40, 3f);
                Flash(gold, 0.35f);
                HitStop(0.12f);
                _slowmo = Mathf.Max(_slowmo, 0.8f);
                _trauma = Mathf.Min(1f, _trauma + 0.8f);
                ShowAlert("화마를 쓰러뜨렸다!", gold);
                GameAudio.Play(Cue.Won);
            }
        }

        /// <summary>소방관에서 본 at의 방위(북·남·동·서·북동…). 화면 위가 북(+Y).</summary>
        private string Compass(Vec2 at)
        {
            float dx = at.X - _sim.Player.X;
            float dy = at.Y - _sim.Player.Y;
            float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            string[] names = { "동쪽", "북동쪽", "북쪽", "북서쪽", "서쪽", "남서쪽", "남쪽", "남동쪽" };
            int k = Mathf.RoundToInt(Mathf.Repeat(a, 360f) / 45f) % 8;
            return names[k];
        }

        // ------------------------------------------------------------------
        // 그리기
        // ------------------------------------------------------------------

        /// <summary>마을 몹 한 마리(DrawEnemies의 switch에서).</summary>
        private void DrawRaider(Enemy e, Vector3 at, int i, bool hit, float punch, float flicker)
        {
            var water = new Color(0.7f, 0.95f, 1f);
            Color body = hit ? water : Color.white;
            float face = e.Goal != null && e.Goal.Pos.X < e.Pos.X ? -1f : 1f;
            // 2:00 뒤 "달아오른" 몹: 몸이 하얗게 탄다(체력은 시간 배율이 이미 올렸다).
            float hot = Mathf.Clamp01((_sim.Time - 120f) / 60f);
            Color flame = Color.Lerp(Color.white, new Color(1f, 1f, 0.85f), hot);
            switch (e.Kind)
            {
                case EnemyKind.Rat:
                {
                    // 불쥐: 작은 몸이 통통 튀고 꼬리 끝에 불이 흔들린다.
                    float hop = Mathf.Abs(Mathf.Sin((_time * 18f) + i)) * 0.12f;
                    _mobs.Put(at + Up(hop), 1.25f * face * punch, 0f, body, RatSprite());
                    _embers.Put(at + new Vector3(-face * 0.55f, 0f, 0f) + Up(0.3f + hop), 0.6f * flicker, 0f, flame, FlameArt.Frame(_emberSheet, _time, i));
                    if (e.Leader == null) DrawPath(e.Pos, e.Goal, new Color(1f, 0.4f, 0.15f, 0.35f));
                    MarkGoal(e.Goal);
                    break;
                }
                case EnemyKind.Goblin:
                {
                    // 도깨비: 뿔 달린 몸, 머리 위 횃불이 예고만큼 부푼다(다 차면 던진다).
                    float windup = Mathf.Clamp01(e.Phase / SurvivorSim.GoblinWindup);
                    float sway = Mathf.Sin((_time * 6f) + i) * 4f;
                    _mobs.Put(at, 2f * face * punch, sway, body, GoblinSprite());
                    Vector3 torch = at + new Vector3(face * 0.35f, 0f, 0f) + Up(2.1f + (0.3f * windup));
                    _mobGlow.Put(torch, (1f + (1.6f * windup)) * flicker, 0f, new Color(1f, 0.5f, 0.1f, 0.3f + (0.4f * windup)));
                    _embers.Put(torch, (0.45f + (0.6f * windup)) * flicker, 0f, flame, FlameArt.Frame(_emberSheet, _time, i + 2));
                    if (windup > 0.6f && Random.value < 0.3f) Burst(torch, 1, new Color(1f, 0.6f, 0.2f), 3f);
                    MarkGoal(e.Goal, windup);
                    break;
                }
                case EnemyKind.FireBalloon:
                {
                    // 불풍선: 붉은 풍선과 바구니가 떠 있고 밑에서 불이 흔들린다. 퓨즈가 타면 빨갛게 깜빡인다.
                    float fuse = Mathf.Clamp01(e.Phase / SurvivorSim.FireBalloonFuse);
                    float bob = Mathf.Sin((_time * 3f) + i) * 0.12f;
                    Vector3 high = at + Up(1.6f + bob);
                    _shadows.Put(at, 1.1f, 0f, new Color(0f, 0f, 0f, 0.25f), null, 0.5f);
                    bool blink = fuse > 0f && Mathf.Repeat(_time * (4f + (10f * fuse)), 1f) < 0.5f;
                    _mobs.Put(high, 1.5f * punch, bob * 20f, hit ? water : blink ? new Color(1f, 0.55f, 0.45f) : Color.white, FireBalloonSprite());
                    _embers.Put(high + Up(-0.15f), 0.5f * flicker, 0f, flame, FlameArt.Frame(_emberSheet, _time, i + 4));
                    if (fuse > 0f) _mobGlow.Put(at, SurvivorSim.FireBalloonBlast * 2f * (0.6f + (0.4f * fuse)), 0f, new Color(1f, 0.3f, 0.1f, 0.15f + (0.25f * fuse)));
                    if (e.Goal != null) DrawPath(e.Pos, e.Goal, new Color(1f, 0.5f, 0.2f, 0.25f));
                    MarkGoal(e.Goal, fuse);
                    break;
                }
                case EnemyKind.Bear:
                {
                    // 불곰: 큰 덩치, 등에 큰 불이 타고, 지나간 자리는 그을린다.
                    float step = Mathf.Sin((_time * 8f) + i) * 0.06f;
                    _shadows.Put(at, 2.6f, 0f, new Color(0f, 0f, 0f, 0.4f), null, 0.5f);
                    _mobs.Put(at + Up(step), 3.4f * face * punch, step * 30f, body, BearSprite());
                    _mobGlow.Put(at + Up(1.2f), 3.2f * flicker, 0f, new Color(1f, 0.35f, 0.08f, 0.35f));
                    _blazes.Put(at + new Vector3(-face * 0.3f, 0f, 0f) + Up(1.7f), 1.5f * flicker * punch, 0f, flame, FlameArt.Frame(_blazeSheet, _time, i));
                    _auras.Put(at, 3f, _time * 40f, new Color(1f, 0.4f, 0.1f, 0.45f));
                    DrawPath(e.Pos, e.Goal, new Color(1f, 0.3f, 0.1f, 0.4f));
                    MarkGoal(e.Goal);
                    DrawHealthBar(at + Up(3.6f), 2.2f, e.Hp / Mathf.Max(1f, e.MaxHp), new Color(1f, 0.45f, 0.1f));
                    break;
                }
                case EnemyKind.Hwama:
                {
                    // 화마: 거대한 불 기둥 셋과 검은 눈. 숨 쉬듯 부풀고, 발밑이 붉게 탄다.
                    float breathe = 1f + (0.06f * Mathf.Sin(_time * 3f));
                    _shadows.Put(at, 5f, 0f, new Color(0f, 0f, 0f, 0.45f), null, 0.5f);
                    _groundGlow.Put(at, 7f * breathe, 0f, new Color(1f, 0.25f, 0.05f, 0.4f));
                    _mobGlow.Put(at + Up(2.5f), 8f * flicker, 0f, new Color(1f, 0.35f, 0.08f, 0.45f));
                    _blazes.Put(at + new Vector3(-1f, 0f, 0f), 3.2f * breathe * punch, 0f, flame, FlameArt.Frame(_blazeSheet, _time, i));
                    _blazes.Put(at + new Vector3(1f, 0f, 0f), 3.2f * breathe * punch, 0f, flame, FlameArt.Frame(_blazeSheet, _time, i + 3));
                    _blazes.Put(at + new Vector3(0f, 0.2f, 0f), 4.4f * breathe * punch, 0f, hit ? water : flame, FlameArt.Frame(_blazeSheet, _time, i + 6));
                    _mobs.Put(at + Up(2.6f * breathe), 1.8f * face, 0f, Color.white, EyesSprite());
                    if (Random.value < 0.4f) Emit("Effects/smoke_02", at + Up(4f), new Vector3(Random.Range(-1f, 1f), Random.Range(0.5f, 1.5f), 0f), 0.5f, 1.4f, 1.2f, 3f, new Color(0.25f, 0.2f, 0.2f, 0.5f), new Color(0.2f, 0.2f, 0.2f, 0f), 0f);
                    DrawHealthBar(at + Up(5.6f), 5f, e.Hp / Mathf.Max(1f, e.MaxHp), new Color(1f, 0.2f, 0.05f));
                    break;
                }
            }
        }

        /// <summary>노리는 집 지붕에 붉은 표식(예고·퓨즈가 차면 조여 들며 진해진다). 한 집에 한 번만.</summary>
        private void MarkGoal(Structure goal, float urgency = 0f)
        {
            if (goal == null || goal.Collapsed || !_marked.Add(goal)) return;
            Vector3 roof = W(goal.Pos) + Up(0.05f);
            float size = Mathf.Max(goal.Half.X, goal.Half.Y) * 2.4f * (1.15f - (0.25f * urgency));
            _reticle.Put(roof, size * (1f + (0.06f * Mathf.Sin(_time * 8f))), _time * 80f, new Color(1f, 0.3f, 0.12f, 0.35f + (0.45f * urgency)));
        }

        /// <summary>몹에서 노리는 집까지 점선(어디로 가는지).</summary>
        private void DrawPath(Vec2 from, Structure goal, Color c)
        {
            if (goal == null) return;
            float len = from.DistanceTo(goal.Pos);
            int n = Mathf.Min(14, (int)(len / 1.2f));
            float drift = Mathf.Repeat(_time * 2f, 1f);
            for (int k = 1; k <= n; k++)
            {
                float t = (k - drift) / (n + 1f);
                var p = new Vector3(Mathf.Lerp(from.X, goal.Pos.X, t), Mathf.Lerp(from.Y, goal.Pos.Y, t), 0f);
                _mobGlow.Put(p, 0.45f, 0f, c);
            }
        }

        /// <summary>몸 위 체력 막대(검은 바탕 + 색 막대).</summary>
        private void DrawHealthBar(Vector3 at, float width, float fill, Color c)
        {
            fill = Mathf.Clamp01(fill);
            _mobBar.Put(at, width + 0.1f, 0f, new Color(0f, 0f, 0f, 0.7f), null, 0.32f / (width + 0.1f));
            if (fill <= 0f) return;
            Vector3 left = at + new Vector3(-width * 0.5f * (1f - fill), 0f, -0.01f);
            _mobBar.Put(left, width * fill, 0f, c, null, 0.2f / Mathf.Max(0.01f, width * fill));
        }

        /// <summary>마을 몹 그림 매 프레임 끝: 날아가는 횃불, 표식 초기화. 화면 밖 곰·화마·풍선은 가장자리 화살표.</summary>
        private void DrawMobsAfter(float dt)
        {
            _marked.Clear();
            for (int k = _torchAge.Count - 1; k >= 0; k--)
            {
                _torchAge[k] += dt;
                float t = Mathf.Clamp01(_torchAge[k] / TorchFlight);
                Vector3 p = Vector3.Lerp(_torchFrom[k], _torchTo[k], t) + Up(Mathf.Sin(t * Mathf.PI) * 1.5f);
                _embers.Put(p, 0.7f, t * 720f, Color.white, FlameArt.Frame(_emberSheet, _time, k));
                _mobGlow.Put(p, 1.4f, 0f, new Color(1f, 0.5f, 0.1f, 0.5f));
                if (t >= 1f)
                {
                    Shockwave(_torchTo[k], new Color(1f, 0.45f, 0.1f), 3.5f, 0.3f);
                    Burst(_torchTo[k], 16, new Color(1f, 0.55f, 0.15f), 6f);
                    _torchFrom.RemoveAt(k);
                    _torchTo.RemoveAt(k);
                    _torchAge.RemoveAt(k);
                }
            }
        }

        private void RaiderArrows()
        {
            foreach (Enemy e in _sim.Enemies)
            {
                if (e.Dead) continue;
                if (e.Kind == EnemyKind.Bear) EdgeArrow(new Vector3(e.Pos.X, e.Pos.Y, 0f), RaidRed, 1.7f, "불곰");
                else if (e.Kind == EnemyKind.Hwama) EdgeArrow(new Vector3(e.Pos.X, e.Pos.Y, 0f), new Color(1f, 0.2f, 0.05f), 2.2f, "화마");
                else if (e.Kind == EnemyKind.FireBalloon) EdgeArrow(new Vector3(e.Pos.X, e.Pos.Y, 0f), new Color(1f, 0.6f, 0.3f), 1.3f, "불풍선");
            }
        }

        // ------------------------------------------------------------------
        // 그림(절차 스프라이트, 64px, 서 있는 모습)
        // ------------------------------------------------------------------

        /// <summary>불쥐(옆모습, 머리가 +u): 짙은 붉은 몸, 큰 귀, 노란 눈, 가는 꼬리.</summary>
        private static Sprite RatSprite()
        {
            if (_ratSprite != null) return _ratSprite;
            _ratSprite = PaintSprite((u, v) =>
            {
                var body = new Color32(150, 35, 25, 255);
                if (InEllipse(u, v, 0.26f, -0.02f, 0.025f, 0.025f)) return new Color32(255, 220, 60, 255);
                if (InEllipse(u, v, 0.15f, 0.09f, 0.07f, 0.08f)) return new Color32(200, 70, 55, 255);
                if (InEllipse(u, v, 0.22f, -0.05f, 0.13f, 0.09f)) return body;
                if (InEllipse(u, v, 0.36f, -0.08f, 0.03f, 0.025f)) return new Color32(255, 120, 110, 255);
                if (InEllipse(u, v, -0.04f, -0.1f, 0.22f, 0.13f)) return body;
                if (u < -0.24f && u > -0.44f && Mathf.Abs(v + 0.12f - ((u + 0.24f) * -0.6f)) < 0.02f) return new Color32(120, 30, 20, 255);
                if ((InEllipse(u, v, 0.1f, -0.25f, 0.03f, 0.05f) || InEllipse(u, v, -0.15f, -0.25f, 0.03f, 0.05f))) return new Color32(90, 20, 15, 255);
                return new Color32(0, 0, 0, 0);
            });
            return _ratSprite;
        }

        /// <summary>횃불 도깨비(앞모습): 주황 몸, 흰 뿔 둘, 큰 노란 눈, 횃불 든 팔.</summary>
        private static Sprite GoblinSprite()
        {
            if (_goblinSprite != null) return _goblinSprite;
            _goblinSprite = PaintSprite((u, v) =>
            {
                var skin = new Color32(225, 90, 40, 255);
                if (Mathf.Abs(u) > 0.07f && Mathf.Abs(u) < 0.13f && v > 0.22f && v < 0.36f + ((0.13f - Mathf.Abs(u)) * 1.2f)) return new Color32(245, 240, 225, 255);
                if (InEllipse(u, v, -0.06f, 0.12f, 0.04f, 0.045f) || InEllipse(u, v, 0.06f, 0.12f, 0.04f, 0.045f)) return new Color32(255, 230, 60, 255);
                if (InEllipse(u, v, 0f, 0.04f, 0.07f, 0.025f)) return new Color32(70, 15, 10, 255);
                if (InEllipse(u, v, 0f, 0.1f, 0.16f, 0.15f)) return skin;
                if (InEllipse(u, v, 0f, -0.16f, 0.17f, 0.18f)) return new Color32(120, 40, 30, 255);
                if (u > 0.12f && u < 0.2f && v > -0.1f && v < 0.3f) return skin;
                if ((InEllipse(u, v, -0.08f, -0.37f, 0.05f, 0.07f) || InEllipse(u, v, 0.08f, -0.37f, 0.05f, 0.07f))) return new Color32(90, 30, 20, 255);
                return new Color32(0, 0, 0, 0);
            });
            return _goblinSprite;
        }

        /// <summary>불풍선(앞모습): 줄무늬 붉은 풍선, 줄, 갈색 바구니.</summary>
        private static Sprite FireBalloonSprite()
        {
            if (_fireBalloonSprite != null) return _fireBalloonSprite;
            _fireBalloonSprite = PaintSprite((u, v) =>
            {
                if (InEllipse(u, v, 0f, 0.12f, 0.3f, 0.32f))
                {
                    bool stripe = Mathf.Repeat((u + 0.3f) * 6f, 1f) < 0.5f;
                    bool shine = InEllipse(u, v, -0.12f, 0.24f, 0.06f, 0.08f);
                    if (shine) return new Color32(255, 220, 200, 255);
                    return stripe ? new Color32(220, 40, 30, 255) : new Color32(250, 150, 40, 255);
                }
                if (v < -0.2f && v > -0.3f && Mathf.Abs(Mathf.Abs(u) - (0.08f + ((v + 0.2f) * -0.2f))) < 0.012f) return new Color32(60, 40, 30, 255);
                if (v < -0.3f && v > -0.42f && Mathf.Abs(u) < 0.1f) return new Color32(140, 90, 45, 255);
                return new Color32(0, 0, 0, 0);
            });
            return _fireBalloonSprite;
        }

        /// <summary>불곰(옆모습, 머리가 +u): 큰 검붉은 몸, 둥근 귀, 노란 눈, 굵은 다리.</summary>
        private static Sprite BearSprite()
        {
            if (_bearSprite != null) return _bearSprite;
            _bearSprite = PaintSprite((u, v) =>
            {
                var fur = new Color32(95, 30, 22, 255);
                if (InEllipse(u, v, 0.33f, 0.04f, 0.025f, 0.025f)) return new Color32(255, 210, 60, 255);
                if (InEllipse(u, v, 0.43f, -0.04f, 0.03f, 0.03f)) return new Color32(20, 10, 8, 255);
                if (InEllipse(u, v, 0.25f, 0.17f, 0.05f, 0.05f)) return fur;
                if (InEllipse(u, v, 0.32f, 0.02f, 0.14f, 0.12f)) return fur;
                if (InEllipse(u, v, 0.38f, -0.04f, 0.07f, 0.05f)) return new Color32(140, 55, 40, 255);
                if (InEllipse(u, v, -0.04f, -0.02f, 0.3f, 0.2f)) return fur;
                bool leg = (u > 0.08f && u < 0.18f || u > -0.26f && u < -0.14f) && v < -0.12f && v > -0.38f;
                if (leg) return new Color32(70, 22, 16, 255);
                return new Color32(0, 0, 0, 0);
            });
            return _bearSprite;
        }

        /// <summary>화마의 눈: 검은 바탕에 노랗게 타는 두 눈과 찢어진 입.</summary>
        private static Sprite EyesSprite()
        {
            if (_eyesSprite != null) return _eyesSprite;
            _eyesSprite = PaintSprite((u, v) =>
            {
                bool eye = InEllipse(u, v, -0.17f, 0.08f, 0.1f, 0.06f) || InEllipse(u, v, 0.17f, 0.08f, 0.1f, 0.06f);
                bool pupil = InEllipse(u, v, -0.15f, 0.07f, 0.035f, 0.035f) || InEllipse(u, v, 0.15f, 0.07f, 0.035f, 0.035f);
                if (pupil) return new Color32(255, 255, 220, 255);
                if (eye) return new Color32(255, 200, 30, 255);
                bool mouth = Mathf.Abs(v + 0.14f + (0.08f * Mathf.Cos(u * 9f))) < 0.025f && Mathf.Abs(u) < 0.26f;
                if (mouth) return new Color32(30, 8, 5, 255);
                return new Color32(0, 0, 0, 0);
            });
            return _eyesSprite;
        }
    }
}
