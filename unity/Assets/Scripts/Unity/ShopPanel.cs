using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using UnityEngine;
using UnityEngine.UI;

namespace FireGame.UnityLayer
{
    /// <summary>
    /// 소방서 상점. 장비 4종·방화복·소방화를 레벨업한다.
    /// 카드마다 지금 레벨과 다음 레벨에서 무엇이 좋아지는지를 적어, 돈을 어디에 쓸지 고르게 한다.
    /// </summary>
    public sealed class ShopPanel
    {
        /// <summary>장비별 쓰임새. 코어는 규칙만 알고 설명은 화면 쪽 몫이다.</summary>
        private static readonly Dictionary<int, string> Uses = new Dictionary<int, string>
        {
            { EquipmentId.Bucket, "물 · 나무 불\n앞 3칸에 끼얹기" },
            { EquipmentId.Extinguisher, "CO2 · 전기 불\n앞 6칸에 뿜기" },
            { EquipmentId.Hose, "물 · 큰 나무 불\n멀리 곧게 쏘기" },
            { EquipmentId.FoamExtinguisher, "폼 · 기름 불\n앞 6칸에 뿜기" },
            { GearId.Suit, "불 피해를 줄인다\n옷도 바뀐다" },
            { GearId.Boots, "더 빨리 달린다" },
        };

        private static readonly Color EffectColor = new Color(1f, 0.93f, 0.45f);

        private const float CardWidth = 272f;
        private const float CardHeight = 500f;
        private const float CardGap = 16f;

        private readonly RectTransform _root;

