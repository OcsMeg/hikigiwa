using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hikigiwa.UI
{
    /// <summary>
    /// ボタンの見た目だけを担当する部品。触れたら少し大きく、押している間は少し縮みます。
    /// Button（など Selectable）と同じ GameObject に付けるだけで動き、Presenter や View の変更はいりません。
    /// </summary>
    /// <remarks>
    /// ・押せない状態（interactable が false、CanvasGroup で操作不可など）のときは大きさを変えません。
    /// ・キーボードやゲームパッドで選ばれたときも、触れたときと同じ大きさにします。
    ///   マウスのクリックで選ばれた場合は、離れたあとも大きいままにならないよう無視します。
    /// ・Time.timeScale = 0（ポーズ中）でも動きます。
    /// </remarks>
    [RequireComponent(typeof(Selectable))]
    public sealed class ButtonAnimation : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        [Tooltip("大きさを変える対象。空ならこの GameObject。")]
        [SerializeField] private RectTransform target;
        [SerializeField, Min(0f)] private float hoverScale = 1.06f;
        [SerializeField, Min(0f)] private float pressedScale = 0.94f;
        [SerializeField, Min(0f)] private float duration = 0.12f;
        [SerializeField] private Ease ease = Ease.OutQuad;

        private Selectable selectable;
        private Vector3 baseScale;
        private Tween tween;

        private bool hovered;
        private bool selected;
        private bool pressed;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
            if (target == null) target = (RectTransform)transform;
            baseScale = target.localScale;
        }

        private void OnDisable()
        {
            // 非表示になったら状態を戻す（再表示したときに大きいまま残らないように）
            hovered = selected = pressed = false;
            tween?.Kill();
            if (target != null) target.localScale = baseScale;
        }

        private void OnDestroy()
        {
            tween?.Kill();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            Refresh();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            pressed = true;
            Refresh();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            pressed = false;
            Refresh();
        }

        public void OnSelect(BaseEventData eventData)
        {
            // クリックで選ばれた場合は無視（マウスが離れても大きいままになるのを防ぐ）
            if (eventData is PointerEventData) return;
            selected = true;
            Refresh();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
            Refresh();
        }

        private void Refresh()
        {
            float scale = 1f;
            if (selectable.IsInteractable())
            {
                if (pressed && hovered) scale = pressedScale;
                else if (hovered || selected) scale = hoverScale;
            }

            tween?.Kill();
            tween = target.DOScale(baseScale * scale, duration)
                .SetEase(ease)
                .SetUpdate(true)
                .SetLink(gameObject);
        }
    }
}
