using System;
using System.Collections.Generic;
using System.Text;
using Hikigiwa.CardSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hikigiwa.Game
{
    /// <summary>
    /// 画面のUI。プレイ開始時にコードで組み立てます。
    /// 左上：夜・ノルマ・スコア・霊力／右上：山札／右：鑑定を終える／中央：結果／
    /// 下：運気と凶の印／左下：占い道具のボタンと占いの結果
    /// </summary>
    public sealed class ReadingHud : MonoBehaviour
    {
        public enum Mode
        {
            /// <summary>引く・終える・道具を使うを選べる。</summary>
            Reading,
            /// <summary>演出中で操作できない。</summary>
            Busy,
            /// <summary>鑑定や夜が終わり、次へ進むボタンだけを出す。</summary>
            Finished,
        }

        [SerializeField] private Font font;

        public event Action StopClicked;
        public event Action NextClicked;
        public event Action<FortuneTool> ToolClicked;

        /// <summary>占い道具のパネルが開いているか。</summary>
        public bool IsToolPanelOpen => toolPanel != null && toolPanel.activeSelf;

        private static readonly Color Paper = new Color32(246, 229, 199, 255);
        private static readonly Color Gold = new Color32(236, 196, 112, 255);
        private static readonly Color Red = new Color32(236, 98, 98, 255);
        private static readonly Color Green = new Color32(140, 214, 150, 255);
        private static readonly Color Dim = new Color32(170, 160, 185, 255);
        private static readonly Color PanelColor = new Color32(14, 10, 24, 215);
        private static readonly Color TileColor = new Color32(36, 27, 54, 240);
        private static readonly Color ButtonText = new Color32(25, 20, 34, 255);

        private static readonly FortuneTool[] Tools =
            { FortuneTool.Crystal, FortuneTool.Tarot, FortuneTool.Incense, FortuneTool.Palm };

        private Text nightText;
        private Text quotaText;
        private Text scoreText;
        private Text spiritText;
        private Text deckText;
        private Text deckDetailText;
        private Text luckText;
        private Text marksText;
        private Text messageText;
        private GameObject resultPanel;
        private Text resultTitle;
        private Text resultBody;
        private Button stopButton;
        private Button nextButton;
        private Text nextLabel;
        private Button toolsButton;
        private GameObject memoPanel;
        private Text memoText;
        private GameObject toolPanel;
        private Text toolPanelSpirit;
        private readonly Dictionary<FortuneTool, ToolTile> tiles = new Dictionary<FortuneTool, ToolTile>();
        private bool built;

        private sealed class ToolTile
        {
            public Button Button;
            public Text ButtonLabel;
            public Text Status;
            public Text Cost;
        }

        public static string NameOf(FortuneTool tool)
        {
            switch (tool)
            {
                case FortuneTool.Crystal: return "水晶";
                case FortuneTool.Tarot: return "タロット";
                case FortuneTool.Incense: return "お香";
                default: return "手相";
            }
        }

        private static string DescriptionOf(FortuneTool tool)
        {
            switch (tool)
            {
                case FortuneTool.Crystal: return "次のカードが凶かどうかが分かる";
                case FortuneTool.Tarot: return "次の3枚の数字が分かる\n（順番は分からない）";
                case FortuneTool.Incense: return "山札から凶札を1枚取り除く\n（1人に1回まで）";
                default: return "一番上のカードを一番下へ回す\n（中身は見えない）";
            }
        }

        private static string ReasonOf(ToolBlockReason reason)
        {
            switch (reason)
            {
                case ToolBlockReason.NotEnoughSpirit: return "霊力が足りない";
                case ToolBlockReason.DeckTooSmall: return "山札が足りない";
                case ToolBlockReason.NoBadCardLeft: return "凶札が残っていない";
                case ToolBlockReason.LimitReached: return "この客にはもう使えない";
                case ToolBlockReason.NotReading: return "今は使えない";
                default: return "";
            }
        }

        public void Build()
        {
            if (built) return;
            built = true;

            var canvasObject = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasObject.transform;

            // 左上：夜・ノルマ・スコア・霊力
            RectTransform status = Panel(root, "夜と記録", new Vector2(0, 1), new Vector2(32, -32), new Vector2(500, 228));
            nightText = Label(status, "", 40, Gold, new Vector2(24, -14), new Vector2(460, 52));
            quotaText = Label(status, "", 30, Paper, new Vector2(24, -74), new Vector2(460, 44));
            scoreText = Label(status, "", 30, Paper, new Vector2(24, -122), new Vector2(460, 44));
            spiritText = Label(status, "", 30, Gold, new Vector2(24, -170), new Vector2(460, 44));

            // 右上：山札
            RectTransform deck = Panel(root, "山札", new Vector2(1, 1), new Vector2(-32, -32), new Vector2(460, 120));
            deckText = Label(deck, "", 36, Paper, new Vector2(-24, -14), new Vector2(420, 52), TextAnchor.UpperRight);
            deckDetailText = Label(deck, "", 26, Dim, new Vector2(-24, -68), new Vector2(420, 40), TextAnchor.UpperRight);

            // 下：運気と凶の印
            RectTransform bottom = Panel(root, "運気", new Vector2(0.5f, 0), new Vector2(0, 32), new Vector2(900, 170));
            messageText = Label(bottom, "", 28, Paper, new Vector2(0, -12), new Vector2(860, 40), TextAnchor.MiddleCenter);
            luckText = Label(bottom, "", 54, Gold, new Vector2(0, -58), new Vector2(860, 64), TextAnchor.MiddleCenter);
            marksText = Label(bottom, "", 32, Red, new Vector2(0, -122), new Vector2(860, 40), TextAnchor.MiddleCenter);

            // 右：鑑定を終える／次へ
            stopButton = MakeButton(root, "鑑定を終える\n（Enter）", Paper, new Vector2(1, 0.5f), new Vector2(-40, 0),
                new Vector2(340, 130), () => StopClicked?.Invoke(), out _);
            nextButton = MakeButton(root, "次の客へ\n（Enter）", Gold, new Vector2(1, 0.5f), new Vector2(-40, 0),
                new Vector2(340, 130), () => NextClicked?.Invoke(), out nextLabel);

            // 左下：占い道具と占いの結果
            toolsButton = MakeButton(root, "占い道具（T）", Gold, new Vector2(0, 0), new Vector2(32, 32),
                new Vector2(320, 96), ToggleToolPanel, out _);
            RectTransform memo = Panel(root, "占いの結果", new Vector2(0, 0), new Vector2(32, 148), new Vector2(500, 230));
            memoPanel = memo.gameObject;
            Label(memo, "占いの結果", 24, Dim, new Vector2(20, -12), new Vector2(460, 34));
            memoText = Label(memo, "", 26, Paper, new Vector2(20, -52), new Vector2(460, 170));
            memoText.horizontalOverflow = HorizontalWrapMode.Wrap;
            memoText.lineSpacing = 1.1f;
            memoPanel.SetActive(false);

            // 中央：結果
            RectTransform result = Panel(root, "結果", new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(660, 460));
            resultPanel = result.gameObject;
            resultTitle = Label(result, "", 50, Gold, new Vector2(32, -24), new Vector2(600, 66));
            resultBody = Label(result, "", 30, Paper, new Vector2(32, -104), new Vector2(600, 340));
            resultBody.lineSpacing = 1.15f;
            resultPanel.SetActive(false);

            BuildToolPanel(root);
            // 道具パネルの暗幕より手前に出し、開いている間も押して閉じられるようにする
            toolsButton.transform.SetAsLastSibling();
        }

        private void BuildToolPanel(Transform root)
        {
            // 背景を薄く暗くし、後ろの山札をクリックできないようにする
            var blocker = new GameObject("占い道具", typeof(RectTransform)).GetComponent<RectTransform>();
            blocker.SetParent(root, false);
            blocker.anchorMin = Vector2.zero;
            blocker.anchorMax = Vector2.one;
            blocker.offsetMin = blocker.offsetMax = Vector2.zero;
            var dim = blocker.gameObject.AddComponent<Image>();
            dim.color = new Color(0, 0, 0, 0.35f);
            toolPanel = blocker.gameObject;

            RectTransform panel = Panel(blocker, "道具の一覧", new Vector2(0.5f, 0.5f), new Vector2(0, 40),
                new Vector2(1280, 560));
            Label(panel, "占い道具", 44, Gold, new Vector2(36, -24), new Vector2(600, 60));
            toolPanelSpirit = Label(panel, "", 34, Gold, new Vector2(-260, -30), new Vector2(400, 50), TextAnchor.UpperRight);
            MakeButton(panel, "閉じる（Esc）", Paper, new Vector2(1, 1), new Vector2(-28, -24), new Vector2(220, 64),
                CloseToolPanel, out _);

            for (int i = 0; i < Tools.Length; i++)
            {
                FortuneTool tool = Tools[i];
                var tileRect = new GameObject(NameOf(tool), typeof(RectTransform)).GetComponent<RectTransform>();
                tileRect.SetParent(panel, false);
                tileRect.anchorMin = tileRect.anchorMax = tileRect.pivot = new Vector2(0, 1);
                tileRect.anchoredPosition = new Vector2(36 + i * 306, -112);
                tileRect.sizeDelta = new Vector2(288, 410);
                tileRect.gameObject.AddComponent<Image>().color = TileColor;

                Label(tileRect, NameOf(tool), 42, Paper, new Vector2(0, -20), new Vector2(288, 56), TextAnchor.MiddleCenter);
                Text description = Label(tileRect, DescriptionOf(tool), 24, Dim, new Vector2(0, -142),
                    new Vector2(260, 120), TextAnchor.UpperCenter);
                description.horizontalOverflow = HorizontalWrapMode.Wrap;

                var tile = new ToolTile();
                tile.Cost = Label(tileRect, "", 30, Gold, new Vector2(0, -84), new Vector2(288, 44), TextAnchor.MiddleCenter);
                tile.Status = Label(tileRect, "", 22, Red, new Vector2(0, -268), new Vector2(260, 34), TextAnchor.MiddleCenter);
                tile.Button = MakeButton(tileRect, "使う", Gold, new Vector2(0.5f, 0), new Vector2(0, 20),
                    new Vector2(240, 80), () => ToolClicked?.Invoke(tool), out tile.ButtonLabel);
                tiles[tool] = tile;
            }

            toolPanel.SetActive(false);
        }

        public void SetMode(Mode mode)
        {
            stopButton.gameObject.SetActive(mode != Mode.Finished);
            nextButton.gameObject.SetActive(mode == Mode.Finished);
            stopButton.interactable = mode == Mode.Reading;
            toolsButton.interactable = mode == Mode.Reading;
            if (mode != Mode.Reading) CloseToolPanel();
        }

        public void SetNextLabel(string label) => nextLabel.text = $"{label}\n（Enter）";

        public void SetRecord(PlayRecord record)
        {
            nightText.text = $"第{record.Night}夜　{Mathf.Min(record.CustomersThisNight + 1, record.CustomersPerNight)} / {record.CustomersPerNight} 人目";
            bool reached = record.NightScore >= record.Quota;
            quotaText.text = $"ノルマ　{record.NightScore} / {record.Quota}" + (reached ? "　達成" : "");
            quotaText.color = reached ? Green : Paper;
            scoreText.text = $"合計スコア　{record.TotalScore}";
            spiritText.text = $"霊力　{record.Spirit}";
        }

        public void SetSession(ReadingSession session)
        {
            deckText.text = $"山札 残り {session.Deck.Remaining} 枚";
            deckDetailText.text =
                $"うち 凶 {session.CountRemaining(CardOmen.Bad)}枚・吉 {session.CountRemaining(CardOmen.Lucky)}枚";
            luckText.text = $"運気 {session.Luck}　×　倍率 {session.Multiplier}　＝　{session.CurrentSales}";

            var marks = new StringBuilder("凶の印　");
            for (int i = 0; i < session.BadMarksToBreak; i++)
                marks.Append(i < session.BadMarks ? "●" : "○");
            marksText.text = marks.ToString();
        }

        /// <summary>道具の一覧の、コスト・使えるかどうかを更新します。</summary>
        public void SetTools(ReadingSession session, int spirit)
        {
            toolPanelSpirit.text = $"霊力　{spirit}";
            foreach (FortuneTool tool in Tools)
            {
                ToolTile tile = tiles[tool];
                tile.Cost.text = $"霊力 {session.CostOf(tool)}";
                ToolBlockReason reason = session.CheckTool(tool, spirit);
                tile.Button.interactable = reason == ToolBlockReason.None;
                tile.ButtonLabel.text = reason == ToolBlockReason.None ? "使う" : "使えない";
                tile.Status.text = ReasonOf(reason);
            }
        }

        public void SetMessage(string message) => messageText.text = message;

        /// <summary>占いの結果を表示します。行がなければパネルを隠します。</summary>
        public void SetMemo(IReadOnlyList<string> lines)
        {
            memoText.text = string.Join("\n", lines);
            memoPanel.SetActive(lines.Count > 0);
        }

        public void ToggleToolPanel()
        {
            if (IsToolPanelOpen) CloseToolPanel();
            else if (toolsButton.interactable) toolPanel.SetActive(true);
        }

        public void CloseToolPanel()
        {
            if (toolPanel != null) toolPanel.SetActive(false);
        }

        public void HideResult() => resultPanel.SetActive(false);

        public void ShowResult(ReadingResult result)
        {
            resultPanel.SetActive(true);
            if (result.IsBroken)
            {
                resultTitle.text = "破綻……";
                resultTitle.color = Red;
                resultBody.text = "凶の印が2つそろいました。\nこの客の売上は 0 です。\n霊力は回復しません。";
                return;
            }

            resultTitle.text = "鑑定成功";
            resultTitle.color = Gold;
            var body = new StringBuilder();
            body.AppendLine($"運気 {result.Luck} × 倍率 {result.Multiplier} ＝ {result.BaseSales}");
            if (result.ForesightCard.HasValue)
            {
                Card top = result.ForesightCard.Value;
                string name = top.IsJoker ? "ジョーカー" : top.Number.ToString();
                body.AppendLine(result.ForesightHit
                    ? $"予感的中！（次は {name}）　売上 ×1.5"
                    : $"予感は外れ（次は {name}）");
            }
            body.AppendLine($"売上 {result.Sales}");
            body.AppendLine(result.SatisfactionReached ? $"満足ライン達成　チップ +{result.Tip}" : "満足ライン未達　チップなし");
            body.AppendLine($"霊力 +{result.SpiritGained}");
            body.Append($"この客のスコア {result.Score}");
            resultBody.text = body.ToString();
        }

        public void ShowNightSummary(NightSummary summary, PlayRecord record)
        {
            resultPanel.SetActive(true);
            var body = new StringBuilder();

            if (record.IsCleared)
            {
                // 最後の夜：夜ごとのスコアと3夜の合計
                resultTitle.text = "すべての夜を終えた";
                resultTitle.color = Gold;
                foreach (NightSummary night in record.NightHistory)
                    body.AppendLine($"第{night.Night}夜　{night.Score}" + (night.Passed ? "　ノルマ達成" : "　ノルマ未達"));
                body.AppendLine();
                body.AppendLine($"破綻した客　{record.BrokenCount}人");
                body.Append($"最終スコア　{record.TotalScore}");
                resultBody.text = body.ToString();
                return;
            }

            body.AppendLine($"今夜のスコア　{summary.Score}");
            body.AppendLine($"ノルマ　{summary.Quota}");
            body.AppendLine();

            if (record.IsGameOver)
            {
                resultTitle.text = $"第{summary.Night}夜　ノルマ未達";
                resultTitle.color = Red;
                body.AppendLine("店をたたむことになった……");
                body.Append($"最終スコア　{record.TotalScore}");
            }
            else
            {
                resultTitle.text = $"第{summary.Night}夜　" + (summary.Passed ? "ノルマ達成" : "ノルマ未達");
                resultTitle.color = summary.Passed ? Green : Red;
                body.Append($"合計スコア　{record.TotalScore}");
            }
            resultBody.text = body.ToString();
        }

        private RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = PanelColor;
            image.raycastTarget = false;
            return rect;
        }

        /// <summary>
        /// 親の上端を基準に置くラベル。左寄せは左上、右寄せは右上、中央寄せは上中央が基準。
        /// </summary>
        private Text Label(Transform parent, string value, int size, Color color, Vector2 position, Vector2 box,
            TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var rect = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Vector2 corner;
            switch (alignment)
            {
                case TextAnchor.UpperRight:
                case TextAnchor.MiddleRight:
                    corner = new Vector2(1, 1);
                    break;
                case TextAnchor.UpperCenter:
                case TextAnchor.MiddleCenter:
                    corner = new Vector2(0.5f, 1);
                    break;
                default:
                    corner = new Vector2(0, 1);
                    break;
            }
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.anchoredPosition = position;
            rect.sizeDelta = box;
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.text = value;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private Button MakeButton(Transform parent, string caption, Color color, Vector2 anchor, Vector2 position,
            Vector2 size, Action onClick, out Text label)
        {
            var rect = new GameObject(caption, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() =>
            {
                // 選択状態が残るとSpace/Enterでボタンも押されてしまうので解除する
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                onClick();
            });

            label = Label(rect, caption, size.y >= 120 ? 34 : 30, ButtonText, Vector2.zero, size, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return button;
        }
    }
}
