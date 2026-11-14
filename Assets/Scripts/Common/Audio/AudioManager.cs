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

        // BGMは2つの AudioSource を交互に使い、片方を消しながらもう片方を出す（クロスフェード）
        private readonly AudioSource[] bgmSources = new AudioSource[2];
        /// <summary>各BGM音源のフェードの進み具合（0〜1）。</summary>
        private readonly float[] bgmFade = new float[2];
        /// <summary>各BGM音源で流している曲そのものの大きさ。</summary>
        private readonly float[] bgmBaseVolume = new float[2];
        private readonly Tween[] bgmTweens = new Tween[2];
        /// <summary>今流している（または最後に流し始めた）BGM音源の番号。</summary>
        private int activeBgm;

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

            for (int i = 0; i < bgmSources.Length; i++)
            {
                bgmSources[i] = CreateSource($"Bgm{i}");
                bgmSources[i].loop = true;
            }
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
            foreach (Tween tween in bgmTweens) tween?.Kill();
        }

        /// <summary>
        /// BGMを流します。同じ曲が流れていれば何もしません。前の曲とはクロスフェードで入れ替えます。
        /// </summary>
        /// <param name="id">流す曲。None なら止める。</param>
        /// <param name="fadeSeconds">切り替えにかける秒数。0 ならすぐに切り替える。</param>
        public void PlayBgm(BgmId id, float fadeSeconds = DefaultBgmFadeSeconds)
        {
            if (id == CurrentBgm) return;
            CurrentBgm = id;

            // 今の曲を消していく
            FadeBgm(activeBgm, 0f, fadeSeconds, stopWhenDone: true);

            if (id == BgmId.None) return;
            if (library == null || !library.TryGetBgm(id, out SoundLibrary.BgmEntry entry))
            {
                WarnMissing($"BGM「{id}」");
                return;
            }

            // もう片方の音源で、新しい曲を出していく
            activeBgm = 1 - activeBgm;
            AudioSource source = bgmSources[activeBgm];
            bgmTweens[activeBgm]?.Kill();
            source.clip = entry.clip;
            bgmBaseVolume[activeBgm] = entry.volume;
            bgmFade[activeBgm] = fadeSeconds > 0f ? 0f : 1f;
            ApplyBgmVolume(activeBgm);
            source.Play();
            FadeBgm(activeBgm, 1f, fadeSeconds, stopWhenDone: false);
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

        private void FadeBgm(int index, float target, float seconds, bool stopWhenDone)
        {
            bgmTweens[index]?.Kill();
            AudioSource source = bgmSources[index];
            if (!source.isPlaying) return;

            if (seconds <= 0f)
            {
                bgmFade[index] = target;
                ApplyBgmVolume(index);
                if (stopWhenDone) source.Stop();
                return;
            }

            bgmTweens[index] = DOTween
                .To(() => bgmFade[index], v => { bgmFade[index] = v; ApplyBgmVolume(index); }, target, seconds)
                .SetEase(Ease.Linear)
                .SetUpdate(true) // ポーズ中（Time.timeScale = 0）でも動かす
                .OnComplete(() => { if (stopWhenDone) source.Stop(); })
                .SetLink(gameObject);
        }

        /// <summary>クリップが未登録の音を鳴らそうとしたことを、音ごとに1回だけ知らせる。</summary>
        private void WarnMissing(string what)
        {
            if (library == null || !warnedMissing.Add(what)) return; // SoundLibrary 自体が無いときは起動時に警告済み
            Debug.LogWarning($"{what} のクリップが SoundLibrary に登録されていないため、鳴らしません。", library);
        }

        private void ApplyBgmVolumes()
        {
            for (int i = 0; i < bgmSources.Length; i++) ApplyBgmVolume(i);
        }

        /// <summary>BGM音源の音量＝フェードの進み具合 × 曲そのものの大きさ × オプションのBGM音量。</summary>
        private void ApplyBgmVolume(int index)
        {
            if (bgmSources[index] == null) return;
            bgmSources[index].volume = bgmFade[index] * bgmBaseVolume[index] * VolumeSettings.Instance.Bgm.CurrentValue;
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
