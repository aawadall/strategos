// MapDrapeTexture.cs
// Renders the 2D sheet that gets draped over the terrain mesh.
//
// Separate from MapSheetCard's render for one reason that matters: MIP-MAPS.
// MapRasterizer.Render builds its Texture2D with mipChain: false and Apply(false), which is
// right for a sheet shown flat at roughly 1:1 and wrong for one viewed in perspective, where
// the far half of the map is minified hard and a mip-less texture shimmers and aliases
// badly. So this goes through the public RenderPixels and builds its own texture with a mip
// chain, trilinear filtering and anisotropy.

using UnityEngine;

namespace Strategos.Maps
{
    public static class MapDrapeTexture
    {
        /// <summary>
        /// Longest edge of the generated texture. 2048 is a good balance: at 256 cells it is
        /// 8 px per cell, well above the 3 px the stroke widths are authored at, so roads and
        /// streams keep their intended weight when you lean in.
        /// </summary>
        public const int DefaultMaxPixels = 2048;

        /// <summary>Render options suited to a perspective drape.</summary>
        public static MapRenderOptions DefaultOptions()
        {
            var o = MapRenderOptions.Default;

            // Hybrid is documented as the default 3D drape: topographic tint with
            // terrain-strength shading.
            o.Mode = MapRenderMode.Hybrid;

            // A coordinate grid on a drape in perspective reads as a net thrown over the
            // ground rather than as a reference, and labels are flat text lying on tilted
            // terrain. Both want a billboarding layer before they earn their place here.
            o.DrawGrid = false;
            o.DrawLabels = false;

            return o;
        }

        public static Texture2D Create(MapData map, MapRenderOptions options,
            int maxPixels = DefaultMaxPixels)
        {
            if (map == null) return null;

            int longest = Mathf.Max(map.Width, map.Height);
            options.PixelsPerCell = Mathf.Clamp(maxPixels / (float)longest, 0.5f, 4f);

            // Whole map, always: the mesh's UVs are derived from ForWholeMap's window and a
            // partial window would silently misregister the drape.
            options.CellWindow = null;

            var pixels = MapRasterizer.RenderPixels(map, options, out var view);

            var tex = new Texture2D(view.Width, view.Height, TextureFormat.RGBA32, mipChain: true)
            {
                name = "MapDrape",
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 4,
            };
            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: true);
            return tex;
        }

        public static Texture2D Create(MapData map, int maxPixels = DefaultMaxPixels) =>
            Create(map, DefaultOptions(), maxPixels);
    }
}
