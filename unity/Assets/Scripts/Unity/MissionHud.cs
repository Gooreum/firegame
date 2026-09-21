using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace FireGame.UnityLayer
{
    /// <summary>
    /// 현장 플레이 화면의 HUD. 위에는 상태(체력·시간·남은 불·구조), 아래에는 조작(조이스틱·장비·발사).
    ///
    /// 가로 모바일 화면 기준으로 엄지가 닿는 아래쪽 양 끝에 조작을, 가운데 위쪽은 비워 현장이 가리지 않게 했다.
    /// </summary>
    public sealed class MissionHud
    {
        private readonly GameFlow _flow;
        private readonly RectTransform _root;

        private readonly Text _title;
        private readonly Image _hpFill;
        private readonly Text _timer;
        private readonly Text _fires;
        private readonly Text _rescued;

        private readonly Image[] _slotImages = new Image[PlayerState.SlotCount];
        private readonly Text[] _slotLabels = new Text[PlayerState.SlotCount];

        public readonly VirtualJoystick Joystick;
        public readonly HoldButton FireButton;

        public MissionHud(Canvas canvas, GameFlow flow)
        {
            _flow = flow;
            _root = UiKit.Stretch(UiKit.Node(canvas.transform, "MissionHud"));

            // ---- 위: 현장 이름 ----
            Image titleBack = UiKit.Image(_root, "TitleBack", Art.Get("UI/panel_grey"), new Color(1f, 1f, 1f, 0.9f));
            UiKit.Place(titleBack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(620f, 76f));
            _title = UiKit.Label(titleBack.transform, "Title", string.Empty, 34, UiKit.Ink, TextAnchor.MiddleCenter);
            UiKit.Stretch(_title.rectTransform);
            _title.rectTransform.offsetMin = new Vector2(12f, 8f);

            // ---- 왼쪽 위: 체력 ----
            Image statusBack = UiKit.Image(_root, "StatusBack", Art.Get("UI/panel_grey"), new Color(1f, 1f, 1f, 0.9f));
            UiKit.Place(statusBack.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -18f), new Vector2(460f, 76f));

            Text hpLabel = UiKit.Label(statusBack.transform, "HpLabel", "체력", 30, UiKit.Ink, TextAnchor.MiddleLeft);
            UiKit.Place(hpLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(22f, 4f), new Vector2(80f, 50f));

            // UI 팩의 막대 그림은 슬라이더라 끝에 손잡이가 붙어 있다. 어두운 패널로 막대 바탕을 만든다.
            Image hpBack = UiKit.Image(statusBack.transform, "HpBack", Art.Get("UI/panel_grey"), new Color(0.35f, 0.35f, 0.4f));
            UiKit.Place(hpBack.rectTransform, new Vector2(0f, 0.5f), new Vector2(104f, 4f), new Vector2(330f, 30f));

            _hpFill = UiKit.Image(hpBack.transform, "HpFill", Art.White, new Color(0.3f, 0.8f, 0.35f));
            UiKit.Stretch(_hpFill.rectTransform, 5f);
            _hpFill.type = Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;

            // ---- 오른쪽 위: 시간·남은 불·구조 ----
            Image infoBack = UiKit.Image(_root, "InfoBack", Art.Get("UI/panel_grey"), new Color(1f, 1f, 1f, 0.9f));
            UiKit.Place(infoBack.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -18f), new Vector2(520f, 76f));

            _timer = UiKit.Label(infoBack.transform, "Timer", "0:00", 38, UiKit.Ink, TextAnchor.MiddleLeft);
            UiKit.Place(_timer.rectTransform, new Vector2(0f, 0.5f), new Vector2(24f, 4f), new Vector2(130f, 56f));

            _fires = UiKit.Label(infoBack.transform, "Fires", string.Empty, 30, new Color(0.85f, 0.3f, 0.1f), TextAnchor.MiddleLeft);
            UiKit.Place(_fires.rectTransform, new Vector2(0f, 0.5f), new Vector2(170f, 4f), new Vector2(160f, 56f));

            _rescued = UiKit.Label(infoBack.transform, "Rescued", string.Empty, 30, new Color(0.15f, 0.55f, 0.25f), TextAnchor.MiddleLeft);
            UiKit.Place(_rescued.rectTransform, new Vector2(0f, 0.5f), new Vector2(340f, 4f), new Vector2(170f, 56f));

            // ---- 왼쪽 아래: 조이스틱 ----
            Image stickBase = UiKit.Image(_root, "JoystickBase", Art.Get("UI/button_blue_round"), new Color(1f, 1f, 1f, 0.35f));
            UiKit.Place(stickBase.rectTransform, new Vector2(0f, 0f), new Vector2(70f, 60f), new Vector2(260f, 260f));
            stickBase.raycastTarget = true;

            Image knob = UiKit.Image(stickBase.transform, "Knob", Art.Get("UI/button_blue_round"), Color.white);
            knob.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            knob.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            knob.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            knob.rectTransform.sizeDelta = new Vector2(120f, 120f);

            Joystick = stickBase.gameObject.AddComponent<VirtualJoystick>();
            Joystick.Knob = knob.rectTransform;
            Joystick.Radius = 110f;

            // 화면은 위가 +y, 격자는 아래가 +y라 세로를 뒤집어 넘긴다.
            Joystick.OnMove = direction => _flow.SetMove(direction.x, -direction.y);

            // ---- 오른쪽 아래: 발사 ----
            Image fire = UiKit.Image(_root, "FireButton", Art.Get("UI/button_red_round"), Color.white);
            UiKit.Place(fire.rectTransform, new Vector2(1f, 0f), new Vector2(-60f, 60f), new Vector2(240f, 240f));
            fire.raycastTarget = true;
            Text fireLabel = UiKit.OutlinedLabel(fire.transform, "Label", "발사", 52, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(fireLabel.rectTransform);

            FireButton = fire.gameObject.AddComponent<HoldButton>();
            FireButton.OnHold = held => _flow.SetFire(held);

            // ---- 발사 버튼 왼쪽: 장비 ----
            for (int slot = 0; slot < PlayerState.SlotCount; slot++)
            {
                int captured = slot;
                Button button = UiKit.Button(_root, "Slot" + slot, Art.Get("UI/button_blue"), string.Empty, 0, () => _flow.SelectSlot(captured));
                // 왼쪽부터 1·2·3·4번 — 키보드 숫자키와 같은 순서.
                // 4칸이 조이스틱(왼쪽 330까지)과 겹치지 않게 폭을 줄였다.
                int fromRight = PlayerState.SlotCount - 1 - slot;
                UiKit.Place((RectTransform)button.transform, new Vector2(1f, 0f), new Vector2(-320f - (fromRight * 226f), 70f), new Vector2(214f, 118f));

                _slotImages[slot] = (Image)button.targetGraphic;
                _slotLabels[slot] = UiKit.OutlinedLabel(button.transform, "Label", string.Empty, 28, Color.white, TextAnchor.MiddleCenter);
                UiKit.Stretch(_slotLabels[slot].rectTransform);
                _slotLabels[slot].rectTransform.offsetMin = new Vector2(6f, 10f);
                _slotLabels[slot].lineSpacing = 0.9f;
            }

            Refresh();
        }

        public void Destroy()
        {
            UiKit.Discard(_root.gameObject);
        }

        public void Refresh()
        {
            StageRunner runner = _flow.Runner;
            if (runner == null) return;

            MissionDef mission = _flow.CurrentMission;
            _title.text = mission != null ? mission.Location + " · " + mission.Title : runner.Def.Name;

            float hp = Mathf.Clamp01(runner.Player.Hp / GameConfig.PlayerMaxHp);
            _hpFill.fillAmount = hp;
            _hpFill.color = hp > 0.5f ? new Color(0.3f, 0.8f, 0.35f) : hp > 0.25f ? new Color(0.95f, 0.75f, 0.15f) : new Color(0.9f, 0.25f, 0.2f);

            int seconds = Mathf.CeilToInt(runner.TimeLeft);
            _timer.text = (seconds / 60) + ":" + (seconds % 60).ToString("00");
            _timer.color = seconds <= 15 ? new Color(0.9f, 0.2f, 0.15f) : UiKit.Ink;

            _fires.text = "남은 불 " + runner.Grid.CountBurning();
            _rescued.text = "구조 " + runner.RescuedCount + "/" + runner.Civilians.Count;

            for (int slot = 0; slot < PlayerState.SlotCount; slot++)
            {
                RefreshSlot(runner, slot);
            }
        }

        private void RefreshSlot(StageRunner runner, int slot)
        {
            PlayerState player = runner.Player;
            EquipmentDef def = runner.SlotEquipment(slot);
            Image image = _slotImages[slot];
            Text label = _slotLabels[slot];

            if (def == null)
            {
                image.sprite = Art.Get("UI/button_grey");
                image.color = new Color(1f, 1f, 1f, 0.45f);
                label.text = "비어 있음";
                label.color = new Color(1f, 1f, 1f, 0.6f);
                return;
            }

            bool active = slot == _flow.ActiveSlot;
            image.sprite = Art.Get(active ? "UI/button_yellow" : "UI/button_blue");
            image.color = Color.white;
            label.color = Color.white;

            bool empty = def.Resource == ResourceKind.Charges && player.Charges[slot] <= 0;
            string ammo = def.Resource == ResourceKind.Charges ? player.Charges[slot] + "회" : "무제한";
            int level = _flow.Save.LevelOf(def.Id);
            label.text = def.Name + "\n<size=22>Lv." + level + " · " + (empty ? "다 씀" : ammo) + "</size>";

            // 다 쓴 장비는 회색으로 바꿔, 눌러도 안 나가는 이유를 보여 준다.
            if (empty)
            {
                image.sprite = Art.Get("UI/button_grey");
                label.color = new Color(1f, 0.85f, 0.85f);
            }
        }
    }
}
