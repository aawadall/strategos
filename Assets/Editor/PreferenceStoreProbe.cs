// PreferenceStoreProbe.cs
// #307: JsonPreferenceStore write/read round-trip for ConfirmOrders.
// #388: same path for Fullscreen + windowed WxH.
// #311 will extend with tutorial scenario Validate when that skeleton lands.
// Batch: -executeMethod Strategos.Editor.PreferenceStoreProbe.Run

#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Persistence.Files;
using Strategos.Preferences;

namespace Strategos.Editor
{
    public static class PreferenceStoreProbe
    {
        [MenuItem("Strategos/Probe Preference Store")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;
            var path = Path.Combine(Path.GetTempPath(), "strategos-pref-probe-" +
                                                       System.Guid.NewGuid().ToString("N") +
                                                       ".json");
            try
            {
                var store = new JsonPreferenceStore(path);

                var fresh = store.Load();
                if (fresh.ConfirmOrders)
                {
                    log.AppendLine("  FAIL default ConfirmOrders should be false");
                    bad++;
                }
                else log.AppendLine("  default ConfirmOrders=false ok");

                if (fresh.Fullscreen)
                {
                    log.AppendLine("  FAIL default Fullscreen should be false");
                    bad++;
                }
                else log.AppendLine("  default Fullscreen=false ok");

                if (fresh.WindowWidth != 1600 || fresh.WindowHeight != 900)
                {
                    log.AppendLine("  FAIL default window want 1600x900 got " +
                                   fresh.WindowWidth + "x" + fresh.WindowHeight);
                    bad++;
                }
                else log.AppendLine("  default WindowWidth/Height 1600x900 ok");

                fresh.ConfirmOrders = true;
                store.Save(fresh);

                var again = store.Load();
                if (!again.ConfirmOrders)
                {
                    log.AppendLine("  FAIL round-trip ConfirmOrders=true");
                    bad++;
                }
                else log.AppendLine("  round-trip ConfirmOrders=true ok");

                again.ConfirmOrders = false;
                again.Fullscreen = true;
                again.WindowWidth = 1280;
                again.WindowHeight = 720;
                store.Save(again);

                var display = store.Load();
                if (display.ConfirmOrders)
                {
                    log.AppendLine("  FAIL round-trip ConfirmOrders=false");
                    bad++;
                }
                else log.AppendLine("  round-trip ConfirmOrders=false ok");

                if (!display.Fullscreen || display.WindowWidth != 1280 || display.WindowHeight != 720)
                {
                    log.AppendLine("  FAIL round-trip display want FS 1280x720 got FS=" +
                                   display.Fullscreen + " " + display.WindowWidth + "x" +
                                   display.WindowHeight);
                    bad++;
                }
                else log.AppendLine("  round-trip Fullscreen + 1280x720 ok");

                // Old ConfirmOrders-only JSON must pick up display defaults (field initializers).
                File.WriteAllText(path, "{\n  \"FormatVersion\": 1,\n  \"ConfirmOrders\": true\n}\n");
                var legacy = store.Load();
                if (!legacy.ConfirmOrders || legacy.Fullscreen ||
                    legacy.WindowWidth != 1600 || legacy.WindowHeight != 900)
                {
                    log.AppendLine("  FAIL legacy JSON defaults want Confirm=true FS=false 1600x900 got Confirm=" +
                                   legacy.ConfirmOrders + " FS=" + legacy.Fullscreen + " " +
                                   legacy.WindowWidth + "x" + legacy.WindowHeight);
                    bad++;
                }
                else log.AppendLine("  legacy JSON fills display defaults ok");

                if (!File.Exists(path))
                {
                    log.AppendLine("  FAIL preferences file missing after Save");
                    bad++;
                }
                else log.AppendLine("  preferences.json written ok");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }

            log.AppendLine(bad == 0 ? "PROBE PASSED" : ("PROBE FAILED with " + bad + " problem(s)"));
            if (bad == 0) Debug.Log("[PreferenceStoreProbe]\n" + log);
            else Debug.LogError("[PreferenceStoreProbe]\n" + log);
        }
    }
}
#endif
