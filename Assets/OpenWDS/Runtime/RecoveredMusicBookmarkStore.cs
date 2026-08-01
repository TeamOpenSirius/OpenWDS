using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace OpenWDS.Runtime
{
    [Flags]
    public enum RecoveredMusicBookmarkFlags
    {
        None = 0,
        Bookmark1 = 1,
        Bookmark2 = 2,
        Bookmark3 = 4,
    }

    /// <summary>
    /// Local durable substitute for the server-backed MusicBookmark flags.
    /// </summary>
    public sealed class RecoveredMusicBookmarkStore
    {
        private const string FileName = "OpenWDSMusicBookmarks";
        private readonly string _path;
        private readonly Dictionary<long, int> _flags;

        public RecoveredMusicBookmarkStore(string persistentDataPath = null)
        {
            var root = string.IsNullOrEmpty(persistentDataPath)
                ? Application.persistentDataPath
                : persistentDataPath;
            var directory = Path.Combine(
                root, RecoveredSettingsPersistence.CurrentDirectory);
            _path = Path.Combine(directory, FileName);
            _flags = Load();
        }

        public RecoveredMusicBookmarkFlags Get(long musicId) =>
            _flags.TryGetValue(musicId, out var value)
                ? (RecoveredMusicBookmarkFlags)value
                : RecoveredMusicBookmarkFlags.None;

        public bool Contains(long musicId, RecoveredMusicBookmarkFlags flag) =>
            flag == RecoveredMusicBookmarkFlags.None ||
            (Get(musicId) & flag) != 0;

        public int Count(RecoveredMusicBookmarkFlags flag)
        {
            var count = 0;
            foreach (var value in _flags.Values)
            {
                if (((RecoveredMusicBookmarkFlags)value & flag) != 0) count++;
            }
            return count;
        }

        public void Set(long musicId, RecoveredMusicBookmarkFlags flags)
        {
            if (flags == RecoveredMusicBookmarkFlags.None)
                _flags.Remove(musicId);
            else
                _flags[musicId] = (int)flags;
            Save();
        }

        private Dictionary<long, int> Load()
        {
            if (!File.Exists(_path)) return new Dictionary<long, int>();
            try
            {
                return JsonConvert.DeserializeObject<Dictionary<long, int>>(
                           File.ReadAllText(_path))
                       ?? new Dictionary<long, int>();
            }
            catch
            {
                return new Dictionary<long, int>();
            }
        }

        private void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(
                _path,
                JsonConvert.SerializeObject(_flags, Formatting.Indented));
        }
    }
}
