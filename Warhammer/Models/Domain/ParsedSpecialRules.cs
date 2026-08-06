using System.Text.RegularExpressions;

namespace Warhammer.Models.Domain;

public record ParsedSpecialRules
{
    public bool AlwaysStrikesFirst { get; init; }
    public bool AlwaysStrikesLast { get; init; }
    public int? ArmourPiercing { get; init; }
    public bool DevastatingCharge { get; init; }
    public bool Ethereal { get; init; }
    public int? FightInExtraRanks { get; init; }
    public bool FlamingAttacks { get; init; }
    public bool Flammable { get; init; }
    public bool Frenzy { get; init; }
    public bool Hatred { get; init; }
    public string? HatredTarget { get; init; }
    public bool IceAttacks { get; init; }
    public bool IgnoresArmourSaves { get; init; }
    public int? ImpactHits { get; init; }
    public string? ImpactHitsRoll { get; init; }
    public bool KillingBlow { get; init; }
    public bool HeroicKillingBlow { get; init; }
    public bool LightningAttacks { get; init; }
    public bool MagicalAttacks { get; init; }
    public int? MightyBlow { get; init; }
    public int? MultipleWounds { get; init; }
    public string? MultipleWoundsRoll { get; init; }
    public int? NaturalArmour { get; init; }
    public int? Parry { get; init; }
    public bool PoisonedAttacks { get; init; }
    public bool ChaosArmour { get; init; }
    public int? Regeneration { get; init; }
    public int? WardSave { get; init; }
    public int? Stomp { get; init; }
    public string? StompRoll { get; init; }
    public bool Stubborn { get; init; }
    public bool Unbreakable { get; init; }
    public bool Unstable { get; init; }

    /// <summary>
    /// Tomb Kings' Indomitable (wap-v3 only): one fewer wound from <see cref="Unstable"/>.
    /// </summary>
    public bool Indomitable { get; init; }
    public bool Fear { get; init; }
    public bool Terror { get; init; }
    public int? RandomAttacks { get; init; }
    public string? RandomAttacksRoll { get; init; }
    public bool ColdBlooded { get; init; }
    public bool PredatoryFighter { get; init; }
    public bool MurderousProwess { get; init; }
    public bool MartialProwess { get; init; }
    public bool PrimalFury { get; init; }
    public bool EternalHatred { get; init; }
    public bool FearElves { get; init; }
    public int? Dodge { get; init; }
    public bool ElvenGrace { get; init; }
    public bool Deathblow { get; init; }
    public bool Swordmaster { get; init; }
    public bool OgreCharge { get; init; }

    /// <summary>Mighty Blow (2) in any turn the model charges.</summary>
    public bool ThunderousCharge { get; init; }

    /// <summary>
    /// Mighty Blow (1) in any turn the model charges. wap-v3's Tusker Charge, which replaced the
    /// boar's "Mighty Blow (2) (War Boars only)" on Boar Boyz and Razorgor.
    /// </summary>
    public bool TuskerCharge { get; init; }
    public bool GromrilArmour { get; init; }
    public bool ForestSpirit { get; init; }
    public bool Vampiric { get; init; }
    public bool LanceFormation { get; init; }
    public bool MartialMemories { get; init; }
    public bool ShadowDancesOfLoec { get; init; }
    public bool TreeWhack { get; init; }
    public bool TreeAspects { get; init; }
    public bool Swiftstride { get; init; }
    public bool ImmunityKillingBlow { get; init; }
    public bool ImmunityPoisonedAttacks { get; init; }
    public bool ImmunityFlamingAttacks { get; init; }

    /// <summary>
    /// The unit ignores a Fear-causing enemy's Combat Resolution bonus, whether from
    /// Immunity (Fear), (Terror) or (Psychology), or from causing Fear or Terror itself —
    /// the books grant fear-causers the immunity for free.
    /// </summary>
    public bool ImmunityFear { get; init; }
    public bool Minus1ToBeHit { get; init; }
    public bool Minus1WSToAttackers { get; init; }
    public bool RerollSuccessfulWoundsAgainst { get; init; }
    public bool Impale { get; init; }
    public bool BlessingOfTheLady { get; init; }

