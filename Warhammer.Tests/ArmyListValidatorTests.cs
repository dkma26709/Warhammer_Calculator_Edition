using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;

namespace Warhammer.Tests;

public class ArmyListValidatorTests
{
    private static readonly ArmyListValidatorInvoker V = new();

    // ─── Helpers ────────────────────────────────────────────────────

    private static UnitConfiguration MakeEntry(
        string name,
        UnitCategory category,
        double pointsPerModel,
        int count = 1,
        bool named = false)
    {
        var unit = TestHelpers.MakeUnit(name: name, points: pointsPerModel, category: category,
            troopType: category.IsCharacterCategory() ? "Infantry (Character)" : "Infantry");
        unit = unit with { Named = named };
        return new UnitConfiguration(unit) { ModelCount = count };
    }

    private static ArmyList MakeList(int target, params UnitConfiguration[] units) => new()
    {
        FactionId = "dwarfs",
        PointsTarget = target,
        Units = units.ToList(),
    };

    // ─── Happy path ─────────────────────────────────────────────────

    [Fact]
    public void ValidList_ReturnsNoIssues()
    {
        var lord = MakeEntry("Lord", UnitCategory.Characters, 200, 1);
        lord.Loadout.IsGeneral = true;
        var list = MakeList(2000,
            lord,                                                    // 200 (10%)
            MakeEntry("Warriors", UnitCategory.Core, 10, 50),       // 500 (25%)
            MakeEntry("Ironbreakers", UnitCategory.Special, 15, 20) // 300 (15%)
        );

        var issues = V.Validate(list);

        Assert.Empty(issues);
    }

    [Fact]
    public void NoGeneralDesignated_WithCharacter_WarnsNotErrors()
    {
        var list = MakeList(2000,
            MakeEntry("Lord", UnitCategory.Characters, 200, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 60));

        var issues = V.Validate(list);

        var general = Assert.Single(issues, i => i.Code == "NO_GENERAL_DESIGNATED");
        Assert.Equal(ValidationSeverity.Warning, general.Severity);
        Assert.DoesNotContain(issues, i => i.Code == "NO_GENERAL");
    }

    [Fact]
    public void MultipleGenerals_FlagsError()
    {
        var lord = MakeEntry("Lord", UnitCategory.Characters, 200, 1);
        lord.Loadout.IsGeneral = true;
        var thane = MakeEntry("Thane", UnitCategory.Characters, 100, 1);
        thane.Loadout.IsGeneral = true;
        var list = MakeList(2000, lord, thane, MakeEntry("Warriors", UnitCategory.Core, 10, 60));

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("MULTIPLE_GENERALS", codes);
    }

    [Fact]
    public void MultipleBsb_FlagsError()
    {
        var lord = MakeEntry("Lord", UnitCategory.Characters, 200, 1);
        lord.Loadout.IsGeneral = true;
        lord.Loadout.IsBattleStandardBearer = true;
        var thane = MakeEntry("Thane", UnitCategory.Characters, 100, 1);
        thane.Loadout.IsBattleStandardBearer = true;
        var list = MakeList(2000, lord, thane, MakeEntry("Warriors", UnitCategory.Core, 10, 60));

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("MULTIPLE_BSB", codes);
    }

    // ─── Invalid target ─────────────────────────────────────────────

    [Fact]
    public void ZeroPointsTarget_ReturnsOnlyInvalidTargetIssue()
    {
        var list = MakeList(0);

        var issues = V.Validate(list);

        Assert.Single(issues);
        Assert.Equal("INVALID_TARGET", issues[0].Code);
    }

    // ─── Percentage caps ────────────────────────────────────────────

    [Fact]
    public void LordsOverCap_FlagsError_UnderASplitBook()
    {
        var list = MakeList(2000,
            MakeEntry("Lord", UnitCategory.Lords, 600, 1),          // 600 > 500 cap
            MakeEntry("Warriors", UnitCategory.Core, 10, 60));

        var codes = V.Validate(list, TestCompositionRules.Split).Select(i => i.Code).ToList();

        Assert.Contains("LORDS_OVER_CAP", codes);
    }

