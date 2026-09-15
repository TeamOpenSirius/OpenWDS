using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OpenWDS.Runtime
{
    // RateDataPanel.SetPlayerRate -> ColorPreset.PlayerRateColor ->
    // GradientText.Gradation. This palette differs from ColorHelper.RateColor.
    public sealed class PlayerRateGradient : BaseMeshEffect
    {
        private static readonly int[] TierColors = { 0xAAE21B, 0x00C3DC, 0x698CDC,
            0xAE81FF, 0xFF5498, 0xFE3D3D };
        private readonly List<UIVertex> _vertices = new List<UIVertex>();
        public Color32 Top { get; private set; } = new Color32(255, 255, 255, 255);
        public Color32 Bottom { get; private set; } = new Color32(255, 255, 255, 255);

        public void SetRate(double rate)
        {
            GetColors(rate, out var top, out var bottom);
            Top = top;
            Bottom = bottom;
            graphic.SetVerticesDirty();
        }

        public static void GetColors(double rate, out Color32 top, out Color32 bottom)
        {
            if (rate < 200d) { top = bottom = Rgb(0xFFFFFF); return; }
            if (rate < 800d)
            {
                var tier = (int)(rate / 100d) - 2;
                top = Rgb(TierColors[tier]);
                bottom = rate >= (tier + 2) * 100d + 50d
                    ? Rgb(0xFFFFFF) : top;
                return;
            }
            if (rate < 850d) { top = Rgb(0xDE7446); bottom = Rgb(0xF8C9B5); }
            else if (rate < 900d) { top = Rgb(0xBAC2C2); bottom = Rgb(0xECF1F2); }
            else if (rate < 950d) { top = Rgb(0xECCD6D); bottom = Rgb(0xFFEEA9); }
            else if (rate < 1000d) { top = Rgb(0xF9AE38); bottom = Rgb(0xFEEF6F); }
            else { top = Rgb(0xFF6EE9); bottom = Rgb(0x81FFF5); }
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            _vertices.Clear();
            vh.GetUIVertexStream(_vertices);
            for (var i = 0; i < _vertices.Count; i++)
            {
                var vertex = _vertices[i];
                var corner = i % 6;
                vertex.color = corner < 2 || corner == 5 ? Top : Bottom;
                _vertices[i] = vertex;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(_vertices);
        }

        private static Color32 Rgb(int value) => new Color32(
            (byte)(value >> 16), (byte)(value >> 8), (byte)value, 255);
    }
}
