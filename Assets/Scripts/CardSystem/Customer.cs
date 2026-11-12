using System;
using System.Collections.Generic;

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
    /// 吉札は吉数2枚。凶数は通常1種類、CPU対戦では2種類（各2枚＋ジョーカー）です。
    /// </summary>
    public sealed class Customer
    {
        public int LuckyNumber { get; }
        public int BadNumber { get; }
        public int? SecondBadNumber { get; }
        public IReadOnlyList<int> BadNumbers { get; }
        public int SatisfactionLine { get; }

        public Customer(int luckyNumber, int badNumber, int satisfactionLine, int? secondBadNumber = null)
        {
            if (luckyNumber < 1 || luckyNumber > 9) throw new ArgumentOutOfRangeException(nameof(luckyNumber));
            if (badNumber < 1 || badNumber > 9) throw new ArgumentOutOfRangeException(nameof(badNumber));
            if (luckyNumber == badNumber) throw new ArgumentException("吉数と凶数は別の数字にしてください。");

            if (secondBadNumber.HasValue)
            {
                if (secondBadNumber < 1 || secondBadNumber > 9) throw new ArgumentOutOfRangeException(nameof(secondBadNumber));
                if (secondBadNumber == luckyNumber || secondBadNumber == badNumber)
                    throw new ArgumentException("2つの凶数と吉数はすべて別の数字にしてください。");
            }

            SecondBadNumber = secondBadNumber;
            BadNumbers = Array.AsReadOnly(secondBadNumber.HasValue
                ? new[] { badNumber, secondBadNumber.Value } : new[] { badNumber });
            LuckyNumber = luckyNumber;
            BadNumber = badNumber;
            SatisfactionLine = satisfactionLine;
        }

        public CardOmen Judge(Card card)
        {
            if (card.IsJoker || card.Number == BadNumber || card.Number == SecondBadNumber) return CardOmen.Bad;
            if (card.Number == LuckyNumber) return CardOmen.Lucky;
            return CardOmen.Neutral;
        }

        /// <summary>吉数・凶数・満足ラインをランダムに決めた客を作ります。</summary>
        public static Customer CreateRandom(Random random, ReadingRules rules, int badNumberCount = 1)
        {
            if (badNumberCount != 1 && badNumberCount != 2) throw new ArgumentOutOfRangeException(nameof(badNumberCount));
            int bad = random.Next(1, 10);
            int lucky = random.Next(1, 9);
            if (lucky >= bad) lucky++; // 凶数を飛ばして1～9から選ぶ

            int steps = Math.Max(0, (rules.SatisfactionLineMax - rules.SatisfactionLineMin) / rules.SatisfactionLineStep);
            int line = rules.SatisfactionLineMin + random.Next(steps + 1) * rules.SatisfactionLineStep;
            if (badNumberCount == 1) return new Customer(lucky, bad, line);
            int second = random.Next(1, 8);
            // 吉数と最初の凶数を飛ばし、残った7種類から一様に選ぶ。
            for (int number = 1; number <= 9; number++)
            {
                if (number == lucky || number == bad) continue;
                if (--second == 0) return new Customer(lucky, bad, line, number);
            }
            throw new InvalidOperationException("2つ目の凶数を選べませんでした。");
        }
    }
}
