using System;
using System.Collections.Generic;

namespace Hikigiwa.CardSystem
{
    /// <summary>
    /// 1～9が各2枚とジョーカー1枚の山札。
    /// Unityや画面に依存せず、引いたカードは山札に戻しません。
    /// 内部のリストは先頭が山札の一番上です。
    /// </summary>
    public sealed class Deck
    {
        public const int TotalCards = 19;

        private readonly List<Card> cards = new List<Card>(TotalCards);
        private readonly Random random;

        public int Remaining => cards.Count;

        public Deck() : this(new Random()) { }
        public Deck(int seed) : this(new Random(seed)) { }

        public Deck(Random random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            Reset();
        }

        /// <summary>全19枚を戻し、シャッフルして新しい山札を作ります。</summary>
        public void Reset()
        {
            cards.Clear();
            for (int number = 1; number <= 9; number++)
            {
                cards.Add(new Card(number));
                cards.Add(new Card(number));
            }
            cards.Add(new Card(0));
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                (cards[i], cards[other]) = (cards[other], cards[i]);
            }
        }

        /// <summary>1枚引きます。空の山札ではfalseを返し、枚数は変えません。</summary>
        public bool TryDraw(out Card card)
        {
            if (!TryPeek(out card)) return false;
            cards.RemoveAt(0);
            return true;
        }

        /// <summary>一番上のカードを、引かずに確認します。</summary>
        public bool TryPeek(out Card card)
        {
            if (cards.Count == 0)
            {
                card = default;
                return false;
            }

            card = cards[0];
            return true;
        }

        /// <summary>上からcount枚を、引かずに確認します（残りが少なければその分だけ）。</summary>
        public List<Card> PeekTop(int count)
        {
            return cards.GetRange(0, Math.Min(count, cards.Count));
        }

        /// <summary>一番上のカードを一番下へ回します（手相）。</summary>
        public bool MoveTopToBottom()
        {
            if (cards.Count < 2) return false;
            Card top = cards[0];
            cards.RemoveAt(0);
            cards.Add(top);
            return true;
        }

        /// <summary>条件に合うカードのうち、ランダムな1枚を山札から取り除きます（お香）。</summary>
        public bool TryRemoveRandom(Func<Card, bool> predicate, out Card removed)
        {
            return TryRemoveRandom(predicate, out removed, out _);
        }

        /// <summary>TryRemoveRandomと同じですが、取り除いたカードが上から何枚目だったか（0が一番上）も返します。</summary>
        public bool TryRemoveRandom(Func<Card, bool> predicate, out Card removed, out int removedIndex)
        {
            var matches = new List<int>();
            for (int i = 0; i < cards.Count; i++)
                if (predicate(cards[i])) matches.Add(i);

            if (matches.Count == 0)
            {
                removed = default;
                removedIndex = -1;
                return false;
            }

            int index = matches[random.Next(matches.Count)];
            removed = cards[index];
            removedIndex = index;
            cards.RemoveAt(index);
            return true;
        }

        /// <summary>残りの山札で、条件に合うカードの枚数を数えます。</summary>
        public int CountRemaining(Func<Card, bool> predicate)
        {
            int count = 0;
            foreach (Card card in cards)
                if (predicate(card)) count++;
            return count;
        }
    }
}
