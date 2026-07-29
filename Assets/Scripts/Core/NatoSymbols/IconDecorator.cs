// IconDecorator.cs
// Table 3-1 Step 2 — main / full-frame / full-octagon icons inside the bounding octagon.

using System.Collections.Generic;
using UnityEngine;

namespace Strategos.NatoSymbols
{
    public sealed class IconDecorator : NatoSymbolDecorator
    {
        public IconDecorator(INatoSymbol inner) : base(inner) { }

        protected override void Contribute(List<SymbolLayerDraw> layers, ref SymbolTextAmplifiers text)
        {
            var code = Code;
            if (code.SymbolSet != SymbolSet.LandUnit && code.SymbolSet != SymbolSet.LandCivilian)
                return;

            var draw = ResolveLandIcon(code);
            if (draw == null) return;

            layers.Add(SymbolLayerDraw.FromProcedural(
                SymbolLayer.Icon,
                $"Icon_{code.EntityCode:D2}{code.EntityType:D2}",
                draw,
                sortOrder: 1));
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
            int th = Mathf.Max(2, Mathf.RoundToInt(4 * sc));
            GetFrameCorners(sc, group, out int x0, out int y0, out int x1, out int y1);
            int margin = SymbolLayout.Scale(16, sc);
            ProceduralDrawUtil.DrawLine(buf, sz,
                x0 + margin, y0 + margin,
                x1 - margin, y1 - margin,
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
            int cx = SymbolLayout.Scale(SymbolLayout.FrameCX, sc);
            int cy = SymbolLayout.Scale(SymbolLayout.MainCY, sc);
            int rx = SymbolLayout.Scale(48, sc);
            int ry = SymbolLayout.Scale(28, sc);
            int bw = Mathf.Max(2, SymbolLayout.Scale(3, sc));
            // Border-only: skip interior so we do not erase the frame fill.
            for (int y = cy - ry; y <= cy + ry; y++)
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                float dx = x - cx, dy = y - cy;
                float outer = dx * dx / ((float)rx * rx) + dy * dy / ((float)ry * ry);
                if (outer > 1.01f) continue;
                int irx = System.Math.Max(1, rx - bw), iry = System.Math.Max(1, ry - bw);
                float inner = dx * dx / ((float)irx * irx) + dy * dy / ((float)iry * iry);
                if (inner > 1.0f)
                    ProceduralDrawUtil.Set(buf, sz, x, y, col);
            }
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

        private static void GetFrameCorners(float sc, IdentityGroup group,
            out int x0, out int y0, out int x1, out int y1)
        {
            // Use rectangular inset for friend; tighter diamond inset for hostile.
            int margin = SymbolLayout.Scale(group == IdentityGroup.Hostile ? 28 : 20, sc);
            x0 = SymbolLayout.Scale(SymbolLayout.FrameLeft,   sc) + margin;
            y0 = SymbolLayout.Scale(SymbolLayout.FrameBottom, sc) + margin;
            x1 = SymbolLayout.Scale(SymbolLayout.FrameRight,  sc) - margin;
            y1 = SymbolLayout.Scale(SymbolLayout.FrameTop,    sc) - margin;

            if (group == IdentityGroup.Neutral)
            {
                int cx = SymbolLayout.Scale(SymbolLayout.FrameCX, sc);
                int hh = SymbolLayout.Scale(SymbolLayout.FrameHalfH, sc);
                x0 = cx - hh + margin / 2;
                x1 = cx + hh - margin / 2;
            }
        }
    }
}
