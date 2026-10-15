using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;

namespace Warhammer.Services;

/// <summary>
/// Flattens an <see cref="ArmyList"/> into a <see cref="RosterDocument"/> — the one-sheet
/// version a player takes to a game. Everything that would otherwise need looking up mid-battle
/// (saves, the names behind equipment keys, what each entry actually costs) is resolved here,
/// so the print view has nothing left to compute.
/// </summary>
public class ArmyRosterBuilder
{
    private readonly IGameDataRepository _repo;
    private readonly GameSystemService _system;

    public ArmyRosterBuilder(IGameDataRepository repo, GameSystemService system)
    {
        _repo = repo;
        _system = system;
    }

    /// <summary>
    /// The four headings a roster is written under. Characters print as one block: whether the
    /// book splits them into Lords and Heroes is a composition rule, and composition is settled
    /// before the sheet is printed. Anything in a category outside this layout still gets
    /// printed — see <see cref="OtherSectionTitle"/> — rather than quietly dropped.
    /// </summary>
    private static readonly (string Title, UnitCategory[] Categories)[] SectionLayout =
    [
        ("Characters", UnitCategoryExtensions.CharacterCategories),
        ("Core", [UnitCategory.Core]),
        ("Special", [UnitCategory.Special]),
        ("Rare", [UnitCategory.Rare]),
    ];

    private const string OtherSectionTitle = "Other";

    public RosterDocument Build(ArmyList list)
    {
        var sections = new List<RosterSection>();
        var placed = new HashSet<UnitCategory>();

        foreach (var (title, categories) in SectionLayout)
        {
            foreach (var category in categories) placed.Add(category);

            var entries = list.Units.Where(u => categories.Contains(u.ResolvedUnit.Category));
            if (Section(title, entries) is { } section) sections.Add(section);
        }

        var leftovers = list.Units.Where(u => !placed.Contains(u.ResolvedUnit.Category));
        if (Section(OtherSectionTitle, leftovers) is { } other) sections.Add(other);

        return new RosterDocument
        {
            ArmyName = string.IsNullOrWhiteSpace(list.Name) ? "Untitled List" : list.Name.Trim(),
            FactionName = _repo.GetFactionDisplayName(list.Faction),
            SystemName = GameSystems.DisplayNameFor(_system.CurrentSystem),
            TotalPoints = list.TotalPoints,
            PointsTarget = list.PointsTarget,
            Sections = sections,
        };
    }

    /// <summary>A section, or null when the list fields nothing in it — a blank heading helps nobody.</summary>
    private RosterSection? Section(string title, IEnumerable<UnitConfiguration> units)
    {
        var entries = units.Select(BuildEntry).ToList();
        if (entries.Count == 0) return null;

        return new RosterSection
        {
            Title = title,
            Points = entries.Sum(e => e.Points),
            Entries = entries,
        };
    }

    private RosterEntry BuildEntry(UnitConfiguration cfg)
    {
        var custom = cfg.Loadout.CustomName?.Trim();
        var named = !string.IsNullOrWhiteSpace(custom);

        return new RosterEntry
        {
            Name = named ? custom! : cfg.ResolvedUnit.Name,
            BookName = named ? cfg.ResolvedUnit.Name : null,
            Roles = Roles(cfg),
            ModelLine = ModelLine(cfg),
            Profiles = Profiles(cfg),
            Saves = Saves(cfg),
            Equipment = Equipment(cfg),
            Upgrades = Upgrades(cfg),
            MagicItems = MagicItems(cfg),
            SpecialRules = SpecialRules(cfg),
            Notes = Notes(cfg),
            Points = cfg.TotalPointsCost,
        };
    }

    // ─── Entry blocks ────────────────────────────────────────────

    private static IReadOnlyList<string> Roles(UnitConfiguration cfg)
    {
        var roles = new List<string>();
        if (cfg.Loadout.IsGeneral) roles.Add("General");
        if (cfg.Loadout.IsBattleStandardBearer) roles.Add("Battle Standard Bearer");
        return roles;
    }

