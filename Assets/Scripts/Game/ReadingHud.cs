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
    /// 画面のUI。
    /// 左上：夜・ノルマ・スコア・霊力／右上：山札／右下：鑑定を終える／中央：結果／
    /// 下：運気と凶の印／左下：占い道具のボタンと占いの結果
    /// メニュー「Hikigiwa/UIをシーンに配置」でシーンに置いておくと、それをそのまま使います（位置や見た目をエディタで変えられる）。
    /// 置かれていなければ、プレイ開始時にコードで組み立てます。
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
        public event Action TitleClicked;
        public event Action<FortuneTool> ToolClicked;

        /// <summary>占い道具のパネルが開いているか。</summary>
        public bool IsToolPanelOpen => toolPanel != null && toolPanel.activeSelf;

        // 色はCPU対戦と共通（HudStyle）
        private static readonly Color Paper = HudStyle.Paper;
        private static readonly Color Gold = HudStyle.Gold;
        private static readonly Color Red = HudStyle.Red;
        private static readonly Color Green = HudStyle.Green;
        private static readonly Color Dim = HudStyle.Dim;

        // 道具の並び順はCPU対戦と共通（ToolPanelView）
        private static FortuneTool[] Tools => ToolPanelView.Tools;

        // シーンに置いたUIの各部品。「UIをシーンに配置」で自動で割り当てる。部品を消すと動かなくなるので、移動・変形・非表示にとどめる
        [Header("UIの部品（自動で割り当て）")]
        [SerializeField] private GameObject canvasRoot;
        [SerializeField] private Text nightText;
        [SerializeField] private Text quotaText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text spiritText;
        [SerializeField] private Text deckText;
        // 名前は「山札の内訳」のままだが、中身はこの客の吉数・凶数（名前を変えるとシーンの割り当てが外れるため据え置き）
        [SerializeField] private Text deckDetailText;
        [SerializeField] private Text luckText;
        [SerializeField] private Text marksText;
        [SerializeField] private Text messageText;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Text resultTitle;
        [SerializeField] private Text resultBody;
        [SerializeField] private Button stopButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Text nextLabel;
        [SerializeField] private Button toolsButton;
        [SerializeField] private GameObject memoPanel;
        [SerializeField] private Text memoText;
        [SerializeField] private GameObject toolPanel;
        [SerializeField] private Text toolPanelSpirit;
        [SerializeField] private Button closeToolButton;
        [SerializeField] private Button titleButton;
        // Tools と同じ順（水晶・タロット・お香・手相）
        [SerializeField] private ToolTile[] toolTiles = new ToolTile[0];
        private bool wired;

        private static T Named<T>(T component, string name) where T : Component => HudFactory.Named(component, name);

        /// <summary>プレイ開始時に呼びます。シーンにUIが無ければ作り、ボタンの処理をつなぎます。</summary>
        public void Build()
        {
            if (wired) return;
            if (canvasRoot == null) CreateLayout();
            else CreateMissingParts();
            Wire();
        }

        /// <summary>UIがシーンに置かれているか。</summary>
        public bool HasLayout => canvasRoot != null;

        /// <summary>
        /// UIの部品を作り、参照を割り当てます。ボタンの処理はつながないので、エディタ（UIをシーンに配置）からも呼べます。
        /// </summary>
        public void CreateLayout()
        {
            var canvasObject = new GameObject("HudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasRoot = canvasObject;
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasObject.transform;

            // 左上：夜・ノルマ・スコア・霊力
            RectTransform status = Panel(root, "StatusPanel", new Vector2(0, 1), new Vector2(32, -32), new Vector2(500, 228));
            nightText = Named(Label(status, "Night 1  —  Client 1 / 3", 40, Gold, new Vector2(24, -14), new Vector2(460, 52)), "NightLabel");
            quotaText = Named(Label(status, "Quota  0 / 180", 30, Paper, new Vector2(24, -74), new Vector2(460, 44)), "QuotaLabel");
            scoreText = Named(Label(status, "Total Score  0", 30, Paper, new Vector2(24, -122), new Vector2(460, 44)), "ScoreLabel");
            spiritText = Named(Label(status, "Spirit Power  3", 30, Gold, new Vector2(24, -170), new Vector2(460, 44)), "SpiritLabel");

            // 右上：山札
            RectTransform deck = Panel(root, "DeckPanel", new Vector2(1, 1), new Vector2(-32, -32), new Vector2(640, 120));
            deckText = Named(Label(deck, "Deck: 19 left", 36, Paper, new Vector2(-24, -14), new Vector2(600, 52),
                TextAnchor.UpperRight), "RemainingLabel");
            deckDetailText = Named(Label(deck, "Lucky Number 1    Unlucky Number 4 + Joker", 26, Paper, new Vector2(-24, -68), new Vector2(600, 40),
                TextAnchor.UpperRight), "BreakdownLabel");

            // 下：運気と凶の印
            RectTransform bottom = Panel(root, "LuckPanel", new Vector2(0.5f, 0), new Vector2(0, 32), new Vector2(900, 170));
            messageText = Named(Label(bottom, "Click the deck (or press Space) to draw a card", 28, Paper, new Vector2(0, -12),
                new Vector2(860, 40), TextAnchor.MiddleCenter), "MessageLabel");
            luckText = Named(Label(bottom, "Luck 0  ×  Multiplier 1  =  0", 54, Gold, new Vector2(0, -58), new Vector2(860, 64),
                TextAnchor.MiddleCenter), "LuckLabel");
            marksText = Named(Label(bottom, "Omen Marks  0 / 2", 32, Red, new Vector2(0, -122), new Vector2(860, 40),
                TextAnchor.MiddleCenter), "MarksLabel");

            // 右下：鑑定を終える／次へ
            // 位置と大きさはCPU対戦と共通（HudStyle）
            stopButton = Named(MakeButton(root, "End Reading\n(Enter)", Paper, HudStyle.MainButtonAnchor,
                HudStyle.MainButtonPosition, HudStyle.MainButtonSize, out _), "StopButton");
            nextButton = Named(MakeButton(root, "Next Client\n(Enter)", Gold, HudStyle.MainButtonAnchor,
                HudStyle.MainButtonPosition, HudStyle.MainButtonSize, out nextLabel), "NextButton");
            nextButton.gameObject.SetActive(false);

            // 左下：占い道具と占いの結果
            toolsButton = Named(MakeButton(root, "Fortune Tools\n(T)", Gold, HudStyle.ToolsButtonAnchor, HudStyle.ToolsButtonPosition,
                HudStyle.ToolsButtonSize, out _), "ToolsButton");
            // 占い道具ボタンのすぐ上（20pxあける）
            float memoY = HudStyle.ToolsButtonPosition.y + HudStyle.ToolsButtonSize.y + 20;
            RectTransform memo = Panel(root, "MemoPanel", new Vector2(0, 0), new Vector2(32, memoY), new Vector2(500, 230));
            memoPanel = memo.gameObject;
            Named(Label(memo, "Reading Notes", 24, Dim, new Vector2(20, -12), new Vector2(460, 34)), "Header");
            memoText = Named(Label(memo, "", 26, Paper, new Vector2(20, -52), new Vector2(460, 170)), "BodyLabel");
            memoText.horizontalOverflow = HorizontalWrapMode.Wrap;
            memoText.lineSpacing = 1.1f;
            memoPanel.SetActive(false);

            // 中央：結果
            RectTransform result = Panel(root, "ResultPanel", new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(660, 460));
            result.GetComponent<Image>().color = HudStyle.ResultPanelColor;
            resultPanel = result.gameObject;
            resultTitle = Named(Label(result, "Reading Complete", 50, Gold, new Vector2(32, -24), new Vector2(600, 66)), "Header");
            resultBody = Named(Label(result, "", 30, Paper, new Vector2(32, -104), new Vector2(600, 340)), "BodyLabel");
            resultBody.lineSpacing = 1.15f;
            resultPanel.SetActive(false);

            BuildToolPanel(root);
            CreateMissingParts();
            // 道具パネルの暗幕より手前に出し、開いている間も押して閉じられるようにする
            toolsButton.transform.SetAsLastSibling();
        }

        /// <summary>占い道具パネルを作ります。見た目はCPU対戦と共通（ToolPanelView）。</summary>
        private void BuildToolPanel(Transform root)
        {
            toolPanel = ToolPanelView.Build(root, font, out toolPanelSpirit, out closeToolButton, out toolTiles);
        }

        /// <summary>
        /// 後から追加した部品が、シーンに置いたUIに無ければ作ります（ボタンの見た目の動き・効果音も付けます）。変えたときは true を返します。
        /// </summary>
        public bool CreateMissingParts()
        {
            bool created = false;
            if (titleButton == null)
            {
                // ゲームを最後まで遊んだあとに「次へ（もう一度遊ぶ）」の上に出す
                titleButton = Named(MakeButton(canvasRoot.transform, "Back to Title\n(Esc)", Paper, HudStyle.MainButtonAnchor,
                    HudStyle.TitleButtonPosition, HudStyle.TitleButtonSize, out _), "TitleButton");
                // 道具パネルの暗幕より後ろ（手前に描かれない位置）に置く
                titleButton.transform.SetSiblingIndex(toolPanel.transform.GetSiblingIndex());
                titleButton.gameObject.SetActive(false);
                created = true;
            }
            // 以前にシーンへ置いたボタンには、見た目の動きと効果音が付いていないので付ける
            foreach (Button button in canvasRoot.GetComponentsInChildren<Button>(true))
                created |= HudFactory.AddButtonEffects(button);
            return created;
        }

        /// <summary>ボタンの処理をつなぎ、最初は隠しておくパネルを隠します（エディタで表示したまま保存しても大丈夫）。</summary>
        private void Wire()
        {
            wired = true;
            AddClick(stopButton, () => StopClicked?.Invoke());
            AddClick(nextButton, () => NextClicked?.Invoke());
            AddClick(toolsButton, ToggleToolPanel);
            AddClick(closeToolButton, CloseToolPanel);
            AddClick(titleButton, () => TitleClicked?.Invoke());
            for (int i = 0; i < Tools.Length; i++)
            {
                FortuneTool tool = Tools[i];
                AddClick(toolTiles[i].Button, () => ToolClicked?.Invoke(tool));
            }

            memoPanel.SetActive(false);
            resultPanel.SetActive(false);
            toolPanel.SetActive(false);
            titleButton.gameObject.SetActive(false);
        }

        private static void AddClick(Button button, Action onClick)
        {
            button.onClick.AddListener(() =>
            {
                // 選択状態が残るとSpace/Enterでボタンも押されてしまうので解除する
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                onClick();
            });
        }

        public void SetMode(Mode mode)
        {
            stopButton.gameObject.SetActive(mode != Mode.Finished);
            nextButton.gameObject.SetActive(mode == Mode.Finished);
            stopButton.interactable = mode == Mode.Reading;
            toolsButton.interactable = mode == Mode.Reading;
            if (mode != Mode.Reading) CloseToolPanel();
            if (mode != Mode.Finished) titleButton.gameObject.SetActive(false);
        }

        /// <summary>「タイトルに戻る」ボタンを出すか。ゲームを最後まで遊んだときだけ出します。</summary>
        public void SetTitleButtonVisible(bool visible) => titleButton.gameObject.SetActive(visible);

        public void SetNextLabel(string label) => nextLabel.text = $"{label}\n(Enter)";

        public void SetRecord(PlayRecord record)
        {
            nightText.text = $"Night {record.Night}  —  Client {Mathf.Min(record.CustomersThisNight + 1, record.CustomersPerNight)} / {record.CustomersPerNight}";
            bool reached = record.NightScore >= record.Quota;
            quotaText.text = $"Quota  {record.NightScore} / {record.Quota}" + (reached ? "  Cleared" : "");
            quotaText.color = reached ? Green : Paper;
            scoreText.text = $"Total Score  {record.TotalScore}";
            spiritText.text = $"Spirit Power  {record.Spirit}";
        }

        public void SetSession(ReadingSession session)
        {
            deckText.text = $"Deck: {session.Deck.Remaining} left";
            // この客の吉数・凶数（残り枚数ではなく数字そのもの）。山札の残り枚数は上の行に出す
            deckDetailText.text = HudStyle.NumbersText(session.Customer.LuckyNumber, session.Customer.BadNumbers);
            luckText.text = $"Luck {session.Luck}  ×  Multiplier {session.Multiplier}  =  {session.CurrentSales}";

            // ●○はYeseva Oneに無いので、数で表す
            marksText.text = $"Omen Marks  {session.BadMarks} / {session.BadMarksToBreak}";
        }

        /// <summary>道具の一覧の、コスト・使えるかどうかを更新します。</summary>
        public void SetTools(ReadingSession session, int spirit) =>
            ToolPanelView.Refresh(toolTiles, toolPanelSpirit, session, spirit, true);

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
                resultTitle.text = "Ruined…";
                resultTitle.color = Red;
                resultBody.text = "Two Omen Marks.\nNo earnings from this client.\nNo Spirit Power recovered.";
                return;
            }

            resultTitle.text = "Reading Complete";
            resultTitle.color = Gold;
            var body = new StringBuilder();
            body.AppendLine($"Luck {result.Luck} × Multiplier {result.Multiplier} = {result.BaseSales}");
            if (result.ForesightCard.HasValue)
            {
                Card top = result.ForesightCard.Value;
                string name = top.IsJoker ? "Joker" : top.Number.ToString();
                body.AppendLine(result.ForesightHit
                    ? $"Premonition hit! (Next: {name})  Earnings ×1.5"
                    : $"Premonition missed (Next: {name})");
            }
            body.AppendLine($"Earnings {result.Sales}");
            body.AppendLine($"Spirit Power +{result.SpiritGained}");
            body.Append($"Client Score {result.Score}");
            resultBody.text = body.ToString();
        }

        public void ShowNightSummary(NightSummary summary, PlayRecord record)
        {
            resultPanel.SetActive(true);
            var body = new StringBuilder();

            if (record.IsCleared)
            {
                // 最後の夜：夜ごとのスコアと3夜の合計
                resultTitle.text = "All Nights Complete";
                resultTitle.color = Gold;
                foreach (NightSummary night in record.NightHistory)
                    body.AppendLine($"Night {night.Night}  {night.Score}");
                body.AppendLine();
                body.AppendLine($"Ruined Readings  {record.BrokenCount}");
                body.Append($"Final Score  {record.TotalScore}");
                resultBody.text = body.ToString();
                return;
            }

            body.AppendLine($"Tonight's Score  {summary.Score}");
            body.AppendLine($"Quota  {summary.Quota}");
            if (summary.Passed) body.AppendLine($"Quota Bonus: Spirit Power +{summary.SpiritGained}");
            body.AppendLine();

            if (record.IsGameOver)
            {
                resultTitle.text = $"Night {summary.Night}  —  Quota Missed";
                resultTitle.color = Red;
                body.AppendLine("You had to close up shop…");
                body.Append($"Final Score  {record.TotalScore}");
            }
            else
            {
                resultTitle.text = $"Night {summary.Night}  —  " + (summary.Passed ? "Quota Met" : "Quota Missed");
                resultTitle.color = summary.Passed ? Green : Red;
                body.Append($"Total Score  {record.TotalScore}");
            }
            resultBody.text = body.ToString();
        }

        // 部品の作り方はCPU対戦と共通（HudFactory）
        private RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size) =>
            HudFactory.Panel(parent, name, anchor, position, size);

        private Text Label(Transform parent, string value, int size, Color color, Vector2 position, Vector2 box,
            TextAnchor alignment = TextAnchor.UpperLeft) =>
            HudFactory.Label(font, parent, value, size, color, position, box, alignment);

        private Button MakeButton(Transform parent, string caption, Color color, Vector2 anchor, Vector2 position,
            Vector2 size, out Text label) =>
            HudFactory.MakeButton(font, parent, caption, color, anchor, position, size, out label);
    }
}
