using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DotnetPkgAge;

public static class BypassList
{
    internal static string DefaultPath { get; set; } = Path.Combine(".config", "pkg-age-bypass.json");

    public static bool TryGet(string packageName, string version, out string? reason)
    {
        reason = null;
        var entries = Load();
        return entries.TryGetValue(Key(packageName, version), out reason);
    }

    private static Dictionary<string, string> Load()
    {
        if (!File.Exists(DefaultPath))
            return [];

        try
        {
            var json = File.ReadAllText(DefaultPath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string Key(string packageName, string version) => $"{packageName}@{version}";
}
