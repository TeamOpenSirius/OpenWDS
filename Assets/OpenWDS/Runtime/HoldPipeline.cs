using System;
using System.Collections.Generic;

namespace OpenWDS.Runtime
{
    public enum InputEffectType
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
    public readonly struct InputEffectEntity :
        IEquatable<InputEffectEntity>
    {
        public readonly int NoteId;
        public readonly long StartMilliseconds;
        public readonly int LaneId;
        public readonly int Width;
        public readonly NoteType NoteType;
        public readonly TimingType TimingType;
        public readonly InputEffectType EffectType;

        private InputEffectEntity(
            int noteId,
            long startMilliseconds,
            int laneId,
            int width,
            NoteType noteType,
            TimingType timingType,
            InputEffectType effectType)
        {
            NoteId = noteId;
            StartMilliseconds = startMilliseconds;
            LaneId = laneId;
            Width = width;
            NoteType = noteType;
            TimingType = timingType;
            EffectType = effectType;
        }

        public static InputEffectEntity OnBeam(
            NotationNote note,
            TimingType timingType)
        {
            if (note == null) throw new ArgumentNullException(nameof(note));
            return new InputEffectEntity(
                note.Id,
                note.StartMilliseconds,
                note.Lane,
                note.Width,
                (NoteType)note.NoteType,
                timingType,
                InputEffectType.Beam);
        }

        public static InputEffectEntity OnBomb(
            NotationNote note,
            TimingType timingType)
        {
            if (note == null) throw new ArgumentNullException(nameof(note));
            return new InputEffectEntity(
                note.Id,
                note.StartMilliseconds,
                note.Lane,
                note.Width,
                (NoteType)note.NoteType,
                timingType,
                InputEffectType.Bomb);
        }

        public static InputEffectEntity OnHoldStart(
            NotationNote note)
        {
            if (note == null) throw new ArgumentNullException(nameof(note));
            return new InputEffectEntity(
                note.Id,
                note.StartMilliseconds,
                note.Lane,
                note.Width,
                (NoteType)note.NoteType,
                TimingType.None,
                InputEffectType.HoldStart);
        }

        public static InputEffectEntity OnHoldEnd(
            NotationNote note)
        {
            if (note == null) throw new ArgumentNullException(nameof(note));
            return new InputEffectEntity(
                note.Id,
                note.StartMilliseconds,
                note.Lane,
                note.Width,
                (NoteType)note.NoteType,
                TimingType.None,
                InputEffectType.HoldEnd);
        }

        public bool Equals(InputEffectEntity other) => NoteId == other.NoteId;
        public override bool Equals(object obj) =>
            obj is InputEffectEntity other && Equals(other);
        public override int GetHashCode() => NoteId;
    }

    /// <summary>
    /// Hold and short-Hold-end windows recovered from GameTimingEntityFactory and
    /// HoldTimingDecider. Every interval is lower-inclusive and upper-exclusive.
    /// </summary>
    public static class HoldTimingDecider
    {
        public const long HoldEndThresholdMilliseconds = 150;
        public const long ApproximateInfiniteUpperBoundMilliseconds = 86400000;

        public static TimingDecision DecideEndMusicTime(
            long inputMusicMilliseconds,
            long holdStartMusicMilliseconds,
            long holdEndMusicMilliseconds)
        {
            var diff = inputMusicMilliseconds - holdEndMusicMilliseconds;
            var duration = holdEndMusicMilliseconds - holdStartMusicMilliseconds;
            var timing = duration < HoldEndThresholdMilliseconds
                ? GetShortHoldEndTiming(diff)
                : GetHoldTiming(diff);
            return timing == TimingType.None
                ? new TimingDecision(
                    TimingType.None,
                    TimingAssistType.None,
                    0)
                : new TimingDecision(
                    timing,
                    TimingAssistType.None,
                    diff);
        }

        public static TimingDecision DecideEnd(
            GameClock clock,
            long inputGameMilliseconds,
            NotationNote hold)
        {
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            if (hold == null) throw new ArgumentNullException(nameof(hold));
            return DecideEndMusicTime(
                clock.InputTimeToMusicMilliseconds(inputGameMilliseconds),
                hold.StartMilliseconds,
                hold.EndMilliseconds);
        }

