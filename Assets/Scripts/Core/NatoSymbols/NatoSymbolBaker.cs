// NatoSymbolBaker.cs
// Flattens an INatoSymbol's procedural (and sprite) layers into a single Texture2D / Sprite.

using UnityEngine;

namespace Strategos.NatoSymbols
{
    public static class NatoSymbolBaker
    {
        /// <summary>
        /// Bakes all layers of <paramref name="symbol"/> into a square Sprite.
        /// </summary>
        public static Sprite Bake(INatoSymbol symbol, int size = 256)
        {
            if (symbol == null) return null;

            float sc = (float)size / SymbolLayout.BASE;
            var buf = new Color32[size * size];

            // Stable order: SortOrder then insertion order.
            var layers = symbol.Layers;
            // Simple insertion sort by SortOrder (layer count is tiny).
            var ordered = new SymbolLayerDraw[layers.Count];
            for (int i = 0; i < layers.Count; i++)
                ordered[i] = layers[i];
            for (int i = 1; i < ordered.Length; i++)
            {
                var key = ordered[i];
                int j = i - 1;
                while (j >= 0 && ordered[j].SortOrder > key.SortOrder)
                {
                    ordered[j + 1] = ordered[j];
                    j--;
                }
                ordered[j + 1] = key;
            }

            foreach (var layer in ordered)
            {
                if (layer.Draw != null)
                {
                    layer.Draw(buf, size, sc);
                }
                else if (layer.Sprite != null)
                {
                    BlitSprite(buf, size, layer.Sprite, layer.Tint);
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
                name       = $"Sym_{symbol.Code.Raw}",
            };
            tex.SetPixels32(buf);
            tex.Apply(false);

            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: size);
            sprite.name = tex.name;
            return sprite;
        }

        private static void BlitSprite(Color32[] buf, int sz, Sprite sprite, Color tint)
        {
            var tex = sprite.texture;
            if (tex == null) return;

            // Requires readable texture; skip silently if not.
            Color32[] src;
            try
            {
                var rect = sprite.rect;
                src = tex.GetPixels32();
                int tw = tex.width;
                int th = tex.height;
                int rx = (int)rect.x, ry = (int)rect.y;
                int rw = (int)rect.width, rh = (int)rect.height;

                for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    int sx = rx + x * rw / sz;
                    int sy = ry + y * rh / sz;
                    if ((uint)sx >= (uint)tw || (uint)sy >= (uint)th) continue;
                    Color32 c = src[sy * tw + sx];
                    if (c.a == 0) continue;
                    c = new Color32(
                        (byte)(c.r * tint.r),
                        (byte)(c.g * tint.g),
                        (byte)(c.b * tint.b),
                        (byte)(c.a * tint.a));
                    // Alpha-over
                    int di = y * sz + x;
                    Color32 dst = buf[di];
                    float a = c.a / 255f;
                    buf[di] = new Color32(
                        (byte)(c.r * a + dst.r * (1 - a)),
                        (byte)(c.g * a + dst.g * (1 - a)),
                        (byte)(c.b * a + dst.b * (1 - a)),
                        (byte)Mathf.Clamp(c.a + dst.a, 0, 255));
                }
            }
            catch
            {
                // Non-readable sprite textures cannot be sampled on CPU.
            }
        }
    }
}