    private static string ModelLine(UnitConfiguration cfg) =>
        cfg.ModelCount <= 1 ? "1 model" : $"{cfg.ModelCount} models";

    /// <summary>
    /// The rider's line, then the mount's. Printed whenever the book gives the entry a second
    /// profile, including the troop types that don't fight as a split profile — the sheet is
    /// there to save looking the mount up, whether or not it gets its own attacks.
    /// </summary>
    private static IReadOnlyList<RosterProfile> Profiles(UnitConfiguration cfg)
    {
        var profiles = new List<RosterProfile> { Profile(cfg.PrimaryProfile, cfg.ResolvedUnit.Name) };
        if (cfg.MountProfile != null)
            profiles.Add(Profile(cfg.MountProfile, cfg.ResolvedUnit.Mount?.Name ?? "Mount"));
        return profiles;
    }

    private static RosterProfile Profile(ProfileEntry profile, string fallbackName) => new(
        string.IsNullOrWhiteSpace(profile.Name) ? fallbackName : profile.Name,
        [Stat(profile.M), Stat(profile.WS), Stat(profile.BS), Stat(profile.S), Stat(profile.T),
         Stat(profile.W), Stat(profile.I), Stat(profile.A), Stat(profile.Ld)]);

    /// <summary>Matches the app's stat tables: a profile the book leaves blank prints as "*".</summary>
    private static string Stat(int? value) => value?.ToString() ?? "*";

    /// <summary>
    /// Every save the entry gets, already worked out, from the same calculations the combat
    /// engine uses. The armour figure is the <em>unmodified</em> save, so it is the right
    /// starting point for applying an attack's Armour Piercing at the table — the 2+ ceiling
    /// applies to the modified save, which is where the engine clamps it.
    /// </summary>
    private IReadOnlyList<string> Saves(UnitConfiguration cfg)
    {
        var saves = new List<string>();
        var modifier = cfg.ComputeBaseArmourModifier();

        saves.Add(modifier > 0 ? $"Armour {7 - modifier}+" : "Armour —");

        var rules = cfg.EffectiveSpecialRules;
        if (rules.WardSave is { } ward) saves.Add($"Ward {ward}+");
        if (rules.Regeneration is { } regeneration) saves.Add($"Regeneration {regeneration}+");

        // Parry and Dodge are save-type rules like the two above; the engine folds Dodge into
        // the ward roll, so leaving it off the sheet loses a save the model actually gets.
        var weapon = cfg.GetSelectedResolvedWeapon() ?? _repo.GetWeapon(cfg.SelectedWeapon);
        if (cfg.BaseParry(weapon) is { } parry) saves.Add($"Parry {parry}+");
        if (rules.Dodge is { } dodge) saves.Add($"Dodge {dodge}+");

        return saves;
    }

    /// <summary>
    /// What the models carry: close combat weapons, then the rest of the kit, then armour. The
    /// weapon the list was built around leads, and the effective lists already account for
    /// upgrades that add or replace gear.
    /// </summary>
    private IReadOnlyList<string> Equipment(UnitConfiguration cfg)
    {
        var items = new List<string>();

        foreach (var key in cfg.EffectiveWeapons.OrderByDescending(k => k == cfg.SelectedWeapon))
            if (_repo.GetWeapon(key) is { } weapon) items.Add(BookCase(weapon.Name));

        items.AddRange(UnmodelledEquipment(cfg));

        foreach (var key in cfg.EffectiveArmour)
            if (_repo.GetArmour(key) is { } armour) items.Add(BookCase(armour.Name));

        return Unique(items);
    }

