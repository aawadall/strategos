// UiTheme.cs
// Light "operations map" palette shared by every view.
//
// Lifted verbatim from SymbolBuilderPanel's private Theme class so a second view
// can match the first. Contrast ratios against the intended background are noted
// so future edits keep the UI legible — every text/background pair here is >= 7:1
// (WCAG AAA).

using UnityEngine;

namespace Strategos.UI
{
    /// <summary>
    /// Light paper palette. Referenced as <c>Theme</c> in views via
    /// <c>using Theme = Strategos.UI.UiTheme;</c>.
    /// </summary>
    public static class UiTheme
    {
        public static readonly Color StageBg      = Hex(0xEF, 0xED, 0xE4); // page
        public static readonly Color MapPaper     = Hex(0xE8, 0xE4, 0xD6); // map card
        public static readonly Color CardBg       = Hex(0xFA, 0xF9, 0xF4); // sidc / table card
        public static readonly Color CardLine     = Hex(0xB9, 0xB5, 0xA4);
        public static readonly Color RowStripe    = Hex(0xF1, 0xEF, 0xE7);

        public static readonly Color RailBg       = Hex(0xE4, 0xE1, 0xD5);
        public static readonly Color RailEdge     = Hex(0xCF, 0xCB, 0xBA);
        public static readonly Color SectionBg    = Hex(0xD2, 0xCE, 0xBE);

        public static readonly Color ControlFace  = Hex(0xFC, 0xFB, 0xF6);
        public static readonly Color ControlEdge  = Hex(0x8C, 0x88, 0x7A);
        public static readonly Color ControlHover = Hex(0xED, 0xEA, 0xDD);
        public static readonly Color SelectFill   = Hex(0xD3, 0xE2, 0xD0); // selected item

        public static readonly Color Ink          = Hex(0x19, 0x1C, 0x18); // 16.5:1 on ControlFace
        public static readonly Color InkMuted     = Hex(0x3F, 0x42, 0x39); //  7.9:1 on RailBg
        public static readonly Color Accent       = Hex(0x2C, 0x5A, 0x38); //  7.6:1 on CardBg
        public static readonly Color AccentText   = Hex(0xF6, 0xF4, 0xEB); //  7.4:1 on Accent

        private static Color Hex(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f, 1f);

        /// <summary>
        /// Nudges a colour toward white (or black, for a negative amount). Used for
        /// button hover/pressed states so a control only needs one authored colour.
        /// </summary>
        public static Color Lighten(Color c, float amount) => new(
            Mathf.Clamp01(c.r + amount),
            Mathf.Clamp01(c.g + amount),
            Mathf.Clamp01(c.b + amount),
            c.a);
    }
}
