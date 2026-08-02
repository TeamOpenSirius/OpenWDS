using System;
using System.Collections.Generic;
using System.IO;
using CriWare;
using UnityEngine;

namespace OpenWDS.Runtime
{
    public readonly struct RecoveredInputResultEntity
    {
        public readonly int NoteId;
        public readonly RecoveredNoteType NoteType;
        public readonly long StartMilliseconds;
        public readonly long EndMilliseconds;
        public readonly RecoveredTimingType TimingType;
        public readonly RecoveredTimingAssistType TimingAssistType;
        public readonly long DiffMilliseconds;
        public readonly bool IsInput;

        private RecoveredInputResultEntity(
            int noteId,
            RecoveredNoteType noteType,
            long startMilliseconds,
            long endMilliseconds,
            RecoveredTimingType timingType,
            RecoveredTimingAssistType timingAssistType,
            long diffMilliseconds,
            bool isInput)
        {
            NoteId = noteId;
            NoteType = noteType;
            StartMilliseconds = startMilliseconds;
            EndMilliseconds = endMilliseconds;
            TimingType = timingType;
            TimingAssistType = timingAssistType;
            DiffMilliseconds = diffMilliseconds;
            IsInput = isInput;
        }

        public static RecoveredInputResultEntity Create(
            RecoveredNotationNote note,
            in RecoveredTimingDecision timing)
        {
            if (note == null) throw new ArgumentNullException(nameof(note));
            return new RecoveredInputResultEntity(
                note.Id,
                (RecoveredNoteType)note.NoteType,
                note.StartMilliseconds,
                note.EndMilliseconds,
                timing.TimingType,
                timing.TimingAssistType,
                timing.DiffMilliseconds,
                true);
        }

        public static RecoveredInputResultEntity OnMiss(
            RecoveredNotationNote note)
        {
            if (note == null) throw new ArgumentNullException(nameof(note));
            return new RecoveredInputResultEntity(
                note.Id,
                (RecoveredNoteType)note.NoteType,
                note.StartMilliseconds,
                note.EndMilliseconds,
                RecoveredTimingType.Miss,
                RecoveredTimingAssistType.None,
                0L,
                false);
        }
    }

    /// <summary>
    /// GameSePlayer subset recovered from WDS 2.30.3 ARM64. Cue selection,
    /// Scratch scheduling, Hold-loop ownership and the 25 ms history gate mirror
    /// RVAs 0xB9E7088-0xB9E8D90; no audio parameters are inferred from captures.
    /// </summary>
    public sealed class RecoveredGameSeRuntime : MonoBehaviour
    {
        private const long IgnorePlayMilliseconds = 25;
        private const int MaxEntriesPerFrame = 25;
        private readonly List<HistoryKey> _expiredHistoryKeys =
            new List<HistoryKey>(MaxEntriesPerFrame);

        private enum SeCue
        {
            Basic1Perfect = 0,
            Basic1Great = 1,
            Basic1Good = 2,
            Basic1Bad = 3,
            Basic1Empty = 4,
            Critical = 5,
            Sound = 6,
            Scratch = 7,
            StandbyEmpty = 8,
        }

        private readonly struct HistoryKey : IEquatable<HistoryKey>
        {
            public readonly long TargetMilliseconds;
            public readonly SeCue Cue;

            public HistoryKey(long targetMilliseconds, SeCue cue)
            {
                TargetMilliseconds = targetMilliseconds;
                Cue = cue;
            }

            public bool Equals(HistoryKey other) =>
                TargetMilliseconds == other.TargetMilliseconds && Cue == other.Cue;
            public override bool Equals(object obj) =>
                obj is HistoryKey other && Equals(other);
            public override int GetHashCode() =>
                unchecked(((int)TargetMilliseconds * 397) ^ (int)Cue);
        }

