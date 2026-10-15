using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// The printable roster: what a player carries to the table. These assert the sheet says what
/// the list says — nothing dropped, nothing invented, and no arithmetic left to do mid-game.
/// </summary>
public class ArmyRosterBuilderTests
{
    private static ArmyRosterBuilder MakeBuilder(string systemKey = GameSystems.Default)
    {
        var system = new GameSystemService();
        system.Initialize(systemKey);
        return new ArmyRosterBuilder(TestHelpers.RepoFor(systemKey), system);
    }

    private static ArmyList MakeList(params UnitConfiguration[] units)
    {
        var list = new ArmyList { Faction = FactionKey.TheEmpire, PointsTarget = 2000, Name = "Test Army" };
        list.Units.AddRange(units);
        return list;
    }

    private static UnitConfiguration Configure(Unit unit, Action<UnitConfiguration>? setup = null)
    {
        var cfg = new UnitConfiguration(unit);
        setup?.Invoke(cfg);
        return cfg;
    }

    private static Unit InCategory(UnitCategory category, string name, string troopType = "Infantry") =>
        TestHelpers.MakeUnit(name: name, troopType: troopType) with { Category = category };

    private static RosterEntry Only(RosterDocument document) =>
        Assert.Single(Assert.Single(document.Sections).Entries);

    // ─── Sections ────────────────────────────────────────────────

    [Fact]
    public void Sections_AppearInRosterOrder()
    {
        var list = MakeList(
            Configure(InCategory(UnitCategory.Rare, "Rare Beast")),
            Configure(InCategory(UnitCategory.Core, "Spearmen")),
            Configure(InCategory(UnitCategory.Lords, "General", "Infantry (Character)")),
            Configure(InCategory(UnitCategory.Special, "Knights")));

        var titles = MakeBuilder().Build(list).Sections.Select(s => s.Title);

        Assert.Equal(["Characters", "Core", "Special", "Rare"], titles);
    }

    [Fact]
    public void Empty_SectionsAreLeftOffTheSheet()
    {
        var list = MakeList(Configure(InCategory(UnitCategory.Core, "Spearmen")));

        var section = Assert.Single(MakeBuilder().Build(list).Sections);

        Assert.Equal("Core", section.Title);
    }

    /// <summary>
    /// v2 splits characters into Lords and Heroes and v3 merges them into Characters; the books
    /// also keep a SPECIAL CHARACTERS section. On the table they are all just characters.
    /// </summary>
    [Theory]
    [InlineData(UnitCategory.Lords)]
    [InlineData(UnitCategory.Heroes)]
    [InlineData(UnitCategory.Characters)]
    [InlineData(UnitCategory.NamedCharacters)]
    public void Every_CharacterCategoryPrintsUnderOneHeading(UnitCategory category)
    {
        var list = MakeList(Configure(InCategory(category, "Captain", "Infantry (Character)")));

        var section = Assert.Single(MakeBuilder().Build(list).Sections);

        Assert.Equal("Characters", section.Title);
    }

    /// <summary>A category outside the four headings still has to reach the paper.</summary>
    [Fact]
    public void Units_OutsideTheFourHeadingsAreNotDropped()
    {
        var list = MakeList(Configure(InCategory(UnitCategory.CharacterMounts, "Pegasus", "Monster")));

        var section = Assert.Single(MakeBuilder().Build(list).Sections);

        Assert.Equal("Other", section.Title);
        Assert.Equal("Pegasus", Assert.Single(section.Entries).Name);
    }

    [Fact]
    public void Section_AndArmyTotalsAddUpTheEntries()
    {
        var spearmen = TestHelpers.MakeUnit(name: "Spearmen", points: 6) with { Category = UnitCategory.Core };
        var knights = TestHelpers.MakeUnit(name: "Knights", points: 20) with { Category = UnitCategory.Special };

        var list = MakeList(
            Configure(spearmen, c => c.ModelCount = 10),
            Configure(knights, c => c.ModelCount = 5));

        var document = MakeBuilder().Build(list);

        Assert.Equal(60, document.Sections.Single(s => s.Title == "Core").Points);
        Assert.Equal(100, document.Sections.Single(s => s.Title == "Special").Points);
        Assert.Equal(160, document.TotalPoints);
        Assert.Equal(2000, document.PointsTarget);
    }

    [Fact]
    public void Header_NamesTheArmyFactionAndSystem()
    {
        var document = MakeBuilder().Build(MakeList(Configure(InCategory(UnitCategory.Core, "Spearmen"))));

        Assert.Equal("Test Army", document.ArmyName);
        Assert.Equal("The Empire", document.FactionName);
        Assert.Equal("WAP v2", document.SystemName);
    }

