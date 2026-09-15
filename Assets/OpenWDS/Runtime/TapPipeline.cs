using System;
using System.Collections.Generic;

namespace OpenWDS.Runtime
{
    public readonly struct TapActionResult
    {
        public readonly bool Consumed;
        public readonly bool DeletedNote;
        public readonly int LaneId;
        public readonly NotationNote Note;
        public readonly TimingDecision Timing;

        public TapActionResult(
            bool consumed,
            bool deletedNote,
            int laneId,
            NotationNote note,
            TimingDecision timing)
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
    public sealed class TapNoteManager
    {
        private readonly List<NotationNote> _notes =
            new List<NotationNote>();

        public int Count => _notes.Count;
        public IReadOnlyList<NotationNote> Notes => _notes;

        public TapNoteManager(IReadOnlyList<NotationNote> notation)
        {
            if (notation == null) throw new ArgumentNullException(nameof(notation));
            foreach (var note in notation)
            {
                if (IsTapType(note.NoteType)) _notes.Add(note);
            }
        }

        public bool Contains(NotationNote note) =>
            note != null && _notes.Contains(note);

        public bool DeleteTapNote(NotationNote note) =>
            note != null && _notes.Remove(note);

        public static bool IsTapType(int noteType)
        {
            switch ((NoteType)noteType)
            {
                case NoteType.Normal:
                case NoteType.Critical:
                case NoteType.HoldStart:
                case NoteType.CriticalHoldStart:
                case NoteType.ScratchHoldStart:
                case NoteType.ScratchCriticalHoldStart:
                case NoteType.BlueTap:
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
    public sealed class TapAction
    {
        private readonly GameClock _clock;
        private readonly TapNoteManager _noteManager;

        public TapAction(
            GameClock clock,
            TapNoteManager noteManager)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _noteManager = noteManager ??
                           throw new ArgumentNullException(nameof(noteManager));
        }

        public TapActionResult TryTap(
            in InputEntity input,
            in HitLaneEntity hitLane,
            NotationNote note)
        {
            if (note == null || !_noteManager.Contains(note) ||
                input.Phase != TouchPhase.Began || !hitLane.Exists ||
                !NotationNoteProvider.TryGetIncludedLane(
                    hitLane, note, out var laneId))
            {
                return default;
            }

            var timing = TapTimingDecider.Decide(
                _clock, input.Milliseconds, note.StartMilliseconds);
            if (timing.TimingType == TimingType.None)
            {
                return default;
            }

            var deleted = _noteManager.DeleteTapNote(note);
            return new TapActionResult(
                deleted, deleted, laneId, note, timing);
        }

    }

    /// <summary>
    /// NotationNoteProvider.GetHitMain/GetHitSub lane selection. ARM64 reads the
    /// four collider-ignore properties before testing the matching sub lane.
    /// </summary>
    public static class NotationNoteProvider
    {
        public static bool TryGetIncludedLane(
            in HitLaneEntity hitLane,
            NotationNote note,
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

        private static bool IsIncluded(int laneId, NotationNote note)
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
