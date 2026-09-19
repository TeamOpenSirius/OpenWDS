using UnityEngine;
using UnityEngine.UI;

namespace OpenWDS.Runtime
{
    /// <summary>Resolution-independent horizontal three-dot menu icon.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class EllipsisIconGraphic : MaskableGraphic
    {
        public EllipsisIconGraphic() { useLegacyMeshGeneration = false; }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            var scale = Mathf.Min(rect.width, rect.height) / 100f;
            const int segments = 32;
            var transparent = color;
            transparent.a = 0;
            for (var dot = -1; dot <= 1; dot++)
            {
                var center = rect.center + new Vector2(dot * 28f * scale, 0);
                var start = mesh.currentVertCount;
                mesh.AddVert(center, color, Vector2.zero);
                for (var i = 0; i < segments; i++)
                {
                    var angle = i * Mathf.PI * 2f / segments;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    mesh.AddVert(center + direction * (8f * scale), color, Vector2.zero);
                    mesh.AddVert(center + direction * (9f * scale), transparent, Vector2.zero);
                }
                for (var i = 0; i < segments; i++)
                {
                    var inner = start + 1 + i * 2;
                    var next = start + 1 + ((i + 1) % segments) * 2;
                    mesh.AddTriangle(start, inner, next);
                    mesh.AddTriangle(inner, inner + 1, next + 1);
                    mesh.AddTriangle(inner, next + 1, next);
                }
            }
        }
    }
}
