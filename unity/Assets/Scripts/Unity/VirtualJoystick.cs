using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FireGame.UnityLayer
{
    /// <summary>
    /// 화면 왼쪽의 가상 조이스틱. 받침 안에서 손가락을 끌면 손잡이가 따라오고,
    /// 받침 반지름을 1로 한 방향 벡터를 넘긴다(-1..1).
    /// </summary>
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform Knob;
        public float Radius = 110f;

        /// <summary>화면 기준 방향. 위쪽이 +y다.</summary>
        public Action<Vector2> OnMove;

        private RectTransform _rect;

        private void Awake()
        {
            _rect = (RectTransform)transform;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                return;
            }

            SetKnob(local / Radius);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            SetKnob(Vector2.zero);
        }

        /// <summary>손잡이 위치를 직접 정한다. 스크린샷 하네스도 이걸로 민 상태를 만든다.</summary>
        public void SetKnob(Vector2 direction)
        {
            direction = Vector2.ClampMagnitude(direction, 1f);
            if (Knob != null) Knob.anchoredPosition = direction * Radius;
            if (OnMove != null) OnMove(direction);
        }
    }
}
