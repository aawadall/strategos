// SplashBaker.cs
// Procedural boot-frame backdrop for SplashView (#482).
//
// The paper card already carries the brand text on its own opaque stock (see
// PaperTexture), so this only has to fill the margin around it — full-bleed behind
// the card, not competing with anything UiTheme holds to >= 7:1. That is why it can
// run through the plain MapGenerator/MapRasterizer pipeline EXPLORE and SCENARIO use
// with no extra legibility guard of its own.
//
// EnableErosion and EnableCulture are both off: this bakes on the boot frame itself,
// not on idle time, and a backdrop wash has no use for settlements or a road network.

using UnityEngine;
using Strategos.Maps;

namespace Strategos.UI
{
    public static class SplashBaker
    {
        /// <summary>Cell grid, chosen 16:9 so a full-bleed RawImage does not stretch it.</summary>
        private const int Cells      = 160;
        private const int CellsTall  = 90;
        private const float PixelsPerCell = 6f;

        /// <summary>How far the rendered sheet is dimmed, so it reads as a backdrop and
        /// not as the subject the paper card sits on top of.</summary>
        private const float Shade = 0.82f;

        /// <summary>
        /// Bakes one backdrop. The caller owns the texture and must <c>Destroy</c> it —
        /// same contract as <see cref="PaperTexture.Create"/>.
        /// </summary>
        public static Texture2D CreateBackground(int seed)
        {
            var settings = new MapGenerationSettings
            {
                Name          = "SPLASH",
                Seed          = seed,
                Width         = Cells,
                Height        = CellsTall,
                MetresPerCell = 25f,
                EnableErosion = false,
                EnableCulture = false,
            };

            var map = MapGenerator.Generate(settings);

            var options = MapRenderOptions.TerrainOnly;
            options.PixelsPerCell = PixelsPerCell;

            var pixels = MapRasterizer.RenderPixels(map, options, out var view);
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                pixels[i] = new Color32(
                    (byte)(c.r * Shade), (byte)(c.g * Shade), (byte)(c.b * Shade), 255);
            }

            var tex = new Texture2D(view.Width, view.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
                name       = $"SplashBg_{seed}",
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }
    }
}
