// ContextHelp.cs
// #308 / #442: in-PLAY contextual copy for palette controls (MOVE, ENGAGE).
// Not the field manual (#124).

namespace Strategos.UI
{
    /// <summary>Authored blurbs keyed by armed palette verb (#308 MOVE, #442 ENGAGE).</summary>
    public static class ContextHelp
    {
        public const string MoveTitle = "MOVE";

        public const string MoveBody =
            "Arm MOVE (M), select a friendly unit, then left-click a destination on the map. " +
            "The unit paths by terrain-cost A* and the order joins its queue. " +
            "Esc clears the armed verb; right-click shortcuts still work. " +
            "This is in-session help for one control — the glossary is #124.";

        public const string EngageTitle = "ENGAGE";

        public const string EngageBody =
            "Arm ENGAGE (E), select a friendly unit, then left-click an enemy contact on the map. " +
            "Issues a direct-fire Engage order against that unit. " +
            "Right-click a contact while unarmed also engages. Esc clears the armed verb. " +
            "This is in-session help — the glossary is #124.";

        /// <summary>True when <paramref name="verb"/> has authored context help.</summary>
        public static bool TryGet(PaletteVerb verb, out string title, out string body)
        {
            if (verb == PaletteVerb.MoveTo)
            {
                title = MoveTitle;
                body = MoveBody;
                return true;
            }

            if (verb == PaletteVerb.Engage)
            {
                title = EngageTitle;
                body = EngageBody;
                return true;
            }

            title = null;
            body = null;
            return false;
        }
    }
}
