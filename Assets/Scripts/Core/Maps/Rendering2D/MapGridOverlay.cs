// MapGridOverlay.cs
// Draws the military grid over a rendered sheet.
//
// MGRS and UTM share one square grid — they differ only in how a line is labelled,
// so both draw the same geometry and diverge at the label. A latitude/longitude
// graticule is a different thing: its lines are not parallel to the cell grid, so
// they are traced as polylines through the projection rather than drawn straight.
//
// Grid lines are the one overlay that must land on exact ground positions: a unit
// reporting "grid 512 604" is describing this geometry, so the spacing is derived
// from the map's georeferencing, never from a round number of pixels.

using System;
using System.Collections.Generic;
using UnityEngine;
using Strategos.NatoSymbols;

namespace Strategos.Maps
{
    public struct MapGridOptions
    {
        public GridSystem System;

        /// <summary>Line spacing in metres. 0 picks the coarsest legible interval.</summary>
        public float SpacingMetres;

        /// <summary>Heavier line every nth, counted from the zone origin. 0 or 1 disables.</summary>
        public int IndexEvery;

        public bool DrawLabels;

        /// <summary>Bitmap font scale for labels. 0 scales with the render resolution.</summary>
        public int LabelScale;

        /// <summary>
        /// Auto spacing keeps lines at least this far apart. Below roughly this the
        /// grid stops being a reference and becomes a texture over the terrain.
        /// </summary>
        public float MinSpacingPixels;

        public static MapGridOptions Default => new MapGridOptions
        {
            System           = GridSystem.Mgrs,
            SpacingMetres    = 0f,
            IndexEvery       = 5,
            DrawLabels       = true,
            LabelScale       = 0,
            MinSpacingPixels = 56f,
        };
    }

    public static class MapGridOverlay
    {
        /// <summary>
        /// Candidate square-grid intervals in metres. Restricted to values a grid
        /// reference is actually quoted in — 1 km above all — so a reader can convert
        /// between the sheet and a report without arithmetic.
        /// </summary>
        private static readonly float[] SquareIntervals =
        {
            100f, 250f, 500f, 1000f, 2000f, 5000f, 10000f, 25000f, 50000f, 100000f,
        };

        /// <summary>
        /// Inset of an edge label from the sheet edge, in pixels. Must clear the label
        /// placer's own padding and edge margin, or every edge label is rejected as
        /// overhanging the sheet and the grid draws with no designators at all.
        /// </summary>
        private const int LabelInset = 6;

        /// <summary>Candidate graticule intervals in degrees, coarse to fine.</summary>
        private static readonly float[] DegreeIntervals =
        {
            5f, 2f, 1f, 0.5f, 0.25f, 1f / 6f, 1f / 12f, 1f / 60f, 1f / 120f, 1f / 600f,
        };

        public static void Draw(Color32[] px, MapData map, MapViewport view,
            MapPalette palette, MapGridOptions options, MapLabelPlacer labels = null)
        {
            if (px == null || map == null || palette == null) return;
            if (options.System == GridSystem.None) return;

            if (options.System == GridSystem.LatLon)
                DrawGraticule(px, map, view, palette, options, labels);
            else
                DrawSquareGrid(px, map, view, palette, options, labels);
        }

        // ─── UTM / MGRS square grid ───────────────────────────────────────────

