// ScenarioProbe.cs
// Verifies scenario serialisation headlessly.
//
// The interesting assertion is not that the JSON parses — it is that a round-tripped
// scenario still generates *the same ground*. Settings surviving a trip field-by-field is
// necessary but not sufficient: one float losing precision produces a subtly different map
// with no error anywhere, which would break shared scenarios and replay alike.
//
// Menu:  Strategos > Probe Scenario     /  Strategos > Write Sample Scenarios
// Batch: -executeMethod Strategos.Editor.ScenarioProbe.Run

#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Maps;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class ScenarioProbe
    {
        private const string ResourceDir = "Assets/Resources/Scenarios";

        [MenuItem("Strategos/Write Sample Scenarios")]
        public static void WriteSamples()
        {
            Directory.CreateDirectory(ResourceDir);
            var path = Path.Combine(ResourceDir, ScenarioSamples.SkirmishName + ".json");
            ScenarioIO.SaveToFile(ScenarioSamples.Skirmish(), path);
            AssetDatabase.Refresh();
            Debug.Log($"[ScenarioProbe] wrote {path}");
        }

        [MenuItem("Strategos/Probe Scenario")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckRoundTrip(log);
            bad += CheckMapReproduces(log);
            bad += CheckShippedFixture(log);
            bad += CheckValidation(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[ScenarioProbe]\n" + log);
            else Debug.LogError("[ScenarioProbe]\n" + log);
        }

        // ─── Round trip ───────────────────────────────────────────────────────

        private static int CheckRoundTrip(StringBuilder log)
        {
            int bad = 0;
            var original = ScenarioSamples.Skirmish();

            var problems = original.Validate();
            if (problems.Count > 0)
            {
                foreach (var p in problems) log.AppendLine($"  FAIL sample invalid: {p}");
                bad += problems.Count;
            }

            string json = ScenarioIO.ToJson(original);
            var back = ScenarioIO.FromJson(json);

            if (back == null)
            {
                log.AppendLine("  FAIL round trip produced null");
                return bad + 1;
            }

            log.AppendLine($"  serialised to {json.Length} bytes");

            bad += Compare(original, back, log, "round trip");

            // Serialising twice must give identical text, or the format is not stable and
            // diffs of committed scenarios become noise.
            if (ScenarioIO.ToJson(back) != json)
            {
                log.AppendLine("  FAIL re-serialising the round trip produced different text");
                bad++;
            }

            return bad;
        }

        private static int Compare(Scenario a, Scenario b, StringBuilder log, string what)
        {
            int bad = 0;

            if (a.Name != b.Name) { log.AppendLine($"  FAIL {what}: name"); bad++; }
            if (a.Description != b.Description) { log.AppendLine($"  FAIL {what}: description"); bad++; }
            if (a.FormatVersion != b.FormatVersion) { log.AppendLine($"  FAIL {what}: format version"); bad++; }

            // Map settings — the fields that change the terrain.
            var (ma, mb) = (a.Map, b.Map);
            if (ma.Seed != mb.Seed) { log.AppendLine($"  FAIL {what}: seed"); bad++; }
            if (ma.Width != mb.Width || ma.Height != mb.Height) { log.AppendLine($"  FAIL {what}: extent"); bad++; }
            if (!Mathf.Approximately(ma.MetresPerCell, mb.MetresPerCell)) { log.AppendLine($"  FAIL {what}: metres per cell"); bad++; }
            if (ma.Profile != mb.Profile) { log.AppendLine($"  FAIL {what}: profile"); bad++; }
            if (ma.EnableErosion != mb.EnableErosion) { log.AppendLine($"  FAIL {what}: erosion flag"); bad++; }
            if (ma.EnableCulture != mb.EnableCulture) { log.AppendLine($"  FAIL {what}: culture flag"); bad++; }
            if (ma.ParameterOverride.HasValue != mb.ParameterOverride.HasValue)
            {
                log.AppendLine($"  FAIL {what}: ParameterOverride nullability lost " +
                               $"({ma.ParameterOverride.HasValue} -> {mb.ParameterOverride.HasValue})");
                bad++;
            }

            if (a.Sides.Count != b.Sides.Count) { log.AppendLine($"  FAIL {what}: side count"); bad++; }
            else for (int i = 0; i < a.Sides.Count; i++)
            {
                var (sa, sb) = (a.Sides[i], b.Sides[i]);
                if (sa.Id != sb.Id || sa.Name != sb.Name || sa.Affiliation != sb.Affiliation)
                { log.AppendLine($"  FAIL {what}: side {i}"); bad++; }
                if (sa.Colour != sb.Colour) { log.AppendLine($"  FAIL {what}: side {i} colour"); bad++; }
            }

            if (a.Units.Count != b.Units.Count) { log.AppendLine($"  FAIL {what}: unit count"); bad++; }
            else for (int i = 0; i < a.Units.Count; i++)
            {
                var (ua, ub) = (a.Units[i], b.Units[i]);
                if (ua.Id != ub.Id) { log.AppendLine($"  FAIL {what}: unit {i} id"); bad++; }
                if (ua.Side != ub.Side) { log.AppendLine($"  FAIL {what}: unit {i} side"); bad++; }
                if (ua.ParentId != ub.ParentId) { log.AppendLine($"  FAIL {what}: unit {i} parent"); bad++; }
                if (ua.Sidc != ub.Sidc) { log.AppendLine($"  FAIL {what}: unit {i} sidc"); bad++; }
                if (ua.Designation != ub.Designation) { log.AppendLine($"  FAIL {what}: unit {i} designation"); bad++; }
                if (ua.HigherFormation != ub.HigherFormation) { log.AppendLine($"  FAIL {what}: unit {i} formation"); bad++; }
                if (ua.Strength != ub.Strength) { log.AppendLine($"  FAIL {what}: unit {i} strength"); bad++; }
                if (ua.CapabilityId != ub.CapabilityId) { log.AppendLine($"  FAIL {what}: unit {i} capability id"); bad++; }
                if (ua.Posture != ub.Posture) { log.AppendLine($"  FAIL {what}: unit {i} posture"); bad++; }
                if (!Mathf.Approximately(ua.Readiness, ub.Readiness)) { log.AppendLine($"  FAIL {what}: unit {i} readiness"); bad++; }
                if (!Mathf.Approximately(ua.Suppression, ub.Suppression)) { log.AppendLine($"  FAIL {what}: unit {i} suppression"); bad++; }
                if (!Mathf.Approximately(ua.Supply.Ammunition, ub.Supply.Ammunition) ||
                    !Mathf.Approximately(ua.Supply.Fuel, ub.Supply.Fuel))
                { log.AppendLine($"  FAIL {what}: unit {i} supply"); bad++; }
                // Exact, not approximate: a position that drifts on every save would walk.
                if (ua.Cell != ub.Cell)
                { log.AppendLine($"  FAIL {what}: unit {i} cell {ua.Cell} -> {ub.Cell}"); bad++; }
            }

            return bad;
        }

        // ─── The assertion that matters ───────────────────────────────────────

        /// <summary>
        /// A round-tripped scenario must generate byte-identical terrain. Field equality is
        /// necessary but not sufficient — a float losing precision in the text produces a
        /// subtly different map with no error raised anywhere.
        /// </summary>
        private static int CheckMapReproduces(StringBuilder log)
        {
            int bad = 0;

            var original = ScenarioSamples.Skirmish();
            var back = ScenarioIO.FromJson(ScenarioIO.ToJson(original));

            // Erosion is the dominant cost and is not what is under test here.
            original.Map.EnableErosion = false;
            back.Map.EnableErosion = false;

            var mapA = original.GenerateMap();
            var mapB = back.GenerateMap();

            if (mapA.Width != mapB.Width || mapA.Height != mapB.Height)
            {
                log.AppendLine("  FAIL regenerated map has different dimensions");
                return bad + 1;
            }

            int differing = 0;
            float worst = 0f;
            for (int i = 0; i < mapA.Elevation.Length; i++)
            {
                float d = Mathf.Abs(mapA.Elevation[i] - mapB.Elevation[i]);
                if (d != 0f) { differing++; worst = Mathf.Max(worst, d); }
            }

            if (differing > 0)
            {
                log.AppendLine($"  FAIL {differing} of {mapA.Elevation.Length} cells differ " +
                               $"after a round trip (worst {worst:0.######} m)");
                bad++;
            }

            int landcoverDiff = 0;
            for (int i = 0; i < mapA.Landcover.Length; i++)
                if (mapA.Landcover[i] != mapB.Landcover[i]) landcoverDiff++;
            if (landcoverDiff > 0)
            {
                log.AppendLine($"  FAIL {landcoverDiff} landcover cells differ after a round trip");
                bad++;
            }

            log.AppendLine($"  regenerated {mapA.Width}x{mapA.Height} identically: " +
                           $"{mapA.Elevation.Length} elevation + {mapA.Landcover.Length} landcover cells");
            return bad;
        }

        // ─── Shipped fixture ──────────────────────────────────────────────────

        /// <summary>
        /// The committed JSON must still match what the code builds. Code is the source of
        /// truth and the file is its rendering, so a model change that breaks the shipped
        /// scenario fails here rather than in front of a player.
        /// </summary>
        private static int CheckShippedFixture(StringBuilder log)
        {
            var shipped = ScenarioIO.Load(ScenarioSamples.SkirmishName);
            if (shipped == null)
            {
                log.AppendLine($"  FAIL no shipped scenario '{ScenarioSamples.SkirmishName}' " +
                               $"in Resources/{ScenarioIO.ResourceFolder} " +
                               $"(run Strategos > Write Sample Scenarios)");
                return 1;
            }

            int bad = Compare(ScenarioSamples.Skirmish(), shipped, log, "shipped fixture");

            var problems = shipped.Validate();
            foreach (var p in problems) log.AppendLine($"  FAIL shipped fixture invalid: {p}");
            bad += problems.Count;

            log.AppendLine($"  shipped fixture: {shipped}");
            foreach (var side in shipped.Sides)
                log.AppendLine($"    {side.Name,-8} {side.Affiliation,-8} " +
                               $"{shipped.CountUnitsOf(side.Id)} units");

            return bad;
        }

        // ─── Validation ───────────────────────────────────────────────────────

        /// <summary>
        /// Validation only earns its place if it actually catches things, so assert the
        /// failures too. Each of these is otherwise silent at runtime: a unit on a
        /// nonexistent side belongs to nobody, and one off the map is never drawn.
        /// </summary>
        private static int CheckValidation(StringBuilder log)
        {
            int bad = 0;

            bad += ExpectProblem(log, "unit on a nonexistent side", s =>
                s.Units[0].Side = new SideId(99));

            bad += ExpectProblem(log, "unit off the map", s =>
                s.Units[0].Cell = new Vector2(s.Map.Width + 50f, 10f));

            bad += ExpectProblem(log, "duplicate unit ids", s =>
                s.Units[1].Id = s.Units[0].Id);

            bad += ExpectProblem(log, "unparseable SIDC", s =>
                s.Units[0].Sidc = "not-a-sidc");

            bad += ExpectProblem(log, "only one side", s =>
            {
                s.Sides.RemoveAt(1);
                s.Units.RemoveAll(u => u.Side == new SideId(2));
            });

            bad += ExpectProblem(log, "unit as its own parent", s =>
                s.Units[0].ParentId = s.Units[0].Id);

            // Only checked when a catalogue is supplied, so it needs its own case.
            var typo = ScenarioSamples.Skirmish();
            typo.Units[0].CapabilityId = "inf-mecha";
            var caught = typo.Validate(UnitCatalogue.Default());
            if (caught.Count == 0)
            {
                log.AppendLine("  FAIL validation missed: unknown capability id");
                bad++;
            }
            else
            {
                log.AppendLine($"  validation catches {"unknown capability id",-28} -> \"{caught[0]}\"");
            }

            // And the shipped fixture's ids must all resolve, and every unit must be
            // standing somewhere its own capabilities can actually occupy. The generator
            // makes a lot of standing water, so a plausible coordinate lands in a lake more
            // often than expected — and the symbol draws fine on top of it.
            var sample = ScenarioSamples.Skirmish();
            var sampleMap = sample.GenerateMap();
            var shippedProblems = sample.Validate(UnitCatalogue.Default(), sampleMap);
            foreach (var p2 in shippedProblems)
            {
                log.AppendLine($"  FAIL sample fails full validation: {p2}");
                bad++;
            }
            log.AppendLine($"  all {sample.Units.Count} sample units start on passable ground");

            // And prove the passability check actually fires.
            var drowned = ScenarioSamples.Skirmish();
            drowned.Units[0].Cell = FindCell(sampleMap, LandcoverClass.Water);
            if (drowned.Units[0].Cell.x >= 0f)
            {
                var caught2 = drowned.Validate(UnitCatalogue.Default(), sampleMap);
                if (caught2.Count == 0)
                {
                    log.AppendLine("  FAIL validation missed: unit standing in water");
                    bad++;
                }
                else
                {
                    log.AppendLine($"  validation catches {"unit in impassable terrain",-28} -> \"{caught2[0]}\"");
                }
            }

            return bad;
        }

        /// <summary>First cell of the given landcover, or (-1,-1) if the map has none.</summary>
        private static Vector2 FindCell(MapData map, LandcoverClass want)
        {
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                if (map.GetLandcover(x, y) == want) return new Vector2(x, y);
            return new Vector2(-1f, -1f);
        }

        private static int ExpectProblem(StringBuilder log, string what,
            System.Action<Scenario> breakIt)
        {
            var s = ScenarioSamples.Skirmish();
            breakIt(s);
            var problems = s.Validate();
            if (problems.Count == 0)
            {
                log.AppendLine($"  FAIL validation missed: {what}");
                return 1;
            }
            log.AppendLine($"  validation catches {what,-28} -> \"{problems[0]}\"");
            return 0;
        }
    }
}
#endif
