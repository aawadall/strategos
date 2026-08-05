// GlossaryProbe.cs
// #205 / #206: glossary pack load + FieldManualBrowserPanel Build/Open/Close.
// Batch: -executeMethod Strategos.Editor.GlossaryProbe.Run

#if UNITY_EDITOR
using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.FieldManual;
using Strategos.UI;

namespace Strategos.Editor
{
    public static class GlossaryProbe
    {
        [MenuItem("Strategos/Probe Glossary")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            var roundTrip = new GlossaryPack
            {
                Name = "probe",
                Source = "probe",
                Terms = new[]
                {
                    new GlossaryTerm
                    {
                        Id = "probe-term",
                        Title = "Probe",
                        Body = "Round-trip body.",
                        DrillRefs = new[] { "T1" },
                    },
                },
            };
            var json = GlossaryIO.ToJson(roundTrip);
            var back = GlossaryIO.FromJson(json);
            if (back == null || back.Terms == null || back.Terms.Length != 1 ||
                back.Terms[0].Id != "probe-term" ||
                back.Terms[0].DrillRefs == null ||
                back.Terms[0].DrillRefs.Length != 1)
            {
                log.AppendLine("  FAIL GlossaryIO round-trip");
                bad++;
            }
            else log.AppendLine("  GlossaryIO round-trip ok");

            var pack = GlossaryIO.Load(GlossaryIO.DefaultPackName);
            if (pack == null)
            {
                log.AppendLine("  FAIL Resources load " + GlossaryIO.DefaultPackName);
                bad++;
            }
            else if (pack.Terms == null || pack.Terms.Length < 5)
            {
                log.AppendLine("  FAIL need ≥5 terms, got " +
                    (pack.Terms == null ? 0 : pack.Terms.Length));
                bad++;
            }
            else
            {
                log.AppendLine("  Loaded " + pack.Name + " (" + pack.Terms.Length + " terms)");
                foreach (var t in pack.Terms)
                {
                    if (string.IsNullOrWhiteSpace(t.Id) ||
                        string.IsNullOrWhiteSpace(t.Title) ||
                        string.IsNullOrWhiteSpace(t.Body))
                    {
                        log.AppendLine("  FAIL term missing Id/Title/Body: " + (t.Id ?? "?"));
                        bad++;
                    }
                }
                bool sawDrill = false;
                foreach (var t in pack.Terms)
                {
                    if (t.DrillRefs != null && t.DrillRefs.Length > 0) { sawDrill = true; break; }
                }
                if (!sawDrill)
                {
                    log.AppendLine("  FAIL expected at least one DrillRefs entry (#205)");
                    bad++;
                }
                else log.AppendLine("  DrillRefs present on ≥1 term ok");
            }

            var hostGo = new GameObject("probe-field-manual", typeof(RectTransform));
            var host = hostGo.GetComponent<RectTransform>();
            var browser = hostGo.AddComponent<FieldManualBrowserPanel>();
            try
            {
                browser.Build(host);
                browser.Open();
                if (!browser.IsOpen)
                {
                    log.AppendLine("  FAIL FieldManualBrowserPanel.Open (#206)");
                    bad++;
                }
                else log.AppendLine("  FieldManualBrowserPanel Open ok");
                browser.Close();
                if (browser.IsOpen)
                {
                    log.AppendLine("  FAIL FieldManualBrowserPanel.Close (#206)");
                    bad++;
                }
                else log.AppendLine("  FieldManualBrowserPanel Close ok");
            }
            catch (Exception ex)
            {
                log.AppendLine("  FAIL FieldManualBrowserPanel: " + ex.Message);
                bad++;
            }
            finally { UnityEngine.Object.DestroyImmediate(hostGo); }

            log.AppendLine(bad == 0 ? "PROBE PASSED" : ("PROBE FAILED with " + bad + " problem(s)"));
            if (bad == 0) Debug.Log("[GlossaryProbe]\n" + log);
            else Debug.LogError("[GlossaryProbe]\n" + log);
        }
    }
}
#endif
