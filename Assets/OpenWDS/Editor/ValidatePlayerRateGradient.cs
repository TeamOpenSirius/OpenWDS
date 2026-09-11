using System;
using System.Collections.Generic;
using OpenWDS.Runtime;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenWDS.Editor
{
    public static class ValidatePlayerRateGradient
    {
        public static void Run()
        {
            var canvas = new GameObject("Rating gradient validation", typeof(Canvas));
            var label = new GameObject("RateText", typeof(RectTransform), typeof(Text), typeof(RecoveredPlayerRateGradient));
            try
            {
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                label.transform.SetParent(canvas.transform, false);
                var text = label.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 32;
                text.text = "275.00";
                text.rectTransform.sizeDelta = new Vector2(500, 100);
                var effect = label.GetComponent<RecoveredPlayerRateGradient>();
                foreach (var rate in new[] { 199.99, 200, 249.99, 250, 300, 799.99, 800, 1000 })
                {
                    effect.SetRate(rate);
                    Canvas.ForceUpdateCanvases();
                    var mesh = text.canvasRenderer.GetMesh();
                    if (mesh.vertexCount == 0) throw new InvalidOperationException("Rating text did not generate vertices.");
                    var vertices = mesh.vertices;
                    var colors = mesh.colors32;
                    var high = float.MinValue; var low = float.MaxValue;
                    foreach (var vertex in vertices) { high = Mathf.Max(high, vertex.y); low = Mathf.Min(low, vertex.y); }
                    if (high <= low) throw new InvalidOperationException("Rating mesh is degenerate.");
                    for (var i = 0; i < vertices.Length; i++)
                    {
                        if (Mathf.Abs(vertices[i].y-high) < .01f && !colors[i].Equals(effect.Top))
                            throw new InvalidOperationException("Rating upper vertex color was lost.");
                        if (Mathf.Abs(vertices[i].y-low) < .01f && !colors[i].Equals(effect.Bottom))
                            throw new InvalidOperationException("Rating lower vertex color was lost.");
                    }
                }
                effect.SetRate(275);
                if (!effect.Top.Equals(new Color32(170,226,27,255)) || !effect.Bottom.Equals(new Color32(255,255,255,255)))
                    throw new InvalidOperationException("Retail 250-300 gradient is incorrect.");
                Debug.Log("OPENWDS_PLAYER_RATE_GRADIENT_VALIDATED actualTextMesh=true tiers=8");
            }
            finally { Object.DestroyImmediate(canvas); }
        }
    }
}
