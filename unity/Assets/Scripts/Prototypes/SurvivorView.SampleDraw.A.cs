using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 숲 개편(2026-10-08, 승인 샘플 그대로): tools/levelup-art/items-a.js 회전 스프링클러의 그리기를 줄 단위로 옮겼다. 물대포는 고압 방수포 대폭발 고리만(펌프에서 옮김).
    /// 바닥 층 = 샘플 drawGround, 공중 층 = 샘플 ents(y 순) + drawAir. 상태는 Logic/SampleItems.A.cs.
    /// </summary>
    public sealed partial class SurvivorView
    {
        partial void DrawGroundA(SampleCanvas g, SampleItem it, int lv)
        {
            if (it is HoseItem h) HoseNovaGround(g, h, lv);
            else if (it is SprinklerItem sp) SprinklerDrawGround(g, sp, lv);
        }

        partial void DrawAirA(SampleCanvas g, SampleItem it, int lv)
        {
            if (it is SprinklerItem sp)
            {
                SprinklerEnts(g, sp, lv);
                SprinklerDrawAir(g, sp, lv);
            }
        }

        // ---------------------------------------------------------------- 1. 물대포 → 고압 방수포(대폭발만, 펌프 items-d.js:135-144에서 옮김)
        private void HoseNovaGround(SampleCanvas g, HoseItem h, int lv)
        {
            if (lv != 6) return;
            float px = _sim.PX, py = _sim.PY, t = _sim.ST, w = _sim.SWide;
            float k = 1f - (Mathf.Max(0f, h.Boom) / HoseItem.BoomEvery);
            // 차오르는 압력 고리: 터지기 직전 빨라지고 밝아진다.
            g.Save();
            g.Lighter = true;
            float r = HoseItem.BoomR * w * (1f - (k * 0.75f));
            g.StrokeStyle = Hsla(SHue(), 100f, 70f, 0.15f + (k * 0.6f));
            g.LineWidth = 2f + (k * 4f);
            g.SetLineDash(new[] { 8f, 6f });
            g.LineDashOffset = -t * 80f;
            g.BeginPath();
            g.Ellipse(px, py, r, r * 0.62f, 0f, 0f, Tau);
            g.Stroke();
            g.SetLineDash(null);
            foreach (SamplePtD b in h.Booms)
            {
                float e = SurvivorSim.Ease(b.K / 0.8f), a = 1f - (b.K / 0.8f), br = 150f * w * e;
                for (int j = 0; j < 3; j++)
                {
                    g.StrokeStyle = Hsla(SHue(j * 120f), 100f, 70f, a);
                    g.LineWidth = 10f - (j * 3f);
                    g.BeginPath();
                    g.Ellipse(b.X, b.Y + 4f, br * (1f - (j * 0.12f)), br * 0.62f * (1f - (j * 0.12f)), 0f, 0f, Tau);
                    g.Stroke();
                }
                float gr = Mathf.Max(0.01f, br);
                SampleCanvas.Paint gl = Radial(b.X, b.Y, 0f, b.X, b.Y, gr);
                gl.AddColorStop(0f, C(160, 220, 255, 0.35f * a));
                gl.AddColorStop(1f, C(160, 220, 255, 0f));
                g.FillStyle = gl;
                g.BeginPath();
                g.Arc(b.X, b.Y, gr, 0f, Tau);
                g.Fill();
            }
            g.Restore();
        }

        // ---------------------------------------------------------------- 2. 회전 스프링클러 → 물 왕관
        // items-a.js:72-84
        private void DrawSprinklerHead(SampleCanvas g, float x, float y, float spin, int lv, float sc = 1f)
        {
            Tier T = SurvivorSim.TierOf(lv);
            ShadowAt(g, x, y + 8f, 9f * sc, 0.3f);
            g.Save();
            g.Translate(x, y - 6f);
            g.Scale(sc, sc);
            TierGlow(g, 0f, 0f, 9f, Mathf.Max(2, lv));
            SampleCanvas.Paint gr = Radial(-2f, -3f, 1f, 0f, 0f, 8f);
            if (lv >= 5)
            {
                gr.AddColorStop(0f, Hex("#fffbe0"));
                gr.AddColorStop(1f, Hex("#d99a12"));
            }
            else
            {
                gr.AddColorStop(0f, Hex("#f2f6ff"));
                gr.AddColorStop(1f, lv >= 3 ? Hex("#7aa8d8") : Hex("#8d97a8"));
            }
            g.FillStyle = gr;
            g.StrokeStyle = lv >= 5 ? Hex("#5a3a00") : Hex("#26303f");
            g.LineWidth = 1.5f;
            g.BeginPath();
            g.Arc(0f, 0f, 7f, 0f, Tau);
            g.Fill();
            g.Stroke();
            g.Rotate(spin);
            g.RoundCap = true;
            int arms = lv >= 4 ? 4 : 3;
            for (int i = 0; i < arms; i++)
            {
                float a = i * Tau / arms;
                g.StrokeStyle = lv >= 5 ? Hex("#ffe9a0") : Hex("#d9dde6");
                g.LineWidth = 3f;
                g.BeginPath();
                g.MoveTo(0f, 0f);
                g.LineTo(Mathf.Cos(a) * 11f, Mathf.Sin(a) * 11f);
                g.Stroke();
                g.FillStyle = C(T.Core, 1f);
                g.BeginPath();
                g.Arc(Mathf.Cos(a) * 11f, Mathf.Sin(a) * 11f, 2.6f, 0f, Tau);
                g.Fill();
            }
            g.Restore();
        }

        // items-a.js:113-119
        private void SprinklerDrawGround(SampleCanvas g, SprinklerItem sp, int lv)
        {
            float t = _sim.ST, px = _sim.PX, py = _sim.PY;
            float R = SprinklerItem.RTab[lv] * _sim.SWide;
            Tier T = SurvivorSim.TierOf(lv);
            g.Save();
            g.StrokeStyle = C(T.Glow, 0.18f + (lv * 0.04f));
            g.LineWidth = 2f;
            g.SetLineDash(new[] { 4f, 6f });
            g.LineDashOffset = -t * 30f;
            g.BeginPath();
            g.Ellipse(px, py, R, R * 0.72f, 0f, 0f, Tau);
            g.Stroke();
            g.SetLineDash(null);
            if (lv >= 5)
            {
                g.Lighter = true;
                for (int j = 0; j < (lv == 6 ? 3 : 1); j++)
                {
                    g.StrokeStyle = lv == 6 ? Hsla(SHue(j * 120f), 100f, 68f, 0.6f - (j * 0.12f)) : C(255, 214, 110, 0.45f);
                    g.LineWidth = (lv == 6 ? 9f : 4f) - (j * 2f);
                    float st = t * (4f + (j * 2f));
                    g.BeginPath();
                    g.Ellipse(px, py - 4f, R, R * 0.72f, 0f, st, st + (Tau * 0.85f));
                    g.Stroke();
                }
            }
            g.Restore();
        }

        private readonly List<SprinklerItem.Head> _sprEnts = new List<SprinklerItem.Head>();

        // items-a.js:120 (ents: 머리끼리 y 순)
        private void SprinklerEnts(SampleCanvas g, SprinklerItem sp, int lv)
        {
            _sprEnts.Clear();
            _sprEnts.AddRange(sp.Heads);
            _sprEnts.Sort((p, q) => p.Y.CompareTo(q.Y));
            float t = _sim.ST;
            foreach (SprinklerItem.Head h in _sprEnts) DrawSprinklerHead(g, h.X, h.Y, t * (12f + (lv * 2f)), lv, (1f + ((lv - 1) * 0.1f)) * (1f + (sp.Surge * 0.6f)));
        }

        // items-a.js:121-137
        private void SprinklerDrawAir(SampleCanvas g, SprinklerItem sp, int lv)
        {
            float t = _sim.ST, px = _sim.PX, py = _sim.PY;
            foreach (SprinklerItem.Head h in sp.Heads)
            {
                if (h.HasJet) TierStream(g, h.X, h.Y - 6f, h.JetX1, h.JetY1 - 6f, 2f + (lv * 0.6f), lv, 4f);
            }
            if (lv != 6) return;
            float w = _sim.SWide, R = SprinklerItem.RTab[6] * w;
            g.Save();
            g.RoundCap = true;
            for (int j = 0; j < 8; j++)
            {
                float a = sp.Jet + (j * Tau / 8f), x0 = px + (Mathf.Cos(a) * R), y0 = py - 6f + (Mathf.Sin(a) * R * 0.72f), x1 = px + (Mathf.Cos(a) * (R + (150f * w))), y1 = py - 6f + (Mathf.Sin(a) * (R + (150f * w)) * 0.72f);
                g.Lighter = true;
                g.StrokeStyle = Hsla(SHue(j * 45f), 100f, 65f, 0.45f);
                g.LineWidth = 16f;
                g.BeginPath();
                g.MoveTo(x0, y0);
                g.LineTo(x1, y1);
                g.Stroke();
                SampleCanvas.Paint lg = Linear(x0, y0, x1, y1);
                lg.AddColorStop(0f, C(255, 255, 255, 1f));
                lg.AddColorStop(0.6f, C(160, 220, 255, 0.7f));
                lg.AddColorStop(1f, C(120, 200, 255, 0f));
                g.StrokeStyle = lg;
                g.LineWidth = 6f;
                g.BeginPath();
                g.MoveTo(x0, y0);
                g.LineTo(x1, y1);
                g.Stroke();
            }
            // 왕관: 머리 위 금빛 왕관이 돈다
            g.Lighter = false;
            g.Translate(px, py - 40f);
            float k = 1f + (Mathf.Sin(t * 6f) * 0.05f);
            g.Scale(k, k);
            SampleCanvas.Paint cg = Linear(0f, -10f, 0f, 8f);
            cg.AddColorStop(0f, Hex("#fffbe0"));
            cg.AddColorStop(0.5f, Hex("#ffd25a"));
            cg.AddColorStop(1f, Hex("#c58a12"));
            g.FillStyle = cg;
            g.StrokeStyle = Hex("#6a4200");
            g.LineWidth = 1.4f;
            g.BeginPath();
            g.MoveTo(-12f, 6f);
            g.LineTo(-14f, -6f);
            g.LineTo(-7f, 0f);
            g.LineTo(0f, -10f);
            g.LineTo(7f, 0f);
            g.LineTo(14f, -6f);
            g.LineTo(12f, 6f);
            g.ClosePath();
            g.Fill();
            g.Stroke();
            DrawCrownGem(g, -7f, "#5cc2ff");
            DrawCrownGem(g, 0f, "#ff6fd8");
            DrawCrownGem(g, 7f, "#7dffb0");
            g.Restore();
        }

        // items-a.js:135
        private static void DrawCrownGem(SampleCanvas g, float x, string c)
        {
            g.FillStyle = Hex(c);
            g.BeginPath();
            g.Arc(x, 2f, 2f, 0f, Tau);
            g.Fill();
        }
    }
}