    [Fact]
    public void HeroesOverCap_FlagsError_UnderASplitBook()
    {
        var list = MakeList(2000,
            MakeEntry("Thane", UnitCategory.Heroes, 600, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 60));

        var codes = V.Validate(list, TestCompositionRules.Split).Select(i => i.Code).ToList();

        Assert.Contains("HEROES_OVER_CAP", codes);
    }

    [Fact]
    public void AMergedBook_HasNoLordsOrHeroesCaps()
    {
        var list = MakeList(2000,
            MakeEntry("Lord", UnitCategory.Lords, 600, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 60));

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.DoesNotContain("LORDS_OVER_CAP", codes);
        Assert.DoesNotContain("HEROES_OVER_CAP", codes);
    }

    [Fact]
    public void CharactersCombinedOverCap_FlagsError()
    {
        // 400 + 400 = 800 total > 700 cap (35% of 2000).
        var list = MakeList(2000,
            MakeEntry("Lord", UnitCategory.Characters, 400, 1),
            MakeEntry("Thane", UnitCategory.Characters, 400, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 60));

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("CHARACTERS_OVER_CAP", codes);
    }

    [Fact]
    public void CoreBelowMin_FlagsError()
    {
        // 400 Core at 2000 target → below 500 (25%) min
        var list = MakeList(2000,
            MakeEntry("Lord", UnitCategory.Characters, 200, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 40));

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("CORE_BELOW_MIN", codes);
    }

    [Fact]
    public void SpecialOverCap_FlagsError()
    {
        var list = MakeList(2000,
            MakeEntry("Lord", UnitCategory.Characters, 200, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 60),
            MakeEntry("Ironbreakers", UnitCategory.Special, 15, 80)); // 1200 > 1000

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("SPECIAL_OVER_CAP", codes);
    }

    [Fact]
    public void RareOverCap_FlagsError()
    {
        var list = MakeList(2000,
            MakeEntry("Lord", UnitCategory.Characters, 200, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 60),
            MakeEntry("Cannon", UnitCategory.Rare, 600, 1)); // 600 > 500

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("RARE_OVER_CAP", codes);
    }

    // ─── General requirement ────────────────────────────────────────

    [Fact]
    public void NoCharacter_FlagsNoGeneral()
    {
        var list = MakeList(2000,
            MakeEntry("Warriors", UnitCategory.Core, 10, 60));

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("NO_GENERAL", codes);
    }

    [Fact]
    public void EmptyList_FlagsCoreMinAndNoGeneral()
    {
        var list = MakeList(2000);

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("CORE_BELOW_MIN", codes);
        Assert.Contains("NO_GENERAL", codes);
    }

    // ─── Total cap ──────────────────────────────────────────────────

    [Fact]
    public void OverPointsTarget_FlagsError()
    {
        var list = MakeList(1000,
            MakeEntry("Lord", UnitCategory.Characters, 200, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 100)); // 1000 + 200 = 1200 > 1000

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("OVER_POINTS_TARGET", codes);
    }

    // ─── Special characters ─────────────────────────────────────────

    [Fact]
    public void ASpecialCharacter_Twice_FlagsError()
    {
        var first = MakeEntry("Thorgrim", UnitCategory.NamedCharacters, 500, named: true);
        first.Loadout.IsGeneral = true;
        var list = MakeList(4000, first,
            MakeEntry("Thorgrim", UnitCategory.NamedCharacters, 500, named: true),
            MakeEntry("Warriors", UnitCategory.Core, 10, 100));

        var issue = Assert.Single(V.Validate(list), i => i.Code == "SPECIAL_CHARACTER_REPEATED");
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
    }

