using System;
using System.Collections.Generic;

namespace Hikigiwa.CardSystem
{
    public enum ReadingState
    {
        /// <summary>鑑定中（引くか終えるかを選べる）。</summary>
        Reading,
        /// <summary>凶の印がそろって破綻した。</summary>
        Broken,
        /// <summary>プレイヤーが鑑定を終え、売上が確定した。</summary>
        Settled,
    }

    /// <summary>
    /// 客1人分の鑑定。画面には依存せず、状態の変化をイベントで知らせます。
    /// UIや3D演出はこのイベントを受け取って表示します。
    /// </summary>
    public sealed class ReadingSession
    {
        /// <summary>カードを引いた（吉・凶の判定込み）。</summary>
        public event Action<DrawResult> CardDrawn;
        /// <summary>吉数を引いた。引数は新しい倍率。</summary>
        public event Action<int> LuckyDrawn;
        /// <summary>凶を引いた。引数は現在の凶の印の数。</summary>
        public event Action<int> BadOmenDrawn;
        /// <summary>破綻した。</summary>
        public event Action<ReadingResult> Broken;
        /// <summary>鑑定を終えて売上が確定した。</summary>
        public event Action<ReadingResult> Settled;
        /// <summary>破綻・確定のどちらでも、鑑定が終わったときに呼ばれる。</summary>
        public event Action<ReadingResult> Finished;
        /// <summary>占い道具を使った。</summary>
        public event Action<ToolResult> ToolUsed;

        private readonly ReadingRules rules;
        private readonly List<Card> drawnCards = new List<Card>();
        private readonly Dictionary<FortuneTool, int> toolUses = new Dictionary<FortuneTool, int>();

        public Customer Customer { get; }
        public Deck Deck { get; }
        public ReadingState State { get; private set; } = ReadingState.Reading;
        public int Luck { get; private set; }
        public int Multiplier { get; private set; }
        public int BadMarks { get; private set; }
        public IReadOnlyList<Card> DrawnCards => drawnCards;
        public ReadingResult Result { get; private set; }

        public int BadMarksToBreak => rules.BadMarksToBreak;
        public bool IsFinished => State != ReadingState.Reading;
        public bool CanDraw => State == ReadingState.Reading && Deck.Remaining > 0;
        /// <summary>今終えた場合の売上（ボーナス抜き）。</summary>
        public int CurrentSales => Luck * Multiplier;

        public ReadingSession(Customer customer, Deck deck, ReadingRules rules)
        {
            Customer = customer ?? throw new ArgumentNullException(nameof(customer));
            Deck = deck ?? throw new ArgumentNullException(nameof(deck));
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Multiplier = rules.StartMultiplier;
        }

        /// <summary>残りの山札にある、指定した吉凶のカード枚数。</summary>
        public int CountRemaining(CardOmen omen) => Deck.CountRemaining(card => Customer.Judge(card) == omen);

        /// <summary>1枚引きます。鑑定が終わっている、または山札が空ならfalse。</summary>
        public bool TryDraw(out DrawResult result)
        {
            result = default;
            if (!CanDraw || !Deck.TryDraw(out Card card)) return false;

            drawnCards.Add(card);
            CardOmen omen = Customer.Judge(card);
            int gained = card.Number;
            if (omen == CardOmen.Bad && !rules.BadNumberAddsLuck) gained = 0;
            Luck += gained;

            if (omen == CardOmen.Lucky) Multiplier += rules.LuckyMultiplierStep;
            if (omen == CardOmen.Bad) BadMarks++;

            bool broke = BadMarks >= rules.BadMarksToBreak;
            result = new DrawResult(card, omen, gained, Luck, Multiplier, BadMarks, broke);

            CardDrawn?.Invoke(result);
            if (omen == CardOmen.Lucky) LuckyDrawn?.Invoke(Multiplier);
            if (omen == CardOmen.Bad) BadOmenDrawn?.Invoke(BadMarks);
            if (broke) Break();
            return true;
        }

