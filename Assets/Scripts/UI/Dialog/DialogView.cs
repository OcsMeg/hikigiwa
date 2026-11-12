using Hikigiwa.UI.Overlay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hikigiwa.UI.Dialog
{
    /// <summary>確認ダイアログの見た目。タイトル・本文・はい／いいえボタンを持つだけです。</summary>
    public class DialogView : OverlayViewBase
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button yesButton;
        [Tooltip("空にすると「はい」だけのお知らせダイアログになります。")]
        [SerializeField] private Button noButton;

        public Button YesButton => yesButton;
        public Button NoButton => noButton;

        public void SetContent(string title, string message)
        {
            if (titleText != null)
            {
                titleText.text = title;
                titleText.gameObject.SetActive(!string.IsNullOrEmpty(title));
            }
            if (messageText != null) messageText.text = message;
        }

        /// <summary>「いいえ」ボタンを出すかを切り替えます（お知らせダイアログ用）。</summary>
        public void SetNoButtonVisible(bool visible)
        {
            if (noButton != null) noButton.gameObject.SetActive(visible);
        }
    }
}
