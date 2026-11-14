using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using Hikigiwa.CardSystem;
using Hikigiwa.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hikigiwa.Battle
{
    /// <summary>対戦の入力と進行。ルール、CPU判断、カード演出、HUDをつなぐ。</summary>
    public sealed class BattleGameController : MonoBehaviour
    {
        /// <summary>カード配置・山札表示・めくり演出の依頼先。</summary>
        [SerializeField] private BattleCardDealer dealer;
        /// <summary>対戦状況の表示とUI入力イベントの受け口。</summary>
        [SerializeField] private BattleHud hud;
        /// <summary>再現確認用の乱数シード。0なら起動ごとに異なる乱数を使う。</summary>
        [SerializeField, Tooltip("0はランダム。それ以外は再現用のシード")] private int seed;
        /// <summary>CPUの手番を知らせてから判断するまでの待ち時間（ゲーム内秒）。</summary>
        [SerializeField, Min(0)] private float cpuThinkSeconds = 0.7f;
        /// <summary>「タイトルに戻る」で移るシーンの名前。</summary>
        [SerializeField, Tooltip("「タイトルに戻る」で移るシーン")] private string titleSceneName = "Title";
        /// <summary>停止抽選を始める凶札率（0〜100%）。CPUへ渡す際に0〜1へ変換する。</summary>
        [Header("CPUの停止判断")]
        [SerializeField, Range(0f, 100f), InspectorName("停止判定を始める凶札率（%）")]
        [Tooltip("凶を1回引いた後、残り山札の凶札率がこの値以上なら停止を抽選します")]
        private float cpuBadRateThresholdPercent = 50f;
        /// <summary>凶札率の条件を満たした手番で辞める確率（0〜100%）。</summary>
        [SerializeField, Range(0f, 100f), InspectorName("停止する確率（%）")]
        [Tooltip("上の凶札率を満たした手番ごとに、この確率で辞めます。変更は次のCPU判断から反映します")]
        private float cpuStopProbabilityPercent = 50f;
        /// <summary>入力の代わりにCPUの引く／辞めるを判断するルールオブジェクト。</summary>
        private CpuBattleStrategy cpu;
        /// <summary>対戦の条件と山札を作る乱数。起動時にCPU専用の乱数も別に作る。</summary>
        private System.Random random;
        /// <summary>CPU待機・演出中の操作を禁止するフラグ。連打による二重進行を防ぐ。</summary>
        private bool busy;
        /// <summary>最後に手番の帯を出した側。ラウンド開始時は null にして、必ず帯を出す。</summary>
        private BattleSide? bannerSide;
        /// <summary>直前に手番の帯を出したか。CPUは帯が消えるまで動かない。</summary>
        private bool bannerShown;
        /// <summary>現在の対戦状態。得点や手番の正本はこのオブジェクトが持つ。</summary>
        public BattleMatch Match { get; private set; }
        /// <summary>外部から進行待ち・演出中かを確認するための読み取り専用の窓口。</summary>
        public bool IsBusy => busy;

        /// <summary>乱数・CPU・HUDを初期化し、入力イベントを購読して最初の対戦を始める。</summary>
        private void Start()
        {
            random = seed == 0 ? new System.Random() : new System.Random(seed);
            // CPUの判断乱数は山札の乱数から分離する。
            cpu = new CpuBattleStrategy(new System.Random(random.Next()),
                cpuStopProbabilityPercent / 100d, cpuBadRateThresholdPercent / 100d);
            hud.Build();
            hud.StopClicked += RequestStop;
            hud.NextClicked += RequestNext;
            hud.ToolClicked += RequestTool;
            hud.TitleClicked += RequestTitle;
            dealer.Table.StackClicked += RequestDraw;
            StartMatch();
        }

        /// <summary>購読した入力イベントを解除し、シーン破棄後に古い進行処理が呼ばれるのを防ぐ。</summary>
        private void OnDestroy()
        {
            if (hud != null)
            {
                hud.StopClicked -= RequestStop;
                hud.NextClicked -= RequestNext;
                hud.ToolClicked -= RequestTool;
                hud.TitleClicked -= RequestTitle;
            }
            if (dealer != null && dealer.Table != null) dealer.Table.StackClicked -= RequestDraw;
        }

        /// <summary>Space・Enter・T・Escを処理する。道具パネル表示中はパネルを閉じる入力だけ受け付ける。</summary>
        private void Update()
        {
            // 現在接続されているキーボード。未接続ならキー入力を処理しない。
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || Match == null) return;
            if (hud.IsToolPanelOpen)
            {
                if (keyboard.escapeKey.wasPressedThisFrame || keyboard.tKey.wasPressedThisFrame) hud.CloseTools();
                return;
            }
            if (keyboard.escapeKey.wasPressedThisFrame) RequestTitle();
            if (keyboard.spaceKey.wasPressedThisFrame) RequestDraw();
            if (keyboard.tKey.wasPressedThisFrame && !busy && Match.CanAct(BattleSide.Player)) hud.ToggleTools();
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                if (Match.IsRoundOver) RequestNext();
                else RequestStop();
            }
        }

        /// <summary>得点と霊力を含めて対戦を作り直し、第1ラウンドの表示を準備する。</summary>
        private void StartMatch()
        {
            Match = new BattleMatch(random);
            PrepareRound();
        }

        /// <summary>カードと前の結果表示を片付け、決定済みの先手へ進行を渡す。</summary>
        private void PrepareRound()
        {
            dealer.ResetCards(Match.Deck.Remaining);
            hud.HideResult();
            hud.SetMemo("");
            bannerSide = null;
            BeginTurn($"Round {Match.Round} begins. " + (Match.FirstSide == BattleSide.Player ? "You go first." : "CPU goes first."));
        }

        /// <summary>山札クリック・Spaceからのドロー要求を受け、操作可能なときだけ演出付きで実行する。</summary>
        public void RequestDraw()
        {
            if (!CanPlayerAct() || hud.IsToolPanelOpen) return;
            StartCoroutine(ResolveAction(BattleSide.Player, false));
        }

        /// <summary>停止要求を受け、操作可能なときだけ精算と予感演出を実行する。</summary>
        public void RequestStop()
        {
            if (!CanPlayerAct() || hud.IsToolPanelOpen) return;
            StartCoroutine(ResolveAction(BattleSide.Player, true));
        }

        /// <summary>結果表示中の次へ操作。途中なら次ラウンド、対戦終了後なら初期状態から再戦する。</summary>
        public void RequestNext()
        {
            if (busy || Match == null || !Match.IsRoundOver) return;
            if (Match.IsMatchOver) StartMatch();
            else if (Match.StartNextRound()) PrepareRound();
        }

        /// <summary>対戦終了後だけ、タイトル画面へ戻る。</summary>
        public void RequestTitle()
        {
            if (busy || Match == null || !Match.IsMatchOver) return;
            ReturnToTitle().Forget();
        }

        /// <summary>フェード付きでタイトルへ遷移する。遷移できなかったときは操作を戻す。</summary>
        private async UniTaskVoid ReturnToTitle()
        {
            busy = true;
            if (!await SceneTransition.Instance.LoadAsync(titleSceneName)) busy = false;
        }

        /// <summary>対戦が存在し、演出中ではなく、プレイヤーの有効な手番かを調べる。</summary>
        private bool CanPlayerAct() => !busy && Match != null && Match.CanAct(BattleSide.Player);

        /// <summary>道具を使って霊力・山札・結果メモを更新する。道具だけで手番は終わらない。</summary>
        public void RequestTool(FortuneTool tool)
        {
            // 停止または道具使用で確定した結果。処理が失敗した場合は以降の表示を行わない。
            if (!CanPlayerAct() || !Match.TryUseTool(tool, out ToolResult result)) return;
            hud.CloseTools();
            dealer.UpdateStack(Match.Deck.Remaining);
            // 今回使用した道具の結果を、プレイヤー用メモとして示す文言。
            string note;
            switch (tool)
            {
                case FortuneTool.Crystal: note = result.NextIsBad ? "Crystal Ball: The next card is unlucky" : "Crystal Ball: The next card is not unlucky"; break;
                case FortuneTool.Tarot: note = "Tarot (any order): " + string.Join(" · ", result.RevealedCards.Select(CardName)); break;
                case FortuneTool.Incense: note = $"Incense: Removed unlucky card {CardName(result.RemovedCard)}"; break;
                default: note = "Palm Reading: Moved the top card to the bottom"; break;
            }
            // 山札を変える道具を使った場合、古い予測は残さない。
            hud.SetMemo(note);
            hud.Refresh(Match, false);
            if (Match.IsRoundOver) EndRound();
            else hud.SetMessage("Tool used. Draw a card or end the reading.");
        }

        /// <summary>ラウンド終了を判定し、続行なら手番側に応じた入力受付またはCPU待機を開始する。</summary>
        /// <param name="previous">直前の行動の案内文。次の手番の案内を続けて表示する。</param>
        private void BeginTurn(string previous)
        {
            if (Match.IsRoundOver) { EndRound(); return; }
            ShowTurnBannerIfChanged();
            if (Match.Turn == BattleSide.Cpu)
            {
                busy = true;
                hud.Refresh(Match, true);
                hud.SetMessage(previous + " CPU is thinking…");
                StartCoroutine(CpuTurn());
            }
            else
            {
                hud.Refresh(Match, false);
                hud.SetMessage(previous + " Your turn (click the deck or press Space to draw).");
            }
        }

        /// <summary>
        /// 手番が替わったとき（ラウンド開始時を含む）だけ、画面中央に手番の帯を出す。
        /// 相手が先に終えて同じ側が続けて引くときは出さない。
        /// </summary>
        private void ShowTurnBannerIfChanged()
        {
            bannerShown = bannerSide != Match.Turn;
            if (!bannerShown) return;
            // ラウンドの最初の帯だけ、ラウンド番号も添える。
            string round = bannerSide == null ? $"Round {Match.Round}  —  " : "";
            bannerSide = Match.Turn;
            hud.ShowTurnBanner(round + (Match.Turn == BattleSide.Player ? "Your Turn" : "CPU's Turn"));
        }

        /// <summary>待ち時間の後にInspector設定を読み直し、CPUの判断を通常の行動処理へ渡す。</summary>
        private IEnumerator CpuTurn()
        {
            // 手番の帯を出したときは、帯が消えるまで待ってから動く。
            yield return new WaitForSeconds(bannerShown ? Mathf.Max(cpuThinkSeconds, hud.TurnBannerSeconds) : cpuThinkSeconds);
            // 再生中にInspectorで変えた場合も、乱数の状態を保ったまま次の判断へ反映する。
            cpu.Configure(cpuStopProbabilityPercent / 100d, cpuBadRateThresholdPercent / 100d);
            yield return ResolveAction(BattleSide.Cpu, cpu.ShouldStop(Match.Cpu.Session));
        }

        /// <summary>ルール上の引く／停止を先に実行し、演出を待ってから次の手番へ進める。</summary>
        /// <param name="stop">trueなら停止して精算、falseなら1枚引く。</param>
        private IEnumerator ResolveAction(BattleSide side, bool stop)
        {
            busy = true;
            hud.Refresh(Match, true);
            hud.SetMemo("");
            // 演出終了後に残す行動結果の案内。次の手番の案内と連結して表示する。
            string message;
            if (stop)
            {
                if (!Match.TryStop(side, out ReadingResult result)) { busy = false; yield break; }
                hud.SetMessage($"{SideName(side)}: ending the reading.");
                yield return dealer.Reveal(result, Match.Conditions);
                message = $"{SideName(side)}: settled at {result.Score} pts." + (result.ForesightHit ? " Premonition hit!" : "");
            }
            else
            {
                // ルール側で確定した1枚の結果。状態を再度変更せず、そのまま演出へ渡す。
                if (!Match.TryDraw(side, out DrawResult draw)) { busy = false; yield break; }
                hud.SetMessage($"{SideName(side)}: drawing…");
                yield return dealer.Draw(side, draw, Match.Deck.Remaining);
                // 引いた結果の吉凶を、メッセージ内で使う短い表示名に変換したもの。
                string omen = draw.Omen == CardOmen.Bad ? "Unlucky" : draw.Omen == CardOmen.Lucky ? "Lucky" : "Normal";
                message = $"{SideName(side)}: {CardName(draw.Card)} ({omen})." + (draw.CausedBreak ? " Ruined." : "");
            }
            busy = false;
            BeginTurn(message);
        }

        /// <summary>二人の確定結果と次へボタンを表示し、山札切れか通常終了かを案内する。</summary>
        private void EndRound()
        {
            hud.Refresh(Match, false);
            hud.ShowRoundResult(Match);
            hud.SetMessage(Match.Deck.Remaining == 0 ? "The deck is empty. Settling without the premonition bonus." : "Both readings are over.");
        }

        /// <summary>対戦者の識別子を画面表示用の名前へ変換する。</summary>
        private static string SideName(BattleSide side) => side == BattleSide.Player ? "You" : "CPU";
        /// <summary>数字札は数字、0のカードはジョーカーという表示名へ変換する。</summary>
        private static string CardName(Card card) => card.IsJoker ? "Joker" : card.Number.ToString();
    }
}
