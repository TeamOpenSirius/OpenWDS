using UnityEngine;

namespace Sirius.Game
{
    public sealed class GameBackgroundJacket : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _jacketRenderer;

        private void Awake()
        {
            if (_jacketRenderer == null) _jacketRenderer = GetComponent<SpriteRenderer>();
        }

        // Recovered from GameBackgroundJacket.Initialize at RVA 0xB926D9C.
        public void Initialize(Sprite jacketSprite)
        {
            if (_jacketRenderer == null) return;
            _jacketRenderer.enabled = jacketSprite != null;
            _jacketRenderer.sprite = jacketSprite;
        }
    }
}
