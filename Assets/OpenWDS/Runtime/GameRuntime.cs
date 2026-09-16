using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

namespace OpenWDS.Runtime
{
    public enum GameExitRoute
    {
        None = 0,
        MusicSelection = 1,
    }

    /// <summary>
    /// Executable recovery of InputHandler's Fire lifecycle. AutoTouch still enters
    /// through the same sorting, lane resolution, Tap/Flick, Hold/Scratch and ended
    /// cleanup phases as a device input frame.
    /// </summary>
    public sealed class InputHandlerRuntime : IDisposable
    {
        private sealed class MissEntry
        {
            public NotationNote Note;
            public long DeadlineMilliseconds;
            public bool IncludesDeadline;
        }

        private readonly GameClock _clock;
        private readonly AutoTouch _autoTouch;
        private readonly Func<Vector2, HitLaneEntity> _resolveLane;
        private readonly LaneRaycaster _laneRaycaster;
        private readonly TapNoteManager _tapNotes;
        private readonly FlickNoteManager _flickNotes;
        private readonly StandardHoldNoteManager _holdNotes;
        private readonly ScratchNoteManager _scratchNotes;
        private readonly TapAction _tapAction;
        private readonly FlickAction _flickAction;
        private readonly UnifiedHoldAction _holdAction;
        private readonly FlickInputManager _flickInputs =
            new FlickInputManager();
        private readonly LaneHitManager _laneHits =
            new LaneHitManager();
        private readonly LaneHoldManager _laneHolds =
            new LaneHoldManager();
        private readonly List<NotationNote> _candidates;
        private readonly List<InputEntity> _touches =
            new List<InputEntity>(32);
        private readonly List<HitLaneEntity> _hitLanes =
            new List<HitLaneEntity>(32);
        private readonly List<HitLaneEntity> _hitLanesForBegan =
            new List<HitLaneEntity>(32);
        private readonly List<InputEntity> _endedTouches =
            new List<InputEntity>(32);
        private readonly List<InputEffectEntity> _holdEvents =
            new List<InputEffectEntity>(32);
        private readonly List<InputEffectEntity> _inputEffects =
            new List<InputEffectEntity>(64);
        private readonly List<InputResultEntity> _inputResults =
            new List<InputResultEntity>(64);
        private readonly Dictionary<int, NotationNote> _flickNotesById =
            new Dictionary<int, NotationNote>();
        private readonly Dictionary<int, List<string>> _flickInputDiagnostics =
            new Dictionary<int, List<string>>();
        private readonly Dictionary<int, NotationNote>
            _holdBodyByStartId =
                new Dictionary<int, NotationNote>();
        private readonly Dictionary<int, HitLaneEntity> _beganHitLaneCache =
            new Dictionary<int, HitLaneEntity>(15);
        private readonly List<MissEntry> _missSchedule = new List<MissEntry>(1000);
        private readonly HashSet<NotationNote> _resolvedNotes =
            new HashSet<NotationNote>();
        private readonly Dictionary<long, Queue<NotationNote>>
            _unresolvedNotesByKey =
                new Dictionary<long, Queue<NotationNote>>();
        private readonly long _delayStartMilliseconds;
        private readonly bool _captureFlickDiagnostics;
        private int _nextMissIndex;

        public int ScheduledEventCount => _autoTouch.ScheduledEventCount;
        public IReadOnlyList<InputEntity> ScheduledEvents =>
            _autoTouch.ScheduledEvents;
        public int ProcessedFrameCount { get; private set; }
        public int ConsumedTapCount { get; private set; }
        public int ConsumedFlickCount { get; private set; }
        public int ConsumedHoldCount { get; private set; }
        public int PublishedHoldEventCount { get; private set; }
        public IReadOnlyList<InputEffectEntity> HoldEvents => _holdEvents;
        public IReadOnlyList<InputEffectEntity> InputEffects => _inputEffects;
        public IReadOnlyList<InputResultEntity> InputResults => _inputResults;
        public int RemainingTapCount => _tapNotes.Count;
        public int RemainingFlickCount => _flickNotes.Count;
        public IReadOnlyList<NotationNote> RemainingFlickNotes =>
            _flickNotes.Notes;
        public int RemainingHoldCount => _holdNotes.HoldNoteCount;
        public int RemainingScratchCount => _scratchNotes.Count;
        public int RemainingHoldingCount => _holdNotes.HoldingNoteCount;
        public int ActiveTouchHoldCount => _laneHolds.Count;
        public IReadOnlyDictionary<int, NotationNote> ActiveTouchHolds =>
            _laneHolds.ActiveAssignments;
        public bool IsGameCompleted { get; private set; }
        public bool PlayableQueuesEmpty =>
            RemainingTapCount == 0 && RemainingFlickCount == 0 &&
            RemainingHoldCount == 0 && RemainingScratchCount == 0;
        public int PublishedMissCount { get; private set; }
        public int ResolvedNoteCount => _resolvedNotes.Count;
        public int ScheduledNoteCount => _missSchedule.Count;

        public InputHandlerRuntime(
            IReadOnlyList<NotationNote> notation,
            float delayStartSeconds,
            Func<NotationNote, Vector2> getScreenPosition,
            Func<Vector2, HitLaneEntity> resolveLane,
            double noteTimingValue = 0d,
            bool captureFlickDiagnostics = false,
            LaneRaycaster laneRaycaster = null)
        {
            if (notation == null) throw new ArgumentNullException(nameof(notation));
            _resolveLane = resolveLane ?? throw new ArgumentNullException(nameof(resolveLane));
            _laneRaycaster = laneRaycaster;
            _delayStartMilliseconds = (long)(delayStartSeconds * 1000f);
            _captureFlickDiagnostics = captureFlickDiagnostics;
            _clock = new GameClock(delayStartSeconds, noteTimingValue);
            _autoTouch = new AutoTouch(
                _clock, notation, getScreenPosition ??
                throw new ArgumentNullException(nameof(getScreenPosition)));
            _tapNotes = new TapNoteManager(notation);
            _flickNotes = new FlickNoteManager(notation);
            foreach (var flickNote in _flickNotes.Notes)
                _flickNotesById[flickNote.Id] = flickNote;
            _holdNotes = new StandardHoldNoteManager(notation);
            _scratchNotes = new ScratchNoteManager(notation);
            _tapAction = new TapAction(_clock, _tapNotes);
            _flickAction = new FlickAction(
                _clock, _flickInputs, _flickNotes);
            _holdAction = new UnifiedHoldAction(_holdNotes, _scratchNotes);
            _candidates = InputFireCore.CreateSortedCandidates(
                _tapNotes.Notes, GetFlickNotes(notation));
            InitializeHoldStartAssignments(notation);
            InitializeMissSchedule(notation);
        }

        public void Tick(long musicMilliseconds, long gameMilliseconds)
        {
            BeginTick(musicMilliseconds, gameMilliseconds);
            if (IsGameCompleted) return;
            _autoTouch.GetTouches(_touches);
            ProcessTouches(musicMilliseconds);
        }

        /// <summary>
        /// Player-input entry to the same InputHandler.Fire pipeline. InputEntity
        /// timestamps are game-clock milliseconds, matching DefaultTouch's
        /// EnhancedTouch conversion. Collection/noise filtering remains outside
        /// this boundary until the original Input System dependency is restored.
        /// </summary>
        public void TickPlayer(
            long musicMilliseconds,
            long gameMilliseconds,
            IReadOnlyList<InputEntity> touches)
        {
            if (touches == null) throw new ArgumentNullException(nameof(touches));
            BeginTick(musicMilliseconds, gameMilliseconds);
            if (IsGameCompleted) return;
            for (var index = 0; index < touches.Count; index++)
                _touches.Add(touches[index]);
            InputOrdering.SortLikeInputHandler(_touches);
            ProcessTouches(musicMilliseconds);
        }

        private void BeginTick(long musicMilliseconds, long gameMilliseconds)
        {
            _holdEvents.Clear();
            _inputEffects.Clear();
            _inputResults.Clear();
            if (IsGameCompleted) return;
            var playerMilliseconds = musicMilliseconds + _delayStartMilliseconds;
            _clock.Sync(
                playerMilliseconds / 1000f,
                playerMilliseconds,
                gameMilliseconds / 1000f,
                gameMilliseconds);

            _touches.Clear();
            _hitLanes.Clear();
            _hitLanesForBegan.Clear();
            _endedTouches.Clear();
        }

