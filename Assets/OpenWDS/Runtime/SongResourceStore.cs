using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace OpenWDS.Runtime
{
    /// <summary>Song payloads only. Shared UI, models and SE remain in StreamingAssets.</summary>
    public static class SongResourceStore
    {
        public static string Root
        {
            get
            {
#if UNITY_EDITOR
                return Path.GetFullPath(Path.Combine(Application.dataPath, "../SongResources"));
#elif UNITY_ANDROID
                var defaultRoot = Path.Combine(Application.persistentDataPath, "SongResources");
                var configured = PlayerPrefs.GetString("OpenWDS.SongResourceDirectory", "");
                // Only app-owned storage is supported; recover stale paths after a restore.
                return string.Equals(configured, defaultRoot, StringComparison.Ordinal) ? configured : defaultRoot;
#else
                return Path.GetFullPath(Path.Combine(Application.dataPath, "../SongResources"));
#endif
            }
        }

        public static string CatalogPath => Resolve("catalog.json");
        private static string _catalogJson;
        // Consumers may filter/mutate a parsed catalog, so share the text snapshot, not its arrays.
        public static string CatalogJson => _catalogJson ?? (_catalogJson = File.ReadAllText(CatalogPath));
        public static void Invalidate() { _catalogJson = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { Invalidate(); }

        public static string Resolve(string relativePath) => ResolveUnder(Root, relativePath);

        public static string ResolveUnder(string root, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath) || relativePath.Contains("\\") ||
                relativePath.Contains(":") || relativePath.StartsWith("/") ||
                relativePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Invalid song path: " + relativePath);
            foreach (var part in relativePath.Split('/'))
                if (part.Length == 0 || part == "." || part == "..")
                    throw new InvalidDataException("Invalid song path: " + relativePath);
            if (relativePath != "catalog.json" &&
                !relativePath.StartsWith("OpenWDS/StandardCharts/", StringComparison.Ordinal) &&
                !relativePath.StartsWith("OpenWDS/AnotherNotations/", StringComparison.Ordinal))
                throw new InvalidDataException("Not a song resource: " + relativePath);
            return Path.Combine(root, relativePath);
        }

        public static IEnumerator ReadBytes(string relativePath, Action<byte[]> completed)
        {
            if (completed == null) throw new ArgumentNullException(nameof(completed));
            completed(File.ReadAllBytes(Resolve(relativePath)));
            yield break;
        }
    }
}
