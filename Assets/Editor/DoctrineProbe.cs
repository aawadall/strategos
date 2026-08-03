// DoctrineProbe.cs
// Writes the shipped doctrine pack, and asserts that it survives a round trip.
//
// Menu:  Strategos > Write Sample Drills  /  Strategos > Probe Doctrine
// Batch: -executeMethod Strategos.Editor.DoctrineProbe.WriteSamples
//        -executeMethod Strategos.Editor.DoctrineProbe.Run
//
// The round trip is the point. Drills are content now rather than code, so the failure mode
// changed: a field that does not serialise no longer fails to compile, it silently loads as
// its default and the binder shows a drill with no steps or a figure with no elements. That is
// invisible in a screenshot of a *different* drill and would be found by a player. The probe
// compares every field of every drill, and every point of every figure element, across a
// serialise-deserialise cycle.
//
// It also prints the readiness matrix. Per the project's rule, the table is the useful output:
// a rating that looks wrong for a unit is how a bad threshold gets noticed, and the
// assertions cannot tell a defensible T from an indefensible one.

#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Doctrine;
using Strategos.Scenarios;

namespace Strategos.Editor
{
    public static class DoctrineProbe
    {
        private const string PackPath = "Assets/Resources/Doctrine/field-drills.json";

        [MenuItem("Strategos/Write Sample Drills")]
        public static void WriteSamples()
        {
            var pack = DoctrineSamples.Pack();
            Directory.CreateDirectory(Path.GetDirectoryName(PackPath));
            TtpIO.SaveToFile(pack, PackPath);
            AssetDatabase.Refresh();
            TtpLibrary.Reload();

            Debug.Log($"[DoctrineProbe] wrote {pack.Drills.Length} drill(s) -> {PackPath}");
        }

        [MenuItem("Strategos/Probe Doctrine")]
        public static void Run()
        {
            bool ok = true;
            var log = new StringBuilder();

            ok &= RoundTrip(log);
            ok &= LoadsFromResources(log);
            ok &= PrintSyntheticReadiness(log);
            ok &= PickerLabelsTrackSelection(log);
            PrintReadiness(log);

            Debug.Log(log.ToString());
            Debug.Log(ok ? "[DoctrineProbe] PROBE PASSED" : "[DoctrineProbe] PROBE FAILED");
        }

        // ─── Round trip ───────────────────────────────────────────────────────

        private static bool RoundTrip(StringBuilder log)
        {
            var before = DoctrineSamples.Pack();
            var json = TtpIO.ToJson(before);
            var after = TtpIO.FromJson(json);

            if (after == null)
            {
                log.AppendLine("  round trip: FAILED, deserialised to null");
                return false;
            }

            if (after.Drills.Length != before.Drills.Length)
            {
                log.AppendLine($"  round trip: FAILED, {before.Drills.Length} drills in, " +
                               $"{after.Drills.Length} out");
                return false;
            }

            int steps = 0, figures = 0, elements = 0;

            for (int i = 0; i < before.Drills.Length; i++)
            {
                var a = before.Drills[i];
                var b = after.Drills[i];

                if (a.Code != b.Code || a.Name != b.Name || a.Summary != b.Summary ||
                    a.NotWhen != b.NotWhen || a.Echelon != b.Echelon || a.Series != b.Series)
                {
                    log.AppendLine($"  round trip: FAILED on drill {a.Code}, scalar mismatch");
                    return false;
                }

                if (a.Steps.Length != b.Steps.Length)
                {
                    log.AppendLine($"  round trip: FAILED on drill {a.Code}, " +
                                   $"{a.Steps.Length} steps in, {b.Steps.Length} out");
                    return false;
                }

                for (int s = 0; s < a.Steps.Length; s++)
                {
                    if (a.Steps[s].Text != b.Steps[s].Text || a.Steps[s].Kind != b.Steps[s].Kind)
                    {
                        log.AppendLine($"  round trip: FAILED on {a.Code} step {s + 1}");
                        return false;
                    }
                    steps++;
                }

                // A figure is the part most likely to be lost quietly: it is nested, it is the
                // only Vector2 in the model, and a drill without one is legitimate.
                if ((a.Diagram == null) != (b.Diagram == null))
                {
                    log.AppendLine($"  round trip: FAILED on {a.Code}, figure presence changed");
                    return false;
                }

                if (a.Diagram == null) continue;
                figures++;

                if (a.Diagram.Caption != b.Diagram.Caption ||
                    a.Diagram.Elements.Length != b.Diagram.Elements.Length)
                {
                    log.AppendLine($"  round trip: FAILED on {a.Code}, figure shape changed");
                    return false;
                }

                for (int e = 0; e < a.Diagram.Elements.Length; e++)
                {
                    var ea = a.Diagram.Elements[e];
                    var eb = b.Diagram.Elements[e];

                    if (ea.Kind != eb.Kind || ea.Label != eb.Label ||
                        ea.Points.Length != eb.Points.Length)
                    {
                        log.AppendLine($"  round trip: FAILED on {a.Code} element {e}");
                        return false;
                    }

                    for (int pt = 0; pt < ea.Points.Length; pt++)
                        if (Vector2.Distance(ea.Points[pt], eb.Points[pt]) > 1e-4f)
                        {
                            log.AppendLine($"  round trip: FAILED on {a.Code} element {e} " +
                                           $"point {pt}: {ea.Points[pt]} vs {eb.Points[pt]}");
                            return false;
                        }
                }

                elements += a.Diagram.Elements.Length;
            }

            log.AppendLine($"  round trip: {before.Drills.Length} drills, {steps} steps, " +
                           $"{figures} figures, {elements} elements  ok");
            log.AppendLine($"  pack size: {json.Length} chars");
            return true;
        }