        private void ProcessTouches(long musicMilliseconds)
        {
            if (_laneRaycaster != null)
                _laneRaycaster.Raycast(
                    _touches, _hitLanes, _hitLanesForBegan);
            for (var inputIndex = 0; inputIndex < _touches.Count; inputIndex++)
            {
                var input = _touches[inputIndex];
                var currentHitLane = _laneRaycaster != null
                    ? _hitLanes[inputIndex]
                    : _resolveLane(input.ScreenPosition);
                if (_laneRaycaster == null)
                    _hitLanes.Add(currentHitLane);
                if (_laneRaycaster != null) continue;

                HitLaneEntity beganHitLane;
                if (!_beganHitLaneCache.TryGetValue(
                        input.TouchId, out beganHitLane))
                {
                    beganHitLane = input.Phase == TouchPhase.Began
                        ? currentHitLane
                        : _resolveLane(input.StartScreenPosition);
                    _beganHitLaneCache[input.TouchId] = beganHitLane;
                    _hitLanesForBegan.Add(beganHitLane);
                }
                else
                {
                    _hitLanesForBegan.Add(beganHitLane);
                }
            }

            for (var index = 0;
                 _captureFlickDiagnostics && index < _touches.Count;
                 index++)
            {
                var input = _touches[index];
                if (!_flickNotesById.ContainsKey(input.TouchId)) continue;
                if (!_flickInputDiagnostics.TryGetValue(input.TouchId, out var entries))
                {
                    entries = new List<string>(2);
                    _flickInputDiagnostics[input.TouchId] = entries;
                }
                entries.Add(
                    $"frame={musicMilliseconds};input={input.Milliseconds};" +
                    $"phase={input.Phase};hit={FormatHitLane(_hitLanes[index])};" +
                    $"beganHit={FormatHitLane(_hitLanesForBegan[index])}");
            }

            InputFireCore.ConsumeTapAndFlickPass(
                _touches,
                _hitLanes,
                _hitLanesForBegan,
                _candidates,
                _laneHits,
                TryTapOrFlick,
                _endedTouches);

            ConsumedHoldCount += InputFireCore.ConsumeHoldPass(
                _touches,
                _hitLanes,
                _laneHits,
                _laneHolds,
                (input, hitLane) =>
                {
                    var result = _holdAction.TryHold(
                        input,
                        hitLane,
                        _clock.InputTimeToMusicMilliseconds(input.Milliseconds),
                        _clock.PassedMilliseconds,
                        _laneHolds.TryGet(
                            input.TouchId, out var currentHold)
                            ? currentHold
                            : null);
                    var holdingTiming = new TimingDecision(
                        TimingType.PerfectStar,
                        TimingAssistType.None,
                        0);
                    foreach (var holding in _holdAction.HoldingResults)
                    {
                        _inputResults.Add(InputResultEntity.Create(
                            holding, holdingTiming));
                        // Type 900 is the invisible eighth-Hold pulse. Types
                        // 30/31 use the recovered Sound bomb/beam path.
                        if (holding.NoteType != (int)NoteType.HoldEighth)
                            PublishNoteEffects(holding, holdingTiming.TimingType);
                    }
                    foreach (var scratch in _holdAction.ScratchResults)
                    {
                        _inputResults.Add(InputResultEntity.Create(
                            scratch.Note, scratch.Timing));
                        PublishNoteEffects(
                            scratch.Note, scratch.Timing.TimingType);
                    }
                    if (result.Consumed && result.HoldNote != null)
                    {
                        _inputResults.Add(InputResultEntity.Create(
                            result.HoldNote, result.Timing));
                        PublishNoteEffects(result.HoldNote, result.Timing.TimingType);
                    }
                    return result;
                });

            InputLifecycle.FinalizeEndedTouches(
                _endedTouches,
                _flickInputs,
                _laneHits,
                _laneHolds.Remove);
            foreach (var endedTouch in _endedTouches)
                _beganHitLaneCache.Remove(endedTouch.TouchId);
            ResolveInputResults();
            PublishExpiredMisses(musicMilliseconds);
            _laneHolds.GetHoldEvents(_holdEvents);
            _inputEffects.AddRange(_holdEvents);
            PublishedHoldEventCount += _holdEvents.Count;
            ProcessedFrameCount++;
        }

        public void Dispose()
        {
            _laneRaycaster?.Dispose();
        }

        public string GetFlickInputDiagnostic(int noteId)
        {
            if (!_flickInputDiagnostics.TryGetValue(noteId, out var entries))
                return string.Empty;
            return string.Join("|", entries);
        }

        private static string FormatHitLane(in HitLaneEntity hitLane)
        {
            return $"{hitLane.HitMainLaneId}/{hitLane.HitSubLeftInnerLaneId}/" +
                   $"{hitLane.HitSubRightInnerLaneId}/{hitLane.HitSubLeftOuterLaneId}/" +
                   $"{hitLane.HitSubRightOuterLaneId}";
        }

        /// <summary>
        /// InputHandler.Reset's InputAction hold-lane cleanup, invoked by
        /// GameTickWorker.Reset when the game leaves its ticking state.
        /// </summary>
        public void CompleteGame()
        {
            if (IsGameCompleted) return;
            _laneHolds.RemoveAll();
            // GameRuntime completes before this frame's consumers so
            // final expiration MISS/results must remain observable. Append the
            // forced HoldEnd events instead of erasing the frame payload.
            var previousHoldEventCount = _holdEvents.Count;
            _laneHolds.GetHoldEvents(_holdEvents);
            for (var index = previousHoldEventCount;
                 index < _holdEvents.Count;
                 index++)
            {
                _inputEffects.Add(_holdEvents[index]);
            }
            PublishedHoldEventCount +=
                _holdEvents.Count - previousHoldEventCount;
            _laneHits.Reset();
            _beganHitLaneCache.Clear();
            IsGameCompleted = true;
        }

        /// <summary>
        /// InputHandler's game-state subscription removes every LaneHold and
        /// publishes the resulting HoldEnd events when ticking is interrupted.
        /// Active device touches may establish fresh assignments after resume.
        /// </summary>
        public void ReleaseHoldLanesForPause()
        {
            _holdEvents.Clear();
            _inputEffects.Clear();
            _laneHolds.RemoveAll();
            _laneHolds.GetHoldEvents(_holdEvents);
            _inputEffects.AddRange(_holdEvents);
            PublishedHoldEventCount += _holdEvents.Count;
            _laneHits.Reset();
            _beganHitLaneCache.Clear();
        }

        public void Reset()
        {
            _autoTouch.Reset();
            _laneHits.Reset();
            _beganHitLaneCache.Clear();
            _laneHolds.RemoveAll();
            _holdEvents.Clear();
            _inputEffects.Clear();
            _inputResults.Clear();
            ProcessedFrameCount = 0;
            ConsumedTapCount = 0;
            ConsumedFlickCount = 0;
            ConsumedHoldCount = 0;
            PublishedHoldEventCount = 0;
            PublishedMissCount = 0;
            _nextMissIndex = 0;
            _resolvedNotes.Clear();
            IsGameCompleted = false;
        }

        private void InitializeMissSchedule(
            IReadOnlyList<NotationNote> notation)
        {
            foreach (var note in notation)
            {
                if (note == null ||
                    note.NoteType == (int)NoteType.None)
                {
                    continue;
                }
                var key = GetResultKey(note.Id, (NoteType)note.NoteType);
                if (!_unresolvedNotesByKey.TryGetValue(key, out var queue))
                {
                    queue = new Queue<NotationNote>();
                    _unresolvedNotesByKey.Add(key, queue);
                }
                queue.Enqueue(note);
                _missSchedule.Add(new MissEntry
                {
                    Note = note,
                    DeadlineMilliseconds = GetMissDeadline(note),
                    IncludesDeadline = UsesHoldTimingDecider(note),
                });
            }
            _missSchedule.Sort((left, right) =>
            {
                var order = left.DeadlineMilliseconds.CompareTo(right.DeadlineMilliseconds);
                if (order != 0) return order;
                // Inclusive deadlines must not wait behind an exclusive
                // deadline at the same timestamp.
                return right.IncludesDeadline.CompareTo(left.IncludesDeadline);
            });
        }

        private void InitializeHoldStartAssignments(
            IReadOnlyList<NotationNote> notation)
        {
            foreach (var start in notation)
            {
                if (start == null ||
                    !IsHoldStartType((NoteType)start.NoteType))
                {
                    continue;
                }
                foreach (var body in notation)
                {
                    if (body == null ||
                        body.StartMilliseconds != start.StartMilliseconds ||
                        body.Lane != start.Lane ||
                        body.EndLane != start.EndLane ||
                        !IsMatchingHoldBody(
                            (NoteType)start.NoteType,
                            (NoteType)body.NoteType))
                    {
                        continue;
                    }
                    _holdBodyByStartId[start.Id] = body;
                    break;
                }
            }
        }

        private static bool IsHoldStartType(NoteType noteType)
        {
            return noteType == NoteType.HoldStart ||
                   noteType == NoteType.CriticalHoldStart ||
                   noteType == NoteType.ScratchHoldStart ||
                   noteType == NoteType.ScratchCriticalHoldStart;
        }

        private static bool IsMatchingHoldBody(
            NoteType startType,
            NoteType bodyType)
        {
            switch (startType)
            {
                case NoteType.HoldStart:
                    return bodyType == NoteType.Hold;
                case NoteType.CriticalHoldStart:
                    return bodyType == NoteType.CriticalHold;
                case NoteType.ScratchHoldStart:
                case NoteType.ScratchCriticalHoldStart:
                    return bodyType == NoteType.ScratchHold ||
                           bodyType == NoteType.ScratchCriticalHold;
                default:
                    return false;
            }
        }

        private void ResolveInputResults()
        {
            for (var index = 0; index < _inputResults.Count; index++)
            {
                var result = _inputResults[index];
                if (!result.IsInput) continue;
                var key = GetResultKey(result.NoteId, result.NoteType);
                if (!_unresolvedNotesByKey.TryGetValue(key, out var queue))
                    continue;
                while (queue.Count > 0 && _resolvedNotes.Contains(queue.Peek()))
                    queue.Dequeue();
                if (queue.Count > 0) _resolvedNotes.Add(queue.Dequeue());
            }
        }

