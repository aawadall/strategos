// MapRasterizer.cs
// Renders MapData to a Texture2D as a topographic sheet.
//
// Everything is drawn on the CPU into a Color32 buffer, for the same reason the
// symbol baker is: a headless batch bake has no camera, no canvas and no render
// texture, and a map you cannot bake in CI is a map nobody can regression-test.
// The cost is per-render, not per-frame — the output is a texture the scene shows.
//
// Draw order is fixed and is not arbitrary; it is the order a survey sheet is
// printed in, and it is what keeps the sheet readable:
//
//     base tint + landcover + hillshade   the ground
//     contours                            relief, under everything man-made
//     areas                               woods, built-up, lakes
//     lines                               drainage, then rail, then roads
//     point marks                         settlements and spot heights
//     labels                              in priority order, collision-avoided
//     grid                                overprints all of it, as on a real sheet
//
// Labels come after every mark so that placement can see what is already occupied,
// and the grid comes last because a grid line that gives way to a road has stopped
// being a coordinate reference.

using System;
using System.Collections.Generic;
using UnityEngine;
using Strategos.NatoSymbols;

namespace Strategos.Maps
{
    public struct MapRenderOptions
    {
        public MapRenderMode Mode;

        /// <summary>Output pixels per map cell. 1 renders a 512-cell map at 512 px.</summary>
        public float PixelsPerCell;

        /// <summary>Cell-space extent to render. Null renders the whole map.</summary>
        public Rect? CellWindow;

        public bool DrawHillshade;
        public bool DrawContours;
        public bool DrawAreas;
        public bool DrawLines;
        public bool DrawPois;
        public bool DrawLabels;
        public bool DrawGrid;

        public MapGridOptions Grid;
        public ContourOptions Contours;

        /// <summary>
        /// Vertical exaggeration applied to the surface before shading. At 25 m per
        /// cell, true-scale relief is almost flat under a light, and hillshade at
        /// 1× reads as a faint smudge; cartographic relief shading is always
        /// exaggerated. Affects shading only, never the elevation data.
        /// </summary>
        public float HillshadeExaggeration;

        /// <summary>Illumination bearing in degrees clockwise from north.</summary>
        public float SunAzimuthDegrees;

        /// <summary>Illumination altitude in degrees above the horizon.</summary>
        public float SunAltitudeDegrees;

        /// <summary>Overrides the palette the mode would select.</summary>
        public MapPalette PaletteOverride;

        public static MapRenderOptions Default => new MapRenderOptions
        {
            Mode          = MapRenderMode.Topographic,
            PixelsPerCell = 1f,
            CellWindow    = null,

            DrawHillshade = true,
            DrawContours  = true,
            DrawAreas     = true,
            DrawLines     = true,
            DrawPois      = true,
            DrawLabels    = true,
            DrawGrid      = true,

            Grid     = MapGridOptions.Default,
            Contours = ContourOptions.Default,

            HillshadeExaggeration = 2.5f,

            // North-west light at 45°: the cartographic convention. Lit from any
            // other quarter and hills read as hollows to most viewers.
            SunAzimuthDegrees  = 315f,
            SunAltitudeDegrees = 45f,
        };

        /// <summary>Base tint and relief only — no culture, no grid, no text.</summary>
        public static MapRenderOptions TerrainOnly
        {
            get
            {
                var o = Default;
                o.DrawAreas  = false;
                o.DrawLines  = false;
                o.DrawPois   = false;
                o.DrawLabels = false;
                o.DrawGrid   = false;
                return o;
            }
        }
    }