        private static readonly Dictionary<RecoveredTimingType, SeCue> ManualTiming =
            new Dictionary<RecoveredTimingType, SeCue>
            {
                { RecoveredTimingType.PerfectStar, SeCue.Basic1Perfect },
                { RecoveredTimingType.Perfect, SeCue.Basic1Perfect },
                { RecoveredTimingType.Great, SeCue.Basic1Great },
                { RecoveredTimingType.Good, SeCue.Basic1Good },
                { RecoveredTimingType.Bad, SeCue.Basic1Bad },
                { RecoveredTimingType.None, SeCue.Basic1Empty },
            };

        private static readonly Dictionary<RecoveredTimingType, SeCue> AutoTiming =
            new Dictionary<RecoveredTimingType, SeCue>
            {
                { RecoveredTimingType.PerfectStar, SeCue.Basic1Perfect },
                { RecoveredTimingType.Perfect, SeCue.Basic1Perfect },
                { RecoveredTimingType.Great, SeCue.Basic1Perfect },
                { RecoveredTimingType.Good, SeCue.Basic1Perfect },
                { RecoveredTimingType.Bad, SeCue.Basic1Bad },
                { RecoveredTimingType.None, SeCue.Basic1Empty },
            };

        private static readonly string[] CueNames =
        {
            "Basic1_perfect",
            "Basic1_great",
            "Basic1_good",
            "Basic1_bad",
            "Basic1_empty",
            "Basic1_critical",
            "Basic1_sound",
            "Scratch",
            "Basic1_empty2",
        };

        private bool _useAutomaticJudgeTiming;
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;
        [SerializeField] private string _commonAcbRelativePath =
            "OpenWDS/CRI/GameCommonSE.acb";
        [SerializeField] private string _customAcbRelativePath =
            "OpenWDS/CRI/GameCustomSE_1.acb";

        private readonly Dictionary<SeCue, CriAtomExPlayer> _tapPlayers =
            new Dictionary<SeCue, CriAtomExPlayer>();
        private readonly Dictionary<HistoryKey, long> _history =
            new Dictionary<HistoryKey, long>();
        private readonly List<RecoveredInputResultEntity> _scratchSchedules =
            new List<RecoveredInputResultEntity>(16);
        private readonly HashSet<int> _holdings = new HashSet<int>();
        private readonly Dictionary<int, bool> _holdRequests =
            new Dictionary<int, bool>();

        private CriAtomExAcb _commonAcb;
        private CriAtomExAcb _customAcb;
        private CriAtomExPlayer _holdPlayer;
        private CriAtomExPlayback _holdingPlayback;
        private CriAtomExPlayback _tapPlayback;
        private CriAtomExPlayback _emptyTapPlayback;
        private int _entryCount;
        private bool _initialized;

        public bool IsInitialized => _initialized;
        public int ScheduledScratchCount => _scratchSchedules.Count;
        public int HoldingCount => _holdings.Count;
        public bool IsHoldingPlaybackActive =>
            _initialized &&
            _holdingPlayback.id != CriAtomExPlayback.invalidId;
        public int HoldOwnershipCorrectionCount { get; private set; }

        public static bool ValidateRecoveredRules()
        {
            return ManualTiming[RecoveredTimingType.PerfectStar] ==
                       SeCue.Basic1Perfect &&
                   ManualTiming[RecoveredTimingType.Great] == SeCue.Basic1Great &&
                   ManualTiming[RecoveredTimingType.Good] == SeCue.Basic1Good &&
                   AutoTiming[RecoveredTimingType.Great] == SeCue.Basic1Perfect &&
                   AutoTiming[RecoveredTimingType.Good] == SeCue.Basic1Perfect &&
                   AutoTiming[RecoveredTimingType.Bad] == SeCue.Basic1Bad &&
                   UsesEndMilliseconds(RecoveredNoteType.Hold) &&
                   UsesEndMilliseconds(RecoveredNoteType.ScratchCriticalHold) &&
                   !UsesEndMilliseconds(RecoveredNoteType.HoldStart) &&
                   GetScratchCue(
                       RecoveredNoteType.Scratch,
                       RecoveredTimingType.Great) == SeCue.Sound &&
                   GetScratchCue(
                       RecoveredNoteType.ScratchHold,
                       RecoveredTimingType.Great) == SeCue.Scratch &&
                   ShouldScheduleScratch(
                       RecoveredNoteType.Scratch,
                       RecoveredTimingType.PerfectStar,
                       -1) &&
                   !ShouldScheduleScratch(
                       RecoveredNoteType.Scratch,
                       RecoveredTimingType.PerfectStar,
                       0);
        }

