// TtpView.cs
// The drill binder: the TTP library as a field manual you thumb through rather than a list
// you search.
//
// WHY A BINDER AND NOT A TABLE. The codes are only worth having if they become learnable —
// the whole argument for code-addressed drills is that fluency is the skill curve, and a
// player who searches every time never becomes fluent. Thumbing is how the codes either side
// of the one you wanted get learned by accident, and that incidental learning is the feature.
// See #61.
//
// A READER, NOT AN EDITOR. Authoring drills is Phase 5.4 and invoking them is #53; neither is
// here. This view answers "what does 2 mean" and nothing else, which is the question a player
// has while the palette does not exist yet.
//
// THE PAGE IS HONEST ABOUT WHAT THE ENGINE CAN DO. Each step is marked with whether an
// executor exists for it, and most do not — `MoveTo` and `Engage` are the only world commands
// there are. Showing that is deliberate: a binder full of drills the simulation cannot carry
// out, presented as though it could, would misrepresent the game to the player and would let
// the gap go unnoticed in development. The symbol library makes the same choice when it
// captions four entity codes FRAME ONLY.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Strategos.Doctrine;
using Strategos.FieldManual;
using Strategos.NatoSymbols;

using Theme = Strategos.UI.UiTheme;
using static Strategos.UI.UiFactory;

namespace Strategos.UI.Views
{
    public sealed class TtpView : MonoBehaviour, IAppView
    {
        /// <summary>
        /// Page bake size, in texture pixels. Fixed rather than following the card, so the
        /// paper is generated once per drill instead of on every window resize — a resize is
        /// continuous and a bake is half a million pixels.
        /// </summary>
        private const int PageWidth = 1152;
        private const int PageHeight = 768;

        /// <summary>
        /// On-screen page height, in reference-resolution px.
        /// </summary>
        /// <remarks>
        /// Capped rather than fitted to the stage. A page that grows with the window ends up as
        /// a vast sheet with a dozen lines huddled at the top, which reads as an unfinished
        /// page rather than a spacious one — which is exactly what the first build looked like.
        /// Also the divisor the reserved band is computed against, so the two stay in step.
        /// </remarks>
        private const float PageDisplayHeight = 640f;

        /// <summary>
        /// The band of the page text occupies, as fractions of its height.
        /// </summary>
        /// <remarks>
        /// Shared by the reserved rect passed to <see cref="PaperTexture"/> and the anchors of
        /// the text column, so the two cannot drift. Reserving the *column* rather than each
        /// line is what keeps this simple: the rects are known before any layout runs, where
        /// per-line rects would need TMP to have measured itself first, and a stain would then
        /// depend on a layout pass having settled.
        ///
        /// Text never reaches the bottom quarter, so that is where the stains end up — which is
        /// where they look right anyway.
        /// </remarks>
        /// <summary>Gap between lines in the page column. Counted into the content height.</summary>
        private const float ColumnSpacing = 4f;

        /// <summary>Height of one numbered step row.</summary>
        private const float StepHeight = 28f;

        private const float TextBottom = 0.10f;
        private const float TextTop = 0.93f;
        private const float TextInsetX = 0.06f;

        private RectTransform _pageRoot;
        private RectTransform _textColumn;
        private RawImage _paper;
        private RectTransform _figurePage;
        private RawImage _figurePaper;
        private RectTransform _indexRoot;
        private RectTransform _readinessRoot;
        private AppSession _session;

        private Ttp _current;

        /// <summary>
        /// One baked sheet per drill, so a page keeps the same coffee ring every time it is
        /// opened. **This view owns these and destroys them** — see docs/ui-invariants.md;
        /// they are not `AppSession.Symbols` sprites and the rule is the opposite one.
        /// </summary>
        private readonly Dictionary<string, Texture2D> _pages = new();

        private readonly Dictionary<string, Image> _indexRows = new();

        /// <summary>Facing-page sheets, one per drill. Owned and destroyed here, like _pages.</summary>
        private readonly Dictionary<string, Texture2D> _figures = new();

        public string Title => "DRILLS";
        public string Key => "ttp";

        /// <summary>Read-only here: the binder rates units, it never orders them.</summary>
        public AppSession Session { set => _session = value; }

        // ─── IAppView ─────────────────────────────────────────────────────────

