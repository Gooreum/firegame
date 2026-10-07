using System.Collections.Generic;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 웹 캔버스 그리기를 그대로 받는 붓(2026-10-08). 사용자가 승인한 레벨업 샘플(tools/levelup-art/*.js)의 그리기 코드를
    /// 줄 단위로 옮기려고, 샘플이 쓰는 캔버스 기능만 같은 이름·같은 뜻으로 둔다:
    /// 경로(moveTo·lineTo·quadraticCurveTo·bezierCurveTo·arc·ellipse·roundRect) → fill/stroke, 선형·방사 그라데이션,
    /// lineWidth·lineCap·setLineDash·lineDashOffset, globalAlpha, 'lighter'(빛 겹침), save/restore/translate/rotate/scale.
    /// 좌표는 샘플 px(월드 1칸 = 1/Px px, y는 아래가 +). 매 프레임 메시로 다시 만들어 층마다 한 번에 그린다.
    /// </summary>
    public sealed class SampleCanvas
    {
        // ------------------------------------------------------------------ 칠
        public sealed class Paint
        {
            public enum Kind { Solid, Linear, Radial }

            public Kind Type;
            public Color Color;
            public Vector2 P0, P1;
            public float R0, R1;
            public readonly List<float> Offsets = new List<float>(4);
            public readonly List<Color> Colors = new List<Color>(4);

            public Paint AddColorStop(float offset, Color c)
            {
                Offsets.Add(offset);
                Colors.Add(c);
                return this;
            }

            public static implicit operator Paint(Color c)
            {
                return new Paint { Type = Kind.Solid, Color = c };
            }

            public Color At(Vector2 p)
            {
                if (Type == Kind.Solid || Offsets.Count == 0) return Type == Kind.Solid ? Color : Color.clear;
                float t;
                if (Type == Kind.Linear)
                {
                    Vector2 d = P1 - P0;
                    float l2 = Vector2.Dot(d, d);
                    t = l2 > 1e-6f ? Vector2.Dot(p - P0, d) / l2 : 0f;
                }
                else
                {
                    t = RadialT(p);
                }
                return Stop(t);
            }

            /// <summary>두 원 방사 그라데이션의 t(캔버스 정의: 점을 지나는 가장 큰 원의 t).</summary>
            private float RadialT(Vector2 p)
            {
                Vector2 cd = P1 - P0;
                float dr = R1 - R0;
                Vector2 pd = p - P0;
                float a = Vector2.Dot(cd, cd) - (dr * dr);
                float b = Vector2.Dot(pd, cd) + (R0 * dr);
                float c = Vector2.Dot(pd, pd) - (R0 * R0);
                if (Mathf.Abs(a) < 1e-5f)
                {
                    if (Mathf.Abs(b) < 1e-6f) return 0f;
                    return c / (2f * b);
                }
                float disc = (b * b) - (a * c);
                if (disc < 0f) return 0f;
                float s = Mathf.Sqrt(disc);
                float t1 = (b + s) / a;
                float t2 = (b - s) / a;
                float t = Mathf.Max(t1, t2);
                if (R0 + (t * dr) < 0f) t = Mathf.Min(t1, t2);
                return t;
            }

            private Color Stop(float t)
            {
                if (t <= Offsets[0]) return Colors[0];
                int n = Offsets.Count;
                if (t >= Offsets[n - 1]) return Colors[n - 1];
                for (int i = 1; i < n; i++)
                {
                    if (t <= Offsets[i])
                    {
                        float span = Offsets[i] - Offsets[i - 1];
                        float k = span > 1e-6f ? (t - Offsets[i - 1]) / span : 1f;
                        return Color.Lerp(Colors[i - 1], Colors[i], k);
                    }
                }
                return Colors[n - 1];
            }

            /// <summary>변환을 미리 적용한 사본(그라데이션 좌표는 칠하는 순간의 변환을 따른다).</summary>
            public Paint Transformed(Matrix m)
            {
                if (Type == Kind.Solid) return this;
                var q = new Paint { Type = Type, P0 = m.Apply(P0), P1 = m.Apply(P1), R0 = R0 * m.Scale, R1 = R1 * m.Scale };
                q.Offsets.AddRange(Offsets);
                q.Colors.AddRange(Colors);
                return q;
            }

            /// <summary>방사 칠의 경계 반지름들(링으로 나눠 그릴 자리).</summary>
            public bool IsGradient
            {
                get { return Type != Kind.Solid; }
            }
        }

        /// <summary>2×3 아핀 변환.</summary>
        public struct Matrix
        {
            public float A, B, C, D, E, F;

            public static readonly Matrix Identity = new Matrix { A = 1f, D = 1f };

            public Vector2 Apply(Vector2 p)
            {
                return new Vector2((A * p.x) + (C * p.y) + E, (B * p.x) + (D * p.y) + F);
            }

            public Vector2 Apply(float x, float y)
            {
                return new Vector2((A * x) + (C * y) + E, (B * x) + (D * y) + F);
            }

            public float Scale
            {
                get { return Mathf.Sqrt(Mathf.Abs((A * D) - (B * C))); }
            }

            public Matrix Mul(Matrix o)
            {
                // this × o (o를 먼저 적용)
                return new Matrix
                {
                    A = (A * o.A) + (C * o.B),
                    B = (B * o.A) + (D * o.B),
                    C = (A * o.C) + (C * o.D),
                    D = (B * o.C) + (D * o.D),
                    E = (A * o.E) + (C * o.F) + E,
                    F = (B * o.E) + (D * o.F) + F,
                };
            }
        }

        private struct State
        {
            public Matrix M;
            public Paint Fill, StrokeP;
            public float LineWidth, Alpha, DashOffset;
            public bool Lighter, RoundCap, RoundJoin;
            public float[] Dash;
        }

        // ------------------------------------------------------------------ 상태(캔버스와 같은 이름)
        public Paint FillStyle = Color.black;
        public Paint StrokeStyle = Color.black;
        public float LineWidth = 1f;
        public float GlobalAlpha = 1f;
        public bool Lighter;
        public bool RoundCap;
        public bool RoundJoin;
        public float[] LineDash;
        public float LineDashOffset;
        private Matrix _m = Matrix.Identity;
        private readonly Stack<State> _stack = new Stack<State>();

        public Matrix Transform
        {
            get { return _m; }
            set { _m = value; }
        }

        public void Save()
        {
            _stack.Push(new State { M = _m, Fill = FillStyle, StrokeP = StrokeStyle, LineWidth = LineWidth, Alpha = GlobalAlpha, Lighter = Lighter, RoundCap = RoundCap, RoundJoin = RoundJoin, Dash = LineDash, DashOffset = LineDashOffset });
        }

        public void Restore()
        {
            if (_stack.Count == 0) return;
            State s = _stack.Pop();
            _m = s.M;
            FillStyle = s.Fill;
            StrokeStyle = s.StrokeP;
            LineWidth = s.LineWidth;
            GlobalAlpha = s.Alpha;
            Lighter = s.Lighter;
            RoundCap = s.RoundCap;
            RoundJoin = s.RoundJoin;
            LineDash = s.Dash;
            LineDashOffset = s.DashOffset;
        }

        public void Translate(float x, float y)
        {
            _m = _m.Mul(new Matrix { A = 1f, D = 1f, E = x, F = y });
        }

        public void Rotate(float a)
        {
            float c = Mathf.Cos(a);
            float s = Mathf.Sin(a);
            _m = _m.Mul(new Matrix { A = c, B = s, C = -s, D = c });
        }

        public void Scale(float sx, float sy)
        {
            _m = _m.Mul(new Matrix { A = sx, D = sy });
        }

        public void SetLineDash(float[] dash)
        {
            LineDash = dash != null && dash.Length > 0 ? dash : null;
        }

        public static Paint CreateLinearGradient(float x0, float y0, float x1, float y1)
        {
            return new Paint { Type = Paint.Kind.Linear, P0 = new Vector2(x0, y0), P1 = new Vector2(x1, y1) };
        }

        public static Paint CreateRadialGradient(float x0, float y0, float r0, float x1, float y1, float r1)
        {
            return new Paint { Type = Paint.Kind.Radial, P0 = new Vector2(x0, y0), R0 = r0, P1 = new Vector2(x1, y1), R1 = r1 };
        }

        // ------------------------------------------------------------------ 경로
        private readonly List<List<Vector2>> _subs = new List<List<Vector2>>();
        private readonly List<bool> _closed = new List<bool>();
        private List<Vector2> _cur;
        private Vector2 _last;
        private readonly Stack<List<Vector2>> _listPool = new Stack<List<Vector2>>();

        private List<Vector2> NewList()
        {
            if (_listPool.Count > 0)
            {
                List<Vector2> l = _listPool.Pop();
                l.Clear();
                return l;
            }
            return new List<Vector2>(32);
        }

        public void BeginPath()
        {
            foreach (List<Vector2> l in _subs) _listPool.Push(l);
            _subs.Clear();
            _closed.Clear();
            _cur = null;
        }

        public void MoveTo(float x, float y)
        {
            _cur = NewList();
            _subs.Add(_cur);
            _closed.Add(false);
            _last = new Vector2(x, y);
            _cur.Add(_m.Apply(x, y));
        }

        public void LineTo(float x, float y)
        {
            if (_cur == null)
            {
                MoveTo(x, y);
                return;
            }
            _last = new Vector2(x, y);
            _cur.Add(_m.Apply(x, y));
        }

        public void ClosePath()
        {
            if (_cur == null) return;
            _closed[_closed.Count - 1] = true;
            if (_cur.Count > 0)
            {
                Vector2 first = _cur[0];
                _cur = NewList();
                _subs.Add(_cur);
                _closed.Add(false);
                _cur.Add(first);
            }
        }

        private int Segs(float lengthPx)
        {
            float px = lengthPx * _m.Scale;
            return Mathf.Clamp(Mathf.CeilToInt(px / 3f), 4, 64);
        }

        public void QuadraticCurveTo(float cx, float cy, float x, float y)
        {
            if (_cur == null) MoveTo(cx, cy);
            Vector2 p0 = _last;
            var c = new Vector2(cx, cy);
            var p1 = new Vector2(x, y);
            int n = Segs(Vector2.Distance(p0, c) + Vector2.Distance(c, p1));
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n;
                float u = 1f - t;
                Vector2 p = (u * u * p0) + (2f * u * t * c) + (t * t * p1);
                _cur.Add(_m.Apply(p));
            }
            _last = p1;
        }

        public void BezierCurveTo(float c1x, float c1y, float c2x, float c2y, float x, float y)
        {
            if (_cur == null) MoveTo(c1x, c1y);
            Vector2 p0 = _last;
            var c1 = new Vector2(c1x, c1y);
            var c2 = new Vector2(c2x, c2y);
            var p1 = new Vector2(x, y);
            int n = Segs(Vector2.Distance(p0, c1) + Vector2.Distance(c1, c2) + Vector2.Distance(c2, p1));
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n;
                float u = 1f - t;
                Vector2 p = (u * u * u * p0) + (3f * u * u * t * c1) + (3f * u * t * t * c2) + (t * t * t * p1);
                _cur.Add(_m.Apply(p));
            }
            _last = p1;
        }

        public void Arc(float x, float y, float r, float a0, float a1, bool ccw = false)
        {
            Ellipse(x, y, r, r, 0f, a0, a1, ccw);
        }

        public void Ellipse(float x, float y, float rx, float ry, float rot, float a0, float a1, bool ccw = false)
        {
            float sweep = a1 - a0;
            const float Tau = Mathf.PI * 2f;
            if (!ccw)
            {
                if (sweep >= Tau) sweep = Tau;
                else
                {
                    sweep %= Tau;
                    if (sweep < 0f) sweep += Tau;
                }
            }
            else
            {
                if (-sweep >= Tau) sweep = -Tau;
                else
                {
                    sweep %= Tau;
                    if (sweep > 0f) sweep -= Tau;
                }
            }
            int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(sweep) * Mathf.Max(rx, ry) * _m.Scale / 3f), 6, 72);
            float cr = Mathf.Cos(rot);
            float sr = Mathf.Sin(rot);
            for (int i = 0; i <= n; i++)
            {
                float a = a0 + (sweep * i / n);
                float ex = Mathf.Cos(a) * rx;
                float ey = Mathf.Sin(a) * ry;
                float px = x + (ex * cr) - (ey * sr);
                float py = y + (ex * sr) + (ey * cr);
                if (i == 0 && _cur == null) MoveTo(px, py);
                else if (i == 0) LineTo(px, py);
                else
                {
                    _cur.Add(_m.Apply(px, py));
                    _last = new Vector2(px, py);
                }
            }
        }

        public void Rect(float x, float y, float w, float h)
        {
            MoveTo(x, y);
            LineTo(x + w, y);
            LineTo(x + w, y + h);
            LineTo(x, y + h);
            ClosePath();
        }

        public void RoundRect(float x, float y, float w, float h, float r)
        {
            r = Mathf.Max(0f, Mathf.Min(r, Mathf.Min(Mathf.Abs(w), Mathf.Abs(h)) / 2f));
            const float H = Mathf.PI / 2f;
            MoveTo(x + r, y);
            LineTo(x + w - r, y);
            Ellipse(x + w - r, y + r, r, r, 0f, -H, 0f);
            LineTo(x + w, y + h - r);
            Ellipse(x + w - r, y + h - r, r, r, 0f, 0f, H);
            LineTo(x + r, y + h);
            Ellipse(x + r, y + h - r, r, r, 0f, H, Mathf.PI);
            LineTo(x, y + r);
            Ellipse(x + r, y + r, r, r, 0f, Mathf.PI, Mathf.PI * 1.5f);
            ClosePath();
        }

        // ------------------------------------------------------------------ 그리기
        public void Fill()
        {
            Paint p = FillStyle.Transformed(_m);
            for (int i = 0; i < _subs.Count; i++)
            {
                List<Vector2> s = _subs[i];
                if (s.Count >= 3) FillPolygon(s, p);
            }
        }

        public void FillRect(float x, float y, float w, float h)
        {
            BeginPath();
            Rect(x, y, w, h);
            Fill();
            BeginPath();
        }

        public void Stroke()
        {
            Paint p = StrokeStyle.Transformed(_m);
            float w = LineWidth * _m.Scale;
            float[] dash = null;
            if (LineDash != null)
            {
                dash = new float[LineDash.Length];
                for (int i = 0; i < dash.Length; i++) dash[i] = LineDash[i] * _m.Scale;
            }
            for (int i = 0; i < _subs.Count; i++)
            {
                List<Vector2> s = _subs[i];
                bool closed = _closed[i];
                if (s.Count < 2) continue;
                if (closed) s.Add(s[0]);
                if (dash != null) StrokeDashed(s, w, p, dash, LineDashOffset * _m.Scale);
                else StrokePolyline(s, w, p, RoundCap && !closed);
                if (closed) s.RemoveAt(s.Count - 1);
            }
        }

        /// <summary>그림 한 장(샘플 drawImage: 왼쪽 위 x,y에서 w×h). 이미지 층에 따로 쌓인다.</summary>
        public void DrawImage(Sprite sprite, float x, float y, float w, float h)
        {
            if (sprite == null) return;
            Vector2 a = _m.Apply(x, y);
            Vector2 b = _m.Apply(x + w, y);
            Vector2 c = _m.Apply(x + w, y + h);
            Vector2 d = _m.Apply(x, y + h);
            Rect uv = UvOf(sprite);
            float alpha = GlobalAlpha;
            _images.Add(new ImageQuad { Sprite = sprite, A = a, B = b, C = c, D = d, Uv = uv, Color = new Color(1f, 1f, 1f, alpha), Lighter = Lighter });
        }

        private static Rect UvOf(Sprite s)
        {
            // textureRect는 투명 가장자리를 잘라 낸 영역일 수 있다: 그림 전체(rect)를 쓴다(구운 그림은 아틀라스가 아니다).
            Texture t = s.texture;
            Rect r = s.rect;
            return new Rect(r.x / t.width, r.y / t.height, r.width / t.width, r.height / t.height);
        }

        // ------------------------------------------------------------------ 메시 쌓기
        private readonly List<Vector2> _pos = new List<Vector2>(8192);
        private readonly List<Color> _col = new List<Color>(8192);
        private readonly List<int> _tri = new List<int>(16384);
        private readonly List<float> _mode = new List<float>(8192);

        public struct ImageQuad
        {
            public Sprite Sprite;
            public Vector2 A, B, C, D;
            public Rect Uv;
            public Color Color;
            public bool Lighter;
        }

        private readonly List<ImageQuad> _images = new List<ImageQuad>(64);

        public int VertexCount
        {
            get { return _pos.Count; }
        }

        public List<ImageQuad> Images
        {
            get { return _images; }
        }

        public void Clear()
        {
            _pos.Clear();
            _col.Clear();
            _tri.Clear();
            _mode.Clear();
            _images.Clear();
            BeginPath();
            _stack.Clear();
            _m = Matrix.Identity;
            GlobalAlpha = 1f;
            Lighter = false;
            LineDash = null;
            LineDashOffset = 0f;
            RoundCap = false;
            RoundJoin = false;
        }

        private int V(Vector2 p, Color c)
        {
            c.a *= GlobalAlpha;
            _pos.Add(p);
            _col.Add(c);
            _mode.Add(Lighter ? 0f : 1f);
            return _pos.Count - 1;
        }

        private void T(int a, int b, int c)
        {
            _tri.Add(a);
            _tri.Add(b);
            _tri.Add(c);
        }

        private readonly List<int> _idx = new List<int>(128);

        private void FillPolygon(List<Vector2> pts, Paint p)
        {
            int n = pts.Count;
            if (n > 2 && (pts[0] - pts[n - 1]).sqrMagnitude < 1e-6f) n--;
            if (n < 3) return;
            if (IsConvex(pts, n))
            {
                // 볼록: 무게중심에서 부채꼴. 그라데이션이면 동심 링으로 나눠 색을 곱게 잇는다.
                Vector2 c = Vector2.zero;
                for (int i = 0; i < n; i++) c += pts[i];
                c /= n;
                int rings = p.IsGradient ? 6 : 1;
                int center = V(c, p.At(c));
                int prevStart = -1;
                for (int r = 1; r <= rings; r++)
                {
                    float k = r / (float)rings;
                    int start = _pos.Count;
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 q = Vector2.LerpUnclamped(c, pts[i], k);
                        V(q, p.At(q));
                    }
                    for (int i = 0; i < n; i++)
                    {
                        int j = (i + 1) % n;
                        if (r == 1) T(center, start + i, start + j);
                        else
                        {
                            T(prevStart + i, start + i, start + j);
                            T(prevStart + i, start + j, prevStart + j);
                        }
                    }
                    prevStart = start;
                }
                return;
            }
            // 오목: 귀 자르기.
            _idx.Clear();
            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = pts[i];
                Vector2 b = pts[(i + 1) % n];
                area += (a.x * b.y) - (b.x * a.y);
            }
            int baseIdx = _pos.Count;
            for (int i = 0; i < n; i++) V(pts[i], p.At(pts[i]));
            for (int i = 0; i < n; i++) _idx.Add(area > 0f ? i : n - 1 - i);
            int guard = 0;
            while (_idx.Count > 3 && guard++ < 4000)
            {
                bool cut = false;
                for (int i = 0; i < _idx.Count; i++)
                {
                    int i0 = _idx[(i + _idx.Count - 1) % _idx.Count];
                    int i1 = _idx[i];
                    int i2 = _idx[(i + 1) % _idx.Count];
                    Vector2 a = pts[i0], b = pts[i1], c = pts[i2];
                    if (Cross(b - a, c - b) <= 0f) continue;
                    bool inside = false;
                    for (int k = 0; k < _idx.Count; k++)
                    {
                        int ik = _idx[k];
                        if (ik == i0 || ik == i1 || ik == i2) continue;
                        if (InTri(pts[ik], a, b, c))
                        {
                            inside = true;
                            break;
                        }
                    }
                    if (inside) continue;
                    T(baseIdx + i0, baseIdx + i1, baseIdx + i2);
                    _idx.RemoveAt(i);
                    cut = true;
                    break;
                }
                if (!cut) break;
            }
            if (_idx.Count == 3) T(baseIdx + _idx[0], baseIdx + _idx[1], baseIdx + _idx[2]);
            else
            {
                // 자기 교차 등으로 못 자르면 부채꼴로 덮는다.
                for (int i = 1; i + 1 < _idx.Count; i++) T(baseIdx + _idx[0], baseIdx + _idx[i], baseIdx + _idx[i + 1]);
            }
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return (a.x * b.y) - (a.y * b.x);
        }

        private static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a);
            float d2 = Cross(c - b, p - b);
            float d3 = Cross(a - c, p - c);
            return d1 >= 0f && d2 >= 0f && d3 >= 0f;
        }

        private static bool IsConvex(List<Vector2> pts, int n)
        {
            int sign = 0;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % n], c = pts[(i + 2) % n];
                float z = Cross(b - a, c - b);
                if (Mathf.Abs(z) < 1e-5f) continue;
                int s = z > 0f ? 1 : -1;
                if (sign == 0) sign = s;
                else if (s != sign) return false;
            }
            return true;
        }

        private void StrokePolyline(List<Vector2> pts, float w, Paint p, bool roundCap)
        {
            int n = pts.Count;
            float hw = w * 0.5f;
            if (hw <= 0.01f) return;
            int prevL = -1, prevR = -1;
            for (int i = 0; i < n; i++)
            {
                Vector2 dirIn = i > 0 ? (pts[i] - pts[i - 1]) : (pts[1] - pts[0]);
                Vector2 dirOut = i < n - 1 ? (pts[i + 1] - pts[i]) : dirIn;
                if (dirIn.sqrMagnitude < 1e-8f) dirIn = dirOut;
                if (dirOut.sqrMagnitude < 1e-8f) dirOut = dirIn;
                dirIn.Normalize();
                dirOut.Normalize();
                Vector2 nIn = new Vector2(-dirIn.y, dirIn.x);
                Vector2 nOut = new Vector2(-dirOut.y, dirOut.x);
                Vector2 miter = nIn + nOut;
                float ml = miter.magnitude;
                Vector2 off;
                if (ml < 1e-4f) off = nOut * hw;
                else
                {
                    miter /= ml;
                    float dot = Mathf.Max(0.25f, Vector2.Dot(miter, nOut));
                    off = miter * Mathf.Min(hw / dot, hw * 3f);
                }
                Vector2 pt = pts[i];
                int l = V(pt + off, p.At(pt + off));
                int r = V(pt - off, p.At(pt - off));
                if (prevL >= 0)
                {
                    T(prevL, prevR, r);
                    T(prevL, r, l);
                }
                prevL = l;
                prevR = r;
                if (RoundJoin && i > 0 && i < n - 1) Disc(pt, hw, p, 8);
            }
            if (roundCap)
            {
                Disc(pts[0], hw, p, 10);
                Disc(pts[n - 1], hw, p, 10);
            }
        }

        private void Disc(Vector2 c, float r, Paint p, int segs)
        {
            int center = V(c, p.At(c));
            int start = _pos.Count;
            for (int i = 0; i < segs; i++)
            {
                float a = i * Mathf.PI * 2f / segs;
                Vector2 q = c + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                V(q, p.At(q));
            }
            for (int i = 0; i < segs; i++) T(center, start + i, start + ((i + 1) % segs));
        }

        private readonly List<Vector2> _dashBuf = new List<Vector2>(64);

        private void StrokeDashed(List<Vector2> pts, float w, Paint p, float[] dash, float offset)
        {
            float total = 0f;
            foreach (float d in dash) total += d;
            if (total <= 0.01f)
            {
                StrokePolyline(pts, w, p, RoundCap);
                return;
            }
            // 캔버스 lineDashOffset: 패턴을 그만큼 앞으로 민다.
            float phase = offset % total;
            if (phase < 0f) phase += total;
            int di = 0;
            float left = dash[0];
            while (phase > 0f)
            {
                if (phase >= left)
                {
                    phase -= left;
                    di = (di + 1) % dash.Length;
                    left = dash[di];
                }
                else
                {
                    left -= phase;
                    phase = 0f;
                }
            }
            bool on = di % 2 == 0;
            _dashBuf.Clear();
            if (on) _dashBuf.Add(pts[0]);
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector2 a = pts[i];
                Vector2 b = pts[i + 1];
                float seg = Vector2.Distance(a, b);
                float pos = 0f;
                while (seg - pos > left)
                {
                    pos += left;
                    Vector2 q = Vector2.Lerp(a, b, pos / seg);
                    if (on)
                    {
                        _dashBuf.Add(q);
                        if (_dashBuf.Count >= 2) StrokePolyline(_dashBuf, w, p, RoundCap);
                        _dashBuf.Clear();
                    }
                    else
                    {
                        _dashBuf.Clear();
                        _dashBuf.Add(q);
                    }
                    on = !on;
                    di = (di + 1) % dash.Length;
                    left = dash[di];
                }
                left -= seg - pos;
                if (on) _dashBuf.Add(b);
            }
            if (on && _dashBuf.Count >= 2) StrokePolyline(_dashBuf, w, p, RoundCap);
            _dashBuf.Clear();
        }

        // ------------------------------------------------------------------ 메시로
        /// <summary>쌓인 모양을 메시로 옮긴다. map은 샘플 px → 월드 자리.</summary>
        public void Build(Mesh mesh, System.Func<Vector2, Vector3> map)
        {
            mesh.Clear();
            int n = _pos.Count;
            if (n == 0) return;
            var verts = new Vector3[n];
            var uv2 = new Vector2[n];
            var uv = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                verts[i] = map(_pos[i]);
                uv2[i] = new Vector2(_mode[i], 0f);
                uv[i] = new Vector2(0.5f, 0.5f);
            }
            mesh.indexFormat = n > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.colors = _col.ToArray();
            mesh.uv = uv;
            mesh.uv2 = uv2;
            mesh.SetTriangles(_tri, 0);
            mesh.RecalculateBounds();
            mesh.bounds = new Bounds(mesh.bounds.center, mesh.bounds.size + (Vector3.one * 1000f));
        }
    }
}
