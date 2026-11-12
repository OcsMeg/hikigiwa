using System;

namespace Hikigiwa.CardSystem
{
    /// <summary>凶取得後、設定した凶札率以上になった手番で停止を抽選する。道具は使わない。</summary>
    public sealed class CpuBattleStrategy
    {
        /// <summary>停止するかどうかの抽選専用の乱数。設定を変えても乱数の状態を維持する。</summary>
        private readonly Random random;
        /// <summary>停止抽選が行われる手番で辞める確率。0で必ず続行、1で必ず停止。</summary>
        public double StopProbability { get; private set; }
        /// <summary>停止抽選を始める凶札率の下限（0〜1）。残り山札の凶札率がこの値以上なら抽選する。</summary>
        public double BadRateThreshold { get; private set; }

        /// <summary>判断用の乱数と初期確率を受け取り、値が0〜1に収まることを検証する。</summary>
        public CpuBattleStrategy(Random random, double stopProbability = 0.5, double badRateThreshold = 0.5)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            Configure(stopProbability, badRateThreshold);
        }

        /// <summary>設定変更は次の判断から適用する。どちらも0〜1。</summary>
        public void Configure(double stopProbability, double badRateThreshold)
        {
            if (double.IsNaN(stopProbability) || stopProbability < 0 || stopProbability > 1)
                throw new ArgumentOutOfRangeException(nameof(stopProbability));
            if (double.IsNaN(badRateThreshold) || badRateThreshold < 0 || badRateThreshold > 1)
                throw new ArgumentOutOfRangeException(nameof(badRateThreshold));
            StopProbability = stopProbability;
            BadRateThreshold = badRateThreshold;
        }

        /// <summary>手番開始時に停止するかを返す。凶未取得・山札切れ・終了済み・しきい値未満はfalse。</summary>
        public bool ShouldStop(ReadingSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (session.IsFinished || session.BadMarks == 0 || session.Deck.Remaining == 0) return false;
            // 2種類の凶数とジョーカーの残数を共有山札の残数で割った現在の凶札率（0〜1）。
            double badRate = (double)session.CountRemaining(CardOmen.Bad) / session.Deck.Remaining;
            if (badRate < BadRateThreshold) return false;
            return random.NextDouble() < StopProbability;
        }
    }
}
