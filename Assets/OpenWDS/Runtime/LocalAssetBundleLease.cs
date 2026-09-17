using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenWDS.Runtime
{
    /// <summary>Shares local bundles by their canonical source-catalog path across retained scenes.</summary>
    public sealed class LocalAssetBundleLease : IDisposable
    {
        private sealed class Entry
        {
            public string Key;
            public int References;
            public AssetBundle Bundle;
            public AssetBundleCreateRequest Request;
            public AssetBundle Resolve()
            {
                if (Request != null)
                {
                    Bundle = Request.assetBundle;
                    Request = null;
                }
                return Bundle;
            }
        }

        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private Entry _entry;
        public AsyncOperation LoadOperation => _entry?.Request;
        public AssetBundle Bundle => _entry?.Resolve();

        private LocalAssetBundleLease(Entry entry)
        {
            _entry = entry;
            entry.References++;
        }

        public static LocalAssetBundleLease FromMemory(string sourceKey, byte[] bytes)
        {
            if (!Entries.TryGetValue(sourceKey, out var entry))
            {
                entry = new Entry { Key = sourceKey, Request = AssetBundle.LoadFromMemoryAsync(bytes) };
                Entries.Add(sourceKey, entry);
            }
            return new LocalAssetBundleLease(entry);
        }

        public static LocalAssetBundleLease FromFile(string sourceKey, string path)
        {
            if (!Entries.TryGetValue(sourceKey, out var entry))
            {
                entry = new Entry { Key = sourceKey, Bundle = AssetBundle.LoadFromFile(path) };
                Entries.Add(sourceKey, entry);
            }
            return new LocalAssetBundleLease(entry);
        }

        public void Dispose()
        {
            var entry = _entry;
            if (entry == null) return;
            _entry = null;
            if (--entry.References != 0) return;
            if (entry.Request != null && !entry.Request.isDone)
                entry.Request.completed += _ => ReleaseUnused(entry);
            else ReleaseUnused(entry);
        }

        private static void ReleaseUnused(Entry entry)
        {
            // A retained/returning scene may have acquired this pending load
            // again before its completion callback runs.
            if (entry.References != 0) return;
            var bundle = entry.Resolve();
            if (bundle != null) bundle.Unload(false);
            if (Entries.TryGetValue(entry.Key, out var current) && ReferenceEquals(current, entry))
                Entries.Remove(entry.Key);
        }
    }
}
