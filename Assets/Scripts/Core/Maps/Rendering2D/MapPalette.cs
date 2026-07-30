// MapPalette.cs
// Colour schemes for the 2D map renderer.
//
// The Topographic palette is the one the symbol builder's backdrop already used
// (paper E9E5D6, water C4D6D0, contours C4B99C / A89A78, grid CEC8B2), promoted
// from a throwaway local to the canonical scheme so symbols keep sitting on the
// paper they were designed against.

using UnityEngine;

namespace Strategos.Maps
{
    /// <summary>
    /// Presentation style. Mirrors the four modes the roadmap calls for: a survey
    /// sheet, a stripped-back operations map, a terrain-dominant view, and a blend.
    /// </summary>
    public enum MapRenderMode
    {
        Topographic = 0,
        Schematic   = 1,
        Terrain     = 2,
        Hybrid      = 3,
    }

    /// <summary>Style for one class of linear feature.</summary>
    public readonly struct LineStyle
    {
        public readonly Color32 Casing;
        public readonly Color32 Fill;
        public readonly int     Width;
        public readonly bool    Dashed;

        public LineStyle(Color32 casing, Color32 fill, int width, bool dashed = false)
        {
            Casing = casing;
            Fill   = fill;
            Width  = width;
            Dashed = dashed;
        }
    }

    public sealed class MapPalette
    {
        // Base surfaces
        public Color32 Paper;
        public Color32 Water;
        public Color32 WaterEdge;

        // Relief
        public Color32 Contour;
        public Color32 ContourIndex;
        public Color32 ContourWater;   // submarine / depression contours
        public float   HillshadeStrength;

        // Grid
        public Color32 Grid;
        public Color32 GridIndex;
        public Color32 GridLabel;

        // Text
        public Color32 Ink;
        public Color32 InkMuted;
        public Color32 WaterInk;

        /// <summary>Outline drawn around label glyphs so they stay legible over detail.</summary>
        public Color32 LabelHalo;

        // Landcover
        public Color32 Open;
        public Color32 Cropland;
        public Color32 Forest;
        public Color32 ForestStipple;
        public Color32 Urban;
        public Color32 UrbanHatch;
        public Color32 Marsh;
        public Color32 MarshTick;
        public Color32 Rock;
        public Color32 Sand;
        public Color32 Snow;

        // Hypsometric ramp, low to high. Positions are normalised elevation.
        public float[]   RampPositions;
        public Color32[] RampColours;

        /// <summary>When false the hypsometric ramp is skipped and Paper fills the sheet.</summary>
        public bool UseHypsometricTint = true;

        /// <summary>
        /// When true only index contours are drawn. An operations map keeps enough
        /// relief to read the ground and no more; the intermediate lines are what
        /// crowd out the control measures drawn on top.
        /// </summary>
        public bool IndexContoursOnly;

        /// <summary>When false landcover is not drawn at all (Schematic).</summary>
        public bool UseLandcover = true;

        /// <summary>
        /// How strongly landcover covers the hypsometric tint, 0–1. Below 1 the two
        /// read together — a wood on a hillside still shows the hillside's elevation
        /// band — which is what a survey sheet does. At 1 landcover wins outright,
        /// which is what a terrain view wants.
        /// </summary>
        public float LandcoverBlend = 0.55f;

        // ─── Lookups ──────────────────────────────────────────────────────────

        public Color32 Hypsometric(float t)
        {
            if (RampPositions == null || RampPositions.Length == 0) return Paper;

            t = Mathf.Clamp01(t);
            for (int i = 1; i < RampPositions.Length; i++)
            {
                if (t > RampPositions[i]) continue;

                float span = RampPositions[i] - RampPositions[i - 1];
                float f    = span <= 0f ? 0f : (t - RampPositions[i - 1]) / span;
                return Color32.Lerp(RampColours[i - 1], RampColours[i], f);
            }
            return RampColours[RampColours.Length - 1];
        }

