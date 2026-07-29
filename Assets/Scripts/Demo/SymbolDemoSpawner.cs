// SymbolDemoSpawner.cs
// Spawns a 4×12 grid (affiliations × echelons) of NATO symbols on scene Start.
// Uses PlaceholderSymbolFactory when NatoSymbolDatabase is not assigned.
// Accessible via: GameObject "SymbolDemoSpawner" in Assets/Scenes/Demo/SymbolDemo.unity

using TMPro;
using UnityEngine;
using Strategos.NatoSymbols;

namespace Strategos.Demo
{
    public class SymbolDemoSpawner : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------

        [Header("Symbol Database (optional)")]
        [Tooltip("Assign NatoSymbolDatabase to use real sprites. Leave null for placeholder mode.")]
        [SerializeField] private NatoSymbolDatabase _database;

        [Header("Layout")]
        [SerializeField] private float _cellSize    = 1.6f;
        [SerializeField] private float _cellSpacing = 0.4f;

        [Header("Labels")]
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private float         _labelScale = 0.22f;

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
            SpawnGrid();
        }

        private void OnDestroy()
        {
            PlaceholderSymbolFactory.ClearCache();
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

            if (_database != null)
            {
                // Use the real NatoSymbolView (art-backed)
                var view = go.AddComponent<NatoSymbolView>();
                view.SetSymbol(sidc);
            }
            else
            {
                // Placeholder: colored sprite
                var sr     = go.AddComponent<SpriteRenderer>();
                sr.sprite  = PlaceholderSymbolFactory.Get(sidc);
                sr.drawMode = SpriteDrawMode.Sliced;
                // Scale sprite to fill cell size; sprite is 128px = 1 world unit by default
                go.transform.localScale = Vector3.one * _cellSize;
            }
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
            tmp.enableWordWrapping = false;
            tmp.overflowMode       = TextOverflowModes.Overflow;
            if (_font != null) tmp.font = _font;
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        /// <summary>
        /// Builds a minimal Infantry SIDC for the given affiliation and echelon.
        /// Format: version(2) + affiliation(1) + land(2) + status(1) + flags(1) +
        ///         echelon(2) + infantry(2) + padding(9)
        /// </summary>
        private static SIDCCode BuildSIDC(Affiliation aff, Echelon echelon)
        {
            var raw = $"10{(int)aff}{(int)SymbolDimension.Land:D2}00{(int)echelon:D2}12000000000";
            if (SIDCParser.TryParse(raw, out var code))
                return code;

            // Fallback: manually set fields if parse fails.
            return new SIDCCode
            {
                Raw         = raw,
                Affiliation = aff,
                Dimension   = SymbolDimension.Land,
                Echelon     = echelon,
                EntityCode  = 12,
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
