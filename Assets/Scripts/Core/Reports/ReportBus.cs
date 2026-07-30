// ReportBus.cs
// The situation topic: observations published here, delivered to subscribers a step later.
//
// The mechanism and its four delivery rules live in MessageBus<T>, shared with the command
// topic — see Assets/Scripts/Core/Messaging/MessageBus.cs.
//
// PUBLICATION AND DELIVERY ARE SEPARATE, AND MUST STAY SEPARATE. A unit publishes; the bus
// decides whether and when the message arrives. Today it always arrives, exactly one step
// later. Under C3 a channel model sits in this gap — delay by distance and echelon, loss on a
// degraded net, corruption, an adversary's forgery — and it can only sit there if neither end
// assumes delivery is immediate or certain. That is the whole reason a unit does not simply
// call a method on its commander.

namespace Strategos.Reports
{
    public sealed class ReportBus : Messaging.MessageBus<SituationReport>
    {
        protected override string TopicName => "ReportBus";
    }
}