        public void Build(RectTransform host)
        {
            BuildUi(host);
            var all = TtpLibrary.All;
            if (all.Count > 0) Select(all[0]);
            Debug.Log($"[TtpView] {all.Count} drill(s) in the library");
        }

        public void OnShown() { }

        public void OnHidden() => HideDropdownsIn(transform);

        private void OnDestroy()
        {
            foreach (var kv in _pages) if (kv.Value != null) Destroy(kv.Value);
            foreach (var kv in _figures) if (kv.Value != null) Destroy(kv.Value);
            _pages.Clear();
            _figures.Clear();
        }

        // ─── UI ───────────────────────────────────────────────────────────────

        private void BuildUi(RectTransform host)
        {
            var root = CreateRect("Root", host);
            Stretch(root);
            root.gameObject.AddComponent<Image>().color = Theme.StageBg;

            var h = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;   // keep the fixed rail off the surplus
            h.childForceExpandHeight = true;

            BuildStage(root);
            BuildRail(root);
        }

        private void BuildStage(Transform root)
        {
            var stage = CreateRect("Stage", root);
            var le = stage.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minWidth = 420f;

            var v = stage.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(24, 24, 18, 18);
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.childAlignment = TextAnchor.MiddleCenter;

            // A SPREAD, NOT A STACK. A binder open on a desk shows two facing pages, and
            // text and figure are wanted at the same time — a drill card is consulted
            // mid-decision, so making the reader flip between what a drill is and what it
            // looks like costs exactly the glance the codes exist to save.
            var spread = CreateRect("Spread", stage);
            var sle = spread.gameObject.AddComponent<LayoutElement>();
            sle.preferredHeight = PageDisplayHeight;
            sle.flexibleHeight = 0f;

            var sh = spread.gameObject.AddComponent<HorizontalLayoutGroup>();
            sh.spacing = 10;
            sh.childControlWidth = true;
            sh.childControlHeight = true;
            sh.childForceExpandWidth = false;
            sh.childForceExpandHeight = true;
            sh.childAlignment = TextAnchor.MiddleCenter;

            _pageRoot = MakePage(spread, "Page", out _paper);
            _figurePage = MakePage(spread, "Figure", out _figurePaper);

            _textColumn = CreateRect("Text", _pageRoot);
            _textColumn.anchorMin = new Vector2(TextInsetX, TextBottom);
            _textColumn.anchorMax = new Vector2(1f - TextInsetX, TextTop);
            _textColumn.offsetMin = Vector2.zero;
            _textColumn.offsetMax = Vector2.zero;

            var tv = _textColumn.gameObject.AddComponent<VerticalLayoutGroup>();
            tv.spacing = ColumnSpacing;
            tv.childControlWidth = true;
            tv.childControlHeight = true;
            tv.childForceExpandWidth = true;
            tv.childForceExpandHeight = false;
            tv.childAlignment = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// One sheet of the spread: aspect-locked, its own paper, sized by the row.
        /// </summary>
        /// <remarks>
        /// Aspect-locked rather than stretched. A page stretched to whatever the window
        /// happens to be stops reading as paper, and the grain would be scaled differently on
        /// each axis — the same reason MapSheetCard crops instead of stretching.
        /// </remarks>
        private static RectTransform MakePage(Transform parent, string name, out RawImage paper)
        {
            var holder = CreateRect($"{name}Holder", parent);
            var le = holder.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = PageDisplayHeight * (PageWidth / (float)PageHeight);
            le.preferredHeight = PageDisplayHeight;
            le.flexibleWidth = 0f;

            var page = CreateRect(name, holder);
            Stretch(page);

            var fitter = page.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = PageWidth / (float)PageHeight;

            paper = page.gameObject.AddComponent<RawImage>();
            paper.color = Color.white;
            return page;
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
            var cl = CreateTmp("L", chrome, "BATTLE DRILLS", 14, FontStyles.Bold,
                withLayout: false);
            Stretch(cl.rectTransform);
            cl.alignment = TextAlignmentOptions.Center;
            cl.color = Theme.AccentText;
            cl.characterSpacing = 6f;

            var content = UiScroll.CreateColumn("Scroll", panel, Theme.RailBg, out var scroll);
            var srt = (RectTransform)scroll.transform;
            srt.offsetMin = new Vector2(2, 0);
            srt.offsetMax = new Vector2(0, -38);

            AddSection(content, "INDEX");
            var hint = CreateTmp("Hint", content,
                "The code is what a commander transmits.  Drills are read here and given" +
                "\nfrom the command palette.",
                10, FontStyles.Italic);
            hint.color = Theme.InkMuted;
            hint.GetComponent<LayoutElement>().preferredHeight = 30;

            _indexRoot = CreateRect("Index", content);
            var iv = _indexRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            iv.spacing = 2;
            iv.childControlWidth = true;
            iv.childControlHeight = true;
            iv.childForceExpandWidth = true;
            iv.childForceExpandHeight = false;
            _indexRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            BuildIndex();

            AddSection(content, "READINESS");
            var rhint = CreateTmp("RHint", content,
                "T trained  ·  P needs practice  ·  U untrained." +
                "\nRated against the force loaded in PLAY.",
                10, FontStyles.Italic);
            rhint.color = Theme.InkMuted;
            rhint.GetComponent<LayoutElement>().preferredHeight = 30;

            _readinessRoot = CreateRect("Readiness", content);
            var rv = _readinessRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            rv.spacing = 2;
            rv.childControlWidth = true;
            rv.childControlHeight = true;
            rv.childForceExpandWidth = true;
            rv.childForceExpandHeight = false;
            _readinessRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
        }

        // ─── Readiness ────────────────────────────────────────────────────────

        /// <summary>
        /// How the loaded force rates against the open drill.
        /// </summary>
        /// <remarks>
        /// A drill is something a unit *trains on*, so "can this unit actually do 1A right
        /// now" is the question that turns the binder from a glossary into a readiness view —
        /// which is a real command-post function rather than a flourish.
        ///
        /// Read from the live simulation through <see cref="AppSession.Simulation"/>, not from
        /// a second copy of the scenario. A copy would be frozen at load time, so a unit shot
        /// to pieces would still read as trained — the mistake the ORBAT list already made
        /// once by showing load-time positions while a unit was under way.
        /// </remarks>
        private void RefreshReadiness(Ttp drill)
        {
            if (_readinessRoot == null) return;

            for (int i = _readinessRoot.childCount - 1; i >= 0; i--)
                Destroy(_readinessRoot.GetChild(i).gameObject);

            var sim = _session?.Simulation;
            if (sim == null)
            {
                // Honest empty state. A player who has never opened PLAY has no force to
                // rate, and inventing one would be worse than saying so.
                AddReadinessNote("No force loaded.  Open PLAY first.");
                return;
            }

            foreach (var unit in sim.Units)
                AddReadinessRow(drill, unit);
        }

        private void AddReadinessNote(string text)
        {
            var row = CreateRect("RNote", _readinessRoot);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
            row.gameObject.AddComponent<Image>().color = Theme.CardBg;

            var t = CreateTmp("T", row, text, 11, FontStyles.Italic, withLayout: false);
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(10, 0);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.color = Theme.InkMuted;
        }

        private void AddReadinessRow(Ttp drill, Strategos.Units.UnitInstance unit)
        {
            var assessment = TtpReadiness.Assess(drill, unit);

            var row = CreateRect($"R_{unit.Id}", _readinessRoot);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
            row.gameObject.AddComponent<Image>().color = Theme.CardBg;

            // The rating in a filled chip, because T/P/U is the thing being scanned down the
            // column and a letter alone at this size does not carry across a rail.
            var chip = CreateRect("Chip", row);
            chip.anchorMin = chip.anchorMax = new Vector2(0f, 0.5f);
            chip.pivot = new Vector2(0f, 0.5f);
            chip.sizeDelta = new Vector2(26, 20);
            chip.anchoredPosition = new Vector2(10, 0);
            chip.gameObject.AddComponent<Image>().color = assessment.Rating switch
            {
                DrillRating.Trained => Theme.Accent,
                DrillRating.Practice => Theme.InkMuted,
                _ => Theme.Alert,
            };

            var code = CreateTmp("C", chip, assessment.Code, 12, FontStyles.Bold,
                withLayout: false);
            Stretch(code.rectTransform);
            code.alignment = TextAlignmentOptions.Center;
            code.color = Theme.AccentText;

            var name = CreateTmp("N", row,
                string.IsNullOrEmpty(unit.Designation) ? unit.Id.ToString() : unit.Designation,
                11, FontStyles.Bold, withLayout: false);
            name.rectTransform.anchorMin = new Vector2(0, 0.45f);
            name.rectTransform.anchorMax = new Vector2(1, 1f);
            name.rectTransform.offsetMin = new Vector2(44, 0);
            name.rectTransform.offsetMax = new Vector2(-8, 0);
            name.alignment = TextAlignmentOptions.MidlineLeft;
            name.color = Theme.Ink;

            var why = CreateTmp("W", row, assessment.Reason, 9, FontStyles.Normal,
                withLayout: false);
            why.rectTransform.anchorMin = new Vector2(0, 0f);
            why.rectTransform.anchorMax = new Vector2(1, 0.45f);
            why.rectTransform.offsetMin = new Vector2(44, 0);
            why.rectTransform.offsetMax = new Vector2(-8, 0);
            why.alignment = TextAlignmentOptions.MidlineLeft;
            why.color = Theme.InkMuted;
            why.textWrappingMode = TextWrappingModes.NoWrap;
            why.overflowMode = TextOverflowModes.Ellipsis;
        }

        private void BuildIndex()
        {
            foreach (var drill in TtpLibrary.All)
            {
                var row = CreateRect($"Drill_{drill.Code}", _indexRoot);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;

                var img = row.gameObject.AddComponent<Image>();
                img.color = Theme.CardBg;
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                var colors = ColorBlock.defaultColorBlock;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.94f, 0.94f, 0.90f);
                colors.pressedColor = new Color(0.88f, 0.90f, 0.86f);
                colors.selectedColor = Color.white;
                colors.fadeDuration = 0.05f;
                btn.colors = colors;

                var captured = drill;
                btn.onClick.AddListener(() => Select(captured));
                _indexRows[drill.Code] = img;

                // The code set apart from the name, because the code is the thing being
                // learned and the name is the gloss on it.
                var code = CreateTmp("C", row, drill.Code, 15, FontStyles.Bold,
                    withLayout: false);
                code.rectTransform.anchorMin = new Vector2(0, 0);
                code.rectTransform.anchorMax = new Vector2(0, 1);
                code.rectTransform.pivot = new Vector2(0, 0.5f);
                code.rectTransform.sizeDelta = new Vector2(52, 0);
                code.rectTransform.anchoredPosition = new Vector2(14, 0);
                code.alignment = TextAlignmentOptions.MidlineLeft;
                code.color = Theme.Accent;
                code.characterSpacing = 2f;

                var name = CreateTmp("N", row, drill.Name, 12, FontStyles.Bold,
                    withLayout: false);
                name.rectTransform.anchorMin = new Vector2(0, 0.45f);
                name.rectTransform.anchorMax = new Vector2(1, 1f);
                name.rectTransform.offsetMin = new Vector2(72, 0);
                name.rectTransform.offsetMax = new Vector2(-10, 0);
                name.alignment = TextAlignmentOptions.MidlineLeft;
                name.color = Theme.Ink;

                var sub = CreateTmp("S", row,
                    $"{drill.EchelonName.ToUpperInvariant()}   ·   {drill.Steps.Length} STEPS" +
                    $"   ·   {drill.MechanisedSteps} EXECUTABLE",
                    9, FontStyles.Normal, withLayout: false);
                sub.rectTransform.anchorMin = new Vector2(0, 0f);
                sub.rectTransform.anchorMax = new Vector2(1, 0.45f);
                sub.rectTransform.offsetMin = new Vector2(72, 0);
                sub.rectTransform.offsetMax = new Vector2(-10, 0);
                sub.alignment = TextAlignmentOptions.MidlineLeft;
                sub.color = Theme.InkMuted;
            }
        }

