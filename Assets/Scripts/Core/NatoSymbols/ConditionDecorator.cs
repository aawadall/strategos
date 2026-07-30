// ConditionDecorator.cs
// Two bars below the frame:
//
//   Operational condition — APP-6D status digit 7. The standard permits either an
//   oblique-slash form or a bar form; the bar is used here because the destroyed
//   slash is an X, which collides with the infantry icon (also an X).
//
//   Combat power — a Strategos game amplifier with no APP-6D equivalent, drawn
//   from the strength percentage.
//
// Both sit below HqLineY so the headquarters staff line never crosses them.

using System.Collections.Generic;
using UnityEngine;

namespace Strategos.NatoSymbols
{
    public sealed class ConditionDecorator : NatoSymbolDecorator
    {
        public static readonly Color32 FullyCapable  = new Color32( 46, 160,  67, 255); // green
        public static readonly Color32 Damaged       = new Color32(214, 158,  46, 255); // amber
        public static readonly Color32 Destroyed     = new Color32(198,  52,  52, 255); // red
        public static readonly Color32 FullCapacity  = new Color32( 47, 116, 200, 255); // blue

        private static readonly Color32 Ink   = new Color32(0, 0, 0, 255);
        private static readonly Color32 Empty = new Color32(230, 230, 230, 255);

        public ConditionDecorator(INatoSymbol inner) : base(inner) { }

        protected override void Contribute(List<SymbolLayerDraw> layers, ref SymbolTextAmplifiers text)
        {
            var code = Code;

            if (TryConditionColour(code.Status, out Color32 condition))
            {
                layers.Add(SymbolLayerDraw.FromProcedural(
                    SymbolLayer.Amplifier, $"Condition_{code.Status}",
                    (buf, sz, sc) => DrawBar(buf, sz, sc, SymbolLayout.ConditionBarY, 1f, condition),
                    sortOrder: 3));
            }

            if (TryStrengthFraction(text.StrengthLabel, out float fraction))
            {
                layers.Add(SymbolLayerDraw.FromProcedural(
                    SymbolLayer.Amplifier, "CombatPower",
                    (buf, sz, sc) => DrawBar(buf, sz, sc, SymbolLayout.StrengthBarY, fraction, Ink),
                    sortOrder: 3));
            }
        }

        /// <summary>
        /// Present and AnticipatedPlanned draw no bar — the latter already shows as
        /// a dashed frame via ProceduralSymbolFactory.ResolveLineStyle.
        /// </summary>
        private static bool TryConditionColour(UnitStatus status, out Color32 colour)
        {
            switch (status)
            {
                case UnitStatus.PresentFullyCapable:   colour = FullyCapable;  return true;
                case UnitStatus.PresentDamaged:        colour = Damaged;       return true;
                case UnitStatus.PresentDestroyed:      colour = Destroyed;     return true;
                case UnitStatus.PresentFullToCapacity: colour = FullCapacity;  return true;
                default:                               colour = default;       return false;
            }
        }

        /// <summary>Full strength draws nothing; the bar means "depleted".</summary>
        private static bool TryStrengthFraction(string label, out float fraction)
        {
            fraction = 1f;
            if (string.IsNullOrEmpty(label)) return false;
            if (!int.TryParse(label, out int pct)) return false;

            pct = Mathf.Clamp(pct, 0, 100);
            if (pct >= 100) return false;

            fraction = pct / 100f;
            return true;
        }

        /// <summary>
        /// Bar spanning the frame width. A fill below 1 draws the remainder in a
        /// light tone so the bar reads as a gauge rather than a shorter bar.
        /// </summary>
        private static void DrawBar(Color32[] buf, int sz, float sc, int yConst,
            float fill, Color32 colour)
        {
            int l = SymbolLayout.Scale(SymbolLayout.FrameLeft, sc);
            int r = SymbolLayout.Scale(SymbolLayout.FrameRight, sc);
            int b = SymbolLayout.Scale(yConst, sc);
            int t = b + Mathf.Max(3, SymbolLayout.Scale(SymbolLayout.BarHeight, sc));

            fill = Mathf.Clamp01(fill);
            int split = l + Mathf.RoundToInt((r - l) * fill);

            if (split > l)
                ProceduralDrawUtil.FillRect(buf, sz, l, b, split, t, colour, colour, 0);
            if (split < r)
                ProceduralDrawUtil.FillRect(buf, sz, split, b, r, t, Empty, Empty, 0);

            // Thin outline keeps the bar legible on a pale frame fill.
            int th = Mathf.Max(1, SymbolLayout.Scale(2, sc));
            ProceduralDrawUtil.DrawLine(buf, sz, l, b, r, b, Ink, th);
            ProceduralDrawUtil.DrawLine(buf, sz, l, t, r, t, Ink, th);
            ProceduralDrawUtil.DrawLine(buf, sz, l, b, l, t, Ink, th);
            ProceduralDrawUtil.DrawLine(buf, sz, r, b, r, t, Ink, th);
        }
    }
}
