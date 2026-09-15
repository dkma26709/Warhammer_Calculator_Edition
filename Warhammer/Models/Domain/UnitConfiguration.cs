using System.Text.RegularExpressions;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Models.Domain;

public class UnitConfiguration
{
    // ─── Source data ─────────────────────────────────────────────

    /// <summary>The fully-resolved unit template.</summary>
    public Unit ResolvedUnit { get; }

    /// <summary>The raw bundle entry (from <see cref="Unit.RawEntry"/>).</summary>
    public UnitEntry UnitEntry { get; }

    // ─── User configuration ─────────────────────────────────────

    private string _selectedProfileId;

    /// <summary>
    /// Which of the entry's priced profiles is fielded — the Captain or the General of a
    /// COMMANDERS entry. An id the entry does not have is ignored.
    /// </summary>
    public string SelectedProfileId
    {
        get => _selectedProfileId;
        set
        {
            if (ResolvedUnit.Profiles.Any(p => string.Equals(p.Id, value, StringComparison.OrdinalIgnoreCase)))
                _selectedProfileId = value;
        }
    }

    public ProfileEntry PrimaryProfile => ResolvedUnit.ProfileById(_selectedProfileId);

    /// <summary>
    /// The mount's profile: the mount chosen from the entry's mount option, else the mount the
    /// entry's own profile block prints — a cavalry unit's horses — else null.
    /// </summary>
    public ProfileEntry? MountProfile => SelectedMount?.Model.Stats ?? ResolvedUnit.Mount?.Stats;

    public int ModelCount { get; set; }
    public int Frontage { get; set; }

    /// <summary>Army-builder state (custom name, wizard config, banner, General/BSB, faction upgrades).</summary>
    public ArmyLoadout Loadout { get; } = new();

    /// <summary>Selected weapon as enum key.</summary>
    public CloseCombatWeaponKey SelectedWeapon { get; set; }

    /// <summary>String accessor for <see cref="SelectedWeapon"/> — used by Blazor select binding.</summary>
    public string SelectedWeaponKey
    {
        get => SelectedWeapon.ToJsonKey();
        set
        {
            if (CloseCombatWeaponKeyExtensions.TryFromJsonKey(value, out var key))
                SelectedWeapon = key;
        }
    }

    public bool HasChampion { get; set; }
    public bool HasMusician { get; set; }
    public bool HasStandardBearer { get; set; }
    public ShadowDance SelectedShadowDance { get; set; }
    public TreeAspect SelectedTreeAspect { get; set; }
    public bool UseTreeWhack { get; set; }

    /// <summary>What the unit has taken from its option groups, by group id and choice id.</summary>
    public OptionSelections Selections { get; } = new();

    /// <summary>Selected magic items with their configured combat modifiers.</summary>
    public List<SelectedMagicItem> SelectedMagicItems { get; } = [];

    /// <summary>Generic modifiers applied to this unit (from magic items, spells, or manual entry).</summary>
    public List<UnitModifier> Modifiers { get; } = [];

    // ─── Capability queries (derived from the unit, not user state) ──

    /// <summary>
    /// How to refer to this entry in messages: the name the user gave it, qualified by the
    /// book's name, so a list with three Captains says which one is meant.
    /// </summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Loadout.CustomName)
        ? ResolvedUnit.Name
        : $"{Loadout.CustomName} ({ResolvedUnit.Name})";

    /// <summary>
    /// The option groups in force for this configuration: the entry's top-level groups that
    /// apply to the fielded profile, and under each choice that has been taken, the groups it
    /// unlocks — a standard bearer's magic standard, a champion's pistol. Command choices count
    /// as taken through <see cref="HasChampion"/> and its siblings.
    /// </summary>
    public IEnumerable<OptionGroup> ActiveGroups() => ActiveGroups(ResolvedUnit.OptionGroups);

    private IEnumerable<OptionGroup> ActiveGroups(IEnumerable<OptionGroup> groups)
    {
        foreach (var group in groups)
        {
            if (!group.AppliesToProfile(_selectedProfileId)) continue;
            yield return group;

            foreach (var choice in group.Choices)
            {
                if (choice.Options is null || !IsTaken(group, choice)) continue;
                foreach (var nested in ActiveGroups(choice.Options))
                    yield return nested;
            }
        }
    }

    /// <summary>Whether a choice of a group is in effect, whatever kind of group it is.</summary>
    public bool IsTaken(OptionGroup group, OptionChoice choice)
    {
        if (!choice.AppliesToProfile(_selectedProfileId)) return false;

        return group.GetKind() switch
        {
            OptionKind.Command => CommandRoleExtensions.TryFromRoleText(group.Role, out var role) && role switch
            {
                CommandRole.Champion => HasChampion,
                CommandRole.Musician => HasMusician,
                CommandRole.StandardBearer => HasStandardBearer,
                _ => false,
            },
            OptionKind.BattleStandard => Loadout.IsBattleStandardBearer,
            OptionKind.MagicItems => false,
            OptionKind.MagicStandard => false,
            _ => Selections.IsSelected(group.Id, choice.Id),
        };
    }

