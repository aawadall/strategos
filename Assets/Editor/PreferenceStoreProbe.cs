// PreferenceStoreProbe.cs
// #307: JsonPreferenceStore write/read round-trip for ConfirmOrders.
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
                store.Save(again);
                var cleared = store.Load();
                if (cleared.ConfirmOrders)
                {
                    log.AppendLine("  FAIL round-trip ConfirmOrders=false");
                    bad++;
                }
                else log.AppendLine("  round-trip ConfirmOrders=false ok");

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