    // ─── Entry identity ──────────────────────────────────────────

    [Fact]
    public void A_NamedCharacterLeadsWithItsNameAndKeepsTheBookName()
    {
        var list = MakeList(Configure(
            InCategory(UnitCategory.Heroes, "Captain", "Infantry (Character)"),
            c => c.Loadout.CustomName = "Aldric the Bold"));

        var entry = Only(MakeBuilder().Build(list));

        Assert.Equal("Aldric the Bold", entry.Name);
        Assert.Equal("Captain", entry.BookName);
    }

    [Fact]
    public void An_UnnamedEntryCarriesNoSecondName()
    {
        var entry = Only(MakeBuilder().Build(
            MakeList(Configure(InCategory(UnitCategory.Core, "Spearmen")))));

        Assert.Equal("Spearmen", entry.Name);
        Assert.Null(entry.BookName);
    }

    [Fact]
    public void General_AndBattleStandardAreCalledOut()
    {
        var list = MakeList(Configure(
            InCategory(UnitCategory.Lords, "General", "Infantry (Character)"),
            c =>
            {
                c.Loadout.IsGeneral = true;
                c.Loadout.IsBattleStandardBearer = true;
            }));

        Assert.Equal(["General", "Battle Standard Bearer"], Only(MakeBuilder().Build(list)).Roles);
    }

    [Fact]
    public void Model_LineStatesTheCountButNotTheFrontage()
    {
        var list = MakeList(Configure(
            InCategory(UnitCategory.Core, "Spearmen"),
            c => { c.ModelCount = 20; c.Frontage = 5; }));

        Assert.Equal("20 models", Only(MakeBuilder().Build(list)).ModelLine);
    }

    [Fact]
    public void A_SingleModelNeedsNoFrontage()
    {
        var list = MakeList(Configure(InCategory(UnitCategory.Heroes, "Captain", "Infantry (Character)")));

        Assert.Equal("1 model", Only(MakeBuilder().Build(list)).ModelLine);
    }

    // ─── Profiles ────────────────────────────────────────────────

    [Fact]
    public void Profile_PrintsTheNineStatsInOrder()
    {
        var unit = TestHelpers.MakeUnit(
            name: "Spearmen", m: 4, ws: 3, bs: 3, s: 3, t: 3, w: 1, i: 3, a: 1, ld: 7)
            with { Category = UnitCategory.Core };

        var profile = Assert.Single(Only(MakeBuilder().Build(MakeList(Configure(unit)))).Profiles);

        Assert.Equal("Spearmen", profile.Name);
        Assert.Equal(["4", "3", "3", "3", "3", "1", "3", "1", "7"], profile.Stats);
        Assert.Equal(9, RosterProfile.Headers.Count);
    }

    /// <summary>Stat profiles carry "*" and "-"; the sheet has to render them, not crash on them.</summary>
    [Fact]
    public void A_MissingStatPrintsAsAStar()
    {
        var chariot = TestHelpers.MakeUnit(name: "Chariot", troopType: "Chariot");
        var unit = chariot with
        {
            Category = UnitCategory.Special,
            BaseModel = chariot.BaseModel with
            {
                Stats = chariot.BaseModel.Stats with { M = null },
            },
        };

        var profile = Assert.Single(Only(MakeBuilder().Build(MakeList(Configure(unit)))).Profiles);

        Assert.Equal("*", profile.Stats[0]);
    }

    [Fact]
    public void A_MountGetsItsOwnLine()
    {
        var mount = new ProfileEntry { Name = "Warhorse", M = 8, WS = 3, BS = 0, S = 3, T = 3, W = 1, I = 3, A = 1, Ld = 5 };
        var unit = TestHelpers.MakeUnit(
            name: "Knight", troopType: "Cavalry (Character)", mountProfile: mount)
            with { Category = UnitCategory.Heroes };

        var profiles = Only(MakeBuilder().Build(MakeList(Configure(unit)))).Profiles;

        Assert.Equal(2, profiles.Count);
        Assert.Equal("Warhorse", profiles[1].Name);
        Assert.Equal("8", profiles[1].Stats[0]);
    }

    // ─── Saves ───────────────────────────────────────────────────

    [Fact]
    public void Armour_SaveIsWorkedOutRatherThanLeftAsEquipment()
    {
        var unit = TestHelpers.MakeUnit(
            name: "Knights", equipment: "Hand weapon, Heavy armour, Shields")
            with { Category = UnitCategory.Special };

        var saves = Only(MakeBuilder().Build(MakeList(Configure(unit)))).Saves;

        // Heavy armour (+3) and a shield (+1) make a 3+.
        Assert.Contains("Armour 3+", saves);
    }

