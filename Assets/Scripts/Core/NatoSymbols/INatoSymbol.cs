// INatoSymbol.cs
// Composed APP-6D symbol: ordered drawable layers + text amplifiers.
// Built by NatoSymbolFactory (base) + NatoSymbolDecorator chain.

using System.Collections.Generic;
using UnityEngine;

namespace Strategos.NatoSymbols
{
    /// <summary>
    /// Immutable construction result for an APP-6D icon-based symbol.
    /// </summary>
    public interface INatoSymbol
    {
        SIDCCode Code { get; }
        IReadOnlyList<SymbolLayerDraw> Layers { get; }
        SymbolTextAmplifiers Text { get; }
    }

    /// <summary>
    /// Text amplifiers placed outside the frame (APP-6D Table 3-2 Fields T, M, F, …).
    /// </summary>
    public struct SymbolTextAmplifiers
    {
        public string Designation;      // Field T
        public string HigherFormation;  // Field M
        public string StrengthLabel;
        public StrengthModifier StrengthModifier; // Field F (+ / - / ±)

        public static SymbolTextAmplifiers FromCode(SIDCCode code) => new SymbolTextAmplifiers
        {
            Designation      = code.Designation ?? string.Empty,
            HigherFormation  = code.HigherFormation ?? string.Empty,
            StrengthLabel    = code.StrengthLabel ?? string.Empty,
            StrengthModifier = code.StrengthModifier,
        };

        public string StrengthDisplay
        {
            get
            {
                var s = StrengthLabel ?? string.Empty;
                switch (StrengthModifier)
                {
                    case StrengthModifier.Reinforced:        return s + "+";
                    case StrengthModifier.Reduced:           return s + "-";
                    case StrengthModifier.ReinforcedReduced: return s + "±";
                    default: return s;
                }
            }
        }
    }

    /// <summary>
    /// One drawable contribution to a composed symbol.
    /// Either a pre-authored Sprite, or a procedural draw callback into a pixel buffer.
    /// </summary>
    public struct SymbolLayerDraw
    {
        public SymbolLayer Kind;
        public string      Name;
        public Sprite      Sprite;
        public Color       Tint;
        public int         SortOrder;
        public ProceduralDraw Draw;

        public static SymbolLayerDraw FromSprite(SymbolLayer kind, string name, Sprite sprite, Color tint, int sortOrder)
        {
            return new SymbolLayerDraw
            {
                Kind      = kind,
                Name      = name,
                Sprite    = sprite,
                Tint      = tint,
                SortOrder = sortOrder,
                Draw      = null,
            };
        }

        public static SymbolLayerDraw FromProcedural(SymbolLayer kind, string name, ProceduralDraw draw, int sortOrder)
        {
            return new SymbolLayerDraw
            {
                Kind      = kind,
                Name      = name,
                Sprite    = null,
                Tint      = Color.white,
                SortOrder = sortOrder,
                Draw      = draw,
            };
        }
    }

    /// <summary>
    /// Procedural pixel contribution. Buffer is RGBA32, y=0 at bottom, size×size.
    /// Layout constants are in SymbolLayout (base 256).
    /// </summary>
    public delegate void ProceduralDraw(Color32[] buffer, int size, float scale);

    /// <summary>
    /// Shared bounding-octagon layout (APP-6D §1.3 / Figure 1-15).
    /// Base resolution BASE=256; L = octagon height ≈ frame content height.
    /// Vertical sectors: top .3L (sector 1), mid .4L (main), bottom .3L (sector 2).
    /// </summary>
    public static class SymbolLayout
    {
        public const int BASE = 256;

        // Frame bounding box (land unit closed frame)
        public const int FrameLeft   = 24;
        public const int FrameRight  = 232;
        public const int FrameBottom = 56;
        public const int FrameTop    = 180;
        public const int FrameCX     = 128;
        public const int FrameCY     = 118;
        public const int FrameHalfW  = 104;
        public const int FrameHalfH  = 62;
        public const int BorderWidth = 4;

        // Bounding octagon ≈ frame interior
        public const int OctagonL = 124; // ≈ FrameTop - FrameBottom

        // Main sector (middle .4L)
        public const int MainBottom = FrameBottom + (int)(0.3f * OctagonL); // ~93
        public const int MainTop    = FrameTop    - (int)(0.3f * OctagonL); // ~143
        public const int MainCY     = (MainBottom + MainTop) / 2;

        // Sector 1 (top .3L inside frame) and Sector 2 (bottom .3L)
        public const int Sector1CY = FrameTop    - (int)(0.15f * OctagonL);
        public const int Sector2CY = FrameBottom + (int)(0.15f * OctagonL);

        // Amplifier zones outside frame
        public const int EchelonCY = 214;
        public const int HqLineY   = 40;
        public const int TfBracketY = 196;

        public static int Scale(int v, float scale) => Mathf.RoundToInt(v * scale);
    }
}
