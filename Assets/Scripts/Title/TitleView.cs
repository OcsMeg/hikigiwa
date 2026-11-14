using Hikigiwa.UI.Overlay;
using UnityEngine;
using UnityEngine.UI;

namespace Hikigiwa.Title
{
    /// <summary>タイトル画面の見た目。ボタンの参照を持つだけです。</summary>
    public sealed class TitleView : OverlayViewBase
    {
        [SerializeField] private Button startButton;
        [SerializeField] private Button optionButton;
        [SerializeField] private Button rulesButton;

        public Button StartButton => startButton;
        public Button OptionButton => optionButton;
        public Button RulesButton => rulesButton;
    }
}
