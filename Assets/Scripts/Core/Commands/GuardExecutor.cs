// GuardExecutor.cs
// Hold a line — dig in, and keep a modest watch.
//
// THE THIRD NEVER-ENDING HOLD. Screen buys reach with exposure; Defend buys protection with
// time and no extra eyes; Guard sits between them: the same dig-in clock as Defend, and a
// detection stretch smaller than Screen (1.15 vs 1.35). Increasing weight on the security
// ladder is Screen → Guard → Cover (#85).
//
// DigInTicks is DefendExecutor's constant on purpose — two clocks that drifted would make
// "how long to prepare" two answers for one question.

using Strategos.Units;

namespace Strategos.Commands
{
    public sealed class GuardExecutor : ICommandExecutor
    {
        public CommandKind Kind => CommandKind.Guard;

        public CommandOutcome Step(UnitInstance unit, in QueuedCommand entry,
            ExecutionContext context)
        {
            if (unit == null) return CommandOutcome.Failed;
            if (unit.IsDestroyed) return CommandOutcome.Failed;

            // Preparing looks like Halted (exposed); once ready, Guarding pays DugIn's
            // fire factor and DetectionPostureFactor's modest watch bonus.
            unit.Posture = entry.TicksExecuting >= DefendExecutor.DigInTicks
                ? Posture.Guarding
                : Posture.Halted;

            return CommandOutcome.Running;
        }
    }
}
