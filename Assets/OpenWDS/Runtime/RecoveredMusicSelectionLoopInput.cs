using UnityEngine;
using UnityEngine.EventSystems;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Forwards the retail EnhancedScroller drag boundary to the offline host.
    /// ScrollRect still owns pointer motion and inertia.
    /// </summary>
    public sealed class RecoveredMusicSelectionLoopInput :
        MonoBehaviour,
        IBeginDragHandler,
        IEndDragHandler
    {
        private RecoveredLocalMusicSelectionRuntime _runtime;

        public void Configure(RecoveredLocalMusicSelectionRuntime runtime)
        {
            _runtime = runtime;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _runtime?.OnListBeginDrag();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _runtime?.OnListEndDrag();
        }
    }
}