    public List<string> AllRuleNames { get; init; } = [];

    /// <summary>
    /// Merge another set of special rules into this one.
    /// Booleans OR, save-type values take best (lowest), bonus-type values take best (highest),
    /// strings prefer non-null (this wins ties), AllRuleNames concatenate.
    /// </summary>
    public ParsedSpecialRules Merge(ParsedSpecialRules other)
    {
        return this with
        {
            // Booleans: OR
            AlwaysStrikesFirst = AlwaysStrikesFirst || other.AlwaysStrikesFirst,
            AlwaysStrikesLast = AlwaysStrikesLast || other.AlwaysStrikesLast,
            DevastatingCharge = DevastatingCharge || other.DevastatingCharge,
            Ethereal = Ethereal || other.Ethereal,
            FlamingAttacks = FlamingAttacks || other.FlamingAttacks,
            Flammable = Flammable || other.Flammable,
            Frenzy = Frenzy || other.Frenzy,
            Hatred = Hatred || other.Hatred,
            IceAttacks = IceAttacks || other.IceAttacks,
            IgnoresArmourSaves = IgnoresArmourSaves || other.IgnoresArmourSaves,
            KillingBlow = KillingBlow || other.KillingBlow,
            HeroicKillingBlow = HeroicKillingBlow || other.HeroicKillingBlow,
            LightningAttacks = LightningAttacks || other.LightningAttacks,
            MagicalAttacks = MagicalAttacks || other.MagicalAttacks,
            PoisonedAttacks = PoisonedAttacks || other.PoisonedAttacks,
            ChaosArmour = ChaosArmour || other.ChaosArmour,
            Stubborn = Stubborn || other.Stubborn,
            Unbreakable = Unbreakable || other.Unbreakable,
            Unstable = Unstable || other.Unstable,
            Indomitable = Indomitable || other.Indomitable,
            Fear = Fear || other.Fear,
            Terror = Terror || other.Terror,
            ColdBlooded = ColdBlooded || other.ColdBlooded,
            PredatoryFighter = PredatoryFighter || other.PredatoryFighter,
            MurderousProwess = MurderousProwess || other.MurderousProwess,
            MartialProwess = MartialProwess || other.MartialProwess,
            PrimalFury = PrimalFury || other.PrimalFury,
            EternalHatred = EternalHatred || other.EternalHatred,
            FearElves = FearElves || other.FearElves,
            ElvenGrace = ElvenGrace || other.ElvenGrace,
            Deathblow = Deathblow || other.Deathblow,
            Swordmaster = Swordmaster || other.Swordmaster,
            OgreCharge = OgreCharge || other.OgreCharge,
            ThunderousCharge = ThunderousCharge || other.ThunderousCharge,
            TuskerCharge = TuskerCharge || other.TuskerCharge,
            GromrilArmour = GromrilArmour || other.GromrilArmour,
            ForestSpirit = ForestSpirit || other.ForestSpirit,
            Vampiric = Vampiric || other.Vampiric,
            LanceFormation = LanceFormation || other.LanceFormation,
            MartialMemories = MartialMemories || other.MartialMemories,
            ShadowDancesOfLoec = ShadowDancesOfLoec || other.ShadowDancesOfLoec,
            TreeWhack = TreeWhack || other.TreeWhack,
            TreeAspects = TreeAspects || other.TreeAspects,
            Swiftstride = Swiftstride || other.Swiftstride,
            ImmunityKillingBlow = ImmunityKillingBlow || other.ImmunityKillingBlow,
            ImmunityPoisonedAttacks = ImmunityPoisonedAttacks || other.ImmunityPoisonedAttacks,
            ImmunityFlamingAttacks = ImmunityFlamingAttacks || other.ImmunityFlamingAttacks,
            ImmunityFear = ImmunityFear || other.ImmunityFear,
            Minus1ToBeHit = Minus1ToBeHit || other.Minus1ToBeHit,
            Minus1WSToAttackers = Minus1WSToAttackers || other.Minus1WSToAttackers,
            RerollSuccessfulWoundsAgainst = RerollSuccessfulWoundsAgainst || other.RerollSuccessfulWoundsAgainst,
            Impale = Impale || other.Impale,
            BlessingOfTheLady = BlessingOfTheLady || other.BlessingOfTheLady,

            // Ward saves are the one save type that combines rather than taking the best —
            // see CombineWard. Regeneration deliberately does not: it is a ward, but the
            // house reading is best-of rather than combined.
            WardSave = CombineWard(WardSave, other.WardSave),

            // Save-type: take best (lower is better)
            Regeneration = MinNullable(Regeneration, other.Regeneration),
            Parry = MinNullable(Parry, other.Parry),
            Dodge = MinNullable(Dodge, other.Dodge),
            NaturalArmour = MinNullable(NaturalArmour, other.NaturalArmour),

            // Two bonus-type rules say outright that they add. Both books use the same sentence
            // for each — "This rule is cumulative with other sources of Mighty Blow" (2.32 p63,
            // 3.11 p64) and the same of Armour Piercing (2.32 p58, 3.11 p58) — so neither is a
            // v3 divergence, and a second source adds rather than masking the first.
            ArmourPiercing = SumNullable(ArmourPiercing, other.ArmourPiercing),
            MightyBlow = SumNullable(MightyBlow, other.MightyBlow),

            // Bonus-type: take best (higher is better)
            MultipleWounds = MaxNullable(MultipleWounds, other.MultipleWounds),
            FightInExtraRanks = MaxNullable(FightInExtraRanks, other.FightInExtraRanks),
            ImpactHits = MaxNullable(ImpactHits, other.ImpactHits),
            Stomp = MaxNullable(Stomp, other.Stomp),
            RandomAttacks = MaxNullable(RandomAttacks, other.RandomAttacks),

            // Strings: prefer non-null, this wins ties
            HatredTarget = HatredTarget ?? other.HatredTarget,
            ImpactHitsRoll = ImpactHitsRoll ?? other.ImpactHitsRoll,
            MultipleWoundsRoll = MultipleWoundsRoll ?? other.MultipleWoundsRoll,
            StompRoll = StompRoll ?? other.StompRoll,
            RandomAttacksRoll = RandomAttacksRoll ?? other.RandomAttacksRoll,

            // Lists: concatenate
            AllRuleNames = [.. AllRuleNames, .. other.AllRuleNames],
        };
    }

