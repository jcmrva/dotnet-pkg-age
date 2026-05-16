namespace DotnetPkgAge.Tests;

public class BypassListTests : IDisposable
{
    private readonly string _tempDir;

    public BypassListTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        BypassList.DefaultPath = Path.Combine(_tempDir, "pkg-age-bypass.json");
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    private void WriteBypass(string json) =>
        File.WriteAllText(BypassList.DefaultPath, json);

    [Fact]
    public void TryGet_ReturnsFalse_WhenFileDoesNotExist()
    {
        var found = BypassList.TryGet("Newtonsoft.Json", "13.0.3", out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGet_ReturnsTrue_WhenPackageVersionIsListed()
    {
        WriteBypass("""{ "Newtonsoft.Json@13.0.3": "security hotfix" }""");

        var found = BypassList.TryGet("Newtonsoft.Json", "13.0.3", out var reason);

        Assert.True(found);
        Assert.Equal("security hotfix", reason);
    }

    [Fact]
    public void TryGet_ReturnsFalse_ForUnknownVersion()
    {
        WriteBypass("""{ "Newtonsoft.Json@13.0.3": "security hotfix" }""");

        var found = BypassList.TryGet("Newtonsoft.Json", "12.0.0", out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGet_ReturnsFalse_ForUnknownPackage()
    {
        WriteBypass("""{ "Newtonsoft.Json@13.0.3": "security hotfix" }""");

        var found = BypassList.TryGet("SomeOtherPackage", "13.0.3", out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenFileIsCorrupted()
    {
        WriteBypass("not valid json {{{{");

        var found = BypassList.TryGet("Newtonsoft.Json", "13.0.3", out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGet_SupportsMultipleEntries()
    {
        WriteBypass("""
            {
                "Newtonsoft.Json@13.0.3": "hotfix",
                "System.CommandLine@2.0.7": "approved"
            }
            """);

        Assert.True(BypassList.TryGet("Newtonsoft.Json", "13.0.3", out var reason1));
        Assert.Equal("hotfix", reason1);

        Assert.True(BypassList.TryGet("System.CommandLine", "2.0.7", out var reason2));
        Assert.Equal("approved", reason2);
    }
}
