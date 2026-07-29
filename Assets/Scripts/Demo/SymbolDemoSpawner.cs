// SymbolDemoSpawner.cs
// Spawns a 4×12 grid (affiliations × echelons) of NATO symbols on scene Start.
// Uses SymbolFactory / NatoSymbolComposer (procedural when no database).

using TMPro;
using UnityEngine;
using Strategos.NatoSymbols;

namespace Strategos.Demo
{
    public class SymbolDemoSpawner : MonoBehaviour
    {
        [Header("Symbol Database (optional)")]
        [Tooltip("Assign NatoSymbolDatabase to use real sprites. Leave null for procedural generation.")]
        [SerializeField] private NatoSymbolDatabase _database;

        [Header("Layout")]
        [SerializeField] private float _cellSize    = 1.6f;
        [SerializeField] private float _cellSpacing = 0.4f;

        [Header("Labels")]
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private float         _labelScale = 0.22f;

        private SymbolFactory _factory;

        // -------------------------------------------------------------------------
        // Data tables
        // -------------------------------------------------------------------------

        private static readonly Echelon[] Echelons =
        {
            Echelon.Team,    Echelon.Squad,    Echelon.Section,  Echelon.Platoon,
            Echelon.Company, Echelon.Battalion, Echelon.Regiment, Echelon.Brigade,
            Echelon.Division, Echelon.Corps,   Echelon.Army,     Echelon.Theater,
        };

        private static readonly (Affiliation aff, string label)[] Affiliations =
        {
            (Affiliation.Friend,  "FRIEND"),
            (Affiliation.Hostile, "HOSTILE"),
            (Affiliation.Neutral, "NEUTRAL"),
            (Affiliation.Unknown, "UNKNOWN"),
        };

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------

        private void Start()
        {
            _factory = SymbolFactory.Create(_database);
            SpawnGrid();
        }

        private void OnDestroy()
        {
            _factory?.ClearCache();
        }

        // -------------------------------------------------------------------------
        // Grid construction
        // -------------------------------------------------------------------------

        private void SpawnGrid()
        {
            float step   = _cellSize + _cellSpacing;
            float labelY = step * 0.85f;  // column headers above row 0

            // --- Column headers (echelon names) ---
            for (int col = 0; col < Echelons.Length; col++)
            {
                float x = col * step;
                SpawnLabel(EchelonLabel(Echelons[col]),
                    new Vector3(x, labelY, 0),
                    _labelScale * 0.75f,
                    new Color(0.7f, 0.7f, 0.7f));
            }

            // --- Rows ---
            for (int row = 0; row < Affiliations.Length; row++)
            {
                var (aff, affLabel) = Affiliations[row];
                float y = -row * step;

                // Row header (affiliation label on the left)
                SpawnLabel(affLabel,
                    new Vector3(-step * 1.4f, y, 0),
                    _labelScale,
                    AffiliationColour.ForAffiliation(aff));

                // Symbols for each echelon
                for (int col = 0; col < Echelons.Length; col++)
                {
                    var sidc = BuildSIDC(aff, Echelons[col]);
                    SpawnSymbol(sidc, new Vector3(col * step, y, 0));
                }
            }

            // Centre the whole grid under this transform for easy camera targeting.
            float cx = (Echelons.Length - 1) * step * 0.5f;
            float cy = -(Affiliations.Length - 1) * step * 0.5f;
            transform.position = new Vector3(-cx, -cy, 0);
        }

        // -------------------------------------------------------------------------
        // Symbol spawning
        // -------------------------------------------------------------------------

        private void SpawnSymbol(SIDCCode sidc, Vector3 localPos)
        {
            var go = new GameObject($"Sym_{sidc.Affiliation}_{sidc.Echelon}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;

            // Compose (factory + decorators) then bake via legacy GetSymbolSprite adapter.
            var sr    = go.AddComponent<SpriteRenderer>();
            sr.sprite = _factory.GetSymbolSprite(sidc, 256);

            // Sprite is 256px at 256 PPU = 1 world unit. Scale to cell size.
            go.transform.localScale = Vector3.one * _cellSize;
        }

        // -------------------------------------------------------------------------
        // Label spawning
        // -------------------------------------------------------------------------

        private void SpawnLabel(string text, Vector3 localPos, float fontSize, Color color)
        {
            var go  = new GameObject($"Label_{text}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;

            var tmp                = go.AddComponent<TextMeshPro>();
            tmp.text               = text;
            tmp.fontSize           = fontSize;
            tmp.color              = color;
            tmp.alignment          = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode       = TextOverflowModes.Overflow;
            if (_font != null) tmp.font = _font;
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        /// <summary>
        /// Builds a Land Unit Infantry SIDC (APP-6D Annex A 20-digit layout).
        /// Digits: ver(10) + context(0) + identity + set(10) + status(0) + hq(0) +
        ///         echelon + entity(12) + type(11) + subtype/mods(000000).
        /// </summary>
        private static SIDCCode BuildSIDC(Affiliation aff, Echelon echelon)
        {
            var raw = string.Format(
                "10{0}{1}{2:D2}00{3:D2}1211000000",
                (int)SymbolContext.Reality,
                (int)aff,
                (int)SymbolSet.LandUnit,
                (int)echelon);

            if (SIDCParser.TryParse(raw, out var code))
                return code;

            return new SIDCCode
            {
                Raw         = raw,
                Context     = SymbolContext.Reality,
                Affiliation = aff,
                SymbolSet   = SymbolSet.LandUnit,
                Echelon     = echelon,
                EntityCode  = 12,
                EntityType  = 11,
                Status      = UnitStatus.Present,
            };
        }

        private static string EchelonLabel(Echelon e) => e switch
        {
            Echelon.Team      => "○\nTeam",
            Echelon.Squad     => "•\nSquad",
            Echelon.Section   => "••\nSection",
            Echelon.Platoon   => "•••\nPlatoon",
            Echelon.Company   => "•••\nCompany",
            Echelon.Battalion => "I\nBattalion",
            Echelon.Regiment  => "II\nRegiment",
            Echelon.Brigade   => "X\nBrigade",
            Echelon.Division  => "XX\nDivision",
            Echelon.Corps     => "XXX\nCorps",
            Echelon.Army      => "XXXX\nArmy",
            Echelon.Theater   => "XXXXXX\nTheater",
            _                 => e.ToString(),
        };
    }
}
