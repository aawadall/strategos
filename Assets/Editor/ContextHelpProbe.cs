// ContextHelpProbe.cs
// #308: MOVE has authored context help; overlay Build/Open/Close.
// Batch: -executeMethod Strategos.Editor.ContextHelpProbe.Run

#if UNITY_EDITOR
using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.UI;

namespace Strategos.Editor
{
    public static class ContextHelpProbe
    {
        [MenuItem("Strategos/Probe Context Help")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            if (!ContextHelp.TryGet(PaletteVerb.MoveTo, out var title, out var body) ||
                title != ContextHelp.MoveTitle ||
                string.IsNullOrEmpty(body) ||
                body.IndexOf("destination", StringComparison.OrdinalIgnoreCase) < 0)
            {
                log.AppendLine("  FAIL ContextHelp.MoveTo missing or incomplete");
                bad++;
            }
            else log.AppendLine("  ContextHelp.MoveTo ok");

            if (ContextHelp.TryGet(PaletteVerb.Engage, out _, out _))
            {
                log.AppendLine("  FAIL Engage should not have authored help yet (#308 is MOVE only)");
                bad++;
            }
            else log.AppendLine("  Engage has no help yet ok");

            var hostGo = new GameObject("probe-ctx-help", typeof(RectTransform));
            var host = hostGo.GetComponent<RectTransform>();
            var overlay = hostGo.AddComponent<ContextHelpOverlay>();
            try
            {
                overlay.Build(host);
                overlay.Open(ContextHelp.MoveTitle, ContextHelp.MoveBody);
                if (!overlay.IsOpen) { log.AppendLine("  FAIL ContextHelpOverlay.Open"); bad++; }
                overlay.Close();
                if (overlay.IsOpen) { log.AppendLine("  FAIL ContextHelpOverlay.Close"); bad++; }
                log.AppendLine("  ContextHelpOverlay Build/Open/Close ok");
            }
            catch (Exception ex)
            {
                log.AppendLine("  FAIL ContextHelpOverlay: " + ex.Message);
                bad++;
            }
            finally { UnityEngine.Object.DestroyImmediate(hostGo); }

            log.AppendLine(bad == 0 ? "PROBE PASSED" : ("PROBE FAILED with " + bad + " problem(s)"));
            if (bad == 0) Debug.Log("[ContextHelpProbe]\n" + log);
            else Debug.LogError("[ContextHelpProbe]\n" + log);
        }
    }
}
#endif