    /// <summary>
    /// The kit the domain model has nowhere to put, recovered from the book's own equipment
    /// line. Two kinds end up here: missile weapons, which the engine doesn't simulate and so
    /// resolves to nothing — leaving Handgunners holding only a hand weapon — and gear the
    /// parser has no key for at all. Upgrades are applied the way
    /// <see cref="UnitConfiguration.EffectiveWeapons"/> applies them, so a replaced bow goes.
    /// </summary>
    private static IReadOnlyList<string> UnmodelledEquipment(UnitConfiguration cfg)
    {
        var items = new List<string>();

        foreach (var part in EquipmentParser.SplitItems(cfg.UnitEntry.Equipment))
            if (Unmodelled(part) is { } item) items.Add(item);

        var options = cfg.UnitEntry.Options;
        for (int i = 0; options != null && i < options.Count; i++)
        {
            var option = options[i];
            if (!option.IsType(OptionType.Equipment) || option.Choices == null) continue;

            foreach (int choiceIndex in cfg.SelectedChoicesIn(i))
            {
                if (choiceIndex >= option.Choices.Count) continue;

                var choice = option.Choices[choiceIndex];

                var replaced = choice.Replaces ?? option.Replaces;
                if (option.IsAction(OptionAction.Replace) && replaced != null && Unmodelled(replaced) is { } gone)
                    items.RemoveAll(item => SameGear(item, gone));

                foreach (var name in choice.Name)
                    if (Unmodelled(name) is { } item) items.Add(item);
            }
        }

        return items;
    }

    /// <summary>
    /// Whether two equipment names are the same gear. An option's <c>replaces</c> is written in
    /// whichever number the book used, so "Elven shortbows" has to match the "Elven shortbow" on
    /// the equipment line — for gear the parser has no key for, the strings are all there is to
    /// compare. Without this the replaced item stays on the sheet beside its replacement.
    /// </summary>
    private static bool SameGear(string a, string b) =>
        string.Equals(Singular(a), Singular(b), StringComparison.OrdinalIgnoreCase);

    private static string Singular(string name)
    {
        var trimmed = name.Trim();
        return trimmed.EndsWith('s') && !trimmed.EndsWith("ss", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^1]
            : trimmed;
    }

    /// <summary>
    /// How an equipment item reads on the sheet, or null when the effective weapon and armour
    /// lists already carry it. A missile weapon takes its key's name, so one book's "handgun"
    /// and another's "Handguns" print alike; gear the parser doesn't know keeps the book's
    /// own wording, which is the only description of it anyone has.
    /// </summary>
    private static string? Unmodelled(string name)
    {
        var trimmed = name.Trim().TrimEnd('.');
        if (trimmed.Length == 0) return null;

        if (!EquipmentParser.TryNormalize(trimmed, out var key, out bool isWeapon))
            return trimmed;

        return isWeapon && MissileWeaponKeyExtensions.TryFromJsonKey(key, out var missile)
            ? Spaced(missile.ToString())
            : null;
    }

    /// <summary>
    /// Mounts, upgrades and faction upgrades taken. Equipment options are left out: they are
    /// already in <see cref="Equipment"/>, resolved to what the models end up holding.
    /// </summary>
    private static IReadOnlyList<string> Upgrades(UnitConfiguration cfg)
    {
        var picks = new List<string>();
        var options = cfg.UnitEntry.Options;

        for (int i = 0; options != null && i < options.Count; i++)
        {
            var option = options[i];

            foreach (int choiceIndex in cfg.SelectedChoicesIn(i))
            {
                switch (option.GetOptionType())
                {
                    case OptionType.Mount when option.Choices != null && choiceIndex < option.Choices.Count:
                        picks.Add(option.Choices[choiceIndex].DisplayName);
                        break;

                    case OptionType.Upgrade when option.SubOptions != null:
                        if (choiceIndex >= option.SubOptions.Count) break;
                        var sub = option.SubOptions[choiceIndex];
                        picks.Add(sub.Choices?.FirstOrDefault()?.DisplayName ?? sub.Upgrade ?? "Upgrade");
                        break;

                    case OptionType.Upgrade or OptionType.WizardUpgrade or OptionType.BlessedSpawning:
                        if (!string.IsNullOrWhiteSpace(option.Upgrade)) picks.Add(option.Upgrade);
                        break;
                }
            }
        }

        foreach (var (_, selections) in cfg.Loadout.SelectedFactionUpgrades)
            picks.AddRange(selections.Select(s => s.Name));

        return Unique(picks);
    }

