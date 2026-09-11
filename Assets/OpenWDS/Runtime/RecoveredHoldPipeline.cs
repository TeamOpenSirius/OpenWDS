using System;
using System.Collections.Generic;

namespace OpenWDS.Runtime
{
    public enum RecoveredInputEffectType
    {
        None = 0,
        Beam = 1,
        Bomb = 2,
        HoldStart = 3,
        HoldEnd = 4,
        EmptyTap = 5,
    }

    /// <summary>
    /// Field layout and factories recovered from InputEffectEntity. Equality in the
    /// player intentionally uses NoteId only.
    /// </summary>
    public readonly struct RecoveredInputEffectEntity :
        IEquatable<RecoveredInputEffectEntity>
    {
        public readonly int NoteId;
        public readonly long StartMilliseconds;
        public readonly int LaneId;
        public readonly int Width;
        public readonly RecoveredNoteType NoteType;
        public readonly RecoveredTimingType TimingType;
        public readonly RecoveredInputEffectType EffectType;

        private RecoveredInputEffectEntity(
            int noteId,
            long startMilliseconds,
            int laneId,
            int width,
            RecoveredNoteType noteType,
            RecoveredTimingType timingType,
            RecoveredInputEffectType effectType)
        {
            NoteId = noteId;
            StartMilliseconds = startMilliseconds;
            LaneId = laneId;
            Width = width;
            NoteType = noteType;
            TimingType = timingType;
            EffectType = effectType;
        }

        public static RecoveredInputEffectEntity OnBeam(
            RecoveredNotationNote note,
            RecoveredTimingType timingType)
        {
            if (note == null) throw new ArgumentNullException(nameof(note));
            return new RecoveredInputEffectEntity(
                note.Id,
                note.StartMilliseconds,
                note.Lane,
                note.Width,
                (RecoveredNoteType)note.NoteType,
                timingType,
                RecoveredInputEffectType.Beam);
        }

        public static RecoveredInputEffectEntity OnBomb(
            RecoveredNotationNote note,
            RecoveredTimingType timingType)
        {
            if (note == null) throw new ArgumentNullException(nameof(note));
            return new RecoveredInputEffectEntity(
                note.Id,
                note.StartMilliseconds,
                note.Lane,
                note.Width,
                (RecoveredNoteType)note.NoteType,
                timingType,
                RecoveredInputEffectType.Bomb);
        }

        public static RecoveredInputEffectEntity OnHoldStart(
            RecoveredNotationNote note)
        {
            if (note == null) throw new ArgumentNullException(nameof(note));
            return new RecoveredInputEffectEntity(
                note.Id,
                note.StartMilliseconds,
                note.Lane,
                note.Width,
                (RecoveredNoteType)note.NoteType,
                RecoveredTimingType.None,
                RecoveredInputEffectType.HoldStart);
        }

        public static RecoveredInputEffectEntity OnHoldEnd(
            RecoveredNotationNote note)
        {
            if (note == null) throw new ArgumentNullException(nameof(note));
            return new RecoveredInputEffectEntity(
                note.Id,
                note.StartMilliseconds,
                note.Lane,
                note.Width,
                (RecoveredNoteType)note.NoteType,
                RecoveredTimingType.None,
                RecoveredInputEffectType.HoldEnd);
        }

        public bool Equals(RecoveredInputEffectEntity other) => NoteId == other.NoteId;
        public override bool Equals(object obj) =>
            obj is RecoveredInputEffectEntity other && Equals(other);
        public override int GetHashCode() => NoteId;
    }

    /// <summary>
    /// Hold and short-Hold-end windows recovered from GameTimingEntityFactory and
    /// HoldTimingDecider. Every interval is lower-inclusive and upper-exclusive.
    /// </summary>
    public static class RecoveredHoldTimingDecider
    {
        public const long HoldEndThresholdMilliseconds = 150;
        public const long ApproximateInfiniteUpperBoundMilliseconds = 86400000;

        public static RecoveredTimingDecision DecideEndMusicTime(
            long inputMusicMilliseconds,
            long holdStartMusicMilliseconds,
            long holdEndMusicMilliseconds)
        {
            var diff = inputMusicMilliseconds - holdEndMusicMilliseconds;
            var duration = holdEndMusicMilliseconds - holdStartMusicMilliseconds;
            var timing = duration < HoldEndThresholdMilliseconds
                ? GetShortHoldEndTiming(diff)
                : GetHoldTiming(diff);
            return timing == RecoveredTimingType.None
                ? new RecoveredTimingDecision(
                    RecoveredTimingType.None,
                    RecoveredTimingAssistType.None,
                    0)
                : new RecoveredTimingDecision(
                    timing,
                    RecoveredTimingAssistType.None,
                    diff);
        }

