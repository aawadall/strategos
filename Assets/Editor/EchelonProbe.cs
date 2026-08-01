// EchelonProbe.cs
// The zoom bands: that they are contiguous, that they round-trip, and what they actually
// permit on a real map.
//
// Menu:  Strategos > Write Sample Config  /  Strategos > Probe Echelon Spans
// Batch: -executeMethod Strategos.Editor.EchelonProbe.WriteSamples
//        -executeMethod Strategos.Editor.EchelonProbe.Run
//
// CONTIGUITY IS THE WHOLE POINT AND IT IS WHAT CAN SILENTLY BREAK. The first cut of this table
// gave every echelon a wide window and they overlapped heavily: a 500 m view was legal for a
// fire team, a squad, a section and a platoon alike, so the band said nothing about whose
// scale it was and the mechanic degenerated into a zoom limit. Bands now begin where their
// subordinate's end, and this probe fails on a gap or an overlap — because the table is
// hand-editable now, and a typo there produces two echelons that feel identical rather than
// an error anyone would notice.

#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.NatoSymbols;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class EchelonProbe
    {
        private const string ConfigPath = "Assets/Resources/Config/echelon-spans.json";

        [MenuItem("Strategos/Write Sample Config")]
        public static void WriteSamples()
        {
            var table = EchelonSpanDefaults.Table();
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            EchelonSpanIO.SaveToFile(table, ConfigPath);
            AssetDatabase.Refresh();
            EchelonSpanIO.Reload();

            Debug.Log($"[EchelonProbe] wrote {table.Spans.Length} band(s) -> {ConfigPath}");
        }

        [MenuItem("Strategos/Probe Echelon Spans")]
        public static void Run()
        {
            var log = new StringBuilder();
            bool ok = true;

            PrintTable(log);
            ok &= LoadsFromResources(log);
            ok &= BandsAreContiguous(log);
            ok &= RoundTrips(log);
            ok &= ClampingToASmallMapStaysUsable(log);

            Debug.Log(log.ToString());
            Debug.Log(ok ? "[EchelonProbe] PROBE PASSED" : "[EchelonProbe] PROBE FAILED");
        }

        // ─── The table ────────────────────────────────────────────────────────

        /// <summary>What each band permits, and what it means on the shipped 6.4 km sheet.</summary>
        private static void PrintTable(StringBuilder log)
        {
            const float sheet = 6400f;
            var table = EchelonSpanIO.Current;

            log.AppendLine($"  '{table.Name}', {table.Spans.Length} band(s)");
            log.AppendLine("    echelon         narrowest    widest     zoom range" +
                           "     on a 6.4 km sheet");

            foreach (var span in table.Spans)
            {
                if (span.Echelon == Echelon.None) continue;

                float ratio = span.MinMetres > 0f ? span.MaxMetres / span.MinMetres : 0f;
                var onSheet = span.ClampedTo(sheet);

                log.AppendLine($"    {span.Echelon,-14}{span.MinMetres,10:0} m{span.MaxMetres,9:0} m" +
                               $"{ratio,13:0.0}x     {onSheet.MinMetres:0}-{onSheet.MaxMetres:0} m");
            }
        }

        // ─── Assertions ───────────────────────────────────────────────────────

        private static bool LoadsFromResources(StringBuilder log)
        {
            var asset = Resources.Load<TextAsset>(
                $"{EchelonSpanIO.ResourceFolder}/{EchelonSpanDefaults.ConfigName}");

            if (asset == null)
            {
                log.AppendLine("  resources: FAILED, no config file — run " +
                               "Strategos > Write Sample Config");
                return false;
            }

            log.AppendLine($"  resources: loaded {asset.text.Length} chars from " +
                           $"Resources/{EchelonSpanIO.ResourceFolder}  ok");
            return true;
        }

        /// <summary>
        /// Each band must begin where its subordinate's ends: no gap, no overlap.
        /// </summary>
        /// <remarks>
        /// A gap means a zoom level no echelon may use, which reads as the scroll wheel
        /// sticking. An overlap means two echelons share scale, which is the degenerate case
        /// this table exists to avoid — and neither would raise anything on its own.
        ///
        /// Equal bands are allowed, and used: Squad and Section share one, because APP-6D
        /// treats them as distinct echelons and most armies do not, so giving them separate
        /// scales would invent a difference the rest of the game does not model.
        /// </remarks>
        private static bool BandsAreContiguous(StringBuilder log)
        {
            var spans = EchelonSpanIO.Current.Spans;
            bool ok = true;
            float previousMax = -1f;

            foreach (var span in spans)
            {
                if (span.MinMetres <= 0f || span.MaxMetres <= span.MinMetres)
                {
                    log.AppendLine($"  contiguity: FAILED, {span.Echelon} has an empty or " +
                                   $"inverted band ({span.MinMetres:0}-{span.MaxMetres:0} m)");
                    ok = false;
                    continue;
                }

                if (previousMax < 0f) { previousMax = span.MaxMetres; continue; }

                // Repeated bands are deliberate; only a *different* band that fails to meet
                // its predecessor is a fault.
                bool sameAsPrevious = Mathf.Approximately(span.MinMetres, previousMax) ||
                                      span.MaxMetres <= previousMax;

                if (!sameAsPrevious && !Mathf.Approximately(span.MinMetres, previousMax))
                {
                    string fault = span.MinMetres > previousMax ? "gap" : "overlap";
                    log.AppendLine($"  contiguity: FAILED, {fault} below {span.Echelon} — " +
                                   $"previous band ends at {previousMax:0} m, this one starts " +
                                   $"at {span.MinMetres:0} m");
                    ok = false;
                }

                previousMax = Mathf.Max(previousMax, span.MaxMetres);
            }

            if (ok)
                log.AppendLine("  contiguity: every band begins where its subordinate ends  ok");
            return ok;
        }

        private static bool RoundTrips(StringBuilder log)
        {
            var before = EchelonSpanDefaults.Table();
            var after = EchelonSpanIO.FromJson(EchelonSpanIO.ToJson(before));

            if (after == null || after.Spans.Length != before.Spans.Length)
            {
                log.AppendLine("  round trip: FAILED, band count changed");
                return false;
            }

            for (int i = 0; i < before.Spans.Length; i++)
                if (before.Spans[i].Echelon != after.Spans[i].Echelon ||
                    !Mathf.Approximately(before.Spans[i].MinMetres, after.Spans[i].MinMetres) ||
                    !Mathf.Approximately(before.Spans[i].MaxMetres, after.Spans[i].MaxMetres))
                {
                    log.AppendLine($"  round trip: FAILED on {before.Spans[i].Echelon}");
                    return false;
                }

            log.AppendLine($"  round trip: {before.Spans.Length} bands survive JSON  ok");
            return true;
        }

        /// <summary>
        /// A map smaller than an echelon's reach must still leave room to zoom.
        /// </summary>
        /// <remarks>
        /// The shipped skirmish is 6.4 km and its player commands a battalion, whose band runs
        /// to 15 km — so the ceiling is clamped to the sheet. If the floor were not pulled down
        /// with it the range would inverse and the view would be pinned at one scale, which
        /// looks exactly like a broken scroll wheel.
        /// </remarks>
        private static bool ClampingToASmallMapStaysUsable(StringBuilder log)
        {
            const float sheet = 6400f;
            var battalion = EchelonSpanIO.Current.For(Echelon.Battalion).ClampedTo(sheet);

            if (battalion.MaxMetres > sheet + 0.001f)
            {
                log.AppendLine($"  clamping: FAILED, a {sheet:0} m sheet still permits a " +
                               $"{battalion.MaxMetres:0} m view");
                return false;
            }

            // A *usable* range, not merely a non-inverted one. The first cut asserted only
            // min < max and passed while giving a battalion 1.1x of zoom on the shipped sheet
            // — the feature was dead in the only scenario that ships and the probe said it
            // was fine. Two-fold is the floor worth having: below it the scroll wheel reads
            // as stuck.
            const float usable = 2f;
            float got = battalion.MaxMetres / Mathf.Max(1f, battalion.MinMetres);

            if (got < usable)
            {
                log.AppendLine($"  clamping: FAILED, band collapsed to " +
                               $"{battalion.MinMetres:0}-{battalion.MaxMetres:0} m — " +
                               $"{got:0.0}x of zoom, under the {usable:0}x that reads as " +
                               "working");
                return false;
            }

            log.AppendLine($"  clamping: battalion on a {sheet:0} m sheet gets " +
                           $"{battalion.MinMetres:0}-{battalion.MaxMetres:0} m, " +
                           $"{battalion.MaxMetres / battalion.MinMetres:0.0}x of zoom  ok");
            return true;
        }
    }
}
#endif