        private static void DrawSquareGrid(Color32[] px, MapData map, MapViewport view,
            MapPalette palette, MapGridOptions options, MapLabelPlacer labels)
        {
            var header = map.Header;
            float mpc  = Mathf.Max(0.001f, header.MetresPerCell);

            float spacing = options.SpacingMetres > 0f
                ? options.SpacingMetres
                : AutoSquareSpacing(mpc, view.PixelsPerCell, options.MinSpacingPixels,
                    view.CellWindow.width * mpc);

            int thin  = 1;
            int heavy = view.PixelsPerCell >= 2f ? 2 : 1;
            int scale = options.LabelScale > 0
                ? options.LabelScale
                : (view.PixelsPerCell >= 3f ? 2 : 1);

            var origin = header.Origin;

            // Eastings: vertical lines.
            double eastMin = origin.Easting + view.CellWindow.xMin * mpc;
            double eastMax = origin.Easting + view.CellWindow.xMax * mpc;

            for (long i = (long)Math.Ceiling(eastMin / spacing);
                      i <= (long)Math.Floor(eastMax / spacing); i++)
            {
                double easting = i * spacing;
                float  cellX   = (float)((easting - origin.Easting) / mpc);
                int    x       = Mathf.RoundToInt(view.CellToPixel(cellX, 0f).x);
                bool   isIndex = IsIndex(i, options.IndexEvery);

                ProceduralDrawUtil.DrawLine(px, view.Width, view.Height,
                    x, 0, x, view.Height - 1,
                    isIndex ? palette.GridIndex : palette.Grid,
                    isIndex ? heavy : thin);

                if (!options.DrawLabels) continue;

                DrawLabel(px, view, labels, PrincipalDigits(easting, spacing),
                    x + 2 + heavy, LabelInset, TextAlign.Left, palette, scale);
            }

            // Northings: horizontal lines.
            double northMin = origin.Northing + view.CellWindow.yMin * mpc;
            double northMax = origin.Northing + view.CellWindow.yMax * mpc;

            for (long i = (long)Math.Ceiling(northMin / spacing);
                      i <= (long)Math.Floor(northMax / spacing); i++)
            {
                double northing = i * spacing;
                float  cellY    = (float)((northing - origin.Northing) / mpc);
                int    y        = Mathf.RoundToInt(view.CellToPixel(0f, cellY).y);
                bool   isIndex  = IsIndex(i, options.IndexEvery);

                ProceduralDrawUtil.DrawLine(px, view.Width, view.Height,
                    0, y, view.Width - 1, y,
                    isIndex ? palette.GridIndex : palette.Grid,
                    isIndex ? heavy : thin);

                if (!options.DrawLabels) continue;

                DrawLabel(px, view, labels, PrincipalDigits(northing, spacing),
                    LabelInset, y + 2 + heavy, TextAlign.Left, palette, scale);
            }
        }

        /// <summary>
        /// The two digits a grid reference quotes for this line: 10 km and 1 km for a
        /// kilometre grid, 1 km and 100 m below that. Full coordinates are deliberately
        /// not printed — the whole point of the convention is that two digits suffice
        /// inside a 100 km square.
        /// </summary>
        private static string PrincipalDigits(double metres, float spacing)
        {
            double unit = spacing >= 1000f ? 1000.0 : 100.0;
            long   step = (long)Math.Round(metres / unit);
            long   two  = ((step % 100) + 100) % 100;
            return two.ToString("00");
        }

        /// <summary>
        /// Picks the interval to draw. The kilometre grid wins wherever it is legible,
        /// because that is the interval a grid reference is quoted in: on a 500 m grid
        /// the two principal digits step by 5 and no longer read as the km figure a
        /// report would give. Finer intervals are only for a sheet too small to carry
        /// more than a square or two of a kilometre grid.
        /// </summary>
        private static float AutoSquareSpacing(float metresPerCell, float pixelsPerCell,
            float minSpacingPixels, float sheetMetres)
        {
            float perMetre = pixelsPerCell / metresPerCell;

            const float MinSquaresAcross = 2.5f;
            if (1000f * perMetre >= minSpacingPixels &&
                sheetMetres >= 1000f * MinSquaresAcross)
                return 1000f;

            // Finest interval that still clears the minimum spacing.
            for (int i = 0; i < SquareIntervals.Length; i++)
                if (SquareIntervals[i] * perMetre >= minSpacingPixels)
                    return SquareIntervals[i];

            // Even a 100 km grid would be too dense: the render is so coarse that no
            // interval reads. Draw the coarsest rather than nothing.
            return SquareIntervals[SquareIntervals.Length - 1];
        }

        private static bool IsIndex(long lineIndex, int indexEvery) =>
            indexEvery > 1 && (((lineIndex % indexEvery) + indexEvery) % indexEvery) == 0;

        // ─── Latitude / longitude graticule ───────────────────────────────────

