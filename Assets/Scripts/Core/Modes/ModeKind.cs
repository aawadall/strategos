// ModeKind.cs
// #287 / #294: how a PLAY session is contested — distinct from MapRenderMode.
//
// Solo is today's default (player vs SideDirector). Hotseat / Spectator / Replay are the
// Phase 7 modes that sit in front of scenario pick, not as new shell tabs.

namespace Strategos.Modes
{
    /// <summary>PLAY session mode (#287). Not map render mode.</summary>
    public enum ModeKind
    {
        /// <summary>Player commands one side; opposing sides run under SideDirector.</summary>
        Solo = 0,

        /// <summary>Two humans, one machine; active side switches; no directors by default.</summary>
        Hotseat = 1,

        /// <summary>Both sides directed; player watches and does not Issue.</summary>
        Spectator = 2,

        /// <summary>Playback of a recorded run through <c>Commands.Replayer</c>.</summary>
        Replay = 3,
    }
}
