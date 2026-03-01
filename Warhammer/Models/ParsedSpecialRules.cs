namespace Warhammer.Models;

using System.Text.RegularExpressions;

public class ParsedSpecialRules
{

    // === Boolean rules (on/off) ===
    public bool AlwaysStrikesFirst { get; set; }
    public bool AlwaysStrikesLast { get; set; }
    public bool AnimatedConstruct { get; set; }
    public bool Aquatic { get; set; }
    public bool BreathWeapon { get; set; }
    public bool ColdBlooded { get; set; }
    public bool DevastatingCharge { get; set; }
    public bool Ethereal { get; set; }
    public bool Expendable { get; set; }
    public bool Fear { get; set; }
    public bool FlamingAttacks { get; set; }
    public bool Flammable { get; set; }
    public bool Frenzy { get; set; }
    public bool Hatred { get; set; }
    public bool Hidden { get; set; }
    public bool IceAttacks { get; set; }
    public bool IgnoresArmourSaves { get; set; }
    public bool KillingBlow { get; set; }
    public bool HeroicKillingBlow { get; set; }
    public bool LightningAttacks { get; set; }
    public bool Loner { get; set; }
    public bool MagicalAttacks { get; set; }
    public bool MixedUnit { get; set; }
    public bool MoveOrFire { get; set; }
    public bool PoisonedAttacks { get; set; }
    public bool Ponderous { get; set; }
    public bool QuickToFire { get; set; }
    public bool RapidFire { get; set; }
    public bool RequiresTwoHands { get; set; }
    public bool SlowToFire { get; set; }
    public bool Sniper { get; set; }
    public bool Strider { get; set; }
    public bool Stubborn { get; set; }
    public bool Stupidity { get; set; }
    public bool Swiftstride { get; set; }
    public bool Terror { get; set; }
    public bool Unbreakable { get; set; }
    public bool Unstable { get; set; }
    public bool VolleyFire { get; set; }
    public bool WeaponTeam { get; set; }
    public bool ChaosArmour { get; set; }



    // === Numeric rules (value-based) ===
    public int ArmourPiercing { get; set; }
    public int? Dodge { get; set; }
    public int FightInExtraRanks { get; set; }
    public int? HoldYourGround { get; set; }
    public int? ImpactHits { get; set; }
    public int? InspiringPresence { get; set; }
    public int? MagicResistance { get; set; }
    public int MightyBlow { get; set; }
    public int MultipleShots { get; set; }
    public int? NaturalArmour { get; set; }
    public int? Parry { get; set; }
    public int? Regeneration { get; set; }
    public int? Stomp { get; set; }
    public int? WardSave { get; set; }

    // === String rules (dice expression or special text) ===
    public string? MultipleWounds { get; set; }    // e.g. "D3", "D6"
    public string? RandomAttacks { get; set; }      // e.g. "D6", "D3+1"
    public string? RandomMovement { get; set; }     // e.g. "2D6", "3D6"
    public string? Immunity { get; set; }           // e.g. "Flaming Attacks", "Killing Blow", "Psychology"
    public string? Loremaster { get; set; }         // e.g. "Fire", "Death"


    public bool HasAnyCombatRule =>
        AlwaysStrikesFirst || AlwaysStrikesLast || AnimatedConstruct ||
        DevastatingCharge || Ethereal || Fear || FlamingAttacks || Flammable ||
        Frenzy || Hatred || IceAttacks || IgnoresArmourSaves ||
        KillingBlow || HeroicKillingBlow || LightningAttacks ||
        MagicalAttacks || PoisonedAttacks ||
        ArmourPiercing > 0 || MightyBlow > 0 || MultipleShots > 0 ||
        FightInExtraRanks > 0 ||
        Dodge.HasValue || ImpactHits.HasValue || NaturalArmour.HasValue ||
        Parry.HasValue || Regeneration.HasValue || Stomp.HasValue ||
        WardSave.HasValue ||
        MultipleWounds != null || RandomAttacks != null ||
        Immunity != null || ChaosArmour;

    public bool HasAnyMovementRule =>
        Aquatic || Strider || Swiftstride || RandomMovement != null;

    public bool HasAnyLeadershipRule =>
        ColdBlooded || Stubborn || Stupidity || Unbreakable || Unstable ||
        Expendable || Fear || Terror ||
        HoldYourGround.HasValue || InspiringPresence.HasValue;

