using System;
using System.IO;
using CriWare;
using UnityEngine;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Local counterpart of BgmMessageBroker.OnPlay(BgmType.GameResult, ...).
    /// The retail enum value is 2 and resolves to the GameResult cue in BGM.acb.
    /// </summary>
    public sealed class RecoveredGameResultBgmRuntime : MonoBehaviour
    {
        public const string CueName = "GameResult";

        [SerializeField] private string _acbRelativePath =
            "OpenWDS/CRI/BGM.acb";
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;

        private CriAtomExAcb _acb;
        private CriAtomExPlayer _player;

        public bool IsInitialized => _acb != null && _player != null;
        public int PlayCount { get; private set; }
        public CriAtomExPlayback LastPlayback { get; private set; }

        private void Awake()
        {
            LastPlayback =
                new CriAtomExPlayback(CriAtomExPlayback.invalidId);
        }

        public void Play()
        {
            EnsureInitialized();
            _player.SetCue(_acb, CueName);
            LastPlayback = _player.Start();
            PlayCount++;
        }

        public void Stop()
        {
            _player?.Stop();
        }

        public void SetVolume(float volume)
        {
            _volume = Mathf.Clamp01(volume);
            if (_player == null) return;
            _player.SetVolume(_volume);
            _player.UpdateAll();
        }

        private void EnsureInitialized()
        {
            if (IsInitialized) return;
            var path = Path.Combine(
                CriWare.Common.streamingAssetsPath, _acbRelativePath);
            _acb = CriAtomExAcb.LoadAcbFile(null, path, null);
            if (_acb == null)
                throw new InvalidOperationException(
                    "Original BGM.acb failed to load.");
            if (!_acb.GetCueInfo(CueName, out _))
                throw new InvalidOperationException(
                    "Original BGM cue is missing: " + CueName);
            _player = new CriAtomExPlayer(true);
            _player.SetVolume(_volume);
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
