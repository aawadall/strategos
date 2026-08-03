// DelayExecutor.cs
// Hold until pressed, then give ground.
//
// NEVER-ENDING UNTIL THE THRESHOLD. Like Defend it occupies the queue as a state, but the
// point of Delay is to trade ground for time — once strength or ammunition crosses the same
// break-contact line ReactionController uses, this order Completes and Simulation issues a
// Withdraw. Cover deliberately suppresses that reflex; Delay is the opposite: it *wants* to
// leave under pressure.
//
// No dig-in. A delaying position that dug in would fight like Defend and only leave late;
// the doctrinal delay gives ground earlier than a prepared defence would.

using Strategos.Reactions;
using Strategos.Units;

namespace Strategos.Commands
{
    public sealed class DelayExecutor : ICommandExecutor
    {
        public CommandKind Kind => CommandKind.Delay;

        public CommandOutcome Step(UnitInstance unit, in QueuedCommand entry,
            ExecutionContext context)
        {
            if (unit == null) return CommandOutcome.Failed;
            if (unit.IsDestroyed) return CommandOutcome.Failed;

            unit.Posture = Posture.Halted;

            if (unit.Strength < ReactionController.BreakStrengthPercent ||
                unit.Supply.Ammunition < ReactionController.BreakAmmunitionPercent)
                return CommandOutcome.Completed;

            return CommandOutcome.Running;
        }
    }
}
