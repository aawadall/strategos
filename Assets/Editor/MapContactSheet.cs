// MapContactSheet.cs
// Bakes generated maps to PNGs so terrain and rendering changes can be reviewed as
// images. The map equivalent of SymbolContactSheet, and for the same reason: the
// output of a generator is a picture, and a diff of the code that made it tells you
// nothing about whether the rivers still run downhill.
//
// Two outputs, because they answer different questions:
//   map-contact-sheet.png  every relief profile against every render mode, whole
//                          sheet, 1 px per cell — does the terrain read at all?
//   map-detail.png         one map at working zoom — do roads, names, spot heights
//                          and the grid survive being looked at closely?
//
// Menu:  Strategos → Bake Map Contact Sheet
// Batch: -executeMethod Strategos.Editor.MapContactSheet.Bake

#if UNITY_EDITOR
using System.IO;
using Stopwatch = System.Diagnostics.Stopwatch;   // System.Diagnostics.Debug would
                                                  // otherwise shadow UnityEngine.Debug

using UnityEditor;
using UnityEngine;
using Strategos.Maps;
using Strategos.NatoSymbols;

namespace Strategos.Editor
{
    public static class MapContactSheet
    {
        /// <summary>
        /// Cells per side of each preview map. Small enough that seven maps generate
        /// in a batch run, large enough that a road network has somewhere to go.
        /// </summary>
        private const int MapCells = 256;

        private const int Seed = 20260729;

        private const string SheetPath  = "Artifacts/map-contact-sheet.png";
        private const string DetailPath = "Artifacts/map-detail.png";

        private static readonly ReliefProfile[] Profiles =
        {
            ReliefProfile.Plains,
            ReliefProfile.Rolling,
            ReliefProfile.Hills,
            ReliefProfile.Mountains,
            ReliefProfile.Coastal,
            ReliefProfile.Desert,
            ReliefProfile.Arctic,
        };

        private static readonly MapRenderMode[] Modes =
        {
            MapRenderMode.Topographic,
            MapRenderMode.Schematic,
            MapRenderMode.Terrain,
            MapRenderMode.Hybrid,
        };

        [MenuItem("Strategos/Bake Map Contact Sheet")]
        public static void Bake()
        {
            Directory.CreateDirectory("Artifacts");

            BakeSheet();
            BakeDetail();
        }

        // ─── Profiles × modes ─────────────────────────────────────────────────

        private static void BakeSheet()
        {
            int tile = MapCells;
            int w    = Modes.Length    * tile;
            int h    = Profiles.Length * tile;

            var sheet = new Color32[w * h];

            for (int row = 0; row < Profiles.Length; row++)
            {
                var profile = Profiles[row];

                var watch = Stopwatch.StartNew();
                var map   = Generate(profile);
                watch.Stop();

                Report(profile, map, watch.ElapsedMilliseconds);

                for (int col = 0; col < Modes.Length; col++)
                {
                    var options = MapRenderOptions.Default;
                    options.Mode          = Modes[col];
                    options.PixelsPerCell = 1f;

                    // At 1 px per cell a 5×7 glyph is a smudge and the sheet is about
                    // whether the terrain reads, so the text comes off.
                    options.DrawLabels = false;

                    var px = MapRasterizer.RenderPixels(map, options, out MapViewport view);

                    // Sheet origin is bottom-left; fill rows top-down so the first
                    // profile appears at the top, as the log lists them.
                    Blit(sheet, w, h, px, view.Width, view.Height,
                        col * tile, (Profiles.Length - 1 - row) * tile);

                    Caption(sheet, w, h,
                        col * tile + 4,
                        (Profiles.Length - 1 - row) * tile + tile - 12,
                        $"{profile} {Modes[col]}".ToUpperInvariant());
                }
            }

            Separators(sheet, w, h, tile);
            WritePng(sheet, w, h, SheetPath);

            Debug.Log($"[MapContactSheet] {Profiles.Length} profiles x {Modes.Length} modes " +
                      $"-> {SheetPath} ({w}x{h})");
        }

        // ─── One map at working zoom ──────────────────────────────────────────