    public static class MapRasterizer
    {
        public static Texture2D Render(MapData map, MapRenderOptions options)
        {
            var buffer = RenderPixels(map, options, out MapViewport view);

            var tex = new Texture2D(view.Width, view.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(buffer);
            tex.Apply(false);
            return tex;
        }

        public static Texture2D Render(MapData map) =>
            Render(map, MapRenderOptions.Default);

        /// <summary>
        /// Renders into a raw buffer and reports the viewport used. Callers that
        /// composite their own layers — unit symbols, control measures, fog — want
        /// this rather than <see cref="Render"/>, so they draw in the same pixel
        /// space instead of guessing a transform back out of a texture.
        /// </summary>
        public static Color32[] RenderPixels(MapData map, MapRenderOptions options,
            out MapViewport view)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (map.Elevation == null || map.Width < 2 || map.Height < 2)
                throw new ArgumentException("Map has no usable elevation grid.", nameof(map));

            var palette = options.PaletteOverride ?? MapPalette.For(options.Mode);

            float ppc = options.PixelsPerCell <= 0f ? 1f : options.PixelsPerCell;
            view = options.CellWindow.HasValue
                ? MapViewport.ForWindow(options.CellWindow.Value, ppc)
                : MapViewport.ForWholeMap(map, ppc);

            var px     = new Color32[view.PixelCount];
            var labels = new MapLabelPlacer(view.Width, view.Height);

            DrawGround(px, map, view, palette, options);

            var contourLabels = new List<ContourLabel>();
            if (options.DrawContours)
                DrawContours(px, map, view, palette, options, contourLabels);

            if (options.DrawAreas) DrawAreas(px, map, view, palette);
            if (options.DrawLines) DrawLines(px, map, view, palette);

            if (options.DrawPois) DrawPoiMarks(px, map, view, palette, labels);

            if (options.DrawLabels)
            {
                if (options.DrawPois) DrawPoiLabels(px, map, view, palette, labels);
                DrawContourLabels(px, view, palette, contourLabels, labels);
            }

            if (options.DrawGrid)
            {
                var grid = options.Grid;
                grid.DrawLabels &= options.DrawLabels;
                MapGridOverlay.Draw(px, map, view, palette, grid, labels);
            }

            return px;
        }

        // ─── Ground ───────────────────────────────────────────────────────────

        /// <summary>
        /// Per-pixel base: hypsometric tint, landcover wash and texture, hillshade,
        /// and the shoreline. One pass rather than several because every one of these
        /// reads the same sampled cell, and sampling is what the pass costs.
        /// </summary>
        private static void DrawGround(Color32[] px, MapData map, MapViewport view,
            MapPalette palette, MapRenderOptions options)
        {
            var  header = map.Header;
            bool shade  = options.DrawHillshade && palette.HillshadeStrength > 0.001f;

            Vector3 light = LightDirection(options.SunAzimuthDegrees, options.SunAltitudeDegrees);

            // Illumination of flat ground. Shading is expressed relative to this, so
            // a plain renders at its palette colour and only slopes depart from it.
            float flat = Mathf.Max(0.05f, light.y);

            float exaggeration = Mathf.Max(0.01f, options.HillshadeExaggeration);
            float inverseRun   = exaggeration / (2f * Mathf.Max(0.001f, header.MetresPerCell));

            int maxX = map.Width - 1, maxY = map.Height - 1;

            for (int y = 0; y < view.Height; y++)
            {
                int row = y * view.Width;

                for (int x = 0; x < view.Width; x++)
                {
                    Vector2 cell = view.PixelToCell(x, y);

                    int cx = Mathf.Clamp(Mathf.RoundToInt(cell.x), 0, maxX);
                    int cy = Mathf.Clamp(Mathf.RoundToInt(cell.y), 0, maxY);

                    var  cover = (LandcoverClass)map.Landcover[cy * map.Width + cx];
                    bool water = LandcoverInfo.IsWater(cover);

                    Color32 c;
                    if (water)
                    {
                        // A shore cell is coloured through rather than outlined, so the
                        // edge thickens with the render scale instead of staying a hairline.
                        c = IsShoreCell(map, cx, cy) ? palette.WaterEdge : palette.Water;
                    }
                    else
                    {
                        c = palette.UseHypsometricTint
                            ? palette.Hypsometric(
                                map.NormalisedElevation(map.SampleElevation(cell.x, cell.y)))
                            : palette.Paper;

                        if (palette.UseLandcover && palette.LandcoverBlend > 0.001f)
                            c = Color32.Lerp(c, palette.LandcoverColour(cover),
                                Mathf.Clamp01(palette.LandcoverBlend));

                        if (shade)
                        {
                            float dzdx = (map.SampleElevation(cell.x + 1f, cell.y) -
                                          map.SampleElevation(cell.x - 1f, cell.y)) * inverseRun;
                            float dzdy = (map.SampleElevation(cell.x, cell.y + 1f) -
                                          map.SampleElevation(cell.x, cell.y - 1f)) * inverseRun;

                            var normal = new Vector3(-dzdx, 1f, -dzdy).normalized;
                            float illumination = Mathf.Max(0f, Vector3.Dot(normal, light));

                            c = Multiply(c, ReliefFactor(
                                illumination, flat, palette.HillshadeStrength));
                        }

                        if (palette.UseLandcover)
                            c = ApplyTexture(c, cover, x, y, palette);
                    }

                    px[row + x] = c;
                }
            }
        }