    /// <summary>The choices taken in a group, each with how many times.</summary>
    public IEnumerable<(OptionChoice Choice, int Count)> TakenIn(OptionGroup group)
    {
        foreach (var choice in group.Choices)
        {
            if (!IsTaken(group, choice)) continue;
            int count = group.GetSelectMode() == SelectMode.Count
                ? Math.Max(1, Selections.CountOf(group.Id, choice.Id))
                : 1;
            yield return (choice, count);
        }
    }

    /// <summary>The most a count choice may be taken, from its own max and the unit's size.</summary>
    public int MaxCountOf(OptionChoice choice)
    {
        int max = choice.Max ?? int.MaxValue;
        if (choice.EveryModels is { } every && every > 0)
            max = Math.Min(max, ModelCount / every);
        return Math.Max(0, max);
    }

    /// <summary>True when this unit can cast: it has a wizard level option, or the book gives it a wizard paragraph.</summary>
    public bool IsWizardCapable =>
        ActiveGroups().Any(g => g.IsKind(OptionKind.WizardLevel))
        || (!string.IsNullOrWhiteSpace(UnitEntry.Magic)
            && UnitEntry.Magic.Contains("wizard", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The magic standard this entry may carry. A unit takes one through its standard bearer;
    /// a character through carrying the Battle Standard. Null when it may carry none; a null
    /// points limit means the book set none — "with no points limit".
    /// </summary>
    public MagicStandardAllowance? MagicStandardAllowance
    {
        get
        {
            foreach (var group in ActiveGroups())
            {
                if (!group.IsKind(OptionKind.MagicStandard)) continue;

                // A group under a standard bearer is only active once the bearer is taken, so
                // reaching it here is the permission. A top-level one is the book's line for the
                // army's Battle Standard Bearer, or for a unit's standard when the book prints
                // it beside the command options rather than under them.
                bool nested = !ResolvedUnit.OptionGroups.Contains(group);
                bool permitted = nested
                    || (TroopType.IsCharacter ? Loadout.IsBattleStandardBearer : HasStandardBearer);
                if (permitted)
                    return new MagicStandardAllowance(group.PointsLimit);
            }

            return null;
        }
    }

    /// <summary>
    /// Points cap for this unit's magic standard. Null when the unit can't carry one; an
    /// unlimited allowance reads as the largest cap there is.
    /// </summary>
    public int? BannerBudget => MagicStandardAllowance is { } allowance
        ? allowance.PointsLimit ?? int.MaxValue
        : null;

    /// <summary>The battle standard group offered to the fielded profile, if any.</summary>
    private OptionGroup? BattleStandardGroup =>
        ActiveGroups().FirstOrDefault(g => g.IsKind(OptionKind.BattleStandard));

    /// <summary>Extra points cost for carrying the Battle Standard, if eligible and enabled.</summary>
    public double BattleStandardCost =>
        Loadout.IsBattleStandardBearer && BattleStandardGroup is { } group
            ? group.Points ?? group.Choices.FirstOrDefault()?.Points ?? 0
            : 0;

    /// <summary>True when this unit is a character that can carry the Battle Standard.</summary>
    public bool IsBattleStandardEligible => BattleStandardGroup is not null;

    /// <summary>The magic item budget of the fielded profile, or null when it may take none.</summary>
    public MagicItemAllowance? MagicItemAllowance =>
        Unit.ResolveMagicItemAllowance(_selectedProfileId, ActiveGroups());

    /// <summary>
    /// The upgrade families this entry may pick from: those its own factionUpgrade groups
    /// offer, and those its magic item allowance admits ("one Virtue and/or Magic Items").
    /// </summary>
    public IEnumerable<OfferedFamily> OfferedFamilies
    {
        get
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in ActiveGroups().Where(g => g.IsKind(OptionKind.FactionUpgrade)))
            {
                if (string.IsNullOrWhiteSpace(group.Family) || !seen.Add(group.Family)) continue;
                var rate = group.Choices.FirstOrDefault();
                yield return new OfferedFamily(group.Family, group.PointsLimit, rate?.Points, rate?.PerModel ?? false, false);
            }

            if (MagicItemAllowance is { } allowance)
            {
                foreach (var family in allowance.Families)
                {
                    if (!seen.Add(family)) continue;
                    yield return new OfferedFamily(family, allowance.PointsLimit, null, false, true);
                }
            }
        }
    }

