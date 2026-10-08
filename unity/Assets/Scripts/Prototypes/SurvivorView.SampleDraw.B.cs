using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 숲 개편(2026-10-08, 승인 샘플 그대로): tools/levelup-art/items-b.js 물풍선(A안)·소화기 부메랑·액체질소 지뢰의 그리기를 줄 단위로 옮겼다.
    /// 붓에 clip()이 없어 풍선 속 물(몸 모양으로 자른 출렁이는 수면)은 몸 윤곽 안쪽만 세로 띠로 채워 같은 모양을 만든다.
    /// 상태는 Logic/SampleItems.B.cs.
    /// </summary>
    public sealed partial class SurvivorView
    {
        partial void DrawGroundB(SampleCanvas g, SampleItem it, int lv)
        {
            if (it is BalloonItem b) BalloonDrawGround(g, b, lv);
            else if (it is MineItem m) MineDrawGround(g, m, lv);
        }

        partial void DrawAirB(SampleCanvas g, SampleItem it, int lv)
        {
            if (it is BalloonItem b) BalloonDrawAir(g, b, lv);
            else if (it is ExtinguisherItem e) ExtinguisherDrawAir(g, e, lv);
        }

        // ============================================================================ 물풍선 그림 A안
        // 몸 윤곽(r=1): 오른쪽 베지어(0,−1.05)→(1.05,−1.05),(1.1,.55)→(0,1.02), 왼쪽은 거울. 세로 띠마다 몸의 위·아래 끝.
        private const int BalCols = 28;
        private static float[] _balColX, _balColTop, _balColBot;

        private static void BuildBalloonCols()
        {
            if (_balColX != null) return;
            const int N = 64;
            var px = new float[N + 1];
            var py = new float[N + 1];
            float xmax = 0f;
            for (int i = 0; i <= N; i++)
            {
                float u = i / (float)N, v = 1f - u;
                px[i] = (3f * v * v * u * 1.05f) + (3f * v * u * u * 1.1f);
                py[i] = (v * v * v * -1.05f) + (3f * v * v * u * -1.05f) + (3f * v * u * u * 0.55f) + (u * u * u * 1.02f);
                xmax = Mathf.Max(xmax, px[i]);
            }
            _balColX = new float[BalCols + 1];
            _balColTop = new float[BalCols + 1];
            _balColBot = new float[BalCols + 1];
            for (int c = 0; c <= BalCols; c++)
            {
                float x = -xmax + (2f * xmax * c / BalCols), ax = Mathf.Min(Mathf.Abs(x), xmax - 1e-4f);
                float top = float.MaxValue, bot = float.MinValue;
                for (int i = 0; i < N; i++)
                {
                    float x0 = px[i], x1 = px[i + 1];
                    if ((ax - x0) * (ax - x1) > 0f || x0 == x1) continue;
                    float y = Mathf.Lerp(py[i], py[i + 1], (ax - x0) / (x1 - x0));
                    top = Mathf.Min(top, y);
                    bot = Mathf.Max(bot, y);
                }
                if (top > bot) top = bot = 0f;
                _balColX[c] = x;
                _balColTop[c] = top;
                _balColBot[c] = bot;
            }
        }

        /// <summary>샘플 수면 꺾은선(9점, items-b.js:25)의 x자리 높이.</summary>
        private static float BalSurface(float xx, float r, float lvl, float tilt, float slosh)
        {
            float u = Mathf.Clamp01((xx + (r * 1.3f)) / (r * 2.6f)) * 8f;
            int i = Mathf.Min(7, Mathf.FloorToInt(u));
            float f = u - i;
            float Y(int k)
            {
                float uk = k / 8f;
                return lvl + (tilt * r * (1f - (2f * uk))) + (Mathf.Sin((uk * 9f) + (slosh * 3f)) * r * 0.06f);
            }
            return Mathf.Lerp(Y(i), Y(i + 1), f);
        }

        // items-b.js:14 body()
        private static void BalloonBody(SampleCanvas g, float r)
        {
            g.BeginPath();
            g.MoveTo(0f, -r * 1.05f);
            g.BezierCurveTo(r * 1.05f, -r * 1.05f, r * 1.1f, r * 0.55f, 0f, r * 1.02f);
            g.BezierCurveTo(-r * 1.1f, r * 0.55f, -r * 1.05f, -r * 1.05f, 0f, -r * 1.05f);
            g.ClosePath();
        }

        // items-b.js:6-44
        private void BalloonA(SampleCanvas g, float x, float y, float r, float ang, float stretch, float slosh, int lv, float t)
        {
            BuildBalloonCols();
            Tier T = SurvivorSim.TierOf(lv > 0 ? lv : 1);
            g.Save();
            g.Translate(x, y);
            // 늘어남(진행 방향)·찌그러짐
            g.Rotate(ang);
            g.Scale(stretch, 1f / stretch);
            g.Rotate(-ang);
            // 등급 빛
            if (lv >= 3)
            {
                g.Save();
                g.Lighter = true;
                SampleCanvas.Paint gl = Radial(0f, 0f, r * 0.6f, 0f, 0f, r * 2f);
                gl.AddColorStop(0f, C(T.Glow, T.GlowA * 0.8f));
                gl.AddColorStop(1f, C(T.Glow, 0f));
                g.FillStyle = gl;
                g.BeginPath();
                g.Arc(0f, 0f, r * 2f, 0f, Tau);
                g.Fill();
                g.Restore();
            }
            // 고무(반투명)
            SampleCanvas.Paint rub = Radial(-r * 0.35f, -r * 0.45f, r * 0.1f, 0f, 0f, r * 1.15f);
            rub.AddColorStop(0f, C(225, 245, 255, 0.55f));
            rub.AddColorStop(0.55f, C(90, 175, 255, 0.35f));
            rub.AddColorStop(1f, C(30, 95, 210, 0.65f));
            BalloonBody(g, r);
            g.FillStyle = rub;
            g.Fill();
            // 안의 물: 몸 모양으로 자르고 수면을 기울여 출렁이게(clip 대신 몸 안쪽 세로 띠: 띠마다 위 = max(몸 위, 수면), 아래 = 몸 아래)
            float tilt = Mathf.Sin(slosh) * 0.5f, lvl = -r * 0.05f;
            SampleCanvas.Paint wg = Linear(0f, lvl - (r * 0.3f), 0f, r);
            wg.AddColorStop(0f, C(120, 210, 255, 0.95f));
            wg.AddColorStop(1f, C(20, 90, 200, 0.95f));
            g.FillStyle = wg;
            for (int c = 0; c < BalCols; c++)
            {
                float xa = _balColX[c] * r, xb = _balColX[c + 1] * r;
                float ba = _balColBot[c] * r, bb = _balColBot[c + 1] * r;
                float ua = Mathf.Min(Mathf.Max(_balColTop[c] * r, BalSurface(xa, r, lvl, tilt, slosh)), ba);
                float ub = Mathf.Min(Mathf.Max(_balColTop[c + 1] * r, BalSurface(xb, r, lvl, tilt, slosh)), bb);
                if (ba - ua < 0.01f && bb - ub < 0.01f) continue;
                g.BeginPath();
                g.MoveTo(xa, ua);
                g.LineTo(xb, ub);
                g.LineTo(xb, bb);
                g.LineTo(xa, ba);
                g.ClosePath();
                g.Fill();
            }
            // 수면 빛줄(몸 안쪽만)
            g.StrokeStyle = C(230, 250, 255, 0.85f);
            g.LineWidth = Mathf.Max(1f, r * 0.09f);
            g.BeginPath();
            bool on = false;
            for (int c = 0; c <= BalCols; c++)
            {
                float xx = _balColX[c] * r, yy = BalSurface(xx, r, lvl, tilt, slosh);
                bool inside = yy >= _balColTop[c] * r && yy <= _balColBot[c] * r;
                if (inside)
                {
                    if (on) g.LineTo(xx, yy);
                    else g.MoveTo(xx, yy);
                }
                on = inside;
            }
            g.Stroke();
            // 물 속 기포
            g.FillStyle = C(255, 255, 255, 0.55f);
            for (int i = 0; i < 3; i++)
            {
                g.BeginPath();
                g.Arc((-r * 0.3f) + (i * r * 0.32f), (r * 0.45f) - (Mathf.Repeat((t * 1.5f) + (i * 0.33f), 1f) * r * 0.4f), r * 0.07f, 0f, Tau);
                g.Fill();
            }
            // 테두리
            BalloonBody(g, r);
            g.LineWidth = Mathf.Max(1.2f, r * 0.13f);
            g.StrokeStyle = lv >= 6 ? Hsla(SHue(), 95f, 70f, 1f) : lv >= 5 ? Hex("#ffd25a") : C(15, 60, 140, 0.9f);
            g.Stroke();
            // 매듭 + 끈
            g.FillStyle = lv >= 5 ? Hex("#e0a020") : Hex("#2a6fd0");
            g.StrokeStyle = C(10, 40, 100, 0.9f);
            g.LineWidth = 1f;
            g.BeginPath();
            g.MoveTo(-r * 0.2f, r * 1.0f);
            g.LineTo(r * 0.2f, r * 1.0f);
            g.LineTo(r * 0.12f, r * 1.25f);
            g.LineTo(-r * 0.12f, r * 1.25f);
            g.ClosePath();
            g.Fill();
            g.Stroke();
            g.StrokeStyle = C(240, 240, 250, 0.85f);
            g.LineWidth = Mathf.Max(0.8f, r * 0.08f);
            g.BeginPath();
            g.MoveTo(0f, r * 1.25f);
            g.QuadraticCurveTo((r * 0.35f) + (Mathf.Sin(t * 9f) * r * 0.2f), r * 1.55f, -r * 0.05f, r * 1.85f);
            g.Stroke();
            // 하이라이트 두 점
            g.FillStyle = C(255, 255, 255, 0.95f);
            g.BeginPath();
            g.Ellipse(-r * 0.4f, -r * 0.5f, r * 0.24f, r * 0.14f, -0.7f, 0f, Tau);
            g.Fill();
            g.FillStyle = C(255, 255, 255, 0.8f);
            g.BeginPath();
            g.Arc(-r * 0.12f, -r * 0.72f, r * 0.08f, 0f, Tau);
            g.Fill();
            g.Restore();
        }

        // items-b.js:81-88
        private static void DrawShreds(SampleCanvas g, BalloonItem b)
        {
            foreach (BalloonItem.Shred p in b.Shreds)
            {
                float a = 1f - (p.K / 0.9f);
                g.Save();
                g.Translate(p.X, p.Y - p.Z);
                g.Rotate(p.Rot);
                g.GlobalAlpha = a;
                SampleCanvas.Paint gr = Linear(-p.Sz, 0f, p.Sz, 0f);
                gr.AddColorStop(0f, p.Gold ? Hex("#ffe9a0") : Hex("#9fd8ff"));
                gr.AddColorStop(1f, p.Gold ? Hex("#c58a12") : Hex("#1d63c4"));
                g.FillStyle = gr;
                g.BeginPath();
                g.MoveTo(-p.Sz, 0f);
                g.QuadraticCurveTo(0f, -p.Sz * 0.9f, p.Sz, 0f);
                g.QuadraticCurveTo(0f, -p.Sz * 0.3f, -p.Sz, 0f);
                g.Fill();
                g.Restore();
            }
        }

        // items-b.js:116-125
        private void DrawBal(SampleCanvas g, float x, float y, float vx, float vy, float r, float sq, float slosh, int lv, List<float> trailX, List<float> trailY, float surge)
        {
            float t = _sim.ST;
            float sp = Mathf.Sqrt((vx * vx) + (vy * vy)), ang = Mathf.Atan2(vy, vx);
            ShadowAt(g, x, y + r + 6f, r * 0.9f, 0.25f);
            // 물방울 꼬리
            g.Save();
            g.Lighter = true;
            int n = trailX != null ? trailX.Count : 0;
            for (int i = 1; i < n; i++)
            {
                float k = 1f - (i / (float)n);
                g.FillStyle = C(SurvivorSim.TierOf(lv).Glow, 0.45f * k);
                g.BeginPath();
                g.Arc(trailX[i], trailY[i] - 6f, r * 0.55f * k, 0f, Tau);
                g.Fill();
            }
            g.Restore();
            float stretch = sq > 0f ? 1f - (sq * 0.4f) : 1f + Mathf.Min(0.18f, sp / 1600f);
            BalloonA(g, x, y - 6f + (Mathf.Sin((t * 9f) + slosh) * 1.2f), r * (1f + (surge * 0.5f)), ang, stretch, slosh, lv, t);
        }

        // items-b.js:148-151
        private void BalloonDrawGround(SampleCanvas g, BalloonItem b, int lv)
        {
            float w = _sim.SWide;
            foreach (BalloonItem.RainDrop r in b.Rain)
            {
                float k = r.K / 0.45f;
                g.Save();
                g.FillStyle = C(20, 40, 90, 0.12f + (0.3f * k));
                g.BeginPath();
                g.Ellipse(r.X, r.Y, (6f + (12f * k)) * w, (6f + (12f * k)) * w * 0.45f, 0f, 0f, Tau);
                g.Fill();
                g.Lighter = true;
                g.StrokeStyle = Hsla(SHue(r.X), 100f, 70f, 0.5f * k);
                g.LineWidth = 1.5f;
                g.BeginPath();
                g.Ellipse(r.X, r.Y, (20f - (10f * k)) * w, (20f - (10f * k)) * w * 0.45f, 0f, 0f, Tau);
                g.Stroke();
                g.Restore();
            }
        }

        // items-b.js:152-156
        private void BalloonDrawAir(SampleCanvas g, BalloonItem b, int lv)
        {
            float t = _sim.ST;
            DrawShreds(g, b);
            foreach (BalloonItem.Bal o in b.B) DrawBal(g, o.X, o.Y, o.Vx, o.Vy, o.R, o.Sq, o.Slosh, o.Lv, o.TrailX, o.TrailY, b.Surge);
            foreach (BalloonItem.RainDrop r in b.Rain)
            {
                float z = 260f * (1f - SurvivorSim.Ease(r.K / 0.45f));
                DrawBal(g, r.X, r.Y - z, 0f, 400f, 14f * _sim.SWide, 0f, r.Sl + (t * 5f), 6, null, null, b.Surge);
            }
        }

        // ============================================================================ 4. 소화기 부메랑 → 분말 회오리
        // items-b.js:205-218
        private void DrawExtLv(SampleCanvas g, float x, float y, float a, int lv, float sc = 1f)
        {
            g.Save();
            g.Translate(x, y);
            g.Scale(sc, sc);
            TierGlow(g, 0f, 0f, 10f, lv);
            g.Rotate(a);
            SampleCanvas.Paint gr = Linear(-5f, 0f, 5f, 0f);
            if (lv >= 5)
            {
                gr.AddColorStop(0f, Hex("#fff2b8"));
                gr.AddColorStop(0.5f, Hex("#e2a12a"));
                gr.AddColorStop(1f, Hex("#8a5c0e"));
            }
            else
            {
                gr.AddColorStop(0f, Hex("#ff7a6a"));
                gr.AddColorStop(0.5f, Hex("#e2241b"));
                gr.AddColorStop(1f, Hex("#8c120d"));
            }
            g.FillStyle = gr;
            g.StrokeStyle = lv >= 5 ? Hex("#4a3005") : Hex("#3b0805");
            g.LineWidth = 1.5f;
            g.BeginPath();
            g.RoundRect(-5f, -10f, 10f, 20f, 4f);
            g.Fill();
            g.Stroke();
            g.FillStyle = C(255, 255, 255, 0.55f);
            g.FillRect(-3.5f, -8f, 1.6f, 15f);
            g.FillStyle = Hex("#2b2b33");
            g.FillRect(-3f, -14f, 6f, 4f);
            SampleCanvas.Paint lb = Linear(-6f, -2f, 6f, 2f);
            lb.AddColorStop(0f, Hex("#f4f4f8"));
            lb.AddColorStop(1f, Hex("#b8bcc8"));
            g.FillStyle = lb;
            g.FillRect(-6f, -2f, 12f, 4f);
            g.StrokeStyle = Hex("#222222");
            g.LineWidth = 2f;
            g.BeginPath();
            g.MoveTo(2f, -13f);
            g.QuadraticCurveTo(10f, -16f, 9f, -6f);
            g.Stroke();
            g.Restore();
        }

        // items-b.js:260-283
        private void ExtinguisherDrawAir(SampleCanvas g, ExtinguisherItem e, int lv)
        {
            float t = _sim.ST;
            foreach (ExtinguisherItem.Ext b in e.B)
            {
                // 잔상 꼬리
                g.Save();
                g.Lighter = true;
                int n = b.TrailX.Count;
                for (int i = 1; i < n; i++)
                {
                    float k = 1f - (i / (float)n);
                    g.StrokeStyle = lv >= 5 ? C(255, 220, 140, 0.4f * k) : C(255, 255, 255, 0.35f * k);
                    g.LineWidth = (4f + (lv * 1.5f)) * k;
                    g.RoundCap = true;
                    g.BeginPath();
                    g.MoveTo(b.TrailX[i - 1], b.TrailY[i - 1]);
                    g.LineTo(b.TrailX[i], b.TrailY[i]);
                    g.Stroke();
                }
                g.Restore();
                ShadowAt(g, b.X, b.Y + 14f, 7f, 0.25f);
                DrawExtLv(g, b.X, b.Y, b.Spin, lv, (1f + ((Mathf.Min(lv, 5) - 1) * 0.12f)) * (1f + (e.Surge * 0.6f)) * _sim.SWide);
            }
            if (lv < 6 || e.Tw == null) return;
            ExtinguisherItem.Twister T = e.Tw;
            float tw = _sim.SWide;
            ShadowAt(g, T.X, T.Y + 6f, 56f * tw, 0.32f);
            g.Save();
            g.RoundCap = true;
            for (int i = 0; i < 12; i++)
            {
                float h = i * 13f * tw, r = (14f + (i * 6.5f)) * tw, a = (t * 10f) + (i * 0.8f), wob = Mathf.Sin((t * 3f) + (i * 0.6f)) * 8f;
                float a0 = a % Tau, a3 = (a + 3f) % Tau;
                g.Lighter = true;
                g.StrokeStyle = Hsla(SHue(i * 30f), 100f, 75f, 0.35f);
                g.LineWidth = 10f - (i * 0.4f);
                g.BeginPath();
                g.Ellipse(T.X + wob, T.Y - h, r, r * 0.3f, 0f, a0, a0 + 4.4f);
                g.Stroke();
                g.Lighter = false;
                g.StrokeStyle = C(250, 252, 255, 0.9f - (i * 0.04f));
                g.LineWidth = 6f - (i * 0.3f);
                g.BeginPath();
                g.Ellipse(T.X + wob, T.Y - h, r, r * 0.3f, 0f, a0, a0 + 4.4f);
                g.Stroke();
                g.StrokeStyle = C(190, 215, 235, 0.6f);
                g.LineWidth = 2f;
                g.BeginPath();
                g.Ellipse(T.X + wob, T.Y - h, r * 0.8f, r * 0.24f, 0f, a3, a3 + 3f);
                g.Stroke();
            }
            g.Lighter = true;
            SampleCanvas.Paint gl = Radial(T.X, T.Y - (70f * tw), 0f, T.X, T.Y - (70f * tw), 110f * tw);
            gl.AddColorStop(0f, C(255, 250, 230, 0.35f));
            gl.AddColorStop(1f, C(255, 250, 230, 0f));
            g.FillStyle = gl;
            g.BeginPath();
            g.Arc(T.X, T.Y - (70f * tw), 110f * tw, 0f, Tau);
            g.Fill();
            g.Restore();
        }

        // ============================================================================ 5. 액체질소 지뢰 → 빙결 지대
        // items-b.js:288-300
        private void DrawMineLv(SampleCanvas g, float x, float y, bool on, int lv, float sc = 1f)
        {
            float t = _sim.ST;
            Tier T = SurvivorSim.TierOf(lv);
            float bl = on ? (Mathf.Sin(t * 10f) > 0f ? 1f : 0.35f) : 0.2f;
            g.Save();
            g.Translate(x, y);
            g.Scale(sc, sc);
            g.FillStyle = C(0, 0, 0, 0.3f);
            g.BeginPath();
            g.Ellipse(0f, 2f, 11f, 4.8f, 0f, 0f, Tau);
            g.Fill();
            if (on) TierGlow(g, 0f, 0f, 9f, lv);
            SampleCanvas.Paint gr = Radial(-3f, -3f, 1f, 0f, 0f, 10f);
            if (lv >= 5)
            {
                gr.AddColorStop(0f, Hex("#fffbe6"));
                gr.AddColorStop(0.5f, Hex("#9fe0ff"));
                gr.AddColorStop(1f, Hex("#2c6aa8"));
            }
            else
            {
                gr.AddColorStop(0f, Hex("#e6f6ff"));
                gr.AddColorStop(1f, Hex("#3d7fb8"));
            }
            g.FillStyle = gr;
            g.StrokeStyle = lv >= 5 ? Hex("#c58a12") : Hex("#123a5c");
            g.LineWidth = 1.6f;
            g.BeginPath();
            g.Ellipse(0f, 0f, 9.5f, 5.8f, 0f, 0f, Tau);
            g.Fill();
            g.Stroke();
            g.StrokeStyle = C(255, 255, 255, 0.6f);
            g.LineWidth = 1f;
            g.BeginPath();
            g.Ellipse(0f, -0.5f, 6f, 3.2f, 0f, Mathf.PI * 1.1f, Mathf.PI * 1.9f);
            g.Stroke();
            g.Lighter = true;
            g.FillStyle = C(T.Core, bl);
            g.BeginPath();
            g.Arc(0f, -1f, 2.6f, 0f, Tau);
            g.Fill();
            if (on)
            {
                g.FillStyle = C(T.Glow, 0.25f * bl);
                g.BeginPath();
                g.Arc(0f, 0f, 13f, 0f, Tau);
                g.Fill();
            }
            g.Restore();
        }

        // items-b.js:331-349
        private void MineDrawGround(SampleCanvas g, MineItem it, int lv)
        {
            float t = _sim.ST;
            List<MineItem.Mine> M = it.Mines;
            float w = _sim.SWide;
            if (lv >= 6)
            {
                for (int i = 0; i + 1 < M.Count; i++)
                {
                    MineItem.Mine A = M[i], B = M[i + 1];
                    if (A.Arm > 0f || B.Arm > 0f) continue;
                    g.Save();
                    g.RoundCap = true;
                    g.Lighter = true;
                    g.StrokeStyle = Hsla(SHue(i * 40f), 100f, 72f, 0.3f);
                    g.LineWidth = 22f * w;
                    g.BeginPath();
                    g.MoveTo(A.X, A.Y);
                    g.LineTo(B.X, B.Y);
                    g.Stroke();
                    g.StrokeStyle = C(120, 210, 255, 0.5f);
                    g.LineWidth = 12f * w;
                    g.Stroke();
                    g.StrokeStyle = C(235, 252, 255, 0.95f);
                    g.LineWidth = 3.5f;
                    g.Stroke();
                    g.Restore();
                    float L = Mathf.Sqrt(((B.X - A.X) * (B.X - A.X)) + ((B.Y - A.Y) * (B.Y - A.Y)));
                    int n = Mathf.FloorToInt(L / 10f);
                    for (int k = 1; k < n; k++)
                    {
                        float u = k / (float)n, x = Mathf.Lerp(A.X, B.X, u), y = Mathf.Lerp(A.Y, B.Y, u), h = 6f + ((k * 7) % 6) + (Mathf.Sin((t * 6f) + k) * 1.2f);
                        SampleCanvas.Paint gr = Linear(0f, y - h, 0f, y);
                        gr.AddColorStop(0f, C(255, 255, 255, 0.98f));
                        gr.AddColorStop(1f, C(120, 200, 255, 0.85f));
                        g.FillStyle = gr;
                        g.StrokeStyle = C(30, 90, 150, 0.6f);
                        g.LineWidth = 0.8f;
                        g.BeginPath();
                        g.MoveTo(x - 2.4f, y);
                        g.LineTo(x, y - h);
                        g.LineTo(x + 2.4f, y);
                        g.ClosePath();
                        g.Fill();
                        g.Stroke();
                    }
                }
            }
            // 냉기 범위 미리 보기(Lv3~)
            float R = MineItem.MineR[lv] * w;
            foreach (MineItem.Mine mi in M)
            {
                bool on = mi.Arm <= 0f;
                if (on && lv >= 3)
                {
                    g.Save();
                    g.StrokeStyle = C(SurvivorSim.TierOf(lv).Glow, 0.15f + (0.1f * Mathf.Sin(t * 6f)));
                    g.LineWidth = 1.2f;
                    g.SetLineDash(new[] { 3f, 5f });
                    g.BeginPath();
                    g.Ellipse(mi.X, mi.Y, R, R * 0.6f, 0f, 0f, Tau);
                    g.Stroke();
                    g.Restore();
                }
                DrawMineLv(g, mi.X, mi.Y, on, lv, (1f + ((Mathf.Min(lv, 5) - 1) * 0.1f)) * (1f + (it.Surge * 0.6f)));
            }
        }
    }
}
