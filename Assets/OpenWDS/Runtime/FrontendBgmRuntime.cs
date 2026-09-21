using System;
using System.IO;
using CriWare;
using UnityEngine;

namespace OpenWDS.Runtime
{
    /// <summary>Page-owned original CRI cues; Home instrumental is an explicit frozen fixture.</summary>
    public sealed class FrontendBgmRuntime : MonoBehaviour
    {
        private CriAtomExAcb _acb;
        private CriAtomExPlayer _player;
        public string Cue { get; private set; }
        public CriAtomExPlayback Playback { get; private set; }
        public bool IsPlaying => _player != null && _player.GetStatus() == CriAtomExPlayer.Status.Playing;
        public void Play(bool title)
        {
            Stop();
            Cue = title ? "TITLE" : "inst_bgm";
            string relative = title ? "OpenWDS/CRI/BGM.acb" : "OpenWDS/Frontend/inst_1.acb";
            _acb = CriAtomExAcb.LoadAcbFile(null, Path.Combine(CriWare.Common.streamingAssetsPath, relative), null);
            if (_acb == null || !_acb.GetCueInfo(Cue, out _))
                throw new InvalidOperationException("Frontend BGM cue missing: " + relative + "/" + Cue);
            var sound = new SettingsStore().LoadOrDefault().SoundVolumeSettings;
            _player = new CriAtomExPlayer(true);
            _player.SetVolume(sound.SystemMaster * sound.SystemBGM / 10000f);
            _player.SetCue(_acb, Cue);
            Playback = _player.Start();
        }
        public void Stop()
        {
            _player?.Stop(); _player?.Dispose(); _player = null;
            _acb?.Dispose(); _acb = null;
            Cue = null;
        }
        private void OnDestroy() { Stop(); }
    }
}
