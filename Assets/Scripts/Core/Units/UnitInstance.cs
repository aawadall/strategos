// UnitInstance.cs
// A unit on a map: who it belongs to, what symbol it draws as, and where it is.
//
// This is the first thing in the project with a position. SIDCCode carries identity and
// text amplifiers but is a rendering key — it has no place on the ground and no state.
//
// Data only, no Unity component dependencies, same as the rest of Core. MapData's header
// note is the precedent: the 2D rasteriser and the 3D mesh builder both read it and nothing
// else. The same should hold here — movement, engagement, AI and the views all read this,
// and it knows about none of them.
//
// NOT an ORBAT. There is no subordinate tree, no commander, no equipment, no supply. Those
// are Phase 3 proper and none of them are needed to move a unit across a map. The single
// exception is ParentId — see the note on it.

using System;
using UnityEngine;
using Strategos.Maps;
using Strategos.NatoSymbols;

namespace Strategos.Units
{
    [Serializable]
    public class UnitInstance
    {
        // ─── Identity ─────────────────────────────────────────────────────────

        public UnitId Id;

        /// <summary>Which side owns this unit.</summary>
        public SideId Side;

        /// <summary>
        /// Parent formation, or <see cref="UnitId.None"/>.
        ///
        /// **Left as None throughout the sandbox milestone — one field, no behaviour.** It
        /// exists because command at echelon is eventually the core mechanic, and three
        /// later things need the hierarchy: order decomposition down the chain (Phase 5.1),
        /// state rollup (a brigade's strength is the sum of its battalions'), and symbol LOD
        /// (a dot at theatre scale, not fifteen thousand symbols). A parent id makes all
        /// three additive rather than structural.
        ///
        /// When the tree does arrive, a formation should be a *composite* — a battalion is
        /// itself a UnitInstance that happens to own subordinates — not a separate Formation
        /// type. That is what phases.md §3.1 describes, it matches reality, and it is why
        /// every SIDC already carries an echelon.
        /// </summary>
        public UnitId ParentId;

        // ─── Symbology ────────────────────────────────────────────────────────

        /// <summary>
        /// The 20-digit SIDC. Stored raw because <c>SIDCCode.Raw</c> is the documented
        /// source of truth and a string survives serialisation intact, where the parsed
        /// struct would not.
        /// </summary>
        public string Sidc = SIDCCode.Empty.Raw;

        /// <summary>Field T — unit designation, e.g. "1-7 IN". Drawn right of the frame.</summary>
        public string Designation = string.Empty;

        /// <summary>Field M — parent command, e.g. "3 ID". Drawn right of the frame.</summary>
        public string HigherFormation = string.Empty;

        // ─── Capability ───────────────────────────────────────────────────────

        /// <summary>
        /// Which <see cref="UnitCapabilities"/> this unit has, by id — see
        /// <see cref="UnitCatalogue"/>.
        ///
        /// An id rather than the object, so a hundred rifle companies share one set of
        /// numbers instead of carrying a hundred copies, and a scenario file says
        /// "inf-mech" rather than restating every stat. Resolve it through
        /// <see cref="UnitCatalogue.Get"/>, which never returns null.
        /// </summary>
        public string CapabilityId = UnitCatalogue.InfantryFoot;

        // ─── State ────────────────────────────────────────────────────────────
        //
        // Everything below is per-instance and mutable. Two units of the same type share
        // one UnitCapabilities and have their own copy of all of this.

        /// <summary>
        /// Position in **fractional cell coordinates**, matching MapData throughout.
        ///
        /// A cell coordinate names a sample point, not a square — the same convention
        /// MapData.SampleElevation and MapViewport use. Keeping units in cell space rather
        /// than world metres is what makes world position, MGRS and terrain lookup all fall
        /// out of the map without a second transform to keep in step.
        /// </summary>
        public Vector2 Cell;

        /// <summary>
        /// Combat power as a percentage. A game amplifier with no APP-6D equivalent — only
        /// the + / - / ± Field F marker is standard.
        /// </summary>
        /// <remarks>
        /// **A float, and it has to be.** Firepower is authored per *minute* and the simulation
        /// steps per *second*, so a tick of fire is a fraction of a strength point. Held as an
        /// integer, every exchange rounds to zero and two units shoot at each other for ever
        /// without either one weakening — which reads as a broken combat model rather than as
        /// a rounding bug.
        ///
        /// Display rounds through <see cref="StrengthPercent"/>. Do not format the raw float
        /// into a symbol's amplifier: <c>StrengthLabel</c> is part of the sprite cache key, so
        /// "93.47184" would bake a new sprite every tick.
        /// </remarks>
        [Range(0, 100)] public float Strength = 100f;

        /// <summary>Strength as a whole percentage, for display.</summary>
        public int StrengthPercent => Mathf.Clamp(Mathf.RoundToInt(Strength), 0, 100);

        /// <summary>
        /// Strength rounded to the nearest 5, which is what the *symbol* is baked from.
        /// </summary>
        /// <remarks>
        /// A symbol is a glance-level object: its combat-power bar is a few pixels tall on the
        /// map and cannot show one percentage point. Banding is not cosmetic, though — it is
        /// what bounds the bake. Every distinct strength label is a separate cache entry and a
        /// separate procedural draw, so an unbanded figure re-bakes a unit's symbol about a
        /// hundred times as it dies, and does it in a burst at high time compression. Banding
        /// cuts that to twenty-one.
        ///
        /// A surviving unit never bands to 0: an empty gauge on a unit that is still fighting
        /// says the wrong thing, and zero is reserved for <see cref="IsDestroyed"/>.
        /// </remarks>
        public int StrengthBand
        {
            get
            {
                if (IsDestroyed) return 0;
                return Mathf.Clamp(Mathf.RoundToInt(Strength / 5f) * 5, 5, 100);
            }
        }

