using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Reads one canonical copy from StreamingAssets on both normal filesystems
    /// and Android's read-only APK/JAR URI boundary.
    /// </summary>
    public static class StreamingAssetsRuntime
    {
        public static IEnumerator ReadBytes(
            string relativePath,
            Action<byte[]> completed)
        {
            if (string.IsNullOrEmpty(relativePath))
                throw new ArgumentException(
                    "StreamingAssets path must not be empty.", nameof(relativePath));
            if (completed == null)
                throw new ArgumentNullException(nameof(completed));

            var root = Application.streamingAssetsPath;
            if (!root.Contains("://"))
            {
                var path = Path.Combine(root, relativePath);
                if (!File.Exists(path))
                    throw new FileNotFoundException(
                        "StreamingAssets resource is missing.", path);
                completed(File.ReadAllBytes(path));
                yield break;
            }

            var uri = root.TrimEnd('/') + "/" + relativePath.Replace('\\', '/');
            using (var request = UnityWebRequest.Get(uri))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                    throw new IOException(
                        $"StreamingAssets read failed: {relativePath}: " +
                        request.error);
                completed(request.downloadHandler.data);
            }
        }
    }
}
