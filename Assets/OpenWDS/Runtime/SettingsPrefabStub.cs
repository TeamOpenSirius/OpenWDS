using UnityEngine;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Serialization host for original Sirius UI components whose behavior is
    /// supplied by GamePauseRuntime. Keeping one concrete MonoScript
    /// avoids Missing Script components without importing AssetRipper dummies.
    /// </summary>
    public sealed class SettingsPrefabStub : MonoBehaviour
    {
    }
}
