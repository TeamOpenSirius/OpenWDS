using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenWDS.Runtime
{
    public enum RecoveredFlickCompletionReason
    {
        None = 0,
        Moved = 1,
        Expired = 2,
        Released = 3,
    }

    public readonly struct RecoveredFlickActionResult
    {
        public readonly bool Handled;
        public readonly bool Consumed;
        public readonly bool DeletedNote;
        public readonly RecoveredNotationNote Note;
        public readonly RecoveredTimingDecision Timing;
        public readonly RecoveredFlickCompletionReason CompletionReason;

        public RecoveredFlickActionResult(
            bool handled,
            bool consumed,
            bool deletedNote,
            RecoveredNotationNote note,
            RecoveredTimingDecision timing,
            RecoveredFlickCompletionReason completionReason)
        {
            Handled = handled;
            Consumed = consumed;
            DeletedNote = deletedNote;
            Note = note;
            Timing = timing;
            CompletionReason = completionReason;
        }
    }

    /// <summary>
    /// FlickTimingDecider uses two symmetric absolute windows: 70 ms
    /// PERFECT_STAR and 100 ms GREAT. Unlike Tap, the returned assist and diff
    /// fields are always zero in the original ARM64 implementation.
    /// </summary>
    public static class RecoveredFlickTimingDecider
    {
        public const long PerfectStarMilliseconds = 70;
        public const long GreatMilliseconds = 100;

        public static RecoveredTimingDecision DecideMusicTime(
            long inputMusicMilliseconds,
            long noteMusicMilliseconds)
        {
            var diff = inputMusicMilliseconds - noteMusicMilliseconds;
            var absolute = diff < 0 ? -diff : diff;
            var timing = absolute <= PerfectStarMilliseconds
                ? RecoveredTimingType.PerfectStar
                : absolute <= GreatMilliseconds
                    ? RecoveredTimingType.Great
                    : RecoveredTimingType.None;
            return new RecoveredTimingDecision(
                timing, RecoveredTimingAssistType.None, 0);
        }

        public static RecoveredTimingDecision Decide(
            RecoveredGameClock clock,
            long inputGameMilliseconds,
            RecoveredNotationNote note)
        {
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            if (note == null) throw new ArgumentNullException(nameof(note));
            return DecideMusicTime(
                clock.InputTimeToMusicMilliseconds(inputGameMilliseconds),
                note.StartMilliseconds);
        }

        public static bool IsBeganRangeTarget(
            RecoveredGameClock clock,
            RecoveredNotationNote note,
            long beganInputMilliseconds)
        {
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            if (note == null) throw new ArgumentNullException(nameof(note));
            var beganMusicMilliseconds =
                clock.InputTimeToMusicMilliseconds(beganInputMilliseconds);
            return note.StartMilliseconds - GreatMilliseconds <= beganMusicMilliseconds &&
                   beganMusicMilliseconds < note.StartMilliseconds + GreatMilliseconds;
        }

        public static bool IsExpired(
            long passedGameMilliseconds,
            long beganInputMilliseconds,
            long flickExpiredMilliseconds)
        {
            return passedGameMilliseconds >=
                   beganInputMilliseconds + flickExpiredMilliseconds;
        }
    }

    public sealed class RecoveredFlickNoteManager
    {
        private readonly List<RecoveredNotationNote> _notes =
            new List<RecoveredNotationNote>();

        public int Count => _notes.Count;
        public IReadOnlyList<RecoveredNotationNote> Notes => _notes;

        public RecoveredFlickNoteManager(IReadOnlyList<RecoveredNotationNote> notation)
        {
            if (notation == null) throw new ArgumentNullException(nameof(notation));
            foreach (var note in notation)
            {
                if (note.NoteType == (int)RecoveredNoteType.Flick)
                {
                    _notes.Add(note);
                }
            }
        }

        public bool Contains(RecoveredNotationNote note) =>
            note != null && _notes.Contains(note);

        public bool DeleteFlickNote(RecoveredNotationNote note) =>
            note != null && _notes.Remove(note);
    }

    /// <summary>
    /// InputAction.TryFlick/TryFlickCore. The machine code compares
    /// DeltaPosition.sqrMagnitude directly against FlickDistance without squaring
    /// the serialized config value.
    /// </summary>
    public sealed class RecoveredFlickAction
    {
        private readonly RecoveredGameClock _clock;
        private readonly RecoveredFlickInputManager _inputManager;
        private readonly RecoveredFlickNoteManager _noteManager;
        private readonly float _flickDistance;
        private readonly long _flickExpiredMilliseconds;

        public RecoveredFlickAction(
            RecoveredGameClock clock,
            RecoveredFlickInputManager inputManager,
            RecoveredFlickNoteManager noteManager,
            float flickDistance = RecoveredOriginalGameConfig.FlickDistance,
            long flickExpiredMilliseconds =
                RecoveredOriginalGameConfig.FlickExpiredMilliseconds)
        {
            if (flickDistance < 0f)
                throw new ArgumentOutOfRangeException(nameof(flickDistance));
            if (flickExpiredMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(flickExpiredMilliseconds));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _inputManager = inputManager ?? throw new ArgumentNullException(nameof(inputManager));
            _noteManager = noteManager ?? throw new ArgumentNullException(nameof(noteManager));
            _flickDistance = flickDistance;
            _flickExpiredMilliseconds = flickExpiredMilliseconds;
        }

        public RecoveredFlickActionResult TryFlick(
            in RecoveredInputEntity input,
            RecoveredNotationNote note)
        {
            if (note == null ||
                note.NoteType != (int)RecoveredNoteType.Flick ||
                !_noteManager.Contains(note) ||
                !_inputManager.TrySetBeganTime(input, out var beganInputMilliseconds) ||
                !RecoveredFlickTimingDecider.IsBeganRangeTarget(
                    _clock, note, beganInputMilliseconds))
            {
                return default;
            }

            RecoveredTimingDecision timing;
            RecoveredFlickCompletionReason reason;
            if (RecoveredFlickTimingDecider.IsExpired(
                    _clock.PassedGameMilliseconds,
                    beganInputMilliseconds,
                    _flickExpiredMilliseconds))
            {
                timing = GreatDecision();
                reason = RecoveredFlickCompletionReason.Expired;
            }
            else if (input.Phase == RecoveredTouchPhase.Moved)
            {
                if (input.DeltaPosition.sqrMagnitude <= _flickDistance)
                {
                    return HandledWithoutCompletion(note);
                }
                timing = RecoveredFlickTimingDecider.Decide(
                    _clock, input.Milliseconds, note);
                if (timing.TimingType == RecoveredTimingType.None)
                {
                    return HandledWithoutCompletion(note);
                }
                reason = RecoveredFlickCompletionReason.Moved;
            }
            else if (RecoveredInputLifecycle.IsEnded(input.Phase))
            {
                timing = GreatDecision();
                reason = RecoveredFlickCompletionReason.Released;
            }
            else
            {
                // InputAction.TryFlick returns true once the input belongs to a
                // valid Flick, even when TryFlickCore has not completed it yet.
                // isDeletedNote remains false, so InputHandler consumes this
                // touch occurrence without deleting the candidate.
                return HandledWithoutCompletion(note);
            }

            var deleted = _noteManager.DeleteFlickNote(note);
            _inputManager.Remove(input.TouchId);
            return new RecoveredFlickActionResult(
                true, true, deleted, note, timing, reason);
        }

        private static RecoveredFlickActionResult HandledWithoutCompletion(
            RecoveredNotationNote note) =>
            new RecoveredFlickActionResult(
                true,
                false,
                false,
                note,
                default,
                RecoveredFlickCompletionReason.None);

        private static RecoveredTimingDecision GreatDecision() =>
            new RecoveredTimingDecision(
                RecoveredTimingType.Great,
                RecoveredTimingAssistType.None,
                0);
    }
}
