using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenWDS.Runtime
{
    public readonly struct RecoveredLaneRange
    {
        public readonly int StartLane;
        public readonly int EndLane;

        public RecoveredLaneRange(int startLane, int endLane)
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
    public static class RecoveredJumpScratch
    {
        public const int GimmickType = 1;

        public static bool IsJumpScratch(RecoveredNotationNote note) =>
            note != null && note.GimmickType == GimmickType;

        public static RecoveredLaneRange GetLaneRange(RecoveredNotationNote note)
        {
            if (!IsJumpScratch(note))
                throw new ArgumentException("Not a Jump-Scratch note.", nameof(note));
            if (note.GimmickValue > 0)
            {
                return new RecoveredLaneRange(
                    note.Lane,
                    note.Lane + note.GimmickValue - 1);
            }
            return new RecoveredLaneRange(
                note.EndLane + note.GimmickValue + 1,
                note.EndLane);
        }

        // AutoTouch's next predicate accepts 110/111, requires exact
        // current.End == candidate.Start, then tests candidate.Lane against the
        // current Jump-Scratch range.
        public static bool IsConnectedNext(
            RecoveredNotationNote current,
            RecoveredNotationNote candidate)
        {
            return IsJumpScratch(current) && IsScratchHold(candidate) &&
                   candidate.StartMilliseconds == current.EndMilliseconds &&
                   GetLaneRange(current).Contains(candidate.Lane);
        }

        // The previous predicate additionally requires the candidate itself to
        // be Jump-Scratch and tests current.Lane in the candidate's range.
        public static bool IsConnectedPrevious(
            RecoveredNotationNote current,
            RecoveredNotationNote candidate)
        {
            return current != null && IsJumpScratch(candidate) &&
                   IsScratchHold(candidate) &&
                   candidate.EndMilliseconds == current.StartMilliseconds &&
                   GetLaneRange(candidate).Contains(current.Lane);
        }

        public static bool ExistsConnectedNext(
            IReadOnlyList<RecoveredNotationNote> targets,
            RecoveredNotationNote current)
        {
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            foreach (var candidate in targets)
            {
                if (IsConnectedNext(current, candidate)) return true;
            }
            return false;
        }

        public static bool ExistsConnectedPrevious(
            IReadOnlyList<RecoveredNotationNote> targets,
            RecoveredNotationNote current)
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
        public static RecoveredNotationNote GetConnectedFirst(
            IReadOnlyList<RecoveredNotationNote> targets,
            RecoveredNotationNote current)
        {
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            if (current == null) throw new ArgumentNullException(nameof(current));

            var first = current;
            var visited = new HashSet<int>();
            while (visited.Add(first.Id))
            {
                RecoveredNotationNote previous = null;
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
        public static int GetDestinationLane(RecoveredNotationNote note)
        {
            var range = GetLaneRange(note);
            return note.GimmickValue > 0 ? range.EndLane : range.StartLane;
        }

        private static bool IsScratchHold(RecoveredNotationNote note) =>
            note != null &&
            (note.NoteType == (int)RecoveredNoteType.ScratchHold ||
             note.NoteType == (int)RecoveredNoteType.ScratchCriticalHold);
    }

    public readonly struct RecoveredScratchActionResult
    {
        public readonly bool Consumed;
        public readonly bool DeletedNote;
        public readonly bool AddedPendingDecide;
        public readonly RecoveredNotationNote Note;
        public readonly RecoveredTimingDecision Timing;

        public RecoveredScratchActionResult(
            bool consumed,
            bool deletedNote,
            bool addedPendingDecide,
            RecoveredNotationNote note,
            RecoveredTimingDecision timing)
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
    public static class RecoveredScratchTimingDecider
    {
        public const long MinMilliseconds = -50;
        public const long JustMilliseconds = 50;
        public const long MaxMilliseconds = 86400000;

        public static long GetTargetMilliseconds(RecoveredNotationNote note)
        {
            if (note == null) throw new ArgumentNullException(nameof(note));
            switch ((RecoveredNoteType)note.NoteType)
            {
                case RecoveredNoteType.Scratch:
                    return note.StartMilliseconds;
                case RecoveredNoteType.ScratchHold:
                case RecoveredNoteType.ScratchCriticalHold:
                    return note.EndMilliseconds;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(note), note.NoteType, "Not a Scratch timing target.");
            }
        }

        public static RecoveredTimingDecision DecideMusicTime(
            long inputMusicMilliseconds,
            RecoveredNotationNote note)
        {
            var diff = inputMusicMilliseconds - GetTargetMilliseconds(note);
            var timing = InRange(diff, MinMilliseconds, JustMilliseconds)
                ? RecoveredTimingType.PerfectStar
                : InRange(diff, JustMilliseconds, MaxMilliseconds)
                    ? RecoveredTimingType.Great
                    : RecoveredTimingType.None;
            return timing == RecoveredTimingType.None
                ? new RecoveredTimingDecision(
                    RecoveredTimingType.None, RecoveredTimingAssistType.None, 0)
                : new RecoveredTimingDecision(
                    timing, RecoveredTimingAssistType.None, diff);
        }

        // IScratchTimingDecider's three TryScratchCore gates. They use the
        // input timestamp, while InRangeTarget uses the current clock time.
        public static bool IsLessThanMinTiming(
            RecoveredNotationNote note,
            long inputMusicMilliseconds) =>
            inputMusicMilliseconds <
            GetTargetMilliseconds(note) + MinMilliseconds;

        public static bool IsMinTimingToJustTiming(
            RecoveredNotationNote note,
            long inputMusicMilliseconds)
        {
            var target = GetTargetMilliseconds(note);
            return target + MinMilliseconds <= inputMusicMilliseconds &&
                   inputMusicMilliseconds < target + JustMilliseconds;
        }

        public static bool IsJustTimingToMaxTiming(
            RecoveredNotationNote note,
            long inputMusicMilliseconds)
        {
            var target = GetTargetMilliseconds(note);
            return target + JustMilliseconds <= inputMusicMilliseconds &&
                   inputMusicMilliseconds < target + MaxMilliseconds;
        }

        public static bool InRangeTarget(
            RecoveredNotationNote note,
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
    public sealed class RecoveredScratchNoteManager
    {
        private readonly List<RecoveredNotationNote> _notes =
            new List<RecoveredNotationNote>();
        private readonly HashSet<int> _pendingDecideNoteIds =
            new HashSet<int>();

        public int Count => _notes.Count;
        public int PendingDecideCount => _pendingDecideNoteIds.Count;

        public RecoveredScratchNoteManager(IReadOnlyList<RecoveredNotationNote> notation)
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
                var timing = RecoveredScratchTimingDecider
                    .GetTargetMilliseconds(left)
                    .CompareTo(RecoveredScratchTimingDecider
                        .GetTargetMilliseconds(right));
                return timing != 0 ? timing : left.Id.CompareTo(right.Id);
            });
        }

        public bool Contains(RecoveredNotationNote note) =>
            note != null && _notes.Contains(note);

        public bool IsIncludedLane(
            int hitLaneId,
            RecoveredNotationNote note,
            bool shouldCheckJumpScratch = false)
        {
            if (hitLaneId == 0 || note == null) return false;
            if (shouldCheckJumpScratch)
            {
                return RecoveredJumpScratch.IsJumpScratch(note) &&
                       RecoveredJumpScratch.GetLaneRange(note).Contains(hitLaneId);
            }
            return note.Lane <= hitLaneId && hitLaneId <= note.EndLane;
        }

        // NotationNoteManager.AddPendingDecideScratchNoteId stores the Note Id
        // once. ScratchNotationNoteQueue.CanDelete rejects pending ids before
        // delegating to the ordinary automatic-deletion predicate.
        public bool AddPendingDecide(RecoveredNotationNote note)
        {
            if (note == null || !Contains(note)) return false;
            return _pendingDecideNoteIds.Add(note.Id);
        }

        public bool IsPendingDecide(RecoveredNotationNote note) =>
            note != null && _pendingDecideNoteIds.Contains(note.Id);

        public bool CanDeleteAutomatically(RecoveredNotationNote note) =>
            note != null && Contains(note) && !IsPendingDecide(note);

        public void ResetPendingDecide() => _pendingDecideNoteIds.Clear();

        public bool DeleteScratchNote(RecoveredNotationNote note)
        {
            return note != null && _notes.Remove(note);
        }

        // NotationNoteManager.GetScratchNotationNotes delegates to the Scratch
        // provider's GetAll(hitLane). The provider returns every candidate
        // covered by any main/sub ray; timing is evaluated later by
        // TryScratchCore for each item.
        public void GetScratchNotationNotes(
            List<RecoveredNotationNote> output,
            in RecoveredHitLaneEntity hitLane,
            long inputMusicMilliseconds)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            foreach (var candidate in _notes)
            {
                // TryScratchCore immediately rejects anything earlier than its
                // -50 ms window. Filter that provably inert tail here so dense
                // Moved/history input does not rescan every future Scratch and
                // ScratchHold in the chart for every finger.
                if (RecoveredScratchTimingDecider.GetTargetMilliseconds(candidate) >
                    inputMusicMilliseconds -
                    RecoveredScratchTimingDecider.MinMilliseconds)
                {
                    break;
                }
                if (IsIncludedByHitLane(hitLane, candidate))
                    output.Add(candidate);
            }
        }

        private bool IsIncludedByHitLane(
            in RecoveredHitLaneEntity hitLane,
            RecoveredNotationNote note)
        {
            return TryGetIncludedLane(hitLane, note, out _);
        }

        public bool TryGetIncludedLane(
            in RecoveredHitLaneEntity hitLane,
            RecoveredNotationNote note,
            out int laneId)
        {
            var jump = RecoveredJumpScratch.IsJumpScratch(note);
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
            RecoveredNotationNote note,
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
            in RecoveredHitLaneEntity hitLane,
            long inputMusicMilliseconds,
            out RecoveredNotationNote note,
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
            in RecoveredHitLaneEntity hitLane,
            long inputMusicMilliseconds,
            out RecoveredNotationNote note,
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

        private bool TryFindActiveScratchHoldForAssignment(
            int candidateLane,
            long inputMusicMilliseconds,
            out RecoveredNotationNote note,
            out int laneId)
        {
            RecoveredNotationNote best = null;
            foreach (var candidate in _notes)
            {
                if ((candidate.NoteType !=
                        (int)RecoveredNoteType.ScratchHold &&
                    candidate.NoteType !=
                        (int)RecoveredNoteType.ScratchCriticalHold) ||
                    candidateLane == 0 ||
                    candidate.StartMilliseconds > inputMusicMilliseconds ||
                    inputMusicMilliseconds >= candidate.EndMilliseconds ||
                    !IsIncludedLane(
                        candidateLane,
                        candidate,
                        RecoveredJumpScratch.IsJumpScratch(candidate)))
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
            RecoveredNotationNote current,
            out RecoveredNotationNote next)
        {
            next = null;
            if (current == null) return false;
            foreach (var candidate in _notes)
            {
                if (!RecoveredJumpScratch.IsConnectedNext(
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
            out RecoveredNotationNote note,
            out int laneId)
        {
            note = FindActiveScratchHold(candidateLane, inputMusicMilliseconds);
            laneId = note != null ? candidateLane : 0;
            return note != null;
        }

        private RecoveredNotationNote FindActiveScratchHold(
            int laneId,
            long inputMusicMilliseconds)
        {
            if (laneId == 0) return null;
            RecoveredNotationNote best = null;
            foreach (var candidate in _notes)
            {
                if (candidate.NoteType != (int)RecoveredNoteType.ScratchHold &&
                    candidate.NoteType != (int)RecoveredNoteType.ScratchCriticalHold)
                {
                    continue;
                }
                var included = RecoveredJumpScratch.IsJumpScratch(candidate)
                    ? IsIncludedLane(laneId, candidate, true)
                    : IsIncludedLane(laneId, candidate);
                if (!included ||
                    RecoveredScratchTimingDecider.DecideMusicTime(
                        inputMusicMilliseconds, candidate).TimingType ==
                    RecoveredTimingType.None)
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
            noteType == (int)RecoveredNoteType.Scratch ||
            noteType == (int)RecoveredNoteType.ScratchHold ||
            noteType == (int)RecoveredNoteType.ScratchCriticalHold;
    }

    /// <summary>
    /// InputAction.TryScratchCore. In the early/Just window a Moved gesture must
    /// exceed ScratchDistance. An insufficient gesture protects the note from
    /// that frame's automatic deletion by adding its id to the pending list.
    /// Once the input timestamp reaches the late window, the original consumes
    /// it as GREAT without another distance or phase requirement.
    /// </summary>
    public sealed class RecoveredScratchAction
    {
        private readonly RecoveredScratchNoteManager _noteManager;
        private readonly float _scratchDistance;

        public RecoveredScratchAction(
            RecoveredScratchNoteManager noteManager,
            float scratchDistance = RecoveredOriginalGameConfig.ScratchDistance)
        {
            if (scratchDistance < 0f)
                throw new ArgumentOutOfRangeException(nameof(scratchDistance));
            _noteManager = noteManager ??
                           throw new ArgumentNullException(nameof(noteManager));
            _scratchDistance = scratchDistance;
        }

        public RecoveredScratchActionResult TryScratch(
            in RecoveredInputEntity input,
            RecoveredNotationNote note,
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

            if (RecoveredScratchTimingDecider.IsLessThanMinTiming(
                    note, inputMusicMilliseconds))
            {
                return default;
            }

            if (RecoveredScratchTimingDecider.IsJustTimingToMaxTiming(
                    note, inputMusicMilliseconds))
            {
                var lateTiming = new RecoveredTimingDecision(
                    RecoveredTimingType.Great,
                    RecoveredTimingAssistType.None,
                    0);
                var lateDeleted = _noteManager.DeleteScratchNote(note);
                return new RecoveredScratchActionResult(
                    lateDeleted, lateDeleted, false, note, lateTiming);
            }

            if (input.Phase != RecoveredTouchPhase.Moved)
                return default;

            if (input.DeltaPosition.sqrMagnitude <= _scratchDistance)
            {
                var clockMilliseconds =
                    currentMusicMilliseconds ?? inputMusicMilliseconds;
                var addedPending =
                    RecoveredScratchTimingDecider.InRangeTarget(
                        note, clockMilliseconds) &&
                    _noteManager.AddPendingDecide(note);
                return new RecoveredScratchActionResult(
                    false, false, addedPending, note, default);
            }

            var timing = RecoveredScratchTimingDecider.DecideMusicTime(
                inputMusicMilliseconds, note);
            if (timing.TimingType == RecoveredTimingType.None)
                return default;

            var deleted = _noteManager.DeleteScratchNote(note);
            return new RecoveredScratchActionResult(
                deleted, deleted, false, note, timing);
        }
    }
}
