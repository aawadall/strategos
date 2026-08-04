// ControlMeasureProbe.cs
// #161–#163: GCM authoring round-trip, validation, and that drawing changes pixels.
//
// Menu:  Strategos > Probe Control Measures
// Batch: -executeMethod Strategos.Editor.ControlMeasureProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.ControlMeasures;
using Strategos.Maps;
using Strategos.NatoSymbols;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class ControlMeasureProbe
    {
        [MenuItem("Strategos/Probe Control Measures")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckSampleRoundTrip(log);
            bad += CheckValidation(log);
            bad += CheckDrawChangesPixels(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[ControlMeasureProbe]\n" + log);
            else Debug.LogError("[ControlMeasureProbe]\n" + log);
        }

        private static int CheckSampleRoundTrip(StringBuilder log)
        {
            var sample = ScenarioSamples.Skirmish();
            if (sample.ControlMeasures == null || sample.ControlMeasures.Count < 3)
            {
                log.AppendLine($"  json: FAILED — sample has {sample.ControlMeasures?.Count ?? 0} GCMs");
                return 1;
            }

            string json = ScenarioIO.ToJson(sample);
            var back = ScenarioIO.FromJson(json);
            if (back == null || back.ControlMeasures.Count != sample.ControlMeasures.Count)
            {
                log.AppendLine($"  json: FAILED — round-trip count " +
                               $"{sample.ControlMeasures.Count}→{back?.ControlMeasures.Count}");
                return 1;
            }

            for (int i = 0; i < sample.ControlMeasures.Count; i++)
            {
                var a = sample.ControlMeasures[i];
                var b = back.ControlMeasures[i];
                if (a.Id != b.Id || a.Kind != b.Kind || a.Name != b.Name ||
                    a.Owner.Value != b.Owner.Value || a.Echelon != b.Echelon)
                {
                    log.AppendLine($"  json: FAILED — field mismatch at [{i}] {a} vs {b}");
                    return 1;
                }
                if (a.IsLineKind)
                {
                    if (a.Points.Count != b.Points.Count)
                    {
                        log.AppendLine($"  json: FAILED — points {a.Points.Count}→{b.Points.Count}");
                        return 1;
                    }
                }
                else if ((a.Cell - b.Cell).sqrMagnitude > 0.0001f ||
                         Mathf.Abs(a.RadiusCells - b.RadiusCells) > 0.0001f)
                {
                    log.AppendLine($"  json: FAILED — checkpoint geometry [{i}]");
                    return 1;
                }
            }

            log.AppendLine($"  json: OK — {back.ControlMeasures.Count} GCMs round-trip " +
                           $"(kinds {back.ControlMeasures[0].Kind}/{back.ControlMeasures[1].Kind}/" +
                           $"{back.ControlMeasures[2].Kind})");
            return 0;
        }

        private static int CheckValidation(StringBuilder log)
        {
            var s = ScenarioSamples.Skirmish();
            s.Map.EnableErosion = false;
            var problems = s.Validate(UnitCatalogue.Default());
            if (problems.Count != 0)
            {
                log.AppendLine($"  validate: FAILED — clean sample has {problems.Count} problem(s): " +
                               problems[0]);
                return 1;
            }

            s.ControlMeasures.Add(new ControlMeasure
            {
                Id = 1, // duplicate
                Kind = ControlMeasureKind.Checkpoint,
                Name = "DUP",
                Owner = new SideId(1),
                Cell = new Vector2(10f, 10f),
            });
            problems = s.Validate(UnitCatalogue.Default());
            bool sawDup = false;
            for (int i = 0; i < problems.Count; i++)
                if (problems[i].Contains("Duplicate control measure")) sawDup = true;
            if (!sawDup)
            {
                log.AppendLine("  validate: FAILED — duplicate id not reported");
                return 1;
            }

            log.AppendLine($"  validate: OK — clean sample clean; duplicate id caught " +
                           $"({problems.Count} problem(s) on foul)");
            return 0;
        }

        private static int CheckDrawChangesPixels(StringBuilder log)
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var map = scenario.GenerateMap();

            var options = MapRenderOptions.Default;
            options.PixelsPerCell = 1f;

            var bare = MapRasterizer.RenderPixels(map, options, out var view);
            var painted = (Color32[])bare.Clone();
            ControlMeasureDrawer.Draw(painted, view, scenario.ControlMeasures, side =>
            {
                var s = scenario.FindSide(side);
                if (s == null) return new Color32(0, 0, 0, 255);
                var c = s.Colour;
                return new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), 255);
            });

            int differ = 0;
            for (int i = 0; i < bare.Length; i++)
                if (bare[i].r != painted[i].r || bare[i].g != painted[i].g ||
                    bare[i].b != painted[i].b || bare[i].a != painted[i].a)
                    differ++;

            if (differ < 50)
            {
                log.AppendLine($"  draw: FAILED — only {differ} pixels changed (need checkpoint+lines)");
                return 1;
            }

            // Point-only vs full set — lines should add more ink.
            var pointsOnly = (Color32[])bare.Clone();
            var cps = new System.Collections.Generic.List<ControlMeasure>();
            for (int i = 0; i < scenario.ControlMeasures.Count; i++)
                if (scenario.ControlMeasures[i].Kind == ControlMeasureKind.Checkpoint)
                    cps.Add(scenario.ControlMeasures[i]);
            ControlMeasureDrawer.Draw(pointsOnly, view, cps);

            int pointDiffer = 0;
            for (int i = 0; i < bare.Length; i++)
                if (bare[i].r != pointsOnly[i].r || bare[i].g != pointsOnly[i].g ||
                    bare[i].b != pointsOnly[i].b)
                    pointDiffer++;

            if (differ <= pointDiffer)
            {
                log.AppendLine($"  draw: FAILED — lines did not add ink " +
                               $"(all={differ}, points={pointDiffer})");
                return 1;
            }

            log.AppendLine($"  draw: OK — {differ} pixels painted " +
                           $"(checkpoint alone {pointDiffer}; lines +{differ - pointDiffer}); " +
                           $"sheet {view.Width}x{view.Height}");
            return 0;
        }
    }
}
#endif
