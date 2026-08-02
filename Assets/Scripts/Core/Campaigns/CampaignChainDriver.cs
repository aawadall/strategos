// CampaignChainDriver.cs
// #75 chunk 3: turns a CampaignChain's data (chunk 1) and CampaignCarryOver's output (chunk 2)
// into an actually-playable next operation. Nothing before this chunk consumed
// CampaignChainEntry.CarriedOverUnits at all — Simulation's constructor (Simulation.cs:175-181)
// builds its UnitHierarchy directly from scenario.Units, and there was no seam for "start this
// scenario, but substitute these carried-over units for its own authored starting ORBAT". This
// file is that seam.
//
// THE Id-CONSISTENCY AUTHORING RULE THIS DEPENDS ON. UnitId (Assets/Scripts/Core/Units/UnitIds.cs)
// is a plain hand-authored int, assigned per-scenario by whoever writes the JSON — there is no
// global uniqueness guarantee across different scenario files. Merging carried-over state into a
// new scenario by matching Id is only correct if a campaign's own scenarios are authored to keep
// the same persistent unit's Id consistent across every operation in the chain. That is a new
// authoring rule this chunk introduces; see docs/campaign-invariants.md, which states it the same
// way docs/simulation-invariants.md states rules an author must respect.
//
// WHAT MERGES AND WHAT DOES NOT, per matched unit: Strength, Readiness, Training, Roe and
// Supply.* come from the carried-over unit (the same field list CampaignCarryOver.CarryOver
// carries into CarriedOverUnits in the first place). Cell and ParentId always come from the next
// scenario's own authored placement — the new operation's map and ORBAT are authoritative for
// where a unit starts and who it reports to, and CarriedOverUnits.Cell is Vector2.zero, a
// sentinel (see CampaignCarryOver.cs), never a real position. Everything else on the authored
// unit (Sidc, Designation, HigherFormation, CapabilityId, Side, Posture, Suppression,
// DestroyedAtTick) is left exactly as the next scenario authored it — not in the merge list, and
// a carried-over unit's Posture is always Halted / Suppression is whatever it was at the end of
// the last operation anyway, neither of which is a "next operation's own state" concept.
//
// AN UNMATCHED CARRIED-OVER UNIT THROWS RATHER THAN BEING DROPPED. See MergeCarriedOver's own
// remarks for the reasoning — briefly, this mirrors CampaignCarryOver.CarryOver's own precedent
// (an undecided Simulation is a caller error and throws rather than guessing), and it keeps this
// chunk's given signature (Scenario MergeCarriedOver(Scenario, List<UnitInstance>)) intact rather
// than adding an out-parameter or changing the return type to carry a problem list alongside it.
//
// WHAT THIS CHUNK DELIBERATELY DOES NOT DO — see the #75 chunk-3 brief and
// Artifacts/agents/campaign-merge-out.md for the reasoning:
//   - The three-operation acceptance probe #75 itself asks for. This proves a real
//     TWO-operation round trip; a third operation is the next chunk's job.
//   - Anything under Assets/Scripts/UI. No PLAY wiring.
//   - Defeat-cost logic beyond choosing a shorter restHours for a Lost outcome when calling
//     CarryOver — CarryOver already takes restHours as a caller-supplied parameter, so that is
//     a one-line driver decision, not a feature added here.

