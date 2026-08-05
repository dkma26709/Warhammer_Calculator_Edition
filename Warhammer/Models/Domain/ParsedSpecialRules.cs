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
    public bool ThunderousCharge { get; init; }
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
    /// Immunity (Fear), (Terror) or (Psychology) — the unit ignores a Fear-causing enemy's
    /// Combat Resolution bonus. Fear-causers get this for free; 115 units in the shipped data
    /// carry it explicitly.
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

            // Save-type: take best (lower is better)
            WardSave = MinNullable(WardSave, other.WardSave),
            Regeneration = MinNullable(Regeneration, other.Regeneration),
            Parry = MinNullable(Parry, other.Parry),
            Dodge = MinNullable(Dodge, other.Dodge),
            NaturalArmour = MinNullable(NaturalArmour, other.NaturalArmour),

            // Bonus-type: take best (higher is better)
            ArmourPiercing = MaxNullable(ArmourPiercing, other.ArmourPiercing),
            MightyBlow = MaxNullable(MightyBlow, other.MightyBlow),
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

    public static ParsedSpecialRules Parse(string? specialRulesText)
    {
        if (string.IsNullOrWhiteSpace(specialRulesText))
            return new ParsedSpecialRules();

        var text = Preprocess(specialRulesText);
        var result = new ParsedSpecialRules();
        var allNames = new List<string>();

        // Split by comma, handling parenthetical values
        var parts = SplitRules(text);

        foreach (var part in parts)
        {
            var (name, param) = ExtractNameAndParam(part.Trim());
            if (string.IsNullOrWhiteSpace(name)) continue;

            var displayName = param != null ? $"{name} ({param})" : name;
            allNames.Add(displayName);

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
                "mighty blow" => result with { MightyBlow = ParseIntParam(param) ?? 1 },
                "multiple wounds" => result with { MultipleWounds = ParseIntParam(param), MultipleWoundsRoll = ParseDiceParam(param) },
                "natural armour" => result with { NaturalArmour = ParseSaveParam(param) },
                "parry" => result with { Parry = ParseSaveParam(param) },
                "poisoned attacks" => result with { PoisonedAttacks = true },
                "regeneration" => result with { Regeneration = ParseSaveParam(param) ?? 4 },
                "ward save" => result with { WardSave = ParseSaveParam(param) },
                "stomp" => result with { Stomp = ParseIntParam(param), StompRoll = ParseDiceParam(param) },
                "stubborn" => result with { Stubborn = true },
                "unbreakable" => result with { Unbreakable = true },
                "unstable" => result with { Unstable = true },
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

        return result with { AllRuleNames = allNames };
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
        var match = Regex.Match(rule, @"^(.+?)\s*\(([^)]+)\)\s*\*?\s*$");
        if (match.Success)
            return (match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim());

        return (rule.Trim().TrimEnd('*'), null);
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
    /// Return the better (lower) ward save value, treating null as "no ward".
    /// </summary>
    private static int MinWard(int? existing, int proposed) =>
        existing.HasValue ? Math.Min(existing.Value, proposed) : proposed;

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
}
