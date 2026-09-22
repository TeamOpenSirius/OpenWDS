using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace OpenWDS.Runtime
{
    /// <summary>Offline, file-based import. One fixed resource directory, no release/version registry.</summary>
    public static class SongResourceImporter
    {
        [Serializable] public sealed class ResourceFile { public string Path; public long Size; public string Sha256; }
        [Serializable] public sealed class Manifest { public int Format; public ResourceFile[] Files; }
        [Serializable] public sealed class Change { public string Path; public bool HadOriginal; }
        [Serializable] public sealed class Journal { public Change[] Changes; }
        [Serializable] private sealed class SpecialCatalog { public AnotherNotationSelectionRuntime.Entry[] Entries; }
        private const string ManifestName = "manifest.json";
        private static string Transaction(string root) => root.TrimEnd(Path.DirectorySeparatorChar, '/') + ".import";
        private static string Target(string root, string path) => path == ManifestName
            ? System.IO.Path.Combine(root, path) : SongResourceStore.ResolveUnder(root, path);

        public static string ArchiveHash(string path)
        {
            using (var input = File.OpenRead(path))
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }
        public static void VerifyArchiveHash(string path, string expected)
        {
            if (string.IsNullOrEmpty(expected) || expected.Length != 64 ||
                !string.Equals(ArchiveHash(path), expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Resource ZIP does not match this app. Please select the supplied resource package. No resources were changed.");
        }
        public static void Recover(string root)
        {
            var transaction = Transaction(root);
            if (!Directory.Exists(transaction)) return;
            var journalPath = System.IO.Path.Combine(transaction, "journal.json");
            if (File.Exists(journalPath) && !File.Exists(System.IO.Path.Combine(transaction, "committed")))
            {
                var journal = JsonUtility.FromJson<Journal>(File.ReadAllText(journalPath));
                foreach (var change in journal.Changes.Reverse())
                {
                    var destination = Target(root, change.Path);
                    var backup = Target(System.IO.Path.Combine(transaction, "backup"), change.Path);
                    if (File.Exists(backup))
                    {
                        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination));
                        if (File.Exists(destination)) File.Delete(destination);
                        File.Move(backup, destination);
                    }
                    else if (!change.HadOriginal && File.Exists(destination)) File.Delete(destination);
                }
            }
            Directory.Delete(transaction, true);
        }

        private static Dictionary<string, ResourceFile> ValidateManifest(Manifest manifest)
        {
            if (manifest == null || manifest.Format != 1 || manifest.Files == null || manifest.Files.Length > 100000)
                throw new InvalidDataException("Unsupported resource manifest.");
            var files = new Dictionary<string, ResourceFile>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in manifest.Files)
            {
                SongResourceStore.ResolveUnder("validation", file.Path);
                if (file.Size < 0 || file.Sha256 == null || file.Sha256.Length != 64 ||
                    file.Sha256.Any(c => !(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) || files.ContainsKey(file.Path))
                    throw new InvalidDataException("Invalid/duplicate manifest entry: " + file.Path);
                files.Add(file.Path, file);
            }
            if (!files.ContainsKey("catalog.json") || !files.ContainsKey("OpenWDS/AnotherNotations/catalog.json"))
                throw new InvalidDataException("The song catalogs are missing.");
            return files;
        }

        private static bool Matches(string path, ResourceFile file)
        {
            if (!File.Exists(path) || new FileInfo(path).Length != file.Size) return false;
            using (var input = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant() == file.Sha256;
        }

        private static void Extract(ZipArchiveEntry entry, string path, ResourceFile expected)
        {
            if (entry.Length != expected.Size) throw new InvalidDataException("Wrong length: " + entry.FullName);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            using (var source = entry.Open())
            using (var destination = File.Create(path))
            {
                var buffer = new byte[128 * 1024];
                long total = 0;
                int length;
                while ((length = source.Read(buffer, 0, buffer.Length)) != 0)
                {
                    total = checked(total + length);
                    if (total > expected.Size) throw new InvalidDataException("Oversized entry: " + entry.FullName);
                    destination.Write(buffer, 0, length);
                }
                destination.Flush(true);
            }
            if (!Matches(path, expected)) throw new InvalidDataException("Damaged resource: " + entry.FullName);
        }

        public static void ValidateCatalogDependencies(string catalogJson, string specialJson, ISet<string> files)
        {
            var ordinary = LocalMusicCatalog.FromJson(catalogJson);
            var special = JsonUtility.FromJson<SpecialCatalog>(specialJson);
            if (special?.Entries == null) throw new InvalidDataException("Invalid special chart catalog.");
            foreach (var music in ordinary.Musics.Concat(special.Entries.Select(e => e.Music)))
            {
                var paths = new List<string> {
                    music.JacketAssetPath,
                    string.IsNullOrEmpty(music.MusicAcbPath) ? $"OpenWDS/StandardCharts/{music.Id}/cri/music_{music.Id}.acb.bundle" : music.MusicAcbPath,
                    string.IsNullOrEmpty(music.PreviewAcbPath) ? $"OpenWDS/StandardCharts/{music.Id}/cri/musicpreview_{music.Id}.acb.bundle" : music.PreviewAcbPath };
                foreach (var live in music.Lives)
                {
                    paths.Add(live.DebugNotationAssetPath);
                    paths.Add(live.DebugMusicConfigAssetPath);
                }
                foreach (var path in paths)
                {
                    SongResourceStore.ResolveUnder("validation", path);
                    if (!files.Contains(path)) throw new InvalidDataException("Missing song dependency: " + path);
                }
            }
        }

        public static void ValidateInstalled(string root)
        {
            Recover(root);
            var manifestPath = System.IO.Path.Combine(root, ManifestName);
            if (!File.Exists(manifestPath)) throw new FileNotFoundException("Import a local song resource ZIP first.");
            var files = ValidateManifest(JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath)));
            foreach (var file in files.Values)
            {
                var path = Target(root, file.Path);
                if (!File.Exists(path) || new FileInfo(path).Length != file.Size)
                    throw new InvalidDataException("Missing/damaged resource; import the resource ZIP again: " + file.Path);
            }
            ValidateCatalogDependencies(File.ReadAllText(Target(root, "catalog.json")),
                File.ReadAllText(Target(root, "OpenWDS/AnotherNotations/catalog.json")), new HashSet<string>(files.Keys, StringComparer.Ordinal));
        }

        /// <summary>Call off the main thread, only while no game or preview owns song files.</summary>
        public static void Import(string archivePath, string root, Action<string> progress = null)
        {
            Recover(root);
            var transaction = Transaction(root);
            var staged = System.IO.Path.Combine(transaction, "staged");
            Directory.CreateDirectory(staged);
            try
            {
                using (var archive = ZipFile.OpenRead(archivePath))
                {
                    var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith("/")) continue;
                        if (entry.FullName != ManifestName) SongResourceStore.ResolveUnder(root, entry.FullName);
                        if (entries.ContainsKey(entry.FullName)) throw new InvalidDataException("Duplicate ZIP entry.");
                        entries.Add(entry.FullName, entry);
                    }
                    if (!entries.TryGetValue(ManifestName, out var manifestEntry) || manifestEntry.Length > 16 * 1024 * 1024)
                        throw new InvalidDataException("Missing/oversized manifest.");
                    string json;
                    using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8)) json = reader.ReadToEnd();
                    var files = ValidateManifest(JsonUtility.FromJson<Manifest>(json));
                    if (entries.Keys.Any(path => path != ManifestName && !files.ContainsKey(path)))
                        throw new InvalidDataException("ZIP contains files not in the manifest.");
                    var changes = new List<Change>();
                    var index = 0;
                    foreach (var file in files.Values)
                    {
                        progress?.Invoke($"Importing resource files: {++index}/{files.Count}");
                        var destination = Target(root, file.Path);
                        if (Matches(destination, file)) continue;
                        if (!entries.TryGetValue(file.Path, out var entry))
                            throw new InvalidDataException("Delta needs an unchanged local file. Import the full package: " + file.Path);
                        Extract(entry, Target(staged, file.Path), file);
                        changes.Add(new Change { Path = file.Path, HadOriginal = File.Exists(destination) });
                    }
                    // Only known song files can be removed; unrelated user data is never enumerated.
                    foreach (var subdir in new[] { "OpenWDS/StandardCharts", "OpenWDS/AnotherNotations" })
                    {
                        var directory = System.IO.Path.Combine(root, subdir);
                        if (!Directory.Exists(directory)) continue;
                        foreach (var path in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                        {
                            var relative = path.Substring(root.TrimEnd('/', '\\').Length + 1).Replace('\\', '/');
                            if (!files.ContainsKey(relative))
                            {
                                SongResourceStore.ResolveUnder(root, relative);
                                changes.Add(new Change { Path = relative, HadOriginal = true });
                            }
                        }
                    }
                    string ReadCandidate(string relative) => File.ReadAllText(File.Exists(Target(staged, relative)) ? Target(staged, relative) : Target(root, relative));
                    ValidateCatalogDependencies(ReadCandidate("catalog.json"), ReadCandidate("OpenWDS/AnotherNotations/catalog.json"),
                        new HashSet<string>(files.Keys, StringComparer.Ordinal));
                    File.WriteAllText(Target(staged, ManifestName), json);
                    changes.Add(new Change { Path = ManifestName, HadOriginal = File.Exists(Target(root, ManifestName)) });
                    var journal = JsonUtility.ToJson(new Journal { Changes = changes.ToArray() });
                    using (var output = new FileStream(System.IO.Path.Combine(transaction, "journal.json.tmp"), FileMode.Create))
                    {
                        var bytes = Encoding.UTF8.GetBytes(journal); output.Write(bytes, 0, bytes.Length); output.Flush(true);
                    }
                    File.Move(System.IO.Path.Combine(transaction, "journal.json.tmp"), System.IO.Path.Combine(transaction, "journal.json"));
                    progress?.Invoke("Applying resource files...");
                    foreach (var change in changes)
                    {
                        var destination = Target(root, change.Path);
                        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination));
                        if (change.HadOriginal)
                        {
                            var backup = Target(System.IO.Path.Combine(transaction, "backup"), change.Path);
                            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(backup));
                            File.Move(destination, backup);
                        }
                        var source = Target(staged, change.Path);
                        if (File.Exists(source)) File.Move(source, destination);
                    }
                    using (var marker = new FileStream(System.IO.Path.Combine(transaction, "committed"), FileMode.Create)) marker.Flush(true);
                }
                Recover(root);
            }
            catch
            {
                Recover(root);
                throw;
            }
        }
    }
}
