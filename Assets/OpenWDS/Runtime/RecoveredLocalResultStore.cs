using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace OpenWDS.Runtime
{
    public enum RecoveredClearLamp
    {
        None = 0,
        Clear = 1,
        FullCombo = 2,
        AllPerfect = 3,
    }

    /// <summary>
    /// Local substitute for the server-backed per-music LiveStatus used by the
    /// original result and selection flows. Records are durable and keyed by
    /// music plus difficulty so clear lamps and best achievement rates survive.
    /// </summary>
    public sealed class RecoveredLocalResultStore
    {
        [Serializable]
        private sealed class Record
        {
            public long MusicId;
            public int Difficulty;
            public double BestAchievementRate;
            public int BestClearLamp;
        }

        private const string FileName = "OpenWDSLocalResults";
        private readonly string _path;
        private readonly List<Record> _records;

        public RecoveredLocalResultStore(string persistentDataPath = null)
        {
            var root = string.IsNullOrEmpty(persistentDataPath)
                ? Application.persistentDataPath
                : persistentDataPath;
            var directory = Path.Combine(
                root, RecoveredSettingsPersistence.CurrentDirectory);
            _path = Path.Combine(directory, FileName);
            _records = Load();
        }

        public double GetBest(
            long musicId,
            RecoveredMusicDifficulty difficulty)
        {
            var record = Find(musicId, difficulty);
            return record != null ? record.BestAchievementRate : 0d;
        }

        public bool HasClear(
            long musicId,
            RecoveredMusicDifficulty difficulty) =>
            GetClearLamp(musicId, difficulty) != RecoveredClearLamp.None;

        public RecoveredClearLamp GetClearLamp(
            long musicId,
            RecoveredMusicDifficulty difficulty)
        {
            var record = Find(musicId, difficulty);
            if (record == null) return RecoveredClearLamp.None;
            if (Enum.IsDefined(typeof(RecoveredClearLamp), record.BestClearLamp) &&
                record.BestClearLamp != (int)RecoveredClearLamp.None)
            {
                return (RecoveredClearLamp)record.BestClearLamp;
            }

            // Records written by the first recovered-store revision did not
            // contain a lamp field. Preserve those completed plays as Clear.
            return record.BestAchievementRate > 0d
                ? RecoveredClearLamp.Clear
                : RecoveredClearLamp.None;
        }

        public bool RecordResult(
            long musicId,
            RecoveredMusicDifficulty difficulty,
            double achievementRate,
            RecoveredClearLamp clearLamp,
            out double previousBest)
        {
            var record = Find(musicId, difficulty);
            previousBest = record != null ? record.BestAchievementRate : 0d;
            var isNew = record == null || achievementRate > previousBest;
            if (record == null)
            {
                record = new Record
                {
                    MusicId = musicId,
                    Difficulty = (int)difficulty,
                    BestAchievementRate = Math.Max(0d, achievementRate),
                    BestClearLamp = (int)clearLamp,
                };
                _records.Add(record);
            }
            else
            {
                if (isNew) record.BestAchievementRate = achievementRate;
                record.BestClearLamp = Math.Max(
                    record.BestClearLamp, (int)clearLamp);
            }
            Save();
            return isNew;
        }

        public bool RecordResult(
            long musicId,
            RecoveredMusicDifficulty difficulty,
            double achievementRate,
            out double previousBest) =>
            RecordResult(
                musicId,
                difficulty,
                achievementRate,
                RecoveredClearLamp.Clear,
                out previousBest);

        private Record Find(
            long musicId,
            RecoveredMusicDifficulty difficulty)
        {
            for (var index = 0; index < _records.Count; index++)
            {
                var record = _records[index];
                if (record.MusicId == musicId &&
                    record.Difficulty == (int)difficulty)
                    return record;
            }
            return null;
        }

        private List<Record> Load()
        {
            if (!File.Exists(_path)) return new List<Record>();
            try
            {
                var json = RecoveredSettingsCrypto.DecryptUtf8(
                    File.ReadAllText(_path));
                return JsonConvert.DeserializeObject<List<Record>>(json) ??
                       new List<Record>();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "Failed to load recovered local results: " + exception);
                return new List<Record>();
            }
        }

        private void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            var json = JsonConvert.SerializeObject(_records);
            File.WriteAllText(
                _path, RecoveredSettingsCrypto.EncryptUtf8(json));
        }
    }
}
