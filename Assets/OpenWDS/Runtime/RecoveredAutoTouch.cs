using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Recovered AutoTouch adapter for Tap, Hold, Flick, Scratch, and the
    /// ScratchHold/Jump-Scratch chain. Generated
    /// InputEntity values still pass through the normal raycast and judgement
    /// pipeline.
    /// </summary>
    public sealed class RecoveredAutoTouch
    {
        private readonly RecoveredGameClock _clock;
        private readonly Func<RecoveredNotationNote, Vector2> _getScreenPosition;
        private readonly List<RecoveredInputEntity> _preTouches;
        private readonly HashSet<int> _holdTouchIds = new HashSet<int>();
        private readonly Dictionary<int, RecoveredInputEntity> _holdings =
            new Dictionary<int, RecoveredInputEntity>(1000);
        private int _lastIndex;

        public int ScheduledEventCount => _preTouches.Count;
        public IReadOnlyList<RecoveredInputEntity> ScheduledEvents => _preTouches;
        public int ActiveHoldingCount => _holdings.Count;
        public bool IsFlickTouchId(int touchId) => _flickTouchIds.Contains(touchId);

        private readonly HashSet<int> _flickTouchIds = new HashSet<int>();

        public RecoveredAutoTouch(
            RecoveredGameClock clock,
            IReadOnlyList<RecoveredNotationNote> notations,
            Func<RecoveredNotationNote, Vector2> getScreenPosition)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (notations == null) throw new ArgumentNullException(nameof(notations));
            _getScreenPosition = getScreenPosition ??
                                 throw new ArgumentNullException(nameof(getScreenPosition));
            _preTouches = CreateSchedule(notations);
        }

        public void GetTouches(List<RecoveredInputEntity> touches)
        {
            if (touches == null) throw new ArgumentNullException(nameof(touches));
            var passedMusicMilliseconds = _clock.PassedMilliseconds;
            while (_lastIndex < _preTouches.Count &&
                   _preTouches[_lastIndex].Milliseconds <= passedMusicMilliseconds)
            {
                var scheduled = _preTouches[_lastIndex++];
                var converted = ConvertTime(
                    scheduled,
                    _clock.MusicTimeToInputMilliseconds(scheduled.Milliseconds));
                touches.Add(converted);
                if (_holdTouchIds.Contains(scheduled.TouchId) &&
                    (scheduled.Phase == RecoveredTouchPhase.Began ||
                     scheduled.Phase == RecoveredTouchPhase.Moved ||
                     scheduled.Phase == RecoveredTouchPhase.Stationary))
                {
                    _holdings[scheduled.TouchId] = scheduled;
                }
                else if (RecoveredInputLifecycle.IsEnded(scheduled.Phase))
                {
                    _holdings.Remove(scheduled.TouchId);
                }
            }

            // Original AutoTouch emits a fresh Stationary input every frame for holds.
            foreach (var pair in _holdings)
            {
                var holding = pair.Value;
                touches.Add(new RecoveredInputEntity(
                    holding.TouchId,
                    _clock.MusicTimeToInputMilliseconds(passedMusicMilliseconds),
                    holding.StartScreenPosition,
                    holding.ScreenPosition,
                    Vector2.zero,
                    RecoveredTouchPhase.Stationary));
            }
            RecoveredInputOrdering.SortLikeInputHandler(touches);
        }

        public void Reset()
        {
            _lastIndex = 0;
            _holdings.Clear();
        }

        private List<RecoveredInputEntity> CreateSchedule(
            IReadOnlyList<RecoveredNotationNote> notations)
        {
            var result = new List<RecoveredInputEntity>();
            // AutoTouch.Initialize uses OrderBy(StartMilliseconds) followed by
            // ThenByDescending(noteType == Scratch). LINQ ordering is stable,
            // which matters when simultaneous wide notes overlap on sub lanes.
            foreach (var note in notations
                         .OrderBy(value => value.StartMilliseconds)
                         .ThenByDescending(value =>
                             value.NoteType == (int)RecoveredNoteType.Scratch))
            {
                var type = (RecoveredNoteType)note.NoteType;
                if (type == RecoveredNoteType.Normal ||
                    type == RecoveredNoteType.Critical ||
                    type == RecoveredNoteType.BlueTap)
                {
                    AddTap(result, note);
                }
                else if (type == RecoveredNoteType.Hold ||
                         type == RecoveredNoteType.CriticalHold)
                {
                    // Initialize ignores 80/81: the paired 100/101 body owns
                    // the actual TouchId and creates the hold events.
                    AddHold(result, note);
                }
                else if (type == RecoveredNoteType.Flick)
                {
                    AddFlick(result, note);
                }
                else if (type == RecoveredNoteType.Scratch)
                {
                    AddScratch(result, note);
                }
                else if (type == RecoveredNoteType.ScratchHold ||
                         type == RecoveredNoteType.ScratchCriticalHold)
                {
                    AddScratchHold(result, notations, note);
                }
            }
            // The original finishes with Enumerable.OrderBy<InputEntity, long>.
            // Keep equal-time events in the notation order established above.
            return result.OrderBy(value => value.Milliseconds).ToList();
        }

        private void AddTap(List<RecoveredInputEntity> result, RecoveredNotationNote note)
        {
            var position = _getScreenPosition(note);
            result.Add(Create(note.Id, note.StartMilliseconds, position, RecoveredTouchPhase.Began));
            result.Add(Create(note.Id, note.StartMilliseconds, position, RecoveredTouchPhase.Ended));
        }

        private void AddHold(
            List<RecoveredInputEntity> result,
            RecoveredNotationNote body)
        {
            var startPosition = _getScreenPosition(body);
            var endPosition = _getScreenPosition(body);
            _holdTouchIds.Add(body.Id);
            result.Add(Create(body.Id, body.StartMilliseconds, startPosition,
                RecoveredTouchPhase.Began));
            result.Add(new RecoveredInputEntity(
                body.Id,
                body.EndMilliseconds + 1,
                startPosition,
                endPosition,
                endPosition - startPosition,
                RecoveredTouchPhase.Ended));
        }

        private void AddFlick(
            List<RecoveredInputEntity> result,
            RecoveredNotationNote note)
        {
            var position = _getScreenPosition(note);
            _flickTouchIds.Add(note.Id);
            // AutoTouch.Initialize emits a lead Began followed by the note-time
            // Moved input. Its gesture vector is (FlickDistance, FlickDistance).
            result.Add(Create(note.Id, note.StartMilliseconds - 1, position,
                RecoveredTouchPhase.Began));
            result.Add(new RecoveredInputEntity(
                note.Id,
                note.StartMilliseconds,
                position,
                position,
                new Vector2(
                    RecoveredOriginalGameConfig.FlickDistance,
                    RecoveredOriginalGameConfig.FlickDistance),
                RecoveredTouchPhase.Moved));
        }

        private void AddScratch(
            List<RecoveredInputEntity> result,
            RecoveredNotationNote note)
        {
            var position = _getScreenPosition(note);
            // AutoTouch.Initialize emits one note-time Moved input for type 40.
            // Its delta uses the constructor's _deltaPositionForScratch vector.
            result.Add(new RecoveredInputEntity(
                note.Id,
                note.StartMilliseconds,
                position,
                position,
                new Vector2(
                    RecoveredOriginalGameConfig.ScratchDistance,
                    RecoveredOriginalGameConfig.ScratchDistance),
                RecoveredTouchPhase.Moved));
        }

        private void AddScratchHold(
            List<RecoveredInputEntity> result,
            IReadOnlyList<RecoveredNotationNote> notations,
            RecoveredNotationNote note)
        {
            var hasPrevious = RecoveredJumpScratch.ExistsConnectedPrevious(
                notations, note);
            var first = hasPrevious
                ? RecoveredJumpScratch.GetConnectedFirst(notations, note)
                : note;
            var touchId = first.Id;
            var position = hasPrevious && RecoveredJumpScratch.IsJumpScratch(note)
                ? GetScreenPositionForLane(
                    note, RecoveredJumpScratch.GetDestinationLane(note))
                : _getScreenPosition(note);

            _holdTouchIds.Add(touchId);
            if (!hasPrevious)
            {
                // TapNotationNoteQueue accepts 82/83. The ScratchHold chain head
                // begins at StartMilliseconds. AutoTouch.Initialize constructs
                // the Scratch Moved separately from EndMilliseconds below.
                result.Add(Create(
                    touchId,
                    note.StartMilliseconds,
                    position,
                    RecoveredTouchPhase.Began));
            }
            // The original native construction reads EndMilliseconds for every
            // 110/111 Moved event, including a chain head. Conflating this with
            // the head's Start-time Began delays its judgment until the next
            // connected segment and incorrectly produces GREAT.
            result.Add(new RecoveredInputEntity(
                touchId,
                note.EndMilliseconds,
                position,
                position,
                new Vector2(
                    RecoveredOriginalGameConfig.ScratchDistance,
                    RecoveredOriginalGameConfig.ScratchDistance),
                RecoveredTouchPhase.Moved));

            var hasNext = RecoveredJumpScratch.ExistsConnectedNext(notations, note);

            // Initialize emits Ended for every terminal ScratchHold. A chain
            // head with a successor stays active, while a standalone head is
            // itself terminal and ends after its own end judgment.
            if (!hasNext)
            {
                result.Add(Create(
                    touchId,
                    note.EndMilliseconds + 1,
                    position,
                    RecoveredTouchPhase.Ended));
            }
        }

        private Vector2 GetScreenPositionForLane(
            RecoveredNotationNote source,
            int lane)
        {
            // The original helper indexes LaneGroup by the destination lane and
            // then calls Camera.WorldToScreenPoint. The adapter's callback is
            // notation-based, so a lane-only proxy preserves that exact lookup.
            return _getScreenPosition(new RecoveredNotationNote
            {
                Id = source.Id,
                StartTickCount = source.StartTickCount,
                EndTickCount = source.EndTickCount,
                NoteType = source.NoteType,
                Lane = lane,
                Width = 1,
                GimmickType = source.GimmickType,
                GimmickValue = source.GimmickValue,
            });
        }

        private static RecoveredInputEntity Create(
            int touchId,
            long milliseconds,
            Vector2 position,
            RecoveredTouchPhase phase)
        {
            return new RecoveredInputEntity(
                touchId, milliseconds, position, position, Vector2.zero, phase);
        }

        private static RecoveredInputEntity ConvertTime(
            RecoveredInputEntity input,
            long milliseconds)
        {
            return new RecoveredInputEntity(
                input.TouchId,
                milliseconds,
                input.StartScreenPosition,
                input.ScreenPosition,
                input.DeltaPosition,
                input.Phase);
        }
    }
}
