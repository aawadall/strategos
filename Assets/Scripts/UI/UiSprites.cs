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
        private static Texture2D _dash;

        /// <summary>Length in pixels of one dash-plus-gap of <see cref="DashTexture"/>.</summary>
        public const float DashPeriod = 16f;

        /// <summary>
        /// A repeating dash, for drawing a planned route as distinct from one under way.
        ///
        /// A texture with wrapMode Repeat rather than many small segments: a RawImage can then
        /// draw a dashed line of any length as one quad by scaling its uvRect, instead of the
        /// UI having to hold an object per dash.
        ///
        /// Dashed for planned and solid for executing follows the convention the symbol system
        /// already uses — APP-6D draws an anticipated frame dashed and a present one solid — so
        /// the same distinction means the same thing on a symbol and on its order.
        /// </summary>
        public static Texture2D DashTexture
        {
            get
            {
                if (_dash != null) return _dash;

                const int w = 16;
                _dash = new Texture2D(w, 1, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,   // crisp edges; a blurred dash reads as dirt
                    wrapMode = TextureWrapMode.Repeat,
                    name = "OrderDash",
                };

                var px = new Color32[w];
                for (int x = 0; x < w; x++)
                    px[x] = x < w * 0.6f
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);

                _dash.SetPixels32(px);
                _dash.Apply(false, false);
                return _dash;
            }
        }

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

        /// <summary>
        /// GLSL-style smoothstep: 0 below <paramref name="edge0"/>, 1 above
        /// <paramref name="edge1"/>, smoothly interpolated between.
        /// </summary>
        /// <remarks>
        /// **`Mathf.SmoothStep` is not this**, and the difference is silent. Unity's version
        /// takes <c>(from, to, t)</c> and returns a smoothed interpolation *between from and
        /// to* — so `Mathf.SmoothStep(0.77f, 0.83f, d)` returns about 0.8 for every d, never 0
        /// and never 1. Used as an edge function it yields a nearly constant value rather than
        /// a transition, which for an alpha mask means a shape that is drawn, positioned and
        /// sized correctly and is invisible.
        /// </remarks>
        private static float Edge(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / Mathf.Max(1e-6f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static Sprite _objectiveRing;

        /// <summary>
        /// A hollow ring marking ground worth taking, drawn white so Image.color can tint it.
        /// </summary>
        /// <remarks>
        /// Hollow because it lies over terrain the player still has to read; a filled disc the
        /// size of an objective hides the ground it is asking them to take.
        ///
        /// Drawn rather than typed: the bundled atlas has no geometric glyphs and would render
        /// `○` as a tofu box. Same reason the dropdown arrow and selection brackets are here.
        /// </remarks>
        public static Sprite ObjectiveRing
        {
            get
            {
                if (_objectiveRing != null) return _objectiveRing;

                const int s = 256;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "ObjectiveRing",
                };

                var px = new Color32[s * s];
                float centre = (s - 1) * 0.5f;

                // Feathered both sides so the ring does not alias into a dotted circle when
                // the card is zoomed out.
                // Band as a fraction of the radius. Generous on purpose: a hairline ring is
                // invisible at map scale, and this is a boundary the player has to find at a
                // glance rather than a precise measurement.
                const float inner = 0.88f;
                const float outer = 0.97f;
                const float feather = 0.03f;

                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x - centre) / centre;
                    float dy = (y - centre) / centre;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    float a = Edge(inner - feather, inner + feather, d) *
                              (1f - Edge(outer - feather, outer + feather, d));

                    px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }

                tex.SetPixels32(px);
                tex.Apply(false, false);
                _objectiveRing = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
                return _objectiveRing;
            }
        }
    }
}
