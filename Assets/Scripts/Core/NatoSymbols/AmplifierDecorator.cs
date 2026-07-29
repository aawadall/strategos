// AmplifierDecorator.cs
// Graphic amplifiers outside the frame: Field B echelon, Field D TF, Field S HQ, feint.
// Text amplifiers: Fields T, M, F.

using System.Collections.Generic;
using UnityEngine;

namespace Strategos.NatoSymbols
{
    public sealed class AmplifierDecorator : NatoSymbolDecorator
    {
        public AmplifierDecorator(INatoSymbol inner) : base(inner) { }

        protected override void Contribute(List<SymbolLayerDraw> layers, ref SymbolTextAmplifiers text)
        {
            var code = Code;
            Color32 black = new Color32(0, 0, 0, 255);

            // Text amplifiers from SIDCCode
            text = SymbolTextAmplifiers.FromCode(code);

            if (code.Echelon != Echelon.None)
            {
                var ech = code.Echelon;
                layers.Add(SymbolLayerDraw.FromProcedural(
                    SymbolLayer.Amplifier,
                    $"Echelon_{ech}",
                    (buf, sz, sc) => DrawEchelon(buf, sz, sc, ech, black),
                    sortOrder: 3));
            }

            if (code.IsHeadquarters)
            {
                layers.Add(SymbolLayerDraw.FromProcedural(
                    SymbolLayer.Amplifier,
                    "HQ_Staff",
                    (buf, sz, sc) => DrawHqStaff(buf, sz, sc, code.IdentityGroup, black),
                    sortOrder: 3));
            }

            if (code.IsTaskForce)
            {
                layers.Add(SymbolLayerDraw.FromProcedural(
                    SymbolLayer.Amplifier,
                    "TF_Bracket",
                    (buf, sz, sc) => DrawTaskForceBracket(buf, sz, sc, code.IdentityGroup, black),
                    sortOrder: 3));
            }

            if (code.IsFeintDummy)
            {
                layers.Add(SymbolLayerDraw.FromProcedural(
                    SymbolLayer.Amplifier,
                    "Feint",
                    (buf, sz, sc) => DrawFeintDashes(buf, sz, sc, code.IdentityGroup, black),
                    sortOrder: 3));
            }
        }

        // ─── Echelon (Field B) ────────────────────────────────────────────────

        private static void DrawEchelon(Color32[] buf, int sz, float sc, Echelon echelon, Color32 col)
        {
            int cy  = SymbolLayout.Scale(SymbolLayout.EchelonCY, sc);
            int cx  = SymbolLayout.Scale(SymbolLayout.FrameCX, sc);
            int dr  = Mathf.Max(3, SymbolLayout.Scale(8, sc));
            int spc = Mathf.Max(dr * 2 + 2, SymbolLayout.Scale(22, sc));
            int bw  = Mathf.Max(2, SymbolLayout.Scale(7, sc));
            int bh  = Mathf.Max(6, SymbolLayout.Scale(22, sc));
            int xr  = Mathf.Max(4, SymbolLayout.Scale(9, sc));
            int xth = Mathf.Max(1, Mathf.RoundToInt(2 * sc));

            switch (echelon)
            {
                case Echelon.Team:
                    ProceduralDrawUtil.DrawCircleOutline(buf, sz, cx, cy, dr + 3, col,
                        Mathf.Max(1, Mathf.RoundToInt(2 * sc)));
                    break;
                case Echelon.Squad:
                    ProceduralDrawUtil.FillCircle(buf, sz, cx, cy, dr, col);
                    break;
                case Echelon.Section:
                    ProceduralDrawUtil.FillCircle(buf, sz, cx - spc / 2, cy, dr, col);
                    ProceduralDrawUtil.FillCircle(buf, sz, cx + spc / 2, cy, dr, col);
                    break;
                case Echelon.Platoon:
                case Echelon.Company:
                    ProceduralDrawUtil.FillCircle(buf, sz, cx - spc, cy, dr, col);
                    ProceduralDrawUtil.FillCircle(buf, sz, cx, cy, dr, col);
                    ProceduralDrawUtil.FillCircle(buf, sz, cx + spc, cy, dr, col);
                    break;
                case Echelon.Battalion:
                    ProceduralDrawUtil.FillRect(buf, sz,
                        cx - bw / 2, cy - bh / 2, cx + bw / 2, cy + bh / 2, col, col, 0);
                    break;
                case Echelon.Regiment:
                    ProceduralDrawUtil.FillRect(buf, sz,
                        cx - spc / 2 - bw / 2, cy - bh / 2, cx - spc / 2 + bw / 2, cy + bh / 2, col, col, 0);
                    ProceduralDrawUtil.FillRect(buf, sz,
                        cx + spc / 2 - bw / 2, cy - bh / 2, cx + spc / 2 + bw / 2, cy + bh / 2, col, col, 0);
                    break;
                case Echelon.Brigade:
                    ProceduralDrawUtil.DrawX(buf, sz, cx, cy, xr, col, xth);
                    break;
                case Echelon.Division:
                    ProceduralDrawUtil.DrawXRow(buf, sz, cx, cy, xr, col, 2, spc, xth);
                    break;
                case Echelon.Corps:
                    ProceduralDrawUtil.DrawXRow(buf, sz, cx, cy, xr, col, 3, spc, xth);
                    break;
                case Echelon.Army:
                    ProceduralDrawUtil.DrawXRow(buf, sz, cx, cy, xr, col, 4, spc, xth);
                    break;
                case Echelon.ArmyGroup:
                    ProceduralDrawUtil.DrawXRow(buf, sz, cx, cy, xr, col, 5, spc, xth);
                    break;
                case Echelon.Theater:
                case Echelon.Command:
                    ProceduralDrawUtil.DrawXRow(buf, sz, cx, cy, xr, col, 6, spc, xth);
                    break;
            }
        }