    /// <summary>
    /// Aggregate cost for faction upgrade selections. A family offered by a group with a rate
    /// of its own — "+2 pts/model per spawning" — charges that rate per pick, per model when
    /// the book says so; otherwise each pick costs what the book lists against it.
    /// </summary>
    public double FactionUpgradeCost
    {
        get
        {
            double total = 0;
            var offered = OfferedFamilies.ToDictionary(f => f.Family, StringComparer.OrdinalIgnoreCase);

            foreach (var (family, picks) in Loadout.SelectedFactionUpgrades)
            {
                if (picks.Count == 0) continue;

                if (offered.TryGetValue(family, out var offer) && offer.Rate is { } rate)
                {
                    double multiplier = offer.RatePerModel ? ModelCount : 1;
                    total += rate * multiplier * picks.Count;
                    continue;
                }

                total += picks.Sum(p => p.PointsCost);
            }

            return total;
        }
    }

    /// <summary>
    /// What the entry has spent against its magic item allowance: its items, plus the picks
    /// from any upgrade family the same allowance pays for.
    /// </summary>
    public double MagicItemSpend
    {
        get
        {
            double spent = SelectedMagicItems.Sum(i => i.PointsCost);
            if (MagicItemAllowance is not { } allowance) return spent;

            foreach (var family in allowance.Families)
            {
                if (Loadout.SelectedFactionUpgrades.TryGetValue(family, out var picks))
                    spent += picks.Sum(p => p.PointsCost);
            }

            return spent;
        }
    }

    // ─── Parsed base data ────────────────────────────────────────

