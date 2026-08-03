// CoverProbe.cs
// Cover: never ends, digs in, no detection stretch, and will not break contact while covering.
//
// Menu:  Strategos > Probe Cover
// Batch: -executeMethod Strategos.Editor.CoverProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Combat;
using Strategos.Commands;
using Strategos.Reactions;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class CoverProbe
    {
        [MenuItem("Strategos/Probe Cover")]
        public static void Run()
        {
            var log = new StringBuilder();
            bool ok = true;

            ok &= DigsInWithoutWatchBonus(log);
            ok &= DoesNotBreakContactWhileCovering(log);

            Debug.Log(log.ToString());
            Debug.Log(ok ? "[CoverProbe] PROBE PASSED" : "[CoverProbe] PROBE FAILED");
        }

        private static Simulation Fresh(out UnitInstance unit, out UnitInstance hostile)
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var sim = new Simulation(scenario, scenario.GenerateMap(), UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            sim.AddExecutor(new DefendExecutor());
            sim.AddExecutor(new ScreenExecutor());
            sim.AddExecutor(new GuardExecutor());
            sim.AddExecutor(new CoverExecutor());

            unit = null;
            hostile = null;
            for (int i = 0; i < sim.Units.Count; i++)
            {
                var u = sim.Units[i];
                if (u.Side.Value == 1) unit ??= u;
                else hostile ??= u;
            }
            return sim;
        }

        private static bool DigsInWithoutWatchBonus(StringBuilder log)
        {
            var sim = Fresh(out var unit, out _);
            sim.Issue(Command.Cover(ActorId.ForSide(unit.Side), unit.Id));
            sim.Step(DefendExecutor.DigInTicks + 10);

            if (unit.Posture != Posture.Covering)
            {
                log.AppendLine($"  dig-in: FAILED, posture {unit.Posture}");
                return false;
            }

            float cover = EngagementResolver.PostureFactor(Posture.Covering);
            float dug = EngagementResolver.PostureFactor(Posture.DugIn);
            float detect = UnitCapabilities.DetectionPostureFactor(Posture.Covering);

            if (Mathf.Abs(cover - dug) > 0.001f || detect > 1.001f)
            {
                log.AppendLine($"  dig-in: FAILED, fire {cover} detect {detect} — Cover is " +
                               "Defend-weight protection with no watch stretch");
                return false;
            }

            var queue = sim.QueueOf(unit.Id);
            if (queue == null || queue.IsEmpty || queue[0].Command.Kind != CommandKind.Cover)
            {
                log.AppendLine("  dig-in: FAILED, cover left the queue");
                return false;
            }

            log.AppendLine($"  dig-in: Covering fire {cover:0.00}, detection ×{detect:0.00}  ok");
            return true;
        }

        /// <summary>
        /// Below the break threshold with Cover on the queue, the unit must not Abort+MoveTo.
        /// </summary>
        private static bool DoesNotBreakContactWhileCovering(StringBuilder log)
        {
            var sim = Fresh(out var unit, out var hostile);
            if (unit == null || hostile == null)
            {
                log.AppendLine("  hold: FAILED, need friendly and hostile");
                return false;
            }

            var actor = ActorId.ForSide(unit.Side);
            sim.Issue(Command.Cover(actor, unit.Id));
            sim.Step(DefendExecutor.DigInTicks + 5);

            // Preempting Engage so BreakContact's engagingNow path is live — without an
            // engage head, ShouldBreakContact true still no-ops (see ReactionController).
            sim.Issue(Command.Engage(actor, unit.Id, hostile.Id, preempt: true));
            unit.Strength = ReactionController.BreakStrengthPercent - 5f;

            sim.EnableReactions();
            int ordersBefore = sim.Reactions.OrdersIssued;
            sim.Step(5);

            var queue = sim.QueueOf(unit.Id);
            bool stillCovering = false;
            if (queue != null)
            {
                for (int i = 0; i < queue.Count; i++)
                    if (queue[i].Command.Kind == CommandKind.Cover) { stillCovering = true; break; }
            }

            if (!stillCovering)
            {
                log.AppendLine("  hold: FAILED, Cover was discarded under fire below threshold");
                return false;
            }

            // BreakContact issues Abort + MoveTo. Neither should appear from the reflex.
            bool withdrew = false;
            if (queue != null)
            {
                for (int i = 0; i < queue.Count; i++)
                {
                    var k = queue[i].Command.Kind;
                    if (k == CommandKind.MoveTo || k == CommandKind.Abort) withdrew = true;
                }
            }

            if (withdrew)
            {
                log.AppendLine("  hold: FAILED, unit withdrew while Cover was on the plan");
                return false;
            }

            log.AppendLine($"  hold: Cover survives below {ReactionController.BreakStrengthPercent}% " +
                           $"(reactions +{sim.Reactions.OrdersIssued - ordersBefore} orders, " +
                           "no Abort/MoveTo)  ok");
            return true;
        }
    }
}
#endif
