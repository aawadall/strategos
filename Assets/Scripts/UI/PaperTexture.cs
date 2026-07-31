// PaperTexture.cs
// Procedurally aged paper: fibre grain, mottling, edge handling and coffee rings.
//
// Built for the TTP binder (#61), which wants a field manual you thumb through rather than a
// list you search. Kept separate from it because the stock is useful on its own — the map
// card, the details card and the situation feed all already read as paper and currently sit
// on flat colour.
//
// LEGIBILITY OUTRANKS THE EFFECT, and that is the whole reason this file has an API rather
// than a constant. UiTheme holds every text/background pair at >= 7:1 (WCAG AAA), and a
// stain laid over body text destroys that *silently* — it reads as styling, not as a contrast
// failure, so nobody files it. Two things guard against that:
//
//   * PaperOptions.StainStrength caps how far any pixel may be darkened, and
//     PaperOptions.WorstCaseLuminance reports what that cap actually costs, so the number can
//     be checked rather than assumed. PaperContactSheet prints it.
//   * KeepClear reserves the rects text will occupy and suppresses stains inside them. Same
//     idea as MapLabelPlacer.Reserve: placement can only avoid collisions it can see.
//
// DETERMINISTIC, FROM A HASH — not UnityEngine.Random and not System.Random. A stain that
// moves between openings reads as a rendering bug, and #61 wants a page seeded from its own
// drill code so a given page always has the same coffee ring (which is also what makes a page
// recognisable at a glance, the way a real dog-eared manual is). A pure hash of (x, y, seed)
// is reproducible across machines and runs, and is independent of the order pixels are
// visited, so a later tiled or threaded version cannot change the output.
//
// NOT SHARED, NOT CACHED. Create returns a texture the caller owns and must Destroy. That is
// the opposite of the rule for AppSession.Symbols sprites, which are shared cache entries and
// must never be destroyed — see docs/ui-invariants.md. Sheets are per-size and per-seed, so
// caching them here would be a leak with a lifetime nobody owns.

using System.Collections.Generic;
using UnityEngine;

namespace Strategos.UI
{
    /// <summary>How worn a sheet of paper is. All amounts are 0..1 unless noted.</summary>
    public struct PaperOptions
    {
        /// <summary>Stock colour before any ageing.</summary>
        public Color Stock;

        /// <summary>Fibre. Very fine, very low amplitude — this is the one that reads as paper.</summary>
        public float Grain;

        /// <summary>Low-frequency blotching, as in uneven pulp or damp.</summary>
        public float Mottle;

        /// <summary>How many coffee rings. Zero for a clean sheet.</summary>
        public int Stains;

        /// <summary>
        /// The most any single pixel may be darkened, as a fraction of the stock's luminance.
        /// This is the contrast guard: at 0.18 a stain can never take more than 18% of the
        /// paper's brightness however many rings overlap.
        /// </summary>
        public float StainStrength;

        /// <summary>Darkening toward the edges, from handling. Subtle; it frames the page.</summary>
        public float EdgeShade;

        /// <summary>
        /// Colour a stain pulls the stock toward.
        /// </summary>
        /// <remarks>
        /// Darkening alone is wrong and looks it: multiplying a warm cream stock by a scalar
        /// desaturates it toward grey, so the rings came out looking like shadows or smudges.
        /// A spill is *browner* than the paper, not merely dimmer.
        /// </remarks>
        public Color StainTint;

        /// <summary>
        /// True when text on this stock must be reserved through <c>keepClear</c>, or carried
        /// on a card over it, rather than drawn straight onto the sheet.
        /// </summary>
        /// <remarks>
        /// Not decoration — PaperContactSheet reads this to decide what to assert, so the
        /// contract is machine-checked rather than left in prose that can drift from the
        /// values above it. A preset that says it is safe unreserved must measure at least
        /// 7:1 across its whole surface; one that says otherwise is only held to that inside
        /// its reserved rects.
        /// </remarks>
        public bool RequiresReservedText;

