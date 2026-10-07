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
        private static readonly Color SprayTint = new Color(0.6f, 0.85f, 1f, 1f);
        private static readonly Color BalloonTint = new Color(0.35f, 0.6f, 1f, 1f);
        private static readonly Color PowderTint = new Color(0.95f, 0.97f, 1f, 1f);
        private static readonly Color IceTint = new Color(0.6f, 0.92f, 1f, 1f);
        private static readonly Color FoamTint = new Color(1f, 1f, 1f, 1f);
        private static readonly Color BubbleTint = new Color(0.85f, 0.75f, 1f, 1f);
        private static readonly Color GeyserTint = new Color(0.55f, 0.85f, 1f, 1f);
        private static readonly Color ChainTint = new Color(0.55f, 0.82f, 1f, 1f);

        /// <summary>바닥에 눕는 그림(지뢰·맨홀·얼음 길), 머리 방향으로 도는 그림(개·소화기·사다리), 떠 있는 그림(풍선·방울).</summary>
        private Pool _gearGround;
        private Pool _gear;
        private Pool _gearGlow;
        private Pool _gearRing;


        /// <summary>막 벽에 튕긴 풍선(그림용: 잠깐 납작해진다).</summary>
        private readonly Dictionary<WaterBalloon, float> _squash = new Dictionary<WaterBalloon, float>();
        private float _barkClock;

        private void BuildArsenalPools()
        {
            _gearGround = new Pool(_world, "KitGround", SkillSprite("mine_off"), 4, null);
            _pools.Add(_gearGround);
            _gear = new Pool(_world, "Kit", SkillSprite("sprinkler_base"), 9, null);
            _pools.Add(_gear);
            _gearGlow = AddPool("KitGlow", "Effects/glow", 8, true);
            _gearRing = new Pool(_world, "KitRing", RingSprite(), 8, Additive);
            _pools.Add(_gearRing);
        }

        private void ResetArsenal()
        {
            _gearFlash.Clear();
            _flung.Clear();
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
            foreach (Vec2 p in _sim.SprinklerHits)
            {
                Vector3 at = W(p);
                Splash(at, 4, 0.45f);
                Shockwave(at, SprayTint, 1.2f, 0.2f);
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
                    EmitSprite(SkillSprite("ice_shard"), at + Up(0.2f), new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Random.Range(2f, 4f), 4f, 0.4f, 0.35f, 0.1f, new Color(0.8f, 0.97f, 1f, 1f), new Color(0.8f, 0.97f, 1f, 0f), Random.Range(-300f, 300f), false);
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
                    EmitSprite(SkillSprite("foam_puff"), at + Up(0.3f), new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Random.Range(2f, 5f), 5f, 0.8f, size, size * 1.4f,
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
                EmitSprite(SkillSprite("manhole"), at + Up(0.2f), new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f), 1f, 0.7f, 0.9f, 0.9f, Color.white, new Color(1f, 1f, 1f, 0f), Random.Range(-500f, 500f), false);
                for (int k = 0; k < 18; k++)
                {
                    float a = k * Mathf.PI * 2f / 18f;
                    EmitFalling("Effects/water_drop", at + Up(2.4f), new Vector3(Mathf.Cos(a) * Random.Range(2f, 4f), Mathf.Sin(a) * Random.Range(2f, 4f), 0f), 0.7f, 0.3f, new Color(0.8f, 0.95f, 1f, 1f));
                }
                AddWet(at, 2.6f, 3f, true);
                _trauma = Mathf.Min(1f, _trauma + 0.08f);
                WeaponSound(Cue.SprayFoam);
            }
            foreach (ChainBolt bolt in _sim.ChainStrikes)
            {
                // 꽂힌 마디마다 물이 튀고 빛이 번쩍한다.
                for (int k = 1; k < bolt.Points.Count; k++)
                {
                    Vector3 at = W(bolt.Points[k]);
                    Splash(at, 3, 0.4f);
                    _gearFlash.Add(new ChainFlash { At = at, Age = 0f });
                }
                _trauma = Mathf.Min(1f, _trauma + 0.04f * bolt.Points.Count);
                WeaponSound(Cue.SprayWater);
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
            DrawFlung(dt);
            DrawManholes();
            DrawMines();
            DrawSprinklers();
            DrawWhip();
            DrawChains(dt);
            DrawBalloons(dt);
            DrawBoomerangs();
            DrawFoam();
            DrawBubbles();
            DrawCaptives();
        }

        private readonly List<Vec2> _sprinklerHeads = new List<Vec2>();

        /// <summary>
        /// 회전 스프링클러(샘플 그대로): 금색 머리 + 빙빙 도는 노즐 셋 + 파란 빛, 사방으로 물방울.
        /// 물 왕관: 둘레에 3겹으로 도는 물 고리 + 8갈래 빛줄기.
        /// </summary>
        private void DrawSprinklers()
        {
            int lv = _sim.Build.PowerOf(UpgradeId.Sprinkler);
            if (lv == 0) return;
            _sim.SprinklerHeads(_sprinklerHeads);
            float spin = _time * 840f;
            for (int i = 0; i < _sprinklerHeads.Count; i++)
            {
                Vector3 at = W(_sprinklerHeads[i]);
                _shadows.Put(at + new Vector3(0f, -0.2f, 0f), 0.9f, 0f, new Color(0f, 0f, 0f, 0.3f), null, 0.5f);
                int tier = TierLv(UpgradeId.Sprinkler);
                float ts = TierSize(tier);
                TierHalo(at + Up(0.35f), 0.9f * ts, tier);
                _gear.Put(at + Up(0.35f), 2.2f * ts, 0f, TierTint(Color.white, tier), SkillSprite("sprinkler_base"));
                _gear.Put(at + Up(0.36f), 1.7f * ts, spin + (i * 40f), TierTint(Color.white, tier), SkillSprite("sprinkler_arms"));
                if (Random.value < 0.55f)
                {
                    float a = (spin * Mathf.Deg2Rad) + (i * 2f) + (Random.Range(0, 3) * 2.094f);
                    EmitFalling("Effects/water_drop", at + Up(0.5f), new Vector3(Mathf.Cos(a) * 3.5f, Mathf.Sin(a) * 3.5f, 0f), 0.22f, 0.45f, new Color(0.7f, 0.9f, 1f, 1f));
                }
            }
            if (_sim.Build.Level(UpgradeId.Crown) == 0) return;

            Vector3 c = W(_sim.Player) + Up(0.2f);
            float r = SurvivorSim.SprinklerRadius[lv];
            float ring = 2f * r * 21f / 19f;
            _gearGlow.Put(c, ring, _time * 230f, new Color(0.3f, 0.6f, 1f, 0.55f), SkillSprite("arc_band"));
            _gearGlow.Put(c, ring * 0.97f, -_time * 330f, new Color(0.6f, 0.85f, 1f, 0.45f), SkillSprite("arc_band"));
            _gearGlow.Put(c, ring * 1.02f, _time * 460f + 120f, new Color(0.85f, 0.95f, 1f, 0.35f), SkillSprite("arc_band"));
            float len = SurvivorSim.CrownJetLen;
            for (int k = 0; k < SurvivorSim.CrownJets; k++)
            {
                float jet = _sim.CrownJetAngle + (k * Mathf.PI * 2f / SurvivorSim.CrownJets);
                var dir = new Vector3(Mathf.Cos(jet), Mathf.Sin(jet), 0f);
                Vector3 mid = c + (dir * (r + (len * 0.5f)));
                float deg = jet * Mathf.Rad2Deg;
                _gearGlow.Put(mid, len, deg, new Color(0.3f, 0.6f, 1f, 0.5f), SkillSprite("beam"), 1.1f / len);
                _gearGlow.Put(mid, len, deg, new Color(0.9f, 0.97f, 1f, 0.85f), SkillSprite("beam"), 0.45f / len);
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
                    // 물 리본 잔상(샘플): 끝이 지나온 호를 14마디로, 뒤로 갈수록 가늘고 옅게.
                    Vector3 prev = tip;
                    for (int j = 1; j <= 14; j++)
                    {
                        float a = arm - (j * 0.06f);
                        Vector3 p = me + (new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r);
                        float fadeJ = 1f - (j / 15f);
                        BoltPiece(prev, p, 0.55f * fadeJ * LevelScale(lv), new Color(0.43f, 0.78f, 1f, 0.55f * fadeJ));
                        BoltPiece(prev, p, 0.18f * fadeJ * LevelScale(lv), new Color(0.9f, 0.97f, 1f, 0.7f * fadeJ));
                        prev = p;
                    }
                    _gear.Put(tip, 0.45f, 0f, new Color(0.85f, 0.85f, 0.9f, 1f), SkillSprite("soft_shadow"));
                    TierHalo(tip, 0.45f, TierLv(UpgradeId.Whip));
                    if (Random.value < 0.5f) Emit("Effects/water_drop", tip, new Vector3(-Mathf.Sin(arm), Mathf.Cos(arm), 0f) * 4f, 4f, 0.3f, 0.22f, 0.05f, HoseTint, new Color(HoseTint.r, HoseTint.g, HoseTint.b, 0f), 0f);
                    if (whirl) _gearRing.Put(tip, 1f, _time * 400f, new Color(0.6f, 0.9f, 1f, 0.6f));
                }
            }
            foreach (WhirlMark m in _sim.WhirlMarks)
            {
                // 물 회오리(샘플): 제자리에서 3겹 물 고리가 서로 다른 속도로 돈다.
                float life = Mathf.Clamp01(m.Life / SurvivorSim.WhirlLife);
                float a = Mathf.Min(1f, (SurvivorSim.WhirlLife - m.Life) * 4f) * Mathf.Min(1f, life * 2f);
                Vector3 at = W(m.Pos) + Up(0.1f);
                for (int j = 0; j < 3; j++)
                {
                    Color c = j == 0 ? new Color(0.3f, 0.65f, 1f, 0.32f * a) : new Color(0.6f, 0.86f, 1f, (0.3f - (j * 0.08f)) * a);
                    _gearGlow.Put(at, (1.3f - (j * 0.25f)) * 21f / 19f, (_time * (460f + (j * 170f))) + (j * 115f) + (m.Pos.X * 40f), c, SkillSprite("arc_band"));
                }
            }
        }

        private struct ChainFlash
        {
            public Vector3 At;
            public float Age;
        }

        /// <summary>물 사슬이 꽂힌 자리의 짧은 빛(0.25초).</summary>
        private readonly List<ChainFlash> _gearFlash = new List<ChainFlash>();

        /// <summary>
        /// 물 사슬(샘플 그대로): 꼭짓점 사이를 지그재그 번개 3겹(파랑 굵게·하늘·흰 심)으로 잇고, 매 프레임 다시 꺾는다.
        /// </summary>
        private void DrawChains(float dt)
        {
            foreach (ChainBolt bolt in _sim.ChainBolts)
            {
                float a = 1f - Mathf.Clamp01(bolt.Age / SurvivorSim.ChainShow);
                for (int k = 0; k + 1 < bolt.Points.Count; k++)
                {
                    Vector3 p0 = W(bolt.Points[k]) + Up(k == 0 ? 0.6f : 0.4f);
                    Vector3 p1 = W(bolt.Points[k + 1]) + Up(0.4f);
                    Vector3 prev = p0;
                    Vector3 side = Vector3.Cross(p1 - p0, Vector3.forward).normalized;
                    const int Segs = 6;
                    for (int j = 1; j <= Segs; j++)
                    {
                        Vector3 next = Vector3.Lerp(p0, p1, j / (float)Segs);
                        if (j < Segs) next += side * Random.Range(-0.35f, 0.35f);
                        // 등급(숲): 굵어지고, Lv5 금빛 겉줄기, Lv6 무지개 겉줄기.
                        int tier = TierLv(UpgradeId.Chain);
                        float ts = TierSize(tier);
                        Color outer = Free && tier >= 6 ? Rainbow(j / 6f) : Free && tier >= 5 ? new Color(1f, 0.8f, 0.35f) : new Color(0.25f, 0.55f, 1f);
                        BoltPiece(prev, next, 0.75f * ts, new Color(outer.r, outer.g, outer.b, 0.45f * a));
                        BoltPiece(prev, next, 0.38f * ts, new Color(0.55f, 0.82f, 1f, 0.8f * a));
                        BoltPiece(prev, next, 0.14f * ts, new Color(1f, 1f, 1f, a));
                        prev = next;
                    }
                }
            }
            for (int i = _gearFlash.Count - 1; i >= 0; i--)
            {
                ChainFlash f = _gearFlash[i];
                f.Age += dt;
                if (f.Age > 0.25f)
                {
                    _gearFlash.RemoveAt(i);
                    continue;
                }
                _gearFlash[i] = f;
                _gearGlow.Put(f.At + Up(0.4f), 1.6f * (1f + f.Age * 2f), 0f, new Color(0.65f, 0.88f, 1f, 0.8f * (1f - (f.Age / 0.25f))), SkillSprite("soft_glow"));
            }
        }

        /// <summary>번개 한 토막: beam 스프라이트(가로 띠, 두께는 높이의 12/28)를 from→to로 늘린다.</summary>
        private void BoltPiece(Vector3 from, Vector3 to, float thick, Color color)
        {
            float len = Mathf.Max(0.05f, Vector3.Distance(from, to));
            float deg = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
            _gearGlow.Put((from + to) * 0.5f, len * 1.15f, deg, color, SkillSprite("beam"), thick / (len * 1.15f * 0.43f));
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
                float size = (b.Small ? 0.85f : 1.35f) * (1f + (0.45f * squash));
                float bob = 0.6f + (0.15f * Mathf.Sin((_time * 9f) + b.Pos.X));
                _shadows.Put(at, size * 0.6f, 0f, new Color(0f, 0f, 0f, 0.3f), null, 0.5f);
                int tier = TierLv(UpgradeId.Balloon);
                size *= TierSize(tier);
                TierHalo(at + Up(bob), size * 0.5f, tier);
                _gear.Put(at + Up(bob), size, Mathf.Atan2(b.Vel.Y, b.Vel.X) * Mathf.Rad2Deg * 0.15f, Color.white, SkillSprite("balloon"), 1f / (1f + (0.9f * squash)));
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
                int tier = TierLv(UpgradeId.Extinguisher);
                TierHalo(at, 0.7f * TierSize(tier), tier);
                _gear.Put(at, 1.6f * TierSize(tier), b.Spin * Mathf.Rad2Deg, TierTint(Color.white, tier), SkillSprite("extinguisher"));
                // 하얀 분말 꼬리.
                Emit(Smokes[Random.Range(0, Smokes.Length)], at, new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(-0.6f, 0.6f), 0f), 2f, 0.6f, 0.4f, 1.2f, new Color(1f, 1f, 1f, 0.7f), new Color(1f, 1f, 1f, 0f), Random.Range(-60f, 60f));
            }
            foreach (Tornado t in _sim.Tornadoes)
            {
                // 하얀 회오리(샘플 그대로): 위로 갈수록 넓어지는 띠 9단이 서로 다른 속도로 돌고, 분말이 소용돌이친다.
                Vector3 at = W(t.Pos);
                float fade = Mathf.Clamp01(t.Life / 0.6f);
                _shadows.Put(at, SurvivorSim.TornadoRadius * 1.6f, 0f, new Color(0f, 0f, 0f, 0.3f * fade), null, 0.5f);
                for (int k = 0; k < 9; k++)
                {
                    float h = 0.15f + (k * 0.42f);
                    float r = 0.45f + (k * 0.2f);
                    float wob = Mathf.Sin((_time * 3f) + (k * 0.6f)) * 0.25f;
                    Vector3 p = at + new Vector3(wob, 0f, 0f) + Up(h);
                    _gearGlow.Put(p, r * 2f * 21f / 19f, (_time * (520f + (k * 40f))) + (k * 47f), new Color(1f, 1f, 1f, (0.85f - (k * 0.05f)) * fade), SkillSprite("arc_band"));
                    _gearGlow.Put(p, r * 1.6f * 21f / 19f, (-_time * 380f) + (k * 90f), new Color(0.75f, 0.85f, 0.95f, 0.45f * fade), SkillSprite("arc_band"));
                }
                for (int k = 0; k < 2; k++)
                {
                    float a = (_time * 9f) + (k * 3.1f);
                    float h = Random.Range(0.2f, 3.4f);
                    Vector3 p = at + (new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (0.45f + (h * 0.2f))) + Up(h);
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
                bool on = m.Arm <= 0f && Mathf.Sin(_time * 10f) > 0f;
                int tier = TierLv(UpgradeId.Mine);
                TierHalo(at, 0.55f * TierSize(tier), tier);
                _gearGround.Put(at, 1.15f * TierSize(tier), 0f, TierTint(Color.white, tier), SkillSprite(on ? "mine_on" : "mine_off"));
                if (m.Arm <= 0f) _gearGlow.Put(at, 1.1f, 0f, new Color(IceTint.r, IceTint.g, IceTint.b, 0.3f * blink), SkillSprite("soft_glow"));
                if (!field) continue;
                for (int j = i + 1; j < _sim.Mines.Count; j++)
                {
                    Vec2 a = m.Pos;
                    Vec2 b = _sim.Mines[j].Pos;
                    float len = a.DistanceTo(b);
                    if (len > SurvivorSim.IceLink) continue;
                    // 빙결 지대(샘플): 빛나는 얼음 선 + 선을 따라 솟은 고드름.
                    Vector3 pa = W(a) + Up(0.05f);
                    Vector3 pb = W(b) + Up(0.05f);
                    BoltPiece(pa, pb, 0.6f, new Color(0.47f, 0.82f, 1f, 0.45f));
                    BoltPiece(pa, pb, 0.16f, new Color(0.92f, 0.99f, 1f, 0.6f + (0.3f * blink)));
                    int spikes = Mathf.FloorToInt(len / 0.6f);
                    for (int q = 1; q < spikes; q++)
                    {
                        Vector3 p = Vector3.Lerp(pa, pb, q / (float)spikes);
                        _gear.Put(p + Up(0.2f), 0.35f + (0.12f * ((q * 7) % 5) / 4f), 0f, new Color(0.92f, 0.99f, 1f, 0.95f), SkillSprite("icicle"));
                    }
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
                TierHalo(at + Up(f.R * 0.6f), f.R, TierLv(UpgradeId.Foam));
                int n = 5 + Mathf.RoundToInt(f.R * 4f);
                for (int k = 0; k < n; k++)
                {
                    float a = (k * 2.4f) + (f.Age * 5f);
                    float d = f.R * 0.6f * (0.3f + (0.7f * ((k * 37) % 10 / 10f)));
                    Vector3 p = at + (new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * d) + Up(f.R * 0.6f);
                    _gear.Put(p, f.R * 1.05f, a * 30f, new Color(1f, 1f, 1f, 0.97f), SkillSprite("foam_puff"));
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
                    _gear.Put(p + Up(0.8f), 2.4f, k * 20f, new Color(1f, 1f, 1f, 0.98f), SkillSprite("foam_puff"));
                    if (Random.value < 0.2f) Emit(Smokes[Random.Range(0, Smokes.Length)], p, new Vector3(-_sim.AvalancheDir * 2f, 0f, 0f), 2f, 0.5f, 0.6f, 1.4f, new Color(1f, 1f, 1f, 0.7f), new Color(1f, 1f, 1f, 0f), 0f);
                }
            }
        }

        private void DrawBubbles()
        {
            foreach (BubbleShot b in _sim.BubbleShots)
            {
                Vector3 at = W(b.Pos) + Up(0.8f);
                int tier = TierLv(UpgradeId.Bubble);
                TierHalo(at, 0.35f * TierSize(tier), tier);
                _gear.Put(at, 0.7f * TierSize(tier), _time * 90f, TierTint(Color.white, tier), SkillSprite("bubble"));
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
                    _gear.Put(at + Up(0.35f) + new Vector3(crack, 0f, 0f), s * 1.1f, 0f, Color.white, SkillSprite("ice_block"));
                    _gearGlow.Put(at, s * 1.2f, 0f, new Color(IceTint.r, IceTint.g, IceTint.b, 0.25f));
                }
                else if (e.Captured > 0f)
                {
                    Vector3 at = W(e.Pos) + Up(CaptureLift(e));
                    float s = (e.Radius * 3.4f) + 0.8f;
                    float wob = 1f + (0.05f * Mathf.Sin(_time * 10f));
                    _gear.Put(at, s * 1.1f * wob, _time * 40f, Color.white, SkillSprite("bubble"), 1f / wob);
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
                _gearGround.Put(W(m), 1.25f, 0f, Color.white, SkillSprite("manhole_hole"));
                if (g == null || g.Fuse > 0f) _gearGround.Put(W(m) + new Vector3(shake, 0f, 0f) + Up(0.02f), 1.15f, 0f, Color.white, SkillSprite("manhole"));
            }
            foreach (Geyser g in _sim.Geysers)
            {
                // 솟기 전: 뚜껑이 들썩이고, 둘레에 물빛 고리가 조여 든다.
                float t = 1f - Mathf.Clamp01(g.Fuse / SurvivorSim.GeyserFuse);
                Vector3 at = W(g.Pos);
                TierHalo(at + Up(0.03f), g.Radius, TierLv(UpgradeId.Manhole));
                _gearGlow.Put(at + Up(0.03f), g.Radius * 2f * (1.35f - (0.55f * t)) * 21f / 20f, _time * 60f, new Color(GeyserTint.r, GeyserTint.g, GeyserTint.b, 0.2f + (0.5f * t)), SkillSprite("ring_dashed"));
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
                case HitSource.Sprinkler: Impact(at, SprayTint, 0.7f, TierLv(UpgradeId.Sprinkler)); return true;
                case HitSource.Balloon: Impact(at, BalloonTint, 0.8f, TierLv(UpgradeId.Balloon)); return true;
                case HitSource.Extinguisher: Impact(at, PowderTint, 0.8f, TierLv(UpgradeId.Extinguisher)); return true;
                case HitSource.Mine: Impact(at, IceTint, 0.9f, TierLv(UpgradeId.Mine)); return true;
                case HitSource.Foam: Impact(at, FoamTint, 0.8f, TierLv(UpgradeId.Foam)); return true;
                case HitSource.Bubble: Impact(at, BubbleTint, 0.8f, TierLv(UpgradeId.Bubble)); return true;
                case HitSource.Geyser: Impact(at, GeyserTint, 1f, TierLv(UpgradeId.Manhole)); return true;
                case HitSource.Chain: Impact(at, ChainTint, 0.7f, TierLv(UpgradeId.Chain)); return true;
                case HitSource.Whip:
                {
                    Vector3 away = Away(h.Pos);
                    float deg = (Mathf.Atan2(away.y, away.x) * Mathf.Rad2Deg) - 90f;
                    EmitSprite(BeamSprite(), at - (away * 0.3f), away * 3f, 4f, 0.2f, 0.4f, 0.16f, HoseTint, new Color(HoseTint.r, HoseTint.g, HoseTint.b, 0f), 0f, true, 0f, 3f, deg);
                    Impact(at, HoseTint, 0.7f, TierLv(UpgradeId.Whip));
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------
        // 그림(절차 스프라이트, 64px)
        // ------------------------------------------------------------------





    }
}
