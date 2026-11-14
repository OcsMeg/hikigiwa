using Hikigiwa.Common.Audio;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hikigiwa.UI
{
    /// <summary>
    /// ボタンの効果音だけを担当する部品。カーソルを合わせたときと、クリックしたときに音を鳴らします。
    /// Button（など Selectable）と同じ GameObject に付けるだけで動き、Presenter や View の変更はいりません。
    /// </summary>
    /// <remarks>
    /// ・押せない状態（interactable が false、CanvasGroup で操作不可など）のときは、カーソルを合わせても鳴らしません。
    /// ・クリックの音は Button.onClick に合わせて鳴らします。Presenter と同じ間隔（UiConstants.ButtonThrottle）で
    ///   連打を間引くので、処理されなかったクリックでは鳴りません。
    /// ・音は AudioManager が鳴らすので、クリックでシーンが切り替わっても途中で切れません。
    /// </remarks>
    [RequireComponent(typeof(Selectable))]
    public sealed class ButtonSound : MonoBehaviour, IPointerEnterHandler
    {
        [Tooltip("カーソルを合わせたときの音。None なら鳴らさない。")]
        [SerializeField] private SeId hoverSe = SeId.UiHover;
        [Tooltip("クリックしたときの音。None なら鳴らさない（Button 以外では鳴らない）。")]
        [SerializeField] private SeId clickSe = SeId.Decide;

        private Selectable selectable;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
            if (selectable is Button button)
            {
                // Presenter の ThrottleFirst と同じ間隔で間引き、処理されるクリックのときだけ鳴らす
                button.OnClickAsObservable()
                    .ThrottleFirst(UiConstants.ButtonThrottle)
                    .Subscribe(_ => Play(clickSe))
                    .AddTo(this);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (selectable.IsInteractable()) Play(hoverSe);
        }

        private static void Play(SeId id)
        {
            if (id != SeId.None && AudioManager.HasInstance) AudioManager.Instance.PlaySe(id);
        }
    }
}