        public void Configure(bool useAutomaticJudgeTiming)
        {
            _useAutomaticJudgeTiming = useAutomaticJudgeTiming;
        }

        public void Initialize()
        {
            if (_initialized) return;
            var commonPath = Path.Combine(
                CriWare.Common.streamingAssetsPath, _commonAcbRelativePath);
            var customPath = Path.Combine(
                CriWare.Common.streamingAssetsPath, _customAcbRelativePath);
            _commonAcb = CriAtomExAcb.LoadAcbFile(null, commonPath, null);
            _customAcb = CriAtomExAcb.LoadAcbFile(null, customPath, null);
            if (_commonAcb == null || _customAcb == null)
                throw new InvalidOperationException("Game SE ACB failed to load.");

            for (var index = 0; index < CueNames.Length; index++)
            {
                var cue = (SeCue)index;
                var acb = cue == SeCue.Critical || cue == SeCue.Scratch
                    ? _commonAcb
                    : _customAcb;
                if (!acb.GetCueInfo(CueNames[index], out _))
                    throw new InvalidOperationException(
                        "Game SE cue is missing: " + CueNames[index]);
                var player = new CriAtomExPlayer();
                player.SetCue(acb, CueNames[index]);
                player.SetVolume(_volume);
                _tapPlayers.Add(cue, player);
            }

            if (!_commonAcb.GetCueInfo("Basic1_hold", out _))
                throw new InvalidOperationException("Game SE cue is missing: Basic1_hold");
            _holdPlayer = new CriAtomExPlayer();
            _holdPlayer.SetCue(_commonAcb, "Basic1_hold");
            _holdPlayer.Loop(true);
            _holdPlayer.SetVolume(_volume);
            _holdingPlayback = new CriAtomExPlayback(CriAtomExPlayback.invalidId);
            _initialized = true;
        }

        public void ProcessFrame(
            IReadOnlyList<RecoveredInputResultEntity> results,
            IReadOnlyList<RecoveredInputEffectEntity> effects,
            long chartMilliseconds,
            IReadOnlyDictionary<int, RecoveredNotationNote> activeTouchHolds = null)
        {
            if (!_initialized) Initialize();
            _entryCount = 0;
            PlayScheduledScratch(chartMilliseconds);
            if (results != null)
            {
                for (var index = 0; index < results.Count; index++)
                    OnPlaySe(results[index]);
            }
            if (effects != null)
            {
                for (var index = 0; index < effects.Count; index++)
                    OnInputEffect(effects[index]);
            }
            FlushHoldRequests();
            ReconcileHoldOwnership(activeTouchHolds);
            ExpireHistory(chartMilliseconds);
        }

        private void OnPlaySe(in RecoveredInputResultEntity result)
        {
            if (_entryCount++ >= MaxEntriesPerFrame ||
                result.NoteType == RecoveredNoteType.HoldEighth ||
                result.TimingType == RecoveredTimingType.Miss)
            {
                return;
            }

            switch (result.NoteType)
            {
                case RecoveredNoteType.Critical:
                case RecoveredNoteType.ScratchCriticalHoldStart:
                case RecoveredNoteType.CriticalHoldStart:
                    if (result.TimingType > RecoveredTimingType.Good)
                        TryPlay(result, SeCue.Critical);
                    else
                        TryPlayTiming(result);
                    return;
                case RecoveredNoteType.Sound:
                case RecoveredNoteType.SoundPurple:
                    Play(SeCue.Sound);
                    return;
                case RecoveredNoteType.Flick:
                    TryPlay(result, SeCue.Scratch);
                    return;
                case RecoveredNoteType.Scratch:
                case RecoveredNoteType.ScratchHold:
                case RecoveredNoteType.ScratchCriticalHold:
                    if (ShouldScheduleScratch(
                        result.NoteType,
                        result.TimingType,
                        result.DiffMilliseconds))
                    {
                        _scratchSchedules.Add(result);
                        return;
                    }
                    TryPlayScratch(result);
                    return;
                default:
                    TryPlayTiming(result);
                    return;
            }
        }

