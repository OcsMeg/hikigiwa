using R3;
using UnityEngine;

namespace Hikigiwa.Common.Audio
{
    /// <summary>
    /// AudioSource の音量を、VolumeSettings の BGM か SE の値に追従させます。
    /// 全体（Master）は AudioListener に反映済みなので、ここでは選ばないでください（二重にかかります）。
    /// 通常のBGM・効果音は AudioManager で鳴らしてください。これはシーンに直接置く音源（環境音など）用です。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class VolumeBinder : MonoBehaviour
    {
        [SerializeField] private VolumeChannel channel = VolumeChannel.Bgm;
        [Tooltip("この音そのものの大きさ。設定の音量はこれに掛けられます。")]
        [SerializeField, Range(0f, 1f)] private float baseVolume = 1f;

        private void Start()
        {
            var source = GetComponent<AudioSource>();
            VolumeSettings.Instance.Get(channel)
                .Subscribe(v => source.volume = baseVolume * v)
                .AddTo(this);
        }
    }
}