        public static RecoveredTimingDecision DecideEnd(
            RecoveredGameClock clock,
            long inputGameMilliseconds,
            RecoveredNotationNote hold)
        {
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            if (hold == null) throw new ArgumentNullException(nameof(hold));
            return DecideEndMusicTime(
                clock.InputTimeToMusicMilliseconds(inputGameMilliseconds),
                hold.StartMilliseconds,
                hold.EndMilliseconds);
        }

        private static RecoveredTimingType GetHoldTiming(long diff)
        {
            if (InRange(diff, -40, ApproximateInfiniteUpperBoundMilliseconds))
                return RecoveredTimingType.PerfectStar;
            if (InRange(diff, -60, -40)) return RecoveredTimingType.PerfectStar;
            if (InRange(diff, -100, -60)) return RecoveredTimingType.Great;
            if (InRange(diff, -125, -100)) return RecoveredTimingType.Good;
            return RecoveredTimingType.None;
        }

        private static RecoveredTimingType GetShortHoldEndTiming(long diff)
        {
            if (InRange(diff, -40, ApproximateInfiniteUpperBoundMilliseconds))
                return RecoveredTimingType.PerfectStar;
            if (InRange(diff, -75, -40)) return RecoveredTimingType.PerfectStar;
            return RecoveredTimingType.None;
        }

        private static bool InRange(long value, long from, long to) =>
            from <= value && value < to;
    }

    public readonly struct RecoveredHoldActionResult
    {
        public readonly bool Assigned;
        public readonly bool ReleaseCurrent;
        public readonly bool Consumed;
        public readonly int LaneId;
        public readonly RecoveredNotationNote HoldNote;
        public readonly RecoveredNotationNote AssignmentNote;
        public readonly RecoveredNotationNote HoldStartNote;
        public readonly RecoveredTimingDecision Timing;
        public readonly int DeletedHoldingNoteCount;
        public readonly int DeletedScratchNoteCount;

        public RecoveredHoldActionResult(
            bool assigned,
            bool consumed,
            int laneId,
            RecoveredNotationNote holdNote,
            RecoveredNotationNote holdStartNote,
            RecoveredTimingDecision timing,
            int deletedHoldingNoteCount,
            int deletedScratchNoteCount = 0,
            RecoveredNotationNote assignmentNote = null,
            bool releaseCurrent = false)
        {
            Assigned = assigned;
            ReleaseCurrent = releaseCurrent;
            Consumed = consumed;
            LaneId = laneId;
            HoldNote = holdNote;
            AssignmentNote = assignmentNote ?? holdNote;
            HoldStartNote = holdStartNote;
            Timing = timing;
            DeletedHoldingNoteCount = deletedHoldingNoteCount;
            DeletedScratchNoteCount = deletedScratchNoteCount;
        }

        public static RecoveredHoldActionResult OnRelease()
        {
            return new RecoveredHoldActionResult(
                false, false, 0, null, null, default, 0,
                releaseCurrent: true);
        }

        public static RecoveredHoldActionResult OnAssigned(
            int laneId,
            RecoveredNotationNote holdNote)
        {
            return new RecoveredHoldActionResult(
                true, false, laneId, holdNote, null, default, 0);
        }
    }

    /// <summary>
    /// Standard-chart subset of NotationNoteManager's Hold providers. The queue
    /// type filters, main/sub precedence, inclusive lane coverage and HoldStart
    /// pairing are recovered from the original provider and manager methods.
    /// Scratch/Jump-Scratch branches are deliberately not approximated here.
    /// </summary>
    public sealed class RecoveredStandardHoldNoteManager
    {
        private readonly List<RecoveredNotationNote> _holdNotes =
            new List<RecoveredNotationNote>();
        private readonly List<RecoveredNotationNote> _holdingNotes =
            new List<RecoveredNotationNote>();
        private readonly List<RecoveredNotationNote> _holdStartNotes =
            new List<RecoveredNotationNote>();
        private readonly HashSet<int> _uncompletedHoldStartNoteIds =
            new HashSet<int>();

        public int HoldNoteCount => _holdNotes.Count;
        public int HoldingNoteCount => _holdingNotes.Count;
        public int UncompletedHoldStartNoteCount => _uncompletedHoldStartNoteIds.Count;

