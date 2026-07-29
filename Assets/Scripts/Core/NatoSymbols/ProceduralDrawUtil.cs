// ProceduralDrawUtil.cs
// Shared pixel primitives for procedural APP-6D symbol rendering.

using System;
using UnityEngine;

namespace Strategos.NatoSymbols
{
    public static class ProceduralDrawUtil
    {
        public static void Set(Color32[] px, int sz, int x, int y, Color32 c)
        {
            if ((uint)x < (uint)sz && (uint)y < (uint)sz)
                px[y * sz + x] = c;
        }

        public static void FillRect(Color32[] px, int sz, int l, int b, int r, int t,
            Color32 fill, Color32 bdr, int bw, FrameLineStyle style = FrameLineStyle.Solid)
        {
            for (int y = b; y <= t; y++)
            for (int x = l; x <= r; x++)
            {
                bool border = bw > 0 &&
                    (x <= l + bw - 1 || x >= r - bw + 1 ||
                     y <= b + bw - 1 || y >= t - bw + 1);

                Color32 c;
                if (border)
                {
                    if (!BorderVisible(x, y, style))
                        c = fill;
                    else
                        c = bdr;
                }
                else
                    c = fill;

                Set(px, sz, x, y, c);
            }
        }

        public static void FillDiamond(Color32[] px, int sz, int cx, int cy, int hw, int hh,
            Color32 fill, Color32 bdr, int bw, FrameLineStyle style = FrameLineStyle.Solid)
        {
            for (int y = cy - hh; y <= cy + hh; y++)
            for (int x = cx - hw; x <= cx + hw; x++)
            {
                float nx = (float)Math.Abs(x - cx) / hw;
                float ny = (float)Math.Abs(y - cy) / hh;
                if (nx + ny > 1.01f) continue;

                int ihw = Math.Max(1, hw - bw), ihh = Math.Max(1, hh - bw);
                float nxi = (float)Math.Abs(x - cx) / ihw;
                float nyi = (float)Math.Abs(y - cy) / ihh;
                bool border = nxi + nyi > 1.0f;
                if (border && !BorderVisible(x, y, style))
                    Set(px, sz, x, y, fill);
                else
                    Set(px, sz, x, y, border ? bdr : fill);
            }
        }

        public static void FillEllipse(Color32[] px, int sz, int cx, int cy, int rx, int ry,
            Color32 fill, Color32 bdr, int bw, FrameLineStyle style = FrameLineStyle.Solid)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                float dx = x - cx, dy = y - cy;
                if (dx * dx / ((float)rx * rx) + dy * dy / ((float)ry * ry) > 1.01f) continue;

                int irx = Math.Max(1, rx - bw), iry = Math.Max(1, ry - bw);
                bool border = dx * dx / ((float)irx * irx) + dy * dy / ((float)iry * iry) > 1.0f;
                if (border && !BorderVisible(x, y, style))
                    Set(px, sz, x, y, fill);
                else
                    Set(px, sz, x, y, border ? bdr : fill);
            }
        }

        public static void FillCircle(Color32[] px, int sz, int cx, int cy, int r, Color32 col)
        {
            int r2 = r * r;
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                int dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy <= r2) Set(px, sz, x, y, col);
            }
        }

        public static void DrawCircleOutline(Color32[] px, int sz, int cx, int cy, int r,
            Color32 col, int thickness)
        {
            float outerSq = (float)(r + thickness) * (r + thickness);
            float innerSq = (float)Math.Max(0, r - thickness) * Math.Max(0, r - thickness);
            for (int y = cy - r - thickness; y <= cy + r + thickness; y++)
            for (int x = cx - r - thickness; x <= cx + r + thickness; x++)
            {
                float dx = x - cx, dy = y - cy, d2 = dx * dx + dy * dy;
                if (d2 >= innerSq && d2 <= outerSq) Set(px, sz, x, y, col);
            }
        }

        public static void DrawLine(Color32[] px, int sz, int x0, int y0, int x1, int y1,
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

        public static void DrawX(Color32[] px, int sz, int cx, int cy, int rad, Color32 col, int th)
        {
            DrawLine(px, sz, cx - rad, cy - rad, cx + rad, cy + rad, col, Math.Max(1, th));
            DrawLine(px, sz, cx - rad, cy + rad, cx + rad, cy - rad, col, Math.Max(1, th));
        }

        public static void DrawXRow(Color32[] px, int sz, int cx, int cy, int xr, Color32 col,
            int count, int spc, int th)
        {
            int half = (count - 1) * spc / 2;
            for (int i = 0; i < count; i++)
                DrawX(px, sz, cx - half + i * spc, cy, xr, col, th);
        }

        /// <summary>Dashed / dotted visibility along a border path.</summary>
        private static bool BorderVisible(int x, int y, FrameLineStyle style)
        {
            switch (style)
            {
                case FrameLineStyle.Dashed:
                    return (x + y) % 14 < 8;
                case FrameLineStyle.Dotted:
                    // Alternating black/white dots approximated as on/off gaps.
                    return (x + y) % 10 < 5;
                default:
                    return true;
            }
        }
    }
}
