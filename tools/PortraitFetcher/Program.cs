using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PortraitFetcher;

internal static class Program
{
    // ─── Configuration ────────────────────────────────────────────

    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string FactionsDir = Path.Combine(RepoRoot, "Warhammer", "wwwroot", "data", "factions");
    private static readonly string OutputDir = Path.Combine(RepoRoot, "Warhammer", "wwwroot", "images", "units");
    private static readonly string OverridesPath = Path.Combine(RepoRoot, "tools", "PortraitFetcher", "portrait-overrides.json");
    private static readonly string ReportPath = Path.Combine(RepoRoot, "tools", "PortraitFetcher", "portrait-report.json");
    private static readonly string ManifestPath = Path.Combine(RepoRoot, "Warhammer", "wwwroot", "data", "portrait-manifest.json");

    private const string ApiBase = "https://totalwarwarhammer.fandom.com/api.php";
    private const string UserAgent = "warhammer-combat-simulator/portrait-fetcher (personal, non-published)";
    private static readonly TimeSpan RequestDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>WAP faction key → TW wiki roster page title.</summary>
    private static readonly Dictionary<string, string> RosterPageByFaction = new()
    {
        ["beastmen"]         = "Beastmen_unit_roster",
        ["bretonnia"]        = "Bretonnia_unit_roster",
        ["daemons-of-chaos"] = "Daemons_of_Chaos_unit_roster",
        ["dark-elves"]       = "Dark_Elves_unit_roster",
        ["dwarfs"]           = "Dwarfs_unit_roster",
        ["high-elves"]       = "High_Elves_unit_roster",
        ["lizardmen"]        = "Lizardmen_unit_roster",
        ["ogre-kingdoms"]    = "Ogre_Kingdoms_unit_roster",
        ["orcs-goblins"] = "Greenskins_unit_roster",
        ["skaven"]           = "Skaven_unit_roster",
        ["empire"]           = "Empire_unit_roster",
        ["tomb-kings"]       = "Tomb_Kings_unit_roster",
        ["vampire-counts"]   = "Vampire_Counts_unit_roster",
        ["warriors-of-chaos"]= "Warriors_of_Chaos_unit_roster",
        ["wood-elves"]       = "Wood_Elves_unit_roster",
    };

    // ─── Entry ────────────────────────────────────────────────────

    private static async Task<int> Main(string[] args)
    {
        string? faction = null;
        bool dryRun = false;

        foreach (var arg in args)
        {
            if (arg.StartsWith("--faction=", StringComparison.OrdinalIgnoreCase))
                faction = arg["--faction=".Length..].ToLowerInvariant();
            else if (arg.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
                dryRun = true;
            else if (arg is "--help" or "-h")
            {
                PrintUsage();
                return 0;
            }
        }

        var factions = faction == null
            ? RosterPageByFaction.Keys.ToList()
            : RosterPageByFaction.ContainsKey(faction)
                ? new List<string> { faction }
                : throw new ArgumentException($"Unknown faction '{faction}'. Known: {string.Join(", ", RosterPageByFaction.Keys)}");

        Console.WriteLine($"PortraitFetcher — {factions.Count} faction(s), dry-run: {dryRun}");
        Console.WriteLine($"  Output: {OutputDir}");
        Console.WriteLine();

        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true });
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var overrides = await LoadOverridesAsync();
        var allReports = new List<FactionReport>();

        foreach (var fac in factions)
        {
            var report = await ProcessFactionAsync(http, fac, overrides, dryRun);
            allReports.Add(report);
            Console.WriteLine();
        }