        public RecoveredStandardHoldNoteManager(
            IReadOnlyList<RecoveredNotationNote> notation)
        {
            if (notation == null) throw new ArgumentNullException(nameof(notation));
            foreach (var note in notation)
            {
                switch ((RecoveredNoteType)note.NoteType)
                {
                    case RecoveredNoteType.Hold:
                    case RecoveredNoteType.CriticalHold:
                        _holdNotes.Add(note);
                        break;
                    case RecoveredNoteType.Sound:
                    case RecoveredNoteType.SoundPurple:
                    case RecoveredNoteType.HoldEighth:
                        _holdingNotes.Add(note);
                        break;
                    case RecoveredNoteType.HoldStart:
                    case RecoveredNoteType.CriticalHoldStart:
                        _holdStartNotes.Add(note);
                        _uncompletedHoldStartNoteIds.Add(note.Id);
                        break;
                }
            }
        }

        public bool TryGetHoldForMain(
            in RecoveredHitLaneEntity hitLane,
            long inputMusicMilliseconds,
            out RecoveredNotationNote note,
            out int laneId)
        {
            laneId = hitLane.HitMainLaneId;
            note = FindActiveHold(laneId, inputMusicMilliseconds);
            return note != null;
        }

        public bool TryGetActiveHoldForMain(
            in RecoveredHitLaneEntity hitLane,
            long inputMusicMilliseconds,
            out RecoveredNotationNote note,
            out int laneId)
        {
            laneId = hitLane.HitMainLaneId;
            note = FindActiveHoldForAssignment(laneId, inputMusicMilliseconds);
            return note != null;
        }

        public bool TryGetActiveHoldForSub(
            in RecoveredHitLaneEntity hitLane,
            long inputMusicMilliseconds,
            out RecoveredNotationNote note,
            out int laneId)
        {
            if (TryFindHold(
                    hitLane.HitSubLeftInnerLaneId, inputMusicMilliseconds,
                    true, out note, out laneId) ||
                TryFindHold(
                    hitLane.HitSubRightInnerLaneId, inputMusicMilliseconds,
                    true, out note, out laneId) ||
                TryFindHold(
                    hitLane.HitSubLeftOuterLaneId, inputMusicMilliseconds,
                    true, out note, out laneId) ||
                TryFindHold(
                    hitLane.HitSubRightOuterLaneId, inputMusicMilliseconds,
                    true, out note, out laneId))
                return true;
            note = null;
            laneId = 0;
            return false;
        }

        public bool ContainsHold(RecoveredNotationNote note) =>
            note != null && _holdNotes.Contains(note);

        public bool TryGetIncludedLane(
            in RecoveredHitLaneEntity hitLane,
            RecoveredNotationNote note,
            out int laneId)
        {
            if (note != null &&
                (TryIncludedLane(hitLane.HitMainLaneId, note, out laneId) ||
                 TryIncludedLane(hitLane.HitSubLeftInnerLaneId, note, out laneId) ||
                 TryIncludedLane(hitLane.HitSubRightInnerLaneId, note, out laneId) ||
                 TryIncludedLane(hitLane.HitSubLeftOuterLaneId, note, out laneId) ||
                 TryIncludedLane(hitLane.HitSubRightOuterLaneId, note, out laneId)))
            {
                return true;
            }
            laneId = 0;
            return false;
        }

        public bool TryGetHoldForSub(
            in RecoveredHitLaneEntity hitLane,
            long inputMusicMilliseconds,
            out RecoveredNotationNote note,
            out int laneId)
        {
            if (TryFindHold(
                    hitLane.HitSubLeftInnerLaneId, inputMusicMilliseconds,
                    false, out note, out laneId) ||
                TryFindHold(
                    hitLane.HitSubRightInnerLaneId, inputMusicMilliseconds,
                    false, out note, out laneId) ||
                TryFindHold(
                    hitLane.HitSubLeftOuterLaneId, inputMusicMilliseconds,
                    false, out note, out laneId) ||
                TryFindHold(
                    hitLane.HitSubRightOuterLaneId, inputMusicMilliseconds,
                    false, out note, out laneId))
                return true;
            note = null;
            laneId = 0;
            return false;
        }

        public void GetHoldingNotationNotes(
            List<RecoveredNotationNote> output,
            in RecoveredHitLaneEntity hitLane,
            long inputMusicMilliseconds)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            foreach (var note in _holdingNotes)
            {
                if (note.StartMilliseconds > inputMusicMilliseconds) break;
                if (note.StartMilliseconds <= inputMusicMilliseconds &&
                    IsIncludedInAnyHitLane(hitLane, note))
                {
                    output.Add(note);
                }
            }
        }

        private bool TryFindHold(
            int candidateLaneId,
            long inputMusicMilliseconds,
            bool forAssignment,
            out RecoveredNotationNote note,
            out int laneId)
        {
            note = forAssignment
                ? FindActiveHoldForAssignment(
                    candidateLaneId, inputMusicMilliseconds)
                : FindActiveHold(candidateLaneId, inputMusicMilliseconds);
            laneId = note != null ? candidateLaneId : 0;
            return note != null;
        }

