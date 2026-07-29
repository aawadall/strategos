// ProceduralSymbolFactory.cs
// Generates NATO APP-6D-correct symbol sprites at runtime via pixel operations.
// No art assets required — shapes are drawn from geometry definitions.
//
// Frame shapes per affiliation (APP-6D standard):
//   Friend        → horizontal rectangle   (#80E0FF blue)
//   Hostile       → diamond (rotated ◇)   (#FF8080 red)
//   Neutral       → square                 (#AAFFAA green)
//   Unknown       → ellipse                (#FFFF80 yellow)
//   Pending       → ellipse (same as Unk)
//
// Unit icon:  infantry "/" diagonal (all land units in demo)
// Echelon:    dots (•/••/•••), bars (I/II), X marks (X/XX/…/XXXXXX)
// Planned:    dashed border variant for AnticipatedPlanned status

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Strategos.NatoSymbols
{
    public sealed class ProceduralSymbolFactory : SymbolFactory
    {
        // ─── Layout (base 256×256, y = 0 at bottom) ───────────────────────────────

        private const int BASE = 256;

        // Frame bounding box
        private const int FL  = 24,  FR  = 232;   // left / right X
        private const int FB  = 56,  FT  = 180;   // bottom / top Y
        private const int FCX = 128, FCY = 118;   // centre ( (24+232)/2=128 , (56+180)/2=118 )
        private const int FHW = 104, FHH = 62;    // half-extents

        // Border (solid line width)
        private const int BW = 4;

        // Echelon mark area (above frame top → higher y)
        private const int ECH_CY   = 214;   // vertical centre of echelon marks
        private const int ECH_DR   = 8;     // dot radius
        private const int ECH_SPC  = 22;    // spacing between marks
        private const int ECH_BW   = 7;     // bar width  (Battalion I / Regiment II)
        private const int ECH_BH   = 22;    // bar height
        private const int ECH_XR   = 9;     // X-mark half-size (Brigade+)

        // ─── Cache ────────────────────────────────────────────────────────────────

        private readonly Dictionary<long, Sprite> _cache = new();

        // ─── SymbolFactory contract ────────────────────────────────────────────────

        public override Sprite GetSymbolSprite(SIDCCode code, int size = 256)
        {
            // Cache key: affiliation | echelon | planned-flag
            long key = ((long)(int)code.Affiliation << 9)
                     | ((long)(int)code.Echelon     << 1)
                     |  (code.IsPlanned ? 1L : 0L);

            if (_cache.TryGetValue(key, out var hit) && hit != null)
                return hit;

            var sprite = Build(code, size);
            _cache[key] = sprite;
            return sprite;
        }

        public override void ClearCache()
        {
            foreach (var s in _cache.Values)
                if (s != null) UnityEngine.Object.Destroy(s.texture);
            _cache.Clear();
        }

        // ─── Build ────────────────────────────────────────────────────────────────

        private Sprite Build(SIDCCode code, int sz)
        {
            float sc = (float)sz / BASE;

            // Pixel buffer (RGBA32, all transparent initially)
            var buf = new Color32[sz * sz];

            Color32 fill  = (Color32)AffiliationColour.ForAffiliation(code.Affiliation);
            Color32 black = new Color32(0, 0, 0, 255);

            RenderFrame   (buf, sz, sc, code, fill, black);
            RenderIcon    (buf, sz, sc, code.EntityCode, code.Dimension, black);
            RenderEchelon (buf, sz, sc, code.Echelon, black);

            var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
                name       = $"Sym_{code.Affiliation}_{code.Echelon}",
            };
            tex.SetPixels32(buf);
            tex.Apply(false);

            return Sprite.Create(tex,
                new Rect(0, 0, sz, sz),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: sz);
        }

        // ─── Frame ────────────────────────────────────────────────────────────────

        private void RenderFrame(Color32[] buf, int sz, float sc,
                                  SIDCCode code, Color32 fill, Color32 bdr)
        {
            int l  = P(FL,  sc), r  = P(FR,  sc);
            int b  = P(FB,  sc), t  = P(FT,  sc);
            int cx = P(FCX, sc), cy = P(FCY, sc);
            int hw = P(FHW, sc), hh = P(FHH, sc);
            int bw = Mathf.Max(2, P(BW, sc));

            switch (code.Affiliation)
            {
                // ── Diamond (Hostile, Suspect) ────────────────────────────────
                case Affiliation.Hostile:
                case Affiliation.Suspect:
                case Affiliation.Faker:
                    FillDiamond(buf, sz, cx, cy, hw, hh, fill, bdr, bw);
                    break;

                // ── Square (Neutral) ──────────────────────────────────────────
                case Affiliation.Neutral:
                case Affiliation.NeutralFriend:
                    // Use the frame height for both axes → square
                    FillRect(buf, sz, cx - hh, b, cx + hh, t,
                             fill, bdr, bw, code.IsPlanned);
                    break;

                // ── Ellipse (Unknown, Pending) ────────────────────────────────
                case Affiliation.Unknown:
                case Affiliation.Pending:
                case Affiliation.Joker:
                    FillEllipse(buf, sz, cx, cy, hw, hh, fill, bdr, bw);
                    break;

                // ── Rectangle (Friend, AssumedFriend — default) ───────────────
                default:
                    FillRect(buf, sz, l, b, r, t, fill, bdr, bw, code.IsPlanned);
                    break;
            }
        }

        // ─── Unit icon ────────────────────────────────────────────────────────────

        private void RenderIcon(Color32[] buf, int sz, float sc,
                                 int entityCode, SymbolDimension dim, Color32 bdr)
        {
            // Only Land units are rendered in the demo; all show the infantry
            // diagonal slash "/" (APP-6D infantry identifier).
            if (dim != SymbolDimension.Land) return;

            int margin = P(20, sc);
            int x0 = P(FL, sc) + margin,  y0 = P(FB, sc) + margin;   // lower-left
            int x1 = P(FR, sc) - margin,  y1 = P(FT, sc) - margin;   // upper-right
            int th = Mathf.Max(2, Mathf.RoundToInt(4 * sc));

            DrawLine(buf, sz, x0, y0, x1, y1, bdr, th);
        }

        // ─── Echelon marks ────────────────────────────────────────────────────────

        private void RenderEchelon(Color32[] buf, int sz, float sc, Echelon echelon, Color32 col)
        {
            int cy  = P(ECH_CY,  sc);
            int cx  = P(FCX,     sc);
            int dr  = Mathf.Max(3, P(ECH_DR,  sc));
            int spc = Mathf.Max(dr * 2 + 2, P(ECH_SPC, sc));
            int bw  = Mathf.Max(2, P(ECH_BW,  sc));
            int bh  = Mathf.Max(6, P(ECH_BH,  sc));
            int xr  = Mathf.Max(4, P(ECH_XR,  sc));
            int xth = Mathf.Max(1, Mathf.RoundToInt(2 * sc));

            switch (echelon)
            {
                // ── Dots ──────────────────────────────────────────────────────
                case Echelon.Team:
                    DrawCircleOutline(buf, sz, cx, cy, dr + 3, col,
                        Mathf.Max(1, Mathf.RoundToInt(2 * sc)));
                    break;
                case Echelon.Squad:
                    FillCircle(buf, sz, cx, cy, dr, col);
                    break;
                case Echelon.Section:
                    FillCircle(buf, sz, cx - spc / 2, cy, dr, col);
                    FillCircle(buf, sz, cx + spc / 2, cy, dr, col);
                    break;
                case Echelon.Platoon:
                case Echelon.Company:
                    FillCircle(buf, sz, cx - spc, cy, dr, col);
                    FillCircle(buf, sz, cx,        cy, dr, col);
                    FillCircle(buf, sz, cx + spc,  cy, dr, col);
                    break;

                // ── Bars (I, II) ──────────────────────────────────────────────
                case Echelon.Battalion:
                    FillRect(buf, sz,
                        cx - bw / 2, cy - bh / 2,
                        cx + bw / 2, cy + bh / 2,
                        col, col, 0);
                    break;
                case Echelon.Regiment:
                    FillRect(buf, sz, cx - spc / 2 - bw / 2, cy - bh / 2,
                                      cx - spc / 2 + bw / 2, cy + bh / 2, col, col, 0);
                    FillRect(buf, sz, cx + spc / 2 - bw / 2, cy - bh / 2,
                                      cx + spc / 2 + bw / 2, cy + bh / 2, col, col, 0);
                    break;

                // ── X marks (Brigade → Theater) ───────────────────────────────
                case Echelon.Brigade:
                    DrawX(buf, sz, cx, cy, xr, col, xth);
                    break;
                case Echelon.Division:
                    DrawXRow(buf, sz, cx, cy, xr, col, 2, spc, xth);
                    break;
                case Echelon.Corps:
                    DrawXRow(buf, sz, cx, cy, xr, col, 3, spc, xth);
                    break;
                case Echelon.Army:
                    DrawXRow(buf, sz, cx, cy, xr, col, 4, spc, xth);
                    break;
                case Echelon.ArmyGroup:
                    DrawXRow(buf, sz, cx, cy, xr, col, 5, spc, xth);
                    break;
                case Echelon.Theater:
                    DrawXRow(buf, sz, cx, cy, xr, col, 6, spc, xth);
                    break;
            }
        }

        // ─── Drawing primitives ───────────────────────────────────────────────────

        /// <summary>Filled/bordered axis-aligned rectangle.</summary>
        private static void FillRect(Color32[] px, int sz, int l, int b, int r, int t,
                                      Color32 fill, Color32 bdr, int bw, bool dashed = false)
        {
            for (int y = b; y <= t; y++)
            for (int x = l; x <= r; x++)
            {
                bool border = bw > 0 &&
                    (x <= l + bw - 1 || x >= r - bw + 1 ||
                     y <= b + bw - 1 || y >= t - bw + 1);

                Color32 c;
                if (border && dashed)
                    c = (x + y) % 14 < 8 ? bdr : fill;
                else
                    c = border ? bdr : fill;

                Set(px, sz, x, y, c);
            }
        }

        /// <summary>Filled/bordered diamond (◇ rotated square).</summary>
        private static void FillDiamond(Color32[] px, int sz, int cx, int cy, int hw, int hh,
                                         Color32 fill, Color32 bdr, int bw)
        {
            for (int y = cy - hh; y <= cy + hh; y++)
            for (int x = cx - hw; x <= cx + hw; x++)
            {
                float nx = (float)Math.Abs(x - cx) / hw;
                float ny = (float)Math.Abs(y - cy) / hh;
                if (nx + ny > 1.01f) continue;   // outside diamond

                int ihw = Math.Max(1, hw - bw), ihh = Math.Max(1, hh - bw);
                float nxi = (float)Math.Abs(x - cx) / ihw;
                float nyi = (float)Math.Abs(y - cy) / ihh;
                Set(px, sz, x, y, (nxi + nyi > 1.0f) ? bdr : fill);
            }
        }

        /// <summary>Filled/bordered ellipse (for Unknown affiliation).</summary>
        private static void FillEllipse(Color32[] px, int sz, int cx, int cy, int rx, int ry,
                                         Color32 fill, Color32 bdr, int bw)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                float dx = x - cx, dy = y - cy;
                if (dx * dx / ((float)rx * rx) + dy * dy / ((float)ry * ry) > 1.01f) continue;

                int irx = Math.Max(1, rx - bw), iry = Math.Max(1, ry - bw);
                bool border = dx * dx / ((float)irx * irx) + dy * dy / ((float)iry * iry) > 1.0f;
                Set(px, sz, x, y, border ? bdr : fill);
            }
        }

        /// <summary>Solid filled circle (echelon dots).</summary>
        private static void FillCircle(Color32[] px, int sz, int cx, int cy, int r, Color32 col)
        {
            int r2 = r * r;
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                int dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy <= r2) Set(px, sz, x, y, col);
            }
        }

        /// <summary>Hollow circle outline (Team echelon marker ○).</summary>
        private static void DrawCircleOutline(Color32[] px, int sz, int cx, int cy, int r,
                                               Color32 col, int thickness)
        {
            float outerSq = (float)(r + thickness) * (r + thickness);
            float innerSq = (float)(r - thickness) * (r - thickness);
            for (int y = cy - r - thickness; y <= cy + r + thickness; y++)
            for (int x = cx - r - thickness; x <= cx + r + thickness; x++)
            {
                float dx = x - cx, dy = y - cy, d2 = dx * dx + dy * dy;
                if (d2 >= innerSq && d2 <= outerSq) Set(px, sz, x, y, col);
            }
        }

        /// <summary>Anti-aliased-ish thick line via Bresenham with square pen.</summary>
        private static void DrawLine(Color32[] px, int sz, int x0, int y0, int x1, int y1,
                                      Color32 col, int thickness)
        {
            int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx - dy, t = thickness / 2;

            while (true)
            {
                for (int ty = -t; ty <= t; ty++)
                for (int tx = -t; tx <= t; tx++)
                    Set(px, sz, x0 + tx, y0 + ty, col);

                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 <  dx) { err += dx; y0 += sy; }
            }
        }

        /// <summary>Draws one × mark centred at (cx, cy) with arm length <paramref name="rad"/>.</summary>
        private static void DrawX(Color32[] px, int sz, int cx, int cy, int rad, Color32 col, int th)
        {
            DrawLine(px, sz, cx - rad, cy - rad, cx + rad, cy + rad, col, Mathf.Max(1, th));
            DrawLine(px, sz, cx - rad, cy + rad, cx + rad, cy - rad, col, Mathf.Max(1, th));
        }

        /// <summary>Draws a centred row of <paramref name="count"/> × marks.</summary>
        private void DrawXRow(Color32[] px, int sz, int cx, int cy, int xr, Color32 col,
                               int count, int spc, int th)
        {
            int half = (count - 1) * spc / 2;
            for (int i = 0; i < count; i++)
                DrawX(px, sz, cx - half + i * spc, cy, xr, col, th);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        // Scale an integer constant from BASE resolution to target size.
        private static int P(int v, float scale) => Mathf.RoundToInt(v * scale);

        // Bounds-checked pixel write (y = 0 at bottom).
        private static void Set(Color32[] px, int sz, int x, int y, Color32 c)
        {
            if ((uint)x < (uint)sz && (uint)y < (uint)sz)
                px[y * sz + x] = c;
        }
    }
}