        // ─── The page ─────────────────────────────────────────────────────────

        private void Select(Ttp drill)
        {
            _current = drill;

            // Page before paper: BuildPage accumulates how tall the content actually is, and
            // that is what decides how much of the sheet has to be held clear of stains.
            BuildPage(drill);
            _paper.texture = PageFor(drill, _contentHeight);
            _figurePaper.texture = FigureFor(drill);
            RefreshReadiness(drill);

            foreach (var kv in _indexRows)
                if (kv.Value != null)
                    kv.Value.color = kv.Key == drill.Code ? Theme.SelectFill : Theme.CardBg;
        }

        /// <summary>
        /// The sheet this drill is printed on, baked once and kept.
        /// </summary>
        /// <remarks>
        /// Seeded from the code, so a drill always has the same marks. That is what makes a
        /// page recognisable before it has been read — the same thing that lets you find a
        /// page in a real manual by the state of it — and a sheet that reshuffled on every
        /// visit would read as a rendering bug.
        /// </remarks>
        private Texture2D PageFor(Ttp drill, float contentHeight)
        {
            if (_pages.TryGetValue(drill.Code, out var cached) && cached != null) return cached;

            // Reserve what the text actually covers, not the whole column.
            //
            // Reserving the column was the first attempt and it suppressed every stain on the
            // page: the band ran the full height, the two rings both landed inside it and were
            // rejected, and the result was a clean sheet with an aged-paper generator behind
            // it doing nothing. Measuring the content instead leaves the lower third of the
            // sheet free, which is where a mug would have been put down anyway.
            const float pad = 0.02f;
            float used = Mathf.Clamp01(contentHeight / PageDisplayHeight);
            float top = TextTop + pad;
            float bottom = Mathf.Max(0f, TextTop - used - pad);

            var reserved = new List<RectInt>
            {
                new(
                    Mathf.RoundToInt(PageWidth * (TextInsetX - pad)),
                    Mathf.RoundToInt(PageHeight * bottom),
                    Mathf.RoundToInt(PageWidth * (1f - 2f * (TextInsetX - pad))),
                    Mathf.RoundToInt(PageHeight * (top - bottom))),
            };

            var tex = PaperTexture.Create(PageWidth, PageHeight,
                PaperTexture.SeedFor(drill.Code), PaperOptions.Used, reserved);
            _pages[drill.Code] = tex;
            return tex;
        }