    private static int? MinNullable(int? a, int? b) =>
        a.HasValue && b.HasValue ? Math.Min(a.Value, b.Value) :
        a ?? b;

    private static int? MaxNullable(int? a, int? b) =>
        a.HasValue && b.HasValue ? Math.Max(a.Value, b.Value) :
        a ?? b;

    private static int? SumNullable(int? a, int? b) =>
        a.HasValue && b.HasValue ? a.Value + b.Value :
        a ?? b;

    /// <summary>
    /// Rules the army book scopes to one named model in a unit, keyed by the name it gives —
    /// the "War Boars" of "Mighty Blow (2) (War Boars only)". Which model that is depends on the
    /// unit's profiles, which the parser cannot see, so resolving the name is left to
    /// <c>GameDataRepository</c>.
    /// </summary>
    public readonly record struct ScopedRules(string Scope, ParsedSpecialRules Rules);

    /// <summary>
    /// A rule list split into the rules that apply to the whole unit and those the book scopes to
    /// one model. <see cref="AllRuleNames"/> on <paramref name="UnitWide"/> still lists every rule
    /// including the scoped ones, so nothing disappears from a rules display.
    /// </summary>
    public sealed record ScopedRuleSets(ParsedSpecialRules UnitWide, IReadOnlyList<ScopedRules> Scoped);

