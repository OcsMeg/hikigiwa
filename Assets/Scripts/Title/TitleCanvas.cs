using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Hikigiwa.UI;
using Hikigiwa.UI.Option;
using Hikigiwa.UI.Overlay;
using R3;
using UnityEngine;

namespace Hikigiwa.Title
{
    /// <summary>タイトルシーンの画面をまとめて持ち、切り替えます。最初はタイトル画面を出します。</summary>
    public sealed class TitleCanvas : CanvasBase<TitleCanvas>
    {
        [SerializeField] private TitlePresenter title;
        [SerializeField] private OptionPresenter option;

        protected override OverlayType InitialOverlay => OverlayType.Title;

        protected override IEnumerable<IOverlay> AllOverlays
        {
            get
            {
                yield return title;
                yield return option;
            }
        }

        protected override IOverlay GetOverlay(OverlayType type) => type switch
        {
            OverlayType.Title => title,
            OverlayType.Option => option,
            _ => null,
        };

        protected override void Initialize()
        {
            base.Initialize();

            // 設定画面を閉じたらタイトルへ戻る（設定画面はどこに戻るかを知らない）
            if (option != null)
            {
                option.CloseRequested
                    .Subscribe(_ => SwitchAsync(OverlayType.Title).Forget())
                    .AddTo(this);
            }
        }
    }
}
