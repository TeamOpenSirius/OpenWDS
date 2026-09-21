using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenWDS.Runtime
{
    /// <summary>Navigate within the shared Main host; standalone recovery scenes retain a bootstrap bridge.</summary>
    public static class FrontendNavigation
    {
        public static bool EnterSelection { get; private set; }
        public static LocalMusicSelection ReturnedSelection { get; private set; }
        public static bool IsJumpFromResult { get; private set; }
        public static void PrepareSelectionReturn(LocalMusicSelection selection, bool fromResult)
        {
            ReturnedSelection = selection;
            IsJumpFromResult = fromResult;
            EnterSelection = true;
            EnterAnother = (selection?.Live.AnotherNotationId ?? 0) > 0;
        }
        public static bool ConsumeSelection() { bool value = EnterSelection; EnterSelection = false; return value; }
        public static bool EnterHome { get; private set; }
        public static bool EnterAnother { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { EnterHome = false; EnterAnother = false; EnterSelection = false; ReturnedSelection = null; IsJumpFromResult = false; }
        public static bool ConsumeHome() { bool value = EnterHome; EnterHome = false; return value; }
        public static bool ConsumeAnother() { bool value = EnterAnother; EnterAnother = false; return value; }
        public static void Home()
        {
            if (MainPageNavigationRuntime.Instance != null) { MainPageNavigationRuntime.Instance.OpenHome(); return; }
            EnterHome = true;
            SceneManager.LoadSceneAsync("FrontendRecovery");
        }
        public static void Selection(bool another)
        {
            ReturnedSelection = null; IsJumpFromResult = false;
            EnterAnother = another;
            if (MainPageNavigationRuntime.Instance != null) { MainPageNavigationRuntime.Instance.OpenSelection(); return; }
            SceneManager.LoadSceneAsync("LocalMusicSelection");
        }
    }
}