    // ─── The book's own constraints ─────────────────────────────────

    private static CompositionRules WithConstraints(params UnitConstraintEntry[] constraints) =>
        TestCompositionRules.War with { UnitConstraints = constraints };

    private static UnitConstraintEntry Max(string unitId, int max, string? profileId = null) => new()
    {
        Type = "max", UnitIds = [unitId], Max = max, ProfileId = profileId,
        Raw = $"You may not have more than {max} {unitId} in your army.",
    };

    [Fact]
    public void PerArmyLimit_Exceeded_FlagsError()
    {
        var list = MakeList(2000,
            MakeEntry("Lord", UnitCategory.Characters, 200, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 60),
            MakeEntry("Shrine", UnitCategory.Rare, 200, 1),
            MakeEntry("Shrine", UnitCategory.Rare, 200, 1));

        var codes = V.Validate(list, WithConstraints(Max("shrine", 1))).Select(i => i.Code).ToList();

        Assert.Contains("PER_ARMY_LIMIT_EXCEEDED", codes);
    }

    [Fact]
    public void PerArmyLimit_AtLimit_DoesNotFlag()
    {
        var list = MakeList(2000,
            MakeEntry("Lord", UnitCategory.Characters, 200, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 60),
            MakeEntry("Shrine", UnitCategory.Rare, 200, 1),
            MakeEntry("Shrine", UnitCategory.Rare, 200, 1));

        var codes = V.Validate(list, WithConstraints(Max("shrine", 2))).Select(i => i.Code).ToList();

        Assert.DoesNotContain("PER_ARMY_LIMIT_EXCEEDED", codes);
    }

    [Fact]
    public void PerArmyLimit_OnOneProfile_CountsOnlyThatProfile()
    {
        // "You may not have more than 2 Arch Lectors": the entry's Warrior Priests are not counted.
        var lector = new ProfileEntry { Id = "arch-lector", Name = "Arch Lector", Points = 150 };
        var priest = new ProfileEntry { Id = "warrior-priest", Name = "Warrior Priest", Points = 75 };
        UnitConfiguration Priest(string profile)
        {
            var unit = TestHelpers.MakeUnit(name: "Warrior Priests", troopType: "Infantry (Character)",
                category: UnitCategory.Characters, profiles: [lector, priest]);
            return new UnitConfiguration(unit) { SelectedProfileId = profile };
        }

        var general = Priest("arch-lector");
        general.Loadout.IsGeneral = true;
        var list = MakeList(3000, general, Priest("arch-lector"), Priest("warrior-priest"),
            MakeEntry("Warriors", UnitCategory.Core, 10, 80));
        var rules = WithConstraints(Max("warrior-priests", 2, profileId: "arch-lector"));

        Assert.DoesNotContain(V.Validate(list, rules), i => i.Code == "PER_ARMY_LIMIT_EXCEEDED");

        list.Units.Add(Priest("arch-lector"));
        Assert.Contains(V.Validate(list, rules), i => i.Code == "PER_ARMY_LIMIT_EXCEEDED");
    }

    [Fact]
    public void ARatio_MoreOfXThanY_FlagsError()
    {
        var constraint = new UnitConstraintEntry
        {
            Type = "ratio", UnitIds = ["ungor-archers"], Max = 1, PerUnit = true,
            Requires = ["ungor-herd"],
            Raw = "You may not have more units of Ungor Archers than you have units of Ungor Herds.",
        };
        var lord = MakeEntry("Lord", UnitCategory.Characters, 200, 1);
        lord.Loadout.IsGeneral = true;
        var list = MakeList(2000, lord,
            MakeEntry("Ungor Herd", UnitCategory.Core, 5, 20),
            MakeEntry("Ungor Archers", UnitCategory.Core, 6, 20),
            MakeEntry("Ungor Archers", UnitCategory.Core, 6, 20),
            MakeEntry("Warriors", UnitCategory.Core, 10, 40));

        Assert.Contains(V.Validate(list, WithConstraints(constraint)), i => i.Code == "RATIO_EXCEEDED");

        list.Units.Add(MakeEntry("Ungor Herd", UnitCategory.Core, 5, 20));
        Assert.DoesNotContain(V.Validate(list, WithConstraints(constraint)), i => i.Code == "RATIO_EXCEEDED");
    }

