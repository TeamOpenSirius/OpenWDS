using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace OpenWDS.Runtime
{
    public enum ClearLamp
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
    public sealed class LocalResultStore
    {
        [Serializable]
        private sealed class Record
        {
            public long MusicId;
            public int Difficulty;
            public double BestAchievementRate;
            public int BestClearLamp;
            public int AchievementStar;
            public int AccuracyStar;
            public int LampStar;
        }

        private const string FileName = "OpenWDSLocalResults";
        private readonly string _path;
        private readonly List<Record> _records;

        public LocalResultStore(string persistentDataPath = null, bool anotherNotation = false)
        {
            var root = string.IsNullOrEmpty(persistentDataPath)
                ? Application.persistentDataPath
                : persistentDataPath;
            var directory = Path.Combine(
                root, SettingsPersistence.CurrentDirectory);
            _path = Path.Combine(directory, anotherNotation ? "OpenWDSAnotherNotationResults" : FileName);
            _records = Load();
        }

        public double GetBest(
            long musicId,
            MusicDifficulty difficulty)
        {
            var record = Find(musicId, difficulty);
            return record != null ? record.BestAchievementRate : 0d;
        }

        public bool HasClear(
            long musicId,
            MusicDifficulty difficulty) =>
            GetClearLamp(musicId, difficulty) != ClearLamp.None;

        public ClearLamp GetClearLamp(
            long musicId,
            MusicDifficulty difficulty)
        {
            var record = Find(musicId, difficulty);
            return record == null ? ClearLamp.None : (ClearLamp)record.BestClearLamp;
        }

        public int GetSpPoint(long musicId)
        {
            var record = Find(musicId, MusicDifficulty.Olivier);
            return record == null ? 0 :
                record.AchievementStar + record.AccuracyStar + record.LampStar;
        }

        public int RecordOlivierResult(LocalMusicEntry music, LocalLiveEntry live,
            double achievementRate, ClearLamp lamp, int nonPerfectStarCount,
            bool isCleared, bool isAuto, LiveType liveType = LiveType.Normal)
        {
            if (!OlivierStars.IsEligible(music, live) || !isCleared || isAuto ||
                liveType != LiveType.Normal) return 0;
            var a = OlivierStars.CalculateAchievementStar(live.Level, achievementRate);
            var b = OlivierStars.CalculateAccuracyStar(nonPerfectStarCount);
            var c = OlivierStars.CalculateLampStar(lamp);
            var record = Find(music.Id, MusicDifficulty.Olivier);
            if (record == null)
            {
                record = new Record { MusicId = music.Id, Difficulty = (int)MusicDifficulty.Olivier };
                _records.Add(record);
            }
            record.AchievementStar = Math.Max(record.AchievementStar, a);
            record.AccuracyStar = Math.Max(record.AccuracyStar, b);
            record.LampStar = Math.Max(record.LampStar, c);
            Save();
            return a + b + c;
        }

        public bool RecordResult(
            long musicId,
            MusicDifficulty difficulty,
            double achievementRate,
            ClearLamp clearLamp,
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
            MusicDifficulty difficulty,
            double achievementRate,
            out double previousBest) =>
            RecordResult(
                musicId,
                difficulty,
                achievementRate,
                ClearLamp.Clear,
                out previousBest);

        private Record Find(
            long musicId,
            MusicDifficulty difficulty)
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
                var json = SettingsCrypto.DecryptUtf8(
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
                _path, SettingsCrypto.EncryptUtf8(json));
        }
    }
}
