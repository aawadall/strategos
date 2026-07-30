// MapData.cs
// The authoritative in-memory representation of one map.
// Data only — nothing here rasterises, meshes, or touches the scene. The 2D
// rasteriser and the 3D mesh builder both read this and nothing else, which is
// what keeps the two views from drifting apart.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Strategos.Maps
{
    /// <summary>
    /// Metadata describing a map's extent, scale and georeferencing.
    /// </summary>
    public sealed class MapHeader
    {
        public string Name = "Untitled";
        public int    Seed;

        /// <summary>Cell counts. Elevation and Landcover are Width * Height long.</summary>
        public int Width  = 512;
        public int Height = 512;

        /// <summary>Ground distance one cell spans, in metres.</summary>
        public float MetresPerCell = 25f;

        /// <summary>Elevation extent in metres AMSL, refreshed by <see cref="MapData.RecalculateElevationBounds"/>.</summary>
        public float MinElevation;
        public float MaxElevation;

        /// <summary>Contour spacing in metres for the topographic renderer.</summary>
        public float ContourInterval = 20f;

        /// <summary>Every fifth contour is drawn heavy and labelled.</summary>
        public int IndexContourEvery = 5;

        /// <summary>Elevation treated as sea level when classifying water.</summary>
        public float SeaLevel;

        /// <summary>UTM position of the south-west corner of cell (0, 0).</summary>
        public GeoOrigin Origin = GeoOrigin.Default;

        public float WidthMetres  => Width  * MetresPerCell;
        public float HeightMetres => Height * MetresPerCell;

        public MapHeader Clone() => (MapHeader)MemberwiseClone();
    }

    /// <summary>
    /// A complete map: a bare-earth elevation grid, a landcover grid, and vector
    /// features derived from them.
    ///
    /// Indexing is row-major, <c>index = y * Width + x</c>, with <b>x east and
    /// y north</b>; y = 0 is the southern edge. That matches the bottom-up pixel
    /// convention the symbol baker already uses, so a rendered map needs no
    /// vertical flip.
    ///
    /// Elevation is the authority. Landcover and features are produced from it at
    /// generation time, but are then stored rather than re-derived, so a
    /// hand-authored fixture can override either without fighting the generator.
    /// </summary>
    public sealed class MapData
    {
        public MapHeader Header;

        /// <summary>Bare-earth elevation in metres AMSL. Length = Width * Height.</summary>
        public float[] Elevation;

        /// <summary>Terrain class per cell, values from <see cref="LandcoverClass"/>.</summary>
        public byte[] Landcover;

        public List<MapPolyline> Lines = new();
        public List<MapPolygon>  Areas = new();
        public List<MapPoi>      Pois  = new();

        public int   Width         => Header.Width;
        public int   Height        => Header.Height;
        public float MetresPerCell => Header.MetresPerCell;

        // ─── Construction ─────────────────────────────────────────────────────

        public MapData() { }

        public MapData(MapHeader header)
        {
            Header    = header ?? throw new ArgumentNullException(nameof(header));
            Elevation = new float[header.Width * header.Height];
            Landcover = new byte[header.Width * header.Height];
        }

        // ─── Cell access ──────────────────────────────────────────────────────

        public bool InBounds(int x, int y) =>
            (uint)x < (uint)Header.Width && (uint)y < (uint)Header.Height;

        public int Index(int x, int y) => y * Header.Width + x;

        /// <summary>Elevation at an integer cell, clamped at the edges.</summary>
        public float GetElevation(int x, int y)
        {
            x = Mathf.Clamp(x, 0, Header.Width  - 1);
            y = Mathf.Clamp(y, 0, Header.Height - 1);
            return Elevation[y * Header.Width + x];
        }

        public void SetElevation(int x, int y, float metres)
        {
            if (!InBounds(x, y)) return;
            Elevation[y * Header.Width + x] = metres;
        }

        public LandcoverClass GetLandcover(int x, int y)
        {
            x = Mathf.Clamp(x, 0, Header.Width  - 1);
            y = Mathf.Clamp(y, 0, Header.Height - 1);
            return (LandcoverClass)Landcover[y * Header.Width + x];
        }

        public void SetLandcover(int x, int y, LandcoverClass c)
        {
            if (!InBounds(x, y)) return;
            Landcover[y * Header.Width + x] = (byte)c;
        }

        // ─── Sampling ─────────────────────────────────────────────────────────

        /// <summary>
        /// Bilinearly interpolated elevation at a fractional cell position.
        /// Used by the 3D mesh, by symbol anchoring, and by line-of-sight.
        /// </summary>
        public float SampleElevation(float x, float y)
        {
            x = Mathf.Clamp(x, 0f, Header.Width  - 1.001f);
            y = Mathf.Clamp(y, 0f, Header.Height - 1.001f);

            int x0 = (int)x, y0 = (int)y;
            int x1 = Mathf.Min(x0 + 1, Header.Width  - 1);
            int y1 = Mathf.Min(y0 + 1, Header.Height - 1);
            float fx = x - x0, fy = y - y0;

            int w = Header.Width;
            float e00 = Elevation[y0 * w + x0];
            float e10 = Elevation[y0 * w + x1];
            float e01 = Elevation[y1 * w + x0];
            float e11 = Elevation[y1 * w + x1];

            float bottom = e00 + (e10 - e00) * fx;
            float top    = e01 + (e11 - e01) * fx;
            return bottom + (top - bottom) * fy;
        }

        /// <summary>
        /// Elevation of the visual surface: bare earth plus the landcover canopy.
        /// This — not <see cref="GetElevation"/> — is what line-of-sight traces
        /// against, so a wood blocks sight the ground alone would not.
        /// </summary>
        public float GetSurfaceElevation(int x, int y) =>
            GetElevation(x, y) + LandcoverInfo.CanopyHeightMetres(GetLandcover(x, y));

        /// <summary>
        /// Central-difference gradient in metres of rise per metre of ground run.
        /// x component points east, y component points north.
        /// </summary>
        public Vector2 SampleGradient(int x, int y)
        {
            float inv = 1f / (2f * Header.MetresPerCell);
            float dzdx = (GetElevation(x + 1, y) - GetElevation(x - 1, y)) * inv;
            float dzdy = (GetElevation(x, y + 1) - GetElevation(x, y - 1)) * inv;
            return new Vector2(dzdx, dzdy);
        }

        /// <summary>Slope angle in radians, 0 flat.</summary>
        public float SampleSlope(int x, int y)
        {
            Vector2 g = SampleGradient(x, y);
            return Mathf.Atan(g.magnitude);
        }

        public float SampleSlopeDegrees(int x, int y) =>
            SampleSlope(x, y) * Mathf.Rad2Deg;

        /// <summary>
        /// Unit surface normal in Unity world axes (x east, y up, z north).
        /// Used for hillshading in 2D and for mesh normals in 3D, so both views
        /// are lit from the same geometry.
        /// </summary>
        public Vector3 SampleNormal(int x, int y)
        {
            Vector2 g = SampleGradient(x, y);
            return new Vector3(-g.x, 1f, -g.y).normalized;
        }

        // ─── World-space conversion ───────────────────────────────────────────

        /// <summary>
        /// Cell position → Unity world position, with the map's south-west corner
        /// at the world origin. y is elevation in metres.
        /// </summary>
        public Vector3 CellToWorld(float x, float y) => new Vector3(
            x * Header.MetresPerCell,
            SampleElevation(x, y),
            y * Header.MetresPerCell);

        public Vector2 WorldToCell(Vector3 world) => new Vector2(
            world.x / Header.MetresPerCell,
            world.z / Header.MetresPerCell);

        // ─── Maintenance ──────────────────────────────────────────────────────

        /// <summary>
        /// Refreshes <see cref="MapHeader.MinElevation"/> / <see cref="MapHeader.MaxElevation"/>.
        /// Every stage that writes elevation must call this before the header is
        /// read, because the hypsometric renderer normalises against these bounds
        /// and a stale range silently flattens the whole colour ramp.
        /// </summary>
        public void RecalculateElevationBounds()
        {
            if (Elevation == null || Elevation.Length == 0)
            {
                Header.MinElevation = Header.MaxElevation = 0f;
                return;
            }

            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < Elevation.Length; i++)
            {
                float e = Elevation[i];
                if (e < min) min = e;
                if (e > max) max = e;
            }
            Header.MinElevation = min;
            Header.MaxElevation = max;
        }

        /// <summary>Elevation mapped to 0–1 across the map's own range.</summary>
        public float NormalisedElevation(float metres)
        {
            float span = Header.MaxElevation - Header.MinElevation;
            if (span <= 0.0001f) return 0f;
            return Mathf.Clamp01((metres - Header.MinElevation) / span);
        }
    }
}