    /// <summary>
    /// Parse a rule list, keeping only the rules that apply to the whole unit. Rules the book
    /// scopes to one model are excluded — applying them unit-wide arms every model with a mount's
    /// or a champion's rule. Use <see cref="ParseScoped"/> to get those back.
    /// </summary>
    public static ParsedSpecialRules Parse(string? specialRulesText)
        => ParseScoped(specialRulesText).UnitWide;

    /// <summary>
    /// Parse a rule list into unit-wide rules plus the per-model scoped ones.
    /// </summary>
    public static ScopedRuleSets ParseScoped(string? specialRulesText)
    {
        if (string.IsNullOrWhiteSpace(specialRulesText))
            return new ScopedRuleSets(new ParsedSpecialRules(), []);

        var text = Preprocess(specialRulesText);
        var result = new ParsedSpecialRules();
        var allNames = new List<string>();
        var scoped = new List<ScopedRules>();

        // Split by comma, handling parenthetical values
        var parts = SplitRules(text);

        foreach (var part in parts)
        {
            var (name, param) = ExtractNameAndParam(part.Trim());
            if (string.IsNullOrWhiteSpace(name)) continue;

            var displayName = param != null ? $"{name} ({param})" : name;
            allNames.Add(displayName);

            // "<Rule> (<Model> only)" and "<Rule> (<value>) (<Model> only)" both scope the rule to
            // one model. Peel the scope off and parse what is left on its own, so the rule reaches
            // its arm — the two-parenthetical form used to reach no arm at all, while the
            // one-parenthetical form was applied to the whole unit.
            if (ScopeOf(name, param) is { } scope)
            {
                var inner = scope.Rule.Length > 0 ? Parse(scope.Rule) : new ParsedSpecialRules();
                scoped.Add(new ScopedRules(scope.Model, inner));
                continue;
            }

            result = name.ToLowerInvariant() switch
            {
                "always strikes first" => result with { AlwaysStrikesFirst = true },
                "always strikes last" => result with { AlwaysStrikesLast = true },
                "armour piercing" => result with { ArmourPiercing = ParseIntParam(param) ?? 1 },
                "chaos armour" => result with { ChaosArmour = true },
                "devastating charge" => result with { DevastatingCharge = true },
                "ethereal" => result with { Ethereal = true },
                "fight in extra ranks" => result with { FightInExtraRanks = ParseIntParam(param) ?? 1 },
                "flaming attacks" => result with { FlamingAttacks = true },
                "flammable" => result with { Flammable = true },
                "frenzy" => result with { Frenzy = true },
                "hatred" => result with { Hatred = true, HatredTarget = param },
                "ice attacks" => result with { IceAttacks = true },
                "ignores armour saves" => result with { IgnoresArmourSaves = true },
                "impact hits" => result with { ImpactHits = ParseIntParam(param), ImpactHitsRoll = ParseDiceParam(param) },
                "killing blow" => result with { KillingBlow = true },
                "heroic killing blow" => result with { HeroicKillingBlow = true },
                "lightning attacks" => result with { LightningAttacks = true },
                "magical attacks" => result with { MagicalAttacks = true },
                // Not additive within one list, even though Mighty Blow is cumulative across
                // sources (see Merge). Every unit that lists it twice is a rider/mount split —
                // "Mighty Blow (1), Mighty Blow (2) (War Boars only)" — where the two numbers
                // belong to two different models, so adding them would arm the rider with the
                // mount's bonus.
                "mighty blow" => result with { MightyBlow = ParseIntParam(param) ?? 1 },
                "multiple wounds" => result with { MultipleWounds = ParseIntParam(param), MultipleWoundsRoll = ParseDiceParam(param) },
                "natural armour" => result with { NaturalArmour = ParseSaveParam(param) },
                "parry" => result with { Parry = ParseSaveParam(param) },
                "poisoned attacks" => result with { PoisonedAttacks = true },
                "regeneration" => result with { Regeneration = ParseSaveParam(param) ?? 4 },
                // wap-v3 renamed Ward Save to Magical Ward: "Models with this rule gain an
                // invulnerable save as indicated in the brackets" (3.11 p63). The two saves are
                // the same rule renamed — 2.32 p38 "WARD SAVES" and 3.11 p38 "INVULNERABLE
                // SAVES" carry the same sentences, down to working like armour saves but never
                // being modified by the attack's Strength. No wap-v3 unit's rule list uses the
                // v2 spelling at all, so without this name every v3 ward save was dropped: the
                // data went from 0 units with a parseable ward save to 28.
                "ward save" or "magical ward" => result with { WardSave = ParseSaveParam(param) },
                "stomp" => result with { Stomp = ParseIntParam(param), StompRoll = ParseDiceParam(param) },
                "stubborn" => result with { Stubborn = true },
                "unbreakable" => result with { Unbreakable = true },
                "unstable" => result with { Unstable = true },

                // Tomb Kings, wap-v3 only: "Models with this special rule suffer 1 Wound less
                // than they normally would due to the Unstable special rule" (Tomb Kings 3.1 p4).
                "indomitable" => result with { Indomitable = true },
                "fear" => result with { Fear = true },
                "terror" => result with { Terror = true },
                "random attacks" => result with { RandomAttacks = ParseIntParam(param), RandomAttacksRoll = ParseDiceParam(param) },
                "cold-blooded" or "cold blooded" => result with { ColdBlooded = true },
                "predatory fighter" => result with { PredatoryFighter = true },
                "murderous prowess" => result with { MurderousProwess = true },
                "martial prowess" => result with { MartialProwess = true },
                "martial memories" => result with { MartialMemories = true },
                "primal fury" => result with { PrimalFury = true },
                "eternal hatred" => result with { EternalHatred = true, Hatred = true },
                "ancestral grudge" => result with { Hatred = true, HatredTarget = param ?? "Orcs, Goblins, Trolls, Skaven" },
                "valour of ages" => result,
                "fear elves" => result with { FearElves = true },
                "dodge" => result with { Dodge = ParseSaveParam(param) ?? 6 },
                "elven grace" => result with { ElvenGrace = true },
                "deathblow" => result with { Deathblow = true },
                "swiftstride" => result with { Swiftstride = true },
                "swordmaster" => result with { Swordmaster = true },
                "ogre charge" => result with { OgreCharge = true },
                "thunderous charge" => result with { ThunderousCharge = true },
                // "Models with this special rule have the Mighty Blow (1) special rule in any
                // turn that they charge" — wap-v3 Orcs & Goblins and Beastmen.
                "tusker charge" => result with { TuskerCharge = true },
                "gromril armour" => result with { GromrilArmour = true },
                "forest spirit" => result with { ForestSpirit = true, Fear = true, MagicalAttacks = true },
                "vampiric" => result with { Vampiric = true, Fear = true },
                "lance formation" => result with { LanceFormation = true },
                "shadow dances of loec" => result with { ShadowDancesOfLoec = true },
                "tree whack" => result with { TreeWhack = true },
                "tree aspects" => result with { TreeAspects = true },
                "jungle poisons" => result,
                "khainite" => result,
                "armed to da teef" => result,
                "animated construct" => result with { Fear = true, Unstable = true },
                "undead" => result with { Fear = true, Unstable = true },
                "chill grasp" => result,
                "strength in numbers" => result,
                "bloodgreed" => result,
                "blood frenzy" => result,
                "resolute" => result,
                "scurry away" => result,
                "verminous valour" => result,

                // Daemon marks (compound rules decomposed into existing fields)
                "daemon of khorne" => result with { Hatred = true },
                "daemon of slaanesh" => result with { AlwaysStrikesFirst = true, ArmourPiercing = Math.Max(result.ArmourPiercing ?? 0, 1) },
                "daemon of nurgle" => result with { Minus1ToBeHit = true },
                "daemon of tzeentch" => result with { WardSave = MinWard(result.WardSave, 6) },
                "daemonic" => result with { WardSave = MinWard(result.WardSave, 5) },

                // Chaos marks
                "mark of khorne" => result with { Frenzy = true },
                "mark of nurgle" => result with { Minus1WSToAttackers = true },
                "mark of slaanesh" => result with { RerollSuccessfulWoundsAgainst = true },
                "mark of tzeentch" => result with { WardSave = MinWard(result.WardSave, 6) },

                // Faction-wide defensive rules
                "the red thirst" => result with { Frenzy = true },
                "blessing of the lady" => result with { BlessingOfTheLady = true, WardSave = MinWard(result.WardSave, 6) },

                // Immunity variants
                "immunity" => ParseImmunity(result, param),

                // Tomb Kings
                "curse of the necropolis" => result with { Minus1ToBeHit = true },

                // Bretonnia / misc
                "impale" => result with { Impale = true },

                _ => result
            };
        }

        return new ScopedRuleSets(
            ApplyImplicitImmunities(result) with { AllRuleNames = allNames },
            scoped);
    }

