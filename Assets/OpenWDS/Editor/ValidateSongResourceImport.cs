using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using OpenWDS.Runtime;
using UnityEngine;

namespace OpenWDS.Editor
{
    public static class ValidateSongResourceImport
    {
        private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
        private static void Require(bool value, string message) { if (!value) throw new Exception("Song import: " + message); }
        private static void Reject(Action action, string message)
        {
            try { action(); } catch (InvalidDataException) { return; }
            throw new Exception("Song import accepted " + message);
        }
        private static void WriteZip(string path, Dictionary<string, byte[]> files, HashSet<string> included = null, bool corrupt = false, bool traversal = false)
        {
            var manifest = new SongResourceImporter.Manifest { Format = 1, Files = files.Select(kv =>
                new SongResourceImporter.ResourceFile { Path = kv.Key, Size = kv.Value.Length, Sha256 = null }).ToArray() };
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                using (var stream = archive.CreateEntry("manifest.json").Open())
                {
                    var bytes = Bytes(JsonUtility.ToJson(manifest)); stream.Write(bytes, 0, bytes.Length);
                }
                foreach (var kv in files)
                {
                    if (included != null && !included.Contains(kv.Key)) continue;
                    using (var stream = archive.CreateEntry(kv.Key).Open())
                    {
                        var bytes = (byte[])kv.Value.Clone();
                        if (corrupt) bytes = bytes.Take(bytes.Length - 1).ToArray();
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }
                if (traversal) archive.CreateEntry("../escape");
            }
        }
        public static void RunRelease()
        {
            ConfigureOfflineSongResources.Run();
            var archive = Path.GetFullPath("Build/Resources/OpenWDS-resources.zip");
            var imported = Path.Combine(Path.GetTempPath(), "openwds-real-import-" + Guid.NewGuid().ToString("N"));
            try
            {
                System.Threading.Tasks.Task.Run(() => SongResourceImporter.Import(archive, imported)).GetAwaiter().GetResult();
                SongResourceImporter.ValidateInstalled(imported);
                var manifest = JsonUtility.FromJson<SongResourceImporter.Manifest>(File.ReadAllText(Path.Combine(imported, "manifest.json")));
                Require(manifest.Files.Length > 0 && Directory.GetFiles(imported, "*.meta", SearchOption.AllDirectories).Length == 0, "release inventory");
                var report = Path.GetFullPath("../../reverse/reports/offline-song-resources.json");
                File.WriteAllText(report, "{\"passed\":true,\"files\":" + manifest.Files.Length + ",\"bytes\":" + manifest.Files.Sum(f => f.Size) + ",\"metadataFiles\":0}\n");
                Debug.Log("OPENWDS_SONG_RELEASE_IMPORT passed=True files=" + manifest.Files.Length);
            }
            finally
            {
                if (Directory.Exists(imported)) Directory.Delete(imported, true);
                if (Directory.Exists(imported + ".import")) Directory.Delete(imported + ".import", true);
            }
            ValidateAssets.Run();
            ValidateCriAudioRuntime.Run();
        }

