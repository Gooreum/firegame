using FireGame.Prototypes.Logic;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 승인 샘플 tools/levelup-art/items-d.js 그리기를 그대로: 호스 채찍 · 고압 펌프 · 장화 · 방화복.
    /// 바닥 = 샘플 drawGround(3D 몸 아래), 공중 = 샘플 ents + drawAir(모든 몸 위). 상태는 SampleItems.D.cs.
    /// 샘플 ents는 y로 몸과 겹쳐 그리는데, 여기 3D 몸은 바닥·공중 층 사이에 있으니 몸 "뒤"면 바닥, "앞"이면 공중에 둔다.
    /// </summary>
    public sealed partial class SurvivorView
    {
        partial void DrawGroundD(SampleCanvas g, SampleItem it, int lv)
        {
            if (it is SampleWhip w) WhipGroundD(g, w, lv);
            else if (it is SampleTank t)
            {
                TankGroundD(g, t, lv);
                if (!TankInFrontD()) TankEntD(g, t, lv);
            }
            else if (it is SampleBoots b) BootsGroundD(g, b, lv);
            else if (it is SampleSuit s) SuitShieldGlowD(g, s);
        }

        partial void DrawAirD(SampleCanvas g, SampleItem it, int lv)
        {
            if (it is SampleWhip w) WhipAirD(g, w, lv);
            else if (it is SampleTank t)
            {
                if (TankInFrontD()) TankEntD(g, t, lv);
                TankAirD(g, t, lv);
            }
            else if (it is SampleBoots b)
            {
                BootsEntsD(g, b, lv);
                BootsAirD(g, lv);
            }
            else if (it is SampleSuit s)
            {
                SuitEntD(g, s, lv);
                SuitAirD(g, s, lv);
            }
        }

        /// <summary>바라보는 쪽(샘플 px, y 아래가 +).</summary>
        private Vector2 FacingD()
        {
            return new Vector2(_sim.Facing.X, -_sim.Facing.Y);
        }

        // ---------------------------------------------------------------- base.js:205-223 drawPlayer(몸만: 장화 잔상)
        private void DrawPlayerD(SampleCanvas g, float x, float y)
        {
            float t = _sim.ST;
            float run = _sim.PlayerMoving ? Mathf.Sin(t * 16f) : 0f;
            ShadowAt(g, x, y + 8f, 12f, 0.35f);
            g.Save();
            g.Translate(x, y);
            g.FillStyle = Hex("#1b2742");
            g.BeginPath();
            g.Ellipse(-4f, 7f + (run * 2f), 3.5f, 4f, 0f, 0f, Tau);
            g.Fill();
            g.BeginPath();
            g.Ellipse(4f, 7f - (run * 2f), 3.5f, 4f, 0f, 0f, Tau);
            g.Fill();
            SampleCanvas.Paint bg = Linear(0f, -6f, 0f, 8f);
            bg.AddColorStop(0f, Hex("#3f7ff0"));
            bg.AddColorStop(1f, Hex("#2353b8"));
            g.FillStyle = bg;
            g.StrokeStyle = Hex("#0f1d3d");
            g.LineWidth = 2f;
            g.BeginPath();
            g.RoundRect(-9f, -6f, 18f, 15f, 6f);
            g.Fill();
            g.Stroke();
            g.FillStyle = Hex("#ffd84a");
            g.FillRect(-9f, 2f, 18f, 3f);
            SampleCanvas.Paint hg = Radial(-3f, -16f, 1f, 0f, -12f, 11f);
            hg.AddColorStop(0f, Hex("#ff7b6b"));
            hg.AddColorStop(1f, Hex("#c4231b"));
            g.FillStyle = hg;
            g.BeginPath();
            g.Arc(0f, -12f, 9.5f, 0f, Tau);
            g.Fill();
            g.Stroke();
            g.FillStyle = Hex("#c4231b");
            g.BeginPath();
            g.Ellipse(0f, -7f, 12f, 3.4f, 0f, 0f, Tau);
            g.Fill();
            g.Stroke();
            g.FillStyle = Hex("#ffd84a");
            g.BeginPath();
            g.Arc(0f, -15f, 2.6f, 0f, Tau);
            g.Fill();
            g.Restore();
        }

        // ================================================================ 10. 호스 채찍
        // items-d.js:46-55 drawGround
        private void WhipGroundD(SampleCanvas g, SampleWhip w, int lv)
        {
            if (lv != 6) return;
            float t = _sim.ST;
            g.Save();
            g.Lighter = true;
            foreach (SamplePtD r in w.Rings)
            {
                float a = Mathf.Min(1f, r.K * 5f) * Mathf.Min(1f, (2.6f - r.K) * 2f);
                for (int j = 0; j < 3; j++)
                {
                    g.StrokeStyle = Hsla(SHue((j * 120f) + r.X), 100f, 70f - (j * 6f), 0.7f * a);
                    g.LineWidth = 5f - (j * 1.3f);
                    float st = (t * (9f + (j * 3f))) + (j * 2f);
                    g.BeginPath();
                    g.Ellipse(r.X, r.Y, 28f - (j * 6f), (28f - (j * 6f)) * 0.55f, 0f, st, st + 4.2f);
                    g.Stroke();
                }
                SampleCanvas.Paint gl = Radial(r.X, r.Y, 0f, r.X, r.Y, 26f);
                gl.AddColorStop(0f, C(160, 225, 255, 0.35f * a));
                gl.AddColorStop(1f, C(160, 225, 255, 0f));
                g.FillStyle = gl;
                g.BeginPath();
                g.Arc(r.X, r.Y, 26f, 0f, Tau);
                g.Fill();
            }
            g.Restore();
        }

        // items-d.js:56-81 drawAir (79줄 물방울은 규칙 쪽 Update가 뿌린다)
        private void WhipAirD(SampleCanvas g, SampleWhip w, int lv)
        {
            float px = _sim.PX, py = _sim.PY;
            int arms = SampleWhip.Arms[lv];
            float R = w.RadiusAt(lv);
            Tier T = SurvivorSim.TierOf(lv);
            for (int i = 0; i < arms; i++)
            {
                if (i >= w.Trail.Count) continue;
                var tr = w.Trail[i];
                if (tr.Count == 0) continue;
                g.Save();
                g.Lighter = true;
                g.RoundCap = true;
                for (int j = 1; j < tr.Count; j++)
                {
                    float k = 1f - (j / (float)tr.Count);
                    g.StrokeStyle = lv == 6 ? Hsla(SHue(j * 18f), 100f, 68f, 0.55f * k) : C(T.Glow, (0.35f + (T.GlowA * 0.4f)) * k);
                    g.LineWidth = (8f + (lv * 2.5f)) * k;
                    g.BeginPath();
                    g.MoveTo(tr[j - 1].X, tr[j - 1].Y);
                    g.LineTo(tr[j].X, tr[j].Y);
                    g.Stroke();
                    if (T.White > 0f)
                    {
                        g.StrokeStyle = new Color(1f, 1f, 1f, T.White * 0.7f * k);
                        g.LineWidth = (2f + (lv * 0.6f)) * k;
                        g.Stroke();
                    }
                    if (T.Gold && j % 2 == 1)
                    {
                        g.StrokeStyle = C(255, 214, 110, 0.5f * k);
                        g.LineWidth = 1.2f;
                        g.Stroke();
                    }
                }
                g.Restore();
                SamplePtD t0 = tr[0];
                float a = w.A + (i * Tau / arms);
                float mx = px + (Mathf.Cos(a - 0.55f) * R * 0.55f), my = py - 4f + (Mathf.Sin(a - 0.55f) * R * 0.45f);
                g.Save();
                g.RoundCap = true;
                g.StrokeStyle = Hex("#5a120d");
                g.LineWidth = 5f + (lv * 0.4f);
                g.BeginPath();
                g.MoveTo(px, py - 4f);
                g.QuadraticCurveTo(mx, my, t0.X, t0.Y);
                g.Stroke();
                SampleCanvas.Paint hg = Linear(px, py, t0.X, t0.Y);
                hg.AddColorStop(0f, Hex("#c4231b"));
                hg.AddColorStop(1f, Hex(lv >= 5 ? "#ff9a5a" : "#ff5a4a"));
                g.StrokeStyle = hg;
                g.LineWidth = 3f + (lv * 0.3f);
                g.Stroke();
                if (lv >= 5)
                {
                    g.StrokeStyle = C(255, 220, 130, 0.8f);
                    g.LineWidth = 1f;
                    g.Stroke();
                }
                g.Restore();
                TierGlow(g, t0.X, t0.Y, 5f + lv, Mathf.Max(2, lv));
                SampleCanvas.Paint ng = Radial(t0.X - 1f, t0.Y - 1f, 0f, t0.X, t0.Y, 4.5f);
                ng.AddColorStop(0f, Hex("#fffbe0"));
                ng.AddColorStop(1f, Hex(lv >= 5 ? "#d99a12" : "#9aa0ae"));
                g.FillStyle = ng;
                g.BeginPath();
                g.Arc(t0.X, t0.Y, 3.8f + (lv * 0.25f), 0f, Tau);
                g.Fill();
            }
        }

        // ================================================================ 11. 고압 펌프
        // items-d.js:85-100 drawPumpTank
        private void DrawPumpTankD(SampleCanvas g, float x, float y, int lv, float sc, float surge)
        {
            float t = _sim.ST;
            g.Save();
            g.Translate(x, y);
            g.Scale(sc, sc);
            Tier T = SurvivorSim.TierOf(lv);
            if (lv >= 2)
            {
                g.Lighter = true;
                float R = 10f + (lv * 3f) + (surge * 10f);
                SampleCanvas.Paint gl = Radial(0f, 0f, 0f, 0f, 0f, R);
                gl.AddColorStop(0f, lv >= 6 ? Hsla(SHue(), 100f, 70f, 0.6f) : C(T.Gold ? new Rgb(255, 210, 110) : T.Glow, 0.2f + (lv * 0.07f)));
                gl.AddColorStop(1f, new Color(0f, 0f, 0f, 0f));
                g.FillStyle = gl;
                g.BeginPath();
                g.Arc(0f, 0f, R, 0f, Tau);
                g.Fill();
                g.Lighter = false;
            }
            SampleCanvas.Paint bg = Linear(-6f, 0f, 6f, 0f);
            if (lv >= 5)
            {
                bg.AddColorStop(0f, Hex("#8a5c0e"));
                bg.AddColorStop(0.4f, Hex("#fff2b8"));
                bg.AddColorStop(1f, Hex("#b07a12"));
            }
            else
            {
                bg.AddColorStop(0f, Hex("#8d1510"));
                bg.AddColorStop(0.4f, Hex("#ff7466"));
                bg.AddColorStop(1f, Hex("#a31a12"));
            }
            g.FillStyle = bg;
            g.StrokeStyle = Hex("#2a0805");
            g.LineWidth = 1.2f;
            g.BeginPath();
            g.RoundRect(-6f, -9f, 12f, 18f, 5f);
            g.Fill();
            g.Stroke();
            // 게이지
            SampleCanvas.Paint dg = Radial(-1f, -1f, 0f, 0f, 0f, 5f);
            dg.AddColorStop(0f, Hex("#ffffff"));
            dg.AddColorStop(1f, Hex("#c9d3e0"));
            g.FillStyle = dg;
            g.StrokeStyle = Hex("#333333");
            g.BeginPath();
            g.Arc(0f, -11f, 5f, 0f, Tau);
            g.Fill();
            g.Stroke();
            g.StrokeStyle = Hex(lv >= 5 ? "#ff3a2a" : "#e8a020");
            g.LineWidth = 1.4f;
            g.BeginPath();
            g.Arc(0f, -11f, 3.6f, Mathf.PI * 0.75f, (Mathf.PI * 0.75f) + (Mathf.PI * 1.5f * (lv / 6f)));
            g.Stroke();
            float na = (Mathf.PI * 0.75f) + (Mathf.PI * 1.5f * Mathf.Min(1f, (lv / 6f) + (Mathf.Sin(t * 30f) * 0.02f * lv)));
            g.StrokeStyle = Hex("#111111");
            g.LineWidth = 1f;
            g.BeginPath();
            g.MoveTo(0f, -11f);
            g.LineTo(Mathf.Cos(na) * 4f, -11f + (Mathf.Sin(na) * 4f));
            g.Stroke();
            g.Restore();
        }

        /// <summary>등 탱크 자리: 샘플(오른쪽을 보는 소방관)의 (−9, −4)를 바라보는 쪽 반대로 돌린다(세로는 .7배로 눕힌다).</summary>
        private Vector2 TankAtD()
        {
            Vector2 f = FacingD();
            return new Vector2(_sim.PX - (f.x * 9f), _sim.PY - 4f - (f.y * 9f * 0.7f));
        }

        /// <summary>샘플은 탱크를 몸 뒤(y − .5)에 그린다. 위를 보면 등이 화면 아래(카메라 쪽)라 몸 앞에 그린다.</summary>
        private bool TankInFrontD()
        {
            return FacingD().y < -0.3f;
        }

        // items-d.js:145 ents
        private void TankEntD(SampleCanvas g, SampleTank t, int lv)
        {
            Vector2 at = TankAtD();
            DrawPumpTankD(g, at.x, at.y, lv, 0.8f + (lv * 0.06f), t.Surge);
        }

        // items-d.js:135-144 drawGround
        private void TankGroundD(SampleCanvas g, SampleTank tk, int lv)
        {
            if (lv != 6) return;
            float px = _sim.PX, py = _sim.PY, t = _sim.ST;
            float k = 1f - (Mathf.Max(0f, tk.Boom) / SampleTank.BoomEvery);
            // 차오르는 압력 고리: 터지기 직전 빨라지고 밝아진다.
            g.Save();
            g.Lighter = true;
            float r = 140f * (1f - (k * 0.75f));
            g.StrokeStyle = Hsla(SHue(), 100f, 70f, 0.15f + (k * 0.6f));
            g.LineWidth = 2f + (k * 4f);
            g.SetLineDash(new[] { 8f, 6f });
            g.LineDashOffset = -t * 80f;
            g.BeginPath();
            g.Ellipse(px, py, r, r * 0.62f, 0f, 0f, Tau);
            g.Stroke();
            g.SetLineDash(null);
            foreach (SamplePtD b in tk.Booms)
            {
                float e = SurvivorSim.Ease(b.K / 0.8f), a = 1f - (b.K / 0.8f);
                for (int j = 0; j < 3; j++)
                {
                    g.StrokeStyle = Hsla(SHue(j * 120f), 100f, 70f, a);
                    g.LineWidth = 10f - (j * 3f);
                    g.BeginPath();
                    g.Ellipse(b.X, b.Y + 4f, 150f * e * (1f - (j * 0.12f)), 150f * e * 0.62f * (1f - (j * 0.12f)), 0f, 0f, Tau);
                    g.Stroke();
                }
                float gr = Mathf.Max(0.01f, 150f * e);
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

        // items-d.js:146-153 drawAir (물줄기 시작점 샘플 (+11, −5)도 바라보는 쪽으로 돌린다)
        private void TankAirD(SampleCanvas g, SampleTank tk, int lv)
        {
            float px = _sim.PX, py = _sim.PY;
            Vector2 f = FacingD();
            float sx = px + (f.x * 11f), sy = py - 5f + (f.y * 11f * 0.7f), sg = 1f + (tk.Surge * 0.5f), w = (2.4f + (lv * 0.8f)) * sg;
            foreach (Enemy m in tk.Targets)
            {
                if (m.Dead) continue;
                float mx = SurvivorSim.SX(m.Pos), my = SurvivorSim.SY(m.Pos);
                TierStream(g, sx, sy, mx, my - 4f, w, lv, 10f);
                if (lv >= 3)
                {
                    float dx = mx - px, dy = my - py, L = Mathf.Sqrt((dx * dx) + (dy * dy));
                    if (L == 0f) L = 1f;
                    float reach = 40f + (lv * 16f);
                    g.Save();
                    g.Lighter = true;
                    SampleCanvas.Paint lg = Linear(mx, my, mx + (dx / L * reach), my + (dy / L * reach));
                    lg.AddColorStop(0f, lv >= 6 ? Hsla(SHue(), 100f, 75f, 0.85f) : C(SurvivorSim.TierOf(lv).Core, 0.85f));
                    lg.AddColorStop(1f, C(160, 220, 255, 0f));
                    g.StrokeStyle = lg;
                    g.LineWidth = w * 0.7f;
                    g.RoundCap = true;
                    g.BeginPath();
                    g.MoveTo(mx, my - 4f);
                    g.LineTo(mx + (dx / L * reach), my - 4f + (dy / L * reach));
                    g.Stroke();
                    g.Restore();
                }
            }
            TierGlow(g, sx, sy, 3f + (lv * 0.5f), lv);
        }

        // ================================================================ 12. 장화
        private static readonly float[][] BootsTrailBandsD = { new[] { 30f, 0.25f }, new[] { 18f, 0.5f }, new[] { 7f, 0.9f } };

        // items-d.js:202-219 drawGround
        private void BootsGroundD(SampleCanvas g, SampleBoots b, int lv)
        {
            Tier T = SurvivorSim.TierOf(lv);
            float px = _sim.PX, py = _sim.PY;
            // 발밑 장화 빛(몸 아래에 깐다)
            TierGlow(g, px, py + 8f, 4f + (lv * 0.6f), Mathf.Max(2, lv));
            // 잔상(Lv4~)·발밑 물보라 꼬리
            if (lv == 6 && b.Trail.Count > 1)
            {
                g.Save();
                g.Lighter = true;
                g.RoundCap = true;
                g.RoundJoin = true;
                var tr = b.Trail;
                foreach (float[] band in BootsTrailBandsD)
                {
                    float w = band[0], a0 = band[1];
                    for (int i = 1; i < tr.Count; i++)
                    {
                        float k = 1f - (tr[i].K / 1.6f);
                        float ddx = tr[i].X - tr[i - 1].X, ddy = tr[i].Y - tr[i - 1].Y;
                        if (Mathf.Sqrt((ddx * ddx) + (ddy * ddy)) > 30f) continue;
                        g.StrokeStyle = w == 7f ? new Color(1f, 1f, 1f, a0 * k) : Hsla(SHue(i * 4f), 100f, w == 30f ? 60f : 72f, a0 * k);
                        g.LineWidth = w * (0.5f + (k * 0.5f));
                        g.BeginPath();
                        g.MoveTo(tr[i - 1].X, tr[i - 1].Y);
                        g.LineTo(tr[i].X, tr[i].Y);
                        g.Stroke();
                    }
                }
                g.Restore();
            }
            else if (lv >= 2)
            {
                var gh = b.Ghost;
                g.Save();
                g.Lighter = true;
                g.RoundCap = true;
                int n = 6 + (lv * 3);
                for (int i = 1; i < Mathf.Min(gh.Count, n); i++)
                {
                    float k = 1f - (i / (float)n);
                    g.StrokeStyle = C(T.Glow, 0.35f * k);
                    g.LineWidth = (6f + (lv * 2f)) * k;
                    g.BeginPath();
                    g.MoveTo(gh[i - 1].X, gh[i - 1].Y + 8f);
                    g.LineTo(gh[i].X, gh[i].Y + 8f);
                    g.Stroke();
                }
                g.Restore();
            }
        }

        // items-d.js:220-227 ents(잔상: 몸 실루엣이 뒤에 겹쳐 남는다). 서 있을 때는 잔상이 3D 몸 위에 겹치니 6px 안은 그리지 않는다.
        private void BootsEntsD(SampleCanvas g, SampleBoots b, int lv)
        {
            if (lv < 4) return;
            float px = _sim.PX, py = _sim.PY;
            var gh = b.Ghost;
            int n = lv == 4 ? 2 : lv == 5 ? 3 : 4;
            for (int i = 1; i <= n; i++)
            {
                if (gh.Count == 0) continue;
                SamplePtD q = gh[Mathf.Min(gh.Count - 1, i * 4)];
                if (Mathf.Abs(q.X - px) + Mathf.Abs(q.Y - py) < 6f) continue;
                float k = 1f - (i / (float)(n + 1));
                g.Save();
                g.GlobalAlpha = 0.4f * k;
                DrawPlayerD(g, q.X, q.Y);
                g.Restore();
                g.Save();
                g.Lighter = true;
                SampleCanvas.Paint gl = Radial(q.X, q.Y - 6f, 0f, q.X, q.Y - 6f, 18f);
                gl.AddColorStop(0f, lv >= 6 ? Hsla(SHue(i * 60f), 100f, 70f, 0.5f * k) : lv >= 5 ? C(255, 214, 110, 0.45f * k) : C(SurvivorSim.TierOf(lv).Glow, 0.45f * k));
                gl.AddColorStop(1f, new Color(0f, 0f, 0f, 0f));
                g.FillStyle = gl;
                g.BeginPath();
                g.Arc(q.X, q.Y - 6f, 18f, 0f, Tau);
                g.Fill();
                g.Restore();
            }
        }

        // items-d.js:228-231 drawAir(Lv6 발밑 제트)
        private void BootsAirD(SampleCanvas g, int lv)
        {
            if (lv < 6) return;
            float px = _sim.PX, py = _sim.PY;
            g.Save();
            g.Lighter = true;
            for (int sd = -1; sd <= 1; sd += 2)
            {
                SampleCanvas.Paint gl = Radial(px + (sd * 4f), py + 12f, 0f, px + (sd * 4f), py + 12f, 10f);
                gl.AddColorStop(0f, new Color(1f, 1f, 1f, 0.9f));
                gl.AddColorStop(0.4f, Hsla(SHue(), 100f, 70f, 0.6f));
                gl.AddColorStop(1f, new Color(0f, 0f, 0f, 0f));
                g.FillStyle = gl;
                g.BeginPath();
                g.Arc(px + (sd * 4f), py + 12f, 10f, 0f, Tau);
                g.Fill();
            }
            g.Restore();
        }

        // ================================================================ 13. 방화복
        // items-d.js:245-255 drawWing(물 불사조 날개: 깃털 6장이 겹친 가산 그라데이션)
        private void DrawWingD(SampleCanvas g, float x, float y, float side, float span, float k)
        {
            g.Save();
            g.Translate(x, y);
            g.Scale(side, 1f);
            g.Lighter = true;
            for (int i = 0; i < 6; i++)
            {
                float a = -0.9f + (i * 0.32f), L = span * (1f - (i * 0.09f)) * k, wdt = span * 0.2f * k;
                float ex = Mathf.Cos(a) * L, ey = Mathf.Sin(a) * L * 0.8f;
                SampleCanvas.Paint gr = Linear(0f, 0f, ex, ey);
                gr.AddColorStop(0f, new Color(1f, 1f, 1f, 0.9f));
                gr.AddColorStop(0.4f, Hsla(SHue(i * 40f), 100f, 72f, 0.7f));
                gr.AddColorStop(1f, C(120, 200, 255, 0f));
                g.FillStyle = gr;
                g.BeginPath();
                g.MoveTo(0f, 0f);
                g.QuadraticCurveTo((ex * 0.5f) - (ey * 0.3f * wdt / span * 4f), (ey * 0.5f) - wdt, ex, ey);
                g.QuadraticCurveTo(ex * 0.5f, (ey * 0.5f) + (wdt * 0.5f), 0f, 0f);
                g.Fill();
            }
            g.Restore();
        }

        // base.js:209 drawPlayer의 보호막 빛(s.shield): 몸 밑에 깐다.
        private void SuitShieldGlowD(SampleCanvas g, SampleSuit s)
        {
            if (s.Shield <= 0f) return;
            float px = _sim.PX, py = _sim.PY;
            float a = Mathf.Min(1f, s.Shield * 3f);
            g.Save();
            g.Lighter = true;
            SampleCanvas.Paint gr = Radial(px, py - 4f, 8f, px, py - 4f, 24f);
            gr.AddColorStop(0f, C(255, 220, 120, 0f));
            gr.AddColorStop(0.8f, C(255, 200, 90, 0.45f * a));
            gr.AddColorStop(1f, C(255, 240, 180, 0.9f * a));
            g.FillStyle = gr;
            g.BeginPath();
            g.Arc(px, py - 4f, 24f, 0f, Tau);
            g.Fill();
            g.Restore();
        }

        // items-d.js:292-312 ents(보호막 구·도는 고리·육각 반짝·불사조 날개·쓰러짐 그늘)
        private void SuitEntD(SampleCanvas g, SampleSuit s, int lv)
        {
            float px = _sim.PX, py = _sim.PY, t = _sim.ST;
            float R = s.RadiusAt(lv), hitk = Mathf.Max(0f, s.Shield) * 3f;
            Tier T = SurvivorSim.TierOf(lv);
            g.Save();
            g.Lighter = true;
            // 보호막 구
            SampleCanvas.Paint gr = Radial(px, py - 6f, R * 0.4f, px, py - 6f, R);
            Color c = lv >= 6 ? HslRgb(SHue(), 1f) : lv >= 5 ? C(255, 210, 110) : C(T.Glow, 1f);
            gr.AddColorStop(0f, new Color(c.r, c.g, c.b, 0f));
            gr.AddColorStop(0.75f, new Color(c.r, c.g, c.b, Mathf.Clamp01(0.12f + (lv * 0.05f) + (hitk * 0.2f))));
            gr.AddColorStop(1f, new Color(1f, 1f, 1f, Mathf.Clamp01(0.25f + (lv * 0.08f) + (hitk * 0.3f))));
            g.FillStyle = gr;
            g.BeginPath();
            g.Ellipse(px, py - 6f, R, R * 0.92f, 0f, 0f, Tau);
            g.Fill();
            // 두께: 도는 고리 수가 레벨마다 는다
            int rings = Mathf.CeilToInt(lv / 2f) + 1;
            for (int j = 0; j < rings; j++)
            {
                g.StrokeStyle = lv >= 6 ? Hsla(SHue(j * 90f), 100f, 72f, 0.8f) : lv >= 5 ? C(255, 220 - (j * 20), 130 - (j * 30), 0.8f) : C(T.Core, 0.5f + (lv * 0.06f));
                g.LineWidth = 1f + (lv * 0.35f);
                float st = (t * (2.5f + j) * (j % 2 == 1 ? -1f : 1f)) + j;
                g.BeginPath();
                g.Ellipse(px, py - 6f, R * (1f - (j * 0.06f)), R * 0.92f * (1f - (j * 0.06f)), 0f, st, st + 2.6f + (lv * 0.4f));
                g.Stroke();
            }
            // 육각 무늬 반짝(Lv3~)
            if (lv >= 3)
            {
                int hn = 6 + lv;
                for (int i = 0; i < hn; i++)
                {
                    float a = (t * 0.8f) + (i * Tau / hn), x = px + (Mathf.Cos(a) * R * 0.8f), y = py - 6f + (Mathf.Sin(a) * R * 0.72f);
                    g.FillStyle = new Color(1f, 1f, 1f, Mathf.Clamp01(0.25f + (0.25f * Mathf.Sin((t * 6f) + i))));
                    g.BeginPath();
                    for (int k = 0; k < 6; k++)
                    {
                        float b = k * Mathf.PI / 3f;
                        g.LineTo(x + (Mathf.Cos(b) * 2.6f), y + (Mathf.Sin(b) * 2.6f));
                    }
                    g.ClosePath();
                    g.Fill();
                }
            }
            g.Restore();
            // 불사조 날개(부활 뒤 상시 + 부활 순간 크게)
            if (lv == 6 && s.Wing >= 0f)
            {
                float w = s.Wing, big = w < 1.4f ? 1f + ((1f - SurvivorSim.Ease(w / 1.4f)) * 3.2f) : 1f, flap = 1f + (Mathf.Sin(t * 7f) * 0.15f);
                DrawWingD(g, px - 5f, py - 12f, -1f, 52f * big * flap, 1f);
                DrawWingD(g, px + 5f, py - 12f, 1f, 52f * big * flap, 1f);
            }
            // 쓰러짐 중엔 몸을 어둡게
            if (s.Ko > 0f)
            {
                g.Save();
                g.GlobalAlpha = 0.55f;
                g.FillStyle = Color.black;
                g.BeginPath();
                g.Ellipse(px, py - 4f, 16f, 18f, 0f, 0f, Tau);
                g.Fill();
                g.Restore();
            }
        }

        // items-d.js:313-322 drawAir. 샘플 체력 막대는 게임 머리 위 체력 막대가 이미 있어 그리지 않는다. '쓰러짐…'은 규칙 쪽 글자 파티클.
        private void SuitAirD(SampleCanvas g, SampleSuit s, int lv)
        {
            if (lv == 6 && s.Wing >= 0f && s.Wing < 1.6f)
            {
                float px = _sim.PX, py = _sim.PY;
                MetalText(g, "txt_phoenix", px, py - 60f - (s.Wing * 10f), 22f, s.Wing > 1.2f ? (1.6f - s.Wing) / 0.4f : 1f);
            }
        }
    }
}