    [Fact]
    public void AUnitThatMayNeverBeGeneral_FlagsErrorWhenItIs()
    {
        var constraint = new UnitConstraintEntry
        {
            Type = "general", UnitIds = ["warhoofs"], MayBeGeneral = false,
            Raw = "A Warhoof may never be the Army General.",
        };
        var warhoof = MakeEntry("Warhoofs", UnitCategory.Characters, 100);
        warhoof.Loadout.IsGeneral = true;
        var list = MakeList(2000, warhoof, MakeEntry("Warriors", UnitCategory.Core, 10, 60));

        Assert.Contains(V.Validate(list, WithConstraints(constraint)), i => i.Code == "MAY_NOT_BE_GENERAL");
    }

    [Fact]
    public void AUnitThatMustBeGeneral_FlagsErrorWhenAnotherIs()
    {
        var constraint = new UnitConstraintEntry
        {
            Type = "general", UnitIds = ["karl-franz"], MustBeGeneral = true,
            Raw = "Karl Franz must be the Army General.",
        };
        var karl = MakeEntry("Karl Franz", UnitCategory.NamedCharacters, 400, named: true);
        var captain = MakeEntry("Captain", UnitCategory.Characters, 60);
        captain.Loadout.IsGeneral = true;
        var list = MakeList(3000, karl, captain, MakeEntry("Warriors", UnitCategory.Core, 10, 80));

        Assert.Contains(V.Validate(list, WithConstraints(constraint)), i => i.Code == "MUST_BE_GENERAL");

        captain.Loadout.IsGeneral = false;
        karl.Loadout.IsGeneral = true;
        Assert.DoesNotContain(V.Validate(list, WithConstraints(constraint)), i => i.Code == "MUST_BE_GENERAL");
    }

    // ─── Duplicate limits ───────────────────────────────────────────

    [Fact]
    public void SpecialDuplicates_AboveBracket_FlagsError()
    {
        // At 1500 target, Special cap = 2. Three copies of the same Special triggers error.
        var list = MakeList(1500,
            MakeEntry("Lord", UnitCategory.Characters, 150, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 50),
            MakeEntry("Hammerers", UnitCategory.Special, 12, 10),
            MakeEntry("Hammerers", UnitCategory.Special, 12, 10),
            MakeEntry("Hammerers", UnitCategory.Special, 12, 10));

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("DUPLICATE_LIMIT_EXCEEDED", codes);
    }

    [Fact]
    public void RareDuplicates_AboveBracket_FlagsError()
    {
        // At 1000 target, Rare cap = 1. Two copies triggers error.
        var list = MakeList(1000,
            MakeEntry("Lord", UnitCategory.Characters, 150, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 40),
            MakeEntry("Cannon", UnitCategory.Rare, 100, 1),
            MakeEntry("Cannon", UnitCategory.Rare, 100, 1));

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("DUPLICATE_LIMIT_EXCEEDED", codes);
    }

    [Fact]
    public void SpecialDuplicates_DifferentNames_AllowedBeyondCap()
    {
        // Many different Specials is fine — cap is per entry, not total count.
        var list = MakeList(1000,
            MakeEntry("Lord", UnitCategory.Characters, 150, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 30),
            MakeEntry("Hammerers", UnitCategory.Special, 100, 1),
            MakeEntry("Ironbreakers", UnitCategory.Special, 100, 1),
            MakeEntry("Miners", UnitCategory.Special, 100, 1));

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.DoesNotContain("DUPLICATE_LIMIT_EXCEEDED", codes);
    }

