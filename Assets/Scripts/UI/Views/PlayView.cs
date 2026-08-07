// PlayView.cs
// A scenario on its map, with both sides' units drawn where they stand — and the one place
// where a player selects, orders, aborts, watches the clock and reads what is being reported.
//
// The rail is a command post, and it is laid out as one: what is selected, what it has been
// told, what time it is, and what has come back up. The last of those is a *subscriber* to the
// situation topic and not a poll of world state — see OnReport. That is the acceptance rule of
// #15 and it is easiest to break here, because a view has every unit in hand and asking them
// directly would be one line shorter.
//
// 2D, NOT 3D, DELIBERATELY.
// The 3D drape in the scenario view stays a preview. Anchoring units in a perspective world
// needs billboarding, depth sorting and a picking ray, and buys nothing for "see a unit on a
// map". MapRasterizer.RenderPixels' own doc comment names unit symbols as the layer a caller
// composites in the same pixel space, and MapLabelPlacer.Reserve exists so a unit symbol can
// be protected from being labelled over — the 2D seam was cut for exactly this.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Strategos.Campaigns;
using Strategos.Commands;
using Strategos.ControlMeasures;
using Strategos.Directives;
using Strategos.Direction;
using Strategos.Doctrine;
using Strategos.Maps;
using Strategos.Modes;
using Strategos.Movement;
using Strategos.NatoSymbols;
using Strategos.Objectives;
using Strategos.Persistence;
using Strategos.Persistence.Files;
using Strategos.Identity;
using Strategos.Audio;
using Strategos.Scenarios;
using Strategos.Units;
using Strategos.Medals;

using Theme = Strategos.UI.UiTheme;
using static Strategos.UI.UiFactory;

namespace Strategos.UI.Views
{
    public sealed class PlayView : MonoBehaviour, IAppView
    {
        /// <summary>
        /// On-screen size of a unit symbol, in reference-resolution px.
        ///
        /// Fixed rather than zoom-scaled. Phase 2.2 wants proper LOD — detailed symbol close
        /// in, simplified icon at distance, a dot at theatre scale — and a naive linear scale
        /// now would have to be unpicked to get there.
        /// </summary>
        private const float SymbolSize = 56f;

        /// <summary>
        /// Bake size, fixed so it stays out of the factory's cache key. Larger than the
        /// on-screen size so symbols stay sharp if that grows.
        /// </summary>
        private const int BakeSize = 128;

        private AppSession _session;
        private Scenario _scenario;
        private MapData _map;

        private MapSheetCard _card;
        private RectTransform _unitLayer;
        private TMP_Text _headerLabel;
        private TMP_Text _statusLabel;
        private TMP_Dropdown _modeDrop;
        private Toggle _showLabels;

        private readonly List<Marker> _markers = new();
        private bool _suppress;

        /// <summary>
        /// Currently selected units.
        ///
        /// A set that happens to hold at most one, not a single UnitInstance field. Multi-
        /// and formation-select are out of scope here but should not be *precluded*:
        /// commanding at echelon is eventually the core mechanic, and the Command message
        /// already reserves a group addressee. The cost of getting this wrong is not the
        /// selection code, which is small — it is that every call site downstream assumes one
        /// unit, so widening it later means touching the whole ordering path.
        /// </summary>
        private readonly List<UnitId> _selection = new();

        /// <summary>
        /// The running simulation. It holds the same UnitInstance objects the markers do, so
        /// a unit moved by an executor is redrawn without anything having to be pushed.
        /// </summary>
        private Simulation _sim;

        /// <summary>
        /// Real seconds banked toward the next simulation step.
        ///
        /// The simulation advances in whole fixed ticks and the presentation runs at whatever
        /// frame rate it gets. Driving state from Time.deltaTime directly would make results
        /// depend on frame rate and machine, which is exactly what the divergence test exists
        /// to prevent.
        /// </summary>
        private float _tickAccumulator;

        private bool _running = true;
        private Toggle _runToggle;
        private TMP_Text _clockLabel;
        private TMP_Dropdown _speedDrop;
        private TMP_Dropdown _playModeDrop;
        private TMP_Dropdown _aiDifficultyDrop;
        private TMP_Dropdown _aiPersonalityDrop;
        private Button _switchSideButton;

        /// <summary>
        /// Real-time multiplier. 1 means a simulated second per real second.
        ///
        /// A PRESENTATION setting, not a simulation one. The simulation only ever advances in
        /// whole fixed ticks, so compression changes how quickly ticks are *asked for* and
        /// nothing about what they do: the same tick count produces the same result at any
        /// speed. That property is a direct dividend of the fixed-step decision, and it is why
        /// this control cannot break the divergence test.
        /// </summary>
        private float _timeScale = 1f;

        /// <summary>
        /// Offered rates. 6.4 km at a foot unit's 1.2 m/s is roughly an hour and a half of
        /// simulated time, so the top of the range has to be genuinely fast to be useful.
        /// </summary>
        private static readonly float[] TimeScales = { 1f, 2f, 5f, 15f, 60f, 300f };

        /// <summary>
        /// Most simulated time that may be banked toward catch-up, in ticks.
        ///
        /// Sized for the fastest offered rate at a poor frame rate: 300x at 15 fps needs 20
        /// steps a frame, so 32 leaves headroom without letting a long stall — a generation, a
        /// breakpoint — dump minutes of simulated time into one frame.
        /// </summary>
        private const float MaxBankedTicks = 32f;

        private OrderTrackLayer _orders;
        private RectTransform _selectionMark;
        private RectTransform _detailsCard;
        private TMP_Text _detailsTitle;
        private TMP_Text _detailsBody;

        /// <summary>
        /// The scenario's directive, as last delivered on <see cref="Simulation.Directives"/> —
        /// never read from <c>Scenario.Directive</c> directly, per this file's own header on
        /// subscribing rather than polling. Null both before delivery (rule 1: not visible on
        /// the tick it was published) and for a scenario that carries none.
        /// </summary>
        private Directive? _directive;
        private RectTransform _directiveSection;
        private TMP_Text _directiveTitle;
        private TMP_Text _directiveBody;
        private Button _directiveAckButton;
        private TMP_Text _directiveAckLabel;

        /// <summary>
        /// Whether the standing directive has been acknowledged — read off <see cref="OnReport"/>
        /// (a <see cref="Strategos.Reports.ReportKind.DirectiveAcknowledged"/> report citing it),
        /// never set from the click handler directly. The simulation is the truth; this is a
        /// cached reflection of what it reported, one tick behind the button press like every
        /// other observer of either bus (rule 1) — not an optimistic flip made at click time,
        /// which a future save/load or replay would have no way to reproduce.
        /// </summary>
        private bool _directiveAcknowledged;
        private int _directiveAcknowledgedTick;

        /// <summary>One unit's on-map presence: the symbol and its caption.</summary>
        private sealed class Marker
        {
            public UnitInstance Unit;
            public GameObject Go;
            public Image Icon;
            public TMP_Text Label;

            /// <summary>
            /// Strength band the current sprite was baked at, so the symbol is re-fetched when
            /// damage actually changes it and not once a frame. −1 means "never baked".
            /// </summary>
            public int BakedStrength = -1;
        }

        public string Title => "PLAY";
        public string Key => "play";
        public AppSession Session { set => _session = value; }
        public AppShell Shell { get; set; }

        private PauseOverlay _pause;
        private ContextHelpOverlay _contextHelp;
        private TutorialFirstBeat _tutorialBeat;
        private PostBattlePanel _postBattle;
        private bool _runningBeforePause = true;

        // ─── IAppView ─────────────────────────────────────────────────────────

        public void Build(RectTransform host)
        {
            BuildUi(host);
            PopulateOptions();
            Canvas.ForceUpdateCanvases();
            // Cold start still loads skirmish so -view play / capture keep working (#371
            // front door is the usual entry; menu starts call LoadScenarioPublic).
            LoadScenario(ScenarioSamples.SkirmishName);
        }

        public void OnShown()
        {
            _card?.UpdateCrop();
            LayOutMarkers();
        }

        public void OnHidden()
        {
            _tutorialBeat?.Reset();
            _contextHelp?.Close();
            _pause?.Close();
            HideDropdownsIn(transform);
        }

        private void OnDestroy() => _card?.Dispose();

        private void Update()
        {
            if (HandlePauseKeys())
            {
                // Pause owns Escape this frame — do not also clear the palette / arm verbs.
            }
            else if (Input.GetKeyDown(KeyCode.Space) && _runToggle != null &&
                     (_pause == null || !_pause.IsOpen))
            {
                _runToggle.isOn = !_runToggle.isOn;
                RefreshClock();
            }
            else if (CommandPalette.TryReadArmingKey(out var arm))
            {
                ArmVerb(arm);
            }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                CommitWaypoints();
            }

            if (_pause == null || !_pause.IsOpen)
                AdvanceSimulation();
            _card?.PollResize();
            LayOutMarkers();
        }

        /// <summary>
        /// Esc layering (#371 / #129 / #308 / #206 / #462 / #469 / #467): post-battle →
        /// historical note → alpha limits → field manual → drills → context help →
        /// pause resume → armed verb clear → open pause.
        /// </summary>
        private bool HandlePauseKeys()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return false;

            if (_postBattle != null && _postBattle.IsOpen)
            {
                _postBattle.Close();
                return true;
            }

            if (_pause != null && _pause.HistoryOpen)
            {
                _pause.CloseHistoryOnly();
                return true;
            }

            if (_pause != null && _pause.AlphaHelpOpen)
            {
                _pause.CloseAlphaHelpOnly();
                return true;
            }

            if (_pause != null && _pause.ManualOpen)
            {
                _pause.CloseManualOnly();
                return true;
            }

            if (_pause != null && _pause.DrillsOpen)
            {
                _pause.CloseDrillsOnly();
                return true;
            }

            if (_contextHelp != null && _contextHelp.IsOpen)
            {
                _contextHelp.Close();
                return true;
            }

            if (_pause != null && _pause.IsOpen)
            {
                ClosePause(resumeClock: true);
                return true;
            }

            if (_armedVerb != PaletteVerb.None)
            {
                ArmVerb(PaletteVerb.None);
                return true;
            }

