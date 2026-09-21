using FireGame.Core.Game;
using UnityEngine;
using UnityEngine.UI;

namespace FireGame.UnityLayer
{
    /// <summary>
    /// 현장이 끝난 뒤의 별점과 정산. 무엇이 보상을 깎았는지 항목별로 보여 줘야
    /// 다음 판에 물을 아끼고 건물을 더 지키려고 한다.
    /// </summary>
    public sealed class ResultScreen
    {
        private readonly RectTransform _root;

        public ResultScreen(Canvas canvas, GameFlow flow)
        {
            bool won = flow.LastOutcome == StageOutcome.Won;
            MissionDef mission = flow.CurrentMission;
            PayoutBreakdown payout = flow.LastPayout;

            _root = UiKit.Stretch(UiKit.Node(canvas.transform, "ResultScreen"));

            Image dim = UiKit.Image(_root, "Dim", Art.White, UiKit.Shade);
            UiKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            Image panel = UiKit.Image(_root, "Panel", Art.Get("UI/panel_grey"), Color.white);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 860f));
            panel.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            Image header = UiKit.Image(panel.transform, "Header", Art.Get(won ? "UI/button_green" : "UI/button_red"), Color.white);
            UiKit.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 40f), new Vector2(620f, 110f));
            header.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Text headerText = UiKit.OutlinedLabel(header.transform, "Label", won ? "진압 성공!" : "진압 실패", 56, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(headerText.rectTransform);
            headerText.rectTransform.offsetMin = new Vector2(0f, 8f);

            Text subtitle = UiKit.Label(panel.transform, "Mission", mission != null ? mission.Location + " · " + mission.Title : string.Empty, 30, new Color(0.35f, 0.35f, 0.4f), TextAnchor.MiddleCenter);
            UiKit.Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(900f, 50f));
            subtitle.rectTransform.pivot = new Vector2(0.5f, 1f);

            // 별
            for (int s = 0; s < StarRating.MaxStars; s++)
            {
                Image star = MapScreen.StarIcon(panel.transform, "Star" + s, s < flow.LastStars);
                float lift = s == 1 ? 24f : 0f;   // 가운데 별을 살짝 올려 왕관 모양으로
                UiKit.Place(star.rectTransform, new Vector2(0.5f, 1f), new Vector2((s - 1) * 150f, -140f + lift), new Vector2(128f, 120f));
                star.rectTransform.pivot = new Vector2(0.5f, 1f);
            }

            if (flow.LastWasNewBest && won)
            {
                Text best = UiKit.OutlinedLabel(panel.transform, "NewBest", "최고 기록!", 30, new Color(1f, 0.85f, 0.2f), TextAnchor.MiddleCenter);
                UiKit.Place(best.rectTransform, new Vector2(0.5f, 1f), new Vector2(330f, -170f), new Vector2(200f, 50f));
                best.rectTransform.pivot = new Vector2(0.5f, 1f);
                best.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            }

            if (won)
            {
                BuildBreakdown(panel.transform, payout);

                string debrief = mission != null && mission.Debrief.Length > 0 ? mission.Debrief[0] : string.Empty;
                Text quote = UiKit.Label(panel.transform, "Debrief", Campaign.ChiefName + ": " + debrief, 28, new Color(0.25f, 0.35f, 0.6f), TextAnchor.MiddleCenter);
                UiKit.Place(quote.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(980f, 60f));
                quote.rectTransform.pivot = new Vector2(0.5f, 0f);
            }
            else
            {
                Text reason = UiKit.Label(panel.transform, "Reason", FailureReason(flow.LastOutcome), 40, new Color(0.75f, 0.2f, 0.15f), TextAnchor.MiddleCenter);
                UiKit.Place(reason.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(980f, 80f));
                reason.rectTransform.pivot = new Vector2(0.5f, 0.5f);

                Text tip = UiKit.Label(panel.transform, "Tip", FailureTip(flow.LastOutcome), 30, UiKit.Ink, TextAnchor.MiddleCenter);
                UiKit.Place(tip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(960f, 100f));
                tip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }

            UnityEngine.UI.Button retry = UiKit.Button(panel.transform, "Retry", Art.Get("UI/button_blue"), "다시 하기", 38, flow.RetryMission);
            UiKit.Place((RectTransform)retry.transform, new Vector2(0.5f, 0f), new Vector2(-190f, 40f), new Vector2(340f, 110f));
            ((RectTransform)retry.transform).pivot = new Vector2(0.5f, 0f);

            UnityEngine.UI.Button map = UiKit.Button(panel.transform, "Map", Art.Get("UI/button_yellow"), "지도로", 38, flow.BackToMap);
            UiKit.Place((RectTransform)map.transform, new Vector2(0.5f, 0f), new Vector2(190f, 40f), new Vector2(340f, 110f));
            ((RectTransform)map.transform).pivot = new Vector2(0.5f, 0f);
        }

        public void Destroy()
        {
            UiKit.Discard(_root.gameObject);
        }

        private static void BuildBreakdown(Transform panel, PayoutBreakdown payout)
        {
            string[] labels = { "기본 보상", "구조 보너스", "건물 보존", "남은 시간", "수손 피해" };
            int[] amounts = { payout.Base, payout.Rescue, payout.Integrity, payout.Time, payout.WaterDamage };

            for (int i = 0; i < labels.Length; i++)
            {
                float y = -300f - (i * 44f);
                Text label = UiKit.Label(panel, "Label" + i, labels[i], 32, UiKit.Ink, TextAnchor.MiddleLeft);
                UiKit.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(-300f, y), new Vector2(300f, 44f));
                label.rectTransform.pivot = new Vector2(0f, 0.5f);

                bool negative = amounts[i] < 0;
                string text = (negative ? "-" : "+") + Format.Money(Mathf.Abs(amounts[i]));
                Text value = UiKit.Label(panel, "Value" + i, text, 32, negative ? new Color(0.85f, 0.2f, 0.15f) : UiKit.Ink, TextAnchor.MiddleRight);
                UiKit.Place(value.rectTransform, new Vector2(0.5f, 1f), new Vector2(300f, y), new Vector2(300f, 44f));
                value.rectTransform.pivot = new Vector2(1f, 0.5f);
            }

            Image line = UiKit.Image(panel, "Line", Art.White, new Color(0f, 0f, 0f, 0.2f));
            UiKit.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -300f - (labels.Length * 44f) + 14f), new Vector2(600f, 3f));
            line.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            float totalY = -300f - (labels.Length * 44f) - 20f;
            Text totalLabel = UiKit.Label(panel, "TotalLabel", "합계", 40, UiKit.Ink, TextAnchor.MiddleLeft);
            UiKit.Place(totalLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(-300f, totalY), new Vector2(300f, 56f));
            totalLabel.rectTransform.pivot = new Vector2(0f, 0.5f);

            Text totalValue = UiKit.Label(panel, "TotalValue", Format.Money(payout.Total), 44, new Color(0.15f, 0.55f, 0.2f), TextAnchor.MiddleRight);
            UiKit.Place(totalValue.rectTransform, new Vector2(0.5f, 1f), new Vector2(300f, totalY), new Vector2(300f, 56f));
            totalValue.rectTransform.pivot = new Vector2(1f, 0.5f);
        }

        private static string FailureReason(StageOutcome outcome)
        {
            switch (outcome)
            {
                case StageOutcome.LostBuildingDestroyed: return "건물이 다 타 버렸다";
                case StageOutcome.LostPlayerDown: return "불길에 쓰러졌다";
                case StageOutcome.LostTimeUp: return "시간이 다 됐다";
                default: return string.Empty;
            }
        }

        /// <summary>왜 졌는지에 맞는 다음 판 요령.</summary>
        private static string FailureTip(StageOutcome outcome)
        {
            switch (outcome)
            {
                case StageOutcome.LostBuildingDestroyed:
                    return "불꽃을 쫓지 말고, 불이 아직 안 닿은 벽을 먼저 적셔서 길을 끊어 보자.";
                case StageOutcome.LostPlayerDown:
                    return "불 옆에 너무 오래 있었다. 체력이 줄면 잠깐 물러나면 다시 찬다.";
                case StageOutcome.LostTimeUp:
                    return "꺼지지 않는 불이 남았다면 장비가 안 맞는 것일 수 있다. 상점을 확인해 보자.";
                default:
                    return string.Empty;
            }
        }
    }
}
