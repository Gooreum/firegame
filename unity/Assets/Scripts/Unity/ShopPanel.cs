using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using UnityEngine;
using UnityEngine.UI;

namespace FireGame.UnityLayer
{
    /// <summary>
    /// 소방서 상점. 장비마다 어떤 불에 쓰는지를 적어, "비싼 것"이 아니라 "맞는 것"을 사게 한다.
    /// </summary>
    public sealed class ShopPanel
    {
        /// <summary>장비별 쓰임새. 코어는 규칙만 알고 설명은 화면 쪽 몫이다.</summary>
        private static readonly Dictionary<int, string> Uses = new Dictionary<int, string>
        {
            { EquipmentId.Bucket, "물 · 나무 불\n가까운 한 칸" },
            { EquipmentId.Extinguisher, "CO2 · 전기 불\n앞쪽 세 칸" },
            { EquipmentId.Hose, "물 · 큰 나무 불\n다섯 칸, 소화전 근처만" },
            { EquipmentId.FoamExtinguisher, "폼 · 기름 불\n앞쪽 세 칸" },
        };

        private readonly RectTransform _root;

        public ShopPanel(Canvas canvas, GameFlow flow)
        {
            SaveData save = flow.Save;
            _root = UiKit.Stretch(UiKit.Node(canvas.transform, "ShopPanel"));

            Image dim = UiKit.Image(_root, "Dim", Art.White, UiKit.Shade);
            UiKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            Image panel = UiKit.Image(_root, "Panel", Art.Get("UI/panel_grey"), Color.white);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560f, 780f));
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

            EquipmentDef[] all = EquipmentCatalog.All;
            for (int i = 0; i < all.Length; i++)
            {
                BuildCard(panel.transform, flow, all[i], i, all.Length);
            }

            UnityEngine.UI.Button close = UiKit.Button(panel.transform, "Close", Art.Get("UI/button_grey"), "닫기", 38, flow.CloseShop);
            UiKit.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(300f, 104f));
            ((RectTransform)close.transform).pivot = new Vector2(0.5f, 0f);
        }

        public void Destroy()
        {
            UiKit.Discard(_root.gameObject);
        }

        private static void BuildCard(Transform panel, GameFlow flow, EquipmentDef def, int index, int count)
        {
            SaveData save = flow.Save;
            bool owned = save.Owns(def.Id);
            bool affordable = !owned && save.Money >= def.Price;

            const float cardWidth = 340f;
            const float gap = 26f;
            float totalWidth = (count * cardWidth) + ((count - 1) * gap);
            float x = -totalWidth / 2f + (index * (cardWidth + gap)) + (cardWidth / 2f);

            Image card = UiKit.Image(panel, "Card" + def.Id, Art.Get("UI/panel_blue"), owned ? new Color(0.8f, 0.85f, 0.9f) : Color.white);
            UiKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, 10f), new Vector2(cardWidth, 420f));
            card.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            Text name = UiKit.OutlinedLabel(card.transform, "Name", def.Name, 40, Color.white, TextAnchor.UpperCenter);
            UiKit.Stretch(name.rectTransform);
            name.rectTransform.offsetMax = new Vector2(0f, -28f);

            string use;
            Uses.TryGetValue(def.Id, out use);
            Text uses = UiKit.Label(card.transform, "Use", use ?? string.Empty, 28, Color.white, TextAnchor.UpperCenter);
            UiKit.Stretch(uses.rectTransform, 16f);
            uses.rectTransform.offsetMax = new Vector2(-16f, -100f);
            uses.lineSpacing = 1.1f;

            string ammo = def.Resource == ResourceKind.Charges ? def.MaxCharges + "회 사용" : "무제한";
            Text ammoText = UiKit.Label(card.transform, "Ammo", ammo, 26, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter);
            UiKit.Place(ammoText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(300f, 40f));
            ammoText.rectTransform.pivot = new Vector2(0.5f, 0f);

            // 상태별 버튼: 보유 중(회색) / 구매(초록) / 잔액 부족(빨강)
            string sprite = owned ? "UI/button_grey" : affordable ? "UI/button_green" : "UI/button_red";
            string label = owned ? "보유 중" : Format.Money(def.Price);
            int id = def.Id;
            UnityEngine.UI.Button buy = UiKit.Button(card.transform, "Buy", Art.Get(sprite), label, 36, () => flow.Buy(id));
            UiKit.Place((RectTransform)buy.transform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(280f, 100f));
            ((RectTransform)buy.transform).pivot = new Vector2(0.5f, 0f);
            buy.interactable = affordable;

            // 비활성 버튼에 Unity가 기본으로 회색을 덧칠하면 빨강이 보라색이 된다.
            // 상태는 그림 색(회색/초록/빨강)으로 이미 구분되므로 덧칠을 끈다.
            ColorBlock colors = buy.colors;
            colors.disabledColor = Color.white;
            buy.colors = colors;

            if (!owned && !affordable)
            {
                Text need = UiKit.Label(card.transform, "Need", "잔액 부족", 24, new Color(1f, 0.85f, 0.85f), TextAnchor.MiddleCenter);
                UiKit.Place(need.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 128f), new Vector2(300f, 30f));
                need.rectTransform.pivot = new Vector2(0.5f, 0f);
                ammoText.rectTransform.anchoredPosition = new Vector2(0f, 160f);
            }
        }
    }
}
