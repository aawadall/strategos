// NullSteamClient.cs
// #301 / #305: always-safe Steam stand-in. Used until Steamworks.NET is linked and an App ID
// exists. Records last stub calls so probes can assert the seam without a Steam client.

using UnityEngine;

namespace Strategos.Steam
{
    /// <summary>No-op <see cref="ISteamClient"/> — CI and local runs without partner App ID.</summary>
    public sealed class NullSteamClient : ISteamClient
    {
        public bool IsAvailable => false;
        public uint AppId { get; private set; }

        /// <summary>Last overlay dialog requested (probe / debug).</summary>
        public string LastOverlayDialog { get; private set; }

        /// <summary>Last achievement id requested.</summary>
        public string LastAchievementId { get; private set; }

        /// <summary>Last cloud write name, if any.</summary>
        public string LastCloudWriteName { get; private set; }

        public bool Init()
        {
            AppId = SteamAppId.TryRead(out var id) ? id : 0u;
            if (AppId == 0)
                Debug.Log("[Steam] NullSteamClient.Init — no App ID (expected until partner registration)");
            else
                Debug.Log(
                    $"[Steam] NullSteamClient.Init — App ID {AppId} present, but Steamworks.NET " +
                    "not linked; staying unavailable (see docs/steam.md)");
            return false;
        }

        public void Shutdown() { }

        public void ActivateOverlay(string dialog)
        {
            LastOverlayDialog = dialog ?? string.Empty;
            Debug.Log($"[Steam] Overlay stub (unavailable): '{LastOverlayDialog}'");
        }

        public void SetAchievement(string achievementId)
        {
            LastAchievementId = achievementId ?? string.Empty;
            Debug.Log($"[Steam] Achievement stub (unavailable): '{LastAchievementId}'");
        }

        public bool CloudWrite(string fileName, byte[] data)
        {
            LastCloudWriteName = fileName ?? string.Empty;
            Debug.Log(
                $"[Steam] CloudWrite stub (unavailable): '{LastCloudWriteName}' " +
                $"({data?.Length ?? 0} bytes)");
            return false;
        }

        public byte[] CloudRead(string fileName)
        {
            Debug.Log($"[Steam] CloudRead stub (unavailable): '{fileName}'");
            return null;
        }
    }
}
