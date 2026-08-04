// ContextHelp.cs
// #308: in-PLAY contextual copy for one palette control (MOVE). Not the field manual (#124).

namespace Strategos.UI
{
    /// <summary>Authored blurbs keyed by armed palette verb. Only MOVE ships full copy (#308).</summary>
    public static class ContextHelp
    {
        public const string MoveTitle = "MOVE";

        public const string MoveBody =
            "Arm MOVE (M), select a friendly unit, then left-click a destination on the map. " +
            "The unit paths by terrain-cost A* and the order joins its queue. " +
            "Esc clears the armed verb; right-click shortcuts still work. " +
            "This is in-session help for one control — the glossary is #124.";

        /// <summary>True when <paramref name="verb"/> has authored context help.</summary>
        public static bool TryGet(PaletteVerb verb, out string title, out string body)
        {
            if (verb == PaletteVerb.MoveTo)
            {
                title = MoveTitle;
                body = MoveBody;
                return true;
            }

            title = null;
            body = null;
            return false;
        }
    }
}