    [Fact]
    public void An_UnarmouredUnitSaysSo()
    {
        var unit = TestHelpers.MakeUnit(name: "Peasants", equipment: "Hand weapon")
            with { Category = UnitCategory.Core };

        Assert.Contains("Armour —", Only(MakeBuilder().Build(MakeList(Configure(unit)))).Saves);
    }

    [Fact]
    public void Ward_AndRegenerationSavesReachTheSheet()
    {
        var unit = TestHelpers.MakeUnit(
            name: "Troll", specialRules: "Ward Save (5+), Regeneration (4+)")
            with { Category = UnitCategory.Rare };

        var saves = Only(MakeBuilder().Build(MakeList(Configure(unit)))).Saves;

        Assert.Contains("Ward 5+", saves);
        Assert.Contains("Regeneration 4+", saves);
    }

    [Fact]
    public void Dodge_ReachesTheSheet()
    {
        // The engine folds Dodge into the ward roll, so omitting it hides a save the model has.
        var unit = TestHelpers.MakeUnit(name: "Assassin", specialRules: "Dodge (5+)")
            with { Category = UnitCategory.Heroes };

        Assert.Contains("Dodge 5+", Only(MakeBuilder().Build(MakeList(Configure(unit)))).Saves);
    }

    [Fact]
    public void A_ShieldsParryReachesTheSheet()
    {
        // Parry comes off the shield, not a special rule — core-rules.json gives shields
        // "Parry (6+)", and the engine takes it whenever the model fights with a hand weapon.
        var unit = TestHelpers.MakeUnit(name: "Spearmen", equipment: "Hand weapon, Shields")
            with { Category = UnitCategory.Core };

        var saves = Only(MakeBuilder().Build(MakeList(Configure(unit)))).Saves;

        Assert.Contains("Parry 6+", saves);
    }

    [Fact]
    public void The_PrintedArmourSaveIsUnmodifiedSoArmourPiercingStartsFromIt()
    {
        // Heavy armour (+3), a shield (+1) and Natural Armour (5+) (+2) come to a modifier of 6,
        // an unmodified 1+. The engine clamps the *modified* save at 2+, so clamping the printed
        // one would leave a player resolving AP(2) a pip adrift of what the simulator rolls.
        var unit = TestHelpers.MakeUnit(
            name: "Dragon", equipment: "Hand weapon, Heavy armour, Shields",
            specialRules: "Natural Armour (5+)")
            with { Category = UnitCategory.Rare };

        var saves = Only(MakeBuilder().Build(MakeList(Configure(unit)))).Saves;

        Assert.Contains("Armour 1+", saves);
    }

    // ─── Equipment, upgrades and items ───────────────────────────

    [Fact]
    public void Equipment_IsNamedInBookCaseNotShouted()
    {
        var unit = TestHelpers.MakeUnit(name: "Greatswords", equipment: "Great weapon, Heavy armour")
            with { Category = UnitCategory.Special };

        var equipment = Only(MakeBuilder().Build(MakeList(Configure(unit)))).Equipment;

        Assert.Contains("Great Weapon", equipment);
        Assert.Contains("Heavy Armour", equipment);
    }

    [Fact]
    public void The_WeaponTheUnitFightsWithIsListedFirst()
    {
        var unit = TestHelpers.MakeUnit(name: "Greatswords", equipment: "Hand weapon, Great weapon")
            with { Category = UnitCategory.Special };

        var equipment = Only(MakeBuilder().Build(MakeList(
            Configure(unit, c => c.SelectedWeapon = CloseCombatWeaponKey.GreatWeapon)))).Equipment;

        Assert.Equal("Great Weapon", equipment[0]);
        Assert.Contains("Hand Weapon", equipment);
    }

    /// <summary>
    /// Gear the equipment parser has no key for — a book's own invention — has nowhere else to
    /// appear, so it has to survive as the name the book gave it.
    /// </summary>
    [Fact]
    public void Equipment_TheParserCannotNormaliseStillPrints()
    {
        var options = new List<UnitOption>
        {
            new()
            {
                Type = "equipment",
                Action = "add",
                Choices = [new OptionChoice { Name = ["Throwing nets"], Points = 5 }],
            },
        };
        var unit = TestHelpers.MakeUnit(name: "Corsairs", options: options) with { Category = UnitCategory.Core };

        var equipment = Only(MakeBuilder().Build(MakeList(
            Configure(unit, c => c.SelectedUpgrades[0] = 0)))).Equipment;

        Assert.Contains("Throwing nets", equipment);
    }