        /// <summary>
        /// A quarter of a Hills map at 3 px per cell. Everything the whole-sheet pass
        /// suppresses — names, spot heights, grid designators, cased roads — only
        /// becomes checkable at this scale.
        /// </summary>
        private static void BakeDetail()
        {
            var map = Generate(ReliefProfile.Hills);

            var options = MapRenderOptions.Default;
            options.Mode          = MapRenderMode.Topographic;
            options.PixelsPerCell = 3f;
            options.CellWindow    = new Rect(
                MapCells * 0.25f, MapCells * 0.25f, MapCells * 0.5f, MapCells * 0.5f);

            var px = MapRasterizer.RenderPixels(map, options, out MapViewport view);

            WritePng(px, view.Width, view.Height, DetailPath);
            Debug.Log($"[MapContactSheet] detail -> {DetailPath} " +
                      $"({view.Width}x{view.Height}, {options.PixelsPerCell} px/cell)");
        }

        // ─── Generation ───────────────────────────────────────────────────────

        private static MapData Generate(ReliefProfile profile)
        {
            var settings = new MapGenerationSettings
            {
                Name    = profile.ToString(),
                Seed    = Seed,
                Width   = MapCells,
                Height  = MapCells,
                Profile = profile,
            };

            return MapGenerator.Generate(settings);
        }

        /// <summary>
        /// Logs what the generator produced. A blank tile on the sheet is ambiguous —
        /// it could be a generation failure or a palette one — and these numbers say
        /// which without opening the image.
        /// </summary>
        private static void Report(ReliefProfile profile, MapData map, long milliseconds)
        {
            var counts = new int[LandcoverInfo.Count];
            for (int i = 0; i < map.Landcover.Length; i++)
            {
                int c = map.Landcover[i];
                if (c < counts.Length) counts[c]++;
            }

            float total = Mathf.Max(1, map.Landcover.Length);
            var cover = new System.Text.StringBuilder();
            for (int c = 0; c < counts.Length; c++)
            {
                if (counts[c] == 0) continue;
                cover.Append($" {LandcoverInfo.DisplayName((LandcoverClass)c)}={counts[c] / total:P0}");
            }

            Debug.Log($"[MapContactSheet] {profile}: {milliseconds} ms, " +
                      $"elevation {map.Header.MinElevation:F0}–{map.Header.MaxElevation:F0} m, " +
                      $"contour {map.Header.ContourInterval:F0} m, " +
                      $"{map.Lines.Count} lines, {map.Areas.Count} areas, {map.Pois.Count} POIs," +
                      cover);
        }

        // ─── Sheet assembly ───────────────────────────────────────────────────

        private static void Blit(Color32[] dst, int dstW, int dstH,
            Color32[] src, int srcW, int srcH, int originX, int originY)
        {
            for (int y = 0; y < srcH; y++)
            {
                int dy = originY + y;
                if ((uint)dy >= (uint)dstH) continue;

                for (int x = 0; x < srcW; x++)
                {
                    int dx = originX + x;
                    if ((uint)dx >= (uint)dstW) continue;
                    dst[dy * dstW + dx] = src[y * srcW + x];
                }
            }
        }

        private static void Caption(Color32[] sheet, int w, int h, int x, int y, string text)
        {
            var halo = new Color32(255, 255, 255, 255);
            var ink  = new Color32(24, 22, 18, 255);

            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                ProceduralDrawUtil.DrawText(sheet, w, h, x + ox, y + oy, text, halo, 1);
            }

            ProceduralDrawUtil.DrawText(sheet, w, h, x, y, text, ink, 1);
        }

        private static void Separators(Color32[] sheet, int w, int h, int tile)
        {
            var rule = new Color32(90, 86, 76, 255);

            for (int x = tile; x < w; x += tile)
                ProceduralDrawUtil.DrawLine(sheet, w, h, x, 0, x, h - 1, rule, 1);

            for (int y = tile; y < h; y += tile)
                ProceduralDrawUtil.DrawLine(sheet, w, h, 0, y, w - 1, y, rule, 1);
        }

        private static void WritePng(Color32[] px, int w, int h, string path)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply(false, false);

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
#endif
