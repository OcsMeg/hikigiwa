using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hikigiwa.Common
{
    /// <summary>
    /// フェード付きのシーン遷移。ゲーム起動時に自動で生成され、シーンをまたいで残ります（シーンに置く必要はありません）。
    /// </summary>
    /// <example>
    /// <code>
    /// bool ok = await SceneTransition.Instance.LoadAsync("Main");
    /// </code>
    /// </example>
    /// <remarks>
    /// 遷移の処理は自分の destroyCancellationToken だけで動かします。
    /// 呼び出し元のトークンを使うと、シーンのロードで呼び出し元が破棄されたときに遷移が途中で止まってしまうためです。
    /// </remarks>
    public sealed class SceneTransition : DontDestroySingletonBase<SceneTransition>
    {
        private const int SortingOrder = 1000;
        private const float FadeOutDuration = 0.4f;
        private const float FadeInDuration = 0.4f;
        private static readonly Color FadeColor = Color.black;

        private CanvasGroup fadeGroup;
        private Tween fadeTween;

        /// <summary>遷移の途中か。</summary>
        public bool IsLoading { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateOnStartup()
        {
            if (HasInstance) return;
            new GameObject(nameof(SceneTransition)).AddComponent<SceneTransition>();
        }

        protected override void OnAwake()
        {
            base.OnAwake();
            BuildFadeCanvas();
        }

        protected override void OnDestroyed()
        {
            fadeTween?.Kill();
        }

        /// <summary>
        /// 画面を暗くしてからシーンをロードし、明るく戻します。
        /// すでに遷移中のとき、またはシーンが Build Settings にないときは何もせず false を返します。
        /// </summary>
        public async UniTask<bool> LoadAsync(string sceneName)
        {
            if (IsLoading) return false;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"シーン「{sceneName}」が Build Settings に登録されていません。", this);
                return false;
            }

            IsLoading = true;
            CancellationToken ct = destroyCancellationToken;
            try
            {
                // 遷移中は下の UI を押せないようにする
                fadeGroup.blocksRaycasts = true;
                await FadeAsync(1f, FadeOutDuration, ct);

                await SceneManager.LoadSceneAsync(sceneName).ToUniTask(cancellationToken: ct);

                // 新しいシーンの Start（UI の初期化など）が終わるのを1フレーム待つ
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                await FadeAsync(0f, FadeInDuration, ct);
                return true;
            }
            finally
            {
                if (fadeGroup != null) fadeGroup.blocksRaycasts = false;
                IsLoading = false;
            }
        }

        private UniTask FadeAsync(float targetAlpha, float duration, CancellationToken ct)
        {
            fadeTween?.Kill();
            fadeTween = DOTween
                .To(() => fadeGroup.alpha, a => fadeGroup.alpha = a, targetAlpha, duration)
                .SetEase(Ease.Linear)
                .SetUpdate(true) // ポーズ中（Time.timeScale = 0）でも動かす
                .SetLink(gameObject);
            return fadeTween.AwaitCompletionAsync(ct);
        }

        /// <summary>全画面を覆う黒い板を、一番手前に描かれる Canvas として作ります。</summary>
        private void BuildFadeCanvas()
        {
            var canvasObject = new GameObject("FadeCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            fadeGroup = canvasObject.GetComponent<CanvasGroup>();
            fadeGroup.alpha = 0f;
            fadeGroup.interactable = false;
            fadeGroup.blocksRaycasts = false;

            var imageObject = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            imageObject.GetComponent<Image>().color = FadeColor;
        }
    }
}
