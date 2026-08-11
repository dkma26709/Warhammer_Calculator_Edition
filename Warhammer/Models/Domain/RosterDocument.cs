namespace Warhammer.Models.Domain;

/// <summary>
/// An army list flattened into what a printed roster shows: the sheet you put beside the
/// models. Built by <c>ArmyRosterBuilder</c> so the print view is pure markup — no lookups,
/// no points arithmetic and no parsing happen while rendering.
/// </summary>
public sealed record RosterDocument
{
    public required string ArmyName { get; init; }
    public required string FactionName { get; init; }
    public required string SystemName { get; init; }
    public required double TotalPoints { get; init; }
    public required int PointsTarget { get; init; }
    public required IReadOnlyList<RosterSection> Sections { get; init; }
}

/// <summary>One of the roster's headings — Characters, Core, Special, Rare — and its entries.</summary>
public sealed record RosterSection
{
    public required string Title { get; init; }
    public required double Points { get; init; }
    public required IReadOnlyList<RosterEntry> Entries { get; init; }
}

/// <summary>
/// One unit or character on the sheet. Every list is already resolved to display strings;
/// an empty list means the block is omitted rather than printed blank.
/// </summary>
public sealed record RosterEntry
{
    /// <summary>The name the sheet leads with — the player's own name for the entry, if it has one.</summary>
    public required string Name { get; init; }

    /// <summary>The army book's name for the entry, set only when <see cref="Name"/> is a custom one.</summary>
    public string? BookName { get; init; }

    /// <summary>Army roles worth calling out on the table: General, Battle Standard Bearer.</summary>
    public required IReadOnlyList<string> Roles { get; init; }

    /// <summary>Model count and frontage, e.g. "20 models · 5 wide".</summary>
    public required string ModelLine { get; init; }

    /// <summary>The model's profile, followed by its mount's when it has one.</summary>
    public required IReadOnlyList<RosterProfile> Profiles { get; init; }

    /// <summary>Saves worked out from equipment and rules, so they need no mid-game arithmetic.</summary>
    public required IReadOnlyList<string> Saves { get; init; }

    public required IReadOnlyList<string> Equipment { get; init; }

    /// <summary>Mounts, upgrades and faction upgrades taken — the picks equipment can't express.</summary>
    public required IReadOnlyList<string> Upgrades { get; init; }

    public required IReadOnlyList<RosterItem> MagicItems { get; init; }
    public required IReadOnlyList<string> SpecialRules { get; init; }

    /// <summary>Command group, wizard details, faction abilities and any manual modifiers.</summary>
    public required IReadOnlyList<string> Notes { get; init; }

    public required double Points { get; init; }
}

/// <summary>A magic item or standard, with what it cost.</summary>
public sealed record RosterItem(string Name, double Points);

/// <summary>A stat line, in <see cref="RosterProfile.Headers"/> order.</summary>
public sealed record RosterProfile(string Name, IReadOnlyList<string> Stats)
{
    public static readonly IReadOnlyList<string> Headers =
        ["M", "WS", "BS", "S", "T", "W", "I", "A", "Ld"];
}