using System;
using System.Collections.Generic;
using Strategos.Commands;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Campaigns
{
    public static class CampaignChainDriver
    {
        /// <summary>
        /// Merges <paramref name="carriedOverUnits"/> into a copy of
        /// <paramref name="nextScenario"/>: for each of the new scenario's own authored units,
        /// a carried-over unit sharing its <see cref="UnitId"/> replaces
        /// <see cref="UnitInstance.Strength"/>, <see cref="UnitInstance.Readiness"/>,
        /// <see cref="UnitInstance.Training"/>, <see cref="UnitInstance.Roe"/> and
        /// <see cref="UnitInstance.Supply"/> — <see cref="UnitInstance.Cell"/> and
        /// <see cref="UnitInstance.ParentId"/> are always kept from the next scenario's own
        /// authoring, never from the carried-over unit (whose Cell is a Vector2.zero sentinel —
        /// see <see cref="CampaignCarryOver"/>). A next-scenario unit with no carried-over match
        /// keeps its own authored state entirely: that is how a new operation introduces
        /// reinforcements.
        /// </summary>
        /// <remarks>
        /// Pure function: does not mutate <paramref name="nextScenario"/> or any of its units.
        /// <see cref="Scenario"/> has no existing Clone/copy path (checked — ScenarioIO only
        /// round-trips through JSON, which is a heavier and lossier way to get a copy than this
        /// needs), so this builds a new <see cref="Scenario"/> sharing every field it does not
        /// touch (Map, Sides, Objectives, Victory, Directive — none of which this function
        /// reads or writes) and a new <see cref="Scenario.Units"/> list of cloned, merged
        /// <see cref="UnitInstance"/>s.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="nextScenario"/> or <paramref name="carriedOverUnits"/> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// A carried-over unit's <see cref="UnitId"/> has no match anywhere in
        /// <paramref name="nextScenario"/>.Units — the unit that survived has nowhere to go in
        /// the next operation. This is a campaign authoring error (see the file header's
        /// Id-consistency rule), not a runtime possibility to degrade gracefully from: silently
        /// dropping the survivor would be indistinguishable from it simply vanishing, which is
        /// worse than a loud failure naming exactly which unit and which scenario. Every
        /// mismatched id is collected before throwing, not just the first, the same way
        /// <see cref="Scenario.Validate"/> reports every problem in one pass rather than
        /// stopping at the first.
        /// </exception>
        public static Scenario MergeCarriedOver(Scenario nextScenario,
            List<UnitInstance> carriedOverUnits)
        {
            if (nextScenario == null) throw new ArgumentNullException(nameof(nextScenario));
            if (carriedOverUnits == null) throw new ArgumentNullException(nameof(carriedOverUnits));

            var carriedById = new Dictionary<UnitId, UnitInstance>();
            foreach (var carried in carriedOverUnits) carriedById[carried.Id] = carried;

            var matched = new HashSet<UnitId>();
            var mergedUnits = new List<UnitInstance>(nextScenario.Units.Count);

            foreach (var authored in nextScenario.Units)
            {
                // Clone first: authored's own Cell, ParentId and every other field this
                // function does not touch survive unchanged, which is what "reinforcements
                // keep their own authored state entirely" and "Cell/ParentId always come from
                // the new scenario" both fall out of for free.
                var merged = authored.Clone();

                if (carriedById.TryGetValue(authored.Id, out var carried))
                {
                    merged.Strength = carried.Strength;
                    merged.Readiness = carried.Readiness;
                    merged.Training = carried.Training;
                    merged.Roe = carried.Roe;
                    merged.Supply = carried.Supply;
                    matched.Add(authored.Id);
                }

                mergedUnits.Add(merged);
            }

            List<string> unmatched = null;
            foreach (var carried in carriedOverUnits)
            {
                if (matched.Contains(carried.Id)) continue;
                unmatched ??= new List<string>();
                unmatched.Add($"{carried.Id} ('{carried.Designation}')");
            }

            if (unmatched != null)
                throw new InvalidOperationException(
                    $"MergeCarriedOver: {unmatched.Count} carried-over unit(s) have no match " +
                    $"in next scenario '{nextScenario.Name}': {string.Join(", ", unmatched)}. " +
                    "A unit that survived has nowhere to go in the next operation — every " +
                    "scenario in a CampaignChain must keep a persistent unit's Id consistent " +
                    "across every operation it appears in (see docs/campaign-invariants.md).");

            return new Scenario
            {
                FormatVersion = nextScenario.FormatVersion,
                Name = nextScenario.Name,
                Description = nextScenario.Description,
                Map = nextScenario.Map,
                Sides = nextScenario.Sides,
                Units = mergedUnits,
                PlayerSide = nextScenario.PlayerSide,
                Objectives = nextScenario.Objectives,
                Victory = nextScenario.Victory,
                TimeLimitTicks = nextScenario.TimeLimitTicks,
                Directive = nextScenario.Directive,
            };
        }

        /// <summary>
        /// Loads <c>chain.Operations[entryIndex]</c>'s scenario (the same resolution
        /// <see cref="ScenarioIO.Load"/> gives any other caller — see chunk 1's own doc on
        /// <see cref="CampaignChainEntry.ScenarioName"/>), merges in the entry's own
        /// <see cref="CampaignChainEntry.CarriedOverUnits"/> if it has any, generates the map
        /// the same way <see cref="Scenario.GenerateMap"/> always does, and constructs the
        /// <see cref="Simulation"/>.
        /// </summary>
        /// <remarks>
        /// <b>Reads <c>chain.Operations[entryIndex].CarriedOverUnits</c>, not
        /// <c>chain.Operations[entryIndex - 1]</c>'s.</b> The #75 chunk-3 brief's prose said
        /// the latter, but <see cref="CampaignChainEntry.CarriedOverUnits"/>'s own doc comment
        /// (chunk 1) and <see cref="CampaignCarryOver.CarryOver"/>'s actual behaviour (chunk 2:
        /// it writes into its <c>nextEntry</c> parameter) both already establish that a survivor
        /// pool lands on the entry it feeds *into*, not the one it came *out of* — an entry's
        /// own <c>CarriedOverUnits</c> already means "what this operation starts with". Reading
        /// <c>entryIndex - 1</c> instead would read operation <c>entryIndex - 1</c>'s *own*
        /// starting ORBAT (empty for the first entry in a chain, and someone else's survivors
        /// for every other one) rather than what it produced at the end — flagged back in
        /// Artifacts/agents/campaign-merge-out.md rather than silently "fixed" without a trace.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="chain"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="entryIndex"/> is not a valid index into <c>chain.Operations</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The entry's <see cref="CampaignChainEntry.ScenarioName"/> does not resolve to a
        /// shipped scenario.
        /// </exception>
        public static Simulation StartNext(CampaignChain chain, int entryIndex,
            UnitCatalogue catalogue = null)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (entryIndex < 0 || entryIndex >= chain.Operations.Count)
                throw new ArgumentOutOfRangeException(nameof(entryIndex),
                    $"{entryIndex} is not a valid operation index; chain has " +
                    $"{chain.Operations.Count} operation(s).");

            var entry = chain.Operations[entryIndex];
            var scenario = ScenarioIO.Load(entry.ScenarioName);
            if (scenario == null)
                throw new InvalidOperationException(
                    $"StartNext: operation {entryIndex} names scenario '{entry.ScenarioName}', " +
                    $"which did not load from Resources/{ScenarioIO.ResourceFolder}.");

            bool hasCarriedOver = entryIndex > 0 &&
                entry.CarriedOverUnits != null && entry.CarriedOverUnits.Count > 0;

            if (hasCarriedOver)
                scenario = MergeCarriedOver(scenario, entry.CarriedOverUnits);

            var map = scenario.GenerateMap();
            return new Simulation(scenario, map, catalogue);
        }
    }
}
