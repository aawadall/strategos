// SettingsView.cs
// #306: settings screen shell — graphics / audio / gameplay / accessibility.
// #307: GAMEPLAY hosts one persisted ConfirmOrders toggle via IPreferenceStore.

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Strategos.Persistence.Files;
using Strategos.Preferences;

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
        private PlayerPreferences _prefs;

        /// <summary>Category section labels in display order (#306).</summary>
        public static readonly string[] Categories =
        {
            "GRAPHICS",
            "AUDIO",
            "GAMEPLAY",
            "ACCESSIBILITY",
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
                "One gameplay preference persists · more controls land with later #289 children",
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
        }

        public void OnHidden() { }

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

            if (label == "GAMEPLAY")
            {
                _confirmOrders = AddToggle(parent, "CONFIRM ORDERS", _prefs.ConfirmOrders, PersistConfirm);
                Spacer(parent, 6);
                return;
            }

            var empty = CreateTmp("Empty_" + label, parent, "No options yet", 14, FontStyles.Italic);
            empty.alignment = TextAlignmentOptions.Center;
            empty.color = Theme.InkMuted;
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

            Spacer(parent, 6);
        }

        private void PersistConfirm()
        {
            if (_confirmOrders == null || Store == null) return;
            _prefs ??= new PlayerPreferences();
            _prefs.ConfirmOrders = _confirmOrders.isOn;
            Store.Save(_prefs);
        }

        private static void Spacer(Transform parent, float h)
        {
            var s = CreateRect("Sp", parent);
            s.gameObject.AddComponent<LayoutElement>().preferredHeight = h;
        }
    }
}
