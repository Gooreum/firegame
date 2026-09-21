using System;

namespace FireGame.Core.Game
{
    /// <summary>게임의 화면 단위.</summary>
    public enum GameScreen : byte
    {
        /// <summary>출동 지도. 현장 선택과 상점 입구.</summary>
        Map = 0,

        /// <summary>출동 전 서장의 브리핑.</summary>
        Briefing = 1,

        Playing = 2,

        /// <summary>현장이 끝난 뒤의 별점·정산.</summary>
        Result = 3,
    }

    /// <summary>
    /// 화면 전환과 입력을 맡는다. Unity 쪽 버튼·조이스틱은 여기 있는 명령만 호출한다.
    ///
    /// 흐름을 Unity 스크립트에 두면 에디터 없이는 검증할 방법이 없다.
    /// 여기 두면 "지도 → 브리핑 → 플레이 → 결과 → 다음 현장 해금" 전체를 테스트로 돌릴 수 있다.
    /// </summary>
    public sealed class GameFlow
    {
        private float _stickX;
        private float _stickY;
        private bool _fireHeld;

        private float _keyboardX;
        private float _keyboardY;
        private bool _keyboardFire;

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

        public GameScreen Screen { get; private set; } = GameScreen.Map;

        /// <summary>지도 위에 상점 패널이 열려 있는지.</summary>
        public bool ShopOpen { get; private set; }

        /// <summary>브리핑·플레이·결과 중인 현장. 지도에서는 마지막으로 고른 현장이 남아 있을 수 있다.</summary>
        public MissionDef CurrentMission { get; private set; }

        public StageRunner Runner { get; private set; }

        public StageOutcome LastOutcome { get; private set; }

        public PayoutBreakdown LastPayout { get; private set; }

        public int LastStars { get; private set; }

        /// <summary>이번 결과로 최고 별점이 올랐는지. 결과 화면 연출용.</summary>
        public bool LastWasNewBest { get; private set; }

        public int ActiveSlot { get; private set; }

        /// <summary>누적 시간. 화면 애니메이션에 쓴다.</summary>
        public float Elapsed { get; private set; }

        // ------------------------------------------------------------------
        // 지도 · 브리핑 · 결과
        // ------------------------------------------------------------------

        /// <summary>지도에서 현장을 고른다. 잠겨 있거나 지도 화면이 아니면 false.</summary>
        public bool SelectMission(int missionId)
        {
            if (Screen != GameScreen.Map || ShopOpen) return false;

            MissionDef mission = Campaign.ById(missionId);
            if (mission == null || !Save.IsMissionUnlocked(mission)) return false;

            CurrentMission = mission;
            Screen = GameScreen.Briefing;
            return true;
        }

        /// <summary>브리핑을 마치고 출동한다.</summary>
        public void BeginMission()
        {
            if (Screen != GameScreen.Briefing || CurrentMission == null) return;

            StartRun();
        }

        /// <summary>결과 화면에서 같은 현장을 바로 다시 한다. 브리핑은 건너뛴다.</summary>
        public void RetryMission()
        {
            if (Screen != GameScreen.Result || CurrentMission == null) return;

            StartRun();
        }

        public void BackToMap()
        {
            if (Screen != GameScreen.Briefing && Screen != GameScreen.Result) return;

            Screen = GameScreen.Map;
        }

        private void StartRun()
        {
            Runner = new StageRunner(CurrentMission.Stage, Save.Unlocked);
            ActiveSlot = 0;
            ResetInput();
            Screen = GameScreen.Playing;
        }

        // ------------------------------------------------------------------
        // 상점
        // ------------------------------------------------------------------

        public void OpenShop()
        {
            if (Screen == GameScreen.Map) ShopOpen = true;
        }

        public void CloseShop()
        {
            ShopOpen = false;
        }

        public PurchaseResult Buy(int equipmentId)
        {
            // 상점이 닫혀 있을 때 사지는 걸 막는다. 버튼이 늦게 눌리는 경우를 대비한다.
            if (!ShopOpen || Screen != GameScreen.Map) return PurchaseResult.UnknownEquipment;

            PurchaseResult result = Shop.Buy(Save, equipmentId);
            if (result == PurchaseResult.Success) Persist();
            return result;
        }

        // ------------------------------------------------------------------
        // 플레이 입력
        // ------------------------------------------------------------------

        /// <summary>가상 조이스틱. 길이 1을 넘는 입력은 단위 원으로 자른다.</summary>
        public void SetMove(float x, float y)
        {
            ClampToUnit(ref x, ref y);
            _stickX = x;
            _stickY = y;
        }

        public void SetFire(bool held)
        {
            _fireHeld = held;
        }

        public void SelectSlot(int slot)
        {
            if (Runner == null) return;
            if (slot < 0 || slot >= PlayerState.SlotCount) return;

            // 빈 슬롯을 고르면 아무것도 못 쏘게 되므로 무시한다.
            if (Runner.Player.Slots[slot] < 0) return;

            ActiveSlot = slot;
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

        // ------------------------------------------------------------------
        // 진행
        // ------------------------------------------------------------------

        public void Update(float dt)
        {
            if (dt <= 0f) return;

            Elapsed += dt;

            if (Screen != GameScreen.Playing || Runner == null) return;

            float moveX = _stickX + _keyboardX;
            float moveY = _stickY + _keyboardY;
            ClampToUnit(ref moveX, ref moveY);

            var input = new StageInput
            {
                MoveX = moveX,
                MoveY = moveY,
                Fire = _fireHeld || _keyboardFire,
                Slot = ActiveSlot,
            };

            Runner.Update(dt, input);

            if (Runner.IsOver) Settle();
        }

        private void Settle()
        {
            StageResult result = Runner.BuildResult();
            PayoutBreakdown payout = Economy.Breakdown(result);
            int stars = StarRating.For(result);

            LastWasNewBest = stars > Save.StarsFor(CurrentMission.Id);

            Save.Money += payout.Total;
            Save.RecordResult(CurrentMission.Id, stars);

            LastOutcome = Runner.Outcome;
            LastPayout = payout;
            LastStars = stars;

            ResetInput();
            Persist();
            Screen = GameScreen.Result;
        }

        private void ResetInput()
        {
            _stickX = 0f;
            _stickY = 0f;
            _fireHeld = false;
        }

        private void Persist()
        {
            if (SaveWriter != null) SaveWriter(Save.Serialize());
        }

        private static void ClampToUnit(ref float x, ref float y)
        {
            float length = (float)Math.Sqrt((x * x) + (y * y));
            if (length <= 1f) return;

            x /= length;
            y /= length;
        }
    }
}