        private void PublishExpiredMisses(long musicMilliseconds)
        {
            while (_nextMissIndex < _missSchedule.Count &&
                   (_missSchedule[_nextMissIndex].DeadlineMilliseconds < musicMilliseconds ||
                    (_missSchedule[_nextMissIndex].DeadlineMilliseconds == musicMilliseconds &&
                     _missSchedule[_nextMissIndex].IncludesDeadline)))
            {
                var note = _missSchedule[_nextMissIndex++].Note;
                if (!_resolvedNotes.Add(note)) continue;
                RemoveExpiredNote(note);
                _inputResults.Add(InputResultEntity.OnMiss(note));
                PublishedMissCount++;
            }
        }

        private void RemoveExpiredNote(NotationNote note)
        {
            switch ((NoteType)note.NoteType)
            {
                case NoteType.Normal:
                case NoteType.Critical:
                case NoteType.HoldStart:
                case NoteType.CriticalHoldStart:
                case NoteType.ScratchHoldStart:
                case NoteType.ScratchCriticalHoldStart:
                case NoteType.BlueTap:
                    _tapNotes.DeleteTapNote(note);
                    _candidates.Remove(note);
                    break;
                case NoteType.Flick:
                    _flickNotes.DeleteFlickNote(note);
                    _candidates.Remove(note);
                    break;
                case NoteType.Hold:
                case NoteType.CriticalHold:
                    _holdNotes.DeleteHoldNote(note);
                    break;
                case NoteType.Sound:
                case NoteType.SoundPurple:
                case NoteType.HoldEighth:
                    _holdNotes.DeleteHoldingNote(note);
                    break;
                case NoteType.Scratch:
                case NoteType.ScratchHold:
                case NoteType.ScratchCriticalHold:
                    _scratchNotes.DeleteScratchNote(note);
                    break;
            }
        }

        private static bool UsesHoldTimingDecider(NotationNote note)
        {
            var type = (NoteType)note.NoteType;
            return type == NoteType.Hold || type == NoteType.CriticalHold ||
                   type == NoteType.Sound || type == NoteType.SoundPurple ||
                   type == NoteType.HoldEighth;
        }

        private static long GetMissDeadline(NotationNote note)
        {
            switch ((NoteType)note.NoteType)
            {
                case NoteType.Hold:
                case NoteType.CriticalHold:
                    // HoldTimingDecider.IsMiss compares the current clock to End.
                    return note.EndMilliseconds;
                case NoteType.ScratchHold:
                case NoteType.ScratchCriticalHold:
                    return note.EndMilliseconds + 125L;
                case NoteType.Flick:
                case NoteType.Scratch:
                    return note.StartMilliseconds + 100L;
                case NoteType.Sound:
                case NoteType.SoundPurple:
                    // Holding queue admission is Start; IsMiss uses End for
                    // 30/31 (the notation's -1 sentinel is not rewritten).
                    return Math.Max(note.StartMilliseconds, note.EndMilliseconds);
                case NoteType.HoldEighth:
                    // HoldingNotationNoteQueue.CanDelete -> HoldTimingDecider
                    // IsMiss: 900 expires at Start, after this frame's input pass.
                    return note.StartMilliseconds;
                default:
                    return note.StartMilliseconds + 125L;
            }
        }

        private static long GetResultKey(int noteId, NoteType noteType)
        {
            return ((long)noteId << 32) | (uint)noteType;
        }

        private CandidateDecision TryTapOrFlick(
            InputEntity input,
            HitLaneEntity currentHitLane,
            HitLaneEntity beganHitLane,
            NotationNote note)
        {
            // The merged list is ordered by StartMilliseconds. Tap has the
            // widest early window (125 ms), so no later Tap/Flick can consume
            // this input once the first future candidate exceeds it.
            var inputMusicMilliseconds =
                _clock.InputTimeToMusicMilliseconds(input.Milliseconds);
            if (note.StartMilliseconds >
                inputMusicMilliseconds +
                TapTimingDecider.BadMilliseconds)
            {
                return new CandidateDecision(
                    false, 0, deleteCandidate: false, stopScanning: true);
            }
            if (InputFireCore.GetCandidateAction(note.NoteType) ==
                CandidateAction.Tap)
            {
                var result = _tapAction.TryTap(input, currentHitLane, note);
                if (!result.Consumed) return default;
                ConsumedTapCount++;
                _inputResults.Add(InputResultEntity.Create(
                    result.Note, result.Timing));
                PublishNoteEffects(result.Note, result.Timing.TimingType);
                if (_holdBodyByStartId.TryGetValue(
                        result.Note.Id, out var holdBody))
                {
                    _laneHolds.Set(input.TouchId, holdBody);
                }
                return new CandidateDecision(true, result.LaneId);
            }

            if (!NotationNoteProvider.TryGetIncludedLane(
                    beganHitLane, note, out var flickLaneId))
            {
                return default;
            }
            var flick = _flickAction.TryFlick(input, note);
            if (!flick.Handled) return default;
            if (!flick.Consumed)
            {
                return new CandidateDecision(
                    true,
                    flickLaneId,
                    deleteCandidate: false);
            }
            ConsumedFlickCount++;
            _inputResults.Add(InputResultEntity.Create(
                flick.Note, flick.Timing));
            PublishNoteEffects(flick.Note, flick.Timing.TimingType);
            return new CandidateDecision(
                true,
                flickLaneId,
                deleteCandidate: flick.DeletedNote);
        }

        private void PublishNoteEffects(
            NotationNote note,
            TimingType timingType)
        {
            // InputAction publishes Beam and Bomb independently for a successful
            // notation result. HoldStart/HoldEnd remain LaneHoldManager events.
            _inputEffects.Add(InputEffectEntity.OnBeam(note, timingType));
            _inputEffects.Add(InputEffectEntity.OnBomb(note, timingType));
        }

        private static IEnumerable<NotationNote> GetFlickNotes(
            IReadOnlyList<NotationNote> notation)
        {
            foreach (var note in notation)
            {
                if (note.NoteType == (int)NoteType.Flick) yield return note;
            }
        }

    }

    /// <summary>Game-scene host for the recovered clock and InputHandler.</summary>
    public sealed class GameRuntime : MonoBehaviour
    {
        [SerializeField] private Camera _gameCamera;
        [SerializeField] private Sirius.Game.LaneGroup _laneGroup;
        [SerializeField] private string _chartRelativePath =
            "OpenWDS/StandardCharts/1/1/4.csv";
        [SerializeField] private string _musicConfigRelativePath =
            "OpenWDS/StandardCharts/1/1/music_config.csv";
        [SerializeField] private TextAsset _chartAsset;
        [SerializeField] private TextAsset _musicConfigAsset;
        [SerializeField] private bool _enableAutoJudge;
        [SerializeField] private double _noteSpeed = 5d;
        private double _currentRecommendationTiming;
        [SerializeField] private int _noteStartOffset;
        [SerializeField] private int _noteHeight = 8;
        [SerializeField] private Material _scratchHoldMaterial;
        [SerializeField] private GameObject[] _noteObjectPrefabs;
        [SerializeField] private Transform _laneEffectParent;
        [SerializeField] private Transform _splitEffectParent;
        [SerializeField] private GameObject _beamEffectPrefab;
        [SerializeField] private GameObject[] _defaultBombEffectPrefabs;
        [SerializeField] private GameObject[] _notesBombEffectPrefabs;
        [SerializeField] private GameObject[] _sakuraBombEffectPrefabs;
        [SerializeField] private BombType _bombType = BombType.Default;
        [SerializeField] private CriMusicRuntime _criMusic;
        [SerializeField] private GameSeRuntime _gameSe;
        [SerializeField] private GameClearSeRuntime _clearSe;
        [SerializeField] private GameResultSeRuntime _resultSe;
        [SerializeField] private GameResultBgmRuntime _resultBgm;
        [SerializeField] private GameObject _gameBackgroundPrefab;
        [SerializeField] private GameObject _gameResultPrefab;
        [SerializeField] private GameObject _gameResultBackgroundPrefab;
        [SerializeField] private GameObject _gameIntroductionPrefab;
        [SerializeField] private bool _isUnlockOlivier;
        [SerializeField] private GameObject _clearAnimationPrefab;
        [SerializeField] private string _musicName = "ワナビスタ！";
        [SerializeField] private long _musicId = 1;
        [SerializeField] private string _musicInfo =
            "作詞：松井洋平　作曲：光増ハジメ（FirstCall）　編曲：EFFY（FirstCall）";
        [SerializeField] private MusicDifficulty _musicDifficulty =
            MusicDifficulty.Stella;
        [SerializeField] private Sprite _musicJacketSprite;
        [SerializeField] private TextAsset _testPlayerUnitAsset;
        private CharacterPresentationRuntime _characterPresentation;
        public CharacterPresentationRuntime CharacterPresentation => _characterPresentation;
        [SerializeField] private TextAsset _localMusicCatalogAsset;

