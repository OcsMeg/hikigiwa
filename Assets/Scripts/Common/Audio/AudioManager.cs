using System.Collections.Generic;
using DG.Tweening;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hikigiwa.Common.Audio
{
    /// <summary>
    /// BGMと効果音を鳴らす。ゲーム起動時に自動で生成され、シーンをまたいで残ります（シーンに置く必要はありません）。
    /// 音量は VolumeSettings に追従するので、オプションのスライダーを動かすとすぐに反映されます。
    /// </summary>
    /// <example>
    /// <code>
    /// AudioManager.Instance.PlaySe(SeId.CardDraw);
    /// AudioManager.Instance.PlayBgm(BgmId.Battle);
    /// </code>
    /// </example>
    /// <remarks>
    /// クリップは Assets/Resources/SoundLibrary.asset に登録します。未登録の音は鳴らさず、エラーにもしません。
    /// シーンごとのBGMは SoundLibrary の「シーンとBGM」に書けば、シーンに入ったときに自動で切り替わります。
    /// 全体音量（Master）は VolumeSettings が AudioListener に反映するので、ここでは掛けません。
    /// </remarks>
    public sealed class AudioManager : DontDestroySingletonBase<AudioManager>
    {
        /// <summary>Resources から読み込む SoundLibrary のパス。</summary>
        private const string LibraryPath = "SoundLibrary";
        /// <summary>BGMを切り替えるときのクロスフェードの秒数（既定）。</summary>
        public const float DefaultBgmFadeSeconds = 0.8f;
        /// <summary>同じ効果音をこの秒数以内に重ねて鳴らさない（音割れ防止）。</summary>
        private const float SameSeInterval = 0.05f;

        private SoundLibrary library;

        /// <summary>BGMの音源1つ分。フェードの進み具合と、使用中かどうかを自分で持つ。</summary>
        private sealed class BgmChannel
        {
            public AudioSource Source;
            /// <summary>フェードの進み具合（0〜1）。</summary>
            public float Fade;
            /// <summary>流している曲そのものの大きさ。</summary>
            public float BaseVolume;
            public Tween Tween;
            /// <summary>
            /// 再生中（フェードアウト中を含む）か。AudioSource.isPlaying は WebGL では Play() の直後に
            /// false のことがある（読み込みが非同期のため）ので、自分で記録する。
            /// </summary>
            public bool Busy;
        }

        // BGMの音源。切り替えるたびに空いている音源で次の曲を流し、前の曲は別の音源でフェードアウトさせる（クロスフェード）。
        // フェードアウト中にさらに切り替えても、その音源には触らず空いている音源を使う（足りなければ増やす）ので、音が途切れない。
        private readonly List<BgmChannel> bgmChannels = new List<BgmChannel>();
        /// <summary>今流している（フェードイン中を含む）曲の音源。止めているときは null。</summary>
        private BgmChannel currentChannel;

        private AudioSource seSource;
        /// <summary>効果音ごとに、最後に鳴らした時刻。</summary>
        private readonly Dictionary<SeId, float> lastSeTime = new Dictionary<SeId, float>();
        /// <summary>未登録の警告を出した音。同じ警告を何度も出さないために使う。</summary>
        private readonly HashSet<string> warnedMissing = new HashSet<string>();

        /// <summary>今流しているBGM。止めているときは None。</summary>
        public BgmId CurrentBgm { get; private set; } = BgmId.None;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateOnStartup()
        {
            if (HasInstance) return;
            new GameObject(nameof(AudioManager)).AddComponent<AudioManager>();
        }

        protected override void OnAwake()
        {
            base.OnAwake();
            library = Resources.Load<SoundLibrary>(LibraryPath);
            if (library == null)
                Debug.LogWarning($"Resources/{LibraryPath} が見つからないため、音は鳴りません。", this);

            seSource = CreateSource("Se");

            VolumeSettings settings = VolumeSettings.Instance;
            settings.Bgm.Subscribe(_ => ApplyBgmVolumes()).AddTo(this);
            settings.Se.Subscribe(v => seSource.volume = v).AddTo(this);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>
        /// 最初のシーンのBGMを流す。エディタで再生を始めたときなどは最初のシーンの sceneLoaded が届かないことがあるため、
        /// 起動直後にも今のシーンを確かめる（sceneLoaded も届いた場合は同じ曲なので二重には鳴らない）。
        /// </summary>
        private void Start() => PlaySceneBgm(SceneManager.GetActiveScene());

        protected override void OnDestroyed()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            foreach (BgmChannel channel in bgmChannels) channel.Tween?.Kill();
        }

        /// <summary>
        /// BGMを流します。同じ曲が流れていれば何もしません。前の曲とはクロスフェードで入れ替えます。
        /// 曲が SoundLibrary に未登録なら、今の曲をそのまま流し続けます（CurrentBgm も変えません）。
        /// </summary>
        /// <param name="id">流す曲。None なら止める。</param>
        /// <param name="fadeSeconds">切り替えにかける秒数。0 ならすぐに切り替える。</param>
        public void PlayBgm(BgmId id, float fadeSeconds = DefaultBgmFadeSeconds)
        {
            if (id == CurrentBgm) return;

            // 先に曲があるかを確かめる（無いのに今の曲を消すと、無音になってしまうため）
            SoundLibrary.BgmEntry entry = null;
            if (id != BgmId.None && (library == null || !library.TryGetBgm(id, out entry)))
            {
                WarnMissing($"BGM「{id}」");
                return;
            }
            CurrentBgm = id;

            // 今の曲を消していく（すでにフェードアウト中の音源はそのまま続ける）
            if (currentChannel != null)
            {
                FadeOutAndStop(currentChannel, fadeSeconds);
                currentChannel = null;
            }
            if (id == BgmId.None) return;

            // 空いている音源で、新しい曲を出していく
            BgmChannel channel = GetIdleChannel();
            channel.Source.clip = entry.clip;
            channel.BaseVolume = entry.volume;
            channel.Fade = fadeSeconds > 0f ? 0f : 1f;
            channel.Busy = true;
            ApplyBgmVolume(channel);
            channel.Source.Play();
            if (fadeSeconds > 0f) channel.Tween = Fade(channel, 1f, fadeSeconds).OnComplete(() => channel.Tween = null);
            currentChannel = channel;
        }

        /// <summary>BGMを止めます。</summary>
        public void StopBgm(float fadeSeconds = DefaultBgmFadeSeconds) => PlayBgm(BgmId.None, fadeSeconds);

        /// <summary>効果音を鳴らします。未登録の音や、同じ音を短い間隔で重ねたときは鳴らしません。</summary>
        public void PlaySe(SeId id)
        {
            if (id == SeId.None) return;
            if (library == null || !library.TryGetSe(id, out SoundLibrary.SeEntry entry))
            {
                WarnMissing($"効果音「{id}」");
                return;
            }

            float now = Time.unscaledTime;
            if (lastSeTime.TryGetValue(id, out float last) && now - last < SameSeInterval) return;
            lastSeTime[id] = now;

            // seSource.volume（オプションのSE音量）に、この音そのものの大きさが掛けられる
            seSource.PlayOneShot(entry.clip, entry.volume);
        }

        /// <summary>シーンに入ったとき、SoundLibrary に書かれたBGMへ切り替えます（書かれていなければそのまま）。</summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) PlaySceneBgm(scene);
        }

        private void PlaySceneBgm(Scene scene)
        {
            if (library == null) return;
            if (library.TryGetSceneBgm(scene.name, out BgmId bgm)) PlayBgm(bgm);
        }

        /// <summary>今の大きさから消していき、消えたら止めて空きにする。</summary>
        private void FadeOutAndStop(BgmChannel channel, float seconds)
        {
            channel.Tween?.Kill();
            if (seconds <= 0f)
            {
                Stop(channel);
                return;
            }
            channel.Tween = Fade(channel, 0f, seconds).OnComplete(() => Stop(channel));
        }

        private void Stop(BgmChannel channel)
        {
            channel.Tween?.Kill();
            channel.Tween = null;
            channel.Source.Stop();
            channel.Source.clip = null;
            channel.Fade = 0f;
            channel.Busy = false;
        }

        private Tween Fade(BgmChannel channel, float target, float seconds) => DOTween
            .To(() => channel.Fade, v => { channel.Fade = v; ApplyBgmVolume(channel); }, target, seconds)
            .SetEase(Ease.Linear)
            .SetUpdate(true) // ポーズ中（Time.timeScale = 0）でも動かす
            .SetLink(gameObject);

        /// <summary>使っていない音源を返す。すべて使用中（フェードアウト中を含む）なら新しく作る。</summary>
        private BgmChannel GetIdleChannel()
        {
            foreach (BgmChannel channel in bgmChannels)
                if (!channel.Busy) return channel;

            var created = new BgmChannel { Source = CreateSource($"Bgm{bgmChannels.Count}") };
            created.Source.loop = true;
            bgmChannels.Add(created);
            return created;
        }

        /// <summary>クリップが未登録の音を鳴らそうとしたことを、音ごとに1回だけ知らせる。</summary>
        private void WarnMissing(string what)
        {
            if (library == null || !warnedMissing.Add(what)) return; // SoundLibrary 自体が無いときは起動時に警告済み
            Debug.LogWarning($"{what} のクリップが SoundLibrary に登録されていないため、鳴らしません。", library);
        }

        private void ApplyBgmVolumes()
        {
            foreach (BgmChannel channel in bgmChannels) ApplyBgmVolume(channel);
        }

        /// <summary>BGM音源の音量＝フェードの進み具合 × 曲そのものの大きさ × オプションのBGM音量。</summary>
        private static void ApplyBgmVolume(BgmChannel channel)
        {
            channel.Source.volume = channel.Fade * channel.BaseVolume * VolumeSettings.Instance.Bgm.CurrentValue;
        }

        private AudioSource CreateSource(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2Dの音（カメラとの距離で変わらない）
            return source;
        }
    }
}