        /// <summary>
        /// Brightness multiplier for a slope. Held to ±strength so that shading never
        /// darkens the ground past the point where the landcover colour underneath it
        /// stops being identifiable — relief shading is a cue, not the content.
        /// </summary>
        private static float ReliefFactor(float illumination, float flat, float strength)
        {
            float relative = (illumination - flat) / flat;
            return Mathf.Clamp(1f + strength * relative, 1f - strength, 1f + strength * 0.6f);
        }

        private static Vector3 LightDirection(float azimuthDegrees, float altitudeDegrees)
        {
            float az  = azimuthDegrees  * Mathf.Deg2Rad;
            float alt = altitudeDegrees * Mathf.Deg2Rad;
            float horizontal = Mathf.Cos(alt);

            // World axes as MapData defines them: x east, y up, z north. Azimuth is a
            // bearing, so it runs clockwise from north rather than counter-clockwise
            // from east.
            return new Vector3(
                Mathf.Sin(az) * horizontal,
                Mathf.Sin(alt),
                Mathf.Cos(az) * horizontal).normalized;
        }

        /// <summary>
        /// Landcover texture. Spacing is in pixels rather than cells: a stipple is a
        /// property of the printed surface, so it should look the same at every zoom,
        /// where a cell-locked pattern would coarsen as you zoom in.
        /// </summary>
        private static Color32 ApplyTexture(Color32 c, LandcoverClass cover,
            int x, int y, MapPalette palette)
        {
            switch (cover)
            {
                case LandcoverClass.Forest:
                    return ProceduralDrawUtil.PatternHit(x, y, FillPattern.Stipple, 7)
                        ? palette.ForestStipple : c;

                case LandcoverClass.Urban:
                    return ProceduralDrawUtil.PatternHit(x, y, FillPattern.DiagonalHatch, 6)
                        ? palette.UrbanHatch : c;

                case LandcoverClass.Marsh:
                    return MarshTick(x, y, 6) ? palette.MarshTick : c;

                case LandcoverClass.Rock:
                    return ProceduralDrawUtil.PatternHit(x, y, FillPattern.Stipple, 23)
                        ? palette.InkMuted : c;

                default:
                    return c;
            }
        }

        /// <summary>
        /// Marsh's staggered horizontal dashes. Not a <see cref="FillPattern"/> because
        /// nothing else wants it and the offset per row is what makes it read as marsh
        /// rather than as ruled lines.
        /// </summary>
        private static bool MarshTick(int x, int y, int spacing)
        {
            spacing = Mathf.Max(2, spacing);
            if (Mod(y, spacing) != 0) return false;

            int row = y / spacing;
            return Mod(x + row * (spacing / 2), spacing) < spacing / 2;
        }

        private static bool IsShoreCell(MapData map, int x, int y)
        {
            return !IsWaterAt(map, x - 1, y) || !IsWaterAt(map, x + 1, y) ||
                   !IsWaterAt(map, x, y - 1) || !IsWaterAt(map, x, y + 1);
        }

