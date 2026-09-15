using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenWDS.Runtime
{
    public enum FlickCompletionReason
    {
        None = 0,
        Moved = 1,
        Expired = 2,
        Released = 3,
    }

    public readonly struct FlickActionResult
    {
        public readonly bool Handled;
        public readonly bool Consumed;
        public readonly bool DeletedNote;
        public readonly NotationNote Note;
        public readonly TimingDecision Timing;
        public readonly FlickCompletionReason CompletionReason;

        public FlickActionResult(
            bool handled,
            bool consumed,
            bool deletedNote,
            NotationNote note,
            TimingDecision timing,
            FlickCompletionReason completionReason)
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
    public static class FlickTimingDecider
    {
        public const long PerfectStarMilliseconds = 70;
        public const long GreatMilliseconds = 100;

        public static TimingDecision DecideMusicTime(
            long inputMusicMilliseconds,
            long noteMusicMilliseconds)
        {
            var diff = inputMusicMilliseconds - noteMusicMilliseconds;
            var absolute = diff < 0 ? -diff : diff;
            var timing = absolute <= PerfectStarMilliseconds
                ? TimingType.PerfectStar
                : absolute <= GreatMilliseconds
                    ? TimingType.Great
                    : TimingType.None;
            return new TimingDecision(
                timing, TimingAssistType.None, 0);
        }

        public static TimingDecision Decide(
            GameClock clock,
            long inputGameMilliseconds,
            NotationNote note)
        {
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            if (note == null) throw new ArgumentNullException(nameof(note));
            return DecideMusicTime(
                clock.InputTimeToMusicMilliseconds(inputGameMilliseconds),
                note.StartMilliseconds);
        }

        public static bool IsBeganRangeTarget(
            GameClock clock,
            NotationNote note,
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

    public sealed class FlickNoteManager
    {
        private readonly List<NotationNote> _notes =
            new List<NotationNote>();

        public int Count => _notes.Count;
        public IReadOnlyList<NotationNote> Notes => _notes;

        public FlickNoteManager(IReadOnlyList<NotationNote> notation)
        {
            if (notation == null) throw new ArgumentNullException(nameof(notation));
            foreach (var note in notation)
            {
                if (note.NoteType == (int)NoteType.Flick)
                {
                    _notes.Add(note);
                }
            }
        }

        public bool Contains(NotationNote note) =>
            note != null && _notes.Contains(note);

        public bool DeleteFlickNote(NotationNote note) =>
            note != null && _notes.Remove(note);
    }

    /// <summary>
    /// InputAction.TryFlick/TryFlickCore. The machine code compares
    /// DeltaPosition.sqrMagnitude directly against FlickDistance without squaring
    /// the serialized config value.
    /// </summary>
    public sealed class FlickAction
    {
        private readonly GameClock _clock;
        private readonly FlickInputManager _inputManager;
        private readonly FlickNoteManager _noteManager;
        private readonly float _flickDistance;
        private readonly long _flickExpiredMilliseconds;

        public FlickAction(
            GameClock clock,
            FlickInputManager inputManager,
            FlickNoteManager noteManager,
            float flickDistance = OriginalGameConfig.FlickDistance,
            long flickExpiredMilliseconds =
                OriginalGameConfig.FlickExpiredMilliseconds)
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

        public FlickActionResult TryFlick(
            in InputEntity input,
            NotationNote note)
        {
            if (note == null ||
                note.NoteType != (int)NoteType.Flick ||
                !_noteManager.Contains(note) ||
                !_inputManager.TrySetBeganTime(input, out var beganInputMilliseconds) ||
                !FlickTimingDecider.IsBeganRangeTarget(
                    _clock, note, beganInputMilliseconds))
            {
                return default;
            }

            TimingDecision timing;
            FlickCompletionReason reason;
            if (FlickTimingDecider.IsExpired(
                    _clock.PassedGameMilliseconds,
                    beganInputMilliseconds,
                    _flickExpiredMilliseconds))
            {
                timing = GreatDecision();
                reason = FlickCompletionReason.Expired;
            }
            else if (input.Phase == TouchPhase.Moved)
            {
                if (input.DeltaPosition.sqrMagnitude <= _flickDistance)
                {
                    return HandledWithoutCompletion(note);
                }
                timing = FlickTimingDecider.Decide(
                    _clock, input.Milliseconds, note);
                if (timing.TimingType == TimingType.None)
                {
                    return HandledWithoutCompletion(note);
                }
                reason = FlickCompletionReason.Moved;
            }
            else if (InputLifecycle.IsEnded(input.Phase))
            {
                timing = GreatDecision();
                reason = FlickCompletionReason.Released;
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
            return new FlickActionResult(
                true, true, deleted, note, timing, reason);
        }

        private static FlickActionResult HandledWithoutCompletion(
            NotationNote note) =>
            new FlickActionResult(
                true,
                false,
                false,
                note,
                default,
                FlickCompletionReason.None);

        private static TimingDecision GreatDecision() =>
            new TimingDecision(
                TimingType.Great,
                TimingAssistType.None,
                0);
    }
}
