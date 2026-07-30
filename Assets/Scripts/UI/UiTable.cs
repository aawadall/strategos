// UiTable.cs
// Four-column read-only table used for code breakdowns.
//
// Extracted from SymbolBuilderPanel's breakdown card so the symbol library's
// click-to-inspect panel can show the same table rather than growing a second copy.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Strategos.UI
{
    /// <summary>
    /// The four cells of one table row, kept so callers can rewrite text without
    /// re-finding children every refresh.
    /// </summary>
    public struct UiTableRow
    {
        public TMP_Text Pos;
        public TMP_Text Code;
        public TMP_Text Field;
        public TMP_Text Meaning;
    }

    public static class UiTable
    {
        // Column widths in reference-resolution px. Meaning takes the slack.
        public const float ColPos   = 62f;
        public const float ColCode  = 62f;
        public const float ColField = 168f;

        public static RectTransform CreateRow(Transform parent, string name, Color bg,
            out UiTableRow row)
        {
            var rt = UiFactory.CreateRect(name, parent);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;
            rt.gameObject.AddComponent<Image>().color = bg;

            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(8, 8, 0, 0);
            h.spacing = 8;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            h.childAlignment = TextAnchor.MiddleLeft;

            row = new UiTableRow
            {
                Pos     = Cell(rt, "Pos", ColPos, 0f),
                Code    = Cell(rt, "Code", ColCode, 0f),
                Field   = Cell(rt, "Field", ColField, 0f),
                Meaning = Cell(rt, "Meaning", 0f, 1f),
            };
            return rt;
        }

        public static TMP_Text Cell(Transform parent, string name, float width, float flexible)
        {
            var tmp = UiFactory.CreateTmp(name, parent, string.Empty, 12, FontStyles.Normal);
            var le = tmp.GetComponent<LayoutElement>();
            le.preferredWidth = width;
            le.flexibleWidth = flexible;
            le.preferredHeight = 22;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        public static void SetRowText(UiTableRow r, string a, string b, string c, string d)
        {
            r.Pos.text = a;
            r.Code.text = b;
            r.Field.text = c;
            r.Meaning.text = d;
        }

        public static void ApplyRowStyle(UiTableRow r, Color color, FontStyles style, float size)
        {
            foreach (var t in new[] { r.Pos, r.Code, r.Field, r.Meaning })
            {
                t.color = color;
                t.fontStyle = style;
                t.fontSize = size;
                t.characterSpacing = 2f;
            }
        }
    }
}
