using Warhammer.Calculator;
using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// The Troop Types chapter (p71–76) diverges between the editions in three places, all
/// verified against the 2.32 and 3.11 core rules books: supporting attacks for the monstrous
/// types, mount supporting attacks, and the inherent special rules.
/// </summary>
public class TroopTypeBySystemTests
{
    private static GameDataRepository RepoFor(string systemKey)
    {
        var wwwroot = Path.Combine(TestHelpers.FindProjectRoot(), "wwwroot");
        var http = new HttpClient(new FileBasedHttpHandler(wwwroot)) { BaseAddress = new Uri("http://localhost/") };
        var system = new GameSystemService();
        system.Initialize(systemKey);
        var coreRules = new CoreRulesService(http, system);
        var factionData = new FactionDataService(http, system);
        var repo = new GameDataRepository(coreRules, factionData, system);
        repo.EnsureLoadedAsync().GetAwaiter().GetResult();
        return repo;
    }

    // ─── Supporting attacks ────────────────────────────────────────

    [Theory]
    [InlineData("Monstrous Infantry", GameSystems.WapV2, 2)]
    [InlineData("Monstrous Infantry", GameSystems.WapV3, 3)]
    [InlineData("Monstrous Beast", GameSystems.WapV2, 2)]
    [InlineData("Monstrous Beast", GameSystems.WapV3, 3)]
    [InlineData("Infantry", GameSystems.WapV2, 1)]
    [InlineData("Infantry", GameSystems.WapV3, 1)]
    [InlineData("Swarms", GameSystems.WapV2, 5)]
    [InlineData("Swarms", GameSystems.WapV3, 5)]
    [InlineData("War Beasts", GameSystems.WapV2, 1)]
    [InlineData("War Beasts", GameSystems.WapV3, 1)]
    public void SupportingAttacksMatchTheBook(string troopType, string systemKey, int expected)
    {
        Assert.Equal(expected, TroopTypeInfo.Parse(troopType, systemKey).MaxSupportingAttacks);
    }

    [Theory]
    [InlineData("Cavalry", GameSystems.WapV2, 1, 1, 2)]
    [InlineData("Cavalry", GameSystems.WapV3, 1, 0, 1)]
    [InlineData("Monstrous Cavalry", GameSystems.WapV2, 1, 2, 3)]
    [InlineData("Monstrous Cavalry", GameSystems.WapV3, 1, 0, 3)]
    public void MountedSupportingAttacksMatchTheBook(
        string troopType, string systemKey, int rider, int mount, int nonSplit)
    {
        var info = TroopTypeInfo.Parse(troopType, systemKey);

        Assert.Equal(rider, info.MaxSupportingAttacks);
        Assert.Equal(mount, info.MountSupportingAttacks);
        Assert.Equal(nonSplit, info.NonSplitSupportingAttacks);
    }

    [Fact]
    public void V3MountsMakeNoSupportingAttacks()
    {
        // "Mounts are not allowed to make supporting attacks" (3.11 p72–73).
        var mount = new ProfileEntry { Name = "Warhorse", M = 8, WS = 3, S = 3, T = 3, W = 1, I = 3, A = 1 };

        int V3Mount(string troopType) => new UnitConfiguration(
            TestHelpers.MakeUnit(troopType: troopType, mountProfile: mount, systemKey: GameSystems.WapV3))
        { ModelCount = 10, Frontage = 5 }.MountAttacks();

        int V2Mount(string troopType) => new UnitConfiguration(
            TestHelpers.MakeUnit(troopType: troopType, mountProfile: mount, systemKey: GameSystems.WapV2))
        { ModelCount = 10, Frontage = 5 }.MountAttacks();

        // Five front-rank mounts attack in both; only wap-v2 adds the second rank's five.
        Assert.Equal(5, V3Mount("Cavalry"));
        Assert.Equal(10, V2Mount("Cavalry"));
    }

    // ─── Inherent Impact Hits ──────────────────────────────────────

    [Theory]
    [InlineData("Cavalry", GameSystems.WapV2, 1)]
    [InlineData("Cavalry", GameSystems.WapV3, 0)]
    [InlineData("Monstrous Cavalry", GameSystems.WapV2, 1)]
    [InlineData("Monstrous Cavalry", GameSystems.WapV3, 0)]
    [InlineData("Infantry", GameSystems.WapV2, 0)]
    [InlineData("Chariot", GameSystems.WapV3, 0)]
    public void InherentImpactHitsMatchTheBook(string troopType, string systemKey, int expected)
    {
        Assert.Equal(expected, TroopTypeInfo.Parse(troopType, systemKey).InherentImpactHits);
    }

