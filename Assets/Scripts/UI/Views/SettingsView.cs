// SettingsView.cs
// #306: settings screen shell — graphics / audio / gameplay / accessibility.
// #307: GAMEPLAY hosts one persisted ConfirmOrders toggle via IPreferenceStore.
// #389: GRAPHICS fullscreen toggle → AppShell display API + PlayerPreferences.Fullscreen.
// #390: GRAPHICS windowed size presets (not a fullscreen resolution list).

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Strategos.Audio;
using Strategos.Persistence.Files;
using Strategos.Preferences;
using Strategos.Steam;

using Theme = Strategos.UI.UiTheme;
using static Strategos.UI.UiFactory;

namespace Strategos.UI.Views
{
    public sealed class SettingsView : MonoBehaviour, IAppView
    {
        public string Title => "OPTIONS";
        public string Key => "settings";

        public AppShell Shell { get; set; }

        /// <summary>Injected for probes; defaults to <see cref="JsonPreferenceStore"/>.</summary>
        public IPreferenceStore Store { get; set; }

        private Texture2D _paperTex;
        private Toggle _confirmOrders;
        private Toggle _fullscreen;
        private TMP_Dropdown _windowedSize;
        private Slider _masterVol;
        private Slider _musicVol;
        private Slider _sfxVol;
        private PlayerPreferences _prefs;

        /// <summary>Category section labels in display order (#306).</summary>
        public static readonly string[] Categories =
        {
            "GRAPHICS",
            "AUDIO",
            "GAMEPLAY",
            "ACCESSIBILITY",
        };

        /// <summary>
        /// Windowed size presets only (#390) — borderless fullscreen always matches the
        /// display; CanvasScaler scales the UI.
        /// </summary>
        public static readonly (string Label, int Width, int Height)[] WindowedPresets =
        {
            ("1280 × 720", 1280, 720),
            ("1600 × 900", 1600, 900),
            ("1920 × 1080", 1920, 1080),
        };

        public void Build(RectTransform host)
        {
            Store ??= new JsonPreferenceStore();
            _prefs = Store.Load();

            var root = CreateRect("Root", host);
            Stretch(root);
            root.gameObject.AddComponent<Image>().color = Theme.StageBg;

            _paperTex = PaperTexture.Create(960, 1080, PaperTexture.SeedFor("settings"),
                PaperOptions.Clean);
            var paper = CreateRect("Paper", root);
            Stretch(paper);
            paper.offsetMin = new Vector2(80, 40);
            paper.offsetMax = new Vector2(-80, -40);
            var raw = paper.gameObject.AddComponent<RawImage>();
            raw.texture = _paperTex;
            raw.color = Color.white;

            var col = CreateRect("Column", paper);
            Stretch(col);
            col.offsetMin = new Vector2(100, 60);
            col.offsetMax = new Vector2(-100, -60);
            var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.padding = new RectOffset(24, 24, 24, 24);

            var title = CreateTmp("Title", col, "OPTIONS", 32, FontStyles.Bold);
            title.alignment = TextAlignmentOptions.Center;
            title.color = Theme.Ink;
            title.characterSpacing = 10f;
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;

            var sub = CreateTmp("Sub", col,
                "Fullscreen · windowed size · audio · confirm-orders",
                14, FontStyles.Normal);
            sub.alignment = TextAlignmentOptions.Center;
            sub.color = Theme.InkMuted;
            sub.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;

            Spacer(col, 12);

            foreach (var cat in Categories)
                CategoryBlock(col, cat);

            Spacer(col, 16);
            AddButton(col, "BACK TO MENU", () => Shell?.GoToMainMenu());
        }

        public void OnShown()
        {
            Store ??= new JsonPreferenceStore();
            _prefs = Store.Load();
            if (_confirmOrders != null)
                _confirmOrders.SetIsOnWithoutNotify(_prefs.ConfirmOrders);
            SyncFullscreenToggle();
            SyncWindowedSizeDrop();
            SyncAudioSliders();
        }

        public void OnHidden() => HideDropdownsIn(transform);

        private void OnDestroy()
        {
            if (_paperTex != null)
            {
                Destroy(_paperTex);
                _paperTex = null;
            }
        }

