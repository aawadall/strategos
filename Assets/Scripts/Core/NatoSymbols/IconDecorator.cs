// IconDecorator.cs
// Table 3-1 Step 2 — main / full-frame / full-octagon icons inside the bounding octagon.

using System.Collections.Generic;
using UnityEngine;

namespace Strategos.NatoSymbols
{
    public sealed class IconDecorator : NatoSymbolDecorator
    {
        // Entity-type variants (SIDC digits 13–14). Like the sector modifier codes
        // these are project stubs, not Annex A values — they exist so the builder's
        // Variant control maps onto a visible mark.
        public const int VarStandard   = 11;
        public const int VarMechanized = 12;
        public const int VarMotorized  = 13;
        public const int VarAirAssault = 14;
        public const int VarAmphibious = 15;
        public const int VarMountain   = 16;
        public const int VarArctic     = 17;
        public const int VarHeavy      = 18;
        public const int VarLight      = 19;

        public IconDecorator(INatoSymbol inner) : base(inner) { }

        protected override void Contribute(List<SymbolLayerDraw> layers, ref SymbolTextAmplifiers text)
        {
            var code = Code;
            if (code.SymbolSet != SymbolSet.LandUnit && code.SymbolSet != SymbolSet.LandCivilian)
                return;

            var draw = ResolveLandIcon(code);
            if (draw != null)
            {
                layers.Add(SymbolLayerDraw.FromProcedural(
                    SymbolLayer.Icon,
                    $"Icon_{code.EntityCode:D2}{code.EntityType:D2}",
                    draw,
                    sortOrder: 1));
            }

            var variant = ResolveVariant(code);
            if (variant != null)
            {
                layers.Add(SymbolLayerDraw.FromProcedural(
                    SymbolLayer.Icon,
                    $"Variant_{code.EntityType:D2}",
                    variant,
                    sortOrder: 1));
            }
        }

        /// <summary>
        /// Mobility mark for the entity type. Everything except mechanized occupies
        /// the lower sector, so it is suppressed when an explicit Sector 2 modifier
        /// is already there — otherwise the two marks overlap.
        /// </summary>
        private static ProceduralDraw ResolveVariant(SIDCCode code)
        {
            Color32 black = new Color32(0, 0, 0, 255);

            if (code.EntityType == VarMechanized)
                return (buf, sz, sc) => DrawMechanizedRing(buf, sz, sc, black);

            if (code.Modifier2 != 0) return null;

            switch (code.EntityType)
            {
                case VarMotorized:
                    return (buf, sz, sc) => ProceduralDrawUtil.DrawWheeled(
                        buf, sz, Cx(sc), Sector2(sc), sc, black);

                case VarAirAssault:
                    return (buf, sz, sc) => ProceduralDrawUtil.DrawChevron(
                        buf, sz, Cx(sc), Sector2(sc), sc, black, up: true);

                case VarAmphibious:
                    return (buf, sz, sc) => ProceduralDrawUtil.DrawWaves(
                        buf, sz, Cx(sc), Sector2(sc), sc, black);

                case VarMountain:
                    return (buf, sz, sc) => ProceduralDrawUtil.DrawMountain(
                        buf, sz, Cx(sc), Sector2(sc), sc, black);

                case VarArctic:
                    return (buf, sz, sc) => ProceduralDrawUtil.DrawArch(
                        buf, sz, Cx(sc), Sector2(sc), sc, black);

                // No conventional glyph for heavy / light — use the bitmap font.
                case VarHeavy:
                    return (buf, sz, sc) => DrawVariantLetter(buf, sz, sc, "H", black);
                case VarLight:
                    return (buf, sz, sc) => DrawVariantLetter(buf, sz, sc, "L", black);

                default:
                    return null; // VarStandard and unknown types draw nothing.
            }
        }

        private static int Cx(float sc) => SymbolLayout.Scale(SymbolLayout.FrameCX, sc);
        private static int Sector2(float sc) => SymbolLayout.Scale(SymbolLayout.Sector2CY, sc);

