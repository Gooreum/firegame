using System.Collections.Generic;
using FireGame.Core.Game;
using FireGame.Core.Render;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FireGame.UnityLayer
{
    /// <summary>
    /// 터치·마우스·키보드를 읽어 GameFlow에 넘긴다. 판정은 전부 GameFlow가 한다.
    ///
    /// Unity 6 신규 프로젝트는 기본 입력이 새 Input System이라 구형 Input API를 부르면
    /// 예외가 난다. 반대로 패키지가 없는 프로젝트는 구형만 쓸 수 있다.
    /// 그래서 두 경로를 전처리기로 모두 지원한다(둘 다 켜져 있으면 새 쪽을 쓴다).
    ///
    /// 두 API의 눌림/뗌 이벤트 의미가 달라서, 매 프레임 "지금 눌려 있는 손가락 목록"만
    /// 모은 뒤 직전 프레임과 비교해 Down/Move/Up을 직접 만든다.
    /// </summary>
    public sealed class TouchInput
    {
        /// <summary>마우스는 손가락 id와 겹치지 않는 번호를 쓴다.</summary>
        private const int MousePointerId = 1000;

        private readonly Dictionary<int, Vector2> _current = new Dictionary<int, Vector2>();
        private readonly HashSet<int> _previous = new HashSet<int>();
        private readonly List<int> _released = new List<int>();

        public void Poll(GameFlow flow)
        {
            _current.Clear();
            CollectPointers(_current);

            foreach (KeyValuePair<int, Vector2> pointer in _current)
            {
                ScreenLayout.ScreenToFrameBuffer(
                    pointer.Value.x, pointer.Value.y, Screen.width, Screen.height,
                    out float x, out float y);

                if (_previous.Contains(pointer.Key))
                {
                    flow.PointerMove(pointer.Key, x, y);
                }
                else
                {
                    flow.PointerDown(pointer.Key, x, y);
                }
            }

            _released.Clear();
            foreach (int id in _previous)
            {
                if (!_current.ContainsKey(id)) _released.Add(id);
            }

            foreach (int id in _released)
            {
                flow.PointerUp(id);
            }

            _previous.Clear();
            foreach (int id in _current.Keys)
            {
                _previous.Add(id);
            }

            ReadKeyboard(flow);
        }

#if ENABLE_INPUT_SYSTEM
        private static void CollectPointers(Dictionary<int, Vector2> into)
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    if (!touch.press.isPressed) continue;
                    into[touch.touchId.ReadValue()] = touch.position.ReadValue();
                }
            }

            // 터치 중에는 마우스를 무시한다. 일부 기기는 터치를 마우스로도 흉내낸다.
            Mouse mouse = Mouse.current;
            if (into.Count == 0 && mouse != null && mouse.leftButton.isPressed)
            {
                into[MousePointerId] = mouse.position.ReadValue();
            }
        }

        private static void ReadKeyboard(GameFlow flow)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            float x = 0f;
            float y = 0f;
            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) x -= 1f;
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) x += 1f;
            if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) y -= 1f;
            if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) y += 1f;

            int slot = -1;
            if (keyboard.digit1Key.wasPressedThisFrame) slot = 0;
            if (keyboard.digit2Key.wasPressedThisFrame) slot = 1;
            if (keyboard.digit3Key.wasPressedThisFrame) slot = 2;

            flow.SetKeyboard(x, y, keyboard.spaceKey.isPressed, slot);
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        private static void CollectPointers(Dictionary<int, Vector2> into)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) continue;

                into[touch.fingerId] = touch.position;
            }

            if (into.Count == 0 && Input.GetMouseButton(0))
            {
                into[MousePointerId] = Input.mousePosition;
            }
        }

        private static void ReadKeyboard(GameFlow flow)
        {
            float x = 0f;
            float y = 0f;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) x -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) x += 1f;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) y -= 1f;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) y += 1f;

            int slot = -1;
            if (Input.GetKeyDown(KeyCode.Alpha1)) slot = 0;
            if (Input.GetKeyDown(KeyCode.Alpha2)) slot = 1;
            if (Input.GetKeyDown(KeyCode.Alpha3)) slot = 2;

            flow.SetKeyboard(x, y, Input.GetKey(KeyCode.Space), slot);
        }
#else
        private static void CollectPointers(Dictionary<int, Vector2> into)
        {
        }

        private static void ReadKeyboard(GameFlow flow)
        {
        }
#endif
    }
}