        /// <summary>
        /// Height of the last page built, in reference px. Accumulated by AddLine and AddStep
        /// as they go rather than recomputed from the drill, so the figure the reserve uses is
        /// by construction the one the layout produced.
        /// </summary>
        private float _contentHeight;

        /// <summary>
        /// The facing sheet: paper with the drill's figure drawn into it.
        /// </summary>
        /// <remarks>
        /// Composited into the paper rather than laid over it as a second image. A figure with
        /// its own transparent texture would need another RawImage, its own disposal and its
        /// own resize path, and would sit *on* the page rather than being printed on it — the
        /// grain would run under the ink instead of through it.
        ///
        /// Seeded differently from the text page so the two sheets of a spread are not twins;
        /// two identical stain patterns side by side read as a tiling bug.
        /// </remarks>
        private Texture2D FigureFor(Ttp drill)
        {
            if (_figures.TryGetValue(drill.Code, out var cached) && cached != null) return cached;

            var area = new RectInt(
                Mathf.RoundToInt(PageWidth * 0.08f), Mathf.RoundToInt(PageHeight * 0.10f),
                Mathf.RoundToInt(PageWidth * 0.84f), Mathf.RoundToInt(PageHeight * 0.80f));

            var tex = PaperTexture.Create(PageWidth, PageHeight,
                PaperTexture.SeedFor(drill.Code + "/figure"), PaperOptions.Used,
                new List<RectInt> { area });

            var px = tex.GetPixels32();

            if (drill.Diagram != null)
            {
                TtpDiagramRenderer.Render(drill.Diagram, px, PageWidth, PageHeight, area,
                    C32(Theme.Ink), C32(Theme.Alert), C32(Theme.InkMuted));
            }
            else
            {
                // Said, not left blank. A drill with no figure is legitimate — some geometry
                // is a line and a sentence says more — but an empty sheet reads as a failure
                // to draw one.
                ProceduralDrawUtil.DrawText(px, PageWidth, PageHeight,
                    PageWidth / 2, PageHeight / 2, "NO FIGURE FOR THIS DRILL",
                    C32(Theme.InkMuted), 3, TextAlign.Center);
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);

            _figures[drill.Code] = tex;
            return tex;
        }

