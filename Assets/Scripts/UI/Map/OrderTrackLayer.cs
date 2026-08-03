// OrderTrackLayer.cs
// Draws routes and arrowheads over a map sheet.
//
// Immediate-mode over a pool: a caller clears, adds the legs it wants this frame, and
// finishes; anything left over from last frame is hidden rather than destroyed. Plans change
// every tick while a unit is moving, so allocating per frame would churn hundreds of objects
// a second for a handful of arrows.
//
// UI elements rather than drawing into the sheet's pixels. ProceduralDrawUtil has polyline,
// dashed-polyline and arrowhead primitives, and they are the right tool for compositing into
// a map raster — but a route follows a unit that moves every tick, and rebuilding a
// card-sized texture at that rate is far more expensive than moving a few rects.
//
// A dashed leg is one quad with a repeating texture scaled through uvRect, not a chain of
// short segments, so a long planned route costs the same as a short one.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Strategos.UI
{
    public sealed class OrderTrackLayer
    {
        private const float LegThickness = 3f;
        private const float ArrowSize = 16f;
        private const float HandleSize = 10f;

        /// <summary>Legs shorter than this are not worth drawing and look like specks.</summary>
        private const float MinLegPixels = 6f;

        private readonly RectTransform _parent;
        private readonly List<RawImage> _legs = new();
        private readonly List<Image> _heads = new();
        private readonly List<Image> _handles = new();

        private int _legCount;
        private int _headCount;
        private int _handleCount;

        public OrderTrackLayer(RectTransform parent) => _parent = parent;

        /// <summary>Starts a frame. Everything added after this is kept; the rest is hidden.</summary>
        public void Begin()
        {
            _legCount = 0;
            _headCount = 0;
            _handleCount = 0;
        }

        /// <summary>
        /// Draws one leg of a route between two cell coordinates.
        /// </summary>
        /// <param name="dashed">
        /// True for a leg that is planned but not yet under way. Matches APP-6D's dashed =
        /// anticipated, which the frames already use.
        /// </param>
        public void AddLeg(MapSheetCard card, Vector2 fromCell, Vector2 toCell,
            Color colour, bool dashed)
        {
            if (card == null) return;

            // Both ends have to be locatable; CellToLocal reports false outside the visible
            // crop, and a leg with one end off-sheet would be drawn to the wrong place.
            bool aVisible = card.CellToLocal(fromCell, out var a);
            bool bVisible = card.CellToLocal(toCell, out var b);
            if (!aVisible && !bVisible) return;

            Vector2 delta = b - a;
            float length = delta.magnitude;
            if (length < MinLegPixels) return;

            var rt = LegAt(_legCount++);
            var img = rt.GetComponent<RawImage>();

            rt.gameObject.SetActive(true);
            rt.sizeDelta = new Vector2(length, LegThickness);
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            img.color = colour;

            if (dashed)
            {
                img.texture = UiSprites.DashTexture;
                // Repeat the dash along the leg's length rather than stretching one across it,
                // so dash size is constant and a long route does not turn into a long smear.
                img.uvRect = new Rect(0f, 0f, length / UiSprites.DashPeriod, 1f);
            }
            else
            {
                img.texture = Texture2D.whiteTexture;
                img.uvRect = new Rect(0f, 0f, 1f, 1f);
            }
        }

        /// <summary>Draws an arrowhead at <paramref name="atCell"/>, pointing away from <paramref name="fromCell"/>.</summary>
        public void AddArrowhead(MapSheetCard card, Vector2 fromCell, Vector2 atCell, Color colour)
        {
            if (card == null) return;
            if (!card.CellToLocal(atCell, out var tip)) return;
            if (!card.CellToLocal(fromCell, out var tail)) return;

            Vector2 dir = tip - tail;
            if (dir.sqrMagnitude < 0.0001f) return;

            var rt = HeadAt(_headCount++);
            rt.gameObject.SetActive(true);
            rt.sizeDelta = new Vector2(ArrowSize, ArrowSize);
            rt.anchoredPosition = tip;

            // UiSprites.Arrow points down (its apex is at y = 0), so a heading of "up" is
            // already rotated 180 degrees from the sprite's own direction.
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            rt.localRotation = Quaternion.Euler(0f, 0f, angle + 90f);

            rt.GetComponent<Image>().color = colour;
        }

        /// <summary>
        /// Draft waypoint handle (#54). Visual only — hit-testing stays in PLAY so route
        /// legs can keep <c>raycastTarget = false</c>.
        /// </summary>
        public void AddHandle(MapSheetCard card, Vector2 cell, Color colour)
        {
            if (card == null) return;
            if (!card.CellToLocal(cell, out var local)) return;

            var rt = HandleAt(_handleCount++);
            rt.gameObject.SetActive(true);
            rt.sizeDelta = new Vector2(HandleSize, HandleSize);
            rt.anchoredPosition = local;
            rt.localRotation = Quaternion.identity;
            rt.GetComponent<Image>().color = colour;
        }

        /// <summary>Hides anything not used this frame.</summary>
        public void End()
        {
            for (int i = _legCount; i < _legs.Count; i++) _legs[i].gameObject.SetActive(false);
            for (int i = _headCount; i < _heads.Count; i++) _heads[i].gameObject.SetActive(false);
            for (int i = _handleCount; i < _handles.Count; i++)
                _handles[i].gameObject.SetActive(false);
        }

        public void Clear()
        {
            Begin();
            End();
        }

        // ─── Pool ─────────────────────────────────────────────────────────────

        private RectTransform LegAt(int i)
        {
            while (_legs.Count <= i)
            {
                var rt = UiFactory.CreateRect($"Leg{_legs.Count}", _parent);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                var img = rt.gameObject.AddComponent<RawImage>();
                img.raycastTarget = false;
                _legs.Add(img);
            }
            return (RectTransform)_legs[i].transform;
        }

        private RectTransform HeadAt(int i)
        {
            while (_heads.Count <= i)
            {
                var rt = UiFactory.CreateRect($"Head{_heads.Count}", _parent);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = UiSprites.Arrow;
                img.raycastTarget = false;
                _heads.Add(img);
            }
            return (RectTransform)_heads[i].transform;
        }

        private RectTransform HandleAt(int i)
        {
            while (_handles.Count <= i)
            {
                var rt = UiFactory.CreateRect($"Handle{_handles.Count}", _parent);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                var img = rt.gameObject.AddComponent<Image>();
                img.raycastTarget = false;
                _handles.Add(img);
            }
            return (RectTransform)_handles[i].transform;
        }
    }
}
