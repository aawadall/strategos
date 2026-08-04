// IPlayerIdentity.cs
// #355 / #367: who is playing — anonymous local stub today; Steam-backed impl is #288.
//
// Same bar as ISidePolicy: an implementation must not need a concrete Simulation or a
// filesystem path. OAuth / registration UI is out of scope for this epic.

namespace Strategos.Identity
{
    /// <summary>Player identity for saves, career, and (later) online services.</summary>
    public interface IPlayerIdentity
    {
        /// <summary>Stable id for this player on this backend (not a display string).</summary>
        string PlayerId { get; }

        /// <summary>Human-readable name for chrome.</summary>
        string DisplayName { get; }
    }
}
