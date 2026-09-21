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

            for (int i = 0; i < tracks.Length; i++)
            {
                float x = -cardsWidth / 2f + (i * (CardWidth + CardGap)) + (CardWidth / 2f);
                BuildCard(panel.transform, flow, tracks[i], x);
            }

            UnityEngine.UI.Button close = UiKit.Button(panel.transform, "Close", Art.Get("UI/button_grey"), "닫기", 38, flow.CloseShop);
            UiKit.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(300f, 100f));
            ((RectTransform)close.transform).pivot = new Vector2(0.5f, 0f);
        }

        public void Destroy()
        {
            UiKit.Discard(_root.gameObject);
        }

        private static void BuildCard(Transform panel, GameFlow flow, UpgradeTrack track, float x)
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

            BuildLevelRow(card.transform, level, track.MaxLevel);

            string use;
            Uses.TryGetValue(track.Id, out use);
            Text uses = UiKit.Label(card.transform, "Use", use ?? string.Empty, 24, new Color(1f, 1f, 1f, 0.9f), TextAnchor.UpperCenter);
            UiKit.Place(uses.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(CardWidth - 24f, 70f));
            uses.rectTransform.pivot = new Vector2(0.5f, 1f);

            if (track.Kind == UpgradeKind.Suit)
            {
                // 다음 레벨(최대면 지금) 옷을 미리 보여 준다.
                int shown = maxed ? level : level + 1;
                Image suit = UiKit.Image(card.transform, "Suit", Art.Get(MissionWorldView.SuitSprite(shown)), Color.white);
                UiKit.Place(suit.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(70f, 86f));
                suit.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                suit.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                suit.preserveAspect = true;
            }

            Text effect = UiKit.OutlinedLabel(card.transform, "Effect", EffectText(track, level, maxed), 24, EffectColor, TextAnchor.MiddleCenter);
            UiKit.Place(effect.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(CardWidth - 16f, 96f));
            effect.rectTransform.pivot = new Vector2(0.5f, 0f);
            effect.lineSpacing = 1.05f;

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
        }

        /// <summary>"Lv.2 / 5"와 레벨 칸(채운 칸 = 지금 레벨).</summary>
        private static void BuildLevelRow(Transform card, int level, int maxLevel)
        {
            Text text = UiKit.Label(card, "Level", level == 0 ? "잠김" : "Lv." + level + " / " + maxLevel, 26, Color.white, TextAnchor.MiddleCenter);
            UiKit.Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(CardWidth - 20f, 34f));
            text.rectTransform.pivot = new Vector2(0.5f, 1f);

            const float pip = 30f;
            const float gap = 8f;
            float total = (maxLevel * pip) + ((maxLevel - 1) * gap);
            for (int i = 0; i < maxLevel; i++)
            {
                bool filled = i < level;
                Image box = UiKit.Image(card, "Pip" + i, Art.White, filled ? EffectColor : new Color(1f, 1f, 1f, 0.25f));
                UiKit.Place(box.rectTransform, new Vector2(0.5f, 1f), new Vector2(-total / 2f + (i * (pip + gap)) + (pip / 2f), -112f), new Vector2(pip, 14f));
                box.rectTransform.pivot = new Vector2(0.5f, 1f);
            }
        }

        /// <summary>지금 → 다음 레벨에서 무엇이 좋아지는지. 최대면 지금 수치만.</summary>
        private static string EffectText(UpgradeTrack track, int level, bool maxed)
        {
            int next = maxed ? level : level + 1;

            switch (track.Kind)
            {
                case UpgradeKind.Suit:
                    return GearStats.SuitName(next) + "\n불 피해 " + Change(DamageCut(level), DamageCut(next), maxed);

                case UpgradeKind.Boots:
                    return "속도 " + Change(Speed(level), Speed(next), maxed) + "\n칸/초";

                default:
                    return EquipmentEffect(EquipmentCatalog.ById(track.Id), level, next, maxed);
            }
        }

        private static string EquipmentEffect(EquipmentDef def, int level, int next, bool maxed)
        {
            if (level == 0) return "해금하면\n현장에 들고 간다";

            EquipmentDef now = def.AtLevel(level);
            EquipmentDef then = def.AtLevel(next);
            string power = "위력 " + Change(Percent(now.Agent.Power / def.Agent.Power), Percent(then.Agent.Power / def.Agent.Power), maxed);

            string second;
            if (def.Resource == ResourceKind.Charges) second = "횟수 " + Change(now.MaxCharges.ToString(), then.MaxCharges.ToString(), maxed);
            else if (def.Pattern == AimPattern.Line) second = "사거리 " + Change(now.Range.ToString(), then.Range.ToString(), maxed) + "칸";
            else second = "연사 " + Change(now.CooldownSeconds.ToString("0.0"), then.CooldownSeconds.ToString("0.0"), maxed) + "초";

            return power + "\n" + second;
        }

        /// <summary>"지금 → 다음". 최대 레벨이면 바뀔 게 없으니 지금 값만.</summary>
        private static string Change(string now, string next, bool maxed)
        {
            return maxed ? now : now + " → " + next;
        }

        private static string Percent(float ratio)
        {
            return Mathf.RoundToInt(ratio * 100f) + "%";
        }

        private static string DamageCut(int suitLevel)
        {
            int cut = Mathf.RoundToInt((1f - GearStats.DamageMultiplier(suitLevel)) * 100f);
            return cut == 0 ? "0%" : "−" + cut + "%";
        }

        private static string Speed(int bootsLevel)
        {
            return (GameConfig.PlayerSpeed * GearStats.SpeedMultiplier(bootsLevel)).ToString("0.0");
        }
    }
}
