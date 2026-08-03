// CampaignChain.cs
// #75 chunk 1: the data shape — an authored, ordered list of operations. #138 adds
// CampaignChain.Validate(), the same kind of load-time check Scenario.Validate is: it reads
// and reports, it does not mutate the chain or play an operation.
//
// AUTHORED, NOT GENERATED. #75 depends on #78's own epic boundary: the procedural campaign
// generator is a later, separate thing (phases.md 6.3) and must not shape this format. A
// CampaignChain is a fixed JSON list, exactly the way a Scenario or a doctrine pack is
// hand-authored content, not a runtime product.
//
// CARRIED-OVER STATE REUSES UnitInstance, NOT A NEW TYPE. SimulationSnapshot.Units is already
// "every fighting unit's full field state, one per leaf, UnitInstance.Clone()" — exactly what a
// carried-over ORBAT needs to hold. Inventing a parallel CampaignUnitState would duplicate that
// shape for no gain.

using System;
using System.Collections.Generic;
using Strategos.Scenarios;
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
        /// <see cref="ScenarioIO.Load"/> already resolves against
        /// <c>Resources/Scenarios</c> (see <see cref="ScenarioSamples.SkirmishName"/>).
        /// </summary>
        public string ScenarioName = string.Empty;

        /// <summary>Human-readable label for this entry, independent of the scenario's own name.</summary>
        public string Name = string.Empty;

        /// <summary><see cref="OperationOutcome.Unplayed"/> until a later chunk records a result.</summary>
        public OperationOutcome Outcome = OperationOutcome.Unplayed;

        /// <summary>
        /// The ORBAT this operation starts with, carried over from the previous entry's result.
        /// Empty for the first entry in a chain (it starts from the scenario's own
        /// <see cref="Scenario.Units"/> instead) and for any entry not yet played.
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
        /// <see cref="Scenario.FormatVersion"/>.
        /// </summary>
        public int FormatVersion = 1;

        public string Name = "Untitled Campaign";
        public string Description = string.Empty;

        /// <summary>The operations, in play order.</summary>
        public List<CampaignChainEntry> Operations = new();

        /// <summary>
        /// Load-time checks before PLAY (#138): scenario names resolve, each scenario's own
        /// <see cref="Scenario.Validate"/> passes, populated
        /// <see cref="CampaignChainEntry.CarriedOverUnits"/> match that entry's scenario, and
        /// UnitIds shared across consecutive operations keep the same Side and CapabilityId
        /// (Id-consistency authoring rule in docs/campaign-invariants.md).
        /// </summary>
        /// <param name="catalogue">
        /// Forwarded to each scenario's Validate. Null skips capability-existence checks the
        /// same way <see cref="Scenario.Validate"/> already does.
        /// </param>
        /// <param name="load">
        /// Scenario resolver; defaults to <see cref="ScenarioIO.Load"/>. Tests may inject a
        /// resolver that returns mutated fixtures without shipping bad JSON.
        /// </param>
        /// <returns>Every problem found in one pass — empty means the chain may start.</returns>
        public List<string> Validate(UnitCatalogue catalogue = null,
            Func<string, Scenario> load = null)
        {
            var problems = new List<string>();
            load ??= ScenarioIO.Load;

            if (string.IsNullOrWhiteSpace(Name))
                problems.Add("Campaign has no name.");

            if (Operations == null || Operations.Count == 0)
            {
                problems.Add("Campaign has no operations.");
                return problems;
            }

            var loaded = new List<Scenario>(Operations.Count);

            for (int i = 0; i < Operations.Count; i++)
            {
                var entry = Operations[i] ?? new CampaignChainEntry();
                string opLabel = OpLabel(i, entry);

                if (string.IsNullOrWhiteSpace(entry.ScenarioName))
                {
                    problems.Add($"{opLabel}: no scenario name.");
                    loaded.Add(null);
                    continue;
                }

                Scenario scenario;
                try { scenario = load(entry.ScenarioName); }
                catch (Exception ex)
                {
                    problems.Add(
                        $"{opLabel}: failed to load scenario '{entry.ScenarioName}': {ex.Message}");
                    loaded.Add(null);
                    continue;
                }

                if (scenario == null)
                {
                    problems.Add(
                        $"{opLabel}: scenario '{entry.ScenarioName}' did not load from " +
                        $"Resources/{ScenarioIO.ResourceFolder}.");
                    loaded.Add(null);
                    continue;
                }

                loaded.Add(scenario);

                foreach (var p in scenario.Validate(catalogue))
                    problems.Add($"{opLabel} / scenario '{entry.ScenarioName}': {p}");

                if (entry.CarriedOverUnits == null || entry.CarriedOverUnits.Count == 0)
                    continue;

                foreach (var carried in entry.CarriedOverUnits)
                {
                    if (carried == null) continue;
                    if (scenario.FindUnit(carried.Id) != null) continue;
                    problems.Add(
                        $"{opLabel}: carried-over unit {carried.Id} ('{carried.Designation}') " +
                        $"has no match in scenario '{entry.ScenarioName}' — " +
                        "MergeCarriedOver would throw (see docs/campaign-invariants.md).");
                }
            }

            for (int i = 0; i < loaded.Count - 1; i++)
            {
                var earlier = loaded[i];
                var later = loaded[i + 1];
                if (earlier == null || later == null) continue;

                CheckIdConsistency(
                    earlier, OpLabel(i, Operations[i]),
                    later, OpLabel(i + 1, Operations[i + 1]),
                    problems);
            }

            return problems;
        }

        /// <summary>
        /// Shared UnitIds across consecutive operations must keep the same Side and
        /// CapabilityId — otherwise MergeCarriedOver would glue unrelated units together.
        /// </summary>
        private static void CheckIdConsistency(Scenario earlier, string earlierLabel,
            Scenario later, string laterLabel, List<string> problems)
        {
            var laterById = new Dictionary<int, UnitInstance>();
            foreach (var u in later.Units)
            {
                if (!u.Id.IsValid) continue;
                laterById[u.Id.Value] = u;
            }

            foreach (var a in earlier.Units)
            {
                if (!a.Id.IsValid) continue;
                if (!laterById.TryGetValue(a.Id.Value, out var b)) continue;

                if (a.Side != b.Side)
                {
                    problems.Add(
                        $"Id-consistency: unit {a.Id} is side {a.Side} in {earlierLabel} but " +
                        $"side {b.Side} in {laterLabel} — the same UnitId must name the same " +
                        "persistent unit across operations in a chain.");
                }

                if (!string.Equals(a.CapabilityId, b.CapabilityId, StringComparison.Ordinal))
                {
                    problems.Add(
                        $"Id-consistency: unit {a.Id} is capability '{a.CapabilityId}' in " +
                        $"{earlierLabel} but '{b.CapabilityId}' in {laterLabel}.");
                }
            }
        }

        private static string OpLabel(int index, CampaignChainEntry entry)
        {
            string name = entry != null && !string.IsNullOrWhiteSpace(entry.Name)
                ? entry.Name
                : "(unnamed)";
            return $"Operation {index} '{name}'";
        }
    }
}