        /// <summary>
        /// Asserts the app is reading the *file*, not the in-code fallback.
        /// </summary>
        /// <remarks>
        /// Worth its own check because the fallback is by design indistinguishable on screen —
        /// it is the same drills — so a pack that failed to ship would look completely normal.
        /// </remarks>
        private static bool LoadsFromResources(StringBuilder log)
        {
            var pack = TtpIO.Load(DoctrineSamples.PackName);
            if (pack == null || pack.Drills.Length == 0)
            {
                log.AppendLine($"  resources: FAILED, no pack at Resources/" +
                               $"{TtpIO.ResourceFolder}/{DoctrineSamples.PackName} " +
                               "(run Strategos > Write Sample Drills)");
                return false;
            }

            log.AppendLine($"  resources: loaded '{pack.Name}' ({pack.Source}), " +
                           $"{pack.Drills.Length} drill(s)  ok");
            return true;
        }

        // ─── Readiness ────────────────────────────────────────────────────────

        /// <summary>
        /// The T/P/U matrix over synthetic units spanning echelon and condition.
        /// </summary>
        /// <remarks>
        /// **The scenario matrix below cannot validate this feature and this one can.** Every
        /// unit in the sample scenario is a fresh company or platoon, so the echelon gate never
        /// fires and the condition gate never fires, and the table comes out uniformly T — a
        /// table that would look identical if `Assess` returned Trained unconditionally.
        ///
        /// Constructing the cases instead is the same choice CombatProbe makes when it stamps
        /// landcover onto one fixed pair of cells rather than hunting the map for a forest:
        /// vary one thing at a time, so a number can be attributed to something.
        /// </remarks>
        private static bool PrintSyntheticReadiness(StringBuilder log)
        {
            var cases = new (string Label, Strategos.NatoSymbols.Echelon Echelon, float Strength,
                float Suppression)[]
            {
                ("team, fresh",       Strategos.NatoSymbols.Echelon.Team,    100f, 0f),
                ("squad, fresh",      Strategos.NatoSymbols.Echelon.Squad,   100f, 0f),
                ("platoon, fresh",    Strategos.NatoSymbols.Echelon.Platoon, 100f, 0f),
                ("platoon, 75%",      Strategos.NatoSymbols.Echelon.Platoon,  75f, 0f),
                ("platoon, 50%",      Strategos.NatoSymbols.Echelon.Platoon,  50f, 0f),
                ("platoon, pinned",   Strategos.NatoSymbols.Echelon.Platoon, 100f, 60f),
                ("platoon, destroyed",Strategos.NatoSymbols.Echelon.Platoon,   0f, 0f),
            };

            var drills = TtpLibrary.All;

            log.AppendLine();
            log.AppendLine("  readiness by echelon and condition (constructed cases)");
            var header = new StringBuilder("    case                ");
            for (int d = 0; d < drills.Count; d++) header.Append(drills[d].Code.PadLeft(4));
            log.AppendLine(header.ToString());

            bool sawTrained = false, sawPractice = false, sawUntrained = false;

            foreach (var c in cases)
            {
                var unit = new Strategos.Units.UnitInstance
                {
                    Sidc = Strategos.NatoSymbols.SIDCBuilder.Build(
                        Strategos.NatoSymbols.Affiliation.Friend, c.Echelon,
                        entityCode: 11, entityType: 0).Raw,
                    Strength = c.Strength,
                    Readiness = 100f,
                    Suppression = c.Suppression,
                };

                var row = new StringBuilder("    " + c.Label.PadRight(20));
                for (int d = 0; d < drills.Count; d++)
                {
                    var a = TtpReadiness.Assess(drills[d], unit);
                    row.Append(a.Code.PadLeft(4));
                    sawTrained |= a.Rating == DrillRating.Trained;
                    sawPractice |= a.Rating == DrillRating.Practice;
                    sawUntrained |= a.Rating == DrillRating.Untrained;
                }
                log.AppendLine(row.ToString());
            }

            // A matrix that never produces one of the three ratings is not exercising the
            // thresholds, whatever it prints.
            if (sawTrained && sawPractice && sawUntrained) return true;

            log.AppendLine($"  readiness: FAILED, matrix produced T={sawTrained} " +
                           $"P={sawPractice} U={sawUntrained} — a rating never appears, so " +
                           "the thresholds are untested");
            return false;
        }

