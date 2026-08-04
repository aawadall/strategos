// WorldObjectDrawer.cs
// #34 / #276: one sheet glyph for HazardBlocking — X mark, same pixel space as GCMs.

using System.Collections.Generic;
using UnityEngine;
using Strategos.Maps;
using Strategos.NatoSymbols;

namespace Strategos.World
{
    public static class WorldObjectDrawer
    {
        private static readonly Color32 HazardInk = new(180, 40, 30, 230);

        public static void Draw(Color32[] pixels, MapViewport view, IReadOnlyList<WorldObject> objects)
        {
            if (pixels == null || objects == null || objects.Count == 0) return;

            int th = Mathf.Max(1, Mathf.RoundToInt(view.PixelsPerCell * 0.4f));
            float arm = Mathf.Max(3f, view.PixelsPerCell * 1.2f);

            for (int i = 0; i < objects.Count; i++)
            {
                var o = objects[i];
                if (o == null || o.Kind != WorldObjectKind.HazardBlocking) continue;

                var p = view.CellToPixel(new Vector2(o.Cell.x, o.Cell.y));
                int cx = Mathf.RoundToInt(p.x);
                int cy = Mathf.RoundToInt(p.y);
                DrawX(pixels, view, cx, cy, arm, th, HazardInk);
            }
        }

        private static void DrawX(Color32[] px, MapViewport view, int cx, int cy, float arm, int th,
            Color32 ink)
        {
            int a = Mathf.RoundToInt(arm);
            ProceduralDrawUtil.DrawLine(px, view.Width, view.Height,
                cx - a, cy - a, cx + a, cy + a, ink, th);
            ProceduralDrawUtil.DrawLine(px, view.Width, view.Height,
                cx - a, cy + a, cx + a, cy - a, ink, th);
        }
    }
}
