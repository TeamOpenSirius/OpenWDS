using UnityEngine;

namespace Sirius.ClientAssetsShared.Extensions
{
    public static class TransformExtensions
    {
        // Current ARM64 0x59826c0 / 0x59827dc. Preserve the original Z.
        public static void SetPositionXY(this Transform self, float x, float y)
        {
            var position = self.position;
            position.x = x;
            position.y = y;
            self.position = position;
        }
        public static void SetLocalPositionXY(this Transform self, float x, float y)
        {
            var position = self.localPosition;
            position.x = x;
            position.y = y;
            self.localPosition = position;
        }
    }
}