        private void OnInputEffect(in RecoveredInputEffectEntity effect)
        {
            if (effect.TimingType == RecoveredTimingType.None &&
                effect.EffectType == RecoveredInputEffectType.EmptyTap)
            {
                TryPlay(
                    effect.StartMilliseconds,
                    0,
                    effect.StartMilliseconds,
                    effect.NoteType,
                    SeCue.Basic1Perfect);
                return;
            }
            if (effect.EffectType == RecoveredInputEffectType.HoldStart)
                _holdRequests[effect.NoteId] = true;
            else if (effect.EffectType == RecoveredInputEffectType.HoldEnd)
                _holdRequests[effect.NoteId] = false;
        }

        private void FlushHoldRequests()
        {
            foreach (var request in _holdRequests)
                PlayHold(request.Key, request.Value);
            _holdRequests.Clear();
        }

        private void PlayHold(int noteId, bool isHolding)
        {
            if (isHolding)
            {
                if (_holdings.Count == 0)
                    _holdingPlayback = _holdPlayer.Start();
                _holdings.Add(noteId);
                return;
            }
            _holdings.Remove(noteId);
            if (_holdings.Count == 0)
                StopHoldingPlayback();
        }

        private void ReconcileHoldOwnership(
            IReadOnlyDictionary<int, RecoveredNotationNote> activeTouchHolds)
        {
            if (activeTouchHolds == null) return;
            var activeNoteIds = new HashSet<int>();
            foreach (var pair in activeTouchHolds)
                if (pair.Value != null) activeNoteIds.Add(pair.Value.Id);
            if (_holdings.SetEquals(activeNoteIds)) return;

            HoldOwnershipCorrectionCount++;
            if (_holdings.Count > 0 && activeNoteIds.Count == 0)
                StopHoldingPlayback();
            else if (_holdings.Count == 0 && activeNoteIds.Count > 0)
                _holdingPlayback = _holdPlayer.Start();
            _holdings.Clear();
            foreach (var noteId in activeNoteIds) _holdings.Add(noteId);
        }

        private void StopHoldingPlayback()
        {
            if (_holdingPlayback.id != CriAtomExPlayback.invalidId)
                _holdingPlayback.Stop();
            // CriAtomExPlayback's invalid value is uint.MaxValue, not zero.
            _holdingPlayback = new CriAtomExPlayback(CriAtomExPlayback.invalidId);
        }

        public void StopAllHolds()
        {
            _holdRequests.Clear();
            _holdings.Clear();
            if (_initialized) StopHoldingPlayback();
        }

        private void PlayScheduledScratch(long chartMilliseconds)
        {
            for (var index = _scratchSchedules.Count - 1; index >= 0; index--)
            {
                var result = _scratchSchedules[index];
                if (GetTargetMilliseconds(result) > chartMilliseconds) continue;
                TryPlayScratch(result);
                _scratchSchedules.RemoveAt(index);
            }
        }

        private void TryPlayScratch(in RecoveredInputResultEntity result)
        {
            TryPlay(result, GetScratchCue(result.NoteType, result.TimingType));
        }

