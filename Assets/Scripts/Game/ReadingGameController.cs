using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Hikigiwa.CardSystem;
using Hikigiwa.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hikigiwa.Game
{
    /// <summary>
    /// ゲームの流れをまとめる司令塔。
    /// 入力 → ReadingSession / PlayRecord（ルール）→ CardTable（3D演出）と ReadingHud（表示）の順につなぎます。
    /// </summary>
    public sealed class ReadingGameController : MonoBehaviour
    {
        private enum Phase
        {
            /// <summary>鑑定中。</summary>
            Reading,
            /// <summary>客1人の鑑定が終わり、結果を表示中。</summary>
            CustomerResult,
            /// <summary>夜が終わり、ノルマの結果を表示中。</summary>
            NightResult,
        }

        [SerializeField] private CardTable table;
        [SerializeField] private ReadingHud hud;
        [Tooltip("0ならランダム。数値を入れると毎回同じ並びになる（デバッグ用）")]
        [SerializeField] private int seed;
        [Tooltip("「タイトルに戻る」で移るシーン")]
        [SerializeField] private string titleSceneName = "Title";
        [Tooltip("1夜のノルマ達成時に加算する霊力")]
        [SerializeField, Min(0)] private int spiritOnQuotaClear = 2;

        private readonly ReadingRules rules = new ReadingRules();
        private readonly RunRules runRules = new RunRules();
        private PlayRecord record;
        private System.Random random;
        private ReadingSession session;
        private Phase phase;
        private bool busy;

        // 占いの結果パネルの中身。ふつうの結果は次に引くまで、タロットは見えたカードを引き終えるまで残す
        private readonly List<string> toolNotes = new List<string>();
        // 水晶の結果（null＝未使用）。山札の一番上が変わったら消す
        private bool? crystalNextIsBad;
        // タロットで見えたカードのうち、まだ引いていないもの。常に山札の上からこの枚数ぶんと一致させる
        private List<Card> tarotLeft;
        private int tarotTotal;

        private void Start()
        {
            random = seed == 0 ? new System.Random() : new System.Random(seed);
            hud.Build();
            hud.StopClicked += RequestStop;
            hud.NextClicked += RequestNext;
            hud.TitleClicked += RequestTitle;
            hud.ToolClicked += RequestTool;
            table.StackClicked += RequestDraw;
            StartRun();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (hud.IsToolPanelOpen)
            {
                if (keyboard.escapeKey.wasPressedThisFrame || keyboard.tKey.wasPressedThisFrame) hud.CloseToolPanel();
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame) RequestTitle();
            if (keyboard.spaceKey.wasPressedThisFrame) RequestDraw();
            if (keyboard.tKey.wasPressedThisFrame && phase == Phase.Reading && !busy) OpenTools();
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                if (phase == Phase.Reading) RequestStop();
                else RequestNext();
            }
        }

        private void StartRun()
        {
            record = new PlayRecord(runRules);
            StartCustomer();
        }

        private void StartCustomer()
        {
            table.Clear();
            session = new ReadingSession(Customer.CreateRandom(random, rules), new Deck(random), rules);
            session.Finished += record.Add;
            phase = Phase.Reading;

            table.SetStackCount(session.Deck.Remaining, Deck.TotalCards);
            hud.HideResult();
            ClearMemo(true);
            RefreshHud();
            hud.SetMessage("Click the deck (or press Space) to draw a card");
            hud.SetMode(ReadingHud.Mode.Reading);
        }

        private void RefreshHud()
        {
            hud.SetRecord(record);
            hud.SetSession(session);
            hud.SetTools(session, record.Spirit);
        }

        private void OpenTools()
        {
            hud.SetTools(session, record.Spirit);
            hud.ToggleToolPanel();
        }

        private void RequestDraw()
        {
            if (busy || phase != Phase.Reading || hud.IsToolPanelOpen || !session.CanDraw) return;
            if (!session.TryDraw(out DrawResult draw)) return;
            StartCoroutine(DrawRoutine(draw));
        }

        private void RequestStop()
        {
            if (busy || phase != Phase.Reading || session.IsFinished) return;
            if (!session.TryStop(out ReadingResult result)) return;
            StartCoroutine(StopRoutine(result));
        }

        private void RequestTool(FortuneTool tool)
        {
            if (busy || phase != Phase.Reading) return;
            if (!session.TryUseTool(tool, record, out ToolResult result)) return;

            hud.CloseToolPanel();
            table.SetStackCount(session.Deck.Remaining, Deck.TotalCards);
            if (tool == FortuneTool.Crystal && result.NextIsBad) table.FlashOmen(CardOmen.Bad, false);

            switch (tool)
            {
                case FortuneTool.Crystal:
                    crystalNextIsBad = result.NextIsBad;
                    break;
                case FortuneTool.Tarot:
                    tarotLeft = new List<Card>(result.RevealedCards);
                    tarotTotal = tarotLeft.Count;
                    break;
                case FortuneTool.Incense:
                    // 同じ数字が2枚あるので、数字ではなく位置で判断する。見えた範囲の外から除いたならタロットは変わらない
                    if (tarotLeft != null && result.RemovedIndex >= 0 && result.RemovedIndex < tarotLeft.Count)
                        RemoveFromTarot(result.RemovedCard);
                    // 一番上が除かれたら、次のカードが変わるので水晶の結果は古くなる
                    if (result.RemovedIndex == 0) crystalNextIsBad = null;
                    toolNotes.Add(ToolMemo(result));
                    break;
                case FortuneTool.Palm:
                    // 一番上が入れ替わるので、水晶の結果も、タロットで見えたカードが次に来るという前提も崩れる
                    crystalNextIsBad = null;
                    tarotLeft = null;
                    toolNotes.Add(ToolMemo(result));
                    break;
            }
            UpdateMemo();
            hud.SetMessage(ToolMemo(result));
            RefreshHud();
        }

        /// <summary>占いの結果パネルを消します。clearTarotがtrueならタロットの結果も消します。</summary>
        private void ClearMemo(bool clearTarot)
        {
            toolNotes.Clear();
            crystalNextIsBad = null;
            if (clearTarot) tarotLeft = null;
            UpdateMemo();
        }

        /// <summary>タロットで見えたカードから1枚消します。山札の上から見えた枚数の範囲内のカードであることが前提です。</summary>
        private void RemoveFromTarot(Card card)
        {
            if (tarotLeft == null) return;
            int index = tarotLeft.FindIndex(c => c.Number == card.Number);
            if (index >= 0) tarotLeft.RemoveAt(index);
            if (tarotLeft.Count == 0) tarotLeft = null;
        }

        private void UpdateMemo()
        {
            var lines = new List<string>();
            if (tarotLeft != null)
            {
                string cards = string.Join(" · ", tarotLeft.Select(ColoredName));
                lines.Add(tarotLeft.Count == tarotTotal
                    ? $"Tarot: Next {tarotTotal} are {cards} (any order)"
                    : $"Tarot: Still to come: {cards} (any order)");
            }
            if (crystalNextIsBad.HasValue) lines.Add(CrystalMemo(crystalNextIsBad.Value));
            lines.AddRange(toolNotes);
            hud.SetMemo(lines);
        }

        private void RequestNext()
        {
            if (busy) return;
            switch (phase)
            {
                case Phase.CustomerResult:
                    if (record.IsNightOver) ShowNightResult();
                    else StartCustomer();
                    break;
                case Phase.NightResult:
                    if (record.IsFinished) StartRun();
                    else
                    {
                        record.StartNextNight();
                        StartCustomer();
                    }
                    break;
            }
        }

        /// <summary>ゲームを最後まで遊んだあとだけ、タイトル画面に戻ります。</summary>
        private void RequestTitle()
        {
            if (busy || phase != Phase.NightResult || !record.IsFinished) return;
            ReturnToTitle().Forget();
        }

        private async UniTaskVoid ReturnToTitle()
        {
            busy = true;
            // 遷移できなかったとき（シーンが Build Settings に無いなど）は操作を戻す
            if (!await SceneTransition.Instance.LoadAsync(titleSceneName)) busy = false;
        }

        private IEnumerator DrawRoutine(DrawResult draw)
        {
            busy = true;
            hud.SetMode(ReadingHud.Mode.Busy);
            hud.SetMessage("Drawing…");
            ClearMemo(false);

            yield return table.PlayDraw(draw, session.Deck.Remaining, Deck.TotalCards);

            RemoveFromTarot(draw.Card);
            UpdateMemo();

            RefreshHud();
            hud.SetMessage(DrawMessage(draw));
            if (session.IsFinished) FinishCustomer(session.Result);
            else hud.SetMode(ReadingHud.Mode.Reading);
            busy = false;
        }

        private IEnumerator StopRoutine(ReadingResult result)
        {
            busy = true;
            hud.SetMode(ReadingHud.Mode.Busy);
            hud.SetMessage("Ending the reading. One last glimpse at the next card…");
            ClearMemo(true);

            if (result.ForesightCard.HasValue)
            {
                Card top = result.ForesightCard.Value;
                yield return table.PlayForesight(top, session.Customer.Judge(top));
            }

            hud.SetMessage(result.ForesightHit ? "Premonition hit! Earnings ×1.5" : "Reading complete");
            FinishCustomer(result);
            busy = false;
        }

        private void FinishCustomer(ReadingResult result)
        {
            phase = Phase.CustomerResult;
            RefreshHud();
            hud.ShowResult(result);
            hud.SetNextLabel(record.IsNightOver ? "End Night" : "Next Client");
            hud.SetMode(ReadingHud.Mode.Finished);
        }

        private void ShowNightResult()
        {
            phase = Phase.NightResult;
            runRules.SpiritOnQuotaClear = Mathf.Max(0, spiritOnQuotaClear);
            NightSummary summary = record.EndNight();
            RefreshHud();
            hud.ShowNightSummary(summary, record);
            hud.SetNextLabel(record.IsFinished ? "Play Again" : "Next Night");
            hud.SetTitleButtonVisible(record.IsFinished);
            hud.SetMessage(summary.Passed ? "Quota met" : "Quota not reached");
        }

        private string DrawMessage(DrawResult draw)
        {
            string name = CardName(draw.Card);
            if (draw.CausedBreak) return $"{name}… {session.BadMarksToBreak} Omen Marks. The reading is ruined";
            switch (draw.Omen)
            {
                case CardOmen.Lucky: return $"Lucky {name}! Multiplier is now ×{draw.MultiplierAfter}";
                case CardOmen.Bad:
                    return $"Unlucky {name}… An Omen Mark appears ({session.BadMarksToBreak - draw.BadMarksAfter} more means ruin)";
                default: return $"{name}  —  Luck +{draw.LuckGained}";
            }
        }

        private string ToolMemo(ToolResult result)
        {
            switch (result.Tool)
            {
                case FortuneTool.Crystal:
                    return CrystalMemo(result.NextIsBad);
                case FortuneTool.Tarot:
                    return "Tarot: Next " + result.RevealedCards.Count + " are " +
                           string.Join(" · ", result.RevealedCards.Select(ColoredName)) + " (any order)";
                case FortuneTool.Incense:
                    return $"Incense: Removed an unlucky card ({CardName(result.RemovedCard)})";
                default:
                    return "Palm Reading: Moved the top card to the bottom";
            }
        }

        private static string CrystalMemo(bool nextIsBad) =>
            nextIsBad ? "Crystal Ball: The next card is <color=#EC6262>unlucky</color>" : "Crystal Ball: The next card is not unlucky";

        private string ColoredName(Card card)
        {
            string name = CardName(card);
            switch (session.Customer.Judge(card))
            {
                case CardOmen.Bad: return $"<color=#EC6262>{name}</color>";
                case CardOmen.Lucky: return $"<color=#ECC470>{name}</color>";
                default: return name;
            }
        }

        private static string CardName(Card card) => card.IsJoker ? "Joker" : card.Number.ToString();
    }
}