        public Color32 LandcoverColour(LandcoverClass c)
        {
            switch (c)
            {
                case LandcoverClass.Water:    return Water;
                case LandcoverClass.Forest:   return Forest;
                case LandcoverClass.Urban:    return Urban;
                case LandcoverClass.Cropland: return Cropland;
                case LandcoverClass.Marsh:    return Marsh;
                case LandcoverClass.Rock:     return Rock;
                case LandcoverClass.Sand:     return Sand;
                case LandcoverClass.Snow:     return Snow;
                default:                      return Open;
            }
        }

        /// <summary>
        /// Road and drainage styling. Widths are at 1:1 render scale and are scaled
        /// by the rasteriser, so a zoomed-out sheet does not end up all road.
        /// </summary>
        public LineStyle LineStyleFor(MapLineKind kind)
        {
            switch (kind)
            {
                case MapLineKind.Highway: return new LineStyle(Hex(0x8C, 0x3A, 0x26), Hex(0xDE, 0x8A, 0x5A), 5);
                case MapLineKind.Road:    return new LineStyle(Hex(0x6E, 0x68, 0x58), Hex(0xFA, 0xF7, 0xEE), 4);
                case MapLineKind.Track:   return new LineStyle(Hex(0x6E, 0x68, 0x58), Hex(0x6E, 0x68, 0x58), 2, dashed: true);
                case MapLineKind.Trail:   return new LineStyle(Hex(0x7A, 0x74, 0x64), Hex(0x7A, 0x74, 0x64), 1, dashed: true);
                case MapLineKind.Railway: return new LineStyle(Hex(0x2A, 0x2A, 0x2A), Hex(0x2A, 0x2A, 0x2A), 3);
                case MapLineKind.River:   return new LineStyle(WaterEdge, Water, 4);
                case MapLineKind.Stream:  return new LineStyle(WaterEdge, WaterEdge, 2);
                case MapLineKind.Canal:   return new LineStyle(WaterEdge, Water, 3);
                case MapLineKind.Coast:   return new LineStyle(WaterEdge, WaterEdge, 2);
                default:                  return new LineStyle(InkMuted, InkMuted, 1);
            }
        }

        // ─── Presets ──────────────────────────────────────────────────────────

        public static MapPalette For(MapRenderMode mode)
        {
            switch (mode)
            {
                case MapRenderMode.Schematic: return Schematic();
                case MapRenderMode.Terrain:   return Terrain();
                case MapRenderMode.Hybrid:    return Hybrid();
                default:                      return Topographic();
            }
        }

        /// <summary>Survey-sheet look: hypsometric tint, full contours, all detail.</summary>
        public static MapPalette Topographic() => new MapPalette
        {
            Paper        = Hex(0xE9, 0xE5, 0xD6),
            Water        = Hex(0xC4, 0xD6, 0xD0),
            WaterEdge    = Hex(0x7F, 0xA8, 0xA0),

            Contour      = Hex(0xC4, 0xB9, 0x9C),
            ContourIndex = Hex(0xA8, 0x9A, 0x78),
            ContourWater = Hex(0x9F, 0xBC, 0xB6),
            HillshadeStrength = 0.30f,

            Grid      = Hex(0xCE, 0xC8, 0xB2),
            GridIndex = Hex(0xB0, 0xA8, 0x8C),
            GridLabel = Hex(0x6A, 0x64, 0x50),

            Ink       = Hex(0x2B, 0x28, 0x20),
            InkMuted  = Hex(0x5C, 0x57, 0x48),
            WaterInk  = Hex(0x3C, 0x6B, 0x66),
            LabelHalo = Hex(0xF6, 0xF4, 0xEC),

            Open          = Hex(0xE9, 0xE5, 0xD6),
            Cropland      = Hex(0xE7, 0xE4, 0xCA),
            Forest        = Hex(0xC9, 0xD9, 0xBC),
            ForestStipple = Hex(0x8F, 0xA8, 0x7C),
            Urban         = Hex(0xDA, 0xD3, 0xC3),
            UrbanHatch    = Hex(0x9A, 0x94, 0x84),
            Marsh         = Hex(0xCF, 0xDC, 0xCE),
            MarshTick     = Hex(0x7C, 0x9A, 0x86),
            Rock          = Hex(0xD6, 0xD0, 0xC2),
            Sand          = Hex(0xE9, 0xDD, 0xBB),
            Snow          = Hex(0xF5, 0xF3, 0xED),

            RampPositions = new[] { 0f, 0.18f, 0.38f, 0.58f, 0.78f, 1f },
            RampColours = new[]
            {
                Hex(0xD7, 0xE2, 0xC6), // valley floor green
                Hex(0xE4, 0xE5, 0xC4),
                Hex(0xE9, 0xDE, 0xB4),
                Hex(0xE2, 0xCB, 0x9C),
                Hex(0xD6, 0xB1, 0x86),
                Hex(0xC8, 0x9C, 0x78), // summit tan
            },
        };

