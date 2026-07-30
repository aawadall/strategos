// ProceduralDrawUtil.cs
// Shared pixel primitives for procedural APP-6D symbol rendering.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Strategos.NatoSymbols
{
    public enum TextAlign
    {
        Left   = 0,
        Center = 1,
        Right  = 2,
    }

    /// <summary>
    /// Fill texture for an area. Cartographic convention carries meaning in the
    /// pattern as much as the colour — marsh is ticks, a minefield is dots — so
    /// these are drawing modes rather than decoration.
    /// </summary>
    public enum FillPattern
    {
        Solid             = 0,
        DiagonalHatch     = 1,
        BackDiagonalHatch = 2,
        CrossHatch        = 3,
        Dots              = 4,
        Stipple           = 5,
    }

    public static class ProceduralDrawUtil
    {
        // Symbols bake into a square buffer, maps into a rectangular one. The
        // rectangular overload is the real implementation; the square one forwards
        // to it with width == height, so both share a single definition of every
        // primitive rather than drifting into two.

        public static void Set(Color32[] px, int sz, int x, int y, Color32 c) =>
            Set(px, sz, sz, x, y, c);

        public static void Set(Color32[] px, int w, int h, int x, int y, Color32 c)
        {
            if ((uint)x < (uint)w && (uint)y < (uint)h)
                px[y * w + x] = c;
        }

        /// <summary>Alpha-blends <paramref name="c"/> over the existing pixel.</summary>
        public static void Blend(Color32[] px, int w, int h, int x, int y, Color32 c)
        {
            if ((uint)x >= (uint)w || (uint)y >= (uint)h) return;
            if (c.a == 0) return;
            if (c.a == 255) { px[y * w + x] = c; return; }

            int i = y * w + x;
            Color32 dst = px[i];
            int a = c.a;
            int inv = 255 - a;
            px[i] = new Color32(
                (byte)((c.r * a + dst.r * inv) / 255),
                (byte)((c.g * a + dst.g * inv) / 255),
                (byte)((c.b * a + dst.b * inv) / 255),
                (byte)Math.Min(255, c.a + dst.a));
        }

        public static void FillRect(Color32[] px, int sz, int l, int b, int r, int t,
            Color32 fill, Color32 bdr, int bw, FrameLineStyle style = FrameLineStyle.Solid) =>
            FillRect(px, sz, sz, l, b, r, t, fill, bdr, bw, style);

        public static void FillRect(Color32[] px, int w, int h, int l, int b, int r, int t,
            Color32 fill, Color32 bdr, int bw, FrameLineStyle style = FrameLineStyle.Solid)
        {
            for (int y = b; y <= t; y++)
            for (int x = l; x <= r; x++)
            {
                bool border = bw > 0 &&
                    (x <= l + bw - 1 || x >= r - bw + 1 ||
                     y <= b + bw - 1 || y >= t - bw + 1);

                Color32 c;
                if (border)
                {
                    if (!BorderVisible(x, y, style))
                        c = fill;
                    else
                        c = bdr;
                }
                else
                    c = fill;

                Set(px, w, h, x, y, c);
            }
        }

        public static void FillDiamond(Color32[] px, int sz, int cx, int cy, int hw, int hh,
            Color32 fill, Color32 bdr, int bw, FrameLineStyle style = FrameLineStyle.Solid)
        {
            for (int y = cy - hh; y <= cy + hh; y++)
            for (int x = cx - hw; x <= cx + hw; x++)
            {
                float nx = (float)Math.Abs(x - cx) / hw;
                float ny = (float)Math.Abs(y - cy) / hh;
                if (nx + ny > 1.01f) continue;

                int ihw = Math.Max(1, hw - bw), ihh = Math.Max(1, hh - bw);
                float nxi = (float)Math.Abs(x - cx) / ihw;
                float nyi = (float)Math.Abs(y - cy) / ihh;
                bool border = nxi + nyi > 1.0f;
                if (border && !BorderVisible(x, y, style))
                    Set(px, sz, x, y, fill);
                else
                    Set(px, sz, x, y, border ? bdr : fill);
            }
        }

        public static void FillEllipse(Color32[] px, int sz, int cx, int cy, int rx, int ry,
            Color32 fill, Color32 bdr, int bw, FrameLineStyle style = FrameLineStyle.Solid)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                float dx = x - cx, dy = y - cy;
                if (dx * dx / ((float)rx * rx) + dy * dy / ((float)ry * ry) > 1.01f) continue;

                int irx = Math.Max(1, rx - bw), iry = Math.Max(1, ry - bw);
                bool border = dx * dx / ((float)irx * irx) + dy * dy / ((float)iry * iry) > 1.0f;
                if (border && !BorderVisible(x, y, style))
                    Set(px, sz, x, y, fill);
                else
                    Set(px, sz, x, y, border ? bdr : fill);
            }
        }

        public static void FillCircle(Color32[] px, int sz, int cx, int cy, int r, Color32 col) =>
            FillCircle(px, sz, sz, cx, cy, r, col);

        public static void FillCircle(Color32[] px, int w, int h, int cx, int cy, int r, Color32 col)
        {
            int r2 = r * r;
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                int dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy <= r2) Set(px, w, h, x, y, col);
            }
        }

        public static void DrawCircleOutline(Color32[] px, int sz, int cx, int cy, int r,
            Color32 col, int thickness) =>
            DrawCircleOutline(px, sz, sz, cx, cy, r, col, thickness);

        public static void DrawCircleOutline(Color32[] px, int w, int h, int cx, int cy, int r,
            Color32 col, int thickness)
        {
            float outerSq = (float)(r + thickness) * (r + thickness);
            float innerSq = (float)Math.Max(0, r - thickness) * Math.Max(0, r - thickness);
            for (int y = cy - r - thickness; y <= cy + r + thickness; y++)
            for (int x = cx - r - thickness; x <= cx + r + thickness; x++)
            {
                float dx = x - cx, dy = y - cy, d2 = dx * dx + dy * dy;
                if (d2 >= innerSq && d2 <= outerSq) Set(px, w, h, x, y, col);
            }
        }

        public static void DrawLine(Color32[] px, int sz, int x0, int y0, int x1, int y1,
            Color32 col, int thickness) =>
            DrawLine(px, sz, sz, x0, y0, x1, y1, col, thickness);

        public static void DrawLine(Color32[] px, int w, int h, int x0, int y0, int x1, int y1,
            Color32 col, int thickness)
        {
            int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx - dy, t = thickness / 2;

            while (true)
            {
                for (int ty = -t; ty <= t; ty++)
                for (int tx = -t; tx <= t; tx++)
                    Set(px, w, h, x0 + tx, y0 + ty, col);

                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 <  dx) { err += dx; y0 += sy; }
            }
        }

        public static void DrawX(Color32[] px, int sz, int cx, int cy, int rad, Color32 col, int th)
        {
            DrawLine(px, sz, cx - rad, cy - rad, cx + rad, cy + rad, col, Math.Max(1, th));
            DrawLine(px, sz, cx - rad, cy + rad, cx + rad, cy - rad, col, Math.Max(1, th));
        }

        public static void DrawXRow(Color32[] px, int sz, int cx, int cy, int xr, Color32 col,
            int count, int spc, int th)
        {
            int half = (count - 1) * spc / 2;
            for (int i = 0; i < count; i++)
                DrawX(px, sz, cx - half + i * spc, cy, xr, col, th);
        }

        public static void DrawEllipseOutline(Color32[] px, int sz, int cx, int cy,
            int rx, int ry, Color32 col, int thickness)
        {
            int irx = Math.Max(1, rx - thickness);
            int iry = Math.Max(1, ry - thickness);
            for (int y = cy - ry; y <= cy + ry; y++)
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                float dx = x - cx, dy = y - cy;
                if (dx * dx / ((float)rx * rx) + dy * dy / ((float)ry * ry) > 1.01f) continue;
                if (dx * dx / ((float)irx * irx) + dy * dy / ((float)iry * iry) > 1.0f)
                    Set(px, sz, x, y, col);
            }
        }

        // ─── Mobility / sector glyphs ─────────────────────────────────────────
        // Shared by SectorModifierDecorator (Sector 1 / Sector 2 modifiers) and
        // IconDecorator (entity-type variants) so both draw the identical mark.
        // cx / cy are already-scaled pixel coordinates; sc sizes the glyph.

        public static void DrawChevron(Color32[] px, int sz, int cx, int cy, float sc,
            Color32 col, bool up)
        {
            int w  = SymbolLayout.Scale(22, sc);
            int h  = SymbolLayout.Scale(12, sc);
            int th = Math.Max(2, SymbolLayout.Scale(3, sc));
            int tipY  = up ? cy + h / 2 : cy - h / 2;
            int baseY = up ? cy - h / 2 : cy + h / 2;
            DrawLine(px, sz, cx - w, baseY, cx, tipY, col, th);
            DrawLine(px, sz, cx + w, baseY, cx, tipY, col, th);
        }

        public static void DrawMountain(Color32[] px, int sz, int cx, int cy, float sc, Color32 col)
        {
            int w  = SymbolLayout.Scale(18, sc);
            int h  = SymbolLayout.Scale(14, sc);
            int th = Math.Max(2, SymbolLayout.Scale(3, sc));
            DrawLine(px, sz, cx - w, cy - h / 2, cx, cy + h / 2, col, th);
            DrawLine(px, sz, cx, cy + h / 2, cx + w, cy - h / 2, col, th);
        }

        public static void DrawWheeled(Color32[] px, int sz, int cx, int cy, float sc, Color32 col)
        {
            int r  = SymbolLayout.Scale(8, sc);
            int sp = SymbolLayout.Scale(20, sc);
            int th = Math.Max(1, SymbolLayout.Scale(2, sc));
            DrawCircleOutline(px, sz, cx - sp / 2, cy, r, col, th);
            DrawCircleOutline(px, sz, cx + sp / 2, cy, r, col, th);
        }

        public static void DrawWaves(Color32[] px, int sz, int cx, int cy, float sc, Color32 col)
        {
            int w  = SymbolLayout.Scale(28, sc);
            int a  = SymbolLayout.Scale(4, sc);
            int th = Math.Max(2, SymbolLayout.Scale(3, sc));
            DrawLine(px, sz, cx - w, cy, cx - w / 3, cy + a, col, th);
            DrawLine(px, sz, cx - w / 3, cy + a, cx + w / 3, cy - a, col, th);
            DrawLine(px, sz, cx + w / 3, cy - a, cx + w, cy, col, th);
        }

        /// <summary>Arch / igloo profile used for the arctic variant.</summary>
        public static void DrawArch(Color32[] px, int sz, int cx, int cy, float sc, Color32 col)
        {
            int w  = SymbolLayout.Scale(18, sc);
            int h  = SymbolLayout.Scale(12, sc);
            int th = Math.Max(2, SymbolLayout.Scale(3, sc));
            DrawLine(px, sz, cx - w, cy - h / 2, cx - w, cy, col, th);
            DrawLine(px, sz, cx - w, cy, cx, cy + h / 2, col, th);
            DrawLine(px, sz, cx, cy + h / 2, cx + w, cy, col, th);
            DrawLine(px, sz, cx + w, cy, cx + w, cy - h / 2, col, th);
        }

        // ─── 5x7 bitmap text ──────────────────────────────────────────────────
        // TMP cannot be rasterised into the bake buffer, so amplifier text uses a
        // built-in glyph set. Covers what unit designations need: digits, capitals,
        // space, hyphen, slash, period, and the Field F markers.

        public const int GlyphW = 5;
        public const int GlyphH = 7;
        private const int GlyphAdvance = GlyphW + 1; // 1px letter spacing

        // Each byte is one row, top first; the low 5 bits are the pixels.
        private static readonly Dictionary<char, byte[]> Glyphs = new()
        {
            [' '] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            ['0'] = new byte[] { 0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E },
            ['1'] = new byte[] { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E },
            ['2'] = new byte[] { 0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F },
            ['3'] = new byte[] { 0x1F, 0x02, 0x04, 0x02, 0x01, 0x11, 0x0E },
            ['4'] = new byte[] { 0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02 },
            ['5'] = new byte[] { 0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E },
            ['6'] = new byte[] { 0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E },
            ['7'] = new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08 },
            ['8'] = new byte[] { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E },
            ['9'] = new byte[] { 0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C },
            ['A'] = new byte[] { 0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 },
            ['B'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E },
            ['C'] = new byte[] { 0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E },
            ['D'] = new byte[] { 0x1C, 0x12, 0x11, 0x11, 0x11, 0x12, 0x1C },
            ['E'] = new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F },
            ['F'] = new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10 },
            ['G'] = new byte[] { 0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F },
            ['H'] = new byte[] { 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 },
            ['I'] = new byte[] { 0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E },
            ['J'] = new byte[] { 0x07, 0x02, 0x02, 0x02, 0x02, 0x12, 0x0C },
            ['K'] = new byte[] { 0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11 },
            ['L'] = new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F },
            ['M'] = new byte[] { 0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11 },
            ['N'] = new byte[] { 0x11, 0x11, 0x19, 0x15, 0x13, 0x11, 0x11 },
            ['O'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E },
            ['P'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10 },
            ['Q'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D },
            ['R'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11 },
            ['S'] = new byte[] { 0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E },
            ['T'] = new byte[] { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 },
            ['U'] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E },
            ['V'] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04 },
            ['W'] = new byte[] { 0x11, 0x11, 0x11, 0x15, 0x15, 0x1B, 0x11 },
            ['X'] = new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11 },
            ['Y'] = new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04 },
            ['Z'] = new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F },
            ['-'] = new byte[] { 0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00 },
            ['/'] = new byte[] { 0x01, 0x02, 0x02, 0x04, 0x08, 0x08, 0x10 },
            ['.'] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x0C },
            ['+'] = new byte[] { 0x00, 0x04, 0x04, 0x1F, 0x04, 0x04, 0x00 },
            ['±'] = new byte[] { 0x04, 0x04, 0x1F, 0x04, 0x04, 0x00, 0x1F },
        };

        /// <summary>Rendered width of <paramref name="s"/> in pixels.</summary>
        public static int MeasureText(string s, int scale)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            return (s.Length * GlyphAdvance - 1) * Math.Max(1, scale);
        }

        /// <summary>
        /// Draws text with (x, y) as the bottom edge of the glyph box; y grows upward
        /// to match the bake buffer. Unmapped characters are skipped, not boxed.
        /// </summary>
        public static void DrawText(Color32[] px, int sz, int x, int y, string s,
            Color32 col, int scale, TextAlign align = TextAlign.Left) =>
            DrawText(px, sz, sz, x, y, s, col, scale, align);

        public static void DrawText(Color32[] px, int w, int h, int x, int y, string s,
            Color32 col, int scale, TextAlign align = TextAlign.Left)
        {
            if (string.IsNullOrEmpty(s)) return;
            scale = Math.Max(1, scale);

            int width = MeasureText(s, scale);
            int startX = align switch
            {
                TextAlign.Center => x - width / 2,
                TextAlign.Right  => x - width,
                _                => x,
            };

            for (int i = 0; i < s.Length; i++)
            {
                if (!Glyphs.TryGetValue(char.ToUpperInvariant(s[i]), out var rows))
                    continue;

                int gx = startX + i * GlyphAdvance * scale;
                for (int r = 0; r < GlyphH; r++)
                {
                    byte bits = rows[r];
                    if (bits == 0) continue;

                    int py = y + (GlyphH - 1 - r) * scale;
                    for (int b = 0; b < GlyphW; b++)
                    {
                        if ((bits & (1 << (GlyphW - 1 - b))) == 0) continue;
                        int gxb = gx + b * scale;
                        for (int oy = 0; oy < scale; oy++)
                        for (int ox = 0; ox < scale; ox++)
                            Set(px, w, h, gxb + ox, py + oy, col);
                    }
                }
            }
        }

        // ─── Polygons, polylines and arrows ───────────────────────────────────
        // Added for the map renderer (areal landcover, drainage, roads, APP-6D
        // control measures) but kept here so symbols and maps draw with one set of
        // primitives. All take explicit width and height: map buffers are not square.

        /// <summary>
        /// Scanline polygon fill, even-odd rule, on an implicitly closed ring.
        /// Sampling at the pixel centre keeps adjacent polygons from double-drawing
        /// their shared edge.
        /// </summary>
        public static void FillPolygon(Color32[] px, int w, int h,
            IReadOnlyList<Vector2> ring, Color32 col,
            FillPattern pattern = FillPattern.Solid, int spacing = 6)
        {
            if (ring == null || ring.Count < 3) return;

            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < ring.Count; i++)
            {
                if (ring[i].y < minY) minY = ring[i].y;
                if (ring[i].y > maxY) maxY = ring[i].y;
            }

            int yStart = Math.Max(0, (int)Math.Floor(minY));
            int yEnd   = Math.Min(h - 1, (int)Math.Ceiling(maxY));
            if (yEnd < yStart) return;

            var crossings = new List<float>(8);

            for (int y = yStart; y <= yEnd; y++)
            {
                float scanY = y + 0.5f;
                crossings.Clear();

                for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
                {
                    Vector2 a = ring[j], b = ring[i];
                    if ((a.y <= scanY && b.y > scanY) || (b.y <= scanY && a.y > scanY))
                    {
                        float t = (scanY - a.y) / (b.y - a.y);
                        crossings.Add(a.x + t * (b.x - a.x));
                    }
                }

                if (crossings.Count < 2) continue;
                crossings.Sort();

                for (int k = 0; k + 1 < crossings.Count; k += 2)
                {
                    int xa = Math.Max(0,     (int)Math.Ceiling(crossings[k]     - 0.5f));
                    int xb = Math.Min(w - 1, (int)Math.Floor  (crossings[k + 1] - 0.5f));
                    for (int x = xa; x <= xb; x++)
                        if (PatternHit(x, y, pattern, spacing))
                            Set(px, w, h, x, y, col);
                }
            }
        }

        /// <summary>Draws the segments of a polyline, optionally closing the ring.</summary>
        public static void DrawPolyline(Color32[] px, int w, int h,
            IReadOnlyList<Vector2> points, Color32 col, int thickness, bool closed = false)
        {
            if (points == null || points.Count < 2) return;

            for (int i = 1; i < points.Count; i++)
                DrawLine(px, w, h,
                    Mathf.RoundToInt(points[i - 1].x), Mathf.RoundToInt(points[i - 1].y),
                    Mathf.RoundToInt(points[i].x),     Mathf.RoundToInt(points[i].y),
                    col, thickness);

            if (closed)
                DrawLine(px, w, h,
                    Mathf.RoundToInt(points[points.Count - 1].x),
                    Mathf.RoundToInt(points[points.Count - 1].y),
                    Mathf.RoundToInt(points[0].x), Mathf.RoundToInt(points[0].y),
                    col, thickness);
        }

        /// <summary>
        /// Dashed polyline with the dash phase carried across segment joins, so the
        /// pattern stays regular around corners instead of restarting at each vertex.
        /// APP-6D uses dash length to distinguish control measures, so the rhythm
        /// carries meaning and cannot be allowed to drift.
        /// </summary>
        public static void DrawDashedPolyline(Color32[] px, int w, int h,
            IReadOnlyList<Vector2> points, Color32 col, int thickness,
            float dashLength = 8f, float gapLength = 6f, bool closed = false)
        {
            if (points == null || points.Count < 2) return;

            float period = Math.Max(1f, dashLength + gapLength);
            float travelled = 0f;
            int   radius = Math.Max(0, thickness / 2);

            int last = closed ? points.Count : points.Count - 1;

            for (int i = 0; i < last; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Count];

                float length = Vector2.Distance(a, b);
                if (length < 1e-4f) continue;

                int steps = Math.Max(1, Mathf.CeilToInt(length));
                for (int s = 0; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    if ((travelled + t * length) % period >= dashLength) continue;

                    int px0 = Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t));
                    int py0 = Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t));
                    for (int oy = -radius; oy <= radius; oy++)
                    for (int ox = -radius; ox <= radius; ox++)
                        Set(px, w, h, px0 + ox, py0 + oy, col);
                }

                travelled += length;
            }
        }

        /// <summary>
        /// Arrowhead at <paramref name="tip"/> pointing along <paramref name="direction"/>.
        /// Used by axis-of-advance and direction-of-attack control measures.
        /// </summary>
        public static void DrawArrowhead(Color32[] px, int w, int h,
            Vector2 tip, Vector2 direction, float size, Color32 col,
            int thickness = 2, bool filled = true)
        {
            if (direction.sqrMagnitude < 1e-6f) return;
            direction.Normalize();

            Vector2 perpendicular = new(-direction.y, direction.x);
            Vector2 back  = tip - direction * size;
            Vector2 left  = back + perpendicular * size * 0.5f;
            Vector2 right = back - perpendicular * size * 0.5f;

            if (filled)
            {
                FillPolygon(px, w, h, new[] { tip, left, right }, col);
            }
            else
            {
                DrawLine(px, w, h, Mathf.RoundToInt(left.x), Mathf.RoundToInt(left.y),
                    Mathf.RoundToInt(tip.x), Mathf.RoundToInt(tip.y), col, thickness);
                DrawLine(px, w, h, Mathf.RoundToInt(right.x), Mathf.RoundToInt(right.y),
                    Mathf.RoundToInt(tip.x), Mathf.RoundToInt(tip.y), col, thickness);
            }
        }

        /// <summary>Circular arc between two angles in radians, measured counter-clockwise from east.</summary>
        public static void DrawArc(Color32[] px, int w, int h, int cx, int cy, int radius,
            float startRadians, float endRadians, Color32 col, int thickness)
        {
            if (radius <= 0) return;

            int steps = Math.Max(8, (int)(Math.Abs(endRadians - startRadians) * radius));
            int t = Math.Max(0, thickness / 2);

            for (int s = 0; s <= steps; s++)
            {
                float angle = Mathf.Lerp(startRadians, endRadians, s / (float)steps);
                int x = cx + Mathf.RoundToInt(Mathf.Cos(angle) * radius);
                int y = cy + Mathf.RoundToInt(Mathf.Sin(angle) * radius);

                for (int oy = -t; oy <= t; oy++)
                for (int ox = -t; ox <= t; ox++)
                    Set(px, w, h, x + ox, y + oy, col);
            }
        }

        /// <summary>
        /// Whether a pattern paints this pixel. Patterns are a pure function of the
        /// pixel coordinate, so a filled area is stable when re-rendered and two
        /// abutting areas of the same class share one continuous texture.
        /// </summary>
        public static bool PatternHit(int x, int y, FillPattern pattern, int spacing)
        {
            spacing = Math.Max(1, spacing);
            switch (pattern)
            {
                case FillPattern.Solid:
                    return true;
                case FillPattern.DiagonalHatch:
                    return Mod(x + y, spacing) == 0;
                case FillPattern.BackDiagonalHatch:
                    return Mod(x - y, spacing) == 0;
                case FillPattern.CrossHatch:
                    return Mod(x + y, spacing) == 0 || Mod(x - y, spacing) == 0;
                case FillPattern.Dots:
                    return Mod(x, spacing) == 0 && Mod(y, spacing) == 0;
                case FillPattern.Stipple:
                    // Hashed rather than periodic: a regular lattice reads as moiré
                    // once the map is scaled, a hash reads as texture.
                    return (Hash2D(x, y) % (uint)spacing) == 0u;
                default:
                    return true;
            }
        }

        private static int Mod(int value, int modulus)
        {
            int r = value % modulus;
            return r < 0 ? r + modulus : r;
        }

        private static uint Hash2D(int x, int y)
        {
            unchecked
            {
                uint hash = (uint)x * 0x9E3779B1u ^ (uint)y * 0x85EBCA77u;
                hash ^= hash >> 15;
                hash *= 0x2545F491u;
                hash ^= hash >> 13;
                return hash;
            }
        }

        /// <summary>Dashed / dotted visibility along a border path.</summary>
        private static bool BorderVisible(int x, int y, FrameLineStyle style)
        {
            switch (style)
            {
                case FrameLineStyle.Dashed:
                    return (x + y) % 14 < 8;
                case FrameLineStyle.Dotted:
                    // Alternating black/white dots approximated as on/off gaps.
                    return (x + y) % 10 < 5;
                default:
                    return true;
            }
        }
    }
}
