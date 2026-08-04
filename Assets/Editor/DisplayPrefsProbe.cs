// DisplayPrefsProbe.cs
// #392: display prefs round-trip + Settings/F11 share AppShell Apply* / ToggleFullscreen.
// Does not assert Screen.fullScreen (batchmode may no-op SetResolution).
// Batch: -executeMethod Strategos.Editor.DisplayPrefsProbe.Run

#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Persistence.Files;
using Strategos.Preferences;
using Strategos.UI;
using Strategos.UI.Views;

namespace Strategos.Editor
{
    public static class DisplayPrefsProbe
    {
        [MenuItem("Strategos/Probe Display Prefs")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;
            var path = Path.Combine(Path.GetTempPath(), "strategos-display-prefs-" +
                                                       System.Guid.NewGuid().ToString("N") +
                                                       ".json");
            var go = new GameObject("display-prefs-probe");
            go.SetActive(false);
            try
            {
                var store = new JsonPreferenceStore(path);
                var shell = go.AddComponent<AppShell>();
                shell.PreferenceStore = store;

                // Round-trip Fullscreen + windowed size (#388 / #392).
                var prefs = new PlayerPreferences
                {
                    Fullscreen = true,
                    WindowWidth = 1280,
                    WindowHeight = 720,
                };
                store.Save(prefs);
                var loaded = store.Load();
                if (!loaded.Fullscreen || loaded.WindowWidth != 1280 || loaded.WindowHeight != 720)
                {
                    log.AppendLine("  FAIL store round-trip want FS 1280x720 got FS=" +
                                   loaded.Fullscreen + " " + loaded.WindowWidth + "x" +
                                   loaded.WindowHeight);
                    bad++;
                }
                else log.AppendLine("  store Fullscreen + 1280x720 round-trip ok");

                // Boot path seeds remembered size from prefs (#391).
                shell.ApplyDisplayPreferences(loaded);
                var seeded = shell.RememberedWindowedSize;
                if (seeded.x != 1280 || seeded.y != 720)
                {
                    log.AppendLine("  FAIL boot seed want 1280x720 got " + seeded.x + "x" + seeded.y);
                    bad++;
                }
                else log.AppendLine("  ApplyDisplayPreferences seeds remembered size ok");

                // Settings WINDOWED SIZE preset index matches prefs (#390).
                if (SettingsView.IndexOfPreset(loaded.WindowWidth, loaded.WindowHeight) != 0)
                {
                    log.AppendLine("  FAIL IndexOfPreset(1280,720) want 0");
                    bad++;
                }
                else log.AppendLine("  SettingsView IndexOfPreset(1280,720)=0 ok");

                // Shared path: Settings leaving fullscreen uses ApplyWindowed(WxH);
                // F11 uses ToggleFullscreen → same ApplyWindowed / ApplyFullscreen (#387/#389).
                shell.ApplyWindowed(loaded.WindowWidth, loaded.WindowHeight);
                if (shell.RememberedWindowedSize.x != 1280 || shell.RememberedWindowedSize.y != 720)
                {
                    log.AppendLine("  FAIL Settings-style ApplyWindowed keep 1280x720");
                    bad++;
                }
                else log.AppendLine("  Settings-style ApplyWindowed(prefs) ok");

                shell.ToggleFullscreen();
                log.AppendLine("  F11-style ToggleFullscreen callable ok");

                shell.ApplyFullscreen();
                log.AppendLine("  Settings-style ApplyFullscreen callable ok");

                // Persist what Settings would write after toggling windowed.
                loaded.Fullscreen = false;
                loaded.WindowWidth = 1600;
                loaded.WindowHeight = 900;
                store.Save(loaded);
                shell.ApplyDisplayPreferences(store.Load());
                var again = shell.RememberedWindowedSize;
                if (again.x != 1600 || again.y != 900)
                {
                    log.AppendLine("  FAIL re-boot after Settings save want 1600x900 got " +
                                   again.x + "x" + again.y);
                    bad++;
                }
                else log.AppendLine("  re-boot after Settings save 1600x900 ok");

                if (SettingsView.Categories[0] != "GRAPHICS")
                {
                    log.AppendLine("  FAIL GRAPHICS category missing");
                    bad++;
                }
                else log.AppendLine("  SettingsView GRAPHICS category ok");
            }
            finally
            {
                Object.DestroyImmediate(go);
                if (File.Exists(path)) File.Delete(path);
            }

            log.AppendLine(bad == 0 ? "PROBE PASSED" : ("PROBE FAILED with " + bad + " problem(s)"));
            if (bad == 0) Debug.Log("[DisplayPrefsProbe]\n" + log);
            else Debug.LogError("[DisplayPrefsProbe]\n" + log);
        }
    }
}
#endif
