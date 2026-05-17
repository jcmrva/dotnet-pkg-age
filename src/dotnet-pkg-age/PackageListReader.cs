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
        XDocument doc;
        try
        {
            doc = XDocument.Load(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            throw new InvalidOperationException(
                $"Failed to parse {Path.GetFileName(path)}: {ex.Message}", ex);
        }

        return doc.Descendants("PackageVersion")
            .Select(el => (
                Package: el.Attribute("Include")?.Value,
                Version: el.Attribute("Version")?.Value))
            .Where(p => p.Package is not null && p.Version is not null)
            .Select(p => (p.Package!, p.Version!))
            .ToList();
    }

    public static string? FindDirectoryPackagesProps(string? startDir = null)
    {
        var dir = new DirectoryInfo(startDir ?? Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Directory.Packages.props");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    public static IReadOnlyList<string> FindPackagesLockFiles(string? startDir = null)
    {
        var root = startDir ?? Directory.GetCurrentDirectory();
        return [..Directory.EnumerateFiles(root, "packages.lock.json", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Contains("obj"))];
    }

    public static IReadOnlyList<(string Package, string Version, string Type)> ReadPackagesLockJson(
        string path,
        bool directOnly = false)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);

            if (!doc.RootElement.TryGetProperty("dependencies", out var deps))
                return [];

            var seen = new HashSet<(string, string)>();
            var results = new List<(string Package, string Version, string Type)>();

            foreach (var framework in deps.EnumerateObject())
            {
                foreach (var pkg in framework.Value.EnumerateObject())
                {
                    var typeStr = pkg.Value.TryGetProperty("type", out var typeProp)
                        ? typeProp.GetString() ?? "Transitive"
                        : "Transitive";

                    if (directOnly && !typeStr.Equals("Direct", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!pkg.Value.TryGetProperty("resolved", out var resolved))
                        continue;

                    var version = resolved.GetString();
                    if (version is null) continue;

                    if (seen.Add((pkg.Name, version)))
                        results.Add((pkg.Name, version, typeStr));
                }
            }

            return results;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidOperationException(
                $"Failed to parse {Path.GetFileName(path)}: {ex.Message}", ex);
        }
    }
}
