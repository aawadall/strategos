// DirectiveBus.cs
// The topic a directive from higher arrives on: published here, delivered to subscribers a
// step later, exactly like CommandBus and ReportBus.
//
// A THIRD MessageBus<T> INSTANTIATION, NOT A REUSE OF EITHER EXISTING TOPIC.
// MessageBus.cs says the base class is generic "on purpose... two topics... obeying the same
// four rules... One implementation, two instantiations" — nothing about that assumes exactly
// two. A directive is neither a Command nor a SituationReport in shape or direction:
//
//   - Folding it into CommandBus means it is delivered to Simulation.OnCommandDelivered, which
//     checks Hierarchy.IsFormation(...) first and decomposes ANY command addressed to a
//     formation, unconditionally, before inspecting its kind. There is no ordering of that
//     check against a kind check that gives a directive both "addressed to a formation" and
//     "never decomposes" — see the note on Simulation.OnCommandDelivered.
//   - Folding it into ReportBus mixes provenance: every existing subscriber (ContactTracker,
//     ReactionController, the feed) is written assuming a report originates from a unit's own
//     observation or status. A directive originates from outside the simulated force entirely.
//
// A distinct type is what makes "publish a directive onto the command topic" a compile error
// instead of a runtime surprise — the same reasoning CommandBus.cs gives for not being a raw
// MessageBus<Command>.

namespace Strategos.Directives
{
    public sealed class DirectiveBus : Messaging.MessageBus<Directive>
    {
        protected override string TopicName => "DirectiveBus";
    }
}
