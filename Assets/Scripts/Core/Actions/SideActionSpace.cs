// SideActionSpace.cs
// #102: fixed macro-action vocabulary for a side — drills plus ADVANCE, never raw cells.
//
// Cell-addressed MoveTo is combinatorially intractable on a 256² map. The shipped doctrine
// pack already names twelve coded drills; an agent choosing among those learns doctrine.
// ADVANCE is the one non-drill companion: MoveTo the nearest unheld objective, the same
// target SideDirector already computes — still not a free cell pick.
//
// Separate from ISidePolicy / SideKnowledge (#100). Wiring into env Step is #104.

using System;
using UnityEngine;
using Strategos.Commands;
using Strategos.Doctrine;
using Strategos.Objectives;
using Strategos.Units;

namespace Strategos.Actions
{
    /// <summary>
    /// Ordered action indices shared by <see cref="SideActionMask"/> and
    /// <see cref="TryToCommand"/>. Length is always <see cref="Count"/>.
    /// </summary>
    public static class SideActionSpace
    {
        /// <summary>Sentinel code for the ADVANCE slot — not a TtpLibrary entry.</summary>
        public const string AdvanceCode = "ADVANCE";

        /// <summary>Index of ADVANCE (last slot).</summary>
        public const int AdvanceIndex = 12;

        /// <summary>Twelve drills + ADVANCE.</summary>
        public const int Count = 13;

        /// <summary>
        /// Drill codes in library order. Built once from <see cref="TtpLibrary.All"/> so the
        /// vocabulary tracks the shipped pack rather than a second hard-coded list.
        /// </summary>
        private static string[] _drillCodes;

        private static string[] DrillCodes => _drillCodes ??= BuildDrillCodes();

        private static string[] BuildDrillCodes()
        {
            var all = TtpLibrary.All;
            if (all == null || all.Count == 0)
                throw new InvalidOperationException("TtpLibrary has no drills — cannot build action space");

            // Vocabulary is fixed at Count-1 drills. If the pack grows, take the first twelve
            // in pack order; if it shrinks, fail loudly rather than silently pad.
            if (all.Count < AdvanceIndex)
                throw new InvalidOperationException(
                    $"TtpLibrary has {all.Count} drills; action space expects {AdvanceIndex}");

            var codes = new string[AdvanceIndex];
            for (int i = 0; i < AdvanceIndex; i++)
                codes[i] = all[i].Code;
            return codes;
        }

        /// <summary>Drop cached codes after a library reload (editor / probes).</summary>
        public static void Reload() => _drillCodes = null;

        public static bool IsAdvance(int index) => index == AdvanceIndex;

        public static bool IsValidIndex(int index) => (uint)index < (uint)Count;

        /// <summary>Drill code, or <see cref="AdvanceCode"/> for ADVANCE.</summary>
        public static string CodeAt(int index)
        {
            if (!IsValidIndex(index))
                throw new ArgumentOutOfRangeException(nameof(index), index, $"0..{Count - 1}");
            if (IsAdvance(index)) return AdvanceCode;
            return DrillCodes[index];
        }

        /// <summary>
        /// Build the command for a legal action. Returns false when ADVANCE has no unheld
        /// objective (callers should respect the mask; this is the last-line guard).
        /// </summary>
        public static bool TryToCommand(int index, ActorId by, UnitInstance unit,
            VictoryEvaluator victory, out Command command)
        {
            command = default;
            if (unit == null || !IsValidIndex(index)) return false;

            if (IsAdvance(index))
            {
                var objective = NearestUnheld(unit, victory);
                if (objective == null) return false;
                command = Command.MoveTo(by, unit.Id, objective.Cell);
                return true;
            }

            command = Command.Drill(by, unit.Id, CodeAt(index));
            return true;
        }

        /// <summary>
        /// Closest objective this unit's side does not hold, or null.
        /// Same rule as SideDirector's private NearestUnheld (authored order, strict &lt; ties).
        /// </summary>
        public static Objective NearestUnheld(UnitInstance unit, VictoryEvaluator victory)
        {
            if (unit == null || victory?.Objectives == null) return null;

            Objective best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < victory.Objectives.Count; i++)
            {
                if (victory.OwnerOfIndex(i) == unit.Side) continue;

                var objective = victory.Objectives[i];
                float d = Vector2.Distance(unit.Cell, objective.Cell);
                if (d < bestDistance) { bestDistance = d; best = objective; }
            }

            return best;
        }
    }
}
