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

            // Glyphs live in ProceduralDrawUtil so IconDecorator's entity-type
            // variants draw the identical marks.
            switch (modCode)
            {
                case ModAirborne:
                case ModAirAssault:
                    return (buf, sz, sc) => ProceduralDrawUtil.DrawChevron(
                        buf, sz, Cx(sc), Cy(cyConst, sc), sc, black, up: true);

                case ModMountain:
                    return (buf, sz, sc) => ProceduralDrawUtil.DrawMountain(
                        buf, sz, Cx(sc), Cy(cyConst, sc), sc, black);

                case ModWheeled:
                    return (buf, sz, sc) => ProceduralDrawUtil.DrawWheeled(
                        buf, sz, Cx(sc), Cy(cyConst, sc), sc, black);

                case ModAmphibious:
                    return (buf, sz, sc) => ProceduralDrawUtil.DrawWaves(
                        buf, sz, Cx(sc), Cy(cyConst, sc), sc, black);

                default:
                    return null;
            }
        }

        private static int Cx(float sc) => SymbolLayout.Scale(SymbolLayout.FrameCX, sc);
        private static int Cy(int cyConst, float sc) => SymbolLayout.Scale(cyConst, sc);
    }
}
