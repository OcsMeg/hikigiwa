using Cysharp.Threading.Tasks;
using Hikigiwa.Common;
using Hikigiwa.UI;
using Hikigiwa.UI.Overlay;
using R3;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Hikigiwa.Title
{
    /// <summary>
    /// タイトル画面。「Solo」で1人用のシーンへ、「CPU」でCPU対戦のシーンへ、
    /// 「Options」で設定画面へ、「Rules」でルール説明へ、「Credits」でクレジットへ進みます。
    /// </summary>
    public sealed class TitlePresenter : OverlayPresenterBase<TitleView>
    {
        // 以前の gameSceneName を引き継ぐ（シーン上で設定した値を保つ）
        [FormerlySerializedAs("gameSceneName")]
        [SerializeField] private string soloSceneName = "Game";
        [SerializeField] private string cpuSceneName = "CPUBattle";

        protected override void SetEvent()
        {
            BindSceneButton(View.SoloButton, soloSceneName);
            BindSceneButton(View.CpuButton, cpuSceneName);

            View.OptionButton.OnClickAsObservable()
                .ThrottleFirst(UiConstants.ButtonThrottle)
                .Subscribe(_ => TitleCanvas.Instance.SwitchAsync(OverlayType.Option).Forget())
                .AddTo(this);

            // ルール画面・クレジット画面をまだシーンに作っていなくても動くよう、ボタンがあるときだけつなぐ
            BindOverlayButton(View.RulesButton, OverlayType.Rules);
            BindOverlayButton(View.CreditsButton, OverlayType.Credits);
        }

        /// <summary>押すと type の画面へ切り替えるボタンにします。ボタンがシーンになければ何もしません。</summary>
        private void BindOverlayButton(Button button, OverlayType type)
        {
            if (button == null) return;

            button.OnClickAsObservable()
                .ThrottleFirst(UiConstants.ButtonThrottle)
                .Subscribe(_ => TitleCanvas.Instance.SwitchAsync(type).Forget())
                .AddTo(this);
        }

        /// <summary>押すと sceneName のシーンへ移るボタンにします。ボタンがシーンになければ何もしません。</summary>
        private void BindSceneButton(Button button, string sceneName)
        {
            if (button == null) return;

            button.OnClickAsObservable()
                .ThrottleFirst(UiConstants.ButtonThrottle)
                .Subscribe(_ => LoadSceneAsync(sceneName).Forget())
                .AddTo(this);
        }

        private async UniTaskVoid LoadSceneAsync(string sceneName)
        {
            // 遷移が始まったら、もうボタンを押せないようにする
            View.SetInteractive(false);

            bool loaded = await SceneTransition.Instance.LoadAsync(sceneName);

            // 失敗したとき（シーン未登録など）は操作を戻す。成功したらこのオブジェクトはもう破棄されている
            if (!loaded && this != null && IsVisible) View.SetInteractive(true);
        }
    }
}
