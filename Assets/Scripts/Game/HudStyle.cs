using Hikigiwa.CardSystem;
using UnityEngine;

namespace Hikigiwa.Game
{
    /// <summary>
    /// ソロ（ReadingHud）とCPU対戦（BattleHud）で共通に使う、UIの色・文字サイズ・文言。
    /// 見た目をそろえるため、どちらのHUDもここの値で部品を作る。
    /// </summary>
    public static class HudStyle
    {
        /// <summary>本文やボタンの背景に使う紙色。</summary>
        public static readonly Color Paper = new Color32(246, 229, 199, 255);
        /// <summary>見出し・強調・主な操作ボタンに使う金色。</summary>
        public static readonly Color Gold = new Color32(236, 196, 112, 255);
        /// <summary>凶・破綻・使えない理由に使う赤。</summary>
        public static readonly Color Red = new Color32(236, 98, 98, 255);
        /// <summary>達成を示す緑。</summary>
        public static readonly Color Green = new Color32(140, 214, 150, 255);
        /// <summary>補足情報に使う薄い色。</summary>
        public static readonly Color Dim = new Color32(170, 160, 185, 255);
        /// <summary>情報パネルの半透明の背景。</summary>
        public static readonly Color PanelColor = new Color32(14, 10, 24, 215);
        /// <summary>結果パネルの背景。後ろが透けると読みにくいので濃くする。</summary>
        public static readonly Color ResultPanelColor = new Color32(14, 10, 24, 248);
        /// <summary>道具パネル内のタイルの背景。</summary>
        public static readonly Color TileColor = new Color32(36, 27, 54, 240);
        /// <summary>ボタンの文字色。</summary>
        public static readonly Color ButtonText = new Color32(25, 20, 34, 255);
        /// <summary>道具パネルを開いている間、後ろを暗くする暗幕の色。</summary>
        public static readonly Color OverlayColor = new Color(0, 0, 0, 0.5f);

        /// <summary>鑑定を終える／次へ／タイトルに戻るボタンのアンカー（画面右下）。</summary>
        public static readonly Vector2 MainButtonAnchor = new Vector2(1, 0);
        /// <summary>鑑定を終える／次へボタンの位置（同じ場所に入れ替わりで出る）。下端は左下の占い道具ボタンとそろえる。</summary>
        public static readonly Vector2 MainButtonPosition = new Vector2(-32, 32);
        /// <summary>鑑定を終える／次へボタンの大きさ。</summary>
        public static readonly Vector2 MainButtonSize = new Vector2(340, 130);
        /// <summary>占い道具ボタンのアンカー（画面左下）。</summary>
        public static readonly Vector2 ToolsButtonAnchor = new Vector2(0, 0);
        /// <summary>占い道具ボタンの位置。画面端からの余白は鑑定を終えるボタンと同じ。</summary>
        public static readonly Vector2 ToolsButtonPosition = new Vector2(32, 32);
        /// <summary>占い道具ボタンの大きさ。鑑定を終えるボタンと同じ。</summary>
        public static readonly Vector2 ToolsButtonSize = new Vector2(340, 130);
        /// <summary>タイトルに戻るボタンの位置（次へボタンの上）。</summary>
        public static readonly Vector2 TitleButtonPosition = new Vector2(-32, 178);
        /// <summary>タイトルに戻るボタンの大きさ。</summary>
        public static readonly Vector2 TitleButtonSize = new Vector2(340, 110);

        /// <summary>道具パネルを閉じるボタンの文言。どちらのモードも Esc と T で閉じられる。</summary>
        public const string CloseToolsCaption = "Close (Esc / T)";

        /// <summary>ボタンの文字サイズ。背の高いボタン（2行表示など）は大きくする。</summary>
        public static int ButtonFontSize(Vector2 size) => size.y >= 120 ? 34 : 30;

        /// <summary>占い道具の名前。</summary>
        public static string NameOf(FortuneTool tool)
        {
            switch (tool)
            {
                case FortuneTool.Crystal: return "Crystal Ball";
                case FortuneTool.Tarot: return "Tarot";
                case FortuneTool.Incense: return "Incense";
                default: return "Palm Reading";
            }
        }

        /// <summary>占い道具を使えない理由の文言。</summary>
        public static string ReasonOf(ToolBlockReason reason)
        {
            switch (reason)
            {
                case ToolBlockReason.NotEnoughSpirit: return "Not enough SP";
                case ToolBlockReason.DeckTooSmall: return "Not enough cards";
                case ToolBlockReason.NoBadCardLeft: return "No unlucky cards";
                case ToolBlockReason.LimitReached: return "Already used";
                case ToolBlockReason.NotReading: return "Can't use now";
                default: return "";
            }
        }
    }
}
