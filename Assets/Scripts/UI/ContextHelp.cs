// ContextHelp.cs
// #308 / #442 / #445 / #447: in-PLAY contextual copy for palette controls
// (MOVE, ENGAGE, WAYPOINTS, DIG IN). Not the field manual (#124).

namespace Strategos.UI
{
    /// <summary>
    /// Authored blurbs keyed by armed palette verb
    /// (#308 MOVE, #442 ENGAGE, #445 WAYPOINTS, #447 DIG IN).
    /// </summary>
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

        public const string WaypointsTitle = "WAYPOINTS";

        public const string WaypointsBody =
            "Arm WAYPOINTS (W), select a friendly unit, then left-click successive map cells " +
            "to draft a multi-leg march. CONFIRM ROUTE commits the legs as queued MoveTos; " +
            "Esc clears the draft and the armed verb. Path preview uses the same A* costs as MOVE. " +
            "This is in-session help — the glossary is #124.";

        public const string DigInTitle = "DIG IN";

        public const string DigInBody =
            "Arm DIG IN (D), select a friendly unit that can dig in, then left-click to issue. " +
            "Bridges to Hold/Defend: the unit digs in over about two minutes and then takes " +
            "half the incoming fire. Units without the dig-in capability are refused (CANNOT DIG IN). " +
            "Esc clears the armed verb. This is in-session help — the glossary is #124.";

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

            if (verb == PaletteVerb.Waypoints)
            {
                title = WaypointsTitle;
                body = WaypointsBody;
                return true;
            }

            if (verb == PaletteVerb.DigIn)
            {
                title = DigInTitle;
                body = DigInBody;
                return true;
            }

            title = null;
            body = null;
            return false;
        }
    }
}