    /// <summary>The mount chosen from the entry's mount option, if it names a mount the book prints.</summary>
    public MountOption? SelectedMount
    {
        get
        {
            foreach (var group in ActiveGroups().Where(g => g.IsKind(OptionKind.Mount)))
            {
                foreach (var (choice, _) in TakenIn(group))
                {
                    if (choice.UnitId is not null
                        && ResolvedUnit.MountOptions.TryGetValue(choice.UnitId, out var mount))
                        return mount;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// The troop type the model fights as: the entry's own, or — once a character has taken a
    /// mount — the type that mount makes it.
    /// </summary>
    public TroopTypeInfo TroopType => SelectedMount?.RiderTroopType ?? ResolvedUnit.BaseModel.TroopType;

    public int BaseWidthMm { get; }
    public ParsedSpecialRules SpecialRules { get; }

    /// <summary>
    /// Only the rules the army book scopes to the mount by name — the "Mighty Blow (2)" of
    /// "Mighty Blow (2) (War Boars only)" — or, for a chosen mount, the mount entry's own rules.
    /// Empty for almost every unit, because a unit's special rules belong to its riders.
    /// </summary>
    public ParsedSpecialRules MountSpecialRules =>
        SelectedMount?.Model.SpecialRules ?? ResolvedUnit.Mount?.SpecialRules ?? new ParsedSpecialRules();

    public List<CloseCombatWeaponKey> BaseWeaponKeys { get; }
    public List<ArmourKey> BaseArmourKeys { get; }

    // ─── Modifier system ─────────────────────────────────────────

    /// <summary>
    /// Faction ability selections (Shadow Dances, Tree Aspects) expressed as ordinary
    /// modifiers, so their effects flow through the same pipeline as magic items and
    /// spells instead of being special-cased in the combat engine.
    /// </summary>
    private IEnumerable<UnitModifier> FactionAbilityModifiers
    {
        get
        {
            switch (SelectedShadowDance)
            {
                case ShadowDance.WhirlingDeath:
                    yield return new UnitModifier
                    {
                        Label = "Whirling Death", Source = "Faction Ability",
                        RuleOverrides = new ParsedSpecialRules { KillingBlow = true, ArmourPiercing = 1 },
                    };
                    break;
                case ShadowDance.StormOfBlades:
                    yield return new UnitModifier
                    {
                        Label = "Storm of Blades", Source = "Faction Ability",
                        AttacksBonus = 1,
                    };
                    break;
                case ShadowDance.TheShadowsCoil:
                    yield return new UnitModifier
                    {
                        Label = "The Shadows Coil", Source = "Faction Ability",
                        RuleOverrides = new ParsedSpecialRules { Minus1ToBeHit = true },
                    };
                    break;
                case ShadowDance.WovenMist:
                    yield return new UnitModifier
                    {
                        Label = "Woven Mist", Source = "Faction Ability",
                        RuleOverrides = new ParsedSpecialRules { AlwaysStrikesFirst = true },
                    };
                    break;
            }

            switch (SelectedTreeAspect)
            {
                case TreeAspect.Birch:
                    yield return new UnitModifier
                    {
                        Label = "Birch", Source = "Faction Ability",
                        AttacksBonus = 1,
                    };
                    break;
                case TreeAspect.Oak:
                    yield return new UnitModifier
                    {
                        Label = "Oak", Source = "Faction Ability",
                        ToughnessBonus = 1,
                    };
                    break;
                case TreeAspect.Willow:
                    yield return new UnitModifier
                    {
                        Label = "Willow", Source = "Faction Ability",
                        RuleOverrides = new ParsedSpecialRules { Minus1ToBeHit = true },
                    };
                    break;
            }
        }
    }

    /// <summary>All modifiers combined: magic items + faction abilities + standalone modifiers.</summary>
    private IEnumerable<UnitModifier> AllModifiers =>
        SelectedMagicItems.Select(mi => mi.Modifier)
            .Concat(FactionAbilityModifiers)
            .Concat(Modifiers);

    /// <summary>Aggregate of all modifiers — stat bonuses summed, booleans ORed, saves take best.</summary>
    public UnitModifier AggregateModifier => UnitModifier.Aggregate(AllModifiers);

    /// <summary>
    /// Base special rules merged with all modifier rule overrides.
    /// Use this instead of <see cref="SpecialRules"/> in combat calculations.
    /// </summary>
    public ParsedSpecialRules EffectiveSpecialRules
    {
        get
        {
            var aggregate = AggregateModifier;
            return aggregate.RuleOverrides != null
                ? SpecialRules.Merge(aggregate.RuleOverrides)
                : SpecialRules;
        }
    }

    // ─── Constructors ────────────────────────────────────────────

    /// <summary>
    /// Construct from a fully-resolved <see cref="Domain.Unit"/>.
    /// No parsing happens here — all data is pre-resolved by the repository.
    /// </summary>
    public UnitConfiguration(Unit unit)
    {
        ResolvedUnit = unit;
        UnitEntry = unit.RawEntry;
        _selectedProfileId = unit.BaseModel.Stats.Id;
        BaseWidthMm = unit.BaseWidthMm;
        SpecialRules = unit.BaseModel.SpecialRules;
        BaseWeaponKeys = unit.BaseModel.Weapons.Select(w => w.Key).ToList();
        BaseArmourKeys = unit.BaseModel.Armour.Select(a => a.Key).ToList();

        // Defaults
        ModelCount = unit.BaseModel.TroopType.IsCharacter ? 1 : unit.MinUnitSize;
        Frontage = unit.BaseModel.TroopType.IsCharacter ? 1 : unit.BaseModel.TroopType.MinRankWidth;
        SelectedWeapon = unit.BaseModel.Weapons
            .Select(w => w.Key)
            .FirstOrDefault(CloseCombatWeaponKey.HandWeapon);
    }

    // ─── Resolved equipment access ───────────────────────────────

    /// <summary>Get the selected weapon as a resolved <see cref="Weapon"/> domain object.</summary>
    public Weapon? GetSelectedResolvedWeapon()
    {
        return ResolvedUnit.BaseModel.Weapons
            .FirstOrDefault(w => w.Key == SelectedWeapon);
    }

    /// <summary>
    /// The best Parry the model has before any combat modifier: its own special rule, its
    /// weapon's, and a shield or buckler when it fights with a hand weapon. Null when it has
    /// none. The negations that depend on the attacker — a Strength gap, or a split profile —
    /// belong to the attack and are applied by <c>AttackResolver</c>, not here.
    /// </summary>
    /// <param name="selectedWeapon">
    /// The resolved <see cref="SelectedWeapon"/>. Passed in because falling back to the core
    /// weapon table needs the repository, which this type has no handle on.
    /// </param>
    public int? BaseParry(Weapon? selectedWeapon)
    {
        int? parry = EffectiveSpecialRules.Parry;

        if (selectedWeapon?.Rules.Parry is { } weaponParry)
            parry = parry is { } own ? Math.Min(own, weaponParry) : weaponParry;

        if (SelectedWeapon == CloseCombatWeaponKey.HandWeapon)
        {
            foreach (var armourKey in EffectiveArmour)
            {
                if (ResolvedUnit.AvailableArmour.TryGetValue(armourKey, out var armour)
                    && armour.ParryValue is { } shieldParry)
                {
                    parry = parry is { } best ? Math.Min(best, shieldParry) : shieldParry;
                }
            }
        }

        return parry;
    }

    /// <summary>
    /// Pre-compute base armour modifier from resolved <see cref="Armour"/> objects,
    /// including equipment upgrades. Uses <see cref="Unit.AvailableArmour"/> for lookup.
    /// </summary>
    public int ComputeBaseArmourModifier()
    {
        int totalModifier = 0;
        var effectiveArmour = EffectiveArmour;

        foreach (var armourKey in effectiveArmour)
        {
            if (ResolvedUnit.AvailableArmour.TryGetValue(armourKey, out var armour))
                totalModifier += armour.CombatSaveModifier;
        }

        var rules = EffectiveSpecialRules;

        if (rules.NaturalArmour.HasValue)
            totalModifier += 7 - rules.NaturalArmour.Value;

        if (TroopType.IntrinsicArmourSave.HasValue)
            totalModifier += 7 - TroopType.IntrinsicArmourSave.Value;

        bool hasConventionalArmour = effectiveArmour.Any(IsBodyArmour);

        if (rules.ChaosArmour && hasConventionalArmour)
            totalModifier += 1;

        if (rules.GromrilArmour && hasConventionalArmour)
            totalModifier += 1;

        totalModifier += AggregateModifier.ArmourSaveBonus ?? 0;

        return totalModifier;
    }

    // ─── Effective Equipment (base + upgrades - replacements) ────

    /// <summary>
    /// All close combat weapon keys the unit currently has, considering base equipment and selected upgrades.
    /// </summary>
    public List<CloseCombatWeaponKey> EffectiveWeapons
    {
        get
        {
            var weapons = new List<CloseCombatWeaponKey>(BaseWeaponKeys);
            ApplyEquipmentUpgrades(weapons, null);
            return weapons;
        }
    }

    /// <summary>
    /// All armour keys the unit currently has, considering base equipment and selected upgrades.
    /// </summary>
    public List<ArmourKey> EffectiveArmour
    {
        get
        {
            var armour = new List<ArmourKey>(BaseArmourKeys);
            ApplyEquipmentUpgrades(null, armour);
            KeepBestBodyArmour(armour);
            return armour;
        }
    }

    /// <summary>True for the mutually exclusive body armour suits.</summary>
    public static bool IsBodyArmour(ArmourKey key) =>
        key is ArmourKey.LightArmour or ArmourKey.MediumArmour or ArmourKey.HeavyArmour;

    /// <summary>
    /// A model wears one suit of body armour: light, medium and heavy are alternatives, so
    /// taking heavy armour over a medium-armoured base replaces it rather than adding to it.
    /// Shields, bucklers and barding are separate pieces and keep stacking.
    /// </summary>
    /// <remarks>
    /// Applied here rather than in the upgrade path so it also covers extracted equipment
    /// strings that list two suits at once. An explicit "replace" option removes the replaced
    /// suit before this runs, so a deliberate downgrade still works.
    /// </remarks>
    private static void KeepBestBodyArmour(List<ArmourKey> keys)
    {
        var worn = keys.Where(IsBodyArmour).ToList();
        if (worn.Count <= 1) return;

        var best = worn.OrderByDescending(BodyArmourRank).First();
        keys.RemoveAll(k => IsBodyArmour(k) && k != best);
    }

    private static int BodyArmourRank(ArmourKey key) => key switch
    {
        ArmourKey.HeavyArmour => 3,
        ArmourKey.MediumArmour => 2,
        ArmourKey.LightArmour => 1,
        _ => 0,
    };

    /// <summary>
    /// Apply every taken equipment choice to the weapon list, the armour list, or both: a
    /// replaced item goes, the chosen items come. Either list may be null when the caller only
    /// wants the other.
    /// </summary>
    private void ApplyEquipmentUpgrades(List<CloseCombatWeaponKey>? weapons, List<ArmourKey>? armour)
    {
        foreach (var group in ActiveGroups())
        {
            if (!group.IsKind(OptionKind.Equipment)) continue;

            foreach (var (choice, _) in TakenIn(group))
            {
                if (choice.Replaces is { } replaces)
                {
                    foreach (var replaced in EquipmentParser.SplitChoiceName(replaces))
                    {
                        if (!EquipmentParser.TryNormalize(replaced, out var replacedKey, out bool replacedIsWeapon))
                            continue;

                        if (replacedIsWeapon && weapons is not null
                            && CloseCombatWeaponKeyExtensions.TryFromJsonKey(replacedKey, out var replacedWeapon))
                            weapons.Remove(replacedWeapon);
                        else if (!replacedIsWeapon && armour is not null
                            && ArmourKeyExtensions.TryFromJsonKey(replacedKey, out var replacedArmour))
                            armour.Remove(replacedArmour);
                    }
                }

                foreach (var name in EquipmentParser.SplitChoiceName(choice.Name))
                {
                    if (!EquipmentParser.TryNormalize(name, out var key, out bool itemIsWeapon))
                        continue;

                    if (itemIsWeapon && weapons is not null
                        && CloseCombatWeaponKeyExtensions.TryFromJsonKey(key, out var weaponKey)
                        && !weapons.Contains(weaponKey))
                        weapons.Add(weaponKey);
                    else if (!itemIsWeapon && armour is not null
                        && ArmourKeyExtensions.TryFromJsonKey(key, out var armourKey)
                        && !armour.Contains(armourKey))
                        armour.Add(armourKey);
                }
            }
        }
    }

    // ─── Combat Stats ────────────────────────────────────────────

    /// <summary>
    /// Calculate effective strength for a given weapon. Weapon Mighty Blow and unit Mighty Blow
    /// are cumulative — see <see cref="ApplyMightyBlow"/>.
    /// </summary>
    public int EffectiveStrength(Weapon? weapon, bool isCharging, int combatRound,
        int weaponMightyBlow = 0)
    {
        int baseS = (PrimaryProfile.S ?? 3) + (AggregateModifier.StrengthBonus ?? 0);
        int s = weapon?.Strength.Resolve(baseS) ?? baseS;
        return ApplyMightyBlow(s, isCharging, combatRound, weaponMightyBlow);
    }

    private int ApplyMightyBlow(int strength, bool isCharging, int combatRound, int weaponMightyBlow)
    {
        var rules = EffectiveSpecialRules;

        // "This rule is cumulative with other sources of Mighty Blow" — 2.32 p63 and 3.11 p64 use
        // the same sentence, so this is not a v3 divergence. Six close-combat weapons grant Mighty
        // Blow in each system, so taking the max understated first-round Strength on every unit
        // that had a bonus of its own as well: Orc Boar Boyz carry Mighty Blow (1) and may take a
        // light lance, itself Mighty Blow (1), and so strike at +2 rather than +1.
        int bonus = combatRound == 1 ? (rules.MightyBlow ?? 0) + weaponMightyBlow : 0;

        // Thunderous Charge grants "the Mighty Blow (2) special rule in turns when it charges", and
        // v3's Tusker Charge the same with (1). Those are keyed on the charge rather than the
        // round, and Mighty Blow covers the difference itself: "If a unit would gain Mighty Blow in
        // a round of close combat after the first for any reason, treat this as the first round of
        // combat for that purpose." In practice the engine only charges in round one, so this
        // matters to the rule's meaning rather than to any current simulation.
        if (isCharging && rules.ThunderousCharge)
            bonus += 2;
        if (isCharging && rules.TuskerCharge)
            bonus += 1;

        return strength + bonus;
    }

    /// <summary>Calculate effective armour save value (lower is better, 7+ = no save).</summary>
    public int EffectiveArmourSave(int attackerStrength, int attackerAP, int strengthModifier)
    {
        int totalModifier = ComputeBaseArmourModifier();
        return ApplyArmourPenalty(totalModifier, attackerAP, strengthModifier);
    }

    /// <summary>
    /// Fast path: uses a pre-computed base armour modifier to avoid per-call lookups.
    /// </summary>
    public int EffectiveArmourSaveFast(int baseArmourModifier, int attackerAP, int strengthModifier)
    {
        return ApplyArmourPenalty(baseArmourModifier, attackerAP, strengthModifier);
    }

    private static int ApplyArmourPenalty(int totalModifier, int attackerAP, int strengthModifier)
    {
        if (totalModifier == 0) return 7;
        int baseSave = 7 - totalModifier;
        int penalty = (-strengthModifier) + attackerAP;
        int modifiedSave = baseSave + penalty;
        return Math.Max(modifiedSave, 2);
    }

    /// <summary>Best WS for combat (max of rider/mount for split profiles), including modifier bonuses.</summary>
    public int BestWS => SplitProfileMax(p => p.WS, 1) + (AggregateModifier.WeaponSkillBonus ?? 0);

    /// <summary>Best T for combat (max of rider/mount for split profiles), including modifier bonuses.</summary>
    public int BestT => SplitProfileMax(p => p.T, 3) + (AggregateModifier.ToughnessBonus ?? 0);

    private int SplitProfileMax(Func<ProfileEntry, int?> stat, int fallback)
    {
        int primary = stat(PrimaryProfile) ?? fallback;
        var mount = MountProfile;
        if (mount != null && TroopType.HasSplitProfile)
            return Math.Max(primary, stat(mount) ?? fallback);
        return primary;
    }

    /// <summary>
    /// Whether this model fights as rider plus mount. The books call it a split profile, and
    /// distinguish it from a model of a mounted troop type that has no separate mount line.
    /// </summary>
    public bool HasMount => MountProfile != null && TroopType.HasSplitProfile;

    /// <summary>Number of complete ranks (models / frontage, rounded down).</summary>
    public int Ranks => Frontage > 0 ? ModelCount / Frontage : 1;

    /// <summary>Rank bonus for combat resolution. Max +3, requires minimum rank width.</summary>
    public int RankBonus
    {
        get
        {
            if (Frontage < TroopType.MinRankWidth) return 0;
            return Math.Clamp(Ranks - 1, 0, 3);
        }
    }

    /// <summary>Number of models that can fight in the front rank.</summary>
    public int ModelsInFrontRank => Math.Min(Frontage, ModelCount);

    /// <summary>
    /// Calculate total attacks from this unit against an opponent.
    /// Weapon rules can provide extra attacks and Fight in Extra Ranks.
    /// </summary>
    public int TotalAttacks(bool isCharging, int combatRound,
        int weaponExtraAttacks = 0, int weaponFiER = 0, int? attacksOverride = null)
    {
        int attacks = (attacksOverride ?? PrimaryProfile.A ?? 1) + (AggregateModifier.AttacksBonus ?? 0);

        var rules = EffectiveSpecialRules;

        // Frenzy: +1 Attack
        if (rules.Frenzy)
            attacks += 1;

        // Devastating Charge: +1 Attack on charge
        if (isCharging && rules.DevastatingCharge)
            attacks += 1;

        // Weapon extra attacks (e.g., Two Hand Weapons: +1 Attack)
        attacks += weaponExtraAttacks;

        // Front rank: full attacks
        int frontRankModels = ModelsInFrontRank;
        int frontRankAttacks = frontRankModels * attacks;

        // Supporting ranks
        int supportingRanks = Math.Max(0, Ranks - 1);

        // Fight in Extra Ranks (unit special rules + weapon, use best)
        int extraRanks = Math.Max(rules.FightInExtraRanks ?? 0, weaponFiER);

        // Lance Formation: FiER(1) and 3 supporting attacks on charge
        bool lanceCharging = isCharging && rules.LanceFormation;
        if (lanceCharging)
            extraRanks = Math.Max(extraRanks, 1);

        int maxSupportRanks = 1 + extraRanks;
        int actualSupportRanks = Math.Min(supportingRanks, maxSupportRanks);

        // Without a split profile the books state their own figure rather than rider-plus-mount.
        int typeSupportAttacks = HasMount
            ? TroopType.MaxSupportingAttacks
            : TroopType.NonSplitSupportingAttacks;
        int maxSupportAttacks = lanceCharging ? 3 : typeSupportAttacks;
        int supportAttacksPerModel = Math.Min(attacks, maxSupportAttacks);
        int supportingModels = actualSupportRanks * Frontage;
        int supportAttacks = supportingModels * supportAttacksPerModel;

        return frontRankAttacks + supportAttacks;
    }

    /// <summary>
    /// Calculate mount attacks including supporting ranks.
    /// </summary>
    public int MountAttacks()
    {
        if (!HasMount) return 0;
        int mountA = MountProfile!.A ?? 1;

        int frontAttacks = ModelsInFrontRank * mountA;

        int supportingRanks = Math.Max(0, Ranks - 1);
        int actualSupportRanks = Math.Min(supportingRanks, 1);

        // Zero where the system bars mounts from supporting attacks entirely.
        int mountSupportPerModel = Math.Min(mountA, TroopType.MountSupportingAttacks);
        int supportModels = actualSupportRanks * Frontage;
        int supportAttacks = supportModels * mountSupportPerModel;

        return frontAttacks + supportAttacks;
    }

    /// <summary>
    /// The profiles an entry prints, sorted into what it fields: the priced profiles are the
    /// variants a player chooses between; the first unpriced one after them is the mount the
    /// profile block carries — a cavalry unit's horse, a chariot's crew line is not one.
    /// </summary>
    public static (IReadOnlyList<ProfileEntry> Priced, ProfileEntry? Mount) ResolveProfiles(UnitEntry unit)
    {
        var profiles = unit.Profiles;
        if (profiles.Count == 0)
            throw new InvalidOperationException($"Unit {unit.Name} has no profiles");
        if (profiles.Count == 1)
            return ([profiles[0]], null);

        var priced = profiles.Where(p => p.Points.HasValue).ToList();
        if (priced.Count == 0)
            priced = [profiles[0]];

        var mount = profiles.FirstOrDefault(p => !priced.Contains(p));
        return (priced, mount);
    }

    // ─── Points Cost ─────────────────────────────────────────────

    /// <summary>Points per model of the fielded profile.</summary>
    public double BaseModelCost => PrimaryProfile.Points ?? ResolvedUnit.BaseModelCost;

    /// <summary>
    /// Total points cost of all taken option choices: equipment, mounts, wizard levels, special
    /// rules and upgrades. Command, banners, magic items, the battle standard and faction
    /// upgrades are priced by their own properties.
    /// </summary>
    public double UpgradeCost
    {
        get
        {
            double total = 0;

            foreach (var group in ActiveGroups())
            {
                switch (group.GetKind())
                {
                    case OptionKind.Equipment:
                    case OptionKind.Mount:
                    case OptionKind.WizardLevel:
                    case OptionKind.SpecialRule:
                    case OptionKind.Upgrade:
                    case null:
                        break;
                    default:
                        continue;
                }

                foreach (var (choice, count) in TakenIn(group))
                {
                    var points = choice.Points ?? 0;
                    total += (choice.PerModel ? points * ModelCount : points) * count;
                }
            }

            return total;
        }
    }

    // ─── Command group ───────────────────────────────────────────

    public bool CanHaveChampion => ResolvedUnit.Command.CanHaveChampion;
    public bool CanHaveMusician => ResolvedUnit.Command.CanHaveMusician;
    public bool CanHaveStandardBearer => ResolvedUnit.Command.CanHaveStandardBearer;

    public (double Champion, double Musician, double Standard) CommandGroupCosts
    {
        get
        {
            var cmd = ResolvedUnit.Command;
            return (cmd.ChampionCost, cmd.MusicianCost, cmd.StandardBearerCost);
        }
    }

    public double TotalPointsCost
    {
        get
        {
            double baseCost = BaseModelCost * ModelCount;
            double equipmentCost = UpgradeCost;
            var (champCost, musCost, stdCost) = CommandGroupCosts;
            double commandCost = (HasChampion ? champCost : 0)
                               + (HasMusician ? musCost : 0)
                               + (HasStandardBearer ? stdCost : 0);
            double magicItemsCost = SelectedMagicItems.Sum(mi => mi.PointsCost);
            double bannerCost = Loadout.SelectedBanner?.PointsCost ?? 0;
            return baseCost + equipmentCost + commandCost + magicItemsCost + bannerCost
                 + BattleStandardCost + FactionUpgradeCost;
        }
    }

    /// <summary>Copy every user choice from another configuration of the same entry.</summary>
    public void CopySelectionsFrom(UnitConfiguration source, bool includeIdentity)
    {
        SelectedProfileId = source.SelectedProfileId;
        ModelCount = source.ModelCount;
        Frontage = source.Frontage;
        HasChampion = source.HasChampion;
        HasMusician = source.HasMusician;
        HasStandardBearer = source.HasStandardBearer;
        SelectedWeapon = source.SelectedWeapon;
        SelectedShadowDance = source.SelectedShadowDance;
        SelectedTreeAspect = source.SelectedTreeAspect;
        UseTreeWhack = source.UseTreeWhack;
        Selections.CopyFrom(source.Selections);
        SelectedMagicItems.Clear();
        SelectedMagicItems.AddRange(source.SelectedMagicItems);
        Modifiers.Clear();
        Modifiers.AddRange(source.Modifiers);

        Loadout.WizardLevel = source.Loadout.WizardLevel;
        Loadout.SelectedLore = source.Loadout.SelectedLore;
        Loadout.SelectedSpells.Clear();
        Loadout.SelectedSpells.AddRange(source.Loadout.SelectedSpells);
        Loadout.SelectedBanner = source.Loadout.SelectedBanner;
        Loadout.SelectedFactionUpgrades.Clear();
        foreach (var (family, picks) in source.Loadout.SelectedFactionUpgrades)
            Loadout.SelectedFactionUpgrades[family] = new List<SelectedFactionUpgrade>(picks);

        if (includeIdentity)
        {
            Loadout.CustomName = source.Loadout.CustomName;
            Loadout.IsGeneral = source.Loadout.IsGeneral;
            Loadout.IsBattleStandardBearer = source.Loadout.IsBattleStandardBearer;
        }
    }

    // ─── Static helpers ──────────────────────────────────────────

    public static int ParseBaseWidthMm(string? baseSize)
    {
        if (string.IsNullOrWhiteSpace(baseSize)) return 25;
        var firstLine = baseSize.Split('\n')[0];
        var match = Regex.Match(firstLine, @"(\d+)x(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : 25;
    }

    public static int ParseMinUnitSize(string? unitSize)
    {
        if (string.IsNullOrWhiteSpace(unitSize)) return 1;
        var match = Regex.Match(unitSize, @"(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : 1;
    }
}

/// <summary>An upgrade family an entry may pick from, and how its picks are priced.</summary>
/// <param name="Family">The family id — the faction's upgrade chapter in kebab-case.</param>
/// <param name="PointsLimit">The most the entry may spend on the family, when the book caps it.</param>
/// <param name="Rate">A rate on the entry's own option — "+2 pts/model" — that prices every pick; null when each pick costs what the book lists.</param>
/// <param name="RatePerModel">Whether <paramref name="Rate"/> is charged per model.</param>
/// <param name="ThroughMagicItems">True when the family comes through the entry's magic item allowance and its picks count against that budget.</param>
public sealed record OfferedFamily(string Family, int? PointsLimit, double? Rate, bool RatePerModel, bool ThroughMagicItems);

/// <summary>A magic standard the entry may carry; a null limit means the book set none.</summary>
public sealed record MagicStandardAllowance(int? PointsLimit);
