// TtpDiagram.cs
// The figure on a drill's facing page: unit symbols and graphic control measures, as data,
// plus the renderer that draws them.
//
// THIS IS A PHASE 5.1 PROTOTYPE, NOT DECORATION. docs/phases.md 5.1 lists graphic control
// measures — axes of advance, battle positions, objectives, support by fire — as unbuilt.
// A drill figure is their first consumer, and building them here buys two things a map
// cannot: the figure is static, so a contact sheet can check it headlessly, and it has no
// camera, no projection and no moving units to confound a rendering bug with a placement
// one. What is settled here transfers to the map unchanged.
//
// NORMALISED COORDINATES, ORIGIN BOTTOM-LEFT. A figure is authored in 0..1 and mapped into
// whatever rect it is drawn into, so the same data serves a binder page, a contact sheet
// cell and eventually a tooltip. Bottom-left because that is the texture convention every
// other buffer in this project uses; mixing the two is how a figure comes out mirrored.
//
// DRAWN INTO A CALLER'S BUFFER, not into a texture of its own. The binder composites a
// figure onto the paper sheet it is printed on, and a figure with its own transparent
// texture would need a second RawImage, its own disposal and its own resize path.

using System.Collections.Generic;
using UnityEngine;
using Strategos.NatoSymbols;

namespace Strategos.Doctrine
{
    /// <summary>What a figure element is.</summary>
    public enum FigureKind
    {
        /// <summary>Friendly land unit. APP-6D frames these as a rectangle.</summary>
        Friendly = 0,

        /// <summary>Hostile land unit. APP-6D frames these as a diamond.</summary>
        Hostile = 1,

        /// <summary>Axis of advance: a route being taken, arrowed.</summary>
        Axis = 2,

        /// <summary>A bound not yet made, or a planned move. Dashed, as APP-6D draws intent.</summary>
        Bound = 3,

        /// <summary>Support by fire: fire delivered onto a position from a flank.</summary>
        SupportByFire = 4,

        /// <summary>Ground to be taken, ringed and named.</summary>
        Objective = 5,

        /// <summary>Ground to be held, drawn as an arc facing the threat.</summary>
        BattlePosition = 6,

        /// <summary>A bare caption. For naming what the geometry cannot say.</summary>
        Note = 7,
    }

    /// <summary>
    /// One thing on a figure.
    /// </summary>
    /// <remarks>
    /// Public mutable fields, not readonly ones, because `FieldsOnlyResolver` skips
    /// `IsInitOnly` members and a readonly field would silently serialise as nothing. Same
    /// contract every other persisted type in this project follows — see ScenarioIO.
    /// </remarks>
    public struct FigureElement
    {
        public FigureKind Kind;

        /// <summary>Normalised 0..1, origin bottom-left. One point for a mark, several for a route.</summary>
        public Vector2[] Points;

        public string Label;

        /// <summary>Echelon mark drawn above a unit frame. Empty for none.</summary>
        public string Echelon;

        public FigureElement(FigureKind kind, Vector2[] points, string label = "",
            string echelon = "") : this()
        {
            Kind = kind;
            Points = points ?? System.Array.Empty<Vector2>();
            Label = label ?? string.Empty;
            Echelon = echelon ?? string.Empty;
        }

        // Constructors named for what they draw, so an authored figure reads as a sketch
        // rather than as an array of magic numbers.

        public static FigureElement Friendly(float x, float y, string label, string echelon = "") =>
            new(FigureKind.Friendly, new[] { new Vector2(x, y) }, label, echelon);

        public static FigureElement Hostile(float x, float y, string label = "ENY") =>
            new(FigureKind.Hostile, new[] { new Vector2(x, y) }, label);

        public static FigureElement Axis(string label, params Vector2[] points) =>
            new(FigureKind.Axis, points, label);

        public static FigureElement Bound(string label, params Vector2[] points) =>
            new(FigureKind.Bound, points, label);

        /// <summary>From a position, onto a target.</summary>
        public static FigureElement SupportByFire(Vector2 from, Vector2 onto) =>
            new(FigureKind.SupportByFire, new[] { from, onto }, "SBF");

        public static FigureElement Objective(float x, float y, string label) =>
            new(FigureKind.Objective, new[] { new Vector2(x, y) }, label);

        public static FigureElement BattlePosition(float x, float y, string label, float facing) =>
            new(FigureKind.BattlePosition, new[] { new Vector2(x, y), new Vector2(facing, 0f) },
                label);

