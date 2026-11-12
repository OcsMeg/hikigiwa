using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Hikigiwa.Common;
using UnityEngine;

namespace Hikigiwa.UI.Overlay
{
    /// <summary>
    /// CanvasGroup.alpha のフェードで表示・非表示を切り替える画面の View。
    /// 子クラスはボタンなどを [SerializeField] で並べ、読み取り専用プロパティで公開するだけでよい。
    /// 別の演出にしたいときは ShowAsync / HideAsync を override します。
    /// </summary>
    public abstract class OverlayViewBase : ViewBase
    {
        [SerializeField, Min(0f)] private float fadeDuration = UiConstants.DefaultFadeDuration;
        [SerializeField] private Ease fadeEase = Ease.OutQuad;

        private Tween fadeTween;

        public override UniTask ShowAsync(bool animate, CancellationToken ct) =>
            FadeAsync(UiConstants.ShownAlpha, animate, ct);

        public override UniTask HideAsync(bool animate, CancellationToken ct) =>
            FadeAsync(UiConstants.HiddenAlpha, animate, ct);

        protected UniTask FadeAsync(float targetAlpha, bool animate, CancellationToken ct)
        {
            // 前のフェードが残っていれば止める（止められた側の await はそこで終わる）
            fadeTween?.Kill();
            fadeTween = null;

            if (!animate || fadeDuration <= 0f)
            {
                CanvasGroup.alpha = targetAlpha;
                return UniTask.CompletedTask;
            }

            fadeTween = DOTween
                .To(() => CanvasGroup.alpha, a => CanvasGroup.alpha = a, targetAlpha, fadeDuration)
                .SetEase(fadeEase)
                .SetUpdate(true) // Time.timeScale = 0（ポーズ中）でも動かす
                .SetLink(gameObject);

            return fadeTween.AwaitCompletionAsync(ct);
        }

        protected virtual void OnDestroy()
        {
            fadeTween?.Kill();
        }
    }
}