        private static void DrawMechanizedRing(Color32[] buf, int sz, float sc, Color32 col)
        {
            int cy = SymbolLayout.Scale(SymbolLayout.MainCY, sc);
            int rx = SymbolLayout.Scale(62, sc);
            int ry = SymbolLayout.Scale(36, sc);
            int th = Mathf.Max(2, SymbolLayout.Scale(3, sc));
            ProceduralDrawUtil.DrawEllipseOutline(buf, sz, Cx(sc), cy, rx, ry, col, th);
        }

        private static void DrawVariantLetter(Color32[] buf, int sz, float sc, string s, Color32 col)
        {
            int scale = Mathf.Max(1, Mathf.RoundToInt(SymbolLayout.TextScale * sc));
            int y = Sector2(sc) - (ProceduralDrawUtil.GlyphH * scale) / 2;
            ProceduralDrawUtil.DrawText(buf, sz, Cx(sc), y, s, col, scale, TextAlign.Center);
        }

        private static ProceduralDraw ResolveLandIcon(SIDCCode code)
        {
            Color32 black = new Color32(0, 0, 0, 255);
            int entity = code.EntityCode;
            IdentityGroup group = code.IdentityGroup;

            switch ((LandEntityCode)entity)
            {
                case LandEntityCode.Infantry:
                    // Full-frame diagonal slash — geometry adapted to frame shape.
                    return (buf, sz, sc) => DrawInfantry(buf, sz, sc, group, black);

                case LandEntityCode.Armor:
                    return (buf, sz, sc) => DrawArmor(buf, sz, sc, black);

                case LandEntityCode.Artillery:
                    return (buf, sz, sc) => DrawArtillery(buf, sz, sc, black);

                case LandEntityCode.Reconnaissance:
                    return (buf, sz, sc) => DrawRecon(buf, sz, sc, group, black);

                case LandEntityCode.CombatEngineering:
                    return (buf, sz, sc) => DrawEngineer(buf, sz, sc, black);

                case LandEntityCode.AirDefense:
                    return (buf, sz, sc) => DrawAirDefense(buf, sz, sc, black);

                case LandEntityCode.Aviation:
                    return (buf, sz, sc) => DrawAviation(buf, sz, sc, black);

                case LandEntityCode.Medical:
                    return (buf, sz, sc) => DrawMedical(buf, sz, sc, black);

                case LandEntityCode.Headquarters:
                    return (buf, sz, sc) => DrawHeadquartersIcon(buf, sz, sc, black);

                case LandEntityCode.SignalsCommunication:
                    return (buf, sz, sc) => DrawSignals(buf, sz, sc, black);

                case LandEntityCode.LogisticsSupport:
                    return (buf, sz, sc) => DrawLogistics(buf, sz, sc, black);

                default:
                    // Unknown entity → frame only (do not invent icons).
                    return null;
            }
        }

        // ─── Icon drawings ────────────────────────────────────────────────────

        private static void DrawInfantry(Color32[] buf, int sz, float sc, IdentityGroup group, Color32 col)
        {
            // Table A-8: infantry is a pair of crossed diagonals filling the frame.
            int th = Mathf.Max(2, Mathf.RoundToInt(4 * sc));
            GetFrameCorners(sc, group, out int x0, out int y0, out int x1, out int y1);
            int margin = SymbolLayout.Scale(16, sc);
            ProceduralDrawUtil.DrawLine(buf, sz,
                x0 + margin, y0 + margin,
                x1 - margin, y1 - margin,
                col, th);
            ProceduralDrawUtil.DrawLine(buf, sz,
                x0 + margin, y1 - margin,
                x1 - margin, y0 + margin,
                col, th);
        }

        private static void DrawRecon(Color32[] buf, int sz, float sc, IdentityGroup group, Color32 col)
        {
            // Cavalry: both diagonals (× inside frame).
            int th = Mathf.Max(2, Mathf.RoundToInt(4 * sc));
            GetFrameCorners(sc, group, out int x0, out int y0, out int x1, out int y1);
            int margin = SymbolLayout.Scale(16, sc);
            ProceduralDrawUtil.DrawLine(buf, sz, x0 + margin, y0 + margin, x1 - margin, y1 - margin, col, th);
            ProceduralDrawUtil.DrawLine(buf, sz, x0 + margin, y1 - margin, x1 - margin, y0 + margin, col, th);
        }

