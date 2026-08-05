// ISteamClient.cs
// #288 / #301: platform seam for Steamworks. NullSteamClient is the default until a real
// App ID + Steamworks.NET (or Facepunch) package is wired behind this interface.

namespace Strategos.Steam
{
    /// <summary>
    /// Minimal Steamworks surface Strategos needs for Phase 0 / early Phase 10 stubs.
    /// Implementations must never throw when Steam is absent or the App ID is unset.
    /// </summary>
    public interface ISteamClient
    {
        /// <summary>True after a successful <see cref="Init"/> against a running Steam client.</summary>
        bool IsAvailable { get; }

        /// <summary>Parsed App ID from <c>steam_appid.txt</c>, or 0 when missing/invalid.</summary>
        uint AppId { get; }

        /// <summary>
        /// Attempt SteamAPI.Init (or no-op). Returns false when no App ID, no Steam client,
        /// or the native package is not linked — never throws (#305).
        /// </summary>
        bool Init();

        /// <summary>SteamAPI.Shutdown counterpart; safe to call when never inited.</summary>
        void Shutdown();

        /// <summary>
        /// Overlay smoke (#303). <paramref name="dialog"/> is a Steam overlay dialog name
        /// (e.g. <c>Friends</c>). No-ops when <see cref="IsAvailable"/> is false.
        /// </summary>
        void ActivateOverlay(string dialog);

        /// <summary>Achievement stub (#304). No-ops without Steam.</summary>
        void SetAchievement(string achievementId);

        /// <summary>Cloud write stub (#304). Returns false without Steam.</summary>
        bool CloudWrite(string fileName, byte[] data);

        /// <summary>Cloud read stub (#304). Returns null without Steam or on miss.</summary>
        byte[] CloudRead(string fileName);
    }
}
