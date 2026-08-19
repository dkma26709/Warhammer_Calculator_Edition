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

    /// <summary>The raw JSON unit entry (from <see cref="Unit.RawEntry"/>).</summary>
    public UnitEntry UnitEntry { get; }

    public ProfileEntry PrimaryProfile { get; }
    public ProfileEntry? MountProfile { get; }

    // ─── User configuration ─────────────────────────────────────

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

    /// <summary>
    /// Tracks selected equipment upgrades. Key = index into Unit.Options,
    /// Value = index into that option's Choices list. Absent or -1 = none selected.
    /// </summary>
    public Dictionary<int, int> SelectedUpgrades { get; } = new();

    /// <summary>
    /// Selections in "choose any" groups (<see cref="Json.UnitOption.AllowsMultipleChoices"/>),
    /// where a unit may take several choices at once. Key = index into Unit.Options, value =
    /// the chosen indices into that option's Choices list. A key present here supersedes the
    /// same key in <see cref="SelectedUpgrades"/>.
    /// </summary>
    public Dictionary<int, List<int>> SelectedMultiUpgrades { get; } = new();

    /// <summary>
    /// Every choice index selected in option group <paramref name="optionIndex"/> — any number
    /// for a "choose any" group, at most one otherwise. The one read path over both stores, so
    /// costing, equipment resolution and the roster cannot disagree.
    /// </summary>
    public IEnumerable<int> SelectedChoicesIn(int optionIndex)
    {
        if (SelectedMultiUpgrades.TryGetValue(optionIndex, out var many))
        {
            foreach (var idx in many)
                if (idx >= 0)
                    yield return idx;
        }
        else if (SelectedUpgrades.TryGetValue(optionIndex, out var one) && one >= 0)
        {
            yield return one;
        }
    }

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

    /// <summary>True when this unit has any wizard_upgrade option, indicating it can cast spells.</summary>
    public bool IsWizardCapable =>
        ResolvedUnit.Options.Any(o => o.IsType(OptionType.WizardUpgrade));

    /// <summary>Points cap for this unit's magic standard. Null when the unit can't carry one.</summary>
    public int? BannerBudget =>
        ResolvedUnit.Options
            .FirstOrDefault(o => o.IsRole(CommandRole.StandardBearer) && o.SubOptions != null)?
            .SubOptions!
            .FirstOrDefault(s => s.IsType(OptionType.MagicStandard))?
            .PointsLimit;

    /// <summary>Extra points cost for carrying the Battle Standard, if eligible and enabled.</summary>
    public double BattleStandardCost =>
        Loadout.IsBattleStandardBearer
            ? ResolvedUnit.Options.FirstOrDefault(o => o.IsType(OptionType.BattleStandard))?.Points ?? 0
            : 0;

    /// <summary>True when this unit is a character that can carry the Battle Standard.</summary>
    public bool IsBattleStandardEligible =>
        ResolvedUnit.Options.Any(o => o.IsType(OptionType.BattleStandard));

    /// <summary>
    /// Aggregate cost for faction upgrade selections. Rank-and-file units pay the rate on
    /// their own option (e.g. blessed_spawning at +2pts/model × spawnings × model count);
    /// characters take theirs through a shared magic item allowance, so their option carries
    /// no rate and each pick costs the points listed against it in the faction's extras.
    /// </summary>
    public double FactionUpgradeCost
    {
        get
        {
            double total = 0;
            foreach (var option in ResolvedUnit.Options)
            {
                var category = FactionUpgradeCategoryFor(option);
                if (category is null) continue;
                if (!Loadout.SelectedFactionUpgrades.TryGetValue(category, out var picks) || picks.Count == 0) continue;

                if (option.Points is null)
                {
                    total += picks.Sum(p => p.PointsCost);
                    continue;
                }

                double multiplier = option.PerModel == true ? ModelCount : 1;
                total += option.Points.Value * multiplier * picks.Count;
            }
            return total;
        }
    }

    /// <summary>
    /// Resolve the factionUpgrades category an option offers. Options name their own category;
    /// the blessed_spawning type predates that field and is mapped for compatibility.
    /// </summary>
    public static string? FactionUpgradeCategoryFor(UnitOption option) =>
        !string.IsNullOrWhiteSpace(option.FactionUpgrade)
            ? option.FactionUpgrade
            : FactionUpgradeCategoryForType(option.GetOptionType());

    /// <summary>Map an option type to the factionUpgrades category name.</summary>
    public static string? FactionUpgradeCategoryForType(OptionType? optionType) => optionType switch
    {
        OptionType.BlessedSpawning => "blessed_spawnings",
        _ => null,
    };

    // ─── Parsed base data ────────────────────────────────────────

    public TroopTypeInfo TroopType { get; }
    public int BaseWidthMm { get; }
    public ParsedSpecialRules SpecialRules { get; }

    /// <summary>
    /// Only the rules the army book scopes to the mount by name — the "Mighty Blow (2)" of
    /// "Mighty Blow (2) (War Boars only)". Empty for almost every unit, because a unit's special
    /// rules belong to its riders: a Dark Elf's Murderous Prowess is not its Cold One's.
    /// </summary>
    public ParsedSpecialRules MountSpecialRules { get; } = new();
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
        PrimaryProfile = unit.BaseModel.Stats;
        MountProfile = unit.Mount?.Stats;
        TroopType = unit.BaseModel.TroopType;
        BaseWidthMm = unit.BaseWidthMm;
        SpecialRules = unit.BaseModel.SpecialRules;
        MountSpecialRules = unit.Mount?.SpecialRules ?? new ParsedSpecialRules();
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
            ApplyWeaponUpgrades(weapons);
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
            ApplyArmourUpgrades(armour);
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

    private void ApplyWeaponUpgrades(List<CloseCombatWeaponKey> keys)
    {
        if (UnitEntry.Options == null) return;

        for (int i = 0; i < UnitEntry.Options.Count; i++)
        {
            var option = UnitEntry.Options[i];
            if (!option.IsType(OptionType.Equipment) || option.Choices == null) continue;

            foreach (int choiceIdx in SelectedChoicesIn(i))
            {
                if (choiceIdx >= option.Choices.Count) continue;

                var choice = option.Choices[choiceIdx];

                // Handle replace: remove the replaced item
                string? replaces = choice.Replaces ?? option.Replaces;
                if (option.IsAction(OptionAction.Replace) && replaces != null)
                {
                    if (EquipmentParser.TryNormalize(replaces, out var replacedKey, out bool replacedIsWeapon)
                        && replacedIsWeapon
                        && CloseCombatWeaponKeyExtensions.TryFromJsonKey(replacedKey, out var replacedEnumKey))
                    {
                        keys.Remove(replacedEnumKey);
                    }
                }

                // Add the selected items
                foreach (var name in choice.Name)
                {
                    if (EquipmentParser.TryNormalize(name, out var key, out bool itemIsWeapon)
                        && itemIsWeapon
                        && CloseCombatWeaponKeyExtensions.TryFromJsonKey(key, out var enumKey)
                        && !keys.Contains(enumKey))
                    {
                        keys.Add(enumKey);
                    }
                }
            }
        }
    }

    private void ApplyArmourUpgrades(List<ArmourKey> keys)
    {
        if (UnitEntry.Options == null) return;

        for (int i = 0; i < UnitEntry.Options.Count; i++)
        {
            var option = UnitEntry.Options[i];
            if (!option.IsType(OptionType.Equipment) || option.Choices == null) continue;

            foreach (int choiceIdx in SelectedChoicesIn(i))
            {
                if (choiceIdx >= option.Choices.Count) continue;

                var choice = option.Choices[choiceIdx];

                // Handle replace: remove the replaced item
                string? replaces = choice.Replaces ?? option.Replaces;
                if (option.IsAction(OptionAction.Replace) && replaces != null)
                {
                    if (EquipmentParser.TryNormalize(replaces, out var replacedKey, out bool replacedIsWeapon)
                        && !replacedIsWeapon
                        && ArmourKeyExtensions.TryFromJsonKey(replacedKey, out var replacedEnumKey))
                    {
                        keys.Remove(replacedEnumKey);
                    }
                }

                // Add the selected items
                foreach (var name in choice.Name)
                {
                    if (EquipmentParser.TryNormalize(name, out var key, out bool itemIsWeapon)
                        && !itemIsWeapon
                        && ArmourKeyExtensions.TryFromJsonKey(key, out var enumKey)
                        && !keys.Contains(enumKey))
                    {
                        keys.Add(enumKey);
                    }
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
        if (MountProfile != null && TroopType.HasSplitProfile)
            return Math.Max(primary, stat(MountProfile) ?? fallback);
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

        // Zero in wap-v3, which bars mounts from supporting attacks entirely.
        int mountSupportPerModel = Math.Min(mountA, TroopType.MountSupportingAttacks);
        int supportModels = actualSupportRanks * Frontage;
        int supportAttacks = supportModels * mountSupportPerModel;

        return frontAttacks + supportAttacks;
    }

    public static (ProfileEntry Primary, ProfileEntry? Mount) ResolveProfiles(UnitEntry unit)
    {
        var profiles = unit.Profiles;
        if (profiles.Count == 0)
            throw new InvalidOperationException($"Unit {unit.Name} has no profiles");
        if (profiles.Count == 1)
            return (profiles[0], null);

        var primary = profiles.FirstOrDefault(p => p.Points.HasValue) ?? profiles[0];
        var mount = profiles.FirstOrDefault(p => p != primary);

        return (primary, mount);
    }

    // ─── Points Cost ─────────────────────────────────────────────

    public double BaseModelCost => ResolvedUnit.BaseModelCost;

    /// <summary>Total points cost of all selected upgrades (equipment, mounts, upgrades, etc.).</summary>
    public double UpgradeCost
    {
        get
        {
            if (UnitEntry.Options == null) return 0;
            double total = 0;

            for (int i = 0; i < UnitEntry.Options.Count; i++)
            {
                var option = UnitEntry.Options[i];

                foreach (int choiceIdx in SelectedChoicesIn(i))
                {
                    switch (option.GetOptionType())
                    {
                        case OptionType.Equipment or OptionType.Mount:
                            if (option.Choices == null || choiceIdx >= option.Choices.Count) continue;
                            var choice = option.Choices[choiceIdx];
                            var choicePts = choice.Points ?? 0;
                            total += choice.PerModel ? choicePts * ModelCount : choicePts;
                            break;

                        case OptionType.Upgrade when option.SubOptions != null:
                            if (choiceIdx >= option.SubOptions.Count) continue;
                            var subOption = option.SubOptions[choiceIdx];
                            var subChoice = subOption.Choices?.FirstOrDefault();
                            var subPts = subChoice?.Points ?? subOption.Points ?? 0;
                            var subPerModel = subChoice?.PerModel ?? subOption.PerModel ?? false;
                            total += subPerModel ? subPts * ModelCount : subPts;
                            break;

                        case OptionType.Upgrade or OptionType.WizardUpgrade or OptionType.BlessedSpawning:
                            var flatPts = option.Points ?? 0;
                            var flatPerModel = option.PerModel ?? false;
                            total += flatPerModel ? flatPts * ModelCount : flatPts;
                            break;
                    }
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
