using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hikigiwa.Common;
using Hikigiwa.UI.Overlay;
using UnityEngine;

namespace Hikigiwa.UI
{
    /// <summary>
    /// シーン内の画面（IOverlay）をまとめて持ち、OverlayType で切り替えるまとめ役の基底。
    /// 画面同士は直接参照し合わず、遷移はすべて XxxCanvas.Instance.SwitchAsync(...) を通します。
    /// </summary>
    /// <example>
    /// <code>
    /// public sealed class TitleCanvas : CanvasBase&lt;TitleCanvas&gt;
    /// {
    ///     [SerializeField] private TitlePresenter title;
    ///     [SerializeField] private OptionPresenter option;
    ///
    ///     protected override OverlayType InitialOverlay => OverlayType.Title;
    ///
    ///     protected override IEnumerable&lt;IOverlay&gt; AllOverlays
    ///     {
    ///         get { yield return title; yield return option; }
    ///     }
    ///
    ///     protected override IOverlay GetOverlay(OverlayType type) => type switch
    ///     {
    ///         OverlayType.Title => title,
    ///         OverlayType.Option => option,
    ///         _ => null,
    ///     };
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// 全 Presenter の Awake が終わってから初期化するため、Start で Initialize を呼びます。
    /// </remarks>
    public abstract class CanvasBase<T> : SingletonBase<T> where T : CanvasBase<T>
    {
        /// <summary>今表示している画面。</summary>
        public OverlayType Current { get; private set; } = OverlayType.None;

        /// <summary>SwitchAsync の途中か。</summary>
        public bool IsSwitching { get; private set; }

        /// <summary>最初に表示する画面。None なら何も出しません。</summary>
        protected virtual OverlayType InitialOverlay => OverlayType.None;

        /// <summary>この Canvas が持つ全画面（初期化に使う）。</summary>
        protected abstract IEnumerable<IOverlay> AllOverlays { get; }

        /// <summary>OverlayType に対応する画面を返します。持っていなければ null。</summary>
        protected abstract IOverlay GetOverlay(OverlayType type);

        protected virtual void Start()
        {
            Initialize();
        }

        protected virtual void Initialize()
        {
            foreach (var overlay in AllOverlays)
            {
                if (overlay == null) continue;
                overlay.Initialize();
                overlay.HideAsync(false, CancellationToken.None).Forget();
            }

            if (InitialOverlay != OverlayType.None)
            {
                ShowAsync(InitialOverlay, false).Forget();
            }
        }

        /// <summary>画面を1つ表示します（今の画面は閉じません。ダイアログを重ねるときなど）。</summary>
        public async UniTask ShowAsync(OverlayType type, bool animate = true, CancellationToken ct = default)
        {
            var overlay = Find(type);
            if (overlay == null) return;

            Current = type;
            using var linked = CreateLinked(ct);
            await overlay.ShowAsync(animate, linked?.Token ?? destroyCancellationToken);
        }

        /// <summary>画面を1つ非表示にします。</summary>
        public async UniTask HideAsync(OverlayType type, bool animate = true, CancellationToken ct = default)
        {
            var overlay = Find(type);
            if (overlay == null) return;

            if (Current == type) Current = OverlayType.None;
            using var linked = CreateLinked(ct);
            await overlay.HideAsync(animate, linked?.Token ?? destroyCancellationToken);
        }

        /// <summary>
        /// 今の画面を閉じてから次の画面を出します。
        /// 切り替え中に呼ばれた場合は無視して false を返します（連打対策）。
        /// </summary>
        public async UniTask<bool> SwitchAsync(OverlayType next, bool animate = true, CancellationToken ct = default)
        {
            if (IsSwitching) return false;

            IsSwitching = true;
            try
            {
                if (Current != OverlayType.None && Current != next)
                {
                    await HideAsync(Current, animate, ct);
                }
                await ShowAsync(next, animate, ct);
                return true;
            }
            finally
            {
                IsSwitching = false;
            }
        }

        private IOverlay Find(OverlayType type)
        {
            if (type == OverlayType.None) return null;

            var overlay = GetOverlay(type);
            if (overlay == null)
            {
                Debug.LogWarning($"{GetType().Name}: {type} の画面が登録されていません。", this);
            }
            return overlay;
        }

        /// <summary>
        /// 呼び出し元のトークンと、この Canvas の破棄をまとめた CTS を作ります。
        /// 呼び出し元がトークンを渡していなければ null（destroyCancellationToken だけを使う）。
        /// 使い終わったら Dispose してください。
        /// </summary>
        private CancellationTokenSource CreateLinked(CancellationToken ct)
        {
            return ct.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(ct, destroyCancellationToken)
                : null;
        }
    }
}