        private static bool ShouldScheduleScratch(
            RecoveredNoteType noteType,
            RecoveredTimingType timingType,
            long diffMilliseconds) =>
            (noteType == RecoveredNoteType.Scratch ||
             noteType == RecoveredNoteType.ScratchHold ||
             noteType == RecoveredNoteType.ScratchCriticalHold) &&
            timingType == RecoveredTimingType.PerfectStar &&
            diffMilliseconds < 0;

        private static SeCue GetScratchCue(
            RecoveredNoteType noteType,
            RecoveredTimingType timingType) =>
            noteType == RecoveredNoteType.Scratch &&
            timingType == RecoveredTimingType.Great
                ? SeCue.Sound
                : SeCue.Scratch;

        private void TryPlayTiming(in RecoveredInputResultEntity result)
        {
            var mapping = _useAutomaticJudgeTiming ? AutoTiming : ManualTiming;
            if (mapping.TryGetValue(result.TimingType, out var cue))
                TryPlay(result, cue);
        }

        private bool TryPlay(in RecoveredInputResultEntity result, SeCue cue) =>
            TryPlay(
                result.StartMilliseconds,
                result.DiffMilliseconds,
                result.EndMilliseconds,
                result.NoteType,
                cue);

        private bool TryPlay(
            long startMilliseconds,
            long diffMilliseconds,
            long endMilliseconds,
            RecoveredNoteType noteType,
            SeCue cue)
        {
            var target = UsesEndMilliseconds(noteType)
                ? endMilliseconds
                : startMilliseconds;
            var playedAt = target + diffMilliseconds;
            var key = new HistoryKey(target, cue);
            if (_history.TryGetValue(key, out var previous) &&
                playedAt <= previous + IgnorePlayMilliseconds)
            {
                return false;
            }
            _history[key] = playedAt;
            Play(cue);
            return true;
        }

        private void Play(SeCue cue)
        {
            var playback = _tapPlayers[cue].Start();
            // GameSePlayer retains the latest regular and empty playback in
            // separate fields. Retaining the handle also lets volume updates
            // be applied to the currently sounding playback, as in the game.
            if (cue == SeCue.StandbyEmpty)
                _emptyTapPlayback = playback;
            else
                _tapPlayback = playback;
        }

        private void ExpireHistory(long chartMilliseconds)
        {
            if (_history.Count == 0) return;
            _expiredHistoryKeys.Clear();
            foreach (var item in _history)
            {
                if (item.Value + IgnorePlayMilliseconds < chartMilliseconds)
                    _expiredHistoryKeys.Add(item.Key);
            }
            foreach (var key in _expiredHistoryKeys) _history.Remove(key);
        }

        private static bool UsesEndMilliseconds(RecoveredNoteType noteType) =>
            noteType == RecoveredNoteType.Hold ||
            noteType == RecoveredNoteType.CriticalHold ||
            noteType == RecoveredNoteType.ScratchHold ||
            noteType == RecoveredNoteType.ScratchCriticalHold;

        private static long GetTargetMilliseconds(
            in RecoveredInputResultEntity result) =>
            UsesEndMilliseconds(result.NoteType)
                ? result.EndMilliseconds
                : result.StartMilliseconds;

        public void SetVolume(float volume)
        {
            _volume = Mathf.Clamp01(volume);
            foreach (var player in _tapPlayers.Values)
            {
                player.SetVolume(_volume);
                if (_tapPlayback.id != 0)
                    player.Update(_tapPlayback);
            }
            _holdPlayer?.SetVolume(_volume);
            if (_holdPlayer != null &&
                _holdingPlayback.id != CriAtomExPlayback.invalidId)
                _holdPlayer.Update(_holdingPlayback);
        }

        private void OnDestroy()
        {
            if (!_initialized) return;
            StopAllHolds();
            foreach (var player in _tapPlayers.Values) player.Dispose();
            _tapPlayers.Clear();
            _holdPlayer?.Dispose();
            _commonAcb?.Dispose();
            _customAcb?.Dispose();
            _holdPlayer = null;
            _commonAcb = null;
            _customAcb = null;
            _initialized = false;
        }
    }
}