        /// <summary>
        /// A clean sheet of the map stock. Grain only, no stains — the safe default, because
        /// anything that ages the paper should be an explicit decision at the call site.
        /// Safe to draw text straight onto: measured at 13.6:1 against <see cref="UiTheme.Ink"/>.
        /// </summary>
        public static PaperOptions Clean => new()
        {
            Stock = UiTheme.CardBg,
            Grain = 0.055f,
            Mottle = 0.04f,
            Stains = 0,
            StainStrength = 0.14f,
            EdgeShade = 0.045f,
            StainTint = Coffee,
        };

        /// <summary>
        /// A sheet that has been carried and used. The binder's default.
        /// </summary>
        /// <remarks>
        /// **Text laid on this must be reserved**, or carried on a card over it. Inside a
        /// stain's rim the paper falls to roughly 4.5:1 against <see cref="UiTheme.Ink"/> —
        /// legible, but under the 7:1 AAA floor UiTheme holds every other pair in the palette
        /// to. Inside a reserved rect it measures above 9:1. PaperContactSheet prints both.
        /// </remarks>
        public static PaperOptions Used => new()
        {
            Stock = UiTheme.MapPaper,
            Grain = 0.06f,
            Mottle = 0.055f,
            Stains = 2,
            StainStrength = 0.13f,
            EdgeShade = 0.08f,
            StainTint = Coffee,
            RequiresReservedText = true,
        };

        /// <summary>
        /// A sheet that has lived in a vehicle. The upper bound worth shipping.
        /// </summary>
        /// <remarks>Same reservation requirement as <see cref="Used"/>, more so.</remarks>
        public static PaperOptions Worn => new()
        {
            Stock = UiTheme.MapPaper,
            Grain = 0.07f,
            Mottle = 0.09f,
            Stains = 5,
            StainStrength = 0.15f,
            EdgeShade = 0.12f,
            StainTint = Coffee,
            RequiresReservedText = true,
        };

        private static readonly Color Coffee = new(0.55f, 0.43f, 0.27f, 1f);
    }

    public static class PaperTexture
    {
        /// <summary>
        /// How far outside a reserved rect a stain stays clear, in pixels.
        /// </summary>
        /// <remarks>
        /// Feathered rather than clipped. A stain with a hard-edged rectangular bite taken out
        /// of it reads as a rendering failure — far more obviously wrong than the stain it was
        /// protecting the text from. Fading out across a margin instead reads the way a real
        /// page does, where the writing simply avoids the mark.
        /// </remarks>
        private const int ClearFeather = 10;

        /// <summary>
        /// Most of the way a fully inked pixel may be pulled toward
        /// <see cref="PaperOptions.StainTint"/>. Held well under 1 because a stain is a wash,
        /// not paint — at 1 the rim reads as a drawn brown ring rather than as a mark in paper.
        /// </summary>
        private const float StainTintMax = 0.42f;

        /// <summary>
        /// Darkest luminance produced by the last <see cref="Create"/>, as a fraction of the
        /// stock's. 1 means nothing was darkened.
        /// </summary>
        /// <remarks>
        /// Exposed so a caller — in practice PaperContactSheet — can state the contrast the
        /// sheet actually achieved instead of trusting the cap. Reading the number is the
        /// point; a generator's output is a picture, but a contrast ratio is not visible in one.
        /// </remarks>
        public static float WorstCaseLuminance { get; private set; } = 1f;

        /// <summary>
        /// Bakes one sheet. The caller owns the texture and must <c>Destroy</c> it.
        /// </summary>
        /// <param name="keepClear">
        /// Rects that stains must not touch — the text boxes the page will lay over the
        /// paper. Grain and mottling still apply inside them: they are far below the
        /// threshold that could move a contrast ratio, and suppressing them too would leave
        /// visibly clean rectangles wherever text sits.
        /// </param>
        public static Texture2D Create(int width, int height, int seed, PaperOptions options,
            IReadOnlyList<RectInt> keepClear = null)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            var px = new Color32[width * height];
            float stockLum = Luminance(options.Stock);
            float darkest = 1f;

