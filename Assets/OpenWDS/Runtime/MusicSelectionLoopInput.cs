using UnityEngine;
using UnityEngine.EventSystems;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Forwards the retail EnhancedScroller drag boundary to the offline host.
    /// ScrollRect still owns pointer motion and inertia.
    /// </summary>
    public sealed class MusicSelectionLoopInput :
        MonoBehaviour,
        IBeginDragHandler,
        IEndDragHandler
    {
        private System.Action _begin, _end;

        public void Configure(LocalMusicSelectionRuntime runtime)
        {
            Configure(runtime.OnListBeginDrag, runtime.OnListEndDrag);
        }

        public void Configure(System.Action begin, System.Action end)
        {
            _begin = begin; _end = end;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _begin?.Invoke();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _end?.Invoke();
        }
    }
}
