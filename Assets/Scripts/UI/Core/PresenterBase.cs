using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hikigiwa.UI
{
    /// <summary>
    /// 入力を受けて判断し、View を動かすクラスの基底。
    /// 初期化の順番を Initialize → View.Initialize → SetEvent → Bind に固定します。
    /// </summary>
    /// <remarks>
    /// View は同じ GameObject に付ける前提です（OnValidate で自動設定）。
    /// Initialize は自分では呼ばず、まとめ役（CanvasBase など）から呼んでもらいます。
    /// </remarks>
    public abstract class PresenterBase<TView> : MonoBehaviour where TView : ViewBase
    {
        [SerializeField] private TView view;

        protected TView View => view;

        public bool IsInitialized { get; private set; }

        public virtual void Initialize()
        {
            if (IsInitialized) return;

            if (view == null) view = GetComponent<TView>();
            if (view == null)
            {
                Debug.LogError($"{GetType().Name}: {typeof(TView).Name} が見つかりません。", this);
                return;
            }

            view.Initialize();
            SetEvent();
            Bind();
            IsInitialized = true;
        }

        /// <summary>入力（View → Presenter）を購読します。例：ボタンのクリック。</summary>
        protected virtual void SetEvent() { }

        /// <summary>データ（Model や ReactiveProperty → View）を購読し、見た目に反映します。</summary>
        protected virtual void Bind() { }

        public virtual UniTask ShowAsync(bool animate, CancellationToken ct) => View.ShowAsync(animate, ct);

        public virtual UniTask HideAsync(bool animate, CancellationToken ct) => View.HideAsync(animate, ct);

        /// <summary>自分が破棄されたらキャンセルされるトークンで表示します。</summary>
        public UniTask ShowAsync(bool animate = true) => ShowAsync(animate, destroyCancellationToken);

        /// <summary>自分が破棄されたらキャンセルされるトークンで非表示にします。</summary>
        public UniTask HideAsync(bool animate = true) => HideAsync(animate, destroyCancellationToken);

        protected virtual void OnValidate()
        {
            if (view == null) view = GetComponent<TView>();
        }
    }
}
