// SymbolBuilderPanel.cs
// Light "operations map" presentation:
//   left  — topo underlay, composed symbol, SIDC, digit-by-digit breakdown table
//   right — light control rail with bordered selectors
// All text/background pairs are chosen for >= 7:1 contrast (WCAG AAA).
//
// The underlay is a real generated map: Strategos.Maps generates the terrain and
// MapRasterizer draws the sheet the symbol sits on. It used to be five Gaussian
// hills with fake isolines, which is where the Topographic palette's paper and
// contour colours came from before they were promoted into MapPalette.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Strategos.Maps;
using Strategos.NatoSymbols;
using Strategos.UI;

// The theme, widget kit and table helpers used to be private members of this class.
// They now live in Strategos.UI so other views can match this one; these aliases keep
// every call site below reading exactly as it did when they were members.
using Theme = Strategos.UI.UiTheme;
using TableRow = Strategos.UI.UiTableRow;
using static Strategos.UI.UiFactory;

namespace Strategos.Demo
{
    public class SymbolBuilderPanel : MonoBehaviour
    {
        [Header("Optional")]
        [SerializeField] private NatoSymbolDatabase _database;
        [SerializeField] private int _previewSize = 384;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInDemoScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != "SymbolDemo") return;
            if (FindAnyObjectByType<SymbolBuilderPanel>() != null) return;

            var spawner = FindAnyObjectByType<SymbolDemoSpawner>(FindObjectsInactive.Include);
            if (spawner != null) spawner.gameObject.SetActive(false);

            var title = GameObject.Find("TitleLabel");
            if (title != null) title.SetActive(false);