        public RecoveredNotationNote GetHoldStartNote(RecoveredNotationNote hold)
        {
            if (hold == null) throw new ArgumentNullException(nameof(hold));
            // The original method returns immediately unless the body is type 100.
            if (hold.NoteType != (int)RecoveredNoteType.Hold) return null;
            foreach (var start in _holdStartNotes)
            {
                if ((start.NoteType & ~1) == (int)RecoveredNoteType.HoldStart &&
                    start.Lane == hold.Lane &&
                    start.EndLane == hold.EndLane &&
                    start.StartMilliseconds == hold.StartMilliseconds)
                {
                    return start;
                }
            }
            return null;
        }

        public bool IsHoldStartNoteNotCompleted(RecoveredNotationNote start)
        {
            return start != null && _uncompletedHoldStartNoteIds.Contains(start.Id);
        }

        public void CompletedHoldStartNote(RecoveredNotationNote start)
        {
            if (start != null) _uncompletedHoldStartNoteIds.Remove(start.Id);
        }

        public bool DeleteHoldNote(RecoveredNotationNote note) =>
            note != null && _holdNotes.Remove(note);

        public bool DeleteHoldingNote(RecoveredNotationNote note) =>
            note != null && _holdingNotes.Remove(note);

        public int DeleteHoldingNotes(
            in RecoveredHitLaneEntity hitLane,
            long inputMusicMilliseconds,
            List<RecoveredNotationNote> buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            buffer.Clear();
            GetHoldingNotationNotes(buffer, hitLane, inputMusicMilliseconds);
            var deleted = 0;
            foreach (var holding in buffer)
            {
                if (DeleteHoldingNote(holding)) deleted++;
            }
            buffer.Clear();
            return deleted;
        }

        private RecoveredNotationNote FindActiveHold(
            int laneId,
            long inputMusicMilliseconds)
        {
            if (laneId == 0) return null;
            foreach (var note in _holdNotes)
            {
                // A held finger produces Stationary every frame.  The original
                // Hold provider does not expose the body as an end candidate
                // before its end time; the timing decider is applied only after
                // that provider selection.  Using the GOOD-window lower bound
                // here consumes every autoplay Hold about 125 ms too early.
                if (inputMusicMilliseconds >= note.EndMilliseconds &&
                    IsIncludedLane(laneId, note) &&
                    RecoveredHoldTimingDecider.DecideEndMusicTime(
                        inputMusicMilliseconds,
                        note.StartMilliseconds,
                        note.EndMilliseconds).TimingType != RecoveredTimingType.None)
                {
                    return note;
                }
            }
            return null;
        }

        private RecoveredNotationNote FindActiveHoldForAssignment(
            int laneId,
            long inputMusicMilliseconds)
        {
            if (laneId == 0) return null;
            RecoveredNotationNote best = null;
            foreach (var note in _holdNotes)
            {
                if (note.StartMilliseconds > inputMusicMilliseconds ||
                    inputMusicMilliseconds >= note.EndMilliseconds ||
                    !IsIncludedLane(laneId, note))
                {
                    continue;
                }
                if (best == null || note.EndMilliseconds < best.EndMilliseconds)
                    best = note;
            }
            return best;
        }

        private static bool TryIncludedLane(
            int laneId,
            RecoveredNotationNote note,
            out int includedLaneId)
        {
            if (IsIncludedLane(laneId, note))
            {
                includedLaneId = laneId;
                return true;
            }
            includedLaneId = 0;
            return false;
        }

        private static bool IsIncludedInAnyHitLane(
            in RecoveredHitLaneEntity hitLane,
            RecoveredNotationNote note)
        {
            return IsIncludedLane(hitLane.HitMainLaneId, note) ||
                   IsIncludedLane(hitLane.HitSubLeftInnerLaneId, note) ||
                   IsIncludedLane(hitLane.HitSubRightInnerLaneId, note) ||
                   IsIncludedLane(hitLane.HitSubLeftOuterLaneId, note) ||
                   IsIncludedLane(hitLane.HitSubRightOuterLaneId, note);
        }

