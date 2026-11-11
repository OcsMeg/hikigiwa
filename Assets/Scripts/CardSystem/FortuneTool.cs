using System.Collections.Generic;

namespace Hikigiwa.CardSystem
{
    /// <summary>鑑定中に霊力を払って使う占い道具。</summary>
    public enum FortuneTool
    {
        /// <summary>水晶：次のカードが凶かどうか分かる。</summary>
        Crystal,
        /// <summary>タロット：次の3枚の数字が分かる（順番は不明）。</summary>
        Tarot,
        /// <summary>お香：山札の凶札を1枚取り除く。</summary>
        Incense,
        /// <summary>手相：一番上のカードを一番下へ回す（中身は見えない）。</summary>
        Palm,
    }

    /// <summary>道具が使えない理由。</summary>
    public enum ToolBlockReason
    {
        None,
        NotReading,
        NotEnoughSpirit,
        DeckTooSmall,
        NoBadCardLeft,
        LimitReached,
    }

    /// <summary>占い道具を使った結果。</summary>
    public sealed class ToolResult
    {
        public FortuneTool Tool { get; internal set; }
        public int SpiritSpent { get; internal set; }

        /// <summary>水晶：次のカードが凶か。</summary>
        public bool NextIsBad { get; internal set; }

        /// <summary>タロット：見えたカード（数字の小さい順に並べ替え済み）。</summary>
        public IReadOnlyList<Card> RevealedCards { get; internal set; } = new List<Card>();

        /// <summary>お香：取り除いたカード。</summary>
        public Card RemovedCard { get; internal set; }
    }
}
