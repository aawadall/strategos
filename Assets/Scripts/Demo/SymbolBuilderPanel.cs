// SymbolBuilderPanel.cs
// Off-white map stage + topo underlay + SIDC + component breakdown table.
// Right rail: Command & Conquer–style dark olive dropdown control panel.

using System;
using System.Collections.Generic;
using System.Text;
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
        private TMP_Text _breakdownTable;
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

        // C&C-style palette (dark olive sidebar) + off-white map stage
        private static class Theme
        {
            public static readonly Color StageBg       = new(0.94f, 0.93f, 0.88f, 1f); // off-white paper
            public static readonly Color MapPaper      = new(0.90f, 0.88f, 0.80f, 1f);
            public static readonly Color CardBg        = new(0.98f, 0.97f, 0.94f, 1f);
            public static readonly Color CardLine      = new(0.72f, 0.70f, 0.62f, 1f);
            public static readonly Color TextInk       = new(0.12f, 0.14f, 0.12f, 1f);
            public static readonly Color TextMuted     = new(0.35f, 0.36f, 0.32f, 1f);
            public static readonly Color SidcInk       = new(0.08f, 0.28f, 0.22f, 1f);

            // C&C / Westwood sidebar
            public static readonly Color SidebarBg     = new(0.10f, 0.14f, 0.08f, 1f);
            public static readonly Color SidebarEdge   = new(0.22f, 0.28f, 0.12f, 1f);
            public static readonly Color ControlFace   = new(0.16f, 0.20f, 0.10f, 1f);
            public static readonly Color ControlHi     = new(0.28f, 0.34f, 0.16f, 1f);
            public static readonly Color Gold          = new(0.92f, 0.78f, 0.22f, 1f);
            public static readonly Color GoldDim       = new(0.70f, 0.58f, 0.16f, 1f);
            public static readonly Color TextOnDark    = new(0.95f, 0.92f, 0.55f, 1f);
            public static readonly Color ButtonFace    = new(0.18f, 0.24f, 0.10f, 1f);
            public static readonly Color SliderFill    = new(0.72f, 0.62f, 0.12f, 1f);
        }

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

                _uiFont = TMP_Settings.defaultFontAsset;
                if (_uiFont != null) return _uiFont;

                // Project may not have TMP Essential Resources imported — build from OS font.
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
                _sidcLabel.text = string.IsNullOrEmpty(code.Raw) ? "—" : code.Raw;

            if (_breakdownTable != null)
                _breakdownTable.text = BuildBreakdownTable(code);
        }

        private string BuildBreakdownTable(SIDCCode code)
        {
            var sb = new StringBuilder(512);
            sb.AppendLine("<b>COMPONENT</b>          <b>VALUE</b>                 <b>MEANING</b>");
            sb.AppendLine("─────────────────────────────────────────────────────────────");
            Row(sb, "Frame / Identity", code.Affiliation.ToString(), FrameMeaning(code.Affiliation));
            Row(sb, "Symbol set", code.SymbolSet.ToString(), "Land unit (APP-6D set 10)");
            Row(sb, "Status", StatusLabel(code.Status), StatusMeaning(code.Status));
            Row(sb, "HQ / TF / Feint", Prettify(code.HqTfDummy.ToString()), HqMeaning(code.HqTfDummy));
            Row(sb, "Echelon", code.Echelon.ToString(), EchelonMeaning(code.Echelon));
            Row(sb, "Main icon", LabelForUnit(code.EntityCode), "Unit function inside the frame");
            Row(sb, "Variant", VariantLabel(code.EntityType), "Equipment / mobility flavour");
            Row(sb, "Sector 1 mod", ModLabel(code.Modifier1), "Upper octagon modifier");
            Row(sb, "Sector 2 mod", ModLabel(code.Modifier2), "Lower octagon modifier");
            Row(sb, "Designation", string.IsNullOrEmpty(code.Designation) ? "—" : code.Designation, "Field T — unique unit label");
            Row(sb, "Higher formation", string.IsNullOrEmpty(code.HigherFormation) ? "—" : code.HigherFormation, "Field M — parent command");
            Row(sb, "Strength", StrengthDisplay(code), "Field F — combat power / health");
            return sb.ToString();
        }

        private static void Row(StringBuilder sb, string a, string b, string c)
        {
            sb.Append(Pad(a, 18));
            sb.Append("  ");
            sb.Append(Pad(b, 20));
            sb.Append("  ");
            sb.AppendLine(c);
        }

        private static string Pad(string s, int w)
        {
            s ??= "";
            if (s.Length > w) return s.Substring(0, w - 1) + "…";
            return s.PadRight(w);
        }

        private static string FrameMeaning(Affiliation a) => a switch
        {
            Affiliation.Friend or Affiliation.AssumedFriend => "Blue rectangle frame (friendly)",
            Affiliation.Hostile or Affiliation.Suspect => "Red diamond frame (hostile)",
            Affiliation.Neutral => "Green square frame (neutral)",
            Affiliation.Unknown or Affiliation.Pending => "Yellow ellipse frame (unknown)",
            _ => "Standard identity frame",
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
            UnitStatus.Present => "Solid frame — confirmed location",
            UnitStatus.AnticipatedPlanned => "Dashed frame — planned / anticipated",
            UnitStatus.PresentDamaged => "Operational condition: damaged",
            UnitStatus.PresentDestroyed => "Operational condition: destroyed",
            UnitStatus.PresentFullyCapable => "Fully capable",
            UnitStatus.PresentFullToCapacity => "At capacity",
            _ => "Status / condition amplifier",
        };

        private static string HqMeaning(HeadquartersTaskForceDummy h) => h switch
        {
            HeadquartersTaskForceDummy.None => "No HQ / TF / feint amplifiers",
            HeadquartersTaskForceDummy.Headquarters => "HQ staff line below frame",
            HeadquartersTaskForceDummy.TaskForce => "Task-force bracket above frame",
            HeadquartersTaskForceDummy.TaskForceHeadquarters => "HQ + task-force amplifiers",
            HeadquartersTaskForceDummy.FeintDummy => "Feint / dummy dashes",
            _ => "Combined HQ / TF / feint graphic",
        };

        private static string EchelonMeaning(Echelon e) => e switch
        {
            Echelon.Team => "○ Team / crew",
            Echelon.Squad => "• Squad",
            Echelon.Section => "•• Section",
            Echelon.Platoon => "••• Platoon",
            Echelon.Company => "••• Company / battery / troop",
            Echelon.Battalion => "I Battalion / squadron",
            Echelon.Regiment => "II Regiment / group",
            Echelon.Brigade => "X Brigade",
            Echelon.Division => "XX Division",
            Echelon.Corps => "XXX Corps",
            Echelon.Army => "XXXX Army",
            Echelon.ArmyGroup => "XXXXX Army group / front",
            Echelon.Theater => "XXXXXX Theater",
            Echelon.Command => "Command",
            _ => "Command level mark above frame",
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
                StrengthModifier.Reinforced => s + "  (+)",
                StrengthModifier.Reduced => s + "  (−)",
                StrengthModifier.ReinforcedReduced => s + "  (±)",
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
            SetDrop(_affiliationDrop, Array.ConvertAll(_affiliations, a => a.ToString().ToUpperInvariant()), 0);

            _echelons = new[]
            {
                Echelon.Team, Echelon.Squad, Echelon.Section, Echelon.Platoon,
                Echelon.Company, Echelon.Battalion, Echelon.Regiment, Echelon.Brigade,
                Echelon.Division, Echelon.Corps, Echelon.Army, Echelon.ArmyGroup,
                Echelon.Theater, Echelon.Command,
            };
            SetDrop(_echelonDrop, Array.ConvertAll(_echelons, e => EchelonLabel(e).ToUpperInvariant()), 4);

            _unitTypes = new[]
            {
                LandEntityCode.Infantry, LandEntityCode.Armor, LandEntityCode.Artillery,
                LandEntityCode.Reconnaissance, LandEntityCode.CombatEngineering,
                LandEntityCode.AirDefense, LandEntityCode.Aviation,
                LandEntityCode.SignalsCommunication, LandEntityCode.LogisticsSupport,
                LandEntityCode.Medical, LandEntityCode.Headquarters,
                LandEntityCode.SpecialOperations, LandEntityCode.MissileBallistic,
            };
            SetDrop(_unitTypeDrop, Array.ConvertAll(_unitTypes, u => Prettify(u.ToString()).ToUpperInvariant()), 0);

            _variants = new (string, int)[]
            {
                ("Standard / Foot", 11), ("Mechanized", 12), ("Motorized", 13),
                ("Air Assault", 14), ("Amphibious", 15), ("Mountain", 16),
                ("Arctic", 17), ("Heavy", 18), ("Light", 19),
            };
            SetDrop(_variantDrop, Array.ConvertAll(_variants, v => v.label.ToUpperInvariant()), 0);

            _sectorMods = new (string, int)[]
            {
                ("None", 0),
                ("Airborne", SectorModifierDecorator.ModAirborne),
                ("Air Assault", SectorModifierDecorator.ModAirAssault),
                ("Wheeled", SectorModifierDecorator.ModWheeled),
                ("Mountain", SectorModifierDecorator.ModMountain),
                ("Amphibious", SectorModifierDecorator.ModAmphibious),
            };
            SetDrop(_mod1Drop, Array.ConvertAll(_sectorMods, m => m.label.ToUpperInvariant()), 0);
            SetDrop(_mod2Drop, Array.ConvertAll(_sectorMods, m => m.label.ToUpperInvariant()), 0);

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
            SetDrop(_hqTfDrop, Array.ConvertAll(_hqTf, h => Prettify(h.ToString()).ToUpperInvariant()), 0);

            _statuses = new[]
            {
                UnitStatus.Present, UnitStatus.AnticipatedPlanned,
                UnitStatus.PresentFullyCapable, UnitStatus.PresentDamaged,
                UnitStatus.PresentDestroyed, UnitStatus.PresentFullToCapacity,
            };
            SetDrop(_statusDrop, new[]
            {
                "PRESENT", "PLANNED", "FULLY CAPABLE", "DAMAGED", "DESTROYED", "FULL CAPACITY",
            }, 0);

            _strengthMods = new[]
            {
                StrengthModifier.None, StrengthModifier.Reinforced,
                StrengthModifier.Reduced, StrengthModifier.ReinforcedReduced,
            };
            SetDrop(_strengthModDrop, new[] { "NONE", "REINFORCED (+)", "REDUCED (−)", "REINF. & RED. (±)" }, 0);

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
            rootH.childForceExpandWidth = true;
            rootH.childForceExpandHeight = true;

            BuildStage(root);
            BuildCnCPanel(root);
        }

        private void BuildStage(Transform root)
        {
            var stage = CreateRect("Stage", root);
            stage.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1.55f;
            stage.gameObject.AddComponent<Image>().color = Theme.StageBg;

            var stageV = stage.gameObject.AddComponent<VerticalLayoutGroup>();
            stageV.padding = new RectOffset(36, 28, 28, 28);
            stageV.spacing = 14;
            stageV.childAlignment = TextAnchor.UpperCenter;
            stageV.childControlWidth = true;
            stageV.childControlHeight = false;
            stageV.childForceExpandWidth = true;

            var hdr = CreateTmp("Hdr", stage, "STRATEGOS  ·  APP-6(D) SYMBOL", 20, FontStyles.Bold);
            hdr.alignment = TextAlignmentOptions.Center;
            hdr.color = Theme.TextInk;

            // Map card: topo underlay + symbol on top (separate siblings)
            var mapCard = CreateRect("MapCard", stage);
            mapCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 420;
            mapCard.gameObject.AddComponent<Image>().color = Theme.MapPaper;

            var mapStack = CreateRect("MapStack", mapCard);
            Stretch(mapStack);
            mapStack.offsetMin = new Vector2(10, 10);
            mapStack.offsetMax = new Vector2(-10, -10);

            var topoRt = CreateRect("Topo", mapStack);
            Stretch(topoRt);
            _topoImage = topoRt.gameObject.AddComponent<RawImage>();
            _topoTex = GenerateTopoTexture(640, 480);
            _topoImage.texture = _topoTex;
            _topoImage.color = Color.white;

            var symbolHolder = CreateRect("SymbolHolder", mapStack);
            Stretch(symbolHolder);
            symbolHolder.offsetMin = new Vector2(100, 50);
            symbolHolder.offsetMax = new Vector2(-100, -50);
            var aspect = symbolHolder.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 1f;
            _previewImage = symbolHolder.gameObject.AddComponent<RawImage>();
            _previewImage.color = Color.white;
            _previewImage.texture = Texture2D.whiteTexture;
            _previewImage.raycastTarget = false;

            // SIDC under symbol
            var sidcCard = CreateRect("SidcCard", stage);
            sidcCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 64;
            sidcCard.gameObject.AddComponent<Image>().color = Theme.CardBg;
            var sidcV = sidcCard.gameObject.AddComponent<VerticalLayoutGroup>();
            sidcV.padding = new RectOffset(16, 16, 8, 8);
            sidcV.spacing = 2;
            sidcV.childAlignment = TextAnchor.MiddleCenter;
            sidcV.childControlWidth = true;
            sidcV.childForceExpandWidth = true;

            var sidcCap = CreateTmp("SidcCap", sidcCard, "SYMBOL IDENTIFICATION CODE (SIDC)", 11, FontStyles.Bold);
            sidcCap.alignment = TextAlignmentOptions.Center;
            sidcCap.color = Theme.TextMuted;
            sidcCap.GetComponent<LayoutElement>().preferredHeight = 14;

            _sidcLabel = CreateTmp("Sidc", sidcCard, "—", 22, FontStyles.Bold);
            _sidcLabel.alignment = TextAlignmentOptions.Center;
            _sidcLabel.color = Theme.SidcInk;
            _sidcLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _sidcLabel.GetComponent<LayoutElement>().preferredHeight = 28;

            // Breakdown table
            var tableCard = CreateRect("TableCard", stage);
            var tableLe = tableCard.gameObject.AddComponent<LayoutElement>();
            tableLe.flexibleHeight = 1;
            tableLe.minHeight = 220;
            tableCard.gameObject.AddComponent<Image>().color = Theme.CardBg;

            var tableV = tableCard.gameObject.AddComponent<VerticalLayoutGroup>();
            tableV.padding = new RectOffset(18, 18, 14, 14);
            tableV.spacing = 6;
            tableV.childControlWidth = true;
            tableV.childForceExpandWidth = true;
            tableV.childControlHeight = false;

            var tableTitle = CreateTmp("TableTitle", tableCard, "SYMBOL BREAKDOWN", 13, FontStyles.Bold);
            tableTitle.color = Theme.TextInk;
            tableTitle.GetComponent<LayoutElement>().preferredHeight = 18;

            var divider = CreateRect("Divider", tableCard);
            divider.gameObject.AddComponent<LayoutElement>().preferredHeight = 2;
            divider.gameObject.AddComponent<Image>().color = Theme.CardLine;

            _breakdownTable = CreateTmp("Breakdown", tableCard, "", 12, FontStyles.Normal);
            _breakdownTable.alignment = TextAlignmentOptions.TopLeft;
            _breakdownTable.color = Theme.TextInk;
            _breakdownTable.enableAutoSizing = false;
            _breakdownTable.richText = true;
            _breakdownTable.fontSize = 12.5f;
            _breakdownTable.lineSpacing = -8f;
            var brLe = _breakdownTable.GetComponent<LayoutElement>();
            brLe.flexibleHeight = 1;
            brLe.minHeight = 180;
            // Monospace-ish: TMP default; still readable as columns via padding
        }

        private void BuildCnCPanel(Transform root)
        {
            var panel = CreateRect("CnCPanel", root);
            var panelLe = panel.gameObject.AddComponent<LayoutElement>();
            panelLe.preferredWidth = 440;
            panelLe.minWidth = 400;
            panelLe.flexibleWidth = 0;
            panel.gameObject.AddComponent<Image>().color = Theme.SidebarBg;

            // Gold top bar (C&C chrome)
            var chrome = CreateRect("Chrome", panel);
            chrome.anchorMin = new Vector2(0, 1);
            chrome.anchorMax = new Vector2(1, 1);
            chrome.pivot = new Vector2(0.5f, 1);
            chrome.sizeDelta = new Vector2(0, 36);
            chrome.anchoredPosition = Vector2.zero;
            chrome.gameObject.AddComponent<Image>().color = Theme.Gold;
            var chromeLabel = CreateTmp("ChromeLabel", chrome, "◆  UNIT CONSTRUCTION  ◆", 14, FontStyles.Bold);
            Stretch(chromeLabel.rectTransform);
            chromeLabel.alignment = TextAlignmentOptions.Center;
            chromeLabel.color = Theme.SidebarBg;

            var scrollGo = CreateRect("Scroll", panel);
            Stretch(scrollGo);
            scrollGo.offsetMin = new Vector2(0, 0);
            scrollGo.offsetMax = new Vector2(0, -36);
            var scroll = scrollGo.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 48f;

            var viewport = CreateRect("Viewport", scrollGo);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Theme.SidebarBg;
            scroll.viewport = viewport;

            var content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var contentV = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentV.padding = new RectOffset(14, 14, 14, 28);
            contentV.spacing = 6;
            contentV.childControlWidth = true;
            contentV.childControlHeight = false;
            contentV.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            AddCnCSection(content, "IDENTITY");
            _affiliationDrop = AddCnCDropdown(content, "AFFILIATION");

            AddCnCSection(content, "ECHELON");
            _echelonDrop = AddCnCDropdown(content, "COMMAND LEVEL");

            AddCnCSection(content, "UNIT / EQUIPMENT");
            _unitTypeDrop = AddCnCDropdown(content, "UNIT TYPE");
            _variantDrop = AddCnCDropdown(content, "VARIANT");

            AddCnCSection(content, "MODIFIERS");
            _mod1Drop = AddCnCDropdown(content, "SECTOR 1");
            _mod2Drop = AddCnCDropdown(content, "SECTOR 2");
            _hqTfDrop = AddCnCDropdown(content, "HQ / TF / FEINT");

            AddCnCSection(content, "STATUS / HEALTH");
            _statusDrop = AddCnCDropdown(content, "STATUS");
            _strengthModDrop = AddCnCDropdown(content, "STRENGTH AMP");
            (_strengthSlider, _strengthValueLabel) = AddCnCSlider(content, "STRENGTH %", 0, 100, 100);

            AddCnCSection(content, "LABELS");
            _designationField = AddCnCInput(content, "DESIGNATION", "1-7 IN");
            _formationField = AddCnCInput(content, "HIGHER FORMATION", "3 ID");

            AddCnCButton(content, "▶  BUILD / REFRESH", RefreshPreview);
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
            var paper = new Color32(236, 232, 218, 255);
            var water = new Color32(190, 210, 200, 255);
            var contour = new Color32(110, 100, 70, 255);
            var contourMajor = new Color32(70, 62, 42, 255);
            var gridCol = new Color32(175, 168, 140, 255);

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
        // C&C control helpers
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
            tmp.color = Theme.TextInk;
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

        private void AddCnCSection(Transform parent, string title)
        {
            var bar = CreateRect($"Sec_{title}", parent);
            bar.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
            bar.gameObject.AddComponent<Image>().color = Theme.SidebarEdge;
            var tmp = CreateTmp("T", bar, $"▌ {title}", 12, FontStyles.Bold, withLayout: false);
            Stretch(tmp.rectTransform);
            tmp.rectTransform.offsetMin = new Vector2(8, 0);
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Theme.Gold;
        }

        private TMP_Dropdown AddCnCDropdown(Transform parent, string label)
        {
            var wrap = CreateRect($"DD_{label}", parent);
            var wrapV = wrap.gameObject.AddComponent<VerticalLayoutGroup>();
            wrapV.spacing = 2;
            wrapV.childControlWidth = true;
            wrapV.childControlHeight = true;
            wrapV.childForceExpandWidth = true;
            wrap.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;

            var lbl = CreateTmp("L", wrap, label, 11, FontStyles.Bold);
            lbl.color = Theme.Gold;

            var dropRt = CreateRect("Dropdown", wrap);
            dropRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
            var dropImg = dropRt.gameObject.AddComponent<Image>();
            dropImg.color = Theme.ControlFace;

            var drop = dropRt.gameObject.AddComponent<TMP_Dropdown>();
            drop.targetGraphic = dropImg;

            // Bright caption on dark face — no LayoutElement
            var caption = CreateOverlayTmp("Caption", dropRt, "SELECT", 14, Theme.Gold);
            Stretch(caption.rectTransform);
            caption.rectTransform.offsetMin = new Vector2(10, 2);
            caption.rectTransform.offsetMax = new Vector2(-28, -2);
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            caption.raycastTarget = false;
            drop.captionText = caption;

            var arrow = CreateOverlayTmp("Arrow", dropRt, "▼", 12, Theme.Gold);
            arrow.rectTransform.anchorMin = new Vector2(1, 0);
            arrow.rectTransform.anchorMax = new Vector2(1, 1);
            arrow.rectTransform.pivot = new Vector2(1, 0.5f);
            arrow.rectTransform.sizeDelta = new Vector2(26, 0);
            arrow.rectTransform.anchoredPosition = Vector2.zero;
            arrow.alignment = TextAlignmentOptions.Center;
            arrow.raycastTarget = false;

            var template = CreateRect("Template", dropRt);
            template.gameObject.SetActive(false);
            template.anchorMin = new Vector2(0, 0);
            template.anchorMax = new Vector2(1, 0);
            template.pivot = new Vector2(0.5f, 1);
            template.anchoredPosition = new Vector2(0, 2);
            template.sizeDelta = new Vector2(0, 200);
            template.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.10f, 0.06f, 1f);
            var templateScroll = template.gameObject.AddComponent<ScrollRect>();
            templateScroll.horizontal = false;
            templateScroll.movementType = ScrollRect.MovementType.Clamped;

            var tViewport = CreateRect("Viewport", template);
            Stretch(tViewport);
            tViewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            tViewport.gameObject.AddComponent<Image>().color = Color.white;
            templateScroll.viewport = tViewport;

            var tContent = CreateRect("Content", tViewport);
            tContent.anchorMin = new Vector2(0, 1);
            tContent.anchorMax = new Vector2(1, 1);
            tContent.pivot = new Vector2(0.5f, 1);
            tContent.sizeDelta = new Vector2(0, 32);
            templateScroll.content = tContent;

            var item = CreateRect("Item", tContent);
            item.anchorMin = new Vector2(0, 0.5f);
            item.anchorMax = new Vector2(1, 0.5f);
            item.sizeDelta = new Vector2(0, 32);
            var itemToggle = item.gameObject.AddComponent<Toggle>();
            var itemBg = item.gameObject.AddComponent<Image>();
            itemBg.color = Theme.ControlFace;
            itemToggle.targetGraphic = itemBg;

            var itemCheck = CreateRect("Item Checkmark", item);
            Stretch(itemCheck);
            var checkImg = itemCheck.gameObject.AddComponent<Image>();
            checkImg.color = new Color(0.92f, 0.78f, 0.22f, 0.35f);
            itemToggle.graphic = checkImg;

            var itemLabel = CreateOverlayTmp("Item Label", item, "OPTION", 13, Theme.Gold);
            Stretch(itemLabel.rectTransform);
            itemLabel.rectTransform.offsetMin = new Vector2(10, 0);
            itemLabel.rectTransform.offsetMax = new Vector2(-6, 0);
            itemLabel.alignment = TextAlignmentOptions.MidlineLeft;
            itemLabel.raycastTarget = false;

            drop.template = template;
            drop.itemText = itemLabel;
            drop.onValueChanged.AddListener(_ => RefreshPreview());
            return drop;
        }

        private (Slider, TMP_Text) AddCnCSlider(Transform parent, string label, float min, float max, float value)
        {
            var wrap = CreateRect($"SL_{label}", parent);
            var wrapV = wrap.gameObject.AddComponent<VerticalLayoutGroup>();
            wrapV.spacing = 2;
            wrapV.childControlWidth = true;
            wrapV.childControlHeight = true;
            wrapV.childForceExpandWidth = true;
            wrap.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;

            var header = CreateRect("H", wrap);
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 14;
            var headerH = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerH.childControlWidth = true;
            headerH.childForceExpandWidth = true;

            var lbl = CreateTmp("L", header, label, 11, FontStyles.Bold);
            lbl.color = Theme.Gold;
            var val = CreateTmp("V", header, $"{value:0}%", 11, FontStyles.Bold);
            val.alignment = TextAlignmentOptions.MidlineRight;
            val.color = Theme.Gold;

            var sliderRt = CreateRect("Slider", wrap);
            sliderRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
            sliderRt.gameObject.AddComponent<Image>().color = Theme.ControlFace;

            var fillArea = CreateRect("Fill Area", sliderRt);
            Stretch(fillArea);
            fillArea.offsetMin = new Vector2(6, 8);
            fillArea.offsetMax = new Vector2(-6, -8);
            var fill = CreateRect("Fill", fillArea);
            Stretch(fill);
            fill.gameObject.AddComponent<Image>().color = Theme.SliderFill;

            var handleArea = CreateRect("Handle Slide Area", sliderRt);
            Stretch(handleArea);
            var handle = CreateRect("Handle", handleArea);
            handle.sizeDelta = new Vector2(16, 16);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = Theme.Gold;

            var slider = sliderRt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;
            slider.value = value;
            slider.onValueChanged.AddListener(_ => RefreshPreview());
            return (slider, val);
        }

        private TMP_InputField AddCnCInput(Transform parent, string label, string defaultText)
        {
            var wrap = CreateRect($"IN_{label}", parent);
            var wrapV = wrap.gameObject.AddComponent<VerticalLayoutGroup>();
            wrapV.spacing = 2;
            wrapV.childControlWidth = true;
            wrapV.childControlHeight = true;
            wrapV.childForceExpandWidth = true;
            wrap.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;

            var lbl = CreateTmp("L", wrap, label, 11, FontStyles.Bold);
            lbl.color = Theme.GoldDim;
            lbl.GetComponent<LayoutElement>().preferredHeight = 14;

            var fieldRt = CreateRect("Field", wrap);
            fieldRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
            var fieldImg = fieldRt.gameObject.AddComponent<Image>();
            fieldImg.color = Theme.ControlFace;

            var textArea = CreateRect("Text Area", fieldRt);
            Stretch(textArea);
            textArea.offsetMin = new Vector2(10, 4);
            textArea.offsetMax = new Vector2(-10, -4);
            textArea.gameObject.AddComponent<RectMask2D>();

            var text = CreateTmp("Text", textArea, defaultText, 13, FontStyles.Bold, withLayout: false);
            Stretch(text.rectTransform);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = Theme.Gold;

            var placeholder = CreateTmp("Placeholder", textArea, label, 13, FontStyles.Italic, withLayout: false);
            Stretch(placeholder.rectTransform);
            placeholder.color = Theme.GoldDim;
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;

            var input = fieldRt.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = textArea;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.targetGraphic = fieldImg;
            input.text = defaultText;
            input.onEndEdit.AddListener(_ => RefreshPreview());
            input.onSubmit.AddListener(_ => RefreshPreview());
            return input;
        }

        private Button AddCnCButton(Transform parent, string label, Action onClick)
        {
            var rt = CreateRect($"BTN_{label}", parent);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 42;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = Theme.ButtonFace;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = Theme.ControlHi;
            colors.pressedColor = Theme.SidebarEdge;
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var tmp = CreateTmp("T", rt, label, 13, FontStyles.Bold, withLayout: false);
            Stretch(tmp.rectTransform);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Theme.Gold;
            tmp.raycastTarget = false;
            return btn;
        }

        private static void SetDrop(TMP_Dropdown drop, string[] options, int index)
        {
            if (drop == null) return;
            drop.ClearOptions();
            drop.AddOptions(new List<string>(options));
            drop.SetValueWithoutNotify(Mathf.Clamp(index, 0, options.Length - 1));
            drop.RefreshShownValue();
            if (drop.captionText != null)
            {
                drop.captionText.color = Theme.Gold;
                if (UiFont != null) drop.captionText.font = UiFont;
            }
            if (drop.itemText != null)
            {
                drop.itemText.color = Theme.Gold;
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
            Echelon.Team => "Team / Crew ○",
            Echelon.Squad => "Squad •",
            Echelon.Section => "Section ••",
            Echelon.Platoon => "Platoon •••",
            Echelon.Company => "Company •••",
            Echelon.Battalion => "Battalion I",
            Echelon.Regiment => "Regiment II",
            Echelon.Brigade => "Brigade X",
            Echelon.Division => "Division XX",
            Echelon.Corps => "Corps XXX",
            Echelon.Army => "Army XXXX",
            Echelon.ArmyGroup => "Army Group",
            Echelon.Theater => "Theater",
            Echelon.Command => "Command",
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