    public bool HasAnyShootingRule =>
        MoveOrFire || QuickToFire || RapidFire || SlowToFire ||
        Sniper || VolleyFire || MultipleShots > 0;

}

public static class SpecialRulesParser
{
    public static ParsedSpecialRules Parse(string? text)
    {
        var rules = new ParsedSpecialRules();
        if (string.IsNullOrWhiteSpace(text))
            return rules;

        // =============================================
        // NUMERIC RULES — extract value from (X+) or (X)
        // =============================================

        // Natural Armour (X+)
        var naturalArmourMatch = Regex.Match(text, @"Natural Armour\s*\((\d+)\+\)", RegexOptions.IgnoreCase);
        if (naturalArmourMatch.Success)
            rules.NaturalArmour = int.Parse(naturalArmourMatch.Groups[1].Value);

        // Regeneration (X+)
        var regenMatch = Regex.Match(text, @"Regeneration\s*\((\d+)\+\)", RegexOptions.IgnoreCase);
        if (regenMatch.Success)
            rules.Regeneration = int.Parse(regenMatch.Groups[1].Value);

        // Ward Save (X+)
        var wardMatch = Regex.Match(text, @"Ward\s+[Ss]ave\s*\((\d+)\+\)", RegexOptions.IgnoreCase);
        if (wardMatch.Success)
            rules.WardSave = int.Parse(wardMatch.Groups[1].Value);

        // Armour Piercing (X)
        var apMatch = Regex.Match(text, @"Armour\s+Piercing\s*\((\d+)\)", RegexOptions.IgnoreCase);
        if (apMatch.Success)
            rules.ArmourPiercing = int.Parse(apMatch.Groups[1].Value);

        // Dodge (X+)
        var dodgeMatch = Regex.Match(text, @"Dodge\s*\((\d+)\+\)", RegexOptions.IgnoreCase);
        if (dodgeMatch.Success)
            rules.Dodge = int.Parse(dodgeMatch.Groups[1].Value);

        // Fight in Extra Ranks (X)
        var fierMatch = Regex.Match(text, @"Fight\s+in\s+Extra\s+Ranks?\s*\((\d+)\)", RegexOptions.IgnoreCase);
        if (fierMatch.Success)
            rules.FightInExtraRanks = int.Parse(fierMatch.Groups[1].Value);

        // Hold Your Ground (X")
        var hygMatch = Regex.Match(text, @"Hold\s+Your\s+Ground\s*\((\d+)\""?\)", RegexOptions.IgnoreCase);
        if (hygMatch.Success)
            rules.HoldYourGround = int.Parse(hygMatch.Groups[1].Value);

        // Impact Hits (X)
        var impactMatch = Regex.Match(text, @"Impact\s+Hits?\s*\((\d+)\)", RegexOptions.IgnoreCase);
        if (impactMatch.Success)
            rules.ImpactHits = int.Parse(impactMatch.Groups[1].Value);

        // Inspiring Presence (X")
        var ipMatch = Regex.Match(text, @"Inspiring\s+Presence\s*\((\d+)\""?\)", RegexOptions.IgnoreCase);
        if (ipMatch.Success)
            rules.InspiringPresence = int.Parse(ipMatch.Groups[1].Value);

        // Magic Resistance (X)
        var mrMatch = Regex.Match(text, @"Magic\s+Resistance\s*\((\d+)\)", RegexOptions.IgnoreCase);
        if (mrMatch.Success)
            rules.MagicResistance = int.Parse(mrMatch.Groups[1].Value);

        // Mighty Blow (X)
        var mbMatch = Regex.Match(text, @"Mighty\s+Blow\s*\((\d+)\)", RegexOptions.IgnoreCase);
        if (mbMatch.Success)
            rules.MightyBlow = int.Parse(mbMatch.Groups[1].Value);

        // Multiple Shots (X)
        var msMatch = Regex.Match(text, @"Multiple\s+Shots?\s*\((\d+)\)", RegexOptions.IgnoreCase);
        if (msMatch.Success)
            rules.MultipleShots = int.Parse(msMatch.Groups[1].Value);

        // Parry (X+)
        var parryMatch = Regex.Match(text, @"Parry\s*\((\d+)\+\)", RegexOptions.IgnoreCase);
        if (parryMatch.Success)
            rules.Parry = int.Parse(parryMatch.Groups[1].Value);

        // Stomp (X)
        var stompMatch = Regex.Match(text, @"Stomp\s*\((\d+)\)", RegexOptions.IgnoreCase);
        if (stompMatch.Success)
            rules.Stomp = int.Parse(stompMatch.Groups[1].Value);

        // =============================================
        // STRING RULES — dice expressions or text
        // =============================================

        // Multiple Wounds (D3), (D6), (2), etc.
        var mwMatch = Regex.Match(text, @"Multiple\s+Wounds?\s*\(([^)]+)\)", RegexOptions.IgnoreCase);
        if (mwMatch.Success)
            rules.MultipleWounds = mwMatch.Groups[1].Value.Trim();

        // Random Attacks (D6), (D3+1), etc.
        var raMatch = Regex.Match(text, @"Random\s+Attacks?\s*\(([^)]+)\)", RegexOptions.IgnoreCase);
        if (raMatch.Success)
            rules.RandomAttacks = raMatch.Groups[1].Value.Trim();

        // Random Movement (2D6), (3D6), etc.
        var rmMatch = Regex.Match(text, @"Random\s+Movement\s*\(([^)]+)\)", RegexOptions.IgnoreCase);
        if (rmMatch.Success)
            rules.RandomMovement = rmMatch.Groups[1].Value.Trim();

        // Immunity (Flaming Attacks), (Killing Blow), (Psychology), etc.
        var immunityMatch = Regex.Match(text, @"Immunity\s*\(([^)]+)\)", RegexOptions.IgnoreCase);
        if (immunityMatch.Success)
            rules.Immunity = immunityMatch.Groups[1].Value.Trim();

        // Loremaster (Fire), (Death), etc.
        var lmMatch = Regex.Match(text, @"Loremaster\s*\(([^)]+)\)", RegexOptions.IgnoreCase);
        if (lmMatch.Success)
            rules.Loremaster = lmMatch.Groups[1].Value.Trim();

        // =============================================
        // BOOLEAN RULES — presence checks
        // =============================================

        // Always Strikes First
        if (Regex.IsMatch(text, @"Always\s+Strikes?\s+First", RegexOptions.IgnoreCase))
            rules.AlwaysStrikesFirst = true;

        // Always Strikes Last
        if (Regex.IsMatch(text, @"Always\s+Strikes?\s+Last", RegexOptions.IgnoreCase))
            rules.AlwaysStrikesLast = true;

        // Animated Construct
        if (Regex.IsMatch(text, @"Animated\s+Construct", RegexOptions.IgnoreCase))
            rules.AnimatedConstruct = true;

        // Aquatic
        if (Regex.IsMatch(text, @"\bAquatic\b", RegexOptions.IgnoreCase))
            rules.Aquatic = true;

        // Breath Weapon
        if (Regex.IsMatch(text, @"Breath\s+Weapons?", RegexOptions.IgnoreCase))
            rules.BreathWeapon = true;

        // Cold-blooded
        if (Regex.IsMatch(text, @"Cold[\s-]?blooded", RegexOptions.IgnoreCase))
            rules.ColdBlooded = true;

        // Devastating Charge
        if (Regex.IsMatch(text, @"Devastating\s+Charge", RegexOptions.IgnoreCase))
            rules.DevastatingCharge = true;

        // Ethereal
        if (Regex.IsMatch(text, @"\bEthereal\b", RegexOptions.IgnoreCase))
            rules.Ethereal = true;

        // Expendable
        if (Regex.IsMatch(text, @"\bExpendable\b", RegexOptions.IgnoreCase))
            rules.Expendable = true;

        // Flammable
        if (Regex.IsMatch(text, @"\bFlammable\b", RegexOptions.IgnoreCase))
            rules.Flammable = true;

        // Heroic Killing Blow (check before Killing Blow)
        if (Regex.IsMatch(text, @"Heroic\s+Killing\s+Blow", RegexOptions.IgnoreCase))
            rules.HeroicKillingBlow = true;

        // Killing Blow (not preceded by "Heroic")
        if (Regex.IsMatch(text, @"(?<!Heroic\s)Killing\s+Blow", RegexOptions.IgnoreCase))
            rules.KillingBlow = true;

        // Ice Attacks
        if (Regex.IsMatch(text, @"Ice\s+Attacks?", RegexOptions.IgnoreCase))
            rules.IceAttacks = true;

        // Ignores Armour Saves
        if (Regex.IsMatch(text, @"Ignores?\s+Armour\s+Saves?", RegexOptions.IgnoreCase))
            rules.IgnoresArmourSaves = true;

        // Lightning Attacks
        if (Regex.IsMatch(text, @"Lightning\s+Attacks?", RegexOptions.IgnoreCase))
            rules.LightningAttacks = true;

        // Loner
        if (Regex.IsMatch(text, @"\bLoner\b", RegexOptions.IgnoreCase))
            rules.Loner = true;

        // Magical Attacks
        if (Regex.IsMatch(text, @"Magical\s+Attacks?", RegexOptions.IgnoreCase))
            rules.MagicalAttacks = true;

        // Mixed Unit
        if (Regex.IsMatch(text, @"Mixed\s+Unit", RegexOptions.IgnoreCase))
            rules.MixedUnit = true;

        // Move or Fire
        if (Regex.IsMatch(text, @"Move\s+or\s+Fire", RegexOptions.IgnoreCase))
            rules.MoveOrFire = true;

        // Poisoned Attacks
        if (Regex.IsMatch(text, @"Poisoned\s+Attacks?", RegexOptions.IgnoreCase))
            rules.PoisonedAttacks = true;

        // Ponderous
        if (Regex.IsMatch(text, @"\bPonderous\b", RegexOptions.IgnoreCase))
            rules.Ponderous = true;

        // Quick to Fire
        if (Regex.IsMatch(text, @"Quick\s+to\s+Fire", RegexOptions.IgnoreCase))
            rules.QuickToFire = true;

        // Rapid Fire
        if (Regex.IsMatch(text, @"Rapid\s+Fire", RegexOptions.IgnoreCase))
            rules.RapidFire = true;

        // Requires Two Hands
        if (Regex.IsMatch(text, @"Requires?\s+Two\s+Hands?", RegexOptions.IgnoreCase))
            rules.RequiresTwoHands = true;

        // Slow to Fire
        if (Regex.IsMatch(text, @"Slow\s+to\s+Fire", RegexOptions.IgnoreCase))
            rules.SlowToFire = true;

        // Sniper
        if (Regex.IsMatch(text, @"\bSniper\b", RegexOptions.IgnoreCase))
            rules.Sniper = true;

        // Strider
        if (Regex.IsMatch(text, @"\bStrider\b", RegexOptions.IgnoreCase))
            rules.Strider = true;

        // Stubborn
        if (Regex.IsMatch(text, @"\bStubborn\b", RegexOptions.IgnoreCase))
            rules.Stubborn = true;

        // Stupidity
        if (Regex.IsMatch(text, @"\bStupidity\b", RegexOptions.IgnoreCase))
            rules.Stupidity = true;

        // Swiftstride
        if (Regex.IsMatch(text, @"\bSwiftstride\b", RegexOptions.IgnoreCase))
            rules.Swiftstride = true;

        // Fear
        if (Regex.IsMatch(text, @"\bFear\b", RegexOptions.IgnoreCase))
            rules.Fear = true;

        // Flaming Attacks
        if (Regex.IsMatch(text, @"Flaming\s+Attacks?", RegexOptions.IgnoreCase))
            rules.FlamingAttacks = true;

        // Frenzy
        if (Regex.IsMatch(text, @"\bFrenzy\b", RegexOptions.IgnoreCase))
            rules.Frenzy = true;

        // Hatred
        if (Regex.IsMatch(text, @"\bHatred\b", RegexOptions.IgnoreCase))
            rules.Hatred = true;

        // Hidden
        if (Regex.IsMatch(text, @"\bHidden\b", RegexOptions.IgnoreCase))
            rules.Hidden = true;

        // Terror
        if (Regex.IsMatch(text, @"\bTerror\b", RegexOptions.IgnoreCase))
            rules.Terror = true;

        // Unbreakable
        if (Regex.IsMatch(text, @"\bUnbreakable\b", RegexOptions.IgnoreCase))
            rules.Unbreakable = true;

        // Unstable
        if (Regex.IsMatch(text, @"\bUnstable\b", RegexOptions.IgnoreCase))
            rules.Unstable = true;

        // Volley Fire
        if (Regex.IsMatch(text, @"Volley\s+Fire", RegexOptions.IgnoreCase))
            rules.VolleyFire = true;

        // Weapon Team
        if (Regex.IsMatch(text, @"Weapon\s+Team", RegexOptions.IgnoreCase))
            rules.WeaponTeam = true;

        // Chaos Armour
        if (Regex.IsMatch(text, @"Chaos\s+Armour", RegexOptions.IgnoreCase))
            rules.ChaosArmour = true;

        return rules;
    }
}
