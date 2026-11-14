using System.Threading;
using Cysharp.Threading.Tasks;
using Hikigiwa.UI.Overlay;
using UnityEngine;
using UnityEngine.UI;

namespace Hikigiwa.UI.Rules
{
    /// <summary>
    /// ルール説明画面の見た目。説明文はシーン上に置いたものをそのまま表示し、スクロールと戻るボタンだけを持ちます。
    /// 中身はメニューの「Hikigiwa → タイトルにルール画面を作成」で作り、文章や配置はシーン上で直接編集します。
    /// </summary>
    public sealed class RulesView : OverlayViewBase
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Button backButton;

        public Button BackButton => backButton;

        /// <summary>開くたびに説明の先頭から読めるよう、スクロール位置を一番上に戻してから表示します。</summary>
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
