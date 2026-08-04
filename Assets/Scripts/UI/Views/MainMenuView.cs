// MainMenuView.cs
// #371: front door outside the tab shell — Play, campaign/scenario starts, Load/Save,
// Options (#306 settings shell), Help stub (#124), Server disabled (#288), Tools tabs.
//
// Aged-paper aesthetic (UiTheme + PaperTexture Clean for the button stack). Not a dark
// glass game-menu skin.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Strategos.Scenarios;

using Theme = Strategos.UI.UiTheme;
using static Strategos.UI.UiFactory;

namespace Strategos.UI.Views
{
    public sealed class MainMenuView : MonoBehaviour, IAppView
    {
        public string Title => "MENU";
        public string Key => "menu";

        public AppSession Session { get; set; }
        public AppShell Shell { get; set; }

        private Texture2D _paperTex;
        private Button _saveBtn;
        private Button _loadBtn;
        private Button _continueBtn;

        public void Build(RectTransform host)
        {
            var root = CreateRect("Root", host);
            Stretch(root);
            root.gameObject.AddComponent<Image>().color = Theme.StageBg;

            _paperTex = PaperTexture.Create(960, 1080, PaperTexture.SeedFor("main-menu"),
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
            col.offsetMin = new Vector2(120, 80);
            col.offsetMax = new Vector2(-120, -80);
            var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 10;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.padding = new RectOffset(24, 24, 24, 24);

            var brand = CreateTmp("Brand", col, "STRATEGOS", 42, FontStyles.Bold);
            brand.alignment = TextAlignmentOptions.Center;
            brand.color = Theme.Ink;
            brand.characterSpacing = 12f;
            brand.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;

            var sub = CreateTmp("Sub", col,
                "Tactical command · enter a session or open tools",
                15, FontStyles.Normal);
            sub.alignment = TextAlignmentOptions.Center;
            sub.color = Theme.InkMuted;
            sub.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

            Spacer(col, 12);

            AddButton(col, "PLAY / CONTINUE", () => Shell?.EnterPlaySession());
            _continueBtn = AddButton(col, "RESUME LAST SESSION", () => Shell?.EnterPlaySession());

            Spacer(col, 8);
            Section(col, "CAMPAIGN / SCENARIO");
            AddButton(col, "START VALLEY CAMPAIGN", () => Shell?.StartValleyFromMenu());
            AddButton(col, "START HIGHLAND CAMPAIGN", () => Shell?.StartHighlandFromMenu());
            AddButton(col, "START CLIMB CAMPAIGN", () => Shell?.StartClimbFromMenu());
            AddButton(col, "SKIRMISH ONLY", () => Shell?.LoadScenarioFromMenu(ScenarioSamples.SkirmishName));
            AddButton(col, "PUSH NORTH", () => Shell?.LoadScenarioFromMenu(ScenarioSamples.PushNorthName));
            AddButton(col, "SQUAD TUTORIAL",
                () => Shell?.LoadScenarioFromMenu(ScenarioSamples.TutorialName));
            AddButton(col, "LITTLE ROUND TOP",
                () => Shell?.LoadScenarioFromMenu(ScenarioSamples.LittleRoundTopName));

            Spacer(col, 8);
            Section(col, "SESSION");
            _saveBtn = AddButton(col, "SAVE", () => Shell?.QuickSaveFromMenu());
            _loadBtn = AddButton(col, "LOAD", () => Shell?.QuickLoadFromMenu());

            Spacer(col, 8);
            Section(col, "TOOLS");
            AddButton(col, "EXPLORE", () => Shell?.EnterTools("explore"));
            AddButton(col, "SCENARIO", () => Shell?.EnterTools("scenario"));
            AddButton(col, "DRILLS", () => Shell?.EnterTools("ttp"));
            AddButton(col, "BUILDER", () => Shell?.EnterTools("builder"));

            Spacer(col, 8);
            Section(col, "ALSO");
            AddButton(col, "OPTIONS", () => Shell?.OpenSettings());
            var help = AddButton(col, "HELP  ·  see #124", () => { });
            help.interactable = false;
            var server = AddButton(col, "SERVER  ·  needs #288", () => { });
            server.interactable = false;

            RefreshSessionButtons();
        }

        public void OnShown() => RefreshSessionButtons();

        public void OnHidden() { }

        private void OnDestroy()
        {
            if (_paperTex != null)
            {
                Destroy(_paperTex);
                _paperTex = null;
            }
        }

        private void RefreshSessionButtons()
        {
            bool hasSim = Session?.Simulation != null;
            if (_saveBtn != null) _saveBtn.interactable = hasSim;
            if (_loadBtn != null) _loadBtn.interactable = true;
            if (_continueBtn != null) _continueBtn.interactable = hasSim;
        }

        private static void Section(Transform parent, string label)
        {
            var t = CreateTmp("Sec", parent, label, 12, FontStyles.Bold);
            t.color = Theme.InkMuted;
            t.characterSpacing = 4f;
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;
        }

        private static void Spacer(Transform parent, float h)
        {
            var s = CreateRect("Sp", parent);
            s.gameObject.AddComponent<LayoutElement>().preferredHeight = h;
        }
    }
}
