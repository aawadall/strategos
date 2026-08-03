// EngagementResolver.cs
// What happens when one unit fires at another for one tick.
//
// The first thing in the project that simulates rather than draws or configures: state that
// changes as a consequence of rules instead of a click.
//
// DELIBERATELY CRUDER THAN FEELS RIGHT. Eight multiplicative modifiers, each one a single
// named number, and nothing hidden. A model with three inputs you can reason about beats one
// with twelve you cannot, and the explainability requirement is what shows which inputs are
// earning their place: a modifier that is 1.0 in every log line is a modifier to delete.
//
// EVERY FACTOR IS A MULTIPLIER ON ONE BASE. That is a choice and not an accident. Additive
// bonuses have to be balanced against each other and against the base, and a stack of them
// can flip a result's sign; multipliers compose in any order, cannot change the sign, and
// each one reads as a plain-English sentence — "forest halves incoming fire" — which is what
// makes the breakdown below explainable to a player rather than merely dumped at them.
//
// DETERMINISM. The single stochastic input is drawn from DeterministicRandom (integer-only
// PCG, chosen so results are platform- and version-stable) seeded from the tick and the two
// unit ids. Never UnityEngine.Random, never a wall clock, never an accumulating field. Two
// runs of the same scenario must produce identical results, and CombatProbe asserts it.
//
// NO UNITY COMPONENT DEPENDENCIES. Static methods over plain data, so AI and headless tests
// call it directly.

using UnityEngine;
using Strategos.Maps;
using Strategos.Units;

namespace Strategos.Combat
{
    /// <summary>Why an exchange produced what it produced.</summary>
    public enum EngagementOutcome
    {
        /// <summary>Rounds went downrange and did something.</summary>
        Fired = 0,

        /// <summary>Beyond the attacker's engagement range. Nothing happens.</summary>
        OutOfRange = 1,

        /// <summary>Nothing left to shoot with.</summary>
        NoAmmunition = 2,

        /// <summary>
        /// The attacker is pinned and cannot return fire this tick. **Temporary** — suppression
        /// decays, so this must not end an engagement. Kept apart from
        /// <see cref="AttackerDestroyed"/> for exactly that reason: they read alike and one
        /// recovers.
        /// </summary>
        AttackerSuppressed = 3,

        /// <summary>The attacker has no combat power left. Permanent.</summary>
        AttackerDestroyed = 4,

        /// <summary>The target had already been destroyed when this shot resolved.</summary>
        TargetDestroyed = 5,
    }

    /// <summary>
    /// The modifiers that produced a result, each one a bare multiplier.
    /// </summary>
    /// <remarks>
    /// Fixed fields rather than a list of (name, value) pairs. The list version reads better
    /// at the call site and allocates once per firing unit per tick, which at any interesting
    /// scale is a great deal of garbage to produce in order to write a log line nobody is
    /// reading. Struct fields cost nothing, are as interrogable, and <see cref="Describe"/>
    /// puts the names back when a human actually wants them.
    /// </remarks>
    public struct EngagementBreakdown
    {
        /// <summary>Authored damage per minute against an unprotected target.</summary>
        public float BaseFirepower;

        /// <summary>Attacker's strength × readiness × (1 − suppression).</summary>
        public float Effectiveness;

        /// <summary>Falls off with distance out to the engagement limit.</summary>
        public float Range;

        /// <summary>Defender's landcover. Below 1 means cover.</summary>
        public float Cover;

        /// <summary>Defender's posture. Dug in is hard to hurt; moving is exposed.</summary>
        public float Posture;

        /// <summary>Attacker's height advantage over the defender.</summary>
        public float Elevation;

        /// <summary>Reciprocal of the defender's protection.</summary>
        public float Protection;

        /// <summary>Attacker's ammunition state. Below 1 only when running low.</summary>
        public float Ammunition;

        /// <summary>The one stochastic term. Deterministic given tick and the two unit ids.</summary>
        public float Chance;

        /// <summary>Product of everything above: damage per minute actually delivered.</summary>
        public float DamagePerMinute;

