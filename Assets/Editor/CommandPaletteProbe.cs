// CommandPaletteProbe.cs
// The armable-verb table (#127): MoveTo and Engage are present, None is not a row, and
// adding a verb is a table entry — this probe fails if chrome would have to hard-code labels.
//
// Menu:  Strategos > Probe Command Palette
// Batch: -executeMethod Strategos.Editor.CommandPaletteProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.UI;

namespace Strategos.Editor
{
    public static class CommandPaletteProbe
    {
        [MenuItem("Strategos/Probe Command Palette")]
        public static void Run()
        {
            var log = new StringBuilder();
            bool ok = TableIsArmable(log);

            Debug.Log(log.ToString());
            Debug.Log(ok
                ? "[CommandPaletteProbe] PROBE PASSED"
                : "[CommandPaletteProbe] PROBE FAILED");
        }

        private static bool TableIsArmable(StringBuilder log)
        {
            var verbs = CommandPalette.Verbs;
            if (verbs == null || verbs.Length < 2)
            {
                log.AppendLine("  palette: FAILED, need at least MoveTo and Engage");
                return false;
            }

            bool sawMove = false, sawEngage = false;
            for (int i = 0; i < verbs.Length; i++)
            {
                var v = verbs[i];
                if (v.Id == PaletteVerb.None)
                {
                    log.AppendLine("  palette: FAILED, None must not be a table row " +
                                   "(it is the clear/select state)");
                    return false;
                }

                if (string.IsNullOrEmpty(v.Label))
                {
                    log.AppendLine($"  palette: FAILED, verb {v.Id} has no label for chrome");
                    return false;
                }

                if (v.Id == PaletteVerb.MoveTo)
                {
                    sawMove = true;
                    if (v.Kind != CommandKind.MoveTo)
                    {
                        log.AppendLine("  palette: FAILED, MoveTo row maps to wrong CommandKind");
                        return false;
                    }
                }

                if (v.Id == PaletteVerb.Engage)
                {
                    sawEngage = true;
                    if (v.Kind != CommandKind.Engage)
                    {
                        log.AppendLine("  palette: FAILED, Engage row maps to wrong CommandKind");
                        return false;
                    }
                }

                log.AppendLine($"    {v.Id,-8} '{v.Label}' → {v.Kind}" +
                               (string.IsNullOrEmpty(v.ShortcutLabel)
                                   ? string.Empty
                                   : $"  [{v.ShortcutLabel}]"));
            }

            if (!sawMove || !sawEngage)
            {
                log.AppendLine($"  palette: FAILED, MoveTo={sawMove} Engage={sawEngage}");
                return false;
            }

            if (!CommandPalette.TryGet(PaletteVerb.MoveTo, out _) ||
                !CommandPalette.TryGet(PaletteVerb.Engage, out _))
            {
                log.AppendLine("  palette: FAILED, TryGet missed a shipped verb");
                return false;
            }

            log.AppendLine($"  palette: {verbs.Length} verb(s), MoveTo + Engage present  ok");
            return true;
        }
    }
}
#endif