        private static void DrawArmor(Color32[] buf, int sz, float sc, Color32 col)
        {
            // Ellipse outline in main sector (tracked armour silhouette approx).
            // Border-only so the frame fill underneath is not erased.
            int cy = SymbolLayout.Scale(SymbolLayout.MainCY, sc);
            int rx = SymbolLayout.Scale(48, sc);
            int ry = SymbolLayout.Scale(28, sc);
            int bw = Mathf.Max(2, SymbolLayout.Scale(3, sc));
            ProceduralDrawUtil.DrawEllipseOutline(buf, sz, Cx(sc), cy, rx, ry, col, bw);
        }

        private static void DrawArtillery(Color32[] buf, int sz, float sc, Color32 col)
        {
            int cx = SymbolLayout.Scale(SymbolLayout.FrameCX, sc);
            int cy = SymbolLayout.Scale(SymbolLayout.MainCY, sc);
            int r  = SymbolLayout.Scale(14, sc);
            ProceduralDrawUtil.FillCircle(buf, sz, cx, cy, r, col);
        }

        private static void DrawEngineer(Color32[] buf, int sz, float sc, Color32 col)
        {
            // Simplified "E" using three horizontals + vertical.
            int cx = SymbolLayout.Scale(SymbolLayout.FrameCX, sc);
            int cy = SymbolLayout.Scale(SymbolLayout.MainCY, sc);
            int w  = SymbolLayout.Scale(28, sc);
            int h  = SymbolLayout.Scale(36, sc);
            int th = Mathf.Max(2, SymbolLayout.Scale(4, sc));
            ProceduralDrawUtil.DrawLine(buf, sz, cx - w, cy + h, cx - w, cy - h, col, th);
            ProceduralDrawUtil.DrawLine(buf, sz, cx - w, cy + h, cx + w, cy + h, col, th);
            ProceduralDrawUtil.DrawLine(buf, sz, cx - w, cy,     cx + w / 2, cy, col, th);
            ProceduralDrawUtil.DrawLine(buf, sz, cx - w, cy - h, cx + w, cy - h, col, th);
        }

        private static void DrawAirDefense(Color32[] buf, int sz, float sc, Color32 col)
        {
            // Arc / upside-down U above a baseline.
            int cx = SymbolLayout.Scale(SymbolLayout.FrameCX, sc);
            int cy = SymbolLayout.Scale(SymbolLayout.MainCY, sc);
            int r  = SymbolLayout.Scale(32, sc);
            int th = Mathf.Max(2, SymbolLayout.Scale(4, sc));
            ProceduralDrawUtil.DrawCircleOutline(buf, sz, cx, cy - r / 4, r, col, th);
            // Clear bottom half of circle to leave an arc (approximate by filling transparent — skip for simplicity)
            ProceduralDrawUtil.DrawLine(buf, sz, cx - r, cy - r / 2, cx + r, cy - r / 2, col, th);
        }

        private static void DrawAviation(Color32[] buf, int sz, float sc, Color32 col)
        {
            // Propeller: horizontal bar + vertical stroke.
            int cx = SymbolLayout.Scale(SymbolLayout.FrameCX, sc);
            int cy = SymbolLayout.Scale(SymbolLayout.MainCY, sc);
            int arm = SymbolLayout.Scale(36, sc);
            int th = Mathf.Max(2, SymbolLayout.Scale(4, sc));
            ProceduralDrawUtil.DrawLine(buf, sz, cx - arm, cy, cx + arm, cy, col, th);
            ProceduralDrawUtil.DrawLine(buf, sz, cx, cy - arm / 2, cx, cy + arm / 2, col, th);
            ProceduralDrawUtil.FillCircle(buf, sz, cx, cy, SymbolLayout.Scale(6, sc), col);
        }

        private static void DrawMedical(Color32[] buf, int sz, float sc, Color32 col)
        {
            // Full-frame cross (medical) — simplified plus.
            int cx = SymbolLayout.Scale(SymbolLayout.FrameCX, sc);
            int cy = SymbolLayout.Scale(SymbolLayout.MainCY, sc);
            int arm = SymbolLayout.Scale(40, sc);
            int th = Mathf.Max(3, SymbolLayout.Scale(8, sc));
            ProceduralDrawUtil.DrawLine(buf, sz, cx - arm, cy, cx + arm, cy, col, th);
            ProceduralDrawUtil.DrawLine(buf, sz, cx, cy - arm, cx, cy + arm, col, th);
        }

