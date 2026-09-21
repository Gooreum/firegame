using System;
using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Render;

namespace FireGame.Core.Game
{
    /// <summary>게임의 화면 단위.</summary>
    public enum GameScreen : byte
    {
        /// <summary>스테이지 선택과 상점 입구.</summary>
        Hub = 0,
        Shop = 1,
        Playing = 2,

        /// <summary>한 판이 끝난 뒤의 정산.</summary>
        Result = 3,
    }

    /// <summary>
    /// 화면 전환과 입력 해석을 맡는다. Unity 쪽은 터치 좌표를 320x200으로 바꿔
    /// 넘기고, <see cref="Render"/>가 채운 버퍼를 텍스처로 올리기만 한다.
    ///
    /// 이 로직을 Unity 스크립트에 두면 에디터 없이는 검증할 방법이 없다.
    /// 여기 두면 "허브에서 탭 → 플레이 → 정산 → 저장" 전체 흐름을 테스트로 돌릴 수 있다.
    /// </summary>
    public sealed class GameFlow
    {
        private readonly HashSet<int> _firePointers = new HashSet<int>();

        private int _joystickPointer = -1;
        private float _joystickAnchorX;
        private float _joystickAnchorY;
        private float _stickX;
        private float _stickY;

        private float _keyboardX;
        private float _keyboardY;
        private bool _keyboardFire;

        private int _activeSlot;
        private float _elapsed;

        public readonly SaveData Save;

        /// <summary>
        /// 진행 상황이 바뀔 때마다 직렬화된 세이브를 넘겨받는다.
        /// Unity에서는 PlayerPrefs에 쓰면 된다. 코어가 저장 방식을 몰라도 되게 콜백으로 뺐다.
        /// </summary>
        public Action<string> SaveWriter;

        public GameFlow(SaveData save)
        {
            Save = save ?? SaveData.NewGame();
        }

        public GameScreen Screen { get; private set; } = GameScreen.Hub;

        /// <summary>플레이 중인 판. 플레이 화면이 아니면 마지막 판이 남아 있을 수 있다.</summary>
        public StageRunner Runner { get; private set; }

        public int ShopCursor { get; private set; } = -1;

        public StageOutcome LastOutcome { get; private set; }

        public PayoutBreakdown LastPayout { get; private set; }

        public string LastStageName { get; private set; }

        public int ActiveSlot
        {
            get { return _activeSlot; }
        }

        /// <summary>화염 애니메이션용 프레임 번호. 초당 6장.</summary>
        public int AnimationFrame
        {
            get { return (int)(_elapsed * 6f); }
        }

        // ------------------------------------------------------------------
        // 입력
        // ------------------------------------------------------------------

        /// <summary>손가락(또는 마우스)이 닿았다. 좌표는 프레임버퍼 기준.</summary>
        public void PointerDown(int pointerId, float x, float y)
        {
            switch (Screen)
            {
                case GameScreen.Hub:
                    HubTap(x, y);
                    break;

                case GameScreen.Shop:
                    ShopTap(x, y);
                    break;

                case GameScreen.Result:
                    Screen = GameScreen.Hub;
                    break;

                case GameScreen.Playing:
                    PlayingDown(pointerId, x, y);
                    break;
            }
        }

        public void PointerMove(int pointerId, float x, float y)
        {
            if (pointerId != _joystickPointer) return;

            float dx = (x - _joystickAnchorX) / ScreenLayout.JoystickRadius;
            float dy = (y - _joystickAnchorY) / ScreenLayout.JoystickRadius;

            // 끝까지 밀었으면 그 이상은 더 빨라지지 않는다.
            float length = (float)Math.Sqrt((dx * dx) + (dy * dy));
            if (length > 1f)
            {
                dx /= length;
                dy /= length;
            }

            _stickX = dx;
            _stickY = dy;
        }

        public void PointerUp(int pointerId)
        {
            if (pointerId == _joystickPointer)
            {
                _joystickPointer = -1;
                _stickX = 0f;
                _stickY = 0f;
            }

            _firePointers.Remove(pointerId);
        }

        /// <summary>
        /// 키보드 상태. 에디터와 데스크톱에서 테스트할 때 쓴다.
        /// <paramref name="slotKey"/>는 이번 프레임에 누른 숫자키(0부터), 없으면 -1.
        /// </summary>
        public void SetKeyboard(float moveX, float moveY, bool fire, int slotKey)
        {
            _keyboardX = moveX;
            _keyboardY = moveY;
            _keyboardFire = fire;

            if (slotKey >= 0) SelectSlot(slotKey);
        }

