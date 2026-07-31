// EngageExecutor.cs
// Carries out an Engage order: keep firing at a named enemy until one of the reasons to stop
// arrives.
//
// DECLARES, DOES NOT RESOLVE. This executor mutates its own unit's posture and nothing else;
// the shot itself is appended to ExecutionContext.Engagements and resolved by the simulation
// once every unit has declared. The reasoning is on that field — the short version is that
// resolving here would give whichever unit the loop reached first a free shot.
//
// WHAT THIS DOES NOT DECIDE. Whether a unit *should* be firing — rules of engagement, return
// fire, defensive fire, breaking contact on a threshold — is #13, and none of it belongs
// here. This executor is what an explicit order looks like, and #13 will issue the same order
// on a unit's own initiative through the same path.

using UnityEngine;
using Strategos.Units;

namespace Strategos.Commands
{
    public sealed class EngageExecutor : ICommandExecutor
    {
        /// <summary>
        /// Ticks an engagement may run before it is abandoned.
        ///
        /// Twenty minutes of simulated fire. A guard, not a rule: an engagement that has not
        /// resolved in twenty minutes is two units unable to hurt each other, and leaving it at
        /// the head of a queue for ever looks exactly like a stuck executor. #13 replaces this
        /// with an actual break-contact decision.
        /// </summary>
        public const int MaxTicks = 1200;

        public CommandKind Kind => CommandKind.Engage;

        /// <summary>
        /// The unit the simulation resolved a shot for, per attacker, for a view to draw a
        /// fire line. Written by the simulation, read by the UI.
        /// </summary>
        public CommandOutcome Step(UnitInstance unit, in QueuedCommand entry,
            ExecutionContext context)
        {
            if (context.Map == null) return CommandOutcome.Failed;

            var against = entry.Command.AgainstUnit;
            if (!against.IsValid || against == unit.Id) return CommandOutcome.Failed;

            if (unit.IsDestroyed) return CommandOutcome.Failed;

            if (entry.TicksExecuting >= MaxTicks)
            {
                unit.Posture = Posture.Halted;
                return CommandOutcome.Failed;
            }

            // Firing from the halt. A unit told to engage stops to do it — which is also what
            // makes PostureFactor's moving penalty a decision a player can act on rather than
            // something that happens to them.
            if (unit.Posture == Posture.Moving) unit.Posture = Posture.Halted;

            context.Engagements.Add(new EngagementIntent
            {
                Attacker = unit.Id,
                Defender = against,
                Command = entry.Command.Seq,
                Opening = entry.TicksExecuting == 0,
            });

            // Running until the simulation says otherwise. It owns the reasons to stop —
            // target destroyed, out of ammunition — because it is what resolved the shot and
            // therefore the only thing that knows.
            return CommandOutcome.Running;
        }
    }
}
