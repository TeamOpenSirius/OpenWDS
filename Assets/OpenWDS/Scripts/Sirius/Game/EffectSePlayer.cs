using OpenWDS.Runtime;
using UnityEngine;

namespace Sirius
{
    /// <summary>
    /// Serialization-compatible receiver for authored effect-SE events.
    /// Retail methods publish to SeMessageBroker; the recovered shared runtime
    /// is the scene-local broker/SePlayer consumer backed by the original bank.
    /// </summary>
    public sealed class EffectSePlayer : MonoBehaviour
    {
        public void OnSEPlay(string cueName)
        {
            RequireSharedPlayer().Play(cueName);
        }

        public void OnSEPlayLoop(string cueName)
        {
            RequireSharedPlayer().PlayLoop(cueName);
        }

        public void OnSEStop(string cueName)
        {
            RequireSharedPlayer().Stop(cueName);
        }

        public void OnSEStopAll()
        {
            RequireSharedPlayer().StopAll();
        }

        private static UiSeRuntime RequireSharedPlayer()
        {
            if (UiSeRuntime.Instance != null)
                return UiSeRuntime.Instance;
            var scenePlayer =
                Object.FindObjectOfType<UiSeRuntime>();
            if (scenePlayer != null) return scenePlayer;
            throw new System.InvalidOperationException(
                "EffectSePlayer requires the recovered shared SE runtime.");
        }
    }
}
