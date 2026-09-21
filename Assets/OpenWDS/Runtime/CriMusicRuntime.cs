using System;
using System.IO;
using CriWare;
using UnityEngine;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Exact per-frame cache used by CriMusicPlayer.GetMillisecondsSyncedWithAudio.
    /// Stores the raw CRI sample, including its negative playback-end sentinel.
    /// MusicTime.Tick uses that sentinel to enter the Clear state.
    /// </summary>
    public sealed class FrameCachedAudioClock
    {
        private int _lastFrame = -1;
        private long _lastMilliseconds;

        public int SourceReadCount { get; private set; }
        public long LastMilliseconds => _lastMilliseconds;

        public long Read(int frame, Func<long> readSource)
        {
            if (readSource == null) throw new ArgumentNullException(nameof(readSource));
            if (_lastFrame == frame) return _lastMilliseconds;
            var value = readSource();
            SourceReadCount++;
            _lastMilliseconds = value;
            _lastFrame = frame;
            return _lastMilliseconds;
        }

        public void Reset()
        {
            _lastFrame = -1; _lastMilliseconds = 0; SourceReadCount = 0;
        }

        public void InvalidateFrame()
        {
            _lastFrame = -1;
        }
    }

    /// <summary>
    /// Music 1 implementation of the original CriMusicPlayer prepare/resume path.
    /// The chart clock applies MusicTime's DelayStartSeconds offset to CRI's
    /// audio-synchronized player clock.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class CriMusicRuntime : MonoBehaviour
    {
        [SerializeField] private TextAsset _musicConfigAsset;
        [SerializeField] private string _musicConfigRelativePath =
            "OpenWDS/StandardCharts/1/1/music_config.csv";
        [SerializeField] private string _acbRelativePath =
            "OpenWDS/StandardCharts/1/cri/music_1.acb.bundle";
        [SerializeField] private string _cueName = "1";
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;
        [SerializeField] private bool _resumeImmediately = true;

        private CriAtomExAcb _acb;
        private CriAtomExPlayer _player;
        private CriAtomExPlayback _playback;
        private long _delayMilliseconds;
        private bool _prepared;
        private bool _resumed;
        private bool _paused;
        private bool _gamePaused;
        private bool _applicationPaused;
        private bool _applicationUnfocused;
        private readonly FrameCachedAudioClock _audioClock =
            new FrameCachedAudioClock();

        public bool IsPrepared => _prepared;
        public bool IsPlaying => _resumed && !_paused;
        public bool IsPaused => _paused;
        public bool IsGamePaused => _gamePaused;
        public bool IsApplicationSuspended =>
            _applicationPaused || _applicationUnfocused;
        public int AudioClockSourceReadCount => _audioClock.SourceReadCount;
        public bool HasPlaybackEnded => _resumed && !_paused &&
            _audioClock.Read(Time.frameCount, _playback.GetTimeSyncedWithAudio) < 0;

        public void Configure(
            TextAsset musicConfigAsset, bool resumeImmediately = true)
        {
            _musicConfigAsset = musicConfigAsset;
            _resumeImmediately = resumeImmediately;
        }

        private void Start()
        {
            if (LocalMusicSelectionSession.HasSelection)
            {
                var musicId =
                    LocalMusicSelectionSession.Selection.Music.Id;
                _musicConfigAsset =
                    LocalMusicSelectionSession.MusicConfigAsset;
                _acbRelativePath =
                    $"OpenWDS/StandardCharts/{musicId}/cri/music_{musicId}.acb.bundle";
                var music = LocalMusicSelectionSession.Selection.Music;
                if (!string.IsNullOrEmpty(music.MusicAcbPath)) _acbRelativePath = music.MusicAcbPath;
                _cueName = string.IsNullOrEmpty(music.MusicCue) ? musicId.ToString() : music.MusicCue;
            }
            var configText = _musicConfigAsset != null
                ? _musicConfigAsset.text
                : File.ReadAllText(Path.Combine(
                    Application.streamingAssetsPath,
                    _musicConfigRelativePath));
            var config = StandardNotation.ParseMusicConfig(
                configText);
            _delayMilliseconds = (long)(config.DelayStartSeconds * 1000f);
            var acbPath = Path.Combine(
                CriWare.Common.streamingAssetsPath, _acbRelativePath);
            _acb = CriAtomExAcb.LoadAcbFile(null, acbPath, null);
            if (_acb == null)
                throw new InvalidOperationException(
                    $"CRI Music ACB failed to load: {_acbRelativePath}");
            if (!_acb.GetCueInfo(_cueName, out _))
                throw new InvalidOperationException(
                    $"CRI Music cue '{_cueName}' is missing.");

            // The original CriMusicPlayer constructs the player with the audio
            // synchronized timer enabled, sets its cue and calls Prepare.
            _player = new CriAtomExPlayer(true);
            _player.SetCue(_acb, _cueName);
            _player.SetVolume(_volume);
            _playback = _player.Prepare();
            _prepared = true;

            // CriMusicPlayer.PlayAsync delays by GamePresenterParameter's
            // startTimingSeconds, which is zero in the standard game path.
            // MusicConfig.DelayStartSeconds belongs to MusicTime's clock offset;
            // using it here as a second playback delay shifts audio by 3.019 s.
            if (_resumeImmediately) BeginPlayback();
        }

        public void PrepareRetry()
        {
            if (!_prepared) throw new InvalidOperationException("Music was not prepared.");
            _playback.Stop();
            _player.Stop();
            _player.SetCue(_acb, _cueName);
            _playback = _player.Prepare();
            _resumed = false; _paused = false; _gamePaused = false;
            _audioClock.Reset();
        }

        public void BeginPlayback()
        {
            if (!_prepared || _resumed) return;
            _playback.Resume(CriAtomEx.ResumeMode.PreparedPlayback);
            _resumed = true;
            _paused = false;
            ApplyPauseState();
        }

        public long GetChartMilliseconds()
        {
            if (!_resumed) return -_delayMilliseconds;
            return _audioClock.Read(
                Time.frameCount, _playback.GetTimeSyncedWithAudio) -
                _delayMilliseconds;
        }

        public void Pause()
        {
            _gamePaused = true;
            ApplyPauseState();
        }

        public void Resume()
        {
            _gamePaused = false;
            ApplyPauseState();
        }

        public void SetVolume(float volume)
        {
            _volume = Mathf.Clamp01(volume);
            _player?.SetVolume(_volume);
        }

        private void OnApplicationPause(bool isPaused)
        {
            _applicationPaused = isPaused;
            ApplyPauseState();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _applicationUnfocused = !hasFocus;
            ApplyPauseState();
        }

        private void ApplyPauseState()
        {
            if (!_resumed) return;
            var shouldPause =
                _gamePaused || _applicationPaused || _applicationUnfocused;
            if (shouldPause == _paused) return;
            if (shouldPause)
                _playback.Pause();
            else
                _playback.Resume(CriAtomEx.ResumeMode.PausedPlayback);
            _paused = shouldPause;
            _audioClock.InvalidateFrame();
        }

        private void OnDestroy()
        {
            if (_prepared) _playback.Stop();
            _player?.Dispose();
            _acb?.Dispose();
            _player = null;
            _acb = null;
        }
    }
}