    /// <summary>
    /// Split a "(… only)" scope off a rule, if it has one. Returns the model the book names and
    /// the rule text without it, or null when the rule applies to the whole unit.
    /// </summary>
    private static (string Model, string Rule)? ScopeOf(string name, string? param)
    {
        // "Mighty Blow (2) (War Boars only)" arrives as name="Mighty Blow (2)", param="War Boars
        // only", because ExtractNameAndParam takes the last parenthetical as the parameter.
        if (param == null || !IsScopeQualifier(param))
            return null;

        return (ScopeName(param), name.Trim());
    }

    /// <summary>
    /// True for a parameter that names a model rather than carrying the rule's value — "War Boars
    /// only", "Slave Master only", "Johann van Hal only, see Witch Hunter Captain". The word can
    /// sit mid-parameter when a cross-reference follows it, and no value — "4+", "D3", "2",
    /// "Psychology" — ever contains it.
    /// </summary>
    private static bool IsScopeQualifier(string param) => OnlyQualifier.IsMatch(param);

    private static readonly Regex OnlyQualifier =
        new(@"\bonly\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>The model name inside a scope qualifier, with "only" and any aside removed.</summary>
    private static string ScopeName(string param)
    {
        var name = Regex.Replace(param, @"\bonly\b", "", RegexOptions.IgnoreCase);
        // "Johann van Hal only, see Witch Hunter Captain" — the cross-reference is not the model.
        int comma = name.IndexOf(',');
        if (comma >= 0)
            name = name[..comma];
        return name.Trim().Trim(',', '.', ' ');
    }

    private static string Preprocess(string text)
    {
        // Join lines where previous line ends with comma (wrapped text from PDF)
        text = Regex.Replace(text, @",\s*\n\s*", ", ");

        // Truncate at \n followed by all-caps word (3+ chars) or bullet — next unit header or description
        var cutoff = Regex.Match(text, @"\n(?=[A-Z]{3,}|•)");
        if (cutoff.Success)
            text = text[..cutoff.Index];

        return text;
    }

    private static List<string> SplitRules(string text)
    {
        var parts = new List<string>();
        int depth = 0;
        int start = 0;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '(') depth++;
            else if (text[i] == ')') depth--;
            else if (text[i] == ',' && depth == 0)
            {
                parts.Add(text[start..i]);
                start = i + 1;
            }
        }