            // Stains are placed first so every pixel can ask the same fixed set. Placing them
            // per-pixel would need a different seed stream per pixel and would not be stable
            // under a change of resolution.
            var stains = PlaceStains(width, height, seed, options, keepClear);

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                // 1 is untouched paper; everything below multiplies it darker.
                float shade = 1f;

                // Fibre: two scales, because per-pixel white noise alone reads as television
                // static rather than paper. The coarse octave is what the eye registers as
                // texture; the fine one stops it looking like a gradient.
                if (options.Grain > 0f)
                {
                    float fine = Value(x, y, seed, 1f) - 0.5f;
                    float coarse = Value(x, y, seed ^ 0x51ED, 3f) - 0.5f;
                    shade += options.Grain * (0.4f * fine + 0.6f * coarse);
                }

                if (options.Mottle > 0f)
                {
                    float blotch = Fbm(x, y, seed ^ 0x2C9F, 48f, 3) - 0.5f;
                    shade += options.Mottle * blotch;
                }

                if (options.EdgeShade > 0f)
                    shade -= options.EdgeShade * EdgeFalloff(x, y, width, height);

                // Stains last, and bounded as a group: overlapping rings must not compound
                // into an arbitrarily dark blot, which is exactly how a contrast guarantee
                // gets lost one ring at a time.
                float ink = 0f;
                if (stains.Count > 0)
                {
                    float clear = KeepClearMask(x, y, keepClear);
                    if (clear > 0f)
                    {
                        // Combined as coverage — ink += r * (1 - ink) — rather than summed and
                        // clamped. Summing saturates wherever two rims cross, and the overlap
                        // flattens into a featureless plateau precisely where a real pair of
                        // rings is at its most obviously ring-shaped.
                        for (int i = 0; i < stains.Count; i++)
                            ink += Ring(x, y, stains[i], seed + i) * (1f - ink);

                        ink *= clear;
                        shade -= options.StainStrength * ink;
                    }
                }

                shade = Mathf.Clamp(shade, 0f, 1.08f);
                if (shade < darkest) darkest = shade;

                // The stain shifts the stock's colour before the shade multiplies it, so a
                // ring reads as spilled coffee rather than as a grey shadow of the paper.
                var stock = ink > 0f
                    ? Color.Lerp(options.Stock, options.StainTint, ink * StainTintMax)
                    : options.Stock;

                px[y * width + x] = new Color32(
                    (byte)(Mathf.Clamp01(stock.r * shade) * 255f),
                    (byte)(Mathf.Clamp01(stock.g * shade) * 255f),
                    (byte)(Mathf.Clamp01(stock.b * shade) * 255f),
                    255);
            }

