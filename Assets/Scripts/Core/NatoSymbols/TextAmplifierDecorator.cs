// TextAmplifierDecorator.cs
// Table 3-2 text amplifiers drawn beside the frame:
//   Field T — unique designation
//   Field M — higher formation
//   Field F — reinforced / reduced
//
// These are baked as pixels rather than laid out with TextMeshPro so a composed
// symbol remains one self-contained Sprite, usable for map markers and headless
// bakes where no canvas exists.

using System.Collections.Generic;
using UnityEngine;

namespace Strategos.NatoSymbols
{
    public sealed class TextAmplifierDecorator : NatoSymbolDecorator
    {
        public TextAmplifierDecorator(INatoSymbol inner) : base(inner) { }

        protected override void Contribute(List<SymbolLayerDraw> layers, ref SymbolTextAmplifiers text)
        {
            // Read the merged amplifiers rather than recomputing from Code —
            // AmplifierDecorator has already populated them.
            var amp = text;
            Color32 ink = new Color32(0, 0, 0, 255);

            if (!string.IsNullOrEmpty(amp.Designation))
            {
                string s = amp.Designation;
                layers.Add(SymbolLayerDraw.FromProcedural(
                    SymbolLayer.Text, "Field_T",
                    (buf, sz, sc) => DrawRight(buf, sz, sc, SymbolLayout.FieldTY, s, ink),
                    sortOrder: 4));
            }

            if (!string.IsNullOrEmpty(amp.HigherFormation))
            {
                string s = amp.HigherFormation;
                layers.Add(SymbolLayerDraw.FromProcedural(
                    SymbolLayer.Text, "Field_M",
                    (buf, sz, sc) => DrawRight(buf, sz, sc, SymbolLayout.FieldMY, s, ink),
                    sortOrder: 4));
            }

            // Field F is the +/-/± marker only; the percentage is a game concept and
            // is drawn as a bar by ConditionDecorator instead.
            string f = StrengthMarker(amp.StrengthModifier);
            if (!string.IsNullOrEmpty(f))
            {
                layers.Add(SymbolLayerDraw.FromProcedural(
                    SymbolLayer.Text, "Field_F",
                    (buf, sz, sc) => DrawRight(buf, sz, sc, SymbolLayout.FieldFY, f, ink),
                    sortOrder: 4));
            }
        }

        private static string StrengthMarker(StrengthModifier m) => m switch
        {
            StrengthModifier.Reinforced        => "+",
            StrengthModifier.Reduced           => "-",
            StrengthModifier.ReinforcedReduced => "±",
            _                                  => null,
        };

        /// <summary>
        /// Draws right-aligned text in the amplifier column beside the frame, stepping
        /// the glyph scale down so a long designation shrinks rather than running back
        /// over the frame.
        /// </summary>
        private static void DrawRight(Color32[] buf, int sz, float sc, int yConst, string s, Color32 col)
        {
            int x = SymbolLayout.Scale(SymbolLayout.TextRightX, sc);
            int y = SymbolLayout.Scale(yConst, sc);
            int available = SymbolLayout.Scale(SymbolLayout.TextWidth, sc);

            int preferred = Mathf.Max(1, Mathf.RoundToInt(SymbolLayout.TextScale * sc));
            int scale = preferred;
            while (scale > 1 && ProceduralDrawUtil.MeasureText(s, scale) > available)
                scale--;

            ProceduralDrawUtil.DrawText(buf, sz, x, y, s, col, scale, TextAlign.Right);
        }
    }
}
