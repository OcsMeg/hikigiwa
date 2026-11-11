using System;

namespace Hikigiwa.CardSystem
{
    /// <summary>1～9の数字札。0はジョーカーを表します。</summary>
    public readonly struct Card
    {
        public int Number { get; }
        public bool IsJoker => Number == 0;

        public Card(int number)
        {
            if (number < 0 || number > 9)
                throw new ArgumentOutOfRangeException(nameof(number), "カードの数字は0～9です。");
            Number = number;
        }
    }
}