            new GameObject("SymbolBuilder").AddComponent<SymbolBuilderPanel>();
        }

        private RawImage _previewImage;
        private RawImage _topoImage;
        private TMP_Text _sidcLabel;
        private TMP_InputField _designationField;
        private TMP_InputField _formationField;
        private Slider _strengthSlider;
        private TMP_Text _strengthValueLabel;

        private TMP_Dropdown _affiliationDrop;
        private TMP_Dropdown _echelonDrop;
        private TMP_Dropdown _unitTypeDrop;
        private TMP_Dropdown _variantDrop;
        private TMP_Dropdown _mod1Drop;
        private TMP_Dropdown _mod2Drop;
        private TMP_Dropdown _hqTfDrop;
        private TMP_Dropdown _statusDrop;
        private TMP_Dropdown _strengthModDrop;
        private TMP_Dropdown _mapProfileDrop;
        private TMP_Dropdown _mapModeDrop;
        private TMP_Text _mapInfoLabel;

        private Texture2D _previewTex;
        private Texture2D _topoTex;
        private bool _suppress;

        private ReliefProfile[] _mapProfiles;
        private MapRenderMode[] _mapModes;

        /// <summary>
        /// Seed of the map currently on screen. Fixed at start so the demo opens the
        /// same way every run; NEW MAP advances it.
        /// </summary>
        private int _mapSeed = 20260729;

        /// <summary>Card size the underlay crop was last computed for.</summary>
        private Vector2 _underlayCardSize;

        private Affiliation[] _affiliations;
        private Echelon[] _echelons;
        private LandEntityCode[] _unitTypes;
        private (string label, int code)[] _variants;
        private (string label, int code)[] _sectorMods;
        private HeadquartersTaskForceDummy[] _hqTf;
        private UnitStatus[] _statuses;
        private StrengthModifier[] _strengthMods;

        /// <summary>
        /// APP-6(D) Annex A field layout of the 20-digit SIDC.
        /// Start/Len index into <see cref="SIDCCode.Raw"/>.
        /// </summary>
        private static readonly (string Pos, int Start, int Len, string Field)[] SidcFields =
        {
            ("1–2",   0, 2, "Version / standard"),
            ("3",          2, 1, "Context"),
            ("4",          3, 1, "Standard identity"),
            ("5–6",   4, 2, "Symbol set"),
            ("7",          6, 1, "Status / condition"),
            ("8",          7, 1, "HQ / TF / dummy"),
            ("9–10",  8, 2, "Echelon / mobility"),
            ("11–12",10, 2, "Entity"),
            ("13–14",12, 2, "Entity type"),
            ("15–16",14, 2, "Entity subtype"),
            ("17–18",16, 2, "Sector 1 modifier"),
            ("19–20",18, 2, "Sector 2 modifier"),
        };

        // Text amplifiers are drawn beside the frame but are not encoded in the SIDC.
        private static readonly string[] AmplifierFields =
        {
            "Designation (T)", "Higher formation (M)", "Strength (F)",
        };

        private TableRow[] _tableRows;

        private void Start()
        {
            EnsureEventSystem();
            BuildUi();
            PopulateOptions();
            Canvas.ForceUpdateCanvases();
            RefreshMap();
            RefreshPreview();
        }

        private void OnDestroy()
        {
            DestroyPreviewAssets();
            if (_topoTex != null) Destroy(_topoTex);
        }

        // -------------------------------------------------------------------------
        // Preview + breakdown
        // -------------------------------------------------------------------------

        private void RefreshPreview()
        {
            if (_suppress || _previewImage == null) return;

            var code = BuildSidc();
            var symbol = NatoSymbolComposer.Compose(code, _database);
            var sprite = NatoSymbolBaker.Bake(symbol, _previewSize);

            DestroyPreviewAssets();
            if (sprite != null && sprite.texture != null)
            {
                _previewTex = sprite.texture;
                // Bake owns the texture via Sprite; keep texture, drop sprite wrapper.
                _previewImage.texture = _previewTex;
                _previewImage.color = Color.white;
                Destroy(sprite);
            }

            if (_sidcLabel != null)
            {
                // <mspace> renders the digits monospaced so they line up with the
                // position column in the table below.
                _sidcLabel.text = string.IsNullOrEmpty(code.Raw)
                    ? "—"
                    : $"<mspace=0.62em>{code.Raw}</mspace>";
            }

            UpdateBreakdownTable(code);
        }

        private void UpdateBreakdownTable(SIDCCode code)
        {
            if (_tableRows == null) return;

            string raw = code.Raw ?? string.Empty;

            for (int i = 0; i < SidcFields.Length; i++)
            {
                var f = SidcFields[i];
                _tableRows[i].Code.text = Slice(raw, f.Start, f.Len);
                _tableRows[i].Meaning.text = SidcFieldMeaning(i, code);
            }

            int a = SidcFields.Length;
            _tableRows[a + 0].Code.text = Dash(code.Designation);
            _tableRows[a + 0].Meaning.text = "Unique unit label drawn right of the frame";
            _tableRows[a + 1].Code.text = Dash(code.HigherFormation);
            _tableRows[a + 1].Meaning.text = "Parent command drawn right of the frame";
            _tableRows[a + 2].Code.text = StrengthDisplay(code);
            _tableRows[a + 2].Meaning.text = "+/-/± drawn upper right; % as combat-power bar";
        }

        private static string Slice(string raw, int start, int len)
        {
            if (string.IsNullOrEmpty(raw) || start + len > raw.Length)
                return "—";
            return raw.Substring(start, len);
        }

        private static string Dash(string s) => string.IsNullOrEmpty(s) ? "—" : s;

        private string SidcFieldMeaning(int index, SIDCCode code) => index switch
        {
            0  => "APP-6(D) symbology version",
            1  => $"{code.Context} — {ContextMeaning(code.Context)}",
            2  => $"{code.Affiliation} — {FrameMeaning(code.Affiliation)}",
            3  => $"{Prettify(code.SymbolSet.ToString())} (set {(int)code.SymbolSet:D2})",
            4  => $"{StatusLabel(code.Status)} — {StatusMeaning(code.Status)}",
            5  => HqMeaning(code.HqTfDummy),
            6  => EchelonMeaning(code.Echelon),
            7  => $"{LabelForUnit(code.EntityCode)} — main icon inside the frame",
            8  => $"{VariantLabel(code.EntityType)} — {VariantMark(code)}",
            9  => code.EntitySubtype == 0
                    ? "Not used by this symbol"
                    : $"Subtype {code.EntitySubtype:D2}",
            10 => $"{ModLabel(code.Modifier1)} — upper octagon sector",
            11 => $"{ModLabel(code.Modifier2)} — lower octagon sector",
            _  => string.Empty,
        };

        /// <summary>Describes the mark IconDecorator actually draws for the variant.</summary>
        private static string VariantMark(SIDCCode code)
        {
            string mark = code.EntityType switch
            {
                IconDecorator.VarMechanized => "ellipse around the icon",
                IconDecorator.VarMotorized  => "wheels, lower sector",
                IconDecorator.VarAirAssault => "chevron, lower sector",
                IconDecorator.VarAmphibious => "waves, lower sector",
                IconDecorator.VarMountain   => "mountain, lower sector",
                IconDecorator.VarArctic     => "arch, lower sector",
                IconDecorator.VarHeavy      => "H, lower sector",
                IconDecorator.VarLight      => "L, lower sector",
                _                           => "no additional mark",
            };

            // The lower-sector mark yields to an explicit Sector 2 modifier.
            if (code.Modifier2 != 0 && code.EntityType != IconDecorator.VarMechanized
                && code.EntityType != IconDecorator.VarStandard)
                return mark + " (hidden — sector 2 in use)";

            return mark;
        }

        private static string ContextMeaning(SymbolContext c) => c switch
        {
            SymbolContext.Reality    => "live operational data",
            SymbolContext.Exercise   => "exercise track",
            SymbolContext.Simulation => "simulated track",
            _ => "context",
        };

        private static string FrameMeaning(Affiliation a) => a switch
        {
            Affiliation.Friend or Affiliation.AssumedFriend => "blue rectangle frame",
            Affiliation.Hostile or Affiliation.Suspect => "red diamond frame",
            Affiliation.Neutral => "green square frame",
            Affiliation.Unknown or Affiliation.Pending => "yellow ellipse frame",
            _ => "standard identity frame",
        };

        private static string StatusLabel(UnitStatus s) => s switch
        {
            UnitStatus.Present => "Present",
            UnitStatus.AnticipatedPlanned => "Planned",
            UnitStatus.PresentFullyCapable => "Fully capable",
            UnitStatus.PresentDamaged => "Damaged",
            UnitStatus.PresentDestroyed => "Destroyed",
            UnitStatus.PresentFullToCapacity => "Full capacity",
            _ => s.ToString(),
        };

        private static string StatusMeaning(UnitStatus s) => s switch
        {
            UnitStatus.Present => "solid frame, confirmed location, no bar",
            UnitStatus.AnticipatedPlanned => "dashed frame, planned / anticipated",
            UnitStatus.PresentDamaged => "amber condition bar below frame",
            UnitStatus.PresentDestroyed => "red condition bar below frame",
            UnitStatus.PresentFullyCapable => "green condition bar below frame",
            UnitStatus.PresentFullToCapacity => "blue condition bar below frame",
            _ => "status / condition amplifier",
        };

        private static string HqMeaning(HeadquartersTaskForceDummy h) => h switch
        {
            HeadquartersTaskForceDummy.None => "No HQ / TF / feint amplifiers",
            HeadquartersTaskForceDummy.Headquarters => "HQ staff line below the frame",
            HeadquartersTaskForceDummy.TaskForce => "Task-force bracket above the frame",
            HeadquartersTaskForceDummy.TaskForceHeadquarters => "HQ staff line + task-force bracket",
            HeadquartersTaskForceDummy.FeintDummy => "Feint / dummy dashed inverted V",
            _ => "Combined HQ / TF / feint graphic",
        };

        private static string EchelonMeaning(Echelon e) => e switch
        {
            Echelon.Team => "o  Team / crew",
            Echelon.Squad => "·  Squad",
            Echelon.Section => "··  Section",
            Echelon.Platoon => "···  Platoon",
            Echelon.Company => "I Company / battery / troop",
            Echelon.Battalion => "II Battalion / squadron",
            Echelon.Regiment => "III Regiment / group",
            Echelon.Brigade => "X Brigade",
            Echelon.Division => "XX Division",
            Echelon.Corps => "XXX Corps",
            Echelon.Army => "XXXX Army",
            Echelon.ArmyGroup => "XXXXX Army group / front",
            Echelon.Theater => "XXXXXX Theater",
            Echelon.Command => "++ Command",
            _ => "No echelon mark",
        };

        private string VariantLabel(int code)
        {
            if (_variants == null) return code.ToString("D2");
            foreach (var v in _variants)
                if (v.code == code) return v.label;
            return code.ToString("D2");
        }

        private string ModLabel(int code)
        {
            if (code == 0) return "None";
            if (_sectorMods == null) return code.ToString("D2");
            foreach (var m in _sectorMods)
                if (m.code == code) return m.label;
            return code.ToString("D2");
        }

        private static string StrengthDisplay(SIDCCode code)
        {
            var s = string.IsNullOrEmpty(code.StrengthLabel) ? "—" : code.StrengthLabel + "%";
            return code.StrengthModifier switch
            {
                StrengthModifier.Reinforced => s + " (+)",
                StrengthModifier.Reduced => s + " (-)",
                StrengthModifier.ReinforcedReduced => s + " (±)",
                _ => s,
            };
        }

        private SIDCCode BuildSidc()
        {
            var aff = Pick(_affiliations, _affiliationDrop, Affiliation.Friend);
            var ech = Pick(_echelons, _echelonDrop, Echelon.Company);
            var ent = Pick(_unitTypes, _unitTypeDrop, LandEntityCode.Infantry);
            int type = PickCode(_variants, _variantDrop, 11);
            int mod1 = PickCode(_sectorMods, _mod1Drop, 0);
            int mod2 = PickCode(_sectorMods, _mod2Drop, 0);
            var hq = Pick(_hqTf, _hqTfDrop, HeadquartersTaskForceDummy.None);
            var status = Pick(_statuses, _statusDrop, UnitStatus.Present);
            var strMod = Pick(_strengthMods, _strengthModDrop, StrengthModifier.None);

            int strengthPct = _strengthSlider != null ? Mathf.RoundToInt(_strengthSlider.value) : 100;
            if (_strengthValueLabel != null)
                _strengthValueLabel.text = $"{strengthPct}%";

            string raw = string.Format(
                "10{0}{1}{2:D2}{3}{4}{5:D2}{6:D2}{7:D2}{8:D2}{9:D2}{10:D2}",
                (int)SymbolContext.Reality,
                (int)aff,
                (int)SymbolSet.LandUnit,
                (int)status,
                (int)hq,
                (int)ech,
                (int)ent,
                type,
                0,
                mod1,
                mod2);

            if (!SIDCParser.TryParse(raw, out var code))
            {
                code = new SIDCCode
                {
                    Raw = raw,
                    Context = SymbolContext.Reality,
                    Affiliation = aff,
                    SymbolSet = SymbolSet.LandUnit,
                    Status = status,
                    HqTfDummy = hq,
                    Echelon = ech,
                    EntityCode = (int)ent,
                    EntityType = type,
                    Modifier1 = mod1,
                    Modifier2 = mod2,
                };
            }

            code.Designation = _designationField != null ? _designationField.text : string.Empty;
            code.HigherFormation = _formationField != null ? _formationField.text : string.Empty;
            code.StrengthLabel = strengthPct.ToString();
            code.StrengthModifier = strMod;
            return code;
        }

        private void DestroyPreviewAssets()
        {
            if (_previewImage != null)
                _previewImage.texture = Texture2D.whiteTexture;
            if (_previewTex != null)
            {
                Destroy(_previewTex);
                _previewTex = null;
            }
        }

        // -------------------------------------------------------------------------
        // Options
        // -------------------------------------------------------------------------

        private void PopulateOptions()
        {
            _suppress = true;

            _affiliations = new[]
            {
                Affiliation.Friend, Affiliation.Hostile, Affiliation.Neutral, Affiliation.Unknown,
                Affiliation.AssumedFriend, Affiliation.Suspect, Affiliation.Pending,
            };
            SetDrop(_affiliationDrop, Array.ConvertAll(_affiliations, a => Prettify(a.ToString())), 0);

            _echelons = new[]
            {
                Echelon.Team, Echelon.Squad, Echelon.Section, Echelon.Platoon,
                Echelon.Company, Echelon.Battalion, Echelon.Regiment, Echelon.Brigade,
                Echelon.Division, Echelon.Corps, Echelon.Army, Echelon.ArmyGroup,
                Echelon.Theater, Echelon.Command,
            };
            SetDrop(_echelonDrop, Array.ConvertAll(_echelons, EchelonLabel), 4);

            _unitTypes = new[]
            {
                LandEntityCode.Infantry, LandEntityCode.Armor, LandEntityCode.Artillery,
                LandEntityCode.Reconnaissance, LandEntityCode.CombatEngineering,
                LandEntityCode.AirDefense, LandEntityCode.Aviation,
                LandEntityCode.SignalsCommunication, LandEntityCode.LogisticsSupport,
                LandEntityCode.Medical, LandEntityCode.Headquarters,
                LandEntityCode.SpecialOperations, LandEntityCode.MissileBallistic,
            };
            SetDrop(_unitTypeDrop, Array.ConvertAll(_unitTypes, u => Prettify(u.ToString())), 0);

            _variants = new (string, int)[]
            {
                ("Standard / Foot", 11), ("Mechanized", 12), ("Motorized", 13),
                ("Air Assault", 14), ("Amphibious", 15), ("Mountain", 16),
                ("Arctic", 17), ("Heavy", 18), ("Light", 19),
            };
            SetDrop(_variantDrop, Array.ConvertAll(_variants, v => v.label), 0);

            _sectorMods = new (string, int)[]
            {
                ("None", 0),
                ("Airborne", SectorModifierDecorator.ModAirborne),
                ("Air Assault", SectorModifierDecorator.ModAirAssault),
                ("Wheeled", SectorModifierDecorator.ModWheeled),
                ("Mountain", SectorModifierDecorator.ModMountain),
                ("Amphibious", SectorModifierDecorator.ModAmphibious),
            };
            SetDrop(_mod1Drop, Array.ConvertAll(_sectorMods, m => m.label), 0);
            SetDrop(_mod2Drop, Array.ConvertAll(_sectorMods, m => m.label), 0);

            _hqTf = new[]
            {
                HeadquartersTaskForceDummy.None,
                HeadquartersTaskForceDummy.Headquarters,
                HeadquartersTaskForceDummy.TaskForce,
                HeadquartersTaskForceDummy.TaskForceHeadquarters,
                HeadquartersTaskForceDummy.FeintDummy,
                HeadquartersTaskForceDummy.FeintDummyHeadquarters,
                HeadquartersTaskForceDummy.FeintDummyTaskForce,
                HeadquartersTaskForceDummy.FeintDummyTaskForceHeadquarters,
            };
            SetDrop(_hqTfDrop, Array.ConvertAll(_hqTf, h => Prettify(h.ToString())), 0);

            _statuses = new[]
            {
                UnitStatus.Present, UnitStatus.AnticipatedPlanned,
                UnitStatus.PresentFullyCapable, UnitStatus.PresentDamaged,
                UnitStatus.PresentDestroyed, UnitStatus.PresentFullToCapacity,
            };
            SetDrop(_statusDrop, Array.ConvertAll(_statuses, StatusLabel), 0);

            _strengthMods = new[]
            {
                StrengthModifier.None, StrengthModifier.Reinforced,
                StrengthModifier.Reduced, StrengthModifier.ReinforcedReduced,
            };
            SetDrop(_strengthModDrop, new[]
            {
                "None", "Reinforced (+)", "Reduced (-)", "Reinforced & reduced (±)",
            }, 0);

            if (_designationField != null) _designationField.text = "1-7 IN";
            if (_formationField != null) _formationField.text = "3 ID";
            if (_strengthSlider != null) _strengthSlider.value = 100;

            _mapProfiles = new[]
            {
                ReliefProfile.Rolling, ReliefProfile.Plains, ReliefProfile.Hills,
                ReliefProfile.Mountains, ReliefProfile.Coastal, ReliefProfile.Desert,
                ReliefProfile.Arctic,
            };
            SetDrop(_mapProfileDrop, Array.ConvertAll(_mapProfiles, p => Prettify(p.ToString())), 0);

            // Schematic first: it is the operations-map look, where everything that is
            // not a symbol or a control measure steps back. That is what an underlay
            // behind a symbol wants.
            _mapModes = new[]
            {
                MapRenderMode.Schematic, MapRenderMode.Topographic,
                MapRenderMode.Hybrid, MapRenderMode.Terrain,
            };
            SetDrop(_mapModeDrop, Array.ConvertAll(_mapModes, m => Prettify(m.ToString())), 0);

            _suppress = false;
        }

        // -------------------------------------------------------------------------
        // UI construction
        // -------------------------------------------------------------------------

        private void BuildUi()
        {
            var canvasGo = new GameObject("SymbolBuilderCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var root = CreateRect("Root", canvasGo.transform);
            Stretch(root);
            root.gameObject.AddComponent<Image>().color = Theme.StageBg;
            var rootH = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            rootH.spacing = 0;
            rootH.childControlWidth = true;
            rootH.childControlHeight = true;
            // Must be false: force-expand hands every child a share of the surplus
            // regardless of flexibleWidth, which pushes the fixed-width rail past
            // the screen edge. With it off the stage absorbs all slack instead.
            rootH.childForceExpandWidth = false;
            rootH.childForceExpandHeight = true;

            BuildStage(root);
            BuildControlRail(root);
        }

        private void BuildStage(Transform root)
        {
            var stage = CreateRect("Stage", root);
            var stageLe = stage.gameObject.AddComponent<LayoutElement>();
            stageLe.flexibleWidth = 1f;   // absorbs all width the rail does not take
            stageLe.minWidth = 480f;
            stage.gameObject.AddComponent<Image>().color = Theme.StageBg;

            var stageV = stage.gameObject.AddComponent<VerticalLayoutGroup>();
            stageV.padding = new RectOffset(32, 24, 24, 24);
            stageV.spacing = 12;
            stageV.childAlignment = TextAnchor.UpperCenter;
            stageV.childControlWidth = true;
            // Must be true: with childControlHeight off the group reserves space
            // from LayoutElement.preferredHeight but never resizes the child,
            // leaving zero-height rects.
            stageV.childControlHeight = true;
            stageV.childForceExpandHeight = false;
            stageV.childForceExpandWidth = true;

            var hdr = CreateTmp("Hdr", stage, "STRATEGOS  ·  APP-6(D) SYMBOL", 20, FontStyles.Bold);
            hdr.alignment = TextAlignmentOptions.Center;
            hdr.color = Theme.Ink;
            hdr.characterSpacing = 6f;

            BuildMapCard(stage);
            BuildSidcCard(stage);
            BuildTableCard(stage);
        }

        private void BuildMapCard(Transform stage)
        {
            var mapCard = CreateRect("MapCard", stage);
            mapCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 400;
            mapCard.gameObject.AddComponent<Image>().color = Theme.CardLine;

            var mapStack = CreateRect("MapStack", mapCard);
            Stretch(mapStack);
            mapStack.offsetMin = new Vector2(2, 2);
            mapStack.offsetMax = new Vector2(-2, -2);
            mapStack.gameObject.AddComponent<Image>().color = Theme.MapPaper;

            var topoRt = CreateRect("Topo", mapStack);
            Stretch(topoRt);
            _topoImage = topoRt.gameObject.AddComponent<RawImage>();
            _topoImage.color = Color.white;
            _topoImage.raycastTarget = false;
            // Texture comes from RefreshMap, which needs the map dropdowns to exist and
            // so cannot run until the whole UI is built.
            _topoImage.texture = Texture2D.whiteTexture;

            // Paper halo so the symbol and its amplifier text stay legible over the
            // sheet. Soft-edged, not a flat rectangle: against a real map a hard edge
            // reads as a panel someone forgot to remove, and it cuts the contours it
            // is meant to quieten.
            var halo = CreateRect("Halo", mapStack);
            halo.anchorMin = new Vector2(0.5f, 0.5f);
            halo.anchorMax = new Vector2(0.5f, 0.5f);
            halo.pivot = new Vector2(0.5f, 0.5f);
            halo.sizeDelta = new Vector2(420, 380);
            var haloImg = halo.gameObject.AddComponent<Image>();
            haloImg.sprite = UiSprites.Halo;
            haloImg.color = new Color(1f, 1f, 0.98f, 0.62f);
            haloImg.raycastTarget = false;

            var symbolHolder = CreateRect("SymbolHolder", mapStack);
            symbolHolder.anchorMin = new Vector2(0.5f, 0.5f);
            symbolHolder.anchorMax = new Vector2(0.5f, 0.5f);
            symbolHolder.pivot = new Vector2(0.5f, 0.5f);
            symbolHolder.sizeDelta = new Vector2(320, 320);
            _previewImage = symbolHolder.gameObject.AddComponent<RawImage>();
            _previewImage.color = Color.white;
            _previewImage.texture = Texture2D.whiteTexture;
            _previewImage.raycastTarget = false;

            BuildMapMarginalia(mapStack);
        }

        /// <summary>
        /// The strip along the foot of the sheet carrying seed, extent, elevation range
        /// and contour interval — a map's marginalia. Without it the underlay is a
        /// picture; with it the reader knows what ground and what scale they are on.
        /// </summary>
        private void BuildMapMarginalia(Transform mapStack)
        {
            var strip = CreateRect("Marginalia", mapStack);
            strip.anchorMin = new Vector2(0, 0);
            strip.anchorMax = new Vector2(1, 0);
            strip.pivot = new Vector2(0.5f, 0);
            strip.sizeDelta = new Vector2(0, 20);

            var stripImg = strip.gameObject.AddComponent<Image>();
            stripImg.color = new Color(1f, 1f, 0.98f, 0.72f);
            stripImg.raycastTarget = false;

            _mapInfoLabel = CreateOverlayTmp("Info", strip, string.Empty, 10, Theme.InkMuted);
            Stretch(_mapInfoLabel.rectTransform);
            _mapInfoLabel.rectTransform.offsetMin = new Vector2(8, 0);
            _mapInfoLabel.rectTransform.offsetMax = new Vector2(-8, 0);
            _mapInfoLabel.alignment = TextAlignmentOptions.MidlineLeft;
            _mapInfoLabel.characterSpacing = 1.5f;
            _mapInfoLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _mapInfoLabel.overflowMode = TextOverflowModes.Ellipsis;
            _mapInfoLabel.raycastTarget = false;
        }

        private void BuildSidcCard(Transform stage)
        {
            var sidcCard = CreateRect("SidcCard", stage);
            sidcCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 62;
            sidcCard.gameObject.AddComponent<Image>().color = Theme.CardBg;
            var sidcV = sidcCard.gameObject.AddComponent<VerticalLayoutGroup>();
            sidcV.padding = new RectOffset(16, 16, 8, 8);
            sidcV.spacing = 2;
            sidcV.childAlignment = TextAnchor.MiddleCenter;
            sidcV.childControlWidth = true;
            sidcV.childForceExpandWidth = true;

            var sidcCap = CreateTmp("SidcCap", sidcCard, "SYMBOL IDENTIFICATION CODE", 11, FontStyles.Bold);
            sidcCap.alignment = TextAlignmentOptions.Center;
            sidcCap.color = Theme.InkMuted;
            sidcCap.characterSpacing = 4f;
            sidcCap.GetComponent<LayoutElement>().preferredHeight = 14;

            _sidcLabel = CreateTmp("Sidc", sidcCard, "—", 24, FontStyles.Bold);
            _sidcLabel.alignment = TextAlignmentOptions.Center;
            _sidcLabel.color = Theme.Accent;
            _sidcLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _sidcLabel.GetComponent<LayoutElement>().preferredHeight = 30;
        }

        private void BuildTableCard(Transform stage)
        {
            var tableCard = CreateRect("TableCard", stage);
            var tableLe = tableCard.gameObject.AddComponent<LayoutElement>();
            tableLe.flexibleHeight = 1;
            tableLe.minHeight = 240;
            tableCard.gameObject.AddComponent<Image>().color = Theme.CardBg;

            var tableV = tableCard.gameObject.AddComponent<VerticalLayoutGroup>();
            tableV.padding = new RectOffset(16, 16, 12, 12);
            tableV.spacing = 0;
            tableV.childControlWidth = true;
            tableV.childForceExpandWidth = true;
            tableV.childControlHeight = true;
            tableV.childForceExpandHeight = false;

            var tableTitle = CreateTmp("TableTitle", tableCard, "CODE BREAKDOWN", 13, FontStyles.Bold);
            tableTitle.color = Theme.Ink;
            tableTitle.characterSpacing = 4f;
            tableTitle.GetComponent<LayoutElement>().preferredHeight = 20;

            // Header row
            var header = UiTable.CreateRow(tableCard, "HeaderRow", Theme.SectionBg, out var h);
            header.GetComponent<LayoutElement>().preferredHeight = 24;
            UiTable.SetRowText(h, "POS", "CODE", "FIELD", "MEANING");
            UiTable.ApplyRowStyle(h, Theme.Ink, FontStyles.Bold, 11.5f);

            var divider = CreateRect("Divider", tableCard);
            divider.gameObject.AddComponent<LayoutElement>().preferredHeight = 2;
            divider.gameObject.AddComponent<Image>().color = Theme.CardLine;

            int total = SidcFields.Length + AmplifierFields.Length;
            _tableRows = new TableRow[total];

            for (int i = 0; i < total; i++)
            {
                bool amplifier = i >= SidcFields.Length;
                var stripe = (i % 2 == 0) ? Theme.CardBg : Theme.RowStripe;
                UiTable.CreateRow(tableCard, $"Row{i}", stripe, out var r);
                _tableRows[i] = r;

                if (amplifier)
                {
                    r.Pos.text = "amp";
                    r.Field.text = AmplifierFields[i - SidcFields.Length];
                }
                else
                {
                    r.Pos.text = SidcFields[i].Pos;
                    r.Field.text = SidcFields[i].Field;
                }

                r.Pos.color = Theme.InkMuted;
                r.Code.color = Theme.Accent;
                r.Code.fontStyle = FontStyles.Bold;
                r.Field.color = Theme.Ink;
                r.Meaning.color = Theme.InkMuted;
            }
        }

        private void BuildControlRail(Transform root)
        {
            var panel = CreateRect("ControlRail", root);
            var panelLe = panel.gameObject.AddComponent<LayoutElement>();
            panelLe.preferredWidth = 440;
            panelLe.minWidth = 400;
            panelLe.flexibleWidth = 0;
            panel.gameObject.AddComponent<Image>().color = Theme.RailBg;

            // Left edge rule separating rail from stage
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
            chrome.anchoredPosition = Vector2.zero;
            chrome.gameObject.AddComponent<Image>().color = Theme.Accent;
            var chromeLabel = CreateTmp("ChromeLabel", chrome, "UNIT CONSTRUCTION", 14, FontStyles.Bold, withLayout: false);
            Stretch(chromeLabel.rectTransform);
            chromeLabel.alignment = TextAlignmentOptions.Center;
            chromeLabel.color = Theme.AccentText;
            chromeLabel.characterSpacing = 6f;

            var content = UiScroll.CreateColumn("Scroll", panel, Theme.RailBg, out var scroll);
            var scrollRt = (RectTransform)scroll.transform;
            scrollRt.offsetMin = new Vector2(2, 0);
            scrollRt.offsetMax = new Vector2(0, -38); // clear the chrome header

            AddSection(content, "IDENTITY");
            _affiliationDrop = AddDropdown(content, "AFFILIATION", RefreshPreview);
            _statusDrop = AddDropdown(content, "STATUS / CONDITION", RefreshPreview);

            AddSection(content, "ECHELON");
            _echelonDrop = AddDropdown(content, "COMMAND LEVEL", RefreshPreview);
            _hqTfDrop = AddDropdown(content, "HQ / TASK FORCE / FEINT", RefreshPreview);

            AddSection(content, "UNIT / EQUIPMENT");
            _unitTypeDrop = AddDropdown(content, "UNIT TYPE", RefreshPreview);
            _variantDrop = AddDropdown(content, "VARIANT", RefreshPreview);

            AddSection(content, "SECTOR MODIFIERS");
            _mod1Drop = AddDropdown(content, "SECTOR 1 (UPPER)", RefreshPreview);
            _mod2Drop = AddDropdown(content, "SECTOR 2 (LOWER)", RefreshPreview);

            AddSection(content, "STRENGTH");
            _strengthModDrop = AddDropdown(content, "STRENGTH AMPLIFIER", RefreshPreview);
            (_strengthSlider, _strengthValueLabel) =
                AddSlider(content, "STRENGTH", 0, 100, 100, RefreshPreview);

            AddSection(content, "LABELS");
            _designationField = AddInput(content, "DESIGNATION", "1-7 IN", RefreshPreview);
            _formationField = AddInput(content, "HIGHER FORMATION", "3 ID", RefreshPreview);

            AddButton(content, "REFRESH SYMBOL", RefreshPreview);

            // The map controls rebuild the sheet, not the symbol. Regenerating is orders
            // of magnitude more expensive than recomposing, so the two refresh paths stay
            // separate rather than having one control trigger both.
            AddSection(content, "MAP UNDERLAY");
            _mapProfileDrop = AddDropdown(content, "RELIEF PROFILE", RefreshMapIfReady);
            _mapModeDrop = AddDropdown(content, "RENDER MODE", RefreshMapIfReady);

            AddButton(content, "NEW MAP", NewMap);
        }

        private void RefreshMapIfReady()
        {
            if (_suppress) return;
            RefreshMap();
        }

        /// <summary>Advances the seed and regenerates. Same profile, different ground.</summary>
        private void NewMap()
        {
            _mapSeed = UnityEngine.Random.Range(1, int.MaxValue);
            RefreshMap();
        }

        // -------------------------------------------------------------------------
        // Topo underlay
        // -------------------------------------------------------------------------

        /// <summary>
        /// Cells per side of the underlay map. Square, and cropped to whatever shape
        /// the card happens to be — see <see cref="UpdateUnderlayCrop"/>. 200 cells at
        /// 25 m is a 5 km square, and generates in a few hundred milliseconds.
        /// </summary>
        private const int UnderlayCells = 200;

        /// <summary>
        /// Render scale. Above the overview zoom on purpose: at 1 px per cell the
        /// renderer generalises roads to hairlines and drops spot detail, which is
        /// right for a whole-map sheet and wrong for a card you look at up close.
        /// </summary>
        private const float UnderlayPixelsPerCell = 4f;

        /// <summary>
        /// How many landforms should fit across the sheet. A profile's
        /// <c>FeatureScaleCells</c> is tuned for the 512-cell default map; used as-is on
        /// a 200-cell one, a single hill is wider than the whole sheet and the underlay
        /// comes out as one dome with concentric rings.
        /// </summary>
        private const float UnderlayLandformsAcross = 2.5f;

        /// <summary>
        /// Regenerates the underlay from the current map controls and shows it.
        /// Called from Start and whenever a map control changes; generation costs a few
        /// hundred milliseconds on the main thread, which is why it is on a button and
        /// two dropdowns rather than a slider.
        /// </summary>
        private void RefreshMap()
        {
            if (_topoImage == null) return;

            var profile = SelectedMapProfile;

            // Shrink the landform scale to suit a sheet this small. This is what
            // ParameterOverride is for — the alternative is inventing a near-duplicate
            // profile per map size.
            var tuned = ReliefProfiles.For(profile);
            tuned.FeatureScaleCells = Mathf.Min(
                tuned.FeatureScaleCells, UnderlayCells / UnderlayLandformsAcross);

            var settings = new MapGenerationSettings
            {
                Name              = "UNDERLAY",
                Seed              = _mapSeed,
                Width             = UnderlayCells,
                Height            = UnderlayCells,
                MetresPerCell     = 25f,
                Profile           = profile,
                ParameterOverride = tuned,
            };

            var map = MapGenerator.Generate(settings);

            var options = MapRenderOptions.Default;
            options.Mode          = SelectedMapMode;
            options.PixelsPerCell = UnderlayPixelsPerCell;

            // No names, no spot heights: this sheet exists to be drawn on. Text and
            // point marks under a 320 px symbol are clutter competing with the subject,
            // and the contact sheet is where that detail gets inspected.
            options.DrawLabels = false;
            options.DrawPois   = false;

            var tex = MapRasterizer.Render(map, options);
            tex.name = "TopoUnderlay";

            _topoImage.texture = tex;
            if (_topoTex != null) Destroy(_topoTex);
            _topoTex = tex;

            UpdateUnderlayCrop();

            if (_mapInfoLabel != null)
            {
                var header = map.Header;
                // Hyphen, not an en dash: U+2013 is not in the bundled LiberationSans
                // SDF atlas and renders as nothing, so the elevation range came out as
                // "202 280 M". Latin-1 punctuation only here — see the glyph coverage
                // note in CLAUDE.md.
                _mapInfoLabel.text =
                    $"SEED {_mapSeed}   ·   {header.WidthMetres / 1000f:0.#} × " +
                    $"{header.HeightMetres / 1000f:0.#} KM   ·   " +
                    $"{header.MinElevation:0}-{header.MaxElevation:0} M   ·   " +
                    $"CONTOUR {header.ContourInterval:0} M";
            }
        }

        /// <summary>
        /// Fits the sheet to the card by cropping to a centred region of the card's
        /// shape, never by stretching. A stretched map has a different scale on each
        /// axis, so every distance and bearing read off it is wrong — which for a map
        /// is a correctness problem, not a cosmetic one. The card's aspect depends on
        /// the window, so this is re-evaluated when its size changes rather than baked
        /// into the generated map.
        /// </summary>
        private void UpdateUnderlayCrop()
        {
            if (_topoImage == null || _topoTex == null) return;

            Rect card = _topoImage.rectTransform.rect;
            if (card.width < 1f || card.height < 1f) return;

            _underlayCardSize = new Vector2(card.width, card.height);

            float cardAspect = card.width / card.height;
            float texAspect  = _topoTex.width / (float)_topoTex.height;

            float uvW = 1f, uvH = 1f;
            if (cardAspect > texAspect) uvH = texAspect / cardAspect;
            else                        uvW = cardAspect / texAspect;

            _topoImage.uvRect = new Rect((1f - uvW) * 0.5f, (1f - uvH) * 0.5f, uvW, uvH);
        }

        private void Update()
        {
            // Cheap guard against a window resize reshaping the card. Regenerating on
            // resize would stall for a few hundred milliseconds; re-cropping is free.
            if (_topoImage == null) return;

            Rect card = _topoImage.rectTransform.rect;
            if (Mathf.Abs(card.width  - _underlayCardSize.x) > 0.5f ||
                Mathf.Abs(card.height - _underlayCardSize.y) > 0.5f)
                UpdateUnderlayCrop();
        }

        private ReliefProfile SelectedMapProfile =>
            Pick(_mapProfiles, _mapProfileDrop, ReliefProfile.Rolling);

        private MapRenderMode SelectedMapMode =>
            Pick(_mapModes, _mapModeDrop, MapRenderMode.Schematic);

        private static string EchelonLabel(Echelon e) => e switch
        {
            // Latin-1 only: the bundled font atlas has no geometric-shape glyphs.
            Echelon.Team => "Team / Crew  o",
            Echelon.Squad => "Squad  ·",
            Echelon.Section => "Section  ··",
            Echelon.Platoon => "Platoon  ···",
            Echelon.Company => "Company  I",
            Echelon.Battalion => "Battalion  II",
            Echelon.Regiment => "Regiment  III",
            Echelon.Brigade => "Brigade  X",
            Echelon.Division => "Division  XX",
            Echelon.Corps => "Corps  XXX",
            Echelon.Army => "Army  XXXX",
            Echelon.ArmyGroup => "Army Group  XXXXX",
            Echelon.Theater => "Theater  XXXXXX",
            Echelon.Command => "Command  ++",
            _ => e.ToString(),
        };

        private static string Prettify(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var chars = new List<char> { s[0] };
            for (int i = 1; i < s.Length; i++)
            {
                if (char.IsUpper(s[i])) chars.Add(' ');
                chars.Add(s[i]);
            }
            return new string(chars.ToArray());
        }

        private static string LabelForUnit(int entityCode)
        {
            if (Enum.IsDefined(typeof(LandEntityCode), entityCode))
                return Prettify(((LandEntityCode)entityCode).ToString());
            return $"Entity {entityCode:D2}";
        }
    }
}