        private static void DrawHeadquartersIcon(Color32[] buf, int sz, float sc, Color32 col)
        {
            // Flag-like rectangle in main sector.
            int cx = SymbolLayout.Scale(SymbolLayout.FrameCX, sc);
            int cy = SymbolLayout.Scale(SymbolLayout.MainCY, sc);
            int w  = SymbolLayout.Scale(20, sc);
            int h  = SymbolLayout.Scale(28, sc);
            int th = Mathf.Max(2, SymbolLayout.Scale(3, sc));
            ProceduralDrawUtil.DrawLine(buf, sz, cx - w, cy - h, cx - w, cy + h, col, th);
            ProceduralDrawUtil.FillRect(buf, sz, cx - w, cy, cx + w, cy + h / 2, col, col, 0);
        }

        private static void DrawSignals(Color32[] buf, int sz, float sc, Color32 col)
        {
            // Lightning bolt approximation: zig-zag.
            int cx = SymbolLayout.Scale(SymbolLayout.FrameCX, sc);
            int cy = SymbolLayout.Scale(SymbolLayout.MainCY, sc);
            int s  = SymbolLayout.Scale(20, sc);
            int th = Mathf.Max(2, SymbolLayout.Scale(4, sc));
            ProceduralDrawUtil.DrawLine(buf, sz, cx + s / 2, cy + s, cx - s / 2, cy, col, th);
            ProceduralDrawUtil.DrawLine(buf, sz, cx - s / 2, cy, cx + s / 2, cy, col, th);
            ProceduralDrawUtil.DrawLine(buf, sz, cx + s / 2, cy, cx - s / 2, cy - s, col, th);
        }

        private static void DrawLogistics(Color32[] buf, int sz, float sc, Color32 col)
        {
            // Broken wheel / circle with gap.
            int cx = SymbolLayout.Scale(SymbolLayout.FrameCX, sc);
            int cy = SymbolLayout.Scale(SymbolLayout.MainCY, sc);
            int r  = SymbolLayout.Scale(28, sc);
            int th = Mathf.Max(2, SymbolLayout.Scale(4, sc));
            ProceduralDrawUtil.DrawCircleOutline(buf, sz, cx, cy, r, col, th);
        }

        /// <summary>
        /// Largest icon box that stays inside the frame for this identity group.
        /// Diamond and ellipse frames taper away from their bounding box, so the
        /// icon must be fitted to the inscribed rectangle — sizing it off the
        /// bounding box pushes the corners outside the frame.
        /// </summary>
        private static void GetFrameCorners(float sc, IdentityGroup group,
            out int x0, out int y0, out int x1, out int y1)
        {
            int cx = SymbolLayout.Scale(SymbolLayout.FrameCX, sc);
            int cy = SymbolLayout.Scale(SymbolLayout.FrameCY, sc);
            int hw = SymbolLayout.Scale(SymbolLayout.FrameHalfW, sc);
            int hh = SymbolLayout.Scale(SymbolLayout.FrameHalfH, sc);
            // fx/fy are the inscribed-rectangle fractions of the bounding box; the
            // margin is the extra breathing room. Tapering frames are already at
            // their geometric limit (a diamond requires fx + fy <= 1), so they take
            // only a token margin or the icon collapses.
            float fx = 1f, fy = 1f;
            int marginUnits = 12;
            switch (group)
            {
                case IdentityGroup.Hostile:
                    fx = fy = 0.50f;  // diamond: fx + fy <= 1
                    marginUnits = 3;
                    break;
                case IdentityGroup.Unknown:
                    fx = fy = 0.70f;  // ellipse: fx² + fy² <= 1, so ~1/√2
                    marginUnits = 5;
                    break;
                case IdentityGroup.Neutral:
                    hw = hh;          // neutral frame is a square
                    break;
            }

            int margin = SymbolLayout.Scale(marginUnits, sc);
            int ix = Mathf.Max(4, Mathf.RoundToInt(hw * fx) - margin);
            int iy = Mathf.Max(4, Mathf.RoundToInt(hh * fy) - margin);

            x0 = cx - ix; x1 = cx + ix;
            y0 = cy - iy; y1 = cy + iy;
        }
    }
}