        /// <summary>
        /// One line, ordered so the terms that usually decide an engagement come first.
        /// Hyphens and middots only — the bundled atlas renders an en dash as nothing.
        /// </summary>
        public string Describe() =>
            $"fp {BaseFirepower:0.#} · eff {Effectiveness:0.00} · rng {Range:0.00} · " +
            $"cov {Cover:0.00} · pos {Posture:0.00} · elev {Elevation:0.00} · " +
            $"prot {Protection:0.00} · ammo {Ammunition:0.00} · luck {Chance:0.00} " +
            $"=> {DamagePerMinute:0.00}/min";
    }

    /// <summary>What one tick of fire did, and why.</summary>
    public struct EngagementResult
    {
        public EngagementOutcome Outcome;

        /// <summary>Strength points taken off the defender this tick.</summary>
        public float Damage;

        /// <summary>Suppression points added to the defender this tick.</summary>
        public float Suppression;

        /// <summary>Ammunition percentage points the attacker spent this tick.</summary>
        public float AmmunitionSpent;

        /// <summary>Distance at which it happened, in metres. For the log.</summary>
        public float RangeMetres;

        public EngagementBreakdown Breakdown;

        public bool DidFire => Outcome == EngagementOutcome.Fired;

        public override string ToString() =>
            Outcome == EngagementOutcome.Fired
                ? $"{Damage:0.000} dmg, {Suppression:0.0} supp at {RangeMetres:0} m  [{Breakdown.Describe()}]"
                : Outcome.ToString();
    }

    public static class EngagementResolver
    {
        // ─── Tuning ───────────────────────────────────────────────────────────
        //
        // Every constant here is a knob a designer will want. They are named and gathered
        // rather than inlined so that balancing is editing this block, not reading the maths.

        /// <summary>Effect remaining at maximum engagement range, as a fraction.</summary>
        public const float RangeFalloff = 0.35f;

        /// <summary>Widest random swing on a single exchange, either way.</summary>
        public const float ChanceSpread = 0.15f;

        /// <summary>
        /// Suppression points added per point of strength damage.
        ///
        /// Tuned against the decay below rather than picked: at 10 firepower a tick of fire is
        /// about 0.13 strength points, so anything under ~7 here is *out-paced by decay* and
        /// suppression never rises at all — a unit under sustained fire that reads as calm.
        /// Twelve puts a minute of fire at roughly 40 net points, and lets it fall away over
        /// the following three-quarters of a minute once the shooting stops.
        /// </summary>
        public const float SuppressionPerDamage = 12f;

        /// <summary>Suppression points shed per second when not under fire.</summary>
        public const float SuppressionDecayPerSecond = 0.9f;

        /// <summary>Ammunition below this percentage starts to cut output.</summary>
        public const float LowAmmunitionPercent = 25f;

        /// <summary>
        /// Effectiveness below which a unit cannot meaningfully fight. Prevents a unit at 1%
        /// strength trading shots for ever, which is the shape a game takes when nothing is
        /// ever quite finished.
        /// </summary>
        public const float MinimumEffectiveness = 0.02f;

        /// <summary>Metres of height advantage that earn the full elevation bonus.</summary>
        public const float FullElevationAdvantageMetres = 120f;

        /// <summary>Largest swing elevation may apply, either way.</summary>
        public const float ElevationSwing = 0.2f;

        // ─── Resolution ───────────────────────────────────────────────────────

