// RankInsignia.cs
// Procedural shoulder-board sprites for the player's command rank (#38).
//
// Same contract as UiSprites: white-friendly geometry where tinting helps, baked once per
// (mark, count) key, shared. Not Resources PNGs — see RankLadder.cs header for the store
// vs compute note.

using System.Collections.Generic;
using UnityEngine;
using Strategos.NatoSymbols;
using Strategos.Units;
using static Strategos.NatoSymbols.ProceduralDrawUtil;

namespace Strategos.UI
{
    public static class RankInsignia
    {
        public const int Width = 96;
        public const int Height = 48;

        private static readonly Dictionary<string, Sprite> Cache = new();

        /// <summary>Shoulder board for a rank step. Cached; never Destroy the sprite.</summary>
        public static Sprite For(in RankStep step)
        {
            string key = $"{step.Mark}:{Mathf.Max(1, step.Count)}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var sprite = Bake(step.Mark, Mathf.Clamp(step.Count, 1, 5));
            Cache[key] = sprite;
            return sprite;
        }

        private static Sprite Bake(RankMark mark, int count)
        {
            int w = Width, h = Height;
            var px = new Color32[w * h];
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < px.Length; i++) px[i] = clear;

            // Board: dark olive strap with a gold edge. Marks sit on it in gold.
            var board = new Color32(38, 48, 32, 255);
            var edge = new Color32(196, 168, 88, 255);
            var metal = new Color32(220, 196, 110, 255);

            FillRect(px, w, h, 2, 4, w - 3, h - 5, board, edge, 2);

            switch (mark)
            {
                case RankMark.Bars:
                    DrawBars(px, w, h, count, metal);
                    break;
                case RankMark.Chevrons:
                    DrawChevrons(px, w, h, count, metal);
                    break;
                case RankMark.Leaf:
                    DrawLeaf(px, w, h, metal);
                    break;
                case RankMark.Pip:
                    DrawPips(px, w, h, count, metal);
                    break;
                default:
                    DrawStars(px, w, h, count, metal);
                    break;
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"Rank_{mark}_{count}",
            };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void DrawBars(Color32[] px, int w, int h, int count, Color32 col)
        {
            int barH = count == 1 ? 7 : 5;
            int gap = 4;
            int total = count * barH + (count - 1) * gap;
            int y0 = (h - total) / 2;
            int left = w / 4;
            int right = w - w / 4;

            for (int i = 0; i < count; i++)
            {
                int y = y0 + i * (barH + gap);
                FillRect(px, w, h, left, y, right, y + barH - 1, col, col, 0);
            }
        }

        private static void DrawChevrons(Color32[] px, int w, int h, int count, Color32 col)
        {
            int cx = w / 2;
            int spacing = 7;
            int baseY = h / 2 - (count - 1) * spacing / 2 - 4;
            for (int i = 0; i < count; i++)
            {
                int cy = baseY + i * spacing;
                // Point up: tip above base.
                DrawLine(px, w, h, cx - 14, cy, cx, cy + 10, col, 3);
                DrawLine(px, w, h, cx + 14, cy, cx, cy + 10, col, 3);
            }
        }

        private static void DrawLeaf(Color32[] px, int w, int h, Color32 col)
        {
            int cx = w / 2, cy = h / 2;
            // Pointed leaf as a diamond — FillEllipse is square-buffer-only.
            var ring = new[]
            {
                new Vector2(cx, cy + 16),
                new Vector2(cx + 11, cy + 2),
                new Vector2(cx + 8, cy - 6),
                new Vector2(cx, cy - 16),
                new Vector2(cx - 8, cy - 6),
                new Vector2(cx - 11, cy + 2),
            };
            FillPolygon(px, w, h, ring, col);
            DrawLine(px, w, h, cx, cy - 14, cx, cy + 14, new Color32(38, 48, 32, 255), 1);
        }

        private static void DrawPips(Color32[] px, int w, int h, int count, Color32 col)
        {
            int r = 5;
            int gap = 4;
            int total = count * (r * 2) + (count - 1) * gap;
            int x0 = (w - total) / 2 + r;
            int cy = h / 2;
            for (int i = 0; i < count; i++)
            {
                int cx = x0 + i * (r * 2 + gap);
                FillCircle(px, w, h, cx, cy, r, col);
            }
        }

        private static void DrawStars(Color32[] px, int w, int h, int count, Color32 col)
        {
            float spacing = count <= 2 ? 28f : 22f;
            float total = (count - 1) * spacing;
            float x0 = (w - total) * 0.5f;
            float cy = h * 0.5f;
            float radius = count >= 4 ? 8f : 10f;

            for (int i = 0; i < count; i++)
            {
                float cx = x0 + i * spacing;
                FillStar(px, w, h, cx, cy, radius, col);
            }
        }

        private static void FillStar(Color32[] px, int w, int h, float cx, float cy,
            float radius, Color32 col)
        {
            // Five-point star as a polygon. ProceduralDrawUtil has no star helper; geometry
            // here is the same reason UiSprites draws brackets instead of typing them.
            var ring = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = -Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = (i % 2 == 0) ? radius : radius * 0.4f;
                ring[i] = new Vector2(cx + Mathf.Cos(ang) * r, cy + Mathf.Sin(ang) * r);
            }
            FillPolygon(px, w, h, ring, col);
        }
    }
}
