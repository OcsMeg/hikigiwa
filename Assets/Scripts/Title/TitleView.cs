using Hikigiwa.UI.Overlay;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Hikigiwa.Title
{
    /// <summary>タイトル画面の見た目。ボタンの参照を持つだけです。</summary>
    public sealed class TitleView : OverlayViewBase
    {
        // 以前の「Start」ボタンを Solo として引き継ぐ（シーン上の参照を保つ）
        [FormerlySerializedAs("startButton")]
        [SerializeField] private Button soloButton;
        [SerializeField] private Button cpuButton;
        [SerializeField] private Button optionButton;
        [SerializeField] private Button rulesButton;

        public Button SoloButton => soloButton;
        public Button CpuButton => cpuButton;
        public Button OptionButton => optionButton;
        public Button RulesButton => rulesButton;
    }
}
