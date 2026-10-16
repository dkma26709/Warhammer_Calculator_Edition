using System.Collections.Concurrent;
using System.Net;
using Warhammer.Models.Domain;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// A page and the faction pickers on it all call <see cref="GameDataRepository.EnsureLoadedAsync"/>
/// while they render, so the load is shared rather than repeated, and a failed one can be retried.
/// </summary>
public class GameDataRepositoryLoadTests
{
    [Fact]
    public async Task ConcurrentCallersShareOneLoad()
    {
        var handler = new CountingHandler(Wwwroot());
        var repo = Unloaded(handler);

        await Task.WhenAll(repo.EnsureLoadedAsync(), repo.EnsureLoadedAsync(), repo.EnsureLoadedAsync());

        Assert.NotEmpty(repo.AllFactions);
        Assert.Equal(1, handler.Requests("core-rules.json"));
        Assert.Equal(1, handler.Requests("manifest.json"));
    }

    [Fact]
    public async Task AFailedLoadIsRetried()
    {
        var handler = new CountingHandler(Wwwroot()) { FailFirst = "manifest.json" };
        var repo = Unloaded(handler);

        await Assert.ThrowsAsync<HttpRequestException>(repo.EnsureLoadedAsync);
        Assert.Empty(repo.AllFactions);

        await repo.EnsureLoadedAsync();

        Assert.NotEmpty(repo.AllFactions);
        Assert.Equal(2, handler.Requests("manifest.json"));
    }

    private static string Wwwroot() => Path.Combine(TestHelpers.FindProjectRoot(), "wwwroot");

    private static GameDataRepository Unloaded(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var system = new GameSystemService();
        system.Initialize(GameSystems.WapV2);
        return new GameDataRepository(
            new CoreRulesService(http, system), new FactionDataService(http, system), system);
    }

    /// <summary>
    /// Serves the real data, counting requests per file. It yields before answering, as a
    /// browser fetch does, so concurrent callers really do overlap.
    /// </summary>
    private sealed class CountingHandler(string baseDir) : FileBasedHttpHandler(baseDir)
    {
        private readonly ConcurrentDictionary<string, int> _counts = new();

        /// <summary>A file name whose first request answers 404.</summary>
        public string? FailFirst { get; init; }

        public int Requests(string fileName) => _counts.GetValueOrDefault(fileName);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Yield();
            var fileName = Path.GetFileName(request.RequestUri!.AbsolutePath);
            var count = _counts.AddOrUpdate(fileName, 1, (_, n) => n + 1);

            if (fileName == FailFirst && count == 1)
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
