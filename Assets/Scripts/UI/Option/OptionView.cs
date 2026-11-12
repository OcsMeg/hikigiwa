using System;
using System.Collections.Generic;
using Hikigiwa.Common.Audio;
using Hikigiwa.UI.Overlay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hikigiwa.UI.Option
{
    /// <summary>設定画面の見た目。音量のスライダーの行と、戻る・初期値に戻すボタンを持ちます。</summary>
    public class OptionView : OverlayViewBase
    {
        [Serializable]
        public sealed class VolumeRow
        {
            [SerializeField] private VolumeChannel channel;
            [SerializeField] private Slider slider;
            [Tooltip("「80%」のような数値の表示。なくてもよい。")]
            [SerializeField] private TMP_Text valueText;

            public VolumeChannel Channel => channel;
            public Slider Slider => slider;
            public TMP_Text ValueText => valueText;
        }

        [SerializeField] private VolumeRow[] volumeRows = Array.Empty<VolumeRow>();
        [SerializeField] private Button backButton;
        [SerializeField] private Button resetButton;

        public IReadOnlyList<VolumeRow> VolumeRows => volumeRows;
        public Button BackButton => backButton;
        public Button ResetButton => resetButton;

        /// <summary>スライダーと数値の表示を更新します（onValueChanged は発行しません）。</summary>
        public void SetVolume(VolumeRow row, float value)
        {
            if (row.Slider != null) row.Slider.SetValueWithoutNotify(value);
            if (row.ValueText != null) row.ValueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }
    }
}
