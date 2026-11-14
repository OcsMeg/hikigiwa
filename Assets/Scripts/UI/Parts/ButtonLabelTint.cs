using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hikigiwa.UI
{
    /// <summary>
    /// ボタンの文字の色だけを担当する部品。触れたとき（またはキーボードで選ばれたとき）に文字を目立つ色に変えます。
    /// Button（など Selectable）と同じ GameObject に付けます。大きさの変化は ButtonAnimation、縁の色は Button の Color Tint が担当します。
    /// </summary>
    /// <remarks>
    /// ・押せない状態（interactable が false、CanvasGroup で操作不可など）のときは色を変えません。
    /// ・マウスのクリックで選ばれた場合は、離れたあとも色が残らないよう無視します（ButtonAnimation と同じ）。
    /// ・Time.timeScale = 0（ポーズ中）でも動きます。
    /// </remarks>
    [RequireComponent(typeof(Selectable))]
    public sealed class ButtonLabelTint : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        [Tooltip("色を変える文字。")]
        [SerializeField] private Graphic label;
        [SerializeField] private Color normalColor = new(0.9647059f, 0.8980392f, 0.78039217f);
        [SerializeField] private Color highlightColor = new(0.9254902f, 0.76862746f, 0.4392157f);
        [SerializeField, Min(0f)] private float duration = 0.12f;

        private Selectable selectable;
        private Tween tween;

        private bool hovered;
        private bool selected;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
        }

        private void OnDisable()
        {
            // 非表示になったら状態を戻す（再表示したときに色が残らないように）
            hovered = selected = false;
            tween?.Kill();
            if (label != null) label.color = normalColor;
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

        public void OnSelect(BaseEventData eventData)
        {
            // クリックで選ばれた場合は無視（マウスが離れても色が残るのを防ぐ）
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
            if (label == null) return;

            bool highlighted = selectable.IsInteractable() && (hovered || selected);
            Color target = highlighted ? highlightColor : normalColor;

            tween?.Kill();
            tween = DOTween.To(() => label.color, c => label.color = c, target, duration)
                .SetUpdate(true)
                .SetLink(gameObject);
        }
    }
}
