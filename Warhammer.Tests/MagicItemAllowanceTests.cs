using System.Text.Json;
using System.Text.RegularExpressions;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;

namespace Warhammer.Tests;

/// <summary>
/// An army book prints one options block per page, so an entry carries its neighbours'
/// allowances alongside its own. These cover picking the right one, and the shipped data
/// actually offering one wherever the book does.
/// </summary>
public class MagicItemAllowanceTests
{
    private static UnitOption Allowance(int limit, string? appliesTo = null) => new()
    {
        Type = "magic_items",
        PointsLimit = limit,
        AppliesTo = appliesTo,
        Raw = appliesTo is null
            ? $"May take Magic Items up to a total of {limit} points"
            : $"A {appliesTo} may take Magic Items up to a total of {limit} points",
    };

    [Fact]
    public void NoAllowanceOption_ResolvesToNull()
    {
        Assert.Null(Unit.ResolveMagicItemAllowance("Captain", []));
        Assert.Null(Unit.ResolveMagicItemAllowance("Captain", null));
    }

    [Fact]
    public void UnnamedAllowance_AppliesToWhoeverHoldsIt()
    {
        Assert.Equal(50, Unit.ResolveMagicItemAllowance("Captain", [Allowance(50)]));
    }

    [Fact]
    public void NamedAllowance_MatchingTheEntry_Wins()
    {
        // The Empire's General entry carries both its own line and the Captain's.
        var options = new List<UnitOption> { Allowance(50, "Captain"), Allowance(100, "General") };

        Assert.Equal(100, Unit.ResolveMagicItemAllowance("General", options));
        Assert.Equal(50, Unit.ResolveMagicItemAllowance("Captain", options));
    }

    [Fact]
    public void NamedAllowance_MatchesRegardlessOfCaseAndSpacing()
    {
        var options = new List<UnitOption> { Allowance(100, " battle wizard LORD ") };

        Assert.Equal(100, Unit.ResolveMagicItemAllowance("Battle Wizard Lord", options));
    }

    [Fact]
    public void UnnamedAllowance_BeatsANeighboursNamedOne()
    {
        var options = new List<UnitOption> { Allowance(100, "Grand Master"), Allowance(50) };

        Assert.Equal(50, Unit.ResolveMagicItemAllowance("Chapter Master", options));
    }

    [Fact]
    public void OnlyNeighboursAllowances_TakesTheSmallest()
    {
        // Nothing names this entry, so the book never granted it the larger budget.
        var options = new List<UnitOption> { Allowance(100, "Prince"), Allowance(50, "Noble") };

        Assert.Equal(50, Unit.ResolveMagicItemAllowance("Loremaster", options));
    }

    // ─── Shipped data ───────────────────────────────────────────────

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>The wording every allowance line ends with, whatever precedes it.</summary>
    private static readonly Regex AllowanceWording =
        new(@"Magic Items.*?\d+\s*points", RegexOptions.IgnoreCase);

    public static TheoryData<string, string> SystemFactions() =>
        FactionDeserializationTests.SystemFactions();

    /// <summary>
    /// A line the parser fails to recognise as an allowance does not disappear — it falls
    /// through to the generic equipment rule and becomes a checkbox that charges the cap as
    /// if it were a wargear upgrade. Every allowance line must parse as one.
    /// </summary>
    [Theory]
    [MemberData(nameof(SystemFactions))]
    public void EveryAllowanceLine_ParsesAsAnAllowance(string systemKey, string faction)
    {
        var json = File.ReadAllText(
            Path.Combine(TestPaths.SystemDataDir(systemKey), "factions", $"{faction}.json"));
        var data = JsonSerializer.Deserialize<FactionData>(json, JsonOptions)!;

        var stragglers = new List<string>();
        foreach (var entry in data.Units)
        {
            foreach (var option in entry.Options ?? [])
            {
                if (option.Raw is null || !AllowanceWording.IsMatch(option.Raw))
                    continue;

                // The companion option offering a shared upgrade family repeats the
                // allowance's wording; it is not itself an allowance.
                var type = option.GetOptionType();
                if (type is OptionType.MagicItems or OptionType.FactionUpgrade)
                    continue;

                stragglers.Add($"{entry.Name}: [{option.Type}] {option.Raw}");
            }
        }

        Assert.Empty(stragglers);
    }

    /// <summary>
    /// The item lists hold more categories than the six each book prints — Dwarf runes are in
    /// there too. An unrecognised key is not loud: <see cref="MagicItemCategories.Plural"/>
    /// returns it verbatim, so the picker shows an "weapon_rune" heading, and the validator
    /// silently treats it as a category a model may hold only one of.
    /// </summary>
    [Theory]
    [MemberData(nameof(SystemFactions))]
    public void EveryShippedItemCategory_IsRecognised(string systemKey, string faction)
    {
        var path = Path.Combine(
            TestPaths.SystemDataDir(systemKey), "factions", $"{faction}-extras.json");
        if (!File.Exists(path))
            return;

        var extras = JsonSerializer.Deserialize<FactionExtrasData>(
            File.ReadAllText(path), JsonOptions)!;

        var unnamed = extras.MagicItems
            .Select(i => i.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .Where(c => MagicItemCategories.Plural(c) == c)
            .ToList();

        Assert.Empty(unnamed);
    }

    /// <summary>
    /// Every entry the book gives an allowance to must resolve to exactly one budget, or the
    /// builder shows no magic item picker at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(SystemFactions))]
    public void EntriesWithAnAllowance_ResolveToABudget(string systemKey, string faction)
    {
        var json = File.ReadAllText(
            Path.Combine(TestPaths.SystemDataDir(systemKey), "factions", $"{faction}.json"));
        var data = JsonSerializer.Deserialize<FactionData>(json, JsonOptions)!;

        foreach (var entry in data.Units)
        {
            var allowances = (entry.Options ?? [])
                .Where(o => o.IsType(OptionType.MagicItems))
                .ToList();
            if (allowances.Count == 0)
                continue;

            var resolved = Unit.ResolveMagicItemAllowance(entry.Name, entry.Options);

            Assert.True(resolved > 0,
                $"{systemKey}/{faction}: {entry.Name} has {allowances.Count} allowance option(s) " +
                $"but resolved to {resolved?.ToString() ?? "null"}");
        }
    }
}