        public static FigureElement Note(float x, float y, string text) =>
            new(FigureKind.Note, new[] { new Vector2(x, y) }, text);
    }

    /// <summary>A drill's figure.</summary>
    public sealed class TtpDiagram
    {
        public FigureElement[] Elements = System.Array.Empty<FigureElement>();

        /// <summary>Printed under the figure. What the reader should take from it.</summary>
        public string Caption = string.Empty;

        public static TtpDiagram Of(string caption, params FigureElement[] elements) =>
            new() { Caption = caption, Elements = elements };
    }

    /// <summary>Draws a <see cref="TtpDiagram"/> into a caller's pixel buffer.</summary>
    public static class TtpDiagramRenderer
    {
        /// <summary>Half-width and half-height of a unit frame, in pixels at a 512 px figure.</summary>
        private const float FrameHalfW = 27f;
        private const float FrameHalfH = 17f;

        /// <summary>
        /// Draws the figure into <paramref name="area"/> of a buffer.
        /// </summary>
        /// <remarks>
        /// Order is fixed and matters, for the same reason MapRasterizer's layer order does:
        /// routes under marks so an arrowhead never covers the unit it belongs to, and labels
        /// last so a caption is never overdrawn by geometry laid down after it.
        /// </remarks>
        public static void Render(TtpDiagram diagram, Color32[] px, int w, int h, RectInt area,
            Color32 ink, Color32 hostile, Color32 muted)
        {
            if (diagram == null || diagram.Elements == null || diagram.Elements.Length == 0) return;

            // One scale for both axes, from the smaller side. Scaling each axis independently
            // would stretch a frame into a different echelon's proportions and turn a right
            // angle in a route into something that is not one.
            float scale = Mathf.Min(area.width, area.height) / 512f;

            var elements = diagram.Elements;

            for (int pass = 0; pass < 3; pass++)
            for (int i = 0; i < elements.Length; i++)
            {
                var e = elements[i];
                bool isRoute = e.Kind is FigureKind.Axis or FigureKind.Bound
                                      or FigureKind.SupportByFire;
                bool isLabel = e.Kind == FigureKind.Note;

                if (pass == 0 && !isRoute) continue;
                if (pass == 1 && (isRoute || isLabel)) continue;
                if (pass == 2 && !isLabel) continue;

                Draw(e, px, w, h, area, scale, ink, hostile, muted);
            }

            if (!string.IsNullOrEmpty(diagram.Caption))
            {
                int cs = Mathf.Max(1, Mathf.RoundToInt(scale * 2f));
                ProceduralDrawUtil.DrawText(px, w, h,
                    area.xMin + area.width / 2, area.yMin + Mathf.RoundToInt(10f * scale),
                    diagram.Caption, muted, cs, TextAlign.Center);
            }
        }

