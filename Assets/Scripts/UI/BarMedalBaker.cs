// BarMedalBaker.cs
// #467: procedural service-ribbon sprites from BarMedalDef.Stripes + Device.

using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Strategos.Medals;
using static Strategos.NatoSymbols.ProceduralDrawUtil;

namespace Strategos.UI
{
    public static class BarMedalBaker
    {
        public const int Width = 96;
        public const int Height = 28;

        private static readonly Dictionary<string, Sprite> Cache = new();

        /// <summary>Cached ribbon. Never Destroy the sprite.</summary>
        public static Sprite For(BarMedalDef def, int numeral = 0)
        {
            if (def == null) return null;
            int n = Mathf.Clamp(numeral, 0, 99);
            string key = $"{def.Id}:{n}:{def.Device}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var sprite = Bake(def, n);
            Cache[key] = sprite;
            return sprite;
        }

        private static Sprite Bake(BarMedalDef def, int numeral)
        {
            int w = Width, h = Height;
            var px = new Color32[w * h];
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < px.Length; i++) px[i] = clear;

            var stripes = ParseStripes(def.Stripes);
            if (stripes.Count == 0)
                stripes.Add(new Color32(80, 80, 80, 255));

            int band = Mathf.Max(1, (h - 6) / stripes.Count);
            int y = 3;
            for (int s = 0; s < stripes.Count; s++)
            {
                int y1 = (s == stripes.Count - 1) ? h - 4 : y + band - 1;
                FillRect(px, w, h, 4, y, w - 5, y1, stripes[s], stripes[s], 0);
                y = y1 + 1;
            }

            var edge = new Color32(40, 36, 28, 255);
            FillRect(px, w, h, 3, 2, w - 4, h - 3, clear, edge, 1);

            var metal = new Color32(220, 196, 110, 255);
            if (def.Device == BarMedalDevice.Star)
                DrawStar(px, w, h, w / 2, h / 2, metal);
            else if (def.Device == BarMedalDevice.Numeral && numeral > 0)
                DrawNumeralPip(px, w, h, numeral, metal);
            else if (def.Device == BarMedalDevice.Oak)
                FillCircle(px, w, h, w / 2, h / 2, 4, metal);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"Medal_{def.Id}_{numeral}",
            };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        private static List<Color32> ParseStripes(string[] hexes)
        {
            var list = new List<Color32>();
            if (hexes == null) return list;
            for (int i = 0; i < hexes.Length; i++)
            {
                if (TryParseHex(hexes[i], out var c)) list.Add(c);
            }
            return list;
        }

        private static bool TryParseHex(string hex, out Color32 color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            hex = hex.Trim();
            if (hex[0] == '#') hex = hex.Substring(1);
            if (hex.Length != 6 && hex.Length != 8) return false;
            if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v))
                return false;
            if (hex.Length == 6)
            {
                color = new Color32(
                    (byte)((v >> 16) & 0xFF),
                    (byte)((v >> 8) & 0xFF),
                    (byte)(v & 0xFF),
                    255);
            }
            else
            {
                color = new Color32(
                    (byte)((v >> 24) & 0xFF),
                    (byte)((v >> 16) & 0xFF),
                    (byte)((v >> 8) & 0xFF),
                    (byte)(v & 0xFF));
            }
            return true;
        }

        private static void DrawStar(Color32[] px, int w, int h, int cx, int cy, Color32 col)
        {
            FillCircle(px, w, h, cx, cy, 3, col);
            FillRect(px, w, h, cx - 1, cy - 5, cx + 1, cy + 5, col, col, 0);
            FillRect(px, w, h, cx - 5, cy - 1, cx + 5, cy + 1, col, col, 0);
        }

        private static void DrawNumeralPip(Color32[] px, int w, int h, int numeral, Color32 col)
        {
            // Up to 5 pips along the ribbon; larger tallies still show five + the label in UI.
            int pips = Mathf.Clamp(numeral, 1, 5);
            int gap = 6;
            int total = pips * 4 + (pips - 1) * gap;
            int x0 = (w - total) / 2;
            for (int i = 0; i < pips; i++)
            {
                int x = x0 + i * (4 + gap);
                FillRect(px, w, h, x, h / 2 - 3, x + 3, h / 2 + 3, col, col, 0);
            }
        }
    }
}
