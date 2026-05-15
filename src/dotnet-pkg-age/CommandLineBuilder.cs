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

        Argument<string> ageArg = new("min-age-days")
        {
            Description = "Desired minimum age of the package in days"
        };

        Argument<string> versionArg = new("version")
        {
            Description = "The package version to check"
        };

        Command pkgCommand = new("package", "Check a specific package")
        {
            nameArg,
            versionArg,
            ageArg
        };

        RootCommand rootCommand = new("A dotnet tool to check the age of a NuGet package");

        pkgCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            string packageName = parseResult.GetValue(nameArg)!;
            int minAgeDays = parseResult.GetValue(ageArg) is string ageStr && int.TryParse(ageStr, out int age) ? age : 0;
            string version = parseResult.GetValue(versionArg)!;
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