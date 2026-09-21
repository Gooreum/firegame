using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FireGame.UnityLayer
{
    /// <summary>
    /// uGUI 요소를 코드로 만드는 공통 함수. 프리팹 없이 화면을 짜므로
    /// 씬이나 인스펙터 설정이 틀려 화면이 비는 일이 없고, 스크린샷 하네스에서도 똑같이 만들 수 있다.
    ///
    /// 기준 해상도는 1920x1080(가로). 모든 크기와 위치는 이 기준의 픽셀로 적는다.
    /// </summary>
    public static class UiKit
    {
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;

        /// <summary>UI 캔버스의 정렬 순서 기준. 현장 스프라이트(0~30)보다 위.</summary>
        public const int UiSortingOrder = 1000;

        public static readonly Color Ink = new Color(0.16f, 0.16f, 0.2f);
        public static readonly Color Paper = new Color(0.97f, 0.96f, 0.93f);
        public static readonly Color Shade = new Color(0f, 0f, 0f, 0.55f);

        /// <summary>
        /// 카메라에 붙는 캔버스. 스크린샷 하네스가 카메라 한 대로 월드와 UI를 함께 찍을 수 있다.
        /// planeDistance가 카메라와 월드(z=0) 사이에 있어야 UI가 스프라이트 위에 그려진다.
        /// </summary>
        public static Canvas CreateCanvas(Transform parent, Camera camera, string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;

            // 카메라에 붙은 캔버스는 거리가 아니라 정렬 순서로 앞뒤가 정해진다.
            // 현장 그림이 0~30을 쓰므로 UI는 그보다 확실히 위에 둔다. 안 그러면 불꽃·벽이 패널을 뚫고 나온다.
            canvas.sortingOrder = UiSortingOrder + order;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 1f;   // 가로 화면이라 높이를 기준으로 맞춘다

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>버튼·드래그를 받으려면 씬에 하나 있어야 한다.</summary>
        public static void EnsureEventSystem(Transform parent)
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.transform.SetParent(parent, false);
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        /// <summary>
        /// 게임 중에는 Destroy, 에디터(스크린샷 하네스)에서는 DestroyImmediate로 지운다.
        /// 에디터에서 Destroy를 부르면 오류가 나고 아무것도 지워지지 않는다.
        /// </summary>
        public static void Discard(GameObject target)
        {
            if (target == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>
        /// 앵커 한 점을 기준으로 놓는다. <paramref name="anchor"/>는 (0,0)=왼쪽 아래 ~ (1,1)=오른쪽 위.
        /// </summary>
        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        // 메서드 이름 Image·Button이 같은 이름의 uGUI 타입을 가리므로 타입은 전체 이름으로 쓴다.
        public static UnityEngine.UI.Image Image(Transform parent, string name, Sprite sprite, Color color)
        {
            RectTransform rect = Node(parent, name);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sprite != null && sprite.border.sqrMagnitude > 0f ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor alignment)
        {
            RectTransform rect = Node(parent, name);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Art.Font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>밝은 배경 위에서도 읽히도록 어두운 테두리를 두른다.</summary>
        public static Text OutlinedLabel(Transform parent, string name, string text, int size, Color color, TextAnchor alignment)
        {
            Text label = Label(parent, name, text, size, color, alignment);
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            outline.effectDistance = new Vector2(2f, -2f);
            return label;
        }

        public static UnityEngine.UI.Button Button(Transform parent, string name, Sprite sprite, string text, int fontSize, Action onClick)
        {
            UnityEngine.UI.Image image = Image(parent, name, sprite, Color.white);
            image.raycastTarget = true;

            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            if (!string.IsNullOrEmpty(text))
            {
                Text label = OutlinedLabel(image.transform, "Label", text, fontSize, Color.white, TextAnchor.MiddleCenter);
                Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(8f, 6f);   // 버튼 아래 두께만큼 글자를 올린다
            }

            return button;
        }
    }
}
