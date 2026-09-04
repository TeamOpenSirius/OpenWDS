using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenWDS.Runtime
{
    [Serializable]
    public sealed class RecoveredNotationNote
    {
        public int Id;
        public float StartTickCount;
        public float EndTickCount;
        public int NoteType;
        public int Lane;
        public int Width;
        public int GimmickType;
        public int GimmickValue;
        public bool IgnoreLeftInnerCollider;
        public bool IgnoreRightInnerCollider;
        public bool IgnoreLeftOuterCollider;
        public bool IgnoreRightOuterCollider;

        public long StartMilliseconds => (long)(StartTickCount * 1000f);
        public long EndMilliseconds => (long)(EndTickCount * 1000f);
        public int EndLane => Lane + Width - 1;
    }

    [Serializable]
    public sealed class RecoveredMusicConfigData
    {
        public string CueSheetName;
        public string CueName;
        public float DelayStartSeconds;
        public string CueSheetDirectory;
    }

    public static class RecoveredStandardNotation
    {
        public static RecoveredNotationNote[] Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("Notation text must not be empty.", nameof(text));
            }
            var notes = new List<RecoveredNotationNote>();
            foreach (var line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }
                var columns = line.Split(',');
                if (columns.Length != 5 && columns.Length != 7)
                {
                    throw new FormatException("Notation records must contain 5 or 7 columns.");
                }
                notes.Add(new RecoveredNotationNote
                {
                    Id = notes.Count,
                    StartTickCount = ParseFloat(columns[0]),
                    EndTickCount = ParseFloat(columns[1]),
                    NoteType = ParseInt(columns[2]),
                    Lane = ParseInt(columns[3]),
                    Width = ParseInt(columns[4]),
                    GimmickType = columns.Length == 7 ? ParseGimmickType(columns[5]) : 0,
                    GimmickValue = columns.Length == 7 ? ParseInt(columns[6]) : 0,
                });
            }
            // Preserve CSV order for equal timestamps. AutoTouch relies on the
            // same stability before applying its Scratch-first secondary key.
            var ordered = notes.OrderBy(note => note.StartMilliseconds).ToArray();
            RecoveredNotationNoteProcessor.SetOuterCollider(ordered);
            return ordered;
        }

        public static RecoveredMusicConfigData ParseMusicConfig(string text)
        {
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length != 2 ||
                lines[0] != "CueSheetName,CueName,DelaySeconds,CueSheetDirectory")
            {
                throw new FormatException("Unexpected music_config header or row count.");
            }
            var columns = lines[1].Split(',');
            if (columns.Length != 4)
            {
                throw new FormatException("music_config row must contain 4 columns.");
            }
            return new RecoveredMusicConfigData
            {
                CueSheetName = columns[0],
                CueName = columns[1],
                DelayStartSeconds = ParseFloat(columns[2]),
                CueSheetDirectory = columns[3],
            };
        }

        private static float ParseFloat(string value)
        {
            return float.Parse(value, CultureInfo.InvariantCulture);
        }

        private static int ParseInt(string value)
        {
            return int.Parse(value, CultureInfo.InvariantCulture);
        }

        private static int ParseGimmickType(string value)
        {
            // Decoded notation CSVs may use AppConst.GimmickType enum labels,
            // while timeline gimmicks remain numeric values (for example 13/14).
            if (value == "JumpScratch") return 1;
            if (value == "OneDirection") return 2;
            return ParseInt(value);
        }
    }

    /// <summary>
    /// Restores NotationNoteProcessor.SetOuterCollider. Adjacent notes suppress
    /// the sub-collider facing their neighbour so one touch cannot be claimed by
    /// the wrong notation. Sustained note bodies also occupy that boundary while
    /// their time interval overlaps the current note.
    /// </summary>
    public static class RecoveredNotationNoteProcessor
    {
        private static readonly int[] Split2LaneIds = { 1, 7 };
        private static readonly int[] Split3LaneIds = { 1, 5, 9 };
        private static readonly int[] Split4LaneIds = { 1, 4, 7, 10 };
        private static readonly int[] Split5LaneIds = { 1, 4, 6, 8, 10 };
        private static readonly int[] Split6LaneIds = { 1, 3, 5, 7, 9, 11 };

        /// <summary>
        /// Restores NotationNoteProcessor.UpdateForSplitRandom. Each non-empty
        /// split-lane interval receives its own random permutation of the
        /// official lane starts. Notes at both interval endpoints are included.
        /// </summary>
        public static RecoveredNotationNote[] UpdateForSplitRandom(
            RecoveredNotationNote[] notes)
        {
            if (notes == null) throw new ArgumentNullException(nameof(notes));

            var splitLanes = notes.Where(note =>
                    note != null &&
                    RecoveredSplitLaneRuntime.IsSplitLane(note.GimmickType) &&
                    note.EndMilliseconds > note.StartMilliseconds)
                .ToArray();
            if (splitLanes.Length == 0) return notes;

            foreach (var splitLane in splitLanes)
            {
                var splitCount = splitLane.GimmickType % 10;
                var laneIds = GetSplitRandomLaneIds(splitCount);
                if (laneIds.Length == 0) continue;

                // The retail method uses OrderBy(_ => Guid.NewGuid()).
                var shuffledLaneIds = laneIds
                    .OrderBy(_ => Guid.NewGuid())
                    .ToArray();
                var laneMap = laneIds
                    .Select((lane, index) => (lane, index))
                    .ToDictionary(pair => pair.lane,
                        pair => shuffledLaneIds[pair.index]);

                foreach (var note in notes.Where(note =>
                             note != null &&
                             !RecoveredSplitLaneRuntime.IsSplitLane(
                                 note.GimmickType) &&
                             note.StartMilliseconds >= splitLane.StartMilliseconds &&
                             note.StartMilliseconds <= splitLane.EndMilliseconds))
                {
                    if (laneMap.TryGetValue(note.Lane, out var shuffledLane))
                        note.Lane = shuffledLane;

                    if (splitCount == 5)
                        note.Width = note.Lane == 1 || note.Lane == 10 ? 3 : 2;
                }
            }

            return notes;
        }

        private static int[] GetSplitRandomLaneIds(int splitCount)
        {
            switch (splitCount)
            {
                case 1: return Array.Empty<int>();
                case 2: return Split2LaneIds;
                case 3: return Split3LaneIds;
                case 4: return Split4LaneIds;
                case 5: return Split5LaneIds;
                case 6: return Split6LaneIds;
                default:
                    throw new ArgumentOutOfRangeException(nameof(splitCount));
            }
        }

        public static void SetOuterCollider(IReadOnlyList<RecoveredNotationNote> notes)
        {
            if (notes == null) throw new ArgumentNullException(nameof(notes));
            for (var index = 0; index < notes.Count; index++)
            {
                var current = notes[index];
                if (current == null) continue;

                current.IgnoreLeftInnerCollider = false;
                current.IgnoreRightInnerCollider = false;
                current.IgnoreLeftOuterCollider = false;
                current.IgnoreRightOuterCollider = false;

                var nextToLeftLane = current.Lane - 1;
                var nextToRightLane = current.EndLane + 1;
                for (var otherIndex = 0; otherIndex < notes.Count; otherIndex++)
                {
                    if (otherIndex == index) continue;
                    var other = notes[otherIndex];
                    if (other == null) continue;

                    var sameStart = other.StartMilliseconds == current.StartMilliseconds;
                    var bodyAtStart = IsSustainedBody(other.NoteType) &&
                                      other.StartMilliseconds <= current.StartMilliseconds &&
                                      current.StartMilliseconds <= other.EndMilliseconds;
                    if (!sameStart && !bodyAtStart) continue;

                    if (other.EndLane == nextToLeftLane)
                    {
                        current.IgnoreLeftInnerCollider = true;
                        current.IgnoreLeftOuterCollider = true;
                    }
                    if (other.Lane == nextToRightLane)
                    {
                        current.IgnoreRightInnerCollider = true;
                        current.IgnoreRightOuterCollider = true;
                    }
                }
            }
        }

        private static bool IsSustainedBody(int noteType)
        {
            switch ((RecoveredNoteType)noteType)
            {
                case RecoveredNoteType.Hold:
                case RecoveredNoteType.CriticalHold:
                case RecoveredNoteType.ScratchHold:
                case RecoveredNoteType.ScratchCriticalHold:
                    return true;
                default:
                    return false;
            }
        }
    }
}
