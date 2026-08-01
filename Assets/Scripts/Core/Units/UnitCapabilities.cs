// UnitCapabilities.cs
// What a *type* of unit can do. Static, shared by every instance of that type.
//
// The split this file exists to draw:
//
//   Capability  static, per type.     Every rifle company has the same top speed.
//   State       runtime, per instance. THIS company is at 60% and low on ammunition.
//
// Two units of the same type share one capability object and have their own state, which
// lives on UnitInstance. Getting this boundary right now matters because movement,
// engagement and AI will all read from it.
//
// UNITS ARE METRES AND SECONDS, NEVER CELLS.
// A map can be generated at 5 m or 100 m per cell, so a speed expressed in cells means
// something different on each one. Everything here is metres, metres per second or degrees,
// and converts through MapHeader.MetresPerCell at the point of use.
//
// Not a ScriptableObject, deliberately. Plain serialisable data stays testable headlessly
// and loadable from JSON alongside the scenario; an asset pipeline can wrap it later if
// authoring in the Inspector ever becomes worth it.

using System;
using System.Collections.Generic;
using UnityEngine;
using Strategos.Maps;

namespace Strategos.Units
{
    /// <summary>What a unit is doing, which modifies what it can do.</summary>
    public enum Posture
    {
        /// <summary>Halted in the open, no preparation.</summary>
        Halted = 0,
        Moving = 1,
        /// <summary>Prepared position. Slower to leave, harder to hurt.</summary>
        DugIn = 2,
    }

    /// <summary>
    /// The consumable classes tracked per unit, as percentages of a full load.
    ///
    /// Four fixed fields rather than a dictionary keyed by an enum: the set does not change
    /// at runtime, and fields serialise cleanly and cost nothing to read in a hot loop.
    /// </summary>
    [Serializable]
    public struct SupplyLevels
    {
        [Range(0, 100)] public float Rations;
        [Range(0, 100)] public float Water;
        [Range(0, 100)] public float Ammunition;
        [Range(0, 100)] public float Fuel;

        public static SupplyLevels Full => new()
        { Rations = 100f, Water = 100f, Ammunition = 100f, Fuel = 100f };

        /// <summary>Lowest class, which is what actually constrains a unit.</summary>
        public float Lowest => Mathf.Min(Mathf.Min(Rations, Water), Mathf.Min(Ammunition, Fuel));

        public void Clamp()
        {
            Rations = Mathf.Clamp(Rations, 0f, 100f);
            Water = Mathf.Clamp(Water, 0f, 100f);
            Ammunition = Mathf.Clamp(Ammunition, 0f, 100f);
            Fuel = Mathf.Clamp(Fuel, 0f, 100f);
        }

        public override string ToString() =>
            $"R{Rations:0} W{Water:0} A{Ammunition:0} F{Fuel:0}";
    }

    /// <summary>Consumption per hour, as percentage points of a full load.</summary>
    [Serializable]
    public struct ConsumptionRates
    {
        public float RationsPerHour;
        public float WaterPerHour;
        /// <summary>Only spent while engaging; see the note on <see cref="UnitCapabilities"/>.</summary>
        public float AmmunitionPerHourEngaged;
        /// <summary>Only spent while moving.</summary>
        public float FuelPerHourMoving;
    }

    /// <summary>
    /// How fast a unit tires and recovers, in readiness points per hour.
    /// </summary>
    /// <remarks>
    /// Shaped and named like <see cref="ConsumptionRates"/> deliberately: readiness is a
    /// consumable in everything but name, spent by the same activities and restored by rest
    /// instead of resupply. Per hour rather than per tick for the same reason as supply — an
    /// authored number a human can reason about, converted once at the point of use.
    ///
    /// Recovery is deliberately slower than either cost. If a unit refilled as fast as it
    /// emptied, fatigue would be a resource you top up rather than one you spend, and pacing
    /// a force over a long scenario would stop being a decision.
    /// </remarks>
    [Serializable]
    public struct FatigueRates
    {
        /// <summary>Spent while <see cref="Posture.Moving"/>, scaled by how hard the going is.</summary>
        public float PerHourMoving;

        /// <summary>Spent while firing or being fired at. Combat is the expensive one.</summary>
        public float PerHourEngaged;

        /// <summary>Regained while halted and out of contact.</summary>
        public float RecoveryPerHourResting;
    }

    [Serializable]
    public sealed class UnitCapabilities
    {
        /// <summary>Stable key, referenced by <see cref="UnitInstance.CapabilityId"/>.</summary>
        public string Id = "generic";

        public string Name = "Generic Unit";

        // ─── Movement ─────────────────────────────────────────────────────────

        /// <summary>Sustained speed off-road on level going, metres per second.</summary>
        public float CrossCountrySpeedMps = 1.2f;

        /// <summary>Sustained speed on a road, metres per second.</summary>
        public float RoadSpeedMps = 1.6f;

        /// <summary>
        /// Steepest ground the unit can traverse at all, in degrees. Beyond this the cell is
        /// impassable, not merely slow — which is what makes a ridge a genuine obstacle
        /// rather than a detour with a cost.
        /// </summary>
        public float MaxClimbDegrees = 30f;