        /// <summary>
        /// True once combat power is spent. The unit stays in the scenario and stops
        /// contributing: <see cref="Effectiveness"/> is already zero here, so it neither
        /// inflicts nor suffers anything further.
        ///
        /// Removal from the map, casualty tracking and reconstitution are not this — they are
        /// #14 and Phase 4.4. This is only the floor.
        /// </summary>
        public bool IsDestroyed => Strength <= 0.0001f;

        /// <summary>
        /// Freshness, 0–100. Falls with movement, action and time without rest; caps what
        /// the unit can achieve regardless of its strength. A full-strength exhausted unit
        /// is not a fresh one.
        /// </summary>
        [Range(0, 100)] public float Readiness = 100f;

        /// <summary>
        /// How pinned the unit is, 0–100. Rises under fire and decays when not. Distinct
        /// from strength because suppression is recoverable and casualties are not.
        /// </summary>
        [Range(0, 100)] public float Suppression;

        /// <summary>What the unit is doing, which modifies what it can do.</summary>
        public Posture Posture = Posture.Halted;

        /// <summary>Consumables remaining, as percentages of a full load.</summary>
        public SupplyLevels Supply = SupplyLevels.Full;

        public UnitInstance() { }

        public UnitInstance(UnitId id, SideId side, string sidc, Vector2 cell,
            string designation = "", string higherFormation = "", float strength = 100f,
            string capabilityId = UnitCatalogue.InfantryFoot)
        {
            Id = id;
            Side = side;
            Sidc = sidc;
            Cell = cell;
            Designation = designation;
            HigherFormation = higherFormation;
            Strength = Mathf.Clamp(strength, 0f, 100f);
            CapabilityId = capabilityId;
            ParentId = UnitId.None;
            Supply = SupplyLevels.Full;
        }

        /// <summary>
        /// How much of its nominal capability the unit can actually deliver, 0–1.
        ///
        /// Strength, readiness and suppression multiply rather than add: a unit that is
        /// halved in any one of them is halved overall, and a unit ruined in one cannot be
        /// rescued by being fresh in another.
        /// </summary>
        public float Effectiveness =>
            Mathf.Clamp01(Strength / 100f) *
            Mathf.Clamp01(Readiness / 100f) *
            Mathf.Clamp01(1f - Suppression / 100f);

        /// <summary>Convenience: this unit's capabilities from <paramref name="catalogue"/>.</summary>
        public UnitCapabilities Capabilities(UnitCatalogue catalogue) =>
            catalogue == null ? UnitCatalogue.Fallback : catalogue.Get(CapabilityId);

        // ─── Derived ──────────────────────────────────────────────────────────

        /// <summary>
        /// The symbol to draw, combining the stored digits with this unit's own text
        /// amplifiers. Parsed on demand — callers rendering many units should cache it.
        /// </summary>
        public SIDCCode ToSidcCode()
        {
            if (!SIDCParser.TryParse(Sidc, out var code))
                code = SIDCCode.Empty;

            code.Designation = Designation ?? string.Empty;
            code.HigherFormation = HigherFormation ?? string.Empty;
            // Banded, not raw: this string is part of the sprite cache key, so every distinct
            // value is another bake and another cached texture.
            code.StrengthLabel = StrengthBand.ToString();
            return code;
        }

        /// <summary>World position on <paramref name="map"/>, y being elevation in metres.</summary>
        public Vector3 WorldPosition(MapData map) =>
            map == null ? Vector3.zero : map.CellToWorld(Cell.x, Cell.y);

        /// <summary>Ground elevation under the unit, in metres AMSL.</summary>
        public float Elevation(MapData map) =>
            map == null ? 0f : map.SampleElevation(Cell.x, Cell.y);

        /// <summary>MGRS grid reference, to <paramref name="digits"/> of easting/northing.</summary>
        public string Mgrs(MapData map, int digits = 4) =>
            map == null ? string.Empty : MapCoordinates.FormatMgrs(map.Header, Cell.x, Cell.y, digits);

        /// <summary>The landcover the unit is standing on.</summary>
        public LandcoverClass Landcover(MapData map)
        {
            if (map == null) return LandcoverClass.Open;
            int cx = Mathf.Clamp(Mathf.RoundToInt(Cell.x), 0, map.Width - 1);
            int cy = Mathf.Clamp(Mathf.RoundToInt(Cell.y), 0, map.Height - 1);
            return map.GetLandcover(cx, cy);
        }

        /// <summary>True while the unit is inside the map's bounds.</summary>
        public bool IsOnMap(MapData map) =>
            map != null &&
            Cell.x >= -0.5f && Cell.x <= map.Width - 0.5f &&
            Cell.y >= -0.5f && Cell.y <= map.Height - 0.5f;

        public override string ToString() =>
            $"{Id} {(string.IsNullOrEmpty(Designation) ? Sidc : Designation)} " +
            $"@ ({Cell.x:0.##}, {Cell.y:0.##}) {StrengthPercent}%";
    }
}
