using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace OpenWDS.Runtime
{
    [Flags]
    public enum MusicBookmarkFlags
    {
        None = 0,
        Bookmark1 = 1,
        Bookmark2 = 2,
        Bookmark3 = 4,
    }

    /// <summary>
    /// Local durable substitute for the server-backed MusicBookmark flags.
    /// </summary>
    public sealed class MusicBookmarkStore
    {
        private const string FileName = "OpenWDSMusicBookmarks";
        private readonly string _path;
        private readonly Dictionary<long, int> _flags;

        public MusicBookmarkStore(string persistentDataPath = null)
        {
            var root = string.IsNullOrEmpty(persistentDataPath)
                ? Application.persistentDataPath
                : persistentDataPath;
            var directory = Path.Combine(
                root, SettingsPersistence.CurrentDirectory);
            _path = Path.Combine(directory, FileName);
            _flags = Load();
        }

        public MusicBookmarkFlags Get(long musicId) =>
            _flags.TryGetValue(musicId, out var value)
                ? (MusicBookmarkFlags)value
                : MusicBookmarkFlags.None;

        public bool Contains(long musicId, MusicBookmarkFlags flag) =>
            flag == MusicBookmarkFlags.None ||
            (Get(musicId) & flag) != 0;

        public int Count(MusicBookmarkFlags flag)
        {
            var count = 0;
            foreach (var value in _flags.Values)
            {
                if (((MusicBookmarkFlags)value & flag) != 0) count++;
            }
            return count;
        }

        public void Set(long musicId, MusicBookmarkFlags flags)
        {
            if (flags == MusicBookmarkFlags.None)
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