        private IReadOnlyList<LocalMusicEntry> _ratingMusics;
        private LocalMusicEntry _ratingMusic;
        private LocalLiveEntry _ratingLive;

        private InputHandlerRuntime _inputHandler;
        private SplitLaneRuntime _splitLaneRuntime;
        private SplitLaneAssetRuntime _splitLaneAssets;
        private Sirius.GameResult.GameResultFontRuntime _gameResultFonts;
        private GameResultRuntime _gameResultRuntime;
        private NoteVisualRuntime _noteVisuals;
        private LaneEffectRuntime _laneEffects;
        private GameHudRuntime _gameHud;
        private float _startedAt;
        private long _chartEndMilliseconds;
        private long _delayStartMilliseconds;
        private GameObject _gameResultInstance;
        private GameObject _gameBackgroundInstance;
        private GameObject _gameResultBackgroundInstance;
        private GameObject _boundaryCanvasObject;
        private GameObject _gameIntroductionInstance;
        private GameObject _clearAnimationInstance;
        private Sirius.Game.GameIntroductionAnimationController
            _gameIntroduction;
        private Sirius.Game.GameResultPanel _clearAnimation;
        private bool _gameplayStarted;
        private bool _clearPerformanceStarted;
        private float _clearPerformanceEndsAt;
        private bool _resultShown;
        private bool _resultNavigationPending;
        private bool _restartPending;
        private int _gameResultPresentationCount;
        private bool _shouldShowPerfectStar = true;
        private bool _isPaused;
        private bool _isRetired;
        private readonly List<InputEntity> _pendingPlayerInputs =
            new List<InputEntity>(16);
        private float _pausedAt;
        private UiSeRuntime _sharedSe;

        public event Action<GameExitRoute> ExitRouteRequested;

        public bool IsInitialized => _inputHandler != null;
        public InputHandlerRuntime InputHandler => _inputHandler;
        public SplitLaneRuntime SplitLaneRuntime => _splitLaneRuntime;
        public GameResultRuntime GameResultRuntime => _gameResultRuntime;
        public NoteVisualRuntime NoteVisuals => _noteVisuals;
        public GameHudRuntime GameHud => _gameHud;
        public GameClearSeRuntime ClearSe => _clearSe;
        public GameResultSeRuntime ResultSe => _resultSe;
        public GameResultBgmRuntime ResultBgm => _resultBgm;
        public TextAsset ChartAsset => _chartAsset;
        public MusicDifficulty MusicDifficulty => _musicDifficulty;
        public bool IsPaused => _isPaused;
        public bool IsRetired => _isRetired;
        public bool IsResultShown => _resultShown;
        public bool IsRestartPending => _restartPending;
        public int GameResultPresentationCount =>
            _gameResultPresentationCount;
        public bool IsIntroductionPlaying =>
            _gameIntroduction != null && _gameIntroduction.IsPlaying;
        public bool IsGameplayStarted => _gameplayStarted;
        public bool IsClearPerformancePlaying =>
            _clearPerformanceStarted && !_resultShown;
        public Camera GameCamera => _gameCamera;
        public bool IsAutoJudgeEnabled => _enableAutoJudge;
        public int PendingPlayerInputCount => _pendingPlayerInputs.Count;
        public long CurrentPlayerInputMilliseconds => !_gameplayStarted
            ? 0L
            : (long)((Time.realtimeSinceStartup - _startedAt) * 1000f);

        /// <summary>
        /// Feeds a device-derived input into the next non-AutoTouch frame.
        /// Milliseconds use the game clock (EnhancedTouch Touch.time relative to
        /// the live start), while positions remain bottom-left screen pixels.
        /// </summary>
        public void SubmitPlayerInput(in InputEntity input)
        {
            if (_enableAutoJudge || _inputHandler == null || !_gameplayStarted ||
                _isPaused || _isRetired || _resultShown)
                return;
            _pendingPlayerInputs.Add(input);
        }

        public long ToPlayerInputMilliseconds(double realtimeSeconds)
        {
            if (!_gameplayStarted) return 0L;
            return Math.Max(
                0L,
                (long)((realtimeSeconds - (double)_startedAt) * 1000d));
        }
        public GameExitRoute RequestedExitRoute { get; private set; }

        public void SetPaused(bool paused)
        {
            if (_isPaused == paused || _resultShown || _isRetired) return;
            _isPaused = paused;
            _characterPresentation?.SetGameplayPaused(paused);
            if (paused)
            {
                _pendingPlayerInputs.Clear();
                GetComponent<DefaultTouchRuntime>()
                    ?.SetInputSuspended(true);
                // InputHandler.<Start>b__35_1 removes every LaneHold on a game
                // state transition and publishes HoldEnd before ticking stops.
                _inputHandler?.ReleaseHoldLanesForPause();
                if (_inputHandler != null)
                {
                    foreach (var holdEvent in _inputHandler.HoldEvents)
                    {
                        if (holdEvent.EffectType ==
                            InputEffectType.HoldEnd)
                        {
                            _noteVisuals?.OnHold(holdEvent.NoteId, false);
                        }
                    }
                    if (_laneEffects != null)
                    {
                        foreach (var inputEffect in _inputHandler.InputEffects)
                            _laneEffects.OnEffect(inputEffect);
                        _laneEffects.Tick();
                    }
                }
                _pausedAt = Time.realtimeSinceStartup;
                // GameSePlayer owns one looping playback shared by every hold.
                // A paused chart cannot deliver HoldEnd, so explicitly release
                // that ownership before freezing the input pipeline.
                _gameSe?.StopAllHolds();
                _criMusic?.Pause();
                return;
            }

            // The fallback clock uses realtime since scene start. Exclude the
            // dialog interval just as the CRI playback clock does.
            _startedAt += Time.realtimeSinceStartup - _pausedAt;
            GetComponent<DefaultTouchRuntime>()
                ?.SetInputSuspended(false);
            _criMusic?.Resume();
        }

        public void RetireGame()
        {
            if (_resultShown || _inputHandler == null || _isRetired) return;

            // GamePresenter.OnPauseDialogAsync does not enter FinishGameAsync
            // after an accepted retire. It awaits GameModel.RetireGameAsync and
            // then routes the ordinary Live back through GoToLiveModeTopAsync /
            // GoToMusicSelection. Completing InputHandler here used to publish a
            // synthetic result and incorrectly open GameResult.
            SetPaused(true);
            _isRetired = true;
            RequestedExitRoute = GetRetireDestination(LiveType.Normal);
            _gameSe?.StopAllHolds();
            _criMusic?.Pause();
            ExitRouteRequested?.Invoke(RequestedExitRoute);
        }

        public static GameExitRoute GetRetireDestination(
            LiveType liveType)
        {
            if (liveType == LiveType.Normal)
                return GameExitRoute.MusicSelection;

            throw new NotSupportedException(
                $"Recovered retire routing is not implemented for {liveType}.");
        }

        public void ReturnFromResult()
        {
            if (!_resultShown || _resultNavigationPending) return;
            _resultNavigationPending = true;
            RequestedExitRoute = GameExitRoute.MusicSelection;
            ExitRouteRequested?.Invoke(RequestedExitRoute);
        }

        public void ReplayFromResult()
        {
            if (!_resultShown || _resultNavigationPending || _restartPending) return;
            _resultNavigationPending = true;
            _restartPending = true;
            StartCoroutine(ReplayAfterSe());
        }

        private IEnumerator ReplayAfterSe()
        {
            _sharedSe?.Play(UiSeRuntime.Cue.ButtonGo);
            yield return new WaitForSecondsRealtime(0.12f);
            yield return PrepareAndRestartPerformance();
        }

        /// <summary>
        /// Restarts the current performance through the same Android
        /// StreamingAssets boundary as a fresh selection. The first game load
        /// consumes and clears the prepared SplitLane bytes, so a direct scene
        /// reload cannot initialize its AssetBundles from the APK jar.
        /// </summary>
        public void RestartPerformance()
        {
            if (_restartPending || _resultNavigationPending || _isRetired) return;
            _restartPending = true;
            StartCoroutine(PrepareAndRestartPerformance());
        }