        await WriteReportAsync(allReports);
        await WriteManifestAsync();
        PrintSummary(allReports);
        return 0;
    }

    /// <summary>
    /// Scans wwwroot/images/units/ and writes a slug manifest so the app can
    /// avoid 404s when rendering portraits. Picks up both fetched and manually-dropped files.
    /// </summary>
    private static async Task WriteManifestAsync()
    {
        var manifest = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        if (Directory.Exists(OutputDir))
        {
            foreach (var factionDir in Directory.EnumerateDirectories(OutputDir))
            {
                var factionKey = Path.GetFileName(factionDir);
                var slugs = Directory.EnumerateFiles(factionDir, "*.png")
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => s!)
                    .OrderBy(s => s, StringComparer.Ordinal)
                    .ToList();
                if (slugs.Count > 0) manifest[factionKey] = slugs;
            }
        }
        var json = JsonSerializer.Serialize(manifest, JsonOpts);
        Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!);
        await File.WriteAllTextAsync(ManifestPath, json);
        Console.WriteLine($"Manifest: {ManifestPath}  ({manifest.Sum(kv => kv.Value.Count)} entries across {manifest.Count} factions)");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: PortraitFetcher [--faction=<key>] [--dry-run]");
        Console.WriteLine("  --faction=<key>  Process a single WAP faction (e.g. lizardmen). Default: all.");
        Console.WriteLine("  --dry-run        Match + resolve URLs but do not download.");
    }

    // ─── Per-faction pipeline ─────────────────────────────────────

    private static async Task<FactionReport> ProcessFactionAsync(
        HttpClient http, string factionKey, Overrides overrides, bool dryRun)
    {
        var report = new FactionReport { Faction = factionKey };

        Console.WriteLine($"[{factionKey}]");

        // 1. Load WAP unit names.
        var wapUnits = LoadWapUnitNames(factionKey);
        if (wapUnits.Count == 0)
        {
            Console.WriteLine($"  No WAP units found — skipping.");
            return report;
        }
        Console.WriteLine($"  WAP units: {wapUnits.Count}");

        // 2. Fetch + parse TW roster.
        var rosterPage = RosterPageByFaction[factionKey];
        string wikitext;
        try
        {
            wikitext = await FetchWikitextAsync(http, rosterPage);
            await Task.Delay(RequestDelay);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ Failed to fetch '{rosterPage}': {ex.Message}");
            report.FetchError = ex.Message;
            return report;
        }

        var twEntries = ExtractFiconEntries(wikitext);
        Console.WriteLine($"  TW roster entries: {twEntries.Count}");

        // 3. Match TW → WAP.
        var matches = MatchUnits(factionKey, wapUnits, twEntries, overrides);
        Console.WriteLine($"  Matched: {matches.Count(m => m.Filename != null)} / {wapUnits.Count}");

        // 4. Resolve + download.
        var outputFactionDir = Path.Combine(OutputDir, factionKey);
        if (!dryRun) Directory.CreateDirectory(outputFactionDir);

        foreach (var match in matches)
        {
            if (match.Filename == null)
            {
                report.Unmatched.Add(match.WapName);
                continue;
            }

            var destPath = Path.Combine(outputFactionDir, match.Slug + ".png");

            if (!dryRun && File.Exists(destPath))
            {
                report.AlreadyPresent.Add(match.WapName);
                continue;
            }

            string? imageUrl;
            try
            {
                imageUrl = await ResolveImageUrlAsync(http, match.Filename);
                await Task.Delay(RequestDelay);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ {match.WapName}: resolve failed ({ex.Message})");
                report.ResolveFailures.Add(new FailureEntry(match.WapName, match.Filename, ex.Message));
                continue;
            }

            if (imageUrl == null)
            {
                Console.WriteLine($"  ✗ {match.WapName}: no URL for {match.Filename}");
                report.ResolveFailures.Add(new FailureEntry(match.WapName, match.Filename, "no URL returned"));
                continue;
            }

            if (dryRun)
            {
                report.WouldDownload.Add(new DownloadEntry(match.WapName, match.Filename, imageUrl, match.Slug));
                continue;
            }

            try
            {
                await DownloadAsync(http, imageUrl, destPath);
                await Task.Delay(RequestDelay);
                report.Downloaded.Add(new DownloadEntry(match.WapName, match.Filename, imageUrl, match.Slug));
                Console.WriteLine($"  ✓ {match.WapName} → {match.Slug}.png");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ {match.WapName}: download failed ({ex.Message})");
                report.DownloadFailures.Add(new FailureEntry(match.WapName, match.Filename, ex.Message));
            }
        }

        return report;
    }

    // ─── Data loading ─────────────────────────────────────────────

    private static List<string> LoadWapUnitNames(string factionKey)
    {
        var path = Path.Combine(FactionsDir, $"{factionKey}.json");
        if (!File.Exists(path)) return new();

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("units", out var units)) return new();

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var u in units.EnumerateArray())
        {
            if (!u.TryGetProperty("name", out var nameEl)) continue;
            var name = nameEl.GetString();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (seen.Add(name)) result.Add(name);
        }
        return result;
    }

    private static async Task<Overrides> LoadOverridesAsync()
    {
        if (!File.Exists(OverridesPath)) return new Overrides();
        var json = await File.ReadAllTextAsync(OverridesPath);
        var parsed = JsonSerializer.Deserialize<OverridesFile>(json, JsonOpts);
        return new Overrides(
            parsed?.Overrides ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(parsed?.Skip ?? new List<string>(), StringComparer.OrdinalIgnoreCase));
    }

    // ─── Fandom API calls ─────────────────────────────────────────

    private static async Task<string> FetchWikitextAsync(HttpClient http, string pageTitle)
    {
        var url = $"{ApiBase}?action=parse&page={Uri.EscapeDataString(pageTitle)}&format=json&prop=wikitext&redirects=1";
        using var resp = await http.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("error", out var err))
            throw new InvalidOperationException(err.GetProperty("info").GetString() ?? "Unknown API error");
        return doc.RootElement
            .GetProperty("parse")
            .GetProperty("wikitext")
            .GetProperty("*")
            .GetString() ?? "";
    }

    private static async Task<string?> ResolveImageUrlAsync(HttpClient http, string filename)
    {
        var url = $"{ApiBase}?action=query&titles=File:{Uri.EscapeDataString(filename)}&prop=imageinfo&iiprop=url&format=json";
        using var resp = await http.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("query", out var query)) return null;
        if (!query.TryGetProperty("pages", out var pages)) return null;
        foreach (var page in pages.EnumerateObject())
        {
            if (!page.Value.TryGetProperty("imageinfo", out var infoArr)) continue;
            foreach (var info in infoArr.EnumerateArray())
            {
                if (info.TryGetProperty("url", out var urlEl))
                    return urlEl.GetString();
            }
        }
        return null;
    }

    private static async Task DownloadAsync(HttpClient http, string url, string destPath)
    {
        using var resp = await http.GetAsync(url);
        if (resp.StatusCode == HttpStatusCode.Forbidden)
            throw new InvalidOperationException("403 Forbidden from CDN");
        resp.EnsureSuccessStatusCode();
        await using var fs = File.Create(destPath);
        await resp.Content.CopyToAsync(fs);
    }

    // ─── Parsing + matching ───────────────────────────────────────

    // {{ficon|filename.png|Display Name}}  — sometimes the display name is omitted.
    private static readonly Regex FiconRegex = new(
        @"\{\{\s*ficon\s*\|\s*([^|}\s][^|}]*?)\s*(?:\|\s*([^}]*?)\s*)?\}\}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static List<TwEntry> ExtractFiconEntries(string wikitext)
    {
        var entries = new List<TwEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in FiconRegex.Matches(wikitext))
        {
            var filename = m.Groups[1].Value.Trim();
            if (!filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                && !filename.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                continue;

            var display = m.Groups[2].Success && !string.IsNullOrWhiteSpace(m.Groups[2].Value)
                ? m.Groups[2].Value.Trim()
                : DeriveNameFromFilename(filename);

            if (!seen.Add(filename)) continue;
            entries.Add(new TwEntry(filename, display));
        }
        return entries;
    }

    private static string DeriveNameFromFilename(string filename)
    {
        var stem = Path.GetFileNameWithoutExtension(filename);
        // Strip known prefixes: wh2_main_lzd_inf_, wh3_dlc24_emp_cav_, etc.
        var parts = stem.Split('_');
        var keep = parts
            .SkipWhile(p => p is "wh" or "wh2" or "wh3"
                         || p.StartsWith("wh", StringComparison.OrdinalIgnoreCase) && p.Length <= 3
                         || p is "main" or "dlc" || p.StartsWith("dlc", StringComparison.OrdinalIgnoreCase)
                         || p.Length == 3); // faction code + type code (lzd/inf/cav/mon)
        return string.Join(' ', keep);
    }

    private static List<MatchResult> MatchUnits(
        string factionKey,
        List<string> wapUnits,
        List<TwEntry> twEntries,
        Overrides overrides)
    {
        var results = new List<MatchResult>(wapUnits.Count);

        // Index TW entries by normalized display name for fast lookup.
        var twByNormalized = new Dictionary<string, TwEntry>(StringComparer.Ordinal);
        foreach (var entry in twEntries)
        {
            var norm = Normalize(entry.DisplayName);
            if (!twByNormalized.ContainsKey(norm)) twByNormalized[norm] = entry;
        }

        foreach (var wap in wapUnits)
        {
            var overrideKey = $"{factionKey}/{wap}".ToLowerInvariant();
            if (overrides.Skip.Contains(overrideKey))
            {
                results.Add(new MatchResult(wap, Slugify(wap), null));
                continue;
            }

            string? filename = null;

            if (overrides.Map.TryGetValue(overrideKey, out var overrideFile))
                filename = overrideFile;

            if (filename == null)
            {
                var normWap = Normalize(wap);
                if (twByNormalized.TryGetValue(normWap, out var direct))
                    filename = direct.Filename;
                else
                {
                    // Fuzzy: strip trailing 's', try substring match on key words.
                    var candidate = twByNormalized
                        .Where(kv => FuzzyMatch(kv.Key, normWap))
                        .Select(kv => kv.Value)
                        .FirstOrDefault();
                    if (candidate != null) filename = candidate.Filename;
                }
            }

            results.Add(new MatchResult(wap, Slugify(wap), filename));
        }

        return results;
    }

    private static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
        }
        return sb.ToString().Trim();
    }

    private static bool FuzzyMatch(string twNorm, string wapNorm)
    {
        if (twNorm == wapNorm) return true;
        // Trailing plural variance: "kroxigors" vs "kroxigor", "skinks" vs "skink".
        if (twNorm.TrimEnd('s') == wapNorm.TrimEnd('s')) return true;
        // Prefix match — WAP "Saurus Scar-veteran" ↔ TW "Saurus Scar-Veterans".
        if (twNorm.StartsWith(wapNorm + " ") || wapNorm.StartsWith(twNorm + " ")) return true;
        return false;
    }

    private static string Slugify(string s)
    {
        var sb = new StringBuilder(s.Length);
        char last = '-';
        foreach (var c in s.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) { sb.Append(c); last = c; }
            else if (last != '-') { sb.Append('-'); last = '-'; }
        }
        return sb.ToString().Trim('-');
    }

    // ─── Report ───────────────────────────────────────────────────

    private static async Task WriteReportAsync(List<FactionReport> reports)
    {
        var json = JsonSerializer.Serialize(reports, JsonOpts);
        await File.WriteAllTextAsync(ReportPath, json);
        Console.WriteLine($"Report: {ReportPath}");
    }

    private static void PrintSummary(List<FactionReport> reports)
    {
        Console.WriteLine();
        Console.WriteLine("─── Summary ────────────────────────────────");
        foreach (var r in reports)
        {
            var matched = r.Downloaded.Count + r.AlreadyPresent.Count + r.WouldDownload.Count;
            var total = matched + r.Unmatched.Count + r.ResolveFailures.Count + r.DownloadFailures.Count;
            Console.WriteLine($"  {r.Faction,-18} matched {matched}/{total}  unmatched: {r.Unmatched.Count}  failures: {r.ResolveFailures.Count + r.DownloadFailures.Count}");
            if (r.Unmatched.Count > 0)
                Console.WriteLine($"    Unmatched: {string.Join(", ", r.Unmatched)}");
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Warhammer"))
                && File.Exists(Path.Combine(dir.FullName, "Warhammer", "Warhammer.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate repo root (expected a 'Warhammer/Warhammer.csproj' ancestor).");
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ─── Records ──────────────────────────────────────────────────

    private sealed record TwEntry(string Filename, string DisplayName);
    private sealed record MatchResult(string WapName, string Slug, string? Filename);
    private sealed record DownloadEntry(string WapName, string Filename, string Url, string Slug);
    private sealed record FailureEntry(string WapName, string Filename, string Reason);

    private sealed class Overrides
    {
        public Dictionary<string, string?> Map { get; }
        public HashSet<string> Skip { get; }
        public Overrides() : this(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase)) { }
        public Overrides(Dictionary<string, string?> map, HashSet<string> skip) { Map = map; Skip = skip; }
    }

    private sealed class OverridesFile
    {
        public Dictionary<string, string?>? Overrides { get; set; }
        public List<string>? Skip { get; set; }
    }

    private sealed class FactionReport
    {
        public string Faction { get; set; } = "";
        public string? FetchError { get; set; }
        public List<DownloadEntry> Downloaded { get; set; } = new();
        public List<DownloadEntry> WouldDownload { get; set; } = new();
        public List<string> AlreadyPresent { get; set; } = new();
        public List<string> Unmatched { get; set; } = new();
        public List<FailureEntry> ResolveFailures { get; set; } = new();
        public List<FailureEntry> DownloadFailures { get; set; } = new();
    }
}
