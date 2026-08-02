using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Recovered NoteObjectScheduler/Spawner/PositionUpdater slice. It keeps the
    /// original spawn filter, NoteType-to-prefab routing, move window and Hold line
    /// height update while pooling the recovered prefab instances.
    /// </summary>
    public sealed class RecoveredNoteVisualRuntime
    {
        private sealed class ActiveNote
        {
            public RecoveredNotationNote Notation;
            public GameObject Instance;
            public SpriteRenderer HoldLine;
            public Transform EndMarker;
            public int PoolIndex;
            public bool EndMarkerInitialized;
            public bool EndMarkerFollowsHeight;
            public bool IsHold;
            public bool ShouldGrayOut;
        }

        private sealed class ConcurrentLine
        {
            public long Milliseconds;
            public int StartLane;
            public int Width;
            public GameObject Instance;
        }

        private readonly struct PoolEvent
        {
            public readonly long Milliseconds;
            public readonly int Delta;

            public PoolEvent(long milliseconds, int delta)
            {
                Milliseconds = milliseconds;
                Delta = delta;
            }
        }

        private readonly Transform _parent;
        private readonly GameObject[] _prefabs;
        private readonly RecoveredNotationNote[] _notes;
        private readonly List<ActiveNote> _active = new List<ActiveNote>(128);
        private readonly Stack<GameObject>[] _pools;
        private readonly List<ConcurrentLine> _concurrentLines = new List<ConcurrentLine>();
        private readonly Stack<GameObject> _concurrentPool = new Stack<GameObject>();
        private readonly float _speedRate;
        private readonly double _offsetValue;
        private readonly long _moveMilliseconds;
        private readonly Material _scratchHoldMaterial;
        private readonly int _noteHeight;
        private readonly bool _isActiveConcurrentLine;
        private int _nextNoteIndex;
        private int _nextConcurrentIndex;

        public int TotalVisualNoteCount => _notes.Length;
        public int SpawnedCount { get; private set; }
        public int RecycledCount { get; private set; }
        public int CompletedConcurrentLineCount { get; private set; }
        public int ActiveCount => _active.Count;
        public int PeakActiveCount { get; private set; }
        public long MoveMilliseconds => _moveMilliseconds;
        public double OffsetValue => _offsetValue;
        public int TotalConcurrentLineCount => _concurrentLines.Count;

        public int ActiveConcurrentLineCount
        {
            get
            {
                var count = 0;
                foreach (var line in _concurrentLines)
                    if (line.Instance != null)
                        count++;
                return count;
            }
        }

        public RecoveredNoteVisualRuntime(
            IReadOnlyList<RecoveredNotationNote> notation,
            Transform parent,
            IReadOnlyList<GameObject> prefabs,
            double noteSpeed,
            double noteOffsetValue = 0d,
            Material scratchHoldMaterial = null,
            int noteHeight = 8,
            bool isActiveConcurrentLine = true)
        {
            if (notation == null) throw new ArgumentNullException(nameof(notation));
            _parent = parent != null ? parent : throw new ArgumentNullException(nameof(parent));
            if (prefabs == null || prefabs.Count != 8)
                throw new ArgumentException("Eight original NoteObject prefabs are required.", nameof(prefabs));

            _prefabs = new GameObject[prefabs.Count];
            _pools = new Stack<GameObject>[7];
            for (var index = 0; index < prefabs.Count; index++)
            {
                _prefabs[index] = prefabs[index] != null
                    ? prefabs[index]
                    : throw new ArgumentException("NoteObject prefab must not be null.", nameof(prefabs));
                if (index < 5) _pools[index] = new Stack<GameObject>();
                else if (index > 5) _pools[index - 1] = new Stack<GameObject>();
            }

            var visualNotes = new List<RecoveredNotationNote>(notation.Count);
            foreach (var note in notation)
            {
                // NoteObjectScheduler.Initialize excludes None and HoldEighth (900).
                if (note != null && note.NoteType != 0 && note.NoteType != 900)
                    visualNotes.Add(note);
            }
            visualNotes.Sort((left, right) =>
                left.StartMilliseconds.CompareTo(right.StartMilliseconds));
            _notes = visualNotes.ToArray();
            _isActiveConcurrentLine = isActiveConcurrentLine;
            if (_isActiveConcurrentLine)
                InitializeConcurrentLines(visualNotes);
            _speedRate = RecoveredNotePositionCalculator.CalculateSpeedRate(noteSpeed);
            _offsetValue = noteOffsetValue;
            _scratchHoldMaterial = scratchHoldMaterial;
            _noteHeight = noteHeight;
            var moveSeconds = RecoveredNotePositionCalculator.CalculateMoveSeconds(
                noteSpeed,
                RecoveredGameConfigValues.NoteVisibleTimeRate1,
                RecoveredGameConfigValues.NoteVisibleTimeRate2,
                RecoveredGameConfigValues.MaxNoteVisiblePositionY);
            _moveMilliseconds = (long)(moveSeconds * 1000f);
            PrewarmPools();
        }

        private void PrewarmPools()
        {
            // Retail NoteObjectSpawner.InitializeAsync preloads every ObjectPool
            // before play and separately warms the animated note types. Keep the
            // recovered pool chart-sized so successful play cannot Instantiate in
            // the note scheduler's frame; the lazy fallback remains for late misses.
            var eventsByPool = new List<PoolEvent>[_pools.Length];
            for (var index = 0; index < eventsByPool.Length; index++)
                eventsByPool[index] = new List<PoolEvent>();
            foreach (var note in _notes)
            {
                var target = UsesHoldManager(note.NoteType) &&
                             note.EndMilliseconds >= note.StartMilliseconds
                    ? note.EndMilliseconds
                    : note.StartMilliseconds;
                var events = eventsByPool[GetPrefabIndex(note.NoteType)];
                events.Add(new PoolEvent(
                    note.StartMilliseconds - _moveMilliseconds, 1));
                events.Add(new PoolEvent(target, -1));
            }
            for (var poolIndex = 0; poolIndex < eventsByPool.Length; poolIndex++)
            {
                var capacity = GetPeakOverlap(eventsByPool[poolIndex]);
                var prefabIndex = poolIndex < 5 ? poolIndex : poolIndex + 1;
                PrewarmPool(_prefabs[prefabIndex], _pools[poolIndex], capacity);
            }

            if (!_isActiveConcurrentLine) return;
            var concurrentEvents = new List<PoolEvent>(_concurrentLines.Count * 2);
            foreach (var line in _concurrentLines)
            {
                concurrentEvents.Add(new PoolEvent(
                    line.Milliseconds - _moveMilliseconds, 1));
                concurrentEvents.Add(new PoolEvent(line.Milliseconds, -1));
            }
            PrewarmPool(
                _prefabs[5], _concurrentPool, GetPeakOverlap(concurrentEvents));
        }

        private static int GetPeakOverlap(List<PoolEvent> events)
        {
            events.Sort((left, right) =>
            {
                var time = left.Milliseconds.CompareTo(right.Milliseconds);
                // Tick spawns before processing input results, so an object ending
                // now still overlaps an object whose visible interval starts now.
                return time != 0 ? time : right.Delta.CompareTo(left.Delta);
            });
            var active = 0;
            var peak = 0;
            foreach (var item in events)
            {
                active += item.Delta;
                peak = Math.Max(peak, active);
            }
            return peak;
        }

        private void PrewarmPool(
            GameObject prefab, Stack<GameObject> pool, int capacity)
        {
            while (pool.Count < capacity)
            {
                var instance = UnityEngine.Object.Instantiate(prefab, _parent, false);
                // Retail WarmupAnimatorAsync rents Flick, Scratch and
                // ScratchHold objects and lets their animator graphs evaluate
                // before gameplay. Force that first evaluation into recovery
                // initialization instead of the note's first visible frame.
                foreach (var animator in
                         instance.GetComponentsInChildren<Animator>(true))
                {
                    animator.Rebind();
                    animator.Update(0f);
                }
                instance.SetActive(false);
                pool.Push(instance);
            }
        }

        public void Tick(long passedMilliseconds)
        {
            // Original tick order: CreateNote, expiration, then PositionUpdater.
            while (_nextNoteIndex < _notes.Length &&
                   _notes[_nextNoteIndex].StartMilliseconds - _moveMilliseconds <=
                   passedMilliseconds)
            {
                Spawn(_notes[_nextNoteIndex++], passedMilliseconds);
            }
            while (_nextConcurrentIndex < _concurrentLines.Count &&
                   _concurrentLines[_nextConcurrentIndex].Milliseconds - _moveMilliseconds <=
                   passedMilliseconds)
            {
                SpawnConcurrent(_concurrentLines[_nextConcurrentIndex++], passedMilliseconds);
            }

            for (var index = _active.Count - 1; index >= 0; index--)
            {
                var item = _active[index];
                var usesHoldManager = UsesHoldManager(item.Notation.NoteType);
                var isExpired = usesHoldManager
                    ? item.Notation.StartMilliseconds < passedMilliseconds &&
                      item.Notation.EndMilliseconds < passedMilliseconds - _moveMilliseconds
                    : item.Notation.StartMilliseconds < passedMilliseconds - _moveMilliseconds;
                if (isExpired)
                {
                    RecycleAt(index);
                    continue;
                }
                UpdatePosition(item, passedMilliseconds);
            }
            for (var index = _concurrentLines.Count - 1; index >= 0; index--)
            {
                var line = _concurrentLines[index];
                if (line.Instance == null) continue;
                if (line.Milliseconds < passedMilliseconds - _moveMilliseconds)
                {
                    line.Instance.SetActive(false);
                    _concurrentPool.Push(line.Instance);
                    line.Instance = null;
                    continue;
                }
                var position = line.Instance.transform.localPosition;
                position.y = CalculatePositionY(line.Milliseconds, passedMilliseconds);
                line.Instance.transform.localPosition = position;
            }
        }

        public void Reset()
        {
            for (var index = _active.Count - 1; index >= 0; index--)
                RecycleAt(index);
            _nextNoteIndex = 0;
            foreach (var line in _concurrentLines)
            {
                if (line.Instance == null) continue;
                line.Instance.SetActive(false);
                _concurrentPool.Push(line.Instance);
                line.Instance = null;
            }
            _nextConcurrentIndex = 0;
            SpawnedCount = 0;
            RecycledCount = 0;
            PeakActiveCount = 0;
            CompletedConcurrentLineCount = 0;
        }

        /// <summary>
        /// NoteObjectManager's HoldStart/HoldEnd subscriber forwards the note id to
        /// HoldNoteObject.OnHold. The original writes IsTouchMask through a
        /// MaterialPropertyBlock and suppresses GrayOut while held.
        /// </summary>
        public bool OnHold(int noteId, bool isHold)
        {
            foreach (var item in _active)
            {
                if (item.Notation.Id != noteId || item.HoldLine == null) continue;
                item.IsHold = isHold;
                ApplyHoldMaterialProperty(item);
                ApplyGrayOut(item);
                return true;
            }
            return false;
        }

        /// <summary>
        /// NoteObjectManager.OnInputResult completes the matching pooled object
        /// immediately for input-backed results. The recovered runtime only
        /// produces input-backed results, so a judged note must leave the visual
        /// manager before the position updater runs for this frame.
        /// </summary>
        public int OnInputResults(IReadOnlyList<RecoveredInputResultEntity> results)
        {
            if (results == null || results.Count == 0) return 0;
            var recycled = 0;
            for (var resultIndex = 0; resultIndex < results.Count; resultIndex++)
            {
                var result = results[resultIndex];
                // NotationNoteManager publishes expiration MISS with
                // InputResultEntity.IsInput=false. NoteObjectManager leaves
                // those visuals alive so they continue below the line.
                if (!result.IsInput) continue;
                for (var activeIndex = _active.Count - 1;
                     activeIndex >= 0;
                     activeIndex--)
                {
                    var item = _active[activeIndex];
                    if (item.Notation.Id != result.NoteId ||
                        item.Notation.NoteType != (int)result.NoteType)
                    {
                        continue;
                    }
                    RecycleAt(activeIndex);
                    recycled++;
                    break;
                }

                // NoteObjectManager.OnInputResult completes the independent
                // ConcurrentNoteEntity by its scheduler key. Only Hold and
                // ScratchHold bodies use EndMilliseconds. HoldStart (80-83),
                // like ordinary tap notes, uses StartMilliseconds even though
                // its numeric NoteType is above Flick. Sound, Scratch and Flick
                // return before this call in retail.
                var noteType = (int)result.NoteType;
                if (IsHoldBody(noteType))
                {
                    CompleteConcurrentLine(result.EndMilliseconds);
                }
                else if (noteType != 30 && noteType != 31 &&
                         noteType != 40 && noteType != 50)
                {
                    CompleteConcurrentLine(result.StartMilliseconds);
                }
            }
            return recycled;
        }

        private void CompleteConcurrentLine(long milliseconds)
        {
            if (milliseconds < 0) return;
            foreach (var line in _concurrentLines)
            {
                if (line.Milliseconds != milliseconds ||
                    line.Instance == null)
                {
                    continue;
                }
                line.Instance.SetActive(false);
                _concurrentPool.Push(line.Instance);
                line.Instance = null;
                CompletedConcurrentLineCount++;
                return;
            }
        }

        private void Spawn(RecoveredNotationNote note, long passedMilliseconds)
        {
            var poolIndex = GetPrefabIndex(note.NoteType);
            var instance = _pools[poolIndex].Count > 0
                ? _pools[poolIndex].Pop()
                : UnityEngine.Object.Instantiate(
                    _prefabs[poolIndex < 5 ? poolIndex : poolIndex + 1],
                    _parent,
                    false);
            instance.name = "Runtime_" + note.Id + "_" + note.NoteType;
            instance.SetActive(true);

            var width = RecoveredNotePositionCalculator.GetNoteWidth(
                note.Width,
                RecoveredGameConfigValues.NoteWidthPerLane,
                RecoveredGameConfigValues.LaneBorderWidth);
            ConfigureTypeVisual(instance, note, width);
            var endMarker = FindDirectChild(
                instance.transform,
                note.NoteType == 110 || note.NoteType == 111
                    ? "ScratchNote"
                    : "Note");
            var jumpScratchWidth = IsJumpScratch(note)
                ? RecoveredNotePositionCalculator.GetNoteWidth(
                    Mathf.Abs(note.GimmickValue),
                    RecoveredGameConfigValues.NoteWidthPerLane,
                    RecoveredGameConfigValues.LaneBorderWidth)
                : width;
            SpriteRenderer holdLine = null;
            foreach (var renderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.drawMode == SpriteDrawMode.Simple) continue;
                var size = renderer.size;
                var isDirectHoldLine = IsHoldBody(note.NoteType) &&
                                       renderer.transform.parent == instance.transform;
                var isJumpScratchEnd = IsJumpScratch(note) &&
                                       endMarker != null &&
                                       renderer.transform.IsChildOf(endMarker);
                size.x = isDirectHoldLine
                    ? GetHoldLineWidth(width)
                    : GetTapVisualWidth(
                        isJumpScratchEnd ? jumpScratchWidth : width);
                renderer.size = size;
                if (holdLine == null && isDirectHoldLine) holdLine = renderer;
            }

            var active = new ActiveNote
            {
                Notation = note,
                Instance = instance,
                HoldLine = holdLine,
                EndMarker = endMarker,
                PoolIndex = poolIndex,
                // Both overrides move their endpoint after resizing the line.
                // HoldNoteObject.UpdateHeight tail-calls
                // TapNoteEntity.SetLocalPositionY on _end (this call is omitted
                // from Cpp2IL's call list); ScratchHold writes _endScratch.
                EndMarkerFollowsHeight = IsHoldBody(note.NoteType),
            };
            if (holdLine != null)
            {
                ApplyHoldMaterialProperty(active);
                ApplyGrayOut(active);
            }
            _active.Add(active);
            SpawnedCount++;
            PeakActiveCount = Math.Max(PeakActiveCount, _active.Count);
            UpdatePosition(active, passedMilliseconds);
        }

        private void SpawnConcurrent(ConcurrentLine line, long passedMilliseconds)
        {
            var instance = _concurrentPool.Count > 0
                ? _concurrentPool.Pop()
                : UnityEngine.Object.Instantiate(_prefabs[5], _parent, false);
            instance.name = "Runtime_Concurrent_" + line.Milliseconds;
            instance.SetActive(true);
            var width = RecoveredNotePositionCalculator.GetNoteWidth(
                line.Width,
                RecoveredGameConfigValues.NoteWidthPerLane,
                RecoveredGameConfigValues.LaneBorderWidth);
            var renderer = instance.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                var size = renderer.size;
                size.x = width;
                renderer.size = size;
            }
            var position = instance.transform.localPosition;
            position.x = RecoveredNotePositionCalculator.GetNotePositionX(
                line.StartLane,
                width,
                RecoveredGameConfigValues.NoteWidthPerLane,
                RecoveredGameConfigValues.LaneBorderWidth);
            position.y = CalculatePositionY(line.Milliseconds, passedMilliseconds);
            instance.transform.localPosition = position;
            line.Instance = instance;
        }

        private void InitializeConcurrentLines(List<RecoveredNotationNote> notes)
        {
            // Original lambdas: exclude None/Sound/SoundPurple/HoldEighth; group
            // Hold and Sound by EndMilliseconds, all other notes by StartMilliseconds.
            var groups = new SortedDictionary<long, List<RecoveredNotationNote>>();
            foreach (var note in notes)
            {
                if (note.NoteType == 30 || note.NoteType == 31 || note.NoteType == 900)
                    continue;
                var milliseconds = IsHoldBody(note.NoteType)
                    ? note.EndMilliseconds
                    : note.StartMilliseconds;
                if (!groups.TryGetValue(milliseconds, out var group))
                {
                    group = new List<RecoveredNotationNote>();
                    groups.Add(milliseconds, group);
                }
                group.Add(note);
            }
            foreach (var pair in groups)
            {
                if (pair.Key < 0 || pair.Value.Count <= 1) continue;
                var minLane = int.MaxValue;
                var maxLane = int.MinValue;
                foreach (var note in pair.Value)
                {
                    minLane = Math.Min(minLane, note.Lane);
                    maxLane = Math.Max(maxLane, note.Lane);
                }
                _concurrentLines.Add(new ConcurrentLine
                {
                    Milliseconds = pair.Key,
                    StartLane = minLane,
                    Width = maxLane - minLane + 1,
                });
            }
        }

        public static float GetTapVisualWidth(float notationWidth)
        {
            return notationWidth - RecoveredOriginalGameConfig.NoteMarginWidth;
        }

        public static float GetHoldLineWidth(float notationWidth)
        {
            return notationWidth - RecoveredOriginalGameConfig.NoteMarginWidth +
                   RecoveredOriginalGameConfig.HoldNoteLineAdditionalWidth;
        }

        private void UpdatePosition(ActiveNote item, long passedMilliseconds)
        {
            var note = item.Notation;
            var width = RecoveredNotePositionCalculator.GetNoteWidth(
                note.Width,
                RecoveredGameConfigValues.NoteWidthPerLane,
                RecoveredGameConfigValues.LaneBorderWidth);
            var position = item.Instance.transform.localPosition;
            position.x = RecoveredNotePositionCalculator.GetNotePositionX(
                note.Lane,
                width,
                RecoveredGameConfigValues.NoteWidthPerLane,
                RecoveredGameConfigValues.LaneBorderWidth);
            var startPositionY =
                CalculatePositionY(note.StartMilliseconds, passedMilliseconds);
            // The lane mask is enabled by HoldNoteObject.OnHold. A successfully
            // held body stops at the judgment line; a missed/unheld body remains
            // visible while falling below it.
            position.y = item.HoldLine != null && item.IsHold
                ? Mathf.Max(0f, startPositionY)
                : startPositionY;
            item.Instance.transform.localPosition = position;

            if (item.HoldLine != null)
            {
                // NotePositionUpdater.UpdateHoldNotes calls GrayOut when the clock
                // reaches hold StartTickCount + HoldNoteGrayOutOffsetSeconds.
                item.ShouldGrayOut = passedMilliseconds / 1000f >=
                                     note.StartMilliseconds / 1000f +
                                     RecoveredOriginalGameConfig.HoldNoteGrayOutOffsetSeconds;
                ApplyGrayOut(item);
                var endPositionY =
                    CalculatePositionY(note.EndMilliseconds, passedMilliseconds);
                var visibleStartPositionY = item.IsHold
                    ? Mathf.Max(0f, startPositionY)
                    : startPositionY;
                var height = endPositionY - visibleStartPositionY;
                var size = item.HoldLine.size;
                size.y = Mathf.Max(0f, height);
                item.HoldLine.size = size;
                if (item.EndMarker != null &&
                    (!item.EndMarkerInitialized || item.EndMarkerFollowsHeight))
                {
                    var endPosition = item.EndMarker.localPosition;
                    endPosition.y = size.y;
                    item.EndMarker.localPosition = endPosition;
                    item.EndMarkerInitialized = true;
                }
            }
        }

        private static void ApplyHoldMaterialProperty(ActiveNote item)
        {
            var block = new MaterialPropertyBlock();
            item.HoldLine.GetPropertyBlock(block);
            block.SetInteger(Shader.PropertyToID("IsTouchMask"), item.IsHold ? 1 : 0);
            block.SetInteger(
                Shader.PropertyToID("_IsScratch"),
                item.Notation.NoteType == 110 || item.Notation.NoteType == 111 ? 1 : 0);
            item.HoldLine.SetPropertyBlock(block);
        }

        private static void ApplyGrayOut(ActiveNote item)
        {
            item.HoldLine.color = item.ShouldGrayOut && !item.IsHold
                ? RecoveredOriginalGameConfig.HoldNoteGrayOutColor
                : Color.white;
        }

        private float CalculatePositionY(long target, long passed)
        {
            return RecoveredNotePositionCalculator.CalculatePositionY(
                target,
                passed,
                _speedRate,
                _offsetValue,
                RecoveredGameConfigValues.PositionPow3Rate,
                RecoveredGameConfigValues.PositionPow1Rate);
        }

        private void RecycleAt(int index)
        {
            var item = _active[index];
            item.Instance.SetActive(false);
            _pools[item.PoolIndex].Push(item.Instance);
            _active.RemoveAt(index);
            RecycledCount++;
        }

        private static bool IsHoldBody(int noteType)
        {
            return noteType == 100 || noteType == 101 ||
                   noteType == 110 || noteType == 111;
        }

        private static bool IsJumpScratch(RecoveredNotationNote note)
        {
            return note != null &&
                   (note.NoteType == 110 || note.NoteType == 111) &&
                   note.GimmickType == RecoveredJumpScratch.GimmickType;
        }

        private static bool UsesHoldManager(int noteType)
        {
            // NoteObjectManager.Spawn places 100/101/110/111 and 30/31 in
            // HoldNoteObjects. Sound UpdateHeight is a no-op, but its -1 end time
            // makes it expire immediately after StartMilliseconds.
            return IsHoldBody(noteType) || noteType == 30 || noteType == 31;
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            for (var index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                if (child.name == name) return child;
            }
            return null;
        }

        private void ConfigureTypeVisual(
            GameObject instance,
            RecoveredNotationNote note,
            float width)
        {
            var noteRotation = Quaternion.Euler(
                RecoveredOriginalGameConfig.GetNoteHeightRotationX(_noteHeight), 0f, 0f);
            if (IsHoldBody(note.NoteType))
            {
                // HoldNoteObject.Initialize writes the rotation to _end._noteTransform
                // (object offset 0x70), not to the HoldNoteObject root. The root and
                // stretchable line therefore remain on the LaneGroup plane.
                instance.transform.localRotation = Quaternion.identity;
                var end = FindDirectChild(
                    instance.transform,
                    note.NoteType == 110 || note.NoteType == 111
                        ? "ScratchNote"
                        : "Note");
                if (end != null) end.localRotation = noteRotation;
            }
            else
            {
                // Tap/Flick/Scratch initialize their entity note transform with
                // GameConfig.GetNoteHeight. Default setting 8 maps to -15 degrees.
                instance.transform.localRotation = noteRotation;
            }

            if (note.NoteType == 10 || note.NoteType == 20 ||
                note.NoteType == 80 || note.NoteType == 81 ||
                note.NoteType == 82 || note.NoteType == 83 ||
                note.NoteType == 200)
            {
                var critical = note.NoteType == 20 || note.NoteType == 81 ||
                               note.NoteType == 83;
                SetDirectChildActive(instance.transform, "Notes1", !critical);
                SetDirectChildActive(instance.transform, "Notes2", critical);
            }

            if (IsHoldBody(note.NoteType))
            {
                foreach (var renderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer.transform.parent == instance.transform &&
                        renderer.drawMode != SpriteDrawMode.Simple)
                    {
                        // HoldNoteObject.Initialize and GrayOut use white/gray
                        // vertex color; the original shader supplies the cyan or
                        // purple gradient from its _IsScratch branch.
                        renderer.color = Color.white;
                        if ((note.NoteType == 110 || note.NoteType == 111) &&
                            _scratchHoldMaterial != null)
                        {
                            renderer.sharedMaterial = _scratchHoldMaterial;
                        }
                        break;
                    }
                }
            }

            if (note.NoteType == 40 || note.NoteType == 50 ||
                note.NoteType == 110 || note.NoteType == 111)
            {
                var arrowWidth = IsJumpScratch(note)
                    ? RecoveredNotePositionCalculator.GetNoteWidth(
                        Mathf.Abs(note.GimmickValue),
                        RecoveredGameConfigValues.NoteWidthPerLane,
                        RecoveredGameConfigValues.LaneBorderWidth)
                    : width;
                ConfigureArrows(instance.transform, note, arrowWidth);
            }

            if (note.NoteType == 110 || note.NoteType == 111)
            {
                // ScratchHoldNoteObject.Set always resets _endScratch X to 0.
                // The spawner optionally calls SetJumpScratch afterwards. This
                // reset is required when a pooled JumpScratch instance is rented
                // for an ordinary ScratchHold.
                var end = FindDirectChild(instance.transform, "ScratchNote");
                SetLocalPositionX(
                    end,
                    IsJumpScratch(note) ? GetJumpScratchEndOffsetX(note) : 0f);
            }
        }

        public static float GetJumpScratchEndOffsetX(RecoveredNotationNote note)
        {
            if (note == null ||
                note.GimmickType != RecoveredJumpScratch.GimmickType)
            {
                return 0f;
            }
            var bodyWidth = RecoveredNotePositionCalculator.GetNoteWidth(
                note.Width,
                RecoveredGameConfigValues.NoteWidthPerLane,
                RecoveredGameConfigValues.LaneBorderWidth);
            var jumpWidth = RecoveredNotePositionCalculator.GetNoteWidth(
                Mathf.Abs(note.GimmickValue),
                RecoveredGameConfigValues.NoteWidthPerLane,
                RecoveredGameConfigValues.LaneBorderWidth);
            var destinationX = RecoveredNotePositionCalculator.GetNotePositionX(
                note.Lane,
                jumpWidth,
                RecoveredGameConfigValues.NoteWidthPerLane,
                RecoveredGameConfigValues.LaneBorderWidth);
            var startX = RecoveredNotePositionCalculator.GetNotePositionX(
                note.Lane,
                bodyWidth,
                RecoveredGameConfigValues.NoteWidthPerLane,
                RecoveredGameConfigValues.LaneBorderWidth);
            var offset = destinationX - startX;
            return note.GimmickValue < 0 ? -offset : offset;
        }

        private static void ConfigureArrows(
            Transform root,
            RecoveredNotationNote note,
            float width)
        {
            var isJumpScratch = note.GimmickType == 1;
            // ScratchHoldNoteObject.SetJumpScratch reads GimmickValue twice,
            // takes its absolute value, and passes that value as laneCount to
            // FlickNoteEntity.SetActive. For JumpScratch, width is likewise
            // the width calculated from abs(GimmickValue), not the Hold body.
            var arrowLaneCount = isJumpScratch
                ? Mathf.Abs(note.GimmickValue)
                : note.Width;
            var activeCount = GetArrowActiveCount(arrowLaneCount, isJumpScratch);
            var interval = GetArrowInterval(arrowLaneCount);
            var left = FindDescendant(root, "NotesLeft");
            var right = FindDescendant(root, "NotesRight");
            SetArrowChildren(left, activeCount, interval);
            SetArrowChildren(right, activeCount, interval);

            // FlickNoteEntity.SetArrowAnimator selects the controller state by
            // activeCount and starts at (Time.time % 0.5) * 2 normalized time.
            // The recovered clips contain the original staggered alpha curves.
            var animator = root.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.enabled = true;
                animator.Play(
                    GetArrowAnimationStateName(isJumpScratch, activeCount),
                    0,
                    (Time.time % 0.5f) * 2f);
            }

            var halfSpan = isJumpScratch
                ? interval * activeCount *
                  RecoveredOriginalGameConfig.JumpScratchArrowWidthRate * 0.5f
                : width * 0.5f +
                  RecoveredOriginalGameConfig.NormalArrowEdgeOffset;
            SetLocalPositionX(left, -halfSpan);
            SetLocalPositionX(right, halfSpan);

            // FlickNoteObject.Set explicitly enables both halves when the
            // notation has no directional gimmick. Only JumpScratch and
            // OneDirection select a single half from GimmickValue.
            if (note.GimmickType == 1 || note.GimmickType == 2)
            {
                var pointsLeft = note.GimmickValue <= 0;
                if (left != null) left.gameObject.SetActive(pointsLeft);
                if (right != null) right.gameObject.SetActive(!pointsLeft);
            }
            else
            {
                if (left != null) left.gameObject.SetActive(true);
                if (right != null) right.gameObject.SetActive(true);
            }
        }

        public static int GetArrowActiveCount(int laneCount, bool isJumpScratch)
        {
            var bucket = Mathf.Clamp((laneCount - 1) / 2, 0, 5);
            var normal = new[] { 3, 5, 9, 12, 16, 20 };
            var jump = new[] { 6, 11, 18, 26, 34, 42 };
            return isJumpScratch ? jump[bucket] : normal[bucket];
        }

        public static float GetArrowInterval(int laneCount)
        {
            return RecoveredOriginalGameConfig.ArrowIntervalBase +
                   (laneCount >= 3 && laneCount % 2 == 0 ? 1f : 0f) *
                   RecoveredOriginalGameConfig.ArrowIntervalLaneStep;
        }

        public static string GetArrowAnimationStateName(
            bool isJumpScratch,
            int activeCount)
        {
            return "ScratchNotesArrow_" +
                   (isJumpScratch ? "jump" : "flick") +
                   activeCount + "_anim";
        }

        private static void SetArrowChildren(
            Transform parent,
            int activeCount,
            float interval)
        {
            if (parent == null) return;
            for (var index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                // NotesArrowsObject.ActivateArrowSpriteRenderer changes Renderer.enabled;
                // disabling GameObjects prevents the Animator alpha bindings from running.
                child.gameObject.SetActive(true);
                var renderer = child.GetComponent<SpriteRenderer>();
                if (renderer != null) renderer.enabled = index < activeCount;
                SetLocalPositionX(child, index * interval);
            }
        }

        private static void SetDirectChildActive(
            Transform parent,
            string name,
            bool active)
        {
            var child = FindDirectChild(parent, name);
            if (child != null) child.gameObject.SetActive(active);
        }

        private static Transform FindDescendant(Transform parent, string name)
        {
            if (parent == null) return null;
            for (var index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                if (child.name == name) return child;
                var nested = FindDescendant(child, name);
                if (nested != null) return nested;
            }
            return null;
        }

        private static void SetLocalPositionX(Transform target, float value)
        {
            if (target == null) return;
            var position = target.localPosition;
            position.x = value;
            target.localPosition = position;
        }

        // Order: Tap, Hold, Flick, Scratch, ScratchHold, Sound, SoundPurple.
        public static int GetPrefabIndex(int noteType)
        {
            switch (noteType)
            {
                case 10:
                case 20:
                case 80:
                case 81:
                case 82:
                case 83:
                case 200:
                    return 0;
                case 100:
                case 101:
                    return 1;
                case 50:
                    return 2;
                case 40:
                    return 3;
                case 110:
                case 111:
                    return 4;
                case 30:
                    return 5;
                case 31:
                    return 6;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(noteType), noteType, "Unsupported NoteObject type.");
            }
        }
    }
}
