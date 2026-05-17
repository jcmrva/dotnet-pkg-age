using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace DotnetPkgAge;

public static class PackageListReader
{
    public static IReadOnlyList<(string Package, string Version)> ReadDirectoryPackagesProps(string path)
    {
        var doc = XDocument.Load(path);
        return doc.Descendants("PackageVersion")
            .Select(el => (
                Package: el.Attribute("Include")?.Value,
                Version: el.Attribute("Version")?.Value))
            .Where(p => p.Package is not null && p.Version is not null)
            .Select(p => (p.Package!, p.Version!))
            .ToList();
    }

    public static IReadOnlyList<(string Package, string Version)> ReadPackagesLockJson(
        string path,
        bool directOnly = false)
    {
        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);

        if (!doc.RootElement.TryGetProperty("dependencies", out var deps))
            return [];

        var seen = new HashSet<(string, string)>();
        var results = new List<(string Package, string Version)>();

        foreach (var framework in deps.EnumerateObject())
        {
            foreach (var pkg in framework.Value.EnumerateObject())
            {
                if (directOnly &&
                    pkg.Value.TryGetProperty("type", out var type) &&
                    !"Direct".Equals(type.GetString(), StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!pkg.Value.TryGetProperty("resolved", out var resolved))
                    continue;

                var version = resolved.GetString();
                if (version is null) continue;

                var entry = (pkg.Name, version);
                if (seen.Add(entry))
                    results.Add(entry);
            }
        }

        return results;
    }
}
