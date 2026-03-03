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

    public List<string> AllRuleNames { get; init; } = [];

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
}