    [Fact]
    public void TwoOfAnEntry_ThatFillOneChoice_CountOnce()
    {
        // "You may take 1-2 Bolt Throwers as a single Rare choice."
        var perSlot = new UnitConstraintEntry
        {
            Type = "perSlot", UnitIds = ["bolt-thrower"], PerSlot = new PerSlotEntry { Min = 1, Max = 2 },
            Section = "rare", Raw = "You may take 1-2 Bolt Throwers as a single Rare choice.",
        };
        var lord = MakeEntry("Lord", UnitCategory.Characters, 150, 1);
        lord.Loadout.IsGeneral = true;
        var list = MakeList(1000, lord,
            MakeEntry("Warriors", UnitCategory.Core, 10, 40),
            MakeEntry("Bolt Thrower", UnitCategory.Rare, 70, 1),
            MakeEntry("Bolt Thrower", UnitCategory.Rare, 70, 1));

        Assert.DoesNotContain(V.Validate(list, WithConstraints(perSlot)), i => i.Code == "DUPLICATE_LIMIT_EXCEEDED");

        list.Units.Add(MakeEntry("Bolt Thrower", UnitCategory.Rare, 70, 1));
        Assert.Contains(V.Validate(list, WithConstraints(perSlot)), i => i.Code == "DUPLICATE_LIMIT_EXCEEDED");
    }

    // ─── Magic items ────────────────────────────────────────────────

    private static UnitConfiguration MakeCharacter(string name, int? allowance, int? bannerCap = null,
        List<string>? itemTypes = null, List<string>? families = null)
    {
        var options = new List<OptionGroup>();
        if (allowance is int limit)
            options.Add(TestHelpers.MagicItems(limit, itemTypes: itemTypes, families: families));
        if (bannerCap is int cap)
            options.Add(TestHelpers.Command(CommandRole.StandardBearer, 0, bannerCap: cap));

        var unit = TestHelpers.MakeUnit(name: name, points: 100, options: options,
            category: UnitCategory.Characters, troopType: "Infantry (Character)");
        return new UnitConfiguration(unit) { ModelCount = 1 };
    }

    private static void GiveItem(UnitConfiguration unit, string name, string category, double points) =>
        unit.SelectedMagicItems.Add(new SelectedMagicItem
        {
            Name = name,
            Category = category,
            PointsCost = points,
        });

    /// <summary>A list whose only interesting property is the character passed in.</summary>
    private static ArmyList ListWith(params UnitConfiguration[] characters)
    {
        characters[0].Loadout.IsGeneral = true;
        var units = new List<UnitConfiguration>(characters)
        {
            // 500 pts: meets the 25% core minimum and sits exactly on the single-unit cap.
            MakeEntry("Warriors", UnitCategory.Core, 10, 50),
        };
        return MakeList(2000, [.. units]);
    }

    [Fact]
    public void MagicItems_WithinAllowance_NoIssue()
    {
        var captain = MakeCharacter("Captain", allowance: 50);
        GiveItem(captain, "Sword of Might", MagicItemCategories.Weapon, 20);
        GiveItem(captain, "Talisman of Protection", MagicItemCategories.Talisman, 25);

        Assert.Empty(V.Validate(ListWith(captain)));
    }

    [Fact]
    public void MagicItems_OverAllowance_FlagsError()
    {
        var captain = MakeCharacter("Captain", allowance: 50);
        GiveItem(captain, "Giant Blade", MagicItemCategories.Weapon, 45);
        GiveItem(captain, "Talisman of Protection", MagicItemCategories.Talisman, 25);

        var issue = Assert.Single(V.Validate(ListWith(captain)), i => i.Code == "MAGIC_ITEMS_OVER_BUDGET");

        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Contains("70", issue.Message);
        Assert.Contains("50", issue.Message);
    }

