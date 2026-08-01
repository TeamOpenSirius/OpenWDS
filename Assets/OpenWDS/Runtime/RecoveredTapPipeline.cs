using System;
using System.Collections.Generic;

namespace OpenWDS.Runtime
{
    public readonly struct RecoveredTapActionResult
    {
        public readonly bool Consumed;
        public readonly bool DeletedNote;
        public readonly int LaneId;
        public readonly RecoveredNotationNote Note;
        public readonly RecoveredTimingDecision Timing;

        public RecoveredTapActionResult(
            bool consumed,
            bool deletedNote,
            int laneId,
            RecoveredNotationNote note,
            RecoveredTimingDecision timing)
        {
            Consumed = consumed;
            DeletedNote = deletedNote;
            LaneId = laneId;
            Note = note;
            Timing = timing;
        }
    }

    /// <summary>
    /// TapNotationNoteQueue's type filter and the ordinary inclusive lane provider.
    /// The list is intentionally shared with InputHandler's sorted Tap/Flick pass.
    /// </summary>
    public sealed class RecoveredTapNoteManager
    {
        private readonly List<RecoveredNotationNote> _notes =
            new List<RecoveredNotationNote>();

        public int Count => _notes.Count;
        public IReadOnlyList<RecoveredNotationNote> Notes => _notes;

        public RecoveredTapNoteManager(IReadOnlyList<RecoveredNotationNote> notation)
        {
            if (notation == null) throw new ArgumentNullException(nameof(notation));
            foreach (var note in notation)
            {
                if (IsTapType(note.NoteType)) _notes.Add(note);
            }
        }

        public bool Contains(RecoveredNotationNote note) =>
            note != null && _notes.Contains(note);

        public bool DeleteTapNote(RecoveredNotationNote note) =>
            note != null && _notes.Remove(note);

        public static bool IsTapType(int noteType)
        {
            switch ((RecoveredNoteType)noteType)
            {
                case RecoveredNoteType.Normal:
                case RecoveredNoteType.Critical:
                case RecoveredNoteType.HoldStart:
                case RecoveredNoteType.CriticalHoldStart:
                case RecoveredNoteType.ScratchHoldStart:
                case RecoveredNoteType.ScratchCriticalHoldStart:
                case RecoveredNoteType.BlueTap:
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// InputAction.TryTap/TryTapCore's recovered input, lane, timing and deletion
    /// gates. Successful non-release inputs are recorded by the caller's
    /// LaneHitManager, matching TryTap's outer wrapper.
    /// </summary>
    public sealed class RecoveredTapAction
    {
        private readonly RecoveredGameClock _clock;
        private readonly RecoveredTapNoteManager _noteManager;

        public RecoveredTapAction(
            RecoveredGameClock clock,
            RecoveredTapNoteManager noteManager)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _noteManager = noteManager ??
                           throw new ArgumentNullException(nameof(noteManager));
        }

        public RecoveredTapActionResult TryTap(
            in RecoveredInputEntity input,
            in RecoveredHitLaneEntity hitLane,
            RecoveredNotationNote note)
        {
            if (note == null || !_noteManager.Contains(note) ||
                input.Phase != RecoveredTouchPhase.Began || !hitLane.Exists ||
                !RecoveredNotationNoteProvider.TryGetIncludedLane(
                    hitLane, note, out var laneId))
            {
                return default;
            }

            var timing = RecoveredTapTimingDecider.Decide(
                _clock, input.Milliseconds, note.StartMilliseconds);
            if (timing.TimingType == RecoveredTimingType.None)
            {
                return default;
            }

            var deleted = _noteManager.DeleteTapNote(note);
            return new RecoveredTapActionResult(
                deleted, deleted, laneId, note, timing);
        }

    }

    /// <summary>
    /// NotationNoteProvider.GetHitMain/GetHitSub lane selection. ARM64 reads the
    /// four collider-ignore properties before testing the matching sub lane.
    /// </summary>
    public static class RecoveredNotationNoteProvider
    {
        public static bool TryGetIncludedLane(
            in RecoveredHitLaneEntity hitLane,
            RecoveredNotationNote note,
            out int laneId)
        {
            if (note == null)
            {
                laneId = 0;
                return false;
            }

            if (IsIncluded(hitLane.HitMainLaneId, note))
                return Return(hitLane.HitMainLaneId, out laneId);
            if (!note.IgnoreLeftInnerCollider &&
                IsIncluded(hitLane.HitSubLeftInnerLaneId, note))
                return Return(hitLane.HitSubLeftInnerLaneId, out laneId);
            if (!note.IgnoreRightInnerCollider &&
                IsIncluded(hitLane.HitSubRightInnerLaneId, note))
                return Return(hitLane.HitSubRightInnerLaneId, out laneId);
            if (!note.IgnoreLeftOuterCollider &&
                IsIncluded(hitLane.HitSubLeftOuterLaneId, note))
                return Return(hitLane.HitSubLeftOuterLaneId, out laneId);
            if (!note.IgnoreRightOuterCollider &&
                IsIncluded(hitLane.HitSubRightOuterLaneId, note))
                return Return(hitLane.HitSubRightOuterLaneId, out laneId);

            laneId = 0;
            return false;
        }

        private static bool IsIncluded(int laneId, RecoveredNotationNote note)
        {
            return laneId != 0 && laneId >= note.Lane && laneId <= note.EndLane;
        }

        private static bool Return(int value, out int laneId)
        {
            laneId = value;
            return true;
        }
    }
}
