using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace OpenWDS.Runtime
{
    public enum NoteType
    {
        None = 0,
        Normal = 10,
        Critical = 20,
        Sound = 30,
        SoundPurple = 31,
        Scratch = 40,
        Flick = 50,
        HoldStart = 80,
        CriticalHoldStart = 81,
        ScratchHoldStart = 82,
        ScratchCriticalHoldStart = 83,
        Hold = 100,
        CriticalHold = 101,
        ScratchHold = 110,
        ScratchCriticalHold = 111,
        BlueTap = 200,
        HoldEighth = 900,
    }

    // Numeric values match UnityEngine.InputSystem.TouchPhase in the recovered player.
    public enum TouchPhase
    {
        None = 0,
        Began = 1,
        Moved = 2,
        Ended = 3,
        Canceled = 4,
        Stationary = 5,
    }

    public readonly struct InputEntity : IEquatable<InputEntity>
    {
        public readonly int TouchId;
        public readonly long Milliseconds;
        public readonly Vector2 StartScreenPosition;
        public readonly Vector2 ScreenPosition;
        public readonly Vector2 DeltaPosition;
        public readonly TouchPhase Phase;

        public InputEntity(
            int touchId,
            long milliseconds,
            Vector2 startScreenPosition,
            Vector2 screenPosition,
            Vector2 deltaPosition,
            TouchPhase phase)
        {
            TouchId = touchId;
            Milliseconds = milliseconds;
            StartScreenPosition = startScreenPosition;
            ScreenPosition = screenPosition;
            DeltaPosition = deltaPosition;
            Phase = phase;
        }

        // InputEntity equality and hash code use TouchId only.
        public bool Equals(InputEntity other) => TouchId == other.TouchId;
        public override bool Equals(object obj) =>
            obj is InputEntity other && Equals(other);
        public override int GetHashCode() => TouchId;
    }

    public readonly struct HitLaneEntity : IEquatable<HitLaneEntity>
    {
        private readonly int _uniqueId;

        public readonly bool Exists;
        public readonly int HitMainLaneId;
        public readonly int HitSubLeftInnerLaneId;
        public readonly int HitSubRightInnerLaneId;
        public readonly int HitSubLeftOuterLaneId;
        public readonly int HitSubRightOuterLaneId;

        public HitLaneEntity(
            int hitMainLaneId,
            int hitSubLeftInnerLaneId,
            int hitSubRightInnerLaneId,
            int hitSubLeftOuterLaneId,
            int hitSubRightOuterLaneId)
        {
            HitMainLaneId = hitMainLaneId;
            HitSubLeftInnerLaneId = hitSubLeftInnerLaneId;
            HitSubRightInnerLaneId = hitSubRightInnerLaneId;
            HitSubLeftOuterLaneId = hitSubLeftOuterLaneId;
            HitSubRightOuterLaneId = hitSubRightOuterLaneId;
            Exists = hitMainLaneId != 0 ||
                     hitSubLeftInnerLaneId != 0 ||
                     hitSubRightInnerLaneId != 0 ||
                     hitSubLeftOuterLaneId != 0 ||
                     hitSubRightOuterLaneId != 0;
            _uniqueId = hitMainLaneId * 100000000 +
                        hitSubLeftInnerLaneId * 1000000 +
                        hitSubRightInnerLaneId * 10000 +
                        hitSubLeftOuterLaneId * 100 +
                        hitSubRightOuterLaneId;
        }

        public int GetLaneIdOrDefault()
        {
            if (HitMainLaneId != 0) return HitMainLaneId;
            if (HitSubLeftInnerLaneId != 0) return HitSubLeftInnerLaneId;
            if (HitSubRightInnerLaneId != 0) return HitSubRightInnerLaneId;
            if (HitSubLeftOuterLaneId != 0) return HitSubLeftOuterLaneId;
            return HitSubRightOuterLaneId;
        }

        public bool Equals(HitLaneEntity other) => _uniqueId == other._uniqueId;
        public override bool Equals(object obj) =>
            obj is HitLaneEntity other && Equals(other);
        public override int GetHashCode() => _uniqueId;
    }

    public static class InputOrdering
    {
        // TouchPhaseComparer order recovered at RVA 0xB945C50.
        public static int CompareTouchPhase(
            TouchPhase left,
            TouchPhase right)
        {
            if (left == right) return 0;
            if (left == TouchPhase.Began) return -1;
            if (right == TouchPhase.Began) return 1;
            if (left == TouchPhase.Moved) return -1;
            if (right == TouchPhase.Moved) return 1;
            if (left == TouchPhase.Stationary) return -1;
            if (right == TouchPhase.Stationary) return 1;
            if (left == TouchPhase.Ended) return -1;
            if (right == TouchPhase.Ended) return 1;
            return left == TouchPhase.Canceled ? -1 : 1;
        }

        public static void SortLikeInputHandler(List<InputEntity> touches)
        {
            if (touches == null) throw new ArgumentNullException(nameof(touches));

            // InputHandler.SortTouches performs these as two distinct List.Sort calls.
            touches.Sort((left, right) => CompareTouchPhase(left.Phase, right.Phase));
            touches.Sort((left, right) => left.Milliseconds.CompareTo(right.Milliseconds));
        }
    }

    public sealed class LaneHitManager
    {
        private readonly Dictionary<int, int> _history = new Dictionary<int, int>(1000);

        public int Count => _history.Count;

        public bool IsChangedHitLaneId(int laneId, int touchId)
        {
            return !_history.TryGetValue(touchId, out var previousLaneId) ||
                   previousLaneId != laneId;
        }

        public void Set(int touchId, int hitLaneId) => _history[touchId] = hitLaneId;
        public void Remove(int touchId) => _history.Remove(touchId);
        public void Reset() => _history.Clear();
    }

    public sealed class FlickInputManager
    {
        private readonly Dictionary<int, long> _startMilliseconds =
            new Dictionary<int, long>(1000);

        public int Count => _startMilliseconds.Count;

        public bool TrySetBeganTime(
            in InputEntity input,
            out long beganMilliseconds)
        {
            if (input.Phase == TouchPhase.Began)
            {
                _startMilliseconds[input.TouchId] = input.Milliseconds;
                beganMilliseconds = input.Milliseconds;
                return true;
            }

            var found = _startMilliseconds.TryGetValue(
                input.TouchId,
                out beganMilliseconds);
            if (found && (input.Phase == TouchPhase.Ended ||
                          input.Phase == TouchPhase.Canceled))
            {
                _startMilliseconds.Remove(input.TouchId);
            }
            return found;
        }

        public void Remove(int touchId) => _startMilliseconds.Remove(touchId);
    }

    public static class InputLifecycle
    {
        public static bool IsEnded(TouchPhase phase)
        {
            return phase == TouchPhase.Ended ||
                   phase == TouchPhase.Canceled;
        }

        // InputHandler.FinalizeOnFire performs this cleanup for every _endedTouches item.
        public static void FinalizeEndedTouches(
            IReadOnlyList<InputEntity> endedTouches,
            FlickInputManager flickInputs,
            LaneHitManager laneHits,
            Action<int> removeHoldLane)
        {
            if (endedTouches == null) throw new ArgumentNullException(nameof(endedTouches));
            if (flickInputs == null) throw new ArgumentNullException(nameof(flickInputs));
            if (laneHits == null) throw new ArgumentNullException(nameof(laneHits));
            if (removeHoldLane == null) throw new ArgumentNullException(nameof(removeHoldLane));

            foreach (var input in endedTouches)
            {
                flickInputs.Remove(input.TouchId);
                laneHits.Remove(input.TouchId);
                removeHoldLane(input.TouchId);
            }
        }
    }

    public enum CandidateAction
    {
        Ignore,
        Tap,
        Flick,
    }

    public readonly struct CandidateDecision
    {
        public readonly bool Consumed;
        public readonly int LaneId;
        public readonly bool DeleteCandidate;
        public readonly bool StopScanning;

        public CandidateDecision(
            bool consumed,
            int laneId,
            bool deleteCandidate = true,
            bool stopScanning = false)
        {
            Consumed = consumed;
            LaneId = laneId;
            DeleteCandidate = deleteCandidate;
            StopScanning = stopScanning;
        }
    }

    public static class InputFireCore
    {
        public static CandidateAction GetCandidateAction(int noteType)
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
                    return CandidateAction.Tap;
                case NoteType.Flick:
                    return CandidateAction.Flick;
                default:
                    return CandidateAction.Ignore;
            }
        }

        public static List<NotationNote> CreateSortedCandidates(
            IEnumerable<NotationNote> tapNotes,
            IEnumerable<NotationNote> flickNotes)
        {
            if (tapNotes == null) throw new ArgumentNullException(nameof(tapNotes));
            if (flickNotes == null) throw new ArgumentNullException(nameof(flickNotes));
            var candidates = new List<NotationNote>(1000);
            candidates.AddRange(tapNotes);
            candidates.AddRange(flickNotes);
            candidates.Sort((left, right) =>
                left.StartMilliseconds.CompareTo(right.StartMilliseconds));
            return candidates;
        }

        public static void ConsumeTapAndFlickPass(
            List<InputEntity> touches,
            List<HitLaneEntity> hitLanes,
            List<HitLaneEntity> hitLanesForBegan,
            List<NotationNote> candidates,
            LaneHitManager laneHits,
            Func<InputEntity, HitLaneEntity,
                HitLaneEntity, NotationNote,
                CandidateDecision> tryConsume,
            List<InputEntity> endedTouches)
        {
            if (touches == null || hitLanes == null || hitLanesForBegan == null ||
                candidates == null || laneHits == null || tryConsume == null ||
                endedTouches == null)
            {
                throw new ArgumentNullException("Fire pass arguments must not be null.");
            }
            if (touches.Count != hitLanes.Count || touches.Count != hitLanesForBegan.Count)
            {
                throw new ArgumentException("Touch and hit-lane lists must remain index-aligned.");
            }

            for (var touchIndex = 0; touchIndex < touches.Count;)
            {
                var input = touches[touchIndex];
                if (InputLifecycle.IsEnded(input.Phase))
                {
                    endedTouches.Add(input);
                }

                var consumedTouch = false;
                for (var noteIndex = 0; noteIndex < candidates.Count; noteIndex++)
                {
                    var notation = candidates[noteIndex];
                    if (GetCandidateAction(notation.NoteType) == CandidateAction.Ignore)
                    {
                        continue;
                    }
                    var decision = tryConsume(
                        input,
                        hitLanes[touchIndex],
                        hitLanesForBegan[touchIndex],
                        notation);
                    if (decision.StopScanning) break;
                    if (!decision.Consumed)
                    {
                        continue;
                    }

                    if (decision.DeleteCandidate)
                        candidates.RemoveAt(noteIndex);
                    laneHits.Set(input.TouchId, decision.LaneId);
                    consumedTouch = true;
                    break;
                }
                if (consumedTouch)
                {
                    // Remove this exact occurrence. AutoTouch may put Began and
                    // Moved with the same TouchId in one frame; index removal
                    // keeps the unconsumed occurrence without allocating a
                    // deleted-index list every frame.
                    touches.RemoveAt(touchIndex);
                    hitLanes.RemoveAt(touchIndex);
                    hitLanesForBegan.RemoveAt(touchIndex);
                    continue;
                }
                touchIndex++;
            }
        }

        public static int ConsumeHoldPass(
            IReadOnlyList<InputEntity> touches,
            IReadOnlyList<HitLaneEntity> hitLanes,
            LaneHitManager laneHits,
            LaneHoldManager laneHolds,
            Func<InputEntity, HitLaneEntity,
                HoldActionResult> tryHold)
        {
            if (touches == null || hitLanes == null || laneHits == null ||
                laneHolds == null || tryHold == null)
            {
                throw new ArgumentNullException("Hold pass arguments must not be null.");
            }
            if (touches.Count != hitLanes.Count)
            {
                throw new ArgumentException("Touch and hit-lane lists must remain index-aligned.");
            }

            var consumed = 0;
            for (var index = 0; index < touches.Count; index++)
            {
                var input = touches[index];
                var result = tryHold(input, hitLanes[index]);
                if (result.ReleaseCurrent)
                {
                    laneHolds.Remove(input.TouchId);
                    laneHits.Remove(input.TouchId);
                    continue;
                }
                if (!result.Assigned || result.AssignmentNote == null)
                {
                    continue;
                }
                laneHolds.Set(input.TouchId, result.AssignmentNote);
                laneHits.Set(input.TouchId, result.LaneId);
                if (result.Consumed) consumed++;
            }
            return consumed;
        }
    }

    /// <summary>
    /// Restores LaneRaycaster semantics. The Android release submits current and
    /// began points as one six-ray-per-input batch.
    /// </summary>
    public sealed class LaneRaycaster : IDisposable
    {
        public const int MainLaneLayerMask = 64;
        public const int SubLeftLaneLayerMask = 8192;
        public const int SubRightLaneLayerMask = 16384;
        public const float MaxDistance = 1000f;

        private readonly Camera _camera;
        private readonly IReadOnlyDictionary<int, int> _mainColliders;
        private readonly IReadOnlyDictionary<int, int> _subLeftInnerColliders;
        private readonly IReadOnlyDictionary<int, int> _subRightInnerColliders;
        private readonly IReadOnlyDictionary<int, int> _subLeftOuterColliders;
        private readonly IReadOnlyDictionary<int, int> _subRightOuterColliders;
        private NativeArray<RaycastCommand> _commands;
        private NativeArray<RaycastHit> _hits;

        public LaneRaycaster(
            Camera camera,
            IReadOnlyDictionary<int, int> mainColliders,
            IReadOnlyDictionary<int, int> subLeftInnerColliders,
            IReadOnlyDictionary<int, int> subRightInnerColliders,
            IReadOnlyDictionary<int, int> subLeftOuterColliders,
            IReadOnlyDictionary<int, int> subRightOuterColliders)
        {
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _mainColliders = mainColliders ?? throw new ArgumentNullException(nameof(mainColliders));
            _subLeftInnerColliders = subLeftInnerColliders ?? throw new ArgumentNullException(nameof(subLeftInnerColliders));
            _subRightInnerColliders = subRightInnerColliders ?? throw new ArgumentNullException(nameof(subRightInnerColliders));
            _subLeftOuterColliders = subLeftOuterColliders ?? throw new ArgumentNullException(nameof(subLeftOuterColliders));
            _subRightOuterColliders = subRightOuterColliders ?? throw new ArgumentNullException(nameof(subRightOuterColliders));
        }

        public void Raycast(
            IReadOnlyList<InputEntity> inputEntities,
            List<HitLaneEntity> hitLanes,
            List<HitLaneEntity> hitLanesForBegan)
        {
            if (inputEntities == null) throw new ArgumentNullException(nameof(inputEntities));
            if (hitLanes == null) throw new ArgumentNullException(nameof(hitLanes));
            if (hitLanesForBegan == null) throw new ArgumentNullException(nameof(hitLanesForBegan));

            var commandCount = inputEntities.Count * 6;
            if (commandCount == 0) return;
            EnsureBatchCapacity(commandCount);
            for (var index = 0; index < inputEntities.Count; index++)
            {
                WriteRayCommands(
                    index * 6, inputEntities[index].ScreenPosition);
                WriteRayCommands(
                    index * 6 + 3, inputEntities[index].StartScreenPosition);
            }
            var commands = _commands.GetSubArray(0, commandCount);
            var hits = _hits.GetSubArray(0, commandCount);
            RaycastCommand.ScheduleBatch(commands, hits, 1).Complete();
            for (var index = 0; index < inputEntities.Count; index++)
            {
                var hitIndex = index * 6;
                hitLanes.Add(ResolveHitInstanceIds(
                    GetColliderInstanceId(_hits[hitIndex]),
                    GetColliderInstanceId(_hits[hitIndex + 1]),
                    GetColliderInstanceId(_hits[hitIndex + 2])));
                hitLanesForBegan.Add(ResolveHitInstanceIds(
                    GetColliderInstanceId(_hits[hitIndex + 3]),
                    GetColliderInstanceId(_hits[hitIndex + 4]),
                    GetColliderInstanceId(_hits[hitIndex + 5])));
            }
        }

        public HitLaneEntity ResolveHitInstanceIds(
            int mainInstanceId,
            int subLeftInstanceId,
            int subRightInstanceId)
        {
            return new HitLaneEntity(
                GetLaneId(_mainColliders, mainInstanceId),
                GetLaneId(_subLeftInnerColliders, subLeftInstanceId),
                GetLaneId(_subRightInnerColliders, subRightInstanceId),
                GetLaneId(_subLeftOuterColliders, subLeftInstanceId),
                GetLaneId(_subRightOuterColliders, subRightInstanceId));
        }

        public HitLaneEntity RaycastPoint(Vector2 screenPosition)
        {
            var ray = _camera.ScreenPointToRay(new Vector3(screenPosition.x, screenPosition.y, 0f));
            return ResolveHitInstanceIds(
                RaycastInstanceId(ray, MainLaneLayerMask),
                RaycastInstanceId(ray, SubLeftLaneLayerMask),
                RaycastInstanceId(ray, SubRightLaneLayerMask));
        }

        public void Dispose()
        {
            if (_commands.IsCreated) _commands.Dispose();
            if (_hits.IsCreated) _hits.Dispose();
        }

        private void EnsureBatchCapacity(int commandCount)
        {
            if (_commands.IsCreated && _commands.Length >= commandCount) return;
            Dispose();
            var capacity = Math.Max(96, commandCount);
            _commands = new NativeArray<RaycastCommand>(
                capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _hits = new NativeArray<RaycastHit>(
                capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        private void WriteRayCommands(int commandIndex, Vector2 screenPosition)
        {
            var ray = _camera.ScreenPointToRay(
                new Vector3(screenPosition.x, screenPosition.y, 0f));
            _commands[commandIndex] = new RaycastCommand(
                ray.origin, ray.direction, MaxDistance,
                MainLaneLayerMask, 1);
            _commands[commandIndex + 1] = new RaycastCommand(
                ray.origin, ray.direction, MaxDistance,
                SubLeftLaneLayerMask, 1);
            _commands[commandIndex + 2] = new RaycastCommand(
                ray.origin, ray.direction, MaxDistance,
                SubRightLaneLayerMask, 1);
        }

        private static int GetColliderInstanceId(in RaycastHit hit)
        {
            return hit.collider != null ? hit.collider.GetInstanceID() : 0;
        }

        private static int RaycastInstanceId(Ray ray, int layerMask)
        {
            return Physics.Raycast(ray, out var hit, MaxDistance, layerMask)
                ? hit.collider.GetInstanceID()
                : 0;
        }

        private static int GetLaneId(IReadOnlyDictionary<int, int> values, int key)
        {
            return key != 0 && values.TryGetValue(key, out var laneId) ? laneId : 0;
        }
    }
}