        private static bool IsWaterAt(MapData map, int x, int y)
        {
            // Off-map counts as water so the sheet edge is not drawn as a shoreline.
            if (!map.InBounds(x, y)) return true;
            return map.Landcover[y * map.Width + x] == (byte)LandcoverClass.Water;
        }

        // ─── Contours ─────────────────────────────────────────────────────────

        private readonly struct ContourLabel
        {
            public readonly Vector2 Position;
            public readonly string  Text;

            public ContourLabel(Vector2 position, string text)
            {
                Position = position;
                Text     = text;
            }
        }

        private static void DrawContours(Color32[] px, MapData map, MapViewport view,
            MapPalette palette, MapRenderOptions options, List<ContourLabel> labelCandidates)
        {
            var levels = ContourTracer.Trace(map, options.Contours);
            if (levels.Count == 0) return;

            float scale = view.StrokeScale;
            int   thin  = Mathf.Max(1, Mathf.RoundToInt(scale * 0.6f));

            // Below the authored zoom an index contour is distinguished by colour
            // alone. Thickness has no 2 px setting — DrawLine steps 1, 3, 5 — so
            // asking for weight here would triple the line, not double it.
            int heavy = scale >= 1f ? Mathf.Max(thin + 1, Mathf.RoundToInt(scale * 1.5f)) : thin;

            // Labels need a line long enough to interrupt without losing its shape.
            float minLabelLength = 90f * Mathf.Max(0.5f, scale);

            var pixels = new List<Vector2>(256);

            foreach (var level in levels)
            {
                if (palette.IndexContoursOnly && !level.IsIndex) continue;

                Color32 colour = level.IsSubmarine ? palette.ContourWater
                               : level.IsIndex     ? palette.ContourIndex
                                                   : palette.Contour;
                int thickness = level.IsIndex ? heavy : thin;

                string text = level.IsIndex
                    ? Mathf.RoundToInt(level.Metres).ToString()
                    : null;

                foreach (var line in level.Lines)
                {
                    pixels.Clear();
                    for (int i = 0; i < line.Count; i++)
                        pixels.Add(view.CellToPixel(line[i]));

                    ProceduralDrawUtil.DrawPolyline(px, view.Width, view.Height,
                        pixels, colour, thickness);

                    if (text == null || pixels.Count < 8) continue;
                    if (MapGeometry.Length(pixels) < minLabelLength) continue;

                    // A third of the way along rather than the midpoint: adjacent
                    // contours have similar midpoints, so labelling them all there
                    // stacks the whole set into one column and the placer drops
                    // everything but the first.
                    labelCandidates.Add(new ContourLabel(pixels[pixels.Count / 3], text));
                }
            }
        }

        private static void DrawContourLabels(Color32[] px, MapViewport view,
            MapPalette palette, List<ContourLabel> candidates, MapLabelPlacer labels)
        {
            int scale = view.PixelsPerCell >= 3f ? 2 : 1;

            foreach (var candidate in candidates)
            {
                labels.TryDraw(px, candidate.Text,
                    Mathf.RoundToInt(candidate.Position.x),
                    Mathf.RoundToInt(candidate.Position.y),
                    palette.ContourIndex, palette.LabelHalo, scale, TextAlign.Center);
            }
        }

        // ─── Areas ────────────────────────────────────────────────────────────

        private static void DrawAreas(Color32[] px, MapData map, MapViewport view,
            MapPalette palette)
        {
            if (map.Areas == null || map.Areas.Count == 0) return;

            int outline = Mathf.Max(1, Mathf.RoundToInt(view.StrokeScale * 0.5f));
            var ring    = new List<Vector2>(64);

            foreach (var area in map.Areas)
            {
                if (area?.Points == null || area.Points.Count < 3) continue;

                ring.Clear();
                for (int i = 0; i < area.Points.Count; i++)
                    ring.Add(view.CellToPixel(area.Points[i]));

                ResolveAreaStyle(area.Kind, palette,
                    out Color32 fill, out Color32 texture, out Color32 edge,
                    out FillPattern pattern, out int spacing);

                ProceduralDrawUtil.FillPolygon(px, view.Width, view.Height, ring, fill);

                if (pattern != FillPattern.Solid)
                    ProceduralDrawUtil.FillPolygon(px, view.Width, view.Height,
                        ring, texture, pattern, spacing);

                if (edge.a > 0)
                    ProceduralDrawUtil.DrawPolyline(px, view.Width, view.Height,
                        ring, edge, outline, closed: true);
            }
        }

