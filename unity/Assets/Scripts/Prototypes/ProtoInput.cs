using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FireGame.Prototypes
{
    /// <summary>
    /// 시험판 입력 한 프레임치. 새 Input System과 구형 Input Manager 둘 다 받는다
    /// (프로젝트 설정에 따라 한쪽 API는 예외를 던진다 — 본 게임 KeyboardInput과 같은 이유).
    /// </summary>
    public struct ProtoInput
    {
        /// <summary>WASD/방향키. 위가 +y(화면 기준).</summary>
        public Vector2 Move;

        /// <summary>마우스 화면 좌표(픽셀).</summary>
        public Vector2 Mouse;

        public bool MouseHeld;
        public bool MouseClicked;
        public bool RightClicked;

        public bool Restart;
        public bool Switch;
        public bool EndTurn;
        public bool Undo;
        public bool Key1;
        public bool Key2;
        public bool Key3;
        public bool Cancel;

        /// <summary>G: 시험판 C 풀장비로 다시 시작(누를 때마다 풀장비/일반 전환).</summary>
        public bool MaxGear;

        /// <summary>폰: 이번 프레임 화면에 닿은 손가락들. 없으면 null이거나 비어 있다.</summary>
        public List<Finger> Fingers;

        private static readonly List<Finger> FingerBuffer = new List<Finger>();

        public static ProtoInput Read()
        {
            var input = new ProtoInput();
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.Move.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.Move.x += 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.Move.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.Move.y -= 1f;
                input.Restart = keyboard.rKey.wasPressedThisFrame;
                input.Switch = keyboard.tabKey.wasPressedThisFrame;
                input.EndTurn = keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame;
                input.Undo = keyboard.zKey.wasPressedThisFrame;
                input.Key1 = keyboard.digit1Key.wasPressedThisFrame;
                input.Key2 = keyboard.digit2Key.wasPressedThisFrame;
                input.Key3 = keyboard.digit3Key.wasPressedThisFrame;
                input.Cancel = keyboard.escapeKey.wasPressedThisFrame;
                input.MaxGear = keyboard.gKey.wasPressedThisFrame;
            }
            if (mouse != null)
            {
                input.Mouse = mouse.position.ReadValue();
                input.MouseHeld = mouse.leftButton.isPressed;
                input.MouseClicked = mouse.leftButton.wasPressedThisFrame;
                input.RightClicked = mouse.rightButton.wasPressedThisFrame;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input.Move.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input.Move.x += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) input.Move.y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) input.Move.y -= 1f;
            input.Restart = Input.GetKeyDown(KeyCode.R);
            input.Switch = Input.GetKeyDown(KeyCode.Tab);
            input.EndTurn = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
            input.Undo = Input.GetKeyDown(KeyCode.Z);
            input.Key1 = Input.GetKeyDown(KeyCode.Alpha1);
            input.Key2 = Input.GetKeyDown(KeyCode.Alpha2);
            input.Key3 = Input.GetKeyDown(KeyCode.Alpha3);
            input.Cancel = Input.GetKeyDown(KeyCode.Escape);
            input.MaxGear = Input.GetKeyDown(KeyCode.G);
            input.Mouse = Input.mousePosition;
            input.MouseHeld = Input.GetMouseButton(0);
            input.MouseClicked = Input.GetMouseButtonDown(0);
            input.RightClicked = Input.GetMouseButtonDown(1);
            FingerBuffer.Clear();
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                FingerPhase phase = t.phase == TouchPhase.Began ? FingerPhase.Down
                    : t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled ? FingerPhase.Up
                    : FingerPhase.Held;
                FingerBuffer.Add(new Finger { Id = t.fingerId, Phase = phase, At = new Vec2(t.position.x, t.position.y) });
            }
            input.Fingers = FingerBuffer;
#endif
            // 새 Input System 쪽은 손가락을 받지 않는다(이 프로젝트는 구형 Input Manager를 쓴다).
            if (input.Move.sqrMagnitude > 1f) input.Move.Normalize();
            return input;
        }
    }
}
