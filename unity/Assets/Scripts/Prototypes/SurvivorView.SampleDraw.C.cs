using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 숲 샘플 아이템 C 그림(tools/levelup-art/items-c.js의 그리기를 줄 단위로 옮김): 비눗방울·맨홀 간헐천·물 사슬.
    /// 상태는 SampleItems.C.cs(규칙)가 들고, 여기서는 샘플 그리기 그대로 그리기만 한다.
    /// </summary>
    public sealed partial class SurvivorView
    {
        partial void DrawGroundC(SampleCanvas g, SampleItem it, int lv)
        {
            if (it is SampleManholeItem mh) DrawGroundManhole(g, mh, lv);
        }

        partial void DrawAirC(SampleCanvas g, SampleItem it, int lv)
        {
            switch (it)
            {
                case SampleBubbleItem b:
                    DrawAirBubble(g, b, lv);
                    break;
                case SampleManholeItem m:
                    DrawMhGeysers(g, m);
                    break;
                case SampleChainItem c:
                    DrawAirChain(g, c, lv);
                    break;
            }
        }

        // items-c.js:14-18 foamPuff(맨홀 물기둥 거품)
        private static void FoamPuff(SampleCanvas g, float x, float y, float r, int lv)
        {
            SampleCanvas.Paint gr = Radial(x - (r * 0.35f), y - (r * 0.4f), r * 0.05f, x, y, r);
            gr.AddColorStop(0f, Hex("#ffffff"));
            gr.AddColorStop(0.65f, lv >= 3 ? Hex("#eef8ff") : Hex("#f2f6f9"));
            gr.AddColorStop(1f, lv >= 5 ? Hex("#f3dca0") : lv >= 3 ? Hex("#a9d6f2") : Hex("#c4d6e2"));
            g.FillStyle = gr;
            g.BeginPath();
            g.Arc(x, y, r, 0f, Tau);
            g.Fill();
        }

        // ---------------------------------------------------------------- 4. 비눗방울 (items-c.js:112-209)
        // items-c.js:113-121 bubbleDraw (t는 샘플처럼 (t·120) mod 360 무지개)
        private static void BubbleDraw(SampleCanvas g, float x, float y, float r, float t, int lv)
        {
            SampleCanvas.Paint gr = Radial(x - (r * 0.3f), y - (r * 0.35f), r * 0.1f, x, y, r);
            gr.AddColorStop(0f, new Color(1f, 1f, 1f, 0.45f));
            gr.AddColorStop(0.7f, C(180, 220, 255, 0.12f));
            gr.AddColorStop(0.88f, lv >= 5 && lv < 6 ? C(255, 215, 120, 0.55f) : Hsla(Mathf.Repeat(t * 120f, 360f), 90f, 75f, 0.55f));
            gr.AddColorStop(1f, lv >= 5 && lv < 6 ? C(255, 240, 180, 0.9f) : Hsla(Mathf.Repeat((t * 120f) + 120f, 360f), 90f, 80f, 0.85f));
            g.FillStyle = gr;
            g.BeginPath();
            g.Arc(x, y, r, 0f, Tau);
            g.Fill();
            g.StrokeStyle = lv >= 5 ? C(255, 240, 200, 0.85f) : new Color(1f, 1f, 1f, 0.7f);
            g.LineWidth = lv >= 3 ? 1.6f : 1.2f;
            g.Stroke();
            g.FillStyle = new Color(1f, 1f, 1f, 0.9f);
            g.BeginPath();
            g.Ellipse(x - (r * 0.38f), y - (r * 0.45f), r * 0.22f, r * 0.12f, -0.6f, 0f, Tau);
            g.Fill();
            g.FillStyle = new Color(1f, 1f, 1f, 0.6f);
            g.BeginPath();
            g.Arc(x + (r * 0.4f), y + (r * 0.35f), r * 0.07f, 0f, Tau);
            g.Fill();
        }

        // items-c.js:183-208 drawAir
        private void DrawAirBubble(SampleCanvas g, SampleBubbleItem it, int lv)
        {
            float t = _sim.ST;
            int l5 = SampleC.L5(lv);
            Tier T = SurvivorSim.TierOf(lv);
            foreach (SampleBubbleItem.Shot b in it.Shots)
            {
                g.Save();
                g.Lighter = true;
                for (int i = 0; i < b.Trail.Count; i++)
                {
                    SampleBubbleItem.Pt p = b.Trail[i];
                    g.FillStyle = C(T.Glow, 0.35f * (1f - (i / 6f)));
                    g.BeginPath();
                    g.Arc(p.X, p.Y, 5f - (i * 0.6f), 0f, Tau);
                    g.Fill();
                }
                g.Restore();
                BubbleDraw(g, b.X, b.Y, 5f + (l5 * 0.6f), t, lv);
            }
            float sg = 1f + (it.Surge * 0.5f);
            foreach (SampleBubbleItem.Cap c in it.Caps)
            {
                Enemy m = c.M;
                float mx = SurvivorSim.SX(m.Pos);
                float y = c.Y - m.AirZ - 4f, r = ((SurvivorSim.SR(m) + 9f + (l5 * 1.2f)) * sg) + (Mathf.Sin(c.K * 10f) * 1.5f);
                TierGlow(g, mx, y, r, lv);
                BubbleDraw(g, mx, y, r, t + c.K, lv);
            }
            SampleBubbleItem.BigBubble B = it.Big;
            if (B != null)
            {
                float r = B.R + (Mathf.Sin(t * 8f) * 2f);
                ShadowAt(g, B.X, B.Y + 70f, r, 0.25f);
                TierGlow(g, B.X, B.Y, r, 6);
                BubbleDraw(g, B.X, B.Y, r, t, 6);
                for (int i = 0; i < B.N; i++)
                {
                    float a = (t * 2f) + (i * 2.4f), rr = r * (0.3f + ((i % 3) * 0.15f));
                    float x = B.X + (Mathf.Cos(a) * rr), y = B.Y + (Mathf.Sin(a) * rr);
                    SampleCanvas.Paint gr = Radial(x - 1.5f, y - 1.5f, 0.5f, x, y, 6f);
                    gr.AddColorStop(0f, Hex("#c58af5"));
                    gr.AddColorStop(1f, Hex("#4c1a85"));
                    g.FillStyle = gr;
                    g.StrokeStyle = Hex("#1b0830");
                    g.LineWidth = 1f;
                    g.BeginPath();
                    g.Arc(x, y, 5.5f, 0f, Tau);
                    g.Fill();
                    g.Stroke();
                }
            }
            SampleBubbleItem.Fall F = it.F;
            if (F != null)
            {
                // 샘플 물폭포 띠: 화면 y 60~140(거대 방울 y 64 기준 −4~+76)
                float a = 1f - (F.K / 0.7f), y0 = F.Y - 4f;
                g.Save();
                g.Lighter = true;
                SampleCanvas.Paint gr = Linear(F.X - F.W, 0f, F.X + F.W, 0f);
                gr.AddColorStop(0f, C(80, 170, 255, 0f));
                gr.AddColorStop(0.3f, C(120, 200, 255, 0.6f * a));
                gr.AddColorStop(0.5f, new Color(1f, 1f, 1f, 0.85f * a));
                gr.AddColorStop(0.7f, C(120, 200, 255, 0.6f * a));
                gr.AddColorStop(1f, C(80, 170, 255, 0f));
                g.FillStyle = gr;
                g.FillRect(F.X - F.W, y0, F.W * 2f, 80f);
                for (int j = 0; j < 3; j++)
                {
                    g.StrokeStyle = Hsla(SHue(j * 120f), 100f, 70f, 0.5f * a);
                    g.LineWidth = 2f;
                    g.BeginPath();
                    g.MoveTo(F.X - (F.W * 0.6f) + (j * F.W * 0.6f), y0);
                    g.LineTo(F.X - (F.W * 0.6f) + (j * F.W * 0.6f), y0 + 80f);
                    g.Stroke();
                }
                g.Restore();
            }
        }

        // ---------------------------------------------------------------- 5. 맨홀 간헐천 (items-c.js:211-317)
        private readonly List<SampleManholeItem.Hole> _mhView = new List<SampleManholeItem.Hole>();

        private static void ManholeLidGrad(SampleCanvas.Paint gr, int lv)
        {
            if (lv >= 5)
            {
                gr.AddColorStop(0f, Hex("#fff6c8"));
                gr.AddColorStop(0.45f, Hex("#ffd25a"));
                gr.AddColorStop(1f, Hex("#9a6a0c"));
            }
            else
            {
                gr.AddColorStop(0f, Hex("#a3a6b0"));
                gr.AddColorStop(1f, Hex("#5d5f68"));
            }
        }

        // items-c.js:304-315 drawGround(갈라진 땅) + items-c.js:227-245 drawManholes
        private void DrawGroundManhole(SampleCanvas g, SampleManholeItem it, int lv)
        {
            float t = _sim.ST;
            foreach (SampleManholeItem.Crack c in it.Cracks)
            {
                if (c.Pts.Count < 2) continue;
                int n = Mathf.Min(c.Pts.Count, Mathf.FloorToInt(c.K / 0.08f) + 1);
                float a = 1f - (c.K / 1.4f);
                g.Save();
                g.RoundJoin = true;
                g.RoundCap = true;
                g.StrokeStyle = C(30, 20, 10, 0.85f * a);
                g.LineWidth = 6f;
                g.BeginPath();
                g.MoveTo(c.Pts[0].X, c.Pts[0].Y);
                for (int i = 1; i < n; i++) g.LineTo(c.Pts[i].X + (i % 2 == 1 ? 4f : -4f), c.Pts[i].Y + (i % 2 == 1 ? -3f : 3f));
                g.Stroke();
                g.Lighter = true;
                g.StrokeStyle = Hsla(SHue(), 100f, 70f, 0.8f * a);
                g.LineWidth = 2.5f;
                g.Stroke();
                g.Restore();
            }

            // drawManholes: 지금 카메라가 비추는 맨홀(규칙과 같은 월드 격자).
            Vector2 cam = SCamCenter();
            float hw = (ViewW / 2f) + 30f, hh = (ViewH / 2f / TiltCos) + 30f;
            SampleManholeItem.HolesIn(cam.x - hw, cam.y - hh, cam.x + hw, cam.y + hh, _mhView);
            foreach (SampleManholeItem.Hole h in _mhView)
            {
                SampleManholeItem.Geyser G = null;
                foreach (SampleManholeItem.Geyser q in it.G)
                {
                    if (SurvivorSim.Hypot(q.X - h.X, q.Y - h.Y) < 2f)
                    {
                        G = q;
                        break;
                    }
                }
                float shake = G != null && G.K < 0f ? UnityEngine.Random.Range(-1.5f, 1.5f) : 0f;
                bool open = G != null && G.K >= 0f;
                g.FillStyle = Hex("#1a1b20");
                g.BeginPath();
                g.Ellipse(h.X, h.Y, 17f, 9f, 0f, 0f, Tau);
                g.Fill();
                if (open)
                {
                    SampleCanvas.Paint og = Radial(h.X, h.Y, 1f, h.X, h.Y, 14f);
                    og.AddColorStop(0f, Hex("#bfe8ff"));
                    og.AddColorStop(1f, Hex("#2d6aa8"));
                    g.FillStyle = og;
                    g.BeginPath();
                    g.Ellipse(h.X, h.Y, 14f, 7f, 0f, 0f, Tau);
                    g.Fill();
                    continue;
                }
                SampleCanvas.Paint gr = Linear(0f, h.Y - 9f, 0f, h.Y + 9f);
                ManholeLidGrad(gr, lv);
                g.FillStyle = gr;
                g.StrokeStyle = Hex("#2b2c33");
                g.LineWidth = 1.5f;
                g.BeginPath();
                g.Ellipse(h.X + shake, h.Y - 1f, 15f, 8f, 0f, 0f, Tau);
                g.Fill();
                g.Stroke();
                g.StrokeStyle = C(40, 40, 48, 0.6f);
                g.LineWidth = 1.2f;
                foreach (float o in new[] { -7f, -2.5f, 2.5f, 7f })
                {
                    g.BeginPath();
                    g.MoveTo(h.X + shake + o, h.Y - 6f);
                    g.LineTo(h.X + shake + o, h.Y + 4f);
                    g.Stroke();
                }
                if (lv >= 3)
                {
                    g.Save();
                    g.Lighter = true;
                    g.StrokeStyle = C(SurvivorSim.TierOf(lv).Glow, 0.25f + (0.2f * Mathf.Sin((t * 5f) + h.X)));
                    g.LineWidth = 2f;
                    g.BeginPath();
                    g.Ellipse(h.X, h.Y, 19f, 10f, 0f, 0f, Tau);
                    g.Stroke();
                    g.Restore();
                }
            }
            foreach (SampleManholeItem.Geyser G in it.G)
            {
                if (G.K >= 0f) continue;
                float k = 1f + (G.K / G.Warn);
                float r = SurvivorSim.Lerp(56f, 16f, SurvivorSim.Ease(k)) * G.P * G.Wd;
                g.StrokeStyle = G.Lv >= 5 ? C(255, 214, 110, 0.4f + (0.5f * k)) : C(120, 210, 255, 0.4f + (0.5f * k));
                g.LineWidth = 2.5f;
                g.SetLineDash(new[] { 6f, 5f });
                g.BeginPath();
                g.Ellipse(G.X, G.Y, r, r * 0.55f, 0f, 0f, Tau);
                g.Stroke();
                g.SetLineDash(null);
            }
        }

        // items-c.js:246-272 drawMhGeysers
        private void DrawMhGeysers(SampleCanvas g, SampleManholeItem it)
        {
            float t = _sim.ST;
            foreach (SampleManholeItem.Geyser G in it.G)
            {
                if (G.K < 0f) continue;
                float k = G.K;
                float h = 140f * G.P * (k < 0.12f ? SurvivorSim.Ease(k / 0.12f) : Mathf.Max(0f, 1f - ((k - 0.35f) / 0.45f)));
                float w = 12f * G.P * G.Wd * (1f + (it.Surge * 0.4f));
                if (h < 2f) continue;
                Tier T = SurvivorSim.TierOf(G.Lv);
                g.Save();
                g.Lighter = true;
                SampleCanvas.Paint gl = Radial(G.X, G.Y - (h * 0.5f), 0f, G.X, G.Y - (h * 0.5f), h * 0.75f);
                gl.AddColorStop(0f, C(T.Glow, 0.3f + (T.GlowA * 0.4f)));
                gl.AddColorStop(1f, C(T.Glow, 0f));
                g.FillStyle = gl;
                g.FillRect(G.X - h, G.Y - (h * 1.3f), h * 2f, h * 1.5f);
                if (G.Lv >= 5)
                {
                    g.StrokeStyle = G.Lv >= 6 ? Hsla(SHue(G.X), 100f, 70f, 0.7f) : C(255, 214, 110, 0.65f);
                    g.LineWidth = 3f;
                    g.BeginPath();
                    g.MoveTo(G.X - (w * 1.1f), G.Y);
                    g.QuadraticCurveTo(G.X - (w * 0.8f), G.Y - (h * 0.6f), G.X - (w * 0.55f), G.Y - h);
                    g.MoveTo(G.X + (w * 1.1f), G.Y);
                    g.QuadraticCurveTo(G.X + (w * 0.8f), G.Y - (h * 0.6f), G.X + (w * 0.55f), G.Y - h);
                    g.Stroke();
                }
                g.Restore();
                SampleCanvas.Paint lg = Linear(G.X - w, 0f, G.X + w, 0f);
                lg.AddColorStop(0f, C(60, 150, 245, 0.9f));
                lg.AddColorStop(0.35f, C(T.Core, 0.95f));
                lg.AddColorStop(0.5f, new Color(1f, 1f, 1f, 0.6f + (T.White * 0.4f)));
                lg.AddColorStop(1f, C(60, 140, 235, 0.9f));
                g.FillStyle = lg;
                g.BeginPath();
                g.MoveTo(G.X - w, G.Y);
                g.QuadraticCurveTo(G.X - (w * 0.7f), G.Y - (h * 0.6f), G.X - (w * 0.5f), G.Y - h);
                g.LineTo(G.X + (w * 0.5f), G.Y - h);
                g.QuadraticCurveTo(G.X + (w * 0.7f), G.Y - (h * 0.6f), G.X + w, G.Y);
                g.Fill();
                for (int i = 0; i < 6; i++)
                {
                    float a = (i / 6f * Tau) + (t * 6f);
                    FoamPuff(g, G.X + (Mathf.Cos(a) * w * 0.7f), G.Y - h + (Mathf.Sin(a) * 4f), w * 0.55f, 3);
                }
                // 날아가는 뚜껑
                float ch = 190f * G.P * Mathf.Sin(Mathf.PI * Mathf.Clamp(k / 0.75f, 0f, 1f));
                g.Save();
                g.Translate(G.X + (k * 60f), G.Y - h - 8f - (ch * 0.3f));
                g.Rotate(k * 14f);
                SampleCanvas.Paint cg = Linear(0f, -7f, 0f, 7f);
                ManholeLidGrad(cg, G.Lv);
                g.FillStyle = cg;
                g.StrokeStyle = Hex("#2b2c33");
                g.LineWidth = 1.5f;
                g.BeginPath();
                g.Ellipse(0f, 0f, 13f, (7f * Mathf.Abs(Mathf.Cos(k * 9f))) + 1f, 0f, 0f, Tau);
                g.Fill();
                g.Stroke();
                g.Restore();
            }
        }

        // ---------------------------------------------------------------- 6. 물 사슬 (items-c.js:319-366)
        // items-c.js:326-333 drawBolt
        private void DrawBolt(SampleCanvas g, List<SampleChainItem.Pt> sg, float a, int lv, float wk)
        {
            Tier T = SurvivorSim.TierOf(lv);
            Color outer = lv >= 6 ? Hsla(SHue(), 100f, 65f, 0.45f * a) : lv >= 5 ? C(255, 190, 70, 0.45f * a) : C(T.Glow, (0.25f + (T.GlowA * 0.3f)) * a);
            Color mid = lv >= 5 ? C(255, 230, 150, 0.8f * a) : C(T.Core, 0.75f * a);
            g.Save();
            g.Lighter = true;
            g.RoundCap = true;
            g.RoundJoin = true;
            float[] ws = { 14f * wk, 7f * wk, 2.5f * wk };
            Color[] cs = { outer, mid, new Color(1f, 1f, 1f, Mathf.Clamp01(a)) };
            for (int j = 0; j < 3; j++)
            {
                g.StrokeStyle = cs[j];
                g.LineWidth = ws[j];
                g.BeginPath();
                g.MoveTo(sg[0].X, sg[0].Y);
                foreach (SampleChainItem.Pt p in sg) g.LineTo(p.X, p.Y);
                g.Stroke();
            }
            g.Restore();
        }

        // items-c.js:361-365 drawAir
        private void DrawAirChain(SampleCanvas g, SampleChainItem it, int lv)
        {
            float px = _sim.PX, py = _sim.PY;
            float wk = SurvivorSim.TierOf(lv).Scale * 0.75f * (1f + (it.Surge * 0.6f));
            TierGlow(g, px + 6f, py - 10f, 7f, lv);
            foreach (SampleChainItem.Bolt b in it.Bolts)
            {
                float a = 1f - (b.K / 0.45f);
                foreach (List<SampleChainItem.Pt> sg in b.Segs) DrawBolt(g, sg, a, b.Lv, wk);
            }
        }
    }
}
