// ControlMeasureProbe.cs
// #161–#165 / #186: GCM authoring round-trip, validation, draw, and per-side visibility.
//
// Menu:  Strategos > Probe Control Measures
// Batch: -executeMethod Strategos.Editor.ControlMeasureProbe.Run

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
            bad += CheckViewerHidesOpposing(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[ControlMeasureProbe]\n" + log);
            else Debug.LogError("[ControlMeasureProbe]\n" + log);
        }

        private static int CheckSampleRoundTrip(StringBuilder log)
        {
            var sample = ScenarioSamples.Skirmish();
            if (sample.ControlMeasures == null || sample.ControlMeasures.Count < 7)
            {
                log.AppendLine($"  json: FAILED — sample has {sample.ControlMeasures?.Count ?? 0} GCMs (need >=7)");
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
                    a.Owner.Value != b.Owner.Value || a.Echelon != b.Echelon ||
                    a.AxisRole != b.AxisRole)
                {
                    log.AppendLine($"  json: FAILED — field mismatch at [{i}] {a} vs {b}");
                    return 1;
                }
                if (a.IsPointKind)
                {
                    if ((a.Cell - b.Cell).sqrMagnitude > 0.0001f ||
                        Mathf.Abs(a.RadiusCells - b.RadiusCells) > 0.0001f)
                    {
                        log.AppendLine($"  json: FAILED — checkpoint geometry [{i}]");
                        return 1;
                    }
                }
                else if ((a.Points?.Count ?? 0) != (b.Points?.Count ?? 0))
                {
                    log.AppendLine($"  json: FAILED — points {a.Points?.Count}→{b.Points?.Count}");
                    return 1;
                }
            }

            bool sawArrow = false, sawArea = false;
            for (int i = 0; i < back.ControlMeasures.Count; i++)
            {
                if (back.ControlMeasures[i].IsArrowKind) sawArrow = true;
                if (back.ControlMeasures[i].IsAreaKind) sawArea = true;
            }
            if (!sawArrow || !sawArea)
            {
                log.AppendLine($"  json: FAILED — need arrow+area in sample (arrow={sawArrow} area={sawArea})");
                return 1;
            }

            log.AppendLine($"  json: OK — {back.ControlMeasures.Count} GCMs round-trip " +
                           $"(incl. arrows/areas)");
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

            var shortArrow = ScenarioSamples.Skirmish();
            shortArrow.Map.EnableErosion = false;
            shortArrow.ControlMeasures.Add(new ControlMeasure
            {
                Id = 99,
                Kind = ControlMeasureKind.AxisOfAdvance,
                Name = "SHORT",
                Owner = shortArrow.Sides[0].Id,
                Points = { new Vector2(10f, 10f) },
            });
            problems = shortArrow.Validate(UnitCatalogue.Default());
            bool sawShort = false;
            for (int i = 0; i < problems.Count; i++)
                if (problems[i].Contains("needs at least 2 points")) sawShort = true;
            if (!sawShort)
            {
                log.AppendLine("  validate: FAILED — short axis not reported");
                return 1;
            }

            log.AppendLine($"  validate: OK — clean sample clean; dup + short axis caught");
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

            Func<SideId, Color32> ink = side =>
            {
                var s = scenario.FindSide(side);
                if (s == null) return new Color32(0, 0, 0, 255);
                var c = s.Colour;
                return new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), 255);
            };

            var painted = (Color32[])bare.Clone();
            ControlMeasureDrawer.Draw(painted, view, scenario.ControlMeasures, ink);

            int differ = CountDiff(bare, painted);
            if (differ < 80)
            {
                log.AppendLine($"  draw: FAILED — only {differ} pixels changed");
                return 1;
            }

            var pointsOnly = (Color32[])bare.Clone();
            var cps = new List<ControlMeasure>();
            for (int i = 0; i < scenario.ControlMeasures.Count; i++)
                if (scenario.ControlMeasures[i].Kind == ControlMeasureKind.Checkpoint)
                    cps.Add(scenario.ControlMeasures[i]);
            ControlMeasureDrawer.Draw(pointsOnly, view, cps, ink);
            int pointDiffer = CountDiff(bare, pointsOnly);

            var noArrowsAreas = new List<ControlMeasure>();
            for (int i = 0; i < scenario.ControlMeasures.Count; i++)
            {
                var m = scenario.ControlMeasures[i];
                if (!m.IsArrowKind && !m.IsAreaKind) noArrowsAreas.Add(m);
            }
            var basePaint = (Color32[])bare.Clone();
            ControlMeasureDrawer.Draw(basePaint, view, noArrowsAreas, ink);
            int baseDiffer = CountDiff(bare, basePaint);

            if (differ <= baseDiffer)
            {
                log.AppendLine($"  draw: FAILED — arrows/areas did not add ink " +
                               $"(all={differ}, without={baseDiffer})");
                return 1;
            }

            log.AppendLine($"  draw: OK — {differ} px (points {pointDiffer}; " +
                           $"lines/pts {baseDiffer}; arrows/areas +{differ - baseDiffer}); " +
                           $"sheet {view.Width}x{view.Height}");
            return 0;
        }

        private static int CheckViewerHidesOpposing(StringBuilder log)
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var map = scenario.GenerateMap();
            var options = MapRenderOptions.Default;
            options.PixelsPerCell = 1f;
            var bare = MapRasterizer.RenderPixels(map, options, out var view);

            Func<SideId, Color32> ink = _ => new Color32(255, 0, 0, 255);

            var all = (Color32[])bare.Clone();
            ControlMeasureDrawer.Draw(all, view, scenario.ControlMeasures, ink);

            var filtered = (Color32[])bare.Clone();
            ControlMeasureDrawer.Draw(filtered, view, scenario.ControlMeasures, ink,
                scenario.PlayerSide);

            int allDiff = CountDiff(bare, all);
            int filtDiff = CountDiff(bare, filtered);
            if (filtDiff >= allDiff)
            {
                log.AppendLine($"  fog: FAILED — viewer filter did not hide ink " +
                               $"(all={allDiff}, filtered={filtDiff}, player={scenario.PlayerSide})");
                return 1;
            }

            // Drawing only the red CP alone should account for the delta roughly.
            var redOnly = new List<ControlMeasure>();
            for (int i = 0; i < scenario.ControlMeasures.Count; i++)
                if (scenario.ControlMeasures[i].Owner != scenario.PlayerSide &&
                    scenario.ControlMeasures[i].Owner.IsValid)
                    redOnly.Add(scenario.ControlMeasures[i]);
            if (redOnly.Count == 0)
            {
                log.AppendLine("  fog: FAILED — sample has no opposing-owned GCM");
                return 1;
            }

            log.AppendLine($"  fog: OK — viewer hides opposing ({redOnly.Count} GCM(s)); " +
                           $"ink {allDiff}→{filtDiff}");
            return 0;
        }

        private static int CountDiff(Color32[] a, Color32[] b)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i++)
                if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b || a[i].a != b[i].a)
                    n++;
            return n;
        }
    }
}
#endif
