using R3;
using UnityEngine;

namespace Hikigiwa.Common.Audio
{
    public enum VolumeChannel
    {
        /// <summary>全体。AudioListener.volume に反映します。</summary>
        Master = 0,
        Bgm = 1,
        Se = 2,
    }

    /// <summary>
    /// 音量の設定。値は ReadOnlyReactiveProperty で公開し、PlayerPrefs に保存します。
    /// 全体（Master）は AudioListener.volume に自動で反映します。
    /// BGM・SE は AudioManager が鳴らす音に自動で反映されます。
    /// シーンに直接置いた AudioSource（環境音など）は、VolumeBinder を付けると反映されます。
    /// </summary>
    public sealed class VolumeSettings
    {
        public const float DefaultMaster = 0.8f;
        public const float DefaultBgm = 0.8f;
        public const float DefaultSe = 0.8f;

        private const string KeyPrefix = "Hikigiwa.Volume.";

        private static VolumeSettings instance;

        public static VolumeSettings Instance => instance ??= new VolumeSettings();

        private readonly ReactiveProperty<float> master;
        private readonly ReactiveProperty<float> bgm;
        private readonly ReactiveProperty<float> se;

        public ReadOnlyReactiveProperty<float> Master => master;
        public ReadOnlyReactiveProperty<float> Bgm => bgm;
        public ReadOnlyReactiveProperty<float> Se => se;

        private VolumeSettings()
        {
            master = new ReactiveProperty<float>(Load(VolumeChannel.Master, DefaultMaster));
            bgm = new ReactiveProperty<float>(Load(VolumeChannel.Bgm, DefaultBgm));
            se = new ReactiveProperty<float>(Load(VolumeChannel.Se, DefaultSe));

            master.Subscribe(v => AudioListener.volume = v);
        }

        public ReadOnlyReactiveProperty<float> Get(VolumeChannel channel) => Property(channel);

        /// <summary>音量を 0〜1 に収めて設定します（保存はしません）。</summary>
        public void Set(VolumeChannel channel, float value)
        {
            Property(channel).Value = Mathf.Clamp01(value);
        }

        /// <summary>今の値を PlayerPrefs に保存します。</summary>
        public void Save()
        {
            PlayerPrefs.SetFloat(Key(VolumeChannel.Master), master.Value);
            PlayerPrefs.SetFloat(Key(VolumeChannel.Bgm), bgm.Value);
            PlayerPrefs.SetFloat(Key(VolumeChannel.Se), se.Value);
            PlayerPrefs.Save();
        }

        /// <summary>初期値に戻します（保存はしません）。</summary>
        public void ResetToDefault()
        {
            master.Value = DefaultMaster;
            bgm.Value = DefaultBgm;
            se.Value = DefaultSe;
        }

        private ReactiveProperty<float> Property(VolumeChannel channel) => channel switch
        {
            VolumeChannel.Bgm => bgm,
            VolumeChannel.Se => se,
            _ => master,
        };

        private static string Key(VolumeChannel channel) => KeyPrefix + channel;

        private static float Load(VolumeChannel channel, float defaultValue) =>
            Mathf.Clamp01(PlayerPrefs.GetFloat(Key(channel), defaultValue));

        /// <summary>Domain Reload を切っていても、プレイのたびに読み込み直す。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => instance = null;

        /// <summary>どのシーンから始めても、保存された全体の音量を反映する。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyOnStartup() => _ = Instance;
    }
}
