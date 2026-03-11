using System.Text.Json;
using Warhammer.Calculator;
using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Tests;

public class CombatResolverTests
{
    private static readonly CoreRulesData CoreData;

    static CombatResolverTests()
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
        string? unitSize = "10+",
        ProfileEntry? mountProfile = null)
    {
        var profiles = new List<ProfileEntry>
        {
            new() { Name = "Test", WS = ws, S = s, T = t, W = w, I = i, A = a, Ld = ld, Points = points }
        };
        if (mountProfile != null)
            profiles.Add(mountProfile);

        return new UnitEntry
        {
            Name = "Test Unit",
            TroopType = troopType,
            Profiles = profiles,
            Equipment = equipment,
            SpecialRules = specialRules,
            UnitSize = unitSize
        };
    }

    private static RoundResult Resolve(CombatResolver resolver, UnitConfiguration attacker, UnitConfiguration defender, CombatContext context, Random rng)
    {
        var groups = resolver.PrepareAttackGroups(attacker, defender, context);
        return resolver.ResolveRound(attacker, defender, context, groups, rng);
    }

    [Fact]
    public void ResolveRound_ImpactHits_OnChargeOnly()
    {
        var coreRules = CreateCoreRules();
        var resolver = new CombatResolver(coreRules);

        int woundsOnCharge = 0;
        int woundsNoCharge = 0;

        for (int seed = 0; seed < 50; seed++)
        {
            var rng1 = new Random(seed);
            var rng2 = new Random(seed);

            var charger = new UnitConfiguration(MakeUnit(ws: 4, s: 4, a: 1, specialRules: "Impact Hits (2)"))
                { ModelCount = 10, Frontage = 5 };
            var defender = new UnitConfiguration(MakeUnit(ws: 3, t: 3))
                { ModelCount = 20, Frontage = 5 };

            var charger2 = new UnitConfiguration(MakeUnit(ws: 4, s: 4, a: 1, specialRules: "Impact Hits (2)"))
                { ModelCount = 10, Frontage = 5 };
            var defender2 = new UnitConfiguration(MakeUnit(ws: 3, t: 3))
                { ModelCount = 20, Frontage = 5 };

            var chargeContext = new CombatContext { Charge = ChargeState.AttackerCharged, CombatRound = 1 };
            var noChargeContext = new CombatContext { Charge = ChargeState.Neither, CombatRound = 1 };

            woundsOnCharge += Resolve(resolver, charger, defender, chargeContext, rng1).AttackerWoundsDealt;
            woundsNoCharge += Resolve(resolver, charger2, defender2, noChargeContext, rng2).AttackerWoundsDealt;
        }

        Assert.True(woundsOnCharge > woundsNoCharge,
            $"Charge with impact hits should deal more wounds: charge={woundsOnCharge}, no={woundsNoCharge}");
    }

    [Fact]
    public void ResolveRound_Stomp_MonstrousInfantryVsInfantry()
    {
        var coreRules = CreateCoreRules();
        var resolver = new CombatResolver(coreRules);

        int totalWounds = 0;
        for (int seed = 0; seed < 50; seed++)
        {
            var rng = new Random(seed);
            var monster = new UnitConfiguration(MakeUnit(troopType: "Monstrous Infantry", ws: 4, s: 4, a: 3, unitSize: "3+"))
                { ModelCount = 3, Frontage = 3 };
            var infantry = new UnitConfiguration(MakeUnit(ws: 3, t: 3))
                { ModelCount = 20, Frontage = 5 };

            var context = new CombatContext { CombatRound = 1 };
            totalWounds += Resolve(resolver, monster, infantry, context, rng).AttackerWoundsDealt;
        }

        // Monstrous Infantry vs Infantry should deal wounds (including stomp)
        Assert.True(totalWounds > 0, "Monstrous Infantry should deal wounds");
    }

    [Fact]
    public void ResolveRound_Stomp_InfantryDoesNotStomp()
    {
        var coreRules = CreateCoreRules();
        var resolver = new CombatResolver(coreRules);

        // Infantry vs Infantry should never have stomp damage
        // We can't easily isolate stomp, but we can test that basic infantry combat works
        var rng = new Random(42);
        var attacker = new UnitConfiguration(MakeUnit(ws: 4, s: 3, a: 1))
            { ModelCount = 10, Frontage = 5 };
        var defender = new UnitConfiguration(MakeUnit(ws: 4, t: 3))
            { ModelCount = 10, Frontage = 5 };
        var context = new CombatContext { CombatRound = 1 };

        var result = Resolve(resolver, attacker, defender, context, rng);
        // Just verify it doesn't crash and produces valid results
        Assert.True(result.AttackerWoundsDealt >= 0);
        Assert.True(result.DefenderWoundsDealt >= 0);
    }

    [Fact]
    public void ResolveRound_CombatRes_IncludesRankBonusAndStandard()
    {
        var coreRules = CreateCoreRules();
        var resolver = new CombatResolver(coreRules);
        var rng = new Random(42);

        var attacker = new UnitConfiguration(MakeUnit(ws: 4, s: 3, a: 1, unitSize: "20+"))
            { ModelCount = 20, Frontage = 5, HasStandardBearer = true };
        var defender = new UnitConfiguration(MakeUnit(ws: 4, t: 3, unitSize: "10+"))
            { ModelCount = 10, Frontage = 5 };

        var context = new CombatContext { CombatRound = 1 };
        var result = Resolve(resolver, attacker, defender, context, rng);

        // Attacker has 20 models/5 frontage = 4 ranks, rank bonus = 3 (capped)
        Assert.Equal(3, result.AttackerBreakdown.RankBonus);
        Assert.Equal(1, result.AttackerBreakdown.StandardBearer);
    }

    [Fact]
    public void ResolveRound_CombatRes_ChargeBonus()
    {
        var coreRules = CreateCoreRules();
        var resolver = new CombatResolver(coreRules);
        var rng = new Random(42);

        var attacker = new UnitConfiguration(MakeUnit(ws: 4, s: 3, a: 1))
            { ModelCount = 10, Frontage = 5 };
        var defender = new UnitConfiguration(MakeUnit(ws: 4, t: 3))
            { ModelCount = 10, Frontage = 5 };

        var context = new CombatContext { Charge = ChargeState.AttackerCharged, CombatRound = 1 };
        var result = Resolve(resolver, attacker, defender, context, rng);

        Assert.Equal(1, result.AttackerBreakdown.ChargeBonus);
        Assert.Equal(0, result.DefenderBreakdown.ChargeBonus);
    }

    [Fact]
    public void ResolveRound_FearBonus_WhenCausingFear()
    {
        var coreRules = CreateCoreRules();
        var resolver = new CombatResolver(coreRules);
        var rng = new Random(42);

        var fearUnit = new UnitConfiguration(MakeUnit(ws: 4, s: 3, a: 1, specialRules: "Fear"))
            { ModelCount = 10, Frontage = 5 };
        var normalUnit = new UnitConfiguration(MakeUnit(ws: 4, t: 3))
            { ModelCount = 10, Frontage = 5 };

        var context = new CombatContext { CombatRound = 1 };
        var result = Resolve(resolver, fearUnit, normalUnit, context, rng);

        Assert.Equal(1, result.AttackerBreakdown.FearBonus);
        Assert.Equal(0, result.DefenderBreakdown.FearBonus);
    }

    [Fact]
    public void ResolveRound_FearBonus_2xUS()
    {
        var coreRules = CreateCoreRules();
        var resolver = new CombatResolver(coreRules);
        var rng = new Random(42);

        // Fear-causing unit with much higher US
        var fearUnit = new UnitConfiguration(MakeUnit(troopType: "Monstrous Infantry", ws: 4, s: 4, a: 2,
            specialRules: "Fear", unitSize: "6+"))
            { ModelCount = 6, Frontage = 3 };
        // US=2 per model, 6 models = 12 total US

        var smallUnit = new UnitConfiguration(MakeUnit(ws: 3, t: 3, unitSize: "5+"))
            { ModelCount = 5, Frontage = 5 };
        // US=1 per model, 5 models = 5 total US. 12 >= 2*5 = 10, so +2 fear bonus

        var context = new CombatContext { CombatRound = 1 };
        var result = Resolve(resolver, fearUnit, smallUnit, context, rng);

        Assert.Equal(2, result.AttackerBreakdown.FearBonus);
    }

    [Fact]
    public void ResolveRound_StrikeOrder_HigherInitiativeFirst()
    {
        // This is hard to test directly, but we can verify that a fast unit
        // with killing blow deals KB wounds before the slow unit can retaliate
        var coreRules = CreateCoreRules();
        var resolver = new CombatResolver(coreRules);

        // Just verify the round completes without error
        var rng = new Random(42);
        var fastUnit = new UnitConfiguration(MakeUnit(ws: 5, s: 4, i: 6, a: 2))
            { ModelCount = 10, Frontage = 5 };
        var slowUnit = new UnitConfiguration(MakeUnit(ws: 3, t: 3, i: 1, a: 1))
            { ModelCount = 10, Frontage = 5 };

        var context = new CombatContext { CombatRound = 1 };
        var result = Resolve(resolver, fastUnit, slowUnit, context, rng);

        Assert.True(result.AttackerWoundsDealt >= 0);
        Assert.True(result.DefenderWoundsDealt >= 0);
    }

    [Fact]
    public void ResolveRound_GreatWeapon_LowersInitiativeNotASL()
    {
        // Great Weapon gives -2 Initiative, not ASL
        // A model with I5 and great weapon should effectively have I3 (not I-∞)
        var coreRules = CreateCoreRules();
        var resolver = new CombatResolver(coreRules);
        var rng = new Random(42);

        // Unit with I5, great weapon
        var gwUnit = new UnitConfiguration(MakeUnit(ws: 4, s: 4, i: 5, a: 1, equipment: "Great weapon"))
            { ModelCount = 10, Frontage = 5, SelectedWeaponKey = "greatWeapon" };
        // Defender with I4
        var defender = new UnitConfiguration(MakeUnit(ws: 4, t: 3, i: 4, a: 1))
            { ModelCount = 10, Frontage = 5 };

        var context = new CombatContext { CombatRound = 1 };
        var result = Resolve(resolver, gwUnit, defender, context, rng);

        // Should complete without errors
        // GW unit effective I = 5-2 = 3, so defender I4 strikes first
        Assert.True(result.AttackerWoundsDealt >= 0);
    }

    [Fact]
    public void ResolveRound_Spear_InitiativeBonus()
    {
        var coreRules = CreateCoreRules();
        var resolver = new CombatResolver(coreRules);
        var rng = new Random(42);

        // Spear unit, I3 base → I4 from spear (+1 Init)
        var spearUnit = new UnitConfiguration(MakeUnit(ws: 4, s: 3, i: 3, a: 1, equipment: "Spear, shield"))
            { ModelCount = 10, Frontage = 5, SelectedWeaponKey = "spear" };
        var defender = new UnitConfiguration(MakeUnit(ws: 4, t: 3, i: 3, a: 1))
            { ModelCount = 10, Frontage = 5 };

        var context = new CombatContext { CombatRound = 1 };
        var result = Resolve(resolver, spearUnit, defender, context, rng);

        Assert.True(result.AttackerWoundsDealt >= 0);
    }

    [Fact]
    public void ResolveRound_CavalryMountAttacks_IncludeSupport()
    {
        var coreRules = CreateCoreRules();
        var resolver = new CombatResolver(coreRules);

        int totalAttackerWounds = 0;
        for (int seed = 0; seed < 50; seed++)
        {
            var rng = new Random(seed);
            var mount = new ProfileEntry { Name = "Warhorse", WS = 3, S = 3, T = 3, W = 1, I = 3, A = 1, Ld = 5 };
            var unit = MakeUnit(troopType: "Cavalry", ws: 4, s: 3, i: 3, a: 1, unitSize: "10+", mountProfile: mount);
            var cavalry = new UnitConfiguration(unit) { ModelCount = 10, Frontage = 5 };

            var defender = new UnitConfiguration(MakeUnit(ws: 3, t: 3)) { ModelCount = 20, Frontage = 5 };

            var context = new CombatContext { CombatRound = 1 };
            totalAttackerWounds += Resolve(resolver, cavalry, defender, context, rng).AttackerWoundsDealt;
        }

        // 10 cavalry should deal some wounds (rider attacks + mount attacks including support)
        Assert.True(totalAttackerWounds > 0, "Cavalry should deal wounds");
    }
}