        private static void ResolveAreaStyle(MapAreaKind kind, MapPalette palette,
            out Color32 fill, out Color32 texture, out Color32 edge,
            out FillPattern pattern, out int spacing)
        {
            switch (kind)
            {
                case MapAreaKind.Woodblock:
                    fill = palette.Forest; texture = palette.ForestStipple;
                    edge = default; pattern = FillPattern.Stipple; spacing = 7;
                    return;

                case MapAreaKind.Lake:
                    fill = palette.Water; texture = default;
                    edge = palette.WaterEdge; pattern = FillPattern.Solid; spacing = 0;
                    return;

                case MapAreaKind.Marsh:
                    fill = palette.Marsh; texture = palette.MarshTick;
                    edge = default; pattern = FillPattern.DiagonalHatch; spacing = 6;
                    return;

                default: // BuiltUp
                    fill = palette.Urban; texture = palette.UrbanHatch;
                    edge = palette.InkMuted; pattern = FillPattern.DiagonalHatch; spacing = 5;
                    return;
            }
        }

        // ─── Lines ────────────────────────────────────────────────────────────

        private static void DrawLines(Color32[] px, MapData map, MapViewport view,
            MapPalette palette)
        {
            if (map.Lines == null || map.Lines.Count == 0) return;

            // Drainage under rail under road: a bridge should read as the road
            // crossing the river, which only works if the road is drawn second.
            var ordered = new List<MapPolyline>(map.Lines);
            ordered.Sort((a, b) => DrawRank(a.Kind).CompareTo(DrawRank(b.Kind)));

            float scale  = view.StrokeScale;
            var   pixels = new List<Vector2>(256);

            foreach (var line in ordered)
            {
                if (line?.Points == null || line.Points.Count < 2) continue;

                // Generator scaffolding: crest lines exist so later stages can reason
                // about ridges, and are not features a sheet shows.
                if (line.Kind == MapLineKind.Ridge) continue;

                LineStyle style = palette.LineStyleFor(line.Kind);

                pixels.Clear();
                for (int i = 0; i < line.Points.Count; i++)
                    pixels.Add(view.CellToPixel(line.Points[i]));

                float width = style.Width;

                // Drainage tapers: a headwater stream and a trunk river are the same
                // feature class at different weights, and drawing both at full width
                // makes every brook look navigable.
                if (IsDrainage(line.Kind))
                    width = Mathf.Lerp(Mathf.Min(2f, style.Width), style.Width,
                        Mathf.Clamp01(line.Weight));

                int casing = Mathf.Max(1, Mathf.RoundToInt(width * scale));

                if (style.Dashed)
                {
                    // Dashes have a legibility floor of their own: scaled down with
                    // the stroke they close up into a solid line, and a track that
                    // reads as a road is a navigation error, not a cosmetic one.
                    ProceduralDrawUtil.DrawDashedPolyline(px, view.Width, view.Height,
                        pixels, style.Fill, casing,
                        dashLength: Mathf.Max(4f, 7f * scale),
                        gapLength:  Mathf.Max(3f, 5f * scale));
                    continue;
                }

                ProceduralDrawUtil.DrawPolyline(px, view.Width, view.Height,
                    pixels, style.Casing, casing);

                // A cased road is an outline with a lighter core; the core has to be
                // at least two pixels narrower or the casing disappears under it.
                int core = casing - Mathf.Max(2, Mathf.RoundToInt(scale));
                if (core >= 1 && !SameColour(style.Fill, style.Casing))
                    ProceduralDrawUtil.DrawPolyline(px, view.Width, view.Height,
                        pixels, style.Fill, core);
            }
        }

