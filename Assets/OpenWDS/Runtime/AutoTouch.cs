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
    public sealed class AutoTouch
    {
        private readonly GameClock _clock;
        private readonly Func<NotationNote, Vector2> _getScreenPosition;
        private readonly List<InputEntity> _preTouches;
        private readonly Dictionary<int, InputEntity> _holdings =
            new Dictionary<int, InputEntity>(1000);
        private int _lastIndex;

        public int ScheduledEventCount => _preTouches.Count;
        public IReadOnlyList<InputEntity> ScheduledEvents => _preTouches;
        public int ActiveHoldingCount => _holdings.Count;
        public bool IsFlickTouchId(int touchId) => _flickTouchIds.Contains(touchId);

        private readonly HashSet<int> _flickTouchIds = new HashSet<int>();

        public AutoTouch(
            GameClock clock,
            IReadOnlyList<NotationNote> notations,
            Func<NotationNote, Vector2> getScreenPosition)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (notations == null) throw new ArgumentNullException(nameof(notations));
            _getScreenPosition = getScreenPosition ??
                                 throw new ArgumentNullException(nameof(getScreenPosition));
            _preTouches = CreateSchedule(notations);
        }

        public void GetTouches(List<InputEntity> touches)
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
                if (scheduled.Phase == TouchPhase.Began ||
                     scheduled.Phase == TouchPhase.Moved ||
                     scheduled.Phase == TouchPhase.Stationary)
                {
                    _holdings[scheduled.TouchId] = scheduled;
                }
                else if (InputLifecycle.IsEnded(scheduled.Phase))
                {
                    _holdings.Remove(scheduled.TouchId);
                }
            }

            // Original AutoTouch emits a fresh Stationary input every frame for holds.
            foreach (var pair in _holdings)
            {
                var holding = pair.Value;
                if (holding.Milliseconds >= passedMusicMilliseconds) continue;
                touches.Add(new InputEntity(
                    holding.TouchId,
                    _clock.MusicTimeToInputMilliseconds(passedMusicMilliseconds),
                    holding.StartScreenPosition,
                    holding.ScreenPosition,
                    Vector2.zero,
                    TouchPhase.Stationary));
            }
            InputOrdering.SortLikeInputHandler(touches);
        }

        public void Reset()
        {
            _lastIndex = 0;
            _holdings.Clear();
        }

        private List<InputEntity> CreateSchedule(
            IReadOnlyList<NotationNote> notations)
        {
            var result = new List<InputEntity>();
            // AutoTouch.Initialize uses OrderBy(StartMilliseconds) followed by
            // ThenByDescending(noteType == Scratch). LINQ ordering is stable,
            // which matters when simultaneous wide notes overlap on sub lanes.
            foreach (var note in notations
                         .OrderBy(value => value.StartMilliseconds)
                         .ThenByDescending(value =>
                             value.NoteType == (int)NoteType.Scratch))
            {
                var type = (NoteType)note.NoteType;
                if (type == NoteType.Normal ||
                    type == NoteType.Critical ||
                    type == NoteType.BlueTap)
                {
                    AddTap(result, note);
                }
                else if (type == NoteType.Hold ||
                         type == NoteType.CriticalHold)
                {
                    // Initialize ignores 80/81: the paired 100/101 body owns
                    // the actual TouchId and creates the hold events.
                    AddHold(result, note);
                }
                else if (type == NoteType.Flick)
                {
                    AddFlick(result, note);
                }
                else if (type == NoteType.Scratch)
                {
                    AddScratch(result, notations, note);
                }
                else if (type == NoteType.ScratchHold ||
                         type == NoteType.ScratchCriticalHold)
                {
                    AddScratchHold(result, notations, note);
                }
            }
            // The original finishes with Enumerable.OrderBy<InputEntity, long>.
            // Keep equal-time events in the notation order established above.
            return result.OrderBy(value => value.Milliseconds).ToList();
        }

        private void AddTap(List<InputEntity> result, NotationNote note)
        {
            var position = _getScreenPosition(note);
            result.Add(Create(note.Id, note.StartMilliseconds, position, TouchPhase.Began));
            result.Add(Create(note.Id, note.StartMilliseconds + 1, position, TouchPhase.Ended));
        }

        private void AddHold(
            List<InputEntity> result,
            NotationNote body)
        {
            var startPosition = _getScreenPosition(body);
            var endPosition = _getScreenPosition(body);
            result.Add(Create(body.Id, body.StartMilliseconds, startPosition,
                TouchPhase.Began));
            // Retail emits an explicit tail sample before releasing. A frame
            // may cross both the tail and Ended; it must still judge Holding
            // notes while contact exists, before Ended removes the dictionary entry.
            result.Add(Create(body.Id, body.EndMilliseconds, endPosition,
                TouchPhase.Stationary));
            result.Add(new InputEntity(
                body.Id,
                body.EndMilliseconds + 1,
                startPosition,
                endPosition,
                endPosition - startPosition,
                TouchPhase.Ended));
        }

        private void AddFlick(
            List<InputEntity> result,
            NotationNote note)
        {
            var position = _getScreenPosition(note);
            _flickTouchIds.Add(note.Id);
            // AutoTouch.Initialize emits a lead Began followed by the note-time
            // Moved input. Its gesture vector is (FlickDistance, FlickDistance).
            result.Add(Create(note.Id, note.StartMilliseconds - 1, position,
                TouchPhase.Began));
            result.Add(new InputEntity(
                note.Id,
                note.StartMilliseconds,
                position,
                position,
                new Vector2(
                    OriginalGameConfig.FlickDistance,
                    OriginalGameConfig.FlickDistance),
                TouchPhase.Moved));
            result.Add(Create(note.Id, note.StartMilliseconds + 1, position,
                TouchPhase.Ended));
        }

        private void AddScratch(
            List<InputEntity> result,
            IReadOnlyList<NotationNote> notations,
            NotationNote note)
        {
            var position = _getScreenPosition(note);
            // AutoTouch.Initialize emits one note-time Moved input for type 40.
            // Its delta uses the constructor's _deltaPositionForScratch vector.
            result.Add(new InputEntity(
                GetScratchTouchId(notations, note),
                note.StartMilliseconds,
                position,
                position,
                new Vector2(
                    OriginalGameConfig.ScratchDistance,
                    OriginalGameConfig.ScratchDistance),
                TouchPhase.Moved));
        }

        private void AddScratchHold(
            List<InputEntity> result,
            IReadOnlyList<NotationNote> notations,
            NotationNote note)
        {
            var hasPrevious = JumpScratch.ExistsConnectedPrevious(
                notations, note);
            var first = hasPrevious
                ? JumpScratch.GetConnectedFirst(notations, note)
                : note;
            var touchId = first.Id;
            // Retail Initialize chooses the jump destination for every tail
            // Moved, including chain heads; Began/terminal Ended use the body
            // lane. Otherwise Stationary stays on the old lane across the next
            // long segment and its type-900 pulses are never touched.
            var startPosition = _getScreenPosition(note);
            var position = JumpScratch.IsJumpScratch(note)
                ? GetScreenPositionForLane(
                    note, JumpScratch.GetDestinationLane(note))
                : _getScreenPosition(note);

            if (!hasPrevious)
            {
                // TapNotationNoteQueue accepts 82/83. The ScratchHold chain head
                // begins at StartMilliseconds. AutoTouch.Initialize constructs
                // the Scratch Moved separately from EndMilliseconds below.
                result.Add(Create(
                    touchId,
                    note.StartMilliseconds,
                    startPosition,
                    TouchPhase.Began));
            }
            // The original native construction reads EndMilliseconds for every
            // 110/111 Moved event, including a chain head. Conflating this with
            // the head's Start-time Began delays its judgment until the next
            // connected segment and incorrectly produces GREAT.
            result.Add(new InputEntity(
                touchId,
                note.EndMilliseconds,
                position,
                position,
                new Vector2(
                    OriginalGameConfig.ScratchDistance,
                    OriginalGameConfig.ScratchDistance),
                TouchPhase.Moved));

            var hasNext = JumpScratch.ExistsConnectedNext(notations, note);

            // Initialize emits Ended for every terminal ScratchHold. A chain
            // head with a successor stays active, while a standalone head is
            // itself terminal and ends after its own end judgment.
            if (!hasNext)
            {
                result.Add(Create(
                    touchId,
                    note.EndMilliseconds + 1,
                    startPosition,
                    TouchPhase.Ended));
            }
        }

        private static int GetScratchTouchId(
            IReadOnlyList<NotationNote> notations, NotationNote note)
        {
            // GetConnectedFirstScratchNote first locates the enclosing 110/111
            // with the same EndLane, then walks backwards through jump spans.
            var first = notations.FirstOrDefault(n =>
                (n.NoteType == 110 || n.NoteType == 111) &&
                n.StartMilliseconds <= note.StartMilliseconds &&
                note.StartMilliseconds <= n.EndMilliseconds && n.EndLane == note.EndLane);
            if (first == null) return note.Id;
            var visited = new HashSet<int>();
            while (visited.Add(first.Id))
            {
                var previous = notations.FirstOrDefault(n =>
                    JumpScratch.IsJumpScratch(n) &&
                    n.EndMilliseconds == first.StartMilliseconds &&
                    JumpScratch.GetLaneRange(n).Contains(note.Lane));
                if (previous == null) break;
                first = previous;
            }
            return first.Id;
        }

        private Vector2 GetScreenPositionForLane(
            NotationNote source,
            int lane)
        {
            // The original helper indexes LaneGroup by the destination lane and
            // then calls Camera.WorldToScreenPoint. The adapter's callback is
            // notation-based, so a lane-only proxy preserves that exact lookup.
            return _getScreenPosition(new NotationNote
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

        private static InputEntity Create(
            int touchId,
            long milliseconds,
            Vector2 position,
            TouchPhase phase)
        {
            return new InputEntity(
                touchId, milliseconds, position, position, Vector2.zero, phase);
        }

        private static InputEntity ConvertTime(
            InputEntity input,
            long milliseconds)
        {
            return new InputEntity(
                input.TouchId,
                milliseconds,
                input.StartScreenPosition,
                input.ScreenPosition,
                input.DeltaPosition,
                input.Phase);
        }
    }
}
