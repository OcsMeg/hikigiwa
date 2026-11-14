using System;
using System.Collections.Generic;

namespace Hikigiwa.CardSystem
{
    /// <summary>1夜が終わったときの結果。</summary>
    public sealed class NightSummary
    {
        public int Night { get; internal set; }
        public int Score { get; internal set; }
        public int Quota { get; internal set; }
        public bool Passed => Score >= Quota;
        public int SpiritGained { get; internal set; }
        public bool IsLastNight { get; internal set; }
    }

    /// <summary>
    /// プレイ全体の記録（夜・ノルマ・スコア・霊力）。
    /// </summary>
    public sealed class PlayRecord
    {
        private readonly RunRules rules;
        private readonly List<NightSummary> nightHistory = new List<NightSummary>();

        /// <summary>終えた夜の結果（第1夜から順）。</summary>
        public IReadOnlyList<NightSummary> NightHistory => nightHistory;

        /// <summary>今が何夜目か（1から）。</summary>
        public int Night { get; private set; } = 1;
        /// <summary>今夜すでに鑑定を終えた客の数。</summary>
        public int CustomersThisNight { get; private set; }
        public int NightScore { get; private set; }
        public int TotalScore { get; private set; }
        public int Spirit { get; private set; }
        public int CustomersServed { get; private set; }
        public int BrokenCount { get; private set; }
        public bool IsGameOver { get; private set; }
        public bool IsCleared { get; private set; }

        public int Nights => rules.Nights;
        public int CustomersPerNight => rules.CustomersPerNight;
        public int Quota => rules.Quotas[Math.Min(Night - 1, rules.Quotas.Length - 1)];
        public bool IsNightOver => CustomersThisNight >= rules.CustomersPerNight;
        public bool IsFinished => IsGameOver || IsCleared;

        public PlayRecord(RunRules rules)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            if (rules.Quotas == null || rules.Quotas.Length == 0)
                throw new ArgumentException("ノルマを1夜分以上設定してください。");
            Spirit = rules.StartSpirit;
        }

        /// <summary>客1人分の結果を加えます。</summary>
        public void Add(ReadingResult result)
        {
            CustomersServed++;
            CustomersThisNight++;
            NightScore += result.Score;
            TotalScore += result.Score;
            Spirit += result.SpiritGained;
            if (result.IsBroken) BrokenCount++;
        }

        /// <summary>霊力を払います。足りなければfalseで、何も変えません。</summary>
        public bool TrySpendSpirit(int amount)
        {
            if (amount < 0 || Spirit < amount) return false;
            Spirit -= amount;
            return true;
        }

        /// <summary>今夜を締めてノルマを判定します。</summary>
        public NightSummary EndNight()
        {
            // 同じ夜を再度締めても、報酬や履歴を重複して加算しない。
            if (nightHistory.Count > 0 && nightHistory[nightHistory.Count - 1].Night == Night)
                return nightHistory[nightHistory.Count - 1];

            var summary = new NightSummary
            {
                Night = Night,
                Score = NightScore,
                Quota = Quota,
                IsLastNight = Night >= rules.Nights,
            };

            if (summary.Passed)
            {
                summary.SpiritGained = Math.Max(0, rules.SpiritOnQuotaClear);
                Spirit += summary.SpiritGained;
            }
            nightHistory.Add(summary);
            if (!summary.Passed && rules.QuotaFailEndsGame) IsGameOver = true;
            else if (summary.IsLastNight) IsCleared = true;
            return summary;
        }

        /// <summary>次の夜へ進みます。</summary>
        public void StartNextNight()
        {
            if (IsFinished) return;
            Night++;
            CustomersThisNight = 0;
            NightScore = 0;
        }
    }
}
