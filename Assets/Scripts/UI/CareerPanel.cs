// CareerPanel.cs
// #519: read-only career summary — current rank/formation plus finished-chain history
// (pause nested, same layering as HistoricalNotePanel).

using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Strategos.Campaigns;
using Strategos.Medals;
using Strategos.Units;

using Theme = Strategos.UI.UiTheme;
using static Strategos.UI.UiFactory;

namespace Strategos.UI
{
    public sealed class CareerPanel : MonoBehaviour
    {
        private GameObject _root;
        private RectTransform _content;
        private TMP_Text _body;
        private LayoutElement _bodyLayout;
        private Transform _medalsHost;
        private BarMedalCatalog _catalog;
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
            card.sizeDelta = new Vector2(560, 480);
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
            scrollHost.gameObject.AddComponent<LayoutElement>().preferredHeight = 340;
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
            _content = content;

            _body = CreateTmp("Body", content, "", 13, FontStyles.Normal);
            _body.color = Theme.Ink;
            _body.alignment = TextAlignmentOptions.TopLeft;
            _body.enableWordWrapping = true;
            // Height tracks the actual history length (set in Refresh) — a fixed height
            // here left a gap before the medals section for every career shorter than the
            // max, pushing it below the fold.
            _bodyLayout = _body.gameObject.AddComponent<LayoutElement>();

            var medalsLabel = CreateTmp("MedalsLabel", content, "MEDALS", 12, FontStyles.Bold);
            medalsLabel.color = Theme.Accent;
            medalsLabel.characterSpacing = 4f;
            medalsLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

            var medalsHost = CreateRect("Medals", content);
            var medalsV = medalsHost.gameObject.AddComponent<VerticalLayoutGroup>();
            medalsV.spacing = 6;
            medalsV.childControlWidth = true;
            medalsV.childControlHeight = true;
            medalsV.childForceExpandWidth = true;
            medalsHost.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            _medalsHost = medalsHost;

            AddButton(card, "CLOSE", Close);

            _root = root.gameObject;
            _root.SetActive(false);
            _catalog = BarMedalIO.Load();
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
            if (_bodyLayout != null)
            {
                int lines = _body.text.Split('\n').Length;
                _bodyLayout.preferredHeight = Mathf.Max(60, lines * 17f);
            }

            RefreshMedals();

            if (_content != null) LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        /// <summary>Career rack (#467 W07): earned bars grouped Training → Campaign →
        /// Historical → Merit → Mode, read-only.</summary>
        private void RefreshMedals()
        {
            if (_medalsHost == null) return;
            for (int i = _medalsHost.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_medalsHost.GetChild(i).gameObject);

            _catalog ??= BarMedalIO.Load();
            var earned = _career?.EarnedMedals;
            if (earned == null || earned.Count == 0)
            {
                var none = CreateTmp("NoneMedals", _medalsHost, "(none earned yet)", 12,
                    FontStyles.Italic);
                none.color = Theme.InkMuted;
                none.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
                return;
            }

            foreach (BarMedalCategory category in Enum.GetValues(typeof(BarMedalCategory)))
            {
                var inCategory = new List<CareerEarnedMedal>();
                for (int i = 0; i < earned.Count; i++)
                {
                    var def = BarMedalIO.Find(_catalog, earned[i].MedalId);
                    if (def != null && def.Category == category) inCategory.Add(earned[i]);
                }
                if (inCategory.Count == 0) continue;

                var row = CreateRect($"Row_{category}", _medalsHost);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 84;
                var rowH = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                rowH.spacing = 8;
                rowH.childAlignment = TextAnchor.MiddleLeft;
                rowH.childControlWidth = false;
                rowH.childControlHeight = false;

                var catLabel = CreateTmp($"Cat_{category}", row,
                    category.ToString().ToUpperInvariant(), 10, FontStyles.Bold);
                catLabel.color = Theme.InkMuted;
                catLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 66;

                for (int i = 0; i < inCategory.Count; i++)
                    AddMedalChip(row, inCategory[i]);
            }
        }

        private void AddMedalChip(Transform parent, CareerEarnedMedal earned)
        {
            var def = BarMedalIO.Find(_catalog, earned.MedalId);
            if (def == null) return;

            var chip = CreateRect($"Medal_{earned.MedalId}", parent);
            chip.gameObject.AddComponent<LayoutElement>().preferredWidth = 110;
            chip.gameObject.AddComponent<LayoutElement>().preferredHeight = 76;

            var col = chip.gameObject.AddComponent<VerticalLayoutGroup>();
            col.spacing = 4;
            col.childAlignment = TextAnchor.UpperCenter;
            col.childControlWidth = true;
            col.childControlHeight = false;
            col.childForceExpandWidth = true;

            var icon = CreateRect("Icon", chip);
            icon.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
            var img = icon.gameObject.AddComponent<Image>();
            img.sprite = BarMedalBaker.For(def, earned.Count);
            img.preserveAspect = true;
            img.color = Color.white;

            string label = earned.Count > 1 ? $"{def.Title} ×{earned.Count}" : def.Title;
            var cap = CreateTmp("Cap", chip, label, 10, FontStyles.Normal);
            cap.color = Theme.InkMuted;
            cap.alignment = TextAlignmentOptions.Center;
            cap.enableWordWrapping = true;
            cap.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
        }
    }
}