        /// <summary>
        /// Traces whole-degree-fraction parallels and meridians through the map's
        /// projection. They are sampled rather than drawn straight because a parallel
        /// is a curve in UTM; at 25 m per cell the bow across a 12 km sheet is small
        /// but visible, and drawing it straight would put the line metres off.
        /// </summary>
        private static void DrawGraticule(Color32[] px, MapData map, MapViewport view,
            MapPalette palette, MapGridOptions options, MapLabelPlacer labels)
        {
            var header = map.Header;
            Rect win   = view.CellWindow;

            // Extent in geographic coordinates, from the window's four corners.
            double latMin = double.MaxValue, latMax = double.MinValue;
            double lonMin = double.MaxValue, lonMax = double.MinValue;

            for (int c = 0; c < 4; c++)
            {
                float cx = (c == 0 || c == 3) ? win.xMin : win.xMax;
                float cy = (c < 2)            ? win.yMin : win.yMax;

                MapCoordinates.UtmToLatLon(
                    MapCoordinates.CellToUtm(header, cx, cy), out double lat, out double lon);

                if (lat < latMin) latMin = lat;
                if (lat > latMax) latMax = lat;
                if (lon < lonMin) lonMin = lon;
                if (lon > lonMax) lonMax = lon;
            }

            float step = AutoDegreeStep(header, view, options.MinSpacingPixels);
            int   scale = options.LabelScale > 0
                ? options.LabelScale
                : (view.PixelsPerCell >= 3f ? 2 : 1);

            const int Samples = 48;

            // Parallels.
            for (long i = (long)Math.Ceiling(latMin / step); i <= (long)Math.Floor(latMax / step); i++)
            {
                double lat = i * step;
                var line = new List<Vector2>(Samples + 1);
                for (int s = 0; s <= Samples; s++)
                {
                    double lon = lonMin + (lonMax - lonMin) * s / Samples;
                    line.Add(view.CellToPixel(ToCell(header, lat, lon)));
                }

                ProceduralDrawUtil.DrawPolyline(px, view.Width, view.Height,
                    line, palette.Grid, 1);

                if (!options.DrawLabels) continue;
                DrawLabel(px, view, labels, FormatDegrees(lat, step) + "N",
                    LabelInset, Mathf.RoundToInt(line[0].y) + 3, TextAlign.Left, palette, scale);
            }

            // Meridians.
            for (long i = (long)Math.Ceiling(lonMin / step); i <= (long)Math.Floor(lonMax / step); i++)
            {
                double lon = i * step;
                var line = new List<Vector2>(Samples + 1);
                for (int s = 0; s <= Samples; s++)
                {
                    double lat = latMin + (latMax - latMin) * s / Samples;
                    line.Add(view.CellToPixel(ToCell(header, lat, lon)));
                }

                ProceduralDrawUtil.DrawPolyline(px, view.Width, view.Height,
                    line, palette.Grid, 1);

                if (!options.DrawLabels) continue;
                DrawLabel(px, view, labels, FormatDegrees(lon, step) + "E",
                    Mathf.RoundToInt(line[0].x) + 3, LabelInset, TextAlign.Left, palette, scale);
            }
        }

        private static Vector2 ToCell(MapHeader header, double lat, double lon) =>
            MapCoordinates.UtmToCell(header,
                MapCoordinates.LatLonToUtm(lat, lon, header.Origin.Zone));

        private static float AutoDegreeStep(MapHeader header, MapViewport view,
            float minSpacingPixels)
        {
            // One degree of latitude is close enough to constant to choose a step by;
            // longitude spacing only shrinks with cos(lat), which pulls in the same
            // direction and at worst gives a slightly denser graticule than asked.
            const float MetresPerDegree = 111320f;
            float perDegree = MetresPerDegree / Mathf.Max(0.001f, header.MetresPerCell)
                              * view.PixelsPerCell;

            // Finest step that still clears the minimum spacing; the list runs coarse
            // to fine, so this walks backwards.
            for (int i = DegreeIntervals.Length - 1; i >= 0; i--)
                if (DegreeIntervals[i] * perDegree >= minSpacingPixels)
                    return DegreeIntervals[i];

            return DegreeIntervals[0];
        }

        /// <summary>Degrees at just enough precision for the interval in use.</summary>
        private static string FormatDegrees(double degrees, float step)
        {
            if (step >= 1f)    return degrees.ToString("0");
            if (step >= 0.1f)  return degrees.ToString("0.0");
            if (step >= 0.01f) return degrees.ToString("0.00");
            return degrees.ToString("0.000");
        }

        // ─── Labels ───────────────────────────────────────────────────────────

        /// <summary>
        /// Grid labels go through the placer when one is supplied, so they lose to
        /// whatever was placed first rather than overprinting it. Without a placer
        /// they are drawn unconditionally — which is what a bare grid render wants.
        /// </summary>
        private static void DrawLabel(Color32[] px, MapViewport view, MapLabelPlacer labels,
            string text, int x, int y, TextAlign align, MapPalette palette, int scale)
        {
            if (labels != null)
            {
                labels.TryDraw(px, text, x, y, palette.GridLabel, palette.LabelHalo,
                    scale, align);
                return;
            }

            ProceduralDrawUtil.DrawText(px, view.Width, view.Height, x, y, text,
                palette.GridLabel, scale, align);
        }
    }
}
