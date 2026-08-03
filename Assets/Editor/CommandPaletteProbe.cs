// CommandPaletteProbe.cs
// The armable-verb table (#127 / #129): MoveTo and Engage are present, None is not a row,
// every verb has a shortcut that does not steal Space, and clear is Escape — so chrome and
// Update can stay table-driven.
//
// Menu:  Strategos > Probe Command Palette
// Batch: -executeMethod Strategos.Editor.CommandPaletteProbe.Run

#if UNITY_EDITOR
using System.Collections.Generic;
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

            if (CommandPalette.ClearShortcut == KeyCode.None)
            {
                log.AppendLine("  palette: FAILED, ClearShortcut must be set (Esc)");
                return false;
            }

            if (CommandPalette.ClearShortcut == KeyCode.Space)
            {
                log.AppendLine("  palette: FAILED, ClearShortcut must not steal Space (clock)");
                return false;
            }

            bool sawMove = false, sawEngage = false;
            var seenKeys = new HashSet<KeyCode> { CommandPalette.ClearShortcut };

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

                if (v.Shortcut == KeyCode.None || string.IsNullOrEmpty(v.ShortcutLabel))
                {
                    log.AppendLine($"  palette: FAILED, verb {v.Id} needs a Shortcut (#129)");
                    return false;
                }

                if (v.Shortcut == KeyCode.Space)
                {
                    log.AppendLine($"  palette: FAILED, verb {v.Id} steals Space (clock)");
                    return false;
                }

                if (v.Shortcut == CommandPalette.ClearShortcut)
                {
                    log.AppendLine($"  palette: FAILED, verb {v.Id} collides with ClearShortcut");
                    return false;
                }

                if (!seenKeys.Add(v.Shortcut))
                {
                    log.AppendLine($"  palette: FAILED, duplicate Shortcut {v.Shortcut}");
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

                log.AppendLine($"    {v.Id,-8} '{v.Label}' → {v.Kind}  [{v.ShortcutLabel}]");
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

            log.AppendLine(
                $"  palette: {verbs.Length} verb(s), shortcuts ok, clear={CommandPalette.ClearShortcut}");
            return true;
        }
    }
}
#endif
