// JsonPreferenceStore.cs
// #307: JSON file for PlayerPreferences — same "bytes outside Core" split as FileGameStore.
// One small document at persistentDataPath/preferences.json; missing file → defaults.

using System;
using System.IO;
using Newtonsoft.Json;
using Strategos.Preferences;
using UnityEngine;

namespace Strategos.Persistence.Files
{
    public sealed class JsonPreferenceStore : IPreferenceStore
    {
        private readonly string _path;

        public static string DefaultPath =>
            Path.Combine(Application.persistentDataPath, "preferences.json");

        public JsonPreferenceStore(string path = null)
        {
            _path = string.IsNullOrWhiteSpace(path) ? DefaultPath : path;
        }

        public PlayerPreferences Load()
        {
            try
            {
                if (!File.Exists(_path))
                    return new PlayerPreferences();
                var json = File.ReadAllText(_path);
                var prefs = JsonConvert.DeserializeObject<PlayerPreferences>(json);
                return prefs ?? new PlayerPreferences();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[JsonPreferenceStore] Load failed ({ex.Message}); using defaults.");
                return new PlayerPreferences();
            }
        }

        public void Save(PlayerPreferences preferences)
        {
            if (preferences == null)
                throw new ArgumentNullException(nameof(preferences));
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonConvert.SerializeObject(preferences, Formatting.Indented));
        }
    }
}
