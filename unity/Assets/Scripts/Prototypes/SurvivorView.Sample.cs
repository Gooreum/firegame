using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using FireGame.UnityLayer;
using UnityEngine;
using UnityEngine.UI;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 숲 개편(2026-10-08): 승인 샘플(tools/levelup-art/*.js)의 그리기를 그대로 옮긴 층.
    /// 숲 카메라는 샘플 비율(화면 높이 270px = 270×Px칸)의 직교 카메라라서, 샘플 px 좌표가 화면에 샘플과 같은 크기로 놓인다.
    /// 층: 바닥(땅 위, 3D 몸 아래) · 공중(모든 몸 위) · 앞(레벨업 머리 위 연출) · 화면(HUD·번쩍·이름 띠).
    /// 규칙 쪽 상태(SurvivorSim.Parts·LevelFx·SampleItems)를 읽어 그리기만 한다.
    /// </summary>
    public sealed partial class SurvivorView
    {
        private const float SampleH = 270f;

        /// <summary>숲에서 3D 사람(소방관·대원)을 키우는 배율. 카메라를 덜 당긴 만큼(270→380px) 1.75에서 줄였다.</summary>
        private const float SamplePersonScale = 1.4f;

        private Vector3 _personScale = Vector3.one;

        /// <summary>숲 카메라가 보는 세로 px(월드). 샘플 270은 너무 가깝다는 지적(2026-10-08)으로 380. 화면 고정 층(HUD)은 SampleH를 그대로 쓴다.</summary>
        private const float ViewH = 380f;

        /// <summary>숲 직교 카메라 반높이(ViewH px).</summary>
        private const float SampleHalf = ViewH * SurvivorSim.Px / 2f;

        private static readonly float TiltCos = Mathf.Cos(Tilt * Mathf.Deg2Rad);

        private readonly SampleCanvas _sg = new SampleCanvas();
        private readonly SampleCanvas _sa = new SampleCanvas();
        private readonly SampleCanvas _sf = new SampleCanvas();
        private readonly SampleCanvas _ss = new SampleCanvas();
        private readonly List<SampleLayer> _sLayers = new List<SampleLayer>();
        private Material _sampleMat;
        private readonly Dictionary<long, Material> _sampleImgMats = new Dictionary<long, Material>();
        private Text _hudName;
        private Text _hudLv;
        private Text _hudBand;
        private UpgradeId _hudItem = UpgradeId.Hose;
        private float _hudBandAge = 99f;

        private sealed class SampleLayer
        {
            public SampleCanvas Canvas;
            public MeshRenderer Vector;
            public Mesh VectorMesh;
            public readonly List<MeshRenderer> Images = new List<MeshRenderer>();
            public readonly List<Mesh> ImageMeshes = new List<Mesh>();
            public int Order;
            public int Kind;   // 0 바닥, 1 공중(월드 px), 2 화면(샘플 화면 px)
        }

        private bool Free
        {
            get { return _sim != null && _sim.Build.Free; }
        }

        /// <summary>샘플 화면 폭(px): 높이 270에 화면비를 곱한다.</summary>
        private float SampleW
        {
            get { return SampleH * (_worldCam != null && _worldCam.aspect > 0.1f ? _worldCam.aspect : 16f / 9f); }
        }

        /// <summary>숲 카메라가 보는 가로 px(월드).</summary>
        private float ViewW
        {
            get { return SampleW * ViewH / SampleH; }
        }

        private void BuildSampleLayers()
        {
            Shader shader = Shader.Find("FireGame/SampleCanvas");
            _sampleMat = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            AddSampleLayer(_sg, 6, 0);
            AddSampleLayer(_sa, 40, 1);
            AddSampleLayer(_sf, 44, 1);
            AddSampleLayer(_ss, 48, 2);

            _hudName = UiKit.OutlinedLabel(_hud, "SampleHudName", "", 48, Color.white, TextAnchor.MiddleLeft);
            UiKit.Place(_hudName.rectTransform, new Vector2(0f, 1f), new Vector2(176f, -72f), new Vector2(520f, 60f));
            _hudLv = UiKit.OutlinedLabel(_hud, "SampleHudLv", "", 34, new Color(0.65f, 0.77f, 0.9f), TextAnchor.MiddleLeft);
            UiKit.Place(_hudLv.rectTransform, new Vector2(0f, 1f), new Vector2(512f, -132f), new Vector2(200f, 44f));
            _hudBand = UiKit.OutlinedLabel(_hud, "SampleHudBand", "", 42, new Color(0.91f, 0.96f, 1f), TextAnchor.MiddleCenter);
            UiKit.Place(_hudBand.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(1600f, 64f));
            foreach (Text t in new[] { _hudName, _hudLv, _hudBand }) t.gameObject.SetActive(false);
            BuildCrewHud();
        }

        private void AddSampleLayer(SampleCanvas c, int order, int kind)
        {
            var go = new GameObject("SampleLayer" + order);
            go.transform.SetParent(_world, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            var mesh = new Mesh { name = "SampleCanvas" + order };
            mesh.MarkDynamic();
            mf.sharedMesh = mesh;
            // 층 순서는 렌더 큐로 못 박는다(정렬 순서만으로는 가까운 층이 먼 바닥 층 밑에 깔렸다).
            mr.sharedMaterial = new Material(_sampleMat) { renderQueue = 3000 + (order * 10) };
            mr.sortingOrder = order;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _sLayers.Add(new SampleLayer { Canvas = c, Vector = mr, VectorMesh = mesh, Order = order, Kind = kind });
        }

        // ------------------------------------------------------------------ 좌표
        /// <summary>샘플 px(월드) → 땅 위 자리.</summary>
        private static Vector3 SGround(Vector2 p)
        {
            return new Vector3(p.x * SurvivorSim.Px, -p.y * SurvivorSim.Px, -0.02f);
        }

        private Vector3 _sForward;
        private float _sNear;

        /// <summary>샘플 px(월드) → 땅 자리를 카메라 바로 앞으로 당긴 자리(직교 카메라라 화면 위치는 같다).</summary>
        private Vector3 SAir(Vector2 p)
        {
            return SGround(p) - (_sForward * _sNear);
        }

        /// <summary>샘플 화면 px → 카메라 바로 앞 자리.</summary>
        private Vector3 SScreen(Vector2 p)
        {
            Vector3 w = _worldCam.ViewportToWorldPoint(new Vector3(p.x / SampleW, 1f - (p.y / SampleH), _worldCam.nearClipPlane + 0.6f));
            return _world.InverseTransformPoint(w);
        }

        /// <summary>카메라가 비추는 가운데의 샘플 px(월드) 자리.</summary>
        private Vector2 SCamCenter()
        {
            Vector3 g = GroundAt(new Vector3(0.5f, 0.5f, 0f));
            return new Vector2(g.x / SurvivorSim.Px, -g.y / SurvivorSim.Px);
        }

        // ------------------------------------------------------------------ 색
        private static Color C(Rgb c, float a)
        {
            return new Color(c.R / 255f, c.G / 255f, c.B / 255f, Mathf.Clamp01(a));
        }

        private static Color C(float r, float g, float b, float a = 1f)
        {
            return new Color(r / 255f, g / 255f, b / 255f, Mathf.Clamp01(a));
        }

        private static Color Hex(string hex, float a = 1f)
        {
            Color c;
            if (!ColorUtility.TryParseHtmlString(hex, out c)) c = Color.magenta;
            c.a = a;
            return c;
        }

        /// <summary>샘플 hue(t, off) = (t·140 + off) mod 360.</summary>
        private float SHue(float off = 0f)
        {
            return Mathf.Repeat((_sim.ST * 140f) + off, 360f);
        }

        /// <summary>CSS hsla(h, s%, l%, a).</summary>
        private static Color Hsla(float h, float s, float l, float a)
        {
            s /= 100f;
            l /= 100f;
            float q = s * Mathf.Min(l, 1f - l);
            float F(float n)
            {
                float k = Mathf.Repeat(n + (h / 30f), 12f);
                return l - (q * Mathf.Max(-1f, Mathf.Min(k - 3f, Mathf.Min(9f - k, 1f))));
            }
            return new Color(F(0f), F(8f), F(4f), Mathf.Clamp01(a));
        }

        /// <summary>샘플 hsl2rgb(h) 색에 알파(무지개 별·꽃가루).</summary>
        private static Color HslRgb(float h, float a, float sat = 1f, float l = 0.7f)
        {
            return Hsla(h, sat * 100f, l * 100f, a);
        }

        private static SampleCanvas.Paint Radial(float x0, float y0, float r0, float x1, float y1, float r1)
        {
            return SampleCanvas.CreateRadialGradient(x0, y0, r0, x1, y1, r1);
        }

        private static SampleCanvas.Paint Linear(float x0, float y0, float x1, float y1)
        {
            return SampleCanvas.CreateLinearGradient(x0, y0, x1, y1);
        }

        private const float Tau = Mathf.PI * 2f;

        // ------------------------------------------------------------------ 매 프레임
        /// <summary>Refresh의 풀 그리기 안에서: 숲 샘플 층을 그린다.</summary>
        private void DrawSample(float dt)
        {
            foreach (SampleLayer l in _sLayers) l.Canvas.Clear();
            if (!Free)
            {
                if (_hudName != null && _hudName.gameObject.activeSelf)
                {
                    foreach (Text t in new[] { _hudName, _hudLv, _hudBand, _crewTitle, _crewCount, _crewX }) t.gameObject.SetActive(false);
                }
                return;
            }
            // 흔들림·번쩍은 실제 시간으로 줄어든다(샘플 s.shake *= .002^real).
            _sim.SShake *= Mathf.Pow(0.002f, dt);

            Vector2 cam = SCamCenter();
            float w = ViewW;
            SampleCanvas g = _sg;

            // 바닥 층: 아이템 바닥 그림 → Lv6 어두움 → 광선 → 발밑 등급 고리.
            foreach (SampleItem it in _sim.SampleItems)
            {
                int lv = _sim.SampleLevel(it.Base);
                if (lv > 0) DrawItemGround(g, it, lv);
            }
            if (_sim.SDark > 0f)
            {
                g.FillStyle = C(8, 4, 20, _sim.SDark);
                g.FillRect(cam.x - (w / 2f) - 40f, cam.y - (ViewH / 2f / TiltCos) - 40f, w + 80f, (ViewH / TiltCos) + 80f);
            }
            DrawRays(g);
            DrawCrewGround(g);
            TierAura(g);

            // 공중 층: 아이템 몸·공중 그림 → 얼음덩이 → 파티클.
            SampleCanvas a = _sa;
            foreach (SampleItem it in _sim.SampleItems)
            {
                int lv = _sim.SampleLevel(it.Base);
                if (lv > 0) DrawItemAir(a, it, lv);
            }
            foreach (Enemy e in _sim.Enemies)
            {
                if (e.Dead || e.SFrozen <= 0f) continue;
                IceBlock(a, SurvivorSim.SX(e.Pos), SurvivorSim.SY(e.Pos) - e.AirZ, SurvivorSim.SR(e), _sim.ST);
            }
            DrawCrew(a);
            DrawParts(a);

            // 앞 층: 레벨업 머리 위 연출.
            DrawFxFront(_sf);

            // 화면 층: 번쩍 → HUD.
            if (_sim.SFlash > 0f)
            {
                _ss.FillStyle = C(_sim.SFlashColor, _sim.SFlash * 0.55f);
                _ss.FillRect(0f, 0f, SampleW, SampleH);
            }
            DrawSampleHud(_ss, dt);
            DrawCrewScreen(_ss);
        }

        private void FlushSample()
        {
            if (_worldCam == null) return;
            _sForward = _world.InverseTransformDirection(_worldCam.transform.forward);
            Vector3 camLocal = _world.InverseTransformPoint(_worldCam.transform.position);
            // 기운 카메라라 화면 아래쪽 땅이 가장 가깝다: 가운데 기준으로 당기면 아래 절반이 near에 잘린다. 화면 아래 바깥(그림이 넘치는 몫)을 기준으로 당긴다.
            Vector3 nearest = GroundAt(new Vector3(0.5f, -0.3f, 0f));
            _sNear = Mathf.Max(0f, Vector3.Dot(nearest - camLocal, _sForward) - (_worldCam.nearClipPlane + 0.8f));
            foreach (SampleLayer l in _sLayers)
            {
                System.Func<Vector2, Vector3> map = l.Kind == 0 ? (System.Func<Vector2, Vector3>)SGround : l.Kind == 1 ? SAir : SScreen;
                l.Canvas.Build(l.VectorMesh, map);
                l.Vector.enabled = l.Canvas.VertexCount > 0;
                BuildImages(l, map);
            }
        }

        private readonly Dictionary<Texture, List<SampleCanvas.ImageQuad>> _imgGroups = new Dictionary<Texture, List<SampleCanvas.ImageQuad>>();

        private void BuildImages(SampleLayer l, System.Func<Vector2, Vector3> map)
        {
            foreach (List<SampleCanvas.ImageQuad> q in _imgGroups.Values) q.Clear();
            foreach (SampleCanvas.ImageQuad q in l.Canvas.Images)
            {
                Texture t = q.Sprite.texture;
                if (!_imgGroups.TryGetValue(t, out List<SampleCanvas.ImageQuad> list))
                {
                    list = new List<SampleCanvas.ImageQuad>();
                    _imgGroups[t] = list;
                }
                list.Add(q);
            }
            int used = 0;
            foreach (KeyValuePair<Texture, List<SampleCanvas.ImageQuad>> kv in _imgGroups)
            {
                if (kv.Value.Count == 0) continue;
                if (used == l.Images.Count)
                {
                    var go = new GameObject("SampleImages" + l.Order);
                    go.transform.SetParent(_world, false);
                    var mf = go.AddComponent<MeshFilter>();
                    var mr = go.AddComponent<MeshRenderer>();
                    var mesh = new Mesh();
                    mesh.MarkDynamic();
                    mf.sharedMesh = mesh;
                    mr.sortingOrder = l.Order + 1;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    l.Images.Add(mr);
                    l.ImageMeshes.Add(mesh);
                }
                MeshRenderer r = l.Images[used];
                Mesh m = l.ImageMeshes[used];
                long key = ((long)kv.Key.GetInstanceID() << 8) | (uint)l.Order;
                if (!_sampleImgMats.TryGetValue(key, out Material mat))
                {
                    mat = new Material(_sampleMat) { mainTexture = kv.Key, renderQueue = 3005 + (l.Order * 10) };
                    _sampleImgMats[key] = mat;
                }
                r.sharedMaterial = mat;
                r.enabled = true;
                FillImageMesh(m, kv.Value, map);
                used++;
            }
            for (int i = used; i < l.Images.Count; i++) l.Images[i].enabled = false;
        }

        private static void FillImageMesh(Mesh m, List<SampleCanvas.ImageQuad> quads, System.Func<Vector2, Vector3> map)
        {
            m.Clear();
            int n = quads.Count;
            var v = new Vector3[n * 4];
            var uv = new Vector2[n * 4];
            var uv2 = new Vector2[n * 4];
            var col = new Color[n * 4];
            var tri = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                SampleCanvas.ImageQuad q = quads[i];
                int b = i * 4;
                v[b] = map(q.A);
                v[b + 1] = map(q.B);
                v[b + 2] = map(q.C);
                v[b + 3] = map(q.D);
                uv[b] = new Vector2(q.Uv.xMin, q.Uv.yMax);
                uv[b + 1] = new Vector2(q.Uv.xMax, q.Uv.yMax);
                uv[b + 2] = new Vector2(q.Uv.xMax, q.Uv.yMin);
                uv[b + 3] = new Vector2(q.Uv.xMin, q.Uv.yMin);
                float mode = q.Lighter ? 0f : 1f;
                for (int k = 0; k < 4; k++)
                {
                    uv2[b + k] = new Vector2(mode, 0f);
                    col[b + k] = q.Color;
                }
                tri[i * 6] = b;
                tri[(i * 6) + 1] = b + 1;
                tri[(i * 6) + 2] = b + 2;
                tri[(i * 6) + 3] = b;
                tri[(i * 6) + 4] = b + 2;
                tri[(i * 6) + 5] = b + 3;
            }
            m.vertices = v;
            m.uv = uv;
            m.uv2 = uv2;
            m.colors = col;
            m.triangles = tri;
            m.RecalculateBounds();
            m.bounds = new Bounds(m.bounds.center, m.bounds.size + (Vector3.one * 1000f));
        }

        // ------------------------------------------------------------------ base.js 그림
        private static void ShadowAt(SampleCanvas g, float x, float y, float r, float a = 0.3f)
        {
            g.FillStyle = new Color(0f, 0f, 0f, a);
            g.BeginPath();
            g.Ellipse(x, y, r, r * 0.38f, 0f, 0f, Tau);
            g.Fill();
        }

        // base.js:140-149
        private static void IceBlock(SampleCanvas g, float x, float y, float r, float t)
        {
            g.Save();
            g.Translate(x, y);
            float R = r * 1.45f;
            SampleCanvas.Paint gr = Linear(-R, -R, R, R);
            gr.AddColorStop(0f, C(220, 250, 255, 0.85f));
            gr.AddColorStop(1f, C(110, 200, 255, 0.55f));
            g.FillStyle = gr;
            g.StrokeStyle = C(240, 255, 255, 0.95f);
            g.LineWidth = 1.6f;
            g.BeginPath();
            g.MoveTo(-R, -R * 0.3f);
            g.LineTo(-R * 0.4f, -R * 1.05f);
            g.LineTo(R * 0.7f, -R * 0.9f);
            g.LineTo(R, R * 0.2f);
            g.LineTo(R * 0.3f, R);
            g.LineTo(-R * 0.8f, R * 0.75f);
            g.ClosePath();
            g.Fill();
            g.Stroke();
            g.FillStyle = new Color(1f, 1f, 1f, 0.7f);
            g.FillRect(-R * 0.5f, -R * 0.7f, R * 0.2f, R * 0.9f);
            g.Restore();
        }

        // base.js:171-191 + core.js:71-104 (별·꽃가루·무지개 고리·빛 알갱이)
        private readonly HashSet<SPart> _textShown = new HashSet<SPart>();

        private void DrawParts(SampleCanvas g)
        {
            float t = _sim.ST;
            foreach (SPart p in _sim.Parts)
            {
                float k = p.Age / p.Life;
                float y = p.Y - p.Z;
                switch (p.Kind)
                {
                    case SPartKind.Spark:
                        g.Lighter = true;
                        g.FillStyle = C(p.Color, 1f - k);
                        g.BeginPath();
                        g.Arc(p.X, y, p.Size * (1f - (k * 0.6f)), 0f, Tau);
                        g.Fill();
                        g.Lighter = false;
                        break;
                    case SPartKind.Smoke:
                        g.FillStyle = C(p.Color, 0.55f * (1f - k));
                        g.BeginPath();
                        g.Arc(p.X, y, p.Size * (1f + k), 0f, Tau);
                        g.Fill();
                        break;
                    case SPartKind.Powder:
                        g.FillStyle = new Color(1f, 1f, 1f, 0.75f * (1f - k));
                        g.BeginPath();
                        g.Arc(p.X, y, p.Size * (1f + (k * 1.5f)), 0f, Tau);
                        g.Fill();
                        break;
                    case SPartKind.Foam:
                        g.FillStyle = new Color(1f, 1f, 1f, 0.95f * (1f - (k * k)));
                        g.StrokeStyle = C(180, 210, 230, 0.8f * (1f - k));
                        g.LineWidth = 1f;
                        g.BeginPath();
                        g.Arc(p.X, y, p.Size * (1f - (k * 0.3f)), 0f, Tau);
                        g.Fill();
                        g.Stroke();
                        break;
                    case SPartKind.Ring:
                        g.StrokeStyle = p.HasColor ? C(p.Color, 1f - k) : Hsla(SHue(p.X * 3f), 100f, 70f, 1f - k);
                        g.LineWidth = (p.Width > 0f ? p.Width : 3f) * (1f - (k * 0.5f));
                        g.BeginPath();
                        g.Ellipse(p.X, y, p.Size * SurvivorSim.Ease(k), p.Size * SurvivorSim.Ease(k) * 0.62f, 0f, 0f, Tau);
                        g.Stroke();
                        break;
                    case SPartKind.Glow:
                    {
                        g.Lighter = true;
                        SampleCanvas.Paint gr = Radial(p.X, y, 0f, p.X, y, p.Size);
                        gr.AddColorStop(0f, C(p.Color, 0.8f * (1f - k)));
                        gr.AddColorStop(1f, C(p.Color, 0f));
                        g.FillStyle = gr;
                        g.BeginPath();
                        g.Arc(p.X, y, p.Size, 0f, Tau);
                        g.Fill();
                        g.Lighter = false;
                        break;
                    }
                    case SPartKind.Drop:
                        if (p.Z > 0f) ShadowAt(g, p.X, p.Y, p.Size * 0.8f, 0.15f);
                        g.FillStyle = C(110, 200, 255, 1f - (k * 0.5f));
                        g.BeginPath();
                        g.Arc(p.X, y, p.Size, 0f, Tau);
                        g.Fill();
                        g.FillStyle = new Color(1f, 1f, 1f, 0.8f);
                        g.BeginPath();
                        g.Arc(p.X - (p.Size * 0.3f), y - (p.Size * 0.3f), p.Size * 0.35f, 0f, Tau);
                        g.Fill();
                        break;
                    case SPartKind.Shard:
                        g.Save();
                        g.Translate(p.X, y);
                        g.Rotate(p.Rot);
                        g.FillStyle = C(200, 240, 255, 1f - (k * 0.5f));
                        g.StrokeStyle = new Color(1f, 1f, 1f, 0.9f);
                        g.LineWidth = 1f;
                        g.BeginPath();
                        g.MoveTo(0f, -p.Size);
                        g.LineTo(p.Size * 0.6f, p.Size * 0.5f);
                        g.LineTo(-p.Size * 0.6f, p.Size * 0.4f);
                        g.ClosePath();
                        g.Fill();
                        g.Stroke();
                        g.Restore();
                        break;
                    case SPartKind.Paw:
                        g.FillStyle = C(150, 215, 255, 0.6f * (1f - k));
                        g.BeginPath();
                        g.Arc(p.X, y, 2.4f, 0f, Tau);
                        g.Fill();
                        break;
                    case SPartKind.Frost:
                        g.FillStyle = C(210, 245, 255, 0.5f * (1f - k));
                        g.BeginPath();
                        g.Arc(p.X, y, p.Size, 0f, Tau);
                        g.Fill();
                        break;
                    case SPartKind.Text:
                        // 샘플 글자 파티클은 화면 글자(SpawnText)로 한 번 띄운다.
                        if (_textShown.Add(p)) SpawnText(SGround(new Vector2(p.X, y)) + new Vector3(0f, 0f, -0.5f), p.Str, C(p.Color, 1f), p.Size / 15f * 1.4f);
                        break;
                    case SPartKind.Star:
                    {
                        Color c = p.HasColor ? C(p.Color, 1f - k) : HslRgb(SHue(p.X * 3f), 1f - k);
                        g.Save();
                        g.Lighter = true;
                        g.Translate(p.X, y);
                        g.Rotate(p.Age * 6f);
                        float r = p.Size * (1f - (k * 0.5f));
                        g.FillStyle = c;
                        g.BeginPath();
                        for (int i = 0; i < 8; i++)
                        {
                            float a = i * Mathf.PI / 4f, rr = i % 2 == 1 ? r * 0.28f : r;
                            g.LineTo(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr);
                        }
                        g.ClosePath();
                        g.Fill();
                        g.FillStyle = new Color(1f, 1f, 1f, 1f - k);
                        g.BeginPath();
                        g.Arc(0f, 0f, r * 0.25f, 0f, Tau);
                        g.Fill();
                        g.Restore();
                        break;
                    }
                    case SPartKind.Confetti:
                    {
                        g.Save();
                        g.Translate(p.X, y);
                        g.Rotate(p.Rot + (p.Age * p.Vr));
                        g.Scale(1f, Mathf.Cos((p.Age * 9f) + p.Rot));
                        g.FillStyle = p.HasColor ? C(p.Color, 1f - (k * k)) : Hsla(SHue(p.Rot * 90f), 95f, 65f, 1f - (k * k));
                        g.FillRect(-p.Size, -p.Size * 0.45f, p.Size * 2f, p.Size * 0.9f);
                        g.Restore();
                        break;
                    }
                    case SPartKind.Prism:
                    {
                        g.Save();
                        g.Lighter = true;
                        for (int j = 0; j < 3; j++)
                        {
                            g.StrokeStyle = Hsla(SHue(j * 120f), 100f, 70f, (1f - k) * 0.9f);
                            g.LineWidth = 3f - (j * 0.6f);
                            float e = SurvivorSim.Ease(k);
                            g.BeginPath();
                            g.Ellipse(p.X, y, p.Size * e * (1f + (j * 0.18f)), p.Size * e * (1f + (j * 0.18f)) * 0.62f, 0f, 0f, Tau);
                            g.Stroke();
                        }
                        g.Restore();
                        break;
                    }
                    case SPartKind.Mote:
                    {
                        g.Save();
                        g.Lighter = true;
                        g.FillStyle = p.HasColor ? C(p.Color, 0.9f * Mathf.Min(1f, k * 4f)) : Hsla(SHue(p.Size * 40f), 100f, 75f, 0.9f * Mathf.Min(1f, k * 4f));
                        g.BeginPath();
                        g.Arc(p.X, y, p.Size, 0f, Tau);
                        g.Fill();
                        g.Restore();
                        break;
                    }
                }
            }
            if (_textShown.Count > 200) _textShown.RemoveWhere(q => !_sim.Parts.Contains(q));
        }

        // ------------------------------------------------------------------ core.js 등급 그림
        // core.js:25-40
        private void TierGlow(SampleCanvas g, float x, float y, float r, int lv)
        {
            Tier T = SurvivorSim.TierOf(lv);
            if (T.GlowA <= 0f) return;
            float t = _sim.ST;
            g.Save();
            g.Lighter = true;
            float R = r * (1.4f + (lv * 0.18f));
            SampleCanvas.Paint gr = Radial(x, y, 0f, x, y, R);
            gr.AddColorStop(0f, C(T.Glow, T.GlowA));
            gr.AddColorStop(1f, C(T.Glow, 0f));
            g.FillStyle = gr;
            g.BeginPath();
            g.Arc(x, y, R, 0f, Tau);
            g.Fill();
            if (T.Gold)
            {
                g.LineWidth = 1.6f;
                g.StrokeStyle = T.Rainbow ? Hsla(SHue(), 100f, 72f, 0.9f) : C(255, 214, 110, 0.85f);
                float a0 = (t * 4f) % Tau;
                g.BeginPath();
                g.Arc(x, y, r * 1.12f, a0, a0 + 4.2f);
                g.Stroke();
                g.StrokeStyle = T.Rainbow ? Hsla(SHue(180f), 100f, 75f, 0.8f) : C(255, 240, 190, 0.7f);
                float a1 = (-t * 3f) % Tau;
                g.BeginPath();
                g.Arc(x, y, r * 1.26f, a1, a1 + 3f);
                g.Stroke();
            }
            g.Restore();
        }

        // core.js:43-57
        private void TierStream(SampleCanvas g, float x0, float y0, float x1, float y1, float w, int lv, float arc = 14f)
        {
            Tier T = SurvivorSim.TierOf(lv);
            float t = _sim.ST;
            float mx = (x0 + x1) / 2f, my = ((y0 + y1) / 2f) - arc;
            void Path()
            {
                g.BeginPath();
                g.MoveTo(x0, y0);
                g.QuadraticCurveTo(mx, my, x1, y1);
            }
            g.Save();
            g.RoundCap = true;
            g.Lighter = true;
            g.StrokeStyle = C(T.Glow, 0.25f + (T.GlowA * 0.5f));
            g.LineWidth = w * (2f + (T.GlowA * 2f));
            Path();
            g.Stroke();
            if (T.Gold)
            {
                g.StrokeStyle = T.Rainbow ? Hsla(SHue(), 100f, 70f, 0.55f) : C(255, 205, 90, 0.5f);
                g.LineWidth = w * 1.55f;
                Path();
                g.Stroke();
            }
            g.Lighter = false;
            SampleCanvas.Paint lg = Linear(x0, y0, x1, y1);
            lg.AddColorStop(0f, C(T.Core, 0.95f));
            lg.AddColorStop(1f, C(200, 240, 255, 0.92f));
            g.StrokeStyle = lg;
            g.LineWidth = w;
            Path();
            g.Stroke();
            if (T.White > 0f)
            {
                g.Lighter = true;
                g.StrokeStyle = new Color(1f, 1f, 1f, T.White);
                g.LineWidth = w * 0.38f;
                Path();
                g.Stroke();
                g.Lighter = false;
            }
            g.StrokeStyle = new Color(1f, 1f, 1f, 0.85f);
            g.LineWidth = Mathf.Max(1f, w * 0.22f);
            g.SetLineDash(new[] { 6f, 9f });
            g.LineDashOffset = -t * (220f + (lv * 60f));
            Path();
            g.Stroke();
            g.SetLineDash(null);
            g.Restore();
        }

        // core.js:240-251
        private void StarShape(SampleCanvas g, float x, float y, float r, string kind, float t)
        {
            g.Save();
            g.Translate(x, y);
            if (kind != "off")
            {
                g.Lighter = true;
                SampleCanvas.Paint gl = Radial(0f, 0f, 0f, 0f, 0f, r * 2.2f);
                gl.AddColorStop(0f, kind == "gold" ? C(255, 210, 100, 0.6f) : kind == "rainbow" ? Hsla(Mathf.Repeat((t * 140f), 360f), 100f, 70f, 0.7f) : C(120, 200, 255, 0.55f));
                gl.AddColorStop(1f, new Color(0f, 0f, 0f, 0f));
                g.FillStyle = gl;
                g.BeginPath();
                g.Arc(0f, 0f, r * 2.2f, 0f, Tau);
                g.Fill();
                g.Lighter = false;
            }
            g.BeginPath();
            for (int i = 0; i < 10; i++)
            {
                float a = (-Mathf.PI / 2f) + (i * Mathf.PI / 5f), rr = i % 2 == 1 ? r * 0.45f : r;
                g.LineTo(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr);
            }
            g.ClosePath();
            SampleCanvas.Paint gr = Linear(0f, -r, 0f, r);
            if (kind == "gold")
            {
                gr.AddColorStop(0f, Hex("#fff6c0"));
                gr.AddColorStop(1f, Hex("#e09a10"));
            }
            else if (kind == "rainbow")
            {
                gr.AddColorStop(0f, Hsla(Mathf.Repeat(t * 140f, 360f), 100f, 80f, 1f));
                gr.AddColorStop(1f, Hsla(Mathf.Repeat((t * 140f) + 160f, 360f), 100f, 60f, 1f));
            }
            else if (kind == "blue")
            {
                gr.AddColorStop(0f, Hex("#e6f6ff"));
                gr.AddColorStop(1f, Hex("#3d8bff"));
            }
            else
            {
                gr.AddColorStop(0f, C(60, 70, 90, 0.8f));
                gr.AddColorStop(1f, C(30, 35, 50, 0.8f));
            }
            g.FillStyle = gr;
            g.Fill();
            g.LineWidth = 1.2f;
            g.StrokeStyle = C(10, 10, 25, 0.85f);
            g.Stroke();
            g.Restore();
        }

        /// <summary>아이템 아이콘(샘플 it.icon을 구운 그림): 가운데 x,y, 반지름 r.</summary>
        private void Icon(SampleCanvas g, UpgradeId id, float x, float y, float r, int lv)
        {
            string name = SurvivorUpgrades.IconOf(id);
            if (name == null) return;
            Sprite s = Art.Get("LevelUp/icon_" + name + (lv >= 5 ? "_6" : "_1"));
            float size = r * 2.5f;
            g.DrawImage(s, x - (size / 2f), y - (size / 2f), size, size);
        }

        /// <summary>샘플 metalText를 구운 그림으로(글자 크기 size px, 가운데 x,y).</summary>
        private static void MetalText(SampleCanvas g, string sprite, float x, float y, float size, float alpha = 1f, float wide = 4f)
        {
            Sprite s = Art.Get("LevelUp/" + sprite);
            if (s == null) return;
            float h = size * 2f, w = h * wide;
            float keep = g.GlobalAlpha;
            g.GlobalAlpha = alpha;
            g.DrawImage(s, x - (w / 2f), y - (h / 2f), w, h);
            g.GlobalAlpha = keep;
        }

        // core.js:253-259
        private void Card(SampleCanvas g, UpgradeId id, float x, float y, float sc, float rot)
        {
            g.Save();
            g.Translate(x, y);
            g.Rotate(Mathf.Sin(rot) * 0.2f);
            g.Scale(sc, sc);
            SampleCanvas.Paint gr = Linear(0f, -18f, 0f, 18f);
            gr.AddColorStop(0f, Hex("#2f4f86"));
            gr.AddColorStop(1f, Hex("#16274a"));
            g.FillStyle = gr;
            g.StrokeStyle = Hex("#bfe3ff");
            g.LineWidth = 2f;
            g.BeginPath();
            g.RoundRect(-15f, -18f, 30f, 36f, 6f);
            g.Fill();
            g.Stroke();
            Icon(g, id, 0f, -1f, 11f, 1);
            g.Restore();
        }

        // ------------------------------------------------------------------ core.js 레벨업 그림
        private static readonly int[] RayN = { 0, 8, 12, 16, 20, 28, 40 };
        private static readonly float[] RayLifeT = { 0f, 1f, 1.1f, 1.2f, 1.3f, 1.7f, 2.6f };

        // core.js:175-194
        private void DrawRays(SampleCanvas g)
        {
            SLevelFx f = _sim.LevelFx;
            if (f == null) return;
            int lv = f.Lv;
            float x = _sim.PX, y = _sim.PY - 8f;
            float life = RayLifeT[lv];
            float start = lv == 1 ? 0.35f : lv == 6 ? f.BoomAt : 0f;
            float a = f.Age - start;
            if (a < 0f || a > life) return;
            float k = a / life, alpha = (a < 0.12f ? a / 0.12f : 1f) * (1f - (k * k));
            int n = RayN[lv];
            float len = (80f + (lv * 40f)) * (0.6f + (SurvivorSim.Ease(a / 0.3f) * 0.4f)) * (lv == 6 ? 2.6f : 1f);
            g.Save();
            g.Lighter = true;
            g.Translate(x, y);
            g.Rotate(f.Age * (0.6f + (lv * 0.15f)));
            for (int i = 0; i < n; i++)
            {
                float ang = i / (float)n * Tau, wdt = lv >= 5 ? 0.09f : 0.07f;
                Color c = lv == 6 ? HslRgb(SHue(i * 360f / n), 1f) : lv == 5 ? (i % 2 == 1 ? C(255, 214, 110) : C(255, 248, 220)) : (i % 2 == 1 ? C(SurvivorSim.TierOf(lv).Core, 1f) : C(235, 248, 255));
                SampleCanvas.Paint gr = Linear(0f, 0f, Mathf.Cos(ang) * len, Mathf.Sin(ang) * len);
                gr.AddColorStop(0f, new Color(c.r, c.g, c.b, (lv == 6 ? 0.45f : 0.75f) * alpha));
                gr.AddColorStop(1f, new Color(c.r, c.g, c.b, 0f));
                g.FillStyle = gr;
                g.BeginPath();
                g.MoveTo(0f, 0f);
                g.LineTo(Mathf.Cos(ang - wdt) * len, Mathf.Sin(ang - wdt) * len * 0.8f);
                g.LineTo(Mathf.Cos(ang + wdt) * len, Mathf.Sin(ang + wdt) * len * 0.8f);
                g.ClosePath();
                g.Fill();
            }
            g.Restore();
        }

        // core.js:197-239
        private void DrawFxFront(SampleCanvas g)
        {
            SLevelFx f = _sim.LevelFx;
            if (f == null) return;
            int lv = f.Lv;
            float px = _sim.PX, py = _sim.PY, t = _sim.ST;
            Vector2 cam = SCamCenter();
            // Lv1: 카드가 화면 아래(샘플 화면 W/2, H+30)에서 날아온다.
            if (f.Fly && f.Age < 0.35f)
            {
                float sx = cam.x, sy = cam.y + (((ViewH / 2f) + 30f) / TiltCos);
                float u = SurvivorSim.Ease(f.Age / 0.35f);
                float x = Mathf.Lerp(sx, px, u), y = Mathf.Lerp(sy, py - 30f, u) - (Mathf.Sin(u * Mathf.PI) * 60f);
                for (int i = 1; i <= 8; i++)
                {
                    float uu = SurvivorSim.Ease(Mathf.Max(0f, f.Age - (i * 0.02f)) / 0.35f);
                    float tx = Mathf.Lerp(sx, px, uu), ty = Mathf.Lerp(sy, py - 30f, uu) - (Mathf.Sin(uu * Mathf.PI) * 60f);
                    g.Save();
                    g.Lighter = true;
                    g.FillStyle = C(150, 215, 255, 0.5f * (1f - (i / 9f)));
                    g.BeginPath();
                    g.Arc(tx, ty, 12f - i, 0f, Tau);
                    g.Fill();
                    g.Restore();
                }
                Card(g, f.Item, x, y, 1.1f, f.Age * 14f);
            }
            float a = f.Age - (lv == 1 ? 0.35f : lv == 6 ? f.BoomAt : 0f);
            float show = lv == 6 ? 1.8f : 1.25f;
            if (a > 0f && a < show)
            {
                float pop = a < 0.14f ? SurvivorSim.Ease(a / 0.14f) * 1.3f : a < 0.26f ? Mathf.Lerp(1.3f, 1f, (a - 0.14f) / 0.12f) : 1f;
                float alpha = a > show - 0.3f ? (show - a) / 0.3f : 1f;
                g.Save();
                g.GlobalAlpha = alpha;
                g.Translate(px, py - 52f - (a * 6f));
                g.Scale(pop, pop);
                TierGlow(g, 0f, 0f, 13f, Mathf.Max(3, lv));
                Icon(g, f.Item, 0f, 0f, 12f, lv);
                for (int i = 1; i <= 6; i++)
                {
                    float sx = (i - 3.5f) * 11f, sy = 21f;
                    bool on = i <= lv, just = i == lv;
                    float sc = just ? 1f + (Mathf.Max(0f, 0.6f - a) * 1.5f) : 1f;
                    StarShape(g, sx, sy, (i == 6 ? 5.4f : 4.4f) * sc, on ? (i == 6 ? "rainbow" : lv >= 5 ? "gold" : "blue") : "off", t + i);
                }
                g.Restore();
                string label = lv == 6 ? null : lv == 5 ? "txt_lv5max" : lv == 1 ? "txt_new" : "txt_lv" + lv;
                if (label != null) MetalText(g, label, px, py - 92f - (a * 8f), 18f + (lv * 2.2f), alpha);
                if (lv == 5 && a > 0.35f) MetalText(g, "txt_next_top", px, py + 34f, 11f, alpha * Mathf.Min(1f, (a - 0.35f) * 4f));
            }
            // Lv6 이름 띠(화면 가운데).
            if (lv == 6 && a > 0f && a < 1.8f)
            {
                SampleCanvas s = _ss;
                float W = SampleW, H = SampleH;
                float k = a < 0.2f ? SurvivorSim.Ease(a / 0.2f) : 1f, alpha = a > 1.4f ? (1.8f - a) / 0.4f : 1f;
                s.Save();
                s.GlobalAlpha = alpha;
                float bh = 54f * k;
                SampleCanvas.Paint bg = Linear(0f, (H / 2f) - (bh / 2f), 0f, (H / 2f) + (bh / 2f));
                bg.AddColorStop(0f, C(20, 8, 40, 0f));
                bg.AddColorStop(0.5f, C(20, 8, 40, 0.85f));
                bg.AddColorStop(1f, C(20, 8, 40, 0f));
                s.FillStyle = bg;
                s.FillRect(0f, (H / 2f) - (bh / 2f), W, bh);
                s.Lighter = true;
                foreach (float yy in new[] { (H / 2f) - (bh / 2f) + 3f, (H / 2f) + (bh / 2f) - 3f })
                {
                    SampleCanvas.Paint lg = Linear(0f, 0f, W, 0f);
                    for (int i = 0; i <= 6; i++) lg.AddColorStop(i / 6f, Hsla(SHue(i * 60f), 100f, 70f, 0.9f));
                    s.FillStyle = lg;
                    s.FillRect(0f, yy - 1f, W * k, 2f);
                }
                s.Restore();
                MetalText(s, "txt_top", W / 2f, (H / 2f) - 13f, 12f, alpha);
                string icon = SurvivorUpgrades.IconOf(f.Item);
                if (icon != null) MetalText(s, "name_" + icon, (W / 2f) + ((1f - k) * 200f), (H / 2f) + 8f, 28f, alpha, 6f);
            }
        }

        // core.js:351-361
        private void TierAura(SampleCanvas g)
        {
            int lv = _sim.SampleLevel(_hudItem);
            if (lv < 3) return;
            SampleItem it = _sim.SampleItemOf(_hudItem);
            float surge = it != null ? it.Surge : 0f;
            float px = _sim.PX, py = _sim.PY;
            Tier T = SurvivorSim.TierOf(lv);
            g.Save();
            g.Lighter = true;
            float r = 14f + (lv * 2f) + (surge * 10f);
            g.StrokeStyle = T.Rainbow ? Hsla(SHue(), 100f, 70f, 0.8f) : T.Gold ? C(255, 214, 110, 0.75f) : C(T.Glow, 0.55f);
            g.LineWidth = 2f;
            g.BeginPath();
            g.Ellipse(px, py + 8f, r, r * 0.4f, 0f, 0f, Tau);
            g.Stroke();
            if (lv >= 5)
            {
                g.LineWidth = 1f;
                g.BeginPath();
                g.Ellipse(px, py + 8f, r + 5f, (r + 5f) * 0.4f, 0f, _sim.ST * 3f, (_sim.ST * 3f) + 4f);
                g.Stroke();
            }
            g.Restore();
        }

        // core.js:363-378(처치 속도 막대는 샘플 비교용이라 뺀다)
        private void DrawSampleHud(SampleCanvas g, float dt)
        {
            if (_sim.LevelFx != null)
            {
                if (_hudItem != Loadout.BaseOf(_sim.LevelFx.Item) || _sim.LevelFx.Age < dt * 2f) _hudBandAge = 0f;
                _hudItem = Loadout.BaseOf(_sim.LevelFx.Item);
            }
            _hudBandAge += dt;
            int lv = _sim.SampleLevel(_hudItem);
            bool show = lv > 0 && _sim.PendingChoices == null;
            _hudName.gameObject.SetActive(show);
            _hudLv.gameObject.SetActive(show);
            if (!show)
            {
                _hudBand.gameObject.SetActive(false);
                return;
            }
            float t = _sim.ST;
            g.FillStyle = C(10, 14, 28, 0.78f);
            g.BeginPath();
            g.RoundRect(6f, 6f, 168f, 40f, 9f);
            g.Fill();
            g.StrokeStyle = lv >= 6 ? Hsla(SHue(), 100f, 70f, 0.9f) : lv >= 5 ? C(255, 214, 110, 0.9f) : C(150, 200, 255, 0.35f);
            g.LineWidth = 1.5f;
            g.Stroke();
            TierGlow(g, 26f, 26f, 12f, lv);
            Icon(g, _hudItem, 26f, 26f, 12f, lv);
            for (int i = 1; i <= 6; i++) StarShape(g, 48f + ((i - 1) * 13f), 33f, i == 6 ? 5.2f : 4.6f, i <= lv ? (i == 6 ? "rainbow" : lv >= 5 ? "gold" : "blue") : "off", t + i);
            UpgradeId shown = lv >= 6 ? (Loadout.EvolutionOf(_hudItem) ?? _hudItem) : _hudItem;
            _hudName.text = SurvivorUpgrades.Name(shown);
            _hudName.color = lv >= 6 ? Hex("#ffe9a8") : Color.white;
            _hudLv.text = lv >= 6 ? "최고급" : "Lv" + lv;
            // 아래 설명 띠: 레벨업 뒤 4초.
            string txt = _hudBandAge < 4f ? SurvivorUpgrades.DescribeFree(lv >= 6 ? shown : _hudItem, lv) : null;
            _hudBand.gameObject.SetActive(txt != null);
            if (txt != null)
            {
                _hudBand.text = txt;
                _hudBand.color = lv >= 6 ? Hex("#ffe9a8") : Hex("#e8f4ff");
                float tw = Mathf.Min(SampleW - 40f, (txt.Length * 11f) + 20f);
                g.FillStyle = C(10, 14, 28, 0.72f);
                g.BeginPath();
                g.RoundRect((SampleW / 2f) - (tw / 2f), SampleH - 26f, tw, 20f, 8f);
                g.Fill();
            }
        }

        // ------------------------------------------------------------------ 아이템 그림(SurvivorView.SampleDraw*.cs가 채운다)
        private void DrawItemGround(SampleCanvas g, SampleItem it, int lv)
        {
            DrawGroundA(g, it, lv);
            DrawGroundB(g, it, lv);
            DrawGroundC(g, it, lv);
        }

        private void DrawItemAir(SampleCanvas g, SampleItem it, int lv)
        {
            DrawAirA(g, it, lv);
            DrawAirB(g, it, lv);
            DrawAirC(g, it, lv);
        }

        partial void DrawGroundA(SampleCanvas g, SampleItem it, int lv);
        partial void DrawGroundB(SampleCanvas g, SampleItem it, int lv);
        partial void DrawGroundC(SampleCanvas g, SampleItem it, int lv);
        partial void DrawAirA(SampleCanvas g, SampleItem it, int lv);
        partial void DrawAirB(SampleCanvas g, SampleItem it, int lv);
        partial void DrawAirC(SampleCanvas g, SampleItem it, int lv);
    }
}
