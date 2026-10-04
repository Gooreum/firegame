using System.Collections.Generic;
using System.IO;
using FireGame.Prototypes.Logic;
using FireGame.UnityLayer;
using FireGame.UnityLayer.Feel;
using UnityEngine;
using UnityEngine.UI;

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
        private Pool _guardRing;

        /// <summary>레벨업 뒤 흐른 시간(수호 반경이 한 번 크게 퍼지는 펄스).</summary>
        private float _guardPulse = 99f;

        /// <summary>그림에 쓰는 수호 반경(실제 값으로 부드럽게 따라간다).</summary>
        private float _guardShown;

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
            _guardRing = new Pool(_world, "GuardRing", RingSprite(), 3, Additive);
            _pools.Add(_guardRing);
        }

        /// <summary>수호 반경 안에서 타는 구조물(불꽃을 눌러 그린다).</summary>
        private bool Held(Structure st)
        {
            return _sim.Guardian && st.Burning && st.DistanceTo(_sim.Player) <= _sim.GuardRadius;
        }

        /// <summary>
        /// 수호 반경: 발밑 둘레에 옅은 물빛 띠가 천천히 돌고, 반경 안 타는 건물엔 물빛 테가 씌워진다(눌려 있다).
        /// 레벨업하면 한 번 크게 퍼졌다 새 반경으로 내려앉는다.
        /// </summary>
        private void DrawGuardRadius(float dt)
        {
            float r = _sim.GuardRadius;
            _guardShown = _guardShown <= 0f ? r : Mathf.MoveTowards(_guardShown, r, dt * 2f);
            _guardPulse += dt;
            if (_sim.Outcome != SOutcome.Playing) return;
            Vector3 me = W(_sim.Player);
            float breathe = 0.5f + (0.5f * Mathf.Sin(_time * 2f));
            // 반경은 구조물 가장자리까지 잰다: 원은 몸 둘레 반지름 r(+몸 반폭).
            float d = (_guardShown + 0.4f) * 2f / 0.85f;
            _guardRing.Put(me, d, _time * 8f, new Color(0.45f, 0.8f, 1f, 0.16f + (0.06f * breathe)));
            _guardRing.Put(me, d * 0.97f, -_time * 5f, new Color(0.6f, 0.9f, 1f, 0.08f));
            if (_guardPulse < 0.8f)
            {
                float t = _guardPulse / 0.8f;
                _guardRing.Put(me, d * (1f + (0.5f * Mathf.Sin(t * Mathf.PI))), 0f, new Color(0.7f, 0.95f, 1f, 0.6f * (1f - t)));
            }
            foreach (Structure st in _sim.Structures)
            {
                if (!Held(st)) continue;
                _guardRing.Put(W(st.Pos), (Mathf.Max(st.Half.X, st.Half.Y) * 2.3f) + 0.5f, 0f, new Color(0.5f, 0.85f, 1f, 0.2f + (0.08f * breathe)));
            }
        }

        /// <summary>시뮬 신호에 반응한다(React 안에서, 틱마다).</summary>
        private void ReactGuardian()
        {
            if (!_sim.Guardian) return;
            if (_sim.JustLeveled) _guardPulse = 0f;
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
            foreach (Structure ruin in _sim.RuinSpat)
            {
                // 잿더미 둥지가 불씨를 뱉었다: 잔해에서 불똥이 튀고 붉은 고리.
                Vector3 at = W(ruin.Pos);
                Burst(at, 14, new Color(1f, 0.45f, 0.1f), 6f);
                Shockwave(at, new Color(1f, 0.35f, 0.08f, 0.7f), Mathf.Max(ruin.Half.X, ruin.Half.Y) * 3.2f, 0.35f);
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

            DrawGuardRadius(dt);
            DrawHaven();
            DrawCrowd();
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

        /// <summary>지켜 낸 집: 앞벽 창 셋에 따뜻한 불이 켜진다(살아 있는 집). 지붕 위로 옅은 금빛.</summary>
        private void DrawGuardedLights(Structure st, int seed)
        {
            for (int k = 0; k < 3; k++)
            {
                Vector3 c = WindowPoint(seed, k);
                float glow = 0.85f + (0.15f * Mathf.Sin((_time * 1.5f) + seed + k));
                _roofTrim.PutRot(c, Facade, 0.42f, 0.32f, new Color(1f, 0.86f, 0.45f, 0.95f * glow));
                _roofGlow.PutRot(c + new Vector3(0f, -0.02f, 0f), Facade, 0.9f, 0.9f, new Color(1f, 0.8f, 0.35f, 0.35f * glow));
            }
        }

        /// <summary>잿더미 둥지: 잔해 사이 불씨가 맥박치고 작은 불꽃이 핀다. 불씨를 뱉는 틱엔 확 솟는다.</summary>
        private void DrawRuinNest(Structure st, Vector3 at, float w, float h, int seed)
        {
            float beat = 0.6f + (0.4f * Mathf.Sin((_time * 3f) + seed));
            bool spat = _sim.RuinSpat.Contains(st);
            _groundGlow.Put(at, Mathf.Max(w, h) * 1.3f, 0f, new Color(1f, 0.28f, 0.05f, 0.3f * beat));
            for (int k = 0; k < 3; k++)
            {
                float ox = (Hash01((seed * 11) + k) - 0.5f) * w * 0.6f;
                float oy = (Hash01((seed * 17) + k) - 0.5f) * h * 0.5f;
                float f = (0.5f + (0.2f * Mathf.Sin((_time * 14f) + k + seed))) * (spat ? 1.6f : 1f);
                _groundFire.Put(at + new Vector3(ox, oy + 0.15f, 0f), f, 0f, new Color(1f, 1f, 1f, 0.85f), FlameArt.Frame(_emberSheet, _time, seed + k), 0.75f);
            }
        }

        /// <summary>쉼터 곁: 발밑에 초록 원이 숨 쉬고 "+" 같은 초록 반짝이가 오른다.</summary>
        private void DrawHaven()
        {
            if (_sim.Haven == null || _sim.Outcome != SOutcome.Playing) return;
            Vector3 me = W(_sim.Player);
            float breathe = 0.5f + (0.5f * Mathf.Sin(_time * 5f));
            _civilianRings.Put(me, 2.2f + (0.3f * breathe), 0f, new Color(0.4f, 1f, 0.5f, 0.35f + (0.2f * breathe)));
            _civilianRings.Put(W(_sim.Haven.Pos), Mathf.Max(_sim.Haven.Half.X, _sim.Haven.Half.Y) * 2f + (SurvivorSim.HavenRange * 2f), 0f, new Color(0.4f, 1f, 0.5f, 0.12f));
            if (Random.value < 0.12f) Sparkle(me + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.2f, 0.2f), 0f), 1, new Color(0.5f, 1f, 0.55f));
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
        // ------------------------------------------------------------------
        // 결과 한 장면: 구한 사람들이 곁에 모이고(0~1.2초) → 카메라가 마을 전체로 빠지고(1.2~3.2초) → 그 장면을 사진으로 남긴다.
        // ------------------------------------------------------------------

        /// <summary>사람들이 모이는 시간과 카메라가 빠지는 구간(초, 결과가 난 순간부터).</summary>
        public const float CrowdTime = 1.2f;
        public const float PullEnd = 3.2f;

        /// <summary>모여 서는 사람 그림 상한(구한 사람이 더 많아도 무리 크기로 읽힌다).</summary>
        private const int CrowdMax = 40;

        /// <summary>판 끝 사진 크기.</summary>
        private const int FaceWidth = 480;
        private const int FaceHeight = 270;

        /// <summary>스테이지별 마지막 판 끝 사진(소방서 배경·썸네일). 파일에서 한 번 읽는다.</summary>
        private static readonly Dictionary<int, Texture2D> StageFaces = new Dictionary<int, Texture2D>();
        private bool _faceTaken;

        public static string FacePath(int stage)
        {
            return Path.Combine(Application.persistentDataPath, "guardian_stage" + stage + ".png");
        }

        /// <summary>그 스테이지의 마지막 사진. 없으면 null.</summary>
        public static Texture2D Face(int stage)
        {
            if (StageFaces.TryGetValue(stage, out Texture2D tex) && tex != null) return tex;
            string path = FacePath(stage);
            if (!File.Exists(path)) return null;
            var loaded = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!loaded.LoadImage(File.ReadAllBytes(path))) return null;
            StageFaces[stage] = loaded;
            return loaded;
        }

        /// <summary>판이 끝난 뒤 카메라: 소방관 클로즈업 → 마을 전체로 부드럽게. 수호자가 아니거나 판 중이면 false.</summary>
        private bool ResultCamera()
        {
            if (!_sim.Guardian || _sim.Outcome == SOutcome.Playing) return false;
            Vector3 me = new Vector3(_sim.Player.X, _sim.Player.Y, -10f);
            var town = new Vector3(SurvivorSim.ArenaSize / 2f, (SurvivorSim.ArenaSize / 2f) - 1f, -10f);
            float close = CameraSize * 0.7f;
            float whole = SurvivorSim.ArenaSize * 0.52f;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_overAge - CrowdTime) / (PullEnd - CrowdTime)));
            float amp = _trauma * _trauma * 0.3f * (1f - t);
            float k = Time.realtimeSinceStartup * 25f;
            Vector3 shake = new Vector3((Mathf.PerlinNoise(k, 0f) * 2f) - 1f, (Mathf.PerlinNoise(0f, k) * 2f) - 1f, 0f) * amp;
            _cameraAt = Vector3.Lerp(me, town, t);
            PlaceWorldCamera(_cameraAt + shake, Mathf.Lerp(close, whole, t));
            return true;
        }

        /// <summary>구한 사람들이 사방에서 걸어와 소방관 둘레 동심원에 선다. 다 오면 만세.</summary>
        private void DrawCrowd()
        {
            if (_sim.Outcome == SOutcome.Playing) return;
            int n = Mathf.Min(CrowdMax, _sim.Rescued);
            Vector3 me = W(_sim.Player);
            int ring = 0;
            int inRing = 0;
            int ringSize = 6;
            for (int i = 0; i < n; i++)
            {
                if (inRing >= ringSize)
                {
                    ring++;
                    inRing = 0;
                    ringSize = 6 + (ring * 6);
                }
                float a = ((inRing + (ring * 0.5f)) * Mathf.PI * 2f / ringSize) + 0.3f;
                float r = 1.3f + (ring * 0.75f);
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                inRing++;
                // 한 사람씩 조금씩 늦게 출발해 바깥에서 걸어 들어온다.
                float walk = Mathf.Clamp01((_overAge - (i * 0.02f)) / CrowdTime);
                float arrive = Mathf.SmoothStep(0f, 1f, walk);
                Vector3 at = me + (dir * Mathf.Lerp(r + 7f, r, arrive));
                _shadows.Put(at + new Vector3(0f, -0.3f, 0f), 0.8f, 0f, new Color(0f, 0f, 0f, 0.35f), null, 0.5f);
                _civilianRings.Put(at, 0.9f, 0f, new Color(0.4f, 1f, 0.5f, 0.25f * arrive));
                GameObject person = _people.Get(CivilianModel(i));
                if (person == null) continue;
                Models3D.Pose(person, at, walk < 1f ? -dir : -dir + new Vector3(0f, -0.6f, 0f));
                Models3D.Play(person, walk < 1f ? "Run" : "Victory", 1f, _time + i);
                Models3D.Tint(person, Color.white, CivilianColor(i), 10 + CivilianKind(i));
            }
        }

        /// <summary>장면이 다 빠진 순간 월드 화면을 한 장 찍어 그 스테이지의 얼굴로 남긴다(한 판에 한 번).</summary>
        private void TakeFace()
        {
            if (_faceTaken || _overAge < PullEnd + 0.1f || _worldRt == null) return;
            _faceTaken = true;
            var small = RenderTexture.GetTemporary(FaceWidth, FaceHeight, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(_worldRt, small);
            RenderTexture before = RenderTexture.active;
            RenderTexture.active = small;
            var tex = new Texture2D(FaceWidth, FaceHeight, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, FaceWidth, FaceHeight), 0, 0);
            tex.Apply();
            RenderTexture.active = before;
            RenderTexture.ReleaseTemporary(small);
            StageFaces[_stage] = tex;
            try
            {
                File.WriteAllBytes(FacePath(_stage), tex.EncodeToPNG());
            }
            catch (IOException e)
            {
                Debug.LogWarning("[SurvivorView] 판 끝 사진 저장 실패: " + e.Message);
            }
        }

        /// <summary>결과창 배치: 수호자는 화면 아래 낮은 띠(마을이 보이게), 아니면 가운데 큰 판.</summary>
        private void LayoutResult(bool guardian)
        {
            _resultBack.rectTransform.anchoredPosition = guardian ? new Vector2(0f, -400f) : Vector2.zero;
            _resultBack.rectTransform.sizeDelta = guardian ? new Vector2(760f, 200f) : new Vector2(1100f, 560f);
            _resultBack.color = new Color(0f, 0f, 0f, guardian ? 0.45f : 0.78f);
            _result.alignment = guardian ? TextAnchor.LowerCenter : TextAnchor.MiddleCenter;
            for (int i = 0; i < _stars.Count; i++) _stars[i].rectTransform.anchoredPosition = new Vector2((i - 1) * 130f, guardian ? 30f : 115f);
        }

        /// <summary>수호자 결과: 장면(사람들·마을)이 말하고, 다 빠진 뒤에야 별과 "탭하면 소방서로"만 뜬다.</summary>
        private void GuardianResult()
        {
            TakeFace();
            bool show = _overAge > PullEnd + 0.2f;
            _resultBack.gameObject.SetActive(show);
            if (!show) return;
            bool won = _sim.Outcome == SOutcome.Won;
            float since = _overAge - (PullEnd + 0.2f);
            _result.text = since > 0.8f ? "탭하면 소방서로" : "";
            for (int i = 0; i < _stars.Count; i++)
            {
                _stars[i].gameObject.SetActive(won);
                float pop2 = Mathf.Clamp01((since - (i * 0.25f)) / 0.2f);
                _stars[i].sprite = Art.Get(i < _sim.Stars ? "UI/star" : "UI/star_empty");
                _stars[i].rectTransform.localScale = Vector3.one * (pop2 < 1f ? Mathf.Lerp(0f, 1.3f, pop2) : 1f);
            }
            _resultBack.rectTransform.localScale = Vector3.one;
        }
        /// <summary>소방서 배경: 그 스테이지의 마지막 사진을 화면 가득(가로 맞춤) 깐다. 사진이 없으면 false.</summary>
        private static bool StationFace(Transform layer, int stage)
        {
            Texture2D tex = Face(stage);
            if (tex == null) return false;
            RawImage back = NewRaw(layer, "Face", tex);
            UiKit.Stretch(back.rectTransform);
            back.color = new Color(1f, 1f, 1f, 0.9f);
            return true;
        }

        /// <summary>스테이지 버튼 왼쪽 끝 작은 사진(56×32). 사진이 없으면 아무것도 안 붙인다.</summary>
        private static void StageThumb(RectTransform button, int stage)
        {
            Texture2D tex = Face(stage);
            if (tex == null) return;
            RawImage thumb = NewRaw(button, "Thumb", tex);
            UiKit.Place(thumb.rectTransform, new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(56f, 32f));
        }

        private static RawImage NewRaw(Transform parent, string name, Texture tex)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var raw = go.GetComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
            return raw;
        }
    }
}