        // ─── HQ staff indicator (Field S) ─────────────────────────────────────

        private static void DrawHqStaff(Color32[] buf, int sz, float sc, IdentityGroup group, Color32 col)
        {
            int cx = SymbolLayout.Scale(SymbolLayout.FrameCX, sc);
            int y0 = SymbolLayout.Scale(SymbolLayout.FrameBottom, sc);
            int y1 = SymbolLayout.Scale(SymbolLayout.HqLineY, sc);
            int th = Mathf.Max(2, SymbolLayout.Scale(4, sc));
            // Vertical staff line from bottom centre of frame downward.
            ProceduralDrawUtil.DrawLine(buf, sz, cx, y0, cx, y1, col, th);
        }

        // ─── Task force bracket (Field D) ─────────────────────────────────────

        private static void DrawTaskForceBracket(Color32[] buf, int sz, float sc, IdentityGroup group, Color32 col)
        {
            int l  = SymbolLayout.Scale(SymbolLayout.FrameLeft, sc);
            int r  = SymbolLayout.Scale(SymbolLayout.FrameRight, sc);
            int y  = SymbolLayout.Scale(SymbolLayout.TfBracketY, sc);
            int h  = SymbolLayout.Scale(14, sc);
            int th = Mathf.Max(2, SymbolLayout.Scale(3, sc));

            if (group == IdentityGroup.Neutral)
            {
                int cx = SymbolLayout.Scale(SymbolLayout.FrameCX, sc);
                int hh = SymbolLayout.Scale(SymbolLayout.FrameHalfH, sc);
                l = cx - hh;
                r = cx + hh;
            }

            ProceduralDrawUtil.DrawLine(buf, sz, l, y, r, y, col, th);
            ProceduralDrawUtil.DrawLine(buf, sz, l, y, l, y - h, col, th);
            ProceduralDrawUtil.DrawLine(buf, sz, r, y, r, y - h, col, th);
        }

        // ─── Feint / dummy dashes ─────────────────────────────────────────────

        private static void DrawFeintDashes(Color32[] buf, int sz, float sc, IdentityGroup group, Color32 col)
        {
            // Diagonal dashed overlay across the frame interior.
            int th = Mathf.Max(2, SymbolLayout.Scale(3, sc));
            int x0 = SymbolLayout.Scale(SymbolLayout.FrameLeft + 30, sc);
            int y0 = SymbolLayout.Scale(SymbolLayout.FrameBottom + 20, sc);
            int x1 = SymbolLayout.Scale(SymbolLayout.FrameRight - 30, sc);
            int y1 = SymbolLayout.Scale(SymbolLayout.FrameTop - 20, sc);

            // Draw as dashed segments.
            const int segs = 8;
            for (int i = 0; i < segs; i += 2)
            {
                float t0 = i / (float)segs;
                float t1 = (i + 1) / (float)segs;
                int ax = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t0));
                int ay = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t0));
                int bx = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t1));
                int by = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t1));
                ProceduralDrawUtil.DrawLine(buf, sz, ax, ay, bx, by, col, th);
            }
        }
    }
}
