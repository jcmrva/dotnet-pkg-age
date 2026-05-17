namespace DotnetPkgAge.Tests;

public class CacheTests : IDisposable
{
    private readonly string _tempDir;

    public CacheTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        Cache.CachePath = Path.Combine(_tempDir, "cache.json");
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenCacheIsEmpty()
    {
        var found = Cache.TryGet("Newtonsoft.Json", "13.0.3", out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGet_ReturnsTrue_AfterSet()
    {
        var published = new DateTimeOffset(2022, 6, 13, 0, 0, 0, TimeSpan.Zero);
        Cache.Set("Newtonsoft.Json", "13.0.3", published);

        var found = Cache.TryGet("Newtonsoft.Json", "13.0.3", out var result);

        Assert.True(found);
        Assert.Equal(published, result);
    }

    [Fact]
    public void TryGet_ReturnsFalse_ForUnknownVersion()
    {
        Cache.Set("Newtonsoft.Json", "13.0.3", DateTimeOffset.UtcNow);

        var found = Cache.TryGet("Newtonsoft.Json", "12.0.0", out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGet_ReturnsFalse_ForUnknownPackage()
    {
        Cache.Set("Newtonsoft.Json", "13.0.3", DateTimeOffset.UtcNow);

        var found = Cache.TryGet("SomeOtherPackage", "13.0.3", out _);

        Assert.False(found);
    }

    [Fact]
    public void Set_Overwrites_ExistingEntry()
    {
        var original = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var updated = new DateTimeOffset(2023, 6, 15, 0, 0, 0, TimeSpan.Zero);
        Cache.Set("Newtonsoft.Json", "13.0.3", original);
        Cache.Set("Newtonsoft.Json", "13.0.3", updated);

        Cache.TryGet("Newtonsoft.Json", "13.0.3", out var result);

        Assert.Equal(updated, result);
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenCacheFileIsCorrupted()
    {
        File.WriteAllText(Cache.CachePath, "not valid json {{{{");

        var found = Cache.TryGet("Newtonsoft.Json", "13.0.3", out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGet_WritesWarningToStderr_WhenCacheFileIsCorrupted()
    {
        File.WriteAllText(Cache.CachePath, "not valid json {{{{");

        var stderr = CaptureStderr(() => Cache.TryGet("Newtonsoft.Json", "13.0.3", out _));

        Assert.Contains("Warning:", stderr);
        Assert.Contains("cache", stderr);
    }

    [Fact]
    public void Set_WritesWarningToStderr_WhenCachePathIsNotWritable()
    {
        Cache.CachePath = _tempDir; // directory, not a file — WriteAllText will throw

        var stderr = CaptureStderr(() => Cache.Set("Newtonsoft.Json", "13.0.3", DateTimeOffset.UtcNow));

        Assert.Contains("Warning:", stderr);
        Assert.Contains("cache", stderr);
    }

    [Fact]
    public void Set_PersistsAcrossMultipleEntries()
    {
        var date1 = new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var date2 = new DateTimeOffset(2022, 6, 1, 0, 0, 0, TimeSpan.Zero);
        Cache.Set("PackageA", "1.0.0", date1);
        Cache.Set("PackageB", "2.0.0", date2);

        Cache.TryGet("PackageA", "1.0.0", out var result1);
        Cache.TryGet("PackageB", "2.0.0", out var result2);

        Assert.Equal(date1, result1);
        Assert.Equal(date2, result2);
    }

    [Fact]
    public void ClearAll_RemovesAllEntries()
    {
        Cache.Set("PackageA", "1.0.0", DateTimeOffset.UtcNow);
        Cache.Set("PackageB", "2.0.0", DateTimeOffset.UtcNow);

        Cache.ClearAll();

        Assert.False(Cache.TryGet("PackageA", "1.0.0", out _));
        Assert.False(Cache.TryGet("PackageB", "2.0.0", out _));
    }

    [Fact]
    public void ClearAll_DoesNotThrow_WhenCacheIsEmpty()
    {
        var ex = Record.Exception(() => Cache.ClearAll());

        Assert.Null(ex);
    }

    [Fact]
    public void Evict_RemovesAllVersionsOfPackage()
    {
        Cache.Set("Newtonsoft.Json", "12.0.3", DateTimeOffset.UtcNow);
        Cache.Set("Newtonsoft.Json", "13.0.3", DateTimeOffset.UtcNow);
        Cache.Set("PackageB", "1.0.0", DateTimeOffset.UtcNow);

        Cache.Evict("Newtonsoft.Json");

        Assert.False(Cache.TryGet("Newtonsoft.Json", "12.0.3", out _));
        Assert.False(Cache.TryGet("Newtonsoft.Json", "13.0.3", out _));
        Assert.True(Cache.TryGet("PackageB", "1.0.0", out _));
    }

    [Fact]
    public void Evict_ReturnsCountOfRemovedEntries()
    {
        Cache.Set("Newtonsoft.Json", "12.0.3", DateTimeOffset.UtcNow);
        Cache.Set("Newtonsoft.Json", "13.0.3", DateTimeOffset.UtcNow);

        var removed = Cache.Evict("Newtonsoft.Json");

        Assert.Equal(2, removed);
    }

    [Fact]
    public void Evict_ReturnsZero_WhenPackageNotInCache()
    {
        var removed = Cache.Evict("NonExistentPackage");

        Assert.Equal(0, removed);
    }

    [Fact]
    public void Evict_IsCaseInsensitive()
    {
        Cache.Set("Newtonsoft.Json", "13.0.3", DateTimeOffset.UtcNow);

        var removed = Cache.Evict("newtonsoft.json");

        Assert.Equal(1, removed);
        Assert.False(Cache.TryGet("Newtonsoft.Json", "13.0.3", out _));
    }

    private static string CaptureStderr(Action action)
    {
        var writer = new StringWriter();
        var original = Console.Error;
        try
        {
            Console.SetError(writer);
            action();
        }
        finally
        {
            Console.SetError(original);
        }
        return writer.ToString();
    }
}
