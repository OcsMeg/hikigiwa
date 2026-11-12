using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hikigiwa.UI
{
    /// <summary>
    /// 見た目を担当するクラスの基底。
    /// 表示の切り替えは SetActive ではなく CanvasGroup で行います
    /// （GameObject を無効にしないので、購読が切れたり Awake の順番がずれたりしない）。
    /// </summary>
    /// <remarks>
    /// View は「見た目を変えるメソッド」と「ボタンなどの参照」だけを公開し、判断は Presenter に任せます。
    /// </remarks>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class ViewBase : MonoBehaviour
    {
        private CanvasGroup canvasGroup;

        public CanvasGroup CanvasGroup
        {
            get
            {
                if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
                return canvasGroup;
            }
        }

        /// <summary>Presenter.Initialize から最初に1回だけ呼ばれます。</summary>
        public virtual void Initialize() { }

        /// <summary>表示します。animate が false なら即座に切り替えます。</summary>
        public abstract UniTask ShowAsync(bool animate, CancellationToken ct);

        /// <summary>非表示にします。animate が false なら即座に切り替えます。</summary>
        public abstract UniTask HideAsync(bool animate, CancellationToken ct);

        /// <summary>操作（クリックやレイキャスト）を受け付けるかを切り替えます。</summary>
        public void SetInteractive(bool interactive)
        {
            CanvasGroup.interactable = interactive;
            CanvasGroup.blocksRaycasts = interactive;
        }
    }
}
