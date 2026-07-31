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

        private static readonly Color32 Ink = new Color32(0, 0, 0, 255);

        /// <summary>How far the unfilled remainder is washed out toward white.</summary>
        private const float PaleAmount = 0.78f;

        /// <summary>The same hue, washed out, for the unfilled part of a gauge.</summary>
        private static Color32 Pale(Color32 c) => new(
            (byte)(c.r + (255 - c.r) * PaleAmount),
            (byte)(c.g + (255 - c.g) * PaleAmount),
            (byte)(c.b + (255 - c.b) * PaleAmount),
            255);

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
                Color32 power = CombatPowerColour(fraction);
                layers.Add(SymbolLayerDraw.FromProcedural(
                    SymbolLayer.Amplifier, "CombatPower",
                    (buf, sz, sc) => DrawBar(buf, sz, sc, SymbolLayout.StrengthBarY, fraction, power),
                    sortOrder: 3));
            }
        }

        /// <summary>
        /// The combat-power bar's colour, from the same three-colour palette the APP-6D
        /// condition bar uses.
        /// </summary>
        /// <remarks>
        /// Reusing the condition palette rather than inventing a gradient is deliberate: the
        /// two bars sit one above the other, and a symbol whose two indicators disagree about
        /// what amber means is worse than one with no colour at all.
        ///
        /// Three bands, not a continuous ramp. The bar is a few pixels tall at map scale, where
        /// a smooth green-to-red gradient is unreadable — the eye can tell three colours apart
        /// there and cannot rank thirty. The exact figure is on the unit's details panel for
        /// anyone who needs it.
        ///
        /// **The frame fill is not touched and must not be.** Fill colour is affiliation in
        /// APP-6D — blue friend, red hostile — and it is the first thing anyone reads on a
        /// symbol. Tinting it by damage would make a badly-mauled friendly unit read as enemy.
        /// </remarks>
        public static Color32 CombatPowerColour(float fraction)
        {
            if (fraction >= 0.67f) return FullyCapable;
            if (fraction >= 0.34f) return Damaged;
            return Destroyed;
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
        /// pale tint of the same colour, so the bar reads as a gauge rather than a
        /// shorter bar.
        /// </summary>
        /// <remarks>
        /// The remainder is tinted rather than neutral grey because of the one case that
        /// matters most: at zero strength there is no filled portion at all, and a neutral
        /// remainder made a destroyed unit's bar look identical to an empty one — no colour,
        /// nothing to read. Tinting means a spent unit shows a pale red bar and a healthy one
        /// a pale green remainder behind a mostly-green fill, so the band is legible at every
        /// value including the ends.
        /// </remarks>
        private static void DrawBar(Color32[] buf, int sz, float sc, int yConst,
            float fill, Color32 colour)
        {
            int l = SymbolLayout.Scale(SymbolLayout.FrameLeft, sc);
            int r = SymbolLayout.Scale(SymbolLayout.FrameRight, sc);
            int b = SymbolLayout.Scale(yConst, sc);
            int t = b + Mathf.Max(3, SymbolLayout.Scale(SymbolLayout.BarHeight, sc));

            fill = Mathf.Clamp01(fill);
            int split = l + Mathf.RoundToInt((r - l) * fill);
            Color32 rest = Pale(colour);

            if (split > l)
                ProceduralDrawUtil.FillRect(buf, sz, l, b, split, t, colour, colour, 0);
            if (split < r)
                ProceduralDrawUtil.FillRect(buf, sz, split, b, r, t, rest, rest, 0);

            // Thin outline keeps the bar legible on a pale frame fill.
            int th = Mathf.Max(1, SymbolLayout.Scale(2, sc));
            ProceduralDrawUtil.DrawLine(buf, sz, l, b, r, b, Ink, th);
            ProceduralDrawUtil.DrawLine(buf, sz, l, t, r, t, Ink, th);
            ProceduralDrawUtil.DrawLine(buf, sz, l, b, l, t, Ink, th);
            ProceduralDrawUtil.DrawLine(buf, sz, r, b, r, t, Ink, th);
        }
    }
}
