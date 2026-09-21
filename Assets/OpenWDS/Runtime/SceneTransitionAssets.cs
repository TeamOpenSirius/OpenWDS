using Spine.Unity;
using UnityEngine;

namespace OpenWDS.Runtime
{
    // Launcher-owned assets, also available when opening the Game development scene directly.
    public sealed class SceneTransitionAssets : ScriptableObject
    {
        public SkeletonDataAsset Curtain;
        public Material CurtainMaterial;
    }
}
