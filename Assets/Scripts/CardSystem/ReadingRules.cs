namespace Hikigiwa.CardSystem
{
    /// <summary>
    /// 鑑定ルールの数値。バランス調整はここの値を変えるだけで済むようにしています。
    /// </summary>
    public sealed class ReadingRules
    {
        /// <summary>鑑定開始時の倍率。</summary>
        public int StartMultiplier = 1;

        /// <summary>吉数を引いたときに増える倍率。</summary>
        public int LuckyMultiplierStep = 1;

        /// <summary>この数だけ凶の印が付くと破綻。</summary>
        public int BadMarksToBreak = 2;

        /// <summary>凶数を引いたとき、その数字を運気に加算するか（ジョーカーは常に0）。</summary>
        public bool BadNumberAddsLuck = true;

        /// <summary>予感的中ボーナス：終了時に山札の一番上が凶なら売上に掛ける倍率。</summary>
        public double ForesightBonusRate = 1.5;

        /// <summary>満足ラインを達成して終えたときのチップ。</summary>
        public int Tip = 20;

        /// <summary>無事に終えたときに回復する霊力。</summary>
        public int SpiritOnSafeFinish = 1;

        /// <summary>満足ライン（運気）の範囲と刻み。</summary>
        public int SatisfactionLineMin = 20;
        public int SatisfactionLineMax = 40;
        public int SatisfactionLineStep = 5;

        /// <summary>占い道具に必要な霊力。</summary>
        public int CrystalCost = 1;
        public int TarotCost = 2;
        public int IncenseCost = 2;
        public int PalmCost = 1;

        /// <summary>タロットで見えるカードの枚数。</summary>
        public int TarotCards = 3;

        /// <summary>お香を1人の客に使える回数（凶で止める緊張感を弱めすぎないため）。</summary>
        public int IncenseLimitPerCustomer = 1;

        public int CostOf(FortuneTool tool)
        {
            switch (tool)
            {
                case FortuneTool.Crystal: return CrystalCost;
                case FortuneTool.Tarot: return TarotCost;
                case FortuneTool.Incense: return IncenseCost;
                default: return PalmCost;
            }
        }
    }

    /// <summary>夜・ノルマ・霊力の初期値など、プレイ全体のルールの数値。</summary>
    public sealed class RunRules
    {
        /// <summary>1夜の客の数。</summary>
        public int CustomersPerNight = 6;

        /// <summary>夜ごとのノルマ（スコア）。配列の長さが夜の数になる。</summary>
        public int[] Quotas = { 180, 240, 300 };

        /// <summary>ゲーム開始時の霊力。</summary>
        public int StartSpirit = 3;

        /// <summary>ノルマ未達でゲームオーバーにするか。falseなら最後の夜まで続く。</summary>
        public bool QuotaFailEndsGame = true;

        public int Nights => Quotas.Length;
    }
}
