namespace Warhammer.Tests;

/// <summary>Locates the app project's data files from wherever the test host is running.</summary>
internal static class TestPaths
{
    public static string SystemDataDir(string systemKey) =>
        Path.Combine(ProjectRoot(), "wwwroot", "data", systemKey);

    public static string ProjectRoot()
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
}
