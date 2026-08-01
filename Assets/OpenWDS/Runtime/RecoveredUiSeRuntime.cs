using System;
using System.Collections.Generic;
using System.IO;
using CriWare;
using UnityEngine;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Shared SeMessageBroker/SePlayer slice backed by the original SE.acb.
    /// Besides enum-backed UI buttons it accepts authored cue names from
    /// EffectSePlayer AnimationEvents. The retail SePlayer owns one CRI player
    /// plus latest-playback dictionaries for cue-name and looping playback.
    /// </summary>
    public sealed class RecoveredUiSeRuntime : MonoBehaviour
    {
        public enum Cue
        {
            ButtonGo = 1,
            ButtonBack = 2,
            CountUp2 = 6,
            BadgeNewGot = 8,
            Plus = 13,
            Minus = 14,
        }

        [SerializeField] private string _acbRelativePath =
            "OpenWDS/CRI/SE.acb";
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;

        private readonly Dictionary<string, CriAtomExPlayback> _playbacks =
            new Dictionary<string, CriAtomExPlayback>(StringComparer.Ordinal);
        private readonly Dictionary<string, CriAtomExPlayback> _loopingPlaybacks =
            new Dictionary<string, CriAtomExPlayback>(StringComparer.Ordinal);
        private CriAtomExAcb _acb;
        private CriAtomExPlayer _player;
        private bool _initialized;

        public static RecoveredUiSeRuntime Instance { get; private set; }
        public bool IsInitialized => _initialized;
        public string LastCueName { get; private set; }
        public int CueNamePlayCount { get; private set; }
        public CriAtomExPlayback LastPlayback { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
                throw new InvalidOperationException(
                    "Only one recovered shared SE runtime may be active.");
            Instance = this;
            LastPlayback =
                new CriAtomExPlayback(CriAtomExPlayback.invalidId);
        }

        public void Play(Cue cue)
        {
            Play(GetCueName(cue));
        }

        public void Play(string cueName)
        {
            StartCue(cueName, false);
        }

        public void PlayLoop(string cueName)
        {
            StartCue(cueName, true);
        }

        public void Stop(string cueName)
        {
            ValidateCueName(cueName);
            if (_loopingPlaybacks.TryGetValue(cueName, out var looping))
                looping.Stop();
            if (_playbacks.TryGetValue(cueName, out var playback))
                playback.Stop();
        }

        public void StopAll()
        {
            if (!_initialized) return;
            _player.Stop();
            foreach (var playback in _loopingPlaybacks.Values)
                playback.Stop();
            foreach (var playback in _playbacks.Values)
                playback.Stop();
            _loopingPlaybacks.Clear();
            _playbacks.Clear();
        }

        public void SetVolume(float volume)
        {
            _volume = Mathf.Clamp01(volume);
            if (!_initialized) return;
            _player.SetVolume(_volume);
            _player.UpdateAll();
        }

        private void StartCue(string cueName, bool loop)
        {
            ValidateCueName(cueName);
            _player.Loop(loop);
            _player.SetCue(_acb, cueName);
            var playback = _player.Start();
            if (loop)
                _loopingPlaybacks[cueName] = playback;
            else
                _playbacks[cueName] = playback;
            LastCueName = cueName;
            LastPlayback = playback;
            CueNamePlayCount++;
        }

        private void ValidateCueName(string cueName)
        {
            if (string.IsNullOrEmpty(cueName))
                throw new ArgumentException(
                    "A recovered shared SE cue name is required.",
                    nameof(cueName));
            if (!_initialized) Initialize();
            if (!_acb.GetCueInfo(cueName, out _))
                throw new InvalidOperationException(
                    "Original shared SE cue is missing: " + cueName);
        }

        private void Initialize()
        {
            var path = Path.Combine(
                CriWare.Common.streamingAssetsPath, _acbRelativePath);
            _acb = CriAtomExAcb.LoadAcbFile(null, path, null);
            if (_acb == null)
                throw new InvalidOperationException(
                    "Original shared SE.acb failed to load.");
            _player = new CriAtomExPlayer();
            _player.SetVolume(_volume);
            foreach (Cue cue in Enum.GetValues(typeof(Cue)))
            {
                var cueName = GetCueName(cue);
                if (!_acb.GetCueInfo(cueName, out _))
                    throw new InvalidOperationException(
                        "Original shared SE cue is missing: " + cueName);
            }
            _initialized = true;
        }

        public static string GetCueName(Cue cue)
        {
            switch (cue)
            {
                case Cue.ButtonGo: return "BUTTON_GO";
                case Cue.ButtonBack: return "BUTTON_BACK";
                case Cue.CountUp2: return "COUNT_UP_2";
                case Cue.BadgeNewGot: return "BADGE_NEW_GOT";
                case Cue.Plus: return "PLUS";
                case Cue.Minus: return "MINUS";
                default:
                    throw new ArgumentOutOfRangeException(nameof(cue), cue, null);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            StopAll();
            _player?.Dispose();
            _acb?.Dispose();
            _player = null;
            _acb = null;
            _initialized = false;
        }
    }
}
