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

        private Pool _mobGlow;
        private Pool _mobBar;


        /// <summary>이번 프레임에 표식을 그린 집(겹쳐 그리지 않는다).</summary>
        private readonly HashSet<Structure> _marked = new HashSet<Structure>();

        /// <summary>날아가는 횃불(그림용): 어디서 → 어느 지붕, 지난 시간.</summary>
        private readonly List<Vector3> _torchFrom = new List<Vector3>();
        private readonly List<Vector3> _torchTo = new List<Vector3>();
        private readonly List<float> _torchAge = new List<float>();
        private const float TorchFlight = 0.35f;

        private void BuildMobPools()
        {
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
                    // 불쥐 → 작은 요괴: 더 작고 더 빨리 통통 튀며 한 줄로 달린다.
                    DrawYokai(at, i, hit, 0.6f, false, face, 16f);
                    MarkGoal(e.Goal);
                    break;
                }
                case EnemyKind.Goblin:
                {
                    // 도깨비 → 횃불 든 요괴: 예고만큼 횃불이 커지고 빛난다(다 차면 던진다).
                    float windup = Mathf.Clamp01(e.Phase / SurvivorSim.GoblinWindup);
                    Vector3 top = DrawYokai(at, i, hit, 0.95f, false, face, 7f);
                    Vector3 torch = at + new Vector3(face * 0.55f, 0f, 0f) + Up(0.9f + (0.25f * windup));
                    _mobGlow.Put(torch + Up(0.3f), (1f + (1.6f * windup)) * flicker, 0f, new Color(1f, 0.5f, 0.1f, 0.3f + (0.4f * windup)));
                    _yokai.Put(torch, (0.9f + (0.5f * windup)) * face, (Mathf.Sin(_time * 7f) * 8f) - (face * 20f * windup), Color.white, SkillSprite("torch"));
                    if (windup > 0.6f && Random.value < 0.3f) Burst(torch + Up(0.6f), 1, new Color(1f, 0.6f, 0.2f), 3f);
                    MarkGoal(e.Goal, windup);
                    break;
                }
                case EnemyKind.FireBalloon:
                {
                    // 불풍선 → 요괴가 매달린 불풍선: 높이 떠서 집으로, 퓨즈가 타면 빨갛게 깜빡인다.
                    float fuse = Mathf.Clamp01(e.Phase / SurvivorSim.FireBalloonFuse);
                    float bob = Mathf.Sin((_time * 3f) + i) * 0.12f;
                    Vector3 high = at + Up(1.4f + bob);
                    bool blink = fuse > 0f && Mathf.Repeat(_time * (4f + (10f * fuse)), 1f) < 0.5f;
                    _shadows.Put(at, 1.1f, 0f, new Color(0f, 0f, 0f, 0.25f), null, 0.5f);
                    DrawYokai(high, i, hit, 0.55f, false, face, 2f);
                    _yokai.Put(high + Up(0.55f), 1.9f * punch, bob * 20f, blink ? new Color(1f, 0.55f, 0.45f) : Color.white, SkillSprite("fire_balloon"));
                    if (fuse > 0f) _mobGlow.Put(at, SurvivorSim.FireBalloonBlast * 2f * (0.6f + (0.4f * fuse)), 0f, new Color(1f, 0.3f, 0.1f, 0.15f + (0.25f * fuse)));
                    MarkGoal(e.Goal, fuse);
                    break;
                }
                case EnemyKind.Bear:
                {
                    // 불곰 → 큰 뿔 요괴(엘리트): 쿵쿵 걷고 발밑에 불 고리, 머리 위 체력 막대.
                    _auras.Put(at, 3f, _time * 40f, new Color(0.6f, 0.2f, 1f, 0.45f));
                    Vector3 top = DrawYokai(at, i, hit, 2f, true, face, 5f);
                    MarkGoal(e.Goal);
                    DrawHealthBar(top + Up(0.6f), 2.2f, e.Hp / Mathf.Max(1f, e.MaxHp), new Color(0.75f, 0.35f, 1f));
                    break;
                }
                case EnemyKind.Hwama:
                {
                    // 화마 → 요괴 왕: 거대한 뿔 요괴가 왕관을 쓰고 숨 쉬듯 부푼다. 발밑이 보랏빛으로 탄다.
                    float breathe = 1f + (0.05f * Mathf.Sin(_time * 3f));
                    _groundGlow.Put(at, 7f * breathe, 0f, new Color(0.6f, 0.2f, 1f, 0.35f));
                    _mobGlow.Put(at + Up(2.5f), 7f * flicker, 0f, new Color(0.7f, 0.3f, 1f, 0.35f));
                    Vector3 top = DrawYokai(at, i, hit, 4.2f * breathe, true, face, 2.5f);
                    _yokai.Put(top + Up(0.2f), 2.2f * breathe, 0f, Color.white, SkillSprite("crown"));
                    if (Random.value < 0.3f) Emit("Effects/smoke_02", top, new Vector3(Random.Range(-1f, 1f), Random.Range(0.5f, 1.5f), 0f), 0.5f, 1.4f, 1.2f, 3f, new Color(0.35f, 0.25f, 0.45f, 0.5f), new Color(0.3f, 0.2f, 0.4f, 0f), 0f);
                    DrawHealthBar(top + Up(2f), 5f, e.Hp / Mathf.Max(1f, e.MaxHp), new Color(0.75f, 0.3f, 1f));
                    break;
                }
            }
        }

        /// <summary>
        /// 노리는 집(샘플 그대로): 지붕 위에 빨간 "!" 원이 통통 뛰고, 집 둘레가 붉게 맥박친다(예고·퓨즈가 차면 빨라진다). 한 집에 한 번만.
        /// </summary>
        private void MarkGoal(Structure goal, float urgency = 0f)
        {
            if (goal == null || goal.Collapsed || !_marked.Add(goal)) return;
            Vector3 roof = W(goal.Pos);
            float rate = 8f + (10f * urgency);
            float pulse = 0.5f + (0.5f * Mathf.Sin(_time * rate));
            float size = (Mathf.Max(goal.Half.X, goal.Half.Y) * 2.3f) + 0.6f;
            _mobGlow.Put(roof + Up(0.05f), size * (1f + (0.05f * pulse)), 0f, new Color(1f, 0.2f, 0.12f, 0.25f + (0.25f * pulse) + (0.2f * urgency)));
            float k = 1f + (0.12f * Mathf.Sin(_time * rate));
            _yokai.Put(roof + Up(1f), 1.25f * k, 0f, Color.white, SkillSprite("warn_mark"));
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
    }
}
