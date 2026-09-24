using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenWDS.Runtime
{
    public readonly struct LaneRange
    {
        public readonly int StartLane;
        public readonly int EndLane;

        public LaneRange(int startLane, int endLane)
        {
            StartLane = startLane;
            EndLane = endLane;
        }

        public bool Contains(int laneId) =>
            StartLane <= laneId && laneId <= EndLane;
    }

    /// <summary>
    /// AutoTouch.GetLaneJumpScratch (RVA 0xB94D6E4). GimmickValue is a signed
    /// lane span, not a destination lane: positive spans right from Lane;
    /// negative spans left through EndLane.
    /// </summary>
    public static class JumpScratch
    {
        public const int GimmickType = 1;

        public static bool IsJumpScratch(NotationNote note) =>
            note != null && note.GimmickType == GimmickType;

        public static LaneRange GetLaneRange(NotationNote note)
        {
            if (!IsJumpScratch(note))
                throw new ArgumentException("Not a Jump-Scratch note.", nameof(note));
            if (note.GimmickValue > 0)
            {
                return new LaneRange(
                    note.Lane,
                    note.Lane + note.GimmickValue - 1);
            }
            return new LaneRange(
                note.EndLane + note.GimmickValue + 1,
                note.EndLane);
        }

        // AutoTouch's next predicate accepts 110/111, requires exact
        // current.End == candidate.Start, then tests candidate.Lane against the
        // current Jump-Scratch range.
        public static bool IsConnectedNext(
            NotationNote current,
            NotationNote candidate)
        {
            return IsJumpScratch(current) && IsScratchHold(candidate) &&
                   candidate.StartMilliseconds == current.EndMilliseconds &&
                   GetLaneRange(current).Contains(candidate.Lane);
        }

        // The previous predicate additionally requires the candidate itself to
        // be Jump-Scratch and tests current.Lane in the candidate's range.
        public static bool IsConnectedPrevious(
            NotationNote current,
            NotationNote candidate)
        {
            return current != null && IsJumpScratch(candidate) &&
                   IsScratchHold(candidate) &&
                   candidate.EndMilliseconds == current.StartMilliseconds &&
                   GetLaneRange(candidate).Contains(current.Lane);
        }

        public static bool ExistsConnectedNext(
            IReadOnlyList<NotationNote> targets,
            NotationNote current)
        {
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            foreach (var candidate in targets)
            {
                if (IsConnectedNext(current, candidate)) return true;
            }
            return false;
        }

        public static bool ExistsConnectedPrevious(
            IReadOnlyList<NotationNote> targets,
            NotationNote current)
        {
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            foreach (var candidate in targets)
            {
                if (IsConnectedPrevious(current, candidate)) return true;
            }
            return false;
        }

        // GetConnectedFirstJumpScratchNote repeatedly follows the exact
        // previous predicate until it reaches the first 110/111 segment.
        public static NotationNote GetConnectedFirst(
            IReadOnlyList<NotationNote> targets,
            NotationNote current)
        {
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            if (current == null) throw new ArgumentNullException(nameof(current));

            var first = current;
            var visited = new HashSet<int>();
            while (visited.Add(first.Id))
            {
                NotationNote previous = null;
                foreach (var candidate in targets)
                {
                    if (IsConnectedPrevious(first, candidate))
                    {
                        previous = candidate;
                        break;
                    }
                }
                if (previous == null) break;
                first = previous;
            }
            return first;
        }

        // GetScreenPositionForJumpScratch selects the far edge in the signed
        // gimmick direction: right edge for positive and left edge for negative.
        public static int GetDestinationLane(NotationNote note)
        {
            var range = GetLaneRange(note);
            return note.GimmickValue > 0 ? range.EndLane : range.StartLane;
        }

        private static bool IsScratchHold(NotationNote note) =>
            note != null &&
            (note.NoteType == (int)NoteType.ScratchHold ||
             note.NoteType == (int)NoteType.ScratchCriticalHold);
    }

    public readonly struct ScratchActionResult
    {
        public readonly bool Consumed;
        public readonly bool DeletedNote;
        public readonly bool AddedPendingDecide;
        public readonly NotationNote Note;
        public readonly TimingDecision Timing;

        public ScratchActionResult(
            bool consumed,
            bool deletedNote,
            bool addedPendingDecide,
            NotationNote note,
            TimingDecision timing)
        {
            Consumed = consumed;
            DeletedNote = deletedNote;
            AddedPendingDecide = addedPendingDecide;
            Note = note;
            Timing = timing;
        }
    }

    /// <summary>
    /// ScratchTimingDecider recovered from CreateGameScratchTimings and
    /// GetTargetMilliseconds. Type 40 targets StartMilliseconds; 110/111 target
    /// EndMilliseconds. Intervals are lower-inclusive and upper-exclusive.
    /// </summary>
    public static class ScratchTimingDecider
    {
        public const long MinMilliseconds = -50;
        public const long JustMilliseconds = 50;
        public const long MaxMilliseconds = 86400000;

        public static long GetTargetMilliseconds(NotationNote note)
        {
            if (note == null) throw new ArgumentNullException(nameof(note));
            switch ((NoteType)note.NoteType)
            {
                case NoteType.Scratch:
                    return note.StartMilliseconds;
                case NoteType.ScratchHold:
                case NoteType.ScratchCriticalHold:
                    return note.EndMilliseconds;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(note), note.NoteType, "Not a Scratch timing target.");
            }
        }

        public static TimingDecision DecideMusicTime(
            long inputMusicMilliseconds,
            NotationNote note)
        {
            var diff = inputMusicMilliseconds - GetTargetMilliseconds(note);
            var timing = InRange(diff, MinMilliseconds, JustMilliseconds)
                ? TimingType.PerfectStar
                : InRange(diff, JustMilliseconds, MaxMilliseconds)
                    ? TimingType.Great
                    : TimingType.None;
            return timing == TimingType.None
                ? new TimingDecision(
                    TimingType.None, TimingAssistType.None, 0)
                : new TimingDecision(
                    timing, TimingAssistType.None, diff);
        }

        // IScratchTimingDecider's three TryScratchCore gates. They use the
        // input timestamp, while InRangeTarget uses the current clock time.
        public static bool IsLessThanMinTiming(
            NotationNote note,
            long inputMusicMilliseconds) =>
            inputMusicMilliseconds <
            GetTargetMilliseconds(note) + MinMilliseconds;

        public static bool IsMinTimingToJustTiming(
            NotationNote note,
            long inputMusicMilliseconds)
        {
            var target = GetTargetMilliseconds(note);
            return target + MinMilliseconds <= inputMusicMilliseconds &&
                   inputMusicMilliseconds < target + JustMilliseconds;
        }

        public static bool IsJustTimingToMaxTiming(
            NotationNote note,
            long inputMusicMilliseconds)
        {
            var target = GetTargetMilliseconds(note);
            return target + JustMilliseconds <= inputMusicMilliseconds &&
                   inputMusicMilliseconds < target + MaxMilliseconds;
        }

        public static bool InRangeTarget(
            NotationNote note,
            long currentMusicMilliseconds) =>
            currentMusicMilliseconds >=
            GetTargetMilliseconds(note) + MinMilliseconds;

        private static bool InRange(long value, long from, long to) =>
            from <= value && value < to;
    }

    /// <summary>
    /// ScratchNotationNoteQueue's accepted type set and the ordinary inclusive
    /// lane branch. Jump-Scratch widens/redirects lanes in a separate provider
    /// path and is intentionally not folded into this class.
    /// </summary>
    public sealed class ScratchNoteManager
    {
        private readonly List<NotationNote> _notes =
            new List<NotationNote>();
        private readonly HashSet<int> _pendingDecideNoteIds =
            new HashSet<int>();

        public int Count => _notes.Count;
        public int PendingDecideCount => _pendingDecideNoteIds.Count;

        public ScratchNoteManager(IReadOnlyList<NotationNote> notation)
        {
            if (notation == null) throw new ArgumentNullException(nameof(notation));
            foreach (var note in notation)
            {
                if (IsScratchType(note.NoteType)) _notes.Add(note);
            }
            // ScratchNotationNoteQueue is consumed by scratch target time:
            // type 40 uses StartMilliseconds, 110/111 use EndMilliseconds.
            // Keeping that order also lets GetAll stop at the first candidate
            // outside the -50 ms input window.
            _notes.Sort((left, right) =>
            {
                var timing = ScratchTimingDecider
                    .GetTargetMilliseconds(left)
                    .CompareTo(ScratchTimingDecider
                        .GetTargetMilliseconds(right));
                return timing != 0 ? timing : left.Id.CompareTo(right.Id);
            });
        }

        public bool Contains(NotationNote note) =>
            note != null && _notes.Contains(note);

        public bool IsIncludedLane(
            int hitLaneId,
            NotationNote note,
            bool shouldCheckJumpScratch = false)
        {
            if (hitLaneId == 0 || note == null) return false;
            if (shouldCheckJumpScratch)
            {
                return JumpScratch.IsJumpScratch(note) &&
                       JumpScratch.GetLaneRange(note).Contains(hitLaneId);
            }
            return note.Lane <= hitLaneId && hitLaneId <= note.EndLane;
        }

        // NotationNoteManager.AddPendingDecideScratchNoteId stores the Note Id
        // once. ScratchNotationNoteQueue.CanDelete rejects pending ids before
        // delegating to the ordinary automatic-deletion predicate.
        public bool AddPendingDecide(NotationNote note)
        {
            if (note == null || !Contains(note)) return false;
            return _pendingDecideNoteIds.Add(note.Id);
        }

        public bool IsPendingDecide(NotationNote note) =>
            note != null && _pendingDecideNoteIds.Contains(note.Id);

        public bool CanDeleteAutomatically(NotationNote note) =>
            note != null && Contains(note) && !IsPendingDecide(note);

        public void ResetPendingDecide() => _pendingDecideNoteIds.Clear();

        public bool DeleteScratchNote(NotationNote note)
        {
            return note != null && _notes.Remove(note);
        }

        // NotationNoteManager.GetScratchNotationNotes delegates to the Scratch
        // provider's GetAll(hitLane). The provider returns every candidate
        // covered by any main/sub ray; timing is evaluated later by
        // TryScratchCore for each item.
        public void GetScratchNotationNotes(
            List<NotationNote> output,
            in HitLaneEntity hitLane,
            long inputMusicMilliseconds)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            foreach (var candidate in _notes)
            {
                // TryScratchCore immediately rejects anything earlier than its
                // -50 ms window. Filter that provably inert tail here so dense
                // Moved/history input does not rescan every future Scratch and
                // ScratchHold in the chart for every finger.
                if (ScratchTimingDecider.GetTargetMilliseconds(candidate) >
                    inputMusicMilliseconds -
                    ScratchTimingDecider.MinMilliseconds)
                {
                    break;
                }
                if (IsIncludedByHitLane(hitLane, candidate))
                    output.Add(candidate);
            }
        }

        private bool IsIncludedByHitLane(
            in HitLaneEntity hitLane,
            NotationNote note)
        {
            return TryGetIncludedLane(hitLane, note, out _);
        }

        public bool TryGetIncludedLane(
            in HitLaneEntity hitLane,
            NotationNote note,
            out int laneId)
        {
            var jump = JumpScratch.IsJumpScratch(note);
            if (TryIncludedLane(hitLane.HitMainLaneId, note, jump, out laneId) ||
                TryIncludedLane(hitLane.HitSubLeftInnerLaneId, note, jump, out laneId) ||
                TryIncludedLane(hitLane.HitSubRightInnerLaneId, note, jump, out laneId) ||
                TryIncludedLane(hitLane.HitSubLeftOuterLaneId, note, jump, out laneId) ||
                TryIncludedLane(hitLane.HitSubRightOuterLaneId, note, jump, out laneId))
                return true;
            laneId = 0;
            return false;
        }

        private bool TryIncludedLane(
            int candidateLane,
            NotationNote note,
            bool jump,
            out int laneId)
        {
            if (IsIncludedLane(candidateLane, note, jump))
            {
                laneId = candidateLane;
                return true;
            }
            laneId = 0;
            return false;
        }

        /// <summary>
        /// HoldNotationNoteProvider.GetHoldIncludeJumpScratch combines ordinary
        /// lane coverage with the signed Jump-Scratch coverage and orders the
        /// temporary candidates by their Hold target (EndMilliseconds).
        /// </summary>
        public bool TryGetHoldIncludeJumpScratch(
            in HitLaneEntity hitLane,
            long inputMusicMilliseconds,
            out NotationNote note,
            out int laneId)
        {
            var mainLane = hitLane.HitMainLaneId;
            note = FindActiveScratchHold(mainLane, inputMusicMilliseconds);
            if (note != null)
            {
                laneId = mainLane;
                return true;
            }

            if (TryFindActiveScratchHold(
                    hitLane.HitSubLeftInnerLaneId, inputMusicMilliseconds,
                    out note, out laneId) ||
                TryFindActiveScratchHold(
                    hitLane.HitSubRightInnerLaneId, inputMusicMilliseconds,
                    out note, out laneId) ||
                TryFindActiveScratchHold(
                    hitLane.HitSubLeftOuterLaneId, inputMusicMilliseconds,
                    out note, out laneId) ||
                TryFindActiveScratchHold(
                    hitLane.HitSubRightOuterLaneId, inputMusicMilliseconds,
                    out note, out laneId))
                return true;
            note = null;
            laneId = 0;
            return false;
        }

        public bool TryGetActiveHoldForAssignment(
            in HitLaneEntity hitLane,
            long inputMusicMilliseconds,
            out NotationNote note,
            out int laneId)
        {
            // Provider precedence is global: search every candidate on Main
            // before falling back to the four Sub lanes. Iterating candidates
            // first lets a neighbouring Jump-Scratch claim the touch through
            // an outer Sub collider even though another body directly covers
            // the Main lane (parallel Stella chains expose this reliably).
            if (TryFindActiveScratchHoldForAssignment(
                    hitLane.HitMainLaneId, inputMusicMilliseconds,
                    out note, out laneId) ||
                TryFindActiveScratchHoldForAssignment(
                    hitLane.HitSubLeftInnerLaneId, inputMusicMilliseconds,
                    out note, out laneId) ||
                TryFindActiveScratchHoldForAssignment(
                    hitLane.HitSubRightInnerLaneId, inputMusicMilliseconds,
                    out note, out laneId) ||
                TryFindActiveScratchHoldForAssignment(
                    hitLane.HitSubLeftOuterLaneId, inputMusicMilliseconds,
                    out note, out laneId) ||
                TryFindActiveScratchHoldForAssignment(
                    hitLane.HitSubRightOuterLaneId, inputMusicMilliseconds,
                    out note, out laneId))
            {
                return true;
            }
            note = null;
            laneId = 0;
            return false;
        }

        public bool TryGetActiveBodyForMain(
            in HitLaneEntity hitLane, long musicMilliseconds, out NotationNote note)
        {
            // NotationNoteManager.GetHoldNotationNoteForMain passes false for
            // jump coverage. The shared Hold queue contains 100/101/110/111;
            // a jump's destination span is only used by the separate provider.
            note = null;
            foreach (var candidate in _notes)
            {
                if ((candidate.NoteType != 110 && candidate.NoteType != 111) ||
                    candidate.StartMilliseconds > musicMilliseconds ||
                    candidate.EndMilliseconds <= musicMilliseconds ||
                    !IsIncludedLane(hitLane.HitMainLaneId, candidate, false)) continue;
                if (note == null || candidate.StartMilliseconds < note.StartMilliseconds)
                    note = candidate;
            }
            return note != null;
        }

        private bool TryFindActiveScratchHoldForAssignment(
            int candidateLane,
            long inputMusicMilliseconds,
            out NotationNote note,
            out int laneId)
        {
            NotationNote best = null;
            foreach (var candidate in _notes)
            {
                if ((candidate.NoteType !=
                        (int)NoteType.ScratchHold &&
                    candidate.NoteType !=
                        (int)NoteType.ScratchCriticalHold) ||
                    candidateLane == 0 ||
                    candidate.StartMilliseconds > inputMusicMilliseconds ||
                    inputMusicMilliseconds >= candidate.EndMilliseconds ||
                    !IsIncludedLane(
                        candidateLane,
                        candidate,
                        JumpScratch.IsJumpScratch(candidate)))
                {
                    continue;
                }
                if (best == null ||
                    candidate.EndMilliseconds < best.EndMilliseconds)
                {
                    best = candidate;
                }
            }
            note = best;
            laneId = note != null ? candidateLane : 0;
            return note != null;
        }

        public bool TryGetConnectedNext(
            NotationNote current,
            out NotationNote next)
        {
            next = null;
            if (current == null) return false;
            foreach (var candidate in _notes)
            {
                if (!JumpScratch.IsConnectedNext(
                        current, candidate))
                    continue;
                if (next == null ||
                    candidate.EndMilliseconds < next.EndMilliseconds)
                    next = candidate;
            }
            return next != null;
        }

        private bool TryFindActiveScratchHold(
            int candidateLane,
            long inputMusicMilliseconds,
            out NotationNote note,
            out int laneId)
        {
            note = FindActiveScratchHold(candidateLane, inputMusicMilliseconds);
            laneId = note != null ? candidateLane : 0;
            return note != null;
        }

        private NotationNote FindActiveScratchHold(
            int laneId,
            long inputMusicMilliseconds)
        {
            if (laneId == 0) return null;
            NotationNote best = null;
            foreach (var candidate in _notes)
            {
                if (candidate.NoteType != (int)NoteType.ScratchHold &&
                    candidate.NoteType != (int)NoteType.ScratchCriticalHold)
                {
                    continue;
                }
                var included = JumpScratch.IsJumpScratch(candidate)
                    ? IsIncludedLane(laneId, candidate, true)
                    : IsIncludedLane(laneId, candidate);
                if (!included ||
                    ScratchTimingDecider.DecideMusicTime(
                        inputMusicMilliseconds, candidate).TimingType ==
                    TimingType.None)
                {
                    continue;
                }
                if (best == null ||
                    candidate.EndMilliseconds < best.EndMilliseconds)
                {
                    best = candidate;
                }
            }
            return best;
        }

        private static bool IsScratchType(int noteType) =>
            noteType == (int)NoteType.Scratch ||
            noteType == (int)NoteType.ScratchHold ||
            noteType == (int)NoteType.ScratchCriticalHold;
    }

    /// <summary>
    /// InputAction.TryScratchCore. In the early/Just window a Moved gesture must
    /// exceed ScratchDistance. An insufficient gesture protects the note from
    /// that frame's automatic deletion by adding its id to the pending list.
    /// Once the input timestamp reaches the late window, the original consumes
    /// it as GREAT without another distance or phase requirement.
    /// </summary>
    public sealed class ScratchAction
    {
        private readonly ScratchNoteManager _noteManager;
        private readonly float _scratchDistance;

        public ScratchAction(
            ScratchNoteManager noteManager,
            float scratchDistance = OriginalGameConfig.ScratchDistance)
        {
            if (scratchDistance < 0f)
                throw new ArgumentOutOfRangeException(nameof(scratchDistance));
            _noteManager = noteManager ??
                           throw new ArgumentNullException(nameof(noteManager));
            _scratchDistance = scratchDistance;
        }

        public ScratchActionResult TryScratch(
            in InputEntity input,
            NotationNote note,
            int hitLaneId,
            long inputMusicMilliseconds,
            bool shouldCheckJumpScratch = false,
            long? currentMusicMilliseconds = null)
        {
            if (note == null || !_noteManager.Contains(note) ||
                !_noteManager.IsIncludedLane(
                    hitLaneId, note, shouldCheckJumpScratch))
            {
                return default;
            }

            if (ScratchTimingDecider.IsLessThanMinTiming(
                    note, inputMusicMilliseconds))
            {
                return default;
            }

            if (ScratchTimingDecider.IsJustTimingToMaxTiming(
                    note, inputMusicMilliseconds))
            {
                var lateTiming = new TimingDecision(
                    TimingType.Great,
                    TimingAssistType.None,
                    0);
                var lateDeleted = _noteManager.DeleteScratchNote(note);
                return new ScratchActionResult(
                    lateDeleted, lateDeleted, false, note, lateTiming);
            }

            if (input.Phase != TouchPhase.Moved)
                return default;

            if (input.DeltaPosition.sqrMagnitude <= _scratchDistance)
            {
                var clockMilliseconds =
                    currentMusicMilliseconds ?? inputMusicMilliseconds;
                var addedPending =
                    ScratchTimingDecider.InRangeTarget(
                        note, clockMilliseconds) &&
                    _noteManager.AddPendingDecide(note);
                return new ScratchActionResult(
                    false, false, addedPending, note, default);
            }

            var timing = ScratchTimingDecider.DecideMusicTime(
                inputMusicMilliseconds, note);
            if (timing.TimingType == TimingType.None)
                return default;

            var deleted = _noteManager.DeleteScratchNote(note);
            return new ScratchActionResult(
                deleted, deleted, false, note, timing);
        }
    }
}
