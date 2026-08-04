// ThroughputProbe.cs
// #105: measure map generation and a full 3600-tick step loop — episodes/hour, separately.
//
// The issue's first deliverable is a measurement that did not exist. No target number is set
// there; this probe *is* the measurement. It prints a table and PASSES when every timed
// path completes with a positive duration (a guard that the timing actually ran).
//
// Menu:  Strategos > Probe Throughput
// Batch: -executeMethod Strategos.Editor.ThroughputProbe.Run

#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.Scenarios;
using Strategos.SimEnv;
using Strategos.Units;
using Debug = UnityEngine.Debug;

namespace Strategos.Editor
{
    public static class ThroughputProbe
    {
        /// <summary>Shipped skirmish episode length (TimeLimitTicks).</summary>
        public const int EpisodeTicks = 3600;

        private const int MapSamplesErosionOn = 2;
        private const int MapSamplesErosionOff = 3;

        [MenuItem("Strategos/Probe Throughput")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            log.AppendLine($"  machine: {System.Environment.MachineName}, " +
                           $"processors={System.Environment.ProcessorCount}, " +
                           $"unity={Application.unityVersion}");
            log.AppendLine($"  episode_ticks: {EpisodeTicks} (skirmish TimeLimitTicks)");

            bad += MeasureMapGeneration(log);
            bad += MeasureStepLoop(log);
            bad += MeasureCachedEnvEpisodes(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[ThroughputProbe]\n" + log);
            else Debug.LogError("[ThroughputProbe]\n" + log);
        }

        private static int MeasureMapGeneration(StringBuilder log)
        {
            int bad = 0;
            bad += TimeMap(log, erosion: true, MapSamplesErosionOn);
            bad += TimeMap(log, erosion: false, MapSamplesErosionOff);
            return bad;
        }

        private static int TimeMap(StringBuilder log, bool erosion, int samples)
        {
            double totalSec = 0;
            for (int i = 0; i < samples; i++)
            {
                var scenario = ScenarioSamples.Skirmish();
                scenario.Map.EnableErosion = erosion;
                // Distinct seeds so we are not timing a warm path that skips work.
                scenario.Map.Seed = 20260729 + i * 17;

                var sw = Stopwatch.StartNew();
                var map = scenario.GenerateMap();
                sw.Stop();
                if (map == null || map.Width <= 0)
                {
                    log.AppendLine($"  map(erosion={erosion}): FAILED — null/empty map");
                    return 1;
                }
                totalSec += sw.Elapsed.TotalSeconds;
            }

            double mean = totalSec / samples;
            if (mean <= 0)
            {
                log.AppendLine($"  map(erosion={erosion}): FAILED — non-positive mean {mean}");
                return 1;
            }

            double mapsPerHour = 3600.0 / mean;
            log.AppendLine($"  map(erosion={(erosion ? "on" : "off")}): " +
                           $"n={samples}, mean={mean:0.###}s, " +
                           $"maps/hour≈{mapsPerHour:0.#}");
            return 0;
        }

        /// <summary>
        /// Bare Simulation.Step loop for EpisodeTicks — no director, measures the sim core
        /// after one map generate (erosion off, same as most probes).
        /// </summary>
        private static int MeasureStepLoop(StringBuilder log)
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var mapSw = Stopwatch.StartNew();
            var map = scenario.GenerateMap();
            mapSw.Stop();

            var sim = new Simulation(scenario, map, UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            sim.AddExecutor(new DefendExecutor());

            var sw = Stopwatch.StartNew();
            sim.Step(EpisodeTicks);
            sw.Stop();

            double stepSec = sw.Elapsed.TotalSeconds;
            if (stepSec <= 0)
            {
                log.AppendLine("  step-loop: FAILED — non-positive duration");
                return 1;
            }

            double episodesPerHour = 3600.0 / stepSec;
            double ticksPerSec = EpisodeTicks / stepSec;
            log.AppendLine($"  step-loop: {EpisodeTicks} ticks in {stepSec:0.###}s " +
                           $"({ticksPerSec:0.#} ticks/s); " +
                           $"episodes/hour≈{episodesPerHour:0.#} " +
                           $"(map-gen excluded; map took {mapSw.Elapsed.TotalSeconds:0.###}s erosion=off)");
            return 0;
        }

        /// <summary>
        /// Option-1 path already on SideEnv: one GenerateMap at Create, then Reset + bulk
        /// Step(EpisodeTicks) without regenerating terrain.
        /// </summary>
        private static int MeasureCachedEnvEpisodes(StringBuilder log)
        {
            var scenario = ScenarioSamples.Skirmish();
            var createSw = Stopwatch.StartNew();
            var env = SideEnv.Create(scenario, new SideId(1), UnitCatalogue.Default(),
                opposingDirectorSides: null, enableReactions: false, enableErosion: false);
            createSw.Stop();

            double episodeTotal = 0;
            const int episodes = 2;
            for (int e = 0; e < episodes; e++)
            {
                var sw = Stopwatch.StartNew();
                env.Reset();
                // Bulk step: same Simulation.Step path an env driver uses between actions;
                // avoids 3600 managed calls when measuring wall-clock episode rate.
                env.Simulation.Step(EpisodeTicks);
                sw.Stop();
                episodeTotal += sw.Elapsed.TotalSeconds;
            }

            double mean = episodeTotal / episodes;
            if (mean <= 0)
            {
                log.AppendLine("  cached-env: FAILED — non-positive episode mean");
                return 1;
            }

            double episodesPerHour = 3600.0 / mean;
            log.AppendLine($"  cached-env: Create={createSw.Elapsed.TotalSeconds:0.###}s " +
                           $"(includes first GenerateMap); " +
                           $"n={episodes} Reset+Step({EpisodeTicks}), mean={mean:0.###}s, " +
                           $"episodes/hour≈{episodesPerHour:0.#}");
            return 0;
        }
    }
}
#endif
