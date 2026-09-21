using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FireGame.UnityLayer
{
    /// <summary>누르고 있는 동안 켜지는 버튼(발사).</summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Action<bool> OnHold;

        public bool Held { get; private set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            Set(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Set(false);
        }

        /// <summary>손가락이 버튼 밖으로 미끄러지면 멈춘다. 안 그러면 손을 떼도 계속 쏜다.</summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            Set(false);
        }

        private void Set(bool held)
        {
            if (Held == held) return;
            Held = held;
            if (OnHold != null) OnHold(held);
        }
    }
}
