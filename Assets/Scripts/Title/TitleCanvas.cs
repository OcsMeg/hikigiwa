using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Hikigiwa.UI;
using Hikigiwa.UI.Credits;
using Hikigiwa.UI.Option;
using Hikigiwa.UI.Overlay;
using Hikigiwa.UI.Rules;
using R3;
using UnityEngine;

namespace Hikigiwa.Title
{
    /// <summary>タイトルシーンの画面をまとめて持ち、切り替えます。最初はタイトル画面を出します。</summary>
    public sealed class TitleCanvas : CanvasBase<TitleCanvas>
    {
        [SerializeField] private TitlePresenter title;
        [SerializeField] private OptionPresenter option;
        [SerializeField] private RulesPresenter rules;
        [SerializeField] private CreditsPresenter credits;

        protected override OverlayType InitialOverlay => OverlayType.Title;

        protected override IEnumerable<IOverlay> AllOverlays
        {
            get
            {
                yield return title;
                yield return option;
                yield return rules;
                yield return credits;
            }
        }

        protected override IOverlay GetOverlay(OverlayType type) => type switch
        {
            OverlayType.Title => title,
            OverlayType.Option => option,
            OverlayType.Rules => rules,
            OverlayType.Credits => credits,
            _ => null,
        };

        protected override void Initialize()
        {
            base.Initialize();

            // 設定・ルール・クレジットの各画面は、閉じたらタイトルへ戻る（各画面はどこに戻るかを知らない）
            if (option != null) ReturnToTitleOn(option.CloseRequested);
            if (rules != null) ReturnToTitleOn(rules.CloseRequested);
            if (credits != null) ReturnToTitleOn(credits.CloseRequested);
        }

        private void ReturnToTitleOn(Observable<Unit> closeRequested)
        {
            closeRequested
                .Subscribe(_ => SwitchAsync(OverlayType.Title).Forget())
                .AddTo(this);
        }
    }
}