        private static Color32 C32(Color c) => new(
            (byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f),
            (byte)(Mathf.Clamp01(c.b) * 255f), 255);

        private void BuildPage(Ttp drill)
        {
            for (int i = _textColumn.childCount - 1; i >= 0; i--)
                Destroy(_textColumn.GetChild(i).gameObject);

            _contentHeight = 0f;

            AddLine($"{drill.Code}   {drill.Name.ToUpperInvariant()}", 24, FontStyles.Bold,
                Theme.Ink, 34, spacingAfter: 2);
            AddLine(drill.EchelonName.ToUpperInvariant(), 11, FontStyles.Bold, Theme.Accent, 18,
                spacingAfter: 12);

            AddLine(drill.Summary, 14, FontStyles.Normal, Theme.Ink, 24, spacingAfter: 4);

            // Binder → glossary (#207): terms that cite this drill code.
            var glossary = GlossaryIO.Load(GlossaryIO.DefaultPackName);
            var linked = GlossaryIO.TermsForDrill(glossary, drill.Code);
            if (linked != null && linked.Length > 0)
            {
                var titles = new System.Text.StringBuilder("FIELD MANUAL: ");
                for (int i = 0; i < linked.Length; i++)
                {
                    if (i > 0) titles.Append(" · ");
                    titles.Append(string.IsNullOrEmpty(linked[i].Title)
                        ? linked[i].Id
                        : linked[i].Title);
                }
                AddLine(titles.ToString(), 12, FontStyles.Bold, Theme.Accent, 20,
                    spacingAfter: 2);
                // One body is enough for the first link — keeps the page from ballooning.
                var body = linked[0].Body ?? "";
                if (body.Length > 160) body = body.Substring(0, 157) + "...";
                AddLine(body, 12, FontStyles.Italic, Theme.InkMuted, 36, spacingAfter: 10);
            }

            // Hyphens and middots only — the atlas renders an en dash as nothing at all.
            AddLine($"NOT WHEN:  {drill.NotWhen}", 12, FontStyles.Italic, Theme.Alert, 22,
                spacingAfter: 14);

            for (int i = 0; i < drill.Steps.Length; i++)
                AddStep(i + 1, drill.Steps[i]);

            AddLine(string.Empty, 8, FontStyles.Normal, Theme.InkMuted, 10, spacingAfter: 6);
            AddLine(
                $"{drill.MechanisedSteps} of {drill.Steps.Length} steps have an executor. " +
                "The rest are doctrine\nthe simulation does not model yet.",
                10, FontStyles.Italic, Theme.InkMuted, 26);
        }

