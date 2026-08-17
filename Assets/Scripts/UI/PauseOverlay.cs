// PauseOverlay.cs
// #371 / #206 / #462 / #469 / #519: pause — Save / Load / Drills / Field manual /
// Historical note / Career / Alpha limits / Resume / Exit.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Strategos.Campaigns;
using Strategos.Scenarios;

using Theme = Strategos.UI.UiTheme;
using static Strategos.UI.UiFactory;

namespace Strategos.UI
{
    public sealed class PauseOverlay : MonoBehaviour
    {
        private GameObject _panel;
        private DrillQuickRefPanel _drills;
        private FieldManualBrowserPanel _manual;
        private HistoricalNotePanel _history;
        private CareerPanel _career;
        private AlphaHelpOverlay _alphaHelp;
        private Button _historyButton;
        private Action _onResume;
        private Action _onSave;
        private Action _onLoad;
        private Action _onExitMenu;

        public bool IsOpen => _panel != null && _panel.activeSelf;
        public bool DrillsOpen => _drills != null && _drills.IsOpen;
        public bool ManualOpen => _manual != null && _manual.IsOpen;
        public bool HistoryOpen => _history != null && _history.IsOpen;
        public bool CareerOpen => _career != null && _career.IsOpen;
        public bool AlphaHelpOpen => _alphaHelp != null && _alphaHelp.IsOpen;

        public void Build(RectTransform host, Action onResume, Action onSave, Action onLoad,
            Action onExitMenu)
        {
            _onResume = onResume;
            _onSave = onSave;
            _onLoad = onLoad;
            _onExitMenu = onExitMenu;

            var root = CreateRect("PauseOverlay", host);
            Stretch(root);
            root.SetAsLastSibling();
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0.12f, 0.10f, 0.08f, 0.55f);
            dim.raycastTarget = true;

            var card = CreateRect("Card", root);
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(420, 600);
            card.gameObject.AddComponent<Image>().color = Theme.CardBg;

            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(28, 28, 24, 24);
            v.spacing = 10;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            var title = CreateTmp("Title", card, "PAUSED", 22, FontStyles.Bold);
            title.alignment = TextAlignmentOptions.Center;
            title.color = Theme.Ink;
            title.characterSpacing = 6f;
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;

            var hint = CreateTmp("Hint", card, "Esc resumes · Space still toggles the clock",
                13, FontStyles.Normal);
            hint.alignment = TextAlignmentOptions.Center;
            hint.color = Theme.InkMuted;
            hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;

            AddButton(card, "RESUME", () => _onResume?.Invoke());
            AddButton(card, "SAVE", () => _onSave?.Invoke());
            AddButton(card, "LOAD", () => _onLoad?.Invoke());
            AddButton(card, "DRILLS  (quick ref)", OpenDrills);
            AddButton(card, "FIELD MANUAL", OpenManual);
            _historyButton = AddButton(card, "HISTORICAL NOTE", OpenHistory);
            AddButton(card, "CAREER", OpenCareer);
            AddButton(card, "ALPHA LIMITS", OpenAlphaHelp);
            AddButton(card, "EXIT TO MENU", () => _onExitMenu?.Invoke());

            _drills = root.gameObject.AddComponent<DrillQuickRefPanel>();
            _drills.Build(root);
            _manual = root.gameObject.AddComponent<FieldManualBrowserPanel>();
            _manual.Build(root);
            _history = root.gameObject.AddComponent<HistoricalNotePanel>();
            _history.Build(root);
            _career = root.gameObject.AddComponent<CareerPanel>();
            _career.Build(root);
            _alphaHelp = root.gameObject.AddComponent<AlphaHelpOverlay>();
            _alphaHelp.Build(root);

            _panel = root.gameObject;
            _panel.SetActive(false);
        }

        public void BindScenario(Scenario scenario)
        {
            _history?.Bind(scenario);
            if (_historyButton != null)
                _historyButton.interactable = _history != null && _history.HasNotes;
        }

        public void BindCareer(CareerProfile career) => _career?.Bind(career);

        public void Open()
        {
            if (_panel == null) return;
            _drills?.Close();
            _manual?.Close();
            _history?.Close();
            _career?.Close();
            _alphaHelp?.Close();
            if (_historyButton != null)
                _historyButton.interactable = _history != null && _history.HasNotes;
            _panel.SetActive(true);
            _panel.transform.SetAsLastSibling();
        }

        public void Close()
        {
            _drills?.Close();
            _manual?.Close();
            _history?.Close();
            _career?.Close();
            _alphaHelp?.Close();
            if (_panel != null) _panel.SetActive(false);
        }

        public void CloseDrillsOnly() => _drills?.Close();
        public void CloseManualOnly() => _manual?.Close();
        public void CloseHistoryOnly() => _history?.Close();
        public void CloseCareerOnly() => _career?.Close();
        public void CloseAlphaHelpOnly() => _alphaHelp?.Close();

        private void OpenDrills()
        {
            _manual?.Close();
            _history?.Close();
            _career?.Close();
            _alphaHelp?.Close();
            _drills?.Open();
        }

        private void OpenManual()
        {
            _drills?.Close();
            _history?.Close();
            _career?.Close();
            _alphaHelp?.Close();
            _manual?.Open();
        }

        private void OpenHistory()
        {
            _drills?.Close();
            _manual?.Close();
            _career?.Close();
            _alphaHelp?.Close();
            _history?.Open();
        }

        /// <summary>Public for the pause CAREER button and <c>-demo-career</c> captures (#519).</summary>
        public void OpenCareer()
        {
            _drills?.Close();
            _manual?.Close();
            _history?.Close();
            _alphaHelp?.Close();
            _career?.Open();
        }

        private void OpenAlphaHelp()
        {
            _drills?.Close();
            _manual?.Close();
            _history?.Close();
            _career?.Close();
            _alphaHelp?.Open();
        }
    }
}
