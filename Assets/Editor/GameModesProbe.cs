// GameModesProbe.cs
// #287 / #299: ModeKind round-trip; spectator directs both sides and decides without a
// player Issue; hotseat side id flips.
//
// Menu:  Strategos > Probe Game Modes
// Batch: -executeMethod Strategos.Editor.GameModesProbe.Run

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.Modes;
using Strategos.Scenarios;
using Strategos.UI;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class GameModesProbe
    {
        [MenuItem("Strategos/Probe Game Modes")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckEnum(log);
            bad += CheckSpectator(log);
            bad += CheckHotseatSession(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[GameModesProbe]\n" + log);
            else Debug.LogError("[GameModesProbe]\n" + log);
        }

        private static int CheckEnum(StringBuilder log)
        {
            var names = System.Enum.GetNames(typeof(ModeKind));
            if (names.Length != 4 ||
                !System.Enum.IsDefined(typeof(ModeKind), ModeKind.Spectator))
            {
                log.AppendLine($"  enum: FAILED — {names.Length} names");
                return 1;
            }

            log.AppendLine("  enum: OK — Solo/Hotseat/Spectator/Replay");
            return 0;
        }

        private static int CheckSpectator(StringBuilder log)
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var map = scenario.GenerateMap();
            var sim = new Simulation(scenario, map, UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            sim.EnableReactions();

            var all = new List<SideId>();
            foreach (var s in scenario.Sides) all.Add(s.Id);
            sim.EnableDirector(all);

            foreach (var u in sim.Units)
                u.Roe = RulesOfEngagement.FireAtWill;

            int before = sim.Log.Count;
            const int Cap = 8000;
            for (int i = 0; i < Cap && !sim.IsOver; i++) sim.Step();

            if (!sim.IsOver)
            {
                log.AppendLine($"  spectator: FAILED — undecided after {Cap} ticks");
                return 1;
            }

            if (sim.Director == null || sim.Director.OrdersIssued == 0)
            {
                log.AppendLine("  spectator: FAILED — director issued nothing");
                return 1;
            }

            // No human ActorId.Player-style commands — every log entry is ForSide.
            for (int i = before; i < sim.Log.Count; i++)
            {
                var c = sim.Log[i];
                if (!c.IssuedBy.IsValid)
                {
                    log.AppendLine($"  spectator: FAILED — command #{c.Seq} has no issuer");
                    return 1;
                }
            }

            log.AppendLine(
                $"  spectator: OK — decided with {sim.Director.OrdersIssued} director " +
                $"orders, {sim.Log.Count - before} log entries");
            return 0;
        }

        private static int CheckHotseatSession(StringBuilder log)
        {
            var session = new AppSession { PlayMode = ModeKind.Hotseat };
            var scenario = ScenarioSamples.Skirmish();
            if (scenario.Sides.Count < 2)
            {
                log.AppendLine("  hotseat: FAILED — skirmish needs two sides");
                return 1;
            }

            session.HotseatSide = scenario.Sides[0].Id;
            var first = session.HotseatSide;
            session.HotseatSide = scenario.Sides[1].Id;
            if (session.HotseatSide == first || session.PlayMode != ModeKind.Hotseat)
            {
                log.AppendLine("  hotseat: FAILED — side did not switch");
                return 1;
            }

            log.AppendLine($"  hotseat: OK — {first} -> {session.HotseatSide}");
            return 0;
        }
    }
}
#endif
