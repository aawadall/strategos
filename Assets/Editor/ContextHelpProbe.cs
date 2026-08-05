// ContextHelpProbe.cs
// #308 / #442 / #445 / #447: MOVE, ENGAGE, WAYPOINTS, DIG IN help; overlay Build/Open/Close.
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

            if (!ContextHelp.TryGet(PaletteVerb.Engage, out var eTitle, out var eBody) ||
                eTitle != ContextHelp.EngageTitle ||
                string.IsNullOrEmpty(eBody) ||
                eBody.IndexOf("contact", StringComparison.OrdinalIgnoreCase) < 0)
            {
                log.AppendLine("  FAIL ContextHelp.Engage missing or incomplete (#442)");
                bad++;
            }
            else log.AppendLine("  ContextHelp.Engage ok");

            if (!ContextHelp.TryGet(PaletteVerb.Waypoints, out var wTitle, out var wBody) ||
                wTitle != ContextHelp.WaypointsTitle ||
                string.IsNullOrEmpty(wBody) ||
                wBody.IndexOf("CONFIRM ROUTE", StringComparison.OrdinalIgnoreCase) < 0)
            {
                log.AppendLine("  FAIL ContextHelp.Waypoints missing or incomplete (#445)");
                bad++;
            }
            else log.AppendLine("  ContextHelp.Waypoints ok");

            if (!ContextHelp.TryGet(PaletteVerb.DigIn, out var dTitle, out var dBody) ||
                dTitle != ContextHelp.DigInTitle ||
                string.IsNullOrEmpty(dBody) ||
                dBody.IndexOf("two minutes", StringComparison.OrdinalIgnoreCase) < 0)
            {
                log.AppendLine("  FAIL ContextHelp.DigIn missing or incomplete (#447)");
                bad++;
            }
            else log.AppendLine("  ContextHelp.DigIn ok");

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