        private static TimingType GetHoldTiming(long diff)
        {
            if (InRange(diff, -40, ApproximateInfiniteUpperBoundMilliseconds))
                return TimingType.PerfectStar;
            if (InRange(diff, -60, -40)) return TimingType.PerfectStar;
            if (InRange(diff, -100, -60)) return TimingType.Great;
            if (InRange(diff, -125, -100)) return TimingType.Good;
            return TimingType.None;
        }

        private static TimingType GetShortHoldEndTiming(long diff)
        {
            if (InRange(diff, -40, ApproximateInfiniteUpperBoundMilliseconds))
                return TimingType.PerfectStar;
            if (InRange(diff, -75, -40)) return TimingType.PerfectStar;
            return TimingType.None;
        }

        private static bool InRange(long value, long from, long to) =>
            from <= value && value < to;
    }

    public readonly struct HoldActionResult
    {
        public readonly bool Assigned;
        public readonly bool ReleaseCurrent;
        public readonly bool Consumed;
        public readonly int LaneId;
        public readonly NotationNote HoldNote;
        public readonly NotationNote AssignmentNote;
        public readonly NotationNote HoldStartNote;
        public readonly TimingDecision Timing;
        public readonly int DeletedHoldingNoteCount;
        public readonly int DeletedScratchNoteCount;

        public HoldActionResult(
            bool assigned,
            bool consumed,
            int laneId,
            NotationNote holdNote,
            NotationNote holdStartNote,
            TimingDecision timing,
            int deletedHoldingNoteCount,
            int deletedScratchNoteCount = 0,
            NotationNote assignmentNote = null,
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

        public static HoldActionResult OnRelease()
        {
            return new HoldActionResult(
                false, false, 0, null, null, default, 0,
                releaseCurrent: true);
        }

        public static HoldActionResult OnAssigned(
            int laneId,
            NotationNote holdNote)
        {
            return new HoldActionResult(
                true, false, laneId, holdNote, null, default, 0);
        }
    }

    /// <summary>
    /// Standard-chart subset of NotationNoteManager's Hold providers. The queue
    /// type filters, main/sub precedence, inclusive lane coverage and HoldStart
    /// pairing are recovered from the original provider and manager methods.
    /// Scratch/Jump-Scratch branches are deliberately not approximated here.
    /// </summary>
    public sealed class StandardHoldNoteManager
    {
        private readonly List<NotationNote> _holdNotes =
            new List<NotationNote>();
        private readonly List<NotationNote> _holdingNotes =
            new List<NotationNote>();
        private readonly List<NotationNote> _holdStartNotes =
            new List<NotationNote>();
        private readonly HashSet<int> _uncompletedHoldStartNoteIds =
            new HashSet<int>();

        public int HoldNoteCount => _holdNotes.Count;
        public int HoldingNoteCount => _holdingNotes.Count;
        public int UncompletedHoldStartNoteCount => _uncompletedHoldStartNoteIds.Count;

        public StandardHoldNoteManager(
            IReadOnlyList<NotationNote> notation)
        {
            if (notation == null) throw new ArgumentNullException(nameof(notation));
            foreach (var note in notation)
            {
                switch ((NoteType)note.NoteType)
                {
                    case NoteType.Hold:
                    case NoteType.CriticalHold:
                        _holdNotes.Add(note);
                        break;
                    case NoteType.Sound:
                    case NoteType.SoundPurple:
                    case NoteType.HoldEighth:
                        _holdingNotes.Add(note);
                        break;
                    case NoteType.HoldStart:
                    case NoteType.CriticalHoldStart:
                        _holdStartNotes.Add(note);
                        _uncompletedHoldStartNoteIds.Add(note.Id);
                        break;
                }
            }
        }

        public bool TryGetHoldForMain(
            in HitLaneEntity hitLane,
            long inputMusicMilliseconds,
            out NotationNote note,
            out int laneId)
        {
            laneId = hitLane.HitMainLaneId;
            note = FindActiveHold(laneId, inputMusicMilliseconds);
            return note != null;
        }

        public bool TryGetActiveHoldForMain(
            in HitLaneEntity hitLane,
            long inputMusicMilliseconds,
            out NotationNote note,
            out int laneId)
        {
            laneId = hitLane.HitMainLaneId;
            note = FindActiveHoldForAssignment(laneId, inputMusicMilliseconds);
            return note != null;
        }

        public bool TryGetActiveHoldForSub(
            in HitLaneEntity hitLane,
            long inputMusicMilliseconds,
            out NotationNote note,
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

        public bool ContainsHold(NotationNote note) =>
            note != null && _holdNotes.Contains(note);

        public bool TryGetIncludedLane(
            in HitLaneEntity hitLane,
            NotationNote note,
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
            in HitLaneEntity hitLane,
            long inputMusicMilliseconds,
            out NotationNote note,
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
            List<NotationNote> output,
            in HitLaneEntity hitLane,
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
            out NotationNote note,
            out int laneId)
        {
            note = forAssignment
                ? FindActiveHoldForAssignment(
                    candidateLaneId, inputMusicMilliseconds)
                : FindActiveHold(candidateLaneId, inputMusicMilliseconds);
            laneId = note != null ? candidateLaneId : 0;
            return note != null;
        }

        public NotationNote GetHoldStartNote(NotationNote hold)
        {
            if (hold == null) throw new ArgumentNullException(nameof(hold));
            // The original method returns immediately unless the body is type 100.
            if (hold.NoteType != (int)NoteType.Hold) return null;
            foreach (var start in _holdStartNotes)
            {
                if ((start.NoteType & ~1) == (int)NoteType.HoldStart &&
                    start.Lane == hold.Lane &&
                    start.EndLane == hold.EndLane &&
                    start.StartMilliseconds == hold.StartMilliseconds)
                {
                    return start;
                }
            }
            return null;
        }

        public bool IsHoldStartNoteNotCompleted(NotationNote start)
        {
            return start != null && _uncompletedHoldStartNoteIds.Contains(start.Id);
        }

        public void CompletedHoldStartNote(NotationNote start)
        {
            if (start != null) _uncompletedHoldStartNoteIds.Remove(start.Id);
        }

        public bool DeleteHoldNote(NotationNote note) =>
            note != null && _holdNotes.Remove(note);

        public bool DeleteHoldingNote(NotationNote note) =>
            note != null && _holdingNotes.Remove(note);

        public int DeleteHoldingNotes(
            in HitLaneEntity hitLane,
            long inputMusicMilliseconds,
            List<NotationNote> buffer)
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

        private NotationNote FindActiveHold(
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
                    HoldTimingDecider.DecideEndMusicTime(
                        inputMusicMilliseconds,
                        note.StartMilliseconds,
                        note.EndMilliseconds).TimingType != TimingType.None)
                {
                    return note;
                }
            }
            return null;
        }

        private NotationNote FindActiveHoldForAssignment(
            int laneId,
            long inputMusicMilliseconds)
        {
            if (laneId == 0) return null;
            NotationNote best = null;
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
            NotationNote note,
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
            in HitLaneEntity hitLane,
            NotationNote note)
        {
            return IsIncludedLane(hitLane.HitMainLaneId, note) ||
                   IsIncludedLane(hitLane.HitSubLeftInnerLaneId, note) ||
                   IsIncludedLane(hitLane.HitSubRightInnerLaneId, note) ||
                   IsIncludedLane(hitLane.HitSubLeftOuterLaneId, note) ||
                   IsIncludedLane(hitLane.HitSubRightOuterLaneId, note);
        }

        private static bool IsIncludedLane(int laneId, NotationNote note)
        {
            return laneId != 0 && note.Lane <= laneId && laneId <= note.EndLane;
        }
    }

