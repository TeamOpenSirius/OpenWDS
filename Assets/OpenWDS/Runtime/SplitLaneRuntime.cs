using System;
using System.Collections.Generic;

namespace OpenWDS.Runtime
{
    public enum SplitLaneType
    {
        BothEnds = 1,
        Full = 3,
        Light = 5,
        Ignore = 7,
    }

    public readonly struct SplitLaneEntry
    {
        public readonly int Id;
        public readonly int SplitLaneEffectId;
        public readonly int SplitCount;
        public readonly bool ShouldShow;
        public readonly bool IsContinued;
        public readonly long StartMilliseconds;
        public readonly long EndMilliseconds;
        public readonly SplitLaneType SplitLaneType;

        public SplitLaneEntry(
            NotationNote note,
            int gimmickType,
            bool shouldShow,
            bool isContinued)
        {
            Id = note.Id;
            SplitLaneEffectId = note.GimmickValue;
            SplitCount = GetSplitCount(gimmickType);
            ShouldShow = shouldShow;
            IsContinued = isContinued;
            StartMilliseconds = note.StartMilliseconds;
            EndMilliseconds = note.EndMilliseconds;
            SplitLaneType = GetSplitLaneType(gimmickType);
        }

        private static int GetSplitCount(int gimmickType) => gimmickType % 10;

        private static SplitLaneType GetSplitLaneType(int gimmickType) =>
            (SplitLaneType)(gimmickType / 10);
    }

    /// <summary>
    /// Machine-code-matched pure scheduling layer for SplitLaneScheduler.
    /// Split lanes begin their one-second show animation before StartMilliseconds;
    /// zero-length records therefore still remain visible through the original
    /// 1.0 second show plus 0.5 second hide animation window.
    /// </summary>
    public sealed class SplitLaneRuntime
    {
        public const long ShowAnimationMilliseconds = 1000;
        public const long HideAnimationMilliseconds = 500;
        public const long MinimumShowingMilliseconds =
            ShowAnimationMilliseconds + HideAnimationMilliseconds;

        private readonly NotationNote[] _splitLanes;
        private readonly List<ActiveSplitLane> _current = new List<ActiveSplitLane>();
        private readonly List<SplitLaneEntry> _frameEntries =
            new List<SplitLaneEntry>();
        private int _lastSplitLaneIndex;
        private readonly bool _richSetting;

        private readonly struct ActiveSplitLane
        {
            public readonly NotationNote Note;
            public readonly int ConvertedGimmickType;

            public ActiveSplitLane(NotationNote note, int convertedGimmickType)
            {
                Note = note;
                ConvertedGimmickType = convertedGimmickType;
            }
        }

        public SplitLaneRuntime(
            IEnumerable<NotationNote> notation,
            int spritEffectSettingType = (int)SpritEffectSettingType.Normal)
        {
            if (notation == null) throw new ArgumentNullException(nameof(notation));
            _richSetting = spritEffectSettingType == (int)SpritEffectSettingType.Rich;
            var splitLanes = new List<NotationNote>();
            foreach (var note in notation)
            {
                if (note != null && IsSplitLane(note.GimmickType))
                    splitLanes.Add(note);
            }
            splitLanes.Sort((left, right) =>
                left.StartMilliseconds.CompareTo(right.StartMilliseconds));
            _splitLanes = splitLanes.ToArray();
        }

        public IReadOnlyList<SplitLaneEntry> FrameEntries => _frameEntries;
        public int SplitLaneCount => _splitLanes.Length;
        public int ActiveCount => _current.Count;

        public void Reset()
        {
            _lastSplitLaneIndex = 0;
            _current.Clear();
            _frameEntries.Clear();
        }

        public void Tick(long chartMilliseconds)
        {
            _frameEntries.Clear();

            while (_lastSplitLaneIndex < _splitLanes.Length)
            {
                var note = _splitLanes[_lastSplitLaneIndex];
                if (!HasReached(
                        chartMilliseconds,
                        note.StartTickCount - ShowAnimationMilliseconds / 1000f))
                    break;

                // NotationNoteManager.InitializeAsync 0xB99E9D4 calls
                // ConvertGimmickType only when SpritEffectSettingType == Rich(0).
                var converted = _richSetting
                    ? ConvertGimmickType(note.GimmickType)
                    : note.GimmickType;
                _current.Add(new ActiveSplitLane(note, converted));
                _frameEntries.Add(new SplitLaneEntry(
                    note, converted, true, false));
                _lastSplitLaneIndex++;
            }

            var hideCount = 0;
            for (var index = _current.Count - 1; index >= 0; index--)
            {
                var active = _current[index];
                if (!HasReached(chartMilliseconds, active.Note.EndTickCount))
                    continue;

                hideCount++;
                _frameEntries.Add(new SplitLaneEntry(
                    active.Note,
                    active.ConvertedGimmickType,
                    false,
                    _current.Count - hideCount > 0));
            }

            if (hideCount == 0) return;
            _current.RemoveAll(active =>
                HasReached(chartMilliseconds, active.Note.EndTickCount));
        }

        private static bool HasReached(long chartMilliseconds, float tickSeconds) =>
            chartMilliseconds / 1000f >= tickSeconds;

        public static bool IsSplitLane(int gimmickType)
        {
            var family = gimmickType / 10;
            var count = gimmickType % 10;
            return (family == 1 || family == 3 || family == 5 || family == 7) &&
                   count >= 1 && count <= 6;
        }

        public static int ConvertGimmickType(int gimmickType)
        {
            // NotationNoteProcessor.ConvertGimmickType adds 20 only to Split1..6.
            return gimmickType >= 11 && gimmickType <= 16
                ? gimmickType + 20
                : gimmickType;
        }
    }
}
