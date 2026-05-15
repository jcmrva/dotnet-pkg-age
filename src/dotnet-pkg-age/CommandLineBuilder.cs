using System;
using System.CommandLine;
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

        Command pkgCommand = new("package", "Check a specific package")
        {
            nameArg,
            versionArg,
            ageArg,
            cacheFileOption
        };

        RootCommand rootCommand = new("A dotnet tool to check the age of NuGet packages");

        pkgCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            string packageName = parseResult.GetValue(nameArg)!;
            int minAgeDays = parseResult.GetValue(ageArg);
            string version = parseResult.GetValue(versionArg)!;
            if (parseResult.GetValue(cacheFileOption) is { } cacheFile)
                Cache.CachePath = cacheFile;
            await HandleCommand(packageName, version, minAgeDays);
        });

        rootCommand.Subcommands.Add(pkgCommand);

        return rootCommand;
    }

    private static async Task HandleCommand(string packageName, string version, int minAgeDays = 0)
    {
        Console.WriteLine($"Package {packageName} {version} should be at least {minAgeDays} days old.");

        bool meetsRequirement = await NuGetAPI.GetPackageInfo(packageName, version, minAgeDays);

        if (meetsRequirement)
        {
            Console.WriteLine($"Package {packageName} {version} meets the age requirement.");
        }
        else
        {
            Console.WriteLine($"Package {packageName} {version} does NOT meet the age requirement.");
        }
    }
}