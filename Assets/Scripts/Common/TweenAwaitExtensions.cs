using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace Hikigiwa.Common
{
    /// <summary>
    /// DOTween の Tween を UniTask で await するための拡張メソッド。
    /// UniTask 標準の DOTween 連携（UNITASK_DOTWEEN_SUPPORT）を使わずに待てるようにしています。
    /// </summary>
    public static class TweenAwaitExtensions
    {
        /// <summary>
        /// Tween が終わる（完了するか Kill される）まで待ちます。
        /// キャンセルされたら Tween を Kill して OperationCanceledException を投げます。
        /// </summary>
        /// <remarks>
        /// 内部で OnKill を使うため、同じ Tween に設定済みの OnKill は上書きされます。
        /// また、SetAutoKill(false) の Tween は完了しても終わらないので使わないでください。
        /// </remarks>
        public static UniTask AwaitCompletionAsync(this Tween tween, CancellationToken ct)
        {
            if (tween == null || !tween.IsActive() || tween.IsComplete()) return UniTask.CompletedTask;

            if (ct.IsCancellationRequested)
            {
                tween.Kill();
                return UniTask.FromCanceled(ct);
            }

            var tcs = new UniTaskCompletionSource();
            var registration = ct.Register(() =>
            {
                // 先にキャンセルを確定させてから Kill する（OnKill の TrySetResult は無視される）
                tcs.TrySetCanceled(ct);
                tween.Kill();
            });

            tween.OnKill(() =>
            {
                registration.Dispose();
                tcs.TrySetResult();
            });

            return tcs.Task;
        }
    }
}
