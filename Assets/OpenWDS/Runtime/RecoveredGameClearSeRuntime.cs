using System;
using System.IO;
using CriWare;
using UnityEngine;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Recovered GameClearSePlayer backed by the APK's dedicated ClearSE bank.
    /// Retail playback converts GameConst.ClearType directly to a cue name.
    /// </summary>
    public sealed class RecoveredGameClearSeRuntime : MonoBehaviour
    {
        [SerializeField] private string _acbRelativePath =
            "OpenWDS/CRI/ClearSE.acb";
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;

        private CriAtomExAcb _acb;
        private CriAtomExPlayer _player;

        public bool IsInitialized => _acb != null && _player != null;
        public string LastCueName { get; private set; }
        public int PlayCount { get; private set; }
        public CriAtomExPlayback LastPlayback { get; private set; }

        private void Awake()
        {
            LastPlayback =
                new CriAtomExPlayback(CriAtomExPlayback.invalidId);
        }

        public void Play(Sirius.Game.RecoveredBoundaryClearType clearType)
        {
            Play(GetCueName(clearType));
        }

        public void Play(string cueName)
        {
            if (string.IsNullOrEmpty(cueName))
                throw new ArgumentException(
                    "A clear SE cue name is required.", nameof(cueName));
            EnsureInitialized();
            if (!_acb.GetCueInfo(cueName, out _))
                throw new InvalidOperationException(
                    "Original ClearSE cue is missing: " + cueName);
            _player.SetCue(_acb, cueName);
            LastPlayback = _player.Start();
            LastCueName = cueName;
            PlayCount++;
        }

        public void SetVolume(float volume)
        {
            _volume = Mathf.Clamp01(volume);
            if (_player == null) return;
            _player.SetVolume(_volume);
            _player.UpdateAll();
        }

        public static string GetCueName(
            Sirius.Game.RecoveredBoundaryClearType clearType)
        {
            switch (clearType)
            {
                case Sirius.Game.RecoveredBoundaryClearType.Failed:
                    // Retail ClearType.Failed has no matching bank cue. The
                    // ordinary failed boundary uses the bank's Finish cue;
                    // AprilFool_FAILED belongs to the distinct enum value 6.
                    return "Finish";
                case Sirius.Game.RecoveredBoundaryClearType.Clear:
                    return "Clear";
                case Sirius.Game.RecoveredBoundaryClearType.FullCombo:
                    return "FullCombo";
                case Sirius.Game.RecoveredBoundaryClearType.AllPerfect:
                    return "AllPerfect";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(clearType), clearType, null);
            }
        }

        private void EnsureInitialized()
        {
            if (IsInitialized) return;
            var path = Path.Combine(
                CriWare.Common.streamingAssetsPath, _acbRelativePath);
            _acb = CriAtomExAcb.LoadAcbFile(null, path, null);
            if (_acb == null)
                throw new InvalidOperationException(
                    "Original ClearSE.acb failed to load.");
            _player = new CriAtomExPlayer();
            _player.SetVolume(_volume);
            foreach (Sirius.Game.RecoveredBoundaryClearType clearType in
                     Enum.GetValues(
                         typeof(Sirius.Game.RecoveredBoundaryClearType)))
            {
                var cueName = GetCueName(clearType);
                if (!_acb.GetCueInfo(cueName, out _))
                    throw new InvalidOperationException(
                        "Original ClearSE cue is missing: " + cueName);
            }
        }

        private void OnDestroy()
        {
            _player?.Dispose();
            _acb?.Dispose();
            _player = null;
            _acb = null;
        }
    }
}