    /// <summary>
    /// Ordinary Hold branch of InputAction.TryHold. Scratch and Jump-Scratch are
    /// separate original branches and intentionally remain outside this class.
    /// </summary>
    public sealed class StandardHoldAction
    {
        private readonly StandardHoldNoteManager _noteManager;
        private readonly List<NotationNote> _tempHoldingNotes =
            new List<NotationNote>();

        public StandardHoldAction(StandardHoldNoteManager noteManager)
        {
            _noteManager = noteManager ?? throw new ArgumentNullException(nameof(noteManager));
        }

        public HoldActionResult TryHold(
            in InputEntity input,
            in HitLaneEntity hitLane,
            long inputMusicMilliseconds,
            NotationNote currentHold = null,
            bool heldByAnotherTouch = false,
            long? currentMusicMilliseconds = null)
        {
            // Native TryHoldCore (0xB95ABA8) uses IClock for both
            // OutOfRangeTarget and GetTimingType(isHolding:true). Device event
            // time belongs only to OnHoldEnd's release branch.
            var frameMusicMilliseconds = currentMusicMilliseconds ?? inputMusicMilliseconds;
            if (!InputLifecycle.IsEnded(input.Phase) &&
                (_noteManager.TryGetHoldForMain(hitLane, frameMusicMilliseconds,
                     out var dueHold, out var dueLane) ||
                 _noteManager.TryGetHoldForSub(hitLane, frameMusicMilliseconds,
                     out dueHold, out dueLane)))
                return CompleteHold(dueHold, dueLane,
                    HoldTimingDecider.DecideEndMusicTime(frameMusicMilliseconds,
                        dueHold.StartMilliseconds, dueHold.EndMilliseconds),
                    hitLane, frameMusicMilliseconds);

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
                    input.Phase == TouchPhase.Ended;
                // OnHoldEnd uses OutOfRangeTarget when another touch still
                // holds this body; only the last contact gets early-release timing.
                // That eligibility check reads IClock, not the event timestamp.
                if (isEnded && heldByAnotherTouch &&
                    (currentMusicMilliseconds ?? inputMusicMilliseconds) < currentHold.EndMilliseconds)
                    return HoldActionResult.OnRelease();
                if (!isEnded &&
                    frameMusicMilliseconds < currentHold.EndMilliseconds)
                {
                    return HoldActionResult.OnAssigned(
                        currentLaneId, currentHold);
                }
                if (input.Phase == TouchPhase.Canceled) return HoldActionResult.OnRelease();
                var judgementMilliseconds = isEnded ? inputMusicMilliseconds : frameMusicMilliseconds;
                var currentTiming = HoldTimingDecider.DecideEndMusicTime(
                    judgementMilliseconds,
                    currentHold.StartMilliseconds,
                    currentHold.EndMilliseconds);
                if (currentTiming.TimingType == TimingType.None)
                {
                    return isEnded
                        ? HoldActionResult.OnRelease()
                        : HoldActionResult.OnAssigned(
                            currentLaneId, currentHold);
                }
                return CompleteHold(currentHold, currentLaneId, currentTiming,
                    hitLane, judgementMilliseconds);
            }
            if (currentHold != null && _noteManager.ContainsHold(currentHold))
                return HoldActionResult.OnRelease();