        private void AddLine(string text, float size, FontStyles style, Color colour,
            float height, float spacingAfter = 0f)
        {
            var tmp = CreateTmp("L", _textColumn, text, size, style, withLayout: false);
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.color = colour;
            tmp.textWrappingMode = TextWrappingModes.Normal;

            var le = tmp.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height + spacingAfter;
            le.flexibleHeight = 0f;

            _contentHeight += height + spacingAfter + ColumnSpacing;
        }

        /// <summary>
        /// One numbered step, with a bar showing whether the engine can carry it out.
        /// </summary>
        /// <remarks>
        /// A bar rather than a tick or a bullet: the bundled atlas has no geometric-shape
        /// glyphs and would render either as a tofu box. Same reason the dropdown arrow and
        /// the selection brackets are drawn rather than typed.
        /// </remarks>
        private void AddStep(int number, in TtpStep step)
        {
            var row = CreateRect($"Step_{number}", _textColumn);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = StepHeight;
            le.flexibleHeight = 0f;
            _contentHeight += StepHeight + ColumnSpacing;

            var bar = CreateRect("Bar", row);
            bar.anchorMin = new Vector2(0, 0.15f);
            bar.anchorMax = new Vector2(0, 0.85f);
            bar.pivot = new Vector2(0, 0.5f);
            bar.sizeDelta = new Vector2(4, 0);
            var barImg = bar.gameObject.AddComponent<Image>();
            barImg.color = step.IsMechanised ? Theme.Accent : Theme.CardLine;

            var tmp = CreateTmp("T", row,
                $"{number}.   {step.Text.ToUpperInvariant()}", 13,
                step.IsMechanised ? FontStyles.Normal : FontStyles.Italic, withLayout: false);
            Stretch(tmp.rectTransform);
            tmp.rectTransform.offsetMin = new Vector2(14, 0);
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = step.IsMechanised ? Theme.Ink : Theme.InkMuted;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}
