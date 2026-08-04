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
