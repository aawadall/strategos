// MoveToExecutor.cs
// Carries out a MoveTo order: walk the unit toward the destination at the speed its
// capabilities allow over the ground it is crossing.
//
// This is #8a — straight line, no pathfinding. It is deliberately first, because it proves
// the whole chain (issue an order -> queue -> execute -> arrive -> next command) while that
// chain is still trivial to debug. A* with roads, fords and climb limits is #8b, and replaces
// only the choice of *where* to step next; everything here about speed, arrival and failure
// stays as it is.
//
// FIXED STEP, NO WALL CLOCK. Distance moved is speed multiplied by the simulation's own
// seconds-per-tick, never Time.deltaTime. A replay has to reproduce the same positions on a
// different machine at a different frame rate, and that only works if movement is a function
// of the tick count.

using UnityEngine;
using Strategos.Maps;
using Strategos.Units;

namespace Strategos.Commands
{
    public sealed class MoveToExecutor : ICommandExecutor
    {
        /// <summary>
        /// How close, in cells, counts as arrived.
        ///
        /// Needed because a unit moving a fractional distance per tick will step over an exact
        /// destination rather than landing on it, and would then oscillate around it for ever.
        /// </summary>
        public const float ArrivalCells = 0.05f;

        /// <summary>
        /// Ticks a single move may run before being abandoned.
        ///
        /// A guard, not a rule: a unit that cannot make progress — walled in by impassable
        /// ground it is already standing next to — would otherwise hold the head of its queue
        /// for ever, and a plan that silently stops advancing looks exactly like a pathfinding
        /// bug. Generous enough that no legitimate move on a 1024-cell map hits it.
        /// </summary>
        public const int MaxTicks = 20000;

        public CommandKind Kind => CommandKind.MoveTo;

        public CommandOutcome Step(UnitInstance unit, in QueuedCommand entry,
            ExecutionContext context)
        {
            var map = context.Map;
            if (map == null) return CommandOutcome.Failed;

            Vector2 target = entry.Command.TargetCell;
            Vector2 from = unit.Cell;
            Vector2 delta = target - from;
            float remaining = delta.magnitude;

            if (remaining <= ArrivalCells)
            {
                unit.Cell = target;
                unit.Posture = Posture.Halted;
                return CommandOutcome.Completed;
            }

            if (entry.TicksExecuting >= MaxTicks)
            {
                unit.Posture = Posture.Halted;
                return CommandOutcome.Failed;
            }

            var caps = unit.Capabilities(context.Catalogue);

            // Speed is a property of the ground being crossed, so it is sampled at the unit's
            // current cell each tick rather than once at the start. A unit entering forest
            // slows down as it enters, not when it set off.
            int cx = Mathf.Clamp(Mathf.RoundToInt(from.x), 0, map.Width - 1);
            int cy = Mathf.Clamp(Mathf.RoundToInt(from.y), 0, map.Height - 1);
            var cover = map.GetLandcover(cx, cy);
            float slope = map.SampleSlopeDegrees(cx, cy);

            // No road following yet — that is #8b, along with the pathfinder that can
            // actually choose to use one.
            float speedMps = caps.SpeedMps(cover, slope, onRoad: false);

            if (speedMps <= 0f)
            {
                // Standing somewhere this unit cannot occupy. Better to fail loudly than to
                // sit at the head of the queue looking like a stall.
                unit.Posture = Posture.Halted;
                return CommandOutcome.Failed;
            }

            unit.Posture = Posture.Moving;

            float metres = speedMps * context.SecondsPerTick;
            float cells = metres / Mathf.Max(0.0001f, map.Header.MetresPerCell);

            if (cells >= remaining)
            {
                // Would overshoot: land exactly on the destination instead.
                unit.Cell = target;
                unit.Posture = Posture.Halted;
                return CommandOutcome.Completed;
            }

            Vector2 next = from + delta / remaining * cells;

            // Straight line, so the path is not checked ahead — but refuse to walk *into*
            // ground the unit cannot occupy rather than sliding through a lake. #8b removes
            // the situation by routing around it in the first place.
            int nx = Mathf.Clamp(Mathf.RoundToInt(next.x), 0, map.Width - 1);
            int ny = Mathf.Clamp(Mathf.RoundToInt(next.y), 0, map.Height - 1);
            if (nx != cx || ny != cy)
            {
                var nextCover = map.GetLandcover(nx, ny);
                float nextSlope = map.SampleSlopeDegrees(nx, ny);
                if (!caps.CanEnter(nextCover, nextSlope))
                {
                    unit.Posture = Posture.Halted;
                    return CommandOutcome.Failed;
                }
            }

            unit.Cell = next;
            return CommandOutcome.Running;
        }
    }
}
