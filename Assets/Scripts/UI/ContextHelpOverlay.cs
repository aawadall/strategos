// ContextHelpOverlay.cs
// #308: small in-PLAY card explaining one control. Owned by PlayView; Esc closes first.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using Theme = Strategos.UI.UiTheme;
using static Strategos.UI.UiFactory;

namespace Strategos.UI
{
    public sealed class ContextHelpOverlay : MonoBehaviour
    {
        private GameObject _panel;
        private TMP_Text _title;
        private TMP_Text _body;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        public void Build(RectTransform host)
        {
            var root = CreateRect("ContextHelpOverlay", host);
            Stretch(root);
            root.SetAsLastSibling();
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0.12f, 0.10f, 0.08f, 0.45f);
            dim.raycastTarget = true;

            var card = CreateRect("Card", root);
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(480, 280);
            card.gameObject.AddComponent<Image>().color = Theme.CardBg;

            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(24, 24, 20, 20);
            v.spacing = 10;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            _title = CreateTmp("Title", card, "HELP", 18, FontStyles.Bold);
            _title.alignment = TextAlignmentOptions.Center;
            _title.color = Theme.Ink;
            _title.characterSpacing = 4f;
            _title.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

            var note = CreateTmp("Note", card, "Context help · not the field manual (#124)",
                12, FontStyles.Normal);
            note.alignment = TextAlignmentOptions.Center;
            note.color = Theme.InkMuted;
            note.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

            _body = CreateTmp("Body", card, "", 14, FontStyles.Normal);
            _body.alignment = TextAlignmentOptions.TopLeft;
            _body.color = Theme.Ink;
            _body.enableWordWrapping = true;
            _body.gameObject.AddComponent<LayoutElement>().preferredHeight = 140;

            AddButton(card, "CLOSE", Close);

            _panel = root.gameObject;
            _panel.SetActive(false);
        }

        public void Open(string title, string body)
        {
            if (_panel == null) return;
            if (_title != null) _title.text = string.IsNullOrEmpty(title) ? "HELP" : title;
            if (_body != null) _body.text = body ?? string.Empty;
            _panel.SetActive(true);
            _panel.transform.SetAsLastSibling();
        }

        public void Close()
        {
            if (_panel != null) _panel.SetActive(false);
        }
    }
}
