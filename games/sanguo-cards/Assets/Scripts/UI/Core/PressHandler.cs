using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Sanguo.UI
{
    /// <summary>
    /// Tap and long-press on any raycastable graphic. Touch screens have no hover, so long-press is
    /// how players read card and skill descriptions. A long press never also fires a tap.
    /// </summary>
    public sealed class PressHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public const float LongPressSeconds = 0.45f;

        private bool _pressed;
        private bool _consumed;
        private float _downTime;
        private Vector2 _downPosition;

        public Action Clicked;
        public Action LongPressed;

        public static PressHandler On(Component c, Action clicked, Action longPressed = null)
        {
            if (!c.TryGetComponent(out PressHandler h)) h = c.gameObject.AddComponent<PressHandler>();
            h.Clicked = clicked;
            h.LongPressed = longPressed;
            return h;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            _consumed = false;
            _downTime = Time.unscaledTime;
            _downPosition = eventData.position;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_pressed) return;
            _pressed = false;
            if (_consumed) return;
            // A drag (e.g. scrolling the seat strip) is not a tap.
            if ((eventData.position - _downPosition).sqrMagnitude > 40f * 40f) return;
            Clicked?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Touch input sends exit on release too; only a real mouse exit cancels the press.
            if (eventData.pointerId < 0) _pressed = false;
        }

        private void OnDisable() => _pressed = false;

        private void Update()
        {
            if (!_pressed || _consumed || LongPressed == null) return;
            if (Time.unscaledTime - _downTime < LongPressSeconds) return;
            _consumed = true;
            LongPressed();
        }
    }
}
