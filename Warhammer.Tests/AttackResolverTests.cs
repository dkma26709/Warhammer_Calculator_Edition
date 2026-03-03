using System.Text.Json;
using Warhammer.Calculator;
using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Tests;

public class AttackResolverTests
{
    private static readonly CoreRulesData CoreData;

    static AttackResolverTests()
    {
        var dataDir = Path.Combine(FindProjectRoot(), "wwwroot", "data");
        var json = File.ReadAllText(Path.Combine(dataDir, "core-rules.json"));
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        CoreData = JsonSerializer.Deserialize<CoreRulesData>(json, options)!;
    }

    private static string FindProjectRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "Warhammer.csproj")))
                return dir;
            var sibling = Path.Combine(Path.GetDirectoryName(dir)!, "Warhammer");
            if (Directory.Exists(sibling) && File.Exists(Path.Combine(sibling, "Warhammer.csproj")))
                return sibling;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not find Warhammer project root");
    }

    private static CoreRulesService CreateCoreRules()
    {
        var service = new CoreRulesService(null!);
        var dataField = typeof(CoreRulesService).GetField("_data", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var loadedField = typeof(CoreRulesService).GetField("_loaded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        dataField.SetValue(service, CoreData);
        loadedField.SetValue(service, true);
        return service;
    }

    private static UnitEntry MakeUnit(
        string troopType = "Infantry",
        int ws = 4, int s = 3, int t = 3, int w = 1, int i = 3, int a = 1, int ld = 7,
        double? points = 8,
        string? equipment = "Hand weapon",
        string? specialRules = null,
        string? unitSize = "10+")
    {
        return new UnitEntry
        {
            Name = "Test Unit",
            TroopType = troopType,
            Profiles = [new ProfileEntry { Name = "Test", WS = ws, S = s, T = t, W = w, I = i, A = a, Ld = ld, Points = points }],
            Equipment = equipment,
            SpecialRules = specialRules,
            UnitSize = unitSize
        };
    }

    [Fact]
    public void ResolveAttacks_BasicAttacks_ProducesHitsAndWounds()
    {
        var coreRules = CreateCoreRules();
        var resolver = new AttackResolver(coreRules);
        var rng = new Random(42);

        var attacker = new UnitConfiguration(MakeUnit(ws: 4, s: 3, a: 1)) { ModelCount = 10, Frontage = 5 };
        var defender = new UnitConfiguration(MakeUnit(ws: 4, t: 3)) { ModelCount = 10, Frontage = 5 };

        var weapon = coreRules.GetHandWeapon();
        var context = new CombatContext { CombatRound = 1 };

        var result = resolver.ResolveAttacks(attacker, defender, weapon, context, false, rng);

        // With 10 attacks, WS4 vs WS4 (need 4+), S3 vs T3 (need 4+), we should get some hits/wounds
        Assert.True(result.TotalHits > 0, "Should have at least 1 hit");
        Assert.True(result.UnsavedWounds >= 0);
    }

    [Fact]
    public void ResolveAttacks_KillingBlow_VsInfantry_IgnoresArmour()
    {
        var coreRules = CreateCoreRules();
        var resolver = new AttackResolver(coreRules);

        // Run many iterations to get KB triggers
        int kbWounds = 0;
        for (int seed = 0; seed < 100; seed++)
        {
            var rng = new Random(seed);
            var attacker = new UnitConfiguration(MakeUnit(ws: 4, s: 4, a: 2, specialRules: "Killing Blow"))
                { ModelCount = 10, Frontage = 5 };
            var defender = new UnitConfiguration(MakeUnit(ws: 3, t: 3, w: 1, equipment: "Hand weapon, heavy armour, shield"))
                { ModelCount = 20, Frontage = 5 };

            var weapon = coreRules.GetHandWeapon();
            var context = new CombatContext { CombatRound = 1 };
            var result = resolver.ResolveAttacks(attacker, defender, weapon, context, false, rng);
            kbWounds += result.KillingBlowWounds;
        }

        // Over 100 iterations with 10 attacks each, we should get some KB wounds
        Assert.True(kbWounds > 0, "Should have triggered at least one Killing Blow");
    }

    [Fact]
    public void ResolveAttacks_KillingBlow_VsMonster_NoKB()
    {
        var coreRules = CreateCoreRules();
        var resolver = new AttackResolver(coreRules);

        int kbWounds = 0;
        for (int seed = 0; seed < 50; seed++)
        {
            var rng = new Random(seed);
            var attacker = new UnitConfiguration(MakeUnit(ws: 4, s: 4, a: 3, specialRules: "Killing Blow"))
                { ModelCount = 5, Frontage = 5 };
            var defender = new UnitConfiguration(MakeUnit(troopType: "Monster", ws: 3, t: 5, w: 4))
                { ModelCount = 1, Frontage = 1 };

            var weapon = coreRules.GetHandWeapon();
            var context = new CombatContext { CombatRound = 1 };
            var result = resolver.ResolveAttacks(attacker, defender, weapon, context, false, rng);
            kbWounds += result.KillingBlowWounds;
        }

        // KB doesn't work vs Monster
        Assert.Equal(0, kbWounds);
    }

    [Fact]
    public void ResolveAttacks_WardSave_ReducesWounds()
    {
        var coreRules = CreateCoreRules();
        var resolver = new AttackResolver(coreRules);

        int woundsWithWard = 0;
        int woundsWithout = 0;

        for (int seed = 0; seed < 50; seed++)
        {
            var rng1 = new Random(seed);
            var rng2 = new Random(seed);

            var attacker1 = new UnitConfiguration(MakeUnit(ws: 4, s: 4, a: 2)) { ModelCount = 10, Frontage = 5 };
            var defenderNoWard = new UnitConfiguration(MakeUnit(ws: 3, t: 3)) { ModelCount = 20, Frontage = 5 };

            var attacker2 = new UnitConfiguration(MakeUnit(ws: 4, s: 4, a: 2)) { ModelCount = 10, Frontage = 5 };
            var defenderWithWard = new UnitConfiguration(MakeUnit(ws: 3, t: 3, specialRules: "Ward save (4+)"))
                { ModelCount = 20, Frontage = 5 };

            var weapon = coreRules.GetHandWeapon();
            var context = new CombatContext { CombatRound = 1 };

            woundsWithout += resolver.ResolveAttacks(attacker1, defenderNoWard, weapon, context, false, rng1).UnsavedWounds;
            woundsWithWard += resolver.ResolveAttacks(attacker2, defenderWithWard, weapon, context, false, rng2).UnsavedWounds;
        }

        // Ward save should reduce total wounds
        Assert.True(woundsWithWard < woundsWithout, $"Ward save should reduce wounds: with={woundsWithWard}, without={woundsWithout}");
    }

    [Fact]
    public void ResolveAttacks_FlamingNegatesRegen()
    {
        var coreRules = CreateCoreRules();
        var resolver = new AttackResolver(coreRules);

        int woundsWithFlaming = 0;
        int woundsWithoutFlaming = 0;

        for (int seed = 0; seed < 50; seed++)
        {
            var rng1 = new Random(seed);
            var rng2 = new Random(seed);

            var attackerFlaming = new UnitConfiguration(MakeUnit(ws: 4, s: 4, a: 2, specialRules: "Flaming Attacks"))
                { ModelCount = 10, Frontage = 5 };
            var attackerNormal = new UnitConfiguration(MakeUnit(ws: 4, s: 4, a: 2))
                { ModelCount = 10, Frontage = 5 };
            var defenderRegen = new UnitConfiguration(MakeUnit(ws: 3, t: 3, specialRules: "Regeneration"))
                { ModelCount = 20, Frontage = 5 };
            var defenderRegen2 = new UnitConfiguration(MakeUnit(ws: 3, t: 3, specialRules: "Regeneration"))
                { ModelCount = 20, Frontage = 5 };

            var weapon = coreRules.GetHandWeapon();
            var context = new CombatContext { CombatRound = 1 };

            woundsWithFlaming += resolver.ResolveAttacks(attackerFlaming, defenderRegen, weapon, context, false, rng1).UnsavedWounds;
            woundsWithoutFlaming += resolver.ResolveAttacks(attackerNormal, defenderRegen2, weapon, context, false, rng2).UnsavedWounds;
        }

        // Flaming negates regen, so flaming should cause more wounds
        Assert.True(woundsWithFlaming > woundsWithoutFlaming,
            $"Flaming should negate regen: flaming={woundsWithFlaming}, normal={woundsWithoutFlaming}");
    }

    [Fact]
    public void ResolveAttacks_Hatred_FirstRoundReroll()
    {
        var coreRules = CreateCoreRules();
        var resolver = new AttackResolver(coreRules);

        int hitsWithHatred = 0;
        int hitsWithout = 0;

        for (int seed = 0; seed < 50; seed++)
        {
            var rng1 = new Random(seed);
            var rng2 = new Random(seed);

            var attackerHatred = new UnitConfiguration(MakeUnit(ws: 3, s: 3, a: 1, specialRules: "Hatred"))
                { ModelCount = 10, Frontage = 5 };
            var attackerNormal = new UnitConfiguration(MakeUnit(ws: 3, s: 3, a: 1))
                { ModelCount = 10, Frontage = 5 };
            var defender = new UnitConfiguration(MakeUnit(ws: 4, t: 3)) { ModelCount = 20, Frontage = 5 };
            var defender2 = new UnitConfiguration(MakeUnit(ws: 4, t: 3)) { ModelCount = 20, Frontage = 5 };

            var weapon = coreRules.GetHandWeapon();
            var context = new CombatContext { CombatRound = 1 };

            hitsWithHatred += resolver.ResolveAttacks(attackerHatred, defender, weapon, context, false, rng1).TotalHits;
            hitsWithout += resolver.ResolveAttacks(attackerNormal, defender2, weapon, context, false, rng2).TotalHits;
        }

        Assert.True(hitsWithHatred > hitsWithout,
            $"Hatred should increase hits: hatred={hitsWithHatred}, normal={hitsWithout}");
    }

    [Fact]
    public void ResolveAttacks_ArmourSaveUsesCorRulesData()
    {
        var coreRules = CreateCoreRules();
        var resolver = new AttackResolver(coreRules);

        int woundsWithArmour = 0;
        int woundsWithout = 0;

        for (int seed = 0; seed < 50; seed++)
        {
            var rng1 = new Random(seed);
            var rng2 = new Random(seed);

            var attacker1 = new UnitConfiguration(MakeUnit(ws: 4, s: 3, a: 2)) { ModelCount = 10, Frontage = 5 };
            var attacker2 = new UnitConfiguration(MakeUnit(ws: 4, s: 3, a: 2)) { ModelCount = 10, Frontage = 5 };

            var defenderArmoured = new UnitConfiguration(MakeUnit(ws: 3, t: 3, equipment: "Hand weapon, heavy armour, shield"))
                { ModelCount = 20, Frontage = 5 };
            var defenderNaked = new UnitConfiguration(MakeUnit(ws: 3, t: 3))
                { ModelCount = 20, Frontage = 5 };

            var weapon = coreRules.GetHandWeapon();
            var context = new CombatContext { CombatRound = 1 };

            woundsWithArmour += resolver.ResolveAttacks(attacker1, defenderArmoured, weapon, context, false, rng1).UnsavedWounds;
            woundsWithout += resolver.ResolveAttacks(attacker2, defenderNaked, weapon, context, false, rng2).UnsavedWounds;
        }

        Assert.True(woundsWithArmour < woundsWithout,
            $"Armour should reduce wounds: armoured={woundsWithArmour}, naked={woundsWithout}");
    }

    [Fact]
    public void ResolveImpactHits_OnCharge_DealsDamage()
    {
        var coreRules = CreateCoreRules();
        var resolver = new AttackResolver(coreRules);

        int totalWounds = 0;
        for (int seed = 0; seed < 50; seed++)
        {
            var rng = new Random(seed);
            var charger = new UnitConfiguration(MakeUnit(troopType: "Cavalry", s: 3, specialRules: "Impact Hits (1)"))
                { ModelCount = 5, Frontage = 5 };
            var target = new UnitConfiguration(MakeUnit(ws: 3, t: 3)) { ModelCount = 10, Frontage = 5 };
            var context = new CombatContext { Charge = ChargeState.AttackerCharged, CombatRound = 1 };

            totalWounds += resolver.ResolveImpactHits(charger, target, context, rng);
        }

        Assert.True(totalWounds > 0, "Impact hits should cause some wounds");
    }
}