        if (start < text.Length)
            parts.Add(text[start..]);

        return parts;
    }

    private static (string Name, string? Param) ExtractNameAndParam(string rule)
    {
        // The books end the last rule of a list with a full stop and the extractor keeps it.
        // Strip it before matching, not after: left in place it either rides along inside the
        // name ("Stubborn.") or defeats the parenthetical match outright, because the pattern
        // below requires the string to end at ')' ("Natural Armour (3+)."). Either way the rule
        // reaches no arm in Parse and is silently dropped — which is how four Giants stopped
        // being Stubborn and the Star Dragon lost its 2+ Natural Armour.
        var trimmed = rule.Trim().TrimEnd('.', '*', ' ');

        var match = Regex.Match(trimmed, @"^(.+?)\s*\(([^)]+)\)\s*\*?\s*$");
        if (match.Success)
            return (match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim());

        return (trimmed, null);
    }

    private static int? ParseIntParam(string? param)
    {
        if (param == null) return null;
        var cleaned = param.TrimEnd('+');
        if (int.TryParse(cleaned, out var val)) return val;
        return null;
    }

    private static string? ParseDiceParam(string? param)
    {
        if (param != null && param.Contains('D', StringComparison.OrdinalIgnoreCase))
            return param;
        return null;
    }

    private static int? ParseSaveParam(string? param)
    {
        if (param == null) return null;
        var m = Regex.Match(param, @"(\d)\+");
        return m.Success ? int.Parse(m.Groups[1].Value) : null;
    }

    /// <summary>
    /// Combine two ward saves, treating null as "no ward".
    /// </summary>
    /// <remarks>
    /// Wards "may be combined with other Ward saves as normal" — that is, like armour saves, each
    /// contributing 7 minus its value — but "no model may have a Ward save better than 4+ by
    /// combining multiple Ward saves regardless of source" (2.32 p38; 3.11 p38 says the same of
    /// invulnerable saves). The books' own example: two 6+ wards and a 5+ give 4+.
    /// <para>
    /// A single ward keeps its listed value however good it is — "this does not stop single Ward
    /// saves from being used, such as a model having a listed 2+ or 3+" — and the player "may
    /// choose which Ward saves are used in any given situation", so the answer is the better of
    /// the best single ward and the capped combination. Without that, adding a 6+ to a listed 2+
    /// would make the model worse.
    /// </para>
    /// <para>
    /// Folding pairwise loses track of how many sources contributed, but only once the result has
    /// already reached the 4+ floor, where no further combining can improve it — so the fold gives
    /// the same answer as combining every source at once.
    /// </para>
    /// </remarks>
    private static int? CombineWard(int? a, int? b)
    {
        if (a is null) return b;
        if (b is null) return a;

        int combined = Math.Max(CombinedWardFloor, 7 - ((7 - a.Value) + (7 - b.Value)));
        return Math.Min(Math.Min(a.Value, b.Value), combined);
    }

    /// <summary>Combining wards can never produce a save better than this.</summary>
    private const int CombinedWardFloor = 4;

    private static int MinWard(int? existing, int proposed) => CombineWard(existing, proposed) ?? proposed;

    /// <summary>
    /// Parse Immunity(target) into the appropriate boolean flags.
    /// Target can be comma-separated, e.g. "Killing Blow, Poisoned Attacks".
    /// </summary>
    private static ParsedSpecialRules ParseImmunity(ParsedSpecialRules result, string? param)
    {
        if (string.IsNullOrWhiteSpace(param)) return result;

        // "Immunity (Fear/Terror)" uses a slash where the rest of the book uses commas.
        var targets = param.Split([',', '/'],
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var target in targets)
        {
            result = target.ToLowerInvariant() switch
            {
                "killing blow" => result with { ImmunityKillingBlow = true },
                "poisoned attacks" => result with { ImmunityPoisonedAttacks = true },
                "flaming attacks" => result with { ImmunityFlamingAttacks = true },
                // Immunity (Psychology) covers Panic, Fear and Terror. Only the Fear/Terror
                // half is combat-relevant: it cancels a Fear-causing enemy's CR bonus.
                "fear" or "terror" or "psychology" => result with { ImmunityFear = true },
                _ => result
            };
        }

        return result;
    }

    /// <summary>
    /// Fold in the immunities the books grant implicitly, so a reader of
    /// <see cref="ImmunityFear"/> gets the right answer without having to know the rule.
    /// </summary>
    private static ParsedSpecialRules ApplyImplicitImmunities(ParsedSpecialRules rules) =>
        (rules.Fear || rules.Terror) && !rules.ImmunityFear
            ? rules with { ImmunityFear = true }
            : rules;
}
