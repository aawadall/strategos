// TutorialFirstBeat.cs
// #310 / #441 / #449: tutorial checklist — select → MoveTo → Engage → queue → abort
// on the real command path. Non-blocking banner (map clicks still work). Driven by
// PlayView Select / IssueMoveTo / IssueEngage / AbortSelected.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

using Theme = Strategos.UI.UiTheme;
using static Strategos.UI.UiFactory;

namespace Strategos.UI
{
    public enum TutorialBeatPhase
    {
        Inactive = 0,
        SelectUnit = 1,
        IssueMove = 2,
        IssueEngage = 3,
        IssueQueue = 4,
        IssueAbort = 5,
        Complete = 6,
    }

    /// <summary>
    /// Checklist for the squad tutorial: select → MOVE → ENGAGE → Shift-queue → ABORT
    /// (#310 / #441 / #449).
    /// </summary>
    public sealed class TutorialFirstBeat : MonoBehaviour
    {
        private GameObject _root;
        private TMP_Text _title;
        private TMP_Text _body;

        public TutorialBeatPhase Phase { get; private set; } = TutorialBeatPhase.Inactive;

        public bool IsActive =>
            Phase == TutorialBeatPhase.SelectUnit ||
            Phase == TutorialBeatPhase.IssueMove ||
            Phase == TutorialBeatPhase.IssueEngage ||
            Phase == TutorialBeatPhase.IssueQueue ||
            Phase == TutorialBeatPhase.IssueAbort;

        public void Build(RectTransform host)
        {
            var root = CreateRect("TutorialFirstBeat", host);
            root.anchorMin = new Vector2(0.5f, 1f);
            root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(580, 108);
            root.anchoredPosition = new Vector2(0, -12);
            root.SetAsLastSibling();

            var face = root.gameObject.AddComponent<Image>();
            face.color = Theme.CardBg;
            face.raycastTarget = false;

            var v = root.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(16, 16, 10, 10);
            v.spacing = 4;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            _title = CreateTmp("Title", root, "TUTORIAL", 14, FontStyles.Bold);
            _title.alignment = TextAlignmentOptions.Center;
            _title.color = Theme.Ink;
            _title.raycastTarget = false;
            _title.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

            _body = CreateTmp("Body", root, "", 13, FontStyles.Normal);
            _body.alignment = TextAlignmentOptions.Center;
            _body.color = Theme.InkMuted;
            _body.enableWordWrapping = true;
            _body.raycastTarget = false;
            _body.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;

            _root = root.gameObject;
            _root.SetActive(false);
        }

        /// <summary>Starts the five-step checklist (tutorial scenario load).</summary>
        public void Begin()
        {
            Phase = TutorialBeatPhase.SelectUnit;
            Show("1 / 5  ·  SELECT",
                "Left-click your squad on the map (or the ORBAT). Real selection — same as any scenario.");
        }

        public void Reset()
        {
            Phase = TutorialBeatPhase.Inactive;
            if (_root != null) _root.SetActive(false);
        }

        /// <summary>
        /// Advances when the player selects a commandable friendly unit during SelectUnit.
        /// </summary>
        public void OnUnitSelected(bool playerCommanded)
        {
            if (Phase != TutorialBeatPhase.SelectUnit || !playerCommanded) return;
            Phase = TutorialBeatPhase.IssueMove;
            Show("2 / 5  ·  MOVE",
                "Arm MOVE (M or the MOVE button), then left-click a destination. Issues a real MoveTo order.");
        }

        /// <summary>
        /// Advances to ENGAGE when PlayView issues a non-queued MoveTo on the normal path.
        /// </summary>
        public void OnMoveIssued()
        {
            if (Phase != TutorialBeatPhase.IssueMove) return;
            Phase = TutorialBeatPhase.IssueEngage;
            Show("3 / 5  ·  ENGAGE",
                "Arm ENGAGE (E or the ENGAGE button), then left-click a red contact. Issues a real Engage order.");
        }

        /// <summary>Advances to QUEUE when PlayView issues Engage through the normal path (#441).</summary>
        public void OnEngageIssued()
        {
            if (Phase != TutorialBeatPhase.IssueEngage) return;
            Phase = TutorialBeatPhase.IssueQueue;
            Show("4 / 5  ·  QUEUE",
                "Hold Shift and issue another MOVE destination so it appends behind the live plan.");
        }

        /// <summary>Advances to ABORT when PlayView issues a Shift-queued MoveTo (#449).</summary>
        public void OnQueuedMoveIssued()
        {
            if (Phase != TutorialBeatPhase.IssueQueue) return;
            Phase = TutorialBeatPhase.IssueAbort;
            Show("5 / 5  ·  ABORT",
                "Press ABORT PLAN (controls rail) to clear the unit's queued orders. Real Abort command.");
        }

        /// <summary>Completes when PlayView issues Abort for the selected unit (#449).</summary>
        public void OnAbortIssued()
        {
            if (Phase != TutorialBeatPhase.IssueAbort) return;
            Phase = TutorialBeatPhase.Complete;
            Show("BEAT COMPLETE",
                "Select, MoveTo, Engage, Shift-queue, and Abort used the live command path.");
        }

        private void Show(string title, string body)
        {
            if (_title != null) _title.text = title;
            if (_body != null) _body.text = body;
            if (_root != null)
            {
                _root.SetActive(true);
                _root.transform.SetAsLastSibling();
            }
        }
    }
}
