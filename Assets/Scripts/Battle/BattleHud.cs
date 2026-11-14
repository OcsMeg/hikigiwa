using System;
using System.Text;
using DG.Tweening;
using Hikigiwa.CardSystem;
using Hikigiwa.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hikigiwa.Battle
{
    /// <summary>
    /// 対戦専用のHUD。状態を表示し、操作をイベントで通知する。
    /// メニュー「Hikigiwa/UIをシーンに配置」でシーンに置いておくと、それをそのまま使う（位置や見た目をエディタで変えられる）。
    /// 置かれていなければ、プレイ開始時にコードで組み立てる。
    /// </summary>
    public sealed class BattleHud : MonoBehaviour
    {
        /// <summary>HUD内の日本語表示に共通で使うフォント。シーン生成時に割り当てる。</summary>
        [SerializeField] private Font font;
        /// <summary>鑑定を終えるボタンを押した通知。得点の精算は進行側へ任せる。</summary>
        public event Action StopClicked;
        /// <summary>次ラウンドまたは再戦ボタンを押した通知。</summary>
        public event Action NextClicked;
        /// <summary>対戦終了後にタイトルへ戻るボタンを押した通知。</summary>
        public event Action TitleClicked;
        /// <summary>選択した道具を引数として通知するイベント。</summary>
        public event Action<FortuneTool> ToolClicked;
        // 以下はシーンに置いたUIの各部品。「UIをシーンに配置」で自動で割り当てる。部品を消すと動かなくなるので、移動・変形・非表示にとどめる。
        /// <summary>HUD全体のCanvas。シーンにUIが置かれているかの判定にも使う。</summary>
        [Header("UIの部品（自動で割り当て）")]
        [SerializeField] private GameObject canvasRoot;
        /// <summary>現在のラウンド番号と共有山札の残数を表示する上部ラベル。</summary>
        [SerializeField] private Text heading;
        /// <summary>共通の吉数・凶数2種類を表示するラベル。</summary>
        [SerializeField] private Text conditions;
        /// <summary>プレイヤーの手番状態・累計・霊力・鑑定状態を表示するラベル。</summary>
        [SerializeField] private Text player;
        /// <summary>CPUの手番状態・累計・霊力・鑑定状態を表示するラベル。</summary>
        [SerializeField] private Text cpu;
        /// <summary>今起きた行動や次の操作を案内するメッセージ。</summary>
        [SerializeField] private Text message;
        /// <summary>道具で得た情報を表示するメモ。情報が古くなると進行側から消される。</summary>
        [SerializeField] private Text memo;
        /// <summary>中央の結果パネルに表示する二人分の精算と最終勝敗。</summary>
        [SerializeField] private Text summary;
        /// <summary>停止ボタン。ラウンドの途中だけ表示する。</summary>
        [SerializeField] private Button stop;
        /// <summary>道具パネルを開閉するボタン。操作不可の手番は無効にする。</summary>
        [SerializeField] private Button tools;
        /// <summary>ラウンド終了後に次へ進むボタン。最終ラウンド後は再戦に使う。</summary>
        [SerializeField] private Button next;
        /// <summary>次へボタンの文言。次ラウンドと再戦で切り替える。</summary>
        [SerializeField] private Text nextLabel;
        /// <summary>背景のクリックを遮断する暗幕と道具一覧の親オブジェクト。</summary>
        [SerializeField] private GameObject toolPanel;
        /// <summary>ラウンド結果表示の親。通常の鑑定中は非表示にする。</summary>
        [SerializeField] private GameObject resultPanel;
        /// <summary>道具パネル内の霊力表示。</summary>
        [SerializeField] private Text toolPanelSpirit;
        /// <summary>4種類の道具のタイル（ToolPanelView.Tools と同じ順：水晶・タロット・お香・手相）。</summary>
        [SerializeField] private ToolTile[] toolTiles = new ToolTile[0];
        /// <summary>道具パネルを閉じるボタン。</summary>
        [SerializeField] private Button closeToolsButton;
        /// <summary>対戦終了後にだけ表示する、タイトルへ戻るボタン。</summary>
        [SerializeField] private Button titleButton;
        /// <summary>手番が替わったときに画面中央に出す帯。</summary>
        [SerializeField] private RectTransform turnBanner;
        /// <summary>帯の文字（「あなたの番」など）。</summary>
        [SerializeField] private Text turnBannerLabel;

        [Header("手番の帯の演出")]
        /// <summary>帯がX軸に回転して開く（閉じる）のにかける秒数。</summary>
        [SerializeField, Min(0f), Tooltip("帯が回転して開く・閉じるのにかける秒数")] private float bannerFlipSeconds = 0.25f;
        /// <summary>帯を開いたまま見せておく秒数。</summary>
        [SerializeField, Min(0f), Tooltip("帯を開いたまま見せておく秒数")] private float bannerHoldSeconds = 0.6f;

        /// <summary>帯の出し入れのアニメーション。次の帯を出すときは前のものを止める。</summary>
        private Sequence bannerSequence;
        /// <summary>ボタンの処理をつないだか。二重につながないために使う。</summary>
        private bool wired;

        /// <summary>手番の帯を出してから消えるまでの秒数。CPUはこの間は動かない。</summary>
        public float TurnBannerSeconds => bannerFlipSeconds * 2 + bannerHoldSeconds;
        /// <summary>本文やボタンの背景に使う紙色（ソロと共通）。</summary>
        private static readonly Color Paper = HudStyle.Paper;
        /// <summary>見出し・進行メッセージ・主な操作ボタンに使う強調色（ソロと共通）。</summary>
        private static readonly Color Gold = HudStyle.Gold;
        /// <summary>道具パネルが開いているか。キー入力や山札操作の制限にも使う。</summary>
        public bool IsToolPanelOpen => toolPanel != null && toolPanel.activeSelf;

        /// <summary>起動時に一度呼ぶ。シーンにUIが無ければ生成し、操作ボタンを入力イベントへ接続する。</summary>
        public void Build()
        {
            if (wired) return;
            if (canvasRoot == null) CreateLayout();
            else CreateMissingParts();
            Wire();
        }

        /// <summary>UIがシーンに置かれているか。</summary>
        public bool HasLayout => canvasRoot != null;

        /// <summary>Canvasと対戦用の各パネルを生成し、参照を割り当てる。ボタンの処理はつながないので、エディタからも呼べる。</summary>
        public void CreateLayout()
        {
            // 画面全体のUIをまとめるCanvasオブジェクト。
            var root = new GameObject("BattleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvasRoot = root;
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            // 1920×1080を基準に、ウィンドウの大きさに合わせてHUD全体を拡縮する設定。
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            // 各パネルやボタンの親にするCanvasのTransform。
            Transform canvas = root.transform;

            // ラウンドと共通条件をまとめる上部パネル。
            RectTransform top = Panel(canvas, "HeaderPanel", new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(1120, 134));
            heading = Label(top, "Round", 35, Gold, new Vector2(20, -12), new Vector2(1080, 48), "RoundLabel");
            conditions = Label(top, "Conditions", 28, Paper, new Vector2(20, -76), new Vector2(1080, 42), "ConditionsLabel");
            // プレイヤーの状態を表示する左側パネル。
            RectTransform left = Panel(canvas, "PlayerPanel", new Vector2(0, 1), new Vector2(24, -180), new Vector2(360, 320));
            player = Label(left, "You", 28, Paper, new Vector2(20, -18), new Vector2(320, 290), "StatusLabel");
            // CPUの状態を表示する右側パネル。
            RectTransform right = Panel(canvas, "CpuPanel", new Vector2(1, 1), new Vector2(-24, -180), new Vector2(360, 320));
            cpu = Label(right, "CPU", 28, Paper, new Vector2(20, -18), new Vector2(320, 290), "StatusLabel");
            // 進行メッセージと道具メモを表示する下部パネル。
            RectTransform bottom = Panel(canvas, "MessagePanel", new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(1120, 130));
            message = Label(bottom, "Message", 28, Gold, new Vector2(20, -14), new Vector2(1080, 45), "MessageLabel");
            memo = Label(bottom, "Reading Notes", 25, Paper, new Vector2(20, -65), new Vector2(1080, 50), "MemoLabel");
            // 鑑定を終える／次へは右下の同じ場所に入れ替わりで出る。位置と大きさはソロと共通（HudStyle）。
            stop = Button(canvas, "End Reading\n(Enter)", HudStyle.MainButtonAnchor, HudStyle.MainButtonPosition,
                HudStyle.MainButtonSize, out _, "StopButton");
            // 位置と大きさはソロと同じ（左下）。
            tools = Button(canvas, "Fortune Tools\n(T)", HudStyle.ToolsButtonAnchor, HudStyle.ToolsButtonPosition, HudStyle.ToolsButtonSize,
                out _, "ToolsButton", Gold);
            next = Button(canvas, "Next Round\n(Enter)", HudStyle.MainButtonAnchor, HudStyle.MainButtonPosition,
                HudStyle.MainButtonSize, out nextLabel, "NextButton", Gold);
            next.gameObject.SetActive(false);

            // 二人の精算と最終勝敗を表示する中央パネル。背景を濃くして文字を読みやすくする。
            RectTransform result = Panel(canvas, "ResultPanel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(850, 500));
            result.GetComponent<Image>().color = HudStyle.ResultPanelColor;
            resultPanel = result.gameObject;
            summary = Label(result, "Result", 30, Paper, new Vector2(30, -24), new Vector2(790, 450), "SummaryLabel");
            resultPanel.SetActive(false);
            BuildTools(canvas);
            CreateMissingParts();
            // 道具パネルの暗幕より手前に出し、開いている間も押して閉じられるようにする（ソロと同じ）。
            tools.transform.SetAsLastSibling();
        }

        /// <summary>後から追加・変更した部品が、シーンに置いたUIに無ければ作る（廃止した部品は消す）。変えたときは true を返す。</summary>
        public bool CreateMissingParts()
        {
            bool created = false;
            // 「1枚引く」ボタンは廃止した（山札クリック／Spaceで引く。ソロと同じ）。古いシーンに残っていれば消す。
            Transform oldDraw = canvasRoot.transform.Find("DrawButton");
            if (oldDraw != null)
            {
                if (Application.isPlaying) Destroy(oldDraw.gameObject);
                else DestroyImmediate(oldDraw.gameObject);
                created = true;
            }
            if (!ToolPanelView.IsComplete(toolTiles, toolPanelSpirit))
            {
                // 以前の縦並びの道具パネルが残っていれば消し、ソロと同じタイル型で作り直す。
                if (toolPanel != null)
                {
                    if (Application.isPlaying) Destroy(toolPanel);
                    else DestroyImmediate(toolPanel);
                }
                BuildTools(canvasRoot.transform);
                tools.transform.SetAsLastSibling();
                created = true;
            }
            if (titleButton == null)
            {
                // 対戦終了後に「もう一度対戦」の上に出す。
                titleButton = Button(canvasRoot.transform, "Back to Title\n(Esc)", HudStyle.MainButtonAnchor,
                    HudStyle.TitleButtonPosition, HudStyle.TitleButtonSize, out _, "TitleButton");
                // 道具パネルの暗幕より後ろ（手前に描かれない位置）に置く。
                titleButton.transform.SetSiblingIndex(toolPanel.transform.GetSiblingIndex());
                titleButton.gameObject.SetActive(false);
                created = true;
            }
            if (turnBanner == null)
            {
                BuildTurnBanner();
                created = true;
            }
            return created;
        }

        /// <summary>画面中央を横切る、手番を知らせる帯を作る。クリックは受け取らない（後ろの山札を押せる）。</summary>
        private void BuildTurnBanner()
        {
            // 横幅いっぱい・高さ150の帯。
            turnBanner = Panel(canvasRoot.transform, "TurnBanner", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 150));
            turnBanner.anchorMin = new Vector2(0, 0.5f);
            turnBanner.anchorMax = new Vector2(1, 0.5f);
            turnBanner.GetComponent<Image>().color = HudStyle.ResultPanelColor;
            // 上下の縁に引く金色の細い線。
            BannerLine("TopLine", 1);
            BannerLine("BottomLine", 0);
            turnBannerLabel = HudFactory.Named(HudFactory.Label(font, turnBanner, "Your Turn", 64, Gold, new Vector2(0, -35),
                new Vector2(1400, 80), TextAnchor.MiddleCenter), "BannerLabel");
            // 道具パネルの暗幕より後ろに置く（道具を選んでいる間は暗幕の下になる）。
            turnBanner.SetSiblingIndex(toolPanel.transform.GetSiblingIndex());
            turnBanner.gameObject.SetActive(false);
        }

        /// <summary>帯の上端（y=1）または下端（y=0）に、横幅いっぱいの金色の線を引く。</summary>
        private void BannerLine(string name, float y)
        {
            RectTransform line = Rect(turnBanner, name, new Vector2(0.5f, y), Vector2.zero, new Vector2(0, 4));
            line.anchorMin = new Vector2(0, y);
            line.anchorMax = new Vector2(1, y);
            var image = line.gameObject.AddComponent<Image>();
            image.color = Gold;
            image.raycastTarget = false;
        }

        /// <summary>
        /// 手番の帯を出す。X軸に90°倒れた状態から開いて見せ、少し置いてから反対側へ倒して消す。
        /// 前の帯が残っていれば止めて出し直す。
        /// </summary>
        public void ShowTurnBanner(string text)
        {
            bannerSequence?.Kill();
            turnBannerLabel.text = text;
            turnBanner.gameObject.SetActive(true);
            turnBanner.localRotation = Quaternion.Euler(90f, 0f, 0f);
            bannerSequence = DOTween.Sequence()
                .Append(turnBanner.DOLocalRotate(Vector3.zero, bannerFlipSeconds).SetEase(Ease.OutBack))
                .AppendInterval(bannerHoldSeconds)
                .Append(turnBanner.DOLocalRotate(new Vector3(-90f, 0f, 0f), bannerFlipSeconds).SetEase(Ease.InCubic))
                .OnComplete(() => turnBanner.gameObject.SetActive(false))
                .SetLink(gameObject);
        }

        /// <summary>背景操作を遮る暗幕と、4種類の道具のタイル・閉じるボタンを生成する。見た目はソロと共通（ToolPanelView）。</summary>
        private void BuildTools(Transform canvas)
        {
            toolPanel = ToolPanelView.Build(canvas, font, out toolPanelSpirit, out closeToolsButton, out toolTiles);
        }

        /// <summary>対戦状態から表示と操作可否を更新する。得点や手番の状態そのものは変更しない。</summary>
        public void Refresh(BattleMatch match, bool busy)
        {
            heading.text = $"VS CPU  —  Round {match.Round} / {BattleMatch.RoundCount}  —  Deck: {match.Deck.Remaining} left";
            conditions.text = HudStyle.NumbersText(match.Conditions.LuckyNumber, match.Conditions.BadNumbers);
            player.text = Participant("You", match.Player, match.Turn == BattleSide.Player && !match.IsRoundOver);
            cpu.text = Participant("CPU", match.Cpu, match.Turn == BattleSide.Cpu && !match.IsRoundOver);
            // 演出中ではなくプレイヤーの有効な手番か。操作ボタンの可否をまとめて揃える。
            bool canAct = !busy && match.CanAct(BattleSide.Player);
            stop.gameObject.SetActive(!match.IsRoundOver);
            stop.interactable = tools.interactable = canAct;
            next.gameObject.SetActive(match.IsRoundOver && !busy);
            titleButton.gameObject.SetActive(match.IsMatchOver && !busy);
            nextLabel.text = match.IsMatchOver ? "Rematch\n(Enter)" : "Next Round\n(Enter)";
            if (!canAct) CloseTools();
            ToolPanelView.Refresh(toolTiles, toolPanelSpirit, match.Player.Session, match.Player.Spirit, canAct);
        }

        /// <summary>対戦者1人分の状態を表示用文字列にする。鑑定中の基本売上と終了後の確定点を区別する。</summary>
        private static string Participant(string name, BattleParticipant participant, bool turn)
        {
            // 表示対象の本人の鑑定。運気・倍率・凶の印・精算結果を読むための参照。
            ReadingSession s = participant.Session;
            // 破綻・確定・手番・待機の優先順で決めた状態名。
            string state = s.State == ReadingState.Broken ? "Ruined" : s.IsFinished ? "Settled" : turn ? "Turn" : "Waiting";
            // 鑑定中はボーナス前の基本売上、終了後は予感ボーナス込みの確定得点を示す行。
            string score = s.IsFinished ? $"Final  {s.Result.Score}" : $"Base Earnings  {s.CurrentSales}";
            return $"{name}  [{state}]\nTotal  {participant.TotalScore} pts\nSpirit Power  {participant.Spirit}\n\nLuck {s.Luck} × Mult. {s.Multiplier}\n{score}\nOmen Marks  {s.BadMarks} / {s.BadMarksToBreak}";
        }

        /// <summary>二人の精算を表示する。最終ラウンドだけ累計を比較して勝敗・引き分けも表示する。</summary>
        public void ShowRoundResult(BattleMatch match)
        {
            // 手番の帯が残っていれば、結果に重ならないようすぐ消す。
            bannerSequence?.Kill();
            turnBanner.gameObject.SetActive(false);
            resultPanel.SetActive(true);
            // 複数行の結果文を組み立てるバッファ。途中結果と最終勝敗を同じパネルにまとめる。
            var text = new StringBuilder($"Round {match.Round} Over\n\n");
            text.AppendLine(ResultLine("You", match.Player.Session.Result));
            text.AppendLine(ResultLine("CPU", match.Cpu.Session.Result));
            text.AppendLine($"\nTotal  You {match.Player.TotalScore} / CPU {match.Cpu.TotalScore}");
            if (match.IsMatchOver)
                text.Append("\n" + (match.Player.TotalScore > match.Cpu.TotalScore ? "You win!" : match.Player.TotalScore < match.Cpu.TotalScore ? "CPU wins" : "Draw"));
            summary.text = text.ToString();
        }

        /// <summary>1人分の得点と予感の有無を結果表示用に整形する。破綻時は0点だけを示す。</summary>
        private static string ResultLine(string name, ReadingResult r)
        {
            if (r.IsBroken) return $"{name}: Ruined  —  0 pts";
            // 予感判定なし・外れ・的中のどれかを示す結果文。
            string bonus = r.ForesightCard.HasValue ? r.ForesightHit ? "Premonition hit ×1.5" : "Premonition missed" : "No premonition";
            return $"{name}: {r.Score} pts  {bonus}";
        }

        /// <summary>ボタンの処理をつなぎ、最初は隠しておくパネルを隠す（エディタで表示したまま保存しても大丈夫）。</summary>
        private void Wire()
        {
            wired = true;
            AddClick(stop, () => StopClicked?.Invoke());
            AddClick(tools, ToggleTools);
            AddClick(next, () => NextClicked?.Invoke());
            AddClick(closeToolsButton, CloseTools);
            AddClick(titleButton, () => TitleClicked?.Invoke());
            for (int i = 0; i < toolTiles.Length; i++)
            {
                // このタイルに対応する道具。ボタンのコールバックへ個別に保持する。
                FortuneTool tool = ToolPanelView.Tools[i];
                AddClick(toolTiles[i].Button, () => ToolClicked?.Invoke(tool));
            }
            resultPanel.SetActive(false);
            titleButton.gameObject.SetActive(false);
            turnBanner.gameObject.SetActive(false);
            CloseTools();
        }

        /// <summary>選択状態を解除してから指定の処理を呼ぶクリック処理を登録する。</summary>
        private static void AddClick(Button button, Action action)
        {
            button.onClick.AddListener(() =>
            {
                // 選択状態が残るとSpace/Enterがボタンにも届くため、進行処理を呼ぶ前に解除する。
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                action();
            });
        }

        /// <summary>次ラウンドの開始に備えて結果パネルを隠す。</summary>
        public void HideResult() => resultPanel.SetActive(false);
        /// <summary>進行メッセージを指定された文言に置き換える。</summary>
        public void SetMessage(string value) => message.text = value;
        /// <summary>道具の結果メモを置き換える。空文字を渡すと古い情報を消せる。</summary>
        public void SetMemo(string value) => memo.text = value;
        /// <summary>操作可能な手番なら道具パネルを開き、開いている場合は閉じる。</summary>
        public void ToggleTools() { if (IsToolPanelOpen) CloseTools(); else if (tools.interactable) toolPanel.SetActive(true); }
        /// <summary>道具パネルを閉じる。生成前でも安全に呼べる。</summary>
        public void CloseTools() { if (toolPanel != null) toolPanel.SetActive(false); }
        /// <summary>親・基準点・位置・サイズを指定してUI用のRectTransformを生成する。</summary>
        /// <param name="anchor">親のどこを基準に配置するかを示す0〜1の座標。pivotにも同じ値を使う。</param>
        /// <param name="position">指定したアンカーからのUI上の相対位置。</param>
        /// <param name="size">UI部品の幅と高さ。CanvasScalerの基準解像度でのサイズ。</param>
        private static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
        /// <summary>クリックを妨げない半透明の背景パネルを生成する。</summary>
        private static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(parent, name, anchor, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = HudStyle.PanelColor;
            image.raycastTarget = false;
            return rect;
        }
        /// <summary>共通フォントでラベルを生成する。操作対象ではないためレイキャストを無効にする。</summary>
        /// <param name="fontSize">基準解像度での文字サイズ。</param>
        /// <param name="name">Hierarchyに表示する名前（英語）。</param>
        private Text Label(Transform parent, string value, int fontSize, Color color, Vector2 position, Vector2 size,
            string name = "Label")
        {
            RectTransform rect = Rect(parent, name, new Vector2(0, 1), position, size);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.text = value;
            label.color = color;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }
        /// <summary>背景と中央ラベルを持つボタンを生成し、選択状態を解除してから指定の処理を呼ぶ。</summary>
        /// <param name="label">生成したボタンのラベル。文言を後から変えるための出力。</param>
        /// <param name="name">Hierarchyに表示する名前（英語）。</param>
        /// <param name="color">ボタンの背景色。省略すると紙色。主な操作（次へ・占い道具）は金色にする。</param>
        private Button Button(Transform parent, string value, Vector2 anchor, Vector2 position, Vector2 size, out Text label,
            string name = "Button", Color? color = null)
        {
            RectTransform rect = Rect(parent, name, anchor, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color ?? Paper;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            label = Label(rect, value, HudStyle.ButtonFontSize(size), HudStyle.ButtonText, Vector2.zero, size);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
    }
}
