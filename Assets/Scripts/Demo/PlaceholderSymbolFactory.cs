// PlaceholderSymbolFactory.cs
// Generates simple colored sprites at runtime, no art assets required.
// Used by SymbolDemoSpawner until real NATO APP-6D artwork is available.

using System.Collections.Generic;
using UnityEngine;

namespace Strategos.Demo
{
    using NatoSymbols;

    public static class PlaceholderSymbolFactory
    {
        private const int TexSize   = 128;
        private const int Border    = 8;
        private const int DotSize   = 9;
        private const int DotGap    = 4;

        // Cache sprites per (Affiliation, Echelon) pair.
        private static readonly Dictionary<long, Sprite> _cache = new();

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>Returns (and caches) a placeholder sprite for the given SIDC code.</summary>
        public static Sprite Get(SIDCCode code)
        {
            long key = ((long)(int)code.Affiliation << 32) | (long)(int)code.Echelon;
            if (_cache.TryGetValue(key, out var cached)) return cached;

            var sprite = Build(code);
            _cache[key] = sprite;
            return sprite;
        }

        /// <summary>Destroys all cached textures. Call when leaving the demo scene.</summary>
        public static void ClearCache()
        {
            foreach (var s in _cache.Values)
                if (s != null) Object.Destroy(s.texture);
            _cache.Clear();
        }

        // -------------------------------------------------------------------------
        // Builder
        // -------------------------------------------------------------------------

        private static Sprite Build(SIDCCode code)
        {
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
                name       = $"Placeholder_{code.Affiliation}_{code.Echelon}",
            };

            var fill   = AffiliationColour.ForAffiliation(code.Affiliation);
            var border = Color.black;

            // --- Draw frame ---
            for (int y = 0; y < TexSize; y++)
            for (int x = 0; x < TexSize; x++)
            {
                bool isBorder = x < Border || x >= TexSize - Border ||
                                y < Border || y >= TexSize - Border;
                tex.SetPixel(x, y, isBorder ? border : fill);
            }

            // --- Hostile: draw diagonal cross over the fill ---
            if (code.Affiliation == Affiliation.Hostile ||
                code.Affiliation == Affiliation.Suspect)
            {
                DrawDiagonalCross(tex, fill);
            }

            // --- Unknown: draw concentric inner ring ---
            if (code.Affiliation == Affiliation.Unknown ||
                code.Affiliation == Affiliation.Pending)
            {
                DrawInnerRing(tex, fill);
            }

            // --- Echelon marks in the top-centre ---
            DrawEchelonMark(tex, code.Echelon);

            tex.Apply(false);
            return Sprite.Create(tex,
                new Rect(0, 0, TexSize, TexSize),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: TexSize);
        }

        // -------------------------------------------------------------------------
        // Drawing primitives
        // -------------------------------------------------------------------------

        private static void DrawDiagonalCross(Texture2D tex, Color fill)
        {
            // Two diagonal stripes across the inner area.
            int inner = Border;
            int size  = TexSize - Border * 2;
            for (int i = 0; i < size; i++)
            {
                // Top-left to bottom-right diagonal band (width 5)
                for (int w = -2; w <= 2; w++)
                {
                    SetIfInside(tex, inner + i + w, inner + i, Color.black);
                    SetIfInside(tex, inner + i + w, inner + size - 1 - i, Color.black);
                }
            }
        }

        private static void DrawInnerRing(Texture2D tex, Color fill)
        {
            int inner  = Border + 8;
            int outer  = TexSize - inner;
            int ring   = 4;
            for (int y = inner; y < outer; y++)
            for (int x = inner; x < outer; x++)
            {
                bool isRing = x >= inner && x < inner + ring ||
                              x >= outer - ring && x < outer ||
                              y >= inner && y < inner + ring ||
                              y >= outer - ring && y < outer;
                if (isRing) tex.SetPixel(x, y, Color.black);
            }
        }

        /// <summary>Draws an echelon indicator in the top-centre of the sprite.</summary>
        private static void DrawEchelonMark(Texture2D tex, Echelon echelon)
        {
            // Dots (Squad, Section, Platoon/Company)
            int dots = EchelonDotCount(echelon);
            if (dots > 0)
            {
                int totalW = dots * DotSize + (dots - 1) * DotGap;
                int startX = (TexSize - totalW) / 2;
                int startY = TexSize - Border - DotSize - 3;

                for (int d = 0; d < dots; d++)
                {
                    int ox = startX + d * (DotSize + DotGap);
                    FillRect(tex, ox, startY, DotSize, DotSize, Color.black);
                }
                return;
            }

            // Roman-numeral / X marks for higher echelons
            int bars = EchelonBarCount(echelon);
            if (bars > 0)
            {
                int barW = 6, barH = 16, gap = 5;
                int totalW = bars * barW + (bars - 1) * gap;
                int startX = (TexSize - totalW) / 2;
                int startY = TexSize - Border - barH - 3;

                for (int b = 0; b < bars; b++)
                {
                    int ox = startX + b * (barW + gap);
                    FillRect(tex, ox, startY, barW, barH, Color.black);
                }
                return;
            }

            // X marks for Brigade+
            int xMarks = EchelonXCount(echelon);
            if (xMarks > 0)
            {
                int xSize = 10, xGap = 3;
                int totalW = xMarks * xSize + (xMarks - 1) * xGap;
                int startX = (TexSize - totalW) / 2;
                int startY = TexSize - Border - xSize - 3;

                for (int x = 0; x < xMarks; x++)
                {
                    int ox = startX + x * (xSize + xGap);
                    // Draw X using two diagonal lines
                    for (int i = 0; i < xSize; i++)
                    {
                        SetIfInside(tex, ox + i,           startY + i,           Color.black);
                        SetIfInside(tex, ox + xSize - 1 - i, startY + i,         Color.black);
                        // Thicker lines
                        SetIfInside(tex, ox + i + 1,       startY + i,           Color.black);
                        SetIfInside(tex, ox + xSize - i,   startY + i,           Color.black);
                    }
                }
            }
        }

        // -------------------------------------------------------------------------
        // Echelon mark tables
        // -------------------------------------------------------------------------

        private static int EchelonDotCount(Echelon e) => e switch
        {
            Echelon.Team    => 0,   // circle would need different drawing
            Echelon.Squad   => 1,
            Echelon.Section => 2,
            Echelon.Platoon => 3,
            Echelon.Company => 3,
            _ => 0
        };

        private static int EchelonBarCount(Echelon e) => e switch
        {
            Echelon.Battalion => 1,
            Echelon.Regiment  => 2,
            _ => 0
        };

        private static int EchelonXCount(Echelon e) => e switch
        {
            Echelon.Brigade   => 1,
            Echelon.Division  => 2,
            Echelon.Corps     => 3,
            Echelon.Army      => 4,
            Echelon.ArmyGroup => 5,
            Echelon.Theater   => 6,
            _ => 0
        };

        // -------------------------------------------------------------------------
        // Low-level helpers
        // -------------------------------------------------------------------------

        private static void FillRect(Texture2D tex, int x, int y, int w, int h, Color c)
        {
            for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
                SetIfInside(tex, x + dx, y + dy, c);
        }

        private static void SetIfInside(Texture2D tex, int x, int y, Color c)
        {
            if (x >= 0 && x < TexSize && y >= 0 && y < TexSize)
                tex.SetPixel(x, y, c);
        }
    }
}