        /// <summary>
        /// Operations-map look: flat paper, no tint, no hillshade, contours reduced
        /// to index lines only. Everything that is not a control measure or a symbol
        /// steps back so the tactical picture reads first.
        /// </summary>
        public static MapPalette Schematic()
        {
            var p = Topographic();
            p.Paper             = Hex(0xF2, 0xF0, 0xE8);
            p.Open              = p.Paper;
            p.Cropland          = p.Paper;
            p.UseHypsometricTint = false;
            p.LandcoverBlend    = 1f;   // nothing left to blend with once the tint is off
            p.HillshadeStrength = 0f;
            p.IndexContoursOnly = true;
            p.Contour           = Hex(0xDD, 0xD7, 0xC4);
            p.ContourIndex      = Hex(0xC4, 0xBC, 0xA4);
            p.Forest            = Hex(0xDF, 0xE7, 0xD8);
            p.ForestStipple     = Hex(0xB2, 0xC4, 0xA6);
            p.Urban             = Hex(0xE2, 0xDE, 0xD2);
            p.Water             = Hex(0xD5, 0xE2, 0xDE);
            return p;
        }

        /// <summary>Landcover-dominant look: ground cover reads first, relief is shading only.</summary>
        public static MapPalette Terrain()
        {
            var p = Topographic();
            p.UseHypsometricTint = false;
            p.LandcoverBlend     = 1f;
            p.HillshadeStrength  = 0.55f;
            p.Open        = Hex(0xCF, 0xCE, 0xA6);
            p.Cropland    = Hex(0xDF, 0xD8, 0x9E);
            p.Forest      = Hex(0x8F, 0xAA, 0x7C);
            p.ForestStipple = Hex(0x6C, 0x88, 0x59);
            p.Urban       = Hex(0xC2, 0xB8, 0xA8);
            p.UrbanHatch  = Hex(0x8A, 0x82, 0x74);
            p.Marsh       = Hex(0xA8, 0xC0, 0xA8);
            p.Rock        = Hex(0xB4, 0xAC, 0x9E);
            p.Sand        = Hex(0xE0, 0xD0, 0x9C);
            p.Snow        = Hex(0xF2, 0xF2, 0xF0);
            p.Water       = Hex(0x9C, 0xBE, 0xC4);
            p.Contour     = Hex(0xB8, 0xAC, 0x92);
            p.ContourIndex = Hex(0x94, 0x88, 0x6E);
            return p;
        }

        /// <summary>Topographic tint with terrain-strength shading; the default 3D drape.</summary>
        public static MapPalette Hybrid()
        {
            var p = Topographic();
            p.HillshadeStrength = 0.45f;
            p.LandcoverBlend = 0.7f;
            p.Forest        = Hex(0xB6, 0xCA, 0xA6);
            p.ForestStipple = Hex(0x7E, 0x99, 0x6A);
            return p;
        }

        private static Color32 Hex(int r, int g, int b) => new((byte)r, (byte)g, (byte)b, 255);
    }
}
