using FireGame.Prototypes.Logic;
using FireGame.UnityLayer;
using FireGame.UnityLayer.Feel;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 수호자 마을 샘플의 그림(docs §20): 버티기 링, 링이 풀리는 순간, 버티기 후보 표시.
    /// 규칙은 SurvivorSim(Guardian)이 정하고 여기선 읽기만 한다.
    /// </summary>
    public sealed partial class SurvivorView
    {
        /// <summary>링 둘레에 선 불꽃 수.</summary>
        private const int SiegeFlames = 36;

        private Pool _siegeFire;
        private Pool _siegeGlow;
        private Pool _siegeRing;

        /// <summary>버티기가 막 끝난 자리와 그때부터 흐른 시간(링 불꽃이 바깥으로 흩어진다).</summary>
        private Vector3 _siegeEndAt;
        private float _siegeEndAge = 99f;
        private bool _siegeEndWon;

        /// <summary>링이 닫힌 뒤 흐른 시간(닫히는 순간 불꽃이 땅에서 솟는다).</summary>
        private float _siegeAge;

        private void BuildGuardianPools()
        {
            _siegeGlow = AddPool("SiegeGlow", "Effects/glow", 3, true);
            _siegeRing = new Pool(_world, "SiegeRing", RingSprite(), 4, Additive);
            _pools.Add(_siegeRing);
            _siegeFire = AddPool("SiegeFire", "Effects/fire_01", 9);
            _siegeFire.Upright = true;
        }

        /// <summary>시뮬 신호에 반응한다(React 안에서, 틱마다).</summary>
        private void ReactGuardian()
        {
            if (!_sim.Guardian) return;
            if (_sim.JustSiegeStart && _sim.Siege != null)
            {
                // 링이 닫힌다: 쿵, 붉은 충격파가 안쪽으로, 카메라가 살짝 물러나 링 전체를 담는다.
                _siegeAge = 0f;
                Vector3 at = W(_sim.Siege.Center);
                var red = new Color(1f, 0.3f, 0.05f);
                Shockwave(at, red, SurvivorSim.SiegeRadius * 2.2f, 0.5f);
                Shockwave(at, new Color(1f, 0.6f, 0.2f), SurvivorSim.SiegeRadius * 1.6f, 0.45f, 0.1f);
                _bossBandText.text = "버텨라! " + Eul(_sim.Siege.Target.Name) + " 끌 때까지";
                _bandTint = new Color(0.55f, 0.08f, 0f);
                _bossBannerAge = 0f;
                _trauma = Mathf.Min(1f, _trauma + 0.6f);
                _zoomKick = Mathf.Min(_zoomKick, -0.6f);
                HitStop(0.05f);
                GameAudio.Play(Cue.Critical);
            }
            if (_sim.JustSiegeWave && _sim.Siege != null)
            {
                // 파도: 링 둘레가 한 번 확 타오른다(불씨가 거기서 나온다).
                Shockwave(W(_sim.Siege.Center), new Color(1f, 0.4f, 0.1f, 0.6f), SurvivorSim.SiegeRadius * 2.05f, 0.3f);
            }
            if (_sim.JustSiegeWon && _sim.LastSiege != null)
            {
                // 지켜 냈다: 물빛 충격파 세 겹이 링 밖으로, 김 기둥, 잠깐 멈칫, 불똥 대신 물보라.
                Vector3 at = W(_sim.LastSiege.Center);
                var water = new Color(0.5f, 0.85f, 1f);
                for (int k = 0; k < 3; k++) Shockwave(at, water, (SurvivorSim.SiegeReliefRange * 2f) + (4f * k), 0.7f, k * 0.12f);
                Steam(at, 16, 2f);
                Pillar(at, water);
                Sparkle(at, 20, new Color(0.8f, 0.95f, 1f));
                Flash(water, 0.3f);
                HitStop(0.08f);
                _slowmo = Mathf.Max(_slowmo, 0.6f);
                _zoomKick = Mathf.Max(_zoomKick, 0.8f);
                _trauma = Mathf.Min(1f, _trauma + 0.5f);
                _siegeEndAt = at;
                _siegeEndAge = 0f;
                _siegeEndWon = true;
                ShowAlert("지켜 냈다! " + _sim.LastSiege.Target.Name, water);
                GameAudio.Play(Cue.Won);
            }
            if (_sim.JustSiegeLost && _sim.LastSiege != null)
            {
                // 무너졌다: 링이 잿빛으로 꺼진다. 짧은 진동.
                _siegeEndAt = W(_sim.LastSiege.Center);
                _siegeEndAge = 0f;
                _siegeEndWon = false;
                _trauma = Mathf.Min(1f, _trauma + 0.4f);
                ShowAlert(Eul(_sim.LastSiege.Target.Name) + " 잃었다", new Color(0.7f, 0.7f, 0.7f));
                GameAudio.Play(Cue.Failed);
            }
        }

        /// <summary>땅 위 수호자 그림: 버티기 링(닫힌 동안), 흩어지는 링(끝난 뒤), 버티기 후보 둘레의 닫힐 자리 표시.</summary>
        private void DrawGuardian(float dt)
        {
            if (!_sim.Guardian) return;
            _siegeAge += dt;
            _siegeEndAge += dt;

            Siege siege = _sim.Siege;
            if (siege != null) DrawSiegeRing(W(siege.Center), siege.Radius, Mathf.Clamp01(_siegeAge / 0.35f), 1f, false);
            else if (_siegeEndAge < 1.2f)
            {
                // 끝난 링: 성공이면 불꽃이 바깥으로 밀려나며 꺼지고, 실패면 잿빛으로 가라앉는다.
                float t = _siegeEndAge / 1.2f;
                float r = SurvivorSim.SiegeRadius * (_siegeEndWon ? 1f + (0.8f * t) : 1f);
                DrawSiegeRing(_siegeEndAt, r, 1f - t, 1f - t, !_siegeEndWon);
            }

            // 후보: 아직 링이 안 닫힌 대형 신고·랜드마크 둘레에 "여기서 닫힌다" 점선 고리가 숨 쉰다.
            if (siege != null || _sim.Outcome != SOutcome.Playing) return;
            foreach (Structure t in _sim.SiegeTargets)
            {
                if (!t.Burning) continue;
                Vector3 at = W(t.Pos);
                float breathe = 0.5f + (0.5f * Mathf.Sin(_time * 4f));
                float reach = Mathf.Max(t.Half.X, t.Half.Y) + SurvivorSim.SiegeEnter;
                _siegeRing.Put(at, reach * 2f / 0.85f, _time * 10f, new Color(1f, 0.5f, 0.15f, 0.25f + (0.2f * breathe)));
                for (int k = 0; k < 16; k++)
                {
                    float a = (k * Mathf.PI * 2f / 16f) + (_time * 0.4f);
                    Vector3 p = at + (new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * reach);
                    _siegeGlow.Put(p, 0.6f, 0f, new Color(1f, 0.55f, 0.2f, 0.35f + (0.25f * breathe)));
                }
            }
        }

        /// <summary>이름 뒤 목적격 조사: 받침이 있으면 "을", 없으면 "를".</summary>
        private static string Eul(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            char last = name[name.Length - 1];
            bool batchim = last >= 0xAC00 && last <= 0xD7A3 && ((last - 0xAC00) % 28) != 0;
            return name + (batchim ? "을" : "를");
        }

        /// <param name="rise">0이면 불꽃이 땅에 붙어 있고 1이면 다 솟았다.</param>
        /// <param name="alpha">전체 투명도.</param>
        /// <param name="ash">잿빛(실패한 링).</param>
        private void DrawSiegeRing(Vector3 at, float radius, float rise, float alpha, bool ash)
        {
            if (alpha <= 0f) return;
            float pulse = 0.85f + (0.15f * Mathf.Sin(_time * 6f));
            Color glow = ash ? new Color(0.4f, 0.4f, 0.4f, 0.35f * alpha) : new Color(1f, 0.35f, 0.05f, 0.5f * alpha * pulse);
            _siegeRing.Put(at, radius * 2f / 0.85f, 0f, glow);
            _siegeRing.Put(at, radius * 2.15f / 0.85f, 0f, new Color(glow.r, glow.g, glow.b, glow.a * 0.4f));
            Color flame = ash ? new Color(0.35f, 0.35f, 0.35f, alpha) : new Color(1f, 1f, 1f, alpha);
            for (int k = 0; k < SiegeFlames; k++)
            {
                float a = k * Mathf.PI * 2f / SiegeFlames;
                Vector3 p = at + (new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius);
                float flick = 0.8f + (0.25f * Mathf.Sin((_time * 13f) + (k * 1.7f)));
                float size = 1.1f * flick * Mathf.Lerp(0.2f, 1f, rise);
                _siegeGlow.Put(p, 1.8f * size, 0f, new Color(glow.r, glow.g, glow.b, glow.a * 0.8f));
                _siegeFire.Put(p, size, 0f, flame, FlameArt.Frame(_emberSheet, _time, k));
            }
        }
    }
}