    /// <summary>
    /// The engine simulates close combat only, so a missile weapon is parsed and then dropped —
    /// which left Handgunners on the sheet holding nothing but a hand weapon.
    /// </summary>
    [Fact]
    public void Missile_WeaponsReachTheSheetEvenThoughTheEngineIgnoresThem()
    {
        var unit = TestHelpers.MakeUnit(name: "Handgunners", equipment: "Hand weapon, handgun")
            with { Category = UnitCategory.Core };

        var equipment = Only(MakeBuilder().Build(MakeList(Configure(unit)))).Equipment;

        Assert.Equal(["Hand Weapon", "Handgun"], equipment);
    }

    [Fact]
    public void A_MissileWeaponPrintsUnderOneNameWhateverTheBookCalledIt()
    {
        var unit = TestHelpers.MakeUnit(name: "Pistoliers", equipment: "Brace of pistols, medium armour")
            with { Category = UnitCategory.Core };

        var equipment = Only(MakeBuilder().Build(MakeList(Configure(unit)))).Equipment;

        Assert.Contains("Pistol", equipment);
        Assert.Contains("Medium Armour", equipment);
    }

    /// <summary>Gear the book invented and the parser has no key for still has to print.</summary>
    [Fact]
    public void Unrecognised_BaseEquipmentKeepsTheBooksWording()
    {
        var unit = TestHelpers.MakeUnit(name: "Outriders", equipment: "Repeater handgun, medium armour")
            with { Category = UnitCategory.Special };

        Assert.Contains("Repeater handgun", Only(MakeBuilder().Build(MakeList(Configure(unit)))).Equipment);
    }

    [Fact]
    public void An_UpgradeThatReplacesAMissileWeaponRemovesIt()
    {
        var options = new List<UnitOption>
        {
            new()
            {
                Type = "equipment",
                Action = "replace",
                Replaces = "bow",
                Choices = [new OptionChoice { Name = ["crossbow"], Points = 2 }],
            },
        };
        var unit = TestHelpers.MakeUnit(name: "Archers", equipment: "Hand weapon, bow", options: options)
            with { Category = UnitCategory.Core };

        var equipment = Only(MakeBuilder().Build(MakeList(
            Configure(unit, c => c.SelectedUpgrades[0] = 0)))).Equipment;

        Assert.Contains("Crossbow", equipment);
        Assert.DoesNotContain("Bow", equipment);
    }

    [Fact]
    public void A_ReplaceWrittenInThePluralStillRemovesTheSingularItem()
    {
        // wap-v3 Shadow Warriors: the equipment line says "Elven shortbow", the option replaces
        // "Elven shortbows". Neither is in the parser's vocabulary, so the strings are all there
        // is to match on — and matching them exactly left both bows on the sheet.
        var options = new List<UnitOption>
        {
            new()
            {
                Type = "equipment",
                Action = "replace",
                Replaces = "Elven shortbows",
                Choices = [new OptionChoice { Name = ["Elven longbows"], Points = 1, PerModel = true }],
            },
        };
        var unit = TestHelpers.MakeUnit(
            name: "Shadow Warriors", equipment: "Hand weapon, Elven shortbow, light armour",
            options: options)
            with { Category = UnitCategory.Special };

        var equipment = Only(MakeBuilder().Build(MakeList(
            Configure(unit, c => c.SelectedUpgrades[0] = 0)))).Equipment;

        Assert.Contains("Elven longbows", equipment);
        Assert.DoesNotContain("Elven shortbow", equipment);
    }

    [Fact]
    public void A_MountUpgradeIsListedAsAnUpgrade()
    {
        var options = new List<UnitOption>
        {
            new()
            {
                Type = "mount",
                Choices = [new OptionChoice { Name = ["Barded Warhorse"], Points = 12 }],
            },
        };
        var unit = TestHelpers.MakeUnit(name: "Captain", troopType: "Infantry (Character)", options: options)
            with { Category = UnitCategory.Heroes };

        var upgrades = Only(MakeBuilder().Build(MakeList(
            Configure(unit, c => c.SelectedUpgrades[0] = 0)))).Upgrades;

        Assert.Contains("Barded Warhorse", upgrades);
    }

    [Fact]
    public void Faction_UpgradesAreListed()
    {
        var unit = InCategory(UnitCategory.Core, "Saurus Warriors");
        var list = MakeList(Configure(unit, c =>
            c.Loadout.SelectedFactionUpgrades["blessed_spawnings"] =
                [new SelectedFactionUpgrade("Spawning of Sotek", 2)]));

        Assert.Contains("Spawning of Sotek", Only(MakeBuilder().Build(list)).Upgrades);
    }

