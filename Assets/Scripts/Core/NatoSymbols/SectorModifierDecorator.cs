// SectorModifierDecorator.cs
// Table 3-1 Step 3 — Sector 1 (top) and Sector 2 (bottom) modifiers in bounding octagon.
// Max one modifier per sector; unknown codes are no-ops.

using System.Collections.Generic;
using UnityEngine;

namespace Strategos.NatoSymbols
{
    public sealed class SectorModifierDecorator : NatoSymbolDecorator
    {
        // Common Land Unit sector modifiers (subset for procedural slice).
        // Codes are illustrative stubs keyed by SIDC digit pairs; expand with Annex tables later.
        public const int ModAirborne = 13; // sector 2 common airborne
        public const int ModWheeled  = 21;
        public const int ModMountain = 25;
        public const int ModAmphibious = 31;
        public const int ModAirAssault = 14;

        public SectorModifierDecorator(INatoSymbol inner) : base(inner) { }

        protected override void Contribute(List<SymbolLayerDraw> layers, ref SymbolTextAmplifiers text)
        {
            var code = Code;
            if (code.Modifier1 != 0)
            {
                var d1 = ResolveModifier(code.Modifier1, sectorTop: true);
                if (d1 != null)
                {
                    layers.Add(SymbolLayerDraw.FromProcedural(
                        SymbolLayer.Modifier, $"Mod1_{code.Modifier1:D2}", d1, sortOrder: 2));
                }
            }

            if (code.Modifier2 != 0)
            {
                var d2 = ResolveModifier(code.Modifier2, sectorTop: false);
                if (d2 != null)
                {
                    layers.Add(SymbolLayerDraw.FromProcedural(
                        SymbolLayer.Modifier, $"Mod2_{code.Modifier2:D2}", d2, sortOrder: 2));
                }
            }
        }

        private static ProceduralDraw ResolveModifier(int modCode, bool sectorTop)
        {
            Color32 black = new Color32(0, 0, 0, 255);
            int cyConst = sectorTop ? SymbolLayout.Sector1CY : SymbolLayout.Sector2CY;

            switch (modCode)
            {
                case ModAirborne:
                case ModAirAssault:
                    return (buf, sz, sc) => DrawChevron(buf, sz, sc, cyConst, black, up: true);

                case ModMountain:
                    return (buf, sz, sc) => DrawMountain(buf, sz, sc, cyConst, black);

                case ModWheeled:
                    return (buf, sz, sc) => DrawWheeled(buf, sz, sc, cyConst, black);

                case ModAmphibious:
                    return (buf, sz, sc) => DrawWaves(buf, sz, sc, cyConst, black);

                default:
                    return null;
            }
        }

        private static void DrawChevron(Color32[] buf, int sz, float sc, int cyConst, Color32 col, bool up)
        {
            int cx = SymbolLayout.Scale(SymbolLayout.FrameCX, sc);
            int cy = SymbolLayout.Scale(cyConst, sc);
            int w  = SymbolLayout.Scale(22, sc);
            int h  = SymbolLayout.Scale(12, sc);
            int th = Mathf.Max(2, SymbolLayout.Scale(3, sc));
            int tipY = up ? cy + h / 2 : cy - h / 2;
            int baseY = up ? cy - h / 2 : cy + h / 2;
            ProceduralDrawUtil.DrawLine(buf, sz, cx - w, baseY, cx, tipY, col, th);
            ProceduralDrawUtil.DrawLine(buf, sz, cx + w, baseY, cx, tipY, col, th);
        }

        private static void DrawMountain(Color32[] buf, int sz, float sc, int cyConst, Color32 col)
        {
            int cx = SymbolLayout.Scale(SymbolLayout.FrameCX, sc);
            int cy = SymbolLayout.Scale(cyConst, sc);
            int w  = SymbolLayout.Scale(18, sc);
            int h  = SymbolLayout.Scale(14, sc);
            int th = Mathf.Max(2, SymbolLayout.Scale(3, sc));
            ProceduralDrawUtil.DrawLine(buf, sz, cx - w, cy - h / 2, cx, cy + h / 2, col, th);
            ProceduralDrawUtil.DrawLine(buf, sz, cx, cy + h / 2, cx + w, cy - h / 2, col, th);
        }

        private static void DrawWheeled(Color32[] buf, int sz, float sc, int cyConst, Color32 col)
        {
            int cx = SymbolLayout.Scale(SymbolLayout.FrameCX, sc);
            int cy = SymbolLayout.Scale(cyConst, sc);
            int r  = SymbolLayout.Scale(8, sc);
            int sp = SymbolLayout.Scale(20, sc);
            int th = Mathf.Max(1, SymbolLayout.Scale(2, sc));
            ProceduralDrawUtil.DrawCircleOutline(buf, sz, cx - sp / 2, cy, r, col, th);
            ProceduralDrawUtil.DrawCircleOutline(buf, sz, cx + sp / 2, cy, r, col, th);
        }

        private static void DrawWaves(Color32[] buf, int sz, float sc, int cyConst, Color32 col)
        {
            int cx = SymbolLayout.Scale(SymbolLayout.FrameCX, sc);
            int cy = SymbolLayout.Scale(cyConst, sc);
            int w  = SymbolLayout.Scale(28, sc);
            int th = Mathf.Max(2, SymbolLayout.Scale(3, sc));
            ProceduralDrawUtil.DrawLine(buf, sz, cx - w, cy, cx - w / 3, cy + SymbolLayout.Scale(4, sc), col, th);
            ProceduralDrawUtil.DrawLine(buf, sz, cx - w / 3, cy + SymbolLayout.Scale(4, sc), cx + w / 3, cy - SymbolLayout.Scale(4, sc), col, th);
            ProceduralDrawUtil.DrawLine(buf, sz, cx + w / 3, cy - SymbolLayout.Scale(4, sc), cx + w, cy, col, th);
        }
    }
}
