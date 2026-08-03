// CommandPalette.cs
// In-code table of armable PLAY verbs (#127 / #53).
//
// Adding a verb is a row here — not a new branch in PlayView.OnMapClicked. PlayView
// dispatches armed left-clicks from PaletteVerbDef.Kind (#128); #129 will read Shortcut.
// Loading this table from config is a future enhancement (#130) and must not block the
// in-code table.
//
// Right-click shortcuts stay a separate path (#53): this table does not redefine them.

using UnityEngine;
using Strategos.Commands;

namespace Strategos.UI
{
    /// <summary>Which palette verb is armed, or none (select-only).</summary>
    public enum PaletteVerb
    {
        None = 0,
        MoveTo = 1,
        Engage = 2,
    }

    /// <summary>One armable verb — chrome, shortcuts, and Kind for the confirming click.</summary>
    public readonly struct PaletteVerbDef
    {
        public readonly PaletteVerb Id;
        public readonly string Label;
        public readonly CommandKind Kind;
        public readonly KeyCode Shortcut;
        public readonly string ShortcutLabel;

        public PaletteVerbDef(PaletteVerb id, string label, CommandKind kind,
            KeyCode shortcut, string shortcutLabel)
        {
            Id = id;
            Label = label;
            Kind = kind;
            Shortcut = shortcut;
            ShortcutLabel = shortcutLabel ?? string.Empty;
        }
    }

    /// <summary>The shipped verb table. Iterate this for rail chrome — do not hard-code MOVE/ENGAGE in the view.</summary>
    public static class CommandPalette
    {
        /// <summary>
        /// Armable verbs in rail order. <see cref="PaletteVerb.None"/> is not a row — it is
        /// the clear/select state the chrome exposes separately.
        /// </summary>
        public static readonly PaletteVerbDef[] Verbs =
        {
            new(PaletteVerb.MoveTo, "MOVE", CommandKind.MoveTo, KeyCode.M, "M"),
            new(PaletteVerb.Engage, "ENGAGE", CommandKind.Engage, KeyCode.E, "E"),
        };

        public static bool TryGet(PaletteVerb id, out PaletteVerbDef def)
        {
            for (int i = 0; i < Verbs.Length; i++)
            {
                if (Verbs[i].Id == id)
                {
                    def = Verbs[i];
                    return true;
                }
            }
            def = default;
            return false;
        }
    }
}
