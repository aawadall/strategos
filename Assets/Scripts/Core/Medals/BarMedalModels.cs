// BarMedalModels.cs
// #467: service-ribbon defs and awards — data only until bake / grant.

using System;

namespace Strategos.Medals
{
    /// <summary>Binding display order: Training → Campaign → Historical → Merit → Mode.</summary>
    public enum BarMedalCategory
    {
        Training = 0,
        Campaign = 1,
        Historical = 2,
        Merit = 3,
        Mode = 4,
    }

    public enum BarMedalDevice
    {
        None = 0,
        Star = 1,
        Numeral = 2,
        Oak = 3,
    }

    /// <summary>Catalogue entry. Public fields for <see cref="Scenarios.FieldsOnlyResolver"/>.</summary>
    public sealed class BarMedalDef
    {
        public string Id = string.Empty;
        public BarMedalCategory Category = BarMedalCategory.Merit;
        public string Title = string.Empty;
        public string Description = string.Empty;

        /// <summary>Ordered ribbon stripe colours as #RRGGBB (or #RRGGBBAA).</summary>
        public string[] Stripes = Array.Empty<string>();

        public BarMedalDevice Device = BarMedalDevice.None;

        /// <summary>Optional Steam map — unused until Phase 10.</summary>
        public string SteamAchievementId = string.Empty;
    }

    /// <summary>One grant. Idempotent on <see cref="MedalId"/> unless <see cref="Count"/> stacks.</summary>
    public sealed class BarMedalAward
    {
        public string MedalId = string.Empty;

        /// <summary>Stack count for numeral Merit medals (neutralized enemies).</summary>
        public int Count = 1;

        public int Tick;
        public string ScenarioName = string.Empty;
    }

    public sealed class BarMedalCatalog
    {
        public string Name = string.Empty;
        public string Source = string.Empty;
        public BarMedalDef[] Medals = Array.Empty<BarMedalDef>();
    }
}
