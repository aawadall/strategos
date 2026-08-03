// CoverExecutor.cs
// Cover the main body — dig in and accept the fight.
//
// THE HEAVIEST NEVER-ENDING SECURITY HOLD. Screen watches without fortifying; Guard digs in
// and keeps a modest watch; Cover digs in with no detection stretch and will not break
// contact on its own (ReactionController reads the Cover order). That is the weight ladder
// in #85: Screen → Guard → Cover.
//
// DigInTicks is DefendExecutor's constant — one preparation clock for every hold that digs.

using Strategos.Units;

namespace Strategos.Commands
{
    public sealed class CoverExecutor : ICommandExecutor
    {
        public CommandKind Kind => CommandKind.Cover;

        public CommandOutcome Step(UnitInstance unit, in QueuedCommand entry,
            ExecutionContext context)
        {
            if (unit == null) return CommandOutcome.Failed;
            if (unit.IsDestroyed) return CommandOutcome.Failed;

            unit.Posture = entry.TicksExecuting >= DefendExecutor.DigInTicks
                ? Posture.Covering
                : Posture.Halted;

            return CommandOutcome.Running;
        }
    }
}
