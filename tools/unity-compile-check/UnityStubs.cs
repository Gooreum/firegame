// Unity API 스텁. Unity 레이어가 실제로 쓰는 멤버만 옮겨 적었다.
// 동작은 없고 시그니처만 있다. 컴파일 검사 전용이며 게임에 포함되지 않는다.
#pragma warning disable CS0067, CS0649, CA1822, IDE0060

namespace UnityEngine
{
    public class Object
    {
        public static void DontDestroyOnLoad(Object target) { }
        public static T FindAnyObjectByType<T>() where T : Object { return null; }
    }

    public class Component : Object
    {
        public Transform transform { get { return null; } }
    }

    public class Behaviour : Component { }

    public class MonoBehaviour : Behaviour { }

    public class Transform : Component
    {
        public Vector3 position { get; set; }
        public void SetParent(Transform parent, bool worldPositionStays) { }
    }

    public sealed class GameObject : Object
    {
        public GameObject(string name) { }
        public string tag { get; set; }
        public Transform transform { get { return null; } }
        public T AddComponent<T>() where T : Component { return null; }
    }

    public enum CameraClearFlags { Skybox = 1, SolidColor = 2, Depth = 3, Nothing = 4 }

    public sealed class Camera : Behaviour
    {
        public static Camera main { get { return null; } }
        public bool orthographic { get; set; }
        public float orthographicSize { get; set; }
        public CameraClearFlags clearFlags { get; set; }
        public Color backgroundColor { get; set; }
    }

    public class Renderer : Component { }

    public sealed class SpriteRenderer : Renderer
    {
        public Sprite sprite { get; set; }
    }

    public class Texture : Object
    {
        public FilterMode filterMode { get; set; }
        public TextureWrapMode wrapMode { get; set; }
    }

    public sealed class Texture2D : Texture
    {
        public Texture2D(int width, int height, TextureFormat textureFormat, bool mipChain) { }
        public void SetPixels32(Color32[] colors) { }
        public void Apply(bool updateMipmaps) { }
    }

    public enum TextureFormat { RGBA32 = 4 }
    public enum FilterMode { Point = 0, Bilinear = 1, Trilinear = 2 }
    public enum TextureWrapMode { Repeat = 0, Clamp = 1 }

    public sealed class Sprite : Object
    {
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit) { return null; }
    }

    public struct Rect
    {
        public Rect(float x, float y, float width, float height) { }
    }

    public struct Vector2
    {
        public float x;
        public float y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static implicit operator Vector2(Vector3 v) { return new Vector2(v.x, v.y); }
    }

    public struct Vector3
    {
        public float x;
        public float y;
        public float z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }

    public struct Color
    {
        public static Color black { get { return default; } }
    }

    public struct Color32
    {
        public Color32(byte r, byte g, byte b, byte a) { }
    }

    public static class Mathf
    {
        public static float Min(float a, float b) { return a < b ? a : b; }
    }

    public static class Time
    {
        public static float unscaledDeltaTime { get { return 0f; } }
    }

    public static class Application
    {
        public static int targetFrameRate { get; set; }
    }

    public static class SleepTimeout
    {
        public const int NeverSleep = -1;
    }

    public static class Screen
    {
        public static int width { get { return 0; } }
        public static int height { get { return 0; } }
        public static int sleepTimeout { get; set; }
    }

    public static class PlayerPrefs
    {
        public static string GetString(string key, string defaultValue) { return defaultValue; }
        public static void SetString(string key, string value) { }
        public static void Save() { }
    }

    public static class Debug
    {
        public static void LogWarning(object message) { }
    }

    public enum RuntimeInitializeLoadType { AfterSceneLoad = 0, BeforeSceneLoad = 1 }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : System.Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType) { }
    }

    // ---- 구형 입력 ----
    public enum TouchPhase { Began = 0, Moved = 1, Stationary = 2, Ended = 3, Canceled = 4 }

    public struct Touch
    {
        public int fingerId { get { return 0; } }
        public Vector2 position { get { return default; } }
        public TouchPhase phase { get { return default; } }
    }

    public enum KeyCode
    {
        Space = 32, Alpha1 = 49, Alpha2 = 50, Alpha3 = 51,
        A = 97, D = 100, S = 115, W = 119,
        UpArrow = 273, DownArrow = 274, RightArrow = 275, LeftArrow = 276,
    }

    public static class Input
    {
        public static int touchCount { get { return 0; } }
        public static Touch GetTouch(int index) { return default; }
        public static bool GetMouseButton(int button) { return false; }
        public static Vector3 mousePosition { get { return default; } }
        public static bool GetKey(KeyCode key) { return false; }
        public static bool GetKeyDown(KeyCode key) { return false; }
    }
}

// ---- 새 Input System ----
namespace UnityEngine.InputSystem.Controls
{
    public class InputControl<TValue> where TValue : struct
    {
        public TValue ReadValue() { return default; }
    }

    public sealed class IntegerControl : InputControl<int> { }

    public sealed class Vector2Control : InputControl<UnityEngine.Vector2> { }

    public class ButtonControl : InputControl<float>
    {
        public bool isPressed { get { return false; } }
        public bool wasPressedThisFrame { get { return false; } }
    }

    public sealed class KeyControl : ButtonControl { }

    public sealed class TouchControl
    {
        public ButtonControl press { get { return null; } }
        public IntegerControl touchId { get { return null; } }
        public Vector2Control position { get { return null; } }
    }
}

namespace UnityEngine.InputSystem.Utilities
{
    public struct ReadOnlyArray<TValue> : System.Collections.Generic.IEnumerable<TValue>
    {
        public System.Collections.Generic.IEnumerator<TValue> GetEnumerator() { yield break; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }
}

namespace UnityEngine.InputSystem
{
    using UnityEngine.InputSystem.Controls;
    using UnityEngine.InputSystem.Utilities;

    public sealed class Touchscreen
    {
        public static Touchscreen current { get { return null; } }
        public ReadOnlyArray<TouchControl> touches { get { return default; } }
    }

    public sealed class Mouse
    {
        public static Mouse current { get { return null; } }
        public ButtonControl leftButton { get { return null; } }
        public Vector2Control position { get { return null; } }
    }

    public sealed class Keyboard
    {
        public static Keyboard current { get { return null; } }
        public KeyControl leftArrowKey { get { return null; } }
        public KeyControl rightArrowKey { get { return null; } }
        public KeyControl upArrowKey { get { return null; } }
        public KeyControl downArrowKey { get { return null; } }
        public KeyControl aKey { get { return null; } }
        public KeyControl dKey { get { return null; } }
        public KeyControl wKey { get { return null; } }
        public KeyControl sKey { get { return null; } }
        public KeyControl spaceKey { get { return null; } }
        public KeyControl digit1Key { get { return null; } }
        public KeyControl digit2Key { get { return null; } }
        public KeyControl digit3Key { get { return null; } }
    }
}