        public static void Run()
        {
            var temporary = Path.Combine(Path.GetTempPath(), "openwds-song-import-" + Guid.NewGuid().ToString("N"));
            var root = Path.Combine(temporary, "SongResources");
            Directory.CreateDirectory(temporary);
            try
            {
                // Synthetic payload bytes test transaction behavior, not asset/audio validity.
                var catalog = LocalMusicCatalog.FromJson(SongResourceStore.CatalogJson);
                var music = catalog.Musics[0]; music.Lives = new[] { music.Lives[0] }; catalog.Musics = new[] { music };
                var chart = music.Lives[0].DebugNotationAssetPath;
                var files = new Dictionary<string, byte[]> {
                    { "catalog.json", Bytes(JsonUtility.ToJson(catalog)) },
                    { "OpenWDS/AnotherNotations/catalog.json", Bytes("{\"Entries\":[]}") },
                    { music.JacketAssetPath, Bytes("fixture jacket") },
                    { $"OpenWDS/StandardCharts/{music.Id}/cri/music_{music.Id}.acb.bundle", Bytes("fixture song") },
                    { $"OpenWDS/StandardCharts/{music.Id}/cri/musicpreview_{music.Id}.acb.bundle", Bytes("fixture preview") },
                    { chart, Bytes("fixture chart") },
                    { music.Lives[0].DebugMusicConfigAssetPath, Bytes("fixture config") },
                    { "OpenWDS/StandardCharts/unused.bin", Bytes("remove me") }
                };
                var full = Path.Combine(temporary, "full.zip"); WriteZip(full, files);
                using (var zip = ZipFile.Open(full, ZipArchiveMode.Update)) zip.GetEntry("manifest.json").Delete();
                SongResourceImporter.Import(full, root); SongResourceImporter.ValidateInstalled(root);
                Require(File.ReadAllText(Path.Combine(root, chart)) == "fixture chart", "full import");
                files[chart] = Bytes("updated chart"); files.Remove("OpenWDS/StandardCharts/unused.bin");
                var delta = Path.Combine(temporary, "delta.zip"); WriteZip(delta, files, new HashSet<string> { chart });
                SongResourceImporter.Import(delta, root); SongResourceImporter.ValidateInstalled(root);
                Require(File.ReadAllText(Path.Combine(root, chart)) == "updated chart", "delta replacement");
                Require(!File.Exists(Path.Combine(root, "OpenWDS/StandardCharts/unused.bin")), "removed stale file");
                var brokenRoot = Path.Combine(temporary, "MissingBase");
                Reject(() => SongResourceImporter.Import(delta, brokenRoot), "delta without baseline");
                files[chart] = Bytes("changed chart");
                var sameSize = Path.Combine(temporary, "same-size.zip"); WriteZip(sameSize, files, new HashSet<string> { chart });
                SongResourceImporter.Import(sameSize, root);
                Require(File.ReadAllText(Path.Combine(root, chart)) == "changed chart", "same-size replacement without hash");
                var corrupt = Path.Combine(temporary, "corrupt.zip"); WriteZip(corrupt, files, new HashSet<string> { chart }, corrupt: true);
                Reject(() => SongResourceImporter.Import(corrupt, root), "truncated entry");
                Require(File.ReadAllText(Path.Combine(root, chart)) == "changed chart", "failed import preserved existing files");
                var traversal = Path.Combine(temporary, "traversal.zip"); WriteZip(traversal, files, new HashSet<string>(), traversal: true);
                Reject(() => SongResourceImporter.Import(traversal, root), "ZIP path traversal");
                Reject(() => SongResourceStore.ResolveUnder(root, "OpenWDS/Frontend/1.bundle"), "non-song payload");
                Reject(() => SongResourceStore.ResolveUnder(root, "OpenWDS/StandardCharts/1.csv.meta"), "Unity metadata");
                // Model a crash after moving an original to backup and committing one replacement/new file.
                var transaction = root + ".import";
                var backup = Path.Combine(transaction, "backup", chart);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.Move(Path.Combine(root, chart), backup);
                File.WriteAllText(Path.Combine(root, chart), "interrupted write");
                var extra = "OpenWDS/StandardCharts/new.bin";
                File.WriteAllText(Path.Combine(root, extra), "interrupted new file");
                File.WriteAllText(Path.Combine(transaction, "journal.json"), JsonUtility.ToJson(new SongResourceImporter.Journal {
                    Changes = new[] { new SongResourceImporter.Change { Path = chart, HadOriginal = true },
                        new SongResourceImporter.Change { Path = extra, HadOriginal = false } } }));
                SongResourceImporter.Recover(root); SongResourceImporter.Recover(root);
                Require(File.ReadAllText(Path.Combine(root, chart)) == "changed chart" && !File.Exists(Path.Combine(root, extra)), "crash rollback");
                SongResourceImporter.ValidateInstalled(root);
                File.Delete(Path.Combine(root, "manifest.json"));
                SongResourceImporter.ValidateInstalled(root);
                File.Delete(Path.Combine(root, chart));
                Reject(() => SongResourceImporter.ValidateInstalled(root), "missing catalog dependency");
                Debug.Log("OPENWDS_SONG_IMPORT passed=True full=True delta=True missingBaseRejected=True corruptRejected=True traversalRejected=True rollback=True metadataRejected=True");
            }
            finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
        }
    }
}
