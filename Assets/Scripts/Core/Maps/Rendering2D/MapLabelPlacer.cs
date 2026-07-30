// MapLabelPlacer.cs
// Collision-avoiding text placement for the 2D map renderer.
//
// A sheet with 200 settlements, spot heights and grid designators on it will pile
// labels on top of each other unless something arbitrates. Cartographers solve
// this by moving a label to whichever side of its feature is clear and dropping it
// when no side is; that is what this does. Placement order is therefore priority
// order — draw cities before villages, or the villages win the good positions.

using System.Collections.Generic;
using UnityEngine;
using Strategos.NatoSymbols;

namespace Strategos.Maps
{
    /// <summary>Where a label sits relative to its feature, in preference order.</summary>
    public enum LabelAnchor
    {
        Right  = 0,
        Left   = 1,
        Above  = 2,
        Below  = 3,
        Centre = 4,
    }

    public sealed class MapLabelPlacer
    {
        private readonly int _width;
        private readonly int _height;
        private readonly List<Rect> _taken = new();

        /// <summary>Clear space kept around every label, in pixels.</summary>
        public int Padding = 2;

        /// <summary>
        /// Labels within this margin of the sheet edge are rejected. A designation
        /// clipped in half is worse than no designation.
        /// </summary>
        public int EdgeMargin = 2;

        public MapLabelPlacer(int width, int height)
        {
            _width  = width;
            _height = height;
        }

        public int PlacedCount => _taken.Count;

        /// <summary>
        /// Marks a rectangle as occupied without drawing anything. Used to protect
        /// the marks themselves — a POI dot, a unit symbol — from being labelled over.
        /// </summary>
        public void Reserve(Rect rect) => _taken.Add(rect);

        public void Reserve(int centreX, int centreY, int halfSize) =>
            _taken.Add(new Rect(centreX - halfSize, centreY - halfSize,
                                halfSize * 2 + 1, halfSize * 2 + 1));

        public bool IsFree(Rect rect)
        {
            if (rect.xMin < EdgeMargin || rect.yMin < EdgeMargin) return false;
            if (rect.xMax > _width - EdgeMargin || rect.yMax > _height - EdgeMargin) return false;

            for (int i = 0; i < _taken.Count; i++)
                if (_taken[i].Overlaps(rect)) return false;

            return true;
        }

        public void Clear() => _taken.Clear();

        // ─── Drawing ──────────────────────────────────────────────────────────

        /// <summary>
        /// Draws <paramref name="text"/> at an exact position if the space is clear.
        /// Returns false and draws nothing otherwise.
        /// </summary>
        public bool TryDraw(Color32[] px, string text, int x, int y,
            Color32 ink, Color32 halo, int scale,
            TextAlign align = TextAlign.Left, bool drawHalo = true)
        {
            if (string.IsNullOrEmpty(text)) return false;

            scale = Mathf.Max(1, scale);
            Rect rect = Measure(text, x, y, scale, align);
            if (!IsFree(rect)) return false;

            Draw(px, text, x, y, ink, halo, scale, align, drawHalo);
            _taken.Add(rect);
            return true;
        }

        /// <summary>
        /// Tries each anchor in turn around a feature at (x, y), taking the first
        /// that fits. <paramref name="offset"/> is the clearance from the feature
        /// centre, normally its mark radius plus a pixel or two.
        /// </summary>
        public bool TryDrawNear(Color32[] px, string text, int x, int y, int offset,
            Color32 ink, Color32 halo, int scale,
            IReadOnlyList<LabelAnchor> anchors = null, bool drawHalo = true)
        {
            if (string.IsNullOrEmpty(text)) return false;

            scale = Mathf.Max(1, scale);
            anchors ??= DefaultAnchors;

            int glyphHeight = ProceduralDrawUtil.GlyphH * scale;

            for (int i = 0; i < anchors.Count; i++)
            {
                int   lx = x, ly = y;
                TextAlign align = TextAlign.Left;

                switch (anchors[i])
                {
                    case LabelAnchor.Right:
                        lx = x + offset;
                        ly = y - glyphHeight / 2;
                        align = TextAlign.Left;
                        break;
                    case LabelAnchor.Left:
                        lx = x - offset;
                        ly = y - glyphHeight / 2;
                        align = TextAlign.Right;
                        break;
                    case LabelAnchor.Above:
                        lx = x;
                        ly = y + offset;
                        align = TextAlign.Center;
                        break;
                    case LabelAnchor.Below:
                        lx = x;
                        ly = y - offset - glyphHeight;
                        align = TextAlign.Center;
                        break;
                    case LabelAnchor.Centre:
                        lx = x;
                        ly = y - glyphHeight / 2;
                        align = TextAlign.Center;
                        break;
                }

                if (TryDraw(px, text, lx, ly, ink, halo, scale, align, drawHalo))
                    return true;
            }

            return false;
        }

        private static readonly LabelAnchor[] DefaultAnchors =
        {
            LabelAnchor.Right, LabelAnchor.Left, LabelAnchor.Above, LabelAnchor.Below,
        };

        /// <summary>
        /// Bounding box a label would occupy, padding included. (x, y) is the glyph
        /// baseline's bottom-left as <see cref="ProceduralDrawUtil.DrawText"/> means it.
        /// </summary>
        public Rect Measure(string text, int x, int y, int scale, TextAlign align)
        {
            int w = ProceduralDrawUtil.MeasureText(text, scale);
            int h = ProceduralDrawUtil.GlyphH * scale;

            int left = align switch
            {
                TextAlign.Center => x - w / 2,
                TextAlign.Right  => x - w,
                _                => x,
            };

            return new Rect(left - Padding, y - Padding, w + Padding * 2, h + Padding * 2);
        }

        /// <summary>
        /// Unconditional draw. The halo is eight offset copies of the glyph mask
        /// rather than a filled box: a box would erase the contours and roads the
        /// label sits on, where a halo only interrupts them.
        /// </summary>
        private void Draw(Color32[] px, string text, int x, int y,
            Color32 ink, Color32 halo, int scale, TextAlign align, bool drawHalo)
        {
            if (drawHalo && halo.a > 0)
            {
                for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0) continue;
                    ProceduralDrawUtil.DrawText(px, _width, _height,
                        x + ox, y + oy, text, halo, scale, align);
                }
            }

            ProceduralDrawUtil.DrawText(px, _width, _height, x, y, text, ink, scale, align);
        }
    }
}
