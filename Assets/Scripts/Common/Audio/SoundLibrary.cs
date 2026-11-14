using System;
using UnityEngine;

namespace Hikigiwa.Common.Audio
{
    /// <summary>
    /// BGM・効果音のクリップと、シーンごとに流すBGMの対応表。
    /// Assets/Resources/SoundLibrary.asset を AudioManager が起動時に読み込みます。
    /// 音の差し替えはこのアセットを編集するだけで済み、コードを変える必要はありません。
    /// </summary>
    [CreateAssetMenu(menuName = "Hikigiwa/Sound Library", fileName = "SoundLibrary")]
    public sealed class SoundLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class BgmEntry
        {
            public BgmId id;
            public AudioClip clip;
            [Tooltip("この曲そのものの大きさ。オプションのBGM音量はこれに掛けられます。")]
            [Range(0f, 1f)] public float volume = 1f;
        }

        [Serializable]
        public sealed class SeEntry
        {
            public SeId id;
            public AudioClip clip;
            [Tooltip("この音そのものの大きさ。オプションのSE音量はこれに掛けられます。")]
            [Range(0f, 1f)] public float volume = 1f;
        }

        [Serializable]
        public sealed class SceneBgm
        {
            [Tooltip("シーン名（例：Title, Game, CPUBattle）")]
            public string sceneName;
            [Tooltip("このシーンに入ったときに流すBGM。None なら止める")]
            public BgmId bgm;
        }

        [SerializeField] private BgmEntry[] bgms = Array.Empty<BgmEntry>();
        [SerializeField] private SeEntry[] ses = Array.Empty<SeEntry>();
        [Tooltip("ここに無いシーンに入ったときは、流れているBGMをそのまま続けます")]
        [SerializeField] private SceneBgm[] sceneBgms = Array.Empty<SceneBgm>();

        /// <summary>BGMの登録を探します。クリップが未設定なら false。</summary>
        public bool TryGetBgm(BgmId id, out BgmEntry entry)
        {
            entry = Array.Find(bgms, e => e != null && e.id == id);
            return entry != null && entry.clip != null;
        }

        /// <summary>効果音の登録を探します。クリップが未設定なら false。</summary>
        public bool TryGetSe(SeId id, out SeEntry entry)
        {
            entry = Array.Find(ses, e => e != null && e.id == id);
            return entry != null && entry.clip != null;
        }

        /// <summary>シーンに対応するBGMを探します。登録が無ければ false。</summary>
        public bool TryGetSceneBgm(string sceneName, out BgmId bgm)
        {
            SceneBgm found = Array.Find(sceneBgms, e => e != null && e.sceneName == sceneName);
            bgm = found != null ? found.bgm : BgmId.None;
            return found != null;
        }
    }
}
