using System.Threading;
using Cysharp.Threading.Tasks;

namespace Hikigiwa.UI.Overlay
{
    /// <summary>
    /// 画面（オーバーレイ）の Presenter の基底。
    /// 初期化時は非表示・操作不可にし、表示し終わってから操作を受け付け、
    /// 非表示を始める前に操作を止めます（フェード中の誤クリックを防ぐ）。
    /// </summary>
    public abstract class OverlayPresenterBase<TView> : PresenterBase<TView>, IOverlay
        where TView : OverlayViewBase
    {
        public bool IsVisible { get; private set; }

        public override void Initialize()
        {
            if (IsInitialized) return;

            base.Initialize();
            if (!IsInitialized) return;

            View.CanvasGroup.alpha = UiConstants.HiddenAlpha;
            View.SetInteractive(false);
            IsVisible = false;
        }

        public override async UniTask ShowAsync(bool animate, CancellationToken ct)
        {
            IsVisible = true;
            await View.ShowAsync(animate, ct);

            // 待っている間に Hide が呼ばれていたら操作を戻さない
            if (IsVisible) View.SetInteractive(true);
        }

        public override async UniTask HideAsync(bool animate, CancellationToken ct)
        {
            IsVisible = false;
            View.SetInteractive(false);
            await View.HideAsync(animate, ct);
        }
    }
}