            // Hold ownership is established by a continuing touch. Began is
            // first offered to Tap/Flick and may remain unconsumed (notably a
            // Flick's start sample); allowing it to claim any body underneath
            // steals neighbouring Holds late in their duration. A replacement
            // finger becomes Stationary on the following input frame.
            if (input.Phase == TouchPhase.Began ||
                InputLifecycle.IsEnded(input.Phase))
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
                return HoldActionResult.OnAssigned(
                    activeLaneId, activeHold);
            }

            if (!_noteManager.TryGetHoldForMain(
                    hitLane, inputMusicMilliseconds, out var hold, out var laneId) &&
                !_noteManager.TryGetHoldForSub(
                    hitLane, inputMusicMilliseconds, out hold, out laneId))
            {
                return default;
            }

            var timing = HoldTimingDecider.DecideEndMusicTime(
                inputMusicMilliseconds,
                hold.StartMilliseconds,
                hold.EndMilliseconds);
            if (timing.TimingType == TimingType.None)
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

        private HoldActionResult CompleteHold(
            NotationNote hold,
            int laneId,
            TimingDecision timing,
            in HitLaneEntity hitLane,
            long inputMusicMilliseconds)
        {
            var holdStart = _noteManager.GetHoldStartNote(hold);
            _noteManager.CompletedHoldStartNote(holdStart);
            _noteManager.DeleteHoldNote(hold);

            // Holding types 30/31/900 are successful once their start has passed.
            var deletedHoldingNoteCount = _noteManager.DeleteHoldingNotes(
                hitLane, inputMusicMilliseconds, _tempHoldingNotes);
            return new HoldActionResult(
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
    public sealed class UnifiedHoldAction
    {
        private readonly StandardHoldNoteManager _holdNotes;
        private readonly ScratchNoteManager _scratchNotes;
        private readonly StandardHoldAction _standardAction;
        private readonly ScratchAction _scratchAction;
        private readonly List<NotationNote> _tempHoldingNotes =
            new List<NotationNote>();
        private readonly List<NotationNote> _tempScratchNotes =
            new List<NotationNote>();
        private readonly List<NotationNote> _holdingResults =
            new List<NotationNote>();
        private readonly List<ScratchActionResult> _scratchResults =
            new List<ScratchActionResult>();

        public IReadOnlyList<NotationNote> HoldingResults => _holdingResults;
        public IReadOnlyList<ScratchActionResult> ScratchResults =>
            _scratchResults;

        public UnifiedHoldAction(
            StandardHoldNoteManager holdNotes,
            ScratchNoteManager scratchNotes)
        {
            _holdNotes = holdNotes ?? throw new ArgumentNullException(nameof(holdNotes));
            _scratchNotes = scratchNotes ??
                            throw new ArgumentNullException(nameof(scratchNotes));
            _standardAction = new StandardHoldAction(_holdNotes);
            _scratchAction = new ScratchAction(_scratchNotes);
        }

        public HoldActionResult TryHold(
            in InputEntity input,
            in HitLaneEntity hitLane,
            long inputMusicMilliseconds,
            long? currentMusicMilliseconds = null,
            NotationNote currentHold = null,
            bool heldByAnotherTouch = false)
        {
            _scratchResults.Clear();
            // TryHoldingCore calls IClock.get_PassedMilliseconds, not
            // InputEntity.Milliseconds, for the sustained pulse decision.
            ConsumeDueHoldingNotes(input, hitLane,
                currentMusicMilliseconds ?? inputMusicMilliseconds);
            var consumedDirectScratch = ConsumeDirectScratchNotes(
                input, hitLane, inputMusicMilliseconds, currentMusicMilliseconds);
            // Retail TryHold (0xB949A30) checks the due ScratchHold provider
            // before ordinary Holds. Cached visual ownership must not bypass
            // that selection: a preceding short jump can be consumed early,
            // and its finger can temporarily overlap a neighbouring long Hold.
            var hasScratch = _scratchNotes.TryGetHoldIncludeJumpScratch(
                hitLane, inputMusicMilliseconds, out var scratch, out var scratchLane);
            if (hasScratch && scratch.EndMilliseconds <=
                (currentMusicMilliseconds ?? inputMusicMilliseconds))
            {
                return TryScratchHold(
                    input, hitLane, scratch, scratchLane, inputMusicMilliseconds,
                    currentMusicMilliseconds);
            }
            if (!InputLifecycle.IsEnded(input.Phase) &&
                (_holdNotes.TryGetHoldForMain(hitLane, currentMusicMilliseconds ?? inputMusicMilliseconds, out _, out _) ||
                 _holdNotes.TryGetHoldForSub(hitLane, currentMusicMilliseconds ?? inputMusicMilliseconds, out _, out _)))
                return _standardAction.TryHold(input, hitLane, inputMusicMilliseconds,
                    currentMusicMilliseconds: currentMusicMilliseconds);
            // The native Hold provider searches the Main lane across all four
            // body types before any Sub lane. Cached ownership of a blue Hold
            // must not hide a purple body now under Main (and vice versa).
            // Neustart's jump chain crosses a parallel ordinary Hold whose Sub
            // collider still overlaps the destination lane.
            if (input.Phase != TouchPhase.Began && !InputLifecycle.IsEnded(input.Phase) &&
                (currentHold != null || !consumedDirectScratch))
            {
                var now = currentMusicMilliseconds ?? inputMusicMilliseconds;
                _holdNotes.TryGetActiveHoldForMain(hitLane, now, out var mainHold, out _);
                _scratchNotes.TryGetActiveBodyForMain(hitLane, now, out var mainScratch);
                var mainBody = mainHold == null ? mainScratch :
                    mainScratch == null || mainHold.StartMilliseconds <= mainScratch.StartMilliseconds
                        ? mainHold : mainScratch;
                if (mainBody != null && (currentHold == null || mainBody.Id != currentHold.Id))
                    currentHold = mainBody;
            }
            if (currentHold != null &&
                (currentHold.NoteType ==
                    (int)NoteType.ScratchHold ||
                 currentHold.NoteType ==
                    (int)NoteType.ScratchCriticalHold) &&
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
                        : HoldActionResult.OnAssigned(
                            currentScratchLane, currentHold);
                }
                // A connected Jump-Scratch changes the owned body at the
                // previous segment's tail. Stationary retains that ownership;
                // only a new Moved sample can prove that the finger left the
                // connected range. This also prevents sub-collider changes from
                // stealing a parallel ScratchHold while no movement occurred.
                if (input.Phase == TouchPhase.Stationary)
                {
                    return HoldActionResult.OnAssigned(
                        currentHold.Lane, currentHold);
                }
                return HoldActionResult.OnRelease();
            }
            if (currentHold != null &&
                (currentHold.NoteType == (int)NoteType.Hold ||
                 currentHold.NoteType == (int)NoteType.CriticalHold))
            {
                return _standardAction.TryHold(
                    input, hitLane, inputMusicMilliseconds, currentHold, heldByAnotherTouch,
                    currentMusicMilliseconds);
            }

            // TryHold's tuple marks a successful standalone type-40 Scratch as
            // a one-shot input. It must not fall through and acquire an
            // overlapping Hold/ScratchHold body.
            if (consumedDirectScratch)
                return default;

            if (_holdNotes.TryGetHoldForMain(
                    hitLane, inputMusicMilliseconds, out _, out _) ||
                _holdNotes.TryGetHoldForSub(
                    hitLane, inputMusicMilliseconds, out _, out _))
            {
                return _standardAction.TryHold(
                    input, hitLane, inputMusicMilliseconds, null,
                    currentMusicMilliseconds: currentMusicMilliseconds);
            }

            if (hasScratch)
            {
                var scratchResult = TryScratchHold(
                    input, hitLane, scratch, scratchLane, inputMusicMilliseconds,
                    currentMusicMilliseconds);
                if (scratchResult.Consumed) return scratchResult;
            }

            if (input.Phase != TouchPhase.Began &&
                !InputLifecycle.IsEnded(input.Phase) &&
                _scratchNotes.TryGetActiveHoldForAssignment(
                    hitLane, inputMusicMilliseconds,
                    out var activeScratch, out var activeScratchLane))
            {
                return HoldActionResult.OnAssigned(
                    activeScratchLane, activeScratch);
            }
            return _standardAction.TryHold(
                input, hitLane, inputMusicMilliseconds, currentHold, heldByAnotherTouch,
                currentMusicMilliseconds);
        }

        private bool ConsumeDirectScratchNotes(
            in InputEntity input,
            in HitLaneEntity hitLane,
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
                if (candidate.NoteType != (int)NoteType.Scratch ||
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
            in InputEntity input,
            in HitLaneEntity hitLane,
            long inputMusicMilliseconds)
        {
            _holdingResults.Clear();
            if (InputLifecycle.IsEnded(input.Phase)) return;

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

        private HoldActionResult TryScratchHold(
            in InputEntity input,
            in HitLaneEntity hitLane,
            NotationNote scratch,
            int laneId,
            long inputMusicMilliseconds,
            long? currentMusicMilliseconds)
        {
            var result = _scratchAction.TryScratch(
                input,
                scratch,
                laneId,
                inputMusicMilliseconds,
                JumpScratch.IsJumpScratch(scratch),
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
                        JumpScratch.IsJumpScratch(candidate),
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
            return new HoldActionResult(
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
    public sealed class LaneHoldManager
    {
        private readonly Dictionary<int, NotationNote> _history =
            new Dictionary<int, NotationNote>(1000);
        private readonly List<InputEffectEntity> _queue =
            new List<InputEffectEntity>(1000);
        private readonly List<int> _tempTouchIds = new List<int>(1000);

        public int Count => _history.Count;
        public int QueuedEventCount => _queue.Count;
        public IReadOnlyDictionary<int, NotationNote> ActiveAssignments =>
            _history;

        public bool TryGet(
            int touchId,
            out NotationNote notationNote) =>
            _history.TryGetValue(touchId, out notationNote);

        public void Set(int touchId, NotationNote newNotationNote)
        {
            if (newNotationNote == null)
                throw new ArgumentNullException(nameof(newNotationNote));

            if (_history.TryGetValue(touchId, out var previousNotationNote) &&
                previousNotationNote.Id != newNotationNote.Id &&
                previousNotationNote.NoteType != (int)NoteType.Flick &&
                !Exists(previousNotationNote, touchId))
            {
                _queue.Add(InputEffectEntity.OnHoldEnd(previousNotationNote));
            }

            var changed = previousNotationNote == null ||
                          previousNotationNote.Id != newNotationNote.Id;
            if (changed &&
                newNotationNote.NoteType != (int)NoteType.Flick &&
                !Exists(newNotationNote, touchId))
            {
                _queue.Add(InputEffectEntity.OnHoldStart(newNotationNote));
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
            if (notationNote.NoteType != (int)NoteType.Flick &&
                !Exists(notationNote, touchId))
            {
                _queue.Add(InputEffectEntity.OnHoldEnd(notationNote));
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

        public bool Exists(NotationNote entity, int touchId)
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

        public void GetHoldEvents(List<InputEffectEntity> list)
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