        /// <summary>
        /// Landcover this unit cannot enter. Water is the usual entry; wheeled units add
        /// marsh and forest where tracked ones do not.
        /// </summary>
        public LandcoverClass[] ImpassableLandcover = Array.Empty<LandcoverClass>();

        /// <summary>
        /// Speed multiplier per landcover class, keyed by <c>(int)LandcoverClass</c>. Missing
        /// or empty means 1.0 everywhere. Values below 1 slow the unit down.
        /// </summary>
        public float[] LandcoverSpeedFactor = Array.Empty<float>();

        // ─── Observation ──────────────────────────────────────────────────────

        /// <summary>
        /// How far the unit can detect, in metres, on open level ground.
        ///
        /// A radius, not line of sight. Terrain LOS — ridgelines blocking, elevation
        /// advantage — does not exist anywhere in the project yet (a Phase 1 / M1 item), so
        /// <see cref="DetectionRangeAt"/> is written as the single call to swap when it does.
        /// </summary>
        public float DetectionRangeMetres = 1500f;

        /// <summary>Detection multiplier when the observer stands in this landcover.</summary>
        public float DetectionInForestFactor = 0.4f;

        // ─── Weapons ──────────────────────────────────────────────────────────

        /// <summary>Furthest the unit can engage, in metres.</summary>
        public float EngagementRangeMetres = 600f;

        /// <summary>Damage output per minute against an unprotected target, arbitrary units.</summary>
        public float Firepower = 10f;

        /// <summary>Damage absorbed. Higher means harder to hurt.</summary>
        public float Protection = 1f;

        // ─── Sustainment ──────────────────────────────────────────────────────

        public ConsumptionRates Consumption = new()
        {
            RationsPerHour = 0.6f,
            WaterPerHour = 1.2f,
            AmmunitionPerHourEngaged = 25f,
            FuelPerHourMoving = 6f,
        };

        // ─── Queries ──────────────────────────────────────────────────────────
        //
        // These are the seam the pathfinder in #8 reads, so climb limits and impassable
        // cover live here once rather than being hard-coded there.

        /// <summary>
        /// Whether this unit can occupy a cell at all. Impassable landcover and slopes past
        /// <see cref="MaxClimbDegrees"/> are hard blocks, not costs.
        /// </summary>
        public bool CanEnter(LandcoverClass cover, float slopeDegrees)
        {
            if (slopeDegrees > MaxClimbDegrees) return false;
            if (ImpassableLandcover != null)
                foreach (var c in ImpassableLandcover)
                    if (c == cover) return false;
            return true;
        }

        /// <summary>
        /// Speed in metres per second over the given going, or 0 if impassable.
        ///
        /// Slope scales linearly to zero at the climb limit: ground at the edge of what a
        /// unit can climb should crawl, not run.
        /// </summary>
        /// <summary>
        /// How quickly this unit tires. Zero rates mean a unit that never tires, which is
        /// what an unconfigured type gets — see <see cref="UnitCatalogue"/>.
        /// </summary>
        public FatigueRates Fatigue;

        public float SpeedMps(LandcoverClass cover, float slopeDegrees, bool onRoad)
        {
            if (!CanEnter(cover, slopeDegrees)) return 0f;

            float speed = onRoad ? RoadSpeedMps : CrossCountrySpeedMps;
            if (!onRoad) speed *= LandcoverFactor(cover);

            if (MaxClimbDegrees > 0.01f)
                speed *= Mathf.Clamp01(1f - slopeDegrees / MaxClimbDegrees * 0.85f);

            return Mathf.Max(0f, speed);
        }

        /// <summary>Seconds to cross one cell of the given going on this map.</summary>
        public float SecondsPerCell(MapHeader header, LandcoverClass cover,
            float slopeDegrees, bool onRoad)
        {
            float mps = SpeedMps(cover, slopeDegrees, onRoad);
            if (mps <= 0.0001f) return float.PositiveInfinity;
            return header.MetresPerCell / mps;
        }

        public float LandcoverFactor(LandcoverClass cover)
        {
            int i = (int)cover;
            if (LandcoverSpeedFactor == null || i < 0 || i >= LandcoverSpeedFactor.Length)
                return 1f;
            float f = LandcoverSpeedFactor[i];
            return f <= 0f ? 1f : f;
        }

        /// <summary>
        /// Detection range in metres from a unit standing in the given landcover.
        ///
        /// **The single call to replace when terrain LOS lands.** Everything that asks "can
        /// this unit see that one" should route through here rather than comparing distances
        /// itself, so LOS becomes one change and not a search.
        /// </summary>
        public float DetectionRangeAt(LandcoverClass observerCover)
        {
            float range = DetectionRangeMetres;
            if (observerCover == LandcoverClass.Forest) range *= DetectionInForestFactor;
            return range;
        }

        public override string ToString() =>
            $"{Id} '{Name}' {CrossCountrySpeedMps:0.#}/{RoadSpeedMps:0.#} m/s, " +
            $"climb {MaxClimbDegrees:0}deg, see {DetectionRangeMetres:0} m, " +
            $"engage {EngagementRangeMetres:0} m";
    }
}
