// LocalAnonymousIdentity.cs
// #355 / #367: default identity when no Steam / OAuth backend is wired.

namespace Strategos.Identity
{
    /// <summary>Local anonymous player — always available; not a network account.</summary>
    public sealed class LocalAnonymousIdentity : IPlayerIdentity
    {
        public const string DefaultPlayerId = "local-anonymous";
        public const string DefaultDisplayName = "Commander";

        public string PlayerId { get; }
        public string DisplayName { get; }

        public LocalAnonymousIdentity(
            string playerId = DefaultPlayerId,
            string displayName = DefaultDisplayName)
        {
            PlayerId = string.IsNullOrWhiteSpace(playerId) ? DefaultPlayerId : playerId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? DefaultDisplayName : displayName;
        }

        /// <summary>Shared default instance for call sites that do not inject.</summary>
        public static LocalAnonymousIdentity Shared { get; } = new();
    }
}
