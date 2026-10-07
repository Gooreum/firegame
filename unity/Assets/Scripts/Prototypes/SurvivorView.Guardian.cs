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
    /// 수호자 마을의 그림(docs §20): 수호 반경, 쉼터, 잿더미 둥지, 지킨 집 불빛, 결과 한 장면.
    /// 규칙은 SurvivorSim(Guardian)이 정하고 여기선 읽기만 한다.
    /// </summary>
    public sealed partial class SurvivorView
    {
        private Pool _guardRing;

        /// <summary>레벨업 뒤 흐른 시간(수호 반경이 한 번 크게 퍼지는 펄스).</summary>
        private float _guardPulse = 99f;

        /// <summary>그림에 쓰는 수호 반경(실제 값으로 부드럽게 따라간다).</summary>
        private float _guardShown;

        private void BuildGuardianPools()
        {
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
            foreach (Structure ruin in _sim.RuinSpat)
            {
                // 잿더미 둥지가 불씨를 뱉었다: 잔해에서 불똥이 튀고 붉은 고리.
                Vector3 at = W(ruin.Pos);
                Burst(at, 14, new Color(1f, 0.45f, 0.1f), 6f);
                Shockwave(at, new Color(1f, 0.35f, 0.08f, 0.7f), Mathf.Max(ruin.Half.X, ruin.Half.Y) * 3.2f, 0.35f);
            }
        }

        /// <summary>땅 위 수호자 그림: 수호 반경, 쉼터, 결과 때 모이는 사람들.</summary>
        private void DrawGuardian(float dt)
        {
            if (!_sim.Guardian) return;
            DrawGuardRadius(dt);
            DrawHaven();
            DrawCrowd();
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
            _civilianRings.Put(me, 2.4f + (0.3f * breathe), 0f, new Color(0.4f, 1f, 0.5f, 0.5f + (0.25f * breathe)));
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
        private Text _help;
        private string _helpDefault;
        private bool _enemiesSteamed;

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
            // 월드 카메라를 지금 한 번 그려 둔다(캡처 하니스는 찍을 때만 그려 RT가 비어 있다).
            _worldCam.Render();
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
        /// <summary>수호자 마을 조작 안내: 물은 저절로 나가고, 쥐면 집중 분사(증기).</summary>
        private string GuardianHelp()
        {
            return Input.touchSupported
                ? "왼손 끌어 이동 · 물은 저절로(오른손 쥐면 집중 분사) · 마을을 노리는 불을 막아라 · 불난 가게 문 앞에 서 있으면 구조"
                : "WASD 이동 · 물은 저절로(왼쪽 버튼 쥐면 겨눈 쪽으로 집중 분사) · 마을을 노리는 불을 막아라 · 카드는 1/2/3 · 문 앞에 서 있으면 구조      R 다시  N 스테이지  Tab 시험판 전환";
        }

        /// <summary>수호자 결과 장면에선 불 몹을 그리지 않는다. 처음 한 번은 몹마다 김이 오른다.</summary>
        private bool EnemiesFaded()
        {
            if (!_sim.Guardian || _sim.Outcome == SOutcome.Playing)
            {
                _enemiesSteamed = false;
                return false;
            }
            if (!_enemiesSteamed)
            {
                _enemiesSteamed = true;
                for (int i = 0; i < _sim.Enemies.Count; i += 3) Steam(W(_sim.Enemies[i].Pos), 1, 0.8f);
            }
            return true;
        }
    }
}
