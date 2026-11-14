using System;
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
                var changed = row.Slider.onValueChanged.AsObservable(destroyCancellationToken);
                changed.Subscribe(v => settings.Set(channel, v)).AddTo(this);

                // SEは、変えた音量で試し音を鳴らす（音量を変える購読より後に購読するので、変えた後の音量で鳴る）。
                // 動かし続けても鳴りすぎないよう間引き、動かし始めと、動かし終えた最後の値では必ず鳴らす。
                // ポーズ中（Time.timeScale = 0）でも動くよう、時間の速さの影響を受けないタイマーを使う。
                if (channel == VolumeChannel.Se)
                {
                    changed.ThrottleFirstLast(SePreviewInterval, UnityTimeProvider.UpdateIgnoreTimeScale)
                        .Subscribe(_ => PlaySePreview())
                        .AddTo(this);
                }
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

        /// <summary>スライダーを動かし続けても鳴りすぎないよう、試し音はこの間隔に1回まで（最後の値では必ず鳴る）。</summary>
        private static readonly TimeSpan SePreviewInterval = TimeSpan.FromSeconds(0.15);

        /// <summary>SEの音量を変えたときの試し音。</summary>
        private static void PlaySePreview()
        {
            if (AudioManager.HasInstance) AudioManager.Instance.PlaySe(SeId.Decide);
        }

        private void OnDestroy()
        {
            closeRequested.Dispose();
        }
    }
}