    [Fact]
    public void MagicItems_OnAnEntryWithNoAllowance_FlagsError()
    {
        var champion = MakeCharacter("Champion", allowance: null);
        GiveItem(champion, "Sword of Might", MagicItemCategories.Weapon, 20);

        var codes = V.Validate(ListWith(champion)).Select(i => i.Code).ToList();

        Assert.Contains("MAGIC_ITEMS_NOT_ALLOWED", codes);
    }

    [Fact]
    public void MagicItems_FromAListTheAllowanceDoesNotAdmit_FlagsError()
    {
        // "May take Talismans up to 50 points" admits no magic weapon.
        var priest = MakeCharacter("Priest", allowance: 50, itemTypes: [MagicItemCategories.Talisman]);
        GiveItem(priest, "Sword of Might", MagicItemCategories.Weapon, 20);

        var issue = Assert.Single(V.Validate(ListWith(priest)), i => i.Code == "MAGIC_ITEM_TYPE_NOT_ALLOWED");
        Assert.Contains("Sword of Might", issue.Message);
    }

    [Fact]
    public void FamilyPicks_PaidForByTheAllowance_CountAgainstIt()
    {
        var paladin = MakeCharacter("Paladin", allowance: 50, families: ["virtues"]);
        GiveItem(paladin, "Sword of Might", MagicItemCategories.Weapon, 20);
        paladin.Loadout.SelectedFactionUpgrades["virtues"] =
            [new SelectedFactionUpgrade("virtue-of-valour", "Virtue of Valour", 40)];

        var issue = Assert.Single(V.Validate(ListWith(paladin)), i => i.Code == "MAGIC_ITEMS_OVER_BUDGET");
        Assert.Contains("60", issue.Message);
    }

    [Fact]
    public void TwoItemsFromOneCategory_FlagsError()
    {
        var captain = MakeCharacter("Captain", allowance: 100);
        GiveItem(captain, "Sword of Might", MagicItemCategories.Weapon, 20);
        GiveItem(captain, "Sword of Striking", MagicItemCategories.Weapon, 30);

        var issue = Assert.Single(V.Validate(ListWith(captain)),
            i => i.Code == "MAGIC_ITEM_CATEGORY_REPEATED");

        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Contains("Sword of Might", issue.Message);
        Assert.Contains("Sword of Striking", issue.Message);
    }

    [Fact]
    public void ItemsFromDifferentCategories_AreFine()
    {
        var captain = MakeCharacter("Captain", allowance: 100);
        GiveItem(captain, "Sword of Might", MagicItemCategories.Weapon, 20);
        GiveItem(captain, "Enchanted Shield", MagicItemCategories.Armour, 10);
        GiveItem(captain, "Talisman of Protection", MagicItemCategories.Talisman, 25);
        GiveItem(captain, "Dispel Scroll", MagicItemCategories.Arcane, 25);

        Assert.Empty(V.Validate(ListWith(captain)));
    }

    [Fact]
    public void SameItemOnTwoCharacters_Warns()
    {
        var captain = MakeCharacter("Captain", allowance: 50);
        GiveItem(captain, "Sword of Might", MagicItemCategories.Weapon, 20);
        var wizard = MakeCharacter("Battle Wizard", allowance: 50);
        GiveItem(wizard, "Sword of Might", MagicItemCategories.Weapon, 20);

        var issue = Assert.Single(V.Validate(ListWith(captain, wizard)),
            i => i.Code == "DUPLICATE_MAGIC_ITEM");

        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Contains("Sword of Might", issue.Message);
    }

    [Fact]
    public void CustomNamedCharacter_IsIdentifiedByItsOwnName()
    {
        var captain = MakeCharacter("Captain", allowance: 50);
        captain.Loadout.CustomName = "Aldric the Bold";
        GiveItem(captain, "Giant Blade", MagicItemCategories.Weapon, 45);
        GiveItem(captain, "Talisman of Protection", MagicItemCategories.Talisman, 25);

        var issue = Assert.Single(V.Validate(ListWith(captain)), i => i.Code == "MAGIC_ITEMS_OVER_BUDGET");

        Assert.Contains("Aldric the Bold", issue.Message);
        Assert.Contains("Captain", issue.Message);
    }