        private IEnumerator PrepareAndRestartPerformance()
        {
            if (_chartAsset == null || string.IsNullOrEmpty(_chartAsset.text))
            {
                HandleRestartPreparationFailure(
                    new InvalidOperationException(
                        "Current chart is unavailable for performance restart."));
                yield break;
            }

            yield return SplitLaneAssetRuntime
                .PrepareStreamingAssets(_chartAsset.text);
            try
            {
                SplitLaneAssetRuntime
                    .ThrowIfStreamingAssetPreparationFailed();
            }
            catch (Exception error)
            {
                HandleRestartPreparationFailure(error);
                yield break;
            }

            Debug.Log("OPENWDS_GAME_RESTART_PREPARED scene=" +
                SceneManager.GetActiveScene().name);
            if (!CurtainTransitionRuntime
                    .ReloadActiveSceneWithRetainedSource())
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void HandleRestartPreparationFailure(Exception error)
        {
            _restartPending = false;
            _resultNavigationPending = false;
            Debug.LogError("OPENWDS_GAME_RESTART_FAILED " + error.Message);
            if (_isPaused && !_resultShown && !_isRetired)
                SetPaused(false);
        }

        public void Configure(
            Camera gameCamera,
            Sirius.Game.LaneGroup laneGroup,
            TextAsset chartAsset = null,
            TextAsset musicConfigAsset = null,
            GameObject[] noteObjectPrefabs = null,
            Material scratchHoldMaterial = null,
            Transform laneEffectParent = null,
            Transform splitEffectParent = null,
            GameObject beamEffectPrefab = null,
            GameObject[] defaultBombEffectPrefabs = null,
            GameObject[] notesBombEffectPrefabs = null,
            GameObject[] sakuraBombEffectPrefabs = null,
            BombType bombType = BombType.Default,
            CriMusicRuntime criMusic = null,
            GameSeRuntime gameSe = null,
            GameObject gameBackgroundPrefab = null,
            GameObject gameResultPrefab = null,
            GameObject gameResultBackgroundPrefab = null,
            GameObject gameIntroductionPrefab = null,
            GameObject clearAnimationPrefab = null,
            string musicName = "ワナビスタ！",
            string musicInfo = "",
            MusicDifficulty musicDifficulty = MusicDifficulty.Stella,
            Sprite musicJacketSprite = null,
            TextAsset testPlayerUnitAsset = null,
            bool isUnlockOlivier = false,
            TextAsset localMusicCatalogAsset = null)
        {
            _gameCamera = gameCamera;
            _laneGroup = laneGroup;
            _chartAsset = chartAsset;
            _musicConfigAsset = musicConfigAsset;
            _noteObjectPrefabs = noteObjectPrefabs;
            _scratchHoldMaterial = scratchHoldMaterial;
            _laneEffectParent = laneEffectParent;
            _splitEffectParent = splitEffectParent;
            _beamEffectPrefab = beamEffectPrefab;
            _defaultBombEffectPrefabs = defaultBombEffectPrefabs;
            _notesBombEffectPrefabs = notesBombEffectPrefabs;
            _sakuraBombEffectPrefabs = sakuraBombEffectPrefabs;
            _bombType = bombType;
            _criMusic = criMusic;
            _gameSe = gameSe;
            _gameBackgroundPrefab = gameBackgroundPrefab;
            _gameResultPrefab = gameResultPrefab;
            _gameResultBackgroundPrefab = gameResultBackgroundPrefab;
            _gameIntroductionPrefab = gameIntroductionPrefab;
            _clearAnimationPrefab = clearAnimationPrefab;
            _musicName = musicName ?? string.Empty;
            _musicInfo = musicInfo ?? string.Empty;
            _musicDifficulty = musicDifficulty;
            _musicJacketSprite = musicJacketSprite;
            _testPlayerUnitAsset = testPlayerUnitAsset;
            _isUnlockOlivier = isUnlockOlivier;
            _localMusicCatalogAsset = localMusicCatalogAsset;
        }

        public void Initialize()
        {
            if (_inputHandler != null) return;
            _gameSe?.Configure(_enableAutoJudge);
            if (_gameCamera == null || _laneGroup == null)
                throw new InvalidOperationException("Game camera and LaneGroup are required.");
            if (LocalMusicSelectionSession.HasSelection)
            {
                var local = LocalMusicSelectionSession.Selection;
                _ratingMusic = local.Music;
                _ratingLive = local.Live;
                _ratingMusics = LocalMusicSelectionSession.Musics;
                _chartAsset = LocalMusicSelectionSession.ChartAsset;
                _musicConfigAsset =
                    LocalMusicSelectionSession.MusicConfigAsset;
                _musicName = local.Music.Name ?? string.Empty;
                _musicId = local.Music.Id;
                _musicInfo = string.Format(
                    "作詞：{0}　作曲：{1}　編曲：{2}",
                    local.Music.LyricWriter,
                    local.Music.Composer,
                    local.Music.Arranger);
                _musicDifficulty = local.Live.Difficulty;
                _musicJacketSprite =
                    LocalMusicSelectionSession.JacketSprite;
            }
            else if (_localMusicCatalogAsset != null)
            {
                // OfflineRhythmPreview can be opened directly, without first
                // visiting LocalMusicSelection.  Resolve the same master data
                // at game initialization so result calculation never depends
                // on a transient static scene hand-off still being present.
                var catalog = LocalMusicCatalog.FromJson(
                    _localMusicCatalogAsset.text);
                var local = catalog.Select(_musicId, _musicDifficulty);
                _ratingMusic = local.Music;
                _ratingLive = local.Live;
                _ratingMusics = catalog.Musics;
            }
            var persistedSettings = new SettingsStore().LoadOrDefault();
            ApplyFrameRate(persistedSettings.GameDetailSettings.IsLowFrameRate);
            _shouldShowPerfectStar =
                persistedSettings.GameDetailSettings.ShouldShowPerfectStar;
            StartCoroutine(
                Sirius.GameResult.GameResultFontRuntime
                    .PrepareStreamingAssets());
            // The desktop recovery bundles are local files. Load them under the
            // existing introduction/initialization boundary so ShowGameResult
            // does not synchronously deserialize five bundles after the last note.
            var resultFontRoot = Path.Combine(
                Application.streamingAssetsPath,
                Sirius.GameResult.GameResultFontRuntime.RelativeRoot);
            if (Directory.Exists(resultFontRoot))
                _gameResultFonts =
                    new Sirius.GameResult.GameResultFontRuntime();
            _noteSpeed = persistedSettings.GameSettings.NoteSpeed;
            _currentRecommendationTiming =
                persistedSettings.GameSettings.NoteOffsetValue;
            _noteStartOffset = persistedSettings.GameDetailSettings.NoteStartOffset;
            _noteHeight = persistedSettings.GameDetailSettings.NoteHeight;
            _bombType = (BombType)persistedSettings.GameCustomSettings.BombType;
            _criMusic?.SetVolume(GameSettings.CalculateCombinedVolume(
                persistedSettings.SoundVolumeSettings.GameMaster,
                persistedSettings.SoundVolumeSettings.GameBGM));
            _gameSe?.SetVolume(GameSettings.CalculateCombinedVolume(
                persistedSettings.SoundVolumeSettings.GameMaster,
                persistedSettings.SoundVolumeSettings.GameNotesTap));
            _sharedSe = UiSeRuntime.Instance;
            if (_sharedSe == null)
                _sharedSe = GetComponent<UiSeRuntime>();
            if (_sharedSe == null)
                _sharedSe = gameObject.AddComponent<UiSeRuntime>();
            _clearSe = GetComponent<GameClearSeRuntime>();
            if (_clearSe == null)
                _clearSe = gameObject.AddComponent<GameClearSeRuntime>();
            _resultSe = GetComponent<GameResultSeRuntime>();
            if (_resultSe == null)
                _resultSe = gameObject.AddComponent<GameResultSeRuntime>();
            _resultSe.Configure(_sharedSe);
            _resultBgm = GetComponent<GameResultBgmRuntime>();
            if (_resultBgm == null)
                _resultBgm = gameObject.AddComponent<GameResultBgmRuntime>();
            _resultBgm.SetVolume(GameSettings.CalculateCombinedVolume(
                persistedSettings.SoundVolumeSettings.GameMaster,
                persistedSettings.SoundVolumeSettings.GameBGM));
            var gameSeVolume = GameSettings.CalculateCombinedVolume(
                persistedSettings.SoundVolumeSettings.GameMaster,
                persistedSettings.SoundVolumeSettings.GameSE);
            _sharedSe.SetVolume(gameSeVolume);
            _clearSe.SetVolume(gameSeVolume);
            _laneGroup.ApplySettings(
                persistedSettings.GameDetailSettings.LaneWidth,
                persistedSettings.GameSettings.LaneAlphaValue);
            if (_gameBackgroundPrefab != null)
            {
                _gameBackgroundInstance = Instantiate(_gameBackgroundPrefab);
                _gameBackgroundInstance.name = "GameBackground";
                var shadow = _gameBackgroundInstance.GetComponent<Sirius.GameBackgroundShadow>();
                if (shadow != null) shadow.Initialize();
                var jacket = _gameBackgroundInstance.GetComponentInChildren<
                    Sirius.Game.GameBackgroundJacket>(true);
                if (jacket != null) jacket.Initialize(_musicJacketSprite);
            }
            var managers = _laneGroup.ColliderManagers;
            if (managers == null)
                throw new InvalidOperationException("LaneColliderManagers is required.");
            managers.InitializeMappings();
            _laneGroup.InitializeSplitLaneVisual(
                _gameCamera,
                persistedSettings.GameSettings.LaneAlphaValue);

            var chartPath = Path.Combine(
                Application.streamingAssetsPath, _chartRelativePath);
            var configPath = Path.Combine(
                Application.streamingAssetsPath, _musicConfigRelativePath);
            // Serialized TextAssets keep the offline scene Android-safe; direct
            // filesystem paths remain an Editor/desktop fallback.
            var notation = StandardNotation.Parse(
                _chartAsset != null ? _chartAsset.text : File.ReadAllText(chartPath));
            if (persistedSettings.GameDetailSettings.IsActiveSplitRandom)
            {
                NotationNoteProcessor.UpdateForSplitRandom(notation);
                // Lane changes alter adjacency, so rebuild the derived collider
                // suppression flags after the retail randomization pass.
                NotationNoteProcessor.SetOuterCollider(notation);
            }
            var config = StandardNotation.ParseMusicConfig(
                _musicConfigAsset != null
                    ? _musicConfigAsset.text
                    : File.ReadAllText(configPath));
            _delayStartMilliseconds = (long)(config.DelayStartSeconds * 1000f);
            if (_laneGroup.NoteParent == null)
                throw new InvalidOperationException("LaneGroup NoteParent is required.");
            ApplyNoteStartMask();
            // Migration cleanup for scenes generated before the real scheduler was
            // connected. Newly generated scenes contain no Preview_* children.
            for (var index = 0; index < _laneGroup.NoteParent.childCount; index++)
            {
                var child = _laneGroup.NoteParent.GetChild(index);
                if (child.name.StartsWith("Preview_", StringComparison.Ordinal))
                    child.gameObject.SetActive(false);
            }
            _noteVisuals = new NoteVisualRuntime(
                notation,
                _laneGroup.NoteParent,
                _noteObjectPrefabs,
                _noteSpeed,
                persistedSettings.GameSettings.NoteOffsetValue,
                _scratchHoldMaterial,
                _noteHeight,
                persistedSettings.GameDetailSettings.IsActiveConcurrentLine);
            _splitLaneRuntime = new SplitLaneRuntime(notation);
            if (_splitEffectParent == null)
                throw new InvalidOperationException(
                    "LaneEffectController SplitEffectParent is required.");
            _splitLaneAssets = new SplitLaneAssetRuntime(
                _splitEffectParent,
                persistedSettings.GameSettings.SpritEffectSettingType,
                persistedSettings.GameSettings.SplitEffectLineOpacity,
                notation);
            _gameResultRuntime = new GameResultRuntime(notation);
            _gameHud = GetComponent<GameHudRuntime>();
            _gameHud?.ApplySettings(
                persistedSettings.GameSettings,
                persistedSettings.GameDetailSettings);
            _gameHud?.Initialize();
            if (_gameHud != null && _testPlayerUnitAsset != null)
            {
                var playerUnit = PlayerUnitFixture.Parse(_testPlayerUnitAsset);
                var scoreNoteIds = _gameResultRuntime.GetScoreNoteIds(notation);
                _gameHud.InitializeScore(playerUnit.CreateScoreContext(scoreNoteIds));
                _gameHud.InitializeSenseScore(playerUnit);
                _gameHud.ApplyStartEffects(playerUnit);
                if (Application.isPlaying)
                {
                    _characterPresentation = gameObject.AddComponent<CharacterPresentationRuntime>();
                    _gameHud.StarActActivated += _characterPresentation.OnStarAct;
                    StartCoroutine(_characterPresentation.Prepare(playerUnit, _gameCamera,
                        persistedSettings.GameSettings.GameEndVoiceSettingType,
                        GameSettings.CalculateCombinedVolume(
                            persistedSettings.SoundVolumeSettings.SystemMaster,
                            persistedSettings.SoundVolumeSettings.SystemVoice),
                        gameVoiceVolume: GameSettings.CalculateCombinedVolume(
                            persistedSettings.SoundVolumeSettings.GameMaster,
                            persistedSettings.SoundVolumeSettings.GameVoice),
                        sensePanel: _gameHud.AdditionalScoreCutInPanel));
                }
            }
            CreateBoundaryPerformances();
            foreach (var note in notation)
            {
                _chartEndMilliseconds = Math.Max(
                    _chartEndMilliseconds,
                    Math.Max(note.StartMilliseconds, note.EndMilliseconds));
            }
            var raycaster = new LaneRaycaster(
                _gameCamera,
                managers.MainColliders,
                managers.SubLeftInnerColliders,
                managers.SubRightInnerColliders,
                managers.SubLeftOuterColliders,
                managers.SubRightOuterColliders);
            _inputHandler = new InputHandlerRuntime(
                notation,
                config.DelayStartSeconds,
                note =>
                {
                    var screen = _gameCamera.WorldToScreenPoint(
                        _laneGroup.GetLaneCollider(note.Lane).position);
                    return new Vector2(screen.x, screen.y);
                },
                raycaster.RaycastPoint,
                noteTimingValue: persistedSettings.GameSettings.NoteTimingValue,
                captureFlickDiagnostics: false,
                laneRaycaster: raycaster);
            if (!_enableAutoJudge)
            {
                var defaultTouch = GetComponent<DefaultTouchRuntime>();
                if (defaultTouch == null)
                    defaultTouch = gameObject.AddComponent<
                        DefaultTouchRuntime>();
                defaultTouch.Configure(this);
            }
            if (_laneEffectParent == null)
                _laneEffectParent = _laneGroup.LaneEffectParent;
            if (_laneEffectParent != null && _beamEffectPrefab != null &&
                _defaultBombEffectPrefabs != null)
                _laneEffects = new LaneEffectRuntime(
                    _laneGroup, _laneEffectParent, _beamEffectPrefab, _bombType,
                    _defaultBombEffectPrefabs, _notesBombEffectPrefabs,
                    _sakuraBombEffectPrefabs,
                    persistedSettings.GameDetailSettings.IsActiveKeyBeam,
                    (GameTapEffectType)persistedSettings
                        .GameDetailSettings.TapEffectType);
            if (_gameIntroduction != null)
            {
                _gameHud?.Hide();
                // The production caller supplies Olivier together with the
                // first-unlock flag. Keep the offline inspector switch
                // self-contained so toggling it reproduces that same call.
                var introductionDifficulty = _isUnlockOlivier
                    ? MusicDifficulty.Olivier
                    : _musicDifficulty;
                _gameIntroduction.Play(
                    Math.Max(0, (int)introductionDifficulty - 1),
                    _isUnlockOlivier);
            }
            else
            {
                BeginGameplay();
            }
        }

        private void CreateBoundaryPerformances()
        {
            if (_gameIntroductionPrefab == null &&
                _clearAnimationPrefab == null) return;
            _boundaryCanvasObject = new GameObject(
                "GameBoundaryCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler));
            _boundaryCanvasObject.transform.SetParent(transform, false);
            var canvas =
                _boundaryCanvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = _boundaryCanvasObject.GetComponent<
                UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode =
                UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1136f, 640f);
            scaler.screenMatchMode =
                UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 0f;

            if (_gameIntroductionPrefab != null)
            {
                _gameIntroductionInstance = Instantiate(
                    _gameIntroductionPrefab, _boundaryCanvasObject.transform,
                    false);
                StretchToCanvas(_gameIntroductionInstance.transform);
                _gameIntroduction = _gameIntroductionInstance.GetComponent<
                    Sirius.Game.GameIntroductionAnimationController>();
                if (_gameIntroduction == null)
                    throw new InvalidOperationException(
                        "GameIntroduction controller is missing.");
                _gameIntroduction.Initialize(
                    _musicJacketSprite, _musicName, _musicInfo);
            }
            if (_clearAnimationPrefab != null)
            {
                _clearAnimationInstance = Instantiate(
                    _clearAnimationPrefab, _boundaryCanvasObject.transform,
                    false);
                StretchToCanvas(_clearAnimationInstance.transform);
                _clearAnimation = _clearAnimationInstance.GetComponent<
                    Sirius.Game.GameResultPanel>();
                if (_clearAnimation == null)
                    throw new InvalidOperationException(
                        "ClearAnimation GameResultPanel is missing.");
                _clearAnimation.Hide();
            }
        }

        private static void StretchToCanvas(Transform target)
        {
            var rect = target as RectTransform;
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private bool _gameplayIntroductionPending;

        private void BeginGameplay()
        {
            if (_gameplayStarted || _gameplayIntroductionPending) return;
            _gameplayIntroductionPending = true;
            StartCoroutine(BeginGameplayAfterHud());
        }

        private IEnumerator BeginGameplayAfterHud()
        {
            while (_characterPresentation != null && !_characterPresentation.IsReady)
                yield return null;
            if (_gameHud != null) yield return _gameHud.ShowIntroduction();
            _gameplayStarted = true;
            _gameplayIntroductionPending = false;
            _startedAt = Time.realtimeSinceStartup;
            _pendingPlayerInputs.Clear();
            _gameHud?.Show();
            _criMusic?.BeginPlayback();
        }

        private void ApplyNoteStartMask()
        {
            var maskScaler = _laneGroup.GetComponentInChildren<Sirius.Game.LaneMaskScaler>(true);
            if (maskScaler != null)
            {
                var position = maskScaler.transform.localPosition;
                position.y = OriginalGameConfig.NoteStartPositionY +
                             OriginalGameConfig.LaneMaskOffsetY;
                maskScaler.transform.localPosition = position;
                var scale = maskScaler.transform.localScale;
                scale.y = OriginalGameConfig.GetLaneMaskScaleY(
                    _noteStartOffset);
                maskScaler.transform.localScale = scale;
            }

            var startLine = _laneGroup.GetComponentInChildren<Sirius.Game.LaneNoteStartLine>(true);
            if (startLine != null)
            {
                var position = startLine.transform.localPosition;
                position.y = OriginalGameConfig.GetNoteVisiblePositionY(
                    _noteStartOffset);
                startLine.transform.localPosition = position;
            }
        }

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (_inputHandler == null || _isPaused || _isRetired) return;
            if (!_gameplayStarted)
            {
                if (_gameIntroduction == null ||
                    !_gameIntroduction.IsPlaying)
                    BeginGameplay();
                return;
            }
            if (_clearPerformanceStarted)
            {
                if (Time.realtimeSinceStartup >= _clearPerformanceEndsAt)
                {
                    _clearPerformanceStarted = false;
                    _clearAnimation?.Hide();
                    ShowGameResult();
                }
                return;
            }
            var elapsed = Time.realtimeSinceStartup - _startedAt;
            var gameMilliseconds = (long)(elapsed * 1000f);
            // MusicTime.PassedMilliseconds is player time minus DelayStartSeconds.
            // Keep the original negative pre-roll so notes whose spawn threshold is
            // before chart zero (especially Hold endpoints initialized by Set) are
            // created at the same clock phase as the game.
            var chartMilliseconds = _criMusic != null && _criMusic.IsPrepared
                ? _criMusic.GetChartMilliseconds()
                : gameMilliseconds - _delayStartMilliseconds;
            _splitLaneRuntime.Tick(chartMilliseconds);
            foreach (var splitLaneEntry in _splitLaneRuntime.FrameEntries)
            {
                _laneGroup.OnSplitLane(splitLaneEntry);
                _splitLaneAssets?.OnSplitLane(splitLaneEntry);
            }
            if (_enableAutoJudge)
            {
                _inputHandler.Tick(chartMilliseconds, gameMilliseconds);
            }
            else
            {
                _inputHandler.TickPlayer(
                    chartMilliseconds,
                    gameMilliseconds,
                    _pendingPlayerInputs);
                _pendingPlayerInputs.Clear();
            }
            // CompleteGame publishes the final HoldEnd events. Run it before the
            // event consumers so they are observed in this frame instead of being
            // cleared by the next Tick call.
            if (chartMilliseconds > _chartEndMilliseconds + 1000)
                _inputHandler.CompleteGame();
            foreach (var holdEvent in _inputHandler.HoldEvents)
            {
                if (holdEvent.EffectType == InputEffectType.HoldStart)
                    _noteVisuals.OnHold(holdEvent.NoteId, true);
                else if (holdEvent.EffectType == InputEffectType.HoldEnd)
                    _noteVisuals.OnHold(holdEvent.NoteId, false);
            }
            if (_laneEffects != null)
            {
                foreach (var inputEffect in _inputHandler.InputEffects)
                    _laneEffects.OnEffect(inputEffect);
                _laneEffects.Tick();
            }
            if (_gameSe != null)
                _gameSe.ProcessFrame(
                    _inputHandler.InputResults,
                    _inputHandler.InputEffects,
                    chartMilliseconds,
                    _inputHandler.ActiveTouchHolds);
            foreach (var inputResult in _inputHandler.InputResults)
                _gameResultRuntime.Collect(inputResult);
            _noteVisuals.OnInputResults(_inputHandler.InputResults);
            _gameHud?.TickSenseScore(chartMilliseconds);
            _gameHud?.ProcessFrame(
                _inputHandler.InputResults, _gameResultRuntime);
            _noteVisuals.Tick(chartMilliseconds);
            if (_inputHandler.IsGameCompleted && !_resultShown)
                BeginClearPerformance();
        }

        /// <summary>
        /// GameFrameRateChanger selects DefaultInGame (60) when low frame rate is
        /// enabled and HighInGame (120) otherwise. FpsChanger writes it directly
        /// to Application.targetFrameRate.
        /// </summary>
        public static int ApplyFrameRate(bool isLowFrameRate)
        {
            var frameRate = isLowFrameRate ? 60 : 120;
            Application.targetFrameRate = frameRate;
            return frameRate;
        }

        private void BeginClearPerformance()
        {
            if (_clearPerformanceStarted) return;
            _clearPerformanceStarted = true;
            _gameSe?.StopAllHolds();
            if (_clearAnimation == null)
            {
                ShowGameResult();
                return;
            }
            var type = GetBoundaryClearType(_gameResultRuntime);
            _clearSe?.Play(type);
            _clearPerformanceEndsAt = Time.realtimeSinceStartup +
                _clearAnimation.Show(type);
        }

        public static Sirius.Game.BoundaryClearType
            GetBoundaryClearType(GameResultRuntime result)
        {
            if (result == null)
                return Sirius.Game.BoundaryClearType.Failed;
            if (result.IsAllPerfect)
                return Sirius.Game.BoundaryClearType.AllPerfect;
            if (result.IsFullCombo)
                return Sirius.Game.BoundaryClearType.FullCombo;
            return Sirius.Game.BoundaryClearType.Clear;
        }

        private void ShowGameResult()
        {
            if (_resultShown) return;
            if (_gameResultPrefab == null)
                throw new InvalidOperationException("Game result prefab is required.");
            _resultShown = true;
            _gameResultPresentationCount++;
            _gameSe?.StopAllHolds();
            _gameHud?.Hide();

            if (_gameResultBackgroundPrefab != null)
            {
                _gameResultBackgroundInstance = Instantiate(_gameResultBackgroundPrefab);
                // GameResultStandbyController.PlayStandbyMove explicitly enables
                // CurtainClose. The extracted subtree preserves its authored
                // inactive root, so reproduce that controller call here.
                _gameResultBackgroundInstance.SetActive(true);
            }
            // GameResultStandbyController.PlayStandbyMove publishes
            // BgmType.GameResult (enum value 2) at this scene boundary.
            _resultBgm?.Play();

            // Game and GameResult are separate original scenes. The curtain
            // subtree contains the authored GameResult camera at depth -1;
            // keeping this preview's depth-0 game camera enabled makes it clear
            // after the curtain camera and erases the curtain frame. Preserve
            // the original z=-14 placement and switch cameras at the scene
            // boundary instead of moving the Spine object into the game camera.
            if (_gameCamera != null)
                _gameCamera.enabled = false;

            var canvasObject = new GameObject(
                "GameResultCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler),
                typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            // Prepare zero digits and inactive badges before any result effect
            // receives OnEnable. Initialize also serves settled-state fixtures.
            canvasObject.SetActive(false);
            _gameResultInstance = Instantiate(_gameResultPrefab, canvasObject.transform);
            var rect = _gameResultInstance.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
            }
            // Font replacement invalidates TMP/layout data. Match the capture and
            // original full-screen root order: establish the final canvas rect
            // before any font-driven layout rebuild.
            if (_gameResultFonts == null)
                _gameResultFonts =
                    new Sirius.GameResult.GameResultFontRuntime();
            _gameResultFonts.Apply(_gameResultInstance);
            var rootCanvasGroup = _gameResultInstance.GetComponent<CanvasGroup>();
            if (rootCanvasGroup != null)
            {
                // Original GameResultView.ShowAsync calls ShowAlphaAsync here.
                rootCanvasGroup.alpha = 1f;
                rootCanvasGroup.interactable = true;
                rootCanvasGroup.blocksRaycasts = true;
            }
            var localResults = new LocalResultStore();
            var isRatingTarget = PlayerRating.IsEligible(
                _ratingMusic, _ratingLive);
            var beforePlayerRate = _ratingMusics != null
                ? PlayerRating.CalculatePlayerRate(
                    _ratingMusics, localResults)
                : 0d;
            var previousNotationRate = isRatingTarget
                ? PlayerRating.CalculateNotationRate(
                    _ratingLive.Level,
                    localResults.GetBest(_musicId, _musicDifficulty),
                    localResults.HasClear(_musicId, _musicDifficulty))
                : 0d;
            var isNewAchievementRate = localResults.RecordResult(
                _musicId,
                _musicDifficulty,
                _gameResultRuntime.AchievementRate,
                _gameResultRuntime.IsAllPerfect
                    ? ClearLamp.AllPerfect
                    : _gameResultRuntime.IsFullCombo
                        ? ClearLamp.FullCombo
                        : ClearLamp.Clear,
                out var previousBestAchievementRate);
            if (_characterPresentation != null)
            {
                var clearLamp = _gameHud != null && _gameHud.Life.Value <= 0 ? 0 :
                    _gameResultRuntime.IsAllPerfect ? 6 : _gameResultRuntime.IsFullCombo ? 2 : 1;
                StartCoroutine(_characterPresentation.ShowResult(
                    _gameResultBackgroundInstance, clearLamp, isNewAchievementRate));
            }
            var thisTimeNotationRate = isRatingTarget
                ? PlayerRating.CalculateNotationRate(
                    _ratingLive.Level,
                    _gameResultRuntime.AchievementRate)
                : 0d;
            var bestEverNotationRate = isRatingTarget
                ? PlayerRating.CalculateNotationRate(
                    _ratingLive.Level,
                    localResults.GetBest(_musicId, _musicDifficulty),
                    localResults.HasClear(_musicId, _musicDifficulty))
                : 0d;
            var afterPlayerRate = _ratingMusics != null
                ? PlayerRating.CalculatePlayerRate(
                    _ratingMusics, localResults)
                : 0d;
            var isNewNotationRate =
                bestEverNotationRate > previousNotationRate;
            var isNewPlayerRate = afterPlayerRate > beforePlayerRate;
            BindMusicInfo(_gameResultInstance.transform, localResults);
            var panel = _gameResultInstance.GetComponentInChildren<
                Sirius.GameResult.GameResultPanel>(true);
            if (panel == null)
                throw new InvalidOperationException("GameResultPanel is missing.");
            // Only the product's pre-live Autoplay button may set IsAuto.
            // _enableAutoJudge is a debug input injector and must keep the
            // ordinary HUD/result presentation. That button is not recovered yet.
            const bool isOfficialAutoplay = false;
            var viewData =
                Sirius.GameResult.GameResultViewData.FromRuntime(
                    _gameResultRuntime,
                    _currentRecommendationTiming,
                    _noteSpeed,
                    isOfficialAutoplay,
                    _musicName,
                    _musicDifficulty,
                    _musicJacketSprite,
                    previousBestAchievementRate,
                    isNewAchievementRate,
                    _shouldShowPerfectStar,
                    // GameResultRate needs the value before this play on the
                    // left of its old -> new presentation.  The store already
                    // contains the new result at this point, so passing
                    // bestEverNotationRate made both numbers identical.
                    previousNotationRate,
                    thisTimeNotationRate,
                    beforePlayerRate,
                    afterPlayerRate,
                    isNewNotationRate,
                    isNewPlayerRate);
            panel.Initialize(viewData);
            BindResultNavigation();
            // GameResultView.ShowAsync starts from the serialized root alpha 0
            // and calls AnimationUtility.ShowAlphaAsync (linear 0.2 seconds).
            // Initialize also supports settled-state editor previews.
            if (rootCanvasGroup != null) rootCanvasGroup.alpha = 0f;

            // GameResultView.ShowAsync sets the original root Animator's "Next"
            // trigger before awaiting its entrance state. Without the removed
            // GameResultView component the controller remains in GameResult_in,
            // whose final LeftPanel X is 52; GameResult_left_in ends at the prefab
            // position X=477. Keep the original pivot and drive the missing call.
            var slideAnimator = _gameResultInstance.GetComponent<Animator>();
            StartCoroutine(CompleteResultPresentation(slideAnimator));

            // GamePresenter stops the Game presentation before opening the
            // separately-authored GameResult presentation.
            _laneGroup.gameObject.SetActive(false);
            if (_gameBackgroundInstance != null)
                _gameBackgroundInstance.SetActive(false);
        }

        private void BindResultNavigation()
        {
            var root = _gameResultInstance.transform.Find("RightBotton");
            var next = root != null
                ? root.Find("NextButton")?.GetComponent<Button>()
                : null;
            var replay = root != null
                ? root.Find("InGameButton")?.GetComponent<Button>()
                : null;
            if (root == null || next == null || replay == null)
                throw new InvalidOperationException(
                    "Original GameResult NextButton/InGameButton are missing.");
            // GameResultView.Initialize calls GameResultNextPanel.Inactivate.
            // The Presenter activates the whole panel only after the entrance
            // and its numeric result animation have completed.
            next.gameObject.SetActive(true);
            replay.gameObject.SetActive(true);
            root.gameObject.SetActive(false);
            next.onClick.RemoveAllListeners();
            replay.onClick.RemoveAllListeners();
            next.onClick.AddListener(ReturnFromResult);
            replay.onClick.AddListener(ReplayFromResult);
        }

        private IEnumerator CompleteResultPresentation(
            Animator slideAnimator)
        {
            var resultPanel = _gameResultInstance.GetComponentInChildren<
                Sirius.GameResult.GameResultPanel>(true);
            var counts = resultPanel.CreateCountUp(_resultSe);
            _gameResultInstance.transform.parent.gameObject.SetActive(true);
            var canvasGroup = _gameResultInstance.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
                canvasGroup.DOFade(1f, 0.2f).SetEase(Ease.Linear)
                    .SetLink(_gameResultInstance);
            if (slideAnimator != null)
            {
                // The controller must be enabled before receiving Next. Sending
                // it under the inactive preparation Canvas loses the trigger
                // when the Animator initializes and leaves LeftPanel at X=52.
                slideAnimator.SetTrigger(Animator.StringToHash("Next"));
                // GameResultView.ShowAsync activates _StageSuccess (offset
                // 0xA8) immediately after Next; its authored root is inactive.
                var stageSuccess = _gameResultInstance.transform.Find(
                    "SucceseTextImagePosition/GameResultStageSuccess");
                if (stageSuccess == null)
                    throw new InvalidOperationException("Stage Success presentation is missing.");
                stageSuccess.gameObject.SetActive(true);
                // Let the trigger transition be evaluated, then reproduce
                // GameResultView.ShowAsync's normalized-time completion wait.
                yield return null;
                while (slideAnimator != null &&
                       (slideAnimator.IsInTransition(0) ||
                        slideAnimator.GetCurrentAnimatorStateInfo(0)
                            .normalizedTime < 1f))
                {
                    yield return null;
                }
            }

            yield return counts.Play().WaitForCompletion();

            if (_gameResultInstance == null) yield break;
            var navigation = _gameResultInstance.transform.Find("RightBotton");
            if (navigation != null)
                navigation.gameObject.SetActive(true);
        }

        private void OnDestroy()
        {
            if (_gameHud != null && _characterPresentation != null)
                _gameHud.StarActActivated -= _characterPresentation.OnStarAct;
            _inputHandler?.Dispose();
            _inputHandler = null;
            _splitLaneAssets?.Dispose();
            _splitLaneAssets = null;
            _gameResultFonts?.Dispose();
            _gameResultFonts = null;
        }

        private void BindMusicInfo(
            Transform root,
            LocalResultStore localResults)
        {
            var musicInfo = root.Find("LeftPanel/MusicInfoPanel");
            if (musicInfo == null) return;

            var title = musicInfo.Find("MusicNamelText/BodyRoot/BodyText")
                ?.GetComponent<UnityEngine.UI.Text>();
            if (title != null) title.text = _musicName;
            var difficulty = musicInfo.Find("Difficulty/Text")
                ?.GetComponent<UnityEngine.UI.Text>();
            if (difficulty != null)
                difficulty.text = _musicDifficulty.ToString().ToUpperInvariant();
            var difficultyImage = musicInfo.Find("Difficulty")
                ?.GetComponent<UnityEngine.UI.Image>();
            if (difficultyImage != null)
                difficultyImage.color = GameResultDifficultyColor(
                    _musicDifficulty);
            var jacket = musicInfo.Find("FocusMask/JacketImage")
                ?.GetComponent<UnityEngine.UI.Image>();
            if (jacket != null && _musicJacketSprite != null)
                jacket.sprite = _musicJacketSprite;

            var lamps = musicInfo.Find("ClearLamps");
            var lampEffects = musicInfo.Find("ClearLampEffects");
            if (lamps != null)
            {
                for (var index = 0; index < lamps.childCount; index++)
                {
                    var child = lamps.GetChild(index);
                    var suffix = child.name.StartsWith("Lamp", StringComparison.Ordinal)
                        ? child.name.Substring(4)
                        : string.Empty;
                    var lampStatus =
                        Enum.TryParse(
                            suffix,
                            true,
                            out MusicDifficulty lampDifficulty) &&
                        localResults != null &&
                        localResults.HasClear(_musicId, lampDifficulty)
                            ? localResults.GetClearLamp(
                                _musicId, lampDifficulty)
                            : ClearLamp.None;
                    // The result header always presents the complete five-slot
                    // difficulty row: Normal, Hard, Extra, Stella and Olivier.
                    // An uncleared slot keeps its authored empty lamp image.
                    child.gameObject.SetActive(true);
                    for (var childIndex = 0;
                         childIndex < child.childCount;
                         childIndex++)
                    {
                        var lamp = child.GetChild(childIndex);
                        lamp.gameObject.SetActive(
                            lamp.name == "ClearLampImage");
                        var image = lamp.GetComponent<UnityEngine.UI.Image>();
                        if (lampStatus != ClearLamp.None)
                        {
                            var source = FindResultLampSource(
                                lampEffects, suffix, lampStatus);
                            if (image != null && source != null)
                                image.sprite = source.sprite;
                        }
                    }
                }
            }
            if (lampEffects != null) lampEffects.gameObject.SetActive(false);
        }

        private static Color32 GameResultDifficultyColor(
            MusicDifficulty difficulty)
        {
            // GameResultMusicInfoPanel.Initialize calls
            // ColorPreset.get_Difficulty_* rather than the darker selection
            // frame palette. Stella is packed as 0xFFE96786 in the original
            // ARM64 method: RGBA (134, 103, 233, 255).
            if (difficulty == MusicDifficulty.Stella)
                return new Color32(134, 103, 233, 255);
            return MusicSelectionPreviewRuntime.DifficultyFrameColor(
                difficulty);
        }

        private static UnityEngine.UI.Image FindResultLampSource(
            Transform effects,
            string difficulty,
            ClearLamp lamp)
        {
            if (effects == null) return null;
            var state = lamp == ClearLamp.AllPerfect
                ? "ClearLampAllParfect"
                : lamp == ClearLamp.FullCombo
                    ? "ClearLampFullCombo"
                    : "ClearLampClear";
            return effects.Find(
                    "ClearLampEffect" + difficulty + "/" + state +
                    "/ClearLampImage")
                ?.GetComponent<UnityEngine.UI.Image>();
        }
    }
}
