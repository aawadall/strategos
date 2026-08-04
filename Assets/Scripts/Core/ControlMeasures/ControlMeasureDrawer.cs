// ControlMeasureDrawer.cs
// #162 / #163: paint authored control measures into a MapRasterizer pixel buffer.
//
// Separate from MapRasterizer on purpose — MapData is generator terrain; these are scenario
// plan graphics. Call after RenderPixels (MapSheetCard afterPixels hook / PlayView). Epic
// child #166 may move the call site; the draw rules live here either way.

using System;
using System.Collections.Generic;
using UnityEngine;
using Strategos.Maps;
using Strategos.NatoSymbols;
using Strategos.Units;

namespace Strategos.ControlMeasures
{
    public static class ControlMeasureDrawer
    {
        static readonly Color32 FallbackInk = new Color32(40, 40, 40, 220);

        /// <summary>
        /// Draw every supported measure into <paramref name="pixels"/> in viewport space.
        /// Unknown / future kinds are skipped silently.
        /// </summary>
        public static void Draw(
            Color32[] pixels,
            MapViewport view,
            IReadOnlyList<ControlMeasure> measures,
            Func<SideId, Color32> sideColour = null)
        {
            if (pixels == null || measures == null || measures.Count == 0)
                return;

            for (int i = 0; i < measures.Count; i++)
            {
                var m = measures[i];
                if (m == null) continue;
                var ink = ResolveInk(m.Owner, sideColour);

                switch (m.Kind)
                {
                    case ControlMeasureKind.Checkpoint:
                        DrawCheckpoint(pixels, view, m, ink);
                        break;
                    case ControlMeasureKind.PhaseLine:
                        DrawPhaseLine(pixels, view, m, ink);
                        break;
                    case ControlMeasureKind.Boundary:
                        DrawBoundary(pixels, view, m, ink);
                        break;
                }
            }
        }

        private static Color32 ResolveInk(SideId owner, Func<SideId, Color32> sideColour)
        {
            if (sideColour == null || !owner.IsValid) return FallbackInk;
            return sideColour(owner);
        }

        private static void DrawCheckpoint(Color32[] px, MapViewport view, ControlMeasure m,
            Color32 ink)
        {
            var p = view.CellToPixel(m.Cell);
            int cx = Mathf.RoundToInt(p.x);
            int cy = Mathf.RoundToInt(p.y);
            int r = Mathf.Max(3, Mathf.RoundToInt(Mathf.Max(1.5f, m.RadiusCells) * view.PixelsPerCell));
            int th = Mathf.Max(1, Mathf.RoundToInt(view.PixelsPerCell * 0.35f));

            ProceduralDrawUtil.DrawCircleOutline(px, view.Width, view.Height, cx, cy, r, ink, th);
            // Flag tick — short upright from the centre, APP-6D-ish checkpoint shorthand.
            int tip = cy + r + Mathf.Max(2, r / 2);
            ProceduralDrawUtil.DrawLine(px, view.Width, view.Height, cx, cy, cx, tip, ink, th);
            ProceduralDrawUtil.FillCircle(px, view.Width, view.Height, cx, tip, Mathf.Max(2, th + 1), ink);
        }

        private static void DrawPhaseLine(Color32[] px, MapViewport view, ControlMeasure m,
            Color32 ink)
        {
            var verts = PolyPixels(view, m);
            if (verts.Count < 2) return;

            int th = Mathf.Max(1, Mathf.RoundToInt(view.PixelsPerCell * 0.45f));
            float dash = Mathf.Max(4f, view.PixelsPerCell * 2.5f);
            float gap = Mathf.Max(3f, view.PixelsPerCell * 1.5f);
            ProceduralDrawUtil.DrawDashedPolyline(px, view.Width, view.Height, verts, ink, th, dash, gap);
        }

        private static void DrawBoundary(Color32[] px, MapViewport view, ControlMeasure m,
            Color32 ink)
        {
            var verts = PolyPixels(view, m);
            if (verts.Count < 2) return;

            int th = Mathf.Max(2, Mathf.RoundToInt(view.PixelsPerCell * 0.55f));
            ProceduralDrawUtil.DrawPolyline(px, view.Width, view.Height, verts, ink, th);

            // Echelon ticks at both ends — adapted from AmplifierDecorator.DrawEchelon geometry
            // but placed in map pixel space at the polyline endpoints.
            if (m.Echelon != Echelon.None)
            {
                DrawEchelonTick(px, view, verts[0], verts[1], m.Echelon, ink);
                int last = verts.Count - 1;
                DrawEchelonTick(px, view, verts[last], verts[last - 1], m.Echelon, ink);
            }
        }

        private static List<Vector2> PolyPixels(MapViewport view, ControlMeasure m)
        {
            var verts = new List<Vector2>();
            var pts = m.Points;
            if (pts != null)
            {
                for (int i = 0; i < pts.Count; i++)
                    verts.Add(view.CellToPixel(pts[i]));
            }
            // Allow a degenerate single-point line authored as Cell + one Point, or Cell alone.
            if (verts.Count == 0 && m.Cell.sqrMagnitude > 0f)
                verts.Add(view.CellToPixel(m.Cell));
            return verts;
        }

