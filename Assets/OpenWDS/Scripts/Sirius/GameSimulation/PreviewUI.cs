using Sirius.Game;
using UnityEngine;

namespace Sirius.GameSimulation
{
    /// <summary>
    /// Serialized view recovered from Sirius.GameSimulation.PreviewUI.
    /// GameSimulationPreview supplies the stripped presenter/tick loop.
    /// </summary>
    public sealed class PreviewUI : MonoBehaviour
    {
        [SerializeField] private Animator _timingAnimator;
        [SerializeField] private Animator _senseAnimator;
        [SerializeField] private BombController _bombController;
        [SerializeField] private Transform _timingTransform;

        public Animator TimingAnimator => _timingAnimator;
        public Animator SenseAnimator => _senseAnimator;
        public BombController BombController => _bombController;
        public Transform TimingTransform => _timingTransform;
    }
}
