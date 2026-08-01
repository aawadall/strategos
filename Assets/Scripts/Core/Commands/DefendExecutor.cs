// DefendExecutor.cs
// Hold this ground.
//
// THE FIRST ORDER THAT DOES NOT END. MoveTo completes on arrival and Engage completes when the
// target dies; both are tasks. Defend is a *state* — the unit is holding until it is told
// otherwise — so it returns Running for ever and leaves the queue only by being cancelled.
// That is what makes "take that ground and hold it" sayable, and it is why the queue was built
// to hold an entry indefinitely rather than assuming every order finishes.
//
// AND THE FIRST THING TO EVER PRODUCE Posture.DugIn. EngagementResolver.PostureFactor has paid
// out 0.5 for a dug-in unit since combat landed, and nothing in the project has ever set it —
// the value was unreachable. Digging in halves incoming fire, and it takes time, so ordering a
// unit to hold ground early is worth something concrete and being forced to fight before the
// position is prepared costs something concrete.
//
// WHAT IT DOES NOT DO: move. A Defend is holding where the unit stands. "March there, then
// hold it" is a MoveTo followed by a Defend, which the plan already expresses — shift-queue
// builds it and the plan card lists it. Giving Defend its own movement would duplicate
// MoveToExecutor's routing and give two answers to what a unit walking somewhere is doing.

using Strategos.Units;

namespace Strategos.Commands
{
    public sealed class DefendExecutor : ICommandExecutor
    {
        /// <summary>
        /// Ticks of undisturbed holding before a position counts as prepared.
        /// </summary>
        /// <remarks>
        /// Two minutes at one second a tick. Long enough that digging in is a decision made in
        /// advance rather than a reflex — a unit that halts as contact opens does not get the
        /// benefit — and short enough to matter inside a scenario measured in tens of minutes.
        ///
        /// It is deliberately *not* instant. An order that conferred half incoming damage the
        /// moment it was given would make holding strictly better than moving, and the whole
        /// point is that preparation is bought with time you might have wanted for something
        /// else.
        /// </remarks>
        public const int DigInTicks = 120;

        public CommandKind Kind => CommandKind.Defend;

        public CommandOutcome Step(UnitInstance unit, in QueuedCommand entry,
            ExecutionContext context)
        {
            if (unit == null) return CommandOutcome.Failed;

            // A unit that has been shot to pieces stops holding anything. Reported as failure
            // rather than completion, because the ground was not held.
            if (unit.IsDestroyed) return CommandOutcome.Failed;

            // Halted the moment the order starts; dug in once the position is prepared.
            //
            // Re-applied every tick rather than set once, because a reflex that preempts this
            // order sends the unit to Halted while it returns fire (EngageExecutor), and the
            // posture has to come back when the reflex finishes. TicksExecuting survives the
            // interruption, so a unit resumes its preparation rather than starting over — it
            // dug for a while, was interrupted, and carries on.
            unit.Posture = entry.TicksExecuting >= DigInTicks
                ? Posture.DugIn
                : Posture.Halted;

            // Never completes. The order is the unit's state until something cancels it.
            return CommandOutcome.Running;
        }
    }
}
