using Cysharp.Threading.Tasks;
using Hikigiwa.Common;
using Hikigiwa.UI;
using Hikigiwa.UI.Overlay;
using R3;
using UnityEngine;

namespace Hikigiwa.Title
{
    /// <summary>タイトル画面。「はじめる」でゲームのシーンへ、「設定」で設定画面へ進みます。</summary>
    public sealed class TitlePresenter : OverlayPresenterBase<TitleView>
    {
        [SerializeField] private string gameSceneName = "Game";

        protected override void SetEvent()
        {
            View.StartButton.OnClickAsObservable()
                .ThrottleFirst(UiConstants.ButtonThrottle)
                .Subscribe(_ => StartGameAsync().Forget())
                .AddTo(this);

            View.OptionButton.OnClickAsObservable()
                .ThrottleFirst(UiConstants.ButtonThrottle)
                .Subscribe(_ => TitleCanvas.Instance.SwitchAsync(OverlayType.Option).Forget())
                .AddTo(this);
        }

        private async UniTaskVoid StartGameAsync()
        {
            // 遷移が始まったら、もうボタンを押せないようにする
            View.SetInteractive(false);

            bool loaded = await SceneTransition.Instance.LoadAsync(gameSceneName);

            // 失敗したとき（シーン未登録など）は操作を戻す。成功したらこのオブジェクトはもう破棄されている
            if (!loaded && this != null && IsVisible) View.SetInteractive(true);
        }
    }
}
