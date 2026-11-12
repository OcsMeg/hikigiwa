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

        public Button StartButton => startButton;
        public Button OptionButton => optionButton;
    }
}