    private static IReadOnlyList<RosterItem> MagicItems(UnitConfiguration cfg)
    {
        var items = cfg.SelectedMagicItems
            .Select(item => new RosterItem(item.Name, item.PointsCost))
            .ToList();

        if (cfg.Loadout.SelectedBanner is { } banner)
            items.Add(new RosterItem(banner.Name, banner.PointsCost));

        return items;
    }

    /// <summary>
    /// The book's rules for the entry, by name — the whole SPECIAL RULES line, including the
    /// ones the book scopes to a single model, which the unit-wide name list keeps. The mount's
    /// set is concatenated for completeness and contributes nothing today:
    /// <see cref="ParsedSpecialRules.ParseScoped"/> clears the name list on every scoped set.
    /// </summary>
    private static IReadOnlyList<string> SpecialRules(UnitConfiguration cfg) =>
        Unique([.. cfg.SpecialRules.AllRuleNames, .. cfg.MountSpecialRules.AllRuleNames]);

    private static IReadOnlyList<string> Notes(UnitConfiguration cfg)
    {
        var notes = new List<string>();

        // Each command model with what it took in brackets: "Leader (Hochland long rifle)".
        (CommandRole Role, string Title)[] roles =
            [(CommandRole.Champion, "Leader"), (CommandRole.Musician, "Musician"), (CommandRole.StandardBearer, "Standard Bearer")];
        var taken = cfg.TakenCommandUpgrades;
        var command = new List<string>();
        foreach (var (role, title) in roles)
        {
            if (!cfg.HasRole(role)) continue;
            var gear = taken.Where(t => t.Role == role).Select(t => t.Name).ToList();
            command.Add(gear.Count == 0 ? title : $"{title} ({string.Join(", ", gear)})");
        }
        if (command.Count > 0) notes.Add("Command: " + string.Join(", ", command));

        if (cfg.IsWizardCapable)
        {
            var wizard = $"Wizard Level {cfg.Loadout.WizardLevel}";
            if (!string.IsNullOrWhiteSpace(cfg.Loadout.SelectedLore))
                wizard += $" · {cfg.Loadout.SelectedLore}";
            notes.Add(wizard);

            if (cfg.Loadout.SelectedSpells.Count > 0)
                notes.Add("Spells: " + string.Join(", ", cfg.Loadout.SelectedSpells));
        }

        if (cfg.SelectedShadowDance != ShadowDance.None)
            notes.Add($"Shadow Dance: {Spaced(cfg.SelectedShadowDance.ToString())}");

        if (cfg.SelectedTreeAspect != TreeAspect.None)
            notes.Add($"Tree Aspect: {cfg.SelectedTreeAspect}");

        if (cfg.UseTreeWhack) notes.Add("Tree Whack");

        // Magic items appear under their own heading; these are the modifiers entered by hand.
        notes.AddRange(cfg.Modifiers
            .Select(m => m.Label)
            .Where(label => !string.IsNullOrWhiteSpace(label)));

        return notes;
    }

    // ─── Formatting ──────────────────────────────────────────────

    /// <summary>
    /// Trim, drop blanks, and keep the first spelling of anything listed twice — a unit whose
    /// book line repeats a rule shouldn't print it twice on the sheet.
    /// </summary>
    private static IReadOnlyList<string> Unique(IEnumerable<string> values) =>
        values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// core-rules.json holds weapon and armour names as the rulebook's tables print them —
    /// "HAND WEAPON". Shouting suits a table and not a roster line, so an all-caps name is
    /// title-cased; a name that already has lower-case letters is left as the data has it.
    /// </summary>
    private static string BookCase(string name)
    {
        if (name.Any(char.IsLower)) return name;

        return string.Concat(name.Select((c, i) =>
            i > 0 && char.IsLetter(name[i - 1]) ? char.ToLowerInvariant(c) : c));
    }

    /// <summary>"StormOfBlades" → "Storm Of Blades", for the enum-named faction abilities.</summary>
    private static string Spaced(string pascalCase) =>
        string.Concat(pascalCase.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));
}
