using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using InputSystemTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using InputSystemTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Stateful DefaultTouch provider. Kept separate from MonoBehaviour so the
    /// same EnhancedTouch conversion can be driven by injected TouchState events.
    /// </summary>
    public sealed class DefaultTouchProvider
    {
        private const long BeganNoiseMilliseconds = 40L;
        private readonly Dictionary<int, double> _lastRecordTimes =
            new Dictionary<int, double>(15);
        private readonly Dictionary<int, long> _lastBeganMilliseconds =
            new Dictionary<int, long>(15);
        private readonly HashSet<int> _blockedTouchIds =
            new HashSet<int>();

        public void GetTouches(
            List<InputEntity> touches,
            long currentGameMilliseconds,
            Func<double, long> convertRealtimeToGameMilliseconds)
        {
            if (touches == null) throw new ArgumentNullException(nameof(touches));
            if (convertRealtimeToGameMilliseconds == null)
                throw new ArgumentNullException(
                    nameof(convertRealtimeToGameMilliseconds));

            var activeTouches = InputSystemTouch.activeTouches;
            for (var touchIndex = 0;
                 touchIndex < activeTouches.Count;
                 touchIndex++)
            {
                var touch = activeTouches[touchIndex];
                if (!touch.valid) continue;
                if (_blockedTouchIds.Contains(touch.touchId))
                {
                    if (touch.phase == InputSystemTouchPhase.Ended ||
                        touch.phase == InputSystemTouchPhase.Canceled)
                    {
                        _blockedTouchIds.Remove(touch.touchId);
                    }
                    continue;
                }

                var lastTime = _lastRecordTimes.TryGetValue(
                    touch.touchId, out var recordedTime)
                    ? recordedTime
                    : double.NegativeInfinity;
                var history = touch.history;

                // TouchHistory indexes newest first. Replay oldest first so fast
                // Began/Moved/Ended sequences keep their device event order.
                // New records form a prefix; stop as soon as the first already
                // processed timestamp is reached instead of walking the entire
                // retained history on every 120 Hz frame.
                var newHistoryCount = 0;
                while (newHistoryCount < history.Count)
                {
                    var record = history[newHistoryCount];
                    if (!record.valid || record.time <= lastTime) break;
                    newHistoryCount++;
                }
                for (var historyIndex = newHistoryCount - 1;
                     historyIndex >= 0;
                     historyIndex--)
                {
                    var record = history[historyIndex];
                    AddTouch(
                        touches,
                        record,
                        convertRealtimeToGameMilliseconds(record.time));
                    lastTime = Math.Max(lastTime, record.time);
                }

                // Input System preserves the last event time for Stationary.
                // DefaultTouch nevertheless supplies Stationary every tick.
                if (touch.phase == InputSystemTouchPhase.Stationary)
                {
                    AddTouch(touches, touch, currentGameMilliseconds);
                }
                else if (touch.time > lastTime)
                {
                    AddTouch(
                        touches,
                        touch,
                        convertRealtimeToGameMilliseconds(touch.time));
                    lastTime = touch.time;
                }

                if (touch.phase == InputSystemTouchPhase.Ended ||
                    touch.phase == InputSystemTouchPhase.Canceled)
                {
                    _lastRecordTimes.Remove(touch.touchId);
                    _lastBeganMilliseconds.Remove(touch.touchId);
                }
                else
                {
                    _lastRecordTimes[touch.touchId] = lastTime;
                }
            }
        }

        public void Reset()
        {
            _lastRecordTimes.Clear();
            _lastBeganMilliseconds.Clear();
            _blockedTouchIds.Clear();
        }

        /// <summary>
        /// Drops fingers that were already down when gameplay input resumed.
        /// Their Ended/Canceled record only releases the block; a later Began
        /// may safely reuse the same platform TouchId.
        /// </summary>
        public void IgnoreActiveTouchesUntilEnded()
        {
            _lastRecordTimes.Clear();
            _lastBeganMilliseconds.Clear();
            _blockedTouchIds.Clear();

            var activeTouches = InputSystemTouch.activeTouches;
            for (var index = 0; index < activeTouches.Count; index++)
            {
                var touch = activeTouches[index];
                if (!touch.valid ||
                    touch.phase == InputSystemTouchPhase.Ended ||
                    touch.phase == InputSystemTouchPhase.Canceled)
                {
                    continue;
                }
                _blockedTouchIds.Add(touch.touchId);
            }
        }

        private void AddTouch(
            List<InputEntity> touches,
            InputSystemTouch touch,
            long milliseconds)
        {
            var phase = (TouchPhase)(int)touch.phase;
            if (!PassesBeganNoiseFilter(
                    touch.touchId, milliseconds, phase))
            {
                return;
            }
            touches.Add(new InputEntity(
                touch.touchId,
                milliseconds,
                touch.startScreenPosition,
                touch.screenPosition,
                touch.delta,
                phase));
        }

        private bool PassesBeganNoiseFilter(
            int touchId,
            long milliseconds,
            TouchPhase phase)
        {
            if (phase == TouchPhase.Began)
            {
                if (_lastBeganMilliseconds.TryGetValue(
                        touchId, out var previous) &&
                    milliseconds <= previous + BeganNoiseMilliseconds)
                {
                    return false;
                }
                _lastBeganMilliseconds[touchId] = milliseconds;
            }
            else if (phase == TouchPhase.Ended ||
                     phase == TouchPhase.Canceled)
            {
                _lastBeganMilliseconds.Remove(touchId);
            }
            return true;
        }
    }

    /// <summary>
    /// Device-input adapter recovered from DefaultTouch. EnhancedTouch records
    /// enter the same InputHandler.Fire path used by AutoTouch.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class DefaultTouchRuntime : MonoBehaviour
    {
        private readonly DefaultTouchProvider _provider =
            new DefaultTouchProvider();
        private readonly List<InputEntity> _touches =
            new List<InputEntity>(32);
        private GameRuntime _runtime;
        private bool _enhancedTouchEnabledHere;
        private bool _inputSuspended;

        public void Configure(GameRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _inputSuspended = runtime.IsPaused;
            if (_inputSuspended)
                _provider.Reset();
        }

        public void SetInputSuspended(bool suspended)
        {
            _touches.Clear();
            if (suspended)
            {
                _inputSuspended = true;
                _provider.Reset();
                return;
            }

            // DefaultTouch is reset while the game state is not ticking, then
            // EnhancedTouch history is read again on resume. A touch that began
            // during the countdown must therefore be delivered and allowed to
            // reacquire an in-progress Hold.
            _provider.Reset();
            _inputSuspended = false;
        }

        private void OnEnable()
        {
            if (EnhancedTouchSupport.enabled) return;
            EnhancedTouchSupport.Enable();
            _enhancedTouchEnabledHere = true;
        }

        private void OnDisable()
        {
            _provider.Reset();
            _touches.Clear();
            if (!_enhancedTouchEnabledHere) return;
            EnhancedTouchSupport.Disable();
            _enhancedTouchEnabledHere = false;
        }

        private void Update()
        {
            if (_inputSuspended || _runtime == null ||
                _runtime.IsAutoJudgeEnabled ||
                !_runtime.IsGameplayStarted || _runtime.IsPaused ||
                _runtime.IsRetired || _runtime.IsResultShown)
            {
                return;
            }

            _touches.Clear();
            _provider.GetTouches(
                _touches,
                _runtime.CurrentPlayerInputMilliseconds,
                _runtime.ToPlayerInputMilliseconds);
            for (var index = 0; index < _touches.Count; index++)
                _runtime.SubmitPlayerInput(_touches[index]);
        }
    }
}