    [Fact]
    public void Magic_ItemsAndTheBannerPrintWithTheirCost()
    {
        var list = MakeList(Configure(
            InCategory(UnitCategory.Heroes, "Captain", "Infantry (Character)"),
            c =>
            {
                c.SelectedMagicItems.Add(new SelectedMagicItem
                {
                    Name = "Sword of Striking", Category = "magic_weapon", PointsCost = 30,
                });
                c.Loadout.SelectedBanner = new SelectedMagicItem
                {
                    Name = "War Banner", Category = "magic_standard", PointsCost = 25,
                };
            }));

        var items = Only(MakeBuilder().Build(list)).MagicItems;

        Assert.Equal(2, items.Count);
        Assert.Equal(new RosterItem("Sword of Striking", 30), items[0]);
        Assert.Equal(new RosterItem("War Banner", 25), items[1]);
    }

    // ─── Rules and notes ─────────────────────────────────────────

    [Fact]
    public void Special_RulesPrintByName()
    {
        var unit = TestHelpers.MakeUnit(name: "Witch Elves", specialRules: "Frenzy, Poisoned Attacks, Hatred")
            with { Category = UnitCategory.Special };

        var rules = Only(MakeBuilder().Build(MakeList(Configure(unit)))).SpecialRules;

        Assert.Equal(["Frenzy", "Poisoned Attacks", "Hatred"], rules);
    }

    /// <summary>A rule the book scopes to one model is still part of the entry's rules line.</summary>
    [Fact]
    public void A_ScopedRuleKeepsItsScopeOnTheSheet()
    {
        var unit = TestHelpers.MakeUnit(name: "Boar Boyz", specialRules: "Mighty Blow (2) (War Boars only)")
            with { Category = UnitCategory.Special };

        Assert.Contains("Mighty Blow (2) (War Boars only)",
            Only(MakeBuilder().Build(MakeList(Configure(unit)))).SpecialRules);
    }

    [Fact]
    public void A_RuleListedTwicePrintsOnce()
    {
        var unit = TestHelpers.MakeUnit(name: "Spearmen", specialRules: "Fear, Fear")
            with { Category = UnitCategory.Core };

        Assert.Equal(["Fear"], Only(MakeBuilder().Build(MakeList(Configure(unit)))).SpecialRules);
    }

    [Fact]
    public void The_CommandGroupIsNoted()
    {
        var list = MakeList(Configure(InCategory(UnitCategory.Core, "Spearmen"), c =>
        {
            c.HasChampion = true;
            c.HasMusician = true;
            c.HasStandardBearer = true;
        }));

        Assert.Contains("Command: Leader, Musician, Standard Bearer", Only(MakeBuilder().Build(list)).Notes);
    }

    [Fact]
    public void Wizard_LevelLoreAndSpellsAreNoted()
    {
        var options = new List<UnitOption> { new() { Type = "wizard_upgrade", Points = 35 } };
        var unit = TestHelpers.MakeUnit(name: "Wizard", troopType: "Infantry (Character)", options: options)
            with { Category = UnitCategory.Heroes };

        var notes = Only(MakeBuilder().Build(MakeList(Configure(unit, c =>
        {
            c.Loadout.WizardLevel = 2;
            c.Loadout.SelectedLore = "Lore of Fire";
            c.Loadout.SelectedSpells.AddRange(["Fireball", "Burning Head"]);
        })))).Notes;

        Assert.Contains("Wizard Level 2 · Lore of Fire", notes);
        Assert.Contains("Spells: Fireball, Burning Head", notes);
    }

    [Fact]
    public void A_FactionAbilityIsNotedInWords()
    {
        var list = MakeList(Configure(
            InCategory(UnitCategory.Special, "Shadow Dancer", "Infantry (Character)"),
            c => c.SelectedShadowDance = ShadowDance.StormOfBlades));

        Assert.Contains("Shadow Dance: Storm Of Blades", Only(MakeBuilder().Build(list)).Notes);
    }

    [Fact]
    public void A_ListWithNoUnitsStillProducesAHeader()
    {
        var document = MakeBuilder().Build(MakeList());

        Assert.Empty(document.Sections);
        Assert.Equal(0, document.TotalPoints);
        Assert.Equal("Test Army", document.ArmyName);
    }

    [Fact]
    public void An_UnnamedListGetsAPlaceholderTitle()
    {
        var list = MakeList() with { Name = "   " };

        Assert.Equal("Untitled List", MakeBuilder().Build(list).ArmyName);
    }
}
