// ScreenExecutor.cs
// Hold this ground to see and report — not to fortify.
//
// THE SECOND ORDER THAT DOES NOT END. Defend buys protection with time; Screen buys reach
// with exposure. The unit stays Halted-equivalent for combat (Posture.Screening pays the same
// incoming-fire factor as Halted) and stretches detection through
// UnitCapabilities.DetectionPostureFactor, which ContactTracker multiplies into range.
//
// WHY NOT DIG IN. A screen that dug in would be Defend with a longer radio — the trade-off
// that makes Screen a different order would disappear. Digging in is the Defend decision;
// watching from where you stand, without preparation, is the Screen one.
//
// WHAT IT DOES NOT DO: move, engage, or change ROE. March-then-screen is MoveTo + Screen.
// A screen on Hold Fire is still Hold Fire — the mission suggests observation, it does not
// silently rewrite the player's ROE (#85 leaves that open on purpose).

using Strategos.Units;

namespace Strategos.Commands
{
    public sealed class ScreenExecutor : ICommandExecutor
    {
        public CommandKind Kind => CommandKind.Screen;

        public CommandOutcome Step(UnitInstance unit, in QueuedCommand entry,
            ExecutionContext context)
        {
            if (unit == null) return CommandOutcome.Failed;
            if (unit.IsDestroyed) return CommandOutcome.Failed;

            // Re-applied every tick for the same reason Defend re-applies posture: a return-fire
            // reflex parks the unit at Halted, and Screening has to come back when it finishes.
            unit.Posture = Posture.Screening;
            return CommandOutcome.Running;
        }
    }
}
