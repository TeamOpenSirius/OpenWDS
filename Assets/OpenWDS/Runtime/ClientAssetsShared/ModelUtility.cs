using UnityEngine;

namespace Sirius.Scarab
{
    public static class ModelUtility
    {
        // 0x597254c: root-inclusive depth-first search, first match in child order.
        public static Transform FindTransformByName(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var result = FindTransformByName(root.GetChild(i), name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