        /// <summary>鑑定を終えて売上を確定します。鑑定中でなければfalse。</summary>
        public bool TryStop(out ReadingResult result)
        {
            result = null;
            if (State != ReadingState.Reading) return false;

            result = new ReadingResult
            {
                Luck = Luck,
                Multiplier = Multiplier,
                BaseSales = CurrentSales,
            };

            if (Deck.TryPeek(out Card top))
            {
                result.ForesightCard = top;
                result.ForesightHit = Customer.Judge(top) == CardOmen.Bad;
            }

            result.Sales = result.ForesightHit
                ? (int)Math.Floor(result.BaseSales * rules.ForesightBonusRate)
                : result.BaseSales;
            result.SatisfactionReached = Luck >= Customer.SatisfactionLine;
            result.Tip = result.SatisfactionReached ? rules.Tip : 0;
            result.SpiritGained = rules.SpiritOnSafeFinish;

            State = ReadingState.Settled;
            Result = result;
            Settled?.Invoke(result);
            Finished?.Invoke(result);
            return true;
        }

        public int CostOf(FortuneTool tool) => rules.CostOf(tool);

        /// <summary>この客にその道具を使った回数。</summary>
        public int UsesOf(FortuneTool tool) => toolUses.TryGetValue(tool, out int count) ? count : 0;

        /// <summary>道具が今使えるか。使えなければその理由を返します。</summary>
        public ToolBlockReason CheckTool(FortuneTool tool, int spirit)
        {
            if (State != ReadingState.Reading) return ToolBlockReason.NotReading;
            switch (tool)
            {
                case FortuneTool.Crystal:
                case FortuneTool.Tarot:
                    if (Deck.Remaining < 1) return ToolBlockReason.DeckTooSmall;
                    break;
                case FortuneTool.Palm:
                    if (Deck.Remaining < 2) return ToolBlockReason.DeckTooSmall;
                    break;
                case FortuneTool.Incense:
                    if (UsesOf(tool) >= rules.IncenseLimitPerCustomer) return ToolBlockReason.LimitReached;
                    if (CountRemaining(CardOmen.Bad) == 0) return ToolBlockReason.NoBadCardLeft;
                    break;
            }
            if (spirit < rules.CostOf(tool)) return ToolBlockReason.NotEnoughSpirit;
            return ToolBlockReason.None;
        }

        /// <summary>霊力を払って占い道具を使います。使えなければfalseで、何も変えません。</summary>
        public bool TryUseTool(FortuneTool tool, PlayRecord wallet, out ToolResult result)
        {
            result = null;
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            if (CheckTool(tool, wallet.Spirit) != ToolBlockReason.None) return false;

            int cost = rules.CostOf(tool);
            if (!wallet.TrySpendSpirit(cost)) return false;

            result = new ToolResult { Tool = tool, SpiritSpent = cost };
            switch (tool)
            {
                case FortuneTool.Crystal:
                    Deck.TryPeek(out Card next);
                    result.NextIsBad = Customer.Judge(next) == CardOmen.Bad;
                    break;
                case FortuneTool.Tarot:
                    List<Card> seen = Deck.PeekTop(rules.TarotCards);
                    // 順番が分からないよう、数字の小さい順に並べ替える（ジョーカーは0なので先頭）
                    seen.Sort((a, b) => a.Number.CompareTo(b.Number));
                    result.RevealedCards = seen;
                    break;
                case FortuneTool.Incense:
                    Deck.TryRemoveRandom(card => Customer.Judge(card) == CardOmen.Bad, out Card removed);
                    result.RemovedCard = removed;
                    break;
                case FortuneTool.Palm:
                    Deck.MoveTopToBottom();
                    break;
            }

            toolUses[tool] = UsesOf(tool) + 1;
            ToolUsed?.Invoke(result);
            return true;
        }

        private void Break()
        {
            var result = new ReadingResult
            {
                IsBroken = true,
                Luck = 0,
                Multiplier = Multiplier,
            };
            Luck = 0;
            State = ReadingState.Broken;
            Result = result;
            Broken?.Invoke(result);
            Finished?.Invoke(result);
        }
    }
}
