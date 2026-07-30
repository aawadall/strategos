// ICommandExecutor.cs
// How a world command actually happens.
//
// The seam between the command system and everything that carries orders out. #9 ships no
// world executors at all — movement arrives in #8 as a MoveTo executor, and engagement later
// as its own. Control commands (Abort, CancelFrom, Hold) never reach an executor: they act on
// the queue and are handled by the simulation directly, which is why they are a separate
// CommandKind range.
//
// Executors may mutate the unit they are given and nothing else. That is delivery rule 3 —
// only the owner mutates — and it is the rule a compiler cannot enforce, so it is stated at
// the seam where it would be broken.

using System.Collections.Generic;
using UnityEngine;
using Strategos.Maps;
using Strategos.Units;

namespace Strategos.Commands
{
    public enum CommandOutcome
    {
        /// <summary>Still working. The command stays at the head of the queue.</summary>
        Running = 0,
        /// <summary>Done. The command leaves the queue and the next one begins.</summary>
        Completed = 1,
        /// <summary>Cannot be done — unreachable, impassable, out of supply.</summary>
        Failed = 2,
    }

    /// <summary>Everything an executor is allowed to see.</summary>
    public sealed class ExecutionContext
    {
        public MapData Map;
        public UnitCatalogue Catalogue;

        /// <summary>Current simulation step.</summary>
        public int Tick;

        /// <summary>Seconds of simulated time per step. Fixed — see Simulation.</summary>
        public float SecondsPerTick;
    }

    /// <summary>
    /// Implemented by an executor that plans a route, so a view can draw what the unit will
    /// actually walk rather than a straight line to the destination.
    ///
    /// Separate from ICommandExecutor because most executors have no route — firing at
    /// something does not involve one — and because the alternative is a view recomputing the
    /// path itself, which is both wasteful and able to disagree with what is being walked.
    /// </summary>
    public interface IRouteProvider
    {
        /// <summary>Cells the unit is currently routed through, or null.</summary>
        IReadOnlyList<Vector2Int> RouteOf(UnitId unit);
    }

    public interface ICommandExecutor
    {
        /// <summary>Which command this executor carries out.</summary>
        CommandKind Kind { get; }

        /// <summary>
        /// Advances the command by one step.
        /// </summary>
        /// <param name="unit">The addressee. The only thing this may mutate.</param>
        /// <param name="entry">
        /// The queue entry, including how many ticks it has been executing — executors should
        /// pace themselves from that rather than keeping their own state, so a replay
        /// reconstructs them exactly.
        /// </param>
        CommandOutcome Step(UnitInstance unit, in QueuedCommand entry, ExecutionContext context);
    }
}
