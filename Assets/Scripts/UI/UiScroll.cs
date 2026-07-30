// UiScroll.cs
// A vertical scrolling column: ScrollRect + masked viewport + auto-sizing content.
//
// Extracted from SymbolBuilderPanel's control rail. Every view's rail is this shape,
// and the layout-group flags below are the ones that go wrong quietly:
//
//   childControlHeight = true      — without it a group reserves space from
//                                    LayoutElement.preferredHeight but never resizes
//                                    the child, so children collapse to zero height
//                                    while still occupying the space.
//   ContentSizeFitter PreferredSize — without it the content rect never grows and the
//                                    ScrollRect has nothing to scroll.

using UnityEngine;
using UnityEngine.UI;

namespace Strategos.UI
{
    public static class UiScroll
    {
        /// <summary>
        /// Builds a scrolling column inside <paramref name="parent"/> and returns the
        /// content rect that children should be parented to.
        /// </summary>
        /// <param name="background">Viewport fill; also what shows through the mask.</param>
        /// <param name="scroll">The ScrollRect, so a caller can inset it — a rail with a
        /// chrome header needs the column to start below that header.</param>
        /// <param name="padding">Content padding. Extra bottom padding keeps the last
        /// control clear of the window edge.</param>
        public static RectTransform CreateColumn(string name, Transform parent,
            Color background, out ScrollRect scroll,
            RectOffset padding = null, float spacing = 8f)
        {
            var scrollGo = UiFactory.CreateRect(name, parent);
            UiFactory.Stretch(scrollGo);
            scroll = scrollGo.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 48f;

            var viewport = UiFactory.CreateRect("Viewport", scrollGo);
            UiFactory.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = background;
            scroll.viewport = viewport;

            var content = UiFactory.CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var contentV = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentV.padding = padding ?? new RectOffset(14, 14, 12, 28);
            contentV.spacing = spacing;
            contentV.childControlWidth = true;
            contentV.childControlHeight = true;
            contentV.childForceExpandHeight = false;
            contentV.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            return content;
        }

        /// <summary>
        /// Same shape, but the content is a grid. Returns the content rect; the caller sets
        /// <c>constraintCount</c> on the returned group when its column count changes.
        /// </summary>
        public static RectTransform CreateGridColumn(string name, Transform parent,
            Color background, Vector2 cellSize, Vector2 spacing,
            out ScrollRect scroll, out GridLayoutGroup grid, RectOffset padding = null)
        {
            var scrollGo = UiFactory.CreateRect(name, parent);
            UiFactory.Stretch(scrollGo);
            scroll = scrollGo.gameObject.AddComponent<ScrollRect>();
            // Both axes: a matrix enumerating two fields can be far wider than the card —
            // 13 unit types against 14 echelons is about 1900 px of tiles — and with only
            // vertical scrolling the right-hand columns are simply unreachable.
            scroll.horizontal = true;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 48f;

            var viewport = UiFactory.CreateRect("Viewport", scrollGo);
            UiFactory.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = background;
            scroll.viewport = viewport;

            var content = UiFactory.CreateRect("Content", viewport);
            // Anchored to the top-left corner only, so the ContentSizeFitter below drives
            // BOTH dimensions. Stretching the width to the viewport would peg the content
            // to the card and leave nothing to scroll horizontally.
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = padding ?? new RectOffset(12, 12, 12, 12);
            grid.cellSize = cellSize;
            grid.spacing = spacing;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            return content;
        }
    }
}
