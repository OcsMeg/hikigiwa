using System;

namespace Hikigiwa.CardSystem
{
    /// <summary>手番・操作対象の識別子。各関数のside引数はこの型で対象を指定する。</summary>
    public enum BattleSide
    {
        /// <summary>画面やキー入力で操作する人間側。</summary>
        Player,
        /// <summary>CpuBattleStrategyの判断で操作する側。</summary>
        Cpu
    }

    /// <summary>対戦者ごとの記録。霊力と累計点だけをラウンド間で持ち越す。</summary>
    public sealed class BattleParticipant
    {
        /// <summary>累計得点と霊力の保存先。対戦ではPlayRecordの夜・ノルマ判定は使用しない。</summary>
        public PlayRecord Record { get; } = new PlayRecord(new RunRules());
        /// <summary>現在のラウンドにおける本人の鑑定状態。次ラウンドの開始時に作り直す。</summary>
        public ReadingSession Session { get; internal set; }
        /// <summary>正常精算したラウンドの得点を加算した対戦全体の累計。</summary>
        public int TotalScore => Record.TotalScore;
        /// <summary>現在使える本人の霊力。ラウンドをまたいで持ち越す。</summary>
        public int Spirit => Record.Spirit;
    }

    /// <summary>
    /// 共有山札を使う3ラウンドの対戦。入力・CPU判断・表示には依存しない。
    /// 道具の使用では手番を消費せず、引く／辞めるで相手へ渡す。
    /// </summary>
    public sealed class BattleMatch
    {
        /// <summary>1対戦で行うラウンド数。最終ラウンド終了後に累計得点を比較する。</summary>
        public const int RoundCount = 3;
        /// <summary>共通条件・山札・先手の抽選に使う乱数。CPUの停止抽選とは分離している。</summary>
        private readonly Random random;
        /// <summary>運気・倍率・道具など、各ReadingSessionで共有する数値設定。</summary>
        private readonly ReadingRules rules;
        /// <summary>人間側の鑑定状態と持ち越し記録。</summary>
        public BattleParticipant Player { get; } = new BattleParticipant();
        /// <summary>CPU側の鑑定状態と持ち越し記録。</summary>
        public BattleParticipant Cpu { get; } = new BattleParticipant();
        /// <summary>二人で共有する今ラウンドの山札。どちらが引いても同じ山札から減る。</summary>
        public Deck Deck { get; private set; }
        /// <summary>二人に共通する吉数・凶数2種類。</summary>
        public Customer Conditions { get; private set; }
        /// <summary>現在引く／辞めるを選べる側。道具の使用では変更しない。</summary>
        public BattleSide Turn { get; private set; }
        /// <summary>今ラウンドの先手。開始時の案内表示にも使う。</summary>
        public BattleSide FirstSide { get; private set; }
        /// <summary>現在のラウンド番号。1から始まりRoundCountまで進む。</summary>
        public int Round { get; private set; }
        /// <summary>二人とも停止または破綻し、ラウンドの得点が確定しているか。</summary>
        public bool IsRoundOver => Player.Session != null && Player.Session.IsFinished && Cpu.Session.IsFinished;
        /// <summary>最終ラウンドの精算まで終わり、勝敗を表示できるか。</summary>
        public bool IsMatchOver => Round == RoundCount && IsRoundOver;
        /// <summary>現在の手番側の状態をTurnから取得する。</summary>
        public BattleParticipant Current => Get(Turn);

        /// <summary>乱数と任意のルールを受け取り、第1ラウンドを開始する。</summary>
        public BattleMatch(Random random, ReadingRules rules = null)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.rules = rules ?? new ReadingRules();
            StartNextRound();
        }

        /// <summary>指定した側の鑑定状態と持ち越し記録を返す。</summary>
        public BattleParticipant Get(BattleSide side) => side == BattleSide.Player ? Player : Cpu;
        /// <summary>指定した側の対戦相手を返す。</summary>
        public static BattleSide Other(BattleSide side) => side == BattleSide.Player ? BattleSide.Cpu : BattleSide.Player;

        /// <summary>初回、または前ラウンド終了後だけ新しい条件・山札・先手を設定する。対戦終了後はfalse。</summary>
        public bool StartNextRound()
        {
            if (Round > 0 && (!IsRoundOver || IsMatchOver)) return false;
            Round++;
            Conditions = Customer.CreateRandom(random, rules, badNumberCount: 2);
            Deck = new Deck(random);
            BeginSession(Player);
            BeginSession(Cpu);
            FirstSide = Turn = random.Next(2) == 0 ? BattleSide.Player : BattleSide.Cpu;
            return true;
        }

        /// <summary>本人の鑑定だけを初期化し、終了時に得点と回復霊力を持ち越し記録へ一度加算する。</summary>
        private void BeginSession(BattleParticipant participant)
        {
            participant.Session = new ReadingSession(Conditions, Deck, rules);
            participant.Session.Finished += participant.Record.Add;
        }

        /// <summary>手番を検証して共有山札から1枚引き、破綻・山札切れを処理して手番を進める。</summary>
        public bool TryDraw(BattleSide side, out DrawResult draw)
        {
            draw = default;
            if (!CanAct(side) || !Get(side).Session.TryDraw(out draw)) return false;
            CompleteTurn(side);
            return true;
        }

        /// <summary>本人の得点を確定する。相手が終了済みのときだけ予感判定を許可し、手番を進める。</summary>
        public bool TryStop(BattleSide side, out ReadingResult result)
        {
            result = null;
            if (!CanAct(side)) return false;
            // 相手が停止・破綻済みなら本人が最後の側なので予感を許可する。先に停止する側には公開しない。
            bool foresight = Get(Other(side)).Session.IsFinished;
            if (!Get(side).Session.TryStop(out result, foresight)) return false;
            CompleteTurn(side);
            return true;
        }

        /// <summary>プレイヤーの手番中だけ道具を使う。霊力を消費するが、通常は手番を維持する。</summary>
        public bool TryUseTool(FortuneTool tool, out ToolResult result)
        {
            result = null;
            if (!CanAct(BattleSide.Player)) return false;
            if (!Player.Session.TryUseTool(tool, Player.Record, out result)) return false;
            // お香で最後の1枚を除いた場合も、山札切れとして通常精算する。
            if (Deck.Remaining == 0) SettleEmptyDeck();
            return true;
        }

        /// <summary>未終了のラウンドで、指定した側が手番中かつ鑑定中かを確認する。</summary>
        public bool CanAct(BattleSide side) => !IsRoundOver && Turn == side && !Get(side).Session.IsFinished;

        /// <summary>引く／辞めるの後処理。相手が終了済みなら本人の続行、そうでなければ相手の手番にする。</summary>
        private void CompleteTurn(BattleSide side)
        {
            if (Deck.Remaining == 0) SettleEmptyDeck();
            if (IsRoundOver) return;
            Turn = Get(Other(side)).Session.IsFinished ? side : Other(side);
        }

        /// <summary>続行中の側を予感ボーナスなしで通常精算する。先に確定した結果は変更しない。</summary>
        private void SettleEmptyDeck()
        {
            if (!Player.Session.IsFinished) Player.Session.TryStop(out _, false);
            if (!Cpu.Session.IsFinished) Cpu.Session.TryStop(out _, false);
        }
    }
}
