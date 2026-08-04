// SettingsView.cs
// #306: settings screen shell — graphics / audio / gameplay / accessibility categories
// with no controls yet (#307 preference store; audio wiring #40). Opened from main-menu
// Options; paper aesthetic matching MainMenuView.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

using Theme = Strategos.UI.UiTheme;
using static Strategos.UI.UiFactory;

namespace Strategos.UI.Views
{
    public sealed class SettingsView : MonoBehaviour, IAppView
    {
        public string Title => "OPTIONS";
        public string Key => "settings";

        public AppShell Shell { get; set; }

        private Texture2D _paperTex;

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
                "Categories only · controls arrive with #307 and later #289 children",
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

        public void OnShown() { }

        public void OnHidden() { }

        private void OnDestroy()
        {
            if (_paperTex != null)
            {
                Destroy(_paperTex);
                _paperTex = null;
            }
        }

        private static void CategoryBlock(Transform parent, string label)
        {
            var t = CreateTmp("Cat_" + label, parent, label, 12, FontStyles.Bold);
            t.color = Theme.InkMuted;
            t.characterSpacing = 4f;
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

            var empty = CreateTmp("Empty_" + label, parent, "No options yet", 14, FontStyles.Italic);
            empty.alignment = TextAlignmentOptions.Center;
            empty.color = Theme.InkMuted;
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

            Spacer(parent, 6);
        }

        private static void Spacer(Transform parent, float h)
        {
            var s = CreateRect("Sp", parent);
            s.gameObject.AddComponent<LayoutElement>().preferredHeight = h;
        }
    }
}
