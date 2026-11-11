using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Hikigiwa.CardSystem;
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

        private readonly ReadingRules rules = new ReadingRules();
        private readonly RunRules runRules = new RunRules();
        private PlayRecord record;
        private System.Random random;
        private ReadingSession session;
        private Phase phase;
        private bool busy;

        // 占いの結果パネルの中身。ふつうの結果は次に引くまで、タロットは見えたカードを引き終えるまで残す
        private readonly List<string> toolNotes = new List<string>();
        private List<Card> tarotLeft;
        private int tarotTotal;

        private void Start()
        {
            random = seed == 0 ? new System.Random() : new System.Random(seed);
            hud.Build();
            hud.StopClicked += RequestStop;
            hud.NextClicked += RequestNext;
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
            hud.SetMessage("山札をクリック（またはSpace）でカードをめくります");
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
                case FortuneTool.Tarot:
                    tarotLeft = new List<Card>(result.RevealedCards);
                    tarotTotal = tarotLeft.Count;
                    break;
                case FortuneTool.Incense:
                    RemoveFromTarot(result.RemovedCard);
                    toolNotes.Add(ToolMemo(result));
                    break;
                case FortuneTool.Palm:
                    // 順番が変わり、タロットで見えたカードが次に来るとは限らなくなるので消す
                    tarotLeft = null;
                    toolNotes.Add(ToolMemo(result));
                    break;
                default:
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
            if (clearTarot) tarotLeft = null;
            UpdateMemo();
        }

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
                string cards = string.Join("・", tarotLeft.Select(ColoredName));
                lines.Add(tarotLeft.Count == tarotTotal
                    ? $"タロット：次の{tarotTotal}枚は {cards}（順不同）"
                    : $"タロット：残りは {cards}（順不同）");
            }
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

        private IEnumerator DrawRoutine(DrawResult draw)
        {
            busy = true;
            hud.SetMode(ReadingHud.Mode.Busy);
            hud.SetMessage("カードをめくっています……");
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
            hud.SetMessage("鑑定を終えます。最後に次の1枚を占うと……");
            ClearMemo(true);

            if (result.ForesightCard.HasValue)
            {
                Card top = result.ForesightCard.Value;
                yield return table.PlayForesight(top, session.Customer.Judge(top));
            }

            hud.SetMessage(result.ForesightHit ? "予感的中！ 売上が1.5倍になりました" : "鑑定が確定しました");
            FinishCustomer(result);
            busy = false;
        }

        private void FinishCustomer(ReadingResult result)
        {
            phase = Phase.CustomerResult;
            RefreshHud();
            hud.ShowResult(result);
            hud.SetNextLabel(record.IsNightOver ? "夜を終える" : "次の客へ");
            hud.SetMode(ReadingHud.Mode.Finished);
        }

        private void ShowNightResult()
        {
            phase = Phase.NightResult;
            NightSummary summary = record.EndNight();
            hud.ShowNightSummary(summary, record);
            hud.SetNextLabel(record.IsFinished ? "最初から" : "次の夜へ");
            hud.SetMessage(summary.Passed ? "ノルマを達成しました" : "ノルマに届きませんでした");
        }

        private string DrawMessage(DrawResult draw)
        {
            string name = CardName(draw.Card);
            if (draw.CausedBreak) return $"{name}……凶の印が{session.BadMarksToBreak}つ。破綻しました";
            switch (draw.Omen)
            {
                case CardOmen.Lucky: return $"吉数 {name}！ 倍率が ×{draw.MultiplierAfter} に上がりました";
                case CardOmen.Bad:
                    return $"凶 {name}……凶の印が付きました（あと{session.BadMarksToBreak - draw.BadMarksAfter}つで破綻）";
                default: return $"{name}　運気 +{draw.LuckGained}";
            }
        }

        private string ToolMemo(ToolResult result)
        {
            switch (result.Tool)
            {
                case FortuneTool.Crystal:
                    return result.NextIsBad ? "水晶：次のカードは <color=#EC6262>凶</color>" : "水晶：次のカードは凶ではない";
                case FortuneTool.Tarot:
                    return "タロット：次の" + result.RevealedCards.Count + "枚は " +
                           string.Join("・", result.RevealedCards.Select(ColoredName)) + "（順不同）";
                case FortuneTool.Incense:
                    return $"お香：凶札（{CardName(result.RemovedCard)}）を1枚取り除いた";
                default:
                    return "手相：一番上のカードを一番下へ回した";
            }
        }

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

        private static string CardName(Card card) => card.IsJoker ? "ジョーカー" : card.Number.ToString();
    }
}
