using FireGame.Core.Game;
using UnityEngine;
using UnityEngine.UI;

namespace FireGame.UnityLayer
{
    /// <summary>
    /// 출동 전 서장의 브리핑. 지도를 어둡게 덮고 대화 패널을 띄운다.
    /// 이번 현장에 어떤 불이 나는지, 무엇을 들고 가야 하는지를 대사로 알려 준다.
    /// </summary>
    public sealed class BriefingScreen
    {
        private readonly RectTransform _root;

        public BriefingScreen(Canvas canvas, GameFlow flow)
        {
            MissionDef mission = flow.CurrentMission;
            _root = UiKit.Stretch(UiKit.Node(canvas.transform, "BriefingScreen"));

            Image dim = UiKit.Image(_root, "Dim", Art.White, UiKit.Shade);
            UiKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;   // 뒤의 지도 버튼이 눌리지 않게 막는다

            Image panel = UiKit.Image(_root, "Panel", Art.Get("UI/panel_grey"), Color.white);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1320f, 780f));
            panel.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // 머리말: 신고 현장
            Image header = UiKit.Image(panel.transform, "Header", Art.Get("UI/button_red"), Color.white);
            UiKit.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 40f), new Vector2(760f, 100f));
            header.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Text headerText = UiKit.OutlinedLabel(header.transform, "Label", "출동 · " + mission.Location, 42, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(headerText.rectTransform);
            headerText.rectTransform.offsetMin = new Vector2(0f, 8f);

            // 서장 초상
            Image portraitBack = UiKit.Image(panel.transform, "PortraitBack", Art.Get("UI/panel_blue"), Color.white);
            UiKit.Place(portraitBack.rectTransform, new Vector2(0f, 1f), new Vector2(60f, -110f), new Vector2(240f, 240f));
            Image portrait = UiKit.Image(portraitBack.transform, "Portrait", Art.Get("TopDown/civilian_old"), Color.white);
            UiKit.Place(portrait.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(150f, 150f));
            portrait.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            portrait.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);   // 그림이 옆을 보고 있어 정면처럼 세운다
            portrait.preserveAspect = true;

            Text name = UiKit.Label(panel.transform, "Name", Campaign.ChiefName, 36, UiKit.Ink, TextAnchor.MiddleCenter);
            UiKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(60f, -360f), new Vector2(240f, 50f));

            // 제목과 대사
            Text title = UiKit.Label(panel.transform, "Title", mission.Title, 48, new Color(0.8f, 0.2f, 0.1f), TextAnchor.UpperLeft);
            UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(340f, -110f), new Vector2(920f, 64f));

            Text lines = UiKit.Label(panel.transform, "Lines", string.Join("\n\n", mission.Briefing), 30, UiKit.Ink, TextAnchor.UpperLeft);
            UiKit.Place(lines.rectTransform, new Vector2(0f, 1f), new Vector2(340f, -180f), new Vector2(920f, 300f));
            lines.lineSpacing = 1.05f;

            bool ready = BuildRequirements(panel.transform, flow.Save, mission);

            // 버튼
            UnityEngine.UI.Button back = UiKit.Button(panel.transform, "Back", Art.Get("UI/button_grey"), "돌아가기", 36, flow.BackToMap);
            UiKit.Place((RectTransform)back.transform, new Vector2(1f, 0f), new Vector2(-400f, 40f), new Vector2(300f, 110f));

            UnityEngine.UI.Button go = UiKit.Button(panel.transform, "Go", Art.Get("UI/button_red"), "출동!", 44, flow.BeginMission);
            UiKit.Place((RectTransform)go.transform, new Vector2(1f, 0f), new Vector2(-50f, 40f), new Vector2(320f, 110f));

            if (!ready)
            {
                // 모자란 장비는 상점에서 바로 올린다. 그래도 출동은 막지 않는다 — 해 보고 느끼는 것도 배움이다.
                UnityEngine.UI.Button shop = UiKit.Button(panel.transform, "Shop", Art.Get("UI/button_yellow"), "상점으로", 36, flow.GoToShop);
                UiKit.Place((RectTransform)shop.transform, new Vector2(1f, 0f), new Vector2(-730f, 40f), new Vector2(300f, 110f));
            }
        }

        /// <summary>
        /// "필요 장비" 줄. 갖춘 건 초록, 모자란 건 빨강 칩으로 보여 주고, 모자라면 경고를 단다.
        /// 모두 갖췄으면 true.
        /// </summary>
        private static bool BuildRequirements(Transform panel, SaveData save, MissionDef mission)
        {
            const float rowY = -500f;

            Text heading = UiKit.Label(panel, "NeedLabel", "필요 장비", 32, UiKit.Ink, TextAnchor.MiddleLeft);
            UiKit.Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(60f, rowY), new Vector2(240f, 64f));

            if (mission.Requirements.Length == 0)
            {
                Text none = UiKit.Label(panel, "NeedNone", "기본 장비로 충분하다", 30, new Color(0.15f, 0.5f, 0.2f), TextAnchor.MiddleLeft);
                UiKit.Place(none.rectTransform, new Vector2(0f, 1f), new Vector2(340f, rowY), new Vector2(900f, 64f));
                return true;
            }

            bool ready = true;
            for (int i = 0; i < mission.Requirements.Length; i++)
            {
                Requirement requirement = mission.Requirements[i];
                int have = save.LevelOf(requirement.TrackId);
                bool met = have >= requirement.Level;
                ready &= met;

                string state = met ? "준비됨" : have == 0 ? "지금 없음" : "지금 Lv" + have;
                Image chip = UiKit.Image(panel, "Need" + i, Art.Get(met ? "UI/button_green" : "UI/button_red"), Color.white);
                UiKit.Place(chip.rectTransform, new Vector2(0f, 1f), new Vector2(340f + (i * 450f), rowY), new Vector2(430f, 64f));
                Text chipText = UiKit.OutlinedLabel(chip.transform, "Label", Format.Requirement(requirement.TrackId, requirement.Level) + " · " + state, 28, Color.white, TextAnchor.MiddleCenter);
                UiKit.Stretch(chipText.rectTransform);
                chipText.rectTransform.offsetMin = new Vector2(0f, 6f);
            }

            if (!ready)
            {
                int cost = Readiness.CostToReady(save, mission);
                Text warning = UiKit.Label(panel, "NeedWarning",
                    "장비가 모자라다. 이대로면 불을 다 못 끈다. (필요 " + Format.Money(cost) + " · 보유 " + Format.Money(save.Money) + ")",
                    28, new Color(0.8f, 0.2f, 0.15f), TextAnchor.MiddleLeft);
                UiKit.Place(warning.rectTransform, new Vector2(0f, 1f), new Vector2(340f, rowY - 72f), new Vector2(940f, 48f));
            }

            return ready;
        }

        public void Destroy()
        {
            UiKit.Discard(_root.gameObject);
        }
    }
}