        private void CategoryBlock(Transform parent, string label)
        {
            var t = CreateTmp("Cat_" + label, parent, label, 12, FontStyles.Bold);
            t.color = Theme.InkMuted;
            t.characterSpacing = 4f;
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

            if (label == "GRAPHICS")
            {
                var on = Shell != null ? Shell.IsFullscreen : _prefs.Fullscreen;
                _fullscreen = AddToggle(parent, "FULLSCREEN", on, PersistFullscreen);

                _windowedSize = AddDropdown(parent, "WINDOWED SIZE", PersistWindowedSize);
                var labels = new string[WindowedPresets.Length];
                for (int i = 0; i < WindowedPresets.Length; i++)
                    labels[i] = WindowedPresets[i].Label;
                SetDrop(_windowedSize, labels, IndexOfPreset(_prefs.WindowWidth, _prefs.WindowHeight));

                var hint = CreateTmp("WinHint", parent,
                    "Windowed only — fullscreen matches the display",
                    11, FontStyles.Italic);
                hint.alignment = TextAlignmentOptions.Center;
                hint.color = Theme.InkMuted;
                hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

                Spacer(parent, 6);
                return;
            }

            if (label == "AUDIO")
            {
                (_masterVol, _) = AddSlider(parent, "MASTER", 0f, 100f,
                    _prefs.MasterVolume * 100f, PersistAudioVolumes);
                (_musicVol, _) = AddSlider(parent, "MUSIC", 0f, 100f,
                    _prefs.MusicVolume * 100f, PersistAudioVolumes);
                (_sfxVol, _) = AddSlider(parent, "SFX", 0f, 100f,
                    _prefs.SfxVolume * 100f, PersistAudioVolumes);
                Spacer(parent, 6);
                return;
            }

            if (label == "GAMEPLAY")
            {
                _confirmOrders = AddToggle(parent, "CONFIRM ORDERS", _prefs.ConfirmOrders, PersistConfirm);
                // #303: Overlay smoke — no-ops when Steam / App ID absent (NullSteamClient).
                AddButton(parent, "STEAM OVERLAY", () =>
                    SteamClientHost.Client.ActivateOverlay("Friends"));
                Spacer(parent, 6);
                return;
            }

            var empty = CreateTmp("Empty_" + label, parent, "No options yet", 14, FontStyles.Italic);
            empty.alignment = TextAlignmentOptions.Center;
            empty.color = Theme.InkMuted;
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

            Spacer(parent, 6);
        }

        private void SyncFullscreenToggle()
        {
            if (_fullscreen == null) return;
            var on = Shell != null ? Shell.IsFullscreen : _prefs.Fullscreen;
            _fullscreen.SetIsOnWithoutNotify(on);
        }

        private void SyncWindowedSizeDrop()
        {
            if (_windowedSize == null || _prefs == null) return;
            var idx = IndexOfPreset(_prefs.WindowWidth, _prefs.WindowHeight);
            _windowedSize.SetValueWithoutNotify(idx);
            _windowedSize.RefreshShownValue();
        }

        private void PersistFullscreen()
        {
            if (_fullscreen == null || Store == null) return;
            _prefs ??= new PlayerPreferences();
            _prefs.Fullscreen = _fullscreen.isOn;
            EnsureWindowedDefaults();
            Store.Save(_prefs);

            if (Shell == null) return;
            if (_fullscreen.isOn)
                Shell.ApplyFullscreen();
            else
                Shell.ApplyWindowed(_prefs.WindowWidth, _prefs.WindowHeight);
        }

        private void PersistWindowedSize()
        {
            if (_windowedSize == null || Store == null) return;
            _prefs ??= new PlayerPreferences();
            var preset = WindowedPresets[Mathf.Clamp(_windowedSize.value, 0, WindowedPresets.Length - 1)];
            _prefs.WindowWidth = preset.Width;
            _prefs.WindowHeight = preset.Height;
            Store.Save(_prefs);

            // Apply immediately only when windowed — fullscreen keeps display match (#385).
            var fullscreen = Shell != null ? Shell.IsFullscreen : _prefs.Fullscreen;
            if (!fullscreen)
                Shell?.ApplyWindowed(_prefs.WindowWidth, _prefs.WindowHeight);
        }

        private void PersistConfirm()
        {
            if (_confirmOrders == null || Store == null) return;
            _prefs ??= new PlayerPreferences();
            _prefs.ConfirmOrders = _confirmOrders.isOn;
            Store.Save(_prefs);
        }

        private void SyncAudioSliders()
        {
            if (_prefs == null) return;
            if (_masterVol != null)
                SetSliderValue(_masterVol, _prefs.MasterVolume * 100f);
            if (_musicVol != null)
                SetSliderValue(_musicVol, _prefs.MusicVolume * 100f);
            if (_sfxVol != null)
                SetSliderValue(_sfxVol, _prefs.SfxVolume * 100f);
        }

        private void PersistAudioVolumes()
        {
            if (Store == null) return;
            _prefs ??= new PlayerPreferences();
            if (_masterVol != null) _prefs.MasterVolume = Mathf.Clamp01(_masterVol.value / 100f);
            if (_musicVol != null) _prefs.MusicVolume = Mathf.Clamp01(_musicVol.value / 100f);
            if (_sfxVol != null) _prefs.SfxVolume = Mathf.Clamp01(_sfxVol.value / 100f);
            Store.Save(_prefs);
            AudioService.Instance?.ApplyPreferences(_prefs);
        }

        private void EnsureWindowedDefaults()
        {
            if (_prefs.WindowWidth > 0 && _prefs.WindowHeight > 0) return;
            _prefs.WindowWidth = 1600;
            _prefs.WindowHeight = 900;
        }

        /// <summary>Nearest preset index; defaults to 1600×900 when no exact match.</summary>
        public static int IndexOfPreset(int width, int height)
        {
            for (int i = 0; i < WindowedPresets.Length; i++)
            {
                if (WindowedPresets[i].Width == width && WindowedPresets[i].Height == height)
                    return i;
            }
            // Default middle preset (1600×900).
            return 1;
        }

        private static void Spacer(Transform parent, float h)
        {
            var s = CreateRect("Sp", parent);
            s.gameObject.AddComponent<LayoutElement>().preferredHeight = h;
        }
    }
}
