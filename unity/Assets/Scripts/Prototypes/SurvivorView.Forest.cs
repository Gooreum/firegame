using FireGame.Prototypes.Logic;
using FireGame.UnityLayer;
using UnityEngine;
using UnityEngine.UI;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 숲 구조대원 그림(승인 샘플 tools/levelup-art/items-e.js CrewScene 그대로): 문에서 튀어나온 사람 → 흰 빛으로 변신 → 방화복 대원.
    /// 합류 빛기둥·광선(인원 수만큼 커진다), 대원 물줄기(인원 수 등급), 금속 배너, 헬멧 8칸 HUD.
    /// 사람 몸은 3D 모델(소방관과 같은 그림체)에 샘플 색(주황 방화복·노란 헬멧)을 입힌다. 규칙은 SurvivorSim(SurvivorFree).
    /// </summary>
    public sealed partial class SurvivorView
    {
        /// <summary>대원·구한 사람 3D 몸 배율: 샘플 대원 줄(30~52px 반원)에서 서로·소방관을 덮지 않는 크기.</summary>
        private const float CrewScale = 1.15f;

        private Text _crewTitle;
        private Text _crewCount;
        private Text _crewX;

        private void BuildCrewHud()
        {
            _crewTitle = UiKit.OutlinedLabel(_hud, "CrewTitle", "구조대원", 46, Color.white, TextAnchor.MiddleLeft);
            UiKit.Place(_crewTitle.rectTransform, new Vector2(0f, 1f), new Vector2(56f, -248f), new Vector2(260f, 56f));
            _crewCount = UiKit.OutlinedLabel(_hud, "CrewCount", "", 46, Hex("#ffd9a8"), TextAnchor.MiddleLeft);
            UiKit.Place(_crewCount.rectTransform, new Vector2(0f, 1f), new Vector2(280f, -248f), new Vector2(200f, 56f));
            _crewX = UiKit.OutlinedLabel(_hud, "CrewX", "", 68, Color.white, TextAnchor.MiddleCenter);
            UiKit.Place(_crewX.rectTransform, new Vector2(0.5f, 1f), new Vector2(368f, -304f), new Vector2(220f, 90f));
            foreach (Text t in new[] { _crewTitle, _crewCount, _crewX }) t.gameObject.SetActive(false);
        }

        /// <summary>대원 3D 몸 색: 샘플 drawCrewman(주황 방화복 #f27a12, 노란 헬멧 #ffd21f, 반사띠).</summary>
        private static Color? CrewColor(string material)
        {
            if (material.StartsWith("Hat")) return new Color(1f, 0.82f, 0.12f);
            if (material.StartsWith("Vest")) return new Color(0.84f, 1f, 0.24f);
            if (material.StartsWith("Shirt")) return new Color(0.95f, 0.48f, 0.07f);
            if (material.StartsWith("Pants")) return new Color(0.7f, 0.3f, 0.02f);
            return null;
        }

        /// <summary>바닥 층: 합류 빛기둥 + 광선(샘플 drawJoinBack, items-e.js:246-271).</summary>
        private void DrawCrewGround(SampleCanvas g)
        {
            foreach (SJoin j in _sim.CrewJoins)
            {
                float k = j.Age / 1.4f, a = (j.Age < 0.1f ? j.Age / 0.1f : 1f) * Mathf.Max(0f, 1f - (k * k));
                if (a <= 0f) continue;
                bool gold = j.N >= 5, rb = j.N >= SurvivorSim.MaxCrew;
                g.Save();
                g.Lighter = true;
                float pw = 14f + (j.N * 1.5f);
                Color pc = rb ? HslRgb(SHue(), 1f) : gold ? C(255, 214, 110) : C(255, 240, 200);
                float top = j.Y - 160f;
                float[][] layers = { new[] { pw, 0.22f, 0f }, new[] { pw * 0.55f, 0.35f, 0f }, new[] { 2.5f, 0.8f, 1f } };
                foreach (float[] L in layers)
                {
                    float ww = L[0], al = L[1];
                    Color c = L[2] > 0f ? Color.white : pc;
                    SampleCanvas.Paint vg = Linear(0f, top, 0f, j.Y);
                    vg.AddColorStop(0f, new Color(c.r, c.g, c.b, 0f));
                    vg.AddColorStop(0.7f, new Color(c.r, c.g, c.b, al * a));
                    vg.AddColorStop(1f, new Color(c.r, c.g, c.b, al * a * 1.2f));
                    g.FillStyle = vg;
                    g.BeginPath();
                    g.RoundRect(j.X - ww, top, ww * 2f, 160f, ww);
                    g.Fill();
                }
                int nr = 10 + (j.N * 4);
                float len = (60f + (j.N * 14f)) * (0.6f + (SurvivorSim.Ease(j.Age / 0.3f) * 0.4f));
                g.Translate(j.X, j.Y - 10f);
                g.Rotate(j.Age * 1.2f);
                for (int i = 0; i < nr; i++)
                {
                    float ang = i / (float)nr * Tau;
                    Color c = rb ? HslRgb(SHue(i * 360f / nr), 1f) : gold ? (i % 2 == 1 ? C(255, 214, 110) : C(255, 248, 220)) : (i % 2 == 1 ? C(255, 200, 120) : C(255, 245, 225));
                    SampleCanvas.Paint gr = Linear(0f, 0f, Mathf.Cos(ang) * len, Mathf.Sin(ang) * len);
                    gr.AddColorStop(0f, new Color(c.r, c.g, c.b, 0.7f * a));
                    gr.AddColorStop(1f, new Color(c.r, c.g, c.b, 0f));
                    g.FillStyle = gr;
                    g.BeginPath();
                    g.MoveTo(0f, 0f);
                    g.LineTo(Mathf.Cos(ang - 0.07f) * len, Mathf.Sin(ang - 0.07f) * len * 0.8f);
                    g.LineTo(Mathf.Cos(ang + 0.07f) * len, Mathf.Sin(ang + 0.07f) * len * 0.8f);
                    g.ClosePath();
                    g.Fill();
                }
                g.Restore();
            }
        }

        /// <summary>공중 층: 변신 빛·갓 합류한 대원의 빛·대원 물줄기(items-e.js:306-325). 몸은 3D 모델.</summary>
        private void DrawCrew(SampleCanvas g)
        {
            float t = _sim.ST;
            for (int i = 0; i < _sim.Civs.Count; i++)
            {
                SCiv c = _sim.Civs[i];
                ShadowAt(g, c.X, c.Y + 9f, 8f * (1f - Mathf.Min(0.5f, c.Z / 60f)), 0.3f);
                GameObject person = _people.Get(CivilianModel(c.Idx));
                if (person != null)
                {
                    person.transform.localScale = _personScale * CrewScale;
                    Models3D.Pose(person, SGround(new Vector2(c.X, c.Y)) + Up(c.Z * SurvivorSim.Px), new Vector3(0f, -1f, 0f));
                    Models3D.Play(person, c.State == 0 ? "Run" : "Idle", 1.2f, _time + i);
                    Models3D.Tint(person, Color.white, CivilianColor(c.Idx), 10 + CivilianKind(c.Idx));
                }
                if (c.State == 1)
                {
                    float u = c.Age / 0.3f;
                    g.Save();
                    g.Lighter = true;
                    SampleCanvas.Paint gl = Radial(c.X, c.Y - 6f, 0f, c.X, c.Y - 6f, 26f);
                    gl.AddColorStop(0f, C(255, 255, 240, 0.9f * u));
                    gl.AddColorStop(1f, C(255, 220, 140, 0f));
                    g.FillStyle = gl;
                    g.BeginPath();
                    g.Arc(c.X, c.Y - 6f, 26f, 0f, Tau);
                    g.Fill();
                    g.Restore();
                }
            }
            int lvl = _sim.CrewTier;
            for (int i = 0; i < _sim.CrewList.Count; i++)
            {
                Crew c = _sim.CrewList[i];
                float born = t - c.Born;
                ShadowAt(g, c.X, c.Y + 9f, 9f, 0.32f);
                GameObject person = _people.Get("People/Worker_Male");
                bool aiming = c.Aim != null && !c.Aim.Dead && c.AimAge > 0f;
                if (person != null)
                {
                    person.transform.localScale = _personScale * CrewScale;
                    float aimX = aiming ? SurvivorSim.SX(c.Aim.Pos) - c.X : c.Face;
                    float aimY = aiming ? -(SurvivorSim.SY(c.Aim.Pos) - c.Y) : 0f;
                    Models3D.Pose(person, SGround(new Vector2(c.X, c.Y)), new Vector3(aimX, aimY, 0f));
                    Models3D.Play(person, c.Moving ? "Run" : "Idle", 1.1f, _time + i);
                    Models3D.Tint(person, Color.white, CrewColor, 3);
                }
                if (born < 0.6f)
                {
                    g.Save();
                    g.Lighter = true;
                    float a = 1f - (born / 0.6f);
                    SampleCanvas.Paint gl = Radial(c.X, c.Y - 6f, 0f, c.X, c.Y - 6f, 22f);
                    gl.AddColorStop(0f, C(255, 250, 230, a));
                    gl.AddColorStop(1f, C(255, 200, 100, 0f));
                    g.FillStyle = gl;
                    g.BeginPath();
                    g.Arc(c.X, c.Y - 6f, 22f, 0f, Tau);
                    g.Fill();
                    g.Restore();
                }
                if (aiming) TierStream(g, c.X + (c.Face * 9f), c.Y - 3f, SurvivorSim.SX(c.Aim.Pos), SurvivorSim.SY(c.Aim.Pos) - 4f, 2.6f + (lvl * 0.35f), lvl, 8f);
            }
        }

        // items-e.js:106-116
        private void HelmetIcon(SampleCanvas g, float x, float y, float r, bool on, float glow)
        {
            g.Save();
            g.Translate(x, y);
            if (on && glow > 0f)
            {
                g.Lighter = true;
                SampleCanvas.Paint gl = Radial(0f, 0f, 0f, 0f, 0f, r * 2.4f);
                gl.AddColorStop(0f, C(255, 210, 80, 0.7f * glow));
                gl.AddColorStop(1f, C(255, 160, 40, 0f));
                g.FillStyle = gl;
                g.BeginPath();
                g.Arc(0f, 0f, r * 2.4f, 0f, Tau);
                g.Fill();
                g.Lighter = false;
            }
            g.LineWidth = 1.2f;
            g.StrokeStyle = on ? Hex("#4a3300") : C(120, 130, 150, 0.6f);
            SampleCanvas.Paint hl = Radial(-r * 0.3f, -r * 0.5f, 1f, 0f, 0f, r * 1.2f);
            if (on)
            {
                hl.AddColorStop(0f, Hex("#fff6b0"));
                hl.AddColorStop(0.5f, Hex("#ffd21f"));
                hl.AddColorStop(1f, Hex("#c79400"));
            }
            else
            {
                hl.AddColorStop(0f, C(70, 78, 98, 0.9f));
                hl.AddColorStop(1f, C(40, 46, 62, 0.9f));
            }
            g.FillStyle = hl;
            g.BeginPath();
            g.Arc(0f, r * 0.2f, r, Mathf.PI, Tau);
            g.ClosePath();
            g.Fill();
            g.Stroke();
            g.BeginPath();
            g.Ellipse(0f, r * 0.25f, r * 1.35f, r * 0.32f, 0f, 0f, Tau);
            g.Fill();
            g.Stroke();
            if (on)
            {
                g.FillStyle = Hex("#e8352a");
                g.BeginPath();
                g.Arc(0f, -r * 0.45f, r * 0.26f, 0f, Tau);
                g.Fill();
            }
            g.Restore();
        }

        /// <summary>화면 층: 합류 배너(items-e.js:333-345) + 헬멧 8칸 칸(346-355, 아이템 칸 아래).</summary>
        private void DrawCrewScreen(SampleCanvas g)
        {
            int n = _sim.CrewList.Count;
            bool show = (n > 0 || _sim.Civs.Count > 0) && _sim.PendingChoices == null;
            _crewTitle.gameObject.SetActive(show);
            _crewCount.gameObject.SetActive(show);
            if (show)
            {
                const float Y0 = 50f;
                g.FillStyle = C(10, 14, 28, 0.8f);
                g.BeginPath();
                g.RoundRect(6f, Y0, 232f, 42f, 9f);
                g.Fill();
                g.StrokeStyle = n >= SurvivorSim.MaxCrew ? Hsla(SHue(), 100f, 70f, 0.9f) : n >= 5 ? C(255, 214, 110, 0.9f) : C(255, 170, 80, 0.5f);
                g.LineWidth = 1.5f;
                g.Stroke();
                for (int i = 0; i < SurvivorSim.MaxCrew; i++) HelmetIcon(g, 20f + (i * 20f), Y0 + 28f, 6.2f, i < n, i == n - 1 ? _sim.CrewHudGlow : 0f);
                _crewCount.text = n + " / " + SurvivorSim.MaxCrew;
                _crewCount.color = n >= 5 ? Hex("#ffe27a") : Hex("#ffd9a8");
            }
            SJoin b = _sim.CrewBanner;
            _crewX.gameObject.SetActive(b != null);
            if (b == null) return;
            float a = b.Age;
            float pop = a < 0.14f ? SurvivorSim.Ease(a / 0.14f) * 1.25f : a < 0.28f ? Mathf.Lerp(1.25f, 1f, (a - 0.14f) / 0.14f) : 1f;
            float alpha = a > 1.2f ? (1.5f - a) / 0.3f : 1f;
            bool rb = b.N >= SurvivorSim.MaxCrew;
            float W = SampleW;
            g.Save();
            g.Translate(W / 2f, 64f);
            g.Scale(pop, pop);
            const float Bh = 30f;
            SampleCanvas.Paint bg = Linear(0f, -Bh, 0f, Bh);
            bg.AddColorStop(0f, C(30, 12, 4, 0f));
            bg.AddColorStop(0.5f, C(30, 12, 4, 0.7f * alpha));
            bg.AddColorStop(1f, C(30, 12, 4, 0f));
            g.FillStyle = bg;
            g.FillRect(-170f, -Bh, 340f, Bh * 2f);
            g.Restore();
            MetalText(g, rb ? "txt_allcrew" : "txt_crew", W / 2f, 58f, 22f * pop, alpha);
            _crewX.text = "×" + b.N;
            _crewX.color = b.N >= 5 ? new Color(1f, 0.85f, 0.35f, alpha) : new Color(0.92f, 0.96f, 1f, alpha);
            _crewX.rectTransform.localScale = Vector3.one * pop;
        }
    }
}
