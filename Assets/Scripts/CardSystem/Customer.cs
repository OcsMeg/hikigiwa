using System;

namespace Hikigiwa.CardSystem
{
    /// <summary>カードがその客にとって吉・凶・ふつうのどれか。</summary>
    public enum CardOmen
    {
        Neutral,
        Lucky,
        Bad,
    }

    /// <summary>
    /// 客1人分の占いの条件。
    /// 凶札はジョーカー＋凶数2枚の計3枚、吉札は吉数2枚です。
    /// </summary>
    public sealed class Customer
    {
        public int LuckyNumber { get; }
        public int BadNumber { get; }
        public int SatisfactionLine { get; }

        public Customer(int luckyNumber, int badNumber, int satisfactionLine)
        {
            if (luckyNumber < 1 || luckyNumber > 9) throw new ArgumentOutOfRangeException(nameof(luckyNumber));
            if (badNumber < 1 || badNumber > 9) throw new ArgumentOutOfRangeException(nameof(badNumber));
            if (luckyNumber == badNumber) throw new ArgumentException("吉数と凶数は別の数字にしてください。");

            LuckyNumber = luckyNumber;
            BadNumber = badNumber;
            SatisfactionLine = satisfactionLine;
        }

        public CardOmen Judge(Card card)
        {
            if (card.IsJoker || card.Number == BadNumber) return CardOmen.Bad;
            if (card.Number == LuckyNumber) return CardOmen.Lucky;
            return CardOmen.Neutral;
        }

        /// <summary>吉数・凶数・満足ラインをランダムに決めた客を作ります。</summary>
        public static Customer CreateRandom(Random random, ReadingRules rules)
        {
            int bad = random.Next(1, 10);
            int lucky = random.Next(1, 9);
            if (lucky >= bad) lucky++; // 凶数を飛ばして1～9から選ぶ

            int steps = Math.Max(0, (rules.SatisfactionLineMax - rules.SatisfactionLineMin) / rules.SatisfactionLineStep);
            int line = rules.SatisfactionLineMin + random.Next(steps + 1) * rules.SatisfactionLineStep;
            return new Customer(lucky, bad, line);
        }
    }
}
