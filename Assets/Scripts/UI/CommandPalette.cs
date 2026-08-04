// CommandPalette.cs
// In-code table of armable PLAY verbs (#127 / #53).
//
// Adding a verb is a row here — not a new branch in PlayView.OnMapClicked or Update.
// PlayView dispatches armed left-clicks from PaletteVerb.Id / Kind (#128, #54) and arms
// from Shortcut / ClearShortcut (#129). Loading this table from config is a future
// enhancement (#130) and must not block the in-code table.
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
        Waypoints = 3,
        DigIn = 4,
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
        /// Clears arming back to select-only. Not a verb row — kept next to the table so
        /// PLAY does not invent its own Escape binding (#129). Must not be Space (clock).
        /// </summary>
        public const KeyCode ClearShortcut = KeyCode.Escape;

        /// <summary>
        /// Armable verbs in rail order. <see cref="PaletteVerb.None"/> is not a row — it is
        /// the clear/select state the chrome exposes separately.
        /// </summary>
        public static readonly PaletteVerbDef[] Verbs =
        {
            new(PaletteVerb.MoveTo, "MOVE", CommandKind.MoveTo, KeyCode.M, "M"),
            new(PaletteVerb.Engage, "ENGAGE", CommandKind.Engage, KeyCode.E, "E"),
            new(PaletteVerb.Waypoints, "WAYPOINTS", CommandKind.MoveTo, KeyCode.W, "W"),
            new(PaletteVerb.DigIn, "DIG IN", CommandKind.Defend, KeyCode.D, "D"),
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

        /// <summary>
        /// If a palette key was pressed this frame, returns the verb to arm (or
        /// <see cref="PaletteVerb.None"/> for clear). Reads <see cref="Verbs"/> and
        /// <see cref="ClearShortcut"/> — do not hard-code M/E/W/Esc in the view (#129).
        /// </summary>
        public static bool TryReadArmingKey(out PaletteVerb verb)
        {
            if (Input.GetKeyDown(ClearShortcut))
            {
                verb = PaletteVerb.None;
                return true;
            }

            var verbs = Verbs;
            for (int i = 0; i < verbs.Length; i++)
            {
                var key = verbs[i].Shortcut;
                if (key == KeyCode.None) continue;
                if (Input.GetKeyDown(key))
                {
                    verb = verbs[i].Id;
                    return true;
                }
            }

            verb = PaletteVerb.None;
            return false;
        }
    }
}
