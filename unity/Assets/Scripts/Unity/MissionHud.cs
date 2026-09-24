using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
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

        /// <summary>장비가 잡는 화재 등급 색 띠. 현장 불꽃과 같은 색이라 눈으로 맞출 수 있다.</summary>
        private readonly Image[] _slotStripes = new Image[PlayerState.SlotCount];

        private readonly Image _rescueBack;
        private readonly Text _rescueLabel;
        private readonly Image _doorBack;
        private readonly Text _doorLabel;

        public readonly VirtualJoystick Joystick;
        public readonly HoldButton FireButton;
        public readonly HoldButton RescueButton;

        /// <summary>문 버튼. 곁에 문이 있을 때만 켜진다.</summary>
        public readonly HoldButton DoorButton;

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

            // ---- 발사 버튼 위: 구조 ----
            // 시민 옆에 섰을 때만 켜진다. 업고 있으면 "출구로!"로 바뀌어 다음에 할 일을 알린다.
            _rescueBack = UiKit.Image(_root, "RescueButton", Art.Get("UI/button_green"), Color.white);
            UiKit.Place(_rescueBack.rectTransform, new Vector2(1f, 0f), new Vector2(-70f, 330f), new Vector2(220f, 112f));
            _rescueBack.raycastTarget = true;
            _rescueLabel = UiKit.OutlinedLabel(_rescueBack.transform, "Label", "구조", 38, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(_rescueLabel.rectTransform);
            _rescueLabel.rectTransform.offsetMin = new Vector2(0f, 8f);

            RescueButton = _rescueBack.gameObject.AddComponent<HoldButton>();
            RescueButton.OnHold = held => _flow.SetRescue(held);

            // 구조 버튼 바로 위. 둘 다 "지금 곁에 있는 것"에 대한 행동이라 붙여 둔다.
            _doorBack = UiKit.Image(_root, "DoorButton", Art.Get("UI/button_grey"), Color.white);
            UiKit.Place(_doorBack.rectTransform, new Vector2(1f, 0f), new Vector2(-70f, 458f), new Vector2(220f, 112f));
            _doorBack.raycastTarget = true;
            _doorLabel = UiKit.OutlinedLabel(_doorBack.transform, "Label", "문", 38, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(_doorLabel.rectTransform);
            _doorLabel.rectTransform.offsetMin = new Vector2(0f, 8f);

            DoorButton = _doorBack.gameObject.AddComponent<HoldButton>();
            DoorButton.OnHold = held => _flow.SetInteract(held);

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

                // 버튼 위쪽 띠. 이 장비가 잡는 불의 색이다.
                _slotStripes[slot] = UiKit.Image(button.transform, "Stripe", Art.White, Color.clear);
                UiKit.Place(_slotStripes[slot].rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(178f, 12f));
                _slotStripes[slot].rectTransform.pivot = new Vector2(0.5f, 1f);
                _slotStripes[slot].raycastTarget = false;

                _slotLabels[slot] = UiKit.OutlinedLabel(button.transform, "Label", string.Empty, 26, Color.white, TextAnchor.MiddleCenter);
                UiKit.Stretch(_slotLabels[slot].rectTransform);
                _slotLabels[slot].rectTransform.offsetMin = new Vector2(6f, 6f);
                _slotLabels[slot].rectTransform.offsetMax = new Vector2(-6f, -16f);
                _slotLabels[slot].lineSpacing = 0.85f;
                _slotLabels[slot].raycastTarget = false;
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

            // 아직 기다리는 사람이 있으면 빨갛게. 한 줄짜리 띠라 글자를 더 넣는 대신 색으로 알린다.
            int waiting = runner.PendingCivilianCount;
            _rescued.text = "구조 " + runner.RescuedCount + "/" + runner.Civilians.Count;
            _rescued.color = waiting > 0 ? new Color(0.82f, 0.23f, 0.16f) : new Color(0.15f, 0.55f, 0.25f);

            RefreshRescueButton(runner);
            RefreshDoorButton(runner);

            for (int slot = 0; slot < PlayerState.SlotCount; slot++)
            {
                RefreshSlot(runner, slot);
            }
        }

        /// <summary>
        /// 구조 버튼의 세 상태. 시민을 업고 있으면 "출구로!", 손이 닿으면 밝은 "구조!",
        /// 그 밖에는 흐리게 남겨 둔다 — 버튼이 늘 밝으면 언제 눌러야 하는지 알 수 없다.
        /// </summary>
        /// <summary>
        /// 문 버튼. 곁에 문이 없으면 흐리게 죽인다 —
        /// 켜 놓으면 눌러 보고 나서야 아무 일도 안 일어난다는 걸 알게 된다.
        /// 지금 닫혀 있는지 열려 있는지를 글자로 미리 알려 준다.
        /// </summary>
        private void RefreshDoorButton(StageRunner runner)
        {
            GridPoint? at = runner.DoorAtHand;
            if (at == null)
            {
                _doorBack.sprite = Art.Get("UI/button_grey");
                _doorBack.color = new Color(1f, 1f, 1f, 0.28f);
                _doorLabel.text = "문";
                _doorLabel.color = new Color(1f, 1f, 1f, 0.45f);
                return;
            }

            ref Cell door = ref runner.Grid[at.Value.X, at.Value.Y];

            if (door.State == CellState.Burning)
            {
                _doorBack.sprite = Art.Get("UI/button_grey");
                _doorBack.color = new Color(1f, 0.6f, 0.5f, 0.6f);
                _doorLabel.text = "불붙음";
                _doorLabel.color = new Color(1f, 0.8f, 0.75f);
                return;
            }

            _doorBack.sprite = Art.Get("UI/button_yellow");
            _doorBack.color = Color.white;
            // 버튼 폭이 좁아 세 글자면 세로로 접힌다. 두 글자로 줄인다.
            _doorLabel.text = door.Shut ? "열기" : "닫기";
            _doorLabel.color = Color.white;
        }

        private void RefreshRescueButton(StageRunner runner)
        {
            if (runner.Player.CarryingCivilian)
            {
                _rescueBack.sprite = Art.Get("UI/button_yellow");
                _rescueBack.color = Color.white;
                _rescueLabel.text = "출구로!";
                return;
            }

            bool ready = runner.RescueTarget != null;
            _rescueBack.sprite = Art.Get(ready ? "UI/button_green" : "UI/button_grey");
            _rescueBack.color = ready ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            _rescueLabel.text = ready ? "구조!" : "구조";
            _rescueLabel.color = ready ? Color.white : new Color(1f, 1f, 1f, 0.5f);
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
                _slotStripes[slot].color = Color.clear;
                return;
            }

            bool active = slot == _flow.ActiveSlot;
            image.sprite = Art.Get(active ? "UI/button_yellow" : "UI/button_blue");
            image.color = Color.white;
            label.color = Color.white;

            bool empty = def.Resource == ResourceKind.Charges && player.Charges[slot] <= 0;
            string ammo = def.Resource == ResourceKind.Charges ? player.Charges[slot] + "회" : "무제한";
            int level = _flow.Save.LevelOf(def.Id);

            // 이 장비가 잡는 불을 글자와 띠 색으로 같이 알린다. 띠 색은 현장 불꽃 색과 같다.
            FireClass target = AgentAdvice.BestClassFor(def.Agent.Type);
            _slotStripes[slot].color = FireLook.Of(target);

            label.text = def.Name
                         + "\n<size=21>Lv." + level + " · " + (empty ? "다 씀" : ammo) + "</size>"
                         + "\n<size=21>" + AgentAdvice.ClassName(target) + " 불</size>";

            // 다 쓴 장비는 회색으로 바꿔, 눌러도 안 나가는 이유를 보여 준다.
            if (empty)
            {
                image.sprite = Art.Get("UI/button_grey");
                label.color = new Color(1f, 0.85f, 0.85f);
            }
        }
    }
}
