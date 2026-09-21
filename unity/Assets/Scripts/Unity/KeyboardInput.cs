using FireGame.Core.Game;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FireGame.UnityLayer
{
    /// <summary>
    /// 에디터·데스크톱용 키보드 입력. 방향키/WASD 이동, Space 발사, 1·2·3 장비.
    ///
    /// 새 Input System과 구형 Input Manager를 전처리기로 모두 지원한다.
    /// 프로젝트 설정에 따라 한쪽 API는 예외를 던지기 때문이다.
    /// </summary>
    public static class KeyboardInput
    {
        public static void Apply(GameFlow flow)
        {
            float x = 0f;
            float y = 0f;
            bool fire = false;
            int slot = -1;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) x -= 1f;
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) x += 1f;
            if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) y -= 1f;
            if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) y += 1f;
            fire = keyboard.spaceKey.isPressed;
            if (keyboard.digit1Key.wasPressedThisFrame) slot = 0;
            if (keyboard.digit2Key.wasPressedThisFrame) slot = 1;
            if (keyboard.digit3Key.wasPressedThisFrame) slot = 2;
            if (keyboard.digit4Key.wasPressedThisFrame) slot = 3;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) x -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) x += 1f;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) y -= 1f;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) y += 1f;
            fire = Input.GetKey(KeyCode.Space);
            if (Input.GetKeyDown(KeyCode.Alpha1)) slot = 0;
            if (Input.GetKeyDown(KeyCode.Alpha2)) slot = 1;
            if (Input.GetKeyDown(KeyCode.Alpha3)) slot = 2;
            if (Input.GetKeyDown(KeyCode.Alpha4)) slot = 3;
#endif

            // 격자는 아래로 갈수록 y가 커진다. 위쪽 키가 -y다.
            flow.SetKeyboard(x, y, fire, slot);
        }
    }
}
