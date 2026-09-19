using UnityEngine;

namespace OpenWDS.Runtime
{
    // Shared native MusicSelectionListCellBase geometry (168 -> 196, spacing 20).
    public static class MusicSelectionListGeometry
    {
        public const float OffsetHeight = 168f;
        public const float TargetHeight = 196f;
        public const float Spacing = 20f;
        public static float FocusDisplacement(int index, int focusIndex, float targetHeight = TargetHeight) =>
            index < focusIndex ? (targetHeight - OffsetHeight) * .5f :
            index > focusIndex ? -(targetHeight - OffsetHeight) * .5f : 0f;
        public static float Curve(float distance) => Mathf.Min(100f, Mathf.Abs(distance) * .1042f - 20f);
    }
}
