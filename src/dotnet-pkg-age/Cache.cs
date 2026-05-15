using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DotnetPkgAge;

public static class Cache
{
    internal static string CachePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".dotnet-pkg-age",
        "cache.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static bool TryGet(string packageName, string version, out DateTimeOffset published)
    {
        published = default;
        var entries = Load();
        return entries.TryGetValue(Key(packageName, version), out published);
    }

    public static void Set(string packageName, string version, DateTimeOffset published)
    {
        var entries = Load();
        entries[Key(packageName, version)] = published;
        Save(entries);
    }

    private static Dictionary<string, DateTimeOffset> Load()
    {
        if (!File.Exists(CachePath))
            return [];

        try
        {
            var json = File.ReadAllText(CachePath);
            return JsonSerializer.Deserialize<Dictionary<string, DateTimeOffset>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void Save(Dictionary<string, DateTimeOffset> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
        File.WriteAllText(CachePath, JsonSerializer.Serialize(entries, JsonOptions));
    }

    private static string Key(string packageName, string version) => $"{packageName}@{version}";
}
