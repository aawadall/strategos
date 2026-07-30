// PointerRegion.cs
// Turns EventSystem pointer callbacks on a rect into plain Actions.
//
// EventSystem handlers rather than polling UnityEngine.Input, even though DemoCamera polls.
// Two reasons: a card-scoped gesture needs a rect hit test anyway, which is exactly what
// the raycaster already did to deliver the event; and IScrollHandler on the card means
// scroll-to-zoom cannot fight the control rail's ScrollRect for the wheel. eventData.delta
// is in screen pixels, which is the natural unit for both pixels-of-pan and
// degrees-per-pixel of orbit.

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Strategos.UI
{
    public sealed class PointerRegion : MonoBehaviour,
        IDragHandler, IEndDragHandler, IScrollHandler, IPointerMoveHandler, IPointerClickHandler
    {
        public Action<PointerEventData> Dragged;
        public Action<PointerEventData> Scrolled;
        public Action<PointerEventData> Moved;
        public Action<PointerEventData> Released;
        public Action<PointerEventData> Clicked;

        public void OnDrag(PointerEventData e) => Dragged?.Invoke(e);
        public void OnEndDrag(PointerEventData e) => Released?.Invoke(e);
        public void OnScroll(PointerEventData e) => Scrolled?.Invoke(e);
        public void OnPointerMove(PointerEventData e) => Moved?.Invoke(e);
        public void OnPointerClick(PointerEventData e) => Clicked?.Invoke(e);
    }
}