        private void HubTap(float x, float y)
        {
            if (ScreenLayout.Inside(
                    x, y,
                    ScreenLayout.HubShopButtonX,
                    ScreenLayout.HubShopButtonY,
                    ScreenLayout.HubShopButtonWidth,
                    ScreenLayout.HubShopButtonHeight))
            {
                ShopCursor = -1;
                Screen = GameScreen.Shop;
                return;
            }

            int row = ScreenLayout.RowAt(y, ScreenLayout.HubStageTop, StageCatalog.All.Length);
            if (row < 0) return;

            MissionDef mission = Campaign.Missions[row];
            if (!Save.IsMissionUnlocked(mission)) return;

            StartStage(mission.Stage);
        }

        private void ShopTap(float x, float y)
        {
            if (ScreenLayout.Inside(
                    x, y,
                    ScreenLayout.ShopBackButtonX,
                    ScreenLayout.ShopBackButtonY,
                    ScreenLayout.ShopBackButtonWidth,
                    ScreenLayout.ShopBackButtonHeight))
            {
                Screen = GameScreen.Hub;
                return;
            }

            int row = ScreenLayout.RowAt(y, ScreenLayout.ShopRowTop, EquipmentCatalog.All.Length);
            if (row < 0) return;

            ShopCursor = row;

            if (Shop.Buy(Save, EquipmentCatalog.All[row].Id) == PurchaseResult.Success)
            {
                Persist();
            }
        }

        private void PlayingDown(int pointerId, float x, float y)
        {
            // HUD 줄은 장비 선택.
            if (y >= FrameBuffer.PlayfieldHeight)
            {
                int slot = ScreenLayout.SlotAt(x, PlayerState.SlotCount);
                if (slot >= 0) SelectSlot(slot);
                return;
            }

            // 왼쪽은 조이스틱. 누른 자리가 중심이 되므로 화면 어디를 짚어도 된다.
            if (x < ScreenLayout.PlaySplitX)
            {
                if (_joystickPointer >= 0) return;

                _joystickPointer = pointerId;
                _joystickAnchorX = x;
                _joystickAnchorY = y;
                _stickX = 0f;
                _stickY = 0f;
                return;
            }

            // 오른쪽은 누르고 있는 동안 발사.
            _firePointers.Add(pointerId);
        }

        private void SelectSlot(int slot)
        {
            if (Runner == null) return;
            if (slot < 0 || slot >= PlayerState.SlotCount) return;

            // 빈 슬롯을 고르면 아무것도 못 쏘게 되므로 무시한다.
            if (Runner.Player.Slots[slot] < 0) return;

            _activeSlot = slot;
        }

        // ------------------------------------------------------------------
        // 진행
        // ------------------------------------------------------------------

        public void StartStage(StageDef stage)
        {
            if (stage == null) throw new ArgumentNullException(nameof(stage));

            Runner = new StageRunner(stage, Save.Unlocked);
            _activeSlot = 0;
            ResetPointers();
            Screen = GameScreen.Playing;
        }

        public void Update(float dt)
        {
            if (dt <= 0f) return;

            _elapsed += dt;

            if (Screen != GameScreen.Playing || Runner == null) return;

            var input = new StageInput
            {
                MoveX = Clamp(_stickX + _keyboardX),
                MoveY = Clamp(_stickY + _keyboardY),
                Fire = _firePointers.Count > 0 || _keyboardFire,
                Slot = _activeSlot,
            };

            Runner.Update(dt, input);

            if (Runner.IsOver) Settle();
        }

        private void Settle()
        {
            StageResult result = Runner.BuildResult();
            PayoutBreakdown payout = Economy.Breakdown(result);

            Save.Money += payout.Total;
            Save.RecordResult(Runner.Def.Id, StarRating.For(result));

            LastOutcome = Runner.Outcome;
            LastPayout = payout;
            LastStageName = Runner.Def.Name;

            ResetPointers();
            Persist();
            Screen = GameScreen.Result;
        }

        private void ResetPointers()
        {
            _joystickPointer = -1;
            _stickX = 0f;
            _stickY = 0f;
            _firePointers.Clear();
        }

        private void Persist()
        {
            if (SaveWriter != null) SaveWriter(Save.Serialize());
        }

        private static float Clamp(float value)
        {
            if (value > 1f) return 1f;
            if (value < -1f) return -1f;
            return value;
        }

        // ------------------------------------------------------------------
        // 그리기
        // ------------------------------------------------------------------

        public void Render(FrameBuffer buffer)
        {
            if (buffer == null) return;

            switch (Screen)
            {
                case GameScreen.Hub:
                    HudRenderer.DrawHub(buffer, Save);
                    break;

                case GameScreen.Shop:
                    HudRenderer.DrawShop(buffer, Save, ShopCursor);
                    break;

                case GameScreen.Playing:
                    SceneRenderer.Render(buffer, Runner, AnimationFrame);
                    HudRenderer.DrawHud(buffer, Runner, Save.Money);
                    break;

                case GameScreen.Result:
                    HudRenderer.DrawResult(buffer, LastStageName, LastOutcome, LastPayout);
                    break;
            }
        }
    }
}
