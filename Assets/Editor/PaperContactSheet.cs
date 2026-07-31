// PaperContactSheet.cs
// Bakes the procedurally aged paper stock to PNGs so it can be judged in an image rather
// than by reading the generator.
//
// Menu:  Strategos → Bake Paper Contact Sheet
// Batch: -executeMethod Strategos.Editor.PaperContactSheet.Bake
//
// Two outputs, following MapContactSheet's split for the same reason — a grid answers "does
// the stock vary sensibly" and a detail page answers "can you still read the text on it":
//
//   Artifacts/paper-contact-sheet.png   every preset against several seeds
//   Artifacts/paper-detail.png          one binder page, text over the paper, rects reserved
//
// READ THE NUMBERS, NOT ONLY THE PICTURE. The contrast ratio of body ink against the darkest
// pixel actually produced is logged per cell, measured from the baked texture rather than
// predicted from the options. A stain that costs contrast is invisible as a bug — it reads as
// styling — so the number is the only thing that catches it. UiTheme's floor is 7:1 (AAA);
// this refuses to pass anything under 4.5:1 (AA) and warns between the two.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Strategos.NatoSymbols;
using Strategos.UI;

namespace Strategos.Editor
{
    public static class PaperContactSheet
    {
        private const int Cell = 256;
        private const int Gutter = 10;
        private const string SheetPath = "Artifacts/paper-contact-sheet.png";
        private const string DetailPath = "Artifacts/paper-detail.png";

        /// <summary>WCAG AAA, the floor UiTheme documents for every text/background pair.</summary>
        private const float TargetContrast = 7f;

        /// <summary>WCAG AA. Below this the sheet is a failure, not a warning.</summary>
        private const float MinimumContrast = 4.5f;

        private static readonly int[] Seeds = { 1, 7, 42, 20260731 };

        [MenuItem("Strategos/Bake Paper Contact Sheet")]
        public static void Bake()
        {
            bool ok = BakeGrid();
            ok &= BakeDetail();

            Debug.Log(ok
                ? "[PaperContactSheet] PROBE PASSED"
                : "[PaperContactSheet] PROBE FAILED");
        }

        // ─── Grid ─────────────────────────────────────────────────────────────

        private static bool BakeGrid()
        {
            var presets = new (string Name, PaperOptions Options)[]
            {
                ("clean", PaperOptions.Clean),
                ("used", PaperOptions.Used),
                ("worn", PaperOptions.Worn),
            };

            int cols = Seeds.Length;
            int rows = presets.Length;

            // Gutters, because each cell carries its own edge shading and abutting them makes
            // twelve separate sheets read as one continuous surface with grid lines ruled
            // across it — which is exactly what the first bake looked like.
            int w = cols * Cell + (cols + 1) * Gutter;
            int h = rows * Cell + (rows + 1) * Gutter;

            var sheet = new Color32[w * h];
            var backing = new Color32(70, 70, 66, 255);
            for (int i = 0; i < sheet.Length; i++) sheet[i] = backing;

            bool ok = true;

            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var options = presets[r].Options;
                var tex = PaperTexture.Create(Cell, Cell, Seeds[c], options);
                var px = tex.GetPixels32();

                // Sheet origin is bottom-left; fill rows top-down so the log's row numbers
                // match what the reader sees.
                int ox = Gutter + c * (Cell + Gutter);
                int oy = Gutter + (rows - 1 - r) * (Cell + Gutter);
                for (int y = 0; y < Cell; y++)
                for (int x = 0; x < Cell; x++)
                    sheet[(oy + y) * w + (ox + x)] = px[y * Cell + x];

                // Held to AAA across the whole surface only where the preset claims to be safe
                // for unreserved text. Where it does not, the whole-sheet figure is measuring
                // the inside of a coffee ring — ground no text will ever be placed on — and
                // failing the bake on it would condemn a page that is perfectly legible. The
                // detail bake is what holds those presets to account, inside their reserves.
                var stats = Measure(px);
                string label = $"{presets[r].Name,-5} seed {Seeds[c],-9}";
                if (options.RequiresReservedText) ReportInfo(label + " unreserved", stats);
                else ok &= Report(label, stats);

                Object.DestroyImmediate(tex);
            }

            Write(sheet, w, h, SheetPath);
            Debug.Log($"[PaperContactSheet] {rows}x{cols} sheets -> {SheetPath}");
            return ok;
        }

