using System;

namespace Hikigiwa.UI
{
    /// <summary>UI 全体で使う定数。</summary>
    public static class UiConstants
    {
        /// <summary>表示中の CanvasGroup.alpha。</summary>
        public const float ShownAlpha = 1f;

        /// <summary>非表示の CanvasGroup.alpha。</summary>
        public const float HiddenAlpha = 0f;

        /// <summary>画面のフェードにかける標準の秒数。</summary>
        public const float DefaultFadeDuration = 0.25f;

        /// <summary>ボタンの連打防止（ThrottleFirst）に使う標準の間隔。</summary>
        public static readonly TimeSpan ButtonThrottle = TimeSpan.FromMilliseconds(500);
    }
}
