using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace OpenWDS.Editor
{
    internal static class PresentationRenderingValidation
    {
        // Compare the actual camera render with just this presentation's
        // renderers disabled. A curtain or lane cannot satisfy this assertion.
        internal static int Capture(Camera camera, Renderer[] renderers, string filename)
        {
            if (camera == null || renderers.Length == 0) throw new InvalidOperationException("Missing presentation camera/renderers.");
            const int width = 1280, height = 800;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var oldAspect = camera.aspect;
            var enabled = renderers.Select(r => r.enabled).ToArray();
            try
            {
                camera.targetTexture = target;
                camera.aspect = width / (float)height;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                var visible = texture.GetPixels32();
                var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../reverse/reports", filename));
                File.WriteAllBytes(output, texture.EncodeToPNG());
                foreach (var renderer in renderers) renderer.enabled = false;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                var hidden = texture.GetPixels32();
                var changed = 0;
                var magenta = 0;
                for (var i = 0; i < visible.Length; i++)
                {
                    var a = visible[i]; var b = hidden[i];
                    if (Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b) <= 20) continue;
                    changed++;
                    if (a.r > 245 && a.g < 10 && a.b > 245) magenta++;
                }
                if (changed < 1000 || magenta > 0)
                    throw new InvalidOperationException($"Presentation render failed: {filename}, changed={changed}, magenta={magenta}");
                return changed;
            }
            finally
            {
                for (var i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = enabled[i];
                camera.targetTexture = oldTarget;
                camera.aspect = oldAspect;
                RenderTexture.active = oldActive;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
