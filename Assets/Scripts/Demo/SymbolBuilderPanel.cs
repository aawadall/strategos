// SymbolBuilderPanel.cs
// Light "operations map" presentation:
//   left  — topo underlay, composed symbol, SIDC, digit-by-digit breakdown table
//   right — light control rail with bordered selectors
// All text/background pairs are chosen for >= 7:1 contrast (WCAG AAA).

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Strategos.NatoSymbols;

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

        private Texture2D _previewTex;
        private Texture2D _topoTex;
        private bool _suppress;
        private static TMP_FontAsset _uiFont;

        private Affiliation[] _affiliations;
        private Echelon[] _echelons;
        private LandEntityCode[] _unitTypes;
        private (string label, int code)[] _variants;
        private (string label, int code)[] _sectorMods;
        private HeadquartersTaskForceDummy[] _hqTf;
        private UnitStatus[] _statuses;
        private StrengthModifier[] _strengthMods;

        /// <summary>
        /// Light paper palette. Contrast ratios against their intended background
        /// are noted so future edits keep the UI legible.
        /// </summary>
        private static class Theme
        {
            public static readonly Color StageBg      = Hex(0xEF, 0xED, 0xE4); // page
            public static readonly Color MapPaper     = Hex(0xE8, 0xE4, 0xD6); // map card
            public static readonly Color CardBg       = Hex(0xFA, 0xF9, 0xF4); // sidc / table card
            public static readonly Color CardLine     = Hex(0xB9, 0xB5, 0xA4);
            public static readonly Color RowStripe    = Hex(0xF1, 0xEF, 0xE7);

            public static readonly Color RailBg       = Hex(0xE4, 0xE1, 0xD5);
            public static readonly Color RailEdge     = Hex(0xCF, 0xCB, 0xBA);
            public static readonly Color SectionBg    = Hex(0xD2, 0xCE, 0xBE);

            public static readonly Color ControlFace  = Hex(0xFC, 0xFB, 0xF6);
            public static readonly Color ControlEdge  = Hex(0x8C, 0x88, 0x7A);
            public static readonly Color ControlHover = Hex(0xED, 0xEA, 0xDD);
            public static readonly Color SelectFill   = Hex(0xD3, 0xE2, 0xD0); // selected item

            public static readonly Color Ink          = Hex(0x19, 0x1C, 0x18); // 16.5:1 on ControlFace
            public static readonly Color InkMuted     = Hex(0x3F, 0x42, 0x39); //  7.9:1 on RailBg
            public static readonly Color Accent       = Hex(0x2C, 0x5A, 0x38); //  7.6:1 on CardBg
            public static readonly Color AccentText   = Hex(0xF6, 0xF4, 0xEB); //  7.4:1 on Accent

            private static Color Hex(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f, 1f);
        }

        // Column widths for the breakdown table (reference-resolution px).
        private const float ColPos = 62f;
        private const float ColCode = 62f;
        private const float ColField = 168f;

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

        private struct TableRow
        {
            public TMP_Text Pos;
            public TMP_Text Code;
            public TMP_Text Field;
            public TMP_Text Meaning;
        }

        private TableRow[] _tableRows;

        private void Start()
        {
            EnsureEventSystem();
            BuildUi();
            PopulateOptions();
            Canvas.ForceUpdateCanvases();
            RefreshPreview();
        }

        private void OnDestroy()
        {
            DestroyPreviewAssets();
            if (_topoTex != null) Destroy(_topoTex);
        }

        private static TMP_FontAsset UiFont
        {
            get
            {
                if (_uiFont != null) return _uiFont;

                // TMP_Settings.defaultFontAsset dereferences TMP_Settings.instance,
                // which is null until TMP Essential Resources are imported — the
                // property throws rather than returning null, so it must be guarded
                // or it takes the whole UI build down with it.
                if (TMP_Settings.instance != null)
                {
                    try { _uiFont = TMP_Settings.defaultFontAsset; }
                    catch (Exception) { _uiFont = null; }
                }
                if (_uiFont != null) return _uiFont;

                // Fall back to an OS font so the panel still renders.
                var osFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Segoe UI", "Arial", "Helvetica", "Tahoma", "sans-serif" }, 28);
                if (osFont != null)
                {
                    _uiFont = TMP_FontAsset.CreateFontAsset(osFont);
                    if (_uiFont != null)
                        _uiFont.name = "StrategosUIFont";
                }
                return _uiFont;
            }
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
            _tableRows[a + 2].Meaning.text = "Combat power / reinforced-reduced amplifier";
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
            8  => $"{VariantLabel(code.EntityType)} — equipment / mobility variant",
            9  => code.EntitySubtype == 0
                    ? "Not used by this symbol"
                    : $"Subtype {code.EntitySubtype:D2}",
            10 => $"{ModLabel(code.Modifier1)} — upper octagon sector",
            11 => $"{ModLabel(code.Modifier2)} — lower octagon sector",
            _  => string.Empty,
        };

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
            UnitStatus.Present => "solid frame, confirmed location",
            UnitStatus.AnticipatedPlanned => "dashed frame, planned / anticipated",
            UnitStatus.PresentDamaged => "operational condition: damaged",
            UnitStatus.PresentDestroyed => "operational condition: destroyed",
            UnitStatus.PresentFullyCapable => "fully capable",
            UnitStatus.PresentFullToCapacity => "at capacity",
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
            _topoTex = GenerateTopoTexture(640, 480);
            _topoImage.texture = _topoTex;
            _topoImage.color = Color.white;
            _topoImage.raycastTarget = false;

            // Translucent paper halo so the symbol stays legible over the contours.
            var halo = CreateRect("Halo", mapStack);
            halo.anchorMin = new Vector2(0.5f, 0.5f);
            halo.anchorMax = new Vector2(0.5f, 0.5f);
            halo.pivot = new Vector2(0.5f, 0.5f);
            halo.sizeDelta = new Vector2(300, 260);
            var haloImg = halo.gameObject.AddComponent<Image>();
            haloImg.color = new Color(1f, 1f, 0.98f, 0.34f);
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
            var header = CreateTableRow(tableCard, "HeaderRow", Theme.SectionBg, out var h);
            header.GetComponent<LayoutElement>().preferredHeight = 24;
            SetRowText(h, "POS", "CODE", "FIELD", "MEANING");
            ApplyRowStyle(h, Theme.Ink, FontStyles.Bold, 11.5f);

            var divider = CreateRect("Divider", tableCard);
            divider.gameObject.AddComponent<LayoutElement>().preferredHeight = 2;
            divider.gameObject.AddComponent<Image>().color = Theme.CardLine;

            int total = SidcFields.Length + AmplifierFields.Length;
            _tableRows = new TableRow[total];

            for (int i = 0; i < total; i++)
            {
                bool amplifier = i >= SidcFields.Length;
                var stripe = (i % 2 == 0) ? Theme.CardBg : Theme.RowStripe;
                CreateTableRow(tableCard, $"Row{i}", stripe, out var r);
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

        private RectTransform CreateTableRow(Transform parent, string name, Color bg, out TableRow row)
        {
            var rt = CreateRect(name, parent);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;
            rt.gameObject.AddComponent<Image>().color = bg;

            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(8, 8, 0, 0);
            h.spacing = 8;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            h.childAlignment = TextAnchor.MiddleLeft;

            row = new TableRow
            {
                Pos     = TableCell(rt, "Pos", ColPos, 0f),
                Code    = TableCell(rt, "Code", ColCode, 0f),
                Field   = TableCell(rt, "Field", ColField, 0f),
                Meaning = TableCell(rt, "Meaning", 0f, 1f),
            };
            return rt;
        }

        private static TMP_Text TableCell(Transform parent, string name, float width, float flexible)
        {
            var tmp = CreateTmp(name, parent, string.Empty, 12, FontStyles.Normal);
            var le = tmp.GetComponent<LayoutElement>();
            le.preferredWidth = width;
            le.flexibleWidth = flexible;
            le.preferredHeight = 22;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        private static void SetRowText(TableRow r, string a, string b, string c, string d)
        {
            r.Pos.text = a;
            r.Code.text = b;
            r.Field.text = c;
            r.Meaning.text = d;
        }

        private static void ApplyRowStyle(TableRow r, Color color, FontStyles style, float size)
        {
            foreach (var t in new[] { r.Pos, r.Code, r.Field, r.Meaning })
            {
                t.color = color;
                t.fontStyle = style;
                t.fontSize = size;
                t.characterSpacing = 2f;
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

            var scrollGo = CreateRect("Scroll", panel);
            Stretch(scrollGo);
            scrollGo.offsetMin = new Vector2(2, 0);
            scrollGo.offsetMax = new Vector2(0, -38);
            var scroll = scrollGo.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 48f;

            var viewport = CreateRect("Viewport", scrollGo);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Theme.RailBg;
            scroll.viewport = viewport;

            var content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var contentV = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentV.padding = new RectOffset(14, 14, 12, 28);
            contentV.spacing = 8;
            contentV.childControlWidth = true;
            contentV.childControlHeight = true;
            contentV.childForceExpandHeight = false;
            contentV.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            AddSection(content, "IDENTITY");
            _affiliationDrop = AddDropdown(content, "AFFILIATION");
            _statusDrop = AddDropdown(content, "STATUS / CONDITION");

            AddSection(content, "ECHELON");
            _echelonDrop = AddDropdown(content, "COMMAND LEVEL");
            _hqTfDrop = AddDropdown(content, "HQ / TASK FORCE / FEINT");

            AddSection(content, "UNIT / EQUIPMENT");
            _unitTypeDrop = AddDropdown(content, "UNIT TYPE");
            _variantDrop = AddDropdown(content, "VARIANT");

            AddSection(content, "SECTOR MODIFIERS");
            _mod1Drop = AddDropdown(content, "SECTOR 1 (UPPER)");
            _mod2Drop = AddDropdown(content, "SECTOR 2 (LOWER)");

            AddSection(content, "STRENGTH");
            _strengthModDrop = AddDropdown(content, "STRENGTH AMPLIFIER");
            (_strengthSlider, _strengthValueLabel) = AddSlider(content, "STRENGTH", 0, 100, 100);

            AddSection(content, "LABELS");
            _designationField = AddInput(content, "DESIGNATION", "1-7 IN");
            _formationField = AddInput(content, "HIGHER FORMATION", "3 ID");

            AddButton(content, "REFRESH SYMBOL", RefreshPreview);
        }

        // -------------------------------------------------------------------------
        // Topo map generation
        // -------------------------------------------------------------------------

        private static Texture2D GenerateTopoTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "TopoUnderlay",
            };

            var px = new Color32[w * h];
            var paper = new Color32(233, 229, 214, 255);
            var water = new Color32(196, 214, 208, 255);
            // Kept low-contrast: the map is an underlay, the symbol is the subject.
            var contour = new Color32(196, 185, 156, 255);
            var contourMajor = new Color32(168, 154, 120, 255);
            var gridCol = new Color32(206, 200, 178, 255);

            // Fixed hill centres (map-like, not stretched Perlin woodgrain)
            var hills = new[]
            {
                new Vector2(0.28f, 0.62f),
                new Vector2(0.68f, 0.55f),
                new Vector2(0.48f, 0.30f),
                new Vector2(0.78f, 0.28f),
                new Vector2(0.18f, 0.28f),
            };
            var hillH = new[] { 1.0f, 0.85f, 0.7f, 0.55f, 0.6f };
            var hillR = new[] { 0.42f, 0.38f, 0.35f, 0.28f, 0.30f };

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = x / (float)(w - 1);
                float ny = y / (float)(h - 1);

                float elev = 0f;
                for (int i = 0; i < hills.Length; i++)
                {
                    float dx = (nx - hills[i].x) / hillR[i];
                    float dy = (ny - hills[i].y) / hillR[i];
                    float d2 = dx * dx + dy * dy;
                    elev += hillH[i] * Mathf.Exp(-d2 * 2.2f);
                }
                // gentle regional tilt
                elev += (1f - ny) * 0.12f;
                elev = Mathf.Clamp01(elev / 1.6f);

                Color32 c = elev < 0.18f ? water : paper;

                // True isolines: mark when crossing contour thresholds
                const int levels = 12;
                float scaled = elev * levels;
                float distToLine = Mathf.Abs(scaled - Mathf.Round(scaled));
                int level = Mathf.RoundToInt(scaled);
                if (distToLine < 0.07f && elev > 0.16f)
                    c = (level % 3 == 0) ? contourMajor : contour;

                // Map grid
                int gx = x * 8 / w;
                int gy = y * 6 / h;
                bool gridLine =
                    Mathf.Abs(x - (gx * w / 8)) < 1 ||
                    Mathf.Abs(y - (gy * h / 6)) < 1;
                if (gridLine)
                    c = Lerp(c, gridCol, 0.55f);

                px[y * w + x] = c;
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        private static Color32 Lerp(Color32 a, Color32 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color32(
                (byte)(a.r + (b.r - a.r) * t),
                (byte)(a.g + (b.g - a.g) * t),
                (byte)(a.b + (b.b - a.b) * t),
                255);
        }

        // -------------------------------------------------------------------------
        // Control helpers
        // -------------------------------------------------------------------------

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Unity UI has no border on Image, so a control is an outer edge-coloured
        /// rect with an inset face rect. Returns the face image for tinting.
        /// </summary>
        private static Image AddBorderedFace(RectTransform outer, float inset = 2f)
        {
            outer.gameObject.AddComponent<Image>().color = Theme.ControlEdge;
            var face = CreateRect("Face", outer);
            Stretch(face);
            face.offsetMin = new Vector2(inset, inset);
            face.offsetMax = new Vector2(-inset, -inset);
            var img = face.gameObject.AddComponent<Image>();
            img.color = Color.white; // tinted via ColorBlock
            return img;
        }

        private static ColorBlock ControlColors()
        {
            var c = ColorBlock.defaultColorBlock;
            c.normalColor = Theme.ControlFace;
            c.highlightedColor = Theme.ControlHover;
            c.pressedColor = Theme.SelectFill;
            c.selectedColor = Theme.ControlFace;
            c.disabledColor = Theme.RailEdge;
            c.fadeDuration = 0.08f;
            return c;
        }

        private static TMP_Text CreateTmp(string name, Transform parent, string text, float size, FontStyles style, bool withLayout = true)
        {
            var rt = CreateRect(name, parent);
            if (withLayout)
            {
                var le = rt.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = size + 8;
            }
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (UiFont != null) tmp.font = UiFont;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = Theme.Ink;
            tmp.raycastTarget = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        private static TMP_Text CreateOverlayTmp(string name, Transform parent, string text, float size, Color color)
        {
            // For dropdown captions / items — no LayoutElement (breaks TMP_Dropdown).
            var tmp = CreateTmp(name, parent, text, size, FontStyles.Bold, withLayout: false);
            tmp.color = color;
            return tmp;
        }

        private void AddSection(Transform parent, string title)
        {
            var bar = CreateRect($"Sec_{title}", parent);
            bar.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
            bar.gameObject.AddComponent<Image>().color = Theme.SectionBg;

            var accent = CreateRect("Accent", bar);
            accent.anchorMin = new Vector2(0, 0);
            accent.anchorMax = new Vector2(0, 1);
            accent.pivot = new Vector2(0, 0.5f);
            accent.sizeDelta = new Vector2(4, 0);
            accent.gameObject.AddComponent<Image>().color = Theme.Accent;

            var tmp = CreateTmp("T", bar, title, 12, FontStyles.Bold, withLayout: false);
            Stretch(tmp.rectTransform);
            tmp.rectTransform.offsetMin = new Vector2(12, 0);
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Theme.Ink;
            tmp.characterSpacing = 3f;
        }

        private TMP_Dropdown AddDropdown(Transform parent, string label)
        {
            var wrap = CreateRect($"DD_{label}", parent);
            var wrapV = wrap.gameObject.AddComponent<VerticalLayoutGroup>();
            wrapV.spacing = 3;
            wrapV.childControlWidth = true;
            wrapV.childControlHeight = true;
            wrapV.childForceExpandWidth = true;
            wrap.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;

            var lbl = CreateTmp("L", wrap, label, 11, FontStyles.Bold);
            lbl.color = Theme.InkMuted;
            lbl.characterSpacing = 2f;
            lbl.GetComponent<LayoutElement>().preferredHeight = 15;

            var dropRt = CreateRect("Dropdown", wrap);
            dropRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;
            var faceImg = AddBorderedFace(dropRt);

            var drop = dropRt.gameObject.AddComponent<TMP_Dropdown>();
            drop.targetGraphic = faceImg;
            drop.colors = ControlColors();

            var caption = CreateOverlayTmp("Caption", dropRt, "Select", 14, Theme.Ink);
            Stretch(caption.rectTransform);
            caption.rectTransform.offsetMin = new Vector2(10, 2);
            caption.rectTransform.offsetMax = new Vector2(-30, -2);
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.overflowMode = TextOverflowModes.Ellipsis;
            caption.raycastTarget = false;
            drop.captionText = caption;

            // Drawn rather than typed: the geometric-shape glyphs (▾ U+25BE) are not
            // in the LiberationSans atlas TMP ships with and render as tofu.
            var arrow = CreateRect("Arrow", dropRt);
            arrow.anchorMin = new Vector2(1, 0.5f);
            arrow.anchorMax = new Vector2(1, 0.5f);
            arrow.pivot = new Vector2(1, 0.5f);
            arrow.sizeDelta = new Vector2(11, 7);
            arrow.anchoredPosition = new Vector2(-11, 0);
            var arrowImg = arrow.gameObject.AddComponent<Image>();
            arrowImg.sprite = ArrowSprite;
            arrowImg.color = Theme.Accent;
            arrowImg.raycastTarget = false;

            BuildDropdownTemplate(drop, dropRt);

            drop.onValueChanged.AddListener(_ => RefreshPreview());
            return drop;
        }

        private static void BuildDropdownTemplate(TMP_Dropdown drop, RectTransform dropRt)
        {
            var template = CreateRect("Template", dropRt);
            template.gameObject.SetActive(false);
            template.anchorMin = new Vector2(0, 0);
            template.anchorMax = new Vector2(1, 0);
            template.pivot = new Vector2(0.5f, 1);
            template.anchoredPosition = new Vector2(0, 2);
            template.sizeDelta = new Vector2(0, 240);
            template.gameObject.AddComponent<Image>().color = Theme.ControlEdge;

            var templateScroll = template.gameObject.AddComponent<ScrollRect>();
            templateScroll.horizontal = false;
            templateScroll.movementType = ScrollRect.MovementType.Clamped;
            templateScroll.scrollSensitivity = 24f;

            var tViewport = CreateRect("Viewport", template);
            Stretch(tViewport);
            tViewport.offsetMin = new Vector2(2, 2);
            tViewport.offsetMax = new Vector2(-2, -2);
            tViewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            tViewport.gameObject.AddComponent<Image>().color = Color.white;
            templateScroll.viewport = tViewport;

            var tContent = CreateRect("Content", tViewport);
            tContent.anchorMin = new Vector2(0, 1);
            tContent.anchorMax = new Vector2(1, 1);
            tContent.pivot = new Vector2(0.5f, 1);
            tContent.sizeDelta = new Vector2(0, 34);
            templateScroll.content = tContent;

            var item = CreateRect("Item", tContent);
            item.anchorMin = new Vector2(0, 0.5f);
            item.anchorMax = new Vector2(1, 0.5f);
            item.sizeDelta = new Vector2(0, 34);
            var itemToggle = item.gameObject.AddComponent<Toggle>();
            var itemBg = item.gameObject.AddComponent<Image>();
            itemBg.color = Color.white; // tinted via ColorBlock
            itemToggle.targetGraphic = itemBg;

            var itemColors = ColorBlock.defaultColorBlock;
            itemColors.normalColor = Theme.ControlFace;
            itemColors.highlightedColor = Theme.ControlHover;
            itemColors.pressedColor = Theme.SelectFill;
            itemColors.selectedColor = Theme.ControlHover;
            itemColors.fadeDuration = 0.05f;
            itemToggle.colors = itemColors;

            // Selection marker: an opaque accent bar down the left edge. A full-bleed
            // translucent overlay would wash out the item text instead.
            var itemCheck = CreateRect("Item Checkmark", item);
            itemCheck.anchorMin = new Vector2(0, 0);
            itemCheck.anchorMax = new Vector2(0, 1);
            itemCheck.pivot = new Vector2(0, 0.5f);
            itemCheck.sizeDelta = new Vector2(5, 0);
            var checkImg = itemCheck.gameObject.AddComponent<Image>();
            checkImg.color = Theme.Accent;
            itemToggle.graphic = checkImg;

            var itemLabel = CreateOverlayTmp("Item Label", item, "Option", 13, Theme.Ink);
            Stretch(itemLabel.rectTransform);
            itemLabel.rectTransform.offsetMin = new Vector2(14, 0);
            itemLabel.rectTransform.offsetMax = new Vector2(-8, 0);
            itemLabel.alignment = TextAlignmentOptions.MidlineLeft;
            itemLabel.textWrappingMode = TextWrappingModes.NoWrap;
            itemLabel.overflowMode = TextOverflowModes.Ellipsis;
            itemLabel.raycastTarget = false;

            drop.template = template;
            drop.itemText = itemLabel;
        }

        private (Slider, TMP_Text) AddSlider(Transform parent, string label, float min, float max, float value)
        {
            var wrap = CreateRect($"SL_{label}", parent);
            var wrapV = wrap.gameObject.AddComponent<VerticalLayoutGroup>();
            wrapV.spacing = 3;
            wrapV.childControlWidth = true;
            wrapV.childControlHeight = true;
            wrapV.childForceExpandWidth = true;
            wrap.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;

            var header = CreateRect("H", wrap);
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 15;
            var headerH = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerH.childControlWidth = true;
            headerH.childForceExpandWidth = true;

            var lbl = CreateTmp("L", header, label, 11, FontStyles.Bold);
            lbl.color = Theme.InkMuted;
            lbl.characterSpacing = 2f;
            var val = CreateTmp("V", header, $"{value:0}%", 12, FontStyles.Bold);
            val.alignment = TextAlignmentOptions.MidlineRight;
            val.color = Theme.Accent;

            var sliderRt = CreateRect("Slider", wrap);
            sliderRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            AddBorderedFace(sliderRt);

            var fillArea = CreateRect("Fill Area", sliderRt);
            Stretch(fillArea);
            fillArea.offsetMin = new Vector2(6, 9);
            fillArea.offsetMax = new Vector2(-6, -9);
            var fill = CreateRect("Fill", fillArea);
            Stretch(fill);
            fill.gameObject.AddComponent<Image>().color = Theme.Accent;

            var handleArea = CreateRect("Handle Slide Area", sliderRt);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(8, 0);
            handleArea.offsetMax = new Vector2(-8, 0);
            var handle = CreateRect("Handle", handleArea);
            handle.sizeDelta = new Vector2(16, 20);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = Color.white;

            var slider = sliderRt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            var sliderColors = ColorBlock.defaultColorBlock;
            sliderColors.normalColor = Theme.Ink;
            sliderColors.highlightedColor = Theme.Accent;
            sliderColors.pressedColor = Theme.Accent;
            slider.colors = sliderColors;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;
            slider.value = value;
            slider.onValueChanged.AddListener(_ => RefreshPreview());
            return (slider, val);
        }

        private TMP_InputField AddInput(Transform parent, string label, string defaultText)
        {
            var wrap = CreateRect($"IN_{label}", parent);
            var wrapV = wrap.gameObject.AddComponent<VerticalLayoutGroup>();
            wrapV.spacing = 3;
            wrapV.childControlWidth = true;
            wrapV.childControlHeight = true;
            wrapV.childForceExpandWidth = true;
            wrap.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;

            var lbl = CreateTmp("L", wrap, label, 11, FontStyles.Bold);
            lbl.color = Theme.InkMuted;
            lbl.characterSpacing = 2f;
            lbl.GetComponent<LayoutElement>().preferredHeight = 15;

            var fieldRt = CreateRect("Field", wrap);
            fieldRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;
            var faceImg = AddBorderedFace(fieldRt);

            var textArea = CreateRect("Text Area", fieldRt);
            Stretch(textArea);
            textArea.offsetMin = new Vector2(10, 4);
            textArea.offsetMax = new Vector2(-10, -4);
            textArea.gameObject.AddComponent<RectMask2D>();

            var text = CreateTmp("Text", textArea, defaultText, 14, FontStyles.Bold, withLayout: false);
            Stretch(text.rectTransform);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = Theme.Ink;

            var placeholder = CreateTmp("Placeholder", textArea, label, 14, FontStyles.Italic, withLayout: false);
            Stretch(placeholder.rectTransform);
            placeholder.color = Theme.InkMuted;
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;

            var input = fieldRt.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = textArea;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.targetGraphic = faceImg;
            input.colors = ControlColors();
            input.caretColor = Theme.Accent;
            input.customCaretColor = true;
            input.selectionColor = new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.3f);
            input.text = defaultText;
            input.onEndEdit.AddListener(_ => RefreshPreview());
            input.onSubmit.AddListener(_ => RefreshPreview());
            return input;
        }

        private Button AddButton(Transform parent, string label, Action onClick)
        {
            var rt = CreateRect($"BTN_{label}", parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 44;
            le.minHeight = 44;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = Color.white;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Theme.Accent;
            colors.highlightedColor = Lighten(Theme.Accent, 0.14f);
            colors.pressedColor = Lighten(Theme.Accent, -0.10f);
            colors.selectedColor = Theme.Accent;
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var tmp = CreateTmp("T", rt, label, 14, FontStyles.Bold, withLayout: false);
            Stretch(tmp.rectTransform);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Theme.AccentText;
            tmp.characterSpacing = 4f;
            tmp.raycastTarget = false;
            return btn;
        }

        private static Sprite _arrowSprite;

        /// <summary>Solid down-triangle, drawn white so Image.color can tint it.</summary>
        private static Sprite ArrowSprite
        {
            get
            {
                if (_arrowSprite != null) return _arrowSprite;

                const int s = 32;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "DropdownArrow",
                };

                var px = new Color32[s * s];
                var clear = new Color32(255, 255, 255, 0);
                var solid = new Color32(255, 255, 255, 255);
                for (int i = 0; i < px.Length; i++) px[i] = clear;

                // Texture origin is bottom-left, so the apex sits at y = 0.
                for (int y = 0; y < s; y++)
                {
                    float t = y / (float)(s - 1);
                    int half = Mathf.RoundToInt(t * (s / 2f));
                    for (int x = s / 2 - half; x <= s / 2 + half; x++)
                        if (x >= 0 && x < s) px[y * s + x] = solid;
                }

                tex.SetPixels32(px);
                tex.Apply(false, false);
                _arrowSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
                return _arrowSprite;
            }
        }

        private static Color Lighten(Color c, float amount) => new(
            Mathf.Clamp01(c.r + amount),
            Mathf.Clamp01(c.g + amount),
            Mathf.Clamp01(c.b + amount),
            c.a);

        private static void SetDrop(TMP_Dropdown drop, string[] options, int index)
        {
            if (drop == null) return;
            drop.ClearOptions();
            drop.AddOptions(new List<string>(options));
            drop.SetValueWithoutNotify(Mathf.Clamp(index, 0, options.Length - 1));
            drop.RefreshShownValue();
            if (drop.captionText != null)
            {
                drop.captionText.color = Theme.Ink;
                if (UiFont != null) drop.captionText.font = UiFont;
            }
            if (drop.itemText != null)
            {
                drop.itemText.color = Theme.Ink;
                if (UiFont != null) drop.itemText.font = UiFont;
            }
        }

        private static T Pick<T>(T[] table, TMP_Dropdown drop, T fallback)
        {
            if (table == null || drop == null || table.Length == 0) return fallback;
            return table[Mathf.Clamp(drop.value, 0, table.Length - 1)];
        }

        private static int PickCode((string label, int code)[] table, TMP_Dropdown drop, int fallback)
        {
            if (table == null || drop == null || table.Length == 0) return fallback;
            return table[Mathf.Clamp(drop.value, 0, table.Length - 1)].code;
        }

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