        private static bool IsIncludedLane(int laneId, RecoveredNotationNote note)
        {
            return laneId != 0 && note.Lane <= laneId && laneId <= note.EndLane;
        }
    }

    /// <summary>
    /// Ordinary Hold branch of InputAction.TryHold. Scratch and Jump-Scratch are
    /// separate original branches and intentionally remain outside this class.
    /// </summary>
    public sealed class RecoveredStandardHoldAction
    {
        private readonly RecoveredStandardHoldNoteManager _noteManager;
        private readonly List<RecoveredNotationNote> _tempHoldingNotes =
            new List<RecoveredNotationNote>();

        public RecoveredStandardHoldAction(RecoveredStandardHoldNoteManager noteManager)
        {
            _noteManager = noteManager ?? throw new ArgumentNullException(nameof(noteManager));
        }

        public RecoveredHoldActionResult TryHold(
            in RecoveredInputEntity input,
            in RecoveredHitLaneEntity hitLane,
            long inputMusicMilliseconds,
            RecoveredNotationNote currentHold = null)
        {
            if (currentHold != null &&
                _noteManager.ContainsHold(currentHold) &&
                _noteManager.TryGetIncludedLane(
                    hitLane, currentHold, out var currentLaneId))
            {
                // Stationary/Moved keeps ownership until the endpoint, but
                // OnHoldEnd runs the HoldTimingDecider when the finger is
                // released. This is the original early-release grace window:
                // long Holds accept GOOD from End-125 ms, GREAT from End-100 ms
                // and PERFECT_STAR from End-60 ms.
                var isEnded =
                    input.Phase == RecoveredTouchPhase.Ended;
                if (!isEnded &&
                    inputMusicMilliseconds < currentHold.EndMilliseconds)
                {
                    return RecoveredHoldActionResult.OnAssigned(
                        currentLaneId, currentHold);
                }
                var currentTiming = RecoveredHoldTimingDecider.DecideEndMusicTime(
                    inputMusicMilliseconds,
                    currentHold.StartMilliseconds,
                    currentHold.EndMilliseconds);
                if (currentTiming.TimingType == RecoveredTimingType.None)
                {
                    return isEnded
                        ? RecoveredHoldActionResult.OnRelease()
                        : RecoveredHoldActionResult.OnAssigned(
                            currentLaneId, currentHold);
                }
                return CompleteHold(currentHold, currentLaneId, currentTiming,
                    hitLane, inputMusicMilliseconds);
            }
            if (currentHold != null && _noteManager.ContainsHold(currentHold))
                return RecoveredHoldActionResult.OnRelease();

            // Hold ownership is established by a continuing touch. Began is
            // first offered to Tap/Flick and may remain unconsumed (notably a
            // Flick's start sample); allowing it to claim any body underneath
            // steals neighbouring Holds late in their duration. A replacement
            // finger becomes Stationary on the following input frame.
            if (input.Phase == RecoveredTouchPhase.Began ||
                RecoveredInputLifecycle.IsEnded(input.Phase))
            {
                return default;
            }

            if (_noteManager.TryGetActiveHoldForMain(
                    hitLane, inputMusicMilliseconds,
                    out var activeHold, out var activeLaneId) ||
                _noteManager.TryGetActiveHoldForSub(
                    hitLane, inputMusicMilliseconds,
                    out activeHold, out activeLaneId))
            {
                // HoldTimingDecider.GetTimingType(..., isHolding:false)
                // establishes ownership from StartMilliseconds onward. It does
                // not delete or publish the body result; the isHolding:true
                // branch judges the tail on a later input.
                return RecoveredHoldActionResult.OnAssigned(
                    activeLaneId, activeHold);
            }

            if (!_noteManager.TryGetHoldForMain(
                    hitLane, inputMusicMilliseconds, out var hold, out var laneId) &&
                !_noteManager.TryGetHoldForSub(
                    hitLane, inputMusicMilliseconds, out hold, out laneId))
            {
                return default;
            }

            var timing = RecoveredHoldTimingDecider.DecideEndMusicTime(
                inputMusicMilliseconds,
                hold.StartMilliseconds,
                hold.EndMilliseconds);
            if (timing.TimingType == RecoveredTimingType.None)
            {
                return default;
            }

            // AutoTouch schedules an ordinary Hold's Ended input at
            // EndMilliseconds + 1. In the original Fire lifecycle that release
            // still travels through OnHoldEnd before FinalizeOnFire drops the
            // touch ownership. Treating every Ended phase as an unconditional
            // TryHoldingCore rejection loses the only tail sample whenever a
            // rendered frame steps over the exact EndMilliseconds. The timing
            // decision above remains the guard for an early manual release.

            return CompleteHold(
                hold, laneId, timing, hitLane, inputMusicMilliseconds);
        }

        private RecoveredHoldActionResult CompleteHold(
            RecoveredNotationNote hold,
            int laneId,
            RecoveredTimingDecision timing,
            in RecoveredHitLaneEntity hitLane,
            long inputMusicMilliseconds)
        {
            var holdStart = _noteManager.GetHoldStartNote(hold);
            _noteManager.CompletedHoldStartNote(holdStart);
            _noteManager.DeleteHoldNote(hold);

            // Holding types 30/31/900 are successful once their start has passed.
            var deletedHoldingNoteCount = _noteManager.DeleteHoldingNotes(
                hitLane, inputMusicMilliseconds, _tempHoldingNotes);
            return new RecoveredHoldActionResult(
                true, true,
                laneId,
                hold,
                holdStart,
                timing,
                deletedHoldingNoteCount);
        }
    }

    /// <summary>
    /// InputAction.TryHold's recovered selector and ScratchHold branch. It first
    /// protects an overdue ScratchHold, otherwise uses main Hold, sub Hold, then
    /// HoldIncludeJumpScratch. A successful 110/111 travels through the same
    /// Scratch gesture/timing core and the same Holding-note cleanup as the game.
    /// </summary>
    public sealed class RecoveredUnifiedHoldAction
    {
        private readonly RecoveredStandardHoldNoteManager _holdNotes;
        private readonly RecoveredScratchNoteManager _scratchNotes;
        private readonly RecoveredStandardHoldAction _standardAction;
        private readonly RecoveredScratchAction _scratchAction;
        private readonly List<RecoveredNotationNote> _tempHoldingNotes =
            new List<RecoveredNotationNote>();
        private readonly List<RecoveredNotationNote> _tempScratchNotes =
            new List<RecoveredNotationNote>();
        private readonly List<RecoveredNotationNote> _holdingResults =
            new List<RecoveredNotationNote>();
        private readonly List<RecoveredScratchActionResult> _scratchResults =
            new List<RecoveredScratchActionResult>();

        public IReadOnlyList<RecoveredNotationNote> HoldingResults => _holdingResults;
        public IReadOnlyList<RecoveredScratchActionResult> ScratchResults =>
            _scratchResults;

        public RecoveredUnifiedHoldAction(
            RecoveredStandardHoldNoteManager holdNotes,
            RecoveredScratchNoteManager scratchNotes)
        {
            _holdNotes = holdNotes ?? throw new ArgumentNullException(nameof(holdNotes));
            _scratchNotes = scratchNotes ??
                            throw new ArgumentNullException(nameof(scratchNotes));
            _standardAction = new RecoveredStandardHoldAction(_holdNotes);
            _scratchAction = new RecoveredScratchAction(_scratchNotes);
        }

        public RecoveredHoldActionResult TryHold(
            in RecoveredInputEntity input,
            in RecoveredHitLaneEntity hitLane,
            long inputMusicMilliseconds,
            long? currentMusicMilliseconds = null,
            RecoveredNotationNote currentHold = null)
        {
            _scratchResults.Clear();
            // TryHoldingCore calls IClock.get_PassedMilliseconds, not
            // InputEntity.Milliseconds, for the sustained pulse decision.
            ConsumeDueHoldingNotes(input, hitLane,
                currentMusicMilliseconds ?? inputMusicMilliseconds);
            var consumedDirectScratch = ConsumeDirectScratchNotes(
                input, hitLane, inputMusicMilliseconds, currentMusicMilliseconds);
            if (currentHold != null &&
                (currentHold.NoteType ==
                    (int)RecoveredNoteType.ScratchHold ||
                 currentHold.NoteType ==
                    (int)RecoveredNoteType.ScratchCriticalHold) &&
                _scratchNotes.Contains(currentHold))
            {
                if (_scratchNotes.TryGetIncludedLane(
                        hitLane, currentHold, out var currentScratchLane))
                {
                    var currentResult = TryScratchHold(
                        input, hitLane, currentHold, currentScratchLane,
                        inputMusicMilliseconds, currentMusicMilliseconds);
                    return currentResult.Consumed
                        ? currentResult
                        : RecoveredHoldActionResult.OnAssigned(
                            currentScratchLane, currentHold);
                }
                // A connected Jump-Scratch changes the owned body at the
                // previous segment's tail. Stationary retains that ownership;
                // only a new Moved sample can prove that the finger left the
                // connected range. This also prevents sub-collider changes from
                // stealing a parallel ScratchHold while no movement occurred.
                if (input.Phase == RecoveredTouchPhase.Stationary)
                {
                    return RecoveredHoldActionResult.OnAssigned(
                        currentHold.Lane, currentHold);
                }
                return RecoveredHoldActionResult.OnRelease();
            }
            if (currentHold != null &&
                (currentHold.NoteType == (int)RecoveredNoteType.Hold ||
                 currentHold.NoteType == (int)RecoveredNoteType.CriticalHold))
            {
                return _standardAction.TryHold(
                    input, hitLane, inputMusicMilliseconds, currentHold);
            }

            // TryHold's tuple marks a successful standalone type-40 Scratch as
            // a one-shot input. It must not fall through and acquire an
            // overlapping Hold/ScratchHold body.
            if (consumedDirectScratch)
                return default;

            var hasScratch = _scratchNotes.TryGetHoldIncludeJumpScratch(
                hitLane, inputMusicMilliseconds, out var scratch, out var scratchLane);

            // TryHold compares the pre-fetched ScratchHold EndMilliseconds with
            // the current music time before querying ordinary main/sub Holds.
            if (hasScratch && scratch.EndMilliseconds <= inputMusicMilliseconds)
            {
                return TryScratchHold(
                    input, hitLane, scratch, scratchLane, inputMusicMilliseconds,
                    currentMusicMilliseconds);
            }

            if (_holdNotes.TryGetHoldForMain(
                    hitLane, inputMusicMilliseconds, out _, out _) ||
                _holdNotes.TryGetHoldForSub(
                    hitLane, inputMusicMilliseconds, out _, out _))
            {
                return _standardAction.TryHold(
                    input, hitLane, inputMusicMilliseconds, null);
            }

            if (hasScratch)
            {
                var scratchResult = TryScratchHold(
                    input, hitLane, scratch, scratchLane, inputMusicMilliseconds,
                    currentMusicMilliseconds);
                if (scratchResult.Consumed) return scratchResult;
            }

            if (input.Phase != RecoveredTouchPhase.Began &&
                !RecoveredInputLifecycle.IsEnded(input.Phase) &&
                _scratchNotes.TryGetActiveHoldForAssignment(
                    hitLane, inputMusicMilliseconds,
                    out var activeScratch, out var activeScratchLane))
            {
                return RecoveredHoldActionResult.OnAssigned(
                    activeScratchLane, activeScratch);
            }
            return _standardAction.TryHold(
                input, hitLane, inputMusicMilliseconds, currentHold);
        }

        private bool ConsumeDirectScratchNotes(
            in RecoveredInputEntity input,
            in RecoveredHitLaneEntity hitLane,
            long inputMusicMilliseconds,
            long? currentMusicMilliseconds)
        {
            // InputAction.TryHold has no separate public TryScratch entry. Its
            // per-input scratch collection feeds type 40 to TryScratchCore even
            // when no Hold body completes in this frame. Restrict this pass to
            // direct Scratch notes; 110/111 still own the returned Hold result
            // and LaneHold lifecycle below.
            _tempScratchNotes.Clear();
            _scratchNotes.GetScratchNotationNotes(
                _tempScratchNotes, hitLane, inputMusicMilliseconds);
            var consumed = false;
            foreach (var candidate in _tempScratchNotes)
            {
                if (candidate.NoteType != (int)RecoveredNoteType.Scratch ||
                    !_scratchNotes.TryGetIncludedLane(
                        hitLane, candidate, out var candidateLane))
                {
                    continue;
                }
                var candidateResult = _scratchAction.TryScratch(
                    input,
                    candidate,
                    candidateLane,
                    inputMusicMilliseconds,
                    false,
                    currentMusicMilliseconds);
                if (candidateResult.Consumed)
                {
                    _scratchResults.Add(candidateResult);
                    consumed = true;
                }
            }
            return consumed;
        }

        private void ConsumeDueHoldingNotes(
            in RecoveredInputEntity input,
            in RecoveredHitLaneEntity hitLane,
            long inputMusicMilliseconds)
        {
            _holdingResults.Clear();
            if (RecoveredInputLifecycle.IsEnded(input.Phase)) return;

            // InputAction.TryHold first asks NotationNoteManager for every
            // Holding note covered by this HitLane and runs TryHoldingCore on
            // each one. Types 30/31/900 become PERFECT_STAR as soon as their
            // StartMilliseconds is reached; they are not deferred until the
            // enclosing type-100/110 Hold body ends.
            _holdNotes.GetHoldingNotationNotes(
                _holdingResults, hitLane, inputMusicMilliseconds);
            for (var index = _holdingResults.Count - 1; index >= 0; index--)
            {
                if (!_holdNotes.DeleteHoldingNote(_holdingResults[index]))
                    _holdingResults.RemoveAt(index);
            }
        }

        private RecoveredHoldActionResult TryScratchHold(
            in RecoveredInputEntity input,
            in RecoveredHitLaneEntity hitLane,
            RecoveredNotationNote scratch,
            int laneId,
            long inputMusicMilliseconds,
            long? currentMusicMilliseconds)
        {
            var result = _scratchAction.TryScratch(
                input,
                scratch,
                laneId,
                inputMusicMilliseconds,
                RecoveredJumpScratch.IsJumpScratch(scratch),
                currentMusicMilliseconds);
            if (!result.Consumed) return default;

            var deletedHolding = _holdNotes.DeleteHoldingNotes(
                hitLane, inputMusicMilliseconds, _tempHoldingNotes);

            // After the selected Hold succeeds, TryHold appends all Scratch
            // candidates covered by the same HitLane and runs the same input
            // through each. This is what lets one Jump-Scratch Moved event
            // finish the overdue segment and the segment ending now.
            _tempScratchNotes.Clear();
            _scratchNotes.GetScratchNotationNotes(
                _tempScratchNotes, hitLane, inputMusicMilliseconds);
            var deletedScratch = 0;
            foreach (var candidate in _tempScratchNotes)
            {
                if (!_scratchNotes.TryGetIncludedLane(
                        hitLane, candidate, out var candidateLane))
                {
                    continue;
                }
                var candidateResult = _scratchAction.TryScratch(
                        input,
                        candidate,
                        candidateLane,
                        inputMusicMilliseconds,
                        RecoveredJumpScratch.IsJumpScratch(candidate),
                        currentMusicMilliseconds);
                if (candidateResult.Consumed)
                {
                    // Original TryScratchCore publishes an InputResult for
                    // every candidate it consumes, including the extra
                    // Scratch notes collected after the selected Hold body.
                    _scratchResults.Add(candidateResult);
                    deletedScratch++;
                }
            }
            _scratchNotes.TryGetConnectedNext(scratch, out var nextScratch);
            return new RecoveredHoldActionResult(
                true, true,
                laneId,
                scratch,
                null,
                result.Timing,
                deletedHolding,
                deletedScratch,
                nextScratch);
        }
    }

    /// <summary>
    /// TouchId-to-hold occupancy and deferred effect publication recovered from
    /// LaneHoldManager. A note emits one start/end pair even when several touches
    /// reference it concurrently.
    /// </summary>
    public sealed class RecoveredLaneHoldManager
    {
        private readonly Dictionary<int, RecoveredNotationNote> _history =
            new Dictionary<int, RecoveredNotationNote>(1000);
        private readonly List<RecoveredInputEffectEntity> _queue =
            new List<RecoveredInputEffectEntity>(1000);
        private readonly List<int> _tempTouchIds = new List<int>(1000);

        public int Count => _history.Count;
        public int QueuedEventCount => _queue.Count;
        public IReadOnlyDictionary<int, RecoveredNotationNote> ActiveAssignments =>
            _history;

        public bool TryGet(
            int touchId,
            out RecoveredNotationNote notationNote) =>
            _history.TryGetValue(touchId, out notationNote);

        public void Set(int touchId, RecoveredNotationNote newNotationNote)
        {
            if (newNotationNote == null)
                throw new ArgumentNullException(nameof(newNotationNote));

            if (_history.TryGetValue(touchId, out var previousNotationNote) &&
                previousNotationNote.Id != newNotationNote.Id &&
                previousNotationNote.NoteType != (int)RecoveredNoteType.Flick &&
                !Exists(previousNotationNote, touchId))
            {
                _queue.Add(RecoveredInputEffectEntity.OnHoldEnd(previousNotationNote));
            }

            var changed = previousNotationNote == null ||
                          previousNotationNote.Id != newNotationNote.Id;
            if (changed &&
                newNotationNote.NoteType != (int)RecoveredNoteType.Flick &&
                !Exists(newNotationNote, touchId))
            {
                _queue.Add(RecoveredInputEffectEntity.OnHoldStart(newNotationNote));
            }
            _history[touchId] = newNotationNote;
        }

        public void Remove(int touchId)
        {
            if (!_history.TryGetValue(touchId, out var notationNote))
            {
                return;
            }
            _history.Remove(touchId);
            if (notationNote.NoteType != (int)RecoveredNoteType.Flick &&
                !Exists(notationNote, touchId))
            {
                _queue.Add(RecoveredInputEffectEntity.OnHoldEnd(notationNote));
            }
        }

        public void RemoveAll()
        {
            _tempTouchIds.Clear();
            _tempTouchIds.AddRange(_history.Keys);
            foreach (var touchId in _tempTouchIds)
            {
                Remove(touchId);
            }
            _tempTouchIds.Clear();
        }

        public bool Exists(RecoveredNotationNote entity, int touchId)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            foreach (var pair in _history)
            {
                if (pair.Key != touchId && pair.Value.Id == entity.Id)
                {
                    return true;
                }
            }
            return false;
        }

        public void GetHoldEvents(List<RecoveredInputEffectEntity> list)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            list.AddRange(_queue);
            _queue.Clear();
        }

        // The original always clears pending events; history is cleared only when forced.
        public void Reset(bool isForced = false)
        {
            _queue.Clear();
            if (isForced)
            {
                _history.Clear();
            }
        }
    }
}
