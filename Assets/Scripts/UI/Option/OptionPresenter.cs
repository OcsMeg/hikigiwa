using System.Threading;
using Cysharp.Threading.Tasks;
using Hikigiwa.Common.Audio;
using Hikigiwa.UI.Overlay;
using R3;

namespace Hikigiwa.UI.Option
{
    /// <summary>
    /// 設定画面。スライダーと VolumeSettings をつなぎ、閉じるときに保存します。
    /// どの画面に戻るかは知らず、CloseRequested を発行するだけです（持ち主の Canvas が遷移を決める）。
    /// そのため、タイトル画面でもゲーム中のポーズ画面でも同じものを使えます。
    /// </summary>
    public class OptionPresenter : OverlayPresenterBase<OptionView>
    {
        private readonly Subject<Unit> closeRequested = new();

        /// <summary>「戻る」が押されたときに発行されます。</summary>
        public Observable<Unit> CloseRequested => closeRequested;

        protected override void SetEvent()
        {
            var settings = VolumeSettings.Instance;

            foreach (var row in View.VolumeRows)
            {
                if (row.Slider == null) continue;
                var channel = row.Channel;

                // OnValueChangedAsObservable() は購読した瞬間に今の値を流し、保存済みの音量を上書きしてしまうため、
                // 値が変わったときだけ流れる onValueChanged.AsObservable() を使う
                row.Slider.onValueChanged.AsObservable(destroyCancellationToken)
                    .Subscribe(v => settings.Set(channel, v))
                    .AddTo(this);
            }

            if (View.ResetButton != null)
            {
                View.ResetButton.OnClickAsObservable()
                    .ThrottleFirst(UiConstants.ButtonThrottle)
                    .Subscribe(_ => settings.ResetToDefault())
                    .AddTo(this);
            }

            View.BackButton.OnClickAsObservable()
                .ThrottleFirst(UiConstants.ButtonThrottle)
                .Subscribe(_ => closeRequested.OnNext(Unit.Default))
                .AddTo(this);
        }

        protected override void Bind()
        {
            var settings = VolumeSettings.Instance;

            foreach (var row in View.VolumeRows)
            {
                var target = row;
                settings.Get(target.Channel)
                    .Subscribe(v => View.SetVolume(target, v))
                    .AddTo(this);
            }
        }

        /// <summary>どんな形で閉じられても保存されるよう、非表示にするときに保存します。</summary>
        public override UniTask HideAsync(bool animate, CancellationToken ct)
        {
            if (IsVisible) VolumeSettings.Instance.Save();
            return base.HideAsync(animate, ct);
        }

        private void OnDestroy()
        {
            closeRequested.Dispose();
        }
    }
}
