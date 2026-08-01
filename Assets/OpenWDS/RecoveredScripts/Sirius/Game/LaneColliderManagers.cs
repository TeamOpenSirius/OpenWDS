using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sirius.Game
{
    // Serialization-compatible recovery of Sirius.Game.LaneColliderManagers.
    public class LaneColliderManagers : MonoBehaviour
    {
        [Serializable]
        private sealed class LaneColliders
        {
            [SerializeField] private Collider _mainCollider;
            [SerializeField] private Collider _leftInnerCollider;
            [SerializeField] private Collider _rightInnerCollider;
            [SerializeField] private Collider _leftOuterCollider;
            [SerializeField] private Collider _rightOuterCollider;

            public Collider MainCollider => _mainCollider;
            public Collider SubLeftInnerCollider => _leftInnerCollider;
            public Collider SubRightInnerCollider => _rightInnerCollider;
            public Collider SubLeftOuterCollider => _leftOuterCollider;
            public Collider SubRightOuterCollider => _rightOuterCollider;
        }

        [SerializeField] private List<LaneColliders> _laneColliders = new List<LaneColliders>();

        public IReadOnlyDictionary<int, int> MainColliders { get; private set; }
        public IReadOnlyDictionary<int, int> SubLeftInnerColliders { get; private set; }
        public IReadOnlyDictionary<int, int> SubRightInnerColliders { get; private set; }
        public IReadOnlyDictionary<int, int> SubLeftOuterColliders { get; private set; }
        public IReadOnlyDictionary<int, int> SubRightOuterColliders { get; private set; }

        private void Start()
        {
            InitializeMappings();
        }

        public void InitializeMappings()
        {
            MainColliders = GetColliders(item => item.MainCollider);
            SubLeftInnerColliders = GetColliders(item => item.SubLeftInnerCollider);
            SubRightInnerColliders = GetColliders(item => item.SubRightInnerCollider);
            SubLeftOuterColliders = GetColliders(item => item.SubLeftOuterCollider);
            SubRightOuterColliders = GetColliders(item => item.SubRightOuterCollider);
        }

        private IReadOnlyDictionary<int, int> GetColliders(Func<LaneColliders, Collider> select)
        {
            var result = new Dictionary<int, int>(_laneColliders.Count);
            for (var index = 0; index < _laneColliders.Count; index++)
            {
                var collider = select(_laneColliders[index]);
                if (collider != null)
                {
                    // Original LINQ projection maps list index + 1 to lane ID.
                    result.Add(collider.GetInstanceID(), index + 1);
                }
            }
            return result;
        }
    }
}
