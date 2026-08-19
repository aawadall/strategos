// AlphaHelpOverlay.cs
// Free-alpha how-to-play + honest known-limitations (fog / artillery DF / no ZoC / wrecks /
// firefight stall). Opened from MainMenuView HELP and pause ALPHA LIMITS (#469).
// Distinct from PLAY ContextHelpOverlay (#308) and the field manual (#124).

using TMPro;
using UnityEngine;
using UnityEngine.UI;

using Theme = Strategos.UI.UiTheme;
using static Strategos.UI.UiFactory;

namespace Strategos.UI
{
    public sealed class AlphaHelpOverlay : MonoBehaviour
    {
        public const string HowToTitle = "HOW TO PLAY";

        public const string HowToBody =
            "1. Start SQUAD TUTORIAL or SKIRMISH ONLY from the menu.\n" +
            "2. Left-click a friendly (blue) unit to select it.\n" +
            "3. Arm MOVE (button or M), then left-click empty ground — " +
            "or right-click ground to march.\n" +
            "4. Arm ENGAGE (button or E), then left-click an enemy — " +
            "or right-click a contact to fire.\n" +
            "5. Space pauses / resumes the clock. Esc opens pause " +
            "(Save / Load / return to menu).\n" +
            "\n" +
            "ALPHA LIMITS (not bugs)\n" +
            "• Little fog of war on small maps — detection ranges are long.\n" +
            "• Artillery fights as direct fire; true indirect fire is not built.\n" +
            "• No zone of control, facing, or unit collision — units pass through.\n" +
            "• Destroyed units stay on the map as wrecks (not commandable).\n" +
            "• Head-on equal firefights can stall under suppression — flank or dig in.\n" +
            "• Splash boot art is a procedural terrain sample (same pipeline as " +
            "EXPLORE/SCENARIO) — not a specific map, not hand-authored.\n" +
            "\n" +
            "Full list: docs/known-gaps.md · Pause → FIELD MANUAL for glossary terms.";

        private GameObject _panel;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        public void Build(RectTransform host)
        {
            var root = CreateRect("AlphaHelpOverlay", host);
            Stretch(root);
            root.SetAsLastSibling();
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0.12f, 0.10f, 0.08f, 0.5f);
            dim.raycastTarget = true;

            var card = CreateRect("Card", root);
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(560, 460);
            card.gameObject.AddComponent<Image>().color = Theme.CardBg;

            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(24, 24, 18, 18);
            v.spacing = 8;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            var title = CreateTmp("Title", card, HowToTitle, 18, FontStyles.Bold);
            title.alignment = TextAlignmentOptions.Center;
            title.color = Theme.Ink;
            title.characterSpacing = 4f;
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

            var note = CreateTmp("Note", card,
                "Playable sandbox · v" + Application.version,
                12, FontStyles.Normal);
            note.alignment = TextAlignmentOptions.Center;
            note.color = Theme.InkMuted;
            note.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;

            var body = CreateTmp("Body", card, HowToBody, 13, FontStyles.Normal);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.color = Theme.Ink;
            body.enableWordWrapping = true;
            var bodyLe = body.gameObject.AddComponent<LayoutElement>();
            bodyLe.flexibleHeight = 1f;
            bodyLe.preferredHeight = 320;

            AddButton(card, "CLOSE", Close);

            _panel = root.gameObject;
            _panel.SetActive(false);
        }

        public void Open()
        {
            if (_panel == null) return;
            _panel.SetActive(true);
            _panel.transform.SetAsLastSibling();
        }

        public void Close()
        {
            if (_panel != null) _panel.SetActive(false);
        }
    }
}