            OpenPause();
            return true;
        }

        private void OpenPause()
        {
            if (_pause == null) return;
            _runningBeforePause = _running;
            if (_runToggle != null) _runToggle.isOn = false;
            else _running = false;
            RefreshClock();
            _pause.Open();
        }

        private void ClosePause(bool resumeClock)
        {
            _pause?.Close();
            if (resumeClock && _runToggle != null)
            {
                _runToggle.isOn = _runningBeforePause;
                RefreshClock();
            }
        }

        // ─── Menu / shell entry points (#371) ─────────────────────────────────

        public void LoadScenarioPublic(string name) => LoadScenario(name);
        public void StartValleyCampaignPublic() => StartValleyCampaign();
        public void StartHighlandCampaignPublic() => StartHighlandCampaign();
        public void StartClimbCampaignPublic() => StartClimbCampaign();
        public void QuickSavePublic() => QuickSave();
        public void QuickLoadPublic() => QuickLoad();

        // ─── UI ───────────────────────────────────────────────────────────────

        private void BuildUi(RectTransform host)
        {
            var root = CreateRect("Root", host);
            Stretch(root);
            root.gameObject.AddComponent<Image>().color = Theme.StageBg;
            var rootH = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            rootH.childControlWidth = true;
            rootH.childControlHeight = true;
            rootH.childForceExpandWidth = false;
            rootH.childForceExpandHeight = true;

            BuildStage(root);
            BuildRail(root);

            _pause = host.gameObject.AddComponent<PauseOverlay>();
            _pause.Build(host,
                onResume: () => ClosePause(resumeClock: true),
                onSave: QuickSave,
                onLoad: QuickLoad,
                onExitMenu: () =>
                {
                    ClosePause(resumeClock: false);
                    Shell?.GoToMainMenu();
                });

            _contextHelp = host.gameObject.AddComponent<ContextHelpOverlay>();
            _contextHelp.Build(host);

            _tutorialBeat = host.gameObject.AddComponent<TutorialFirstBeat>();
            _tutorialBeat.Build(host);

            _postBattle = host.gameObject.AddComponent<PostBattlePanel>();
            _postBattle.Build(host);
        }

        private void BuildStage(Transform root)
        {
            var stage = CreateRect("Stage", root);
            var le = stage.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minWidth = 480f;

            var v = stage.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(20, 20, 16, 16);
            v.spacing = 8;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            _headerLabel = CreateTmp("Header", stage, "SCENARIO", 15, FontStyles.Bold);
            _headerLabel.color = Theme.Ink;
            _headerLabel.characterSpacing = 6f;
            _headerLabel.GetComponent<LayoutElement>().preferredHeight = 22;

            _statusLabel = CreateTmp("Status", stage, string.Empty, 11, FontStyles.Normal);
            _statusLabel.color = Theme.InkMuted;
            _statusLabel.characterSpacing = 1f;
            _statusLabel.GetComponent<LayoutElement>().preferredHeight = 18;

            _clockLabel = CreateTmp("Clock", stage, "T+0000", 11, FontStyles.Bold);
            _clockLabel.color = Theme.Accent;
            _clockLabel.characterSpacing = 2f;
            _clockLabel.GetComponent<LayoutElement>().preferredHeight = 16;

            BuildForceBars(stage);

            var holder = CreateRect("CardHolder", stage);
            var hle = holder.gameObject.AddComponent<LayoutElement>();
            hle.flexibleHeight = 1f;
            hle.minHeight = 320f;

            _card = new MapSheetCard(holder, preferredHeight: 0, out var stack);

            // Symbols overlay the sheet, inside the card's own stack so they move with it.
            _unitLayer = CreateRect("Units", stack);
            Stretch(_unitLayer);

            // Masked, because a square map in a wide card is cropped vertically and units
            // near the map's north or south edge then fall outside it. Without this they are
            // still drawn — floating over the tab bar and the rail, which looks like a
            // layout failure rather than a unit being off-screen.
            _unitLayer.gameObject.AddComponent<RectMask2D>();

            // Routes go under the symbols so an arrowhead never covers the unit it belongs
            // to, but inside the same masked layer so they are clipped with it.
            var trackRoot = CreateRect("Orders", _unitLayer);
            Stretch(trackRoot);
            trackRoot.SetAsFirstSibling();
            _orders = new OrderTrackLayer(trackRoot);

            // Selection brackets live in the unit layer so they are clipped with it, and
            // above the symbols so they read as chrome rather than part of a symbol.
            _selectionMark = CreateRect("Selection", _unitLayer);
            _selectionMark.anchorMin = _selectionMark.anchorMax = new Vector2(0.5f, 0.5f);
            _selectionMark.pivot = new Vector2(0.5f, 0.5f);
            _selectionMark.sizeDelta = new Vector2(SymbolSize * 1.5f, SymbolSize * 1.5f);
            var markImg = _selectionMark.gameObject.AddComponent<Image>();
            markImg.sprite = UiSprites.SelectionBrackets;
            markImg.color = Theme.Accent;
            markImg.raycastTarget = false;
            _selectionMark.gameObject.SetActive(false);

            // Pointer handling goes on the sheet itself, as MapExplorerView does, so a later
            // scroll-to-zoom cannot fight the rail's ScrollRect for the wheel.
            _card.Image.raycastTarget = true;
            var region = _card.Rect.gameObject.AddComponent<PointerRegion>();
            region.Clicked = OnMapClicked;
            region.Dragged = OnMapDragged;
            region.Scrolled = OnMapScrolled;

            // The marginalia strip is built by the card but must stay above the symbols.
            var strip = stack.Find("Marginalia");
            if (strip != null) strip.SetAsLastSibling();
        }

        private void BuildRail(Transform root)
        {
            var panel = CreateRect("Rail", root);
            var le = panel.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 440;
            le.minWidth = 400;
            le.flexibleWidth = 0;
            panel.gameObject.AddComponent<Image>().color = Theme.RailBg;

            var edge = CreateRect("Edge", panel);
            edge.anchorMin = new Vector2(0, 0);
            edge.anchorMax = new Vector2(0, 1);
            edge.pivot = new Vector2(0, 0.5f);
            edge.sizeDelta = new Vector2(2, 0);
            edge.gameObject.AddComponent<Image>().color = Theme.CardLine;

            var chrome = CreateRect("Chrome", panel);
            chrome.anchorMin = new Vector2(0, 1);
            chrome.anchorMax = new Vector2(1, 1);
            chrome.pivot = new Vector2(0.5f, 1);
            chrome.sizeDelta = new Vector2(0, 38);
            chrome.gameObject.AddComponent<Image>().color = Theme.Accent;
            var cl = CreateTmp("L", chrome, "ORDER OF BATTLE", 14, FontStyles.Bold, withLayout: false);
            Stretch(cl.rectTransform);
            cl.alignment = TextAlignmentOptions.Center;
            cl.color = Theme.AccentText;
            cl.characterSpacing = 6f;

            var content = UiScroll.CreateColumn("Scroll", panel, Theme.RailBg, out var scroll);
            var srt = (RectTransform)scroll.transform;
            srt.offsetMin = new Vector2(2, 0);
            srt.offsetMax = new Vector2(0, -38);

            // Standing state, not tied to selection — see BuildDirectiveCard. First in the
            // rail, immediately after the ORDER OF BATTLE chrome, so it reads as something the
            // whole command is under rather than a property of whichever unit is picked.
            BuildDirectiveCard(content);

            BuildCampaignCard(content);

            AddSection(content, "SELECTED UNIT");
            BuildDetailsCard(content);

            AddSection(content, "ORDERS");
            var hint = CreateTmp("Hint", content,
                "Left-click selects, or issues the armed verb when one is armed." +
                "\nRight-click still orders a move, or fire if it lands on an enemy." +
                "\nHold Shift to queue behind the current plan." +
                "\nCANCEL drops that order and every one queued behind it." +
                "\nPalette: M / E / W arm MOVE / ENGAGE / WAYPOINTS; Esc clears." +
                "\nWAYPOINTS: click places, drag moves, click handle removes; Enter commits.",
                10, FontStyles.Italic);
            hint.color = Theme.InkMuted;
            hint.GetComponent<LayoutElement>().preferredHeight = 72;

            BuildVerbPalette(content);
            BuildPlanCard(content);

            // Side by side, because they are the same kind of decision taken about the same
            // plan. Stacked, HOLD reads as belonging to the clock controls below it.
            var controls = AddButtonRow(content);
            AddButton(controls, "ABORT PLAN", AbortSelected);
            AddButton(controls, "HOLD", HoldSelected);
            AddButton(controls, "SCREEN", ScreenSelected);
            AddButton(controls, "GUARD", GuardSelected);
            AddButton(controls, "COVER", CoverSelected);
            AddButton(controls, "WITHDRAW", WithdrawSelected);
            AddButton(controls, "DELAY", DelaySelected);
            AddButton(controls, "ATTACK", AttackSelected);
            AddButton(controls, "RECON", ReconSelected);
            AddButton(controls, "EXPLOIT", ExploitSelected);
            AddButton(controls, "PURSUE", PursueSelected);

            // Calling a drill by code is what the whole doctrine library is for. A dropdown
            // is the discovery path; #53's palette adds typing the code, which is the
            // accelerator a practised player uses.
            _drillDrop = AddDropdown(content, "DRILL", null);
            AddButton(content, "EXECUTE DRILL", ExecuteDrillOnSelected);

            _roeDrop = AddDropdown(content, "RULES OF ENGAGEMENT", OnRoeChanged);
            _runToggle = AddToggle(content, "CLOCK RUNNING  (SPACE)", true,
                () => _running = _runToggle.isOn);
            _speedDrop = AddDropdown(content, "TIME COMPRESSION", OnSpeedChanged);

            AddSection(content, "SITUATION");
            BuildFeedCard(content);

            AddSection(content, "PRESENTATION");
            _modeDrop = AddDropdown(content, "RENDER MODE", RefreshSheet);
            _showLabels = AddToggle(content, "UNIT LABELS", true, LayOutMarkers);

            _orbatRoot = CreateRect("Orbat", content);
            var ov = _orbatRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            ov.spacing = 2;
            ov.childControlWidth = true;
            ov.childControlHeight = true;
            ov.childForceExpandWidth = true;
            ov.childForceExpandHeight = false;
            _orbatRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
        }

        private RectTransform _orbatRoot;
        private TMP_Dropdown _roeDrop;
        private TMP_Dropdown _drillDrop;

        /// <summary>Armed palette verb, or <see cref="PaletteVerb.None"/> for select-only (#127).</summary>
        private PaletteVerb _armedVerb = PaletteVerb.None;

        private Button _selectVerbButton;
        private Button _confirmWaypointsButton;
        private readonly List<(PaletteVerb Id, Button Button)> _verbButtons = new();
        private TMP_Text _armedLabel;

        /// <summary>Draft cells for an armed WAYPOINTS session (#54). Issued only on commit.</summary>
        private readonly List<Vector2> _waypointDraft = new();
        private int _dragHandle = -1;
        private MovementGrid _previewGrid;
        private string _previewCapsId;

        /// <summary>
        /// Table-driven arming chrome. Iterates <see cref="CommandPalette.Verbs"/> — adding a
        /// verb is a table row, not a new branch here. Right-click stays its own shortcut (#53).
        /// </summary>
        private void BuildVerbPalette(Transform parent)
        {
            AddSection(parent, "COMMAND");

            // Two rows so WAYPOINTS fits without crushing MOVE / ENGAGE labels.
            var row1 = AddButtonRow(parent);
            _selectVerbButton = AddButton(row1, "SELECT", () => ArmVerb(PaletteVerb.None));

            _verbButtons.Clear();
            RectTransform row = row1;
            for (int i = 0; i < CommandPalette.Verbs.Length; i++)
            {
                if (i == 2) row = AddButtonRow(parent);
                var def = CommandPalette.Verbs[i];
                var id = def.Id;
                var btn = AddButton(row, def.Label, () => ArmVerb(id));
                _verbButtons.Add((id, btn));
            }

            _armedLabel = CreateTmp("Armed", parent, "", 11, FontStyles.Bold);
            _armedLabel.color = Theme.Ink;
            _armedLabel.GetComponent<LayoutElement>().preferredHeight = 22;

            var confirmRow = AddButtonRow(parent);
            _confirmWaypointsButton = AddButton(confirmRow, "CONFIRM ROUTE", CommitWaypoints);
            AddButton(confirmRow, "HELP", OpenContextHelp);

            RefreshVerbChrome();
        }

        private void OpenContextHelp()
        {
            if (_contextHelp == null) return;
            // Prefer authored help for the armed verb; fall back to MOVE then ENGAGE.
            var verb = _armedVerb == PaletteVerb.None ? PaletteVerb.MoveTo : _armedVerb;
            if (!ContextHelp.TryGet(verb, out var title, out var body))
            {
                title = "HELP";
                body = "Context help for this control is not authored yet. " +
                       "Arm MOVE (M), ENGAGE (E), WAYPOINTS (W), or DIG IN (D) and open HELP again. " +
                       "Glossary / field manual remains #124.";
            }
            _contextHelp.Open(title, body);
        }

        /// <summary>Arms a palette verb (or clears to select-only). Clears any open draft.</summary>
        private void ArmVerb(PaletteVerb verb)
        {
            if (_armedVerb == verb) return;
            ClearWaypointDraft();
            _armedVerb = verb;
            RefreshVerbChrome();
        }

        private void RefreshVerbChrome()
        {
            if (_selectVerbButton != null)
                PaintVerbButton(_selectVerbButton, _armedVerb == PaletteVerb.None);

            for (int i = 0; i < _verbButtons.Count; i++)
            {
                var (id, btn) = _verbButtons[i];
                PaintVerbButton(btn, id == _armedVerb);
            }

            if (_confirmWaypointsButton != null)
            {
                bool drafting = _armedVerb == PaletteVerb.Waypoints && _waypointDraft.Count > 0;
                _confirmWaypointsButton.interactable = drafting;
                PaintVerbButton(_confirmWaypointsButton, drafting);
            }

            if (_armedLabel == null) return;

            if (_armedVerb == PaletteVerb.None ||
                !CommandPalette.TryGet(_armedVerb, out var def))
            {
                _armedLabel.text = "ARMED  ·  SELECT  ·  left-click picks a unit";
                return;
            }

            if (def.Id == PaletteVerb.Waypoints)
            {
                _armedLabel.text = $"ARMED  ·  {def.Label}  ({def.ShortcutLabel})" +
                    $"  ·  {_waypointDraft.Count} point(s)  ·  Enter commits";
                return;
            }

            if (def.Id == PaletteVerb.DigIn)
            {
                _armedLabel.text = $"ARMED  ·  {def.Label}" +
                    (string.IsNullOrEmpty(def.ShortcutLabel)
                        ? string.Empty
                        : $"  ({def.ShortcutLabel})") +
                    "  ·  left-click digs in (Hold/Defend)";
                return;
            }

            _armedLabel.text = $"ARMED  ·  {def.Label}" +
                (string.IsNullOrEmpty(def.ShortcutLabel)
                    ? string.Empty
                    : $"  ({def.ShortcutLabel})") +
                "  ·  left-click issues  ·  right-click still shortcuts";
        }

        private static void PaintVerbButton(Button btn, bool armed)
        {
            if (btn == null) return;
            var face = armed ? Theme.Accent : Theme.SectionBg;
            btn.colors = AccentButtonColors(face);
            if (btn.targetGraphic != null)
                btn.targetGraphic.color = Color.white;
            var label = btn.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.color = armed ? Theme.AccentText : Theme.Ink;
        }

        /// <summary>Rail order matches <see cref="RulesOfEngagement"/>'s own values.</summary>
        private static readonly string[] RoeLabels =
        {
            "Hold fire  ·  never initiate",
            "Return fire  ·  answer only",
            "Fire at will  ·  engage on sight",
        };

        /// <summary>
        /// Applies the rail's setting to the selected unit.
        ///
        /// Per unit rather than per side, because the interesting orders are the asymmetric
        /// ones — the recon screen on Hold Fire so it observes without being drawn into a
        /// fight, while everything behind it is free to shoot.
        /// </summary>
        private void OnRoeChanged()
        {
            if (_suppress || _roeDrop == null || _selection.Count == 0) return;

            var unit = _scenario?.FindUnit(_selection[0]);
            if (unit == null) return;

            unit.Roe = (RulesOfEngagement)Mathf.Clamp(_roeDrop.value, 0, RoeLabels.Length - 1);
            RefreshSelection();
        }

        private void PopulateOptions()
        {
            _suppress = true;
            SetDrop(_modeDrop, DisplayNames.RenderModeLabels(), 1);   // Topographic

            var speeds = new string[TimeScales.Length];
            speeds[0] = "x1   (real time)";
            for (int i = 1; i < TimeScales.Length; i++) speeds[i] = $"x{TimeScales[i]:0}";
            SetDrop(_speedDrop, speeds, 0);
            SetDrop(_roeDrop, RoeLabels, (int)RulesOfEngagement.ReturnFire);

            // Drills are selection-sensitive (T/P/U annotation); RefreshSelection rebuilds them.
            _drillOptionsKey = null;
            RefreshDrillOptions();
            _suppress = false;
        }

        /// <summary>
        /// What the drill dropdown was last built for. Same role as <see cref="_planKey"/>:
        /// <see cref="RefreshSelection"/> runs every tick while a unit is selected, and
        /// rebuilding a TMP_Dropdown that often closes the list under the pointer.
        /// </summary>
        private string _drillOptionsKey;

        /// <summary>
        /// Annotates each drill with the selected unit's T/P/U readiness, and rebuilds only
        /// when the selection (or its effectiveness) changes.
        /// </summary>
        private void RefreshDrillOptions()
        {
            if (_drillDrop == null) return;

            UnitInstance unit = null;
            if (_selection.Count > 0 && _scenario != null)
                unit = _scenario.FindUnit(_selection[0]);

            // Effectiveness drives Assess; round so a fractional drift every tick does not
            // thrash the options list, but a real drop in EFF still updates the letter.
            string key = unit == null
                ? "none"
                : $"{unit.Id}|{unit.IsDestroyed}|{unit.Effectiveness:0.00}|{TtpReadiness.EchelonOf(unit)}";
            if (key == _drillOptionsKey) return;
            _drillOptionsKey = key;

            var drills = TtpLibrary.All;
            string keep = null;
            if (drills.Count > 0)
                keep = drills[Mathf.Clamp(_drillDrop.value, 0, drills.Count - 1)].Code;

            var labels = new string[drills.Count];
            for (int i = 0; i < drills.Count; i++)
                labels[i] = TtpReadiness.PickerLabel(drills[i], unit);

            int index = 0;
            if (keep != null)
            {
                for (int i = 0; i < drills.Count; i++)
                {
                    if (drills[i].Code == keep) { index = i; break; }
                }
            }

            bool was = _suppress;
            _suppress = true;
            SetDrop(_drillDrop, labels, index);
            _suppress = was;
        }

        // ─── Scenario ─────────────────────────────────────────────────────────

        /// <summary>Shipped valley chain rest between ops — Lost gets less (#75 pattern).</summary>
        private const float CampaignRestHoursWon = 6f;
        private const float CampaignRestHoursLost = 2f;

        private TMP_Text _campaignLabel;
        private Button _continueCampaignButton;
        private IGameStore _store;
        private IPlayerIdentity _identity;

        private const string QuickSaveId = "quicksave";

        private IGameStore Store =>
            _store ??= new FileGameStore(FileGameStore.DefaultDirectory);

        private IPlayerIdentity Identity =>
            _identity ??= LocalAnonymousIdentity.Shared;

        /// <summary>
        /// CAMPAIGN rail (#139 / #140) and mode-select (#287 / #295): pick how to play, then
        /// start a chain or scenario. Not a new top-level tab.
        /// </summary>
        private void BuildCampaignCard(Transform parent)
        {
            AddSection(parent, "CAMPAIGN");

            _campaignLabel = CreateTmp("CampaignStatus", parent,
                "Single scenario  ·  START loads the valley chain", 11, FontStyles.Normal);
            _campaignLabel.color = Theme.InkMuted;
            _campaignLabel.GetComponent<LayoutElement>().preferredHeight = 36;

            _playModeDrop = AddDropdown(parent, "PLAY MODE", OnPlayModeChanged);
            SetDrop(_playModeDrop, new[] { "SOLO", "HOTSEAT", "SPECTATOR", "REPLAY" }, 0);

            _aiDifficultyDrop = AddDropdown(parent, "AI DIFFICULTY", OnAiDifficultyChanged);
            SetDrop(_aiDifficultyDrop, new[] { "EASY", "NORMAL", "HARD" }, 1);

            _aiPersonalityDrop = AddDropdown(parent, "AI PERSONALITY", OnAiPersonalityChanged);
            SetDrop(_aiPersonalityDrop, new[] { "BALANCED", "AGGRESSIVE", "DEFENSIVE" }, 0);

            var row = AddButtonRow(parent);
            AddButton(row, "START VALLEY", StartValleyCampaign);
            AddButton(row, "START HIGHLAND", StartHighlandCampaign);
            AddButton(row, "START CLIMB", StartClimbCampaign);
            _continueCampaignButton = AddButton(row, "CONTINUE", AdvanceCampaign);
            _continueCampaignButton.interactable = false;

            var row2 = AddButtonRow(parent);
            AddButton(row2, "SKIRMISH ONLY", () => LoadScenario(ScenarioSamples.SkirmishName));
            AddButton(row2, "PUSH NORTH", () => LoadScenario(ScenarioSamples.PushNorthName));
            AddButton(row2, "TUTORIAL", () => LoadScenario(ScenarioSamples.TutorialName));

            var row3 = AddButtonRow(parent);
            AddButton(row3, "SAVE", QuickSave);
            AddButton(row3, "LOAD", QuickLoad);
            AddButton(row3, "REPLAY SAVE", ReplayQuickSave);
            _switchSideButton = AddButton(row3, "SWITCH SIDE", SwitchHotseatSide);
            _switchSideButton.interactable = false;
        }

        private void OnPlayModeChanged()
        {
            if (_suppress || _session == null || _playModeDrop == null) return;
            _session.PlayMode = (ModeKind)Mathf.Clamp(_playModeDrop.value, 0, 3);
            if (_switchSideButton != null)
                _switchSideButton.interactable = _session.PlayMode == ModeKind.Hotseat;
            RefreshCampaignChrome();
            Debug.Log($"[PlayView] play mode {_session.PlayMode}");
        }

        private void OnAiDifficultyChanged()
        {
            if (_suppress || _session == null || _aiDifficultyDrop == null) return;
            _session.AiDifficulty = (AiDifficultyLevel)Mathf.Clamp(_aiDifficultyDrop.value, 0, 2);
            RefreshCampaignChrome();
            Debug.Log($"[PlayView] AI difficulty {_session.AiDifficulty}");
        }

        private void OnAiPersonalityChanged()
        {
            if (_suppress || _session == null || _aiPersonalityDrop == null) return;
            _session.AiPersonality = (AiPersonality)Mathf.Clamp(_aiPersonalityDrop.value, 0, 2);
            RefreshCampaignChrome();
            Debug.Log($"[PlayView] AI personality {_session.AiPersonality}");
        }

        private DifficultyParams CurrentDirectorParams() =>
            _session != null
                ? _session.ResolvedDirectorParams()
                : DifficultyParams.Normal();

        private ModeKind CurrentPlayMode() =>
            _session?.PlayMode ?? ModeKind.Solo;

        /// <summary>Side used for Issue chrome and GCM fog (#297).</summary>
        private SideId ViewerSide()
        {
            if (CurrentPlayMode() == ModeKind.Hotseat &&
                _session != null && _session.HotseatSide.IsValid)
                return _session.HotseatSide;
            return _scenario != null ? _scenario.PlayerSide : SideId.None;
        }

        private void SwitchHotseatSide()
        {
            if (_session == null || _scenario == null || CurrentPlayMode() != ModeKind.Hotseat)
                return;
            if (_scenario.Sides == null || _scenario.Sides.Count < 2) return;

            int i = 0;
            for (; i < _scenario.Sides.Count; i++)
                if (_scenario.Sides[i].Id == _session.HotseatSide) break;
            i = (i + 1) % _scenario.Sides.Count;
            _session.HotseatSide = _scenario.Sides[i].Id;
            ClearSelection();
            PublishCommandContext();
            RefreshSheet();
            RefreshCampaignChrome();
            _statusLabel.text = $"HOTSEAT  ·  {_scenario.FindSide(_session.HotseatSide)?.Name ?? _session.HotseatSide.ToString()}";
            Debug.Log($"[PlayView] hotseat side {_session.HotseatSide}");
        }

        private void LoadScenario(string name)
        {
            _session?.ClearCampaign();
            RefreshCampaignChrome();

            var scenario = ScenarioIO.Load(name);
            if (scenario == null)
            {
                Debug.LogError($"[PlayView] no scenario '{name}' in Resources/{ScenarioIO.ResourceFolder}");
                _statusLabel.text = $"SCENARIO '{name.ToUpperInvariant()}' NOT FOUND";
                return;
            }

            if (_session != null &&
                !RankGate.Authorize(_session.CareerRankId, scenario, out var gateProblem))
            {
                Debug.LogWarning($"[PlayView] {gateProblem}");
                _statusLabel.text = gateProblem.ToUpperInvariant();
                return;
            }

            var map = scenario.GenerateMap();
            var problems = scenario.Validate(UnitCatalogue.Default(), map);
            foreach (var p in problems) Debug.LogWarning($"[PlayView] {scenario.Name}: {p}");

            BindSimulation(new Simulation(scenario, map, UnitCatalogue.Default()), problems);
        }

        /// <summary>Starts the shipped three-op valley campaign (#139).</summary>
        private void StartValleyCampaign() =>
            StartNamedCampaign(CampaignSamples.ValleyName);

        /// <summary>
        /// Starts the shipped regiment-seat highland campaign (#109 / #213) — expects career
        /// rank that can command Regiment (after a valley win promotes battalion → regiment).
        /// </summary>
        private void StartHighlandCampaign() =>
            StartNamedCampaign(CampaignSamples.HighlandName);

        /// <summary>
        /// Starts the shipped Squad → Company → Battalion climb (#403 / #407).
        /// Default battalion career may command the opening Squad seat via RankGate.
        /// </summary>
        private void StartClimbCampaign() =>
            StartNamedCampaign(CampaignSamples.ClimbName);

        /// <summary>Shared start path for authored campaign chains.</summary>
        private void StartNamedCampaign(string campaignName)
        {
            var chain = CampaignChainIO.Load(campaignName);
            if (chain == null)
            {
                Debug.LogError(
                    $"[PlayView] no campaign '{campaignName}' in " +
                    $"Resources/{CampaignChainIO.ResourceFolder}");
                _statusLabel.text = "CAMPAIGN NOT FOUND";
                return;
            }

            var problems = chain.Validate(UnitCatalogue.Default());
            if (problems.Count > 0)
            {
                foreach (var p in problems) Debug.LogWarning($"[PlayView] campaign: {p}");
                _statusLabel.text = $"CAMPAIGN INVALID  ·  {problems.Count} PROBLEM(S)";
                return;
            }

            var first = ScenarioIO.Load(chain.Operations[0].ScenarioName);
            if (first == null)
            {
                _statusLabel.text = "CAMPAIGN FIRST SCENARIO MISSING";
                return;
            }

            if (_session != null &&
                !RankGate.Authorize(_session.CareerRankId, first, out var gateProblem))
            {
                Debug.LogWarning($"[PlayView] {gateProblem}");
                _statusLabel.text = gateProblem.ToUpperInvariant();
                return;
            }

            _session?.BeginCampaign(chain, 0);
            var sim = CampaignChainDriver.StartNext(chain, 0, UnitCatalogue.Default());
            BindSimulation(sim);
            RefreshCampaignChrome();
            Debug.Log($"[PlayView] campaign '{chain.Name}' operation 0 started");
        }

        /// <summary>
        /// After an operation decides: carry over into the next entry and bind that sim.
        /// </summary>
        private void AdvanceCampaign()
        {
            if (_session == null || !_session.HasNextOperation || _sim == null) return;
            if (_sim.Victory == null || !_sim.Victory.Outcome.Decided)
            {
                _statusLabel.text = "OPERATION NOT DECIDED";
                return;
            }

            var chain = _session.ActiveChain;
            int i = _session.ActiveOperationIndex;
            var finished = chain.Operations[i];
            var next = chain.Operations[i + 1];
            float rest = RestHoursFor(_sim);

            try
            {
                CampaignCarryOver.CarryOver(_sim, finished, next, rest);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PlayView] carry-over failed: {ex.Message}");
                _statusLabel.text = "CARRY-OVER FAILED";
                return;
            }

            _session.AdvanceCampaignOperation();
            var sim = CampaignChainDriver.StartNext(chain, _session.ActiveOperationIndex,
                UnitCatalogue.Default());
            BindSimulation(sim);
            RefreshCampaignChrome();

            Debug.Log(
                $"[PlayView] campaign advanced to operation {_session.ActiveOperationIndex} " +
                $"('{next.Name}') after {rest}h rest");
        }

        private static float RestHoursFor(Simulation sim)
        {
            var outcome = sim.Victory.Outcome;
            if (outcome.IsDraw) return CampaignRestHoursWon;
            if (outcome.Winner == sim.Scenario.PlayerSide) return CampaignRestHoursWon;
            return CampaignRestHoursLost;
        }

        /// <summary>
        /// Wires a ready <see cref="Simulation"/> into PLAY — shared by single-scenario load,
        /// campaign <see cref="CampaignChainDriver.StartNext"/>, and #140 restore.
        /// </summary>
        /// <param name="restoreFrom">
        /// When non-null, reactions and director memory are rebuilt from this snapshot after
        /// the controllers are enabled — required for a correct mid-run load.
        /// </param>
        private void BindSimulation(Simulation sim, List<string> problems = null,
            SimulationSnapshot restoreFrom = null)
        {
            problems ??= new List<string>();

            _sim = sim;
            _scenario = sim.Scenario;
            _map = sim.Map;
            _pause?.BindScenario(_scenario);

            _headerLabel.text = HeaderForCurrent();

            _statusLabel.text =
                $"{_scenario.Sides.Count} SIDES   ·   {_sim.Units.Count} UNITS   ·   " +
                $"{_map.Header.WidthMetres / 1000f:0.#} × {_map.Header.HeightMetres / 1000f:0.#} KM" +
                (problems.Count > 0 ? $"   ·   {problems.Count} VALIDATION WARNING(S)" : string.Empty);

            if (_session != null) _session.Simulation = _sim;

            _sim.AddExecutor(new MoveToExecutor());
            _sim.AddExecutor(new EngageExecutor());
            _sim.AddExecutor(new DefendExecutor());
            _sim.AddExecutor(new ScreenExecutor());
            _sim.AddExecutor(new GuardExecutor());
            _sim.AddExecutor(new CoverExecutor());
            _sim.AddExecutor(new DelayExecutor());
            _sim.EnableReactions();
            if (restoreFrom != null) _sim.RestoreReactionPicture(restoreFrom);

            ApplyPlayModeDirectors(restoreFrom);

            _tickAccumulator = 0f;
            _running = true;
            if (_runToggle != null)
            {
                _suppress = true;
                _runToggle.isOn = true;
                _suppress = false;
            }

            _outcomeShown = false;
            _postBattle?.Close();
            if (_clockLabel != null) _clockLabel.color = Theme.InkMuted;
            _feed.Clear();
            _sim.Reports.Subscribe("ui-feed", 100, OnReport);
            RefreshFeed();

            _directive = null;
            _directiveAcknowledged = false;
            _directiveAcknowledgedTick = 0;
            RefreshDirectiveCard();
            _sim.Directives.Subscribe("ui-directive", 100, OnDirective);

            RefreshSheet();
            BuildMarkers();
            BuildOrbat();
            ClearSelection();
            PublishCommandContext();
            RefreshCampaignChrome();
            SyncTutorialBeat();

            Debug.Log($"[PlayView] {_scenario} — {problems.Count} validation problem(s)");
        }

        /// <summary>#310: start or clear the select→MoveTo checklist for the tutorial scenario.</summary>
        private void SyncTutorialBeat()
        {
            if (_tutorialBeat == null) return;
            if (ScenarioSamples.IsTutorial(_scenario))
                _tutorialBeat.Begin();
            else
                _tutorialBeat.Reset();
        }

        /// <summary>#287 / #291: directors, hotseat seat, and difficulty follow AppSession.</summary>
        private void ApplyPlayModeDirectors(SimulationSnapshot restoreFrom)
        {
            var mode = CurrentPlayMode();
            if (_switchSideButton != null)
                _switchSideButton.interactable = mode == ModeKind.Hotseat;

            var parms = CurrentDirectorParams();

            if (mode == ModeKind.Spectator)
            {
                var all = new List<SideId>();
                foreach (var side in _scenario.Sides) all.Add(side.Id);
                _sim.EnableDirector(all, parms);
                if (restoreFrom != null) _sim.RestoreDirectorMemory(restoreFrom);
                return;
            }

            if (mode == ModeKind.Hotseat)
            {
                if (_session != null)
                {
                    if (!_session.HotseatSide.IsValid)
                    {
                        _session.HotseatSide = _scenario.PlayerSide.IsValid
                            ? _scenario.PlayerSide
                            : (_scenario.Sides.Count > 0 ? _scenario.Sides[0].Id : SideId.None);
                    }
                }
                return;
            }

            if (mode == ModeKind.Replay) return;

            // Solo (default).
            if (_scenario.PlayerSide.IsValid)
            {
                var opposing = new List<SideId>();
                foreach (var side in _scenario.Sides)
                    if (side.Id != _scenario.PlayerSide) opposing.Add(side.Id);
                _sim.EnableDirector(opposing, parms);
                if (restoreFrom != null) _sim.RestoreDirectorMemory(restoreFrom);
            }
        }

        /// <summary>Quicksave the live run — campaign chain blob included when active (#140).</summary>
        private void QuickSave()
        {
            if (_sim == null || _scenario == null)
            {
                _statusLabel.text = "NOTHING TO SAVE";
                return;
            }

            var record = new SaveRecord
            {
                SaveId = QuickSaveId,
                ScenarioName = _scenario.Name,
                Tick = _sim.Tick,
                SavedAtUtc = DateTime.UtcNow.ToString("o"),
                Snapshot = _sim.Snapshot(),
                OperationIndex = -1,
            };

            if (_session != null && _session.HasActiveCampaign)
            {
                record.CampaignName = _session.ActiveChain.Name;
                record.OperationIndex = _session.ActiveOperationIndex;
                record.CampaignChainJson = CampaignChainIO.ToJson(_session.ActiveChain);
            }

            try
            {
                var saveResult = Store.SaveAsync(record).GetAwaiter().GetResult();
                if (!saveResult.Ok)
                {
                    Debug.LogError($"[PlayView] save failed: {saveResult.Message}");
                    _statusLabel.text = "SAVE FAILED";
                    return;
                }

                _statusLabel.text = _session != null && _session.HasActiveCampaign
                    ? $"SAVED  ·  {record.CampaignName}  OP {record.OperationIndex + 1}  T+{record.Tick}"
                    : $"SAVED  ·  {_scenario.Name.ToUpperInvariant()}  T+{record.Tick}";
                Debug.Log($"[PlayView] saved '{QuickSaveId}' at tick {_sim.Tick} " +
                          $"(player={Identity.PlayerId})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayView] save failed: {ex.Message}");
                _statusLabel.text = "SAVE FAILED";
            }
        }

        /// <summary>Load the quicksave — restores campaign context when the record carries it.</summary>
        private void QuickLoad()
        {
            var loadResult = Store.LoadAsync(QuickSaveId).GetAwaiter().GetResult();
            if (loadResult.Status == StoreStatus.VersionMismatch)
            {
                Debug.LogError($"[PlayView] {loadResult.Message}");
                _statusLabel.text = "SAVE VERSION MISMATCH";
                return;
            }

            if (loadResult.Status == StoreStatus.NotFound || !loadResult.Ok)
            {
                _statusLabel.text = "NO QUICKSAVE";
                return;
            }

            var record = loadResult.Value;
            if (record?.Snapshot == null)
            {
                _statusLabel.text = "NO QUICKSAVE";
                return;
            }

            if (!string.IsNullOrWhiteSpace(record.CampaignChainJson))
            {
                var chain = CampaignChainIO.FromJson(record.CampaignChainJson);
                if (chain == null || record.OperationIndex < 0 ||
                    record.OperationIndex >= chain.Operations.Count)
                {
                    _statusLabel.text = "CAMPAIGN SAVE CORRUPT";
                    return;
                }

                _session?.BeginCampaign(chain, record.OperationIndex);
            }
            else
            {
                _session?.ClearCampaign();
            }

            var sim = Simulation.Restore(record.Snapshot, UnitCatalogue.Default());
            BindSimulation(sim, restoreFrom: record.Snapshot);
            _statusLabel.text = _session != null && _session.HasActiveCampaign
                ? $"LOADED  ·  {_session.ActiveChain.Name}  OP {_session.ActiveOperationIndex + 1}  T+{sim.Tick}"
                : $"LOADED  ·  {_scenario.Name.ToUpperInvariant()}  T+{sim.Tick}";
            Debug.Log($"[PlayView] loaded '{QuickSaveId}' at tick {sim.Tick}");
        }

        /// <summary>
        /// #298: load the quicksave's logs into a fresh sim via <see cref="Replayer"/> —
        /// Issue chrome stays off under Replay mode.
        /// </summary>
        private void ReplayQuickSave()
        {
            if (_session != null)
            {
                _session.PlayMode = ModeKind.Replay;
                if (_playModeDrop != null)
                {
                    _suppress = true;
                    _playModeDrop.value = (int)ModeKind.Replay;
                    _suppress = false;
                }
            }

            var loadResult = Store.LoadAsync(QuickSaveId).GetAwaiter().GetResult();
            if (loadResult.Status == StoreStatus.VersionMismatch)
            {
                Debug.LogError($"[PlayView] {loadResult.Message}");
                _statusLabel.text = "SAVE VERSION MISMATCH";
                return;
            }

            if (!loadResult.Ok || loadResult.Value?.Snapshot == null)
            {
                _statusLabel.text = "NO QUICKSAVE TO REPLAY";
                return;
            }

            var record = loadResult.Value;

            _session?.ClearCampaign();

            var recorded = Simulation.Restore(record.Snapshot, UnitCatalogue.Default());
            var scenario = recorded.Scenario;
            var map = recorded.Map;
            var target = new Simulation(scenario, map, UnitCatalogue.Default());
            BindSimulation(target);

            int steps = Mathf.Max(0, record.Tick);
            Replayer.Run(recorded, target, steps);

            if (_runToggle != null)
            {
                _suppress = true;
                _runToggle.isOn = false;
                _suppress = false;
            }
            _running = false;

            RefreshSheet();
            BuildMarkers();
            BuildOrbat();
            ClearSelection();
            PublishCommandContext();

            _statusLabel.text =
                $"REPLAY  ·  {scenario.Name.ToUpperInvariant()}  ·  {steps} TICKS";
            Debug.Log($"[PlayView] replayed '{QuickSaveId}' through {steps} ticks");
        }

        private string HeaderForCurrent()
        {
            if (_session != null && _session.HasActiveCampaign)
            {
                var chain = _session.ActiveChain;
                var op = chain.Operations[_session.ActiveOperationIndex];
                return $"{chain.Name.ToUpperInvariant()}  ·  OP {_session.ActiveOperationIndex + 1}/" +
                       $"{chain.Operations.Count}  ·  {op.Name.ToUpperInvariant()}";
            }

            return _scenario != null ? _scenario.Name.ToUpperInvariant() : "SCENARIO";
        }

        private void RefreshCampaignChrome()
        {
            if (_campaignLabel == null) return;

            string modeTag = CurrentPlayMode().ToString().ToUpperInvariant();
            string aiTag = _session != null
                ? $"{_session.AiDifficulty.ToString().ToUpperInvariant()}/" +
                  $"{_session.AiPersonality.ToString().ToUpperInvariant()}"
                : "NORMAL/BALANCED";

            if (_session == null || !_session.HasActiveCampaign)
            {
                _campaignLabel.text =
                    $"{modeTag}  ·  {aiTag}  ·  Single scenario  ·  START loads the valley chain";
                if (_continueCampaignButton != null)
                    _continueCampaignButton.interactable = false;
                if (_switchSideButton != null)
                    _switchSideButton.interactable = CurrentPlayMode() == ModeKind.Hotseat;
                return;
            }

            var chain = _session.ActiveChain;
            int i = _session.ActiveOperationIndex;
            var op = chain.Operations[i];
            bool canContinue = _outcomeShown && _session.HasNextOperation;

            _campaignLabel.text =
                $"{modeTag}  ·  {aiTag}  ·  {chain.Name}  ·  op {i + 1}/{chain.Operations.Count}  ·  {op.Name}" +
                (canContinue
                    ? "\nOperation decided  ·  CONTINUE carries the ORBAT forward"
                    : _outcomeShown && !_session.HasNextOperation
                        ? "\nCampaign complete"
                        : "\nFight to a decision, then CONTINUE");

            if (_continueCampaignButton != null)
                _continueCampaignButton.interactable = canContinue;
            if (_switchSideButton != null)
                _switchSideButton.interactable = CurrentPlayMode() == ModeKind.Hotseat;
        }

        private void RefreshSheet()
        {
            if (_suppress || _map == null || _card == null) return;

            var options = MapRenderOptions.Default;
            options.Mode = Pick(DisplayNames.RenderModes, _modeDrop, MapRenderMode.Topographic);
            options.PixelsPerCell =
                Mathf.Clamp(1600f / Mathf.Max(_map.Width, _map.Height), 0.5f, 4f);

            _card.Render(_map, options, (pixels, view) =>
            {
                if (_scenario?.ControlMeasures != null && _scenario.ControlMeasures.Count > 0)
                {
                    // #186: hide opposing-owned GCMs; shared (Owner = None) still draw.
                    ControlMeasureDrawer.Draw(pixels, view, _scenario.ControlMeasures, SideInk,
                        ViewerSide());
                }

                // #34 / #276: dynamic hazards on the sheet, same pixel space as GCMs.
                if (_sim?.World != null && _sim.World.Count > 0)
                    World.WorldObjectDrawer.Draw(pixels, view, _sim.World.Objects);
            });
            _card.SetMarginaliaFor(_map, _scenario.Map.Seed);
            LayOutMarkers();
        }

        private Color32 SideInk(SideId side)
        {
            var s = _scenario?.FindSide(side);
            if (s == null) return new Color32(40, 40, 40, 220);
            var c = s.Colour;
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255),
                220);
        }

        // ─── Unit markers ─────────────────────────────────────────────────────

        private void BuildMarkers()
        {
            foreach (var m in _markers) Destroy(m.Go);
            _markers.Clear();
            if (_scenario == null) return;

            // Fighting units only. A formation's symbol *stands for* its subordinates, so
            // drawing both puts a battalion on top of its own companies — which is exactly
            // what the first build of the ORBAT tree did, and no probe could see it.
            // Drawing the formation instead of its children at low zoom is symbol LOD, and
            // it is Phase 2.2 rather than this.
            foreach (var unit in _sim.Units)
                _markers.Add(CreateMarker(unit));

            BuildObjectiveMarkers();
            RebuildForceBars();
            LayOutMarkers();
        }

        private Marker CreateMarker(UnitInstance unit)
        {
            var rt = CreateRect($"Unit_{unit.Id}", _unitLayer);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(SymbolSize, SymbolSize);

            var icon = rt.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var label = CreateOverlayTmp("Label", rt, unit.Designation, 10, Theme.Ink);
            label.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.sizeDelta = new Vector2(140, 14);
            label.rectTransform.anchoredPosition = new Vector2(0, -2);
            label.alignment = TextAlignmentOptions.Top;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;

            var marker = new Marker { Unit = unit, Go = rt.gameObject, Icon = icon, Label = label };
            RefreshMarkerSymbol(marker);
            return marker;
        }

        /// <summary>
        /// Re-bakes a marker's symbol when its strength band has moved, and dims it once the
        /// unit is out of the fight.
        /// </summary>
        /// <remarks>
        /// The symbol was previously baked once in <see cref="CreateMarker"/> and never looked
        /// at again, so a unit could be shot to pieces without its map symbol changing at all —
        /// the combat-power bar that <see cref="ConditionDecorator"/> has drawn all along was
        /// simply frozen at the value it had when the scenario loaded.
        ///
        /// Guarded on the band rather than called unconditionally because this runs inside
        /// LayOutMarkers, which runs every frame. <see cref="UnitInstance.StrengthBand"/> is
        /// what bounds how often the guard opens.
        ///
        /// The sprite comes from the shared cache: **never Destroy what this returns.** Only
        /// ClearCache may, and it frees the textures for every holder.
        /// </remarks>
        private void RefreshMarkerSymbol(Marker marker)
        {
            // Negative bands mark a wreck, so the crossing into destroyed always moves the
            // guard even though StrengthBand is 0 on both sides of it.
            int band = marker.Unit.IsDestroyed ? -2 : marker.Unit.StrengthBand;
            if (band == marker.BakedStrength) return;
            marker.BakedStrength = band;

            var sprite = _session.Symbols.GetSymbolSprite(marker.Unit.ToSidcCode(), BakeSize);
            if (sprite != null) marker.Icon.sprite = sprite;

            // A destroyed unit stays on the map — removal is #14 — so it has to read as out of
            // the fight without disappearing. Fading is a presentation choice and touches no
            // symbology: the frame keeps its affiliation colour, which is the one thing on a
            // symbol that must never be repurposed.
            // A wreck stays on the map — it is information, and ground where a company was
            // destroyed is worth knowing about. The symbol now carries APP-6D's
            // PresentDestroyed status of its own accord, so this only has to say "not a going
            // concern"; the meaning is in the symbology rather than in an opacity.
            marker.Icon.color = sprite == null ? new Color(0, 0, 0, 0)
                              : marker.Unit.IsDestroyed ? new Color(1f, 1f, 1f, 0.55f)
                              : Color.white;
        }

        /// <summary>
        /// Places every marker from its cell position through the card's transform.
        ///
        /// Recomputed rather than cached, so pan, zoom, re-render and window resize all keep
        /// symbols on their own ground — the acceptance criterion that a screenshot at one
        /// zoom cannot demonstrate on its own.
        /// </summary>
        private void LayOutMarkers()
        {
            if (_card == null) return;

            bool labels = _showLabels == null || _showLabels.isOn;

            // The frame is left of centre inside the baked texture, so placing the sprite by
            // its pivot would put the *texture* centre on the unit's position and leave every
            // symbol visibly offset. Shift by the pivot-to-frame-centre vector instead.
            Vector2 frameOffset = SymbolLayout.PivotToFrameCentre * SymbolSize;

            LayOutObjectives();

            bool markedThisPass = false;

            foreach (var m in _markers)
            {
                bool visible = _card.CellToLocal(m.Unit.Cell, out var local);
                m.Go.SetActive(visible);
                if (!visible) continue;

                // Cheap: returns immediately unless the unit's strength band has moved.
                RefreshMarkerSymbol(m);

                ((RectTransform)m.Go.transform).anchoredPosition = local - frameOffset;
                m.Label.gameObject.SetActive(labels);

                // The brackets sit on the frame, not on the marker's centre, so they stay
                // concentric with the symbol the user is actually looking at.
                if (_selectionMark != null && IsSelected(m.Unit.Id))
                {
                    _selectionMark.anchoredPosition = local;
                    _selectionMark.gameObject.SetActive(true);
                    markedThisPass = true;
                }
            }

            // Hidden when nothing is selected, and also when the selected unit is scrolled
            // out of the cropped view — brackets floating over empty ground would imply a
            // unit is there.
            if (_selectionMark != null && !markedThisPass)
                _selectionMark.gameObject.SetActive(false);

            LayOutOrders();
        }

        // ─── Directive ────────────────────────────────────────────────────────

        /// <summary>
        /// A mission from higher, standing for the whole scenario — not tied to selection
        /// (unlike <see cref="BuildDetailsCard"/>, which clears to nothing on deselect) and not
        /// only logged to the feed (unlike <see cref="BuildFeedCard"/>, which scrolls). Built
        /// the same shape as those two cards: a title and a body TMP over a coloured rect, plus
        /// a left accent strip — the device <see cref="UiFactory.AddSection"/> uses for its own
        /// header bar — so the card reads as correspondence from outside the ORBAT rather than
        /// another unit-status panel.
        ///
        /// The whole section, header and card and button together, lives in one container so
        /// hiding it for "no directive" is a single SetActive rather than three. There is no
        /// empty-state card: a scenario without a directive shows nothing here at all.
        /// </summary>
        private void BuildDirectiveCard(Transform parent)
        {
            _directiveSection = CreateRect("DirectiveSection", parent);
            var v = _directiveSection.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            _directiveSection.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            AddSection(_directiveSection, "DIRECTIVE");

            var card = CreateRect("Directive", _directiveSection);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 208;
            card.gameObject.AddComponent<Image>().color = UiTheme.CardBg;

            var accent = CreateRect("Accent", card);
            accent.anchorMin = new Vector2(0, 0);
            accent.anchorMax = new Vector2(0, 1);
            accent.pivot = new Vector2(0, 0.5f);
            accent.sizeDelta = new Vector2(4, 0);
            accent.gameObject.AddComponent<Image>().color = Theme.Accent;

            _directiveTitle = CreateTmp("T", card, string.Empty, 13, FontStyles.Bold,
                withLayout: false);
            _directiveTitle.rectTransform.anchorMin = new Vector2(0, 1);
            _directiveTitle.rectTransform.anchorMax = new Vector2(1, 1);
            _directiveTitle.rectTransform.pivot = new Vector2(0.5f, 1);
            _directiveTitle.rectTransform.sizeDelta = new Vector2(-24, 22);
            _directiveTitle.rectTransform.anchoredPosition = new Vector2(6, -6);
            _directiveTitle.alignment = TextAlignmentOptions.MidlineLeft;
            _directiveTitle.color = Theme.Accent;
            _directiveTitle.characterSpacing = 2f;

            _directiveBody = CreateTmp("B", card, string.Empty, 11, FontStyles.Normal,
                withLayout: false);
            Stretch(_directiveBody.rectTransform);
            _directiveBody.rectTransform.offsetMin = new Vector2(16, 8);
            _directiveBody.rectTransform.offsetMax = new Vector2(-10, -28);
            _directiveBody.alignment = TextAlignmentOptions.TopLeft;
            _directiveBody.color = Theme.InkMuted;
            _directiveBody.lineSpacing = 12f;

            // One button, ACKNOWLEDGE only. There is no REFUSE: Simulation.AcknowledgeDirective
            // is deliberately the only response the core exposes, because #73/#36 never state
            // what refusing would change mechanically, and a control with no mechanical
            // consequence is a dead one. Same row shape as ABORT PLAN/HOLD (AddButtonRow +
            // AddButton), just with one entry instead of two.
            var controls = AddButtonRow(_directiveSection);
            _directiveAckButton = AddButton(controls, "ACKNOWLEDGE", AcknowledgeSelectedDirective);
            _directiveAckLabel = _directiveAckButton.GetComponentInChildren<TMP_Text>();

            RefreshDirectiveCard();
        }

        /// <summary>
        /// Runs on delivery of <see cref="Simulation.Directives"/> — subscribed, not polled, so
        /// this view never reads <c>Scenario.Directive</c> directly (see the file header). One
        /// step delayed from publication like everything else on either bus (rule 1), which is
        /// invisible here in practice: v1 ships exactly one directive, published from the
        /// constructor and delivered on the scenario's first step.
        /// </summary>
        private void OnDirective(Directive directive)
        {
            _directive = directive;
            RefreshDirectiveCard();
        }

        private void RefreshDirectiveCard()
        {
            if (_directiveSection == null) return;

            bool has = _directive.HasValue;
            _directiveSection.gameObject.SetActive(has);
            if (!has) return;

            var d = _directive.Value;

            _directiveTitle.text = string.IsNullOrEmpty(d.From)
                ? "DIRECTIVE"
                : $"DIRECTIVE  ·  FROM {d.From.ToUpperInvariant()}";

            var sb = new System.Text.StringBuilder();
            sb.Append("INTENT\n").Append(d.Intent);
            if (!string.IsNullOrEmpty(d.Constraints))
                sb.Append("\n\nCONSTRAINTS\n").Append(d.Constraints);
            sb.Append("\n\nOBJECTIVE: ").Append(ObjectiveNamesOf(d.ObjectiveIds));
            if (d.DeadlineTick > 0)
                sb.Append("\nDEADLINE: T+").Append(d.DeadlineTick.ToString("0000"));
            if (_directiveAcknowledged)
                sb.Append("\nACKNOWLEDGED  T+").Append(_directiveAcknowledgedTick.ToString("0000"));

            _directiveBody.text = sb.ToString();

            // Same idiom ScenarioSetupView.GenerateRoutine uses for "this button has already
            // done its job": disable it and relabel it, rather than leaving it sitting there
            // identical to before — the button itself is the only signal a player has that a
            // second press would do nothing, now that it correctly does nothing (Simulation
            // guards the repeat too; this is the visible half of that).
            if (_directiveAckButton != null) _directiveAckButton.interactable = !_directiveAcknowledged;
            if (_directiveAckLabel != null)
                _directiveAckLabel.text = _directiveAcknowledged ? "ACKNOWLEDGED" : "ACKNOWLEDGE";
        }

        /// <summary>
        /// Objective names for a directive's <see cref="Directive.ObjectiveIds"/>, cross-
        /// referencing <see cref="Scenario.Objectives"/> rather than duplicating the name —
        /// the same "one place tests whether they are held" reasoning that keeps the ids rather
        /// than a copy of the objective payload in the directive itself.
        /// </summary>
        private string ObjectiveNamesOf(int[] ids)
        {
            if (ids == null || ids.Length == 0 || _scenario == null) return "NONE NAMED";

            var names = new List<string>();
            foreach (int id in ids)
            {
                Objective found = null;
                foreach (var o in _scenario.Objectives)
                    if (o.Id == id) { found = o; break; }
                names.Add(found != null ? found.Name : $"#{id}");
            }
            return string.Join(", ", names);
        }

        /// <summary>
        /// The one response v1 offers. Publishes exactly one report through
        /// <see cref="Simulation.AcknowledgeDirective"/> and touches nothing else — no unit's
        /// queue, no command log entry. It is deliberately not a decision about *what to do*;
        /// the player expresses that afterward by issuing their own orders, which is the entire
        /// point of "leaves the how to the receiver".
        /// </summary>
        /// <remarks>
        /// Guarded by <see cref="_directiveAcknowledged"/> as well as by
        /// <see cref="Simulation.AcknowledgeDirective"/>'s own idempotency — belt and braces,
        /// not a substitute for it. This flag is one report-delivery tick behind a press (it is
        /// only ever set from <see cref="OnReport"/>), so a second press landing inside that
        /// window would still reach the simulation; the simulation is what actually cannot be
        /// driven twice, keyed by <c>Directive.Seq</c>. This guard only stops the common case —
        /// a disabled button a player clicks again anyway — from making a redundant call at all.
        /// </remarks>
        private void AcknowledgeSelectedDirective()
        {
            if (_sim == null || !_directive.HasValue || _directiveAcknowledged) return;
            _sim.AcknowledgeDirective(_directive.Value);
        }

        // ─── Selection ────────────────────────────────────────────────────────

        private void BuildDetailsCard(Transform parent)
        {
            _detailsCard = CreateRect("Details", parent);
            // Six lines now: identity, capability and training, hesitation and ground,
            // and the plan. It was 92 and the plan line — the one that says what the
            // unit is actually doing — was being clipped off the bottom unseen.
            _detailsCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 118;
            _detailsCard.gameObject.AddComponent<Image>().color = UiTheme.CardBg;

            _detailsTitle = CreateTmp("T", _detailsCard, string.Empty, 13, FontStyles.Bold,
                withLayout: false);
            _detailsTitle.rectTransform.anchorMin = new Vector2(0, 1);
            _detailsTitle.rectTransform.anchorMax = new Vector2(1, 1);
            _detailsTitle.rectTransform.pivot = new Vector2(0.5f, 1);
            _detailsTitle.rectTransform.sizeDelta = new Vector2(-20, 22);
            _detailsTitle.rectTransform.anchoredPosition = new Vector2(0, -6);
            _detailsTitle.alignment = TextAlignmentOptions.MidlineLeft;
            _detailsTitle.color = Theme.Ink;
            _detailsTitle.characterSpacing = 2f;

            _detailsBody = CreateTmp("B", _detailsCard, string.Empty, 11, FontStyles.Normal,
                withLayout: false);
            Stretch(_detailsBody.rectTransform);
            _detailsBody.rectTransform.offsetMin = new Vector2(10, 6);
            _detailsBody.rectTransform.offsetMax = new Vector2(-10, -28);
            _detailsBody.alignment = TextAlignmentOptions.TopLeft;
            _detailsBody.color = Theme.InkMuted;
            _detailsBody.lineSpacing = 12f;

            ClearSelection();
        }

        // ─── Force bars ───────────────────────────────────────────────────────

        private sealed class ForceBar
        {
            public SideId Side;
            public RectTransform Fill;
            public TMP_Text Label;
        }

        private RectTransform _forceRow;
        private readonly List<ForceBar> _forceBars = new();

        /// <summary>
        /// A strength bar per side across the top of the stage.
        /// </summary>
        /// <remarks>
        /// The one thing a player cannot otherwise see. Individual strengths are in the ORBAT
        /// list, but "how is my force doing against theirs" needed adding up by eye — and it is
        /// the question a commander actually asks.
        ///
        /// Fed by <see cref="VictoryEvaluator.RemainingFraction"/> rather than recomputed, so
        /// the bar is **the same number the DestroyEnemy condition tests**. A bar with its own
        /// denominator would move differently from the condition it appears to predict.
        ///
        /// Coloured from <see cref="Side.Colour"/>, not hard-coded blue and red. Side and
        /// affiliation are deliberately separate here, so a coalition or a three-way scenario
        /// colours itself correctly and this needs no special case.
        /// </remarks>
        private void BuildForceBars(Transform stage)
        {
            _forceRow = CreateRect("ForceBars", stage);
            var le = _forceRow.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 22f;
            le.flexibleHeight = 0f;

            var row = _forceRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 14;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
        }

        private void RebuildForceBars()
        {
            if (_forceRow == null) return;

            for (int i = _forceRow.childCount - 1; i >= 0; i--)
                Destroy(_forceRow.GetChild(i).gameObject);
            _forceBars.Clear();

            if (_scenario == null || _sim?.Victory == null) return;

            foreach (var side in _scenario.Sides)
            {
                var cell = CreateRect($"Force_{side.Id}", _forceRow);

                var track = CreateRect("Track", cell);
                Stretch(track);
                track.offsetMin = new Vector2(0, 4);
                track.offsetMax = new Vector2(0, -4);
                track.gameObject.AddComponent<Image>().color = Theme.SectionBg;

                // Anchored to the left edge so the fill shrinks from the right as the side is
                // worn down, rather than shrinking about its centre.
                var fill = CreateRect("Fill", track);
                fill.anchorMin = new Vector2(0f, 0f);
                fill.anchorMax = new Vector2(1f, 1f);
                fill.pivot = new Vector2(0f, 0.5f);
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = Vector2.zero;
                fill.gameObject.AddComponent<Image>().color = side.Colour;

                var label = CreateTmp("L", track, side.Name, 10, FontStyles.Bold,
                    withLayout: false);
                Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(6, 0);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.color = Theme.Ink;
                label.characterSpacing = 2f;
                label.raycastTarget = false;

                _forceBars.Add(new ForceBar { Side = side.Id, Fill = fill, Label = label });
            }

            RefreshForceBars();
        }

        private void RefreshForceBars()
        {
            var victory = _sim?.Victory;
            if (victory == null) return;

            foreach (var bar in _forceBars)
            {
                float fraction = victory.RemainingFraction(bar.Side, _sim.Units);

                // anchorMax.x drives the width because the fill is pinned to the left edge.
                var max = bar.Fill.anchorMax;
                max.x = fraction;
                bar.Fill.anchorMax = max;
                bar.Fill.offsetMax = Vector2.zero;

                var side = _scenario.FindSide(bar.Side);
                bar.Label.text = $"{side?.Name ?? bar.Side.ToString()}   {fraction * 100f:0}%";
            }
        }

        // ─── Objective markers ────────────────────────────────────────────────

        private sealed class ObjectiveMarker
        {
            public Objective Objective;
            public RectTransform Rect;
            public Image Ring;
            public TMP_Text Label;
        }

        private readonly List<ObjectiveMarker> _objectiveMarkers = new();

        /// <summary>
        /// Builds a ring per objective, under the same masked layer as the unit symbols.
        /// </summary>
        /// <remarks>
        /// Without this an objective exists only in the status line, and a player cannot march
        /// on ground they have no way to find — which makes the victory condition from #14
        /// unplayable even though it evaluates correctly.
        ///
        /// First sibling, so rings sit behind unit symbols and order routes. An objective is
        /// context for the units standing in it; a ring drawn over a symbol obscures the thing
        /// being commanded.
        /// </remarks>
        private void BuildObjectiveMarkers()
        {
            foreach (var m in _objectiveMarkers) Destroy(m.Rect.gameObject);
            _objectiveMarkers.Clear();

            var victory = _sim?.Victory;
            if (victory == null) return;

            foreach (var objective in victory.Objectives)
            {
                var rt = CreateRect($"Obj_{objective.Id}", _unitLayer);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(64f, 64f);
                rt.SetAsFirstSibling();

                var ring = rt.gameObject.AddComponent<Image>();
                ring.sprite = UiSprites.ObjectiveRing;
                ring.raycastTarget = false;
                ring.color = Theme.InkMuted;

                var label = CreateOverlayTmp("Name", rt, objective.Name, 10, Theme.Ink);
                var lrt = label.rectTransform;
                lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 1f);
                lrt.pivot = new Vector2(0.5f, 0f);
                lrt.sizeDelta = new Vector2(220f, 14f);
                lrt.anchoredPosition = new Vector2(0f, 2f);
                label.alignment = TextAlignmentOptions.Bottom;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                label.raycastTarget = false;

                _objectiveMarkers.Add(new ObjectiveMarker
                {
                    Objective = objective, Rect = rt, Ring = ring, Label = label,
                });
            }
        }

        /// <summary>
        /// Places and tints each ring. Driven from LayOutMarkers so objectives follow pan, zoom
        /// and re-crop exactly as unit symbols do.
        /// </summary>
        private void LayOutObjectives()
        {
            if (_card == null || _objectiveMarkers.Count == 0 || _sim?.Victory == null) return;

            var victory = _sim.Victory;

            for (int i = 0; i < _objectiveMarkers.Count; i++)
            {
                var m = _objectiveMarkers[i];
                bool visible = _card.CellToLocal(m.Objective.Cell, out var centre);
                m.Rect.gameObject.SetActive(visible);
                if (!visible) continue;

                // Radius measured over ONE cell and multiplied up, not by transforming the rim.
                // CellToLocal reports false outside the crop, so measuring the rim directly
                // yields nothing for any objective whose edge is off screen.
                float radius = 0f;
                if (_card.CellToLocal(m.Objective.Cell + new Vector2(1f, 0f), out var oneCell))
                    radius = Mathf.Abs(oneCell.x - centre.x) * m.Objective.RadiusCells;

                m.Rect.anchoredPosition = centre;
                m.Rect.sizeDelta = new Vector2(radius * 2f, radius * 2f);

                var owner = victory.OwnerOfIndex(i);
                var side = owner.IsValid ? _scenario.FindSide(owner) : null;
                var colour = side?.Colour ?? Theme.InkMuted;

                m.Ring.color = new Color(colour.r, colour.g, colour.b, 0.9f);
                m.Label.color = colour;
                m.Label.text = side == null
                    ? m.Objective.Name
                    : $"{m.Objective.Name}  ·  {side.Name.ToUpperInvariant()}";
            }
        }

        // ─── Outcome ──────────────────────────────────────────────────────────

        private bool _outcomeShown;

        /// <summary>Objective control, appended to the status line while the game is running.</summary>
        private string DescribeObjectives()
        {
            var victory = _sim?.Victory;
            if (victory == null || victory.Objectives.Count == 0) return string.Empty;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < victory.Objectives.Count; i++)
            {
                var owner = victory.OwnerOfIndex(i);
                string held = owner.IsValid
                    ? (_scenario.FindSide(owner)?.Name ?? owner.ToString())
                    : "NEUTRAL";

                sb.Append("   ·   ").Append(victory.Objectives[i].Name).Append(": ").Append(held);

                // The clock a hold condition actually depends on. It was invisible, which is
                // most of why an objective that had been walked away from went on reading as
                // held: the ring stays your colour, and nothing said nobody was standing in it.
                if (owner.IsValid)
                {
                    int ticks = victory.HeldTicksOfIndex(i, _sim.Tick);
                    sb.Append(ticks > 0 ? $" {ticks / 60}:{ticks % 60:00}" : " (VACATED)");
                }
            }
            return sb.ToString().ToUpperInvariant();
        }

        /// <summary>
        /// Replaces the status line with the result, once.
        ///
        /// Guarded because <c>AdvanceSimulation</c> runs every frame and a decided scenario
        /// stays decided — without it this would rewrite the same string for ever.
        /// </summary>
        private void ShowOutcome()
        {
            if (_outcomeShown || _sim?.Victory == null) return;
            _outcomeShown = true;

            var outcome = _sim.Victory.Outcome;
            string winner = outcome.IsDraw
                ? "DRAW"
                : (_scenario.FindSide(outcome.Winner)?.Name ?? outcome.Winner.ToString())
                  .ToUpperInvariant() + " WINS";

            string campaignNote = string.Empty;
            if (_session != null && _session.HasActiveCampaign)
            {
                if (_session.HasNextOperation)
                {
                    campaignNote = "   ·   CONTINUE FOR NEXT OPERATION";
                }
                else
                {
                    campaignNote = "   ·   CAMPAIGN COMPLETE";
                    // #212: formation labels outlive the finished chain.
                    _session.Career.StampFormationFrom(_scenario);

                    // #224: one promotion when the player wins the last operation.
                    bool playerWon = !outcome.IsDraw &&
                                     _scenario != null &&
                                     _scenario.PlayerSide.IsValid &&
                                     outcome.Winner == _scenario.PlayerSide;
                    if (playerWon)
                    {
                        string rank = _session.CareerRankId;
                        if (RankGate.TryPromoteAfterCampaignWin(ref rank))
                        {
                            _session.CareerRankId = rank;
                            var step = RankAuthorityIO.Current.Find(rank);
                            campaignNote +=
                                $"   ·   PROMOTED TO {(step?.Title ?? rank).ToUpperInvariant()}";
                            Debug.Log($"[PlayView] career promoted to '{rank}' " +
                                      $"(formation '{_session.Career.FormationDesignation}', " +
                                      $"higher '{_session.Career.HigherFormation}')");
                        }
                    }
                }
            }

            _clockLabel.text =
                $"T+{outcome.Tick:0000}   ·   {winner}   ·   {outcome.Summary}{campaignNote}";
            _clockLabel.color = Theme.Alert;

            if (_runToggle != null) _runToggle.isOn = false;

            RefreshCampaignChrome();
            Debug.Log($"[PlayView] scenario decided: {outcome}");

            // #467: post-battle stats + Merit medals (neutralized / objective).
            if (_postBattle != null)
            {
                var review = PostBattleReviewer.Build(_sim);
                _postBattle.Open(review);
            }
        }

        // ─── Situation feed ───────────────────────────────────────────────────

        /// <summary>How many reports the feed keeps. Older ones live in the log.</summary>
        private const int FeedLines = 9;

        private TMP_Text _feedLabel;
        private readonly List<string> _feed = new();

        private void BuildFeedCard(Transform parent)
        {
            var card = CreateRect("Feed", parent);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 128;
            card.gameObject.AddComponent<Image>().color = UiTheme.CardBg;

            _feedLabel = CreateTmp("B", card, string.Empty, 10, FontStyles.Normal,
                withLayout: false);
            Stretch(_feedLabel.rectTransform);
            _feedLabel.rectTransform.offsetMin = new Vector2(10, 6);
            _feedLabel.rectTransform.offsetMax = new Vector2(-10, -6);
            _feedLabel.alignment = TextAlignmentOptions.TopLeft;
            _feedLabel.color = Theme.InkMuted;
            _feedLabel.lineSpacing = 10f;

            RefreshFeed();
        }

        /// <summary>
        /// A delivered report, turned into a line.
        ///
        /// **Subscribed, not polled.** The view could walk <c>_sim.Units</c> every frame and
        /// work out which hostiles are visible in about the same amount of code, and that
        /// version can never be deceived, delayed or jammed — so every consumer written that
        /// way is one that C3 has to come back and rewrite. Reading the topic instead means
        /// this panel already displays a five-minute-old contact correctly, because it never
        /// knew when the contact happened except by being told.
        ///
        /// Order 100: observers run after the unit layer, which subscribes at 0 and is the only
        /// subscriber permitted to mutate anything.
        /// </summary>
        private void OnReport(Strategos.Reports.SituationReport report)
        {
            _feed.Insert(0, FormatReport(report));
            if (_feed.Count > FeedLines) _feed.RemoveRange(FeedLines, _feed.Count - FeedLines);
            RefreshFeed();

            // Opening fire is reported once per engagement (#252) — not per combat tick.
            if (report.Kind == Strategos.Reports.ReportKind.Engaged)
                AudioService.Instance?.PlayCombatFire();

            // The DIRECTIVE card's acknowledged state is read off this same stream, never set
            // directly by the click handler — see _directiveAcknowledged's own note. Matched by
            // AboutDirective against the standing directive's Seq, not merely "any
            // DirectiveAcknowledged", so a hypothetical future second directive could not flip
            // this one's card.
            if (!_directiveAcknowledged && _directive.HasValue &&
                report.Kind == Strategos.Reports.ReportKind.DirectiveAcknowledged &&
                report.AboutDirective == _directive.Value.Seq)
            {
                _directiveAcknowledged = true;
                _directiveAcknowledgedTick = report.ObservedTick;
                RefreshDirectiveCard();
            }
        }

        private string FormatReport(Strategos.Reports.SituationReport report)
        {
            string who = NameOf(report.Source);
            string colour = ColorUtility.ToHtmlStringRGB(
                report.IsObservation ? Theme.Alert : Theme.InkMuted);

            string what = report.Kind switch
            {
                Strategos.Reports.ReportKind.Contact => $"CONTACT  {NameOf(report.Subject)}",
                Strategos.Reports.ReportKind.ContactLost => $"LOST  {NameOf(report.Subject)}",
                Strategos.Reports.ReportKind.Engaged => $"ENGAGING  {NameOf(report.Subject)}",
                Strategos.Reports.ReportKind.Destroyed => "COMBAT INEFFECTIVE",
                Strategos.Reports.ReportKind.Depleted => "AMMUNITION EXPENDED",
                Strategos.Reports.ReportKind.Arrived => "IN POSITION",
                Strategos.Reports.ReportKind.OrderCompleted => "TASK COMPLETE",
                Strategos.Reports.ReportKind.OrderFailed => "UNABLE TO COMPLY",
                Strategos.Reports.ReportKind.Halted => "HALTED",
                Strategos.Reports.ReportKind.DirectiveAcknowledged => "DIRECTIVE ACKNOWLEDGED",
                _ => report.Kind.ToString().ToUpperInvariant(),
            };

            // Hyphens and middots only — the atlas has no en dash and renders it as nothing.
            return $"<color=#{colour}>T+{report.ObservedTick:0000}  {who}  ·  {what}</color>";
        }

        private string NameOf(UnitId id)
        {
            var unit = _scenario?.FindUnit(id);
            if (unit == null) return id.ToString();
            return string.IsNullOrEmpty(unit.Designation) ? id.ToString() : unit.Designation;
        }

        private void RefreshFeed()
        {
            if (_feedLabel == null) return;
            _feedLabel.text = _feed.Count == 0
                ? "<i>No reports.</i>"
                : string.Join("\n", _feed);
        }

        private void OnMapClicked(UnityEngine.EventSystems.PointerEventData e)
        {
            // Left selects (or issues the armed verb), right-click is the practised shortcut —
            // engage-or-march by what is under the cursor. The shortcut path is deliberately
            // not the palette: #53 keeps it unchanged so a practised player is not taxed.
            if (e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
            {
                _dragDistance = 0f;
                _dragHandle = -1;
                OrderByRightClick(e);
                return;
            }

            // A pan ends in a click Unity delivers anyway, so a drag that moved would
            // otherwise select whatever the cursor happened to stop over.
            if (_dragDistance > ClickSlop) { _dragDistance = 0f; _dragHandle = -1; return; }
            _dragDistance = 0f;
            _dragHandle = -1;

            // Armed confirming click (#128 / #54): dispatch from the table row. Unarmed
            // left-click keeps selecting.
            if (_armedVerb != PaletteVerb.None)
            {
                IssueArmedVerb(e);
                return;
            }

            var hit = UnitAt(e);
            if (hit == null) ClearSelection();
            else Select(hit.Id);
        }

        /// <summary>
        /// Right-click shortcut: engage if the click landed on an enemy, otherwise march.
        /// </summary>
        /// <remarks>
        /// One button for both because the distinction is in the world, not in the input —
        /// right-clicking an enemy has meant "attack that" for thirty years. Kept as its own
        /// path so the palette (#53) cannot silently change it. Shared issue helpers below
        /// keep Shift-queue semantics identical to the armed click path.
        /// </remarks>
        private void OrderByRightClick(UnityEngine.EventSystems.PointerEventData e)
        {
            if (_sim == null || _selection.Count == 0) return;

            var unit = _scenario.FindUnit(_selection[0]);
            if (unit == null || !IsPlayerCommanded(unit)) return;

            bool queue = WantsQueue();
            var target = UnitAt(e);
            if (target != null && IsHostileTo(unit, target))
            {
                IssueEngage(unit, target, queue);
                return;
            }

            if (!CellAt(e, out var cell)) return;
            IssueMoveTo(unit, cell, queue);
        }

        /// <summary>
        /// Left-click with a verb armed: issue or draft from the palette table (#128 / #54).
        /// </summary>
        /// <remarks>
        /// Dispatches on <see cref="PaletteVerbDef.Id"/> first so WAYPOINTS (same
        /// <see cref="CommandKind.MoveTo"/> as MOVE) does not fire an immediate march.
        /// A miss (Engage on empty ground, no selection) issues nothing and leaves the verb armed.
        /// </remarks>
        private void IssueArmedVerb(UnityEngine.EventSystems.PointerEventData e)
        {
            if (_sim == null || _selection.Count == 0) return;
            if (!CommandPalette.TryGet(_armedVerb, out var def)) return;

            var unit = _scenario.FindUnit(_selection[0]);
            if (unit == null || !IsPlayerCommanded(unit)) return;

            if (def.Id == PaletteVerb.Waypoints)
            {
                EditWaypointDraft(e);
                return;
            }

            if (def.Id == PaletteVerb.DigIn)
            {
                IssueDigIn(unit, WantsQueue());
                return;
            }

            bool queue = WantsQueue();

            switch (def.Kind)
            {
                case CommandKind.MoveTo:
                    if (!CellAt(e, out var cell)) return;
                    IssueMoveTo(unit, cell, queue);
                    return;

                case CommandKind.Engage:
                {
                    var target = UnitAt(e);
                    if (target == null || !IsHostileTo(unit, target)) return;
                    IssueEngage(unit, target, queue);
                    return;
                }

                default:
                    return;
            }
        }

        /// <summary>
        /// WAYPOINTS draft edit (#54): click a handle to remove it, otherwise append a cell.
        /// </summary>
        private void EditWaypointDraft(UnityEngine.EventSystems.PointerEventData e)
        {
            if (HitWaypointHandle(e, out int handle))
            {
                _waypointDraft.RemoveAt(handle);
                RefreshVerbChrome();
                return;
            }

            if (!CellAt(e, out var cell)) return;
            _waypointDraft.Add(cell);
            RefreshVerbChrome();
        }

        /// <summary>
        /// Commits the draft as ordinary queued <see cref="CommandKind.MoveTo"/> entries.
        /// Shift appends; without Shift the plan is replaced (Abort once, then N MoveTos).
        /// </summary>
        private void CommitWaypoints()
        {
            if (_armedVerb != PaletteVerb.Waypoints) return;
            if (_sim == null || _selection.Count == 0 || _waypointDraft.Count == 0) return;

            var unit = _scenario.FindUnit(_selection[0]);
            if (unit == null || !IsPlayerCommanded(unit)) return;

            bool append = WantsQueue();
            for (int i = 0; i < _waypointDraft.Count; i++)
            {
                bool queue = append || i > 0;
                IssueMoveTo(unit, _waypointDraft[i], queue);
            }

            ClearWaypointDraft();
            RefreshVerbChrome();
        }

        private void ClearWaypointDraft()
        {
            _waypointDraft.Clear();
            _dragHandle = -1;
        }

        private static bool WantsQueue() =>
            Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        /// <summary>
        /// Player-issued order with issued/rejected SFX (#251).
        /// <see cref="Simulation.Issue"/> leaves <c>Seq == 0</c> on scope refusal.
        /// Abort preambles stay on bare Issue so a MoveTo does not double-beep.
        /// </summary>
        private Command IssuePlayer(Command command)
        {
            var stamped = _sim.Issue(command);
            if (stamped.Seq == 0)
                AudioService.Instance?.PlayOrderRejected();
            else
                AudioService.Instance?.PlayOrderIssued();
            return stamped;
        }

        private void IssueMoveTo(UnitInstance unit, Vector2 cell, bool queue)
        {
            var actor = ActorId.ForSide(unit.Side);
            if (!queue) _sim.Issue(Command.Abort(actor, unit.Id));
            IssuePlayer(Command.MoveTo(actor, unit.Id, cell));
            if (queue) _tutorialBeat?.OnQueuedMoveIssued();
            else _tutorialBeat?.OnMoveIssued();
        }

        private void IssueEngage(UnitInstance unit, UnitInstance target, bool queue)
        {
            var actor = ActorId.ForSide(unit.Side);
            if (!queue) _sim.Issue(Command.Abort(actor, unit.Id));
            IssuePlayer(Command.Engage(actor, unit.Id, target.Id));
            _tutorialBeat?.OnEngageIssued();
        }

        /// <summary>
        /// Whether the player may give this unit orders.
        /// </summary>
        /// <remarks>
        /// Selection is deliberately *not* restricted — inspecting an enemy is useful and
        /// harmless, and what a player is allowed to know is the fog-of-war question rather
        /// than this one. Only issuing orders is gated.
        ///
        /// A scenario with no declared player side leaves everything commandable, which is how
        /// a hot-seat game is spelled.
        /// </remarks>
        private bool IsPlayerCommanded(UnitInstance unit)
        {
            if (_sim != null && _sim.Hierarchy.IsFormation(unit.Id)
                    ? _sim.Hierarchy.IsDestroyed(unit.Id)
                    : unit.IsDestroyed)
                return false;

            var mode = CurrentPlayMode();
            if (mode == ModeKind.Spectator || mode == ModeKind.Replay) return false;

            if (mode == ModeKind.Hotseat)
            {
                if (_session == null || !_session.HotseatSide.IsValid) return false;
                return unit.Side == _session.HotseatSide;
            }

            return (_scenario == null || !_scenario.PlayerSide.IsValid ||
                    unit.Side == _scenario.PlayerSide) &&
                   Commands.CommandScope.CanAddress(_scenario, unit);
        }

        private bool IsHostileTo(UnitInstance a, UnitInstance b) =>
            Side.AreHostile(_scenario?.FindSide(a.Side), _scenario?.FindSide(b.Side));

        // --- Pan and zoom ---------------------------------------------------

        /// <summary>
        /// How far the pointer may travel during a press and still count as a click.
        /// </summary>
        /// <remarks>
        /// Unity delivers OnPointerClick on release whether or not the pointer moved, so
        /// without this every pan would also select whatever happened to be under the cursor
        /// when the drag stopped. Screen pixels, generous enough to survive a shaky hand on a
        /// press that was meant to be a click.
        /// </remarks>
        private const float ClickSlop = 6f;

        private float _dragDistance;

        /// <summary>
        /// Drag pans, or moves a waypoint handle when WAYPOINTS is armed (#54).
        /// Left button, because every map pans with a left drag and right has to
        /// stay free for orders, which is the gesture that must not be ambiguous.
        /// </summary>
        private void OnMapDragged(UnityEngine.EventSystems.PointerEventData e)
        {
            if (e.button != UnityEngine.EventSystems.PointerEventData.InputButton.Left) return;
            if (_card == null) return;

            _dragDistance += e.delta.magnitude;

            if (_armedVerb == PaletteVerb.Waypoints)
            {
                if (_dragHandle < 0 && HitWaypointHandle(e, out int handle))
                    _dragHandle = handle;

                if (_dragHandle >= 0 && _dragHandle < _waypointDraft.Count &&
                    CellAt(e, out var cell))
                {
                    _waypointDraft[_dragHandle] = cell;
                    return;
                }
            }

            Rect card = _card.Rect.rect;
            if (card.width < 1f || card.height < 1f) return;

            var uv = _card.Image.uvRect;

            // The sheet follows the cursor, so the window moves against it.
            uv.x -= e.delta.x / card.width * uv.width;
            uv.y -= e.delta.y / card.height * uv.height;

            _card.Image.uvRect = ClampWindow(uv);
            LayOutMarkers();
        }

        /// <summary>Nearest draft handle under the pointer, in sheet-local pixels.</summary>
        private bool HitWaypointHandle(UnityEngine.EventSystems.PointerEventData e, out int index)
        {
            index = -1;
            if (_waypointDraft.Count == 0 || _card == null) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _card.Rect, e.position, e.pressEventCamera, out var click))
                return false;

            float radius = 14f;
            float bestSq = radius * radius;
            for (int i = 0; i < _waypointDraft.Count; i++)
            {
                if (!_card.CellToLocal(_waypointDraft[i], out var local)) continue;
                float d = (click - local).sqrMagnitude;
                if (d < bestSq) { bestSq = d; index = i; }
            }

            return index >= 0;
        }

        /// <summary>
        /// Scroll zooms, smoothly, about the cursor and within the echelon band.
        /// </summary>
        /// <remarks>
        /// About the cursor rather than the centre so the ground under the pointer stays under
        /// it. Continuous rather than stepped: stops belong to symbol LOD, where a level means
        /// "draw formations, or draw their subordinates", and inventing them before that
        /// exists would be steps that quantise nothing.
        /// </remarks>
        private void OnMapScrolled(UnityEngine.EventSystems.PointerEventData e)
        {
            if (_card == null || Mathf.Approximately(e.scrollDelta.y, 0f)) return;

            var uv = _card.Image.uvRect;
            float factor = e.scrollDelta.y > 0f ? 0.9f : 1f / 0.9f;

            if (!PointerToUv(e, out var pivot)) return;

            float w = uv.width * factor;
            float h = uv.height * factor;
            uv.x = pivot.x - (pivot.x - uv.x) * (w / uv.width);
            uv.y = pivot.y - (pivot.y - uv.y) * (h / uv.height);
            uv.width = w;
            uv.height = h;

            _card.Image.uvRect = ClampWindow(uv);
            LayOutMarkers();
        }

        /// <summary>Where the pointer is, in the sheet's uv space.</summary>
        private bool PointerToUv(UnityEngine.EventSystems.PointerEventData e, out Vector2 uv)
        {
            uv = default;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _card.Rect, e.position, e.pressEventCamera, out var local))
                return false;

            Rect r = _card.Rect.rect;
            if (r.width < 1f || r.height < 1f) return false;

            var shown = _card.Image.uvRect;
            uv = new Vector2(
                shown.x + (local.x - r.xMin) / r.width * shown.width,
                shown.y + (local.y - r.yMin) / r.height * shown.height);
            return true;
        }

        /// <summary>
        /// Holds the view inside the echelon band and on the sheet.
        /// </summary>
        /// <remarks>
        /// **The zoom limits are the echelon's, and that is the point.** A squad leader may not
        /// widen to a theatre picture and a corps commander may not drop to a single platoon,
        /// because the natural thing to do with a platoon on screen is to order it directly —
        /// which is the command problem ROADMAP.md says height is supposed to take away. The
        /// bands are contiguous and configurable; see EchelonSpans.
        ///
        /// Both axes scale from one factor, because a sheet with a different scale on each
        /// axis misreports every distance on it.
        /// </remarks>
        private Rect ClampWindow(Rect uv)
        {
            float mapMetres = _map == null ? 0f : _map.Header.WidthMetres;

            if (mapMetres > 0f)
            {
                var span = CommandSpan();
                float minW = Mathf.Clamp01(span.MinMetres / mapMetres);
                float maxW = Mathf.Clamp01(span.MaxMetres / mapMetres);
                if (maxW < minW) maxW = minW;

                float width = Mathf.Clamp(uv.width, minW, maxW);
                float scale = uv.width > 0.0001f ? width / uv.width : 1f;

                uv.x += (uv.width - width) * 0.5f;
                uv.y += (uv.height - uv.height * scale) * 0.5f;
                uv.width = width;
                uv.height *= scale;
            }

            uv.width = Mathf.Min(uv.width, 1f);
            uv.height = Mathf.Min(uv.height, 1f);
            uv.x = Mathf.Clamp(uv.x, 0f, 1f - uv.width);
            uv.y = Mathf.Clamp(uv.y, 0f, 1f - uv.height);
            return uv;
        }

        /// <summary>
        /// The echelon the player commands, and therefore how much ground they may see.
        /// </summary>
        /// <remarks>
        /// Taken from <see cref="Commands.CommandScope.EffectivePlayerEchelon"/> — authored
        /// <see cref="Scenario.PlayerEchelon"/> when set, else the top of their ORBAT — so
        /// zoom follows the seat (#36) and career rank (#76) gates which seats they get.
        /// With no player side declared the game is hot-seat and nobody's echelon is
        /// privileged, so the highest on the map wins.
        /// </remarks>
        private EchelonSpan CommandSpan() =>
            EchelonSpanIO.Current.For(CommandEchelon())
                .ClampedTo(_map == null ? 0f : _map.Header.WidthMetres);

        /// <summary>Player command echelon — authored or derived from the ORBAT.</summary>
        private NatoSymbols.Echelon CommandEchelon() =>
            Commands.CommandScope.EffectivePlayerEchelon(_scenario);

        /// <summary>Publishes who the player commands so the shell can show rank insignia.</summary>
        private void PublishCommandContext()
        {
            if (_session == null) return;

            if (CurrentPlayMode() == ModeKind.Spectator || CurrentPlayMode() == ModeKind.Replay)
            {
                _session.ClearCommandContext();
                return;
            }

            var sideId = ViewerSide();
            if (_scenario == null || !sideId.IsValid)
            {
                _session.ClearCommandContext();
                return;
            }

            var side = _scenario.FindSide(sideId);
            _session.SetCommandContext(side, CommandEchelon());
        }

        /// <summary>Cell under the pointer. The inverse of the transform that draws markers.</summary>
        private bool CellAt(UnityEngine.EventSystems.PointerEventData e, out Vector2 cell)
        {
            cell = default;
            if (_map == null) return false;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _card.Rect, e.position, e.pressEventCamera, out var local))
                return false;

            Rect r = _card.Rect.rect;
            if (r.width < 1f || r.height < 1f) return false;

            var t = new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);

            var shown = _card.Image.uvRect;
            var uv = new Vector2(shown.x + t.x * shown.width, shown.y + t.y * shown.height);

            var view = _card.Viewport;
            int px = Mathf.Clamp(Mathf.FloorToInt(uv.x * view.Width), 0, view.Width - 1);
            int py = Mathf.Clamp(Mathf.FloorToInt(uv.y * view.Height), 0, view.Height - 1);

            cell = view.PixelToCell(px, py);
            return true;
        }

        /// <summary>
        /// Advances the simulation by whole fixed ticks, banking the remainder.
        ///
        /// Capped per frame so a stall — a long generation, a breakpoint — cannot produce a
        /// burst of catch-up steps that looks like units teleporting.
        /// </summary>
        private void AdvanceSimulation()
        {
            if (_sim == null) return;

            if (!_running)
            {
                // Nothing to step, but an order the player issues while paused lands on
                // Simulation.Bus immediately and sits there undelivered until the clock
                // resumes — the command path is correct (#59) and this is the only place that
                // says so. Without a refresh every paused frame, the clock line would only
                // ever reflect the pending count as of the moment the pause toggle was hit,
                // not as of the order just given.
                RefreshClock();
                return;
            }

            _tickAccumulator += Time.deltaTime * _timeScale;

            // Bank a bounded amount of simulated time rather than a bounded number of steps.
            // A step cap has to grow with the compression to keep up, and then stops being a
            // guard; capping the bank keeps the guard fixed — a frame hitch drops simulated
            // time instead of producing a burst of catch-up steps that looks like teleporting
            // — while still allowing a compressed clock all the steps it legitimately needs.
            float maxBank = MaxBankedTicks * Simulation.SecondsPerTick;
            if (_tickAccumulator > maxBank) _tickAccumulator = maxBank;

            int steps = 0;
            while (_tickAccumulator >= Simulation.SecondsPerTick)
            {
                _tickAccumulator -= Simulation.SecondsPerTick;
                _sim.Step();
                steps++;

                // Stop on the tick it was decided rather than finishing the banked batch —
                // at x300 that would run several more minutes of a scenario already over.
                if (_sim.IsOver) { _running = false; _tickAccumulator = 0f; break; }
            }

            if (steps > 0) RefreshClock();
            if (_sim.IsOver) ShowOutcome();
        }

        private void OnSpeedChanged()
        {
            if (_suppress || _speedDrop == null) return;
            _timeScale = TimeScales[Mathf.Clamp(_speedDrop.value, 0, TimeScales.Length - 1)];
            RefreshClock();
        }

        /// <summary>
        /// Everything the clock line states, including — while paused — how many issued orders
        /// have not yet reached their addressee.
        /// </summary>
        /// <remarks>
        /// #59: pausing to study the ground and edit a plan is the obvious use of a pause
        /// button, and every order given while paused is logged and published, then sits on
        /// <see cref="Simulation.Bus"/> until the clock resumes — correct delivery timing, no
        /// visible sign of it. <see cref="Messaging.MessageBus{T}.PendingCount"/> already states
        /// exactly that (added for #74's snapshot), so this reads existing public state rather
        /// than adding anything to Core.
        /// </remarks>
        private void RefreshClock()
        {
            if (_clockLabel == null || _sim == null) return;

            int moving = 0;
            foreach (var u in _sim.Units)
            {
                var q = _sim.QueueOf(u.Id);
                if (q != null && !q.IsEmpty) moving++;
            }

            int pending = _sim.Bus.PendingCount;
            string clockState = _running ? $"x{_timeScale:0}"
                : pending > 0 ? $"PAUSED   ·   {pending} ORDER{(pending == 1 ? string.Empty : "S")} PENDING"
                : "PAUSED";

            _clockLabel.text =
                $"T+{_sim.Tick:0000}   ·   {clockState}   ·   " +
                $"{moving} UNDER ORDERS   ·   {_sim.Log.Count} ORDERS   ·   " +
                $"{_sim.ReportLog.Count} REPORTS{DescribeObjectives()}";

            RefreshForceBars();

            // The details panel shows a live plan, so keep it current while one is selected.
            if (_selection.Count > 0) RefreshSelection();
        }

        /// <summary>
        /// The unit under the pointer, or null.
        ///
        /// Hit-tested in the card's LOCAL space against the drawn symbol size, not in cell
        /// space. Symbols are drawn at a fixed on-screen size, so a fixed local radius always
        /// matches what is actually on screen; a fixed *cell* radius would shrink away as you
        /// zoom out and make units progressively harder to click. (The issue's note said cell
        /// space — that reasoning was written before #6 fixed the symbol size on screen.)
        ///
        /// Nearest wins rather than first found, so overlapping units resolve to the one the
        /// pointer is actually closest to instead of whichever happens to come first in the
        /// scenario's list.
        /// </summary>
        private UnitInstance UnitAt(UnityEngine.EventSystems.PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _card.Rect, e.position, e.pressEventCamera, out var click))
                return null;

            float radius = SymbolSize * 0.6f;
            float bestSq = radius * radius;
            UnitInstance best = null;

            // Test against `local`, which is where the *frame* is drawn. LayOutMarkers
            // offsets each marker's centre by -frameOffset precisely so the frame lands on
            // the unit's position, so the frame — the thing the eye targets — is at `local`.
            foreach (var m in _markers)
            {
                if (!m.Go.activeSelf) continue;
                if (!_card.CellToLocal(m.Unit.Cell, out var local)) continue;

                float d = (click - local).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = m.Unit; }
            }

            return best;
        }

        /// <summary>
        /// Aborts the selected unit's plan.
        ///
        /// Issued as a command rather than clearing the queue directly, so it is logged and a
        /// replay reconstructs that the plan was cut short and when — the motivating case for
        /// the whole design: recon reports a trap, the commander aborts.
        /// </summary>
        private void AbortSelected()
        {
            if (_sim == null || _selection.Count == 0) return;
            var unit = _scenario.FindUnit(_selection[0]);
            if (unit == null || !IsPlayerCommanded(unit)) return;
            IssuePlayer(Command.Abort(ActorId.ForSide(unit.Side), unit.Id));
            _tutorialBeat?.OnAbortIssued();
        }

        /// <summary>
        /// Orders the selected unit to stand where it is.
        /// </summary>
        /// <remarks>
        /// Issues a Defend, which is a *world* command: it occupies the queue rather than
        /// emptying it, and the unit is doing something for as long as it holds.
        ///
        /// It used to be a control command that was a byte-for-byte copy of Abort, so the two
        /// buttons differed only in what the log called them. Now HOLD means stay here and
        /// prepare — the unit halts, and digs in after two minutes, which halves incoming fire
        /// — while ABORT PLAN still means stop and do nothing. They are different orders at
        /// last, which is what #58 asked for.
        /// </remarks>
        private void HoldSelected()
        {
            if (_sim == null || _selection.Count == 0) return;
            var unit = _scenario.FindUnit(_selection[0]);
            if (unit == null || !IsPlayerCommanded(unit)) return;
            IssueDigIn(unit, queue: false);
        }

        /// <summary>
        /// DigIn special action (#33): capability-gated bridge to Hold/Defend.
        /// </summary>
        private void IssueDigIn(UnitInstance unit, bool queue)
        {
            if (_sim == null || unit == null) return;
            var actor = ActorId.ForSide(unit.Side);
            var cmd = SpecialActions.SpecialAction.TryCreate(
                SpecialActions.ActionKind.DigIn, actor, unit, _sim.Catalogue);
            if (!cmd.HasValue)
            {
                if (_statusLabel != null) _statusLabel.text = "CANNOT DIG IN";
                Debug.Log($"[PlayView] DigIn refused for {unit.Designation} ({unit.CapabilityId})");
                AudioService.Instance?.PlayOrderRejected();
                return;
            }

            if (!queue) _sim.Issue(Command.Abort(actor, unit.Id));
            IssuePlayer(cmd.Value);
        }

        /// <summary>
        /// Screen where you stand — observe further, without digging in (#85).
        /// </summary>
        private void ScreenSelected()
        {
            if (_sim == null || _selection.Count == 0) return;
            var unit = _scenario.FindUnit(_selection[0]);
            if (unit == null || !IsPlayerCommanded(unit)) return;
            IssuePlayer(Command.Screen(ActorId.ForSide(unit.Side), unit.Id));
        }

        /// <summary>
        /// Guard where you stand — dig in and keep a modest watch (#85).
        /// </summary>
        private void GuardSelected()
        {
            if (_sim == null || _selection.Count == 0) return;
            var unit = _scenario.FindUnit(_selection[0]);
            if (unit == null || !IsPlayerCommanded(unit)) return;
            IssuePlayer(Command.Guard(ActorId.ForSide(unit.Side), unit.Id));
        }

        /// <summary>
        /// Cover where you stand — dig in and accept the fight (#85).
        /// </summary>
        private void CoverSelected()
        {
            if (_sim == null || _selection.Count == 0) return;
            var unit = _scenario.FindUnit(_selection[0]);
            if (unit == null || !IsPlayerCommanded(unit)) return;
            IssuePlayer(Command.Cover(ActorId.ForSide(unit.Side), unit.Id));
        }

        private void WithdrawSelected()
        {
            if (_sim == null || _selection.Count == 0) return;
            var unit = _scenario.FindUnit(_selection[0]);
            if (unit == null || !IsPlayerCommanded(unit)) return;
            IssuePlayer(Command.Withdraw(ActorId.ForSide(unit.Side), unit.Id));
        }

        private void DelaySelected()
        {
            if (_sim == null || _selection.Count == 0) return;
            var unit = _scenario.FindUnit(_selection[0]);
            if (unit == null || !IsPlayerCommanded(unit)) return;
            IssuePlayer(Command.Delay(ActorId.ForSide(unit.Side), unit.Id));
        }

        private void AttackSelected()
        {
            if (_sim == null || _selection.Count == 0) return;
            var unit = _scenario.FindUnit(_selection[0]);
            if (unit == null || !IsPlayerCommanded(unit)) return;
            IssuePlayer(Command.Attack(ActorId.ForSide(unit.Side), unit.Id));
        }

        private void ReconSelected()
        {
            if (_sim == null || _selection.Count == 0) return;
            var unit = _scenario.FindUnit(_selection[0]);
            if (unit == null || !IsPlayerCommanded(unit)) return;
            IssuePlayer(Command.Recon(ActorId.ForSide(unit.Side), unit.Id));
        }

        private void ExploitSelected()
        {
            if (_sim == null || _selection.Count == 0) return;
            var unit = _scenario.FindUnit(_selection[0]);
            if (unit == null || !IsPlayerCommanded(unit)) return;
            IssuePlayer(Command.Exploit(ActorId.ForSide(unit.Side), unit.Id));
        }

        private void PursueSelected()
        {
            if (_sim == null || _selection.Count == 0) return;
            var unit = _scenario.FindUnit(_selection[0]);
            if (unit == null || !IsPlayerCommanded(unit)) return;
            IssuePlayer(Command.Pursue(ActorId.ForSide(unit.Side), unit.Id));
        }

        /// <summary>
        /// Calls the chosen drill on the selected unit or formation.
        /// </summary>
        /// <remarks>
        /// The order goes out as a single `Drill` command and is unpacked by the simulation,
        /// rather than the view expanding it and issuing the parts. That keeps the log honest
        /// — it records that the commander called 2, and separately what 2 became — and it
        /// means a drill addressed to a formation decomposes to its subordinates through the
        /// same path a formation's other orders already take. A view that expanded drills
        /// itself would have to reimplement both, and would be the only thing that knew.
        /// </remarks>
        private void ExecuteDrillOnSelected()
        {
            if (_sim == null || _selection.Count == 0 || _drillDrop == null) return;

            var unit = _scenario.FindUnit(_selection[0]);
            if (unit == null || !IsPlayerCommanded(unit)) return;

            var drills = TtpLibrary.All;
            int i = Mathf.Clamp(_drillDrop.value, 0, drills.Count - 1);
            if (drills.Count == 0) return;

            IssuePlayer(Command.Drill(ActorId.ForSide(unit.Side), unit.Id, drills[i].Code));
        }

        private void Select(UnitId id)
        {
            ClearWaypointDraft();
            _selection.Clear();
            if (id.IsValid)
            {
                _selection.Add(id);
                AudioService.Instance?.PlayUiSelect();
            }
            RefreshSelection();
            RefreshVerbChrome();

            if (_tutorialBeat != null && id.IsValid && _scenario != null)
            {
                var unit = _scenario.FindUnit(id);
                _tutorialBeat.OnUnitSelected(unit != null && IsPlayerCommanded(unit));
            }
        }

        private void ClearSelection()
        {
            ClearWaypointDraft();
            _selection.Clear();
            RefreshSelection();
            RefreshVerbChrome();
        }

        private bool IsSelected(UnitId id) => _selection.Contains(id);

        /// <summary>
        /// The unit's live plan, read from its queue.
        ///
        /// Read, not reconstructed from the command stream — delivery rule 4. A shadow copy
        /// built by listening would be a second source of truth and would drift.
        /// </summary>
        /// <summary>The plan as the details card states it, where live figures are affordable.</summary>
        private string DescribePlanLive(UnitInstance unit)
        {
            _detailsLine = true;
            try { return DescribePlan(unit); }
            finally { _detailsLine = false; }
        }

        private string DescribePlan(UnitInstance unit)
        {
            var plan = BelievedPlanOf(unit);
            if (plan == null || plan.Count == 0) return "NO ORDERS";

            var head = plan[0];
            string more = plan.Count > 1 ? $"   (+{plan.Count - 1} QUEUED)" : string.Empty;
            return $"{head.Status.ToString().ToUpperInvariant()}: " +
                   $"{DescribeOrder(unit, head.Command)}{more}";
        }

        /// <summary>
        /// One order as a phrase, shared by the details line and the plan rows so the head of
        /// the plan cannot be described two different ways in the same panel.
        /// </summary>
        private string DescribeOrder(UnitInstance unit, in Command command) => command.Kind switch
        {
            CommandKind.MoveTo => $"MOVE TO {command.TargetCell.x:0},{command.TargetCell.y:0}",
            CommandKind.Engage => DescribeEngagement(unit, command.AgainstUnit),
            CommandKind.Defend => DescribeDefence(unit),
            CommandKind.Screen => DescribeScreen(unit),
            CommandKind.Guard => DescribeGuard(unit),
            CommandKind.Cover => DescribeCover(unit),
            CommandKind.Delay => "DELAYING",
            CommandKind.Withdraw => "WITHDRAWING",
            CommandKind.Attack => "ATTACKING",
            CommandKind.Recon => "RECONNING",
            CommandKind.Exploit => "EXPLOITING",
            CommandKind.Pursue => "PURSUING",
            _ => command.Kind.ToString().ToUpperInvariant(),
        };

        /// <summary>
        /// Shown instead of a plan for a unit the player cannot order. Without it, right-click
        /// doing nothing reads as a broken control rather than as a rule.
        /// </summary>
        private string DescribeCommandability(UnitInstance unit) =>
            unit.IsDestroyed ? $"   ·   DESTROYED AT T+{unit.DestroyedAtTick:0000}"
            : IsPlayerCommanded(unit) ? string.Empty
            : "   ·   NOT UNDER YOUR COMMAND";

        /// <summary>
        /// An engage order, with the range to the target and whether it is inside the weapon's
        /// envelope.
        /// </summary>
        /// <remarks>
        /// The range matters more than it looks. Engage does **not** advance to contact — a
        /// unit ordered to fire at something 3 km away with a 1200 m weapon sits there
        /// declaring intent for twenty minutes while nothing happens, and without this line
        /// the panel says "EXECUTING" the whole time. Naming the gap is the difference between
        /// a rule the player learns in one engagement and one they file as a bug.
        /// </remarks>
        private string DescribeEngagement(UnitInstance unit, UnitId against)
        {
            var target = _scenario?.FindUnit(against);
            if (target == null) return "ENGAGING";

            string who = string.IsNullOrEmpty(target.Designation)
                ? against.ToString() : target.Designation;

            if (_map == null) return $"ENGAGING {who}";

            float metres = Vector2.Distance(unit.Cell, target.Cell) * _map.Header.MetresPerCell;
            float envelope = unit.Capabilities(UnitCatalogue.Default()).EngagementRangeMetres;

            return metres > envelope
                ? $"ENGAGING {who}   ·   {metres:0} M   ·   OUT OF RANGE ({envelope:0} M)"
                : $"ENGAGING {who}   ·   {metres:0} M";
        }

        // ─── Plan card ────────────────────────────────────────────────────────

        private RectTransform _planRoot;

        /// <summary>
        /// What the listed rows were built from.
        /// </summary>
        /// <remarks>
        /// Rebuilding a row is destroy-and-recreate and <see cref="RefreshSelection"/> runs on
        /// every tick, so the rows are rebuilt only when the plan they describe has actually
        /// moved. Deliberately excludes <see cref="QueuedCommand.TicksExecuting"/>: it changes
        /// every single tick and no row displays it, so including it would rebuild the whole
        /// list — and destroy the button under the player's cursor — once a second.
        /// </remarks>
        private string _planKey;

        private void BuildPlanCard(Transform parent)
        {
            _planRoot = CreateRect("Plan", parent);
            var v = _planRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 2;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            _planRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>
        /// Two buttons on one line. Its own row rather than two entries in the rail's column,
        /// because the column stacks and a full-width HOLD under a full-width ABORT PLAN reads
        /// as a list of unrelated actions.
        /// </summary>
        private static RectTransform AddButtonRow(Transform parent)
        {
            var row = CreateRect("Buttons", parent);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 44;
            le.minHeight = 44;

            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            return row;
        }

        /// <summary>
        /// Identity of the listed plan: whose it is, and every entry a row draws.
        /// </summary>
        private string PlanKey(UnitInstance unit, IReadOnlyList<QueuedCommand> plan)
        {
            if (unit == null) return "none";

            var sb = new System.Text.StringBuilder();
            sb.Append(unit.Id.Value).Append(IsPlayerCommanded(unit) ? '+' : '-').Append('|');
            if (plan != null)
                for (int i = 0; i < plan.Count; i++)
                {
                    var c = plan[i].Command;
                    sb.Append((int)c.Kind).Append(':').Append((int)plan[i].Status).Append(':')
                      .Append(c.TargetCell.x.ToString("F1")).Append(',')
                      .Append(c.TargetCell.y.ToString("F1")).Append(':')
                      .Append(c.AgainstUnit.Value).Append(';');

                // Posture, because a defence changes what a row says when it finishes digging
                // in and that transition happens once rather than every tick. TicksExecuting
                // still stays out: a row showing a live percentage would rebuild the list —
                // and destroy the button under the pointer — once a second.
                sb.Append('p').Append((int)unit.Posture);
                }
            return sb.ToString();
        }

        /// <summary>
        /// Lists the selected unit's live plan, one row per queued order, with a cancel on each.
        /// </summary>
        /// <remarks>
        /// Read from <see cref="BelievedPlanOf"/> — the same accessor the map routes use — so
        /// the list and the arrows on the map can never disagree about what a unit is doing,
        /// and both become fog-of-war-aware in one edit rather than two.
        ///
        /// An enemy's plan is listed without cancels, matching the map: its route is already
        /// drawn there, and hiding it here while drawing it there would be an inconsistency
        /// rather than a rule.
        /// </remarks>
        private void RefreshPlan()
        {
            if (_planRoot == null) return;

            UnitInstance unit = null;
            if (_selection.Count > 0 && _scenario != null)
                unit = _scenario.FindUnit(_selection[0]);

            var plan = unit == null ? null : BelievedPlanOf(unit);
            int pendingCount = unit == null ? 0 : PendingCommandCountFor(unit.Id);

            // Folded into the row-rebuild guard (see PlanKey's own note) so a pending count that
            // moves — the player issues a second order while still paused — rebuilds the list,
            // even though the queue itself, and therefore PlanKey's own reading of it, has not
            // moved at all.
            string key = PlanKey(unit, plan) + "|P" + pendingCount;
            if (key == _planKey) return;
            _planKey = key;

            for (int i = _planRoot.childCount - 1; i >= 0; i--)
                Destroy(_planRoot.GetChild(i).gameObject);

            if (unit == null) { AddPlanNote("No unit selected."); return; }

            if (plan == null || plan.Count == 0)
                AddPlanNote("NO ORDERS");
            else
            {
                bool commandable = IsPlayerCommanded(unit);
                for (int i = 0; i < plan.Count; i++)
                    AddPlanRow(unit, i, plan[i], commandable);
            }

            // #59: an order issued for this unit does not enter its queue until the bus
            // delivers it next step, so nothing above this line changes the instant a paused
            // player gives one. This is the plan card's half of that signal — the clock line
            // carries the force-wide count.
            if (pendingCount > 0) AddPlanPendingNote(pendingCount);
        }

        /// <summary>
        /// Orders addressed to this unit that have been issued but not yet delivered — sitting
        /// on <see cref="Simulation.Bus"/>, not yet applied to its queue.
        /// </summary>
        /// <remarks>
        /// A read of <see cref="Messaging.MessageBus{T}.Pending"/>, which already exists for
        /// #74's snapshot — not a shadow copy of anything (delivery rule 4). The same list
        /// <see cref="Simulation.Step"/> hands to <c>OnCommandDelivered</c> next tick, so this
        /// can never disagree with what actually lands.
        /// </remarks>
        private int PendingCommandCountFor(UnitId id)
        {
            if (_sim == null) return 0;
            var pending = _sim.Bus.Pending;
            int n = 0;
            for (int i = 0; i < pending.Count; i++)
                if (pending[i].TargetUnit == id) n++;
            return n;
        }

        /// <summary>
        /// One row saying orders are in flight for the selected unit but have not landed.
        /// Styled apart from <see cref="AddPlanNote"/> (accent colour, an arrow) so it reads as
        /// "about to happen" rather than as the unit's current state.
        /// </summary>
        private void AddPlanPendingNote(int count)
        {
            var row = CreateRect("PlanPending", _planRoot);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;
            row.gameObject.AddComponent<Image>().color = Theme.CardBg;

            var t = CreateTmp("T", row,
                $"->  {count} ORDER{(count == 1 ? string.Empty : "S")} PENDING   ·   " +
                "TAKES EFFECT NEXT TICK",
                10, FontStyles.Italic, withLayout: false);
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(10, 0);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.color = Theme.Accent;
        }

        private void AddPlanNote(string text)
        {
            var row = CreateRect("PlanNote", _planRoot);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
            row.gameObject.AddComponent<Image>().color = Theme.CardBg;

            var t = CreateTmp("T", row, text, 11, FontStyles.Italic, withLayout: false);
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(10, 0);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.color = Theme.InkMuted;
        }

        private void AddPlanRow(UnitInstance unit, int index, in QueuedCommand entry,
            bool commandable)
        {
            var row = CreateRect($"Plan_{index}", _planRoot);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
            row.gameObject.AddComponent<Image>().color = Theme.CardBg;

            // The order under way is marked by a bar down the left rather than by colouring the
            // whole row: the rows carry a red cancel each, and a second tinted field behind it
            // makes the one destructive control on the row harder to pick out, not easier.
            var flag = CreateRect("Flag", row);
            flag.anchorMin = new Vector2(0, 0);
            flag.anchorMax = new Vector2(0, 1);
            flag.pivot = new Vector2(0, 0.5f);
            flag.sizeDelta = new Vector2(4, 0);
            flag.gameObject.AddComponent<Image>().color =
                entry.Status == CommandStatus.Executing ? Theme.Accent : Theme.CardLine;

            // Hyphens and middots only — the atlas renders an en dash as nothing at all.
            var t = CreateTmp("T", row,
                $"{index + 1}.   {entry.Status.ToString().ToUpperInvariant()}   ·   " +
                DescribeOrder(unit, entry.Command),
                11, FontStyles.Normal, withLayout: false);
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(12, 0);
            t.rectTransform.offsetMax = new Vector2(commandable ? -80 : -8, 0);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.color = Theme.Ink;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;

            if (!commandable) return;

            var btn = CreateRect("Cancel", row);
            btn.anchorMin = btn.anchorMax = new Vector2(1f, 0.5f);
            btn.pivot = new Vector2(1f, 0.5f);
            btn.sizeDelta = new Vector2(68, 20);
            btn.anchoredPosition = new Vector2(-6, 0);

            var img = btn.gameObject.AddComponent<Image>();
            img.color = Color.white;
            var button = btn.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.colors = AccentButtonColors(Theme.Alert);

            int captured = index;
            var target = unit.Id;
            button.onClick.AddListener(() => CancelPlanFrom(target, captured));

            var label = CreateTmp("T", btn, "CANCEL", 9, FontStyles.Bold, withLayout: false);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
            label.color = Theme.AccentText;
            label.characterSpacing = 2f;
        }

        /// <summary>
        /// Cuts the plan at one entry, issued as a command so it is logged and replayable.
        /// </summary>
        /// <remarks>
        /// CANCEL FROM is what the queue offers and what the button therefore means: entries
        /// before the chosen row are untouched and everything from it onward goes.
        /// There is no cancel-one-in-the-middle, because a plan is a sequence and removing a leg
        /// from the middle of a route silently rewrites the two legs either side of it.
        ///
        /// **Row 0 goes out as an Abort, not as CancelFrom.** The two do the same thing to
        /// the queue, but <c>Simulation.OnCommandDelivered</c> follows an Abort with a halt
        /// report and <c>ApplyAbortPosture</c> and follows a CancelFrom with neither — so
        /// cancelling a running move through CancelFrom leaves the unit standing still while
        /// still flagged <c>Posture.Moving</c>, which is a silent 1.25x to every round fired at
        /// it (<c>EngagementResolver.PostureFactor</c>).
        ///
        /// Later rows address <see cref="QueuedCommand.Ordinal"/>, not the live list index —
        /// Core CancelFrom is ordinal-keyed (#57), and multi-leg waypoint plans make a live
        /// index wrong as soon as an earlier entry completes between click and delivery.
        /// </remarks>
        private void CancelPlanFrom(UnitId id, int liveIndex)
        {
            if (_sim == null) return;
            var unit = _scenario?.FindUnit(id);
            if (unit == null || !IsPlayerCommanded(unit)) return;

            var plan = BelievedPlanOf(unit);
            if (plan == null || liveIndex < 0 || liveIndex >= plan.Count) return;

            var actor = ActorId.ForSide(unit.Side);
            IssuePlayer(liveIndex <= 0
                ? Command.Abort(actor, unit.Id)
                : Command.CancelFrom(actor, unit.Id, plan[liveIndex].Ordinal));

            RefreshSelection();
        }

        /// <summary>
        /// A defence, and how far along its preparation is.
        /// </summary>
        /// <remarks>
        /// The progress matters more than it looks. Digging in halves incoming fire
        /// (EngagementResolver.PostureFactor) and takes two minutes, so "holding" and "dug in"
        /// are materially different states and a player deciding whether to move a unit needs
        /// to know which one it is in. Without this the order reads as EXECUTING for the whole
        /// scenario and the benefit arrives invisibly.
        /// </remarks>
        /// <summary>
        /// True while the details card is composing its body, which is a text assignment
        /// refreshed every tick, as against the plan rows, which are GameObjects rebuilt only
        /// when <see cref="_planKey"/> moves. Live figures belong on the former.
        /// </summary>
        private bool _detailsLine;

        private string DescribeDefence(UnitInstance unit)
        {
            var q = _sim?.QueueOf(unit.Id);
            if (q == null || q.IsEmpty) return "HOLDING";

            if (unit.Posture == Posture.DugIn) return "HOLDING   ·   DUG IN";

            // A percentage only where it can actually keep up. The plan row is rebuilt from
            // _planKey and deliberately does not track TicksExecuting, so a figure there
            // would freeze at whatever it was when the row was built — which is exactly what
            // the first cut of this shipped: PREPARING 0% for the whole scenario.
            int percent = Mathf.Clamp(
                q[0].TicksExecuting * 100 / DefendExecutor.DigInTicks, 0, 99);
            return _detailsLine ? $"HOLDING   ·   PREPARING {percent}%" : "HOLDING   ·   PREPARING";
        }

        private static string DescribeScreen(UnitInstance unit) =>
            unit.Posture == Posture.Screening
                ? "SCREENING   ·   OBSERVING"
                : "SCREENING";

        private string DescribeGuard(UnitInstance unit)
        {
            var q = _sim?.QueueOf(unit.Id);
            if (q == null || q.IsEmpty) return "GUARDING";

            if (unit.Posture == Posture.Guarding) return "GUARDING   ·   DUG IN";

            int percent = Mathf.Clamp(
                q[0].TicksExecuting * 100 / DefendExecutor.DigInTicks, 0, 99);
            return _detailsLine ? $"GUARDING   ·   PREPARING {percent}%" : "GUARDING   ·   PREPARING";
        }

        private string DescribeCover(UnitInstance unit)
        {
            var q = _sim?.QueueOf(unit.Id);
            if (q == null || q.IsEmpty) return "COVERING";

            if (unit.Posture == Posture.Covering) return "COVERING   ·   DUG IN";

            int percent = Mathf.Clamp(
                q[0].TicksExecuting * 100 / DefendExecutor.DigInTicks, 0, 99);
            return _detailsLine ? $"COVERING   ·   PREPARING {percent}%" : "COVERING   ·   PREPARING";
        }

        private void RefreshSelection()
        {
            UnitInstance unit = null;
            if (_selection.Count > 0 && _scenario != null)
                unit = _scenario.FindUnit(_selection[0]);

            // The rail shows the selected unit's own setting. Suppressed so that following the
            // selection does not read as the player changing it.
            if (_roeDrop != null && unit != null)
            {
                _suppress = true;
                _roeDrop.value = (int)unit.Roe;
                _suppress = false;
            }

            RefreshDrillOptions();

            if (_detailsTitle != null)
            {
                if (unit == null)
                {
                    _detailsTitle.text = "NONE";
                    _detailsBody.text = "Click a unit on the map.";
                }
                else if (_sim != null && _sim.Hierarchy.IsFormation(unit.Id))
                {
                    DescribeFormation(unit);
                }
                else
                {
                    var side = _scenario.FindSide(unit.Side);
                    var caps = unit.Capabilities(UnitCatalogue.Default());
                    var code = unit.ToSidcCode();

                    int cx = Mathf.Clamp(Mathf.RoundToInt(unit.Cell.x), 0, _map.Width - 1);
                    int cy = Mathf.Clamp(Mathf.RoundToInt(unit.Cell.y), 0, _map.Height - 1);

                    _detailsTitle.text = string.IsNullOrEmpty(unit.Designation)
                        ? unit.Id.ToString()
                        : unit.Designation.ToUpperInvariant();

                    // Hyphens and middots only — the atlas renders an en dash as nothing.
                    _detailsBody.text =
                        $"{side?.Name ?? "?"}   ·   {DisplayNames.EchelonName(code.Echelon)}   ·   " +
                        $"{DisplayNames.UnitTypeLabel(code.EntityCode)}\n" +
                        $"{caps.Name}   ·   STR {unit.StrengthPercent}%   ·   " +
                        $"RDY {unit.Readiness:0}%   ·   EFF {unit.Effectiveness * 100f:0}%   ·   " +
                        // Training earns its place because it costs the player *time* they
                        // would otherwise blame on the engine: a green unit sits PENDING for
                        // a few ticks after being ordered, and with nothing on screen to
                        // explain it that reads as a dropped click. The hesitation is stated
                        // beside it, in the units the cost is actually paid in.
                        $"TRN {unit.Training:0}%\n" +
                        (unit.HesitationTicks > 0
                            ? $"HESITATES {unit.HesitationTicks} S BEFORE ACTING   ·   "
                            : "ACTS ON ORDERS AT ONCE   ·   ") +
                        $"{unit.Mgrs(_map)}   ·   {unit.Elevation(_map):0} M   ·   " +
                        $"{LandcoverInfo.DisplayName(unit.Landcover(_map)).ToUpperInvariant()}   ·   " +
                        $"SLOPE {_map.SampleSlopeDegrees(cx, cy):0} DEG\n" +
                        DescribePlanLive(unit) + DescribeCommandability(unit);
                }
            }

            RefreshPlan();
            LayOutMarkers();
            RefreshOrbatHighlight();
        }

        /// <summary>
        /// A formation's details, rolled up from what it owns.
        /// </summary>
        /// <remarks>
        /// **Never its own fields.** A formation's stored strength, readiness and position are
        /// meaningless — it is an aggregate — and reading them would show a battalion at full
        /// strength while its companies were destroyed. Everything here comes from
        /// UnitHierarchy, which is the single answer.
        ///
        /// It has no plan of its own either: an order to a formation decomposes at delivery
        /// and never sits in a queue, so what a formation is "doing" is what its subordinates
        /// were told to do.
        /// </remarks>
        private void DescribeFormation(UnitInstance unit)
        {
            var h = _sim.Hierarchy;
            var side = _scenario.FindSide(unit.Side);
            var code = unit.ToSidcCode();
            var subordinates = h.SubordinatesOf(unit.Id);

            _detailsTitle.text = string.IsNullOrEmpty(unit.Designation)
                ? unit.Id.ToString()
                : unit.Designation.ToUpperInvariant();

            var names = new System.Text.StringBuilder();
            for (int i = 0; i < subordinates.Count; i++)
            {
                if (i > 0) names.Append(",  ");
                names.Append(subordinates[i].Designation);
            }

            // Hyphens and middots only — the atlas renders an en dash as nothing.
            _detailsBody.text =
                $"{side?.Name ?? "?"}   ·   {DisplayNames.EchelonName(code.Echelon)}   ·   " +
                $"FORMATION\n" +
                $"STR {h.StrengthOf(unit.Id):0}%   ·   RDY {h.ReadinessOf(unit.Id):0}%   ·   " +
                $"{subordinates.Count} SUBORDINATE(S)\n" +
                $"{names}\n" +
                "ORDERS DECOMPOSE TO SUBORDINATES" + DescribeCommandability(unit);
        }

        // ─── Order tracks ─────────────────────────────────────────────────────

        /// <summary>
        /// The plan this side's commander believes a unit is following.
        ///
        /// Today it is simply the unit's queue, and the indirection looks pointless. It is not:
        /// under C3 (noise, latency, hijack) a commander's picture and the ground truth
        /// diverge — a unit may be executing a plan the commander does not know about, or
        /// sitting idle because a FRAGO never arrived — and at that point drawing the real
        /// queue is cheating. Routing every such read through one accessor named for the
        /// observer makes that a change here rather than a search through the view.
        ///
        /// See docs/command-architecture.md, "The consequence that breaks a rule".
        /// </summary>
        private IReadOnlyList<QueuedCommand> BelievedPlanOf(UnitInstance unit)
        {
            var q = _sim?.QueueOf(unit.Id);
            return q?.Entries;
        }

        /// <summary>
        /// Draws every unit's plan as a connected route, leg by leg.
        ///
        /// Read from the queue rather than reconstructed from the command stream — delivery
        /// rule 4. A shadow copy maintained by listening would be a second source of truth and
        /// would drift, which for a route means arrows pointing at places nobody is going.
        /// </summary>
        private void LayOutOrders()
        {
            if (_orders == null) return;
            _orders.Begin();

            if (_sim != null && _scenario != null)
            {
                foreach (var m in _markers)
                {
                    var plan = BelievedPlanOf(m.Unit);
                    if (plan == null || plan.Count == 0) continue;

                    var side = _scenario.FindSide(m.Unit.Side);
                    var colour = side?.Colour ?? Color.white;

                    // A route starts at the unit, not at the first waypoint, so the leg under
                    // way shortens as the unit closes on it.
                    Vector2 from = m.Unit.Cell;
                    Vector2 last = from;

                    for (int i = 0; i < plan.Count; i++)
                    {
                        var entry = plan[i];
                        if (entry.Command.Kind != CommandKind.MoveTo) continue;

                        Vector2 to = entry.Command.TargetCell;
                        bool executing = entry.Status == CommandStatus.Executing;
                        var tint = Tint(colour, executing);

                        // Draw the route the unit will walk, not a straight line. Executing
                        // legs use the live IRouteProvider cache; pending legs recompute via
                        // the same PlanCells chain (#54).
                        var route = executing ? _sim.RouteOf(m.Unit.Id) : null;
                        if (route == null || route.Count == 0)
                            route = PreviewRoute(m.Unit, from, to);

                        if (route != null && route.Count > 0)
                        {
                            Vector2 leg = from;
                            for (int r = 0; r < route.Count; r++)
                            {
                                var next = new Vector2(route[r].x, route[r].y);
                                _orders.AddLeg(_card, leg, next, tint, dashed: !executing);
                                leg = next;
                            }
                            if (Vector2.Distance(leg, to) > 0.05f)
                                _orders.AddLeg(_card, leg, to, tint, dashed: !executing);
                            from = to;
                        }
                        else
                        {
                            _orders.AddLeg(_card, from, to, tint, dashed: !executing);
                            from = to;
                        }

                        last = to;
                    }

                    // One arrowhead, at the end of the whole plan rather than on every leg —
                    // a head per waypoint reads as several separate orders.
                    if (last != m.Unit.Cell)
                        _orders.AddArrowhead(_card, from == last ? m.Unit.Cell : from, last,
                            Tint(colour, true));
                }

                LayOutWaypointDraft();
            }

            _orders.End();
        }

        /// <summary>Dashed pathfinder preview + handles for the open WAYPOINTS draft (#54).</summary>
        private void LayOutWaypointDraft()
        {
            if (_armedVerb != PaletteVerb.Waypoints || _waypointDraft.Count == 0) return;
            if (_selection.Count == 0 || _scenario == null) return;

            var unit = _scenario.FindUnit(_selection[0]);
            if (unit == null || !IsPlayerCommanded(unit)) return;

            var side = _scenario.FindSide(unit.Side);
            var colour = Tint(side?.Colour ?? Theme.Accent, false);

            Vector2 from = unit.Cell;
            Vector2 last = from;
            for (int i = 0; i < _waypointDraft.Count; i++)
            {
                Vector2 to = _waypointDraft[i];
                var route = PreviewRoute(unit, from, to);
                if (route != null && route.Count > 0)
                {
                    Vector2 leg = from;
                    for (int r = 0; r < route.Count; r++)
                    {
                        var next = new Vector2(route[r].x, route[r].y);
                        _orders.AddLeg(_card, leg, next, colour, dashed: true);
                        leg = next;
                    }
                    if (Vector2.Distance(leg, to) > 0.05f)
                        _orders.AddLeg(_card, leg, to, colour, dashed: true);
                }
                else
                {
                    _orders.AddLeg(_card, from, to, colour, dashed: true);
                }

                _orders.AddHandle(_card, to, Theme.Accent);
                from = to;
                last = to;
            }

            if (last != unit.Cell)
                _orders.AddArrowhead(_card, unit.Cell, last, Theme.Accent);
        }

        /// <summary>
        /// Same Find → Simplify → Smooth cells the executor will walk, for pending / draft
        /// preview. Null when unreachable.
        /// </summary>
        private IReadOnlyList<Vector2Int> PreviewRoute(UnitInstance unit, Vector2 from, Vector2 to)
        {
            var grid = PreviewGridFor(unit);
            if (grid == null) return null;

            var a = new Vector2Int(Mathf.RoundToInt(from.x), Mathf.RoundToInt(from.y));
            var b = new Vector2Int(Mathf.RoundToInt(to.x), Mathf.RoundToInt(to.y));
            return MoveToExecutor.PlanCells(grid, a, b);
        }

        private MovementGrid PreviewGridFor(UnitInstance unit)
        {
            if (_map == null || unit == null) return null;
            var caps = unit.Capabilities(UnitCatalogue.Default());
            if (caps == null) return null;

            if (_previewGrid != null && _previewCapsId == caps.Id && _previewGrid.Map == _map)
                return _previewGrid;

            _previewGrid = MoveToExecutor.GridFor(_map, caps);
            _previewCapsId = caps.Id;
            return _previewGrid;
        }

        /// <summary>
        /// Side colour, muted for a leg that has not started. The distinction is carried by
        /// dash versus solid; the tint only reinforces it, so it stays subtle.
        /// </summary>
        private static Color Tint(Color side, bool executing)
        {
            var c = executing ? side : Color.Lerp(side, Theme.InkMuted, 0.35f);
            c.a = executing ? 0.95f : 0.7f;
            return c;
        }

        // ─── Order of battle ──────────────────────────────────────────────────

        /// <summary>Row background per unit id, so the selected one can be picked out.</summary>
        private readonly Dictionary<int, Image> _orbatRows = new();

        private void RefreshOrbatHighlight()
        {
            foreach (var kv in _orbatRows)
            {
                if (kv.Value == null) continue;
                kv.Value.color = IsSelected(new UnitId(kv.Key))
                    ? UiTheme.SelectFill
                    : UiTheme.CardBg;
            }
        }

        private void BuildOrbat()
        {
            for (int i = _orbatRoot.childCount - 1; i >= 0; i--)
                Destroy(_orbatRoot.GetChild(i).gameObject);
            _orbatRows.Clear();
            if (_scenario == null) return;

            var catalogue = UnitCatalogue.Default();

            foreach (var side in _scenario.Sides)
            {
                var head = CreateRect($"Side_{side.Id}", _orbatRoot);
                head.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
                head.gameObject.AddComponent<Image>().color = Theme.SectionBg;

                // A side's own colour, not its affiliation's — two allied contingents share
                // an affiliation and still need telling apart.
                var swatch = CreateRect("Swatch", head);
                swatch.anchorMin = new Vector2(0, 0);
                swatch.anchorMax = new Vector2(0, 1);
                swatch.pivot = new Vector2(0, 0.5f);
                swatch.sizeDelta = new Vector2(6, 0);
                swatch.gameObject.AddComponent<Image>().color = side.Colour;

                var t = CreateTmp("T", head,
                    $"{side.Name.ToUpperInvariant()}   ·   {FightingUnitsOf(side.Id)} UNITS",
                    12, FontStyles.Bold, withLayout: false);
                Stretch(t.rectTransform);
                t.rectTransform.offsetMin = new Vector2(14, 0);
                t.alignment = TextAlignmentOptions.MidlineLeft;
                t.color = Theme.Ink;
                t.characterSpacing = 2f;

                // Walked as a tree, root first, so the list reads as an order of battle
                // rather than an alphabetised inventory. Depth is the indent.
                foreach (var unit in _sim.Hierarchy.Roots)
                    if (unit.Side == side.Id) AddOrbatRow(unit, catalogue);
            }
        }

        /// <summary>How many fighting units a side has. Formations are not troops.</summary>
        private int FightingUnitsOf(SideId side)
        {
            int n = 0;
            foreach (var u in _sim.Units) if (u.Side == side && !u.IsDestroyed) n++;
            return n;
        }

        /// <summary>
        /// One ORBAT row and, beneath it, its subordinates.
        /// </summary>
        /// <remarks>
        /// A formation's numbers are **rolled up**, never read from its own fields — those are
        /// meaningless on a unit that owns others, and reading them would show a battalion at
        /// full strength while its companies were being destroyed.
        ///
        /// Formations are not selectable here. Selection drives the details card and the map
        /// brackets, both of which assume something that stands somewhere and fights; a
        /// formation does neither. Ordering a formation works in Core — an order to one
        /// decomposes to its subordinates — but giving it a UI path belongs with the mission
        /// orders in #72, not here.
        /// </remarks>
        private void AddOrbatRow(UnitInstance unit, UnitCatalogue catalogue)
        {
            var h = _sim.Hierarchy;
            bool formation = h.IsFormation(unit.Id);
            int depth = h.DepthOf(unit.Id);
            float indent = 14f + depth * 14f;

            var row = CreateRect($"Unit_{unit.Id}", _orbatRoot);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = formation ? 30 : 34;

            var rowImg = row.gameObject.AddComponent<Image>();
            rowImg.color = formation ? UiTheme.RowStripe : UiTheme.CardBg;

            // Selectable whether or not it is a formation. A formation cannot be clicked on
            // the map — it has no marker, because its symbol would stand on top of its own
            // subordinates — so the ORBAT is the only way to reach one, and reaching one is
            // the point: an order to a battalion decomposes to its companies.
            {
                // Selectable from the list as well as the map. Cheap, and the list is the
                // only way to reach a unit that is currently cropped off the sheet.
                var rowBtn = row.gameObject.AddComponent<Button>();
                rowBtn.targetGraphic = rowImg;
                var rowColors = ColorBlock.defaultColorBlock;
                rowColors.normalColor = Color.white;
                rowColors.highlightedColor = new Color(0.94f, 0.94f, 0.90f);
                rowColors.pressedColor = new Color(0.88f, 0.90f, 0.86f);
                rowColors.selectedColor = Color.white;
                rowColors.fadeDuration = 0.05f;
                rowBtn.colors = rowColors;

                var captured = unit.Id;
                rowBtn.onClick.AddListener(() => Select(captured));
                _orbatRows[captured.Value] = rowImg;
            }

            var name = CreateTmp("N", row,
                string.IsNullOrEmpty(unit.Designation) ? unit.Id.ToString() : unit.Designation,
                formation ? 11 : 12, FontStyles.Bold, withLayout: false);
            name.rectTransform.anchorMin = new Vector2(0, 0.5f);
            name.rectTransform.anchorMax = new Vector2(1, 1f);
            name.rectTransform.offsetMin = new Vector2(indent, 0);
            name.rectTransform.offsetMax = new Vector2(-8, 0);
            name.alignment = TextAlignmentOptions.MidlineLeft;
            name.color = formation ? Theme.Accent : Theme.Ink;

            // Composition only, no position. The order of battle is built once and units
            // move, so anything positional here would be the load-time value presented as
            // current -- and it was, reading three grid squares behind the details panel
            // while a unit was under way. Live position belongs in the details panel, which
            // is refreshed on every tick.
            string detailText = formation
                ? $"{DisplayNames.EchelonName(unit.ToSidcCode().Echelon)}   ·   " +
                  $"{h.SubordinatesOf(unit.Id).Count} SUBORDINATE(S)   ·   " +
                  $"STR {h.StrengthOf(unit.Id):0}%"
                : unit.IsDestroyed
                    ? $"{unit.Capabilities(catalogue).Name}   ·   " +
                      $"DESTROYED T+{unit.DestroyedAtTick:0000}"
                    : $"{unit.Capabilities(catalogue).Name}   ·   STR {unit.StrengthPercent}%";

            var detail = CreateTmp("D", row, detailText, 10, FontStyles.Normal,
                withLayout: false);
            detail.rectTransform.anchorMin = new Vector2(0, 0f);
            detail.rectTransform.anchorMax = new Vector2(1, 0.5f);
            detail.rectTransform.offsetMin = new Vector2(indent, 0);
            detail.rectTransform.offsetMax = new Vector2(-8, 0);
            detail.alignment = TextAlignmentOptions.MidlineLeft;
            detail.color = Theme.InkMuted;

            foreach (var child in h.SubordinatesOf(unit.Id)) AddOrbatRow(child, catalogue);
        }
    }
}
