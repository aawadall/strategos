// ControlMeasureDrawer.cs
// #162–#165: paint authored control measures into a MapRasterizer pixel buffer.
//
// Separate from MapRasterizer on purpose — MapData is generator terrain; these are scenario
// plan graphics. Call after RenderPixels (MapSheetCard afterPixels hook / PlayView).
// Visually distinct from OrderTrackLayer (UI overlays for live plans).

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
        /// Draw measures into <paramref name="pixels"/> in viewport space.
        /// </summary>
        /// <param name="sideColour">Side → ink. Null / unowned → dark grey.</param>
        /// <param name="viewer">
        /// When valid (#186), skip measures owned by another side. Shared (Owner = None) stay.
        /// </param>
        public static void Draw(
            Color32[] pixels,
            MapViewport view,
            IReadOnlyList<ControlMeasure> measures,
            Func<SideId, Color32> sideColour = null,
            SideId viewer = default)
        {
            if (pixels == null || measures == null || measures.Count == 0)
                return;

            for (int i = 0; i < measures.Count; i++)
            {
                var m = measures[i];
                if (m == null) continue;
                if (viewer.IsValid && m.Owner.IsValid && m.Owner != viewer) continue;

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
                    case ControlMeasureKind.AxisOfAdvance:
                        DrawAxis(pixels, view, m, ink);
                        break;
                    case ControlMeasureKind.DirectionOfAttack:
                        DrawArrow(pixels, view, m, ink,
                            thick: 0.5f, dash: false, filledHead: true, headCells: 3.2f);
                        break;
                    case ControlMeasureKind.Retirement:
                        DrawArrow(pixels, view, m, ink,
                            thick: 0.45f, dash: true, filledHead: false, headCells: 2.8f);
                        break;
                    case ControlMeasureKind.Counterattack:
                        DrawArrow(pixels, view, m, ink,
                            thick: 0.6f, dash: false, filledHead: false, headCells: 3.4f);
                        break;
                    case ControlMeasureKind.BattlePosition:
                        DrawArea(pixels, view, m, ink, hatchStep: 0);
                        break;
                    case ControlMeasureKind.EngagementArea:
                        DrawArea(pixels, view, m, ink, hatchStep: 10);
                        break;
                    case ControlMeasureKind.KillZone:
                        DrawArea(pixels, view, m, ink, hatchStep: 6);
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

            if (m.Echelon != Echelon.None)
            {
                DrawEchelonTick(px, view, verts[0], verts[1], m.Echelon, ink);
                int last = verts.Count - 1;
                DrawEchelonTick(px, view, verts[last], verts[last - 1], m.Echelon, ink);
            }
        }

        // #164 — main: fat solid + filled head; supporting: dashed; deception: lighter open head.
        private static void DrawAxis(Color32[] px, MapViewport view, ControlMeasure m, Color32 ink)
        {
            var verts = PolyPixels(view, m);
            if (verts.Count < 2) return;

            bool dashed = m.AxisRole != AxisOfAdvanceRole.Main;
            bool filled = m.AxisRole != AxisOfAdvanceRole.Deception;
            float thickMul = m.AxisRole == AxisOfAdvanceRole.Main ? 0.85f : 0.55f;
            Color32 stroke = m.AxisRole == AxisOfAdvanceRole.Deception
                ? new Color32(ink.r, ink.g, ink.b, (byte)Mathf.Max(100, ink.a * 2 / 3))
                : ink;

            int th = Mathf.Max(2, Mathf.RoundToInt(view.PixelsPerCell * thickMul));
            if (dashed)
            {
                float on = Mathf.Max(5f, view.PixelsPerCell * 2.8f);
                float gap = Mathf.Max(3f, view.PixelsPerCell * 1.4f);
                ProceduralDrawUtil.DrawDashedPolyline(px, view.Width, view.Height,
                    verts, stroke, th, on, gap);
            }
            else
            {
                ProceduralDrawUtil.DrawPolyline(px, view.Width, view.Height, verts, stroke, th);
            }

            float head = Mathf.Max(8f, view.PixelsPerCell * 4.5f);
            DrawHead(px, view, verts, stroke, head, filled);
        }

        private static void DrawArrow(
            Color32[] px, MapViewport view, ControlMeasure m, Color32 ink,
            float thick, bool dash, bool filledHead, float headCells)
        {
            var verts = PolyPixels(view, m);
            if (verts.Count < 2) return;

            int th = Mathf.Max(1, Mathf.RoundToInt(view.PixelsPerCell * thick));
            if (dash)
            {
                float d = Mathf.Max(4f, view.PixelsPerCell * 2.2f);
                float g = Mathf.Max(3f, view.PixelsPerCell * 1.2f);
                ProceduralDrawUtil.DrawDashedPolyline(px, view.Width, view.Height, verts, ink, th, d, g);
            }
            else
            {
                ProceduralDrawUtil.DrawPolyline(px, view.Width, view.Height, verts, ink, th);
            }

            float head = Mathf.Max(7f, view.PixelsPerCell * headCells);
            DrawHead(px, view, verts, ink, head, filledHead);
        }

        // #165 — closed outline; EA/KZ add diagonal hatch (step in px; 0 = outline only).
        private static void DrawArea(
            Color32[] px, MapViewport view, ControlMeasure m, Color32 ink, int hatchStep)
        {
            var verts = PolyPixels(view, m);
            if (verts.Count < 3) return;

            int th = Mathf.Max(1, Mathf.RoundToInt(view.PixelsPerCell * 0.45f));
            // Close the ring for DrawPolyline.
            var ring = new List<Vector2>(verts.Count + 1);
            ring.AddRange(verts);
            ring.Add(verts[0]);
            ProceduralDrawUtil.DrawPolyline(px, view.Width, view.Height, ring, ink, th);

            if (hatchStep > 0)
                DrawHatch(px, view, verts, ink, hatchStep);
        }

        private static void DrawHatch(
            Color32[] px, MapViewport view, List<Vector2> poly, Color32 ink, int step)
        {
            float minX = poly[0].x, maxX = poly[0].x, minY = poly[0].y, maxY = poly[0].y;
            for (int i = 1; i < poly.Count; i++)
            {
                minX = Mathf.Min(minX, poly[i].x);
                maxX = Mathf.Max(maxX, poly[i].x);
                minY = Mathf.Min(minY, poly[i].y);
                maxY = Mathf.Max(maxY, poly[i].y);
            }

            var hatchInk = new Color32(ink.r, ink.g, ink.b, (byte)Mathf.Max(70, ink.a * 2 / 3));
            float span = maxY - minY;
            for (float s = minX - span; s < maxX + span; s += step)
            {
                ProceduralDrawUtil.DrawLine(px, view.Width, view.Height,
                    Mathf.RoundToInt(s), Mathf.RoundToInt(minY),
                    Mathf.RoundToInt(s + span), Mathf.RoundToInt(maxY),
                    hatchInk, 1);
            }
        }

        private static void DrawHead(
            Color32[] px, MapViewport view, List<Vector2> verts, Color32 ink,
            float size, bool filled)
        {
            Vector2 tip = verts[verts.Count - 1];
            Vector2 prev = verts[verts.Count - 2];
            Vector2 dir = tip - prev;
            if (dir.sqrMagnitude < 0.01f) return;
            ProceduralDrawUtil.DrawArrowhead(px, view.Width, view.Height,
                tip, dir, size, ink, thickness: 2, filled: filled);
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
            if (verts.Count == 0 && m.Cell.sqrMagnitude > 0f)
                verts.Add(view.CellToPixel(m.Cell));
            return verts;
        }

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
