namespace Hikigiwa.CardSystem
{
    /// <summary>1枚引いた結果。</summary>
    public readonly struct DrawResult
    {
        public Card Card { get; }
        public CardOmen Omen { get; }
        /// <summary>この1枚で増えた運気。</summary>
        public int LuckGained { get; }
        public int LuckAfter { get; }
        public int MultiplierAfter { get; }
        public int BadMarksAfter { get; }
        public bool CausedBreak { get; }

        public DrawResult(Card card, CardOmen omen, int luckGained, int luckAfter, int multiplierAfter,
            int badMarksAfter, bool causedBreak)
        {
            Card = card;
            Omen = omen;
            LuckGained = luckGained;
            LuckAfter = luckAfter;
            MultiplierAfter = multiplierAfter;
            BadMarksAfter = badMarksAfter;
            CausedBreak = causedBreak;
        }
    }

    /// <summary>客1人分の鑑定の最終結果。</summary>
    public sealed class ReadingResult
    {
        public bool IsBroken { get; internal set; }
        public int Luck { get; internal set; }
        public int Multiplier { get; internal set; }
        /// <summary>運気×倍率。</summary>
        public int BaseSales { get; internal set; }
        /// <summary>終了時に公開した山札の一番上。山札が空なら null。</summary>
        public Card? ForesightCard { get; internal set; }
        public bool ForesightHit { get; internal set; }
        /// <summary>予感的中ボーナス込みの売上。</summary>
        public int Sales { get; internal set; }
        public int SpiritGained { get; internal set; }

        /// <summary>この客で得たスコア（＝売上）。</summary>
        public int Score => Sales;
    }
}
