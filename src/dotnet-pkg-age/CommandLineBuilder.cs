using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DotnetPkgAge;

public static class CommandLineBuilder
{
    public static RootCommand Build()
    {
        Argument<string> nameArg = new("name")
        {
            Description = "The package to check"
        };

        Argument<int> ageArg = new("min-age-days")
        {
            Description = "Desired minimum age of the package in days"
        };

        Argument<string> versionArg = new("version")
        {
            Description = "The package version to check"
        };

        Option<string?> cacheFileOption = new("--cache-file", "-c")
        {
            Description = "Path to the cache file (default: ~/.dotnet-pkg-age/cache.json)"
        };

        Option<string> formatOption = new("--format", "-f")
        {
            Description = "Output format: text (default) or json",
            DefaultValueFactory = _ => "text"
        };
        formatOption.AcceptOnlyFromAmong("text", "json");

        Option<bool> ignoreBypassOption = new("--ignore-bypass")
        {
            Description = $"Ignore the bypass list at {BypassList.DefaultPath}"
        };

        Command pkgCommand = new("package", "Check a specific package")
        {
            nameArg,
            versionArg,
            ageArg,
            cacheFileOption,
            formatOption,
            ignoreBypassOption
        };

        RootCommand rootCommand = new("A dotnet tool to check the age of NuGet packages");

        pkgCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            string packageName = parseResult.GetValue(nameArg)!;
            int minAgeDays = parseResult.GetValue(ageArg);
            string version = parseResult.GetValue(versionArg)!;
            string format = parseResult.GetValue(formatOption)!;
            bool ignoreBypass = parseResult.GetValue(ignoreBypassOption);
            if (parseResult.GetValue(cacheFileOption) is { } cacheFile)
                Cache.CachePath = cacheFile;
            return await HandleCommand(packageName, version, minAgeDays, format, ignoreBypass);
        });

        Option<bool> clearAllOption = new("--clear-all")
        {
            Description = "Remove all entries from the cache"
        };

        Option<string?> evictPkgOption = new("--evict-pkg")
        {
            Description = "Remove all cached versions of the specified package"
        };

        Command cacheCommand = new("cache", "Manage the local cache")
        {
            clearAllOption,
            evictPkgOption
        };

        cacheCommand.SetAction(parseResult =>
        {
            if (parseResult.GetValue(clearAllOption))
            {
                Cache.ClearAll();
                Console.WriteLine("Cache cleared.");
            }
            else if (parseResult.GetValue(evictPkgOption) is { } pkg)
            {
                int removed = Cache.Evict(pkg);
                Console.WriteLine($"Evicted {removed} entr{(removed == 1 ? "y" : "ies")} for {pkg}.");
            }
            else
            {
                Console.WriteLine("Specify --clear-all or --evict-pkg <name>.");
            }
        });

        Argument<int> bulkAgeArg = new("min-age-days")
        {
            Description = "Minimum age in days all packages must meet"
        };

        Option<bool> propsOption = new("--props")
        {
            Description = "Check packages from auto-discovered Directory.Packages.props"
        };

        Option<bool> lockFilesOption = new("--lock-files")
        {
            Description = "Check packages from auto-discovered packages.lock.json files"
        };

        Option<bool> directOnlyOption = new("--direct-only")
        {
            Description = "Only check direct dependencies (lock files only)"
        };

        Option<string> bulkFormatOption = new("--format", "-f")
        {
            Description = "Output format: text (default) or json",
            DefaultValueFactory = _ => "text"
        };
        bulkFormatOption.AcceptOnlyFromAmong("text", "json");

        Option<bool> bulkIgnoreBypassOption = new("--ignore-bypass")
        {
            Description = $"Ignore the bypass list at {BypassList.DefaultPath}"
        };

        Command bulkCommand = new("bulk", "Check all packages from project files")
        {
            bulkAgeArg,
            propsOption,
            lockFilesOption,
            directOnlyOption,
            bulkFormatOption,
            bulkIgnoreBypassOption
        };

        bulkCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            int minAgeDays = parseResult.GetValue(bulkAgeArg);
            bool useProps = parseResult.GetValue(propsOption);
            bool useLockFiles = parseResult.GetValue(lockFilesOption);
            bool directOnly = parseResult.GetValue(directOnlyOption);
            string format = parseResult.GetValue(bulkFormatOption)!;
            bool ignoreBypass = parseResult.GetValue(bulkIgnoreBypassOption);

            if (!useProps && !useLockFiles)
            {
                Console.Error.WriteLine("Specify at least one source: --props or --lock-files");
                return 1;
            }

            return await HandleBulkCommand(minAgeDays, useProps, useLockFiles, directOnly, format, ignoreBypass, cancellationToken);
        });

        rootCommand.Subcommands.Add(pkgCommand);
        rootCommand.Subcommands.Add(cacheCommand);
        rootCommand.Subcommands.Add(bulkCommand);

        return rootCommand;
    }

    private static int TypePriority(string? type) => type?.ToLowerInvariant() switch
    {
        "direct"           => 2,
        "centraltransitive" => 1,
        _                  => 0
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static async Task<int> HandleBulkCommand(
        int minAgeDays, bool useProps, bool useLockFiles, bool directOnly,
        string format, bool ignoreBypass, CancellationToken cancellationToken)
    {
        var packages = new Dictionary<(string Package, string Version), string?>();

        if (useProps)
        {
            var path = PackageListReader.FindDirectoryPackagesProps();
            if (path is null) { Console.Error.WriteLine("Error: Directory.Packages.props not found."); return 1; }
            try
            {
                foreach (var pkg in PackageListReader.ReadDirectoryPackagesProps(path))
                    packages.TryAdd((pkg.Package, pkg.Version), null);
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        if (useLockFiles)
        {
            var paths = PackageListReader.FindPackagesLockFiles();
            if (paths.Count == 0) { Console.Error.WriteLine("Error: No packages.lock.json files found."); return 1; }
            foreach (var path in paths)
            {
                try
                {
                    foreach (var pkg in PackageListReader.ReadPackagesLockJson(path, directOnly))
                    {
                        var key = (pkg.Package, pkg.Version);
                        if (!packages.TryGetValue(key, out var existing) || TypePriority(pkg.Type) > TypePriority(existing))
                            packages[key] = pkg.Type;
                    }
                }
                catch (InvalidOperationException ex)
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    return 1;
                }
            }
        }

        if (packages.Count == 0)
        {
            Console.Error.WriteLine("Warning: no packages found in the specified sources.");
            return 0;
        }

        var results = new List<BulkPackageResult>();

        var toCheck = new List<(string Package, string Version)>();
        foreach (var (pkg, depType) in packages)
        {
            BypassList.TryGet(pkg.Package, pkg.Version, out var reason);
            if (reason is not null)
                results.Add(new BulkPackageResult(pkg.Package, pkg.Version,
                    ignoreBypass ? "fail" : "bypass", null, reason, depType));
            else
                toCheck.Add(pkg);
        }

        IReadOnlyDictionary<(string Package, string Version), DateTimeOffset?> published;
        try
        {
            published = await NuGetAPI.GetPublishedDatesBatch(toCheck, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        foreach (var (pkg, date) in published)
        {
            var depType = packages.GetValueOrDefault(pkg);
            if (date is null)
            {
                results.Add(new BulkPackageResult(pkg.Package, pkg.Version, "not_found", null, null, depType));
                continue;
            }
            var ageDays = (int)(DateTimeOffset.Now - date.Value).TotalDays;
            var status = ageDays >= minAgeDays ? "pass" : "fail";
            results.Add(new BulkPackageResult(pkg.Package, pkg.Version, status, ageDays, null, depType));
        }

        results.Sort((a, b) => string.Compare(a.Package, b.Package, StringComparison.OrdinalIgnoreCase));

        var summary = new BulkSummary(
            results.Count(r => r.Status == "pass"),
            results.Count(r => r.Status == "fail"),
            results.Count(r => r.Status == "bypass"),
            results.Count(r => r.Status == "not_found"));

        if (format == "json")
        {
            Console.WriteLine(JsonSerializer.Serialize(new { minAgeDays, results, summary }, JsonOptions));
        }
        else
        {
            foreach (var r in results)
            {
                var label = r.Status.ToUpperInvariant().PadRight(8);
                var typeTag = r.DependencyType is { } t ? $"[{t.ToLowerInvariant()}] " : "";
                var detail = r.Status switch
                {
                    "pass"                              => $"({r.AgeDays} days)",
                    "fail" when r.BypassReason is not null => $"(bypass overridden: {r.BypassReason})",
                    "fail"                              => $"({r.AgeDays} days, requires {minAgeDays})",
                    "bypass"                            => $"({r.BypassReason})",
                    "not_found"                         => "(not found on NuGet)",
                    _                                   => string.Empty
                };
                Console.WriteLine($"{label} {r.Package} {r.Version} {typeTag}{detail}");
            }
            Console.WriteLine();
            Console.WriteLine($"{packages.Count} packages: {summary.Passed} passed, {summary.Failed} failed, {summary.Bypassed} bypassed, {summary.NotFound} not found");
        }

        return summary.Failed > 0 ? 1 : 0;
    }

    private static async Task<int> HandleCommand(string packageName, string version, int minAgeDays = 0, string format = "text", bool ignoreBypass = false)
    {
        BypassList.TryGet(packageName, version, out var bypassReason);

        if (bypassReason is not null)
        {
            if (ignoreBypass)
            {
                if (format == "json")
                    Console.WriteLine(JsonSerializer.Serialize(new { package = packageName, version, bypassed = false, bypassOverridden = true, reason = bypassReason }, JsonOptions));
                else
                    Console.WriteLine($"Package {packageName} {version} FAIL — bypass overridden: {bypassReason}");
                return 1;
            }

            if (format == "json")
                Console.WriteLine(JsonSerializer.Serialize(new { package = packageName, version, bypassed = true, reason = bypassReason }, JsonOptions));
            else
                Console.WriteLine($"Package {packageName} {version} is bypassed: {bypassReason}");
            return 0;
        }

        PackageCheckResult result;
        try
        {
            result = await NuGetAPI.GetPackageInfo(packageName, version, minAgeDays);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        if (format == "json")
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        else
        {
            Console.WriteLine($"Package {packageName} {version} should be at least {minAgeDays} days old.");
            if (result.MeetsRequirement)
                Console.WriteLine($"Package {packageName} {version} meets the age requirement.");
            else
                Console.WriteLine($"Package {packageName} {version} does NOT meet the age requirement.");
        }

        return result.MeetsRequirement ? 0 : 1;
    }
}