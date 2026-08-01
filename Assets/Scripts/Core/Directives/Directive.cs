// Directive.cs
// A message from higher, as data. Not an order.
//
// #73/#36: intent and constraints — take this ground by this time, do not become decisively
// engaged — leaving the how to the receiver. It states what to achieve; a Command states what
// to do. THIS MUST NEVER DECOMPOSE INTO ORDERS — see DirectiveBus.cs and Simulation.cs's
// OnCommandDelivered, which a Directive never passes through. A directive that silently became
// three MoveTo orders is indistinguishable from a bug: the player would have nothing to do.
//
// Same shape decision as Command and SituationReport, for the same reason: a flat struct
// rather than a class hierarchy, because polymorphic serialisation is a liability in data that
// has to round-trip identically through ScenarioIO and through a log that must replay.
//
// TWO IDENTITIES, NOT ONE, AND THAT IS DELIBERATE:
//
//   Id   author-assigned, scenario data, stable before any simulation runs — exactly like
//        Objective.Id. This is what a VictoryCondition references (VictoryCondition.DirectiveId)
//        so a scenario can say "this condition came from that directive" without the reference
//        depending on runtime ordering.
//   Seq  runtime-assigned by DirectiveLog.Append, mirroring Command.Seq / SituationReport.Seq —
//        exactly one writer, the log, so the total order cannot depend on who happened to
//        publish first. A scenario author cannot know this in advance and must not try to.
//
// Collapsing these into one field was considered and rejected: in v1 there is exactly one
// directive per scenario, so Seq happens to always be 1 — but leaning on that accident breaks
// the moment a FRAGO stream (out of scope here, see #36) makes Seq assignment order matter.

using System;
using Strategos.Units;

namespace Strategos.Directives
{
    [Serializable]
    public struct Directive
    {
        /// <summary>
        /// Author-assigned, stable scenario identity. See the file header for why this is not
        /// the same field as <see cref="Seq"/>. Referenced by
        /// <see cref="Objectives.VictoryCondition.DirectiveId"/>.
        /// </summary>
        public int Id;

        /// <summary>
        /// Total order among directives ever published, assigned by <see cref="DirectiveLog"/>
        /// at publish time — never by the author. Zero means unassigned (not yet published).
        /// </summary>
        public ulong Seq;

        /// <summary>Simulation step the directive was published on.</summary>
        public int Tick;

        /// <summary>
        /// The formation addressed. Must resolve to exactly one <c>UnitHierarchy</c> root under
        /// <c>Scenario.PlayerSide</c> — checked in <c>Scenario.Validate</c>, because "more than
        /// one root and nobody said which" is a scenario error, not a runtime pick.
        /// </summary>
        public UnitId TargetUnit;

        /// <summary>
        /// Who it is from. Free text, read from the addressee's own
        /// <see cref="UnitInstance.HigherFormation"/> at authoring time rather than modelled as
        /// a real off-map unit — every shipped scenario already fills that field in.
        /// </summary>
        public string From;

        /// <summary>What to achieve. The receiver decides how.</summary>
        public string Intent;

        /// <summary>What not to do while achieving it — e.g. "do not become decisively engaged".</summary>
        public string Constraints;

        /// <summary>Tick by which the intent should be achieved. Zero means no deadline.</summary>
        public int DeadlineTick;

        /// <summary>
        /// The objective(s) this directive concerns, referencing <c>Scenario.Objectives</c> by
        /// id. Not a duplicated payload: the directive names which objectives are in force, and
        /// <c>VictoryEvaluator</c> — handed its objectives, never fetching them — is still the
        /// only place that tests whether they are held. Two numbers that can drift is exactly
        /// the failure this avoids (see <c>VictoryEvaluator.cs</c>'s force-bar note).
        /// </summary>
        public int[] ObjectiveIds;

        public override string ToString()
        {
            string deadline = DeadlineTick > 0 ? $" by t{DeadlineTick}" : string.Empty;
            return $"#{Seq} [{Id}] t{Tick} from {From} -> {TargetUnit}: {Intent}{deadline}";
        }
    }
}
