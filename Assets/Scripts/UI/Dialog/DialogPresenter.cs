using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hikigiwa.UI.Overlay;
using R3;

namespace Hikigiwa.UI.Dialog
{
    /// <summary>
    /// 確認ダイアログ。OpenAsync を await すると、閉じられるまで待って「はい」が押されたかを返します。
    /// </summary>
    /// <example>
    /// <code>
    /// bool ok = await dialog.OpenAsync("確認", "タイトルへ戻りますか？", destroyCancellationToken);
    /// if (ok) { /* タイトルへ */ }
    /// </code>
    /// </example>
    public class DialogPresenter : OverlayPresenterBase<DialogView>
    {
        private UniTaskCompletionSource<bool> pending;

        /// <summary>ダイアログが開いていて、返事を待っているか。</summary>
        public bool IsOpen => pending != null;

        protected override void SetEvent()
        {
            View.YesButton.OnClickAsObservable()
                .Subscribe(_ => pending?.TrySetResult(true))
                .AddTo(this);

            if (View.NoButton != null)
            {
                View.NoButton.OnClickAsObservable()
                    .Subscribe(_ => pending?.TrySetResult(false))
                    .AddTo(this);
            }
        }

        /// <summary>「はい／いいえ」の確認ダイアログを開き、結果を待ちます。</summary>
        public UniTask<bool> OpenAsync(string title, string message, CancellationToken ct) =>
            OpenCoreAsync(title, message, showNoButton: true, ct);

        /// <summary>「はい」だけのお知らせダイアログを開き、閉じられるまで待ちます。</summary>
        public UniTask AlertAsync(string title, string message, CancellationToken ct) =>
            OpenCoreAsync(title, message, showNoButton: false, ct).AsUniTask();

        private async UniTask<bool> OpenCoreAsync(string title, string message, bool showNoButton, CancellationToken ct)
        {
            if (!IsInitialized) Initialize();
            if (IsOpen) throw new InvalidOperationException("ダイアログはすでに開いています。");

            var tcs = new UniTaskCompletionSource<bool>();
            pending = tcs;

            View.SetContent(title, message);
            View.SetNoButtonVisible(showNoButton);

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, destroyCancellationToken);
            bool result;
            try
            {
                using (linked.Token.Register(() => tcs.TrySetCanceled(linked.Token)))
                {
                    await ShowAsync(true, linked.Token);
                    result = await tcs.Task;
                }
            }
            catch (OperationCanceledException)
            {
                // 呼び出し元のキャンセルなら即座に閉じる（自分が破棄されたときは何もしない）
                if (!destroyCancellationToken.IsCancellationRequested)
                {
                    HideAsync(false, CancellationToken.None).Forget();
                }
                throw;
            }
            finally
            {
                pending = null;
            }

            await HideAsync(true, destroyCancellationToken);
            return result;
        }
    }
}
