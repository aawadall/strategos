// MapViewport.cs
// The cell-space → pixel-space transform shared by everything that draws a map.
//
// Kept as one value rather than passed around as a scale and an offset because the
// grid overlay, the feature renderer and the label placer must agree exactly: a
// half-pixel disagreement puts a town's label off its dot and a grid label off its
// line, and both are the kind of error that only shows up in a finished sheet.

using UnityEngine;

namespace Strategos.Maps
{
    public readonly struct MapViewport
    {
        /// <summary>The cell-space extent being rendered.</summary>
        public readonly Rect CellWindow;

        /// <summary>Output pixels per map cell. Below 1 the sheet is downsampled.</summary>
        public readonly float PixelsPerCell;

        public readonly int Width;
        public readonly int Height;

        public MapViewport(Rect cellWindow, float pixelsPerCell)
        {
            PixelsPerCell = Mathf.Max(0.01f, pixelsPerCell);
            CellWindow    = cellWindow;
            Width         = Mathf.Max(1, Mathf.RoundToInt(cellWindow.width  * PixelsPerCell));
            Height        = Mathf.Max(1, Mathf.RoundToInt(cellWindow.height * PixelsPerCell));
        }

        /// <summary>
        /// The whole map. The window starts half a cell outside cell 0 because a cell
        /// coordinate names a sample point, not a square: cell 0's centre has to land
        /// on the first pixel's centre, or the whole sheet is shifted half a cell and
        /// a 512-cell map renders 511 px wide.
        /// </summary>
        public static MapViewport ForWholeMap(MapData map, float pixelsPerCell) =>
            new MapViewport(new Rect(-0.5f, -0.5f, map.Width, map.Height), pixelsPerCell);

        public static MapViewport ForWindow(Rect cellWindow, float pixelsPerCell) =>
            new MapViewport(cellWindow, pixelsPerCell);

        public int PixelCount => Width * Height;

        // ─── Transform ────────────────────────────────────────────────────────

        public Vector2 CellToPixel(float x, float y) => new Vector2(
            (x - CellWindow.xMin) * PixelsPerCell,
            (y - CellWindow.yMin) * PixelsPerCell);

        public Vector2 CellToPixel(Vector2 cell) => CellToPixel(cell.x, cell.y);

        /// <summary>Cell position sampled by a pixel, taken at the pixel's centre.</summary>
        public Vector2 PixelToCell(int px, int py) => new Vector2(
            CellWindow.xMin + (px + 0.5f) / PixelsPerCell,
            CellWindow.yMin + (py + 0.5f) / PixelsPerCell);

        /// <summary>Ground distance in pixels, for scaling anything specified in metres.</summary>
        public float MetresToPixels(MapHeader header, float metres) =>
            metres / Mathf.Max(0.001f, header.MetresPerCell) * PixelsPerCell;

        /// <summary>
        /// The zoom that <see cref="MapPalette"/>'s line widths and the renderer's mark
        /// sizes are authored against. A road of 4 px reads correctly here.
        /// </summary>
        public const float ReferencePixelsPerCell = 3f;

        /// <summary>
        /// Multiplier taking authored stroke widths to this viewport's zoom. Below 1 at
        /// overview zooms, and deliberately not floored there: a 512-cell map drawn at
        /// 1 px per cell has its whole drainage network inside 512 px, and a stream
        /// held at its authored 2 px is 50 m of ground width — draw the network that
        /// way and the sheet is all river. Callers floor the *final* pixel width at 1
        /// so features thin out to hairlines instead of disappearing.
        /// </summary>
        public float StrokeScale => PixelsPerCell / ReferencePixelsPerCell;
    }
}