        // ─── Detail ───────────────────────────────────────────────────────────

        /// <summary>
        /// One page of the binder from #61, so the reserve can be seen working: the stains are
        /// generated over the whole sheet and every text box is held clear of them.
        /// </summary>
        private static bool BakeDetail()
        {
            const int w = 900;
            const int h = 520;
            const int scale = 3;
            const int lineH = 40;
            const int marginX = 40;
            const int pad = 8;

            // The content a drill page carries — see #61. Codes and text are placeholder;
            // the layout is what is being checked.
            // No commas or colons: the 5x7 bitmap font maps neither, and DrawText skips an
            // unmapped character silently rather than boxing it — so punctuation vanishes and
            // the line still looks deliberate. Only the contact sheet is affected; the binder
            // itself will use TMP.
            var lines = new[]
            {
                "36B   REACT TO CONTACT",
                "",
                "RETURN FIRE - LOCATE - SUPPRESS - ASSAULT",
                "NOT IN THE OPEN WITH NO COVER WITHIN 50 M",
                "",
                "1.  HALT AND RETURN FIRE",
                "2.  REPORT CONTACT TO HIGHER",
                "3.  LOCATE AND SUPPRESS",
                "4.  ASSAULT OR BREAK CONTACT",
            };

            // Reserve every text box before the paper is generated, so a stain is never laid
            // where the ink will go. This is the whole point of the keepClear parameter and
            // the reason MapLabelPlacer.Reserve exists for the map's labels.
            var reserved = new List<RectInt>();
            var baselines = new int[lines.Length];
            bool fits = true;

            for (int i = 0; i < lines.Length; i++)
            {
                int y = h - 56 - i * lineH;
                baselines[i] = y;
                if (string.IsNullOrEmpty(lines[i])) continue;

                int tw = ProceduralDrawUtil.MeasureText(lines[i], scale);
                int th = ProceduralDrawUtil.GlyphH * scale;
                reserved.Add(new RectInt(marginX - pad, y - pad, tw + pad * 2, th + pad * 2));

                // Asserted, because DrawText clips silently at the buffer edge: an overlong
                // line simply loses its tail and the page still looks deliberate. The first
                // bake of this shed the last three characters of a line and it read as a
                // deliberate abbreviation.
                if (marginX + tw > w)
                {
                    Debug.LogError($"[PaperContactSheet] line {i} is {marginX + tw} px wide on " +
                                   $"a {w} px page and will be clipped: \"{lines[i]}\"");
                    fits = false;
                }
            }

            var tex = PaperTexture.Create(w, h, PaperTexture.SeedFor("36B"),
                PaperOptions.Worn, reserved);
            var px = tex.GetPixels32();

            // Measured before the ink goes on: this is the contrast the *paper* offers, which
            // is what the reserve is protecting. Measuring afterwards would just find the ink.
            //
            // TWO NUMBERS, AND THE SECOND IS THE ONE THAT MATTERS. The whole-sheet figure
            // includes the inside of a stain's rim, where no text will ever be placed, so on
            // its own it condemns a page that is perfectly legible. The reserved-region figure
            // is the contrast text will actually be read against, and it is what the reserve
            // exists to hold up.
            ReportInfo("detail  36B  whole sheet", Measure(px));
            bool ok = fits & Report("detail  36B  in reserve ", Measure(px, w, h, reserved));

            var ink = ToColor32(UiTheme.Ink);
            var muted = ToColor32(UiTheme.InkMuted);

            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i])) continue;
                // Heading and the two summary lines muted, the numbered sequence in full ink,
                // matching how the plan card in #52 weights a row against its caption.
                var col = i == 0 || i >= 5 ? ink : muted;
                ProceduralDrawUtil.DrawText(px, w, h, marginX, baselines[i], lines[i], col, scale);
            }

            Write(px, w, h, DetailPath);
            Object.DestroyImmediate(tex);

            Debug.Log($"[PaperContactSheet] detail page, {reserved.Count} reserved rect(s) " +
                      $"-> {DetailPath}");
            return ok;
        }

        // ─── Measurement ──────────────────────────────────────────────────────

        private struct Stats
        {
            public Color32 Darkest;
            public float MinContrast;   // body ink against the darkest paper pixel
            public float MeanLuminance;
            public float Spread;        // max minus min relative luminance, i.e. how much texture
        }

        private static Stats Measure(Color32[] px)
        {
            float min = float.MaxValue, max = float.MinValue, sum = 0f;
            Color32 darkest = px.Length > 0 ? px[0] : default;

            for (int i = 0; i < px.Length; i++)
            {
                float l = RelativeLuminance(px[i]);
                sum += l;
                if (l > max) max = l;
                if (l < min) { min = l; darkest = px[i]; }
            }

            return Finish(darkest, min, max, sum, px.Length);
        }

        /// <summary>The same measurement, restricted to the rects text will occupy.</summary>
        private static Stats Measure(Color32[] px, int w, int h, IReadOnlyList<RectInt> regions)
        {
            float min = float.MaxValue, max = float.MinValue, sum = 0f;
            Color32 darkest = px.Length > 0 ? px[0] : default;
            int n = 0;

            for (int r = 0; r < regions.Count; r++)
            {
                var rect = regions[r];
                for (int y = Mathf.Max(0, rect.yMin); y < Mathf.Min(h, rect.yMax); y++)
                for (int x = Mathf.Max(0, rect.xMin); x < Mathf.Min(w, rect.xMax); x++)
                {
                    float l = RelativeLuminance(px[y * w + x]);
                    sum += l;
                    n++;
                    if (l > max) max = l;
                    if (l < min) { min = l; darkest = px[y * w + x]; }
                }
            }

            return Finish(darkest, min, max, sum, n);
        }

        private static Stats Finish(Color32 darkest, float min, float max, float sum, int n) =>
            new()
            {
                Darkest = darkest,
                MinContrast = Contrast(RelativeLuminance(ToColor32(UiTheme.Ink)), min),
                MeanLuminance = n == 0 ? 0f : sum / n,
                Spread = n == 0 ? 0f : max - min,
            };

        /// <summary>
        /// The same line, stated and not asserted — for a figure that is worth knowing and is
        /// not a promise the stock makes. Kept distinct from <see cref="Report"/> so a number
        /// is never quietly downgraded from an assertion to a note.
        /// </summary>
        private static void ReportInfo(string label, in Stats s) =>
            Debug.Log($"[PaperContactSheet]   {label}  {Describe(s)}");

        private static bool Report(string label, in Stats s)
        {
            string line = $"[PaperContactSheet]   {label}  {Describe(s)}";

            if (s.MinContrast < MinimumContrast) { Debug.LogError(line); return false; }
            if (s.MinContrast < TargetContrast) { Debug.LogWarning(line); return true; }
            Debug.Log(line);
            return true;
        }

        private static string Describe(in Stats s)
        {
            string hex = ColorUtility.ToHtmlStringRGB(
                new Color32(s.Darkest.r, s.Darkest.g, s.Darkest.b, 255));

            string verdict = s.MinContrast >= TargetContrast ? "AAA"
                           : s.MinContrast >= MinimumContrast ? "AA (below the 7:1 UiTheme sets)"
                           : "under AA";

            return $"darkest #{hex}  ink contrast {s.MinContrast:0.00}:1 {verdict}  " +
                   $"mean L {s.MeanLuminance:0.000}  spread {s.Spread:0.000}";
        }

        /// <summary>
        /// WCAG relative luminance, which is **not** a plain weighted sum of the sRGB channels —
        /// each channel is linearised first. Skipping that overstates the luminance of a dark
        /// colour and would report a contrast ratio better than the one on screen.
        /// </summary>
        private static float RelativeLuminance(Color32 c) =>
            0.2126f * Linear(c.r / 255f) + 0.7152f * Linear(c.g / 255f) + 0.0722f * Linear(c.b / 255f);

        private static float Linear(float c) =>
            c <= 0.03928f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);

        private static float Contrast(float a, float b)
        {
            float hi = Mathf.Max(a, b), lo = Mathf.Min(a, b);
            return (hi + 0.05f) / (lo + 0.05f);
        }

        private static Color32 ToColor32(Color c) => new(
            (byte)(Mathf.Clamp01(c.r) * 255f),
            (byte)(Mathf.Clamp01(c.g) * 255f),
            (byte)(Mathf.Clamp01(c.b) * 255f), 255);

        private static void Write(Color32[] px, int w, int h, string path)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply(false, false);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
#endif
