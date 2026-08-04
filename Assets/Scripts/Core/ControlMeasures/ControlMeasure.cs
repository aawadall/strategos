// ControlMeasure.cs
// #161: an authored graphic control measure — definition only, no runtime state.
//
// Same shape as Objective: plain [Serializable], scenario JSON, round-tripped by ScenarioIO.
// Not MapData — MapPolyline / MapPoiKind are generator terrain; a control measure is a side's
// plan (see epic #160). Geometry is point (Cell) and/or polyline (Points); which fields a
// kind reads is documented on ControlMeasureKind and enforced in Scenario.Validate.

using System;
using System.Collections.Generic;
using UnityEngine;
using Strategos.NatoSymbols;
using Strategos.Units;

namespace Strategos.ControlMeasures
{
    [Serializable]
    public sealed class ControlMeasure
    {
        /// <summary>Stable id within the scenario.</summary>
        public int Id;

        public ControlMeasureKind Kind = ControlMeasureKind.Checkpoint;

        /// <summary>Briefing label — "CP 1", "PL AMBER", "AXIS BLUE".</summary>
        public string Name = "Control Measure";

        /// <summary>Owning side. <see cref="SideId.None"/> means shared / unowned.</summary>
        public SideId Owner;

        /// <summary>
        /// Point geometry (checkpoint centre). Unused for pure polylines when
        /// <see cref="Points"/> has two or more vertices.
        /// </summary>
        public Vector2 Cell;

        /// <summary>
        /// Optional radius in cells for a checkpoint — same idea as
        /// <see cref="Objectives.Objective.RadiusCells"/>, for Contains tests and draw size.
        /// Ignored for line / arrow / area kinds.
        /// </summary>
        public float RadiusCells = 3f;

        /// <summary>
        /// Polyline / polygon vertices in cell coordinates. Phase lines and arrows need
        /// ≥2; areas ≥3. Empty for a checkpoint that only uses <see cref="Cell"/>.
        /// </summary>
        public List<Vector2> Points = new();

        /// <summary>
        /// Boundary echelon tick mark. <see cref="Echelon.None"/> for other kinds —
        /// ignored when drawing those.
        /// </summary>
        public Echelon Echelon = Echelon.None;

        /// <summary>
        /// Axis-of-advance role (#164). Ignored unless <see cref="Kind"/> is
        /// <see cref="ControlMeasureKind.AxisOfAdvance"/>.
        /// </summary>
        public AxisOfAdvanceRole AxisRole = AxisOfAdvanceRole.Main;

        public bool IsPointKind => Kind == ControlMeasureKind.Checkpoint;

        public bool IsLineKind =>
            Kind == ControlMeasureKind.PhaseLine || Kind == ControlMeasureKind.Boundary;

        public bool IsArrowKind =>
            Kind == ControlMeasureKind.AxisOfAdvance ||
            Kind == ControlMeasureKind.DirectionOfAttack ||
            Kind == ControlMeasureKind.Retirement ||
            Kind == ControlMeasureKind.Counterattack;

        public bool IsAreaKind =>
            Kind == ControlMeasureKind.BattlePosition ||
            Kind == ControlMeasureKind.EngagementArea ||
            Kind == ControlMeasureKind.KillZone;

        public bool Contains(Vector2 cell) =>
            IsPointKind && (cell - Cell).sqrMagnitude <= RadiusCells * RadiusCells;

        public override string ToString() =>
            IsPointKind
                ? $"[{Id}] {Kind} '{Name}' @ ({Cell.x:0},{Cell.y:0})"
                : $"[{Id}] {Kind} '{Name}' pts={Points?.Count ?? 0}";
    }
}