        private static void Draw(in FigureElement e, Color32[] px, int w, int h, RectInt area,
            float scale, Color32 ink, Color32 hostile, Color32 muted)
        {
            int thick = Mathf.Max(1, Mathf.RoundToInt(2f * scale));
            int textScale = Mathf.Max(1, Mathf.RoundToInt(scale * 2f));

            switch (e.Kind)
            {
                case FigureKind.Friendly:
                {
                    var c = Map(e.Points[0], area);
                    DrawFriendlyFrame(px, w, h, c, scale, ink, thick);
                    DrawEchelon(px, w, h, c, scale, e.Echelon, ink, thick);
                    Caption(px, w, h, c, scale, e.Label, ink, textScale, below: true);
                    break;
                }

                case FigureKind.Hostile:
                {
                    var c = Map(e.Points[0], area);
                    // A diamond, because that is how APP-6D frames a hostile land unit. Using
                    // the friendly rectangle in a different colour would make the figure
                    // readable only in colour, and the frame shape is the one thing on a
                    // symbol that must never be repurposed.
                    //
                    // Drawn as a closed polyline rather than through FillDiamond: that one
                    // takes a single `sz` and treats the buffer as square, and a figure buffer
                    // is not. Giving it a (w, h) overload is the documented fix but would edit
                    // a primitive every symbol bake goes through, for one hollow outline here.
                    float dr = FrameHalfW * scale;
                    ProceduralDrawUtil.DrawPolyline(px, w, h, new List<Vector2>
                    {
                        new(c.x, c.y + dr), new(c.x + dr, c.y),
                        new(c.x, c.y - dr), new(c.x - dr, c.y),
                    }, hostile, thick, closed: true);
                    Caption(px, w, h, c, scale, e.Label, hostile, textScale, below: true);
                    break;
                }

                case FigureKind.Axis:
                case FigureKind.Bound:
                {
                    var pts = MapAll(e.Points, area);
                    if (pts.Count < 2) break;

                    var col = e.Kind == FigureKind.Axis ? ink : muted;

                    // Dashed for a move not yet made, solid for one under way — the same
                    // distinction APP-6D draws between an anticipated and a present frame, and
                    // the same one PlayView's order tracks already use.
                    if (e.Kind == FigureKind.Axis)
                        ProceduralDrawUtil.DrawPolyline(px, w, h, pts, col, thick);
                    else
                        ProceduralDrawUtil.DrawDashedPolyline(px, w, h, pts, col, thick,
                            10f * scale, 7f * scale);

                    var tip = pts[pts.Count - 1];
                    var dir = (tip - pts[pts.Count - 2]).normalized;
                    ProceduralDrawUtil.DrawArrowhead(px, w, h, tip, dir, 16f * scale, col);

                    if (!string.IsNullOrEmpty(e.Label))
                    {
                        var mid = pts[pts.Count / 2];
                        ProceduralDrawUtil.DrawText(px, w, h,
                            Mathf.RoundToInt(mid.x), Mathf.RoundToInt(mid.y + 8f * scale),
                            e.Label, col, textScale, TextAlign.Center);
                    }
                    break;
                }

                case FigureKind.SupportByFire:
                {
                    var from = Map(e.Points[0], area);
                    var onto = Map(e.Points[1], area);
                    DrawSupportByFire(px, w, h, from, onto, scale, ink, thick, textScale);
                    break;
                }

                case FigureKind.Objective:
                {
                    var c = Map(e.Points[0], area);
                    int r = Mathf.RoundToInt(34f * scale);
                    ProceduralDrawUtil.DrawCircleOutline(px, w, h,
                        Mathf.RoundToInt(c.x), Mathf.RoundToInt(c.y), r, ink, thick);
                    ProceduralDrawUtil.DrawText(px, w, h,
                        Mathf.RoundToInt(c.x), Mathf.RoundToInt(c.y - 3f * scale),
                        e.Label, ink, textScale, TextAlign.Center);
                    break;
                }

                case FigureKind.BattlePosition:
                {
                    var c = Map(e.Points[0], area);
                    float facing = e.Points.Length > 1 ? e.Points[1].x : 90f;
                    float mid = facing * Mathf.Deg2Rad;
                    ProceduralDrawUtil.DrawArc(px, w, h,
                        Mathf.RoundToInt(c.x), Mathf.RoundToInt(c.y),
                        Mathf.RoundToInt(38f * scale),
                        mid - 1.05f, mid + 1.05f, ink, thick);
                    Caption(px, w, h, c, scale, e.Label, ink, textScale, below: true);
                    break;
                }

                case FigureKind.Note:
                {
                    var c = Map(e.Points[0], area);
                    ProceduralDrawUtil.DrawText(px, w, h,
                        Mathf.RoundToInt(c.x), Mathf.RoundToInt(c.y), e.Label, muted, textScale);
                    break;
                }
            }
        }

        // ─── Marks ────────────────────────────────────────────────────────────

        private static void DrawFriendlyFrame(Color32[] px, int w, int h, Vector2 c, float scale,
            Color32 col, int thick)
        {
            int hw = Mathf.RoundToInt(FrameHalfW * scale);
            int hh = Mathf.RoundToInt(FrameHalfH * scale);
            int cx = Mathf.RoundToInt(c.x), cy = Mathf.RoundToInt(c.y);

            var corners = new[]
            {
                new Vector2(cx - hw, cy - hh), new Vector2(cx + hw, cy - hh),
                new Vector2(cx + hw, cy + hh), new Vector2(cx - hw, cy + hh),
            };
            ProceduralDrawUtil.DrawPolyline(px, w, h, corners, col, thick, closed: true);
        }

