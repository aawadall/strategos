// MapPaletteProbe.cs
// #169 / #192: NatoTopo is wired, distinct from aged-paper Topographic, and paints differently.
//
// Menu:  Strategos > Probe Map Palette
// Batch: -executeMethod Strategos.Editor.MapPaletteProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Maps;
using Strategos.NatoSymbols;
using Strategos.UI;

namespace Strategos.Editor
{
    public static class MapPaletteProbe
    {
        [MenuItem("Strategos/Probe Map Palette")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckEnumAndLabels(log);
            bad += CheckNatoDistinct(log);
            bad += CheckDrawDiffers(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[MapPaletteProbe]\n" + log);
            else Debug.LogError("[MapPaletteProbe]\n" + log);
        }

        private static int CheckEnumAndLabels(StringBuilder log)
        {
            bool found = false;
            for (int i = 0; i < DisplayNames.RenderModes.Length; i++)
                if (DisplayNames.RenderModes[i] == MapRenderMode.NatoTopo) found = true;
            if (!found)
            {
                log.AppendLine("  enum: FAILED — NatoTopo missing from DisplayNames.RenderModes");
                return 1;
            }

            string label = DisplayNames.RenderModeLabel(MapRenderMode.NatoTopo);
            if (string.IsNullOrEmpty(label) || !label.Contains("NATO"))
            {
                log.AppendLine($"  enum: FAILED — label '{label}' should name NATO");
                return 1;
            }

            var p = MapPalette.For(MapRenderMode.NatoTopo);
            if (p == null)
            {
                log.AppendLine("  enum: FAILED — For(NatoTopo) returned null");
                return 1;
            }

            log.AppendLine($"  enum: OK — NatoTopo in dropdown as '{label}'");
            return 0;
        }

        private static int CheckNatoDistinct(StringBuilder log)
        {
            var paper = MapPalette.Topographic();
            var nato = MapPalette.NatoTopo();

            if (Same(paper.Paper, nato.Paper) && Same(paper.Water, nato.Water) &&
                Same(paper.Contour, nato.Contour) && Same(paper.Forest, nato.Forest))
            {
                log.AppendLine("  colours: FAILED — NatoTopo matches Topographic on paper/water/contour/forest");
                return 1;
            }

            // Brown relief: contour red channel should dominate green (not paper-tan).
            if (nato.Contour.r <= nato.Contour.g)
            {
                log.AppendLine($"  colours: FAILED — NatoTopo contour not brown " +
                               $"(r={nato.Contour.r} g={nato.Contour.g})");
                return 1;
            }

            // Blue water: blue channel high relative to red.
            if (nato.Water.b <= nato.Water.r)
            {
                log.AppendLine($"  colours: FAILED — NatoTopo water not blue " +
                               $"(r={nato.Water.r} b={nato.Water.b})");
                return 1;
            }

            log.AppendLine($"  colours: OK — paper {Hex(nato.Paper)} water {Hex(nato.Water)} " +
                           $"contour {Hex(nato.Contour)} forest {Hex(nato.Forest)}");
            return 0;
        }

        private static int CheckDrawDiffers(StringBuilder log)
        {
            var settings = new MapGenerationSettings
            {
                Width = 64,
                Height = 64,
                Seed = 20260804,
                EnableErosion = false,
            };
            var map = MapGenerator.Generate(settings);

            var optPaper = MapRenderOptions.Default;
            optPaper.Mode = MapRenderMode.Topographic;
            optPaper.PixelsPerCell = 1f;

            var optNato = MapRenderOptions.Default;
            optNato.Mode = MapRenderMode.NatoTopo;
            optNato.PixelsPerCell = 1f;

            var a = MapRasterizer.RenderPixels(map, optPaper, out _);
            var b = MapRasterizer.RenderPixels(map, optNato, out _);

            int differ = 0;
            for (int i = 0; i < a.Length; i++)
                if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b)
                    differ++;

            if (differ < 100)
            {
                log.AppendLine($"  draw: FAILED — only {differ} px differ Topographic vs NatoTopo");
                return 1;
            }

            log.AppendLine($"  draw: OK — {differ}/{a.Length} px differ at 64x64");
            return 0;
        }

        private static bool Same(Color32 a, Color32 b) =>
            a.r == b.r && a.g == b.g && a.b == b.b;

        private static string Hex(Color32 c) => $"#{c.r:X2}{c.g:X2}{c.b:X2}";
    }
}
#endif