        /// <summary>
        /// Resolves one tick of <paramref name="attacker"/> firing at <paramref name="defender"/>.
        /// </summary>
        /// <remarks>
        /// **Reads state and writes none of it.** Applying the result is the simulation's job,
        /// and the separation is what lets an exchange be simultaneous: every shot in a tick is
        /// computed against the state at the *start* of that tick, then all of them are applied
        /// together. Resolve-and-apply in one pass would give whoever the unit loop reached
        /// first a free shot at an undamaged enemy, which is a first-mover advantage decided by
        /// scenario ordering — invisible, unfair, and untraceable when someone eventually
        /// notices that unit 1 wins more often than unit 6.
        /// </remarks>
        public static EngagementResult Resolve(UnitInstance attacker, UnitInstance defender,
            MapData map, UnitCatalogue catalogue, int tick, float secondsPerTick)
        {
            var result = new EngagementResult();

            if (attacker == null || defender == null || map == null)
            { result.Outcome = EngagementOutcome.AttackerDestroyed; return result; }

            if (attacker.IsDestroyed)
            { result.Outcome = EngagementOutcome.AttackerDestroyed; return result; }

            if (defender.IsDestroyed)
            { result.Outcome = EngagementOutcome.TargetDestroyed; return result; }

            var attackerCaps = attacker.Capabilities(catalogue);
            var defenderCaps = defender.Capabilities(catalogue);

            float metresPerCell = Mathf.Max(0.0001f, map.Header.MetresPerCell);
            float distance = Vector2.Distance(attacker.Cell, defender.Cell) * metresPerCell;
            result.RangeMetres = distance;

            if (distance > attackerCaps.EngagementRangeMetres)
            { result.Outcome = EngagementOutcome.OutOfRange; return result; }

            // Pinned, not finished. Sustained fire drives suppression to the cap in about
            // forty-five seconds, which is the point of suppression — but it decays, so this
            // is a tick with no fire in it and not the end of the engagement.
            float effectiveness = attacker.Effectiveness;
            if (effectiveness < MinimumEffectiveness)
            { result.Outcome = EngagementOutcome.AttackerSuppressed; return result; }

            if (attacker.Supply.Ammunition <= 0f)
            { result.Outcome = EngagementOutcome.NoAmmunition; return result; }

            var b = new EngagementBreakdown
            {
                BaseFirepower = attackerCaps.Firepower,
                Effectiveness = effectiveness,
                Range = RangeFactor(distance, attackerCaps.EngagementRangeMetres),
                Cover = CoverFactor(defender.Landcover(map)),
                Posture = PostureFactor(defender.Posture),
                Elevation = ElevationFactor(attacker.Elevation(map) - defender.Elevation(map)),
                Protection = ProtectionFactor(defenderCaps.Protection),
                Ammunition = AmmunitionFactor(attacker.Supply.Ammunition),
                Chance = ChanceFactor(tick, attacker.Id, defender.Id),
            };

            b.DamagePerMinute = b.BaseFirepower * b.Effectiveness * b.Range * b.Cover *
                                b.Posture * b.Elevation * b.Protection * b.Ammunition * b.Chance;

            result.Breakdown = b;
            result.Outcome = EngagementOutcome.Fired;

            // Firepower is authored per minute; the simulation steps per second.
            result.Damage = Mathf.Max(0f, b.DamagePerMinute * secondsPerTick / 60f);
            result.Suppression = result.Damage * SuppressionPerDamage;
            result.AmmunitionSpent =
                attackerCaps.Consumption.AmmunitionPerHourEngaged * secondsPerTick / 3600f;

            return result;
        }

        /// <summary>
        /// Applies a resolved exchange. Separate from <see cref="Resolve"/> so a whole tick's
        /// worth of fire can be computed before any of it lands — see the note there.
        /// </summary>
        public static void Apply(UnitInstance attacker, UnitInstance defender,
            in EngagementResult result)
        {
            if (!result.DidFire) return;

            defender.Strength = Mathf.Clamp(defender.Strength - result.Damage, 0f, 100f);
            defender.Suppression = Mathf.Clamp(defender.Suppression + result.Suppression, 0f, 100f);

            attacker.Supply.Ammunition =
                Mathf.Clamp(attacker.Supply.Ammunition - result.AmmunitionSpent, 0f, 100f);
        }

        /// <summary>
        /// Sheds suppression. Called every tick for every unit, including ones under fire —
        /// an exchange adds far more than a second of decay removes, so a single rule covers
        /// both cases and there is no "was I shot at this tick" flag to get wrong.
        /// </summary>
        public static void DecaySuppression(UnitInstance unit, float secondsPerTick)
        {
            if (unit == null || unit.Suppression <= 0f) return;
            unit.Suppression = Mathf.Max(0f,
                unit.Suppression - SuppressionDecayPerSecond * secondsPerTick);
        }

