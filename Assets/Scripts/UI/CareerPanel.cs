// CareerPanel.cs
// #519: read-only career summary — current rank/formation plus finished-chain history
// (pause nested, same layering as HistoricalNotePanel).

using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Strategos.Campaigns;
using Strategos.Units;

using Theme = Strategos.UI.UiTheme;
using static Strategos.UI.UiFactory;

namespace Strategos.UI
{
    public sealed class CareerPanel : MonoBehaviour
    {
        private GameObject _root;
        private TMP_Text _body;
        private CareerProfile _career;

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Build(RectTransform host)
        {
            var root = CreateRect("CareerPanel", host);
            Stretch(root);
            root.SetAsLastSibling();
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0.10f, 0.09f, 0.07f, 0.65f);

            var card = CreateRect("Card", root);
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(560, 420);
            card.gameObject.AddComponent<Image>().color = Theme.MapPaper;

            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(20, 20, 16, 16);
            v.spacing = 8;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            var title = CreateTmp("Title", card, "CAREER", 18, FontStyles.Bold);
            title.color = Theme.Ink;
            title.alignment = TextAlignmentOptions.Center;
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;

            var note = CreateTmp("Note", card,
                "Rank and formation carry between campaigns · #109",
                12, FontStyles.Normal);
            note.color = Theme.InkMuted;
            note.alignment = TextAlignmentOptions.Center;
            note.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

            var scrollHost = CreateRect("Scroll", card);
            scrollHost.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            scrollHost.gameObject.AddComponent<LayoutElement>().preferredHeight = 280;
            var scroll = scrollHost.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = CreateRect("Viewport", scrollHost);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            var content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(0, 0);
            var contentV = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentV.childControlWidth = true;
            contentV.childControlHeight = true;
            contentV.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            _body = CreateTmp("Body", content, "", 13, FontStyles.Normal);
            _body.color = Theme.Ink;
            _body.alignment = TextAlignmentOptions.TopLeft;
            _body.enableWordWrapping = true;
            _body.gameObject.AddComponent<LayoutElement>().preferredHeight = 400;

            AddButton(card, "CLOSE", Close);

            _root = root.gameObject;
            _root.SetActive(false);
        }

        public void Bind(CareerProfile career) => _career = career;

        public void Open()
        {
            if (_root == null) return;
            Refresh();
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }

        public void Close()
        {
            if (_root != null) _root.SetActive(false);
        }

        private void Refresh()
        {
            if (_body == null) return;
            if (_career == null)
            {
                _body.text = "(no career profile loaded)";
                return;
            }

            var sb = new StringBuilder();
            var step = RankAuthorityIO.Current.Find(_career.CareerRankId);
            string rankTitle = (step?.Title ?? _career.CareerRankId).ToUpperInvariant();

            sb.AppendLine($"RANK  {rankTitle}");
            sb.AppendLine(
                $"FORMATION  {(string.IsNullOrEmpty(_career.FormationDesignation) ? "—" : _career.FormationDesignation)}");
            sb.AppendLine(
                $"REPORTS TO  {(string.IsNullOrEmpty(_career.HigherFormation) ? "—" : _career.HigherFormation)}");
            sb.AppendLine();
            sb.AppendLine("CAMPAIGN HISTORY");

            if (_career.History == null || _career.History.Count == 0)
            {
                sb.AppendLine("(no finished campaigns yet)");
            }
            else
            {
                for (int i = 0; i < _career.History.Count; i++)
                {
                    var rec = _career.History[i];
                    var recStep = RankAuthorityIO.Current.Find(rec.CareerRankId);
                    string recRank = (recStep?.Title ?? rec.CareerRankId).ToUpperInvariant();
                    string chain = string.IsNullOrEmpty(rec.ChainName) ? "(unnamed)" : rec.ChainName;

                    sb.AppendLine(
                        $"{chain} — {rec.Outcome.ToString().ToUpperInvariant()} — {recRank}" +
                        (string.IsNullOrEmpty(rec.FormationDesignation)
                            ? string.Empty
                            : $" ({rec.FormationDesignation})"));
                }
            }

            _body.text = sb.ToString();
        }
    }
}
