// MainMenuView.cs
// #371: front door outside the tab shell — Play, campaign/scenario starts, Load/Save,
// Options (#306 settings shell), Help (how-to + alpha limits), Server disabled (online),
// Tools tabs.
//
// #427: fit the full menu to the host height — no ScrollRect. Compact rows + two-column
// blocks, then scale button heights so EXIT stays on-screen.
// #428: EXIT quits the player (Editor stops Play mode).
// #429: AUDIO opens Settings (MASTER / MUSIC / SFX already live there).
// Free alpha: HELP opens AlphaHelpOverlay (how-to-play + fog / artillery DF / no ZoC).
//
// Aged-paper aesthetic (UiTheme + PaperTexture Clean for the button stack). Not a dark
// glass game-menu skin.

using System;
using System.Collections.Generic;
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
        private const float MinButtonHeight = 22f;
        private const float MaxButtonHeight = 36f;
        private const float SectionHeight = 16f;

        public string Title => "MENU";
        public string Key => "menu";

        public AppSession Session { get; set; }
        public AppShell Shell { get; set; }

        private Texture2D _paperTex;
        private RectTransform _column;
        private RectTransform _host;
        private Button _saveBtn;
        private Button _loadBtn;
        private Button _continueBtn;
        private Button _exitBtn;
        private Button _audioBtn;
        private Button _helpBtn;
        private AlphaHelpOverlay _help;
        private readonly List<LayoutElement> _scalableButtons = new List<LayoutElement>();

        public void Build(RectTransform host)
        {
            _scalableButtons.Clear();
            _host = host;

            var root = CreateRect("Root", host);
            Stretch(root);
            root.gameObject.AddComponent<Image>().color = Theme.StageBg;

            _paperTex = PaperTexture.Create(960, 1080, PaperTexture.SeedFor("main-menu"),
                PaperOptions.Clean);
            var paper = CreateRect("Paper", root);
            Stretch(paper);
            paper.offsetMin = new Vector2(40, 16);
            paper.offsetMax = new Vector2(-40, -16);
            var raw = paper.gameObject.AddComponent<RawImage>();
            raw.texture = _paperTex;
            raw.color = Color.white;
            raw.raycastTarget = false;

            _column = CreateRect("Column", paper);
            Stretch(_column);
            _column.offsetMin = new Vector2(28, 12);
            _column.offsetMax = new Vector2(-28, -12);
            var v = _column.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 4;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.padding = new RectOffset(8, 8, 4, 4);

            var brand = CreateTmp("Brand", _column, "STRATEGOS", 32, FontStyles.Bold);
            brand.alignment = TextAlignmentOptions.Center;
            brand.color = Theme.Ink;
            brand.characterSpacing = 10f;
            brand.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;

            var sub = CreateTmp("Sub", _column,
                "Playable alpha · HELP for how-to and known limits",
                13, FontStyles.Normal);
            sub.alignment = TextAlignmentOptions.Center;
            sub.color = Theme.InkMuted;
            sub.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

            MenuButton(_column, "PLAY / CONTINUE", () => Shell?.EnterPlaySession());
            _continueBtn = MenuButton(_column, "RESUME LAST SESSION",
                () => Shell?.EnterPlaySession());

            Section(_column, "CAMPAIGN / SCENARIO");
            var camp = TwoCol(_column);
            MenuButton(camp.left, "START VALLEY CAMPAIGN",
                () => Shell?.StartValleyFromMenu());
            MenuButton(camp.left, "START HIGHLAND CAMPAIGN",
                () => Shell?.StartHighlandFromMenu());
            MenuButton(camp.left, "START CLIMB CAMPAIGN",
                () => Shell?.StartClimbFromMenu());
            MenuButton(camp.left, "SKIRMISH ONLY",
                () => Shell?.LoadScenarioFromMenu(ScenarioSamples.SkirmishName));
            MenuButton(camp.left, "DRILL RANGE: T1",
                () => Shell?.LoadScenarioFromMenu(ScenarioSamples.DrillRangeT1Name));
            MenuButton(camp.right, "PUSH NORTH",
                () => Shell?.LoadScenarioFromMenu(ScenarioSamples.PushNorthName));
            MenuButton(camp.right, "SQUAD TUTORIAL",
                () => Shell?.LoadScenarioFromMenu(ScenarioSamples.TutorialName));
            MenuButton(camp.right, "LITTLE ROUND TOP",
                () => Shell?.LoadScenarioFromMenu(ScenarioSamples.LittleRoundTopName));
            MenuButton(camp.right, "BELLEAU WOOD",
                () => Shell?.LoadScenarioFromMenu(ScenarioSamples.BelleauWoodName));
            MenuButton(camp.right, "REMAGEN BRIDGE",
                () => Shell?.LoadScenarioFromMenu(ScenarioSamples.RemagenName));

            Section(_column, "SESSION / TOOLS");
            var mid = TwoCol(_column);
            _saveBtn = MenuButton(mid.left, "SAVE", () => Shell?.QuickSaveFromMenu());
            _loadBtn = MenuButton(mid.left, "LOAD", () => Shell?.QuickLoadFromMenu());
            MenuButton(mid.right, "EXPLORE", () => Shell?.EnterTools("explore"));
            MenuButton(mid.right, "SCENARIO", () => Shell?.EnterTools("scenario"));
            MenuButton(mid.right, "DRILLS", () => Shell?.EnterTools("ttp"));
            MenuButton(mid.right, "BUILDER", () => Shell?.EnterTools("builder"));

            Section(_column, "ALSO");
            var also = TwoCol(_column);
            _helpBtn = MenuButton(also.left, "HELP", OpenHelp);
            var server = MenuButton(also.right, "SERVER  ·  needs online", () => { });
            server.interactable = false;

            var footer = CreateRect("Footer", _column);
            var fv = footer.gameObject.AddComponent<VerticalLayoutGroup>();
            fv.spacing = 4;
            fv.childAlignment = TextAnchor.UpperCenter;
            fv.childControlWidth = true;
            fv.childControlHeight = true;
            fv.childForceExpandWidth = true;
            fv.childForceExpandHeight = false;
            var footFit = footer.gameObject.AddComponent<ContentSizeFitter>();
            footFit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            footFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            MenuButton(footer, "OPTIONS", () => Shell?.OpenSettings());
            _audioBtn = MenuButton(footer, "AUDIO", () => Shell?.OpenSettings());
            _exitBtn = MenuButton(footer, "EXIT", QuitApplication);

            _help = host.gameObject.AddComponent<AlphaHelpOverlay>();
            _help.Build(host);

            Canvas.ForceUpdateCanvases();
            FitToHostHeight();
            RefreshSessionButtons();
        }

        public void OnShown()
        {
            FitToHostHeight();
            RefreshSessionButtons();
        }

        public void OnHidden()
        {
            _help?.Close();
        }

        public void OpenHelp()
        {
            if (_help == null && _host != null)
            {
                _help = _host.gameObject.AddComponent<AlphaHelpOverlay>();
                _help.Build(_host);
            }
            _help?.Open();
        }

        /// <summary>
        /// Scale menu button heights so the column fits the paper rect — no scroll.
        /// </summary>
        public void FitToHostHeight()
        {
            if (_column == null || _scalableButtons.Count == 0) return;

            Canvas.ForceUpdateCanvases();
            float available = _column.rect.height;
            if (available < 80f) return;

            SetButtonHeights(MinButtonHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_column);
            float atMin = LayoutUtility.GetPreferredHeight(_column);
            float overhead = atMin - _scalableButtons.Count * MinButtonHeight;
            float forButtons = Mathf.Max(0f, available - overhead);
            float h = Mathf.Clamp(forButtons / _scalableButtons.Count,
                MinButtonHeight, MaxButtonHeight);
            SetButtonHeights(h);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_column);
        }

        private void SetButtonHeights(float h)
        {
            for (int i = 0; i < _scalableButtons.Count; i++)
            {
                _scalableButtons[i].preferredHeight = h;
                _scalableButtons[i].minHeight = h;
            }
        }

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

        private Button MenuButton(Transform parent, string label, Action onClick)
        {
            var btn = AddButton(parent, label, onClick);
            var le = btn.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.preferredHeight = MaxButtonHeight;
                le.minHeight = MinButtonHeight;
                _scalableButtons.Add(le);
            }
            // Slightly smaller type so compact rows still read.
            var tmp = btn.GetComponentInChildren<TMP_Text>();
            if (tmp != null) tmp.fontSize = 12f;
            return btn;
        }

        private static (Transform left, Transform right) TwoCol(Transform parent)
        {
            var row = CreateRect("Row", parent);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8;
            h.childAlignment = TextAnchor.UpperCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = false;
            var rowFit = row.gameObject.AddComponent<ContentSizeFitter>();
            rowFit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            rowFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var left = CreateRect("Left", row);
            left.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var lv = left.gameObject.AddComponent<VerticalLayoutGroup>();
            lv.spacing = 4;
            lv.childControlWidth = true;
            lv.childControlHeight = true;
            lv.childForceExpandWidth = true;
            lv.childForceExpandHeight = false;

            var right = CreateRect("Right", row);
            right.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var rv = right.gameObject.AddComponent<VerticalLayoutGroup>();
            rv.spacing = 4;
            rv.childControlWidth = true;
            rv.childControlHeight = true;
            rv.childForceExpandWidth = true;
            rv.childForceExpandHeight = false;

            return (left, right);
        }

        private static void Section(Transform parent, string label)
        {
            var t = CreateTmp("Sec", parent, label, 11, FontStyles.Bold);
            t.color = Theme.InkMuted;
            t.characterSpacing = 3f;
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = SectionHeight;
        }
    }
}