    [Fact]
    public void AnUnknownCategory_IsStillHeldToOnePerModel()
    {
        // An imported list can carry any category string; everything answers to the limit.
        var captain = MakeCharacter("Captain", allowance: 100);
        GiveItem(captain, "Sword of Might", "magic_weapons", 20);
        GiveItem(captain, "Sword of Striking", "magic_weapons", 30);

        var issue = Assert.Single(V.Validate(ListWith(captain)),
            i => i.Code == "MAGIC_ITEM_CATEGORY_REPEATED");

        Assert.Equal(ValidationSeverity.Error, issue.Severity);
    }

    // ─── Dwarf runes ─────────────────────────────────────────────
    // Runes are an upgrade family, "runic-items", not items: up to three go on one item and
    // combine freely, but no master rune may be used more than once per army.

    private static UnitConfiguration MakeRunesmith(string name, int runeBudget)
    {
        var unit = TestHelpers.MakeUnit(name: name, points: 100,
            options: [TestHelpers.FactionUpgrade("runic-items", pointsLimit: runeBudget)],
            category: UnitCategory.Characters, troopType: "Infantry (Character)");
        return new UnitConfiguration(unit) { ModelCount = 1 };
    }

    private static void GiveRune(UnitConfiguration unit, string name, double points)
    {
        if (!unit.Loadout.SelectedFactionUpgrades.TryGetValue("runic-items", out var picks))
            unit.Loadout.SelectedFactionUpgrades["runic-items"] = picks = [];
        picks.Add(new SelectedFactionUpgrade(TestHelpers.Slug(name), name, points));
    }

    [Fact]
    public void ThreeRunesOfOneKind_IsLegal()
    {
        var lord = MakeRunesmith("Dwarf Lord", 100);
        GiveRune(lord, "Rune of Stone", 5);
        GiveRune(lord, "Rune of Iron", 10);
        GiveRune(lord, "Rune of Preservation", 15);

        Assert.Empty(V.Validate(ListWith(lord)));
    }

    [Fact]
    public void RunesOverTheFamilysBudget_FlagsError()
    {
        var lord = MakeRunesmith("Dwarf Lord", 50);
        GiveRune(lord, "Master Rune of Smiting", 35);
        GiveRune(lord, "Rune of Cleaving", 25);

        var issue = Assert.Single(V.Validate(ListWith(lord)), i => i.Code == "FACTION_UPGRADE_OVER_BUDGET");
        Assert.Contains("60", issue.Message);
    }

    [Fact]
    public void SameMasterRune_TwiceInTheArmy_FlagsError()
    {
        var lord = MakeRunesmith("Dwarf Lord", 100);
        GiveRune(lord, "Master Rune of Smiting", 35);
        var thane = MakeRunesmith("Thane", 100);
        GiveRune(thane, "Master Rune of Smiting", 35);

        var issue = Assert.Single(V.Validate(ListWith(lord, thane)),
            i => i.Code == "MASTER_RUNE_REPEATED");

        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Contains("Master Rune of Smiting", issue.Message);
    }

    [Fact]
    public void SameOrdinaryRune_TwiceInTheArmy_IsFine()
    {
        var lord = MakeRunesmith("Dwarf Lord", 100);
        GiveRune(lord, "Rune of Cleaving", 25);
        var thane = MakeRunesmith("Thane", 100);
        GiveRune(thane, "Rune of Cleaving", 25);

        Assert.Empty(V.Validate(ListWith(lord, thane)));
    }