        private static bool IsDrainage(MapLineKind kind) =>
            kind == MapLineKind.River || kind == MapLineKind.Stream ||
            kind == MapLineKind.Canal;

        private static int DrawRank(MapLineKind kind)
        {
            switch (kind)
            {
                case MapLineKind.Coast:   return 0;
                case MapLineKind.Stream:  return 1;
                case MapLineKind.River:   return 2;
                case MapLineKind.Canal:   return 3;
                case MapLineKind.Railway: return 4;
                case MapLineKind.Trail:   return 5;
                case MapLineKind.Track:   return 6;
                case MapLineKind.Road:    return 7;
                case MapLineKind.Highway: return 8;
                default:                  return 9;
            }
        }

        // ─── Points of interest ───────────────────────────────────────────────

        private static void DrawPoiMarks(Color32[] px, MapData map, MapViewport view,
            MapPalette palette, MapLabelPlacer labels)
        {
            if (map.Pois == null || map.Pois.Count == 0) return;

            float scale = view.StrokeScale;
            bool  detail = scale >= DetailZoom;

            foreach (var poi in map.Pois)
            {
                if (poi == null) continue;
                if (!detail && IsDetailFeature(poi.Kind)) continue;

                Vector2 p = view.CellToPixel(poi.Position);
                int x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y);
                if (x < 0 || y < 0 || x >= view.Width || y >= view.Height) continue;

                int radius = MarkRadius(poi.Kind, scale);

                switch (poi.Kind)
                {
                    case MapPoiKind.City:
                    case MapPoiKind.Town:
                        // Built-up places are squares, the convention for a place with
                        // an extent, as against a dot for a point.
                        ProceduralDrawUtil.FillRect(px, view.Width, view.Height,
                            x - radius, y - radius, x + radius, y + radius,
                            palette.Urban, palette.Ink,
                            Mathf.Max(1, Mathf.RoundToInt(scale)));
                        break;

                    case MapPoiKind.Village:
                        ProceduralDrawUtil.FillCircle(px, view.Width, view.Height,
                            x, y, radius, palette.Ink);
                        break;

                    case MapPoiKind.SpotHeight:
                    case MapPoiKind.HillTop:
                        // A bare cross: the elevation figure is the feature, the mark
                        // only says precisely where it was measured.
                        ProceduralDrawUtil.DrawLine(px, view.Width, view.Height,
                            x - radius, y, x + radius, y, palette.InkMuted, 1);
                        ProceduralDrawUtil.DrawLine(px, view.Width, view.Height,
                            x, y - radius, x, y + radius, palette.InkMuted, 1);
                        break;

                    case MapPoiKind.Bridge:
                        ProceduralDrawUtil.FillRect(px, view.Width, view.Height,
                            x - radius, y - radius, x + radius, y + radius,
                            palette.Ink, palette.Ink, 0);
                        break;

                    case MapPoiKind.Ford:
                    case MapPoiKind.Crossroads:
                    case MapPoiKind.Pass:
                        ProceduralDrawUtil.DrawCircleOutline(px, view.Width, view.Height,
                            x, y, radius, palette.InkMuted, 1);
                        break;
                }

                labels.Reserve(x, y, radius);
            }
        }

        /// <summary>
        /// Stroke scale below which point detail is generalised away. A mark has a
        /// minimum legible size and so cannot thin out the way a line does: keep
        /// drawing every ford and spot height on an overview and the sheet fills with
        /// 5 px crosses that hide the terrain they annotate.
        /// </summary>
        private const float DetailZoom = 0.6f;

        /// <summary>
        /// Features a reader only needs once they are close enough to use them. A
        /// settlement is never one of these — placing yourself on the map is what an
        /// overview is for.
        /// </summary>
        private static bool IsDetailFeature(MapPoiKind kind)
        {
            switch (kind)
            {
                case MapPoiKind.City:
                case MapPoiKind.Town:
                case MapPoiKind.Village:
                    return false;
                default:
                    return true;
            }
        }

