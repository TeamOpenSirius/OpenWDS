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