    [Fact]
    public void AFamilyTheEntryIsNotOffered_FlagsError()
    {
        var captain = MakeCharacter("Captain", allowance: 50);
        captain.Loadout.SelectedFactionUpgrades["knightly-orders"] =
            [new SelectedFactionUpgrade("reiksguard-knights", "Reiksguard Knights", 0)];

        Assert.Contains(V.Validate(ListWith(captain)), i => i.Code == "FACTION_UPGRADE_NOT_ALLOWED");
    }

    // ─── Banners ─────────────────────────────────────────────────

    [Fact]
    public void Banner_OverItsAllowance_FlagsError()
    {
        var captain = MakeCharacter("Captain", allowance: 50, bannerCap: 25);
        captain.HasStandardBearer = true;
        captain.Loadout.SelectedBanner = new SelectedMagicItem
        {
            Name = "Banner of Swiftness",
            Category = MagicItemCategories.Standard,
            PointsCost = 50,
        };

        var issue = Assert.Single(V.Validate(ListWith(captain)), i => i.Code == "BANNER_OVER_BUDGET");

        Assert.Equal(ValidationSeverity.Error, issue.Severity);
    }

    [Fact]
    public void Banner_OnAnEntryThatCannotCarryOne_FlagsError()
    {
        var captain = MakeCharacter("Captain", allowance: 50);
        captain.Loadout.SelectedBanner = new SelectedMagicItem
        {
            Name = "Banner of Swiftness",
            Category = MagicItemCategories.Standard,
            PointsCost = 25,
        };

        var codes = V.Validate(ListWith(captain)).Select(i => i.Code).ToList();

        Assert.Contains("BANNER_NOT_ALLOWED", codes);
    }

    [Fact]
    public void Banner_WithinItsAllowance_NoIssue()
    {
        var captain = MakeCharacter("Captain", allowance: 50, bannerCap: 50);
        captain.HasStandardBearer = true;
        captain.Loadout.SelectedBanner = new SelectedMagicItem
        {
            Name = "Banner of Swiftness",
            Category = MagicItemCategories.Standard,
            PointsCost = 50,
        };

        Assert.Empty(V.Validate(ListWith(captain)));
    }

    // Simple wrapper so tests aren't littered with `new ArmyListValidator()`.
    private class ArmyListValidatorInvoker
    {
        private readonly Warhammer.Services.ArmyListValidator _inner = new();

        public List<ValidationIssue> Validate(ArmyList l) => _inner.Validate(l, TestCompositionRules.War);
        public List<ValidationIssue> Validate(ArmyList l, CompositionRules rules) => _inner.Validate(l, rules);
    }
}

/// <summary>
/// Rules mirroring the shipped bundle's composition block, so validator tests don't need an
/// HTTP round trip. Kept in step with the bundle by <see cref="CompositionDataTests"/>.
/// </summary>
internal static class TestCompositionRules
{
    private static DuplicateRule Chart(int[] maxes) => new()
    {
        Bands = maxes.Select((max, i) => new DuplicateBand(i * 1000, i * 1000 + 999, max)).ToList(),
        BeyondEveryPoints = 1000,
        BeyondAdd = 1,
    };

    public static readonly CompositionRules War = new()
    {
        CharactersCombinedMaxPercent = 0.35,
        SingleUnitMaxPercent = 0.25,
        CoreMinPercent = 0.25,
        SpecialMaxPercent = 0.50,
        RareMaxPercent = 0.25,
        GeneralRequired = true,
        BattleStandardMax = 1,
        SpecialCharactersMaxEach = 1,
        SpecialDuplicates = Chart([1, 2, 3, 4, 5]),
        RareDuplicates = Chart([1, 1, 1, 2, 3]),
    };

    /// <summary>A book that splits its characters into Lords and Heroes, with a cap on each.</summary>
    public static readonly CompositionRules Split = War with
    {
        LordsMaxPercent = 0.25,
        HeroesMaxPercent = 0.25,
    };
}
