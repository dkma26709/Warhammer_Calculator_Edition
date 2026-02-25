namespace Warhammer.Models;

using System.Text.RegularExpressions;

public class ParsedSpecialRules
{
    public int? NaturalArmour { get; set; }
    public int? Regeneration { get; set; }
    public int? WardSave { get; set; }
    public bool KillingBlow { get; set; }
    public bool HeroicKillingBlow { get; set; }
    public bool PoisonedAttacks { get; set; }
    public bool Hatred { get; set; }
    public bool Frenzy { get; set; }
    public bool FlamingAttacks { get; set; }
    public bool Fear { get; set; }
    public bool Terror { get; set; }
    public bool ChaosArmour { get; set; }
    public int ArmourPiercing { get; set; }

    public bool HasAnyCombatRule =>
        NaturalArmour.HasValue || Regeneration.HasValue || WardSave.HasValue ||
        KillingBlow || HeroicKillingBlow || PoisonedAttacks || Hatred || Frenzy ||
        FlamingAttacks || ChaosArmour || ArmourPiercing > 0;
}

public static class SpecialRulesParser
{
    public static ParsedSpecialRules Parse(string? text)
    {
        var rules = new ParsedSpecialRules();
        if (string.IsNullOrWhiteSpace(text))
            return rules;

        // Natural Armour (X+)
        var naturalArmourMatch = Regex.Match(text, @"Natural Armour\s*\((\d+)\+\)", RegexOptions.IgnoreCase);
        if (naturalArmourMatch.Success)
            rules.NaturalArmour = int.Parse(naturalArmourMatch.Groups[1].Value);

        // Regeneration (X+)
        var regenMatch = Regex.Match(text, @"Regeneration\s*\((\d+)\+\)", RegexOptions.IgnoreCase);
        if (regenMatch.Success)
            rules.Regeneration = int.Parse(regenMatch.Groups[1].Value);

        // Ward Save (X+) — handle possible newline between "Ward Save" and "(X+)"
        var wardMatch = Regex.Match(text, @"Ward\s+[Ss]ave\s*\((\d+)\+\)", RegexOptions.IgnoreCase);
        if (wardMatch.Success)
            rules.WardSave = int.Parse(wardMatch.Groups[1].Value);

        // Heroic Killing Blow (check before Killing Blow to avoid false match)
        if (Regex.IsMatch(text, @"Heroic\s+Killing\s+Blow", RegexOptions.IgnoreCase))
            rules.HeroicKillingBlow = true;

        // Killing Blow (not preceded by "Heroic")
        if (Regex.IsMatch(text, @"(?<!Heroic\s)Killing\s+Blow", RegexOptions.IgnoreCase))
            rules.KillingBlow = true;

        // Poisoned Attacks
        if (Regex.IsMatch(text, @"Poisoned\s+Attacks", RegexOptions.IgnoreCase))
            rules.PoisonedAttacks = true;

        // Hatred
        if (Regex.IsMatch(text, @"\bHatred\b", RegexOptions.IgnoreCase))
            rules.Hatred = true;

        // Frenzy
        if (Regex.IsMatch(text, @"\bFrenzy\b", RegexOptions.IgnoreCase))
            rules.Frenzy = true;

        // Flaming Attacks
        if (Regex.IsMatch(text, @"Flaming\s+Attacks", RegexOptions.IgnoreCase))
            rules.FlamingAttacks = true;

        // Fear (but not "causes Fear" from Terror description — handled by checking Terror first)
        if (Regex.IsMatch(text, @"\bFear\b", RegexOptions.IgnoreCase))
            rules.Fear = true;

        // Terror
        if (Regex.IsMatch(text, @"\bTerror\b", RegexOptions.IgnoreCase))
            rules.Terror = true;

        // Chaos Armour
        if (Regex.IsMatch(text, @"Chaos\s+Armour", RegexOptions.IgnoreCase))
            rules.ChaosArmour = true;

        // Armour Piercing (X) as a unit special rule
        var apMatch = Regex.Match(text, @"Armour\s+Piercing\s*\((\d+)\)", RegexOptions.IgnoreCase);
        if (apMatch.Success)
            rules.ArmourPiercing = int.Parse(apMatch.Groups[1].Value);

        return rules;
    }
}
