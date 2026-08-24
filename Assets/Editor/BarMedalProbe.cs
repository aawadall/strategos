// BarMedalProbe.cs
// #467: catalogue load, baker pixels, PostBattleReviewer grants, panel Build/Open/Close.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Medals;
using Strategos.UI;
using Strategos.Commands;
using Strategos.Direction;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class BarMedalProbe
    {
        [MenuItem("Strategos/Probe Bar Medals")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            var catalog = BarMedalIO.Load();
            if (catalog == null || catalog.Medals == null || catalog.Medals.Length < 2)
            {
                log.AppendLine("  FAIL load alpha-medals");
                bad++;
            }
            else
            {
                log.AppendLine($"  Catalog '{catalog.Name}' ({catalog.Medals.Length} medals)");
                if (BarMedalIO.Find(catalog, BarMedalIO.EnemyNeutralizedId) == null)
                {
                    log.AppendLine("  FAIL missing enemy-neutralized");
                    bad++;
                }
                if (BarMedalIO.Find(catalog, BarMedalIO.ObjectiveSecuredId) == null)
                {
                    log.AppendLine("  FAIL missing objective-secured");
                    bad++;
                }
            }

            var enemyDef = BarMedalIO.Find(catalog, BarMedalIO.EnemyNeutralizedId);
            if (enemyDef != null)
            {
                var sprite = BarMedalBaker.For(enemyDef, 3);
                if (sprite == null || sprite.texture == null)
                {
                    log.AppendLine("  FAIL baker enemy-neutralized");
                    bad++;
                }
                else
                {
                    var tex = sprite.texture;
                    int opaque = 0;
                    var px = tex.GetPixels32();
                    for (int i = 0; i < px.Length; i++) if (px[i].a > 8) opaque++;
                    if (opaque < 50)
                    {
                        log.AppendLine($"  FAIL baker too transparent ({opaque} opaque px)");
                        bad++;
                    }
                    else log.AppendLine($"  Baker ok ({opaque} opaque px, {tex.width}x{tex.height})");
                }
            }

            // Synthetic review: skirmish sample if available, else empty stats path.
            var scenario = ScenarioSamples.Skirmish();
            if (scenario != null)
            {
                var map = scenario.GenerateMap();
                var sim = new Simulation(scenario, map, UnitCatalogue.Default());
                var review = PostBattleReviewer.Build(sim, catalog);
                log.AppendLine(
                    $"  Fresh sim: neutralized={review.Stats.EnemyNeutralized} " +
                    $"awards={review.Awards.Count} critiques={review.Critiques.Count}");
                if (review.Stats.EnemyNeutralized != 0)
                {
                    log.AppendLine("  FAIL fresh sim should have 0 enemy casualties");
                    bad++;
                }
                if (review.Critiques.Count == 0 ||
                    !review.Critiques[0].StartsWith("DOCTRINE"))
                {
                    log.AppendLine("  FAIL #421 critique missing/mis-ordered doctrine line");
                    bad++;
                }
                if (review.Critiques.Count != 1)
                {
                    log.AppendLine(
                        $"  FAIL fresh sim is undecided — expected only the doctrine line, " +
                        $"got {review.Critiques.Count}");
                    bad++;
                }
            }
            else
            {
                log.AppendLine("  WARN ScenarioSamples.Skirmish() null — skip sim grant check");
            }

            // #421: a decided fight gets exchange + objective lines too, not just doctrine.
            {
                var decidedScenario = ScenarioSamples.Skirmish();
                decidedScenario.Map.EnableErosion = false;
                foreach (var u in decidedScenario.Units) u.Roe = RulesOfEngagement.FireAtWill;
                var decidedMap = decidedScenario.GenerateMap();
                var decidedSim = new Simulation(decidedScenario, decidedMap);
                decidedSim.AddExecutor(new MoveToExecutor());
                decidedSim.AddExecutor(new EngageExecutor());
                decidedSim.EnableReactions();

                var sides = new List<SideId>();
                foreach (var s in decidedScenario.Sides) sides.Add(s.Id);
                decidedSim.EnableDirector(sides);

                int limit = decidedScenario.TimeLimitTicks > 0
                    ? decidedScenario.TimeLimitTicks + 60 : 4000;
                while (decidedSim.Tick < limit && !decidedSim.IsOver) decidedSim.Step();

                if (!decidedSim.IsOver)
                {
                    log.AppendLine($"  FAIL decided-fight setup never reached a decision after {decidedSim.Tick} ticks");
                    bad++;
                }
                else
                {
                    var decidedReview = PostBattleReviewer.Build(decidedSim, catalog);
                    bool hasObjectiveLine = decidedReview.Critiques.Exists(c => c.StartsWith("OBJECTIVE"));
                    log.AppendLine(
                        $"  Decided sim (T+{decidedSim.Tick}, draw={decidedReview.Stats.IsDraw}): " +
                        $"critiques={decidedReview.Critiques.Count} " +
                        $"[{string.Join(" | ", decidedReview.Critiques)}]");
                    if (!decidedReview.Stats.IsDraw && !hasObjectiveLine)
                    {
                        log.AppendLine("  FAIL decided non-draw sim should carry an OBJECTIVE critique line");
                        bad++;
                    }
                }
            }

            // Panel Build/Open/Close
            var go = new GameObject("BarMedalProbeHost");
            try
            {
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                go.AddComponent<UnityEngine.UI.CanvasScaler>();
                go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                var host = go.GetComponent<RectTransform>();
                if (host == null) host = go.AddComponent<RectTransform>();

                var panel = go.AddComponent<PostBattlePanel>();
                panel.Build(host);
                var fake = new PostBattleReview
                {
                    Stats = new PostBattleStats
                    {
                        ScenarioName = "Probe Fight",
                        OutcomeLine = "probe wins",
                        PlayerWon = true,
                        Tick = 120,
                        EnemyNeutralized = 3,
                        FriendlyLost = 1,
                        ObjectivesHeld = 1,
                        ObjectiveCount = 1,
                        FriendlyRemaining = 0.8f,
                        EnemyRemaining = 0.4f,
                    },
                };
                fake.Awards.Add(new BarMedalAward
                {
                    MedalId = BarMedalIO.EnemyNeutralizedId,
                    Count = 3,
                    Tick = 120,
                    ScenarioName = "Probe Fight",
                });
                fake.Awards.Add(new BarMedalAward
                {
                    MedalId = BarMedalIO.ObjectiveSecuredId,
                    Count = 1,
                    Tick = 120,
                    ScenarioName = "Probe Fight",
                });
                panel.Open(fake);
                if (!panel.IsOpen)
                {
                    log.AppendLine("  FAIL panel Open");
                    bad++;
                }
                else log.AppendLine("  Panel Open ok");
                panel.Close();
                if (panel.IsOpen)
                {
                    log.AppendLine("  FAIL panel Close");
                    bad++;
                }
                else log.AppendLine("  Panel Close ok");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            if (bad == 0) log.Insert(0, "PASS BarMedalProbe\n");
            else log.Insert(0, $"FAIL BarMedalProbe ({bad})\n");
            Debug.Log(log.ToString());
            if (bad > 0) throw new Exception($"BarMedalProbe: {bad} failure(s)");
        }
    }
}
#endif
