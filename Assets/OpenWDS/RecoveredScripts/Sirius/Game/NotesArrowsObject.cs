using System;
using UnityEngine;

namespace Sirius.Game
{
    // Field layout and behavior recovered from Sirius.dll metadata and ARM64 code.
    public class NotesArrowsObject : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] _leftArrowSprites;
        [SerializeField] private SpriteRenderer[] _rightArrowSprites;

        private Transform[] _leftArrowTransforms;
        private Transform[] _rightArrowTransforms;
        private bool[] _arrowEnables;
        private int _arrowCount;

        public void Initialize()
        {
            _arrowCount = Math.Min(
                _leftArrowSprites == null ? 0 : _leftArrowSprites.Length,
                _rightArrowSprites == null ? 0 : _rightArrowSprites.Length
            );
            _leftArrowTransforms = ToTransforms(_leftArrowSprites, _arrowCount);
            _rightArrowTransforms = ToTransforms(_rightArrowSprites, _arrowCount);
            _arrowEnables = new bool[_arrowCount];
            for (var index = 0; index < _arrowCount; index++)
            {
                // The recovered Initialize lambda returns true for every entry.
                _arrowEnables[index] = true;
            }
            ActivateArrowSpriteRenderer(0);
        }

        public void ActivateArrowSpriteRenderer(int activeCount)
        {
            activeCount = Mathf.Clamp(activeCount, 0, _arrowCount);
            for (var index = 0; index < _arrowCount; index++)
            {
                var enabled = index < activeCount;
                if (_arrowEnables[index] == enabled)
                {
                    continue;
                }
                _leftArrowSprites[index].enabled = enabled;
                _rightArrowSprites[index].enabled = enabled;
                _arrowEnables[index] = enabled;
            }
        }

        public void SetArrowPositions(float arrowInterval)
        {
            for (var index = 0; index < _arrowCount; index++)
            {
                SetLocalPositionX(_leftArrowTransforms[index], index * arrowInterval);
                SetLocalPositionX(_rightArrowTransforms[index], index * arrowInterval);
            }
        }

        public void SetArrowSprites(Sprite arrowSprite)
        {
            for (var index = 0; index < _arrowCount; index++)
            {
                _leftArrowSprites[index].sprite = arrowSprite;
                _rightArrowSprites[index].sprite = arrowSprite;
            }
        }

        private static Transform[] ToTransforms(SpriteRenderer[] renderers, int count)
        {
            var result = new Transform[count];
            for (var index = 0; index < count; index++)
            {
                result[index] = renderers[index].transform;
            }
            return result;
        }

        private static void SetLocalPositionX(Transform target, float value)
        {
            var position = target.localPosition;
            position.x = value;
            target.localPosition = position;
        }
    }
}
