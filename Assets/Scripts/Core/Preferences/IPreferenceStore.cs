// IPreferenceStore.cs
// #307: load/save player options for the settings screen. Not IGameStore — prefs are not
// a run snapshot, and #66's embedded DB choice may still absorb this later.

namespace Strategos.Preferences
{
    /// <summary>Read/write <see cref="PlayerPreferences"/>. Implementations own the bytes.</summary>
    public interface IPreferenceStore
    {
        PlayerPreferences Load();
        void Save(PlayerPreferences preferences);
    }
}
