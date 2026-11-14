using Hikigiwa.UI.Overlay;
using R3;

namespace Hikigiwa.UI.Rules
{
    /// <summary>
    /// ルール説明画面。どの画面に戻るかは知らず、CloseRequested を発行するだけです（持ち主の Canvas が遷移を決める）。
    /// OptionPresenter と同じ作りなので、ゲーム中のポーズ画面などからも同じものを使えます。
    /// </summary>
    public sealed class RulesPresenter : OverlayPresenterBase<RulesView>
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

        private void OnDestroy()
        {
            closeRequested.Dispose();
        }
    }
}
