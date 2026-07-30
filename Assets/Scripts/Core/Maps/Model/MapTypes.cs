// MapTypes.cs
// Enums and small value types for the map model.
// The heavyweight container is MapData; georeferencing lives in MapCoordinates.

using System.Collections.Generic;
using UnityEngine;

namespace Strategos.Maps
{
    /// <summary>
    /// Terrain classification of a single cell. Stored as one byte per cell in
    /// <see cref="MapData.Landcover"/>, so the underlying values must stay 0–255
    /// and must not be renumbered once a fixture map is authored against them.
    /// </summary>
    public enum LandcoverClass : byte
    {
        Open     = 0, // bare ground, grass, pasture
        Cropland = 1,
        Forest   = 2,
        Urban    = 3,
        Water    = 4,
        Marsh    = 5,
        Rock     = 6, // exposed rock / scree above the treeline
        Sand     = 7,
        Snow     = 8,
    }

    /// <summary>
    /// Per-class properties the rest of the system reads instead of switching on
    /// <see cref="LandcoverClass"/> in a dozen places.
    ///
    /// Named <c>LandcoverInfo</c> rather than <c>Landcover</c> because
    /// <see cref="MapData.Landcover"/> is the per-cell array; a static class of
    /// the same name would be shadowed by the field inside MapData.
    /// </summary>
    public static class LandcoverInfo
    {
        public const int Count = 9;

        /// <summary>
        /// Height in metres that a class adds above the bare-earth elevation for
        /// line-of-sight purposes. Bare-earth classes are 0; this is what makes a
        /// forested valley floor block sight that open ground would not.
        /// </summary>
        public static float CanopyHeightMetres(LandcoverClass c)
        {
            switch (c)
            {
                case LandcoverClass.Forest: return 15f;
                case LandcoverClass.Urban:  return 10f;
                case LandcoverClass.Marsh:  return 2f;
                default:                    return 0f;
            }
        }

        public static bool IsWater(LandcoverClass c) =>
            c == LandcoverClass.Water;

        /// <summary>True for classes that a settlement or road may not be placed on.</summary>
        public static bool IsImpassableToBuilding(LandcoverClass c) =>
            c == LandcoverClass.Water || c == LandcoverClass.Marsh;

        public static string DisplayName(LandcoverClass c)
        {
            switch (c)
            {
                case LandcoverClass.Open:     return "Open";
                case LandcoverClass.Cropland: return "Cropland";
                case LandcoverClass.Forest:   return "Forest";
                case LandcoverClass.Urban:    return "Urban";
                case LandcoverClass.Water:    return "Water";
                case LandcoverClass.Marsh:    return "Marsh";
                case LandcoverClass.Rock:     return "Rock";
                case LandcoverClass.Sand:     return "Sand";
                case LandcoverClass.Snow:     return "Snow";
                default:                      return "Unknown";
            }
        }
    }

    /// <summary>Linear map feature class. Drives width, colour and dash pattern.</summary>
    public enum MapLineKind
    {
        Coast    = 0,
        River    = 1,
        Stream   = 2,
        Canal    = 3,
        Highway  = 4,
        Road     = 5,
        Track    = 6,
        Trail    = 7,
        Railway  = 8,
        Ridge    = 9, // generator scaffolding / authored crest line, not normally drawn
    }

    /// <summary>Areal map feature class.</summary>
    public enum MapAreaKind
    {
        BuiltUp   = 0,
        Woodblock = 1,
        Lake      = 2,
        Marsh     = 3,
    }

    /// <summary>Point of interest class.</summary>
    public enum MapPoiKind
    {
        City        = 0,
        Town        = 1,
        Village     = 2,
        HillTop     = 3,
        SpotHeight  = 4,
        Ford        = 5,
        Bridge      = 6,
        Crossroads  = 7,
        Pass        = 8,
    }

    /// <summary>Grid overlay flavour. See MapCoordinates for the conversions.</summary>
    public enum GridSystem
    {
        None   = 0,
        Mgrs   = 1,
        Utm    = 2,
        LatLon = 3,
    }

    /// <summary>
    /// A linear feature in cell space. Points are fractional cell coordinates with
    /// x east and y north, matching <see cref="MapData"/> indexing.
    /// </summary>
    public sealed class MapPolyline
    {
        public MapLineKind    Kind;
        public List<Vector2>  Points = new();
        public string         Name;

        /// <summary>
        /// Relative prominence, 0–1. Rivers use normalised flow accumulation so a
        /// trunk river draws wider than a headwater; roads use it for class weight.
        /// </summary>
        public float Weight = 1f;

        public MapPolyline() { }

        public MapPolyline(MapLineKind kind, IEnumerable<Vector2> points, float weight = 1f, string name = null)
        {
            Kind   = kind;
            Weight = weight;
            Name   = name;
            if (points != null) Points.AddRange(points);
        }
    }

    /// <summary>An areal feature in cell space. The ring is implicitly closed.</summary>
    public sealed class MapPolygon
    {
        public MapAreaKind   Kind;
        public List<Vector2> Points = new();
        public string        Name;

        public MapPolygon() { }

        public MapPolygon(MapAreaKind kind, IEnumerable<Vector2> points, string name = null)
        {
            Kind = kind;
            Name = name;
            if (points != null) Points.AddRange(points);
        }
    }

    /// <summary>A named point in cell space.</summary>
    public sealed class MapPoi
    {
        public MapPoiKind Kind;
        public Vector2    Position;
        public string     Name;

        /// <summary>Elevation in metres, cached at generation time for spot heights.</summary>
        public float ElevationMetres;

        /// <summary>Population for settlements; 0 for everything else.</summary>
        public int Population;

        public MapPoi() { }

        public MapPoi(MapPoiKind kind, Vector2 position, string name = null)
        {
            Kind     = kind;
            Position = position;
            Name     = name;
        }
    }

    /// <summary>
    /// Georeferencing anchor for a map: the UTM position of the south-west corner
    /// of cell (0, 0). Every cell → MGRS conversion is an offset from this.
    /// </summary>
    public struct GeoOrigin
    {
        public int   Zone;        // UTM zone 1–60
        public char  Band;        // MGRS latitude band C–X (omitting I and O)
        public double Easting;    // metres
        public double Northing;   // metres

        public bool IsNorthernHemisphere => Band >= 'N';

        /// <summary>
        /// Default anchor: zone 32U, roughly central Germany. Chosen so the
        /// unconfigured case still produces plausible grid designators rather than
        /// zone 0 nonsense.
        /// </summary>
        public static GeoOrigin Default => new GeoOrigin
        {
            Zone     = 32,
            Band     = 'U',
            Easting  = 500000.0,
            Northing = 5600000.0,
        };
    }
}
