using System;
using System.CommandLine;
using System.Text.Json;
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

        Command pkgCommand = new("package", "Check a specific package")
        {
            nameArg,
            versionArg,
            ageArg,
            cacheFileOption,
            formatOption
        };

        RootCommand rootCommand = new("A dotnet tool to check the age of NuGet packages");

        pkgCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            string packageName = parseResult.GetValue(nameArg)!;
            int minAgeDays = parseResult.GetValue(ageArg);
            string version = parseResult.GetValue(versionArg)!;
            string format = parseResult.GetValue(formatOption)!;
            if (parseResult.GetValue(cacheFileOption) is { } cacheFile)
                Cache.CachePath = cacheFile;
            return await HandleCommand(packageName, version, minAgeDays, format);
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

        rootCommand.Subcommands.Add(pkgCommand);
        rootCommand.Subcommands.Add(cacheCommand);

        return rootCommand;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static async Task<int> HandleCommand(string packageName, string version, int minAgeDays = 0, string format = "text")
    {
        var result = await NuGetAPI.GetPackageInfo(packageName, version, minAgeDays);

        if (format == "json")
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        }
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