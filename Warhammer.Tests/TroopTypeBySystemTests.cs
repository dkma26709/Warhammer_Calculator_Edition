using System.Text.Json;
using Warhammer.Calculator;
using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// The Troop Types chapter's figures, as the per-system troop-types.json states them:
/// supporting attacks for the monstrous types, mount supporting attacks, inherent Impact
/// Hits and Swiftstride. Verified against the rulebook the bundle prints.
/// </summary>
public class TroopTypeBySystemTests
{
    // ─── The figures are data, and a gap is loud ───────────────────

    /// <summary>
    /// Adding a system is a data exercise (AGENTS.md), so the figures live in a per-system
    /// troop-types.json. A system whose file is missing, or missing a row, must fail on load
    /// rather than quietly inherit another edition's numbers.
    /// </summary>
    [Fact]
    public void AMissingTroopTypeRowFailsOnLoad()
    {
        var wwwroot = Path.Combine(TestHelpers.FindProjectRoot(), "wwwroot");
        var http = new HttpClient(new TroopTypeRowRemovingHandler(wwwroot, "monstrous-cavalry"))
        {
            BaseAddress = new Uri("http://localhost/"),
        };
        var system = new GameSystemService();
        system.Initialize(GameSystems.War);
        var repo = new GameDataRepository(
            new CoreRulesService(http, system), new BundleService(http, system), system);

        var error = Assert.Throws<InvalidOperationException>(
            () => repo.EnsureLoadedAsync().GetAwaiter().GetResult());

        Assert.Contains("troop-types.json", error.Message);
        Assert.Contains("monstrous-cavalry", error.Message);
        Assert.Contains(GameSystems.War, error.Message);
    }

    /// <summary>Serves the real data, minus one troop type row.</summary>
    private sealed class TroopTypeRowRemovingHandler(string baseDir, string rowToRemove)
        : FileBasedHttpHandler(baseDir)
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (!request.RequestUri!.AbsolutePath.EndsWith("troop-types.json"))
                return response;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var types = doc.RootElement.GetProperty("types").EnumerateObject()
                .Where(p => p.Name != rowToRemove)
                .ToDictionary(p => p.Name, p => p.Value.Clone());

            var trimmed = JsonSerializer.Serialize(new { types });
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(trimmed, System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }

    // ─── Supporting attacks ────────────────────────────────────────

    [Theory]
    [InlineData("Monstrous Infantry", 3)]
    [InlineData("Monstrous Beast", 3)]
    [InlineData("Infantry", 1)]
    [InlineData("Swarms", 5)]
    [InlineData("War Beasts", 1)]
    public void SupportingAttacksMatchTheBook(string troopType, int expected)
    {
        Assert.Equal(expected, TroopTypeInfo.Parse(troopType, TestHelpers.TroopTypeRulesFor(GameSystems.War)).MaxSupportingAttacks);
    }

    [Theory]
    [InlineData("Cavalry", 1, 0, 1)]
    [InlineData("Monstrous Cavalry", 1, 0, 3)]
    public void MountedSupportingAttacksMatchTheBook(
        string troopType, int rider, int mount, int nonSplit)
    {
        var info = TroopTypeInfo.Parse(troopType, TestHelpers.TroopTypeRulesFor(GameSystems.War));

        Assert.Equal(rider, info.MaxSupportingAttacks);
        Assert.Equal(mount, info.MountSupportingAttacks);
        Assert.Equal(nonSplit, info.NonSplitSupportingAttacks);
    }

    [Fact]
    public void MountsMakeNoSupportingAttacks()
    {
        // "Mounts are not allowed to make supporting attacks."
        var mount = new ProfileEntry { Name = "Warhorse", M = 8, WS = 3, S = 3, T = 3, W = 1, I = 3, A = 1 };

        int MountAttacks(string troopType) => new UnitConfiguration(
            TestHelpers.MakeUnit(troopType: troopType, mountProfile: mount, systemKey: GameSystems.War))
        { ModelCount = 10, Frontage = 5 }.MountAttacks();

        // Five front-rank mounts attack; the second rank's five do not.
        Assert.Equal(5, MountAttacks("Cavalry"));
    }

    // ─── Inherent Impact Hits ──────────────────────────────────────

    [Theory]
    [InlineData("Cavalry", 0)]
    [InlineData("Monstrous Cavalry", 0)]
    [InlineData("Infantry", 0)]
    [InlineData("Chariot", 0)]
    public void NoTroopTypeGrantsImpactHitsOfItsOwn(string troopType, int expected)
    {
        // Chariots take their Impact Hits from their own entry's special rules line.
        Assert.Equal(expected, TroopTypeInfo.Parse(troopType, TestHelpers.TroopTypeRulesFor(GameSystems.War)).InherentImpactHits);
    }

    [Fact]
    public async Task KnightsDoNoImpactHitsOnTheCharge()
    {
        // Knights of the Realm have no Impact Hits on their entry, and the troop type supplies
        // none, so a charge produces none at all.
        var repo = TestHelpers.RepoFor(GameSystems.War);
        var resolver = new AttackResolver(repo);

        var knights = new UnitConfiguration(await TestHelpers.FindUnitAsync("bretonnia", "KNIGHT OF THE REALM"))
            { ModelCount = 10, Frontage = 5 };
        var target = new UnitConfiguration(await TestHelpers.FindUnitAsync("empire", "HALBERDIERS"))
            { ModelCount = 25, Frontage = 5 };
        var context = new CombatContext { Charge = ChargeState.AttackerCharged, CombatRound = 1 };

        int total = 0;
        var rng = new Random(4);
        for (int i = 0; i < 2000; i++)
            total += resolver.ResolveImpactHits(knights, target, context, rng,
                resolver.ComputeBaseArmour(target));

        Assert.Equal(0, total);
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
    [InlineData("Infantry")]
    [InlineData("Swarms")]
    [InlineData("War Machine")]
    public void NoTroopTypeGrantsSwiftstride(string troopType)
    {
        // The chapter lists Independent against the mounted types, which has no combat effect here.
        Assert.False(TroopTypeInfo.Parse(troopType, TestHelpers.TroopTypeRulesFor(GameSystems.War)).HasInherentSwiftstride, troopType);
    }

    // ─── Through the engine ────────────────────────────────────────

    [Fact]
    public async Task MonstrousInfantrySupportsThreeAttacks()
    {
        // Kroxigors, 6 models three wide: two ranks, so the second rank supports with all
        // three of its attacks.
        var kroxigors = await TestHelpers.FindUnitAsync("lizardmen", "KROXIGORS");
        var config = new UnitConfiguration(kroxigors) { ModelCount = 6, Frontage = 3 };

        int attacks = kroxigors.BaseModel.Stats.A ?? 1;
        Assert.Equal(3 * attacks + 3 * Math.Min(attacks, 3), config.TotalAttacks(isCharging: false, combatRound: 1));
    }
}