    [Fact]
    public async Task V3KnightsDoNoImpactHitsOnTheCharge()
    {
        // Knights of the Realm have no Impact Hits on their entry, so in wap-v3 a charge
        // produces none at all. In wap-v2 the troop type supplies one per front-rank model.
        var resolverV2 = new AttackResolver(RepoFor(GameSystems.WapV2));
        var resolverV3 = new AttackResolver(RepoFor(GameSystems.WapV3));

        async Task<int> Wounds(GameDataRepository repo, AttackResolver resolver)
        {
            var knights = new UnitConfiguration((await repo.GetUnitsAsync(FactionKey.Bretonnia))
                .First(u => u.Name == "Knight of the Realm")) { ModelCount = 10, Frontage = 5 };
            var target = new UnitConfiguration((await repo.GetUnitsAsync(FactionKey.TheEmpire))
                .First(u => u.Name == "Halberdiers")) { ModelCount = 25, Frontage = 5 };
            var context = new CombatContext { Charge = ChargeState.AttackerCharged, CombatRound = 1 };

            int total = 0;
            var rng = new Random(4);
            for (int i = 0; i < 2000; i++)
                total += resolver.ResolveImpactHits(knights, target, context, rng,
                    resolver.ComputeBaseArmour(target));
            return total;
        }

        int v2 = await Wounds(RepoFor(GameSystems.WapV2), resolverV2);
        int v3 = await Wounds(RepoFor(GameSystems.WapV3), resolverV3);

        Assert.True(v2 > 0, "wap-v2 cavalry should land impact hits on the charge");
        Assert.Equal(0, v3);
    }

    // ─── Inherent Swiftstride ──────────────────────────────────────

    [Theory]
    [InlineData("Cavalry")]
    [InlineData("Monstrous Cavalry")]
    [InlineData("Monstrous Infantry")]
    [InlineData("Monstrous Beast")]
    [InlineData("War Beasts")]
    [InlineData("Monster")]
    [InlineData("Chariot")]
    public void SwiftstrideIsInherentInV2AndGoneInV3(string troopType)
    {
        // wap-v2 lists Swiftstride against each of these types; wap-v3 lists Independent
        // in its place, which has no combat effect here.
        Assert.True(TroopTypeInfo.Parse(troopType, GameSystems.WapV2).HasInherentSwiftstride, troopType);
        Assert.False(TroopTypeInfo.Parse(troopType, GameSystems.WapV3).HasInherentSwiftstride, troopType);
    }

    [Theory]
    [InlineData("Infantry")]
    [InlineData("Swarms")]
    [InlineData("War Machine")]
    public void FootTroopsNeverGetInherentSwiftstride(string troopType)
    {
        Assert.False(TroopTypeInfo.Parse(troopType, GameSystems.WapV2).HasInherentSwiftstride);
        Assert.False(TroopTypeInfo.Parse(troopType, GameSystems.WapV3).HasInherentSwiftstride);
    }

    // ─── Through the engine ────────────────────────────────────────

    [Fact]
    public async Task V3MonstrousInfantryFieldsMoreAttacksThanV2()
    {
        // Kroxigors, 6 models three wide: two ranks, so the second rank supports. wap-v2 caps
        // a supporting model at two attacks, wap-v3 at three, and Kroxigors have three each.
        int Attacks(GameDataRepository repo, IReadOnlyList<Unit> units) =>
            new UnitConfiguration(units.First(u => u.Name == "Kroxigors")) { ModelCount = 6, Frontage = 3 }
                .TotalAttacks(isCharging: false, combatRound: 1);

        var v2Repo = RepoFor(GameSystems.WapV2);
        var v3Repo = RepoFor(GameSystems.WapV3);

        int v2 = Attacks(v2Repo, await v2Repo.GetUnitsAsync(FactionKey.Lizardmen));
        int v3 = Attacks(v3Repo, await v3Repo.GetUnitsAsync(FactionKey.Lizardmen));

        Assert.Equal(3 * 3 + 3 * 2, v2);   // front rank full, supporting rank capped at 2
        Assert.Equal(3 * 3 + 3 * 3, v3);   // supporting rank now matches the profile
        Assert.True(v3 > v2);
    }
}
