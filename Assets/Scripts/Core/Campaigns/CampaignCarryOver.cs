// CampaignCarryOver.cs
// #75 chunk 2: given a Simulation that just finished an operation, produce the next entry's
// carried-over state. Behaviour, deliberately kept out of CampaignChain.cs — that file's own
// header says "nothing here computes anything", and this is the thing that computes.
//
// PURE FUNCTION. No mutation of the Simulation passed in, no side effects beyond the two output
// writes it exists to make: the finished entry's Outcome, and the next entry's CarriedOverUnits.
//
// SIGNATURE NOTE: the #75 chunk-2 brief wrote this as `CarryOver(Simulation, CampaignChainEntry,
// float) -> populates finishedEntry.Outcome and the NEXT entry's CarriedOverUnits`, but that
// leaves "the next entry" with no parameter to be. A pure function's outputs have to be explicit
// — reaching into an unstated CampaignChain/index would be a side channel, not a data-in/data-out
// call. So CarryOver below takes nextEntry explicitly. See the chunk-2 handoff for this being
// flagged back.
//
// WHAT THIS CHUNK DELIBERATELY DOES NOT DO:
//   - Defeat-cost logic. Outcome.Lost is recorded; what losing costs the campaign is unimplemented.
//   - The multi-operation probe (three linked operations played end to end).
//   - Anything under Assets/Scripts/UI or PlayView — no wiring into the play flow.

using System;
using System.Collections.Generic;
using Strategos.Commands;
using Strategos.Objectives;
using Strategos.Units;
using UnityEngine;

namespace Strategos.Campaigns
{
    public static class CampaignCarryOver
    {
        /// <summary>
        /// Populates <paramref name="finishedEntry"/>.Outcome from how
        /// <paramref name="endedSim"/> actually ended, and <paramref name="nextEntry"/>'s
        /// CarriedOverUnits from its surviving ORBAT, readiness partially recovered over
        /// <paramref name="restHours"/>.
        /// </summary>
        /// <remarks>
        /// Strength, casualties (via <see cref="UnitInstance.Strength"/>), Training, Roe,
        /// Supply and identity fields carry over unchanged. Destroyed units (wrecks) are
        /// excluded entirely — see <see cref="BuildCarriedOverUnits"/>. Cell is reset to
        /// <see cref="Vector2.zero"/> and Posture to <see cref="Posture.Halted"/>; Readiness is
        /// the one field this function actually computes, via <see cref="FatigueModel.Apply"/>
        /// (the exact recovery formula at FatigueModel.cs:89, reused rather than duplicated).
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Any of <paramref name="endedSim"/>, <paramref name="finishedEntry"/> or
        /// <paramref name="nextEntry"/> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// <paramref name="endedSim"/> has no decided outcome (no <c>VictoryEvaluator</c> at
        /// all, or one that has not decided). Passing an undecided simulation is a caller
        /// error — this function throws rather than guessing an outcome.
        /// </exception>
        public static void CarryOver(Simulation endedSim, CampaignChainEntry finishedEntry,
            CampaignChainEntry nextEntry, float restHours)
        {
            if (endedSim == null) throw new ArgumentNullException(nameof(endedSim));
            if (finishedEntry == null) throw new ArgumentNullException(nameof(finishedEntry));
            if (nextEntry == null) throw new ArgumentNullException(nameof(nextEntry));

            finishedEntry.Outcome = DetermineOutcome(endedSim);
            nextEntry.CarriedOverUnits = BuildCarriedOverUnits(endedSim, restHours);
        }

        /// <summary>
        /// Maps <see cref="Simulation.Victory"/>'s <see cref="ScenarioOutcome"/> against
        /// <see cref="Scenario.PlayerSide"/> — see VictoryEvaluator.cs:26-42 and Scenario.cs:64.
        /// </summary>
        private static OperationOutcome DetermineOutcome(Simulation endedSim)
        {
            if (endedSim.Victory == null || !endedSim.Victory.Outcome.Decided)
                throw new InvalidOperationException(
                    "CarryOver requires a decided simulation — endedSim.Victory is null or " +
                    "endedSim.Victory.Outcome.Decided is false. An undecided operation has " +
                    "nothing to carry over; the caller must wait for a decision, not guess one.");

            var outcome = endedSim.Victory.Outcome;

            if (outcome.IsDraw) return OperationOutcome.Drew;
            if (outcome.Winner == endedSim.Scenario.PlayerSide) return OperationOutcome.Won;
            return OperationOutcome.Lost;
        }

        /// <summary>
        /// Clones every surviving unit out of <paramref name="endedSim"/>, excluding wrecks,
        /// resets what does not carry meaningfully into a new operation, and recovers readiness
        /// over <paramref name="restHours"/> of rest.
        /// </summary>
        private static List<UnitInstance> BuildCarriedOverUnits(
            Simulation endedSim, float restHours)
        {
            var result = new List<UnitInstance>();
            float restSeconds = restHours * 3600f;

            foreach (var unit in endedSim.Units)
            {
                // A wreck does not walk into the next operation. Simulation.Snapshot() adds
                // every unit in _units unconditionally (Simulation.cs:961) — destroyed units
                // included, because a save needs to remember a wreck happened. A campaign
                // carry-over is a different question: only a unit that can still fight goes
                // forward. This filter is the whole reason this function exists rather than
                // just reusing Snapshot() directly.
                if (unit.IsDestroyed) continue;

                var carried = unit.Clone();

                // A unit does not arrive at the next operation still moving or mid-order — the
                // next operation's Simulation has not started yet, so there is no order to be
                // mid-way through.
                carried.Posture = Posture.Halted;

                // The ended map is not the next operation's map — a Cell here would be a
                // coordinate on ground that no longer exists for this ORBAT. Reset to the
                // sentinel rather than leaving a real-looking but meaningless position: the
                // next operation's scenario places units at its own authored start cells, and
                // whoever writes that placement logic must not be tempted to trust this field.
                carried.Cell = Vector2.zero;

                // Readiness recovery: FatigueModel.Apply with engaged=false and Posture already
                // reset off Moving takes exactly the "resting" branch (FatigueModel.cs:86-89),
                // which is the exact formula this chunk was told to reuse rather than
                // duplicate. map is null because the terrain multiplier is only read when cost
                // > 0 (moving or engaged), neither of which applies here.
                FatigueModel.Apply(carried, engaged: false, map: null, endedSim.Catalogue,
                    restSeconds);

                result.Add(carried);
            }

            return result;
        }
    }
}