        public ShopPanel(Canvas canvas, GameFlow flow)
        {
            SaveData save = flow.Save;
            _root = UiKit.Stretch(UiKit.Node(canvas.transform, "ShopPanel"));

            Image dim = UiKit.Image(_root, "Dim", Art.White, UiKit.Shade);
            UiKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            UpgradeTrack[] tracks = UpgradeCatalog.All;
            float cardsWidth = (tracks.Length * CardWidth) + ((tracks.Length - 1) * CardGap);

            Image panel = UiKit.Image(_root, "Panel", Art.Get("UI/panel_grey"), Color.white);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(cardsWidth + 80f, 800f));
            panel.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            Image header = UiKit.Image(panel.transform, "Header", Art.Get("UI/button_yellow"), Color.white);
            UiKit.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 40f), new Vector2(620f, 104f));
            header.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Text title = UiKit.OutlinedLabel(header.transform, "Label", "소방서 상점", 50, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(title.rectTransform);
            title.rectTransform.offsetMin = new Vector2(0f, 8f);

            Text money = UiKit.Label(panel.transform, "Money", "보유금 " + Format.Money(save.Money), 38, new Color(0.15f, 0.5f, 0.2f), TextAnchor.MiddleCenter);
            UiKit.Place(money.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(600f, 60f));
            money.rectTransform.pivot = new Vector2(0.5f, 1f);

            // 다음 신고에 모자란 장비. 그 카드에 리본을 달아 "뭘 사야 하는지"를 바로 보이게 한다.
            var needed = new HashSet<int>();
            foreach (Shortfall shortfall in Readiness.Missing(save, Readiness.NextCall(save))) needed.Add(shortfall.TrackId);

            for (int i = 0; i < tracks.Length; i++)
            {
                float x = -cardsWidth / 2f + (i * (CardWidth + CardGap)) + (CardWidth / 2f);
                BuildCard(panel.transform, flow, tracks[i], x, needed.Contains(tracks[i].Id));
            }

            UnityEngine.UI.Button close = UiKit.Button(panel.transform, "Close", Art.Get("UI/button_grey"), "닫기", 38, flow.CloseShop);
            UiKit.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(300f, 100f));
            ((RectTransform)close.transform).pivot = new Vector2(0.5f, 0f);
        }

        public void Destroy()
        {
            UiKit.Discard(_root.gameObject);
        }

        private static void BuildCard(Transform panel, GameFlow flow, UpgradeTrack track, float x, bool neededNext)
        {
            SaveData save = flow.Save;
            int level = save.LevelOf(track.Id);
            int cost = Shop.NextCost(save, track.Id);
            bool maxed = cost < 0;
            bool affordable = !maxed && save.Money >= cost;

            Image card = UiKit.Image(panel, "Card" + track.Id, Art.Get("UI/panel_blue"), level == 0 ? new Color(0.78f, 0.82f, 0.9f) : Color.white);
            UiKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, 20f), new Vector2(CardWidth, CardHeight));
            card.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            Text name = UiKit.OutlinedLabel(card.transform, "Name", track.Name, 36, Color.white, TextAnchor.UpperCenter);
            UiKit.Stretch(name.rectTransform);
            name.rectTransform.offsetMax = new Vector2(0f, -22f);

            BuildLevelRow(card.transform, track, level);

            if (track.Kind == UpgradeKind.Suit)
            {
                // 다음 레벨(최대면 지금) 옷을 미리 보여 준다. 방화복엔 특성이 없어 그 자리를 쓴다.
                int shown = maxed ? level : level + 1;
                Image suit = UiKit.Image(card.transform, "Suit", Art.Get(MissionWorldView.SuitSprite(shown)), Color.white);
                UiKit.Place(suit.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -138f), new Vector2(56f, 68f));
                suit.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                suit.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                suit.preserveAspect = true;
            }
            else
            {
                string milestone = LevelPreview.NextMilestone(track, level);
                if (milestone != null)
                {
                    Text next = UiKit.Label(card.transform, "Milestone", "다음 특성 " + milestone, 20, new Color(1f, 0.93f, 0.6f), TextAnchor.UpperCenter);
                    UiKit.Place(next.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(CardWidth - 24f, 50f));
                    next.rectTransform.pivot = new Vector2(0.5f, 1f);
                }
            }

            string use;
            Uses.TryGetValue(track.Id, out use);
            Text uses = UiKit.Label(card.transform, "Use", use ?? string.Empty, 22, new Color(1f, 1f, 1f, 0.9f), TextAnchor.UpperCenter);
            UiKit.Place(uses.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -172f), new Vector2(CardWidth - 24f, 60f));
            uses.rectTransform.pivot = new Vector2(0.5f, 1f);

            // 다음 레벨이 현장에서 무엇을 바꾸는지 "게임 말"로: 몇 발에 꺼지는지, 버티는 시간, 달리는 속도.
            string effectText = string.Join("\n", LevelPreview.NextLevelLines(save, track).ToArray());
            Text effect = UiKit.OutlinedLabel(card.transform, "Effect", effectText, 21, EffectColor, TextAnchor.MiddleCenter);
            UiKit.Place(effect.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 146f), new Vector2(CardWidth - 14f, 124f));
            effect.rectTransform.pivot = new Vector2(0.5f, 0f);
            effect.lineSpacing = 1.0f;

            // 상태별 버튼: 최대(회색) / 살 수 있음(초록) / 잔액 부족(빨강)
            string sprite = maxed ? "UI/button_grey" : affordable ? "UI/button_green" : "UI/button_red";
            string label = maxed ? "최대" : (level == 0 ? "해금 " : "레벨업 ") + Format.Money(cost);
            int id = track.Id;
            UnityEngine.UI.Button buy = UiKit.Button(card.transform, "Buy", Art.Get(sprite), label, 30, () => flow.Upgrade(id));
            UiKit.Place((RectTransform)buy.transform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(CardWidth - 28f, 92f));
            ((RectTransform)buy.transform).pivot = new Vector2(0.5f, 0f);
            buy.interactable = affordable;

            // 비활성 버튼에 Unity가 기본으로 회색을 덧칠하면 빨강이 보라색이 된다.
            // 상태는 그림 색(회색/초록/빨강)으로 이미 구분되므로 덧칠을 끈다.
            ColorBlock colors = buy.colors;
            colors.disabledColor = Color.white;
            buy.colors = colors;

            if (!maxed && !affordable)
            {
                Text need = UiKit.Label(card.transform, "Need", "잔액 부족", 22, new Color(1f, 0.85f, 0.85f), TextAnchor.MiddleCenter);
                UiKit.Place(need.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 116f), new Vector2(CardWidth - 20f, 28f));
                need.rectTransform.pivot = new Vector2(0.5f, 0f);
            }

            if (neededNext)
            {
                Image ribbon = UiKit.Image(card.transform, "NeededNext", Art.Get("UI/button_yellow"), Color.white);
                UiKit.Place(ribbon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 26f), new Vector2(CardWidth - 20f, 48f));
                ribbon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                Text ribbonText = UiKit.OutlinedLabel(ribbon.transform, "Label", "다음 신고에 필요", 24, Color.white, TextAnchor.MiddleCenter);
                UiKit.Stretch(ribbonText.rectTransform);
                ribbonText.rectTransform.offsetMin = new Vector2(0f, 5f);
            }
        }

        /// <summary>"Lv.7". 어떤 항목에도 끝이 없어 "/ 최대"를 적지 않는다.</summary>
        private static void BuildLevelRow(Transform card, UpgradeTrack track, int level)
        {
            string text = level == 0 ? "잠김" : "Lv." + level;
            Text label = UiKit.OutlinedLabel(card, "Level", text, 30, level == 0 ? Color.white : EffectColor, TextAnchor.MiddleCenter);
            UiKit.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(CardWidth - 20f, 40f));
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
        }
    }
}
