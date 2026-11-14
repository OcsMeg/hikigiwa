using R3;

namespace Hikigiwa.UI.Overlay
{
    /// <summary>
    /// スクロールする文章と「戻る」ボタンだけの画面の Presenter（ルール説明・クレジットなど）。
    /// どの画面に戻るかは知らず、CloseRequested を発行するだけです（持ち主の Canvas が遷移を決める）。
    /// </summary>
    public abstract class ScrollOverlayPresenterBase<TView> : OverlayPresenterBase<TView>
        where TView : ScrollOverlayViewBase
    {
        private readonly Subject<Unit> closeRequested = new();

        /// <summary>「戻る」が押されたときに発行されます。</summary>
        public Observable<Unit> CloseRequested => closeRequested;

        protected override void SetEvent()
        {
            if (View.BackButton == null) return;

            View.BackButton.OnClickAsObservable()
                .ThrottleFirst(UiConstants.ButtonThrottle)
                .Subscribe(_ => closeRequested.OnNext(Unit.Default))
                .AddTo(this);
        }

        protected virtual void OnDestroy()
        {
            closeRequested.Dispose();
        }
    }
}