        /// <summary>
        /// Company–corps style marks (bars / Xs) at a line endpoint, perpendicular to the
        /// segment toward <paramref name="toward"/>.
        /// </summary>
        private static void DrawEchelonTick(Color32[] px, MapViewport view, Vector2 at, Vector2 toward,
            Echelon echelon, Color32 ink)
        {
            Vector2 dir = at - toward;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
            dir.Normalize();
            Vector2 perp = new Vector2(-dir.y, dir.x);

            float s = Mathf.Max(1f, view.PixelsPerCell);
            int cx = Mathf.RoundToInt(at.x + dir.x * s * 1.2f);
            int cy = Mathf.RoundToInt(at.y + dir.y * s * 1.2f);
            int th = Mathf.Max(1, Mathf.RoundToInt(s * 0.35f));

            switch (echelon)
            {
                case Echelon.Team:
                case Echelon.Squad:
                    ProceduralDrawUtil.FillCircle(px, view.Width, view.Height, cx, cy,
                        Mathf.Max(2, Mathf.RoundToInt(s * 0.7f)), ink);
                    break;
                case Echelon.Section:
                    ProceduralDrawUtil.FillCircle(px, view.Width, view.Height,
                        Mathf.RoundToInt(cx - perp.x * s), Mathf.RoundToInt(cy - perp.y * s),
                        Mathf.Max(2, Mathf.RoundToInt(s * 0.55f)), ink);
                    ProceduralDrawUtil.FillCircle(px, view.Width, view.Height,
                        Mathf.RoundToInt(cx + perp.x * s), Mathf.RoundToInt(cy + perp.y * s),
                        Mathf.Max(2, Mathf.RoundToInt(s * 0.55f)), ink);
                    break;
                case Echelon.Platoon:
                    for (int k = -1; k <= 1; k++)
                        ProceduralDrawUtil.FillCircle(px, view.Width, view.Height,
                            Mathf.RoundToInt(cx + perp.x * s * k),
                            Mathf.RoundToInt(cy + perp.y * s * k),
                            Mathf.Max(2, Mathf.RoundToInt(s * 0.5f)), ink);
                    break;
                case Echelon.Company:
                    DrawBars(px, view, cx, cy, perp, 1, s, th, ink);
                    break;
                case Echelon.Battalion:
                    DrawBars(px, view, cx, cy, perp, 2, s, th, ink);
                    break;
                case Echelon.Regiment:
                    DrawBars(px, view, cx, cy, perp, 3, s, th, ink);
                    break;
                case Echelon.Brigade:
                    DrawX(px, view, cx, cy, s * 2.2f, th, ink);
                    break;
                case Echelon.Division:
                    DrawX(px, view, cx, cy, s * 2.2f, th, ink);
                    DrawX(px, view, Mathf.RoundToInt(cx + dir.x * s * 1.6f),
                        Mathf.RoundToInt(cy + dir.y * s * 1.6f), s * 2.2f, th, ink);
                    break;
                default:
                    // Corps and above — three Xs is visually dense at map scale; two + a bar.
                    DrawX(px, view, cx, cy, s * 2.4f, th, ink);
                    DrawBars(px, view, Mathf.RoundToInt(cx + dir.x * s * 2f),
                        Mathf.RoundToInt(cy + dir.y * s * 2f), perp, 1, s, th, ink);
                    break;
            }
        }

        private static void DrawBars(Color32[] px, MapViewport view, int cx, int cy, Vector2 perp,
            int count, float s, int th, Color32 ink)
        {
            float half = s * 1.8f;
            float spacing = s * 1.1f;
            float start = -0.5f * (count - 1) * spacing;
            Vector2 across = new Vector2(-perp.y, perp.x);
            for (int i = 0; i < count; i++)
            {
                float o = start + i * spacing;
                int ax = Mathf.RoundToInt(cx + perp.x * o - across.x * half);
                int ay = Mathf.RoundToInt(cy + perp.y * o - across.y * half);
                int bx = Mathf.RoundToInt(cx + perp.x * o + across.x * half);
                int by = Mathf.RoundToInt(cy + perp.y * o + across.y * half);
                ProceduralDrawUtil.DrawLine(px, view.Width, view.Height, ax, ay, bx, by, ink, th);
            }
        }

        private static void DrawX(Color32[] px, MapViewport view, int cx, int cy, float arm, int th,
            Color32 ink)
        {
            int a = Mathf.RoundToInt(arm);
            ProceduralDrawUtil.DrawLine(px, view.Width, view.Height, cx - a, cy - a, cx + a, cy + a, ink, th);
            ProceduralDrawUtil.DrawLine(px, view.Width, view.Height, cx - a, cy + a, cx + a, cy - a, ink, th);
        }
    }
}
