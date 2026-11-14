namespace Hikigiwa.Common.Audio
{
    /// <summary>
    /// BGMの種類。SoundLibrary でクリップを割り当てます。
    /// 番号は上から自動で振られ、SoundLibrary はその番号で割り当てを保存しています。
    /// 追加は必ず一番最後にしてください（途中に入れる・並べ替える・消すと、登録済みの割り当てがずれます）。
    /// </summary>
    public enum BgmId
    {
        /// <summary>BGMなし（止める）。</summary>
        None,
        Title,
        /// <summary>ソロ（Game シーン）。</summary>
        Solo,
        /// <summary>CPU対戦。</summary>
        Battle,
    }

    /// <summary>
    /// 効果音の種類。SoundLibrary でクリップを割り当てます。
    /// 番号は上から自動で振られ、SoundLibrary はその番号で割り当てを保存しています。
    /// 追加は必ず一番最後にしてください（途中に入れる・並べ替える・消すと、登録済みの割り当てがずれます）。
    /// </summary>
    public enum SeId
    {
        None,

        // UI
        /// <summary>ボタンなどをクリックした（決定）。オプションの音量の試し音にも使う。</summary>
        Decide,
        /// <summary>ボタンなどにカーソルを合わせた。</summary>
        UiHover,

        // カード
        /// <summary>カードをめくった（吉・凶に関係なく、めくるたびに鳴らす）。</summary>
        CardDraw,
        /// <summary>ジョーカーを引いた（めくって表が見えたときに、CardDraw に重ねて鳴らす）。</summary>
        JokerDraw,
        /// <summary>吉数のカードを引いた（めくって表が見えたときに、CardDraw に重ねて鳴らす）。</summary>
        LuckyDraw,
        /// <summary>凶数のカード（ジョーカー以外）を引いた（めくって表が見えたときに、CardDraw に重ねて鳴らす）。</summary>
        UnluckyDraw,
    }
}
