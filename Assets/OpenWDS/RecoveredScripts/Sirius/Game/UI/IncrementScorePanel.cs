using UnityEngine;

namespace Sirius.Game.UI
{
    public sealed class IncrementScorePanel : MonoBehaviour
    {
        [SerializeField] private IncrementScoreCount _incrementScoreCount;

        public void Initialize()
        {
            _incrementScoreCount.SetText(12345678901L);
            _incrementScoreCount.OnPlay();
        }

        public void FireIncrementScore(long incrementScore)
        {
            _incrementScoreCount.SetText(incrementScore);
            _incrementScoreCount.OnPlay();
        }

        public void CompleteInitialization()
        {
            _incrementScoreCount.OnExit();
        }
    }
}
