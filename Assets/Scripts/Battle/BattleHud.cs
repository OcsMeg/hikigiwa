using System;
using System.Text;
using Hikigiwa.CardSystem;
using Hikigiwa.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hikigiwa.Battle
{
    /// <summary>対戦専用のHUD。状態を表示し、操作をイベントで通知する。</summary>
    public sealed class BattleHud : MonoBehaviour
    {
        /// <summary>HUD内の日本語表示に共通で使うフォント。シーン生成時に割り当てる。</summary>
        [SerializeField] private Font font;
        /// <summary>1枚引くボタンを押した通知。実際のドロー可否は進行側で判断する。</summary>
        public event Action DrawClicked;
        /// <summary>辞めるボタンを押した通知。得点の精算は進行側へ任せる。</summary>
        public event Action StopClicked;
        /// <summary>次ラウンドまたは再戦ボタンを押した通知。</summary>
        public event Action NextClicked;
        /// <summary>選択した道具を引数として通知するイベント。</summary>
        public event Action<FortuneTool> ToolClicked;
        /// <summary>現在のラウンド番号と共有山札の残数を表示する上部ラベル。</summary>
        private Text heading;
        /// <summary>共通の吉数・凶数2種類・満足ラインを表示するラベル。</summary>
        private Text conditions;
        /// <summary>プレイヤーの手番状態・累計・霊力・鑑定状態を表示するラベル。</summary>
        private Text player;
        /// <summary>CPUの手番状態・累計・霊力・鑑定状態を表示するラベル。</summary>
        private Text cpu;
        /// <summary>今起きた行動や次の操作を案内するメッセージ。</summary>
        private Text message;
        /// <summary>道具で得た情報を表示するメモ。情報が古くなると進行側から消される。</summary>
        private Text memo;
        /// <summary>中央の結果パネルに表示する二人分の精算と最終勝敗。</summary>
        private Text summary;
        /// <summary>ドローボタン。自分の操作可能な手番だけ押せる。</summary>
        private Button draw;
        /// <summary>停止ボタン。ラウンドの途中だけ表示する。</summary>
        private Button stop;
        /// <summary>道具パネルを開閉するボタン。操作不可の手番は無効にする。</summary>
        private Button tools;
        /// <summary>ラウンド終了後に次へ進むボタン。最終ラウンド後は再戦に使う。</summary>
        private Button next;
        /// <summary>次へボタンの文言。次ラウンドと再戦で切り替える。</summary>
        private Text nextLabel;
        /// <summary>背景のクリックを遮断する暗幕と道具一覧の親オブジェクト。</summary>
        private GameObject toolPanel;
        /// <summary>ラウンド結果表示の親。通常の鑑定中は非表示にする。</summary>
        private GameObject resultPanel;
        /// <summary>FortuneToolの列挙値に対応する4種類の使用ボタン。</summary>
        private readonly Button[] toolButtons = new Button[4];
        /// <summary>各道具の名前・必要霊力・効果・使えない理由を表示するラベル。</summary>
        private readonly Text[] toolLabels = new Text[4];
        /// <summary>本文やボタンの背景に使う紙色。</summary>
        private static readonly Color Paper = new Color32(246, 229, 199, 255);
        /// <summary>見出しや進行メッセージに使う強調色。</summary>
        private static readonly Color Gold = new Color32(236, 196, 112, 255);
        /// <summary>道具パネルが開いているか。キー入力や山札操作の制限にも使う。</summary>
        public bool IsToolPanelOpen => toolPanel != null && toolPanel.activeSelf;

        /// <summary>Canvasと対戦用の各パネルを生成し、操作ボタンを入力イベントへ接続する。起動時に一度呼ぶ。</summary>
        public void Build()
        {
            // 画面全体のUIをまとめるCanvasオブジェクト。
            var root = new GameObject("対戦HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            // 1920×1080を基準に、ウィンドウの大きさに合わせてHUD全体を拡縮する設定。
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            // 各パネルやボタンの親にするCanvasのTransform。
            Transform canvas = root.transform;

            // ラウンドと共通条件をまとめる上部パネル。
            RectTransform top = Panel(canvas, "ラウンドと共通条件", new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(1120, 134));
            heading = Label(top, "", 35, Gold, new Vector2(20, -12), new Vector2(1080, 48));
            conditions = Label(top, "", 28, Paper, new Vector2(20, -76), new Vector2(1080, 42));
            // プレイヤーの状態を表示する左側パネル。
            RectTransform left = Panel(canvas, "プレイヤー", new Vector2(0, 1), new Vector2(24, -180), new Vector2(360, 320));
            player = Label(left, "", 28, Paper, new Vector2(20, -18), new Vector2(320, 290));
            // CPUの状態を表示する右側パネル。
            RectTransform right = Panel(canvas, "CPU", new Vector2(1, 1), new Vector2(-24, -180), new Vector2(360, 320));
            cpu = Label(right, "", 28, Paper, new Vector2(20, -18), new Vector2(320, 290));
            // 進行メッセージと道具メモを表示する下部パネル。
            RectTransform bottom = Panel(canvas, "進行メッセージ", new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(1120, 130));
            message = Label(bottom, "", 28, Gold, new Vector2(20, -14), new Vector2(1080, 45));
            memo = Label(bottom, "", 25, Paper, new Vector2(20, -65), new Vector2(1080, 50));
            draw = Button(canvas, "1枚引く（Space）", new Vector2(1, 0), new Vector2(-24, 280), new Vector2(350, 80), () => DrawClicked?.Invoke(), out _);
            stop = Button(canvas, "辞める（Enter）", new Vector2(1, 0), new Vector2(-24, 184), new Vector2(350, 80), () => StopClicked?.Invoke(), out _);
            tools = Button(canvas, "占い道具（T）", new Vector2(0, 0), new Vector2(24, 184), new Vector2(350, 80), ToggleTools, out _);
            next = Button(canvas, "", new Vector2(1, 0), new Vector2(-24, 184), new Vector2(350, 80), () => NextClicked?.Invoke(), out nextLabel);

            // 二人の精算と最終勝敗を表示する中央パネル。背景を濃くして文字を読みやすくする。
            RectTransform result = Panel(canvas, "ラウンド結果", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(850, 500));
            result.GetComponent<Image>().color = new Color32(18, 13, 29, 248);
            resultPanel = result.gameObject;
            summary = Label(result, "", 30, Paper, new Vector2(30, -24), new Vector2(790, 450));
            resultPanel.SetActive(false);
            BuildTools(canvas);
        }

        /// <summary>背景操作を遮る暗幕と、4種類の道具ボタン・閉じるボタンを生成する。</summary>
        private void BuildTools(Transform canvas)
        {
            // 画面全体を覆う暗幕。道具を選んでいる間の背後のクリックを受け止める。
            RectTransform blocker = Rect(canvas, "占い道具パネル", Vector2.zero, Vector2.zero, Vector2.zero);
            blocker.anchorMax = Vector2.one;
            blocker.offsetMin = blocker.offsetMax = Vector2.zero;
            blocker.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);
            toolPanel = blocker.gameObject;
            // 暗幕の手前に置く、道具一覧と閉じるボタンの背景。
            RectTransform panel = Panel(blocker, "道具", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 620));
            Label(panel, "占い道具　使用後も引く／辞めるを選べます", 30, Gold, new Vector2(24, -20), new Vector2(950, 50));
            // FortuneToolの並びに対応するボタン番号（0〜3）。
            for (int i = 0; i < 4; i++)
            {
                // このボタンに対応する道具。ボタンのコールバックへ個別に保持する。
                FortuneTool tool = (FortuneTool)i;
                toolButtons[i] = Button(panel, "", new Vector2(0.5f, 1), new Vector2(0, -90 - i * 105), new Vector2(930, 90),
                    () => ToolClicked?.Invoke(tool), out toolLabels[i]);
            }
            Button(panel, "閉じる（Esc / T）", new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(420, 70), CloseTools, out _);
            CloseTools();
        }

        /// <summary>対戦状態から表示と操作可否を更新する。得点や手番の状態そのものは変更しない。</summary>
        public void Refresh(BattleMatch match, bool busy)
        {
            heading.text = $"CPU対戦　ラウンド {match.Round} / {BattleMatch.RoundCount}　山札 {match.Deck.Remaining}枚";
            conditions.text = $"共通条件　吉数 {match.Conditions.LuckyNumber}　凶数 {string.Join("・", match.Conditions.BadNumbers)} ＋ ジョーカー　満足ライン {match.Conditions.SatisfactionLine}";
            player.text = Participant("あなた", match.Player, match.Turn == BattleSide.Player && !match.IsRoundOver);
            cpu.text = Participant("CPU", match.Cpu, match.Turn == BattleSide.Cpu && !match.IsRoundOver);
            // 演出中ではなくプレイヤーの有効な手番か。操作ボタンの可否をまとめて揃える。
            bool canAct = !busy && match.CanAct(BattleSide.Player);
            draw.gameObject.SetActive(!match.IsRoundOver);
            stop.gameObject.SetActive(!match.IsRoundOver);
            draw.interactable = stop.interactable = tools.interactable = canAct;
            next.gameObject.SetActive(match.IsRoundOver && !busy);
            nextLabel.text = match.IsMatchOver ? "もう一度対戦（Enter）" : "次のラウンド（Enter）";
            if (!canAct) CloseTools();
            for (int i = 0; i < 4; i++)
            {
                FortuneTool tool = (FortuneTool)i;
                // 現在の霊力・山札・使用回数から求めた、この道具を使えない理由。
                ToolBlockReason reason = match.Player.Session.CheckTool(tool, match.Player.Spirit);
                toolButtons[i].interactable = canAct && reason == ToolBlockReason.None;
                // ボタンに表示する道具の短い効果説明。
                string effect = i == 0 ? "次が凶か確認" : i == 1 ? "次の3枚を順不同で確認" : i == 2 ? "凶札を1枚除去（ラウンド1回）" : "一番上を一番下へ";
                toolLabels[i].text = $"{ReadingHud.NameOf(tool)}　霊力 {match.Player.Session.CostOf(tool)}　{effect}" +
                    (reason == ToolBlockReason.None ? "" : "\n" + Reason(reason));
            }
        }

        /// <summary>対戦者1人分の状態を表示用文字列にする。鑑定中の基本売上と終了後の確定点を区別する。</summary>
        private static string Participant(string name, BattleParticipant participant, bool turn)
        {
            // 表示対象の本人の鑑定。運気・倍率・凶の印・精算結果を読むための参照。
            ReadingSession s = participant.Session;
            // 破綻・確定・手番・待機の優先順で決めた状態名。
            string state = s.State == ReadingState.Broken ? "破綻" : s.IsFinished ? "確定" : turn ? "手番" : "待機";
            // 鑑定中はボーナス前の基本売上、終了後はチップ込みの確定得点を示す行。
            string score = s.IsFinished ? $"確定得点　{s.Result.Score}" : $"基本売上　{s.CurrentSales}";
            return $"{name}　【{state}】\n累計　{participant.TotalScore}点\n霊力　{participant.Spirit}\n\n運気 {s.Luck} × 倍率 {s.Multiplier}\n{score}\n凶の印　{new string('●', s.BadMarks)}{new string('○', Math.Max(0, s.BadMarksToBreak - s.BadMarks))}";
        }

        /// <summary>二人の精算を表示する。最終ラウンドだけ累計を比較して勝敗・引き分けも表示する。</summary>
        public void ShowRoundResult(BattleMatch match)
        {
            resultPanel.SetActive(true);
            // 複数行の結果文を組み立てるバッファ。途中結果と最終勝敗を同じパネルにまとめる。
            var text = new StringBuilder($"ラウンド {match.Round} 終了\n\n");
            text.AppendLine(ResultLine("あなた", match.Player.Session.Result));
            text.AppendLine(ResultLine("CPU", match.Cpu.Session.Result));
            text.AppendLine($"\n累計　あなた {match.Player.TotalScore} ／ CPU {match.Cpu.TotalScore}");
            if (match.IsMatchOver)
                text.Append("\n" + (match.Player.TotalScore > match.Cpu.TotalScore ? "あなたの勝利！" : match.Player.TotalScore < match.Cpu.TotalScore ? "CPUの勝利" : "引き分け"));
            summary.text = text.ToString();
        }

        /// <summary>1人分の売上・チップ・予感の有無を結果表示用に整形する。破綻時は0点だけを示す。</summary>
        private static string ResultLine(string name, ReadingResult r)
        {
            if (r.IsBroken) return $"{name}：破綻　0点";
            // 予感判定なし・外れ・的中のどれかを示す結果文。
            string bonus = r.ForesightCard.HasValue ? r.ForesightHit ? "予感的中 ×1.5" : "予感は外れ" : "予感判定なし";
            return $"{name}：{r.Score}点\n売上 {r.Sales} ＋ チップ {r.Tip}　{bonus}";
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
        /// <summary>道具を使えない理由の列挙値を短い日本語へ変換する。</summary>
        private static string Reason(ToolBlockReason reason)
        {
            switch (reason)
            {
                case ToolBlockReason.NotEnoughSpirit: return "霊力不足";
                case ToolBlockReason.LimitReached: return "使用済み";
                case ToolBlockReason.NoBadCardLeft: return "凶札なし";
                case ToolBlockReason.DeckTooSmall: return "山札不足";
                default: return "今は使えません";
            }
        }

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
            image.color = new Color32(18, 13, 29, 225);
            image.raycastTarget = false;
            return rect;
        }
        /// <summary>共通フォントでラベルを生成する。操作対象ではないためレイキャストを無効にする。</summary>
        /// <param name="fontSize">基準解像度での文字サイズ。</param>
        private Text Label(Transform parent, string value, int fontSize, Color color, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(parent, value, new Vector2(0, 1), position, size);
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
        private Button Button(Transform parent, string value, Vector2 anchor, Vector2 position, Vector2 size, Action action, out Text label)
        {
            RectTransform rect = Rect(parent, value, anchor, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Paper;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() =>
            {
                // 選択状態が残るとSpace/Enterがボタンにも届くため、進行処理を呼ぶ前に解除する。
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                action();
            });
            label = Label(rect, value, 28, new Color32(25, 20, 34, 255), Vector2.zero, size);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
    }
}
