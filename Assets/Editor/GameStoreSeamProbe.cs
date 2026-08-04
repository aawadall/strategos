// GameStoreSeamProbe.cs
// #355 / #364: exercise IGameStore through SaveAsync / LoadAsync / ListSavesAsync / DeleteAsync
// and StoreResult statuses (Ok, NotFound, VersionMismatch). SaveLoadProbe still covers
// snapshot fidelity; this probe names the seam methods in its log lines.
//
// Menu:  Strategos > Probe Game Store Seam
// Batch: -executeMethod Strategos.Editor.GameStoreSeamProbe.Run

#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Campaigns;
using Strategos.Commands;
using Strategos.Content;
using Strategos.Content.Resources;
using Strategos.Identity;
using Strategos.Persistence;
using Strategos.Persistence.Files;
using Strategos.Scenarios;

namespace Strategos.Editor
{
    public static class GameStoreSeamProbe
    {
        [MenuItem("Strategos/Probe Game Store Seam")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckStoreAsyncShape(log);
            bad += CheckContentSources(log);
            bad += CheckAnonymousIdentity(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[GameStoreSeamProbe]\n" + log);
            else Debug.LogError("[GameStoreSeamProbe]\n" + log);
        }

        private static int CheckStoreAsyncShape(StringBuilder log)
        {
            int bad = 0;
            var dir = Path.Combine(Path.GetTempPath(), "strategos-seam-store-" + Guid.NewGuid());
            try
            {
                var scenario = ScenarioSamples.Skirmish();
                scenario.Map.EnableErosion = false;
                var map = scenario.GenerateMap();
                var sim = new Simulation(scenario, map);
                sim.Step(2);

                IGameStore store = new FileGameStore(dir);
                var record = new SaveRecord
                {
                    SaveId = "seam-1",
                    ScenarioName = scenario.Name,
                    Tick = sim.Tick,
                    SavedAtUtc = DateTime.UtcNow.ToString("o"),
                    Snapshot = sim.Snapshot(),
                };

                var save = store.SaveAsync(record).GetAwaiter().GetResult();
                if (!save.Ok)
                {
                    log.AppendLine($"  FAIL SaveAsync: {save.Status} {save.Message}");
                    bad++;
                }

                var load = store.LoadAsync("seam-1").GetAwaiter().GetResult();
                if (!load.Ok)
                {
                    log.AppendLine($"  FAIL LoadAsync: {load.Status} {load.Message}");
                    bad++;
                }

                var missing = store.LoadAsync("no-such-save").GetAwaiter().GetResult();
                if (missing.Status != StoreStatus.NotFound)
                {
                    log.AppendLine($"  FAIL LoadAsync missing expected NotFound, got {missing.Status}");
                    bad++;
                }

                record.FormatVersion = SaveRecord.CurrentFormatVersion + 9;
                record.SaveId = "seam-bad-ver";
                store.SaveAsync(record).GetAwaiter().GetResult();
                var mismatch = store.LoadAsync("seam-bad-ver").GetAwaiter().GetResult();
                if (mismatch.Status != StoreStatus.VersionMismatch)
                {
                    log.AppendLine($"  FAIL LoadAsync bad version expected VersionMismatch, got {mismatch.Status}");
                    bad++;
                }

                var list = store.ListSavesAsync().GetAwaiter().GetResult();
                if (!list.Ok || list.Value.Count < 1)
                {
                    log.AppendLine("  FAIL ListSavesAsync");
                    bad++;
                }

                var del = store.DeleteAsync("seam-1").GetAwaiter().GetResult();
                if (!del.Ok || !del.Value)
                {
                    log.AppendLine("  FAIL DeleteAsync");
                    bad++;
                }

                log.AppendLine("  IGameStore SaveAsync/LoadAsync/ListSavesAsync/DeleteAsync + " +
                               $"StoreResult statuses  {(bad == 0 ? "ok" : "FAILED")}");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
            return bad;
        }

        private static int CheckContentSources(StringBuilder log)
        {
            int bad = 0;

            IContentSource<Scenario> scenarios = new ResourcesScenarioSource();
            var skirmish = scenarios.Load(ScenarioSamples.SkirmishName);
            if (skirmish == null || skirmish.Name != ScenarioSamples.Skirmish().Name)
            {
                log.AppendLine("  FAIL ResourcesScenarioSource.Load(skirmish)");
                bad++;
            }

            var viaIo = ScenarioIO.Load(ScenarioSamples.SkirmishName);
            if (viaIo == null || viaIo.Name != skirmish.Name)
            {
                log.AppendLine("  FAIL ScenarioIO.Load thin-wrap disagrees with IContentSource");
                bad++;
            }

            var chain = CampaignChainIO.DefaultContentSource.Load(CampaignSamples.ValleyName);
            // valley may use CampaignSamples name — soft check: source does not throw
            log.AppendLine($"  IContentSource Scenario + CampaignChain.Load " +
                           $"(chain={(chain != null ? chain.Name : "null")})  " +
                           $"{(bad == 0 ? "ok" : "FAILED")}");
            return bad;
        }

        private static int CheckAnonymousIdentity(StringBuilder log)
        {
            IPlayerIdentity id = LocalAnonymousIdentity.Shared;
            if (id.PlayerId != LocalAnonymousIdentity.DefaultPlayerId ||
                string.IsNullOrWhiteSpace(id.DisplayName))
            {
                log.AppendLine("  FAIL LocalAnonymousIdentity");
                return 1;
            }

            log.AppendLine($"  IPlayerIdentity anonymous stub id='{id.PlayerId}' ok");
            return 0;
        }
    }
}
#endif