        private static int MarkRadius(MapPoiKind kind, float scale)
        {
            switch (kind)
            {
                case MapPoiKind.City:    return Mathf.Max(3, Mathf.RoundToInt(3.5f * scale));
                case MapPoiKind.Town:    return Mathf.Max(2, Mathf.RoundToInt(2.5f * scale));
                case MapPoiKind.Village: return Mathf.Max(1, Mathf.RoundToInt(1.5f * scale));
                case MapPoiKind.Bridge:  return Mathf.Max(1, Mathf.RoundToInt(1.5f * scale));
                default:                 return Mathf.Max(2, Mathf.RoundToInt(2f * scale));
            }
        }

        /// <summary>
        /// Places names in importance order, because the placer is first-come: run
        /// villages before cities and the cities lose their positions to hamlets.
        /// </summary>
        private static void DrawPoiLabels(Color32[] px, MapData map, MapViewport view,
            MapPalette palette, MapLabelPlacer labels)
        {
            if (map.Pois == null || map.Pois.Count == 0) return;

            var ordered = new List<MapPoi>(map.Pois);
            ordered.Sort((a, b) =>
            {
                int byKind = LabelRank(a.Kind).CompareTo(LabelRank(b.Kind));
                return byKind != 0 ? byKind : b.Population.CompareTo(a.Population);
            });

            float scale     = view.StrokeScale;
            bool  detail    = scale >= DetailZoom;
            int   nameScale = view.PixelsPerCell >= 3f ? 2 : 1;

            foreach (var poi in ordered)
            {
                if (poi == null) continue;
                if (!detail && IsDetailFeature(poi.Kind)) continue;

                Vector2 p = view.CellToPixel(poi.Position);
                int x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y);
                if (x < 0 || y < 0 || x >= view.Width || y >= view.Height) continue;

                int offset = MarkRadius(poi.Kind, scale) + 3;

                switch (poi.Kind)
                {
                    case MapPoiKind.City:
                    case MapPoiKind.Town:
                    case MapPoiKind.Village:
                        if (string.IsNullOrEmpty(poi.Name)) continue;

                        // Villages take the base size; a city or town is set larger so
                        // the hierarchy reads without a legend. The bitmap font has no
                        // lower case, so names are upper-cased here rather than left to
                        // DrawText's fallback, which would measure a width it then
                        // does not draw.
                        labels.TryDrawNear(px, poi.Name.ToUpperInvariant(), x, y, offset,
                            palette.Ink, palette.LabelHalo,
                            poi.Kind == MapPoiKind.Village ? nameScale
                                                           : Mathf.Max(2, nameScale));
                        break;

                    case MapPoiKind.SpotHeight:
                    case MapPoiKind.HillTop:
                        labels.TryDrawNear(px,
                            Mathf.RoundToInt(poi.ElevationMetres).ToString(),
                            x, y, offset, palette.InkMuted, palette.LabelHalo, 1);
                        break;
                }
            }
        }

        private static int LabelRank(MapPoiKind kind)
        {
            switch (kind)
            {
                case MapPoiKind.City:       return 0;
                case MapPoiKind.Town:       return 1;
                case MapPoiKind.Village:    return 2;
                case MapPoiKind.Pass:       return 3;
                case MapPoiKind.HillTop:    return 4;
                case MapPoiKind.SpotHeight: return 5;
                default:                    return 6;
            }
        }

        // ─── Colour helpers ───────────────────────────────────────────────────

        private static Color32 Multiply(Color32 c, float factor)
        {
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * factor), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * factor), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * factor), 0, 255),
                c.a);
        }

        private static bool SameColour(Color32 a, Color32 b) =>
            a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        private static int Mod(int value, int modulus)
        {
            int r = value % modulus;
            return r < 0 ? r + modulus : r;
        }
    }
}
