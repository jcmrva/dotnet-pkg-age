using System;
using System.CommandLine;

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

        Command pkgCommand = new("package", "Check a specific package")
        {
            nameArg,
            ageArg
        };

        RootCommand rootCommand = new("A dotnet tool to check the age of a NuGet package");

        pkgCommand.SetAction(parseResult =>
        {
            string packageName = parseResult.GetValue(nameArg)!;
            int minAgeDays = parseResult.GetValue(ageArg) is string ageStr && int.TryParse(ageStr, out int age) ? age : 0;
            HandleCommand(packageName, minAgeDays);
        });

        rootCommand.Subcommands.Add(pkgCommand);

        return rootCommand;
    }

    private static void HandleCommand(string packageName, int minAgeDays = 0)
    {
        // TODO: implement

        Console.WriteLine($"Package {packageName} should be at least {minAgeDays} days old.");
    }
}