// SteamProbe.cs
// #305: SteamClientHost.Bootstrap / Overlay / Achievement / Cloud never throw without an
// App ID or Steamworks package. NullSteamClient stays unavailable; stubs record calls.
//
// Menu:  Strategos > Probe Steam
// Batch: -executeMethod Strategos.Editor.SteamProbe.Run

#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Steam;

namespace Strategos.Editor
{
    public static class SteamProbe
    {
        [MenuItem("Strategos/Probe Steam")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckInitWithoutAppId(log);
            bad += CheckGuardedStubs(log);
            bad += CheckAppIdParseWhenPresent(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[SteamProbe]\n" + log);
            else Debug.LogError("[SteamProbe]\n" + log);
        }

        private static int CheckInitWithoutAppId(StringBuilder log)
        {
            SteamClientHost.ResetForProbe();
            try
            {
                // Ensure no project-root steam_appid.txt is required for CI.
                var available = SteamClientHost.Bootstrap();
                var client = SteamClientHost.Client;
                if (available || client.IsAvailable)
                {
                    log.AppendLine("  FAIL expected unavailable without Steamworks package");
                    return 1;
                }

                // AppId may be 0 (missing file) or >0 if a local steam_appid.txt exists —
                // either way Init must not throw and IsAvailable stays false (#305).
                log.AppendLine(
                    $"  Bootstrap without Steamworks  ok " +
                    $"(available={client.IsAvailable}, appId={client.AppId})");
                return 0;
            }
            finally
            {
                SteamClientHost.ResetForProbe();
            }
        }

        private static int CheckGuardedStubs(StringBuilder log)
        {
            SteamClientHost.ResetForProbe();
            try
            {
                SteamClientHost.SetClient(new NullSteamClient());
                SteamClientHost.Bootstrap();
                var nullClient = (NullSteamClient)SteamClientHost.Client;

                SteamClientHost.Client.ActivateOverlay("Friends");
                SteamClientHost.Client.SetAchievement("PROBE_FIRST_BLOOD");
                var wrote = SteamClientHost.Client.CloudWrite("probe.bin", new byte[] { 1, 2, 3 });
                var read = SteamClientHost.Client.CloudRead("probe.bin");

                if (nullClient.LastOverlayDialog != "Friends")
                {
                    log.AppendLine($"  FAIL overlay stub recorded '{nullClient.LastOverlayDialog}'");
                    return 1;
                }

                if (nullClient.LastAchievementId != "PROBE_FIRST_BLOOD")
                {
                    log.AppendLine($"  FAIL achievement stub recorded '{nullClient.LastAchievementId}'");
                    return 1;
                }

                if (wrote || read != null)
                {
                    log.AppendLine("  FAIL cloud stub should return false / null when unavailable");
                    return 1;
                }

                log.AppendLine("  Overlay / Achievement / Cloud stubs  ok (no throw)");
                return 0;
            }
            finally
            {
                SteamClientHost.ResetForProbe();
            }
        }

        private static int CheckAppIdParseWhenPresent(StringBuilder log)
        {
            SteamClientHost.ResetForProbe();
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                log.AppendLine("  FAIL could not resolve project root");
                return 1;
            }

            var realPath = Path.Combine(projectRoot, SteamAppId.FileName);
            var hadReal = File.Exists(realPath);
            string backup = null;
            try
            {
                if (hadReal)
                {
                    backup = realPath + ".bak-probe";
                    File.Copy(realPath, backup, overwrite: true);
                }

                File.WriteAllText(realPath, "# probe\n480\n");
                var nullClient = new NullSteamClient();
                SteamClientHost.SetClient(nullClient);
                SteamClientHost.Bootstrap();

                if (nullClient.AppId != 480)
                {
                    log.AppendLine($"  FAIL AppId parse expected 480, got {nullClient.AppId}");
                    return 1;
                }

                if (nullClient.IsAvailable)
                {
                    log.AppendLine("  FAIL NullSteamClient must stay unavailable with App ID only");
                    return 1;
                }

                log.AppendLine("  steam_appid.txt parse  ok (AppId=480, still unavailable)");
                return 0;
            }
            finally
            {
                SteamClientHost.ResetForProbe();
                try
                {
                    if (hadReal && backup != null && File.Exists(backup))
                    {
                        File.Copy(backup, realPath, overwrite: true);
                        File.Delete(backup);
                    }
                    else if (!hadReal && File.Exists(realPath))
                        File.Delete(realPath);
                }
                catch
                {
                    // best-effort restore
                }
            }
        }
    }
}
#endif