        /// <summary>
        /// PLAY's drill dropdown labels must change with the selected unit (#97).
        /// </summary>
        /// <remarks>
        /// The failure mode is a one-shot list built at Build() that never consults
        /// <see cref="TtpReadiness"/> — a probe that only asserts "a letter is present"
        /// would pass before and after. This one builds labels for two units that Assess
        /// rates differently on the same drill, and fails if either label does not carry
        /// that unit's own code, or if the two labels are identical.
        /// </remarks>
        private static bool PickerLabelsTrackSelection(StringBuilder log)
        {
            var drills = TtpLibrary.All;
            if (drills.Count == 0)
            {
                log.AppendLine("  picker labels: FAILED, no drills loaded");
                return false;
            }

            // Fresh platoon → T on platoon tasks; destroyed platoon → U on everything.
            var trained = new Strategos.Units.UnitInstance
            {
                Sidc = Strategos.NatoSymbols.SIDCBuilder.Build(
                    Strategos.NatoSymbols.Affiliation.Friend,
                    Strategos.NatoSymbols.Echelon.Platoon,
                    entityCode: 11, entityType: 0).Raw,
                Strength = 100f,
                Readiness = 100f,
            };
            var untrained = new Strategos.Units.UnitInstance
            {
                Sidc = trained.Sidc,
                Strength = 0f,
                Readiness = 100f,
            };

            Ttp drill = null;
            for (int i = 0; i < drills.Count; i++)
            {
                if (drills[i].Echelon == DrillEchelon.Platoon)
                {
                    drill = drills[i];
                    break;
                }
            }
            if (drill == null) drill = drills[0];

            var a = TtpReadiness.Assess(drill, trained);
            var b = TtpReadiness.Assess(drill, untrained);
            if (a.Code == b.Code)
            {
                log.AppendLine($"  picker labels: FAILED, Assess rated both units {a.Code} " +
                               $"on {drill.Code} — fixture does not differ");
                return false;
            }

            string labelA = TtpReadiness.PickerLabel(drill, trained);
            string labelB = TtpReadiness.PickerLabel(drill, untrained);

            if (!labelA.EndsWith("  ·  " + a.Code) || !labelB.EndsWith("  ·  " + b.Code))
            {
                log.AppendLine($"  picker labels: FAILED, label does not carry Assess code — " +
                               $"'{labelA}' vs {a.Code}, '{labelB}' vs {b.Code}");
                return false;
            }

            if (labelA == labelB)
            {
                log.AppendLine($"  picker labels: FAILED, labels identical across selection " +
                               $"('{labelA}') — would not update when the player changes unit");
                return false;
            }

            log.AppendLine($"  picker labels: {drill.Code}  '{labelA}' / '{labelB}'  ok");
            return true;
        }

        /// <summary>
        /// The T/P/U matrix for the sample scenario. Read the table, not the pass.
        /// </summary>
        private static void PrintReadiness(StringBuilder log)
        {
            var scenario = ScenarioIO.Load(ScenarioSamples.SkirmishName);
            if (scenario == null)
            {
                log.AppendLine("  readiness: no sample scenario, skipped");
                return;
            }

            var drills = TtpLibrary.All;
            log.AppendLine();
            log.AppendLine("  readiness of the sample force (informational: all fresh, so all T)");

            var header = new StringBuilder("    unit               ");
            for (int d = 0; d < drills.Count; d++) header.Append(drills[d].Code.PadLeft(4));
            log.AppendLine(header.ToString());

            foreach (var unit in scenario.Units)
            {
                var row = new StringBuilder("    " +
                    (string.IsNullOrEmpty(unit.Designation) ? unit.Id.ToString() : unit.Designation)
                    .PadRight(19));

                for (int d = 0; d < drills.Count; d++)
                    row.Append(TtpReadiness.Assess(drills[d], unit).Code.PadLeft(4));

                row.Append("    ").Append(TtpReadiness.EchelonOf(unit))
                   .Append("  eff ").Append((unit.Effectiveness * 100f).ToString("0"));
                log.AppendLine(row.ToString());
            }
        }
    }
}
#endif