        // ─── Factors ──────────────────────────────────────────────────────────
        //
        // One function each, all pure, all returning a plain multiplier. Written this way so
        // the probe can chart any one of them in isolation and so a designer can change one
        // without reading the others.

        /// <summary>
        /// Falls off with the square of the fraction of maximum range, so the drop is gentle
        /// where most fighting happens and steep at the limit. Linear would make the far half
        /// of a weapon's envelope feel uniformly mediocre.
        /// </summary>
        public static float RangeFactor(float distanceMetres, float maxRangeMetres)
        {
            if (maxRangeMetres <= 0.0001f) return 0f;
            float t = Mathf.Clamp01(distanceMetres / maxRangeMetres);
            return Mathf.Lerp(1f, RangeFalloff, t * t);
        }

        /// <summary>
        /// How much the ground protects whoever is standing on it.
        ///
        /// Urban is the strongest because a defended built-up area is historically the most
        /// expensive thing to take; forest is next. Water is not a hiding place — a unit in it
        /// is either wading or afloat, and neither conceals anything.
        /// </summary>
        public static float CoverFactor(LandcoverClass cover) => cover switch
        {
            LandcoverClass.Urban => 0.45f,
            LandcoverClass.Forest => 0.55f,
            LandcoverClass.Rock => 0.65f,
            LandcoverClass.Cropland => 0.85f,
            LandcoverClass.Marsh => 0.90f,
            LandcoverClass.Snow => 1.05f,
            LandcoverClass.Sand => 1.05f,
            LandcoverClass.Water => 1.10f,
            _ => 1f,   // Open
        };

        /// <summary>
        /// A dug-in unit is hard to hurt and a moving one is exposed. This is the modifier a
        /// player can act on directly, so it is deliberately the largest single swing in the
        /// model: halting under fire should visibly be the right decision.
        /// </summary>
        public static float PostureFactor(Posture posture) => posture switch
        {
            Posture.DugIn => 0.5f,
            Posture.Guarding => 0.5f,
            Posture.Covering => 0.5f,
            Posture.Moving => 1.25f,
            _ => 1f,   // Halted / Screening
        };

        /// <summary>Height advantage, capped both ways so a mountain is not a win condition.</summary>
        public static float ElevationFactor(float metresAbove)
        {
            float t = Mathf.Clamp(metresAbove / FullElevationAdvantageMetres, -1f, 1f);
            return 1f + t * ElevationSwing;
        }

        /// <summary>Reciprocal, floored so a protection of zero cannot divide by nothing.</summary>
        public static float ProtectionFactor(float protection) =>
            1f / Mathf.Max(0.2f, protection);

        /// <summary>
        /// Full output until ammunition runs low, then a linear taper. A unit does not shoot
        /// half as well at 50% of a basic load — it shoots the same until it starts rationing.
        /// </summary>
        public static float AmmunitionFactor(float ammunitionPercent)
        {
            if (ammunitionPercent >= LowAmmunitionPercent) return 1f;
            return Mathf.Clamp01(ammunitionPercent / LowAmmunitionPercent);
        }

        /// <summary>
        /// The one stochastic term.
        /// </summary>
        /// <remarks>
        /// Seeded from the tick and both unit ids, so it is reproducible from state alone: no
        /// generator is carried between ticks, which means a replay does not have to reproduce
        /// a random *stream*, only the state each draw was made from. That is a much weaker
        /// thing to have to get right, and it survives a unit being added, removed or resolved
        /// in a different order.
        ///
        /// The ids are mixed with odd multipliers so that (a,b) and (b,a) draw differently —
        /// two units firing at each other on the same tick must not share a roll.
        /// </remarks>
        public static float ChanceFactor(int tick, UnitId attacker, UnitId defender)
        {
            unchecked
            {
                int seed = tick * 73856093 ^ attacker.Value * 19349663 ^ defender.Value * 83492791;
                var rng = new DeterministicRandom(seed);
                return rng.Range(1f - ChanceSpread, 1f + ChanceSpread);
            }
        }
    }
}
