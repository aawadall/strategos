// SteamAppId.cs
// #300 / #302: read steam_appid.txt from the player/data directory or project root.

using System;
using System.IO;
using UnityEngine;

namespace Strategos.Steam
{
    /// <summary>Locates and parses the development App ID file.</summary>
    public static class SteamAppId
    {
        public const string FileName = "steam_appid.txt";
        public const string ExampleFileName = "steam_appid.txt.example";

        /// <summary>
        /// Tries to read a uint App ID. Looks beside the executable (player) then
        /// <see cref="Application.dataPath"/>'s parent (Editor project root).
        /// </summary>
        public static bool TryRead(out uint appId)
        {
            appId = 0;
            var path = ResolvePath();
            if (path == null) return false;

            try
            {
                var text = File.ReadAllText(path);
                foreach (var raw in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    if (uint.TryParse(line, out appId) && appId > 0) return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Steam] could not read {path}: {e.Message}");
            }

            return false;
        }

        /// <summary>Absolute path to an existing steam_appid.txt, or null.</summary>
        public static string ResolvePath()
        {
            // Player: next to the .exe
            var besideExe = Path.Combine(Directory.GetParent(Application.dataPath)?.FullName
                                         ?? Application.dataPath, FileName);
            if (File.Exists(besideExe)) return besideExe;

            // Editor: project root (parent of Assets)
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrEmpty(projectRoot))
            {
                var atRoot = Path.Combine(projectRoot, FileName);
                if (File.Exists(atRoot)) return atRoot;
            }

            return null;
        }
    }
}
