// DisplayModeProbe.cs
// #387: AppShell display-mode API — ApplyWindowed remembers size; F11 path is ToggleFullscreen.
// Does not assert Screen.fullScreen (batchmode may no-op SetResolution).
// Batch: -executeMethod Strategos.Editor.DisplayModeProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.UI;

namespace Strategos.Editor
{
    public static class DisplayModeProbe
    {
        [MenuItem("Strategos/Probe Display Mode")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;
            // Inactive so AppShell.Start does not build the full chrome.
            var go = new GameObject("display-mode-probe");
            go.SetActive(false);
            try
            {
                var shell = go.AddComponent<AppShell>();

                var def = shell.RememberedWindowedSize;
                if (def.x != 1600 || def.y != 900)
                {
                    log.AppendLine("  FAIL default RememberedWindowedSize want 1600x900 got " +
                                   def.x + "x" + def.y);
                    bad++;
                }
                else log.AppendLine("  default RememberedWindowedSize 1600x900 ok");

                shell.ApplyWindowed(1280, 720);
                var remembered = shell.RememberedWindowedSize;
                if (remembered.x != 1280 || remembered.y != 720)
                {
                    log.AppendLine("  FAIL ApplyWindowed remember want 1280x720 got " +
                                   remembered.x + "x" + remembered.y);
                    bad++;
                }
                else log.AppendLine("  ApplyWindowed(1280,720) remembers ok");

                // Re-apply without args must keep the explicit size (do not go through
                // ApplyFullscreen first — that intentionally snapshots Screen WxH).
                shell.ApplyWindowed();
                var after = shell.RememberedWindowedSize;
                if (after.x != 1280 || after.y != 720)
                {
                    log.AppendLine("  FAIL ApplyWindowed() keeps remembered size got " +
                                   after.x + "x" + after.y);
                    bad++;
                }
                else log.AppendLine("  ApplyWindowed() keeps remembered size ok");

                // ToggleFullscreen / ApplyFullscreen must be callable (shared F11 path).
                // Do not assert Screen.fullScreen — batchmode often no-ops SetResolution.
                shell.ToggleFullscreen();
                log.AppendLine("  ToggleFullscreen callable ok");

                shell.ApplyFullscreen();
                log.AppendLine("  ApplyFullscreen callable ok");

                // #389: Settings GRAPHICS fullscreen uses the same AppShell methods.
                if (Strategos.UI.Views.SettingsView.Categories[0] != "GRAPHICS")
                {
                    log.AppendLine("  FAIL SettingsView.Categories[0] should be GRAPHICS");
                    bad++;
                }
                else log.AppendLine("  SettingsView GRAPHICS category present ok");

                // #390: windowed presets are fixed sizes, not a fullscreen res list.
                var presets = Strategos.UI.Views.SettingsView.WindowedPresets;
                if (presets == null || presets.Length < 3)
                {
                    log.AppendLine("  FAIL WindowedPresets want >=3 got " +
                                   (presets == null ? "null" : presets.Length.ToString()));
                    bad++;
                }
                else if (presets[0].Width != 1280 || presets[1].Width != 1600 || presets[2].Width != 1920)
                {
                    log.AppendLine("  FAIL WindowedPresets widths want 1280/1600/1920");
                    bad++;
                }
                else log.AppendLine("  WindowedPresets 1280/1600/1920 ok");

                if (Strategos.UI.Views.SettingsView.IndexOfPreset(1600, 900) != 1)
                {
                    log.AppendLine("  FAIL IndexOfPreset(1600,900) want 1");
                    bad++;
                }
                else log.AppendLine("  IndexOfPreset(1600,900)=1 ok");

                // #391: boot apply seeds remembered size from prefs without requiring Start().
                var boot = new Strategos.Preferences.PlayerPreferences
                {
                    Fullscreen = false,
                    WindowWidth = 1920,
                    WindowHeight = 1080,
                };
                shell.ApplyDisplayPreferences(boot);
                var seeded = shell.RememberedWindowedSize;
                if (seeded.x != 1920 || seeded.y != 1080)
                {
                    log.AppendLine("  FAIL ApplyDisplayPreferences seed want 1920x1080 got " +
                                   seeded.x + "x" + seeded.y);
                    bad++;
                }
                else log.AppendLine("  ApplyDisplayPreferences seeds 1920x1080 ok");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            log.AppendLine(bad == 0 ? "PROBE PASSED" : ("PROBE FAILED with " + bad + " problem(s)"));
            if (bad == 0) Debug.Log("[DisplayModeProbe]\n" + log);
            else Debug.LogError("[DisplayModeProbe]\n" + log);
        }
    }
}
#endif