        /// <summary>
        /// The echelon mark above a frame.
        /// </summary>
        /// <remarks>
        /// Matches <c>AmplifierDecorator.DrawEchelon</c>, which is the authority: a team is a
        /// circle, a squad one dot, a section two, a platoon three, and a **company one bar**.
        /// The company mark has been "corrected" to two before and it was wrong both times —
        /// see the echelon note in CLAUDE.md before changing it.
        /// </remarks>
        private static void DrawEchelon(Color32[] px, int w, int h, Vector2 c, float scale,
            string echelon, Color32 col, int thick)
        {
            if (string.IsNullOrEmpty(echelon)) return;

            int cy = Mathf.RoundToInt(c.y + (FrameHalfH + 10f) * scale);
            int cx = Mathf.RoundToInt(c.x);
            int dot = Mathf.Max(2, Mathf.RoundToInt(3f * scale));
            int gap = Mathf.RoundToInt(11f * scale);

            switch (echelon.ToLowerInvariant())
            {
                case "team":
                case "crew":
                    ProceduralDrawUtil.DrawCircleOutline(px, w, h, cx, cy, dot + 1, col, thick);
                    break;
                case "squad":
                    ProceduralDrawUtil.FillCircle(px, w, h, cx, cy, dot, col);
                    break;
                case "section":
                    ProceduralDrawUtil.FillCircle(px, w, h, cx - gap / 2, cy, dot, col);
                    ProceduralDrawUtil.FillCircle(px, w, h, cx + gap / 2, cy, dot, col);
                    break;
                case "platoon":
                    ProceduralDrawUtil.FillCircle(px, w, h, cx - gap, cy, dot, col);
                    ProceduralDrawUtil.FillCircle(px, w, h, cx, cy, dot, col);
                    ProceduralDrawUtil.FillCircle(px, w, h, cx + gap, cy, dot, col);
                    break;
                case "company":
                    // ONE bar. It has been "corrected" to two before and was wrong both
                    // times — see the echelon note in CLAUDE.md.
                    ProceduralDrawUtil.DrawLine(px, w, h, cx, cy - dot * 2, cx, cy + dot * 2,
                        col, thick);
                    break;
            }
        }

        /// <summary>
        /// The support-by-fire graphic: a stem from the firing position that splits into two
        /// arrows onto the target.
        /// </summary>
        /// <remarks>
        /// Drawn rather than approximated with a plain arrow because SBF and an assault are
        /// different orders and a figure that renders them alike teaches the wrong thing —
        /// which element shoots and which one moves is the whole content of most drills.
        /// </remarks>
        private static void DrawSupportByFire(Color32[] px, int w, int h, Vector2 from,
            Vector2 onto, float scale, Color32 col, int thick, int textScale)
        {
            Vector2 dir = (onto - from);
            float len = dir.magnitude;
            if (len < 1f) return;
            dir /= len;

            Vector2 perp = new(-dir.y, dir.x);
            Vector2 fork = from + dir * (len * 0.45f);

            // Proportional to the run, not a fixed pixel amount. A constant spread vanishes
            // over a long shot — the first figures drew SBF as two nearly parallel lines,
            // which is exactly what the graphic exists to not look like.
            float spread = Mathf.Max(22f * scale, len * 0.30f);

            ProceduralDrawUtil.DrawPolyline(px, w, h, new List<Vector2> { from, fork },
                col, thick);

            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 tip = onto + perp * spread * s * 0.5f;
                ProceduralDrawUtil.DrawPolyline(px, w, h,
                    new List<Vector2> { fork, tip }, col, thick);
                ProceduralDrawUtil.DrawArrowhead(px, w, h, tip, (tip - fork).normalized,
                    13f * scale, col);
            }

            ProceduralDrawUtil.DrawText(px, w, h,
                Mathf.RoundToInt(from.x), Mathf.RoundToInt(from.y - 16f * scale),
                "SBF", col, textScale, TextAlign.Center);
        }

        private static void Caption(Color32[] px, int w, int h, Vector2 c, float scale,
            string text, Color32 col, int textScale, bool below)
        {
            if (string.IsNullOrEmpty(text)) return;
            int y = Mathf.RoundToInt(c.y + (below ? -(FrameHalfH + 16f) * scale
                                                  : (FrameHalfH + 8f) * scale));
            ProceduralDrawUtil.DrawText(px, w, h, Mathf.RoundToInt(c.x), y, text, col,
                textScale, TextAlign.Center);
        }

        // ─── Mapping ──────────────────────────────────────────────────────────

        private static Vector2 Map(Vector2 normalised, RectInt area) => new(
            area.xMin + normalised.x * area.width,
            area.yMin + normalised.y * area.height);

        private static List<Vector2> MapAll(IReadOnlyList<Vector2> points, RectInt area)
        {
            var list = new List<Vector2>(points.Count);
            for (int i = 0; i < points.Count; i++) list.Add(Map(points[i], area));
            return list;
        }
    }
}
