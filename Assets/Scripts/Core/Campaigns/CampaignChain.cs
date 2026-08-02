// CampaignChain.cs
// #75 chunk 1: the data shape only. An authored, ordered list of operations, each naming a
// scenario and a place to record what happened to it. Data only, same rule as Scenario and
// every other model type in Core — nothing here reads or writes a byte (see CampaignChainIO)
// and nothing here computes anything.
//
// AUTHORED, NOT GENERATED. #75 depends on #78's own epic boundary: the procedural campaign
// generator is a later, separate thing (phases.md 6.3) and must not shape this format. A
// CampaignChain is a fixed JSON list, exactly the way a Scenario or a doctrine pack is
// hand-authored content, not a runtime product.
//
// WHAT THIS CHUNK DELIBERATELY DOES NOT DO — see the #75 chunk-1 brief and handoff in
// Artifacts/agents/campaign-chain-shape-out.md for the reasoning:
//   - No carry-over logic. CarriedOverUnits is a field a later chunk writes into after an
//     operation is played and reads out of before the next one starts; this chunk never
//     populates it from a Simulation or a SaveRecord.
//   - No readiness recovery curve. The user has already decided readiness partially recovers
//     between operations (see #75) — that curve is not computed here, only a place for the
//     recovered value to live once the next chunk computes it (readiness is a field on each
//     carried-over UnitInstance already, via SimulationSnapshot's own shape).
//   - No defeat-cost logic. Outcome records won/lost/drew per entry; what a loss actually costs
//     the campaign is a separate, later decision.
//
// CARRIED-OVER STATE REUSES UnitInstance, NOT A NEW TYPE. SimulationSnapshot.Units is already
// "every fighting unit's full field state, one per leaf, UnitInstance.Clone()" — exactly what a
// carried-over ORBAT needs to hold (strength, readiness, casualties are all fields on
// UnitInstance already). Inventing a parallel CampaignUnitState would duplicate that shape and
// have to be kept in step with it for no gain, the same reasoning Scenario.Units gives for using
// UnitInstance directly at t = 0 rather than a separate placement type.

using System.Collections.Generic;
using Strategos.Units;

namespace Strategos.Campaigns
{
    /// <summary>How one entry in the chain ended, or that it has not been played yet.</summary>
    public enum OperationOutcome
    {
        Unplayed,
        Won,
        Lost,
        Drew,
    }

    /// <summary>
    /// One operation in the chain: which scenario it runs, how it ended, and the ORBAT state
    /// carried into it.
    /// </summary>
    public sealed class CampaignChainEntry
    {
        /// <summary>
        /// The scenario this operation runs, by name — the same convention
        /// <see cref="Scenarios.ScenarioIO.Load"/> already resolves against
        /// <c>Resources/Scenarios</c> (see <see cref="Scenarios.ScenarioSamples.SkirmishName"/>).
        /// Not a new id scheme: a campaign entry names a scenario exactly the way any other
        /// caller of <c>ScenarioIO.Load</c> does.
        /// </summary>
        public string ScenarioName = string.Empty;

        /// <summary>Human-readable label for this entry, independent of the scenario's own name.</summary>
        public string Name = string.Empty;

        /// <summary><see cref="OperationOutcome.Unplayed"/> until a later chunk records a result.</summary>
        public OperationOutcome Outcome = OperationOutcome.Unplayed;

        /// <summary>
        /// The ORBAT this operation starts with, carried over from the previous entry's result.
        /// Empty for the first entry in a chain (it starts from the scenario's own
        /// <see cref="Scenarios.Scenario.Units"/> instead) and for any entry not yet played.
        ///
        /// Full field state, one per leaf unit — the same shape
        /// <see cref="Persistence.SimulationSnapshot.Units"/> already carries, deliberately
        /// reused rather than duplicated. This chunk only holds the field: nothing here writes
        /// to it from a played <c>Simulation</c>, computes readiness recovery, or applies a
        /// defeat cost — that is later chunks' work.
        /// </summary>
        public List<UnitInstance> CarriedOverUnits = new();
    }

    /// <summary>
    /// A fixed, ordered list of operations sharing one persistent ORBAT. See the file header:
    /// authored content, not a generator's output.
    /// </summary>
    public sealed class CampaignChain
    {
        /// <summary>
        /// Bumped when the on-disk shape changes incompatibly — same role as
        /// <see cref="Scenarios.Scenario.FormatVersion"/>.
        /// </summary>
        public int FormatVersion = 1;

        public string Name = "Untitled Campaign";
        public string Description = string.Empty;

        /// <summary>The operations, in play order.</summary>
        public List<CampaignChainEntry> Operations = new();
    }
}