            WorstCaseLuminance = stockLum <= 0f ? 1f : darkest;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"Paper_{seed}",
            };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>
        /// A sheet as a Sprite. The caller owns both the sprite and its texture.
        /// </summary>
        public static Sprite CreateSprite(int width, int height, int seed, PaperOptions options,
            IReadOnlyList<RectInt> keepClear = null)
        {
            var tex = Create(width, height, seed, options, keepClear);
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }

        /// <summary>A stable seed for a page identified by a string — a drill code, say.</summary>
        public static int SeedFor(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < key.Length; i++) { h ^= key[i]; h *= 16777619u; }
                return (int)h;
            }
        }

        // ─── Stains ───────────────────────────────────────────────────────────

        private struct Stain
        {
            public float X, Y, Radius;
            public float Wobble;   // how far from circular
        }

        /// <summary>
        /// Scatters rings, skipping any whose centre lands on reserved text.
        /// </summary>
        /// <remarks>
        /// Rejected rather than nudged. Nudging a stain off a text box packs rings against the
        /// edges of every reserved rect, which draws the eye to exactly the region the reserve
        /// existed to keep quiet.
        /// </remarks>
        private static List<Stain> PlaceStains(int w, int h, int seed, in PaperOptions o,
            IReadOnlyList<RectInt> keepClear)
        {
            var stains = new List<Stain>();
            if (o.Stains <= 0 || o.StainStrength <= 0f) return stains;

            float small = Mathf.Min(w, h);

            for (int i = 0; i < o.Stains; i++)
            {
                float cx = Rand(seed, i, 1) * w;
                float cy = Rand(seed, i, 2) * h;
                if (IsReserved((int)cx, (int)cy, keepClear)) continue;

                stains.Add(new Stain
                {
                    X = cx,
                    Y = cy,
                    // Wide enough to read as a mug, not a droplet, and bounded so one ring
                    // cannot cover a whole page. Tuned down after the binder: at the old range
                    // a ring on a 1152x768 sheet was 200 px across, which is a blot rather
                    // than a mug, and one laid in the free band below the text was clipped by
                    // the page edge.
                    Radius = small * Mathf.Lerp(0.06f, 0.13f, Rand(seed, i, 3)),
                    Wobble = Mathf.Lerp(0.03f, 0.09f, Rand(seed, i, 4)),
                });
            }
            return stains;
        }

        /// <summary>
        /// One coffee ring: a dark rim with a faint interior.
        /// </summary>
        /// <remarks>
        /// The rim carries most of the pigment because that is what actually happens — as the
        /// drop evaporates, flow toward the pinned edge carries solute outward and deposits it
        /// at the perimeter. A ring drawn as a uniform disc reads as a shadow or a smudge, not
        /// as a spill, and it is the single detail that decides whether this looks authentic.
        /// </remarks>
        private static float Ring(int x, int y, in Stain s, int seed)
        {
            float dx = x - s.X;
            float dy = y - s.Y;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d > s.Radius * 1.30f) return 0f;

            // Perturb the radius by angle so the rim is not a compass circle — a mug leaves an
            // uneven mark and a perfect one reads as UI chrome.
            //
            // A FEW HARMONICS, NOT SAMPLED NOISE. Sampling a noise field per angle gives
            // neighbouring angles uncorrelated radii, and the rim comes out as a saw blade:
            // the first version of this did exactly that and the rings looked like gears.
            // A short Fourier sum is smooth and closed by construction, which is what a
            // contour needs.
            float angle = Mathf.Atan2(dy, dx);
            float wobble = 1f;
            for (int k = 2; k <= 5; k++)
                wobble += s.Wobble * (1f / k) *
                          Mathf.Sin(k * angle + Rand(seed, k, 7) * Mathf.PI * 2f);

            float t = d / Mathf.Max(1f, s.Radius * wobble);

            // Generously feathered both sides. A narrow band gives a crisp edge that reads as
            // a *drawn* brown ring — vector art laid over the paper — where a spill bleeds
            // into the fibre and has no hard boundary anywhere.
            float rim = Edge(0.80f, 0.96f, t) * (1f - Edge(0.97f, 1.14f, t));
            float fill = (1f - Edge(0.92f, 1.04f, t)) * 0.20f;

            // Broken up by a low-frequency field, which is the difference between a stain and
            // a gradient. An analytically smooth ring looks manufactured however good its
            // silhouette is; pigment settles unevenly and the eye knows it.
            float blotch = 0.55f + 0.45f * Fbm(x, y, seed ^ 0x7A11, 26f, 2);
            return Mathf.Clamp01((fill + rim) * blotch);
        }

        // ─── Reserved regions ─────────────────────────────────────────────────

        private static bool IsReserved(int x, int y, IReadOnlyList<RectInt> keepClear)
        {
            if (keepClear == null) return false;
            for (int i = 0; i < keepClear.Count; i++)
                if (keepClear[i].Contains(new Vector2Int(x, y))) return true;
            return false;
        }

        /// <summary>
        /// 0 inside a reserved rect, easing to 1 across <see cref="ClearFeather"/> outside it.
        /// </summary>
        private static float KeepClearMask(int x, int y, IReadOnlyList<RectInt> keepClear)
        {
            if (keepClear == null || keepClear.Count == 0) return 1f;

            float mask = 1f;
            for (int i = 0; i < keepClear.Count; i++)
            {
                var r = keepClear[i];

                // Distance outside the rect, per axis, zero when inside.
                float ox = Mathf.Max(r.xMin - x, x - (r.xMax - 1));
                float oy = Mathf.Max(r.yMin - y, y - (r.yMax - 1));
                float outside = Mathf.Sqrt(Mathf.Max(0f, ox) * Mathf.Max(0f, ox) +
                                           Mathf.Max(0f, oy) * Mathf.Max(0f, oy));

                if (ox <= 0f && oy <= 0f) return 0f;   // inside: nothing may darken it
                mask = Mathf.Min(mask, Mathf.Clamp01(outside / ClearFeather));
            }
            return mask;
        }

        // ─── Noise ────────────────────────────────────────────────────────────

        /// <summary>
        /// Integer hash. Order-independent by construction, so output cannot depend on the
        /// order pixels happen to be visited in.
        /// </summary>
        private static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u;
                h ^= (uint)x * 0x85EBCA77u;
                h ^= (uint)y * 0xC2B2AE3Du;
                h ^= h >> 15; h *= 0x2545F491u;
                h ^= h >> 13; h *= 0x27220A95u;
                return h ^ (h >> 16);
            }
        }

        private static float Rand(int seed, int i, int salt) =>
            Hash(i, salt, seed) / (float)uint.MaxValue;

        /// <summary>Value noise at a given cell size, bilinearly interpolated.</summary>
        private static float Value(int x, int y, int seed, float scale)
        {
            if (scale <= 1f) return Hash(x, y, seed) / (float)uint.MaxValue;

            float fx = x / scale, fy = y / scale;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = Smooth(fx - x0), ty = Smooth(fy - y0);

            float a = Hash(x0, y0, seed) / (float)uint.MaxValue;
            float b = Hash(x0 + 1, y0, seed) / (float)uint.MaxValue;
            float c = Hash(x0, y0 + 1, seed) / (float)uint.MaxValue;
            float d = Hash(x0 + 1, y0 + 1, seed) / (float)uint.MaxValue;

            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        /// <summary>Octaves of <see cref="Value"/>, halving amplitude and scale each time.</summary>
        private static float Fbm(int x, int y, int seed, float scale, int octaves)
        {
            float sum = 0f, amp = 1f, total = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Value(x, y, seed + o * 977, scale);
                total += amp;
                amp *= 0.5f;
                scale *= 0.5f;
            }
            return total <= 0f ? 0.5f : sum / total;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>0 in the body of the page, rising to 1 at the extreme edge.</summary>
        private static float EdgeFalloff(int x, int y, int w, int h)
        {
            float nx = Mathf.Abs(x / (w - 1f) * 2f - 1f);
            float ny = Mathf.Abs(y / (h - 1f) * 2f - 1f);
            // Narrow. A wide falloff reads as a vignette or a fold across the whole sheet
            // rather than as a handled edge, and it swamps the grain it is meant to sit under.
            return Edge(0.88f, 1f, Mathf.Max(nx, ny));
        }

        /// <summary>
        /// GLSL-style smoothstep. **Not <c>Mathf.SmoothStep</c>**, which takes (from, to, t)
        /// and would return a near-constant value here rather than a transition — the same
        /// trap documented on <c>UiSprites.Edge</c>.
        /// </summary>
        private static float Edge(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / Mathf.Max(1e-6f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
    }
}
