// UiSprites.cs
// Procedurally generated UI sprites.
//
// These exist because the bundled LiberationSans SDF atlas has no geometric-shape
// glyphs — U+25BE, U+25CB, U+2022 and U+2212 all render as tofu boxes — and because
// there is no gradient asset in the project. Drawing them into a texture is cheaper
// than authoring a shader and keeps the UI free of imported art.
//
// Both are static and lazily built, so they cost nothing until a view asks and are
// then shared across every view.

using UnityEngine;

namespace Strategos.UI
{
    public static class UiSprites
    {
        private static Sprite _arrow;
        private static Sprite _halo;
        private static Sprite _selection;

        /// <summary>Solid down-triangle, drawn white so Image.color can tint it.</summary>
        public static Sprite Arrow
        {
            get
            {
                if (_arrow != null) return _arrow;

                const int s = 32;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "DropdownArrow",
                };

                var px = new Color32[s * s];
                var clear = new Color32(255, 255, 255, 0);
                var solid = new Color32(255, 255, 255, 255);
                for (int i = 0; i < px.Length; i++) px[i] = clear;

                // Texture origin is bottom-left, so the apex sits at y = 0.
                for (int y = 0; y < s; y++)
                {
                    float t = y / (float)(s - 1);
                    int half = Mathf.RoundToInt(t * (s / 2f));
                    for (int x = s / 2 - half; x <= s / 2 + half; x++)
                        if (x >= 0 && x < s) px[y * s + x] = solid;
                }

                tex.SetPixels32(px);
                tex.Apply(false, false);
                _arrow = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
                return _arrow;
            }
        }

        /// <summary>
        /// Four corner brackets, drawn white so Image.color can tint them.
        ///
        /// Corners rather than a full box or a ring: a closed outline around a symbol reads
        /// as part of the symbol — APP-6D already uses enclosing shapes to mean something —
        /// whereas brackets read as chrome pointing *at* it. It is also the convention most
        /// military UI uses for a selected track.
        /// </summary>
        public static Sprite SelectionBrackets
        {
            get
            {
                if (_selection != null) return _selection;

                const int s = 64;
                const int thickness = 5;   // arm width
                const int arm = 20;        // how far each arm runs from its corner

                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "SelectionBrackets",
                };

                var px = new Color32[s * s];
                var clear = new Color32(255, 255, 255, 0);
                var solid = new Color32(255, 255, 255, 255);
                for (int i = 0; i < px.Length; i++) px[i] = clear;

                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    // Distance from whichever edge is nearer, per axis.
                    int dx = Mathf.Min(x, s - 1 - x);
                    int dy = Mathf.Min(y, s - 1 - y);

                    // A pixel belongs to a bracket when it is inside the thickness of one
                    // edge and within the arm length along the other.
                    bool horizontalArm = dy < thickness && dx < arm;
                    bool verticalArm = dx < thickness && dy < arm;

                    if (horizontalArm || verticalArm) px[y * s + x] = solid;
                }

                tex.SetPixels32(px);
                tex.Apply(false, false);
                _selection = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
                return _selection;
            }
        }

        /// <summary>
        /// Radial falloff used to quieten the sheet under a symbol.
        /// </summary>
        public static Sprite Halo
        {
            get
            {
                if (_halo != null) return _halo;

                const int s = 128;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "SymbolHalo",
                };

                var px = new Color32[s * s];
                float centre = (s - 1) * 0.5f;

                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x - centre) / centre;
                    float dy = (y - centre) / centre;
                    float d  = Mathf.Sqrt(dx * dx + dy * dy);

                    // Opaque out to 45% of the radius, then eased to nothing at the
                    // edge. Smoothstep rather than linear: a linear ramp leaves a
                    // visible ring where the gradient starts.
                    float a = 1f - Mathf.SmoothStep(0.45f, 1f, d);
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }

                tex.SetPixels32(px);
                tex.Apply(false, false);
                _halo = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
                return _halo;
            }
        }
    }
}
