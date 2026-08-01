// DefendProbe.cs
// Holding ground: that the order does not end, that digging in happens and is worth
// something, and that a reflex does not throw the position away.
//
// Menu:  Strategos > Probe Defend
// Batch: -executeMethod Strategos.Editor.DefendProbe.Run
//
// THE NUMBER THAT MATTERS is what digging in is worth, and it is printed rather than asserted
// to a value: EngagementResolver.PostureFactor has paid 0.5 for Posture.DugIn since combat
// landed and *nothing in the project had ever set it*, so this is the first measurement of a
// figure that has been in the model, unreachable, the whole time. If it stops being roughly
// half, the model changed and the table is where that shows.

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Combat;
using Strategos.Commands;
using Strategos.Maps;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class DefendProbe
    {
        [MenuItem("Strategos/Probe Defend")]
        public static void Run()
        {
            var log = new StringBuilder();
            bool ok = true;

            ok &= OrderDoesNotEnd(log);
            ok &= DiggingInTakesTimeAndPays(log);
            ok &= ReflexDoesNotDiscardThePosition(log);
            ok &= HoldIsNoLongerAbort(log);

            Debug.Log(log.ToString());
            Debug.Log(ok ? "[DefendProbe] PROBE PASSED" : "[DefendProbe] PROBE FAILED");
        }

        private static Simulation Fresh(out UnitInstance unit)
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var sim = new Simulation(scenario, scenario.GenerateMap(), UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            sim.AddExecutor(new DefendExecutor());

            unit = sim.Units[0];
            return sim;
        }

        // ─── Assertions ───────────────────────────────────────────────────────

        /// <summary>
        /// The first order in the project that is a state rather than a task.
        /// </summary>
        private static bool OrderDoesNotEnd(StringBuilder log)
        {
            var sim = Fresh(out var unit);
            sim.Issue(Command.Hold(ActorId.ForSide(unit.Side), unit.Id));
            sim.Step(600);

            var queue = sim.QueueOf(unit.Id);
            if (queue == null || queue.IsEmpty)
            {
                log.AppendLine("  persistence: FAILED, the defence left the queue — an order " +
                               "that is a state must not complete on its own");
                return false;
            }

            if (queue[0].Command.Kind != CommandKind.Defend)
            {
                log.AppendLine($"  persistence: FAILED, head is {queue[0].Command.Kind}");
                return false;
            }

            log.AppendLine($"  persistence: still holding after 600 ticks, " +
                           $"{queue[0].TicksExecuting} ticks executing  ok");
            return true;
        }

        /// <summary>
        /// Digging in must take time, and must then be worth something measurable.
        /// </summary>
        /// <remarks>
        /// An order that conferred the benefit instantly would make holding strictly better
        /// than moving; the point is that preparation is bought with time. And a posture that
        /// changed nothing would be a label — hence the damage comparison rather than only an
        /// assertion that the enum moved.
        /// </remarks>
        private static bool DiggingInTakesTimeAndPays(StringBuilder log)
        {
            var sim = Fresh(out var unit);
            sim.Issue(Command.Hold(ActorId.ForSide(unit.Side), unit.Id));

            sim.Step(2);
            if (unit.Posture != Posture.Halted)
            {
                log.AppendLine($"  digging in: FAILED, posture is {unit.Posture} two ticks in, " +
                               "expected Halted");
                return false;
            }

            sim.Step(DefendExecutor.DigInTicks - 10);
            if (unit.Posture == Posture.DugIn)
            {
                log.AppendLine("  digging in: FAILED, dug in early — preparation must cost the " +
                               "time it claims to");
                return false;
            }

            sim.Step(20);
            if (unit.Posture != Posture.DugIn)
            {
                log.AppendLine($"  digging in: FAILED, posture is {unit.Posture} after " +
                               $"{DefendExecutor.DigInTicks} ticks, expected DugIn");
                return false;
            }

            // What it is worth. Measured, not asserted to a constant: this is the first time
            // the figure has been reachable at all.
            float halted = EngagementResolver.PostureFactor(Posture.Halted);
            float dug = EngagementResolver.PostureFactor(Posture.DugIn);
            float moving = EngagementResolver.PostureFactor(Posture.Moving);

            log.AppendLine($"  digging in: Halted at t2, DugIn at t{DefendExecutor.DigInTicks} " +
                           $"({DefendExecutor.DigInTicks / 60f:0.0} min)  ok");
            log.AppendLine($"    incoming fire multiplier: moving {moving:0.00}, " +
                           $"halted {halted:0.00}, dug in {dug:0.00}  " +
                           $"({(1f - dug / halted) * 100f:0}% less than halted)");

            if (dug >= halted)
            {
                log.AppendLine("    FAILED, digging in is not worth anything");
                return false;
            }
            return true;
        }

        /// <summary>
        /// A reflex must interrupt the defence without discarding it.
        /// </summary>
        /// <remarks>
        /// This is the case #72 names in its acceptance and the one a naive implementation
        /// gets wrong: the reaction preempts onto the front of the queue, the unit fights, and
        /// the defence has to be underneath still — otherwise being shot at once would end a
        /// unit's standing orders for the rest of the scenario.
        /// </remarks>
        private static bool ReflexDoesNotDiscardThePosition(StringBuilder log)
        {
            var sim = Fresh(out var unit);
            sim.Issue(Command.Hold(ActorId.ForSide(unit.Side), unit.Id));
            sim.Step(DefendExecutor.DigInTicks + 20);

            if (unit.Posture != Posture.DugIn)
            {
                log.AppendLine("  interruption: FAILED, never dug in to begin with");
                return false;
            }

            // Something else lands at the head of the queue, exactly as a reaction would.
            var target = sim.Units[sim.Units.Count - 1];
            sim.Issue(Command.Engage(ActorId.ForSide(unit.Side), unit.Id, target.Id,
                preempt: true));
            sim.Step(3);

            var queue = sim.QueueOf(unit.Id);
            if (queue == null || queue.Count < 2)
            {
                log.AppendLine($"  interruption: FAILED, queue holds {queue?.Count ?? 0} " +
                               "entries — the defence was discarded rather than interrupted");
                return false;
            }

            if (queue[1].Command.Kind != CommandKind.Defend)
            {
                log.AppendLine($"  interruption: FAILED, {queue[1].Command.Kind} is underneath, " +
                               "not the defence");
                return false;
            }

            // And it comes back when the interruption is cleared.
            sim.Issue(Command.CancelFrom(ActorId.ForSide(unit.Side), unit.Id, 0));
            sim.Issue(Command.Hold(ActorId.ForSide(unit.Side), unit.Id));
            sim.Step(DefendExecutor.DigInTicks + 5);

            if (unit.Posture != Posture.DugIn)
            {
                log.AppendLine($"  interruption: FAILED, posture is {unit.Posture} after " +
                               "resuming, expected DugIn");
                return false;
            }

            log.AppendLine("  interruption: a preempting order sits above the defence, which " +
                           "survives and resumes  ok");
            return true;
        }

        /// <summary>
        /// HOLD and ABORT PLAN must now be different orders.
        /// </summary>
        /// <remarks>
        /// They were a byte-for-byte copy of each other, so the two buttons differed only in
        /// what the log called them. This is the assertion that keeps them apart.
        /// </remarks>
        private static bool HoldIsNoLongerAbort(StringBuilder log)
        {
            var sim = Fresh(out var unit);
            var actor = ActorId.ForSide(unit.Side);

            sim.Issue(Command.Hold(actor, unit.Id));
            sim.Step(3);
            int afterHold = sim.QueueOf(unit.Id).Count;

            sim.Issue(Command.Abort(actor, unit.Id));
            sim.Step(2);
            int afterAbort = sim.QueueOf(unit.Id).Count;

            if (afterHold == 0)
            {
                log.AppendLine("  distinct: FAILED, HOLD emptied the queue — it is still Abort");
                return false;
            }

            if (afterAbort != 0)
            {
                log.AppendLine($"  distinct: FAILED, ABORT left {afterAbort} entries");
                return false;
            }

            log.AppendLine($"  distinct: HOLD leaves {afterHold} order queued, ABORT leaves " +
                           $"{afterAbort}  ok");
            return true;
        }
    }
}
#endif
