using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Hikigiwa.UI.Overlay
{
    /// <summary>
    /// 縦にスクロールする文章と「戻る」ボタンだけを持つ画面の View（ルール説明・クレジットなど）。
    /// 文章はシーン上に置いたものをそのまま表示します。
    /// </summary>
    public abstract class ScrollOverlayViewBase : OverlayViewBase
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Button backButton;

        public Button BackButton => backButton;

        /// <summary>開くたびに先頭から読めるよう、スクロール位置を一番上に戻してから表示します。</summary>
        public override UniTask ShowAsync(bool animate, CancellationToken ct)
        {
            if (scrollRect != null)
            {
                // 文章の高さが確定していないと一番上に戻せないため、先にレイアウトを計算させる
                Canvas.ForceUpdateCanvases();
                scrollRect.StopMovement();
                scrollRect.verticalNormalizedPosition = 1f;
            }
            return base.ShowAsync(animate, ct);
        }
    }
}
