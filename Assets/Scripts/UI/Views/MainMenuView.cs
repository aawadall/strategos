// MainMenuView.cs
// #371: front door outside the tab shell — Play, campaign/scenario starts, Load/Save,
// Options (#306 settings shell), Help stub (#124), Server disabled (online / App ID),
// Tools tabs.
//
// #426 / #427: scrollable campaign/tools stack; footer pins OPTIONS / AUDIO / EXIT so they
// stay on-screen without scrolling (wheel failed when the viewport had no raycast Graphic).
// #428: EXIT quits the player (Editor stops Play mode).
// #429: AUDIO opens Settings (MASTER / MUSIC / SFX already live there).
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
        /// <summary>Footer height reserved for OPTIONS / AUDIO / EXIT (#427 fix).</summary>
        public const float FooterHeight = 168f;

        public string Title => "MENU";
        public string Key => "menu";

        public AppSession Session { get; set; }
        public AppShell Shell { get; set; }

        private Texture2D _paperTex;
        private Button _saveBtn;
        private Button _loadBtn;
        private Button _continueBtn;
        private Button _exitBtn;
        private Button _audioBtn;

        public void Build(RectTransform host)
        {
            var root = CreateRect("Root", host);
            Stretch(root);
            root.gameObject.AddComponent<Image>().color = Theme.StageBg;

            _paperTex = PaperTexture.Create(960, 1080, PaperTexture.SeedFor("main-menu"),
                PaperOptions.Clean);
            var paper = CreateRect("Paper", root);
            Stretch(paper);
            paper.offsetMin = new Vector2(48, 24);
            paper.offsetMax = new Vector2(-48, -24);
            var raw = paper.gameObject.AddComponent<RawImage>();
            raw.texture = _paperTex;
            raw.color = Color.white;
            raw.raycastTarget = false;

            // Body: scroll (flex) + sticky footer so EXIT is always visible.
            var body = CreateRect("Body", paper);
            Stretch(body);
            body.offsetMin = new Vector2(40, 28);
            body.offsetMax = new Vector2(-40, -28);
            var bodyV = body.gameObject.AddComponent<VerticalLayoutGroup>();
            bodyV.spacing = 8;
            bodyV.childAlignment = TextAnchor.UpperCenter;
            bodyV.childControlWidth = true;
            bodyV.childControlHeight = true;
            bodyV.childForceExpandWidth = true;
            bodyV.childForceExpandHeight = false;

            var scrollRt = CreateRect("Scroll", body);
            var scrollLe = scrollRt.gameObject.AddComponent<LayoutElement>();
            scrollLe.flexibleHeight = 1f;
            scrollLe.minHeight = 120f;
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 55f;

            var viewport = CreateRect("Viewport", scrollRt);
            Stretch(viewport);
            // ScrollRect only receives wheel/drag on a Graphic — RectMask2D alone is not enough.
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0.01f);
            vpImg.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            var col = CreateRect("Column", viewport);
            col.anchorMin = new Vector2(0, 1);
            col.anchorMax = new Vector2(1, 1);
            col.pivot = new Vector2(0.5f, 1);
            col.anchoredPosition = Vector2.zero;
            col.sizeDelta = new Vector2(0, 0);
            var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.padding = new RectOffset(16, 16, 8, 16);
            var fitter = col.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = col;

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
            var help = AddButton(col, "HELP  ·  see #124", () => { });
            help.interactable = false;
            var server = AddButton(col, "SERVER  ·  needs online", () => { });
            server.interactable = false;

            // Sticky footer — always on screen; do not require scroll to quit (#427 / #428).
            var footer = CreateRect("Footer", body);
            var footerLe = footer.gameObject.AddComponent<LayoutElement>();
            footerLe.preferredHeight = FooterHeight;
            footerLe.minHeight = FooterHeight;
            footerLe.flexibleHeight = 0f;
            var fv = footer.gameObject.AddComponent<VerticalLayoutGroup>();
            fv.spacing = 8;
            fv.childAlignment = TextAnchor.UpperCenter;
            fv.childControlWidth = true;
            fv.childControlHeight = true;
            fv.childForceExpandWidth = true;
            fv.childForceExpandHeight = false;
            fv.padding = new RectOffset(16, 16, 4, 4);

            AddButton(footer, "OPTIONS", () => Shell?.OpenSettings());
            _audioBtn = AddButton(footer, "AUDIO", () => Shell?.OpenSettings());
            _exitBtn = AddButton(footer, "EXIT", QuitApplication);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(col);
            LayoutRebuilder.ForceRebuildLayoutImmediate(body);

            RefreshSessionButtons();
        }

        public void OnShown() => RefreshSessionButtons();

        public void OnHidden() { }

        /// <summary>
        /// #428 — player builds quit; Editor stops Play mode so probes never hang.
        /// Reflection avoids a UnityEditor asmdef reference from Strategos.Runtime.
        /// </summary>
        public static void QuitApplication()
        {
#if UNITY_EDITOR
            var editorApp = Type.GetType("UnityEditor.EditorApplication,UnityEditor");
            editorApp?.GetProperty("isPlaying")?.SetValue(null, false);
#else
            Application.Quit();
#endif
        }

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
